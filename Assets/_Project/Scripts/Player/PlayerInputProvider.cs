using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Owns the Player action map of the shared input asset so sibling
    /// components (movement, look, tools later) read one action state.
    /// Actions are resolved by name once in Awake; a missing action throws
    /// immediately instead of failing silently at first read.
    /// </summary>
    public sealed class PlayerInputProvider : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actionsAsset;

        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction UseTool { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction HeightAdjust { get; private set; }

        private InputActionMap _playerMap;

        private void Awake()
        {
            _playerMap = _actionsAsset.FindActionMap("Player", throwIfNotFound: true);
            Move = _playerMap.FindAction("Move", throwIfNotFound: true);
            Look = _playerMap.FindAction("Look", throwIfNotFound: true);
            Sprint = _playerMap.FindAction("Sprint", throwIfNotFound: true);
            UseTool = _playerMap.FindAction("UseTool", throwIfNotFound: true);
            Interact = _playerMap.FindAction("Interact", throwIfNotFound: true);
            HeightAdjust = _playerMap.FindAction("HeightAdjust", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _playerMap.Enable();
        }

        private void OnDisable()
        {
            _playerMap.Disable();
        }
    }
}
