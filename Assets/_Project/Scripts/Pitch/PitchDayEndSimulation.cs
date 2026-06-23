using Greenkeeper.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Parameters of one night pass. Plain data, parametric response shapes —
    /// no AnimationCurve, keeping the simulation UnityEngine-free
    /// (CLAUDE.md rule 5 / TDD §3.4 determinism).
    /// </summary>
    public struct DayEndParams
    {
        public float BaseGrowthMmPerDay;
        /// <summary>Flat multiplier until the M.5 season calendar drives it.</summary>
        public float SeasonMultiplier;
        /// <summary>Optimal moisture band (0-100): full growth inside, linear falloff outside.</summary>
        public float MoistureLowOpt;
        public float MoistureHighOpt;
        public float MaxGrassHeightMm;
        /// <summary>Per-cell growth variation: ± this fraction, hashed from (cell, daySeed).</summary>
        public float GrowthJitterFraction;
        /// <summary>Moisture drifts toward this value overnight (dew/drainage)…</summary>
        public float OvernightMoistureTarget;
        /// <summary>…by this fraction of the gap (0-1).</summary>
        public float OvernightMoistureLerp;
        /// <summary>Cells whose DAY-END moisture (before overnight dew) is below this count as "dried" in the report.</summary>
        public float DriedMoistureThreshold;
        public float PaintFadePerDay;
    }

    /// <summary>
    /// Pure, seeded day-end simulation (TDD §3.4): growth = f(health,
    /// moisture, season) with deterministic per-cell jitter, overnight
    /// moisture equalization, paint fade. Same grid + same seed = identical
    /// result (save/load and test guarantee). Fertilizer (Phase 2), seed
    /// germination and neglect erosion (M.6) are intentionally absent.
    /// </summary>
    public static class PitchDayEndSimulation
    {
        public static DayReport Run(NativeArray<CellState> cells, in DayEndParams p, uint daySeed)
        {
            float totalGrowthMm = 0f;
            float totalMoistureDelta = 0f;
            int driedCount = 0;

            for (int i = 0; i < cells.Length; i++)
            {
                CellState cell = cells[i];

                // Growth
                float health01 = cell.Health / 100f;
                float moistureFactor = MoistureGrowthFactor(cell.Moisture, p.MoistureLowOpt, p.MoistureHighOpt);
                float jitter = 1f + (Hash01((uint)i, daySeed) * 2f - 1f) * p.GrowthJitterFraction;
                float growthMm = p.BaseGrowthMmPerDay * p.SeasonMultiplier * health01 * moistureFactor * jitter;

                float oldHeight = cell.GrassHeightMm;
                float newHeight = math.min(p.MaxGrassHeightMm, oldHeight + growthMm);
                cell.GrassHeightMm = (half)newHeight;
                totalGrowthMm += newHeight - oldHeight;

                // "Dried" is counted on the DAY-END moisture, i.e. the dry low
                // point reached during the day, BEFORE the overnight dew lifts
                // it back up. This is the honest neglect signal: a zone that
                // got critically dry yesterday is reported even though dew has
                // since recovered it — and it is decoupled from the recovery
                // floor (counting after dew flagged cells that were rising
                // again, which read as "positive moisture but dried").
                float dayEndMoisture = cell.Moisture;
                if (dayEndMoisture < p.DriedMoistureThreshold)
                {
                    driedCount++;
                }

                // Overnight moisture equalization (dew / drainage toward target)
                float newMoisture = dayEndMoisture + (p.OvernightMoistureTarget - dayEndMoisture) * p.OvernightMoistureLerp;
                newMoisture = math.clamp(newMoisture, 0f, 100f);
                cell.Moisture = (byte)math.round(newMoisture);
                totalMoistureDelta += cell.Moisture - dayEndMoisture;

                // Paint fade (generic since F0.5 stamps; the liner writes
                // PaintCoverage from M.2 on)
                cell.PaintCoverage = (byte)math.max(0f, cell.PaintCoverage - p.PaintFadePerDay);

                cells[i] = cell;
            }

            int count = math.max(cells.Length, 1);
            return new DayReport
            {
                Day = 0, // stamped by the pipeline orchestrator
                AverageGrowthMm = totalGrowthMm / count,
                DriedCellCount = driedCount,
                AverageMoistureDelta = totalMoistureDelta / count
            };
        }

        /// <summary>Full growth inside [low, high]; linear falloff to 0 outside.</summary>
        public static float MoistureGrowthFactor(float moisture, float lowOpt, float highOpt)
        {
            if (moisture < lowOpt)
            {
                return lowOpt <= 0f ? 1f : math.max(0f, moisture / lowOpt);
            }
            if (moisture > highOpt)
            {
                float range = 100f - highOpt;
                return range <= 0f ? 1f : math.max(0f, 1f - (moisture - highOpt) / range);
            }
            return 1f;
        }

        private static float Hash01(uint index, uint seed)
        {
            uint n = index * 374761393u + seed * 668265263u;
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return (n & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
