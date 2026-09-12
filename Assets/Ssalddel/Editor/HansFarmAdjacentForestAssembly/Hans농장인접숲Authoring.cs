using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 한스 농장과 짧은 인접 숲길을 하나의 ScenarioLocalMeters 배치 입력으로 만든다.
    /// Simulation 좌표를 바꾸지 않으며, 세 수리 울타리 좌표는 정적 경계에서 비워 둔다.
    /// </summary>
    public static class Hans농장인접숲Authoring
    {
        public const string CandidateRevision = "hans-farm-adjacent-forest.natural-world.r3";
        public const string DraftStableId = "area-set-placement-draft:forest-edge-farm:hans-living-farm.r5-natural-world";
        public const string AuthoringDirectory =
            "ArtSource/Blender/AreaSet-배치초안/숲경계-한스-생활농장/r5-natural-world";
        public const string HomeModelAssetPath =
            "Assets/SsalddelGenerated/ProjectOwnedVariants/farm-residential-home-damaged-variant/farm-residential-home-damaged-variant.fbx";
        public const string HomePrefabAssetPath =
            "Assets/SsalddelGenerated/ProjectOwnedVariants/farm-residential-home-damaged-variant/farm-residential-home-damaged-variant.prefab";
        public const string StagingPrefabAssetPath =
            "Assets/Ssalddel/Presentation/World/Generated/AreaSetPlacementDrafts/숲경계-한스-생활농장-r5-natural-r3.prefab";
        public const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        public const string SceneRootName = "HansFarmMinimumPresentation";
        public const string CandidateInstanceName = "HansFarmAdjacentForest_r5_NaturalWorldCandidate";

        public const string SurfacePath = AuthoringDirectory + "/surface-snapshot.r3.json";
        public const string CatalogPath = AuthoringDirectory + "/asset-calibrations.r3.json";
        public const string DraftPath = AuthoringDirectory + "/placement-map.r5.r3.json";
        public const string ReceiptPath = AuthoringDirectory + "/unity-validation-receipt.r3.json";
        public const string ValidatedPath = AuthoringDirectory + "/validated-placement-plan.r3.json";
        public const string OverviewPath = AuthoringDirectory + "/unity-static-overview-r3-final.png";
        public const string EyeLevelPath = AuthoringDirectory + "/unity-static-eye-level-r3-final.png";

        private const string TerrainMeshAssetPath =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/Meshes/ContinuousScenarioTerrain.asset";
        private const string TerrainMaterialAssetPath =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/Materials/ContinuousTerrain.mat";

        private static readonly Seed[] Seeds =
        {
            new("damaged-home", "h1-stock:farm-residential-home", AreaSet배치KindCodes.Building,
                HomePrefabAssetPath, HomeModelAssetPath, string.Empty, 12f, 1.5f, false, true),
            new("work-yard", "h1-stock:farm-work-yard", AreaSet배치KindCodes.Building,
                "Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_ProduceStand_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Bld_ProduceStand_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 12f, .9f, false, true),
            new("cultivation-plot", "h1-stock:farm-production", AreaSet배치KindCodes.Cultivation,
                "Assets/Synty/PolygonFarm/Prefabs/Environments/SM_Env_Dirt_Rows_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Env_Dirt_Rows_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 10f, .5f, false, true),
            new("forest-road", "graph-edge:forest-trail-to-farm-entry", AreaSet배치KindCodes.RouteSurface,
                "Assets/Synty/PolygonConstruction/Prefabs/Environments/SM_Env_DirtRoad_Straight_01.prefab",
                "Assets/Synty/PolygonConstruction/Models/SM_Env_DirtRoad_Straight_01.fbx",
                "Assets/Synty/PolygonConstruction/Textures/PolygonConstruction_Texture_01_A.png", 16f, 2.2f, true, false),
            new("wood-fence", "h1-stock:farm-fence-edge", AreaSet배치KindCodes.Boundary,
                "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Fence_Wood_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Prop_Fence_Wood_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 18f, 1.2f, false, false),
            new("wood-gate", "h1-context:farm-entry-gate", AreaSet배치KindCodes.BoundaryPassage,
                "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Fence_Wood_Gate_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Prop_Fence_Wood_Gate_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 18f, 1.2f, false, false),
            new("pine", "h1-expression:nature:숲-가장자리", AreaSet배치KindCodes.Vegetation,
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab",
                "Assets/Synty/PolygonNature/Models/SM_Tree_Pine_01.fbx",
                "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png", 30f, 3f, false, false),
            new("large-pine", "h1-expression:nature:숲-가장자리", AreaSet배치KindCodes.Vegetation,
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_Large_01.prefab",
                "Assets/Synty/PolygonNature/Models/SM_Tree_Pine_Large_01.fbx",
                "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png", 30f, 3f, false, false),
            new("bush", "h1-expression:nature:숲-가장자리", AreaSet배치KindCodes.Vegetation,
                "Assets/Synty/PolygonNature/Prefabs/Plants/SM_Plant_Bush_Leaves_02.prefab",
                "Assets/Synty/PolygonNature/Models/SM_Plant_Bush_Leaves_02.fbx",
                "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png", 30f, 2f, false, false),
            new("grass", "h1-expression:nature:숲-가장자리", AreaSet배치KindCodes.Vegetation,
                "Assets/Synty/PolygonNature/Prefabs/Plants/SM_Plant_Grass_02.prefab",
                "Assets/Synty/PolygonNature/Models/SM_Plant_Grass_02.fbx",
                "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png", 30f, 2f, false, false),
        };

        [MenuItem("Ssalddel/한스 농장 인접 숲/1. 후보 생성·검증·정적 캡처")]
        public static void BuildValidateAndCapture()
        {
            RequireSafeEditor();
            EnsureProjectOwnedHomePrefab();
            var surface = AreaSet배치AuthoringPipeline.BuildSurfaceSnapshot();
            var catalog = BuildCatalog();
            var draft = CreateDraft(surface, catalog);
            var receipt = AreaSet배치DraftValidator.Validate(draft, surface, catalog);
            WriteJson(SurfacePath, surface);
            WriteJson(CatalogPath, catalog);
            WriteJson(DraftPath, draft);
            WriteJson(ReceiptPath, receipt);
            WriteJson(ValidatedPath, receipt);
            if (!receipt.CanStage)
                throw new InvalidOperationException("HansFarmNaturalWorldRejected:"
                    + string.Join("|", receipt.Issues.Select(x => x.IssueCode + ":" + x.PlacementStableId)));
            BuildStagingPrefab(receipt, surface, catalog, draft);
            CapturePrefab(OverviewPath, new Vector3(37f, 34f, -40f), new Vector3(0f, 1.5f, 1f), true, 38f);
            CapturePrefab(EyeLevelPath, new Vector3(22f, 4.2f, 10f), new Vector3(7f, 1.5f, -1.5f), false, 0f);
            Debug.Log($"HansFarmNaturalWorldPrepared:{receipt.DraftHashSha256}:placements={receipt.ResolvedPlacements.Length}");
        }

        [MenuItem("Ssalddel/한스 농장 인접 숲/2. canonical 최소 Root에 적용")]
        public static void ApplyValidatedCandidateToCanonicalScene()
        {
            RequireSafeEditor();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || !scene.isLoaded || scene.isDirty)
                throw new InvalidOperationException("HansFarmCanonicalSceneUnsafe");
            var receipt = ReadJson<AreaSet배치검증ReceiptData>(ValidatedPath);
            if (!receipt.CanStage || !string.Equals(receipt.DraftStableId, DraftStableId, StringComparison.Ordinal))
                throw new InvalidOperationException("HansFarmValidatedCandidateMissing");
            var draft = ReadJson<AreaSet배치DraftData>(DraftPath);
            var surface = ReadJson<AreaSet지표면SnapshotData>(SurfacePath);
            var catalog = ReadJson<AreaSet배치자산CatalogData>(CatalogPath);
            if (!string.Equals(receipt.DraftHashSha256, AreaSet배치Hash.ComputeDraft(draft), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.SurfaceHashSha256, AreaSet배치Hash.ComputeSurface(surface), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.CalibrationCatalogHashSha256, AreaSet배치Hash.ComputeCatalog(catalog), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("HansFarmValidatedCandidateHashMismatch");
            var root = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(x => x.name == SceneRootName)
                ?? throw new InvalidOperationException("HansFarmMinimumPresentationMissing");
            var terrainMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshAssetPath)
                              ?? throw new InvalidOperationException("HansFarmCanonicalTerrainMeshMissing");
            var canonicalTerrain = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<MeshFilter>(true))
                .FirstOrDefault(x => x.sharedMesh == terrainMesh && !x.transform.IsChildOf(root));
            if (canonicalTerrain == null)
                throw new InvalidOperationException("HansFarmCanonicalTerrainInstanceMissing");
            var existing = root.Cast<Transform>().ToArray();
            foreach (var child in existing) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagingPrefabAssetPath)
                         ?? throw new InvalidOperationException("HansFarmNaturalWorldPrefabMissing");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject
                           ?? throw new InvalidOperationException("HansFarmNaturalWorldInstantiateFailed");
            instance.name = CandidateInstanceName;
            instance.transform.SetParent(root, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var previewGround = instance.transform.Find("Ground_ContinuousScenarioTerrain_Unflattened");
            if (previewGround == null)
                throw new InvalidOperationException("HansFarmPreviewGroundMarkerMissing");
            if (previewGround.GetComponent<MeshFilter>() == null)
                throw new InvalidOperationException("HansFarmCandidateTerrainMeshMissing");
            previewGround.name = "Ground_HansFarmAdjacentForest_Unflattened_r3";
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("HansFarmNaturalWorldAppliedToCanonicalScene:save-not-yet-called");
        }

        public static bool CanonicalTerrainCoversCandidate(Bounds canonical, Bounds candidate)
        {
            const float tolerance = 0.05f;
            return canonical.min.x <= candidate.min.x + tolerance
                   && canonical.max.x >= candidate.max.x - tolerance
                   && canonical.min.z <= candidate.min.z + tolerance
                   && canonical.max.z >= candidate.max.z - tolerance;
        }


        public static AreaSet배치자산CatalogData BuildCatalog()
        {
            var value = new AreaSet배치자산CatalogData
            {
                CatalogStableId = "asset-calibration:forest-edge-farm:natural-world.r1",
                CatalogRevision = CandidateRevision,
                Items = Seeds.Select(Measure).ToArray(),
            };
            value.CatalogHashSha256 = AreaSet배치Hash.ComputeCatalog(value);
            return value;
        }

        public static AreaSet배치DraftData CreateDraft(
            AreaSet지표면SnapshotData surface, AreaSet배치자산CatalogData catalog)
        {
            var assets = catalog.Items.ToDictionary(x => x.AssetKey, StringComparer.Ordinal);
            var placements = new List<AreaSet배치DraftItemData>
            {
                Item(assets, "damaged-home", "placement-instance:forest-edge-farm:hans-residential-home", -10.5f, -8.2f, 18f, 1f),
                Item(assets, "work-yard", "placement-instance:forest-edge-farm:barn-work-yard", -.5f, -10.5f, 195f, 1.1f),
                Item(assets, "cultivation-plot", "placement-instance:forest-edge-farm:mixed-crop-field", 6.5f, -8f, 0f, .5f),
            };

            foreach (var x in new[] { -18.75f, -16.25f, -13.75f, -11.25f, -8.75f, -6.25f, -4.8f, 4.8f, 7.3f, 15.6f, 18.1f })
                placements.Add(Item(assets, "wood-fence", "boundary:farm:north:" + x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    x, 2.2f, 0f, 1f, cluster: "farm-boundary"));
            foreach (var z in new[] { .95f, -1.55f, -4.05f, -6.55f, -9.05f, -11.55f, -13.25f })
            {
                placements.Add(Item(assets, "wood-fence", "boundary:farm:west:" + z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    -20f, z, 90f, 1f, cluster: "farm-boundary"));
                placements.Add(Item(assets, "wood-fence", "boundary:farm:east:" + z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    20f, z, 90f, 1f, cluster: "farm-boundary"));
            }
            foreach (var x in new[] { -18.75f, -16.25f, -13.75f, -11.25f, -8.75f, -6.25f, -3.75f, -1.25f, 1.25f, 3.75f, 6.25f, 8.75f, 11.25f, 13.75f, 16.25f, 18.75f })
                placements.Add(Item(assets, "wood-fence", "boundary:farm:south:" + x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    x, -14f, 0f, 1f, cluster: "farm-boundary"));

            AddVegetation(placements, assets, "large-pine", "forest-dense", new[]
            {
                (-25f, 20f, 8f, .9f), (-20.5f, 17.5f, 42f, .86f), (-16f, 20.5f, 91f, .92f),
                (-11.5f, 17f, 136f, .88f), (-7f, 20.5f, 201f, .91f), (-2.5f, 17.5f, 267f, .86f),
                (2f, 21f, 322f, .9f), (6f, 17f, 17f, .84f), (10f, 21f, 73f, .88f),
                (20f, 20f, 116f, .9f), (24.5f, 16.5f, 183f, .86f), (-24f, 12f, 247f, .88f),
            });
            AddVegetation(placements, assets, "pine", "forest-transition", new[]
            {
                (-21f, 14f, 23f, .86f), (-17f, 11f, 77f, .82f), (-12f, 14f, 129f, .84f),
                (-7f, 12f, 184f, .8f), (-2f, 14.5f, 244f, .82f), (3f, 12f, 301f, .78f),
                (8f, 15f, 33f, .83f), (18f, 15f, 92f, .82f), (23f, 12f, 151f, .84f),
            });
            AddVegetation(placements, assets, "bush", "forest-edge-understory", new[]
            {
                (-22f, 9f, 12f, 1f), (-18f, 8f, 68f, .9f), (-14f, 10f, 124f, .85f),
                (-9f, 8.5f, 177f, .85f), (-4f, 10f, 231f, .9f), (1f, 8.5f, 289f, .75f),
                (6f, 10.5f, 44f, .9f), (18f, 9f, 103f, .9f), (22f, 7f, 164f, .85f),
            });
            AddVegetation(placements, assets, "grass", "farm-edge-grass", new[]
            {
                (-18f, 4.5f, 11f, .9f), (-14f, 5.5f, 55f, .85f), (-10f, 4.5f, 91f, .85f),
                (-6f, 6f, 145f, .8f), (-1f, 5f, 199f, .8f), (4f, 6.5f, 251f, .9f),
                (8f, 4.5f, 31f, .85f), (17f, 5f, 89f, .85f), (21f, 3.5f, 149f, .9f),
            });

            return new AreaSet배치DraftData
            {
                DraftStableId = DraftStableId,
                DraftRevision = CandidateRevision,
                AreaSetStableId = surface.AreaSetStableId,
                SurfaceSnapshotStableId = surface.SnapshotStableId,
                SurfaceHashSha256 = surface.SurfaceHashSha256,
                CalibrationCatalogStableId = catalog.CatalogStableId,
                CalibrationCatalogHashSha256 = catalog.CatalogHashSha256,
                Camera = new AreaSet배치CameraRecipeData
                {
                    PositionX = 37f, PositionY = 34f, PositionZ = -40f,
                    TargetX = 0f, TargetY = 1.5f, TargetZ = 1f,
                    OrthographicSize = 38f, PixelWidth = 1600, PixelHeight = 1000,
                },
                Placements = placements.ToArray(),
                Routes = new[]
                {
                    Route("gm-edge:forest-trail-to-farm-entry", 3.2f,
                        (18f, 18f), (16f, 15f), (15.4f, 10.25f), (13.5f, 6f), (11.8f, 2.2f)),
                    Route("gm-edge:farm-entry-to-hans-house", 2.8f,
                        (11.8f, 2.2f), (8f, -.5f), (3f, -2.5f), (-3f, -3.5f)),
                    Route("gm-edge:farm-entry-to-production", 2.4f,
                        (11.8f, 2.2f), (10f, -1f), (9.5f, -4.5f)),
                    Route("gm-edge:farm-return-to-forest", 2.8f,
                        (-3f, -3.5f), (3f, -2.5f), (8f, -.5f), (11.8f, 2.2f)),
                },
            };
        }

        private static void EnsureProjectOwnedHomePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HomeModelAssetPath)
                        ?? throw new InvalidOperationException("HansFarmDamagedHomeModelMissing");
            var directory = Path.GetDirectoryName(HomePrefabAssetPath)?.Replace('\\', '/');
            Directory.CreateDirectory(ProjectPath(directory ?? throw new InvalidOperationException("HansFarmHomeDirectoryInvalid")));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("FarmResidentialHome_Damaged_ProjectOwned_r1");
                SceneManager.MoveGameObjectToScene(root, scene);
                var visual = PrefabUtility.InstantiatePrefab(model, scene) as GameObject
                             ?? throw new InvalidOperationException("HansFarmDamagedHomeInstantiateFailed");
                visual.name = "VisualRoot_FarmResidentialHome_Damaged";
                visual.transform.SetParent(root.transform, false);
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("HansFarmDamagedHomeRendererMissing");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (var index = 0; index < materials.Length; index++)
                    {
                        var sourceName = materials[index] != null ? materials[index].name : string.Empty;
                        materials[index] = HomeReviewMaterial(sourceName);
                    }
                    renderer.sharedMaterials = materials;
                }
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, HomePrefabAssetPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Material HomeReviewMaterial(string sourceName)
        {
            var lower = sourceName.ToLowerInvariant();
            var suffix = "Weathered";
            var color = new Color(.58f, .47f, .32f, 1f);
            if (lower.Contains("roof")) { suffix = "Roof"; color = new Color(.16f, .12f, .08f, 1f); }
            else if (lower.Contains("glass") || lower.Contains("window"))
            { suffix = "Window"; color = new Color(.18f, .42f, .56f, 1f); }
            else if (lower.Contains("wood")) { suffix = "ExposedWood"; color = new Color(.34f, .18f, .08f, 1f); }
            var path = Path.GetDirectoryName(HomePrefabAssetPath)?.Replace('\\', '/') + "/Materials/Home_" + suffix + ".mat";
            var fullDirectory = ProjectPath(Path.GetDirectoryName(path)?.Replace('\\', '/')
                                            ?? throw new InvalidOperationException("HansFarmHomeMaterialDirectoryInvalid"));
            Directory.CreateDirectory(fullDirectory);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")
                             ?? throw new InvalidOperationException("HansFarmHomeShaderMissing");
                material = new Material(shader) { name = "Home_" + suffix };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AreaSet배치자산CalibrationData Measure(Seed seed)
        {
            var root = PrefabUtility.LoadPrefabContents(seed.PrefabPath);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("HansFarmRendererMissing:" + seed.Key);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var value = new AreaSet배치자산CalibrationData
                {
                    AssetKey = seed.Key, H1StableId = seed.H1, PlacementKindCode = seed.Kind,
                    PrefabAssetPath = seed.PrefabPath, ModelAssetPath = seed.ModelPath,
                    TextureAssetPath = seed.TexturePath,
                    AssetGuid = AssetDatabase.AssetPathToGUID(seed.PrefabPath),
                    AssetDependencyHash = AssetDatabase.GetAssetDependencyHash(seed.PrefabPath).ToString(),
                    BoundsMinimumX = bounds.min.x, BoundsMinimumY = bounds.min.y, BoundsMinimumZ = bounds.min.z,
                    BoundsCenterX = bounds.center.x, BoundsCenterY = bounds.center.y, BoundsCenterZ = bounds.center.z,
                    BoundsSizeX = bounds.size.x, BoundsSizeY = bounds.size.y, BoundsSizeZ = bounds.size.z,
                    MaximumSlopeDegrees = seed.MaxSlope, MaximumHeightSpreadMeters = seed.MaxSpread,
                    SupportsSurfaceOffset = seed.SupportsOffset, CollisionEligible = seed.CollisionEligible,
                };
                value.AssetFingerprint = AreaSet배치Hash.ComputeCalibrationFingerprint(value);
                return value;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildStagingPrefab(AreaSet배치검증ReceiptData receipt,
            AreaSet지표면SnapshotData surface, AreaSet배치자산CatalogData catalog,
            AreaSet배치DraftData draft)
        {
            Directory.CreateDirectory(ProjectPath(Path.GetDirectoryName(StagingPrefabAssetPath)?.Replace('\\', '/')
                                                   ?? throw new InvalidOperationException("HansFarmStagingDirectoryInvalid")));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("AreaSet_숲경계_한스농장_r5_NaturalWorldCandidate");
                SceneManager.MoveGameObjectToScene(root, scene);
                var terrain = new GameObject("Ground_ContinuousScenarioTerrain_Unflattened");
                terrain.transform.SetParent(root.transform, false);
                terrain.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshAssetPath);
                terrain.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialAssetPath);
                terrain.AddComponent<MeshCollider>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshAssetPath);
                BuildContinuousRouteSurface(root.transform, surface,
                    draft.Routes.Single(x => x.RouteStableId == "gm-edge:forest-trail-to-farm-entry"), "ForestTrail");
                BuildContinuousRouteSurface(root.transform, surface,
                    draft.Routes.Single(x => x.RouteStableId == "gm-edge:farm-entry-to-hans-house"), "FarmYard");
                BuildContinuousRouteSurface(root.transform, surface,
                    draft.Routes.Single(x => x.RouteStableId == "gm-edge:farm-entry-to-production"), "Production");
                var assets = catalog.Items.ToDictionary(x => x.AssetKey, StringComparer.Ordinal);
                var sampler = new AreaSet지표면Sampler(surface);
                foreach (var placement in receipt.ResolvedPlacements)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assets[placement.AssetKey].PrefabAssetPath)
                                 ?? throw new InvalidOperationException("HansFarmPlacementPrefabMissing:" + placement.AssetKey);
                    var instance = new GameObject(placement.PlacementStableId);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    instance.transform.SetParent(root.transform, false);
                    var visual = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject
                                   ?? throw new InvalidOperationException("HansFarmPlacementInstantiateFailed:" + placement.AssetKey);
                    visual.name = "Visual_" + placement.AssetKey;
                    visual.transform.SetParent(instance.transform, false);
                    instance.transform.localPosition = new Vector3(placement.LocalX, placement.LocalY, placement.LocalZ);
                    instance.transform.localRotation = Quaternion.Euler(0f, placement.RotationYDegrees, 0f);
                    instance.transform.localScale = Vector3.one * placement.UniformScale;
                    if (placement.AssetKey == "wood-fence")
                        AlignFenceVisualToSurface(visual.transform, placement, assets[placement.AssetKey], sampler);
                }
                var binding = new GameObject("AuthorityBinding_DoNotDuplicate");
                binding.transform.SetParent(root.transform, false);
                binding.name = "AuthorityBinding_fence-segments_-2.4_0_2.4_z2.2_and_broken-axe";
                PrefabUtility.SaveAsPrefabAsset(root, StagingPrefabAssetPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void BuildContinuousRouteSurface(Transform parent,
            AreaSet지표면SnapshotData surface, AreaSet배치RouteData route, string suffix)
        {
            const float visualWidth = 2.6f;
            const float lift = .035f;
            var sampler = new AreaSet지표면Sampler(surface);
            var samples = new List<Vector2>();
            for (var segment = 0; segment < route.Points.Length - 1; segment++)
            {
                var from = route.Points[segment];
                var to = route.Points[segment + 1];
                var distance = Vector2.Distance(new Vector2(from.X, from.Z), new Vector2(to.X, to.Z));
                var steps = Mathf.Max(1, Mathf.CeilToInt(distance));
                for (var step = 0; step < steps; step++)
                    samples.Add(Vector2.Lerp(new Vector2(from.X, from.Z), new Vector2(to.X, to.Z), step / (float)steps));
            }
            samples.Add(new Vector2(route.Points[^1].X, route.Points[^1].Z));
            var vertices = new List<Vector3>(samples.Count * 2);
            var uv = new List<Vector2>(samples.Count * 2);
            var triangles = new List<int>((samples.Count - 1) * 6);
            var traveled = 0f;
            for (var index = 0; index < samples.Count; index++)
            {
                var previous = samples[Mathf.Max(0, index - 1)];
                var next = samples[Mathf.Min(samples.Count - 1, index + 1)];
                var direction = (next - previous).normalized;
                var right = new Vector2(direction.y, -direction.x) * (visualWidth * .5f);
                if (!sampler.TrySample(samples[index].x - right.x, samples[index].y - right.y, out var leftY)
                    || !sampler.TrySample(samples[index].x + right.x, samples[index].y + right.y, out var rightY))
                    throw new InvalidOperationException("HansFarmContinuousRouteSurfaceMissing");
                if (index > 0) traveled += Vector2.Distance(samples[index - 1], samples[index]);
                vertices.Add(new Vector3(samples[index].x - right.x, leftY + lift, samples[index].y - right.y));
                vertices.Add(new Vector3(samples[index].x + right.x, rightY + lift, samples[index].y + right.y));
                uv.Add(new Vector2(0f, traveled / visualWidth));
                uv.Add(new Vector2(1f, traveled / visualWidth));
                if (index == 0) continue;
                var start = (index - 1) * 2;
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            var mesh = new Mesh { name = "HansForestTrail_Continuous_" + suffix + "_r3" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var meshPath = "Assets/Ssalddel/Presentation/World/Generated/AreaSetPlacementDrafts/HansForestTrail_Continuous_" + suffix + "_r3.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            var materialPath = "Assets/Ssalddel/Presentation/World/Generated/AreaSetPlacementDrafts/HansForestTrail_Review_r3.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var color = new Color(.36f, .20f, .095f, 1f);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .02f);
            EditorUtility.SetDirty(material);
            var road = new GameObject("RouteSurface_" + suffix + "_Continuous_r3");
            road.transform.SetParent(parent, false);
            road.AddComponent<MeshFilter>().sharedMesh = mesh;
            road.AddComponent<MeshRenderer>().sharedMaterial = material;
            road.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void AlignFenceVisualToSurface(Transform visual,
            AreaSet배치ResolvedItemData placement, AreaSet배치자산CalibrationData calibration,
            AreaSet지표면Sampler sampler)
        {
            var half = calibration.BoundsSizeX * placement.UniformScale * .5f;
            var yaw = placement.RotationYDegrees * Mathf.Deg2Rad;
            var dx = Mathf.Cos(yaw) * half;
            var dz = -Mathf.Sin(yaw) * half;
            if (!sampler.TrySample(placement.LocalX - dx, placement.LocalZ - dz, out var left)
                || !sampler.TrySample(placement.LocalX + dx, placement.LocalZ + dz, out var right))
                throw new InvalidOperationException("HansFarmFenceSurfaceMissing:" + placement.PlacementStableId);
            var roll = Mathf.Atan2(right - left, half * 2f) * Mathf.Rad2Deg;
            visual.localRotation = Quaternion.Euler(0f, 0f, roll);
            visual.name += $"_GroundAligned_{roll:0.00}deg";
        }

        private static void CapturePrefab(string outputRelativePath, Vector3 cameraPosition,
            Vector3 target, bool orthographic, float orthographicSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagingPrefabAssetPath)
                         ?? throw new InvalidOperationException("HansFarmCapturePrefabMissing");
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture texture = null;
            Texture2D image = null;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousAmbientLight = RenderSettings.ambientLight;
            try
            {
                var root = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(root, scene);
                var cameraObject = new GameObject("BrightReviewCamera_NoAuthorityTimeChange");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.48f, .64f, .78f);
                camera.orthographic = orthographic;
                camera.orthographicSize = orthographicSize;
                camera.fieldOfView = 52f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 300f;
                camera.transform.position = cameraPosition;
                camera.transform.rotation = Quaternion.LookRotation(target - cameraPosition, Vector3.up);
                var key = new GameObject("BrightReviewDirectionalLight");
                SceneManager.MoveGameObjectToScene(key, scene);
                var light = key.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.62f, .62f, .58f);
                texture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
                texture.Create();
                camera.targetTexture = texture;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                image = new Texture2D(1600, 1000, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                var output = ProjectPath(outputRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(output)
                                          ?? throw new InvalidOperationException("HansFarmCaptureOutputInvalid"));
                if (File.Exists(output)) throw new IOException("HansFarmCaptureAlreadyExists:" + output);
                File.WriteAllBytes(output, image.EncodeToPNG());
            }
            finally
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static AreaSet배치DraftItemData Item(
            IReadOnlyDictionary<string, AreaSet배치자산CalibrationData> assets, string key,
            string stableId, float x, float z, float yaw, float scale,
            string cluster = "")
        {
            var asset = assets[key];
            return new AreaSet배치DraftItemData
            {
                PlacementStableId = stableId, H1StableId = asset.H1StableId,
                AssetKey = key, AssetFingerprint = asset.AssetFingerprint,
                LocalX = x, LocalZ = z, RotationYDegrees = yaw, UniformScale = scale,
                HeightPolicyCode = AreaSet배치HeightPolicyCodes.SurfaceSnap,
                ClusterKey = cluster, AllowOverlapWithPlacementStableIds = Array.Empty<string>(), Enabled = true,
            };
        }

        private static void AddVegetation(List<AreaSet배치DraftItemData> output,
            IReadOnlyDictionary<string, AreaSet배치자산CalibrationData> assets,
            string key, string cluster, IEnumerable<(float X, float Z, float Yaw, float Scale)> values)
        {
            var index = 0;
            foreach (var value in values)
                output.Add(Item(assets, key, $"vegetation:{cluster}:{++index:00}",
                    value.X, value.Z, value.Yaw, value.Scale, cluster));
        }

        private static void AddRoadSegments(List<AreaSet배치DraftItemData> output,
            IReadOnlyDictionary<string, AreaSet배치자산CalibrationData> assets,
            string cluster, IReadOnlyList<(float X, float Z)> points)
        {
            var calibration = assets["forest-road"];
            var nativeLength = calibration.BoundsSizeZ;
            if (nativeLength <= 0f) throw new InvalidOperationException("HansFarmRoadLengthInvalid");
            for (var index = 0; index < points.Count - 1; index++)
            {
                var from = points[index];
                var to = points[index + 1];
                var dx = to.X - from.X;
                var dz = to.Z - from.Z;
                var length = Mathf.Sqrt(dx * dx + dz * dz);
                var yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                var scale = length / nativeLength;
                output.Add(Item(assets, "forest-road", $"route-surface:{cluster}:{index + 1:00}",
                    from.X, from.Z, yaw, scale, cluster: cluster));
            }
        }

        private static AreaSet배치RouteData Route(string id, float width,
            params (float X, float Z)[] points) => new()
        {
            RouteStableId = id, WidthMeters = width,
            Points = points.Select(x => new AreaSet배치RoutePointData(x.X, x.Z)).ToArray(),
        };

        private static void RequireSafeEditor()
        {
            if (UnityEngine.Application.isPlaying || EditorApplication.isCompiling
                                                  || EditorApplication.isUpdating)
                throw new InvalidOperationException("HansFarmEditorBusy");
        }

        private static T ReadJson<T>(string path) where T : class =>
            AreaSet배치Json.Deserialize<T>(File.ReadAllText(ProjectPath(path)));

        private static void WriteJson(string path, object value)
        {
            var full = ProjectPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)
                                      ?? throw new InvalidOperationException("HansFarmOutputDirectoryInvalid"));
            File.WriteAllText(full, AreaSet배치Json.Serialize(value));
        }

        private static string ProjectPath(string relative) => Path.GetFullPath(
            Path.Combine(UnityEngine.Application.dataPath, "..", relative.Replace('/', Path.DirectorySeparatorChar)));

        private sealed class Seed
        {
            public Seed(string key, string h1, string kind, string prefabPath, string modelPath,
                string texturePath, float maxSlope, float maxSpread, bool supportsOffset, bool collisionEligible)
            {
                Key = key; H1 = h1; Kind = kind; PrefabPath = prefabPath; ModelPath = modelPath;
                TexturePath = texturePath; MaxSlope = maxSlope; MaxSpread = maxSpread;
                SupportsOffset = supportsOffset; CollisionEligible = collisionEligible;
            }
            public string Key { get; }
            public string H1 { get; }
            public string Kind { get; }
            public string PrefabPath { get; }
            public string ModelPath { get; }
            public string TexturePath { get; }
            public float MaxSlope { get; }
            public float MaxSpread { get; }
            public bool SupportsOffset { get; }
            public bool CollisionEligible { get; }
        }
    }
}
