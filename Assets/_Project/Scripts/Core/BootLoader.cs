using UnityEngine;
using UnityEngine.SceneManagement;

namespace Greenkeeper.Core
{
    /// <summary>
    /// Entry point of the Boot scene. Phase 0 has no main menu (GDD §5.1),
    /// so it loads the Stadium scene directly. The MainMenu hop is added in MVP.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        [SerializeField] private string _stadiumSceneName = "Stadium";

        private void Start()
        {
            SceneManager.LoadSceneAsync(_stadiumSceneName);
        }
    }
}
