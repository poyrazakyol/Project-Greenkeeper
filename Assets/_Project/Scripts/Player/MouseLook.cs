using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Mouse look: yaw rotates the player body (this transform), pitch rotates
    /// only the camera target so the CharacterController stays upright.
    /// Locks the cursor while enabled.
    /// </summary>
    [RequireComponent(typeof(PlayerInputProvider))]
    public sealed class MouseLook : MonoBehaviour
    {
        [SerializeField] private Transform _pitchTarget;
        [SerializeField] private float _sensitivity = 0.1f;
        [SerializeField] private float _pitchLimit = 85f;

        private PlayerInputProvider _input;
        private float _pitch;

        private void Awake()
        {
            _input = GetComponent<PlayerInputProvider>();
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            Vector2 look = _input.Look.ReadValue<Vector2>() * _sensitivity;

            transform.Rotate(0f, look.x, 0f);

            _pitch = Mathf.Clamp(_pitch - look.y, -_pitchLimit, _pitchLimit);
            if (_pitchTarget != null)
            {
                _pitchTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }
    }
}
