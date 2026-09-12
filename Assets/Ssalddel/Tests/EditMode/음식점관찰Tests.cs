using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "음식점 UI 객체·명령 결속·프로필 저장 격리를 EditMode에서 검증한다.",
        Boundary = "실제 Game View·Play Mode 입력 완주·Hosted 증거가 아니다.")]
    public sealed class 음식점관찰Tests
    {
        private GameObject root;
        private string previousProfile;
        private string saves;
        [SetUp] public void SetUp()
        {
            previousProfile = Environment.GetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER");
            saves = Path.Combine(Path.GetTempPath(), "restaurant-unity-tests", Guid.NewGuid().ToString("N"));
            SimulationWorldLocalRuntimeScope.Reset();
        }
        [TearDown] public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            SimulationWorldLocalRuntimeScope.Reset();
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", previousProfile);
        }
        private (음식점관찰SceneController controller, 음식점관찰View view) CreateUi()
        {
            root = new GameObject("음식점 시험");
            var view = new GameObject("카드", typeof(RectTransform)).AddComponent<음식점관찰View>();
            view.transform.SetParent(root.transform, false);
            return (root.AddComponent<음식점관찰SceneController>(), view);
        }
        [Test] public async Task 카드버튼은_실제Core를호출하고_표시와저장을재조회한다()
        {
            var scope = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.RestaurantObserver, saves);
            var (controller, view) = CreateUi();
            await controller.InitializeAsync(scope.Runtime.Sessions, scope.Runtime, scope.BusinessWorkflow.Orders, scope.SessionStableId, view);
            Assert.That(controller.카드.주문목록, Is.Empty);
            Assert.That(view.자리입력.text, Is.EqualTo("1"));
            view.주문버튼.onClick.Invoke();
            Assert.That(controller.카드.주문목록.Length, Is.EqualTo(1));
            Assert.That(controller.카드.주문목록[0].표시상태, Is.EqualTo("접수 대기"));
            view.진행버튼.onClick.Invoke();
            Assert.That(controller.카드.현재Tick, Is.EqualTo(1));
            await controller.진행Async(); await controller.진행Async();
            Assert.That(controller.카드.주문목록[0].표시상태, Is.EqualTo("픽업 준비"));
            await controller.저장Async();
            Assert.That(view.표시안내, Does.Contain("저장했습니다"));
            var tick = controller.카드.현재Tick;
            SimulationWorldLocalRuntimeScope.Reset();
            var restored = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.RestaurantObserver, saves);
            Assert.That(restored.RestoredFromSlot, Is.True);
            var state = await restored.Runtime.Sessions.GetAsync(restored.SessionStableId);
            Assert.That(state.CurrentTick, Is.EqualTo(tick));
            Assert.That(state.FoodDeliveries.Length, Is.EqualTo(1));
        }
        [Test] public void 프로필은_명시선택하고_기존슬롯과분리하며_다른프로필혼합을거부한다()
        {
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", "1");
            Assert.That(SimulationWorldLocalRuntimeScope.DetectLaunchProfile(), Is.EqualTo(Nature생존LaunchProfile.RestaurantObserver));
            var scope = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.RestaurantObserver, saves);
            Assert.That(scope.SlotStableId, Is.EqualTo(음식점관찰표본.SlotId));
            Assert.Throws<InvalidOperationException>(() => SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.PlayableDefault, saves));
            Environment.SetEnvironmentVariable("SSALDDEL_RESTAURANT_OBSERVER", null);
            Assert.That(SimulationWorldLocalRuntimeScope.DetectLaunchProfile(), Is.Not.EqualTo(Nature생존LaunchProfile.RestaurantObserver));
        }
        [Test] public async Task 조회중_중복입력을막고_Controller제거시_이벤트를해제한다()
        {
            var scope = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.RestaurantObserver, saves);
            var (controller, view) = CreateUi();
            var policy = new 지연조회(scope.Runtime);
            await controller.InitializeAsync(scope.Runtime.Sessions, policy, scope.BusinessWorkflow.Orders, scope.SessionStableId, view);
            policy.Delay = new TaskCompletionSource<bool>();
            var pending = controller.새로고침Async();
            Assert.That(controller.처리중, Is.True);
            Assert.That(view.주문버튼.interactable, Is.False);
            await controller.주문추가Async();
            policy.Delay.SetResult(true); await pending;
            Assert.That(controller.카드.주문목록, Is.Empty);
            Assert.That(view.주문버튼.interactable, Is.True);
            UnityEngine.Object.DestroyImmediate(controller);
            var reads = policy.Reads;
            view.조회버튼.onClick.Invoke();
            Assert.That(policy.Reads, Is.EqualTo(reads));
        }
        private sealed class 지연조회 : ISimulationNpcPolicyRuntime
        {
            private readonly ISimulationNpcPolicyRuntime inner;
            public int Reads;
            public Exception Error;
            public TaskCompletionSource<bool> Delay;
            public 지연조회(ISimulationNpcPolicyRuntime value) { inner = value; }
            public async ValueTask<경영SimulationSessionSnapshot> GetPolicySessionAsync(string id, CancellationToken token = default)
            { Reads++; if (Delay != null) await Delay.Task; if (Error != null) throw Error; return await inner.GetPolicySessionAsync(id, token); }
            public ValueTask<경영SimulationSessionSnapshot> UpdateNpcPolicyAsync(string id, SimulationNpcPolicyChangeRequest request, CancellationToken token = default)
                => inner.UpdateNpcPolicyAsync(id, request, token);
        }
        [Test] public async Task 충돌은_최신설정을다시표시하고_조회실패는_편집을차단한다()
        {
            var scope = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.RestaurantObserver, saves);
            var (controller, view) = CreateUi();
            var policy = new 지연조회(scope.Runtime);
            await controller.InitializeAsync(scope.Runtime.Sessions, policy, scope.BusinessWorkflow.Orders, scope.SessionStableId, view);
            var other = new 음식점정책카드Presenter(scope.Runtime, scope.SessionStableId, 음식점관찰표본.FacilityId);
            await other.조회Async(); await other.적용Async("other-edit", other.판본, 3, 2, true);
            view.자리입력.text = "2";
            await controller.적용Async();
            Assert.That(view.자리입력.text, Is.EqualTo("3"));
            Assert.That(view.표시안내, Does.Contain("처리 실패"));
            policy.Error = new InvalidOperationException("test read failure");
            await controller.새로고침Async();
            Assert.That(view.적용버튼.interactable, Is.False);
            Assert.That(view.진행버튼.interactable, Is.False);
            Assert.That(view.저장버튼.interactable, Is.False);
            Assert.That(controller.카드.조회됨, Is.False);
            Assert.That(view.조회버튼.interactable, Is.True);
            policy.Error = null;
            await controller.새로고침Async();
            Assert.That(view.적용버튼.interactable, Is.True);
        }
    }
}
