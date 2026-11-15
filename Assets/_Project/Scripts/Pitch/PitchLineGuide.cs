using System.Diagnostics;
using Greenkeeper.Core.Events;
using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Ghost line guide (M.3.5): bakes the ideal line geometry (LineLayoutSO →
    /// LineGeometry) into a static offset field once at startup and exposes it
    /// to the ground/blade shaders through globals, which draw it as a thin
    /// "taut string" center line. How visible the ghost is comes from the
    /// equipped liner's guidance strength, received over an event channel
    /// (0 = no liner equipped). Visual only: a CPU-baked Texture2D, never read
    /// back, never consulted by game logic (TDD §3.1).
    /// </summary>
    public sealed class PitchLineGuide : MonoBehaviour
    {
        [SerializeField] private LineLayoutSO _lineLayout;
        [Tooltip("Same-subsystem reference: the guide field shares the bridge's pitch bounds.")]
        [SerializeField] private PitchRenderBridge _bridge;
        [Tooltip("Guidance strength (0-1) of the equipped tool; raised by the player's tool host.")]
        [SerializeField] private FloatEventChannelSO _guidanceChannel;

        private static readonly int GuideTexId = Shader.PropertyToID("_LineGuideTex");
        private static readonly int GuideColorId = Shader.PropertyToID("_LineGuideColor");
        private static readonly int GuideStrengthId = Shader.PropertyToID("_LineGuideStrength");
        private static readonly int GuideParamsId = Shader.PropertyToID("_LineGuideParams");

        public Texture2D GuideTexture { get; private set; }

        /// <summary>Strength last announced by the equipped tool (0-1).</summary>
        public float ToolStrength { get; private set; }

        /// <summary>Strength actually shown (see LineGuideStrength.Resolve).</summary>
        public float EffectiveStrength =>
            LineGuideStrength.Resolve(ToolStrength, _debugOverride, _debugLinerEquipped);

        /// <summary>CPU cost of the one-off field bake (debug readout).</summary>
        public double LastBakeMs { get; private set; }

        private float? _debugOverride;
        private bool _debugLinerEquipped;

        /// <summary>
        /// Debug-only override (LineScoreHud [H]): previews another guidance
        /// strength before higher-tier liners exist. It applies only while a
        /// liner is equipped — the caller reports that — so other tools never
        /// show the guide. Null = follow the tool.
        /// </summary>
        public void SetDebugOverride(float? strength01, bool linerEquipped)
        {
            _debugOverride = strength01;
            _debugLinerEquipped = linerEquipped;
            PushStrength();
        }

        private void OnEnable()
        {
            if (_guidanceChannel != null)
            {
                _guidanceChannel.Subscribe(OnGuidanceChanged);
            }
        }

        private void OnDisable()
        {
            if (_guidanceChannel != null)
            {
                _guidanceChannel.Unsubscribe(OnGuidanceChanged);
            }
            Shader.SetGlobalFloat(GuideStrengthId, 0f);
        }

        private void Start()
        {
            // Start, not Awake: the bridge publishes its bounds in Awake.
            Bake();
            PushStrength();
        }

        private void Bake()
        {
            var stopwatch = Stopwatch.StartNew();
            int size = _lineLayout.guideTextureSize;
            byte[] field = LineGuideRasterizer.RasterizeOffsetField(
                _lineLayout.BuildIdeal(), _bridge.Bounds, size, size, _lineLayout.guideFieldRangeM);

            // Linear two-channel offsets; bilinear filtering is what makes
            // the thin line resolvable between texel centers.
            GuideTexture = new Texture2D(size, size, TextureFormat.RG16, false, true)
            {
                name = "LineGuide",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            GuideTexture.SetPixelData(field, 0);
            GuideTexture.Apply(false, true); // static: drop the CPU copy

            Shader.SetGlobalTexture(GuideTexId, GuideTexture);
            Shader.SetGlobalColor(GuideColorId, _lineLayout.guideColor);
            // x: field range, y: half width (ground), z: edge softness,
            // w: half width (blades).
            Shader.SetGlobalVector(GuideParamsId, new Vector4(
                _lineLayout.guideFieldRangeM,
                _lineLayout.guideWidthM * 0.5f,
                Mathf.Max(_lineLayout.guideEdgeSoftnessM, 1e-4f),
                _lineLayout.guideBladeWidthM * 0.5f));
            stopwatch.Stop();
            LastBakeMs = stopwatch.Elapsed.TotalMilliseconds;
        }

        private void OnGuidanceChanged(float strength01)
        {
            ToolStrength = Mathf.Clamp01(strength01);
            PushStrength();
        }

        private void PushStrength()
        {
            float opacity = _lineLayout != null ? _lineLayout.guideMaxOpacity : 1f;
            Shader.SetGlobalFloat(GuideStrengthId, EffectiveStrength * opacity);
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(GuideStrengthId, 0f);
            if (GuideTexture != null)
            {
                Destroy(GuideTexture);
            }
        }
    }
}
