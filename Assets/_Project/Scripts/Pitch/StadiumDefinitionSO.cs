using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Stadium/pitch parameters (TDD §5.3 — minimal F0.2 slice). Pitch size is
    /// fixed at 105x68 m (GDD §1.5, FIFA tournament standard); the grid is
    /// slightly larger so off-pitch margin cells exist around the lines.
    /// Environment prefab and career tier fields arrive with MVP.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Stadium Definition", fileName = "Stadium_New")]
    public sealed class StadiumDefinitionSO : ScriptableObject
    {
        [Header("Pitch geometry (FIFA standard, fixed)")]
        public float pitchLengthM = 105f;
        public float pitchWidthM = 68f;
        public float cellSizeM = 0.5f;
        public int gridWidth = 212;   // cells along pitch length (world X)
        public int gridHeight = 140;  // cells along pitch width (world Z)

        [Header("Initial cell state")]
        // 52 mm (raised from 45 after the Phase 0 external test: "starting
        // grass should be taller"). Jitter ±6 keeps the max at 58, inside the
        // 0-60 mm CellState range.
        public float initialGrassHeightMm = 52f;
        [Range(0, 100)] public int initialHealth = 70;
        [Tooltip("Flat starting moisture; matches the overnight equilibrium target (GrassGrowthProfileSO) so day 1 starts settled.")]
        [Range(0, 100)] public int initialMoisture = 50;
        [Tooltip("Per-cell random moisture variation (± units) around the flat base.")]
        public float initialMoistureJitter = 3f;
        public float grassHeightJitterMm = 6f;
        public int initialStateSeed = 1337;

        public PitchInitParams ToInitParams()
        {
            return new PitchInitParams
            {
                BaseGrassHeightMm = initialGrassHeightMm,
                BaseHealth = (byte)initialHealth,
                BaseMoisture = (byte)initialMoisture,
                GrassHeightJitterMm = grassHeightJitterMm,
                MoistureJitter = initialMoistureJitter,
                Seed = (uint)initialStateSeed
            };
        }
    }
}
