namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Per-tick inputs for the in-day simulation. MoistureLoss is an integer
    /// because cell moisture is byte-quantized; the caller accumulates the
    /// fractional evaporation debt and passes whole units (see PitchController).
    /// </summary>
    public struct PitchTickParams
    {
        public int MoistureLoss; // moisture units (0–100 scale) removed this tick
    }

    /// <summary>
    /// In-day tick math (TDD §3.4, 4 Hz), pure static so EditMode tests call it
    /// directly. A Burst IJobParallelFor wrapper can adopt the same Step
    /// function later without touching the math (TDD risk T3 fallback).
    /// F0.2 skeleton only evaporates moisture; irrigation spread and visual
    /// dirty-marking arrive with their own blocks (M.3+).
    /// </summary>
    public static class PitchTick
    {
        public static CellState Step(in CellState cell, in PitchTickParams p)
        {
            CellState next = cell;

            int moisture = cell.Moisture - p.MoistureLoss;
            next.Moisture = (byte)(moisture < 0 ? 0 : moisture);

            return next;
        }
    }
}
