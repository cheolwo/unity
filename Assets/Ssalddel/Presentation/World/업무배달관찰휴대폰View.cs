using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Ssalddel.Contracts.Common.Metadata;
using Card = Ssalddel.Unity.Presentation.World.동네관찰휴대폰View.관찰카드;

namespace Ssalddel.Unity.Presentation.World
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E4, "NPC 선택형 격리 Simulation 검증 휴대폰", Boundary = "개발용 상태 사본만 표현하며 실제 기사 배차·위치·픽업·전달 권한이 없다")]
    public sealed class 업무배달관찰휴대폰View : MonoBehaviour
    {
        public event Action StartRequested, RetryRequested;
        public event Action WarehouseRetryRequested;
        public 마트입고관찰Store Warehouse { get; set; }
        public event Action<string> ActorSelected;
        public 동네관찰휴대폰View Shell { get; private set; }
        public string SelectedActorId { get; private set; } = "";
        private 업무배달관찰Store store;
        private bool resetScroll;
        private static readonly Color Purple = new(.40f, .26f, .66f), Orange = new(.86f, .36f, .22f), Teal = new(.07f, .56f, .52f);
        public void Configure(업무배달관찰Store value)
        { store = value; Shell = gameObject.AddComponent<동네관찰휴대폰View>(); Shell.구성(); Refresh(); }
        public void SelectActor(string id)
        { SelectedActorId = id ?? ""; resetScroll = true; Shell.열기(); Refresh(); ActorSelected?.Invoke(SelectedActorId); }
        public void Refresh()
        {
            if (Shell == null || store == null) return;
            var state = store.State; var cards = new List<Card>();
            cards.Add(new Card { Title = "가상 NPC 관찰 · 실제 기사 배차 없음", Body = store.ConnectionLabel(DateTime.UtcNow) + "\n개발용 격리 Simulation 결과만 표시합니다. 실제 기사 위치·수락·픽업·전달·정산은 실행하지 않습니다. 음식 배달과 마트의 연결 상태는 독립적이며 주변 연한색 시설·보충창고는 미연동 배경입니다.", Chip = "관찰 표현 전용", Tint = new Color(.94f,.92f,.98f), ChipColor = Purple });
            if (Warehouse != null)
            {
                cards.Add(new Card { Title = "살뜰마트 작업자 · 입고 관찰", Body = Warehouse.Error.Length > 0 ? Warehouse.Error
                    : "서버 창고 " + Warehouse.State?.WarehouseId + " · " + Warehouse.Stage + "\n실제 근무자 위치가 아닌 모의 표현",
                    Click = () => SelectActor(마트입고관찰Store.WorkerId), Chip = "같은 동네 · 기존 마트", ChipColor = Teal });
                if (SelectedActorId == 마트입고관찰Store.WorkerId)
                {
                    foreach (var entry in Warehouse.State?.Items ?? Array.Empty<마트입고관찰품목>())
                    {
                        var id = entry.Id;
                        cards.Add(new Card { Title = entry.ProductName, Body = entry.Status + " · 가용 " + entry.AvailableQuantity
                            + "\n보관 위치 " + entry.StorageLocation + "\n" + id, Chip = id == Warehouse.SelectedItemId ? "현재 관찰" : "관찰 선택",
                            Click = () => { Warehouse.Select(id); Refresh(); } });
                    }
                    cards.Add(new Card { Title = "창고 자료 다시 조회", Body = "최대 50개 품목의 조회 범위 · 목록에서 사라진 것은 완료로 판정하지 않음",
                        Enabled = !Warehouse.InFlight, Click = () => WarehouseRetryRequested?.Invoke() });
                    cards.Add(new Card { Title = "NPC 목록으로", Click = Home });
                }
            }
            if (store.PendingIdle != null) cards.Add(new Card { Title = "새 검증 서버 결과 확인", Body = "이전 결과를 시작 대기 결과로 바꿉니다. 주문은 생성하지 않습니다. 확인 후 ‘5분 검증 시작’을 별도로 선택하세요.",
                Chip = "사용자 확인 필요", ChipColor = Orange, Enabled = store.CanAcceptPendingIdle(DateTime.UtcNow), Click = () => { store.AcceptPendingIdle(DateTime.UtcNow); Refresh(); } });
            if (state == null)
                cards.Add(new Card { Title = "아직 서버 결과가 없습니다", Body = "연결 파일·검증 서버를 확인하세요. 로컬 표본으로 대체하지 않습니다." });
            else
            {
                if (state.MenuSelection != null)
                {
                    var menu = state.MenuSelection;
                    cards.Add(new Card { Title = menu.MenuId > 0 ? "NPC가 고른 메뉴 · " + menu.Title : "NPC 메뉴 선택 대기",
                        Body = menu.Reason + "\n" + (menu.MenuId > 0 ? menu.SalePrice.ToString("0") + "원 · " : "") + menu.PriceBasis,
                        Chip = "자료 기반 선택 · 주문은 서버 검증", ChipColor = Orange });
                    cards.Add(new Card { Title = "메뉴 자료와 선택 후보", Body = menu.Source + "\n" + string.Join("\n", menu.Candidates),
                        Chip = "기존 식약처 자료 · 내부 검증", ChipColor = Teal });
                    cards.Add(new Card { Title = "재료 가격 참고 · 메뉴 판매가 아님", Body = menu.Reference,
                        Chip = "자료 " + menu.RecipeId + " / 메뉴 " + menu.MenuId, ChipColor = Purple });
                }
                cards.Add(new Card { Title = "같은 주문번호 · " + (state.OrderNo.Length > 0 ? state.OrderNo : "아직 등록 전"),
                    Body = "주문 " + state.OrderStatus + "\n배차 " + state.DispatchStatus + "\n" + state.Message,
                    Chip = 업무배달관찰Store.상태이름(state.Status) + " · 조회 판본 " + state.Revision, ChipColor = Teal });
                var actor = state.Actors.FirstOrDefault(a => a.Id == SelectedActorId);
                if (actor == null)
                {
                    foreach (var entry in state.Actors)
                    { var id = entry.Id; cards.Add(ActorCard(entry, state.OrderNo, () => SelectActor(id))); }
                }
                else
                {
                    cards.Add(ActorCard(actor, state.OrderNo));
                    cards.Add(new Card { Title = "마지막 행동", Body = Empty(actor.LastAction), Chip = "서버가 기록한 행동", ChipColor = RoleColor(actor.Role) });
                    cards.Add(new Card { Title = "다음 행동 / 대기 이유", Body = Empty(actor.NextAction) + "\n" + Empty(actor.WaitReason) });
                    foreach (var entry in state.Events.Where(e => e.ActorId == actor.Id).Reverse().Take(4))
                        cards.Add(new Card { Title = "행동 " + entry.Sequence + " · " + entry.Action, Body = entry.Result + "\n" + entry.OccurredAtUtc });
                    cards.Add(new Card { Title = "NPC 목록으로", Click = Home });
                }
            }
            if (state?.Status == "Idle") cards.Add(new Card { Title = "5분 검증 시작", Body = "개발용 격리 검증 서버에 합성 Simulation 실행을 명시적으로 요청합니다. 운영 기사 API는 호출하지 않습니다.", Chip = "개발 검증 전용", Click = () => StartRequested?.Invoke(), Enabled = !store.InFlight && store.Error.Length == 0, Tint = new Color(.88f,.96f,.94f), ChipColor = Teal });
            cards.Add(new Card { Title = "지금 다시 조회", Body = "조회만 수행 · 시작 요청은 자동 재시도하지 않음", Click = () => RetryRequested?.Invoke(), Enabled = !store.InFlight });
            var seconds = state?.ElapsedSeconds ?? 0;
            Shell.외부관찰표시(SelectedActorId.Length == 0 ? "NPC 업무 관찰" : "선택한 NPC", $"{TimeSpan.FromSeconds(Math.Min(seconds,359999)):mm\\:ss} / 05:00 · " + 업무배달관찰Store.상태이름(state?.Status ?? "Idle"),
                state == null ? "연결 확인" : 업무배달관찰Store.상태이름(state.Status), cards.ToArray(), Home, Back, resetScroll);
            resetScroll = false;
        }
        private void Home() { SelectedActorId = ""; resetScroll = true; Refresh(); ActorSelected?.Invoke(""); }
        private void Back() { if (SelectedActorId.Length == 0) Shell.닫기(); else Home(); }
        private static Card ActorCard(업무배달관찰Actor actor, string orderNo, Action click = null) => new Card {
            Title = actor.Name + " · " + 업무배달관찰Store.역할이름(actor.Role), Body = "주문번호 " + (orderNo.Length > 0 ? orderNo : "등록 전") + "\n" + Empty(actor.State),
            Chip = 업무배달관찰Store.역할이름(actor.Role) + " · " + (click != null ? "선택해서 관찰" : "현재 선택"), Click = click,
            Tint = Color.Lerp(RoleColor(actor.Role), Color.white, .94f), ChipColor = RoleColor(actor.Role) };
        private static string Empty(string value) => string.IsNullOrEmpty(value) ? "서버 결과 대기" : value;
        private static Color RoleColor(string role) => role == "restaurant" ? Orange : role == "driver" ? Teal : Purple;
    }
}
