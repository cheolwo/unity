using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.UrbanMarket;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 마트입고관찰Tests
    {
        private static 마트입고관찰Model Sample(int revision = 1, string status = "보관중", string location = "") => new() {
            WarehouseId = 7, Revision = revision.ToString(), GeneratedAtUtc = DateTimeOffset.Parse("2026-09-08T00:00:00Z").AddSeconds(revision),
            TotalAvailableQuantity = 10, Items = new[] { new 마트입고관찰품목 { Id = "warehouse-inventory:31", ProductName = "감자", AvailableQuantity = 10, Status = status, StorageLocation = location } } };

        [Test] public void 같은품목의_입고검수적치를_원장상태로만판독한다()
        {
            var store = new 마트입고관찰Store();
            store.Accept(Sample()); Assert.That(store.Stage, Is.EqualTo("입고 확인"));
            var id = store.SelectedItemId;
            store.Accept(Sample(2, "검수완료")); Assert.That(store.Stage, Is.EqualTo("검수 확인"));
            store.Accept(Sample(3, "적재완료")); Assert.That(store.Stage, Is.EqualTo("상태 확인 필요"));
            store.Accept(Sample(4, "적재완료", "A-01")); Assert.That(store.Stage, Is.EqualTo("적치 확인"));
            Assert.That(store.SelectedItemId, Is.EqualTo(id));
        }
        [Test] public void 누락은완료가아니고_연결실패는성공자료를보존한다()
        {
            var store = new 마트입고관찰Store(); var first = Sample(); store.Accept(first);
            store.Fail("접속 실패"); Assert.That(store.State, Is.SameAs(first));
            var empty = Sample(2); empty.Items = Array.Empty<마트입고관찰품목>(); store.Accept(empty);
            Assert.That(store.Stage, Is.EqualTo("대기")); Assert.That(store.Item, Is.Null); Assert.That(store.Error, Is.Empty);
        }
        [Test] public void 중복품목_다른창고_오래된조회는거부한다()
        {
            var store = new 마트입고관찰Store(); store.Accept(Sample(3));
            Assert.Throws<InvalidOperationException>(() => store.Accept(Sample(2)));
            var other = Sample(4); other.WarehouseId = 8;
            Assert.Throws<InvalidOperationException>(() => store.Accept(other));
            var duplicate = Sample(4); duplicate.Items = new[] { duplicate.Items[0], duplicate.Items[0] };
            Assert.Throws<FormatException>(() => store.Accept(duplicate));
            Assert.That(store.State.Revision, Is.EqualTo("3"));
        }
        [Test] public void 같은해시재조회는이동을재시작하지않고_다중요청은합친다()
        {
            var store = new 마트입고관찰Store(); Assert.That(store.Accept(Sample()), Is.True);
            Assert.That(store.Accept(Sample()), Is.False);
            Assert.That(store.TryBegin(), Is.True); Assert.That(store.TryBegin(), Is.False);
            store.End(); Assert.That(store.TryBegin(), Is.True);
        }
        [Test] public async Task 창고는_기존권한조회API만호출한다()
        {
            var client = new Client(); var repo = new 마트입고관찰Repository(client, 7);
            var model = await repo.LoadAsync(CancellationToken.None);
            Assert.That(model.Items.Single().ProductName, Is.EqualTo("감자"));
            Assert.That(client.Request.Method, Is.EqualTo("GET")); Assert.That(client.Request.RequiresAuthentication, Is.True);
            Assert.That(client.Request.RelativePath, Does.EndWith("?warehouseId=7"));
            client.Status = 403;
            Assert.ThrowsAsync<InvalidOperationException>(async () => await repo.LoadAsync(CancellationToken.None));
        }
        [Test] public void 범위밖품목과_음수재고는매핑거부한다()
        {
            Assert.Throws<FormatException>(() => 마트입고관찰Repository.Map(Json.Replace("warehouse:7", "warehouse:8"), 7));
            Assert.Throws<FormatException>(() => 마트입고관찰Repository.Map(Json.Replace("\"availableQuantity\":10", "\"availableQuantity\":-1"), 7));
        }
        [TestCase("BuildMart")]
        [TestCase("업무배달동네배경조립")]
        public void 로컬과서버는_같은내부를_한번만조립한다(string build)
        {
            var root = new GameObject("마트 통합 시험"); root.SetActive(false);
            try
            {
                var controller = root.AddComponent<가상배달관찰Controller>(); controller.enabled = false;
                Invoke(controller, build); Invoke(controller, "마트내부조립");
                var children = root.GetComponentsInChildren<Transform>(true);
                Assert.That(children.Count(x => x.name == "마트 작업자"), Is.EqualTo(1));
                Assert.That(children.Single(x => x.name == "적재대 1").position, Is.EqualTo(마트공통공간.선반(0)));
                Assert.That(children.Single(x => x.name == "마트 검수대").position, Is.EqualTo(마트공통공간.검수대));
                if (build == "업무배달동네배경조립") Assert.That(children.Single(x => x.name == "마트 작업자").gameObject.activeSelf, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void 내부경로는통로를따라가며_선반과검수대를침범하지않는다()
        {
            var points = new[] { 마트공통공간.대기, 마트공통공간.입고, 마트공통공간.검수, 마트공통공간.선반접근 };
            var obstacles = new[] { new Rect(-9,-15.75f,2,1.5f), new Rect(-5,-15.75f,2,1.5f), new Rect(-7,-9.6f,2,1.2f), new Rect(-1,-15.75f,2,1.5f) };
            foreach (var from in points) foreach (var to in points)
            {
                var prior = from;
                foreach (var end in 마트공통공간.이동경로(from,to))
                {
                    for (int i=0;i<=20;i++)
                    {
                        var p = Vector3.Lerp(prior,end,i/20f);
                        foreach (var obstacle in obstacles)
                        {
                            var expanded = Rect.MinMaxRect(obstacle.xMin-.3f,obstacle.yMin-.3f,obstacle.xMax+.3f,obstacle.yMax+.3f);
                            Assert.That(expanded.Contains(new Vector2(p.x,p.z)), Is.False, $"{from} -> {to}: {p}");
                        }
                    }
                    prior=end;
                }
            }
        }
        private static void Invoke(object instance,string name) => instance.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,null);
        private const string Json = "{\"stableId\":\"warehouse-zone:7\",\"revision\":\"abc\",\"generatedAtUtc\":\"2026-09-08T00:00:00Z\",\"totalAvailableQuantity\":10,\"inventoryItems\":[{\"stableId\":\"warehouse-inventory:31\",\"warehouseStableId\":\"warehouse:7\",\"productName\":\"감자\",\"status\":\"보관중\",\"storageLocation\":\"\",\"availableQuantity\":10}]}";
        private sealed class Client : IOperationalUnityApiClient
        {
            public UnityApiRequest Request; public long Status = 200;
            public Task<UnityApiResponse> SendAsync(UnityApiRequest request,CancellationToken cancel)
            { Request=request; return Task.FromResult(new UnityApiResponse {StatusCode=Status, Body=Json}); }
        }
    }
}
