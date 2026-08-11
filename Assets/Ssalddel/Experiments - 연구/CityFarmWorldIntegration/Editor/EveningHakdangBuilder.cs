using System;
using System.IO;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class EveningHakdangBuilder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/저녁학당.unity";
        public const string RootName = "EVENING-1 Evening Hakdang";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-11-evening-hakdang/evening-hakdang-game-view.png";

        [MenuItem("Ssalddel/EVENING-1/Build Evening Hakdang")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName);
            var camera = Camera(root.transform, "EveningCamera");
            var light = Light(root.transform, "MoonAndDawnLight");
            var world = World(root.transform);
            var time = root.AddComponent<월드시간대Presenter>();
            time.Configure(light, camera, world, 21f / 24f, false);

            var presenter = root.AddComponent<EveningHakdangPresenter>();
            var ui = Ui(root.transform, presenter);
            presenter.Configure(time, ui.Time, ui.Title, ui.Teaching, ui.Prompt,
                ui.Status, ui.Effect, ui.Source, ui.Reflection);

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("EveningHakdangSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("EveningHakdangBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/EVENING-1/Validate Evening Hakdang")]
        public static void ValidateOpenScene()
        {
            var presenter = GameObject.Find(RootName)?.GetComponent<EveningHakdangPresenter>()
                ?? throw new InvalidOperationException("EveningHakdangPresenterMissing");
            if (!presenter.ValidateWiring() || presenter.CurrentSnapshot.DataRevision != 1
                || presenter.CurrentSnapshot.DayPhaseCode != "EveningStudy")
                throw new InvalidOperationException("EveningHakdangInitialPresentationInvalid");
        }

        [MenuItem("Ssalddel/EVENING-1/Capture Completed Study Play Mode")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("EveningHakdangCaptureRequiresPlayMode");
            var presenter = GameObject.Find(RootName)?.GetComponent<EveningHakdangPresenter>()
                ?? throw new InvalidOperationException("EveningHakdangPresenterMissing");
            presenter.RunFoolStudyPath();
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("EveningHakdangCaptureRequested:" + absolute);
        }

        private static Camera Camera(Transform parent, string name)
        {
            var value = new GameObject(name).AddComponent<Camera>();
            value.transform.SetParent(parent, false);
            value.transform.position = new Vector3(0f, 5.8f, -9f);
            value.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            value.clearFlags = CameraClearFlags.SolidColor;
            value.fieldOfView = 46f;
            value.gameObject.AddComponent<AudioListener>();
            return value;
        }

        private static Light Light(Transform parent, string name)
        {
            var value = new GameObject(name).AddComponent<Light>();
            value.transform.SetParent(parent, false);
            value.type = LightType.Directional;
            return value;
        }

        private static Transform World(Transform parent)
        {
            var world = new GameObject("QuietFarmReadingRoom").transform;
            world.SetParent(parent, false);
            Primitive(world, "Floor", PrimitiveType.Cube, new Vector3(0f, -.3f, 2f),
                new Vector3(12f, .5f, 12f), new Color(.13f, .2f, .17f));
            Primitive(world, "ReadingDesk", PrimitiveType.Cube, new Vector3(0f, .65f, 1.9f),
                new Vector3(4.5f, .25f, 1.5f), new Color(.32f, .18f, .09f));
            Primitive(world, "FoolCard", PrimitiveType.Cube, new Vector3(0f, 1.7f, 2f),
                new Vector3(1.25f, 2f, .12f), new Color(.9f, .78f, .42f));
            Primitive(world, "FarmWindow", PrimitiveType.Cube, new Vector3(0f, 2.7f, 4.8f),
                new Vector3(8f, 4.5f, .2f), new Color(.08f, .15f, .24f));
            return world;
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

        private static UiRefs Ui(Transform parent, EveningHakdangPresenter presenter)
        {
            var canvasObject = new GameObject("EveningHakdangCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1100f, 620f);

            var card = Panel(canvasObject.transform, "StudyCard", new Vector2(.04f, .06f),
                new Vector2(.61f, .94f), new Color(.025f, .035f, .075f, .96f));
            Panel(card, "GoldLine", new Vector2(0f, .985f), Vector2.one, new Color(.92f, .7f, .24f));
            var time = Text(card, "Time", "EVENING 21:00", new Vector2(.055f, .89f),
                new Vector2(.95f, .97f), 14, new Color(.58f, .74f, 1f), FontStyle.Bold);
            var title = Text(card, "Title", "0. 바보 · 모를 뿐", new Vector2(.055f, .76f),
                new Vector2(.95f, .9f), 28, Color.white, FontStyle.Bold);
            var teaching = Text(card, "Teaching", "", new Vector2(.055f, .62f),
                new Vector2(.95f, .76f), 16, new Color(.94f, .88f, .7f), FontStyle.Normal);
            var prompt = Text(card, "Prompt", "", new Vector2(.055f, .51f),
                new Vector2(.95f, .62f), 19, Color.white, FontStyle.Bold);
            var reflection = Input(card, new Vector2(.055f, .39f), new Vector2(.95f, .51f));
            var status = Text(card, "Status", "", new Vector2(.055f, .31f),
                new Vector2(.95f, .39f), 14, new Color(.5f, .92f, .68f), FontStyle.Bold);
            var effect = Text(card, "Effect", "", new Vector2(.055f, .18f),
                new Vector2(.95f, .31f), 15, new Color(.75f, .85f, 1f), FontStyle.Normal);
            var source = Text(card, "Source", "", new Vector2(.055f, .11f),
                new Vector2(.95f, .18f), 10, new Color(.58f, .62f, .72f), FontStyle.Normal);
            var actions = new[] { ("RESET", EveningHakdangActionCodes.Reset),
                ("PREVIEW", EveningHakdangActionCodes.Preview), ("CONFIRM", EveningHakdangActionCodes.Confirm),
                ("NEXT DAWN", EveningHakdangActionCodes.ApplyTick) };
            for (var i = 0; i < actions.Length; i++)
            {
                var min = .055f + i * .225f;
                Button(card, actions[i].Item1, actions[i].Item2, presenter,
                    new Vector2(min, .025f), new Vector2(min + .2f, .1f));
            }
            return new UiRefs(time, title, teaching, prompt, status, effect, source, reflection);
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max,
            int size, Color color, FontStyle style)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.fontStyle = style; text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static InputField Input(Transform parent, Vector2 min, Vector2 max)
        {
            var panel = Panel(parent, "ReflectionInput", min, max, new Color(.08f, .1f, .17f, 1f));
            var text = Text(panel, "Text", "", new Vector2(.03f, .08f), new Vector2(.97f, .92f),
                14, Color.white, FontStyle.Normal);
            var input = panel.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            return input;
        }

        private static void Button(Transform parent, string label, string code,
            EveningHakdangPresenter presenter, Vector2 min, Vector2 max)
        {
            var value = new GameObject("Action_" + code, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(EveningHakdangActionButton));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            value.GetComponent<Image>().color = new Color(.16f, .2f, .34f, 1f);
            value.GetComponent<EveningHakdangActionButton>().Configure(presenter, code);
            var text = Text(value.transform, "Label", label, new Vector2(.02f, .02f),
                new Vector2(.98f, .98f), 11, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private readonly struct UiRefs
        {
            public UiRefs(Text time, Text title, Text teaching, Text prompt, Text status,
                Text effect, Text source, InputField reflection)
            { Time = time; Title = title; Teaching = teaching; Prompt = prompt; Status = status;
                Effect = effect; Source = source; Reflection = reflection; }
            public Text Time { get; } public Text Title { get; } public Text Teaching { get; }
            public Text Prompt { get; } public Text Status { get; } public Text Effect { get; }
            public Text Source { get; } public InputField Reflection { get; }
        }
    }
}
