using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 절기 장터(2026-11-24 · 사용자 09:35 · 09:49 「가안대로」): 전국 지도의 자료 층.
    //  - 게임 날짜(= 실제 달력)로 절기를 정하고, 그 달의 공식 제철 목록(농촌진흥청 「이달의 식재료」 · 해양수산부 「이달의 수산물」)을 켠다.
    //  - 지도: 그 품목을 다루는 산지유통센터(점) · 제철 수산물의 10월 위판 물량(위판장 원 크기).
    //  - 아래 띠: 24절기(누르면 그 절기를 미리 본다). 오른쪽: 제철 목록 → 품목 카드(24절기 원판 · 올해 값 선).
    //  - 값은 서울 동부 4구 21곳 월평균(서울시 OA-1170). 전국 지역 값 · 평년은 KAMIS를 모은 뒤.
    // 자료: Resources/hongdal-data-season-market.json ← artifacts/local/public-data/season-market-20261124/build_season_market.py
    public sealed partial class SagajeongDispatchView
    {
        private const string JangteoResource = "hongdal-data-season-market";

        [Serializable] private sealed class JangteoPack { public string schema, topic, title, release, note, portNote; public JangteoSource[] sources; public JangteoTerm[] terms; public JangteoMonth[] months; public JangteoItem[] items; public JangteoApc[] apc; public JangteoPort[] ports; }
        [Serializable] private sealed class JangteoSource { public string what, source, url, unit, sha256; }
        [Serializable] private sealed class JangteoTerm { public string name; public int y, m, d; public string hm; }
        [Serializable] private sealed class JangteoMonth { public int m; public string[] food; public string[] kinds; public string[] sea; public int seaYear; public float[] seaTons; }
        [Serializable] private sealed class JangteoItem { public string name, kind, label; public int[] prices; public int apc; }
        [Serializable] private sealed class JangteoApc { public float x, y; public string items; public string[] tags; }
        [Serializable] private sealed class JangteoPort { public string name; public float x, y, tons; public string top; public string[] sea; public float[] seaTons; }

        private JangteoPack jangteo;
        private bool jangteoTried;
        private readonly Dictionary<string, JangteoItem> jangteoItems = new();
        private bool nationSeasonOn;          // 전국 지도 왼쪽 「자료 층」의 절기 장터
        private string jangteoPick;           // 고른 품목. 없으면 제철 목록
        private int jangteoPreview = -1;      // 절기 띠에서 미리 보는 절기(terms 번호). -1 = 게임 날짜의 절기

        private static Color JangteoKind(string kind) => kind switch
        {
            "과일" => Ink(0xFF9F5A),
            "채소" => Ink(0x7CDB72),
            "곡물" => Ink(0xF2C35B),
            "버섯" => Ink(0xC9A27A),
            "견과" => Ink(0xB88A5A),
            "축산" => Ink(0xE08A8A),
            "수산" => Ink(0x7CC6F0),
            _ => Ink(0xABC1CB),
        };

        private static bool JangteoProduce(string kind) => kind is "과일" or "채소" or "곡물" or "버섯" or "견과";

        // 계절 오행: 입춘~곡우 목 · 입하~대서 화 · 입추~상강 금 · 입동~대한 수 (한 해 안의 순번 0 = 소한)
        private static Color JangteoSeasonColor(int place) => place >= 2 && place <= 7 ? Ink(0x7CDB72) :
            place >= 8 && place <= 13 ? Ink(0xFF7A6B) : place >= 14 && place <= 19 ? Ink(0xDDE3E8) : Ink(0x6FA8FF);

        private bool LoadJangteo()
        {
            if (jangteoTried) return jangteo != null;
            jangteoTried = true;
            var pack = ReadMapResource<JangteoPack>(JangteoResource);
            if (pack?.terms == null || pack.terms.Length < 24 || pack.months == null || pack.months.Length < 12 || pack.items == null)
            {
                Debug.LogWarning("Hongdal season market: 절기 장터 자료가 없어 층을 켜지 않습니다.");
                return false;
            }
            foreach (var item in pack.items)
                if (!string.IsNullOrEmpty(item.name)) jangteoItems[item.name] = item;
            jangteo = pack;
            Debug.Log($"Hongdal season market: 절기 {pack.terms.Length} · 품목 {pack.items.Length} · 산지유통센터 {pack.apc?.Length ?? 0} · " +
                $"위판장 {pack.ports?.Length ?? 0}");
            return true;
        }

        private static DateTime JangteoDate(JangteoTerm term) => new(term.y, term.m, term.d);

        private int JangteoToday()
        {
            var date = GameDate().Date;
            var found = 0;
            for (var index = 0; index < jangteo.terms.Length; index++)
                if (JangteoDate(jangteo.terms[index]) <= date) found = index;
            return found;
        }

        private int JangteoShownTerm => jangteoPreview >= 0 && jangteoPreview < jangteo.terms.Length ? jangteoPreview : JangteoToday();

        private JangteoMonth JangteoMonthOf(int term) =>
            jangteo.months.FirstOrDefault(month => month.m == jangteo.terms[term].m) ?? jangteo.months[0];

        private DateTime JangteoEnd(int term) => term + 1 < jangteo.terms.Length
            ? JangteoDate(jangteo.terms[term + 1]).AddDays(-1)
            : new DateTime(jangteo.terms[term].y, 12, 31);

        // 한 해 안에서의 순번(0 = 소한)
        private int JangteoPlace(int term) => term - Array.FindIndex(jangteo.terms, item => item.y == jangteo.terms[term].y);

        private static string JangteoWon(int value) => $"{value:#,0}원";
        private static string JangteoTons(float tons) => tons >= 10f ? $"{tons:#,0} t" : $"{tons:0.0} t";

        // 품목이 공식 목록에 든 달들
        private List<int> JangteoMonthsOf(string name)
        {
            var list = new List<int>();
            foreach (var month in jangteo.months)
                if (Array.IndexOf(month.food ?? Array.Empty<string>(), name) >= 0 || Array.IndexOf(month.sea ?? Array.Empty<string>(), name) >= 0)
                    list.Add(month.m);
            return list;
        }

        // ── 지도(Field 안쪽 좌표 · 다시 그릴 때만) ──────────────────────────────

        private void DrawJangteoMap(float now)
        {
            if (!LoadJangteo()) return;
            Paint(new Rect(0, 0, Field.width, Field.height), Fade(Canvas, 0.42f));      // 관세청 흐름은 옅게
            if (flowFocus != null) Paint(new Rect(0, 0, Field.width, Field.height), Fade(Canvas, 0.3f));      // 집중 보기: 바탕을 더 낮춘다
            BeginNationIcons();      // 시설 아이콘 배지(2026-11-24 13:50 「점으로서 표현하지 말고 … 아이콘 또는 형상」, SagajeongDispatchProduceFlow.cs)
            DrawFlowUnder(now);      // 배추 흐름 · 시도 넓이(관점에 따라, SagajeongDispatchProduceFlow.cs)
            var month = JangteoMonthOf(JangteoShownTerm);
            var pick = jangteoPick != null && jangteoItems.TryGetValue(jangteoPick, out var picked) ? picked : null;
            var produce = new HashSet<string>();
            for (var index = 0; index < month.food.Length; index++)
                if (JangteoProduce(index < month.kinds.Length ? month.kinds[index] : string.Empty)) produce.Add(month.food[index]);

            if (FlowLayerOn("apc") && jangteo.apc != null)
            for (var apcIndex = 0; apcIndex < jangteo.apc.Length; apcIndex++)
            {
                var apc = jangteo.apc[apcIndex];
                if (apc.tags == null || apc.tags.Length == 0) continue;
                string hit = null;
                if (pick != null)
                {
                    if (Array.IndexOf(apc.tags, pick.name) >= 0) hit = pick.name;
                }
                else
                    foreach (var tag in apc.tags)
                        if (produce.Contains(tag))
                        {
                            hit = tag;
                            break;
                        }
                if (hit == null) continue;
                var point = NationPoint(apc.x, apc.y);
                var color = JangteoKind(jangteoItems.TryGetValue(hit, out var item) ? item.kind : string.Empty);
                var badge = pick != null ? 9.5f : 8.5f;
                var key = "a:" + apcIndex;
                if (mapEffects) Disc(point, badge + 4f, Fade(color, (0.12f + 0.08f * (0.5f + 0.5f * Mathf.Sin(now * 1.7f + apc.x * 0.31f))) * FocusAlpha(key)));
                NationIconMark(point, "farm-hub", color, badge, pick != null ? 20f : 10f, key);      // 산지유통센터: 선별 · 포장 창고(색 = 품목). 겹치면 작은 점
            }

            if (jangteo.ports != null && FlowLayerOn("port")) DrawJangteoPorts(month, pick);
            DrawFlowOver(now);       // 주산지 · 공영도매시장 · 동네 값 · 경매 숨
            EndNationIcons();        // 모은 배지를 맨 위에 한 번에
        }

        private void DrawJangteoPorts(JangteoMonth month, JangteoItem pick)
        {
            var seaPick = pick != null && pick.kind == "수산" ? pick.name : null;
            if (pick != null && seaPick == null) return;         // 농산 품목을 고르면 위판장은 감춘다
            var shown = new List<(JangteoPort Port, float Tons)>();
            foreach (var port in jangteo.ports)
            {
                var tons = 0f;
                if (port.sea != null && port.seaTons != null)
                    for (var index = 0; index < port.sea.Length && index < port.seaTons.Length; index++)
                        if (seaPick != null ? port.sea[index] == seaPick : Array.IndexOf(month.sea, port.sea[index]) >= 0)
                            tons += port.seaTons[index];
                shown.Add((port, tons));
            }
            var sea = NationSea;
            foreach (var (port, tons) in shown.OrderBy(entry => entry.Tons))
            {
                var point = NationPoint(port.x, port.y);
                var key = "p:" + port.name;
                var alpha = FocusAlpha(key);
                if (tons <= 0.05f)
                {
                    Hoop(point, 3f, Fade(sea, 0.35f * alpha), 1.2f);
                    continue;
                }
                var radius = Mathf.Min(28f, 10f + Mathf.Sqrt(tons) * 0.4f);      // 원 크기 = 이 절기 위판 물량
                Disc(point, radius, Fade(sea, 0.20f * alpha));
                Hoop(point, radius, Fade(sea, 0.75f * alpha), 1.3f);
                NationIconMark(point, "fish-market", sea, 8.5f, 50f + Mathf.Min(40f, Mathf.Sqrt(tons)), key);      // 위판장: 물고기(물량 많은 곳이 먼저)
            }
            if (FlowFocusKind == "p" && focusSpecies != null)
            {
                // 위판장 집중: 그 어종을 받는 위판장 이름표(집중한 곳 먼저)
                foreach (var port in shown.Select(entry => entry.Port).Where(port => port.name == FlowFocusName || focusPorts.Contains(port.name))
                             .OrderByDescending(port => port.name == FlowFocusName ? float.MaxValue : PortTons(port, focusSpecies)).Take(5))
                {
                    var point = NationPoint(port.x, port.y);
                    var west = port.x < 570f;
                    FlowTag(point + new Vector2(west ? -14f : 14f, 0f), $"{port.name} {focusSpecies} {JangteoTons(PortTons(port, focusSpecies))}", 10,
                        port.name == FlowFocusName ? NationSea : Paper, !west, 0.9f);
                }
                return;
            }
            foreach (var (port, tons) in shown.Where(entry => entry.Tons > 0.05f).OrderByDescending(entry => entry.Tons).Take(4))
            {
                var point = NationPoint(port.x, port.y);
                var west = port.x < 570f;
                var alpha = FocusAlpha("p:" + port.name);
                FlowTag(point + new Vector2(west ? -14f : 14f, 0f), $"{port.name} {JangteoTons(tons)}", 10, Fade(Paper, alpha), !west, 0.86f * alpha);
            }
        }

        private bool PickJangteo(Vector2 local)
        {
            if (!LoadJangteo()) return false;
            return PickFlowFocus(local);      // 아이콘을 누르면 집중 보기(SagajeongDispatchProduceFocus.cs) · 빈 곳이면 풀고 아래 층 고르기로
        }

        // ── 아래 띠: 24절기 (Field 안쪽 좌표) ─────────────────────────────────

        private void DrawJangteoStrip()
        {
            var panel = NationLocal(NationClock);
            Paint(panel, Fade(Canvas, 0.95f), 9);
            var today = JangteoToday();
            var shown = JangteoShownTerm;
            var first = Array.FindIndex(jangteo.terms, item => item.y == jangteo.terms[shown].y);
            var cell = (panel.width - 70f) / 24f;
            for (var place = 0; place < 24 && first + place < jangteo.terms.Length; place++)
            {
                var index = first + place;
                var term = jangteo.terms[index];
                var rect = new Rect(panel.x + 6 + place * cell, panel.y + 4, cell - 1.5f, 42);
                var on = index == shown;
                Paint(rect, on ? Fade(Gold, 0.24f) : Fade(Tile, 0.7f), 4);
                Paint(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, 3), Fade(JangteoSeasonColor(place), on ? 1f : 0.65f), 1.5f);
                Text(new Rect(rect.x - 2, rect.y + 8, rect.width + 4, 17), term.name, 10, on ? Gold : Paper, on, TextAnchor.MiddleCenter);
                Text(new Rect(rect.x - 3, rect.y + 25, rect.width + 6, 13), $"{term.m}/{term.d}", 8, Soft, false, TextAnchor.MiddleCenter);
                if (index == today) Paint(new Rect(rect.center.x - 4, rect.yMax - 3, 8, 3), Gold, 1.5f);
                if (!Hit(rect)) continue;
                jangteoPreview = index == today ? -1 : index;
                Debug.Log($"Hongdal season market: 절기 {term.name}{(jangteoPreview >= 0 ? " 미리 보기" : " (오늘)")}");
            }
            var right = new Rect(panel.xMax - 62, panel.y + 4, 56, 42);
            var preview = jangteoPreview >= 0;
            Text(new Rect(right.x, right.y + 3, right.width, 18), preview ? "미리 보기" : "오늘", 11, preview ? Gold : Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(right.x - 4, right.y + 21, right.width + 8, 16), preview ? "누르면 오늘로" : GameDate().ToString("M/d"), 9, Soft, false,
                TextAnchor.MiddleCenter);
            if (preview && Hit(right)) jangteoPreview = -1;
        }

        // ── 왼쪽 아래 「표시」 자리 (논리 좌표) ─────────────────────────────────

        private void DrawJangteoLegend(Rect box)
        {
            if (DrawFlowLensBox(box)) return;      // 관점 고르기(흐름 자료가 있으면)
            Paint(box, Panel, 10);
            Text(new Rect(box.x + 13, box.y + 6, 160, 18), "절기 장터 표시", 11, Gold, true);
            var row = box.y + 34f;
            var left = box.x + 18f;
            var kinds = new[] { ("과일", "과일"), ("채소", "채소"), ("곡물", "곡물 · 견과") };
            for (var index = 0; index < kinds.Length; index++)
            {
                Disc(new Vector2(left + index * 54f, row), 4f, JangteoKind(kinds[index].Item1));
                Text(new Rect(left + 7 + index * 54f, row - 9, 52, 18), kinds[index].Item2, 9, Paper);
            }
            row += 16;
            Text(new Rect(box.x + 13, row - 8, 170, 16), "산지유통센터 · 이번 달 제철 품목", 9, Soft);
            row += 18;
            Disc(new Vector2(left + 4, row), 7f, Fade(NationSea, 0.26f));
            Hoop(new Vector2(left + 4, row), 7f, NationSea, 1.4f);
            Text(new Rect(left + 16, row - 9, 160, 18), "위판장 · 크기 = 10월 물량", 10, Paper);
            row += 17;
            Text(new Rect(box.x + 13, row - 7, 172, 28), "위판장 자리는 항구 대표점(수기) · 값은 서울 동부 4구 월평균", 9, Fade(Soft, 0.85f), false,
                TextAnchor.UpperLeft, true);
        }

        // ── 오른쪽 패널 (논리 좌표) ──────────────────────────────────────────

        private void DrawJangteoPanel()
        {
            var panel = NationPanel;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);
            if (DrawFlowFocusPanel()) return;      // 아이콘 집중 보기: 그 시설 카드(SagajeongDispatchProduceFocus.cs)
            if (DrawFlowLensPanel()) return;       // 입장 카드의 관점으로 그 관점의 자료 줄
            var term = JangteoShownTerm;
            if (jangteoPick != null && jangteoItems.TryGetValue(jangteoPick, out var chosen))
            {
                DrawJangteoCard(chosen, term);
                return;
            }
            var info = jangteo.terms[term];
            var month = JangteoMonthOf(term);
            var x = panel.x + 16;
            var width = panel.width - 32;
            var y = panel.y + 10;
            Text(new Rect(x, y, width, 16), "절기 장터 · 공식 제철 목록", 11, Gold, true);
            Text(new Rect(x, y + 17, 90, 30), info.name, 21, Paper, true);
            Text(new Rect(x + 70, y + 25, width - 70, 18), $"{info.m}/{info.d} ~ {JangteoEnd(term):M/d} · {month.m}월 목록" +
                (jangteoPreview >= 0 ? " · 미리 보기" : string.Empty), 10, Soft, false, TextAnchor.MiddleRight);
            y += 52;
            Text(new Rect(x, y, width, 15), "농산 · 농촌진흥청 「이달의 식재료」", 10, Mint, true);
            y += 18;
            var priced = new List<JangteoItem>();
            var plain = new List<JangteoItem>();
            foreach (var name in month.food)
                if (jangteoItems.TryGetValue(name, out var item))
                    (item.prices != null && item.prices.Count(value => value > 0) >= 3 ? priced : plain).Add(item);
            foreach (var item in priced.Take(6))
            {
                JangteoRow(new Rect(x - 4, y, width + 8, 30), item);
                y += 31;
            }
            y = JangteoChips(x, y + 3, width, plain.Concat(priced.Skip(6)));
            y += 8;
            Text(new Rect(x, y, width, 15), "수산 · 해수부 「이달의 수산물」" + (month.seaYear != jangteo.terms[term].y ? $" · {month.seaYear}년 목록" : string.Empty),
                10, NationSea, true);
            y += 18;
            var top = Mathf.Max(1f, month.seaTons?.Max() ?? 1f);
            for (var index = 0; index < month.sea.Length; index++)
            {
                var name = month.sea[index];
                var tons = month.seaTons != null && index < month.seaTons.Length ? month.seaTons[index] : 0f;
                var row = new Rect(x - 4, y, width + 8, 30);
                Paint(row, Fade(Tile, 0.55f), 6);
                Disc(new Vector2(row.x + 11, row.center.y), 4.5f, NationSea);
                Text(new Rect(row.x + 22, row.y + 2, 90, 16), name, 12, Paper, true);
                Text(new Rect(row.x + 22, row.y + 16, 120, 13), JangteoPortsOf(name, 2), 9, Soft);
                if (tons > 0.05f)
                {
                    Paint(new Rect(row.x + 118, row.center.y - 4, 62f * tons / top, 8), NationSea, 4);
                    Text(new Rect(row.xMax - 76, row.y, 70, 30), JangteoTons(tons), 11, Paper, true, TextAnchor.MiddleRight);
                }
                else Text(new Rect(row.xMax - 110, row.y, 104, 30), "10월 위판 기록 없음", 9, Soft, false, TextAnchor.MiddleRight);
                if (Hit(row)) jangteoPick = name;
                y += 33;
            }
            Text(new Rect(x, panel.yMax - 40, width, 34), "값 = 서울 동부 4구 21곳 월평균(서울시 OA-1170) · 위판 = 2026년 10월 물량(수협) · 전국 지역 값은 KAMIS를 모은 뒤",
                9, Soft, false, TextAnchor.UpperLeft, true);
            DrawFlowPeekBack();
        }

        private string JangteoPortsOf(string name, int count)
        {
            var list = (jangteo.ports ?? Array.Empty<JangteoPort>())
                .Select(port => (port.name, Tons: port.sea == null ? 0f : port.sea.Select((item, index) => item == name && index < port.seaTons.Length ? port.seaTons[index] : 0f).Sum()))
                .Where(entry => entry.Tons > 0.05f).OrderByDescending(entry => entry.Tons).Take(count).Select(entry => entry.name).ToArray();
            return list.Length > 0 ? "많은 곳 " + string.Join(" · ", list) : "위판장 자료 없음";
        }

        private void JangteoRow(Rect row, JangteoItem item)
        {
            Paint(row, Fade(Tile, 0.55f), 6);
            var color = JangteoKind(item.kind);
            Disc(new Vector2(row.x + 11, row.center.y), 4.5f, color);
            Text(new Rect(row.x + 22, row.y + 2, 80, 16), item.name, 12, Paper, true);
            Text(new Rect(row.x + 22, row.y + 16, 92, 13), item.label, 8, Soft);
            JangteoSpark(new Rect(row.x + 110, row.y + 6, 56, 18), item.prices, color);
            var last = -1;
            for (var index = item.prices.Length - 1; index >= 0; index--)
                if (item.prices[index] > 0)
                {
                    last = index;
                    break;
                }
            var high = 0;
            for (var index = 0; index < item.prices.Length; index++)
                if (item.prices[index] > item.prices[high]) high = index;
            if (last >= 0)
            {
                Text(new Rect(row.xMax - 80, row.y + 1, 74, 16), JangteoWon(item.prices[last]), 11, Paper, true, TextAnchor.MiddleRight);
                var change = Mathf.RoundToInt(100f * (item.prices[last] - item.prices[high]) / Mathf.Max(1, item.prices[high]));
                Text(new Rect(row.xMax - 96, row.y + 16, 90, 13), change == 0 ? $"{last + 1}월 · 올해 최고" : $"{high + 1}월보다 {change}%", 9,
                    change < 0 ? Mint : Ink(0xFF7A6B), true, TextAnchor.MiddleRight);
            }
            if (Hit(row)) jangteoPick = item.name;
        }

        private float JangteoChips(float x, float y, float width, IEnumerable<JangteoItem> items)
        {
            var left = x;
            foreach (var item in items)
            {
                var label = item.name + (JangteoProduce(item.kind) ? string.Empty : $"({item.kind})");
                var chip = GuNameSize(label, 10).x + 18f;
                if (left + chip > x + width)
                {
                    left = x;
                    y += 24;
                }
                var rect = new Rect(left, y, chip, 21);
                var color = JangteoKind(item.kind);
                Paint(rect, Fade(color, JangteoProduce(item.kind) ? 0.22f : 0.12f), 10);
                Text(rect, label, 10, JangteoProduce(item.kind) ? Paper : Soft, true, TextAnchor.MiddleCenter);
                if (Hit(rect)) jangteoPick = item.name;
                left += chip + 5;
            }
            return y + 24;
        }

        private static void JangteoSpark(Rect area, int[] prices, Color color)
        {
            if (prices == null) return;
            var values = prices.Where(value => value > 0).ToArray();
            if (values.Length < 2) return;
            float low = values.Min(), high = Mathf.Max(values.Max(), low + 1);
            Vector2? previous = null;
            var top = Vector2.zero;
            var topValue = -1;
            Vector2 last = Vector2.zero;
            for (var index = 0; index < prices.Length; index++)
            {
                if (prices[index] <= 0)
                {
                    previous = null;
                    continue;
                }
                var point = new Vector2(area.x + area.width * index / 11f, area.yMax - area.height * (prices[index] - low) / (high - low));
                if (previous.HasValue) Capsule(previous.Value, point, color, 1.6f);
                previous = point;
                last = point;
                if (prices[index] > topValue)
                {
                    topValue = prices[index];
                    top = point;
                }
            }
            Disc(top, 2.4f, Ink(0xFF7A6B));
            Disc(last, 2.8f, Ink(0xFFD395));
        }

        // ── 품목 카드 ─────────────────────────────────────────────────────────

        private void DrawJangteoCard(JangteoItem item, int term)
        {
            var panel = NationPanel;
            var x = panel.x + 16;
            var width = panel.width - 32;
            var y = panel.y + 10;
            if (ExpansionButton(new Rect(panel.xMax - 80, y, 64, 24), "← 목록", true, Tile))
            {
                jangteoPick = null;
                return;
            }
            Text(new Rect(x, y, width - 80, 16), $"{item.kind} · 제철 품목 카드", 11, Gold, true);
            Text(new Rect(x, y + 18, width, 30), item.name, 21, Paper, true);
            Text(new Rect(x, y + 47, width, 14), string.IsNullOrEmpty(item.label) ? "값 자료 없음 · 전국 값은 KAMIS를 모은 뒤" : $"값 = {item.label} · 서울 동부 4구 월평균", 9, Soft);

            var months = JangteoMonthsOf(item.name);
            DrawJangteoRing(new Vector2(panel.center.x, panel.y + 178), 84f, item, term, months);

            var chart = new Rect(x, panel.y + 286, width, 120);
            DrawJangteoChart(chart, item);

            var row = panel.y + 416f;
            Text(new Rect(x, row, width, 16), "공식 목록에 든 달 · " + (months.Count > 0 ? string.Join(" · ", months.Select(m => m + "월")) : "없음"), 10, Gold, true);
            row += 20;
            if (item.kind == "수산")
            {
                var month = jangteo.months.FirstOrDefault(entry => Array.IndexOf(entry.sea, item.name) >= 0);
                var tons = 0f;
                if (month != null) tons = month.seaTons[Array.IndexOf(month.sea, item.name)];
                Text(new Rect(x, row, width, 16), tons > 0.05f ? $"10월 위판 {JangteoTons(tons)} · {JangteoPortsOf(item.name, 3)}" : "10월 위판 기록 없음", 10, Paper);
            }
            else
                Text(new Rect(x, row, width, 16), item.apc > 0 ? $"이 품목을 다루는 산지유통센터 {item.apc}곳 · 지도에 켜짐" : "산지유통센터 품목 목록에 없음", 10, Paper);
            row += 20;
            Text(new Rect(x, row, width, 30), "원판: 바깥 고리 = 24절기 · 안쪽 막대 = 달마다 값(짧을수록 쌈) · 금빛 호 = 공식 목록에 든 달 · 바늘 = 오늘",
                9, Soft, false, TextAnchor.UpperLeft, true);
        }

        private float JangteoAngle(DateTime date, int year)
        {
            var spring = jangteo.terms.FirstOrDefault(term => term.y == year && term.name == "입춘");
            var start = spring != null ? JangteoDate(spring) : new DateTime(year, 2, 4);
            return -90f + (float)(date - start).TotalDays / 365.25f * 360f;
        }

        private static Vector2 JangteoAt(Vector2 centre, float angle, float radius) => centre + Direction(angle) * radius;

        private static void JangteoArc(Vector2 centre, float radius, float from, float to, Color color, float width)
        {
            while (to < from) to += 360f;
            var steps = Mathf.Max(2, Mathf.CeilToInt((to - from) / 5f));
            var previous = JangteoAt(centre, from, radius);
            for (var step = 1; step <= steps; step++)
            {
                var point = JangteoAt(centre, Mathf.Lerp(from, to, step / (float)steps), radius);
                Capsule(previous, point, color, width);
                previous = point;
            }
        }

        private void DrawJangteoRing(Vector2 centre, float radius, JangteoItem item, int term, List<int> months)
        {
            var year = jangteo.terms[term].y;
            var first = Array.FindIndex(jangteo.terms, entry => entry.y == year);
            for (var place = 0; place < 24; place++)
            {
                var index = first + place;
                var from = JangteoAngle(JangteoDate(jangteo.terms[index]), year);
                var to = JangteoAngle(index + 1 < jangteo.terms.Length ? JangteoDate(jangteo.terms[index + 1]) : new DateTime(year + 1, 1, 5), year);
                var on = index == term;
                JangteoArc(centre, radius - 7f, from + 0.8f, to - 0.8f, Fade(JangteoSeasonColor(place), on ? 0.95f : 0.32f), 12f);
                var name = jangteo.terms[index].name;
                var near = Mathf.Abs(place - JangteoPlace(term)) <= 1;
                if (on || !near && name is "입춘" or "입하" or "입추" or "입동")
                {
                    var label = JangteoAt(centre, (from + to) / 2f, radius + 13f);
                    Text(Centered(label, 34, 14), name, 9, on ? Gold : Soft, on, TextAnchor.MiddleCenter);
                }
            }
            foreach (var month in months)
            {
                var from = JangteoAngle(new DateTime(year, month, 1), year);
                var to = JangteoAngle(new DateTime(year, month, 1).AddMonths(1).AddDays(-1), year);
                JangteoArc(centre, radius + 3f, from, to, Gold, 4f);
            }
            var prices = item.prices ?? Array.Empty<int>();
            var high = prices.Length > 0 ? prices.Max() : 0;
            var last = Array.FindLastIndex(prices, value => value > 0);
            for (var index = 0; index < prices.Length; index++)
            {
                if (prices[index] <= 0) continue;
                var angle = JangteoAngle(new DateTime(year, index + 1, 15), year);
                var length = 46f * prices[index] / Mathf.Max(1, high);
                var color = index == last ? Gold : prices[index] == high ? Ink(0xFF7A6B) : Fade(Soft, 0.7f);
                Capsule(JangteoAt(centre, angle, 20f), JangteoAt(centre, angle, 20f + length), color, 6f);
            }
            var now = JangteoAngle(GameDate().Date, year);
            Capsule(JangteoAt(centre, now, 8f), JangteoAt(centre, now, radius + 2f), Gold, 1.6f);
            Disc(centre, 15f, Panel);
            Text(Centered(centre, 40, 16), jangteo.terms[term].name, 10, Gold, true, TextAnchor.MiddleCenter);
        }

        private void DrawJangteoChart(Rect area, JangteoItem item)
        {
            Paint(area, Fade(Panel, 0.9f), 8);
            var prices = item.prices ?? Array.Empty<int>();
            var values = prices.Where(value => value > 0).ToArray();
            Text(new Rect(area.x + 8, area.y + 4, area.width - 16, 14), "올해 값 · 서울 동부 4구 월평균", 9, Soft, true);
            if (values.Length < 2)
            {
                Text(new Rect(area.x, area.y + 20, area.width, area.height - 24), "값 자료 없음", 10, Soft, false, TextAnchor.MiddleCenter);
                return;
            }
            var plot = new Rect(area.x + 12, area.y + 24, area.width - 24, area.height - 60);
            float low = values.Min(), high = Mathf.Max(values.Max(), low + 1);
            Vector2? previous = null;
            var points = new List<(Vector2 Point, int Value, int Month)>();
            for (var index = 0; index < prices.Length; index++)
            {
                if (prices[index] <= 0)
                {
                    previous = null;
                    continue;
                }
                var point = new Vector2(plot.x + plot.width * index / 11f, plot.yMax - plot.height * (prices[index] - low) / (high - low));
                if (previous.HasValue) Capsule(previous.Value, point, JangteoKind(item.kind), 2.2f);
                previous = point;
                points.Add((point, prices[index], index + 1));
            }
            foreach (var (point, _, _) in points) Disc(point, 2.4f, JangteoKind(item.kind));
            var min = points.OrderBy(entry => entry.Value).First();
            var max = points.OrderByDescending(entry => entry.Value).First();
            Disc(min.Point, 4f, Mint);
            Disc(max.Point, 4f, Ink(0xFF7A6B));
            Text(new Rect(area.x + 8, area.yMax - 18, area.width / 2, 14), $"최저 {min.Month}월 {JangteoWon(min.Value)}", 9, Mint, true);
            Text(new Rect(area.center.x, area.yMax - 18, area.width / 2 - 8, 14), $"최고 {max.Month}월 {JangteoWon(max.Value)}", 9, Ink(0xFF7A6B), true,
                TextAnchor.MiddleRight);
            for (var month = 1; month <= 12; month += 3)
                Text(new Rect(plot.x + plot.width * (month - 1) / 11f - 12, plot.yMax + 3, 24, 12), month + "월", 8, Fade(Soft, 0.8f), false, TextAnchor.MiddleCenter);
        }
    }
}
