using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Pitch
{
    /// <summary>Which render texture a stamp writes into (TDD §3.2).</summary>
    public enum PitchRenderTarget
    {
        GrassState,
        Pattern,
        Paint
    }

    /// <summary>
    /// One brush stamp request. Tools and debug brushes enqueue these on the
    /// PitchRenderBridge; all stamps of a frame are recorded into a single
    /// CommandBuffer (TDD §3.2: quad-stamp, not Blit).
    /// </summary>
    public struct StampRequest
    {
        public PitchRenderTarget Target;
        public Vector3 WorldPosition;
        public float WorldRadius;     // meters
        public float RotationRad;
        public Color Value;           // channel semantics belong to the consumer (F0.4+)
        public float Hardness;        // 0 = soft falloff, 1 = hard edge
    }

    /// <summary>
    /// Stateless stamp recording core, separated from the scene component so
    /// EditMode tests can drive it against a throwaway RT without a scene.
    /// </summary>
    public static class PitchStampRenderer
    {
        private static readonly int CenterUvId = Shader.PropertyToID("_CenterUv");
        private static readonly int RadiusUvId = Shader.PropertyToID("_RadiusUv");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int ValueId = Shader.PropertyToID("_Value");
        private static readonly int HardnessId = Shader.PropertyToID("_Hardness");
        private static readonly int FlipYId = Shader.PropertyToID("_FlipY");

        public static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "PitchBrushQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }

        /// <summary>
        /// Records one stamp draw into the command buffer. The property block
        /// is copied at record time, so a single block can be reused across calls.
        /// </summary>
        public static void AppendStamp(CommandBuffer commandBuffer, RenderTexture target, Mesh quad,
            Material brushMaterial, MaterialPropertyBlock propertyBlock,
            in StampRequest stamp, in PitchBounds bounds)
        {
            Vector2 centerUv = bounds.WorldToUv(stamp.WorldPosition);
            var radiusUv = new Vector2(
                stamp.WorldRadius / bounds.Size.x,
                stamp.WorldRadius / bounds.Size.y);

            propertyBlock.Clear();
            propertyBlock.SetVector(CenterUvId, centerUv);
            propertyBlock.SetVector(RadiusUvId, radiusUv);
            propertyBlock.SetFloat(RotationId, stamp.RotationRad);
            propertyBlock.SetColor(ValueId, stamp.Value);
            propertyBlock.SetFloat(HardnessId, Mathf.Clamp01(stamp.Hardness));
            // NDC y points up while D3D-style render targets store row 0 at the
            // top; without this flip, written and sampled v coordinates diverge.
            propertyBlock.SetFloat(FlipYId, SystemInfo.graphicsUVStartsAtTop ? 1f : 0f);

            commandBuffer.SetRenderTarget(target);
            commandBuffer.DrawMesh(quad, Matrix4x4.identity, brushMaterial, 0, 0, propertyBlock);
        }
    }
}
