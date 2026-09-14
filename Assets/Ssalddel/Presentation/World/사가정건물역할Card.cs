using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 사가정건물RoleCodes
    {
        public const string Restaurant = "Restaurant";
        public const string Warehouse = "Warehouse";
        public const string ResidentialDelivery = "ResidentialDelivery";
        public const string GeneralUnbound = "GeneralUnbound";
    }

    public static class 사가정건물FollowUpCodes
    {
        public const string None = "None";
        public const string FoodDeliveryJourney = "FoodDeliveryJourney";
        public const string LifecyclePreview = "LifecyclePreview";
    }

    /// <summary>
    /// 건물 종류를 H 계층으로 오인하지 않도록 건물과 역할별 H1 행동 지점을 분리한 읽기 전용 카드 자료다.
    /// 한 건물은 여러 역할을 가질 수 있지만 실제 근거 없는 역할은 자동 추론하지 않는다.
    /// </summary>
    public sealed class 사가정건물RoleProfile
    {
        internal 사가정건물RoleProfile(string roleStableId, string roleCode,
            string displayNameKo, string iconLabel, string semanticPlaceStableId,
            string operatingSystemId, IReadOnlyList<string> h1ActionStableIds,
            string lifecycleSummaryKo, IReadOnlyList<string> lifecycleStageCodes,
            string followUpCode, string followUpLabelKo, string followUpTargetStableId,
            string evidenceStatusCode)
        {
            RoleStableId = Require(roleStableId, "BuildingRoleStableIdMissing");
            RoleCode = Require(roleCode, "BuildingRoleCodeMissing");
            DisplayNameKo = Require(displayNameKo, "BuildingRoleDisplayNameMissing");
            IconLabel = Require(iconLabel, "BuildingRoleIconMissing");
            SemanticPlaceStableId = semanticPlaceStableId?.Trim() ?? string.Empty;
            OperatingSystemId = operatingSystemId?.Trim() ?? string.Empty;
            H1ActionStableIds = FreezeDistinct(h1ActionStableIds, "BuildingRoleH1Invalid");
            LifecycleSummaryKo = lifecycleSummaryKo?.Trim() ?? string.Empty;
            LifecycleStageCodes = FreezeDistinct(lifecycleStageCodes, "BuildingRoleLifecycleInvalid");
            FollowUpCode = Require(followUpCode, "BuildingRoleFollowUpCodeMissing");
            FollowUpLabelKo = followUpLabelKo?.Trim() ?? string.Empty;
            FollowUpTargetStableId = followUpTargetStableId?.Trim() ?? string.Empty;
            EvidenceStatusCode = Require(evidenceStatusCode, "BuildingRoleEvidenceStatusMissing");

            if (FollowUpCode != 사가정건물FollowUpCodes.None
                && (FollowUpLabelKo.Length == 0 || OperatingSystemId.Length == 0))
                throw new ArgumentException("BuildingRoleFollowUpInvalid");
            if (FollowUpCode == 사가정건물FollowUpCodes.FoodDeliveryJourney
                && FollowUpTargetStableId.Length == 0)
                throw new ArgumentException("BuildingRoleFollowUpTargetMissing");
        }

        public string RoleStableId { get; }
        public string RoleCode { get; }
        public string DisplayNameKo { get; }
        public string IconLabel { get; }
        public string SemanticPlaceStableId { get; }
        public string OperatingSystemId { get; }
        public IReadOnlyList<string> H1ActionStableIds { get; }
        public string LifecycleSummaryKo { get; }
        public IReadOnlyList<string> LifecycleStageCodes { get; }
        public string FollowUpCode { get; }
        public string FollowUpLabelKo { get; }
        public string FollowUpTargetStableId { get; }
        public string EvidenceStatusCode { get; }
        public bool IsSyntheticFixture => EvidenceStatusCode == "SyntheticFixture";
        public bool IsPublicBusinessFact => false;
        public bool HasFollowUp => FollowUpCode != 사가정건물FollowUpCodes.None;

        private static string Require(string value, string error)
        {
            value = value?.Trim() ?? string.Empty;
            if (value.Length == 0) throw new ArgumentException(error);
            return value;
        }

        private static IReadOnlyList<string> FreezeDistinct(
            IReadOnlyList<string> values, string error)
        {
            if (values == null || values.Any(string.IsNullOrWhiteSpace)
                || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                throw new ArgumentException(error);
            return values.Select(value => value.Trim()).ToArray();
        }
    }

    public static class 사가정건물RoleCatalog
    {
        public const string FoodDeliveryRouteStableId =
            "route:synthetic:sagajeong-food-delivery:a";

        private static readonly IReadOnlyDictionary<string, IReadOnlyList<사가정건물RoleProfile>>
            ExplicitProfiles = new Dictionary<string, IReadOnlyList<사가정건물RoleProfile>>(
                StringComparer.Ordinal)
            {
                ["osm:way:1256772531"] = new[]
                {
                    Profile("building-role:synthetic:sagajeong:restaurant-a",
                        사가정건물RoleCodes.Restaurant, "음식점", "식",
                        "synthetic-place:food-route-a",
                        OperationalWorldOperatingSystemIds.FoodDelivery,
                        new[] { "h1-stock:town-order-packing" },
                        "주문 → 수락 → 조리 → 배차 → 픽업 → 전달 → 귀환",
                        new[]
                        {
                            "food.order", "food.restaurant-response", "food.cooking",
                            "food.dispatch", "food.pickup", "food.delivery"
                        },
                        사가정건물FollowUpCodes.FoodDeliveryJourney,
                        "주문·기사 흐름 보기", FoodDeliveryRouteStableId),
                },
                ["osm:way:1256772606"] = new[]
                {
                    Profile("building-role:synthetic:sagajeong:residential-delivery-a",
                        사가정건물RoleCodes.ResidentialDelivery, "주거·전달", "집",
                        "synthetic-place:food-route-b",
                        OperationalWorldOperatingSystemIds.FoodDelivery,
                        new[] { "h1-stock:town-resident-pickup" },
                        "배달 출발 → 도착 → 수령 확인 → 기사 귀환",
                        new[] { "food.dispatch", "food.delivery" },
                        사가정건물FollowUpCodes.FoodDeliveryJourney,
                        "배달 도착 흐름 보기", FoodDeliveryRouteStableId),
                },
                ["osm:way:470492219"] = new[]
                {
                    Profile("building-role:synthetic:sagajeong:warehouse-a",
                        사가정건물RoleCodes.Warehouse, "창고", "창",
                        "synthetic-place:warehouse-a",
                        OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                        new[]
                        {
                            "h1-stock:hub-receiving-storage",
                            "h1-stock:hub-outbound-staging"
                        },
                        "입고 계획 → 입고 → 검수 → 적치 → 출고 할당 → 피킹 → 포장 → 인계",
                        new[]
                        {
                            "warehouse.inbound-plan", "warehouse.receiving",
                            "warehouse.inspection", "warehouse.put-away-inventory",
                            "warehouse.outbound-allocation", "warehouse.picking",
                            "warehouse.packing", "warehouse.outbound-handoff"
                        },
                        사가정건물FollowUpCodes.LifecyclePreview,
                        "창고 흐름 미리보기", string.Empty),
                },
            };

        public static int ExplicitBuildingCount => ExplicitProfiles.Count;
        public static int ExplicitRoleCount => ExplicitProfiles.Values.Sum(value => value.Count);

        public static IReadOnlyList<사가정건물RoleProfile> Resolve(string buildingStableId)
        {
            if (!string.IsNullOrWhiteSpace(buildingStableId)
                && ExplicitProfiles.TryGetValue(buildingStableId, out var profiles))
                return profiles;
            return new[]
            {
                Profile("building-role:unbound:" + (buildingStableId ?? "missing"),
                    사가정건물RoleCodes.GeneralUnbound, "일반·미결속", "건",
                    string.Empty, string.Empty, Array.Empty<string>(),
                    "공간 근거만 있음 · 역할과 업무 흐름 결속 대기",
                    Array.Empty<string>(), 사가정건물FollowUpCodes.None,
                    string.Empty, string.Empty, "SourceGeometryOnly")
            };
        }

        private static 사가정건물RoleProfile Profile(string roleStableId,
            string roleCode, string displayNameKo, string iconLabel,
            string semanticPlaceStableId, string operatingSystemId,
            IReadOnlyList<string> h1ActionStableIds, string lifecycleSummaryKo,
            IReadOnlyList<string> lifecycleStageCodes, string followUpCode,
            string followUpLabelKo, string followUpTargetStableId,
            string evidenceStatusCode = "SyntheticFixture")
            => new(roleStableId, roleCode, displayNameKo, iconLabel,
                semanticPlaceStableId, operatingSystemId, h1ActionStableIds,
                lifecycleSummaryKo, lifecycleStageCodes, followUpCode,
                followUpLabelKo, followUpTargetStableId, evidenceStatusCode);
    }

    public sealed class 사가정건물RoleFollowUpResult
    {
        internal 사가정건물RoleFollowUpResult(사가정건물RoleProfile profile,
            bool routeSnapshotAvailable, string statusCode)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            RouteSnapshotAvailable = routeSnapshotAvailable;
            StatusCode = statusCode;
        }

        public 사가정건물RoleProfile Profile { get; }
        public bool RouteSnapshotAvailable { get; }
        public bool ReadOnly => true;
        public bool ChangesAuthorityState => false;
        public string StatusCode { get; }
    }

    public static class 사가정건물RoleFollowUpResolver
    {
        public static bool TryResolve(사가정건물RoleProfile profile,
            IReadOnlyCollection<string> availableRouteStableIds,
            out 사가정건물RoleFollowUpResult result)
        {
            result = null;
            if (profile == null || !profile.HasFollowUp) return false;
            var available = availableRouteStableIds ?? Array.Empty<string>();
            var routeReady = profile.FollowUpCode
                             == 사가정건물FollowUpCodes.FoodDeliveryJourney
                             && available.Contains(profile.FollowUpTargetStableId,
                                 StringComparer.Ordinal);
            var status = profile.FollowUpCode switch
            {
                사가정건물FollowUpCodes.FoodDeliveryJourney when routeReady
                    => "ReadOnlyRouteSnapshotSelected",
                사가정건물FollowUpCodes.FoodDeliveryJourney
                    => "WaitingForReadOnlyRouteSnapshot",
                _ => "SyntheticLifecyclePreviewOnly",
            };
            result = new 사가정건물RoleFollowUpResult(profile, routeReady, status);
            return true;
        }
    }
}
