using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class HAreaSet배치규칙Record
    {
        public string AreaRoleCode = string.Empty;
        public float NearestAreaCenterDistance;
        public float CoreRadiusBefore;
        public float AllowedCoreRadius;
        public float AppliedScale;
    }

    [Serializable]
    public sealed class H1배치규칙Record
    {
        public string AreaRoleCode = string.Empty;
        public string H1Name = string.Empty;
        public string CategoryCode = string.Empty;
        public string LandscapeNodeName = string.Empty;
        public string BlockNodeName = string.Empty;
        public string ScaleBandCode = string.Empty;
        public Vector3 OriginalPosition;
        public Vector3 AppliedPosition;
        public Vector3 FootprintCenterAfter;
        public Vector2 FootprintAfter;
        public float HeightAfter;
        public float HeightToPlayerRatio;
        public float FootprintToPlayerRatio;
        public float AppliedScale;
        public float SampledSlopeDegrees;
        public bool Accepted;
    }

    /// <summary>
    /// H5 중심 간격, H4 외곽 크기와 H1 실제 발자국을 연결해 적용한
    /// Scene 배치 제약의 재현 가능한 기록입니다. 실제 DEM 판정은 아니며,
    /// 현재 H3 평탄 바닥에서의 조립 증거만 나타냅니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class H공간배치규칙증거Root : MonoBehaviour
    {
        public const string RequiredRevision = "h-spatial-placement-rules.v1";

        [SerializeField] private string ruleRevision = string.Empty;
        [SerializeField] private string surfacePolicyCode = string.Empty;
        [SerializeField] private string farmCoreAnchorPolicyCode = string.Empty;
        [SerializeField] private float areaCoreRadiusRatio;
        [SerializeField] private float minimumObjectClearance;
        [SerializeField] private float buildingRidgeClearance;
        [SerializeField] private float maximumBuildingSlopeDegrees;
        [SerializeField] private float maximumRidgeEnvelope;
        [SerializeField] private float maximumFenceSegmentGap;
        [SerializeField] private HAreaSet배치규칙Record[] areaSetRecords =
            Array.Empty<HAreaSet배치규칙Record>();
        [SerializeField] private H1배치규칙Record[] h1Records =
            Array.Empty<H1배치규칙Record>();
        [SerializeField] private 배치통제계층Root placementControlHierarchy = null!;
        [SerializeField] private 도로변전면배치PatternRoot[] roadsideFrontagePatterns =
            Array.Empty<도로변전면배치PatternRoot>();
        [SerializeField] private int fenceChainCount;
        [SerializeField] private int farmCoreAnchorCount;
        [SerializeField] private int relocatedObjectCount;
        [SerializeField] private int normalizedFootprintCount;
        [SerializeField] private string[] violations = Array.Empty<string>();
        [SerializeField] private bool presentationOnly = true;

        public string RuleRevision => ruleRevision;
        public string SurfacePolicyCode => surfacePolicyCode;
        public string FarmCoreAnchorPolicyCode => farmCoreAnchorPolicyCode;
        public float AreaCoreRadiusRatio => areaCoreRadiusRatio;
        public float MinimumObjectClearance => minimumObjectClearance;
        public float BuildingRidgeClearance => buildingRidgeClearance;
        public float MaximumBuildingSlopeDegrees => maximumBuildingSlopeDegrees;
        public float MaximumRidgeEnvelope => maximumRidgeEnvelope;
        public float MaximumFenceSegmentGap => maximumFenceSegmentGap;
        public HAreaSet배치규칙Record[] AreaSetRecords => areaSetRecords;
        public H1배치규칙Record[] H1Records => h1Records;
        public 배치통제계층Root PlacementControlHierarchy => placementControlHierarchy;
        public 도로변전면배치PatternRoot[] RoadsideFrontagePatterns =>
            roadsideFrontagePatterns;
        public int FenceChainCount => fenceChainCount;
        public int FarmCoreAnchorCount => farmCoreAnchorCount;
        public int RelocatedObjectCount => relocatedObjectCount;
        public int NormalizedFootprintCount => normalizedFootprintCount;
        public string[] Violations => violations;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            float coreRadiusRatio,
            float objectClearance,
            float ridgeClearance,
            float maximumSlope,
            float ridgeEnvelope,
            float fenceGap,
            HAreaSet배치규칙Record[] areaRecords,
            H1배치규칙Record[] objectRecords,
            배치통제계층Root controlHierarchy,
            도로변전면배치PatternRoot[] frontagePatterns,
            int continuousFenceChains,
            int anchoredFarmCoreObjects,
            int relocatedObjects,
            int normalizedFootprints,
            string[] ruleViolations)
        {
            ruleRevision = RequiredRevision;
            surfacePolicyCode = "SyntheticFlatH3Underlay";
            farmCoreAnchorPolicyCode = "FivePointPlayerWorkRing";
            areaCoreRadiusRatio = coreRadiusRatio;
            minimumObjectClearance = objectClearance;
            buildingRidgeClearance = ridgeClearance;
            maximumBuildingSlopeDegrees = maximumSlope;
            maximumRidgeEnvelope = ridgeEnvelope;
            maximumFenceSegmentGap = fenceGap;
            areaSetRecords = areaRecords ?? Array.Empty<HAreaSet배치규칙Record>();
            h1Records = objectRecords ?? Array.Empty<H1배치규칙Record>();
            placementControlHierarchy = controlHierarchy;
            roadsideFrontagePatterns = frontagePatterns
                ?? Array.Empty<도로변전면배치PatternRoot>();
            fenceChainCount = continuousFenceChains;
            farmCoreAnchorCount = anchoredFarmCoreObjects;
            relocatedObjectCount = relocatedObjects;
            normalizedFootprintCount = normalizedFootprints;
            violations = ruleViolations ?? Array.Empty<string>();
            presentationOnly = true;
            Validate();
        }

        public void Validate()
        {
            if (ruleRevision != RequiredRevision
                || surfacePolicyCode != "SyntheticFlatH3Underlay"
                || farmCoreAnchorPolicyCode != "FivePointPlayerWorkRing"
                || areaCoreRadiusRatio is <= 0f or >= .5f
                || minimumObjectClearance <= 0f
                || buildingRidgeClearance <= minimumObjectClearance
                || maximumBuildingSlopeDegrees is <= 0f or > 10f
                || maximumRidgeEnvelope <= 0f
                || maximumFenceSegmentGap <= 0f
                || areaSetRecords.Length != 4
                || areaSetRecords.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.AreaRoleCode)
                    || value.NearestAreaCenterDistance <= 0f
                    || value.CoreRadiusBefore <= 0f
                    || value.AllowedCoreRadius <= 0f
                    || value.AppliedScale is <= 0f or > 1f)
                || h1Records.Length < 100
                || h1Records.Any(value => value == null || !value.Accepted
                    || string.IsNullOrWhiteSpace(value.H1Name)
                    || string.IsNullOrWhiteSpace(value.LandscapeNodeName)
                    || string.IsNullOrWhiteSpace(value.BlockNodeName)
                    || value.ScaleBandCode != value.CategoryCode
                    || value.FootprintAfter.x <= 0f
                    || value.FootprintAfter.y <= 0f
                    || value.HeightAfter <= 0f
                    || value.HeightToPlayerRatio <= 0f
                    || value.FootprintToPlayerRatio <= 0f
                    || value.SampledSlopeDegrees > maximumBuildingSlopeDegrees)
                || placementControlHierarchy == null
                || roadsideFrontagePatterns.Length != 1
                || roadsideFrontagePatterns.Any(value => value == null)
                || fenceChainCount <= 0
                || farmCoreAnchorCount != 5
                || relocatedObjectCount < 0 || normalizedFootprintCount <= 0
                || violations.Length != 0 || !presentationOnly)
                throw new InvalidOperationException(
                    "HSpatialPlacementRuleEvidenceInvalid");
            placementControlHierarchy.Validate();
            foreach (var pattern in roadsideFrontagePatterns)
                pattern.Validate();
            if (h1Records.Any(value => !placementControlHierarchy
                    .FindBand(value.ScaleBandCode).Contains(
                        value.HeightToPlayerRatio,
                        value.FootprintToPlayerRatio)))
                throw new InvalidOperationException(
                    "HSpatialPlacementScaleBandInvalid");
        }
    }
}
