// ─────────────────────────────────────────────────────────────────────────────
// File: Editor/DemoSceneBuilder.cs
// Module: Procedural terrain generation · Unity editor
// Status: Fully implemented
// ─────────────────────────────────────────────────────────────────────────────

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace TerrainDemo.Editor
{
    /// <summary>
    /// One-click editor tool that builds the demo scene, avoiding manual object setup.
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string TerrainName = "TerrainDemo_Terrain";
        private const string UiName = "TerrainDemo_UI";
        private const string PanelSettingsPath = "Assets/Settings/DemoPanelSettings.asset";
        private const string UxmlPath = "Assets/TerrainDemo/Runtime/UI/TerrainDemoUI.uxml";
        private const string UssPath = "Assets/TerrainDemo/Runtime/UI/TerrainDemoUI.uss";

        /// <summary>Menu entry: Terrain Demo → Setup Scene; repeatable (stale objects are cleaned up first).</summary>
        [MenuItem("Terrain Demo/Setup Scene")]
        public static void SetupScene()
        {
            Cleanup();

            // 1) Terrain root: mesh + collider + generator, and generate immediately so
            //    the mountain is visible as soon as the scene is saved.
            GameObject terrain = new GameObject(TerrainName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(TerrainGenerator));
            Undo.RegisterCreatedObjectUndo(terrain, "Setup Terrain Demo");
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            terrain.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(litShader != null ? litShader : Shader.Find("Standard"));
            terrain.GetComponent<TerrainGenerator>().Generate();

            // 2) Camera: reuse the template scene's Main Camera; pose it to overlook the
            //    200 m terrain.
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

            // 3) UI: create the object inactive first, so TerrainDemoUI.OnEnable — which
            //    runs on activation — sees a UIDocument with panel settings, UXML and USS
            //    all assigned.
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

            // 4) Light fallback: add a directional light only when the scene has none.
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

        /// <summary>Removes the previous round's objects by naming convention, keeping the menu idempotent.</summary>
        private static void Cleanup()
        {
            foreach (string goName in new[] { TerrainName, UiName })
            {
                GameObject existing = GameObject.Find(goName);
                if (existing != null) Undo.DestroyObjectImmediate(existing);
            }
        }

        /// <summary>
        /// Returns the existing PanelSettings asset or creates one. The theme style sheet
        /// is looked up inside packages on a best-effort basis; if none is found the
        /// panel's USS styling takes over — functionality is unaffected.
        /// </summary>
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

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. The demo scene consists of three kinds of objects: the terrain root, a
// camera with FlyCamera, and a UIDocument with the panel. Building them by hand is
// tedious and reference-prone; a one-click script makes the project "open and run",
// and provides a repeatable entry point for rebuilding after parameter changes.
//
// Principle. A MenuItem registers the static method as an editor menu. On click the
// objects are created programmatically: the terrain root gets MeshFilter/MeshRenderer/
// MeshCollider plus TerrainGenerator, a URP Lit material (Standard as fallback), and
// Generate() runs immediately so the mountain exists without pressing Play. The camera
// reuses the template scene's existing Main Camera (its native name) — only FlyCamera
// is added and the pose set — instead of spawning a duplicate. The UI object is created
// inactive, wired (panel settings, UXML, USS, generator reference), then activated, so
// TerrainDemoUI.OnEnable sees everything ready. Editor hygiene throughout: every
// created object is Undo-registered and the scene is marked dirty, so one-click setup
// is both undoable and saveable.
//
// Approach. The script is idempotent: objects from the previous round are removed by
// the TerrainDemo_ naming convention before rebuilding, so clicking repeatedly never
// stacks duplicates. References (UIDocument.panelSettings, TerrainDemoUI.Generator)
// are wired at creation time; anything missing fails loudly at the point of use rather
// than silently. Lights are only a fallback check — never duplicated.
// ─────────────────────────────────────────────────────────────────────────────
