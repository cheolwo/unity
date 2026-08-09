using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Perspectives;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class SimulatedUrbanLogisticsRoleApiClient : IRolePerspectiveApiClient
    {
        private static readonly DateTimeOffset FixtureAt =
            DateTimeOffset.Parse("2026-08-08T15:00:00+09:00");

        public Task<RolePerspectiveApiModel> GetAsync(
            역할관점조회Request request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new RolePerspectiveApiModel
            {
                StableId = "role-perspective:urban-logistics-center.transport-71",
                Revision = 1,
                AuthorizedRoleCode = RolePerspectiveCodes.Transporter,
                WorldZoneCode = WorldZoneCodes.UrbanLogisticsCenter,
                ViewerScopeCode = WorldViewerScopeCodes.AuthorizedParty,
                SourceTypeCode = RolePerspectiveSourceTypeCodes.SimulatedFixture,
                AuthorizationDecisionId = "simulation:transport-71",
                GeneratedAt = FixtureAt,
                ObjectEmphases = new[]
                {
                    Emphasis("transport:71", RoleObjectEmphasisCodes.Primary, "내 운송 · 배차확정"),
                    Emphasis("transport-stop:71.pickup", RoleObjectEmphasisCodes.Destination, "상차 위치"),
                    Emphasis("transport-stop:71.dropoff", RoleObjectEmphasisCodes.Related, "하차 위치"),
                },
                AllowedInteractions = new[]
                {
                    Interaction("inspect-current-transport", "transport:71", WorldInteractionEffectCodes.ReadOnly),
                    Interaction("arrive-pickup", "transport-stop:71.pickup", WorldInteractionEffectCodes.ServerCommand, true),
                },
            });
        }

        private static RoleObjectEmphasisApiModel Emphasis(
            string stableId,
            string emphasisCode,
            string label)
        {
            return new RoleObjectEmphasisApiModel
            {
                TargetStableId = stableId,
                EmphasisCode = emphasisCode,
                Label = label,
                DetailPanelCode = "transport-detail",
            };
        }

        private static RoleAllowedInteractionApiModel Interaction(
            string code,
            string targetStableId,
            string effectCode,
            bool command = false)
        {
            return new RoleAllowedInteractionApiModel
            {
                InteractionCode = code,
                TargetStableId = targetStableId,
                EffectCode = effectCode,
                RequiresExplicitConfirmation = command,
                RequiresCanonicalStateRefresh = command,
            };
        }
    }

    public sealed class SimulatedUrbanLogisticsNpcApiClient : INpcMovementApiClient
    {
        public Task<NpcMovementApiModel?> GetAsync(
            NpcMovementQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<NpcMovementApiModel?>(new NpcMovementApiModel
            {
                StableId = "npc-movement:transport-71",
                Revision = 1,
                NpcStableId = "npc:transport-driver.71",
                ActorRoleCode = RolePerspectiveCodes.Transporter,
                WorldZoneCode = WorldZoneCodes.UrbanLogisticsCenter,
                RouteCode = "logistics-center-transporter-handoff",
                CurrentWaypointKey = "logistics.vehicle-gate",
                DestinationWaypointKey = "logistics.loading-bay",
                MovementStateCode = NpcMovementStateCodes.Moving,
                ArrivalActionCode = "wait-for-loading",
                SourceTypeCode = NpcMovementSourceTypeCodes.SimulatedFixture,
                GeneratedAt = DateTimeOffset.Parse("2026-08-08T15:00:00+09:00"),
            });
        }
    }

    public sealed class SimulatedCargoWarehouseHandoffApiClient
        : ICargoWarehouseHandoffApiClient
    {
        public Task<CargoWarehouseHandoffApiModel?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generatedAt = DateTimeOffset.Parse("2026-08-08T15:00:00+09:00");
            return Task.FromResult<CargoWarehouseHandoffApiModel?>(
                new CargoWarehouseHandoffApiModel
                {
                    StableId = "cargo-handoff:transport-71.inbound-91",
                    Revision = 1,
                    HandoffStateCode = CargoHandoffStateCodes.InTransit,
                    CargoStableId = "cargo:transport-71",
                    TransportTaskStableId = "transport-task:71",
                    InboundTaskStableId = "inbound-task:91",
                    Movements = new[]
                    {
                        Movement(
                            "npc-movement:transporter.transport-71.inbound-91",
                            "npc:transport-driver.71",
                            RolePerspectiveCodes.Transporter,
                            "transport-network",
                            "transport-network-hub-delivery",
                            "network.logistics-center",
                            "network.warehouse",
                            "arrive-at-warehouse",
                            "transport-task:71",
                            generatedAt),
                    },
                    GeneratedAt = generatedAt,
                });
        }

        private static NpcMovementApiModel Movement(
            string stableId,
            string npcStableId,
            string roleCode,
            string worldZoneCode,
            string routeCode,
            string currentWaypoint,
            string destinationWaypoint,
            string arrivalAction,
            string canonicalTaskStableId,
            DateTimeOffset generatedAt)
        {
            return new NpcMovementApiModel
            {
                StableId = stableId,
                Revision = 1,
                NpcStableId = npcStableId,
                ActorRoleCode = roleCode,
                WorldZoneCode = worldZoneCode,
                RouteCode = routeCode,
                CurrentWaypointKey = currentWaypoint,
                DestinationWaypointKey = destinationWaypoint,
                MovementStateCode = NpcMovementStateCodes.Moving,
                ArrivalActionCode = arrivalAction,
                SourceTypeCode = NpcMovementSourceTypeCodes.SimulatedFixture,
                CanonicalTaskStableId = canonicalTaskStableId,
                GeneratedAt = generatedAt,
            };
        }
    }
}
