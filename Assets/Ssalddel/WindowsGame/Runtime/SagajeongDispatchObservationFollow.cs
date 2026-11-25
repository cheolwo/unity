using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private bool 관찰따라가는중;
        private int 추적프레임 = -1;
        private (double Latitude, double Longitude)? 최근추적위치;

        private void 관찰지도수동이동()
        {
            관찰따라가는중 = false;
            최근추적위치 = null;
            추적프레임 = -1;
        }

        private (double Latitude, double Longitude)? 관찰장소위치(string 장소Id)
        {
            if (string.IsNullOrEmpty(장소Id)) return null;
            LoadRoleBook();
            return buildingByPlace.TryGetValue(장소Id, out var 건물) ? PlaceLatLon(건물) : null;
        }

        private (double Latitude, double Longitude)? 관찰위치읽기()
        {
            if (state == null || mapLevel != MapLevel.Dong || 관찰대상 is not { } 대상) return null;
            switch (대상.Kind)
            {
                case FocusKind.Store:
                    var 매장 = state.stores?.FirstOrDefault(s => s?.id == 대상.Id);
                    return 매장 == null ? null : (매장.latitude, 매장.longitude);
                case FocusKind.Courier:
                    var 기사 = state.couriers?.FirstOrDefault(c => c?.id == 대상.Id);
                    return 기사 == null ? null : (기사.latitude, 기사.longitude);
                case FocusKind.Center:
                    return 관찰장소위치(ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.CenterPlaceId);
                case FocusKind.Inspection:
                    var 매장업무 = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
                    return 단일물류행(매장업무?.Receipts, 대상.Id, r => r.Id) == null ? null : 관찰장소위치(매장업무.StorePlaceId);
                case FocusKind.Cargo:
                    return 관찰차량위치();
                default:
                    return null;
            }
        }

        private (double Latitude, double Longitude)? 관찰차량위치()
        {
            if (string.IsNullOrEmpty(관찰운행Id) || !LayerVisible(BusinessCargo)) return null;
            var 운송 = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport);
            var 운행 = 단일물류행(운송?.Trips, 관찰운행Id, t => t.Id);
            if (운행 == null) return null;
            // 동 지도에 표시하는 화물차는 첫 사슬에 한 대다. 다른 운행의 차로 안내하지 않는다.
            var 발주 = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders.LastOrDefault();
            var 표시운행 = 발주 == null ? null : 운송.Trips.LastOrDefault(t => t.OrderId == 발주.OrderId);
            if (표시운행?.Id != 운행.Id) return null;
            LoadRoleBook();
            var 길 = roleBook?.routes?.FirstOrDefault();
            if (길?.latlon == null || 길.latlon.Length < 4 || 길.latlon.Length % 2 != 0 || 길.toPlaceId != 운행.ToPlaceId) return null;
            if (운행.Stage == 운송.ArrivedStage) return AlongRoute(길.latlon, 1f);
            if (운행.Stage != 운송.LoadedStage || 운행.ArriveAtMs <= 운행.DepartedAtMs) return null;
            var 진행률 = (BusinessNow - 운행.DepartedAtMs) / (float)(운행.ArriveAtMs - 운행.DepartedAtMs);
            return 진행률 < 0.5f ? null : AlongRoute(길.latlon, Mathf.Clamp01((진행률 - 0.5f) * 2f));
        }

        private static bool 관찰위치유효((double Latitude, double Longitude)? 위치) =>
            위치 is { } 점 && 점.Latitude >= -90 && 점.Latitude <= 90 && 점.Longitude >= -180 && 점.Longitude <= 180 &&
            (점.Latitude != 0 || 점.Longitude != 0);

        private bool 관찰위치맞추기((double Latitude, double Longitude)? 위치)
        {
            if (!관찰위치유효(위치) || metresPerPixel <= 0 || mapZoom <= 0) return false;
            var 점 = 위치.Value;
            if (DioramaVisible && DongModulesLoaded)
            {
                var 동 = DongModuleAt(점.Latitude, 점.Longitude);
                if (동 == null || !동.Loaded) return false;
                if (!DongVisible(동) || (StationView && !StationShows(점.Latitude, 점.Longitude)))
                {
                    // 역 판이 동의 일부만 담을 때는 동을 골라도 바깥의 기사가 계속 숨겨진다.
                    // 이 동만 보기로 열면 원래 판에서 숨긴 동 설정도 그대로 보존한다.
                    var 기존확대 = mapZoom;
                    if (dongView != DongView.Solo)
                    {
                        focusDongRegion = 동.Entry.regionId;
                        SetDongView(DongView.Solo);
                    }
                    else if (!FocusDongModule(동.Entry.regionId)) return false;
                    mapZoom = 기존확대;
                    SyncCloseLens();
                    if (!DongVisible(동) || StationView) return false;
                }
                var 이동 = DongShift(동);
                점.Latitude += 이동.y / 111200.0;
                점.Longitude += 이동.x / longitudeScale;
            }
            MoveViewTo(점.Latitude, 점.Longitude);
            return true;
        }

        private bool 관찰위치보기()
        {
            관찰지도수동이동();
            return 관찰위치맞추기(관찰위치읽기());
        }

        private bool 관찰다시따라가기()
        {
            var 위치 = 관찰위치읽기();
            관찰따라가는중 = 관찰위치맞추기(위치);
            최근추적위치 = 관찰따라가는중 ? 위치 : null;
            return 관찰따라가는중;
        }

        private void 관찰따라가기갱신()
        {
            if (!관찰따라가는중) return;
            if (관찰대상 is not { } 대상 || !IsFocused(대상.Kind, 대상.Id))
            {
                관찰지도수동이동();
                return;
            }
            if (추적프레임 == Time.frameCount) return;
            추적프레임 = Time.frameCount;
            var 위치 = 관찰위치읽기();
            if (!관찰위치유효(위치)) 관찰지도수동이동();
            else if (위치 != 최근추적위치)
            {
                if (관찰위치맞추기(위치)) 최근추적위치 = 위치;
                else 관찰지도수동이동();
            }
        }

        private void 관찰이동단추그리기(float x, float y)
        {
            var 가능 = 관찰위치유효(관찰위치읽기());
            if (ExpansionButton(new Rect(x, y, 79, 25), 가능 ? "위치 보기" : "지도 위치 없음", 가능, Tile)) 관찰위치보기();
            if (ExpansionButton(new Rect(x + 83, y, 94, 25), 관찰따라가는중 ? "따라가는 중" : "다시 따라가기", 가능 && !관찰따라가는중, Tile))
                관찰다시따라가기();
        }
    }
}
