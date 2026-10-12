using Greenkeeper.Gameplay;
using Greenkeeper.Pitch;
using UnityEngine;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Hose / moisture dose debug view (M.3): while the hose is equipped, shows
    /// the pressure setting plus the dose counts (puddle / dry cells, average
    /// moisture) from MoistureDose.CountStates — the same logical values the
    /// inspector reads at M.4. F1 already shows the moisture field spatially.
    /// </summary>
    public sealed class HoseHud : MonoBehaviour
    {
        [SerializeField] private ToolController _toolController;
        [SerializeField] private PitchController _pitch;
        [SerializeField] private GrassRenderSettingsSO _grassSettings;
        [SerializeField] private float _refreshIntervalSeconds = 0.5f;

        private DoseCounts _counts;
        private float _timer;

        private void Update()
        {
            if (_toolController == null || !_toolController.HoseActive || _pitch == null || _pitch.Grid == null)
            {
                return;
            }
            _timer += Time.unscaledDeltaTime;
            if (_timer >= _refreshIntervalSeconds)
            {
                _timer = 0f;
                int dry = Mathf.RoundToInt(_grassSettings.dryThreshold * 100f);
                int puddle = Mathf.RoundToInt(_grassSettings.puddleThreshold * 100f);
                _counts = MoistureDose.CountStates(_pitch.Grid.Cells, dry, puddle);
            }
        }

        private void OnGUI()
        {
            if (_toolController == null || !_toolController.HoseActive || _toolController.Hose == null)
            {
                return;
            }

            var box = new Rect(Screen.width - 360f, Screen.height - 64f, 352f, 56f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, 20f),
                $"Hortum  basınç {_toolController.Hose.CurrentFlowLPerSec:0.#} L/sn [scroll]");
            GUI.Label(new Rect(box.x + 8f, box.y + 28f, box.width - 16f, 20f),
                $"Birikinti {_counts.Puddle}   Kuru {_counts.Dry}   Ort.nem {_counts.AverageMoisture:0}");
        }
    }
}
