using Greenkeeper.Core;
using NUnit.Framework;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Pure day clock (TDD §8 / GDD §2.4): phase mapping, the evening hold
    /// (a day never rolls over by itself) and the manual rollover.
    /// </summary>
    public class DayClockTests
    {
        [Test]
        public void Phase_MapsNormalizedTimeToPhases()
        {
            var clock = DayClock.StartOfGame;
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Morning));

            clock.Time01 = DayClock.MorningEnd01 - 0.01f;
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Morning));
            clock.Time01 = DayClock.MorningEnd01;
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Midday));
            clock.Time01 = DayClock.MiddayEnd01;
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Evening));
            clock.Time01 = 1f;
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Evening));
        }

        [Test]
        public void Advance_ScalesWithDayLength_AndHoldsAtOne()
        {
            var clock = DayClock.StartOfGame;
            clock.Advance(scaledDeltaSeconds: 150f, dayLengthSeconds: 600f);
            Assert.That(clock.Time01, Is.EqualTo(0.25f).Within(1e-5f));

            // The evening holds: no auto-rollover no matter how long we wait.
            clock.Advance(scaledDeltaSeconds: 10_000f, dayLengthSeconds: 600f);
            Assert.That(clock.Time01, Is.EqualTo(1f));
            Assert.That(clock.Day, Is.EqualTo(1), "A day must never end by itself (GDD §2.4).");
        }

        [Test]
        public void Advance_IgnoresZeroOrNegativeInput()
        {
            var clock = DayClock.StartOfGame;
            clock.Advance(0f, 600f);
            clock.Advance(-5f, 600f);
            clock.Advance(5f, 0f);
            Assert.That(clock.Time01, Is.EqualTo(0f));
        }

        [Test]
        public void StartNextDay_IncrementsDayAndResetsClock()
        {
            var clock = DayClock.StartOfGame;
            clock.Time01 = 1f;
            clock.StartNextDay();
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.Time01, Is.EqualTo(0f));
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Morning));
        }
    }
}
