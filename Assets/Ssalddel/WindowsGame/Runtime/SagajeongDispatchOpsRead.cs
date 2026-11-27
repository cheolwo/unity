using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 지휘관 입장 · 판독 층 (2026-11-25 사용자 13:29 「동단위 내에서 전투에 대해서 … 지휘관이 생각을 해볼 수 있도록」 ·
    //  13:43 「입장을 보는 카드에 지휘관을 추가를 하고 … 적군이랑 싸우는 식으로 … 데이터들이 불러와져가지고 … 플레이」 ·
    //  13:45 「모바일에서도 동단위 지도를 활용할 수 있도록」).
    //  시안: artifacts/local/hongdal-unity-claude-20261112/demos/작전지도-시안-20261125/ ① 판독 · 기획: docs/AI/Handoffs/2026-11-25-Claude-기획-입장패키지-지휘관.md
    //  - 공개판 동 디오라마(SagajeongDispatchPublicDong.cs)가 열려 있고 입장이 지휘관이면 그 위에 판독 층을 그린다(폰 · PC 같음).
    //  - 셈은 그 동의 메시에서 바로 한다(새 자료 파일 없음 · 공개 자료만):
    //      경계 = 동 판 옆면 아래 끝(y = −14) · 차도 = y 0.06 판(표준노드링크 차로 수 × 3.2 m + 0.8 m) · 지붕 = 건물 위 끝(GIS건물통합정보 높이, 14 m 위는 0.4배로 눌러 그림 → 되돌려 적음).
    //      진입로 = 차도가 경계에 닿는 입(가까운 입끼리 묶음) · 몫 = 입의 폭(차로가 많을수록 많이 온다는 근사) · 길목 = 몫에 비해 좁은 입 ·
    //      고지 = 가장 높은 옥상(서로 160 m 넘게 떨어진 둘) · 사각 = 두 고지에서 가장 먼 진입로.
    //  - 키워드 검색량 · 적 수 · 물결은 게임 값(예시)이라 「예시」로 표시한다. 답은 판독이 주고 결정은 지휘관이 한다.
    //  - 차례 띠(판독 ▸ 배치 ▸ 모의전 ▸ 실전 ▸ 복기) 가운데 지금은 판독만 연다. 배치부터는 옥상 진형 방어 규칙(Codex P1)과 함께 다음 판.
    public sealed partial class SagajeongDispatchView
    {
        private sealed class OpsEntry
        {
            public Vector3 Mouth, Inward;
            public float Width, Share, FromPeaks;
            public string Label;
            public bool Choke, Blind, Edge;
        }

        private sealed class OpsPeak
        {
            public Vector3 Top;
            public float Drawn, Metres;
            public string Label;
        }

        private static readonly Color OpsFire = Ink(0xFF7A6B);
        private static readonly Color OpsPeakTint = Ink(0x6FE3C4);
        private static readonly string[] OpsEntryNames = { "가", "나", "다", "라" };
        private static readonly string[] OpsPeakNames = { "ㄱ", "ㄴ" };
        private static readonly string[] OpsSteps = { "판독", "배치", "모의전", "실전", "복기" };

        private string opsCode;
        private bool opsLayerOn = true;
        private bool opsPanelFull;
        private float opsSay;
        private string opsSayText;
        private readonly List<OpsEntry> opsEntries = new();
        private readonly List<OpsPeak> opsPeaks = new();
        private float opsScatter;
        private int opsMouths, opsTallCells, opsVolume, opsEnemies, opsMalice;
        private string opsKeyword;

        private bool OpsReadActive => PublicDongOpen && stanceKey == "commander";

        private void ResetOpsRead() => opsCode = null;

        // 동이 바뀌면 한 번 센다
        private void PrepareOpsRead()
        {
            if (opsCode == pdongCode) return;
            opsCode = pdongCode;
            opsEntries.Clear();
            opsPeaks.Clear();
            opsScatter = 0f;
            opsMouths = opsTallCells = 0;
            var mesh = pdongMeshes.Count > 0 ? pdongMeshes[0] : null;
            if (mesh == null) return;
            var points = mesh.vertices;

            // 1) 경계 · 차도 · 지붕
            var edge = new Dictionary<long, List<Vector2>>();
            static long Key(float x, float z, float cell) => ((long)Mathf.FloorToInt(x / cell) << 32) ^ (uint)Mathf.FloorToInt(z / cell);
            var roads = new List<Vector2>();
            var tall = new Dictionary<long, Vector3>();
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in points)
            {
                if (p.y < -13f)
                {
                    var key = Key(p.x, p.z, 10f);
                    if (!edge.TryGetValue(key, out var list)) edge[key] = list = new List<Vector2>();
                    list.Add(new Vector2(p.x, p.z));
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                }
                else if (Mathf.Abs(p.y - 0.06f) < 0.004f) roads.Add(new Vector2(p.x, p.z));
                else if (p.y > 4f)
                {
                    var key = Key(p.x, p.z, 25f);
                    if (!tall.TryGetValue(key, out var best) || p.y > best.y) tall[key] = p;
                }
            }
            if (edge.Count == 0) return;
            var centre = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float EdgeDistance(Vector2 v)
            {
                var best = float.MaxValue;
                int cx = Mathf.FloorToInt(v.x / 10f), cz = Mathf.FloorToInt(v.y / 10f);
                for (var dx = -1; dx <= 1; dx++)
                for (var dz = -1; dz <= 1; dz++)
                    if (edge.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out var list))
                        foreach (var e in list) best = Mathf.Min(best, (e - v).sqrMagnitude);
                return Mathf.Sqrt(best);
            }

            // 2) 진입로: 차도가 경계에 닿는 점 → 24 m 안끼리 묶음
            var mouths = roads.Where(v => EdgeDistance(v) < 12f).Distinct().ToList();      // 옆면 테두리는 단순화돼 있어 차도 끝과 몇 m 벌어진다
            opsMouths = mouths.Count;
            var group = new int[mouths.Count];
            for (var i = 0; i < group.Length; i++) group[i] = i;
            int Root(int i) { while (group[i] != i) i = group[i] = group[group[i]]; return i; }
            for (var i = 0; i < mouths.Count; i++)
            for (var j = i + 1; j < mouths.Count; j++)
                if ((mouths[i] - mouths[j]).sqrMagnitude < 24f * 24f) group[Root(i)] = Root(j);
            var clusters = mouths.Select((v, i) => (v, r: Root(i))).GroupBy(item => item.r).Select(g => g.Select(item => item.v).ToList()).ToList();
            var found = new List<OpsEntry>();
            foreach (var c in clusters)
            {
                var extent = 0f;
                foreach (var a in c) foreach (var b in c) extent = Mathf.Max(extent, (a - b).magnitude);
                if (extent < 3f) continue;
                var mid = Vector2.zero;
                foreach (var v in c) mid += v;
                mid /= c.Count;
                var inward = (centre - mid).normalized;
                found.Add(new OpsEntry
                {
                    Mouth = new Vector3(mid.x, 0.5f, mid.y), Inward = new Vector3(inward.x, 0f, inward.y),
                    Width = Mathf.Min(extent, 40f), Edge = extent > 60f,      // 경계를 따라 달리는 큰길(입이 길게 붙음)
                });
            }
            var total = found.Sum(item => item.Width);
            if (total > 0f)
            {
                foreach (var entry in found) entry.Share = entry.Width / total;
                var top = found.OrderByDescending(item => item.Share).Take(4).ToList();
                opsScatter = 1f - top.Sum(item => item.Share);
                for (var i = 0; i < top.Count; i++) top[i].Label = OpsEntryNames[i];
                opsEntries.AddRange(top);
            }
            // 길목: 몫이 10% 넘는 입 가운데 폭에 비해 몫이 가장 큰(좁은데 많이 오는) 곳
            var choke = opsEntries.Where(item => item.Share >= 0.1f && !item.Edge).OrderBy(item => item.Width).FirstOrDefault();
            if (choke != null) choke.Choke = true;

            // 3) 고지: 가장 높은 옥상 둘(서로 160 m 넘게)
            foreach (var cell in tall.Values.OrderByDescending(v => v.y))
            {
                if (opsPeaks.Any(peak => (new Vector2(peak.Top.x - cell.x, peak.Top.z - cell.z)).magnitude < 160f)) continue;
                if (EdgeDistance(new Vector2(cell.x, cell.z)) < 8f) continue;
                var drawn = cell.y;
                var metres = drawn <= 14f ? drawn : drawn >= 35.9f ? 69f : 14f + (drawn - 14f) / 0.4f;      // 굽기의 눌러 그리기를 되돌림
                opsPeaks.Add(new OpsPeak { Top = cell, Drawn = drawn, Metres = metres, Label = OpsPeakNames[opsPeaks.Count] });
                if (opsPeaks.Count >= OpsPeakNames.Length) break;
            }
            opsTallCells = tall.Values.Count(v => v.y >= 10f);
            // 4) 사각: 두 고지에서 가장 먼 진입로
            foreach (var entry in opsEntries)
                entry.FromPeaks = opsPeaks.Count == 0 ? 0f : opsPeaks.Min(peak => (new Vector2(peak.Top.x - entry.Mouth.x, peak.Top.z - entry.Mouth.z)).magnitude);
            var blind = opsEntries.OrderByDescending(item => item.FromPeaks).FirstOrDefault();
            if (blind != null && blind.FromPeaks > 250f) blind.Blind = true;

            // 5) 예시(게임 값): 키워드 검색량 → 오는 적
            opsKeyword = pdongCode == "1126057500" ? "사가정역 치킨" : (pdongName ?? "이 동") + " 치킨";
            opsVolume = Mathf.RoundToInt((1200f + pdongBuildings * 1.8f) / 100f) * 100;
            opsEnemies = Mathf.Max(10, opsVolume / 50);
            opsMalice = Mathf.Max(1, Mathf.RoundToInt(opsEnemies * 0.07f));
            Debug.Log($"Hongdal ops read {pdongCode} {pdongName}: mouths {opsMouths}, entries {found.Count} (top {opsEntries.Count}), peaks {opsPeaks.Count}, tall cells {opsTallCells}");
        }

        private bool OpsToLogical(Vector3 world, out Vector2 logical)
        {
            logical = default;
            var camera = dioramaCamera != null ? dioramaCamera : Camera.main;
            if (camera == null) return false;
            var s = camera.WorldToScreenPoint(world);
            if (s.z <= 0f) return false;
            logical = new Vector2(s.x / pdGuiScale, (Screen.height - s.y) / pdGuiScale);
            return true;
        }

        private void OpsLabel(Vector2 at, string text, Color tint, float width)
        {
            var box = new Rect(at.x, at.y - 11f, width, 22f);
            Paint(box, Fade(Canvas, 0.9f), 7);
            Paint(new Rect(box.x, box.y + 4f, 3f, box.height - 8f), tint, 1.5f);
            Text(new Rect(box.x + 8f, box.y, box.width - 10f, box.height), text, 11, Paper, true);
        }

        // 디오라마 위(띠 · 판보다 먼저): 화살표 · 길목 · 고지
        private void DrawOpsWorld()
        {
            if (!OpsReadActive) return;
            PrepareOpsRead();
            if (!opsLayerOn) return;
            var reach = Mathf.Min(170f, pdongExtent * 0.45f);
            foreach (var entry in opsEntries)
            {
                if (!OpsToLogical(entry.Mouth - entry.Inward * 45f, out var a) || !OpsToLogical(entry.Mouth + entry.Inward * reach, out var b)) continue;
                var thick = 4f + entry.Share * 30f;
                var tint = entry.Blind ? Fade(OpsFire, 0.55f) : Fade(OpsFire, 0.8f);
                Line(a, b, Fade(Canvas, 0.55f), thick + 4f);
                Line(a, b, tint, thick);
                var dir = (b - a).normalized;
                var side = new Vector2(-dir.y, dir.x);
                var head = 10f + thick * 0.8f;
                Line(b, b - dir * head + side * head * 0.7f, OpsFire, Mathf.Max(3f, thick * 0.45f));
                Line(b, b - dir * head - side * head * 0.7f, OpsFire, Mathf.Max(3f, thick * 0.45f));
                OpsLabel(a + new Vector2(8f, -16f), $"진입로 {entry.Label} · {entry.Share * 100f:0}%" + (entry.Edge ? " · 경계 큰길" : string.Empty), OpsFire, entry.Edge ? 168f : 112f);
                if (entry.Choke)
                {
                    var across = Vector3.Cross(entry.Inward, Vector3.up).normalized * Mathf.Max(6f, entry.Width * 0.5f);
                    if (OpsToLogical(entry.Mouth + across, out var c) && OpsToLogical(entry.Mouth - across, out var d))
                    {
                        Line(c, d, Fade(Canvas, 0.6f), 9f);
                        Line(c, d, Gold, 6f);
                        OpsLabel((c + d) * 0.5f + new Vector2(10f, 34f), $"길목 · 폭 약 {entry.Width:0} m · 막기 좋음", Gold, 176f);
                    }
                }
                if (entry.Blind) OpsLabel(b + new Vector2(10f, 10f), $"사각 · 고지에서 {entry.FromPeaks:0} m", Soft, 140f);
            }
            for (var k = 0; k < opsPeaks.Count; k++)
            {
                var peak = opsPeaks[k];
                if (!OpsToLogical(peak.Top + Vector3.up * 2f, out var p)) continue;
                var t1 = p + new Vector2(0f, -16f); var t2 = p + new Vector2(14f, 9f); var t3 = p + new Vector2(-14f, 9f);
                Line(t1, t2, Fade(Canvas, 0.7f), 7f); Line(t2, t3, Fade(Canvas, 0.7f), 7f); Line(t3, t1, Fade(Canvas, 0.7f), 7f);
                Line(t1, t2, OpsPeakTint, 4f); Line(t2, t3, OpsPeakTint, 4f); Line(t3, t1, OpsPeakTint, 4f);
                OpsLabel(p + new Vector2(18f, k == 0 ? 18f : -30f), $"고지 {peak.Label} · 옥상 약 {peak.Metres:0} m" + (peak.Drawn >= 35.9f ? "+" : string.Empty), OpsPeakTint, 150f);
            }
        }

        // 띠 아래: 차례 띠 · 판독 요약(가로 = 오른쪽 판, 세로 = 아래 판, 접고 펼침)
        private void DrawOpsPanel(float width, float height, float safeTop, float footTop)
        {
            if (!OpsReadActive) return;
            PrepareOpsRead();
            var portrait = PortraitScreen;
            // 차례 띠
            var band = new Rect(10f, safeTop + 64f, Mathf.Min(width - 102f, 360f), 30f);      // 오른쪽에 「판독 끄기」 자리(세로 폭 412에서도)
            pdUiRects.Add(band);
            Paint(band, Fade(Canvas, 0.92f), 9);
            var x = band.x + 6f;
            for (var i = 0; i < OpsSteps.Length; i++)
            {
                var w = (band.width - 12f) / OpsSteps.Length;
                var chip = new Rect(x, band.y + 4f, w - 4f, band.height - 8f);
                if (i == 0) Paint(chip, Gold, 7);
                Text(chip, OpsSteps[i], 12, i == 0 ? Canvas : Fade(Soft, 0.75f), true, TextAnchor.MiddleCenter);
                if (i > 0 && Hit(chip)) { opsSay = Time.unscaledTime; opsSayText = $"{OpsSteps[i]} — 옥상 진형 방어 규칙과 함께 다음 판에서 엽니다. 지금은 판독만"; }
                x += w;
            }
            var toggle = new Rect(band.xMax + 6f, band.y, 76f, band.height);
            if (toggle.xMax <= width - 10f)
            {
                pdUiRects.Add(toggle);
                Paint(toggle, opsLayerOn ? Tile : Fade(OpsFire, 0.85f), 9);
                Text(toggle, opsLayerOn ? "판독 끄기" : "판독 켜기", 11, Paper, true, TextAnchor.MiddleCenter);
                if (Hit(toggle)) opsLayerOn = !opsLayerOn;
            }
            if (opsSayText != null && Time.unscaledTime - opsSay < 3.5f)
            {
                var say = new Rect(10f, band.yMax + 6f, Mathf.Min(width - 20f, 460f), 24f);
                Paint(say, Fade(Canvas, 0.92f), 7);
                Text(new Rect(say.x + 10f, say.y, say.width - 14f, say.height), opsSayText, 11, Gold, false);
            }

            // 판독 요약
            var lines = new List<(string Key, string Value, bool Example)>
            {
                ("키워드", $"「{opsKeyword}」", true),
                ("월 검색 → 오는 적", $"{opsVolume:N0} → {opsEnemies:N0} · 악의 {opsMalice}", true),
                ("언제 오나", "물결 셋 · 첫 물결 0:40", true),
                ("어디로 오나(%)", opsEntries.Count == 0 ? "길이 경계에 닿는 곳이 없음" : string.Join(" · ", opsEntries.Select(e => $"{e.Label} {e.Share * 100f:0}")) + (opsScatter > 0.02f ? $" · 흩어짐 {opsScatter * 100f:0}" : string.Empty), false),
                ("어디서 막나", opsEntries.FirstOrDefault(e => e.Choke) is { } c ? $"진입로 {c.Label} · 폭 약 {c.Width:0} m" : "뚜렷한 길목 없음", false),
                ("어디서 내려다보나", opsPeaks.Count == 0 ? "높은 옥상 없음" : string.Join(" · ", opsPeaks.Select(p => $"{p.Label} {p.Metres:0} m")), false),
                ("못 보는 곳", opsEntries.FirstOrDefault(e => e.Blind) is { } b ? $"진입로 {b.Label} 쪽 · 고지에서 {b.FromPeaks / 1000f:0.0} km" : "고지가 다 덮음", false),
            };
            var rowH = 21f;
            Rect panel;
            if (portrait)
            {
                var full = opsPanelFull;
                var h = full ? 46f + lines.Count * rowH + 36f : 66f;
                panel = new Rect(8f, footTop - h - 8f, width - 16f, h);
            }
            else panel = new Rect(width - 352f, safeTop + 66f, 340f, 46f + lines.Count * rowH + 36f);
            pdUiRects.Add(panel);
            Paint(panel, Fade(OpsFire, 0.7f), 12);
            Paint(new Rect(panel.x + 2f, panel.y + 2f, panel.width - 4f, panel.height - 4f), Fade(Panel, 0.97f), 11);
            Text(new Rect(panel.x + 14f, panel.y + 8f, 160f, 20f), "판독 요약", 14, Paper, true);
            var chipRect = new Rect(panel.x + 92f, panel.y + 10f, 54f, 17f);
            Paint(chipRect, Gold, 8);
            Text(chipRect, "방어 전", 10, Canvas, true, TextAnchor.MiddleCenter);
            if (portrait)
            {
                var more = new Rect(panel.xMax - 84f, panel.y + 6f, 74f, 24f);
                Paint(more, Tile, 8);
                Text(more, opsPanelFull ? "접기 ▾" : "펼치기 ▴", 11, Paper, true, TextAnchor.MiddleCenter);
                if (Hit(more)) opsPanelFull = !opsPanelFull;
                if (!opsPanelFull)
                {
                    Text(new Rect(panel.x + 14f, panel.y + 34f, panel.width - 28f, 22f), $"{lines[3].Value}  ·  {lines[4].Value}", 11, Paper, false);
                    return;
                }
            }
            var y = panel.y + 36f;
            foreach (var (key, value, example) in lines)
            {
                Text(new Rect(panel.x + 14f, y, 112f, rowH), key, 11, Soft);
                Text(new Rect(panel.x + 120f, y, panel.width - 134f - (example ? 32f : 0f), rowH), value, 11, Paper, true, TextAnchor.MiddleRight);
                if (example)
                {
                    var tag = new Rect(panel.xMax - 40f, y + 3f, 28f, rowH - 6f);
                    Paint(tag, Ink(0x3A3020), 5);
                    Text(tag, "예시", 9, Gold, true, TextAnchor.MiddleCenter);
                }
                Paint(new Rect(panel.x + 14f, y + rowH - 1f, panel.width - 28f, 1f), Fade(Rim, 0.6f));
                y += rowH;
            }
            Text(new Rect(panel.x + 14f, y + 4f, panel.width - 28f, 14f), "답은 판독이 주고, 결정은 지휘관이 합니다", 10, Gold, true);
            Text(new Rect(panel.x + 14f, y + 18f, panel.width - 28f, 14f), "길 폭 · 건물 높이 = 공개 자료로 셈(근사) · 예시 = 게임 값", 9, Soft);
        }

        // 입장으로 들어온 뒤 잠깐 뜨는 띠(공개판 디오라마 위)
        private void DrawPublicDongEnterBand(float width, float safeTop)
        {
            if (stanceEnterNote == null || Time.unscaledTime - stanceEnterAt > 4.5f) return;
            var since = Time.unscaledTime - stanceEnterAt;
            var alpha = Mathf.Clamp01((4.5f - since) / 0.8f) * Mathf.Clamp01(since / 0.3f);
            var w = Mathf.Min(width - 20f, 620f);
            var band = new Rect((width - w) * 0.5f, safeTop + (OpsReadActive ? 100f : 64f), w, 28f);
            Paint(band, Fade(Canvas, 0.92f * alpha), 9);
            Paint(new Rect(band.x, band.y, 4f, band.height), Fade(Array.Find(Stances, s => s.Key == stanceKey)?.Tint ?? Mint, alpha), 2);
            Text(band, stanceEnterNote, PortraitScreen ? 10 : 12, Fade(Paper, alpha), true, TextAnchor.MiddleCenter);
        }
    }
}
