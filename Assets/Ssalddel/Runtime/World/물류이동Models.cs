using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 물류이동PhaseCodes
    {
        public const string CargoSelected = "CargoSelected";
        public const string PreviewReady = "PreviewReady";
        public const string Reserved = "Reserved";
        public const string InTransit = "InTransit";
        public const string Arrived = "Arrived";
        public const string Failed = "Failed";
    }

    [Serializable]
    public sealed class 물류이동PreviewRequestData
    {
        public string CargoStableId = string.Empty;
        public long CargoRevision;
        public string SourceAllocationStableId = string.Empty;
        public string HarvestLotStableId = string.Empty;
        public string PackageLotStableId = string.Empty;
        public string ProductStableId = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
        public string RouteStableId = string.Empty;
        public string OriginFacilityStableId = string.Empty;
        public string DestinationFacilityStableId = string.Empty;
        public string ActorStableId = string.Empty;
        public int RequiredRouteTicks;
        public string[] SourceStableIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class 화물배차후보RequestData
    {
        public string CarrierCandidateStableId = string.Empty;
        public string VehicleStableId = string.Empty;
        public bool IsFreightApp;
        public bool IsVehicleActive;
        public bool IsDriverOperating;
        public bool WasPreviouslyRejected;
        public decimal? LocationAgeMinutes;
        public decimal? PickupDistanceKm;
        public decimal? PickupAllowedRadiusKm;
        public decimal VehicleCapacity;
        public string VehicleCapacityUnitCode = string.Empty;
        public bool IsVehicleCompatible;
        public string[] VehicleBlockReasonCodes = Array.Empty<string>();
        public decimal DriverWaitingMinutes;
        public bool? CanCompleteSchedule;
        public bool? CanInsertSchedule;
        public bool HasRouteChangeBenefit;
        public decimal? EstimatedExtraProfit;
        public decimal? AdditionalDelayMinutes;
        public string RecommendationTypeCode = "single";
        public bool IsCargoSensitive;
        public decimal? ReturnDetourDistanceKm;
        public bool UsesReturnDestination;
        public string BaseReason = string.Empty;
    }

    [Serializable]
    public sealed class 화물배차RequestData
    {
        public string TransportRequestStableId = string.Empty;
        public decimal LocationFreshnessMinutes = 10m;
        public decimal BasePickupRadiusKm = 5m;
        public decimal MaximumRemotePickupRadiusKm = 30m;
        public decimal RemotePickupAverageSpeedKmH = 40m;
        public decimal RemotePickupArrivalBufferMinutes = 10m;
        public decimal? PickupWindowRemainingMinutes;
        public string? ExcludedCarrierCandidateStableId;
        public 화물배차후보RequestData[] Candidates = Array.Empty<화물배차후보RequestData>();
        public string[] SourceStableIds = Array.Empty<string>();
    }

    public sealed class 화물배차후보평가Data
    {
        public string CarrierCandidateStableId { get; set; } = string.Empty;
        public string VehicleStableId { get; set; } = string.Empty;
        public bool IsEligible { get; set; }
        public bool IsRecommended { get; set; }
        public bool IsSelected { get; set; }
        public int Rank { get; set; }
        public decimal? PickupDistanceKm { get; set; }
        public decimal VehicleCapacity { get; set; }
        public string VehicleCapacityUnitCode { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string[] BlockReasonCodes { get; set; } = Array.Empty<string>();
        public decimal BaseScore { get; set; }
        public decimal DriverWaitingScore { get; set; }
        public decimal TotalScore { get; set; }
    }

    public sealed class 물류이동PreviewData
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long ObservedRevision { get; set; }
        public long ObservedWorldTick { get; set; }
        public string CargoStableId { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public int RequiredRouteTicks { get; set; }
        public string DestinationStockCandidateStableId { get; set; } = string.Empty;
        public string TransportRequestStableId { get; set; } = string.Empty;
        public string DispatchOfferStableId { get; set; } = string.Empty;
        public string RecommendedCarrierCandidateStableId { get; set; } = string.Empty;
        public string DispatchRuleRevision { get; set; } = string.Empty;
        public 화물배차후보평가Data[] CandidateEvaluations { get; set; }
            = Array.Empty<화물배차후보평가Data>();
        public string[] BoundaryCodes { get; set; } = Array.Empty<string>();
        public 물류이동PreviewRequestData Request { get; set; } = new();
    }

    public sealed class 물류이동AuthoritySnapshot
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public long WorldTick { get; set; }
        public string GameDateLabel { get; set; } = string.Empty;
        public string CargoStableId { get; set; } = string.Empty;
        public string MovementStateCode { get; set; } = string.Empty;
        public string TaskStateCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal SourceAvailableQuantity { get; set; }
        public int CompletedRouteTicks { get; set; }
        public int RequiredRouteTicks { get; set; }
        public string RouteStableId { get; set; } = string.Empty;
        public string DestinationStockCandidateStableId { get; set; } = string.Empty;
        public string TransportRequestStableId { get; set; } = string.Empty;
        public string DispatchStateCode { get; set; } = string.Empty;
        public string CarrierCandidateStableId { get; set; } = string.Empty;
        public string VehicleStableId { get; set; } = string.Empty;
        public string DispatchRuleRevision { get; set; } = string.Empty;
        public string SourceModeCode { get; set; } = string.Empty;
        public 정착지상호작용AuthoritySnapshot Settlement { get; set; } = new();

        public 물류이동AuthoritySnapshot Clone()
            => (물류이동AuthoritySnapshot)MemberwiseClone();
    }

    public interface I물류이동AuthorityClient
    {
        Task<물류이동PreviewData> PreviewAsync(
            string sessionStableId,
            물류이동PreviewRequestData request,
            CancellationToken cancellationToken);
        Task<물류이동AuthoritySnapshot> ConfirmAsync(
            string sessionStableId,
            long expectedRevision,
            물류이동PreviewData preview,
            CancellationToken cancellationToken);
        Task<물류이동AuthoritySnapshot> AdvanceAsync(
            string sessionStableId,
            long expectedRevision,
            CancellationToken cancellationToken);
    }

    public sealed class 물류이동Coordinator
    {
        private readonly I물류이동AuthorityClient authority;

        public 물류이동Coordinator(
            I물류이동AuthorityClient authorityClient,
            물류이동AuthoritySnapshot initialSnapshot)
        {
            authority = authorityClient ?? throw new ArgumentNullException(nameof(authorityClient));
            CurrentSnapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            PhaseCode = 물류이동PhaseCodes.CargoSelected;
        }

        public 물류이동AuthoritySnapshot CurrentSnapshot { get; private set; }
        public 물류이동PreviewData? CurrentPreview { get; private set; }
        public string PhaseCode { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;

        public async Task PreviewAsync(CancellationToken cancellationToken = default)
        {
            var before = CurrentSnapshot;
            try
            {
                var preview = await authority.PreviewAsync(
                    before.SessionStableId,
                    물류이동Fixture.CreateRequest(),
                    cancellationToken);
                if (preview.SessionStableId != before.SessionStableId
                    || preview.ObservedRevision != before.Revision
                    || preview.ObservedWorldTick != before.WorldTick)
                    throw new InvalidOperationException("LogisticsMovementPreviewMutatedSnapshot");
                CurrentPreview = preview;
                PhaseCode = 물류이동PhaseCodes.PreviewReady;
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
            if (CurrentPreview == null || PhaseCode != 물류이동PhaseCodes.PreviewReady)
                throw new InvalidOperationException("LogisticsMovementPreviewRequired");
            try
            {
                var next = await authority.ConfirmAsync(
                    CurrentSnapshot.SessionStableId,
                    CurrentSnapshot.Revision,
                    CurrentPreview,
                    cancellationToken);
                Apply(next);
                if (next.MovementStateCode != "Reserved" || next.ReservedQuantity != next.Quantity)
                    throw new InvalidOperationException("LogisticsMovementReservationMissing");
                PhaseCode = 물류이동PhaseCodes.Reserved;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task AdvanceAsync(CancellationToken cancellationToken = default)
        {
            if (PhaseCode != 물류이동PhaseCodes.Reserved
                && PhaseCode != 물류이동PhaseCodes.InTransit)
                throw new InvalidOperationException("LogisticsMovementActiveTaskRequired");
            try
            {
                var next = await authority.AdvanceAsync(
                    CurrentSnapshot.SessionStableId,
                    CurrentSnapshot.Revision,
                    cancellationToken);
                Apply(next);
                PhaseCode = next.MovementStateCode == "ArrivedAtDestination"
                    ? 물류이동PhaseCodes.Arrived
                    : 물류이동PhaseCodes.InTransit;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        private void Apply(물류이동AuthoritySnapshot next)
        {
            if (next.SessionStableId != CurrentSnapshot.SessionStableId
                || next.Revision <= CurrentSnapshot.Revision
                || next.WorldTick < CurrentSnapshot.WorldTick)
                throw new InvalidOperationException("LogisticsMovementAuthoritySnapshotInvalid");
            CurrentSnapshot = next;
            ErrorCode = string.Empty;
        }

        private void Fail(Exception error)
        {
            ErrorCode = error.Message;
            PhaseCode = 물류이동PhaseCodes.Failed;
        }
    }

    public static class 물류이동Fixture
    {
        public const string CargoStableId = "cargo:sim.potato-1";

        public static 물류이동PreviewRequestData CreateRequest()
            => new()
            {
                CargoStableId = CargoStableId,
                CargoRevision = 1,
                SourceAllocationStableId = "allocation:harvest-lot:harvest-lot:potato-1",
                HarvestLotStableId = "harvest-lot:potato-1",
                PackageLotStableId = "package-lot:potato-1",
                ProductStableId = "product:potato",
                Quantity = 300m,
                UnitCode = "KGM",
                RouteStableId = "route:sim.farm-hub-1",
                OriginFacilityStableId = "facility:sim.farm-packing-1",
                DestinationFacilityStableId = "facility:sim.regional-hub-1",
                ActorStableId = "actor:sim.farmer-1",
                RequiredRouteTicks = 3,
                SourceStableIds = new[]
                {
                    "harvest-lot:potato-1", "package-lot:potato-1", "source:fixture.cargo-1",
                },
            };
    }

    public static class 화물배차Fixture
    {
        public const string TransportRequestStableId = "freight-transport:sim.potato-1";
        public const string RecommendedCandidateStableId = "carrier-candidate:sim.waiting-truck";

        public static 화물배차RequestData CreateRequest()
            => new()
            {
                TransportRequestStableId = TransportRequestStableId,
                PickupWindowRemainingMinutes = 60m,
                SourceStableIds = new[] { "source:fixture.unity-freight-dispatch-1" },
                Candidates = new[]
                {
                    Candidate("carrier-candidate:sim.small-van", "vehicle:sim.van-small",
                        200m, 2m, 1m, 10m, "가까운 소형 밴"),
                    Candidate("carrier-candidate:sim.stale-truck", "vehicle:sim.truck-stale",
                        400m, 3m, 30m, 30m, "위치 확인이 필요한 트럭"),
                    Candidate(RecommendedCandidateStableId, "vehicle:sim.truck-fresh",
                        400m, 6m, 2m, 90m, "대기 중인 지역 트럭"),
                },
            };

        private static 화물배차후보RequestData Candidate(
            string candidateStableId,
            string vehicleStableId,
            decimal capacity,
            decimal distanceKm,
            decimal locationAgeMinutes,
            decimal waitingMinutes,
            string reason)
            => new()
            {
                CarrierCandidateStableId = candidateStableId,
                VehicleStableId = vehicleStableId,
                IsFreightApp = true,
                IsVehicleActive = true,
                IsDriverOperating = true,
                LocationAgeMinutes = locationAgeMinutes,
                PickupDistanceKm = distanceKm,
                PickupAllowedRadiusKm = 10m,
                VehicleCapacity = capacity,
                VehicleCapacityUnitCode = "KGM",
                IsVehicleCompatible = true,
                DriverWaitingMinutes = waitingMinutes,
                CanCompleteSchedule = true,
                CanInsertSchedule = true,
                EstimatedExtraProfit = 4_000m,
                AdditionalDelayMinutes = 5m,
                BaseReason = reason,
            };
    }

    public sealed class 물류이동FixtureAuthorityClient : I물류이동AuthorityClient
    {
        private 물류이동AuthoritySnapshot current;

        public 물류이동FixtureAuthorityClient(물류이동AuthoritySnapshot initial)
            => current = initial.Clone();

        public static 물류이동AuthoritySnapshot CreateInitialSnapshot()
        {
            var settlement = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            settlement.Revision = 15;
            settlement.WorldTick = 14;
            settlement.GameDateLabel = "2026-04-15";
            settlement.AllocationStateCode = "Applied";
            settlement.SourceModeCode = "SimulationFixtureAuthority";
            return new 물류이동AuthoritySnapshot
            {
                SessionStableId = settlement.SessionStableId,
                Revision = settlement.Revision,
                WorldTick = settlement.WorldTick,
                GameDateLabel = settlement.GameDateLabel,
                CargoStableId = 물류이동Fixture.CargoStableId,
                Quantity = 300m,
                SourceAvailableQuantity = 300m,
                RequiredRouteTicks = 3,
                RouteStableId = "route:sim.farm-hub-1",
                SourceModeCode = settlement.SourceModeCode,
                Settlement = settlement,
            };
        }

        public Task<물류이동PreviewData> PreviewAsync(
            string sessionStableId,
            물류이동PreviewRequestData request,
            CancellationToken cancellationToken)
            => Task.FromResult(new 물류이동PreviewData
            {
                SessionStableId = current.SessionStableId,
                ObservedRevision = current.Revision,
                ObservedWorldTick = current.WorldTick,
                CargoStableId = request.CargoStableId,
                Quantity = request.Quantity,
                UnitCode = request.UnitCode,
                RequiredRouteTicks = request.RequiredRouteTicks,
                DestinationStockCandidateStableId = "stock-candidate:arrival:" + request.CargoStableId,
                TransportRequestStableId = 화물배차Fixture.TransportRequestStableId,
                DispatchOfferStableId = "dispatch-offer:" + 화물배차Fixture.TransportRequestStableId,
                RecommendedCarrierCandidateStableId = 화물배차Fixture.RecommendedCandidateStableId,
                DispatchRuleRevision = "freight-dispatch-candidate.v1",
                CandidateEvaluations = CreateCandidateEvaluations(),
                BoundaryCodes = new[]
                {
                    "CandidateOnly", "VehicleAnimationIsPresentationOnly",
                    "DestinationStockRequiresReceivingDecision",
                },
                Request = request,
            });

        public Task<물류이동AuthoritySnapshot> ConfirmAsync(
            string sessionStableId,
            long expectedRevision,
            물류이동PreviewData preview,
            CancellationToken cancellationToken)
        {
            EnsureRevision(sessionStableId, expectedRevision);
            if (preview.RecommendedCarrierCandidateStableId != 화물배차Fixture.RecommendedCandidateStableId)
                throw new InvalidOperationException("SimulationFreightDispatchRecommendationMissing");
            current.Revision++;
            current.MovementStateCode = "Reserved";
            current.TaskStateCode = "Scheduled";
            current.ReservedQuantity = preview.Quantity;
            current.SourceAvailableQuantity = 0m;
            current.DestinationStockCandidateStableId = "stock-candidate:arrival:" + preview.CargoStableId;
            current.TransportRequestStableId = preview.TransportRequestStableId;
            current.DispatchStateCode = "배차확정";
            current.CarrierCandidateStableId = preview.RecommendedCarrierCandidateStableId;
            current.VehicleStableId = "vehicle:sim.truck-fresh";
            current.DispatchRuleRevision = preview.DispatchRuleRevision;
            current.Settlement.Revision = current.Revision;
            current.Settlement.ActiveTaskCount = 1;
            return Task.FromResult(current.Clone());
        }

        private static 화물배차후보평가Data[] CreateCandidateEvaluations()
            => new[]
            {
                new 화물배차후보평가Data
                {
                    CarrierCandidateStableId = "carrier-candidate:sim.waiting-truck",
                    VehicleStableId = "vehicle:sim.truck-fresh",
                    IsEligible = true,
                    IsRecommended = true,
                    Rank = 1,
                    PickupDistanceKm = 6m,
                    VehicleCapacity = 400m,
                    VehicleCapacityUnitCode = "KGM",
                    Reason = "대기 중인 지역 트럭 · 상차 6.0km · 기사대기보정 +9 · 추천점수 43",
                    BaseScore = 34m,
                    DriverWaitingScore = 9m,
                    TotalScore = 43m,
                },
                new 화물배차후보평가Data
                {
                    CarrierCandidateStableId = "carrier-candidate:sim.small-van",
                    VehicleStableId = "vehicle:sim.van-small",
                    VehicleCapacity = 200m,
                    VehicleCapacityUnitCode = "KGM",
                    BlockReasonCodes = new[] { "VehicleCapacityExceeded" },
                    Reason = "차단: VehicleCapacityExceeded",
                },
                new 화물배차후보평가Data
                {
                    CarrierCandidateStableId = "carrier-candidate:sim.stale-truck",
                    VehicleStableId = "vehicle:sim.truck-stale",
                    VehicleCapacity = 400m,
                    VehicleCapacityUnitCode = "KGM",
                    BlockReasonCodes = new[] { "CandidateLocationStale" },
                    Reason = "차단: CandidateLocationStale",
                },
            };

        public Task<물류이동AuthoritySnapshot> AdvanceAsync(
            string sessionStableId,
            long expectedRevision,
            CancellationToken cancellationToken)
        {
            EnsureRevision(sessionStableId, expectedRevision);
            current.Revision++;
            current.WorldTick++;
            current.CompletedRouteTicks++;
            current.MovementStateCode = current.CompletedRouteTicks >= current.RequiredRouteTicks
                ? "ArrivedAtDestination" : "InTransit";
            current.TaskStateCode = current.MovementStateCode == "ArrivedAtDestination"
                ? "Completed" : "InProgress";
            current.GameDateLabel = "2026-04-" + (15 + current.CompletedRouteTicks).ToString("00");
            current.Settlement.Revision = current.Revision;
            current.Settlement.WorldTick = current.WorldTick;
            current.Settlement.GameDateLabel = current.GameDateLabel;
            current.Settlement.ActiveTaskCount = current.TaskStateCode == "Completed" ? 0 : 1;
            return Task.FromResult(current.Clone());
        }

        private void EnsureRevision(string sessionStableId, long expectedRevision)
        {
            if (sessionStableId != current.SessionStableId || expectedRevision != current.Revision)
                throw new InvalidOperationException("SimulationExpectedRevisionConflict");
        }
    }
}
