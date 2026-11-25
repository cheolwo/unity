using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 홍달 화물 · 홍달 창고 · 홍달 가게 휴대폰 (2026-11-15, 사용자: 「기사뿐만 아니라 음식점 · 창고 · 화물 기사님도 …
    // 음식 배달 기사님이랑 화물 배달 기사님 나뉘어져 있으니까 … 홍달에서 정한 배차 엔진도 여기서 활용」).
    // 틀 · 재연 큐는 SagajeongDispatchRiderPhone.cs와 같다. 넣는 까닭은 관찰을 쉽게 하는 것이고(제안 9절), 모두 보기 전용이다.
    //  - 홍달 화물(화물 기사, DriverApp): 지도의 화물차나 위쪽 인계 사슬의 「화물 운송」을 누르면. 화물 운송 모듈의 운행(CargoTripState)을 읽는다.
    //    배차 추천 카드(상차 · 하차 · 화물 · 차량 · 배차), 진행 중 운송(진행 상황 · 상차지 · 하차지 · 화물 정보 · 다음 행동).
    //    배차: 첫 사슬은 차량이 한 대라 후보를 묻지 않는다(CargoTransportModule) — 그대로 적는다.
    //  - 홍달 창고(물류센터, WarehouseManagerApp): 인계 사슬의 「센터 출고」를 누르면. 물류센터 모듈의 출고(OutboundState)를 읽는다.
    //    창고 작업 보드(가용 · 예약 · 출고), 출고 요청 · 로트 · 피킹 작업 · 포장 작업 · 출고 인계.
    //  - 홍달 가게(음식점, RestaurantDeskApp): 빛으로 보는 동 지도에서 매장 A · B를 누르면(매장 카드 대신, 「카드」로 되돌림).
    //    주문 처리(신규 · 진행), 주문번호 · 메뉴 · 처리상태 · 조리예상, 기사 줄에 배차 엔진. 조리 단계가 게임에 없어(카2 · 카3)
    //    주문 수락 · 조리 시작 · 픽업 준비 완료 단추는 흐리게 「준비 전」이고 손가락은 누르지 않는다.
    //  - 카페 1호점(가맹)의 홍달 가게는 왼쪽 매장 운영 창이 같은 일을 보이므로 이번에는 넣지 않았다.
    public sealed partial class SagajeongDispatchView
    {
        // 화면에 보일 주문 번호. 게임 주문 ID(예: day-5:food-campaign-01:jungnang-a)를 「D5-01A」처럼 줄인다. 모르는 모양이면 끝 조각.
        private static string PhoneOrderNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return "주문";
            var match = System.Text.RegularExpressions.Regex.Match(id, @"^day-(\d+):[^:]*?(\d+):(.+)$");
            if (match.Success) return "D" + match.Groups[1].Value + "-" + match.Groups[2].Value + Letter(match.Groups[3].Value);
            var tail = id.Substring(id.LastIndexOf(':') + 1);
            return tail.Length > 14 ? tail.Substring(tail.Length - 14) : tail;
        }

        // ── 홍달 화물 ──

        private string 관찰출고Id, 관찰운행Id, 관찰검수Id;
        private readonly List<(string Id, string 이름, string 상태)> 고를물류 = new();
        private bool 물류관찰중 => 관찰대상?.Kind is FocusKind.Center or FocusKind.Cargo or FocusKind.Inspection;
        private string 현재물류키 => 관찰대상?.Kind switch
        {
            FocusKind.Center => 관찰출고Id, FocusKind.Cargo => 관찰운행Id, FocusKind.Inspection => 관찰검수Id, _ => null,
        };

        private void 물류관찰키비우기()
        {
            관찰출고Id = 관찰운행Id = 관찰검수Id = null;
            고를물류.Clear();
        }

        // 같은 참조가 둘 이상이면 어느 한쪽을 임의로 상대 업무로 표시하지 않는다.
        private static T 단일물류행<T>(IEnumerable<T> 행들, string id, Func<T, string> 참조) where T : class
        {
            if (행들 == null || string.IsNullOrEmpty(id)) return null;
            T 결과 = null;
            foreach (var 행 in 행들)
            {
                if (행 == null || 참조(행) != id) continue;
                if (결과 != null) return null;
                결과 = 행;
            }
            return 결과;
        }

        private OutboundState 고른출고읽기() => 단일물류행(ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds, 관찰출고Id, o => o.Id);
        private CargoTripState 고른운행읽기() => 단일물류행(ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport)?.Trips, 관찰운행Id, t => t.Id);
        private StoreReceiptState 고른검수읽기() => 단일물류행(ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations)?.Receipts, 관찰검수Id, r => r.Id);

        private static string 출고표시상태(string 단계) => 단계 switch
        {
            OutboundStages.Reserved => "피킹 작업 중", OutboundStages.Picked => "포장 완료 · 상차 대기",
            OutboundStages.Loaded => "출고 인계 완료", _ => "상태 미확인",
        };

        private static string 검수표시상태(string 단계) => 단계 switch
        {
            ReceiptStages.Waiting => "검수 대기", ReceiptStages.Inspected => "검수 완료", _ => "검수 상태 미확인",
        };

        private void 물류후보읽기(FocusKind 종류)
        {
            고를물류.Clear();
            if (종류 == FocusKind.Center && ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds is { } 출고들)
                foreach (var 출고 in 출고들)
                    if (!string.IsNullOrEmpty(출고?.Id)) 고를물류.Add((출고.Id, 출고.ItemName, 출고표시상태(출고.Stage)));
            if (종류 == FocusKind.Cargo && ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport) is { } 운송)
                foreach (var 운행 in 운송.Trips)
                    if (!string.IsNullOrEmpty(운행?.Id)) 고를물류.Add((운행.Id, 운행.VehicleName, CargoStage(운송, 운행)));
        }

        private void 물류관찰시작(역할관찰행 행)
        {
            if (관찰대상?.Kind == 행.종류 && 현재물류키 != null)
            {
                역할목록열림 = 관찰주문목록 = false;
                return;
            }
            관찰시작점기억();
            관찰지도수동이동();
            물류관찰키비우기();
            관찰주문Id = null;
            직전관점 = null;
            관찰대상 = (행.종류, 행.대상Id);
            물류후보읽기(행.종류);
            if (고를물류.Count == 1) 물류관찰건선택(행.종류, 고를물류[0].Id);
            관찰주문목록 = 역할목록열림 = 고를물류.Count > 1;
            역할스크롤 = 0;
            selectedBuilding = null;
            phoneKey = null;
            역할휴대폰초점(행.종류, 행.대상Id);
        }

        private bool 물류관찰건선택(FocusKind 종류, string 업무Id)
        {
            if (관찰대상?.Kind != 종류 || string.IsNullOrEmpty(업무Id)) return false;
            if (종류 == FocusKind.Center)
            {
                if (단일물류행(ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds, 업무Id, o => o.Id) == null) return false;
                if (관찰출고Id != 업무Id)
                {
                    관찰지도수동이동();
                    관찰출고Id = 업무Id;
                    관찰운행Id = 관찰검수Id = null;
                    직전관점 = null;
                    phoneKey = null;
                }
            }
            else if (종류 == FocusKind.Cargo)
            {
                if (단일물류행(ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport)?.Trips, 업무Id, t => t.Id) == null) return false;
                if (관찰운행Id != 업무Id)
                {
                    관찰지도수동이동();
                    관찰운행Id = 업무Id;
                    관찰출고Id = 관찰검수Id = null;
                    직전관점 = null;
                    phoneKey = null;
                }
            }
            else return false;
            역할목록열림 = 관찰주문목록 = false;
            return true;
        }

        private CargoTripState 출고에연결된운행()
        {
            var 출고 = 고른출고읽기();
            var 운행들 = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport)?.Trips;
            var 상대 = 단일물류행(운행들, 출고?.Id, t => t.OutboundId);
            return 상대 != null && (관찰운행Id == null || 상대.Id == 관찰운행Id)
                ? 단일물류행(운행들, 상대.Id, t => t.Id) : null;
        }

        private OutboundState 운행에연결된출고()
        {
            var 운행 = 고른운행읽기();
            if (운행 == null || (관찰출고Id != null && 운행.OutboundId != 관찰출고Id)) return null;
            return 단일물류행(ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds, 운행.OutboundId, o => o.Id);
        }

        private StoreReceiptState 운행에연결된검수()
        {
            var 운행 = 고른운행읽기();
            var 검수들 = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations)?.Receipts;
            var 상대 = 단일물류행(검수들, 운행?.Id, r => r.TripId);
            return 상대 != null && (관찰검수Id == null || 상대.Id == 관찰검수Id)
                ? 단일물류행(검수들, 상대.Id, r => r.Id) : null;
        }

        private bool 같은출고화물보기()
        {
            if (관찰대상?.Kind != FocusKind.Center || 출고에연결된운행() is not { } 운행 || string.IsNullOrEmpty(운행.Id)) return false;
            관찰운행Id = 운행.Id;
            물류상대열기(FocusKind.Cargo, "cargo");
            return true;
        }

        private bool 같은화물출고보기()
        {
            if (관찰대상?.Kind != FocusKind.Cargo || 운행에연결된출고() is not { } 출고) return false;
            관찰출고Id = 출고.Id;
            물류상대열기(FocusKind.Center, "center");
            return true;
        }

        private bool 같은화물검수보기()
        {
            if (관찰대상?.Kind != FocusKind.Cargo || 운행에연결된검수() is not { } 검수 || string.IsNullOrEmpty(검수.Id)) return false;
            관찰검수Id = 검수.Id;
            물류상대열기(FocusKind.Inspection, 검수.Id);
            return true;
        }

        private void 물류상대열기(FocusKind 종류, string 대상Id)
        {
            관찰지도수동이동();
            직전관점 = 관찰대상;
            관찰대상 = (종류, 대상Id);
            역할목록열림 = 관찰주문목록 = false;
            역할휴대폰초점(종류, 대상Id);
        }

        private string 물류관찰안내(FocusKind 종류)
        {
            if (현재물류키 == null)
            {
                var 있음 = 종류 == FocusKind.Center ? ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds.Count > 0
                    : ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport)?.Trips.Count > 0;
                return 있음 ? "읽을 업무를 골라 주세요" : null;
            }
            var 유효 = 종류 switch
            {
                FocusKind.Center => 고른출고읽기() != null, FocusKind.Cargo => 고른운행읽기() != null,
                FocusKind.Inspection => 고른검수읽기() != null, _ => false,
            };
            return 유효 ? null : "고른 업무의 기록을 확인할 수 없습니다";
        }

        private void 관찰검수폰그리기()
        {
            var 검수 = 고른검수읽기();
            if (검수 == null || PhoneShell("점포 검수", false, null) is not { } screen) return;
            var x = screen.x + 11;
            var y = screen.y + 60;
            var w = screen.width - 22;
            var 점포 = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            Text(new Rect(x, y, w, 22), 점포?.StoreName ?? "점포", 14, PhoneText, true);
            Text(new Rect(x, y + 24, w, 18), 검수표시상태(검수.Stage), 12, PhoneInfo, true);
            Text(new Rect(x, y + 50, w, 22), 검수.ItemName + " · 하차 " + 검수.Unloaded.ToString("0.##") + 검수.Unit, 11, PhoneText);
            if (검수.Stage == ReceiptStages.Inspected)
            {
                Text(new Rect(x, y + 82, w, 20), "인수 " + 검수.Accepted.ToString("0.##") + 검수.Unit, 11, PhoneText);
                Text(new Rect(x, y + 104, w, 20), "격리 " + 검수.Quarantined.ToString("0.##") + 검수.Unit, 11, PhoneText);
                Text(new Rect(x, y + 126, w, 20), "거절 " + 검수.Rejected.ToString("0.##") + 검수.Unit, 11, PhoneText);
            }
        }

        private void 물류관찰띠그리기(FocusKind 종류)
        {
            var 칸 = 관찰띠칸;
            var 센터 = 종류 == FocusKind.Center;
            var 화물 = 종류 == FocusKind.Cargo;
            var 출고 = 센터 ? 고른출고읽기() : null;
            var 운행 = 화물 ? 고른운행읽기() : null;
            var 검수 = 종류 == FocusKind.Inspection ? 고른검수읽기() : null;
            var 상태 = 물류관찰안내(종류) ?? (센터 ? (출고 == null ? "출고 요청 대기" : 출고표시상태(출고.Stage)) :
                화물 ? (운행 == null ? "배차 대기" : CargoStage(ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport), 운행)) :
                검수표시상태(검수?.Stage));
            Paint(칸, Lilac, 9);
            Paint(new Rect(칸.x + 1, 칸.y + 1, 칸.width - 2, 칸.height - 2), Canvas, 8);
            Text(new Rect(칸.x + 12, 칸.y + 4, 275, 22), 센터 ? "물류센터 출고" : 화물 ? "화물 기사" : "점포 검수", 14, Paper, true);
            Text(new Rect(칸.x + 12, 칸.y + 28, 275, 18), (현재물류키 == null ? "고른 업무 없음" : PhoneOrderNumber(현재물류키)) + " · " + (출고?.ItemName ?? 운행?.ItemName ?? 검수?.ItemName), 12, Mint);
            Text(new Rect(칸.x + 12, 칸.y + 49, 275, 18), 상태, 11, Soft);
            if (센터 && ExpansionButton(new Rect(칸.x + 292, 칸.y + 8, 108, 26), "이 화물차 보기", 출고에연결된운행() != null, Tile)) 같은출고화물보기();
            if (화물 && ExpansionButton(new Rect(칸.x + 292, 칸.y + 8, 108, 26), "해당 출고 보기", 운행에연결된출고() != null, Tile)) 같은화물출고보기();
            if (화물)
            {
                var 연결 = 운행에연결된검수() != null;
                if (ExpansionButton(new Rect(칸.x + 405, 칸.y + 8, 93, 26), 연결 ? "점포 검수" : "검수 기록 대기", 연결, Tile)) 같은화물검수보기();
            }
            if (ExpansionButton(new Rect(칸.x + 503, 칸.y + 8, 62, 26), "이전 관점", 직전관점 is { } 전 && 관찰객체있음(전.Kind, 전.Id), Tile)) 이전관점보기();
            if (ExpansionButton(new Rect(칸.x + 570, 칸.y + 8, 68, 26), "관찰 끝내기", true, Tile))
            {
                역할관찰끝내기();
                return;
            }
            if ((센터 || 화물) && ExpansionButton(new Rect(칸.x + 292, 칸.y + 41, 108, 25), "업무 선택", true, Tile))
            {
                역할목록열기();
                물류후보읽기(종류);
                관찰주문목록 = true;
            }
            관찰이동단추그리기(칸.x + 459, 칸.y + 41);
        }

        // 운행 단계를 화면 단계로 줄인다. 상차 완료 뒤 도착 전은 「운송중」이다. 단계 코드의 글자는 모듈 사본이 알려 준다.
        private static string CargoStage(CargoTransportSnapshot cargo, CargoTripState trip)
        {
            if (cargo == null || trip == null) return "대기";
            if (trip.Stage == cargo.ReceivedStage) return "인수완료";
            if (trip.Stage == cargo.ArrivedStage) return "하차지도착";
            if (trip.Stage == cargo.LoadedStage) return "운송중";
            return "배차확정";
        }

        private void DrawCargoPhone()
        {
            var cargo = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport);
            var center = ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter);
            var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            var trip = 물류관찰중 ? 단일물류행(cargo?.Trips, 관찰운행Id, t => t.Id) : cargo?.Trips.LastOrDefault();
            var stage = CargoStage(cargo, trip);
            // 화물 대본의 초 단위 간격은 장면을 읽기 위한 재생 시간이다. 상차 소요시간은 이 사본에서 알 수 없다.
            SyncPhone("cargo", trip?.Id, stage, 역할앱재연Policy.CargoScript);
            var progress = AdvancePhoneScene();
            var rest = 역할앱재연Policy.CargoScreen(stage);
            var scene = phoneScene;
            var view = scene?.Screen ?? rest.Screen;
            var step = scene?.Step ?? rest.Step;
            var shell = PhoneShell("홍달 화물", false, null);
            if (shell is not { } screen) return;
            var x = screen.x + 10;
            var w = screen.width - 20;
            var y = screen.y + 52;
            var from = string.IsNullOrEmpty(center?.CenterName) ? "물류센터" : center.CenterName;
            var to = string.IsNullOrEmpty(store?.StoreName) ? "점포" : store.StoreName;
            var load = trip == null ? "—" : trip.ItemName + " " + trip.Quantity.ToString("0") + trip.Unit;
            var vehicle = trip == null || string.IsNullOrEmpty(trip.VehicleName) ? "화물차 한 대" : trip.VehicleName;
            PhoneSegment(x, y, w, "진행 중 운송", "배차 추천", view != 화물화면.추천);
            y += 30;
            var working = view == 화물화면.추천 || view == 화물화면.진행;
            var moving = scene != null && scene.Tap == null && scene.Toast == null && scene.Push == null ? Ease01(progress) : 1f;
            var 운행진행 = trip == null ? 0 : Mathf.Clamp01((BusinessNow - trip.DepartedAtMs) / (float)Math.Max(1L, trip.ArriveAtMs - trip.DepartedAtMs));
            var along = step <= 1 ? 0f : step <= 3 ? 1f : step == 4 ? 1f + (scene?.Step == 4 ? 0.1f + 0.4f * moving : 운행진행) : 2f;
            DrawPhoneMap(new Rect(x, y, w, 62), working, along, "상차", PhoneLoad, "하차", PhoneInfo);
            y += 70;

            Vector2? tapAt = null;
            if (view == 화물화면.대기 || view == 화물화면.추천 || view == 화물화면.완료)
            {
                PhoneCard(new Rect(x, y, w, 60));
                PhonePill(x + 10, y + 9, "대한민국 운행", PhoneSuccess, Ink(0xE3F4E9));
                Text(new Rect(x + 10, y + 28, w - 20, 16), "신규 배차 추천을 기다리고 있어요", 10, PhoneText, true);
                Text(new Rect(x + 10, y + 44, w - 20, 12), "차량 · " + vehicle, 8, PhoneSub);
                y += 68;
                if (view == 화물화면.완료 && trip != null)
                {
                    PhoneCard(new Rect(x, y, w, 46));
                    Text(new Rect(x + 10, y + 6, w - 20, 12), "최근 운송", 8, PhoneSub);
                    Text(new Rect(x + 10, y + 22, w - 84, 16), load + " · " + to, 10, PhoneText, true);
                    Text(new Rect(x + w - 80, y + 22, 70, 16), "하차완료", 9, PhoneSuccess, true, TextAnchor.MiddleRight);
                    y += 54;
                }
                PhoneButton(new Rect(x, y, w, 26), "신규 배차 받기", false, false);
            }
            else
            {
                PhoneCard(new Rect(x, y, w, 44));
                Text(new Rect(x + 10, y + 4, 80, 12), "진행 상황", 8, PhoneSub);
                DrawPhoneSteps(x + 14, y + 24, w - 28, step, 역할앱재연Policy.CargoStepNames);
                y += 50;
                PhoneCard(new Rect(x, y, w, 36));
                Text(new Rect(x + 10, y + 3, 80, 12), "상차지", 8, PhoneSub);
                Text(new Rect(x + 10, y + 16, w - 86, 16), from, 10, PhoneText, true);
                PhonePill(x + w - 70, y + 17, step >= 3 ? "상차완료" : "상차 대기", step >= 3 ? PhoneSub : PhoneLoad, step >= 3 ? Ink(0xECF0F3) : Ink(0xFDF1E2));
                y += 42;
                PhoneCard(new Rect(x, y, w, 36));
                Text(new Rect(x + 10, y + 3, 80, 12), "하차지", 8, PhoneSub);
                Text(new Rect(x + 10, y + 16, w - 86, 16), to, 10, PhoneText, true);
                var 남은초 = trip == null ? 0 : Math.Max(0L, (trip.ArriveAtMs - BusinessNow + 999) / 1000);
                var left = trip != null && trip.Stage == cargo.LoadedStage ? $"{남은초 / 60}:{남은초 % 60:00} 뒤" : "도착 전";
                PhonePill(x + w - 70, y + 17, step >= 5 ? "도착" : step == 4 ? left : "대기", step >= 5 ? PhoneInfo : PhoneSub,
                    step >= 5 ? Ink(0xE4ECFD) : Ink(0xECF0F3));
                y += 42;
                PhoneCard(new Rect(x, y, w, 40));
                Text(new Rect(x + 10, y + 4, 90, 14), "화물 정보", 8, PhoneSub);
                Text(new Rect(x + 90, y + 4, w - 100, 14), load, 9, PhoneText, align: TextAnchor.MiddleRight);
                Text(new Rect(x + 10, y + 21, 90, 14), "기사 지급 예정액", 8, PhoneSub);
                Text(new Rect(x + 90, y + 21, w - 100, 14), "자료 미확인", 9, PhoneSub, align: TextAnchor.MiddleRight);
                y += 48;
                var next = step <= 2 ? "상차" : step == 3 ? "하차지 이동" : step == 4 ? "하차" : "하차 완료";
                var label = scene?.Tap ?? next;
                var main = new Rect(x, y, w * 0.6f, 28);
                PhoneButton(main, label, true, PhonePressed(scene, progress, label));
                PhoneButton(new Rect(x + w * 0.6f + 6, y, w * 0.4f - 6, 28), "운행 지도로", false, false);
                if (scene?.Tap != null) tapAt = main.center;
            }

            var tabsY = PhoneFooter(screen, "홈", "진행 중 운송", "보기 전용 · 장면 간격은 재연 속도", view != 화물화면.진행);
            if (view == 화물화면.추천)
            {
                var rows = new[]
                {
                    ("상차", from),
                    ("하차", to),
                    ("화물", load),
                    ("차량", vehicle),
                    ("배차", "차량 한 대 · 후보 없이 배정(첫 사슬)"),
                };
                tapAt = PhoneSheet(screen, tabsY, scene, progress, 화물화면.추천, "배차 추천", rows, "추천 거절", "추천 수락") ?? tapAt;
            }
            PhoneOverlays(screen, tabsY, scene, progress, tapAt, "홍달 화물", "가상 화물 기사가 누른 곳");
        }

        // ── 홍달 창고 ──

        private void DrawCenterPhone()
        {
            var center = ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter);
            var outbound = 물류관찰중 ? 단일물류행(center?.Outbounds, 관찰출고Id, o => o.Id) : center?.Outbounds.LastOrDefault();
            var stage = outbound == null ? "대기" : outbound.Stage;
            SyncPhone("center", outbound?.Id, stage, 역할앱재연Policy.CenterScript);
            var progress = AdvancePhoneScene();
            var rest = 역할앱재연Policy.CenterScreen(stage);
            var scene = phoneScene;
            var view = scene?.Screen ?? rest.Screen;
            var step = scene?.Step ?? rest.Step;
            var shell = PhoneShell("홍달 창고", false, null);
            if (shell is not { } screen) return;
            var x = screen.x + 10;
            var w = screen.width - 20;
            var y = screen.y + 52;
            PhoneSegment(x, y, w, "창고 작업 보드", "재고");
            y += 30;

            // 창고 숫자 셋: 가용 · 예약 · 출고 (모듈 사본 그대로)
            var unit = center?.Unit ?? string.Empty;
            PhoneCard(new Rect(x, y, w, 62));
            Text(new Rect(x + 10, y + 4, w - 20, 14), string.IsNullOrEmpty(center?.CenterName) ? "물류센터" : center.CenterName, 10, PhoneText, true);
            var cells = new[] { ("가용", center?.Available ?? 0m), ("예약", center?.Reserved ?? 0m), ("출고", center?.Shipped ?? 0m) };
            var cell = (w - 20) / 3f;
            for (var i = 0; i < cells.Length; i++)
            {
                var box = new Rect(x + 10 + i * cell, y + 22, cell - 6, 34);
                Paint(box, i == 0 ? PhonePrimaryLight : Ink(0xF1F4F6), 7);
                Text(new Rect(box.x + 6, box.y + 2, box.width - 12, 12), cells[i].Item1, 8, PhoneSub);
                Text(new Rect(box.x + 6, box.y + 15, box.width - 12, 16), cells[i].Item2.ToString("0") + unit, 11, i == 0 ? PhonePrimaryDark : PhoneText, true);
            }
            y += 70;

            Vector2? tapAt = null;
            if (view == 창고화면.대기 || outbound == null)
            {
                PhoneCard(new Rect(x, y, w, 52));
                Text(new Rect(x + 10, y + 6, w - 20, 12), "다음 업무", 8, PhoneSub);
                Text(new Rect(x + 10, y + 22, w - 20, 16), "처리할 출고 요청이 없어요", 10, PhoneText, true);
                y += 60;
                PhoneButton(new Rect(x, y, w, 26), "출고 인계 준비", false, false);
            }
            else
            {
                PhoneCard(new Rect(x, y, w, 44));
                Text(new Rect(x + 10, y + 4, 80, 12), "출고 흐름", 8, PhoneSub);
                DrawPhoneSteps(x + 14, y + 24, w - 28, step, 역할앱재연Policy.CenterStepNames);
                y += 50;
                PhoneCard(new Rect(x, y, w, 58));
                Text(new Rect(x + 10, y + 4, 90, 12), "출고 요청", 8, PhoneSub);
                var supplyOrder = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders.LastOrDefault(item => item.OrderId == outbound.OrderId);
                Text(new Rect(x + 90, y + 4, w - 100, 12), supplyOrder?.OrderNumber ?? PhoneOrderNumber(outbound.OrderId), 8, PhoneSub, align: TextAnchor.MiddleRight);
                Text(new Rect(x + 10, y + 18, w - 20, 16), outbound.ItemName + " · 예정 수량 " + outbound.Requested.ToString("0") + outbound.Unit, 10, PhoneText, true);
                var shortText = outbound.Short > 0 ? " · 부족 " + outbound.Short.ToString("0") + outbound.Unit : string.Empty;
                Text(new Rect(x + 10, y + 37, w - 20, 14), "확보 " + outbound.Quantity.ToString("0") + outbound.Unit + shortText +
                    (string.IsNullOrEmpty(outbound.LotId) ? string.Empty : " · 로트 " + outbound.LotId), 8, outbound.Short > 0 ? PhoneWarn : PhoneSub);
                y += 64;
                var status = step >= 3 ? ("출고 인계 완료", PhoneSuccess, Ink(0xE3F4E9)) : step == 2 ? ("포장 완료 · 상차 대기", PhoneLoad, Ink(0xFDF1E2)) :
                    ("피킹 작업 중", PhoneInfo, Ink(0xE4ECFD));
                PhoneCard(new Rect(x, y, w, 30));
                Text(new Rect(x + 10, y + 8, 60, 14), "작업 상태", 8, PhoneSub);
                PhonePill(x + w - PlaceTextWidth(status.Item1, 8, false) - 22, y + 8, status.Item1, status.Item2, status.Item3);
                y += 38;
                var next = step <= 1 ? "피킹 작업 실행" : step == 2 ? "출고 인계" : "출고 인계 완료";
                var label = scene?.Tap ?? next;
                var main = new Rect(x, y, w * 0.6f, 28);
                PhoneButton(main, label, step < 3 || scene?.Tap != null, PhonePressed(scene, progress, label));
                PhoneButton(new Rect(x + w * 0.6f + 6, y, w * 0.4f - 6, 28), "포장 작업", false, false);
                if (scene?.Tap != null) tapAt = main.center;
            }

            var tabsY = PhoneFooter(screen, "작업 보드", "이력", "보기 전용 · 가상 창고 직원이 한 일을 재연");
            PhoneOverlays(screen, tabsY, scene, progress, tapAt, "홍달 창고", "가상 창고 직원이 누른 곳");
        }

        // ── 홍달 가게 ──

        // 그 매장의 지금 주문: 끝나지 않은 주문 가운데 가장 나중 것, 없으면 가장 나중에 정산한 주문.
        private DispatchOrder PhoneStoreOrder(string storeId)
        {
            if (state?.orders == null) return null;
            DispatchOrder open = null, done = null;
            foreach (var order in state.orders)
            {
                if (order.storeId != storeId) continue;
                if (order.stage == "Settled") done = order;
                else open = order;
            }
            return open ?? done;
        }

        private void DrawStorePhone(string storeId)
        {
            var shop = state.stores?.FirstOrDefault(item => item.id == storeId);
            if (shop == null) return;
            var order = 표시할배달주문(FocusKind.Store, storeId);
            SyncPhone("store:" + storeId, order?.id, order?.stage, (from, to) => 역할앱재연Policy.StoreScript(from, to, PhoneOrderNumber(order?.id)));
            var progress = AdvancePhoneScene();
            var rest = 역할앱재연Policy.StoreScreen(order?.stage);
            var scene = phoneScene;
            var view = scene?.Screen ?? rest.Screen;
            var step = scene?.Step ?? rest.Step;
            var shell = PhoneShell("홍달 가게", true, null);
            if (shell is not { } screen) return;
            var x = screen.x + 10;
            var w = screen.width - 20;
            var y = screen.y + 52;
            var name = string.IsNullOrEmpty(shop.name) ? "매장 " + Letter(shop.id) : shop.name;
            Text(new Rect(x, y, w - 60, 18), "주문 처리", 12, PhoneText, true);
            Text(new Rect(x + w - 110, y, 110, 18), name, 9, PhoneSub, align: TextAnchor.MiddleRight);
            y += 24;
            PhoneSegment(x, y, w, "신규", "진행", view == 가게화면.신규 || view == 가게화면.대기);
            y += 30;
            var today = state.orders.Count(item => item.storeId == storeId);
            PhoneCard(new Rect(x, y, w, 34));
            Paint(new Rect(x + 1, y + 1, 4, 32), PhonePrimary, 2);
            Text(new Rect(x + 12, y + 4, w - 24, 12), "오늘 들어온 주문", 8, PhoneSub);
            Text(new Rect(x + 12, y + 16, w - 24, 16), today + "건 · 가상 주문", 10, PhoneText, true);
            y += 42;

            if (view == 가게화면.대기 || order == null)
            {
                PhoneCard(new Rect(x, y, w, 52));
                Text(new Rect(x + 10, y + 6, w - 20, 12), "신규", 8, PhoneSub);
                Text(new Rect(x + 10, y + 22, w - 20, 16), "새 주문을 기다리고 있어요", 10, PhoneText, true);
            }
            else
            {
                var status = step >= 4 && view == 가게화면.완료 ? "정산 완료" : step >= 4 ? "전달 완료" : step == 3 ? "픽업 완료" :
                    step == 2 ? "기사 도착" : step == 1 ? "기사 배정" : "접수";
                PhoneCard(new Rect(x, y, w, 82));
                var rows = new[]
                {
                    ("주문번호", PhoneOrderNumber(order.id)),
                    ("메뉴", order.menu ?? "자료 미확인"),
                    ("처리상태", status),
                    ("수량 · 조리예상", "자료 미확인"),
                };
                for (var i = 0; i < rows.Length; i++)
                {
                    Text(new Rect(x + 10, y + 5 + i * 18, 70, 14), rows[i].Item1, 8, PhoneSub);
                    Text(new Rect(x + 74, y + 5 + i * 18, w - 84, 14), rows[i].Item2, 9, i == 2 ? PhonePrimaryDark : i == 3 ? PhoneSub : PhoneText,
                        i == 2, TextAnchor.MiddleRight);
                }
                y += 88;
                PhoneCard(new Rect(x, y, w, 44));
                Text(new Rect(x + 10, y + 4, 80, 12), "진행", 8, PhoneSub);
                DrawPhoneSteps(x + 14, y + 24, w - 28, step, 역할앱재연Policy.StoreStepNames);
                y += 50;
                PhoneCard(new Rect(x, y, w, 34));
                Text(new Rect(x + 10, y + 3, 60, 12), "기사", 8, PhoneSub);
                var rider = string.IsNullOrEmpty(order.driverId) ? (order.stage == "Waiting" ? "기사를 찾는 중" : "—") : "기사 " + Letter(order.driverId);
                Text(new Rect(x + 10, y + 16, 80, 16), rider, 10, PhoneText, true);
                Text(new Rect(x + 84, y + 16, w - 94, 16), EngineLine(order), 8, PhonePrimaryDark, align: TextAnchor.MiddleRight);
                y += 40;
                // 가게 쪽 처리(주문 수락 · 조리 시작 · 픽업 준비 완료)는 게임에 아직 없다: 흐리게 그리고 「준비 전」.
                PhoneButton(new Rect(x, y, w, 26), "주문 수락 · 조리 시작 (준비 전)", false, false);
            }

            var tabsY = PhoneFooter(screen, "주문 처리", "매장", "보기 전용 · 가게에 들어온 일을 재연");
            PhoneOverlays(screen, tabsY, scene, progress, null, "홍달 가게", string.Empty);
        }

        // 방향 촬영: 화물(지금 화면 → 배차 추천의 「추천 수락」), 창고(「피킹 작업 시작」), 가게(주문 수신 알림)를 찍는다.
        // 대본은 보기만 하므로 시계 · 원장 · 주문 단계는 그대로여야 한다(부르는 ViewProofRiderPhone이 전후를 견준다).
        private IEnumerator ViewProofRolePhones(string directory, ViewProofReceipt receipt)
        {
            IEnumerator Open(FocusKind kind, string id)
            {
                phoneKey = null;
                phoneHold = null;
                OpenRolePhone(kind, id);
                for (var frame = 0; frame < 4; frame++) yield return null;
                phoneDrawn = 0;
                yield return null;
                yield return null;
            }

            IEnumerator HoldAt(System.Func<재연장면, bool> found, float hold)
            {
                var deadline = Time.realtimeSinceStartup + 6f;
                while (!(phoneScene != null && found(phoneScene)) && Time.realtimeSinceStartup < deadline) yield return null;
                phoneHold = hold;
                for (var frame = 0; frame < 3; frame++) yield return null;
            }

            void Clear()
            {
                phoneHold = null;
                phoneQueue.Clear();
                phoneScene = null;
            }

            // 홍달 화물
            yield return Open(FocusKind.Cargo, "cargo");
            receipt.cargoPhoneShown = phoneDrawn > 0 && RolePhoneOn(FocusKind.Cargo);
            var cargo = ModuleSnapshot<CargoTransportSnapshot>(BusinessIds.CargoTransport);
            receipt.cargoStage = CargoStage(cargo, cargo?.Trips.LastOrDefault());
            receipt.cargoIdleOnOpen = phoneQueue.Count == 0 && phoneScene == null;
            yield return SaveFrame(directory, "100-cargo-phone.png");
            EnqueuePhoneScript(역할앱재연Policy.CargoScript(null, "상차완료"));
            yield return HoldAt(scene => scene.Tap == "추천 수락", 0.7f);
            receipt.cargoTap = phoneScene?.Tap ?? string.Empty;
            yield return SaveFrame(directory, "101-cargo-phone-accept.png");
            Clear();

            // 홍달 창고
            yield return Open(FocusKind.Center, "center");
            receipt.centerPhoneShown = phoneDrawn > 0 && RolePhoneOn(FocusKind.Center);
            receipt.centerStage = ModuleSnapshot<LogisticsCenterSnapshot>(BusinessIds.LogisticsCenter)?.Outbounds.LastOrDefault()?.Stage ?? "대기";
            EnqueuePhoneScript(역할앱재연Policy.CenterScript(null, "Reserved"));
            yield return HoldAt(scene => scene.Tap == "피킹 작업 시작", 0.7f);
            receipt.centerTap = phoneScene?.Tap ?? string.Empty;
            yield return SaveFrame(directory, "102-center-phone.png");
            Clear();

            // 홍달 가게
            var shop = state?.stores?.FirstOrDefault();
            if (shop != null)
            {
                yield return Open(FocusKind.Store, shop.id);
                receipt.storePhoneShown = phoneDrawn > 0 && RolePhoneOn(FocusKind.Store);
                EnqueuePhoneScript(역할앱재연Policy.StoreScript(null, "Waiting", PhoneOrderNumber(PhoneStoreOrder(shop.id)?.id)));
                yield return HoldAt(scene => scene.Push != null, 0.6f);
                receipt.storePush = phoneScene?.Push ?? string.Empty;
                yield return SaveFrame(directory, "103-store-phone.png");
                Clear();
            }
            actorFocus = null;
            yield return null;
        }
    }
}
