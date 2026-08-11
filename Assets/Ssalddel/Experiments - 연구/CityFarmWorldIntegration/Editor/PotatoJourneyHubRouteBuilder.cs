using System;
using System.IO;
using Ssalddel.Unity.PotatoJourney;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class PotatoJourneyHubRouteBuilder
    {
        public const string ScenePath =
            연구Scene경로.감자생산유통 + "/감자농장물류거점이동.unity";
        public const string RootName = "PVS6 Potato Journey Hub Route";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-potato-journey-hub-route/potato-journey-hub-route-game-view.png";
        private const string VanPath =
            "Assets/Synty/PolygonCity/Prefabs/Vehicles/SM_Veh_Car_Van_01.prefab";

        [MenuItem("Ssalddel/PVS/Build Potato Journey Hub Route")]
        public static void Build()
        {
            if (!File.Exists(PotatoHarvestCargoLifecycleBuilder.ScenePath))
                PotatoHarvestCargoLifecycleBuilder.Build();
            EditorSceneManager.OpenScene(PotatoHarvestCargoLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                        ?? throw new InvalidOperationException("PotatoJourneyHubWorldRootMissing");
            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var previousTitle = GameObject.Find(
                "WorldBootstrap/PVS5 Potato Journey Farm Slice/PotatoJourneyDataCanvas/WorldTitlePanel");
            if (previousTitle != null) previousTitle.SetActive(false);
            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var cargoLifecycle = world.transform.Find(PotatoHarvestCargoLifecycleBuilder.RootName)
                ?.GetComponent<PotatoHarvestCargoLifecyclePresenter>()
                ?? throw new InvalidOperationException("PotatoJourneyHubCargoLifecycleMissing");

            var start = Anchor(root.transform, "Waypoint_FarmYardPotatoCargo",
                new Vector3(-19f, 1.2f, 13f));
            var end = Anchor(root.transform, "Waypoint_HubInboundDock",
                new Vector3(9f, 1.2f, 5f));
            Marker(root.transform, "Marker_FarmYard", start.position, new Color(.98f, .62f, .10f));
            Marker(root.transform, "Marker_HubDock", end.position, new Color(.08f, .78f, .94f));
            var route = Ribbon(root.transform, start.position, end.position);
            var vanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VanPath)
                            ?? throw new InvalidOperationException("PotatoJourneyHubVanPrefabMissing");
            var van = PrefabUtility.InstantiatePrefab(vanPrefab, root.transform) as GameObject
                      ?? throw new InvalidOperationException("PotatoJourneyHubVanInstantiateFailed");
            van.name = "SyntyVehicleView_PotatoSimulationVan";
            van.transform.localScale = Vector3.one * .72f;
            var follower = van.AddComponent<절차형VehicleRouteFollower>();
            follower.Configure(start, end, 8f, true);
            var ui = BuildUi(root.transform);
            var presenter = root.AddComponent<PotatoJourneyHubRoutePresenter>();
            presenter.Configure(follower, cargoLifecycle, route, ui.Title, ui.State, ui.Boundary);
            ConfigureCamera(start.position, end.position);
            ValidateOpenScene();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("PotatoJourneyHubSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("PotatoJourneyHubRouteBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/PVS/Open Potato Journey Hub Route")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [MenuItem("Ssalddel/PVS/Validate Potato Journey Hub Route")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                       ?? throw new InvalidOperationException("PotatoJourneyHubRootMissing");
            var presenter = root.GetComponent<PotatoJourneyHubRoutePresenter>()
                            ?? throw new InvalidOperationException("PotatoJourneyHubPresenterMissing");
            presenter.ApplyProjection();
            if (!presenter.ValidateWiring() || presenter.CurrentModel == null
                || !presenter.CurrentModel.IsVisible
                || presenter.CurrentModel.ModeLabel != "SIMULATION"
                || presenter.CurrentModel.Quantity != 300m
                || presenter.CurrentModel.PackageCount != 15
                || presenter.CurrentModel.HandoffStateCode != "Loaded"
                || presenter.CurrentModel.SourceModeCode != PotatoJourneySourceModeCodes.SimulationFixture)
                throw new InvalidOperationException("PotatoJourneyHubPresentationInvalid");
            if (root.GetComponentsInChildren<MonoBehaviour>(true) is var behaviours)
            foreach (var behaviour in behaviours)
                if (behaviour != null && behaviour.GetType().Name.Contains("Command", StringComparison.Ordinal))
                    throw new InvalidOperationException("PotatoJourneyHubCommandAuthorityLeak");
        }

        [MenuItem("Ssalddel/PVS/Capture Potato Journey Hub Route Play Mode")]
        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("PotatoJourneyHubCaptureRequiresPlayMode");
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("Potato Journey Hub capture requested:" + absolute);
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, true);
            anchor.position = position;
            return anchor;
        }

        private static GameObject Ribbon(Transform parent, Vector3 start, Vector3 end)
        {
            var ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = "DataRoute_PotatoSimulation_FarmHub";
            ribbon.transform.SetParent(parent, true);
            ribbon.transform.position = (start + end) * .5f + Vector3.up * .08f;
            ribbon.transform.rotation = Quaternion.LookRotation(end - start, Vector3.up);
            ribbon.transform.localScale = new Vector3(1.15f, .12f, Vector3.Distance(start, end));
            UnityEngine.Object.DestroyImmediate(ribbon.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = new Color(.08f, .72f, .94f, .92f) };
            ribbon.GetComponent<Renderer>().sharedMaterial = material;
            return ribbon;
        }

        private static void Marker(Transform parent, string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, true);
            marker.transform.position = position + Vector3.up * 1.4f;
            marker.transform.localScale = new Vector3(2.2f, 1.4f, 2.2f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            marker.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
        }

        private static UiResult BuildUi(Transform parent)
        {
            var canvasObject = new GameObject("PotatoJourneyHubDataCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            canvasObject.AddComponent<GraphicRaycaster>();
            var panel = new GameObject("HubRouteBoundaryPanel");
            panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(.03f, .70f); rect.anchorMax = new Vector2(.39f, .95f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = panel.AddComponent<Image>();
            image.color = new Color(.035f, .055f, .065f, .91f);
            var title = Text(panel.transform, "Title", new Vector2(.06f, .66f), new Vector2(.94f, .91f),
                26, FontStyle.Bold, new Color(.98f, .66f, .16f), "POTATO CARGO · FARM → HUB");
            var state = Text(panel.transform, "State", new Vector2(.06f, .38f), new Vector2(.94f, .66f),
                20, FontStyle.Bold, new Color(.12f, .82f, .95f), "SIMULATION ROUTE · CARGO LOADED");
            var boundary = Text(panel.transform, "Boundary", new Vector2(.06f, .07f), new Vector2(.94f, .38f),
                15, FontStyle.Normal, Color.white, "HARVEST → PACKAGE → CARGO IDENTITY LINKED");
            return new UiResult(title, state, boundary);
        }

        private static Text Text(Transform parent, string name, Vector2 min, Vector2 max,
            int size, FontStyle style, Color color, string value)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var rect = item.AddComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = item.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = style; text.color = color;
            text.alignment = TextAnchor.MiddleLeft; text.text = value;
            return text;
        }

        private static void ConfigureCamera(Vector3 start, Vector3 end)
        {
            var camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null) throw new InvalidOperationException("PotatoJourneyHubCameraMissing");
            var focus = (start + end) * .5f;
            var rig = camera.GetComponent<DioramaTopDownCameraRig>()
                      ?? UnityEngine.Object.FindAnyObjectByType<DioramaTopDownCameraRig>()
                      ?? throw new InvalidOperationException("PotatoJourneyHubCameraRigMissing");
            var cameraFocus = new GameObject("CameraFocus_PotatoHubRoute").transform;
            cameraFocus.SetParent(GameObject.Find("WorldBootstrap/" + RootName).transform, false);
            cameraFocus.position = focus;
            rig.Configure(camera, new[]
            {
                new DioramaCameraFocusBinding
                {
                    AnchorId = "camera-focus:potato-hub-route",
                    LevelCode = DioramaCameraFocusLevelCodes.Object,
                    Anchor = cameraFocus,
                },
            }, "camera-focus:potato-hub-route", false);
            rig.ConfigureComposition(45f, 68f, 38f, 38f, 34f, 31f, 28f, 90f);
            rig.ApplyNowForTests();
            camera.backgroundColor = new Color(.56f, .66f, .65f);
        }

        private readonly struct UiResult
        {
            public UiResult(Text title, Text state, Text boundary)
            { Title = title; State = state; Boundary = boundary; }
            public Text Title { get; }
            public Text State { get; }
            public Text Boundary { get; }
        }
    }
}
