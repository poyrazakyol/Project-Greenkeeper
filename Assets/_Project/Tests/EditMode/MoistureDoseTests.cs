using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Moisture dose classification (TDD §4.4): &lt; 30 dry, &gt; 85 puddle,
    /// otherwise ok. The logical values the inspector reads (TDD §6.2).
    /// </summary>
    public class MoistureDoseTests
    {
        [TestCase(0, DoseState.Dry)]
        [TestCase(29, DoseState.Dry)]
        [TestCase(30, DoseState.Ok)]
        [TestCase(85, DoseState.Ok)]
        [TestCase(86, DoseState.Puddle)]
        [TestCase(100, DoseState.Puddle)]
        public void Classify_AppliesThresholds(int moisture, DoseState expected)
        {
            Assert.That(MoistureDose.Classify(moisture, 30, 85), Is.EqualTo(expected));
        }

        [Test]
        public void CountStates_TalliesAndAverages()
        {
            var cells = new NativeArray<CellState>(4, Allocator.Temp);
            cells[0] = new CellState { Moisture = 10 };  // dry
            cells[1] = new CellState { Moisture = 50 };  // ok
            cells[2] = new CellState { Moisture = 90 };  // puddle
            cells[3] = new CellState { Moisture = 28 };  // dry
            try
            {
                DoseCounts counts = MoistureDose.CountStates(cells, 30, 85);
                Assert.That(counts.Dry, Is.EqualTo(2));
                Assert.That(counts.Ok, Is.EqualTo(1));
                Assert.That(counts.Puddle, Is.EqualTo(1));
                Assert.That(counts.AverageMoisture, Is.EqualTo((10 + 50 + 90 + 28) / 4f).Within(0.001f));
            }
            finally
            {
                cells.Dispose();
            }
        }
    }
}
