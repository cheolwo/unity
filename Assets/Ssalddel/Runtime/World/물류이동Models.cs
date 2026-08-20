using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

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

    public static class Npc물류운송Codes
    {
        public const string Freight = "Freight";
        public const string FoodDelivery = "FoodDelivery";
        public const string ScenarioProcedural = "ScenarioProcedural";
        public const string Planned = "Planned";
        public const string AwaitingRouteCells = "AwaitingRouteCells";
        public const string Moving = "Moving";
        public const string PausedByStreaming = "PausedByStreaming";
        public const string Arrived = "Arrived";
    }

    [Serializable]
    public sealed class Npc물류PositionData
    {
        public double X;
        public double Y;
        public double Z;
    }

    [Serializable]
    public sealed class Npc물류WaypointData
    {
        public string WaypointStableId = string.Empty;
        public int Sequence;
        public string L3CellKey = string.Empty;
        public Npc물류PositionData Position = new();
    }

    [Serializable]
    public sealed class Npc물류RoutePlanData
    {
        public string RouteStableId = string.Empty;
        public string RouteVersion = string.Empty;
        public string TransportKindCode = string.Empty;
        public string EvidenceKindCode = Npc물류운송Codes.ScenarioProcedural;
        public string CargoOrOrderStableId = string.Empty;
        public string NpcStableId = string.Empty;
        public string VehicleStableId = string.Empty;
        public Npc물류WaypointData[] Waypoints = Array.Empty<Npc물류WaypointData>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(RouteStableId)
                || string.IsNullOrWhiteSpace(RouteVersion)
                || (TransportKindCode != Npc물류운송Codes.Freight
                    && TransportKindCode != Npc물류운송Codes.FoodDelivery)
                || EvidenceKindCode != Npc물류운송Codes.ScenarioProcedural
                || string.IsNullOrWhiteSpace(CargoOrOrderStableId)
                || string.IsNullOrWhiteSpace(NpcStableId)
                || string.IsNullOrWhiteSpace(VehicleStableId)
                || Waypoints == null || Waypoints.Length < 2)
                throw new InvalidOperationException("NpcLogisticsRoutePlanInvalid");
            for (var index = 0; index < Waypoints.Length; index++)
            {
                var waypoint = Waypoints[index];
                if (waypoint == null || waypoint.Sequence != index
                    || waypoint.WaypointStableId != RouteStableId + ":waypoint:" + index
                    || string.IsNullOrWhiteSpace(waypoint.L3CellKey)
                    || waypoint.Position == null)
                    throw new InvalidOperationException("NpcLogisticsWaypointInvalid:" + index);
            }
        }
    }

    [Serializable]
    public sealed class Npc물류RouteCheckpointData
    {
        public string CheckpointStableId = string.Empty;
        public string RouteStableId = string.Empty;
        public string CargoOrOrderStableId = string.Empty;
        public string NpcStableId = string.Empty;
        public string VehicleStableId = string.Empty;
        public int Sequence;
        public long ExpectedRevision;
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

        public async Task ApplyNpcRouteCheckpointAsync(
            Npc물류RouteCheckpointData checkpoint,
            CancellationToken cancellationToken = default)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));
            if (PhaseCode != 물류이동PhaseCodes.Reserved
                && PhaseCode != 물류이동PhaseCodes.InTransit)
                throw new InvalidOperationException("NpcLogisticsActiveTransportRequired");
            var snapshot = CurrentSnapshot;
            var expectedSequence = snapshot.CompletedRouteTicks + 1;
            if (checkpoint.RouteStableId != snapshot.RouteStableId
                || checkpoint.CargoOrOrderStableId != snapshot.CargoStableId
                || checkpoint.NpcStableId != snapshot.CarrierCandidateStableId
                || checkpoint.VehicleStableId != snapshot.VehicleStableId
                || checkpoint.ExpectedRevision != snapshot.Revision
                || checkpoint.Sequence != expectedSequence
                || checkpoint.CheckpointStableId
                    != snapshot.RouteStableId + ":checkpoint:" + expectedSequence)
                throw new InvalidOperationException("NpcLogisticsRouteCheckpointRejected");

            var beforeRevision = snapshot.Revision;
            var beforeProgress = snapshot.CompletedRouteTicks;
            await AdvanceAsync(cancellationToken);
            if (CurrentSnapshot.Revision != beforeRevision + 1
                || CurrentSnapshot.CompletedRouteTicks != beforeProgress + 1)
                throw new InvalidOperationException("NpcLogisticsCheckpointAuthorityMismatch");
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
        {
            var source = 화물배차Fixture.CreateRequest();
            var decision = 화물배차후보선정Policy.판정(new 화물배차후보선정요청
            {
                화물수량 = 300m,
                화물단위코드 = "KGM",
                위치유효시간분 = source.LocationFreshnessMinutes,
                기본상차접근반경Km = source.BasePickupRadiusKm,
                원거리상차접근최대반경Km = source.MaximumRemotePickupRadiusKm,
                원거리상차평균속도KmH = source.RemotePickupAverageSpeedKmH,
                원거리상차도착여유분 = source.RemotePickupArrivalBufferMinutes,
                상차시간창남은분 = source.PickupWindowRemainingMinutes,
                제외후보StableId = source.ExcludedCarrierCandidateStableId,
                후보목록 = Array.ConvertAll(source.Candidates, candidate => new 화물배차후보입력
                {
                    후보StableId = candidate.CarrierCandidateStableId,
                    차량StableId = candidate.VehicleStableId,
                    화물운송앱여부 = candidate.IsFreightApp,
                    차량활성여부 = candidate.IsVehicleActive,
                    기사운행중여부 = candidate.IsDriverOperating,
                    이전거절여부 = candidate.WasPreviouslyRejected,
                    위치경과분 = candidate.LocationAgeMinutes,
                    상차거리Km = candidate.PickupDistanceKm,
                    상차접근허용반경Km = candidate.PickupAllowedRadiusKm,
                    차량용량 = candidate.VehicleCapacity,
                    차량용량단위코드 = candidate.VehicleCapacityUnitCode,
                    차량적합여부 = candidate.IsVehicleCompatible,
                    차량부적합사유코드목록 = candidate.VehicleBlockReasonCodes,
                    기사대기분 = candidate.DriverWaitingMinutes,
                    기본추천사유 = candidate.BaseReason,
                    추천점수요청 = new 화물배차추천점수요청
                    {
                        전체일정완수가능여부 = candidate.CanCompleteSchedule,
                        일정삽입가능여부 = candidate.CanInsertSchedule,
                        경로변경이점여부 = candidate.HasRouteChangeBenefit,
                        예상추가순이익 = candidate.EstimatedExtraProfit,
                        추가지연분 = candidate.AdditionalDelayMinutes,
                        경로기준거리Km = candidate.PickupDistanceKm,
                        추천유형 = candidate.RecommendationTypeCode,
                        화물민감여부 = candidate.IsCargoSensitive,
                        복귀우회증가거리Km = candidate.ReturnDetourDistanceKm,
                        복귀지기준사용여부 = candidate.UsesReturnDestination,
                    },
                }),
            });
            return Array.ConvertAll(decision.후보평가목록, value => new 화물배차후보평가Data
            {
                CarrierCandidateStableId = value.후보StableId,
                VehicleStableId = value.차량StableId,
                IsEligible = value.적격여부,
                IsRecommended = value.후보StableId == decision.추천후보StableId,
                Rank = value.추천순위,
                PickupDistanceKm = value.상차거리Km,
                VehicleCapacity = value.차량용량,
                VehicleCapacityUnitCode = value.차량용량단위코드,
                Reason = value.추천사유,
                BlockReasonCodes = value.차단사유코드목록,
                BaseScore = value.기본추천점수,
                DriverWaitingScore = value.기사대기보정점수,
                TotalScore = value.총추천점수,
            });
        }

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
