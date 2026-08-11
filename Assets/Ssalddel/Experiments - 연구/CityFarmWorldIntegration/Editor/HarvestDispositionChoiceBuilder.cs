using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class HarvestDispositionChoiceBuilder
    {
        public const string ScenePath =
            연구Scene경로.생산자판로 + "/수확물판로선택.unity";
        public const string RootName = "HARVEST-CHOICE-1 Potato Harvest Disposition";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-harvest-disposition-choice/harvest-disposition-choice-game-view.png";

        [MenuItem("Ssalddel/HARVEST-CHOICE-1/Build Harvest Disposition Choice")]
        public static void Build()
        {
            if (!File.Exists(PotatoCultivationLifecycleBuilder.ScenePath))
                PotatoCultivationLifecycleBuilder.Build();
            EditorSceneManager.OpenScene(PotatoCultivationLifecycleBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("HarvestDispositionWorldMissing");
            var cultivationRoot = world.transform.Find(PotatoCultivationLifecycleBuilder.RootName)
                ?? throw new InvalidOperationException("HarvestDispositionCultivationRootMissing");
            var cultivation = cultivationRoot.GetComponent<PotatoCultivationLifecyclePresenter>()
                ?? throw new InvalidOperationException("HarvestDispositionCultivationPresenterMissing");
            var oldCanvas = cultivationRoot.Find("PotatoCultivationLifecycleCanvas");
            if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
            var harvestLot = cultivationRoot.Find("HarvestLotMarker_300kg")
                ?? throw new InvalidOperationException("HarvestDispositionLotMarkerMissing");

            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var presenter = root.AddComponent<HarvestDispositionChoicePresenter>();
            var cooperative = Marker(root.transform, "CooperativeChoiceMarker",
                harvestLot.position + new Vector3(-1.35f, .15f, 0f), new Color(.25f, .84f, .44f));
            var direct = Marker(root.transform, "DirectOnlineChoiceMarker",
                harvestLot.position + new Vector3(0f, .15f, 1.25f), new Color(.2f, .68f, .95f));
            var export = Marker(root.transform, "ExportAgentChoiceMarker",
                harvestLot.position + new Vector3(1.35f, .15f, 0f), new Color(.96f, .64f, .2f));
            var ui = BuildUi(root.transform, presenter);
            presenter.Configure(cultivation, ui.Card.gameObject, cooperative, direct, export,
                ui.Title, ui.Harvest, ui.State, ui.Selection, ui.Detail, ui.Limitation, false);

            var collider = harvestLot.GetComponent<Collider>() ?? harvestLot.gameObject.AddComponent<SphereCollider>();
            if (collider is SphereCollider sphere) sphere.radius = 1.2f;
            var interactable = harvestLot.GetComponent<HarvestDispositionInteractable>()
                ?? harvestLot.gameObject.AddComponent<HarvestDispositionInteractable>();
            interactable.Configure(presenter);

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("HarvestDispositionSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("HarvestDispositionChoiceBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/HARVEST-CHOICE-1/Validate Harvest Disposition Choice")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("HarvestDispositionRootMissing");
            var presenter = root.GetComponent<HarvestDispositionChoicePresenter>()
                ?? throw new InvalidOperationException("HarvestDispositionPresenterMissing");
            presenter.ResetChoice();
            var cultivationRoot = GameObject.Find("WorldBootstrap/" + PotatoCultivationLifecycleBuilder.RootName);
            var lot = cultivationRoot?.transform.Find("HarvestLotMarker_300kg");
            var actions = root.GetComponentsInChildren<HarvestDispositionChoiceActionButton>(true).Length;
            if (!presenter.ValidateWiring() || presenter.CurrentModel.StateText != "AwaitingChoice"
                || presenter.CurrentSnapshot.HarvestLot.Quantity != 300m || presenter.IsCardOpen
                || lot == null || lot.GetComponent<HarvestDispositionInteractable>() == null || actions != 6)
                throw new InvalidOperationException("HarvestDispositionInitialPresentationInvalid:actions=" + actions);
        }

        [MenuItem("Ssalddel/HARVEST-CHOICE-1/Capture Direct Online Choice Play Mode")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("HarvestDispositionCaptureRequiresPlayMode");
            var presenter = GameObject.Find("WorldBootstrap/" + RootName)
                ?.GetComponent<HarvestDispositionChoicePresenter>()
                ?? throw new InvalidOperationException("HarvestDispositionPresenterMissing");
            presenter.RunDirectOnlinePath();
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("HarvestDispositionCaptureRequested:" + absolute);
        }

        private static GameObject Marker(Transform parent, string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(.34f, .09f, .34f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            marker.GetComponent<Renderer>().sharedMaterial = new Material(shader) { name = name, color = color };
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);
            return marker;
        }

        private static Ui BuildUi(Transform parent, HarvestDispositionChoicePresenter presenter)
        {
            var canvasObject = new GameObject("HarvestDispositionChoiceCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100f, 506f);

            var card = Panel(canvasObject.transform, "DispositionChoiceCard",
                new Vector2(.025f, .025f), new Vector2(.74f, .56f), new Color(.024f, .038f, .048f, .97f));
            Panel(card, "Accent", new Vector2(0f, .96f), Vector2.one, new Color(.26f, .84f, .42f));
            var title = Text(card, "Title", "수확한 감자의 판로를 선택하세요",
                new Vector2(.035f, .84f), new Vector2(.965f, .96f), 20, Color.white, FontStyle.Bold);
            var harvest = Text(card, "Harvest", "HARVEST LOT",
                new Vector2(.035f, .71f), new Vector2(.965f, .84f), 12,
                new Color(1f, .82f, .38f), FontStyle.Bold);
            var state = Text(card, "State", "STATE",
                new Vector2(.035f, .62f), new Vector2(.965f, .71f), 11,
                new Color(.48f, .88f, .96f), FontStyle.Bold);
            var selection = Text(card, "Selection", "SELECTION",
                new Vector2(.035f, .51f), new Vector2(.965f, .62f), 13,
                new Color(.62f, .96f, .64f), FontStyle.Bold);
            var detail = Text(card, "Detail", "DETAIL",
                new Vector2(.035f, .37f), new Vector2(.965f, .51f), 12,
                new Color(.88f, .9f, .86f), FontStyle.Normal);
            var limitation = Text(card, "Limitation", "LIMIT",
                new Vector2(.035f, .27f), new Vector2(.965f, .37f), 10,
                new Color(.7f, .73f, .7f), FontStyle.Normal);
            var actions = new[]
            {
                ("RESET", HarvestDispositionChoiceActionCodes.Reset),
                ("조합 출하", HarvestDispositionChoiceActionCodes.Cooperative),
                ("온라인 직판", HarvestDispositionChoiceActionCodes.DirectOnline),
                ("수출대행", HarvestDispositionChoiceActionCodes.ExportAgent),
                ("확인", HarvestDispositionChoiceActionCodes.Confirm),
                ("TICK", HarvestDispositionChoiceActionCodes.ApplyTick),
            };
            for (var index = 0; index < actions.Length; index++)
            {
                var min = .035f + index * .155f;
                Button(card, "Action_" + actions[index].Item2, actions[index].Item1,
                    actions[index].Item2, presenter, new Vector2(min, .055f), new Vector2(min + .14f, .235f));
            }
            정보Panel상호작용Builder.Attach(canvasObject.transform, card, "수확물 판로 선택");
            return new Ui(card, title, harvest, state, selection, detail, limitation);
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
            HarvestDispositionChoicePresenter presenter, Vector2 min, Vector2 max)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(HarvestDispositionChoiceActionButton));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = new Color(.08f, .18f, .2f, 1f);
            value.GetComponent<HarvestDispositionChoiceActionButton>().Configure(presenter, code);
            var text = Text(value.transform, "Label", label, new Vector2(.03f, .04f),
                new Vector2(.97f, .96f), 11, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private readonly struct Ui
        {
            public Ui(RectTransform card, Text title, Text harvest, Text state,
                Text selection, Text detail, Text limitation)
            {
                Card = card; Title = title; Harvest = harvest; State = state;
                Selection = selection; Detail = detail; Limitation = limitation;
            }
            public RectTransform Card { get; }
            public Text Title { get; }
            public Text Harvest { get; }
            public Text State { get; }
            public Text Selection { get; }
            public Text Detail { get; }
            public Text Limitation { get; }
        }
    }
}
