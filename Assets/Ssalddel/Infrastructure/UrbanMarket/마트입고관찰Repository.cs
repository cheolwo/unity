using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.UrbanMarket
{
    /// <summary>기존 권한 창고 API의 읽기 연결. 주문 생성·적치 확정·표본 fallback은 없다.</summary>
    public sealed class 마트입고관찰Repository
    {
        private readonly IOperationalUnityApiClient client;
        private readonly long warehouseId;
        public 마트입고관찰Repository(IOperationalUnityApiClient client, long warehouseId)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            if (warehouseId <= 0) throw new ArgumentOutOfRangeException(nameof(warehouseId));
            this.warehouseId = warehouseId;
        }
        public async Task<마트입고관찰Model> LoadAsync(CancellationToken cancel)
        {
            var response = await client.SendAsync(new UnityApiRequest {
                Method = "GET", RequiresAuthentication = true,
                RelativePath = "api/v1/warehouse-operations/world/zones/warehouse?warehouseId=" + warehouseId.ToString(CultureInfo.InvariantCulture)
            }, cancel);
            cancel.ThrowIfCancellationRequested();
            if (response == null || !response.IsSuccess) throw new InvalidOperationException("WarehouseObservationRequestFailed");
            if (response.Body == null || response.Body.Length > 1024 * 1024) throw new FormatException("WarehouseObservationResponseTooLarge");
            return Map(response.Body, warehouseId);
        }
        public static 마트입고관찰Model Map(string json, long warehouseId)
        {
            var wire = JsonConvert.DeserializeObject<SnapshotWire>(json);
            if (wire == null || wire.stableId != "warehouse-zone:" + warehouseId.ToString(CultureInfo.InvariantCulture)
                || wire.inventoryItems == null || wire.inventoryItems.Length > 50) throw new FormatException("WarehouseObservationScopeMismatch");
            var items = wire.inventoryItems.Select(x => {
                if (x == null || x.warehouseStableId != "warehouse:" + warehouseId.ToString(CultureInfo.InvariantCulture))
                    throw new FormatException("WarehouseObservationItemScopeMismatch");
                return new 마트입고관찰품목 { Id = x.stableId, ProductName = x.productName, Status = x.status,
                    StorageLocation = x.storageLocation, AvailableQuantity = x.availableQuantity };
            }).ToArray();
            var result = new 마트입고관찰Model { WarehouseId = warehouseId, Revision = wire.revision,
                GeneratedAtUtc = wire.generatedAtUtc, TotalAvailableQuantity = wire.totalAvailableQuantity, Items = items };
            마트입고관찰Store.Validate(result);
            return result;
        }
        private sealed class SnapshotWire
        {
            public string stableId, revision;
            public DateTimeOffset generatedAtUtc;
            public int totalAvailableQuantity;
            public ItemWire[] inventoryItems;
        }
        private sealed class ItemWire
        {
            public string stableId, warehouseStableId, productName, status, storageLocation;
            public int availableQuantity;
        }
    }
}
