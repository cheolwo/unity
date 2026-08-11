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
    public static class PotatoJourneyCityBuilder
    {
        public const string ScenePath =
            연구Scene경로.감자생산유통 + "/감자도시도착단계구현.unity";
        public const string RootName = "PVS7 Potato Journey City Public Product";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-potato-journey-city/potato-journey-city-game-view.png";
        private const string ShopPath =
            "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_Shop_03.prefab";
        private const string BoxPath =
            "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_CardboardBox_01.prefab";
        private static double automatedCaptureStartedAt;
        private static bool automatedCaptureRequested;

        [MenuItem("Ssalddel/PVS/Build Potato Journey City")]
        public static void Build()
        {
            if (!File.Exists(PotatoJourneyHubRouteBuilder.ScenePath))
                PotatoJourneyHubRouteBuilder.Build();
            EditorSceneManager.OpenScene(PotatoJourneyHubRouteBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                        ?? throw new InvalidOperationException("PotatoJourneyCityWorldRootMissing");
            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var oldCard = GameObject.Find(
                "WorldBootstrap/PVS5 Potato Journey Farm Slice/PotatoJourneyDataCanvas/DataCardPanel");
            if (oldCard != null) oldCard.SetActive(false);

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var city = new GameObject("CityPublicProductAnchor");
            city.transform.SetParent(root.transform, false);
            city.transform.position = new Vector3(20f, 0f, 4f);
            Foundation(city.transform);
            Prefab(city.transform, ShopPath, "SyntyFacilityView_UrbanMarket",
                new Vector3(0f, .1f, 2.5f), Quaternion.Euler(0f, 225f, 0f), .72f);
            for (var index = 0; index < 5; index++)
                Prefab(city.transform, BoxPath, "SyntyProductView_PotatoBox_" + (index + 1),
                    new Vector3(-2.2f + index * 1.05f, .45f, -2f),
                    Quaternion.Euler(0f, index * 23f, 0f), 1.15f);
            var anchor = Marker(city.transform);
            var ui = BuildUi(root.transform);
            var presenter = root.AddComponent<PotatoJourneyCityPresenter>();
            presenter.Configure(anchor, ui.Title, ui.Observed, ui.Sale, ui.Availability, ui.Boundary);
            ConfigureCamera(root.transform, new Vector3(2f, 0f, 6f));
            ValidateOpenScene();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("PotatoJourneyCitySceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("PotatoJourneyCityBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/PVS/Open Potato Journey City")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [MenuItem("Ssalddel/PVS/Validate Potato Journey City")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                       ?? throw new InvalidOperationException("PotatoJourneyCityRootMissing");
            var presenter = root.GetComponent<PotatoJourneyCityPresenter>()
                            ?? throw new InvalidOperationException("PotatoJourneyCityPresenterMissing");
            presenter.ApplyProjection();
            var model = presenter.CurrentModel;
            if (!presenter.ValidateWiring() || model == null || !model.IsVisible
                || model.ModeLabel != "SIMULATION"
                || model.QuantityMeaningCode != PotatoJourneyCityQuantityMeaningCodes.ProjectedSaleAvailability
                || !model.ObservedPriceText.Contains("KRW/kg", StringComparison.Ordinal)
                || !model.SalePriceText.Contains("20kg box", StringComparison.Ordinal))
                throw new InvalidOperationException("PotatoJourneyCityPresentationInvalid");
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Name.Contains("Command", StringComparison.Ordinal))
                    throw new InvalidOperationException("PotatoJourneyCityCommandAuthorityLeak");
        }

        [MenuItem("Ssalddel/PVS/Capture Potato Journey City Play Mode")]
        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("PotatoJourneyCityCaptureRequiresPlayMode");
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("Potato Journey City capture requested:" + absolute);
        }

        public static void CaptureAutomatedPlayMode()
        {
            Open();
            automatedCaptureStartedAt = EditorApplication.timeSinceStartup;
            automatedCaptureRequested = false;
            EditorApplication.update -= TickAutomatedCapture;
            EditorApplication.update += TickAutomatedCapture;
            EditorApplication.isPlaying = true;
        }

        private static void TickAutomatedCapture()
        {
            if (!EditorApplication.isPlaying) return;
            var elapsed = EditorApplication.timeSinceStartup - automatedCaptureStartedAt;
            var absolute = Path.GetFullPath(EvidencePath);
            if (!automatedCaptureRequested && elapsed >= 4d)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                if (File.Exists(absolute)) File.Delete(absolute);
                ScreenCapture.CaptureScreenshot(absolute, 1);
                automatedCaptureRequested = true;
                automatedCaptureStartedAt = EditorApplication.timeSinceStartup;
                return;
            }

            if (automatedCaptureRequested && elapsed >= 3d
                && File.Exists(absolute) && new FileInfo(absolute).Length > 1000)
            {
                EditorApplication.update -= TickAutomatedCapture;
                EditorApplication.isPlaying = false;
                Debug.Log("PotatoJourneyCityAutomatedCaptureComplete:" + absolute);
                EditorApplication.Exit(0);
            }
        }

        private static void Foundation(Transform parent)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = "CityMarketFoundation";
            value.transform.SetParent(parent, false);
            value.transform.localPosition = new Vector3(0f, -.25f, 0f);
            value.transform.localScale = new Vector3(15f, .5f, 12f);
            UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            value.GetComponent<Renderer>().sharedMaterial = new Material(shader)
                { color = new Color(.25f, .31f, .34f) };
        }

        private static void Prefab(Transform parent, string path, string name,
            Vector3 position, Quaternion rotation, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                        ?? throw new InvalidOperationException("PotatoJourneyCityPrefabMissing:" + path);
            var instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject
                           ?? throw new InvalidOperationException("PotatoJourneyCityPrefabInstantiateFailed");
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;
            instance.transform.localScale = Vector3.one * scale;
        }

        private static GameObject Marker(Transform parent)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "DataAnchor_CityPublicPotatoProduct";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, .15f, -3f);
            marker.transform.localScale = new Vector3(3.2f, .12f, 3.2f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            marker.GetComponent<Renderer>().sharedMaterial = new Material(shader)
                { color = new Color(.10f, .83f, .92f) };
            return marker;
        }

        private static UiResult BuildUi(Transform parent)
        {
            var canvasObject = new GameObject("PotatoJourneyCityDataCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            canvasObject.AddComponent<GraphicRaycaster>();
            var panel = new GameObject("CityPublicProductPanel");
            panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(.61f, .57f); rect.anchorMax = new Vector2(.97f, .95f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(.025f, .045f, .065f, .93f);
            var title = Text(panel.transform, "Title", .79f, .94f, 25, FontStyle.Bold,
                new Color(.96f, .68f, .18f), "POTATO · CITY PUBLIC PRODUCT");
            var observed = Text(panel.transform, "ObservedPrice", .59f, .79f, 18, FontStyle.Bold,
                new Color(.28f, .82f, .97f), "KAMIS OBSERVATION");
            var sale = Text(panel.transform, "SalePrice", .40f, .59f, 20, FontStyle.Bold,
                new Color(.98f, .76f, .25f), "STORE SALE PRICE");
            var availability = Text(panel.transform, "Availability", .22f, .40f, 17, FontStyle.Normal,
                Color.white, "PROJECTED SALE AVAILABILITY");
            var boundary = Text(panel.transform, "Boundary", .05f, .22f, 14, FontStyle.Normal,
                new Color(.78f, .84f, .88f), "SIMULATION");
            정보Panel상호작용Builder.Attach(canvasObject.transform, rect, "도시 도착 정보");
            return new UiResult(title, observed, sale, availability, boundary);
        }

        private static Text Text(Transform parent, string name, float minY, float maxY,
            int size, FontStyle style, Color color, string value)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var rect = item.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(.06f, minY); rect.anchorMax = new Vector2(.94f, maxY);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = item.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.fontStyle = style; text.color = color;
            text.alignment = TextAnchor.MiddleLeft; text.text = value;
            return text;
        }

        private static void ConfigureCamera(Transform parent, Vector3 focusPosition)
        {
            var camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            var rig = camera?.GetComponent<DioramaTopDownCameraRig>()
                      ?? UnityEngine.Object.FindAnyObjectByType<DioramaTopDownCameraRig>()
                      ?? throw new InvalidOperationException("PotatoJourneyCityCameraRigMissing");
            var focus = new GameObject("CameraFocus_PotatoCity").transform;
            focus.SetParent(parent, false);
            focus.position = focusPosition;
            rig.Configure(camera!, new[]
            {
                new DioramaCameraFocusBinding
                {
                    AnchorId = "camera-focus:potato-city",
                    LevelCode = DioramaCameraFocusLevelCodes.Zone,
                    Anchor = focus,
                },
            }, "camera-focus:potato-city", false);
            rig.ConfigureComposition(45f, 70f, 48f, 36f, 35f, 33f, 30f, 95f);
            rig.ApplyNowForTests();
            camera!.backgroundColor = new Color(.49f, .60f, .63f);
        }

        private readonly struct UiResult
        {
            public UiResult(Text title, Text observed, Text sale, Text availability, Text boundary)
            { Title = title; Observed = observed; Sale = sale; Availability = availability; Boundary = boundary; }
            public Text Title { get; }
            public Text Observed { get; }
            public Text Sale { get; }
            public Text Availability { get; }
            public Text Boundary { get; }
        }
    }
}
