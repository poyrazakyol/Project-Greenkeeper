using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Determinism guarantee of the initial fill (TDD §3.4: same input =
    /// same grid) plus value-range sanity.
    /// </summary>
    public class PitchGridInitializerTests
    {
        private static readonly PitchInitParams Params = new PitchInitParams
        {
            BaseGrassHeightMm = 45f,
            BaseHealth = 70,
            BaseMoisture = 55,
            GrassHeightJitterMm = 6f,
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
                Assert.That((int)cell.Moisture, Is.InRange(0, 100));
                Assert.That(cell.Damage, Is.Zero);
                Assert.That(cell.PaintCoverage, Is.Zero);
            }
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
