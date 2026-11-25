using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 생활 자료 팩과 실제 달력 (D0 · 2026-11-23, 사용자: 「1.0 · 2.0 구분하지 말고 될 수 있는 대로 다 표현」 · 「게임 하루가 실제 하루, 실제 달력으로」).
    //  - 팩: Resources/hongdal-data-*.json. 굽는 도구 artifacts/local/hongdal-data-packs-20261123/bake_packs.py가
    //    Codex가 모은 공개 자료 SQLite에서 만든다. 팩마다 출처 · 기준일 · 단위가 있다. 값을 서로 더해 새 숫자를 만들지 않는다.
    //  - 실제 달력: 운영 1일차 = 이 PC에서 게임을 처음 켠 날(PlayerPrefs). n일차 = 그날 + (n−1)일. 시각은 게임 시계.
    //    그 날짜의 관측이 없으면 가장 가까운 앞선 해의 같은 날을 쓰고 「작년 같은 날」로 적는다. 그것도 없으면 「관측 없음」.
    //  - 촬영은 --hongdal-date=YYYY-MM-DD로 1일차의 날짜를 고정한다(늘 같은 그림).
    //  - 표현뿐이다. 주문 · 돈 · 시계 규칙은 읽지도 바꾸지도 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string CalendarStartPreference = "hongdal.calendar.start";
        private const string CalendarDateFlag = "--hongdal-date=";

        [Serializable] private sealed class CalendarPack { public string schema; public string title; public CalendarSource[] sources; public CalendarDay[] days; }
        [Serializable] private sealed class CalendarSource { public string what; public string source; public string url; public string unit; }
        [Serializable] private sealed class CalendarDay
        {
            public string d;
            public int on, off, rise, set;        // KST 자정부터 지난 초. 0 = 없음
            public float rain, peak;              // 하루 강우 mm(세 구 우량계 평균) · 10분 최대
            public int wet, gauges;               // 젖은 시간 비트(0~23시) · 쓴 우량계 수. gauges 0 = 비 관측 없음
            public float snow, newSnow;           // 적설 · 신적설 cm (서울 관측소 한 점)
            public string snowText;
        }

        [Serializable] private sealed class StationHoursPack { public string title; public string source; public string unit; public string[] months; public StationHoursEntry[] stations; }
        [Serializable] private sealed class StationHoursEntry { public string n; public string l; public StationHoursMonth[] months; }
        [Serializable] private sealed class StationHoursMonth { public string m; public int[] b; public int[] a; }

        // 고른 날의 읽음. 필드마다 어느 날의 관측인지 따로 적는다.
        private sealed class DayRead
        {
            public DateTime Date;
            public CalendarDay Lamp, Rain, Snow;
            public string LampNote, RainNote, SnowNote;
        }

        private CalendarPack calendarPack;
        private StationHoursPack stationHoursPack;
        private bool calendarTried, stationHoursTried;
        private readonly Dictionary<string, CalendarDay> calendarByDate = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<StationHoursEntry>> stationHoursByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (int[] Board, int[] Alight, string Month, string Note, int Lines)> stationHoursCache = new(StringComparer.Ordinal);
        private DateTime? calendarStart;
        private DateTime? proofDate;          // 촬영 장면이 고정하는 날짜 · 시각(그 밖에는 null)
        private int proofSeconds = -1;
        private DayRead dayRead;
        private string dayReadKey;

        private CalendarPack EnsureCalendar()
        {
            if (calendarTried) return calendarPack;
            calendarTried = true;
            calendarPack = LoadPack<CalendarPack>("hongdal-data-calendar");
            if (calendarPack?.days != null)
                foreach (var day in calendarPack.days)
                    if (day?.d != null) calendarByDate[day.d] = day;
            Debug.Log($"Hongdal data: 하루 자료 {calendarByDate.Count}일");
            return calendarPack;
        }

        private StationHoursPack EnsureStationHours()
        {
            if (stationHoursTried) return stationHoursPack;
            stationHoursTried = true;
            stationHoursPack = LoadPack<StationHoursPack>("hongdal-data-station-hours");
            if (stationHoursPack?.stations != null)
                foreach (var entry in stationHoursPack.stations)
                {
                    if (entry?.n == null) continue;
                    if (!stationHoursByName.TryGetValue(entry.n, out var list)) stationHoursByName[entry.n] = list = new List<StationHoursEntry>();
                    list.Add(entry);
                }
            Debug.Log($"Hongdal data: 역 승하차 {stationHoursByName.Count}역");
            return stationHoursPack;
        }

        private static T LoadPack<T>(string resource) where T : class
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null) { Debug.Log("Hongdal data: no pack " + resource); return null; }
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (Exception error) { Debug.LogWarning("Hongdal data: " + resource + " " + error.Message); return null; }
        }

        // ── 실제 달력 ────────────────────────────────────────────────────────────

        private DateTime CalendarStart()
        {
            if (calendarStart.HasValue) return calendarStart.Value;
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith(CalendarDateFlag, StringComparison.Ordinal) &&
                    DateTime.TryParseExact(arg.Substring(CalendarDateFlag.Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fixedDate))
                    return (calendarStart = fixedDate).Value;
            var saved = PlayerPrefs.GetString(CalendarStartPreference, "");
            if (!DateTime.TryParseExact(saved, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            {
                start = DateTime.Today;
                PlayerPrefs.SetString(CalendarStartPreference, start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            return (calendarStart = start).Value;
        }

        private DateTime GameDate() => proofDate ?? previewDate ?? CalendarStart().AddDays(Math.Max(1, state?.campaign?.day ?? 1) - 1);      // 미리 보기: SagajeongDispatchEnvironment.cs

        // 게임 시계의 지금(자정부터 지난 초).
        private int GameSeconds()
        {
            if (proofSeconds >= 0) return proofSeconds;
            if (previewSeconds >= 0) return previewSeconds;
            var clock = ClockLabel(state?.now ?? "11:30:00");
            var parts = clock.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute)) return 11 * 3600 + 1800;
            var second = parts.Length > 2 && int.TryParse(parts[2], out var s) ? s : 0;
            return Mathf.Clamp(hour * 3600 + minute * 60 + second, 0, 86399);
        }

        private static string DateLabel(DateTime date)
        {
            var names = new[] { "일", "월", "화", "수", "목", "금", "토" };
            return $"{date.Month}월 {date.Day}일({names[(int)date.DayOfWeek]})";
        }

        private DayRead TodayRead()
        {
            var date = GameDate();
            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (dayRead != null && dayReadKey == key) return dayRead;
            EnsureCalendar();
            dayReadKey = key;
            dayRead = new DayRead { Date = date };
            dayRead.Lamp = FindDay(date, day => day.on > 0, out dayRead.LampNote);
            dayRead.Rain = FindDay(date, day => day.gauges > 0, out dayRead.RainNote);
            // 눈은 겨울 관측이 있는 철에만 찾는다(없는 날은 눈 없음이 아니라 관측 없음이다).
            dayRead.Snow = FindDay(date, day => day.snow > 0f || day.newSnow > 0f || !string.IsNullOrEmpty(day.snowText), out dayRead.SnowNote, exactOnly: date.Month is >= 4 and <= 10);
            return dayRead;
        }

        private CalendarDay FindDay(DateTime date, Func<CalendarDay, bool> has, out string note, bool exactOnly = false)
        {
            note = "관측 없음";
            for (var back = 0; back <= 3; back++)
            {
                if (back > 0 && exactOnly) break;
                DateTime probe;
                try { probe = date.AddYears(-back); } catch { break; }
                if (calendarByDate.TryGetValue(probe.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out var day) && has(day))
                {
                    note = back == 0 ? "그날 관측" : back == 1 ? "작년 같은 날" : $"{back}년 전 같은 날";
                    return day;
                }
            }
            return null;
        }

        // 밤의 깊이: 0 = 낮, 1 = 밤. 점등 30분 전부터 20분 뒤까지 저물고, 소등 20분 전부터 30분 뒤까지 밝는다.
        private float NightFactor()
        {
            var read = TodayRead();
            var on = read.Lamp?.on > 0 ? read.Lamp.on : 18 * 3600;
            var off = read.Lamp?.off > 0 ? read.Lamp.off : 6 * 3600 + 1800;
            var now = GameSeconds();
            var dusk = Mathf.InverseLerp(on - 1800, on + 1200, now);
            var dawn = 1f - Mathf.InverseLerp(off - 1200, off + 1800, now);
            return now >= 12 * 3600 ? dusk : dawn;
        }

        private bool LampsLit()
        {
            var read = TodayRead();
            var on = read.Lamp?.on > 0 ? read.Lamp.on : 18 * 3600;
            var off = read.Lamp?.off > 0 ? read.Lamp.off : 6 * 3600 + 1800;
            var now = GameSeconds();
            return now >= on || now < off;
        }

        // 지금 시각에 비가 오는가(그날 젖은 시간 비트). 비트가 없고 양만 있으면 하루 내내로 본다.
        private float RainNow()
        {
            var rain = TodayRead().Rain;
            if (rain == null || rain.rain <= 0f) return 0f;
            var hour = GameSeconds() / 3600;
            if (rain.wet != 0 && (rain.wet & (1 << hour)) == 0) return 0f;
            return rain.rain;
        }

        // ── 생활 시설 참고점 (중랑 16동 · 24범주) ──────────────────────────────────

        [Serializable] private sealed class FacilityPack { public string title; public string scope; public string[] keys; public string[] labels; public float[] x; public float[] y; public int[] k; }

        private FacilityPack facilityPack;
        private bool facilityTried;
        private Dictionary<int, int[]> facilityByTile;       // 타일 차례 → 범주별 개수

        private FacilityPack EnsureFacilities()
        {
            if (facilityTried) return facilityPack;
            facilityTried = true;
            facilityPack = LoadPack<FacilityPack>("hongdal-data-facilities");
            Debug.Log($"Hongdal data: 생활 시설 {facilityPack?.k?.Length ?? 0}곳");
            return facilityPack;
        }

        // 타일마다 범주별 참고점 수. 타일 팩이 읽힌 뒤 한 번 센다(가운데가 가장 가까운 칸).
        private int[] FacilitiesOfTile(int tileIndex)
        {
            var pack = EnsureFacilities();
            if (pack?.k == null || EnsureHexTiles() == null) return null;
            if (facilityByTile == null)
            {
                facilityByTile = new Dictionary<int, int[]>();
                for (var i = 0; i < pack.k.Length; i++)
                {
                    var tile = HexTileAt(new Vector2(pack.x[i], pack.y[i]));
                    if (tile < 0) continue;
                    if (!facilityByTile.TryGetValue(tile, out var counts)) facilityByTile[tile] = counts = new int[pack.keys.Length];
                    counts[pack.k[i]]++;
                }
            }
            return facilityByTile.TryGetValue(tileIndex, out var found) ? found : null;
        }

        private string FacilityLine(int[] counts, int top)
        {
            if (counts == null || facilityPack?.labels == null) return null;
            var order = new List<int>();
            for (var i = 0; i < counts.Length; i++) if (counts[i] > 0 && FacilityKeyShown(i)) order.Add(i);
            order.Sort((a, b) => counts[b].CompareTo(counts[a]));
            var parts = new List<string>();
            for (var i = 0; i < order.Count && i < top; i++) parts.Add($"{facilityPack.labels[order[i]]} {counts[order[i]]}");
            if (order.Count > top) parts.Add($"외 {order.Count - top}갈래");
            return string.Join(" · ", parts);
        }

        private static Color FacilityColour(string key) => key switch
        {
            "pedestrian_signal" => Ink(0xF4F7F8),
            "bus_stop" => Ink(0x6FE3C4),
            "heat_shade" or "heat_shelter" or "drinking_water" or "public_toilet" => Ink(0x6FA8FF),
            "bicycle_parking" or "bike_share" => Ink(0x7CDB72),
            "park" or "protected_tree" or "campground" => Ink(0x6FCF97),
            "seoul_market_inventory" or "designated_specialty_street" or "tourism_site" => Ink(0xFFD395),
            "metro_unnumbered_exit" or "station_elevator" => Ink(0xC9D08A),
            _ => Ink(0xABC1CB),
        };

        // 가까이 다가가면(타일 이름이 뜨는 배율부터) 시설 참고점을 작은 점으로 보인다.
        private void DrawGuFacilities()
        {
            if (guScale < 0.20f) return;
            var pack = EnsureFacilities();
            if (pack?.k == null) return;
            var alpha = Mathf.InverseLerp(0.20f, 0.25f, guScale);
            var radius = Mathf.Clamp(guScale * 13f, 2.6f, 6f);
            BeginFacilityMarks();
            for (var i = 0; i < pack.k.Length; i++)
            {
                if (!FacilityKeyShown(pack.k[i])) continue;
                var at = GuPoint(pack.x[i], pack.y[i]);
                if (at.x < -6f || at.y < -6f || at.x > Field.width + 6f || at.y > Field.height + 6f) continue;
                var colour = FacilityColour(pack.keys[pack.k[i]]);
                DrawFacilityMark(at, pack.keys[pack.k[i]], radius, alpha, colour);   // 가까우면 아이콘(SagajeongDispatchMapIcons.cs)
            }
            DrawGroundMarks(alpha, radius);                                         // 위치 확인 표식(횡단보도 · 따릉이, SagajeongDispatchMapIcons.cs)
            EndFacilityMarks();
        }

        // ── 길의 속도 · 버스 노선 (레이어 「길」 「버스」) ─────────────────────────────

        [Serializable] private sealed class RoadPack { public string title; public string source; public RoadLink[] links; }
        [Serializable] private sealed class RoadLink { public string n; public float[] p; public int[] v; public int ok; }
        [Serializable] private sealed class BusPack { public string title; public string source; public BusRoute[] routes; }
        [Serializable] private sealed class BusRoute { public string n; public int missing; public BusPart[] parts; }
        [Serializable] private sealed class BusPart { public float[] p; }

        private const string DataLayerPreference = "hongdal.data.layers";      // 비트: 1 길 · 2 버스
        private RoadPack roadPack;
        private BusPack busPack;
        private bool roadTried, busTried, dataLayersRead;
        private bool roadLayerOn = true, busLayerOn = true;

        private void ReadDataLayers()
        {
            if (dataLayersRead) return;
            dataLayersRead = true;
            var bits = PlayerPrefs.GetInt(DataLayerPreference, 3);
            roadLayerOn = (bits & 1) != 0;
            busLayerOn = (bits & 2) != 0;
        }

        private void SaveDataLayers()
        {
            PlayerPrefs.SetInt(DataLayerPreference, (roadLayerOn ? 1 : 0) | (busLayerOn ? 2 : 0));
            PlayerPrefs.Save();
        }

        private static Color SpeedColour(float kmh) =>
            kmh < 15f ? Ink(0xFF6B5B) : kmh < 25f ? Ink(0xFFB547) : kmh < 40f ? Ink(0xD9E27A) : Ink(0x6FE3C4);

        // 큰길을 그 시각의 10월 평일 평균 속도로 칠한다(느릴수록 붉다). 노선 밑, 타일 위.
        private void DrawGuRoadSpeed()
        {
            ReadDataLayers();
            if (!roadLayerOn) return;
            if (!roadTried) { roadTried = true; roadPack = LoadPack<RoadPack>("hongdal-data-roads"); Debug.Log($"Hongdal data: 길 {roadPack?.links?.Length ?? 0}"); }
            if (roadPack?.links == null) return;
            var hour = GameSeconds() / 3600;
            var width = Mathf.Clamp(guScale * 22f, 1.6f, 5f);
            var view = new Rect(-40, -40, Field.width + 80, Field.height + 80);
            // 시각 검증(2026-11-23): 모든 길을 같은 굵기로 칠하면 지도가 거미줄이 된다. 막힘(평소 속도에 견준 지금)만 키운다.
            //  1겹: 잘 뚫리는 길은 가늘고 옅은 청록. 2겹: 더딘 길 · 막힌 길은 굵고 따뜻하게, 막힌 길에는 번지는 빛.
            for (var pass = 0; pass < 2; pass++)
                for (var index = 0; index < roadPack.links.Length; index++)
                {
                    var link = roadPack.links[index];
                    if (link?.p == null || link.p.Length < 4 || link.v == null || link.v.Length < 24) continue;
                    var jam = RoadJam(index, hour, out _);
                    var loud = jam >= 0.2f;
                    if ((pass == 1) != loud) continue;
                    var colour = JamColour(jam);
                    var w = jam < 0f ? width * 0.45f : width * (0.5f + 0.9f * jam);
                    var alpha = jam < 0f ? 0.22f : loud ? 0.62f + 0.33f * jam : 0.30f;
                    var previous = GuPoint(link.p[0], link.p[1]);
                    for (var i = 2; i + 1 < link.p.Length; i += 2)
                    {
                        var point = GuPoint(link.p[i], link.p[i + 1]);
                        if (view.Contains(previous) || view.Contains(point))
                        {
                            if (jam >= 0.5f) Line(previous, point, Fade(colour, 0.10f + 0.10f * jam), w * 3.2f);
                            if (loud) Line(previous, point, Fade(Canvas, 0.55f), w + 1.6f);
                            Line(previous, point, Fade(colour, alpha), w, jam < 0f || link.ok != 1);
                        }
                        previous = point;
                    }
                }
        }

        // ── 구 지도의 밤 불빛 (2026-11-23 시각 검증): 큰길을 따라 놓은 불빛이 그날 점등 시각에 하나씩 켜진다 ──
        //  자리는 표현이다(큰길 선을 따라 70 m마다, 가로등 자리 자료가 아니다). 켜지는 때는 서울시 가로등 점등 기록(OA-22308).
        private List<Vector2> guLampSpots;

        private void DrawGuStreetLights(float now)
        {
            if (!skyOn) return;
            var night = NightFactor();
            if (night <= 0.01f) return;
            if (guLampSpots == null)
            {
                guLampSpots = new List<Vector2>();
                if (!roadTried) { roadTried = true; roadPack = LoadPack<RoadPack>("hongdal-data-roads"); }
                if (roadPack?.links != null)
                    foreach (var link in roadPack.links)
                    {
                        if (link?.p == null || link.p.Length < 4) continue;
                        var carry = 35f;
                        for (var i = 2; i + 1 < link.p.Length; i += 2)
                        {
                            var a = new Vector2(link.p[i - 2], link.p[i - 1]);
                            var b = new Vector2(link.p[i], link.p[i + 1]);
                            var length = Vector2.Distance(a, b);
                            var at = carry;
                            while (at < length) { guLampSpots.Add(Vector2.Lerp(a, b, at / length)); at += 70f; }
                            carry = at - length;
                        }
                    }
            }
            var view = new Rect(-10, -10, Field.width + 20, Field.height + 20);
            var core = Mathf.Clamp(guScale * 9f, 0.8f, 2.2f);
            var step = guScale < 0.05f ? 3 : guScale < 0.09f ? 2 : 1;      // 멀리서는 셋에 하나
            for (var i = 0; i < guLampSpots.Count; i += step)
            {
                var spot = GuPoint(guLampSpots[i].x, guLampSpots[i].y);
                if (!view.Contains(spot)) continue;
                var turn = SubwayHash(i, 31);
                if (turn > night * 1.15f) continue;                          // 점등이 번져 가는 동안 하나씩 켜진다
                var flicker = 0.9f + 0.1f * Mathf.Sin(now * 1.7f + i);
                Disc(spot, core * 4.2f, Fade(Ink(0xFFC77A), 0.07f * night * flicker));
                Disc(spot, core, Fade(Ink(0xFFE3B0), 0.85f * night));
            }
        }

        // 중랑구를 지나는 버스 노선의 확인된 선 조각. 끊긴 곳은 끊긴 채로.
        private void DrawGuBusLines()
        {
            ReadDataLayers();
            if (!busLayerOn) return;
            if (!busTried) { busTried = true; busPack = LoadPack<BusPack>("hongdal-data-bus"); Debug.Log($"Hongdal data: 버스 {busPack?.routes?.Length ?? 0}노선"); }
            if (busPack?.routes == null) return;
            var view = new Rect(-40, -40, Field.width + 80, Field.height + 80);
            var colour = Fade(Ink(0x4C8DF0), 0.42f);
            var width = Mathf.Clamp(guScale * 9f, 1f, 2.2f);
            foreach (var route in busPack.routes)
            {
                if (route?.parts == null) continue;
                foreach (var part in route.parts)
                {
                    if (part?.p == null || part.p.Length < 4) continue;
                    var previous = GuPoint(part.p[0], part.p[1]);
                    for (var i = 2; i + 1 < part.p.Length; i += 2)
                    {
                        var point = GuPoint(part.p[i], part.p[i + 1]);
                        if (view.Contains(previous) || view.Contains(point)) Line(previous, point, colour, width);
                        previous = point;
                    }
                }
            }
        }

        private static string Seconds(int value) => value <= 0 ? "—" : $"{value / 3600:00}:{value % 3600 / 60:00}";

        // ── 역의 하루 ────────────────────────────────────────────────────────────

        // 역 이름(「사가정역」)의 시간대 승하차. 갈아타는 역은 그 역의 노선을 모두 더한다(같은 원천 · 같은 단위).
        // 달: 게임 날짜의 달 → 작년 같은 달 → 가장 최근 달.
        private (int[] Board, int[] Alight, string Month, string Note, int Lines)? StationHours(string stationName)
        {
            if (stationName == null || EnsureStationHours()?.months == null) return null;
            var label = stationName.EndsWith("역") ? stationName.Substring(0, stationName.Length - 1) : stationName;
            var date = GameDate();
            var want = date.ToString("yyyyMM", CultureInfo.InvariantCulture);
            var cacheKey = label + "|" + want;
            if (stationHoursCache.TryGetValue(cacheKey, out var cached)) return cached.Board == null ? null : cached;
            string month = null, note = null;
            var months = new List<string>(stationHoursPack.months);
            if (months.Contains(want)) { month = want; note = "그달"; }
            else if (months.Contains(date.AddYears(-1).ToString("yyyyMM", CultureInfo.InvariantCulture))) { month = date.AddYears(-1).ToString("yyyyMM", CultureInfo.InvariantCulture); note = "작년 같은 달"; }
            else if (months.Count > 0) { month = months[0]; note = "가장 최근 달"; }
            var board = new int[24];
            var alight = new int[24];
            var lines = 0;
            if (month != null && stationHoursByName.TryGetValue(label, out var entries))
                foreach (var entry in entries)
                {
                    if (entry.months == null) continue;
                    foreach (var row in entry.months)
                    {
                        if (row.m != month || row.b == null || row.a == null) continue;
                        lines++;
                        for (var hour = 0; hour < 24; hour++)
                        {
                            board[hour] += Math.Max(0, row.b[hour]);
                            alight[hour] += Math.Max(0, row.a[hour]);
                        }
                    }
                }
            var result = lines == 0 ? (null, null, null, null, 0) : (board, alight, month, note, lines);
            stationHoursCache[cacheKey] = result;
            return lines == 0 ? null : result;
        }

        // 지금 시각의 승하차가 그 역의 가장 붐비는 시각에 비해 얼마인가(0~1). 지도의 역 불빛 크기.
        private float StationHourShare(string stationName)
        {
            var hours = StationHours(stationName);
            if (hours == null) return -1f;
            var peak = 1;
            for (var hour = 0; hour < 24; hour++) peak = Math.Max(peak, hours.Value.Board[hour] + hours.Value.Alight[hour]);
            var now = GameSeconds() / 3600;
            return (hours.Value.Board[now] + hours.Value.Alight[now]) / (float)peak;
        }

        private static string MonthLabel(string month) =>
            month != null && month.Length == 6 ? $"{month.Substring(0, 4)}년 {int.Parse(month.Substring(4))}월" : "—";

        // 역 창의 「역의 하루」: 24시간 승차 · 하차 막대, 지금 시각 칸이 밝다. 쓴 높이를 돌려준다.
        private float DrawStationHours(SubwayStation station, float x, float y, float wide)
        {
            var hours = StationHours(station.name);
            if (hours == null)
            {
                Text(new Rect(x, y, wide, 14f), "역의 하루 · 승하차 자료 없음(서울 밖 역이거나 이름이 다르다)", 10, Soft, true);
                return 18f;
            }
            var (board, alight, month, note, lines) = hours.Value;
            Text(new Rect(x, y, wide, 14f), $"역의 하루 · {MonthLabel(month)}({note}){(lines > 1 ? $" · 노선 {lines}개 합" : "")}", 10, Gold, true);
            var peak = 1;
            for (var hour = 0; hour < 24; hour++) peak = Math.Max(peak, Math.Max(board[hour], alight[hour]));
            var top = y + 16f;
            const float tall = 30f;
            var slot = wide / 24f;
            var now = GameSeconds() / 3600;
            Paint(new Rect(x + slot * now - 1f, top - 1f, slot + 2f, tall + 2f), Fade(Paper, 0.13f), 3);
            var alightColour = Ink(0x6FA8FF);
            for (var hour = 0; hour < 24; hour++)
            {
                var bh = Mathf.Max(board[hour] > 0 ? 1f : 0f, tall * board[hour] / peak);
                var ah = Mathf.Max(alight[hour] > 0 ? 1f : 0f, tall * alight[hour] / peak);
                var bar = Mathf.Max(1f, slot / 2f - 0.8f);
                var lit = hour == now ? 1f : 0.62f;
                Paint(new Rect(x + slot * hour + 0.5f, top + tall - bh, bar, bh), Fade(Mint, lit), 1);
                Paint(new Rect(x + slot * hour + slot / 2f, top + tall - ah, bar, ah), Fade(alightColour, lit), 1);
            }
            foreach (var mark in new[] { 0, 6, 12, 18 })
                Text(new Rect(x + slot * mark, top + tall + 1f, 30f, 11f), mark + "시", 8, Soft);
            Text(new Rect(x, top + tall + 12f, wide, 14f),
                $"지금 {now}시대 · 승차 {board[now]:N0} · 하차 {alight[now]:N0}건(교통카드 · 월 누적)", 10, Paper);
            return 16f + tall + 12f + 16f;
        }

        // ── 구 지도의 밤과 날씨 ────────────────────────────────────────────────────

        // 바탕 위, 다른 그림 아래에 깐다(DrawGuMap).
        private void DrawGuSky(float now)
        {
            ReadDataControl();
            if (!skyOn) return;
            var night = NightFactor();
            var area = new Rect(0, 0, Field.width, Field.height);
            if (night < 1f) Paint(area, Fade(Ink(0xC9D6DE), 0.06f * (1f - night)));           // 낮: 살짝 걷힌다
            // 노을(2026-11-23 시각 검증): 점등 앞뒤로 화면 위쪽부터 따뜻한 띠가 내려온다
            var twilight = Mathf.Clamp01(1f - Mathf.Abs(night - 0.35f) / 0.4f);
            if (twilight > 0f)
                for (var band = 0; band < 10; band++)
                {
                    var h = area.height * 0.07f;
                    Paint(new Rect(0, band * h, area.width, h + 1f), Fade(band < 4 ? Ink(0xF28C6B) : Ink(0xB76BA8), 0.13f * twilight * (1f - band / 10f)));
                }
            if (night > 0f) Paint(area, Fade(Ink(0x030915), 0.50f * night));                  // 밤: 깊고 푸르게
        }

        // 맨 위에 덮는다: 비 · 눈, 그리고 「오늘」 칩.
        private void DrawGuWeather(float now) => DrawWeatherLayer(now, true, false);

        // 비 · 눈 그림. 구 지도는 「오늘」 칩까지, 동 디오라마는 그림만(SagajeongDispatchStreetProps.cs).
        // bright = 동 디오라마(밝은 바탕): 하늘을 더 가라앉히고 빗줄기를 진하게.
        private void DrawWeatherLayer(float now, bool chip, bool bright)
        {
            ReadDataControl();
            var read = TodayRead();
            var rain = weatherOn ? RainNow() : 0f;
            var area = new Rect(0, 0, Field.width, Field.height);
            // 시각 검증(2026-11-23): 비는 먼 빗줄기 · 가까운 빗줄기 두 겹과 땅에 떨어지는 물방울 고리, 눈은 덮인 땅의 흰 기운과 천천히 흔들리며 내리는 눈송이.
            if (rain > 0f)
            {
                var strength = Mathf.InverseLerp(0f, 30f, rain);
                Paint(area, Fade(Ink(0x0E2233), (bright ? 0.20f : 0.10f) + (bright ? 0.18f : 0.14f) * strength));      // 젖은 하늘
                var windNow = EnvNow();      // 바람 쪽으로 비껴 내린다(2026-11-24, SagajeongDispatchEnvironment.cs). 화면은 북이 위
                var lean = 0.10f * windNow.WindSpeed;
                var fall = new Vector2(windNow.WindToward.x * lean, 1f - windNow.WindToward.y * lean * 0.5f).normalized;
                var far = Mathf.RoundToInt(140f + 260f * strength);
                for (var i = 0; i < far; i++)
                {
                    var speed = 1.4f + 0.6f * SubwayHash(i, 7);
                    var px = SubwayHash(i, 1) * (area.width + 160f) - 20f;
                    var py = Mathf.Repeat(SubwayHash(i, 2) * area.height + now * speed * area.height, area.height + 40f) - 20f;
                    var start = new Vector2(px - fall.x * py * 0.3f, py);
                    Line(start, start + fall * (7f + 6f * SubwayHash(i, 3)), Fade(Ink(0xA9C9E2), 0.16f + 0.12f * strength), 1f);
                }
                var near = Mathf.RoundToInt(40f + 110f * strength);
                for (var i = 0; i < near; i++)
                {
                    var speed = 2.1f + 0.7f * SubwayHash(i, 17);
                    var px = SubwayHash(i, 11) * (area.width + 200f) - 20f;
                    var py = Mathf.Repeat(SubwayHash(i, 12) * area.height + now * speed * area.height, area.height + 60f) - 30f;
                    var head = new Vector2(px - fall.x * py * 0.3f, py);
                    var length = 18f + 16f * SubwayHash(i, 13);
                    Line(head - fall * length, head - fall * length * 0.45f, Fade(Ink(0xCFE4F4), 0.14f + 0.10f * strength), 1.6f);
                    Line(head - fall * length * 0.45f, head, Fade(Ink(0xE6F2FB), (bright ? 0.52f : 0.34f) + 0.22f * strength), bright ? 2f : 1.6f);
                }
                var rings = Mathf.RoundToInt(14f + 36f * strength);
                for (var i = 0; i < rings; i++)
                {
                    var cycle = now * 0.9f + SubwayHash(i, 41);
                    var t = cycle - Mathf.Floor(cycle);
                    var round = Mathf.FloorToInt(cycle);
                    var spot = new Vector2(SubwayHash(i * 31 + round, 42) * area.width, SubwayHash(i * 17 + round, 43) * area.height);
                    Hoop(spot, 2f + 11f * t, Fade(Ink(0xCFE4F4), 0.32f * (1f - t)), 1f);
                }
            }
            var snow = weatherOn ? read.Snow : null;
            if (snow != null && (snow.snow > 0f || snow.newSnow > 0f))
            {
                var cover = Mathf.Clamp01(snow.snow / 8f);
                if (!seasonOn) Paint(area, Fade(Ink(0xDCE8F2), 0.06f + 0.14f * cover));      // 덮인 땅(계절 바탕을 켜면 동은 셰이더가, 구는 계절 바탕 그림이 땅 · 지붕에 쌓는다)
                var flakes = Mathf.RoundToInt(snow.newSnow > 0f ? 220f : 90f);
                for (var i = 0; i < flakes; i++)
                {
                    var depth = SubwayHash(i, 25);                                              // 0 멀리 · 1 가까이
                    var px = Mathf.Repeat(SubwayHash(i, 21) * area.width + Mathf.Sin(now * (0.5f + 0.4f * depth) + i) * (8f + 14f * depth), area.width);
                    var py = Mathf.Repeat(SubwayHash(i, 22) * area.height + now * (14f + 30f * depth), area.height);
                    var size = 1.0f + 2.4f * depth;
                    Disc(new Vector2(px, py), size * 2.4f, Fade(Paper, 0.08f + 0.06f * depth));
                    Disc(new Vector2(px, py), size, Fade(Paper, 0.45f + 0.4f * depth));
                }
            }
            if (chip && todayChipOn && !(PortraitScreen && (PortraitCardOpen || portraitSheet == 2))) DrawTodayChip(read, RainNow());
        }

        private Rect TodayChipRect => PortraitScreen      // 세로 화면: 지도 왼쪽 아래(판 바로 위) — 위쪽 동 이름을 가리지 않게 (SagajeongDispatchPortrait.cs)
            ? new Rect(10f, portraitSheetRect.y - (seasonOn ? 66f : 52f) - (CapitalReady ? 34f : 22f), 196f, seasonOn ? 66f : 52f)
            : new Rect(22f - Field.x, 128f - Field.y, 196f, seasonOn ? 66f : 52f);

        private void DrawTodayChip(DayRead read, float rainNow)
        {
            var chip = TodayChipRect;
            Paint(chip, Fade(Canvas, 0.88f), 8);
            var x = chip.x + 10f;
            var lit = LampsLit();
            Text(new Rect(x, chip.y + 4f, chip.width - 20f, 18f), (previewDate.HasValue ? $"미리 보기 · {read.Date:yy}년 " : "") + $"{DateLabel(read.Date)} · {GameSeconds() / 3600:00}:{GameSeconds() % 3600 / 60:00}", previewDate.HasValue ? 11 : 13, previewDate.HasValue ? Gold : Paper, true);
            var lamp = read.Lamp == null ? "점등 자료 없음" : $"점등 {Seconds(read.Lamp.on)} · 소등 {Seconds(read.Lamp.off)}{(read.LampNote == "그날 관측" ? "" : " · " + read.LampNote)}";
            Text(new Rect(x, chip.y + 22f, chip.width - 20f, 13f), (lit ? "● " : "○ ") + lamp, 9, lit ? Gold : Soft);
            string weather;
            if (read.Snow != null && (read.Snow.snow > 0f || read.Snow.newSnow > 0f))
                weather = $"눈 · 적설 {read.Snow.snow:0.#} cm{(read.Snow.newSnow > 0f ? $" · 새 눈 {read.Snow.newSnow:0.#}" : "")} · {read.SnowNote}";
            else if (read.Rain == null) weather = "비 · 관측 없음";
            else weather = read.Rain.rain <= 0f ? $"비 없음 · {read.RainNote}" :
                $"비 {read.Rain.rain:0.#} mm{(rainNow > 0f ? " · 지금 내림" : "")} · {read.RainNote}";
            Text(new Rect(x, chip.y + 36f, chip.width - 20f, 13f), weather, 9, rainNow > 0f ? Ink(0x9CC4E4) : Soft);
            if (!seasonOn) return;
            var env = EnvNow();      // 철 한 줄 (SagajeongDispatchEnvironment.cs)
            Text(new Rect(x, chip.y + 50f, chip.width - 20f, 13f), SeasonLine(env) + " · " + SeasonShortSource(env), 9, Ink(0x9BD67A));
        }
    }
}
