using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Minimal performance HUD (TDD §11, F2): smoothed frame time + FPS.
    /// Toggled with F2; visible by default so the F0.1 60 FPS check is immediate.
    /// GPU/tick/stamp counters are added in their own blocks (F0.2+).
    /// </summary>
    public sealed class PerfHud : MonoBehaviour
    {
        [SerializeField] private bool _visible = true;

        private float _smoothedDeltaTime;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _smoothedDeltaTime = _smoothedDeltaTime <= 0f
                ? dt
                : Mathf.Lerp(_smoothedDeltaTime, dt, 0.03f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _smoothedDeltaTime <= 0f)
            {
                return;
            }

            float ms = _smoothedDeltaTime * 1000f;
            float fps = 1f / _smoothedDeltaTime;
            GUI.Box(new Rect(8f, 8f, 220f, 24f), GUIContent.none);
            GUI.Label(new Rect(14f, 11f, 210f, 20f), $"Frame {ms:F2} ms  ({fps:F0} FPS)  [F2]");
        }
    }
}
