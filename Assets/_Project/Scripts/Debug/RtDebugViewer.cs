using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// F4 render-texture inspector: cycles None → GrassState → Pattern → Paint
    /// and shows the selected RT in the bottom-right screen corner. The debug
    /// view half of the F0.3 closure trio. (F1–F3 are allocated by TDD §11.)
    /// </summary>
    public sealed class RtDebugViewer : MonoBehaviour
    {
        private enum RtView
        {
            None,
            GrassState,
            Pattern,
            Paint,
            LineGuide
        }

        [SerializeField] private PitchRenderBridge _bridge;
        [Tooltip("Static ghost guide mask (M.3.5) — a Texture2D, shown as the last F4 view.")]
        [SerializeField] private PitchLineGuide _lineGuide;
        [SerializeField] private int _viewSizePx = 360;

        private RtView _view = RtView.None;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
            {
                _view = (RtView)(((int)_view + 1) % System.Enum.GetValues(typeof(RtView)).Length);
            }
        }

        private void OnGUI()
        {
            if (_view == RtView.None || _bridge == null)
            {
                return;
            }

            Texture rt = _view switch
            {
                RtView.GrassState => _bridge.GrassStateRt,
                RtView.Pattern => _bridge.PatternRt,
                RtView.Paint => _bridge.PaintRt,
                _ => _lineGuide != null ? _lineGuide.GuideTexture : null
            };
            if (rt == null)
            {
                return;
            }

            var rect = new Rect(
                Screen.width - _viewSizePx - 8f,
                Screen.height - _viewSizePx - 8f,
                _viewSizePx,
                _viewSizePx);
            GUI.DrawTexture(rect, rt, ScaleMode.ScaleToFit, false);

            var labelRect = new Rect(rect.x, rect.y - 26f, _viewSizePx, 22f);
            GUI.Box(labelRect, GUIContent.none);
            GUI.Label(new Rect(labelRect.x + 6f, labelRect.y + 2f, labelRect.width - 12f, 18f),
                $"[F4] {rt.name}  {rt.width}x{rt.height} {rt.graphicsFormat}");
        }
    }
}
