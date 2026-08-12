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
    public static class 통합전시관Builder
    {
        public const string ScenePath = "Assets/Ssalddel/Scenes/통합모판전시관.unity";
        public const string RootName = "통합모판전시관";
        public const string EvidencePath =
            "Documentation/Changes/2026-08-11-integrated-seedbed-exhibition-exh5/integrated-seedbed-exhibition-exh5.png";

        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs";
        private const string CityHallPath = "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_CityHall_01.prefab";
        private const string TownHousePath = "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_01.prefab";
        private const string GreenhousePath = FarmRoot + "/Buildings/SM_Bld_Greenhouse_01.prefab";
        private const string DirtRowsPath = FarmRoot + "/Environments/SM_Env_Dirt_Rows_01.prefab";
        private const string PotatoPlantPath = FarmRoot + "/Plants/SM_Prop_Plant_Potato_01_L.prefab";
        private const string PotatoBoxPath = FarmRoot + "/Plants/SM_Prop_Box_Potato_01.prefab";
        private const string HubGaragePath = "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_Garage_01.prefab";
        private const string DeliveryTruckPath = "Assets/Synty/PolygonTown/Prefabs/Vehicles/SM_Veh_Truck_Delivery_01.prefab";
        private const string CityPalletPath = "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Pallet_01.prefab";
        private const string FarmPalletCratePath = FarmRoot + "/Props/SM_Prop_PalletCrate_01.prefab";
        private const string CityShopPath = "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_Shop_01.prefab";
        private const string MarketShelfPath = "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_ShopInterior_Shelf_01.prefab";
        private const string TownCartPath = "Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_Cart_01.prefab";
        private const string TownResidentPath = "Assets/Synty/PolygonTown/Prefabs/Characters/SM_Chr_Mother_01.prefab";
        private const string TownShopKeeperPath = "Assets/Synty/PolygonTown/Prefabs/Characters/SM_Chr_ShopKeeper_01.prefab";
        private const string RestaurantPath = "Assets/Synty/PolygonCity/Prefabs/Buildings/SM_Bld_Shop_03.prefab";
        private const string PizzaSignPath = "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_LargeSign_Pizza_01.prefab";
        private const string FoodDriverVehiclePath = "Assets/Synty/PolygonTown/Prefabs/Vehicles/SM_Veh_Convertable_01.prefab";
        private const string FoodDriverPath = "Assets/Synty/PolygonTown/Prefabs/Characters/SM_Chr_Father_02.prefab";
        private const string PizzaBoxPath = "Assets/Synty/PolygonTown/Prefabs/Items/SM_Item_Pizza_Box_01.prefab";

        [MenuItem("Ssalddel/EXH-5/통합 모판 전시관 생성")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("IntegratedExhibitionBuildRequiresEditMode");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "통합모판전시관";
            var root = new GameObject(RootName);
            var camera = BuildCamera(root.transform);
            var beacons = BuildWorld(root.transform);
            BuildEventSystem(root.transform);

            var presenter = root.AddComponent<통합전시관Presenter>();
            var ui = BuildUi(root.transform, camera);
            presenter.Configure(ui.Title, ui.Summary, ui.State, ui.Detail, ui.Evidence,
                ui.Boundary, ui.Footer, ui.Buttons, beacons);
            presenter.Initialize();

            ValidateOpenScene();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("IntegratedExhibitionSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("IntegratedSeedbedExhibitionBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/EXH-5/통합 모판 전시관 검증")]
        public static void ValidateOpenScene()
        {
            var root = GameObject.Find(RootName)
                ?? throw new InvalidOperationException("IntegratedExhibitionRootMissing");
            var presenter = root.GetComponent<통합전시관Presenter>()
                ?? throw new InvalidOperationException("IntegratedExhibitionPresenterMissing");
            presenter.ValidateWiring();
            if (presenter.전시수 != 6 || presenter.운영Command제공여부)
                throw new InvalidOperationException("IntegratedExhibitionInitialStateInvalid");
            if (root.GetComponentsInChildren<Button>(true).Any(value =>
                value.name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0
                || value.name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0
                || value.name.IndexOf("Command", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("IntegratedExhibitionCommandButtonForbidden");
            if (root.GetComponentsInChildren<Transform>(true).Count(value =>
                    value.name.StartsWith("SyntyPrefabInstance_", StringComparison.Ordinal)) < 22)
                throw new InvalidOperationException("IntegratedExhibitionSyntyVisualsMissing");

            presenter.Execute(통합전시관ActionCode.네번째전시);
            var detail = root.transform.Find("통합전시관Canvas/DetailPanel/Detail")
                ?.GetComponent<Text>()?.text ?? string.Empty;
            if (presenter.선택ExhibitStableId != "exhibit:logistics:cargo-hub-warehouse"
                || !detail.Contains("ArrivedAtHub", StringComparison.Ordinal)
                || !detail.Contains("ArrivedAtWarehouse", StringComparison.Ordinal)
                || !detail.Contains("별도 Confirm", StringComparison.Ordinal))
                throw new InvalidOperationException("IntegratedExhibitionCargoLineageMissing");

            presenter.Execute(통합전시관ActionCode.다섯번째전시);
            var state = root.transform.Find("통합전시관Canvas/StatePanel/State")
                ?.GetComponent<Text>()?.text ?? string.Empty;
            if (!state.Contains("미수집", StringComparison.Ordinal))
                throw new InvalidOperationException("IntegratedExhibitionUncollectedStateMissing");
            presenter.Execute(통합전시관ActionCode.여섯번째전시);
            detail = root.transform.Find("통합전시관Canvas/DetailPanel/Detail")
                ?.GetComponent<Text>()?.text ?? string.Empty;
            var footer = root.transform.Find("통합전시관Canvas/Footer")
                ?.GetComponent<Text>()?.text ?? string.Empty;
            if (presenter.선택ExhibitStableId != "exhibit:town-city:orderer-group-urban-market"
                || !detail.Contains("본인 비공개", StringComparison.Ordinal)
                || !detail.Contains("개인정보 제거 집계", StringComparison.Ordinal)
                || !detail.Contains("주문자 공개", StringComparison.Ordinal)
                || !detail.Contains("마트 운영자 전용", StringComparison.Ordinal)
                || !footer.Contains("판매가 ≠ KAMIS 관측", StringComparison.Ordinal))
                throw new InvalidOperationException("IntegratedExhibitionOrdererMarketBoundaryMissing");
            presenter.Execute(통합전시관ActionCode.두번째전시);
            detail = root.transform.Find("통합전시관Canvas/DetailPanel/Detail")
                ?.GetComponent<Text>()?.text ?? string.Empty;
            footer = root.transform.Find("통합전시관Canvas/Footer")
                ?.GetComponent<Text>()?.text ?? string.Empty;
            if (presenter.선택ExhibitStableId != "exhibit:city:food-delivery"
                || !detail.Contains("기사 후보 권역 축약", StringComparison.Ordinal)
                || !detail.Contains("확정 기사 전용", StringComparison.Ordinal)
                || !detail.Contains("전달완료", StringComparison.Ordinal)
                || !detail.Contains("수령확인", StringComparison.Ordinal)
                || !footer.Contains("전달 완료 ≠ 주문자 수령 확인", StringComparison.Ordinal))
                throw new InvalidOperationException("IntegratedExhibitionFoodDeliveryBoundaryMissing");
            presenter.Execute(통합전시관ActionCode.세번째전시);
            if (presenter.선택ExhibitStableId != "exhibit:farm:potato-lifecycle")
                throw new InvalidOperationException("IntegratedExhibitionFarmSelectionInvalid");
            presenter.Execute(통합전시관ActionCode.첫번째전시);
        }

        [MenuItem("Ssalddel/EXH-5/신티 에셋 모판 보기")]
        public static void ShowAssetSeedbed() => FindPresenter().Execute(통합전시관ActionCode.첫번째전시);

        [MenuItem("Ssalddel/EXH-5/음식배달 계보 보기")]
        public static void ShowFoodDelivery() => FindPresenter().Execute(통합전시관ActionCode.두번째전시);

        [MenuItem("Ssalddel/EXH-5/감자 재배 체험 보기")]
        public static void ShowFarmLifecycle() => FindPresenter().Execute(통합전시관ActionCode.세번째전시);

        [MenuItem("Ssalddel/EXH-5/화물 Hub 창고 계보 보기")]
        public static void ShowCargoHubWarehouse() => FindPresenter().Execute(통합전시관ActionCode.네번째전시);

        [MenuItem("Ssalddel/EXH-5/감자 현실 관측 보기")]
        public static void ShowPotatoObservation() => FindPresenter().Execute(통합전시관ActionCode.다섯번째전시);

        [MenuItem("Ssalddel/EXH-5/주문자 집단 도심마트 경계 보기")]
        public static void ShowOrdererGroupUrbanMarket() => FindPresenter().Execute(통합전시관ActionCode.여섯번째전시);

        private static 통합전시관Presenter FindPresenter()
            => GameObject.Find(RootName)?.GetComponent<통합전시관Presenter>()
               ?? throw new InvalidOperationException("IntegratedExhibitionPresenterMissing");

        private static Camera BuildCamera(Transform parent)
        {
            var camera = new GameObject("통합전시관Camera").AddComponent<Camera>();
            camera.transform.SetParent(parent, false);
            camera.transform.position = new Vector3(0f, 23f, -27f);
            camera.transform.LookAt(new Vector3(0f, 2.4f, 4.5f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .07f, .09f);
            camera.fieldOfView = 43f;
            camera.gameObject.AddComponent<AudioListener>();

            var sun = new GameObject("전시관Sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.color = new Color(1f, .86f, .68f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.27f, .34f, .36f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.08f, .13f, .15f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 58f;
            return camera;
        }

        private static Renderer[] BuildWorld(Transform parent)
        {
            var world = new GameObject("VisualRoot_PresentationOnly").transform;
            world.SetParent(parent, false);
            Primitive(world, "WorldBase", PrimitiveType.Cube, new Vector3(0f, -.65f, 4f),
                new Vector3(39f, .8f, 23f), new Color(.07f, .12f, .13f));
            Primitive(world, "자료관Ground", PrimitiveType.Cube, new Vector3(-12f, -.18f, 4f),
                new Vector3(11f, .16f, 18f), new Color(.09f, .23f, .29f));
            Primitive(world, "농장Ground", PrimitiveType.Cube, new Vector3(0f, -.18f, 4f),
                new Vector3(11f, .16f, 18f), new Color(.2f, .27f, .13f));
            Primitive(world, "모판Ground", PrimitiveType.Cube, new Vector3(12f, -.18f, 4f),
                new Vector3(11f, .16f, 18f), new Color(.15f, .24f, .22f));
            Primitive(world, "MainPath", PrimitiveType.Cube, new Vector3(0f, -.05f, -3.6f),
                new Vector3(35f, .12f, 2.4f), new Color(.32f, .33f, .3f));
            Primitive(world, "화물HubGround", PrimitiveType.Cube, new Vector3(6f, -.02f, -1.8f),
                new Vector3(12f, .18f, 5.2f), new Color(.24f, .2f, .12f));
            Primitive(world, "TownCityMarketGround", PrimitiveType.Cube, new Vector3(-6f, -.02f, -1.8f),
                new Vector3(11f, .18f, 5.2f), new Color(.14f, .18f, .28f));
            Primitive(world, "음식배달Ground", PrimitiveType.Cube, new Vector3(14.8f, -.01f, -1.8f),
                new Vector3(5.2f, .2f, 5.2f), new Color(.3f, .13f, .09f));

            InstantiateVisual(CityHallPath, world, "SyntyPrefabInstance_자료관시청",
                new Vector3(-12f, 0f, 6.2f), Quaternion.Euler(0f, 180f, 0f), 8.8f);
            InstantiateVisual(GreenhousePath, world, "SyntyPrefabInstance_농장온실",
                new Vector3(0f, 0f, 7f), Quaternion.identity, 8.5f);
            InstantiateVisual(TownHousePath, world, "SyntyPrefabInstance_모판마을집",
                new Vector3(12f, 0f, 6.5f), Quaternion.Euler(0f, 180f, 0f), 8f);

            for (var i = 0; i < 2; i++)
                InstantiateVisual(DirtRowsPath, world, "SyntyPrefabInstance_감자밭고랑_" + i,
                    new Vector3(-2.5f + i * 5f, 0f, 1.5f), Quaternion.identity, 4.4f);
            for (var i = 0; i < 3; i++)
                InstantiateVisual(PotatoPlantPath, world, "SyntyPrefabInstance_감자재배체_" + i,
                    new Vector3(-2f + i * 2f, .08f, -.3f), Quaternion.identity, 1.8f);
            InstantiateVisual(PotatoBoxPath, world, "SyntyPrefabInstance_감자수확상자",
                new Vector3(2.8f, .05f, -1f), Quaternion.Euler(0f, -28f, 0f), 2.2f);
            InstantiateVisual(HubGaragePath, world, "SyntyPrefabInstance_화물Hub창고",
                new Vector3(8.6f, 0f, 1.2f), Quaternion.Euler(0f, 205f, 0f), 5.4f);
            InstantiateVisual(DeliveryTruckPath, world, "SyntyPrefabInstance_CargoJourneyTruck",
                new Vector3(3.2f, .04f, -2.2f), Quaternion.Euler(0f, 75f, 0f), 3.8f);
            InstantiateVisual(CityPalletPath, world, "SyntyPrefabInstance_창고Pallet",
                new Vector3(7.4f, .05f, -1.8f), Quaternion.identity, 1.5f);
            InstantiateVisual(FarmPalletCratePath, world, "SyntyPrefabInstance_입고PalletCrate",
                new Vector3(9.2f, .05f, -2f), Quaternion.Euler(0f, 25f, 0f), 1.8f);
            InstantiateVisual(CityShopPath, world, "SyntyPrefabInstance_City도심마트",
                new Vector3(-8.4f, 0f, .2f), Quaternion.Euler(0f, 165f, 0f), 5.4f);
            InstantiateVisual(MarketShelfPath, world, "SyntyPrefabInstance_운영자전용재고진열",
                new Vector3(-4.1f, .05f, -1.5f), Quaternion.Euler(0f, -15f, 0f), 2.1f);
            InstantiateVisual(TownCartPath, world, "SyntyPrefabInstance_Town집단수요Cart",
                new Vector3(-6.2f, .05f, -2.4f), Quaternion.Euler(0f, 20f, 0f), 1.8f);
            InstantiateVisual(TownResidentPath, world, "SyntyPrefabInstance_비공개개별의향주민",
                new Vector3(-7.2f, .05f, -3.1f), Quaternion.Euler(0f, 150f, 0f), 1.8f);
            InstantiateVisual(TownShopKeeperPath, world, "SyntyPrefabInstance_마트운영자",
                new Vector3(-4.7f, .05f, -2.8f), Quaternion.Euler(0f, 205f, 0f), 1.8f);
            InstantiateVisual(RestaurantPath, world, "SyntyPrefabInstance_음식점조리픽업",
                new Vector3(16.1f, 0f, .2f), Quaternion.Euler(0f, 160f, 0f), 4.5f);
            InstantiateVisual(PizzaSignPath, world, "SyntyPrefabInstance_음식점Pizza표지",
                new Vector3(13.1f, .05f, -.8f), Quaternion.Euler(0f, 180f, 0f), 2.1f);
            InstantiateVisual(FoodDriverVehiclePath, world, "SyntyPrefabInstance_음식배달기사차량",
                new Vector3(13.2f, .04f, -2.1f), Quaternion.Euler(0f, 72f, 0f), 2.8f);
            InstantiateVisual(FoodDriverPath, world, "SyntyPrefabInstance_확정음식배달기사",
                new Vector3(14.1f, .05f, -2.8f), Quaternion.Euler(0f, 205f, 0f), 1.7f);
            InstantiateVisual(PizzaBoxPath, world, "SyntyPrefabInstance_음식픽업인계상자",
                new Vector3(15.2f, .05f, -2.3f), Quaternion.Euler(0f, 15f, 0f), 1.2f);
            for (var i = 0; i < 6; i++)
                Primitive(world, "TownCity공개범위Checkpoint_" + (i + 1), PrimitiveType.Cylinder,
                    new Vector3(-9f + i * 1.25f, .27f, -3.5f), new Vector3(.3f, .27f, .3f),
                    i == 0 ? new Color(.55f, .3f, .64f)
                    : i < 3 ? new Color(.26f, .62f, .64f)
                    : i == 3 ? new Color(.3f, .68f, .4f)
                    : new Color(.9f, .48f, .18f));
            for (var i = 0; i < 7; i++)
                Primitive(world, "화물Checkpoint_" + (i + 1), PrimitiveType.Cylinder,
                    new Vector3(1.2f + i * 1.35f, .28f, -3.2f),
                    new Vector3(.34f, .28f, .34f), (i == 1 || i == 4 || i == 5)
                        ? new Color(.94f, .52f, .17f) : new Color(.28f, .65f, .72f));
            for (var i = 0; i < 8; i++)
                Primitive(world, "음식배달Checkpoint_" + (i + 1), PrimitiveType.Cylinder,
                    new Vector3(11.1f + i * .95f, .3f, -3.55f), new Vector3(.27f, .3f, .27f),
                    i < 3 ? new Color(.78f, .32f, .16f)
                    : i == 3 ? new Color(.42f, .66f, .72f)
                    : i < 7 ? new Color(.95f, .58f, .16f)
                    : new Color(.58f, .32f, .68f));

            for (var i = 0; i < 4; i++)
                Primitive(world, "모판연구대_" + i, PrimitiveType.Cylinder,
                    new Vector3(9.2f + i * 1.9f, .35f, -.2f + (i % 2) * 1.8f),
                    new Vector3(1.25f, .28f, 1.25f), new Color(.25f, .42f, .36f));
            for (var i = 0; i < 3; i++)
                Primitive(world, "자료관관측Marker_" + i, PrimitiveType.Sphere,
                    new Vector3(-14f + i * 2f, .65f, -.2f + i * .35f),
                    Vector3.one * .75f, i == 1 ? new Color(.77f, .25f, .18f) : new Color(.2f, .62f, .74f));

            BuildLobby(world);
            return new[]
            {
                Beacon(world, "모판상태Beacon", new Vector3(12f, 2.4f, -2.2f), new Color(.2f, .55f, .72f)),
                Beacon(world, "음식배달상태Beacon", new Vector3(17f, 2.4f, -2.2f), new Color(.2f, .55f, .72f)),
                Beacon(world, "농장상태Beacon", new Vector3(0f, 2.4f, -2.2f), new Color(.2f, .55f, .72f)),
                Beacon(world, "화물Hub상태Beacon", new Vector3(6f, 2.4f, -2.2f), new Color(.2f, .55f, .72f)),
                Beacon(world, "자료관상태Beacon", new Vector3(-12f, 2.4f, -2.2f), new Color(.78f, .25f, .2f)),
                Beacon(world, "TownCity마트상태Beacon", new Vector3(-6f, 2.4f, -2.2f), new Color(.2f, .55f, .72f)),
            };
        }

        private static void BuildLobby(Transform parent)
        {
            Primitive(parent, "LobbyLeft", PrimitiveType.Cube, new Vector3(-4.5f, 2f, -6.2f),
                new Vector3(.7f, 4f, .7f), new Color(.76f, .49f, .17f));
            Primitive(parent, "LobbyRight", PrimitiveType.Cube, new Vector3(4.5f, 2f, -6.2f),
                new Vector3(.7f, 4f, .7f), new Color(.76f, .49f, .17f));
            Primitive(parent, "LobbyBeam", PrimitiveType.Cube, new Vector3(0f, 4f, -6.2f),
                new Vector3(9.7f, .7f, .7f), new Color(.76f, .49f, .17f));
            Primitive(parent, "LobbyAuthorityCore", PrimitiveType.Sphere, new Vector3(0f, 2.2f, -5.8f),
                Vector3.one * 1.25f, new Color(.95f, .7f, .24f));
        }

        private static Renderer Beacon(Transform parent, string name, Vector3 position, Color color)
        {
            var value = Primitive(parent, name, PrimitiveType.Cylinder, position,
                new Vector3(.7f, 2.4f, .7f), color);
            return value.GetComponent<Renderer>();
        }

        private static void InstantiateVisual(
            string path, Transform parent, string name, Vector3 position, Quaternion rotation, float targetSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("IntegratedExhibitionPrefabMissing:" + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            Normalize(instance, targetSize, position.y);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        private static void Normalize(GameObject instance, float targetSize, float groundY)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (max > .001f) instance.transform.localScale *= targetSize / max;
            renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            instance.transform.position += new Vector3(0f, groundY - bounds.min.y, 0f);
        }

        private static GameObject Primitive(
            Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.position = position;
            value.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            value.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
            return value;
        }

        private static void BuildEventSystem(Transform parent)
        {
            var value = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            value.transform.SetParent(parent, false);
        }

        private static UiRefs BuildUi(Transform parent, Camera camera)
        {
            var canvasObject = new GameObject("통합전시관Canvas");
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

            var header = Panel(canvasObject.transform, "Header", new Vector2(.018f, .835f),
                new Vector2(.982f, .985f), new Color(.025f, .055f, .065f, .94f));
            var title = Text(header, "Title", "통합 모판·전시관", new Vector2(.025f, .5f),
                new Vector2(.62f, .98f), 31, new Color(.98f, .75f, .28f), FontStyle.Bold);
            var summary = Text(header, "Summary", "", new Vector2(.025f, .03f),
                new Vector2(.62f, .52f), 15, new Color(.78f, .88f, .85f), FontStyle.Normal);
            var statePanel = Panel(canvasObject.transform, "StatePanel", new Vector2(.63f, .855f),
                new Vector2(.97f, .965f), new Color(.08f, .13f, .15f, .98f));
            var state = Text(statePanel, "State", "", new Vector2(.04f, .08f),
                new Vector2(.96f, .92f), 15, new Color(.94f, .94f, .84f), FontStyle.Bold);

            var nav = Panel(canvasObject.transform, "ExhibitNavigation", new Vector2(.018f, .045f),
                new Vector2(.30f, .325f), new Color(.03f, .065f, .07f, .96f));
            Text(nav, "NavTitle", "전시 후보", new Vector2(.04f, .84f), new Vector2(.96f, .98f),
                18, Color.white, FontStyle.Bold);
            var buttons = new Button[6];
            for (var i = 0; i < buttons.Length; i++)
            {
                var top = .8f - i * .125f;
                buttons[i] = Button(nav, "Exhibit_" + i, "전시", new Vector2(.04f, top - .095f),
                    new Vector2(.96f, top));
            }

            var detailPanel = Panel(canvasObject.transform, "DetailPanel", new Vector2(.31f, .045f),
                new Vector2(.56f, .325f), new Color(.035f, .06f, .08f, .96f));
            var detail = Text(detailPanel, "Detail", "", new Vector2(.045f, .05f),
                new Vector2(.955f, .95f), 13, new Color(.92f, .87f, .68f), FontStyle.Normal);
            detail.resizeTextForBestFit = true;
            detail.resizeTextMinSize = 10;
            detail.resizeTextMaxSize = 13;

            var evidencePanel = Panel(canvasObject.transform, "EvidencePanel", new Vector2(.57f, .045f),
                new Vector2(.755f, .325f), new Color(.04f, .07f, .075f, .96f));
            var evidence = Text(evidencePanel, "Evidence", "", new Vector2(.05f, .05f),
                new Vector2(.95f, .95f), 13, new Color(.76f, .88f, .86f), FontStyle.Normal);
            evidence.resizeTextForBestFit = true;
            evidence.resizeTextMinSize = 10;
            evidence.resizeTextMaxSize = 13;

            var boundaryPanel = Panel(canvasObject.transform, "BoundaryPanel", new Vector2(.765f, .045f),
                new Vector2(.982f, .325f), new Color(.07f, .05f, .055f, .96f));
            var boundary = Text(boundaryPanel, "Boundary", "", new Vector2(.05f, .05f),
                new Vector2(.95f, .95f), 13, new Color(.94f, .72f, .64f), FontStyle.Normal);
            boundary.resizeTextForBestFit = true;
            boundary.resizeTextMinSize = 10;
            boundary.resizeTextMaxSize = 13;

            var footer = Text(canvasObject.transform, "Footer", "", new Vector2(.018f, .005f),
                new Vector2(.982f, .04f), 13, new Color(1f, .58f, .3f), FontStyle.Bold);
            footer.alignment = TextAnchor.MiddleCenter;
            return new UiRefs(title, summary, state, detail, evidence, boundary, footer, buttons);
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

        private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            target.GetComponent<Image>().color = new Color(.1f, .16f, .18f, .96f);
            var labelText = Text(target.transform, "Label", label, new Vector2(.04f, .08f),
                new Vector2(.96f, .92f), 14, Color.white, FontStyle.Bold);
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

        private sealed class UiRefs
        {
            public UiRefs(
                Text title,
                Text summary,
                Text state,
                Text detail,
                Text evidence,
                Text boundary,
                Text footer,
                Button[] buttons)
            {
                Title = title;
                Summary = summary;
                State = state;
                Detail = detail;
                Evidence = evidence;
                Boundary = boundary;
                Footer = footer;
                Buttons = buttons;
            }

            public Text Title { get; }
            public Text Summary { get; }
            public Text State { get; }
            public Text Detail { get; }
            public Text Evidence { get; }
            public Text Boundary { get; }
            public Text Footer { get; }
            public Button[] Buttons { get; }
        }
    }
}
