using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class 감자생산유통통합Builder
    {
        public const string ScenePath = 연구Scene경로.감자생산유통 + "/감자생산유통통합흐름.unity";
        public const string RootName = "감자생산유통 통합 흐름";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-11-potato-production-distribution-integrated/감자생산유통통합흐름.png";

        private static readonly string[] StageLabels =
        {
            "재배·수확",
            "수확·포장·상차",
            "농장 출발·거점 이동",
            "물류 거점 입고·검수",
            "물류 거점 판로 분배",
            "도시 도착",
        };

        [MenuItem("Ssalddel/감자생산유통/Build 통합 흐름")]
        public static void Build()
        {
            EnsureSourceScenes();
            EditorSceneManager.OpenScene(PotatoHubDispositionLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                        ?? throw new InvalidOperationException("PotatoIntegratedWorldMissing");

            var previous = world.transform.Find(RootName);
            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var cityRoot = CloneCityRoot(world.transform);
            var baseRoot = RequiredRoot(world.transform, PotatoJourneyFarmVerticalSliceBuilder.RootName);
            var cultivation = RequiredRoot(world.transform, PotatoCultivationLifecycleBuilder.RootName);
            var harvestCargo = RequiredRoot(world.transform, PotatoHarvestCargoLifecycleBuilder.RootName);
            var hubRoute = RequiredRoot(world.transform, PotatoJourneyHubRouteBuilder.RootName)
                .GetComponent<PotatoJourneyHubRoutePresenter>()
                ?? throw new InvalidOperationException("PotatoIntegratedHubRoutePresenterMissing");
            var journey = RequiredRoot(world.transform, PotatoCargoJourneyLifecycleBuilder.RootName);
            var receiving = RequiredRoot(world.transform, PotatoHubReceivingLifecycleBuilder.RootName);
            var disposition = RequiredRoot(world.transform, PotatoHubDispositionLifecycleBuilder.RootName);
            var cultivationPresenter = RequiredPresenter<PotatoCultivationLifecyclePresenter>(cultivation);
            var harvestCargoPresenter = RequiredPresenter<PotatoHarvestCargoLifecyclePresenter>(harvestCargo);
            var cargoJourneyPresenter = RequiredPresenter<PotatoCargoJourneyLifecyclePresenter>(journey);
            var hubReceivingPresenter = RequiredPresenter<PotatoHubReceivingLifecyclePresenter>(receiving);
            var hubDispositionPresenter = RequiredPresenter<PotatoHubDispositionLifecyclePresenter>(disposition);
            var cityArrivalPresenter = RequiredPresenter<PotatoJourneyCityPresenter>(cityRoot);

            HideSupportingCanvases(baseRoot, hubRoute.transform);
            var stageCanvases = new[]
            {
                RequiredCanvas(cultivation, "PotatoCultivationLifecycleCanvas"),
                RequiredCanvas(harvestCargo, "PotatoHarvestCargoCanvas"),
                RequiredCanvas(journey, "PotatoCargoJourneyCanvas"),
                RequiredCanvas(receiving, "PotatoHubReceivingCanvas"),
                RequiredCanvas(disposition, "PotatoHubDispositionCanvas"),
                RequiredCanvas(cityRoot, "PotatoJourneyCityDataCanvas"),
            };

            var farmPlot = baseRoot.Find("FarmPlotAnchor_Potato")
                           ?? throw new InvalidOperationException("PotatoIntegratedFarmPlotMissing");
            var farmCargo = baseRoot.Find("FarmYardCargoAnchor_Potato")
                            ?? throw new InvalidOperationException("PotatoIntegratedFarmCargoMissing");
            var routeStart = hubRoute.RouteFollower.RouteStart;
            var routeEnd = hubRoute.RouteFollower.RouteEnd;
            var routeFocus = CreateFocus(world.transform, "통합흐름_이동구간Focus",
                Vector3.Lerp(routeStart.position, routeEnd.position, .5f));
            var receivingFocus = CreateFocus(world.transform, "통합흐름_입고검수Focus", routeEnd.position);
            var dispositionFocus = CreateFocus(world.transform, "통합흐름_판로분배Focus",
                routeEnd.position + new Vector3(2f, 0f, 0f));
            var cityFocus = cityRoot.Find("CityPublicProductAnchor")
                            ?? throw new InvalidOperationException("PotatoIntegratedCityFocusMissing");

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            routeFocus.SetParent(root.transform, true);
            receivingFocus.SetParent(root.transform, true);
            dispositionFocus.SetParent(root.transform, true);
            cityRoot.SetParent(root.transform, true);

            var presenter = root.AddComponent<감자생산유통통합Presenter>();
            var ui = BuildNavigationUi(root.transform, presenter);
            var camera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("PotatoIntegratedCameraMissing");

            presenter.Configure(
                camera,
                stageCanvases,
                new[] { farmPlot, farmCargo, routeFocus, receivingFocus, dispositionFocus, cityFocus },
                StageLabels,
                ui.CurrentStage,
                ui.DemonstrationState,
                ui.Lineage,
                new Vector3(-13f, 16f, -18f));
            presenter.ConfigureDemonstrations(
                cultivationPresenter,
                harvestCargoPresenter,
                cargoJourneyPresenter,
                hubReceivingPresenter,
                hubDispositionPresenter,
                cityArrivalPresenter);

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("PotatoIntegratedSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("PotatoProductionDistributionIntegratedBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/감자생산유통/Open 통합 흐름")]
        public static void Open() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [MenuItem("Ssalddel/감자생산유통/Validate 통합 흐름")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                       ?? throw new InvalidOperationException("PotatoIntegratedRootMissing");
            var presenter = root.GetComponent<감자생산유통통합Presenter>()
                            ?? throw new InvalidOperationException("PotatoIntegratedPresenterMissing");
            var buttons = root.GetComponentsInChildren<감자생산유통단계Button>(true);
            var wiring = presenter.ValidateWiring();
            var demonstrationWiring = presenter.ValidateDemonstrationWiring();
            var activeCanvasCount = presenter.ActiveStageCanvasCount();
            var distinctButtonCount = buttons.Select(value => value.StageIndex).Distinct().Count();
            if (!wiring
                || !demonstrationWiring
                || presenter.StageCount != 6
                || presenter.CurrentStageIndex != 0
                || activeCanvasCount != 1
                || buttons.Length != 6
                || distinctButtonCount != 6)
                throw new InvalidOperationException("PotatoIntegratedPresentationInvalid:"
                    + $"wiring={wiring}:demonstrations={demonstrationWiring}:stages={presenter.StageCount}:current={presenter.CurrentStageIndex}:"
                    + $"activeCanvases={activeCanvasCount}:buttons={buttons.Length}:distinctButtons={distinctButtonCount}");

            foreach (var name in new[]
                     {
                         PotatoCultivationLifecycleBuilder.RootName,
                         PotatoHarvestCargoLifecycleBuilder.RootName,
                         PotatoCargoJourneyLifecycleBuilder.RootName,
                         PotatoHubReceivingLifecycleBuilder.RootName,
                         PotatoHubDispositionLifecycleBuilder.RootName,
                         PotatoJourneyCityBuilder.RootName,
                     })
                if (GameObject.Find("WorldBootstrap/" + name) == null
                    && GameObject.Find("WorldBootstrap/" + RootName + "/" + name) == null)
                    throw new InvalidOperationException("PotatoIntegratedStageRootMissing:" + name);
        }

        [MenuItem("Ssalddel/감자생산유통/Capture 통합 흐름 Play Mode")]
        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("PotatoIntegratedCaptureRequiresPlayMode");
            var presenter = GameObject.Find("WorldBootstrap/" + RootName)
                ?.GetComponent<감자생산유통통합Presenter>()
                ?? throw new InvalidOperationException("PotatoIntegratedPresenterMissing");
            presenter.SelectStage(2);
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("PotatoIntegratedCaptureRequested:" + absolute);
        }

        private static void EnsureSourceScenes()
        {
            if (!File.Exists(PotatoHubDispositionLifecycleBuilder.ScenePath))
                PotatoHubDispositionLifecycleBuilder.Build();
            if (!File.Exists(PotatoJourneyCityBuilder.ScenePath))
                PotatoJourneyCityBuilder.Build();
        }

        private static Transform CloneCityRoot(Transform destinationWorld)
        {
            var existing = destinationWorld.Find(PotatoJourneyCityBuilder.RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var cityScene = EditorSceneManager.OpenScene(PotatoJourneyCityBuilder.ScenePath, OpenSceneMode.Additive);
            var cityWorld = cityScene.GetRootGameObjects().Single(value => value.name == "WorldBootstrap");
            var source = cityWorld.transform.Find(PotatoJourneyCityBuilder.RootName)
                         ?? throw new InvalidOperationException("PotatoIntegratedCitySourceMissing");
            var clone = UnityEngine.Object.Instantiate(source.gameObject, destinationWorld).transform;
            clone.name = PotatoJourneyCityBuilder.RootName;
            EditorSceneManager.CloseScene(cityScene, true);
            return clone;
        }

        private static Transform RequiredRoot(Transform world, string name)
            => world.Find(name) ?? throw new InvalidOperationException("PotatoIntegratedRequiredRootMissing:" + name);

        private static Canvas RequiredCanvas(Transform root, string name)
            => root.Find(name)?.GetComponent<Canvas>()
               ?? throw new InvalidOperationException("PotatoIntegratedCanvasMissing:" + name);

        private static T RequiredPresenter<T>(Transform root) where T : Component
            => root.GetComponent<T>()
               ?? throw new InvalidOperationException("PotatoIntegratedPresenterMissing:" + typeof(T).Name);

        private static void HideSupportingCanvases(params Transform[] roots)
        {
            foreach (var canvas in roots.SelectMany(value => value.GetComponentsInChildren<Canvas>(true)))
                canvas.gameObject.SetActive(false);
        }

        private static Transform CreateFocus(Transform parent, string name, Vector3 position)
        {
            var focus = new GameObject(name).transform;
            focus.SetParent(parent, false);
            focus.position = position;
            return focus;
        }

        private static NavigationUi BuildNavigationUi(Transform parent, 감자생산유통통합Presenter presenter)
        {
            var canvasObject = new GameObject("감자생산유통통합NavigationCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            var bar = Panel(canvasObject.transform, "통합단계Navigation",
                new Vector2(.02f, .80f), new Vector2(.98f, .985f), new Color(.018f, .035f, .04f, .96f));
            var current = Text(bar, "현재단계", "1/6 · 재배·수확",
                new Vector2(.018f, .72f), new Vector2(.24f, .95f), 20,
                new Color(1f, .72f, .22f), FontStyle.Bold);
            var lineage = Text(bar, "계보", "LINEAGE",
                new Vector2(.25f, .72f), new Vector2(.98f, .95f), 12,
                new Color(.7f, .87f, .9f), FontStyle.Normal);
            var demonstration = Text(bar, "단계시연상태", "ACTION · 1단계 시연 준비",
                new Vector2(.018f, .48f), new Vector2(.98f, .70f), 15,
                new Color(.55f, 1f, .64f), FontStyle.Bold);

            for (var index = 0; index < StageLabels.Length; index++)
            {
                var min = .018f + index * .162f;
                StageButton(bar, presenter, index, StageLabels[index],
                    new Vector2(min, .06f), new Vector2(min + .151f, .43f));
            }

            return new NavigationUi(current, demonstration, lineage);
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max,
            int size, Color color, FontStyle style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void StageButton(Transform parent, 감자생산유통통합Presenter presenter,
            int index, string label, Vector2 min, Vector2 max)
        {
            var buttonObject = new GameObject("단계_" + (index + 1), typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(감자생산유통단계Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = index == 0
                ? new Color(.2f, .38f, .16f, 1f)
                : new Color(.08f, .17f, .19f, 1f);
            buttonObject.GetComponent<감자생산유통단계Button>().Configure(presenter, index);
            var text = Text(buttonObject.transform, "Label", $"{index + 1}. {label}",
                new Vector2(.03f, .05f), new Vector2(.97f, .95f), 12, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private readonly struct NavigationUi
        {
            public NavigationUi(Text currentStage, Text demonstrationState, Text lineage)
            {
                CurrentStage = currentStage;
                DemonstrationState = demonstrationState;
                Lineage = lineage;
            }

            public Text CurrentStage { get; }
            public Text DemonstrationState { get; }
            public Text Lineage { get; }
        }
    }
}
