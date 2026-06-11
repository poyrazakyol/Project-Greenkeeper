using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Owns the GPU render texture set (TDD §3.2) and executes the per-frame
    /// stamp queue as one CommandBuffer in LateUpdate. RTs are visual-only;
    /// game logic never reads them back (CPU/GPU authority split, TDD §3.1).
    /// Day-end bulk refresh from the CPU grid arrives with M.1.
    /// </summary>
    public sealed class PitchRenderBridge : MonoBehaviour
    {
        [SerializeField] private StadiumDefinitionSO _stadium;
        [SerializeField] private Shader _brushShader;
        [Tooltip("Same-subsystem reference used to seed RT_GrassState from the CPU grid at startup.")]
        [SerializeField] private PitchController _pitchController;

        [Header("RT sizes (TDD §3.2)")]
        [SerializeField] private int _grassStateSize = 2048;
        [SerializeField] private int _patternSize = 2048;
        [SerializeField] private int _paintSize = 4096;

        public RenderTexture GrassStateRt { get; private set; }
        public RenderTexture PatternRt { get; private set; }
        public RenderTexture PaintRt { get; private set; }
        public PitchBounds Bounds { get; private set; }
        public int LastStampCount { get; private set; }
        public double LastBuildExecuteMs { get; private set; }

        /// <summary>
        /// Cached delegate for ToolContext so per-frame context building does
        /// not allocate (System.Action cannot bind the in-parameter overload).
        /// </summary>
        public System.Action<StampRequest> EnqueueStampAction { get; private set; }

        private readonly List<StampRequest> _queue = new List<StampRequest>();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        // ColorMask/BlendOp are ShaderLab state and read from the material (a
        // property block cannot set them), so each (mask, blend op) pair in
        // use gets its own material.
        private readonly Dictionary<int, Material> _brushMaterials = new Dictionary<int, Material>();
        private CommandBuffer _commandBuffer;
        private Shader _resolvedBrushShader;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _quad;

        private void Awake()
        {
            Bounds = PitchBounds.FromStadium(_stadium);
            EnqueueStampAction = stamp => EnqueueStamp(stamp);

            GrassStateRt = CreateRt("RT_GrassState", _grassStateSize, RenderTextureFormat.ARGBHalf);
            PatternRt = CreateRt("RT_Pattern", _patternSize, RenderTextureFormat.RGHalf);
            PaintRt = CreateRt("RT_Paint", _paintSize, RenderTextureFormat.R8);

            _resolvedBrushShader = _brushShader != null ? _brushShader : Shader.Find("Greenkeeper/PitchBrush");
            _propertyBlock = new MaterialPropertyBlock();
            _quad = PitchStampRenderer.CreateQuadMesh();
            _commandBuffer = new CommandBuffer { name = "Greenkeeper.PitchStamps" };

            ClearAllTargets();

            // Consumers (grass blades F0.4, ground, future stripe shading)
            // sample the grass state RT through globals so runtime-created RTs
            // never need manual material wiring.
            Shader.SetGlobalTexture("_GrassStateRt", GrassStateRt);
            Shader.SetGlobalVector("_PitchUvTransform", Bounds.UvTransform);
        }

        private void Start()
        {
            if (_pitchController != null)
            {
                SeedFromGrid(_pitchController.Grid);
            }
        }

        /// <summary>
        /// One-shot seed of RT_GrassState from the CPU grid (encoding:
        /// GrassStateEncoding). Forerunner of the M.1 day-end bulk refresh;
        /// still CPU->GPU only, never the reverse (authority split, TDD §3.1).
        /// </summary>
        public void SeedFromGrid(PitchGrid grid)
        {
            var seedTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false, true)
            {
                name = "GrassStateSeed",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[grid.Width * grid.Height];
            var cells = grid.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                pixels[i] = GrassStateEncoding.Encode(cells[i]);
            }
            seedTexture.SetPixels32(pixels);
            seedTexture.Apply(false);

            Graphics.Blit(seedTexture, GrassStateRt);
            Destroy(seedTexture);
        }

        public RenderTexture GetTexture(PitchRenderTarget target)
        {
            switch (target)
            {
                case PitchRenderTarget.GrassState: return GrassStateRt;
                case PitchRenderTarget.Pattern: return PatternRt;
                default: return PaintRt;
            }
        }

        public void EnqueueStamp(in StampRequest stamp)
        {
            _queue.Add(stamp);
        }

        private void LateUpdate()
        {
            LastStampCount = _queue.Count;
            if (_queue.Count == 0)
            {
                LastBuildExecuteMs = 0.0;
                return;
            }

            _stopwatch.Restart();
            _commandBuffer.Clear();
            for (int i = 0; i < _queue.Count; i++)
            {
                StampRequest stamp = _queue[i];
                PitchStampRenderer.AppendStamp(
                    _commandBuffer, GetTexture(stamp.Target), _quad,
                    GetMaterialForState(stamp.EffectiveMask, stamp.BlendOp), _propertyBlock,
                    stamp, Bounds);
            }
            Graphics.ExecuteCommandBuffer(_commandBuffer);
            _stopwatch.Stop();

            _queue.Clear();
            LastBuildExecuteMs = _stopwatch.Elapsed.TotalMilliseconds;
        }

        private Material GetMaterialForState(ColorWriteMask mask, BlendOp blendOp)
        {
            int key = ((int)mask << 8) | (int)blendOp;
            if (!_brushMaterials.TryGetValue(key, out Material material))
            {
                material = new Material(_resolvedBrushShader);
                material.SetFloat("_ColorMask", (float)mask);
                material.SetFloat("_BlendOp", (float)blendOp);
                material.SetFloat("_MinNeutral", blendOp == BlendOp.Min ? 1f : 0f);
                _brushMaterials.Add(key, material);
            }
            return material;
        }

        private static RenderTexture CreateRt(string rtName, int size, RenderTextureFormat format)
        {
            var rt = new RenderTexture(size, size, 0, format)
            {
                name = rtName,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        private void ClearAllTargets()
        {
            // Cleared to zero; channel semantics belong to the consumers (F0.4+).
            _commandBuffer.Clear();
            ClearTarget(GrassStateRt);
            ClearTarget(PatternRt);
            ClearTarget(PaintRt);
            Graphics.ExecuteCommandBuffer(_commandBuffer);
            _commandBuffer.Clear();
        }

        private void ClearTarget(RenderTexture rt)
        {
            _commandBuffer.SetRenderTarget(rt);
            _commandBuffer.ClearRenderTarget(false, true, Color.clear);
        }

        private void OnDestroy()
        {
            _commandBuffer?.Dispose();
            ReleaseRt(GrassStateRt);
            ReleaseRt(PatternRt);
            ReleaseRt(PaintRt);
            foreach (Material material in _brushMaterials.Values)
            {
                Destroy(material);
            }
            _brushMaterials.Clear();
            if (_quad != null)
            {
                Destroy(_quad);
            }
        }

        private static void ReleaseRt(RenderTexture rt)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }
    }
}
