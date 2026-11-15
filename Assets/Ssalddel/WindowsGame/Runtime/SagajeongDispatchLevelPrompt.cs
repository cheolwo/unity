using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2A-1 (2026-11-12): 축소 범위와 화면 전환.
    // - 동네 지도는 고정 70% 대신 "동네 전체(2.5D 자료 범위)가 지도 영역에 들어오는 배율"까지 축소된다.
    // - 그 한계에서 더 축소하면 바로 넘어가지 않고 알림을 띄운다. 휠을 계속 내려 막대를 채우거나
    //   버튼을 누르면 한 단계 넓은 지도로 간다. 휠을 올리거나 잠시 손을 떼면 취소된다.
    // - 넓은 지도에 있는 동안에도 배달은 계속 진행되고, 위쪽 띠로 그 사실을 알린다.
    // 1차는 법정동 경계가 없어 2.5D 자료 사각형을 "동네"로 쓴다. 법정동 팩이 들어오면 경계로 바꾼다.
    public sealed partial class SagajeongDispatchView
    {
        private const float LegacyMinMapZoom = 0.7f;     // 2.5D 자료가 없을 때 쓰는 이전 한계
        private const float DongZoomLowest = 0.12f;      // 성능을 위한 바닥. 이보다 더 축소하지 않는다
        private const float DongFitMargin = 0.08f;       // 동네 전체 보기에서 지도 가장자리에 남길 여백(비율)
        // 원근 보기에서는 화면 아래쪽(가까운 쪽)이 더 크게 보여 여백을 더 둔다 (3D 기반 A6).
        private float DongFitMarginNow => dioramaOn && dioramaPerspective ? DongFitMargin + DioramaPerspectiveMargin : DongFitMargin;
        private const float FarViewZoom = 0.5f;          // 이보다 멀리 보면 자료 범위 테두리를 그린다
        private const int LevelPromptSteps = 3;          // 막대를 채우는 휠 칸 수 (알림을 여는 칸 포함)
        private const double LevelPromptStepGap = 0.18;  // 관성 스크롤이 한꺼번에 채우지 않도록 칸 사이 최소 간격(초)
        private const double LevelPromptIdle = 4.0;      // 이만큼 휠이 없고 알림 위에 마우스가 없으면 거둔다
        private static readonly Rect LevelPromptPanel = new(430, 444, 420, 104);
        private static readonly Rect LevelBanner = new(400, 94, 480, 34);

        private bool levelPromptOpen;
        private MapLevel levelPromptTarget;
        private int levelPromptProgress;
        private int levelPromptShown;
        private double levelPromptLastStep;
        private double levelPromptLastWheel;

        private bool LevelPromptCovers(Vector2 point) => levelPromptOpen && LevelPromptPanel.Contains(point);

        private bool LevelBannerCovers(Vector2 point) => mapLevel != MapLevel.Dong && LevelBannerRect.Contains(point);

        private bool AtDongFloor => mapLevel == MapLevel.Dong && mapZoom <= DongMinZoom + 0.0005f;

        // 동네 전체 보기에서 동네가 들어와야 하는 화면 부분(논리 좌표). 확장 화면은 위 머리줄과 아래 운영 카드를 뺀다.
        private Rect DongFitArea => expansionView && !mapOnly ? new Rect(0, 92, Width, 560) : new Rect(0, 0, Width, Height);

        // 화면에서 옮긴 거리 → 평면 지도 이동량. 2.5D가 아직 그려지기 전(지도 단계를 막 바꾼 때)에도 쓰도록 기울기를 받는다.
        private Vector2 FlatDeltaFor(bool tilted, Vector2 screenDelta)
        {
            if (!tilted) return screenDelta;
            var (x, y) = dioramaLens.FlatDelta(screenDelta.x, screenDelta.y);
            return new Vector2((float)x, (float)y);
        }

        // 동네 전체가 지도 영역에 들어오는 배율. 2.5D면 카메라 각도로 돌리고 눌러서 잰다.
        private float DongMinZoom
        {
            get
            {
                if (dioramaRoot == null || metresPerPixel <= 0 || dioramaEast <= dioramaWest || dioramaNorth <= dioramaSouth)
                    return LegacyMinMapZoom;
                var width = (float)((dioramaEast - dioramaWest) * longitudeScale);
                var depth = (float)((dioramaNorth - dioramaSouth) * 111200);
                float spanX, spanY;
                if (dioramaOn)
                {
                    var (x, y) = BaseLens.ScreenSpan(width, depth);      // 동네 전체는 멀리서 본다: 바탕 시점
                    spanX = (float)x;
                    spanY = (float)y;
                }
                else
                {
                    spanX = width;
                    spanY = depth;
                }
                var usable = 1f - 2f * DongFitMarginNow;
                var metres = (float)metresPerPixel;
                var area = DongFitArea;
                var zoom = Mathf.Min(area.width * usable * metres / spanX, area.height * usable * metres / spanY);
                return Mathf.Clamp(zoom, DongZoomLowest, LegacyMinMapZoom);
            }
        }

        // 창 크기를 바꾸거나 2.5D를 끄고 켜서 한계가 올라가면 지금 배율을 한계까지 끌어올린다.
        private void KeepDongZoomFloor()
        {
            if (mapLevel != MapLevel.Dong) return;
            var floor = DongMinZoom;
            if (mapZoom >= floor - 0.0005f) return;
            var centre = Field.size * 0.5f;
            mapOffset = ClampDioramaOffset(ClampMapOffset(centre - (centre - mapOffset) * (floor / mapZoom), floor), floor);
            mapZoom = floor;
            localTextureDirty = true;
        }

        // 동네 전체 보기: 한계 배율로, 자료 범위 한가운데를 화면 가운데에 둔다.
        private void ShowWholeDong()
        {
            // 한 동만 보는 중에는 그 동이 곧 전체다. 그 동에 맞춘다(동 사업 칸도 그대로 뜬다).
            var chosen = selectedBuilding;       // 동에 맞추면 고른 건물이 풀리므로 되돌려 둔다
            // 역 중심 보기에서는 역 판이 곧 전체다.
            if (StationView && mapLevel == MapLevel.Dong && FocusStation())
            {
                selectedBuilding = chosen;
                return;
            }
            if (TidyScreen && DongFlagsActive && dongView == DongView.Solo && soloRegion != null && mapLevel == MapLevel.Dong &&
                FocusDongModule(soloRegion))
            {
                selectedBuilding = chosen;
                return;
            }
            ClearDongFocus();          // 동네 전체 보기에서는 어느 행정동도 고르지 않는다
            mapZoom = DongMinZoom;
            SyncCloseLens();
            if (dioramaRoot != null && metresPerPixel > 0)
            {
                var metres = (float)metresPerPixel / mapZoom;
                var east = (float)(((dioramaWest + dioramaEast) * 0.5 - centerLongitude) * longitudeScale);
                var north = (float)(((dioramaSouth + dioramaNorth) * 0.5 - centerLatitude) * 111200);
                var centre = Field.size * 0.5f;
                mapOffset = new Vector2(centre.x * (1f - mapZoom) - east / metres, centre.y * (1f - mapZoom) + north / metres);
                // 자료 한가운데를 지도 영역 가운데가 아니라 가려지지 않는 부분(DongFitArea)의 가운데에 둔다.
                mapOffset += FlatDeltaFor(dioramaOn, DongFitArea.center - Field.position - centre);
            }
            else
                mapOffset = ClampMapOffset(mapOffset, mapZoom);
            dongZoomFloorVisible = false;
            localTextureDirty = true;
        }

        // 한계에서 휠을 한 칸 더 내렸다. 넘어가야 하면 true.
        private bool AdvanceLevelPrompt(MapLevel target, double now)
        {
            levelPromptLastWheel = now;
            if (!levelPromptOpen || levelPromptTarget != target)
            {
                levelPromptOpen = true;
                levelPromptTarget = target;
                levelPromptProgress = 1;
                levelPromptLastStep = now;
                levelPromptShown++;
                Debug.Log("Hongdal map level prompt: open → " + target);
                return false;
            }
            if (now - levelPromptLastStep < LevelPromptStepGap) return false;
            levelPromptLastStep = now;
            levelPromptProgress++;
            if (levelPromptProgress < LevelPromptSteps) return false;
            CloseLevelPrompt();
            return true;
        }

        private void CloseLevelPrompt()
        {
            levelPromptOpen = false;
            levelPromptProgress = 0;
        }

        // 한 단계 넓은(+1) 또는 좁은(-1) 지도로 옮긴다. 동네로 돌아오면 동네 전체 보기로 시작한다.
        private void ChangeMapLevel(int direction)
        {
            CloseLevelPrompt();
            var previousLevel = mapLevel;
            var level = Mathf.Clamp((int)previousLevel + direction, (int)MapLevel.Dong, (int)MapLevel.Nation);
            if (level != (int)previousLevel) LoadMapLevel((MapLevel)level);
            if (mapLevel == previousLevel) return;
            if (mapLevel == MapLevel.Dong) ShowWholeDong();
            lastWheelLevelDirection = direction;
            wheelLevelBlockedUntil = Time.realtimeSinceStartupAsDouble + WheelLevelCooldown;
        }

        private void ReturnToDong()
        {
            CloseLevelPrompt();
            if (mapLevel == MapLevel.Dong) return;
            LoadMapLevel(MapLevel.Dong);
            if (mapLevel == MapLevel.Dong) ShowWholeDong();
        }

        private void DrawLevelPrompt()
        {
            if (!levelPromptOpen) return;
            var hovering = LevelPromptPanel.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint && !hovering &&
                Time.realtimeSinceStartupAsDouble - levelPromptLastWheel > LevelPromptIdle)
            {
                CloseLevelPrompt();
                return;
            }

            var (place, next) = levelPromptTarget switch
            {
                MapLevel.Seoul => ("구 지도", "서울 지도"),
                MapLevel.Nation => ("서울 지도", "전국 지도"),
                _ => ("동네", "구 지도")
            };
            // 넓은 지도로 가도 배달은 이어진다(r162 공통 시간 계약). 전국 지도 정지 정책을 켰을 때만 문구가 바뀐다.
            var keeps = levelPromptTarget == MapLevel.Nation && NationPausesGameClock ? "자료를 보는 동안 게임 시계는 멈춥니다" : "배달은 그대로 진행됩니다";
            Paint(LevelPromptPanel, Rim, 11);
            Paint(new Rect(LevelPromptPanel.x + 1, LevelPromptPanel.y + 1, LevelPromptPanel.width - 2,
                LevelPromptPanel.height - 2), Canvas, 10);
            var x = LevelPromptPanel.x + 18;
            var y = LevelPromptPanel.y;
            if (levelPromptShown <= 1)
            {
                Text(new Rect(x, y + 12, 384, 22), place + " 전체를 보고 있습니다", 15, Paper, true);
                Text(new Rect(x, y + 36, 384, 20), "계속 축소하면 " + next + "로 이동합니다 · " + keeps, 12, Soft);
            }
            else
            {
                Text(new Rect(x, y + 12, 384, 22), "계속 축소하면 " + next + "로", 15, Paper, true);
                Text(new Rect(x, y + 36, 384, 20), "휠을 올리면 취소됩니다", 12, Soft);
            }

            for (var step = 0; step < LevelPromptSteps; step++)
                Paint(new Rect(x + step * 38, y + 70, 32, 8), step < levelPromptProgress ? Mint : Tile, 4);
            if (ExpansionButton(new Rect(LevelPromptPanel.xMax - 214, y + 62, 108, 28), next + "로", true, Mint))
                ChangeMapLevel(1);
            else if (ExpansionButton(new Rect(LevelPromptPanel.xMax - 100, y + 62, 82, 28), "머무르기", true, Tile))
                CloseLevelPrompt();
        }

        // 구·서울 지도에 있을 때 위쪽 띠. 배달이 계속되고 있음을 알리고, 누르면 동네 전체 보기로 돌아간다.
        private void DrawLevelBanner()
        {
            if (mapLevel == MapLevel.Dong || state == null) return;
            if (mapLevel == MapLevel.Nation)
            {
                DrawNationBanner();
                return;
            }
            var waiting = state.orders.Count(order => order.stage == "Waiting");
            var settled = state.orders.Count(order => order.stage == "Settled");
            Paint(LevelBanner, Rim, 9);
            Paint(new Rect(LevelBanner.x + 1, LevelBanner.y + 1, LevelBanner.width - 2, LevelBanner.height - 2), Canvas, 8);
            Text(new Rect(LevelBanner.x + 14, LevelBanner.y, 330, LevelBanner.height),
                $"사가정 배달 운영 중 · 배차 대기 {waiting}건 · 정산 {settled}건", 12, Paper);
            var back = new Rect(LevelBanner.xMax - 122, LevelBanner.y + 4, 114, 26);
            if (ExpansionButton(back, "동네로 돌아가기", true, Mint)) ReturnToDong();
        }

        // 멀리 볼 때 동네 범위를 점선으로 보인다. 1차는 2.5D 자료 사각형이다 (법정동 경계 아님).
        private void DrawDongExtent()
        {
            if (DongModulesLoaded)
            {
                DrawDongModuleBoundaries();      // 행정동 모듈의 경계와 이름 (SagajeongDispatchDongModules.cs)
                return;
            }
            if (mapLevel != MapLevel.Dong || mapZoom >= FarViewZoom || dioramaRoot == null) return;
            var corners = new[]
            {
                PlotScene(dioramaNorth, dioramaWest), PlotScene(dioramaNorth, dioramaEast),
                PlotScene(dioramaSouth, dioramaEast), PlotScene(dioramaSouth, dioramaWest)
            };
            for (var index = 0; index < corners.Length; index++)
                Line(corners[index], corners[(index + 1) % corners.Length], Soft, 2, true);
            var top = corners.OrderBy(point => point.y).First();
            var plate = new Rect(top.x - 92, Mathf.Max(top.y + 8, 96 - Field.y), 184, 22);
            Paint(plate, Canvas, 6);
            Text(plate, "사가정 일대 · 2.5D 자료 범위", 11, Soft, true, TextAnchor.MiddleCenter);
        }
    }
}
