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
    /// The moisture channel (RT_GrassState.B) is driven from the CPU grid:
    /// whenever the grid's moisture is dirty it is re-uploaded (throttled),
    /// so the screen always reflects the simulation's water, diffusion and
    /// evaporation included.
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

        [Header("Moisture visual sync")]
        [Tooltip("Minimum real-time interval between moisture re-uploads while the grid is dirty.")]
        [SerializeField] private float _moistureSyncIntervalSeconds = 0.1f;

        public RenderTexture GrassStateRt { get; private set; }
        public RenderTexture PatternRt { get; private set; }
        public RenderTexture PaintRt { get; private set; }
        public PitchBounds Bounds { get; private set; }
        public int LastStampCount { get; private set; }
        public double LastBuildExecuteMs { get; private set; }
        /// <summary>CPU cost of the last moisture re-upload (F2 HUD).</summary>
        public double LastMoistureSyncMs { get; private set; }

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
        private Material _paintFadeMaterial;
        private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");

        private readonly Stopwatch _syncStopwatch = new Stopwatch();
        private Material _moistureSyncMaterial;
        private Texture2D _moistureTexture;
        private CellState[] _cellCopy;
        private byte[] _moistureBytes;
        private float _moistureSyncTimer;

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
            _paintFadeMaterial = new Material(Shader.Find("Greenkeeper/PitchPaintFade"));
            _moistureSyncMaterial = new Material(Shader.Find("Greenkeeper/PitchMoistureSync"));

            ClearAllTargets();

            // Consumers (grass blades F0.4, stripes F0.6, ground) sample the
            // RTs through globals so runtime-created RTs never need manual
            // material wiring.
            Shader.SetGlobalTexture("_GrassStateRt", GrassStateRt);
            Shader.SetGlobalTexture("_PatternRt", PatternRt);
            Shader.SetGlobalTexture("_PaintRt", PaintRt);
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
            grid.ClearMoistureDirty(); // B was just written from the grid
        }

        private void TrySyncMoisture()
        {
            // Timer keeps running while clean, so the first change after an
            // idle period uploads immediately (no added latency on spray).
            _moistureSyncTimer += Time.unscaledDeltaTime;
            PitchGrid grid = _pitchController != null ? _pitchController.Grid : null;
            if (grid == null || !grid.MoistureDirty || _moistureSyncTimer < _moistureSyncIntervalSeconds)
            {
                return;
            }
            _moistureSyncTimer = 0f;
            SyncMoisture(grid);
        }

        /// <summary>
        /// Re-uploads CPU moisture into RT_GrassState.B only (ColorMask B), so
        /// the mower's fine height in R is preserved. Bulk CopyTo avoids
        /// per-element NativeArray safety checks; bilinear sampling of the
        /// 0.5 m grid gives soft edges.
        /// </summary>
        private void SyncMoisture(PitchGrid grid)
        {
            _syncStopwatch.Restart();
            int count = grid.CellCount;
            if (_moistureTexture == null)
            {
                _moistureTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.R8, false, true)
                {
                    name = "MoistureSync",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _cellCopy = new CellState[count];
                _moistureBytes = new byte[count];
            }

            grid.Cells.CopyTo(_cellCopy);
            for (int i = 0; i < count; i++)
            {
                _moistureBytes[i] = _cellCopy[i].Moisture;
            }
            _moistureTexture.SetPixelData(_moistureBytes, 0);
            _moistureTexture.Apply(false);
            Graphics.Blit(_moistureTexture, GrassStateRt, _moistureSyncMaterial);

            grid.ClearMoistureDirty();
            _syncStopwatch.Stop();
            LastMoistureSyncMs = _syncStopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Fades the painted lines on RT_Paint by subtracting <paramref
        /// name="amount01"/> (0-1) from the coverage channel. Called once per
        /// day-end to mirror the CPU PaintCoverage fade rate (TDD §3.4) — a
        /// full-screen blit keeps the high-resolution lines crisp, unlike a
        /// re-seed from the coarse CPU grid.
        /// </summary>
        public void FadePaint(float amount01)
        {
            if (amount01 <= 0f || _paintFadeMaterial == null)
            {
                return;
            }
            _paintFadeMaterial.SetFloat(FadeAmountId, amount01);
            RenderTexture temp = RenderTexture.GetTemporary(PaintRt.descriptor);
            Graphics.Blit(PaintRt, temp, _paintFadeMaterial);
            Graphics.Blit(temp, PaintRt);
            RenderTexture.ReleaseTemporary(temp);
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
            TrySyncMoisture();

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
            if (_paintFadeMaterial != null)
            {
                Destroy(_paintFadeMaterial);
            }
            if (_moistureSyncMaterial != null)
            {
                Destroy(_moistureSyncMaterial);
            }
            if (_moistureTexture != null)
            {
                Destroy(_moistureTexture);
            }
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
