using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 지사장 판 · 세로 화면(폰) 첫 조각 (2026-11-27 16:34 · 16:41 사용자: 「구지도를 기반으로 지사장 판」 ·
    //  「육각형 칸을 두 번 누르면 동 디오라마로 … 쉽게 구지도로 돌아올 수 있게 … 모바일 조작이 쉬우면」).
    //  - 시작 화면(원방각 · 시작 · 이어 하기 · 설정 · 출처) → 입장 카드 → 「지사장」을 고르면 이 판이 뜬다.
    //  - 바탕은 구 지도의 공개판 육각 칸(SagajeongDispatchCapitalHex.cs). 중랑구 16동의 칸을 동마다 내 몫으로 칠한다.
    //  - 셈은 Kernel의 지사경쟁Policy · 지사경쟁Ai(J1 · J2) 그대로. 여기서는 판을 만들고 보여 주고 수를 넘길 뿐이다.
    //  - 칸 한 번 = 동 카드, 같은 칸 두 번 = 동 디오라마(공개판), 디오라마의 「← 구 지도」로 돌아온다.
    //  - 동별 회차 주문 · 음식점 수 · 처음 자리는 예시(게임 안 값)다. 판은 아직 저장하지 않는다(켤 때마다 3턴에서 시작).
    public sealed partial class SagajeongDispatchView
    {
        private const string BranchKey = "branch";
        private const string BranchMe = "me";
        private const float BranchHomeScale = 0.07f;      // 중랑구가 한 화면에 드는 배율(칸이 다 떠오르는 배율)
        private const float BranchNearScale = 0.11f;
        private static readonly Color BranchRivalTint = Ink(0xC9A96A);
        private static readonly Color BranchDanger = Ink(0xFF5E57);
        private static readonly CultureInfo BranchInv = CultureInfo.InvariantCulture;

        // 중랑구 16동(행정동 코드 · 이름 · 회차 주문 · 음식점 수) — 숫자는 예시
        private static readonly (string Code, string Name, double Orders, int Shops)[] BranchDongs =
        {
            ("1126052000", "면목2동", 230, 110), ("1126054000", "면목4동", 200, 80), ("1126055000", "면목5동", 140, 60),
            ("1126056500", "면목본동", 300, 150), ("1126057000", "면목7동", 210, 90), ("1126057500", "면목3·8동", 280, 130),
            ("1126058000", "상봉1동", 250, 140), ("1126059000", "상봉2동", 290, 160), ("1126060000", "중화1동", 180, 85),
            ("1126061000", "중화2동", 220, 100), ("1126062000", "묵1동", 240, 105), ("1126063000", "묵2동", 150, 65),
            ("1126065500", "망우본동", 260, 120), ("1126066000", "망우3동", 160, 70), ("1126068000", "신내1동", 320, 125),
            ("1126069000", "신내2동", 170, 60),
        };

        private static readonly (string Id, string Name, 경쟁전략 Plan)[] BranchOperators =
        {
            (BranchMe, "나", 경쟁전략.사람), ("a", "경쟁 A", 경쟁전략.할인형), ("b", "경쟁 B", 경쟁전략.속도형), ("c", "경쟁 C", 경쟁전략.동네밀착형),
        };

        private static readonly (string Op, string Code, int Riders, int Shops)[] BranchSeats =
        {
            (BranchMe, "1126057500", 6, 14), (BranchMe, "1126056500", 5, 12), (BranchMe, "1126058000", 5, 12),
            ("a", "1126054000", 4, 10), ("a", "1126057000", 4, 10), ("a", "1126056500", 3, 8),
            ("b", "1126061000", 4, 10), ("b", "1126062000", 4, 10), ("b", "1126058000", 3, 8),
            ("c", "1126065500", 4, 10), ("c", "1126057500", 3, 8),
        };

        private 지사경쟁판 branchBoard;
        private bool branchTried;
        private int[] branchSlotOfDong;            // 칸 표의 동 번호 → 이 판의 동 자리(-1 = 판 밖)
        private Vector2[] branchCentre;            // 동 가운데(구 지도 미터)
        private int[] branchCell;                  // 동 가운데에 가장 가까운 칸(-1 = 칸 없음)
        private float[] branchMine, branchRival;
        private List<경쟁수> branchTodos = new List<경쟁수>();
        private long branchLastGain, branchSpent, branchFundsAtTurn;
        private int branchTab;                     // 0 지도 · 1 사람 · 2 돈 · 3 카드
        private bool branchCameraSet;
        private string branchNote;
        private float branchNoteUntil;
        private bool portraitTitleOpen;            // 시작 화면(세로)
        private bool portraitLadderOpen;           // 배달 판 자리 고르기(세로)
        private float ladderNoteUntil;

        private bool BranchStance => stanceKey == BranchKey;
        private bool BranchSheetOn => BranchStance && EnsureBranchBoard();
        private bool BranchBoardOn => PortraitScreen && BranchStance && !PublicDongOpen && EnsureBranchBoard();

        private float BranchSheetHeight(int level) =>
            level <= 0 ? PortraitHandle + 52f + portraitBottomInset : 348f + portraitBottomInset;

        private static string BranchMoney(double value) => (value / 10000.0).ToString("0.#", BranchInv) + "만";

        private static string BranchKindName(경쟁수종류 kind) =>
            kind == 경쟁수종류.기사모집 ? "기사 모집" : kind == 경쟁수종류.음식점영업 ? "가게 영업" : kind == 경쟁수종류.행사 ? "행사" :
            kind == 경쟁수종류.새동진출 ? "지사 열기" : "쉼";

        private int BranchSlotOfCode(string code) => Array.FindIndex(BranchDongs, item => item.Code == code);

        private int BranchSlotOfCell(int cell) =>
            branchSlotOfDong != null && cell >= 0 && cell < capitalHexCount && chDong[cell] < branchSlotOfDong.Length ? branchSlotOfDong[chDong[cell]] : -1;

        private void BranchSay(string text)
        {
            branchNote = text;
            branchNoteUntil = Time.unscaledTime + 3.5f;
        }

        // 판 만들기: 공개판 칸에서 16동의 가운데 · 이웃을 세고, Kernel 판에 운영자 넷과 처음 자리를 놓은 뒤 두 회차를 돌린다.
        private bool EnsureBranchBoard()
        {
            if (branchTried) return branchBoard != null;
            if (!EnsureCapitalHex() || chTable?.dongs == null) return false;      // 칸 자료가 아직이면 다음에 다시 본다
            branchTried = true;
            var count = BranchDongs.Length;
            branchSlotOfDong = new int[chTable.dongs.Length];
            for (var d = 0; d < branchSlotOfDong.Length; d++)
                branchSlotOfDong[d] = chTable.dongs[d]?.code == null ? -1 : BranchSlotOfCode(chTable.dongs[d].code);
            var sums = new Vector2[count];
            var cells = new int[count];
            for (var i = 0; i < capitalHexCount; i++)
            {
                var slot = BranchSlotOfCell(i);
                if (slot < 0) continue;
                sums[slot] += new Vector2(chX[i], chY[i]);
                cells[slot]++;
            }
            if (cells.All(value => value == 0))
            {
                Debug.LogWarning("Hongdal branch board: no hex cells for the sixteen dongs");
                return false;
            }
            branchCentre = new Vector2[count];
            branchCell = new int[count];
            var nearest = new float[count];
            var neighbours = new HashSet<int>[count];
            for (var slot = 0; slot < count; slot++)
            {
                branchCentre[slot] = cells[slot] > 0 ? sums[slot] / cells[slot] : Vector2.zero;
                branchCell[slot] = -1;
                nearest[slot] = float.MaxValue;
                neighbours[slot] = new HashSet<int>();
            }
            var reach = chRadius * 1.75f;
            for (var i = 0; i < capitalHexCount; i++)
            {
                var slot = BranchSlotOfCell(i);
                if (slot < 0) continue;
                var here = new Vector2(chX[i], chY[i]);
                var distance = (here - branchCentre[slot]).sqrMagnitude;
                if (distance < nearest[slot]) { nearest[slot] = distance; branchCell[slot] = i; }
                for (var k = 0; k < 12; k++)
                {
                    var angle = k * Mathf.PI / 6f;
                    var other = BranchSlotOfCell(CapitalHexAt(here + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach));
                    if (other >= 0 && other != slot) neighbours[slot].Add(other);
                }
            }

            var dongs = new List<경쟁동>();
            for (var slot = 0; slot < count; slot++)
            {
                if (cells[slot] == 0) continue;
                var item = BranchDongs[slot];
                var dong = new 경쟁동 { Code = item.Code, 이름 = item.Name, 회차주문 = item.Orders, 음식점수 = item.Shops };
                dong.이웃.AddRange(neighbours[slot].OrderBy(value => value).Select(value => BranchDongs[value].Code));
                dongs.Add(dong);
            }
            var operators = BranchOperators.Select(item => new 경쟁운영자 { Id = item.Id, 이름 = item.Name, 전략 = item.Plan, 자본 = 200000 }).ToList();
            var board = 지사경쟁Policy.새판(new 지사경쟁Rules(), dongs, operators, 20261127UL);
            foreach (var seat in BranchSeats) 지사경쟁Policy.처음자리(board, seat.Op, seat.Code, seat.Riders, seat.Shops);
            지사경쟁Policy.회차넘기기(board);
            var warm = board.운영자(BranchMe).자본;
            지사경쟁Policy.회차넘기기(board);
            branchBoard = board;
            branchMine = new float[count];
            branchRival = new float[count];
            branchFundsAtTurn = board.운영자(BranchMe).자본;
            branchLastGain = branchFundsAtTurn - warm;
            branchSpent = 0;
            RefreshBranch();
            Debug.Log($"Hongdal branch board: {dongs.Count} dongs, turn {board.회차}, seats {board.자리들.Count}");
            return true;
        }

        private void RefreshBranch()
        {
            for (var slot = 0; slot < BranchDongs.Length; slot++)
            {
                var code = BranchDongs[slot].Code;
                var mine = 0.0;
                var rival = 0.0;
                foreach (var seat in branchBoard.동자리(code))
                {
                    if (seat.운영자 == BranchMe) mine = seat.몫;
                    else rival = Math.Max(rival, seat.몫);
                }
                branchMine[slot] = (float)mine;
                branchRival[slot] = (float)rival;
            }
            // 같은 동의 같은 일은 한 줄로(하나 하면 다시 고른다)
            // 가게 입점은 배달 플랫폼 자리의 일이다(11-27 18:25 · 18:37 사용자) — 지사장의 할 일에서 뺀다. 경쟁 AI는 그대로 둔다(판의 배경).
            branchTodos = 지사경쟁Ai.수고르기(branchBoard, BranchMe).Where(item => item.종류 != 경쟁수종류.음식점영업).GroupBy(item => (item.종류, item.동)).Select(group => group.First()).ToList();
        }

        private void BranchDo(경쟁수 move)
        {
            if (move == null) return;
            var cost = 지사경쟁Policy.수비용(branchBoard.Rules, move);
            string why;
            if (지사경쟁Policy.수두기(branchBoard, BranchMe, move, out why))
            {
                branchSpent += cost;
                var dong = branchBoard.동(move.동);
                BranchSay($"{dong?.이름} · {BranchKindName(move.종류)} — {BranchMoney(cost)} 씀");
                RefreshBranch();
            }
            else BranchSay(string.IsNullOrEmpty(why) ? "지금은 할 수 없습니다" : why);
        }

        private void BranchNextTurn()
        {
            지사경쟁Policy.회차넘기기(branchBoard);
            var platformNote = BranchPlatformOnboarding();
            var funds = branchBoard.운영자(BranchMe).자본;
            branchLastGain = funds - branchFundsAtTurn;
            branchFundsAtTurn = funds;
            branchSpent = 0;
            RefreshBranch();
            var news = branchBoard.소식.LastOrDefault(item => item.회차 == branchBoard.회차 - 1 && item.운영자 == BranchMe);
            BranchSay(news != null ? news.글 : platformNote ?? $"{branchBoard.회차}턴 — 지난 회차 {(branchLastGain >= 0 ? "+" : "")}{BranchMoney(branchLastGain)}");
        }

        // 가게 입점을 늘리는 것은 배달 플랫폼 자리의 일(11-27 18:37 사용자). 플랫폼 자리가 아직 열리지 않았으니
        //  본사(플랫폼)가 대신 한다(가안): 턴마다 내 지사 가운데 가게 수가 기사보다 모자란 동에 한 번 입점시킨다. 돈은 지사가 내지 않는다.
        private string BranchPlatformOnboarding()
        {
            var rules = branchBoard.Rules;
            string note = null;
            foreach (var seat in branchBoard.운영자자리(BranchMe).ToList())
            {
                var dong = branchBoard.동(seat.동);
                if (dong == null || seat.입점 >= dong.음식점수) continue;
                if (seat.입점 * rules.음식점회차처리 >= seat.기사 * rules.기사회차처리) continue;
                var before = seat.입점;
                seat.입점 = Math.Min(dong.음식점수, seat.입점 + rules.영업한번입점);
                note = $"본사(플랫폼)가 {dong.이름}에 가게 {seat.입점 - before}곳을 입점시킴 → 제휴 가게 {seat.입점}곳";
                branchBoard.소식.Add(new 경쟁소식 { 회차 = branchBoard.회차, 운영자 = BranchMe, 동 = dong.Code, 글 = note });
            }
            return note;
        }

        // 지사장의 기분 관리(가안): 받을 주문 가운데 놓친 몫이 클수록 오른다. 80부터 위험.
        private int BranchMood()
        {
            double due = 0, missed = 0;
            foreach (var seat in branchBoard.운영자자리(BranchMe)) { due += seat.받을주문; missed += seat.놓친주문; }
            var share = due > 1e-9 ? missed / due : 0.0;
            return Mathf.Clamp(Mathf.RoundToInt(28f + (float)share * 160f), 0, 100);
        }

        private Vector2 BranchMapCentre => new Vector2(PortraitWidth * 0.5f, (PortraitMapTop + portraitSheetRect.y) * 0.5f);

        private void FocusBranchAt(Vector2 world, float scale)
        {
            if (guSpace == null) return;
            var target = BranchMapCentre;
            guScale = scale;
            guManual = true;
            guOrigin = new Vector2(target.x - world.x * scale, target.y - (guSpace.height - world.y) * scale);
            ClampGu();
        }

        private void FocusBranchHome()
        {
            if (branchSeat == 0)      // 기사: 사가정 지사 둘레
            {
                var home = BranchSlotOfCode(RiderHomeDong);
                if (home >= 0 && branchCell[home] >= 0) FocusBranchAt(branchCentre[home], 0.10f);
                return;
            }
            var sum = Vector2.zero;
            var count = 0;
            for (var slot = 0; slot < BranchDongs.Length; slot++)
            {
                if (branchCell[slot] < 0) continue;
                sum += branchCentre[slot];
                count++;
            }
            if (count > 0) FocusBranchAt(sum / count, BranchHomeScale);
        }

        // 할 일 · 사람 · 돈 줄을 누르면: 그 동으로 카메라를 좁히고 동 카드를 띄운다.
        private void FocusBranchDong(string code)
        {
            var slot = BranchSlotOfCode(code);
            if (slot < 0 || branchCell[slot] < 0) return;
            FocusBranchAt(branchCentre[slot], BranchNearScale);
            chSelected = branchCell[slot];
            chLastTap = -1;
            guStation = null;
        }

        // 칸 색: 내 몫이 절반을 넘으면 내 빛, 경쟁이 절반을 넘으면 금빛, 그 밖은 앞선 쪽 빛을 옅게. 판 밖 칸은 어둡게.
        private void BranchHexColour(int cell, ref Color colour, ref float strength)
        {
            if (branchSeat == 0) { RiderSeatHexColour(cell, ref colour, ref strength); return; }      // 기사 자리: 주문이 나는 자리
            var slot = BranchSlotOfCell(cell);
            if (slot < 0)
            {
                strength *= 0.3f;
                return;
            }
            var mine = branchMine[slot];
            var rival = branchRival[slot];
            if (mine >= 0.5f) { colour = Mint; strength = 0.44f + 0.5f * (mine - 0.5f); }
            else if (rival >= 0.5f) { colour = BranchRivalTint; strength = 0.36f + 0.5f * (rival - 0.5f); }
            else { colour = mine >= rival ? Mint : BranchRivalTint; strength = 0.10f + 0.45f * Mathf.Max(mine, rival); }
        }

        // 구 지도 위 표식: 내 지사 깃발과 동마다 「이름 · 내 몫」. DrawCapitalHex가 칸을 그린 뒤 부른다(지도 좌표).
        private void DrawBranchMapMarks(float alpha)
        {
            if (alpha < 0.4f) return;
            if (branchSeat == 0) { DrawRiderSeatMarks(alpha); return; }
            DrawGuRuns();      // 지사장: 내 기사들이 칸에서 칸으로 (SagajeongDispatchBranchGuRuns.cs)
            for (var slot = 0; slot < BranchDongs.Length; slot++)
            {
                if (branchCell[slot] < 0) continue;
                var at = GuPoint(branchCentre[slot].x, branchCentre[slot].y);
                if (at.x < -60f || at.x > Field.width + 60f || at.y < -30f || at.y > Field.height + 30f) continue;
                var info = BranchDongs[slot];
                var seat = branchBoard.자리(BranchMe, info.Code);
                var label = seat != null ? $"{info.Name} {Mathf.RoundToInt(branchMine[slot] * 100f)}%"
                    : branchRival[slot] > 0.004f ? $"{info.Name} · 경쟁 {Mathf.RoundToInt(branchRival[slot] * 100f)}%" : info.Name;
                var wide = PortraitTextWidth(label, 10) + 14f;
                if (seat != null)
                {
                    Paint(new Rect(at.x - 1f, at.y - 24f, 2f, 24f), Paper);
                    Paint(new Rect(at.x + 1f, at.y - 24f, 15f, 10f), Mint, 2f);
                    Paint(new Rect(at.x - 5f, at.y - 2f, 10f, 4f), Fade(Mint, 0.8f), 2f);
                    var pill = new Rect(at.x - wide * 0.5f, at.y + 5f, wide, 17f);
                    Paint(new Rect(pill.x - 1f, pill.y - 1f, pill.width + 2f, pill.height + 2f), Fade(Mint, 0.9f), 6f);
                    Paint(pill, Fade(Canvas, 0.94f), 5f);
                    Text(pill, label, 10, Paper, true, TextAnchor.MiddleCenter);
                }
                else
                {
                    var pill = new Rect(at.x - wide * 0.5f, at.y - 8f, wide, 16f);
                    Paint(pill, Fade(Canvas, 0.62f), 5f);
                    Text(pill, label, 10, branchRival[slot] >= 0.5f ? BranchRivalTint : Soft, false, TextAnchor.MiddleCenter);
                }
            }
        }

        private Rect BranchDongCardRect => new Rect(10f, Mathf.Max(PortraitMapTop + 4f, portraitSheetRect.y - 170f), PortraitWidth - 20f, 162f);

        // 동 카드: 몫 띠 · 회차 주문 · 내 지사 · 단추(기사 모집 · 가게 영업 · 지사 열기 · 동 디오라마). 눌렀으면 true.
        private bool DrawBranchDongCard(int slot)
        {
            var card = CapitalHexCardRect;
            var info = BranchDongs[slot];
            var dong = branchBoard.동(info.Code);
            if (dong == null) return false;
            var seat = branchBoard.자리(BranchMe, info.Code);
            var mine = branchMine[slot];
            var outside = Mathf.Clamp01((float)dong.밖몫);
            var rivals = Mathf.Clamp01(1f - mine - outside);
            Paint(new Rect(card.x - 1f, card.y - 1f, card.width + 2f, card.height + 2f), Fade(Gold, 0.8f), 11f);
            Paint(card, Fade(Canvas, 0.97f), 10f);
            var x = card.x + 12f;
            var wide = card.width - 24f;
            Text(new Rect(x, card.y + 6f, 150f, 24f), info.Name, 17, Paper, true);
            var state = mine >= 0.5f ? "내 빛" : branchRival[slot] >= 0.5f ? "경쟁이 앞섬" : seat != null ? "겨루는 중" : "지사 없음";
            var stateTint = mine >= 0.5f ? Mint : branchRival[slot] >= 0.5f ? BranchRivalTint : Soft;
            Text(new Rect(x + PortraitTextWidth(info.Name, 17) + 8f, card.y + 10f, 90f, 18f), state, 10, stateTint, true);
            Text(new Rect(card.xMax - 196f, card.y + 10f, 156f, 16f), "칸을 두 번 누르면 디오라마", 9, Fade(Soft, 0.9f), false, TextAnchor.MiddleRight);
            var close = new Rect(card.xMax - 38f, card.y + 2f, 36f, 32f);
            Text(close, "×", 17, Soft, false, TextAnchor.MiddleCenter);

            var bar = new Rect(x, card.y + 36f, wide, 12f);
            Paint(bar, Rim, 6f);
            Paint(new Rect(bar.x, bar.y, bar.width * (mine + rivals), bar.height), BranchRivalTint, 6f);
            if (mine > 0.004f) Paint(new Rect(bar.x, bar.y, bar.width * mine, bar.height), Mint, 6f);
            Text(new Rect(x, card.y + 51f, wide, 15f),
                $"나 {Mathf.RoundToInt(mine * 100f)}% · 경쟁 지사 {Mathf.RoundToInt(rivals * 100f)}% · 플랫폼 밖 {Mathf.RoundToInt(outside * 100f)}%", 10, Soft);
            Text(new Rect(x, card.y + 69f, wide, 17f), $"회차 주문 {dong.회차주문:0}건 · 음식점 {dong.음식점수}곳", 12, Paper);
            Text(new Rect(x, card.y + 88f, wide, 16f),
                seat != null ? $"내 지사 · 기사 {seat.기사}명 · 제휴 가게 {seat.입점}곳 · 신뢰 {Mathf.RoundToInt((float)seat.신뢰 * 100f)}% · 놓친 주문 {seat.놓친주문:0}건"
                    : "내 지사가 없는 동 — 내 지사와 맞닿아 있으면 지사를 열 수 있습니다", 10, seat != null ? Paper : Soft);

            var moves = seat != null
                ? new[] { new 경쟁수 { 종류 = 경쟁수종류.기사모집, 동 = info.Code } }      // 가게 영업은 음식점 판에서
                : new[] { new 경쟁수 { 종류 = 경쟁수종류.새동진출, 동 = info.Code } };
            var gap = 8f;
            var buttonWide = (wide - gap * 2f) / 3f;
            var top = card.y + 112f;
            var current = Event.current;
            var up = current.type == EventType.MouseUp && current.button == 0;
            for (var k = 0; k < moves.Length; k++)
            {
                var rect = new Rect(x + k * (buttonWide + gap), top, buttonWide, 40f);
                string why;
                var can = 지사경쟁Policy.둘수있나(branchBoard, BranchMe, moves[k], out why);
                Paint(rect, can ? Tile : Fade(Tile, 0.5f), 9f);
                Text(new Rect(rect.x, rect.y + 4f, rect.width, 18f), BranchKindName(moves[k].종류), 12, can ? Paper : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
                Text(new Rect(rect.x, rect.y + 21f, rect.width, 14f), BranchMoney(지사경쟁Policy.수비용(branchBoard.Rules, moves[k])), 10, can ? Gold : Fade(Soft, 0.7f), false,
                    TextAnchor.MiddleCenter);
                if (up && rect.Contains(current.mousePosition))
                {
                    current.Use();
                    if (can) BranchDo(moves[k]);
                    else BranchSay(why);
                    return true;
                }
            }
            var go = new Rect(card.xMax - 12f - buttonWide, top, buttonWide, 40f);
            Paint(go, Mint, 9f);
            Text(go, "동 디오라마 ▸", 12, Canvas, true, TextAnchor.MiddleCenter);
            if (up)
            {
                if (go.Contains(current.mousePosition))
                {
                    current.Use();
                    OpenCapitalHexDong(chSelected);
                    return true;
                }
                if (close.Contains(current.mousePosition))
                {
                    current.Use();
                    chSelected = -1;
                    return true;
                }
            }
            if ((current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.MouseDrag) && card.Contains(current.mousePosition))
            {
                current.Use();
                return true;
            }
            return false;
        }

        // 위 띠 · 소식 줄 · 확대 단추 · 아래 판. DrawPortraitChrome이 지사장 입장일 때 대신 부른다.
        private void DrawBranchChrome(Stance stance)
        {
            if (!branchCameraSet && GuReady && guCameraSet)
            {
                branchCameraSet = true;
                FocusBranchHome();
            }
            var board = branchBoard;
            var me = board.운영자(BranchMe);
            Paint(new Rect(0f, 0f, PortraitWidth, PortraitMapTop), Fade(Canvas, 0.95f));
            var y = portraitTopInset;
            Text(new Rect(14f, y + 4f, 220f, 14f), "홍달 · 배달 판", 10, Mint, true);
            Text(new Rect(14f, y + 19f, 190f, 26f), branchSeat == 0 ? "기사 · 사가정 지사" : "지사장 · 중랑구", 19, Paper, true);
            var mood = branchSeat == 0 ? rsMood : BranchMood();
            var moodTint = mood >= 80 ? BranchDanger : mood >= 60 ? Gold : Mint;
            var chip = new Rect(PortraitWidth - 200f, y + 23f, 96f, 22f);
            Paint(chip, Fade(moodTint, 0.9f), 11f);
            Paint(new Rect(chip.x + 1f, chip.y + 1f, chip.width - 2f, chip.height - 2f), Canvas, 10f);
            Text(chip, $"기분 관리 {mood}", 10, moodTint, true, TextAnchor.MiddleCenter);
            Text(new Rect(PortraitWidth - 200f, y + 5f, 186f, 15f), branchSeat == 0 ? $"{rsTurn}턴 · 지사 {board.회차}회차" : $"{board.회차}턴 · 자본 {BranchMoney(me.자본)}", 10, Soft, false,
                TextAnchor.MiddleRight);
            var all = new Rect(PortraitWidth - 96f, y + 23f, 82f, 22f);
            Paint(all, Tile, 11f);
            Text(all, "자리 바꾸기", 10, Paper, true, TextAnchor.MiddleCenter);
            if (PortraitTapped(new Rect(PortraitWidth - 100f, 0f, 100f, portraitTop.yMax))) portraitLadderOpen = true;

            // 소식 줄(칩 줄 자리)
            var line = branchNote != null && Time.unscaledTime < branchNoteUntil ? branchNote
                : branchSeat == 0 ? "칸을 누르면 기다릴 자리 · 같은 칸을 두 번 누르면 동 디오라마" : "칸을 누르면 동 카드 · 같은 칸을 두 번 누르면 동 디오라마";
            var strip = new Rect(10f, portraitChips.y + 5f, PortraitWidth - 20f, 30f);
            Paint(strip, Fade(Tile, 0.7f), 15f);
            Text(new Rect(strip.x + 14f, strip.y, strip.width - 28f, strip.height), line, 11, branchNote != null && Time.unscaledTime < branchNoteUntil ? Gold : Soft,
                false, TextAnchor.MiddleLeft);

            if (PortraitZoomShown && GuMapVisible && guCameraSet)
            {
                PortraitRound(portraitZoomIn, "+", 20);
                PortraitRound(portraitZoomOut, "-", 22);
                PortraitRound(portraitFit, "구", 12);
                var centre = BranchMapCentre;
                var full = GuFullScale;
                if (PortraitTapped(portraitZoomIn)) ZoomGu(Mathf.Min(Mathf.Max(full, GuMaxScale), guScale * 1.4f), centre);
                else if (PortraitTapped(portraitZoomOut)) ZoomGu(Mathf.Max(full, guScale / 1.4f), centre);
                else if (PortraitTapped(portraitFit)) FocusBranchHome();
            }
            if (branchSeat == 0) DrawRiderSeatSheet();      // 기사 자리 (SagajeongDispatchBranchRiderSeat.cs)
            else DrawBranchSheet();
        }

        private static readonly string[] BranchTabNames = { "지도", "사람", "돈", "카드" };

        private void DrawBranchSheet()
        {
            var sheet = portraitSheetRect;
            var board = branchBoard;
            var me = board.운영자(BranchMe);
            var seats = board.운영자자리(BranchMe).ToList();
            Paint(new Rect(sheet.x, sheet.y, sheet.width, sheet.height + 20f), Fade(Panel, 0.985f), 18f);
            Paint(new Rect(sheet.center.x - 20f, sheet.y + 7f, 40f, 4f), Rim, 2f);
            var title = branchTab == 0 ? $"다음 할 일 {branchTodos.Count}" : branchTab == 1 ? $"내 지사 {seats.Count}곳" : branchTab == 2 ? "돈 · 지난 회차" : "지사장 카드";
            Text(new Rect(16f, sheet.y + 15f, sheet.width - 110f, 18f), title, 14, Paper, true);
            Text(new Rect(16f, sheet.y + 34f, sheet.width - 32f, 14f),
                branchTab == 0 ? "급한 것부터 · 줄을 누르면 그 동으로 카메라가 갑니다" : branchTab == 1 ? "놓친 주문이 많은 지사부터 · 누르면 그 동으로"
                : branchTab == 2 ? "게임 돈 · 줄을 누르면 그 돈이 오가는 동으로" : "원방각 카드는 다음 조각에서 붙습니다", 10, Soft);
            Text(new Rect(sheet.xMax - 92f, sheet.y + 15f, 78f, 18f), portraitSheet == 0 ? "펼치기 ▲" : "접기 ▼", 11, Mint, true, TextAnchor.MiddleRight);
            if (PortraitTapped(new Rect(sheet.x, sheet.y, sheet.width, PortraitHandle))) SetPortraitSheet(portraitSheet == 0 ? 1 : 0);

            // 아래 칸 넷(엄지 자리)
            var tabTop = sheet.yMax - portraitBottomInset - 46f;
            var tabWide = (sheet.width - 32f - 18f) / 4f;
            for (var k = 0; k < 4; k++)
            {
                var rect = new Rect(16f + k * (tabWide + 6f), tabTop, tabWide, 38f);
                var on = branchTab == k;
                if (on) Paint(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), Gold, 10f);
                Paint(rect, on ? Ink(0x2A2412) : Tile, 9f);
                Text(rect, BranchTabNames[k], 13, on ? Gold : Paper, on, TextAnchor.MiddleCenter);
                if (PortraitTapped(rect))
                {
                    branchTab = k;
                    if (portraitSheet == 0) SetPortraitSheet(1);
                }
            }
            if (portraitSheet == 0) return;

            var y = sheet.y + PortraitHandle + 2f;
            var wide = sheet.width - 32f;
            if (branchTab == 0)
            {
                // 숫자 셋
                double due = 0, got = 0, missed = 0, share = 0;
                foreach (var seat in seats) { due += seat.받을주문; got += seat.받은주문; missed += seat.놓친주문; share += seat.몫; }
                var tile = (wide - 12f) / 3f;
                BranchStat(new Rect(16f, y, tile, 46f), $"점유율 · {seats.Count}동", seats.Count == 0 ? "—" : $"{Mathf.RoundToInt((float)(share / seats.Count) * 100f)}%", Mint);
                BranchStat(new Rect(16f + tile + 6f, y, tile, 46f), "처리 · 놓침", $"{got:0} · {missed:0}", missed > due * 0.1 ? BranchDanger : Paper);
                BranchStat(new Rect(16f + (tile + 6f) * 2f, y, tile, 46f), "남는 돈 · 지난 회차", (branchLastGain >= 0 ? "+" : "") + BranchMoney(branchLastGain),
                    branchLastGain >= 0 ? Gold : BranchDanger);
                y += 52f;
                if (branchTodos.Count == 0)
                    Text(new Rect(16f, y + 8f, wide, 36f), "이번 턴에 꼭 할 일이 없습니다. 지도에서 동을 눌러 직접 고르거나 턴을 넘기세요.", 11, Soft, false, TextAnchor.UpperLeft, true);
                for (var k = 0; k < Mathf.Min(3, branchTodos.Count); k++)
                {
                    var move = branchTodos[k];
                    var row = new Rect(16f, y + k * 44f, wide, 40f);
                    Paint(row, k == 0 ? Ink(0x123029) : Fade(Tile, 0.6f), 9f);
                    Paint(new Rect(row.x + 8f, row.y + 9f, 22f, 22f), k == 0 ? Gold : Rim, 11f);
                    Text(new Rect(row.x + 8f, row.y + 9f, 22f, 22f), (k + 1).ToString(BranchInv), 11, Canvas, true, TextAnchor.MiddleCenter);
                    var dong = board.동(move.동);
                    Text(new Rect(row.x + 38f, row.y + 3f, row.width - 130f, 18f), $"{dong?.이름} · {BranchKindName(move.종류)}", 13, Paper, true);
                    Text(new Rect(row.x + 38f, row.y + 21f, row.width - 130f, 15f), move.왜, 10, Soft);
                    var go = new Rect(row.xMax - 84f, row.y + 4f, 78f, 32f);
                    Paint(go, Mint, 8f);
                    Text(go, "하기 · " + BranchMoney(지사경쟁Policy.수비용(board.Rules, move)), 11, Canvas, true, TextAnchor.MiddleCenter);
                    if (PortraitTapped(go)) { BranchDo(move); break; }
                    if (PortraitTapped(row)) FocusBranchDong(move.동);
                }
                var turn = new Rect(16f, tabTop - 54f, wide, 44f);
                Paint(turn, branchTodos.Count == 0 ? Mint : Tile, 11f);
                Text(turn, "턴 넘기기 ▸", 15, branchTodos.Count == 0 ? Canvas : Paper, true, TextAnchor.MiddleCenter);
                if (PortraitTapped(turn)) BranchNextTurn();
            }
            else if (branchTab == 1)
            {
                var k = 0;
                foreach (var seat in seats.OrderByDescending(item => item.받을주문 > 1e-9 ? item.놓친주문 / item.받을주문 : 0.0).Take(5))
                {
                    var row = new Rect(16f, y + k * 46f, wide, 42f);
                    var lost = seat.받을주문 > 1e-9 ? seat.놓친주문 / seat.받을주문 : 0.0;
                    Paint(row, Fade(Tile, 0.6f), 9f);
                    Paint(new Rect(row.x + 1f, row.y + 7f, 3f, row.height - 14f), lost > 0.1 ? BranchDanger : Mint, 1.5f);
                    Text(new Rect(row.x + 12f, row.y + 4f, row.width - 90f, 18f), $"{board.동(seat.동)?.이름} 지사 · 기사 {seat.기사}명 · 제휴 가게 {seat.입점}곳", 13, Paper, true);
                    Text(new Rect(row.x + 12f, row.y + 23f, row.width - 24f, 15f),
                        $"몫 {Mathf.RoundToInt((float)seat.몫 * 100f)}% · 받은 {seat.받은주문:0}건 · 놓친 {seat.놓친주문:0}건 · 신뢰 {Mathf.RoundToInt((float)seat.신뢰 * 100f)}%", 10,
                        lost > 0.1 ? Red : Soft);
                    Text(new Rect(row.xMax - 30f, row.y, 22f, row.height), "▸", 13, Gold, false, TextAnchor.MiddleCenter);
                    if (PortraitTapped(row)) FocusBranchDong(seat.동);
                    k++;
                }
            }
            else if (branchTab == 2)
            {
                var rules = board.Rules;
                var k = 0;
                double income = 0;
                foreach (var seat in seats.OrderByDescending(item => item.받은주문).Take(4))
                {
                    var gain = Math.Round(seat.받은주문 * rules.건당지사몫);
                    var row = new Rect(16f, y + k * 30f, wide, 27f);
                    Paint(row, Fade(Tile, 0.45f), 7f);
                    Text(new Rect(row.x + 10f, row.y, row.width - 110f, row.height), $"{board.동(seat.동)?.이름} 지사 · 배달 대행료 ▸", 12, Paper, false, TextAnchor.MiddleLeft);
                    Text(new Rect(row.xMax - 100f, row.y, 90f, row.height), "+" + BranchMoney(gain), 12, Mint, true, TextAnchor.MiddleRight);
                    if (PortraitTapped(row)) FocusBranchDong(seat.동);
                    k++;
                }
                foreach (var seat in seats) income += Math.Round(seat.받은주문 * rules.건당지사몫);
                var upkeep = rules.지사회차운영비 * seats.Count;
                y += k * 30f + 2f;
                Text(new Rect(26f, y, wide - 120f, 24f), $"지사 운영비 · {seats.Count}곳", 12, Paper, false, TextAnchor.MiddleLeft);
                Text(new Rect(sheet.xMax - 126f, y, 100f, 24f), "−" + BranchMoney(upkeep), 12, Ink(0xFF9F5A), true, TextAnchor.MiddleRight);
                y += 24f;
                Text(new Rect(26f, y, wide - 120f, 24f), "이번 턴에 쓴 돈(모집 · 지사 열기)", 12, Paper, false, TextAnchor.MiddleLeft);
                Text(new Rect(sheet.xMax - 126f, y, 100f, 24f), "−" + BranchMoney(branchSpent), 12, Ink(0xFF9F5A), true, TextAnchor.MiddleRight);
                y += 28f;
                Paint(new Rect(16f, y, wide, 1f), Rim);
                Text(new Rect(26f, y + 4f, wide - 120f, 26f), $"한 회차 들어옴 {BranchMoney(income)} · 자본", 12, Soft, false, TextAnchor.MiddleLeft);
                Text(new Rect(sheet.xMax - 126f, y + 4f, 100f, 26f), BranchMoney(me.자본), 14, Gold, true, TextAnchor.MiddleRight);
            }
            else
            {
                Text(new Rect(16f, y + 6f, wide, 60f),
                    "지사장 카드(원방각: 철학 · 사람 · 과학과 두 이음매)는 시안까지 되어 있고, 게임에는 다음 조각에서 붙입니다.", 12, Paper, false, TextAnchor.UpperLeft, true);
                Text(new Rect(16f, y + 70f, wide, 18f), $"연구 점수 {me.연구점수} · 지사 {seats.Count}곳 · {board.회차}턴", 12, Gold, true);
                Text(new Rect(16f, y + 94f, wide, 40f), "셈은 지사경쟁 규칙(점유율 · 이웃 AI) 그대로이고, 동별 주문 · 음식점 수는 예시입니다.", 10, Soft, false, TextAnchor.UpperLeft, true);
            }
        }

        private void BranchStat(Rect rect, string label, string value, Color tint)
        {
            Paint(rect, Fade(Tile, 0.55f), 9f);
            Text(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 12f, 14f), label, 9, Soft);
            Text(new Rect(rect.x + 8f, rect.y + 19f, rect.width - 12f, 24f), value, 16, tint, true);
        }

        // 배달 판의 자리(11-27 17:22 사용자: 「기사, 팀장, 지사장, 지사 대표, 배달 플랫폼 … 철학자의 입장까지는 같이 묶어서 …
        //  나머지 입장 부분들은 갈라가지고」). 지금 열린 자리는 지사장 하나. 나머지 입장(자료 보기)은 아래 문으로 따로 간다.
        private static readonly (string Key, string Name, string Line, bool Ready)[] LadderSeats =
        {
            ("ladder-rider", "기사", "콜을 받아 달린다 · 주문이 나는 자리 읽기", true),
            ("ladder-lead", "팀장", "기사 소통방을 이끈다 · 소통 능력", false),
            (BranchKey, "지사장", "구에서 지사를 맡는다 · 동별 몫 · 다음 할 일", true),
            ("ladder-head", "지사 대표", "여러 지사의 베이스라인을 짓는다", false),
            ("ladder-platform", "배달 플랫폼", "운영 조절판을 쥔다 · 수수료 · 단가", false),
        };

        private void DrawPortraitLadder()
        {
            Paint(new Rect(0f, 0f, PortraitWidth, Field.height), Fade(Ink(0x080D13), 0.985f));
            var y = portraitTopInset + 16f;
            Text(new Rect(0f, y, PortraitWidth, 16f), "홍달 · 배달 판", 11, Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(0f, y + 18f, PortraitWidth, 30f), "어느 자리에서 볼까요", 21, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(28f, y + 50f, PortraitWidth - 56f, 32f), "기사로 시작해 한 자리씩 오릅니다. 어느 자리에서든 배달은 그대로 하고, 오를 때마다 하는 일이 하나씩 더해집니다.", 11, Soft, false,
                TextAnchor.UpperCenter, true);
            var top = y + 92f;
            var lilac = Ink(0xC9B6FF);
            var chosen = -2;
            for (var index = 0; index <= LadderSeats.Length; index++)
            {
                var sage = index == LadderSeats.Length;      // 마지막 카드 = 철학자(어느 자리에나 겹쳐 보는 눈)
                var ready = !sage && LadderSeats[index].Ready;
                var card = new Rect(16f, top + index * 80f + (sage ? 10f : 0f), PortraitWidth - 32f, 72f);
                if (!sage && index > 0) Paint(new Rect(card.x + 24f, card.y - 8f, 2f, 8f), Rim);
                if (ready || sage) Paint(new Rect(card.x - 1f, card.y - 1f, card.width + 2f, card.height + 2f), Fade(sage ? lilac : Gold, sage ? 0.55f : 0.95f), 13f);
                Paint(card, Panel, 12f);
                var badge = new Rect(card.x + 12f, card.y + 23f, 26f, 26f);
                Paint(badge, sage ? lilac : ready ? Gold : Rim, 13f);
                Text(badge, sage ? "○" : (index + 1).ToString(BranchInv), 12, Canvas, true, TextAnchor.MiddleCenter);
                Text(new Rect(card.x + 50f, card.y + 10f, card.width - 170f, 24f), sage ? "철학자" : LadderSeats[index].Name, 17, ready || sage ? Paper : Soft, true);
                Text(new Rect(card.x + 50f, card.y + 38f, card.width - 62f, 18f),
                    sage ? "몰라 · 접속 · 인가 · 맡김 — 어느 자리에나 겹쳐 보는 눈" : LadderSeats[index].Line, 11, Soft);
                var chip = new Rect(card.xMax - 112f, card.y + 11f, 100f, 22f);
                Paint(chip, ready ? Mint : Fade(Tile, 0.9f), 11f);
                var peek = !sage && LadderSeats[index].Key == BranchKey;      // 지사장: 먼저 보기
                Paint(chip, ready ? (peek ? Gold : Mint) : Fade(Tile, 0.9f), 11f);
                Text(chip, ready ? (peek ? "먼저 보기" : "여기서 시작") : "다음 조각", 10, ready ? Canvas : Soft, true, TextAnchor.MiddleCenter);
                if (PortraitTapped(card)) chosen = ready ? index : -1;
            }
            if (chosen == -1) ladderNoteUntil = Time.unscaledTime + 3f;
            var link = new Rect(16f, Field.height - portraitBottomInset - 58f, PortraitWidth - 32f, 44f);
            if (Time.unscaledTime < ladderNoteUntil)
                Text(new Rect(16f, link.y - 30f, PortraitWidth - 32f, 20f), "이 자리는 다음 조각에서 엽니다 — 지금은 기사부터", 11, Gold, false, TextAnchor.MiddleCenter);
            Paint(link, Fade(Tile, 0.6f), 12f);
            Text(link, "다른 입장으로 자료 보기(음식점 · 주민 · 화물 · 도매 · 농가 …) ▸", 11, Soft, false, TextAnchor.MiddleCenter);
            if (chosen >= 0)
            {
                var stance = Array.Find(Stances, item => item.Key == BranchKey);
                if (stance != null)
                {
                    branchSeat = LadderSeats[chosen].Key == "ladder-rider" ? 0 : 2;
                    branchCameraSet = false;
                    chSelected = -1;
                    branchTab = 0;
                    portraitLadderOpen = false;
                    PortraitChoose(stance);
                }
            }
            else if (PortraitTapped(link))
            {
                portraitLadderOpen = false;
                stancePickerOpen = true;
            }
        }

        // 시작 화면(세로): 원방각 · 시작 · 이어 하기 · 설정 · 출처
        private void DrawPortraitTitle()
        {
            var high = Field.height;
            var ground = Ink(0x0A1218);
            Paint(new Rect(0f, 0f, PortraitWidth, high), ground);
            var cx = PortraitWidth * 0.5f;
            var top = portraitTopInset + Mathf.Max(40f, high * 0.09f);
            Paint(new Rect(cx - 72f, top, 144f, 144f), Ink(0xC9B6FF), 72f);                 // ○ 철학
            Paint(new Rect(cx - 69f, top + 3f, 138f, 138f), Ink(0x1A1630), 69f);
            Paint(new Rect(cx - 8f, top + 64f, 16f, 16f), Ink(0xFFF3C4), 8f);                // 허공 자리
            Paint(new Rect(cx - 6.5f, top + 65.5f, 13f, 13f), Ink(0x04070B), 6.5f);
            const int rows = 56;
            for (var k = 0; k < rows; k++)                                                    // △ 사람
            {
                var wideRow = 170f * (k + 1) / rows;
                var rowY = top + 134f + k * 2f;
                Paint(new Rect(cx - wideRow * 0.5f, rowY, wideRow, 2.2f), Ink(0x2A2412));
                Paint(new Rect(cx - wideRow * 0.5f - 1.5f, rowY, 3.2f, 2.2f), Ink(0xF2C35B));
                Paint(new Rect(cx + wideRow * 0.5f - 1.7f, rowY, 3.2f, 2.2f), Ink(0xF2C35B));
            }
            var box = new Rect(cx - 88f, top + 246f, 176f, 46f);                              // □ 과학
            Paint(box, Ink(0x6FA8FF), 3f);
            Paint(new Rect(box.x + 3f, box.y + 3f, box.width - 6f, box.height - 6f), Ink(0x0F1C27), 2f);
            Text(new Rect(0f, top + 306f, PortraitWidth, 44f), "홍달", 34, Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(0f, top + 350f, PortraitWidth, 18f), "경영 시뮬레이션", 13, Soft, false, TextAnchor.MiddleCenter);

            var y = top + 394f;
            var start = new Rect(44f, y, PortraitWidth - 88f, 54f);
            Paint(start, Mint, 14f);
            Text(start, "시작", 18, Canvas, true, TextAnchor.MiddleCenter);
            y += 64f;
            var last = CurrentStance;
            var resume = new Rect(44f, y, PortraitWidth - 88f, 46f);
            if (last != null)
            {
                Paint(resume, Tile, 12f);
                Text(resume, "이어 하기 · " + (last.Key == BranchKey ? "배달 판 · " + (branchSeat == 0 ? "기사" : "지사장") : last.Name), 14, Paper, true, TextAnchor.MiddleCenter);
                y += 56f;
            }
            var about = new Rect(44f, y, PortraitWidth - 88f, 44f);
            Paint(about, Fade(Tile, 0.6f), 12f);
            Text(about, "설정 · 출처", 13, Soft, false, TextAnchor.MiddleCenter);
            Text(new Rect(0f, high - portraitBottomInset - 30f, PortraitWidth, 14f), "게임 안 이름만 · 게임 돈 · 가상 운영", 9, Fade(Soft, 0.7f), false, TextAnchor.MiddleCenter);

            if (PortraitTapped(start))
            {
                portraitTitleOpen = false;
                portraitLadderOpen = true;      // 먼저 배달 판의 자리를 고른다
            }
            else if (last != null && PortraitTapped(resume))
            {
                portraitTitleOpen = false;
                PortraitChoose(last);
            }
            else if (PortraitTapped(about)) creditsOpen = true;
        }
    }
}
