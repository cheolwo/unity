using System;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 그림 중심 왼쪽 창 — 그3: 매장 운영 창의 몸 (2026-11-15). 요약 탭(첫 조각) · 재료 탭의 재고 막대와 팩 그림 · 설비 · 인력 탭의 빈 탭 그림.
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
            Paint(new Rect(x, y, width, 38), Tile, 7);
            Paint(new Rect(x + 5, y + 5, 28, 28), Fade(iconColour, 0.14f), 7);
            Icon(new Rect(x + 8, y + 8, 22, 22), icon, iconColour);
            Text(new Rect(x + 39, y + 2, width - 43, 14), label, 9, Soft);
            Text(new Rect(x + 39, y + 16, width - 43, 20), value, 12, valueColour, true);
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
            PictureCell(StoreBodyX, y + 42, half, "milk", stocked ? Mint : SignalRed, "재료 · " + store.ItemName,
                stocked ? store.AvailableContent.ToString("N0") + store.ContentUnit : "없음", stocked ? Mint : SignalRed);
            PictureCell(StoreBodyX + half + 6, y + 42, half, "won", Gold, "본부 대금 · 매출",
                StoreMoney(place, "supply-payable").ToString("N0") + " · " + store.Sales.ToString("N0") + "원", Gold);
            y += 86;

            // 원인 → 결과: 그림 두 개와 화살표, 한 문장, 단추
            var reason = StoreReason(store, out var action);
            var box = new Rect(StoreBodyX, y, StoreBodyWidth, action == null ? 46 : 70);
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
                var go = new Rect(box.x + 10, box.y + 46, 128, 20);
                Paint(go, Mint, 6);
                Text(go, action, 10, Canvas, true, TextAnchor.MiddleCenter);
                Icon(new Rect(go.xMax + 8, go.y + 2, 16, 16), "milk", Soft);
                Text(new Rect(go.xMax + 27, go.y, box.width - go.width - 44, 20), "재료 탭에서 한다", 9, Soft);
                if (Hit(go)) run = () => StoreTab(StoreTabMaterials);
            }
            y += box.height + 12;

            // 흐름 줄: 여섯 걸음을 아이콘으로 잇는다. 끝난 칸 민트 · 지금 칸 금색 · 아직 흐리게. 오는 화물은 줄 위의 화물차.
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
                y += 44;
            }
            else
            {
                Text(new Rect(StoreBodyX, y, StoreBodyWidth, 18), "자료 미확인 · 사슬이 시작되지 않음", 10, Fade(Soft, 0.75f));
                y += 22;
            }
            StoreRow(ref y, "들어오는 차", StoreTruckLine(), chain.TruckMoving || chain.WaitingInspection ? Sky : Soft, step: 24f);
            StoreRow(ref y, "설비 · 직원", "자료 미확인 · 아직 연결 안 됨", Fade(Soft, 0.75f), false, step: 22f);
            return y;
        }

        // ── 재료 탭: 재고 막대 (가용 민트 · 격리 금색 · 오는 중 하늘). 막대 끝은 추천 발주량이다 ──
        private bool pictureStockDrawn;        // 이번 그리기에서 재고 막대를 그렸나 (촬영이 읽는다)

        private decimal IncomingPacks(FranchiseSupplySnapshot supply)
        {
            decimal packs = 0;
            foreach (var order in supply.Orders)
                packs += Math.Max(0m, order.AcceptedQuantity - order.ReceivedAccepted - order.ReceivedQuarantined - order.ReceivedRejected);
            return packs;
        }

        private float StoreStockPicture(float y, StoreOperationsSnapshot store, FranchiseSupplySnapshot supply)
        {
            StoreLabel(ref y, "재고");
            var perPack = store.ContentPerPack > 0 ? store.ContentPerPack : supply.ContentQuantity;
            var incoming = IncomingPacks(supply) * perPack;
            var total = store.AvailableContent + store.QuarantineContent + incoming;
            var scale = Math.Max(total, perPack * Math.Max(1m, supply.Recommended));
            Paint(new Rect(StoreBodyX, y, StoreBodyWidth, 58), Tile, 8);
            var stocked = store.AvailableContent > 0;
            Icon(new Rect(StoreBodyX + 8, y + 8, 22, 22), "milk", stocked ? Mint : SignalRed);
            Text(new Rect(StoreBodyX + 36, y + 4, StoreBodyWidth - 44, 16), store.ItemName, 11, Paper, true);
            Text(new Rect(StoreBodyX + 36, y + 4, StoreBodyWidth - 44, 16),
                "가용 " + store.AvailableContent.ToString("N0") + store.ContentUnit + " · 격리 " + store.QuarantineContent.ToString("N0") + store.ContentUnit,
                9, stocked ? Mint : SignalRed, align: TextAnchor.MiddleRight);
            var bar = new Rect(StoreBodyX + 36, y + 24, StoreBodyWidth - 46, 10);
            Paint(bar, Canvas, 5);
            if (scale > 0)
            {
                var x = bar.x;
                foreach (var (amount, colour) in new[] { (store.AvailableContent, Mint), (store.QuarantineContent, Gold), (incoming, Sky) })
                {
                    if (amount <= 0) continue;
                    var width = (float)(amount / scale) * bar.width;
                    Paint(new Rect(x, bar.y, Mathf.Max(2f, width), bar.height), colour, 4);
                    x += width;
                }
            }
            Text(new Rect(bar.x, y + 38, bar.width, 16),
                (incoming > 0 ? "오는 중 " + (incoming / Math.Max(1m, perPack)).ToString("0") + supply.Unit + " · " : "") + "막대 끝 = 추천 발주 " + supply.Recommended.ToString("0") + supply.Unit,
                9, Fade(Soft, 0.85f));
            pictureStockDrawn = true;
            return y + 64;
        }

        // 발주 칸의 팩 그림: 고른 수량만큼 작은 우유 그림 (열 개까지, 넘으면 「+n」)
        private void PackPictures(Rect line, int quantity)
        {
            var shown = Mathf.Clamp(quantity, 0, 10);
            var x = line.xMax - shown * 12f - (quantity > 10 ? 22f : 0f);
            for (var k = 0; k < shown; k++) Icon(new Rect(x + k * 12f, line.y + 1, 12, 12), "milk", Gold);
            if (quantity > 10) Text(new Rect(line.xMax - 22, line.y, 22, 14), "+" + (quantity - 10), 9, Gold, true, TextAnchor.MiddleRight);
        }

        // ── 설비 · 인력 탭: 업무가 아직 없을 때의 그림. 흐린 아이콘 · 한 문장 · 단계 아이콘 다섯 ──
        private float EmptyTabPicture(float y, string title, string[] icons, string sentence, string stepsTitle, string[] stepIcons, string[] steps, string note)
        {
            StoreLabel(ref y, title);
            var card = new Rect(StoreBodyX, y, StoreBodyWidth, 64);
            Paint(card, Fade(Tile, 0.6f), 8);
            for (var k = 0; k < 18; k++)      // 점선 테두리(업무가 아직 없다는 표시)
            {
                var t = k / 18f;
                Paint(new Rect(card.x + 4 + t * (card.width - 8), card.y, 6, 1), Fade(Soft, 0.35f));
                Paint(new Rect(card.x + 4 + t * (card.width - 8), card.yMax - 1, 6, 1), Fade(Soft, 0.35f));
            }
            for (var k = 0; k < icons.Length; k++) Icon(new Rect(card.x + 10 + k * 30, card.y + 8, 24, 24), icons[k], Fade(Soft, 0.55f));
            Text(new Rect(card.x + 10, card.y + 36, card.width - 18, 24), sentence, 10, Soft, wrap: true);
            y += 72;
            StoreLabel(ref y, stepsTitle);
            var gap = (StoreBodyWidth - 24f) / Mathf.Max(1, stepIcons.Length - 1);
            Paint(new Rect(StoreBodyX + 12, y + 11, StoreBodyWidth - 24, 1), Fade(Rim, 0.9f));
            for (var k = 0; k < stepIcons.Length; k++)
            {
                var cx = StoreBodyX + 12 + gap * k;
                Paint(new Rect(cx - 11, y, 22, 22), Canvas, 11);
                Icon(new Rect(cx - 8, y + 3, 16, 16), stepIcons[k], Fade(Soft, 0.5f));
                Text(new Rect(cx - 26, y + 24, 52, 14), steps[k], 9, Fade(Soft, 0.65f), align: TextAnchor.MiddleCenter);
            }
            y += 44;
            Text(new Rect(StoreBodyX, y, StoreBodyWidth, 30), note, 9, Fade(Soft, 0.85f), wrap: true);
            return y + 30;
        }

        private float StoreEquipmentPicture(float y) => EmptyTabPicture(y, "지금 설비",
            new[] { "machine", "grinder", "ice" }, "에스프레소 머신 · 그라인더 · 제빙기: 자료 미확인. 설비 업무가 아직 연결되지 않았다.",
            "설비 들이기 (업무가 연결되면)", new[] { "search", "receipt", "truck", "tool", "check" },
            new[] { "고르기", "구매", "도착", "설치", "사용" }, "구입 단추만으로 바로 만들 수 있는 잔 수가 늘지 않는다.");

        private float StoreStaffPicture(float y) => EmptyTabPicture(y, "이 매장의 직원",
            new[] { "barista", "people" }, "자료 미확인 · 점포별 명부가 아직 없다.",
            "바리스타 고용 (업무가 연결되면)", new[] { "search", "receipt", "check", "clock", "machine" },
            new[] { "후보", "조건", "확정", "근무", "자리" }, "배달 기사 고용은 동 관리 창에서 한다. 여기 직원과 다르다.");
    }
}
