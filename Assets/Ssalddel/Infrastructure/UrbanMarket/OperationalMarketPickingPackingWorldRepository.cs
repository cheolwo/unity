using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.UrbanMarket
{
    public sealed class OperationalMarketPickingPackingWorldRepository
        : I마트피킹포장WorldRepository
    {
        private const string Route = "api/v1/warehouse-operations/mart/world/picking-packing";
        private readonly IOperationalUnityApiClient apiClient;

        public OperationalMarketPickingPackingWorldRepository(IOperationalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<마트피킹포장WorldSnapshot> LoadAsync(
            long warehouseId,
            CancellationToken cancellationToken)
        {
            if (warehouseId <= 0) throw new ArgumentOutOfRangeException(nameof(warehouseId));
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = Route + "?warehouseId=" + warehouseId.ToString(CultureInfo.InvariantCulture),
                RequiresAuthentication = true,
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            OperationalMarketProductApiClient.EnsureSuccess(
                response,
                "MarketPickingPackingWorldRequestFailed");

            var wire = JsonConvert.DeserializeObject<SnapshotWire>(response.Body)
                ?? throw new InvalidOperationException("MarketPickingPackingWorldJsonInvalid");
            var result = wire.ToModel();
            result.Validate(warehouseId);
            return result;
        }

        private sealed class SnapshotWire
        {
            [JsonProperty("stableId")] public string StableId { get; set; } = string.Empty;
            [JsonProperty("revision")] public string Revision { get; set; } = string.Empty;
            [JsonProperty("revisionNumber")] public long RevisionNumber { get; set; }
            [JsonProperty("generatedAtUtc")] public DateTimeOffset GeneratedAtUtc { get; set; }
            [JsonProperty("warehouseStableId")] public string WarehouseStableId { get; set; } = string.Empty;
            [JsonProperty("totalOrderCount")] public int TotalOrderCount { get; set; }
            [JsonProperty("isTruncated")] public bool IsTruncated { get; set; }
            [JsonProperty("workflows")] public WorkflowWire[] Workflows { get; set; } = Array.Empty<WorkflowWire>();
            [JsonProperty("shelves")] public ShelfWire[] Shelves { get; set; } = Array.Empty<ShelfWire>();
            [JsonProperty("tasks")] public TaskWire[] Tasks { get; set; } = Array.Empty<TaskWire>();
            [JsonProperty("npcs")] public NpcWire[] Npcs { get; set; } = Array.Empty<NpcWire>();

            public 마트피킹포장WorldSnapshot ToModel() => new 마트피킹포장WorldSnapshot
            {
                StableId = StableId ?? string.Empty,
                Revision = Revision ?? string.Empty,
                RevisionNumber = RevisionNumber,
                GeneratedAtUtc = GeneratedAtUtc,
                WarehouseStableId = WarehouseStableId ?? string.Empty,
                TotalOrderCount = TotalOrderCount,
                IsTruncated = IsTruncated,
                Workflows = Array.ConvertAll(Workflows ?? Array.Empty<WorkflowWire>(), item => item.ToModel()),
                Shelves = Array.ConvertAll(Shelves ?? Array.Empty<ShelfWire>(), item => item.ToModel()),
                Tasks = Array.ConvertAll(Tasks ?? Array.Empty<TaskWire>(), item => item.ToModel()),
                Npcs = Array.ConvertAll(Npcs ?? Array.Empty<NpcWire>(), item => item.ToModel()),
            };
        }

        private sealed class ShelfWire
        {
            [JsonProperty("stableId")] public string StableId { get; set; } = string.Empty;
            [JsonProperty("seedbedObjectStableId")] public string SeedbedObjectStableId { get; set; } = string.Empty;
            [JsonProperty("warehouseStableId")] public string WarehouseStableId { get; set; } = string.Empty;
            [JsonProperty("locationCode")] public string LocationCode { get; set; } = string.Empty;
            [JsonProperty("accessScopeCode")] public string AccessScopeCode { get; set; } = string.Empty;
            [JsonProperty("stateCode")] public string StateCode { get; set; } = string.Empty;
            [JsonProperty("totalAvailableQuantity")] public int TotalAvailableQuantity { get; set; }
            [JsonProperty("totalReservedQuantity")] public int TotalReservedQuantity { get; set; }
            [JsonProperty("inventoryItemStableIds")] public string[] InventoryItemStableIds { get; set; } = Array.Empty<string>();
            [JsonProperty("productNames")] public string[] ProductNames { get; set; } = Array.Empty<string>();
            [JsonProperty("activeTaskStableIds")] public string[] ActiveTaskStableIds { get; set; } = Array.Empty<string>();
            [JsonProperty("pickApproachWaypointKey")] public string PickApproachWaypointKey { get; set; } = string.Empty;
            [JsonProperty("pickPointWaypointKey")] public string PickPointWaypointKey { get; set; } = string.Empty;
            [JsonProperty("updatedAtUtc")] public DateTimeOffset UpdatedAtUtc { get; set; }
            [JsonProperty("isPresentationReady")] public bool IsPresentationReady { get; set; }

            public 운영자재고Shelf ToModel() => new 운영자재고Shelf
            {
                StableId = StableId ?? string.Empty,
                SeedbedObjectStableId = SeedbedObjectStableId ?? string.Empty,
                WarehouseStableId = WarehouseStableId ?? string.Empty,
                LocationCode = LocationCode ?? string.Empty,
                AccessScopeCode = AccessScopeCode ?? string.Empty,
                StateCode = StateCode ?? string.Empty,
                TotalAvailableQuantity = TotalAvailableQuantity,
                TotalReservedQuantity = TotalReservedQuantity,
                InventoryItemStableIds = InventoryItemStableIds ?? Array.Empty<string>(),
                ProductNames = ProductNames ?? Array.Empty<string>(),
                ActiveTaskStableIds = ActiveTaskStableIds ?? Array.Empty<string>(),
                PickApproachWaypointKey = PickApproachWaypointKey ?? string.Empty,
                PickPointWaypointKey = PickPointWaypointKey ?? string.Empty,
                UpdatedAtUtc = UpdatedAtUtc,
                IsPresentationReady = IsPresentationReady,
            };
        }

        private sealed class WorkflowWire
        {
            [JsonProperty("stableId")] public string StableId { get; set; } = string.Empty;
            [JsonProperty("orderStateCode")] public string OrderStateCode { get; set; } = string.Empty;
            [JsonProperty("currentStageCode")] public string CurrentStageCode { get; set; } = string.Empty;
            [JsonProperty("productLineCount")] public int ProductLineCount { get; set; }
            [JsonProperty("taskCount")] public int TaskCount { get; set; }
            [JsonProperty("completedTaskCount")] public int CompletedTaskCount { get; set; }

            public 마트피킹포장Workflow ToModel() => new 마트피킹포장Workflow
            {
                StableId = StableId ?? string.Empty,
                OrderStateCode = OrderStateCode ?? string.Empty,
                CurrentStageCode = CurrentStageCode ?? string.Empty,
                ProductLineCount = ProductLineCount,
                TaskCount = TaskCount,
                CompletedTaskCount = CompletedTaskCount,
            };
        }

        private sealed class TaskWire
        {
            [JsonProperty("stableId")] public string StableId { get; set; } = string.Empty;
            [JsonProperty("workflowStableId")] public string WorkflowStableId { get; set; } = string.Empty;
            [JsonProperty("orderLineStableId")] public string OrderLineStableId { get; set; } = string.Empty;
            [JsonProperty("inventoryItemStableId")] public string InventoryItemStableId { get; set; } = string.Empty;
            [JsonProperty("previousTaskStableId")] public string PreviousTaskStableId { get; set; } = string.Empty;
            [JsonProperty("nextTaskStableId")] public string NextTaskStableId { get; set; } = string.Empty;
            [JsonProperty("taskKindCode")] public string TaskKindCode { get; set; } = string.Empty;
            [JsonProperty("productName")] public string ProductName { get; set; } = string.Empty;
            [JsonProperty("sku")] public string Sku { get; set; } = string.Empty;
            [JsonProperty("quantity")] public int Quantity { get; set; }
            [JsonProperty("locationCode")] public string LocationCode { get; set; } = string.Empty;
            [JsonProperty("locationMappingStateCode")] public string LocationMappingStateCode { get; set; } = string.Empty;
            [JsonProperty("toteStableId")] public string ToteStableId { get; set; } = string.Empty;
            [JsonProperty("packingStationWaypointKey")] public string PackingStationWaypointKey { get; set; } = string.Empty;
            [JsonProperty("statusCode")] public string StatusCode { get; set; } = string.Empty;
            [JsonProperty("activityCode")] public string ActivityCode { get; set; } = string.Empty;
            [JsonProperty("isPresentationReady")] public bool IsPresentationReady { get; set; }
            [JsonProperty("updatedAtUtc")] public DateTimeOffset UpdatedAtUtc { get; set; }

            public 마트피킹포장Task ToModel() => new 마트피킹포장Task
            {
                StableId = StableId ?? string.Empty,
                WorkflowStableId = WorkflowStableId ?? string.Empty,
                OrderLineStableId = OrderLineStableId ?? string.Empty,
                InventoryItemStableId = InventoryItemStableId ?? string.Empty,
                PreviousTaskStableId = PreviousTaskStableId ?? string.Empty,
                NextTaskStableId = NextTaskStableId ?? string.Empty,
                TaskKindCode = TaskKindCode ?? string.Empty,
                ProductName = ProductName ?? string.Empty,
                Sku = Sku ?? string.Empty,
                Quantity = Quantity,
                LocationCode = LocationCode ?? string.Empty,
                LocationMappingStateCode = LocationMappingStateCode ?? string.Empty,
                ToteStableId = ToteStableId ?? string.Empty,
                PackingStationWaypointKey = PackingStationWaypointKey ?? string.Empty,
                StatusCode = StatusCode ?? string.Empty,
                ActivityCode = ActivityCode ?? string.Empty,
                IsPresentationReady = IsPresentationReady,
                UpdatedAtUtc = UpdatedAtUtc,
            };
        }

        private sealed class NpcWire
        {
            [JsonProperty("stableId")] public string StableId { get; set; } = string.Empty;
            [JsonProperty("sourceTaskStableId")] public string SourceTaskStableId { get; set; } = string.Empty;
            [JsonProperty("roleCode")] public string RoleCode { get; set; } = string.Empty;
            [JsonProperty("routeCode")] public string RouteCode { get; set; } = string.Empty;
            [JsonProperty("currentWaypointKey")] public string CurrentWaypointKey { get; set; } = string.Empty;
            [JsonProperty("destinationWaypointKey")] public string DestinationWaypointKey { get; set; } = string.Empty;
            [JsonProperty("activityCode")] public string ActivityCode { get; set; } = string.Empty;

            public 마트피킹포장Npc ToModel() => new 마트피킹포장Npc
            {
                StableId = StableId ?? string.Empty,
                SourceTaskStableId = SourceTaskStableId ?? string.Empty,
                RoleCode = RoleCode ?? string.Empty,
                RouteCode = RouteCode ?? string.Empty,
                CurrentWaypointKey = CurrentWaypointKey ?? string.Empty,
                DestinationWaypointKey = DestinationWaypointKey ?? string.Empty,
                ActivityCode = ActivityCode ?? string.Empty,
            };
        }
    }
}
