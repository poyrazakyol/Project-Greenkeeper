using Greenkeeper.Core;
using Greenkeeper.Core.Events;
using Greenkeeper.Debugging;
using Greenkeeper.Gameplay;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using Greenkeeper.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Greenkeeper.EditorTools
{
    /// <summary>
    /// Builds the generated project assets from code: ground material, stadium
    /// definition, Player prefab, Boot and Stadium scenes, and the build scene
    /// list. Cumulative across blocks (F0.1: scenes + player; F0.2: pitch grid
    /// host + F1 overlay). Idempotent — assets are regenerated in place and
    /// existing SO asset values are preserved.
    /// </summary>
    public static class StadiumSceneBuilder
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string ArtDir = "Assets/_Project/Art";
        private const string GroundMaterialPath = ArtDir + "/Pitch_Ground.mat";
        private const string PlayerPrefabPath = PrefabsDir + "/Player.prefab";
        private const string InputActionsPath = "Assets/_Project/Settings/Greenkeeper.inputactions";
        private const string StadiumDefinitionPath = "Assets/_Project/Data/Stadium_Default.asset";
        private const string BrushShaderPath = "Assets/_Project/Shaders/PitchBrush.shader";
        private const string GrassSettingsPath = "Assets/_Project/Data/GrassRender_Default.asset";
        private const string GrassShaderPath = "Assets/_Project/Shaders/GrassBlade.shader";
        private const string GroundShaderPath = "Assets/_Project/Shaders/PitchGround.shader";
        private const string MowerDefinitionPath = "Assets/_Project/Data/Tool_PushMower.asset";
        private const string LinerDefinitionPath = "Assets/_Project/Data/Tool_PushLiner.asset";
        private const string HoseDefinitionPath = "Assets/_Project/Data/Tool_PushHose.asset";
        private const string LineLayoutPath = "Assets/_Project/Data/LineLayout_Default.asset";
        private const string ChannelsDir = "Assets/_Project/Data/Channels";
        private const string MowProgressChannelPath = ChannelsDir + "/OnMowProgress.asset";
        private const string CameraNoisePath = "Assets/_Project/Data/CameraNoise_FP.asset";
        private const string GrowthProfilePath = "Assets/_Project/Data/GrassGrowth_Default.asset";
        private const string DayEndedChannelPath = ChannelsDir + "/OnDayEnded.asset";

        [MenuItem("Greenkeeper/Build Stadium Scene")]
        public static void Build()
        {
            // GPU frame timings feed the F2 HUD (T1 measurement, TDD §13).
            PlayerSettings.enableFrameTimingStats = true;
            // Keep ticking when the Game view loses focus — otherwise time
            // appears frozen during playtesting (M.1 day clock investigation).
            PlayerSettings.runInBackground = true;

            Material groundMaterial = CreateOrUpdateGroundMaterial();
            EnsureStadiumDefinitionAsset();
            EnsureGrassSettingsAsset();
            EnsureToolAndChannelAssets();
            EnsureCameraNoiseAsset();
            GameObject playerPrefab = CreatePlayerPrefab();
            CreateBootScene();
            CreateStadiumScene(groundMaterial, playerPrefab);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesDir + "/Boot.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/Stadium.unity", true)
            };

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[Greenkeeper] Stadium scene, prefab and build settings generated.");
        }

        private static void EnsureStadiumDefinitionAsset()
        {
            // Create-if-missing only: designer-tuned values in the asset are
            // never overwritten by the builder (data lives in the SO, TDD §1.2).
            Object main = AssetDatabase.LoadMainAssetAtPath(StadiumDefinitionPath);
            UnityEngine.Debug.Log("[Greenkeeper] Stadium definition probe: " +
                (main == null ? "not found" : $"type={main.GetType().FullName}"));

            if (AssetDatabase.LoadAssetAtPath<StadiumDefinitionSO>(StadiumDefinitionPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<StadiumDefinitionSO>(), StadiumDefinitionPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(StadiumDefinitionPath);
                UnityEngine.Debug.Log("[Greenkeeper] Stadium definition created at " + StadiumDefinitionPath);
            }
        }

        private static void EnsureGrassSettingsAsset()
        {
            // Create-if-missing only, same policy as the stadium definition.
            if (AssetDatabase.LoadAssetAtPath<GrassRenderSettingsSO>(GrassSettingsPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<GrassRenderSettingsSO>(), GrassSettingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(GrassSettingsPath);
                UnityEngine.Debug.Log("[Greenkeeper] Grass render settings created at " + GrassSettingsPath);
            }
        }

        private static void EnsureToolAndChannelAssets()
        {
            // Create-if-missing only, same policy as the stadium definition.
            if (AssetDatabase.LoadAssetAtPath<ToolDefinitionSO>(MowerDefinitionPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<ToolDefinitionSO>(), MowerDefinitionPath);
                UnityEngine.Debug.Log("[Greenkeeper] Mower definition created at " + MowerDefinitionPath);
            }

            if (!AssetDatabase.IsValidFolder(ChannelsDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Channels");
            }
            if (AssetDatabase.LoadAssetAtPath<FloatEventChannelSO>(MowProgressChannelPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<FloatEventChannelSO>(), MowProgressChannelPath);
                UnityEngine.Debug.Log("[Greenkeeper] Mow progress channel created at " + MowProgressChannelPath);
            }
            if (AssetDatabase.LoadAssetAtPath<DayEndedEventChannelSO>(DayEndedChannelPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<DayEndedEventChannelSO>(), DayEndedChannelPath);
                UnityEngine.Debug.Log("[Greenkeeper] Day ended channel created at " + DayEndedChannelPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GrassGrowthProfileSO>(GrowthProfilePath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<GrassGrowthProfileSO>(), GrowthProfilePath);
                UnityEngine.Debug.Log("[Greenkeeper] Grass growth profile created at " + GrowthProfilePath);
            }
            if (AssetDatabase.LoadAssetAtPath<LinerToolDefinitionSO>(LinerDefinitionPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<LinerToolDefinitionSO>(), LinerDefinitionPath);
                UnityEngine.Debug.Log("[Greenkeeper] Liner definition created at " + LinerDefinitionPath);
            }
            if (AssetDatabase.LoadAssetAtPath<HoseToolDefinitionSO>(HoseDefinitionPath) == null)
            {
                AssetDatabase.CreateAsset(
                    ScriptableObject.CreateInstance<HoseToolDefinitionSO>(), HoseDefinitionPath);
                UnityEngine.Debug.Log("[Greenkeeper] Hose definition created at " + HoseDefinitionPath);
            }
            if (AssetDatabase.LoadAssetAtPath<LineLayoutSO>(LineLayoutPath) == null)
            {
                var layout = ScriptableObject.CreateInstance<LineLayoutSO>();
                var serialized = new SerializedObject(layout);
                serialized.FindProperty("_stadium").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<StadiumDefinitionSO>(StadiumDefinitionPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(layout, LineLayoutPath);
                UnityEngine.Debug.Log("[Greenkeeper] Line layout created at " + LineLayoutPath);
            }
            AssetDatabase.SaveAssets();
        }

        private static void EnsureCameraNoiseAsset()
        {
            // Built in code instead of loading a Cinemachine package preset:
            // package paths shift between versions, this stays deterministic.
            if (AssetDatabase.LoadAssetAtPath<NoiseSettings>(CameraNoisePath) != null)
            {
                return;
            }
            var noise = ScriptableObject.CreateInstance<NoiseSettings>();
            noise.PositionNoise = new NoiseSettings.TransformNoiseParams[0];
            noise.OrientationNoise = new[]
            {
                new NoiseSettings.TransformNoiseParams
                {
                    X = new NoiseSettings.NoiseParams { Amplitude = 0.8f, Frequency = 1.1f },
                    Y = new NoiseSettings.NoiseParams { Amplitude = 0.6f, Frequency = 0.9f },
                    Z = new NoiseSettings.NoiseParams { Amplitude = 0.25f, Frequency = 0.7f }
                }
            };
            AssetDatabase.CreateAsset(noise, CameraNoisePath);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("[Greenkeeper] Camera noise profile created at " + CameraNoisePath);
        }

        private static Material CreateOrUpdateGroundMaterial()
        {
            // F0.4: the ground is the far-LOD half of the grass system — it
            // samples RT_GrassState via the PitchGround shader (TDD §3.3).
            Shader groundShader = AssetDatabase.LoadAssetAtPath<Shader>(GroundShaderPath);
            if (groundShader == null)
            {
                throw new System.InvalidOperationException(
                    $"Ground shader not found at {GroundShaderPath}.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material == null)
            {
                material = new Material(groundShader);
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }
            else if (material.shader != groundShader)
            {
                material.shader = groundShader;
            }
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
                ToolController toolController = root.AddComponent<ToolController>();

                // Asset reference lives in the prefab; scene references
                // (_pitch/_bridge) are wired on the scene instance.
                var mowerDefinition = AssetDatabase.LoadAssetAtPath<ToolDefinitionSO>(MowerDefinitionPath);
                SetReference(toolController, "_mowerDefinition", mowerDefinition);
                var linerDefinition = AssetDatabase.LoadAssetAtPath<LinerToolDefinitionSO>(LinerDefinitionPath);
                SetReference(toolController, "_linerDefinition", linerDefinition);
                var hoseDefinition = AssetDatabase.LoadAssetAtPath<HoseToolDefinitionSO>(HoseDefinitionPath);
                SetReference(toolController, "_hoseDefinition", hoseDefinition);

                // Feel layer (F0.7). CameraFeel._noise is a scene object,
                // wired on the instance.
                root.AddComponent<MowerAudio>();
                root.AddComponent<CameraFeel>();

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

            // Camera feel noise (F0.7): amplitude is driven by CameraFeel.
            var cameraNoise = fpCamera.AddComponent<CinemachineBasicMultiChannelPerlin>();
            cameraNoise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(CameraNoisePath);
            cameraNoise.AmplitudeGain = 0f;

            // Pitch grid host (F0.2). The stadium asset is loaded here, AFTER
            // NewScene: single-mode scene creation unloads unused assets, so a
            // reference held from before that call goes fake-null.
            var stadiumDefinition = AssetDatabase.LoadAssetAtPath<StadiumDefinitionSO>(StadiumDefinitionPath);

            // Time service (M.1)
            TimeSystem timeSystem = new GameObject("TimeSystem").AddComponent<TimeSystem>();

            var pitchGo = new GameObject("Pitch");
            PitchController pitchController = pitchGo.AddComponent<PitchController>();
            SetReference(pitchController, "_stadium", stadiumDefinition);
            SetReference(pitchController, "_timeSystem", timeSystem);

            // GPU render bridge (F0.3)
            var brushShader = AssetDatabase.LoadAssetAtPath<Shader>(BrushShaderPath);
            PitchRenderBridge renderBridge = pitchGo.AddComponent<PitchRenderBridge>();
            SetReference(renderBridge, "_stadium", stadiumDefinition);
            SetReference(renderBridge, "_brushShader", brushShader);
            SetReference(renderBridge, "_pitchController", pitchController);

            // Instanced grass renderer (F0.4)
            var grassSettings = AssetDatabase.LoadAssetAtPath<GrassRenderSettingsSO>(GrassSettingsPath);
            var grassShader = AssetDatabase.LoadAssetAtPath<Shader>(GrassShaderPath);
            GrassRenderer grassRenderer = pitchGo.AddComponent<GrassRenderer>();
            SetReference(grassRenderer, "_bridge", renderBridge);
            SetReference(grassRenderer, "_settings", grassSettings);
            SetReference(grassRenderer, "_grassShader", grassShader);

            PerfHud perfHud = new GameObject("Debug_PerfHud").AddComponent<PerfHud>();
            SetReference(perfHud, "_pitch", pitchController);
            SetReference(perfHud, "_renderBridge", renderBridge);
            SetReference(perfHud, "_grass", grassRenderer);

            PitchGridOverlay overlay = new GameObject("Debug_PitchOverlay").AddComponent<PitchGridOverlay>();
            SetReference(overlay, "_pitch", pitchController);

            RtDebugViewer rtViewer = new GameObject("Debug_RtViewer").AddComponent<RtDebugViewer>();
            SetReference(rtViewer, "_bridge", renderBridge);

            GrassDebugControls grassControls =
                new GameObject("Debug_GrassControls").AddComponent<GrassDebugControls>();
            SetReference(grassControls, "_grass", grassRenderer);

            // Tool wiring (F0.5): scene refs go on the prefab INSTANCE.
            var progressChannel = AssetDatabase.LoadAssetAtPath<FloatEventChannelSO>(MowProgressChannelPath);
            ToolController toolController = player.GetComponent<ToolController>();
            SetReference(toolController, "_pitch", pitchController);
            SetReference(toolController, "_bridge", renderBridge);

            MowProgressTracker progressTracker =
                new GameObject("MowProgressTracker").AddComponent<MowProgressTracker>();
            SetReference(progressTracker, "_pitch", pitchController);
            SetReference(progressTracker, "_toolController", toolController);
            SetReference(progressTracker, "_progressChannel", progressChannel);

            MowHud mowHud = new GameObject("Debug_MowHud").AddComponent<MowHud>();
            SetReference(mowHud, "_toolController", toolController);
            SetReference(mowHud, "_progressChannel", progressChannel);

            // Line score debug view (M.2)
            var lineLayout = AssetDatabase.LoadAssetAtPath<LineLayoutSO>(LineLayoutPath);
            LineScoreHud lineScoreHud = new GameObject("Debug_LineScoreHud").AddComponent<LineScoreHud>();
            SetReference(lineScoreHud, "_toolController", toolController);
            SetReference(lineScoreHud, "_lineLayout", lineLayout);

            // Hose / moisture dose debug view (M.3)
            HoseHud hoseHud = new GameObject("Debug_HoseHud").AddComponent<HoseHud>();
            SetReference(hoseHud, "_toolController", toolController);
            SetReference(hoseHud, "_pitch", pitchController);
            SetReference(hoseHud, "_grassSettings", grassSettings);

            // Feel wiring (F0.7).
            CameraFeel cameraFeel = player.GetComponent<CameraFeel>();
            SetReference(cameraFeel, "_noise", cameraNoise);

            // Day-end pipeline (M.1)
            var dayEndedChannel = AssetDatabase.LoadAssetAtPath<DayEndedEventChannelSO>(DayEndedChannelPath);
            var growthProfile = AssetDatabase.LoadAssetAtPath<GrassGrowthProfileSO>(GrowthProfilePath);

            MowCompletionCelebration celebration =
                new GameObject("MowCompletion").AddComponent<MowCompletionCelebration>();
            SetReference(celebration, "_progressChannel", progressChannel);
            SetReference(celebration, "_dayEndedChannel", dayEndedChannel);
            SetReference(celebration, "_cameraFeel", cameraFeel);

            DayEndController dayEndController =
                new GameObject("DayEndController").AddComponent<DayEndController>();
            SetReference(dayEndController, "_timeSystem", timeSystem);
            SetReference(dayEndController, "_pitch", pitchController);
            SetReference(dayEndController, "_bridge", renderBridge);
            SetReference(dayEndController, "_growthProfile", growthProfile);
            SetReference(dayEndController, "_dayEndedChannel", dayEndedChannel);
            SetReference(dayEndController, "_input", player.GetComponent<PlayerInputProvider>());

            MorningSummaryPanel summaryPanel =
                new GameObject("UI_MorningSummary").AddComponent<MorningSummaryPanel>();
            SetReference(summaryPanel, "_dayEndedChannel", dayEndedChannel);
            SetReference(summaryPanel, "_timeSystem", timeSystem);

            TimeHud timeHud = new GameObject("Debug_TimeHud").AddComponent<TimeHud>();
            SetReference(timeHud, "_timeSystem", timeSystem);

            EditorSceneManager.SaveScene(scene, ScenesDir + "/Stadium.unity");
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            if (value == null)
            {
                throw new System.InvalidOperationException(
                    $"Refusing to write a null reference into {target.GetType().Name}.{fieldName} — " +
                    "the source object is missing or was invalidated by a reimport.");
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"Serialized field '{fieldName}' not found on {target.GetType().Name}.");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
