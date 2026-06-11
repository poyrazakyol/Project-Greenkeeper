using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Deterministic placeholder audio synthesis (F0.7, Poyraz-approved):
    /// used whenever a ToolDefinitionSO clip field is left empty, so real
    /// samples can replace these later without code changes. Sample buffer
    /// generation is pure (static float[] in, no engine state) and EditMode
    /// tested; only CreateClip touches the AudioClip API.
    /// </summary>
    public static class ProceduralAudioClips
    {
        public const int SampleRate = 44100;
        private const float LoopCrossfadeSeconds = 0.05f;

        /// <summary>
        /// Mower engine loop: fundamental + harmonics + a little noise.
        /// Harmonic frequencies are snapped to whole cycles over the LOOP
        /// length (phase-exact at the seam); the noise component is made
        /// seamless by the overlap-trim in MakeSeamlessLoop.
        /// </summary>
        public static float[] GenerateEngineLoopSamples(float fundamentalHz, float seconds, int sampleRate = SampleRate)
        {
            int loopCount = Mathf.Max(sampleRate / 10, Mathf.RoundToInt(seconds * sampleRate));
            int fade = FadeSamples(loopCount, sampleRate);
            float loopDuration = loopCount / (float)sampleRate;

            // Integer cycle counts over the trimmed loop duration.
            float f1 = Mathf.Round(fundamentalHz * loopDuration) / loopDuration;
            float f2 = Mathf.Round(fundamentalHz * 2.02f * loopDuration) / loopDuration;
            float f3 = Mathf.Round(fundamentalHz * 2.94f * loopDuration) / loopDuration;
            float fSub = Mathf.Round(fundamentalHz * 0.5f * loopDuration) / loopDuration;

            var raw = new float[loopCount + fade];
            uint noiseState = 0x9E3779B9u;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)sampleRate;
                float value =
                    0.50f * Mathf.Sin(2f * Mathf.PI * f1 * t) +
                    0.22f * Mathf.Sin(2f * Mathf.PI * f2 * t) +
                    0.12f * Mathf.Sin(2f * Mathf.PI * f3 * t) +
                    0.18f * Mathf.Sin(2f * Mathf.PI * fSub * t) +
                    0.06f * (NextNoise(ref noiseState) * 2f - 1f);
                raw[i] = value * 0.55f; // headroom: |sum| can reach ~1.08
            }
            return MakeSeamlessLoop(raw, loopCount, fade);
        }

        /// <summary>
        /// Cutting swish loop: band-limited noise (fast low-pass minus slow
        /// low-pass) with a gentle amplitude flutter; seam handled by
        /// MakeSeamlessLoop.
        /// </summary>
        public static float[] GenerateSwishLoopSamples(float seconds, int sampleRate = SampleRate)
        {
            int loopCount = Mathf.Max(sampleRate / 10, Mathf.RoundToInt(seconds * sampleRate));
            int fade = FadeSamples(loopCount, sampleRate);

            var raw = new float[loopCount + fade];
            uint noiseState = 0x6C078965u;
            float fastLp = 0f;
            float slowLp = 0f;
            const float fastAlpha = 0.30f; // ~2.5 kHz corner
            const float slowAlpha = 0.04f; // ~300 Hz corner

            for (int i = 0; i < raw.Length; i++)
            {
                float white = NextNoise(ref noiseState) * 2f - 1f;
                fastLp += fastAlpha * (white - fastLp);
                slowLp += slowAlpha * (white - slowLp);
                float band = fastLp - slowLp;

                float t = i / (float)sampleRate;
                float flutter = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                raw[i] = Mathf.Clamp(band * flutter * 1.6f, -1f, 1f);
            }
            return MakeSeamlessLoop(raw, loopCount, fade);
        }

        /// <summary>Single beep with attack/decay envelope (basket full).</summary>
        public static float[] GenerateBeepSamples(float frequencyHz, float seconds, int sampleRate = SampleRate)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / 0.01f);
                float decay = Mathf.Clamp01(1f - t / seconds);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequencyHz * t) * attack * decay * decay * 0.6f;
            }
            return samples;
        }

        /// <summary>Completion sting: short rising major arpeggio.</summary>
        public static float[] GenerateStingSamples(int sampleRate = SampleRate)
        {
            float[] notesHz = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
            const float noteSpacing = 0.12f;
            const float noteLength = 0.5f;
            float total = noteSpacing * (notesHz.Length - 1) + noteLength;

            int count = Mathf.RoundToInt(total * sampleRate);
            var samples = new float[count];
            for (int n = 0; n < notesHz.Length; n++)
            {
                int start = Mathf.RoundToInt(n * noteSpacing * sampleRate);
                int length = Mathf.Min(Mathf.RoundToInt(noteLength * sampleRate), count - start);
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)sampleRate;
                    float envelope = Mathf.Clamp01(t / 0.01f) * Mathf.Pow(1f - t / noteLength, 2f);
                    samples[start + i] += Mathf.Sin(2f * Mathf.PI * notesHz[n] * t) * envelope * 0.30f;
                }
            }
            for (int i = 0; i < count; i++)
            {
                samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            }
            return samples;
        }

        public static AudioClip CreateClip(string clipName, float[] samples, int sampleRate = SampleRate)
        {
            var clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Xorshift PRNG — deterministic across runs and platforms.</summary>
        private static float NextNoise(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x1000000;
        }

        private static int FadeSamples(int loopCount, int sampleRate)
        {
            return Mathf.Min(Mathf.RoundToInt(LoopCrossfadeSeconds * sampleRate), loopCount / 4);
        }

        /// <summary>
        /// Overlap-trim: the raw buffer is loopCount + fade samples long; the
        /// extra tail is crossfaded into the head and trimmed. The last output
        /// sample (raw[loopCount-1]) is then followed on wrap by output[0]
        /// (= raw[loopCount] at fade weight 0) — consecutive samples of the
        /// original stream, so the seam is click-free.
        /// </summary>
        private static float[] MakeSeamlessLoop(float[] raw, int loopCount, int fade)
        {
            var output = new float[loopCount];
            System.Array.Copy(raw, output, loopCount);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                output[i] = Mathf.Lerp(raw[loopCount + i], raw[i], t);
            }
            return output;
        }
    }
}
