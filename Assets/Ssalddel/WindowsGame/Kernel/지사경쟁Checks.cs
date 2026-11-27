using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 지사장 판 J1 · J2(2026-11-27)의 순수 검사. 점유율 엔진(지사경쟁Policy)과 이웃 AI(지사경쟁Ai).
    /// dotnet run --project Checks~/BranchMarket 로 돌린다. 화면 · 커널 · 원장 없이 규칙만 본다.
    /// </summary>
    public static class 지사경쟁Checks
    {
        public static List<(string name, bool ok, string detail)> Run()
        {
            var results = new List<(string name, bool ok, string detail)>();
            Action<string, bool, string> check = (name, ok, detail) => results.Add((name, ok, detail ?? string.Empty));
            Action<string, Action> group = (name, body) =>
            {
                try { body(); }
                catch (Exception exception) { check(name + ": 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message); }
            };
            group("규칙", () => Rules(check));
            group("수", () => Moves(check));
            group("회차", () => Turns(check));
            group("이웃 AI", () => Ai(check));
            return results;
        }

        // 작은 판: 동 넷이 한 줄로 맞닿아 있다(가 — 나 — 다 — 라).
        private static 지사경쟁판 Board(ulong seed = 7, long funds = 120000)
        {
            var dongs = new[]
            {
                Dong("d1", "가동", 280, 400, "d2"), Dong("d2", "나동", 240, 300, "d1", "d3"),
                Dong("d3", "다동", 320, 500, "d2", "d4"), Dong("d4", "라동", 200, 250, "d3"),
            };
            var ops = new[]
            {
                new 경쟁운영자 { Id = "me", 이름 = "나", 전략 = 경쟁전략.사람, 자본 = funds },
                new 경쟁운영자 { Id = "a", 이름 = "경쟁 A", 전략 = 경쟁전략.할인형, 자본 = funds },
                new 경쟁운영자 { Id = "b", 이름 = "경쟁 B", 전략 = 경쟁전략.속도형, 자본 = funds },
                new 경쟁운영자 { Id = "c", 이름 = "경쟁 C", 전략 = 경쟁전략.입점형, 자본 = funds },
                new 경쟁운영자 { Id = "d", 이름 = "경쟁 D", 전략 = 경쟁전략.동네밀착형, 자본 = funds },
            };
            var board = 지사경쟁Policy.새판(new 지사경쟁Rules(), dongs, ops, seed);
            지사경쟁Policy.처음자리(board, "me", "d1", 3, 10);
            지사경쟁Policy.처음자리(board, "a", "d2", 2, 8);
            지사경쟁Policy.처음자리(board, "b", "d3", 2, 8);
            지사경쟁Policy.처음자리(board, "c", "d4", 2, 8);
            지사경쟁Policy.처음자리(board, "d", "d1", 2, 8);
            return board;
        }

        private static 경쟁동 Dong(string code, string name, double orders, int restaurants, params string[] neighbors)
        {
            var dong = new 경쟁동 { Code = code, 이름 = name, 회차주문 = orders, 음식점수 = restaurants };
            dong.이웃.AddRange(neighbors);
            return dong;
        }

        private static void Rules(Action<string, bool, string> check)
        {
            var rules = new 지사경쟁Rules();
            check("매력 가중치의 합은 1", Math.Abs(rules.가중합 - 1.0) < 1e-9, rules.가중합.ToString("0.000"));
            check("규칙 이름에 깨어 있음 · 명덕이 없다(지사장 판의 식에 넣지 않는다)",
                !typeof(지사경쟁Rules).GetFields().Any(f => f.Name.Contains("깨어") || f.Name.Contains("명덕") || f.Name.Contains("영성")), string.Empty);

            var board = Board();
            check("새 판은 모든 동이 플랫폼 밖 100%", board.동들.All(d => Math.Abs(d.밖몫 - 1.0) < 1e-12), string.Empty);
            check("지사가 없는 동의 점유율은 0", 지사경쟁Policy.점유율(board, "me", "d3") == 0.0, string.Empty);
            var dong = board.동("d1");
            check("처리력 = 기사와 입점 음식점 가운데 모자란 쪽", 지사경쟁Policy.처리력(rules, 2, 8) == Math.Min(2 * rules.기사회차처리, 8 * rules.음식점회차처리)
                && 지사경쟁Policy.처리력(rules, 10, 1) == rules.음식점회차처리, string.Empty);
            check("입점 점수는 포화(음식점의 10%)에서 1로 멎는다", 지사경쟁Policy.입점점수(rules, dong, 40) == 1.0 && 지사경쟁Policy.입점점수(rules, dong, 400) == 1.0
                && Math.Abs(지사경쟁Policy.입점점수(rules, dong, 20) - 0.5) < 1e-9, string.Empty);
            check("행사 점수는 예산 0에서 0, 예산이 늘수록 늘고 1을 넘지 않는다", 지사경쟁Policy.행사점수(rules, dong, 0) == 0.0
                && 지사경쟁Policy.행사점수(rules, dong, 10000) < 지사경쟁Policy.행사점수(rules, dong, 30000)
                && 지사경쟁Policy.행사점수(rules, dong, 1e9) <= 1.0, string.Empty);
        }

        private static void Moves(Action<string, bool, string> check)
        {
            var board = Board();
            var rules = board.Rules;
            var me = board.운영자("me");
            string why;
            var before = me.자본;
            var ok = 지사경쟁Policy.수두기(board, "me", new 경쟁수 { 종류 = 경쟁수종류.기사모집, 동 = "d1" }, out why);
            check("기사 모집: 비용만큼 자본이 줄고 기사가 1명 는다", ok && me.자본 == before - rules.기사모집비 && board.자리("me", "d1").기사 == 4, why);

            before = me.자본;
            ok = 지사경쟁Policy.수두기(board, "me", new 경쟁수 { 종류 = 경쟁수종류.음식점영업, 동 = "d1" }, out why);
            check("음식점 영업: 비용만큼 줄고 입점이 는다", ok && me.자본 == before - rules.영업비 && board.자리("me", "d1").입점 == 10 + rules.영업한번입점, why);

            var print = 지사경쟁Policy.지문(board);
            ok = 지사경쟁Policy.수두기(board, "me", new 경쟁수 { 종류 = 경쟁수종류.기사모집, 동 = "d3" }, out why);
            check("지사가 없는 동에는 기사를 모집할 수 없다(판이 바뀌지 않는다)", !ok && 지사경쟁Policy.지문(board) == print, why);
            ok = 지사경쟁Policy.수두기(board, "me", new 경쟁수 { 종류 = 경쟁수종류.새동진출, 동 = "d4" }, out why);
            check("맞닿지 않은 동에는 지사를 낼 수 없다", !ok && 지사경쟁Policy.지문(board) == print, why);
            ok = 지사경쟁Policy.수두기(board, "me", new 경쟁수 { 종류 = 경쟁수종류.행사, 동 = "d1", 금액 = me.자본 + 1 }, out why);
            check("자본보다 큰 돈은 쓸 수 없다(자본은 음수가 되지 않는다)", !ok && 지사경쟁Policy.지문(board) == print && me.자본 >= 0, why);

            before = me.자본;
            ok = 지사경쟁Policy.수두기(board, "me", new 경쟁수 { 종류 = 경쟁수종류.새동진출, 동 = "d2" }, out why);
            check("맞닿은 동에 지사를 낸다: 진출비 · 기사 · 입점이 규칙대로", ok && me.자본 == before - rules.진출비
                && board.자리("me", "d2").기사 == rules.진출기사 && board.자리("me", "d2").입점 == rules.진출입점
                && 지사경쟁Policy.지사수(board, "me") == 2, why);

            // 나와 AI가 같은 함수 · 같은 비용
            var twin = Board();
            var ai = twin.운영자("a");
            var aiBefore = ai.자본;
            지사경쟁Policy.수두기(twin, "a", new 경쟁수 { 종류 = 경쟁수종류.기사모집, 동 = "d2" }, out why);
            check("AI도 같은 함수로 같은 비용을 낸다", aiBefore - ai.자본 == rules.기사모집비 && twin.자리("a", "d2").기사 == 3, string.Empty);

            var read = 지사경쟁Policy.지문(board);
            for (var i = 0; i < 50; i++)
            {
                지사경쟁Policy.점유율(board, "me", "d1");
                지사경쟁Policy.새몫(board, "me", "d1");
                지사경쟁Ai.수고르기(board, "a");
                string reason;
                지사경쟁Policy.둘수있나(board, "me", new 경쟁수 { 종류 = 경쟁수종류.기사모집, 동 = "d1" }, out reason);
            }
            check("조회 · 미리 보기 · AI의 셈은 판을 바꾸지 않는다(창을 여닫아도 그대로)", 지사경쟁Policy.지문(board) == read, string.Empty);
        }

        private static void Turns(Action<string, bool, string> check)
        {
            var board = Board();
            var rules = board.Rules;
            var sumOk = true; var inertiaOk = true; var capOk = true; var fundsOk = true; var served받아냄 = true;
            var worstSum = 0.0; var worstStep = 0.0;
            for (var t = 0; t < 30; t++)
            {
                var prev = board.자리들.ToDictionary(s => s.운영자 + "@" + s.동, s => s.몫);
                var turn = board.회차;
                지사경쟁Policy.회차넘기기(board);
                if (board.회차 != turn + 1) sumOk = false;
                foreach (var dong in board.동들)
                {
                    var sum = board.동자리(dong.Code).Sum(s => s.몫) + dong.밖몫;
                    worstSum = Math.Max(worstSum, Math.Abs(sum - 1.0));
                    if (Math.Abs(sum - 1.0) > 1e-9 || dong.밖몫 < -1e-12) sumOk = false;
                }
                foreach (var seat in board.자리들)
                {
                    double was;
                    prev.TryGetValue(seat.운영자 + "@" + seat.동, out was);
                    var step = Math.Abs(seat.몫 - was);
                    worstStep = Math.Max(worstStep, step);
                    if (step > 1.0 - rules.관성 + 1e-9) inertiaOk = false;
                    if (seat.받은주문 > 지사경쟁Policy.처리력(rules, rules.동기사한도, 100000) + 1e-9 || seat.놓친주문 < -1e-9
                        || Math.Abs(seat.받은주문 + seat.놓친주문 - seat.받을주문) > 1e-6) capOk = false;
                    if (seat.받아냄.HasValue && Math.Abs(seat.받아냄.Value - (1.0 - seat.놓친주문 / seat.받을주문)) > 1e-9) served받아냄 = false;
                }
                if (board.운영자들.Any(o => o.자본 < 0)) fundsOk = false;
            }
            check("30회차 내내 운영자 몫 + 플랫폼 밖 = 100%", sumOk, "가장 큰 어긋남 " + worstSum.ToString("0.0e0"));
            check("한 회차에 몫은 (1 − 관성)보다 크게 움직이지 않는다", inertiaOk, "가장 큰 걸음 " + (worstStep * 100).ToString("0.0") + "%p · 한도 " + ((1 - rules.관성) * 100).ToString("0") + "%p");
            check("받은 주문 + 놓친 주문 = 받을 주문이고 처리력을 넘지 않는다", capOk, string.Empty);
            check("받아냄 = 1 − 놓친 비율(지사 요약의 받아냄과 같은 뜻)", served받아냄, string.Empty);
            check("누구도 자본이 음수가 되지 않는다", fundsOk, string.Empty);

            // 놓친 주문은 신뢰를 깎고, 다 받아내면 신뢰가 오른다
            var tight = Board();
            tight.운영자들.RemoveAll(o => o.Id != "me");
            tight.자리들.RemoveAll(s => s.운영자 != "me");
            var seatMe = tight.자리("me", "d1");
            seatMe.몫 = 0.6; seatMe.기사 = 2; seatMe.입점 = 40;
            var trust = seatMe.신뢰;
            지사경쟁Policy.회차넘기기(tight);
            check("처리력이 모자라 놓치면 신뢰가 떨어진다", seatMe.놓친주문 > 0 && seatMe.신뢰 < trust, "놓친 " + seatMe.놓친주문.ToString("0") + "건 · 신뢰 " + trust.ToString("0.00") + " → " + seatMe.신뢰.ToString("0.00"));
            seatMe.기사 = 20; trust = seatMe.신뢰;
            지사경쟁Policy.회차넘기기(tight);
            check("다 받아내면 신뢰가 오른다", seatMe.놓친주문 < 1e-9 && seatMe.신뢰 > trust, string.Empty);

            // 일감이 적으면 기사가 떠난다(진출 때 기사 수 아래로는 내려가지 않는다)
            var idle = Board();
            idle.운영자들.RemoveAll(o => o.Id != "me");
            idle.자리들.RemoveAll(s => s.운영자 != "me");
            idle.자리("me", "d1").기사 = 12;      // 입점 8곳이 받는 주문은 64건 · 기사 12명이면 한 명에 5건 남짓
            idle.자리("me", "d1").입점 = 4;
            for (var t = 0; t < 30; t++) 지사경쟁Policy.회차넘기기(idle);
            check("일감이 적으면 기사가 떠나지만 진출 때 기사 수는 남는다", idle.자리("me", "d1").기사 < 12 && idle.자리("me", "d1").기사 >= rules.진출기사,
                "12명 → " + idle.자리("me", "d1").기사 + "명");
            check("혼자 있어도 플랫폼 밖 몫이 남는다(100%가 되지 않는다)", idle.동("d1").밖몫 > 0.05, "플랫폼 밖 " + (idle.동("d1").밖몫 * 100).ToString("0") + "%");

            // 연구 점수 = 지사 수 × 회차 × 지사 연구 점수
            var study = Board();
            for (var t = 0; t < 5; t++) 지사경쟁Policy.회차넘기기(study);
            check("연구 점수 = 지사 수 × 회차 × 2(가만히 둔 나: 지사 1곳 · 5회차 = 10)", study.운영자("me").연구점수 == 10, study.운영자("me").연구점수.ToString());

            // 씨앗 재현
            var one = Board(11); var two = Board(11);
            for (var t = 0; t < 30; t++) { 지사경쟁Policy.회차넘기기(one); 지사경쟁Policy.회차넘기기(two); }
            check("씨앗이 같으면 30회차 뒤 판이 똑같다", 지사경쟁Policy.지문(one) == 지사경쟁Policy.지문(two), 지사경쟁Policy.지문(one));
        }

        private static void Ai(Action<string, bool, string> check)
        {
            var board = Board();
            var rules = board.Rules;
            var countOk = true; var whyOk = true; var wordOk = true; var moved = 0;
            var banned = new[] { "깨어", "명덕", "영성", "인의예지" };
            for (var t = 0; t < 30; t++)
            {
                foreach (var op in board.운영자들.Where(o => !o.나))
                {
                    var moves = 지사경쟁Ai.수고르기(board, op.Id);
                    if (moves.Count > rules.AI최대수) countOk = false;
                    moved += moves.Count;
                    foreach (var move in moves)
                    {
                        if (string.IsNullOrEmpty(move.왜)) whyOk = false;
                        if (banned.Any(word => move.왜.Contains(word))) wordOk = false;
                        string reason;
                        if (!지사경쟁Policy.둘수있나(board, op.Id, move, out reason) && moves.IndexOf(move) == 0) countOk = false;
                    }
                }
                지사경쟁Policy.회차넘기기(board);
            }
            check("AI는 회차마다 0~3수를 두고 첫 수는 늘 둘 수 있는 수다", countOk, "30회차 동안 고른 수 " + moved + "개");
            check("AI의 수마다 「왜」 한 줄이 있다", whyOk && moved > 0, string.Empty);
            check("AI의 말 · 소식에 깨어 있음 · 명덕이 없다(지사장이 쓰는 말만)", wordOk && !board.소식.Any(n => banned.Any(word => n.글.Contains(word))), "소식 " + board.소식.Count + "줄");
            check("가만히 둔 나는 수를 두지 않는다", !board.소식.Any(n => n.운영자 == "me" && !n.글.Contains("놓친") && !n.글.Contains("떠남")), string.Empty);

            // 전략 넷이 같은 자리에서 시작하면 서로 다르게 자란다
            var shapes = new Dictionary<경쟁전략, string>();
            foreach (var strategy in new[] { 경쟁전략.할인형, 경쟁전략.속도형, 경쟁전략.입점형, 경쟁전략.동네밀착형 })
            {
                var solo = Board();
                solo.운영자들.RemoveAll(o => o.Id != "a" && o.Id != "me");
                solo.자리들.RemoveAll(s => s.운영자 != "a" && s.운영자 != "me");
                solo.운영자("a").전략 = strategy;
                for (var t = 0; t < 30; t++) 지사경쟁Policy.회차넘기기(solo);
                Func<string, int> count = word => solo.소식.Count(n => n.운영자 == "a" && n.글.Contains(word));
                shapes[strategy] = "지사 " + solo.운영자자리("a").Count() + " · 기사 모집 " + count("기사 모집") + " · 영업 " + count("음식점 영업") + " · 행사 " + count("행사");
            }
            check("전략 넷은 같은 자리에서 시작해도 30회차 뒤 모양이 서로 다르다", shapes.Values.Distinct().Count() == 4,
                string.Join(" / ", shapes.Select(k => k.Key + " " + k.Value)));

            var rich = Board(7, 400000); var poor = Board(7, 40000);
            for (var t = 0; t < 20; t++) { 지사경쟁Policy.회차넘기기(rich); 지사경쟁Policy.회차넘기기(poor); }
            check("난이도는 시작 자본으로만: 자본이 많은 AI가 더 넓게 편다", rich.운영자자리("b").Count() >= poor.운영자자리("b").Count()
                && rich.운영자자리("b").Sum(s => s.기사) > poor.운영자자리("b").Sum(s => s.기사),
                "지사 " + rich.운영자자리("b").Count() + " 대 " + poor.운영자자리("b").Count() + " · 기사 " + rich.운영자자리("b").Sum(s => s.기사) + " 대 " + poor.운영자자리("b").Sum(s => s.기사));
        }
    }
}
