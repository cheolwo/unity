using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 길찾기(내비게이션) 화면 (2026-11-18, Codex r287 · r279).
    // 사용자 방향: 「주소를 입력하거나 지도에 핀을 찍으면 … 경로와 거리 · 예상시간」 · 「그 내비게이션 경로에 따라 배달 기사님이
    //            움직이는 것을 표현 … 이동 경로에 따라 빛이 있다가 사라지는 … 실제 게임으로서 작용을 할 수 있도록」.
    //  - 표현 층이다. 동 디오라마의 길 그래프(HRTE, 순찰과 같은 그래프) 위에서만 논다. 배차 엔진의 경로(courier.route) · 주문 · 돈 ·
    //    시계 · 저장을 읽어 바꾸지 않는다. 실제 운행 안내가 아니다(가상 배달 · 시뮬레이션 추정 · 내부 검토용).
    //  - 출발 · 도착: 건물을 눌러 고르거나(핀), 주소 칸에 도로명 + 건물번호나 건물 이름을 쳐서 고른다(길안내Policy).
    //  - 길: 길찾기Policy가 문에서 문까지 찾는다. 큰길은 금빛, 골목은 민트빛 선. 문에서 길까지 걷는 구간은 점선으로 따로 그린다.
    //  - 문: 색인의 door가 "juso"면 공식 건물 출입구(도로명주소 2026-10), 아니면 가상 출입구다. 공식 건물 출입구는 점포 픽업문이 아니다.
    //  - 기사 보내기: 빛 한 점이 그 길을 따라간다. 순찰 기사와 같은 표현의 시간(게임이 흐를 때만) · 같은 빛 판을 쓴다.
    //  - 일방통행 · 회전 제한은 아직 길 그래프에 없다. 띠가 그 사실을 늘 적는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string NavFieldName = "hongdal-nav-field";
        private const float NavHitRow = 26f;
        private static readonly Rect NavButtonRect = new(340, 596, 84, 28);
        private static readonly Rect NavStripRect = new(340, 512, 540, 112);
        private static readonly Rect NavCardFromRect = new(986, 78, 132, 24);
        private static readonly Rect NavCardToRect = new(1126, 78, 132, 24);

        private bool navOpen;
        private int navFrom = -1, navTo = -1;
        private string navQuery = string.Empty;
        private string navSearched = string.Empty;
        private bool navHitsOpen;
        private readonly List<int> navHits = new();
        private string[] navKeys, navNameKeys;
        private readonly List<(float X, float Z, byte Kind)> navPath = new();
        private readonly List<(double Latitude, double Longitude, byte Kind, DongModuleState Module)> navDraw = new();
        private int navPlannedFrom = -2, navPlannedTo = -2;
        private bool navFound;
        private string navProblem;
        private float navDrive, navDriveSeconds, navWalkFrom, navWalkTo;
        private Roamer navRider;                 // 길안내 기사(빛). 순찰 기사 목록에는 넣지 않는다
        private float navRiderRest;              // 닿은 뒤 머무는 시간 (표현의 시간)
        private int navRuns;                     // 지금까지 끝까지 간 횟수 (표현)
        private float navRunMetres;
        private GUIStyle navFieldStyle;

        private bool NavAvailable => expansionView && mapLevel == MapLevel.Dong && DongModulesActive && EnsureRouteGraph() != null;

        // 왼쪽 창(동 관리 · 매장 운영)이 이 자리를 덮으면 띠와 단추는 그리지 않는다. 지도 위의 길은 그대로 보인다.
        private bool NavRoom
        {
            get
            {
                var corner = new Vector2(NavStripRect.x + 6f, NavStripRect.yMax - 6f);
                return !DongDeskCovers(corner) && !StoreDeskCovers(corner);
            }
        }

        private bool NavCardVisible => selectedBuilding != null && actorFocus == null && routeGraph != null &&
            selectedBuilding.Value < buildingShapes.Length && routeGraph.HasDoor(selectedBuilding.Value) &&
            !string.IsNullOrEmpty(buildingShapes[selectedBuilding.Value].Record.region);

        private bool NavHitsVisible => navOpen && navHitsOpen && navHits.Count > 0;

        private Rect NavHitsRect => new(NavStripRect.x + 70f, NavStripRect.y - 12f - NavHitRow * navHits.Count, 462f, NavHitRow * navHits.Count + 8f);

        // 지도 누르기를 막는 자리 (MapOverlayCovers)
        private bool NavCovers(Vector2 point)
        {
            if (mapOnly || !NavAvailable) return false;
            if (NavCardVisible && (NavCardFromRect.Contains(point) || NavCardToRect.Contains(point))) return true;
            if (!NavRoom) return false;
            if (!navOpen) return NavButtonRect.Contains(point);
            return NavStripRect.Contains(point) || NavHitsVisible && NavHitsRect.Contains(point);
        }

        private bool NavOfficialDoor(int building) => building >= 0 && building < buildingShapes.Length &&
            buildingShapes[building].Record.door == "juso";

        private string NavName(int building)
        {
            var record = buildingShapes[building].Record;
            if (!string.IsNullOrEmpty(record.road))
                return string.IsNullOrEmpty(record.name) ? record.road + " " + record.number : record.road + " " + record.number + " · " + record.name;
            return (string.IsNullOrEmpty(record.dong) ? "건물" : record.dong + " 건물") + " (주소 자료 없음)";
        }

        private void EnsureNavKeys()
        {
            if (navKeys != null && navKeys.Length == buildingShapes.Length) return;
            navKeys = new string[buildingShapes.Length];
            navNameKeys = new string[buildingShapes.Length];
            for (var building = 0; building < buildingShapes.Length; building++)
            {
                var record = buildingShapes[building].Record;
                var module = !string.IsNullOrEmpty(record.region);
                navKeys[building] = module ? 길안내Policy.Key(record.road, record.number) : string.Empty;
                navNameKeys[building] = module ? 길안내Policy.Fold(record.name) : string.Empty;
            }
        }

        private void SetNav(bool from, int building)
        {
            if (from) navFrom = building;
            else navTo = building;
            navOpen = true;
            navRider = null;                 // 길이 바뀌면 가던 기사는 거둔다
        }

        private void ClearNav()
        {
            navFrom = navTo = -1;
            navRider = null;
            navHitsOpen = false;
        }

        private void CloseNav()
        {
            ClearNav();
            navOpen = false;
            navQuery = string.Empty;
            GUIUtility.keyboardControl = 0;
        }

        private float NavWalk(int building)
        {
            var foot = routeGraph.Along(routeGraph.DoorEdge[building], routeGraph.DoorT[building]);
            var dx = routeGraph.DoorX[building] - foot.X;
            var dz = routeGraph.DoorZ[building] - foot.Z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // 출발 · 도착이 바뀌었을 때만 길을 다시 찾는다.
        private void PlanNav()
        {
            if (navPlannedFrom == navFrom && navPlannedTo == navTo) return;
            navPlannedFrom = navFrom;
            navPlannedTo = navTo;
            navPath.Clear();
            navDraw.Clear();
            navFound = false;
            navProblem = null;
            navDrive = navDriveSeconds = navWalkFrom = navWalkTo = 0f;
            if (navFrom < 0 || navTo < 0 || routeGraph == null) return;
            if (navFrom == navTo)
            {
                navProblem = "출발과 도착이 같은 건물입니다.";
                return;
            }
            if (!routeGraph.HasDoor(navFrom) || !routeGraph.HasDoor(navTo))
            {
                navProblem = "길에 닿는 문이 없는 건물입니다 (출입구 미확인).";
                return;
            }
            if (!길찾기Policy.Route(routeGraph, navFrom, navTo, navPath, out _))
            {
                navProblem = "두 곳을 잇는 길을 찾지 못했습니다 (이어지지 않은 길).";
                return;
            }
            길안내Policy.Measure(navPath, out navDrive, out navDriveSeconds);
            navWalkFrom = NavWalk(navFrom);
            navWalkTo = NavWalk(navTo);
            navFound = true;
            BuildNavDraw();
            Debug.Log($"Hongdal navigation: {navPath.Count} points, drive {navDrive:0} m, walk {navWalkFrom + navWalkTo:0} m, " +
                $"{navDriveSeconds + 길안내Policy.WalkSeconds(navWalkFrom + navWalkTo):0} s");
        }

        // 그릴 점을 줄인다: 곧게 이어도 0.5 m 안에 드는 사이 점은 뺀다. 종류가 바뀌는 점은 남긴다.
        private void BuildNavDraw()
        {
            navDraw.Clear();
            if (navPath.Count == 0) return;
            void Keep(int k)
            {
                var latitude = buildingIndex.origin.latitude + navPath[k].Z / buildingIndex.metresPerDegreeLatitude;
                var longitude = buildingIndex.origin.longitude + navPath[k].X / buildingIndex.metresPerDegreeLongitude;
                navDraw.Add((latitude, longitude, navPath[k].Kind, DongModuleAt(latitude, longitude)));
            }
            Keep(0);
            var anchor = 0;
            for (var k = 1; k < navPath.Count; k++)
            {
                if (k < navPath.Count - 1 && navPath[k + 1].Kind == navPath[k].Kind && NavChordFits(anchor, k + 1)) continue;
                Keep(k);
                anchor = k;
            }
        }

        private bool NavChordFits(int anchor, int end)
        {
            var ax = navPath[anchor].X;
            var az = navPath[anchor].Z;
            var dx = navPath[end].X - ax;
            var dz = navPath[end].Z - az;
            var length = Mathf.Sqrt(dx * dx + dz * dz);
            if (length < 0.001f) return true;
            if (length > 60f) return false;
            for (var k = anchor + 1; k < end; k++)
                if (Mathf.Abs((navPath[k].X - ax) * dz - (navPath[k].Z - az) * dx) / length > 0.5f) return false;
            return true;
        }

        // ── 지도 위: 길 · 걷는 구간 · 출발과 도착 · 기사 빛 (DrawBoard) ──
        private void DrawNavRoute()
        {
            if (Event.current.type != EventType.Repaint || !NavAvailable) return;
            if (navFrom < 0 && navTo < 0 && navRider == null) return;
            PlanNav();
            var anchorBuilding = navFrom >= 0 ? navFrom : navTo;
            var home = anchorBuilding >= 0 ? DongModuleById(buildingShapes[anchorBuilding].Record.region)
                : navRider != null ? DongModuleById(navRider.Region) : null;
            if (navFound && navDraw.Count > 1)
            {
                Vector2 Plot(int k)
                {
                    var p = navDraw[k];
                    if (dongView == DongView.Solo && (p.Module == null || !DongVisible(p.Module))) return DongHiddenPoint;
                    return PlotSceneIn(p.Module ?? home, p.Latitude, p.Longitude, 0.4f);
                }
                var road = Fade(Gold, 0.95f);
                var alley = Fade(Mint, 0.9f);
                var before = Plot(0);
                for (var k = 1; k < navDraw.Count; k++)
                {
                    var here = Plot(k);
                    if (Field.Contains(before) || Field.Contains(here))
                    {
                        var big = navDraw[k].Kind == 길그래프.Road;
                        Line(before, here, big ? road : alley, big ? 4f : 3f);
                    }
                    before = here;
                }
                DrawNavWalk(home, navFrom);
                DrawNavWalk(home, navTo);
            }
            if (navFrom >= 0) DrawNavPin(home, navFrom, "출발", Mint);
            if (navTo >= 0) DrawNavPin(home, navTo, "도착", Gold);
            if (navRider == null || navRiderRest > 0f && navRider.Path.Count == 0) return;
            EnsureRoamGlow();
            var point = PlotRoam(home, navRider.X, navRider.Z);
            if (point == DongHiddenPoint || !Field.Contains(point)) return;
            var beat = 0.5f + 0.5f * Mathf.Sin(lightBeat * 6.3f);
            Glow(point, 66f + 8f * beat, Fade(Gold, 0.42f));
            Glow(point, 30f, Fade(Gold, 0.8f));
            GlowCore(point, 13f, FireflyCore);
        }

        // 문에서 길까지 걷는 구간: 가는 점선. 주행 거리와 따로 센다.
        private void DrawNavWalk(DongModuleState home, int building)
        {
            if (building < 0 || !routeGraph.HasDoor(building)) return;
            var foot = routeGraph.Along(routeGraph.DoorEdge[building], routeGraph.DoorT[building]);
            var a = PlotRoam(home, routeGraph.DoorX[building], routeGraph.DoorZ[building]);
            var b = PlotRoam(home, foot.X, foot.Z);
            if (a == DongHiddenPoint || b == DongHiddenPoint) return;
            Line(a, b, Fade(Paper, 0.85f), 2f, true);
        }

        private void DrawNavPin(DongModuleState home, int building, string label, Color colour)
        {
            if (routeGraph == null || !routeGraph.HasDoor(building)) return;
            var point = PlotRoam(home, routeGraph.DoorX[building], routeGraph.DoorZ[building]);
            if (point == DongHiddenPoint || !Field.Contains(point)) return;
            Hoop(point, 8f, colour, 2.5f);
            var tag = new Rect(point.x - 19f, point.y - 31f, 38f, 18f);
            Paint(tag, Fade(Canvas, 0.92f), 6);
            Text(tag, label, 10, colour, true, TextAnchor.MiddleCenter);
        }

        // ── 기사 빛 ──
        private void StartNavRider()
        {
            if (!navFound || navPath.Count < 2) return;
            navRider = new Roamer
            {
                Region = buildingShapes[navFrom].Record.region, Index = -1, Seed = 1u, At = navFrom, Target = navTo, Phase = 1,
                X = navPath[0].X, Z = navPath[0].Z,
            };
            navRider.Path.AddRange(navPath);
            navRiderRest = 0f;
        }

        // 순찰과 같은 표현의 시간으로 나아간다. (StepRoamers가 부른다. scaled: 보기 좋게 빠르게 한 초)
        private void StepNavRider(float scaled)
        {
            if (navRider == null || routeGraph == null || scaled <= 0f) return;
            if (navRiderRest > 0f)
            {
                navRiderRest -= scaled;
                if (navRiderRest <= 0f) navRider = null;
                return;
            }
            var beforeX = navRider.X;
            var beforeZ = navRider.Z;
            var arrived = 기사순찰Policy.Advance(navRider.Path, ref navRider.Leg, ref navRider.Along, scaled,
                out navRider.X, out navRider.Z, out navRider.Heading);
            navRunMetres += Mathf.Sqrt((navRider.X - beforeX) * (navRider.X - beforeX) + (navRider.Z - beforeZ) * (navRider.Z - beforeZ));
            if (!arrived) return;
            navRuns++;
            navRiderRest = 기사순찰Policy.DropWait * 2f;
            if (roamLight == null || navRider.Target < 0 || navRider.Target >= buildingShapes.Length) return;
            var shape = buildingShapes[navRider.Target];
            var centre = (shape.Min + shape.Max) * 0.5f;
            roamLight.Light(기사불빛Policy.Drop, centre.x, centre.y, 기사불빛Policy.DropRadius, 1f);      // 닿은 곳이 잠깐 밝아진다
        }

        // ── 띠와 단추 ──
        private void NavButton(Rect rect, string label, Color back, Color ink, int size = 11)
        {
            Paint(rect, back, 7);
            Text(rect, label, size, ink, true, TextAnchor.MiddleCenter);
        }

        private void DrawNavPanel()
        {
            if (!NavAvailable) return;
            if (NavCardVisible)
            {
                // 고른 건물을 출발 · 도착으로: 건물 카드 바로 위
                NavButton(NavCardFromRect, "여기서 출발", Tile, Mint);
                NavButton(NavCardToRect, "여기로 도착", Tile, Gold);
                if (Hit(NavCardFromRect)) SetNav(true, selectedBuilding.Value);
                else if (Hit(NavCardToRect)) SetNav(false, selectedBuilding.Value);
            }
            if (!NavRoom) return;
            if (!navOpen)
            {
                Paint(NavButtonRect, Rim, 9);
                Paint(new Rect(NavButtonRect.x + 1, NavButtonRect.y + 1, NavButtonRect.width - 2, NavButtonRect.height - 2), Canvas, 8);
                Text(NavButtonRect, "길찾기", 12, Paper, true, TextAnchor.MiddleCenter);
                if (Hit(NavButtonRect)) navOpen = true;
                return;
            }
            EnsureNavKeys();
            PlanNav();
            var strip = NavStripRect;
            var x = strip.x + 14f;
            var y = strip.y;
            Paint(strip, Rim, 11);
            Paint(new Rect(strip.x + 1, strip.y + 1, strip.width - 2, strip.height - 2), Canvas, 10);
            Text(new Rect(x, y + 8, 52, 26), "길찾기", 13, Gold, true);

            // 주소 칸: Enter는 첫 후보를 비어 있는 쪽(출발 먼저)에 넣는다.
            var current = Event.current;
            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == NavFieldName)
            {
                current.Use();
                NavChooseHit(0, navFrom < 0);
            }
            navFieldStyle ??= new GUIStyle(GUI.skin.textField) { font = font, fontSize = 13 };
            GUI.SetNextControlName(NavFieldName);
            navQuery = GUI.TextField(new Rect(x + 56, y + 9, 230, 24), navQuery ?? string.Empty, 40, navFieldStyle);
            if (navQuery != navSearched)
            {
                navSearched = navQuery;
                navHits.Clear();
                navHits.AddRange(길안내Policy.Search(navKeys, navNameKeys, navQuery, 길안내Policy.HitLimit));
                navHitsOpen = navHits.Count > 0;
            }
            Text(new Rect(x + 294, y + 8, 186, 26), navQuery.Length == 0 ? "도로명 + 건물번호 · 건물 이름"
                : navHits.Count == 0 ? "맞는 주소가 없습니다" : "Enter: 첫 후보를 " + (navFrom < 0 ? "출발로" : "도착으로"), 11, Soft);
            var close = new Rect(strip.xMax - 30, y + 8, 22, 24);
            Text(close, "×", 18, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                CloseNav();
                return;
            }

            DrawNavSlot(new Rect(x, y + 40, 252, 24), true);
            DrawNavSlot(new Rect(x + 260, y + 40, 252, 24), false);

            var line = new Rect(x, y + 68, 334, 22);
            if (navFound)
                Text(line, "주행 " + 길안내Policy.Metres(navDrive) + " · 도보 " + 길안내Policy.Metres(navWalkFrom + navWalkTo) + " · " +
                    길안내Policy.Time(navDriveSeconds + 길안내Policy.WalkSeconds(navWalkFrom + navWalkTo)), 13, Paper, true);
            else
                Text(line, navProblem ?? "출발과 도착을 고르면 길 · 거리 · 예상시간이 나옵니다.", 12, navProblem != null ? Gold : Soft);
            if (navFound)
            {
                var send = new Rect(x + 340, y + 67, 92, 24);
                var going = navRider != null;
                NavButton(send, going ? "가는 중" : "기사 보내기", going ? Rim : Mint, going ? Paper : Canvas);
                if (!going && Hit(send)) StartNavRider();
            }
            if (navFrom >= 0 || navTo >= 0)
            {
                var wipe = new Rect(x + 438, y + 67, 74, 24);
                NavButton(wipe, "지우기", Tile, Paper);
                if (Hit(wipe)) ClearNav();
            }
            var doors = navFound
                ? "문: " + 길안내Policy.DoorLabel(true, NavOfficialDoor(navFrom)) + " → " + 길안내Policy.DoorLabel(true, NavOfficialDoor(navTo))
                : navRider != null && campaignSpeed == 0 ? "정지 중에는 기사가 멈춰 있습니다" : string.Empty;
            Text(new Rect(x, y + 91, 250, 16), doors, 10, Soft);
            Text(new Rect(x + 232, y + 91, 280, 16), "추정값 · 일방통행 · 회전 제한 미반영 · 내부 검토용", 10, Soft, align: TextAnchor.MiddleRight);
            DrawNavHits();
        }

        private void DrawNavSlot(Rect slot, bool from)
        {
            var value = from ? navFrom : navTo;
            var canPick = selectedBuilding != null && selectedBuilding.Value != value && selectedBuilding.Value < buildingShapes.Length &&
                routeGraph.HasDoor(selectedBuilding.Value);
            Paint(slot, Tile, 6);
            Text(new Rect(slot.x + 7, slot.y, 30, slot.height), from ? "출발" : "도착", 11, from ? Mint : Gold, true);
            var right = slot.xMax - 4f;
            if (value >= 0)
            {
                var clear = new Rect(right - 18, slot.y + 2, 18, slot.height - 4);
                Text(clear, "×", 14, Soft, align: TextAnchor.MiddleCenter);
                if (Hit(clear))
                {
                    SetNav(from, -1);
                    return;
                }
                right -= 20f;
            }
            if (canPick)
            {
                var pick = new Rect(right - 56, slot.y + 3, 56, slot.height - 6);
                NavButton(pick, "고른 건물", Rim, Paper, 10);
                if (Hit(pick))
                {
                    SetNav(from, selectedBuilding.Value);
                    return;
                }
                right -= 60f;
            }
            Text(new Rect(slot.x + 38, slot.y, right - slot.x - 40, slot.height), value >= 0 ? NavName(value) : "건물을 누르거나 주소로 찾기",
                12, value >= 0 ? Paper : Soft);
        }

        private void DrawNavHits()
        {
            if (!NavHitsVisible) return;
            var box = NavHitsRect;
            Paint(box, Rim, 9);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 8);
            for (var row = 0; row < navHits.Count; row++)
            {
                var building = navHits[row];
                var record = buildingShapes[building].Record;
                var line = new Rect(box.x + 8, box.y + 4 + NavHitRow * row, box.width - 16, NavHitRow);
                var info = NavName(building) + (string.IsNullOrEmpty(record.dong) ? string.Empty : " · " + record.dong) +
                    (record.area > 0 ? $" · {record.area:N0}㎡" : string.Empty);
                Text(new Rect(line.x, line.y, line.width - 104, line.height), info, 12, Paper);
                var asFrom = new Rect(line.xMax - 100, line.y + 3, 46, line.height - 6);
                var asTo = new Rect(line.xMax - 50, line.y + 3, 46, line.height - 6);
                NavButton(asFrom, "출발", Tile, Mint);
                NavButton(asTo, "도착", Tile, Gold);
                if (Hit(asFrom))
                {
                    NavChooseHit(row, true);
                    return;
                }
                if (Hit(asTo))
                {
                    NavChooseHit(row, false);
                    return;
                }
            }
        }

        private void NavChooseHit(int row, bool from)
        {
            if (row < 0 || row >= navHits.Count) return;
            SetNav(from, navHits[row]);
            navHitsOpen = false;
            GUIUtility.keyboardControl = 0;
        }

        // ── 방향 촬영 ──
        // 면목4동에서 공공시설 이름이 붙은 건물 둘을 골라(없으면 공식 출입구가 있는 건물), 이름으로 찾기 → 길 → 기사 빛이 끝까지 가는지 본다.
        // 시계와 원장은 그대로여야 한다.
        private int NavProofPick(string region, string[] words, int except)
        {
            var best = -1;
            var bestArea = -1f;
            for (var building = 0; building < buildingShapes.Length; building++)
            {
                var record = buildingShapes[building].Record;
                if (building == except || record.region != region || !routeGraph.HasDoor(building) || string.IsNullOrEmpty(record.name)) continue;
                if (!words.Any(word => record.name.Contains(word))) continue;
                var area = record.area + (record.door == "juso" ? 100000f : 0f);      // 공식 출입구가 있는 건물을 먼저
                if (area <= bestArea) continue;
                bestArea = area;
                best = building;
            }
            return best;
        }

        // 그 동에서 from과 곧게 420 m쯤(200 m 이상) 떨어진 건물. words가 있으면 이름에 그 말이 든 건물만, 없으면 공식 출입구 건물만.
        private int NavProofPickFar(string region, string[] words, int from)
        {
            var best = -1;
            var gap = float.MaxValue;
            for (var building = 0; building < buildingShapes.Length; building++)
            {
                var record = buildingShapes[building].Record;
                if (building == from || record.region != region || !routeGraph.HasDoor(building)) continue;
                if (words != null ? string.IsNullOrEmpty(record.name) || !words.Any(word => record.name.Contains(word)) : !NavOfficialDoor(building)) continue;
                var straight = NavProofStraight(from, building);
                if (straight < 200f) continue;
                var off = Mathf.Abs(straight - 420f);
                if (off >= gap) continue;
                gap = off;
                best = building;
            }
            return best;
        }

        private float NavProofStraight(int a, int b)
        {
            var dx = routeGraph.DoorX[a] - routeGraph.DoorX[b];
            var dz = routeGraph.DoorZ[a] - routeGraph.DoorZ[b];
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private IEnumerator ViewProofNav(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            SetDioramaPerspective(false);
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            SetDongView(DongView.Joined);
            yield return null;
            receipt.navReady = NavAvailable;
            if (!receipt.navReady)
            {
                holdProofClock = false;
                yield break;
            }
            foreach (var shape in buildingShapes)
            {
                if (shape.Record.door == "juso") receipt.navOfficialDoors++;
                if (!string.IsNullOrEmpty(shape.Record.road)) receipt.navAddresses++;
            }
            var region = HjdRegionPrefix + "1126054000";      // 면목4동 (r287의 첫 검증 자리)
            var publicWords = new[] { "주민센터", "행정복지센터", "우체국", "파출소", "지구대", "소방", "초등학교", "중학교", "고등학교", "도서관", "체육", "복지관", "보건" };
            var from = NavProofPick(region, new[] { "주민센터", "행정복지센터", "우체국", "파출소", "지구대", "소방" }, -1);
            if (from < 0) from = NavProofPick(region, publicWords, -1);
            for (var building = 0; from < 0 && building < buildingShapes.Length; building++)
                if (buildingShapes[building].Record.region == region && NavOfficialDoor(building) && routeGraph.HasDoor(building))
                    from = building;
            if (from < 0)
            {
                holdProofClock = false;
                yield break;
            }
            var to = NavProofPickFar(region, publicWords, from);
            receipt.navPublicPlaces = to >= 0 && !string.IsNullOrEmpty(buildingShapes[from].Record.name) &&
                publicWords.Any(word => buildingShapes[from].Record.name.Contains(word));
            if (to < 0) to = NavProofPickFar(region, null, from);
            if (to < 0)
            {
                holdProofClock = false;
                yield break;
            }

            // 주소 칸에 친 것처럼: 도착 건물의 이름(없으면 도로명 + 번호)으로 찾는다.
            navOpen = true;
            EnsureNavKeys();
            var toRecord = buildingShapes[to].Record;
            navQuery = navSearched = !string.IsNullOrEmpty(toRecord.name) ? toRecord.name : toRecord.road + " " + toRecord.number;
            navHits.Clear();
            navHits.AddRange(길안내Policy.Search(navKeys, navNameKeys, navQuery, 길안내Policy.HitLimit));
            receipt.navSearchHits = navHits.Count;
            receipt.navSearchExact = navHits.Count > 0 && (navHits.Contains(to) ||
                navNameKeys[navHits[0]].Length > 0 && navNameKeys[navHits[0]] == navNameKeys[to] ||
                navKeys[navHits[0]].Length > 0 && navKeys[navHits[0]] == navKeys[to]);
            navHitsOpen = false;
            SetNav(true, from);
            SetNav(false, to);
            PlanNav();
            receipt.navFound = navFound;
            receipt.navStraightMetres = NavProofStraight(from, to);
            receipt.navDriveMetres = navDrive;
            receipt.navWalkMetres = navWalkFrom + navWalkTo;
            receipt.navSeconds = navDriveSeconds + 길안내Policy.WalkSeconds(navWalkFrom + navWalkTo);
            receipt.navPathPoints = navPath.Count;
            receipt.navDrawPoints = navDraw.Count;
            receipt.navDoorsOfficial = (NavOfficialDoor(from) ? 1 : 0) + (NavOfficialDoor(to) ? 1 : 0);
            var stride = Mathf.Max(1, navPath.Count / 160);
            for (var k = 1; k < navPath.Count - 1; k += stride)
                if (RoamInsideBuilding(navPath[k].X, navPath[k].Z)) receipt.navInsideBuildings++;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in navPath)
            {
                minX = Mathf.Min(minX, p.X);
                maxX = Mathf.Max(maxX, p.X);
                minZ = Mathf.Min(minZ, p.Z);
                maxZ = Mathf.Max(maxZ, p.Z);
            }
            if (navFound && navPath.Count > 1)
            {
                var span = Mathf.Max(maxX - minX, maxZ - minZ, 160f);
                ViewProofCentre(buildingIndex.origin.latitude + (minZ + maxZ) * 0.5f / buildingIndex.metresPerDegreeLatitude,
                    buildingIndex.origin.longitude + (minX + maxX) * 0.5f / buildingIndex.metresPerDegreeLongitude,
                    (float)metresPerPixel * 400f / span);
            }
            yield return null;
            yield return null;
            yield return null;
            receipt.navStripDrawn = navOpen && NavRoom;
            yield return SaveFrame(directory, "135-nav-route.png");

            // 기사 보내기: 절반쯤 간 모습을 찍고, 끝까지 간다.
            var runsBefore = navRuns;
            var metresBefore = navRunMetres;
            StartNavRider();
            for (var step = 0; step < 4000 && navRider != null && navRuns == runsBefore && navRider.Leg < navRider.Path.Count / 2; step++)
            {
                StepRoamers(0.25f);
                StepRoamLight();
            }
            ShowRoamLight();
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "136-nav-rider.png");
            for (var step = 0; step < 20000 && navRuns == runsBefore && navRider != null; step++)
            {
                StepRoamers(0.25f);
                StepRoamLight();
            }
            ShowRoamLight();
            receipt.navRiderArrived = navRuns == runsBefore + 1;
            receipt.navRiderMetres = navRunMetres - metresBefore;
            receipt.navUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;

            CloseNav();
            navPlannedFrom = navPlannedTo = -2;
            SetDongView(DongView.Station);
            yield return null;
            yield return null;
            holdProofClock = false;
        }
    }
}
