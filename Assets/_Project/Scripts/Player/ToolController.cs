using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Player-side tool host (TDD §4.1): owns the tool lifecycle, builds a
    /// ToolContext per frame and routes input to the equipped tool. Phase 0
    /// has a single always-equipped MowerTool; the tool wheel arrives with
    /// MVP. Scene references (_pitch/_bridge) are wired on the scene instance
    /// by the builder, not in the prefab.
    /// </summary>
    [RequireComponent(typeof(PlayerInputProvider))]
    public sealed class ToolController : MonoBehaviour
    {
        [SerializeField] private PitchController _pitch;
        [SerializeField] private PitchRenderBridge _bridge;
        [SerializeField] private ToolDefinitionSO _mowerDefinition;

        /// <summary>Concrete access for the debug HUD (Phase 0 single tool).</summary>
        public MowerTool Mower { get; private set; }

        private PlayerInputProvider _input;
        private Transform _mowerVisual;

        private void Awake()
        {
            _input = GetComponent<PlayerInputProvider>();
        }

        private void Start()
        {
            Mower = new MowerTool(_mowerDefinition);
            Mower.OnEquip(BuildContext());
            _mowerVisual = BuildPlaceholderMowerVisual();
        }

        private void Update()
        {
            ToolContext ctx = BuildContext();

            float scroll = _input.HeightAdjust.ReadValue<float>();
            if (scroll > 0.01f)
            {
                Mower.CycleHeightStep(1);
            }
            else if (scroll < -0.01f)
            {
                Mower.CycleHeightStep(-1);
            }

            if (_input.UseTool.IsPressed())
            {
                Mower.OnUseHeld(ctx, Time.deltaTime);
            }
            else if (_input.UseTool.WasReleasedThisFrame())
            {
                Mower.OnUseReleased(ctx);
            }

            if (_input.Interact.IsPressed())
            {
                Mower.TickEmptyHold(Time.deltaTime);
            }
            else if (_input.Interact.WasReleasedThisFrame())
            {
                Mower.ResetEmptyHold();
            }
        }

        private ToolContext BuildContext()
        {
            var lookRay = new Ray(transform.position + Vector3.up * 1.65f, transform.forward);
            return new ToolContext(
                transform.position, transform.forward, lookRay,
                _pitch.Grid, _bridge.EnqueueStampAction);
        }

        /// <summary>
        /// Crude primitive push mower so the sweep location reads on screen.
        /// Real model, animation and feel pass arrive with F0.7.
        /// </summary>
        private Transform BuildPlaceholderMowerVisual()
        {
            var root = new GameObject("MowerVisual_Placeholder").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, _mowerDefinition.mowForwardOffsetM);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            body.transform.localScale = new Vector3(_mowerDefinition.mowWidthM, 0.25f, 0.45f);

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(handle.GetComponent<Collider>());
            handle.name = "Handle";
            handle.transform.SetParent(root, false);
            handle.transform.localPosition = new Vector3(0f, 0.6f, -0.35f);
            handle.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.05f, 0.45f, 0.05f);

            return root;
        }
    }
}
