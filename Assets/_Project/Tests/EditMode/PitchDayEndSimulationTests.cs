using Greenkeeper.Core;
using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Day-end simulation (TDD §3.4): seeded, pure and deterministic —
    /// the save/load consistency guarantee. Covers growth response, the
    /// 60 mm clamp, overnight moisture, dried counting and paint fade.
    /// </summary>
    public class PitchDayEndSimulationTests
    {
        private static DayEndParams DefaultParams(float jitter = 0f)
        {
            return new DayEndParams
            {
                BaseGrowthMmPerDay = 2f,
                SeasonMultiplier = 1f,
                MoistureLowOpt = 40f,
                MoistureHighOpt = 75f,
                MaxGrassHeightMm = 60f,
                GrowthJitterFraction = jitter,
                OvernightMoistureTarget = 50f,
                OvernightMoistureLerp = 0.5f,
                DriedMoistureThreshold = 30f,
                PaintFadePerDay = 6f
            };
        }

        private static NativeArray<CellState> MakeCells(params CellState[] cells)
        {
            return new NativeArray<CellState>(cells, Allocator.Temp);
        }

        private static CellState Cell(float heightMm, byte health, byte moisture, byte paint = 0)
        {
            return new CellState
            {
                GrassHeightMm = (half)heightMm,
                Health = health,
                Moisture = moisture,
                PaintCoverage = paint
            };
        }

        [Test]
        public void Run_IsDeterministic_AndDaySeedChangesJitter()
        {
            var paramsWithJitter = DefaultParams(jitter: 0.25f);

            using NativeArray<CellState> a = MakeCells(Cell(30f, 70, 55), Cell(40f, 90, 60));
            using NativeArray<CellState> b = MakeCells(Cell(30f, 70, 55), Cell(40f, 90, 60));
            using NativeArray<CellState> c = MakeCells(Cell(30f, 70, 55), Cell(40f, 90, 60));

            DayReport reportA = PitchDayEndSimulation.Run(a, paramsWithJitter, 1234u);
            DayReport reportB = PitchDayEndSimulation.Run(b, paramsWithJitter, 1234u);
            PitchDayEndSimulation.Run(c, paramsWithJitter, 9999u);

            for (int i = 0; i < a.Length; i++)
            {
                Assert.That((float)a[i].GrassHeightMm, Is.EqualTo((float)b[i].GrassHeightMm),
                    "Same grid + same seed must be bit-identical (TDD §3.4).");
            }
            Assert.That(reportA.AverageGrowthMm, Is.EqualTo(reportB.AverageGrowthMm));
            Assert.That((float)a[0].GrassHeightMm, Is.Not.EqualTo((float)c[0].GrassHeightMm),
                "A different day seed must produce different jitter.");
        }

        [Test]
        public void Run_GrowthFollowsHealthAndMoisture()
        {
            // Optimal moisture, full health -> full base growth.
            using NativeArray<CellState> cells = MakeCells(
                Cell(30f, 100, 55),  // full growth: +2.0
                Cell(30f, 50, 55),   // half health: +1.0
                Cell(30f, 100, 20)); // moisture 20 < lowOpt 40 -> factor 0.5: +1.0

            PitchDayEndSimulation.Run(cells, DefaultParams(), 1u);

            Assert.That((float)cells[0].GrassHeightMm, Is.EqualTo(32f).Within(0.05f));
            Assert.That((float)cells[1].GrassHeightMm, Is.EqualTo(31f).Within(0.05f));
            Assert.That((float)cells[2].GrassHeightMm, Is.EqualTo(31f).Within(0.05f));
        }

        [Test]
        public void MoistureGrowthFactor_BandAndFalloff()
        {
            Assert.That(PitchDayEndSimulation.MoistureGrowthFactor(55f, 40f, 75f), Is.EqualTo(1f));
            Assert.That(PitchDayEndSimulation.MoistureGrowthFactor(40f, 40f, 75f), Is.EqualTo(1f));
            Assert.That(PitchDayEndSimulation.MoistureGrowthFactor(20f, 40f, 75f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(PitchDayEndSimulation.MoistureGrowthFactor(0f, 40f, 75f), Is.EqualTo(0f));
            Assert.That(PitchDayEndSimulation.MoistureGrowthFactor(100f, 40f, 75f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(PitchDayEndSimulation.MoistureGrowthFactor(87.5f, 40f, 75f), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Run_ClampsHeightAtMax()
        {
            using NativeArray<CellState> cells = MakeCells(Cell(59.5f, 100, 55));
            DayReport report = PitchDayEndSimulation.Run(cells, DefaultParams(), 1u);

            Assert.That((float)cells[0].GrassHeightMm, Is.EqualTo(60f).Within(0.05f));
            Assert.That(report.AverageGrowthMm, Is.EqualTo(0.5f).Within(0.05f),
                "Reported growth must be the ACTUAL clamped delta.");
        }

        [Test]
        public void Run_MovesMoistureTowardTarget_AndCountsDriedFromDayEndLowPoint()
        {
            // Dried is counted on the DAY-END moisture (before overnight dew),
            // so both 20 and 4 — below the threshold 30 — count, even though
            // dew then lifts them to 35 and 27.
            using NativeArray<CellState> cells = MakeCells(
                Cell(30f, 70, 20),  // day-end 20 < 30 -> dried; dew -> 35
                Cell(30f, 70, 4),   // day-end 4 < 30 -> dried;  dew -> 27
                Cell(30f, 70, 90)); // day-end 90 -> not dried;  dew -> 70

            DayReport report = PitchDayEndSimulation.Run(cells, DefaultParams(), 1u);

            Assert.That(cells[0].Moisture, Is.EqualTo(35));
            Assert.That(cells[1].Moisture, Is.EqualTo(27));
            Assert.That(cells[2].Moisture, Is.EqualTo(70));
            Assert.That(report.DriedCellCount, Is.EqualTo(2),
                "Both cells that ended the day below the threshold must count, pre-dew.");
        }

        [Test]
        public void Run_FadesPaintAndStopsAtZero()
        {
            using NativeArray<CellState> cells = MakeCells(
                Cell(30f, 70, 55, paint: 100),
                Cell(30f, 70, 55, paint: 4));

            PitchDayEndSimulation.Run(cells, DefaultParams(), 1u);

            Assert.That(cells[0].PaintCoverage, Is.EqualTo(94));
            Assert.That(cells[1].PaintCoverage, Is.EqualTo(0), "Paint fade must clamp at zero.");
        }

        [Test]
        public void Run_ReportAveragesAreConsistent()
        {
            using NativeArray<CellState> cells = MakeCells(Cell(30f, 100, 55), Cell(30f, 0, 55));
            DayReport report = PitchDayEndSimulation.Run(cells, DefaultParams(), 1u);

            // Cell 0 grows +2, cell 1 (health 0) grows 0 -> average 1.
            Assert.That(report.AverageGrowthMm, Is.EqualTo(1f).Within(0.05f));
        }
    }
}
