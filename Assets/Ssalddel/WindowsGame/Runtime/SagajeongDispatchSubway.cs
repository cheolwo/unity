using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 지하철 덧입힘 — 화면 층 (2026-11-19 사용자 방향: 디오라마 위에 지하철 노선 길이 보이고, 역을 중심으로 생각한다).
    //  - 굽기판(건물 · 길)은 그대로 두고 노선 선형과 역 자리만 따로 읽어 그 위에 그린다(Resources/hongdal-subway.json).
    //    자료는 주소기반산업지원서비스 기타자료(서울)의 지하철역 · 노선 층에서 뽑았다. 내부 검토용 · 배포 아님.
    //  - 땅속 길을 땅 위의 빛 줄로 보인다. 역은 빛과 고리, 가까이 보면 이름이 선다.
    //  - 역 중심 보기에서는 판(둥근 창) 안쪽만 그린다. 띄워 보기에서는 동이 옮겨져 선이 어긋나므로 쉰다.
    // 보기만 더한다. 시계 · 주문 · 돈 · 영역 규칙을 읽거나 바꾸지 않는다. 역 단계 · 사람 자리 · 구간 잇기는 여기 없다(기획 뒤).
    public sealed partial class SagajeongDispatchView
    {
        private const string SubwayResource = "hongdal-subway";
        private const float SubwayNameZoom = 1.1f;        // 이보다 가까이 보면 기둥 위에 노선 이름도 세운다(표현값)
        private const float SubwayPillarMetres = 70f;     // 역 기둥의 높이(m, 표현값). 환승역은 조금 더 높다

        [Serializable]
        private sealed class SubwayPack
        {
            public string schema;
            public string source;
            public string label;
            public SubwayLine[] lines;
            public SubwayStation[] stations;
        }

        [Serializable]
        private sealed class SubwayLine
        {
            public string name;
            public string colour;
            public SubwayPart[] parts;
        }

        [Serializable]
        private sealed class SubwayPart
        {
            public double[] points;      // 위도, 경도, 위도, 경도 …
        }

        [Serializable]
        private sealed class SubwayStation
        {
            public string name;
            public double latitude;
            public double longitude;
            public string[] lines;
            public bool transfer;
            public string[] dongs;
        }

        private SubwayPack subwayPack;
        private bool subwayTried;
        private readonly Dictionary<string, Color> subwayColours = new(StringComparer.Ordinal);

        private SubwayPack EnsureSubway()
        {
            if (subwayTried) return subwayPack;
            subwayTried = true;
            try
            {
                var asset = Resources.Load<TextAsset>(SubwayResource);
                if (asset == null)
                {
                    Debug.Log("Hongdal subway: no overlay resource");
                    return null;
                }
                var pack = JsonUtility.FromJson<SubwayPack>(asset.text);
                if (pack?.lines == null || pack.stations == null) return null;
                foreach (var line in pack.lines)
                {
                    if (line?.name == null) continue;
                    subwayColours[line.name] = ColorUtility.TryParseHtmlString("#" + line.colour, out var parsed) ? parsed : Soft;
                }
                subwayPack = pack;
                Debug.Log("Hongdal subway: lines " + pack.lines.Length + ", stations " + pack.stations.Length);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Hongdal subway: overlay unreadable — " + error.Message);
                subwayPack = null;
            }
            return subwayPack;
        }

        // 역 중심 보기에서는 판 안쪽(반경 + 흐려지는 띠)만 그린다.
        private bool SubwayShown(double latitude, double longitude) =>
            !StationView || StationDistance(latitude, longitude) <= 역중심Policy.Radius + 역중심Policy.Fade;

        private const float SubwayLightRadius = 150f;     // 역 둘레를 밝히는 빛의 반경(m, 표현값)
        private const float SubwayLightRest = 0.8f;       // 역이 늘 띠는 빛(표현값). 환승역은 조금 더 밝다

        // 땅의 빛을 놓을 때 부른다(SagajeongDispatchRoam.cs). 역 둘레의 건물과 길이 밝아진다. 보기만 바꾼다.
        private void LightSubwayStations()
        {
            if (roamLight == null || dongManifest == null) return;
            var pack = EnsureSubway();
            if (pack?.stations == null) return;
            foreach (var station in pack.stations)
            {
                if (station == null) continue;
                var (east, north) = DongLocal(station.latitude, station.longitude);
                roamLight.Light(기사불빛Policy.Store, (float)east, (float)north, SubwayLightRadius,
                    station.transfer ? Mathf.Min(1f, SubwayLightRest + 0.2f) : SubwayLightRest);
            }
        }

        // DrawDongModuleBoundaries가 맨 먼저 부른다. GUI.BeginGroup(Field) 안의 좌표.
        private void DrawSubwayOverlay()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (dongManifest == null || dongSpread > 0.01f) return;
            if (!subwayLinesOn) return;                 // 자료 창 「지하철 노선」 (SagajeongDispatchDataControl.cs)
            var pack = EnsureSubway();
            if (pack == null) return;

            foreach (var line in pack.lines)
            {
                if (line?.parts == null) continue;
                var colour = subwayColours.TryGetValue(line.name ?? string.Empty, out var found) ? found : Soft;
                foreach (var part in line.parts)
                {
                    var values = part?.points;
                    if (values == null || values.Length < 4) continue;
                    var previousShown = SubwayShown(values[0], values[1]);
                    var previous = ProjectScene(values[0], values[1], 0f, Vector2.zero);
                    for (var i = 2; i + 1 < values.Length; i += 2)
                    {
                        var shown = SubwayShown(values[i], values[i + 1]);
                        var point = ProjectScene(values[i], values[i + 1], 0f, Vector2.zero);
                        if (shown || previousShown)
                        {
                            Line(previous, point, Fade(colour, 0.14f), 16f);
                            Line(previous, point, Fade(Canvas, 0.8f), 9f);
                            Line(previous, point, Fade(colour, 0.45f), 8f);
                            Line(previous, point, colour, 4f);
                            Line(previous, point, Fade(Paper, 0.55f), 1.2f);
                        }
                        previous = point;
                        previousShown = shown;
                    }
                }
            }

            var names = mapZoom >= SubwayNameZoom;
            foreach (var station in pack.stations)
            {
                if (station == null || !SubwayShown(station.latitude, station.longitude)) continue;
                var at = ProjectScene(station.latitude, station.longitude, 0f, Vector2.zero);
                if (at.x < -40 || at.x > Field.width + 40 || at.y < -40 || at.y > Field.height + 40) continue;
                var colour = station.lines != null && station.lines.Length > 0 && subwayColours.TryGetValue(station.lines[0], out var found) ? found : StationLine;
                var size = station.transfer ? 22f : 18f;
                if (roamGlow != null) GlowNow(at, station.transfer ? 96f : 76f, Fade(Paper, 0.4f));
                Paint(Centered(at, size + 5f, size + 5f), Canvas, (size + 5f) / 2f);
                Paint(Centered(at, size, size), colour, size / 2f);
                Paint(Centered(at, size - 8f, size - 8f), Paper, (size - 8f) / 2f);
                if (string.IsNullOrEmpty(station.name)) continue;
                // 역 기둥: 땅에서 솟는 빛기둥과 그 꼭대기의 역 이름. 멀리서도 역이 어디인지 눈에 들어오게 한다(사용자 11-19 17:46).
                var height = station.transfer ? SubwayPillarMetres * 1.25f : SubwayPillarMetres;
                var top = ProjectScene(station.latitude, station.longitude, height, Vector2.zero);
                if (top.y > at.y - 26f) top = new Vector2(at.x, at.y - 26f);      // 평면 지도처럼 높이가 안 서는 보기에서도 기둥이 선다
                Line(at, top, Fade(colour, 0.16f), 15f);
                Line(at, top, Fade(Paper, 0.22f), 8f);
                Line(at, top, Fade(colour, 0.9f), 3.2f);
                Line(at, top, Fade(Paper, 0.85f), 1.2f);
                var title = station.name;
                var width = title.Length * 12.5f + 20f;
                var plate = new Rect(top.x - width / 2f, top.y - 24f, width, 22f);
                if (plate.y < 4f || plate.yMax > Field.height - 60) continue;
                Paint(new Rect(plate.x - 1.5f, plate.y - 1.5f, plate.width + 3f, plate.height + 3f), colour, 8);
                Paint(plate, Canvas, 7);
                Text(plate, title, 12, Paper, true, TextAnchor.MiddleCenter);
                if (names && station.lines != null && station.lines.Length > 0)
                {
                    var sub = string.Join(" · ", station.lines);
                    var subWidth = sub.Length * 9.5f + 14f;
                    var subPlate = new Rect(top.x - subWidth / 2f, plate.y - 17f, subWidth, 15f);
                    if (subPlate.y > 4f)
                    {
                        Paint(subPlate, Fade(Canvas, 0.85f), 5);
                        Text(subPlate, sub, 9, Soft, false, TextAnchor.MiddleCenter);
                    }
                }
            }
        }
    }
}
