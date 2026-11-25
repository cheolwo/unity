using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 역으로 내려가기 (D6 · 2026-11-23, 사용자 「계속 진행」). 역 창의 「역 속」 단추 → 지도 가운데에 역의 단면.
    //  - 서울교통공사 역 277곳: 승강장 깊이(지표에서) · 승강장 길이 · 섬식/상대식 · 층 수 · 역사 · 대합실 · 승강장 면적.
    //  - 면목 · 사가정 · 용마산: 안내도의 층 용도 · 번호 출입구(폭 · 캐노피) · 공표 시간표(2025-09). 열차가 시간표대로 들고 난다.
    //  - 대합실 층의 깊이는 고르게 나눈 그림이다(승강장 깊이만 실측). 출입구의 지도 위 자리는 아직 확인 전이라 땅 위에 고르게 늘어놓는다.
    //  - 표현뿐이다.
    public sealed partial class SagajeongDispatchView
    {
        [Serializable] private sealed class InsidePack { public InsideStation[] stations; public InsideDetail[] detail; }
        [Serializable] private sealed class InsideStation { public string n; public string l; public string level; public string style; public float rail; public float depth; public float length; public float area; public float concourse; public float platformArea; public int year; }
        [Serializable] private sealed class InsideDetail { public string code; public string n; public InsideFloor[] floors; public InsideExit[] exits; public InsideTrains[] trains; }
        [Serializable] private sealed class InsideFloor { public string f; public string use; }
        [Serializable] private sealed class InsideExit { public int no; public string f; public float width; public string canopy; public string state; }
        [Serializable] private sealed class InsideTrains { public string week; public string dir; public string toward; public int[] dep; public int[] arr; }

        private InsidePack insidePack;
        private bool insideTried;
        private bool stationInsideOpen;
        private const int InsideProofSeconds = 8 * 3600 + 22 * 60 + 10;      // 촬영: 10-15(목) 08:22:10 — 사가정역에 두 방향 열차가 다 서 있다(공표 시간표)

        private InsidePack EnsureInside()
        {
            if (insideTried) return insidePack;
            insideTried = true;
            insidePack = LoadPack<InsidePack>("hongdal-data-station-inside");
            Debug.Log($"Hongdal data: 역 속 {insidePack?.stations?.Length ?? 0}역");
            return insidePack;
        }

        private InsideStation InsideOf(SubwayStation station, out InsideDetail detail)
        {
            detail = null;
            var pack = EnsureInside();
            if (pack?.stations == null || station?.name == null) return null;
            var label = station.name.EndsWith("역") ? station.name.Substring(0, station.name.Length - 1) : station.name;
            InsideStation found = null;
            foreach (var row in pack.stations)
            {
                if (row?.n != label) continue;
                if (found == null) found = row;
                if (station.lines != null && Array.IndexOf(station.lines, row.l) >= 0) { found = row; break; }
            }
            if (pack.detail != null)
                foreach (var row in pack.detail)
                    if (row?.n == label && found != null && found.l == "7호선") detail = row;
            return found;
        }

        private Rect StationInsideRect => new(352f - Field.x, 216f - Field.y, 624f, 372f);

        private bool DrawStationInside()
        {
            if (PortraitScreen)      // 세로 화면: 역 속 창(624 너비)은 아직 넓은 화면에서만
            {
                if (stationInsideOpen) { stationInsideOpen = false; GuSay("역 속 창은 넓은 화면(가로 · 데스크탑)에서 열립니다"); }
                return false;
            }
            var station = GuStationNow();
            if (!stationInsideOpen || station == null || dataPanelOpen) return false;
            var panel = StationInsideRect;
            var current = Event.current;
            var over = panel.Contains(current.mousePosition);
            Paint(new Rect(panel.x - 1.5f, panel.y - 1.5f, panel.width + 3f, panel.height + 3f), Fade(Ink(0x6FA8FF), 0.7f), 13);
            Paint(panel, Fade(Panel, 0.97f), 12);
            var inside = InsideOf(station, out var detail);
            var x = panel.x + 16f;
            var y = panel.y + 12f;
            if (ExpansionButton(new Rect(panel.xMax - 40f, panel.y + 10f, 28f, 24f), "×", true, Tile)) { stationInsideOpen = false; return true; }
            if (inside == null)
            {
                Text(new Rect(x, y, panel.width - 70f, 22f), station.name + " 속", 17, Paper, true);
                Text(new Rect(x, y + 34f, panel.width - 40f, 40f), "역 속 자료 없음 · 서울교통공사(1~8호선) 역만 깊이 · 길이 · 면적을 모았다", 11, Soft, wrap: true);
                return InsideSwallow(over, current);
            }
            Text(new Rect(x, y, panel.width - 70f, 22f), $"{station.name} 속 · {inside.l} · {inside.style} 승강장" + (inside.year > 0 ? $" · {inside.year}년" : ""), 17, Paper, true);

            // 왼쪽: 숫자
            var info = x;
            var row = y + 32f;
            InsideFigure(info, ref row, "승강장 깊이(지표에서)", inside.depth > 0 ? $"{inside.depth:0.##} m" : "—");
            InsideFigure(info, ref row, "승강장 길이", inside.length > 0 ? $"{inside.length:0} m" : "—");
            InsideFigure(info, ref row, "층", string.IsNullOrEmpty(inside.level) ? "—" : $"지하 {inside.level.TrimStart('B')}층까지");
            InsideFigure(info, ref row, "역사 · 대합실 · 승강장", inside.area > 0 ? $"{inside.area:N0} · {inside.concourse:N0} · {inside.platformArea:N0} ㎡" : "—");
            row += 6f;
            if (detail?.exits != null && detail.exits.Length > 0)
            {
                Text(new Rect(info, row, 190f, 14f), $"번호 출입구 {detail.exits.Length}곳", 10, Gold, true);
                row += 16f;
                foreach (var exit in detail.exits)
                {
                    Text(new Rect(info, row, 196f, 14f), $"{exit.no}번 · {exit.f} · 폭 {exit.width:0.##} m · {exit.canopy} 캐노피", 10, Paper);
                    row += 15f;
                }
                Text(new Rect(info, row + 2f, 196f, 26f), "출입구의 지도 위 자리는 확인 전이다", 9, Soft, wrap: true);
            }
            else Text(new Rect(info, row, 196f, 40f), "층 용도 · 출입구 · 시간표는 면목 · 사가정 · 용마산만 모았다", 10, Soft, wrap: true);

            // 오른쪽: 단면
            var sx = panel.x + 228f;
            var sw = panel.xMax - 16f - sx;
            var ground = panel.y + 74f;
            var bottom = panel.yMax - 76f;
            var levels = 1;
            if (!string.IsNullOrEmpty(inside.level) && int.TryParse(inside.level.TrimStart('B'), out var parsed)) levels = Mathf.Max(1, parsed);
            var depth = Mathf.Max(5f, inside.depth);
            float DepthY(float metres) => ground + (bottom - ground) * Mathf.Clamp01(metres / depth);
            Paint(new Rect(sx, panel.y + 40f, sw, ground - panel.y - 40f), Fade(Ink(0x223545), 0.6f), 4);          // 하늘
            Paint(new Rect(sx, ground, sw, bottom - ground + 34f), Fade(Ink(0x2A2420), 0.9f), 4);                   // 땅
            Line(new Vector2(sx, ground), new Vector2(sx + sw, ground), Fade(Paper, 0.6f), 1.5f);
            Text(new Rect(sx + 4f, ground - 16f, 80f, 14f), "지표", 9, Soft);
            for (var level = 1; level < levels; level++)
            {
                var top = DepthY(depth * level / levels) - 8f;
                var band = new Rect(sx + sw * 0.18f, top, sw * 0.64f, 16f);
                Paint(band, Fade(Ink(0x3A4B59), 0.95f), 3);
                var use = detail?.floors != null && level - 1 < detail.floors.Length ? detail.floors[level - 1].use : "대합실";
                Text(band, $"B{level} {use}", 10, Paper, false, TextAnchor.MiddleCenter);
            }
            // 승강장
            var platformY = DepthY(depth);
            var deck = new Rect(sx + 8f, platformY - 20f, sw - 16f, 40f);
            Paint(deck, Fade(Ink(0x47596A), 0.95f), 3);
            var island = inside.style == "섬식";
            var trackA = island ? deck.y + 6f : deck.y + 14f;
            var trackB = island ? deck.yMax - 6f : deck.yMax - 14f;
            if (island) Paint(new Rect(deck.x + 30f, deck.y + 13f, deck.width - 60f, 14f), Fade(Paper, 0.25f), 2);
            else { Paint(new Rect(deck.x + 30f, deck.y, deck.width - 60f, 5f), Fade(Paper, 0.25f), 2); Paint(new Rect(deck.x + 30f, deck.yMax - 5f, deck.width - 60f, 5f), Fade(Paper, 0.25f), 2); }
            Line(new Vector2(deck.x, trackA), new Vector2(deck.xMax, trackA), Fade(Soft, 0.7f), 1.2f);
            Line(new Vector2(deck.x, trackB), new Vector2(deck.xMax, trackB), Fade(Soft, 0.7f), 1.2f);
            Text(new Rect(deck.x, deck.yMax + 2f, deck.width, 14f), $"B{levels} 승강장 · {inside.length:0} m · 지표에서 {inside.depth:0.##} m", 10, Paper, true, TextAnchor.MiddleCenter);
            // 깊이 자
            Line(new Vector2(sx + sw - 6f, ground), new Vector2(sx + sw - 6f, platformY), Fade(Gold, 0.7f), 1.2f, true);
            // 출입구: 땅 위에 고르게
            if (detail?.exits != null && detail.exits.Length > 0)
            {
                var b1 = levels > 1 ? DepthY(depth / levels) : platformY;
                for (var i = 0; i < detail.exits.Length; i++)
                {
                    var ex = sx + sw * (0.12f + 0.76f * (i + 0.5f) / detail.exits.Length);
                    Paint(new Rect(ex - 9f, ground - 10f, 18f, 10f), Ink(0x6FE3C4), 3);
                    Text(new Rect(ex - 9f, ground - 10f, 18f, 10f), detail.exits[i].no.ToString(), 8, Canvas, true, TextAnchor.MiddleCenter);
                    Line(new Vector2(ex, ground), new Vector2(ex, b1 - 8f), Fade(Ink(0x6FE3C4), 0.5f), 1.2f, true);
                }
            }
            // 열차
            var note = "시간표 자료 없음";
            if (detail?.trains != null)
            {
                var week = GameDate().DayOfWeek switch { DayOfWeek.Saturday => "SAT", DayOfWeek.Sunday => "END", _ => "DAY" };
                var now = GameSeconds();
                if (now < 3 * 3600) now += 86400;                     // 시간표는 운행일 기준(자정 넘은 열차)
                var notes = new List<string>();
                foreach (var trains in detail.trains)
                {
                    if (trains?.week != week || trains.dep == null || trains.arr == null) continue;
                    var up = trains.dir == "UP";
                    var trackY = up ? trackA : trackB;
                    var at = TrainPlace(trains, now, out var next);
                    if (at > -2f)
                    {
                        var carLength = (deck.width - 60f) / 7f;
                        var centre = deck.x + deck.width / 2f + (up ? -1f : 1f) * at * deck.width;
                        for (var car = 0; car < 7; car++)
                        {
                            var cx = centre + (car - 3) * carLength;
                            if (cx < deck.x - carLength || cx > deck.xMax + carLength) continue;
                            Paint(new Rect(cx - carLength / 2f + 1f, trackY - 4f, carLength - 2f, 8f), Ink(0x747F00), 2);
                        }
                    }
                    notes.Add($"{trains.toward} 방면 {(next > 0 ? $"{next % 86400 / 3600:00}:{next % 3600 / 60:00}" : "운행 끝")}");
                }
                note = notes.Count > 0 ? "다음 출발 · " + string.Join(" · ", notes) + (week == "DAY" ? " · 평일" : week == "SAT" ? " · 토요일" : " · 일요일") : note;
            }
            Text(new Rect(sx, panel.yMax - 34f, sw, 14f), note, 10, Paper);
            Text(new Rect(x, panel.yMax - 18f, panel.width - 32f, 12f), "서울교통공사 역사 건축 · 심도 · 면적 · 안내도(2026-08-31) · 시간표(2025-09 공표) · 대합실 층 깊이는 고르게 나눈 그림", 9, Soft);
            return InsideSwallow(over, current);
        }

        // 열차 자리: 0 = 승강장 한가운데에 섬, 양수 = 떠나는 중, 음수 = 들어오는 중, -2 이하 = 안 보임. next = 다음 출발 시각.
        private static float TrainPlace(InsideTrains trains, int now, out int next)
        {
            next = -1;
            for (var i = 0; i < trains.dep.Length; i++)
            {
                var arrive = trains.arr[i] > 0 ? trains.arr[i] : trains.dep[i] - 30;
                var leave = trains.dep[i];
                if (leave >= now && next < 0) next = leave;
                if (now >= arrive && now <= leave) return 0f;
                if (now < arrive && arrive - now <= 40) return -(arrive - now) / 40f;          // 들어온다
                if (now > leave && now - leave <= 40) return (now - leave) / 40f;              // 떠난다
            }
            return -3f;
        }

        private void InsideFigure(float x, ref float row, string label, string value)
        {
            Text(new Rect(x, row, 196f, 13f), label, 9, Soft);
            Text(new Rect(x, row + 12f, 196f, 18f), value, 13, Paper, true);
            row += 33f;
        }

        private static bool InsideSwallow(bool over, Event current)
        {
            if (!over || current.type == EventType.Repaint || current.type == EventType.Layout) return false;
            if (current.type != EventType.MouseDown && current.type != EventType.MouseUp && current.type != EventType.MouseDrag &&
                current.type != EventType.ScrollWheel) return false;
            current.Use();
            return true;
        }

        // ── 생활인구 시간대 지수 ─────────────────────────────────────────────────

        [Serializable] private sealed class LifePack { public LifeBand[] bands; public LifeRow[] rows; }
        [Serializable] private sealed class LifeBand { public string id; public string label; public int from; public int to; }
        [Serializable] private sealed class LifeRow { public string code; public string name; public string group; public string band; public int v; }

        private LifePack lifePack;
        private bool lifeTried;

        // 지금 시각의 시간대와 요일 묶음. 시간대 밖이면 band = null.
        private (LifeBand Band, string Group) LifeNow()
        {
            if (!lifeTried) { lifeTried = true; lifePack = LoadPack<LifePack>("hongdal-data-life-bands"); }
            var group = GameDate().DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "토·일" : "월~금";
            if (lifePack?.bands == null) return (null, group);
            var hour = GameSeconds() / 3600;
            foreach (var band in lifePack.bands) if (hour >= band.from && hour <= band.to) return (band, group);
            return (null, group);
        }

        // 행정동 코드(10자리 또는 8자리)의 지금 지수. 없으면 -1.
        private int LifeIndex(string dongCode)
        {
            var (band, group) = LifeNow();
            if (band == null || lifePack?.rows == null || string.IsNullOrEmpty(dongCode) || dongCode.Length < 8) return -1;
            var code = dongCode.Substring(0, 8);
            foreach (var row in lifePack.rows)
                if (row.code == code && row.group == group && row.band == band.id) return row.v;
            return -1;
        }

        private string LifeLine(IEnumerable<string> codes, IEnumerable<string> names)
        {
            var (band, group) = LifeNow();
            if (lifePack?.rows == null) return null;
            if (band == null) return "생활인구 지수 · 지금은 오전 · 점심 · 저녁 밖의 시간";
            var parts = new List<string>();
            using (var name = names.GetEnumerator())
                foreach (var code in codes)
                {
                    name.MoveNext();
                    var value = LifeIndex(code);
                    if (value >= 0) parts.Add($"{name.Current} {value}");
                }
            if (parts.Count == 0) return "생활인구 지수 · 중랑 16동만";
            return $"생활인구 · {(group == "월~금" ? "평일" : "주말")} {band.label} · " + string.Join(" · ", parts);
        }
    }
}
