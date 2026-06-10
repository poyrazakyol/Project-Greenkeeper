using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// World↔cell mapping and lifecycle of the CPU pitch grid (TDD §3.1).
    /// </summary>
    public class PitchGridTests
    {
        private PitchGrid _grid;
        private StadiumDefinitionSO _stadium;

        [SetUp]
        public void SetUp()
        {
            _stadium = ScriptableObject.CreateInstance<StadiumDefinitionSO>();
            _grid = PitchGrid.CreateCentered(_stadium, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _grid?.Dispose();
            Object.DestroyImmediate(_stadium);
        }

        [Test]
        public void DefaultStadium_ProducesTddGridDimensions()
        {
            Assert.That(_grid.Width, Is.EqualTo(212));
            Assert.That(_grid.Height, Is.EqualTo(140));
            Assert.That(_grid.CellCount, Is.EqualTo(29680));
            Assert.That(_grid.Cells.Length, Is.EqualTo(29680));
        }

        [Test]
        public void Grid_IsCenteredOnWorldOrigin()
        {
            Assert.That(_grid.WorldOrigin.x, Is.EqualTo(-53f).Within(0.001f));   // 212 * 0.5 / 2
            Assert.That(_grid.WorldOrigin.y, Is.EqualTo(-35f).Within(0.001f));   // 140 * 0.5 / 2

            Assert.That(_grid.TryWorldToCell(Vector3.zero, out int x, out int y), Is.True);
            Assert.That(x, Is.EqualTo(106));
            Assert.That(y, Is.EqualTo(70));
        }

        [Test]
        public void CellToWorldCenter_RoundTripsThroughTryWorldToCell()
        {
            int[][] samples = { new[] { 0, 0 }, new[] { 211, 139 }, new[] { 106, 70 }, new[] { 13, 121 } };
            foreach (int[] sample in samples)
            {
                Vector3 center = _grid.CellToWorldCenter(sample[0], sample[1]);
                Assert.That(_grid.TryWorldToCell(center, out int x, out int y), Is.True);
                Assert.That(x, Is.EqualTo(sample[0]), "X mismatch after round trip");
                Assert.That(y, Is.EqualTo(sample[1]), "Y mismatch after round trip");
            }
        }

        [Test]
        public void CornerCell_MapsToWorldMinCorner()
        {
            Vector3 center = _grid.CellToWorldCenter(0, 0);
            Assert.That(center.x, Is.EqualTo(-52.75f).Within(0.001f));
            Assert.That(center.z, Is.EqualTo(-34.75f).Within(0.001f));
        }

        [Test]
        public void TryWorldToCell_OutsideGrid_ReturnsFalse()
        {
            Assert.That(_grid.TryWorldToCell(new Vector3(-60f, 0f, 0f), out _, out _), Is.False);
            Assert.That(_grid.TryWorldToCell(new Vector3(0f, 0f, 40f), out _, out _), Is.False);
            Assert.That(_grid.TryWorldToCell(new Vector3(500f, 0f, 500f), out _, out _), Is.False);
        }

        [Test]
        public void Dispose_ReleasesNativeArray()
        {
            _grid.Dispose();
            Assert.That(_grid.Cells.IsCreated, Is.False);
        }
    }
}
