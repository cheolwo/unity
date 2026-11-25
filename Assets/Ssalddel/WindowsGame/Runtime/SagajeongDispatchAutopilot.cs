using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 켜 두기 성장 — 맡김의 화면 쪽 뼈대 (2026-11-15). 규칙과 시험값은 맡김Policy.cs.
    //  - 중(관찰)에서는 정상 업무를 맡는다. 화(집중)에서는 사람이 직접 처리한다. 자리 비움 시간은 복귀 카드에 쓴다.
    //  - 하는 일(한 번에 하나): ① 오늘 점검하지 않은 기사의 출발 전 점검 ② 기다린 주문을 엔진이 권한 기사에게 배차
    //    ③ 방침 국면에서 정한 시간 뒤 지난 방침으로 이어 가기. 모두 플레이어가 누르는 것과 같은 명령(check · assign · campaign-policy)이다.
    //  - 못 한 일(빈 기사가 없음)은 기다리는 일에 남긴다. 시한 · 벌점이 없다. 정산 승인 · 예외 · 새 사업 · 확장은 하지 않는다.
    //  - 손이 오래 빈 뒤 돌아오면 「다녀오신 사이」를 한 번 띄운다.
    //  - 오른쪽 위 중화 단추와 설정 「회차」가 같은 선호 값을 읽는다. 이전의 맡김 켜기/끄기도 그대로 읽는다.
    //    검증 촬영(--sagajeong-* · 방향 촬영)에서는 늘 꺼 두고, 방향 촬영의 맡김 장면에서만 켠다.
    public sealed partial class SagajeongDispatchView
    {
        private const string AutopilotPreference = "hongdal.autopilot.v1";
        private static readonly Rect AutopilotChip = new(1052, 10, 118, 28);
        private bool autopilotOn;
        private bool 집중전환대기;
        private bool autopilotPersistent;
        private bool autopilotProofIdle;                // 방향 촬영의 자리 비움 장면에서는 실제 포인터를 무시한다
        private double autopilotPolicyWait = 맡김Policy.PolicyWaitSeconds;
        private double lastHandsTime;
        private Vector2 lastMouse = new(-1f, -1f);
        private double policySeenAt = -1;
        private string autopilotSentAction;            // 맡김이 보낸 요청이 돌아오기를 기다리는 중
        private string autopilotSentText;
        private readonly Dictionary<string, long> orderSeenAt = new();
        private readonly HashSet<string> checkTried = new();
        private readonly 맡김일지 autopilotLog = new();
        private readonly 기다리는일목록 waitingWork = new();
        private int idleMark = -1;                      // 손이 비기 시작할 때의 일지 길이
        private long idleStartGameMs;
        private int idleStartDay;
        private bool returnCardOpen;
        private string returnSummary;
        private string returnDetail;

        private bool AutopilotActing => state != null && error == null &&
            맡김Policy.자동업무가능(autopilotOn, running, campaignSpeed != 0, CampaignPolicyOpen);

        private void LoadAutopilot()
        {
            autopilotPersistent = Array.TrueForAll(Environment.GetCommandLineArgs(), value =>
                !value.StartsWith("--sagajeong-", StringComparison.Ordinal) && !value.StartsWith(ViewProofFlag, StringComparison.Ordinal));
            autopilotOn = autopilotPersistent && PlayerPrefs.GetInt(AutopilotPreference, 1) == 1;
            if (autopilotPersistent) noticeLevel = autopilotOn ? 알림정도.조용히 : 알림정도.보통;
            lastHandsTime = Time.realtimeSinceStartupAsDouble;
        }

        private void SetAutopilot(bool on)
        {
            if (autopilotOn == on) return;
            autopilotOn = on;
            집중전환대기 = false;
            policySeenAt = -1;
            idleMark = -1;
            lastHandsTime = Time.realtimeSinceStartupAsDouble;
            if (on && expansionView) showDesk = false;
            if (on) 관찰업무복귀();
            if (autopilotPersistent) noticeLevel = on ? 알림정도.조용히 : 알림정도.보통;
            if (!autopilotPersistent) return;
            PlayerPrefs.SetInt(AutopilotPreference, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        // 새 회차를 열면 맡김의 기억도 처음부터.
        private void ResetAutopilot()
        {
            orderSeenAt.Clear();
            checkTried.Clear();
            autopilotLog.Clear();
            waitingWork.Clear();
            autopilotSentAction = null;
            autopilotSentText = null;
            policySeenAt = -1;
            idleMark = -1;
            returnCardOpen = false;
            집중전환대기 = false;
        }

        // OnGUI의 맨 앞에서 부른다. 손이 닿으면 빈 시간을 처음으로 돌리고, 오래 비었으면 「다녀오신 사이」를 연다.
        private void NoteHands()
        {
            var current = Event.current;
            if (current == null || autopilotProofIdle) return;
            var moved = false;
            if (current.type == EventType.Repaint)
            {
                var mouse = current.mousePosition;
                moved = lastMouse.x >= 0f && (mouse - lastMouse).sqrMagnitude > 4f;
                lastMouse = mouse;
            }
            if (!moved && current.type is not (EventType.MouseDown or EventType.KeyDown or EventType.ScrollWheel or EventType.MouseDrag)) return;
            HandsBack();
        }

        private void HandsBack()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var idle = now - lastHandsTime;
            lastHandsTime = now;
            if (idleMark < 0) return;
            if (맡김Policy.ShowReturn(idle, autopilotLog.Count - idleMark)) OpenReturnCard(idle);
            idleMark = -1;
        }

        // Update에서 응답이 없을 때 부른다. 무엇이든 보냈으면 true(그 프레임에는 시계를 넘기지 않는다).
        private bool StepAutopilot()
        {
            if (!AutopilotActing || pending != null || autopilotSentAction != null || SettingsVisible) return false;
            if (idleMark < 0)
            {
                idleMark = autopilotLog.Count;
                idleStartGameMs = state.elapsedMs;
                idleStartDay = state.campaign?.day ?? 0;
            }
            var now = Time.realtimeSinceStartupAsDouble;

            // ③ 방침 국면: 정한 시간 동안 고르지 않으면 지난 방침으로 이어 간다.
            if (CampaignPolicyOpen)
            {
                if (policySeenAt < 0) policySeenAt = now;
                if (!맡김Policy.PolicyDue(now - policySeenAt, autopilotPolicyWait)) return false;
                var policy = 맡김Policy.ContinuePolicy(state.campaign?.policy, campaignPolicyCards.Select(card => card.Id).ToList());
                if (policy == null) return false;
                autopilotSentAction = "campaign-policy";
                autopilotSentText = "지난 방침 「" + campaignPolicyCards.First(card => card.Id == policy).Name + "」로 이어 갔습니다";
                ApplyCampaignPolicy(policy);
                if (pending == null) { autopilotSentAction = null; return false; }
                policySeenAt = -1;
                return true;
            }
            policySeenAt = -1;
            if (!AssignmentOpen || AssignmentPending) return false;
            var day = state.campaign?.day ?? 0;
            var couriers = state.couriers ?? Array.Empty<DispatchCourier>();
            var orders = state.orders ?? Array.Empty<DispatchOrder>();
            waitingWork.RemoveWhere(key => key.StartsWith("rider:", StringComparison.Ordinal) &&
                !orders.Any(order => "rider:" + order.id == key && IsWaiting(order)));

            // ① 오늘 점검하지 않은 기사의 출발 전 점검(기사마다 하루 한 번만 해 본다)
            foreach (var courier in couriers)
            {
                var mind = MindOf(courier.id);
                if (ReferenceEquals(mind, NoRiderMind) || mind.CheckedToday || !checkTried.Add(day + ":" + courier.id)) continue;
                var result = engine.Business(new GameCommand
                {
                    OsId = 기사무념Module.Id,
                    Action = "check",
                    CommandId = "autopilot-" + Guid.NewGuid().ToString("N"),
                    ElapsedMs = state.elapsedMs,
                }.With("rider", courier.id));
                if (result.Ok)
                {
                    var text = $"기사 {Letter(courier.id)} 출발 전 점검";
                    autopilotLog.Add(state.elapsedMs, 맡김종류.점검, text);
                    notice = "맡김 · " + text;
                    NoticeAutopilot(맡김종류.점검, courier.id);      // 떴다 사라지는 알림 (SagajeongDispatchNotices.cs)
                }
                return true;
            }

            // ② 기다린 주문을 배차한다
            foreach (var order in orders)
                if (IsWaiting(order) && !orderSeenAt.ContainsKey(order.id)) orderSeenAt[order.id] = state.elapsedMs;
            var due = orders.FirstOrDefault(order => IsWaiting(order) && orderSeenAt.TryGetValue(order.id, out var seen) &&
                맡김Policy.OrderDue(state.elapsedMs - seen));
            if (due == null) return false;
            var free = couriers.Select(courier => courier.id).Where(CanSelect).ToList();
            var rider = 맡김Policy.PickRider(due.recommendedDriverId, free, MindBroken);
            if (rider == null)
            {
                waitingWork.Add("rider:" + due.id, $"{due.storeName} 주문 · 배차할 빈 기사가 없음");
                return false;
            }
            autopilotSentAction = "assign";
            autopilotSentText = $"{due.storeName} 주문을 기사 {Letter(rider)}에게 배차했습니다";
            Send(new DispatchRequest
            {
                action = "assign",
                orderId = due.id,
                driverId = rider,
                revision = due.revision,
                commandId = "autopilot-" + Guid.NewGuid().ToString("N"),
                elapsedMs = Elapsed(now),
            });
            return true;
        }

        private static bool IsWaiting(DispatchOrder order) => order.stage == "Waiting" && string.IsNullOrEmpty(order.driverId);

        private bool MindBroken(string courierId) =>
            engine.BusinessPreview(new GameCommand { OsId = 기사무념Module.Id, Action = "assign" }.With("rider", courierId)).BlockCode ==
            기사무념배차판정.Broken;

        // Update의 응답 처리 끝에서 부른다. 맡김이 보낸 것만 일지에 적는다.
        private void AutopilotReply(string action, bool ok, string message)
        {
            if (action == "assign" && ok) CountAssign(autopilotSentAction == "assign");     // 카1: 그날 직접 · 맡김 배차 수
            if (autopilotSentAction == null || autopilotSentAction != action) return;
            if (ok)
            {
                autopilotLog.Add(state?.elapsedMs ?? 0, action == "assign" ? 맡김종류.배차 : 맡김종류.방침, autopilotSentText);
                notice = "맡김 · " + autopilotSentText;
                if (action == "campaign-policy") NoticeAutopilot(맡김종류.방침, null, autopilotSentText);
            }
            else notice = "맡김 · " + message;
            autopilotSentAction = null;
            autopilotSentText = null;
        }

        private void OpenReturnCard(double idleSeconds)
        {
            var assigned = autopilotLog.CountSince(idleMark, 맡김종류.배차);
            var checks = autopilotLog.CountSince(idleMark, 맡김종류.점검);
            var policies = autopilotLog.CountSince(idleMark, 맡김종류.방침);
            var day = state?.campaign?.day ?? 0;
            var real = idleSeconds >= 3600 ? $"{(int)(idleSeconds / 3600)}시간 {(int)(idleSeconds % 3600 / 60)}분" : $"{(int)(idleSeconds / 60)}분";
            var game = day != idleStartDay ? $"{idleStartDay}일차 → {day}일차" : $"{Math.Max(0, (state?.elapsedMs ?? 0) - idleStartGameMs) / 60000}분";
            returnSummary = $"실제 {real} · 게임 {game}";
            returnDetail = $"맡김이 한 일: 배차 {assigned}건 · 출발 전 점검 {checks}번 · 방침 이어 가기 {policies}번" +
                (waitingWork.Count > 0 ? $"\n기다리는 일 {waitingWork.Count}건 · 시한 없음 — {waitingWork.Items[0].Text}" : string.Empty);
            returnCardOpen = true;
        }

        // 맡김을 바꾸는 단추는 이전 상태와 관계없이 같은 자리에 둔다.
        private void DrawAutopilotChip()
        {
            var 색 = autopilotOn ? Mint : Gold;
            Paint(AutopilotChip, 색, 14);
            Paint(new Rect(AutopilotChip.x + 1, AutopilotChip.y + 1, AutopilotChip.width - 2, AutopilotChip.height - 2), Canvas, 13);
            Paint(new Rect(AutopilotChip.x + 10, AutopilotChip.y + 10, 8, 8), 색, 4);
            Text(new Rect(AutopilotChip.x + 24, AutopilotChip.y, AutopilotChip.width - 26, AutopilotChip.height),
                autopilotOn ? "중 · 관찰" : "화 · 집중", 12, 색, true);
            if (Hit(AutopilotChip))
            {
                SetAutopilot(!autopilotOn);
                if (!autopilotOn) showDesk = true;
            }
        }

        // 업무 명령을 보관하지 않는다. 확인은 모드만 고르고, 원래 화면에서 최신 상태와 수량을 확인해 실행한다.
        private bool 직접업무가능()
        {
            if (!autopilotOn) return true;
            집중전환대기 = true;
            return false;
        }

        private void 집중전환응답(bool 전환)
        {
            if (!집중전환대기) return;
            집중전환대기 = false;
            if (전환) SetAutopilot(false);
        }

        private void 집중전환창그리기()
        {
            var box = new Rect(440, 234, 400, 186);
            Paint(new Rect(0, 0, Width, Height), Fade(Canvas, 0.58f));
            Paint(box, Gold, 10);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 9);
            Text(new Rect(box.x + 20, box.y + 14, 350, 28), "화 · 집중으로 바꿀까요?", 18, Paper, true);
            Text(new Rect(box.x + 20, box.y + 51, 356, 54),
                "전환하면 배차와 출발 점검을 직접 맡습니다. 선택한 업무는 전환 뒤 원래 화면에서 다시 확인해 실행하세요.", 13, Soft, wrap: true);
            if (ExpansionButton(new Rect(box.x + 20, box.yMax - 50, 167, 32), "중 · 관찰 유지", true, Tile))
                집중전환응답(false);
            if (ExpansionButton(new Rect(box.x + 199, box.yMax - 50, 181, 32), "화 · 집중으로 전환", true, Gold))
                집중전환응답(true);
        }

        // 확인을 읽는 동안 시계는 돌아간다. 입력은 이 창만 받고 지도와 뒤쪽 업무 단추는 받지 않는다.
        private bool 집중전환입력처리()
        {
            var 입력 = Event.current;
            if (!집중전환대기 || 입력.type is EventType.Repaint or EventType.Layout) return false;
            if (입력.type == EventType.KeyDown && 입력.keyCode == KeyCode.Escape)
                집중전환응답(false);
            else
                집중전환창그리기();
            if (입력.type != EventType.Used) 입력.Use();
            return true;
        }

        // 「다녀오신 사이」. 닫기 한 번으로 끝난다. 읽지 않아도 된다.
        private void DrawAutopilotReturn()
        {
            if (!returnCardOpen || mapOnly) return;
            var box = new Rect(440, 236, 400, 206);
            Paint(box, Rim, 10);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 9);
            Text(new Rect(box.x + 18, box.y + 12, 220, 26), "다녀오신 사이", 17, Paper, true);
            Text(new Rect(box.x + 18, box.y + 40, box.width - 36, 20), returnSummary, 11, Soft);
            Text(new Rect(box.x + 18, box.y + 66, box.width - 36, 70), returnDetail, 12, Paper, wrap: true);
            Text(new Rect(box.x + 18, box.yMax - 50, 250, 40), "직접 경영하면 더 빨리 자라요.\n맡김은 안전한 쪽만 고릅니다.", 10, Soft, wrap: true);
            if (ExpansionButton(new Rect(box.xMax - 96, box.yMax - 44, 78, 28), "닫기", true, Mint)) returnCardOpen = false;
        }

        // 방향 촬영의 맡김 장면: 손이 오래 빈 것으로 두고, ③ 방침 이어 가기 → ② 배차 → 「다녀오신 사이」 → 끄면 조용함을 본다.
        // 시계는 촬영이 넘긴다(다른 장면과 같은 방식). 사용자 저장은 읽지도 쓰지도 않는 세션이다.
        private IEnumerator ViewProofAutopilot(string directory, ViewProofReceipt receipt)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 30;
            receipt.autopilotDefaultOff = !autopilotOn;
            holdProofClock = true;
            settingsOpen = false;
            actorFocus = null;
            if (!running) BeginRound();
            campaignSpeed = Math.Max(1, campaignSpeed);
            campaignResumeSpeed = Math.Max(1, campaignResumeSpeed);
            autopilotOn = true;
            autopilotProofIdle = true;
            autopilotPolicyWait = 1.0;
            var logStart = autopilotLog.Count;
            receipt.autopilotDayBefore = state.campaign?.day ?? 0;

            // ③ 방침 국면까지 경계마다 시계를 넘긴다. 맡김이 1초 뒤 지난 방침으로 이어 가야 한다.
            for (var step = 0; step < 8 && !CampaignPolicyOpen && error == null && state.campaign != null; step++)
            {
                var target = state.campaign.nextBoundaryMs > state.elapsedMs ? state.campaign.nextBoundaryMs : state.elapsedMs + 600_000;
                Send(new DispatchRequest { action = "tick", elapsedMs = target });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            receipt.autopilotReachedPolicy = CampaignPolicyOpen;
            yield return null;
            yield return SaveFrame(directory, "79-autopilot-policy.png");
            yield return Until(() => !CampaignPolicyOpen && pending == null && autopilotSentAction == null, Time.realtimeSinceStartupAsDouble + 6);
            receipt.autopilotPolicies = autopilotLog.CountSince(logStart, 맡김종류.방침);
            receipt.autopilotDayAfter = state.campaign?.day ?? 0;

            // ② 새 날: 영업이 열릴 때까지 경계를 넘기고, 열리면 2분씩 넘기며 맡김이 배차하는지 본다.
            for (var step = 0; step < 40 && error == null && autopilotLog.CountSince(logStart, 맡김종류.배차) == 0 &&
                 Time.realtimeSinceStartupAsDouble < deadline; step++)
            {
                if (CampaignPolicyOpen)
                {
                    yield return Until(() => !CampaignPolicyOpen && pending == null, Time.realtimeSinceStartupAsDouble + 4);
                    continue;
                }
                var open = state.campaign == null || state.campaign.phase == "open";
                var target = open || state.campaign.nextBoundaryMs <= state.elapsedMs ? state.elapsedMs + 120_000 : state.campaign.nextBoundaryMs;
                Send(new DispatchRequest { action = "tick", elapsedMs = target });
                yield return Until(() => pending == null, deadline);
                for (var frame = 0; frame < 6; frame++) yield return null;      // 맡김이 움직일 틈
                yield return Until(() => pending == null && autopilotSentAction == null, deadline);
            }
            receipt.autopilotAssigned = autopilotLog.CountSince(logStart, 맡김종류.배차);
            receipt.autopilotChecks = autopilotLog.CountSince(logStart, 맡김종류.점검);
            receipt.autopilotWaitingWork = waitingWork.Count;
            yield return null;
            yield return SaveFrame(directory, "80-autopilot-assigned.png");

            // 손이 돌아옴: 「다녀오신 사이」
            autopilotProofIdle = false;
            lastHandsTime = Time.realtimeSinceStartupAsDouble - 맡김Policy.ReturnSummarySeconds - 60;
            HandsBack();
            receipt.autopilotReturnShown = returnCardOpen;
            receipt.autopilotReturnText = returnDetail ?? string.Empty;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "81-autopilot-return.png");
            returnCardOpen = false;

            // 설정 「회차」 탭의 맡겨두기 줄
            settingsOpen = true;
            settingsTab = SettingsTabRound;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "82-autopilot-settings.png");
            settingsOpen = false;
            settingsTab = SettingsTabMap;
            yield return null;

            // 끄면 조용함: 손이 빈 채로 시계를 넘겨도 맡김은 아무것도 보내지 않는다.
            SetAutopilot(false);
            autopilotProofIdle = true;
            var logOff = autopilotLog.Count;
            var ledgerOff = engine.Kernel.Ledger.Count;
            for (var step = 0; step < 4 && error == null; step++)
            {
                Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 300_000 });
                yield return Until(() => pending == null, deadline);
                for (var frame = 0; frame < 4; frame++) yield return null;
            }
            receipt.autopilotOffQuiet = autopilotLog.Count == logOff && autopilotSentAction == null;
            receipt.autopilotLog = string.Join(" | ", autopilotLog.Entries.Skip(logStart).Select(entry => entry.Kind + ":" + entry.Text));
            receipt.autopilotLedgerWhileOff = engine.Kernel.Ledger.Count - ledgerOff;
            autopilotProofIdle = false;
            autopilotPolicyWait = 맡김Policy.PolicyWaitSeconds;
            holdProofClock = false;
        }
    }
}
