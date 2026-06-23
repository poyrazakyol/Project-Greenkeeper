namespace Greenkeeper.Core
{
    public enum DayPhase
    {
        Morning,
        Midday,
        Evening
    }

    /// <summary>
    /// Pure day clock (TDD §8): normalized day time in [0,1], phase mapping
    /// and the manual day rollover. The clock HOLDS at the end of the evening
    /// — a day only ends through the explicit "End Day" action (GDD §2.4:
    /// pressure-free completion). No UnityEngine dependency; EditMode tested.
    /// </summary>
    public struct DayClock
    {
        public const float MorningEnd01 = 0.4f;
        public const float MiddayEnd01 = 0.75f;

        /// <summary>1-based day index.</summary>
        public int Day;

        /// <summary>Normalized time of day, clamped at 1 (evening hold).</summary>
        public float Time01;

        public static DayClock StartOfGame => new DayClock { Day = 1, Time01 = 0f };

        public DayPhase Phase
        {
            get
            {
                if (Time01 < MorningEnd01)
                {
                    return DayPhase.Morning;
                }
                return Time01 < MiddayEnd01 ? DayPhase.Midday : DayPhase.Evening;
            }
        }

        public void Advance(float scaledDeltaSeconds, float dayLengthSeconds)
        {
            if (dayLengthSeconds <= 0f || scaledDeltaSeconds <= 0f)
            {
                return;
            }
            Time01 += scaledDeltaSeconds / dayLengthSeconds;
            if (Time01 > 1f)
            {
                Time01 = 1f;
            }
        }

        public void StartNextDay()
        {
            Day++;
            Time01 = 0f;
        }
    }
}
