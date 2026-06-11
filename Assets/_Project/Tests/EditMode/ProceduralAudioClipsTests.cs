using Greenkeeper.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Procedural placeholder audio buffers (F0.7): deterministic, in range,
    /// and loop-seam continuous (the loops play for minutes while mowing —
    /// a clicking seam would ruin the ASMR goal).
    /// </summary>
    public class ProceduralAudioClipsTests
    {
        private const int SampleRate = ProceduralAudioClips.SampleRate;

        private static void AssertValidBuffer(float[] samples)
        {
            Assert.That(samples.Length, Is.GreaterThan(0));
            foreach (float sample in samples)
            {
                Assert.That(float.IsNaN(sample), Is.False, "Buffer contains NaN.");
                Assert.That(sample, Is.InRange(-1f, 1f));
            }
        }

        private static void AssertSeamContinuity(float[] samples, float maxStep)
        {
            // On wrap the last sample is followed by the first: the step must
            // be no larger than typical adjacent-sample steps in the buffer.
            float seamStep = Mathf.Abs(samples[0] - samples[samples.Length - 1]);
            Assert.That(seamStep, Is.LessThanOrEqualTo(maxStep),
                $"Loop seam steps by {seamStep:F4} — audible click.");
        }

        [Test]
        public void EngineLoop_IsDeterministic_InRange_AndSeamless()
        {
            float[] a = ProceduralAudioClips.GenerateEngineLoopSamples(55f, 2f);
            float[] b = ProceduralAudioClips.GenerateEngineLoopSamples(55f, 2f);

            AssertValidBuffer(a);
            Assert.That(a, Is.EqualTo(b), "Generation must be deterministic.");
            // 55 Hz fundamental at 44.1 kHz: adjacent-sample step is tiny.
            AssertSeamContinuity(a, 0.05f);
        }

        [Test]
        public void SwishLoop_IsDeterministic_InRange_AndSeamless()
        {
            float[] a = ProceduralAudioClips.GenerateSwishLoopSamples(1.5f);
            float[] b = ProceduralAudioClips.GenerateSwishLoopSamples(1.5f);

            AssertValidBuffer(a);
            Assert.That(a, Is.EqualTo(b));
            // Band-limited noise: adjacent steps stay moderate; the seam must too.
            AssertSeamContinuity(a, 0.35f);
        }

        [Test]
        public void Beep_HasEnvelope_NoClickAtStartOrEnd()
        {
            float[] samples = ProceduralAudioClips.GenerateBeepSamples(880f, 0.35f);
            AssertValidBuffer(samples);
            Assert.That(Mathf.Abs(samples[0]), Is.LessThan(0.01f), "Attack must start at silence.");
            Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.02f), "Decay must end near silence.");
        }

        [Test]
        public void Sting_IsInRange_AndEndsNearSilence()
        {
            float[] samples = ProceduralAudioClips.GenerateStingSamples();
            AssertValidBuffer(samples);
            Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.02f));
        }
    }
}
