using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Fixed world↔UV mapping (TDD §3.2) and its alignment with the CPU grid:
    /// both must live in the same space so tools can write both layers from
    /// one stroke position.
    /// </summary>
    public class PitchBoundsTests
    {
        private StadiumDefinitionSO _stadium;
        private PitchBounds _bounds;

        [SetUp]
        public void SetUp()
        {
            _stadium = ScriptableObject.CreateInstance<StadiumDefinitionSO>();
            _bounds = PitchBounds.FromStadium(_stadium);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_stadium);
        }

        [Test]
        public void FromStadium_CoversGridAreaCenteredOnOrigin()
        {
            Assert.That(_bounds.Origin.x, Is.EqualTo(-53f).Within(0.001f));
            Assert.That(_bounds.Origin.y, Is.EqualTo(-35f).Within(0.001f));
            Assert.That(_bounds.Size.x, Is.EqualTo(106f).Within(0.001f));
            Assert.That(_bounds.Size.y, Is.EqualTo(70f).Within(0.001f));
        }

        [Test]
        public void WorldToUv_MapsCornersAndCenter()
        {
            Vector2 min = _bounds.WorldToUv(new Vector3(-53f, 0f, -35f));
            Vector2 max = _bounds.WorldToUv(new Vector3(53f, 0f, 35f));
            Vector2 center = _bounds.WorldToUv(Vector3.zero);

            Assert.That(min.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(min.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(max.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(max.y, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(center.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(center.y, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void UvToWorld_RoundTripsWithWorldToUv()
        {
            var samples = new[] { new Vector3(-53f, 0f, -35f), Vector3.zero, new Vector3(12.5f, 0f, -20.25f) };
            foreach (Vector3 world in samples)
            {
                Vector3 roundTripped = _bounds.UvToWorld(_bounds.WorldToUv(world));
                Assert.That(roundTripped.x, Is.EqualTo(world.x).Within(0.001f));
                Assert.That(roundTripped.z, Is.EqualTo(world.z).Within(0.001f));
            }
        }

        [Test]
        public void WorldToUv_OutsideBounds_LeavesUnitRange()
        {
            Vector2 uv = _bounds.WorldToUv(new Vector3(-60f, 0f, 40f));
            Assert.That(uv.x, Is.LessThan(0f));
            Assert.That(uv.y, Is.GreaterThan(1f));
        }

        [Test]
        public void UvTransform_MatchesWorldToUv()
        {
            Vector4 t = _bounds.UvTransform;
            var world = new Vector3(7.25f, 0f, -11.5f);
            Vector2 expected = _bounds.WorldToUv(world);

            Assert.That(world.x * t.z + t.x, Is.EqualTo(expected.x).Within(1e-5f));
            Assert.That(world.z * t.w + t.y, Is.EqualTo(expected.y).Within(1e-5f));
        }

        [Test]
        public void GridCellCenters_AlignWithUvTexelCenters()
        {
            using PitchGrid grid = PitchGrid.CreateCentered(_stadium, Allocator.Persistent);
            int[][] cells = { new[] { 0, 0 }, new[] { 211, 139 }, new[] { 106, 70 } };
            foreach (int[] cell in cells)
            {
                Vector2 uv = _bounds.WorldToUv(grid.CellToWorldCenter(cell[0], cell[1]));
                Assert.That(uv.x * grid.Width, Is.EqualTo(cell[0] + 0.5f).Within(0.001f),
                    "Grid X and UV must share one space (TDD §3.2).");
                Assert.That(uv.y * grid.Height, Is.EqualTo(cell[1] + 0.5f).Within(0.001f),
                    "Grid Y and UV must share one space (TDD §3.2).");
            }
        }
    }
}
