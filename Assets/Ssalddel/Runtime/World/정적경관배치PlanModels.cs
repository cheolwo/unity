using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 정적경관배치SourceKindCodes
    {
        public const string Fixture = "Fixture";
        public const string ServerDerived = "ServerDerived";
    }

    public static class 정적경관배치AssetReferenceKindCodes
    {
        public const string VisualKey = "VisualKey";
        public const string CompositionKey = "CompositionKey";
    }

    public static class 정적경관배치HeightPolicyCodes
    {
        public const string ScenarioPreview = "ScenarioPreview";
        public const string PhysicalElevation = "PhysicalElevation";
        public const string Explicit = "Explicit";
    }

    public static class 정적경관배치OverrideOperationCodes
    {
        public const string Add = "Add";
        public const string Modify = "Modify";
        public const string Disable = "Disable";
    }

    public static class 정적경관배치ReviewStateCodes
    {
        public const string Draft = "Draft";
        public const string Reviewed = "Reviewed";
        public const string ApprovedForSceneApply = "ApprovedForSceneApply";
        public const string Stale = "Stale";

        public static bool IsPersistedState(string value) =>
            value == Draft || value == Reviewed || value == ApprovedForSceneApply;
    }

    public static class 정적경관배치ContainerStableIds
    {
        public const string ScenicRoot = "container:pyeongchang:scenic-root";
        public const string FarmSouthWest = "container:daegwallyeong:l2:700:1144";
        public const string FarmSouthEast = "container:daegwallyeong:l2:701:1144";
        public const string FarmNorthWest = "container:daegwallyeong:l2:700:1145";
        public const string FarmNorthEast = "container:daegwallyeong:l2:701:1145";
        public const string FarmHubCorridor = "container:pyeongchang:farm-hub-corridor";
        public const string JinbuHub = "container:jinbu:hub";
        public const string HubTownCorridor = "container:pyeongchang:hub-town-corridor";
        public const string PyeongchangTown = "container:pyeongchang-eup:town";
    }

    [Serializable]
    public sealed class 정적경관배치SourceData
    {
        public string KindCode = string.Empty;
        public string StableId = string.Empty;
        public string OutputHashSha256 = string.Empty;
        public string SpatialEvidenceStatusCode = string.Empty;
    }

    [Serializable]
    public sealed class 정적경관배치PositionData
    {
        public float X;
        public float Z;
        public float? ExplicitY;
    }

    [Serializable]
    public sealed class 정적경관배치ContainerTransformData
    {
        public string ContainerStableId = string.Empty;
        public float WorldAnchorX;
        public float WorldAnchorZ;
        public float AuthoringMinimumX;
        public float AuthoringMinimumZ;
        public float AuthoringMaximumX;
        public float AuthoringMaximumZ;
        public float LocalToAnchorScale = 1f;
        public float LocalRotationY;
        public float HeightOffset;
    }

    [Serializable]
    public sealed class 정적경관PerformanceBudgetData
    {
        public long TriangleLimit;
        public int MaterialSlotLimit;
        public int DrawCallLimit;
        public int ShadowCasterLimit;
        public int ColliderLimit;
        public int AnimatorLimit;
    }

    [Serializable]
    public sealed class 정적경관배치ItemData
    {
        public string PlacementStableId = string.Empty;
        public string TargetContainerStableId = string.Empty;
        public string TargetNodeStableId = string.Empty;
        public string AssetReferenceKindCode = string.Empty;
        public string AssetKey = string.Empty;
        public string LandCoverCode = string.Empty;
        public string RegionRoleCode = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public 정적경관배치PositionData Position = new();
        public string HeightPolicyCode = 정적경관배치HeightPolicyCodes.ScenarioPreview;
        public float RotationY;
        public float UniformScale = 1f;
        public int DensityTier;
        public int LodGroup;
        public bool HasWaterMask;
        public string SeasonCode = "Spring";
        public string MoodCode = "Peaceful";
        public float ViewDistance;
        public bool Enabled = true;
        public bool PresentationOnly = true;
    }

    [Serializable]
    public sealed class 정적경관배치PlanData
    {
        public int SchemaVersion = 2;
        public string PlanStableId = string.Empty;
        public string PlanRevision = string.Empty;
        public 정적경관배치SourceData Source = new();
        public string AreaSetStableId = string.Empty;
        public string VisualCatalogRevision = string.Empty;
        public string CompositionCatalogRevision = string.Empty;
        public int Seed;
        public 정적경관PerformanceBudgetData PerformanceBudget = new();
        public 정적경관배치ContainerTransformData[] ContainerTransforms =
            Array.Empty<정적경관배치ContainerTransformData>();
        public 정적경관배치ItemData[] Placements = Array.Empty<정적경관배치ItemData>();
    }

    [Serializable]
    public sealed class 정적경관배치AdjustmentData
    {
        public string? TargetContainerStableId;
        public string? TargetNodeStableId;
        public string? AssetReferenceKindCode;
        public string? AssetKey;
        public string? LandCoverCode;
        public string? RegionRoleCode;
        public string? EvidenceKindCode;
        public 정적경관배치PositionData? Position;
        public string? HeightPolicyCode;
        public float? RotationY;
        public float? UniformScale;
        public int? DensityTier;
        public int? LodGroup;
        public bool? HasWaterMask;
        public string? SeasonCode;
        public string? MoodCode;
        public float? ViewDistance;
        public bool? Enabled;
    }

    [Serializable]
    public sealed class 정적경관배치OverrideChangeData
    {
        public string OperationCode = string.Empty;
        public string PlacementStableId = string.Empty;
        public string ExpectedPlacementHashSha256 = string.Empty;
        public 정적경관배치ItemData? Placement;
        public 정적경관배치AdjustmentData? Adjustment;
    }

    [Serializable]
    public sealed class 정적경관배치OverridePlanData
    {
        public int SchemaVersion = 1;
        public string OverrideStableId = string.Empty;
        public string BasePlanStableId = string.Empty;
        public string ExpectedBasePlanHashSha256 = string.Empty;
        public 정적경관배치OverrideChangeData[] Changes =
            Array.Empty<정적경관배치OverrideChangeData>();
    }

    [Serializable]
    public sealed class 정적경관배치ReviewReceiptData
    {
        public int SchemaVersion = 1;
        public string ReviewStableId = string.Empty;
        public string BriefStableId = string.Empty;
        public string BriefRevision = string.Empty;
        public string BriefHashSha256 = string.Empty;
        public string PlanStableId = string.Empty;
        public string BasePlanHashSha256 = string.Empty;
        public string OverrideHashSha256 = string.Empty;
        public string MergedPlanHashSha256 = string.Empty;
        public string ReviewStateCode = 정적경관배치ReviewStateCodes.Draft;
        public string ReviewedAtUtc = string.Empty;
        public string ReviewNote = string.Empty;
    }

    public static class 정적경관배치PlanValidator
    {
        public const string InvalidCode = "StaticSceneryPlacementPlanInvalid";

        public static void Validate(정적경관배치PlanData plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            Require(plan.SchemaVersion == 2, "지원하지 않는 정적 경관 배치 계획 schema입니다.");
            RequireText(plan.PlanStableId, "배치 계획 식별자");
            RequireText(plan.PlanRevision, "배치 계획 개정");
            Require(plan.Source != null, "배치 계획 원본 정보가 필요합니다.");
            Require(plan.Source.KindCode == 정적경관배치SourceKindCodes.Fixture
                    || plan.Source.KindCode == 정적경관배치SourceKindCodes.ServerDerived,
                "지원하지 않는 배치 계획 원본 종류입니다.");
            RequireText(plan.Source.StableId, "배치 계획 원본 식별자");
            RequireSha256(plan.Source.OutputHashSha256, "배치 계획 원본 hash");
            RequireText(plan.Source.SpatialEvidenceStatusCode, "공간 근거 상태");
            RequireText(plan.AreaSetStableId, "AreaSet 식별자");
            RequireText(plan.VisualCatalogRevision, "시각 자산 대장 개정");
            RequireText(plan.CompositionCatalogRevision, "조합 대장 개정");
            Require(plan.Seed != 0, "결정적 seed가 필요합니다.");
            Require(plan.PerformanceBudget != null, "성능 예산이 필요합니다.");
            RequireBudget(plan.PerformanceBudget);
            Require(plan.ContainerTransforms != null && plan.ContainerTransforms.Length > 0,
                "구획별 로컬 제작 공간이 필요합니다.");
            RequireDistinct(plan.ContainerTransforms.Select(item => item.ContainerStableId),
                "구획 변환 식별자");
            foreach (var containerTransform in plan.ContainerTransforms)
                ValidateContainerTransform(containerTransform);
            Require(plan.Placements != null && plan.Placements.Length > 0,
                "정적 경관 배치 항목이 필요합니다.");
            RequireDistinct(plan.Placements.Select(item => item.PlacementStableId),
                "배치 식별자");
            var containerIds = plan.ContainerTransforms
                .Select(item => item.ContainerStableId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var placement in plan.Placements)
            {
                ValidatePlacement(placement);
                Require(containerIds.Contains(placement.TargetContainerStableId),
                    "배치 대상 구획의 로컬 제작 공간이 없습니다.");
            }
        }

        public static void ValidateOverride(
            정적경관배치OverridePlanData plan,
            정적경관배치PlanData basePlan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (basePlan == null) throw new ArgumentNullException(nameof(basePlan));
            Require(plan.SchemaVersion == 1, "지원하지 않는 배치 보정 schema입니다.");
            RequireText(plan.OverrideStableId, "배치 보정 식별자");
            Require(plan.BasePlanStableId == basePlan.PlanStableId,
                "배치 보정의 기본 계획 식별자가 일치하지 않습니다.");
            if (plan.Changes.Length > 0)
                Require(plan.ExpectedBasePlanHashSha256 == 정적경관배치PlanHash.Compute(basePlan),
                    "배치 보정의 기본 계획 hash가 오래되었습니다.");
            RequireDistinct(plan.Changes.Select(item => item.PlacementStableId),
                "배치 보정 항목");
            foreach (var change in plan.Changes)
            {
                Require(change != null, "빈 배치 보정 항목은 허용되지 않습니다.");
                RequireText(change.PlacementStableId, "보정 대상 배치 식별자");
                Require(change.OperationCode == 정적경관배치OverrideOperationCodes.Add
                        || change.OperationCode == 정적경관배치OverrideOperationCodes.Modify
                        || change.OperationCode == 정적경관배치OverrideOperationCodes.Disable,
                    "지원하지 않는 배치 보정 작업입니다.");
                if (change.OperationCode == 정적경관배치OverrideOperationCodes.Add)
                {
                    Require(change.Placement != null, "추가할 전체 배치가 필요합니다.");
                    Require(change.Placement!.PlacementStableId == change.PlacementStableId,
                        "추가 배치 식별자가 보정 대상과 일치하지 않습니다.");
                    ValidatePlacement(change.Placement);
                }
                else
                {
                    RequireSha256(change.ExpectedPlacementHashSha256, "대상 배치 hash");
                    Require(change.OperationCode != 정적경관배치OverrideOperationCodes.Modify
                            || change.Adjustment != null,
                        "수정할 배치 값이 필요합니다.");
                }
            }
        }

        public static void ValidatePlacement(정적경관배치ItemData placement)
        {
            Require(placement != null, "빈 배치 항목은 허용되지 않습니다.");
            RequireText(placement.PlacementStableId, "배치 식별자");
            RequireText(placement.TargetContainerStableId, "배치 Container 식별자");
            RequireText(placement.TargetNodeStableId, "대상 node 식별자");
            Require(placement.AssetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.VisualKey
                    || placement.AssetReferenceKindCode == 정적경관배치AssetReferenceKindCodes.CompositionKey,
                "지원하지 않는 시각 자산 참조 종류입니다.");
            RequireSemanticKey(placement.AssetKey, "시각 자산 의미 키");
            RequireText(placement.LandCoverCode, "토지피복 코드");
            RequireText(placement.RegionRoleCode, "영역 역할 코드");
            RequireText(placement.EvidenceKindCode, "배치 근거 코드");
            Require(placement.Position != null
                    && IsFinite(placement.Position.X) && IsFinite(placement.Position.Z)
                    && (!placement.Position.ExplicitY.HasValue
                        || IsFinite(placement.Position.ExplicitY.Value)),
                "배치 좌표가 유효하지 않습니다.");
            Require(placement.HeightPolicyCode == 정적경관배치HeightPolicyCodes.ScenarioPreview
                    || placement.HeightPolicyCode == 정적경관배치HeightPolicyCodes.PhysicalElevation
                    || placement.HeightPolicyCode == 정적경관배치HeightPolicyCodes.Explicit,
                "지원하지 않는 높이 정책입니다.");
            Require(placement.HeightPolicyCode != 정적경관배치HeightPolicyCodes.Explicit
                    || placement.Position.ExplicitY.HasValue,
                "명시적 높이 정책에는 Y 좌표가 필요합니다.");
            Require(IsFinite(placement.RotationY), "배치 회전이 유효하지 않습니다.");
            Require(IsFinite(placement.UniformScale) && placement.UniformScale > 0f,
                "배치 축척은 0보다 큰 유한 값이어야 합니다.");
            Require(placement.DensityTier is >= 0 and <= 2, "배치 밀도 단계가 유효하지 않습니다.");
            Require(placement.LodGroup is >= 0 and <= 2, "배치 LOD가 유효하지 않습니다.");
            Require(placement.PresentationOnly, "정적 경관 배치는 표현 전용이어야 합니다.");
        }

        public static void ValidateContainerTransform(
            정적경관배치ContainerTransformData value)
        {
            Require(value != null, "빈 구획 변환은 허용되지 않습니다.");
            RequireText(value.ContainerStableId, "구획 변환 식별자");
            Require(IsFinite(value.WorldAnchorX) && IsFinite(value.WorldAnchorZ),
                "구획의 World 기준점이 유효하지 않습니다.");
            Require(IsFinite(value.AuthoringMinimumX)
                    && IsFinite(value.AuthoringMinimumZ)
                    && IsFinite(value.AuthoringMaximumX)
                    && IsFinite(value.AuthoringMaximumZ)
                    && value.AuthoringMinimumX < value.AuthoringMaximumX
                    && value.AuthoringMinimumZ < value.AuthoringMaximumZ,
                "구획의 로컬 제작 경계가 유효하지 않습니다.");
            Require(IsFinite(value.LocalToAnchorScale) && value.LocalToAnchorScale > 0f,
                "구획의 Scene 변환 축척은 0보다 커야 합니다.");
            Require(IsFinite(value.LocalRotationY) && IsFinite(value.HeightOffset),
                "구획의 Scene 변환 회전 또는 높이가 유효하지 않습니다.");
        }

        private static void RequireBudget(정적경관PerformanceBudgetData budget)
        {
            Require(budget.TriangleLimit > 0 && budget.MaterialSlotLimit > 0
                    && budget.DrawCallLimit > 0 && budget.ShadowCasterLimit > 0
                    && budget.ColliderLimit > 0 && budget.AnimatorLimit > 0,
                "정적 경관 성능 예산은 모두 0보다 커야 합니다.");
        }

        private static void RequireSemanticKey(string value, string name)
        {
            RequireText(value, name);
            Require(!value.Contains("/") && !value.Contains("\\"),
                name + "에는 자산 파일 경로를 저장할 수 없습니다.");
        }

        private static void RequireDistinct(IEnumerable<string> values, string name)
        {
            var items = values.ToArray();
            Require(items.Distinct(StringComparer.Ordinal).Count() == items.Length,
                name + "가 중복되었습니다.");
        }

        private static void RequireText(string? value, string name) =>
            Require(!string.IsNullOrWhiteSpace(value), name + "이(가) 필요합니다.");

        private static void RequireSha256(string value, string name) =>
            Require(value != null && value.Length == 64 && value.All(Uri.IsHexDigit),
                name + "은(는) 64자리 SHA-256이어야 합니다.");

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(InvalidCode + ":" + message);
        }
    }

    public static class 정적경관배치PlanMerger
    {
        public const string RebaseRequiredCode = "StaticSceneryPlacementOverrideRebaseRequired";

        public static 정적경관배치PlanData Merge(
            정적경관배치PlanData basePlan,
            정적경관배치OverridePlanData overridePlan)
        {
            정적경관배치PlanValidator.Validate(basePlan);
            정적경관배치PlanValidator.ValidateOverride(overridePlan, basePlan);
            var merged = Clone(basePlan);
            var values = merged.Placements.ToDictionary(
                item => item.PlacementStableId, StringComparer.Ordinal);
            foreach (var change in overridePlan.Changes)
            {
                if (change.OperationCode == 정적경관배치OverrideOperationCodes.Add)
                {
                    if (values.ContainsKey(change.PlacementStableId))
                        throw new InvalidOperationException(RebaseRequiredCode + ":DuplicateAdd");
                    values.Add(change.PlacementStableId, Clone(change.Placement!));
                    continue;
                }
                if (!values.TryGetValue(change.PlacementStableId, out var current)
                    || 정적경관배치PlanHash.ComputePlacement(current)
                        != change.ExpectedPlacementHashSha256)
                    throw new InvalidOperationException(RebaseRequiredCode + ":"
                        + change.PlacementStableId);
                if (change.OperationCode == 정적경관배치OverrideOperationCodes.Disable)
                {
                    current.Enabled = false;
                    continue;
                }
                Apply(current, change.Adjustment!);
                정적경관배치PlanValidator.ValidatePlacement(current);
            }
            merged.Placements = values.Values
                .OrderBy(item => item.PlacementStableId, StringComparer.Ordinal)
                .ToArray();
            정적경관배치PlanValidator.Validate(merged);
            return merged;
        }

        private static void Apply(
            정적경관배치ItemData target,
            정적경관배치AdjustmentData value)
        {
            if (value.TargetContainerStableId != null) target.TargetContainerStableId = value.TargetContainerStableId;
            if (value.TargetNodeStableId != null) target.TargetNodeStableId = value.TargetNodeStableId;
            if (value.AssetReferenceKindCode != null) target.AssetReferenceKindCode = value.AssetReferenceKindCode;
            if (value.AssetKey != null) target.AssetKey = value.AssetKey;
            if (value.LandCoverCode != null) target.LandCoverCode = value.LandCoverCode;
            if (value.RegionRoleCode != null) target.RegionRoleCode = value.RegionRoleCode;
            if (value.EvidenceKindCode != null) target.EvidenceKindCode = value.EvidenceKindCode;
            if (value.Position != null) target.Position = Clone(value.Position);
            if (value.HeightPolicyCode != null) target.HeightPolicyCode = value.HeightPolicyCode;
            if (value.RotationY.HasValue) target.RotationY = value.RotationY.Value;
            if (value.UniformScale.HasValue) target.UniformScale = value.UniformScale.Value;
            if (value.DensityTier.HasValue) target.DensityTier = value.DensityTier.Value;
            if (value.LodGroup.HasValue) target.LodGroup = value.LodGroup.Value;
            if (value.HasWaterMask.HasValue) target.HasWaterMask = value.HasWaterMask.Value;
            if (value.SeasonCode != null) target.SeasonCode = value.SeasonCode;
            if (value.MoodCode != null) target.MoodCode = value.MoodCode;
            if (value.ViewDistance.HasValue) target.ViewDistance = value.ViewDistance.Value;
            if (value.Enabled.HasValue) target.Enabled = value.Enabled.Value;
        }

        public static 정적경관배치PlanData Clone(정적경관배치PlanData value) => new()
        {
            SchemaVersion = value.SchemaVersion,
            PlanStableId = value.PlanStableId,
            PlanRevision = value.PlanRevision,
            Source = new 정적경관배치SourceData
            {
                KindCode = value.Source.KindCode,
                StableId = value.Source.StableId,
                OutputHashSha256 = value.Source.OutputHashSha256,
                SpatialEvidenceStatusCode = value.Source.SpatialEvidenceStatusCode,
            },
            AreaSetStableId = value.AreaSetStableId,
            VisualCatalogRevision = value.VisualCatalogRevision,
            CompositionCatalogRevision = value.CompositionCatalogRevision,
            Seed = value.Seed,
            PerformanceBudget = new 정적경관PerformanceBudgetData
            {
                TriangleLimit = value.PerformanceBudget.TriangleLimit,
                MaterialSlotLimit = value.PerformanceBudget.MaterialSlotLimit,
                DrawCallLimit = value.PerformanceBudget.DrawCallLimit,
                ShadowCasterLimit = value.PerformanceBudget.ShadowCasterLimit,
                ColliderLimit = value.PerformanceBudget.ColliderLimit,
                AnimatorLimit = value.PerformanceBudget.AnimatorLimit,
            },
            ContainerTransforms = value.ContainerTransforms.Select(Clone).ToArray(),
            Placements = value.Placements.Select(Clone).ToArray(),
        };

        private static 정적경관배치ContainerTransformData Clone(
            정적경관배치ContainerTransformData value) => new()
        {
            ContainerStableId = value.ContainerStableId,
            WorldAnchorX = value.WorldAnchorX,
            WorldAnchorZ = value.WorldAnchorZ,
            AuthoringMinimumX = value.AuthoringMinimumX,
            AuthoringMinimumZ = value.AuthoringMinimumZ,
            AuthoringMaximumX = value.AuthoringMaximumX,
            AuthoringMaximumZ = value.AuthoringMaximumZ,
            LocalToAnchorScale = value.LocalToAnchorScale,
            LocalRotationY = value.LocalRotationY,
            HeightOffset = value.HeightOffset,
        };

        public static 정적경관배치ItemData Clone(정적경관배치ItemData value) => new()
        {
            PlacementStableId = value.PlacementStableId,
            TargetContainerStableId = value.TargetContainerStableId,
            TargetNodeStableId = value.TargetNodeStableId,
            AssetReferenceKindCode = value.AssetReferenceKindCode,
            AssetKey = value.AssetKey,
            LandCoverCode = value.LandCoverCode,
            RegionRoleCode = value.RegionRoleCode,
            EvidenceKindCode = value.EvidenceKindCode,
            Position = Clone(value.Position),
            HeightPolicyCode = value.HeightPolicyCode,
            RotationY = value.RotationY,
            UniformScale = value.UniformScale,
            DensityTier = value.DensityTier,
            LodGroup = value.LodGroup,
            HasWaterMask = value.HasWaterMask,
            SeasonCode = value.SeasonCode,
            MoodCode = value.MoodCode,
            ViewDistance = value.ViewDistance,
            Enabled = value.Enabled,
            PresentationOnly = value.PresentationOnly,
        };

        private static 정적경관배치PositionData Clone(정적경관배치PositionData value) => new()
        {
            X = value.X,
            Z = value.Z,
            ExplicitY = value.ExplicitY,
        };
    }

    public static class 정적경관배치PlanHash
    {
        public static string Compute(정적경관배치PlanData plan)
        {
            정적경관배치PlanValidator.Validate(plan);
            var builder = new StringBuilder();
            Append(builder, plan.SchemaVersion, plan.PlanStableId, plan.PlanRevision,
                plan.Source.KindCode, plan.Source.StableId,
                plan.Source.OutputHashSha256.ToLowerInvariant(),
                plan.Source.SpatialEvidenceStatusCode, plan.AreaSetStableId,
                plan.VisualCatalogRevision, plan.CompositionCatalogRevision, plan.Seed,
                plan.PerformanceBudget.TriangleLimit,
                plan.PerformanceBudget.MaterialSlotLimit,
                plan.PerformanceBudget.DrawCallLimit,
                plan.PerformanceBudget.ShadowCasterLimit,
                plan.PerformanceBudget.ColliderLimit,
                plan.PerformanceBudget.AnimatorLimit);
            foreach (var containerTransform in plan.ContainerTransforms
                         .OrderBy(item => item.ContainerStableId, StringComparer.Ordinal))
                Append(builder,
                    containerTransform.ContainerStableId,
                    Format(containerTransform.WorldAnchorX),
                    Format(containerTransform.WorldAnchorZ),
                    Format(containerTransform.AuthoringMinimumX),
                    Format(containerTransform.AuthoringMinimumZ),
                    Format(containerTransform.AuthoringMaximumX),
                    Format(containerTransform.AuthoringMaximumZ),
                    Format(containerTransform.LocalToAnchorScale),
                    Format(containerTransform.LocalRotationY),
                    Format(containerTransform.HeightOffset));
            foreach (var placement in plan.Placements
                         .OrderBy(item => item.PlacementStableId, StringComparer.Ordinal))
                builder.Append(ComputePlacement(placement)).Append('|');
            return Sha256(builder.ToString());
        }

        public static string ComputePlacement(정적경관배치ItemData placement)
        {
            정적경관배치PlanValidator.ValidatePlacement(placement);
            var builder = new StringBuilder();
            Append(builder, placement.PlacementStableId,
                placement.TargetContainerStableId, placement.TargetNodeStableId,
                placement.AssetReferenceKindCode, placement.AssetKey,
                placement.LandCoverCode, placement.RegionRoleCode,
                placement.EvidenceKindCode, Format(placement.Position.X),
                Format(placement.Position.Z),
                placement.Position.ExplicitY.HasValue
                    ? Format(placement.Position.ExplicitY.Value) : string.Empty,
                placement.HeightPolicyCode, Format(placement.RotationY),
                Format(placement.UniformScale), placement.DensityTier,
                placement.LodGroup, placement.HasWaterMask,
                placement.SeasonCode, placement.MoodCode,
                Format(placement.ViewDistance), placement.Enabled,
                placement.PresentationOnly);
            return Sha256(builder.ToString());
        }

        public static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(valueByte => valueByte.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void Append(StringBuilder builder, params object[] values)
        {
            foreach (var value in values)
                builder.Append(value?.ToString() ?? string.Empty).Append('|');
        }

        private static string Format(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);
    }
}
