using Greenkeeper.Core.Events;
using Greenkeeper.Gameplay;
using Greenkeeper.Tools;
using UnityEngine;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Mower status box (F0.5 verification view): cut height setting, basket
    /// fill, hold-to-empty progress and the mow completion percentage. The
    /// percentage arrives via the float event channel raised by
    /// MowProgressTracker — first real use of the TDD §5 channel pattern.
    /// </summary>
    public sealed class MowHud : MonoBehaviour
    {
        [SerializeField] private ToolController _toolController;
        [SerializeField] private FloatEventChannelSO _progressChannel;

        private float _progress01;

        private void OnEnable()
        {
            if (_progressChannel != null)
            {
                _progressChannel.Subscribe(OnProgress);
            }
        }

        private void OnDisable()
        {
            if (_progressChannel != null)
            {
                _progressChannel.Unsubscribe(OnProgress);
            }
        }

        private void OnProgress(float progress01)
        {
            _progress01 = progress01;
        }

        private void OnGUI()
        {
            MowerTool mower = _toolController != null ? _toolController.Mower : null;
            if (mower == null)
            {
                return;
            }

            string line =
                $"Cut {mower.CurrentCutHeightMm:F0} mm [scroll]   " +
                $"Basket {mower.BasketFill01 * 100f:F0}%   " +
                $"Mowed {_progress01 * 100f:F1}%";
            if (mower.BasketFull)
            {
                line += "   BASKET FULL — hold E";
            }
            else if (mower.EmptyHoldProgress01 > 0f && mower.EmptyHoldProgress01 < 1f)
            {
                line += $"   Emptying {mower.EmptyHoldProgress01 * 100f:F0}%";
            }

            float width = 520f;
            var box = new Rect(Screen.width - width - 8f, 8f, width, 24f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 6f, box.y + 3f, width - 12f, 20f), line);
        }
    }
}
