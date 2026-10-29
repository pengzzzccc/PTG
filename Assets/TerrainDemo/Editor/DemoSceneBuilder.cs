using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using TerrainDemo;

namespace TerrainDemo.Editor
{
    public static class DemoSceneBuilder
    {
        private const string TerrainName = "TerrainDemo_Terrain";
        private const string UiName = "TerrainDemo_UI";
        private const string PanelSettingsPath = "Assets/Settings/DemoPanelSettings.asset";
        private const string UxmlPath = "Assets/TerrainDemo/Runtime/UI/TerrainDemoUI.uxml";
        private const string UssPath = "Assets/TerrainDemo/Runtime/UI/TerrainDemoUI.uss";

        [MenuItem("Terrain Demo/Setup Scene")]
        public static void SetupScene()
        {
            Cleanup();

            GameObject terrain = new GameObject(TerrainName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(TerrainGenerator), typeof(PerfLog));
            Undo.RegisterCreatedObjectUndo(terrain, "Setup Terrain Demo");
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            terrain.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(litShader != null ? litShader : Shader.Find("Standard"));
            terrain.GetComponent<TerrainGenerator>().Generate();

            GameObject cameraGo = GameObject.Find("Main Camera");
            if (cameraGo == null)
            {
                cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
                Undo.RegisterCreatedObjectUndo(cameraGo, "Setup Terrain Demo");
            }
            Undo.RecordObject(cameraGo.transform, "Setup Terrain Demo");
            cameraGo.transform.position = new Vector3(160f, 120f, -160f);
            cameraGo.transform.LookAt(new Vector3(0f, 20f, 0f));
            if (cameraGo.GetComponent<FlyCamera>() == null)
            {
                Undo.AddComponent<FlyCamera>(cameraGo);
            }

            GameObject ui = new GameObject(UiName);
            ui.SetActive(false);
            Undo.RegisterCreatedObjectUndo(ui, "Setup Terrain Demo");
            UIDocument document = ui.AddComponent<UIDocument>();
            TerrainDemoUI panel = ui.AddComponent<TerrainDemoUI>();
            document.panelSettings = GetOrCreatePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            panel.Generator = terrain.GetComponent<TerrainGenerator>();
            panel.PanelStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            ui.SetActive(true);

            if (!Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Any(light => light.type == LightType.Directional))
            {
                GameObject lightGo = new GameObject("Directional Light", typeof(Light));
                Undo.RegisterCreatedObjectUndo(lightGo, "Setup Terrain Demo");
                lightGo.GetComponent<Light>().type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            Selection.activeGameObject = terrain;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Terrain Demo] Scene ready: terrain generated, camera and UI panel wired. Enter Play mode to use the panel.");
        }

        private static void Cleanup()
        {
            foreach (string goName in new[] { TerrainName, UiName })
            {
                GameObject existing = GameObject.Find(goName);
                if (existing != null) Undo.DestroyObjectImmediate(existing);
            }
        }

        private static PanelSettings GetOrCreatePanelSettings()
        {
            PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;

            string themeGuid = AssetDatabase.FindAssets("t:ThemeStyleSheet", new[] { "Packages" }).FirstOrDefault();
            if (themeGuid != null)
            {
                settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(themeGuid));
            }

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            return settings;
        }
    }
}
