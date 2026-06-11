using Greenkeeper.Core.Events;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Mow completion counter (TDD §4.2): once per second the CPU grid is
    /// scanned for cells within the target height band — never a GPU readback.
    /// The result is published on a float event channel (TDD §5); the debug
    /// HUD listens there, keeping the cross-system rule intact.
    /// </summary>
    public sealed class MowProgressTracker : MonoBehaviour
    {
        [SerializeField] private PitchController _pitch;
        [SerializeField] private ToolController _toolController;
        [SerializeField] private FloatEventChannelSO _progressChannel;
        [SerializeField] private float _scanIntervalSeconds = 1f;

        public float LastProgress01 { get; private set; }

        private float _timer;

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _scanIntervalSeconds)
            {
                return;
            }
            _timer = 0f;

            MowerTool mower = _toolController != null ? _toolController.Mower : null;
            if (mower == null || _pitch.Grid == null)
            {
                return;
            }

            float target = mower.CurrentCutHeightMm;
            float tolerance = mower.Definition.progressToleranceMm;
            var cells = _pitch.Grid.Cells;
            int inBand = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (Mathf.Abs((float)cells[i].GrassHeightMm - target) <= tolerance)
                {
                    inBand++;
                }
            }

            LastProgress01 = cells.Length == 0 ? 0f : (float)inBand / cells.Length;
            if (_progressChannel != null)
            {
                _progressChannel.Raise(LastProgress01);
            }
        }
    }
}
