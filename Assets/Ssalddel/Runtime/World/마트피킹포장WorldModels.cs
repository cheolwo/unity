using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class 마트피킹포장WorldSnapshot
    {
        public string StableId { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public long RevisionNumber { get; set; }
        public DateTimeOffset GeneratedAtUtc { get; set; }
        public string WarehouseStableId { get; set; } = string.Empty;
        public int TotalOrderCount { get; set; }
        public bool IsTruncated { get; set; }
        public 마트피킹포장Workflow[] Workflows { get; set; } = Array.Empty<마트피킹포장Workflow>();
        public 운영자재고Shelf[] Shelves { get; set; } = Array.Empty<운영자재고Shelf>();
        public 마트피킹포장Task[] Tasks { get; set; } = Array.Empty<마트피킹포장Task>();
        public 마트피킹포장Npc[] Npcs { get; set; } = Array.Empty<마트피킹포장Npc>();

        public void Validate(long expectedWarehouseId)
        {
            if (expectedWarehouseId <= 0
                || !string.Equals(StableId, "market-picking-packing-zone:" + expectedWarehouseId, StringComparison.Ordinal)
                || !string.Equals(WarehouseStableId, "warehouse:" + expectedWarehouseId, StringComparison.Ordinal))
                throw new InvalidOperationException("MarketPickingPackingWorldIdentityMismatch");
            if (string.IsNullOrWhiteSpace(Revision) || Revision.Length != 64
                || RevisionNumber < 0 || GeneratedAtUtc == default
                || TotalOrderCount < 0 || Workflows == null || Shelves == null || Tasks == null || Npcs == null)
                throw new InvalidOperationException("MarketPickingPackingWorldSnapshotInvalid");

            foreach (var task in Tasks) task.Validate();
            var taskIds = Tasks.Select(task => task.StableId).ToHashSet(StringComparer.Ordinal);
            foreach (var shelf in Shelves) shelf.Validate(WarehouseStableId, taskIds);
            var shelfIds = Shelves.Select(shelf => shelf.StableId).ToHashSet(StringComparer.Ordinal);
            var workflowIds = Workflows.Select(item => item.StableId).ToHashSet(StringComparer.Ordinal);
            if (taskIds.Count != Tasks.Length
                || shelfIds.Count != Shelves.Length
                || Workflows.Any(item => string.IsNullOrWhiteSpace(item.StableId))
                || workflowIds.Count != Workflows.Length
                || Tasks.Any(task => !workflowIds.Contains(task.WorkflowStableId))
                || Npcs.Any(npc => !taskIds.Contains(npc.SourceTaskStableId)))
                throw new InvalidOperationException("MarketPickingPackingWorldRelationshipInvalid");
            foreach (var npc in Npcs) npc.Validate();
        }
    }

    public sealed class 운영자재고Shelf
    {
        public const string ObjectStableId = "seedbed-object:city.operator-inventory-shelf.a";

        public string StableId { get; set; } = string.Empty;
        public string SeedbedObjectStableId { get; set; } = ObjectStableId;
        public string WarehouseStableId { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public string AccessScopeCode { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public int TotalAvailableQuantity { get; set; }
        public int TotalReservedQuantity { get; set; }
        public string[] InventoryItemStableIds { get; set; } = Array.Empty<string>();
        public string[] ProductNames { get; set; } = Array.Empty<string>();
        public string[] ActiveTaskStableIds { get; set; } = Array.Empty<string>();
        public string PickApproachWaypointKey { get; set; } = string.Empty;
        public string PickPointWaypointKey { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public bool IsPresentationReady { get; set; }

        public void Validate(string expectedWarehouseStableId, System.Collections.Generic.ISet<string> taskIds)
        {
            if (string.IsNullOrWhiteSpace(StableId)
                || !string.Equals(SeedbedObjectStableId, ObjectStableId, StringComparison.Ordinal)
                || !string.Equals(WarehouseStableId, expectedWarehouseStableId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(LocationCode)
                || !string.Equals(AccessScopeCode, "OperatorOnly", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(StateCode)
                || TotalAvailableQuantity < 0
                || TotalReservedQuantity < 0
                || InventoryItemStableIds == null
                || ProductNames == null
                || ActiveTaskStableIds == null
                || InventoryItemStableIds.Any(string.IsNullOrWhiteSpace)
                || ActiveTaskStableIds.Any(id => !taskIds.Contains(id))
                || string.IsNullOrWhiteSpace(PickApproachWaypointKey)
                || string.IsNullOrWhiteSpace(PickPointWaypointKey)
                || UpdatedAtUtc == default
                || !IsPresentationReady)
                throw new InvalidOperationException("MarketOperatorInventoryShelfInvalid");
        }
    }

    public sealed class 마트피킹포장Workflow
    {
        public string StableId { get; set; } = string.Empty;
        public string OrderStateCode { get; set; } = string.Empty;
        public string CurrentStageCode { get; set; } = string.Empty;
        public int ProductLineCount { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
    }

    public sealed class 마트피킹포장Task
    {
        public string StableId { get; set; } = string.Empty;
        public string WorkflowStableId { get; set; } = string.Empty;
        public string OrderLineStableId { get; set; } = string.Empty;
        public string InventoryItemStableId { get; set; } = string.Empty;
        public string PreviousTaskStableId { get; set; } = string.Empty;
        public string NextTaskStableId { get; set; } = string.Empty;
        public string TaskKindCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public string LocationMappingStateCode { get; set; } = string.Empty;
        public string ToteStableId { get; set; } = string.Empty;
        public string PackingStationWaypointKey { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;
        public bool IsPresentationReady { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(StableId)
                || string.IsNullOrWhiteSpace(WorkflowStableId)
                || string.IsNullOrWhiteSpace(OrderLineStableId)
                || string.IsNullOrWhiteSpace(TaskKindCode)
                || string.IsNullOrWhiteSpace(ProductName)
                || string.IsNullOrWhiteSpace(Sku)
                || Quantity <= 0
                || string.IsNullOrWhiteSpace(LocationMappingStateCode)
                || string.IsNullOrWhiteSpace(ToteStableId)
                || string.IsNullOrWhiteSpace(PackingStationWaypointKey)
                || string.IsNullOrWhiteSpace(StatusCode)
                || string.IsNullOrWhiteSpace(ActivityCode)
                || UpdatedAtUtc == default)
                throw new InvalidOperationException("MarketPickingPackingWorldTaskInvalid");
            if (string.Equals(LocationMappingStateCode, "LocationUnmapped", StringComparison.Ordinal)
                && IsPresentationReady)
                throw new InvalidOperationException("MarketPickingPackingUnmappedLocationPresentationForbidden");
        }
    }

    public sealed class 마트피킹포장Npc
    {
        public string StableId { get; set; } = string.Empty;
        public string SourceTaskStableId { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string RouteCode { get; set; } = string.Empty;
        public string CurrentWaypointKey { get; set; } = string.Empty;
        public string DestinationWaypointKey { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(StableId)
                || string.IsNullOrWhiteSpace(SourceTaskStableId)
                || string.IsNullOrWhiteSpace(RoleCode)
                || string.IsNullOrWhiteSpace(RouteCode)
                || string.IsNullOrWhiteSpace(CurrentWaypointKey)
                || string.IsNullOrWhiteSpace(DestinationWaypointKey)
                || string.IsNullOrWhiteSpace(ActivityCode))
                throw new InvalidOperationException("MarketPickingPackingWorldNpcInvalid");
        }
    }

    public interface I마트피킹포장WorldRepository
    {
        Task<마트피킹포장WorldSnapshot> LoadAsync(
            long warehouseId,
            CancellationToken cancellationToken);
    }

    public sealed class 마트피킹포장WorldStateStore
    {
        public 마트피킹포장WorldSnapshot Current { get; private set; } = null!;

        public 마트피킹포장WorldSnapshot Accept(
            long warehouseId,
            마트피킹포장WorldSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            snapshot.Validate(warehouseId);
            if (Current != null)
            {
                if (!string.Equals(Current.WarehouseStableId, snapshot.WarehouseStableId, StringComparison.Ordinal))
                    throw new InvalidOperationException("MarketPickingPackingWorldWarehouseChanged");
                if (snapshot.RevisionNumber < Current.RevisionNumber)
                    throw new InvalidOperationException("MarketPickingPackingWorldRevisionRegressed");
                if (snapshot.RevisionNumber == Current.RevisionNumber
                    && !string.Equals(snapshot.Revision, Current.Revision, StringComparison.Ordinal))
                    throw new InvalidOperationException("MarketPickingPackingWorldRevisionConflict");
            }

            Current = snapshot;
            return snapshot;
        }
    }

    public sealed class 운영자재고ShelfPresentationModel
    {
        public string ObjectStableId { get; set; } = string.Empty;
        public string ShelfStableId { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public string StateCode { get; set; } = string.Empty;
        public int TotalAvailableQuantity { get; set; }
        public int TotalReservedQuantity { get; set; }
        public string FarLabel { get; set; } = string.Empty;
        public string NearStatus { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string[] AllowedActionCodes { get; set; } = Array.Empty<string>();
        public string[] RequiredSocketNames { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 서버 상태 사본을 운영자용 Shelf 표현으로 바꿉니다.
    /// 재고 변경이나 피킹 완료 명령은 만들지 않습니다.
    /// </summary>
    public sealed class 운영자재고ShelfProjector
    {
        private static readonly string[] ReadOnlyActions =
        {
            "InspectInventory",
            "InspectPickTasks",
        };

        private static readonly string[] RequiredSockets =
        {
            "Inventory",
            "ShelfTask",
            "Operator",
            "Interaction",
            "Label",
            "CameraFocus",
        };

        public 운영자재고ShelfPresentationModel Project(운영자재고Shelf shelf)
        {
            if (shelf == null) throw new ArgumentNullException(nameof(shelf));
            var taskIds = shelf.ActiveTaskStableIds ?? Array.Empty<string>();
            shelf.Validate(shelf.WarehouseStableId, taskIds.ToHashSet(StringComparer.Ordinal));
            var products = shelf.ProductNames == null || shelf.ProductNames.Length == 0
                ? "품목 정보 없음"
                : string.Join(", ", shelf.ProductNames);

            return new 운영자재고ShelfPresentationModel
            {
                ObjectStableId = shelf.SeedbedObjectStableId,
                ShelfStableId = shelf.StableId,
                LocationCode = shelf.LocationCode,
                StateCode = shelf.StateCode,
                TotalAvailableQuantity = shelf.TotalAvailableQuantity,
                TotalReservedQuantity = shelf.TotalReservedQuantity,
                FarLabel = "재고 Shelf " + shelf.LocationCode,
                NearStatus = ShelfStateLabel(shelf.StateCode)
                    + " · 가용 " + shelf.TotalAvailableQuantity
                    + " · 예약 " + shelf.TotalReservedQuantity,
                DetailText = products + " · 연결된 피킹 작업 " + taskIds.Length + "건",
                AllowedActionCodes = (string[])ReadOnlyActions.Clone(),
                RequiredSocketNames = (string[])RequiredSockets.Clone(),
            };
        }

        private static string ShelfStateLabel(string stateCode)
        {
            switch (stateCode)
            {
                case "PickingInProgress": return "피킹 진행 중";
                case "PickingReady": return "피킹 대기";
                case "PickCompleted": return "피킹 후 포장 이동 중";
                case "ReservedOnly": return "예약 재고만 있음";
                case "Depleted": return "재고 소진";
                case "Available": return "출고 가능";
                default: return "상태 확인 필요";
            }
        }
    }
}
