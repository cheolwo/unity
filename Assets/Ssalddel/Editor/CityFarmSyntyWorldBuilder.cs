using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    public static class CityFarmSyntyWorldBuilder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시신티월드시제품.unity";
        public const string FarmCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/FarmVisualCatalog.asset";
        public const string UrbanCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/UrbanVisualCatalog.asset";
        public const string TransitionCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/TransitionVisualCatalog.asset";
        public const string VolumeProfilePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Profiles/WORLD-2-GlobalVolumeProfile.asset";

        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs/";

        [MenuItem("Ssalddel/WORLD-2/Build Synty City Farm World")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CityFarmMacroWorldBuilder.ScenePath) == null)
                throw new InvalidOperationException("WORLD1MacroWorldSceneMissing");

            var farm = EnsureFarmCatalog();
            var urban = EnsureUrbanCatalog();
            var transition = EnsureTransitionCatalog();
            var scene = EditorSceneManager.OpenScene(
                CityFarmMacroWorldBuilder.ScenePath,
                OpenSceneMode.Single);
            var world = UnityEngine.Object.FindFirstObjectByType<공급망MacroWorldView>()
                ?? throw new InvalidOperationException("WORLD1MacroWorldViewMissing");

            var zones = world.Zones.ToDictionary(
                value => value.PresentationZoneCode,
                StringComparer.Ordinal);
            BuildFarmProduction(zones[공급망PresentationZoneCodes.FarmProduction], farm);
            BuildFarmYard(zones[공급망PresentationZoneCodes.FarmYard], farm);
            BuildTransport(zones[공급망PresentationZoneCodes.TransportCorridor], urban);
            BuildLogistics(zones[공급망PresentationZoneCodes.UrbanLogistics], urban);
            BuildMarket(zones[공급망PresentationZoneCodes.UrbanMarket], urban);
            BuildResidential(zones[공급망PresentationZoneCodes.ResidentialCommunity], urban);
            BuildRoutes(world, zones, transition);
            ConfigureVolume(world.CameraRig.GetComponent<Camera>());

            world.CameraRig.Initialize();
            world.CameraRig.Focus("camera-focus:world.city-farm-supply-chain");
            world.CameraRig.ApplyNowForTests();
            var occlusion = world.CameraRig.GetComponent<DioramaForegroundOcclusionController>();
            if (occlusion != null) occlusion.ApplyNow();

            if (!world.ValidateWiring())
                throw new InvalidOperationException("WORLD2MacroWorldWiringInvalid");
            ValidateScene(farm, urban, transition);
            System.IO.Directory.CreateDirectory(
                "Assets/Ssalddel/Experiments - 연구/CityFarmWorld");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("WORLD2SceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = world.gameObject;
            SceneView.RepaintAll();
        }

        [MenuItem("Ssalddel/WORLD-2/Validate Synty City Farm World")]
        public static void ValidateOpenScene()
        {
            ValidateScene(
                LoadCatalog(FarmCatalogPath),
                LoadCatalog(UrbanCatalogPath),
                LoadCatalog(TransitionCatalogPath));
        }

        private static void BuildFarmProduction(공급망WorldZoneView zone, WorldVisualCatalog catalog)
        {
            Clear(zone.VisualRoot);
            for (var z = 0; z < 6; z++)
            for (var x = 0; x < 6; x++)
            {
                var position = new Vector3((x - 2.5f) * 1.25f, .02f, (z - 2.5f) * 1.25f);
                Visual(zone.VisualRoot, catalog, FarmVisualKeys.SoilRows,
                    $"FarmSoilRow_{x}_{z}", position, Vector3.zero);
                if ((x + z) % 3 != 0)
                {
                    var stage = z < 2 ? FarmVisualKeys.PotatoSmall
                        : z < 4 ? FarmVisualKeys.PotatoMedium
                        : FarmVisualKeys.PotatoLarge;
                    Visual(zone.VisualRoot, catalog, stage,
                        $"PotatoPresentation_{x}_{z}", position + Vector3.up * .08f,
                        new Vector3(0f, (x * 37 + z * 19) % 360, 0f));
                }
            }

            Visual(zone.VisualRoot, catalog, FarmVisualKeys.Barn, "BarnVisual",
                new Vector3(-6f, 0f, 1.8f), new Vector3(0f, 18f, 0f));
            Visual(zone.VisualRoot, catalog, FarmVisualKeys.Silo, "SiloVisual",
                new Vector3(-6.5f, 0f, -3.3f), Vector3.zero);
            Visual(zone.VisualRoot, catalog, FarmVisualKeys.Farmer, "FarmerVisual",
                new Vector3(4.2f, 0f, -2.8f), new Vector3(0f, -40f, 0f));
        }

        private static void BuildFarmYard(공급망WorldZoneView zone, WorldVisualCatalog catalog)
        {
            Clear(zone.VisualRoot);
            Visual(zone.VisualRoot, catalog, FarmVisualKeys.ProduceStand, "ProduceStandVisual",
                new Vector3(-3f, 0f, 1.5f), new Vector3(0f, 25f, 0f));
            Visual(zone.VisualRoot, catalog, FarmVisualKeys.Tractor, "TractorVisual",
                new Vector3(2.8f, 0f, -1.4f), new Vector3(0f, -55f, 0f));
            for (var index = 0; index < 5; index++)
                Visual(zone.VisualRoot, catalog, FarmVisualKeys.PotatoBox,
                    "PotatoBoxVisual_" + index,
                    new Vector3(-1.5f + index * .75f, 0f, -2.8f),
                    new Vector3(0f, index * 17f, 0f));
        }

        private static void BuildTransport(공급망WorldZoneView zone, WorldVisualCatalog catalog)
        {
            Clear(zone.VisualRoot);
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Van, "TransportVanVisual",
                new Vector3(0f, 0f, -1f), new Vector3(0f, -62f, 0f));
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.CargoBox, "TransportCargoVisual",
                new Vector3(-1.8f, 0f, -1.4f), Vector3.zero);
        }

        private static void BuildLogistics(공급망WorldZoneView zone, WorldVisualCatalog catalog)
        {
            Clear(zone.VisualRoot);
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.LogisticsBuilding,
                "LogisticsBuildingVisual", new Vector3(0f, 0f, 2.6f),
                new Vector3(0f, 180f, 0f));
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Van, "InboundVanVisual",
                new Vector3(-4.2f, 0f, -3.2f), new Vector3(0f, 90f, 0f));
            for (var index = 0; index < 3; index++)
            {
                var pallet = Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Pallet,
                    "LogisticsPalletVisual_" + index,
                    new Vector3(-.5f + index * 2.1f, 0f, -3.1f), Vector3.zero);
                Visual(pallet.VisualRoot, catalog, UrbanVisualKeys.CargoBox,
                    "LogisticsCargoVisual_" + index, new Vector3(0f, .35f, 0f),
                    new Vector3(0f, index * 30f, 0f));
            }
        }

        private static void BuildMarket(공급망WorldZoneView zone, WorldVisualCatalog catalog)
        {
            Clear(zone.VisualRoot);
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.MarketBuilding,
                "MarketBuildingVisual", new Vector3(0f, 0f, 2.8f),
                new Vector3(0f, 180f, 0f), true);
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Desk, "MarketDeskVisual",
                new Vector3(-3.4f, 0f, -2.5f), new Vector3(0f, 15f, 0f));
            for (var index = 0; index < 3; index++)
                Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Shelf,
                    "MarketShelfVisual_" + index,
                    new Vector3(-.5f + index * 1.8f, 0f, -2.6f),
                    new Vector3(0f, 90f, 0f));
        }

        private static void BuildResidential(공급망WorldZoneView zone, WorldVisualCatalog catalog)
        {
            Clear(zone.VisualRoot);
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Apartment,
                "ApartmentVisualA", new Vector3(-3.5f, 0f, 2f),
                new Vector3(0f, 165f, 0f), true);
            Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Apartment,
                "ApartmentVisualB", new Vector3(3.5f, 0f, 1f),
                new Vector3(0f, 205f, 0f), true);
            var pickup = Visual(zone.VisualRoot, catalog, UrbanVisualKeys.Pallet,
                "ResidentialPickupVisual", new Vector3(0f, 0f, -4f), Vector3.zero);
            for (var index = 0; index < 3; index++)
                Visual(pickup.VisualRoot, catalog, UrbanVisualKeys.CargoBox,
                    "PickupCargoVisual_" + index,
                    new Vector3((index - 1) * .6f, .35f, 0f),
                    new Vector3(0f, index * 25f, 0f));
        }

        private static void BuildRoutes(
            공급망MacroWorldView world,
            IReadOnlyDictionary<string, 공급망WorldZoneView> zones,
            WorldVisualCatalog catalog)
        {
            var byStableId = zones.Values.ToDictionary(value => value.StableId, StringComparer.Ordinal);
            foreach (var route in world.Routes)
            {
                Clear(route.VisualRoot);
                var from = byStableId[route.FromZoneStableId].transform.position;
                var to = byStableId[route.ToZoneStableId].transform.position;
                var direction = (to - from).normalized;
                from += direction * 5f;
                to -= direction * 5f;
                var delta = to - from;
                var count = Mathf.Max(2, Mathf.CeilToInt(delta.magnitude / 3.2f));
                var key = route.FlowOrder < 2
                    ? TransitionVisualKeys.RuralRoad
                    : TransitionVisualKeys.UrbanRoad;
                var rotation = Quaternion.LookRotation(delta.normalized, Vector3.up).eulerAngles;
                for (var index = 0; index < count; index++)
                {
                    var item = Visual(route.VisualRoot, catalog, key,
                        $"RouteVisual_{route.FlowOrder}_{index}", Vector3.zero, rotation);
                    item.transform.position = Vector3.Lerp(from, to, (index + .5f) / count)
                        + Vector3.up * .03f;
                }
            }
        }

        private static WorldVisualInstanceView Visual(
            Transform parent,
            WorldVisualCatalog catalog,
            string key,
            string name,
            Vector3 localPosition,
            Vector3 localEuler,
            bool occluder = false)
        {
            var entry = catalog.Resolve(key);
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = localPosition;
            wrapper.transform.localRotation = Quaternion.Euler(localEuler);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab) as GameObject
                ?? throw new InvalidOperationException("WORLD2PrefabInstantiationFailed:" + key);
            instance.name = "SyntyPrefabInstance";
            instance.transform.SetParent(visualRoot, false);
            instance.transform.localPosition = entry.LocalPositionCorrection;
            instance.transform.localRotation = Quaternion.Euler(entry.LocalEulerCorrection);
            instance.transform.localScale = entry.LocalScale;
            var view = wrapper.AddComponent<WorldVisualInstanceView>();
            view.Configure(key, catalog, visualRoot, instance);
            if (occluder) wrapper.AddComponent<DioramaOcclusionView>();
            return view;
        }

        private static WorldVisualCatalog EnsureFarmCatalog()
            => EnsureCatalog(FarmCatalogPath, WorldVisualCatalogCodes.Farm, new[]
            {
                Entry(FarmVisualKeys.SoilDirt, FarmRoot + "Environments/SM_Env_Dirt_01.prefab", .22f),
                Entry(FarmVisualKeys.SoilRows, FarmRoot + "Environments/SM_Env_Dirt_Rows_01.prefab", .22f),
                Entry(FarmVisualKeys.PotatoSmall, FarmRoot + "Plants/SM_Prop_Plant_Potato_01_S.prefab", .45f),
                Entry(FarmVisualKeys.PotatoMedium, FarmRoot + "Plants/SM_Prop_Plant_Potato_01_M.prefab", .45f),
                Entry(FarmVisualKeys.PotatoLarge, FarmRoot + "Plants/SM_Prop_Plant_Potato_01_L.prefab", .45f),
                Entry(FarmVisualKeys.PotatoBox, FarmRoot + "Plants/SM_Prop_Box_Potato_01.prefab", .75f),
                Entry(FarmVisualKeys.Farmer, FarmRoot + "Characters/SM_Chr_Farmer_Male_01.prefab", .8f),
                Entry(FarmVisualKeys.Barn, FarmRoot + "Buildings/SM_Bld_Barn_01.prefab", .36f),
                Entry(FarmVisualKeys.Silo, FarmRoot + "Buildings/SM_Bld_Silo_01.prefab", .3f),
                Entry(FarmVisualKeys.ProduceStand, FarmRoot + "Buildings/SM_Bld_ProduceStand_01.prefab", .65f),
                Entry(FarmVisualKeys.Tractor, FarmRoot + "Vehicles/SM_Veh_Tractor_01.prefab", .7f),
            });

        private static WorldVisualCatalog EnsureUrbanCatalog()
            => EnsureCatalog(UrbanCatalogPath, WorldVisualCatalogCodes.Urban, new[]
            {
                Entry(UrbanVisualKeys.LogisticsBuilding, CityRoot + "Buildings/SM_Bld_Station_03.prefab", .62f),
                Entry(UrbanVisualKeys.MarketBuilding, CityRoot + "Buildings/SM_Bld_Shop_05.prefab", .95f),
                Entry(UrbanVisualKeys.Apartment, CityRoot + "Buildings/SM_Bld_Apartment_01.prefab", .95f),
                Entry(UrbanVisualKeys.Van, CityRoot + "Vehicles/SM_Veh_Car_Van_01.prefab", .7f),
                Entry(UrbanVisualKeys.Pallet, CityRoot + "Props/SM_Prop_Pallet_01.prefab", .8f),
                Entry(UrbanVisualKeys.CargoBox, CityRoot + "Props/SM_Prop_CardboardBox_01.prefab", .55f),
                Entry(UrbanVisualKeys.Shelf, CityRoot + "Props/SM_Prop_ShopInterior_Shelf_01.prefab", .85f),
                Entry(UrbanVisualKeys.Desk, CityRoot + "Props/SM_Prop_ShopInterior_Desk_01.prefab", .8f),
            });

        private static WorldVisualCatalog EnsureTransitionCatalog()
            => EnsureCatalog(TransitionCatalogPath, WorldVisualCatalogCodes.Transition, new[]
            {
                Entry(TransitionVisualKeys.RuralRoad,
                    FarmRoot + "Environments/SM_Env_Road_Dirt_Straight_01.prefab", .6f),
                Entry(TransitionVisualKeys.UrbanRoad,
                    CityRoot + "Environments/SM_Env_Road_01.prefab", .72f),
            });

        private static WorldVisualCatalogEntry Entry(string key, string path, float scale)
        {
            var value = new WorldVisualCatalogEntry();
            value.Configure(key, LoadPrefab(path), Vector3.zero, Vector3.zero, Vector3.one * scale);
            return value;
        }

        private static WorldVisualCatalog EnsureCatalog(
            string path,
            string code,
            WorldVisualCatalogEntry[] entries)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            var catalog = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.Configure(code, entries);
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void ConfigureVolume(Camera camera)
        {
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(VolumeProfilePath)!);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            var color = profile.TryGet<ColorAdjustments>(out var existingColor)
                ? existingColor : profile.Add<ColorAdjustments>(true);
            color.contrast.Override(6f);
            color.saturation.Override(4f);
            var tonemapping = profile.TryGet<Tonemapping>(out var existingTonemapping)
                ? existingTonemapping : profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.TryGet<Bloom>(out var existingBloom)
                ? existingBloom : profile.Add<Bloom>(true);
            bloom.intensity.Override(.12f);
            bloom.threshold.Override(1.1f);
            EditorUtility.SetDirty(profile);

            var anchor = GameObject.Find("GlobalVolumeAnchor")
                ?? throw new InvalidOperationException("WORLD2GlobalVolumeAnchorMissing");
            var volume = anchor.GetComponent<Volume>() ?? anchor.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static void ValidateScene(params WorldVisualCatalog[] catalogs)
        {
            foreach (var catalog in catalogs) catalog.Validate();
            var world = UnityEngine.Object.FindFirstObjectByType<공급망MacroWorldView>()
                ?? throw new InvalidOperationException("WORLD2MacroWorldViewMissing");
            if (!world.ValidateWiring())
                throw new InvalidOperationException("WORLD2MacroWorldWiringInvalid");
            var instances = UnityEngine.Object.FindObjectsByType<WorldVisualInstanceView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (instances.Length < 80 || instances.Any(value => !value.ValidateWiring()))
                throw new InvalidOperationException("WORLD2VisualInstanceWiringInvalid");
            if (instances.Any(value => PrefabUtility.GetCorrespondingObjectFromSource(
                    value.PrefabInstanceRoot) == null))
                throw new InvalidOperationException("WORLD2VendorPrefabConnectionMissing");
            var renderers = instances.SelectMany(value =>
                value.GetComponentsInChildren<Renderer>(true)).ToArray();
            if (renderers.Length == 0 || renderers.Any(renderer =>
                    renderer.sharedMaterials.Any(material => material == null
                        || material.shader == null
                        || material.shader.name == "Hidden/InternalErrorShader")))
                throw new InvalidOperationException("WORLD2MissingOrErrorShader");
            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
            if (volume == null || !volume.isGlobal || volume.sharedProfile == null)
                throw new InvalidOperationException("WORLD2GlobalVolumeMissing");
        }

        private static WorldVisualCatalog LoadCatalog(string path)
            => AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(path)
               ?? throw new InvalidOperationException("WORLD2CatalogMissing:" + path);

        private static GameObject LoadPrefab(string path)
            => AssetDatabase.LoadAssetAtPath<GameObject>(path)
               ?? throw new InvalidOperationException("WORLD2PrefabMissing:" + path);

        private static void Clear(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }
    }
}
