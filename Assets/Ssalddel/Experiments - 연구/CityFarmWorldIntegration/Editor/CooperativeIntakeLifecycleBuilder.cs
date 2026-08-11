using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class CooperativeIntakeLifecycleBuilder
    {
        public const string ScenePath =
            연구Scene경로.생산자판로 + "/생산자조합인수흐름.unity";
        public const string RootName = "COOP-1 Producer Cooperative Intake";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-cooperative-intake-lifecycle/cooperative-intake-game-view.png";

        [MenuItem("Ssalddel/COOP-1/Build Cooperative Intake Lifecycle")]
        public static void Build()
        {
            if (!File.Exists(HarvestDispositionChoiceBuilder.ScenePath))
                HarvestDispositionChoiceBuilder.Build();
            EditorSceneManager.OpenScene(HarvestDispositionChoiceBuilder.ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("CooperativeIntakeWorldMissing");
            var dispositionRoot = world.transform.Find(HarvestDispositionChoiceBuilder.RootName)
                ?? throw new InvalidOperationException("CooperativeIntakeDispositionRootMissing");
            var disposition = dispositionRoot.GetComponent<HarvestDispositionChoicePresenter>()
                ?? throw new InvalidOperationException("CooperativeIntakeDispositionMissing");
            var oldCanvas = dispositionRoot.Find("HarvestDispositionChoiceCanvas");
            if (oldCanvas != null) oldCanvas.gameObject.SetActive(false);
            var cultivationRoot = world.transform.Find(PotatoCultivationLifecycleBuilder.RootName)
                ?? throw new InvalidOperationException("CooperativeIntakeCultivationRootMissing");
            var harvest = cultivationRoot.Find("HarvestLotMarker_300kg")
                ?? throw new InvalidOperationException("CooperativeIntakeHarvestMarkerMissing");

            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var intake = Marker(root.transform, "CooperativeIntakeLotMarker_300kg",
                harvest.position + new Vector3(-1.25f, .3f, -.5f), new Color(.27f, .9f, .43f));
            var candidate = Marker(root.transform, "CargoPreparationCandidateMarker",
                harvest.position + new Vector3(1.25f, .3f, -.5f), new Color(.18f, .78f, .94f));
            var presenter = root.AddComponent<CooperativeIntakeLifecyclePresenter>();
            var ui = BuildUi(root.transform, presenter);
            presenter.Configure(disposition, intake, candidate, ui.State, ui.Intake,
                ui.Candidate, ui.Lineage, ui.Action, ui.Limitation);
            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("CooperativeIntakeSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("CooperativeIntakeLifecycleBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/COOP-1/Validate Cooperative Intake Lifecycle")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("CooperativeIntakeRootMissing");
            var presenter = root.GetComponent<CooperativeIntakeLifecyclePresenter>()
                ?? throw new InvalidOperationException("CooperativeIntakePresenterMissing");
            presenter.ResetLifecycle();
            var actions = root.GetComponentsInChildren<CooperativeIntakeActionButton>(true).Length;
            if (!presenter.ValidateWiring() || presenter.CurrentSnapshot.StateCode != "AwaitingReview"
                || presenter.CurrentSnapshot.HarvestLot.Quantity != 300m || actions != 5)
                throw new InvalidOperationException("CooperativeIntakeInitialPresentationInvalid:actions=" + actions);
        }

        [MenuItem("Ssalddel/COOP-1/Capture Cooperative Intake Play Mode")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("CooperativeIntakeCaptureRequiresPlayMode");
            var presenter = GameObject.Find("WorldBootstrap/" + RootName)
                ?.GetComponent<CooperativeIntakeLifecyclePresenter>()
                ?? throw new InvalidOperationException("CooperativeIntakePresenterMissing");
            presenter.RunGoldenPath();
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("CooperativeIntakeCaptureRequested:" + absolute);
        }

        private static GameObject Marker(Transform parent, string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(.4f, .11f, .4f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            marker.GetComponent<Renderer>().sharedMaterial = new Material(shader) { name = name, color = color };
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);
            return marker;
        }

        private static Ui BuildUi(Transform parent, CooperativeIntakeLifecyclePresenter presenter)
        {
            var canvasObject = new GameObject("CooperativeIntakeCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100f, 506f);
            var panel = Panel(canvasObject.transform, "CooperativeIntakePanel",
                new Vector2(.025f, .025f), new Vector2(.73f, .55f), new Color(.022f, .04f, .046f, .97f));
            Panel(panel, "Accent", new Vector2(0f, .96f), Vector2.one, new Color(.28f, .88f, .43f));
            Text(panel, "Title", "COOP-1 · 생산자 조합 출하 인수",
                new Vector2(.035f, .84f), new Vector2(.965f, .96f), 19, Color.white, FontStyle.Bold);
            var state = Text(panel, "State", "STATE", new Vector2(.035f, .72f),
                new Vector2(.965f, .84f), 13, new Color(.48f, .9f, .96f), FontStyle.Bold);
            var intake = Text(panel, "Intake", "INTAKE", new Vector2(.035f, .6f),
                new Vector2(.965f, .72f), 13, new Color(.58f, .96f, .62f), FontStyle.Bold);
            var candidate = Text(panel, "Candidate", "NEXT", new Vector2(.035f, .49f),
                new Vector2(.965f, .6f), 12, new Color(1f, .8f, .34f), FontStyle.Bold);
            var lineage = Text(panel, "Lineage", "LINEAGE", new Vector2(.035f, .37f),
                new Vector2(.965f, .49f), 10, new Color(.73f, .86f, .94f), FontStyle.Bold);
            var action = Text(panel, "Action", "ACTION", new Vector2(.035f, .28f),
                new Vector2(.965f, .37f), 11, Color.white, FontStyle.Bold);
            var limitation = Text(panel, "Limitation", "LIMIT", new Vector2(.035f, .2f),
                new Vector2(.965f, .28f), 10, new Color(.72f, .75f, .7f), FontStyle.Normal);
            var actions = new[]
            {
                ("RESET", CooperativeIntakeActionCodes.Reset),
                ("인수 검토", CooperativeIntakeActionCodes.Review),
                ("확인", CooperativeIntakeActionCodes.Confirm),
                ("TICK", CooperativeIntakeActionCodes.ApplyTick),
                ("CARGO-1 열기", CooperativeIntakeActionCodes.ConnectCargo),
            };
            for (var index = 0; index < actions.Length; index++)
            {
                var min = .035f + index * .185f;
                Button(panel, "Action_" + actions[index].Item2, actions[index].Item1,
                    actions[index].Item2, presenter, new Vector2(min, .045f), new Vector2(min + .17f, .175f));
            }
            정보Panel상호작용Builder.Attach(canvasObject.transform, panel, "생산자 조합 인수");
            return new Ui(state, intake, candidate, lineage, action, limitation);
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min; rect.anchorMax = max;
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
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.fontStyle = style; text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Button(Transform parent, string name, string label, string code,
            CooperativeIntakeLifecyclePresenter presenter, Vector2 min, Vector2 max)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(CooperativeIntakeActionButton));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = new Color(.07f, .19f, .15f, 1f);
            value.GetComponent<CooperativeIntakeActionButton>().Configure(presenter, code);
            var text = Text(value.transform, "Label", label, new Vector2(.03f, .05f),
                new Vector2(.97f, .95f), 10, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private readonly struct Ui
        {
            public Ui(Text state, Text intake, Text candidate, Text lineage, Text action, Text limitation)
            { State = state; Intake = intake; Candidate = candidate; Lineage = lineage; Action = action; Limitation = limitation; }
            public Text State { get; }
            public Text Intake { get; }
            public Text Candidate { get; }
            public Text Lineage { get; }
            public Text Action { get; }
            public Text Limitation { get; }
        }
    }
}
