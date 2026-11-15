using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Parametric pitch line layout + scoring tolerances (TDD §4.3, §5.3).
    /// The ideal geometry is generated from the stadium dimensions, so it
    /// always matches the pitch. League-specific tightness (amateur loose,
    /// top-flight tight) arrives with LeagueStandardSO at M.4; for now the
    /// tolerances are this asset's defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Line Layout", fileName = "LineLayout_New")]
    public sealed class LineLayoutSO : ScriptableObject
    {
        [SerializeField] private StadiumDefinitionSO _stadium;

        [Header("Geometry")]
        [Tooltip("Arc/line tessellation spacing in meters (ideal polyline density).")]
        public float tessellationSpacingM = 0.25f;

        [Header("Scoring tolerances (M.4 LeagueStandardSO will drive these)")]
        [Tooltip("A stroke point farther than this from every ideal line is 'stray'.")]
        public float maxAssociationM = 1.0f;
        [Tooltip("An ideal sample counts as painted if a stroke point is within this (≈ half line width + slack).")]
        public float coverageThresholdM = 0.15f;
        [Tooltip("Spacing for resampling ideal lines into coverage samples.")]
        public float coverageSampleSpacingM = 0.25f;

        [Header("Guide overlay (M.3.5 — ghost lines, visual only)")]
        [Tooltip("Guide offset-field resolution (square). The field stores the vector to the nearest ideal line, so 2048 (≈ 5 cm texels) still draws a crisp thin line.")]
        public int guideTextureSize = 2048;
        [Tooltip("Total ghost line width in meters — a taut-string center line, much thinner than the painted line so a correctly painted line covers it completely.")]
        public float guideWidthM = 0.03f;
        [Tooltip("Soft edge in meters, INSIDE the guide width (nothing is drawn beyond the width).")]
        public float guideEdgeSoftnessM = 0.01f;
        [Tooltip("Width over which grass blades pick up the guide tint. Blades are sparse, so a 3 cm band would catch almost none; keep this below the painted line width so paint still covers it.")]
        public float guideBladeWidthM = 0.08f;
        [Tooltip("Largest offset the field stores, in meters. Must exceed half of both guide widths plus a texel.")]
        public float guideFieldRangeM = 0.25f;
        [Tooltip("Ghost tint. Deliberately not white so it never reads as paint.")]
        public Color guideColor = new Color(0.55f, 0.9f, 1f);
        [Tooltip("Opacity of the ghost at guidance strength 1 (the liner's strength scales this).")]
        [Range(0f, 1f)] public float guideMaxOpacity = 0.85f;

        public IdealLine[] BuildIdeal()
        {
            return LineGeometry.Generate(_stadium.pitchLengthM, _stadium.pitchWidthM, tessellationSpacingM);
        }

        public LineScoreParams ToScoreParams()
        {
            return new LineScoreParams
            {
                MaxAssociationM = maxAssociationM,
                CoverageThresholdM = coverageThresholdM,
                CoverageSampleSpacingM = coverageSampleSpacingM
            };
        }

        private void OnValidate()
        {
            tessellationSpacingM = Mathf.Max(0.05f, tessellationSpacingM);
            maxAssociationM = Mathf.Max(0.05f, maxAssociationM);
            coverageThresholdM = Mathf.Max(0.01f, coverageThresholdM);
            coverageSampleSpacingM = Mathf.Max(0.05f, coverageSampleSpacingM);
            guideTextureSize = Mathf.Clamp(guideTextureSize, 256, 4096);
            guideWidthM = Mathf.Max(0.005f, guideWidthM);
            guideEdgeSoftnessM = Mathf.Clamp(guideEdgeSoftnessM, 0.001f, guideWidthM * 0.5f);
            guideBladeWidthM = Mathf.Max(guideWidthM, guideBladeWidthM);
            guideFieldRangeM = Mathf.Max(guideFieldRangeM, guideBladeWidthM * 0.5f + 0.1f);
        }
    }
}
