using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Farm;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 정착지상호작용PhaseCodes
    {
        public const string LotSelected = "LotSelected";
        public const string PreviewReady = "PreviewReady";
        public const string TaskReserved = "TaskReserved";
        public const string EffectApplied = "EffectApplied";
        public const string Failed = "Failed";
    }

    public sealed class 정착지상호작용PreviewData
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long ObservedRevision { get; set; }
        public long ObservedWorldTick { get; set; }
        public string ChoiceCode { get; set; } = string.Empty;
        public string NextWorkflowCode { get; set; } = string.Empty;
        public string HarvestLotStableId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public decimal RequiredLabor { get; set; }
        public decimal SimulationCost { get; set; }
        public decimal? ProjectedRevenue { get; set; }
        public int DurationTicks { get; set; }
        public decimal FoodSecurityDaysBefore { get; set; }
        public decimal FoodSecurityDaysCandidate { get; set; }
        public decimal? ExpectedStoredQuantity { get; set; }
        public string PolicyRevision { get; set; } = string.Empty;
        public string[] RiskCodes { get; set; } = Array.Empty<string>();
        public string[] BoundaryCodes { get; set; } = Array.Empty<string>();
        public HarvestDispositionImpactPreviewRequestData Request { get; set; }
            = new HarvestDispositionImpactPreviewRequestData();
    }

    public sealed class 수확LotTaskAuthorityData
    {
        public string HarvestLotStableId { get; set; } = string.Empty;
        public string AllocationStateCode { get; set; } = string.Empty;
        public string TaskStableId { get; set; } = string.Empty;
        public string TaskStateCode { get; set; } = string.Empty;
        public int TaskScheduledStartTick { get; set; }
        public int TaskExpectedEndTick { get; set; }
        public int TaskRemainingTicks { get; set; }
        public string EffectStateCode { get; set; } = string.Empty;

        public 수확LotTaskAuthorityData Clone()
            => (수확LotTaskAuthorityData)MemberwiseClone();
    }

    public sealed class 정착지상호작용AuthoritySnapshot
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public long WorldTick { get; set; }
        public string GameDateLabel { get; set; } = string.Empty;
        public decimal TreasuryBalance { get; set; }
        public decimal TreasuryReserved { get; set; }
        public decimal LaborAvailable { get; set; }
        public decimal LaborReserved { get; set; }
        public decimal MarketFoodSupplyKg { get; set; }
        public decimal ReserveFoodEquivalent { get; set; }
        public decimal StorageOccupied { get; set; }
        public decimal StorageReserved { get; set; }
        public decimal FoodSecurityDays { get; set; }
        public int ActiveTaskCount { get; set; }
        public string AllocationStateCode { get; set; } = string.Empty;
        public string AllocationTaskStableId { get; set; } = string.Empty;
        public string TaskStateCode { get; set; } = string.Empty;
        public int TaskScheduledStartTick { get; set; }
        public int TaskExpectedEndTick { get; set; }
        public int TaskRemainingTicks { get; set; }
        public string EffectStateCode { get; set; } = string.Empty;
        public 수확LotTaskAuthorityData[] HarvestLotTasks { get; set; }
            = Array.Empty<수확LotTaskAuthorityData>();
        public string SourceModeCode { get; set; } = string.Empty;

        public SimulationWorldShellSnapshot ToWorldShellSnapshot()
            => new(
                Required(SessionStableId, "SettlementInteractionSessionMissing"),
                Revision,
                WorldTick,
                Required(GameDateLabel, "SettlementInteractionGameDateMissing"),
                TreasuryBalance,
                LaborAvailable,
                LaborReserved,
                MarketFoodSupplyKg,
                ReserveFoodEquivalent,
                FoodSecurityDays,
                ActiveTaskCount,
                Required(SourceModeCode, "SettlementInteractionSourceModeMissing"),
                new[]
                {
                    new SimulationWorldSettlementNode(
                        SimulationWorldShellFixture.SettlementStableId,
                        new[]
                        {
                            District("district:farm", "harvest-lot:potato-001"),
                            District("district:town"), District("district:market"),
                            District("district:storage"),
                            District("district:logistics", 물류이동Fixture.CargoStableId),
                            District("district:residential"), District("district:garrison"),
                            District("district:gate"),
                        }),
                });

        public 정착지상호작용AuthoritySnapshot Clone()
        {
            var clone = (정착지상호작용AuthoritySnapshot)MemberwiseClone();
            clone.HarvestLotTasks = HarvestLotTasks.Select(value => value.Clone()).ToArray();
            return clone;
        }

        public 정착지상호작용AuthoritySnapshot ForHarvestLot(string harvestLotStableId)
        {
            if (string.IsNullOrWhiteSpace(harvestLotStableId))
                throw new InvalidOperationException("HarvestRouteMappedLotStableIdMissing");
            var task = HarvestLotTasks.SingleOrDefault(value =>
                value.HarvestLotStableId == harvestLotStableId);
            if (task == null)
                throw new InvalidOperationException("HarvestRouteMappedTaskSnapshotMissing");
            var clone = Clone();
            clone.AllocationStateCode = task.AllocationStateCode;
            clone.AllocationTaskStableId = task.TaskStableId;
            clone.TaskStateCode = task.TaskStateCode;
            clone.TaskScheduledStartTick = task.TaskScheduledStartTick;
            clone.TaskExpectedEndTick = task.TaskExpectedEndTick;
            clone.TaskRemainingTicks = task.TaskRemainingTicks;
            clone.EffectStateCode = task.EffectStateCode;
            return clone;
        }

        private static SimulationWorldDistrictNode District(string id, params string[] objectIds)
            => new(id, objectIds);

        private static string Required(string value, string error)
            => !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new InvalidOperationException(error);
    }

    public static class 수확판로결과단계Codes
    {
        public const string NotSelected = "NotSelected";
        public const string DispositionTaskScheduled = "DispositionTaskScheduled";
        public const string CooperativeIntakeCandidate = "CooperativeIntakeCandidate";
        public const string CooperativeCargoReserved = "CooperativeCargoReserved";
        public const string CooperativeCargoInTransit = "CooperativeCargoInTransit";
        public const string CooperativeCargoArrived = "CooperativeCargoArrived";
        public const string DirectMarketSupplyAvailable = "DirectMarketSupplyAvailable";
        public const string ReserveStored = "ReserveStored";
        public const string ExportPreparation = "ExportPreparation";
        public const string ExportCargoPreparation = "ExportCargoPreparation";
        public const string ExportCargoHandoff = "ExportCargoHandoff";
        public const string ExportPortMovement = "ExportPortMovement";
        public const string ExportPortReceipt = "ExportPortReceipt";
        public const string ExportReadinessReview = "ExportReadinessReview";
        public const string ExportShipmentPlan = "ExportShipmentPlan";
        public const string ExportShipmentScheduled = "ExportShipmentScheduled";
        public const string ExportShipmentInTransit = "ExportShipmentInTransit";
        public const string ExportDelivered = "ExportDelivered";
        public const string ExportDisruptedWithLoss = "ExportDisruptedWithLoss";
    }

    public sealed class 수확판로선택지결과Data
    {
        public string ChoiceCode { get; set; } = string.Empty;
        public string SelectionStateCode { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public string CurrentStageCode { get; set; } = string.Empty;
        public string SourceStateCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ResolvedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal MarketSuppliedQuantity { get; set; }
        public decimal StoredQuantity { get; set; }
        public decimal ExportDeliveredQuantity { get; set; }
        public decimal ExportLostQuantity { get; set; }
        public decimal OutboundReservedQuantity { get; set; }
        public decimal RecognizedTreasuryDelta { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string RiskResultCode { get; set; } = string.Empty;
        public string[] RiskCodes { get; set; } = Array.Empty<string>();
        public string[] RelatedStableIds { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 수확판로결과Data
    {
        public string SessionStableId { get; set; } = string.Empty;
        public int WorldTick { get; set; }
        public long WorldRevision { get; set; }
        public string SettlementStableId { get; set; } = string.Empty;
        public string AllocationStableId { get; set; } = string.Empty;
        public string HarvestLotStableId { get; set; } = string.Empty;
        public long HarvestLotRevision { get; set; }
        public string ProductStableId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public string SelectedChoiceCode { get; set; } = string.Empty;
        public string AllocationStateCode { get; set; } = string.Empty;
        public decimal CurrentTreasuryBalance { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CurrentProductMarketSupplyQuantity { get; set; }
        public decimal CurrentProductReserveQuantity { get; set; }
        public 수확판로선택지결과Data[] Routes { get; set; }
            = Array.Empty<수확판로선택지결과Data>();
        public string[] BoundaryCodes { get; set; } = Array.Empty<string>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 수확판로선택지PresentationModel
    {
        public string ChoiceCode { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public string ChoiceText { get; set; } = string.Empty;
        public string SelectionText { get; set; } = string.Empty;
        public string StageText { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public string TreasuryText { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
    }

    public sealed class 수확판로결과PresentationModel
    {
        public string HarvestLotText { get; set; } = string.Empty;
        public string SelectedRouteText { get; set; } = string.Empty;
        public string CurrentStageText { get; set; } = string.Empty;
        public string ResultText { get; set; } = string.Empty;
        public 수확판로선택지PresentationModel[] Routes { get; set; }
            = Array.Empty<수확판로선택지PresentationModel>();
    }

    public sealed class 수확판로결과Projector
    {
        public 수확판로결과PresentationModel Project(수확판로결과Data source)
        {
            Validate(source);
            var routes = source.Routes.Select(ProjectRoute).ToArray();
            var selected = routes.Single(value => value.IsSelected);
            return new 수확판로결과PresentationModel
            {
                HarvestLotText = source.HarvestLotStableId + " · "
                    + Number(source.Quantity) + " " + source.UnitCode,
                SelectedRouteText = selected.ChoiceText + " · " + selected.SelectionText,
                CurrentStageText = selected.StageText,
                ResultText = selected.QuantityText + "\n" + selected.TreasuryText
                    + (string.IsNullOrWhiteSpace(selected.RiskText)
                        ? string.Empty : "\n" + selected.RiskText),
                Routes = routes,
            };
        }

        private static 수확판로선택지PresentationModel ProjectRoute(
            수확판로선택지결과Data route)
            => new()
            {
                ChoiceCode = route.ChoiceCode,
                IsSelected = route.IsSelected,
                ChoiceText = ChoiceLabel(route.ChoiceCode),
                SelectionText = route.IsSelected ? "선택됨" : "선택하지 않음",
                StageText = StageLabel(route.CurrentStageCode),
                QuantityText = QuantityLabel(route),
                TreasuryText = route.IsSelected
                    ? "반영 재정 " + Signed(route.RecognizedTreasuryDelta) + " "
                        + route.CurrencyCode
                    : "재정 반영 없음",
                RiskText = RiskLabel(route.RiskResultCode),
            };

        private static void Validate(수확판로결과Data source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.SessionStableId)
                || source.WorldRevision <= 0 || string.IsNullOrWhiteSpace(source.HarvestLotStableId)
                || source.Quantity <= 0 || string.IsNullOrWhiteSpace(source.UnitCode)
                || source.Routes == null || source.Routes.Length != 4
                || source.Routes.Count(value => value != null && value.IsSelected) != 1
                || source.Routes.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.ChoiceCode)
                    || string.IsNullOrWhiteSpace(value.CurrentStageCode)))
                throw new InvalidOperationException("HarvestRouteOutcomeProjectionInvalid");
        }

        private static string ChoiceLabel(string code)
            => code switch
            {
                HarvestDispositionChoiceCodes.CooperativeShipment => "생산자 조합 출하",
                HarvestDispositionChoiceCodes.DirectOnlineSale => "온라인 직접 판매",
                HarvestDispositionChoiceCodes.ReserveStorage => "정착지 비축 보관",
                HarvestDispositionChoiceCodes.ExportAgent => "외부 교역 준비",
                _ => code,
            };

        private static string StageLabel(string code)
            => code switch
            {
                수확판로결과단계Codes.NotSelected => "선택되지 않은 경로",
                수확판로결과단계Codes.DispositionTaskScheduled => "판로 작업 예약",
                수확판로결과단계Codes.CooperativeIntakeCandidate => "조합 인수 대기",
                수확판로결과단계Codes.CooperativeCargoReserved => "조합 출하 예약",
                수확판로결과단계Codes.CooperativeCargoInTransit => "조합 운송 중",
                수확판로결과단계Codes.CooperativeCargoArrived => "조합 도착",
                수확판로결과단계Codes.DirectMarketSupplyAvailable => "온라인 판매 재고 반영",
                수확판로결과단계Codes.ReserveStored => "비축 창고 보관 완료",
                수확판로결과단계Codes.ExportPreparation => "수출 포장·검사 준비",
                수확판로결과단계Codes.ExportCargoPreparation => "수출 Cargo 준비",
                수확판로결과단계Codes.ExportCargoHandoff => "배송대행지 인계",
                수확판로결과단계Codes.ExportPortMovement => "항만 준비시설 운송 중",
                수확판로결과단계Codes.ExportPortReceipt => "항만 준비시설 인수",
                수확판로결과단계Codes.ExportReadinessReview => "수출 준비성 검토",
                수확판로결과단계Codes.ExportShipmentPlan => "선적 계획 비교",
                수확판로결과단계Codes.ExportShipmentScheduled => "가상 선적 예약",
                수확판로결과단계Codes.ExportShipmentInTransit => "가상 국제 운송 중",
                수확판로결과단계Codes.ExportDelivered => "가상 수출 판매 완료",
                수확판로결과단계Codes.ExportDisruptedWithLoss => "가상 운송 손실 확정",
                _ => code,
            };

        private static string QuantityLabel(수확판로선택지결과Data route)
        {
            if (!route.IsSelected) return "수량 반영 없음";
            if (route.ExportDeliveredQuantity > 0)
                return "도착 " + Number(route.ExportDeliveredQuantity)
                    + " · 잔여 " + Number(route.RemainingQuantity);
            if (route.ExportLostQuantity > 0)
                return "손실 " + Number(route.ExportLostQuantity)
                    + " · 잔여 " + Number(route.RemainingQuantity);
            if (route.StoredQuantity > 0)
                return "비축 " + Number(route.StoredQuantity)
                    + " · 잔여 " + Number(route.RemainingQuantity);
            if (route.MarketSuppliedQuantity > 0)
                return "시장 공급 " + Number(route.MarketSuppliedQuantity)
                    + " · 잔여 " + Number(route.RemainingQuantity);
            if (route.OutboundReservedQuantity > 0)
                return "출고 예약 " + Number(route.OutboundReservedQuantity)
                    + " · 해결 " + Number(route.ResolvedQuantity)
                    + " · 잔여 " + Number(route.RemainingQuantity);
            return "해결 " + Number(route.ResolvedQuantity)
                + " · 잔여 " + Number(route.RemainingQuantity);
        }

        private static string RiskLabel(string code)
            => code switch
            {
                "DeliveredInSimulation" => "Simulation 결과: 도착",
                "DisruptedWithLossInSimulation" => "Simulation 결과: 운송 손실",
                "Pending" => "Simulation 결과 대기",
                "" => string.Empty,
                _ => "위험 결과 " + code,
            };

        private static string Signed(decimal value)
            => (value > 0 ? "+" : string.Empty) + Number(value);

        private static string Number(decimal value)
            => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    public interface I정착지상호작용AuthorityClient
    {
        Task<정착지상호작용AuthoritySnapshot> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken);
        Task<정착지상호작용PreviewData> PreviewAsync(
            string sessionStableId,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken);
        Task<정착지상호작용AuthoritySnapshot> ConfirmAsync(
            string sessionStableId,
            long expectedRevision,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken);
        Task<정착지상호작용AuthoritySnapshot> AdvanceAsync(
            string sessionStableId,
            long expectedRevision,
            int tickCount,
            CancellationToken cancellationToken);
        Task<수확판로결과Data> Get수확판로결과Async(
            string sessionStableId,
            string harvestLotStableId,
            CancellationToken cancellationToken);
        Task<수확판로결과Data[]> Get수확판로결과목록Async(
            string sessionStableId,
            CancellationToken cancellationToken);
    }

    public sealed class 수확LotObjectMappingData
    {
        public 수확LotObjectMappingData(string objectStableId, string harvestLotStableId)
        {
            ObjectStableId = Required(objectStableId, "HarvestRouteObjectStableIdMissing");
            HarvestLotStableId = Required(
                harvestLotStableId, "HarvestRouteMappedLotStableIdMissing");
        }

        public string ObjectStableId { get; }
        public string HarvestLotStableId { get; }

        private static string Required(string value, string error)
            => !string.IsNullOrWhiteSpace(value) ? value.Trim()
                : throw new InvalidOperationException(error);
    }

    public sealed class 수확LotObjectMappingCatalog
    {
        private readonly 수확LotObjectMappingData[] mappings;

        public 수확LotObjectMappingCatalog(params 수확LotObjectMappingData[] values)
        {
            mappings = values?.ToArray() ?? Array.Empty<수확LotObjectMappingData>();
            if (mappings.Any(value => value == null)
                || mappings.Select(value => value.ObjectStableId)
                    .Distinct(StringComparer.Ordinal).Count() != mappings.Length
                || mappings.Select(value => value.HarvestLotStableId)
                    .Distinct(StringComparer.Ordinal).Count() != mappings.Length)
                throw new InvalidOperationException("HarvestRouteObjectLotMappingInvalid");
        }

        public bool TryResolve(string objectStableId, out string harvestLotStableId)
        {
            var mapping = mappings.SingleOrDefault(value =>
                value.ObjectStableId == objectStableId);
            harvestLotStableId = mapping?.HarvestLotStableId ?? string.Empty;
            return mapping != null;
        }
    }

    public sealed class 정착지상호작용Coordinator
    {
        private readonly I정착지상호작용AuthorityClient authority;

        public 정착지상호작용Coordinator(
            I정착지상호작용AuthorityClient authorityClient,
            정착지상호작용AuthoritySnapshot initialSnapshot)
        {
            authority = authorityClient ?? throw new ArgumentNullException(nameof(authorityClient));
            CurrentSnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            PhaseCode = 정착지상호작용PhaseCodes.LotSelected;
        }

        public 정착지상호작용AuthoritySnapshot CurrentSnapshot { get; private set; }
        public 정착지상호작용PreviewData? CurrentPreview { get; private set; }
        public 수확판로결과Data? Current수확판로결과 { get; private set; }
        public 수확판로결과Data[] Current수확판로결과목록 { get; private set; }
            = Array.Empty<수확판로결과Data>();
        public string PhaseCode { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;
        public string 수확판로결과ErrorCode { get; private set; } = string.Empty;
        public bool CanResumeReservedTask =>
            PhaseCode == 정착지상호작용PhaseCodes.TaskReserved
            && HasAuthoritativeReservedTask(CurrentSnapshot);

        public async Task PreviewAsync(
            HarvestDispositionBranchEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            if (!envelope.RequiresServerPreview || !envelope.RequiresExplicitConfirmation
                || !envelope.DoesNotApplySettlementState)
                throw new InvalidOperationException("SettlementInteractionEnvelopeBoundaryInvalid");

            var beforeRevision = CurrentSnapshot.Revision;
            var beforeTick = CurrentSnapshot.WorldTick;
            try
            {
                var preview = await authority.PreviewAsync(
                    CurrentSnapshot.SessionStableId,
                    envelope.PreviewRequest,
                    cancellationToken);
                if (preview.SessionStableId != CurrentSnapshot.SessionStableId
                    || preview.ObservedRevision != beforeRevision
                    || preview.ObservedWorldTick != beforeTick)
                    throw new InvalidOperationException("SettlementInteractionPreviewMutatedSnapshot");
                CurrentPreview = preview;
                PhaseCode = 정착지상호작용PhaseCodes.PreviewReady;
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task ConfirmAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentPreview == null
                || PhaseCode != 정착지상호작용PhaseCodes.PreviewReady)
                throw new InvalidOperationException("SettlementInteractionPreviewRequired");
            try
            {
                var next = await authority.ConfirmAsync(
                    CurrentSnapshot.SessionStableId,
                    CurrentSnapshot.Revision,
                    CurrentPreview.Request,
                    cancellationToken);
                ApplyNewerSnapshot(next, "SettlementInteractionConfirmSnapshotInvalid");
                if (next.AllocationStateCode != "Reserved" || next.ActiveTaskCount <= 0)
                    throw new InvalidOperationException("SettlementInteractionReservationMissing");
                PhaseCode = 정착지상호작용PhaseCodes.TaskReserved;
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task AdvanceToCompletionAsync(CancellationToken cancellationToken = default)
        {
            if (!CanResumeReservedTask)
                throw new InvalidOperationException("SettlementInteractionReservedTaskRequired");
            try
            {
                var next = await authority.AdvanceAsync(
                    CurrentSnapshot.SessionStableId,
                    CurrentSnapshot.Revision,
                    CurrentSnapshot.TaskRemainingTicks,
                    cancellationToken);
                ApplyNewerSnapshot(next, "SettlementInteractionTickSnapshotInvalid");
                if (next.AllocationStateCode != "Applied" || next.ActiveTaskCount != 0)
                    throw new InvalidOperationException("SettlementInteractionEffectNotApplied");
                PhaseCode = 정착지상호작용PhaseCodes.EffectApplied;
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task Refresh수확판로결과Async(
            string harvestLotStableId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await authority.Get수확판로결과Async(
                    CurrentSnapshot.SessionStableId,
                    harvestLotStableId,
                    cancellationToken);
                if (result.SessionStableId != CurrentSnapshot.SessionStableId
                    || result.HarvestLotStableId != harvestLotStableId
                    || result.WorldRevision != CurrentSnapshot.Revision
                    || result.WorldTick != CurrentSnapshot.WorldTick)
                    throw new InvalidOperationException("HarvestRouteOutcomeSnapshotMismatch");
                Current수확판로결과 = result;
                수확판로결과ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                수확판로결과ErrorCode = error.Message;
                throw;
            }
        }

        public async Task Refresh수확판로결과목록Async(
            string? mappedHarvestLotStableId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var latest = await authority.RefreshAsync(
                    CurrentSnapshot.SessionStableId,
                    cancellationToken);
                ValidateRefreshedSnapshot(latest);
                var results = await authority.Get수확판로결과목록Async(
                    CurrentSnapshot.SessionStableId,
                    cancellationToken);
                ValidateOutcomeList(latest, results);

                var requestedLotStableId = !string.IsNullOrWhiteSpace(mappedHarvestLotStableId)
                    ? mappedHarvestLotStableId.Trim()
                    : CurrentPreview?.HarvestLotStableId
                        ?? Current수확판로결과?.HarvestLotStableId
                        ?? (results.Length == 1 ? results[0].HarvestLotStableId : string.Empty);
                var selected = string.IsNullOrWhiteSpace(requestedLotStableId)
                    ? null
                    : results.SingleOrDefault(value =>
                        value.HarvestLotStableId == requestedLotStableId)
                        ?? throw new InvalidOperationException("HarvestRouteMappedOutcomeMissing");
                var scopedLatest = selected != null && latest.HarvestLotTasks.Length > 0
                    ? latest.ForHarvestLot(selected.HarvestLotStableId)
                    : latest;
                var nextPhase = ResolvePhaseFromSnapshot(scopedLatest);
                CurrentSnapshot = scopedLatest;
                Current수확판로결과목록 = results;
                Current수확판로결과 = selected;
                PhaseCode = nextPhase;
                ErrorCode = string.Empty;
                수확판로결과ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                수확판로결과ErrorCode = error.Message;
                throw;
            }
        }

        public void Select수확판로결과(string harvestLotStableId)
        {
            if (string.IsNullOrWhiteSpace(harvestLotStableId))
                throw new InvalidOperationException("HarvestRouteMappedLotStableIdMissing");
            var selected = Current수확판로결과목록.SingleOrDefault(value =>
                value.HarvestLotStableId == harvestLotStableId.Trim())
                ?? throw new InvalidOperationException("HarvestRouteMappedOutcomeMissing");
            var scoped = CurrentSnapshot.HarvestLotTasks.Length > 0
                ? CurrentSnapshot.ForHarvestLot(selected.HarvestLotStableId)
                : CurrentSnapshot;
            var nextPhase = ResolvePhaseFromSnapshot(scoped);
            CurrentSnapshot = scoped;
            Current수확판로결과 = selected;
            PhaseCode = nextPhase;
            ErrorCode = string.Empty;
        }

        private void ValidateRefreshedSnapshot(정착지상호작용AuthoritySnapshot latest)
        {
            if (latest == null || latest.SessionStableId != CurrentSnapshot.SessionStableId
                || latest.Revision < CurrentSnapshot.Revision
                || latest.WorldTick < CurrentSnapshot.WorldTick)
                throw new InvalidOperationException("HarvestRouteRefreshSnapshotInvalid");
        }

        private static void ValidateOutcomeList(
            정착지상호작용AuthoritySnapshot latest,
            수확판로결과Data[] results)
        {
            if (results == null
                || results.Any(value => value == null
                    || value.SessionStableId != latest.SessionStableId
                    || value.WorldRevision != latest.Revision
                    || value.WorldTick != latest.WorldTick)
                || results.Select(value => value.HarvestLotStableId)
                    .Distinct(StringComparer.Ordinal).Count() != results.Length
                || (latest.HarvestLotTasks.Length > 0
                    && results.Any(value => !latest.HarvestLotTasks.Any(task =>
                        task.HarvestLotStableId == value.HarvestLotStableId)))
                || (!string.IsNullOrWhiteSpace(latest.AllocationStateCode)
                    && results.Length == 0))
                throw new InvalidOperationException("HarvestRouteRefreshOutcomeListInvalid");
        }

        private static string ResolvePhaseFromSnapshot(
            정착지상호작용AuthoritySnapshot snapshot)
        {
            if (snapshot.AllocationStateCode == "Reserved"
                && !HasAuthoritativeReservedTask(snapshot))
                throw new InvalidOperationException("HarvestRouteReservedTaskSnapshotInvalid");
            return snapshot.AllocationStateCode switch
            {
                "Reserved" => 정착지상호작용PhaseCodes.TaskReserved,
                "Applied" => 정착지상호작용PhaseCodes.EffectApplied,
                _ => 정착지상호작용PhaseCodes.LotSelected,
            };
        }

        private static bool HasAuthoritativeReservedTask(
            정착지상호작용AuthoritySnapshot snapshot)
            => snapshot.AllocationStateCode == "Reserved"
                && snapshot.ActiveTaskCount > 0
                && !string.IsNullOrWhiteSpace(snapshot.AllocationTaskStableId)
                && (snapshot.TaskStateCode == "Scheduled"
                    || snapshot.TaskStateCode == "InProgress")
                && snapshot.TaskScheduledStartTick <= snapshot.TaskExpectedEndTick
                && snapshot.TaskExpectedEndTick > snapshot.WorldTick
                && snapshot.TaskRemainingTicks
                    == snapshot.TaskExpectedEndTick - snapshot.WorldTick;

        private void ApplyNewerSnapshot(
            정착지상호작용AuthoritySnapshot next,
            string error)
        {
            if (next == null || next.SessionStableId != CurrentSnapshot.SessionStableId
                || next.Revision <= CurrentSnapshot.Revision
                || next.WorldTick < CurrentSnapshot.WorldTick)
                throw new InvalidOperationException(error);
            CurrentSnapshot = next;
        }

        private void Fail(Exception error)
        {
            PhaseCode = 정착지상호작용PhaseCodes.Failed;
            ErrorCode = error.Message;
        }
    }

    /// <summary>
    /// Visual proof test double. Production flow uses the HTTP authority client.
    /// It never represents operational sales, shipping or settlement.
    /// </summary>
    public sealed class 정착지상호작용FixtureAuthorityClient
        : I정착지상호작용AuthorityClient
    {
        private 정착지상호작용AuthoritySnapshot snapshot;
        private 정착지상호작용PreviewData? preview;
        private readonly 수확판로결과Data[] existingOutcomes;

        public 정착지상호작용FixtureAuthorityClient(
            정착지상호작용AuthoritySnapshot initialSnapshot,
            params 수확판로결과Data[] outcomes)
        {
            snapshot = initialSnapshot?.Clone()
                ?? throw new ArgumentNullException(nameof(initialSnapshot));
            existingOutcomes = outcomes?.Select(CloneOutcome).ToArray()
                ?? Array.Empty<수확판로결과Data>();
        }

        public Task<정착지상호작용AuthoritySnapshot> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            ValidateSession(sessionStableId);
            return Task.FromResult(snapshot.Clone());
        }

        public Task<정착지상호작용PreviewData> PreviewAsync(
            string sessionStableId,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken)
        {
            ValidateSession(sessionStableId);
            var policy = Policy(request.ChoiceCode, request.NextWorkflowCode);
            preview = new 정착지상호작용PreviewData
            {
                SessionStableId = snapshot.SessionStableId,
                ObservedRevision = snapshot.Revision,
                ObservedWorldTick = snapshot.WorldTick,
                ChoiceCode = request.ChoiceCode,
                NextWorkflowCode = request.NextWorkflowCode,
                HarvestLotStableId = request.HarvestLotStableId,
                Quantity = request.Quantity,
                UnitCode = request.UnitCode,
                RequiredLabor = policy.labor,
                SimulationCost = policy.cost,
                ProjectedRevenue = policy.revenue,
                DurationTicks = policy.duration,
                FoodSecurityDaysBefore = snapshot.FoodSecurityDays,
                FoodSecurityDaysCandidate = request.ChoiceCode == HarvestDispositionChoiceCodes.ReserveStorage
                    ? 12.94m : snapshot.FoodSecurityDays,
                ExpectedStoredQuantity = request.ChoiceCode == HarvestDispositionChoiceCodes.ReserveStorage
                    ? 294m : null,
                PolicyRevision = "harvest-impact:fixture-r1",
                RiskCodes = new[] { "SimulationOutcomeOnly" },
                BoundaryCodes = new[] { "NoOperationalSale", "NoOperationalShipping" },
                Request = request,
            };
            return Task.FromResult(preview);
        }

        public Task<정착지상호작용AuthoritySnapshot> ConfirmAsync(
            string sessionStableId,
            long expectedRevision,
            HarvestDispositionImpactPreviewRequestData request,
            CancellationToken cancellationToken)
        {
            ValidateSession(sessionStableId);
            ValidateRevision(expectedRevision);
            if (preview == null || preview.ChoiceCode != request.ChoiceCode)
                throw new InvalidOperationException("SettlementInteractionFixturePreviewRequired");
            if (snapshot.LaborAvailable < preview.RequiredLabor)
                throw new InvalidOperationException("SimulationSettlementLaborCapacityExceeded");
            if (snapshot.TreasuryBalance - snapshot.TreasuryReserved < preview.SimulationCost)
                throw new InvalidOperationException("SimulationSettlementTreasuryCapacityExceeded");

            snapshot.Revision += 1;
            snapshot.LaborAvailable -= preview.RequiredLabor;
            snapshot.LaborReserved += preview.RequiredLabor;
            snapshot.TreasuryReserved += preview.SimulationCost;
            snapshot.StorageReserved = preview.ExpectedStoredQuantity ?? 0m;
            snapshot.ActiveTaskCount = 1;
            snapshot.AllocationStateCode = "Reserved";
            snapshot.AllocationTaskStableId = "task:fixture.harvest-route";
            snapshot.TaskStateCode = "Scheduled";
            snapshot.TaskScheduledStartTick = (int)snapshot.WorldTick + 1;
            snapshot.TaskExpectedEndTick = (int)snapshot.WorldTick + preview.DurationTicks;
            snapshot.TaskRemainingTicks = preview.DurationTicks;
            snapshot.EffectStateCode = "Pending";
            SyncFixtureLotTask();
            return Task.FromResult(snapshot.Clone());
        }

        public Task<정착지상호작용AuthoritySnapshot> AdvanceAsync(
            string sessionStableId,
            long expectedRevision,
            int tickCount,
            CancellationToken cancellationToken)
        {
            ValidateSession(sessionStableId);
            ValidateRevision(expectedRevision);
            if (preview == null || snapshot.AllocationStateCode != "Reserved")
                throw new InvalidOperationException("SettlementInteractionFixtureReservationRequired");
            if (tickCount <= 0 || tickCount > snapshot.TaskRemainingTicks)
                throw new InvalidOperationException("SettlementInteractionFixtureTickCountInvalid");

            snapshot.WorldTick += tickCount;
            snapshot.Revision += tickCount;
            snapshot.GameDateLabel = "Year 1 · 04-" + (12 + snapshot.WorldTick - 12).ToString("00");
            snapshot.TaskRemainingTicks -= tickCount;
            if (snapshot.TaskRemainingTicks > 0)
            {
                snapshot.TaskStateCode = "InProgress";
                SyncFixtureLotTask();
                return Task.FromResult(snapshot.Clone());
            }
            snapshot.TreasuryBalance -= preview.SimulationCost;
            if (preview.ProjectedRevenue.HasValue)
                snapshot.TreasuryBalance += preview.ProjectedRevenue.Value;
            snapshot.TreasuryReserved = 0m;
            snapshot.LaborAvailable += preview.RequiredLabor;
            snapshot.LaborReserved -= preview.RequiredLabor;
            if (preview.ChoiceCode == HarvestDispositionChoiceCodes.DirectOnlineSale)
                snapshot.MarketFoodSupplyKg += preview.Quantity;
            if (preview.ChoiceCode == HarvestDispositionChoiceCodes.ReserveStorage)
            {
                snapshot.StorageOccupied += preview.ExpectedStoredQuantity ?? 0m;
                snapshot.ReserveFoodEquivalent += 352.8m;
                snapshot.FoodSecurityDays = preview.FoodSecurityDaysCandidate;
            }
            snapshot.StorageReserved = 0m;
            snapshot.ActiveTaskCount = 0;
            snapshot.AllocationStateCode = "Applied";
            snapshot.TaskStateCode = "Completed";
            snapshot.EffectStateCode = "Applied";
            SyncFixtureLotTask();
            return Task.FromResult(snapshot.Clone());
        }

        public Task<수확판로결과Data> Get수확판로결과Async(
            string sessionStableId,
            string harvestLotStableId,
            CancellationToken cancellationToken)
        {
            ValidateSession(sessionStableId);
            var existing = existingOutcomes.SingleOrDefault(value =>
                value.HarvestLotStableId == harvestLotStableId);
            if (existing != null) return Task.FromResult(CloneOutcome(existing));
            if (preview == null || snapshot.AllocationStateCode.Length == 0
                || harvestLotStableId != preview.HarvestLotStableId)
                throw new InvalidOperationException("SimulationHarvestRouteOutcomeNotFound");
            var routes = new[]
            {
                Route(HarvestDispositionChoiceCodes.CooperativeShipment),
                Route(HarvestDispositionChoiceCodes.DirectOnlineSale),
                Route(HarvestDispositionChoiceCodes.ReserveStorage),
                Route(HarvestDispositionChoiceCodes.ExportAgent),
            };
            return Task.FromResult(new 수확판로결과Data
            {
                SessionStableId = snapshot.SessionStableId,
                WorldTick = (int)snapshot.WorldTick,
                WorldRevision = snapshot.Revision,
                SettlementStableId = SimulationWorldShellFixture.SettlementStableId,
                AllocationStableId = "allocation:fixture.harvest-route",
                HarvestLotStableId = harvestLotStableId,
                HarvestLotRevision = 1,
                ProductStableId = "product:potato",
                Quantity = preview.Quantity,
                UnitCode = preview.UnitCode,
                SelectedChoiceCode = preview.ChoiceCode,
                AllocationStateCode = snapshot.AllocationStateCode,
                CurrentTreasuryBalance = snapshot.TreasuryBalance,
                CurrencyCode = "KRW",
                CurrentProductMarketSupplyQuantity = snapshot.MarketFoodSupplyKg,
                CurrentProductReserveQuantity = snapshot.ReserveFoodEquivalent,
                Routes = routes,
                BoundaryCodes = new[] { "ProjectionOnly", "NoStateMutation", "NoOperationalEffect" },
                SourceStableIds = new[] { harvestLotStableId, "source:fixture.harvest-route-outcome" },
            });
        }

        public async Task<수확판로결과Data[]> Get수확판로결과목록Async(
            string sessionStableId,
            CancellationToken cancellationToken)
        {
            ValidateSession(sessionStableId);
            if (existingOutcomes.Length > 0)
                return existingOutcomes.Select(CloneOutcome).ToArray();
            if (preview == null || snapshot.AllocationStateCode.Length == 0)
                return Array.Empty<수확판로결과Data>();
            return new[]
            {
                await Get수확판로결과Async(
                    sessionStableId,
                    preview.HarvestLotStableId,
                    cancellationToken),
            };
        }

        public static 정착지상호작용AuthoritySnapshot CreateInitialSnapshot()
            => new()
            {
                SessionStableId = SimulationWorldShellFixture.SessionStableId,
                Revision = 12,
                WorldTick = 12,
                GameDateLabel = "Year 1 · 04-12",
                TreasuryBalance = 1_000_000m,
                LaborAvailable = 75m,
                LaborReserved = 25m,
                MarketFoodSupplyKg = 300m,
                ReserveFoodEquivalent = 1200m,
                StorageOccupied = 1200m,
                FoodSecurityDays = 10m,
                ActiveTaskCount = 0,
                SourceModeCode = "SimulationFixtureAuthority",
            };

        private void ValidateSession(string sessionStableId)
        {
            if (sessionStableId != snapshot.SessionStableId)
                throw new InvalidOperationException("SimulationSessionNotFound");
        }

        private void ValidateRevision(long expectedRevision)
        {
            if (expectedRevision != snapshot.Revision)
                throw new InvalidOperationException("SimulationExpectedRevisionConflict");
        }

        private void SyncFixtureLotTask()
        {
            if (preview == null) return;
            snapshot.HarvestLotTasks = new[]
            {
                new 수확LotTaskAuthorityData
                {
                    HarvestLotStableId = preview.HarvestLotStableId,
                    AllocationStateCode = snapshot.AllocationStateCode,
                    TaskStableId = snapshot.AllocationTaskStableId,
                    TaskStateCode = snapshot.TaskStateCode,
                    TaskScheduledStartTick = snapshot.TaskScheduledStartTick,
                    TaskExpectedEndTick = snapshot.TaskExpectedEndTick,
                    TaskRemainingTicks = snapshot.TaskRemainingTicks,
                    EffectStateCode = snapshot.EffectStateCode,
                },
            };
        }

        private static (decimal labor, decimal cost, decimal? revenue, int duration) Policy(
            string choiceCode, string workflowCode)
        {
            var expected = HarvestDispositionWorkflowCodes.ForChoice(choiceCode);
            if (workflowCode != expected)
                throw new InvalidOperationException("SimulationHarvestDispositionWorkflowMismatch");
            return choiceCode switch
            {
                HarvestDispositionChoiceCodes.CooperativeShipment => (8m, 30_000m, 240_000m, 2),
                HarvestDispositionChoiceCodes.DirectOnlineSale => (18m, 60_000m, 360_000m, 3),
                HarvestDispositionChoiceCodes.ExportAgent => (24m, 90_000m, 450_000m, 4),
                HarvestDispositionChoiceCodes.ReserveStorage => (6m, 15_000m, null, 1),
                _ => throw new InvalidOperationException("SimulationHarvestDispositionChoiceUnknown"),
            };
        }

        private 수확판로선택지결과Data Route(string choiceCode)
        {
            var selected = preview != null && preview.ChoiceCode == choiceCode;
            if (!selected)
                return new 수확판로선택지결과Data
                {
                    ChoiceCode = choiceCode,
                    SelectionStateCode = "NotSelected",
                    CurrentStageCode = 수확판로결과단계Codes.NotSelected,
                    Quantity = preview?.Quantity ?? 300m,
                    CurrencyCode = "KRW",
                };
            var applied = snapshot.AllocationStateCode == "Applied";
            var stage = !applied
                ? 수확판로결과단계Codes.DispositionTaskScheduled
                : choiceCode switch
                {
                    HarvestDispositionChoiceCodes.CooperativeShipment =>
                        수확판로결과단계Codes.CooperativeIntakeCandidate,
                    HarvestDispositionChoiceCodes.DirectOnlineSale =>
                        수확판로결과단계Codes.DirectMarketSupplyAvailable,
                    HarvestDispositionChoiceCodes.ReserveStorage =>
                        수확판로결과단계Codes.ReserveStored,
                    HarvestDispositionChoiceCodes.ExportAgent =>
                        수확판로결과단계Codes.ExportPreparation,
                    _ => 수확판로결과단계Codes.DispositionTaskScheduled,
                };
            var resolved = applied && (choiceCode == HarvestDispositionChoiceCodes.DirectOnlineSale
                || choiceCode == HarvestDispositionChoiceCodes.ReserveStorage)
                ? choiceCode == HarvestDispositionChoiceCodes.ReserveStorage ? 294m : preview!.Quantity
                : 0m;
            var delta = applied
                ? (preview!.ProjectedRevenue ?? 0m) - preview.SimulationCost
                : 0m;
            return new 수확판로선택지결과Data
            {
                ChoiceCode = choiceCode,
                SelectionStateCode = "Selected",
                IsSelected = true,
                CurrentStageCode = stage,
                SourceStateCode = snapshot.AllocationStateCode,
                Quantity = preview!.Quantity,
                ResolvedQuantity = resolved,
                RemainingQuantity = preview.Quantity - resolved,
                MarketSuppliedQuantity = applied
                    && choiceCode == HarvestDispositionChoiceCodes.DirectOnlineSale
                    ? preview.Quantity : 0m,
                StoredQuantity = applied && choiceCode == HarvestDispositionChoiceCodes.ReserveStorage
                    ? 294m : 0m,
                OutboundReservedQuantity = applied
                    && choiceCode == HarvestDispositionChoiceCodes.ExportAgent
                    ? preview.Quantity : 0m,
                RecognizedTreasuryDelta = delta,
                CurrencyCode = "KRW",
                RiskResultCode = string.Empty,
                RiskCodes = new[] { "SimulationOutcomeOnly" },
                RelatedStableIds = new[] { "allocation:fixture.harvest-route" },
                SourceStableIds = new[] { preview.HarvestLotStableId },
            };
        }

        private static 수확판로결과Data CloneOutcome(수확판로결과Data source)
            => new()
            {
                SessionStableId = source.SessionStableId,
                WorldTick = source.WorldTick,
                WorldRevision = source.WorldRevision,
                SettlementStableId = source.SettlementStableId,
                AllocationStableId = source.AllocationStableId,
                HarvestLotStableId = source.HarvestLotStableId,
                HarvestLotRevision = source.HarvestLotRevision,
                ProductStableId = source.ProductStableId,
                Quantity = source.Quantity,
                UnitCode = source.UnitCode,
                SelectedChoiceCode = source.SelectedChoiceCode,
                AllocationStateCode = source.AllocationStateCode,
                CurrentTreasuryBalance = source.CurrentTreasuryBalance,
                CurrencyCode = source.CurrencyCode,
                CurrentProductMarketSupplyQuantity = source.CurrentProductMarketSupplyQuantity,
                CurrentProductReserveQuantity = source.CurrentProductReserveQuantity,
                Routes = source.Routes.Select(CloneRoute).ToArray(),
                BoundaryCodes = source.BoundaryCodes.ToArray(),
                SourceStableIds = source.SourceStableIds.ToArray(),
            };

        private static 수확판로선택지결과Data CloneRoute(수확판로선택지결과Data source)
            => new()
            {
                ChoiceCode = source.ChoiceCode,
                SelectionStateCode = source.SelectionStateCode,
                IsSelected = source.IsSelected,
                CurrentStageCode = source.CurrentStageCode,
                SourceStateCode = source.SourceStateCode,
                Quantity = source.Quantity,
                ResolvedQuantity = source.ResolvedQuantity,
                RemainingQuantity = source.RemainingQuantity,
                MarketSuppliedQuantity = source.MarketSuppliedQuantity,
                StoredQuantity = source.StoredQuantity,
                ExportDeliveredQuantity = source.ExportDeliveredQuantity,
                ExportLostQuantity = source.ExportLostQuantity,
                OutboundReservedQuantity = source.OutboundReservedQuantity,
                RecognizedTreasuryDelta = source.RecognizedTreasuryDelta,
                CurrencyCode = source.CurrencyCode,
                RiskResultCode = source.RiskResultCode,
                RiskCodes = source.RiskCodes.ToArray(),
                RelatedStableIds = source.RelatedStableIds.ToArray(),
                SourceStableIds = source.SourceStableIds.ToArray(),
            };
    }

    public static class 정착지상호작용BranchFixture
    {
        public static HarvestDispositionBranchEnvelope CreateEnvelope(string choiceCode)
        {
            var cultivationValidator = new 감자재배LifecycleSimulationValidator(
                new FarmSoilTileSimulationValidator(), new 재배달력ProfileValidator());
            var cultivation = new 감자재배LifecycleSimulationEngine(cultivationValidator);
            var crop = 감자재배LifecycleSimulationFixture.Create();
            var tile = crop.Soil.Tiles.First(value =>
                value.CultivationStateCode == FarmSoilTileCultivationStateCodes.Tilled);
            crop = cultivation.Tick(crop,
                cultivation.Confirm(crop, cultivation.PreviewSowing(crop, tile.StableId)));
            crop = cultivation.Tick(crop, cultivation.CreateAdvanceDaysCommand(crop, 6));
            crop = cultivation.Tick(crop,
                cultivation.Confirm(crop, cultivation.PreviewHarvest(crop)));

            var validator = new HarvestDispositionSimulationValidator();
            var engine = new HarvestDispositionSimulationEngine(validator);
            var disposition = HarvestDispositionSimulationFixture.Create(crop);
            disposition = engine.Tick(disposition,
                engine.Confirm(disposition, engine.Preview(disposition, choiceCode)));
            return new HarvestDispositionBranchAdapter(validator)
                .CreatePreviewEnvelope(disposition, "actor:sim.farmer-1");
        }
    }
}
