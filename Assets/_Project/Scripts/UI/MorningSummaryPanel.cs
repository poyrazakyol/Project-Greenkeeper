using Greenkeeper.Core;
using Greenkeeper.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.UI
{
    /// <summary>
    /// Morning summary card (TDD §8, pipeline step 5 — first real UI
    /// component): listens on OnDayEnded, shows the night report and holds
    /// game time until dismissed. OnGUI-based for M.1; visual polish is M.9.
    /// </summary>
    public sealed class MorningSummaryPanel : MonoBehaviour
    {
        [SerializeField] private DayEndedEventChannelSO _dayEndedChannel;
        [SerializeField] private TimeSystem _timeSystem;

        private DayReport _report;
        private bool _visible;

        private void OnEnable()
        {
            if (_dayEndedChannel != null)
            {
                _dayEndedChannel.Subscribe(OnDayEnded);
            }
        }

        private void OnDisable()
        {
            if (_dayEndedChannel != null)
            {
                _dayEndedChannel.Unsubscribe(OnDayEnded);
            }
        }

        private void OnDayEnded(DayReport report)
        {
            _report = report;
            _visible = true;
        }

        private void Update()
        {
            if (!_visible)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
            {
                _visible = false;
                if (_timeSystem != null)
                {
                    _timeSystem.Paused = false; // resume the new morning
                }
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            var panel = new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.5f - 90f, 440f, 180f);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none); // double box: stronger backdrop without a skin

            GUI.Label(new Rect(panel.x + 20f, panel.y + 16f, 400f, 26f),
                $"— Gün {_report.Day} bitti —");
            GUI.Label(new Rect(panel.x + 20f, panel.y + 52f, 400f, 22f),
                $"Gece: çim ortalama +{_report.AverageGrowthMm:F1} mm uzadı");
            GUI.Label(new Rect(panel.x + 20f, panel.y + 78f, 400f, 22f),
                _report.DriedCellCount > 0
                    ? $"{_report.DriedCellCount} bölge kurudu"
                    : "Kuruyan bölge yok");
            GUI.Label(new Rect(panel.x + 20f, panel.y + 104f, 400f, 22f),
                $"Nem değişimi: {_report.AverageMoistureDelta:+0.0;-0.0} (ortalama)");
            GUI.Label(new Rect(panel.x + 20f, panel.y + 140f, 400f, 22f),
                $"[Enter/E] Gün {_report.Day + 1} — yeni güne başla");
        }
    }
}
