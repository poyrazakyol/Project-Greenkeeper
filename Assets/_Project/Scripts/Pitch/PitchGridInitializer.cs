using Unity.Mathematics;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Parameters for the deterministic initial grid fill. Plain struct (not
    /// the SO asset) so tests can drive the initializer without ScriptableObjects.
    /// </summary>
    public struct PitchInitParams
    {
        public float BaseGrassHeightMm;
        public byte BaseHealth;
        public byte BaseMoisture;
        public float GrassHeightJitterMm;
        public float MoistureJitter;
        public uint Seed;
    }

    /// <summary>
    /// Deterministic initial fill: base values + seeded jitter. Same seed and
    /// params always produce the identical grid (save/load and test guarantee).
    /// Moisture is flat around the base (the overnight equilibrium) with small
    /// per-cell jitter — no spatial gradient, so no part of the pitch reads
    /// wet or dry before the player touches it.
    /// </summary>
    public static class PitchGridInitializer
    {
        public static void Initialize(PitchGrid grid, in PitchInitParams p)
        {
            var random = new Random(p.Seed == 0 ? 1u : p.Seed);
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    float jitter = random.NextFloat(-p.GrassHeightJitterMm, p.GrassHeightJitterMm);
                    float heightMm = math.max(0f, p.BaseGrassHeightMm + jitter);

                    float moistureJitter = random.NextFloat(-p.MoistureJitter, p.MoistureJitter);
                    int moisture = (int)math.clamp(math.round(p.BaseMoisture + moistureJitter), 0f, 100f);

                    grid.Cells[grid.IndexOf(x, y)] = new CellState
                    {
                        GrassHeightMm = (half)heightMm,
                        Health = p.BaseHealth,
                        Moisture = (byte)moisture,
                        Compaction = 0,
                        Damage = 0,
                        PaintCoverage = 0,
                        ZoneId = 0,
                        PatternDirX = (half)0f,
                        PatternDirY = (half)0f,
                        _reserved = 0
                    };
                }
            }
        }
    }
}
