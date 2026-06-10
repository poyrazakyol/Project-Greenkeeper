using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Debug keys for the grass renderer (T1 measurement aids, TDD §11):
    /// G toggles grass rendering (the honest A/B GPU cost switch),
    /// F5 toggles the LOD band tint. Input stays in the Debug assembly so
    /// Pitch never reads devices.
    /// </summary>
    public sealed class GrassDebugControls : MonoBehaviour
    {
        [SerializeField] private GrassRenderer _grass;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _grass == null)
            {
                return;
            }

            if (keyboard.gKey.wasPressedThisFrame)
            {
                _grass.RenderingEnabled = !_grass.RenderingEnabled;
            }
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                _grass.LodTintEnabled = !_grass.LodTintEnabled;
            }
        }

        private void OnGUI()
        {
            if (_grass != null && !_grass.RenderingEnabled)
            {
                GUI.Box(new Rect(8f, 36f, 220f, 24f), GUIContent.none);
                GUI.Label(new Rect(14f, 39f, 210f, 20f), "Grass OFF [G]");
            }
        }
    }
}
