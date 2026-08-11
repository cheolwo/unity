using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Farm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class PotatoCultivationLifecycleBuilder
    {
        public const string ScenePath =
            연구Scene경로.감자생산유통 + "/감자재배수확흐름.unity";
        public const string RootName = "FARM-3 Potato Cultivation Lifecycle";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-potato-cultivation-lifecycle/potato-cultivation-lifecycle-game-view.png";

        [MenuItem("Ssalddel/FARM-3/Build Potato Cultivation Lifecycle")]
        public static void Build()
        {
            if (!File.Exists(PotatoJourneyFarmVerticalSliceBuilder.ScenePath))
                throw new InvalidOperationException("PotatoCultivationBaseSceneMissing");

            EditorSceneManager.OpenScene(
                PotatoJourneyFarmVerticalSliceBuilder.ScenePath,
                OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("PotatoCultivationWorldRootMissing");
            var baseRoot = world.transform.Find(PotatoJourneyFarmVerticalSliceBuilder.RootName)
                ?? throw new InvalidOperationException("PotatoCultivationBaseRootMissing");
            var field = baseRoot.Find("FarmPlotAnchor_Potato")
                ?? throw new InvalidOperationException("PotatoCultivationFieldMissing");
            var cargo = baseRoot.Find("FarmYardCargoAnchor_Potato")
                ?? throw new InvalidOperationException("PotatoCultivationCargoMissing");

            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var presenter = root.AddComponent<PotatoCultivationLifecyclePresenter>();
            var marker = BuildHarvestLotMarker(root.transform, cargo.position);
            var ui = BuildUi(root.transform, presenter);
            var plants = field.GetComponentsInChildren<Transform>(true)
                .Where(value => value.name == "SyntyPotatoVisual")
                .ToArray();
            var baseTexts = baseRoot.GetComponentsInChildren<Text>(true);
            var readOnlyDataTitle = baseTexts.Single(value => value.name == "SelectionTitle");
            var readOnlyDataMode = baseTexts.Single(value => value.name == "ModeBadge");
            presenter.Configure(
                plants,
                cargo.gameObject,
                marker,
                ui.DateAndStage,
                ui.Calendar,
                ui.Lineage,
                ui.ActionState,
                ui.Limitation,
                readOnlyDataTitle,
                readOnlyDataMode,
                true);
            ValidateOpenScene();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("PotatoCultivationSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("PotatoCultivationLifecycleBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/FARM-3/Open Potato Cultivation Lifecycle")]
        public static void Open()
            => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [MenuItem("Ssalddel/FARM-3/Validate Potato Cultivation Lifecycle")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("PotatoCultivationRootMissing");
            var presenter = root.GetComponent<PotatoCultivationLifecyclePresenter>()
                ?? throw new InvalidOperationException("PotatoCultivationPresenterMissing");
            if (!presenter.ValidateWiring())
                throw new InvalidOperationException("PotatoCultivationPresenterWiringInvalid");
            presenter.ResetLifecycle();
            var actionCount = root.GetComponentsInChildren<PotatoCultivationLifecycleActionButton>(true).Length;
            if (presenter.CurrentModel.CanonicalProductStableId != "product:potato"
                || presenter.CurrentModel.SourceModeCode != "Simulation/Fixture"
                || presenter.CurrentModel.GrowthStageCode != "NotStarted"
                || actionCount != 8)
            {
                throw new InvalidOperationException("PotatoCultivationInitialPresentationInvalid:"
                    + presenter.CurrentModel.CanonicalProductStableId + ":"
                    + presenter.CurrentModel.SourceModeCode + ":"
                    + presenter.CurrentModel.GrowthStageCode + ":actions=" + actionCount);
            }
        }

        [MenuItem("Ssalddel/FARM-3/Capture Potato Cultivation Lifecycle Play Mode")]
        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("PotatoCultivationCaptureRequiresPlayMode");
            var presenter = GameObject.Find("WorldBootstrap/" + RootName)
                ?.GetComponent<PotatoCultivationLifecyclePresenter>()
                ?? throw new InvalidOperationException("PotatoCultivationPresenterMissing");
            presenter.RunGoldenPathToHarvest();
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("PotatoCultivationGameViewCaptureRequested:" + absolute);
        }

        private static GameObject BuildHarvestLotMarker(Transform parent, Vector3 cargoPosition)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "HarvestLotMarker_300kg";
            marker.transform.SetParent(parent, false);
            marker.transform.position = cargoPosition + new Vector3(1.3f, 2.4f, .5f);
            marker.transform.localScale = new Vector3(.42f, .08f, .42f);
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/PotatoJourneyMaterials/PotatoSimulationAccent.mat")
                ?? throw new InvalidOperationException("PotatoCultivationMarkerMaterialMissing");
            marker.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);
            return marker;
        }

        private static UiBuildResult BuildUi(
            Transform parent,
            PotatoCultivationLifecyclePresenter presenter)
        {
            var canvasObject = new GameObject("PotatoCultivationLifecycleCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 45;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = Panel(canvasObject.transform, "LifecyclePanel",
                new Vector2(.035f, .035f), new Vector2(.635f, .49f),
                new Color(.025f, .043f, .052f, .95f));
            Panel(panel, "LifecycleAccent", new Vector2(0f, .955f), Vector2.one,
                new Color(.34f, .82f, .34f, 1f));
            Text(panel, "LifecycleEyebrow", "FARM-3 · CANONICAL POTATO LIFECYCLE",
                new Vector2(.035f, .87f), new Vector2(.965f, .955f), 18,
                new Color(.55f, .94f, .52f), FontStyle.Bold);
            var dateAndStage = Text(panel, "DateAndStage", "GAME DATE",
                new Vector2(.035f, .7f), new Vector2(.48f, .87f), 24,
                Color.white, FontStyle.Bold);
            var calendar = Text(panel, "Calendar", "CALENDAR",
                new Vector2(.5f, .7f), new Vector2(.965f, .87f), 14,
                new Color(.75f, .84f, .82f), FontStyle.Normal);
            var lineage = Text(panel, "Lineage", "LINEAGE",
                new Vector2(.035f, .5f), new Vector2(.965f, .69f), 16,
                new Color(1f, .84f, .4f), FontStyle.Bold);
            var action = Text(panel, "ActionState", "ACTION",
                new Vector2(.035f, .36f), new Vector2(.965f, .5f), 16,
                new Color(.42f, .85f, .94f), FontStyle.Bold);
            var limitation = Text(panel, "Limitation", "LIMIT",
                new Vector2(.035f, .25f), new Vector2(.965f, .36f), 12,
                new Color(.7f, .75f, .72f), FontStyle.Normal);

            var actions = new[]
            {
                ("RESET", PotatoCultivationLifecycleActionCodes.Reset),
                ("SOW REVIEW", PotatoCultivationLifecycleActionCodes.ReviewSowing),
                ("CONFIRM", PotatoCultivationLifecycleActionCodes.Confirm),
                ("APPLY TICK", PotatoCultivationLifecycleActionCodes.ApplyTick),
                ("+1 DAY", PotatoCultivationLifecycleActionCodes.AdvanceDay),
                ("TO READY", PotatoCultivationLifecycleActionCodes.AdvanceToReady),
                ("HARVEST REVIEW", PotatoCultivationLifecycleActionCodes.ReviewHarvest),
                ("FINISH HARVEST", PotatoCultivationLifecycleActionCodes.FinishHarvest),
            };
            for (var index = 0; index < actions.Length; index++)
            {
                var column = index % 4;
                var row = index / 4;
                var minX = .035f + column * .237f;
                var maxX = minX + .22f;
                var maxY = .235f - row * .11f;
                var minY = maxY - .09f;
                Button(panel, "Action_" + actions[index].Item2,
                    actions[index].Item1, actions[index].Item2, presenter,
                    new Vector2(minX, minY), new Vector2(maxX, maxY));
            }

            정보Panel상호작용Builder.Attach(canvasObject.transform, panel, "감자 재배·수확");
            return new UiBuildResult(dateAndStage, calendar, lineage, action, limitation);
        }

        private static RectTransform Panel(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text Text(
            Transform parent, string name, string value,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, FontStyle style)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Button(
            Transform parent,
            string name,
            string label,
            string actionCode,
            PotatoCultivationLifecyclePresenter presenter,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(PotatoCultivationLifecycleActionButton));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            gameObject.GetComponent<Image>().color = new Color(.09f, .19f, .2f, 1f);
            var button = gameObject.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(.2f, .55f, .38f, 1f);
            colors.pressedColor = new Color(.12f, .72f, .4f, 1f);
            button.colors = colors;
            gameObject.GetComponent<PotatoCultivationLifecycleActionButton>()
                .Configure(presenter, actionCode);
            var text = Text(gameObject.transform, "Label", label,
                new Vector2(.04f, .05f), new Vector2(.96f, .95f), 12,
                Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private readonly struct UiBuildResult
        {
            public UiBuildResult(Text dateAndStage, Text calendar, Text lineage,
                Text actionState, Text limitation)
            {
                DateAndStage = dateAndStage;
                Calendar = calendar;
                Lineage = lineage;
                ActionState = actionState;
                Limitation = limitation;
            }

            public Text DateAndStage { get; }
            public Text Calendar { get; }
            public Text Lineage { get; }
            public Text ActionState { get; }
            public Text Limitation { get; }
        }
    }
}
