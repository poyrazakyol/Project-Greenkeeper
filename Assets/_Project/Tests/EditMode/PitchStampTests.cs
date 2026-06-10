using Greenkeeper.Pitch;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// GPU smoke test for the brush stamp pipeline — the automated half of the
    /// F0.3 "a test brush leaves a mark on the RT" verification. Uses ReadPixels,
    /// which is fine here: the no-GPU-readback rule applies to game logic, not
    /// tests. Requires a real graphics device (run batchmode WITHOUT -nographics).
    /// </summary>
    public class PitchStampTests
    {
        private const int RtSize = 64;

        private RenderTexture _rt;
        private Texture2D _readback;
        private Material _material;
        private Mesh _quad;
        private CommandBuffer _commandBuffer;
        private PitchBounds _bounds;

        [SetUp]
        public void SetUp()
        {
            _rt = new RenderTexture(RtSize, RtSize, 0, RenderTextureFormat.ARGB32);
            _rt.Create();
            _readback = new Texture2D(RtSize, RtSize, TextureFormat.RGBA32, false);
            Shader shader = Shader.Find("Greenkeeper/PitchBrush");
            Assert.That(shader, Is.Not.Null, "Greenkeeper/PitchBrush shader not found.");
            _material = new Material(shader);
            _quad = PitchStampRenderer.CreateQuadMesh();
            _commandBuffer = new CommandBuffer { name = "PitchStampTests" };
            // 64x64 m area centered on origin -> 1 texel per meter.
            _bounds = new PitchBounds(new Vector2(-32f, -32f), new Vector2(64f, 64f));
        }

        [TearDown]
        public void TearDown()
        {
            _commandBuffer.Dispose();
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_quad);
            Object.DestroyImmediate(_readback);
            _rt.Release();
            Object.DestroyImmediate(_rt);
        }

        private void ExecuteStamp(in StampRequest stamp)
        {
            _commandBuffer.Clear();
            _commandBuffer.SetRenderTarget(_rt);
            _commandBuffer.ClearRenderTarget(false, true, Color.clear);
            PitchStampRenderer.AppendStamp(
                _commandBuffer, _rt, _quad, _material, new MaterialPropertyBlock(), stamp, _bounds);
            Graphics.ExecuteCommandBuffer(_commandBuffer);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0, 0, RtSize, RtSize), 0, 0);
            _readback.Apply();
            RenderTexture.active = previous;
        }

        [Test]
        public void CenterStamp_LeavesMark_AndCornerStaysClear()
        {
            ExecuteStamp(new StampRequest
            {
                Target = PitchRenderTarget.Paint,
                WorldPosition = Vector3.zero,
                WorldRadius = 8f,
                RotationRad = 0f,
                Value = Color.white,
                Hardness = 1f
            });

            Color center = _readback.GetPixel(RtSize / 2, RtSize / 2);
            Color corner = _readback.GetPixel(2, 2);
            Assert.That(center.r, Is.GreaterThan(0.5f), "Stamp left no mark at the RT center.");
            Assert.That(corner.r, Is.LessThan(0.05f), "Stamp bled into the far corner.");
        }

        [Test]
        public void OffCenterStamp_LandsAtMappedTexel()
        {
            // World (16, 0, 16) -> UV (0.75, 0.75) -> texel (48, 48) in Unity
            // bottom-left convention. Guards the world->UV->NDC orientation
            // (including the platform Y-flip) end to end.
            ExecuteStamp(new StampRequest
            {
                Target = PitchRenderTarget.Paint,
                WorldPosition = new Vector3(16f, 0f, 16f),
                WorldRadius = 4f,
                RotationRad = 0f,
                Value = Color.white,
                Hardness = 1f
            });

            Color expected = _readback.GetPixel(48, 48);
            Color mirroredY = _readback.GetPixel(48, 16);
            Color centerPixel = _readback.GetPixel(32, 32);
            Assert.That(expected.r, Is.GreaterThan(0.5f),
                "Stamp did not land at the texel mapped from world space — check the Y flip.");
            Assert.That(mirroredY.r, Is.LessThan(0.05f),
                "Stamp landed at the Y-mirrored texel — the platform flip is inverted.");
            Assert.That(centerPixel.r, Is.LessThan(0.05f));
        }

        [Test]
        public void SoftStamp_FallsOffTowardEdge()
        {
            ExecuteStamp(new StampRequest
            {
                Target = PitchRenderTarget.Paint,
                WorldPosition = Vector3.zero,
                WorldRadius = 16f,
                RotationRad = 0f,
                Value = Color.white,
                Hardness = 0f
            });

            float center = _readback.GetPixel(32, 32).r;
            float halfway = _readback.GetPixel(32 + 8, 32).r;
            float edge = _readback.GetPixel(32 + 15, 32).r;
            Assert.That(center, Is.GreaterThan(halfway));
            Assert.That(halfway, Is.GreaterThan(edge));
        }
    }
}
