using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class FarmCityGraphicalShowcaseBuilder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시그래픽전시.unity";
        public const string CatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/FarmCityEnvironmentCatalog.asset";
        public const string VolumeProfilePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Profiles/FarmCityShowcaseVolumeProfile.asset";
        public const string RootName = "Farm City Graphical Environment";

        private const string SourceScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/농장도시시각품질검증.unity";
        private const string FarmRoot = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string CityRoot = "Assets/Synty/PolygonCity/Prefabs/";
        private const string WorldFocus = "camera-focus:world.city-farm-supply-chain";
        private const string FarmFocus = "camera-focus:zone.farm-production";
        private const string TransitionFocus = "camera-focus:zone.transport-corridor";
        private const string MarketFocus = "camera-focus:zone.urban-market";

        private static int serial;

        [MenuItem("Ssalddel/SHOWCASE/Build Farm City Graphical Background")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new InvalidOperationException("ShowcaseSourceSceneMissing");

            var catalog = EnsureCatalog();
            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            var world = GameObject.Find("WorldBootstrap")
                ?? throw new InvalidOperationException("ShowcaseWorldRootMissing");
            var previous = world.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            serial = 0;
            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);
            BuildFarmLandscape(Group(root.transform, "Farm Landscape"), catalog);
            BuildFarmYard(Group(root.transform, "Farm Yard Landscape"), catalog);
            BuildRuralTransition(Group(root.transform, "Rural To City Transition"), catalog);
            BuildUrbanLandscape(Group(root.transform, "Urban Landscape"), catalog);
            ConfigureLighting();
            ConfigureVolume();
            ConfigureCamera(FarmFocus);

            ValidateOpenScene();
            SetShowcaseHudVisible(false);
            SetLegacyGroundVisible(false);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("ShowcaseSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
            Debug.Log("Farm City graphical showcase created: " + ScenePath);
        }

        [MenuItem("Ssalddel/SHOWCASE/Validate Farm City Graphical Background")]
        public static void ValidateOpenScene()
        {
            CityFarmVisualQualityGateBuilder.ValidateOpenScene();
            var catalog = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("ShowcaseCatalogMissing");
            catalog.Validate();
            var root = GameObject.Find("WorldBootstrap/" + RootName)
                ?? throw new InvalidOperationException("ShowcaseEnvironmentRootMissing");
            var groups = new[]
            {
                "Farm Landscape", "Farm Yard Landscape",
                "Rural To City Transition", "Urban Landscape",
            };
            if (groups.Any(value => root.transform.Find(value) == null))
                throw new InvalidOperationException("ShowcaseLandscapeGroupMissing");

            var instances = root.GetComponentsInChildren<WorldVisualInstanceView>(true);
            if (instances.Length < 180 || instances.Any(value => !value.ValidateWiring()
                || value.SourceCatalog != catalog
                || PrefabUtility.GetCorrespondingObjectFromSource(value.PrefabInstanceRoot) == null))
                throw new InvalidOperationException("ShowcaseEnvironmentInstanceInvalid");
            if (instances.Count(value => value.VisualKey.StartsWith(
                    "environment.farm.", StringComparison.Ordinal)) < 130
                || instances.Count(value => value.VisualKey.StartsWith(
                    "environment.city.", StringComparison.Ordinal)) < 25)
                throw new InvalidOperationException("ShowcasePackUsageInsufficient");
            var renderers = instances.SelectMany(value =>
                value.GetComponentsInChildren<Renderer>(true)).ToArray();
            if (renderers.Length == 0 || renderers.Any(value => value.sharedMaterials.Any(
                    material => material == null || material.shader == null
                        || material.shader.name == "Hidden/InternalErrorShader")))
                throw new InvalidOperationException("ShowcaseShaderInvalid");
            if (UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Any(value =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(value.gameObject) > 0))
                throw new InvalidOperationException("ShowcaseMissingScriptReference");
        }

        [MenuItem("Ssalddel/SHOWCASE/Focus World Overview")]
        public static void FocusWorldOverview() => ConfigureCamera(WorldFocus);

        [MenuItem("Ssalddel/SHOWCASE/Focus Farm Landscape")]
        public static void FocusFarmLandscape() => ConfigureCamera(FarmFocus);

        [MenuItem("Ssalddel/SHOWCASE/Focus Rural City Transition")]
        public static void FocusRuralTransition() => ConfigureCamera(TransitionFocus);

        [MenuItem("Ssalddel/SHOWCASE/Focus Urban Market")]
        public static void FocusUrbanMarket() => ConfigureCamera(MarketFocus);

        private static void BuildFarmLandscape(Transform parent, WorldVisualCatalog catalog)
        {
            Place(parent, catalog, EnvironmentVisualKeys.FarmGroundFlat,
                new Vector3(-30f, -1.58f, 20f), new Vector3(0f, 12f, 0f), 3.3f);

            Place(parent, catalog, EnvironmentVisualKeys.FarmMountainA,
                new Vector3(-48f, -1.3f, 40f), new Vector3(0f, 18f, 0f), .95f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmMountainB,
                new Vector3(-31f, -1.4f, 43f), new Vector3(0f, -8f, 0f), 1f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmMountainA,
                new Vector3(-14f, -1.4f, 36f), new Vector3(0f, -28f, 0f), .85f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmHillA,
                new Vector3(-53f, -.3f, 35f), new Vector3(0f, 25f, 0f), .48f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmHillB,
                new Vector3(-40f, -.35f, 41f), new Vector3(0f, -15f, 0f), .48f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmHillC,
                new Vector3(-21f, -.35f, 40f), new Vector3(0f, 35f, 0f), .42f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmHillA,
                new Vector3(-51f, -.4f, 12f), new Vector3(0f, -20f, 0f), .45f);

            var treeRing = new[]
            {
                new Vector3(-52f,0f,33f), new Vector3(-46f,0f,37f),
                new Vector3(-40f,0f,37f), new Vector3(-33f,0f,36f),
                new Vector3(-26f,0f,33f), new Vector3(-20f,0f,29f),
                new Vector3(-53f,0f,25f), new Vector3(-52f,0f,17f),
                new Vector3(-49f,0f,9f), new Vector3(-43f,0f,8f),
                new Vector3(-31f,0f,9f), new Vector3(-25f,0f,10f),
            };
            for (var index = 0; index < treeRing.Length; index++)
                Place(parent, catalog, index % 2 == 0
                        ? EnvironmentVisualKeys.FarmTreeClusterA
                        : EnvironmentVisualKeys.FarmTreeClusterB,
                    treeRing[index], new Vector3(0f, index * 31f, 0f),
                    .38f + index % 3 * .04f);

            var treeKeys = new[]
            {
                EnvironmentVisualKeys.FarmTreeA, EnvironmentVisualKeys.FarmTreeB,
                EnvironmentVisualKeys.FarmTreeC, EnvironmentVisualKeys.FarmTreeD,
                EnvironmentVisualKeys.FarmTreeLarge,
            };
            for (var index = 0; index < 28; index++)
            {
                var angle = index * 2.399963f;
                var radiusX = 11f + index % 5 * 1.7f;
                var radiusZ = 8f + index % 4 * 1.5f;
                var position = new Vector3(
                    -36f + Mathf.Cos(angle) * radiusX,
                    0f,
                    22f + Mathf.Sin(angle) * radiusZ);
                var treeKey = treeKeys[index % treeKeys.Length];
                var treeScale = treeKey == EnvironmentVisualKeys.FarmTreeLarge
                    ? .62f + index % 2 * .06f
                    : 1.25f + index % 4 * .12f;
                Place(parent, catalog, treeKey, position,
                    new Vector3(0f, index * 47f, 0f), treeScale);
            }

            Place(parent, catalog, EnvironmentVisualKeys.FarmPond,
                new Vector3(-46f, .02f, 23f), new Vector3(0f, 18f, 0f), .24f);
            for (var index = 0; index < 9; index++)
            {
                var angle = index * Mathf.PI * 2f / 9f;
                var key = index % 3 == 0 ? EnvironmentVisualKeys.FarmReedsA
                    : index % 3 == 1 ? EnvironmentVisualKeys.FarmReedsB
                    : EnvironmentVisualKeys.FarmReedsC;
                Place(parent, catalog, key,
                    new Vector3(-46f + Mathf.Cos(angle) * 4.2f, .05f,
                        23f + Mathf.Sin(angle) * 3.1f),
                    new Vector3(0f, index * 39f, 0f), .8f);
            }
            var rockKeys = new[]
            {
                EnvironmentVisualKeys.FarmRocksA, EnvironmentVisualKeys.FarmRocksB,
                EnvironmentVisualKeys.FarmRocksC, EnvironmentVisualKeys.FarmRocksD,
                EnvironmentVisualKeys.FarmRocksE,
            };
            for (var index = 0; index < 10; index++)
            {
                Place(parent, catalog, rockKeys[index % rockKeys.Length],
                    new Vector3(-49f + index * .8f, .04f, 20f + index % 3 * 1.5f),
                    new Vector3(0f, index * 53f, 0f), .8f);
            }

            ScatterGroundCover(parent, catalog, new Vector3(-36f, 0f, 22f), 72, 19f, 14f);
            BuildFieldBorder(parent, catalog, new Vector3(-36f, 0f, 22f));
            BuildDecorativeCropPatch(parent, catalog, EnvironmentVisualKeys.FarmWheat,
                new Vector3(-46f, .02f, 28f), 6, 4, .85f, .7f);
            BuildDecorativeCropPatch(parent, catalog, EnvironmentVisualKeys.FarmCorn,
                new Vector3(-46f, .02f, 10f), 5, 4, 1f, .75f);
        }

        private static void BuildFarmYard(Transform parent, WorldVisualCatalog catalog)
        {
            Place(parent, catalog, EnvironmentVisualKeys.FarmhouseA,
                new Vector3(-28f, 0f, 20f), new Vector3(0f, 28f, 0f), 1f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmhouseB,
                new Vector3(-17f, 0f, 18f), new Vector3(0f, -40f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmWindmill,
                new Vector3(-15f, 0f, 23f), new Vector3(0f, -15f, 0f), 1.1f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmWaterTower,
                new Vector3(-20f, 0f, 25f), new Vector3(0f, 12f, 0f), .85f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmWell,
                new Vector3(-27f, 0f, 12f), new Vector3(0f, 25f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.FarmBench,
                new Vector3(-25f, 0f, 11f), new Vector3(0f, -35f, 0f), .9f);
            for (var index = 0; index < 7; index++)
                Place(parent, catalog, index % 2 == 0
                        ? EnvironmentVisualKeys.FarmHayA : EnvironmentVisualKeys.FarmHayB,
                    new Vector3(-18f + index % 4 * 1.2f, .02f, 11f + index / 4 * 1.1f),
                    new Vector3(0f, index * 31f, 0f), .75f);
            for (var index = 0; index < 14; index++)
            {
                var key = index % 2 == 0
                    ? EnvironmentVisualKeys.FarmGrassB : EnvironmentVisualKeys.FarmFlowersB;
                Place(parent, catalog, key,
                    new Vector3(-30f + index * 1.25f, .02f, 7f + index % 3 * 1.3f),
                    new Vector3(0f, index * 43f, 0f), .75f);
            }
            var orchardKeys = new[]
            {
                EnvironmentVisualKeys.FarmTreeApple,
                EnvironmentVisualKeys.FarmTreeCherry,
                EnvironmentVisualKeys.FarmTreeOrange,
            };
            for (var index = 0; index < 8; index++)
                Place(parent, catalog, orchardKeys[index % orchardKeys.Length],
                    new Vector3(-30f + index % 4 * 3f, 0f, 29f + index / 4 * 3.2f),
                    new Vector3(0f, 18f + index * 43f, 0f), .9f);
        }

        private static void BuildRuralTransition(Transform parent, WorldVisualCatalog catalog)
        {
            var roadPoints = new[]
            {
                new Vector3(-19f,.03f,12f), new Vector3(-15f,.03f,10f),
                new Vector3(-11f,.03f,8f), new Vector3(-7f,.03f,6f),
                new Vector3(-3f,.03f,4f),
            };
            for (var index = 0; index < roadPoints.Length; index++)
                Place(parent, catalog, index == 0 ? EnvironmentVisualKeys.FarmRoadCurveA
                        : index == roadPoints.Length - 1 ? EnvironmentVisualKeys.FarmRoadCurveB
                        : EnvironmentVisualKeys.FarmRoadStraight,
                    roadPoints[index], new Vector3(0f, 62f, 0f), 1f);

            for (var index = 0; index < 18; index++)
            {
                var side = index % 2 == 0 ? 1f : -1f;
                var x = -18f + index * 1.05f;
                var z = 11f - index * .52f + side * 3.3f;
                var key = index < 10 ? EnvironmentVisualKeys.FarmTreeA
                    : index % 3 == 0 ? EnvironmentVisualKeys.CityTreeA
                    : index % 3 == 1 ? EnvironmentVisualKeys.CityTreeB
                    : EnvironmentVisualKeys.CityTreeC;
                Place(parent, catalog, key, new Vector3(x, 0f, z),
                    new Vector3(0f, index * 57f, 0f), .65f + index % 3 * .08f);
            }

            Place(parent, catalog, EnvironmentVisualKeys.CityShopA,
                new Vector3(-2f, 0f, 9f), new Vector3(0f, 145f, 0f), .78f);
            Place(parent, catalog, EnvironmentVisualKeys.CityShopB,
                new Vector3(3f, 0f, 5f), new Vector3(0f, 150f, 0f), .72f);
            for (var index = 0; index < 8; index++)
            {
                Place(parent, catalog, EnvironmentVisualKeys.CityGrass,
                    new Vector3(-5f + index * 1.4f, .02f, 1f + index % 2 * 1.8f),
                    new Vector3(0f, index * 41f, 0f), .8f);
                if (index % 2 == 0)
                    Place(parent, catalog, EnvironmentVisualKeys.CityFlower,
                        new Vector3(-4.5f + index * 1.4f, .03f, 2.8f),
                        new Vector3(0f, index * 65f, 0f), .8f);
            }
            Place(parent, catalog, EnvironmentVisualKeys.CityParkBench,
                new Vector3(1f, 0f, 1f), new Vector3(0f, -25f, 0f), .8f);
            for (var index = 0; index < 4; index++)
                Place(parent, catalog, EnvironmentVisualKeys.CityLightPole,
                    new Vector3(-4f + index * 3.2f, 0f, -.2f),
                    new Vector3(0f, 20f, 0f), .75f);
        }

        private static void BuildUrbanLandscape(Transform parent, WorldVisualCatalog catalog)
        {
            Place(parent, catalog, EnvironmentVisualKeys.FarmGroundFlat,
                new Vector3(31f, -1.35f, -13f), new Vector3(0f, -9f, 0f), 2.8f);
            Place(parent, catalog, EnvironmentVisualKeys.CityShopC,
                new Vector3(16f, 0f, -7f), new Vector3(0f, 145f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.CityShopD,
                new Vector3(34f, 0f, -7f), new Vector3(0f, -32f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.CityOffice,
                new Vector3(36f, 0f, -20f), new Vector3(0f, 58f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.CityStation,
                new Vector3(14f, 0f, -22f), new Vector3(0f, -28f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.CityBusStop,
                new Vector3(20f, 0f, -17f), new Vector3(0f, 62f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.CityPicnicTable,
                new Vector3(27f, 0f, -19f), new Vector3(0f, 22f, 0f), .9f);
            Place(parent, catalog, EnvironmentVisualKeys.CityUmbrella,
                new Vector3(29f, 0f, -19f), new Vector3(0f, -18f, 0f), .9f);
            for (var index = 0; index < 6; index++)
                Place(parent, catalog, EnvironmentVisualKeys.CityPlanter,
                    new Vector3(18f + index * 3.2f, 0f, -14f + index % 2 * 2f),
                    new Vector3(0f, index * 37f, 0f), .85f);
            var parkCenters = new[]
            {
                new Vector3(8f,0f,-8f), new Vector3(25f,0f,-18f),
                new Vector3(40f,0f,-28f),
            };
            foreach (var center in parkCenters)
            {
                for (var index = 0; index < 8; index++)
                {
                    var angle = index * Mathf.PI * .25f;
                    var key = index % 3 == 0 ? EnvironmentVisualKeys.CityTreeA
                        : index % 3 == 1 ? EnvironmentVisualKeys.CityTreeB
                        : EnvironmentVisualKeys.CityTreeC;
                    Place(parent, catalog, key,
                        center + new Vector3(Mathf.Cos(angle) * 6f, 0f, Mathf.Sin(angle) * 4.5f),
                        new Vector3(0f, index * 37f, 0f), .72f);
                }
                Place(parent, catalog, EnvironmentVisualKeys.CityGrass,
                    center, Vector3.zero, 1.25f);
                Place(parent, catalog, EnvironmentVisualKeys.CityParkBench,
                    center + new Vector3(0f, 0f, -2f), new Vector3(0f, 90f, 0f), .8f);
                Place(parent, catalog, EnvironmentVisualKeys.CityTrashCan,
                    center + new Vector3(1.6f, 0f, -2f), Vector3.zero, .8f);
            }

            for (var index = 0; index < 9; index++)
                Place(parent, catalog, EnvironmentVisualKeys.CityLightPole,
                    new Vector3(7f + index * 4.1f, 0f, -7f - index * 2.1f),
                    new Vector3(0f, 25f, 0f), .78f);
            for (var index = 0; index < 6; index++)
                Place(parent, catalog, EnvironmentVisualKeys.CityRoad,
                    new Vector3(5f + index * 8f, .02f, -10f - index * 4.5f),
                    new Vector3(0f, 60f, 0f), .9f);
        }

        private static void ScatterGroundCover(
            Transform parent, WorldVisualCatalog catalog, Vector3 center,
            int count, float radiusX, float radiusZ)
        {
            var keys = new[]
            {
                EnvironmentVisualKeys.FarmGrassA, EnvironmentVisualKeys.FarmGrassB,
                EnvironmentVisualKeys.FarmGrassC, EnvironmentVisualKeys.FarmFlowersA,
                EnvironmentVisualKeys.FarmFlowersB, EnvironmentVisualKeys.FarmFlowersC,
            };
            for (var index = 0; index < count; index++)
            {
                var angle = index * 2.399963f;
                var normalized = .42f + (index % 13) / 18f;
                var position = center + new Vector3(
                    Mathf.Cos(angle) * radiusX * normalized,
                    .02f,
                    Mathf.Sin(angle) * radiusZ * normalized);
                Place(parent, catalog, keys[index % keys.Length], position,
                    new Vector3(0f, index * 67f, 0f), 1.05f + index % 4 * .13f);
            }
        }

        private static void BuildFieldBorder(
            Transform parent, WorldVisualCatalog catalog, Vector3 center)
        {
            for (var index = 0; index < 9; index++)
            {
                var offset = -10f + index * 2.5f;
                Place(parent, catalog, EnvironmentVisualKeys.FarmFence,
                    center + new Vector3(offset, 0f, -7.2f), Vector3.zero, .75f);
                Place(parent, catalog, EnvironmentVisualKeys.FarmFence,
                    center + new Vector3(offset, 0f, 7.2f), Vector3.zero, .75f);
            }
            for (var index = 0; index < 5; index++)
            {
                var offset = -5f + index * 2.5f;
                Place(parent, catalog, EnvironmentVisualKeys.FarmFence,
                    center + new Vector3(-11.2f, 0f, offset), new Vector3(0f, 90f, 0f), .75f);
                Place(parent, catalog, EnvironmentVisualKeys.FarmFence,
                    center + new Vector3(11.2f, 0f, offset), new Vector3(0f, 90f, 0f), .75f);
            }
        }

        private static void BuildDecorativeCropPatch(
            Transform parent, WorldVisualCatalog catalog, string key, Vector3 origin,
            int width, int height, float spacing, float scale)
        {
            for (var z = 0; z < height; z++)
            for (var x = 0; x < width; x++)
                Place(parent, catalog, key,
                    origin + new Vector3(x * spacing, 0f, z * spacing),
                    new Vector3(0f, (x * 31 + z * 47) % 360, 0f), scale);
        }

        private static Transform Group(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static WorldVisualInstanceView Place(
            Transform parent, WorldVisualCatalog catalog, string key,
            Vector3 position, Vector3 euler, float scale)
        {
            var entry = catalog.Resolve(key);
            var wrapper = new GameObject("Environment_" + (++serial) + "_" + key);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = position;
            wrapper.transform.localRotation = Quaternion.Euler(euler);
            var visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(wrapper.transform, false);
            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab) as GameObject
                ?? throw new InvalidOperationException("ShowcasePrefabInstantiationFailed:" + key);
            instance.name = "SyntyPrefabInstance";
            instance.transform.SetParent(visualRoot, false);
            instance.transform.localPosition = entry.LocalPositionCorrection;
            instance.transform.localRotation = Quaternion.Euler(entry.LocalEulerCorrection);
            instance.transform.localScale = entry.LocalScale * scale;
            var view = wrapper.AddComponent<WorldVisualInstanceView>();
            view.Configure(key, catalog, visualRoot, instance);
            return view;
        }

        private static WorldVisualCatalog EnsureCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            var catalog = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(WorldVisualCatalogCodes.Environment, Entries());
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static WorldVisualCatalogEntry[] Entries() => new[]
        {
            Entry(EnvironmentVisualKeys.FarmGroundFlat, FarmRoot + "Generic/SM_Generic_Ground_Flat_01.prefab", 1f),
            Entry(EnvironmentVisualKeys.FarmHillA, FarmRoot + "Generic/SM_Generic_Ground_Hill_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmHillB, FarmRoot + "Generic/SM_Generic_Ground_Hill_02.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmHillC, FarmRoot + "Generic/SM_Generic_Ground_Hill_03.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmMountainA, FarmRoot + "Generic/SM_Generic_Mountains_Grass_01.prefab", .55f),
            Entry(EnvironmentVisualKeys.FarmMountainB, FarmRoot + "Generic/SM_Generic_Mountains_Grass_02.prefab", .55f),
            Entry(EnvironmentVisualKeys.FarmTreeClusterA, FarmRoot + "Generic/SM_Generic_Tree_Patch_01.prefab", .65f),
            Entry(EnvironmentVisualKeys.FarmTreeClusterB, FarmRoot + "Generic/SM_Generic_Tree_Patch_02.prefab", .65f),
            Entry(EnvironmentVisualKeys.FarmTreeA, FarmRoot + "Generic/SM_Generic_Tree_01.prefab", .62f),
            Entry(EnvironmentVisualKeys.FarmTreeB, FarmRoot + "Generic/SM_Generic_Tree_02.prefab", .62f),
            Entry(EnvironmentVisualKeys.FarmTreeC, FarmRoot + "Generic/SM_Generic_Tree_03.prefab", .62f),
            Entry(EnvironmentVisualKeys.FarmTreeD, FarmRoot + "Generic/SM_Generic_Tree_04.prefab", .62f),
            Entry(EnvironmentVisualKeys.FarmTreeLarge, FarmRoot + "Environments/SM_Env_Tree_Large_01.prefab", .55f),
            Entry(EnvironmentVisualKeys.FarmTreeApple, FarmRoot + "Environments/SM_Env_Tree_Apple_Grown_01.prefab", .65f),
            Entry(EnvironmentVisualKeys.FarmTreeCherry, FarmRoot + "Environments/SM_Env_Tree_Cherry_Grown_01.prefab", .65f),
            Entry(EnvironmentVisualKeys.FarmTreeOrange, FarmRoot + "Environments/SM_Env_Tree_Orange_Grown_01.prefab", .65f),
            Entry(EnvironmentVisualKeys.FarmGrassA, FarmRoot + "Generic/SM_Generic_Grass_Patch_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmGrassB, FarmRoot + "Generic/SM_Generic_Grass_Patch_02.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmGrassC, FarmRoot + "Generic/SM_Generic_Grass_Patch_03.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmFlowersA, FarmRoot + "Environments/SM_Env_Flowers_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmFlowersB, FarmRoot + "Environments/SM_Env_Flowers_02.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmFlowersC, FarmRoot + "Environments/SM_Env_Flowers_03.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmPond, FarmRoot + "Environments/SM_Env_Pond_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.FarmReedsA, FarmRoot + "Environments/SM_Env_Reeds_01.prefab", .75f),
            Entry(EnvironmentVisualKeys.FarmReedsB, FarmRoot + "Environments/SM_Env_Reeds_02.prefab", .75f),
            Entry(EnvironmentVisualKeys.FarmReedsC, FarmRoot + "Environments/SM_Env_Reeds_03.prefab", .75f),
            Entry(EnvironmentVisualKeys.FarmRocksA, FarmRoot + "Generic/SM_Generic_Small_Rocks_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.FarmRocksB, FarmRoot + "Generic/SM_Generic_Small_Rocks_02.prefab", .8f),
            Entry(EnvironmentVisualKeys.FarmRocksC, FarmRoot + "Generic/SM_Generic_Small_Rocks_03.prefab", .8f),
            Entry(EnvironmentVisualKeys.FarmRocksD, FarmRoot + "Generic/SM_Generic_Small_Rocks_04.prefab", .8f),
            Entry(EnvironmentVisualKeys.FarmRocksE, FarmRoot + "Generic/SM_Generic_Small_Rocks_05.prefab", .8f),
            Entry(EnvironmentVisualKeys.FarmhouseA, FarmRoot + "Buildings/SM_Bld_Farmhouse_01.prefab", .42f),
            Entry(EnvironmentVisualKeys.FarmhouseB, FarmRoot + "Buildings/SM_Bld_Farmhouse_02.prefab", .42f),
            Entry(EnvironmentVisualKeys.FarmWindmill, FarmRoot + "Props/SM_Prop_Windmill_01.prefab", .55f),
            Entry(EnvironmentVisualKeys.FarmWaterTower, FarmRoot + "Buildings/SM_Bld_WaterTower_01.prefab", .42f),
            Entry(EnvironmentVisualKeys.FarmBench, FarmRoot + "Props/SM_Prop_Bench_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.FarmWell, FarmRoot + "Props/SM_Prop_Well_01.prefab", .68f),
            Entry(EnvironmentVisualKeys.FarmFence, FarmRoot + "Props/SM_Prop_Fence_Wood_01.prefab", .75f),
            Entry(EnvironmentVisualKeys.FarmHayA, FarmRoot + "Props/SM_Prop_Hay_Bale_Round_01.prefab", .72f),
            Entry(EnvironmentVisualKeys.FarmHayB, FarmRoot + "Props/SM_Prop_Hay_Bale_Square_01.prefab", .72f),
            Entry(EnvironmentVisualKeys.FarmRoadStraight, FarmRoot + "Environments/SM_Env_Road_Dirt_Straight_01.prefab", .6f),
            Entry(EnvironmentVisualKeys.FarmRoadCurveA, FarmRoot + "Environments/SM_Env_Road_Dirt_Swerve_01.prefab", .6f),
            Entry(EnvironmentVisualKeys.FarmRoadCurveB, FarmRoot + "Environments/SM_Env_Road_Dirt_Swerve_02.prefab", .6f),
            Entry(EnvironmentVisualKeys.FarmWheat, FarmRoot + "Plants/SM_Prop_Plant_Wheat_Optimised_01.prefab", .55f),
            Entry(EnvironmentVisualKeys.FarmCorn, FarmRoot + "Plants/SM_Prop_Corn_01_Group.prefab", .55f),
            Entry(EnvironmentVisualKeys.CityTreeA, CityRoot + "Environments/SM_Env_Tree_01.prefab", .65f),
            Entry(EnvironmentVisualKeys.CityTreeB, CityRoot + "Environments/SM_Env_Tree_02.prefab", .65f),
            Entry(EnvironmentVisualKeys.CityTreeC, CityRoot + "Environments/SM_Env_Tree_03.prefab", .65f),
            Entry(EnvironmentVisualKeys.CityGrass, CityRoot + "Environments/SM_Env_Grass_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.CityFlower, CityRoot + "Environments/SM_Env_Flower_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.CityGrassPathStraight, CityRoot + "Environments/SM_Env_GrassPath_Straight_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.CityGrassPathCorner, CityRoot + "Environments/SM_Env_GrassPath_Corner_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.CityShopA, CityRoot + "Buildings/SM_Bld_Shop_02.prefab", .72f),
            Entry(EnvironmentVisualKeys.CityShopB, CityRoot + "Buildings/SM_Bld_Shop_04.prefab", .72f),
            Entry(EnvironmentVisualKeys.CityShopC, CityRoot + "Buildings/SM_Bld_Shop_03.prefab", .72f),
            Entry(EnvironmentVisualKeys.CityShopD, CityRoot + "Buildings/SM_Bld_Shop_06.prefab", .72f),
            Entry(EnvironmentVisualKeys.CityOffice, CityRoot + "Buildings/SM_Bld_OfficeOld_Small_01.prefab", .25f),
            Entry(EnvironmentVisualKeys.CityStation, CityRoot + "Buildings/SM_Bld_Station_01.prefab", .45f),
            Entry(EnvironmentVisualKeys.CityParkBench, CityRoot + "Props/SM_Prop_ParkBench_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.CityBusStop, CityRoot + "Props/SM_Prop_BusStop_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.CityPlanter, CityRoot + "Props/SM_Prop_Planter_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.CityPicnicTable, CityRoot + "Props/SM_Prop_PicnicTable_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.CityUmbrella, CityRoot + "Props/SM_Prop_Umbrella_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.CityLightPole, CityRoot + "Props/SM_Prop_LightPole_Lights_01.prefab", .7f),
            Entry(EnvironmentVisualKeys.CityTrashCan, CityRoot + "Props/SM_Prop_TrashCan_01.prefab", .8f),
            Entry(EnvironmentVisualKeys.CityRoad, CityRoot + "Environments/SM_Env_Road_01.prefab", .72f),
        };

        private static WorldVisualCatalogEntry Entry(string key, string path, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("ShowcasePrefabMissing:" + path);
            var entry = new WorldVisualCatalogEntry();
            entry.Configure(key, prefab, Vector3.zero, Vector3.zero, Vector3.one * scale);
            return entry;
        }

        private static void ConfigureLighting()
        {
            var light = GameObject.Find("WorldBootstrap/GlobalLighting")?.GetComponent<Light>()
                ?? throw new InvalidOperationException("ShowcaseGlobalLightMissing");
            light.intensity = 1.28f;
            light.color = new Color(1f, .91f, .76f);
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.68f, .78f, .92f);
            RenderSettings.ambientEquatorColor = new Color(.72f, .68f, .54f);
            RenderSettings.ambientGroundColor = new Color(.25f, .29f, .20f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.66f, .75f, .69f);
            RenderSettings.fogDensity = .0045f;
        }

        private static void SetShowcaseHudVisible(bool visible)
        {
            var canvas = GameObject.Find(
                "WorldBootstrap/SharedPresentationCanvasAnchor/WorldQualityPresentationCanvas");
            if (canvas != null) canvas.SetActive(visible);
        }

        private static void SetLegacyGroundVisible(bool visible)
        {
            var ground = GameObject.Find("WorldBootstrap/SharedWorldGround");
            if (ground != null) ground.SetActive(visible);
        }

        private static void ConfigureVolume()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VolumeProfilePath)!);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            var color = profile.TryGet<ColorAdjustments>(out var currentColor)
                ? currentColor : profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.08f);
            color.contrast.Override(12f);
            color.saturation.Override(14f);
            color.colorFilter.Override(new Color(1f, .96f, .88f));
            var tone = profile.TryGet<Tonemapping>(out var currentTone)
                ? currentTone : profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.TryGet<Bloom>(out var currentBloom)
                ? currentBloom : profile.Add<Bloom>(true);
            bloom.intensity.Override(.18f);
            bloom.threshold.Override(1.05f);
            var vignette = profile.TryGet<Vignette>(out var currentVignette)
                ? currentVignette : profile.Add<Vignette>(true);
            vignette.intensity.Override(.13f);
            vignette.smoothness.Override(.34f);
            EditorUtility.SetDirty(profile);

            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>()
                ?? throw new InvalidOperationException("ShowcaseGlobalVolumeMissing");
            volume.sharedProfile = profile;
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static void ConfigureCamera(string focus)
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<DioramaTopDownCameraRig>()
                ?? throw new InvalidOperationException("ShowcaseCameraRigMissing");
            rig.ConfigureComposition(50f, 96f, 26f, 20f, 35f, 30f, 28f);
            rig.Focus(focus);
            rig.ApplyNowForTests();
            var occlusion = rig.GetComponent<DioramaForegroundOcclusionController>();
            if (occlusion != null) occlusion.ApplyNow();
            EditorUtility.SetDirty(rig);
        }
    }
}
