using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// F1 grid layer overlay (TDD §11): semi-transparent quad projected just
    /// above the ground, colored from the CPU grid. F1 cycles layers
    /// (Off → Moisture → Health → Height → Damage → Pattern). Pattern renders
    /// direction as hue; arrow glyphs may come with F0.6 if needed.
    /// </summary>
    public sealed class PitchGridOverlay : MonoBehaviour
    {
        private enum OverlayLayer
        {
            Off,
            Moisture,
            Health,
            Height,
            Damage,
            Pattern
        }

        [SerializeField] private PitchController _pitch;
        [SerializeField] private float _refreshIntervalSeconds = 0.25f;
        [SerializeField] private float _overlayHeightM = 0.05f;
        [Range(0f, 1f)]
        [SerializeField] private float _opacity = 0.6f;

        private OverlayLayer _layer = OverlayLayer.Off;
        private GameObject _quad;
        private Material _material;
        private Texture2D _texture;
        private Color32[] _pixels;
        private float _refreshTimer;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                _layer = (OverlayLayer)(((int)_layer + 1) % System.Enum.GetValues(typeof(OverlayLayer)).Length);
                ApplyLayerChange();
            }

            if (_layer == OverlayLayer.Off || _pitch == null || _pitch.Grid == null)
            {
                return;
            }

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= _refreshIntervalSeconds)
            {
                _refreshTimer = 0f;
                RefreshTexture();
            }
        }

        private void ApplyLayerChange()
        {
            bool visible = _layer != OverlayLayer.Off && _pitch != null && _pitch.Grid != null;
            if (visible && _quad == null)
            {
                CreateQuad();
            }
            if (_quad != null)
            {
                _quad.SetActive(visible);
            }
            if (visible)
            {
                _refreshTimer = 0f;
                RefreshTexture();
            }
        }

        private void CreateQuad()
        {
            PitchGrid grid = _pitch.Grid;

            _texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixels = new Color32[grid.CellCount];

            // Sprites/Default: unlit + transparent, works under URP — enough for a debug view.
            _material = new Material(Shader.Find("Sprites/Default"))
            {
                mainTexture = _texture,
                color = new Color(1f, 1f, 1f, _opacity)
            };

            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "PitchGridOverlayQuad";
            _quad.transform.SetParent(transform, false);
            Destroy(_quad.GetComponent<Collider>());

            float worldWidth = grid.Width * grid.CellSize;
            float worldHeight = grid.Height * grid.CellSize;
            // Lay the quad flat: local +Y becomes world +Z, so texture V tracks grid Y.
            _quad.transform.SetPositionAndRotation(
                new Vector3(
                    grid.WorldOrigin.x + worldWidth * 0.5f,
                    _overlayHeightM,
                    grid.WorldOrigin.y + worldHeight * 0.5f),
                Quaternion.Euler(90f, 0f, 0f));
            _quad.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
            _quad.GetComponent<MeshRenderer>().sharedMaterial = _material;
        }

        private void RefreshTexture()
        {
            PitchGrid grid = _pitch.Grid;
            var cells = grid.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                _pixels[i] = ColorFor(cells[i]);
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        private Color32 ColorFor(in CellState cell)
        {
            switch (_layer)
            {
                case OverlayLayer.Moisture:
                    return Lerp(new Color32(200, 60, 40, 255), new Color32(30, 90, 220, 255), cell.Moisture / 100f);
                case OverlayLayer.Health:
                    return Lerp(new Color32(200, 40, 40, 255), new Color32(40, 200, 60, 255), cell.Health / 100f);
                case OverlayLayer.Height:
                {
                    byte v = (byte)(Mathf.Clamp01((float)cell.GrassHeightMm / 60f) * 255f);
                    return new Color32(v, v, v, 255);
                }
                case OverlayLayer.Damage:
                    return Lerp(new Color32(40, 160, 60, 255), new Color32(220, 30, 30, 255), cell.Damage / 100f);
                case OverlayLayer.Pattern:
                {
                    float dirX = cell.PatternDirX;
                    float dirY = cell.PatternDirY;
                    if (dirX * dirX + dirY * dirY < 0.0001f)
                    {
                        return new Color32(120, 120, 120, 255);
                    }
                    float hue = (Mathf.Atan2(dirY, dirX) / (2f * Mathf.PI) + 1f) % 1f;
                    return Color.HSVToRGB(hue, 0.9f, 1f);
                }
                default:
                    return new Color32(0, 0, 0, 0);
            }
        }

        private static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            return Color32.Lerp(a, b, Mathf.Clamp01(t));
        }

        private void OnGUI()
        {
            if (_layer == OverlayLayer.Off)
            {
                return;
            }

            string legend = _layer switch
            {
                OverlayLayer.Moisture => "Moisture  (red = 0, blue = 100)",
                OverlayLayer.Health => "Health  (red = 0, green = 100)",
                OverlayLayer.Height => "Grass height  (black = 0 mm, white = 60 mm)",
                OverlayLayer.Damage => "Damage  (green = 0, red = 100)",
                OverlayLayer.Pattern => "Pattern direction  (hue = angle, gray = none)",
                _ => string.Empty
            };
            GUI.Box(new Rect(8f, 36f, 320f, 24f), GUIContent.none);
            GUI.Label(new Rect(14f, 39f, 310f, 20f), $"[F1] Overlay: {legend}");
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }
    }
}
