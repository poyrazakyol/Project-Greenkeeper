using Unity.Collections;
using Unity.Mathematics;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Pure in-day moisture diffusion (TDD §3.4 "sulama yayılımı"): each cell
    /// blends a fraction toward its 4-neighbour average, softening spray edges
    /// and bleeding over-watered spots into their surroundings.
    ///
    /// Split for tick cost (M.3 measurement: the first NativeArray-based
    /// version cost 1.5 ms vs 0.5 ms for the whole evaporation pass, because
    /// every NativeArray access is safety-checked in the editor):
    /// the caller snapshots moisture into a managed array during the pass it
    /// already makes over the cells, Diffuse runs the stencil on managed
    /// arrays only, and ApplyMoisture writes back just the cells that changed
    /// (byte quantization leaves most of a smooth field untouched).
    /// Double-buffered (no directional smear), edge-replicating (no leak to a
    /// phantom zero), deterministic.
    /// </summary>
    public static class PitchMoistureSim
    {
        /// <summary>
        /// Stencil pass: <paramref name="destination"/>[i] = blend of
        /// <paramref name="source"/>[i] toward its 4-neighbour average. Both
        /// arrays must be at least width*height long. With rate 0 the source is
        /// copied through unchanged.
        /// </summary>
        public static void Diffuse(byte[] source, byte[] destination, int width, int height, float rate)
        {
            int count = width * height;
            float r = math.clamp(rate, 0f, 1f);
            if (r <= 0f)
            {
                System.Array.Copy(source, destination, count);
                return;
            }

            // Fixed-point rate (1/1024 steps): integer-only inner loop. The
            // editor's Mono JIT does not inline Unity.Mathematics calls, and
            // float round/clamp per cell dominated the stencil cost.
            int rateFixed = (int)(r * 1024f + 0.5f);

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int i = row + x;
                    int self = source[i];
                    int left = x > 0 ? source[i - 1] : self;
                    int right = x < width - 1 ? source[i + 1] : self;
                    int down = y > 0 ? source[i - width] : self;
                    int up = y < height - 1 ? source[i + width] : self;

                    // 4 × (neighbour average − self).
                    int diff4 = left + right + down + up - 4 * self;
                    if (diff4 == 0)
                    {
                        destination[i] = (byte)self; // flat or linear: no change
                        continue;
                    }

                    // change = diff4 / 4 × rate, rounded half away from zero.
                    int scaled = diff4 * rateFixed;
                    int change = scaled >= 0 ? (scaled + 2048) >> 12 : -((-scaled + 2048) >> 12);
                    int value = self + change;
                    if (value < 0)
                    {
                        value = 0;
                    }
                    else if (value > 100)
                    {
                        value = 100;
                    }
                    destination[i] = (byte)value;
                }
            }
        }

        /// <summary>
        /// Writes <paramref name="after"/> moisture back into the grid where it
        /// differs from <paramref name="before"/>. Returns the changed count.
        /// </summary>
        public static int ApplyMoisture(NativeArray<CellState> cells, byte[] before, byte[] after)
        {
            int changed = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (after[i] == before[i])
                {
                    continue;
                }
                CellState cell = cells[i];
                cell.Moisture = after[i];
                cells[i] = cell;
                changed++;
            }
            return changed;
        }
    }
}
