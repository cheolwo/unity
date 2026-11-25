using System;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 그림 중심 왼쪽 창 — 그3 첫 조각: 매장 운영 창 「요약」 탭의 몸 (2026-11-15 밤 예약 회차).
    //  - 「왼쪽 창: 그림 중심(시험)」(PictureDesk)을 켰을 때만 쓴다. 기본(글 중심)은 전과 같다.
    //  - 제안서(Handoffs/2026-11-14-Claude-제안-왼쪽창-그림중심.md) 3절의 ② 아이콘 칸 넷 · 원인 → 결과 카드 · 흐름 줄.
    //  - 값은 글 중심과 같은 상태(StoreOperationsSnapshot · 사슬 투영 chain)에서 읽는다. 없는 값은 「자료 미확인」이다.
    //  - 보는 것만으로 시간 · 돈 · 주문이 바뀌지 않는다. 단추는 글 중심과 같은 일(재료 탭 열기)만 한다.
    public sealed partial class SagajeongDispatchView
    {
        // 사슬 여섯 걸음의 그림: 본부 발주 · 센터 출고 · 화물 운송 · 점포 검수 · 제조 · 배달 · 수령
        private static readonly string[] ChainIcons = { "receipt", "warehouse", "truck", "eye", "machine", "scooter" };
        private static readonly string[] ChainShort = { "발주", "출고", "운송", "검수", "제조", "배달" };
        private bool pictureSummaryDrawn;      // 이번 그리기에서 흐름 줄 여섯 칸을 그렸나 (촬영이 읽는다)

        // 왜 막혔나 · 바로 갈 행동. 글 중심과 그림 중심이 같은 문장을 쓴다.
        private string StoreReason(StoreOperationsSnapshot store, out string action)
        {
            action = null;
            switch (chain.ActionKey)
            {
                case "order":
                    action = "본부 발주로  →";
                    return store.WaitingCups > 0
                        ? store.ItemName + "가 없어 " + store.Menu + " " + store.WaitingCups.ToString("0") + "잔이 기다립니다."
                        : store.ItemName + "가 없습니다.";
                case "inspect":
                    action = "검수로  →";
                    return "화물이 도착했습니다. 검수를 기다립니다.";
                case "produce":
                    action = "제조로  →";
                    return store.Menu + " " + store.WaitingCups.ToString("0") + "잔 주문이 기다립니다. 만들 수 있습니다.";
                default:
                    return chain.TruckMoving ? "막힌 것이 없습니다. " + chain.TruckName + "이 오는 중입니다." : "막힌 것이 없습니다.";
            }
        }

        // 아이콘 칸 하나: 왼쪽에 아이콘, 오른쪽 위에 이름(작게), 아래에 값(크게)
        private void PictureCell(float x, float y, float width, string icon, Color iconColour, string label, string value, Color valueColour)
        {
            Paint(new Rect(x, y, width, 40), Tile, 7);
            Paint(new Rect(x + 6, y + 6, 28, 28), Fade(iconColour, 0.14f), 7);
            Icon(new Rect(x + 9, y + 9, 22, 22), icon, iconColour);
            Text(new Rect(x + 40, y + 3, width - 44, 14), label, 9, Soft);
            Text(new Rect(x + 40, y + 17, width - 44, 20), value, 12, valueColour, true);
        }

        private float StoreSummaryPicture(float y, string place, StoreOperationsSnapshot store, FranchiseSupplySnapshot supply, ref Action run)
        {
            pictureSummaryDrawn = false;
            StoreLabel(ref y, "지금");
            var half = (StoreBodyWidth - 6) / 2f;
            var nowIndex = chain.Steps.FindIndex(step => step.State == ChainStepStates.Now);
            var stepTitle = !chain.Started ? "시작 전" : nowIndex < 0 ? "끝까지 이어짐" : chain.Steps[nowIndex].Title;
            var stepIcon = nowIndex >= 0 && nowIndex < ChainIcons.Length ? ChainIcons[nowIndex] : chain.Started ? "check" : "clock";
            PictureCell(StoreBodyX, y, half, stepIcon, nowIndex >= 0 ? Gold : Mint, "지금 단계", stepTitle, Paper);
            PictureCell(StoreBodyX + half + 6, y, half, "cup", store.WaitingCups > 0 ? Gold : Soft, "기다리는 주문",
                store.WaitingCups > 0 ? store.Menu + " " + store.WaitingCups.ToString("0") + "잔" : "없음", store.WaitingCups > 0 ? Gold : Paper);
            var stocked = store.AvailableContent > 0;
            PictureCell(StoreBodyX, y + 46, half, "milk", stocked ? Mint : SignalRed, "재료 · " + store.ItemName,
                stocked ? store.AvailableContent.ToString("N0") + store.ContentUnit : "없음", stocked ? Mint : SignalRed);
            PictureCell(StoreBodyX + half + 6, y + 46, half, "won", Gold, "본부 대금 · 매출",
                StoreMoney(place, "supply-payable").ToString("N0") + " · " + store.Sales.ToString("N0") + "원", Gold);
            y += 94;

            // 원인 → 결과: 그림 두 개와 화살표, 한 문장, 단추
            var reason = StoreReason(store, out var action);
            var box = new Rect(StoreBodyX, y, StoreBodyWidth, action == null ? 50 : 78);
            Paint(box, Tile, 8);
            Paint(new Rect(box.x, box.y, 3, box.height), action == null ? Mint : Gold, 1);
            string cause = null, effect = null;
            Color causeColour = SignalRed, effectColour = Gold;
            switch (chain.ActionKey)
            {
                case "order": cause = "milk"; effect = "cup"; break;
                case "inspect": cause = "truck"; effect = "eye"; causeColour = Sky; break;
                case "produce": cause = "milk"; effect = "cup"; causeColour = Mint; break;
            }
            var textX = box.x + 10;
            if (cause != null)
            {
                Icon(new Rect(box.x + 10, box.y + 8, 24, 24), cause, causeColour);
                Text(new Rect(box.x + 36, box.y + 8, 18, 24), "→", 13, Soft, true, TextAnchor.MiddleCenter);
                Icon(new Rect(box.x + 56, box.y + 8, 24, 24), effect, effectColour);
                if (store.WaitingCups > 0 && effect == "cup")
                    Text(new Rect(box.x + 80, box.y + 18, 24, 14), "×" + store.WaitingCups.ToString("0"), 9, Gold, true);
                textX = box.x + 108;
            }
            else
                Icon(new Rect(box.x + 10, box.y + 8, 20, 20), "check", Mint);
            Text(new Rect(cause != null ? textX : box.x + 36, box.y + 3, box.xMax - (cause != null ? textX : box.x + 36) - 6, 16),
                action == null ? "지금" : "왜 막혔나", 10, action == null ? Mint : Gold, true);
            Text(new Rect(cause != null ? textX : box.x + 36, box.y + 18, box.xMax - (cause != null ? textX : box.x + 36) - 6, 28),
                reason, 10, Paper, wrap: true);
            if (action != null)
            {
                var go = new Rect(box.x + 10, box.y + 52, 128, 20);
                Paint(go, Mint, 6);
                Text(go, action, 10, Canvas, true, TextAnchor.MiddleCenter);
                Icon(new Rect(go.xMax + 8, go.y + 2, 16, 16), "milk", Soft);
                Text(new Rect(go.xMax + 27, go.y, box.width - go.width - 44, 20), "재료 탭에서 한다", 9, Soft);
                if (Hit(go)) run = () => StoreTab(StoreTabMaterials);
            }
            y += box.height + 10;

            // 흐름 줄: 여섯 걸음을 아이콘으로 잇는다. 끝난 칸 민트 · 지금 칸 금색 · 아직 흐리게. 오는 화물은 줄 위의 화물차.
            StoreLabel(ref y, "흐름");
            var count = Mathf.Min(ChainIcons.Length, chain.Steps.Count);
            if (count > 0)
            {
                var gap = (StoreBodyWidth - 24f) / Mathf.Max(1, count - 1);
                Paint(new Rect(StoreBodyX + 12, y + 13, StoreBodyWidth - 24, 2), Fade(Rim, 0.9f), 1);
                for (var i = 0; i < count; i++)
                {
                    var state = chain.Steps[i].State;
                    var colour = state == ChainStepStates.Done ? Mint : state == ChainStepStates.Now ? Gold : Fade(Soft, 0.45f);
                    var centre = new Vector2(StoreBodyX + 12 + gap * i, y + 14);
                    if (i > 0 && state != ChainStepStates.Next)
                        Paint(new Rect(centre.x - gap, y + 13, gap, 2), Fade(Mint, 0.7f), 1);
                    Paint(new Rect(centre.x - 12, centre.y - 12, 24, 24), state == ChainStepStates.Now ? Gold : Canvas, 12);
                    Paint(new Rect(centre.x - 11, centre.y - 11, 22, 22), Canvas, 11);
                    Icon(new Rect(centre.x - 8, centre.y - 8, 16, 16), ChainIcons[i], colour);
                    Text(new Rect(centre.x - 20, y + 28, 40, 14), ChainShort[i], 9, state == ChainStepStates.Next ? Fade(Soft, 0.6f) : colour,
                        state == ChainStepStates.Now, TextAnchor.MiddleCenter);
                }
                if (chain.TruckMoving)
                {
                    // 화물차는 「출고」와 「검수」 사이를 남은 비율만큼 간다
                    var from = StoreBodyX + 12 + gap * 1;
                    var to = StoreBodyX + 12 + gap * 3;
                    var x = Mathf.Lerp(from, to, Mathf.Clamp01(chain.TruckProgress));
                    Icon(new Rect(x - 7, y - 4, 14, 14), "truck", Sky);
                }
                pictureSummaryDrawn = count == ChainIcons.Length;
                y += 46;
            }
            else
            {
                Text(new Rect(StoreBodyX, y, StoreBodyWidth, 18), "자료 미확인 · 사슬이 시작되지 않음", 10, Fade(Soft, 0.75f));
                y += 22;
            }
            StoreRow(ref y, "들어오는 차", StoreTruckLine(), chain.TruckMoving || chain.WaitingInspection ? Sky : Soft, step: 24f);
            StoreRow(ref y, "설비 · 직원", "자료 미확인 · 아직 연결 안 됨", Fade(Soft, 0.75f), false, step: 24f);
            return y;
        }
    }
}
