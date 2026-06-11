using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Tool stats (TDD §5.3, Phase 0 family). New equipment = new asset, code
    /// unchanged: higher-tier mowers reuse MowerTool behavior with different
    /// stats. Scalping thresholds live here by decision (F0.5 plan): they sit
    /// in the tool design space (blade quality) and TDD §5.3 defines no
    /// separate turf-rules SO family.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Tool Definition", fileName = "Tool_New")]
    public sealed class ToolDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string toolName = "Push Mower";

        [Header("Mowing")]
        [Tooltip("Cut swath width in meters (capsule sweep diameter).")]
        public float mowWidthM = 0.5f;
        [Tooltip("Cut center distance in front of the player (push mower deck offset).")]
        public float mowForwardOffsetM = 0.7f;
        [Tooltip("Cut height steps in mm. Includes the scalping zone (<15 mm) on purpose — wrong settings must be reachable (GDD §3.1.1).")]
        public float[] cutHeightStepsMm = { 10f, 15f, 20f, 25f, 30f, 35f, 40f, 45f };
        public int defaultStepIndex = 3; // 25 mm

        [Header("Basket")]
        public float basketCapacityL = 50f;
        [Tooltip("Clippings volume produced per m² per mm of cut grass.")]
        public float clippingsLPerM2PerMm = 0.35f;
        [Tooltip("Seconds the empty interaction must be held (tempo-break micro task).")]
        public float basketEmptySeconds = 2f;

        [Header("Scalping rule (GDD §3.1.1 / TDD §4.2)")]
        public float scalpingHeightThresholdMm = 15f;
        public float scalpingCutThresholdMm = 20f;
        [Range(0, 100)] public int scalpingHealthDamage = 15;

        [Header("Progress")]
        [Tooltip("Height band tolerance for the completion counter (± mm). Superseded by LeagueStandardSO at MVP (M.4).")]
        public float progressToleranceMm = 2f;

        [Header("Progression (data only — economy logic arrives at M.7)")]
        public int unlockTier = 1;
        public float price = 0f;
    }
}
