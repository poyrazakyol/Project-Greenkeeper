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

        [Header("Paint mips (M.3.5 — distant lines stay solid)")]
        [Tooltip("Minimum real-time interval between RT_Paint mip rebuilds while a stroke is being painted. Mips are also rebuilt once as soon as painting stops.")]
        [SerializeField] private float _paintMipIntervalSeconds = 0.1f;
        [Tooltip("Anisotropic filtering of RT_Paint on the ground (grazing-angle lines).")]
        [SerializeField] private int _paintAnisoLevel = 8;

        public RenderTexture GrassStateRt { get; private set; }
        public RenderTexture PatternRt { get; private set; }
        public RenderTexture PaintRt { get; private set; }
        public PitchBounds Bounds { get; private set; }
        public int LastStampCount { get; private set; }
        public double LastBuildExecuteMs { get; private set; }
        /// <summary>CPU cost of the last moisture re-upload (F2 HUD).</summary>
        public double LastMoistureSyncMs { get; private set; }
        /// <summary>RT_Paint mip rebuilds since startup (debug: confirms the throttle).</summary>
        public int PaintMipRebuildCount { get; private set; }

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
        private bool _paintMipsDirty;
        private float _paintMipTimer;

        private void Awake()
        {
            Bounds = PitchBounds.FromStadium(_stadium);
            EnqueueStampAction = stamp => EnqueueStamp(stamp);

            GrassStateRt = CreateRt("RT_GrassState", _grassStateSize, RenderTextureFormat.ARGBHalf);
            PatternRt = CreateRt("RT_Pattern", _patternSize, RenderTextureFormat.RGHalf);
            // RT_Paint carries a mip chain: a 12 cm line is thinner than a
            // pixel beyond ~12 m, so unfiltered sampling breaks it into dots.
            // Mips are rebuilt by hand (throttled), never per stamp.
            PaintRt = CreateRt("RT_Paint", _paintSize, RenderTextureFormat.R8, true, _paintAnisoLevel);

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
            // World size of one RT_Paint texel: lets the blade vertex stage
            // pick a mip level from its on-screen footprint.
            Shader.SetGlobalFloat("_PaintTexelM", Bounds.Size.x / _paintSize);
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
            RenderTextureDescriptor descriptor = PaintRt.descriptor;
            descriptor.useMipMap = false; // scratch copy of mip 0 only
            RenderTexture temp = RenderTexture.GetTemporary(descriptor);
            Graphics.Blit(PaintRt, temp, _paintFadeMaterial);
            Graphics.Blit(temp, PaintRt);
            RenderTexture.ReleaseTemporary(temp);
            RebuildPaintMips(); // once per day-end, no throttle needed
        }

        /// <summary>
        /// Rebuilds the RT_Paint mip chain when paint changed, at most every
        /// _paintMipIntervalSeconds while stamps keep arriving and once
        /// immediately on the first frame without paint stamps (stroke end).
        /// Mip 0 is always current, so close-up paint never lags.
        /// </summary>
        private void UpdatePaintMips(bool paintedThisFrame)
        {
            _paintMipTimer += Time.unscaledDeltaTime;
            if (!_paintMipsDirty)
            {
                return;
            }
            if (!paintedThisFrame || _paintMipTimer >= _paintMipIntervalSeconds)
            {
                RebuildPaintMips();
            }
        }

        private void RebuildPaintMips()
        {
            PaintRt.GenerateMips();
            _paintMipsDirty = false;
            _paintMipTimer = 0f;
            PaintMipRebuildCount++;
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
                UpdatePaintMips(false);
                return;
            }

            bool paintedThisFrame = false;
            _stopwatch.Restart();
            _commandBuffer.Clear();
            for (int i = 0; i < _queue.Count; i++)
            {
                StampRequest stamp = _queue[i];
                paintedThisFrame |= stamp.Target == PitchRenderTarget.Paint;
                PitchStampRenderer.AppendStamp(
                    _commandBuffer, GetTexture(stamp.Target), _quad,
                    GetMaterialForState(stamp.EffectiveMask, stamp.BlendOp), _propertyBlock,
                    stamp, Bounds);
            }
            Graphics.ExecuteCommandBuffer(_commandBuffer);
            _stopwatch.Stop();

            _queue.Clear();
            LastBuildExecuteMs = _stopwatch.Elapsed.TotalMilliseconds;

            _paintMipsDirty |= paintedThisFrame;
            UpdatePaintMips(paintedThisFrame);
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

        private static RenderTexture CreateRt(string rtName, int size, RenderTextureFormat format,
            bool manualMips = false, int anisoLevel = 0)
        {
            // Every pitch RT holds data (height, health, coverage), not color.
            // In this Linear-space project the default for 8-bit formats is
            // sRGB: R8 became R8_SRGB, which D3D12 lacks, so it silently fell
            // back to RGBA8_SRGB (4x memory) and gamma-encoded paint coverage.
            var rt = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Linear)
            {
                name = rtName,
                useMipMap = manualMips,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = manualMips ? FilterMode.Trilinear : FilterMode.Bilinear,
                anisoLevel = anisoLevel
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
            PaintRt.GenerateMips(); // cleared chain: every level starts at zero
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
