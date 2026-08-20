using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Survival;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ssalddel.Unity.Editor
{
    public static class SimulationWorldShellBuilder
    {
        public const string ScenePath = 통합WorldScenePolicy.CanonicalScenePath;
        public const string RootName = "SimulationWorldShell";
        public const string EnvironmentCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/FarmCityEnvironmentCatalog.asset";
        public const string ExhibitionObjectCatalogPath =
            "Assets/Ssalddel/Presentation/ExhibitionObjects/통합전시관ObjectVisualCatalog.asset";
        public const string FigmaMauiWarehouseUiThemePath =
            "Assets/Ssalddel/Presentation/World/Catalogs/FigmaMauiWarehouseUiThemeCatalog.asset";
        public const string TacticalCharacterVisualCatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창군전술CharacterVisualCatalog.asset";
        public const string InputActionAssetPath = "Assets/InputSystem_Actions.inputactions";
        public const string SceneStableId = "scene:simulation-world-shell";
        public const string PotatoHarvestBoxPlacementStableId =
            "scene-placement:simulation-world-shell.farm.potato-harvest-box.a";
        public const string HubInboundGatePlacementStableId =
            "scene-placement:simulation-world-shell.logistics.hub-inbound-gate.a";
        public const string DeliveryTruckPlacementStableId =
            "scene-placement:simulation-world-shell.logistics.delivery-truck.a";
        public const string CargoPalletPlacementStableId =
            "scene-placement:simulation-world-shell.logistics.cargo-pallet.a";
        public const string FarmPalletCratePlacementStableId =
            "scene-placement:simulation-world-shell.farm.pallet-crate.a";
        public const string UrbanMarketShopPlacementStableId =
            "scene-placement:simulation-world-shell.market.urban-market-shop.a";
        public const string GroupingCartTablePlacementStableId =
            "scene-placement:simulation-world-shell.town.grouping-cart-table.a";

        private const float TacticalNavigationFloorThickness = .12f;

        [MenuItem("Ssalddel/WORLD-SHELL-0/Build Read Only Shell")]
        public static void BuildWorldShell()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName);
            var shellRuntimeRoot = Child(root.transform, "ShellRuntimeRoot").gameObject;
            var worldMapRoot = Child(root.transform, "WorldMapRoot").gameObject;
            var settlementRoot = Child(root.transform, "SettlementInteriorRoot").gameObject;
            var cameraSystem = Child(root.transform, "CameraSystem");
            var lightingRoot = Child(root.transform, "Lighting");
            var persistentUi = Child(root.transform, "PersistentUI");

            BuildWorldMap(worldMapRoot.transform);
            BuildSettlementHierarchy(settlementRoot.transform);
            BuildSettlementTerrain(Required(settlementRoot.transform, "Terrain"));
            BuildSettlementRoads(Required(settlementRoot.transform, "Roads"));
            BuildDistricts(Required(settlementRoot.transform, "Districts"));
            BuildSettlementVisualBase(settlementRoot.transform);
            var rig = BuildCamera(cameraSystem, worldMapRoot.transform, settlementRoot.transform);
            var light = BuildLighting(lightingRoot);
            root.AddComponent<월드시간대Presenter>()
                .Configure(light, rig.GetComponent<Camera>(), root.transform, 15f / 24f, false, 180f);
            var ui = BuildHud(persistentUi, rig.GetComponent<Camera>());

            var presenter = shellRuntimeRoot.AddComponent<SimulationWorldShellPresenter>();
            presenter.Configure(
                worldMapRoot,
                settlementRoot,
                rig,
                ui.Mode,
                ui.Identity,
                ui.Economy,
                ui.Selection,
                ui.WorldMap,
                ui.Settlement,
                ui.Back,
                ui.Pause,
                ui.Speed);
            cameraSystem.GetComponentInChildren<전략카메라Controller>(true)
                .BindShellPresenter(presenter);
            Build정착지상호작용(ui.Canvas, presenter, shellRuntimeRoot.transform);
            Build물류이동(ui.Canvas, presenter, shellRuntimeRoot.transform);
            Build턴마감(ui.Canvas, presenter, shellRuntimeRoot.transform);
            Build진부Hub입고Ui(ui.Canvas, presenter, shellRuntimeRoot.transform);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            settlementRoot.SetActive(false);
            worldMapRoot.SetActive(true);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SimulationWorldShellSceneSaveFailed");
            통합WorldScenePolicy.ApplyCanonicalBuildSettings();
            AssetDatabase.SaveAssets();
            ValidateWorldShellOpenScene();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("WORLD-SHELL-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/SETTLEMENT-SCENE-0/Build First Settlement")]
        public static void BuildSettlementScene()
        {
            OpenShellIfRequired();
            var shellRoot = Required(RootName);
            var settlementRoot = Required(shellRoot.transform, "SettlementInteriorRoot").gameObject;
            var terrain = Required(settlementRoot.transform, "Terrain");
            var roads = Required(settlementRoot.transform, "Roads");
            var districts = Required(settlementRoot.transform, "Districts");
            Clear(terrain);
            Clear(roads);
            Clear(districts);

            BuildSettlementTerrain(terrain);
            BuildSettlementRoads(roads);
            BuildDistricts(districts);
            BuildSettlementVisualBase(settlementRoot.transform);
            settlementRoot.SetActive(false);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SimulationSettlementSceneSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateSettlementSceneOpenScene();
            Selection.activeGameObject = settlementRoot;
            SceneView.RepaintAll();
            Debug.Log("SETTLEMENT-SCENE-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/JINBU-INBOUND-UI-0/Build Figma MAUI Compatible UI")]
        public static void Build진부Hub입고Ui()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var runtimeRoot = Required(root.transform, "ShellRuntimeRoot");
            var canvas = Required(Required(root.transform, "PersistentUI"), "SimulationWorldHud");
            var oldPanel = canvas.Find("JinbuInboundPanel");
            if (oldPanel != null) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);
            var oldPresenter = runtimeRoot.GetComponent<진부Hub입고UiPresenter>();
            if (oldPresenter != null) UnityEngine.Object.DestroyImmediate(oldPresenter);
            var oldComposition = runtimeRoot.GetComponent<진부Hub입고UiSceneCompositionRoot>();
            if (oldComposition != null) UnityEngine.Object.DestroyImmediate(oldComposition);
            Build진부Hub입고Ui(canvas, FindPresenter(), runtimeRoot);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("JinbuInboundUiSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Validate진부Hub입고UiOpenScene();
            Debug.Log("JINBU-INBOUND-UI-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/JINBU-INBOUND-UI-0/Validate Open Scene")]
        public static void Validate진부Hub입고UiOpenScene()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("JinbuInboundUiPresenterMissing");
            presenter.ValidateWiring();
            var composition = UnityEngine.Object.FindFirstObjectByType<진부Hub입고UiSceneCompositionRoot>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("JinbuInboundUiCompositionMissing");
            if (!composition.서버기준사용중)
                throw new InvalidOperationException("JinbuInboundUiServerAuthorityDisabled");
            var panel = Required(Required(RootName).transform,
                "PersistentUI/SimulationWorldHud/JinbuInboundPanel");
            if (panel.GetComponentsInChildren<Button>(true).Length != 4)
                throw new InvalidOperationException("JinbuInboundUiButtonCountInvalid");
            var theme = AssetDatabase.LoadAssetAtPath<FigmaMauiWarehouseUiThemeCatalog>(
                FigmaMauiWarehouseUiThemePath)
                ?? throw new InvalidOperationException("JinbuInboundUiThemeMissing");
            if (!theme.Supports(진부Hub입고UiCodes.SupportedDesignProfileRevision))
                throw new InvalidOperationException("JinbuInboundUiThemeRevisionInvalid");
            Debug.Log("JINBU-INBOUND-UI-0 validation passed");
        }

        [MenuItem("Ssalddel/통합 월드/기존 Scene에 모드 전환 UI 연결")]
        public static void Build통합월드ModeNavigation()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var runtimeRoot = Required(root.transform, "ShellRuntimeRoot");
            var persistentUi = Required(root.transform, "PersistentUI");
            var canvas = Required(persistentUi, "SimulationWorldHud");
            var oldBar = canvas.Find("UnifiedWorldModeBar");
            if (oldBar != null) UnityEngine.Object.DestroyImmediate(oldBar.gameObject);
            var oldModeCanvas = persistentUi.Find("UnifiedWorldModeCanvas");
            if (oldModeCanvas != null)
                UnityEngine.Object.DestroyImmediate(oldModeCanvas.gameObject);
            var oldPresenter = runtimeRoot.GetComponent<통합월드ModePresenter>();
            if (oldPresenter != null) UnityEngine.Object.DestroyImmediate(oldPresenter);

            var shell = FindPresenter();
            var player = UnityEngine.Object.FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldPlayerMissing");
            var inbound = UnityEngine.Object.FindFirstObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("UnifiedWorldInboundUiMissing");
            Build통합월드ModeNavigation(persistentUi, runtimeRoot, shell, player, inbound);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("UnifiedWorldModeSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Debug.Log("UNIFIED-WORLD-1: 기존 SimulationWorldShell에 통합 모드 전환 UI를 연결했습니다.");
        }

        [MenuItem("Ssalddel/FARM-TACTICAL-SQUAD-0/Build Formation Movement")]
        public static void Build전술분대이동()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var runtimeRoot = Required(root.transform, "ShellRuntimeRoot");
            var player = root.GetComponentInChildren<플레이어경관Controller>(true)
                ?? throw new InvalidOperationException("TacticalSquadPlayerMissing");
            Build농장경영시점(runtimeRoot, player);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("TacticalSquadSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Validate전술분대이동OpenScene();
            Debug.Log("FARM-TACTICAL-SQUAD-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/FARM-TACTICAL-SQUAD-0/Validate Open Scene")]
        public static void Validate전술분대이동OpenScene()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<전술분대Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TacticalSquadPresenterMissing");
            if (!presenter.ValidateWiring() || presenter.Squads.Count != 2
                || presenter.Squads.Sum(value => value.Members.Count) != 12
                || presenter.Squads.Any(value => value.NavigationAgent == null
                    || !value.PresentationOnly))
                throw new InvalidOperationException("TacticalSquadSceneWiringInvalid");
            var catalog = AssetDatabase.LoadAssetAtPath<전술CharacterVisualCatalog>(
                TacticalCharacterVisualCatalogPath)
                ?? throw new InvalidOperationException("TacticalCharacterCatalogMissing");
            catalog.Validate();
            var combat = UnityEngine.Object.FindFirstObjectByType<전투시점Controller>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TacticalCombatControllerMissing");
            if (combat.TacticalSquads != presenter || !combat.PresentationOnly)
                throw new InvalidOperationException("TacticalCombatSquadBridgeInvalid");
        }

        [MenuItem("Ssalddel/WORLD-SHELL-0/Build Shell And Settlement")]
        public static void BuildAll()
        {
            BuildWorldShell();
            BuildSettlementScene();
        }

        [MenuItem("Ssalddel/WORLD-SHELL-0/Add Player Strategy Camera")]
        public static void BuildPlayerStrategyCamera()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var cameraSystem = Required(root.transform, "CameraSystem");
            var rig = root.GetComponentInChildren<DioramaTopDownCameraRig>(true)
                ?? throw new InvalidOperationException("SimulationWorldShellCameraMissing");
            UpgradePlayerCameraHierarchy(cameraSystem, rig);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SimulationWorldShellStrategyCameraSaveFailed");
            AssetDatabase.SaveAssets();
            ValidatePlayerStrategyCameraOpenScene();
            Debug.Log("PLAYER-CAMERA-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/SETTLEMENT-VISUAL-BASE-0/Build Visual Base")]
        public static void BuildVisualBase()
        {
            OpenShellIfRequired();
            var shellRoot = Required(RootName);
            var settlementRoot = Required(shellRoot.transform, "SettlementInteriorRoot");
            BuildSettlementVisualBase(settlementRoot);
            ConfigureTimeOfDay(shellRoot.transform);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SimulationSettlementVisualBaseSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateSettlementVisualBaseOpenScene();
            Selection.activeGameObject = settlementRoot.gameObject;
            SceneView.RepaintAll();
            Debug.Log("SETTLEMENT-VISUAL-BASE-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/SETTLEMENT-INTERACTION-0/Build Interaction")]
        public static void Build정착지상호작용()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var runtimeRoot = Required(root.transform, "ShellRuntimeRoot");
            var canvas = Required(Required(root.transform, "PersistentUI"), "SimulationWorldHud");
            var existingCard = canvas.Find("HarvestDispositionCard");
            if (existingCard != null) UnityEngine.Object.DestroyImmediate(existingCard.gameObject);
            var existing = runtimeRoot.GetComponent<정착지상호작용Presenter>();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            Build정착지상호작용(canvas, FindPresenter(), runtimeRoot);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SettlementInteractionSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Validate정착지상호작용OpenScene();
            Debug.Log("SETTLEMENT-INTERACTION-0 built: " + ScenePath);
        }

        [MenuItem("Ssalddel/LOGISTICS-MOVEMENT-1/Build Interaction")]
        public static void Build물류이동Interaction()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var runtimeRoot = Required(root.transform, "ShellRuntimeRoot");
            var canvas = Required(Required(root.transform, "PersistentUI"), "SimulationWorldHud");
            var existingCard = canvas.Find("LogisticsMovementCard");
            if (existingCard != null) UnityEngine.Object.DestroyImmediate(existingCard.gameObject);
            var existing = runtimeRoot.GetComponent<물류이동Presenter>();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            Build물류이동(canvas, FindPresenter(), runtimeRoot);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("LogisticsMovementSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Debug.Log("LOGISTICS-MOVEMENT-1 interaction built: " + ScenePath);
        }

        [MenuItem("Ssalddel/TURN-CARD-UI-1B/Build Turn Closing")]
        public static void Build턴마감Interaction()
        {
            OpenShellIfRequired();
            var root = Required(RootName);
            var runtimeRoot = Required(root.transform, "ShellRuntimeRoot");
            var canvas = Required(Required(root.transform, "PersistentUI"), "SimulationWorldHud");
            var existingPanel = canvas.Find("TurnClosingPanel");
            if (existingPanel != null) UnityEngine.Object.DestroyImmediate(existingPanel.gameObject);
            var existing = runtimeRoot.GetComponent<턴마감Presenter>();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            var existingComposition = runtimeRoot.GetComponent<턴마감SceneCompositionRoot>();
            if (existingComposition != null) UnityEngine.Object.DestroyImmediate(existingComposition);
            Build턴마감(canvas, FindPresenter(), runtimeRoot);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("TurnClosingSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Validate턴마감OpenScene();
            Debug.Log("TURN-CARD-UI-1B built: " + ScenePath);
        }

        [MenuItem("Ssalddel/TURN-CARD-UI-1B/Validate Open Scene")]
        public static void Validate턴마감OpenScene()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<턴마감Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TurnClosingPresenterMissing");
            presenter.ValidateWiring();
            var composition = UnityEngine.Object.FindFirstObjectByType<턴마감SceneCompositionRoot>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TurnClosingCompositionRootMissing");
            if (!composition.서버기준사용중)
                throw new InvalidOperationException("TurnClosingServerAuthorityDisabled");
            var panel = Required(Required(RootName).transform,
                "PersistentUI/SimulationWorldHud/TurnClosingPanel");
            if (panel.GetComponentsInChildren<Button>(true).Length != 6)
                throw new InvalidOperationException("TurnClosingButtonCountInvalid");
            Debug.Log("TURN-CARD-UI-1B validation passed");
        }

        [MenuItem("Ssalddel/TURN-CARD-UI-1B/Preview Fool Card")]
        public static async void Preview턴마감Fool()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("TurnClosingPreviewRequiresPlayMode");
            var presenter = UnityEngine.Object.FindFirstObjectByType<턴마감Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TurnClosingPresenterMissing");
            presenter.SelectCard(턴마감CardStableIds.Fool);
            await presenter.PreviewAsync();
        }

        [MenuItem("Ssalddel/TURN-CARD-UI-1B/Run Fool Golden Path")]
        public static async void Run턴마감FoolGoldenPath()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("TurnClosingGoldenPathRequiresPlayMode");
            var presenter = UnityEngine.Object.FindFirstObjectByType<턴마감Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TurnClosingPresenterMissing");
            presenter.SelectCard(턴마감CardStableIds.Fool);
            await presenter.PreviewAsync();
            await presenter.ConfirmAsync();
        }

        [MenuItem("Ssalddel/CULTURE-CARD-0/Preview Seoul Culture Card")]
        public static async void Preview서울문화Card()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("CultureCardPreviewRequiresPlayMode");
            var presenter = UnityEngine.Object.FindFirstObjectByType<턴마감Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TurnClosingPresenterMissing");
            presenter.SelectCard(턴마감CardStableIds.SeoulCulture);
            await presenter.PreviewAsync();
        }

        [MenuItem("Ssalddel/CULTURE-CARD-0/Run Seoul Culture Golden Path")]
        public static async void Run서울문화CardGoldenPath()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("CultureCardGoldenPathRequiresPlayMode");
            var presenter = UnityEngine.Object.FindFirstObjectByType<턴마감Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("TurnClosingPresenterMissing");
            presenter.SelectCard(턴마감CardStableIds.SeoulCulture);
            await presenter.PreviewAsync();
            await presenter.ConfirmAsync();
        }

        [MenuItem("Ssalddel/LOGISTICS-MOVEMENT-1/Preview Cargo")]
        public static void Preview물류화물()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("LogisticsMovementPreviewRequiresPlayMode");
            var target = UnityEngine.Object.FindObjectsByType<SimulationWorldNavigationTargetView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.ObjectStableId == 물류이동Fixture.CargoStableId);
            FindPresenter().NavigateTo(target);
        }

        [MenuItem("Ssalddel/LOGISTICS-MOVEMENT-1/Run Golden Path")]
        public static async void Run물류이동GoldenPath()
        {
            Preview물류화물();
            var presenter = UnityEngine.Object.FindFirstObjectByType<물류이동Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("LogisticsMovementPresenterMissing");
            await presenter.RunGoldenPathAsync();
        }

        [MenuItem("Ssalddel/SETTLEMENT-INTERACTION-0/Validate Open Scene")]
        public static void Validate정착지상호작용OpenScene()
        {
            var presenter = UnityEngine.Object.FindFirstObjectByType<정착지상호작용Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("SettlementInteractionPresenterMissing");
            presenter.ValidateWiring();
            var card = Required(Required(RootName).transform, "PersistentUI/SimulationWorldHud/HarvestDispositionCard");
            if (card.GetComponentsInChildren<Button>(true).Length != 6)
                throw new InvalidOperationException("SettlementInteractionButtonCountInvalid");
            Debug.Log("SETTLEMENT-INTERACTION-0 validation passed");
        }

        [MenuItem("Ssalddel/SETTLEMENT-INTERACTION-0/Preview Harvest Lot")]
        public static void Preview수확Lot()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("SettlementInteractionPreviewRequiresPlayMode");
            var target = UnityEngine.Object.FindObjectsByType<SimulationWorldNavigationTargetView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.ObjectStableId == 정착지상호작용Presenter.수확LotObjectStableId);
            FindPresenter().NavigateTo(target);
        }

        [MenuItem("Ssalddel/SETTLEMENT-INTERACTION-0/Preview Reserve Candidate")]
        public static async void Preview비축Candidate()
        {
            Preview수확Lot();
            var presenter = UnityEngine.Object.FindFirstObjectByType<정착지상호작용Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("SettlementInteractionPresenterMissing");
            await presenter.SelectChoiceAsync(Ssalddel.Unity.Farm.HarvestDispositionChoiceCodes.ReserveStorage);
        }

        [MenuItem("Ssalddel/SETTLEMENT-INTERACTION-0/Run Reserve Golden Path")]
        public static async void Run비축GoldenPath()
        {
            Preview수확Lot();
            var presenter = UnityEngine.Object.FindFirstObjectByType<정착지상호작용Presenter>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("SettlementInteractionPresenterMissing");
            await presenter.RunReserveStorageGoldenPathAsync();
        }

        [MenuItem("Ssalddel/WORLD-SHELL-0/Validate Open Scene")]
        public static void ValidateWorldShellOpenScene()
        {
            var root = Required(RootName);
            var map = Required(root.transform, "WorldMapRoot");
            var settlement = Required(root.transform, "SettlementInteriorRoot");
            Required(root.transform, "ShellRuntimeRoot");
            Required(root.transform, "CameraSystem");
            Required(root.transform, "Lighting");
            Required(root.transform, "PersistentUI");

            foreach (var child in new[]
                     {
                         "TerrainRoot", "TerritoryRoot", "SettlementMarkers", "RegionMarkers",
                         "RouteRoot", "CargoPresentationRoot", "ThreatPresentationRoot", "CameraAnchors",
                     })
                Required(map, child);
            foreach (var child in new[]
                     {
                         "Terrain", "Roads", "Districts", "WorldObjects", "NpcVisuals",
                         "CargoVisuals", "InteractionAnchors", "CameraAnchors",
                     })
                Required(settlement, child);

            var presenter = root.GetComponentInChildren<SimulationWorldShellPresenter>(true)
                ?? throw new InvalidOperationException("SimulationWorldShellPresenterMissing");
            presenter.ValidateWiring();
            var rig = root.GetComponentInChildren<DioramaTopDownCameraRig>(true)
                ?? throw new InvalidOperationException("SimulationWorldShellCameraMissing");
            rig.ApplyNowForTests();
            if (rig.CurrentFocusAnchorId != SimulationWorldShellPresenter.WorldMapFocusAnchorId)
                throw new InvalidOperationException("SimulationWorldShellInitialFocusInvalid");
            if (!map.gameObject.activeSelf || settlement.gameObject.activeSelf)
                throw new InvalidOperationException("SimulationWorldShellInitialSurfaceInvalid");
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(component =>
                    component.GetType().Name.Contains("Command", StringComparison.Ordinal)
                    || component.GetType().Name.Contains("Operational", StringComparison.Ordinal)))
                throw new InvalidOperationException("SimulationWorldShellAuthorityLeak");
        }

        [MenuItem("Ssalddel/SETTLEMENT-SCENE-0/Validate Open Scene")]
        public static void ValidateSettlementSceneOpenScene()
        {
            ValidateWorldShellOpenScene();
            var shellRoot = Required(RootName);
            var districts = Required(shellRoot.transform, "SettlementInteriorRoot/Districts").gameObject;
            var views = districts.GetComponentsInChildren<SimulationWorldDistrictView>(true);
            if (views.Length != 8)
                throw new InvalidOperationException("SimulationSettlementDistrictCountInvalid:" + views.Length);
            foreach (var view in views) view.Validate();
            if (views.Count(view => view.PresentationPlaceholder) != 2)
                throw new InvalidOperationException("SimulationSettlementPlaceholderCountInvalid");
            var ids = views.Select(view => view.DistrictStableId).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidOperationException("SimulationSettlementDistrictStableIdDuplicate");
            if (Required(shellRoot.transform, "SettlementInteriorRoot/Roads").childCount < 6)
                throw new InvalidOperationException("SimulationSettlementRoadsMissing");
            var targets = shellRoot.GetComponentsInChildren<SimulationWorldNavigationTargetView>(true);
            if (targets.Length != 11)
                throw new InvalidOperationException("SimulationWorldNavigationTargetCountInvalid:" + targets.Length);
            foreach (var target in targets) target.Validate();
        }

        [MenuItem("Ssalddel/SETTLEMENT-VISUAL-BASE-0/Validate Open Scene")]
        public static void ValidateSettlementVisualBaseOpenScene()
        {
            ValidateSettlementSceneOpenScene();
            var shellRoot = Required(RootName);
            var settlement = Required(shellRoot.transform, "SettlementInteriorRoot");
            var visualInstances = settlement.GetComponentsInChildren<WorldVisualInstanceView>(true);
            if (visualInstances.Length < 45 || visualInstances.Any(value => !value.ValidateWiring()))
                throw new InvalidOperationException(
                    "SimulationSettlementVisualBaseInstanceInvalid:" + visualInstances.Length);
            if (visualInstances.Any(value => PrefabUtility.GetCorrespondingObjectFromSource(
                    value.PrefabInstanceRoot) == null))
                throw new InvalidOperationException("SimulationSettlementVendorPrefabConnectionMissing");

            RequireDistrictVisual(settlement, "FarmDistrict", FarmVisualKeys.Barn, FarmVisualKeys.PotatoLarge);
            RequireDistrictVisual(settlement, "TownDistrict", UrbanVisualKeys.Apartment);
            RequireDistrictVisual(settlement, "MarketDistrict", UrbanVisualKeys.MarketBuilding,
                FarmVisualKeys.ProduceStand);
            RequireDistrictVisual(settlement, "StorageDistrict", UrbanVisualKeys.LogisticsBuilding,
                UrbanVisualKeys.Pallet);
            RequireDistrictVisual(settlement, "ResidentialDistrict", UrbanVisualKeys.Apartment);

            var placements = settlement.GetComponentsInChildren<통합전시관ScenePlacementView>(true);
            if (placements.Length != 7 || placements.Any(value => !value.ValidateWiring()))
                throw new InvalidOperationException("SimulationSettlementScenePlacementInvalid:" + placements.Length);
            RequireScenePlacement(placements, PotatoHarvestBoxPlacementStableId,
                "district:farm", "seedbed-object:farm.potato-harvest-box.a");
            RequireScenePlacement(placements, HubInboundGatePlacementStableId,
                "district:logistics", "seedbed-object:town.hub-inbound-gate.a");
            RequireScenePlacement(placements, DeliveryTruckPlacementStableId,
                "district:logistics", "seedbed-object:town.delivery-truck.a");
            RequireScenePlacement(placements, CargoPalletPlacementStableId,
                "district:logistics", "seedbed-object:shared.cargo-pallet.a");
            RequireScenePlacement(placements, FarmPalletCratePlacementStableId,
                "district:farm", "seedbed-object:farm.pallet-crate.a");
            RequireScenePlacement(placements, UrbanMarketShopPlacementStableId,
                "district:market", "seedbed-object:city.urban-market-building.a");
            RequireScenePlacement(placements, GroupingCartTablePlacementStableId,
                "district:town", "seedbed-object:town.grouping-cart-table.a");

            var renderers = settlement.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || renderers.Any(renderer => renderer.sharedMaterials.Any(material =>
                    material == null || material.shader == null
                    || material.shader.name == "Hidden/InternalErrorShader")))
                throw new InvalidOperationException("SimulationSettlementVisualBaseShaderInvalid");

            var time = shellRoot.GetComponent<월드시간대Presenter>()
                ?? throw new InvalidOperationException("SimulationSettlementTimeOfDayMissing");
            if (!time.ValidateWiring() || time.SourceMode != 월드시간대SourceMode.FixedReference
                || time.AutoCycleInPlayMode || time.SurfaceBindingCount < 45)
                throw new InvalidOperationException("SimulationSettlementTimeOfDayInvalid");
        }

        [MenuItem("Ssalddel/WORLD-SHELL-0/Open Scene")]
        public static void Open()
            => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        [MenuItem("Ssalddel/WORLD-SHELL-0/Preview World Map")]
        public static void PreviewWorldMap()
        {
            var presenter = FindPresenter();
            if (!UnityEngine.Application.isPlaying)
                presenter.Initialize(SimulationWorldShellFixture.CreateSnapshot());
            presenter.ShowWorldMap();
            SceneView.RepaintAll();
        }

        [MenuItem("Ssalddel/WORLD-SHELL-0/Preview Settlement")]
        public static void PreviewSettlement()
        {
            var presenter = FindPresenter();
            if (!UnityEngine.Application.isPlaying)
                presenter.Initialize(SimulationWorldShellFixture.CreateSnapshot());
            presenter.ShowSettlement();
            SceneView.RepaintAll();
        }

        [MenuItem("Ssalddel/SETTLEMENT-VISUAL-BASE-0/Preview Farm District")]
        public static void PreviewFarmDistrict() => PreviewDistrict("district:farm");

        [MenuItem("Ssalddel/SETTLEMENT-VISUAL-BASE-0/Preview Market District")]
        public static void PreviewMarketDistrict() => PreviewDistrict("district:market");

        [MenuItem("Ssalddel/SETTLEMENT-VISUAL-BASE-0/Preview Town District")]
        public static void PreviewTownDistrict() => PreviewDistrict("district:town");

        [MenuItem("Ssalddel/SETTLEMENT-VISUAL-BASE-0/Preview Logistics District")]
        public static void PreviewLogisticsDistrict() => PreviewDistrict("district:logistics");

        private static void PreviewDistrict(string stableId)
        {
            var presenter = FindPresenter();
            if (!UnityEngine.Application.isPlaying)
                presenter.Initialize(SimulationWorldShellFixture.CreateSnapshot());
            presenter.ShowSettlement();
            var target = UnityEngine.Object.FindObjectsByType<SimulationWorldNavigationTargetView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(value => value.DistrictStableId == stableId
                    && string.IsNullOrEmpty(value.ObjectStableId))
                ?? throw new InvalidOperationException("SimulationSettlementDistrictTargetMissing:" + stableId);
            presenter.NavigateTo(target);
            SceneView.RepaintAll();
        }

        private static void BuildWorldMap(Transform root)
        {
            var terrain = Child(root, "TerrainRoot");
            var territory = Child(root, "TerritoryRoot");
            var settlementMarkers = Child(root, "SettlementMarkers");
            var regionMarkers = Child(root, "RegionMarkers");
            var routes = Child(root, "RouteRoot");
            Child(root, "CargoPresentationRoot");
            var threats = Child(root, "ThreatPresentationRoot");
            threats.gameObject.SetActive(false);
            Child(root, "CameraAnchors");

            Primitive(terrain, "WorldTerrain", PrimitiveType.Cube,
                new Vector3(0f, -.45f, 0f), new Vector3(78f, .8f, 52f), Rgb(.18f, .31f, .22f));
            Primitive(territory, "Territory_1", PrimitiveType.Cylinder,
                new Vector3(0f, .02f, 0f), new Vector3(34f, .08f, 25f), Rgb(.28f, .43f, .27f));

            var farm = Marker(regionMarkers, "FarmRegion", new Vector3(-18f, .8f, 11f),
                new Color(.46f, .69f, .27f), "FARM");
            var town = Marker(regionMarkers, "TownRegion", new Vector3(-10f, .8f, -4f),
                new Color(.76f, .57f, .29f), "TOWN");
            var hub = Marker(regionMarkers, "RegionalLogisticsHub", new Vector3(12f, .8f, -8f),
                new Color(.27f, .62f, .72f), "HUB");
            var city = Marker(regionMarkers, "CityRegion", new Vector3(27f, .8f, -17f),
                new Color(.45f, .48f, .66f), "CITY");
            var settlement = Marker(settlementMarkers, "Settlement_1", new Vector3(5f, 1.2f, 9f),
                new Color(.92f, .72f, .28f), "SETTLEMENT");
            settlement.localScale = Vector3.one * 1.55f;
            settlement.gameObject.AddComponent<CapsuleCollider>();
            settlement.gameObject.AddComponent<SimulationWorldNavigationTargetView>().Configure(
                SimulationObservationScaleCodes.Settlement,
                SimulationWorldShellFixture.SettlementStableId,
                string.Empty,
                string.Empty,
                SimulationWorldShellPresenter.SettlementFocusAnchorId,
                settlement.GetComponent<Renderer>(),
                new Color(.92f, .72f, .28f),
                new Color(1f, .9f, .35f));

            Route(routes, "Route_Farm_Settlement", farm.position, settlement.position, .8f);
            Route(routes, "Route_Town_Settlement", town.position, settlement.position, .65f);
            Route(routes, "Route_Town_Hub", town.position, hub.position, .65f);
            Route(routes, "Route_Settlement_Hub", settlement.position, hub.position, .75f);
            Route(routes, "Route_Hub_City", hub.position, city.position, .8f);

            for (var index = 0; index < 12; index++)
            {
                var x = -34f + index * 5.8f;
                var z = 20f + index % 3 * 2.4f;
                Primitive(terrain, "ForestHill_" + index, PrimitiveType.Cylinder,
                    new Vector3(x, .5f + index % 2 * .35f, z),
                    new Vector3(2.8f, 1.1f + index % 2 * .5f, 2.8f),
                    Rgb(.12f, .25f, .17f));
            }
        }

        private static void BuildSettlementHierarchy(Transform root)
        {
            foreach (var child in new[]
                     {
                         "Terrain", "Roads", "Districts", "WorldObjects", "NpcVisuals",
                         "CargoVisuals", "InteractionAnchors", "CameraAnchors",
                     })
                Child(root, child);
        }

        private static DioramaTopDownCameraRig BuildCamera(
            Transform parent,
            Transform worldMapRoot,
            Transform settlementRoot)
        {
            var mapAnchors = Required(worldMapRoot, "CameraAnchors");
            var settlementAnchors = Required(settlementRoot, "CameraAnchors");
            var mapAnchor = Child(mapAnchors, "WorldMapOverviewFocus");
            mapAnchor.position = Vector3.zero;
            var settlementAnchor = Child(settlementAnchors, "SettlementOverviewFocus");
            settlementAnchor.position = new Vector3(0f, 1.5f, 0f);
            var farmAnchor = FocusAnchor(settlementAnchors, "FarmDistrictFocus", new Vector3(-27f, 1.5f, 2f));
            var townAnchor = FocusAnchor(settlementAnchors, "TownDistrictFocus", new Vector3(-10f, 1.5f, 10f));
            var marketAnchor = FocusAnchor(settlementAnchors, "MarketDistrictFocus", new Vector3(24f, 1.5f, 5f));
            var storageAnchor = FocusAnchor(settlementAnchors, "StorageDistrictFocus", new Vector3(4f, 1.5f, -13f));
            var logisticsAnchor = FocusAnchor(settlementAnchors, "LogisticsDistrictFocus", new Vector3(9f, 1.5f, -23f));
            var residentialAnchor = FocusAnchor(settlementAnchors, "ResidentialDistrictFocus", new Vector3(-10f, 1.5f, -8f));
            var garrisonAnchor = FocusAnchor(settlementAnchors, "GarrisonDistrictFocus", new Vector3(0f, 1.5f, 20f));
            var gateAnchor = FocusAnchor(settlementAnchors, "GateDistrictFocus", new Vector3(0f, 1.5f, 31f));
            var harvestLotAnchor = FocusAnchor(settlementAnchors, "HarvestLotPotatoFocus", new Vector3(-23f, 1.2f, -2f));
            var cargoAnchor = FocusAnchor(settlementAnchors, "PotatoCargoFocus", new Vector3(14f, 1.2f, -25f));

            var playerCameraRig = Child(parent, "PlayerCameraRig");
            var cameraPivot = Child(playerCameraRig, "CameraPivot");
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(cameraPivot, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.52f, .67f, .72f);
            var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
            rig.Configure(camera, new[]
            {
                Binding(SimulationWorldShellPresenter.WorldMapFocusAnchorId,
                    DioramaCameraFocusLevelCodes.World, mapAnchor),
                Binding(SimulationWorldShellPresenter.SettlementFocusAnchorId,
                    DioramaCameraFocusLevelCodes.World, settlementAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:farm",
                    DioramaCameraFocusLevelCodes.Zone, farmAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:town",
                    DioramaCameraFocusLevelCodes.Zone, townAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:market",
                    DioramaCameraFocusLevelCodes.Zone, marketAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:storage",
                    DioramaCameraFocusLevelCodes.Zone, storageAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:logistics",
                    DioramaCameraFocusLevelCodes.Zone, logisticsAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:residential",
                    DioramaCameraFocusLevelCodes.Zone, residentialAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:garrison",
                    DioramaCameraFocusLevelCodes.Zone, garrisonAnchor),
                Binding(SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + "district:gate",
                    DioramaCameraFocusLevelCodes.Zone, gateAnchor),
                Binding(SimulationWorldShellPresenter.ObjectFocusAnchorPrefix + "harvest-lot:potato-001",
                    DioramaCameraFocusLevelCodes.Object, harvestLotAnchor),
                Binding(SimulationWorldShellPresenter.ObjectFocusAnchorPrefix + 물류이동Fixture.CargoStableId,
                    DioramaCameraFocusLevelCodes.Object, cargoAnchor),
            }, SimulationWorldShellPresenter.WorldMapFocusAnchorId);
            rig.ConfigureComposition(50f, 98f, 30f, 18f, 34f, 30f, 28f, 110f);
            rig.ConfigureInteractionLimits(35f, 75f, 12f, 110f);
            rig.Initialize();
            var controller = playerCameraRig.gameObject.AddComponent<전략카메라Controller>();
            controller.Configure(
                rig,
                cameraPivot,
                camera,
                new Vector2(-65f, -50f),
                new Vector2(65f, 50f),
                12f,
                110f);
            return rig;
        }

        private static void UpgradePlayerCameraHierarchy(
            Transform cameraSystem,
            DioramaTopDownCameraRig rig)
        {
            var camera = rig.GetComponent<Camera>()
                ?? throw new InvalidOperationException("SimulationWorldShellCameraMissing");
            var playerCameraRig = cameraSystem.Find("PlayerCameraRig")
                ?? Child(cameraSystem, "PlayerCameraRig");
            var cameraPivot = playerCameraRig.Find("CameraPivot")
                ?? Child(playerCameraRig, "CameraPivot");
            camera.transform.SetParent(cameraPivot, true);
            camera.gameObject.name = "Main Camera";
            camera.gameObject.tag = "MainCamera";

            rig.SetPrototypeInputEnabled(false);
            rig.ConfigureInteractionLimits(35f, 75f, 12f, 110f);
            rig.Initialize();
            var controller = playerCameraRig.GetComponent<전략카메라Controller>()
                ?? playerCameraRig.gameObject.AddComponent<전략카메라Controller>();
            controller.Configure(
                rig,
                cameraPivot,
                camera,
                new Vector2(-65f, -50f),
                new Vector2(65f, 50f),
                12f,
                110f);
            controller.BindShellPresenter(FindPresenter());
        }

        private static void ValidatePlayerStrategyCameraOpenScene()
        {
            var root = Required(RootName);
            var playerCameraRig = Required(Required(root.transform, "CameraSystem"), "PlayerCameraRig");
            var cameraPivot = Required(playerCameraRig, "CameraPivot");
            var camera = Required(cameraPivot, "Main Camera").GetComponent<Camera>();
            var controller = playerCameraRig.GetComponent<전략카메라Controller>();
            if (camera == null || controller == null)
                throw new InvalidOperationException("SimulationWorldShellStrategyCameraMissing");
            controller.ValidateConfiguration();
            if (UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>() == null
                || UnityEngine.Object.FindFirstObjectByType<StandaloneInputModule>() != null)
                throw new InvalidOperationException("SimulationWorldShellInputSystemConfigurationInvalid");
        }

        private static Light BuildLighting(Transform parent)
        {
            var light = new GameObject("GlobalDirectionalLight").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Directional;
            light.color = new Color(1f, .91f, .76f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            RenderSettings.ambientLight = new Color(.42f, .47f, .45f);
            return light;
        }

        private static HudReferences BuildHud(Transform parent, Camera worldCamera)
        {
            var canvas = new GameObject("SimulationWorldHud",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(parent, false);
            var canvasComponent = canvas.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceCamera;
            canvasComponent.worldCamera = worldCamera;
            canvasComponent.planeDistance = 1f;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            var top = Panel(canvas.transform, "WorldStatusPanel",
                new Vector2(22f, -22f), new Vector2(450f, 276f), new Vector2(0f, 1f));
            var mode = Text(top.transform, "ModeText", new Vector2(18f, -14f),
                new Vector2(414f, 52f), 19, TextAnchor.UpperLeft, new Color(.94f, .77f, .35f));
            var identity = Text(top.transform, "IdentityText", new Vector2(18f, -70f),
                new Vector2(414f, 56f), 15, TextAnchor.UpperLeft, Color.white);
            var economy = Text(top.transform, "EconomyText", new Vector2(18f, -130f),
                new Vector2(414f, 132f), 17, TextAnchor.UpperLeft, Color.white);

            var navigation = Panel(canvas.transform, "NavigationPanel",
                new Vector2(22f, 22f), new Vector2(760f, 132f), new Vector2(0f, 0f));
            var selection = Text(navigation.transform, "SelectionText", new Vector2(18f, -12f),
                new Vector2(724f, 68f), 15, TextAnchor.UpperLeft, Color.white);
            var worldMap = Button(navigation.transform, "WorldMapButton", "WORLD MAP",
                new Vector2(18f, -87f), new Vector2(132f, 34f), new Color(.18f, .48f, .63f));
            var settlement = Button(navigation.transform, "SettlementButton", "정착지 내부",
                new Vector2(158f, -87f), new Vector2(132f, 34f), new Color(.72f, .48f, .18f));
            var back = Button(navigation.transform, "BackButton", "뒤로",
                new Vector2(298f, -87f), new Vector2(92f, 34f), new Color(.38f, .4f, .42f));
            var pause = Button(navigation.transform, "PauseButton", "PAUSE · 미연결",
                new Vector2(438f, -87f), new Vector2(124f, 34f), new Color(.25f, .28f, .31f));
            var speed = Button(navigation.transform, "SpeedButton", "1× · 미연결",
                new Vector2(570f, -87f), new Vector2(124f, 34f), new Color(.25f, .28f, .31f));

            return new HudReferences(canvas.transform, mode, identity, economy, selection,
                worldMap, settlement, back, pause, speed);
        }

        private static void Build정착지상호작용(
            Transform canvas,
            SimulationWorldShellPresenter shell,
            Transform runtimeRoot)
        {
            var card = Panel(canvas, "HarvestDispositionCard",
                new Vector2(-22f, -22f), new Vector2(520f, 570f), new Vector2(1f, 1f));
            card.GetComponent<Image>().color = new Color(.045f, .055f, .065f, .96f);
            var title = Text(card.transform, "LotText", new Vector2(22f, -18f),
                new Vector2(476f, 62f), 20, TextAnchor.UpperLeft, new Color(.96f, .78f, .31f));
            var phase = Text(card.transform, "PhaseText", new Vector2(22f, -84f),
                new Vector2(476f, 48f), 15, TextAnchor.UpperLeft, new Color(.56f, .82f, .94f));
            var preview = Text(card.transform, "PreviewText", new Vector2(22f, -138f),
                new Vector2(476f, 196f), 17, TextAnchor.UpperLeft, Color.white);
            var cooperative = Button(card.transform, "CooperativeButton", "생산자 조합 출하",
                new Vector2(22f, -344f), new Vector2(230f, 42f), new Color(.30f, .52f, .30f));
            var direct = Button(card.transform, "DirectButton", "온라인 직접 판매",
                new Vector2(268f, -344f), new Vector2(230f, 42f), new Color(.31f, .48f, .68f));
            var storage = Button(card.transform, "StorageButton", "비축 보관",
                new Vector2(22f, -394f), new Vector2(230f, 42f), new Color(.59f, .44f, .22f));
            var export = Button(card.transform, "ExportButton", "외부 교역 준비",
                new Vector2(268f, -394f), new Vector2(230f, 42f), new Color(.48f, .36f, .62f));
            var confirm = Button(card.transform, "ConfirmButton", "CONFIRM · capacity 예약",
                new Vector2(22f, -458f), new Vector2(230f, 48f), new Color(.78f, .49f, .13f));
            var advance = Button(card.transform, "AdvanceButton", "WORLD TICK",
                new Vector2(268f, -458f), new Vector2(230f, 48f), new Color(.18f, .58f, .61f));
            var boundary = Text(card.transform, "BoundaryText", new Vector2(22f, -518f),
                new Vector2(476f, 34f), 13, TextAnchor.UpperLeft, new Color(.72f, .74f, .76f));
            boundary.text = "SIMULATION ONLY · 실제 판매·배송·수출·정산을 실행하지 않음";

            var presenter = runtimeRoot.gameObject.AddComponent<정착지상호작용Presenter>();
            presenter.Configure(shell, card, title, phase, preview,
                cooperative, direct, storage, export, confirm, advance);
            card.SetActive(false);
        }

        private static void Build물류이동(
            Transform canvas,
            SimulationWorldShellPresenter shell,
            Transform runtimeRoot)
        {
            var card = Panel(canvas, "LogisticsMovementCard",
                new Vector2(-22f, -22f), new Vector2(520f, 500f), new Vector2(1f, 1f));
            card.GetComponent<Image>().color = new Color(.035f, .065f, .075f, .97f);
            var cargo = Text(card.transform, "CargoText", new Vector2(22f, -18f),
                new Vector2(476f, 62f), 20, TextAnchor.UpperLeft, new Color(.47f, .88f, .92f));
            var phase = Text(card.transform, "PhaseText", new Vector2(22f, -84f),
                new Vector2(476f, 42f), 15, TextAnchor.UpperLeft, new Color(.96f, .75f, .32f));
            var detail = Text(card.transform, "DetailText", new Vector2(22f, -132f),
                new Vector2(476f, 230f), 16, TextAnchor.UpperLeft, Color.white);
            var preview = Button(card.transform, "PreviewButton", "배차 미리보기",
                new Vector2(22f, -374f), new Vector2(148f, 48f), new Color(.27f, .52f, .62f));
            var confirm = Button(card.transform, "ConfirmButton", "추천 기사 확정",
                new Vector2(180f, -374f), new Vector2(154f, 48f), new Color(.76f, .48f, .14f));
            var tick = Button(card.transform, "TickButton", "하루 진행 +1",
                new Vector2(344f, -374f), new Vector2(154f, 48f), new Color(.20f, .60f, .52f));
            var boundary = Text(card.transform, "BoundaryText", new Vector2(22f, -438f),
                new Vector2(476f, 42f), 13, TextAnchor.UpperLeft, new Color(.72f, .76f, .78f));
            boundary.text = "시뮬레이션 전용 · 실제 기사 호출 없음 · 도착 후 인수 전에는 목적지 재고 아님";
            var presenter = runtimeRoot.gameObject.AddComponent<물류이동Presenter>();
            presenter.Configure(shell, card, cargo, phase, detail, preview, confirm, tick);
            card.SetActive(false);
        }

        private static void Build턴마감(
            Transform canvas,
            SimulationWorldShellPresenter shell,
            Transform runtimeRoot)
        {
            var panel = Panel(canvas, "TurnClosingPanel",
                new Vector2(-22f, 22f), new Vector2(540f, 382f), new Vector2(1f, 0f));
            panel.GetComponent<Image>().color = new Color(.055f, .045f, .075f, .97f);
            var title = Text(panel.transform, "TitleText", new Vector2(22f, -18f),
                new Vector2(496f, 40f), 21, TextAnchor.UpperLeft, new Color(.96f, .79f, .36f));
            var card = Text(panel.transform, "CardText", new Vector2(22f, -64f),
                new Vector2(496f, 128f), 15, TextAnchor.UpperLeft, Color.white);
            var status = Text(panel.transform, "StatusText", new Vector2(22f, -196f),
                new Vector2(496f, 50f), 14, TextAnchor.UpperLeft, new Color(.66f, .84f, .94f));
            var noCard = Button(panel.transform, "NoCardButton", "카드 없이",
                new Vector2(22f, -252f), new Vector2(100f, 38f), new Color(.32f, .34f, .40f));
            var fool = Button(panel.transform, "FoolCardButton", "바보 · 모를 뿐",
                new Vector2(130f, -252f), new Vector2(124f, 38f), new Color(.24f, .43f, .63f));
            var chariot = Button(panel.transform, "ChariotCardButton", "전차 · 통합 정진",
                new Vector2(262f, -252f), new Vector2(142f, 38f), new Color(.48f, .31f, .60f));
            var culture = Button(panel.transform, "CultureCardButton", "문화 · 서울 질문",
                new Vector2(412f, -252f), new Vector2(106f, 38f), new Color(.24f, .52f, .42f));
            var preview = Button(panel.transform, "PreviewButton", "턴 마감 PREVIEW",
                new Vector2(22f, -302f), new Vector2(240f, 42f), new Color(.27f, .52f, .62f));
            var confirm = Button(panel.transform, "ConfirmButton", "CONFIRM · 다음 날",
                new Vector2(278f, -302f), new Vector2(240f, 42f), new Color(.78f, .49f, .13f));
            var highlighter = runtimeRoot.gameObject.AddComponent<타로객체강조Presenter>();
            var presenter = runtimeRoot.gameObject.AddComponent<턴마감Presenter>();
            presenter.Configure(shell, panel, title, card, status,
                noCard, fool, chariot, culture, preview, confirm, highlighter);
            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset")
                ?? throw new InvalidOperationException("UnityClientRuntimeSettingsMissing");
            var composition = runtimeRoot.gameObject.AddComponent<턴마감SceneCompositionRoot>();
            composition.Configure(settings, shell, presenter, true);
            presenter.SetContextVisible(false);
        }

        private static void Build진부Hub입고Ui(
            Transform canvas,
            SimulationWorldShellPresenter shell,
            Transform runtimeRoot)
        {
            var theme = EnsureFigmaMauiWarehouseUiTheme();
            var panel = Panel(canvas, "JinbuInboundPanel",
                new Vector2(-22f, 22f), new Vector2(560f, 820f), new Vector2(1f, 0f));
            var panelSurface = panel.GetComponent<Image>();
            panelSurface.color = theme.Background;
            var accent = Panel(panel.transform, "WarehouseRoleAccent",
                Vector2.zero, new Vector2(560f, 7f), new Vector2(0f, 1f));
            accent.GetComponent<Image>().color = theme.WarehouseAccent;

            var context = Text(panel.transform, "ContextText", new Vector2(22f, -18f),
                new Vector2(390f, 24f), 14, TextAnchor.UpperLeft, theme.Muted);
            var badge = Panel(panel.transform, "StateBadge",
                new Vector2(418f, -14f), new Vector2(120f, 30f), new Vector2(0f, 1f));
            var state = Text(badge.transform, "StateText", Vector2.zero,
                new Vector2(120f, 30f), 14, TextAnchor.MiddleCenter, Color.white);
            state.rectTransform.anchorMin = Vector2.zero;
            state.rectTransform.anchorMax = Vector2.one;
            state.rectTransform.pivot = new Vector2(.5f, .5f);
            state.rectTransform.anchoredPosition = Vector2.zero;
            state.rectTransform.sizeDelta = Vector2.zero;

            var title = Text(panel.transform, "TitleText", new Vector2(22f, -62f),
                new Vector2(516f, 42f), 25, TextAnchor.UpperLeft, theme.Text);
            var summaryCard = Panel(panel.transform, "SummaryCard",
                new Vector2(22f, -112f), new Vector2(516f, 102f), new Vector2(0f, 1f));
            summaryCard.GetComponent<Image>().color = theme.Surface;
            var summary = Text(summaryCard.transform, "SummaryText", new Vector2(18f, -16f),
                new Vector2(480f, 70f), 18, TextAnchor.UpperLeft, theme.Text);
            var workflow = Text(panel.transform, "WorkflowText", new Vector2(22f, -228f),
                new Vector2(516f, 32f), 15, TextAnchor.MiddleLeft, theme.WarehouseAccent);

            var detailCard = Panel(panel.transform, "DetailCard",
                new Vector2(22f, -270f), new Vector2(516f, 226f), new Vector2(0f, 1f));
            detailCard.GetComponent<Image>().color = theme.Surface;
            var detail = Text(detailCard.transform, "DetailText", new Vector2(18f, -14f),
                new Vector2(480f, 198f), 15, TextAnchor.UpperLeft, theme.Text);
            var previewCard = Panel(panel.transform, "PreviewConfirmCard",
                new Vector2(22f, -510f), new Vector2(516f, 114f), new Vector2(0f, 1f));
            previewCard.GetComponent<Image>().color = theme.WarehouseAccentSoft;
            var preview = Text(previewCard.transform, "PreviewText", new Vector2(16f, -12f),
                new Vector2(484f, 90f), 14, TextAnchor.UpperLeft, theme.Text);

            var stale = Panel(panel.transform, "StaleBanner",
                new Vector2(22f, -636f), new Vector2(516f, 42f), new Vector2(0f, 1f));
            stale.GetComponent<Image>().color = new Color(.42f, .22f, .08f, .96f);
            var staleText = Text(stale.transform, "StaleText", new Vector2(12f, -6f),
                new Vector2(492f, 30f), 13, TextAnchor.MiddleLeft, Color.white);

            var previewButton = Button(panel.transform, "PreviewButton", "입고 검수 미리보기",
                new Vector2(22f, -690f), new Vector2(242f, 46f), theme.Preview);
            var confirmButton = Button(panel.transform, "ConfirmButton", "입고 검수 확정",
                new Vector2(276f, -690f), new Vector2(262f, 46f), theme.WarehouseAccent);
            var tickButton = Button(panel.transform, "TickButton", "WorldTick +1",
                new Vector2(22f, -746f), new Vector2(242f, 42f), theme.Success);
            var refreshButton = Button(panel.transform, "RefreshButton", "상태 다시 불러오기",
                new Vector2(276f, -746f), new Vector2(262f, 42f), theme.Muted);
            var boundary = Text(panel.transform, "BoundaryText", new Vector2(22f, -794f),
                new Vector2(516f, 22f), 12, TextAnchor.UpperLeft, theme.Muted);

            var presenter = runtimeRoot.gameObject.AddComponent<진부Hub입고UiPresenter>();
            presenter.Configure(shell, theme, panel, panelSurface, accent.GetComponent<Image>(),
                context, badge.GetComponent<Image>(), state, title, summary, workflow, detail,
                preview, boundary, stale, staleText, previewButton, confirmButton, tickButton,
                refreshButton, "district:logistics");
            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset")
                ?? throw new InvalidOperationException("UnityClientRuntimeSettingsMissing");
            var composition = runtimeRoot.gameObject.AddComponent<진부Hub입고UiSceneCompositionRoot>();
            composition.Configure(settings, shell, presenter, true);
            stale.SetActive(false);
            panel.SetActive(false);
        }

        private static void Build통합월드ModeNavigation(
            Transform persistentUi,
            Transform runtimeRoot,
            SimulationWorldShellPresenter shell,
            플레이어경관Controller player,
            진부Hub입고UiPresenter inbound)
        {
            var canvas = new GameObject("UnifiedWorldModeCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(persistentUi, false);
            var canvasComponent = canvas.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 200;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);

            var bar = Panel(canvas.transform, "UnifiedWorldModeBar",
                new Vector2(306f, -16f), new Vector2(340f, 52f), new Vector2(0f, 1f));
            bar.GetComponent<Image>().color = new Color(.04f, .055f, .06f, .94f);
            var world = Button(bar.transform, "WorldOverviewButton", "월드",
                new Vector2(8f, -8f), new Vector2(76f, 36f), Rgb(.94f, .42f, .05f));
            var firstPerson = Button(bar.transform, "FarmFirstPersonButton", "농장 1인칭",
                new Vector2(90f, -8f), new Vector2(78f, 36f), Rgb(.15f, .19f, .20f));
            var tactical = Button(bar.transform, "FarmTacticalButton", "농장 경영",
                new Vector2(174f, -8f), new Vector2(74f, 36f), Rgb(.15f, .19f, .20f));
            var inboundButton = Button(bar.transform, "JinbuInboundButton", "진부 입고",
                new Vector2(254f, -8f), new Vector2(78f, 36f), Rgb(.15f, .19f, .20f));
            foreach (var label in bar.GetComponentsInChildren<Text>(true))
                label.fontSize = 12;

            Build농장경영시점(runtimeRoot, player);
            var presenter = runtimeRoot.gameObject.AddComponent<통합월드ModePresenter>();
            presenter.Configure(
                shell, player, inbound, world, firstPerson, tactical, inboundButton);
        }

        private static void Build농장경영시점(
            Transform runtimeRoot,
            플레이어경관Controller player)
        {
            var previous = runtimeRoot.GetComponent<농장경영시점Controller>();
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);

            var shellRoot = Required(RootName).transform;
            var farm = Required(shellRoot,
                "SettlementInteriorRoot/Districts/FarmDistrict");
            foreach (var oldHighlight in Enumerable.Range(0, farm.childCount)
                         .Select(index => farm.GetChild(index))
                         .Where(value => value.name.StartsWith(
                             "FarmManagementHighlight_", StringComparison.Ordinal))
                         .ToArray())
                UnityEngine.Object.DestroyImmediate(oldHighlight.gameObject);

            var targets = new List<농장경영선택대상View>();
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 5; column++)
            {
                var plot = Required(farm, $"FarmPlot_{row}_{column}");
                foreach (var oldView in plot.GetComponents<농장경영선택대상View>())
                    UnityEngine.Object.DestroyImmediate(oldView);
                if (plot.GetComponent<Collider>() == null)
                    plot.gameObject.AddComponent<BoxCollider>();
                var highlight = Primitive(farm,
                    $"FarmManagementHighlight_{row}_{column}",
                    PrimitiveType.Cylinder,
                    plot.localPosition + Vector3.up * .24f,
                    new Vector3(plot.localScale.x * .54f, .025f,
                        plot.localScale.z * .54f),
                    new Color(1f, .54f, .08f, 1f));
                highlight.gameObject.SetActive(false);
                var view = plot.gameObject.AddComponent<농장경영선택대상View>();
                view.Configure(
                    $"farm-plot:pyeongchang:{row}:{column}",
                    $"감자밭 {row + 1}-{column + 1}",
                    농장경영대상종류Codes.Plot,
                    농장경영작업Codes.All,
                    highlight.gameObject);
                EditorUtility.SetDirty(view);
                targets.Add(view);
            }

            var harvestLot = Required(farm, "HarvestLot_Potato_001");
            foreach (var oldHarvestView in harvestLot
                         .GetComponents<농장경영선택대상View>())
                UnityEngine.Object.DestroyImmediate(oldHarvestView);
            if (harvestLot.GetComponent<Collider>() == null)
                harvestLot.gameObject.AddComponent<BoxCollider>();
            var harvestHighlight = Primitive(farm,
                "FarmManagementHighlight_HarvestLot",
                PrimitiveType.Cylinder,
                harvestLot.localPosition + Vector3.up * 1.08f,
                new Vector3(1.7f, .025f, 1.7f),
                new Color(.1f, .82f, .72f, 1f));
            harvestHighlight.gameObject.SetActive(false);
            var harvestView = harvestLot.gameObject
                .AddComponent<농장경영선택대상View>();
            harvestView.Configure(
                "farm-harvest-lot:pyeongchang:potato:001",
                "감자 수확 마당",
                농장경영대상종류Codes.Facility,
                new[] { 농장경영작업Codes.Harvest },
                harvestHighlight.gameObject);
            EditorUtility.SetDirty(harvestView);
            targets.Add(harvestView);

            var management = runtimeRoot.gameObject
                .AddComponent<농장경영시점Controller>();
            management.Configure(player, targets);
            player.ConfigureFarmManagement(management);

            var previousDailyPresenter = runtimeRoot.GetComponent<오늘작업계획Presenter>();
            if (previousDailyPresenter != null)
                UnityEngine.Object.DestroyImmediate(previousDailyPresenter);
            var previousDailyComposition = runtimeRoot
                .GetComponent<오늘작업계획SceneCompositionRoot>();
            if (previousDailyComposition != null)
                UnityEngine.Object.DestroyImmediate(previousDailyComposition);
            var dailyPresenter = runtimeRoot.gameObject
                .AddComponent<오늘작업계획Presenter>();
            dailyPresenter.Configure(management, player,
                runtimeRoot.GetComponent<턴마감Presenter>());
            var dailySettings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset")
                ?? throw new InvalidOperationException("UnityClientRuntimeSettingsMissing");
            var dailyComposition = runtimeRoot.gameObject
                .AddComponent<오늘작업계획SceneCompositionRoot>();
            dailyComposition.Configure(dailySettings, dailyPresenter, true);

            var previousCombat = runtimeRoot.GetComponent<전투시점Controller>();
            if (previousCombat != null) UnityEngine.Object.DestroyImmediate(previousCombat);
            var combat = runtimeRoot.gameObject.AddComponent<전투시점Controller>();
            combat.Configure(player);
            var combatInput = runtimeRoot.GetComponent<전투입력Adapter>()
                ?? runtimeRoot.gameObject.AddComponent<전투입력Adapter>();
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                InputActionAssetPath)
                ?? throw new InvalidOperationException("FarmCombatInputActionsMissing");
            combatInput.Configure(inputActions);
            combat.ConfigureInput(combatInput);
            var tacticalSquads = Build전술분대Views(runtimeRoot);
            combat.ConfigureTacticalSquads(tacticalSquads);
            player.ConfigureCombat(combat);

            var previousComposition = runtimeRoot
                .GetComponent<농장전투CompositionRoot>();
            if (previousComposition != null)
                UnityEngine.Object.DestroyImmediate(previousComposition);
            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset")
                ?? throw new InvalidOperationException("UnityClientRuntimeSettingsMissing");
            var composition = runtimeRoot.gameObject
                .AddComponent<농장전투CompositionRoot>();
            composition.Configure(settings, FindPresenter(), player, combat,
                "actor:sim:player-survivor", true);
        }

        private static 전술분대Presenter Build전술분대Views(Transform runtimeRoot)
        {
            var previous = runtimeRoot.GetComponent<전술분대Presenter>();
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            var shellRoot = Required(RootName).transform;
            var farm = Required(shellRoot,
                "SettlementInteriorRoot/Districts/FarmDistrict");
            var oldRoot = farm.Find("TacticalBattleRoot");
            if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

            var battleRoot = Child(farm, "TacticalBattleRoot");
            var navigationRoot = Child(battleRoot, "NavigationRoot");
            var districtSurface = Required(farm, "DistrictSurface")
                .GetComponent<Renderer>();
            var groundTopLocalY = navigationRoot.InverseTransformPoint(
                new Vector3(navigationRoot.position.x,
                    districtSurface.bounds.max.y,
                    navigationRoot.position.z)).y;
            var floor = Primitive(navigationRoot, "TacticalWalkableFloor",
                PrimitiveType.Cube,
                new Vector3(0f,
                    groundTopLocalY - TacticalNavigationFloorThickness * .5f,
                    2f),
                new Vector3(26f, TacticalNavigationFloorThickness, 26f),
                Color.clear);
            floor.gameObject.layer = 31; // 전술 NavMesh 전용 비공개 레이어
            floor.gameObject.AddComponent<BoxCollider>();
            var floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            floorRenderer.receiveShadows = false;
            var surface = navigationRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1 << floor.gameObject.layer;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            // NavMesh 생성에는 Renderer가 필요하지만 저장 Scene에서는
            // 실제 지형을 덮지 않도록 계산 직후 완전히 숨긴다.
            floorRenderer.enabled = false;

            var anchors = Child(battleRoot, "PositionAnchors");
            var alliedInner = TacticalAnchor(anchors, "Allied_InnerFarm",
                new Vector3(-2.2f, groundTopLocalY, -6.5f));
            var alliedPerimeter = TacticalAnchor(anchors, "Allied_Perimeter",
                new Vector3(-1.4f, groundTopLocalY, 1.5f));
            var alliedForward = TacticalAnchor(anchors, "Allied_Forward",
                new Vector3(-.9f, groundTopLocalY, 5.8f));
            var hostileInner = TacticalAnchor(anchors, "Hostile_InnerFarm",
                new Vector3(2.4f, groundTopLocalY, 12.5f));
            var hostilePerimeter = TacticalAnchor(anchors, "Hostile_Perimeter",
                new Vector3(1.8f, groundTopLocalY, 10.2f));
            var hostileForward = TacticalAnchor(anchors, "Hostile_Forward",
                new Vector3(1.2f, groundTopLocalY, 7.8f));

            var catalog = EnsureTacticalCharacterVisualCatalog();
            var allied = BuildTacticalSquad(battleRoot, catalog,
                FarmCombatPresentationCodes.Allied, alliedInner,
                alliedPerimeter, alliedForward, 3.4f);
            var hostile = BuildTacticalSquad(battleRoot, catalog,
                FarmCombatPresentationCodes.Hostile, hostileInner,
                hostilePerimeter, hostileForward, 2.2f);
            var presenter = runtimeRoot.gameObject.AddComponent<전술분대Presenter>();
            presenter.Configure(farm.gameObject, battleRoot.gameObject, surface,
                new[] { allied, hostile });
            battleRoot.gameObject.SetActive(false);
            return presenter;
        }

        private static 전술분대대형Controller BuildTacticalSquad(
            Transform parent,
            전술CharacterVisualCatalog catalog,
            string sideCode,
            Transform innerFarm,
            Transform perimeter,
            Transform forward,
            float speed)
        {
            var root = Child(parent, sideCode + "SquadRoot");
            root.position = perimeter.position;
            var agent = root.gameObject.AddComponent<NavMeshAgent>();
            agent.radius = .62f;
            agent.height = 1.8f;
            agent.speed = speed;
            agent.acceleration = 14f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = .12f;
            agent.autoBraking = true;
            agent.updateRotation = false;
            if (NavMesh.SamplePosition(root.position, out var hit, 2f,
                    NavMesh.AllAreas))
                agent.Warp(hit.position);

            var members = new List<전술분대원View>();
            for (var index = 0; index < 6; index++)
            {
                var stableId = "tactical-squad:fixture:"
                    + sideCode.ToLowerInvariant() + ":member:"
                    + (index + 1).ToString("D2");
                var memberRoot = Child(root, "Member_" + (index + 1).ToString("D2"));
                memberRoot.localPosition = 전술분대대형Controller.CalculateSlot(
                    FarmCombatPresentationCodes.LineFormation, index);
                var entry = catalog.Resolve(sideCode, stableId);
                var visual = UnityEngine.Object.Instantiate(entry.Prefab, memberRoot);
                visual.name = "VisualRoot_" + entry.VisualKey;
                visual.transform.SetLocalPositionAndRotation(
                    Vector3.zero, Quaternion.identity);
                캐릭터지면정렬Utility.AlignFeetToGround(
                    visual.transform, memberRoot);
                var animator = visual.GetComponentInChildren<Animator>(true)
                    ?? throw new InvalidOperationException(
                        "TacticalCharacterAnimatorMissing:" + entry.VisualKey);
                var animationEntry = new 공용AnimationCatalogEntry();
                animationEntry.Configure(entry.AnimationPackCode,
                    "tactical-unit", "locomotion.idle.v1",
                    "locomotion.walk.v1",
                    공용AnimationSourceKindCodes.ProceduralFallback,
                    "humanoid.procedural-locomotion.v1", entry.Prefab,
                    null, null);
                var adapter = memberRoot.gameObject.AddComponent<공용AnimationAdapter>();
                adapter.Configure(animationEntry, animator);
                var view = memberRoot.gameObject.AddComponent<전술분대원View>();
                view.Configure(stableId, index, adapter);
                members.Add(view);
            }
            var controller = root.gameObject.AddComponent<전술분대대형Controller>();
            controller.Configure(sideCode, innerFarm, perimeter, forward,
                agent, members.ToArray());
            return controller;
        }

        private static Transform TacticalAnchor(Transform parent,
            string name, Vector3 localPosition)
        {
            var anchor = Child(parent, name);
            anchor.localPosition = localPosition;
            return anchor;
        }

        private static 전술CharacterVisualCatalog EnsureTacticalCharacterVisualCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<전술CharacterVisualCatalog>(
                TacticalCharacterVisualCatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(
                    TacticalCharacterVisualCatalogPath)!);
                catalog = ScriptableObject.CreateInstance<전술CharacterVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, TacticalCharacterVisualCatalogPath);
            }
            전술CharacterVisualCatalogEntry Entry(string key, string side,
                string pack, string prefabPath, int weight)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    ?? throw new InvalidOperationException(
                        "TacticalCharacterPrefabMissing:" + prefabPath);
                var entry = new 전술CharacterVisualCatalogEntry();
                entry.Configure(key, side, pack, prefab, weight);
                return entry;
            }
            catalog.Configure("pyeongchang-tactical-character.r1", new[]
            {
                Entry("character.tactical.allied.farmer-male",
                    FarmCombatPresentationCodes.Allied,
                    월드CompositionPackCodes.Farm,
                    "Assets/Synty/PolygonFarm/Prefabs/Characters/SM_Chr_Farmer_Male_01.prefab", 2),
                Entry("character.tactical.allied.farmer-female",
                    FarmCombatPresentationCodes.Allied,
                    월드CompositionPackCodes.Farm,
                    "Assets/Synty/PolygonFarm/Prefabs/Characters/SM_Chr_Farmer_Female_01.prefab", 2),
                Entry("character.tactical.allied.farmer-senior",
                    FarmCombatPresentationCodes.Allied,
                    월드CompositionPackCodes.Farm,
                    "Assets/Synty/PolygonFarm/Prefabs/Characters/SM_Chr_Farmer_Male_Old_01.prefab", 1),
                Entry("character.tactical.hostile.charred",
                    FarmCombatPresentationCodes.Hostile,
                    월드CompositionPackCodes.Mixed,
                    "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Charred_01.prefab", 3),
                Entry("character.tactical.hostile.skeleton",
                    FarmCombatPresentationCodes.Hostile,
                    월드CompositionPackCodes.Mixed,
                    "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Skeleton_01.prefab", 1),
            });
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static FigmaMauiWarehouseUiThemeCatalog EnsureFigmaMauiWarehouseUiTheme()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FigmaMauiWarehouseUiThemeCatalog>(
                FigmaMauiWarehouseUiThemePath);
            if (catalog != null) return catalog;
            Directory.CreateDirectory(Path.GetDirectoryName(FigmaMauiWarehouseUiThemePath)!);
            catalog = ScriptableObject.CreateInstance<FigmaMauiWarehouseUiThemeCatalog>();
            AssetDatabase.CreateAsset(catalog, FigmaMauiWarehouseUiThemePath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void BuildSettlementTerrain(Transform parent)
        {
            Primitive(parent, "SettlementGround", PrimitiveType.Cube,
                new Vector3(0f, -.45f, 0f), new Vector3(76f, .8f, 64f), Rgb(.32f, .42f, .27f));
            for (var index = 0; index < 14; index++)
            {
                var x = -34f + index * 5.2f;
                var z = 28f - index % 3 * 2.2f;
                Primitive(parent, "ForestBoundary_" + index, PrimitiveType.Cylinder,
                    new Vector3(x, .7f, z), new Vector3(2.6f, 1.5f, 2.6f), Rgb(.13f, .27f, .15f));
            }
        }

        private static void BuildSettlementRoads(Transform parent)
        {
            Route(parent, "Road_Farm_Center", new Vector3(-28f, .05f, 2f), Vector3.zero, 2.2f);
            Route(parent, "Road_Center_Market", Vector3.zero, new Vector3(25f, .05f, 5f), 2.2f);
            Route(parent, "Road_Center_Residential", Vector3.zero, new Vector3(-8f, .05f, -8f), 2f);
            Route(parent, "Road_Residential_Storage", new Vector3(-8f, .05f, -8f), new Vector3(4f, .05f, -13f), 2f);
            Route(parent, "Road_Storage_Logistics", new Vector3(4f, .05f, -13f), new Vector3(9f, .05f, -23f), 2.4f);
            Route(parent, "Road_Center_Gate", Vector3.zero, new Vector3(0f, .05f, 30f), 2.4f);
            Primitive(parent, "SettlementCenter", PrimitiveType.Cylinder,
                new Vector3(0f, .12f, 0f), new Vector3(7f, .18f, 7f), Rgb(.58f, .52f, .39f));
        }

        private static void BuildSettlementVisualBase(Transform settlementRoot)
        {
            var farmCatalog = LoadCatalog(CityFarmSyntyWorldBuilder.FarmCatalogPath);
            var urbanCatalog = LoadCatalog(CityFarmSyntyWorldBuilder.UrbanCatalogPath);
            var environmentCatalog = LoadCatalog(EnvironmentCatalogPath);
            farmCatalog.Validate();
            urbanCatalog.Validate();
            environmentCatalog.Validate();

            var terrain = Required(settlementRoot, "Terrain");
            var landscape = RebuildRoot(terrain, "VisualBaseRoot");
            foreach (var boundary in Enumerable.Range(0, 14)
                         .Select(index => terrain.Find("ForestBoundary_" + index))
                         .Where(value => value != null))
                SetRendererVisible(boundary!, false);

            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmHillA,
                "BackgroundHillWest", new Vector3(-29f, 0f, 29f), new Vector3(0f, 18f, 0f), 1.35f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmHillB,
                "BackgroundHillCenter", new Vector3(-8f, 0f, 31f), new Vector3(0f, -12f, 0f), 1.15f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmHillC,
                "BackgroundHillEast", new Vector3(23f, 0f, 28f), new Vector3(0f, 22f, 0f), 1.3f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmTreeClusterA,
                "TreeClusterWest", new Vector3(-35f, 0f, 17f), Vector3.zero, 1.1f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmTreeClusterB,
                "TreeClusterNorth", new Vector3(14f, 0f, 29f), new Vector3(0f, 30f, 0f), 1.05f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmTreeLarge,
                "ForegroundTreeWest", new Vector3(-35f, 0f, -17f), new Vector3(0f, -20f, 0f), 1.15f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmTreeA,
                "BoundaryTreeWest", new Vector3(-33f, 0f, -28f), Vector3.zero, 1f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmTreeB,
                "BoundaryTreeEast", new Vector3(35f, 0f, 13f), new Vector3(0f, 42f, 0f), 1f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmTreeC,
                "BoundaryTreeSouthEast", new Vector3(31f, 0f, -27f), new Vector3(0f, -25f, 0f), 1.05f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.CityTreeA,
                "CenterTreeWest", new Vector3(-5f, 0f, 2f), Vector3.zero, .9f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.CityTreeB,
                "CenterTreeEast", new Vector3(7f, 0f, 5f), new Vector3(0f, 30f, 0f), .9f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.CityTreeC,
                "MarketTree", new Vector3(17f, 0f, 10f), new Vector3(0f, -15f, 0f), .85f);
            CatalogVisual(landscape, environmentCatalog, EnvironmentVisualKeys.FarmFlowersA,
                "FarmRoadFlowers", new Vector3(-17f, .05f, 5f), Vector3.zero, 1.1f);

            var districts = Required(settlementRoot, "Districts");
            var farm = Required(districts, "FarmDistrict");
            var farmVisuals = RebuildRoot(farm, "VisualBaseRoot");
            SetRendererVisible(Required(farm, "FarmBarn"), false);
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 5; column++)
            {
                var position = new Vector3(-5.5f + column * 2.7f, .3f, -2.3f + row * 4.6f);
                CatalogVisual(farmVisuals, farmCatalog, FarmVisualKeys.SoilRows,
                    $"SoilRows_{row}_{column}", position, Vector3.zero, 1.25f);
                CatalogVisual(farmVisuals, farmCatalog, FarmVisualKeys.PotatoLarge,
                    $"PotatoCrop_{row}_{column}", position + Vector3.up * .12f,
                    new Vector3(0f, (row * 71 + column * 37) % 360, 0f), 2.1f);
            }
            CatalogVisual(farmVisuals, farmCatalog, FarmVisualKeys.Barn,
                "FarmBarnVisual", new Vector3(5.3f, .2f, 5f), new Vector3(0f, 16f, 0f), 1.15f);
            CatalogVisual(farmVisuals, farmCatalog, FarmVisualKeys.Silo,
                "FarmSiloVisual", new Vector3(-7f, .2f, 5.4f), Vector3.zero, 1.05f);
            var harvestLot = Required(farm, "HarvestLot_Potato_001");
            SetRendererVisible(harvestLot, false);
            var placementRoot = RebuildRoot(farm, "SceneObjectPlacements");
            var potatoBox = SeedbedObjectPlacement(
                placementRoot,
                "seedbed-object:farm.potato-harvest-box.a",
                PotatoHarvestBoxPlacementStableId,
                "district:farm",
                "r1",
                "farm.harvest-lot.potato-001",
                "HarvestLot:harvest-lot:potato-001",
                "HarvestLotPotatoBoxPlacement",
                new Vector3(4f, .25f, -4f),
                Vector3.zero);
            var lotRenderer = potatoBox.GetComponentsInChildren<Renderer>(true).First();
            SeedbedObjectPlacement(
                placementRoot,
                "seedbed-object:farm.pallet-crate.a",
                FarmPalletCratePlacementStableId,
                "district:farm",
                "r1",
                "farm.outbound.pallet-crate",
                "CanonicalProductHarvestCargo:cargo:sim.potato.20260407.r3",
                "FarmOutboundPalletCratePlacement",
                new Vector3(6.3f, .2f, -1.2f),
                new Vector3(0f, 18f, 0f));
            harvestLot.GetComponent<SimulationWorldNavigationTargetView>().Configure(
                SimulationObservationScaleCodes.Object,
                SimulationWorldShellFixture.SettlementStableId,
                "district:farm",
                "harvest-lot:potato-001",
                SimulationWorldShellPresenter.ObjectFocusAnchorPrefix + "harvest-lot:potato-001",
                lotRenderer,
                Rgb(.75f, .53f, .2f),
                new Color(1f, .78f, .18f));

            var town = Required(districts, "TownDistrict");
            var townVisuals = RebuildRoot(town, "VisualBaseRoot");
            SetRendererVisible(Required(town, "TownHall"), false);
            CatalogVisual(townVisuals, urbanCatalog, UrbanVisualKeys.Apartment,
                "TownHousingVisual", new Vector3(-2.4f, .2f, .8f), new Vector3(0f, 18f, 0f), .68f);
            CatalogVisual(townVisuals, urbanCatalog, UrbanVisualKeys.MarketBuilding,
                "TownHallVisual", new Vector3(3f, .2f, -.8f), new Vector3(0f, -18f, 0f), .78f);
            var townPlacementRoot = RebuildRoot(town, "SceneObjectPlacements");
            SeedbedObjectPlacement(
                townPlacementRoot,
                "seedbed-object:town.grouping-cart-table.a",
                GroupingCartTablePlacementStableId,
                "district:town",
                "r1",
                "town.orderer-group.grouping-cart-table",
                "GroupingPreview:grouping-preview:sim.potato.town",
                "GroupingCartTablePlacement",
                new Vector3(-1.2f, .2f, -3.1f),
                new Vector3(0f, 8f, 0f));

            var market = Required(districts, "MarketDistrict");
            var marketVisuals = RebuildRoot(market, "VisualBaseRoot");
            for (var index = 0; index < 4; index++)
                SetRendererVisible(Required(market, "MarketStall_" + index), false);
            var legacyMarketBuilding = CatalogVisual(marketVisuals, urbanCatalog, UrbanVisualKeys.MarketBuilding,
                "MarketBuildingVisual", new Vector3(1.5f, .2f, 1.2f), new Vector3(0f, -8f, 0f), 1.02f);
            foreach (var renderer in legacyMarketBuilding.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            var marketPlacementRoot = RebuildRoot(market, "SceneObjectPlacements");
            SeedbedObjectPlacement(
                marketPlacementRoot,
                "seedbed-object:city.urban-market-building.a",
                UrbanMarketShopPlacementStableId,
                "district:market",
                "r1",
                "market.public-products.shop",
                "MartPublicProduct:mart-product:sim.potato.public",
                "UrbanMarketShopPlacement",
                new Vector3(1.5f, .2f, 1.2f),
                new Vector3(0f, -8f, 0f));
            CatalogVisual(marketVisuals, farmCatalog, FarmVisualKeys.ProduceStand,
                "ProduceStandWest", new Vector3(-5.3f, .2f, -2.8f), new Vector3(0f, 12f, 0f), 1.08f);
            CatalogVisual(marketVisuals, farmCatalog, FarmVisualKeys.ProduceStand,
                "ProduceStandEast", new Vector3(5.6f, .2f, -3f), new Vector3(0f, -12f, 0f), 1.08f);

            var storage = Required(districts, "StorageDistrict");
            var storageVisuals = RebuildRoot(storage, "VisualBaseRoot");
            SetRendererVisible(Required(storage, "StorageBuilding"), false);
            CatalogVisual(storageVisuals, urbanCatalog, UrbanVisualKeys.LogisticsBuilding,
                "StorageBuildingVisual", new Vector3(-1.2f, .2f, 1f), new Vector3(0f, 8f, 0f), .88f);
            for (var index = 0; index < 3; index++)
            {
                CatalogVisual(storageVisuals, urbanCatalog, UrbanVisualKeys.Pallet,
                    "ReservePallet_" + index, new Vector3(-4f + index * 3.3f, .2f, -3.6f),
                    new Vector3(0f, index * 17f, 0f), 1.12f);
                CatalogVisual(storageVisuals, urbanCatalog, UrbanVisualKeys.CargoBox,
                    "ReserveBox_" + index, new Vector3(-4f + index * 3.3f, .45f, -3.6f),
                    new Vector3(0f, index * 23f, 0f), 1.18f);
            }

            var logistics = Required(districts, "LogisticsDistrict");
            var logisticsVisuals = RebuildRoot(logistics, "VisualBaseRoot");
            SetRendererVisible(Required(logistics, "LogisticsHub"), false);
            SetRendererVisible(Required(logistics, "LoadingDock"), false);
            var logisticsPlacementRoot = RebuildRoot(logistics, "SceneObjectPlacements");
            SeedbedObjectPlacement(
                logisticsPlacementRoot,
                "seedbed-object:town.hub-inbound-gate.a",
                HubInboundGatePlacementStableId,
                "district:logistics",
                "r1",
                "logistics.hub.inbound-gate",
                "HubReceiving:hub-receiving:sim.potato",
                "HubInboundGatePlacement",
                new Vector3(-3f, .2f, 1f),
                new Vector3(0f, -8f, 0f));
            SeedbedObjectPlacement(
                logisticsPlacementRoot,
                "seedbed-object:town.delivery-truck.a",
                DeliveryTruckPlacementStableId,
                "district:logistics",
                "r1",
                "logistics.cargo-journey.delivery-truck",
                "CargoJourney:cargo-journey:sim.potato.farm-hub",
                "DeliveryTruckPlacement",
                new Vector3(5.2f, .2f, -2f),
                new Vector3(0f, -18f, 0f));
            SeedbedObjectPlacement(
                logisticsPlacementRoot,
                "seedbed-object:shared.cargo-pallet.a",
                CargoPalletPlacementStableId,
                "district:logistics",
                "r1",
                "logistics.warehouse-handoff.cargo-pallet",
                "WarehouseHandoff:cargo-handoff:sim.potato.20260407.r3.inbound-91",
                "CargoPalletPlacement",
                new Vector3(3.7f, .2f, 2.8f),
                Vector3.zero);
            CatalogVisual(logisticsVisuals, urbanCatalog, UrbanVisualKeys.CargoBox,
                "OutboundCargoVisual", new Vector3(3.7f, .5f, 2.8f), Vector3.zero, 1.1f);

            var residential = Required(districts, "ResidentialDistrict");
            var residentialVisuals = RebuildRoot(residential, "VisualBaseRoot");
            for (var index = 0; index < 4; index++)
                SetRendererVisible(Required(residential, "House_" + index), false);
            CatalogVisual(residentialVisuals, urbanCatalog, UrbanVisualKeys.Apartment,
                "ResidentialWest", new Vector3(-4f, .2f, .5f), new Vector3(0f, 12f, 0f), .7f);
            CatalogVisual(residentialVisuals, urbanCatalog, UrbanVisualKeys.Apartment,
                "ResidentialEast", new Vector3(4f, .2f, .5f), new Vector3(0f, -12f, 0f), .7f);
            CatalogVisual(residentialVisuals, environmentCatalog, EnvironmentVisualKeys.CityTreeB,
                "ResidentialCourtyardTree", new Vector3(0f, .2f, -3.5f), Vector3.zero, .7f);
        }

        private static void ConfigureTimeOfDay(Transform shellRoot)
        {
            var light = Required(shellRoot, "Lighting/GlobalDirectionalLight").GetComponent<Light>()
                ?? throw new InvalidOperationException("SimulationSettlementDirectionalLightMissing");
            var camera = shellRoot.GetComponentInChildren<Camera>(true)
                ?? throw new InvalidOperationException("SimulationSettlementCameraMissing");
            var presenter = shellRoot.GetComponent<월드시간대Presenter>()
                ?? shellRoot.gameObject.AddComponent<월드시간대Presenter>();
            presenter.Configure(light, camera, shellRoot, 15f / 24f, false, 180f);
            EditorUtility.SetDirty(presenter);
        }

        private static GameObject SeedbedObjectPlacement(
            Transform parent,
            string objectStableId,
            string placementStableId,
            string zoneStableId,
            string profileRevision,
            string sceneAnchorKey,
            string dataBindingKey,
            string name,
            Vector3 localPosition,
            Vector3 localEuler)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<통합전시관ObjectVisualCatalog>(
                              ExhibitionObjectCatalogPath)
                          ?? throw new InvalidOperationException(
                              "SimulationSettlementExhibitionObjectCatalogMissing");
            var entry = catalog.Resolve(objectStableId);
            var placement = new GameObject(name);
            placement.transform.SetParent(parent, false);
            placement.transform.localPosition = localPosition;
            placement.transform.localRotation = Quaternion.Euler(localEuler);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, placement.transform);
            instance.name = "SeedbedObject_" + entry.DisplayName.Replace(" ", string.Empty);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var objectRoot = instance.GetComponent<통합전시관SeedbedObjectRoot>()
                             ?? throw new InvalidOperationException(
                                 "SimulationSettlementSeedbedObjectRootMissing:" + objectStableId);
            var view = placement.AddComponent<통합전시관ScenePlacementView>();
            view.Configure(placementStableId, SceneStableId, zoneStableId, profileRevision,
                sceneAnchorKey, dataBindingKey, objectRoot);
            if (!view.ValidateWiring())
                throw new InvalidOperationException(
                    "SimulationSettlementScenePlacementWiringInvalid:" + placementStableId);
            return instance;
        }

        private static void RequireScenePlacement(
            통합전시관ScenePlacementView[] placements,
            string placementStableId,
            string zoneStableId,
            string objectStableId)
        {
            var placement = placements.SingleOrDefault(value =>
                value.PlacementStableId == placementStableId);
            if (placement == null
                || placement.SceneStableId != SceneStableId
                || placement.ZoneStableId != zoneStableId
                || placement.ObjectRoot.ObjectStableId != objectStableId)
                throw new InvalidOperationException(
                    "SimulationSettlementScenePlacementReceiptInvalid:" + placementStableId);
            if (PrefabUtility.GetCorrespondingObjectFromSource(placement.ObjectRoot.gameObject) == null)
                throw new InvalidOperationException(
                    "SimulationSettlementScenePlacementPrefabConnectionMissing:" + placementStableId);
        }

        private static void BuildDistricts(Transform parent)
        {
            var farm = District(parent, "FarmDistrict", "district:farm", "district.farm",
                new Vector3(-27f, 0f, 2f), new Vector3(18f, .35f, 18f), new Color(.49f, .61f, .27f), false);
            for (var row = 0; row < 2; row++)
            for (var column = 0; column < 5; column++)
                Primitive(farm, $"FarmPlot_{row}_{column}", PrimitiveType.Cube,
                    new Vector3(-5.5f + column * 2.7f, .42f, -2.3f + row * 4.6f),
                    new Vector3(2.2f, .35f, 3.5f), Rgb(.46f, .31f, .17f));
            Primitive(farm, "FarmBarn", PrimitiveType.Cube,
                new Vector3(5.5f, 2f, 4.8f), new Vector3(5f, 4f, 4f), Rgb(.56f, .28f, .18f));
            var harvestLot = Primitive(farm, "HarvestLot_Potato_001", PrimitiveType.Cube,
                new Vector3(4f, 1f, -4f), new Vector3(2.8f, 2f, 2.8f), Rgb(.75f, .53f, .2f));
            harvestLot.gameObject.AddComponent<BoxCollider>();
            harvestLot.gameObject.AddComponent<SimulationWorldNavigationTargetView>().Configure(
                SimulationObservationScaleCodes.Object,
                SimulationWorldShellFixture.SettlementStableId,
                "district:farm",
                "harvest-lot:potato-001",
                SimulationWorldShellPresenter.ObjectFocusAnchorPrefix + "harvest-lot:potato-001",
                harvestLot.GetComponent<Renderer>(),
                Rgb(.75f, .53f, .2f),
                new Color(1f, .78f, .18f));
            Label(harvestLot, "POTATO LOT · 300kg", new Vector3(0f, 1.5f, 0f), 32, Color.white, .045f);

            var town = District(parent, "TownDistrict", "district:town", "district.town",
                new Vector3(-10f, 0f, 10f), new Vector3(13f, .35f, 11f), new Color(.64f, .51f, .32f), false);
            Primitive(town, "TownHall", PrimitiveType.Cube,
                new Vector3(0f, 2.2f, 0f), new Vector3(6f, 4.4f, 5f), Rgb(.72f, .54f, .3f));

            var market = District(parent, "MarketDistrict", "district:market", "district.market",
                new Vector3(24f, 0f, 5f), new Vector3(16f, .35f, 13f), new Color(.74f, .55f, .27f), false);
            for (var index = 0; index < 4; index++)
                Primitive(market, "MarketStall_" + index, PrimitiveType.Cube,
                    new Vector3(-5f + index * 3.3f, 1.1f, 0f), new Vector3(2.5f, 2.2f, 3.2f),
                    index % 2 == 0 ? Rgb(.78f, .34f, .22f) : Rgb(.86f, .66f, .24f));

            var storage = District(parent, "StorageDistrict", "district:storage", "district.storage",
                new Vector3(4f, 0f, -13f), new Vector3(14f, .35f, 11f), new Color(.43f, .47f, .45f), false);
            Primitive(storage, "StorageBuilding", PrimitiveType.Cube,
                new Vector3(0f, 2.4f, 0f), new Vector3(9f, 4.8f, 7f), Rgb(.52f, .54f, .5f));

            var logistics = District(parent, "LogisticsDistrict", "district:logistics", "district.logistics",
                new Vector3(9f, 0f, -23f), new Vector3(19f, .35f, 12f), new Color(.28f, .48f, .55f), false);
            Primitive(logistics, "LogisticsHub", PrimitiveType.Cube,
                new Vector3(-2f, 2.5f, 0f), new Vector3(10f, 5f, 7f), Rgb(.32f, .58f, .64f));
            Primitive(logistics, "LoadingDock", PrimitiveType.Cube,
                new Vector3(5f, .65f, 0f), new Vector3(4f, 1.3f, 7f), Rgb(.65f, .58f, .36f));
            var cargo = Primitive(logistics, "PotatoCargo", PrimitiveType.Cube,
                new Vector3(6f, 1.1f, -3f), new Vector3(3.8f, 2.2f, 2.8f), Rgb(.24f, .65f, .68f));
            cargo.gameObject.AddComponent<BoxCollider>();
            cargo.gameObject.AddComponent<SimulationWorldNavigationTargetView>().Configure(
                SimulationObservationScaleCodes.Object,
                SimulationWorldShellFixture.SettlementStableId,
                "district:logistics",
                물류이동Fixture.CargoStableId,
                SimulationWorldShellPresenter.ObjectFocusAnchorPrefix + 물류이동Fixture.CargoStableId,
                cargo.GetComponent<Renderer>(),
                Rgb(.24f, .65f, .68f),
                new Color(1f, .78f, .18f));
            Label(cargo.transform, "POTATO CARGO · 300kg", new Vector3(0f, 1.8f, 0f), 32, Color.white, .045f);

            var residential = District(parent, "ResidentialDistrict", "district:residential", "district.residential",
                new Vector3(-10f, 0f, -8f), new Vector3(14f, .35f, 12f), new Color(.52f, .45f, .36f), false);
            for (var index = 0; index < 4; index++)
                Primitive(residential, "House_" + index, PrimitiveType.Cube,
                    new Vector3(-4f + index % 2 * 8f, 1.7f, -2.4f + index / 2 * 5f),
                    new Vector3(4.5f, 3.4f, 3.8f), Rgb(.73f, .63f, .47f));

            var garrison = District(parent, "GarrisonDistrict", "district:garrison", "district.garrison.placeholder",
                new Vector3(0f, 0f, 20f), new Vector3(14f, .35f, 10f), new Color(.36f, .37f, .4f), true);
            Primitive(garrison, "GarrisonPlaceholder", PrimitiveType.Cube,
                new Vector3(0f, 1.4f, 0f), new Vector3(7f, 2.8f, 5f), Rgb(.43f, .43f, .46f));
            Label(garrison, "GARRISON · PLACEHOLDER", new Vector3(0f, 3.4f, 0f), 40, Rgb(.9f, .78f, .38f));

            var gate = District(parent, "GateDistrict", "district:gate", "district.gate.placeholder",
                new Vector3(0f, 0f, 31f), new Vector3(16f, .35f, 7f), new Color(.4f, .39f, .37f), true);
            Primitive(gate, "GatePillarLeft", PrimitiveType.Cube,
                new Vector3(-4f, 2.8f, 0f), new Vector3(3f, 5.6f, 3f), Rgb(.49f, .47f, .42f));
            Primitive(gate, "GatePillarRight", PrimitiveType.Cube,
                new Vector3(4f, 2.8f, 0f), new Vector3(3f, 5.6f, 3f), Rgb(.49f, .47f, .42f));
            Primitive(gate, "GateLintel", PrimitiveType.Cube,
                new Vector3(0f, 5.1f, 0f), new Vector3(6f, 1.2f, 3f), Rgb(.49f, .47f, .42f));
        }

        private static Transform District(
            Transform parent,
            string name,
            string stableId,
            string visualKey,
            Vector3 position,
            Vector3 padScale,
            Color color,
            bool placeholder)
        {
            var district = Child(parent, name);
            district.localPosition = position;
            district.gameObject.AddComponent<SimulationWorldDistrictView>()
                .Configure(stableId, visualKey, placeholder);
            var surface = Primitive(district, "DistrictSurface", PrimitiveType.Cube,
                new Vector3(0f, .02f, 0f), padScale, color);
            surface.gameObject.AddComponent<BoxCollider>();
            surface.gameObject.AddComponent<SimulationWorldNavigationTargetView>().Configure(
                SimulationObservationScaleCodes.District,
                SimulationWorldShellFixture.SettlementStableId,
                stableId,
                string.Empty,
                SimulationWorldShellPresenter.DistrictFocusAnchorPrefix + stableId,
                surface.GetComponent<Renderer>(),
                color,
                Color.Lerp(color, Color.white, .35f));
            Label(district, name.Replace("District", string.Empty).ToUpperInvariant(),
                new Vector3(0f, .55f, -padScale.z * .4f), 42, Color.white);
            return district;
        }

        private static WorldVisualInstanceView CatalogVisual(
            Transform parent,
            WorldVisualCatalog catalog,
            string visualKey,
            string name,
            Vector3 localPosition,
            Vector3 localEuler,
            float scaleMultiplier)
        {
            var entry = catalog.Resolve(visualKey);
            var wrapper = Child(parent, name);
            wrapper.localPosition = localPosition;
            wrapper.localRotation = Quaternion.Euler(localEuler);
            var visualRoot = Child(wrapper, "VisualRoot");
            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, visualRoot) as GameObject
                ?? throw new InvalidOperationException(
                    "SimulationSettlementPrefabInstantiationFailed:" + visualKey);
            instance.name = "SyntyPrefabInstance";
            instance.transform.localPosition = entry.LocalPositionCorrection;
            instance.transform.localRotation = Quaternion.Euler(entry.LocalEulerCorrection);
            instance.transform.localScale = entry.LocalScale * scaleMultiplier;
            var view = wrapper.gameObject.AddComponent<WorldVisualInstanceView>();
            view.Configure(visualKey, catalog, visualRoot, instance);
            return view;
        }

        private static Transform RebuildRoot(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            return Child(parent, name);
        }

        private static void SetRendererVisible(Transform target, bool visible)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = visible;
        }

        private static WorldVisualCatalog LoadCatalog(string path)
            => AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(path)
               ?? throw new InvalidOperationException("SimulationSettlementVisualCatalogMissing:" + path);

        private static void RequireDistrictVisual(
            Transform settlement,
            string districtName,
            params string[] visualKeys)
        {
            var district = Required(settlement, "Districts/" + districtName);
            var keys = district.GetComponentsInChildren<WorldVisualInstanceView>(true)
                .Select(value => value.VisualKey)
                .ToArray();
            foreach (var visualKey in visualKeys)
                if (!keys.Contains(visualKey, StringComparer.Ordinal))
                    throw new InvalidOperationException(
                        "SimulationSettlementDistrictVisualMissing:" + districtName + ":" + visualKey);
        }

        private static Transform Marker(
            Transform parent,
            string name,
            Vector3 position,
            Color color,
            string label)
        {
            var marker = Primitive(parent, name, PrimitiveType.Cylinder,
                position, new Vector3(3.4f, .8f, 3.4f), color);
            Primitive(marker, "MarkerCore", PrimitiveType.Sphere,
                new Vector3(0f, 1.8f, 0f), new Vector3(1.6f, 1.6f, 1.6f), color * 1.15f);
            Label(marker, label, new Vector3(0f, 3.7f, 0f), 54, Color.white);
            return marker;
        }

        private static void Route(Transform parent, string name, Vector3 from, Vector3 to, float width)
        {
            var direction = to - from;
            var midpoint = (from + to) * .5f;
            var route = Primitive(parent, name, PrimitiveType.Cube,
                new Vector3(midpoint.x, .08f, midpoint.z),
                new Vector3(width, .12f, direction.magnitude), Rgb(.64f, .57f, .42f));
            route.rotation = Quaternion.Euler(0f,
                Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
        }

        private static Transform Primitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            var renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.sharedMaterial.color = color;
            var collider = item.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            return item.transform;
        }

        private static void Label(
            Transform parent,
            string value,
            Vector3 localPosition,
            int fontSize,
            Color color,
            float characterSize = .12f)
        {
            var item = new GameObject("Label_" + value).AddComponent<TextMesh>();
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            item.anchor = TextAnchor.MiddleCenter;
            item.alignment = TextAlignment.Center;
            item.fontSize = fontSize;
            item.characterSize = characterSize;
            item.color = color;
            item.text = value;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform Required(Transform parent, string name)
            => parent.Find(name)
               ?? throw new InvalidOperationException("SimulationWorldShellChildMissing:" + name);

        private static GameObject Required(string path)
            => GameObject.Find(path)
               ?? throw new InvalidOperationException("SimulationWorldShellObjectMissing:" + path);

        private static SimulationWorldShellPresenter FindPresenter()
            => UnityEngine.Object.FindFirstObjectByType<SimulationWorldShellPresenter>(
                   FindObjectsInactive.Include)
               ?? throw new InvalidOperationException("SimulationWorldShellPresenterMissing");

        private static void Clear(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }

        private static void OpenShellIfRequired()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path == ScenePath && GameObject.Find(RootName) != null) return;
            if (!File.Exists(ScenePath)) BuildWorldShell();
            else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static DioramaCameraFocusBinding Binding(
            string stableId,
            string levelCode,
            Transform anchor)
            => new()
            {
                AnchorId = stableId,
                LevelCode = levelCode,
                Anchor = anchor,
            };

        private static Transform FocusAnchor(Transform parent, string name, Vector3 position)
        {
            var anchor = Child(parent, name);
            anchor.position = position;
            return anchor;
        }

        private static GameObject Panel(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Vector2 anchor)
        {
            var panel = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = new Color(.055f, .07f, .08f, .92f);
            return panel;
        }

        private static Text Text(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button Button(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            var text = Text(item.transform, "Label", Vector2.zero, size, 14,
                TextAnchor.MiddleCenter, Color.white);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.text = label;
            return item.GetComponent<Button>();
        }

        private static Color Rgb(float r, float g, float b)
            => new(r, g, b, 1f);

        private readonly struct HudReferences
        {
            public HudReferences(
                Transform canvas,
                Text mode,
                Text identity,
                Text economy,
                Text selection,
                Button worldMap,
                Button settlement,
                Button back,
                Button pause,
                Button speed)
            {
                Canvas = canvas;
                Mode = mode;
                Identity = identity;
                Economy = economy;
                Selection = selection;
                WorldMap = worldMap;
                Settlement = settlement;
                Back = back;
                Pause = pause;
                Speed = speed;
            }

            public Transform Canvas { get; }
            public Text Mode { get; }
            public Text Identity { get; }
            public Text Economy { get; }
            public Text Selection { get; }
            public Button WorldMap { get; }
            public Button Settlement { get; }
            public Button Back { get; }
            public Button Pause { get; }
            public Button Speed { get; }
        }
    }
}
