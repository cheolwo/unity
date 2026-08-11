using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class PotatoHubDispositionLifecycleBuilder
    {
        public const string ScenePath =
            연구Scene경로.감자생산유통 + "/감자물류거점판로분배흐름.unity";
        public const string RootName = "HUB-2 WORLD-8 Potato Hub Disposition";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-potato-hub-disposition-lifecycle/potato-hub-disposition-game-view.png";

        [MenuItem("Ssalddel/HUB-2 WORLD-8/Build Potato Hub Disposition Lifecycle")]
        public static void Build()
        {
            if (!File.Exists(PotatoHubReceivingLifecycleBuilder.ScenePath))
                PotatoHubReceivingLifecycleBuilder.Build();
            EditorSceneManager.OpenScene(PotatoHubReceivingLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("HubDispositionWorldMissing");
            var old = world.transform.Find(RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var receiving = world.transform.Find(PotatoHubReceivingLifecycleBuilder.RootName)
                ?.GetComponent<PotatoHubReceivingLifecyclePresenter>()
                ?? throw new InvalidOperationException("HubDispositionReceivingMissing");
            var oldCanvas = receiving.transform.Find("PotatoHubReceivingCanvas");
            if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
            var hub = world.transform.Find(PotatoJourneyHubRouteBuilder.RootName)
                ?.GetComponent<PotatoJourneyHubRoutePresenter>()
                ?? throw new InvalidOperationException("HubDispositionRouteMissing");
            var hubPosition = hub.RouteFollower.RouteEnd.position;

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var accepted = Marker(root.transform, "HubAcceptedLotMarker_288kg",
                hubPosition + new Vector3(1.2f, 2.6f, 0f), new Color(.38f, .95f, .48f));
            var rejected = Marker(root.transform, "HubRejectedLossLotMarker_12kg",
                hubPosition + new Vector3(-1.2f, 2.6f, 0f), new Color(.95f, .38f, .24f));
            var outbound = Marker(root.transform, "CityOutboundCandidateMarker_288kg",
                hubPosition + new Vector3(2.5f, 2.8f, 0f), new Color(1f, .73f, .16f));
            var route = CandidateRoute(root.transform, hubPosition + new Vector3(0f, .35f, 0f),
                new Vector3(35f, .35f, 0f));
            var presenter = root.AddComponent<PotatoHubDispositionLifecyclePresenter>();
            var ui = BuildUi(root.transform, presenter);
            presenter.Configure(receiving, accepted, rejected, outbound, route,
                ui.State, ui.Lots, ui.Candidate, ui.Lineage, ui.Action, ui.Limit, true);
            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("HubDispositionSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("PotatoHubDispositionLifecycleBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/HUB-2 WORLD-8/Validate Potato Hub Disposition Lifecycle")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("HubDispositionRootMissing");
            var presenter = root.GetComponent<PotatoHubDispositionLifecyclePresenter>()
                ?? throw new InvalidOperationException("HubDispositionPresenterMissing");
            presenter.ResetLifecycle();
            var actionCount = root.GetComponentsInChildren<PotatoHubDispositionActionButton>(true).Length;
            if (!presenter.ValidateWiring()
                || presenter.CurrentModel.StateCode != "AcceptedAtHub" || actionCount != 7)
                throw new InvalidOperationException("HubDispositionInitialInvalid:actions=" + actionCount);
        }

        [MenuItem("Ssalddel/HUB-2 WORLD-8/Capture Potato Hub Disposition Play Mode")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("HubDispositionCaptureRequiresPlayMode");
            var presenter = GameObject.Find("WorldBootstrap/" + RootName)
                ?.GetComponent<PotatoHubDispositionLifecyclePresenter>()
                ?? throw new InvalidOperationException("HubDispositionPresenterMissing");
            presenter.RunGoldenPath();
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("PotatoHubDispositionCaptureRequested:" + absolute);
        }

        private static GameObject Marker(Transform parent, string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(.42f, .1f, .42f);
            marker.GetComponent<Renderer>().sharedMaterial = Material(name, color);
            marker.SetActive(false);
            return marker;
        }

        private static LineRenderer CandidateRoute(Transform parent, Vector3 start, Vector3 end)
        {
            var route = new GameObject("CandidateRoute_HubCity").AddComponent<LineRenderer>();
            route.transform.SetParent(parent, false);
            route.sharedMaterial = Material("CandidateRoute", new Color(1f, .67f, .08f));
            route.startColor = route.endColor = new Color(1f, .67f, .08f);
            route.startWidth = route.endWidth = .32f;
            route.positionCount = 3;
            route.SetPositions(new[] { start, Vector3.Lerp(start, end, .5f) + Vector3.up, end });
            route.gameObject.SetActive(false);
            return route;
        }

        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }

        private static Ui BuildUi(Transform parent, PotatoHubDispositionLifecyclePresenter presenter)
        {
            var canvasObject = new GameObject("PotatoHubDispositionCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100, 506);
            var panel = Panel(canvasObject.transform, "DispositionPanel", new Vector2(.035f, .025f),
                new Vector2(.69f, .51f), new Color(.025f, .038f, .048f, .96f));
            Panel(panel, "Accent", new Vector2(0f, .955f), Vector2.one, new Color(1f, .62f, .08f));
            Text(panel, "Eye", "HUB-2 · WORLD-8  LOT SPLIT AND CITY OUTBOUND CANDIDATE",
                new Vector2(.035f, .86f), new Vector2(.965f, .955f), 16,
                new Color(1f, .74f, .25f), FontStyle.Bold);
            var state = Text(panel, "State", "STATE", new Vector2(.035f, .71f),
                new Vector2(.965f, .86f), 19, Color.white, FontStyle.Bold);
            var lots = Text(panel, "Lots", "LOTS", new Vector2(.035f, .52f),
                new Vector2(.965f, .71f), 12, new Color(.52f, .94f, .58f), FontStyle.Bold);
            var candidate = Text(panel, "Candidate", "OUTBOUND", new Vector2(.035f, .41f),
                new Vector2(.965f, .52f), 13, new Color(1f, .77f, .3f), FontStyle.Bold);
            var lineage = Text(panel, "Lineage", "LINEAGE", new Vector2(.035f, .29f),
                new Vector2(.965f, .41f), 10, new Color(.72f, .86f, .95f), FontStyle.Bold);
            var action = Text(panel, "Action", "ACTION", new Vector2(.035f, .21f),
                new Vector2(.965f, .29f), 12, new Color(.42f, .85f, .94f), FontStyle.Bold);
            var limit = Text(panel, "Limit", "LIMIT", new Vector2(.035f, .14f),
                new Vector2(.965f, .21f), 10, new Color(.75f, .76f, .72f), FontStyle.Normal);
            var actions = new[]
            {
                ("RESET", PotatoHubDispositionActionCodes.Reset),
                ("SPLIT", PotatoHubDispositionActionCodes.ReviewSeparation),
                ("CONFIRM", PotatoHubDispositionActionCodes.Confirm),
                ("APPLY TICK", PotatoHubDispositionActionCodes.ApplyTick),
                ("OUTBOUND", PotatoHubDispositionActionCodes.ReviewOutbound),
                ("FINISH", PotatoHubDispositionActionCodes.Finish),
                ("GOLDEN", PotatoHubDispositionActionCodes.GoldenPath),
            };
            for (var index = 0; index < actions.Length; index++)
            {
                var min = .035f + index * .133f;
                Button(panel, "Action_" + actions[index].Item2, actions[index].Item1,
                    actions[index].Item2, presenter, new Vector2(min, .035f), new Vector2(min + .12f, .13f));
            }
            정보Panel상호작용Builder.Attach(canvasObject.transform, panel, "물류 거점 판로 분배");
            return new Ui(state, lots, candidate, lineage, action, limit);
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
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = gameObject.GetComponent<Text>();
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

        private static void Button(Transform parent, string name, string label, string code,
            PotatoHubDispositionLifecyclePresenter presenter, Vector2 min, Vector2 max)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(PotatoHubDispositionActionButton));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = new Color(.22f, .13f, .04f, 1f);
            value.GetComponent<PotatoHubDispositionActionButton>().Configure(presenter, code);
            var text = Text(value.transform, "Label", label, new Vector2(.03f, .05f),
                new Vector2(.97f, .95f), 10, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private readonly struct Ui
        {
            public Ui(Text state, Text lots, Text candidate, Text lineage, Text action, Text limit)
            {
                State = state;
                Lots = lots;
                Candidate = candidate;
                Lineage = lineage;
                Action = action;
                Limit = limit;
            }

            public Text State { get; }
            public Text Lots { get; }
            public Text Candidate { get; }
            public Text Lineage { get; }
            public Text Action { get; }
            public Text Limit { get; }
        }
    }
}
