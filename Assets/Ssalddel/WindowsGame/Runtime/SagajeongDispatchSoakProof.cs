using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 생명주기 확인(2026-11-15, 사용자: 「구현된 것들을 전반적으로 보고 생명주기 안에서 일이 마무리가 되는지,
    // 플레이 폐 루프가 닫히는지 … 인지된 범위 하에서」).
    //  - 방향 촬영의 끝에서 맡김을 켜고 하루 경계를 넘기며 사흘을 굴린다. 사람 손은 없다.
    //  - 날마다 적는 것: 그날의 주문(마감 요약의 완료 · 대기 · 배달 중 · 전체), 맡김의 배차, 무념이 깨진 기사.
    //  - 시작과 끝을 견주는 것: 음식 배달 Core의 가상 차액, 동 수요 자금(쓸 수 있는 돈), 원장 줄 수, 보류된 인계(받을 사업이 없는 준비 완료).
    //  - 업무 규칙은 바꾸지 않는다. 메모리에서만 연 회차이고 사용자 저장을 읽지도 쓰지도 않는다(방향 촬영과 같음).
    //  - 받은 값은 관찰용이다(실패 판정에 넣지 않음). 결과는 Hongdal docs/AI/Handoffs/2026-11-15-Claude-검증-생명주기와플레이루프.md.
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator ViewProofSoak(string directory, ViewProofReceipt receipt)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 50;
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            if (!running) BeginRound();
            campaignSpeed = Math.Max(1, campaignSpeed);
            campaignResumeSpeed = Math.Max(1, campaignResumeSpeed);
            var autopilotBefore = autopilotOn;
            autopilotOn = true;
            autopilotProofIdle = true;
            autopilotPolicyWait = 1.0;
            var logStart = autopilotLog.Count;

            DongDemandSnapshot SoakDemand() => engine.Kernel.Find(BusinessIds.DongDemand)?.Snapshot() as DongDemandSnapshot;
            int SoakHeld() => (engine.Kernel.Find(BusinessIds.FoodDelivery) as FoodDeliveryModule)?.HeldHandoffs.Count ?? -1;
            int SoakBroken() => state?.couriers?.Count(courier => MindBroken(courier.id)) ?? 0;

            var summaryBefore = engine.CompletedSummary();
            var demandBefore = SoakDemand();
            receipt.soakStartDay = state.campaign?.day ?? 0;
            receipt.soakFundBefore = demandBefore?.Funds ?? -1;
            receipt.soakHeldBefore = SoakHeld();
            var ledgerBefore = engine.Kernel.Ledger.Count;
            var rows = new List<string>();
            var day = receipt.soakStartDay;
            var brokenMost = 0;
            for (var step = 0; step < 240 && error == null && receipt.soakDays < 3 && Time.realtimeSinceStartupAsDouble < deadline; step++)
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
                for (var frame = 0; frame < 4; frame++) yield return null;      // 맡김이 움직일 틈
                yield return Until(() => pending == null && autopilotSentAction == null, deadline);
                brokenMost = Math.Max(brokenMost, SoakBroken());
                var now = state.campaign?.day ?? day;
                if (now == day) continue;
                var summary = state.campaign?.latestSummary;
                var finished = summary != null && summary.waiting == 0 && summary.delivering == 0 && summary.completed == summary.totalOrders;
                if (!finished) receipt.soakStuckDays++;
                rows.Add(summary == null ? $"{day}일차: 마감 요약 없음"
                    : $"{summary.day}일차: 완료 {summary.completed}/{summary.totalOrders} · 대기 {summary.waiting} · 배달 중 {summary.delivering} · 차액 {summary.balance}");
                day = now;
                receipt.soakDays++;
            }

            var summaryAfter = engine.CompletedSummary();
            var demandAfter = SoakDemand();
            receipt.soakEndDay = state.campaign?.day ?? 0;
            receipt.soakRows = string.Join(" | ", rows);
            receipt.soakCompletedAdded = (summaryAfter?.completedOrders ?? 0) - (summaryBefore?.completedOrders ?? 0);
            receipt.soakOrdersAdded = (summaryAfter?.totalOrders ?? 0) - (summaryBefore?.totalOrders ?? 0);
            receipt.soakBalanceAdded = (summaryAfter?.virtualBaseBalance ?? 0) - (summaryBefore?.virtualBaseBalance ?? 0);
            receipt.soakFundAfter = demandAfter?.Funds ?? -1;
            receipt.soakDemandRevenueAdded = (long)Math.Round((demandAfter?.Revenue ?? 0) - (demandBefore?.Revenue ?? 0));
            receipt.soakOpenDongs = demandAfter?.Dongs.Count(dong => dong.Open) ?? 0;
            receipt.soakLedgerAdded = engine.Kernel.Ledger.Count - ledgerBefore;
            receipt.soakHeldAfter = SoakHeld();
            receipt.soakAutopilotAssigns = autopilotLog.CountSince(logStart, 맡김종류.배차);
            receipt.soakMindBrokenMost = brokenMost;
            receipt.soakWaitingNow = state.orders.Count(order => order.stage == "Waiting");

            autopilotOn = autopilotBefore;
            autopilotProofIdle = false;
            autopilotPolicyWait = 맡김Policy.PolicyWaitSeconds;
            holdProofClock = false;
            yield return null;
            yield return SaveFrame(directory, "104-soak-after.png");
        }
    }
}
