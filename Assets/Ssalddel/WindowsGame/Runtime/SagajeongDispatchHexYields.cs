using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 타일 수치 — 문명식 「이 칸에서 얻는 것」 (2026-11-25 사용자:
    //  「문명에서는 타일을 볼 때 식량이 플러스 몇, 망치가 몇 개, 골드가 몇 개 … 각각의 입장에 따라서 플러스가 되는 수치를
    //   타일에 각자 다르게 표현해서 활용」 → 「주요 입장들에 대해서」 → 시안(타일수치-시안-20261125)을 보고 「그대로 진행해줘」).
    //  - 주요 세 입장만: 음식점 사장(store) · 배달 기사(rider) · 홍달 경영자(owner). 다른 입장은 예전 타일 그대로.
    //  - 입장마다 수치 셋, 칸 위에는 0~5 단계(서울 자료가 덮는 칸 전체에서 다섯 등분), 깎이는 것은 −.
    //      음식점 사장: 손님(생활인구 250m → 타일, 실측 · 게임 시각의 평일/주말 · 점심/저녁) · 배달(예시) · 경쟁(카페 수, 실제, −)
    //        (2026-11-25 16:36 「그렇게 진행해줘」: 손님을 어림에서 서울 생활인구 250m로 — public-data/floating-250m-20261125)
    //      배달 기사:   콜(음식점 수, 실제) · 전달(사는 사람, 추정) · 길(건물 덮인 몫의 반대, 어림)
    //      홍달 경영자: 주문(예시) · 가맹(음식점 수, 실제) · 기사(사가정역에서의 거리, 예시)
    //  - 칸 색 = 그 입장 수치의 합(어두운 남색 → 민트 → 금빛). 가까이(HexLabelScale)에서는 칸마다 수치 세 줄.
    //  - 칸을 고르면 그릇 창 옆에 「이 칸의 수치」와 둘레 두 겹 가운데 「이 입장에 좋은 칸」 셋(누르면 그 칸을 고른다).
    //  - 어림 · 예시 값은 실제 자료가 오면 바꾼다: 손님 → 서울 생활인구 250 m 격자, 배달 · 주문 → 홍달 주문 기록, 길 → 길 속도, 기사 → 게임 기사 자리.
    // 보기와 고르기만 더한다. 영역 · 주문 · 돈 규칙을 읽거나 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const int HexYieldCafe = 8;     // foods의 「카페·제과」 자리

        private sealed class HexYieldSet
        {
            public string Key;
            public string Title;
            public string[] Names;
            public string[] Tags;               // 실제 · 추정 · 어림 · 예시
            public int[] Colours;               // Ink 값(처음 쓸 때 색으로)
            public int Min;
            public int Max;
        }

        private static readonly HexYieldSet[] HexYieldSets =
        {
            new HexYieldSet { Key = "store", Title = "음식점 사장", Names = new[] { "손님", "배달", "경쟁" }, Tags = new[] { "250m", "예시", "실제" },
                Colours = new[] { 0x6EE7CC, 0x6FA8FF, 0xFF7A6B }, Min = -5, Max = 10 },
            new HexYieldSet { Key = "rider", Title = "배달 기사", Names = new[] { "콜", "전달", "길" }, Tags = new[] { "실제", "추정", "어림" },
                Colours = new[] { 0xFFD395, 0x7CDB72, 0x6FA8FF }, Min = 0, Max = 15 },
            new HexYieldSet { Key = "owner", Title = "홍달 경영자", Names = new[] { "주문", "가맹", "기사" }, Tags = new[] { "예시", "실제", "예시" },
                Colours = new[] { 0x6FA8FF, 0xFF9F5A, 0xFFD395 }, Min = 0, Max = 15 },
        };

        private sbyte[] hexYieldValues;          // 타일 × 3입장 × 3수치
        private int[][] hexYieldFlow;            // 손님(생활인구) 0~5: [평일 점심 · 평일 저녁 · 주말 점심 · 주말 저녁][타일]. 팩에 lp가 없으면 null
        private HexPack hexYieldPack;
        private Rect hexYieldPanel;
        private readonly List<(Rect Row, int Tile)> hexYieldRows = new();

        private HexYieldSet HexYieldNow
        {
            get
            {
                if (string.IsNullOrEmpty(stanceKey)) return null;
                foreach (var set in HexYieldSets) if (set.Key == stanceKey) return set;
                return null;
            }
        }

        private int HexYieldSetIndex(HexYieldSet set) => Array.IndexOf(HexYieldSets, set);

        // 수치 표를 한 번 만든다(팩이 바뀌면 다시).
        private sbyte[] EnsureHexYields()
        {
            var pack = hexPack;
            if (pack?.tiles == null) return null;
            if (hexYieldValues != null && hexYieldPack == pack) return hexYieldValues;
            var count = pack.tiles.Length;
            var flow = new float[count];
            var people = new float[count];
            var shops = new float[count];
            var cover = new float[count];
            for (var i = 0; i < count; i++)
            {
                var tile = pack.tiles[i];
                if (tile == null || tile.w < 500) { flow[i] = people[i] = shops[i] = cover[i] = -1f; continue; }
                var commerce = tile.u != null && tile.u.Length > 2 ? tile.u[2] / 1000f : 0f;
                var floor = tile.b * Mathf.Max(tile.f, 10) / 10f;
                flow[i] = (floor * commerce + Mathf.Max(tile.p, 0) * 0.012f) * (string.IsNullOrEmpty(tile.s) ? 1f : 1.5f);
                people[i] = Mathf.Max(tile.p, 0);
                shops[i] = Mathf.Max(tile.n, 0);
                cover[i] = tile.c;
            }
            var qFlow = HexQuintiles(flow);
            var qPeople = HexQuintiles(people);
            var qShops = HexQuintiles(shops);
            var qCover = HexQuintiles(cover);
            hexYieldFlow = null;
            var hasFlow = false;
            foreach (var tile in pack.tiles) if (tile?.lp != null && tile.lp.Length >= 4) { hasFlow = true; break; }
            if (hasFlow)
            {
                hexYieldFlow = new int[4][];
                for (var band = 0; band < 4; band++)
                {
                    var raw = new float[count];
                    for (var i = 0; i < count; i++)
                    {
                        var tile = pack.tiles[i];
                        raw[i] = tile == null || tile.w < 500 || tile.lp == null || tile.lp.Length < 4 ? -1f : tile.lp[band];
                    }
                    hexYieldFlow[band] = HexQuintiles(raw);
                }
            }
            var home = HexInfluenceHome(pack);
            var values = new sbyte[count * 9];
            for (var i = 0; i < count; i++)
            {
                var tile = pack.tiles[i];
                if (tile == null || tile.w < 500) continue;
                var cafes = tile.m != null && tile.m.Length > HexYieldCafe ? tile.m[HexYieldCafe] : 0;
                var road = tile.c <= 0 ? 5 : Mathf.Clamp(6 - qCover[i], 1, 5);
                var ring = home == null ? 99 : HexDistance(tile.q - home.q, tile.r - home.r);
                var riders = Mathf.Clamp(5 - Mathf.RoundToInt(ring * 0.8f), 0, 5);
                var at = i * 9;
                values[at + 0] = (sbyte)qFlow[i];
                values[at + 1] = (sbyte)qPeople[i];
                values[at + 2] = (sbyte)(-HexCafeBin(cafes));
                values[at + 3] = (sbyte)qShops[i];
                values[at + 4] = (sbyte)qPeople[i];
                values[at + 5] = (sbyte)road;
                values[at + 6] = (sbyte)qPeople[i];
                values[at + 7] = (sbyte)qShops[i];
                values[at + 8] = (sbyte)riders;
            }
            hexYieldValues = values;
            hexYieldPack = pack;
            Debug.Log($"Hongdal hex yields: 타일 {count} · 세 입장 수치를 만들었습니다");
            return values;
        }

        // 사가정역 칸(기사 예시값의 가운데). 역 이름이 든 칸을 찾는다.
        private static HexTile HexInfluenceHome(HexPack pack)
        {
            foreach (var tile in pack.tiles)
                if (tile != null && !string.IsNullOrEmpty(tile.s) && tile.s.Contains(HexHomeStation)) return tile;
            return null;
        }

        private static int HexCafeBin(int count) =>
            count <= 0 ? 0 : count <= 2 ? 1 : count <= 5 ? 2 : count <= 8 ? 3 : count <= 12 ? 4 : 5;

        // 0보다 큰 값을 다섯 등분: 0은 0, 나머지는 1~5. 준비 전(−1)은 0.
        private static int[] HexQuintiles(float[] raw)
        {
            var sorted = new List<float>();
            foreach (var value in raw) if (value > 0f) sorted.Add(value);
            sorted.Sort();
            var result = new int[raw.Length];
            if (sorted.Count == 0) return result;
            for (var i = 0; i < raw.Length; i++)
            {
                if (raw[i] <= 0f) continue;
                var index = sorted.BinarySearch(raw[i]);
                if (index < 0) index = ~index - 1;
                while (index + 1 < sorted.Count && sorted[index + 1] <= raw[i]) index++;
                var rank = (index + 1) / (float)sorted.Count;
                result[i] = Mathf.Clamp(Mathf.CeilToInt(rank * 5f), 1, 5);
            }
            return result;
        }

        private int HexYield(int tile, HexYieldSet set, int which)
        {
            var values = EnsureHexYields();
            if (values == null || set == null || tile < 0 || tile * 9 + 8 >= values.Length) return 0;
            if (which == 0 && set.Key == "store" && hexYieldFlow != null) return hexYieldFlow[HexFlowBand()][tile];
            return values[tile * 9 + HexYieldSetIndex(set) * 3 + which];
        }

        // 게임 시각의 생활인구 칸: 평일/주말 × 점심(15시 전)/저녁. 게임 하루 = 실제 달력(GameDate).
        private int HexFlowBand()
        {
            var weekend = GameDate().DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var evening = GameSeconds() / 3600 >= 15;
            return (weekend ? 2 : 0) + (evening ? 1 : 0);
        }

        private static readonly string[] HexFlowNames = { "평일 점심", "평일 저녁", "주말 점심", "주말 저녁" };

        private int HexYieldSum(int tile, HexYieldSet set) => HexYield(tile, set, 0) + HexYield(tile, set, 1) + HexYield(tile, set, 2);

        // 합 → 어두운 남색 · 청록 · 민트 · 금빛
        private static Color HexYieldColour(int sum, HexYieldSet set)
        {
            var v = Mathf.InverseLerp(set.Min, set.Max, sum);
            Color a = Ink(0x1C2C3C), b = Ink(0x347878), c = Ink(0x6FE3C4), d = Ink(0xFFD395);
            if (v < 0.45f) return Color.Lerp(a, b, v / 0.45f);
            if (v < 0.75f) return Color.Lerp(b, c, (v - 0.45f) / 0.30f);
            return Color.Lerp(c, d, (v - 0.75f) / 0.25f);
        }

        private static string HexYieldText(int value) => value < 0 ? "−" + (-value) : value == 0 ? "0" : "+" + value;

        // DrawGuHexTiles의 칸 색: 수치 입장이면 합의 색, 아니면 성격 색.
        private Color HexTileFill(int index, HexTile tile)
        {
            var set = HexYieldNow;
            if (set == null || tile.w < 500) return HexKindColour(tile.k);
            return HexYieldColour(HexYieldSum(index, set), set);
        }

        // 가까이에서 칸마다 수치 세 줄. 그렸으면 true(성격 이름은 적지 않는다).
        private bool DrawHexYieldLabels(float alpha, float radius)
        {
            var set = HexYieldNow;
            if (set == null || EnsureHexYields() == null) return false;
            var pack = hexPack;
            var line = Mathf.Clamp(radius * 0.26f, 10f, 15f);
            var size = Mathf.RoundToInt(Mathf.Clamp(line - 1.5f, 9f, 13f));
            for (var i = 0; i < pack.tiles.Length; i++)
            {
                var tile = pack.tiles[i];
                if (tile.w < 500) continue;
                var at = GuPoint(tile.x, tile.y);
                if (at.x < -50f || at.y < -30f || at.x > Field.width + 50f || at.y > Field.height + 30f) continue;
                var top = at.y - line * 1.5f;
                for (var k = 0; k < 3; k++)
                {
                    var value = HexYield(i, set, k);
                    var colour = Ink(set.Colours[k]);
                    var row = new Rect(at.x - radius * 0.62f, top + line * k, radius * 1.24f, line);
                    Paint(new Rect(row.x + 2f, row.y + line * 0.5f - 2.5f, 5f, 5f), Fade(colour, (value == 0 ? 0.35f : 0.95f) * alpha), 2.5f);
                    Text(new Rect(row.x + 9f, row.y, row.width * 0.55f, line), set.Names[k], size - 1, Fade(Soft, 0.85f * alpha));
                    Text(new Rect(row.x, row.y, row.width - 2f, line), HexYieldText(value), size,
                        Fade(value < 0 ? Ink(0xFF9C90) : value == 0 ? Soft : Paper, alpha), true, TextAnchor.MiddleRight);
                }
            }
            return true;
        }

        private bool HexYieldPanelCovers(Vector2 point) => HexYieldNow != null && hexYieldPanel.width > 0f && hexYieldPanel.Contains(point);

        // 그릇 창 옆(세로 화면은 창 위)의 「이 칸의 수치」 + 「이 입장에 좋은 칸」. 누름을 썼으면 true.
        private bool DrawHexYieldPanel(Rect card)
        {
            var set = HexYieldNow;
            hexYieldRows.Clear();
            if (set == null || EnsureHexYields() == null || hexSelected < 0)
            {
                hexYieldPanel = Rect.zero;
                return false;
            }
            var pack = hexPack;
            var tile = pack.tiles[hexSelected];
            var portrait = PortraitScreen;
            var panel = portrait ? new Rect(card.x, card.y - 92f, card.width, 84f) : new Rect(card.xMax + 10f, card.y, 284f, 324f);
            hexYieldPanel = panel;
            var tint = Gold;
            Paint(new Rect(panel.x - 1.5f, panel.y - 1.5f, panel.width + 3f, panel.height + 3f), Fade(tint, 0.6f), 13);
            Paint(panel, Panel, 12);
            var x = panel.x + 14f;
            var wide = panel.width - 28f;
            Text(new Rect(x, panel.y + 10f, wide, 14f), $"{set.Title} · 이 칸의 수치", 10, Gold, true);
            Text(new Rect(x, panel.y + 10f, wide, 14f), "0~5", 10, Soft, false, TextAnchor.MiddleRight);
            var box = (wide - 12f) / 3f;
            for (var k = 0; k < 3; k++)
            {
                var value = tile.w < 500 ? 0 : HexYield(hexSelected, set, k);
                var r = new Rect(x + (box + 6f) * k, panel.y + 28f, box, 46f);
                Paint(r, Tile, 8);
                Paint(new Rect(r.x, r.y, r.width, 2.5f), Ink(set.Colours[k]), 1);
                Text(new Rect(r.x + 8f, r.y + 5f, r.width - 10f, 13f), set.Names[k], 10, Soft);
                Text(new Rect(r.x + 8f, r.y + 5f, r.width - 12f, 13f), set.Tags[k], 8, set.Tags[k] == "실제" ? Mint : Ink(0xC58BFF), true, TextAnchor.MiddleRight);
                Text(new Rect(r.x + 8f, r.y + 20f, r.width - 10f, 22f), HexYieldText(value), 18, value < 0 ? Ink(0xFF9C90) : Paper, true);
            }
            if (portrait) return HexYieldPanelClick(panel);

            // 어떻게 셈했나(2026-11-25 16:49 사용자: 「게임 내 수치가 어떻게 정해졌는지 계산식 같은 거 작게 표시」)
            var why = panel.y + 80f;
            Paint(new Rect(x, why, wide, 70f), Fade(Canvas, 0.55f), 7);
            if (tile.w >= 500)
            {
                for (var k = 0; k < 3; k++)
                {
                    var line = new Rect(x + 8f, why + 4f + k * 15f, wide - 12f, 14f);
                    Paint(new Rect(line.x, line.y + 5f, 5f, 5f), Ink(set.Colours[k]), 2.5f);
                    Text(new Rect(line.x + 9f, line.y, 30f, 14f), set.Names[k], 9, Soft, true);
                    Text(new Rect(line.x + 38f, line.y, line.width - 38f, 14f), HexYieldWhy(hexSelected, set, k), 9, Paper);
                }
                var a = HexYield(hexSelected, set, 0); var b = HexYield(hexSelected, set, 1); var c = HexYield(hexSelected, set, 2);
                Text(new Rect(x + 8f, why + 50f, wide - 12f, 14f),
                    $"합 {HexYieldText(a)} {HexYieldText(b)} {HexYieldText(c)} = {HexYieldText(a + b + c)}  →  칸 색(어두움 {set.Min} ~ 금빛 {set.Max})", 9, Gold, true);
            }
            else Text(new Rect(x + 8f, why + 4f, wide - 12f, 40f), "서울 자료가 이 칸을 절반도 덮지 않아 셈하지 않았습니다", 9, Soft, false, TextAnchor.UpperLeft, true);

            // 이 입장에 좋은 칸: 고른 칸 둘레 두 겹 가운데 합이 큰 셋
            var y = panel.y + 158f;
            Text(new Rect(x, y, wide, 14f), "둘레 두 겹 · 이 입장에 좋은 칸", 10, Gold, true);
            var best = new List<int>();
            for (var dq = -2; dq <= 2; dq++)
                for (var dr = -2; dr <= 2; dr++)
                {
                    if (HexDistance(dq, dr) > 2) continue;
                    if (!hexIndex.TryGetValue(HexKey(tile.q + dq, tile.r + dr), out var index)) continue;
                    if (pack.tiles[index].w < 500) continue;
                    best.Add(index);
                }
            best.Sort((a, b) => HexYieldSum(b, set).CompareTo(HexYieldSum(a, set)));
            for (var i = 0; i < best.Count && i < 3; i++)
            {
                var index = best[i];
                var other = pack.tiles[index];
                var row = new Rect(x, y + 18f + i * 30f, wide, 26f);
                var over = row.Contains(Event.current.mousePosition);
                Paint(row, index == hexSelected ? Fade(Gold, 0.22f) : over ? Rim : Tile, 7);
                Paint(new Rect(row.x + 6f, row.y + 5f, 16f, 16f), Gold, 8);
                Text(new Rect(row.x + 6f, row.y + 5f, 16f, 16f), (i + 1).ToString(), 10, Canvas, true, TextAnchor.MiddleCenter);
                var name = !string.IsNullOrEmpty(other.s) ? other.s.Split('·')[0] : HexKindName(other.k);
                var dong = HexDongOf(other.d)?.name ?? string.Empty;
                Text(new Rect(row.x + 28f, row.y, row.width - 80f, 26f), dong.Length > 0 ? $"{name} · {dong}" : name, 11, Paper);
                Text(new Rect(row.x, row.y, row.width - 8f, 26f), $"합 {HexYieldSum(index, set)}", 11, Gold, true, TextAnchor.MiddleRight);
                hexYieldRows.Add((row, index));
            }
            var flowLine = set.Key == "store" && tile.lp != null && tile.lp.Length >= 4
                ? $"손님 = 지나는 사람 {HexFlowNames[HexFlowBand()]} 시간당 {tile.lp[HexFlowBand()]:N0}명 · 서울 생활인구 250m"
                : "누르면 그 칸을 고릅니다 · 어림 · 예시는 실제 자료가 오면 바뀝니다";
            Text(new Rect(x, panel.yMax - 40f, wide, 30f), flowLine, 9, set.Key == "store" && tile.lp != null ? Mint : Soft, false, TextAnchor.UpperLeft, true);
            return HexYieldPanelClick(panel);
        }

        // 수치 하나가 어디서 왔는지 한 줄(옆 창 「어떻게 셈했나」). 「5등분 n째」 = 서울 타일을 그 값으로 줄 세워 다섯 묶음 가운데 n째.
        private string HexYieldWhy(int index, HexYieldSet set, int which)
        {
            var tile = hexPack.tiles[index];
            var v = HexYield(index, set, which);
            var cafes = tile.m != null && tile.m.Length > HexYieldCafe ? tile.m[HexYieldCafe] : 0;
            var people = Mathf.Max(tile.p, 0);
            var shops = Mathf.Max(tile.n, 0);
            switch (set.Key + which)
            {
                case "store0":
                    if (tile.lp != null && tile.lp.Length >= 4 && hexYieldFlow != null)
                        return $"{HexFlowNames[HexFlowBand()]} {tile.lp[HexFlowBand()]:N0}명/시 · 서울 5등분 {v}째";
                    return $"상업 연면적 · 역으로 어림 · 5등분 {v}째";
                case "store1": return $"사는 사람 {people:N0}명 · 5등분 {v}째 · 예시";
                case "store2": return $"카페 {cafes}곳 → {HexYieldText(v)} (0 · ~2 · ~5 · ~8 · ~12 · 13+)";
                case "rider0": return $"음식점 {shops:N0}곳 · 5등분 {v}째";
                case "rider1": return $"사는 사람 {people:N0}명 · 5등분 {v}째";
                case "rider2": return $"건물 덮인 몫 {tile.c / 10}% · 빽빽할수록 느림 · 어림";
                case "owner0": return $"사는 사람 {people:N0}명 · 5등분 {v}째 · 예시";
                case "owner1": return $"음식점 {shops:N0}곳 · 5등분 {v}째";
                case "owner2":
                    var home = HexInfluenceHome(hexPack);
                    var ring = home == null ? -1 : HexDistance(tile.q - home.q, tile.r - home.r);
                    return ring < 0 ? "기사 자리 · 예시" : $"{HexHomeStation}에서 {ring}칸 · 가까울수록 많음 · 예시";
            }
            return string.Empty;
        }

        private bool HexYieldPanelClick(Rect panel)
        {
            var current = Event.current;
            if (!panel.Contains(current.mousePosition)) return false;
            if (current.type == EventType.MouseDown)
                foreach (var (row, index) in hexYieldRows)
                    if (row.Contains(current.mousePosition))
                    {
                        hexSelected = index;
                        break;
                    }
            return HexCardSwallow(true, current);
        }

        // 촬영: 세 입장으로 사가정역 칸을 가까이 · 멀리 (관찰용).
        private IEnumerator ViewProofHexYields(string directory, ViewProofReceipt receipt)
        {
            if (mapLevel != MapLevel.District) LoadMapLevel(MapLevel.District);
            for (var frame = 0; frame < 8; frame++) yield return null;
            if (!GuMapVisible || EnsureHexTiles() == null) yield break;
            var keepStance = stanceKey;
            var keepLayer = hexLayerOn;
            hexLayerOn = true;
            guStation = null;
            var station = HexInfluenceTile(out _);
            if (station < 0) station = 0;
            var trace = new System.Text.StringBuilder();
            foreach (var set in HexYieldSets)
            {
                stanceKey = set.Key;
                PlaceGuOnHex(hexPack.tiles[station], 0.32f);
                hexSelected = station;
                for (var frame = 0; frame < 10; frame++) yield return null;
                yield return SaveFrame(directory, $"148-yield-{set.Key}.png");
                trace.AppendLine($"{set.Key} · 띠 {HexFlowNames[HexFlowBand()]} · lp {(hexPack.tiles[station].lp == null ? "없음" : string.Join("/", hexPack.tiles[station].lp))} · 칸 {station} · 수치 {HexYield(station, set, 0)}/{HexYield(station, set, 1)}/{HexYield(station, set, 2)} · 합 {HexYieldSum(station, set)} · 창 {HexCardVisible} · 옆 {hexYieldPanel} · 배율 {guScale:0.00}");
            }
            stanceKey = "store";
            PlaceGuOnHex(hexPack.tiles[station], 0.22f);
            hexSelected = -1;
            for (var frame = 0; frame < 10; frame++) yield return null;
            yield return SaveFrame(directory, "148-yield-store-far.png");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "148-yield-trace.txt"), trace.ToString()); } catch (Exception) { }
            stanceKey = keepStance;
            hexLayerOn = keepLayer;
            hexSelected = -1;
            Debug.Log("Hongdal hex yields proof:\n" + trace);
        }
    }
}
