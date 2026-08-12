using System;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    public static class 통합전시관Object모판Builder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/ExhibitionObjects/통합전시관ObjectVisualCatalog.asset";
        public const string PreviewScenePath = "Assets/Ssalddel/Scenes/통합Object모판.unity";
        public const string PreviewRootName = "통합Object모판";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-12-integrated-orderer-market-object-seedbed-obj7a/integrated-orderer-market-object-seedbed-obj7a.png";

        private const string PrefabRoot = "Assets/Ssalddel/Presentation/ExhibitionObjects/Prefabs";
        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs";
        private const string TownRoot = "Assets/Synty/PolygonTown/Prefabs";
        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs";

        [MenuItem("Ssalddel/OBJ-2~7A/통합 Object Catalog와 모판 생성")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("IntegratedExhibitionObjectBuildRequiresEditMode");
            BuildCatalog();
            BuildPreviewScene();
            Debug.Log("IntegratedExhibitionObjectSeedbedBuilt:" + PreviewScenePath);
        }

        [MenuItem("Ssalddel/OBJ-2~7A/통합 Object Catalog 생성")]
        public static void BuildCatalog()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            var entries = Definitions().Select(CreateWrapperEntry).ToArray();
            var catalog = AssetDatabase.LoadAssetAtPath<통합전시관ObjectVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null
                    && !AssetDatabase.DeleteAsset(CatalogPath))
                    throw new InvalidOperationException("IntegratedExhibitionInvalidCatalogDeleteFailed");
                catalog = ScriptableObject.CreateInstance<통합전시관ObjectVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure("integrated-exhibition-object-visual-catalog:obj-7a.r1", entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.Validate();
        }

        [MenuItem("Ssalddel/OBJ-3~7A/독립 Object 모판 생성")]
        public static void BuildPreviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "통합Object모판";
            var catalog = AssetDatabase.LoadAssetAtPath<통합전시관ObjectVisualCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("IntegratedExhibitionObjectCatalogMissing");
            catalog.Validate();

            var root = new GameObject(PreviewRootName);
            var camera = BuildCamera(root.transform);
            var previewHost = BuildPreviewBay(root.transform);
            BuildEventSystem(root.transform);
            var ui = BuildUi(root.transform, camera, catalog);
            var presenter = root.AddComponent<통합전시관ObjectPreviewPresenter>();
            presenter.Configure(catalog, previewHost, ui.Title, ui.Detail, ui.Boundary, ui.Buttons,
                "seedbed-object:city.urban-market-building.a");
            presenter.Initialize();

            ValidateOpenPreviewScene();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
                throw new InvalidOperationException("IntegratedExhibitionObjectPreviewSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
        }

        [MenuItem("Ssalddel/OBJ-3~7A/독립 Object 모판 검증")]
        public static void ValidateOpenPreviewScene()
        {
            var root = GameObject.Find(PreviewRootName)
                ?? throw new InvalidOperationException("IntegratedExhibitionObjectPreviewRootMissing");
            var presenter = root.GetComponent<통합전시관ObjectPreviewPresenter>()
                ?? throw new InvalidOperationException("IntegratedExhibitionObjectPreviewPresenterMissing");
            presenter.ValidateWiring();
            if (presenter.Catalog개수 != Definitions().Length || presenter.운영Command제공여부)
                throw new InvalidOperationException("IntegratedExhibitionObjectPreviewStateInvalid");

            foreach (var stableId in Definitions().Select(value => value.StableId))
            {
                presenter.SelectObject(stableId);
                var objectRoot = presenter.현재ObjectRoot;
                if (objectRoot == null || objectRoot.ObjectStableId != stableId || !objectRoot.ValidateWiring())
                    throw new InvalidOperationException("IntegratedExhibitionObjectPreviewObjectInvalid:" + stableId);
                if (root.GetComponentsInChildren<통합전시관SeedbedObjectRoot>(true).Length != 1)
                    throw new InvalidOperationException("IntegratedExhibitionObjectPreviewMultiplicityInvalid");
            }

            presenter.SelectObject(Definitions()[0].StableId);
            if (root.GetComponentsInChildren<Button>(true).Any(value =>
                    value.name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0
                    || value.name.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("IntegratedExhibitionObjectPreviewCommandButtonForbidden");
        }

        private static 통합전시관ObjectVisualCatalogEntry CreateWrapperEntry(ObjectDefinition definition)
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.SourcePrefabPath)
                ?? throw new InvalidOperationException("IntegratedExhibitionObjectSourcePrefabMissing:"
                    + definition.SourcePrefabPath);
            var root = new GameObject("SeedbedObjectRoot_" + definition.DisplayName.Replace(" ", string.Empty));
            try
            {
                var visualRoot = new GameObject("VisualRoot_PresentationOnly").transform;
                visualRoot.SetParent(root.transform, false);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, visualRoot);
                visual.name = "SourceVisual_" + definition.VisualVariantKey;
                Normalize(visual, definition.TargetSize);
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

                var sockets = definition.Sockets.Select(value =>
                {
                    var socketObject = new GameObject("Socket_" + value.Code);
                    socketObject.transform.SetParent(root.transform, false);
                    socketObject.transform.localPosition = value.Position;
                    socketObject.transform.localRotation = Quaternion.Euler(value.Euler);
                    var socket = socketObject.AddComponent<통합전시관ObjectSocket>();
                    socket.Configure(value.Code);
                    return socket;
                }).ToArray();
                var objectRoot = root.AddComponent<통합전시관SeedbedObjectRoot>();
                objectRoot.Configure(definition.StableId, definition.VisualVariantKey,
                    definition.PlacementProfileKey, definition.Footprint, visualRoot, sockets);
                if (!objectRoot.ValidateWiring())
                    throw new InvalidOperationException("IntegratedExhibitionObjectWrapperInvalid:" + definition.StableId);

                var prefabPath = PrefabRoot + "/" + definition.PrefabFileName;
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath)
                    ?? throw new InvalidOperationException("IntegratedExhibitionObjectPrefabSaveFailed:" + prefabPath);
                var bounds = MeasureBounds(saved);
                var entry = new 통합전시관ObjectVisualCatalogEntry();
                entry.Configure(definition.StableId, definition.DisplayName, definition.VisualVariantKey,
                    definition.PlacementProfileKey, definition.Sockets.Select(value => value.Code).ToArray(),
                    saved, definition.Footprint, bounds.size);
                if (!entry.Validate())
                    throw new InvalidOperationException("IntegratedExhibitionObjectCatalogEntryInvalid:" + definition.StableId);
                return entry;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform BuildPreviewBay(Transform parent)
        {
            var bay = new GameObject("ObjectPreviewBay").transform;
            bay.SetParent(parent, false);
            Primitive(bay, "PreviewGround", PrimitiveType.Cylinder, new Vector3(0f, .12f, 0f),
                new Vector3(6.6f, .12f, 6.6f), new Color(.08f, .18f, .18f));
            Primitive(bay, "PreviewPlinth", PrimitiveType.Cylinder, new Vector3(0f, .35f, 0f),
                new Vector3(3.1f, .35f, 3.1f), new Color(.36f, .26f, .12f));
            var host = new GameObject("SelectedObjectHost").transform;
            host.SetParent(bay, false);
            host.localPosition = new Vector3(0f, .72f, 0f);
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI * .25f;
                Primitive(bay, "ObjectGate_" + index, PrimitiveType.Cylinder,
                    new Vector3(Mathf.Cos(angle) * 4.5f, .3f, Mathf.Sin(angle) * 4.5f),
                    new Vector3(.22f, .3f, .22f), index < 6
                        ? new Color(.23f, .7f, .66f) : new Color(.34f, .39f, .4f));
            }
            return host;
        }

        private static Camera BuildCamera(Transform parent)
        {
            var camera = new GameObject("통합Object모판Camera").AddComponent<Camera>();
            camera.transform.SetParent(parent, false);
            camera.transform.position = new Vector3(10.5f, 7.2f, -13.5f);
            camera.transform.LookAt(new Vector3(0f, 1.7f, 0f));
            camera.fieldOfView = 39f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .055f, .065f);
            camera.gameObject.AddComponent<AudioListener>();
            var sun = new GameObject("Object모판Sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(44f, -35f, 0f);
            sun.color = new Color(1f, .86f, .68f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.28f, .36f, .36f);
            return camera;
        }

        private static UiRefs BuildUi(
            Transform parent, Camera camera, 통합전시관ObjectVisualCatalog catalog)
        {
            var canvasObject = new GameObject("통합Object모판Canvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            var header = Panel(canvasObject.transform, "Header", new Vector2(.025f, .79f),
                new Vector2(.55f, .97f), new Color(.025f, .07f, .075f, .94f));
            var title = Text(header, "Title", "통합 Object 모판 · OBJ-7A 주문자·마트 Object", new Vector2(.04f, .08f),
                new Vector2(.96f, .92f), 27, new Color(.98f, .72f, .25f), FontStyle.Bold);
            var detailPanel = Panel(canvasObject.transform, "ObjectDescriptor", new Vector2(.68f, .29f),
                new Vector2(.975f, .97f), new Color(.025f, .065f, .075f, .96f));
            var detail = Text(detailPanel, "Detail", string.Empty, new Vector2(.06f, .05f),
                new Vector2(.94f, .95f), 16, new Color(.86f, .92f, .84f), FontStyle.Normal);
            detail.resizeTextForBestFit = true;
            detail.resizeTextMinSize = 11;
            detail.resizeTextMaxSize = 16;
            var nav = Panel(canvasObject.transform, "ObjectNavigation", new Vector2(.025f, .04f),
                new Vector2(.56f, .57f), new Color(.025f, .065f, .07f, .96f));
            Text(nav, "NavigationTitle", "Scene에 심을 Object", new Vector2(.05f, .88f),
                new Vector2(.95f, .97f), 18, Color.white, FontStyle.Bold);
            var buttons = new Button[catalog.Entries.Count];
            for (var index = 0; index < buttons.Length; index++)
            {
                const int rowsPerColumn = 8;
                var column = index / rowsPerColumn;
                var row = index % rowsPerColumn;
                var left = .035f + column * .485f;
                var right = left + .45f;
                var top = .86f - row * .105f;
                buttons[index] = Button(nav, "Object_" + index, catalog.Entries[index].DisplayName,
                    new Vector2(left, top - .078f), new Vector2(right, top));
            }
            var boundaryPanel = Panel(canvasObject.transform, "BoundaryPanel", new Vector2(.57f, .04f),
                new Vector2(.975f, .25f), new Color(.09f, .045f, .04f, .96f));
            var boundary = Text(boundaryPanel, "Boundary", string.Empty, new Vector2(.04f, .08f),
                new Vector2(.96f, .92f), 16, new Color(.98f, .72f, .58f), FontStyle.Bold);
            return new UiRefs(title, detail, boundary, buttons);
        }

        private static void BuildEventSystem(Transform parent)
        {
            var value = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            value.transform.SetParent(parent, false);
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

        private static Text Text(
            Transform parent, string name, string value, Vector2 min, Vector2 max,
            int size, Color color, FontStyle style)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = target.GetComponent<Text>();
            text.text = value;
            text.font = KoreanFont();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button Button(
            Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            target.GetComponent<Image>().color = new Color(.09f, .17f, .18f, .96f);
            var labelText = Text(target.transform, "Label", label, new Vector2(.05f, .08f),
                new Vector2(.95f, .92f), 15, Color.white, FontStyle.Bold);
            labelText.alignment = TextAnchor.MiddleLeft;
            return target.GetComponent<Button>();
        }

        private static Font KoreanFont()
        {
            var installed = Font.GetOSInstalledFontNames();
            foreach (var candidate in new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Arial Unicode MS" })
                if (installed.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    return Font.CreateDynamicFontFromOSFont(candidate, 18);
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject Primitive(
            Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            value.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
            var collider = value.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            return value;
        }

        private static void Normalize(GameObject instance, float targetSize)
        {
            var bounds = MeasureBounds(instance);
            var max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (max > .001f) instance.transform.localScale *= targetSize / max;
            bounds = MeasureBounds(instance);
            instance.transform.position += new Vector3(0f, -bounds.min.y, 0f);
        }

        private static Bounds MeasureBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("IntegratedExhibitionObjectRendererMissing:" + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static ObjectDefinition[] Definitions() => new[]
        {
            new ObjectDefinition(
                "seedbed-object:farm.potato-harvest-box.a", "감자 수확 상자",
                "farm.potato-harvest-box.a", "placement-profile:farm.harvest-box.a",
                FarmRoot + "/Plants/SM_Prop_Box_Potato_01.prefab", "감자수확상자_A.prefab", 2.2f,
                new Vector2(2.4f, 2.2f), new[]
                {
                    S("Cargo", Vector3.zero), S("Interaction", new Vector3(0f, 0f, -1.25f)),
                    S("Label", new Vector3(0f, 1.6f, 0f)), S("CameraFocus", new Vector3(0f, 1f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:town.hub-inbound-gate.a", "Hub 입고 Gate",
                "town.hub-inbound-gate.a", "placement-profile:town.hub-inbound-gate.a",
                "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_Garage_01.prefab",
                "Hub입고Gate_A.prefab", 7f, new Vector2(7.5f, 6.5f), new[]
                {
                    S("Entry", new Vector3(0f, 0f, -3.2f)), S("Exit", new Vector3(0f, 0f, 3.2f)),
                    S("Vehicle", new Vector3(-2f, 0f, -2.2f)), S("Cargo", new Vector3(2f, 0f, -1.2f)),
                    S("Interaction", new Vector3(0f, 0f, -3.7f)), S("Label", new Vector3(0f, 3.7f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.8f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:shared.food-pickup-handoff-box.a", "음식 픽업 인계 상자",
                "shared.food-pickup-handoff-box.a", "placement-profile:shared.food-pickup-handoff-box.a",
                "Assets/Synty/PolygonTown/Prefabs/Items/SM_Item_Pizza_Box_01.prefab",
                "음식픽업인계상자_A.prefab", 1.6f, new Vector2(1.8f, 1.6f), new[]
                {
                    S("Cargo", Vector3.zero), S("Actor", new Vector3(-1f, 0f, -1f)),
                    S("Interaction", new Vector3(1f, 0f, -1f)), S("Label", new Vector3(0f, 1.4f, 0f)),
                    S("CameraFocus", new Vector3(0f, .8f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:farm.greenhouse.a", "농장 온실",
                "farm.greenhouse.a", "placement-profile:farm.greenhouse.a",
                FarmRoot + "/Buildings/SM_Bld_Greenhouse_01.prefab", "농장온실_A.prefab", 7f,
                new Vector2(7.5f, 6.5f), new[]
                {
                    S("Entry", new Vector3(0f, 0f, -3.2f)), S("CropBed", Vector3.zero),
                    S("Irrigation", new Vector3(2.2f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -3.7f)), S("Label", new Vector3(0f, 3.8f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.8f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:farm.potato-row.a", "감자 밭고랑",
                "farm.potato-row.a", "placement-profile:farm.potato-row.a",
                FarmRoot + "/Environments/SM_Env_Dirt_Rows_01.prefab", "감자밭고랑_A.prefab", 4.4f,
                new Vector2(4.8f, 4.8f), new[]
                {
                    S("Crop", Vector3.zero), S("SoilObservation", new Vector3(-1.4f, 0f, 0f)),
                    S("Irrigation", new Vector3(1.4f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -2.6f)), S("Label", new Vector3(0f, 1.1f, 0f)),
                    S("CameraFocus", new Vector3(0f, .6f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:farm.potato-plant-visual.a", "감자 재배체",
                "farm.potato-plant-visual.a", "placement-profile:farm.potato-plant-visual.a",
                FarmRoot + "/Plants/SM_Prop_Plant_Potato_01_L.prefab", "감자재배체_A.prefab", 1.8f,
                new Vector2(2f, 2f), new[]
                {
                    S("Soil", Vector3.zero), S("WeatherObservation", new Vector3(-1f, 0f, 0f)),
                    S("Interaction", new Vector3(1f, 0f, -1f)), S("Label", new Vector3(0f, 1.6f, 0f)),
                    S("CameraFocus", new Vector3(0f, .9f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:farm.irrigation-sprinkler.a", "밭 관수 스프링클러",
                "farm.irrigation-sprinkler.a", "placement-profile:farm.irrigation-sprinkler.a",
                FarmRoot + "/Props/SM_Prop_Sprinkler_01.prefab", "밭관수스프링클러_A.prefab", 2f,
                new Vector2(2.2f, 2.2f), new[]
                {
                    S("WaterInput", new Vector3(-1f, 0f, 0f)), S("CropTarget", new Vector3(1f, 0f, 0f)),
                    S("WeatherObservation", Vector3.zero),
                    S("Interaction", new Vector3(0f, 0f, -1.3f)), S("Label", new Vector3(0f, 1.8f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:town.delivery-truck.a", "화물 배송 차량",
                "town.delivery-truck.a", "placement-profile:town.delivery-truck.a",
                TownRoot + "/Vehicles/SM_Veh_Truck_Delivery_01.prefab", "화물배송차량_A.prefab", 4.8f,
                new Vector2(5.2f, 3f), new[]
                {
                    S("Driver", new Vector3(-1.3f, .7f, 0f)), S("Cargo", new Vector3(1.1f, .7f, 0f)),
                    S("RouteEntry", new Vector3(0f, 0f, -2.8f)), S("RouteExit", new Vector3(0f, 0f, 2.8f)),
                    S("Interaction", new Vector3(-2.8f, 0f, 0f)), S("Label", new Vector3(0f, 2.6f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.2f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:shared.cargo-pallet.a", "공용 화물 Pallet",
                "shared.cargo-pallet.a", "placement-profile:shared.cargo-pallet.a",
                CityRoot + "/Props/SM_Prop_Pallet_01.prefab", "공용화물Pallet_A.prefab", 1.8f,
                new Vector2(2.2f, 2.2f), new[]
                {
                    S("Cargo", new Vector3(0f, .4f, 0f)), S("Forklift", new Vector3(-1.3f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -1.3f)), S("Label", new Vector3(0f, 1.5f, 0f)),
                    S("CameraFocus", new Vector3(0f, .8f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:farm.pallet-crate.a", "농장 출하 Pallet Crate",
                "farm.pallet-crate.a", "placement-profile:farm.pallet-crate.a",
                FarmRoot + "/Props/SM_Prop_PalletCrate_01.prefab", "농장출하PalletCrate_A.prefab", 2.3f,
                new Vector2(2.7f, 2.5f), new[]
                {
                    S("HarvestCargo", Vector3.zero), S("Vehicle", new Vector3(-1.5f, 0f, 0f)),
                    S("HubHandoff", new Vector3(1.5f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -1.5f)), S("Label", new Vector3(0f, 1.8f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:town.resident-visual.a", "주민 관점 Visual",
                "town.resident-visual.a", "placement-profile:town.resident-visual.a",
                TownRoot + "/Characters/SM_Chr_Father_01.prefab", "주민관점Visual_A.prefab", 2.2f,
                new Vector2(1.3f, 1.3f), new[]
                {
                    S("Perspective", Vector3.zero), S("AggregateBoundary", new Vector3(1f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -1f)), S("Label", new Vector3(0f, 2.2f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.1f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:town.grouping-cart-table.a", "집단수요 Cart Table",
                "town.grouping-cart-table.a", "placement-profile:town.grouping-cart-table.a",
                TownRoot + "/Props/SM_Prop_Cart_01.prefab", "집단수요CartTable_A.prefab", 3f,
                new Vector2(3.3f, 2.3f), new[]
                {
                    S("IntentInput", new Vector3(-1.3f, .7f, 0f)),
                    S("AggregateOutput", new Vector3(1.3f, .7f, 0f)),
                    S("ConsentBoundary", new Vector3(0f, .7f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -1.5f)), S("Label", new Vector3(0f, 2.2f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.1f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:city.urban-market-building.a", "도심마트 Shop",
                "city.urban-market-building.a", "placement-profile:city.urban-market-building.a",
                CityRoot + "/Buildings/SM_Bld_Shop_01.prefab", "도심마트Shop_A.prefab", 7f,
                new Vector2(7.5f, 6.5f), new[]
                {
                    S("Entry", new Vector3(0f, 0f, -3.3f)),
                    S("PublicProduct", new Vector3(-1.8f, 1f, -2.5f)),
                    S("DemandSignal", new Vector3(1.8f, 1f, -2.5f)),
                    S("Interaction", new Vector3(0f, 0f, -3.8f)), S("Label", new Vector3(0f, 4.2f, 0f)),
                    S("CameraFocus", new Vector3(0f, 2f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:city.operator-inventory-shelf.a", "운영자 전용 재고 Shelf",
                "city.operator-inventory-shelf.a", "placement-profile:city.operator-inventory-shelf.a",
                CityRoot + "/Props/SM_Prop_ShopInterior_Shelf_01.prefab", "운영자전용재고Shelf_A.prefab", 3f,
                new Vector2(3.3f, 1.8f), new[]
                {
                    S("Inventory", new Vector3(0f, 1.1f, 0f)), S("ShelfTask", new Vector3(1.4f, .8f, 0f)),
                    S("Operator", new Vector3(-1.4f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -1.2f)), S("Label", new Vector3(0f, 2.6f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.3f, 0f)),
                }),
            new ObjectDefinition(
                "seedbed-object:city.market-operator-visual.a", "마트 운영자 Visual",
                "city.market-operator-visual.a", "placement-profile:city.market-operator-visual.a",
                CityRoot + "/Characters/Character_BusinessMan_Shirt.prefab", "마트운영자Visual_A.prefab", 2.2f,
                new Vector2(1.3f, 1.3f), new[]
                {
                    S("Perspective", Vector3.zero), S("Inventory", new Vector3(-1f, 0f, 0f)),
                    S("ShelfTask", new Vector3(1f, 0f, 0f)),
                    S("Interaction", new Vector3(0f, 0f, -1f)), S("Label", new Vector3(0f, 2.2f, 0f)),
                    S("CameraFocus", new Vector3(0f, 1.1f, 0f)),
                }),
        };

        private static SocketDefinition S(string code, Vector3 position)
            => new SocketDefinition(code, position, Vector3.zero);

        private sealed class ObjectDefinition
        {
            public ObjectDefinition(string stableId, string displayName, string visualVariantKey,
                string placementProfileKey, string sourcePrefabPath, string prefabFileName,
                float targetSize, Vector2 footprint, SocketDefinition[] sockets)
            {
                StableId = stableId;
                DisplayName = displayName;
                VisualVariantKey = visualVariantKey;
                PlacementProfileKey = placementProfileKey;
                SourcePrefabPath = sourcePrefabPath;
                PrefabFileName = prefabFileName;
                TargetSize = targetSize;
                Footprint = footprint;
                Sockets = sockets;
            }

            public string StableId { get; }
            public string DisplayName { get; }
            public string VisualVariantKey { get; }
            public string PlacementProfileKey { get; }
            public string SourcePrefabPath { get; }
            public string PrefabFileName { get; }
            public float TargetSize { get; }
            public Vector2 Footprint { get; }
            public SocketDefinition[] Sockets { get; }
        }

        private sealed class SocketDefinition
        {
            public SocketDefinition(string code, Vector3 position, Vector3 euler)
            {
                Code = code;
                Position = position;
                Euler = euler;
            }
            public string Code { get; }
            public Vector3 Position { get; }
            public Vector3 Euler { get; }
        }

        private sealed class UiRefs
        {
            public UiRefs(Text title, Text detail, Text boundary, Button[] buttons)
            {
                Title = title;
                Detail = detail;
                Boundary = boundary;
                Buttons = buttons;
            }
            public Text Title { get; }
            public Text Detail { get; }
            public Text Boundary { get; }
            public Button[] Buttons { get; }
        }
    }
}
