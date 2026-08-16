using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 대관령L2창고아이템Codes
    {
        public const string TileKey = "kr5186:l2:700:1145";
        public const string BuildingStableId = "scenario-object:pyeongchang-farm:barn-a";
        public const string InteriorStableId = "interior:sim:pyeongchang-farm:barn-a";
        public const string ContainerStableId =
            "container:sim:pyeongchang-farm:barn-a:pallet-1";
        public const string ItemStackStableId =
            "item-stack:sim:pyeongchang-farm:potato-boxes-1";
        public const string PlayerStableId = "player:sim:survival-a";
        public const string Allowed = "Allowed";
        public const string SimulationScenario = "SimulationScenario";
    }

    [Serializable]
    public sealed class 대관령L2창고InventorySnapshot
    {
        public string SessionStableId = string.Empty;
        public long WorldRevision;
        public int WorldTick;
        public string RuleRevision = string.Empty;
        public 대관령L2창고BuildingSnapshot[] Buildings =
            Array.Empty<대관령L2창고BuildingSnapshot>();
        public 대관령L2창고ContainerSnapshot[] Containers =
            Array.Empty<대관령L2창고ContainerSnapshot>();
        public 대관령L2창고ItemStackSnapshot[] ContainerItemStacks =
            Array.Empty<대관령L2창고ItemStackSnapshot>();
        public 대관령L2플레이어InventorySnapshot[] Players =
            Array.Empty<대관령L2플레이어InventorySnapshot>();
        public 대관령L2아이템TransferSnapshot[] Transfers =
            Array.Empty<대관령L2아이템TransferSnapshot>();
        public bool SimulationOnly = true;
        public bool IsOperationalState;

        public void Validate()
        {
            Require(SessionStableId, "DaegwallyeongInventorySessionMissing");
            Require(RuleRevision, "DaegwallyeongInventoryRuleRevisionMissing");
            if (WorldRevision < 0 || WorldTick < 0 || !SimulationOnly || IsOperationalState)
                throw new InvalidOperationException("DaegwallyeongInventoryAuthorityBoundaryInvalid");
            if (Buildings == null || Containers == null || ContainerItemStacks == null
                || Players == null || Transfers == null)
                throw new InvalidOperationException("DaegwallyeongInventoryCollectionMissing");
            Unique(Buildings.Select(value => value?.BuildingStableId), "Building");
            Unique(Containers.Select(value => value?.ContainerStableId), "Container");
            Unique(ContainerItemStacks.Select(value => value?.ItemStackStableId), "ItemStack");
            Unique(Players.Select(value => value?.PlayerStableId), "Player");
            foreach (var value in Buildings) value.Validate();
            foreach (var value in Containers) value.Validate();
            foreach (var value in ContainerItemStacks) value.Validate();
            foreach (var value in Players) value.Validate();
        }

        public 대관령L2창고ItemStackSnapshot RequiredItemStack(string stableId)
            => ContainerItemStacks.SingleOrDefault(value =>
                   string.Equals(value.ItemStackStableId, stableId,
                       StringComparison.Ordinal))
               ?? throw new InvalidOperationException("DaegwallyeongItemStackMissing");

        public decimal PlayerQuantity(string playerStableId, string itemCode)
            => Players.Single(value => value.PlayerStableId == playerStableId).Items
                .Where(value => value.ItemCode == itemCode).Sum(value => value.Quantity);

        private static void Require(string value, string error)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(error);
        }

        private static void Unique(System.Collections.Generic.IEnumerable<string> values,
            string kind)
        {
            var ids = values.ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace)
                || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidOperationException("DaegwallyeongInventoryDuplicate" + kind);
        }
    }

    [Serializable]
    public sealed class 대관령L2창고BuildingSnapshot
    {
        public string BuildingStableId = string.Empty;
        public string TileKey = string.Empty;
        public string RegionStableId = string.Empty;
        public string BuildingEvidenceKindCode = string.Empty;
        public string SourceRecordStableId = string.Empty;
        public string InteriorSpaceStableId = string.Empty;
        public string InteriorEvidenceKindCode = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(BuildingStableId)
                || string.IsNullOrWhiteSpace(TileKey)
                || string.IsNullOrWhiteSpace(RegionStableId)
                || string.IsNullOrWhiteSpace(BuildingEvidenceKindCode)
                || string.IsNullOrWhiteSpace(SourceRecordStableId)
                || string.IsNullOrWhiteSpace(InteriorSpaceStableId)
                || string.IsNullOrWhiteSpace(InteriorEvidenceKindCode))
                throw new InvalidOperationException("DaegwallyeongInventoryBuildingInvalid");
        }
    }

    [Serializable]
    public sealed class 대관령L2창고ContainerSnapshot
    {
        public string ContainerStableId = string.Empty;
        public string BuildingStableId = string.Empty;
        public string InteriorSpaceStableId = string.Empty;
        public string AccessPolicyCode = string.Empty;
        public decimal CapacityUnits;
        public string[] ManagerPlayerStableIds = Array.Empty<string>();
        public string EvidenceKindCode = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ContainerStableId)
                || string.IsNullOrWhiteSpace(BuildingStableId)
                || string.IsNullOrWhiteSpace(InteriorSpaceStableId)
                || string.IsNullOrWhiteSpace(AccessPolicyCode)
                || string.IsNullOrWhiteSpace(EvidenceKindCode)
                || CapacityUnits <= 0 || ManagerPlayerStableIds == null)
                throw new InvalidOperationException("DaegwallyeongInventoryContainerInvalid");
        }
    }

    [Serializable]
    public sealed class 대관령L2창고ItemStackSnapshot
    {
        public string ItemStackStableId = string.Empty;
        public string ContainerStableId = string.Empty;
        public string ItemCode = string.Empty;
        public string KoreanName = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
        public string BuildingItemRelationStableId = string.Empty;
        public string EvidenceKindCode = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ItemStackStableId)
                || string.IsNullOrWhiteSpace(ContainerStableId)
                || string.IsNullOrWhiteSpace(ItemCode)
                || string.IsNullOrWhiteSpace(KoreanName)
                || string.IsNullOrWhiteSpace(UnitCode)
                || string.IsNullOrWhiteSpace(BuildingItemRelationStableId)
                || string.IsNullOrWhiteSpace(EvidenceKindCode) || Quantity < 0)
                throw new InvalidOperationException("DaegwallyeongInventoryItemStackInvalid");
        }
    }

    [Serializable]
    public sealed class 대관령L2플레이어InventorySnapshot
    {
        public string PlayerStableId = string.Empty;
        public string CurrentBuildingStableId = string.Empty;
        public decimal InventoryCapacityUnits;
        public string[] ManagedContainerStableIds = Array.Empty<string>();
        public 대관령L2플레이어ItemSnapshot[] Items =
            Array.Empty<대관령L2플레이어ItemSnapshot>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PlayerStableId)
                || string.IsNullOrWhiteSpace(CurrentBuildingStableId)
                || InventoryCapacityUnits < 0 || ManagedContainerStableIds == null
                || Items == null || Items.Any(value => value == null || value.Quantity < 0))
                throw new InvalidOperationException("DaegwallyeongInventoryPlayerInvalid");
        }
    }

    [Serializable]
    public sealed class 대관령L2플레이어ItemSnapshot
    {
        public string ItemCode = string.Empty;
        public string KoreanName = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
    }

    [Serializable]
    public sealed class 대관령L2아이템TransferSnapshot
    {
        public string TransferStableId = string.Empty;
        public string CommandId = string.Empty;
        public string PlayerStableId = string.Empty;
        public string BuildingStableId = string.Empty;
        public string ContainerStableId = string.Empty;
        public string ItemStackStableId = string.Empty;
        public string ItemCode = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
        public long AppliedWorldRevision;
        public int AppliedWorldTick;
    }

    [Serializable]
    public sealed class 대관령L2아이템획득PreviewRequest
    {
        public long ObservedWorldRevision;
        public string PlayerStableId = string.Empty;
        public string BuildingStableId = string.Empty;
        public string ContainerStableId = string.Empty;
        public string ItemStackStableId = string.Empty;
        public decimal Quantity;
    }

    [Serializable]
    public sealed class 대관령L2아이템획득ConfirmRequest
    {
        public string CommandId = string.Empty;
        public long ExpectedRevision;
        public string PlayerStableId = string.Empty;
        public string BuildingStableId = string.Empty;
        public string ContainerStableId = string.Empty;
        public string ItemStackStableId = string.Empty;
        public decimal Quantity;
    }

    [Serializable]
    public sealed class 대관령L2아이템획득PreviewSnapshot
    {
        public string SessionStableId = string.Empty;
        public long WorldRevision;
        public string PlayerStableId = string.Empty;
        public string BuildingStableId = string.Empty;
        public string ContainerStableId = string.Empty;
        public string ItemStackStableId = string.Empty;
        public string ItemCode = string.Empty;
        public decimal RequestedQuantity;
        public decimal ContainerQuantityBefore;
        public decimal ContainerQuantityAfter;
        public decimal PlayerQuantityBefore;
        public decimal PlayerQuantityAfter;
        public string EligibilityStateCode = string.Empty;
        public string[] BlockReasonCodes = Array.Empty<string>();
        public bool CanConfirm;
        public bool StateChanged;
        public bool SimulationOnly = true;
        public bool IsOperationalState;

        public void Validate(long expectedRevision)
        {
            if (WorldRevision != expectedRevision || RequestedQuantity <= 0
                || StateChanged || !SimulationOnly || IsOperationalState
                || BlockReasonCodes == null)
                throw new InvalidOperationException("DaegwallyeongInventoryPreviewInvalid");
        }
    }

    public interface I대관령L2창고아이템AuthorityClient
    {
        Task<대관령L2창고InventorySnapshot> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken);
        Task<대관령L2아이템획득PreviewSnapshot> PreviewAsync(
            string sessionStableId, 대관령L2아이템획득PreviewRequest request,
            CancellationToken cancellationToken);
        Task<대관령L2창고InventorySnapshot> ConfirmAndReloadAsync(
            string sessionStableId, 대관령L2아이템획득ConfirmRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class 대관령L2창고아이템Coordinator
    {
        private readonly I대관령L2창고아이템AuthorityClient authority;

        public 대관령L2창고아이템Coordinator(I대관령L2창고아이템AuthorityClient client)
            => authority = client ?? throw new ArgumentNullException(nameof(client));

        public 대관령L2창고InventorySnapshot Current { get; private set; }
        public 대관령L2아이템획득PreviewSnapshot Preview { get; private set; }
        public bool IsStale { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;

        public async Task LoadAsync(string sessionStableId, CancellationToken cancellationToken)
        {
            var loaded = await authority.LoadAsync(sessionStableId, cancellationToken);
            ApplyCanonical(loaded, allowSameRevision: true);
        }

        public async Task PreviewOneAsync(CancellationToken cancellationToken)
        {
            var current = RequiredCurrent();
            var item = RequiredVerticalSlice(current);
            Preview = await authority.PreviewAsync(current.SessionStableId,
                new 대관령L2아이템획득PreviewRequest
                {
                    ObservedWorldRevision = current.WorldRevision,
                    PlayerStableId = 대관령L2창고아이템Codes.PlayerStableId,
                    BuildingStableId = 대관령L2창고아이템Codes.BuildingStableId,
                    ContainerStableId = 대관령L2창고아이템Codes.ContainerStableId,
                    ItemStackStableId = item.ItemStackStableId,
                    Quantity = 1m,
                }, cancellationToken);
            Preview.Validate(current.WorldRevision);
        }

        public async Task ConfirmAsync(string commandId, CancellationToken cancellationToken)
        {
            var current = RequiredCurrent();
            if (Preview == null || !Preview.CanConfirm
                || Preview.WorldRevision != current.WorldRevision)
                throw new InvalidOperationException("DaegwallyeongInventoryPreviewRequired");
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("DaegwallyeongInventoryCommandIdMissing",
                    nameof(commandId));
            var beforeRevision = current.WorldRevision;
            var beforeContainer = Preview.ContainerQuantityBefore;
            var beforePlayer = Preview.PlayerQuantityBefore;
            var canonical = await authority.ConfirmAndReloadAsync(current.SessionStableId,
                new 대관령L2아이템획득ConfirmRequest
                {
                    CommandId = commandId.Trim(),
                    ExpectedRevision = beforeRevision,
                    PlayerStableId = Preview.PlayerStableId,
                    BuildingStableId = Preview.BuildingStableId,
                    ContainerStableId = Preview.ContainerStableId,
                    ItemStackStableId = Preview.ItemStackStableId,
                    Quantity = Preview.RequestedQuantity,
                }, cancellationToken);
            ApplyCanonical(canonical, allowSameRevision: false);
            var item = RequiredVerticalSlice(Current);
            var playerQuantity = Current.PlayerQuantity(
                대관령L2창고아이템Codes.PlayerStableId, item.ItemCode);
            if (item.Quantity != beforeContainer - Preview.RequestedQuantity
                || playerQuantity != beforePlayer + Preview.RequestedQuantity)
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryCanonicalTransferMismatch");
            Preview = null;
        }

        public void ClearPreview()
            => Preview = null;

        public void MarkStale(Exception error)
        {
            IsStale = Current != null;
            ErrorCode = error?.Message ?? "DaegwallyeongInventoryUnknownError";
        }

        private void ApplyCanonical(대관령L2창고InventorySnapshot snapshot,
            bool allowSameRevision)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            snapshot.Validate();
            RequiredVerticalSlice(snapshot);
            if (Current != null && (snapshot.WorldRevision < Current.WorldRevision
                || (!allowSameRevision && snapshot.WorldRevision <= Current.WorldRevision)))
                throw new InvalidOperationException(
                    "DaegwallyeongInventoryCanonicalRevisionInvalid");
            Current = snapshot;
            IsStale = false;
            ErrorCode = string.Empty;
        }

        private 대관령L2창고InventorySnapshot RequiredCurrent()
            => Current ?? throw new InvalidOperationException(
                "DaegwallyeongInventoryNotLoaded");

        private static 대관령L2창고ItemStackSnapshot RequiredVerticalSlice(
            대관령L2창고InventorySnapshot snapshot)
        {
            var building = snapshot.Buildings.SingleOrDefault(value =>
                value.BuildingStableId == 대관령L2창고아이템Codes.BuildingStableId);
            if (building == null || building.TileKey != 대관령L2창고아이템Codes.TileKey
                || building.InteriorSpaceStableId !=
                대관령L2창고아이템Codes.InteriorStableId)
                throw new InvalidOperationException("DaegwallyeongInventoryAnchorInvalid");
            var container = snapshot.Containers.SingleOrDefault(value =>
                value.ContainerStableId == 대관령L2창고아이템Codes.ContainerStableId);
            if (container == null || container.BuildingStableId != building.BuildingStableId
                || container.InteriorSpaceStableId != building.InteriorSpaceStableId)
                throw new InvalidOperationException("DaegwallyeongInventoryContainerAnchorInvalid");
            return snapshot.RequiredItemStack(대관령L2창고아이템Codes.ItemStackStableId);
        }
    }
}
