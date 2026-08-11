using System;
using System.IO;
using System.Linq;
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
    public static class PotatoJourneyFarmVerticalSliceBuilder
    {
        public const string ScenePath =
            연구Scene경로.감자생산유통 + "/감자농장출발단계구현.unity";
        public const string RootName = "PVS5 Potato Journey Farm Slice";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-10-potato-journey-farm-slice/potato-journey-farm-game-view.png";

        private const string FarmHeroScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장대표풍경전시.unity";
        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string MaterialRoot =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/PotatoJourneyMaterials/";

        [MenuItem("Ssalddel/PVS/Build Potato Journey Farm Slice")]
        public static void Build()
        {
            if (!File.Exists(FarmHeroScenePath)) FarmHeroShowcaseBuilder.Build();
            EditorSceneManager.OpenScene(FarmHeroScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("PotatoJourneyWorldRootMissing");
            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            var accent = Material("PotatoSelectionAccent", new Color(.98f, .63f, .12f), true);
            var simulation = Material("PotatoSimulationAccent", new Color(.14f, .69f, .82f), true);
            var field = BuildPotatoField(root.transform, accent);
            var cargo = BuildPotatoCargo(root.transform, simulation);
            var ui = BuildUi(root.transform);
            var presenter = root.AddComponent<PotatoJourneyFarmSlicePresenter>();
            presenter.Configure(
                field.Anchor, cargo.Anchor, field.Ring, cargo.Ring,
                ui.SelectionTitle, ui.ModeBadge, ui.PriceValue, ui.PriceEvidence,
                ui.LinkageSummary, ui.SourceLineage, ui.HelpText);
            field.Selectable.Configure(presenter, PotatoJourneyAnchorKindCodes.FarmPlot);
            cargo.Selectable.Configure(presenter, PotatoJourneyAnchorKindCodes.FarmYardCargo);
            ConfigureCamera(root.transform);
            ValidateOpenScene();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("PotatoJourneySceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("PotatoJourneyFarmVerticalSliceBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/PVS/Open Potato Journey Farm Slice")]
        public static void Open()
            => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [MenuItem("Ssalddel/PVS/Validate Potato Journey Farm Slice")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("PotatoJourneySliceRootMissing");
            var presenter = root.GetComponent<PotatoJourneyFarmSlicePresenter>()
                ?? throw new InvalidOperationException("PotatoJourneyPresenterMissing");
            if (!presenter.ValidateWiring())
                throw new InvalidOperationException("PotatoJourneyPresenterWiringInvalid");
            presenter.ApplyFarmSelection();
            var selectables = root.GetComponentsInChildren<PotatoJourneySelectableAnchor>(true);
            if (selectables.Length != 2)
                throw new InvalidOperationException("PotatoJourneySelectableCountInvalid");
            var potatoVisuals = root.GetComponentsInChildren<Transform>(true)
                .Count(value => value.name == "SyntyPotatoVisual");
            if (potatoVisuals < 29)
                throw new InvalidOperationException("PotatoJourneySyntyVisualCountInvalid");
            if (presenter.CurrentModel == null || presenter.CurrentModel.CardDeck.Cards.Length != 3
                || presenter.CurrentModel.ModeLabel != "SIMULATION"
                || presenter.CurrentAnchorKind != PotatoJourneyAnchorKindCodes.FarmPlot)
                throw new InvalidOperationException("PotatoJourneyInitialPresentationInvalid");
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Where(value => value != null).Any(value =>
                    value.GetType().Name.Contains("Command", StringComparison.Ordinal)))
                throw new InvalidOperationException("PotatoJourneyCommandAuthorityLeak");
        }

        [MenuItem("Ssalddel/PVS/Capture Potato Journey Farm Play Mode")]
        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("PotatoJourneyCaptureRequiresPlayMode");
            var absolute = Path.GetFullPath(EvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            ScreenCapture.CaptureScreenshot(absolute, 1);
            Debug.Log("Potato Journey Game View capture requested:" + absolute);
        }

        private static AnchorBuildResult BuildPotatoField(Transform parent, Material accent)
        {
            var root = new GameObject("FarmPlotAnchor_Potato").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(-36f, .04f, 17.5f);
            Vendor(root, "Environments/SM_Env_Dirt_Rows_01.prefab",
                "SyntyDirtRows", Vector3.zero, 0f, 1.05f, false);
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 6; column++)
            {
                var path = (row + column) % 3 == 0
                    ? "Plants/SM_Prop_Plant_Potato_01_L.prefab"
                    : (row + column) % 3 == 1
                        ? "Plants/SM_Prop_Plant_Potato_01_M.prefab"
                        : "Plants/SM_Prop_Plant_Potato_01_S.prefab";
                Vendor(root, path, "SyntyPotatoVisual",
                    new Vector3(-5f + column * 2f, .14f, -3f + row * 2f),
                    (row * 17f + column * 23f) % 360f, .86f, true);
            }

            var interaction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            interaction.name = "InteractionSurface_FarmPlot";
            interaction.transform.SetParent(root, false);
            interaction.transform.localPosition = new Vector3(0f, .35f, 0f);
            interaction.transform.localScale = new Vector3(13f, .65f, 9f);
            interaction.GetComponent<Renderer>().enabled = false;
            var selectable = interaction.AddComponent<PotatoJourneySelectableAnchor>();
            var ring = BorderRing(root, "SelectionRing_FarmPlot", new Vector3(0f, .22f, 0f),
                new Vector2(13.2f, 9.2f), accent);
            return new AnchorBuildResult(root, ring, selectable);
        }

        private static AnchorBuildResult BuildPotatoCargo(Transform parent, Material accent)
        {
            var root = new GameObject("FarmYardCargoAnchor_Potato").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(-23.5f, .04f, 14.5f);
            for (var index = 0; index < 5; index++)
            {
                Vendor(root, "Plants/SM_Prop_Box_Potato_01.prefab", "SyntyPotatoVisual",
                    new Vector3(index % 3 * 1.35f, index / 3 * .7f, index / 3 * 1.25f),
                    index % 2 == 0 ? -18f : 9f, .92f, true);
            }

            Vendor(root, "Props/SM_Prop_Sign_Potatoes_01.prefab", "PotatoSign",
                new Vector3(-1.2f, 0f, 2.1f), -24f, .78f, false);
            var interaction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            interaction.name = "InteractionSurface_FarmYardCargo";
            interaction.transform.SetParent(root, false);
            interaction.transform.localPosition = new Vector3(1.3f, .65f, .55f);
            interaction.transform.localScale = new Vector3(5.6f, 1.4f, 4.4f);
            interaction.GetComponent<Renderer>().enabled = false;
            var selectable = interaction.AddComponent<PotatoJourneySelectableAnchor>();
            var ring = BorderRing(root, "SelectionRing_FarmYardCargo", new Vector3(1.3f, .12f, .55f),
                new Vector2(5.8f, 4.6f), accent);
            ring.SetActive(false);
            return new AnchorBuildResult(root, ring, selectable);
        }

        private static UiBuildResult BuildUi(Transform parent)
        {
            var canvasObject = new GameObject("PotatoJourneyDataCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = Panel(canvasObject.transform, "DataCardPanel",
                new Vector2(.665f, .075f), new Vector2(.965f, .925f),
                new Color(.035f, .055f, .07f, .94f));
            Panel(panel, "AccentBar", new Vector2(0f, .94f), new Vector2(1f, 1f),
                new Color(.94f, .55f, .12f, 1f));
            Text(panel, "Eyebrow", "SSALDDEL · SERVER DATA VERTICAL SLICE",
                new Vector2(.07f, .88f), new Vector2(.93f, .94f), 18,
                new Color(.96f, .7f, .32f), FontStyle.Bold);
            var title = Text(panel, "SelectionTitle", "POTATO FIELD · CULTIVATION VIEW",
                new Vector2(.07f, .77f), new Vector2(.93f, .88f), 27,
                Color.white, FontStyle.Bold);
            var badge = Text(panel, "ModeBadge", "SIMULATION · READ ONLY",
                new Vector2(.07f, .71f), new Vector2(.93f, .77f), 16,
                new Color(.26f, .82f, .9f), FontStyle.Bold);
            Text(panel, "PriceLabel", "DOMESTIC MARKET OBSERVATION",
                new Vector2(.07f, .63f), new Vector2(.93f, .7f), 16,
                new Color(.66f, .72f, .74f), FontStyle.Bold);
            var price = Text(panel, "PriceValue", "WHOLESALE AVG ₩2,450/kg",
                new Vector2(.07f, .52f), new Vector2(.93f, .64f), 31,
                new Color(1f, .84f, .4f), FontStyle.Bold);
            var evidence = Text(panel, "PriceEvidence", "RANGE ₩2,200–₩2,700/kg · SAMPLE 8",
                new Vector2(.07f, .45f), new Vector2(.93f, .53f), 15,
                new Color(.83f, .86f, .83f), FontStyle.Normal);
            Panel(panel, "Divider", new Vector2(.07f, .425f), new Vector2(.93f, .43f),
                new Color(.25f, .32f, .33f, 1f));
            var linkage = Text(panel, "LinkageSummary", "SimulationLinked",
                new Vector2(.07f, .27f), new Vector2(.93f, .415f), 19,
                new Color(.9f, .93f, .89f), FontStyle.Bold);
            var source = Text(panel, "SourceLineage", "SOURCE",
                new Vector2(.07f, .14f), new Vector2(.93f, .27f), 14,
                new Color(.56f, .69f, .7f), FontStyle.Normal);
            var help = Text(panel, "HelpText", "CLICK FIELD / BOX",
                new Vector2(.07f, .03f), new Vector2(.93f, .13f), 13,
                new Color(.7f, .74f, .71f), FontStyle.Normal);

            var worldTitle = Panel(canvasObject.transform, "WorldTitlePanel",
                new Vector2(.035f, .78f), new Vector2(.39f, .925f),
                new Color(.035f, .055f, .07f, .82f));
            Text(worldTitle, "WorldTitle", "POTATO JOURNEY · FARM",
                new Vector2(.06f, .46f), new Vector2(.94f, .88f), 31,
                Color.white, FontStyle.Bold);
            Text(worldTitle, "WorldSubtitle", "FIELD → YARD BOX → VERIFIED DATA CARD",
                new Vector2(.06f, .12f), new Vector2(.94f, .46f), 15,
                new Color(.98f, .66f, .25f), FontStyle.Bold);
            정보Panel상호작용Builder.Attach(canvasObject.transform, panel, "농장 감자 정보");
            return new UiBuildResult(title, badge, price, evidence, linkage, source, help);
        }

        private static void ConfigureCamera(Transform parent)
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new InvalidOperationException("PotatoJourneyCameraRigMissing");
            var camera = rig.GetComponent<Camera>();
            var focus = new GameObject("CameraFocus_PotatoJourney").transform;
            focus.SetParent(parent, false);
            focus.position = new Vector3(-31f, 0f, 17f);
            rig.Configure(camera, new[]
            {
                new DioramaCameraFocusBinding
                {
                    AnchorId = "camera-focus:object.potato-journey",
                    LevelCode = DioramaCameraFocusLevelCodes.Object,
                    Anchor = focus,
                },
            }, "camera-focus:object.potato-journey", false);
            rig.ConfigureComposition(49f, 72f, 42f, 31f, 35f, 31f, 28f, 90f);
            rig.ApplyNowForTests();
            camera.backgroundColor = new Color(.56f, .66f, .65f);
        }

        private static GameObject Vendor(
            Transform parent, string relativePath, string name,
            Vector3 localPosition, float yaw, float scale, bool potatoVisual)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmRoot + relativePath)
                ?? throw new InvalidOperationException("PotatoJourneyPrefabMissing:" + relativePath);
            var wrapper = new GameObject(name + "_Wrapper");
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = localPosition;
            wrapper.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(prefab, visualRoot) as GameObject
                ?? throw new InvalidOperationException("PotatoJourneyPrefabInstantiateFailed:" + relativePath);
            instance.name = potatoVisual ? "SyntyPotatoVisual" : "SyntyEnvironmentVisual";
            instance.transform.localScale = Vector3.one * scale;
            return wrapper;
        }

        private static GameObject BorderRing(
            Transform parent, string name, Vector3 localPosition, Vector2 size, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            Border(root.transform, new Vector3(0f, 0f, size.y * .5f), new Vector3(size.x, .08f, .13f), material);
            Border(root.transform, new Vector3(0f, 0f, -size.y * .5f), new Vector3(size.x, .08f, .13f), material);
            Border(root.transform, new Vector3(size.x * .5f, 0f, 0f), new Vector3(.13f, .08f, size.y), material);
            Border(root.transform, new Vector3(-size.x * .5f, 0f, 0f), new Vector3(.13f, .08f, size.y), material);
            return root;
        }

        private static void Border(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = "SelectionAccent";
            border.transform.SetParent(parent, false);
            border.transform.localPosition = position;
            border.transform.localScale = scale;
            border.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(border.GetComponent<Collider>());
        }

        private static Material Material(string name, Color color, bool emission)
        {
            Directory.CreateDirectory(MaterialRoot);
            var path = MaterialRoot + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException("PotatoJourneyUrpShaderMissing");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.4f);
            }
            EditorUtility.SetDirty(material);
            return material;
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

        private readonly struct AnchorBuildResult
        {
            public AnchorBuildResult(Transform anchor, GameObject ring, PotatoJourneySelectableAnchor selectable)
            {
                Anchor = anchor;
                Ring = ring;
                Selectable = selectable;
            }

            public Transform Anchor { get; }
            public GameObject Ring { get; }
            public PotatoJourneySelectableAnchor Selectable { get; }
        }

        private readonly struct UiBuildResult
        {
            public UiBuildResult(Text selectionTitle, Text modeBadge, Text priceValue,
                Text priceEvidence, Text linkageSummary, Text sourceLineage, Text helpText)
            {
                SelectionTitle = selectionTitle;
                ModeBadge = modeBadge;
                PriceValue = priceValue;
                PriceEvidence = priceEvidence;
                LinkageSummary = linkageSummary;
                SourceLineage = sourceLineage;
                HelpText = helpText;
            }

            public Text SelectionTitle { get; }
            public Text ModeBadge { get; }
            public Text PriceValue { get; }
            public Text PriceEvidence { get; }
            public Text LinkageSummary { get; }
            public Text SourceLineage { get; }
            public Text HelpText { get; }
        }
    }
}
