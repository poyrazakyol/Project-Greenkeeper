using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Minimal performance HUD (TDD §11, F2): smoothed frame time + FPS, plus
    /// the pitch tick cost when a PitchController is wired (budget 0.5 ms,
    /// TDD §1.3). Toggled with F2. GPU/stamp counters arrive in later blocks.
    /// </summary>
    public sealed class PerfHud : MonoBehaviour
    {
        [SerializeField] private bool _visible = true;
        [SerializeField] private PitchController _pitch;
        [SerializeField] private PitchRenderBridge _renderBridge;

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
            string line = $"Frame {ms:F2} ms  ({fps:F0} FPS)  [F2]";
            if (_pitch != null && _pitch.TickCount > 0)
            {
                line += $"   Tick {_pitch.LastTickMs:F3} ms / 0.5";
            }
            if (_renderBridge != null && _renderBridge.LastStampCount > 0)
            {
                line += $"   Stamps {_renderBridge.LastStampCount} ({_renderBridge.LastBuildExecuteMs:F2} ms)";
            }
            GUI.Box(new Rect(8f, 8f, 560f, 24f), GUIContent.none);
            GUI.Label(new Rect(14f, 11f, 550f, 20f), line);
        }
    }
}
