using Greenkeeper.Tools;
using Unity.Cinemachine;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Camera feel layer (F0.7): drives the Cinemachine perlin noise
    /// amplitude — near zero at rest, a light bob while walking, a rumble
    /// while mowing, plus a short decaying pulse for the completion moment.
    /// Values live here (camera feel is not tool data); promoted to an SO at
    /// MVP if more cameras need them.
    /// </summary>
    [RequireComponent(typeof(ToolController), typeof(CharacterController))]
    public sealed class CameraFeel : MonoBehaviour
    {
        [SerializeField] private CinemachineBasicMultiChannelPerlin _noise;
        [SerializeField] private float _walkBobAmplitude = 0.35f;
        [SerializeField] private float _mowRumbleAmplitude = 0.55f;
        [SerializeField] private float _responseSeconds = 0.2f;
        [SerializeField] private float _pulseDecaySeconds = 0.6f;

        private ToolController _toolController;
        private CharacterController _characterController;
        private float _pulseAmplitude;

        private void Awake()
        {
            _toolController = GetComponent<ToolController>();
            _characterController = GetComponent<CharacterController>();
        }

        /// <summary>One-shot feel accent (completion celebration).</summary>
        public void TriggerPulse(float amplitude)
        {
            _pulseAmplitude = Mathf.Max(_pulseAmplitude, amplitude);
        }

        private void Update()
        {
            if (_noise == null)
            {
                return;
            }

            float speed01 = Mathf.Clamp01(
                new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude / 4f);
            MowerTool mower = _toolController.Mower;
            bool engaged = mower != null && mower.IsEngaged;

            float target = speed01 * _walkBobAmplitude + (engaged ? _mowRumbleAmplitude : 0f);
            float blend = Mathf.Clamp01(Time.deltaTime / Mathf.Max(_responseSeconds, 0.01f));
            float baseAmplitude = Mathf.Lerp(_noise.AmplitudeGain, target, blend);

            _pulseAmplitude = Mathf.MoveTowards(
                _pulseAmplitude, 0f, Time.deltaTime / Mathf.Max(_pulseDecaySeconds, 0.01f));

            _noise.AmplitudeGain = baseAmplitude + _pulseAmplitude;
        }
    }
}
