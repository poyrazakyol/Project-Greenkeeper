using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Pure mowing core (TDD §4.2): the per-cell cut rule including the
    /// scalping boundary matrix (GDD §3.1.1). Capsule cell selection moved to
    /// CapsuleCellsTests at M.2.
    /// </summary>
    public class MowSweepTests
    {
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
    }
}
