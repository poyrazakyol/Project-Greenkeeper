using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// GPU smoke test for the day-end paint fade (M.2 fix): the fade shader
    /// must subtract its amount from RT_Paint.r so painted lines dim over days.
    /// Requires a real graphics device (run batchmode WITHOUT -nographics).
    /// </summary>
    public class PitchPaintFadeTests
    {
        private const int Size = 16;

        [Test]
        public void FadeShader_SubtractsFromRedChannel_AndClampsAtZero()
        {
            Shader shader = Shader.Find("Greenkeeper/PitchPaintFade");
            Assert.That(shader, Is.Not.Null, "Greenkeeper/PitchPaintFade shader not found.");
            var material = new Material(shader);

            // Linear RTs so the subtract is validated as exact arithmetic,
            // free of sRGB gamma round-trips on clear/sample/store.
            var source = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var dest = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, linear: true);
            source.Create();
            dest.Create();

            try
            {
                // Source red = 0.8 everywhere.
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = source;
                GL.Clear(false, true, new Color(0.8f, 0f, 0f, 0f));
                RenderTexture.active = previous;

                // Fade by 0.3 -> expect 0.5.
                material.SetFloat("_FadeAmount", 0.3f);
                Graphics.Blit(source, dest, material);

                Assert.That(ReadRed(dest, readback), Is.EqualTo(0.5f).Within(0.02f));

                // Fade by 0.9 from 0.5 -> clamps at 0.
                material.SetFloat("_FadeAmount", 0.9f);
                Graphics.Blit(dest, source, material);
                Assert.That(ReadRed(source, readback), Is.EqualTo(0f).Within(0.02f));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(readback);
                source.Release();
                dest.Release();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(dest);
            }
        }

        private static float ReadRed(RenderTexture rt, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            return readback.GetPixel(Size / 2, Size / 2).r;
        }
    }
}
