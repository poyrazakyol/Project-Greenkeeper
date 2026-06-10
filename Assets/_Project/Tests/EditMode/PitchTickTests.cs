using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Mathematics;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// In-day tick math (TDD §3.4). Pure function — these tests run without
    /// any scene or allocation.
    /// </summary>
    public class PitchTickTests
    {
        private static CellState MakeCell(byte moisture)
        {
            return new CellState
            {
                GrassHeightMm = (half)42.5f,
                Health = 70,
                Moisture = moisture,
                Compaction = 10,
                Damage = 5,
                PaintCoverage = 20,
                ZoneId = 3,
                PatternDirX = (half)0.7f,
                PatternDirY = (half)(-0.7f),
                _reserved = 0
            };
        }

        [Test]
        public void Step_ReducesMoistureByLoss()
        {
            CellState next = PitchTick.Step(MakeCell(50), new PitchTickParams { MoistureLoss = 3 });
            Assert.That(next.Moisture, Is.EqualTo(47));
        }

        [Test]
        public void Step_ClampsMoistureAtZero()
        {
            CellState next = PitchTick.Step(MakeCell(2), new PitchTickParams { MoistureLoss = 5 });
            Assert.That(next.Moisture, Is.Zero);
        }

        [Test]
        public void Step_WithZeroLoss_IsNoOp()
        {
            CellState cell = MakeCell(50);
            CellState next = PitchTick.Step(cell, new PitchTickParams { MoistureLoss = 0 });
            Assert.That(next, Is.EqualTo(cell));
        }

        [Test]
        public void Step_DoesNotTouchOtherChannels()
        {
            CellState cell = MakeCell(50);
            CellState next = PitchTick.Step(cell, new PitchTickParams { MoistureLoss = 3 });

            Assert.That((float)next.GrassHeightMm, Is.EqualTo((float)cell.GrassHeightMm));
            Assert.That(next.Health, Is.EqualTo(cell.Health));
            Assert.That(next.Compaction, Is.EqualTo(cell.Compaction));
            Assert.That(next.Damage, Is.EqualTo(cell.Damage));
            Assert.That(next.PaintCoverage, Is.EqualTo(cell.PaintCoverage));
            Assert.That(next.ZoneId, Is.EqualTo(cell.ZoneId));
            Assert.That((float)next.PatternDirX, Is.EqualTo((float)cell.PatternDirX));
            Assert.That((float)next.PatternDirY, Is.EqualTo((float)cell.PatternDirY));
        }

        [Test]
        public void Step_IsDeterministic()
        {
            CellState cell = MakeCell(73);
            var tickParams = new PitchTickParams { MoistureLoss = 2 };
            Assert.That(PitchTick.Step(cell, tickParams), Is.EqualTo(PitchTick.Step(cell, tickParams)));
        }
    }
}
