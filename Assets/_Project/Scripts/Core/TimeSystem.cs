using UnityEngine;

namespace Greenkeeper.Core
{
    /// <summary>
    /// Game time service (TDD §8): wraps the pure DayClock with the
    /// acceleration steps (1x/4x/12x) and exposes ScaledDeltaTime for systems
    /// that follow GAME time (pitch tick, day clock). Player movement stays
    /// real-time by design (GDD §2.4). Day rollover happens only through
    /// AdvanceToNextMorning, called by the day-end pipeline orchestrator.
    /// </summary>
    public sealed class TimeSystem : MonoBehaviour
    {
        private static readonly float[] TimeScaleSteps = { 1f, 4f, 12f };

        [Tooltip("Real seconds from morning to the evening hold at 1x speed.")]
        [SerializeField] private float _dayLengthRealSeconds = 180f;

        private DayClock _clock = DayClock.StartOfGame;
        private int _scaleIndex;

        /// <summary>While true (e.g. morning summary open) game time stands still.</summary>
        public bool Paused { get; set; }

        public float TimeScale => TimeScaleSteps[_scaleIndex];
        public int CurrentDay => _clock.Day;
        public float DayTime01 => _clock.Time01;
        public DayPhase Phase => _clock.Phase;

        /// <summary>
        /// Game-time delta of this frame. Zero while paused AND at the evening
        /// hold: once the day's natural cycle has wound down it must not keep
        /// running (otherwise leaving the game at evening — especially at 12x —
        /// drains the whole pitch to zero moisture; see day-end simulation).
        /// </summary>
        public float ScaledDeltaTime { get; private set; }

        /// <summary>True while the day's in-game processes should advance.</summary>
        public bool DayActive => !Paused && _clock.Time01 < 1f;

        private void Update()
        {
            ScaledDeltaTime = DayActive ? Time.deltaTime * TimeScale : 0f;
            _clock.Advance(ScaledDeltaTime, _dayLengthRealSeconds);
        }

        public void CycleTimeScale()
        {
            _scaleIndex = (_scaleIndex + 1) % TimeScaleSteps.Length;
        }

        public void AdvanceToNextMorning()
        {
            _clock.StartNextDay();
        }
    }
}
