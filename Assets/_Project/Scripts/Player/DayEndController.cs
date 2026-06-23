using Greenkeeper.Core;
using Greenkeeper.Core.Events;
using Greenkeeper.Pitch;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Day-end pipeline orchestrator (TDD §8). Lives in Gameplay because the
    /// pipeline ORDER must be deterministic and explicit in one place — event
    /// subscription order would be fragile — and only this assembly sees both
    /// Core (time) and Pitch (simulation). Also routes the time-scale key.
    /// </summary>
    public sealed class DayEndController : MonoBehaviour
    {
        [SerializeField] private TimeSystem _timeSystem;
        [SerializeField] private PitchController _pitch;
        [SerializeField] private PitchRenderBridge _bridge;
        [SerializeField] private GrassGrowthProfileSO _growthProfile;
        [SerializeField] private DayEndedEventChannelSO _dayEndedChannel;
        [SerializeField] private PlayerInputProvider _input;

        private void Update()
        {
            if (_input.TimeScale.WasPressedThisFrame())
            {
                _timeSystem.CycleTimeScale();
            }
            // Blocked while paused (morning summary open).
            if (_input.EndDay.WasPressedThisFrame() && !_timeSystem.Paused)
            {
                EndDay();
            }
        }

        /// <summary>
        /// TDD §8 deterministic pipeline:
        /// 1) pitch day-end simulation
        /// 2) economy day end        (M.7 — hook position, nothing yet)
        /// 3) autosave               (M.8 — hook position, nothing yet)
        /// 4) day rollover + OnDayEnded publish
        /// 5) morning summary UI     (listens on the channel)
        /// </summary>
        public void EndDay()
        {
            int endedDay = _timeSystem.CurrentDay;

            // 1) Pitch simulation — seed ties to the stadium seed and the day,
            // so the same save state always produces the same night (TDD §3.4).
            uint daySeed = CombineSeed((uint)_pitch.Stadium.initialStateSeed, (uint)endedDay);
            DayReport report = PitchDayEndSimulation.Run(_pitch.Grid.Cells, _growthProfile.ToParams(), daySeed);
            report.Day = endedDay;

            // 2) (M.7) economy day-end runs here.
            // 3) (M.8) autosave runs here.

            // 4) Roll over and publish. The visual layer re-seeds from the CPU
            // grid in bulk (TDD §3.4); the pattern RT is untouched on purpose —
            // stripes survive light growth like a real lawn.
            _timeSystem.AdvanceToNextMorning();
            _timeSystem.Paused = true; // 5) the summary panel unpauses on dismiss
            _bridge.SeedFromGrid(_pitch.Grid);
            _dayEndedChannel.Raise(report);
        }

        private static uint CombineSeed(uint globalSeed, uint day)
        {
            uint n = globalSeed * 2654435761u ^ (day * 2246822519u + 374761393u);
            n ^= n >> 15;
            return n == 0 ? 1u : n;
        }
    }
}
