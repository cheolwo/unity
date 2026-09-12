using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Blender를 배치 권위로 만들지 않고 Unity의 실제 지형·Prefab 측정값을
    /// 편집 가능한 AreaSet 초안에 제공하고, 검증된 변환만 staging Prefab에 반영합니다.
    /// </summary>
    public static class AreaSet배치AuthoringPipeline
    {
        public const string AreaSetStableId = "area-set:review:forest-edge-farm:hans-living-farm.v1";
        public const string SurfaceSnapshotStableId = "surface-snapshot:simulation-world-shell:continuous-scenario-terrain.v1";
        public const string CalibrationCatalogStableId = "asset-calibration:forest-edge-farm:h1-candidates.v1";
        public const string DraftStableId = "area-set-placement-draft:forest-edge-farm:hans-living-farm.r3";
        public const string TerrainMeshAssetPath =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/Meshes/ContinuousScenarioTerrain.asset";
        public const string TerrainMaterialAssetPath =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/Materials/ContinuousTerrain.mat";
        public const string AuthoringDirectory =
            "ArtSource/Blender/AreaSet-배치초안/숲경계-한스-생활농장/r3";
        public const string SurfaceSnapshotRelativePath = AuthoringDirectory + "/surface-snapshot.json";
        public const string CalibrationCatalogRelativePath = AuthoringDirectory + "/asset-calibrations.json";
        public const string DraftRelativePath = AuthoringDirectory + "/blender-draft.json";
        public const string ValidationReceiptRelativePath = AuthoringDirectory + "/unity-validation-receipt.json";
        public const string ValidatedPlanRelativePath = AuthoringDirectory + "/validated-placement-plan.json";
        public const string UnityStagingImageRelativePath = AuthoringDirectory + "/unity-staging-overview.png";
        public const string StagingPrefabAssetPath =
            "Assets/Ssalddel/Presentation/World/Generated/AreaSetPlacementDrafts/숲경계-한스-생활농장-r3.prefab";

        private static readonly CandidateSeed[] CandidateSeeds =
        {
            Seed("farmhouse", "h1-stock:farm-residential-home", AreaSet배치KindCodes.Building,
                "Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_Farmhouse_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Bld_Farmhouse_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 12f, 1.5f),
            Seed("work-yard", "h1-stock:farm-work-yard", AreaSet배치KindCodes.Building,
                "Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_ProduceStand_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Bld_ProduceStand_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 12f, .9f),
            Seed("loading-point", "h1-node-candidate:farm-loading-point", AreaSet배치KindCodes.WorkSurface,
                "Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_ConcreteBase_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Bld_ConcreteBase_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 10f, .8f, true),
            Seed("cultivation-plot-proxy", "h1-stock:farm-production", AreaSet배치KindCodes.Cultivation,
                "Assets/Synty/PolygonFarm/Prefabs/Environments/SM_Env_Dirt_Rows_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Env_Dirt_Rows_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 10f, .5f),
            Seed("rural-road", "graph-edge:farm-entry-route", AreaSet배치KindCodes.RouteSurface,
                "Assets/Synty/PolygonConstruction/Prefabs/Environments/SM_Env_DirtRoad_Straight_01.prefab",
                "Assets/Synty/PolygonConstruction/Models/SM_Env_DirtRoad_Straight_01.fbx",
                "Assets/Synty/PolygonConstruction/Textures/PolygonConstruction_Texture_01_A.png", 16f, 2.2f, true, false),
            Seed("wood-fence", "h1-context:farm-boundary", AreaSet배치KindCodes.Boundary,
                "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Fence_Wood_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Prop_Fence_Wood_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 18f, 1.2f, false, false),
            Seed("wood-gate", "h1-context:farm-entry-gate", AreaSet배치KindCodes.BoundaryPassage,
                "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Fence_Wood_Gate_01.prefab",
                "Assets/Synty/PolygonFarm/Models/SM_Prop_Fence_Wood_Gate_01.fbx",
                "Assets/Synty/PolygonFarm/Textures/PolygonFarm_01_A.png", 18f, 1.2f, false, false),
            Seed("pine", "h1-context:nature-forest-buffer", AreaSet배치KindCodes.Vegetation,
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab",
                "Assets/Synty/PolygonNature/Models/SM_Tree_Pine_01.fbx",
                "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png", 30f, 3f, false, false),
            Seed("large-pine", "h1-context:nature-forest-buffer", AreaSet배치KindCodes.Vegetation,
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_Large_01.prefab",
                "Assets/Synty/PolygonNature/Models/SM_Tree_Pine_Large_01.fbx",
                "Assets/Synty/PolygonNature/Textures/PolygonNature_01.png", 30f, 3f, false, false),
        };

        [MenuItem("Ssalddel/AreaSet 배치 초안/1. Unity 지형·자산 사본 내보내기")]
        public static void ExportForestEdgeFarmAuthoringPackage()
        {
            var surface = BuildSurfaceSnapshot();
            var catalog = BuildCalibrationCatalog();
            WriteJson(SurfaceSnapshotRelativePath, surface);
            WriteJson(CalibrationCatalogRelativePath, catalog);
            if (!File.Exists(ProjectPath(DraftRelativePath)))
                WriteJson(DraftRelativePath, CreateInitialDraft(surface, catalog));
            Debug.Log($"AreaSet 배치 초안 패키지 내보내기 완료: {ProjectPath(AuthoringDirectory)}");
        }

        [MenuItem("Ssalddel/AreaSet 배치 초안/2. 현재 초안 검사·staging·비교 화면")]
        public static void ValidateStageAndCaptureForestEdgeFarm()
        {
            var surface = ReadJson<AreaSet지표면SnapshotData>(SurfaceSnapshotRelativePath);
            var catalog = ReadJson<AreaSet배치자산CatalogData>(CalibrationCatalogRelativePath);
            var draft = ReadJson<AreaSet배치DraftData>(DraftRelativePath);
            var receipt = AreaSet배치DraftValidator.Validate(draft, surface, catalog);
            if (receipt.CanStage)
            {
                receipt.StagingPrefabAssetPath = BuildStagingPrefab(receipt, surface, catalog);
                receipt.UnityStagingImagePath = CaptureStagingPrefab(
                    receipt.StagingPrefabAssetPath, draft.Camera, UnityStagingImageRelativePath);
            }
            WriteJson(ValidationReceiptRelativePath, receipt);
            WriteJson(ValidatedPlanRelativePath, receipt);
            if (!receipt.CanStage)
                throw new InvalidOperationException("AreaSetPlacementDraftRejected:"
                    + string.Join("|", receipt.Issues.Where(x => x.SeverityCode == "Error")
                        .Take(8).Select(x => x.IssueCode + ":" + x.PlacementStableId)));
            Debug.Log($"AreaSet 배치 초안 검증 완료: placements={receipt.ResolvedPlacements.Length};"
                      + $"prefab={receipt.StagingPrefabAssetPath};image={receipt.UnityStagingImagePath}");
        }

        [MenuItem("Ssalddel/AreaSet 배치 초안/초안 참조를 현재 사본으로 명시적 갱신")]
        public static void RebaseDraftReferencesToCurrentPackage()
        {
            var surface = ReadJson<AreaSet지표면SnapshotData>(SurfaceSnapshotRelativePath);
            var catalog = ReadJson<AreaSet배치자산CatalogData>(CalibrationCatalogRelativePath);
            var draft = ReadJson<AreaSet배치DraftData>(DraftRelativePath);
            var assets = catalog.Items.ToDictionary(x => x.AssetKey, StringComparer.Ordinal);
            draft.SurfaceSnapshotStableId = surface.SnapshotStableId;
            draft.SurfaceHashSha256 = surface.SurfaceHashSha256;
            draft.CalibrationCatalogStableId = catalog.CatalogStableId;
            draft.CalibrationCatalogHashSha256 = catalog.CatalogHashSha256;
            foreach (var placement in draft.Placements)
                if (assets.TryGetValue(placement.AssetKey, out var asset))
                    placement.AssetFingerprint = asset.AssetFingerprint;
            draft.DraftRevision = IncrementRevision(draft.DraftRevision);
            WriteJson(DraftRelativePath, draft);
            Debug.Log("AreaSet Blender 초안의 참조를 명시적으로 갱신했습니다. 배치 좌표는 변경하지 않았습니다.");
        }

        public static AreaSet지표면SnapshotData BuildSurfaceSnapshot()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshAssetPath)
                       ?? throw new InvalidOperationException("AreaSetTerrainMeshMissing:" + TerrainMeshAssetPath);
            var vertices = mesh.vertices;
            var xValues = vertices.Select(x => x.x).Distinct().OrderBy(x => x).ToArray();
            var zValues = vertices.Select(x => x.z).Distinct().OrderBy(x => x).ToArray();
            if (xValues.Length * zValues.Length != vertices.Length)
                throw new InvalidOperationException("AreaSetTerrainMeshNotRegularGrid");
            var heights = new float[vertices.Length];
            for (var zIndex = 0; zIndex < zValues.Length; zIndex++)
            for (var xIndex = 0; xIndex < xValues.Length; xIndex++)
            {
                var x = xValues[xIndex];
                var z = zValues[zIndex];
                var matches = vertices.Where(value => Mathf.Approximately(value.x, x)
                                                     && Mathf.Approximately(value.z, z)).ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException($"AreaSetTerrainGridPointAmbiguous:{x:R}:{z:R}");
                heights[zIndex * xValues.Length + xIndex] = matches[0].y;
            }
            var value = new AreaSet지표면SnapshotData
            {
                SnapshotStableId = SurfaceSnapshotStableId,
                SnapshotRevision = "surface-r1",
                AreaSetStableId = AreaSetStableId,
                SourceMeshAssetPath = TerrainMeshAssetPath,
                SourceMeshDependencyHash = AssetDatabase.GetAssetDependencyHash(TerrainMeshAssetPath).ToString(),
                Columns = xValues.Length,
                Rows = zValues.Length,
                MinimumX = xValues.First(),
                MaximumX = xValues.Last(),
                MinimumZ = zValues.First(),
                MaximumZ = zValues.Last(),
                Heights = heights,
                Triangles = mesh.triangles,
            };
            value.SurfaceHashSha256 = AreaSet배치Hash.ComputeSurface(value);
            return value;
        }

        public static AreaSet배치자산CatalogData BuildCalibrationCatalog()
        {
            var items = CandidateSeeds.Select(Measure).ToArray();
            var catalog = new AreaSet배치자산CatalogData
            {
                CatalogStableId = CalibrationCatalogStableId,
                CatalogRevision = "calibration-r1",
                Items = items,
            };
            catalog.CatalogHashSha256 = AreaSet배치Hash.ComputeCatalog(catalog);
            return catalog;
        }

        private static AreaSet배치자산CalibrationData Measure(CandidateSeed seed)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(seed.PrefabAssetPath)
                         ?? throw new InvalidOperationException("AreaSetCalibrationPrefabMissing:" + seed.PrefabAssetPath);
            var root = PrefabUtility.LoadPrefabContents(seed.PrefabAssetPath);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("AreaSetCalibrationRendererMissing:" + seed.AssetKey);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var value = new AreaSet배치자산CalibrationData
                {
                    AssetKey = seed.AssetKey,
                    H1StableId = seed.H1StableId,
                    PlacementKindCode = seed.PlacementKindCode,
                    PrefabAssetPath = seed.PrefabAssetPath,
                    ModelAssetPath = seed.ModelAssetPath,
                    TextureAssetPath = seed.TextureAssetPath,
                    AssetGuid = AssetDatabase.AssetPathToGUID(seed.PrefabAssetPath),
                    AssetDependencyHash = AssetDatabase.GetAssetDependencyHash(seed.PrefabAssetPath).ToString(),
                    BoundsMinimumX = bounds.min.x,
                    BoundsMinimumY = bounds.min.y,
                    BoundsMinimumZ = bounds.min.z,
                    BoundsCenterX = bounds.center.x,
                    BoundsCenterY = bounds.center.y,
                    BoundsCenterZ = bounds.center.z,
                    BoundsSizeX = bounds.size.x,
                    BoundsSizeY = bounds.size.y,
                    BoundsSizeZ = bounds.size.z,
                    MaximumSlopeDegrees = seed.MaximumSlopeDegrees,
                    MaximumHeightSpreadMeters = seed.MaximumHeightSpreadMeters,
                    SupportsSurfaceOffset = seed.SupportsSurfaceOffset,
                    CollisionEligible = seed.CollisionEligible,
                };
                value.AssetFingerprint = AreaSet배치Hash.ComputeCalibrationFingerprint(value);
                return value;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AreaSet배치DraftData CreateInitialDraft(
            AreaSet지표면SnapshotData surface,
            AreaSet배치자산CatalogData catalog)
        {
            var assets = catalog.Items.ToDictionary(item => item.AssetKey, StringComparer.Ordinal);
            var placements = new List<AreaSet배치DraftItemData>
            {
                Item(assets, "farmhouse", "farmhouse-01", -16f, 9f, -8f, .62f),
                Item(assets, "work-yard", "work-yard-01", 1.5f, 9.5f, 180f, 1.35f),
                Item(assets, "loading-point", "loading-point-01", 3f, 1.8f, 0f, 1f,
                    AreaSet배치HeightPolicyCodes.SurfaceOffset, -.72f,
                    "콘크리트 기반 상부를 작업 지표면 가까이에 맞추는 E4 기초 매립 예외"),
            };
            var plotPositions = new[]
            {
                (-8f, .5f), (-5f, .5f), (-8f, -2.5f), (-5f, -2.5f),
                (-8f, -5.5f), (-5f, -5.5f),
            };
            for (var i = 0; i < plotPositions.Length; i++)
                placements.Add(Item(assets, "cultivation-plot-proxy", $"cultivation-plot-{i + 1:00}",
                    plotPositions[i].Item1, plotPositions[i].Item2, 0f, .5f, cluster: "cultivation-grid"));

            foreach (var (z, index) in new[] { (-12f, 1), (-1f, 2), (10f, 3) })
                placements.Add(Item(assets, "rural-road", $"rural-road-{index:00}", 15f, z, 0f, 1f,
                    cluster: "entry-route"));
            placements.Add(Item(assets, "wood-gate", "wood-gate-01", 15f, -16.2f, 0f, 1f,
                cluster: "farm-boundary", allowOverlap: new[] { "rural-road-01" }));
            var fenceX = new[] { -19f, -15.8f, -12.6f, -9.4f, -6.2f, -3f, .2f, 3.4f, 6.6f, 9.8f };
            for (var i = 0; i < fenceX.Length; i++)
                placements.Add(Item(assets, "wood-fence", $"south-fence-{i + 1:00}", fenceX[i], -16.2f,
                    0f, 1.28f, cluster: "farm-boundary"));
            var treeX = new[] { -20f, -14f, -8f, -2f, 4f, 10f, 16f, 21f };
            for (var i = 0; i < treeX.Length; i++)
                placements.Add(Item(assets, i % 2 == 0 ? "large-pine" : "pine",
                    $"forest-buffer-{i + 1:00}", treeX[i], 17.2f + (i % 3) * .5f,
                    i * 29f, i % 2 == 0 ? .72f : .82f, cluster: "forest-buffer"));

            return new AreaSet배치DraftData
            {
                DraftStableId = DraftStableId,
                DraftRevision = "draft-r1",
                AreaSetStableId = AreaSetStableId,
                SurfaceSnapshotStableId = surface.SnapshotStableId,
                SurfaceHashSha256 = surface.SurfaceHashSha256,
                CalibrationCatalogStableId = catalog.CatalogStableId,
                CalibrationCatalogHashSha256 = catalog.CatalogHashSha256,
                Camera = new AreaSet배치CameraRecipeData
                {
                    PositionX = 36f,
                    PositionY = 38f,
                    PositionZ = -42f,
                    TargetX = 0f,
                    TargetY = 1f,
                    TargetZ = 0f,
                    OrthographicSize = 52f,
                    PixelWidth = 1600,
                    PixelHeight = 1000,
                },
                Placements = placements.ToArray(),
                Routes = new[]
                {
                    new AreaSet배치RouteData
                    {
                        RouteStableId = "graph-edge:farm-entry-route",
                        WidthMeters = 5.5f,
                        Points = new[]
                        {
                            new AreaSet배치RoutePointData(15f, -22f),
                            new AreaSet배치RoutePointData(15f, 15f),
                        },
                    },
                },
            };
        }

        private static AreaSet배치DraftItemData Item(
            IReadOnlyDictionary<string, AreaSet배치자산CalibrationData> assets,
            string assetKey,
            string stableId,
            float x,
            float z,
            float rotation,
            float scale,
            string heightPolicy = AreaSet배치HeightPolicyCodes.SurfaceSnap,
            float offset = 0f,
            string reason = "",
            string cluster = "",
            string[] allowOverlap = null)
        {
            var asset = assets[assetKey];
            return new AreaSet배치DraftItemData
            {
                PlacementStableId = stableId,
                H1StableId = asset.H1StableId,
                AssetKey = assetKey,
                AssetFingerprint = asset.AssetFingerprint,
                LocalX = x,
                LocalZ = z,
                RotationYDegrees = rotation,
                UniformScale = scale,
                HeightPolicyCode = heightPolicy,
                SurfaceOffsetMeters = offset,
                HeightExceptionReason = reason,
                ClusterKey = cluster,
                AllowOverlapWithPlacementStableIds = allowOverlap ?? Array.Empty<string>(),
            };
        }

        private static string BuildStagingPrefab(
            AreaSet배치검증ReceiptData receipt,
            AreaSet지표면SnapshotData surface,
            AreaSet배치자산CatalogData catalog)
        {
            var assetDirectory = Path.GetDirectoryName(StagingPrefabAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetDirectory)) throw new InvalidOperationException("AreaSetStagingDirectoryInvalid");
            Directory.CreateDirectory(ProjectPath(assetDirectory));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("AreaSet배치초안_숲경계_한스생활농장_r3");
                SceneManager.MoveGameObjectToScene(root, scene);
                var terrain = new GameObject("지표면_" + surface.SurfaceHashSha256[..12]);
                terrain.transform.SetParent(root.transform, false);
                terrain.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshAssetPath);
                terrain.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialAssetPath);
                terrain.AddComponent<MeshCollider>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshAssetPath);

                var assets = catalog.Items.ToDictionary(item => item.AssetKey, StringComparer.Ordinal);
                foreach (var placement in receipt.ResolvedPlacements)
                {
                    var calibration = assets[placement.AssetKey];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(calibration.PrefabAssetPath)
                                 ?? throw new InvalidOperationException("AreaSetStagingPrefabMissing:" + calibration.PrefabAssetPath);
                    var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject
                                   ?? throw new InvalidOperationException("AreaSetStagingInstantiateFailed:" + placement.AssetKey);
                    instance.name = placement.PlacementStableId;
                    instance.transform.SetParent(root.transform, true);
                    instance.transform.localPosition = new Vector3(placement.LocalX, placement.LocalY, placement.LocalZ);
                    instance.transform.localRotation = Quaternion.Euler(0f, placement.RotationYDegrees, 0f);
                    instance.transform.localScale = Vector3.one * placement.UniformScale;
                }
                PrefabUtility.SaveAsPrefabAsset(root, StagingPrefabAssetPath);
                AssetDatabase.SaveAssets();
                return StagingPrefabAssetPath;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string CaptureStagingPrefab(
            string prefabAssetPath,
            AreaSet배치CameraRecipeData recipe,
            string outputRelativePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath)
                         ?? throw new InvalidOperationException("AreaSetStagingAssetMissing:" + prefabAssetPath);
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                var root = UnityEngine.Object.Instantiate(prefab);
                root.SetActive(true);
                SceneManager.MoveGameObjectToScene(root, scene);
                var visibleRenderers = root.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .ToArray();
                if (visibleRenderers.Length == 0)
                    throw new InvalidOperationException("AreaSetStagingRendererMissing:" + prefabAssetPath);
                var visibleBounds = visibleRenderers[0].bounds;
                foreach (var renderer in visibleRenderers.Skip(1)) visibleBounds.Encapsulate(renderer.bounds);
                var cameraObject = new GameObject("AreaSet_동일구도Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f, .075f, .10f);
                camera.orthographic = true;
                camera.orthographicSize = recipe.OrthographicSize;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 500f;
                camera.cullingMask = ~0;
                camera.transform.position = new Vector3(recipe.PositionX, recipe.PositionY, recipe.PositionZ);
                camera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(recipe.TargetX, recipe.TargetY, recipe.TargetZ) - camera.transform.position,
                    Vector3.up);

                var lightObject = new GameObject("AreaSet_주광");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.48f, .50f, .53f);

                target = new RenderTexture(recipe.PixelWidth, recipe.PixelHeight, 24, RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                Debug.Log($"AreaSet staging capture: renderers={visibleRenderers.Length};"
                          + $"boundsCenter={visibleBounds.center};boundsSize={visibleBounds.size};"
                          + $"camera={camera.transform.position};target=({recipe.TargetX},{recipe.TargetY},{recipe.TargetZ})");
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                image = new Texture2D(recipe.PixelWidth, recipe.PixelHeight, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, recipe.PixelWidth, recipe.PixelHeight), 0, 0);
                image.Apply();
                var output = ProjectPath(outputRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(output)
                                          ?? throw new InvalidOperationException("AreaSetCaptureDirectoryInvalid"));
                File.WriteAllBytes(output, image.EncodeToPNG());
                RenderTexture.active = previous;
                camera.targetTexture = null;
                return outputRelativePath.Replace('\\', '/');
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static T ReadJson<T>(string relativePath) where T : class =>
            AreaSet배치Json.Deserialize<T>(File.ReadAllText(ProjectPath(relativePath)));

        private static void WriteJson(string relativePath, object value)
        {
            var path = ProjectPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)
                                      ?? throw new InvalidOperationException("AreaSetAuthoringDirectoryInvalid"));
            File.WriteAllText(path, AreaSet배치Json.Serialize(value));
        }

        private static string ProjectPath(string relativePath) =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", relativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static string IncrementRevision(string value)
        {
            var split = value.LastIndexOf('r');
            if (split < 0 || !int.TryParse(value[(split + 1)..], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var revision)) return value + "-rebased-r1";
            return value[..(split + 1)] + (revision + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static CandidateSeed Seed(string assetKey, string h1, string kind,
            string prefab, string model, string texture, float slope, float spread,
            bool offset = false, bool collision = true) => new(assetKey, h1, kind,
            prefab, model, texture, slope, spread, offset, collision);

        private sealed class CandidateSeed
        {
            public CandidateSeed(
                string assetKey,
                string h1StableId,
                string placementKindCode,
                string prefabAssetPath,
                string modelAssetPath,
                string textureAssetPath,
                float maximumSlopeDegrees,
                float maximumHeightSpreadMeters,
                bool supportsSurfaceOffset,
                bool collisionEligible)
            {
                AssetKey = assetKey;
                H1StableId = h1StableId;
                PlacementKindCode = placementKindCode;
                PrefabAssetPath = prefabAssetPath;
                ModelAssetPath = modelAssetPath;
                TextureAssetPath = textureAssetPath;
                MaximumSlopeDegrees = maximumSlopeDegrees;
                MaximumHeightSpreadMeters = maximumHeightSpreadMeters;
                SupportsSurfaceOffset = supportsSurfaceOffset;
                CollisionEligible = collisionEligible;
            }

            public string AssetKey { get; }
            public string H1StableId { get; }
            public string PlacementKindCode { get; }
            public string PrefabAssetPath { get; }
            public string ModelAssetPath { get; }
            public string TextureAssetPath { get; }
            public float MaximumSlopeDegrees { get; }
            public float MaximumHeightSpreadMeters { get; }
            public bool SupportsSurfaceOffset { get; }
            public bool CollisionEligible { get; }
        }
    }
}
