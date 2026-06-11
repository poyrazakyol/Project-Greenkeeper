using Greenkeeper.Core.Events;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Completion satisfaction moment (F0.7, TDD §12): the first time the mow
    /// progress channel crosses the threshold, play a sting and pulse the
    /// camera. Once per session — grass does not regrow in Phase 0; the
    /// day-end reset re-arms this at M.1.
    /// </summary>
    public sealed class MowCompletionCelebration : MonoBehaviour
    {
        [SerializeField] private FloatEventChannelSO _progressChannel;
        [SerializeField] private CameraFeel _cameraFeel;
        [SerializeField, Range(0.5f, 1f)] private float _threshold = 0.99f;
        [SerializeField] private float _pulseAmplitude = 1.4f;

        private AudioSource _stingSource;
        private AudioClip _stingClip;
        private bool _fired;

        private void Start()
        {
            _stingSource = gameObject.AddComponent<AudioSource>();
            _stingSource.playOnAwake = false;
            _stingSource.spatialBlend = 0f;
            _stingClip = ProceduralAudioClips.CreateClip(
                "CompletionSting_Procedural", ProceduralAudioClips.GenerateStingSamples());
        }

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
            if (_fired || progress01 < _threshold)
            {
                return;
            }
            _fired = true;
            _stingSource.PlayOneShot(_stingClip, 0.9f);
            if (_cameraFeel != null)
            {
                _cameraFeel.TriggerPulse(_pulseAmplitude);
            }
        }
    }
}
