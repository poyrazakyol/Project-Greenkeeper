using System.Collections.Generic;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Capsule-vs-grid cell collection (extracted from MowSweep at M.2; shared
    /// by mower + liner). Touch is capsule-vs-cell-rect, so a narrow swath
    /// still collects every cell it crosses.
    /// </summary>
    public class CapsuleCellsTests
    {
        private StadiumDefinitionSO _stadium;
        private PitchGrid _grid;
        private readonly List<Vector2Int> _cells = new List<Vector2Int>();

        [SetUp]
        public void SetUp()
        {
            _stadium = ScriptableObject.CreateInstance<StadiumDefinitionSO>();
            _grid = PitchGrid.CreateCentered(_stadium, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _grid.Dispose();
            Object.DestroyImmediate(_stadium);
        }

        [Test]
        public void Collect_Segment_CollectsCapsuleAndNothingElse()
        {
            var from = new Vector3(-2f, 0f, 0f);
            var to = new Vector3(2f, 0f, 0f);
            const float radius = 0.6f;

            CapsuleCells.Collect(_grid, from, to, radius, _cells);

            Assert.That(_cells, Is.Not.Empty);
            // The capsule may clip a cell whose CENTER is up to half a cell
            // diagonal beyond the radius, never more.
            float halfDiagonal = _stadium.cellSizeM * 0.5f * Mathf.Sqrt(2f);
            foreach (Vector2Int cell in _cells)
            {
                Vector3 center = _grid.CellToWorldCenter(cell.x, cell.y);
                float distance = DistanceToSegmentXz(center, from, to);
                Assert.That(distance, Is.LessThanOrEqualTo(radius + halfDiagonal + 0.001f),
                    $"Cell {cell} cannot be touched by the capsule.");
            }

            Assert.That(_cells.Contains(CellAt(0f, 0.25f)), Is.True);
            Assert.That(_cells.Contains(CellAt(0f, 1.25f)), Is.False);
            Assert.That(_cells.Contains(CellAt(3.25f, 0.25f)), Is.False);
        }

        [Test]
        public void Collect_CoincidentPoints_DegeneratesToDisc()
        {
            CapsuleCells.Collect(_grid, Vector3.zero, Vector3.zero, 0.6f, _cells);

            Assert.That(_cells.Contains(CellAt(0.25f, 0.25f)), Is.True);
            Assert.That(_cells.Contains(CellAt(-0.25f, -0.25f)), Is.True);
            Assert.That(_cells.Contains(CellAt(0.75f, 0.75f)), Is.False); // rect dist ≈ 0.71
        }

        [Test]
        public void Collect_NarrowSwath_StillCollectsCrossedCells()
        {
            // Radius 0.06 (12 cm liner) < cell half-diagonal: a straight pass
            // must still collect the cells along its path.
            var from = new Vector3(-3f, 0f, 0.05f);
            var to = new Vector3(3f, 0f, 0.05f);
            CapsuleCells.Collect(_grid, from, to, 0.06f, _cells);

            Assert.That(_cells.Contains(CellAt(0f, 0.05f)), Is.True);
            Assert.That(_cells.Contains(CellAt(-1.5f, 0.05f)), Is.True);
            Assert.That(_cells.Contains(CellAt(1.5f, 0.05f)), Is.True);
        }

        private Vector2Int CellAt(float worldX, float worldZ)
        {
            Assert.That(_grid.TryWorldToCell(new Vector3(worldX, 0f, worldZ), out int x, out int y), Is.True);
            return new Vector2Int(x, y);
        }

        private static float DistanceToSegmentXz(Vector3 point, Vector3 a, Vector3 b)
        {
            var p = new Vector2(point.x, point.z);
            var s = new Vector2(a.x, a.z);
            var e = new Vector2(b.x, b.z);
            Vector2 se = e - s;
            float t = se.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - s, se) / se.sqrMagnitude);
            return Vector2.Distance(p, s + se * t);
        }
    }
}
