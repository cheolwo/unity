using System.IO;
using Ssalddel.Unity.Samples.NpcMovement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter.Editor
{
    public static class 도심물류센터PrimitiveSceneBuilder
    {
        private const string SceneDirectory = "Assets/Ssalddel/Scenes";
        private const string ScenePath = SceneDirectory + "/UrbanLogisticsCenterPrimitive.unity";

        [MenuItem("Ssalddel/Samples/Create Urban Logistics Center Primitive Scene")]
        public static void CreateScene()
        {
            if (!CanReplaceCurrentScene())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("UrbanLogisticsCenterZone");
            CreateGround(root.transform);
            CreateDock("InboundDock", root.transform, new Vector3(-6f, 0.5f, 3f), new Color(0.25f, 0.46f, 0.62f));
            CreateDock("SortingZone", root.transform, new Vector3(0f, 0.5f, 3f), new Color(0.52f, 0.48f, 0.26f));
            CreateDock("OutboundDock", root.transform, new Vector3(6f, 0.5f, 3f), new Color(0.32f, 0.58f, 0.34f));

            var roleTargets = new[]
            {
                CreateRoleTarget("Transport_71", "transport:71", root.transform, new Vector3(0f, 0.8f, -2f), new Vector3(3.4f, 1.4f, 1.8f)),
                CreateRoleTarget("PickupStop_71", "transport-stop:71.pickup", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(2.6f, 0.5f, 2.6f)),
                CreateRoleTarget("DropoffStop_71", "transport-stop:71.dropoff", root.transform, new Vector3(7f, 0.25f, -3f), new Vector3(2.6f, 0.5f, 2.6f)),
            };

            var waypointRegistry = CreateWaypoints(root.transform);
            var npcView = CreateTransporterNpc(root.transform, waypointRegistry);
            var npcController = root.AddComponent<ZoneNpcMovementController>();
            npcController.Configure(new[] { npcView });
            var corridorRegistry = CreateTransportCorridorWaypoints(root.transform);
            var truckView = CreateTransportTruck(root.transform, corridorRegistry);
            var interactionPanel = CreateInteractionPanel(root.transform);
            var facilityOverview = CreateFacilityOverview(root.transform);

            var zoneView = root.AddComponent<도심물류센터View>();
            zoneView.Configure(roleTargets, interactionPanel, npcController, truckView, facilityOverview);
            root.AddComponent<도심물류센터SceneController>();
            var tokenProvider = root.AddComponent<RuntimeSessionAccessTokenProvider>();
            var lifetimeScope = root.AddComponent<도심물류센터LifetimeScope>();
            lifetimeScope.ConfigureSimulationApi(tokenProvider);

            CreateCamera();
            CreateLight();
            Directory.CreateDirectory(SceneDirectory);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Created urban logistics center primitive scene. Bake NavMesh before movement validation: " + ScenePath);
        }

        [MenuItem("Ssalddel/Samples/Validate Urban Logistics Center Primitive Scene")]
        public static void ValidateGeneratedScene()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException("Urban logistics center scene was not generated.", ScenePath);
            }

            if (!CanReplaceCurrentScene())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<도심물류센터View>();
            var controller = Object.FindFirstObjectByType<도심물류센터SceneController>();
            var scope = Object.FindFirstObjectByType<도심물류센터LifetimeScope>();
            var tokenProvider = Object.FindFirstObjectByType<RuntimeSessionAccessTokenProvider>();
            var npc = Object.FindFirstObjectByType<NpcMovementView>();
            var truck = Object.FindFirstObjectByType<TransportCorridorTruckView>(FindObjectsInactive.Include);
            var facility = Object.FindFirstObjectByType<LogisticsFacilityOverviewView>();
            if (view == null || controller == null || scope == null || tokenProvider == null
                || npc == null || truck == null || facility == null
                || !truck.ValidateWiring() || !facility.ValidateWiring() || !view.ValidateWiring())
            {
                throw new MissingReferenceException("Urban logistics center wiring is invalid after scene reload.");
            }

            Debug.Log("Validated urban logistics center Role View, waypoint, NPC, and transport-corridor truck wiring. NavMesh bake remains a project step.");
        }

        public static LogisticsFacilityOverviewView CreateFacilityOverviewForTests()
        {
            var root = new GameObject("LogisticsFacilityOverviewTestRoot");
            return CreateFacilityOverview(root.transform);
        }

        private static ZoneNpcWaypointRegistry CreateWaypoints(Transform parent)
        {
            var registryRoot = new GameObject("NpcWaypoints");
            registryRoot.transform.SetParent(parent, false);
            var values = new[]
            {
                CreateWaypoint("VehicleGate", "logistics.vehicle-gate", registryRoot.transform, new Vector3(-7f, 0.1f, -5f)),
                CreateWaypoint("LoadingBay", "logistics.loading-bay", registryRoot.transform, new Vector3(0f, 0.1f, -1f)),
                CreateWaypoint("VehicleExit", "logistics.vehicle-exit", registryRoot.transform, new Vector3(7f, 0.1f, -5f)),
            };
            var registry = registryRoot.AddComponent<ZoneNpcWaypointRegistry>();
            registry.Configure(values);
            return registry;
        }

        private static NpcWaypointView CreateWaypoint(
            string name,
            string key,
            Transform parent,
            Vector3 position)
        {
            var marker = Primitive(name, parent, position, new Vector3(0.4f, 0.2f, 0.4f), new Color(0.95f, 0.65f, 0.12f));
            var view = marker.AddComponent<NpcWaypointView>();
            view.Configure(key);
            return view;
        }

        private static NpcMovementView CreateTransporterNpc(
            Transform parent,
            ZoneNpcWaypointRegistry registry)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "TransporterNpc_71";
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(-7f, 1f, -5f);
            root.GetComponent<Renderer>().sharedMaterial = CreateMaterial("TransporterNpcMaterial", new Color(0.15f, 0.45f, 0.8f));
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.acceleration = 8f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.2f;
            var animator = root.AddComponent<Animator>();
            var view = root.AddComponent<NpcMovementView>();
            view.Configure(
                "npc:transport-driver.71",
                agent,
                animator,
                registry,
                new NpcActionAnimationBinding[0]);
            return view;
        }

        private static ZoneNpcWaypointRegistry CreateTransportCorridorWaypoints(Transform parent)
        {
            var root = new GameObject("TransportCorridorWaypoints");
            root.transform.SetParent(parent, false);
            var values = new[]
            {
                CreateWaypoint("NetworkLogisticsCenter", "network.logistics-center", root.transform, new Vector3(-9f, 0.1f, 6f)),
                CreateWaypoint("NetworkWarehouse", "network.warehouse", root.transform, new Vector3(9f, 0.1f, 6f)),
            };
            var registry = root.AddComponent<ZoneNpcWaypointRegistry>();
            registry.Configure(values);
            return registry;
        }

        private static TransportCorridorTruckView CreateTransportTruck(
            Transform parent,
            ZoneNpcWaypointRegistry registry)
        {
            var root = Primitive(
                "TransportTruck_71",
                parent,
                new Vector3(-9f, 0.7f, 6f),
                new Vector3(2.4f, 1.2f, 1.3f),
                new Color(0.18f, 0.38f, 0.72f));
            var cargo = Primitive(
                "CargoVisualRoot",
                root.transform,
                new Vector3(-0.15f, 0.65f, 0f),
                new Vector3(0.8f, 0.55f, 0.8f),
                new Color(0.72f, 0.48f, 0.2f),
                true);
            var label = CreateText(
                "TruckStatus",
                root.transform,
                "SIMULATED TRUCK",
                new Vector3(0f, 1.35f, 0f),
                0.025f,
                Color.white);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 5f;
            agent.acceleration = 8f;
            agent.angularSpeed = 240f;
            agent.stoppingDistance = 0.3f;
            var animator = root.AddComponent<Animator>();
            var view = root.AddComponent<TransportCorridorTruckView>();
            view.Configure(
                "truck-projection:cargo:transport-71",
                agent,
                animator,
                registry,
                cargo.transform,
                label);
            root.SetActive(false);
            return view;
        }

        private static LogisticsRoleTargetView CreateRoleTarget(
            string name,
            string stableId,
            Transform parent,
            Vector3 position,
            Vector3 scale)
        {
            var root = Primitive(name, parent, position, scale, new Color(0.38f, 0.4f, 0.42f));
            var badge = Primitive(
                "RoleBadge",
                root.transform,
                new Vector3(0f, 1.1f, 0f),
                new Vector3(1.8f, 0.35f, 0.18f),
                Color.gray,
                true);
            var text = CreateText(
                "RoleLabel",
                badge.transform,
                string.Empty,
                new Vector3(0f, 0f, -0.7f),
                0.025f,
                Color.white,
                true);
            var view = root.AddComponent<LogisticsRoleTargetView>();
            view.Configure(stableId, badge, text, badge.GetComponent<Renderer>());
            badge.SetActive(false);
            return view;
        }

        private static LogisticsInteractionPanelView CreateInteractionPanel(Transform parent)
        {
            var root = Primitive(
                "InteractionPanel",
                parent,
                new Vector3(-7f, 2f, 4.8f),
                new Vector3(4f, 3.2f, 0.3f),
                new Color(0.12f, 0.16f, 0.2f));
            var text = CreateText(
                "InteractionText",
                root.transform,
                "TRANSPORTER ACTIONS",
                new Vector3(0f, 0f, -0.7f),
                0.025f,
                Color.white,
                true);
            var view = root.AddComponent<LogisticsInteractionPanelView>();
            view.Configure(text);
            return view;
        }

        private static LogisticsFacilityOverviewView CreateFacilityOverview(Transform parent)
        {
            var root = new GameObject("LogisticsFacilityOverview");
            root.transform.SetParent(parent, false);
            var building = Primitive(
                "WarehouseBuildingVisualRoot",
                root.transform,
                new Vector3(0f, 2.8f, 5.4f),
                new Vector3(18f, 4.2f, 1.2f),
                new Color(0.28f, 0.31f, 0.34f));
            var summary = CreateText(
                "FacilitySummary",
                building.transform,
                "LOGISTICS FACILITY",
                new Vector3(0f, 0.22f, -0.65f),
                0.02f,
                Color.white,
                true);
            var boundary = CreateText(
                "FacilityBoundary",
                building.transform,
                string.Empty,
                new Vector3(0f, -0.22f, -0.65f),
                0.012f,
                Color.white,
                true);
            var areas = new[]
            {
                FacilityArea(root.transform, "VehicleGate", Ssalddel.Unity.Transport.LogisticsFacilityAreaCodes.VehicleGate, new Vector3(-6.6f, .35f, 4.2f)),
                FacilityArea(root.transform, "InboundDock", Ssalddel.Unity.Transport.LogisticsFacilityAreaCodes.InboundDock, new Vector3(-2.2f, .35f, 4.2f)),
                FacilityArea(root.transform, "Inspection", Ssalddel.Unity.Transport.LogisticsFacilityAreaCodes.Inspection, new Vector3(2.2f, .35f, 4.2f)),
                FacilityArea(root.transform, "Storage", Ssalddel.Unity.Transport.LogisticsFacilityAreaCodes.Storage, new Vector3(6.6f, .35f, 4.2f)),
            };
            var cargo = Primitive(
                "FacilityCargoVisualRoot",
                root.transform,
                areas[0].CargoAnchor.position + new Vector3(0f, .7f, 0f),
                new Vector3(.8f, .8f, .8f),
                new Color(.72f, .48f, .2f));
            var view = root.AddComponent<LogisticsFacilityOverviewView>();
            view.Configure(
                building,
                cargo,
                summary,
                boundary,
                areas,
                new[]
                {
                    StateMaterial(Ssalddel.Unity.Transport.LogisticsFacilityAreaStateCodes.Idle, new Color(.28f, .3f, .32f)),
                    StateMaterial(Ssalddel.Unity.Transport.LogisticsFacilityAreaStateCodes.Next, new Color(.68f, .52f, .18f)),
                    StateMaterial(Ssalddel.Unity.Transport.LogisticsFacilityAreaStateCodes.Active, new Color(.12f, .62f, .76f)),
                    StateMaterial(Ssalddel.Unity.Transport.LogisticsFacilityAreaStateCodes.Completed, new Color(.22f, .58f, .3f)),
                });
            return view;
        }

        private static LogisticsFacilityAreaBinding FacilityArea(
            Transform parent,
            string name,
            string areaCode,
            Vector3 position)
        {
            var visual = Primitive(name + "VisualRoot", parent, position,
                new Vector3(3.6f, .7f, 2.2f), Color.gray);
            var anchor = new GameObject(name + "CargoAnchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = position + new Vector3(0f, .7f, 0f);
            var label = CreateText(name + "Status", visual.transform, string.Empty,
                new Vector3(0f, .65f, -.65f), .018f, Color.white, true);
            return new LogisticsFacilityAreaBinding
            {
                AreaCode = areaCode,
                VisualRoot = visual,
                CargoAnchor = anchor.transform,
                StatusRenderer = visual.GetComponent<Renderer>(),
                StatusLabel = label,
            };
        }

        private static LogisticsFacilityStateMaterialBinding StateMaterial(string state, Color color)
            => new LogisticsFacilityStateMaterialBinding
            {
                StateCode = state,
                Material = CreateMaterial("LogisticsFacility" + state, color),
            };

        private static void CreateGround(Transform parent)
        {
            var ground = Primitive(
                "Ground",
                parent,
                new Vector3(0f, -0.25f, 0f),
                new Vector3(20f, 0.5f, 14f),
                new Color(0.62f, 0.64f, 0.66f));
            ground.isStatic = true;
        }

        private static void CreateDock(string name, Transform parent, Vector3 position, Color color)
        {
            Primitive(name, parent, position, new Vector3(4.5f, 1f, 3f), color);
            CreateText(name + "Label", parent, name, position + new Vector3(0f, 1.1f, 0f), 0.035f, Color.white);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            cameraObject.transform.position = new Vector3(0f, 15f, -17f);
            cameraObject.transform.LookAt(Vector3.zero);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static GameObject Primitive(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool local = false)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            if (local)
            {
                value.transform.localPosition = position;
            }
            else
            {
                value.transform.position = position;
            }

            value.transform.localScale = scale;
            value.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name + "Material", color);
            return value;
        }

        private static TextMesh CreateText(
            string name,
            Transform parent,
            string value,
            Vector3 position,
            float size,
            Color color,
            bool local = false)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            if (local)
            {
                root.transform.localPosition = position;
            }
            else
            {
                root.transform.position = position;
            }

            var text = root.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = size;
            text.color = color;
            return text;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader)
            {
                name = name,
                color = color,
            };
        }

        private static bool CanReplaceCurrentScene()
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new System.InvalidOperationException(
                        "Batch mode refuses to replace a modified scene: " + scene.name);
                }
            }

            return true;
        }
    }
}
