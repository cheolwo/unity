using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 실제 운영 자료와 섞이지 않는 네 공간형 OS의 메모리 전용 검증 표본입니다.
    /// 이 타입은 HTTP, DB, 파일 저장 또는 운영 Command를 제공하지 않습니다.
    /// </summary>
    public static class 다중Os생명주기검증Policy
    {
        public const string FixtureRevision = "observable-operations-lifecycle.r4";
        public const string AreaStableId = "area:kr-seoul-jungnang-myeonmok";
        public const int DurationSeconds = 600;
        public const string RestaurantIconInputStatus = "restaurant-icon input pending";
        public const string Disclosure = "합성 표본 · 운영 효과 없음 · 실제 업체 주문 아님";
        public const string MovementMode = "GeneralizedStageAnchorInterpolation";
        public const bool RouteAuthority = false;

        private static readonly string[] Supported =
        {
            OperationalWorldOperatingSystemIds.FoodDelivery,
            OperationalWorldOperatingSystemIds.DomesticCargoTransport,
            OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
            OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics
        };

        private static readonly IReadOnlyDictionary<string, 다중Os생명주기검증Anchor>
            VerificationStageAnchors = CreateVerificationStageAnchors();

        public static IReadOnlyList<string> SupportedOperatingSystemIds
            => Supported.ToArray();

        public static bool IsSupportedOperatingSystem(string value)
            => Supported.Contains(value, StringComparer.Ordinal);

        /// <summary>
        /// 서버 r4 표본의 정확한 의미 위치를 기존 여섯 가상 배치 지점 주변의
        /// 서로 다른 검증 전용 기준점으로 일반화합니다. 공개·운영 위치에는
        /// 사용하지 않으며 도로, 차선 또는 실제 주소를 추정하지 않습니다.
        /// </summary>
        public static bool TryResolveVerificationAnchor(
            string operatingSystemId,
            string semanticPlaceStableId,
            out 다중Os생명주기검증Anchor anchor)
        {
            anchor = null;
            return !string.IsNullOrWhiteSpace(operatingSystemId)
                   && !string.IsNullOrWhiteSpace(semanticPlaceStableId)
                   && VerificationStageAnchors.TryGetValue(semanticPlaceStableId, out anchor)
                   && string.Equals(anchor.OperatingSystemId, operatingSystemId,
                       StringComparison.Ordinal);
        }

        public static IReadOnlyList<string> VerificationSemanticPlaceStableIds
            => VerificationStageAnchors.Keys.OrderBy(value => value,
                StringComparer.Ordinal).ToArray();

        private static IReadOnlyDictionary<string, 다중Os생명주기검증Anchor>
            CreateVerificationStageAnchors()
        {
            var result = new Dictionary<string, 다중Os생명주기검증Anchor>(
                StringComparer.Ordinal);
            void Add(string os, string placeCode, string baseAnchor,
                float offsetX, float offsetZ)
            {
                var semanticPlace = "synthetic-place:" + placeCode;
                result.Add(semanticPlace, new 다중Os생명주기검증Anchor(
                    os, semanticPlace, baseAnchor, offsetX, 0f, offsetZ));
            }

            var food = OperationalWorldOperatingSystemIds.FoodDelivery;
            Add(food, "food-customer-a", "synthetic-place:food-route-a", 42f, 34f);
            Add(food, "food-restaurant-a", "synthetic-place:food-route-a", -34f, -26f);
            Add(food, "food-driver-wait-a", "synthetic-place:food-route-a", -65f, 18f);
            Add(food, "food-customer-b", "synthetic-place:food-route-b", 35f, 38f);
            Add(food, "food-restaurant-b", "synthetic-place:food-route-b", -36f, -24f);
            Add(food, "food-driver-wait-b", "synthetic-place:food-route-b", -68f, 18f);
            Add(food, "food-route-b", "synthetic-place:food-route-b", 0f, 8f);

            var cargo = OperationalWorldOperatingSystemIds.DomesticCargoTransport;
            Add(cargo, "cargo-origin-a", "synthetic-place:cargo-route-a", -58f, -34f);
            Add(cargo, "cargo-driver-wait-a", "synthetic-place:cargo-route-a", -70f, 25f);
            Add(cargo, "cargo-route-a", "synthetic-place:cargo-route-a", 0f, 0f);
            Add(cargo, "cargo-destination-a", "synthetic-place:cargo-route-a", 62f, 36f);
            Add(cargo, "cargo-origin-b", "synthetic-place:cargo-route-b", -58f, -34f);
            Add(cargo, "cargo-driver-wait-b", "synthetic-place:cargo-route-b", -70f, 25f);
            Add(cargo, "cargo-route-b", "synthetic-place:cargo-route-b", 0f, 0f);
            Add(cargo, "cargo-safe-stop-b", "synthetic-place:cargo-route-b", 12f, 45f);
            Add(cargo, "cargo-destination-b", "synthetic-place:cargo-route-b", 62f, 36f);

            var warehouse = OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment;
            Add(warehouse, "warehouse-inbound-a", "synthetic-place:warehouse-a", -70f, -42f);
            Add(warehouse, "warehouse-inspection-a", "synthetic-place:warehouse-a", -42f, -10f);
            Add(warehouse, "warehouse-storage-a", "synthetic-place:warehouse-a", 0f, 28f);
            Add(warehouse, "warehouse-picking-a", "synthetic-place:warehouse-a", 32f, 18f);
            Add(warehouse, "warehouse-packing-a", "synthetic-place:warehouse-a", 55f, -5f);
            Add(warehouse, "warehouse-outbound-a", "synthetic-place:warehouse-a", 78f, -35f);
            Add(warehouse, "warehouse-inbound-b", "synthetic-place:warehouse-a", -68f, 55f);
            Add(warehouse, "warehouse-inspection-b", "synthetic-place:warehouse-a", -40f, 75f);
            Add(warehouse, "warehouse-hold-b", "synthetic-place:warehouse-a", -5f, 92f);
            Add(warehouse, "warehouse-storage-b", "synthetic-place:warehouse-a", 20f, 70f);
            Add(warehouse, "warehouse-picking-b", "synthetic-place:warehouse-a", 48f, 54f);
            Add(warehouse, "warehouse-packing-b", "synthetic-place:warehouse-a", 70f, 32f);
            Add(warehouse, "warehouse-outbound-b", "synthetic-place:warehouse-a", 90f, 7f);

            var mart = OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics;
            Add(mart, "mart-a", "synthetic-place:mart-a", -70f, -40f);
            Add(mart, "mart-inbound-a", "synthetic-place:mart-a", -48f, -10f);
            Add(mart, "mart-storage-a", "synthetic-place:mart-a", -15f, 18f);
            Add(mart, "mart-packing-a", "synthetic-place:mart-a", 20f, 5f);
            Add(mart, "mart-handoff-a", "synthetic-place:mart-a", 58f, -20f);
            Add(mart, "mart-b", "synthetic-place:mart-a", -70f, 60f);
            Add(mart, "mart-inbound-b", "synthetic-place:mart-a", -45f, 88f);
            Add(mart, "mart-storage-b", "synthetic-place:mart-a", -10f, 105f);
            Add(mart, "mart-hold-b", "synthetic-place:mart-a", 18f, 82f);
            Add(mart, "mart-packing-b", "synthetic-place:mart-a", 45f, 60f);
            Add(mart, "mart-handoff-b", "synthetic-place:mart-a", 75f, 35f);

            return result;
        }
    }

    public sealed class 다중Os생명주기검증Anchor
    {
        public 다중Os생명주기검증Anchor(string operatingSystemId,
            string semanticPlaceStableId, string baseAnchorStableId,
            float offsetX, float offsetY, float offsetZ)
        {
            OperatingSystemId = operatingSystemId;
            SemanticPlaceStableId = semanticPlaceStableId;
            BaseAnchorStableId = baseAnchorStableId;
            OffsetX = offsetX;
            OffsetY = offsetY;
            OffsetZ = offsetZ;
        }

        public string OperatingSystemId { get; }
        public string SemanticPlaceStableId { get; }
        public string BaseAnchorStableId { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }
    }

    public static class 다중Os생명주기StageCodes
    {
        public const string FoodOrder = "food.order";
        public const string FoodRestaurantResponse = "food.restaurant-response";
        public const string FoodCooking = "food.cooking";
        public const string FoodDispatch = "food.dispatch";
        public const string FoodPickup = "food.pickup";
        public const string FoodDelivery = "food.delivery";
        public const string FoodInterruptionRecovery = "food.interruption-recovery";

        public const string CargoRequest = "cargo.request";
        public const string CargoTermsAgreement = "cargo.terms-agreement";
        public const string CargoDispatch = "cargo.dispatch";
        public const string CargoPickup = "cargo.pickup";
        public const string CargoTransport = "cargo.transport";
        public const string CargoDropoff = "cargo.dropoff";
        public const string CargoEvidenceSettlement = "cargo.evidence-settlement";
        public const string CargoInterruptionRecovery = "cargo.interruption-recovery";

        public const string WarehouseInboundPlan = "warehouse.inbound-plan";
        public const string WarehouseReceiving = "warehouse.receiving";
        public const string WarehouseInspection = "warehouse.inspection";
        public const string WarehousePutAwayInventory = "warehouse.put-away-inventory";
        public const string WarehouseOutboundAllocation = "warehouse.outbound-allocation";
        public const string WarehousePicking = "warehouse.picking";
        public const string WarehousePacking = "warehouse.packing";
        public const string WarehouseOutboundHandoff = "warehouse.outbound-handoff";
        public const string WarehouseExceptionRecovery = "warehouse.exception-recovery";

        public const string MartSupplyAgreement = "mart.supply-agreement";
        public const string MartReplenishmentOrder = "mart.replenishment-order";
        public const string MartInboundReceiving = "mart.inbound-receiving";
        public const string MartPutawayInventory = "mart.putaway-inventory";
        public const string MartCustomerOrderAllocation = "mart.customer-order-allocation";
        public const string MartPickingPacking = "mart.picking-packing";
        public const string MartLastMileHandoff = "mart.last-mile-handoff";
        public const string MartCompletionRecovery = "mart.completion-recovery";
    }

    public enum 다중Os생명주기검증Moment
    {
        Overview,
        FoodRecovery,
        WarehouseCargoMartHandoff,
        CompletionAndReturn
    }

    public sealed class 다중Os생명주기표현State
    {
        public string ObjectStableId { get; set; } = string.Empty;
        public string WorkStableId { get; set; } = string.Empty;
        public string OperatingSystemId { get; set; } = string.Empty;
        public string OperatingSystemLabel { get; set; } = string.Empty;
        public string LifecycleStageCode { get; set; } = string.Empty;
        public string LifecycleStageLabel { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;
        public string ActivityLabel { get; set; } = string.Empty;
        public string AttentionStateCode { get; set; } = string.Empty;
        public string AttentionLabel { get; set; } = string.Empty;
        public string CueCode { get; set; } = string.Empty;
        public string SemanticPlaceStableId { get; set; } = string.Empty;
        public string SourceKindCode { get; set; } = string.Empty;
        public string ScenarioRunStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public float Progress01 { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public sealed class 다중Os생명주기표현Diagnostic
    {
        public string ObjectStableId { get; set; } = string.Empty;
        public string OperatingSystemId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class 다중Os생명주기표현Plan
    {
        public bool Accepted { get; set; }
        public 다중Os생명주기표현State[] States { get; set; }
            = Array.Empty<다중Os생명주기표현State>();
        public 다중Os생명주기표현Diagnostic[] Diagnostics { get; set; }
            = Array.Empty<다중Os생명주기표현Diagnostic>();
    }

    public static class 다중Os생명주기표현DiagnosticCodes
    {
        public const string PlacementPlanRejected = "LifecyclePlacementPlanRejected";
        public const string IdentityMissing = "LifecycleIdentityMissing";
        public const string OperatingSystemUnsupported = "LifecycleOperatingSystemUnsupported";
        public const string StageUnsupported = "LifecycleStageUnsupported";
        public const string AttentionUnsupported = "LifecycleAttentionUnsupported";
        public const string SourceUnsupported = "LifecycleSourceUnsupported";
    }

    /// <summary>
    /// v2 배치 준비 명령을 색상만이 아닌 단계 문구·주의 표식·진행률로 좁힙니다.
    /// 미지원 항목 한 건은 격리하되 다른 OS의 표현 계획은 계속 만듭니다.
    /// </summary>
    public sealed class 다중Os생명주기표현Projector
    {
        private sealed class StageDescriptor
        {
            public StageDescriptor(string label, float progress)
            {
                Label = label;
                Progress = progress;
            }

            public string Label { get; }
            public float Progress { get; }
        }

        private static readonly IReadOnlyDictionary<string, string> OsLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OperationalWorldOperatingSystemIds.FoodDelivery] = "음식배달",
                [OperationalWorldOperatingSystemIds.DomesticCargoTransport] = "화물운송",
                [OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment] = "창고",
                [OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics] = "마트·도심물류"
            };

        private static readonly IReadOnlyDictionary<string, StageDescriptor> Stages =
            CreateStageDescriptors();
        private static readonly IReadOnlyDictionary<string, string> ActivityLabels =
            CreateActivityLabels();

        public 다중Os생명주기표현Plan Create(OperationalWorldPlacementPlan source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.Accepted)
            {
                return new 다중Os생명주기표현Plan
                {
                    Accepted = false,
                    Diagnostics = new[]
                    {
                        new 다중Os생명주기표현Diagnostic
                        {
                            Code = 다중Os생명주기표현DiagnosticCodes.PlacementPlanRejected
                        }
                    }
                };
            }

            var states = new List<다중Os생명주기표현State>();
            var diagnostics = new List<다중Os생명주기표현Diagnostic>();
            foreach (var instruction in source.Instructions
                         ?? Array.Empty<OperationalWorldPlacementInstruction>())
            {
                var error = Validate(instruction);
                if (!string.IsNullOrEmpty(error))
                {
                    diagnostics.Add(new 다중Os생명주기표현Diagnostic
                    {
                        ObjectStableId = instruction?.ObjectStableId ?? string.Empty,
                        OperatingSystemId = instruction?.OperatingSystemId ?? string.Empty,
                        Code = error
                    });
                    continue;
                }

                var stage = Stages[instruction.LifecycleStageCode];
                states.Add(new 다중Os생명주기표현State
                {
                    ObjectStableId = instruction.ObjectStableId,
                    WorkStableId = instruction.WorkStableId,
                    OperatingSystemId = instruction.OperatingSystemId,
                    OperatingSystemLabel = OsLabels[instruction.OperatingSystemId],
                    LifecycleStageCode = instruction.LifecycleStageCode,
                    LifecycleStageLabel = stage.Label,
                    ActivityCode = instruction.ActivityCode,
                    ActivityLabel = ActivityLabel(instruction.ActivityCode, stage.Label),
                    AttentionStateCode = instruction.AttentionStateCode,
                    AttentionLabel = AttentionLabel(instruction.AttentionStateCode),
                    CueCode = CueCode(instruction.AttentionStateCode,
                        instruction.LifecycleStageCode),
                    SemanticPlaceStableId = instruction.SemanticPlaceStableId,
                    SourceKindCode = instruction.SourceKindCode,
                    ScenarioRunStableId = instruction.ScenarioRunStableId,
                    Revision = instruction.Revision,
                    Progress01 = stage.Progress,
                    ExpiresAtUtc = instruction.ExpiresAtUtc
                });
            }

            return new 다중Os생명주기표현Plan
            {
                Accepted = true,
                States = states
                    .OrderBy(value => value.OperatingSystemId, StringComparer.Ordinal)
                    .ThenBy(value => value.ObjectStableId, StringComparer.Ordinal)
                    .ToArray(),
                Diagnostics = diagnostics
                    .OrderBy(value => value.OperatingSystemId, StringComparer.Ordinal)
                    .ThenBy(value => value.ObjectStableId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        private static string Validate(OperationalWorldPlacementInstruction value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ObjectStableId)
                              || string.IsNullOrWhiteSpace(value.WorkStableId)
                              || string.IsNullOrWhiteSpace(value.SemanticPlaceStableId))
                return 다중Os생명주기표현DiagnosticCodes.IdentityMissing;
            if (!OsLabels.ContainsKey(value.OperatingSystemId))
                return 다중Os생명주기표현DiagnosticCodes.OperatingSystemUnsupported;
            if (!Stages.ContainsKey(value.LifecycleStageCode))
                return 다중Os생명주기표현DiagnosticCodes.StageUnsupported;
            if (!SupportedAttention(value.AttentionStateCode))
                return 다중Os생명주기표현DiagnosticCodes.AttentionUnsupported;
            if (!string.Equals(value.SourceKindCode,
                    OperationalWorldSceneSourceKinds.VerificationSample, StringComparison.Ordinal)
                && !string.Equals(value.SourceKindCode,
                    OperationalWorldSceneSourceKinds.OperationalProjection, StringComparison.Ordinal))
                return 다중Os생명주기표현DiagnosticCodes.SourceUnsupported;
            return string.Empty;
        }

        private static bool SupportedAttention(string value)
            => string.Equals(value, OperationalWorldAttentionStateCodes.Active, StringComparison.Ordinal)
               || string.Equals(value, OperationalWorldAttentionStateCodes.Completed, StringComparison.Ordinal)
               || string.Equals(value, OperationalWorldAttentionStateCodes.RecoveryPending, StringComparison.Ordinal)
               || string.Equals(value, OperationalWorldAttentionStateCodes.Recovered, StringComparison.Ordinal);

        private static string AttentionLabel(string value)
            => value == OperationalWorldAttentionStateCodes.Completed ? "완료"
                : value == OperationalWorldAttentionStateCodes.RecoveryPending ? "보호 보류"
                : value == OperationalWorldAttentionStateCodes.Recovered ? "회복 재개"
                : "진행 중";

        private static string CueCode(string attention, string stage)
        {
            if (attention == OperationalWorldAttentionStateCodes.RecoveryPending)
                return "Hold";
            if (attention == OperationalWorldAttentionStateCodes.Recovered)
                return "Recovery";
            if (attention == OperationalWorldAttentionStateCodes.Completed)
                return "Result";
            if (stage.IndexOf("handoff", StringComparison.Ordinal) >= 0)
                return "Handoff";
            if (stage.IndexOf("cooking", StringComparison.Ordinal) >= 0
                || stage.IndexOf("inspection", StringComparison.Ordinal) >= 0
                || stage.IndexOf("packing", StringComparison.Ordinal) >= 0)
                return "Working";
            return "Moving";
        }

        private static IReadOnlyDictionary<string, StageDescriptor> CreateStageDescriptors()
        {
            var result = new Dictionary<string, StageDescriptor>(StringComparer.Ordinal);
            void Add(string code, string label, float progress)
                => result.Add(code, new StageDescriptor(label, progress));

            Add(다중Os생명주기StageCodes.FoodOrder, "주문 접수", .08f);
            Add(다중Os생명주기StageCodes.FoodRestaurantResponse, "음식점 수락", .18f);
            Add(다중Os생명주기StageCodes.FoodCooking, "조리", .3f);
            Add(다중Os생명주기StageCodes.FoodDispatch, "기사 배정", .42f);
            Add(다중Os생명주기StageCodes.FoodPickup, "픽업", .55f);
            Add(다중Os생명주기StageCodes.FoodDelivery, "전달·수령·귀환", .72f);
            Add(다중Os생명주기StageCodes.FoodInterruptionRecovery, "중단 보호·재조리 준비", .46f);

            Add(다중Os생명주기StageCodes.CargoRequest, "운송 의뢰", .08f);
            Add(다중Os생명주기StageCodes.CargoTermsAgreement, "조건 합의", .18f);
            Add(다중Os생명주기StageCodes.CargoDispatch, "화물차 배차", .32f);
            Add(다중Os생명주기StageCodes.CargoPickup, "상차", .45f);
            Add(다중Os생명주기StageCodes.CargoTransport, "운송", .58f);
            Add(다중Os생명주기StageCodes.CargoDropoff, "하차·수하인 인수", .72f);
            Add(다중Os생명주기StageCodes.CargoEvidenceSettlement, "증빙 반환", 1f);
            Add(다중Os생명주기StageCodes.CargoInterruptionRecovery, "운행 중단 보호·재계획", .54f);

            Add(다중Os생명주기StageCodes.WarehouseInboundPlan, "입고 예정", .06f);
            Add(다중Os생명주기StageCodes.WarehouseReceiving, "입고", .16f);
            Add(다중Os생명주기StageCodes.WarehouseInspection, "검수", .28f);
            Add(다중Os생명주기StageCodes.WarehousePutAwayInventory, "적치·재고 반영", .42f);
            Add(다중Os생명주기StageCodes.WarehouseOutboundAllocation, "출고 할당", .56f);
            Add(다중Os생명주기StageCodes.WarehousePicking, "피킹", .69f);
            Add(다중Os생명주기StageCodes.WarehousePacking, "포장", .82f);
            Add(다중Os생명주기StageCodes.WarehouseOutboundHandoff, "출고 대기 인계", 1f);
            Add(다중Os생명주기StageCodes.WarehouseExceptionRecovery, "수량 이상 보호·재검수", .3f);

            Add(다중Os생명주기StageCodes.MartSupplyAgreement, "공급계약 이용", .06f);
            Add(다중Os생명주기StageCodes.MartReplenishmentOrder, "점포별 입고 발주", .18f);
            Add(다중Os생명주기StageCodes.MartInboundReceiving, "입고·검수", .31f);
            Add(다중Os생명주기StageCodes.MartPutawayInventory, "적재·재고", .44f);
            Add(다중Os생명주기StageCodes.MartCustomerOrderAllocation, "고객 주문 할당", .57f);
            Add(다중Os생명주기StageCodes.MartPickingPacking, "피킹·포장", .72f);
            Add(다중Os생명주기StageCodes.MartLastMileHandoff, "라스트마일 인계", .88f);
            Add(다중Os생명주기StageCodes.MartCompletionRecovery, "완료·회복", 1f);

            // 현재 6개 완료 표본도 새 계층에서 사라지지 않게 하는 읽기 호환 값입니다.
            Add("ReceiptConfirmed", "수령 확인 완료", 1f);
            Add("ReceiptConfirmedAfterRedispatch", "재배차 후 수령 확인", 1f);
            Add("ConsigneeAccepted", "수하인 인수 완료", 1f);
            Add("ConsigneeAcceptedAfterReplan", "재계획 후 인수 완료", 1f);
            Add("Packed", "포장 완료", 1f);
            Add("ProjectionRecovered", "후속 투영 회복", 1f);
            return result;
        }

        private static string ActivityLabel(string code, string stageFallback)
            => !string.IsNullOrWhiteSpace(code)
               && ActivityLabels.TryGetValue(code, out var label)
                ? label
                : stageFallback;

        private static IReadOnlyDictionary<string, string> CreateActivityLabels()
            => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["OrderRequested"] = "주문 요청",
                ["RestaurantAccepted"] = "음식점 수락",
                ["Cooking"] = "조리 중",
                ["ReadyForPickup"] = "픽업 준비 완료",
                ["DriverAssigned"] = "기사 배정",
                ["PickedUp"] = "픽업 완료",
                ["Delivered"] = "전달 완료",
                ["ReceiptConfirmed"] = "수령 확인",
                ["DriverReturned"] = "기사 대기지 복귀",
                ["DriverInterrupted"] = "기사 운행 중단",
                ["Recooking"] = "재조리",
                ["Redispatched"] = "재배차",
                ["RepickedUp"] = "재픽업",
                ["TransportRequested"] = "운송 의뢰",
                ["TermsAgreed"] = "운송 조건 합의",
                ["Loaded"] = "상차 완료",
                ["InTransit"] = "운송 중",
                ["Unloaded"] = "하차 완료",
                ["ConsigneeAccepted"] = "수하인 인수",
                ["ProofReturned"] = "인수 증빙 반환",
                ["TransportInterrupted"] = "운송 중단",
                ["CargoProtected"] = "화물 보호 정차",
                ["TransportResumed"] = "운송 재개",
                ["InboundExpected"] = "입고 예정",
                ["Receiving"] = "입고 중",
                ["Inspecting"] = "검수 중",
                ["PutAwayCompleted"] = "적치 완료",
                ["OutboundAllocated"] = "출고 할당",
                ["Picking"] = "피킹 중",
                ["Packed"] = "포장 완료",
                ["OutboundReady"] = "출고 인계 준비",
                ["QuantityMismatchDetected"] = "수량 불일치 발견",
                ["UnitProtected"] = "단위 화물 보호",
                ["Recounted"] = "재검수 완료",
                ["SupplyAgreementSelected"] = "공급 계약 선택",
                ["InboundOrdered"] = "입고 발주",
                ["ReceivedAndInspected"] = "입고·검수 완료",
                ["Stocked"] = "재고 적재",
                ["OrderAllocated"] = "고객 주문 할당",
                ["PickedAndPacked"] = "피킹·포장 완료",
                ["FoodDeliveryChildHandedOff"] = "음식배달 하위 업무 인계",
                ["Completed"] = "완료",
                ["StockoutDetected"] = "재고 부족 발견",
                ["ReplacementOrHold"] = "대체 또는 보류",
                ["StockAllocatedAfterRetry"] = "재시도 후 재고 할당",
                ["CompletedAfterRecovery"] = "회복 후 완료"
            };
    }

    /// <summary>
    /// 서버 observable-operations-lifecycle.r4와 같은 사례·단계·시각·revision을 만드는
    /// 표현 전용 동결 표본입니다. 여덟 사례가 5~565초 사이에 서로 겹쳐 진행됩니다.
    /// </summary>
    public static class 다중Os생명주기검증Fixture
    {
        private sealed class Step
        {
            public Step(int offsetSeconds, string stageCode, string activityCode,
                string placeCode, string attention)
            {
                OffsetSeconds = offsetSeconds;
                StageCode = stageCode;
                ActivityCode = activityCode;
                PlaceCode = placeCode;
                Attention = attention;
            }

            public int OffsetSeconds { get; }
            public string StageCode { get; }
            public string ActivityCode { get; }
            public string PlaceCode { get; }
            public string Attention { get; }
        }

        private sealed class Track
        {
            public Track(string caseCode, string os, string itemKind, string objectKind,
                string role, Step[] steps, params string[] relations)
            {
                CaseCode = caseCode;
                OperatingSystemId = os;
                ItemKind = itemKind;
                ObjectKind = objectKind;
                Role = role;
                Steps = steps;
                Relations = relations ?? Array.Empty<string>();
            }

            public string CaseCode { get; }
            public string OperatingSystemId { get; }
            public string ItemKind { get; }
            public string ObjectKind { get; }
            public string Role { get; }
            public Step[] Steps { get; }
            public string[] Relations { get; }
        }

        private static readonly Track[] Tracks =
        {
            TrackOf("food-normal", OperationalWorldOperatingSystemIds.FoodDelivery,
                OperationalWorldSceneItemKinds.CompletedLifecycle, "FoodDelivery",
                "FoodDeliveryTeam", new[]
                {
                    S(5, 다중Os생명주기StageCodes.FoodOrder, "OrderRequested", "food-customer-a"),
                    S(15, 다중Os생명주기StageCodes.FoodRestaurantResponse, "RestaurantAccepted", "food-restaurant-a"),
                    S(25, 다중Os생명주기StageCodes.FoodCooking, "Cooking", "food-restaurant-a"),
                    S(35, 다중Os생명주기StageCodes.FoodCooking, "ReadyForPickup", "food-restaurant-a"),
                    S(45, 다중Os생명주기StageCodes.FoodDispatch, "DriverAssigned", "food-driver-wait-a"),
                    S(55, 다중Os생명주기StageCodes.FoodPickup, "PickedUp", "food-restaurant-a"),
                    S(65, 다중Os생명주기StageCodes.FoodDelivery, "Delivered", "food-customer-a"),
                    S(75, 다중Os생명주기StageCodes.FoodDelivery, "ReceiptConfirmed", "food-customer-a"),
                    S(85, 다중Os생명주기StageCodes.FoodDelivery, "DriverReturned", "food-driver-wait-a",
                        OperationalWorldAttentionStateCodes.Completed)
                }, "synthetic-role:customer", "synthetic-role:restaurant",
                "synthetic-role:food-driver"),
            TrackOf("food-recovery", OperationalWorldOperatingSystemIds.FoodDelivery,
                OperationalWorldSceneItemKinds.CompletedLifecycle, "FoodDeliveryRecovery",
                "FoodDeliveryRecoveryTeam", new[]
                {
                    S(20, 다중Os생명주기StageCodes.FoodOrder, "OrderRequested", "food-customer-b"),
                    S(30, 다중Os생명주기StageCodes.FoodRestaurantResponse, "RestaurantAccepted", "food-restaurant-b"),
                    S(40, 다중Os생명주기StageCodes.FoodCooking, "Cooking", "food-restaurant-b"),
                    S(50, 다중Os생명주기StageCodes.FoodCooking, "ReadyForPickup", "food-restaurant-b"),
                    S(60, 다중Os생명주기StageCodes.FoodDispatch, "DriverAssigned", "food-driver-wait-b"),
                    S(70, 다중Os생명주기StageCodes.FoodPickup, "PickedUp", "food-restaurant-b"),
                    S(80, 다중Os생명주기StageCodes.FoodInterruptionRecovery, "DriverInterrupted", "food-route-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(95, 다중Os생명주기StageCodes.FoodCooking, "Recooking", "food-restaurant-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(110, 다중Os생명주기StageCodes.FoodDispatch, "Redispatched", "food-driver-wait-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(125, 다중Os생명주기StageCodes.FoodPickup, "RepickedUp", "food-restaurant-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(140, 다중Os생명주기StageCodes.FoodDelivery, "Delivered", "food-customer-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(150, 다중Os생명주기StageCodes.FoodDelivery, "ReceiptConfirmed", "food-customer-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(160, 다중Os생명주기StageCodes.FoodInterruptionRecovery, "DriverReturned", "food-driver-wait-b",
                        OperationalWorldAttentionStateCodes.Recovered)
                }, "synthetic-event:pickup-incident", "synthetic-action:recook",
                "synthetic-action:redispatch"),
            TrackOf("cargo-normal", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                OperationalWorldSceneItemKinds.CargoHandoff, "CargoTransport",
                "CargoDeliveryTeam", new[]
                {
                    S(110, 다중Os생명주기StageCodes.CargoRequest, "TransportRequested", "cargo-origin-a"),
                    S(125, 다중Os생명주기StageCodes.CargoTermsAgreement, "TermsAgreed", "cargo-origin-a"),
                    S(140, 다중Os생명주기StageCodes.CargoDispatch, "DriverAssigned", "cargo-driver-wait-a"),
                    S(155, 다중Os생명주기StageCodes.CargoPickup, "Loaded", "cargo-origin-a"),
                    S(170, 다중Os생명주기StageCodes.CargoTransport, "InTransit", "cargo-route-a"),
                    S(185, 다중Os생명주기StageCodes.CargoDropoff, "Unloaded", "cargo-destination-a"),
                    S(200, 다중Os생명주기StageCodes.CargoDropoff, "ConsigneeAccepted", "cargo-destination-a"),
                    S(215, 다중Os생명주기StageCodes.CargoEvidenceSettlement, "ProofReturned", "cargo-driver-wait-a",
                        OperationalWorldAttentionStateCodes.Completed)
                }, "synthetic-role:shipper", "synthetic-role:cargo-driver",
                "synthetic-role:consignee"),
            TrackOf("cargo-recovery", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                OperationalWorldSceneItemKinds.CargoHandoff, "CargoTransportRecovery",
                "CargoRecoveryTeam", new[]
                {
                    S(150, 다중Os생명주기StageCodes.CargoRequest, "TransportRequested", "cargo-origin-b"),
                    S(165, 다중Os생명주기StageCodes.CargoTermsAgreement, "TermsAgreed", "cargo-origin-b"),
                    S(180, 다중Os생명주기StageCodes.CargoDispatch, "DriverAssigned", "cargo-driver-wait-b"),
                    S(195, 다중Os생명주기StageCodes.CargoPickup, "Loaded", "cargo-origin-b"),
                    S(210, 다중Os생명주기StageCodes.CargoTransport, "InTransit", "cargo-route-b"),
                    S(225, 다중Os생명주기StageCodes.CargoInterruptionRecovery, "TransportInterrupted", "cargo-route-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(240, 다중Os생명주기StageCodes.CargoInterruptionRecovery, "CargoProtected", "cargo-safe-stop-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(255, 다중Os생명주기StageCodes.CargoDispatch, "Redispatched", "cargo-safe-stop-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(270, 다중Os생명주기StageCodes.CargoTransport, "TransportResumed", "cargo-route-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(285, 다중Os생명주기StageCodes.CargoDropoff, "ConsigneeAccepted", "cargo-destination-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(300, 다중Os생명주기StageCodes.CargoEvidenceSettlement, "ProofReturned", "cargo-driver-wait-b",
                        OperationalWorldAttentionStateCodes.Recovered)
                }, "synthetic-event:transport-interruption", "synthetic-action:cargo-protection",
                "synthetic-action:replan"),
            TrackOf("warehouse-normal", OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                OperationalWorldSceneItemKinds.WarehouseTask, "WarehouseOperation",
                "WarehouseTeam", new[]
                {
                    S(250, 다중Os생명주기StageCodes.WarehouseInboundPlan, "InboundExpected", "warehouse-inbound-a"),
                    S(265, 다중Os생명주기StageCodes.WarehouseReceiving, "Receiving", "warehouse-inbound-a"),
                    S(280, 다중Os생명주기StageCodes.WarehouseInspection, "Inspecting", "warehouse-inspection-a"),
                    S(295, 다중Os생명주기StageCodes.WarehousePutAwayInventory, "PutAwayCompleted", "warehouse-storage-a"),
                    S(310, 다중Os생명주기StageCodes.WarehouseOutboundAllocation, "OutboundAllocated", "warehouse-storage-a"),
                    S(325, 다중Os생명주기StageCodes.WarehousePicking, "Picking", "warehouse-picking-a"),
                    S(340, 다중Os생명주기StageCodes.WarehousePacking, "Packed", "warehouse-packing-a"),
                    S(355, 다중Os생명주기StageCodes.WarehouseOutboundHandoff, "OutboundReady", "warehouse-outbound-a",
                        OperationalWorldAttentionStateCodes.Completed)
                }, "synthetic-role:warehouse-worker", "synthetic-object:pallet-a"),
            TrackOf("warehouse-quantity-mismatch-recovery",
                OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                OperationalWorldSceneItemKinds.WarehouseTask, "WarehouseOperationRecovery",
                "WarehouseRecoveryTeam", new[]
                {
                    S(290, 다중Os생명주기StageCodes.WarehouseInboundPlan, "InboundExpected", "warehouse-inbound-b"),
                    S(305, 다중Os생명주기StageCodes.WarehouseReceiving, "Receiving", "warehouse-inbound-b"),
                    S(320, 다중Os생명주기StageCodes.WarehouseInspection, "QuantityMismatchDetected", "warehouse-inspection-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(335, 다중Os생명주기StageCodes.WarehouseExceptionRecovery, "UnitProtected", "warehouse-hold-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(350, 다중Os생명주기StageCodes.WarehouseInspection, "Recounted", "warehouse-inspection-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(365, 다중Os생명주기StageCodes.WarehousePutAwayInventory, "PutAwayCompleted", "warehouse-storage-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(380, 다중Os생명주기StageCodes.WarehouseOutboundAllocation, "OutboundAllocated", "warehouse-storage-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(395, 다중Os생명주기StageCodes.WarehousePicking, "Picking", "warehouse-picking-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(410, 다중Os생명주기StageCodes.WarehousePacking, "Packed", "warehouse-packing-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(425, 다중Os생명주기StageCodes.WarehouseOutboundHandoff, "OutboundReady", "warehouse-outbound-b",
                        OperationalWorldAttentionStateCodes.Recovered)
                }, "synthetic-event:quantity-mismatch", "synthetic-action:unit-hold",
                "synthetic-action:recount"),
            TrackOf("mart-normal", OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
                OperationalWorldSceneItemKinds.WarehouseActor, "MartOperation",
                "MartTeam", new[]
                {
                    S(390, 다중Os생명주기StageCodes.MartSupplyAgreement, "SupplyAgreementSelected", "mart-a"),
                    S(405, 다중Os생명주기StageCodes.MartReplenishmentOrder, "InboundOrdered", "mart-a"),
                    S(420, 다중Os생명주기StageCodes.MartInboundReceiving, "ReceivedAndInspected", "mart-inbound-a"),
                    S(435, 다중Os생명주기StageCodes.MartPutawayInventory, "Stocked", "mart-storage-a"),
                    S(450, 다중Os생명주기StageCodes.MartCustomerOrderAllocation, "OrderAllocated", "mart-storage-a"),
                    S(465, 다중Os생명주기StageCodes.MartPickingPacking, "PickedAndPacked", "mart-packing-a"),
                    S(480, 다중Os생명주기StageCodes.MartLastMileHandoff, "FoodDeliveryChildHandedOff", "mart-handoff-a"),
                    S(495, 다중Os생명주기StageCodes.MartCompletionRecovery, "Completed", "mart-a",
                        OperationalWorldAttentionStateCodes.Completed)
                }, "synthetic-role:mart-worker", "synthetic-object:customer-order-a",
                "synthetic-handoff:food-delivery-a"),
            TrackOf("mart-recovery", OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
                OperationalWorldSceneItemKinds.WarehouseActor, "MartOperationRecovery",
                "MartRecoveryTeam", new[]
                {
                    S(430, 다중Os생명주기StageCodes.MartSupplyAgreement, "SupplyAgreementSelected", "mart-b"),
                    S(445, 다중Os생명주기StageCodes.MartReplenishmentOrder, "InboundOrdered", "mart-b"),
                    S(460, 다중Os생명주기StageCodes.MartInboundReceiving, "ReceivedAndInspected", "mart-inbound-b"),
                    S(475, 다중Os생명주기StageCodes.MartPutawayInventory, "Stocked", "mart-storage-b"),
                    S(490, 다중Os생명주기StageCodes.MartCustomerOrderAllocation, "StockoutDetected", "mart-storage-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(505, 다중Os생명주기StageCodes.MartCompletionRecovery, "ReplacementOrHold", "mart-hold-b",
                        OperationalWorldAttentionStateCodes.RecoveryPending),
                    S(520, 다중Os생명주기StageCodes.MartCustomerOrderAllocation, "StockAllocatedAfterRetry", "mart-storage-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(535, 다중Os생명주기StageCodes.MartPickingPacking, "PickedAndPacked", "mart-packing-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(550, 다중Os생명주기StageCodes.MartLastMileHandoff, "FoodDeliveryChildHandedOff", "mart-handoff-b",
                        OperationalWorldAttentionStateCodes.Recovered),
                    S(565, 다중Os생명주기StageCodes.MartCompletionRecovery, "CompletedAfterRecovery", "mart-b",
                        OperationalWorldAttentionStateCodes.Recovered)
                }, "synthetic-event:stockout", "synthetic-action:replacement-or-hold",
                "synthetic-action:retry")
        };

        public static OperationalWorldSceneResponse CreateResponse(
            string runStableId,
            int elapsedSeconds,
            DateTime startedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(runStableId))
                throw new ArgumentException("MultiOsLifecycleRunStableIdRequired", nameof(runStableId));
            if (startedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("MultiOsLifecycleStartTimeMustBeUtc", nameof(startedAtUtc));

            var elapsed = Math.Max(0, Math.Min(다중Os생명주기검증Policy.DurationSeconds - 1,
                elapsedSeconds));
            var publishedAt = startedAtUtc.AddSeconds(elapsed);

            return new OperationalWorldSceneResponse
            {
                SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2,
                AreaStableId = 다중Os생명주기검증Policy.AreaStableId,
                Cursor = elapsed + 1,
                IsFullSnapshot = true,
                AsOfUtc = publishedAt,
                RefreshAfterSeconds = OperationalWorldScenePolicy.RefreshAfterSeconds,
                Items = Tracks
                    .Where(track => track.Steps[0].OffsetSeconds <= elapsed)
                    .Select(track => ToItem(track, runStableId, elapsed,
                        startedAtUtc, publishedAt))
                    .OrderBy(value => value.OperatingSystemId, StringComparer.Ordinal)
                    .ThenBy(value => value.SnapshotStableId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        public static int MomentSecond(다중Os생명주기검증Moment moment)
            => moment switch
            {
                다중Os생명주기검증Moment.Overview => 465,
                다중Os생명주기검증Moment.FoodRecovery => 80,
                다중Os생명주기검증Moment.WarehouseCargoMartHandoff => 550,
                다중Os생명주기검증Moment.CompletionAndReturn => 590,
                _ => throw new ArgumentOutOfRangeException(nameof(moment), moment, null)
            };

        private static OperationalWorldSceneItem ToItem(Track track, string runStableId,
            int elapsedSeconds,
            DateTime startedAtUtc, DateTime publishedAtUtc)
        {
            var index = Array.FindLastIndex(track.Steps,
                step => step.OffsetSeconds <= elapsedSeconds);
            if (index < 0) throw new InvalidOperationException("MultiOsLifecycleStepMissing");
            var step = track.Steps[index];
            var occurredAt = startedAtUtc.AddSeconds(step.OffsetSeconds);
            return new OperationalWorldSceneItem
            {
                SnapshotStableId = "sample-observation:" + runStableId + ":" + track.CaseCode,
                AreaStableId = 다중Os생명주기검증Policy.AreaStableId,
                OperatingSystemId = track.OperatingSystemId,
                ItemKind = track.ItemKind,
                RoleCode = track.Role,
                ActivityCode = step.ActivityCode,
                Revision = index + 1,
                OccurredAtUtc = occurredAt,
                PublishedAtUtc = publishedAtUtc,
                ExpiresAtUtc = publishedAtUtc.AddMinutes(30),
                IsTombstone = false,
                DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
                LocalStorageAllowed = false,
                ReplayAllowed = false,
                RepresentationDataJson = "{\"synthetic\":true,\"personalDataIncluded\":false,\"actualBusinessOrder\":false}",
                WorkStableId = "sample-work:" + runStableId + ":" + track.CaseCode,
                LifecycleStageCode = step.StageCode,
                AttentionStateCode = step.Attention,
                ObjectKindCode = track.ObjectKind,
                SemanticPlaceStableId = "synthetic-place:" + step.PlaceCode,
                RelationStableIds = track.Relations.ToArray(),
                SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
                ScenarioRunStableId = runStableId
            };
        }

        private static Track TrackOf(string caseCode, string os, string itemKind,
            string objectKind, string role, Step[] steps, params string[] relations)
            => new Track(caseCode, os, itemKind, objectKind, role, steps, relations);

        private static Step S(int offsetSeconds, string stageCode, string activityCode,
            string placeCode, string attention = OperationalWorldAttentionStateCodes.Active)
            => new Step(offsetSeconds, stageCode, activityCode, placeCode, attention);
    }

    /// <summary>여러 Game View 검증기가 같은 카메라·표현 상태를 동시에 소유하지 않게 합니다.</summary>
    public static class 사가정GameView검증CaptureCoordinator
    {
        private static readonly object Gate = new object();
        private static string activeOwner = string.Empty;

        public static string ActiveOwner
        {
            get { lock (Gate) return activeOwner; }
        }

        public static bool TryAcquire(string owner, out IDisposable lease)
        {
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("SagajeongCaptureOwnerRequired", nameof(owner));
            lock (Gate)
            {
                if (!string.IsNullOrEmpty(activeOwner))
                {
                    lease = null;
                    return false;
                }
                activeOwner = owner;
                lease = new Lease(owner);
                return true;
            }
        }

        private sealed class Lease : IDisposable
        {
            private readonly string owner;
            private bool disposed;

            public Lease(string owner) => this.owner = owner;

            public void Dispose()
            {
                lock (Gate)
                {
                    if (disposed) return;
                    if (string.Equals(activeOwner, owner, StringComparison.Ordinal))
                        activeOwner = string.Empty;
                    disposed = true;
                }
            }
        }
    }
}
