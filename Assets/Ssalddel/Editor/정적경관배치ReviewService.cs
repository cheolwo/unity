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

namespace Ssalddel.Unity.Editor
{
    [Serializable]
    public sealed class 정적경관배치BriefMetadataData
    {
        public int BriefSchemaVersion;
        public string BriefStableId = string.Empty;
        public string BriefRevision = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string PlanStableId = string.Empty;
        public string NatureGuideStableId = string.Empty;
        public string NatureGuideRevision = string.Empty;
        public string NatureGuideHashSha256 = string.Empty;
        public string FarmGuideStableId = string.Empty;
        public string FarmGuideRevision = string.Empty;
        public string FarmGuideHashSha256 = string.Empty;
        public string TownGuideStableId = string.Empty;
        public string TownGuideRevision = string.Empty;
        public string TownGuideHashSha256 = string.Empty;
        public string CityGuideStableId = string.Empty;
        public string CityGuideRevision = string.Empty;
        public string CityGuideHashSha256 = string.Empty;
        public string FourPackGuideBundleHashSha256 = string.Empty;
        public string CompositionCatalogRevision = string.Empty;
        public string RenderingProfileStableId = string.Empty;
        public string RenderingProfileRevision = string.Empty;
        public string RenderingProfileHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class 자연경관배치GuideMetadataData
    {
        public int GuideSchemaVersion;
        public string GuideStableId = string.Empty;
        public string GuideRevision = string.Empty;
        public string SourcePackCode = string.Empty;
        public string VisualCatalogRevision = string.Empty;
        public string CompositionCatalogRevision = string.Empty;
        public bool PresentationOnly;
    }

    public sealed class 정적경관배치ReviewStatusData
    {
        public 정적경관배치BriefMetadataData Brief = new();
        public 정적경관배치ReviewReceiptData Receipt = new();
        public string BriefHashSha256 = string.Empty;
        public string EffectiveReviewStateCode = 정적경관배치ReviewStateCodes.Draft;
        public bool ReviewMatchesInputs;
        public string MismatchReason = string.Empty;

        public bool IsApprovedForSceneApply =>
            EffectiveReviewStateCode == 정적경관배치ReviewStateCodes.ApprovedForSceneApply
            && ReviewMatchesInputs;
    }

    public static class 정적경관배치ReviewService
    {
        public const string BriefPath =
            "Assets/Documentation/WorldPlacementPlans/pyeongchang-farm-hub-town-v1/경관배치기획서.md";
        public const string NatureGuidePath =
            "Assets/Documentation/WorldPlacementPlans/공통/PolygonNature숲경관배치기준.md";
        public const string FarmGuidePath =
            "Assets/Documentation/WorldPlacementPlans/공통/PolygonFarm농촌경관배치기준.md";
        public const string TownGuidePath =
            "Assets/Documentation/WorldPlacementPlans/공통/PolygonTown읍내경관배치기준.md";
        public const string CityGuidePath =
            "Assets/Documentation/WorldPlacementPlans/공통/PolygonCity도시물류경관배치기준.md";
        public const string ReviewReceiptPath =
            정적경관배치PlanPipeline.PlanDirectory + "/pyeongchang-static-scenery.review.json";

        private static readonly string[] RequiredHeadings =
        {
            "## 경관 목표",
            "## 영역별 구성",
            "## 네 팩 배치 기준",
            "## PolygonNature 숲 경관 적용",
            "## 조명과 명암",
            "## 필수 요소",
            "## 금지 요소",
            "## 성능과 LOD",
            "## 자료 한계",
            "## 검토 체크리스트",
        };

        private static readonly string[] RequiredPackGuideHeadings =
        {
            "## 팩의 특징",
            "## 배치 순서",
            "## 구성 세트 기준",
            "## LOD와 성능",
            "## 금지 요소",
            "## 검토 체크리스트",
        };

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        public static void EnsureArtifacts(정적경관배치PlanData basePlan)
        {
            if (!File.Exists(BriefPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BriefPath)!);
                File.WriteAllText(BriefPath, BuildDefaultBrief(basePlan));
            }

            var brief = LoadBriefMetadata();
            if (!File.Exists(ReviewReceiptPath))
                WriteReceipt(CreateDraftReceipt(brief));
            else
            {
                var existing = JsonConvert.DeserializeObject<정적경관배치ReviewReceiptData>(
                    File.ReadAllText(ReviewReceiptPath), JsonSettings);
                if (existing != null && existing.SchemaVersion < 2
                    && existing.ReviewStateCode == 정적경관배치ReviewStateCodes.Draft)
                    WriteReceipt(CreateDraftReceipt(brief));
            }
            AssetDatabase.Refresh();
        }

        public static 정적경관배치BriefMetadataData LoadBriefMetadata()
        {
            if (!File.Exists(BriefPath))
                throw new InvalidOperationException("StaticSceneryPlacementBriefMissing:" + BriefPath);
            var normalized = NormalizeBrief(File.ReadAllText(BriefPath));
            foreach (var heading in RequiredHeadings)
                if (!normalized.Contains(heading, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "StaticSceneryPlacementBriefHeadingMissing:" + heading);

            var values = ParseFrontMatter(normalized);
            var metadata = new 정적경관배치BriefMetadataData
            {
                BriefSchemaVersion = ParseInt(values, "briefSchemaVersion"),
                BriefStableId = Require(values, "briefStableId"),
                BriefRevision = Require(values, "briefRevision"),
                AreaSetStableId = Require(values, "areaSetStableId"),
                PlanStableId = Require(values, "planStableId"),
                NatureGuideStableId = Require(values, "natureGuideStableId"),
                NatureGuideRevision = Require(values, "natureGuideRevision"),
                NatureGuideHashSha256 = Require(values, "natureGuideHashSha256"),
                FarmGuideStableId = Require(values, "farmGuideStableId"),
                FarmGuideRevision = Require(values, "farmGuideRevision"),
                FarmGuideHashSha256 = Require(values, "farmGuideHashSha256"),
                TownGuideStableId = Require(values, "townGuideStableId"),
                TownGuideRevision = Require(values, "townGuideRevision"),
                TownGuideHashSha256 = Require(values, "townGuideHashSha256"),
                CityGuideStableId = Require(values, "cityGuideStableId"),
                CityGuideRevision = Require(values, "cityGuideRevision"),
                CityGuideHashSha256 = Require(values, "cityGuideHashSha256"),
                FourPackGuideBundleHashSha256 = Require(
                    values, "fourPackGuideBundleHashSha256"),
                CompositionCatalogRevision = Require(
                    values, "compositionCatalogRevision"),
                RenderingProfileStableId = Require(
                    values, "renderingProfileStableId"),
                RenderingProfileRevision = Require(
                    values, "renderingProfileRevision"),
                RenderingProfileHashSha256 = Require(
                    values, "renderingProfileHashSha256"),
            };
            if (metadata.BriefSchemaVersion != 4)
                throw new InvalidOperationException(
                    "StaticSceneryPlacementBriefSchemaUnsupported:" + metadata.BriefSchemaVersion);
            if (!IsSha256(metadata.NatureGuideHashSha256)
                || !IsSha256(metadata.FarmGuideHashSha256)
                || !IsSha256(metadata.TownGuideHashSha256)
                || !IsSha256(metadata.CityGuideHashSha256)
                || !IsSha256(metadata.FourPackGuideBundleHashSha256)
                || !IsSha256(metadata.RenderingProfileHashSha256))
                throw new InvalidOperationException(
                    "StaticSceneryFourPackGuideHashInvalid");
            if (metadata.CompositionCatalogRevision
                != 정적경관배치PlanPipeline.CompositionCatalogRevision)
                throw new InvalidOperationException(
                    "StaticSceneryCompositionCatalogReferenceMismatch");
            var renderingProfile = 평창군경관RenderingFixture.Create();
            if (metadata.RenderingProfileStableId != renderingProfile.ProfileStableId
                || metadata.RenderingProfileRevision != renderingProfile.RuleRevision
                || !SameHash(metadata.RenderingProfileHashSha256,
                    경관RenderingProfileHash.Compute(renderingProfile)))
                throw new InvalidOperationException(
                    "StaticSceneryRenderingProfileReferenceMismatch");

            ValidateGuideReference(
                metadata.NatureGuideStableId, metadata.NatureGuideRevision,
                metadata.NatureGuideHashSha256,
                LoadNatureGuideMetadata(), ComputeNatureGuideHash(), "Nature");
            ValidateGuideReference(
                metadata.FarmGuideStableId, metadata.FarmGuideRevision,
                metadata.FarmGuideHashSha256,
                LoadFarmGuideMetadata(), ComputeFarmGuideHash(), "Farm");
            ValidateGuideReference(
                metadata.TownGuideStableId, metadata.TownGuideRevision,
                metadata.TownGuideHashSha256,
                LoadTownGuideMetadata(), ComputeTownGuideHash(), "Town");
            ValidateGuideReference(
                metadata.CityGuideStableId, metadata.CityGuideRevision,
                metadata.CityGuideHashSha256,
                LoadCityGuideMetadata(), ComputeCityGuideHash(), "City");
            if (!SameHash(
                    metadata.FourPackGuideBundleHashSha256,
                    ComputeFourPackGuideBundleHash()))
                throw new InvalidOperationException(
                    "StaticSceneryFourPackGuideBundleHashMismatch");
            return metadata;
        }

        public static 자연경관배치GuideMetadataData LoadNatureGuideMetadata()
            => LoadGuideMetadata(NatureGuidePath, 월드CompositionPackCodes.Nature);

        public static 자연경관배치GuideMetadataData LoadFarmGuideMetadata()
            => LoadGuideMetadata(FarmGuidePath, 월드CompositionPackCodes.Farm);

        public static 자연경관배치GuideMetadataData LoadTownGuideMetadata()
            => LoadGuideMetadata(TownGuidePath, 월드CompositionPackCodes.Town);

        public static 자연경관배치GuideMetadataData LoadCityGuideMetadata()
            => LoadGuideMetadata(CityGuidePath, 월드CompositionPackCodes.City);

        private static 자연경관배치GuideMetadataData LoadGuideMetadata(
            string path,
            string expectedPackCode)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "StaticSceneryPackGuideMissing:" + path);
            var normalized = NormalizeBrief(File.ReadAllText(path));
            foreach (var heading in RequiredPackGuideHeadings)
                if (!normalized.Contains(heading, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "StaticSceneryPackGuideHeadingMissing:" + heading);

            var values = ParseFrontMatter(normalized);
            var metadata = new 자연경관배치GuideMetadataData
            {
                GuideSchemaVersion = ParseInt(values, "guideSchemaVersion"),
                GuideStableId = Require(values, "guideStableId"),
                GuideRevision = Require(values, "guideRevision"),
                SourcePackCode = Require(values, "sourcePackCode"),
                VisualCatalogRevision = Require(values, "visualCatalogRevision"),
                CompositionCatalogRevision = Require(values, "compositionCatalogRevision"),
                PresentationOnly = ParseBool(values, "presentationOnly"),
            };
            if (metadata.GuideSchemaVersion != 1
                || metadata.SourcePackCode != expectedPackCode
                || metadata.VisualCatalogRevision != "legal-dong-scenic-catalog.v2"
                || metadata.CompositionCatalogRevision
                    != 정적경관배치PlanPipeline.CompositionCatalogRevision
                || !metadata.PresentationOnly)
                throw new InvalidOperationException(
                    "StaticSceneryPackGuideMetadataInvalid:" + expectedPackCode);
            return metadata;
        }

        public static string ComputeNatureGuideHash() =>
            ComputeGuideHash(NatureGuidePath);

        public static string ComputeFarmGuideHash() => ComputeGuideHash(FarmGuidePath);
        public static string ComputeTownGuideHash() => ComputeGuideHash(TownGuidePath);
        public static string ComputeCityGuideHash() => ComputeGuideHash(CityGuidePath);

        private static string ComputeGuideHash(string path) =>
            정적경관배치PlanHash.Sha256(NormalizeBrief(File.ReadAllText(path)));

        public static string ComputeFourPackGuideBundleHash() =>
            ComputeFourPackGuideBundleHash(
                File.ReadAllText(NatureGuidePath),
                File.ReadAllText(FarmGuidePath),
                File.ReadAllText(TownGuidePath),
                File.ReadAllText(CityGuidePath));

        public static string ComputeFourPackGuideBundleHash(
            string natureGuide,
            string farmGuide,
            string townGuide,
            string cityGuide) =>
            정적경관배치PlanHash.Sha256(
                "--- nature ---\n" + NormalizeBrief(natureGuide)
                + "--- farm ---\n" + NormalizeBrief(farmGuide)
                + "--- town ---\n" + NormalizeBrief(townGuide)
                + "--- city ---\n" + NormalizeBrief(cityGuide));

        public static string ComputeBriefHash()
        {
            LoadBriefMetadata();
            return ComputeBriefBundleHash(
                File.ReadAllText(BriefPath),
                File.ReadAllText(NatureGuidePath),
                File.ReadAllText(FarmGuidePath),
                File.ReadAllText(TownGuidePath),
                File.ReadAllText(CityGuidePath));
        }

        public static string ComputeBriefBundleHash(
            string brief,
            string natureGuide,
            string farmGuide,
            string townGuide,
            string cityGuide) =>
            정적경관배치PlanHash.Sha256(
                NormalizeBrief(brief)
                + "--- referenced-nature-guide ---\n"
                + NormalizeBrief(natureGuide)
                + "--- referenced-farm-guide ---\n"
                + NormalizeBrief(farmGuide)
                + "--- referenced-town-guide ---\n"
                + NormalizeBrief(townGuide)
                + "--- referenced-city-guide ---\n"
                + NormalizeBrief(cityGuide));

        public static string ComputeBriefBundleHash(string brief, string natureGuide) =>
            ComputeBriefBundleHash(
                brief, natureGuide,
                File.ReadAllText(FarmGuidePath),
                File.ReadAllText(TownGuidePath),
                File.ReadAllText(CityGuidePath));

        public static string NormalizeBrief(string value) =>
            value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .TrimStart('\uFEFF')
                .TrimEnd() + "\n";

        public static 정적경관배치ReviewReceiptData LoadReceiptOrDraft(
            정적경관배치BriefMetadataData brief)
        {
            if (!File.Exists(ReviewReceiptPath)) return CreateDraftReceipt(brief);
            try
            {
                var receipt = JsonConvert.DeserializeObject<정적경관배치ReviewReceiptData>(
                        File.ReadAllText(ReviewReceiptPath), JsonSettings)
                    ?? throw new InvalidOperationException("StaticSceneryReviewReceiptEmpty");
                ValidateReceipt(receipt);
                return receipt;
            }
            catch (JsonException error)
            {
                throw new InvalidOperationException("StaticSceneryReviewReceiptInvalid", error);
            }
        }

        public static 정적경관배치ReviewStatusData EvaluateCurrent(
            정적경관배치PlanData basePlan,
            정적경관배치OverridePlanData overridePlan,
            정적경관배치PlanData mergedPlan)
        {
            var brief = LoadBriefMetadata();
            if (brief.AreaSetStableId != basePlan.AreaSetStableId
                || brief.PlanStableId != basePlan.PlanStableId
                || brief.RenderingProfileStableId != basePlan.RenderingProfileStableId
                || brief.RenderingProfileRevision != basePlan.RenderingProfileRevision
                || !SameHash(brief.RenderingProfileHashSha256,
                    basePlan.RenderingProfileHashSha256))
                throw new InvalidOperationException("StaticSceneryPlacementBriefPlanMismatch");
            return Evaluate(
                brief,
                ComputeBriefHash(),
                LoadReceiptOrDraft(brief),
                정적경관배치PlanHash.Compute(basePlan),
                정적경관배치PlanPipeline.ComputeOverrideHash(overridePlan),
                정적경관배치PlanHash.Compute(mergedPlan));
        }

        public static 정적경관배치ReviewStatusData Evaluate(
            정적경관배치BriefMetadataData brief,
            string briefHash,
            정적경관배치ReviewReceiptData receipt,
            string baseHash,
            string overrideHash,
            string mergedHash)
        {
            var reasons = new List<string>();
            if (receipt.BriefStableId != brief.BriefStableId) reasons.Add("BriefStableId");
            if (receipt.BriefRevision != brief.BriefRevision) reasons.Add("BriefRevision");
            if (receipt.PlanStableId != brief.PlanStableId) reasons.Add("PlanStableId");
            if (!SameHash(receipt.BriefHashSha256, briefHash)) reasons.Add("BriefHash");
            if (!SameHash(receipt.BasePlanHashSha256, baseHash)) reasons.Add("BasePlanHash");
            if (!SameHash(receipt.OverrideHashSha256, overrideHash)) reasons.Add("OverrideHash");
            if (!SameHash(receipt.MergedPlanHashSha256, mergedHash)) reasons.Add("MergedPlanHash");
            if (receipt.RenderingProfileStableId != brief.RenderingProfileStableId)
                reasons.Add("RenderingProfileStableId");
            if (receipt.RenderingProfileRevision != brief.RenderingProfileRevision)
                reasons.Add("RenderingProfileRevision");
            if (!SameHash(receipt.RenderingProfileHashSha256,
                    brief.RenderingProfileHashSha256))
                reasons.Add("RenderingProfileHash");
            var matches = reasons.Count == 0;
            var effective = receipt.ReviewStateCode;
            if (!matches && effective != 정적경관배치ReviewStateCodes.Draft)
                effective = 정적경관배치ReviewStateCodes.Stale;
            return new 정적경관배치ReviewStatusData
            {
                Brief = brief,
                Receipt = receipt,
                BriefHashSha256 = briefHash,
                EffectiveReviewStateCode = effective,
                ReviewMatchesInputs = matches,
                MismatchReason = string.Join(",", reasons),
            };
        }

        public static 정적경관배치ReviewStatusData ApproveCurrent(
            string reviewStateCode,
            string reviewNote)
        {
            if (reviewStateCode != 정적경관배치ReviewStateCodes.Reviewed
                && reviewStateCode != 정적경관배치ReviewStateCodes.ApprovedForSceneApply)
                throw new InvalidOperationException(
                    "StaticSceneryReviewStateNotApprovable:" + reviewStateCode);
            var basePlan = 정적경관배치PlanPipeline.LoadBasePlan();
            var overridePlan = 정적경관배치PlanPipeline.LoadOverridePlan();
            var mergedPlan = 정적경관배치PlanMerger.Merge(basePlan, overridePlan);
            var brief = LoadBriefMetadata();
            if (brief.AreaSetStableId != basePlan.AreaSetStableId
                || brief.PlanStableId != basePlan.PlanStableId
                || brief.RenderingProfileStableId != basePlan.RenderingProfileStableId
                || brief.RenderingProfileRevision != basePlan.RenderingProfileRevision
                || !SameHash(brief.RenderingProfileHashSha256,
                    basePlan.RenderingProfileHashSha256))
                throw new InvalidOperationException("StaticSceneryPlacementBriefPlanMismatch");
            var receipt = new 정적경관배치ReviewReceiptData
            {
                ReviewStableId = "world-placement-review:pyeongchang-farm-hub-town-v1",
                BriefStableId = brief.BriefStableId,
                BriefRevision = brief.BriefRevision,
                BriefHashSha256 = ComputeBriefHash(),
                PlanStableId = basePlan.PlanStableId,
                BasePlanHashSha256 = 정적경관배치PlanHash.Compute(basePlan),
                OverrideHashSha256 = 정적경관배치PlanPipeline.ComputeOverrideHash(overridePlan),
                MergedPlanHashSha256 = 정적경관배치PlanHash.Compute(mergedPlan),
                RenderingProfileStableId = brief.RenderingProfileStableId,
                RenderingProfileRevision = brief.RenderingProfileRevision,
                RenderingProfileHashSha256 = brief.RenderingProfileHashSha256,
                ReviewStateCode = reviewStateCode,
                ReviewedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ReviewNote = reviewNote?.Trim() ?? string.Empty,
            };
            ValidateReceipt(receipt);
            WriteReceipt(receipt);
            AssetDatabase.Refresh();
            return EvaluateCurrent(basePlan, overridePlan, mergedPlan);
        }

        public static void OpenBrief()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(BriefPath);
            if (asset == null)
                throw new InvalidOperationException("StaticSceneryPlacementBriefAssetMissing");
            AssetDatabase.OpenAsset(asset);
        }

        public static void OpenNatureGuide()
            => OpenGuide(NatureGuidePath, "Nature");

        public static void OpenFarmGuide()
            => OpenGuide(FarmGuidePath, "Farm");

        public static void OpenTownGuide()
            => OpenGuide(TownGuidePath, "Town");

        public static void OpenCityGuide()
            => OpenGuide(CityGuidePath, "City");

        private static void OpenGuide(string path, string packCode)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(path);
            if (asset == null)
                throw new InvalidOperationException(
                    "StaticSceneryPackGuideAssetMissing:" + packCode);
            AssetDatabase.OpenAsset(asset);
        }

        private static 정적경관배치ReviewReceiptData CreateDraftReceipt(
            정적경관배치BriefMetadataData brief) => new()
        {
            ReviewStableId = "world-placement-review:pyeongchang-farm-hub-town-v1",
            BriefStableId = brief.BriefStableId,
            BriefRevision = brief.BriefRevision,
            PlanStableId = brief.PlanStableId,
            RenderingProfileStableId = brief.RenderingProfileStableId,
            RenderingProfileRevision = brief.RenderingProfileRevision,
            RenderingProfileHashSha256 = brief.RenderingProfileHashSha256,
            ReviewStateCode = 정적경관배치ReviewStateCodes.Draft,
        };

        private static void ValidateReceipt(정적경관배치ReviewReceiptData receipt)
        {
            if (receipt.SchemaVersion != 2
                || string.IsNullOrWhiteSpace(receipt.ReviewStableId)
                || string.IsNullOrWhiteSpace(receipt.BriefStableId)
                || string.IsNullOrWhiteSpace(receipt.BriefRevision)
                || string.IsNullOrWhiteSpace(receipt.PlanStableId)
                || string.IsNullOrWhiteSpace(receipt.RenderingProfileStableId)
                || string.IsNullOrWhiteSpace(receipt.RenderingProfileRevision)
                || !IsSha256(receipt.RenderingProfileHashSha256)
                || !정적경관배치ReviewStateCodes.IsPersistedState(receipt.ReviewStateCode))
                throw new InvalidOperationException("StaticSceneryReviewReceiptInvalid");
            if (receipt.ReviewStateCode == 정적경관배치ReviewStateCodes.Draft) return;
            if (!IsSha256(receipt.BriefHashSha256)
                || !IsSha256(receipt.BasePlanHashSha256)
                || !IsSha256(receipt.OverrideHashSha256)
                || !IsSha256(receipt.MergedPlanHashSha256)
                || !DateTimeOffset.TryParse(receipt.ReviewedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out _))
                throw new InvalidOperationException("StaticSceneryReviewReceiptApprovalInvalid");
        }

        private static Dictionary<string, string> ParseFrontMatter(string value)
        {
            var lines = value.Split('\n');
            if (lines.Length < 3 || lines[0].Trim() != "---")
                throw new InvalidOperationException("StaticSceneryPlacementBriefFrontMatterMissing");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 1; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line == "---") return result;
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var separator = line.IndexOf(':');
                if (separator <= 0)
                    throw new InvalidOperationException(
                        "StaticSceneryPlacementBriefFrontMatterInvalid:" + line);
                result[line.Substring(0, separator).Trim()] =
                    line.Substring(separator + 1).Trim();
            }
            throw new InvalidOperationException("StaticSceneryPlacementBriefFrontMatterUnclosed");
        }

        private static string Require(IReadOnlyDictionary<string, string> values, string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException(
                    "StaticSceneryPlacementBriefMetadataMissing:" + key);

        private static int ParseInt(IReadOnlyDictionary<string, string> values, string key) =>
            int.TryParse(Require(values, key), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new InvalidOperationException(
                    "StaticSceneryPlacementBriefMetadataInvalid:" + key);

        private static bool ParseBool(IReadOnlyDictionary<string, string> values, string key) =>
            bool.TryParse(Require(values, key), out var value)
                ? value
                : throw new InvalidOperationException(
                    "StaticSceneryPlacementBriefMetadataInvalid:" + key);

        private static void WriteReceipt(정적경관배치ReviewReceiptData receipt)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReviewReceiptPath)!);
            File.WriteAllText(
                ReviewReceiptPath,
                JsonConvert.SerializeObject(receipt, JsonSettings));
        }

        private static void ValidateGuideReference(
            string stableId,
            string revision,
            string expectedHash,
            자연경관배치GuideMetadataData actual,
            string actualHash,
            string packCode)
        {
            if (stableId != actual.GuideStableId || revision != actual.GuideRevision)
                throw new InvalidOperationException(
                    "StaticSceneryPackGuideReferenceMismatch:" + packCode);
            if (!SameHash(expectedHash, actualHash))
                throw new InvalidOperationException(
                    "StaticSceneryPackGuideHashMismatch:" + packCode);
        }

        private static bool SameHash(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static bool IsSha256(string value) =>
            value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        private static string BuildDefaultBrief(정적경관배치PlanData plan)
        {
            var natureGuide = LoadNatureGuideMetadata();
            var farmGuide = LoadFarmGuideMetadata();
            var townGuide = LoadTownGuideMetadata();
            var cityGuide = LoadCityGuideMetadata();
            var renderingProfile = 평창군경관RenderingFixture.Create();
            return $@"---
briefSchemaVersion: 4
briefStableId: world-placement-brief:pyeongchang-farm-hub-town-v1
briefRevision: pyeongchang-static-scenery-brief.v5
areaSetStableId: {plan.AreaSetStableId}
planStableId: {plan.PlanStableId}
natureGuideStableId: {natureGuide.GuideStableId}
natureGuideRevision: {natureGuide.GuideRevision}
natureGuideHashSha256: {ComputeNatureGuideHash()}
farmGuideStableId: {farmGuide.GuideStableId}
farmGuideRevision: {farmGuide.GuideRevision}
farmGuideHashSha256: {ComputeFarmGuideHash()}
townGuideStableId: {townGuide.GuideStableId}
townGuideRevision: {townGuide.GuideRevision}
townGuideHashSha256: {ComputeTownGuideHash()}
cityGuideStableId: {cityGuide.GuideStableId}
cityGuideRevision: {cityGuide.GuideRevision}
cityGuideHashSha256: {ComputeCityGuideHash()}
fourPackGuideBundleHashSha256: {ComputeFourPackGuideBundleHash()}
compositionCatalogRevision: {정적경관배치PlanPipeline.CompositionCatalogRevision}
renderingProfileStableId: {renderingProfile.ProfileStableId}
renderingProfileRevision: {renderingProfile.RuleRevision}
renderingProfileHashSha256: {경관RenderingProfileHash.Compute(renderingProfile)}
---

# 평창 Farm·Hub·Town 정적 경관 배치 기획서

## 경관 목표

공공 공간자료와 Simulation 역할을 근거로 대관령 Farm, 진부 Hub, 평창 Town이 하나의 이동 가능한 로우폴리 경관으로 읽히게 한다.

## 영역별 구성

- 대관령 Farm L2 4개 타일: 밭·농장 마당·산림 전이·저수지 경관
- Farm–Hub 회랑: 농로·울타리·수목대와 점진적 물류 전환
- 진부 Hub: 물류 거점과 완충 수목대
- Hub–Town 회랑: 물류 경관에서 생활권으로 점진 전환
- 평창 Town: 주거·상업 경관과 생활 소품

`L4_L7_Synty경관_PresentationOnly`은 8개 배치 대상을 묶는 부모이며 자체 배치를 갖지 않는다.

## 네 팩 배치 기준

- PolygonNature: 연속 지형 위의 산림·능선·가장자리 경관
- PolygonFarm: 경작 블록·농장 마당·집하 작업 경관
- PolygonTown: 저층 주택·상점·정류장·생활권 경관
- PolygonCity: Hub의 Station·상하차·회차·안전 설비 경관
- 네 팩 기준 문서와 구성 대장 개정은 기획서 승인 입력으로 함께 고정한다.

## PolygonNature 숲 경관 적용

- 대관령 북서 L2는 활엽·침엽·혼효림 군집, 숲 가장자리와 산 능선을 주역으로 사용한다.
- 농장과 경작지 경계는 낮은 숲 가장자리와 드문 개별 수목으로 연결한다.
- 회랑·Hub·Town은 이동과 업무 시야를 막지 않는 완충 수목만 사용한다.
- 실제 수계 마스크가 연결되기 전에는 수변 완충지와 개울 회랑을 배치하지 않는다.

## 조명과 명암

- PC·봄·평화 상태의 맑은 늦은 오전 Rendering Profile을 기준으로 사용한다.
- 태양·환경광·안개·URP 후처리와 그림자 Cascade는 Profile 값에서 함께 적용한다.
- 수관과 큰 바위만 그림자를 만들고 하층 식생은 수신 전용, 원경 능선과 FX는 그림자를 끈다.
- Rendering Profile 식별자·개정·hash가 달라지면 기존 Scene 적용 승인은 오래된 상태가 된다.

## 필수 요소

- 큰 지형 덩어리, 중간 군집, 작은 디테일 순서
- VisualKey·CompositionKey 기반 배치와 결정적 hash
- 구획별 로컬 저작 좌표를 세계 Anchor로 변환하는 컨테이너 계약
- 정적 경관과 계절 사건·NPC·차량 표현의 분리

## 금지 요소

- Synty 원본 Prefab 경로·GUID를 업무 계약에 저장하지 않는다.
- 수계·과도한 경사·컨테이너 밖에 임의 배치하지 않는다.
- ScenarioPreview 위치를 실제 관측 위치로 표시하지 않는다.

## 성능과 LOD

- 현재 계획의 Triangle·Material Slot·Draw Call·Shadow Caster·Collider·Animator 예산을 검증한다.
- Overview는 군집, Region은 경관 블록, Task는 세부 자산을 우선한다.

## 자료 한계

- 실제 DEM Mesh와 세분류 토지피복 위치는 아직 연결되지 않았다.
- 현재 높이는 ScenarioPreview이며 관측 공간 사실이 아니다.

## 검토 체크리스트

- [ ] 8개 배치 대상의 주역과 경계 전환이 읽힌다.
- [ ] Nature·Farm·Town·City 기준과 구성 대장 개정이 일치한다.
- [ ] 대관령 Farm·진부 Hub·평창 Town 대표 구획의 큰 덩어리를 검토했다.
- [ ] 경고와 성능 예산을 검토했다.
- [ ] 기획서·기본·보정·병합 계획 hash가 승인 기록과 일치한다.
- [ ] WORLD-PLAN-3 실행 전 Staging을 검토했다.
";
        }
    }
}
