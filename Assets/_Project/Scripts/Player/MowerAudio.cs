using Greenkeeper.Tools;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Mower sound layer (F0.7, GDD: ASMR value): engine idles while the tool
    /// is equipped, revs while engaged; the cutting swish follows the ACTUAL
    /// cut throughput so already-mown grass stays silent; a beep marks the
    /// basket filling up. Clips come from ToolDefinitionSO; empty fields fall
    /// back to the procedural placeholders.
    /// </summary>
    [RequireComponent(typeof(ToolController), typeof(CharacterController))]
    public sealed class MowerAudio : MonoBehaviour
    {
        [Header("Engine")]
        [SerializeField] private float _idleVolume = 0.22f;
        [SerializeField] private float _engagedVolume = 0.45f;
        [SerializeField] private float _idlePitch = 0.92f;
        [SerializeField] private float _engagedPitch = 1.18f;
        [SerializeField] private float _speedPitchGain = 0.05f;
        [SerializeField] private float _responseSeconds = 0.25f;

        [Header("Cutting")]
        [SerializeField] private float _swishMaxVolume = 0.8f;

        private ToolController _toolController;
        private CharacterController _characterController;
        private AudioSource _engineSource;
        private AudioSource _swishSource;
        private AudioSource _oneShotSource;
        private AudioClip _basketFullClip;
        private bool _basketWasFull;

        private void Awake()
        {
            _toolController = GetComponent<ToolController>();
            _characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            ToolDefinitionSO definition = _toolController.Mower.Definition;

            AudioClip engineClip = definition.engineLoopClip != null
                ? definition.engineLoopClip
                : ProceduralAudioClips.CreateClip("EngineLoop_Procedural",
                    ProceduralAudioClips.GenerateEngineLoopSamples(55f, 2f));
            AudioClip swishClip = definition.cuttingLoopClip != null
                ? definition.cuttingLoopClip
                : ProceduralAudioClips.CreateClip("CutSwish_Procedural",
                    ProceduralAudioClips.GenerateSwishLoopSamples(1.5f));
            _basketFullClip = definition.basketFullClip != null
                ? definition.basketFullClip
                : ProceduralAudioClips.CreateClip("BasketFull_Procedural",
                    ProceduralAudioClips.GenerateBeepSamples(880f, 0.35f));

            _engineSource = CreateLoopSource(engineClip, _idleVolume, _idlePitch);
            _swishSource = CreateLoopSource(swishClip, 0f, 1f);
            _oneShotSource = gameObject.AddComponent<AudioSource>();
            _oneShotSource.playOnAwake = false;
            _oneShotSource.spatialBlend = 0f;
        }

        private AudioSource CreateLoopSource(AudioClip clip, float volume, float pitch)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 0f; // player-attached, 2D
            source.Play();
            return source;
        }

        private void Update()
        {
            MowerTool mower = _toolController.Mower;
            if (mower == null || _engineSource == null)
            {
                return;
            }

            float speed01 = Mathf.Clamp01(
                new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude / 4f);

            float targetVolume = mower.IsEngaged ? _engagedVolume : _idleVolume;
            float targetPitch = (mower.IsEngaged ? _engagedPitch : _idlePitch) + speed01 * _speedPitchGain;
            float blend = Mathf.Clamp01(Time.deltaTime / Mathf.Max(_responseSeconds, 0.01f));
            _engineSource.volume = Mathf.Lerp(_engineSource.volume, targetVolume, blend);
            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, blend);

            _swishSource.volume = Mathf.Lerp(
                _swishSource.volume, mower.SmoothedCutRate01 * _swishMaxVolume, blend);

            if (mower.BasketFull && !_basketWasFull)
            {
                _oneShotSource.PlayOneShot(_basketFullClip, 0.8f);
            }
            _basketWasFull = mower.BasketFull;
        }
    }
}
