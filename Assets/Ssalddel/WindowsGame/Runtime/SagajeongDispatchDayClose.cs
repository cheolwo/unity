using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 카1 첫날 끝내기 (2026-11-15, 사용자: 「진행해줘」 — 카페 하루 운영 시안의 첫 걸음).
    // 시안: Hongdal docs/AI/Handoffs/2026-11-15-Claude-시안-카페하루운영.md 3절.
    //  - 정리한 첫 화면에서 영업 마감 국면(phase "closed")이면 하루 마감 카드를 띄운다: 엔진의 그날 요약(정산 완료 · 대기 · 배송 중 ·
    //    가상 차액), 그날 배차의 직접 · 맡김 수, 「오늘 결산」 · 「다음 영업 시작까지」. 다음 영업으로 넘기는 것은 기존 AdvanceCampaignBoundary다.
    //  - 카드는 읽기만 한다. 금액을 계산하지 않는다(엔진의 latestSummary를 그대로 보인다). 카페 매출 · 배달 정산 금액의 결산 연결은 카4다.
    //  - 맡김이 켜져 있으면 「주문이 기사를 오래 기다립니다」 위험 신호를 화면에서만 주의로 낮추고 맡김이 붙인다고 적는다(커널 신호는 그대로).
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect DayCard = new(440, 232, 400, 214);
        private int dayCardDismissed = -1;
        private int dayCardOpenDay = -1;          // 알림을 쓰면 카드는 마감 알림이나 마감 칩을 눌러야 열린다 (SagajeongDispatchNotices.cs)
        private readonly Dictionary<int, (int Player, int Autopilot)> assignsByDay = new();

        private bool DayCardVisible => TidyScreen && !mapOnly && state?.campaign != null && state.campaign.phase == "closed" &&
            dayCardDismissed != state.campaign.day && !returnCardOpen && !SettingsVisible &&
            (!NoticesOn || dayCardOpenDay == state.campaign.day);

        // 받아들여진 배차를 그날의 직접 · 맡김 수로 센다(AutopilotReply가 부른다).
        private void CountAssign(bool byAutopilot)
        {
            var day = state?.campaign?.day ?? 0;
            assignsByDay.TryGetValue(day, out var count);
            assignsByDay[day] = byAutopilot ? (count.Player, count.Autopilot + 1) : (count.Player + 1, count.Autopilot);
        }

        private (int Player, int Autopilot) AssignsOn(int day) => assignsByDay.TryGetValue(day, out var count) ? count : (0, 0);

        private DispatchDaySummary TodaySummary =>
            state?.campaign?.latestSummary != null && state.campaign.latestSummary.day == state.campaign.day ? state.campaign.latestSummary : null;

        private void DrawDayCloseCard()
        {
            if (!DayCardVisible) return;
            var campaign = state.campaign;
            var summary = TodaySummary;
            var (player, autopilot) = AssignsOn(campaign.day);
            var box = DayCard;
            Paint(box, Gold, 10);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 9);
            Text(new Rect(box.x + 18, box.y + 12, 260, 26), $"{campaign.day}일차 영업 마감", 17, Paper, true);
            var close = new Rect(box.xMax - 36, box.y + 10, 26, 26);
            Text(close, "×", 18, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                dayCardDismissed = campaign.day;
                dayCardOpenDay = -1;
            }

            Text(new Rect(box.x + 18, box.y + 44, box.width - 36, 20), summary == null ? "마감 집계를 기다립니다." :
                $"정산 완료 {summary.completed} / {summary.totalOrders}건 · 배차 대기 {summary.waiting} · 배송 중 {summary.delivering}", 12, Paper);
            if (summary != null)
                Text(new Rect(box.x + 18, box.y + 66, box.width - 36, 22), $"가상 차액 {summary.balance:N0}원", 14, Mint, true);
            Text(new Rect(box.x + 18, box.y + 92, box.width - 36, 20), $"오늘 배차 · 직접 {player} · 맡김 {autopilot}", 12, Paper);
            Text(new Rect(box.x + 18, box.y + 114, box.width - 36, 36),
                "시간이 흐르면 다음 영업이 저절로 열립니다. 방침을 고르는 날에는 맡김이 지난 방침으로 이어 갑니다.", 10, Soft, wrap: true);
            Text(new Rect(box.x + 18, box.y + 146, box.width - 36, 18), "카페 매출 · 배달 정산 금액은 아직 이 카드에 잇지 않았습니다.", 10, Fade(Soft, 0.75f));

            var report = new Rect(box.x + 18, box.yMax - 42, 120, 28);
            if (ExpansionButton(report, "오늘 결산", true, Tile))
            {
                dailyReportOpen = true;
                dailyReportCache = null;
            }
            var next = new Rect(box.xMax - 178, box.yMax - 42, 160, 28);
            if (ExpansionButton(next, "다음 영업 시작까지", campaign.canFastForward && pending == null, Mint)) PressDayCardNext();
        }

        private void PressDayCardNext() => AdvanceCampaignBoundary();

        // 맡김이 켜져 있으면 배차 대기 위험을 화면에서만 주의로 낮춘다. 빈 기사가 없으면 그렇게 적는다.
        private Signal SoftenForAutopilot(Signal signal)
        {
            if (!autopilotOn || signal.ReasonCode != "dispatch-waiting" || !signal.IsDanger) return signal;
            var anyFree = state?.couriers != null && state.couriers.Any(courier => CanSelect(courier.id));
            return new Signal
            {
                Id = signal.Id, OsId = signal.OsId, PlaceId = signal.PlaceId, Severity = SignalSeverities.Caution,
                ReasonCode = signal.ReasonCode, Title = signal.Title,
                Message = anyFree ? "맡김이 기다린 주문에 기사를 붙여요." : "빈 기사가 없어 기다려요 · 맡김이 기다리는 일에 남겨요.",
                ActionLabel = signal.ActionLabel, SinceMs = signal.SinceMs, EscalatedMs = 0, ResolvedMs = signal.ResolvedMs,
            };
        }

        // 방향 촬영: 첫날을 마감해 카드를 보고, 카드의 「다음 영업 시작까지」로 둘째 날을 연다.
        // 그다음 맡김을 켠 채 손이 있는 동안(맡김이 아직 움직이지 않음) 배차 대기가 위험이 되면 화면에서는 주의로 보이는지 본다.
        private IEnumerator ViewProofFirstDay(string directory, ViewProofReceipt receipt)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 30;
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            actorFocus = null;
            if (!running) BeginRound();
            campaignSpeed = Math.Max(1, campaignSpeed);
            campaignResumeSpeed = Math.Max(1, campaignResumeSpeed);
            var firstDay = state.campaign?.day ?? 0;

            // 첫날 마감: 영업 중이면 다음 경계(마감)까지
            for (var step = 0; step < 4 && error == null && state.campaign != null && state.campaign.phase != "closed"; step++)
            {
                if (CampaignPolicyOpen) break;
                var target = state.campaign.nextBoundaryMs > state.elapsedMs ? state.campaign.nextBoundaryMs : state.elapsedMs + 600_000;
                Send(new DispatchRequest { action = "tick", elapsedMs = target });
                yield return Until(() => pending == null, deadline);
                yield return null;
            }
            yield return null;
            yield return null;
            receipt.dayCardShown = DayCardVisible;
            receipt.dayCardDay = state.campaign?.day ?? 0;
            var summary = TodaySummary;
            receipt.dayCardCompleted = summary?.completed ?? -1;
            receipt.dayCardTotal = summary?.totalOrders ?? -1;
            receipt.dayCardBalance = summary?.balance ?? int.MinValue;
            (receipt.dayCardPlayerAssigns, receipt.dayCardAutopilotAssigns) = AssignsOn(receipt.dayCardDay);
            yield return SaveFrame(directory, "83-day-close.png");

            // 카드의 「다음 영업 시작까지」(단추가 눌리는 때를 기다린다)
            yield return Until(() => state.campaign != null && state.campaign.canFastForward && pending == null, deadline);
            receipt.dayCardNextEnabled = state.campaign != null && state.campaign.canFastForward && pending == null;
            PressDayCardNext();
            yield return Until(() => pending == null && !campaignJumpPending && state.campaign != null && state.campaign.phase != "closed", deadline);
            yield return null;
            receipt.dayAdvanced = state.campaign != null && state.campaign.day > firstDay && !DayCardVisible;
            receipt.dayAfterCard = state.campaign?.day ?? 0;

            // 화에서 대기 위험을 만든 뒤, 같은 시점의 신호를 중에서 어떻게 보여주는지 확인한다.
            autopilotOn = false;
            Signal kernelDanger = null;
            for (var step = 0; step < 10 && error == null && kernelDanger == null; step++)
            {
                if (state.campaign != null && state.campaign.phase != "open") break;
                Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 60_000 });
                yield return Until(() => pending == null, deadline);
                yield return null;
                kernelDanger = engine.Kernel.Signals.FirstOrDefault(item => item.ReasonCode == "dispatch-waiting" && item.IsDanger);
            }
            var 영업중 = running;
            running = false;
            autopilotOn = true;
            yield return null;
            yield return null;
            receipt.waitingSignalDangerInKernel = kernelDanger != null;
            var shown = kernelDanger == null ? null : signals.FirstOrDefault(item => item.Id == kernelDanger.Id);
            receipt.waitingSignalSoftShown = shown != null && !shown.IsDanger && shown.Message.StartsWith("맡김", StringComparison.Ordinal) ||
                shown != null && !shown.IsDanger && shown.Message.StartsWith("빈 기사", StringComparison.Ordinal);
            receipt.waitingSignalShownText = shown?.Message ?? string.Empty;
            yield return SaveFrame(directory, "84-signal-soft.png");
            autopilotOn = false;
            running = 영업중;
            holdProofClock = false;
        }
    }
}
