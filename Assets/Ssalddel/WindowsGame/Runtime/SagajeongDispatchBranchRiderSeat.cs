using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 배달 판 · 기사 자리 첫 조각 (2026-11-27 18:45 사용자: 「기사 자리부터 실제 게임을 만드는 게 나쁘지 않지」).
    //  - 배달 판은 기사로 시작한다. 지도 = 같은 구 지도의 공개판 칸, 그 가운데 사가정 지사(면목3·8동) 둘레.
    //  - 한 턴 = 한 시간: 기다릴 칸을 고르고 「이 칸에서 기다리기」 → 콜 · 번 돈 · 기분 관리. 「쉬기 · 몰라」는 그 턴을 쉬어 기분 관리를 내린다.
    //  - 세 턴(세 시간)마다 지사 한 회차가 지난다(지사경쟁Policy.회차넘기기 — 지사장 판과 같은 판).
    //  - 칸의 주문 = 그 동의 회차 주문을 칸의 건물 수로 나눈 것. 콜 = 우리 지사가 그 동에서 받은 주문 ÷ 기사 수 × 칸의 짙기(가안 식).
    //  - 자리 읽기: Lv 1은 짙기를 세 단계로 흐릿하게, Lv 2(일한 턴 6번)는 또렷하게 + 예상 콜.
    //  - 숫자(배달비 3,400원 · 기분 관리 오르내림)는 예시다. 내 콜이 지사의 받은 주문으로 들어가는 것 · 동료 돕기 · 오르기는 다음 조각.
    public sealed partial class SagajeongDispatchView
    {
        private const string RiderHomeDong = "1126057500";      // 사가정 지사(면목3·8동)
        private const long RiderFare = 3400;
        private const int RiderTurnsPerRound = 3;
        private const int RiderLevelTwoXp = 6;

        private sealed class RiderSeatLog
        {
            public int Turn, Calls;
            public long Earn;
            public string Dong;
            public bool Rested;
            public int Mood;
        }

        private int branchSeat;                              // 0 기사 · 2 지사장
        private bool BranchRiderSeat => BranchBoardOn && branchSeat == 0;
        private int rsTurn = 1, rsMood = 46, rsXp, rsCalmStreak, rsLastCalls = -1, rsWaitCell = -1;
        private long rsLastEarn, rsEarnTotal;
        private readonly List<RiderSeatLog> rsLog = new List<RiderSeatLog>();
        private readonly List<int> rsTargets = new List<int>();
        private float rsTargetsAt = -10f;
        private System.Random rsRandom;
        private int[] rsCells;                                // 16동 안의 칸
        private float[] rsOrders;                             // 칸마다 회차 주문(rsCells와 같은 차례)
        private readonly Dictionary<int, int> rsIndex = new Dictionary<int, int>();
        private float[] rsAverage;                            // 동마다 칸 평균 주문
        private float rsMax = 1f;
        private int rsRound = -1;

        private int RiderSeatLevel => rsXp >= RiderLevelTwoXp ? 2 : 1;

        private void EnsureRiderSeatCells()
        {
            if (branchBoard == null) return;
            if (rsCells == null)
            {
                var list = new List<int>();
                for (var i = 0; i < capitalHexCount; i++)
                    if (BranchSlotOfCell(i) >= 0) list.Add(i);
                rsCells = list.ToArray();
                rsIndex.Clear();
                for (var k = 0; k < rsCells.Length; k++) rsIndex[rsCells[k]] = k;
                rsOrders = new float[rsCells.Length];
                rsAverage = new float[BranchDongs.Length];
                rsRandom = new System.Random(20261127);
            }
            if (rsRound == branchBoard.회차) return;
            rsRound = branchBoard.회차;
            var buildings = new float[BranchDongs.Length];
            var counts = new int[BranchDongs.Length];
            foreach (var cell in rsCells)
            {
                var slot = BranchSlotOfCell(cell);
                buildings[slot] += chBuildings[cell] + 2f;      // 빈 칸에도 조금(길가 · 공원 주문)
                counts[slot]++;
            }
            rsMax = 0.01f;
            Array.Clear(rsAverage, 0, rsAverage.Length);
            for (var k = 0; k < rsCells.Length; k++)
            {
                var cell = rsCells[k];
                var slot = BranchSlotOfCell(cell);
                var dong = branchBoard.동(BranchDongs[slot].Code);
                var orders = dong == null || buildings[slot] <= 0f ? 0f : (float)dong.회차주문 * (chBuildings[cell] + 2f) / buildings[slot];
                rsOrders[k] = orders;
                rsAverage[slot] += orders / Mathf.Max(1, counts[slot]);
                rsMax = Mathf.Max(rsMax, orders);
            }
        }

        private float RiderSeatHeat(int cell)
        {
            EnsureRiderSeatCells();
            return rsOrders != null && rsIndex.TryGetValue(cell, out var k) ? rsOrders[k] / rsMax : 0f;
        }

        // 이 칸에서 한 턴(한 시간) 기다리면 받을 콜(평균)
        private float RiderSeatExpected(int cell)
        {
            EnsureRiderSeatCells();
            if (rsOrders == null || !rsIndex.TryGetValue(cell, out var k)) return 0f;
            var slot = BranchSlotOfCell(cell);
            var seat = branchBoard.자리(BranchMe, BranchDongs[slot].Code);
            if (seat == null || seat.기사 <= 0) return 0.3f;      // 우리 지사가 없는 동: 거의 없다
            var perRider = (float)(seat.받은주문 / seat.기사) / RiderTurnsPerRound * 0.72f;      // 한 시간 평균 3 ~ 4건(가안)
            var factor = Mathf.Pow(Mathf.Clamp(rsOrders[k] / Mathf.Max(0.01f, rsAverage[slot]), 0.25f, 1.8f), 0.8f);
            return perRider * factor;
        }

        private int RiderSeatDefaultCell()
        {
            var slot = BranchSlotOfCode(RiderHomeDong);
            return slot >= 0 ? branchCell[slot] : -1;
        }

        private void RiderSeatWork(int cell)
        {
            var expected = RiderSeatExpected(cell);
            var calls = Mathf.Clamp(Mathf.RoundToInt(expected * (0.75f + (float)rsRandom.NextDouble() * 0.5f)), 0, 9);
            var earn = calls * RiderFare;
            rsMood = Mathf.Clamp(rsMood + (calls <= 3 ? 12 : 5), 0, 100);       // 콜 3건 이하면 크게 오른다 · 달리면 조금씩
            rsXp++;
            rsTargets.Clear();
            var from = new Vector2(chX[cell], chY[cell]);
            for (var guard = 0; guard < 160 && rsTargets.Count < calls; guard++)
            {
                var angle = (float)rsRandom.NextDouble() * Mathf.PI * 2f;
                var reach = 250f + (float)rsRandom.NextDouble() * 800f;
                var target = CapitalHexAt(from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach);
                if (target < 0 || BranchSlotOfCell(target) < 0) continue;
                if (RiderSeatHeat(target) < (float)rsRandom.NextDouble() * 0.6f) continue;      // 주문이 짙은 곳으로 더 간다
                rsTargets.Add(target);
            }
            rsTargetsAt = Time.unscaledTime;
            QueueMyGuRuns(cell, rsTargets);      // 고른 칸 → 픽업 칸 → 전달 칸을 칸으로 이어 나른다
            RiderSeatEndTurn(cell, calls, earn, false);
        }

        private void RiderSeatRest()
        {
            rsMood = Mathf.Clamp(rsMood - 18, 0, 100);
            rsTargets.Clear();
            RiderSeatEndTurn(rsWaitCell, 0, 0, true);
        }

        private void RiderSeatEndTurn(int cell, int calls, long earn, bool rested)
        {
            rsWaitCell = cell;
            rsLastCalls = rested ? -1 : calls;
            rsLastEarn = earn;
            rsEarnTotal += earn;
            rsCalmStreak = rsMood < 60 ? rsCalmStreak + 1 : 0;
            var slot = cell >= 0 ? BranchSlotOfCell(cell) : -1;
            rsLog.Add(new RiderSeatLog { Turn = rsTurn, Calls = calls, Earn = earn, Rested = rested, Mood = rsMood, Dong = slot >= 0 ? BranchDongs[slot].Name : "" });
            if (rsLog.Count > 40) rsLog.RemoveAt(0);
            string note = rested ? $"{rsTurn}턴 · 쉬었다 — 몰라 · 기분 관리 {rsMood}"
                : $"{rsTurn}턴 · 콜 {calls}건 · +{BranchMoney(earn)}" + (calls <= 3 ? " — 콜이 적어 기분 관리가 크게 올랐다" : "");
            rsTurn++;
            if ((rsTurn - 1) % RiderTurnsPerRound == 0)       // 세 시간 = 지사 한 회차
            {
                지사경쟁Policy.회차넘기기(branchBoard);
                BranchPlatformOnboarding();
                RefreshBranch();
                note += " · 지사 한 회차가 지났다";
            }
            if (rsXp == RiderLevelTwoXp && !rested) note = "자리 읽기 Lv 2 — 칸의 짙기가 또렷해지고 예상 콜이 보입니다";
            BranchSay(note);
        }

        // 칸 색: 주문이 나는 자리(금빛). Lv 1은 세 단계로 흐릿하게, 우리 지사가 없는 동은 옅게.
        private void RiderSeatHexColour(int cell, ref Color colour, ref float strength)
        {
            var slot = BranchSlotOfCell(cell);
            if (slot < 0) { strength *= 0.3f; return; }
            var heat = RiderSeatHeat(cell);
            if (RiderSeatLevel < 2) heat = Mathf.Round(heat * 3f) / 3f;
            colour = Ink(0xFFC46B);
            strength = 0.05f + (RiderSeatLevel < 2 ? 0.34f : 0.55f) * Mathf.Sqrt(heat);
            if (branchBoard.자리(BranchMe, BranchDongs[slot].Code) == null) strength *= 0.45f;
        }

        // 지도 위: 동 이름 · 사가정 지사 깃발 · 지난 턴의 배달 빛줄 · 「나」
        private void DrawRiderSeatMarks(float alpha)
        {
            if (alpha < 0.4f) return;
            for (var slot = 0; slot < BranchDongs.Length; slot++)
            {
                if (branchCell[slot] < 0) continue;
                var at = GuPoint(branchCentre[slot].x, branchCentre[slot].y);
                if (at.x < -60f || at.x > Field.width + 60f || at.y < -30f || at.y > Field.height + 30f) continue;
                var name = BranchDongs[slot].Name;
                if (BranchDongs[slot].Code == RiderHomeDong)
                {
                    Paint(new Rect(at.x - 1f, at.y - 24f, 2f, 24f), Paper);
                    Paint(new Rect(at.x + 1f, at.y - 24f, 15f, 10f), Mint, 2f);
                    name = "사가정 지사";
                }
                var wide = PortraitTextWidth(name, 10) + 14f;
                var pill = new Rect(at.x - wide * 0.5f, at.y + 4f, wide, 16f);
                Paint(pill, Fade(Canvas, 0.62f), 5f);
                Text(pill, name, 10, BranchDongs[slot].Code == RiderHomeDong ? Mint : Soft, BranchDongs[slot].Code == RiderHomeDong, TextAnchor.MiddleCenter);
            }
            var now = Time.unscaledTime;
            var me = rsWaitCell >= 0 ? rsWaitCell : RiderSeatDefaultCell();
            if (me < 0) return;
            var running = DrawGuRuns();      // 칸에서 칸으로 달리는 기사들 · 콜을 나르는 「나」 (SagajeongDispatchBranchGuRuns.cs)
            var start = running ?? GuPoint(chX[me], chY[me]);
            var pulse = 0.85f + 0.15f * Mathf.Sin(now * 4f);
            BranchGlow(start, 16f * pulse, Fade(Mint, 0.22f));
            BranchGlow(start, 7f, Mint);
            BranchGlow(start, 3f, Paper);
            Text(new Rect(start.x + 10f, start.y - 9f, 30f, 18f), "나", 12, Mint, true);
        }

        private void DrawRiderSeatSheet()
        {
            var sheet = portraitSheetRect;
            Paint(new Rect(sheet.x, sheet.y, sheet.width, sheet.height + 20f), Fade(Panel, 0.985f), 18f);
            Paint(new Rect(sheet.center.x - 20f, sheet.y + 7f, 40f, 4f), Rim, 2f);
            var title = branchTab == 0 ? "이번 턴 — 어디서 기다릴까" : branchTab == 1 ? "콜 기록" : branchTab == 2 ? "번 돈" : "기사 카드";
            Text(new Rect(16f, sheet.y + 15f, sheet.width - 110f, 18f), title, 14, Paper, true);
            Text(new Rect(16f, sheet.y + 34f, sheet.width - 32f, 14f),
                branchTab == 0 ? (RiderSeatLevel < 2 ? $"자리 읽기 Lv 1 · 짙기가 흐릿합니다({rsXp}/{RiderLevelTwoXp} 턴 뒤 또렷)" : "자리 읽기 Lv 2 · 짙은 칸일수록 콜이 많습니다")
                : branchTab == 1 ? "한 턴 = 한 시간 · 세 턴마다 지사 한 회차" : branchTab == 2 ? "게임 돈 · 콜 한 건 3,400원(예시)" : "열쇠가 차면 팀장 자리가 열립니다(다음 조각)", 10, Soft);
            Text(new Rect(sheet.xMax - 92f, sheet.y + 15f, 78f, 18f), portraitSheet == 0 ? "펼치기 ▲" : "접기 ▼", 11, Mint, true, TextAnchor.MiddleRight);
            if (PortraitTapped(new Rect(sheet.x, sheet.y, sheet.width, PortraitHandle))) SetPortraitSheet(portraitSheet == 0 ? 1 : 0);

            var tabTop = sheet.yMax - portraitBottomInset - 46f;
            var tabWide = (sheet.width - 32f - 18f) / 4f;
            var names = new[] { "지도", "콜", "돈", "카드" };
            for (var k = 0; k < 4; k++)
            {
                var rect = new Rect(16f + k * (tabWide + 6f), tabTop, tabWide, 38f);
                var on = branchTab == k;
                if (on) Paint(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), Gold, 10f);
                Paint(rect, on ? Ink(0x2A2412) : Tile, 9f);
                Text(rect, names[k], 13, on ? Gold : Paper, on, TextAnchor.MiddleCenter);
                if (PortraitTapped(rect)) { branchTab = k; if (portraitSheet == 0) SetPortraitSheet(1); }
            }
            if (portraitSheet == 0) return;
            var y = sheet.y + PortraitHandle + 2f;
            var wide = sheet.width - 32f;
            if (branchTab == 0)
            {
                var tile = (wide - 12f) / 3f;
                BranchStat(new Rect(16f, y, tile, 46f), "지난 턴", rsLastCalls < 0 ? (rsLog.Count == 0 ? "—" : "쉼") : $"콜 {rsLastCalls}건",
                    rsLastCalls >= 0 && rsLastCalls <= 3 ? BranchDanger : Paper);
                BranchStat(new Rect(16f + tile + 6f, y, tile, 46f), "지난 턴 번 돈", "+" + BranchMoney(rsLastEarn), Gold);
                BranchStat(new Rect(16f + (tile + 6f) * 2f, y, tile, 46f), "오늘 번 돈", BranchMoney(rsEarnTotal), Gold);
                y += 54f;
                var cell = chSelected >= 0 && BranchSlotOfCell(chSelected) >= 0 ? chSelected : rsWaitCell;
                var row = new Rect(16f, y, wide, 44f);
                Paint(row, Fade(Tile, 0.55f), 9f);
                if (cell < 0)
                    Text(new Rect(row.x + 12f, row.y, row.width - 24f, row.height), "지도에서 기다릴 칸을 누르세요 — 금빛이 짙을수록 주문이 나는 자리", 11, Soft, false, TextAnchor.MiddleLeft, true);
                else
                {
                    var slot = BranchSlotOfCell(cell);
                    var mine = branchBoard.자리(BranchMe, BranchDongs[slot].Code) != null;
                    var expected = RiderSeatExpected(cell);
                    var heat = RiderSeatHeat(cell);
                    Text(new Rect(row.x + 12f, row.y + 4f, row.width - 24f, 18f), $"{BranchDongs[slot].Name} 칸" + (cell == rsWaitCell && cell != chSelected ? " · 지난번 자리" : ""), 13, Paper, true);
                    var line = !mine ? "우리 지사가 없는 동 — 콜이 거의 없습니다"
                        : RiderSeatLevel >= 2 ? $"예상 콜 {Mathf.FloorToInt(expected * 0.75f)} ~ {Mathf.CeilToInt(expected * 1.25f)}건"
                        : heat > 0.55f ? "주문이 많은 편(흐릿)" : heat > 0.25f ? "주문이 보통(흐릿)" : "주문이 적은 편(흐릿)";
                    Text(new Rect(row.x + 12f, row.y + 23f, row.width - 24f, 16f), line, 11, mine ? Gold : Soft);
                }
                y += 52f;
                var go = new Rect(16f, y, wide * 0.6f - 4f, 48f);
                var rest = new Rect(go.xMax + 8f, y, wide * 0.4f - 4f, 48f);
                Paint(go, cell >= 0 ? Mint : Fade(Tile, 0.6f), 11f);
                Text(go, "이 칸에서 기다리기", 14, cell >= 0 ? Canvas : Soft, true, TextAnchor.MiddleCenter);
                var lilac = Ink(0xC9B6FF);
                Paint(rest, lilac, 11f);
                Paint(new Rect(rest.x + 1.5f, rest.y + 1.5f, rest.width - 3f, rest.height - 3f), Panel, 10f);
                Text(rest, "쉬기 · 몰라", 13, lilac, true, TextAnchor.MiddleCenter);
                if (PortraitTapped(go) && cell >= 0) { RiderSeatWork(cell); chSelected = -1; }
                else if (PortraitTapped(rest)) RiderSeatRest();
                Text(new Rect(16f, y + 56f, wide, 30f), "콜이 3건 이하면 기분 관리가 크게 오릅니다. 쉬면 그 턴은 못 벌지만 내려갑니다. 칸을 두 번 누르면 동 디오라마.", 10, Fade(Soft, 0.9f),
                    false, TextAnchor.UpperLeft, true);
            }
            else if (branchTab == 1)
            {
                var k = 0;
                for (var i = rsLog.Count - 1; i >= 0 && k < 6; i--, k++)
                {
                    var log = rsLog[i];
                    var row = new Rect(16f, y + k * 34f, wide, 30f);
                    Paint(row, Fade(Tile, 0.45f), 7f);
                    Text(new Rect(row.x + 10f, row.y, 60f, row.height), $"{log.Turn}턴", 12, Soft, false, TextAnchor.MiddleLeft);
                    Text(new Rect(row.x + 60f, row.y, 150f, row.height), log.Rested ? "쉼 · 몰라" : $"{log.Dong} · 콜 {log.Calls}건", 12,
                        log.Rested ? Ink(0xC9B6FF) : log.Calls <= 3 ? Red : Paper, false, TextAnchor.MiddleLeft);
                    Text(new Rect(row.xMax - 170f, row.y, 160f, row.height), $"+{BranchMoney(log.Earn)} · 기분 {log.Mood}", 12, Gold, false, TextAnchor.MiddleRight);
                }
                if (rsLog.Count == 0) Text(new Rect(16f, y + 8f, wide, 20f), "아직 달린 턴이 없습니다.", 11, Soft);
            }
            else if (branchTab == 2)
            {
                var works = rsLog.Count(item => !item.Rested);
                var calls = rsLog.Sum(item => item.Calls);
                Text(new Rect(26f, y, wide - 120f, 26f), $"배달비 · 콜 {calls}건", 13, Paper, false, TextAnchor.MiddleLeft);
                Text(new Rect(sheet.xMax - 126f, y, 100f, 26f), "+" + BranchMoney(rsEarnTotal), 13, Mint, true, TextAnchor.MiddleRight);
                y += 28f;
                Text(new Rect(26f, y, wide - 40f, 40f), $"달린 턴 {works} · 쉰 턴 {rsLog.Count - works}. 기름 · 보험 · 오토바이 리스는 다음 조각에서 나가는 돈으로 붙습니다.", 11, Soft, false,
                    TextAnchor.UpperLeft, true);
            }
            else
            {
                BranchKeyRow(ref y, wide, "기분 관리 60 아래로 이어 간 턴", rsCalmStreak, 5);
                BranchKeyRow(ref y, wide, "동료 기사 돕기(다음 조각)", 0, 5);
                BranchKeyRow(ref y, wide, "과학 · 주문이 나는 자리 읽기 Lv", RiderSeatLevel, 2);
                Text(new Rect(16f, y + 4f, wide, 36f), "셋이 다 차면 팀장으로 오를 수 있습니다. 팀장이 되어도 배달은 그대로 하고, 소통이 더해집니다.", 10, Soft, false, TextAnchor.UpperLeft, true);
            }
        }

        private void BranchKeyRow(ref float y, float wide, string label, int now, int need)
        {
            var done = now >= need;
            Text(new Rect(16f, y, wide - 70f, 18f), label, 12, Paper);
            Text(new Rect(16f + wide - 70f, y, 70f, 18f), $"{Mathf.Min(now, need)} / {need}{(done ? " ✓" : "")}", 12, done ? Mint : Gold, true, TextAnchor.MiddleRight);
            var bar = new Rect(16f, y + 21f, wide, 9f);
            Paint(bar, Rim, 4.5f);
            Paint(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(now / (float)need), bar.height), done ? Mint : Gold, 4.5f);
            y += 40f;
        }
    }
}
