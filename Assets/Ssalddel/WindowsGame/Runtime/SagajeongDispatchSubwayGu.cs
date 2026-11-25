using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 구 지도 위의 지하철 노선도와 역 번화가 (2026-11-19 18:02 사용자 방향).
    //  - 사용자: 동 단위 지도는 법령 때문에 배포가 어렵다. 구 단위 지도에서 노선도가 잘 보이고 역을 중심으로 번화가가 선 듯한 지도를 만든다.
    //  - 노선은 제 색의 빛 줄, 역은 고리, 역 둘레는 따뜻한 빛과 불 켜진 작은 칸들(번화가). 노선이 많이 만나는 역일수록 넓고 촘촘하다.
    //  - 번화가의 넓이 · 칸 수 · 자리는 표현값이다(역마다 고정된 난수). 실제 상권 자료가 아니다.
    //  - 자료(Resources/hongdal-subway-gu.json)는 지금 주소기반산업지원서비스 기타자료에서 뽑았다. 내부 검토용 · 배포 아님.
    //    배포하려면 공개 이용이 되는 역 · 노선 자료로 바꿔야 한다(파일만 갈면 된다).
    // 보기만 더한다. 영역 · 주문 · 돈 규칙을 읽거나 바꾸지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string SubwayGuResource = "hongdal-subway-gu";
        private const float GuStationNameScale = 0.11f;      // 이보다 가까이 보면 모든 역 이름을 세운다. 그 전에는 환승역만(표현값)
        private const float GuTownMetres = 260f;             // 번화가 반경(m, 표현값). 노선 하나마다 넓어진다
        private const string SubwayOptionPreference = "hongdal.subway.gu.options";      // 비트: 1 노선 · 2 번화가 · 4 내력 · 8 빛의 번짐
        private bool subwayOptionsRead;
        private bool subwayLinesOn = true;      // 자료 창(ReadDataControl)이 덮는다
        private bool subwayTownOn = true;
        private bool subwayLoreOn = true;
        private SubwayPack subwayGuPack;
        private bool subwayGuTried;

        private SubwayPack EnsureSubwayGu()
        {
            if (subwayGuTried) return subwayGuPack;
            subwayGuTried = true;
            var asset = Resources.Load<TextAsset>(SubwayGuResource);
            if (asset == null) return null;
            try { subwayGuPack = JsonUtility.FromJson<SubwayPack>(asset.text); }
            catch (Exception error) { Debug.LogWarning("Hongdal subway (gu): " + error.Message); }
            if (subwayGuPack?.lines != null)
                foreach (var line in subwayGuPack.lines)
                    if (line != null && !string.IsNullOrEmpty(line.name) && ColorUtility.TryParseHtmlString("#" + line.colour, out var colour))
                        subwayColours[line.name] = colour;
            Debug.Log($"Hongdal subway (gu): lines {subwayGuPack?.lines?.Length ?? 0}, stations {subwayGuPack?.stations?.Length ?? 0}");
            return subwayGuPack;
        }

        private void ReadSubwayOptions()
        {
            if (subwayOptionsRead) return;
            subwayOptionsRead = true;
            var bits = PlayerPrefs.GetInt(SubwayOptionPreference, 15);
            subwayLinesOn = (bits & 1) != 0;
            subwayTownOn = (bits & 2) != 0;
            subwayLoreOn = (bits & 4) != 0;
            subwayClashOn = (bits & 8) != 0;
        }

        // 보기 옵션(노선 · 번화가 · 내력 · 번짐)은 자료 창이 켜고 끈다 (SagajeongDispatchDataControl.cs).

        private Vector2 GuSubwayPoint(double latitude, double longitude)
        {
            var metres = 이어지는지도Policy.구미터(longitude, latitude, guSpace.west, guSpace.south);
            return GuPoint(metres.x, metres.y);
        }

        private static float SubwayHash(int seed, int step)
        {
            unchecked
            {
                var value = (uint)(seed * 73856093) ^ (uint)(step * 19349663);
                value ^= value >> 13; value *= 0x5bd1e995; value ^= value >> 15;
                return (value & 0xFFFFFF) / 16777216f;
            }
        }

        // DrawGuMap이 바탕과 영역 빛 뒤, 이름표 앞에 부른다. Repaint에서만.
        private void DrawGuSubway(float now)
        {
            var pack = EnsureSubwayGu();
            if (pack == null || guSpace == null) return;
            ReadSubwayOptions();
            if (!subwayLinesOn && !subwayTownOn && !subwayLoreOn && !subwayClashOn) return;
            loreTaken.Clear();
            foreach (var cluster in guClusters)      // 가게 · 기사 표식(앞 그림의 자리)은 패가 가리지 않는다
                loreTaken.Add(new Rect(cluster.Point.x - 22f, cluster.Point.y - 50f, 44f, 56f));
            var view = new Rect(-60, -60, Field.width + 120, Field.height + 120);

            // 1. 번화가: 역 둘레의 따뜻한 빛과 불 켜진 칸. 노선보다 먼저 깔아 노선이 그 위를 지난다.
            //    타일 지도가 떠오르면 꾸민 칸은 옅어진다 — 실제 자료의 타일이 그 자리를 맡는다(SagajeongDispatchHexTiles.cs).
            var townKeep = 1f - 0.85f * HexTilesAlpha;
            var townNight = 0.65f + 0.55f * NightFactor();      // 해가 지면 번화가 불이 더 밝다(그날 실제 점등 시각, SagajeongDispatchDataPacks.cs)
            if (subwayTownOn && pack.stations != null)
                foreach (var station in pack.stations)
                {
                    if (station == null) continue;
                    var at = GuSubwayPoint(station.latitude, station.longitude);
                    if (!view.Contains(at)) continue;
                    var count = Mathf.Clamp(station.lines?.Length ?? 1, 1, 4);
                    var reach = GuTownMetres * (0.75f + 0.35f * count);
                    var radius = reach * guScale;
                    var seed = 17;
                    if (station.name != null) foreach (var letter in station.name) seed = unchecked(seed * 31 + letter);      // 실행마다 같은 자리
                    var breath = 1f + 0.06f * Mathf.Sin(now * 1.3f + (seed & 255) * 0.1f);
                    // 불은 그 역 둘레의 운영 깊이를 따른다(SagajeongDispatchStationCard.cs). 닿지 않은 역은 잠든 칸만 옅게 남는다.
                    var depth = StationDepth(station);
                    const int layers = 12;
                    if (depth > 0f)
                        for (var layer = 0; layer < layers; layer++)      // 얇은 겹을 포개 가장자리가 스르르 풀리게 한다
                        {
                            var t = layer / (float)(layers - 1);                      // 0 = 가장 바깥, 1 = 가운데
                            Disc(at, radius * Mathf.Lerp(1.7f, 0.18f, t), Fade(Gold, (0.01f + 0.022f * t) * (0.7f + 0.15f * count) * breath * (0.35f + 0.65f * depth) * townKeep * townNight));
                        }
                    var cells = 14 + 12 * count;
                    var lit = Mathf.RoundToInt(cells * depth);
                    for (var i = 0; i < cells; i++)
                    {
                        var angle = SubwayHash(seed, i * 3) * Mathf.PI * 2f;
                        var far = Mathf.Pow(SubwayHash(seed, i * 3 + 1), 0.8f) * radius;      // 가운데가 더 촘촘하다
                        var size = Mathf.Max(1.6f, (22f + 30f * SubwayHash(seed, i * 3 + 2)) * guScale);
                        var spot = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * far;
                        var glow = 1f - far / Mathf.Max(1f, radius);
                        var box = new Rect(spot.x - size / 2f, spot.y - size / 2f, size, size);
                        if (i < lit)
                        {
                            var tint = i % 5 == 0 ? Paper : i % 3 == 0 ? Ink(0xFF9F5A) : Gold;
                            Paint(box, Fade(tint, Mathf.Clamp01((0.4f + 0.55f * glow) * townKeep * townNight)), 1);
                        }
                        else Paint(box, Fade(Soft, (0.10f + 0.10f * glow) * townKeep), 1);      // 잠든 칸
                    }
                }

            DrawStationCells(now);      // 고른 역의 칸 = 행정동 (SagajeongDispatchStationCells.cs)

            // 2. 노선
            if (subwayLinesOn && pack.lines != null)
                foreach (var line in pack.lines)
                {
                    if (line?.parts == null) continue;
                    var colour = subwayColours.TryGetValue(line.name ?? "", out var found) ? found : StationLine;
                    foreach (var part in line.parts)
                    {
                        if (part?.points == null || part.points.Length < 4) continue;
                        var previous = GuSubwayPoint(part.points[0], part.points[1]);
                        for (var i = 2; i + 1 < part.points.Length; i += 2)
                        {
                            var point = GuSubwayPoint(part.points[i], part.points[i + 1]);
                            if (view.Contains(previous) || view.Contains(point))
                            {
                                Line(previous, point, Fade(colour, 0.14f), 15f);
                                Line(previous, point, Fade(Canvas, 0.9f), 8f);
                                Line(previous, point, colour, 5f);
                                Line(previous, point, Fade(Paper, 0.35f), 1.2f);
                            }
                            previous = point;
                        }
                    }
                }

            DrawGuLightClash(now);      // 빛의 번짐 (SagajeongDispatchLightClash.cs)

            // 3. 역 고리와 이름
            if (pack.stations == null) return;
            DrawGuStationMark(now);
            var all = guScale >= GuStationNameScale;
            foreach (var station in pack.stations)
            {
                if (station == null) continue;
                var at = GuSubwayPoint(station.latitude, station.longitude);
                if (!view.Contains(at)) continue;
                var colour = station.lines != null && station.lines.Length > 0 && subwayColours.TryGetValue(station.lines[0], out var found) ? found : StationLine;
                var size = station.transfer ? 16f : 11f;
                var lored = subwayLoreOn && station.name != null && StationLores.ContainsKey(station.name);
                if (!subwayLinesOn && !lored) continue;
                // 역의 하루: 지금 시각의 승하차가 그 역의 가장 붐비는 시각에 비해 클수록 역 불빛이 크다(서울시 시간대 승하차).
                var busy = stationPulseOn ? StationHourShare(station.name) : -1f;
                if (busy > 0f)
                {
                    // 시각 검증(2026-11-23): 회색 원 대신 노선 색의 빛과 「사람의 물결」 — 붐빌수록 고리가 자주 · 멀리 퍼진다.
                    var tint = station.transfer ? Ink(0xFFE3B0) : colour;
                    var swell = size + 6f + 30f * busy * (0.94f + 0.06f * Mathf.Sin(now * 2.2f + at.x * 0.01f));
                    Disc(at, swell, Fade(tint, 0.05f + 0.10f * busy));
                    Disc(at, swell * 0.5f, Fade(tint, 0.08f + 0.16f * busy));
                    var period = Mathf.Lerp(3.4f, 1.2f, busy);
                    for (var ring = 0; ring < 2; ring++)
                    {
                        var t = Mathf.Repeat(now / period + ring * 0.5f + at.x * 0.0137f, 1f);
                        Hoop(at, size * 0.6f + swell * 1.25f * t, Fade(tint, (0.08f + 0.32f * busy) * (1f - t)), 1.4f);
                    }
                }
                Paint(Centered(at, size + 4f, size + 4f), Canvas, (size + 4f) / 2f);
                Paint(Centered(at, size, size), station.transfer ? Paper : colour, size / 2f);
                Paint(Centered(at, size - 5f, size - 5f), station.transfer ? Canvas : Paper, (size - 5f) / 2f);
                if (string.IsNullOrEmpty(station.name) || !(lored || (subwayLinesOn && (all || station.transfer)))) continue;
                var title = station.name.EndsWith("역") ? station.name.Substring(0, station.name.Length - 1) : station.name;
                var width = title.Length * 11f + 12f;
                var plate = new Rect(at.x - width / 2f, at.y - size / 2f - 20f, width, 16f);
                Paint(plate, Fade(Canvas, 0.82f), 5);
                Text(plate, title, 10, station.transfer ? Paper : Soft, station.transfer, TextAnchor.MiddleCenter);
                loreTaken.Add(plate);
                if (lored) DrawStationLore(station.name, at, plate.y, now);      // 역의 내력 문양 (SagajeongDispatchStationLore.cs)
            }
        }
    }
}
