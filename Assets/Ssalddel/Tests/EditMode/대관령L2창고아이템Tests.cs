using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 대관령L2창고아이템Tests
    {
        [Test]
        public async Task Preview는재고를바꾸지않고_Confirm뒤서버상태사본을다시적용한다()
        {
            var coordinator = new 대관령L2창고아이템Coordinator(
                new 대관령L2창고아이템FixtureAuthorityClient());
            await coordinator.LoadAsync("fixture", CancellationToken.None);
            var before = coordinator.Current;

            await coordinator.PreviewOneAsync(CancellationToken.None);

            Assert.That(coordinator.Preview.CanConfirm, Is.True);
            Assert.That(coordinator.Preview.StateChanged, Is.False);
            Assert.That(coordinator.Current.WorldRevision, Is.EqualTo(before.WorldRevision));
            Assert.That(coordinator.Current.RequiredItemStack(
                대관령L2창고아이템Codes.ItemStackStableId).Quantity, Is.EqualTo(3m));

            await coordinator.ConfirmAsync(
                "command:unity:test:acquire-one", CancellationToken.None);

            Assert.That(coordinator.Current.WorldRevision, Is.EqualTo(1));
            Assert.That(coordinator.Current.RequiredItemStack(
                대관령L2창고아이템Codes.ItemStackStableId).Quantity, Is.EqualTo(2m));
            Assert.That(coordinator.Current.PlayerQuantity(
                대관령L2창고아이템Codes.PlayerStableId,
                "produce.potato.sample"), Is.EqualTo(1m));
            Assert.That(coordinator.Preview, Is.Null);
        }

        [Test]
        public async Task ServerRepository는획득경로만호출하고_Confirm뒤GET으로재조회한다()
        {
            var api = new StubApiClient(new[]
            {
                Ok(InventoryJson(0, 3, 0)),
                Ok(PreviewJson()),
                Ok("{}"),
                Ok(InventoryJson(1, 2, 1)),
            });
            var repository = new 대관령L2창고아이템ServerRepository(api);
            var coordinator = new 대관령L2창고아이템Coordinator(repository);

            await coordinator.LoadAsync("session:test", CancellationToken.None);
            await coordinator.PreviewOneAsync(CancellationToken.None);
            await coordinator.ConfirmAsync("command:unity:test:server", CancellationToken.None);

            Assert.That(api.Requests, Has.Count.EqualTo(4));
            Assert.That(api.Requests[0].Method, Is.EqualTo("GET"));
            Assert.That(api.Requests[1].RelativePath,
                Does.EndWith("/world-inventory/item-acquisition-previews"));
            Assert.That(api.Requests[2].RelativePath,
                Does.EndWith("/world-inventory/item-acquisitions/confirm"));
            Assert.That(api.Requests[2].JsonBody,
                Does.Contain("\"ExpectedRevision\":0"));
            Assert.That(api.Requests[3].Method, Is.EqualTo("GET"));
            Assert.That(coordinator.Current.WorldRevision, Is.EqualTo(1));
        }

        [Test]
        public async Task Presenter는대관령L2_Barn과플레이어관계를고정해표시한다()
        {
            var root = new GameObject("대관령L2창고아이템PresenterTest");
            try
            {
                var presenter = root.AddComponent<대관령L2창고아이템Presenter>();
                Assert.That(presenter.ValidateWiring(), Is.True);
                await presenter.InitializeAsync(
                    new 대관령L2창고아이템FixtureAuthorityClient(), "fixture");

                Assert.That(presenter.상태요약(), Does.Contain("팔레트 3box"));
                await presenter.PreviewOneAsync();
                presenter.CancelPreview();
                Assert.That(presenter.Preview, Is.Null);
                await presenter.PreviewOneAsync();
                await presenter.ConfirmAsync();

                Assert.That(presenter.상태요약(), Does.Contain("팔레트 2box"));
                Assert.That(presenter.상태요약(), Does.Contain("플레이어 1box"));
                Assert.That(presenter.PresentationOnly, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task Pallet표현은서버상태사본수량만큼상자를보인다()
        {
            var root = new GameObject("대관령L2창고TargetTest");
            try
            {
                var volume = new GameObject("InteractionVolume");
                volume.transform.SetParent(root.transform, false);
                var collider = volume.AddComponent<BoxCollider>();
                var highlight = new GameObject("Highlight");
                highlight.transform.SetParent(root.transform, false);
                var visuals = new GameObject[3];
                for (var index = 0; index < visuals.Length; index++)
                {
                    visuals[index] = new GameObject("Box_" + index);
                    visuals[index].transform.SetParent(root.transform, false);
                }
                var target = root.AddComponent<대관령L2창고상호작용TargetView>();
                target.Configure(collider, highlight, visuals);
                var coordinator = new 대관령L2창고아이템Coordinator(
                    new 대관령L2창고아이템FixtureAuthorityClient());
                await coordinator.LoadAsync("fixture", CancellationToken.None);

                target.ApplySnapshot(coordinator.Current);
                Assert.That(target.VisibleQuantity, Is.EqualTo(3));
                Assert.That(visuals, Has.All.Matches<GameObject>(value => value.activeSelf));

                await coordinator.PreviewOneAsync(CancellationToken.None);
                await coordinator.ConfirmAsync(
                    "command:unity:test:target-quantity", CancellationToken.None);
                target.ApplySnapshot(coordinator.Current);

                Assert.That(target.VisibleQuantity, Is.EqualTo(2));
                Assert.That(visuals[0].activeSelf, Is.True);
                Assert.That(visuals[1].activeSelf, Is.True);
                Assert.That(visuals[2].activeSelf, Is.False);
                Assert.That(target.PresentationOnly, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 시선탐색은전환이끝난1인칭과준비된상태에서만허용된다()
        {
            Assert.That(일인칭창고상호작용Controller.CanScan(
                플레이어시점Mode.FirstPerson, false, true), Is.True);
            Assert.That(일인칭창고상호작용Controller.CanScan(
                플레이어시점Mode.FirstPerson, true, true), Is.False);
            Assert.That(일인칭창고상호작용Controller.CanScan(
                플레이어시점Mode.ThirdPerson, false, true), Is.False);
            Assert.That(일인칭창고상호작용Controller.CanScan(
                플레이어시점Mode.FirstPerson, false, false), Is.False);
        }

        private static UnityApiResponse Ok(string body)
            => new() { StatusCode = 200, Body = body };

        private static string InventoryJson(long revision, int containerQuantity,
            int playerQuantity)
            => "{"
               + "\"SessionStableId\":\"session:test\","
               + "\"WorldRevision\":" + revision + ",\"WorldTick\":0,"
               + "\"RuleRevision\":\"world-survival-inventory.pyeongchang-farm.r1\","
               + "\"Buildings\":[{\"BuildingStableId\":\""
               + 대관령L2창고아이템Codes.BuildingStableId + "\","
               + "\"TileKey\":\"" + 대관령L2창고아이템Codes.TileKey + "\","
               + "\"RegionStableId\":\"region:kr:administrative:5176038000\","
               + "\"BuildingEvidenceKindCode\":\"ObservedFixture\","
               + "\"SourceRecordStableId\":\"fixture:vworld-building:51760:sample-warehouse-1\","
               + "\"InteriorSpaceStableId\":\"" + 대관령L2창고아이템Codes.InteriorStableId + "\","
               + "\"InteriorEvidenceKindCode\":\"SimulationScenario\"}],"
               + "\"Containers\":[{\"ContainerStableId\":\""
               + 대관령L2창고아이템Codes.ContainerStableId + "\","
               + "\"BuildingStableId\":\"" + 대관령L2창고아이템Codes.BuildingStableId + "\","
               + "\"InteriorSpaceStableId\":\"" + 대관령L2창고아이템Codes.InteriorStableId + "\","
               + "\"AccessPolicyCode\":\"PublicAcquisition\",\"CapacityUnits\":20,"
               + "\"ManagerPlayerStableIds\":[\"" + 대관령L2창고아이템Codes.PlayerStableId + "\"],"
               + "\"EvidenceKindCode\":\"SimulationScenario\"}],"
               + "\"ContainerItemStacks\":[{\"ItemStackStableId\":\""
               + 대관령L2창고아이템Codes.ItemStackStableId + "\","
               + "\"ContainerStableId\":\"" + 대관령L2창고아이템Codes.ContainerStableId + "\","
               + "\"ItemCode\":\"produce.potato.sample\",\"KoreanName\":\"대관령 감자 상자\","
               + "\"Quantity\":" + containerQuantity + ",\"UnitCode\":\"box\","
               + "\"BuildingItemRelationStableId\":\"relation:sim:pyeongchang-farm:barn-potato-sample\","
               + "\"EvidenceKindCode\":\"SimulationScenario\"}],"
               + "\"Players\":[{\"PlayerStableId\":\"" + 대관령L2창고아이템Codes.PlayerStableId + "\","
               + "\"CurrentBuildingStableId\":\"" + 대관령L2창고아이템Codes.BuildingStableId + "\","
               + "\"InventoryCapacityUnits\":10,\"ManagedContainerStableIds\":[],\"Items\":"
               + (playerQuantity == 0 ? "[]" : "[{\"ItemCode\":\"produce.potato.sample\",\"KoreanName\":\"대관령 감자 상자\",\"Quantity\":1,\"UnitCode\":\"box\"}]")
               + "}],\"Transfers\":[],\"SimulationOnly\":true,\"IsOperationalState\":false}";

        private static string PreviewJson()
            => "{\"SessionStableId\":\"session:test\",\"WorldRevision\":0,"
               + "\"PlayerStableId\":\"" + 대관령L2창고아이템Codes.PlayerStableId + "\","
               + "\"BuildingStableId\":\"" + 대관령L2창고아이템Codes.BuildingStableId + "\","
               + "\"ContainerStableId\":\"" + 대관령L2창고아이템Codes.ContainerStableId + "\","
               + "\"ItemStackStableId\":\"" + 대관령L2창고아이템Codes.ItemStackStableId + "\","
               + "\"ItemCode\":\"produce.potato.sample\",\"RequestedQuantity\":1,"
               + "\"ContainerQuantityBefore\":3,\"ContainerQuantityAfter\":2,"
               + "\"PlayerQuantityBefore\":0,\"PlayerQuantityAfter\":1,"
               + "\"EligibilityStateCode\":\"Allowed\",\"BlockReasonCodes\":[],"
               + "\"CanConfirm\":true,\"StateChanged\":false,"
               + "\"SimulationOnly\":true,\"IsOperationalState\":false}";

        private sealed class StubApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly Queue<UnityApiResponse> responses;
            public readonly List<UnityApiRequest> Requests = new();

            public StubApiClient(IEnumerable<UnityApiResponse> values)
                => responses = new Queue<UnityApiResponse>(values);

            public Task<UnityApiResponse> SendAsync(UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(responses.Dequeue());
            }
        }
    }
}
