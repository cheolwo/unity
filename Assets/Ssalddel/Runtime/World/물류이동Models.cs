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
            물류이동PreviewRequestData request,
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
                    CurrentPreview.Request,
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
            물류이동PreviewRequestData request,
            CancellationToken cancellationToken)
        {
            EnsureRevision(sessionStableId, expectedRevision);
            current.Revision++;
            current.MovementStateCode = "Reserved";
            current.TaskStateCode = "Scheduled";
            current.ReservedQuantity = request.Quantity;
            current.SourceAvailableQuantity = 0m;
            current.DestinationStockCandidateStableId = "stock-candidate:arrival:" + request.CargoStableId;
            current.Settlement.Revision = current.Revision;
            current.Settlement.ActiveTaskCount = 1;
            return Task.FromResult(current.Clone());
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
