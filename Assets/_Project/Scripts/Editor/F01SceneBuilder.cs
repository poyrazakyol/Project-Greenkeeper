using Greenkeeper.Core;
using Greenkeeper.Debugging;
using Greenkeeper.Gameplay;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greenkeeper.EditorTools
{
    /// <summary>
    /// Builds the F0.1 deliverables from code: ground material, Player prefab,
    /// Boot and Stadium scenes, and the build scene list. Idempotent — running
    /// it again regenerates the same assets in place.
    /// </summary>
    public static class F01SceneBuilder
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string ArtDir = "Assets/_Project/Art";
        private const string GroundMaterialPath = ArtDir + "/Pitch_Ground.mat";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string InputActionsPath = "Assets/_Project/Settings/Greenkeeper.inputactions";

        [MenuItem("Greenkeeper/Build F0.1 Scenes")]
        public static void Build()
        {
            Material groundMaterial = CreateOrUpdateGroundMaterial();
            GameObject playerPrefab = CreatePlayerPrefab();
            CreateBootScene();
            CreateStadiumScene(groundMaterial, playerPrefab);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesDir + "/Boot.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/Stadium.unity", true)
            };

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[Greenkeeper] F0.1 scenes, prefab and build settings generated.");
        }

        private static Material CreateOrUpdateGroundMaterial()
        {
            // Placeholder pitch green; the real grass system arrives in F0.4.
            var pitchGreen = new Color(0.16f, 0.42f, 0.16f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }
            material.SetColor("_BaseColor", pitchGreen);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePlayerPrefab()
        {
            var root = new GameObject("Player");
            try
            {
                CharacterController controller = root.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0f, 0.9f, 0f);

                PlayerInputProvider inputProvider = root.AddComponent<PlayerInputProvider>();
                root.AddComponent<FirstPersonController>();
                MouseLook mouseLook = root.AddComponent<MouseLook>();

                var cameraTarget = new GameObject("CameraTarget").transform;
                cameraTarget.SetParent(root.transform, false);
                cameraTarget.localPosition = new Vector3(0f, 1.65f, 0f);

                var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (actionsAsset == null)
                {
                    throw new System.InvalidOperationException(
                        $"Input actions asset not found at {InputActionsPath}.");
                }

                var serializedInput = new SerializedObject(inputProvider);
                serializedInput.FindProperty("_actionsAsset").objectReferenceValue = actionsAsset;
                serializedInput.ApplyModifiedPropertiesWithoutUndo();

                var serialized = new SerializedObject(mouseLook);
                serialized.FindProperty("_pitchTarget").objectReferenceValue = cameraTarget;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var loader = new GameObject("BootLoader");
            loader.AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, ScenesDir + "/Boot.unity");
        }

        private static void CreateStadiumScene(Material groundMaterial, GameObject playerPrefab)
        {
            // DefaultGameObjects keeps the default skybox/ambient lighting setup.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject sun = GameObject.Find("Directional Light");
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = sun.GetComponent<Light>();
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;

            // 105x68 m pitch placeholder (built-in plane is 10x10 m at scale 1).
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10.5f, 1f, 6.8f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            ground.isStatic = true;

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(0f, 0.05f, 0f);
            Transform cameraTarget = player.transform.Find("CameraTarget");

            GameObject mainCamera = GameObject.Find("Main Camera");
            mainCamera.AddComponent<CinemachineBrain>();

            var fpCamera = new GameObject("CM_FirstPerson");
            var cinemachineCamera = fpCamera.AddComponent<CinemachineCamera>();
            fpCamera.AddComponent<CinemachineHardLockToTarget>();
            fpCamera.AddComponent<CinemachineRotateWithFollowTarget>();
            cinemachineCamera.Follow = cameraTarget;

            new GameObject("Debug_PerfHud").AddComponent<PerfHud>();

            EditorSceneManager.SaveScene(scene, ScenesDir + "/Stadium.unity");
        }
    }
}
