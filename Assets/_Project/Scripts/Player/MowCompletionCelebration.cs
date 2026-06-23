using Greenkeeper.Core.Events;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Completion satisfaction moment (F0.7, TDD §12): the first time the mow
    /// progress channel crosses the threshold, play a sting and pulse the
    /// camera. Re-armed every morning (M.1): grass grows back overnight, so
    /// each day offers a fresh completion.
    /// </summary>
    public sealed class MowCompletionCelebration : MonoBehaviour
    {
        [SerializeField] private FloatEventChannelSO _progressChannel;
        [SerializeField] private DayEndedEventChannelSO _dayEndedChannel;
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
            if (_dayEndedChannel != null)
            {
                _dayEndedChannel.Subscribe(OnDayEnded);
            }
        }

        private void OnDisable()
        {
            if (_progressChannel != null)
            {
                _progressChannel.Unsubscribe(OnProgress);
            }
            if (_dayEndedChannel != null)
            {
                _dayEndedChannel.Unsubscribe(OnDayEnded);
            }
        }

        private void OnDayEnded(Greenkeeper.Core.DayReport report)
        {
            _fired = false; // a new day, a new completion to earn
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
