using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>같은 Scope의 세션 포트에 명령을 한 번만 보내고 재조회 결과로 화면·모형을 갱신한다.</summary>
    public sealed class 다중센터출고SceneController : MonoBehaviour
    {
        private LocalSimulationRuntime runtime;
        private string sessionId, slotId;
        private 다중센터출고View view;
        private 다중센터출고선반Binding binding;
        private readonly CancellationTokenSource lifetime = new();
        private bool busy, draftActive;
        private string selectedAllocation = "";
        private string message = "출고 상태 준비 중";
        public 출고센터선택Presenter 카드 { get; private set; }
        public 출고피킹지시표시 현재지시 => 카드?.피킹지시.SingleOrDefault(value => value.배분코드 == selectedAllocation);
        public bool 처리중 => busy;

        public async Task InitializeAsync(LocalSimulationRuntime runtime, string sessionId,
            다중센터출고View view, 다중센터출고선반Binding binding, string slotId)
        {
            if (this.view != null) throw new InvalidOperationException("출고 화면은 이미 초기화되어 있습니다.");
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.binding = binding ?? throw new ArgumentNullException(nameof(binding));
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(slotId))
                throw new ArgumentException("세션과 저장 슬롯 식별자가 필요합니다.");
            this.sessionId = sessionId; this.slotId = slotId;
            카드 = new 출고센터선택Presenter(runtime, (id, token) => runtime.Sessions.GetAsync(id, token), sessionId);
            view.구성(); view.입력환경구성();
            view.주문검토요청 += Review; view.센터선택요청 += SelectCenter; view.분할변경요청 += Split;
            view.예약요청 += Reserve; view.지시선택요청 += SelectInstruction; view.피킹요청 += Pick;
            view.포장준비요청 += Pack; view.취소요청 += Cancel; view.조회요청 += Refresh; view.저장요청 += Save;
            busy = true; Render(false);
            try
            {
                await 갱신Async(true, true);
                message = 카드.현재주문상태 == SimulationFulfillmentStateCodes.Reserved
                    || 카드.현재주문상태 == SimulationFulfillmentStateCodes.Picking
                    ? "진행 중인 출고 예약을 불러왔습니다. 남은 선반 지시부터 이어가세요."
                    : 카드.현재주문상태 == SimulationFulfillmentStateCodes.PackReady
                    ? "피킹을 충족한 포장 준비 상태를 불러왔습니다. 새 샘플 주문을 검토할 수 있습니다."
                    : "가상 센터에서 출고를 연습합니다. 주문 검토부터 시작하세요.";
            }
            finally { busy = false; if (!lifetime.IsCancellationRequested) Render(true); }
        }

        public Task 주문검토Async() => Run(async () =>
        {
            if (카드.현재주문상태 == SimulationFulfillmentStateCodes.Reserved || 카드.현재주문상태 == SimulationFulfillmentStateCodes.Picking)
                throw new InvalidOperationException("현재 예약을 마무리하거나 취소한 뒤 새 주문을 검토하세요.");
            draftActive = false;
            var request = 다중센터출고표본.Order("order:sample:" + Guid.NewGuid().ToString("N"));
            request.AllowSplitCenters = view.분할입력.isOn;
            await 카드.주문선택Async(request, lifetime.Token); draftActive = true;
        }, "센터별 재고·담당 조건을 비교했습니다. 미리보기는 재고를 점유하지 않습니다.");

        public Task 센터선택Async(string centerId) => Run(async () =>
        {
            await 초안준비Async();
            await 카드.센터선택Async(string.IsNullOrEmpty(centerId) ? Array.Empty<string>() : new[] { centerId }, lifetime.Token);
        }, "센터 선택 조건으로 출고안을 다시 검토했습니다.");

        public Task 분할출고Async(bool allowed) => Run(async () =>
        {
            await 초안준비Async();
            await 카드.분할출고설정Async(allowed, lifetime.Token);
        }, "분할 허용 조건으로 출고안을 다시 검토했습니다.");

        public Task 예약Async() => Run(async () =>
        {
            await 카드.예약확정Async(Command("reserve"), lifetime.Token); draftActive = false;
        }, "출고 재고를 예약했습니다. 선반 지시와 실제 확인 수량을 확인하세요.");

        public Task 지시선택Async(string allocationId) => Run(() =>
        {
            if (!카드.피킹지시.Any(value => value.배분코드 == allocationId))
                throw new InvalidOperationException("현재 주문의 선반 지시를 선택하세요.");
            selectedAllocation = allocationId; return Task.CompletedTask;
        }, "선택한 지시의 센터와 선반을 강조했습니다.");

        public Task 피킹Async() => Run(async () =>
        {
            var selected = 현재지시 ?? throw new InvalidOperationException("피킹할 선반 지시를 먼저 선택하세요.");
            if (!decimal.TryParse(view.수량입력.text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
                throw new InvalidOperationException("확인 수량은 0보다 큰 수로 입력하세요.");
            await 카드.피킹확인Async(Command("pick"), selected.배분코드,
                view.위치입력.text.Trim(), view.상품입력.text.Trim(), quantity, lifetime.Token);
        }, "입력 수량의 가상 피킹을 확인했습니다. 남은 수량과 보관·예약 값을 다시 읽었습니다.");

        public Task 포장준비Async() => Run(() => 카드.출고준비확인Async(Command("pack-ready"), lifetime.Token),
            "전량 피킹을 충족해 포장 준비로 전환했습니다. 실제 포장 완료·기사 인계·배송 완료는 아닙니다.");

        public Task 취소Async() => Run(() => 카드.예약취소Async(Command("cancel"), lifetime.Token),
            "미피킹 예약을 해제했습니다. 물건을 이미 피킹한 주문은 복구 절차 없이 취소하지 않습니다.");

        public Task 조회Async() => Run(async () =>
        {
            await 카드.조회Async(lifetime.Token); draftActive = false;
        }, "최신 상태를 다시 읽었습니다. 다시 예약하려면 새 출고안을 검토하세요.");

        public Task 저장Async() => Run(async () =>
        {
            var current = await runtime.Sessions.GetAsync(sessionId, lifetime.Token);
            await runtime.Sessions.SaveSlotAsync(sessionId, new SimulationLocalSaveSlotRequest
            {
                SlotStableId = slotId, ExpectedRevision = current.Revision,
            }, lifetime.Token);
        }, "전용 출고 슬롯에 저장했습니다. 다른 프로필의 진행 내용은 변경하지 않았습니다.");

        private async Task 초안준비Async()
        {
            if (카드.현재주문상태 == SimulationFulfillmentStateCodes.Reserved
                || 카드.현재주문상태 == SimulationFulfillmentStateCodes.Picking)
                throw new InvalidOperationException("현재 예약을 마무리하거나 취소한 뒤 센터 조건을 변경하세요.");
            if (draftActive) return;
            var request = 다중센터출고표본.Order("order:sample:" + Guid.NewGuid().ToString("N"));
            request.AllowSplitCenters = view.분할입력.isOn;
            await 카드.주문선택Async(request, lifetime.Token); draftActive = true;
        }

        private async Task Run(Func<Task> action, string success)
        {
            if (busy || lifetime.IsCancellationRequested || 카드 == null) return;
            busy = true; Render(false);
            var refreshInputs = true;
            try
            {
                await action(); await 갱신Async(false, false);
                message = 카드.보류사유.Length == 0 ? success : string.Join("\n", 카드.보류사유);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                refreshInputs = false; draftActive = false;
                message = "처리를 확인하지 못했습니다 · " + 오류표시(error) + "\n자동으로 명령을 재전송하지 않았습니다.";
                try { await 갱신Async(true, false); }
                catch { message += " · 최신 조회도 실패하여 입력을 잠갔습니다."; }
                Debug.LogWarning("가상 출고 요청 확인 필요: " + error.Message, this);
            }
            finally
            {
                busy = false;
                if (!lifetime.IsCancellationRequested && view != null) Render(refreshInputs);
            }
        }

        private async Task 갱신Async(bool refreshCard, bool restoreOrder)
        {
            if (refreshCard) await 카드.조회Async(lifetime.Token);
            var snapshot = await runtime.Sessions.GetAsync(sessionId, lifetime.Token);
            if (snapshot.Revision != 카드.현재개정)
            {
                await 카드.조회Async(lifetime.Token);
                snapshot = await runtime.Sessions.GetAsync(sessionId, lifetime.Token);
                draftActive = false;
                if (snapshot.Revision != 카드.현재개정) throw new InvalidOperationException("출고 상태가 바뀌고 있습니다. 다시 조회하세요.");
            }
            if (snapshot.Fulfillment == null) throw new InvalidOperationException("이 회차에 출고 설정이 없습니다.");
            if (!binding.구성됨) binding.구성(snapshot.Fulfillment); else binding.갱신(snapshot.Fulfillment);
            if (restoreOrder && snapshot.Fulfillment.Orders.Length > 0)
            {
                var order = snapshot.Fulfillment.Orders.OrderBy(value => value.StateCode == SimulationFulfillmentStateCodes.Reserved
                    || value.StateCode == SimulationFulfillmentStateCodes.Picking ? 0 : 1).ThenBy(value => value.OrderStableId, StringComparer.Ordinal).First();
                await 카드.확정주문선택Async(order.OrderStableId, lifetime.Token);
                view.분할입력.SetIsOnWithoutNotify(order.Order.AllowSplitCenters);
            }
            if (!카드.피킹지시.Any(value => value.배분코드 == selectedAllocation))
                selectedAllocation = 카드.피킹지시.FirstOrDefault()?.배분코드 ?? "";
            var selected = 현재지시;
            binding.선택강조(selected?.센터코드 ?? "", selected?.위치코드 ?? "");
        }

        private void Render(bool refreshInputs)
        {
            if (view != null) view.표시(카드, busy, message, 현재지시, refreshInputs);
        }
        private static string Command(string kind) => "command:fulfillment-ui:" + kind + ":" + Guid.NewGuid().ToString("N");
        private static string 오류표시(Exception error)
        {
            if (error.Message.Contains("LocationBarcodeMismatch")) return "입력한 위치 코드가 현재 선반과 다릅니다.";
            if (error.Message.Contains("ProductBarcodeMismatch")) return "입력한 상품 코드가 피킹 지시와 다릅니다.";
            if (error.Message.Contains("PickQuantityExceeded")) return "입력 수량이 남은 피킹 수량을 초과합니다.";
            if (error.Message.Contains("PickingIncomplete")) return "아직 피킹하지 않은 물량이 있습니다.";
            if (error.Message.Contains("ExpectedRevision")) return "다른 요청으로 상태가 바뀌었습니다. 최신 결과를 확인하세요.";
            if (error.Message.Contains("PickedOrderCancellation")) return "이미 피킹한 물건은 반납·복구 없이 예약 취소할 수 없습니다.";
            return error.Message;
        }
        private async void Review() => await 주문검토Async();
        private async void SelectCenter(string id) => await 센터선택Async(id);
        private async void Split(bool value) => await 분할출고Async(value);
        private async void Reserve() => await 예약Async();
        private async void SelectInstruction(string id) => await 지시선택Async(id);
        private async void Pick() => await 피킹Async();
        private async void Pack() => await 포장준비Async();
        private async void Cancel() => await 취소Async();
        private async void Refresh() => await 조회Async();
        private async void Save() => await 저장Async();
        private void OnDestroy()
        {
            lifetime.Cancel();
            if (view != null)
            {
                view.주문검토요청 -= Review; view.센터선택요청 -= SelectCenter; view.분할변경요청 -= Split;
                view.예약요청 -= Reserve; view.지시선택요청 -= SelectInstruction; view.피킹요청 -= Pick;
                view.포장준비요청 -= Pack; view.취소요청 -= Cancel; view.조회요청 -= Refresh; view.저장요청 -= Save;
            }
            // Runtime·공용 Scope의 소유자는 Bootstrap입니다. 이 Controller는 취소만 소유합니다.
        }
    }
}
