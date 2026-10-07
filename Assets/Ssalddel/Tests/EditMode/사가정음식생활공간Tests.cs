using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Warehouse;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    /// <summary>동결 자료와 순수 표현 투영 시험. 실제 Play Mode·Game View·실제 통행의 증거가 아니다.</summary>
    public sealed class 사가정음식생활공간Tests
    {
        private static 사가정음식생활공간Data Data() => JsonUtility.FromJson<사가정음식생활공간Data>(Resources.Load<TextAsset>("SagajeongFoodLifeBinding").text);
        private static 사가정음식생활공간Binding Binding(사가정음식생활공간Data data = null)
        {
            var resource = Resources.Load<TextAsset>("SagajeongReference");
            return new 사가정음식생활공간Binding(data ?? Data(), JsonUtility.FromJson<공간참고자료>(resource.text), resource.bytes);
        }
        [Test] public void 현재지도판본과_건물주소와_게임접근점을결속한다()
        {
            var binding = Binding();
            Assert.That(binding.Data.buildingId, Is.EqualTo("osm:way:1256615245"));
            Assert.That(binding.Anchor("restaurant").x, Is.EqualTo(628.35823555).Within(.02));
            Assert.That(binding.Anchor("pickup").z, Is.Not.EqualTo(binding.Anchor("restaurant").z));
            Assert.That(binding.Data.authority, Is.EqualTo("ReferenceOnly;NoTraversal;NoOperational"));
        }
        [TestCase("hash")] [TestCase("revision")] [TestCase("frame")] [TestCase("authority")] [TestCase("address")] [TestCase("road")]
        public void 자료결속을변조하면_시작을거부한다(string change)
        {
            var data = Data();
            if (change == "hash") data.mapSha256 = "wrong";
            if (change == "revision") data.mapRevision = "new";
            if (change == "frame") data.offsetX = 0;
            if (change == "authority") data.authority = "Operational";
            if (change == "address") data.storeAddress = "다른 지점";
            if (change == "road") data.segments.First(s => s.sourceRoadIds.Length > 0).sourceRoadIds[0] = "missing";
            Assert.Throws<ArgumentException>(() => Binding(data));
        }
        [Test] public void 연속접점이틀리면_근처좌표로대체하지않고거부한다()
        {
            var data = Data();
            data.segments.Single(s => s.id == "restaurant-access").gamePoints[0].x += 2;
            Assert.Throws<ArgumentException>(() => Binding(data));
        }
        [Test] public void 미등록좌표와비정상좌표와앵커는_대체하지않는다()
        {
            var binding = Binding();
            Assert.Throws<ArgumentException>(() => binding.Project(999, 999));
            Assert.Throws<ArgumentException>(() => binding.Project(double.NaN, 0));
            Assert.Throws<ArgumentException>(() => binding.Anchor("unknown"));
        }
        [Test] public void 외부Dto수정이_살아있는투영을변경하지않는다()
        {
            var data = Data(); var binding = Binding(data); var before = binding.Project(10, 4);
            data.segments.Single(s => s.id == "restaurant-access").gamePoints[1].x = -1000;
            var exposed = binding.Data; exposed.storeName = "수정";
            exposed.segments.Single(s => s.id == "restaurant-access").gamePoints[1].x = -2000;
            var after = binding.Project(10, 4);
            Assert.That(after.x, Is.EqualTo(before.x)); Assert.That(after.z, Is.EqualTo(before.z));
            var anchor = binding.Anchor("pickup"); anchor.x = -1000;
            Assert.That(binding.Anchor("pickup").x, Is.EqualTo(before.x).Within(.02));
            Assert.That(binding.Data.storeName, Is.EqualTo("샐러리아 샌드위치&샐러드&포케 사가정점"));
        }
        [Test] public void 삼십분Core위치를_업무변경없이투영하고_두주민수령과귀가를보존한다()
        {
            var binding = Binding();
            var core = new 경영SimulationSessionAggregate(가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid(), dayEnabled: true));
            Validate(binding, core.Snapshot());
            for (var tick = 0; tick < 1800; tick++)
            {
                var state = core.Advance(new 경영SimulationTick진행Request { CommandId = "world-binding:" + tick, ExpectedRevision = core.Snapshot().Revision, TickCount = 1 });
                var before = System.Text.Json.JsonSerializer.Serialize(state);
                Validate(binding, state);
                Assert.That(System.Text.Json.JsonSerializer.Serialize(state), Is.EqualTo(before));
                if (state.CurrentTick == 300)
                    foreach (var resident in new[] { "participant:synthetic:a", "participant:synthetic:b" })
                        Assert.That(state.FoodDeliveries.Count(o => o.OrdererStableId == resident && o.ReceivedTick.HasValue), Is.GreaterThanOrEqualTo(2));
            }
            Assert.That(core.Snapshot().IsCompleted, Is.True);
            Assert.That(core.Snapshot().WaitingFleet.NeighborhoodLife.Actors.Where(Focused).Any(a => a.Stage == "Sleeping"), Is.True);
        }
        private static bool Focused(가상생활NpcSnapshot actor) => actor.Role == "Resident" || actor.Role == "Courier" || actor.ActorId == 가상동네생활기준.RestaurantActorId;
        private static void Validate(사가정음식생활공간Binding binding, 경영SimulationSessionSnapshot state)
        {
            foreach (var actor in state.WaitingFleet.NeighborhoodLife.Actors.Where(Focused))
                Assert.DoesNotThrow(() => binding.ProjectActor(actor), state.CurrentTick + ":" + actor.ActorId + ":" + actor.Stage + ":" + actor.X + "," + actor.Z);
            foreach (var driver in state.WaitingFleet.Drivers)
                Assert.DoesNotThrow(() => binding.Project(driver.Courier.VehicleX, driver.Courier.VehicleZ), state.CurrentTick + ":" + driver.Courier.ActorStableId);
        }
    }
}
