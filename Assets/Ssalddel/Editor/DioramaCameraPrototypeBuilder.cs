using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class DioramaCameraPrototypeBuilder
    {
        private const string WorldAnchorId = "camera-focus:world.overview";
        private const string FarmAnchorId = "camera-focus:zone.farm";
        private const string LogisticsAnchorId = "camera-focus:zone.logistics";
        private const string MarketAnchorId = "camera-focus:zone.market";
        private const string MarketObjectAnchorId = "camera-focus:object.market-shelf";

        [MenuItem("Ssalddel/WORLD-0/Build Unsaved Camera Prototype")]
        public static void BuildUnsaved()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("WORLD-0_CameraPrototype");
            var zones = new GameObject("ZoneRoots").transform;
            zones.SetParent(root.transform, false);

            CreateGround(root.transform);
            CreateRoute(root.transform);
            CreateFarm(zones);
            CreateLogistics(zones);
            CreateMarket(zones);

            var anchors = new GameObject("CameraFocusAnchors").transform;
            anchors.SetParent(root.transform, false);
            var world = Anchor(anchors, "WorldOverviewFocus", Vector3.up);
            var farm = Anchor(anchors, "FarmZoneFocus", new Vector3(-25f, 2f, 13f));
            var logistics = Anchor(anchors, "LogisticsZoneFocus", new Vector3(3f, 3f, 0f));
            var market = Anchor(anchors, "MarketZoneFocus", new Vector3(25f, 2f, -13f));
            var marketObject = Anchor(anchors, "MarketShelfObjectFocus", new Vector3(23f, 1.2f, -11f));

            var cameraObject = new GameObject("DioramaTopDownCameraRig");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 250f;
            var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
            rig.Configure(camera, new[]
            {
                Binding(WorldAnchorId, DioramaCameraFocusLevelCodes.World, world),
                Binding(FarmAnchorId, DioramaCameraFocusLevelCodes.Zone, farm),
                Binding(LogisticsAnchorId, DioramaCameraFocusLevelCodes.Zone, logistics),
                Binding(MarketAnchorId, DioramaCameraFocusLevelCodes.Zone, market),
                Binding(MarketObjectAnchorId, DioramaCameraFocusLevelCodes.Object, marketObject),
            }, WorldAnchorId);
            rig.Initialize();
            var occlusion = cameraObject.AddComponent<DioramaForegroundOcclusionController>();
            occlusion.Configure(rig);
            occlusion.ApplyNow();

            var light = new GameObject("GlobalDirectionalLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.58f, .61f, .67f);

            Selection.activeGameObject = root;
            SceneView.RepaintAll();
        }

        [MenuItem("Ssalddel/WORLD-0/Focus/World Overview")]
        public static void FocusWorld() => Focus(WorldAnchorId);

        [MenuItem("Ssalddel/WORLD-0/Focus/Farm")]
        public static void FocusFarm() => Focus(FarmAnchorId);

        [MenuItem("Ssalddel/WORLD-0/Focus/Logistics")]
        public static void FocusLogistics() => Focus(LogisticsAnchorId);

        [MenuItem("Ssalddel/WORLD-0/Focus/Market")]
        public static void FocusMarket() => Focus(MarketAnchorId);

        [MenuItem("Ssalddel/WORLD-0/Focus/Market Shelf Object")]
        public static void FocusMarketObject() => Focus(MarketObjectAnchorId);

        [MenuItem("Ssalddel/WORLD-0/Rotate Right 90")]
        public static void RotateRight()
        {
            var rig = FindRig();
            rig.RotateRight();
            rig.ApplyNowForTests();
            ApplyOcclusion();
            SceneView.RepaintAll();
        }

        private static void Focus(string anchorId)
        {
            var rig = FindRig();
            rig.Focus(anchorId);
            rig.ApplyNowForTests();
            ApplyOcclusion();
            SceneView.RepaintAll();
        }

        private static void ApplyOcclusion()
        {
            var controller = Object.FindFirstObjectByType<DioramaForegroundOcclusionController>();
            if (controller != null) controller.ApplyNow();
        }

        private static DioramaTopDownCameraRig FindRig()
            => Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new MissingReferenceException("WORLD-0 camera rig is missing.");

        private static void CreateGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "SharedWorldGround";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(7f, 1f, 4.2f);
        }

        private static void CreateRoute(Transform parent)
        {
            var route = GameObject.CreatePrimitive(PrimitiveType.Cube);
            route.name = "FarmToResidentialRoute";
            route.transform.SetParent(parent, false);
            route.transform.SetPositionAndRotation(new Vector3(0f, .12f, 0f), Quaternion.Euler(0f, -27f, 0f));
            route.transform.localScale = new Vector3(58f, .18f, 3.4f);
        }

        private static void CreateFarm(Transform parent)
        {
            var zone = Zone(parent, "Zone_FarmProduction", new Vector3(-25f, 0f, 13f));
            for (var z = 0; z < 6; z++)
            for (var x = 0; x < 6; x++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"FarmTile_{x}_{z}";
                tile.transform.SetParent(zone, false);
                tile.transform.localPosition = new Vector3((x - 2.5f) * 1.35f, .2f, (z - 2.5f) * 1.35f);
                tile.transform.localScale = new Vector3(1.15f, .35f, 1.15f);
            }

            Block(zone, "FarmBarn", new Vector3(-6f, 2f, 1f), new Vector3(5f, 4f, 4f));
            Block(zone, "FarmSilo", new Vector3(-7f, 2.5f, -4f), new Vector3(2.5f, 5f, 2.5f));
        }

        private static void CreateLogistics(Transform parent)
        {
            var zone = Zone(parent, "Zone_UrbanLogistics", new Vector3(3f, 0f, 0f));
            Block(zone, "LogisticsBuilding", new Vector3(0f, 3f, 2f), new Vector3(12f, 6f, 8f));
            Block(zone, "InboundDock", new Vector3(-4f, .6f, -4f), new Vector3(3f, 1.2f, 3f));
            Block(zone, "InspectionArea", new Vector3(0f, .3f, -4f), new Vector3(3f, .6f, 3f));
            Block(zone, "StorageArea", new Vector3(4f, .8f, -4f), new Vector3(3f, 1.6f, 3f));
        }

        private static void CreateMarket(Transform parent)
        {
            var zone = Zone(parent, "Zone_UrbanMarket", new Vector3(25f, 0f, -13f));
            Block(zone, "MarketCutawayBackWall", new Vector3(0f, 2.4f, 3.25f), new Vector3(9f, 4.8f, .5f))
                .AddComponent<DioramaOcclusionView>();
            Block(zone, "MarketCutawaySideWall", new Vector3(-4.25f, 2.4f, 0f), new Vector3(.5f, 4.8f, 7f))
                .AddComponent<DioramaOcclusionView>();
            Block(zone, "MarketBackroom", new Vector3(-2.5f, .65f, -4.5f), new Vector3(3f, 1.3f, 2.4f));
            Block(zone, "MarketShelf", new Vector3(2f, 1f, 2f), new Vector3(3f, 2f, .8f));
            Block(zone, "ResidentialPickup", new Vector3(9f, 2.5f, -2f), new Vector3(7f, 5f, 6f))
                .AddComponent<DioramaOcclusionView>();
        }

        private static Transform Zone(Transform parent, string name, Vector3 position)
        {
            var zone = new GameObject(name).transform;
            zone.SetParent(parent, false);
            zone.localPosition = position;
            return zone;
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            return item;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            return anchor;
        }

        private static DioramaCameraFocusBinding Binding(string id, string level, Transform anchor)
            => new()
            {
                AnchorId = id,
                LevelCode = level,
                Anchor = anchor,
            };
    }
}
