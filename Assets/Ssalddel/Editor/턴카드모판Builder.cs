using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    public static class 턴카드모판Builder
    {
        public const string ScenePath = "Assets/Ssalddel/Scenes/턴카드모판.unity";
        public const string RootName = "턴카드모판";
        public const string EvidencePath =
            "Assets/Documentation/Changes/2026-08-11-turn-card-seedbed-ui-1/turn-card-seedbed.png";

        [MenuItem("Ssalddel/TURN-CARD-SEEDBED-UI-1/Build Turn Card Seedbed")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("TurnCardSeedbedBuildRequiresEditMode");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName);
            var camera = BuildCamera(root.transform);
            BuildWorld(root.transform);
            BuildEventSystem(root.transform);

            var presenter = root.AddComponent<턴카드모판Presenter>();
            var ui = BuildUi(root.transform, presenter, camera);
            presenter.Configure(ui.Title, ui.Summary, ui.Stage, ui.Detail, ui.Boundary,
                ui.Footer, ui.Philosophy, ui.Culture, ui.Candidates);
            presenter.Initialize();

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("TurnCardSeedbedSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("TurnCardSeedbedBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/TURN-CARD-SEEDBED-UI-1/Validate Turn Card Seedbed")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find(RootName)
                ?? throw new InvalidOperationException("TurnCardSeedbedRootMissing");
            var presenter = root.GetComponent<턴카드모판Presenter>()
                ?? throw new InvalidOperationException("TurnCardSeedbedPresenterMissing");
            presenter.ValidateWiring();
            if (presenter.현재모판Code != 턴카드모판Code.철학학당
                || presenter.현재후보수 != 2 || presenter.턴확정제공여부)
                throw new InvalidOperationException("TurnCardSeedbedInitialStateInvalid");
            if (root.GetComponentsInChildren<Button>(true).Any(value =>
                value.name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0
                || value.name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("TurnCardSeedbedGameActionForbidden");
            if (root.GetComponentInChildren<턴마감Presenter>(true) != null)
                throw new InvalidOperationException("TurnCardSeedbedTurnAuthorityForbidden");
        }

        [MenuItem("Ssalddel/TURN-CARD-SEEDBED-UI-1/Show Philosophy Academy")]
        public static void ShowPhilosophyAcademy()
        {
            FindPresenter().Execute(턴카드모판ActionCode.철학학당보기);
        }

        [MenuItem("Ssalddel/TURN-CARD-SEEDBED-UI-1/Show Regional Culture")]
        public static void ShowRegionalCulture()
        {
            FindPresenter().Execute(턴카드모판ActionCode.지역문화보기);
        }

        private static 턴카드모판Presenter FindPresenter()
        {
            return GameObject.Find(RootName)?.GetComponent<턴카드모판Presenter>()
                ?? throw new InvalidOperationException("TurnCardSeedbedPresenterMissing");
        }

        private static Camera BuildCamera(Transform parent)
        {
            var camera = new GameObject("SeedbedCamera").AddComponent<Camera>();
            camera.transform.SetParent(parent, false);
            camera.transform.position = new Vector3(0f, 5.4f, -10f);
            camera.transform.rotation = Quaternion.Euler(17f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .09f, .085f);
            camera.fieldOfView = 48f;
            camera.gameObject.AddComponent<AudioListener>();

            var light = new GameObject("SeedbedLight").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            light.color = new Color(1f, .84f, .62f);
            light.intensity = 1.15f;
            return camera;
        }

        private static void BuildWorld(Transform parent)
        {
            var world = new GameObject("VisualRoot_ResearchOnly").transform;
            world.SetParent(parent, false);
            Primitive(world, "NurseryFloor", PrimitiveType.Cube, new Vector3(0f, -.4f, 2.6f),
                new Vector3(16f, .5f, 13f), new Color(.1f, .17f, .13f));
            for (var i = 0; i < 5; i++)
            {
                Primitive(world, "SeedbedRow_" + (i + 1), PrimitiveType.Cube,
                    new Vector3(-5f + i * 2.5f, .1f, 3.5f), new Vector3(1.7f, .25f, 6.5f),
                    new Color(.25f, .16f, .09f));
            }
            Primitive(world, "PhilosophyMarker", PrimitiveType.Cube,
                new Vector3(-2.4f, 1.5f, 3f), new Vector3(1.5f, 2.4f, .16f),
                new Color(.72f, .44f, .16f));
            Primitive(world, "CultureMarker", PrimitiveType.Cube,
                new Vector3(2.4f, 1.5f, 3f), new Vector3(1.5f, 2.4f, .16f),
                new Color(.2f, .56f, .4f));
        }

        private static void Primitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Color color)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.position = position;
            value.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            value.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
        }

        private static void BuildEventSystem(Transform parent)
        {
            var value = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            value.transform.SetParent(parent, false);
        }

        private static UiRefs BuildUi(
            Transform parent, 턴카드모판Presenter presenter, Camera camera)
        {
            var canvasObject = new GameObject("턴카드모판Canvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 90;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            Panel(canvasObject.transform, "Backdrop", Vector2.zero, Vector2.one,
                new Color(.025f, .045f, .045f, .92f));
            var title = Text(canvasObject.transform, "Title", "턴 카드 모판", new Vector2(.035f, .9f),
                new Vector2(.965f, .975f), 34, new Color(.96f, .78f, .38f), FontStyle.Bold);
            var summary = Text(canvasObject.transform, "Summary", "", new Vector2(.035f, .82f),
                new Vector2(.965f, .9f), 16, new Color(.78f, .86f, .8f), FontStyle.Normal);

            var nav = Panel(canvasObject.transform, "NurseryNavigation", new Vector2(.035f, .14f),
                new Vector2(.22f, .8f), new Color(.055f, .085f, .08f, .97f));
            Text(nav, "NavigationTitle", "분야별 모판", new Vector2(.08f, .88f),
                new Vector2(.92f, .98f), 20, Color.white, FontStyle.Bold);
            var philosophy = Button(nav, "Nursery_PhilosophyAcademy", "철학·학당 모판\n후보 2장",
                presenter, 턴카드모판ActionCode.철학학당보기,
                new Vector2(.08f, .68f), new Vector2(.92f, .84f));
            var culture = Button(nav, "Nursery_RegionalCulture", "지역문화 모판\n후보 1장",
                presenter, 턴카드모판ActionCode.지역문화보기,
                new Vector2(.08f, .49f), new Vector2(.92f, .65f));
            Text(nav, "NavigationBoundary",
                "모판은 연구 공간입니다.\n게시 승인이나 게임 효과를\n자동으로 만들지 않습니다.",
                new Vector2(.08f, .1f), new Vector2(.92f, .4f), 14,
                new Color(.64f, .75f, .68f), FontStyle.Normal);

            var candidates = Panel(canvasObject.transform, "CandidatePanel", new Vector2(.235f, .14f),
                new Vector2(.54f, .8f), new Color(.045f, .065f, .07f, .97f));
            Text(candidates, "CandidateTitle", "모판 후보", new Vector2(.05f, .9f),
                new Vector2(.95f, .98f), 20, Color.white, FontStyle.Bold);
            var candidateButtons = new Button[3];
            for (var i = 0; i < candidateButtons.Length; i++)
            {
                var top = .86f - i * .19f;
                candidateButtons[i] = Button(candidates, "Candidate_" + i, "후보",
                    presenter, string.Empty, new Vector2(.05f, top - .16f), new Vector2(.95f, top));
            }
            var stage = Text(candidates, "GateStatus", "", new Vector2(.05f, .03f),
                new Vector2(.95f, .31f), 12, new Color(.82f, .86f, .84f), FontStyle.Normal);
            stage.resizeTextForBestFit = true;
            stage.resizeTextMinSize = 9;
            stage.resizeTextMaxSize = 12;

            var detailPanel = Panel(canvasObject.transform, "DetailPanel", new Vector2(.555f, .14f),
                new Vector2(.965f, .8f), new Color(.045f, .055f, .075f, .98f));
            var detail = Text(detailPanel, "CardDetail", "", new Vector2(.045f, .52f),
                new Vector2(.955f, .96f), 15, new Color(.94f, .9f, .76f), FontStyle.Normal);
            var boundary = Text(detailPanel, "CardBoundary", "", new Vector2(.045f, .08f),
                new Vector2(.955f, .5f), 14, new Color(.78f, .86f, .94f), FontStyle.Normal);
            boundary.resizeTextForBestFit = true;
            boundary.resizeTextMinSize = 11;
            boundary.resizeTextMaxSize = 14;

            var footer = Text(canvasObject.transform, "ResearchOnlyFooter", "", new Vector2(.035f, .045f),
                new Vector2(.965f, .115f), 16, new Color(.98f, .58f, .34f), FontStyle.Bold);
            footer.alignment = TextAnchor.MiddleCenter;
            return new UiRefs(title, summary, stage, detail, boundary, footer,
                philosophy, culture, candidateButtons);
        }

        private static RectTransform Panel(Transform parent, string name,
            Vector2 min, Vector2 max, Color color)
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

        private static Text Text(Transform parent, string name, string value,
            Vector2 min, Vector2 max, int size, Color color, FontStyle style)
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

        private static Button Button(Transform parent, string name, string label,
            턴카드모판Presenter presenter, string actionCode, Vector2 min, Vector2 max)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = new Color(.14f, .2f, .2f, 1f);
            var text = Text(value.transform, "Label", label, new Vector2(.04f, .04f),
                new Vector2(.96f, .96f), 14, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            return value.GetComponent<Button>();
        }

        private readonly struct UiRefs
        {
            public UiRefs(Text title, Text summary, Text stage, Text detail, Text boundary,
                Text footer, Button philosophy, Button culture, Button[] candidates)
            {
                Title = title;
                Summary = summary;
                Stage = stage;
                Detail = detail;
                Boundary = boundary;
                Footer = footer;
                Philosophy = philosophy;
                Culture = culture;
                Candidates = candidates;
            }

            public Text Title { get; }
            public Text Summary { get; }
            public Text Stage { get; }
            public Text Detail { get; }
            public Text Boundary { get; }
            public Text Footer { get; }
            public Button Philosophy { get; }
            public Button Culture { get; }
            public Button[] Candidates { get; }
        }
    }
}
