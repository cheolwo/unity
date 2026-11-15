using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // r174 「오늘 결산」 보기 (걸음 3, 2026-11-14).
    //  - 왼쪽 아래 「오늘 결산」 단추로 오른쪽 칸을 연다. 설정 칸과 같은 자리이고 둘 가운데 하나만 열린다.
    //  - 오늘(한국표준시)에 적힌 원장 줄을 묶음별로 보여 준다. 진행 중 집계라 일하면 숫자가 늘어난다.
    //  - 「확인」은 읽었다는 표시만 남긴다. 시계·돈·재고·주문은 그대로다(Kernel/일일결산Module).
    //  - 지난 날짜는 읽음 상태와 함께 줄로만 보여 준다. 확인하지 않아도 아무것도 막지 않는다.
    //  - 날짜가 지난 뒤 확인하지 않은 보고가 있으면 「오늘 결산」 단추에 작은 점으로만 알린다(r174 3절). 창을 띄우지 않는다.
    //  - 배달 정산 금액과 지급·수금은 아직 원장에 없다. 0원으로 적지 않고 「연결 전」, 「기록 없음」으로 적는다.
    // 규칙과 저장은 Kernel의 일일결산Policy·일일결산Module에 있다. 이 파일은 읽고 그리기만 한다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect DailyReportButton = new(124, 626, 96, 26);
        private const float DailyRow = 22f;

        private bool dailyReportOpen;
        private 일일결산Snapshot dailyReportCache;
        private float dailyReportCacheAt = -10f;
        private int dailyPendingPast;
        private float dailyPendingAt = -10f;

        private bool DailyReportVisible => TidyScreen && dailyReportOpen && !settingsOpen && expansionView && !mapOnly && !showDesk &&
            actorFocus == null && selectedBuilding == null && !dongPanelOpen && !zonePanelOpen;

        // 오늘의 날짜(한국표준시). PC의 지역 시각 설정과 무관하다.
        private static string TodayKey => 일일결산Policy.DateKey(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        private void ToggleDailyReport()
        {
            dailyReportOpen = !dailyReportOpen;
            if (!dailyReportOpen) return;
            // 오른쪽 칸을 쓰므로 설정 칸과 고른 것은 내려놓는다.
            settingsOpen = false;
            actorFocus = null;
            selectedBuilding = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
            dailyReportCache = null;
        }

        private void DrawDailyReportButton()
        {
            if (NationMapVisible) return;       // 전국 지도에서는 왼쪽이 자료 층이다
            if (ExpansionButton(DailyReportButton, dailyReportOpen ? "결산 닫기" : "오늘 결산", true, Tile)) ToggleDailyReport();
            // 지난 날짜에 확인하지 않은 결산이 있다는 표시. 누르라고 요구하지 않고 아무것도 막지 않는다.
            if (dailyReportOpen || DailyPendingPast(false) <= 0 || Event.current.type != EventType.Repaint) return;
            var dot = new Rect(DailyReportButton.xMax - 9, DailyReportButton.y - 4, 13, 13);
            Paint(dot, Canvas, 6.5f);
            Paint(new Rect(dot.x + 2, dot.y + 2, 9, 9), Gold, 4.5f);
        }

        // 오늘보다 앞선 날짜 가운데 확인하지 않은 보고의 수. 2초에 한 번만 다시 센다(fresh면 바로).
        private int DailyPendingPast(bool fresh)
        {
            if (!fresh && Time.unscaledTime - dailyPendingAt < 2f) return dailyPendingPast;
            dailyPendingAt = Time.unscaledTime;
            var module = engine?.Kernel?.Module<일일결산Module>();
            dailyPendingPast = module == null ? 0 : 일일결산Policy.PendingPast(
                ((일일결산Snapshot)module.Snapshot()).Days.Select(day => (day.Report.DateKey, day.Reading)), TodayKey);
            return dailyPendingPast;
        }

        // 원장을 날짜별로 묶은 사본. 칸이 열려 있는 동안 0.5초에 한 번만 다시 읽는다.
        private 일일결산Snapshot DailyReports()
        {
            if (dailyReportCache != null && Time.unscaledTime - dailyReportCacheAt < 0.5f) return dailyReportCache;
            var module = engine?.Kernel?.Module<일일결산Module>();
            dailyReportCache = module == null ? new 일일결산Snapshot() : (일일결산Snapshot)module.Snapshot();
            dailyReportCacheAt = Time.unscaledTime;
            return dailyReportCache;
        }

        // 원장의 묶음 이름을 화면 말로. 모르는 묶음은 이름 그대로 보여 준다(뜻을 지어내지 않는다).
        private static string DailyBucketName(string bucket)
        {
            switch (bucket)
            {
                case "sales": return "판매 (가상)";
                case "spent": return "쓴 돈 (고용·제휴·엔진)";
                case "supply-payable": return "공급대금 (확정 · 지급 전)";
                default: return bucket;
            }
        }

        private static string DailyReadingName(string reading)
        {
            switch (reading)
            {
                case 일일결산읽음.Read: return "확인함";
                case 일일결산읽음.Changed: return "기록이 더 생김";
                default: return "읽지 않음";
            }
        }

        // 읽었다는 표시만 남긴다. 시계를 진행하지 않고 원장에 줄을 적지 않는다.
        private void ConfirmDailyReport(string dateKey)
        {
            SendBusiness(일일결산Module.Id, 일일결산Module.ConfirmAction, command => command.With("date", dateKey));
            dailyReportCache = null;
            dailyPendingAt = -10f;
        }

        private void DrawDailyReportPanel()
        {
            var panel = DailyReportPanel;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);
            var x = panel.x + 17;
            var width = panel.width - 34;
            var y = panel.y + 12;
            Text(new Rect(x, y, 150, 24), "오늘 결산", 18, Paper, true);
            var close = new Rect(panel.xMax - 35, panel.y + 11, 23, 24);
            Text(close, "×", 19, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                dailyReportOpen = false;
                return;
            }
            y += 28;

            var today = TodayKey;
            var days = DailyReports().Days;
            var todayReport = days.FirstOrDefault(candidate => candidate.Report.DateKey == today);
            Text(new Rect(x, y, width, 18), today + " · 진행 중 집계", 11, Soft);
            y += 24;

            var money = todayReport == null
                ? new List<일일결산줄>()
                : todayReport.Report.Lines.Where(candidate => candidate.Kind == LedgerKinds.Money).ToList();
            if (money.Count == 0)
            {
                Text(new Rect(x, y, width, 18), "오늘 적힌 돈 기록이 없습니다.", 12, Fade(Soft, 0.9f));
                y += DailyRow;
            }
            for (var index = 0; index < money.Count && index < 5; index++)
            {
                var line = money[index];
                Text(new Rect(x, y, width - 104, 18), DailyBucketName(line.Bucket), 12, Paper);
                Text(new Rect(x + width - 104, y, 104, 18), line.Amount.ToString("N0") + "원 · " + line.Count + "건", 12, Paper,
                    align: TextAnchor.MiddleRight);
                y += DailyRow;
            }
            if (money.Count > 5)
            {
                Text(new Rect(x, y, width, 18), "그 밖에 " + (money.Count - 5) + "묶음", 11, Soft);
                y += DailyRow;
            }
            var stockLines = todayReport == null
                ? 0
                : todayReport.Report.Lines.Where(candidate => candidate.Kind == LedgerKinds.Stock).Sum(candidate => candidate.Count);
            if (stockLines > 0)
            {
                Text(new Rect(x, y, width, 18), "재고 이동 " + stockLines + "줄", 11, Soft);
                y += DailyRow;
            }

            y += 4;
            Paint(new Rect(x, y, width, 1), Rim);
            y += 8;
            // 아직 원장에 없는 것. 0원으로 적지 않는다.
            Text(new Rect(x, y, width, 16), "배달 정산 금액 · 연결 전", 10, Fade(Soft, 0.85f));
            y += 16;
            Text(new Rect(x, y, width, 16), "지급·수금 · 기록 없음", 10, Fade(Soft, 0.85f));
            y += 24;

            // 확인: 읽었다는 표시만 남긴다.
            var button = new Rect(x, y, width, 26);
            if (todayReport == null)
            {
                Paint(button, Panel, 6);
                Text(button, "확인할 기록이 아직 없습니다", 11, Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
            }
            else if (todayReport.Reading == 일일결산읽음.Read)
            {
                Paint(button, Panel, 6);
                Text(button, "확인함", 11, Mint, true, TextAnchor.MiddleCenter);
            }
            else if (ExpansionButton(button, todayReport.Reading == 일일결산읽음.Changed ? "기록이 더 생겼습니다 · 다시 확인" : "오늘 결산 확인", true, Tile))
            {
                ConfirmDailyReport(today);
                return;
            }
            y += 36;

            // 지난 날: 줄로만 보여 준다. 확인하지 않아도 아무것도 막지 않는다.
            var past = days.Where(candidate => candidate.Report.DateKey != today && candidate.Report.DateKey != 일일결산Policy.Unlinked)
                .Reverse().Take(3).ToList();
            if (past.Count > 0)
            {
                var waiting = 일일결산Policy.PendingPast(days.Select(candidate => (candidate.Report.DateKey, candidate.Reading)), today);
                Text(new Rect(x, y, width, 16), waiting > 0 ? "지난 날 · 확인 안 한 날 " + waiting + "일 (안 해도 됩니다)" : "지난 날", 11, Soft);
                y += 20;
                foreach (var item in past)
                {
                    var read = item.Reading == 일일결산읽음.Read;
                    Text(new Rect(x, y, 92, 20), item.Report.DateKey, 11, Paper);
                    Text(new Rect(x + 94, y, 86, 20), DailyReadingName(item.Reading), 11, read ? Mint : Gold);
                    if (!read && ExpansionButton(new Rect(x + width - 50, y - 1, 50, 22), "확인", true, Tile))
                    {
                        ConfirmDailyReport(item.Report.DateKey);
                        return;
                    }
                    y += 24;
                }
            }
            var unlinked = days.FirstOrDefault(candidate => candidate.Report.DateKey == 일일결산Policy.Unlinked);
            if (unlinked != null)
                Text(new Rect(x, y, width, 16), "날짜를 모르는 예전 기록 " + unlinked.Report.Entries + "줄", 10, Fade(Soft, 0.8f));

            Text(new Rect(x, panel.yMax - 44, width, 32), "결산은 보고입니다. 확인해도 시간·돈·재고는 그대로입니다.", 10, Fade(Soft, 0.8f), wrap: true);
        }
    }
}
