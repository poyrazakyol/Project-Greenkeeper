using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Determinism guarantee of the initial fill (TDD §3.4: same input =
    /// same grid) plus value-range sanity. Moisture starts flat (base ±
    /// jitter, no spatial gradient) so no part of the pitch reads wet or dry
    /// on day 1.
    /// </summary>
    public class PitchGridInitializerTests
    {
        private static readonly PitchInitParams Params = new PitchInitParams
        {
            BaseGrassHeightMm = 45f,
            BaseHealth = 70,
            BaseMoisture = 50,
            GrassHeightJitterMm = 6f,
            MoistureJitter = 3f,
            Seed = 1337
        };

        private static PitchGrid CreateSmallGrid()
        {
            return new PitchGrid(32, 20, 0.5f, new Vector2(-8f, -5f), Allocator.Persistent);
        }

        [Test]
        public void SameSeed_ProducesIdenticalGrids()
        {
            using PitchGrid first = CreateSmallGrid();
            using PitchGrid second = CreateSmallGrid();

            PitchGridInitializer.Initialize(first, Params);
            PitchGridInitializer.Initialize(second, Params);

            for (int i = 0; i < first.CellCount; i++)
            {
                Assert.That(second.Cells[i], Is.EqualTo(first.Cells[i]),
                    $"Cell {i} differs between identically seeded grids.");
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentGrid()
        {
            using PitchGrid first = CreateSmallGrid();
            using PitchGrid second = CreateSmallGrid();

            PitchGridInitializer.Initialize(first, Params);
            PitchInitParams other = Params;
            other.Seed = 42;
            PitchGridInitializer.Initialize(second, other);

            bool anyDifferent = false;
            for (int i = 0; i < first.CellCount && !anyDifferent; i++)
            {
                anyDifferent = !second.Cells[i].Equals(first.Cells[i]);
            }
            Assert.That(anyDifferent, Is.True, "Different seeds produced identical grids.");
        }

        [Test]
        public void Values_StayWithinConfiguredRanges()
        {
            using PitchGrid grid = CreateSmallGrid();
            PitchGridInitializer.Initialize(grid, Params);

            foreach (CellState cell in grid.Cells)
            {
                float height = cell.GrassHeightMm;
                Assert.That(height, Is.InRange(
                    Params.BaseGrassHeightMm - Params.GrassHeightJitterMm - 0.1f,
                    Params.BaseGrassHeightMm + Params.GrassHeightJitterMm + 0.1f));
                Assert.That(cell.Health, Is.EqualTo(Params.BaseHealth));
                Assert.That((int)cell.Moisture, Is.InRange(
                    (int)(Params.BaseMoisture - Params.MoistureJitter),
                    (int)(Params.BaseMoisture + Params.MoistureJitter)));
                Assert.That(cell.Damage, Is.Zero);
                Assert.That(cell.PaintCoverage, Is.Zero);
            }
        }

        [Test]
        public void Moisture_HasNoSpatialGradient()
        {
            // F0.2 seeded a debug X-gradient (40→70); it made half the pitch
            // read wet and half dry once the wet look became visible (M.3).
            // The two pitch halves must now average the same.
            using PitchGrid grid = CreateSmallGrid();
            PitchGridInitializer.Initialize(grid, Params);

            float leftSum = 0f, rightSum = 0f;
            int leftCount = 0, rightCount = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    float m = grid.Cells[grid.IndexOf(x, y)].Moisture;
                    if (x < grid.Width / 2) { leftSum += m; leftCount++; }
                    else { rightSum += m; rightCount++; }
                }
            }
            float leftAvg = leftSum / leftCount;
            float rightAvg = rightSum / rightCount;
            Assert.That(leftAvg, Is.EqualTo(Params.BaseMoisture).Within(1f));
            Assert.That(rightAvg, Is.EqualTo(leftAvg).Within(1f), "Left and right halves must not differ.");
        }

        [Test]
        public void Moisture_HasSmallRandomVariation()
        {
            using PitchGrid grid = CreateSmallGrid();
            PitchGridInitializer.Initialize(grid, Params);

            int first = grid.Cells[0].Moisture;
            bool anyDifferent = false;
            for (int i = 1; i < grid.CellCount && !anyDifferent; i++)
            {
                anyDifferent = grid.Cells[i].Moisture != first;
            }
            Assert.That(anyDifferent, Is.True, "Jitter must vary moisture between cells.");
        }

        [Test]
        public void ZeroSeed_DoesNotThrow()
        {
            using PitchGrid grid = CreateSmallGrid();
            PitchInitParams zeroSeed = Params;
            zeroSeed.Seed = 0;
            Assert.DoesNotThrow(() => PitchGridInitializer.Initialize(grid, zeroSeed));
        }
    }
}
