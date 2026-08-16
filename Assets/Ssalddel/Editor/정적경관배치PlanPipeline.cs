using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Editor
{
    [Serializable]
    public sealed class 정적경관배치ValidationIssueData
    {
        public string SeverityCode = string.Empty;
        public string IssueCode = string.Empty;
        public string PlacementStableId = string.Empty;
        public string Detail = string.Empty;
    }

    [Serializable]
    public sealed class 정적경관배치PerformanceTotalData
    {
        public long Triangles;
        public int MaterialSlots;
        public int DrawCalls;
        public int ShadowCasters;
        public int Colliders;
        public int Animators;
    }

    [Serializable]
    public sealed class 정적경관배치ValidationReportData
    {
        public string PlanStableId = string.Empty;
        public string BriefStableId = string.Empty;
        public string BriefRevision = string.Empty;
        public string BriefHashSha256 = string.Empty;
        public string BasePlanHashSha256 = string.Empty;
        public string OverrideHashSha256 = string.Empty;
        public string MergedPlanHashSha256 = string.Empty;
        public string RenderingProfileStableId = string.Empty;
        public string RenderingProfileRevision = string.Empty;
        public string RenderingProfileHashSha256 = string.Empty;
        public string EffectiveReviewStateCode = 정적경관배치ReviewStateCodes.Draft;
        public bool ReviewMatchesInputs;
        public string ReviewMismatchReason = string.Empty;
        public string GeneratedAtUtc = string.Empty;
        public int EnabledPlacementCount;
        public int ErrorCount;
        public int WarningCount;
        public 정적경관배치PerformanceTotalData PerformanceTotal = new();
        public 정적경관PerformanceBudgetData PerformanceBudget = new();
        public string[] StagingPrefabPaths = Array.Empty<string>();
        public 정적경관배치ValidationIssueData[] Issues =
            Array.Empty<정적경관배치ValidationIssueData>();

        public bool CanStage => ErrorCount == 0;
        public bool CanApply => CanStage
            && ReviewMatchesInputs
            && EffectiveReviewStateCode
                == 정적경관배치ReviewStateCodes.ApprovedForSceneApply;
    }

    public sealed class 정적경관배치ContainerInfoData
    {
        public string StableId = string.Empty;
        public string DisplayName = string.Empty;
        public Vector2 Minimum;
        public Vector2 Maximum;
        public Vector2 WorldAnchor;
        public float LocalToAnchorScale;
        public float LocalRotationY;
    }

    public static class 정적경관배치PlanPipeline
    {
        public const string PlanDirectory =
            "Assets/Ssalddel/Data/WorldPlacementPlans/pyeongchang-farm-hub-town-v1";
        public const string BasePlanPath = PlanDirectory + "/pyeongchang-static-scenery.base.json";
        public const string OverridePlanPath = PlanDirectory + "/pyeongchang-static-scenery.override.json";
        public const string StagingDirectory =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World/StaticSceneryStaging";
        public const string CompositionCatalogRevision = 팩경관CompositionSetBuilder.CatalogRevision;

        private const string ReportRelativePath =
            "artifacts/local/world-placement/pyeongchang-static-scenery.validation.json";
        private const string ReportMarkdownRelativePath =
            "artifacts/local/world-placement/pyeongchang-static-scenery.validation.md";
        private const string GeneratedRootName = "StaticSceneryGeneratedRoot";
        private const string CandidateRootName = GeneratedRootName + "__Candidate";
        private const string BackupRootName = GeneratedRootName + "__Backup";
        private const string LegacySceneryPrefix = "scenic_sim_pyeongchang_";
        private const string PreservedObservedWarehouseFixtureName =
            "scenic_sim_pyeongchang_l2-700-1145-observed-fixture-barn";
        private const string CompletionAreaRootName =
            "CompletionArea_대관령면Farm_1km_L2_2x2";
        private const string CorridorLayerName =
            "L5_FarmHubTown이동회랑_SimulationRoute";
        private const string ScenicLayerName = "L4_L7_Synty경관_PresentationOnly";

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        private static readonly IReadOnlyDictionary<string, ContainerDefinition> Containers =
            new Dictionary<string, ContainerDefinition>(StringComparer.Ordinal)
            {
                [정적경관배치ContainerStableIds.FarmSouthWest] =
                    new("daegwallyeong-700-1144", new Vector2(-32f, -22f),
                        new Vector2(32f, 22f), new Vector2(16f, 7f)),
                [정적경관배치ContainerStableIds.FarmSouthEast] =
                    new("daegwallyeong-701-1144", new Vector2(15f, -3f),
                        new Vector2(37f, 17f), Vector2.zero),
                [정적경관배치ContainerStableIds.FarmNorthWest] =
                    new("daegwallyeong-700-1145", new Vector2(3f, 4f),
                        new Vector2(31f, 31f), Vector2.zero),
                [정적경관배치ContainerStableIds.FarmNorthEast] =
                    new("daegwallyeong-701-1145", new Vector2(14f, 4f),
                        new Vector2(38f, 30f), Vector2.zero),
                [정적경관배치ContainerStableIds.FarmHubCorridor] =
                    new("farm-hub-corridor", new Vector2(0f, -4f),
                        new Vector2(32f, 22f), Vector2.zero),
                [정적경관배치ContainerStableIds.JinbuHub] =
                    new("jinbu-hub", new Vector2(-30f, -24f),
                        new Vector2(30f, 24f), new Vector2(8f, 11.5f)),
                [정적경관배치ContainerStableIds.HubTownCorridor] =
                    new("hub-town-corridor", new Vector2(-28f, -28f),
                        new Vector2(21f, 18f), Vector2.zero),
                [정적경관배치ContainerStableIds.PyeongchangTown] =
                    new("pyeongchang-town", new Vector2(-32f, -24f),
                        new Vector2(32f, 24f), new Vector2(-14f, -14.5f)),
            };

        [MenuItem("Ssalddel/WORLD-PLAN-1 평창 정적 경관 기본 JSON 생성")]
        public static void GenerateBasePlanJson()
        {
            Directory.CreateDirectory(PlanDirectory);
            SyntyPackAssetInventoryBuilder.Build();
            자연경관CompositionSetBuilder.Build();
            팩경관CompositionSetBuilder.Build();
            var visualCatalog = LoadVisualCatalog();
            var compositionRegistry = LoadCompositionRegistry();
            var plan = BuildMigrationPlan(compositionRegistry);
            plan.PerformanceBudget = BuildInitialBudget(plan, visualCatalog, compositionRegistry);
            정적경관배치PlanValidator.Validate(plan);
            WriteJson(BasePlanPath, plan);
            var baseHash = 정적경관배치PlanHash.Compute(plan);
            if (!File.Exists(OverridePlanPath))
            {
                WriteJson(OverridePlanPath, new 정적경관배치OverridePlanData
                {
                    OverrideStableId = "world-placement-override:pyeongchang-farm-hub-town-v1",
                    BasePlanStableId = plan.PlanStableId,
                    ExpectedBasePlanHashSha256 = baseHash,
                });
            }
            else
            {
                var existingOverride = LoadOverridePlan();
                if (existingOverride.Changes.Length == 0
                    && existingOverride.ExpectedBasePlanHashSha256 != baseHash)
                {
                    existingOverride.ExpectedBasePlanHashSha256 = baseHash;
                    WriteJson(OverridePlanPath, existingOverride);
                }
            }
            정적경관배치ReviewService.EnsureArtifacts(plan);
            AssetDatabase.Refresh();
            Debug.Log("WORLD-PLAN-1: Scene을 수정하지 않고 평창 정적 경관 기본·보정 JSON을 준비했습니다.");
        }

        [MenuItem("Ssalddel/WORLD-PLAN-2 평창 정적 경관 계획 검증과 staging 생성")]
        public static void ValidateAndStageMenu()
        {
            var report = ValidateAndStage();
            if (!report.CanStage)
                throw new InvalidOperationException(
                    "StaticSceneryPlacementValidationFailed:" + report.ErrorCount);
            Debug.Log($"WORLD-PLAN-2: 배치 {report.EnabledPlacementCount}건을 검증하고 "
                + $"Container staging {report.StagingPrefabPaths.Length}개를 생성했습니다. "
                + $"경고={report.WarningCount}");
        }

        [MenuItem("Ssalddel/WORLD-PLAN-3 검증된 평창 정적 경관 Scene 적용")]
        public static void ApplyValidatedPlanToScene()
        {
            var report = ValidateAndStage();
            if (!report.CanApply)
                throw new InvalidOperationException(
                    "StaticSceneryPlacementApplyBlocked:errors=" + report.ErrorCount
                    + ";review=" + report.EffectiveReviewStateCode
                    + ";mismatch=" + report.ReviewMismatchReason);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != 대한민국법정동WorldBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(
                    대한민국법정동WorldBuilder.ScenePath, OpenSceneMode.Single);

            var anchors = EnsureAnchorsForCurrentSceneLayout(scene)
                .Where(item => item.ValidateWiring())
                .ToDictionary(item => item.ContainerStableId, StringComparer.Ordinal);
            var requiredContainerIds = LoadMergedPlan().Placements
                .Where(item => item.Enabled)
                .Select(item => item.TargetContainerStableId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var missing = requiredContainerIds.Where(id => !anchors.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    "StaticSceneryPlacementAnchorMissing:" + string.Join(",", missing));

            var candidates = new List<(정적경관배치AnchorView Anchor, GameObject Candidate)>();
            try
            {
                foreach (var containerId in requiredContainerIds)
                {
                    var path = StagingPrefabPath(containerId);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                        ?? throw new InvalidOperationException(
                            "StaticSceneryStagingPrefabMissing:" + path);
                    var candidate = PrefabUtility.InstantiatePrefab(
                            prefab, anchors[containerId].transform) as GameObject
                        ?? throw new InvalidOperationException(
                            "StaticSceneryCandidateInstantiateFailed:" + containerId);
                    candidate.name = CandidateRootName;
                    var receipt = candidate.GetComponent<정적경관배치ReceiptView>();
                    if (receipt == null || !receipt.ValidateWiring()
                        || receipt.MergedPlanHashSha256 != report.MergedPlanHashSha256)
                        throw new InvalidOperationException(
                            "StaticSceneryCandidateReceiptInvalid:" + containerId);
                    candidates.Add((anchors[containerId], candidate));
                }
            }
            catch
            {
                foreach (var candidate in candidates)
                    UnityEngine.Object.DestroyImmediate(candidate.Candidate);
                throw;
            }

            var backups = new List<GameObject>();
            foreach (var value in candidates)
            {
                var old = value.Anchor.transform.Find(GeneratedRootName);
                if (old != null)
                {
                    old.name = BackupRootName;
                    old.gameObject.SetActive(false);
                    backups.Add(old.gameObject);
                }
                value.Candidate.name = GeneratedRootName;
            }

            var legacyBackups = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(IsLegacyStaticSceneryRoot)
                .Select(value => value.gameObject)
                .ToArray();
            foreach (var legacy in legacyBackups) legacy.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                foreach (var value in candidates)
                    UnityEngine.Object.DestroyImmediate(value.Candidate);
                foreach (var backup in backups)
                {
                    backup.name = GeneratedRootName;
                    backup.SetActive(true);
                }
                foreach (var legacy in legacyBackups) legacy.SetActive(true);
                throw new InvalidOperationException("StaticScenerySceneSaveFailed");
            }
            foreach (var backup in backups) UnityEngine.Object.DestroyImmediate(backup);
            foreach (var legacy in legacyBackups) UnityEngine.Object.DestroyImmediate(legacy);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("StaticSceneryBackupCleanupSaveFailed");
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD-PLAN-3: 검증된 JSON hash와 일치하는 정적 경관만 Scene에 적용했습니다. "
                + $"이전 중복 경관 제거={legacyBackups.Length}");
        }

        public static 정적경관배치ValidationReportData ValidateAndStage()
        {
            var basePlan = ReadJson<정적경관배치PlanData>(BasePlanPath);
            var overridePlan = ReadJson<정적경관배치OverridePlanData>(OverridePlanPath);
            var merged = 정적경관배치PlanMerger.Merge(basePlan, overridePlan);
            var visualCatalog = LoadVisualCatalog();
            var compositionRegistry = LoadCompositionRegistry();
            var issues = new List<정적경관배치ValidationIssueData>();
            if (visualCatalog.CatalogRevision != merged.VisualCatalogRevision)
                Error(issues, "VisualCatalogRevisionMismatch", string.Empty,
                    $"계획={merged.VisualCatalogRevision};현재={visualCatalog.CatalogRevision}");
            if (merged.CompositionCatalogRevision != CompositionCatalogRevision)
                Error(issues, "CompositionCatalogRevisionMismatch", string.Empty,
                    $"계획={merged.CompositionCatalogRevision};현재={CompositionCatalogRevision}");
            var renderingProfile = 평창군경관RenderingFixture.Create();
            var renderingProfileHash = 경관RenderingProfileHash.Compute(renderingProfile);
            if (merged.RenderingProfileStableId != renderingProfile.ProfileStableId
                || merged.RenderingProfileRevision != renderingProfile.RuleRevision
                || !string.Equals(merged.RenderingProfileHashSha256,
                    renderingProfileHash, StringComparison.OrdinalIgnoreCase))
                Error(issues, "RenderingProfileReferenceMismatch", string.Empty,
                    $"계획={merged.RenderingProfileStableId}:{merged.RenderingProfileRevision};"
                    + $"현재={renderingProfile.ProfileStableId}:{renderingProfile.RuleRevision}");
            ValidateContainerTransformContract(merged, issues);

            var totals = new 정적경관배치PerformanceTotalData();
            var resolved = new Dictionary<string, ResolvedAsset>(StringComparer.Ordinal);
            foreach (var placement in merged.Placements.Where(item => item.Enabled))
            {
                if (!Containers.TryGetValue(placement.TargetContainerStableId, out var container))
                {
                    Error(issues, "TargetContainerMissing", placement.PlacementStableId,
                        placement.TargetContainerStableId);
                    continue;
                }
                if (!container.Contains(placement.Position.X, placement.Position.Z))
                    Error(issues, "TargetContainerBoundsRejected", placement.PlacementStableId,
                        $"x={placement.Position.X};z={placement.Position.Z}");
                var asset = ResolveAsset(placement, visualCatalog, compositionRegistry, issues);
                if (asset == null) continue;
                if (!container.ContainsFootprint(
                        placement.Position.X, placement.Position.Z,
                        asset.Footprint, placement.RotationY, placement.UniformScale))
                    Error(issues, "TargetContainerFootprintRejected",
                        placement.PlacementStableId,
                        $"footprint={asset.Footprint.x:0.###}x{asset.Footprint.y:0.###};"
                        + $"rotation={placement.RotationY:0.###};scale={placement.UniformScale:0.###}");
                resolved.Add(placement.PlacementStableId, asset);
                AddTotals(totals, asset);
            }
            ValidateCropsInsideSoilRows(merged, resolved, issues);
            AddOverlapWarnings(merged, resolved, issues);
            CheckBudget(merged.PerformanceBudget, totals, issues);
            if (merged.Source.SpatialEvidenceStatusCode != "PhysicalSpatialArtifact")
                Warning(issues, "ScenarioPreviewHeightEvidence", string.Empty,
                    "실제 DEM 배치 높이가 아니라 ScenarioPreview 높이 정책을 사용합니다.");

            var baseHash = 정적경관배치PlanHash.Compute(basePlan);
            var overrideHash = ComputeOverrideHash(overridePlan);
            var mergedHash = 정적경관배치PlanHash.Compute(merged);
            var review = new 정적경관배치ReviewStatusData();
            try
            {
                review = 정적경관배치ReviewService.EvaluateCurrent(
                    basePlan, overridePlan, merged);
            }
            catch (InvalidOperationException error)
            {
                Error(issues, "PlacementBriefOrReviewInvalid", string.Empty, error.Message);
            }
            var report = new 정적경관배치ValidationReportData
            {
                PlanStableId = merged.PlanStableId,
                BriefStableId = review.Brief.BriefStableId,
                BriefRevision = review.Brief.BriefRevision,
                BriefHashSha256 = review.BriefHashSha256,
                BasePlanHashSha256 = baseHash,
                OverrideHashSha256 = overrideHash,
                MergedPlanHashSha256 = mergedHash,
                RenderingProfileStableId = merged.RenderingProfileStableId,
                RenderingProfileRevision = merged.RenderingProfileRevision,
                RenderingProfileHashSha256 = merged.RenderingProfileHashSha256,
                EffectiveReviewStateCode = review.EffectiveReviewStateCode,
                ReviewMatchesInputs = review.ReviewMatchesInputs,
                ReviewMismatchReason = review.MismatchReason,
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                EnabledPlacementCount = merged.Placements.Count(item => item.Enabled),
                ErrorCount = issues.Count(item => item.SeverityCode == "Error"),
                WarningCount = issues.Count(item => item.SeverityCode == "Warning"),
                PerformanceTotal = totals,
                PerformanceBudget = merged.PerformanceBudget,
                Issues = issues.ToArray(),
            };
            if (report.CanStage)
                report.StagingPrefabPaths = BuildStagingPrefabs(
                    merged, resolved, baseHash, overrideHash, mergedHash);
            WriteReport(report);
            return report;
        }

        public static 정적경관배치PlanData LoadBasePlan() =>
            ReadJson<정적경관배치PlanData>(BasePlanPath);

        public static 정적경관배치OverridePlanData LoadOverridePlan() =>
            ReadJson<정적경관배치OverridePlanData>(OverridePlanPath);

        public static 정적경관배치PlanData LoadMergedPlan()
        {
            var basePlan = LoadBasePlan();
            return 정적경관배치PlanMerger.Merge(basePlan, LoadOverridePlan());
        }

        public static void SaveOverridePlan(정적경관배치OverridePlanData value)
        {
            정적경관배치PlanValidator.ValidateOverride(value, LoadBasePlan());
            WriteJson(OverridePlanPath, value);
            AssetDatabase.Refresh();
        }

        public static string ComputeOverrideHash(정적경관배치OverridePlanData value) =>
            정적경관배치PlanHash.Sha256(
                JsonConvert.SerializeObject(value, JsonSettings));

        public static IReadOnlyList<정적경관배치ContainerInfoData> GetContainerInfos() =>
            Containers.Select(item => new 정적경관배치ContainerInfoData
                {
                    StableId = item.Key,
                    DisplayName = item.Value.FileCode,
                    Minimum = item.Value.Minimum,
                    Maximum = item.Value.Maximum,
                    WorldAnchor = item.Value.WorldAnchor,
                    LocalToAnchorScale = item.Value.LocalToAnchorScale,
                    LocalRotationY = item.Value.LocalRotationY,
                })
                .OrderBy(item => item.StableId, StringComparer.Ordinal)
                .ToArray();

        public static 정적경관배치AnchorView ConfigureAnchor(
            Transform value,
            string containerStableId)
        {
            if (!Containers.TryGetValue(containerStableId, out var definition))
                throw new InvalidOperationException(
                    "StaticSceneryContainerDefinitionMissing:" + containerStableId);
            var anchor = value.GetComponent<정적경관배치AnchorView>()
                ?? value.gameObject.AddComponent<정적경관배치AnchorView>();
            anchor.Configure(definition.ToData(containerStableId));
            return anchor;
        }

        /// <summary>
        /// 통합 Scene 재조립 과정에서 표현 전용 Anchor component가 빠져도 기존 지형과
        /// 업무 객체를 다시 만들지 않고 정적 경관 적용 계약만 복구합니다.
        /// </summary>
        public static IReadOnlyList<정적경관배치AnchorView> EnsureAnchorsForSceneLayout(
            Transform completionAreaRoot,
            Transform corridorLayer,
            Transform scenicLayer)
        {
            if (completionAreaRoot == null || corridorLayer == null || scenicLayer == null)
                throw new InvalidOperationException("StaticSceneryPlacementLayoutRootMissing");

            var southWest = RequireDirectChild(completionAreaRoot,
                "Tile_kr5186_l2_700_1144_농장마당_Reference");
            var southEast = RequireDirectChild(completionAreaRoot,
                "Tile_kr5186_l2_701_1144_감자경작지");
            var northWest = RequireDirectChild(completionAreaRoot,
                "Tile_kr5186_l2_700_1145_산림전이");
            var northEast = RequireDirectChild(completionAreaRoot,
                "Tile_kr5186_l2_701_1145_출발회랑");

            var farmHub = FindOrCreateDirectChild(
                corridorLayer, "StaticSceneryAnchor_FarmHubCorridor");
            var hubTown = FindOrCreateDirectChild(
                corridorLayer, "StaticSceneryAnchor_HubTownCorridor");
            var areaAnchors = FindOrCreateDirectChild(
                scenicLayer, "StaticSceneryAreaAnchors_Hub_Town");
            var jinbuHub = FindOrCreateDirectChild(
                areaAnchors, "StaticSceneryAnchor_진부Hub");
            var pyeongchangTown = FindOrCreateDirectChild(
                areaAnchors, "StaticSceneryAnchor_평창읍Town");

            return new[]
            {
                ConfigureAnchor(southWest, 정적경관배치ContainerStableIds.FarmSouthWest),
                ConfigureAnchor(southEast, 정적경관배치ContainerStableIds.FarmSouthEast),
                ConfigureAnchor(northWest, 정적경관배치ContainerStableIds.FarmNorthWest),
                ConfigureAnchor(northEast, 정적경관배치ContainerStableIds.FarmNorthEast),
                ConfigureAnchor(farmHub, 정적경관배치ContainerStableIds.FarmHubCorridor),
                ConfigureAnchor(hubTown, 정적경관배치ContainerStableIds.HubTownCorridor),
                ConfigureAnchor(jinbuHub, 정적경관배치ContainerStableIds.JinbuHub),
                ConfigureAnchor(pyeongchangTown, 정적경관배치ContainerStableIds.PyeongchangTown),
            };
        }

        public static bool IsLegacyStaticSceneryRoot(Transform value) =>
            value != null
            && value.name.StartsWith(LegacySceneryPrefix, StringComparison.Ordinal)
            && value.name != PreservedObservedWarehouseFixtureName
            && value.GetComponent<정적경관배치InstanceView>() == null;

        private static IReadOnlyList<정적경관배치AnchorView>
            EnsureAnchorsForCurrentSceneLayout(UnityEngine.SceneManagement.Scene scene)
        {
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            return EnsureAnchorsForSceneLayout(
                RequireUniqueSceneTransform(transforms, CompletionAreaRootName),
                RequireUniqueSceneTransform(transforms, CorridorLayerName),
                RequireUniqueSceneTransform(transforms, ScenicLayerName));
        }

        private static Transform RequireUniqueSceneTransform(
            IEnumerable<Transform> transforms,
            string name)
        {
            var matches = transforms.Where(value => value.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    "StaticSceneryPlacementLayoutNodeInvalid:" + name
                    + ":count=" + matches.Length);
            return matches[0];
        }

        private static Transform RequireDirectChild(Transform parent, string name) =>
            parent.Find(name) ?? throw new InvalidOperationException(
                "StaticSceneryPlacementLayoutNodeMissing:" + name);

        private static Transform FindOrCreateDirectChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var created = new GameObject(name).transform;
            created.SetParent(parent, false);
            return created;
        }

        private static 정적경관배치PlanData BuildMigrationPlan(
            FourPackCompositionRegistry compositionRegistry)
        {
            var renderingProfile = 평창군경관RenderingFixture.Create();
            var placements = new List<정적경관배치ItemData>();
            foreach (var source in 평창군경관Fixture.Create().Placements)
            {
                if (source.VisualKey == 법정동경관VisualKeys.Van) continue;
                if (source.RegionRoleCode == 법정동WorldRoleCodes.Farm
                    && IsLegacyNatureVisualKey(source.VisualKey))
                    continue;
                var container = ResolveMigratedContainer(source);
                if (container == 정적경관배치ContainerStableIds.FarmSouthWest
                    || container == 정적경관배치ContainerStableIds.JinbuHub
                    || container == 정적경관배치ContainerStableIds.PyeongchangTown)
                    continue;
                placements.Add(Visual(
                    source.PlacementStableId,
                    container,
                    source.RegionStableId, source.VisualKey, source.LandCoverCode,
                    source.RegionRoleCode, source.LocalX, source.LocalZ,
                    source.RotationY, source.Scale, source.DensityTier, source.LodGroup));
            }

            AddFarmCompletionPlacements(placements, compositionRegistry);
            AddRepresentativeHubAndTownPlacements(placements, compositionRegistry);
            AddCorridorPlacements(placements, compositionRegistry);
            return new 정적경관배치PlanData
            {
                PlanStableId = "world-placement:pyeongchang-farm-hub-town-v1",
                PlanRevision = "pyeongchang-static-scenery.v4",
                Source = new 정적경관배치SourceData
                {
                    KindCode = 정적경관배치SourceKindCodes.Fixture,
                    StableId = "fixture:pyeongchang-static-scenery.v4",
                    OutputHashSha256 = 정적경관배치PlanHash.Sha256(
                        "fixture:pyeongchang-static-scenery.v4|51760|four-pack-composition.v2|nature-late-morning.v2"),
                    SpatialEvidenceStatusCode = "ScenarioPreview",
                },
                AreaSetStableId = "pyeongchang-farm-hub-town-v1",
                VisualCatalogRevision = "legal-dong-scenic-catalog.v2",
                CompositionCatalogRevision = CompositionCatalogRevision,
                RenderingProfileStableId = renderingProfile.ProfileStableId,
                RenderingProfileRevision = renderingProfile.RuleRevision,
                RenderingProfileHashSha256 = 경관RenderingProfileHash.Compute(
                    renderingProfile),
                Seed = 51760,
                PerformanceBudget = new 정적경관PerformanceBudgetData
                {
                    TriangleLimit = 1, MaterialSlotLimit = 1, DrawCallLimit = 1,
                    ShadowCasterLimit = 1, ColliderLimit = 1, AnimatorLimit = 1,
                },
                ContainerTransforms = Containers
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => item.Value.ToData(item.Key))
                    .ToArray(),
                Placements = placements
                    .OrderBy(item => item.PlacementStableId, StringComparer.Ordinal)
                    .ToArray(),
            };
        }

        private static string ResolveMigratedContainer(법정동경관PlacementData source)
        {
            if (source.RegionRoleCode == 법정동WorldRoleCodes.Hub)
                return 정적경관배치ContainerStableIds.JinbuHub;
            if (source.RegionRoleCode == 법정동WorldRoleCodes.Town)
                return 정적경관배치ContainerStableIds.PyeongchangTown;
            if (source.RegionRoleCode != 법정동WorldRoleCodes.Farm)
                throw new InvalidOperationException(
                    "StaticSceneryMigrationRoleUnsupported:" + source.RegionRoleCode);

            var east = source.LocalX > 21f;
            var north = source.LocalZ > 12.5f;
            if (north)
                return east
                    ? 정적경관배치ContainerStableIds.FarmNorthEast
                    : 정적경관배치ContainerStableIds.FarmNorthWest;
            return east
                ? 정적경관배치ContainerStableIds.FarmSouthEast
                : 정적경관배치ContainerStableIds.FarmSouthWest;
        }

        private static bool IsLegacyNatureVisualKey(string visualKey) =>
            visualKey == 법정동경관VisualKeys.MountainSoft
            || visualKey == 법정동경관VisualKeys.DistantMountain
            || visualKey == 법정동경관VisualKeys.TreePatch
            || visualKey == 법정동경관VisualKeys.MixedTreePatch
            || visualKey == 법정동경관VisualKeys.ConiferTree
            || visualKey == 법정동경관VisualKeys.BroadleafTree
            || visualKey == 법정동경관VisualKeys.Understory
            || visualKey == 법정동경관VisualKeys.ForestEdge
            || visualKey == 법정동경관VisualKeys.SmallRocks
            || visualKey == 법정동경관VisualKeys.RockWall
            || visualKey == 법정동경관VisualKeys.Tree;

        private static void AddFarmCompletionPlacements(
            ICollection<정적경관배치ItemData> values,
            FourPackCompositionRegistry compositionRegistry)
        {
            var farm = 평창군법정동WorldFixture.FarmRegionStableId;
            void Add(string container, string suffix, string visualKey, string cover,
                float x, float z, float rotation, float scale, int density, int lod) =>
                values.Add(Visual(
                    "scenic:sim:pyeongchang:completion-area-farm-" + suffix,
                    container, farm, visualKey, cover, 법정동WorldRoleCodes.Farm,
                    x, z, rotation, scale, density, lod));

            var sw = 정적경관배치ContainerStableIds.FarmSouthWest;
            AddPackComposition(values, compositionRegistry, sw,
                "representative-farm-barn-yard",
                월드CompositionPackCodes.Farm, 농장풍경SetNames.헛간작업마당, "A",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                -17f, 7f, 0f, 1f, 18f);
            AddPackComposition(values, compositionRegistry, sw,
                "representative-farm-potato-field",
                월드CompositionPackCodes.Farm, 농장풍경SetNames.감자밭두렁, "B",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                9f, 8f, 0f, 1f, 18f);
            AddPackComposition(values, compositionRegistry, sw,
                "representative-farm-collection-yard",
                월드CompositionPackCodes.Farm, 농장풍경SetNames.수확물집하장, "C",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                12f, -9f, 0f, 1f, 18f);
            AddNatureComposition(values, compositionRegistry, sw,
                "farm-yard-broadleaf-buffer", 자연경관SetNames.활엽수림군집,
                "kr5186:l2:700:1144:broadleaf-buffer-01",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                -25f, 16f, -18f, .30f, 28f);
            AddNatureComposition(values, compositionRegistry, sw,
                "farm-yard-forest-edge", 자연경관SetNames.숲가장자리,
                "kr5186:l2:700:1144:forest-edge-01",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                -7f, 18f, 82f, .32f, 18f);
            Add(sw, "farm-yard-tree-west-a", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, -26f, 6f, 17f, .54f, 2, 2);
            Add(sw, "farm-yard-tree-west-b", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, -24f, -5f, 71f, .50f, 2, 2);
            Add(sw, "farm-yard-tree-east", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, 24f, 17f, 138f, .56f, 2, 2);
            Add(sw, "farm-yard-understory-west", 법정동경관VisualKeys.Understory,
                법정동LandCoverCodes.Forest, -11f, 19f, 31f, .45f, 2, 2);
            Add(sw, "farm-yard-understory-east", 법정동경관VisualKeys.Understory,
                법정동LandCoverCodes.Forest, 18f, 19f, -27f, .45f, 2, 2);

            var se = 정적경관배치ContainerStableIds.FarmSouthEast;
            // Dirt Rows 원본 Prefab의 시각 중심은 root Pivot에서 좌측 0.72m,
            // 앞쪽 0.96m 치우쳐 있다. 제작 좌표를 시각 중심으로 해석할 수 있도록
            // 밭고랑 root에 반대 보정을 적용하고, 작물은 그 경계 안쪽에 배치한다.
            const float fieldPivotCompensationX = .72f;
            const float fieldPivotCompensationZ = -.96f;
            const float fieldRotationY = 8f;
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 4; column++)
                Add(se, $"field-{row}-{column}", 법정동경관VisualKeys.SoilRows,
                    법정동LandCoverCodes.Cropland,
                    22.5f + fieldPivotCompensationX + column * 2.1f,
                    3.9f + fieldPivotCompensationZ + row * 2.35f,
                    fieldRotationY, .34f, 1, 1);
            var fieldRotationRadians = fieldRotationY * Mathf.Deg2Rad;
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 4; column++)
            for (var cropIndex = 0; cropIndex < 2; cropIndex++)
            {
                var localOffsetX = cropIndex == 0 ? -.38f : .38f;
                var cropX = 22.5f + column * 2.1f
                    + Mathf.Cos(fieldRotationRadians) * localOffsetX;
                var cropZ = 3.9f + row * 2.35f
                    - Mathf.Sin(fieldRotationRadians) * localOffsetX;
                Add(se, $"potato-{row}-{column}-{cropIndex}", 법정동경관VisualKeys.Potato,
                    법정동LandCoverCodes.Cropland, cropX, cropZ,
                    fieldRotationY, .38f, 2, 2);
            }
            AddNatureComposition(values, compositionRegistry, se,
                "field-edge-west", 자연경관SetNames.숲가장자리,
                "kr5186:l2:701:1144:forest-edge-west",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                18.2f, 14.8f, 91f, .30f, 18f);
            AddNatureComposition(values, compositionRegistry, se,
                "field-edge-east", 자연경관SetNames.숲가장자리,
                "kr5186:l2:701:1144:forest-edge-east",
                farm, 법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                31.2f, 14.8f, 88f, .30f, 18f);
            Add(se, "field-edge-tree-west", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, 23f, 14f, 31f, .52f, 2, 2);
            Add(se, "field-edge-tree-east", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, 27f, 14.2f, 109f, .56f, 2, 2);

            var nw = 정적경관배치ContainerStableIds.FarmNorthWest;
            AddNatureComposition(values, compositionRegistry, nw,
                "forest-broadleaf-composition", 자연경관SetNames.활엽수림군집,
                "kr5186:l2:700:1145:broadleaf-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                6.5f, 22.5f, -16f, .34f, 28f);
            AddNatureComposition(values, compositionRegistry, nw,
                "forest-conifer-composition", 자연경관SetNames.침엽수림군집,
                "kr5186:l2:700:1145:conifer-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                26.5f, 22.5f, 29f, .34f, 34f);
            AddNatureComposition(values, compositionRegistry, nw,
                "forest-mixed-composition", 자연경관SetNames.혼효림군집,
                "kr5186:l2:700:1145:mixed-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                11.5f, 27f, 8f, .32f, 30f);
            AddNatureComposition(values, compositionRegistry, nw,
                "forest-mountain-ridge-composition", 자연경관SetNames.산능선,
                "kr5186:l1:175:286:mountain-ridge-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                18f, 29f, -11f, .10f, 120f);
            AddNatureComposition(values, compositionRegistry, nw,
                "forest-edge-west-composition", 자연경관SetNames.숲가장자리,
                "kr5186:l2:700:1145:forest-edge-west", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                8.7f, 11.2f, 82f, .30f, 18f);
            AddNatureComposition(values, compositionRegistry, nw,
                "forest-edge-east-composition", 자연경관SetNames.숲가장자리,
                "kr5186:l2:700:1145:forest-edge-east", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                26.4f, 11.4f, 96f, .30f, 18f);

            var ne = 정적경관배치ContainerStableIds.FarmNorthEast;
            var roadPoints = new[]
            {
                new Vector3(20.5f, 0f, 12f), new Vector3(22.5f, 0f, 13.6f),
                new Vector3(24.6f, 0f, 15.1f), new Vector3(27f, 0f, 16.2f),
                new Vector3(29.4f, 0f, 17.8f), new Vector3(31f, 0f, 19.4f),
            };
            AddRoadSegments(values, ne, farm, 법정동WorldRoleCodes.Farm,
                "scenic:sim:pyeongchang:completion-area-farm-departure-road-", roadPoints, .43f);
            Add(ne, "departure-stand", 법정동경관VisualKeys.ProduceStand,
                법정동LandCoverCodes.Cropland, 24f, 18.1f, 138f, .42f, 1, 1);
            Add(ne, "departure-windmill", 법정동경관VisualKeys.Windmill,
                법정동LandCoverCodes.Cropland, 28.7f, 20.2f, -12f, .48f, 1, 1);
            AddNatureComposition(values, compositionRegistry, ne,
                "departure-mixed-buffer", 자연경관SetNames.혼효림군집,
                "kr5186:l2:701:1145:mixed-buffer-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                17f, 25f, 18f, .28f, 30f);
            AddNatureComposition(values, compositionRegistry, ne,
                "departure-conifer-buffer", 자연경관SetNames.침엽수림군집,
                "kr5186:l2:701:1145:conifer-buffer-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                23.5f, 26f, -22f, .28f, 34f);
            AddNatureComposition(values, compositionRegistry, ne,
                "departure-ridge-continuation", 자연경관SetNames.산능선,
                "kr5186:l1:175:286:ridge-continuation", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                30.5f, 27f, 9f, .14f, 120f);
            AddNatureComposition(values, compositionRegistry, ne,
                "departure-forest-edge", 자연경관SetNames.숲가장자리,
                "kr5186:l2:701:1145:forest-edge-01", farm,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                34f, 22.5f, 70f, .28f, 18f);
            Add(ne, "departure-tree-west", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Forest, 18f, 21.4f, 17f, .52f, 2, 2);
            Add(ne, "departure-tree-center", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Forest, 26f, 21.5f, 83f, .54f, 2, 2);
            Add(ne, "departure-tree-east", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Forest, 36f, 25.5f, 141f, .50f, 2, 2);
            for (var index = 0; index < 5; index++)
                Add(ne, "departure-fence-" + index, 법정동경관VisualKeys.Fence,
                    법정동LandCoverCodes.Corridor, 23.8f + index * 1.9f,
                    10f + index * 1.25f, 56f, .48f, 2, 2);
        }

        private static void AddRepresentativeHubAndTownPlacements(
            ICollection<정적경관배치ItemData> values,
            FourPackCompositionRegistry compositionRegistry)
        {
            var hub = 평창군법정동WorldFixture.HubRegionStableId;
            var hubContainer = 정적경관배치ContainerStableIds.JinbuHub;
            AddPackComposition(values, compositionRegistry, hubContainer,
                "representative-hub-station-entry",
                월드CompositionPackCodes.City, 도시물류경관SetNames.물류Station진입부, "A",
                hub, 법정동WorldRoleCodes.Hub, 법정동LandCoverCodes.Logistics,
                -15f, 4f, 0f, 1f, 24f);
            AddPackComposition(values, compositionRegistry, hubContainer,
                "representative-hub-cargo-yard",
                월드CompositionPackCodes.City, 도시물류경관SetNames.화물대기야드, "B",
                hub, 법정동WorldRoleCodes.Hub, 법정동LandCoverCodes.Logistics,
                14f, 4f, 0f, 1f, 20f);
            AddPackComposition(values, compositionRegistry, hubContainer,
                "representative-hub-safety-service",
                월드CompositionPackCodes.City, 도시물류경관SetNames.안전서비스설비, "C",
                hub, 법정동WorldRoleCodes.Hub, 법정동LandCoverCodes.Logistics,
                9f, -15f, 0f, 1f, 16f);
            AddNatureComposition(values, compositionRegistry, hubContainer,
                "hub-buffer-east", 자연경관SetNames.침엽수림군집,
                "jinbu:hub:buffer-east", hub, 법정동WorldRoleCodes.Hub,
                법정동LandCoverCodes.Forest, 25f, 18f, -17f, .30f, 38f);
            AddHubTownTree(values, hubContainer, hub, 법정동WorldRoleCodes.Hub,
                "hub-tree-south-west", -26f, -18f, 31f, .52f);
            AddHubTownTree(values, hubContainer, hub, 법정동WorldRoleCodes.Hub,
                "hub-tree-south-center", -20f, -21f, 89f, .48f);
            AddHubTownTree(values, hubContainer, hub, 법정동WorldRoleCodes.Hub,
                "hub-tree-south-east", 22f, -20f, 137f, .50f);
            AddHubTownTree(values, hubContainer, hub, 법정동WorldRoleCodes.Hub,
                "hub-tree-east", 27f, -14f, 173f, .54f);

            var town = 평창군법정동WorldFixture.TownRegionStableId;
            var townContainer = 정적경관배치ContainerStableIds.PyeongchangTown;
            AddPackComposition(values, compositionRegistry, townContainer,
                "representative-town-housing",
                월드CompositionPackCodes.Town, 타운경관SetNames.저층주택블록, "A",
                town, 법정동WorldRoleCodes.Town, 법정동LandCoverCodes.Residential,
                -19f, 10f, 0f, 1f, 18f);
            AddPackComposition(values, compositionRegistry, townContainer,
                "representative-town-shop",
                월드CompositionPackCodes.Town, 타운경관SetNames.읍내상점전면, "B",
                town, 법정동WorldRoleCodes.Town, 법정동LandCoverCodes.Residential,
                11f, 10f, 0f, 1f, 18f);
            AddPackComposition(values, compositionRegistry, townContainer,
                "representative-town-bus-stop",
                월드CompositionPackCodes.Town, 타운경관SetNames.버스정류장보행쉼터, "C",
                town, 법정동WorldRoleCodes.Town, 법정동LandCoverCodes.Residential,
                -16f, -12f, 0f, 1f, 16f);
            AddPackComposition(values, compositionRegistry, townContainer,
                "representative-town-delivery-parking",
                월드CompositionPackCodes.Town, 타운경관SetNames.소형배달주차공간, "A",
                town, 법정동WorldRoleCodes.Town, 법정동LandCoverCodes.Residential,
                14f, -11f, 0f, 1f, 16f);
            AddNatureComposition(values, compositionRegistry, townContainer,
                "town-green-west", 자연경관SetNames.활엽수림군집,
                "pyeongchang-town:green-west", town, 법정동WorldRoleCodes.Town,
                법정동LandCoverCodes.Forest, -26f, 19f, -8f, .24f, 30f);
            AddNatureComposition(values, compositionRegistry, townContainer,
                "town-green-east", 자연경관SetNames.혼효림군집,
                "pyeongchang-town:green-east", town, 법정동WorldRoleCodes.Town,
                법정동LandCoverCodes.Forest, 26f, 19f, 16f, .24f, 30f);
            AddNatureComposition(values, compositionRegistry, townContainer,
                "town-green-north-edge", 자연경관SetNames.숲가장자리,
                "pyeongchang-town:green-north-edge", town,
                법정동WorldRoleCodes.Town, 법정동LandCoverCodes.Forest,
                0f, 20f, 91f, .25f, 18f);
            AddHubTownTree(values, townContainer, town, 법정동WorldRoleCodes.Town,
                "town-tree-west-a", -29f, -18f, 23f, .46f);
            AddHubTownTree(values, townContainer, town, 법정동WorldRoleCodes.Town,
                "town-tree-west-b", -28f, 0f, 71f, .48f);
            AddHubTownTree(values, townContainer, town, 법정동WorldRoleCodes.Town,
                "town-tree-north-west", -15f, 21f, 109f, .44f);
            AddHubTownTree(values, townContainer, town, 법정동WorldRoleCodes.Town,
                "town-tree-north-east", 15f, 21f, 139f, .44f);
            AddHubTownTree(values, townContainer, town, 법정동WorldRoleCodes.Town,
                "town-tree-east-a", 28f, 2f, 173f, .48f);
            AddHubTownTree(values, townContainer, town, 법정동WorldRoleCodes.Town,
                "town-tree-east-b", 29f, -18f, 211f, .46f);
        }

        private static void AddCorridorPlacements(
            ICollection<정적경관배치ItemData> values,
            FourPackCompositionRegistry compositionRegistry)
        {
            AddRoadSegments(values, 정적경관배치ContainerStableIds.FarmHubCorridor,
                평창군법정동WorldFixture.FarmRegionStableId, 법정동WorldRoleCodes.Farm,
                "scenic:sim:pyeongchang:corridor-road-", new[]
                {
                    new Vector3(22f, 0f, 12f), new Vector3(19.5f, 0f, 11f),
                    new Vector3(17f, 0f, 10.4f), new Vector3(14.5f, 0f, 9.2f),
                    new Vector3(12f, 0f, 7.8f), new Vector3(10f, 0f, 6.5f),
                }, .42f);
            for (var index = 0; index < 4; index++)
                values.Add(Visual(
                    "scenic:sim:pyeongchang:corridor-fence-" + index,
                    정적경관배치ContainerStableIds.FarmHubCorridor,
                    평창군법정동WorldFixture.FarmRegionStableId,
                    법정동경관VisualKeys.Fence, 법정동LandCoverCodes.Corridor,
                    법정동WorldRoleCodes.Farm, 20f - index * 2.3f,
                    12.5f - index * 1.1f, 62f, .62f, 2, 2));
            AddNatureComposition(values, compositionRegistry,
                정적경관배치ContainerStableIds.FarmHubCorridor,
                "corridor-mixed-transition", 자연경관SetNames.혼효림군집,
                "farm-hub-corridor:mixed-transition",
                평창군법정동WorldFixture.FarmRegionStableId,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                10.5f, 18.2f, 18f, .28f, 30f);
            AddNatureComposition(values, compositionRegistry,
                정적경관배치ContainerStableIds.FarmHubCorridor,
                "corridor-conifer-transition", 자연경관SetNames.침엽수림군집,
                "farm-hub-corridor:conifer-transition",
                평창군법정동WorldFixture.FarmRegionStableId,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Forest,
                16.5f, 18.8f, -21f, .26f, 34f);
            AddNatureComposition(values, compositionRegistry,
                정적경관배치ContainerStableIds.FarmHubCorridor,
                "corridor-edge-middle", 자연경관SetNames.숲가장자리,
                "farm-hub-corridor:edge-middle",
                평창군법정동WorldFixture.FarmRegionStableId,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                23f, 18f, 72f, .26f, 18f);
            AddNatureComposition(values, compositionRegistry,
                정적경관배치ContainerStableIds.FarmHubCorridor,
                "corridor-edge-hub-approach", 자연경관SetNames.숲가장자리,
                "farm-hub-corridor:edge-hub-approach",
                평창군법정동WorldFixture.FarmRegionStableId,
                법정동WorldRoleCodes.Farm, 법정동LandCoverCodes.Cropland,
                28f, 15f, 68f, .24f, 18f);
            AddRoadSegments(values, 정적경관배치ContainerStableIds.HubTownCorridor,
                평창군법정동WorldFixture.HubRegionStableId, 법정동WorldRoleCodes.Hub,
                "scenic:sim:pyeongchang:hub-town-road-", new[]
                {
                    new Vector3(8f, 0f, 5f), new Vector3(3f, 0f, .5f),
                    new Vector3(-2f, 0f, -4f), new Vector3(-7f, 0f, -8f),
                    new Vector3(-12f, 0f, -13f), new Vector3(-15f, 0f, -15f),
                }, .45f);
            AddNatureComposition(values, compositionRegistry,
                정적경관배치ContainerStableIds.HubTownCorridor,
                "hub-town-edge-west", 자연경관SetNames.숲가장자리,
                "hub-town-corridor:edge-west",
                평창군법정동WorldFixture.HubRegionStableId,
                법정동WorldRoleCodes.Hub, 법정동LandCoverCodes.Forest,
                -20f, 13f, 48f, .25f, 18f);
            AddNatureComposition(values, compositionRegistry,
                정적경관배치ContainerStableIds.HubTownCorridor,
                "hub-town-edge-east", 자연경관SetNames.숲가장자리,
                "hub-town-corridor:edge-east",
                평창군법정동WorldFixture.HubRegionStableId,
                법정동WorldRoleCodes.Hub, 법정동LandCoverCodes.Forest,
                12f, 13f, 51f, .25f, 18f);
            AddHubTownTree(values, 정적경관배치ContainerStableIds.HubTownCorridor,
                평창군법정동WorldFixture.HubRegionStableId,
                법정동WorldRoleCodes.Hub, "hub-town-tree-west", -25f, 1f, 17f, .45f);
            AddHubTownTree(values, 정적경관배치ContainerStableIds.HubTownCorridor,
                평창군법정동WorldFixture.HubRegionStableId,
                법정동WorldRoleCodes.Hub, "hub-town-tree-center-west", -10f, 15f, 71f, .46f);
            AddHubTownTree(values, 정적경관배치ContainerStableIds.HubTownCorridor,
                평창군법정동WorldFixture.HubRegionStableId,
                법정동WorldRoleCodes.Hub, "hub-town-tree-center-east", 5f, 15f, 131f, .46f);
            AddHubTownTree(values, 정적경관배치ContainerStableIds.HubTownCorridor,
                평창군법정동WorldFixture.HubRegionStableId,
                법정동WorldRoleCodes.Hub, "hub-town-tree-east", 17f, 5f, 197f, .45f);
        }

        private static void AddRoadSegments(
            ICollection<정적경관배치ItemData> values,
            string container,
            string region,
            string role,
            string idPrefix,
            IReadOnlyList<Vector3> points,
            float scale)
        {
            for (var index = 0; index < points.Count - 1; index++)
            {
                var from = points[index];
                var to = points[index + 1];
                var middle = Vector3.Lerp(from, to, .5f);
                values.Add(Visual(idPrefix + index, container, region,
                    법정동경관VisualKeys.RuralRoad, 법정동LandCoverCodes.Corridor,
                    role, middle.x, middle.z,
                    Quaternion.LookRotation(to - from).eulerAngles.y,
                    scale, 1, 1));
            }
        }

        private static void AddNatureComposition(
            ICollection<정적경관배치ItemData> values,
            FourPackCompositionRegistry compositionRegistry,
            string container,
            string suffix,
            string setName,
            string worldSlotStableKey,
            string region,
            string role,
            string landCover,
            float x,
            float z,
            float rotation,
            float scale,
            float viewDistance)
        {
            var variant = new 자연경관CompositionSelector()
                .ResolveVariant(setName, worldSlotStableKey, 51760);
            var compositionKey = 월드CompositionDescriptor.BuildKey(
                월드CompositionPackCodes.Nature, setName, variant);
            compositionRegistry.Resolve(compositionKey);
            values.Add(new 정적경관배치ItemData
            {
                PlacementStableId = "scenic:sim:pyeongchang:nature-" + suffix,
                TargetContainerStableId = container,
                TargetNodeStableId = region,
                AssetReferenceKindCode = 정적경관배치AssetReferenceKindCodes.CompositionKey,
                AssetKey = compositionKey,
                LandCoverCode = landCover,
                RegionRoleCode = role,
                EvidenceKindCode = 법정동WorldEvidenceCodes.SimulationScenario,
                Position = new 정적경관배치PositionData { X = x, Z = z },
                RotationY = rotation,
                UniformScale = scale,
                DensityTier = 0,
                LodGroup = 1,
                SeasonCode = 자연경관SeasonCodes.Spring,
                MoodCode = 자연경관MoodCodes.Peaceful,
                ViewDistance = viewDistance,
            });
        }

        private static void AddHubTownTree(
            ICollection<정적경관배치ItemData> values,
            string container,
            string region,
            string role,
            string suffix,
            float x,
            float z,
            float rotation,
            float scale) => values.Add(Visual(
            "scenic:sim:pyeongchang:nature-" + suffix,
            container, region, 법정동경관VisualKeys.Tree,
            법정동LandCoverCodes.Forest, role,
            x, z, rotation, scale, 2, 2));

        private static void AddPackComposition(
            ICollection<정적경관배치ItemData> values,
            FourPackCompositionRegistry compositionRegistry,
            string container,
            string suffix,
            string packCode,
            string setName,
            string variantCode,
            string region,
            string role,
            string landCover,
            float x,
            float z,
            float rotation,
            float scale,
            float viewDistance)
        {
            var compositionKey = 월드CompositionDescriptor.BuildKey(
                packCode, setName, variantCode);
            compositionRegistry.Resolve(compositionKey);
            values.Add(new 정적경관배치ItemData
            {
                PlacementStableId = "scenic:sim:pyeongchang:" + suffix,
                TargetContainerStableId = container,
                TargetNodeStableId = region,
                AssetReferenceKindCode = 정적경관배치AssetReferenceKindCodes.CompositionKey,
                AssetKey = compositionKey,
                LandCoverCode = landCover,
                RegionRoleCode = role,
                EvidenceKindCode = 법정동WorldEvidenceCodes.SimulationScenario,
                Position = new 정적경관배치PositionData { X = x, Z = z },
                RotationY = rotation,
                UniformScale = scale,
                DensityTier = 1,
                LodGroup = 1,
                SeasonCode = 자연경관SeasonCodes.Spring,
                MoodCode = 자연경관MoodCodes.Peaceful,
                ViewDistance = viewDistance,
            });
        }

        private static 정적경관배치ItemData Visual(
            string id,
            string container,
            string region,
            string visualKey,
            string landCover,
            string role,
            float x,
            float z,
            float rotation,
            float scale,
            int density,
            int lod) => new()
        {
            PlacementStableId = id,
            TargetContainerStableId = container,
            TargetNodeStableId = region,
            AssetReferenceKindCode = 정적경관배치AssetReferenceKindCodes.VisualKey,
            AssetKey = visualKey,
            LandCoverCode = landCover,
            RegionRoleCode = role,
            EvidenceKindCode = 법정동WorldEvidenceCodes.SimulationScenario,
            Position = new 정적경관배치PositionData { X = x, Z = z },
            RotationY = rotation,
            UniformScale = scale,
            DensityTier = density,
            LodGroup = lod,
        };

        private static 정적경관PerformanceBudgetData BuildInitialBudget(
            정적경관배치PlanData plan,
            법정동경관VisualCatalog visualCatalog,
            FourPackCompositionRegistry compositionRegistry)
        {
            var totals = new 정적경관배치PerformanceTotalData();
            var issues = new List<정적경관배치ValidationIssueData>();
            foreach (var placement in plan.Placements.Where(item => item.Enabled))
            {
                var asset = ResolveAsset(placement, visualCatalog, compositionRegistry, issues);
                if (asset != null) AddTotals(totals, asset);
            }
            if (issues.Any(item => item.SeverityCode == "Error"))
                throw new InvalidOperationException(
                    "StaticSceneryInitialBudgetAssetInvalid:"
                    + string.Join(";", issues
                        .Where(item => item.SeverityCode == "Error")
                        .Select(item => item.IssueCode + ":"
                            + item.PlacementStableId + ":" + item.Detail)));
            // 이전 v3 계획의 상한을 높여 경고를 숨기지 않습니다. Nature 군집 치환과
            // 그림자 정책으로 실제 비용을 줄이고, 특히 Shadow Caster는 더 엄격히 제한합니다.
            return new 정적경관PerformanceBudgetData
            {
                TriangleLimit = 222_412,
                MaterialSlotLimit = 408,
                DrawCallLimit = 408,
                ShadowCasterLimit = 260,
                ColliderLimit = 213,
                AnimatorLimit = 7,
            };
        }

        private static ResolvedAsset? ResolveAsset(
            정적경관배치ItemData placement,
            법정동경관VisualCatalog visualCatalog,
            FourPackCompositionRegistry compositionRegistry,
            ICollection<정적경관배치ValidationIssueData> issues)
        {
            if (placement.AssetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.VisualKey)
            {
                법정동경관VisualCatalogEntry entry;
                try { entry = visualCatalog.Resolve(placement.AssetKey); }
                catch (InvalidOperationException)
                {
                    Error(issues, "VisualKeyMissing", placement.PlacementStableId, placement.AssetKey);
                    return null;
                }
                if (!entry.AllowedLandCoverCodes.Contains(placement.LandCoverCode))
                    Error(issues, "LandCoverRejected", placement.PlacementStableId, placement.LandCoverCode);
                if (!entry.AllowedRegionRoleCodes.Contains(placement.RegionRoleCode))
                    Error(issues, "RegionRoleRejected", placement.PlacementStableId, placement.RegionRoleCode);
                var mappedVisual = MapToAnchor(placement);
                var slope = ScenarioSlopeDegrees(mappedVisual.x, mappedVisual.y);
                if (slope < entry.SlopeRange.x || slope > entry.SlopeRange.y)
                    Error(issues, "SlopeRejected", placement.PlacementStableId, slope.ToString("0.###"));
                if (!entry.RotationAllowed && Math.Abs(placement.RotationY) > .001f)
                    Error(issues, "RotationRejected", placement.PlacementStableId, placement.RotationY.ToString("0.###"));
                return entry.Prefab == null ? null : Measure(entry.Prefab, entry.Footprint, entry.ClusterAllowed);
            }

            FourPackCompositionResolvedEntry composition;
            try
            {
                composition = compositionRegistry.Resolve(placement.AssetKey);
            }
            catch (InvalidOperationException)
            {
                Error(issues, "CompositionKeyMissing", placement.PlacementStableId,
                    placement.AssetKey);
                return null;
            }
            var mapped = MapToAnchor(placement);
            if (!composition.CanPlace(
                    placement.LandCoverCode, placement.RegionRoleCode,
                    ScenarioSlopeDegrees(mapped.x, mapped.y),
                    placement.HasWaterMask, placement.SeasonCode,
                    placement.MoodCode, placement.ViewDistance))
                Error(issues, "CompositionRuleRejected", placement.PlacementStableId, placement.AssetKey);
            return Measure(
                composition.Prefab, composition.Footprint, composition.ClusterAllowed);
        }

        private static ResolvedAsset Measure(GameObject prefab, Vector2 footprint, bool clusterAllowed)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var localBounds = MeasureLocalRendererBounds(prefab, renderers, footprint);
            return new ResolvedAsset(
                prefab,
                footprint,
                clusterAllowed,
                new Vector2(localBounds.center.x, localBounds.center.z),
                new Vector2(localBounds.size.x, localBounds.size.z),
                prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Where(item => item.sharedMesh != null)
                    .Sum(item => (long)item.sharedMesh.triangles.Length / 3L),
                renderers.Sum(item => item.sharedMaterials.Length),
                renderers.Sum(item => item.sharedMaterials.Length),
                renderers.Count(item => item.shadowCastingMode != ShadowCastingMode.Off),
                prefab.GetComponentsInChildren<Collider>(true).Length,
                prefab.GetComponentsInChildren<Animator>(true).Length);
        }

        private static Bounds MeasureLocalRendererBounds(
            GameObject prefab,
            IReadOnlyList<Renderer> renderers,
            Vector2 fallbackFootprint)
        {
            if (renderers.Count == 0)
                return new Bounds(Vector3.zero,
                    new Vector3(fallbackFootprint.x, 0f, fallbackFootprint.y));

            var hasBounds = false;
            var result = new Bounds();
            var rootWorldToLocal = prefab.transform.worldToLocalMatrix;
            foreach (var renderer in renderers)
            {
                var bounds = renderer.localBounds;
                var matrix = rootWorldToLocal * renderer.transform.localToWorldMatrix;
                var minimum = bounds.min;
                var maximum = bounds.max;
                for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                for (var z = 0; z < 2; z++)
                {
                    var point = matrix.MultiplyPoint3x4(new Vector3(
                        x == 0 ? minimum.x : maximum.x,
                        y == 0 ? minimum.y : maximum.y,
                        z == 0 ? minimum.z : maximum.z));
                    if (!hasBounds)
                    {
                        result = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        private static void AddTotals(정적경관배치PerformanceTotalData total, ResolvedAsset asset)
        {
            total.Triangles += asset.Triangles;
            total.MaterialSlots += asset.MaterialSlots;
            total.DrawCalls += asset.DrawCalls;
            total.ShadowCasters += asset.ShadowCasters;
            total.Colliders += asset.Colliders;
            total.Animators += asset.Animators;
        }

        private static void CheckBudget(
            정적경관PerformanceBudgetData budget,
            정적경관배치PerformanceTotalData total,
            ICollection<정적경관배치ValidationIssueData> issues)
        {
            Check("Triangles", total.Triangles, budget.TriangleLimit);
            Check("MaterialSlots", total.MaterialSlots, budget.MaterialSlotLimit);
            Check("DrawCalls", total.DrawCalls, budget.DrawCallLimit);
            Check("ShadowCasters", total.ShadowCasters, budget.ShadowCasterLimit);
            Check("Colliders", total.Colliders, budget.ColliderLimit);
            Check("Animators", total.Animators, budget.AnimatorLimit);
            void Check(string code, long value, long limit)
            {
                if (value > limit) Error(issues, "PerformanceBudgetExceeded:" + code,
                    string.Empty, $"{value}/{limit}");
                else if (value >= Math.Ceiling(limit * .8d))
                    Warning(issues, "PerformanceBudgetWarning:" + code,
                        string.Empty, $"{value}/{limit}");
            }
        }

        private static void AddOverlapWarnings(
            정적경관배치PlanData plan,
            IReadOnlyDictionary<string, ResolvedAsset> resolved,
            ICollection<정적경관배치ValidationIssueData> issues)
        {
            foreach (var group in plan.Placements.Where(item => item.Enabled)
                         .GroupBy(item => item.TargetContainerStableId, StringComparer.Ordinal))
            {
                var items = group.Where(item => resolved.ContainsKey(item.PlacementStableId)).ToArray();
                for (var leftIndex = 0; leftIndex < items.Length; leftIndex++)
                for (var rightIndex = leftIndex + 1; rightIndex < items.Length; rightIndex++)
                {
                    var left = items[leftIndex];
                    var right = items[rightIndex];
                    var leftAsset = resolved[left.PlacementStableId];
                    var rightAsset = resolved[right.PlacementStableId];
                    var minimumDistance = Math.Min(
                        Math.Min(leftAsset.Footprint.x, leftAsset.Footprint.y) * left.UniformScale,
                        Math.Min(rightAsset.Footprint.x, rightAsset.Footprint.y) * right.UniformScale) * .2f;
                    var dx = left.Position.X - right.Position.X;
                    var dz = left.Position.Z - right.Position.Z;
                    if (dx * dx + dz * dz < minimumDistance * minimumDistance)
                        Warning(issues, "PotentialFootprintOverlap", left.PlacementStableId,
                            right.PlacementStableId);
                }
            }
        }

        private static void ValidateCropsInsideSoilRows(
            정적경관배치PlanData plan,
            IReadOnlyDictionary<string, ResolvedAsset> resolved,
            ICollection<정적경관배치ValidationIssueData> issues)
        {
            foreach (var group in plan.Placements.Where(item => item.Enabled)
                         .GroupBy(item => item.TargetContainerStableId, StringComparer.Ordinal))
            {
                var soilRows = group.Where(item =>
                        item.AssetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.VisualKey
                        && item.AssetKey == 법정동경관VisualKeys.SoilRows
                        && resolved.ContainsKey(item.PlacementStableId))
                    .ToArray();
                var crops = group.Where(item =>
                        item.AssetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.VisualKey
                        && item.AssetKey == 법정동경관VisualKeys.Potato
                        && resolved.ContainsKey(item.PlacementStableId))
                    .ToArray();
                if (soilRows.Length == 0 || crops.Length == 0) continue;

                foreach (var crop in crops)
                {
                    var cropBounds = VisualFootprintBounds(
                        crop, resolved[crop.PlacementStableId]);
                    var containingRow = soilRows
                        .Where(item => Contains(VisualFootprintBounds(
                            item, resolved[item.PlacementStableId]), cropBounds, .03f))
                        .OrderBy(item =>
                        {
                            var dx = item.Position.X - crop.Position.X;
                            var dz = item.Position.Z - crop.Position.Z;
                            return dx * dx + dz * dz;
                        })
                        .FirstOrDefault();
                    if (containingRow == null)
                    {
                        Error(issues, "CropOutsideSoilRowBounds", crop.PlacementStableId,
                            $"crop={cropBounds.xMin:0.###},{cropBounds.yMin:0.###},"
                            + $"{cropBounds.xMax:0.###},{cropBounds.yMax:0.###}");
                        continue;
                    }
                    if (Mathf.Abs(Mathf.DeltaAngle(
                            crop.RotationY, containingRow.RotationY)) > 5f)
                        Error(issues, "CropRowRotationMismatch", crop.PlacementStableId,
                            $"crop={crop.RotationY:0.###};row={containingRow.RotationY:0.###}");
                }
            }
        }

        private static Rect VisualFootprintBounds(
            정적경관배치ItemData placement,
            ResolvedAsset asset)
        {
            var radians = placement.RotationY * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            var centerX = asset.LocalBoundsCenter.x * placement.UniformScale;
            var centerZ = asset.LocalBoundsCenter.y * placement.UniformScale;
            var rotatedCenterX = centerX * cosine + centerZ * sine;
            var rotatedCenterZ = -centerX * sine + centerZ * cosine;
            var absoluteCosine = Mathf.Abs(cosine);
            var absoluteSine = Mathf.Abs(sine);
            var halfX = (absoluteCosine * asset.LocalBoundsSize.x
                + absoluteSine * asset.LocalBoundsSize.y) * placement.UniformScale * .5f;
            var halfZ = (absoluteSine * asset.LocalBoundsSize.x
                + absoluteCosine * asset.LocalBoundsSize.y) * placement.UniformScale * .5f;
            var x = placement.Position.X + rotatedCenterX;
            var z = placement.Position.Z + rotatedCenterZ;
            return Rect.MinMaxRect(x - halfX, z - halfZ, x + halfX, z + halfZ);
        }

        private static bool Contains(Rect outer, Rect inner, float tolerance) =>
            inner.xMin >= outer.xMin - tolerance
            && inner.yMin >= outer.yMin - tolerance
            && inner.xMax <= outer.xMax + tolerance
            && inner.yMax <= outer.yMax + tolerance;

        private static string[] BuildStagingPrefabs(
            정적경관배치PlanData plan,
            IReadOnlyDictionary<string, ResolvedAsset> assets,
            string baseHash,
            string overrideHash,
            string mergedHash)
        {
            Directory.CreateDirectory(StagingDirectory);
            var groups = plan.Placements
                .Where(item => item.Enabled)
                .GroupBy(item => item.TargetContainerStableId, StringComparer.Ordinal)
                .ToArray();
            var expectedPaths = groups
                .Select(group => StagingPrefabPath(group.Key))
                .ToHashSet(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { StagingDirectory }))
            {
                var existingPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(existingPath)?.Replace('\\', '/')
                        == StagingDirectory
                    && !expectedPaths.Contains(existingPath))
                    AssetDatabase.DeleteAsset(existingPath);
            }
            var paths = new List<string>();
            foreach (var group in groups)
            {
                var root = new GameObject("StaticSceneryStaging_" + Containers[group.Key].FileCode);
                try
                {
                    foreach (var placement in group.OrderBy(
                                 item => item.PlacementStableId, StringComparer.Ordinal))
                    {
                        var wrapper = new GameObject(SafeName(placement.PlacementStableId));
                        wrapper.transform.SetParent(root.transform, false);
                        var mapped = MapToAnchor(placement);
                        wrapper.transform.localPosition = new Vector3(
                            mapped.x, ResolveHeight(placement), mapped.y);
                        wrapper.transform.localRotation = Quaternion.Euler(
                            0f,
                            placement.RotationY
                            + Containers[placement.TargetContainerStableId].LocalRotationY,
                            0f);
                        wrapper.transform.localScale = Vector3.one * placement.UniformScale;
                        var instance = PrefabUtility.InstantiatePrefab(
                                assets[placement.PlacementStableId].Prefab, wrapper.transform) as GameObject
                            ?? throw new InvalidOperationException(
                                "StaticSceneryStagingInstantiateFailed:" + placement.PlacementStableId);
                        instance.name = "VisualRoot";
                        var view = wrapper.AddComponent<정적경관배치InstanceView>();
                        view.Configure(placement, mergedHash);
                        if (!view.ValidateWiring())
                            throw new InvalidOperationException(
                                "StaticSceneryStagingWiringInvalid:" + placement.PlacementStableId);
                    }
                    var receipt = root.AddComponent<정적경관배치ReceiptView>();
                    receipt.Configure(plan.PlanStableId, baseHash, overrideHash, mergedHash,
                        plan.VisualCatalogRevision,
                        plan.RenderingProfileStableId,
                        plan.RenderingProfileRevision,
                        plan.RenderingProfileHashSha256,
                        group.Count());
                    var path = StagingPrefabPath(group.Key);
                    var saved = PrefabUtility.SaveAsPrefabAsset(root, path)
                        ?? throw new InvalidOperationException("StaticSceneryStagingSaveFailed:" + path);
                    var savedReceipt = saved.GetComponent<정적경관배치ReceiptView>();
                    if (savedReceipt == null || !savedReceipt.ValidateWiring())
                        throw new InvalidOperationException("StaticSceneryStagingReceiptInvalid:" + path);
                    paths.Add(path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            AssetDatabase.SaveAssets();
            return paths.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static float ResolveHeight(정적경관배치ItemData placement)
        {
            var definition = Containers[placement.TargetContainerStableId];
            var mapped = MapToAnchor(placement);
            return (placement.HeightPolicyCode == 정적경관배치HeightPolicyCodes.Explicit
                    ? placement.Position.ExplicitY!.Value
                    : ScenarioHeight(mapped.x, mapped.y))
                + definition.HeightOffset;
        }

        private static Vector2 MapToAnchor(정적경관배치ItemData placement) =>
            Containers[placement.TargetContainerStableId].MapToAnchor(
                placement.Position.X, placement.Position.Z);

        private static void ValidateContainerTransformContract(
            정적경관배치PlanData plan,
            ICollection<정적경관배치ValidationIssueData> issues)
        {
            var planTransforms = plan.ContainerTransforms.ToDictionary(
                item => item.ContainerStableId, StringComparer.Ordinal);
            foreach (var pair in Containers)
            {
                if (!planTransforms.TryGetValue(pair.Key, out var value))
                {
                    Error(issues, "ContainerTransformMissing", string.Empty, pair.Key);
                    continue;
                }
                if (!pair.Value.Matches(value))
                    Error(issues, "ContainerTransformContractMismatch", string.Empty, pair.Key);
            }
            foreach (var extra in planTransforms.Keys.Except(Containers.Keys, StringComparer.Ordinal))
                Error(issues, "ContainerTransformUnknown", string.Empty, extra);
        }

        private static float ScenarioHeight(float x, float z) =>
            .18f + (x + 30f) * .022f + (z + 22f) * .012f
            + Mathf.Sin(x * .16f) * .28f + Mathf.Cos(z * .18f) * .22f;

        private static float ScenarioSlopeDegrees(float x, float z)
        {
            const float sample = .25f;
            var dx = (ScenarioHeight(x + sample, z) - ScenarioHeight(x - sample, z)) / (sample * 2f);
            var dz = (ScenarioHeight(x, z + sample) - ScenarioHeight(x, z - sample)) / (sample * 2f);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        private static 법정동경관VisualCatalog LoadVisualCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<법정동경관VisualCatalog>(
                    대한민국법정동WorldBuilder.CatalogPath)
                ?? throw new InvalidOperationException("StaticSceneryVisualCatalogMissing");
            catalog.Validate();
            return catalog;
        }

        private static FourPackCompositionRegistry LoadCompositionRegistry()
        {
            var natureCatalog = AssetDatabase.LoadAssetAtPath<자연경관CompositionCatalog>(
                     자연경관CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException("StaticSceneryCompositionCatalogMissing");
            var packCatalog = AssetDatabase.LoadAssetAtPath<팩경관CompositionCatalog>(
                    팩경관CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException(
                    "StaticSceneryPackCompositionCatalogMissing");
            if (packCatalog.CatalogRevision != CompositionCatalogRevision)
                throw new InvalidOperationException(
                    "StaticSceneryPackCompositionCatalogRevisionMismatch");
            return new FourPackCompositionRegistry(natureCatalog, packCatalog);
        }

        private static T ReadJson<T>(string assetPath)
        {
            if (!File.Exists(assetPath))
                throw new InvalidOperationException("StaticSceneryPlanFileMissing:" + assetPath);
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(assetPath), JsonSettings)
                    ?? throw new InvalidOperationException("StaticSceneryPlanJsonEmpty:" + assetPath);
            }
            catch (JsonException error)
            {
                throw new InvalidOperationException("StaticSceneryPlanJsonInvalid:" + assetPath, error);
            }
        }

        private static void WriteJson<T>(string assetPath, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            File.WriteAllText(assetPath, JsonConvert.SerializeObject(value, JsonSettings));
        }

        private static void WriteReport(정적경관배치ValidationReportData report)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(report, JsonSettings));

            var markdownPath = Path.Combine(
                Directory.GetCurrentDirectory(), ReportMarkdownRelativePath);
            File.WriteAllText(markdownPath, BuildReportMarkdown(report));
        }

        private static string BuildReportMarkdown(
            정적경관배치ValidationReportData report)
        {
            var lines = new List<string>
            {
                "# 평창 정적 경관 배치 검증 기록",
                string.Empty,
                $"- 계획: `{report.PlanStableId}`",
                $"- 배치: {report.EnabledPlacementCount}개",
                $"- 오류/경고: {report.ErrorCount}/{report.WarningCount}",
                $"- 검토 상태: `{report.EffectiveReviewStateCode}`",
                $"- Staging 가능: `{report.CanStage}`",
                $"- Scene 적용 가능: `{report.CanApply}`",
                $"- 기획서 hash: `{report.BriefHashSha256}`",
                $"- 병합 계획 hash: `{report.MergedPlanHashSha256}`",
                $"- Rendering Profile: `{report.RenderingProfileStableId}` / `{report.RenderingProfileRevision}`",
                $"- Rendering Profile hash: `{report.RenderingProfileHashSha256}`",
                string.Empty,
                "## 성능 합계 / 예산",
                string.Empty,
                $"- Triangle: {report.PerformanceTotal.Triangles} / {report.PerformanceBudget.TriangleLimit}",
                $"- Material Slot: {report.PerformanceTotal.MaterialSlots} / {report.PerformanceBudget.MaterialSlotLimit}",
                $"- Draw Call: {report.PerformanceTotal.DrawCalls} / {report.PerformanceBudget.DrawCallLimit}",
                $"- Shadow Caster: {report.PerformanceTotal.ShadowCasters} / {report.PerformanceBudget.ShadowCasterLimit}",
                $"- Collider: {report.PerformanceTotal.Colliders} / {report.PerformanceBudget.ColliderLimit}",
                $"- Animator: {report.PerformanceTotal.Animators} / {report.PerformanceBudget.AnimatorLimit}",
                string.Empty,
                "## 검증 항목",
                string.Empty,
                "| 심각도 | 코드 | 배치 | 상세 |",
                "| --- | --- | --- | --- |",
            };
            lines.AddRange(report.Issues.Select(item =>
                $"| {item.SeverityCode} | `{item.IssueCode}` | `{item.PlacementStableId}` | "
                + item.Detail.Replace("|", "\\|", StringComparison.Ordinal) + " |"));
            if (report.Issues.Length == 0) lines.Add("| - | - | - | 검증 문제 없음 |");
            return string.Join("\n", lines) + "\n";
        }

        private static string StagingPrefabPath(string containerStableId) =>
            StagingDirectory + "/" + Containers[containerStableId].FileCode + ".prefab";

        private static string SafeName(string value) =>
            value.Replace(':', '_').Replace('/', '_').Replace('\\', '_');

        private static void Error(
            ICollection<정적경관배치ValidationIssueData> issues,
            string code,
            string placement,
            string detail) => issues.Add(new 정적경관배치ValidationIssueData
        {
            SeverityCode = "Error", IssueCode = code,
            PlacementStableId = placement, Detail = detail,
        });

        private static void Warning(
            ICollection<정적경관배치ValidationIssueData> issues,
            string code,
            string placement,
            string detail) => issues.Add(new 정적경관배치ValidationIssueData
        {
            SeverityCode = "Warning", IssueCode = code,
            PlacementStableId = placement, Detail = detail,
        });

        private sealed class ContainerDefinition
        {
            public ContainerDefinition(
                string fileCode,
                Vector2 minimum,
                Vector2 maximum,
                Vector2 worldAnchor,
                float localToAnchorScale = 1f,
                float localRotationY = 0f,
                float heightOffset = 0f)
            {
                FileCode = fileCode;
                Minimum = minimum;
                Maximum = maximum;
                WorldAnchor = worldAnchor;
                LocalToAnchorScale = localToAnchorScale;
                LocalRotationY = localRotationY;
                HeightOffset = heightOffset;
            }

            public string FileCode { get; }
            public Vector2 Minimum { get; }
            public Vector2 Maximum { get; }
            public Vector2 WorldAnchor { get; }
            public float LocalToAnchorScale { get; }
            public float LocalRotationY { get; }
            public float HeightOffset { get; }
            public bool Contains(float x, float z) =>
                x >= Minimum.x && x <= Maximum.x && z >= Minimum.y && z <= Maximum.y;

            public bool ContainsFootprint(
                float x,
                float z,
                Vector2 footprint,
                float rotationY,
                float uniformScale)
            {
                var radians = rotationY * Mathf.Deg2Rad;
                var cosine = Mathf.Abs(Mathf.Cos(radians));
                var sine = Mathf.Abs(Mathf.Sin(radians));
                var halfX = (cosine * footprint.x + sine * footprint.y)
                    * uniformScale * .5f;
                var halfZ = (sine * footprint.x + cosine * footprint.y)
                    * uniformScale * .5f;
                return x - halfX >= Minimum.x && x + halfX <= Maximum.x
                    && z - halfZ >= Minimum.y && z + halfZ <= Maximum.y;
            }

            public Vector2 MapToAnchor(float x, float z)
            {
                var scaled = new Vector2(x, z) * LocalToAnchorScale;
                var radians = LocalRotationY * Mathf.Deg2Rad;
                var cosine = Mathf.Cos(radians);
                var sine = Mathf.Sin(radians);
                return WorldAnchor + new Vector2(
                    scaled.x * cosine - scaled.y * sine,
                    scaled.x * sine + scaled.y * cosine);
            }

            public 정적경관배치ContainerTransformData ToData(string stableId) => new()
            {
                ContainerStableId = stableId,
                WorldAnchorX = WorldAnchor.x,
                WorldAnchorZ = WorldAnchor.y,
                AuthoringMinimumX = Minimum.x,
                AuthoringMinimumZ = Minimum.y,
                AuthoringMaximumX = Maximum.x,
                AuthoringMaximumZ = Maximum.y,
                LocalToAnchorScale = LocalToAnchorScale,
                LocalRotationY = LocalRotationY,
                HeightOffset = HeightOffset,
            };

            public bool Matches(정적경관배치ContainerTransformData value) =>
                Mathf.Approximately(value.WorldAnchorX, WorldAnchor.x)
                && Mathf.Approximately(value.WorldAnchorZ, WorldAnchor.y)
                && Mathf.Approximately(value.AuthoringMinimumX, Minimum.x)
                && Mathf.Approximately(value.AuthoringMinimumZ, Minimum.y)
                && Mathf.Approximately(value.AuthoringMaximumX, Maximum.x)
                && Mathf.Approximately(value.AuthoringMaximumZ, Maximum.y)
                && Mathf.Approximately(value.LocalToAnchorScale, LocalToAnchorScale)
                && Mathf.Approximately(value.LocalRotationY, LocalRotationY)
                && Mathf.Approximately(value.HeightOffset, HeightOffset);
        }

        private sealed class ResolvedAsset
        {
            public ResolvedAsset(
                GameObject prefab, Vector2 footprint, bool clusterAllowed,
                Vector2 localBoundsCenter, Vector2 localBoundsSize,
                long triangles, int materialSlots, int drawCalls,
                int shadowCasters, int colliders, int animators)
            {
                Prefab = prefab;
                Footprint = footprint;
                ClusterAllowed = clusterAllowed;
                LocalBoundsCenter = localBoundsCenter;
                LocalBoundsSize = localBoundsSize;
                Triangles = triangles;
                MaterialSlots = materialSlots;
                DrawCalls = drawCalls;
                ShadowCasters = shadowCasters;
                Colliders = colliders;
                Animators = animators;
            }

            public GameObject Prefab { get; }
            public Vector2 Footprint { get; }
            public bool ClusterAllowed { get; }
            public Vector2 LocalBoundsCenter { get; }
            public Vector2 LocalBoundsSize { get; }
            public long Triangles { get; }
            public int MaterialSlots { get; }
            public int DrawCalls { get; }
            public int ShadowCasters { get; }
            public int Colliders { get; }
            public int Animators { get; }
        }
    }
}
