using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Growth curve parameters (TDD §5.3, MVP family). Decision (M.1 plan):
    /// the overnight moisture and paint fade rates also live here — the whole
    /// night process tunes from one asset; TDD defines no separate day-end SO
    /// family. The season multiplier is flat until the M.5 calendar drives it.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Grass Growth Profile", fileName = "GrassGrowth_New")]
    public sealed class GrassGrowthProfileSO : ScriptableObject
    {
        [Header("Growth")]
        public float baseGrowthMmPerDay = 1.4f;
        [Tooltip("Flat until the M.5 season calendar drives it.")]
        public float seasonMultiplier = 1f;
        [Tooltip("Optimal moisture band (0-100): full growth inside, linear falloff outside.")]
        public float moistureLowOpt = 40f;
        public float moistureHighOpt = 75f;
        public float maxGrassHeightMm = 60f;
        [Range(0f, 0.5f)] public float growthJitterFraction = 0.25f;

        [Header("Overnight moisture")]
        [Range(0f, 100f)] public float overnightMoistureTarget = 50f;
        [Range(0f, 1f)] public float overnightMoistureLerp = 0.6f;
        [Tooltip("Cells whose DAY-END moisture (before overnight dew) is below this count as dried on the morning card. Genuinely dry, not mid-range.")]
        [Range(0f, 100f)] public float driedMoistureThreshold = 18f;

        [Header("Paint fade")]
        [Tooltip("PaintCoverage units (0-100) lost per night. The liner writes paint from M.2 on.")]
        public float paintFadePerDay = 6f;

        public DayEndParams ToParams()
        {
            return new DayEndParams
            {
                BaseGrowthMmPerDay = baseGrowthMmPerDay,
                SeasonMultiplier = seasonMultiplier,
                MoistureLowOpt = moistureLowOpt,
                MoistureHighOpt = moistureHighOpt,
                MaxGrassHeightMm = maxGrassHeightMm,
                GrowthJitterFraction = growthJitterFraction,
                OvernightMoistureTarget = overnightMoistureTarget,
                OvernightMoistureLerp = overnightMoistureLerp,
                DriedMoistureThreshold = driedMoistureThreshold,
                PaintFadePerDay = paintFadePerDay
            };
        }

        private void OnValidate()
        {
            moistureHighOpt = Mathf.Max(moistureHighOpt, moistureLowOpt);
            maxGrassHeightMm = Mathf.Clamp(maxGrassHeightMm, 1f, 60f);
            baseGrowthMmPerDay = Mathf.Max(0f, baseGrowthMmPerDay);
            paintFadePerDay = Mathf.Max(0f, paintFadePerDay);
        }
    }
}
