using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// CharacterController-based first-person locomotion: walk, sprint, gravity.
    /// Yaw rotation is handled by <see cref="MouseLook"/> on the same object.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputProvider))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private float _walkSpeed = 3.5f;
        [SerializeField] private float _sprintSpeed = 6.0f;
        [SerializeField] private float _gravity = -9.81f;
        [SerializeField] private float _groundedStickVelocity = -2f;

        private CharacterController _controller;
        private PlayerInputProvider _input;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputProvider>();
        }

        private void Update()
        {
            Vector2 move = _input.Move.ReadValue<Vector2>();
            float speed = _input.Sprint.IsPressed() ? _sprintSpeed : _walkSpeed;

            Vector3 planar = (transform.right * move.x + transform.forward * move.y) * speed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundedStickVelocity;
            }
            _verticalVelocity += _gravity * Time.deltaTime;

            _controller.Move((planar + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }
    }
}
