using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.BusinessWorkflow;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>화면 입력을 주입받은 세션 포트로 전달하고 변경 뒤 반드시 다시 조회한다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "음식점 입력·명령·최신 조회·표시의 수명을 연결한다.",
        Boundary = "NPC 규칙이나 시간 진행을 Update/애니메이션에서 실행하지 않는다.")]
    public sealed class 음식점관찰SceneController : MonoBehaviour
    {
        private ISimulationSessionRuntime sessions;
        private I주문업무Runtime orders;
        private string sessionId;
        private 음식점관찰View view;
        private 음식점정책카드Presenter card;
        private readonly CancellationTokenSource lifetime = new();
        private bool busy;
        public bool 처리중 => busy;
        public 음식점정책카드Presenter 카드 => card;

        public async Task InitializeAsync(ISimulationSessionRuntime sessionPort, ISimulationNpcPolicyRuntime policies,
            I주문업무Runtime foodOrders, string id, 음식점관찰View target)
        {
            if (view != null) throw new InvalidOperationException("RestaurantObserverAlreadyInitialized");
            sessions = sessionPort; orders = foodOrders; sessionId = id; view = target;
            card = new 음식점정책카드Presenter(policies, id, 음식점관찰표본.FacilityId);
            view.구성();
            view.새로고침요청 += Refresh; view.주문요청 += Order; view.진행요청 += Tick;
            view.저장요청 += Save; view.적용요청 += Apply;
            await 새로고침Async();
        }
        private async void Refresh() => await 새로고침Async();
        private async void Order() => await 주문추가Async();
        private async void Tick() => await 진행Async();
        private async void Save() => await 저장Async();
        private async void Apply() => await 적용Async();
        public Task 새로고침Async() => Run(() => Task.CompletedTask, "최신 상태 조회 완료");
        public Task 주문추가Async() => Run(async () =>
        {
            if (!card.편집가능 || !card.진행가능) throw new InvalidOperationException("음식점 정책과 남은 Tick을 확인하세요.");
            var id = Guid.NewGuid().ToString("N");
            await orders.ConfirmFoodDeliveryAsync(sessionId,
                음식점관찰표본.주문("command:restaurant-order:" + id, card.판본, "food-order:observer:" + id), lifetime.Token);
        }, "모의 주문 등록 · 다음 Tick에 NPC가 접수합니다.");
        public Task 진행Async() => Run(async () =>
        {
            if (!card.조회됨 || !card.진행가능) throw new InvalidOperationException("진행 가능한 상태가 아닙니다.");
            await sessions.AdvanceWorldTickAsync(sessionId, new 경영SimulationTick진행Request
            { CommandId = "command:restaurant-tick:" + Guid.NewGuid().ToString("N"), ExpectedRevision = card.판본, TickCount = 1 }, lifetime.Token);
        }, "1 Tick 진행 · 최신 주문 상태를 확인하세요.");
        public Task 저장Async() => Run(async () =>
        {
            await sessions.SaveSlotAsync(sessionId, new SimulationLocalSaveSlotRequest
            { SlotStableId = 음식점관찰표본.SlotId, ExpectedRevision = card.판본 }, lifetime.Token);
        }, "음식점 전용 슬롯에 저장했습니다. 다시 접속해도 자동 진행하지 않습니다.");
        public Task 적용Async() => Run(async () =>
        {
            if (!int.TryParse(view.자리입력.text, out var slots) || !int.TryParse(view.시간입력.text, out var ticks)
                || slots < 1 || ticks < 1) throw new InvalidOperationException("조리 자리와 시간은 1 이상의 정수로 입력하세요.");
            await card.적용Async("command:restaurant-policy:" + Guid.NewGuid().ToString("N"), card.판본,
                slots, ticks, view.자동배정입력.isOn, lifetime.Token);
        }, "설정 적용 · 이미 배정한 조리 일정은 유지합니다.");

        private async Task Run(Func<Task> command, string success)
        {
            if (busy || lifetime.IsCancellationRequested) return;
            busy = true; view.표시(card, true, "", false);
            var message = success;
            try { await command(); await card.조회Async(lifetime.Token); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                message = "처리 실패 · " + error.Message + " · 최신 상태를 확인하고 다시 선택하세요.";
                try { await card.조회Async(lifetime.Token); }
                catch { message += " / 조회 실패·편집 차단"; }
            }
            finally
            {
                busy = false;
                if (!lifetime.IsCancellationRequested && view != null) view.표시(card, false, message, true);
            }
        }
        private void OnDestroy()
        {
            lifetime.Cancel();
            if (view != null)
            {
                view.새로고침요청 -= Refresh; view.주문요청 -= Order; view.진행요청 -= Tick;
                view.저장요청 -= Save; view.적용요청 -= Apply;
            }
        }
    }
}
