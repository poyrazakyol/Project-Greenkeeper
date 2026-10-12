using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Hand hose stats (TDD §4.4). Dedicated SO (per-tool decision, like the
    /// liner): hose stats — pressure steps, cone footprint, flow→moisture —
    /// share nothing with the mower or liner. Dose thresholds live in
    /// GrassRenderSettingsSO so the logical and visual sides stay in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Hose Tool Definition", fileName = "HoseTool_New")]
    public sealed class HoseToolDefinitionSO : ScriptableObject
    {
        public string toolName = "Hand Hose";

        [Header("Spray")]
        [Tooltip("Pressure steps in liters/second (scroll cycles them).")]
        public float[] pressureStepsLPerSec = { 2f, 5f, 9f };
        public int defaultStepIndex = 1;
        [Tooltip("Ground footprint radius of the spray disc, in meters.")]
        public float coneRadiusM = 1.2f;
        [Tooltip("Max distance from the player the spray can reach.")]
        public float maxRangeM = 8f;
        [Tooltip("Moisture units (0-100) added per liter, per cell, per second.")]
        public float moisturePerLiter = 1.4f;

        [Header("Progression (data only — economy logic arrives at M.7)")]
        public int unlockTier = 1;
        public float price = 0f;
    }
}
