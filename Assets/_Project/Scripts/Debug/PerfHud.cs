using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Minimal performance HUD (TDD §11, F2): smoothed frame time + FPS, pitch
    /// tick cost, stamp count, grass tile/instance stats and the GPU frame
    /// time (FrameTimingManager) used for the T1 budget check (grass ≤4 ms,
    /// TDD §1.3 — measured as the on/off delta via the G toggle).
    /// </summary>
    public sealed class PerfHud : MonoBehaviour
    {
        [SerializeField] private bool _visible = true;
        [SerializeField] private PitchController _pitch;
        [SerializeField] private PitchRenderBridge _renderBridge;
        [SerializeField] private GrassRenderer _grass;

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        private float _smoothedDeltaTime;
        private float _smoothedGpuMs;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _smoothedDeltaTime = _smoothedDeltaTime <= 0f
                ? dt
                : Mathf.Lerp(_smoothedDeltaTime, dt, 0.03f);

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
            {
                var gpuMs = (float)_frameTimings[0].gpuFrameTime;
                _smoothedGpuMs = _smoothedGpuMs <= 0f
                    ? gpuMs
                    : Mathf.Lerp(_smoothedGpuMs, gpuMs, 0.03f);
            }

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
            if (_smoothedGpuMs > 0f)
            {
                line += $"   GPU {_smoothedGpuMs:F2} ms";
            }
            if (_pitch != null && _pitch.TickCount > 0)
            {
                line += $"   Tick {_pitch.LastTickMs:F3} ms / 0.5";
            }
            if (_grass != null)
            {
                line += $"   Grass {_grass.VisibleTileCount}t/{_grass.TotalInstanceCount / 1000f:F0}k (cull {_grass.LastCullMs:F2} ms)";
            }
            if (_renderBridge != null && _renderBridge.LastStampCount > 0)
            {
                line += $"   Stamps {_renderBridge.LastStampCount} ({_renderBridge.LastBuildExecuteMs:F2} ms)";
            }
            if (_renderBridge != null && _renderBridge.LastMoistureSyncMs > 0.0)
            {
                line += $"   MoistSync {_renderBridge.LastMoistureSyncMs:F2} ms";
            }
            GUI.Box(new Rect(8f, 8f, 980f, 24f), GUIContent.none);
            GUI.Label(new Rect(14f, 11f, 970f, 20f), line);
        }
    }
}
