using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect StoreFocusPanel = new(970, 209, 250, 266);
        private static readonly Rect CourierFocusPanel = new(970, 209, 250, 392);
        private const int BeforeDrivingMinutes = 120;
        private const int AfterDrivingMinutes = 180;
        private (FocusKind Kind, string Id)? actorFocus;

        private enum FocusKind { Courier, Store, Drop, Rider, Cargo, Center, Inspection } // 기존 값은 유지하고 검수 관찰을 끝에 붙인다.

        private bool IsFocused(FocusKind kind, string id) =>
            actorFocus is { } focus && focus.Kind == kind && focus.Id == id;

        private static Rect FocusPanelFor(FocusKind kind) => kind == FocusKind.Courier ? CourierFocusPanel : StoreFocusPanel;

        // 홍달 기사 휴대폰이 떠 있으면 그 휴대폰 자리(SagajeongDispatchRiderPhone.cs), 아니면 카드 자리.
        private bool FocusCovers(Vector2 point) => actorFocus is { } focus &&
            (RolePhoneOn(focus.Kind) ? PhoneCovers(point) : FocusPanelNow(focus.Kind).Contains(point));

        private static (int Lower, int Upper) EstimateDailyDeliveries(int drivingMinutes) =>
            (drivingMinutes * 3 / 60, drivingMinutes * 4 / 60);

        private int SettledCourierDeliveries(string courierId) =>
            state.orders.Count(order => order.driverId == courierId && order.stage == "Settled");

        private void DrawDrivingCapacity(float x, float y, string courierId)
        {
            Paint(new Rect(x, y, 224, 117), Tile, 8);
            Text(new Rect(x + 9, y + 5, 206, 20), "하루 운행 계획 · 가상 예시", 13, Paper, true);
            Text(new Rect(x + 9, y + 27, 206, 17), "무념 시간에 비례 · 시간당 3~4건", 10, Soft);
            DrawDrivingCapacityColumn(x + 9, y + 46, "성장 전", BeforeDrivingMinutes, Paper);
            DrawDrivingCapacityColumn(x + 115, y + 46, "성장 후", AfterDrivingMinutes, Mint);
            Text(new Rect(x + 9, y + 96, 206, 16), $"오늘 실제 가상 정산 {SettledCourierDeliveries(courierId)}건", 10, Soft);
        }

        private void DrawDrivingCapacityColumn(float x, float y, string label, int drivingMinutes, Color valueColor)
        {
            var (lower, upper) = EstimateDailyDeliveries(drivingMinutes);
            Text(new Rect(x, y, 103, 19), $"{label} · {drivingMinutes / 60}시간", 11, Soft, true);
            Text(new Rect(x, y + 20, 103, 22), $"예상 {lower}~{upper}건", 15, valueColor, true);
        }

        private void DrawFocusPanel()
        {
            if (actorFocus is not { } focus || state == null) return;
            if (focus.Kind == FocusKind.Drop || focus.Kind == FocusKind.Rider)
            {
                DrawLightCard(focus.Kind, focus.Id);      // 배송지 · 고용한 기사 (SagajeongDispatchRoam.cs)
                return;
            }
            if (RolePhoneOn(focus.Kind))
            {
                DrawRolePhone(focus.Kind, focus.Id);      // 확장 화면에서는 기사 · 매장 카드 대신 역할 휴대폰, 화물 · 창고도 (SagajeongDispatchRiderPhone.cs)
                return;
            }

            var courier = focus.Kind == FocusKind.Courier
                ? state.couriers.FirstOrDefault(item => item.id == focus.Id)
                : null;
            var store = focus.Kind == FocusKind.Store
                ? state.stores.FirstOrDefault(item => item.id == focus.Id)
                : null;
            if (courier == null && store == null) return;

            var order = courier != null
                ? state.orders.FirstOrDefault(item => item.driverId == courier.id)
                : state.orders.FirstOrDefault(item => item.storeId == store.id);
            var title = courier != null ? "기사 " + Letter(courier.id) : "음식점 " + Letter(store.id);
            var status = order != null ? StageLabel(order.stage) :
                courier != null && running ? "신규 배차 대기" : "현재 연결된 주문 없음";
            var detail = courier != null
                ? order == null ? "아직 맡은 주문이 없습니다." : order.storeName + " · " + order.menu
                : order == null ? "현재 주문이 없습니다." : order.menu;
            var dispatch = "";
            if (courier != null && selectedDriver == courier.id)
                dispatch = "배차 선택됨 · 대기 매장을 누르세요.";
            else if (store != null && !string.IsNullOrEmpty(order?.driverId))
                dispatch = "담당 기사 " + Letter(order.driverId);
            else if (store != null && running && !string.IsNullOrEmpty(order?.recommendedDriverId))
                dispatch = "추천 기사 " + Letter(order.recommendedDriverId);

            var focusPanel = FocusPanelNow(focus.Kind);      // 빛으로 보는 동안에는 오른쪽 칸 한 자리 (SagajeongDispatchRoam.cs)
            var x = focusPanel.x + 13;
            var y = focusPanel.y;
            Paint(focusPanel, Rim, 10);
            Paint(new Rect(focusPanel.x + 1, y + 1, focusPanel.width - 2, focusPanel.height - 2), Canvas, 9);
            Text(new Rect(x, y + 11, 168, 19), courier != null ? "음식 배달 · 기사" : "음식 배달 · 음식점", 11, Mint, true);
            Text(new Rect(x, y + 34, 168, 29), title, 20, Paper, true);
            var close = new Rect(focusPanel.xMax - 59, y + 12, 47, 27);
            Paint(close, Tile, 6);
            Text(close, "닫기", 12, Paper, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                // 상세 창만 닫는다. 고른 기사는 배차할 때까지 유지한다 (r141 점검 3).
                actorFocus = null;
                return;
            }
            if (PhoneHasCard(focus.Kind) && PhoneArea(focus.Kind))
            {
                var phone = new Rect(focusPanel.xMax - 112, y + 12, 49, 27);      // 다시 홍달 기사 · 홍달 가게 휴대폰으로
                Paint(phone, Tile, 6);
                Text(phone, "휴대폰", 12, Mint, align: TextAnchor.MiddleCenter);
                if (Hit(phone))
                {
                    phoneCardView = false;
                    return;
                }
            }

            Text(new Rect(x, y + 72, 223, 23), status, 14, Gold, true);
            Text(new Rect(x, y + 98, 223, 23), detail, 13, Paper);
            Text(new Rect(x, y + 123, 223, 21), dispatch, 12, Mint);
            var historyOffset = 154;
            if (courier != null)
            {
                DrawGyeongFigures(x, y + 148, focusPanel.width - 26f, courier.id);      // 오늘 경 유지 · 경 시간당 순소득 (위 띠에서 옮겨 옴)
                DrawMindCard(x, y + 204, courier.id);
                historyOffset += 195;
            }

            Text(new Rect(x, y + historyOffset, 223, 20), "최근 관련 진행", 12, Soft, true);
            var events = order == null ? Array.Empty<DispatchEvent>() : (state.events ?? Array.Empty<DispatchEvent>())
                .Where(entry => entry.orderId == order.id).Reverse().Take(courier != null ? (focusPanel.height >= 420 ? 2 : 1) : 3).ToArray();
            if (events.Length == 0)
                Text(new Rect(x, y + historyOffset + 27, 223, 21), "아직 표시할 진행 기록이 없습니다.", 12, Soft);
            for (var index = 0; index < events.Length; index++)
            {
                var entry = events[index];
                var row = y + historyOffset + 27 + index * 24;
                Text(new Rect(x, row, 59, 21), ClockLabel(entry.at), 11, Soft);
                Text(new Rect(x + 62, row, 158, 21), EventLabel(entry.code, entry.text), 12, Paper);
            }
        }
    }
}
