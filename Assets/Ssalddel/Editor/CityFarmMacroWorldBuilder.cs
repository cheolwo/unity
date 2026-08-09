using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class CityFarmMacroWorldBuilder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CityFarmMacroWorldBlockout.unity";
        private const string MaterialRoot =
            "Assets/Ssalddel/Experiments/CityFarmWorld/Materials";
        private const string WorldFocusAnchorId = "camera-focus:world.city-farm-supply-chain";

        [MenuItem("Ssalddel/WORLD-1/Build City Farm Macro World")]
        public static void Build()
        {
            var layout = 공급망WorldLayoutFixture.Create();
            layout.Validate();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldRoot = new GameObject("WorldBootstrap");
            var worldView = worldRoot.AddComponent<공급망MacroWorldView>();
            CreateGround(worldRoot.transform);
            new GameObject("GlobalVolumeAnchor").transform.SetParent(worldRoot.transform, false);
            new GameObject("SharedPresentationCanvasAnchor").transform.SetParent(worldRoot.transform, false);

            var zoneRoots = new GameObject("ZoneRoots").transform;
            zoneRoots.SetParent(worldRoot.transform, false);
            var zoneViews = layout.Zones
                .OrderBy(value => value.FlowOrder)
                .Select(value => CreateZone(zoneRoots, value))
                .ToArray();

            var routeRoots = new GameObject("SupplyChainRoutes").transform;
            routeRoots.SetParent(worldRoot.transform, false);
            var byId = layout.Zones.ToDictionary(value => value.StableId, StringComparer.Ordinal);
            var routeViews = layout.RouteLegs
                .OrderBy(value => value.FlowOrder)
                .Select(value => CreateRoute(routeRoots, value, byId))
                .ToArray();

            var worldAnchor = new GameObject("WorldOverviewFocusAnchor").transform;
            worldAnchor.SetParent(worldRoot.transform, false);
            worldAnchor.position = new Vector3(2f, 2f, 0f);
            var focusBindings = new List<DioramaCameraFocusBinding>
            {
                Binding(WorldFocusAnchorId, DioramaCameraFocusLevelCodes.World, worldAnchor),
            };
            focusBindings.AddRange(zoneViews.Select(value => Binding(
                "camera-focus:zone." + value.PresentationZoneCode,
                DioramaCameraFocusLevelCodes.Zone,
                value.FocusAnchor)));

            var cameraObject = new GameObject("DioramaTopDownCameraRig");
            cameraObject.transform.SetParent(worldRoot.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300f;
            camera.allowHDR = true;
            var cameraRig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
            cameraRig.Configure(camera, focusBindings.ToArray(), WorldFocusAnchorId);
            cameraRig.Initialize();
            var occlusion = cameraObject.AddComponent<DioramaForegroundOcclusionController>();
            occlusion.Configure(cameraRig);
            occlusion.ApplyNow();

            CreateLighting(worldRoot.transform);
            worldView.Configure(zoneViews, routeViews, cameraRig);
            if (!worldView.ValidateWiring())
                throw new InvalidOperationException("SupplyChainMacroWorldWiringInvalid");

            System.IO.Directory.CreateDirectory("Assets/Ssalddel/Experiments/CityFarmWorld");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("SupplyChainMacroWorldSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = worldRoot;
            SceneView.RepaintAll();
        }

        [MenuItem("Ssalddel/WORLD-1/Focus/World Overview")]
        public static void FocusWorld() => Focus(WorldFocusAnchorId);

        [MenuItem("Ssalddel/WORLD-1/Focus/Farm Production")]
        public static void FocusFarmProduction()
            => Focus("camera-focus:zone." + 공급망PresentationZoneCodes.FarmProduction);

        [MenuItem("Ssalddel/WORLD-1/Focus/Farm Yard")]
        public static void FocusFarmYard()
            => Focus("camera-focus:zone." + 공급망PresentationZoneCodes.FarmYard);

        [MenuItem("Ssalddel/WORLD-1/Focus/Transport Corridor")]
        public static void FocusTransport()
            => Focus("camera-focus:zone." + 공급망PresentationZoneCodes.TransportCorridor);

        [MenuItem("Ssalddel/WORLD-1/Focus/Urban Logistics")]
        public static void FocusLogistics()
            => Focus("camera-focus:zone." + 공급망PresentationZoneCodes.UrbanLogistics);

        [MenuItem("Ssalddel/WORLD-1/Focus/Urban Market")]
        public static void FocusMarket()
            => Focus("camera-focus:zone." + 공급망PresentationZoneCodes.UrbanMarket);

        [MenuItem("Ssalddel/WORLD-1/Focus/Residential Community")]
        public static void FocusResidential()
            => Focus("camera-focus:zone." + 공급망PresentationZoneCodes.ResidentialCommunity);

        private static void Focus(string anchorId)
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new MissingReferenceException("WORLD-1 camera rig is missing.");
            rig.Focus(anchorId);
            rig.ApplyNowForTests();
            var occlusion = UnityEngine.Object.FindFirstObjectByType<DioramaForegroundOcclusionController>();
            if (occlusion != null) occlusion.ApplyNow();
            SceneView.RepaintAll();
        }

        private static 공급망WorldZoneView CreateZone(
            Transform parent,
            공급망WorldZoneDefinition definition)
        {
            var root = new GameObject(ZoneRootName(definition.PresentationZoneCode));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Point(definition.Center);
            var view = root.AddComponent<공급망WorldZoneView>();
            var focus = new GameObject("ZoneFocusAnchor").transform;
            focus.SetParent(root.transform, false);
            focus.localPosition = new Vector3(0f, FocusHeight(definition.PresentationZoneCode), 0f);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            CreateZoneVisuals(definition.PresentationZoneCode, visualRoot);
            view.Configure(definition, focus, visualRoot);
            return view;
        }

        private static 공급망WorldRouteView CreateRoute(
            Transform parent,
            공급망WorldRouteLegDefinition definition,
            IReadOnlyDictionary<string, 공급망WorldZoneDefinition> zones)
        {
            var root = new GameObject("Route_" + definition.FlowOrder + "_"
                + zones[definition.FromZoneStableId].PresentationZoneCode + "_To_"
                + zones[definition.ToZoneStableId].PresentationZoneCode);
            root.transform.SetParent(parent, false);
            var visualRoot = new GameObject("RouteVisualRoot").transform;
            visualRoot.SetParent(root.transform, false);

            var from = Point(zones[definition.FromZoneStableId].Center);
            var to = Point(zones[definition.ToZoneStableId].Center);
            var delta = to - from;
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "RouteSurface";
            surface.transform.SetParent(visualRoot, false);
            surface.transform.position = Vector3.Lerp(from, to, .5f) + Vector3.up * .12f;
            surface.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            surface.transform.localScale = new Vector3(2.6f, .2f, delta.magnitude);
            AssignMaterial(surface, EnsureMaterial("Route", new Color(.24f, .29f, .34f)));

            var view = root.AddComponent<공급망WorldRouteView>();
            view.Configure(definition, visualRoot);
            return view;
        }

        private static void CreateZoneVisuals(string code, Transform parent)
        {
            switch (code)
            {
                case 공급망PresentationZoneCodes.FarmProduction:
                    CreateFarmProduction(parent);
                    return;
                case 공급망PresentationZoneCodes.FarmYard:
                    CreateFarmYard(parent);
                    return;
                case 공급망PresentationZoneCodes.TransportCorridor:
                    CreateTransportCorridor(parent);
                    return;
                case 공급망PresentationZoneCodes.UrbanLogistics:
                    CreateLogistics(parent);
                    return;
                case 공급망PresentationZoneCodes.UrbanMarket:
                    CreateMarket(parent);
                    return;
                case 공급망PresentationZoneCodes.ResidentialCommunity:
                    CreateResidential(parent);
                    return;
                default:
                    throw new InvalidOperationException("SupplyChainPresentationZoneUnknown:" + code);
            }
        }

        private static void CreateFarmProduction(Transform parent)
        {
            for (var z = 0; z < 6; z++)
            for (var x = 0; x < 6; x++)
            {
                Block(parent, $"FarmTile_{x}_{z}",
                    new Vector3((x - 2.5f) * 1.25f, .18f, (z - 2.5f) * 1.25f),
                    new Vector3(1.05f, .3f, 1.05f),
                    EnsureMaterial("FarmSoil", new Color(.42f, .23f, .12f)));
            }
            Block(parent, "FarmBarnBlock", new Vector3(-5.5f, 1.8f, 1.5f), new Vector3(4f, 3.6f, 4f),
                EnsureMaterial("FarmBuilding", new Color(.58f, .19f, .12f)));
            Block(parent, "FarmSiloBlock", new Vector3(-5.8f, 2.5f, -3f), new Vector3(2f, 5f, 2f),
                EnsureMaterial("FarmMetal", new Color(.58f, .63f, .61f)));
        }

        private static void CreateFarmYard(Transform parent)
        {
            var material = EnsureMaterial("FarmYard", new Color(.72f, .49f, .18f));
            Block(parent, "PackingArea", Vector3.up * .2f, new Vector3(8f, .35f, 7f), material);
            Block(parent, "ProduceStandBlock", new Vector3(-3f, 1.5f, 1.5f), new Vector3(3f, 3f, 3f), material);
            Block(parent, "FarmVehicleBay", new Vector3(3f, .3f, -1.8f), new Vector3(4f, .55f, 3f), material);
            for (var index = 0; index < 4; index++)
                Block(parent, "PotatoCargoPlaceholder_" + index,
                    new Vector3(-1.5f + index, .55f, -2.4f), Vector3.one * .9f,
                    EnsureMaterial("Cargo", new Color(.83f, .68f, .3f)));
        }

        private static void CreateTransportCorridor(Transform parent)
        {
            var material = EnsureMaterial("Transport", new Color(.22f, .5f, .39f));
            Block(parent, "FarmGateLeft", new Vector3(-3f, 1.5f, 0f), new Vector3(1f, 3f, 1f), material);
            Block(parent, "FarmGateRight", new Vector3(3f, 1.5f, 0f), new Vector3(1f, 3f, 1f), material);
            Block(parent, "VehiclePlaceholder", new Vector3(0f, .7f, -1.5f), new Vector3(3.5f, 1.4f, 2f), material);
        }

        private static void CreateLogistics(Transform parent)
        {
            var material = EnsureMaterial("Logistics", new Color(.18f, .42f, .65f));
            Block(parent, "LogisticsBackWall", new Vector3(0f, 3f, 4f), new Vector3(13f, 6f, .6f), material)
                .AddComponent<DioramaOcclusionView>();
            Block(parent, "LogisticsSideWall", new Vector3(-6.2f, 3f, 0f), new Vector3(.6f, 6f, 8f), material)
                .AddComponent<DioramaOcclusionView>();
            Block(parent, "InboundDock", new Vector3(-4f, .6f, -3.5f), new Vector3(3f, 1.2f, 3f), material);
            Block(parent, "InspectionArea", new Vector3(0f, .3f, -3.5f), new Vector3(3f, .6f, 3f), material);
            Block(parent, "StorageArea", new Vector3(4f, .8f, -3.5f), new Vector3(3f, 1.6f, 3f), material);
        }

        private static void CreateMarket(Transform parent)
        {
            var material = EnsureMaterial("Market", new Color(.78f, .37f, .14f));
            Block(parent, "MarketBackWall", new Vector3(0f, 2.5f, 3.5f), new Vector3(10f, 5f, .5f), material)
                .AddComponent<DioramaOcclusionView>();
            Block(parent, "MarketSideWall", new Vector3(-4.75f, 2.5f, 0f), new Vector3(.5f, 5f, 7f), material)
                .AddComponent<DioramaOcclusionView>();
            Block(parent, "MarketBackroom", new Vector3(-2.5f, .7f, -3f), new Vector3(3f, 1.4f, 2.5f), material);
            for (var index = 0; index < 3; index++)
                Block(parent, "MarketShelf_" + index,
                    new Vector3(1.5f + index * 1.2f, 1f, 1.5f), new Vector3(.8f, 2f, 3f), material);
        }

        private static void CreateResidential(Transform parent)
        {
            var material = EnsureMaterial("Residential", new Color(.48f, .32f, .65f));
            Block(parent, "ApartmentTowerA", new Vector3(-3f, 3.5f, 2f), new Vector3(5f, 7f, 5f), material)
                .AddComponent<DioramaOcclusionView>();
            Block(parent, "ApartmentTowerB", new Vector3(3f, 4.5f, 1f), new Vector3(5f, 9f, 5f), material)
                .AddComponent<DioramaOcclusionView>();
            Block(parent, "ResidentialPickupPoint", new Vector3(0f, .5f, -4f), new Vector3(4f, 1f, 3f), material);
            Block(parent, "RepresentativePlaceholder", new Vector3(-2f, 1f, -4f), new Vector3(.8f, 2f, .8f), material);
        }

        private static void CreateGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "SharedWorldGround";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(2f, 0f, 0f);
            ground.transform.localScale = new Vector3(10.5f, 1f, 6.5f);
            AssignMaterial(ground, EnsureMaterial("Ground", new Color(.52f, .57f, .49f)));
        }

        private static void CreateLighting(Transform parent)
        {
            var lightObject = new GameObject("GlobalLighting");
            lightObject.transform.SetParent(parent, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.58f, .61f, .67f);
        }

        private static GameObject Block(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material = null!)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            if (material != null) AssignMaterial(item, material);
            return item;
        }

        private static void AssignMaterial(GameObject target, Material material)
            => target.GetComponent<Renderer>().sharedMaterial = material;

        private static Material EnsureMaterial(string name, Color color)
        {
            System.IO.Directory.CreateDirectory(MaterialRoot);
            var path = MaterialRoot + "/WORLD-1-" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? throw new InvalidOperationException("WORLD1BlockoutShaderMissing");
                material = new Material(shader) { name = "WORLD-1-" + name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static string ZoneRootName(string code)
            => code switch
            {
                공급망PresentationZoneCodes.FarmProduction => "Zone_FarmProduction",
                공급망PresentationZoneCodes.FarmYard => "Zone_FarmYard",
                공급망PresentationZoneCodes.TransportCorridor => "Zone_TransportCorridor",
                공급망PresentationZoneCodes.UrbanLogistics => "Zone_UrbanLogistics",
                공급망PresentationZoneCodes.UrbanMarket => "Zone_UrbanMarket",
                공급망PresentationZoneCodes.ResidentialCommunity => "Zone_ResidentialCommunity",
                _ => throw new InvalidOperationException("SupplyChainPresentationZoneUnknown:" + code),
            };

        private static float FocusHeight(string code)
            => code == 공급망PresentationZoneCodes.UrbanLogistics
                || code == 공급망PresentationZoneCodes.ResidentialCommunity
                ? 3f
                : 2f;

        private static Vector3 Point(DioramaPoint value)
            => new(value.X, value.Y, value.Z);

        private static DioramaCameraFocusBinding Binding(string id, string level, Transform anchor)
            => new()
            {
                AnchorId = id,
                LevelCode = level,
                Anchor = anchor,
            };
    }
}
