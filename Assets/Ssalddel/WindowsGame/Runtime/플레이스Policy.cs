using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ssalddel.WindowsGame
{
    // 홍달 플레이스 순위 · 쪽 · 주문자 투영의 순수 규칙 (2026-11-15). System만 쓴다.
    // 계획: Hongdal docs/AI/Handoffs/2026-11-15-Claude-개발계획-홍달플레이스-종합-정본하나뷰셋.md 플2(점수 · 순위표 · 쪽)와 플4(투영)의 첫 조각.
    //  - 기본 점수 = 비중(적합 · 접근 · 정보 · 이전 반응)의 합. r238 시험값 40 · 25 · 15 · 20이면 0~100,000(천분의 1점) 정수다.
    //    r239의 잠정 관심 · 제한 조정은 아직 0이라 이 조각에는 칸을 두지 않는다(정본 모듈 걸음에서 더한다).
    //  - 검색과 맞지 않는 곳(분류 · 판매 태그가 의도와 다름)과 자리를 모르는 곳은 점수를 매기지 않고 제외 이유를 남긴다.
    //    자리를 모르는 곳의 거리를 0이나 추정으로 채우지 않는다.
    //  - 정렬은 총점이 높은 순, 같으면 장소 ID의 글자 순. 넣는 순서가 달라도 같은 순위표다. 쪽은 순위에서 정해지고, 쪽을 넘겨도 다시 정렬하지 않는다.
    //  - 주문자 투영(플레이스카드)은 허용 목록이다. 점수 · 요소 · 검색량 · 판본 칸이 아예 없다.
    // 실제 지도 · 검색 서비스의 알고리즘이나 가중치가 아니다(게임의 시험값). 업무 상태를 읽지도 바꾸지도 않는다.
    internal sealed class 플레이스장소
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Category = string.Empty;
        public string[] Tags = Array.Empty<string>();
        public double Info = 1;                       // 필요한 정보의 정확성 0~1 (운영자 쪽 값, 주문자에게 보이지 않음)
        public string State = "영업 중";               // 주문자에게 보이는 영업 상태 글
        public double DistanceMetres = double.NaN;    // 검색 범위의 기준점에서 직선거리. 모르면 NaN
        public int Building = -1;                     // 지도의 건물 번호(모르면 -1)
    }

    internal sealed class 플레이스검색어
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        public string Kind = string.Empty;            // "category"(분류 의도) · "menu"(메뉴 의도)
        public string Value = string.Empty;           // 분류 이름 또는 메뉴 이름
        public string Category = string.Empty;        // 메뉴 의도일 때 분류만 맞아도 후보가 되는 분류(예: 라떼 → 카페)
    }

    internal sealed class 플레이스비중
    {
        public double Fit = 40, Access = 25, Info = 15, Prior = 20;
        public double CategoryOnlyFit = 0.6;          // 메뉴 태그는 없고 분류만 맞을 때의 적합도
        public double NeutralPrior = 0.5;             // 관측이 없을 때의 이전 반응(중립)
        public double ReachMetres = 1000;             // 접근성이 0이 되는 직선거리
        public int PageSize = 10, MaxPages = 10;
    }

    internal sealed class 플레이스순위
    {
        public 플레이스장소 Place;
        public double Fit, Access, Info, Prior;
        public int Milli;                             // 기본 점수(천분의 1점)
        public int Rank, Page;
    }

    internal sealed class 플레이스순위표
    {
        public readonly List<플레이스순위> Ranked = new();
        public readonly List<(플레이스장소 Place, string Reason)> Excluded = new();
    }

    // 주문자 뷰가 받는 카드. 이 칸 말고는 없다(플레이스Policy.CustomerFields와 같아야 한다 — 검사).
    internal sealed class 플레이스카드
    {
        public readonly int Order;                    // 목록 순서(= 지도 번호)
        public readonly string PlaceId;
        public readonly string Name;
        public readonly string Category;
        public readonly string[] Tags;
        public readonly int DistanceMetres;           // 10 m 단위로 반올림
        public readonly string State;
        public readonly int Building;

        public 플레이스카드(int order, string placeId, string name, string category, string[] tags, int distanceMetres, string state, int building)
        {
            Order = order;
            PlaceId = placeId;
            Name = name;
            Category = category;
            Tags = tags;
            DistanceMetres = distanceMetres;
            State = state;
            Building = building;
        }
    }

    internal static class 플레이스Policy
    {
        public static readonly string[] CustomerFields = { "Order", "PlaceId", "Name", "Category", "Tags", "DistanceMetres", "State", "Building" };

        public static double? Fit(플레이스장소 place, 플레이스검색어 keyword, 플레이스비중 weights)
        {
            if (place == null || keyword == null || weights == null) return null;
            var tags = place.Tags ?? Array.Empty<string>();
            if (keyword.Kind == "category") return place.Category == keyword.Value ? (double?)1.0 : null;
            if (tags.Contains(keyword.Value, StringComparer.Ordinal)) return 1.0;           // 같은 태그를 여러 번 넣어도 1.0
            if (!string.IsNullOrEmpty(keyword.Category) && place.Category == keyword.Category) return Clamp01(weights.CategoryOnlyFit);
            return null;
        }

        public static double? Access(플레이스장소 place, 플레이스비중 weights)
        {
            if (place == null || weights == null || double.IsNaN(place.DistanceMetres) || double.IsInfinity(place.DistanceMetres) ||
                place.DistanceMetres < 0 || weights.ReachMetres <= 0) return null;
            return Clamp01(1 - place.DistanceMetres / weights.ReachMetres);
        }

        public static int Milli(double fit, double access, double info, double prior, 플레이스비중 weights) =>
            (int)Math.Round(1000 * (Clamp01(fit) * weights.Fit + Clamp01(access) * weights.Access + Clamp01(info) * weights.Info +
                Clamp01(prior) * weights.Prior), MidpointRounding.AwayFromZero);

        public static 플레이스순위표 Rank(IEnumerable<플레이스장소> places, 플레이스검색어 keyword, 플레이스비중 weights)
        {
            var table = new 플레이스순위표();
            if (places == null || keyword == null || weights == null) return table;
            foreach (var place in places)
            {
                if (place == null) continue;
                var fit = Fit(place, keyword, weights);
                if (fit == null)
                {
                    table.Excluded.Add((place, "분류 · 판매 태그가 검색 의도와 맞지 않음"));
                    continue;
                }
                var access = Access(place, weights);
                if (access == null)
                {
                    table.Excluded.Add((place, "자리 미확인 · 거리를 정하지 않음"));
                    continue;
                }
                var info = double.IsNaN(place.Info) ? 0 : Clamp01(place.Info);
                var prior = Clamp01(weights.NeutralPrior);
                table.Ranked.Add(new 플레이스순위
                {
                    Place = place, Fit = fit.Value, Access = access.Value, Info = info, Prior = prior,
                    Milli = Milli(fit.Value, access.Value, info, prior, weights),
                });
            }
            table.Ranked.Sort((a, b) =>
            {
                var byScore = b.Milli.CompareTo(a.Milli);
                return byScore != 0 ? byScore : string.CompareOrdinal(a.Place.Id, b.Place.Id);
            });
            var size = Math.Max(1, weights.PageSize);
            for (var i = 0; i < table.Ranked.Count; i++)
            {
                table.Ranked[i].Rank = i + 1;
                table.Ranked[i].Page = i / size + 1;
            }
            return table;
        }

        // 보이는 쪽 수: 후보가 없으면 0, 많아도 최대 쪽 수까지.
        public static int PageCount(int candidates, 플레이스비중 weights)
        {
            if (candidates <= 0 || weights == null) return 0;
            var size = Math.Max(1, weights.PageSize);
            return Math.Min(Math.Max(1, weights.MaxPages), (candidates + size - 1) / size);
        }

        // 쪽에 보이는 후보 수의 끝(최대 쪽 수 × 한 쪽 수).
        public static int Shown(int candidates, 플레이스비중 weights) =>
            weights == null ? 0 : Math.Min(Math.Max(0, candidates), Math.Max(1, weights.PageSize) * Math.Max(1, weights.MaxPages));

        // 주문자 투영: 그 쪽의 카드만, 허용한 칸만.
        public static List<플레이스카드> ForCustomer(IReadOnlyList<플레이스순위> ranked, int page, 플레이스비중 weights)
        {
            var cards = new List<플레이스카드>();
            if (ranked == null || weights == null || page < 1 || page > PageCount(ranked.Count, weights)) return cards;
            foreach (var row in ranked)
            {
                if (row.Page != page) continue;
                var place = row.Place;
                var metres = (int)(Math.Round(place.DistanceMetres / 10, MidpointRounding.AwayFromZero) * 10);
                cards.Add(new 플레이스카드(row.Rank, place.Id, place.Name, place.Category,
                    (place.Tags ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray(), metres,
                    string.IsNullOrEmpty(place.State) ? "영업 중" : place.State, place.Building));
            }
            return cards;
        }

        // 주문자 카드의 공개 칸이 허용 목록과 꼭 같은가.
        public static bool CustomerFieldsOnly()
        {
            var fields = typeof(플레이스카드).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var allowed = CustomerFields.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            return fields.SequenceEqual(allowed, StringComparer.Ordinal);
        }

        private static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
    }
}
