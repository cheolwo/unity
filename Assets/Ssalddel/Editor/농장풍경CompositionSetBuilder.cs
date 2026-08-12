using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static class 농장풍경CompositionSetBuilder
    {
        public const string CatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/농장풍경CompositionCatalog.asset";
        public const string PrefabRoot =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/Farm";
        public const string PreviewScenePath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/CompositionSets/농장풍경조합모음미리보기.unity";

        private const string VendorRoot = "Assets/Synty/PolygonFarm/Prefabs";

        [MenuItem("Ssalddel/Farm Composition/Build 24 Reusable Sets")]
        public static void Build()
        {
            var definitions = CreateDefinitions();
            ValidateDefinitions(definitions);
            EnsureFolder(PrefabRoot);
            EnsureFolder(Path.GetDirectoryName(CatalogPath)!.Replace('\\', '/'));

            var entries = new List<농장풍경CompositionCatalogEntry>();
            foreach (var definition in definitions)
            {
                var prefab = BuildPrefab(definition);
                var entry = new 농장풍경CompositionCatalogEntry();
                entry.Configure(
                    definition.SetName,
                    definition.VariantCode,
                    prefab,
                    definition.Footprint);
                entries.Add(entry);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<농장풍경CompositionCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<농장풍경CompositionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Configure(entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            catalog.Validate();
            BuildPreviewScene(catalog);

            Debug.Log($"FarmCompositionSetsBuilt:{entries.Count}:{CatalogPath}");
        }

        public static void BuildFromCommandLine()
        {
            Build();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Ssalddel/Farm Composition/Open Library Preview")]
        public static void OpenLibraryPreview()
            => EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);

        private static void BuildPreviewScene(농장풍경CompositionCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var previewRoot = new GameObject("농장풍경CompositionSetLibrary");

            var orderedEntries = 농장풍경SetNames.All.SelectMany(setName =>
                농장풍경VariantCodes.All.Select(variant =>
                    catalog.Resolve(setName, variant))).ToArray();
            for (var index = 0; index < orderedEntries.Length; index++)
            {
                var entry = orderedEntries[index];
                var setIndex = index / 농장풍경VariantCodes.All.Count;
                var variantIndex = index % 농장풍경VariantCodes.All.Count;
                var position = new Vector3(
                    setIndex * 24f,
                    0f,
                    variantIndex * 22f);

                var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject
                    ?? throw new InvalidOperationException(
                        "FarmCompositionPreviewInstantiateFailed:" + entry.CompositionKey);
                instance.name = entry.SetName.Replace(" ", string.Empty)
                    + "_" + entry.VariantCode;
                instance.transform.SetParent(previewRoot.transform, true);
                instance.transform.position = position;

                var labelObject = new GameObject("Label_" + entry.CompositionKey);
                labelObject.transform.SetParent(previewRoot.transform, false);
                labelObject.transform.position = position + new Vector3(0f, .2f, -8f);
                labelObject.transform.eulerAngles = new Vector3(90f, 0f, 0f);
                var label = labelObject.AddComponent<TextMesh>();
                label.text = entry.SetName + " " + entry.VariantCode;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = .28f;
                label.fontSize = 32;
                label.color = new Color(.15f, .12f, .08f, 1f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "PreviewGround";
            ground.transform.SetParent(previewRoot.transform, false);
            ground.transform.position = new Vector3(84f, -.3f, 22f);
            ground.transform.localScale = new Vector3(190f, .5f, 74f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.68f, .64f, .56f);
            RenderSettings.fog = false;

            var lightObject = new GameObject("PreviewDirectionalLight");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .88f, .72f);
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.eulerAngles = new Vector3(55f, -30f, 0f);

            var cameraObject = new GameObject("FarmCompositionPreviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 37f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.72f, .79f, .82f);
            var focus = new Vector3(84f, 0f, 22f);
            cameraObject.transform.position = focus + new Vector3(0f, 102f, -94f);
            cameraObject.transform.LookAt(focus);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
                throw new InvalidOperationException(
                    "FarmCompositionPreviewSceneSaveFailed:" + PreviewScenePath);
        }

        private static GameObject BuildPrefab(SetDefinition definition)
        {
            var root = new GameObject(definition.SetName + "_" + definition.VariantCode);
            try
            {
                var environmentRoot = new GameObject("EnvironmentRoot").transform;
                environmentRoot.SetParent(root.transform, false);

                for (var index = 0; index < definition.Placements.Length; index++)
                {
                    var placement = definition.Placements[index];
                    var sourcePrefab = FindVendorPrefab(placement.PrefabName);
                    var instance = PrefabUtility.InstantiatePrefab(sourcePrefab, environmentRoot) as GameObject
                        ?? throw new InvalidOperationException(
                            "FarmCompositionPrefabInstantiateFailed:" + placement.PrefabName);
                    instance.name = $"Environment_{index + 1:D2}_{sourcePrefab.name}";
                    instance.transform.localPosition = placement.LocalPosition;
                    instance.transform.localEulerAngles = placement.LocalEuler;
                    instance.transform.localScale = placement.LocalScale;
                }

                var socketRoot = new GameObject("StatefulSockets").transform;
                socketRoot.SetParent(root.transform, false);
                var sockets = definition.Sockets.Select((socket, index) =>
                {
                    var socketObject = new GameObject($"Socket_{index + 1:D2}_{socket.SocketCode}");
                    socketObject.transform.SetParent(socketRoot, false);
                    socketObject.transform.localPosition = socket.LocalPosition;
                    socketObject.transform.localEulerAngles = socket.LocalEuler;
                    var view = socketObject.AddComponent<농장풍경CompositionSocketView>();
                    view.Configure(socket.SocketCode);
                    return view;
                }).ToArray();

                var setView = root.AddComponent<농장풍경CompositionSetView>();
                setView.Configure(
                    definition.SetName,
                    definition.VariantCode,
                    definition.Footprint,
                    environmentRoot,
                    sockets);

                if (!setView.ValidateWiring())
                    throw new InvalidOperationException(
                        "FarmCompositionSetWiringInvalid:"
                        + definition.SetName + ":" + definition.VariantCode);

                var path = PrefabRoot + "/"
                    + definition.SetName.Replace(" ", string.Empty)
                    + "_" + definition.VariantCode + ".prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path)
                    ?? throw new InvalidOperationException("FarmCompositionPrefabSaveFailed:" + path);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject FindVendorPrefab(string prefabName)
        {
            var paths = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { VendorRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    prefabName,
                    StringComparison.Ordinal))
                .ToArray();
            if (paths.Length != 1)
                throw new InvalidOperationException(
                    "FarmCompositionVendorPrefabAmbiguous:" + prefabName + ":" + paths.Length);
            return AssetDatabase.LoadAssetAtPath<GameObject>(paths[0])
                ?? throw new InvalidOperationException("FarmCompositionVendorPrefabMissing:" + prefabName);
        }

        private static void ValidateDefinitions(IReadOnlyList<SetDefinition> definitions)
        {
            var expected = 농장풍경SetNames.All.Count * 농장풍경VariantCodes.All.Count;
            if (definitions.Count != expected
                || definitions.Any(value => !농장풍경SetNames.IsKnown(value.SetName)
                    || !농장풍경VariantCodes.IsKnown(value.VariantCode)
                    || value.Footprint.x <= 0f
                    || value.Footprint.y <= 0f
                    || value.Placements.Length < 3
                    || value.Sockets.Select(socket => socket.SocketCode)
                        .Distinct(StringComparer.Ordinal).Count() != value.Sockets.Length)
                || definitions.Select(value => value.SetName + ":" + value.VariantCode)
                    .Distinct(StringComparer.Ordinal).Count() != definitions.Count)
            {
                throw new InvalidOperationException("FarmCompositionDefinitionsInvalid");
            }
        }

        private static SetDefinition[] CreateDefinitions()
            => new[]
            {
                D(농장풍경SetNames.감자밭두렁, "A", 14f, 12f,
                    P("SM_Prop_Fence_Wood_01", -5f, 0f, 4.5f),
                    P("SM_Prop_Fence_Wood_01", 0f, 0f, 4.5f),
                    P("SM_Prop_Fence_Wood_01", 5f, 0f, 4.5f),
                    P("SM_Prop_Sunflower_01", -5.5f, 0f, 3.4f),
                    P("SM_Generic_Grass_Patch_01", 5.5f, 0f, 3.6f)),
                D(농장풍경SetNames.감자밭두렁, "B", 14f, 12f,
                    P("SM_Prop_Fence_Wood_02", -5f, 0f, -4.5f, 180f),
                    P("SM_Prop_Fence_Wood_02", 0f, 0f, -4.5f, 180f),
                    P("SM_Prop_Fence_Wood_02", 5f, 0f, -4.5f, 180f),
                    P("SM_Prop_Hay_Bale_Square_01", 5.2f, 0f, -3.3f, 20f),
                    P("SM_Env_Flowers_01", -5.3f, 0f, -3.2f)),
                D(농장풍경SetNames.감자밭두렁, "C", 14f, 12f,
                    P("SM_Prop_Fence_Wood_Round_01", -5f, 0f, 4.5f),
                    P("SM_Prop_Fence_Wood_Round_01", 0f, 0f, 4.5f),
                    P("SM_Prop_Fence_Wood_Round_01", 5f, 0f, 4.5f),
                    P("SM_Env_Tree_Apple_Grown_01", -5.5f, 0f, 2.8f),
                    P("SM_Prop_Sunflower_02", 5.4f, 0f, 3.4f)),

                D(농장풍경SetNames.혼합작물밭, "A", 16f, 14f,
                    P("SM_Env_Vege_Rows_01", 0f, 0f, 0f),
                    P("SM_Prop_Plant_Corn_01_L", -3f, 0f, -1f),
                    P("SM_Prop_Plant_Corn_01_L", -1f, 0f, -1f),
                    P("SM_Prop_Plant_Wheat_Optimised_01", 2f, 0f, 1f),
                    P("SM_Prop_Sunflower_01", 5f, 0f, 4f)),
                D(농장풍경SetNames.혼합작물밭, "B", 16f, 14f,
                    P("SM_Env_Vege_Rows_02", 0f, 0f, 0f, 90f),
                    P("SM_Prop_Plant_Wheat_Optimised_02", -2f, 0f, 1f),
                    P("SM_Prop_Plant_Wheat_Optimised_02", 0f, 0f, 1f),
                    P("SM_Prop_Plant_Corn_01_L", 3f, 0f, -1f),
                    P("SM_Env_Flowers_02", -5f, 0f, 4f)),
                D(농장풍경SetNames.혼합작물밭, "C", 16f, 14f,
                    P("SM_Env_Vege_Rows_03", 0f, 0f, 0f),
                    P("SM_Prop_Plant_Corn_01_M", -3f, 0f, 0f),
                    P("SM_Prop_Plant_Corn_01_L", -1f, 0f, 0f),
                    P("SM_Prop_Plant_Wheat_Optimised_03", 2f, 0f, 0f),
                    P("SM_Prop_Scarecrow_01", 5f, 0f, 3f, -20f)),

                D(농장풍경SetNames.헛간작업마당, "A", 20f, 18f,
                    P("SM_Bld_Barn_01", 0f, 0f, 3f),
                    P("SM_Veh_Tractor_01", -5f, 0f, -3f, 25f),
                    P("SM_Prop_Hay_Bale_Square_01", 5f, 0f, -2f, -15f),
                    P("SM_Prop_Tool_Rake_01", 4f, 0f, 1f, 80f),
                    P("SM_Prop_Wheelbarrow_01", -2f, 0f, -4f, 20f)),
                D(농장풍경SetNames.헛간작업마당, "B", 20f, 18f,
                    P("SM_Bld_Barn_02", 0f, 0f, 3f),
                    P("SM_Veh_TractorOld_01", 5f, 0f, -3f, -25f),
                    P("SM_Prop_Hay_Bale_Round_01", -5f, 0f, -1f, 15f),
                    P("SM_Prop_GrainBag_01", -3f, 0f, -3f),
                    P("SM_Prop_Tool_Pitchfork_01", 3f, 0f, 1f, 75f)),
                D(농장풍경SetNames.헛간작업마당, "C", 20f, 18f,
                    P("SM_Bld_Barn_03", 0f, 0f, 3f),
                    P("SM_Veh_Attach_Trailer_Cage_01", -5f, 0f, -3f, 20f),
                    P("SM_Prop_Hay_Pile_01", 5f, 0f, -1f),
                    P("SM_Prop_Wood_Stack_01", 4f, 0f, 1f),
                    P("SM_Prop_Tool_Hoe_01", -3f, 0f, 1f, 80f)),

                D(농장풍경SetNames.농기계대기장, "A", 18f, 14f,
                    P("SM_Bld_Shelter_01", 0f, 0f, 2f),
                    P("SM_Veh_Tractor_01", -3f, 0f, -2f, 15f),
                    P("SM_Veh_Attach_Plough_01", 3f, 0f, -2f, -15f),
                    P("SM_Prop_GasCan_01", 5f, 0f, 1f),
                    P("SM_Prop_Tyre_01", 4f, 0f, -3f)),
                D(농장풍경SetNames.농기계대기장, "B", 18f, 14f,
                    P("SM_Bld_Garage_01", 0f, 0f, 2f),
                    P("SM_Veh_Pickup_01", -3f, 0f, -2f, 10f),
                    P("SM_Veh_Attach_Planter_01", 3f, 0f, -2f, -10f),
                    P("SM_Prop_ToolBox_01", 4f, 0f, 1f),
                    P("SM_Prop_GasCan_01", 5f, 0f, -2f)),
                D(농장풍경SetNames.농기계대기장, "C", 18f, 14f,
                    P("SM_Bld_Shelter_01", 0f, 0f, 2f, 180f),
                    P("SM_Veh_Harvester_01", -2f, 0f, -2f, 5f),
                    P("SM_Veh_Attach_Sprayer_01", 4f, 0f, -1f, -20f),
                    P("SM_Prop_Rusted_Drum_01", 5f, 0f, 2f),
                    P("SM_Prop_Tyre_01", -5f, 0f, -3f)),

                D(농장풍경SetNames.농산물직판장, "A", 16f, 14f,
                    P("SM_Bld_ProduceStand_01", 0f, 0f, 2f),
                    P("SM_Prop_Box_Potato_01", -3f, 0f, -2f),
                    P("SM_Prop_Box_Carrot_01", 0f, 0f, -2f),
                    P("SM_Prop_Box_Apple_01", 3f, 0f, -2f),
                    P("SM_Prop_Sign_Veges_01", 4f, 0f, 1f)),
                D(농장풍경SetNames.농산물직판장, "B", 16f, 14f,
                    P("SM_Bld_ProduceStand_01", 0f, 0f, 2f, 180f),
                    P("SM_Prop_Table_01", -3f, 0f, -2f),
                    P("SM_Prop_Crate_01", 0f, 0f, -2f),
                    P("SM_Prop_Box_Plum_01", 3f, 0f, -2f),
                    P("SM_Prop_Sign_Fruit_01", -4f, 0f, 1f)),
                D(농장풍경SetNames.농산물직판장, "C", 16f, 14f,
                    P("SM_Bld_ProduceStand_01", 0f, 0f, 2f, 90f),
                    P("SM_Prop_Box_Cucumber_01", -2f, 0f, -3f),
                    P("SM_Prop_Box_Onion_01", 1f, 0f, -3f),
                    P("SM_Prop_Pumpkin_01", 4f, 0f, -2f),
                    P("SM_Prop_Sign_Pumpkins_01", 4f, 0f, 1f)),

                D(농장풍경SetNames.수확물집하장, "A", 16f, 12f,
                    P("SM_Prop_PalletCrate_01", 0f, 0f, 1f),
                    P("SM_Prop_Box_Potato_01", -3f, 0f, -1f),
                    P("SM_Prop_GrainBag_01", 3f, 0f, -1f),
                    P("SM_Prop_Wheelbarrow_01", -4f, 0f, 2f, 30f),
                    P("SM_Prop_Crate_01", 4f, 0f, 2f)),
                D(농장풍경SetNames.수확물집하장, "B", 16f, 12f,
                    P("SM_Prop_PalletCrate_01", -2f, 0f, 1f, 90f),
                    P("SM_Prop_Box_Apple_01", 2f, 0f, -1f),
                    P("SM_Prop_Box_Carrot_01", 4f, 0f, -1f),
                    P("SM_Prop_GrainBag_Open_01", -4f, 0f, -1f),
                    P("SM_Prop_Table_02", 0f, 0f, 3f)),
                D(농장풍경SetNames.수확물집하장, "C", 16f, 12f,
                    P("SM_Prop_PalletCrate_01", 2f, 0f, 1f, -90f),
                    P("SM_Prop_Box_Peach_01", -2f, 0f, -1f),
                    P("SM_Prop_Box_Plum_01", -4f, 0f, -1f),
                    P("SM_Prop_Wheelbarrow_Metal_01", 4f, 0f, -1f, -30f),
                    P("SM_Prop_GrainBag_01", 0f, 0f, 3f)),

                D(농장풍경SetNames.농로교차로, "A", 18f, 18f,
                    P("SM_Env_Road_Dirt_Intersection_01", 0f, 0f, 0f),
                    P("SM_Prop_SignPost_01", 5f, 0f, 5f),
                    P("SM_Prop_Fence_Wood_01", -5f, 0f, 5f),
                    P("SM_Generic_Grass_Patch_01", 5f, 0f, -5f),
                    P("SM_Env_Pebbles_01", -5f, 0f, -5f)),
                D(농장풍경SetNames.농로교차로, "B", 18f, 18f,
                    P("SM_Env_Road_Dirt_T_Section_01", 0f, 0f, 0f),
                    P("SM_Prop_Ranch_Sign_01", -5f, 0f, 5f),
                    P("SM_Prop_Fence_Wood_Gate_01", 5f, 0f, 5f),
                    P("SM_Env_Flowers_03", 5f, 0f, -5f),
                    P("SM_Env_Pebbles_02", -5f, 0f, -5f)),
                D(농장풍경SetNames.농로교차로, "C", 18f, 18f,
                    P("SM_Env_Road_Dirt_Corner_01", 0f, 0f, 0f),
                    P("SM_Prop_LetterBox_01", 5f, 0f, 5f),
                    P("SM_Prop_Fence_Wood_Round_01", -5f, 0f, 5f),
                    P("SM_Generic_Grass_Patch_02", 5f, 0f, -5f),
                    P("SM_Env_Pebbles_03", -5f, 0f, -5f)),

                D(농장풍경SetNames.수목완충지, "A", 20f, 12f,
                    P("SM_Generic_Tree_Patch_01", -4f, 0f, 0f),
                    P("SM_Env_Tree_Large_01", 3f, 0f, 0f),
                    P("SM_Env_Flowers_01", 0f, 0f, -3f),
                    P("SM_Generic_Grass_Patch_01", 5f, 0f, -3f),
                    P("SM_Generic_Small_Rocks_01", -5f, 0f, -3f)),
                D(농장풍경SetNames.수목완충지, "B", 20f, 12f,
                    P("SM_Generic_Tree_Patch_02", 4f, 0f, 0f),
                    P("SM_Env_Tree_Apple_Grown_01", -3f, 0f, 0f),
                    P("SM_Env_Flowers_02", 0f, 0f, -3f),
                    P("SM_Generic_Grass_Patch_02", -5f, 0f, -3f),
                    P("SM_Generic_Small_Rocks_02", 5f, 0f, -3f)),
                D(농장풍경SetNames.수목완충지, "C", 20f, 12f,
                    P("SM_Generic_Tree_03", -4f, 0f, 0f),
                    P("SM_Generic_Tree_04", 0f, 0f, 1f),
                    P("SM_Env_Tree_Cherry_Grown_01", 4f, 0f, 0f),
                    P("SM_Env_Flowers_03", 0f, 0f, -3f),
                    P("SM_Generic_Small_Rocks_03", 5f, 0f, -3f)),
            };

        private static SetDefinition D(
            string setName,
            string variant,
            float width,
            float depth,
            params Placement[] placements)
        {
            var sockets = CreateSockets(setName, width, depth);
            return new SetDefinition(
                setName,
                variant,
                new Vector2(width, depth),
                placements,
                sockets);
        }

        private static SocketPlacement[] CreateSockets(string setName, float width, float depth)
        {
            if (setName == 농장풍경SetNames.감자밭두렁)
                return new[] { S(농장풍경SocketCodes.실제감자밭, 0f, 0f, 0f) };
            if (setName == 농장풍경SetNames.헛간작업마당)
                return new[]
                {
                    S(농장풍경SocketCodes.농부, -2f, 0f, 0f),
                    S(농장풍경SocketCodes.차량, 2f, 0f, -2f),
                    S(농장풍경SocketCodes.화물, 4f, 0f, 0f),
                };
            if (setName == 농장풍경SetNames.농기계대기장)
                return new[]
                {
                    S(농장풍경SocketCodes.차량, -2f, 0f, -2f),
                    S(농장풍경SocketCodes.농기계, 2f, 0f, -2f),
                };
            if (setName == 농장풍경SetNames.농산물직판장)
                return new[]
                {
                    S(농장풍경SocketCodes.농부, -2f, 0f, -1f),
                    S(농장풍경SocketCodes.화물, 2f, 0f, -1f),
                    S(농장풍경SocketCodes.상호작용, 0f, 0f, -4f),
                };
            if (setName == 농장풍경SetNames.수확물집하장)
                return new[]
                {
                    S(농장풍경SocketCodes.화물, 0f, 0f, 0f),
                    S(농장풍경SocketCodes.차량, width * .35f, 0f, 0f),
                };
            if (setName == 농장풍경SetNames.농로교차로)
                return new[] { S(농장풍경SocketCodes.차량, 0f, 0f, -depth * .3f) };
            return Array.Empty<SocketPlacement>();
        }

        private static Placement P(
            string prefabName,
            float x,
            float y,
            float z,
            float yaw = 0f,
            float scale = 1f)
            => new Placement(
                prefabName,
                new Vector3(x, y, z),
                new Vector3(0f, yaw, 0f),
                Vector3.one * scale);

        private static SocketPlacement S(
            string socketCode,
            float x,
            float y,
            float z,
            float yaw = 0f)
            => new SocketPlacement(
                socketCode,
                new Vector3(x, y, z),
                new Vector3(0f, yaw, 0f));

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

        private sealed class SetDefinition
        {
            public SetDefinition(
                string setName,
                string variantCode,
                Vector2 footprint,
                Placement[] placements,
                SocketPlacement[] sockets)
            {
                SetName = setName;
                VariantCode = variantCode;
                Footprint = footprint;
                Placements = placements;
                Sockets = sockets;
            }

            public string SetName { get; }
            public string VariantCode { get; }
            public Vector2 Footprint { get; }
            public Placement[] Placements { get; }
            public SocketPlacement[] Sockets { get; }
        }

        private sealed class Placement
        {
            public Placement(
                string prefabName,
                Vector3 localPosition,
                Vector3 localEuler,
                Vector3 localScale)
            {
                PrefabName = prefabName;
                LocalPosition = localPosition;
                LocalEuler = localEuler;
                LocalScale = localScale;
            }

            public string PrefabName { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalEuler { get; }
            public Vector3 LocalScale { get; }
        }

        private sealed class SocketPlacement
        {
            public SocketPlacement(
                string socketCode,
                Vector3 localPosition,
                Vector3 localEuler)
            {
                SocketCode = socketCode;
                LocalPosition = localPosition;
                LocalEuler = localEuler;
            }

            public string SocketCode { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalEuler { get; }
        }
    }
}
