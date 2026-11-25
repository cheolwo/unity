using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 서울 25구 비교 (2026-11-23 사용자: 「계속 구현 작업 진행해줘」 — Codex 지도 축척 자료의 다음 걸음 「구 비교 지표」).
    //  - 서울 지도(수도권 단계)에서 25구를 공표 통계 하나로 칠한다. 왼쪽 창에서 지표를 고르고, 구를 누르면 오른쪽에 그 구의 지표 카드.
    //  - 자료: Resources/hongdal-data-seoul-gu.json — map-icons-20261123-r1/bake_seoul_gu.py가 Codex 색인
    //    (public-data/hongdal-map-scale-index-20261123/district, v_district_map_reading · SGIS 2025-06-30 경계)에서 굽는다.
    //  - 색은 25구 안의 차례(5단)다. 값이 없거나 미공표면 빗금 · 「자료 미확인」 — 0으로 칠하지 않는다.
    //  - 아이콘은 Codex 대응(district_metric_symbols): 음식점 · 주민 · 1인 세대 · 도로 화물 · 지역내총생산 · 상권.
    //  - 보기만 한다. 영역 · 주문 · 돈 규칙과 상관없다. 「끄기」로 예전 서울 지도로 돌아간다.
    public sealed partial class SagajeongDispatchView
    {
        private const string SeoulComparePreference = "hongdal.seoul.compare.v1";   // -1 끔, 0.. 지표 차례
        private const int SeoulRasterWidth = 720;
        private static readonly Color[] SeoulRamp =
            { Ink(0x1F3B4D), Ink(0x22606B), Ink(0x2F8C7E), Ink(0x7CC08A), Ink(0xF2D06B) };
        private static readonly Rect SeoulSide = new(22f, 226f, 238f, 392f);
        private static readonly Rect SeoulCard = new(990f, 226f, 268f, 376f);
        private static readonly Rect SeoulStage = new(272f, 222f, 706f, 428f);    // 두 창 사이, 위 띠 아래 — 서울 전체가 들어가게 맞춘다

        [Serializable] private sealed class SeoulRing { public float[] p; }
        [Serializable] private sealed class SeoulGu { public string code; public string name; public float cx; public float cy; public SeoulRing[] rings; }
        [Serializable] private sealed class SeoulMetric
        {
            public string id; public string label; public string @short; public string unit; public string period; public string icon;
            public string meaning; public string source; public string url; public string rights;
            public float[] values; public string[] states; public float seoul; public string seoulState;
        }
        [Serializable] private sealed class SeoulGuPack
        {
            public string title; public string boundaryAsOf; public string note; public float west; public float south; public float east; public float north;
            public SeoulGu[] gus; public SeoulMetric[] metrics;
        }

        private SeoulGuPack seoulPack;
        private bool seoulTried, seoulPrefRead;
        private byte[] seoulIndex;                 // 칸마다 구 차례(255 = 서울 밖), 위 줄부터
        private int seoulRasterHeight;
        private Texture2D seoulTexture;
        private Color32[] seoulPixels;
        private int seoulMetric = 0;               // -1 = 끔
        private int seoulSelected = -1, seoulHover = -1;
        private int seoulPaintedMetric = -2, seoulPaintedSelected = -2, seoulPaintedHover = -2;
        private int[] seoulRank;                   // 지표 값의 차례(큰 값이 0)

        private bool SeoulCompareOn => mapLevel == MapLevel.Seoul && seoulPack?.gus != null && seoulMetric >= 0;
        private bool SeoulCompareCovers(Vector2 point) =>
            mapLevel == MapLevel.Seoul && seoulPack?.gus != null &&
            (SeoulSide.Contains(point) && seoulMetric >= 0 || !SeoulCompareOn && new Rect(SeoulSide.x, SeoulSide.y, 150f, 30f).Contains(point) ||
             SeoulCompareOn && seoulSelected >= 0 && SeoulCard.Contains(point));

        private SeoulGuPack EnsureSeoulGu()
        {
            if (seoulTried) return seoulPack;
            seoulTried = true;
            seoulPack = LoadPack<SeoulGuPack>("hongdal-data-seoul-gu");
            if (seoulPack?.gus == null || seoulPack.metrics == null || seoulPack.metrics.Length == 0) { seoulPack = null; return null; }
            RasterizeSeoul();
            for (var i = 0; i < seoulPack.gus.Length; i++) if (seoulPack.gus[i].code == "11070") seoulSelected = i;   // 처음엔 중랑구
            Debug.Log($"Hongdal data: 서울 25구 비교 {seoulPack.gus.Length}구 · 지표 {seoulPack.metrics.Length}");
            return seoulPack;
        }

        // 검증 · 촬영 실행(--sagajeong-… · --hongdal-view-proof=)은 설정을 읽지도 쓰지도 않는다(처음 지표로 찍는다).
        private bool? seoulPrefsAllowed;
        private bool SeoulPrefsAllowed => seoulPrefsAllowed ??= Array.TrueForAll(Environment.GetCommandLineArgs(),
            value => !value.StartsWith("--sagajeong-", StringComparison.Ordinal) && !value.StartsWith(ViewProofFlag, StringComparison.Ordinal));

        private void ReadSeoulPreference()
        {
            if (seoulPrefRead) return;
            seoulPrefRead = true;
            if (!SeoulPrefsAllowed) return;
            seoulMetric = Mathf.Clamp(PlayerPrefs.GetInt(SeoulComparePreference, 0), -1, (seoulPack?.metrics?.Length ?? 1) - 1);
        }

        private void SaveSeoulPreference()
        {
            if (!SeoulPrefsAllowed) return;
            PlayerPrefs.SetInt(SeoulComparePreference, seoulMetric);
            PlayerPrefs.Save();
        }

        // 경위도 상자를 미터 비율대로 칸으로 나누고, 구마다 가로줄을 긋어 안쪽을 채운다(짝홀 규칙).
        private void RasterizeSeoul()
        {
            var pack = seoulPack;
            var widthM = (pack.east - pack.west) * 88224f;
            var heightM = (pack.north - pack.south) * 111320f;
            seoulRasterHeight = Mathf.Max(8, Mathf.RoundToInt(SeoulRasterWidth * heightM / widthM));
            var w = SeoulRasterWidth;
            var h = seoulRasterHeight;
            seoulIndex = new byte[w * h];
            for (var i = 0; i < seoulIndex.Length; i++) seoulIndex[i] = 255;
            var crossings = new List<float>();
            for (var g = 0; g < pack.gus.Length && g < 255; g++)
            {
                var rings = pack.gus[g].rings;
                if (rings == null) continue;
                for (var row = 0; row < h; row++)
                {
                    var lat = pack.north - (row + 0.5f) / h * (pack.north - pack.south);
                    crossings.Clear();
                    foreach (var ring in rings)
                    {
                        var p = ring.p;
                        if (p == null || p.Length < 6) continue;
                        var count = p.Length / 2;
                        for (var k = 0; k < count; k++)
                        {
                            var a = k * 2;
                            var b = (k + 1) % count * 2;
                            float y0 = p[a + 1], y1 = p[b + 1];
                            if (y0 <= lat == y1 <= lat) continue;
                            var x = p[a] + (lat - y0) / (y1 - y0) * (p[b] - p[a]);
                            crossings.Add((x - pack.west) / (pack.east - pack.west) * w);
                        }
                    }
                    if (crossings.Count < 2) continue;
                    crossings.Sort();
                    for (var c = 0; c + 1 < crossings.Count; c += 2)
                    {
                        var from = Mathf.Clamp(Mathf.CeilToInt(crossings[c] - 0.5f), 0, w);
                        var to = Mathf.Clamp(Mathf.FloorToInt(crossings[c + 1] - 0.5f), -1, w - 1);
                        for (var col = from; col <= to; col++) seoulIndex[row * w + col] = (byte)g;
                    }
                }
            }
        }

        private int SeoulClass(int gu)
        {
            if (seoulRank == null || gu < 0 || gu >= seoulRank.Length || seoulRank[gu] < 0) return -1;
            var valid = 0;
            foreach (var r in seoulRank) if (r >= 0) valid++;
            return Mathf.Clamp(4 - seoulRank[gu] * 5 / Mathf.Max(1, valid), 0, 4);
        }

        private void RankSeoul(SeoulMetric metric)
        {
            var count = seoulPack.gus.Length;
            seoulRank = new int[count];
            var order = new List<int>();
            for (var i = 0; i < count; i++)
                if (metric.values != null && i < metric.values.Length && metric.values[i] >= 0f) order.Add(i);
                else seoulRank[i] = -1;
            order.Sort((a, b) => metric.values[b].CompareTo(metric.values[a]));
            for (var i = 0; i < order.Count; i++) seoulRank[order[i]] = i;
        }

        private void PaintSeoulTexture()
        {
            if (seoulPaintedMetric == seoulMetric && seoulPaintedSelected == seoulSelected && seoulPaintedHover == seoulHover && seoulTexture != null) return;
            var w = SeoulRasterWidth;
            var h = seoulRasterHeight;
            if (seoulTexture == null)
            {
                seoulTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                seoulPixels = new Color32[w * h];
            }
            var metric = seoulPack.metrics[seoulMetric];
            if (seoulPaintedMetric != seoulMetric) RankSeoul(metric);
            var fills = new Color32[seoulPack.gus.Length];
            for (var g = 0; g < fills.Length; g++)
            {
                var cls = SeoulClass(g);
                var colour = cls < 0 ? Ink(0x3A4652) : SeoulRamp[cls];
                if (g == seoulHover) colour = Color.Lerp(colour, Color.white, 0.18f);
                colour.a = 0.86f;
                fills[g] = colour;
            }
            Color32 border = Ink(0x0C141B), clear = new Color32(0, 0, 0, 0), gold = Gold;
            for (var row = 0; row < h; row++)
            {
                var texRow = h - 1 - row;                     // 텍스처는 아래 줄부터
                for (var col = 0; col < w; col++)
                {
                    var g = seoulIndex[row * w + col];
                    Color32 c;
                    if (g == 255) c = clear;
                    else
                    {
                        var right = col + 1 < w ? seoulIndex[row * w + col + 1] : (byte)255;
                        var down = row + 1 < h ? seoulIndex[(row + 1) * w + col] : (byte)255;
                        var left = col > 0 ? seoulIndex[row * w + col - 1] : (byte)255;
                        var up = row > 0 ? seoulIndex[(row - 1) * w + col] : (byte)255;
                        var edge = right != g || down != g || left != g || up != g;
                        if (edge) c = g == seoulSelected ? gold : border;
                        else
                        {
                            c = fills[g];
                            if (SeoulClass(g) < 0 && (col + row) % 9 < 2) c = Ink(0x56636E);        // 자료 미확인 빗금
                        }
                    }
                    seoulPixels[texRow * w + col] = c;
                }
            }
            seoulTexture.SetPixels32(seoulPixels);
            seoulTexture.Apply(false);
            seoulPaintedMetric = seoulMetric;
            seoulPaintedSelected = seoulSelected;
            seoulPaintedHover = seoulHover;
        }

        private static string SeoulFormat(float value, string unit)
        {
            if (value < 0f) return "자료 미확인";
            if (unit == "백만원")
                return value >= 1_000_000f ? $"{value / 1_000_000f:0.0}조원" : value >= 100f ? $"{value / 100f:0.0}억원" : $"{value:0.#}백만원";
            return $"{value:N0}{unit}";
        }

        // 서울 경계 상자를 SeoulStage 안에 미터 비율대로 맞춘 자리.
        private Rect SeoulArea()
        {
            var pack = seoulPack;
            var widthM = (pack.east - pack.west) * 88224f;
            var heightM = (pack.north - pack.south) * 111320f;
            var scale = Mathf.Min(SeoulStage.width / widthM, SeoulStage.height / heightM);
            var size = new Vector2(widthM * scale, heightM * scale);
            return new Rect(SeoulStage.center - size / 2f, size);
        }

        private Vector2 SeoulPoint(Rect area, float lon, float lat) => new(
            area.x + (lon - seoulPack.west) / (seoulPack.east - seoulPack.west) * area.width,
            area.y + (seoulPack.north - lat) / (seoulPack.north - seoulPack.south) * area.height);

        // DrawMap(서울 단계)에서 부른다(SagajeongDispatchMap.cs). 비교 판을 그렸으면 true — 그때는 예전 서울 그림과 표식을 덮는다.
        private bool DrawSeoulCompare()
        {
            if (EnsureSeoulGu() == null) return false;
            ReadSeoulPreference();
            var current = Event.current;
            var covered = MapOverlayCovers(current.mousePosition + Field.position);
            if (seoulMetric < 0)
            {
                var open = new Rect(SeoulSide.x, SeoulSide.y, 150f, 28f);
                Paint(open, Fade(Panel, 0.95f), 8);
                DrawMapIcon(new Rect(open.x + 10f, open.y + 7f, 14f, 14f), "local-business", Mint);
                Text(new Rect(open.x + 30f, open.y, open.width - 34f, open.height), "구 비교 켜기", 11, Mint, true);
                if (Hit(open)) { seoulMetric = 0; SaveSeoulPreference(); }
                return false;
            }
            var pack = seoulPack;
            var area = SeoulArea();
            Paint(new Rect(0f, 0f, Field.width, Field.height), Canvas);           // 예전 서울 개략 그림을 덮는다

            // 가리킨 구
            var hover = -1;
            if (!covered && area.Contains(current.mousePosition) && !SeoulSide.Contains(current.mousePosition) &&
                !(seoulSelected >= 0 && SeoulCard.Contains(current.mousePosition)))
            {
                var col = Mathf.FloorToInt((current.mousePosition.x - area.x) / area.width * SeoulRasterWidth);
                var row = Mathf.FloorToInt((current.mousePosition.y - area.y) / area.height * seoulRasterHeight);
                if (col >= 0 && col < SeoulRasterWidth && row >= 0 && row < seoulRasterHeight)
                {
                    var g = seoulIndex[row * SeoulRasterWidth + col];
                    if (g != 255) hover = g;
                }
            }
            if (current.type == EventType.Repaint) seoulHover = hover;
            if (current.type == EventType.MouseDown && current.button == 0 && hover >= 0)
            {
                seoulSelected = hover;
                current.Use();
            }

            if (current.type == EventType.Repaint)
            {
                PaintSeoulTexture();
                GUI.DrawTexture(area, seoulTexture, ScaleMode.StretchToFill, true);
            }

            var metric = pack.metrics[seoulMetric];
            // 구 이름 · 값
            for (var g = 0; g < pack.gus.Length; g++)
            {
                var gu = pack.gus[g];
                var at = SeoulPoint(area, gu.cx, gu.cy);
                var strong = g == seoulSelected || g == seoulHover;
                var bright = SeoulClass(g) >= 3;                                 // 밝은 칸엔 어두운 글자
                var ink = bright ? Ink(0x0E1A24) : Paper;
                Text(new Rect(at.x - 44f, at.y - 13f, 88f, 14f), gu.name, strong ? 11 : 10, strong ? ink : Fade(ink, 0.88f), strong, TextAnchor.MiddleCenter);
                var value = metric.values != null && g < metric.values.Length ? metric.values[g] : -1f;
                Text(new Rect(at.x - 50f, at.y + 1f, 100f, 12f), SeoulFormat(value, metric.unit), 9,
                    value < 0f ? Soft : bright ? Fade(Ink(0x0E1A24), 0.8f) : Fade(Gold, 0.95f), false, TextAnchor.MiddleCenter);
            }

            DrawSeoulSide(pack);
            if (seoulSelected >= 0) DrawSeoulCard(pack);
            return true;
        }

        private void DrawSeoulSide(SeoulGuPack pack)
        {
            var side = SeoulSide;
            Paint(side, Fade(Panel, 0.96f), 10);
            var x = side.x + 12f;
            var y = side.y + 10f;
            Text(new Rect(x, y, 150f, 18f), "서울 25구 비교", 13, Paper, true);
            if (ExpansionButton(new Rect(side.xMax - 52f, y - 1f, 42f, 20f), "끄기", true, Tile))
            {
                seoulMetric = -1;
                SaveSeoulPreference();
                return;
            }
            Text(new Rect(x, y + 18f, side.width - 24f, 14f), "공표 통계 · 구마다 같은 기준 · 누르면 그 구 카드", 9, Soft);
            y += 38f;
            for (var i = 0; i < pack.metrics.Length; i++)
            {
                var m = pack.metrics[i];
                var row = new Rect(side.x + 6f, y, side.width - 12f, 22f);
                var on = i == seoulMetric;
                if (on) Paint(row, Fade(Mint, 0.16f), 6);
                DrawMapIcon(new Rect(row.x + 6f, row.y + 4f, 14f, 14f), m.icon, on ? Mint : Soft);
                Text(new Rect(row.x + 26f, row.y, 140f, row.height), m.@short, 11, on ? Paper : Fade(Paper, 0.8f), on);
                Text(new Rect(row.x + 150f, row.y, row.width - 156f, row.height), m.period, 9, Soft, false, TextAnchor.MiddleRight);
                if (Hit(row) && !on)
                {
                    seoulMetric = i;
                    SaveSeoulPreference();
                }
                y += 23f;
            }
            // 범례: 25구 안의 차례 5단
            var metric = pack.metrics[seoulMetric];
            y += 6f;
            Text(new Rect(x, y, side.width - 24f, 14f), $"{metric.label} · {metric.unit} · {metric.period}", 9, Gold, true);
            y += 16f;
            var bandW = (side.width - 24f) / 5f;
            for (var c = 0; c < 5; c++) Paint(new Rect(x + c * bandW, y, bandW - 2f, 9f), SeoulRamp[c], 2);
            Text(new Rect(x, y + 10f, 80f, 13f), "적은 쪽", 9, Soft);
            Text(new Rect(x + bandW * 3f, y + 10f, bandW * 2f - 2f, 13f), "많은 쪽(5구씩)", 9, Soft, false, TextAnchor.MiddleRight);
            Paint(new Rect(x, y + 26f, 9f, 9f), Ink(0x3A4652), 2);
            Text(new Rect(x + 14f, y + 23f, side.width - 40f, 14f), "빗금 = 자료 미확인(0 아님)", 9, Soft);
            // 이 판의 축척(위 띠의 축척 자는 예전 서울 그림의 것이다)
            var area = SeoulArea();
            var metresPerPoint = (pack.east - pack.west) * 88224f / area.width;
            var bar = 5000f / metresPerPoint;
            Paint(new Rect(x, side.yMax - 50f, bar, 2f), Paper, 0);
            Paint(new Rect(x, side.yMax - 54f, 1.5f, 6f), Paper, 0);
            Paint(new Rect(x + bar - 1.5f, side.yMax - 54f, 1.5f, 6f), Paper, 0);
            Text(new Rect(x + bar + 6f, side.yMax - 58f, 120f, 14f), "5 km · 이 판의 축척", 9, Soft);
            Text(new Rect(x, side.yMax - 38f, side.width - 24f, 30f), $"{metric.source} · {pack.boundaryAsOf} · 내부 검토용", 8, Soft, false, TextAnchor.UpperLeft, true);
        }

        private void DrawSeoulCard(SeoulGuPack pack)
        {
            var card = SeoulCard;
            var gu = pack.gus[seoulSelected];
            var metric = pack.metrics[seoulMetric];
            Paint(card, Fade(Panel, 0.97f), 10);
            var x = card.x + 14f;
            var y = card.y + 10f;
            var wide = card.width - 28f;
            Text(new Rect(x, y, wide, 14f), "구 · 공표 통계 카드", 10, Gold, true);
            if (ExpansionButton(new Rect(card.xMax - 38f, y - 2f, 26f, 22f), "×", true, Tile)) { seoulSelected = -1; return; }
            Text(new Rect(x, y + 16f, wide, 26f), gu.name, 20, Paper, true);
            var value = metric.values[seoulSelected];
            var rank = seoulRank != null && seoulSelected < seoulRank.Length ? seoulRank[seoulSelected] : -1;
            var box = new Rect(x, y + 48f, wide, 52f);
            Paint(box, Tile, 6);
            DrawMapIcon(new Rect(box.x + 10f, box.y + 9f, 16f, 16f), metric.icon, Mint);
            Text(new Rect(box.x + 32f, box.y + 6f, box.width - 40f, 16f), metric.label, 10, Soft);
            Text(new Rect(box.x + 10f, box.y + 26f, box.width - 20f, 22f), SeoulFormat(value, metric.unit), 17, value < 0f ? Soft : Paper, true);
            Text(new Rect(box.x + 10f, box.y + 26f, box.width - 20f, 22f), rank >= 0 ? $"25구 중 {rank + 1}번째로 많음" : "차례 없음", 10, Gold, false, TextAnchor.MiddleRight);
            Text(new Rect(x, y + 104f, wide, 14f), $"서울 전체 {SeoulFormat(metric.seoul, metric.unit)} · {metric.states[seoulSelected]} · {metric.period}", 9, Soft);
            y += 126f;
            Text(new Rect(x, y, wide, 14f), "이 구의 다른 지표", 10, Gold, true);
            y += 17f;
            for (var i = 0; i < pack.metrics.Length; i++)
            {
                if (i == seoulMetric) continue;
                var m = pack.metrics[i];
                var v = m.values != null && seoulSelected < m.values.Length ? m.values[seoulSelected] : -1f;
                DrawMapIcon(new Rect(x, y + 2f, 12f, 12f), m.icon, Soft);
                Text(new Rect(x + 17f, y, 130f, 16f), m.@short, 10, Fade(Paper, 0.85f));
                Text(new Rect(x + 120f, y, wide - 120f, 16f), SeoulFormat(v, m.unit), 10, v < 0f ? Soft : Paper, false, TextAnchor.MiddleRight);
                y += 17f;
            }
            Text(new Rect(x, card.yMax - 44f, wide, 40f), metric.meaning + " · 지표마다 기간이 다릅니다.", 8, Soft, false, TextAnchor.UpperLeft, true);
        }
    }
}
