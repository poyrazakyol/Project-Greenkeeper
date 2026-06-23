using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using UnityEngine;

namespace Greenkeeper.Gameplay
{
    /// <summary>
    /// Player-side tool host (TDD §4.1): owns the tool lifecycle, builds a
    /// ToolContext per frame and routes input to the current tool. M.2 adds a
    /// second tool (liner); Q (ToolWheel) cycles mower↔liner — a real radial
    /// wheel + OnToolChanged channel arrive at M.9. Scene references
    /// (_pitch/_bridge) are wired on the scene instance by the builder.
    /// </summary>
    [RequireComponent(typeof(PlayerInputProvider))]
    public sealed class ToolController : MonoBehaviour
    {
        [SerializeField] private PitchController _pitch;
        [SerializeField] private PitchRenderBridge _bridge;
        [SerializeField] private ToolDefinitionSO _mowerDefinition;
        [SerializeField] private LinerToolDefinitionSO _linerDefinition;

        public MowerTool Mower { get; private set; }
        public LinerTool Liner { get; private set; }
        public ITool CurrentTool { get; private set; }
        public bool MowerActive => CurrentTool == Mower;

        private PlayerInputProvider _input;
        private ITool[] _tools;
        private int _currentIndex;
        private Transform _mowerVisual;
        private Transform _linerVisual;

        private void Awake()
        {
            _input = GetComponent<PlayerInputProvider>();
            // Constructed in Awake (needs only the SOs) so sibling components
            // (MowerAudio, CameraFeel) can rely on the tools being non-null in
            // their Start — Unity runs all Awakes before any Start, while the
            // order of Starts on one GameObject is undefined.
            Mower = new MowerTool(_mowerDefinition);
            Liner = new LinerTool(_linerDefinition);
            _tools = new ITool[] { Mower, Liner };
            CurrentTool = Mower;
        }

        private void Start()
        {
            // Equip stays in Start: the context touches scene-wired Pitch
            // components whose own Awakes must have run.
            _mowerVisual = BuildPlaceholderMowerVisual();
            _linerVisual = BuildPlaceholderLinerVisual();
            CurrentTool.OnEquip(BuildContext());
            ApplyVisualForCurrentTool();
        }

        private void Update()
        {
            ToolContext ctx = BuildContext();

            if (_input.ToolWheel.WasPressedThisFrame())
            {
                CycleTool(ctx);
            }

            if (MowerActive)
            {
                UpdateMowerInput(ctx);
            }

            if (_input.UseTool.IsPressed())
            {
                CurrentTool.OnUseHeld(ctx, Time.deltaTime);
            }
            else if (_input.UseTool.WasReleasedThisFrame())
            {
                CurrentTool.OnUseReleased(ctx);
            }
        }

        private void UpdateMowerInput(in ToolContext ctx)
        {
            float scroll = _input.HeightAdjust.ReadValue<float>();
            if (scroll > 0.01f)
            {
                Mower.CycleHeightStep(1);
            }
            else if (scroll < -0.01f)
            {
                Mower.CycleHeightStep(-1);
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

        private void CycleTool(in ToolContext ctx)
        {
            CurrentTool.OnUseReleased(ctx); // close any active stroke
            CurrentTool.OnUnequip();
            _currentIndex = (_currentIndex + 1) % _tools.Length;
            CurrentTool = _tools[_currentIndex];
            CurrentTool.OnEquip(ctx);
            ApplyVisualForCurrentTool();
        }

        private void ApplyVisualForCurrentTool()
        {
            if (_mowerVisual != null)
            {
                _mowerVisual.gameObject.SetActive(MowerActive);
            }
            if (_linerVisual != null)
            {
                _linerVisual.gameObject.SetActive(!MowerActive);
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

        /// <summary>Slim primitive liner cart (placeholder, like the mower).</summary>
        private Transform BuildPlaceholderLinerVisual()
        {
            var root = new GameObject("LinerVisual_Placeholder").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 0f, _linerDefinition.forwardOffsetM);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.name = "PaintBox";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            body.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

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
