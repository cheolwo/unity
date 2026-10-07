using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Observation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정음식생활관찰UiTests
    {
        [Test]
        public void 관찰모드는_정책진입을거부하고_역사설명과명시추적만제공한다()
        {
            var root = new GameObject("사가정 관찰 UI 시험");
            try
            {
                var phone = root.AddComponent<동네관찰휴대폰View>(); phone.구성();
                var model = new 동네관찰ScreenModel { ObservationOnly = true, PlaceName = "사가정 음식 생활", Time = "00:00",
                    VisitHistory = new[] { new 동네관찰Row { Title = "역사 메뉴", Body = "훈제오리 포케 · 과거 기록" } },
                    Actors = new[] { new 동네관찰Row { Id = "participant:synthetic:a", Title = "주민 A", ActorId = "participant:synthetic:a" } } };
                int commands = 0; string followed = null;
                phone.명령요청 += _ => commands++; phone.주체관찰요청 += id => followed = id;
                phone.표시(model, false, false, false, false, "정지", ""); phone.열기();
                Assert.That(root.GetComponentsInChildren<Button>().Any(b => b.name == "카드 · 정책" || b.name == "카드 · 운영지도"), Is.False);
                phone.이동(동네휴대폰App.정책); Assert.That(phone.SelectedApp, Is.EqualTo(동네휴대폰App.홈));
                phone.이동(동네휴대폰App.운영지도); Assert.That(phone.SelectedApp, Is.EqualTo(동네휴대폰App.홈));
                phone.이동(동네휴대폰App.방문기록);
                Assert.That(root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("훈제오리 포케")), Is.True);
                phone.홈(); phone.이동(동네휴대폰App.NPC, "participant:synthetic:a");
                Assert.That(commands, Is.Zero); Assert.That(followed, Is.Null);
                root.GetComponentsInChildren<Button>().Single(b => b.name == "카드 · 이 NPC 따라가기").onClick.Invoke();
                Assert.That(followed, Is.EqualTo("participant:synthetic:a")); Assert.That(commands, Is.Zero);
                phone.홈(); phone.이동(동네휴대폰App.동네);
                root.GetComponentsInChildren<Button>().Single(b => b.name == "카드 · 재생").onClick.Invoke();
                Assert.That(commands, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator 세백Tick_음식수령반복과_별도슬롯저장재진입은_원장을보존한다()
        {
            var folder = Path.GetFullPath(Path.Combine("artifacts", "sagajeong-food-life-tests", Guid.NewGuid().ToString("N")));
            경영SimulationSessionSnapshot state;
            using (var runtime = LocalSimulationUnityRuntime.Create(folder))
            {
                state = runtime.Sessions.CreateAsync(사가정음식생활관찰Profile.생성(Guid.NewGuid())).AsTask().GetAwaiter().GetResult();
                for (var tick = 0; tick < 300; tick++)
                    state = runtime.Sessions.AdvanceWorldTickAsync(state.SessionStableId, new 경영SimulationTick진행Request
                    { CommandId = "command:sagajeong-unit:" + tick, ExpectedRevision = state.Revision, TickCount = 1 }).AsTask().GetAwaiter().GetResult();
                foreach (var resident in new[] { "participant:synthetic:a", "participant:synthetic:b" })
                    Assert.That(state.FoodDeliveries.Count(order => order.OrdererStableId == resident && order.ReceivedTick.HasValue), Is.GreaterThanOrEqualTo(2), resident);
                var adapter = new 동네관찰RuntimeAdapter(runtime, state.SessionStableId, 사가정음식생활관찰Profile.SlotId, true);
                var revision = state.Revision;
                Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.정책Async(new SimulationNpcPolicyChangeRequest()));
                Assert.That(adapter.조회Async().GetAwaiter().GetResult().Revision, Is.EqualTo(revision));
                var save = adapter.저장Async(revision); var deadline = DateTime.UtcNow.AddSeconds(120);
                while (!save.IsCompleted && DateTime.UtcNow < deadline) yield return null;
                Assert.That(save.IsCompleted, Is.True); save.GetAwaiter().GetResult();
            }
            using (var restored = LocalSimulationUnityRuntime.Create(folder))
            {
                var load = restored.Sessions.LoadSlotAsync(사가정음식생활관찰Profile.SlotId).AsTask().GetAwaiter().GetResult();
                Assert.That(load.Restore.Session.CurrentTick, Is.EqualTo(300));
                Assert.That(load.Restore.Session.Revision, Is.EqualTo(state.Revision));
                Assert.That(System.Text.Json.JsonSerializer.Serialize(load.Restore.Session.WaitingFleet),
                    Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(state.WaitingFleet)));
                Assert.That(load.Restore.Session.FoodDeliveries.Select(order => order.ReceivedTick),
                    Is.EqualTo(state.FoodDeliveries.Select(order => order.ReceivedTick)));
                Assert.Throws<SimulationNotFoundException>(() =>
                    restored.Sessions.LoadSlotAsync(Ssalddel.Unity.Warehouse.가상배달관찰표본.NeighborhoodLifeSlotId).AsTask().GetAwaiter().GetResult());
            }
        }
    }
}
