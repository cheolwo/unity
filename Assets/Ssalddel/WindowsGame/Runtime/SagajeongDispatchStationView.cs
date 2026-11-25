using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 역 중심 보기 — 화면 층 (2026-11-14). 사용자 방향: 동 단위로도, 역을 중심으로도 사업을 생각할 수 있다.
    // 사용자 스타일에 맞게 고르는 것이니 우선 옵션으로 둔다. 설정 칸 「동 보기」의 네 번째 칸이다.
    // 같은 날 뒤에: 공공 자료는 행정동·법정동으로 돼 있으니 지도를 굽는 것은 그 자료로 하되, 게임에서 관점을 잡을 때는 역 중심으로 보여 준다.
    // 그래서 처음 보기가 역 중심이 됐다(SagajeongDispatchDongSolo.cs의 PrimeDongSolo). 「이 동만」·붙여·띄워 보기는 설정 칸에 그대로 있다.
    //  - 읽어 둔 행정동 모듈을 붙여 놓고 그 위에 둥근 창(역 중심, 반경 600 m)을 씌운다. 창 밖은 바탕색으로 흐려지다 끊긴다(셰이더의 _Window).
    //  - 카메라는 역을 가운데 두고 판에 맞춘다. 판의 둘레는 민트 선, 동 경계는 판 안쪽만 옅게 그린다.
    //  - 깃발·수요·제휴는 행정동 그대로다. 판 안에 걸친 동마다 이름표가 서고, 이름표를 누르면 그 동의 사업 칸이 판 위에 뜬다.
    //    동의 밝기(영역 단계·운영의 깊이)도 그대로라 한 판 안에서 내 동과 아닌 동이 갈린다.
    //  - 판 밖의 표식(매장·기사·주문·관리 건물 점)과 건물 고르기는 쉰다.
    //    판에 없는 동으로 가면 그 동이 든 다른 역의 판으로 넘기고, 어느 판에도 없을 때만 그 동만 보기로 넘어간다.
    //  - 역 자리는 임시 값이다(역중심Policy). 자료가 덮지 못한 넓이가 있으면 역 이름 칸에 비율을 적는다.
    // 보기만 바꾼다. 시계·주문·돈·재고와 영역 규칙을 읽거나 바꾸지 않는다. 검증 촬영(--sagajeong-…)에는 행정동 모듈이 없어 해당하지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly int DioramaWindowId = Shader.PropertyToID("_Window");
        private static readonly int StationTierId = Shader.PropertyToID("_HongdalTier");      // 역 세 겹(전역 값)
        private static readonly Color StationLine = Ink(0x8C9828);       // 역 표식의 색(올리브, 표현값)
        private const int StationRingPoints = 64;
        private const int StationMinBuildings = 20;       // 판 안에 그 동의 건물이 이보다 적으면 이름표를 세우지 않는다
        private const float StationTitleRoom = 48f;       // 판 아래 역 이름 칸이 설 자리(px)
        private const float StationCardRoom = 26f;        // 동 사업 칸이 판의 위쪽 끝을 덮지 않도록 더 남기는 자리(px)

        private sealed class StationAnchor
        {
            public double Latitude;
            public double Longitude;
            public int Buildings;
        }

        private int stationIndex;                 // 지금 보는 역
        private string stationCardRegion;         // 판 위의 동 사업 칸이 보여 줄 동
        // 판 위의 동 사업 칸을 펼쳤는가 (2026-11-17 사용자: 처음 화면의 위 카드는 굳이 없어도 된다).
        // 처음 보기 · 역 넘기기에서는 접어 둔다. 다른 곳에서 그 동을 찾아 넘어올 때만 편다. 고른 동(밝기 · 사업 창)은 그대로다.
        private bool stationCardShown;
        // 역 판의 동 이름표로 그 동에 들어왔을 때 돌아갈 역(없으면 -1). 이 동만 보기 아래에 「역 판으로」 단추가 선다.
        private int stationReturnIndex = -1;
        private readonly Dictionary<string, StationAnchor> stationAnchors = new(StringComparer.Ordinal);     // 지역 ID → 판 안에서 그 동의 이름표 자리
        private int stationLayoutStamp = int.MinValue;
        private double stationCoverage = 1;
        private Vector4 stationWindowApplied = new(float.NaN, 0f, 0f, 0f);
        private Material stationWindowBase;
        private Material stationWindowManaged;

        private bool StationView => dongView == DongView.Station && DongModulesActive;

        private 역자리 Station => 역중심Policy.Stations[Mathf.Clamp(stationIndex, 0, 역중심Policy.Stations.Count - 1)];

        // 역에서 그 점까지의 거리(m). 메시를 구운 공통 원점 기준으로 잰다.
        private double StationDistance(double latitude, double longitude)
        {
            var station = Station;
            var (east, north) = DongLocal(latitude, longitude);
            var (stationEast, stationNorth) = DongLocal(station.Latitude, station.Longitude);
            return Math.Sqrt((east - stationEast) * (east - stationEast) + (north - stationNorth) * (north - stationNorth));
        }

        // 그 점의 표식을 그리는가(판 안, 또는 흐려지는 띠의 절반까지).
        private bool StationShows(double latitude, double longitude) =>
            역중심Policy.Shows(StationDistance(latitude, longitude), 역중심Policy.Radius, 역중심Policy.Fade);

        // 그 건물을 고를 수 있는가. 색인의 외곽은 공통 원점 기준 미터다.
        private bool StationShowsBuilding(BuildingShape shape)
        {
            var station = Station;
            var (stationEast, stationNorth) = DongLocal(station.Latitude, station.Longitude);
            var centre = (shape.Min + shape.Max) * 0.5f;
            var dx = centre.x - stationEast;
            var dz = centre.y - stationNorth;
            return 역중심Policy.Shows(Math.Sqrt(dx * dx + dz * dz), 역중심Policy.Radius, 역중심Policy.Fade);
        }

        // 판 안에 든 동과 그 이름표 자리(그 동 건물들의 가운데), 자료가 덮는 비율. 역이나 보이는 동이 바뀌면 다시 센다.
        private void EnsureStationLayout()
        {
            LoadBuildingIndex();
            var stamp = stationIndex * 7919 + buildingShapes.Length * 31 + dongHidden.Count;
            if (stamp == stationLayoutStamp) return;
            stationLayoutStamp = stamp;
            stationAnchors.Clear();
            var station = Station;
            var (stationEast, stationNorth) = DongLocal(station.Latitude, station.Longitude);
            var sums = new Dictionary<string, (double East, double North, int Count)>(StringComparer.Ordinal);
            foreach (var shape in buildingShapes)
            {
                var region = shape.Record.region;
                if (string.IsNullOrEmpty(region) || shape.Record.fill) continue;
                var centre = (shape.Min + shape.Max) * 0.5f;
                var dx = centre.x - stationEast;
                var dz = centre.y - stationNorth;
                if (dx * dx + dz * dz > 역중심Policy.Radius * 역중심Policy.Radius) continue;
                sums.TryGetValue(region, out var sum);
                sums[region] = (sum.East + centre.x, sum.North + centre.y, sum.Count + 1);
            }
            foreach (var pair in sums)
            {
                var module = DongModuleById(pair.Key);
                if (module == null || !DongVisible(module) || pair.Value.Count < StationMinBuildings) continue;
                stationAnchors[pair.Key] = new StationAnchor
                {
                    Latitude = dongManifest.originLatitude + pair.Value.North / pair.Value.Count / dongManifest.metresPerDegreeLatitude,
                    Longitude = dongManifest.originLongitude + pair.Value.East / pair.Value.Count / dongManifest.metresPerDegreeLongitude,
                    Buildings = pair.Value.Count,
                };
            }
            stationCoverage = dongGrid == null ? 0 : 역중심Policy.Coverage(stationEast, stationNorth, 역중심Policy.Radius,
                (x, z) => dongGrid.At(x, z) >= 0);
            if (stationCardRegion == null || !stationAnchors.ContainsKey(stationCardRegion)) stationCardRegion = StationDefaultDong();
        }

        // 판 위의 사업 칸이 처음 보여 줄 동: 시작 동이 판 안에 있으면 그 동, 아니면 역이 든 동, 그것도 아니면 건물이 가장 많은 동.
        private string StationDefaultDong()
        {
            if (stationAnchors.Count == 0) return null;
            var home = dongTerritory?.Dongs.FirstOrDefault(zone => zone.IsHome);
            if (home != null && stationAnchors.ContainsKey(HjdRegionPrefix + home.Code)) return HjdRegionPrefix + home.Code;
            var at = DongModuleAt(Station.Latitude, Station.Longitude)?.Entry.regionId;
            if (at != null && stationAnchors.ContainsKey(at)) return at;
            return stationAnchors.OrderByDescending(pair => pair.Value.Buildings).First().Key;
        }

        // 그 동의 건물이 가장 많이 든 역. 어느 역의 판에도 충분히 들지 않으면 -1.
        private int StationFor(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || dongManifest == null) return -1;
            LoadBuildingIndex();
            var stations = 역중심Policy.Stations;
            var counts = new int[stations.Count];
            var centres = new (double East, double North)[stations.Count];
            for (var i = 0; i < stations.Count; i++) centres[i] = DongLocal(stations[i].Latitude, stations[i].Longitude);
            var reach = 역중심Policy.Radius * 역중심Policy.Radius;
            foreach (var shape in buildingShapes)
            {
                if (shape.Record.region != regionId || shape.Record.fill) continue;
                var centre = (shape.Min + shape.Max) * 0.5f;
                for (var i = 0; i < counts.Length; i++)
                {
                    var dx = centre.x - centres[i].East;
                    var dz = centre.y - centres[i].North;
                    if (dx * dx + dz * dz <= reach) counts[i]++;
                }
            }
            var best = -1;
            for (var i = 0; i < counts.Length; i++)
                if (counts[i] >= StationMinBuildings && (best < 0 || counts[i] > counts[best])) best = i;
            return best;
        }

        // 그 동이 든 다른 역의 판으로 넘긴다. 넘겼으면 true.
        private bool StepStationTo(string regionId)
        {
            var other = StationFor(regionId);
            if (other < 0 || other == stationIndex) return false;
            stationIndex = other;
            stationCardRegion = regionId;
            stationCardShown = true;                  // 그 동을 찾아 넘어왔으니 사업 칸을 편다
            stationLayoutStamp = int.MinValue;
            return FocusStation();
        }

        // 그 동이 지금 역 판 안에 있는가.
        private bool StationHas(string regionId)
        {
            EnsureStationLayout();
            return !string.IsNullOrEmpty(regionId) && stationAnchors.ContainsKey(regionId);
        }

        // 판 위의 사업 칸을 그 동으로 바꾼다. 지도는 옮기지 않는다.
        private void SelectStationDong(string regionId, bool showCard = true)
        {
            stationCardRegion = regionId;
            if (showCard) stationCardShown = true;
            focusDongRegion = regionId;
            ApplyDongDim();
            Debug.Log("Hongdal station view: dong " + regionId);
        }

        // 역을 넘긴다.
        private void StepStation(int step)
        {
            관찰지도수동이동();
            stationIndex = 역중심Policy.Step(stationIndex, step, 역중심Policy.Stations.Count);
            stationCardRegion = null;
            stationCardShown = false;
            stationLayoutStamp = int.MinValue;
            FocusStation();
        }

        // 역을 가운데 두고 판이 화면에 다 들어오게 지도를 옮긴다. 동 사업 칸과 역 이름 칸이 설 자리를 남긴다.
        private bool FocusStation()
        {
            if (!StationView || mapLevel != MapLevel.Dong || dioramaRoot == null || metresPerPixel <= 0) return false;
            EnsureStationLayout();
            var station = Station;
            var ring = 역중심Policy.Ring((station.Longitude - centerLongitude) * longitudeScale, (station.Latitude - centerLatitude) * 111200,
                역중심Policy.Radius * 1.03, StationRingPoints);
            var area = CycleCardVisible ? DongFocusArea : DongFitArea;
            if (DongFlagsActive)
            {
                var room = DongTradeCardRoom + StationCardRoom + (DongTradeCardLift > 0 ? DongTradeCardLift + 30f : 0f);
                area = new Rect(area.x, area.y + room, area.width, area.height - room - StationTitleRoom);
            }
            var (metres, east, north) = 행정동모듈Policy.Fit(ring, area.width, area.height, DongFitMarginNow, dioramaOn,
                BaseLens.YawCos, BaseLens.YawSin, BaseLens.PitchSin);      // 화면에 맞추는 것은 멀리서 보는 일이라 바탕 시점으로 잰다
            var zoom = Mathf.Clamp((float)(metresPerPixel / metres), DongMinZoom, MaxMapZoom);
            var perPixel = (float)metresPerPixel / zoom;
            var centre = Field.size * 0.5f;
            mapZoom = zoom;
            SyncCloseLens();
            mapOffset = new Vector2(centre.x * (1f - zoom) - (float)east / perPixel, centre.y * (1f - zoom) + (float)north / perPixel);
            mapOffset += FlatDeltaFor(dioramaOn, area.center - Field.position - centre);
            selectedBuilding = null;
            dongZoomFloorVisible = false;
            localTextureDirty = true;
            focusDongRegion = stationCardRegion;
            ApplyDongDim();
            Debug.Log($"Hongdal station view: {station.Name}, radius {역중심Policy.Radius:0} m, dongs {stationAnchors.Count}, " +
                $"coverage {stationCoverage:0.00}, zoom {zoom:0.000}");
            return true;
        }

        // LateUpdate가 부른다. 둥근 창을 세 재질(바탕·관리 건물·그림자)에 알린다. 역 중심 보기가 아니면 창을 없앤다.
        private void SyncStationWindow()
        {
            if (dioramaMaterial == null) return;
            Shader.SetGlobalVector(StationTierId, StationView
                ? new Vector4((float)역중심Policy.Core, (float)역중심Policy.Area, 1f, (float)역중심Policy.FarHeight) : Vector4.zero);
            var window = Vector4.zero;
            if (StationView)
            {
                var station = Station;
                var (east, north) = DongLocal(station.Latitude, station.Longitude);
                window = new Vector4((float)east, (float)north, (float)역중심Policy.Radius, (float)역중심Policy.Fade);
            }
            if (window == stationWindowApplied && stationWindowBase == dioramaMaterial && stationWindowManaged == managedMaterial) return;
            stationWindowApplied = window;
            stationWindowBase = dioramaMaterial;
            stationWindowManaged = managedMaterial;
            dioramaMaterial.SetVector(DioramaWindowId, window);
            if (managedMaterial != null) managedMaterial.SetVector(DioramaWindowId, window);
            if (dioramaShadowMaterial != null) dioramaShadowMaterial.SetVector(DioramaWindowId, window);
        }

        // 역 판의 선과 이름표. DrawDongModuleBoundaries가 역 중심 보기일 때 대신 부른다. GUI.BeginGroup(Field) 안의 좌표.
        private void DrawStationBoard()
        {
            EnsureStationLayout();
            var repaint = Event.current.type == EventType.Repaint;
            var station = Station;

            // 동 경계: 판 안쪽만 옅게. 판 밖의 점은 숨김 자리로 가므로 그 선은 그려지지 않는다.
            if (repaint)
            {
                foreach (var module in dongModules)
                {
                    var count = module.Latitudes.Length;
                    if (count < 3 || !DongVisible(module)) continue;
                    var previous = PlotSceneIn(module, module.Latitudes[count - 1], module.Longitudes[count - 1]);
                    for (var i = 0; i < count; i++)
                    {
                        var point = PlotSceneIn(module, module.Latitudes[i], module.Longitudes[i]);
                        Line(previous, point, Fade(Soft, 0.4f), 1.2f);
                        previous = point;
                    }
                }
            }

            // 판의 둘레
            var (stationEast, stationNorth) = DongLocal(station.Latitude, station.Longitude);
            var ring = 역중심Policy.Ring(stationEast, stationNorth, 역중심Policy.Radius, StationRingPoints);
            var points = new Vector2[ring.Count];
            for (var i = 0; i < ring.Count; i++)
                points[i] = ProjectScene(dongManifest.originLatitude + ring[i].North / dongManifest.metresPerDegreeLatitude,
                    dongManifest.originLongitude + ring[i].East / dongManifest.metresPerDegreeLongitude, 0f, Vector2.zero);
            if (repaint)
            {
                for (var i = 0; i < points.Length; i++)
                {
                    Line(points[i], points[(i + 1) % points.Length], Canvas, 4.5f);
                    Line(points[i], points[(i + 1) % points.Length], Mint, 2.2f);
                }
                // 역 세 겹의 둘레: 역 핵심(250 m)과 역세권(500 m)
                foreach (var (reach, tint, label) in new[] { (역중심Policy.Core, Gold, "역 핵심"), (역중심Policy.Area, Soft, "역세권") })
                {
                    var tier = 역중심Policy.Ring(stationEast, stationNorth, reach, StationRingPoints);
                    var first = Vector2.zero;
                    var before = Vector2.zero;
                    for (var i = 0; i <= tier.Count; i++)
                    {
                        var spot = tier[i % tier.Count];
                        var now = ProjectScene(dongManifest.originLatitude + spot.North / dongManifest.metresPerDegreeLatitude,
                            dongManifest.originLongitude + spot.East / dongManifest.metresPerDegreeLongitude, 0f, Vector2.zero);
                        if (i == 0) first = now;
                        else if (i % 2 == 1) { Line(before, now, Fade(Canvas, 0.7f), 4f); Line(before, now, Fade(tint, 0.9f), 2f); }      // 점선
                        before = now;
                    }
                    var plate = new Rect(first.x - 44f, first.y - 9f, 88f, 17f);      // 정북 쪽 둘레 위
                    Paint(plate, Fade(Canvas, 0.88f), 6);
                    Text(plate, label + " " + reach.ToString("0") + " m", 10, tint, true, TextAnchor.MiddleCenter);
                }
                // 역 자리
                var middle = ProjectScene(station.Latitude, station.Longitude, 0f, Vector2.zero);
                Paint(new Rect(middle.x - 8, middle.y - 8, 16, 16), Canvas, 8);
                Paint(new Rect(middle.x - 5.5f, middle.y - 5.5f, 11, 11), StationLine, 5.5f);
                Paint(new Rect(middle.x - 2.5f, middle.y - 2.5f, 5, 5), Paper, 2.5f);
            }

            // 동 이름표와 깃발: 판 안에 든 그 동 건물들의 가운데에. 누르면 그 동의 사업 칸으로 바뀐다(지도는 그대로).
            foreach (var pair in stationAnchors)
            {
                var module = DongModuleById(pair.Key);
                if (module == null || !DongVisible(module)) continue;
                var at = ProjectScene(pair.Value.Latitude, pair.Value.Longitude, 0f, Vector2.zero);
                if (at.x < 0 || at.x > Field.width || at.y < 0 || at.y > Field.height - 60) continue;
                var text = DongPlate(module, out var tone, out var rim, out var flagged);
                if (pair.Key == stationCardRegion) rim = Mint;
                var plate = new Rect(at.x - text.Length * 6.5f - 10, at.y + 18, text.Length * 13f + 20, 22);
                if (rim.a > 0f) Paint(new Rect(plate.x - 1.5f, plate.y - 1.5f, plate.width + 3, plate.height + 3), rim, 7);
                Paint(plate, rim.a > 0f ? Canvas : Fade(Canvas, 0.85f), 6);
                Text(plate, text, 11, tone, true, TextAnchor.MiddleCenter);
                if (flagged && repaint) DrawDongFlag(at);
                if (HitMapMarker(plate))
                {
                    관찰지도수동이동();
                    EnterDongFromStation(pair.Key);       // 한 번 누르면 그 동 디오라마 + 동 관리 창 (2026-11-17 사용자)
                    return;
                }
            }

            // 역 이름 칸: 판 아래. 양옆의 화살표로 역을 넘긴다.
            var bottom = points[0];
            var top = points[0];
            foreach (var point in points)
            {
                if (point.y > bottom.y) bottom = point;
                if (point.y < top.y) top = point;
            }
            var title = station.Name + " · " + station.Line + " · 반경 " + 역중심Policy.Radius.ToString("0") + " m" +
                (stationCoverage < 0.995 ? " · 자료 " + Mathf.FloorToInt((float)stationCoverage * 100f) + "%" : string.Empty);
            var width = title.Length * 11.5f + 28f;
            var bar = new Rect(bottom.x - width / 2, Mathf.Min(bottom.y + 8, Field.height - 92), width, 26);
            Paint(new Rect(bar.x - 1.5f, bar.y - 1.5f, bar.width + 3, bar.height + 3), StationLine, 8);
            Paint(bar, Canvas, 7);
            Text(bar, title, 12, Paper, true, TextAnchor.MiddleCenter);
            if (역중심Policy.Stations.Count > 1)
            {
                var prev = new Rect(bar.x - 30, bar.y + 1, 24, 24);
                var next = new Rect(bar.xMax + 6, bar.y + 1, 24, 24);
                Paint(prev, Rim, 6);
                Text(prev, "◀", 11, Paper, true, TextAnchor.MiddleCenter);
                Paint(next, Rim, 6);
                Text(next, "▶", 11, Paper, true, TextAnchor.MiddleCenter);
                if (HitMapMarker(prev))
                {
                    StepStation(-1);
                    return;
                }
                if (HitMapMarker(next))
                {
                    StepStation(1);
                    return;
                }
            }
            HitMapMarker(bar);

            // 고른 동의 사업 칸을 판 위에.
            var chosen = DongModuleById(stationCardRegion);
            if (chosen != null && DongFlagsActive && stationCardShown) DrawDongBusinessCard(chosen, top);
        }

        // 역 판의 동 이름표를 한 번 누름 (2026-11-17 사용자: 면목5동 · 면목7동을 누르면 그 행정동을 관리하는 디오라마로 옮겨 조작이 간편하게).
        // 그 동만 보기로 옮기고 왼쪽 동 관리 창을 연다. 떠나온 역을 적어 두어 아래에 「역 판으로」 단추를 세운다. 보기만 바꾼다(시계 · 업무 그대로).
        private bool EnterDongFromStation(string regionId)
        {
            var from = stationIndex;
            stationCardShown = false;
            if (!PickDong(regionId)) return false;       // 동 이동 창(M)과 같은 길로 「이 동만」에 들어간다
            stationReturnIndex = from;
            OpenDongDesk(regionId);
            return true;
        }

        // 떠나온 역 판으로 돌아간다. 들어왔던 동을 고른 동으로 둔 채 판을 다시 맞추고, 동 관리 창은 닫는다.
        private bool ReturnToStationBoard()
        {
            if (stationReturnIndex < 0) return false;
            var region = soloRegion;
            stationIndex = Mathf.Clamp(stationReturnIndex, 0, 역중심Policy.Stations.Count - 1);
            stationReturnIndex = -1;
            dongDeskOpen = false;
            focusDongRegion = region;
            stationCardRegion = region;
            stationCardShown = false;
            SetDongView(DongView.Station);
            return StationView;
        }

        // 이 동만 보기에서 아래 가운데의 「◀ 역 판으로 · 역 이름」 단추(역 판의 역 이름 칸과 같은 자리 · 모양). 눌렀으면 true.
        private bool DrawStationReturn()
        {
            if (stationReturnIndex < 0 || dongView != DongView.Solo || !DongModulesActive || PlaceVisible || CloseNow > 0.5f) return false;
            var station = 역중심Policy.Stations[Mathf.Clamp(stationReturnIndex, 0, 역중심Policy.Stations.Count - 1)];
            var title = "◀  역 판으로 · " + station.Name;
            var width = title.Length * 11.5f + 28f;
            var bar = new Rect(Field.width / 2 - width / 2, Field.height - 92, width, 26);
            Paint(new Rect(bar.x - 1.5f, bar.y - 1.5f, bar.width + 3, bar.height + 3), StationLine, 8);
            Paint(bar, bar.Contains(Event.current.mousePosition) ? Tile : Canvas, 7);
            Text(bar, title, 12, Paper, true, TextAnchor.MiddleCenter);
            if (!HitMapMarker(bar)) return false;
            관찰지도수동이동();
            return ReturnToStationBoard();
        }
    }
}
