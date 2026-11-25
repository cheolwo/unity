using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect 역할입구칸 = new(858, 666, 150, 32);
        private static readonly Rect 역할목록칸 = new(22, 130, 300, 406);
        private const float 역할행간격 = 45f;
        private readonly List<역할관찰행> 역할후보 = new();
        private bool 역할목록열림;
        private float 역할스크롤;
        private static readonly Rect 관찰띠칸 = new(330, 92, 646, 78);
        private (FocusKind Kind, string Id)? 관찰대상, 직전관점;
        private string 관찰주문Id;
        private bool 관찰주문목록;
        private readonly List<DispatchOrder> 고를주문 = new();
        private 관찰시작화면 관찰복귀점;

        private sealed class 관찰시작화면
        {
            public DongView 보기;
            public string 동, 단독동, 역카드동, 매장창;
            public int 역, 사업;
            public float 확대;
            public Vector2 이동;
            public (FocusKind Kind, string Id)? 대상;
            public int? 건물;
            public bool 상세, 카드, 동관리, 현황, 사업창, 보고, 동패널, 구역패널;
        }

        private bool 역할입구가능 => expansionView && !mapOnly && !showDesk && mapLevel == MapLevel.Dong && state != null;
        private bool 역할목록표시 => 역할목록열림 && 역할입구가능 && !sidePanelsOpen && !DongDeskVisible && !StoreDeskVisible;
        private bool 관찰띠표시 => 역할입구가능 && 관찰대상 is { } 대상 &&
            (autopilotOn || !actorFocus.HasValue || IsFocused(대상.Kind, 대상.Id));

        private readonly struct 역할관찰행
        {
            public readonly FocusKind 종류;
            public readonly string 대상Id;
            public readonly string 직업;
            public readonly string 이름;
            public readonly string 상황;

            public 역할관찰행(FocusKind 종류, string 대상Id, string 직업, string 이름, string 상황)
            {
                this.종류 = 종류;
                this.대상Id = 대상Id;
                this.직업 = 직업;
                this.이름 = 이름;
                this.상황 = 상황;
            }
        }

        private void 역할목록열기()
        {
            if (!역할입구가능) return;
            관찰시작점기억();
            관찰주문목록 = false;
            dongDeskOpen = false;
            storeDeskPlace = null;
            sidePanelsOpen = false;
            expansionPanelOpen = false;
            dailyReportOpen = false;
            역할목록열림 = true;
            역할스크롤 = 0;
        }

        private void 역할입구그리기()
        {
            var tab = 역할입구칸;
            Paint(tab, Lilac, 7);
            Paint(new Rect(tab.x + 1, tab.y + 1, tab.width - 2, tab.height - 2), 역할목록표시 ? Ink(0x352B49) : Ink(0x282335), 6);
            Text(new Rect(tab.x + 12, tab.y, 90, tab.height), "역할로 보기", 13, Paper, true);
            Text(new Rect(tab.xMax - 45, tab.y, 34, tab.height), "관찰", 10, Lilac, align: TextAnchor.MiddleRight);
            if (!Hit(tab)) return;
            if (역할목록표시) 역할목록열림 = false;
            else 역할목록열기();
        }

        private static string 배달관찰상황(DispatchOrder order, string 대기)
        {
            if (order == null) return 대기;
            if (order.stage == "Settled") return 대기 + " · 최근 정산 완료";
            var 상태 = order.stage == "AtPickup" ? "기사 도착 · 픽업 전" : StageLabel(order.stage);
            return 상태 + " · " + PhoneOrderNumber(order.id);
        }

        private void 관찰대상없음(FocusKind 종류, string 직업)
        {
            역할후보.Add(new 역할관찰행(종류, null, 직업, null, "현재 볼 대상 없음"));
        }

        private void 역할후보갱신(DongModuleState dong)
        {
            역할후보.Clear();
            if (dong != null && state?.stores != null)
                foreach (var store in state.stores)
                {
                    if (store == null || string.IsNullOrEmpty(store.id) || DongModuleAt(store.latitude, store.longitude) != dong) continue;
                    역할후보.Add(new 역할관찰행(FocusKind.Store, store.id, "음식점", store.name ?? store.id,
                        배달관찰상황(PhoneStoreOrder(store.id), "새 주문 대기")));
                }
            if (역할후보.Count == 0) 관찰대상없음(FocusKind.Store, "음식점");

            var 가게행수 = 역할후보.Count;
            if (dong != null && state?.couriers != null)
                foreach (var courier in state.couriers)
                {
                    if (courier == null || string.IsNullOrEmpty(courier.id) || DongModuleAt(courier.latitude, courier.longitude) != dong) continue;
                    역할후보.Add(new 역할관찰행(FocusKind.Courier, courier.id, "음식 배달기사", courier.label ?? courier.id,
                        배달관찰상황(PhoneOrderOf(courier.id), "배차 대기")));
                }
            if (역할후보.Count == 가게행수) 관찰대상없음(FocusKind.Courier, "음식 배달기사");

            var 차량 = engine?.Pack?.transport;
            var 운송 = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport);
            if (운송 == null || string.IsNullOrEmpty(차량?.vehicleId)) 관찰대상없음(FocusKind.Cargo, "화물 기사");
            else
            {
                var 운행 = 운송.Trips.LastOrDefault();
                역할후보.Add(new 역할관찰행(FocusKind.Cargo, "cargo", "화물 기사", 차량.vehicleName,
                    운행 == null ? "배차 대기" : CargoStage(운송, 운행)));
            }

            var 센터 = ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter);
            if (string.IsNullOrEmpty(센터?.CenterPlaceId)) 관찰대상없음(FocusKind.Center, "물류센터 출고");
            else
            {
                var 상태 = 센터.Outbounds.LastOrDefault()?.Stage switch
                {
                    null => "출고 요청 대기",
                    OutboundStages.Reserved => "피킹 작업 중",
                    OutboundStages.Picked => "포장 완료 · 상차 대기",
                    OutboundStages.Loaded => "출고 인계 완료",
                    _ => "상태 미확인",
                };
                역할후보.Add(new 역할관찰행(FocusKind.Center, "center", "물류센터 출고", 센터.CenterName, 상태));
            }
        }

        private void 역할휴대폰열기(역할관찰행 행)
        {
            if (string.IsNullOrEmpty(행.대상Id)) return;
            if (행.종류 is FocusKind.Store or FocusKind.Courier)
            {
                if (관찰대상 is { } 현재 && 현재.Kind == 행.종류 && 현재.Id == 행.대상Id && 관찰주문Id != null)
                {
                    역할목록열림 = false;
                    관찰주문목록 = false;
                    return;
                }
                관찰시작점기억();
                관찰지도수동이동();
                물류관찰키비우기();
                관찰대상 = (행.종류, 행.대상Id);
                직전관점 = null;
                관찰주문Id = null;
                관찰주문후보읽기();
                if (고를주문.Count == 1) 관찰주문Id = 고를주문[0].id;
                관찰주문목록 = 고를주문.Count > 1;
                역할목록열림 = 관찰주문목록;
                역할스크롤 = 0;
                phoneKey = null;
                selectedBuilding = null;
                역할휴대폰초점(행.종류, 행.대상Id);
                return;
            }
            if (행.종류 is FocusKind.Center or FocusKind.Cargo)
            {
                물류관찰시작(행);
                return;
            }
            selectedBuilding = null;
            OpenRolePhone(행.종류, 행.대상Id);
        }

        private void 관찰시작점기억()
        {
            관찰복귀점 ??= new 관찰시작화면
            {
                보기 = dongView, 동 = focusDongRegion, 단독동 = soloRegion, 역카드동 = stationCardRegion,
                역 = stationIndex, 확대 = mapZoom, 이동 = mapOffset, 대상 = actorFocus, 건물 = selectedBuilding,
                상세 = showDetails, 카드 = phoneCardView, 동관리 = dongDeskOpen, 매장창 = storeDeskPlace,
                현황 = sidePanelsOpen, 사업창 = expansionPanelOpen, 사업 = expansionSelection, 보고 = dailyReportOpen,
                동패널 = dongPanelOpen, 구역패널 = zonePanelOpen,
            };
        }

        // 지도에서 새 대상을 직접 고르면 현재 위치에서 일반 보기를 계속한다.
        private void 관찰선택해제()
        {
            관찰지도수동이동();
            if (관찰대상.HasValue) phoneKey = null;
            관찰대상 = null;
            직전관점 = null;
            관찰주문Id = null;
            물류관찰키비우기();
            관찰주문목록 = false;
            관찰복귀점 = null;
        }

        private void 관찰선택동기화()
        {
            if (!관찰대상.HasValue)
            {
                if (!역할목록표시) 관찰복귀점 = null;
                return;
            }
            if (state == null || mapLevel != MapLevel.Dong) 관찰선택해제();
            else if (역할입구가능 && autopilotOn && (selectedBuilding.HasValue ||
                actorFocus.HasValue && !IsFocused(관찰대상.Value.Kind, 관찰대상.Value.Id)))
                관찰선택해제();
        }

        // 직접 배차할 때 가리킨 가게·기사는 업무 선택이다. 관찰로 돌아오면 처음 고른 원문을 다시 읽는다.
        private void 관찰업무복귀()
        {
            if (state == null || mapLevel != MapLevel.Dong || 관찰대상 is not { } 대상) return;
            if (!관찰객체있음(대상.Kind, 대상.Id)) return;
            if (!IsFocused(대상.Kind, 대상.Id)) phoneKey = null;
            actorFocus = 대상;
            phoneCardView = false;
            showDetails = true;
        }

        private void 역할관찰끝내기()
        {
            var 전 = 관찰복귀점;
            관찰선택해제();
            역할목록열림 = false;
            if (전 == null) return;
            dongView = 전.보기;
            focusDongRegion = 전.동;
            soloRegion = 전.단독동;
            stationCardRegion = 전.역카드동;
            stationIndex = 전.역;
            stationLayoutStamp = int.MinValue;
            RecomputeDongBounds();
            ApplyDongDim();
            mapZoom = 전.확대;
            mapOffset = 전.이동;
            SyncCloseLens();
            localTextureDirty = true;
            actorFocus = 전.대상;
            selectedBuilding = 전.건물;
            showDetails = 전.상세;
            phoneCardView = 전.카드;
            dongDeskOpen = 전.동관리;
            storeDeskPlace = 전.매장창;
            sidePanelsOpen = 전.현황;
            expansionPanelOpen = 전.사업창;
            expansionSelection = 전.사업;
            dailyReportOpen = 전.보고;
            dongPanelOpen = 전.동패널;
            zonePanelOpen = 전.구역패널;
        }

        private static bool 대상의주문(DispatchOrder 주문, FocusKind 종류, string 대상Id) =>
            주문 != null && (종류 == FocusKind.Store ? 주문.storeId == 대상Id : 종류 == FocusKind.Courier && 주문.driverId == 대상Id);

        private DispatchOrder 고정주문읽기() => string.IsNullOrEmpty(관찰주문Id) ? null :
            state?.orders?.FirstOrDefault(item => item != null && item.id == 관찰주문Id);

        private DispatchOrder 표시할배달주문(FocusKind 종류, string 대상Id)
        {
            if (관찰대상 is { } 대상 && 대상.Kind == 종류 && 대상.Id == 대상Id)
            {
                var 주문 = 고정주문읽기();
                return 대상의주문(주문, 종류, 대상Id) ? 주문 : null;
            }
            return 종류 == FocusKind.Store ? PhoneStoreOrder(대상Id) : PhoneOrderOf(대상Id);
        }

        private void 관찰주문후보읽기()
        {
            고를주문.Clear();
            if (관찰대상 is not { } 대상 || state?.orders == null) return;
            DispatchOrder 최근정산 = null;
            foreach (var 주문 in state.orders)
            {
                if (!대상의주문(주문, 대상.Kind, 대상.Id) || string.IsNullOrEmpty(주문.id)) continue;
                if (주문.stage != "Settled" || 주문.id == 관찰주문Id) 고를주문.Add(주문);
                else 최근정산 = 주문;
            }
            if (고를주문.Count == 0 && 최근정산 != null) 고를주문.Add(최근정산);
        }

        private bool 관찰주문선택(string 원주문Id)
        {
            관찰주문후보읽기();
            if (string.IsNullOrEmpty(원주문Id) || !고를주문.Any(item => item.id == 원주문Id)) return false;
            if (관찰주문Id != 원주문Id)
            {
                관찰주문Id = 원주문Id;
                직전관점 = null;
                phoneKey = null;
            }
            관찰주문목록 = false;
            역할목록열림 = false;
            return true;
        }

        private bool 관찰객체있음(FocusKind 종류, string id) => 종류 switch
        {
            FocusKind.Store => state?.stores?.Any(s => s?.id == id) == true,
            FocusKind.Courier => state?.couriers?.Any(c => c?.id == id) == true,
            FocusKind.Center => engine?.Pack?.center != null,
            FocusKind.Cargo => engine?.Pack?.transport != null,
            FocusKind.Inspection => 고른검수읽기()?.Id == id,
            _ => false,
        };

        private bool 관찰상대찾기(out (FocusKind Kind, string Id) 상대)
        {
            상대 = default;
            var 주문 = 고정주문읽기();
            if (관찰대상 is not { } 현재 || 주문 == null) return false;
            if (현재.Kind == FocusKind.Store && 주문.storeId == 현재.Id) 상대 = (FocusKind.Courier, 주문.driverId);
            else if (현재.Kind == FocusKind.Courier) 상대 = (FocusKind.Store, 주문.storeId);
            return !string.IsNullOrEmpty(상대.Id) && 관찰객체있음(상대.Kind, 상대.Id);
        }

        private bool 같은주문상대보기()
        {
            if (!관찰상대찾기(out var 상대)) return false;
            관찰지도수동이동();
            직전관점 = 관찰대상;
            관찰대상 = 상대;
            관찰주문목록 = false;
            역할목록열림 = false;
            역할휴대폰초점(상대.Kind, 상대.Id);
            return true;
        }

        private bool 이전관점보기()
        {
            if (직전관점 is not { } 전 || !관찰객체있음(전.Kind, 전.Id)) return false;
            관찰지도수동이동();
            관찰대상 = 전;
            직전관점 = null;
            관찰주문목록 = false;
            역할목록열림 = false;
            역할휴대폰초점(전.Kind, 전.Id);
            return true;
        }

        private string 관찰주문안내(FocusKind 종류, string 대상Id)
        {
            if (!관찰객체있음(종류, 대상Id)) return "고른 대상의 자료를 찾을 수 없습니다";
            if (관찰주문Id == null)
                return state?.orders?.Any(o => 대상의주문(o, 종류, 대상Id) && !string.IsNullOrEmpty(o.id)) == true
                    ? "읽을 주문을 골라 주세요" : null;
            var 주문 = 고정주문읽기();
            if (주문 == null) return "고른 주문의 기록을 찾을 수 없습니다";
            if (!대상의주문(주문, 종류, 대상Id)) return "이 주문의 담당이 바뀌었습니다";
            return null;
        }

        private bool 관찰휴대폰안내그리기(FocusKind 종류, string 대상Id)
        {
            if (관찰대상 is not { } 대상 || 대상.Kind != 종류 || 대상.Id != 대상Id) return false;
            var 안내 = 물류관찰중 ? 물류관찰안내(종류) : 관찰주문안내(종류, 대상Id);
            if (안내 == null) return false;
            var 앱 = 종류 switch { FocusKind.Store => "홍달 가게", FocusKind.Center => "홍달 창고", FocusKind.Cargo => "홍달 화물", FocusKind.Inspection => "점포 검수", _ => "홍달 기사" };
            var 화면 = PhoneShell(앱, false, null);
            if (화면 is { } screen)
                Text(new Rect(screen.x + 14, screen.y + 70, screen.width - 28, 52), 안내, 12, PhoneText, true, wrap: true);
            return true;
        }

        private static string 관찰대기이유(string 단계) => 단계 switch
        {
            "Waiting" => "아직 배정 전",
            "Assigned" => "기사 수락 대기",
            "Accepted" or "ToPickup" => "기사의 매장 도착 대기",
            "AtPickup" => "픽업 대기",
            "ToDropoff" => "전달지 도착 대기",
            "Delivered" => "주문자 수령 대기",
            "Received" => "정산 대기",
            "Settled" => "이 주문의 업무 완료",
            _ => "다음 기록 대기",
        };

        private void 관찰띠그리기()
        {
            if (관찰대상 is not { } 대상) return;
            if (물류관찰중)
            {
                물류관찰띠그리기(대상.Kind);
                return;
            }
            var 칸 = 관찰띠칸;
            var 가게 = 대상.Kind == FocusKind.Store;
            var 이름 = 가게 ? state.stores?.FirstOrDefault(s => s?.id == 대상.Id)?.name : state.couriers?.FirstOrDefault(c => c?.id == 대상.Id)?.label;
            var 주문 = 고정주문읽기();
            var 안내 = 관찰주문안내(대상.Kind, 대상.Id);
            var 상태 = 관찰주문Id == null ? "고른 주문 없음" : "주문 " + PhoneOrderNumber(관찰주문Id);
            if (!string.IsNullOrEmpty(주문?.menu)) 상태 += " · " + 주문.menu;
            var 기다림 = 안내 ?? (주문 == null ? (가게 ? "새 주문 대기" : "배차 대기") : 관찰대기이유(주문.stage));
            Paint(칸, Lilac, 9);
            Paint(new Rect(칸.x + 1, 칸.y + 1, 칸.width - 2, 칸.height - 2), Canvas, 8);
            Text(new Rect(칸.x + 12, 칸.y + 4, 300, 22), (가게 ? "음식점 · " : "음식 배달기사 · ") + (이름 ?? 대상.Id), 14, Paper, true);
            Text(new Rect(칸.x + 12, 칸.y + 27, 300, 20), 상태, 12, Mint, true);
            Text(new Rect(칸.x + 12, 칸.y + 49, 300, 19), 기다림, 11, Soft);
            var 연결 = 관찰상대찾기(out _);
            var 상대문구 = 가게 && 주문 != null && string.IsNullOrEmpty(주문.driverId) ? "아직 배정 전" : 가게 ? "이 주문의 기사 보기" : "이 주문의 가게 보기";
            if (ExpansionButton(new Rect(칸.x + 315, 칸.y + 9, 139, 27), 상대문구, 연결, Tile)) 같은주문상대보기();
            var 이전가능 = 직전관점 is { } 전 && 관찰객체있음(전.Kind, 전.Id);
            if (ExpansionButton(new Rect(칸.x + 459, 칸.y + 9, 78, 27), "이전 관점", 이전가능, Tile)) 이전관점보기();
            if (ExpansionButton(new Rect(칸.x + 542, 칸.y + 9, 94, 27), "관찰 끝내기", true, Tile))
            {
                역할관찰끝내기();
                return;
            }
            if (ExpansionButton(new Rect(칸.x + 315, 칸.y + 41, 139, 25), "주문 선택", true, Tile))
            {
                역할목록열기();
                관찰주문후보읽기();
                관찰주문목록 = true;
            }
            관찰이동단추그리기(칸.x + 459, 칸.y + 41);
        }

        private void 관찰주문목록그리기()
        {
            var panel = 역할목록칸;
            var 물류 = 물류관찰중;
            Paint(panel, Canvas, 10);
            Text(new Rect(panel.x + 12, panel.y + 7, 166, 23), 물류 ? "관찰할 물류 업무 선택" : "관찰할 주문 선택", 15, Paper, true);
            if (ExpansionButton(new Rect(panel.xMax - 88, panel.y + 7, 78, 22), "역할 목록", true, Tile))
            {
                관찰주문목록 = false;
                역할스크롤 = 0;
                return;
            }
            var body = new Rect(panel.x + 8, panel.y + 42, panel.width - 16, panel.height - 54);
            var 수 = 물류 ? 고를물류.Count : 고를주문.Count;
            var overflow = Mathf.Max(0, 수 * 역할행간격 - body.height);
            var wheel = Event.current;
            if (wheel.type == EventType.ScrollWheel && body.Contains(wheel.mousePosition))
            {
                역할스크롤 += wheel.delta.y * 16f;
                wheel.Use();
            }
            역할스크롤 = Mathf.Clamp(역할스크롤, 0, overflow);
            string 선택한주문 = null;
            GUI.BeginGroup(body);
            if (수 == 0)
                Text(new Rect(4, 0, body.width - 8, 32), "현재 선택할 기록 없음", 12, Soft);
            for (var i = 0; i < 수; i++)
            {
                // 목록을 연 뒤 새 주문이 와도 누르던 줄의 원 주문 ID는 바뀌지 않는다.
                var 원번호 = 물류 ? 고를물류[i].Id : 고를주문[i].id;
                var 주문 = 물류 ? null : state.orders?.FirstOrDefault(o => o != null && o.id == 원번호);
                var 유효 = 물류 || 관찰대상 is { } 대상 && 대상의주문(주문, 대상.Kind, 대상.Id);
                var y = i * 역할행간격 - 역할스크롤;
                var row = new Rect(0, y, body.width, 역할행간격 - 4);
                var 이름 = 물류 ? 고를물류[i].이름 : 주문?.menu ?? "메뉴 미확인";
                var 상태 = 물류 ? 고를물류[i].상태 : 유효 ? 관찰대기이유(주문.stage) : "현재 대상과 연결된 주문 없음";
                Paint(row, 원번호 == (물류 ? 현재물류키 : 관찰주문Id) ? Ink(0x352B49) : Tile, 6);
                Text(new Rect(10, y + 2, body.width - 20, 18), (i + 1) + ". " + PhoneOrderNumber(원번호) + " · " + 이름, 12, Paper, true);
                Text(new Rect(10, y + 20, body.width - 20, 16), 상태, 10, Soft);
                if (유효 && Hit(row)) 선택한주문 = 원번호;
            }
            GUI.EndGroup();
            if (선택한주문 != null)
            {
                if (물류) 물류관찰건선택(관찰대상.Value.Kind, 선택한주문);
                else 관찰주문선택(선택한주문);
            }
        }

        private void 역할목록그리기()
        {
            if (관찰주문목록 && 관찰대상.HasValue)
            {
                관찰주문목록그리기();
                return;
            }
            var panel = 역할목록칸;
            var dong = BusinessDong();
            역할후보갱신(dong);
            Paint(panel, Canvas, 10);
            Text(new Rect(panel.x + 12, panel.y + 6, 232, 21), "역할로 보기 · " + (dong?.Entry.name ?? "동 선택 전"), 15, Paper, true);
            var close = new Rect(panel.xMax - 51, panel.y + 7, 40, 21);
            if (ExpansionButton(close, "닫기", true, Tile)) 역할목록열림 = false;
            Text(new Rect(panel.x + 12, panel.y + 29, 270, 18), "음식점·배달기사는 고른 동, 화물·센터는 첫 인계 사슬", 10, Soft);

            var body = new Rect(panel.x + 8, panel.y + 52, panel.width - 16, panel.height - 108);
            var overflow = Mathf.Max(0, 역할후보.Count * 역할행간격 - body.height);
            var wheel = Event.current;
            if (wheel.type == EventType.ScrollWheel && body.Contains(wheel.mousePosition))
            {
                역할스크롤 += wheel.delta.y * 16f;
                wheel.Use();
            }
            역할스크롤 = Mathf.Clamp(역할스크롤, 0f, overflow);
            역할관찰행? 선택 = null;
            GUI.BeginGroup(body);
            var y = -역할스크롤;
            foreach (var 행 in 역할후보)
            {
                var 가능 = !string.IsNullOrEmpty(행.대상Id);
                var 고름 = 가능 && actorFocus is { } focus && focus.Kind == 행.종류 && focus.Id == 행.대상Id;
                var row = new Rect(0, y, body.width - 5, 역할행간격 - 4);
                Paint(row, 고름 ? Ink(0x352B49) : Tile, 6);
                var 색 = 행.종류 == FocusKind.Center ? Gold : 행.종류 == FocusKind.Cargo ? Sky : Mint;
                Paint(new Rect(9, y + 15, 10, 10), 가능 ? 색 : Soft, 5);
                Text(new Rect(27, y + 2, row.width - 73, 17), string.IsNullOrEmpty(행.이름) ? 행.직업 : 행.직업 + " · " + 행.이름,
                    10, 가능 ? 색 : Soft);
                Text(new Rect(27, y + 19, row.width - 73, 18), 행.상황, 12, 가능 ? Paper : Soft, true);
                if (가능)
                {
                    Text(new Rect(row.xMax - 40, y + 8, 31, 24), 고름 ? "보는 중" : "보기 ›", 9, 고름 ? Lilac : Soft, align: TextAnchor.MiddleRight);
                    if (Hit(row)) 선택 = 행;
                }
                y += 역할행간격;
            }
            GUI.EndGroup();
            if (선택.HasValue) 역할휴대폰열기(선택.Value);
            if (overflow > 0)
            {
                var thumb = body.height * body.height / (body.height + overflow);
                Paint(new Rect(body.xMax - 3, body.y + (body.height - thumb) * 역할스크롤 / overflow, 3, thumb), Soft, 2);
            }
            Text(new Rect(panel.x + 12, panel.yMax - 49, 260, 16), "후속 직업", 10, Soft);
            Text(new Rect(panel.x + 12, panel.yMax - 31, 274, 20), "주문자 · 카페 바리스타 · 창고 작업자", 12, Fade(Soft, 0.7f));
        }
    }
}
