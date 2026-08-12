using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Application.Exhibition;
using Ssalddel.Unity.Infrastructure.UrbanMarket;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 마트피킹포장WorldLoadingTests
    {
        private const long WarehouseId = 7;

        [Test]
        public async Task 운영저장소는_인증된마트World경로를읽고_Rack과포장대이동을변환한다()
        {
            var api = new StubOperationalApiClient(Response(5));
            var repository = new OperationalMarketPickingPackingWorldRepository(api);

            var result = await repository.LoadAsync(WarehouseId, CancellationToken.None);

            Assert.That(api.Requests, Has.Count.EqualTo(1));
            Assert.That(api.Requests[0].Method, Is.EqualTo("GET"));
            Assert.That(api.Requests[0].RelativePath,
                Is.EqualTo("api/v1/warehouse-operations/mart/world/picking-packing?warehouseId=7"));
            Assert.That(api.Requests[0].RequiresAuthentication, Is.True);
            Assert.That(result.RevisionNumber, Is.EqualTo(5));
            Assert.That(result.Shelves, Has.Length.EqualTo(1));
            Assert.That(result.Shelves[0].SeedbedObjectStableId,
                Is.EqualTo("seedbed-object:city.operator-inventory-shelf.a"));
            Assert.That(result.Shelves[0].TotalAvailableQuantity, Is.EqualTo(8));
            Assert.That(result.Tasks, Has.Length.EqualTo(1));
            Assert.That(result.Tasks[0].ActivityCode, Is.EqualTo("MovingToPacking"));
            Assert.That(result.Npcs[0].CurrentWaypointKey, Is.EqualTo("market.rack:a-03-02:pick"));
            Assert.That(result.Npcs[0].DestinationWaypointKey,
                Is.EqualTo("market.packing:station-01:input"));
        }

        [Test]
        public void 상태저장소는_낮은개정번호를거부하고_마지막성공상태를보존한다()
        {
            var store = new 마트피킹포장WorldStateStore();
            var accepted = Snapshot(5);
            store.Accept(WarehouseId, accepted);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                store.Accept(WarehouseId, Snapshot(4)));

            Assert.That(exception!.Message, Is.EqualTo("MarketPickingPackingWorldRevisionRegressed"));
            Assert.That(store.Current, Is.SameAs(accepted));
        }

        [Test]
        public void 상태저장소는_같은개정번호의다른내용을충돌로거부한다()
        {
            var store = new 마트피킹포장WorldStateStore();
            store.Accept(WarehouseId, Snapshot(5));
            var conflict = Snapshot(5);
            conflict.Revision = new string('b', 64);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                store.Accept(WarehouseId, conflict));

            Assert.That(exception!.Message, Is.EqualTo("MarketPickingPackingWorldRevisionConflict"));
            Assert.That(store.Current!.Revision, Is.EqualTo(new string('a', 64)));
        }

        [Test]
        public async Task 새로고침실패는_이미수락한상태사본을교체하지않는다()
        {
            var store = new 마트피킹포장WorldStateStore();
            var useCase = new 마트피킹포장WorldLoadUseCase(
                new QueueRepository(Snapshot(5), new InvalidOperationException("NetworkFailed")),
                store);
            await useCase.ExecuteAsync(WarehouseId, CancellationToken.None);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(() =>
                useCase.ExecuteAsync(WarehouseId, CancellationToken.None));

            Assert.That(exception!.Message, Is.EqualTo("NetworkFailed"));
            Assert.That(store.Current!.RevisionNumber, Is.EqualTo(5));
        }

        [Test]
        public void 위치미연결작업을_표현준비상태로받아들이지않는다()
        {
            var snapshot = Snapshot(5);
            snapshot.Tasks[0].LocationMappingStateCode = "LocationUnmapped";
            snapshot.Tasks[0].IsPresentationReady = true;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                snapshot.Validate(WarehouseId));

            Assert.That(exception!.Message,
                Is.EqualTo("MarketPickingPackingUnmappedLocationPresentationForbidden"));
        }

        [Test]
        public void 운영자재고Shelf는_읽기전용상세와_기존연결지점을만든다()
        {
            var shelf = Snapshot(5).Shelves[0];

            var model = new 운영자재고ShelfProjector().Project(shelf);

            Assert.That(model.ObjectStableId,
                Is.EqualTo("seedbed-object:city.operator-inventory-shelf.a"));
            Assert.That(model.FarLabel, Does.Contain("A-03-02"));
            Assert.That(model.NearStatus, Does.Contain("가용 8"));
            Assert.That(model.DetailText, Does.Contain("감자"));
            Assert.That(model.AllowedActionCodes,
                Is.EquivalentTo(new[] { "InspectInventory", "InspectPickTasks" }));
            Assert.That(model.AllowedActionCodes, Does.Not.Contain("CompletePicking"));
            Assert.That(model.RequiredSocketNames,
                Does.Contain("Inventory").And.Contain("ShelfTask").And.Contain("Operator"));
        }

        [Test]
        public void 위치없는재고Shelf는_임의배치를막는다()
        {
            var snapshot = Snapshot(5);
            snapshot.Shelves[0].LocationCode = string.Empty;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                snapshot.Validate(WarehouseId));

            Assert.That(exception!.Message, Is.EqualTo("MarketOperatorInventoryShelfInvalid"));
        }

        private static 마트피킹포장WorldSnapshot Snapshot(long revisionNumber)
        {
            var task = new 마트피킹포장Task
            {
                StableId = "market-fulfillment-task:pick-potato",
                WorkflowStableId = "market-order-workflow:order-1",
                OrderLineStableId = "market-order-line:line-1",
                InventoryItemStableId = "warehouse-inventory:31",
                NextTaskStableId = "market-fulfillment-task:pack-potato",
                TaskKindCode = "Picking",
                ProductName = "감자",
                Sku = "POTATO-2",
                Quantity = 2,
                LocationCode = "A-03-02",
                LocationMappingStateCode = "Mapped",
                ToteStableId = "market-tote:tote-1",
                PackingStationWaypointKey = "market.packing:station-01:input",
                StatusCode = "완료",
                ActivityCode = "MovingToPacking",
                IsPresentationReady = true,
                UpdatedAtUtc = new DateTimeOffset(2026, 8, 12, 1, 0, 0, TimeSpan.Zero),
            };
            return new 마트피킹포장WorldSnapshot
            {
                StableId = "market-picking-packing-zone:7",
                Revision = new string('a', 64),
                RevisionNumber = revisionNumber,
                GeneratedAtUtc = new DateTimeOffset(2026, 8, 12, 1, 1, 0, TimeSpan.Zero),
                WarehouseStableId = "warehouse:7",
                TotalOrderCount = 1,
                Workflows = new[]
                {
                    new 마트피킹포장Workflow
                    {
                        StableId = "market-order-workflow:order-1",
                        OrderStateCode = "출고 예정",
                        CurrentStageCode = "피킹",
                        ProductLineCount = 1,
                        TaskCount = 1,
                    }
                },
                Shelves = new[]
                {
                    new 운영자재고Shelf
                    {
                        StableId = "market-inventory-shelf:a-03-02",
                        SeedbedObjectStableId = "seedbed-object:city.operator-inventory-shelf.a",
                        WarehouseStableId = "warehouse:7",
                        LocationCode = "A-03-02",
                        AccessScopeCode = "OperatorOnly",
                        StateCode = "PickCompleted",
                        TotalAvailableQuantity = 8,
                        TotalReservedQuantity = 2,
                        InventoryItemStableIds = new[] { "warehouse-inventory:31" },
                        ProductNames = new[] { "감자" },
                        ActiveTaskStableIds = new[] { task.StableId },
                        PickApproachWaypointKey = "market.rack:a-03-02:approach",
                        PickPointWaypointKey = "market.rack:a-03-02:pick",
                        UpdatedAtUtc = new DateTimeOffset(2026, 8, 12, 0, 59, 0, TimeSpan.Zero),
                        IsPresentationReady = true,
                    }
                },
                Tasks = new[] { task },
                Npcs = new[]
                {
                    new 마트피킹포장Npc
                    {
                        StableId = "market-npc:picker-1",
                        SourceTaskStableId = task.StableId,
                        RoleCode = "Picker",
                        RouteCode = "market.rack-to-packing",
                        CurrentWaypointKey = "market.rack:a-03-02:pick",
                        DestinationWaypointKey = "market.packing:station-01:input",
                        ActivityCode = "MovingToPacking",
                    }
                },
            };
        }

        private static UnityApiResponse Response(long revisionNumber) => new UnityApiResponse
        {
            StatusCode = 200,
            Body = "{\"stableId\":\"market-picking-packing-zone:7\""
                + ",\"revision\":\"" + new string('a', 64) + "\""
                + ",\"revisionNumber\":" + revisionNumber
                + ",\"generatedAtUtc\":\"2026-08-12T01:01:00Z\""
                + ",\"warehouseStableId\":\"warehouse:7\",\"totalOrderCount\":1,\"isTruncated\":false"
                + ",\"workflows\":[{\"stableId\":\"market-order-workflow:order-1\""
                + ",\"orderStateCode\":\"출고 예정\",\"currentStageCode\":\"피킹\""
                + ",\"productLineCount\":1,\"taskCount\":1,\"completedTaskCount\":1}]"
                + ",\"shelves\":[{\"stableId\":\"market-inventory-shelf:a-03-02\""
                + ",\"seedbedObjectStableId\":\"seedbed-object:city.operator-inventory-shelf.a\""
                + ",\"warehouseStableId\":\"warehouse:7\",\"locationCode\":\"A-03-02\""
                + ",\"accessScopeCode\":\"OperatorOnly\",\"stateCode\":\"PickCompleted\""
                + ",\"totalAvailableQuantity\":8,\"totalReservedQuantity\":2"
                + ",\"inventoryItemStableIds\":[\"warehouse-inventory:31\"]"
                + ",\"productNames\":[\"감자\"]"
                + ",\"activeTaskStableIds\":[\"market-fulfillment-task:pick-potato\"]"
                + ",\"pickApproachWaypointKey\":\"market.rack:a-03-02:approach\""
                + ",\"pickPointWaypointKey\":\"market.rack:a-03-02:pick\""
                + ",\"updatedAtUtc\":\"2026-08-12T00:59:00Z\",\"isPresentationReady\":true}]"
                + ",\"tasks\":[{\"stableId\":\"market-fulfillment-task:pick-potato\""
                + ",\"workflowStableId\":\"market-order-workflow:order-1\""
                + ",\"orderLineStableId\":\"market-order-line:line-1\""
                + ",\"inventoryItemStableId\":\"warehouse-inventory:31\",\"previousTaskStableId\":\"\""
                + ",\"nextTaskStableId\":\"market-fulfillment-task:pack-potato\",\"taskKindCode\":\"Picking\""
                + ",\"productName\":\"감자\",\"sku\":\"POTATO-2\",\"quantity\":2"
                + ",\"locationCode\":\"A-03-02\",\"locationMappingStateCode\":\"Mapped\""
                + ",\"toteStableId\":\"market-tote:tote-1\""
                + ",\"packingStationWaypointKey\":\"market.packing:station-01:input\""
                + ",\"statusCode\":\"완료\",\"activityCode\":\"MovingToPacking\""
                + ",\"isPresentationReady\":true,\"updatedAtUtc\":\"2026-08-12T01:00:00Z\"}]"
                + ",\"npcs\":[{\"stableId\":\"market-npc:picker-1\""
                + ",\"sourceTaskStableId\":\"market-fulfillment-task:pick-potato\""
                + ",\"roleCode\":\"Picker\",\"routeCode\":\"market.rack-to-packing\""
                + ",\"currentWaypointKey\":\"market.rack:a-03-02:pick\""
                + ",\"destinationWaypointKey\":\"market.packing:station-01:input\""
                + ",\"activityCode\":\"MovingToPacking\"}]}"
        };

        private sealed class StubOperationalApiClient : IOperationalUnityApiClient
        {
            private readonly UnityApiResponse response;
            public StubOperationalApiClient(UnityApiResponse value) => response = value;
            public List<UnityApiRequest> Requests { get; } = new List<UnityApiRequest>();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(response);
            }
        }

        private sealed class QueueRepository : I마트피킹포장WorldRepository
        {
            private readonly Queue<object> values;
            public QueueRepository(params object[] results)
                => values = new Queue<object>(results);

            public Task<마트피킹포장WorldSnapshot> LoadAsync(
                long warehouseId,
                CancellationToken cancellationToken)
            {
                var next = values.Dequeue();
                return next is Exception exception
                    ? Task.FromException<마트피킹포장WorldSnapshot>(exception)
                    : Task.FromResult((마트피킹포장WorldSnapshot)next);
            }
        }
    }
}
