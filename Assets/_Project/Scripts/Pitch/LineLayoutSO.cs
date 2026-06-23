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
        }
    }
}
