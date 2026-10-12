using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// GPU smoke test for the visual moisture sync (M.3 fix): raw CPU moisture
    /// bytes (0-100) blitted through PitchMoistureSync must land in the B
    /// channel as moisture/100 — the same mapping as GrassStateEncoding — while
    /// R (mower height), G (health) and A (damage) stay untouched (ColorMask B).
    /// Linear textures so the check is exact arithmetic (no sRGB round trip).
    /// Requires a real graphics device (run batchmode WITHOUT -nographics).
    /// </summary>
    public class PitchMoistureSyncTests
    {
        private const int Size = 8;

        [TestCase(0, 0f)]
        [TestCase(50, 0.5f)]
        [TestCase(85, 0.85f)]
        [TestCase(100, 1f)]
        public void Sync_WritesMoistureToBlueOnly(int moisture, float expectedBlue)
        {
            Shader shader = Shader.Find("Greenkeeper/PitchMoistureSync");
            Assert.That(shader, Is.Not.Null, "Greenkeeper/PitchMoistureSync shader not found.");
            var material = new Material(shader);

            var source = new Texture2D(Size, Size, TextureFormat.R8, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var bytes = new byte[Size * Size];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)moisture;
            }
            source.SetPixelData(bytes, 0);
            source.Apply(false);

            var target = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create();
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);

            try
            {
                // Pre-existing grass state: height 0.7, health 0.3, damage 0.2.
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                GL.Clear(false, true, new Color(0.7f, 0.3f, 0f, 0.2f));
                RenderTexture.active = previous;

                Graphics.Blit(source, target, material);

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;

                Color c = readback.GetPixel(Size / 2, Size / 2);
                Assert.That(c.b, Is.EqualTo(expectedBlue).Within(0.02f), "B must mirror CPU moisture / 100.");
                Assert.That(c.r, Is.EqualTo(0.7f).Within(0.02f), "R (mower height) must survive the sync.");
                Assert.That(c.g, Is.EqualTo(0.3f).Within(0.02f), "G (health) must survive the sync.");
                Assert.That(c.a, Is.EqualTo(0.2f).Within(0.02f), "A (damage) must survive the sync.");
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(readback);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
