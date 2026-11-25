using System;
using System.Collections;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 다음 할 일의 방향 촬영 (2026-11-16). 메모리에서만 연 회차이고 사용자 저장을 읽지도 쓰지도 않는다(방향 촬영과 같음).
    //  - 화(맡김 꺼짐)에서 고를 일이 생길 때까지 시계를 넘긴다(커널이 올린 신호 · 사슬 행동만).
    //  - 단추 → 안내판 → 정확한 창 → 계획 담기(한 번 더 담아 바꾸기)까지 업무 지문이 그대로인지 본다.
    //  - 턴 넘기기: 고르지 않은 일이 있으면 미루기 확인을 찍고 「이번 턴은 미루고 넘기기」와 같은 길로 넘긴다.
    //    계획한 한 건만 한 번 보내졌는지, 중으로 돌아왔는지, 결과 칸이 그 원 ID를 읽는지 본다.
    public sealed partial class SagajeongDispatchView
    {
        private string 할일지문()
        {
            var kernel = engine?.Kernel;
            var orders = state?.orders == null ? string.Empty
                : string.Join(",", state.orders.Select(order => order.id + ":" + order.stage + ":" + order.driverId));
            var supply = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders.Count ?? -1;
            var receipts = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations)?.Receipts;
            var receiptText = receipts == null ? string.Empty : string.Join(",", receipts.Select(item => item.Id + ":" + item.Stage));
            var demand = ModuleSnapshot<DongDemandSnapshot>(BusinessIds.DongDemand);
            return (state?.elapsedMs ?? -1) + "|" + (kernel?.Ledger.Count ?? -1) + "|" + (kernel?.Handoffs.Count ?? -1) + "|" + orders + "|" +
                supply + "|" + receiptText + "|" + (demand?.Funds ?? -1) + "|" + (demand?.Dongs.Sum(dong => dong.Riders + dong.Partners) ?? -1);
        }

        // 지금 계획까지 갈 수 있는 항목인가(촬영이 고를 대상을 정할 때만 쓴다).
        private bool 할일촬영가능(다음할일항목 item)
        {
            if (item == null || item.명령키 == null || item.묶음 == 할일묶음.지켜볼것) return false;
            switch (item.창)
            {
                case 할일창.배차데스크:
                    var order = state?.orders?.FirstOrDefault(o => o.id == item.주문Id);
                    return AssignmentOpen && order != null && order.stage == "Waiting" &&
                        state.couriers.Any(c => CanSelect(c.id) && !MindBlocksAssignment(c.id, out _));
                case 할일창.매장재료:
                    var place = 할일매장자리;
                    var action = item.명령키.Substring(0, item.명령키.IndexOf(':'));
                    return place != null && StoreRole(place) != null && chain.ActionKey == action;
                case 할일창.동관리:
                    var code = item.장소 != null && item.장소.StartsWith("dong:", StringComparison.Ordinal) ? item.장소.Substring(5) : null;
                    var trade = code == null ? null : DongTrade(HjdRegionPrefix + code);
                    return trade != null && (item.명령키.StartsWith("contract:", StringComparison.Ordinal) ? trade.CanContract : trade.CanRecruit);
            }
            return false;
        }

        // 창의 확정 단추가 화에서 부르는 것과 같은 함수로 담는다.
        private bool 할일촬영담기(다음할일항목 item)
        {
            switch (item.창)
            {
                case 할일창.배차데스크:
                {
                    var order = state.orders.FirstOrDefault(o => o.id == item.주문Id);
                    if (order == null) return false;
                    var free = state.couriers.Where(c => CanSelect(c.id) && !MindBlocksAssignment(c.id, out _)).Select(c => c.id).ToList();
                    var driver = free.Contains(order.recommendedDriverId) ? order.recommendedDriverId : free.FirstOrDefault();
                    if (driver == null) return false;
                    selectedDriver = driver;
                    주문배차연결(order.storeId, order.id);
                    break;
                }
                case 할일창.매장재료:
                {
                    var supply = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply);
                    if (item.명령키.StartsWith("order:", StringComparison.Ordinal))
                    {
                        if (orderQuantity <= 0) orderQuantity = (int)(supply?.Recommended ?? 1);
                        매장계획담기(계획종류.발주);
                    }
                    else if (item.명령키.StartsWith("inspect:", StringComparison.Ordinal))
                    {
                        var receipt = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations)?.Receipts
                            .LastOrDefault(r => r.Stage == ReceiptStages.Waiting);
                        if (receipt == null) return false;
                        if (inspectReceipt != receipt.Id)
                        {
                            inspectReceipt = receipt.Id;
                            inspectAccept = (int)receipt.SuggestAccept;
                            inspectQuarantine = (int)receipt.SuggestQuarantine;
                            inspectReject = 0;
                        }
                        매장계획담기(계획종류.검수);
                    }
                    else 매장계획담기(계획종류.제조);
                    break;
                }
                case 할일창.동관리:
                {
                    var code = item.장소.Substring(5);
                    동계획담기(HjdRegionPrefix + code, item.명령키.StartsWith("contract:", StringComparison.Ordinal));
                    break;
                }
                default:
                    return false;
            }
            return 턴계획.찾기(item.명령키) != null;
        }

        private bool 할일창열렸나(다음할일항목 item) => item.창 switch
        {
            할일창.매장재료 => StoreDeskVisible && storeDeskTab == StoreTabMaterials,
            할일창.배차데스크 => showDesk,
            할일창.동관리 => DongDeskVisible,
            _ => false,
        };

        private IEnumerator ViewProofNextTodo(string directory, ViewProofReceipt receipt)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 70;
            receipt.todoOffInChecks = !다음할일사용;           // 검증 촬영에서는 꺼져 있다(예전 화면 그대로)
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            selectedBuilding = null;
            showDesk = false;
            mapOnly = false;
            if (!running) BeginRound();
            campaignSpeed = Math.Max(1, campaignSpeed);
            campaignResumeSpeed = Math.Max(1, campaignResumeSpeed);
            var autopilotBefore = autopilotOn;
            autopilotOn = false;                                // 화
            다음할일촬영 = true;
            할일판열림 = false;

            // 고를 일이 생길 때까지 시계를 넘긴다(맡김이 꺼져 있으니 주문이 기다린다).
            for (var step = 0; step < 80 && error == null && Time.realtimeSinceStartupAsDouble < deadline; step++)
            {
                yield return null;
                yield return null;
                if (할일목록.Any(할일촬영가능)) break;
                if (CampaignPolicyOpen)
                {
                    Send(new DispatchRequest { action = "campaign-policy", elapsedMs = state.elapsedMs, policy = "temperance" });
                    yield return Until(() => pending == null, deadline);
                    continue;
                }
                var open = state.campaign == null || state.campaign.phase == "open";
                var target = open || state.campaign.nextBoundaryMs <= state.elapsedMs ? state.elapsedMs + 60_000 : state.campaign.nextBoundaryMs;
                Send(new DispatchRequest { action = "tick", elapsedMs = target });
                yield return Until(() => pending == null, deadline);
            }
            yield return null;
            yield return null;
            receipt.todoCount = 할일수;
            receipt.todoDanger = 위험수;
            receipt.todoChoice = 고를일수;
            receipt.todoWatch = 할일목록.Count(item => item.묶음 == 할일묶음.지켜볼것);
            receipt.todoDockDrawn = 할일단추그림;
            receipt.todoRailHidden = 아래줄걷음 && !아래줄그림;          // 한 단추 순환(11-17): 사업 줄을 걷었나
            receipt.todoButtonFirst = 단추마지막 ?? string.Empty;
            receipt.todoButtonTarget = 단추다음일()?.명령키 ?? string.Empty;
            yield return SaveFrame(directory, "105-todo-dock.png");

            var 지문 = 할일지문();
            할일판열림 = true;
            yield return null;
            yield return null;
            yield return null;
            receipt.todoPanelDrawn = 할일판그림;
            receipt.todoClockApplies = campaignMode && running;
            receipt.todoClockHeld = 턴시계보류 != 0 && campaignSpeed == 0;
            receipt.todoGuideUnchanged = 할일지문() == 지문;
            receipt.todoShortcutChips = 바로가기그림;
            yield return SaveFrame(directory, "106-todo-guide.png");

            // 한 단추 순환: 단추 한 번이 다음 일의 창을 연다
            var 단추첫 = 단추다음일();
            할일단추누름();
            yield return null;
            yield return null;
            yield return null;
            receipt.todoButtonOpened = 단추첫 != null && 할일창열렸나(단추첫) && 할일열린키 == 단추첫.명령키 && 할일지문() == 지문;

            다음할일항목 고른 = null;
            foreach (var item in 할일목록.Where(할일촬영가능).ToList())
            {
                할일열기(item);
                yield return null;
                yield return null;
                yield return null;
                if (!할일창열렸나(item)) continue;
                if (!receipt.todoWindowOpened)
                {
                    receipt.todoWindowOpened = true;
                    receipt.todoWindow = item.창.ToString();
                    receipt.todoWindowUnchanged = 할일지문() == 지문;
                    yield return SaveFrame(directory, "107-todo-window.png");
                }
                if (!할일촬영담기(item)) continue;
                고른 = item;
                break;
            }
            receipt.todoPicked = 고른?.명령키 ?? string.Empty;
            receipt.todoPlanAdded = 고른 != null && 턴계획.Count == 1;
            receipt.todoPlanUnchanged = 할일지문() == 지문;
            if (고른 != null)
            {
                할일촬영담기(고른);                              // 같은 일을 다시 담으면 바뀌고 쌓이지 않는다
                receipt.todoPlanReplaced = 턴계획.Count == 1 && 할일지문() == 지문;
            }
            actorFocus = null;
            selectedBuilding = null;
            for (var frame = 0; frame < 3; frame++) yield return null;
            yield return SaveFrame(directory, "108-todo-plan.png");

            receipt.todoPlanned = 턴계획.Count;
            receipt.todoDeferredExpected = 남은할일.Count;
            if (고른 != null)
            {
                yield return null;
                receipt.todoButtonAfterPlan = 단추상태;
                if (단추상태 == "턴넘기기")
                {
                    할일단추누름();                              // 다 골랐으면 같은 단추가 턴 넘기기
                    receipt.todoButtonPassed = true;
                }
                else 턴넘김요청();
                if (턴넘김확인)
                {
                    receipt.todoDeferAsked = true;
                    yield return null;
                    yield return null;
                    yield return SaveFrame(directory, "109-todo-defer.png");
                    턴넘김확인 = false;                          // 「이번 턴은 미루고 넘기기」와 같은 길
                    StartCoroutine(턴넘기기());
                }
                yield return null;
                yield return Until(() => !턴넘기는중, Time.realtimeSinceStartupAsDouble + 15);
            }
            receipt.todoBackToCenter = autopilotOn;
            yield return null;
            receipt.todoButtonAfterTurn = 단추상태;
            receipt.todoPlanCleared = 턴계획.Count == 0;
            receipt.todoSent = 지난턴결과.Count(r => !r.미룸 && r.실패 == null);
            receipt.todoDeferred = 지난턴결과.Count(r => r.미룸);
            receipt.todoFailure = 지난턴결과.FirstOrDefault(r => r.실패 != null)?.실패 ?? string.Empty;
            var 첫 = 지난턴결과.FirstOrDefault(r => !r.미룸);
            if (첫 != null)
            {
                var 읽음 = 결과읽기(첫);
                receipt.todoResult = 첫.명령키 + " · " + 읽음.상태 + " · " + 읽음.문장;
                receipt.todoResultReadsId = 첫.종류 == 계획종류.배차
                    ? state.orders.Any(order => order.id == 첫.주문Id && order.driverId == 첫.기사)
                    : 첫.종류 != 계획종류.발주 || !string.IsNullOrEmpty(첫.공급주문Id);
            }
            actorFocus = null;
            selectedBuilding = null;
            storeDeskPlace = null;
            dongDeskOpen = false;
            showDesk = false;
            할일판열림 = true;
            for (var frame = 0; frame < 3; frame++) yield return null;
            receipt.todoResultDrawn = 할일판그림;
            receipt.todoClockResumed = 턴시계보류 == 0 && campaignSpeed != 0;
            yield return SaveFrame(directory, "110-todo-result.png");

            // 중에서 단추 한 번: 다음 턴 준비 · 화로
            할일단추누름();
            for (var frame = 0; frame < 3; frame++) yield return null;
            receipt.todoButtonReady = !autopilotOn;
            receipt.todoButtonReadyState = 단추마지막 ?? string.Empty;
            yield return SaveFrame(directory, "111-todo-ready.png");

            // 되돌림: 촬영 밖의 장면은 예전 화면 그대로
            autopilotOn = autopilotBefore;
            다음할일촬영 = false;
            if (턴시계보류 != 0)
            {
                SetCampaignSpeed(턴시계보류);
                턴시계보류 = 0;
            }
            턴계획.비우기();
            지난턴결과.Clear();
            미룬할일.Clear();
            이번턴미룸.Clear();
            할일판열림 = false;
            할일강조 = null;
            할일열린키 = null;
            storeDeskPlace = null;
            dongDeskOpen = false;
            showDesk = false;
            턴넘김확인 = false;
            selectedDriver = null;
            holdProofClock = false;
            yield return null;
        }
    }
}
