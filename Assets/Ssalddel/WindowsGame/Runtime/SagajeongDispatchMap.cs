using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect MapToolbar = new(675, 119, 577, 68);
        private static readonly Rect MapStatus = new(333, 609, 919, 56);
        private Rect Field = new(0, 0, Width, Height);
        // 지금 그리는 곳이 GUI.BeginGroup(Field) 안이면 Field의 시작점, 밖이면 (0, 0).
        // IMGUI는 그룹 안 좌표에 그룹 위치를 더한 다음 GUI.matrix를 곱한다. 창 비율이 16:9가 아니면 Field가 (0, 0)에서
        // 시작하지 않으므로(MapViewport), 지도 안에서 돌릴 때는 회전 중심에도 이 값을 더해야 그린 자리가 맞는다(2026-11-13).
        private static Vector2 mapGroupShift;
        private DispatchRoad[] overviewRoads = Array.Empty<DispatchRoad>();
        private DispatchRoad[] overviewOutline = Array.Empty<DispatchRoad>();
        private bool viewportTextureDirty;
        // 확대 한계(MaxMapZoom)는 가까이 보기가 정한다 (SagajeongDispatchCloseView.cs). 평면 지도에서는 전과 같이 4배다.
        private const double WheelLevelCooldown = 0.18;
        private float mapZoom = 1f;
        private Vector2 mapOffset;
        private MapLevel mapLevel;
        private bool dongZoomFloorVisible;
        private int lastWheelLevelDirection;
        private double wheelLevelBlockedUntil;
        private DispatchRoad[] localRoads = Array.Empty<DispatchRoad>();
        private OverviewLabel[] overviewLabels = Array.Empty<OverviewLabel>();
        private Color32[] roadPixels;
        private string mapNotice;

        private enum MapLevel { Dong, District, Seoul, Nation }      // Nation: r155 전국 지도(SagajeongDispatchNation.cs)

        [Serializable]
        private sealed class OverviewLayer
        {
            public DispatchRoad[] roads;
            public DispatchRoad[] outline;
            public OverviewLabel[] labels;
        }

        [Serializable]
        private sealed class OverviewLabel
        {
            public string name;
            public double latitude;
            public double longitude;
        }

        private static Rect MapViewport(int screenWidth, int screenHeight, float scale)
        {
            var width = Mathf.CeilToInt(screenWidth / scale);
            var height = Mathf.CeilToInt(screenHeight / scale);
            return new Rect((Width - width) * 0.5f, (Height - height) * 0.5f, width, height);
        }

        private void ResizeMapViewport(float scale)
        {
            SetMapField(MapViewport(Screen.width, Screen.height, scale));
        }

        // 지도 자리를 바꾼다(창 크기 · 세로 화면: SagajeongDispatchPortrait.cs)
        private void SetMapField(Rect next)
        {
            if (next == Field) return;

            mapOffset += (next.size - Field.size) * ((1f - mapZoom) * 0.5f);
            Field = next;
            mapPointerDown = false;
            mapDragging = false;
            mapClick = false;
            if (roadTexture == null) return;

            if (mapLevel == MapLevel.Dong) mapOffset = ClampMapOffset(mapOffset, mapZoom);
            ReleaseMapTexture();
            viewportTextureDirty = true;
        }

        private void PrepareMap(DispatchRoad[] roads)
        {
            localRoads = roads;
            if (mapLevel == MapLevel.Dong) DrawLocalTexture();
        }

        private void DrawLocalTexture(LegalDong target = null)
        {
            var latitudes = state.stores.Select(item => item.latitude).Concat(state.destinations.Select(item => item.latitude)).ToArray();
            var longitudes = state.stores.Select(item => item.longitude).Concat(state.destinations.Select(item => item.longitude)).ToArray();
            FitMap(latitudes, longitudes, 310, 250);
            tileIndex ??= ReadMapResource<TileIndex>(TileFolder + "index");
            if (tileIndex?.tiles == null || tileIndex.step <= 0) tileIndex = null;
            if (target != null && tileIndex != null)
            {
                mapZoom = 1f;
                mapOffset = Field.center - Plot(target.latitude, target.longitude);
                viewedDong = target;
                SelectDongCityBoard(target);
            }
            mapOffset = ClampMapOffset(mapOffset, mapZoom);
            RefreshLocalRoadTexture();
        }

        private void RefreshLocalRoadTexture()
        {
            BuildRoadTexture(UpdateVisibleTiles(), Array.Empty<DispatchRoad>());
            localTextureDirty = false;
        }

        private void FitMap(double[] latitudes, double[] longitudes, double paddingX, double paddingY)
        {
            var south = latitudes.Min();
            var north = latitudes.Max();
            var west = longitudes.Min();
            var east = longitudes.Max();
            centerLatitude = (south + north) * 0.5;
            centerLongitude = (west + east) * 0.5;
            longitudeScale = 111200 * Math.Cos(centerLatitude * Math.PI / 180);
            var widthMetres = (east - west) * longitudeScale + paddingX;
            var heightMetres = (north - south) * 111200 + paddingY;
            metresPerPixel = Math.Max(widthMetres / Math.Min(Field.width, Width),
                heightMetres / Math.Min(Field.height, Height));
        }

        private void BuildRoadTexture(DispatchRoad[] roads, DispatchRoad[] outline)
        {

            var width = Mathf.RoundToInt(Field.width);
            var height = Mathf.RoundToInt(Field.height);
            if (roadPixels == null || roadPixels.Length != width * height)
                roadPixels = new Color32[width * height];
            var pixels = roadPixels;
            var ground = (Color32)Ink(0x142632);
            var grid = (Color32)Ink(0x19303C);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = x % 48 == 0 || y % 48 == 0 ? grid : ground;

            if (mapLevel == MapLevel.Dong) BlendCityBoard(pixels, width, height);

            foreach (var edge in roads)
                RasterRoad(edge, pixels, width, height, 4, Ink(0x263D48));
            foreach (var edge in roads)
                RasterRoad(edge, pixels, width, height, 2, Ink(0x3D5964));
            foreach (var edge in outline)
                RasterRoad(edge, pixels, width, height, 2, Ink(0x668897));

            if (roadTexture == null)
                roadTexture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Sagajeong road board" };
            roadTexture.SetPixels32(pixels);
            roadTexture.Apply(false, false);
            viewportTextureDirty = false;
        }

        private Vector2 Plot(double latitude, double longitude) => new(
            Field.center.x + (float)((longitude - centerLongitude) * longitudeScale / metresPerPixel),
            Field.center.y - (float)((latitude - centerLatitude) * 111200 / metresPerPixel));

        private Vector2 PlotOnBoard(double latitude, double longitude)
        {
            var point = Plot(latitude, longitude) - Field.position;
            return mapLevel == MapLevel.Dong ? point * mapZoom + mapOffset : point;
        }

        private void HandleMapWheel()
        {
            var wheel = Event.current;
            if (wheel.type != EventType.ScrollWheel || wheel.delta.y == 0 || !Field.Contains(wheel.mousePosition)) return;
            // 전환 알림 위에서도 휠은 지도로 보낸다. 그 밖의 패널 위에서는 지도를 움직이지 않는다.
            if (!LevelPromptCovers(wheel.mousePosition) && MapOverlayCovers(wheel.mousePosition)) return;

            var delta = wheel.delta.y;
            wheel.Use();
            if (mapPointerDown) return;
            ApplyMapWheel(delta, wheel.mousePosition, Time.realtimeSinceStartupAsDouble);
        }

        // 정밀 3(2026-11-16): 휠 한 칸의 처리. 촬영의 휠 왕복(SagajeongDispatchContinuityProof.cs)도 이 길을 그대로 지난다.
        // mouse는 논리 화면 좌표(Event.mousePosition과 같은 좌표), now는 막힘 시간을 재는 시각.
        private void ApplyMapWheel(float delta, Vector2 mouse, double now)
        {
            var wheelAt = mouse - Field.position;
            var pointer = FlatPoint(wheelAt);
            var direction = delta > 0 ? 1 : -1;
            if (direction == lastWheelLevelDirection && now < wheelLevelBlockedUntil)
                return;
            lastWheelLevelDirection = 0;
            if (direction < 0) CloseLevelPrompt();

            if (mapLevel == MapLevel.Dong)
            {
                var floor = DongMinZoom;
                if (direction < 0 || mapZoom > floor + 0.0005f)
                {
                    var nextZoom = Mathf.Clamp(mapZoom * Mathf.Pow(1.08f, -delta), floor, MaxMapZoom);
                    if (nextZoom != mapZoom)
                    {
                        관찰지도수동이동();
                        // 가까이 보기: 배율에 따라 내려다보는 각이 눕는다. 새 각에서도 가리킨 땅이 그 자리에 남게 한다.
                        var board = (pointer - mapOffset) / mapZoom;
                        var anchor = UseCloseLens(nextZoom) ? FlatPoint(wheelAt) : pointer;
                        var nextOffset = anchor - board * nextZoom;
                        mapOffset = ClampDioramaOffset(ClampMapOffset(nextOffset, nextZoom), nextZoom);
                        mapZoom = nextZoom;
                        dongZoomFloorVisible = false;
                        localTextureDirty = true;
                    }
                    return;
                }

                // 동네 전체가 보이는 한계에 닿았다. 그 화면을 한 번 보여 준 뒤, 알림 창 없이 같은 자리의 구 지도로 이어 간다(이어지는 지도).
                if (!dongZoomFloorVisible) return;
                DongToGu();
                lastWheelLevelDirection = direction;
                wheelLevelBlockedUntil = now + WheelLevelCooldown;
                return;
            }

            if (GuMapVisible)
            {
                // 구 지도는 그 안에서 확대·축소한다. 끝까지 축소하면 같은 자리의 전국 지도로(서울 단계 없음, r184), 면목동에서 끝까지 확대하면 동네 지도로 간다.
                var step = GuWheel(delta, mouse - Field.position);
                if (step == 0) return;
                if (step < 0)
                {
                    ChangeMapLevel(-1);
                    return;
                }
                GuToNation(mouse - Field.position);
                return;
            }

            if (NationMapVisible)
            {
                // 전국 지도도 그 안에서 확대·축소한다. 구 지도 자료 안에서 끝까지 확대하면 같은 자리의 구 지도로 이어 간다.
                if (NationWheel(delta, mouse - Field.position) < 0) NationToGu(mouse - Field.position);
                return;
            }

            if (direction > 0)
            {
                // 구 → 서울, 서울 → 전국(r155). 전국이 가장 넓은 지도다.
                var wider = mapLevel == MapLevel.Seoul ? MapLevel.Nation : MapLevel.Seoul;
                if (mapLevel == MapLevel.Nation || !AdvanceLevelPrompt(wider, now)) return;
                ChangeMapLevel(1);
                return;
            }
            ChangeMapLevel(-1);
        }

        // 판 가름(판Policy.cs): 기본 판은 구 지도가 바탕이다. 검증 촬영(--sagajeong-… · --hongdal-view…)은 상위 판의 길까지 보므로 열어 둔다.
        private bool dongMapOpen = 판Policy.동지도 || (Environment.GetCommandLineArgs().Any(item =>
            item.StartsWith("--sagajeong-", StringComparison.Ordinal) || item.StartsWith("--hongdal-view", StringComparison.Ordinal)) &&
            !Environment.GetCommandLineArgs().Any(item => item.StartsWith("--hongdal-base", StringComparison.Ordinal)));      // 기본 판 시험은 닫고 본다
        private bool baseEditionStarted;      // 기본 판: 첫 상태를 받은 뒤 구 지도로 올라갔다

        // Update가 부른다. 기본 판이면 게임이 시작되자마자 구 지도로 올라간다. 그 뒤로 동네 지도 요청은 구 지도에 머문다.
        private void StartBaseEditionOnGu()
        {
            if (dongMapOpen || baseEditionStarted || state == null) return;
            LoadMapLevel(MapLevel.District);
            if (mapLevel != MapLevel.District) return;      // 구 지도 자료가 아직이면 다음에 다시
            baseEditionStarted = true;
            guManual = false;
            Debug.Log("Hongdal edition: 기본 판 — 구 지도에서 시작");
        }

        private void LoadMapLevel(MapLevel target, LegalDong dong = null)
        {
            if (target == MapLevel.Dong && !dongMapOpen && baseEditionStarted)
            {
                if (mapLevel == MapLevel.District) return;      // 기본 판: 동네 지도 대신 구 지도에 머문다
                target = MapLevel.District;
                dong = null;
            }
            if (target != mapLevel) 관찰지도수동이동();
            if (target != mapLevel && target != MapLevel.Dong) 관찰선택해제();
            if (target == MapLevel.Dong)
            {
                ReleaseMapTexture();
                mapLevel = target;
                DrawLocalTexture(dong);
                overviewLabels = Array.Empty<OverviewLabel>();
            }
            else if (target == MapLevel.District && LoadGu())
            {
                // 구 지도는 구운 바탕과 영역 층으로 그린다(SagajeongDispatchGu.cs). 예전 도로 표본 그림은 구 공간 팩이 없을 때만 쓴다.
                if (visibleTiles.Count != 0)
                    Debug.Log($"Sagajeong road tiles: loaded 0, released {visibleTiles.Count}, visible 0");
                visibleTiles.Clear();
                tileIndex = null;
                ReleaseMapTexture();
                mapLevel = target;
                overviewRoads = Array.Empty<DispatchRoad>();
                overviewOutline = Array.Empty<DispatchRoad>();
                overviewLabels = Array.Empty<OverviewLabel>();
                BuildRoadTexture(overviewRoads, overviewOutline);   // 빈 바탕. 지도 그리기 흐름이 이 그림이 있는지를 본다
                guCameraSet = false;
                guManual = false;
                guPressed = false;
                guDragging = false;
            }
            else if (target == MapLevel.Nation)
            {
                // r155 전국 지도. 구운 바탕과 흐름 표로 그린다(SagajeongDispatchNation.cs).
                if (!LoadNation())
                {
                    mapNotice = "전국 지도 자료를 열 수 없습니다.";
                    return;
                }
                if (visibleTiles.Count != 0)
                    Debug.Log($"Sagajeong road tiles: loaded 0, released {visibleTiles.Count}, visible 0");
                visibleTiles.Clear();
                tileIndex = null;
                ReleaseMapTexture();
                mapLevel = target;
                overviewRoads = Array.Empty<DispatchRoad>();
                overviewOutline = Array.Empty<DispatchRoad>();
                overviewLabels = Array.Empty<OverviewLabel>();
                BuildRoadTexture(overviewRoads, overviewOutline);   // 빈 바탕. 지도 그리기 흐름이 이 그림이 있는지를 본다
                nationCameraSet = false;
                nationManual = false;
                nationPressed = false;
                nationDragging = false;
            }
            else
            {
                var name = target == MapLevel.District ? "district" : "seoul";
                var layer = ReadMapResource<OverviewLayer>("SagajeongDispatch/" + name);
                if (layer == null)
                {
                    mapNotice = "이 축척의 지도 자료를 열 수 없습니다.";
                    return;
                }

                var roads = layer.roads ?? Array.Empty<DispatchRoad>();
                var outline = layer.outline ?? Array.Empty<DispatchRoad>();
                var points = roads.Concat(outline).SelectMany(edge => new[] { edge.a, edge.b }).ToArray();
                if (points.Length == 0)
                {
                    mapNotice = "이 축척의 지도 자료가 비어 있습니다.";
                    return;
                }
                FitMap(points.Select(point => point.latitude).ToArray(),
                    points.Select(point => point.longitude).ToArray(), 500, 500);
                if (visibleTiles.Count != 0)
                    Debug.Log($"Sagajeong road tiles: loaded 0, released {visibleTiles.Count}, visible 0");
                visibleTiles.Clear();
                tileIndex = null;
                ReleaseMapTexture();
                mapLevel = target;
                overviewRoads = roads;
                overviewOutline = outline;
                BuildRoadTexture(overviewRoads, overviewOutline);
                overviewLabels = layer.labels ?? Array.Empty<OverviewLabel>();
            }

            mapNotice = dong != null && tileIndex == null ? "이 동네의 상세 도로를 열 수 없습니다." : null;
            dongZoomFloorVisible = false;
            lastWheelLevelDirection = 0;
            mapPointerDown = false;
            mapDragging = false;
            mapClick = false;
            if (mapLevel != MapLevel.Nation) ReleaseNationClock();     // (정지 정책이 켜져 있었다면) 멈춘 게임 시계를 되돌린다
            Debug.Log("Sagajeong map level: " + mapLevel);
            if (dong != null && tileIndex != null) Debug.Log("Sagajeong map focus: " + dong.id);
        }

        private void ReleaseMapTexture()
        {
            if (roadTexture != null) Destroy(roadTexture);
            roadTexture = null;
        }

        private static T ReadMapResource<T>(string path) where T : class
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return null;
            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch (ArgumentException)
            {
                return null;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        private void RasterRoad(DispatchRoad edge, Color32[] pixels, int width, int height, int radius, Color32 color)
        {
            var a = PlotOnBoard(edge.a.latitude, edge.a.longitude) + Field.position;
            var b = PlotOnBoard(edge.b.latitude, edge.b.longitude) + Field.position;
            if (Mathf.Max(a.x, b.x) < Field.xMin || Mathf.Min(a.x, b.x) >= Field.xMax ||
                Mathf.Max(a.y, b.y) < Field.yMin || Mathf.Min(a.y, b.y) >= Field.yMax) return;

            var steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude));
            for (var step = 0; step <= steps; step++)
            {
                var point = Vector2.LerpUnclamped(a, b, step / (float)steps);
                var px = Mathf.RoundToInt(point.x - Field.x);
                var py = height - 1 - Mathf.RoundToInt(point.y - Field.y);
                for (var y = -radius; y <= radius; y++)
                    for (var x = -radius; x <= radius; x++)
                        if (x * x + y * y <= radius * radius && px + x >= 0 && px + x < width && py + y >= 0 && py + y < height)
                            pixels[(py + y) * width + px + x] = color;
            }
        }

        private void DrawMap()
        {
            if (GUI.enabled)
            {
                if (roadTexture != null) HandleMapWheel();
                HandleMapPointer();
            }
            if (Event.current.type == EventType.Repaint)
            {
                KeepDongZoomFloor();
                if (mapLevel == MapLevel.Dong && (localTextureDirty || viewportTextureDirty))
                    RefreshLocalRoadTexture();
                else if (viewportTextureDirty)
                    BuildRoadTexture(overviewRoads, overviewOutline);
            }
            if (roadTexture == null)
            {
                Paint(Field, Canvas);
                return;
            }

            GUI.BeginGroup(Field);
            mapGroupShift = Field.position;
            if (GuMapVisible)
                DrawGuMap();
            else if (NationMapVisible)
                DrawNationMap();
            else
            {
                // 2.5D 장면이 그려진 프레임에는 평면 지도 그림을 덮지 않는다. 표식만 그 위에 올린다.
                if (!DioramaVisible) GUI.DrawTexture(new Rect(Vector2.zero, Field.size), roadTexture);
                if (mapLevel == MapLevel.Dong)
                    DrawBoard();
                else
                {
                    // 서울 25구 비교가 켜져 있으면 그 판이 서울 지도를 대신한다(SagajeongDispatchSeoulCompare.cs)
                    if (mapLevel != MapLevel.Seoul || !DrawSeoulCompare()) DrawOverviewLabels();
                }
            }
            GUI.EndGroup();
            mapGroupShift = Vector2.zero;
            if (Event.current.type == EventType.Repaint && AtDongFloor)
                dongZoomFloorVisible = true;
        }

        private void DrawMapChrome()
        {
            Paint(MapToolbar, Panel, 10);
            Paint(MapStatus, Panel, 10);
            var title = mapLevel switch
            {
                MapLevel.District => "구 단위 · 서울 동북권",
                MapLevel.Seoul => "서울 전역",
                MapLevel.Nation => "전국 · 해상·항공 자료 관찰",
                _ => viewedDong == null ? "동네 지도 · 상세 도로" : viewedDong.name + " 주변 · 상세 도로"
            };
            var description = mapLevel switch
            {
                MapLevel.District => GuReady ? "중랑·동대문·광진 · 밝은 곳이 운영하는 곳" : "중랑·동대문·광진·노원 도로 표본 · 관찰용",
                MapLevel.Seoul => "Natural Earth 서울 개략 경계 · 관찰용",
                MapLevel.Nation => "관세청 2025 · 통계 관계 · 관찰용",
                _ => viewedDong == null ? "사가정 배차 · 서울 동북권 표본 도로" :
                    viewedDong.district + (selectedCityBoard != null ? " · 도로·건물·녹지 표본" : " · 법정동 주변 도로 표본")
            };
            Text(new Rect(351, 612, 610, 22), title, 16, Paper, true);
            Text(new Rect(688, 164, 250, 18), description, 11, Soft);
            if (roadTexture != null) DrawDongPickerButton();
            if (mapLevel == MapLevel.Dong)
            {
                DrawCityBoardControls();
                Line(new Vector2(948, 154), new Vector2(976, 154), Gold, 3, true);
                Text(new Rect(981, 142, 75, 24), "매장 이동", 12, Soft);
                Line(new Vector2(1064, 154), new Vector2(1091, 154), Mint, 3);
                Text(new Rect(1097, 142, 137, 24), "배송지 전달", 12, Soft);
            }
            if (roadTexture == null) return;

            var scaleHint = mapLevel switch
            {
                MapLevel.Dong when AtDongFloor => $"동네 전체  ·  더 축소하면 구 지도 안내  ·  {mapZoom:P0}",
                MapLevel.Dong => $"휠 확대·축소  ·  드래그 이동  ·  {mapZoom:P0}",
                MapLevel.District => "구 이름을 눌러 법정동 보기",
                MapLevel.Nation => "휠 확대·축소  ·  끌어서 이동",
                _ => "휠을 올려 더 자세히 보기"
            };
            Text(new Rect(948, 164, 284, 18), scaleHint, 12, Soft,
                align: TextAnchor.MiddleRight);
            var count = state.orders.Count(order => order.stage == "Settled");
            var hint = CampaignPolicyOpen ? "3일차 마감 · 다음 운영 방침을 확정하면 시계가 이어집니다." :
                state.campaign?.phase == "closed" ? "영업 마감 · 다음 날 11:30에 새 주문이 열립니다." :
                state.settled ? "정산 완료 · 배차 데스크에서 결산을 확인하세요." :
                !running ? "운영을 시작하면 가상 시계와 주문이 움직입니다." :
                !HasWaitingOrder() ? "기사 이동, 고객 수령과 정산이 진행됩니다." :
                selectedDriver != null ? "기사 " + Letter(selectedDriver) + " 선택됨 · 노란 주문 매장을 누르세요." :
                "기사의 빛을 누른 뒤 주문이 있는 매장을 누르세요.";
            if (mapLevel == MapLevel.Nation && NationPausesGameClock)
                hint = "자료를 보는 동안 게임 시계가 멈춥니다 · 상세 지도로 돌아가면 이어집니다.";
            else if (mapLevel != MapLevel.Dong)
                hint = "배달은 계속 진행됩니다 · 상세 지도로 돌아가면 마커를 선택할 수 있습니다.";
            else if (tileIndex != null && visibleTiles.Count == 0)
                hint = "이 위치에 표시할 도로 표본이 없습니다 · 지도를 이동해 주세요.";
            else if (!state.settled && ((viewedDong != null && viewedDong.id != "region:kr:bjd:1126010100") ||
                IsMyeonmokStationOnBoard()))
                hint = "주변 도로를 보고 있습니다 · 사가정 배차는 배차 데스크에서 계속할 수 있습니다.";
            Text(new Rect(352, 637, 610, 20), mapNotice ?? hint, 12, Soft);
            DrawZoneButton();
            var total = new Rect(1103, 621, 130, 30);
            Paint(total, Canvas, 8);
            Text(total, $"정산  {count} / {state.orders.Length}", 13, Mint, true, TextAnchor.MiddleCenter);
        }

        private void DrawBoard()
        {
            TouchLightMenu();            // 빛의 차림표가 열려 있으면 그 누름을 먼저 받는다 (SagajeongDispatchRoam.cs)
            BeginGlowFrame();            // 빛의 가운데 점은 맨 위에 모아 그린다 (SagajeongDispatchLightCrowd.cs)
            DrawDongExtent();
            DrawBuildingHighlight();
            DrawRoleBuildings();
            DrawManagedMarks();          // 회색 동네와 관리 건물: 옥상의 운영 표식
            DrawRoamers();               // 계속 돌아다니는 기사 (SagajeongDispatchRoam.cs)
            DrawNavRoute();              // 길찾기: 고른 길 · 걷는 구간 · 기사 빛 (SagajeongDispatchNavigation.cs)
            DrawCityBoardLandmark();
            DrawOrderRipples();          // r154: 착지 파동은 경로·표식보다 아래
            foreach (var courier in state.couriers)
                DrawRoute(courier);
            for (var index = 0; index < state.destinations.Length; index++)
            {
                var destination = state.destinations[index];
                var point = PlotScene(destination.latitude, destination.longitude);
                if (DrawDropLight(index, point)) continue;                  // 빛으로 보는 동안에는 번호 대신 빛
                var rect = Centered(point, 28, 28);
                Paint(rect, Rim, 14);
                Text(rect, (index + 1).ToString(), 13, Paper, true, TextAnchor.MiddleCenter);
                if (mapZoom < FarViewZoom) continue;   // 멀리 볼 때는 번호만
                var label = new Rect(point.x - 31, point.y - 38, 62, 18);
                Paint(label, Canvas, 7);
                Text(label, "배송지", 10, Soft, align: TextAnchor.MiddleCenter);
            }
            foreach (var store in state.stores)
                DrawStore(store);
            DrawCargoTruck();
            DrawSignalRipples();
            DrawOrderDrops();            // r154: 새 주문 핀과 "새 주문 +N"은 매장 위, 기사 아래
            foreach (var courier in state.couriers)
                DrawCourier(courier);
            DrawNoticeLights();          // 떴다 사라지는 알림: 그 자리의 빛이 잠깐 밝아짐 (SagajeongDispatchNotices.cs)
            DrawNoticeTags();            //   그 자리의 짧은 글
            DrawGlowName();              // 빛에 올리면 그때만 이름이 뜬다 (SagajeongDispatchRoam.cs)
            // 표식이 먼저다. 표식이 아닌 곳을 눌렀을 때만 건물을 고른다 (2A-2).
            if (mapClick && !PickDongFocus())      // 디오라마 시설 모양을 누르면 집중 · 빈 곳이면 풀고 건물 고르기로(SagajeongDispatchDongFocus.cs)
            {
                PickBuilding(releasePoint);
                if (mapDoubleClick) OpenDongDeskAtPick();      // 땅이나 건물을 두 번 누르면 그 동의 관리 창 (SagajeongDispatchDongDesk.cs)
            }
        }

        private void DrawOverviewLabels()
        {
            var districtLevel = mapLevel == MapLevel.District;
            var pointerCovered = MapOverlayCovers(Event.current.mousePosition + Field.position);
            foreach (var label in overviewLabels)
            {
                var point = PlotOnBoard(label.latitude, label.longitude);
                Paint(Centered(point, 9, 9), Mint, 5);
                var plate = new Rect(point.x + 8, point.y - 25, 102, 23);
                var selected = dongPanelOpen && dongDistrict == label.name;
                Paint(plate, selected ? Rim : Canvas, 6);
                Text(plate, districtLevel ? label.name + " ›" : label.name, 12, districtLevel ? Mint : Paper,
                    true, TextAnchor.MiddleCenter);
                if (districtLevel && !pointerCovered && Hit(plate))
                    OpenDongPanel(label.name);
            }
        }

        private void DrawRoute(DispatchCourier courier)
        {
            var route = courier.route;
            if (route == null || route.Length < 2) return;
            var pickup = courier.phase == "ToPickup";
            for (var index = 1; index < route.Length; index++)
            {
                var a = PlotScene(route[index - 1].latitude, route[index - 1].longitude);
                var b = PlotScene(route[index].latitude, route[index].longitude);
                Line(a, b, Canvas, 7);
                Line(a, b, pickup ? Gold : RiderColor(courier.id), 4, pickup);
            }
        }

        private void DrawStore(DispatchStore store)
        {
            var position = PlotScene(store.latitude, store.longitude);
            var order = state.orders.FirstOrDefault(item => item.storeId == store.id);
            var available = AssignmentOpen && order?.stage == "Waiting";
            if (DrawStoreLight(store, position, available)) return;      // 빛으로 보는 동안에는 이름표 대신 빛 (SagajeongDispatchRoam.cs)
            var accent = available ? Gold : Paper;
            Paint(Centered(position, 14, 14), accent, 7);
            var center = position + new Vector2(0, -32);
            var plate = Centered(center, 94, 28);
            if (IsFocused(FocusKind.Store, store.id)) Paint(Centered(center, 102, 36), Paper, 10);
            Paint(plate, available ? Gold : Tile, 7);
            Text(plate, "매장 " + Letter(store.id), 14, available ? Canvas : Paper, true, TextAnchor.MiddleCenter);
            if (available)
            {
                var badge = new Rect(plate.x + 65, plate.y - 14, 40, 17);
                Paint(badge, Canvas, 5);
                Text(badge, "주문", 10, Gold, true, TextAnchor.MiddleCenter);
            }
            if (HitMapMarker(plate)) ConnectStore(store.id);
        }

        private void DrawCourier(DispatchCourier courier)
        {
            if (DioramaVisible && DongModulesLoaded && DongSoloView && !DongVisible(DongModuleAt(courier.latitude, courier.longitude))) return;
            var position = PlotScene(courier.latitude, courier.longitude);
            if (position == DongHiddenPoint) return;
            var chosen = IsFocused(FocusKind.Courier, courier.id) || courier.id == selectedDriver;
            if (chosen)
            {
                Paint(Centered(position, 48, 48), Paper, 24);
                Paint(Centered(position, 44, 44), Canvas, 22);
            }
            DrawCourierGlow(courier, position);
            DrawMindMarker(courier, position);
            TouchCourierLight(courier, position);
        }

        private static Rect Centered(Vector2 point, float width, float height) =>
            new(point.x - width / 2, point.y - height / 2, width, height);

        private static void Line(Vector2 a, Vector2 b, Color color, float width, bool dashed = false)
        {
            if (a.x < -50000f || b.x < -50000f) return;     // 숨긴 행정동의 점(DongHiddenPoint)과 잇는 선은 그리지 않는다
            var delta = b - a;
            var length = delta.magnitude;
            if (length <= 0.01f) return;
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(a, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            if (dashed)
            {
                for (float start = 0; start < length; start += 12)
                    Paint(new Rect(start, -width / 2, Mathf.Min(7, length - start), width), color);
            }
            else
                Paint(new Rect(0, -width / 2, length, width), color);
            GUI.matrix = original;
        }

        /// <summary>pivot을 원점으로 삼아 angle만큼 돌린 좌표계. 그 안의 사각형은 pivot에서 잰 자리로 그린다.</summary>
        private static Matrix4x4 TurnAt(Vector2 pivot, float angle) =>
            Matrix4x4.Translate(new Vector3(pivot.x + mapGroupShift.x, pivot.y + mapGroupShift.y, 0)) *
            Matrix4x4.Rotate(Quaternion.Euler(0, 0, angle)) *
            Matrix4x4.Translate(new Vector3(-mapGroupShift.x, -mapGroupShift.y, 0));

        /// <summary>pivot을 중심으로 돌린다. 그 안의 사각형은 원래 자리로 그린다.</summary>
        private static Matrix4x4 TurnAround(Vector2 pivot, Matrix4x4 turn)
        {
            var centre = new Vector3(pivot.x + mapGroupShift.x, pivot.y + mapGroupShift.y, 0);
            return Matrix4x4.Translate(centre) * turn * Matrix4x4.Translate(-centre);
        }
    }
}
