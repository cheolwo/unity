using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class 세RegionHubJourneyBuilder
    {
        private const string AutomatedNightRaidCaptureArgument = "-ssalddelCaptureNightRaid";
        private const string AutomatedNightRaidCaptureStateKey =
            "Ssalddel.ThreeRegion.NightRaidCaptureState";
        private static int automatedCaptureFrame;
        public const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장마을도시물류거점이동.unity";
        public const string CaptureRelativePath =
            "Documentation/Changes/2026-08-10-art0-art3-first-pass/three-region-hub-art-first-pass.png";
        public const string SpacedCaptureRelativePath =
            "Documentation/Changes/2026-08-10-three-region-300m/three-region-300m-game-view.png";
        public const string SpacedHubCaptureRelativePath =
            "Documentation/Changes/2026-08-10-three-region-300m/hub-transition-landmarks-game-view.png";
        public const string RoadsideDayCaptureRelativePath =
            "Documentation/Changes/2026-08-10-night-monster-raid/roadside-day-game-view.png";
        public const string NightRaidCaptureRelativePath =
            "Documentation/Changes/2026-08-10-night-monster-raid/night-monster-looting-game-view.png";
        public const string TerrainReliefCaptureRelativePath =
            "Documentation/Changes/2026-08-10-night-monster-raid/terrain-relief-game-view.png";
        public const string WorldFocusId = "camera-focus:three-region-world";
        public const string FarmFocusId = "camera-focus:region:" + 월드CompositionPackCodes.Farm;
        public const string TownFocusId = "camera-focus:region:" + 월드CompositionPackCodes.Town;
        public const string HubFocusId = "camera-focus:region:" + 월드CompositionPackCodes.RegionalLogisticsHub;
        public const string CityFocusId = "camera-focus:region:" + 월드CompositionPackCodes.City;
        public const string NightRaidFocusId = "camera-focus:encounter:hub-city-raid";
        public const string ExistingFarmCargoStableId = "cargo:transport-71";
        public const string TownCargoStableId = "cargo:town-delivery-01";
        public const float RegionSeparationScale = 6.8f;

        private const string AnchorCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/거점CompositionCatalog.asset";
        private const string GateCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/도로GateCompositionCatalog.asset";
        private const string AnimationCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/공용AnimationCatalog.asset";
        private const string EnvironmentCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/FarmCityEnvironmentCatalog.asset";
        private const string MaterialRoot =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/ThreeRegionHubMaterials";
        private const string CityVanPath =
            "Assets/Synty/PolygonCity/Prefabs/Vehicles/SM_Veh_Car_Van_01.prefab";
        private const string PalletPath =
            "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Pallet_01.prefab";
        private const string BoxPath =
            "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_CardboardBox_01.prefab";
        private const string MonsterPath =
            "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Skeleton_01.prefab";
        private const string TownHouse03Path =
            "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_03.prefab";
        private const string TownHouse06Path =
            "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_06.prefab";
        private const string TownHouse09Path =
            "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_09.prefab";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomatedNightRaidCapture()
        {
            if (!System.Environment.GetCommandLineArgs().Contains(
                    AutomatedNightRaidCaptureArgument, StringComparer.Ordinal))
                return;
            var state = SessionState.GetInt(AutomatedNightRaidCaptureStateKey, 0);
            if (EditorApplication.isPlaying)
            {
                BeginAutomatedCaptureInPlayMode();
                return;
            }
            if (state == 3)
            {
                SessionState.EraseInt(AutomatedNightRaidCaptureStateKey);
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
                return;
            }
            if (state == 1 && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged += ResumeAutomatedCaptureAfterReload;
                return;
            }
            if (state == 1)
            {
                EditorApplication.update -= BeginAutomatedNightRaidCapture;
                EditorApplication.update += BeginAutomatedNightRaidCapture;
                return;
            }
            if (state != 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetInt(AutomatedNightRaidCaptureStateKey, 1);
            Debug.Log("AutomatedNightRaidCaptureScheduled");
            EditorApplication.update += BeginAutomatedNightRaidCapture;
        }

        private static void BeginAutomatedNightRaidCapture()
        {
            if (EditorApplication.isCompiling) return;
            EditorApplication.update -= BeginAutomatedNightRaidCapture;
            Debug.Log("AutomatedNightRaidCaptureStarting");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            automatedCaptureFrame = 0;
            EditorApplication.EnterPlaymode();
        }

        private static void ResumeAutomatedCaptureAfterReload(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= ResumeAutomatedCaptureAfterReload;
            BeginAutomatedCaptureInPlayMode();
        }

        private static void BeginAutomatedCaptureInPlayMode()
        {
            if (SessionState.GetInt(AutomatedNightRaidCaptureStateKey, 0) > 1) return;
            SessionState.SetInt(AutomatedNightRaidCaptureStateKey, 2);
            automatedCaptureFrame = 0;
            EditorApplication.update -= TickAutomatedNightRaidCapture;
            EditorApplication.update += TickAutomatedNightRaidCapture;
        }

        private static void TickAutomatedNightRaidCapture()
        {
            automatedCaptureFrame++;
            if (automatedCaptureFrame == 20)
            {
                RequireNightRaid().ApplyDayPreview();
                FocusCamera(HubFocusId);
                CapturePlayModeTo(TerrainReliefCaptureRelativePath);
            }
            else if (automatedCaptureFrame == 60)
            {
                PreviewRoadsideDay();
                CapturePlayModeTo(RoadsideDayCaptureRelativePath);
            }
            else if (automatedCaptureFrame == 100)
            {
                PreviewNightRaid();
                CapturePlayModeTo(NightRaidCaptureRelativePath);
            }
            else if (automatedCaptureFrame >= 160)
            {
                EditorApplication.update -= TickAutomatedNightRaidCapture;
                SessionState.SetInt(AutomatedNightRaidCaptureStateKey, 3);
                EditorApplication.ExitPlaymode();
            }
        }

        [MenuItem("Ssalddel/World Composition/Build Three Region Hub Journey")]
        public static void Build()
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));
            EnsureFolder(MaterialRoot);
            var anchors = Load<거점CompositionCatalog>(AnchorCatalogPath);
            var gates = Load<도로GateCompositionCatalog>(GateCatalogPath);
            var animations = Load<공용AnimationCatalog>(AnimationCatalogPath);
            var environment = Load<WorldVisualCatalog>(EnvironmentCatalogPath);
            anchors.Validate();
            gates.Validate();
            animations.Validate();
            environment.Validate();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("CMP5 Three Region Hub Journey");
            BuildEnvironment(root.transform, environment);
            var anchorViews = BuildAnchors(scene, root.transform, anchors);
            var gateViews = BuildGates(scene, root.transform, gates);
            BuildRouteRibbons(root.transform);
            var passengers = BuildPassengers(scene, root.transform, animations);
            var cargo = BuildCargoJourneys(scene, root.transform);
            var lighting = BuildLightingAndCamera(root.transform, anchorViews);
            var timeOfDay = root.AddComponent<월드시간대Presenter>();
            timeOfDay.Configure(lighting.Light, lighting.Camera, root.transform,
                12.5f / 24f, false, 180f);
            var nightRaid = BuildNightRaid(root.transform,
                cargo.Single(value => value.OutboundAllocated).OutboundFollower!, timeOfDay);
            timeOfDay.RebuildSurfaceBindings();
            nightRaid.ApplyDayPreview();

            var map = root.AddComponent<세RegionHubJourneyView>();
            map.Configure(anchorViews, gateViews, passengers, cargo, nightRaid);
            if (!map.ValidateWiring())
                throw new InvalidOperationException("ThreeRegionHubJourneyWiringInvalid");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("ThreeRegionHubJourneySaveFailed");
            AssetDatabase.SaveAssets();
            Debug.Log("ThreeRegionHubJourneyBuilt:" + ScenePath);
        }

        [MenuItem("Ssalddel/World Composition/Capture Three Region Hub Art Pass")]
        public static void CaptureArtPass()
        {
            var camera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("ARTCaptureCameraMissing");

            const int width = 1600;
            const int height = 900;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", CaptureRelativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(path)
                                          ?? throw new InvalidOperationException("ARTCaptureDirectoryMissing"));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("ThreeRegionHubArtPassCaptured:" + path);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(renderTexture);
            }
        }

        [MenuItem("Ssalddel/World Composition/Capture Three Region 300m Play Mode")]
        public static void CaptureSpacedPlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("ThreeRegion300mCaptureRequiresPlayMode");
            var path = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", SpacedCaptureRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                                      ?? throw new InvalidOperationException(
                                          "ThreeRegion300mCaptureDirectoryMissing"));
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("ThreeRegion300mGameViewCaptureRequested:" + path);
        }

        [MenuItem("Ssalddel/World Composition/Capture Hub Transition Landmarks Play Mode")]
        public static void CaptureHubTransitionPlayMode()
        {
            FocusCamera(HubFocusId);
            EditorApplication.delayCall += () => CapturePlayModeTo(SpacedHubCaptureRelativePath);
        }

        [MenuItem("Ssalddel/World Composition/Preview Hub City Roadside Day")]
        public static void PreviewRoadsideDay()
        {
            RequireNightRaid().ApplyDayPreview();
            FocusNightRaidCamera();
        }

        [MenuItem("Ssalddel/World Composition/Preview Night Monster Looting")]
        public static void PreviewNightRaid()
        {
            RequireNightRaid().ApplyNightPreview(.62f);
            FocusNightRaidCamera();
        }

        [MenuItem("Ssalddel/World Composition/Capture Hub City Roadside Day Play Mode")]
        public static void CaptureRoadsideDayPlayMode()
        {
            PreviewRoadsideDay();
            EditorApplication.delayCall += () => CapturePlayModeTo(RoadsideDayCaptureRelativePath);
        }

        [MenuItem("Ssalddel/World Composition/Capture Night Monster Looting Play Mode")]
        public static void CaptureNightRaidPlayMode()
        {
            PreviewNightRaid();
            EditorApplication.delayCall += () => CapturePlayModeTo(NightRaidCaptureRelativePath);
        }

        [MenuItem("Ssalddel/World Composition/Focus Three Region Overview")]
        public static void FocusOverview() => FocusCamera(WorldFocusId);

        [MenuItem("Ssalddel/World Composition/Focus Farm Region")]
        public static void FocusFarm() => FocusCamera(FarmFocusId);

        [MenuItem("Ssalddel/World Composition/Focus Town Region")]
        public static void FocusTown() => FocusCamera(TownFocusId);

        [MenuItem("Ssalddel/World Composition/Focus Hub Region")]
        public static void FocusHub() => FocusCamera(HubFocusId);

        [MenuItem("Ssalddel/World Composition/Focus City Region")]
        public static void FocusCity() => FocusCamera(CityFocusId);

        public static 화물JourneyPresentationModel[] CreateCargoModelsForValidation()
            => new[] { FarmCargoModel(), TownCargoModel() };

        private static 거점CompositionSetView[] BuildAnchors(
            Scene scene,
            Transform parent,
            거점CompositionCatalog catalog)
        {
            var definitions = new[]
            {
                (거점CompositionSetNames.실제감자6x6필지, new Vector3(-42f, 0f, 18f)),
                (거점CompositionSetNames.타운기본주택, new Vector3(-42f, 0f, -25f)),
                (거점CompositionSetNames.지역물류허브Dock, new Vector3(5f, 0f, 0f)),
                (거점CompositionSetNames.시티공동주택가로형, new Vector3(47f, 0f, 0f)),
            };
            return definitions.Select(value =>
            {
                var entry = catalog.Resolve(value.Item1);
                var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject
                               ?? throw new InvalidOperationException("CMP5AnchorInstantiateFailed");
                instance.transform.SetParent(parent, true);
                instance.transform.position = Separate(value.Item2);
                instance.name = "RegionAnchor_" + entry.Descriptor.PackCode;
                CreateLabel(parent, entry.Descriptor.SetName, value.Item2
                    + new Vector3(0f, .15f, entry.Descriptor.Footprint.y * .5f + 2f));
                return instance.GetComponent<거점CompositionSetView>();
            }).ToArray();
        }

        private static 도로GateCompositionSetView[] BuildGates(
            Scene scene,
            Transform parent,
            도로GateCompositionCatalog catalog)
        {
            var definitions = new[]
            {
                (도로GateCompositionSetNames.농장타운농장출구, new Vector3(-48f, 0f, -3f), 0f),
                (도로GateCompositionSetNames.농장타운타운입구, new Vector3(-48f, 0f, -12f), 0f),
                (도로GateCompositionSetNames.타운시티타운출구, new Vector3(-28f, 0f, -30f), 90f),
                (도로GateCompositionSetNames.타운시티시티입구, new Vector3(34f, 0f, -18f), 90f),
                (도로GateCompositionSetNames.농장허브농장출구, new Vector3(-22f, 0f, 13f), 70f),
                (도로GateCompositionSetNames.농장허브허브입구, new Vector3(-12f, 0f, 8f), 70f),
                (도로GateCompositionSetNames.타운허브타운출구, new Vector3(-22f, 0f, -15f), 110f),
                (도로GateCompositionSetNames.타운허브허브입구, new Vector3(-12f, 0f, -8f), 110f),
                (도로GateCompositionSetNames.허브시티허브출구, new Vector3(24f, 0f, 0f), 90f),
                (도로GateCompositionSetNames.허브시티시티입구, new Vector3(32f, 0f, 0f), 90f),
            };
            return definitions.Select(value =>
            {
                var entry = catalog.Resolve(value.Item1);
                var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject
                               ?? throw new InvalidOperationException("CMP5GateInstantiateFailed");
                instance.transform.SetParent(parent, true);
                instance.transform.position = Separate(value.Item2);
                instance.transform.eulerAngles = new Vector3(0f, value.Item3, 0f);
                instance.name = "JourneyGate_" + entry.Descriptor.SetName.Replace(" ", string.Empty);
                return instance.GetComponent<도로GateCompositionSetView>();
            }).ToArray();
        }

        private static void BuildRouteRibbons(Transform parent)
        {
            var passenger = Material("PassengerRoute", new Color(.93f, .68f, .25f));
            var freight = Material("FreightRoute", new Color(.27f, .61f, .74f));
            Ribbon(parent, "DataRoute_Passenger_FarmTown",
                new Vector3(-48f, .14f, -3f), new Vector3(-48f, .14f, -12f), .22f, passenger);
            Ribbon(parent, "DataRoute_Passenger_TownCity",
                new Vector3(-28f, .14f, -30f), new Vector3(34f, .14f, -18f), .22f, passenger);
            Ribbon(parent, "DataRoute_Freight_FarmHub",
                new Vector3(-22f, .15f, 13f), new Vector3(-12f, .15f, 8f), .28f, freight);
            Ribbon(parent, "DataRoute_Freight_TownHub",
                new Vector3(-22f, .15f, -15f), new Vector3(-12f, .15f, -8f), .28f, freight);
            Ribbon(parent, "DataRoute_Freight_HubCity",
                new Vector3(24f, .15f, 0f), new Vector3(32f, .15f, 0f), .28f, freight);
        }

        private static 공용ActorRouteFollower[] BuildPassengers(
            Scene scene,
            Transform parent,
            공용AnimationCatalog catalog)
        {
            return new[]
            {
                Passenger(scene, parent, catalog.Resolve(월드CompositionPackCodes.Farm),
                    "Passenger_FarmTown", new Vector3(-51f, 0f, -2f),
                    new Vector3(-51f, 0f, -14f)),
                Passenger(scene, parent, catalog.Resolve(월드CompositionPackCodes.Town),
                    "Passenger_TownCity", new Vector3(-27f, 0f, -27f),
                    new Vector3(34f, 0f, -15f)),
            };
        }

        private static 공용ActorRouteFollower Passenger(
            Scene scene,
            Transform parent,
            공용AnimationCatalogEntry entry,
            string name,
            Vector3 startPosition,
            Vector3 endPosition)
        {
            var start = Anchor(parent, name + "_Start", startPosition);
            var end = Anchor(parent, name + "_End", endPosition);
            var actor = new GameObject(name);
            actor.transform.SetParent(parent, false);
            var visual = PrefabUtility.InstantiatePrefab(entry.CharacterPrefab, actor.transform)
                         as GameObject
                         ?? throw new InvalidOperationException("CMP5PassengerInstantiateFailed");
            visual.name = "VisualRoot";
            visual.transform.localPosition = Vector3.zero;
            var animator = visual.GetComponentInChildren<Animator>(true)
                           ?? throw new InvalidOperationException("CMP5PassengerAnimatorMissing");
            var adapter = actor.AddComponent<공용AnimationAdapter>();
            adapter.Configure(entry, animator);
            var follower = actor.AddComponent<공용ActorRouteFollower>();
            follower.Configure(start, end, adapter, 2f, .7f);
            return follower;
        }

        private static 화물PresentationJourneyView[] BuildCargoJourneys(Scene scene, Transform parent)
        {
            var palletPrefab = Load<GameObject>(PalletPath);
            var boxPrefab = Load<GameObject>(BoxPath);
            var vanPrefab = Load<GameObject>(CityVanPath);

            var farmWrapper = new GameObject("CargoJourney_FarmPotato_HubStored");
            farmWrapper.transform.SetParent(parent, false);
            farmWrapper.transform.position = Separate(new Vector3(-3f, 0f, 4f));
            var farmVisual = new GameObject("CargoVisual");
            farmVisual.transform.SetParent(farmWrapper.transform, false);
            Nested(palletPrefab, farmVisual.transform, Vector3.zero, 0f);
            Nested(boxPrefab, farmVisual.transform, new Vector3(0f, .55f, 0f), 0f);
            var farmView = farmWrapper.AddComponent<화물PresentationJourneyView>();
            farmView.Configure(farmVisual, null);
            farmView.Apply(FarmCargoModel());
            CreateLabel(parent, "FARM POTATO | HUB STORED", new Vector3(-3f, .1f, 7f));

            var townWrapper = new GameObject("CargoJourney_TownDelivery_CityOutbound");
            townWrapper.transform.SetParent(parent, false);
            var routeStart = Anchor(parent, "TownCargoOutbound_Start", new Vector3(16f, 0f, 0f));
            var routeEnd = Anchor(parent, "TownCargoOutbound_End", new Vector3(36f, 0f, 0f));
            var van = PrefabUtility.InstantiatePrefab(vanPrefab, townWrapper.transform) as GameObject
                      ?? throw new InvalidOperationException("CMP5CargoVanInstantiateFailed");
            van.name = "CargoVisual";
            var follower = townWrapper.AddComponent<절차형VehicleRouteFollower>();
            follower.Configure(routeStart, routeEnd, 4.5f, true);
            var townView = townWrapper.AddComponent<화물PresentationJourneyView>();
            townView.Configure(van, follower);
            townView.Apply(TownCargoModel());
            return new[] { farmView, townView };
        }

        private static 화물JourneyPresentationModel FarmCargoModel()
            => new()
            {
                CargoStableId = ExistingFarmCargoStableId,
                OriginRegionCode = 월드CompositionPackCodes.Farm,
                ProductStableId = "product:potato",
                CurrentStageCode = 화물JourneyStageCodes.HubStored,
                AcceptedAtHub = true,
                StoredAtHub = true,
                OutboundAllocated = false,
                SourceStableIds = new[]
                {
                    "farm-handoff:sim.potato.1",
                    "product:potato",
                    ExistingFarmCargoStableId,
                    "cargo-handoff:transport-71.inbound-91",
                    "transport-task:71",
                    "inbound-task:91",
                },
            };

        private static 화물JourneyPresentationModel TownCargoModel()
            => new()
            {
                CargoStableId = TownCargoStableId,
                OriginRegionCode = 월드CompositionPackCodes.Town,
                ProductStableId = "product:town-grocery-sample",
                CurrentStageCode = 화물JourneyStageCodes.CityOutbound,
                AcceptedAtHub = true,
                StoredAtHub = true,
                OutboundAllocated = true,
                SourceStableIds = new[]
                {
                    "town-dispatch:sample-01",
                    "product:town-grocery-sample",
                    TownCargoStableId,
                    "cargo-handoff:town-delivery-01.inbound-02",
                    "inbound-task:town-02",
                    "outbound-allocation:town-delivery-01.city-01",
                },
            };

        private static void BuildEnvironment(Transform parent, WorldVisualCatalog catalog)
        {
            var grounds = Group(parent, "ART1 Region Grounds");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "WorldGround";
            ground.transform.SetParent(grounds, false);
            ground.transform.position = new Vector3(0f, -1.15f, 0f);
            ground.transform.localScale = new Vector3(850f, 1f, 560f);
            ground.GetComponent<Renderer>().sharedMaterial =
                Material("WorldGround", new Color(.22f, .34f, .24f));
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            RegionGround(grounds, "FarmGround", new Vector3(-39f, -.48f, 14f),
                new Vector3(45f, .55f, 39f), Material("FarmGround", new Color(.39f, .51f, .27f)));
            RegionGround(grounds, "TownGround", new Vector3(-39f, -.46f, -24f),
                new Vector3(39f, .58f, 29f), Material("TownGround", new Color(.48f, .53f, .34f)));
            RegionGround(grounds, "HubGround", new Vector3(5f, -.43f, 0f),
                new Vector3(31f, .62f, 29f), Material("HubGround", new Color(.43f, .43f, .38f)));
            RegionGround(grounds, "CityGround", new Vector3(44f, -.40f, 0f),
                new Vector3(38f, .66f, 35f), Material("CityGround", new Color(.33f, .39f, .39f)));

            BuildRoadNetwork(parent);
            BuildTerrainRelief(parent);
            BuildLandscapeClusters(parent, catalog);
            BuildTransitionLandmarks(parent, catalog);
            BuildRoadsideClusters(parent, catalog);

            CreateLabel(parent, "FARM REGION", new Vector3(-42f, .1f, 36f));
            CreateLabel(parent, "TOWN REGION", new Vector3(-42f, .1f, -36f));
            CreateLabel(parent, "REGIONAL LOGISTICS HUB", new Vector3(5f, .1f, 17f));
            CreateLabel(parent, "CITY REGION", new Vector3(47f, .1f, 14f));
        }

        private static void BuildRoadNetwork(Transform parent)
        {
            var roads = Group(parent, "ART1 Continuous Roads");
            var rural = Material("RuralRoad", new Color(.49f, .35f, .20f));
            var asphalt = Material("AsphaltRoad", new Color(.19f, .23f, .24f));
            var apron = Material("HubApron", new Color(.32f, .34f, .32f));
            var shoulder = Material("RoadShoulder", new Color(.64f, .57f, .43f));
            var line = Material("RoadCenterLine", new Color(.91f, .79f, .42f));

            Road(roads, "FarmTownRoad", new Vector3(-48f, .02f, 16f),
                new Vector3(-48f, .02f, -31f), 4.8f, rural, shoulder, null);
            Road(roads, "TownCityRoad", new Vector3(-43f, .03f, -29f),
                new Vector3(45f, .03f, -12f), 5.4f, asphalt, shoulder, line);
            Road(roads, "FarmHubRoad", new Vector3(-34f, .04f, 15f),
                new Vector3(5f, .04f, 2f), 5.8f, apron, shoulder, line);
            Road(roads, "TownHubRoad", new Vector3(-31f, .05f, -21f),
                new Vector3(4f, .05f, -3f), 5.8f, apron, shoulder, line);
            Road(roads, "HubCityRoad", new Vector3(4f, .06f, 0f),
                new Vector3(49f, .06f, -1f), 6.4f, asphalt, shoulder, line);
        }

        private static void BuildLandscapeClusters(Transform parent, WorldVisualCatalog catalog)
        {
            var landscape = Group(parent, "ART1 Landscape Clusters");

            var farmTreeKeys = new[]
            {
                EnvironmentVisualKeys.FarmTreeA, EnvironmentVisualKeys.FarmTreeB,
                EnvironmentVisualKeys.FarmTreeC, EnvironmentVisualKeys.FarmTreeApple,
            };
            for (var index = 0; index < 22; index++)
            {
                var edge = index < 12;
                var position = edge
                    ? new Vector3(-59f + index * 3.3f, 0f, 32f + index % 3 * 1.8f)
                    : new Vector3(-59f + (index - 12) * 3.4f, 0f, 1f + index % 2 * 2.3f);
                Environment(landscape, catalog, farmTreeKeys[index % farmTreeKeys.Length],
                    position, index * 47f, .7f + index % 3 * .08f);
            }
            for (var index = 0; index < 11; index++)
            {
                Environment(landscape, catalog, EnvironmentVisualKeys.FarmFence,
                    new Vector3(-57f + index * 3.1f, 0f, 4.5f), 0f, .7f);
            }
            for (var index = 0; index < 12; index++)
            {
                var key = index % 2 == 0
                    ? EnvironmentVisualKeys.FarmGrassB : EnvironmentVisualKeys.FarmFlowersB;
                Environment(landscape, catalog, key,
                    new Vector3(-57f + index * 2.8f, .02f, 8f + index % 3 * 2f),
                    index * 61f, .9f);
            }

            PlaceVendor(landscape,
                "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_03.prefab",
                "TownHouseWest", new Vector3(-51f, 0f, -23f), 18f, .42f);
            PlaceVendor(landscape,
                "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_06.prefab",
                "TownHouseSouth", new Vector3(-42f, 0f, -32f), -8f, .40f);
            PlaceVendor(landscape,
                "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/SM_Bld_House_Preset_09.prefab",
                "TownHouseEast", new Vector3(-32f, 0f, -23f), -26f, .42f);
            for (var index = 0; index < 14; index++)
            {
                var angle = index * Mathf.PI * 2f / 14f;
                Environment(landscape, catalog,
                    index % 3 == 0 ? EnvironmentVisualKeys.CityTreeA
                        : index % 3 == 1 ? EnvironmentVisualKeys.CityTreeB
                        : EnvironmentVisualKeys.CityTreeC,
                    new Vector3(-42f + Mathf.Cos(angle) * 16f, 0f,
                        -24f + Mathf.Sin(angle) * 11f), index * 43f, .65f);
            }

            Environment(landscape, catalog, EnvironmentVisualKeys.CityShopA,
                new Vector3(34f, 0f, 8f), 175f, .78f);
            Environment(landscape, catalog, EnvironmentVisualKeys.CityShopB,
                new Vector3(35f, 0f, -10f), 8f, .74f);
            Environment(landscape, catalog, EnvironmentVisualKeys.CityOffice,
                new Vector3(53f, 0f, -11f), -22f, .72f);
            Environment(landscape, catalog, EnvironmentVisualKeys.CityShopA,
                new Vector3(48f, 0f, 8f), 188f, .62f);
            Environment(landscape, catalog, EnvironmentVisualKeys.CityShopB,
                new Vector3(51f, 0f, -1f), -8f, .58f);
            for (var index = 0; index < 10; index++)
            {
                Environment(landscape, catalog,
                    index % 2 == 0 ? EnvironmentVisualKeys.CityTreeA
                        : EnvironmentVisualKeys.CityTreeC,
                    new Vector3(29f + index * 3.1f, 0f, 13f + index % 2 * 2f),
                    index * 57f, .66f);
            }
            for (var index = 0; index < 7; index++)
            {
                Environment(landscape, catalog, EnvironmentVisualKeys.CityLightPole,
                    new Vector3(28f + index * 5f, 0f, -5.5f), 0f, .72f);
            }

            for (var index = 0; index < 6; index++)
            {
                Environment(landscape, catalog, EnvironmentVisualKeys.CityPlanter,
                    new Vector3(-3f + index * 3.2f, 0f, 10f), index * 31f, .75f);
                Nested(Load<GameObject>(PalletPath), landscape,
                    Separate(new Vector3(-2f + index * 2.2f, 0f, -9f + index % 2 * 2f)),
                    index * 17f);
            }
        }

        private static void BuildTransitionLandmarks(
            Transform parent, WorldVisualCatalog catalog)
        {
            var landmarks = Group(parent, "ART1 Transition Landmarks");

            // Farm → Town: 생산지의 형태가 점차 생활권으로 바뀌는 농촌 완충 구간.
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmWindmill,
                new Vector3(-55f, 0f, 9f), 18f, .72f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmWell,
                new Vector3(-52f, 0f, 1f), -12f, .82f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmHayA,
                new Vector3(-54f, 0f, -8f), 31f, .86f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmBench,
                new Vector3(-51f, 0f, -18f), 90f, .82f);

            // Farm → Hub: 생산지 출하를 읽게 하는 급수·암석·수목 landmark.
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmWaterTower,
                new Vector3(-30f, 0f, 12f), 12f, .68f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmRocksB,
                new Vector3(-22f, 0f, 8f), 37f, .9f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.FarmTreeClusterA,
                new Vector3(-14f, 0f, 5f), -18f, .72f);

            // Town → Hub: 휴게·승하차·화단을 둔 생활 물류 접점.
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityPicnicTable,
                new Vector3(-24f, 0f, -15f), 24f, .76f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityBusStop,
                new Vector3(-15f, 0f, -10f), 74f, .72f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityPlanter,
                new Vector3(-7f, 0f, -5f), -12f, .82f);

            // Town → City: 교외 녹지에서 도시 가로 시설로 변하는 전이 구간.
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityParkBench,
                new Vector3(-11f, 0f, -25f), 82f, .78f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityTreeA,
                new Vector3(5f, 0f, -22f), 17f, .78f);
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityBusStop,
                new Vector3(20f, 0f, -18f), 92f, .74f);

            // Hub → City: 역·가로등·화단으로 도시 진입축을 강조.
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityStation,
                new Vector3(17f, 0f, 7f), 178f, .58f);
            for (var index = 0; index < 4; index++)
            {
                Environment(landmarks, catalog, EnvironmentVisualKeys.CityLightPole,
                    new Vector3(13f + index * 8f, 0f, -2.5f), 0f, .72f);
            }
            Environment(landmarks, catalog, EnvironmentVisualKeys.CityPlanter,
                new Vector3(29f, 0f, 5f), 21f, .86f);
        }

        private static void BuildRoadsideClusters(Transform parent, WorldVisualCatalog catalog)
        {
            var roadside = Group(parent, "ART1 Roadside Clusters");
            var corridors = new[]
            {
                ("FarmTown", new[] { new Vector3(-50f, 0f, -4f), new Vector3(-46f, 0f, -10f) }),
                ("FarmHub", new[] { new Vector3(-25f, 0f, 13f), new Vector3(-9f, 0f, 5.5f) }),
                ("TownHub", new[] { new Vector3(-24f, 0f, -13f), new Vector3(-9f, 0f, -8f) }),
                ("TownCity", new[] { new Vector3(-7f, 0f, -22f), new Vector3(13f, 0f, -16f) }),
                ("HubCity", new[] { new Vector3(20f, 0f, 1.35f), new Vector3(34f, 0f, -1.35f) }),
            };
            var housePaths = new[] { TownHouse03Path, TownHouse06Path, TownHouse09Path };
            for (var corridorIndex = 0; corridorIndex < corridors.Length; corridorIndex++)
            {
                var corridor = Group(roadside, "Roadside_" + corridors[corridorIndex].Item1);
                var homes = corridors[corridorIndex].Item2;
                for (var homeIndex = 0; homeIndex < homes.Length; homeIndex++)
                {
                    PlaceVendor(corridor,
                        housePaths[(corridorIndex + homeIndex) % housePaths.Length],
                        "RoadsideHouse_" + corridors[corridorIndex].Item1 + "_" + (homeIndex + 1),
                        homes[homeIndex], homeIndex == 0 ? 22f : 202f,
                        corridors[corridorIndex].Item1 == "HubCity" ? .95f : .72f);
                    for (var treeIndex = 0; treeIndex < 3; treeIndex++)
                    {
                        var offset = new Vector3((treeIndex - 1) * .7f, 0f,
                            homeIndex == 0 ? .9f : -.9f);
                        Environment(corridor, catalog,
                            treeIndex == 1
                                ? EnvironmentVisualKeys.CityTreeA
                                : EnvironmentVisualKeys.FarmTreeClusterA,
                            homes[homeIndex] + offset,
                            corridorIndex * 19f + treeIndex * 31f, .58f + treeIndex * .05f);
                    }
                }
            }
        }

        private static void BuildTerrainRelief(Transform parent)
        {
            var relief = Group(parent, "ART1 Terrain Relief");
            var farm = Material("TerrainReliefFarm", new Color(.34f, .47f, .24f));
            var town = Material("TerrainReliefTown", new Color(.43f, .48f, .30f));
            var hub = Material("TerrainReliefHub", new Color(.36f, .37f, .34f));
            var city = Material("TerrainReliefCity", new Color(.29f, .35f, .35f));
            var definitions = new[]
            {
                ("FarmBund_01", new Vector3(-57f, 0f, 27f), 32f, 22f, 6.5f, farm),
                ("FarmBund_02", new Vector3(-28f, 0f, 31f), 38f, 24f, 5.5f, farm),
                ("FarmBund_03", new Vector3(-61f, 0f, 9f), 28f, 18f, 4.5f, farm),
                ("FarmBund_04", new Vector3(-26f, 0f, 7f), 24f, 17f, 4f, farm),
                ("TownHill_01", new Vector3(-57f, 0f, -25f), 31f, 21f, 6f, town),
                ("TownHill_02", new Vector3(-36f, 0f, -39f), 39f, 25f, 7f, town),
                ("TownHill_03", new Vector3(-17f, 0f, -28f), 28f, 19f, 5f, town),
                ("HubCut_01", new Vector3(-3f, 0f, 14f), 27f, 20f, 4.5f, hub),
                ("HubCut_02", new Vector3(8f, 0f, -15f), 34f, 22f, 5.5f, hub),
                ("HubCut_03", new Vector3(22f, 0f, 2.4f), 22f, 10f, 4.5f, hub),
                ("CityRidge_01", new Vector3(32f, 0f, -2.4f), 24f, 10f, 5f, city),
                ("CityRidge_02", new Vector3(54f, 0f, 14f), 40f, 25f, 7.5f, city),
                ("CityRidge_03", new Vector3(59f, 0f, -10f), 33f, 22f, 6f, city),
                ("CityRidge_04", new Vector3(39f, 0f, -17f), 27f, 18f, 4.5f, city),
            };
            foreach (var definition in definitions)
                TerrainMound(relief, definition.Item1, definition.Item2,
                    definition.Item3, definition.Item4, definition.Item5, definition.Item6);
        }

        private static void TerrainMound(
            Transform parent, string name, Vector3 position,
            float radiusX, float radiusZ, float height, Material material)
        {
            const int segments = 16;
            const int rings = 4;
            var vertices = new Vector3[(rings + 1) * segments];
            var triangles = new int[rings * segments * 6];
            for (var ring = 0; ring <= rings; ring++)
            {
                var t = ring / (float)rings;
                for (var segment = 0; segment < segments; segment++)
                {
                    var angle = segment / (float)segments * Mathf.PI * 2f;
                    vertices[ring * segments + segment] = new Vector3(
                        Mathf.Cos(angle) * radiusX * t,
                        height * (1f - t * t),
                        Mathf.Sin(angle) * radiusZ * t);
                }
            }
            var triangleIndex = 0;
            for (var ring = 0; ring < rings; ring++)
            for (var segment = 0; segment < segments; segment++)
            {
                var next = (segment + 1) % segments;
                var inner = ring * segments + segment;
                var innerNext = ring * segments + next;
                var outer = (ring + 1) * segments + segment;
                var outerNext = (ring + 1) * segments + next;
                triangles[triangleIndex++] = inner;
                triangles[triangleIndex++] = innerNext;
                triangles[triangleIndex++] = outerNext;
                triangles[triangleIndex++] = inner;
                triangles[triangleIndex++] = outerNext;
                triangles[triangleIndex++] = outer;
            }
            var mesh = new Mesh { name = name + "_LowPolyMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var mound = new GameObject(name);
            mound.transform.SetParent(parent, false);
            mound.transform.position = Separate(position) + Vector3.down * 2.8f;
            mound.AddComponent<MeshFilter>().sharedMesh = mesh;
            mound.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static (Light Light, Camera Camera) BuildLightingAndCamera(
            Transform parent, IReadOnlyList<거점CompositionSetView> anchorViews)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.66f, .73f, .76f);
            RenderSettings.ambientEquatorColor = new Color(.47f, .49f, .43f);
            RenderSettings.ambientGroundColor = new Color(.24f, .25f, .20f);
            RenderSettings.reflectionIntensity = .72f;
            RenderSettings.fog = false;
            var lightObject = new GameObject("WorldDirectionalLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.28f;
            light.color = new Color(1f, .94f, .84f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .82f;
            light.shadowBias = .035f;
            light.shadowNormalBias = .28f;
            lightObject.transform.eulerAngles = new Vector3(43f, -28f, 0f);

            var cameraObject = new GameObject("ThreeRegionHubCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 43f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 1800f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.65f, .74f, .78f);
            var worldFocus = new GameObject("CameraFocus_ThreeRegionWorld").transform;
            worldFocus.SetParent(parent, false);
            worldFocus.position = anchorViews.Aggregate(Vector3.zero,
                (sum, anchor) => sum + anchor.transform.position) / anchorViews.Count;
            var bindings = new List<DioramaCameraFocusBinding>
            {
                new()
                {
                    AnchorId = WorldFocusId,
                    LevelCode = DioramaCameraFocusLevelCodes.World,
                    Anchor = worldFocus,
                },
            };
            bindings.AddRange(anchorViews.Select(anchor => new DioramaCameraFocusBinding
            {
                AnchorId = "camera-focus:region:" + anchor.Descriptor.PackCode,
                LevelCode = DioramaCameraFocusLevelCodes.Zone,
                Anchor = anchor.transform,
            }));
            var raidFocus = Anchor(parent, "CameraFocus_HubCityRaid", new Vector3(27f, 0f, 0f));
            bindings.Add(new DioramaCameraFocusBinding
            {
                AnchorId = NightRaidFocusId,
                LevelCode = DioramaCameraFocusLevelCodes.Object,
                Anchor = raidFocus,
            });
            var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
            rig.Configure(camera, bindings.ToArray(), WorldFocusId, true);
            rig.ConfigureComposition(50f, 830f, 95f, 50f, 35f, 30f, 28f, 850f);
            rig.ApplyNowForTests();
            return (light, camera);
        }

        private static 야간MonsterRaidPresenter BuildNightRaid(
            Transform parent,
            절차형VehicleRouteFollower truckFollower,
            월드시간대Presenter timeOfDay)
        {
            var group = Group(parent, "NMR4 Hub City Night Raid");
            var visualRoot = Group(group, "RaidVisualRoot").gameObject;
            var monsterPrefab = Load<GameObject>(MonsterPath);
            var boxPrefab = Load<GameObject>(BoxPath);
            var monsters = new Transform[3];
            var spawnAnchors = new Transform[3];
            var blockAnchors = new Transform[3];
            var escapeAnchors = new Transform[3];
            var cargoProps = new GameObject[2];
            var spawn = new[]
            {
                new Vector3(22f, 0f, 2.2f), new Vector3(25f, 0f, -2.2f), new Vector3(30f, 0f, 2f),
            };
            var block = new[]
            {
                new Vector3(24f, 0f, .55f), new Vector3(26f, 0f, -.75f), new Vector3(29f, 0f, .5f),
            };
            var escape = new[]
            {
                new Vector3(20f, 0f, 2.8f), new Vector3(24f, 0f, -2.9f), new Vector3(33f, 0f, 2.8f),
            };
            for (var index = 0; index < monsters.Length; index++)
            {
                spawnAnchors[index] = Anchor(group, $"MonsterSpawn_{index + 1:00}", spawn[index]);
                blockAnchors[index] = Anchor(group, $"MonsterBlock_{index + 1:00}", block[index]);
                escapeAnchors[index] = Anchor(group, $"MonsterEscape_{index + 1:00}", escape[index]);
                var actor = new GameObject($"Monster_{index + 1:00}");
                actor.transform.SetParent(visualRoot.transform, false);
                actor.transform.position = spawnAnchors[index].position;
                monsters[index] = actor.transform;
                var visual = PrefabUtility.InstantiatePrefab(monsterPrefab, actor.transform) as GameObject
                             ?? throw new InvalidOperationException("NightMonsterInstantiateFailed");
                visual.name = "SyntySkeletonVisual";
                visual.transform.localScale = Vector3.one * 2.35f;
                if (index == 0) continue;
                var cargo = PrefabUtility.InstantiatePrefab(boxPrefab, actor.transform) as GameObject
                            ?? throw new InvalidOperationException("NightMonsterCargoInstantiateFailed");
                cargo.name = "LootedCargo";
                cargo.transform.localPosition = new Vector3(.2f, 1.15f, .55f);
                cargo.transform.localRotation = Quaternion.Euler(0f, 18f, -12f);
                cargo.transform.localScale = Vector3.one * 1.25f;
                cargoProps[index - 1] = cargo;
            }
            RaidLight(visualRoot.transform, "RaidWarmLight_West", new Vector3(24f, 0f, 1.2f));
            RaidLight(visualRoot.transform, "RaidWarmLight_East", new Vector3(30f, 0f, -1.2f));
            var intercept = Anchor(group, "TruckIntercept", new Vector3(27f, 0f, 0f));
            var presenter = group.gameObject.AddComponent<야간MonsterRaidPresenter>();
            presenter.Configure(timeOfDay, truckFollower, intercept, visualRoot,
                monsters, spawnAnchors, blockAnchors, escapeAnchors, cargoProps);
            return presenter;
        }

        private static void RaidLight(Transform parent, string name, Vector3 position)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = Separate(position) + Vector3.up * 7f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .63f, .34f);
            light.intensity = 2.4f;
            light.range = 42f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static 야간MonsterRaidPresenter RequireNightRaid()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("NightMonsterRaidPreviewRequiresPlayMode");
            return Object.FindFirstObjectByType<야간MonsterRaidPresenter>(FindObjectsInactive.Include)
                   ?? throw new InvalidOperationException("NightMonsterRaidPresenterMissing");
        }

        private static void FocusCamera(string focusId)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("ThreeRegionFocusRequiresPlayMode");
            var rig = Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                      ?? throw new InvalidOperationException("ThreeRegionCameraRigMissing");
            rig.Focus(focusId);
            rig.ApplyNowForTests();
        }

        private static void FocusNightRaidCamera()
        {
            FocusCamera(NightRaidFocusId);
            var rig = Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                      ?? throw new InvalidOperationException("ThreeRegionCameraRigMissing");
            while (rig.YawQuarterTurns != 0) rig.RotateRight();
            rig.ApplyNowForTests();
        }

        private static void CapturePlayModeTo(string relativePath)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("ThreeRegionCaptureRequiresPlayMode");
            var path = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                                      ?? throw new InvalidOperationException(
                                          "ThreeRegionCaptureDirectoryMissing"));
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log("ThreeRegionGameViewCaptureRequested:" + path);
        }

        private static Transform Group(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static void RegionGround(
            Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.position = Separate(position);
            value.transform.localScale = scale;
            value.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(value.GetComponent<Collider>());
        }

        private static void Road(
            Transform parent, string name, Vector3 start, Vector3 end, float width,
            Material surface, Material shoulder, Material? centerLine)
        {
            Ribbon(parent, name + "_Shoulder", start, end, width + 1.4f, shoulder);
            Ribbon(parent, name + "_Surface", start + Vector3.up * .025f,
                end + Vector3.up * .025f, width, surface);
            if (centerLine != null)
                Ribbon(parent, name + "_CenterLine", start + Vector3.up * .06f,
                    end + Vector3.up * .06f, .13f, centerLine);
        }

        private static WorldVisualInstanceView Environment(
            Transform parent, WorldVisualCatalog catalog, string key,
            Vector3 position, float yaw, float scale)
        {
            var entry = catalog.Resolve(key);
            var wrapper = new GameObject("Environment_" + key);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = Separate(position);
            wrapper.transform.eulerAngles = new Vector3(0f, yaw, 0f);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, visualRoot) as GameObject
                           ?? throw new InvalidOperationException("ART1EnvironmentInstantiateFailed:" + key);
            instance.name = "SyntyPrefabInstance";
            instance.transform.localPosition = entry.LocalPositionCorrection;
            instance.transform.localRotation = Quaternion.Euler(entry.LocalEulerCorrection);
            instance.transform.localScale = entry.LocalScale * scale;
            var view = wrapper.AddComponent<WorldVisualInstanceView>();
            view.Configure(key, catalog, visualRoot, instance);
            return view;
        }

        private static void PlaceVendor(
            Transform parent, string path, string name, Vector3 position, float yaw, float scale)
        {
            var wrapper = new GameObject("Environment_" + name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = Separate(position);
            wrapper.transform.eulerAngles = new Vector3(0f, yaw, 0f);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(Load<GameObject>(path), visualRoot)
                           as GameObject
                           ?? throw new InvalidOperationException("ART1VendorInstantiateFailed:" + path);
            instance.name = "SyntyPrefabInstance";
            instance.transform.localScale = Vector3.one * scale;
        }

        private static void Ribbon(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float width,
            Material material)
        {
            start = Separate(start);
            end = Separate(end);
            var ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = name;
            ribbon.transform.SetParent(parent, false);
            var delta = end - start;
            ribbon.transform.position = (start + end) * .5f;
            ribbon.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            ribbon.transform.localScale = new Vector3(width, .08f, delta.magnitude);
            ribbon.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(ribbon.GetComponent<Collider>());
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            result.position = Separate(position);
            return result;
        }

        private static void Nested(GameObject prefab, Transform parent, Vector3 localPosition, float yaw)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject
                           ?? throw new InvalidOperationException("CMP5NestedPrefabFailed");
            instance.transform.localPosition = localPosition;
            instance.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
        }

        private static void CreateLabel(Transform parent, string text, Vector3 position)
        {
            var root = new GameObject("Label_" + text);
            root.transform.SetParent(parent, false);
            root.transform.position = Separate(position);
            root.transform.eulerAngles = new Vector3(90f, 0f, 0f);
            var label = root.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .12f;
            label.fontSize = 38;
            label.color = new Color(.07f, .06f, .05f, 1f);
            root.SetActive(false);
        }

        private static Material Material(string name, Color color)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException("CMP5ShaderMissing"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Vector3 Separate(Vector3 position)
            => new(position.x * RegionSeparationScale, position.y,
                position.z * RegionSeparationScale);

        private static T Load<T>(string path) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>(path)
               ?? throw new InvalidOperationException("CMP5AssetMissing:" + path);

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
