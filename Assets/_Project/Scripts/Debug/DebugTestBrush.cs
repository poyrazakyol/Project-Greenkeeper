using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Temporary F0.3 verification brush: while LMB is held, raycasts from the
    /// view center to the ground and stamps the selected RT; B cycles the
    /// target RT. Reads devices directly (debug-only shortcut) — slated for
    /// removal/replacement when MowerTool lands in F0.5.
    /// </summary>
    public sealed class DebugTestBrush : MonoBehaviour
    {
        [SerializeField] private PitchRenderBridge _bridge;
        [SerializeField] private float _radiusM = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] private float _hardness = 0.7f;
        [SerializeField] private float _maxDistanceM = 50f;

        private PitchRenderTarget _target = PitchRenderTarget.Paint;
        private Camera _camera;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
            {
                _target = (PitchRenderTarget)(((int)_target + 1) % 3);
            }

            Mouse mouse = Mouse.current;
            if (_bridge == null || mouse == null || !mouse.leftButton.isPressed)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            // Cursor is locked in FP mode, so the view center is the aim point.
            Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistanceM))
            {
                return;
            }

            _bridge.EnqueueStamp(new StampRequest
            {
                Target = _target,
                WorldPosition = hit.point,
                WorldRadius = _radiusM,
                RotationRad = 0f,
                Value = ValueFor(_target),
                Hardness = _hardness
            });
        }

        private static Color ValueFor(PitchRenderTarget target)
        {
            switch (target)
            {
                case PitchRenderTarget.GrassState: return new Color(0.1f, 0.9f, 0.2f, 0.9f);
                case PitchRenderTarget.Pattern: return new Color(1f, 0.5f, 0f, 1f); // RG-only RT keeps red+green
                default: return Color.white;
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(8f, 64f, 320f, 24f), GUIContent.none);
            GUI.Label(new Rect(14f, 67f, 310f, 20f),
                $"[B] Test brush target: {_target}   (hold LMB to stamp)");
        }
    }
}
