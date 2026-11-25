using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 2026-11-14 한 동만 보기를 기본으로. 사용자 방향: 여러 동이 함께 보이면 몰입하기 어렵다. 행정동 하나만 보이는 것이 기본값이다
    // (r182 「한 행정동에 집중」과 같은 방향. 붙여 보기·띄워 보기는 설정 칸의 「동 보기」에 그대로 있다).
    //  - 행정동 깃발이 켜진 실행에서 동네 2.5D가 처음 보일 때 시작 동(매장 A·B가 있는 동) 하나만 보이게 하고 그 동에 맞춘다.
    //  - 다른 동으로 넘어가는 길: 동 사업 칸 양옆의 화살표, 신호 줄의 「가서 보기」, 다른 동의 건물·매장을 고를 때, 결정 순회.
    //  - 보이지 않는 동의 표식(매장·기사·주문)은 그리지 않는다(이 동만 보기의 기존 동작).
    // 같은 날 뒤에 사용자 방향이 더해졌다: 공공 자료는 행정동·법정동으로 돼 있으니 지도를 굽는 것은 그 자료로 하되,
    // 게임에서 관점을 잡을 때는 역 중심으로 보여 준다. 그래서 처음 보기는 시작 동이 든 역의 판이다(SagajeongDispatchStationView.cs).
    // 그 판을 열 수 없을 때만 예전처럼 시작 동 하나를 보인다. 「이 동만」은 설정 칸의 「동 보기」에 그대로 있다.
    // 보기만 바꾼다. 시계·업무 상태는 건드리지 않는다. 검증 촬영(--sagajeong-…)에는 행정동 모듈이 없어 해당하지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private bool dongSoloPrimed;

        private bool DongSoloView => dongView == DongView.Solo;

        // 영역 사본을 읽은 뒤(Layout) 부른다. 준비가 될 때까지 기다렸다가 한 번만 한다.
        private void PrimeDongSolo()
        {
            if (dongSoloPrimed || !TidyScreen || !DongFlagsActive) return;
            if (mapLevel != MapLevel.Dong || !DioramaVisible || !DongModulesLoaded || metresPerPixel <= 0) return;
            dongSoloPrimed = true;
            var home = dongTerritory.Dongs.FirstOrDefault(zone => zone.IsHome && DongModuleById(HjdRegionPrefix + zone.Code)?.Loaded == true);
            if (home == null) return;
            var region = HjdRegionPrefix + home.Code;
            // 처음 보기: 시작 동의 건물이 가장 많이 든 역의 판. 사업 칸은 시작 동의 것을 보인다.
            var station = StationFor(region);
            if (station >= 0)
            {
                stationIndex = station;
                stationCardRegion = region;
                stationLayoutStamp = int.MinValue;
                dongView = DongView.Station;
                dongSpread = DongSpreadTarget;
                RecomputeDongBounds();
                if (FocusStation())
                {
                    Debug.Log("Hongdal station view default: " + Station.Name + " for " + region + " " + home.Name);
                    return;
                }
            }
            dongView = DongView.Solo;
            soloRegion = region;
            dongSpread = DongSpreadTarget;      // 처음에는 벌어졌다 모이는 움직임 없이 바로
            RecomputeDongBounds();
            if (!FocusDongModule(region)) ApplyDongDim();
            Debug.Log("Hongdal dong solo default: " + region + " " + home.Name);
        }

        // 한 동만 보는 중에 다른 동의 것을 고르면 그 동으로 넘어간다. 고르기 전에 부른다(동에 맞추면 고른 건물이 풀린다).
        private void ShowDongOf(string regionId)
        {
            if (!DongSoloView || string.IsNullOrEmpty(regionId) || regionId == soloRegion) return;
            var module = DongModuleById(regionId);
            if (module == null || !module.Loaded || mapLevel != MapLevel.Dong) return;
            FocusDongModule(regionId);
        }

        // 그 자리(매장·건물)가 든 동으로.
        private void ShowDongOfPlace(string place)
        {
            if (!DongSoloView || string.IsNullOrEmpty(place)) return;
            if (place.StartsWith("store:", System.StringComparison.Ordinal) && state?.stores != null)
            {
                var id = place.Substring(6);
                var store = state.stores.FirstOrDefault(item => item.id == id);
                if (store != null) ShowDongOf(DongModuleAt(store.latitude, store.longitude)?.Entry.regionId);
                return;
            }
            if (buildingByPlace.TryGetValue(place, out var index)) ShowDongOf(buildingShapes[index].Record.region);
        }

        // 동 사업 칸의 화살표: 읽은 동을 차례로 넘겨 본다.
        private void StepDongSolo(int step)
        {
            관찰지도수동이동();
            var regions = dongModules.Where(module => module.Loaded).Select(module => module.Entry.regionId).ToList();
            if (regions.Count < 2) return;
            var at = regions.IndexOf(soloRegion ?? focusDongRegion);
            if (at < 0) at = 0;
            FocusDongModule(regions[(at + step + regions.Count) % regions.Count]);
        }

        // 동 사업 칸 양옆의 화살표. GUI.BeginGroup(Field) 안의 좌표. 눌렀으면 true(동이 바뀌었으니 이번 그리기는 그만둔다).
        private bool DrawDongSoloArrows(Rect card)
        {
            if (!DongSoloView) return false;
            var prev = new Rect(card.x - 30, card.y + 6, 24, 24);
            var next = new Rect(card.xMax + 6, card.y + 6, 24, 24);
            Paint(prev, Rim, 6);
            Text(prev, "◀", 11, Paper, true, TextAnchor.MiddleCenter);
            Paint(next, Rim, 6);
            Text(next, "▶", 11, Paper, true, TextAnchor.MiddleCenter);
            if (HitMapMarker(prev))
            {
                StepDongSolo(-1);
                return true;
            }
            if (HitMapMarker(next))
            {
                StepDongSolo(1);
                return true;
            }
            return false;
        }
    }
}
