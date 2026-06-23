using Greenkeeper.Core;
using UnityEngine;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Day/phase/time-scale readout (M.1 debug view): bottom-left box with
    /// the key hints for time scale (T) and end day (K).
    /// </summary>
    public sealed class TimeHud : MonoBehaviour
    {
        [SerializeField] private TimeSystem _timeSystem;

        private void OnGUI()
        {
            if (_timeSystem == null)
            {
                return;
            }

            string phase;
            switch (_timeSystem.Phase)
            {
                case DayPhase.Morning: phase = "Sabah"; break;
                case DayPhase.Midday: phase = "Öğle"; break;
                default: phase = "Akşam"; break;
            }

            string line = _timeSystem.Paused
                ? $"Gün {_timeSystem.CurrentDay} — duraklatıldı"
                : $"Gün {_timeSystem.CurrentDay} — {phase} ({_timeSystem.TimeScale:0}×)   [T] hız  [K] günü bitir";

            var box = new Rect(8f, Screen.height - 32f, 420f, 24f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 6f, box.y + 3f, 410f, 20f), line);
        }
    }
}
