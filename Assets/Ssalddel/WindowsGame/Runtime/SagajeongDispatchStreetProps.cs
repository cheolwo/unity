using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 동 디오라마 위의 생활 자료 (D5 · 2026-11-23).
    //  1차(「계속 진행」): 생활 시설 참고점(중랑 16동 · 24범주, hongdal-data-facilities.json)마다 그 갈래의 작은 모양.
    //  2차(20:15 「원하는 것만 켜서」 · 「정보를 디오라마에서 잘 알아볼 수 있도록」):
    //    - 자료 창에서 켠 것만 그린다: 길 속도(그 시각 · 10월 평일) · 버스 노선 · 생활 시설 · 비 · 눈.
    //    - 마우스를 올리면 이름표: 시설 갈래 · 길 이름과 속도 · 버스 번호. 드문 갈래(공원 · 시장 · 역 출입구)는 가까우면 늘 이름.
    //    - 굵은 길은 이름과 속도를 길 위에 적는다(같은 이름은 한 번).
    //    - 아래 범례 줄: 지금 화면에 보이는 갈래별 개수(DrawDongLegend, SagajeongDispatchDataControl.cs의 단추 옆).
    //  모양은 화면 표지다(3D 부품은 따로 모델링해 바꿔 끼운다). 크기는 보이게 과장했다(실제 높이 × 2.5).
    //  시설 자리는 「근처에 있다고 보고된 곳」이다(보도 · 출입구에 맞춘 자리가 아니다). 표현뿐이다.
    public sealed partial class SagajeongDispatchView
    {
        private const float PropExaggerate = 2.5f;

        // ── 막힘: 그 길의 평소(하루 중 가장 빠른 시각) 속도에 견준 지금 속도 (2026-11-23 시각 검증) ──
        //  구 · 동 같은 색: 잘 뚫리면 차분한 청록(가늘고 옅게), 평소의 85 % 아래로 떨어질수록 호박색 → 붉게(굵고 밝게).
        private float[] roadFreeSpeed;

        private float RoadJam(int index, int hour, out float speed)
        {
            var link = roadPack.links[index];
            speed = link.v != null && link.v.Length > hour ? link.v[hour] / 10f : -1f;
            if (speed < 0f) return -1f;
            if (roadFreeSpeed == null || roadFreeSpeed.Length != roadPack.links.Length)
            {
                roadFreeSpeed = new float[roadPack.links.Length];
                for (var i = 0; i < roadPack.links.Length; i++)
                {
                    var best = 0;
                    var v = roadPack.links[i].v;
                    if (v != null) foreach (var value in v) best = Mathf.Max(best, value);
                    roadFreeSpeed[i] = best / 10f;
                }
            }
            var free = Mathf.Max(5f, roadFreeSpeed[index]);
            return Mathf.Clamp01((0.85f - speed / free) / 0.5f);
        }

        private static Color JamColour(float jam)
        {
            if (jam < 0f) return Ink(0x8D99A3);
            return jam < 0.5f ? Color.Lerp(Ink(0x5FB8C9), Ink(0xFFB547), jam * 2f) : Color.Lerp(Ink(0xFFB547), Ink(0xFF5A4E), (jam - 0.5f) * 2f);
        }

        // ── 동 디오라마 바닥 그림: 길 속도 · 버스를 메시 좌표의 색 그림 한 장으로 구워 셰이더가 길 바닥에 입힌다 ──
        //  선을 화면 위에 긋지 않으니 건물이 길을 가리고, 길 바닥 자체가 물든다. 시각(시)이 바뀌거나 켜고 끌 때만 다시 굽는다.
        private static readonly int DataMapId = Shader.PropertyToID("_HongdalDataMap");
        private static readonly int DataRectId = Shader.PropertyToID("_HongdalDataRect");
        private static readonly int DataOnId = Shader.PropertyToID("_HongdalData");
        private static readonly int DioramaNightId = Shader.PropertyToID("_HongdalNight");
        private const float DataCellMetres = 2f;
        private const float RoadHalfWidth = 4.2f;      // 큰길 칠의 반폭 (m, 표현값)
        private const float BusHalfWidth = 2.1f;       // 버스 선의 반폭: 칸(2 m)보다 굵어야 끊기지 않는다
        private Texture2D dongDataMap;
        private int dongDataKey = -1;

        private void SyncDongDataMap()
        {
            var on = mapLevel == MapLevel.Dong && (roadLayerOn || busLayerOn) && dioramaRoot != null && dongSpread <= 0.01f;
            if (!on)
            {
                Shader.SetGlobalFloat(DataOnId, 0f);
                return;
            }
            var hour = GameSeconds() / 3600;
            var key = hour * 4 + (roadLayerOn ? 1 : 0) + (busLayerOn ? 2 : 0);
            if (key != dongDataKey)
            {
                dongDataKey = key;
                BakeDongDataMap(hour);
            }
            Shader.SetGlobalFloat(DataOnId, dongDataMap != null ? 1f : 0f);
        }

        private void BakeDongDataMap(int hour)
        {
            var graph = EnsureRouteGraph();
            if (graph == null || graph.Nodes == 0) return;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (var node = 0; node < graph.Nodes; node++)
            {
                minX = Mathf.Min(minX, graph.X[node]); maxX = Mathf.Max(maxX, graph.X[node]);
                minZ = Mathf.Min(minZ, graph.Z[node]); maxZ = Mathf.Max(maxZ, graph.Z[node]);
            }
            const float margin = 40f;
            var x0 = minX - margin;
            var z0 = minZ - margin;
            var spanX = maxX - minX + 2f * margin;
            var spanZ = maxZ - minZ + 2f * margin;
            var cell = Mathf.Max(DataCellMetres, Mathf.Max(spanX, spanZ) / 2048f);
            var width = Mathf.Max(2, Mathf.CeilToInt(spanX / cell));
            var height = Mathf.Max(2, Mathf.CeilToInt(spanZ / cell));
            var pixels = new Color[width * height];
            var root = dioramaRoot.transform;

            void Stamp(float cx, float cz, float radius, Color colour, float alpha, bool over)
            {
                var r = radius / cell;
                var px = (cx - x0) / cell;
                var pz = (cz - z0) / cell;
                var xa = Mathf.Max(1, Mathf.FloorToInt(px - r - 1)); var xb = Mathf.Min(width - 2, Mathf.CeilToInt(px + r + 1));
                var za = Mathf.Max(1, Mathf.FloorToInt(pz - r - 1)); var zb = Mathf.Min(height - 2, Mathf.CeilToInt(pz + r + 1));
                for (var z = za; z <= zb; z++)
                for (var x = xa; x <= xb; x++)
                {
                    var d = Mathf.Sqrt((x + 0.5f - px) * (x + 0.5f - px) + (z + 0.5f - pz) * (z + 0.5f - pz));
                    var a = alpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r * 0.55f, r, d)));
                    if (a <= 0f) continue;
                    var at = z * width + x;
                    var old = pixels[at];
                    if (over)
                        pixels[at] = new Color(Mathf.Lerp(old.r, colour.r, a), Mathf.Lerp(old.g, colour.g, a), Mathf.Lerp(old.b, colour.b, a), Mathf.Max(old.a, a));
                    else if (a > old.a)
                        pixels[at] = new Color(colour.r, colour.g, colour.b, a);
                }
            }

            void Polyline(float[] p, float radius, Color colour, float alpha, bool over)
            {
                var previous = root.InverseTransformPoint(GuWorld(p[0], p[1], 0f));
                for (var i = 2; i + 1 < p.Length; i += 2)
                {
                    var point = root.InverseTransformPoint(GuWorld(p[i], p[i + 1], 0f));
                    var inside = (previous.x > x0 && previous.x < x0 + spanX && previous.z > z0 && previous.z < z0 + spanZ) ||
                                 (point.x > x0 && point.x < x0 + spanX && point.z > z0 && point.z < z0 + spanZ);
                    if (inside)
                    {
                        var length = Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(point.x, point.z));
                        var steps = Mathf.Max(1, Mathf.CeilToInt(length / (cell * 0.7f)));
                        for (var s = 0; s <= steps; s++)
                        {
                            var t = s / (float)steps;
                            Stamp(Mathf.Lerp(previous.x, point.x, t), Mathf.Lerp(previous.z, point.z, t), radius, colour, alpha, over);
                        }
                    }
                    previous = point;
                }
            }

            if (roadLayerOn && EnsureRoads()?.links != null)
                for (var i = 0; i < roadPack.links.Length; i++)
                {
                    var link = roadPack.links[i];
                    if (link?.p == null || link.p.Length < 4) continue;
                    var jam = RoadJam(i, hour, out _);
                    Polyline(link.p, RoadHalfWidth * (0.8f + 0.5f * Mathf.Max(0f, jam)), JamColour(jam), jam < 0f ? 0.22f : 0.38f + 0.52f * jam, false);
                }
            if (busLayerOn && EnsureBus()?.routes != null)
                foreach (var route in busPack.routes)
                {
                    if (route?.parts == null) continue;
                    foreach (var part in route.parts)
                        if (part?.p != null && part.p.Length >= 4) Polyline(part.p, BusHalfWidth, Ink(0x6FA8FF), 0.95f, true);
                }

            if (dongDataMap != null) Destroy(dongDataMap);
            dongDataMap = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "Hongdal data map", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            dongDataMap.SetPixels(pixels);
            dongDataMap.Apply(false, true);
            Shader.SetGlobalTexture(DataMapId, dongDataMap);
            Shader.SetGlobalVector(DataRectId, new Vector4(x0, z0, 1f / (width * cell), 1f / (height * cell)));
            Debug.Log($"Hongdal data map: {width} x {height} cells, {cell:0.0} m · {hour}시 · 길 {roadLayerOn} · 버스 {busLayerOn}");
        }

        private sealed class DongPolyline
        {
            public Vector3[] world;
            public Bounds bounds;
            public int index;        // 길: 링크 차례 · 버스: 노선 차례
        }

        private List<DongPolyline> dongRoadLines, dongBusLines;
        private double dongLineLongitude = double.NaN, dongLineLatitude = double.NaN, dongLineScale = double.NaN;
        private readonly int[] dongPropSeen = new int[9];
        private int dongRoadSeen, dongBusSeen;
        private readonly HashSet<int> dongBusRoutesSeen = new();
        private readonly HashSet<string> dongRoadNamed = new();
        private bool proofHoverAuto;            // 촬영: 화면 가운데에 가장 가까운 시설에 이름표를 띄운다

        private Vector3 GuWorld(float x, float y, float height) =>
            new((float)((126.94 + x / 88224.0 - centerLongitude) * longitudeScale), height,
                (float)((37.523 + y / 111320.0 - centerLatitude) * 111200));

        private bool WorldScreen(Vector3 world, out Vector2 point)
        {
            var screen = dioramaCamera.WorldToScreenPoint(world);
            point = Vector2.zero;
            if (screen.z <= 0f) return false;
            var left = (Screen.width - Width * dioramaGuiScale) * 0.5f;
            var top = (Screen.height - Height * dioramaGuiScale) * 0.5f;
            point = new Vector2((screen.x - left) / dioramaGuiScale - Field.x, (Screen.height - screen.y - top) / dioramaGuiScale - Field.y);
            return true;
        }

        private bool WorldShown(Vector3 world) =>
            SubwayShown(centerLatitude + world.z / 111200.0, centerLongitude + world.x / longitudeScale);

        private RoadPack EnsureRoads()
        {
            if (!roadTried) { roadTried = true; roadPack = LoadPack<RoadPack>("hongdal-data-roads"); Debug.Log($"Hongdal data: 길 {roadPack?.links?.Length ?? 0}"); }
            return roadPack;
        }

        private BusPack EnsureBus()
        {
            if (!busTried) { busTried = true; busPack = LoadPack<BusPack>("hongdal-data-bus"); Debug.Log($"Hongdal data: 버스 {busPack?.routes?.Length ?? 0}노선"); }
            return busPack;
        }

        // 길 · 버스 선을 세계 좌표로 한 번 바꿔 둔다. 디오라마 가운데가 바뀌면 다시.
        private void EnsureDongLines()
        {
            if (dongLineLongitude == centerLongitude && dongLineLatitude == centerLatitude && dongLineScale == longitudeScale) return;
            dongLineLongitude = centerLongitude;
            dongLineLatitude = centerLatitude;
            dongLineScale = longitudeScale;
            dongRoadLines = new List<DongPolyline>();
            dongBusLines = new List<DongPolyline>();
            var roads = EnsureRoads();
            if (roads?.links != null)
                for (var i = 0; i < roads.links.Length; i++)
                {
                    var p = roads.links[i]?.p;
                    if (p != null && p.Length >= 4) dongRoadLines.Add(MakeDongLine(p, 0.8f, i));
                }
            var bus = EnsureBus();
            if (bus?.routes != null)
                for (var r = 0; r < bus.routes.Length; r++)
                {
                    if (bus.routes[r]?.parts == null) continue;
                    foreach (var part in bus.routes[r].parts)
                        if (part?.p != null && part.p.Length >= 4) dongBusLines.Add(MakeDongLine(part.p, 1.6f, r));
                }
        }

        private DongPolyline MakeDongLine(float[] p, float height, int index)
        {
            var world = new Vector3[p.Length / 2];
            for (var i = 0; i < world.Length; i++) world[i] = GuWorld(p[i * 2], p[i * 2 + 1], height);
            var bounds = new Bounds(world[0], Vector3.one);
            foreach (var w in world) bounds.Encapsulate(w);
            bounds.Expand(new Vector3(0f, 6f, 0f));
            return new DongPolyline { world = world, bounds = bounds, index = index };
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 0.01f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        // DrawDongModuleBoundaries가 지하철 다음에 부른다(Repaint).
        private void DrawDongDataLayers()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (dioramaCamera == null || dongSpread > 0.01f)
            {
                dongHoverFacility = -1;      // 모듈이 모이는 동안은 누를 시설이 없다(SagajeongDispatchDongFocus.cs)
                return;
            }
            System.Array.Clear(dongPropSeen, 0, dongPropSeen.Length);
            dongRoadSeen = 0;
            dongBusSeen = 0;
            dongBusRoutesSeen.Clear();
            dongRoadNamed.Clear();
            var now = Time.unscaledTime;
            var rise = DongDataRise();               // 입장 뒤 자료가 떠오름(2026-11-24 16:52 · SagajeongDispatchDongFocus.cs)
            var hoverFacility = -1;                  // 마우스 아래 시설(누르면 집중)
            string hoverFacilityText = null;
            var proofFocus = -1;
            var proofFocusBest = float.MaxValue;
            dongFocusOnScreen = false;
            var mouse = Event.current.mousePosition;
            var mouseFree = !MapOverlayCovers(mouse + Field.position) && new Rect(0, 0, Field.width, Field.height).Contains(mouse);
            var hoverText = (string)null;
            var hoverSub = (string)null;
            var hoverIcon = (string)null;
            var hoverBest = 12f;
            var view = new Rect(-60f, -60f, Field.width + 120f, Field.height + 120f);
            var planes = GeometryUtility.CalculateFrustumPlanes(dioramaCamera);
            if (roadLayerOn || busLayerOn) EnsureDongLines();

            // 길: 그 시각의 10월 평일 평균 속도. 붉을수록 느리다. 도형이 확인 안 된 링크는 점선.
            if (roadLayerOn && roadPack?.links != null)
            {
                var hour = GameSeconds() / 3600;
                var labels = 0;
                foreach (var line in dongRoadLines)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, line.bounds)) continue;
                    var link = roadPack.links[line.index];
                    if (link.v == null || link.v.Length < 24) continue;
                    var jam = RoadJam(line.index, hour, out var kmh);
                    var speed = kmh < 0f ? -1 : Mathf.RoundToInt(kmh * 10f);
                    var drawn = false;
                    var length = 0f;
                    var mid = Vector2.zero;
                    var midLength = 0f;
                    var hasPrevious = WorldScreen(line.world[0], out var previous) && WorldShown(line.world[0]);
                    for (var i = 1; i < line.world.Length; i++)
                    {
                        var ok = WorldScreen(line.world[i], out var point) && WorldShown(line.world[i]);
                        if (ok && hasPrevious && (view.Contains(previous) || view.Contains(point)))
                        {
                            drawn = true;          // 선은 바닥 그림(BakeDongDataMap)이 길 위에 칠한다
                            var piece = Vector2.Distance(previous, point);
                            if (piece > midLength) { midLength = piece; mid = (previous + point) * 0.5f; }
                            length += piece;
                            if (mouseFree)
                            {
                                var distance = SegmentDistance(mouse, previous, point);
                                if (distance < hoverBest - 4f)
                                {
                                    hoverBest = distance + 4f;      // 시설 모양이 길보다 먼저 잡힌다
                                    hoverText = $"{link.n} · {(speed < 0 ? "속도 자료 없음" : $"{speed / 10f:0} km/h")}" + (jam >= 0.5f ? " · 막힘" : jam >= 0.2f ? " · 더딤" : "");
                                    hoverSub = $"10월 평일 {hour:00}시 평균 · 평소(가장 빠른 시각) {roadFreeSpeed[line.index]:0} km/h · 서울시 교통정보" + (link.ok == 1 ? "" : " · 도형 미확인");
                                }
                            }
                        }
                        previous = point;
                        hasPrevious = ok;
                    }
                    if (!drawn) continue;
                    dongRoadSeen++;
                    // 길 이름표: 화면에서 길게 보이는 길만, 같은 이름은 한 번, 열두 개까지
                    if (labels < 12 && length > 150f && !string.IsNullOrEmpty(link.n) && dongRoadNamed.Add(link.n) &&
                        mid.x > 40f && mid.x < Field.width - 40f && mid.y > 40f && mid.y < Field.height - 40f)
                    {
                        labels++;
                        var text = speed < 0 ? link.n : $"{link.n} {speed / 10f:0}";
                        var plate = new Rect(mid.x - 4f - text.Length * 5.2f, mid.y - 9f, text.Length * 10.4f + 8f, 18f);
                        Paint(plate, Fade(Canvas, 0.82f), 5);
                        Text(plate, text, 10, speed < 0 ? Soft : JamColour(jam), true, TextAnchor.MiddleCenter);
                    }
                }
            }

            // 버스: 중랑을 지나는 노선의 확인된 선 조각. 끊긴 곳은 끊긴 채로.
            if (busLayerOn && busPack?.routes != null)
            {
                foreach (var line in dongBusLines)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, line.bounds)) continue;
                    var hasPrevious = WorldScreen(line.world[0], out var previous) && WorldShown(line.world[0]);
                    var drawn = false;
                    for (var i = 1; i < line.world.Length; i++)
                    {
                        var ok = WorldScreen(line.world[i], out var point) && WorldShown(line.world[i]);
                        if (ok && hasPrevious && (view.Contains(previous) || view.Contains(point)))
                        {
                            drawn = true;          // 선은 바닥 그림이 칠한다
                            if (mouseFree)
                            {
                                var distance = SegmentDistance(mouse, previous, point);
                                if (distance < hoverBest - 2f)
                                {
                                    hoverBest = distance + 2f;
                                    hoverText = $"버스 {busPack.routes[line.index].n}";
                                    hoverSub = "확인된 선 조각만 · 서울 버스 노선 자료";
                                }
                            }
                        }
                        previous = point;
                        hasPrevious = ok;
                    }
                    if (drawn && dongBusRoutesSeen.Add(line.index)) dongBusSeen++;
                }
            }

            // 생활 시설 거리 소품
            var pack = facilityGroupMask != 0 ? EnsureFacilities() : null;
            if (pack?.k != null)
            {
                var night = NightFactor();
                var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
                var proofBest = float.MaxValue;
                PrepareDongFocus(pack);
                if (dongFocusIndex >= 0) Paint(new Rect(0f, 0f, Field.width, Field.height), Fade(Canvas, 0.32f * rise), 0);   // 집중: 둘레를 한 겹 어둡게(이어진 것만 밝게 다시 그린다)
                for (var i = 0; i < pack.k.Length; i++)
                {
                    if (!FacilityKeyShown(pack.k[i])) continue;
                    var foot3 = GuWorld(pack.x[i], pack.y[i], 0f);
                    if (!WorldShown(foot3)) continue;
                    if (!WorldScreen(foot3, out var foot) || !view.Contains(foot)) continue;
                    if (!WorldScreen(foot3 + Vector3.up * PropExaggerate, out var metreUp)) continue;
                    var upStep = metreUp - foot;                                   // 과장한 1 m가 화면에서 어느 쪽으로 몇 점인가(원근 기울기 포함)
                    var ppm = upStep.magnitude;
                    if (ppm < 0.9f) continue;                                    // 멀면 그리지 않는다
                    var fade = Mathf.InverseLerp(0.9f, 1.8f, ppm) * rise;
                    var key = pack.keys[pack.k[i]];
                    var group = FacilityGroup(key);
                    var label = pack.labels != null && pack.k[i] < pack.labels.Length ? pack.labels[pack.k[i]] : key;
                    var mark = DongFocusMark(i);           // 집중: 1 그것 · 2 이어진 것 · 3 옅게 · 0 집중 없음
                    DrawProp(key, foot, upStep, ppm, mark == 3 ? fade * 0.25f : fade, night, now, i);
                    if (mark == 1) NoteDongFocusScreen(foot, upStep, label, group);
                    else if (mark == 2) DrawDongFocusRelated(foot, group, fade);
                    dongPropSeen[group]++;
                    // 드문 갈래는 가까우면 늘 이름
                    if ((group == 5 || group == 6 || group == 7) && ppm >= 1.6f)
                    {
                        var plate = new Rect(foot.x - 40f, foot.y + 2f, 80f, 14f);
                        Text(plate, label, 9, Fade(FacilityGroupColour(group), 0.95f * fade), true, TextAnchor.UpperCenter);
                    }
                    var glyph = foot + upStep * 1.5f;
                    if (mouseFree)
                    {
                        var distance = Mathf.Min(Vector2.Distance(mouse, glyph), Vector2.Distance(mouse, foot));
                        if (distance < hoverBest)
                        {
                            hoverBest = distance;
                            hoverText = label;
                            hoverFacility = i;
                            hoverFacilityText = label;
                            hoverIcon = FacilityIconKey(key);
                            hoverSub = $"{FacilityGroupNames[group]} 갈래 · 근처에 있다고 보고된 곳 · 중랑 16동 참고점";
                        }
                    }
                    if (dongFocusProofGroup >= 0 && group == dongFocusProofGroup)
                    {
                        var distance = Vector2.Distance(centre, foot) + (DongSpotClear(foot, upStep) ? 0f : 5000f);   // 판에 가린 것은 뒤로
                        if (distance < proofFocusBest)
                        {
                            proofFocusBest = distance;
                            proofFocus = i;
                        }
                    }
                    if (proofHoverAuto && group == 1)
                    {
                        var distance = Vector2.Distance(centre, foot);
                        if (distance < proofBest)
                        {
                            proofBest = distance;
                            mouse = glyph;
                            hoverText = label;
                            hoverIcon = FacilityIconKey(key);
                            hoverSub = $"{FacilityGroupNames[group]} 갈래 · 근처에 있다고 보고된 곳 · 중랑 16동 참고점";
                        }
                    }
                }
            }

            // 위치 확인 표식(횡단보도 후보 · 따릉이 대여소, SagajeongDispatchMapIcons.cs)
            DrawDongGroundMarks(view, mouse, mouseFree, dongPropSeen, ref hoverBest, ref hoverText, ref hoverSub, ref hoverIcon);
            if (hoverText != hoverFacilityText) hoverFacility = -1;      // 표식 · 길이 더 가까우면 시설은 아니다
            dongHoverFacility = hoverFacility;
            if (proofFocus >= 0) dongFocusIndex = proofFocus;

            if (weatherOn && !WeatherParticlesActive) DrawWeatherLayer(now, false, true);      // 알갱이가 내리면 화면 위 그림은 쉰다

            DrawDongFocusOverlay(view, now);       // 집중: 이어진 버스 · 길 · 빛 기둥 · 카드(SagajeongDispatchDongFocus.cs)
            if (hoverText != null && !(hoverFacility >= 0 && hoverFacility == dongFocusIndex)) DrawDongHover(mouse, hoverText, hoverSub + (hoverFacility >= 0 ? " · 누르면 집중" : string.Empty), hoverIcon);
        }

        private void DrawDongHover(Vector2 at, string text, string sub, string icon = null)
        {
            var lead = icon != null && MapIcon(icon) != null ? 20f : 0f;            // 시설 이름표엔 갈래 아이콘(SagajeongDispatchMapIcons.cs)
            var width = Mathf.Max(text.Length * 11f + lead, sub.Length * 8.6f) + 20f;
            var box = new Rect(at.x + 14f, at.y - 44f, width, 38f);
            if (box.xMax > Field.width - 8f) box.x = at.x - 14f - width;
            if (box.y < 8f) box.y = at.y + 14f;
            Disc(at, 3.5f, Fade(Gold, 0.9f));
            Paint(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f), Fade(Gold, 0.7f), 7);
            Paint(box, Fade(Canvas, 0.95f), 6);
            if (lead > 0f) DrawMapIcon(new Rect(box.x + 9f, box.y + 4f, 16f, 16f), icon, Gold);
            Text(new Rect(box.x + 10f + lead, box.y + 3f, box.width - 14f - lead, 18f), text, 12, Paper, true);
            Text(new Rect(box.x + 10f, box.y + 20f, box.width - 14f, 14f), sub, 9, Soft);
        }

        // 동 디오라마 아래 범례 줄: 지금 화면에 보이는 것. OnGUI 맨 위(필드 밖 좌표)에서 그린다.
        private void DrawDongLegend()
        {
            var strip = DongLegendScreen;
            Paint(strip, Fade(Canvas, 0.88f), 8);
            var x = strip.x + 10f;
            var y = strip.y;
            void Chip(Color colour, string text, string icon = null)
            {
                var width = text.Length * 9.6f + 18f + (icon != null ? 4f : 0f);
                if (x + width > strip.xMax - 6f) return;
                if (icon == null || !DrawMapIcon(new Rect(x - 1f, y + strip.height * 0.5f - 7f, 14f, 14f), icon, colour))
                    Disc(new Vector2(x + 5f, y + strip.height * 0.5f), 4f, colour);
                else x += 4f;
                Text(new Rect(x + 13f, y, width - 13f, strip.height), text, 10, Paper);
                x += width + 4f;
            }
            if (seasonOn)
            {
                var env = EnvNow();      // 철 칩: 날짜와 지금의 나무 (SagajeongDispatchEnvironment.cs)
                Chip(SeasonChipColour(env), (previewDate.HasValue ? "미리 · " : "") + $"{env.Date.Month}/{env.Date.Day} {SeasonLine(env)}");
                Chip(Ink(0xBFD6E6), "바람 " + WindLine(env));      // 평년 풍속 × 날마다(표현) · 방향 규칙
            }
            if (roadLayerOn && dongRoadSeen > 0) Chip(JamColour(0.8f), $"길 {GameSeconds() / 3600}시 · 붉을수록 막힘");
            if (busLayerOn && dongBusSeen > 0) Chip(Ink(0x4C8DF0), $"버스 {dongBusSeen}노선");
            for (var g = 0; g < dongPropSeen.Length; g++)
                if (dongPropSeen[g] > 0) Chip(FacilityGroupColour(g), $"{FacilityGroupNames[g]} {dongPropSeen[g]}", FacilityGroupIconKey(g));
            if (weatherOn && RainNow() > 0f) Chip(Ink(0x9CC4E4), "비 오는 중");
            if (weatherOn && snowPlaying) Chip(Ink(0xE6F2FB), "눈 오는 날");
            if (x == strip.x + 10f)
            {
                var anything = roadLayerOn || busLayerOn || facilityGroupMask != 0 || weatherOn;
                Text(new Rect(x, y, strip.width - 20f, strip.height),
                    anything ? "켠 자료가 이 화면에는 없습니다 · 생활 시설은 중랑 16동만" : "자료 창에서 볼 것을 골라 켜세요 · 지금은 " + (subwayLinesOn ? "지하철 노선만" : "아무 자료도 켜지 않음"),
                    10, Soft, false, TextAnchor.MiddleLeft);
            }
        }

        private void DrawProp(string key, Vector2 foot, Vector2 upStep, float ppm, float fade, float night, float now, int seed)
        {
            Color F(Color colour, float alpha) => Fade(colour, alpha * fade);
            Vector2 Up(float metres) => foot + upStep * metres;
            var pole = Ink(0x9AA7B2);
            switch (key)
            {
                case "pedestrian_signal":
                {
                    var head = Up(3.2f);
                    Line(foot, head, F(pole, 0.9f), 1.2f);
                    var green = Mathf.Repeat(now / 30f + SubwayHash(seed, 3), 1f) < 0.4f;        // 녹색 40 % · 적색 60 %(표현값)
                    var box = new Rect(head.x - 2.2f, head.y - 4.5f, 4.4f, 4.5f);
                    Paint(box, F(Canvas, 0.95f), 1);
                    Paint(new Rect(box.x + 0.8f, box.y + (green ? 2.4f : 0.6f), 2.8f, 1.6f), F(green ? Ink(0x7CDB72) : Ink(0xFF6B5B), 1f), 1);
                    if (night > 0.3f) Disc(new Vector2(head.x, box.y + 2f), 4f, F(green ? Ink(0x7CDB72) : Ink(0xFF6B5B), 0.25f * night));
                    break;
                }
                case "bus_stop":
                {
                    var w = Mathf.Max(6f, 3.4f * ppm);
                    var roof = Up(2.6f);
                    Line(new Vector2(foot.x - w / 2f, foot.y), new Vector2(roof.x - w / 2f, roof.y), F(pole, 0.9f), 1.1f);
                    Line(new Vector2(foot.x + w / 2f, foot.y), new Vector2(roof.x + w / 2f, roof.y), F(pole, 0.9f), 1.1f);
                    Paint(new Rect(roof.x - w / 2f - 1f, roof.y - 2f, w + 2f, 2.6f), F(Ink(0x6FE3C4), 0.95f), 1);
                    Paint(new Rect(roof.x - w / 2f, roof.y + 0.8f, w, foot.y - roof.y - 1.5f), F(Ink(0x6FE3C4), 0.12f), 0);
                    break;
                }
                case "heat_shade":
                {
                    var top = Up(3f);
                    Line(foot, top, F(pole, 0.9f), 1.1f);
                    var open = night < 0.5f;                                                      // 그늘막은 밤에 접는다
                    var span = open ? Mathf.Max(6f, 3.2f * ppm) : 2.4f;
                    Paint(new Rect(top.x - span / 2f, top.y - (open ? 2f : 5f), span, open ? 2.6f : 6f), F(Ink(0x6FA8FF), 0.9f), open ? 2 : 1);
                    break;
                }
                case "heat_shelter":
                case "drinking_water":
                case "public_toilet":
                {
                    var top = Up(2f);
                    Line(foot, top, F(pole, 0.8f), 1f);
                    Disc(top, 2.4f, F(Ink(0x6FA8FF), 0.95f));
                    break;
                }
                case "bike_share":
                case "bicycle_parking":
                {
                    var w = Mathf.Max(6f, 3f * ppm);
                    var colour = key == "bike_share" ? Ink(0x7CDB72) : Ink(0xA9B87A);
                    Line(new Vector2(foot.x - w / 2f, foot.y - 1.5f), new Vector2(foot.x + w / 2f, foot.y - 1.5f), F(colour, 0.9f), 1.4f);
                    for (var b = 0; b < 3; b++)
                    {
                        var c = new Vector2(foot.x - w / 2f + w * (b + 0.5f) / 3f, foot.y - 2.5f);
                        Hoop(c, 1.6f, F(colour, 0.9f), 0.8f);
                    }
                    break;
                }
                case "park":
                case "protected_tree":
                {
                    var top = Up(key == "protected_tree" ? 7f : 4.5f);
                    Line(foot, top, F(Ink(0x7A5C44), 0.9f), 1.6f);
                    Disc(top, Mathf.Max(3f, 1.6f * ppm), F(Ink(0x469664), 0.9f));
                    Disc(top + new Vector2(-1f, -1f), Mathf.Max(1.5f, 0.8f * ppm), F(Ink(0x6FCF97), 0.7f));
                    break;
                }
                case "seoul_market_inventory":
                case "designated_specialty_street":
                case "tourism_site":
                {
                    var top = Up(4f);
                    Line(foot, top, F(pole, 0.9f), 1.1f);
                    var wave = Mathf.Sin(now * 2f + seed) * 0.6f;
                    Paint(new Rect(top.x, top.y + wave, Mathf.Max(5f, 2f * ppm), 3.4f), F(Ink(0xFFD395), 0.95f), 1);
                    break;
                }
                case "metro_unnumbered_exit":
                case "station_elevator":
                {
                    var w = Mathf.Max(5f, 2.4f * ppm);
                    var roof = Up(2.4f);
                    Paint(new Rect(foot.x - w / 2f, roof.y, w, foot.y - roof.y), F(Ink(0x3A4B59), 0.95f), 1);
                    Paint(new Rect(foot.x - w / 2f - 1f, roof.y - 1.5f, w + 2f, 2f), F(Ink(0xC9D08A), 0.95f), 1);
                    break;
                }
                case "smart_pole":
                {
                    var top = Up(8f);
                    Line(foot, top, F(Ink(0xDDE3E8), 0.9f), 1.4f);
                    Disc(top, 2f, F(night > 0.3f ? Ink(0xFFF4D6) : Ink(0xDDE3E8), 0.95f));
                    if (night > 0.3f) Disc(top, 7f, F(Ink(0xFFD395), 0.22f * night));
                    break;
                }
                case "pedestrian_entrance":
                    Paint(new Rect(foot.x - 1.6f, foot.y - 3.2f, 3.2f, 3.2f), F(Ink(0xABC1CB), 0.8f), 1);
                    break;
                default:
                {
                    var top = Up(1.2f);
                    Paint(new Rect(foot.x - 2f, top.y, 4f, foot.y - top.y), F(Ink(0x8D99A3), 0.9f), 1);
                    break;
                }
            }
        }
    }
}
