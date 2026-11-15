using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 홍달 플레이스 순위 · 쪽 · 주문자 투영의 순수 검사 (2026-11-15). 빌드의 kernel-checks.txt에 붙는다.
    internal static class 플레이스Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));
            var weights = new 플레이스비중();
            var cafe = new 플레이스검색어 { Id = "kw:cafe", Label = "카페", Kind = "category", Value = "카페" };
            var latte = new 플레이스검색어 { Id = "kw:latte", Label = "라떼", Kind = "menu", Value = "라떼", Category = "카페" };

            플레이스장소 Place(string id, double metres, string category = "카페", params string[] tags) =>
                new() { Id = id, Name = id, Category = category, Tags = tags, DistanceMetres = metres };
            List<플레이스장소> Many(int count) =>
                Enumerable.Range(0, count).Select(i => Place($"p{i:000}", 10 * (i % 50), "카페", "라떼")).ToList();

            // 쪽: 0 · 9 · 10 · 11 · 24 · 100 · 101
            var expected = new[] { (0, 0), (9, 1), (10, 1), (11, 2), (24, 3), (100, 10), (101, 10) };
            var pagesOk = expected.All(pair => 플레이스Policy.PageCount(플레이스Policy.Rank(Many(pair.Item1), latte, weights).Ranked.Count, weights) == pair.Item2);
            Check("후보 0 · 9 · 10 · 11 · 24 · 100 · 101 → 0 · 1 · 1 · 2 · 3 · 10 · 10쪽", pagesOk);
            var big = 플레이스Policy.Rank(Many(101), latte, weights);
            Check("101곳이면 101번째는 쪽 밖이지만 후보 수는 숨기지 않음",
                big.Ranked.Count == 101 && big.Ranked[100].Page == 11 && 플레이스Policy.Shown(101, weights) == 100 &&
                플레이스Policy.ForCustomer(big.Ranked, 11, weights).Count == 0);
            var twentyFour = 플레이스Policy.Rank(Many(24), latte, weights);
            Check("24곳의 3쪽은 4곳", 플레이스Policy.ForCustomer(twentyFour.Ranked, 3, weights).Count == 4);

            // 동점과 입력 순서
            var tie = new List<플레이스장소> { Place("b", 300, "카페", "라떼"), Place("a", 300, "카페", "라떼"), Place("c", 100, "카페", "라떼") };
            var forward = 플레이스Policy.Rank(tie, latte, weights).Ranked.Select(row => row.Place.Id).ToArray();
            tie.Reverse();
            var backward = 플레이스Policy.Rank(tie, latte, weights).Ranked.Select(row => row.Place.Id).ToArray();
            Check("동점은 장소 ID 순이고 넣는 순서가 달라도 같은 순위표", forward.SequenceEqual(new[] { "c", "a", "b" }) && forward.SequenceEqual(backward));

            // 태그
            var plain = Place("x", 200, "카페");
            var tagged = Place("y", 200, "카페", "라떼");
            var repeated = Place("z", 200, "카페", "라떼", "라떼", "라떼");
            var unsold = Place("w", 200, "카페", "피자", "치킨", "족발");
            var tagTable = 플레이스Policy.Rank(new[] { plain, tagged, repeated, unsold }, latte, weights).Ranked.ToDictionary(row => row.Place.Id);
            Check("메뉴 태그가 맞으면 적합 1, 분류만 맞으면 0.6", tagTable["y"].Fit == 1 && tagTable["x"].Fit == 0.6);
            Check("같은 태그를 되풀이해도 · 다른 메뉴 태그를 많이 넣어도 점수가 오르지 않음",
                tagTable["z"].Milli == tagTable["y"].Milli && tagTable["w"].Milli == tagTable["x"].Milli);

            // 키워드마다 다른 순위
            var mixed = new List<플레이스장소> { Place("near-no-latte", 50, "카페"), Place("far-latte", 600, "카페", "라떼") };
            var byCafe = 플레이스Policy.Rank(mixed, cafe, weights).Ranked[0].Place.Id;
            var byLatte = 플레이스Policy.Rank(mixed, latte, weights).Ranked[0].Place.Id;
            Check("키워드마다 순위가 따로(카페 1위가 라떼 1위는 아님)", byCafe == "near-no-latte" && byLatte == "far-latte");

            // 제외
            var outside = 플레이스Policy.Rank(new[] { Place("snack", 100, "분식", "떡볶이"), Place("lost", double.NaN, "카페", "라떼"), Place("ok", 100, "카페", "라떼") }, latte, weights);
            Check("의도가 다른 곳 · 자리를 모르는 곳은 점수 없이 제외하고 이유를 남김",
                outside.Ranked.Count == 1 && outside.Excluded.Count == 2 && outside.Excluded.All(item => !string.IsNullOrEmpty(item.Reason)));

            // 점수 범위
            var best = 플레이스Policy.Milli(1, 1, 1, 1, weights);
            var worst = 플레이스Policy.Milli(0, 0, 0, 0, weights);
            var odd = 플레이스Policy.Milli(double.NaN, 2, -1, 0.5, weights);
            Check("기본 점수는 0~100,000 정수(깨진 값은 범위 안으로)", best == 100000 && worst == 0 && odd >= 0 && odd <= 100000);

            // 주문자 투영
            Check("주문자 카드에는 허용한 칸만(점수 · 요소 · 검색량 · 판본 없음)", 플레이스Policy.CustomerFieldsOnly());
            var cards = 플레이스Policy.ForCustomer(twentyFour.Ranked, 1, weights);
            Check("주문자 카드 순서 = 순위표 1쪽 순서", cards.Select(card => card.Order).SequenceEqual(Enumerable.Range(1, 10)) &&
                cards.Select(card => card.PlaceId).SequenceEqual(twentyFour.Ranked.Take(10).Select(row => row.Place.Id)));
            var before = string.Join(",", twentyFour.Ranked.Select(row => row.Place.Id + ":" + row.Milli));
            플레이스Policy.ForCustomer(twentyFour.Ranked, 2, weights);
            플레이스Policy.ForCustomer(twentyFour.Ranked, 1, weights);
            Check("쪽을 넘겨도 순위표가 바뀌지 않음", before == string.Join(",", twentyFour.Ranked.Select(row => row.Place.Id + ":" + row.Milli)));
            var rounded = 플레이스Policy.ForCustomer(플레이스Policy.Rank(new[] { Place("r", 523.6, "카페", "라떼") }, latte, weights).Ranked, 1, weights);
            Check("주문자에게 거리는 10 m 단위", rounded.Count == 1 && rounded[0].DistanceMetres == 520);
            return results;
        }
    }
}
