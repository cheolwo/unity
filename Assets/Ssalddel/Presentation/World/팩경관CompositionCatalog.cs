using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 타운경관SetNames
    {
        public const string 저층주택블록 = "저층 주택 블록";
        public const string 읍내상점전면 = "읍내 상점 전면";
        public const string 정원담장경계 = "정원·담장 경계";
        public const string 버스정류장보행쉼터 = "버스 정류장·보행 쉼터";
        public const string 생활서비스골목 = "생활 서비스 골목";
        public const string 소형배달주차공간 = "소형 배달·주차 공간";
        public const string 소도시도로직선 = "소도시 도로 직선";
        public const string T자교차로 = "T자 교차로";
        public const string 십자교차로 = "십자 교차로";
        public const string 텃밭형단독주택 = "텃밭형 단독주택";
        public const string 근린놀이터 = "근린 놀이터";
        public const string 생활공공광장 = "생활 공공광장";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            저층주택블록,
            읍내상점전면,
            정원담장경계,
            버스정류장보행쉼터,
            생활서비스골목,
            소형배달주차공간,
            소도시도로직선,
            T자교차로,
            십자교차로,
            텃밭형단독주택,
            근린놀이터,
            생활공공광장,
        };
    }

    public static class 도시물류경관SetNames
    {
        public const string 물류Station진입부 = "물류 Station 진입부";
        public const string 상하차Dock = "상하차 Dock";
        public const string 화물대기야드 = "화물 대기 야드";
        public const string 포장도로회차공간 = "포장도로·회차 공간";
        public const string 안전서비스설비 = "안전·서비스 설비";
        public const string TownHub전환경관 = "Town–Hub 전환 경관";
        public const string 도시진입교차로 = "도시 진입 교차로";
        public const string 도심마트앞마당 = "도심 마트 앞마당";
        public const string 먹거리상점골목 = "먹거리 상점 골목";
        public const string 공동주택생활마당 = "공동주택 생활마당";
        public const string 사무공공정보관앞 = "사무·공공정보관 앞";
        public const string 도시공원쉼터 = "도시 공원 쉼터";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            물류Station진입부,
            상하차Dock,
            화물대기야드,
            포장도로회차공간,
            안전서비스설비,
            TownHub전환경관,
            도시진입교차로,
            도심마트앞마당,
            먹거리상점골목,
            공동주택생활마당,
            사무공공정보관앞,
            도시공원쉼터,
        };
    }

    public static class 혼합전환경관SetNames
    {
        public const string NatureFarm = "Nature–Farm 전환";
        public const string FarmTown = "Farm–Town 전환";
        public const string FarmHub = "Farm–Hub 전환";
        public const string TownCity = "Town–City 전환";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            NatureFarm,
            FarmTown,
            FarmHub,
            TownCity,
        };
    }

    [Serializable]
    public sealed class 팩경관CompositionCatalogEntry
    {
        [SerializeField] private 월드CompositionDescriptor descriptor = null!;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string[] allowedLandCoverCodes = Array.Empty<string>();
        [SerializeField] private string[] allowedRegionRoleCodes = Array.Empty<string>();
        [SerializeField] private Vector2 slopeRange = new(0f, 15f);
        [SerializeField] private bool requiresWaterMask;
        [SerializeField] private bool clusterAllowed;
        [SerializeField] private bool presentationOnly = true;

        public 월드CompositionDescriptor Descriptor => descriptor;
        public GameObject Prefab => prefab;
        public string CompositionKey => descriptor?.CompositionKey ?? string.Empty;
        public IReadOnlyList<string> AllowedLandCoverCodes => allowedLandCoverCodes;
        public IReadOnlyList<string> AllowedRegionRoleCodes => allowedRegionRoleCodes;
        public Vector2 SlopeRange => slopeRange;
        public bool RequiresWaterMask => requiresWaterMask;
        public bool ClusterAllowed => clusterAllowed;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            월드CompositionDescriptor value,
            GameObject sourcePrefab,
            string[] landCoverCodes,
            string[] regionRoleCodes,
            Vector2 physicalSlopeRange,
            bool waterMaskRequired,
            bool canCluster)
        {
            descriptor = value;
            prefab = sourcePrefab;
            allowedLandCoverCodes = landCoverCodes ?? Array.Empty<string>();
            allowedRegionRoleCodes = regionRoleCodes ?? Array.Empty<string>();
            slopeRange = physicalSlopeRange;
            requiresWaterMask = waterMaskRequired;
            clusterAllowed = canCluster;
            presentationOnly = true;
        }

        public bool Validate() => string.IsNullOrEmpty(DescribeValidationFailure());

        public string DescribeValidationFailure()
        {
            if (descriptor == null) return "DescriptorMissing";
            if (!descriptor.Validate()) return "DescriptorInvalid";
            if (descriptor.PackCode != 월드CompositionPackCodes.Farm
                && descriptor.PackCode != 월드CompositionPackCodes.Town
                && descriptor.PackCode != 월드CompositionPackCodes.City
                && descriptor.PackCode != 월드CompositionPackCodes.Mixed)
                return "PackCodeInvalid";
            if (prefab == null) return "PrefabMissing";
            if (allowedLandCoverCodes.Length == 0
                || allowedLandCoverCodes.Any(value =>
                    !법정동LandCoverCodes.All.Contains(value, StringComparer.Ordinal)))
                return "LandCoverInvalid";
            if (allowedRegionRoleCodes.Length == 0
                || allowedRegionRoleCodes.Any(value =>
                    value != 법정동WorldRoleCodes.Farm
                    && value != 법정동WorldRoleCodes.Hub
                    && value != 법정동WorldRoleCodes.Town))
                return "RegionRoleInvalid";
            if (slopeRange.x < 0f || slopeRange.y < slopeRange.x || slopeRange.y > 90f)
                return "SlopeRangeInvalid";
            if (requiresWaterMask) return "WaterMaskUnsupported";
            if (!presentationOnly) return "PresentationAuthorityInvalid";

            if (descriptor.PackCode == 월드CompositionPackCodes.Farm)
            {
                var farmView = prefab.GetComponent<농장풍경CompositionSetView>();
                if (farmView == null) return "FarmViewMissing";
                if (farmView.SetName != descriptor.SetName
                    || farmView.VariantCode != descriptor.VariantCode)
                    return "FarmViewIdentityMismatch";
                return farmView.ValidateWiring() ? string.Empty : "FarmViewWiringInvalid";
            }

            var packView = prefab.GetComponent<팩경관CompositionSetView>();
            if (packView == null) return "PackViewMissing";
            if (packView.Descriptor.CompositionKey != descriptor.CompositionKey)
                return "PackViewIdentityMismatch";
            return packView.ValidateWiring() ? string.Empty : "PackViewWiringInvalid";
        }

        public bool CanPlace(
            string landCoverCode,
            string regionRoleCode,
            float physicalSlopeDegrees,
            bool hasWaterMask) =>
            allowedLandCoverCodes.Contains(landCoverCode, StringComparer.Ordinal)
            && allowedRegionRoleCodes.Contains(regionRoleCode, StringComparer.Ordinal)
            && physicalSlopeDegrees >= slopeRange.x
            && physicalSlopeDegrees <= slopeRange.y
            && (!requiresWaterMask || hasWaterMask);
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/Farm·Town·City 경관 Composition 대장")]
    public sealed class 팩경관CompositionCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private 팩경관CompositionCatalogEntry[] entries =
            Array.Empty<팩경관CompositionCatalogEntry>();

        public string CatalogRevision => catalogRevision;
        public IReadOnlyList<팩경관CompositionCatalogEntry> Entries => entries;

        public void Configure(string revision, 팩경관CompositionCatalogEntry[] values)
        {
            catalogRevision = revision ?? string.Empty;
            entries = values ?? Array.Empty<팩경관CompositionCatalogEntry>();
        }

        public 팩경관CompositionCatalogEntry Resolve(string compositionKey)
        {
            Validate();
            return entries.SingleOrDefault(value => value.CompositionKey == compositionKey)
                ?? throw new InvalidOperationException(
                    "PackLandscapeCompositionMissing:" + compositionKey);
        }

        public void Validate()
        {
            const int expectedFarmCount = 36;
            const int expectedTownCount = 36;
            const int expectedCityCount = 36;
            const int expectedMixedCount = 12;
            var invalidEntries = entries == null
                ? new[] { "entries:null" }
                : entries.Select((value, index) => new { value, index })
                    .Where(item => item.value == null || !item.value.Validate())
                    .Select(item => item.value == null
                        ? item.index + ":null"
                        : item.index + ":" + item.value.CompositionKey
                          + "(" + item.value.DescribeValidationFailure() + ")")
                    .ToArray();
            var farmCount = entries?.Count(value => value != null
                && value.Descriptor.PackCode == 월드CompositionPackCodes.Farm) ?? 0;
            var townCount = entries?.Count(value => value != null
                && value.Descriptor.PackCode == 월드CompositionPackCodes.Town) ?? 0;
            var cityCount = entries?.Count(value => value != null
                && value.Descriptor.PackCode == 월드CompositionPackCodes.City) ?? 0;
            var mixedCount = entries?.Count(value => value != null
                && value.Descriptor.PackCode == 월드CompositionPackCodes.Mixed) ?? 0;
            if (string.IsNullOrWhiteSpace(catalogRevision)
                || entries == null
                || entries.Length != expectedFarmCount + expectedTownCount
                    + expectedCityCount + expectedMixedCount
                || invalidEntries.Length > 0
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || farmCount != expectedFarmCount
                || townCount != expectedTownCount
                || cityCount != expectedCityCount
                || mixedCount != expectedMixedCount)
            {
                throw new InvalidOperationException(
                    "PackLandscapeCompositionCatalogInvalid:"
                    + $"revision={catalogRevision};count={entries?.Length ?? 0};"
                    + $"farm={farmCount};town={townCount};city={cityCount};mixed={mixedCount};"
                    + "invalid=" + string.Join(",", invalidEntries.Take(8)));
            }

            월드CompositionContractValidator.Validate(
                entries.Select(value => value.Descriptor).ToArray());
        }
    }

    public sealed class FourPackCompositionResolvedEntry
    {
        private readonly 자연경관CompositionCatalogEntry? _natureEntry;
        private readonly 팩경관CompositionCatalogEntry? _packEntry;

        public FourPackCompositionResolvedEntry(자연경관CompositionCatalogEntry value)
        {
            _natureEntry = value;
            Prefab = value.Prefab;
            Footprint = value.Footprint;
            ClusterAllowed = value.HlodEligible;
        }

        public FourPackCompositionResolvedEntry(팩경관CompositionCatalogEntry value)
        {
            _packEntry = value;
            Prefab = value.Prefab;
            Footprint = value.Descriptor.Footprint;
            ClusterAllowed = value.ClusterAllowed;
        }

        public GameObject Prefab { get; }
        public Vector2 Footprint { get; }
        public bool ClusterAllowed { get; }

        public bool CanPlace(
            string landCoverCode,
            string regionRoleCode,
            float physicalSlopeDegrees,
            bool hasWaterMask,
            string seasonCode,
            string moodCode,
            float viewDistance)
        {
            if (_packEntry != null)
                return _packEntry.CanPlace(
                    landCoverCode, regionRoleCode, physicalSlopeDegrees, hasWaterMask);
            return _natureEntry != null
                && new 자연경관CompositionSelector().CanPlace(
                    _natureEntry, landCoverCode, physicalSlopeDegrees,
                    hasWaterMask, seasonCode, moodCode, viewDistance);
        }
    }

    public sealed class FourPackCompositionRegistry
    {
        private readonly 자연경관CompositionCatalog _natureCatalog;
        private readonly 팩경관CompositionCatalog _packCatalog;

        public FourPackCompositionRegistry(
            자연경관CompositionCatalog natureCatalog,
            팩경관CompositionCatalog packCatalog)
        {
            _natureCatalog = natureCatalog
                ?? throw new ArgumentNullException(nameof(natureCatalog));
            _packCatalog = packCatalog
                ?? throw new ArgumentNullException(nameof(packCatalog));
            _natureCatalog.Validate();
            _packCatalog.Validate();
        }

        public FourPackCompositionResolvedEntry Resolve(string compositionKey)
        {
            var nature = _natureCatalog.Entries.SingleOrDefault(value =>
                value.CompositionKey == compositionKey);
            if (nature != null) return new FourPackCompositionResolvedEntry(nature);
            return new FourPackCompositionResolvedEntry(_packCatalog.Resolve(compositionKey));
        }
    }
}
