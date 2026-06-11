using System.Collections.Generic;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Pure mowing core (TDD §4.2): capsule sweep cell selection and the
    /// per-cell cut rule including the scalping boundary matrix (GDD §3.1.1).
    /// </summary>
    public class MowSweepTests
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

        private static MowCutParams DefaultParams(float cutHeightMm)
        {
            return new MowCutParams
            {
                CutHeightMm = cutHeightMm,
                MoveDir = new Vector2(0f, 1f),
                ScalpingHeightThresholdMm = 15f,
                ScalpingCutThresholdMm = 20f,
                ScalpingHealthDamage = 15
            };
        }

        [Test]
        public void CollectCells_Segment_CollectsCapsuleAndNothingElse()
        {
            var from = new Vector3(-2f, 0f, 0f);
            var to = new Vector3(2f, 0f, 0f);
            const float radius = 0.6f;

            MowSweep.CollectCells(_grid, from, to, radius, _cells);

            Assert.That(_cells, Is.Not.Empty);
            // Touch semantics: the capsule may clip a cell whose CENTER is up
            // to half a cell diagonal beyond the radius, never more.
            float halfDiagonal = _stadium.cellSizeM * 0.5f * Mathf.Sqrt(2f);
            foreach (Vector2Int cell in _cells)
            {
                Vector3 center = _grid.CellToWorldCenter(cell.x, cell.y);
                float distance = DistanceToSegmentXz(center, from, to);
                Assert.That(distance, Is.LessThanOrEqualTo(radius + halfDiagonal + 0.001f),
                    $"Cell {cell} cannot be touched by the capsule.");
            }

            // Inside: directly on the segment. Outside: one full cell beyond the radius.
            Assert.That(_cells.Contains(CellAt(0f, 0.25f)), Is.True);
            Assert.That(_cells.Contains(CellAt(0f, 1.25f)), Is.False);
            Assert.That(_cells.Contains(CellAt(3.25f, 0.25f)), Is.False);
        }

        [Test]
        public void CollectCells_CoincidentPoints_DegeneratesToDisc()
        {
            MowSweep.CollectCells(_grid, Vector3.zero, Vector3.zero, 0.6f, _cells);

            Assert.That(_cells.Contains(CellAt(0.25f, 0.25f)), Is.True);
            Assert.That(_cells.Contains(CellAt(-0.25f, -0.25f)), Is.True);
            Assert.That(_cells.Contains(CellAt(0.75f, 0.75f)), Is.False); // rect dist ≈ 0.71
        }

        [Test]
        public void ApplyCut_TallerGrass_CutsToSettingAndWritesPattern()
        {
            var cell = new CellState { GrassHeightMm = (half)45f, Health = 70 };
            var p = DefaultParams(25f);
            p.MoveDir = new Vector2(0.6f, 0.8f);

            MowCutResult result = MowSweep.ApplyCut(cell, p);

            Assert.That((float)result.Cell.GrassHeightMm, Is.EqualTo(25f).Within(0.05f));
            Assert.That(result.CutMm, Is.EqualTo(20f).Within(0.05f));
            Assert.That((float)result.Cell.PatternDirX, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That((float)result.Cell.PatternDirY, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(result.Cell.Health, Is.EqualTo(70), "25 mm setting must not scalp.");
        }

        [Test]
        public void ApplyCut_ShorterGrass_IsUntouched()
        {
            var cell = new CellState { GrassHeightMm = (half)20f, Health = 70 };

            MowCutResult result = MowSweep.ApplyCut(cell, DefaultParams(25f));

            Assert.That(result.CutMm, Is.EqualTo(0f));
            Assert.That((float)result.Cell.GrassHeightMm, Is.EqualTo(20f).Within(0.05f));
            Assert.That((float)result.Cell.PatternDirX, Is.EqualTo(0f),
                "Pattern is laid only where blades are cut.");
        }

        // Scalping boundary matrix: setting < 15 AND cut >= 20 in one pass.
        [TestCase(10f, 30f, true)]    // setting < 15, cut 20  -> scalped
        [TestCase(10f, 29.5f, false)] // setting < 15, cut 19.5 -> safe
        [TestCase(15f, 40f, false)]   // setting at threshold   -> safe
        [TestCase(14.5f, 34.5f, true)] // 14.5 setting, cut 20  -> scalped
        public void ApplyCut_ScalpingRule_BoundaryMatrix(float settingMm, float grassMm, bool expectScalped)
        {
            var cell = new CellState { GrassHeightMm = (half)grassMm, Health = 70 };

            MowCutResult result = MowSweep.ApplyCut(cell, DefaultParams(settingMm));

            Assert.That(result.Scalped, Is.EqualTo(expectScalped));
            Assert.That(result.Cell.Health, Is.EqualTo(expectScalped ? 55 : 70));
        }

        [Test]
        public void ApplyCut_ScalpingDamage_ClampsHealthAtZero()
        {
            var cell = new CellState { GrassHeightMm = (half)40f, Health = 10 };

            MowCutResult result = MowSweep.ApplyCut(cell, DefaultParams(10f));

            Assert.That(result.Scalped, Is.True);
            Assert.That(result.Cell.Health, Is.EqualTo(0));
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
