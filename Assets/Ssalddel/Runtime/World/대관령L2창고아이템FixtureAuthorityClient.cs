using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class 대관령L2창고아이템FixtureAuthorityClient
        : I대관령L2창고아이템AuthorityClient
    {
        private const string SessionId =
            "simulation-session:daegwallyeong-warehouse-item.fixture";
        private decimal containerQuantity = 3m;
        private decimal playerQuantity;
        private long revision;
        private int tick;
        private 대관령L2아이템TransferSnapshot[] transfers =
            Array.Empty<대관령L2아이템TransferSnapshot>();

        public Task<대관령L2창고InventorySnapshot> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot());
        }

        public Task<대관령L2아이템획득PreviewSnapshot> PreviewAsync(
            string sessionStableId,
            대관령L2아이템획득PreviewRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allowed = request.ObservedWorldRevision == revision
                          && request.Quantity > 0
                          && request.Quantity <= containerQuantity;
            return Task.FromResult(new 대관령L2아이템획득PreviewSnapshot
            {
                SessionStableId = SessionId,
                WorldRevision = revision,
                PlayerStableId = request.PlayerStableId,
                BuildingStableId = request.BuildingStableId,
                ContainerStableId = request.ContainerStableId,
                ItemStackStableId = request.ItemStackStableId,
                ItemCode = "produce.potato.sample",
                RequestedQuantity = request.Quantity,
                ContainerQuantityBefore = containerQuantity,
                ContainerQuantityAfter = allowed
                    ? containerQuantity - request.Quantity : containerQuantity,
                PlayerQuantityBefore = playerQuantity,
                PlayerQuantityAfter = allowed
                    ? playerQuantity + request.Quantity : playerQuantity,
                EligibilityStateCode = allowed
                    ? 대관령L2창고아이템Codes.Allowed : "Blocked",
                BlockReasonCodes = allowed
                    ? Array.Empty<string>() : new[] { "QuantityUnavailable" },
                CanConfirm = allowed,
                StateChanged = false,
                SimulationOnly = true,
                IsOperationalState = false,
            });
        }

        public Task<대관령L2창고InventorySnapshot> ConfirmAndReloadAsync(
            string sessionStableId,
            대관령L2아이템획득ConfirmRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ExpectedRevision != revision || request.Quantity <= 0
                || request.Quantity > containerQuantity)
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryFixtureRevisionOrQuantityInvalid");
            containerQuantity -= request.Quantity;
            playerQuantity += request.Quantity;
            revision++;
            transfers = new[]
            {
                new 대관령L2아이템TransferSnapshot
                {
                    TransferStableId = "transfer:fixture:" + revision,
                    CommandId = request.CommandId,
                    PlayerStableId = request.PlayerStableId,
                    BuildingStableId = request.BuildingStableId,
                    ContainerStableId = request.ContainerStableId,
                    ItemStackStableId = request.ItemStackStableId,
                    ItemCode = "produce.potato.sample",
                    Quantity = request.Quantity,
                    UnitCode = "box",
                    AppliedWorldRevision = revision,
                    AppliedWorldTick = tick,
                },
            };
            return Task.FromResult(Snapshot());
        }

        private 대관령L2창고InventorySnapshot Snapshot()
            => new()
            {
                SessionStableId = SessionId,
                WorldRevision = revision,
                WorldTick = tick,
                RuleRevision = "world-survival-inventory.pyeongchang-farm.r1",
                Buildings = new[]
                {
                    new 대관령L2창고BuildingSnapshot
                    {
                        BuildingStableId = 대관령L2창고아이템Codes.BuildingStableId,
                        TileKey = 대관령L2창고아이템Codes.TileKey,
                        RegionStableId = "region:kr:administrative:5176038000",
                        BuildingEvidenceKindCode = "ObservedFixture",
                        SourceRecordStableId =
                            "fixture:vworld-building:51760:sample-warehouse-1",
                        InteriorSpaceStableId = 대관령L2창고아이템Codes.InteriorStableId,
                        InteriorEvidenceKindCode =
                            대관령L2창고아이템Codes.SimulationScenario,
                    },
                },
                Containers = new[]
                {
                    new 대관령L2창고ContainerSnapshot
                    {
                        ContainerStableId = 대관령L2창고아이템Codes.ContainerStableId,
                        BuildingStableId = 대관령L2창고아이템Codes.BuildingStableId,
                        InteriorSpaceStableId = 대관령L2창고아이템Codes.InteriorStableId,
                        AccessPolicyCode = "PublicAcquisition",
                        CapacityUnits = 20m,
                        ManagerPlayerStableIds = new[]
                        {
                            대관령L2창고아이템Codes.PlayerStableId,
                        },
                        EvidenceKindCode = 대관령L2창고아이템Codes.SimulationScenario,
                    },
                },
                ContainerItemStacks = new[]
                {
                    new 대관령L2창고ItemStackSnapshot
                    {
                        ItemStackStableId = 대관령L2창고아이템Codes.ItemStackStableId,
                        ContainerStableId = 대관령L2창고아이템Codes.ContainerStableId,
                        ItemCode = "produce.potato.sample",
                        KoreanName = "대관령 감자 상자",
                        Quantity = containerQuantity,
                        UnitCode = "box",
                        BuildingItemRelationStableId =
                            "relation:sim:pyeongchang-farm:barn-potato-sample",
                        EvidenceKindCode = 대관령L2창고아이템Codes.SimulationScenario,
                    },
                },
                Players = new[]
                {
                    new 대관령L2플레이어InventorySnapshot
                    {
                        PlayerStableId = 대관령L2창고아이템Codes.PlayerStableId,
                        CurrentBuildingStableId = 대관령L2창고아이템Codes.BuildingStableId,
                        InventoryCapacityUnits = 10m,
                        ManagedContainerStableIds = new[]
                        {
                            대관령L2창고아이템Codes.ContainerStableId,
                        },
                        Items = playerQuantity <= 0
                            ? Array.Empty<대관령L2플레이어ItemSnapshot>()
                            : new[]
                            {
                                new 대관령L2플레이어ItemSnapshot
                                {
                                    ItemCode = "produce.potato.sample",
                                    KoreanName = "대관령 감자 상자",
                                    Quantity = playerQuantity,
                                    UnitCode = "box",
                                },
                            },
                    },
                },
                Transfers = transfers,
                SimulationOnly = true,
                IsOperationalState = false,
            };
    }
}
