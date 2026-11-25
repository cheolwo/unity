using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 매장 운영 창 (r215 · 2026-11-14 Claude 개발 계획 매1~매6. 사용자: 「너가 정한 대로 추천한 대로 구현 진행」).
    //  - 내 매장을 한 번 누르면 왼쪽(동 관리 창과 같은 자리)에 그 매장의 운영 창이 뜬다. 동 관리 창은 지역의 확장을, 이 창은 점포 하나의 운영을 맡는다.
    //    한 자리에 창은 하나다: 이 창을 열면 동 관리 창 · 운영 현황이 닫히고, 그 반대도 같다. 「← 동 관리」로 그 매장이 있는 동의 관리 창으로 돌아간다.
    //  - 지금은 가맹 카페(첫 사슬의 점포, 카페 1호점) 한 곳에 묶는다. 점포 업무의 상태 사본이 전역에 하나뿐이어서 다른 점포에 보이면 안 된다(r215 1절).
    //    배달 매장 A · B는 배정과 얽혀 있어 전과 같다(계획 D4). 한 번 누르기는 왼쪽 창만 연다(D2). 건물 정보는 빛의 차림표 「건물 정보」.
    //  - 탭: 요약(막힌 까닭과 바로 갈 행동) · 재료(본부 발주 · 검수 · 제조와 진행. 전의 오른쪽 카페 카드의 명령 그대로) · 설비 · 인력(업무가 아직 없다) · 이력.
    //  - 조회만으로 시간 · 돈 · 주문이 바뀌지 않는다. 명령은 확정 단추를 눌렀을 때만 보낸다(SendBusiness). 이어지지 않은 값은 「자료 미확인」이다.
    //  - 돈: 이 매장의 발주는 본부에 낼 돈(원장의 supply-payable)으로 적힌다. 동 관리 창의 자금에서 빠지지 않으므로 그 자금을 이 매장 돈으로 보이지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly string[] StoreDeskTabs = { "요약", "재료", "설비", "인력", "이력" };
        private static readonly string[] StoreHistoryKinds = { "전체", "발주", "입고·검수", "제조", "돈" };
        private const int StoreTabSummary = 0, StoreTabMaterials = 1, StoreTabEquipment = 2, StoreTabStaff = 3, StoreTabHistory = 4;
        private const float StoreBodyX = 15f;
        private const float StoreBodyWidth = DeskListWidth - 30f;

        private string storeDeskPlace;                    // 열린 매장 창의 장소 (닫혀 있으면 null)
        private int storeDeskTab;
        private float storeDeskScroll;
        private float storeDeskContent;
        private int storeHistoryKind;
        private float storeNoteAt = -100f;

        private bool StoreDeskReady => DongDeskReady;
        private bool StoreDeskVisible => storeDeskPlace != null && !sidePanelsOpen && StoreDeskReady && StoreDeskRole() != null;
        private bool StoreDeskCovers(Vector2 point) => StoreDeskVisible && DongDeskPanel.Contains(point);
        private BuildingRole StoreDeskRole() => StoreRole(storeDeskPlace);

        // 매장 창을 여는 곳인가. 지금은 첫 사슬의 가맹 카페뿐이다.
        private BuildingRole StoreRole(string place)
        {
            if (string.IsNullOrEmpty(place) || engine?.Pack == null || place != engine.Pack.network.store.placeId) return null;
            LoadRoleBook();
            if (!buildingByPlace.TryGetValue(place, out var index) || !roleByBuilding.TryGetValue(index, out var role)) return null;
            return RoleBusiness(role) == BusinessFranchise ? role : null;
        }

        // 그 자리로 간다. 매장이면 왼쪽 매장 창을, 아니면 전과 같이 오른쪽 카드를 연다.
        // 빛의 한 번 누르기 · 동 관리 창의 「이 동의 내 곳」 · 신호의 「가서 보기」 · 사업 칸의 「카페 1호점 보기」 · 사슬 띠가 이 길로 온다.
        private void GoToPlace(string place)
        {
            SelectPlace(place);
            if (StoreRole(place) == null || !StoreDeskReady) return;      // 매장 창을 쓸 수 없는 화면이면 전과 같이 오른쪽 카드
            selectedBuilding = null;
            OpenStoreDesk(place);
        }

        private void OpenStoreDesk(string place)
        {
            if (storeDeskPlace != place)
            {
                storeDeskTab = StoreTabSummary;
                storeDeskScroll = 0f;
            }
            storeDeskPlace = place;
            dongDeskOpen = false;
            sidePanelsOpen = false;
        }

        private string StoreRegion(string place) =>
            place != null && buildingByPlace.TryGetValue(place, out var index) && index >= 0 && index < buildingShapes.Length
                ? buildingShapes[index].Record.region : null;

        // 「← 동 관리」: 이 매장이 있는 동의 관리 창, 「이 동의 내 곳」을 펴 둔다.
        private void StoreDeskBack()
        {
            var region = StoreRegion(storeDeskPlace);
            storeDeskPlace = null;
            OpenDongDesk(region, DeskFold.Places);
        }

        private void StoreTab(int tab)
        {
            if (tab == storeDeskTab) return;
            storeDeskTab = tab;
            storeDeskScroll = 0f;
        }

        // ── 명령: 전에 오른쪽 카페 카드가 보내던 것 그대로 ──
        private void StoreOrder()
        {
            storeNoteAt = Time.unscaledTime;
            SendBusiness(BusinessIds.FranchiseSupply, "order", command => command.With("quantity", orderQuantity));
        }

        private void StoreInspect()
        {
            storeNoteAt = Time.unscaledTime;
            SendBusiness(BusinessIds.StoreOperations, "inspect", command => InspectCommand(command));
        }

        private void StoreProduce()
        {
            storeNoteAt = Time.unscaledTime;
            SendBusiness(BusinessIds.StoreOperations, "produce", command => command.With("cups", 1m));
        }

        // 이 매장에 적힌 돈(원장). bucket이 null이면 모두.
        private decimal StoreMoney(string place, string bucket)
        {
            decimal sum = 0;
            var ledger = engine?.Kernel?.Ledger;
            if (ledger == null) return 0;
            foreach (var entry in ledger)
                if (entry.Kind == LedgerKinds.Money && entry.PlaceId == place && (bucket == null || entry.Bucket == bucket)) sum += entry.Amount;
            return sum;
        }

        // ── 지도: 고른 매장의 표시 (빛 위, DrawRoleLight). 얇은 흰 고리와 이름표. 명덕의 빛과 다른 모양이다 ──
        private void StoreDeskMark(Vector2 top, string title)
        {
            var close = CloseNow;
            var ring = 20f * (1f + 1.2f * close);           // 가까이에서는 건물이 커진 만큼 고리도 키운다
            Hoop(top, ring, Fade(Paper, 0.95f), 2f);
            var tag = new Rect(top.x - 64, top.y - 28 - ring - 24f * close, 128, 22);
            if (close > 0.2f)
            {
                // 가까이에서는 이름표를 띄우고 이음선으로 잇는다
                Paint(new Rect(top.x - 0.8f, tag.yMax, 1.6f, Mathf.Max(0f, top.y - ring - tag.yMax)), Fade(Lilac, 0.9f));
                Disc(new Vector2(top.x, top.y - ring), 2.6f, Lilac);
            }
            Paint(tag, Lilac, 11);
            Paint(new Rect(tag.x + 1.5f, tag.y + 1.5f, tag.width - 3, tag.height - 3), Fade(Canvas, 0.95f), 10);
            Text(tag, title + " · 고름", 11, Paper, true, TextAnchor.MiddleCenter);
            storeMarkRect = Rect.MinMaxRect(Mathf.Min(tag.x, top.x - ring) - 10f, tag.y - 8f, Mathf.Max(tag.xMax, top.x + ring) + 10f, top.y + ring + 10f);   // 동 집중 카드가 피한다(SagajeongDispatchDongFocus.cs)
            storeMarkFrame = Time.frameCount;
        }

        // ── 오른쪽 건물 카드의 카페 칸: 매장 창이 있는 화면에서는 명령을 걷고 문 하나만 둔다(계획 D3) ──
        private void DrawStoreDoor(BuildingRole role, float x, float y, float width)
        {
            Text(new Rect(x, y, width, 34), "이 매장의 발주 · 검수 · 제조는 왼쪽 매장 운영 창에서 합니다.", 11, Paper, wrap: true);
            Text(new Rect(x, y + 36, width, 18), "지금: " + RoleStatus(role), 11, Gold, true);
            if (!ExpansionButton(new Rect(x, y + 62, width, 32), "왼쪽 매장 창 열기", true, Mint)) return;
            selectedBuilding = null;
            OpenStoreDesk(role.placeId);
        }

        // ── 그리기 (화면 좌표, OnGUI) ──
        private void DrawStoreDesk()
        {
            if (storeDeskPlace != null && sidePanelsOpen) storeDeskPlace = null;      // 운영 현황 칸을 폈다. 같은 자리이므로 닫는다
            if (!StoreDeskVisible) return;
            var role = StoreDeskRole();
            var place = storeDeskPlace;
            var store = ModuleSnapshot<StoreOperationsSnapshot>(BusinessIds.StoreOperations);
            var supply = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply);
            var panel = DongDeskPanel;
            var x = panel.x + 16;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);

            // 머리: 돌아가기 · 닫기 · 이름 · 운영 모델 · 소재 동과 상태
            var back = new Rect(x - 2, panel.y + 5, 84, 22);
            var close = new Rect(panel.xMax - 34, panel.y + 5, 24, 24);
            Text(back, "← 동 관리", 11, Mint, true);
            Text(close, "×", 18, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                storeDeskPlace = null;
                return;
            }
            if (Hit(back))
            {
                StoreDeskBack();
                return;
            }
            var picture = PictureDesk;                 // 그2: 그림 중심(시험)이면 머리에 매장 그림, 탭에 아이콘 (SagajeongDispatchIcons.cs)
            var head = picture ? 50f : 0f;
            pictureDeskDrawn = false;
            if (picture) DrawStoreHeadPicture(new Rect(x, panel.y + 28, 42, 40), place);
            Text(new Rect(x + head, panel.y + 26, 200, 26), role.title + " 운영", 19, Paper, true);
            var badge = new Rect(panel.xMax - 62, panel.y + 31, 44, 18);
            Paint(badge, Lilac, 9);
            Paint(new Rect(badge.x + 1.5f, badge.y + 1.5f, badge.width - 3, badge.height - 3), Canvas, 8);
            Text(badge, "가맹", 10, Lilac, true, TextAnchor.MiddleCenter);
            var region = StoreRegion(place);
            var dongName = region == null ? "—" : DongModuleById(region)?.Entry.name ?? "—";
            // 그림 중심(시험)에서는 머리 그림만큼 좁아지므로 본부 이름을 뺀다(가맹은 오른쪽 표, 본부는 재료 탭의 발주 줄에 있다).
            Text(new Rect(x + head, panel.y + 52, panel.width - 32 - head, 16),
                picture ? dongName + " · " + RoleStatus(role)
                    : dongName + " · " + (string.IsNullOrEmpty(supply?.HeadquartersName) ? "본부" : supply.HeadquartersName) + " 가맹 · " + RoleStatus(role), 10, Soft);

            // 탭 다섯
            var tabWidth = (panel.width - 32) / StoreDeskTabs.Length;
            for (var tab = 0; tab < StoreDeskTabs.Length; tab++)
            {
                var rect = new Rect(x + tab * tabWidth, panel.y + 70, tabWidth - 3, 24);
                var chosen = tab == storeDeskTab;
                if (chosen)
                {
                    Paint(rect, Tile, 6);
                    Paint(new Rect(rect.x + 8, rect.yMax - 2, rect.width - 16, 2), Mint, 1);
                }
                if (picture && Icon(new Rect(rect.x + 5, rect.y + 5, 14, 14), StoreTabIcons[tab], chosen ? Paper : Soft))
                    Text(new Rect(rect.x + 17, rect.y, rect.width - 18, rect.height), StoreDeskTabs[tab], 11, chosen ? Paper : Soft, chosen, TextAnchor.MiddleCenter);
                else
                    Text(rect, StoreDeskTabs[tab], 11, chosen ? Paper : Soft, chosen, TextAnchor.MiddleCenter);
                if (Hit(rect)) StoreTab(tab);
            }
            Paint(new Rect(x, panel.y + 98, panel.width - 32, 1), Rim);

            // 몸통. 넘치면 휠로 내린다. 지도는 패널 위의 휠을 받지 않는다(MapOverlayCovers).
            var body = new Rect(panel.x + 1, panel.y + 102, DeskListWidth, panel.height - 102 - 30);
            var overflow = Mathf.Max(0f, storeDeskContent - body.height);
            var wheel = Event.current;
            if (wheel.type == EventType.ScrollWheel && body.Contains(wheel.mousePosition))
            {
                storeDeskScroll += wheel.delta.y * 16f;
                wheel.Use();
            }
            storeDeskScroll = Mathf.Clamp(storeDeskScroll, 0f, overflow);

            // 칸 안에서는 누른 것을 적어만 두고, 칸을 닫은 뒤에 한 번 실행한다.
            Action run = null;
            GUI.BeginGroup(body);
            var y = 4f - storeDeskScroll;
            if (store == null || supply == null)
            {
                Text(new Rect(StoreBodyX, y, StoreBodyWidth, 34), "이 매장의 업무 자료가 없습니다.", 11, Soft, wrap: true);
                y += 34;
            }
            else
            {
                switch (storeDeskTab)
                {
                    case StoreTabMaterials:
                        y = StoreMaterials(y, store, supply, ref run);
                        break;
                    case StoreTabEquipment:
                        y = StoreEquipment(y);
                        break;
                    case StoreTabStaff:
                        y = StoreStaff(y);
                        break;
                    case StoreTabHistory:
                        y = StoreHistory(y, place, store, supply, ref run);
                        break;
                    default:
                        y = StoreSummary(y, place, store, supply, ref run);
                        break;
                }
            }
            storeDeskContent = y + storeDeskScroll + 4f;
            GUI.EndGroup();
            if (overflow > 0f)
            {
                var thumb = Mathf.Max(24f, body.height * body.height / storeDeskContent);
                Paint(new Rect(panel.xMax - 6, body.y + (body.height - thumb) * storeDeskScroll / overflow, 3, thumb), Fade(Soft, 0.55f), 2);
            }

            // 맨 아래: 방금 한 일 → 안내
            var recent = Time.unscaledTime - storeNoteAt < 4f && !string.IsNullOrEmpty(businessNotice);
            Text(new Rect(x, panel.yMax - 26, panel.width - 32, 20),
                recent ? businessNotice : overflow > 0f ? "휠로 내립니다 · 가상 운영" : "가상 운영 · 실제 주문 · 결제와 연결되지 않음",
                10, recent ? (businessNoticeOk ? Mint : SignalRed) : Fade(Soft, 0.8f));
            run?.Invoke();
        }

        // ── 몸통의 부품 (칸 안의 좌표) ──
        private void StoreLabel(ref float y, string label)
        {
            Text(new Rect(StoreBodyX, y, StoreBodyWidth, 16), label, 10, Soft);
            y += 18;
        }

        private void StoreRow(ref float y, string key, string value, Color valueColour, bool line = true, bool dim = false, float step = 26f)
        {
            Text(new Rect(StoreBodyX, y, 120, 20), key, 12, dim ? Fade(Soft, 0.8f) : Paper);
            Text(new Rect(StoreBodyX + 96, y, StoreBodyWidth - 96, 20), value, 10, valueColour, align: TextAnchor.MiddleRight);
            if (line) Paint(new Rect(StoreBodyX, y + step - 3, StoreBodyWidth, 1), Fade(Rim, 0.6f));
            y += step;
        }

        private void StoreChip(float x, float y, float width, string label, string value, Color colour)
        {
            Paint(new Rect(x, y, width, 36), Tile, 7);
            Text(new Rect(x + 8, y + 2, width - 12, 14), label, 9, Soft);
            Text(new Rect(x + 8, y + 16, width - 12, 18), value, 12, colour, true);
        }

        private bool StoreButton(float y, string label, bool enabled, Color colour)
        {
            var rect = new Rect(StoreBodyX + 8, y, StoreBodyWidth - 16, 26);
            Paint(rect, enabled ? colour : Panel, 6);
            Text(rect, label, 11, enabled ? Canvas : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
            return enabled && Hit(rect);
        }

        // 흐름 한 줄씩: 0 끝남 · 1 지금 · 2 아직
        private float StoreSteps(float y, IReadOnlyList<(string Title, string Line, int State)> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var (title, line, state) = steps[i];
                var colour = state == 0 ? Mint : state == 1 ? Gold : Fade(Soft, 0.45f);
                var dot = new Rect(StoreBodyX + 4, y + 5, 10, 10);
                if (state < 2) Paint(dot, colour, 5);
                else
                {
                    Paint(dot, colour, 5);
                    Paint(new Rect(dot.x + 1.5f, dot.y + 1.5f, 7, 7), Canvas, 4);
                }
                if (i < steps.Count - 1) Paint(new Rect(StoreBodyX + 8.5f, y + 16, 1, 9), Fade(Rim, 0.9f));
                Text(new Rect(StoreBodyX + 22, y, 110, 20), title, 11, state < 2 ? Paper : Fade(Soft, 0.75f), state == 1);
                if (!string.IsNullOrEmpty(line))
                    Text(new Rect(StoreBodyX + 120, y, StoreBodyWidth - 120, 20), line, 9, state < 2 ? colour : Fade(Soft, 0.6f), align: TextAnchor.MiddleRight);
                y += 24;
            }
            return y;
        }

        private string StoreTruckLine()
        {
            if (chain.TruckMoving) return chain.TruckName + " · " + chain.TruckSecondsLeft / 60 + ":" + (chain.TruckSecondsLeft % 60).ToString("00") + " 뒤";
            if (chain.WaitingInspection) return chain.TruckName + " · 도착 · 검수 대기";
            return "없음";
        }

        // ── 요약: 지금 · 왜 막혔나 · 진행 중 ──
        private float StoreSummary(float y, string place, StoreOperationsSnapshot store, FranchiseSupplySnapshot supply, ref Action run)
        {
            if (PictureDesk) return StoreSummaryPicture(y, place, store, supply, ref run);      // 그3: 그림 중심(시험) (SagajeongDispatchPictureDesk.cs)
            StoreLabel(ref y, "지금");
            var half = (StoreBodyWidth - 6) / 2f;
            var current = chain.Steps.FirstOrDefault(step => step.State == ChainStepStates.Now);
            StoreChip(StoreBodyX, y, half, "지금 단계", !chain.Started ? "시작 전" : current == null ? "끝까지 이어짐" : current.Title, Paper);
            StoreChip(StoreBodyX + half + 6, y, half, "기다리는 주문",
                store.WaitingCups > 0 ? store.Menu + " " + store.WaitingCups.ToString("0") + "잔" : "없음", store.WaitingCups > 0 ? Gold : Paper);
            var stocked = store.AvailableContent > 0;
            StoreChip(StoreBodyX, y + 42, half, "재료 · " + store.ItemName,
                stocked ? store.AvailableContent.ToString("N0") + store.ContentUnit : "없음", stocked ? Mint : SignalRed);
            StoreChip(StoreBodyX + half + 6, y + 42, half, "본부 대금 · 매출",
                StoreMoney(place, "supply-payable").ToString("N0") + " · " + store.Sales.ToString("N0") + "원", Gold);
            y += 86;

            // 왜 막혔나 → 바로 갈 행동 (지금 할 일이 있을 때만). 문장은 그림 중심과 같다(StoreReason).
            var reason = StoreReason(store, out var action);
            var box = new Rect(StoreBodyX, y, StoreBodyWidth, action == null ? 44 : 64);
            Paint(box, Tile, 8);
            Paint(new Rect(box.x, box.y, 3, box.height), action == null ? Mint : Gold, 1);
            Text(new Rect(box.x + 10, box.y + 3, 200, 18), action == null ? "지금" : "왜 막혔나", 11, action == null ? Mint : Gold, true);
            Text(new Rect(box.x + 10, box.y + 20, box.width - 18, 20), reason, 11, Paper);
            if (action != null)
            {
                var go = new Rect(box.x + 10, box.y + 41, 128, 18);
                Paint(go, Mint, 6);
                Text(go, action, 10, Canvas, true, TextAnchor.MiddleCenter);
                Text(new Rect(go.xMax + 8, go.y, box.width - go.width - 26, 20), "재료 탭에서 한다", 9, Soft);
                if (Hit(go)) run = () => StoreTab(StoreTabMaterials);
            }
            y += box.height + 8;

            StoreLabel(ref y, "진행 중");
            StoreRow(ref y, "들어오는 차", StoreTruckLine(), chain.TruckMoving || chain.WaitingInspection ? Sky : Soft, step: 24f);
            StoreRow(ref y, "설비", "자료 미확인 · 아직 연결 안 됨", Fade(Soft, 0.75f), step: 24f);
            StoreRow(ref y, "직원", "자료 미확인 · 점포 명부 없음", Fade(Soft, 0.75f), false, step: 24f);
            return y;
        }

        // ── 재료: 재고 · 주 행동(본부 발주 / 검수 / 제조) · 진행 · 다른 길 ──
        private float StoreMaterials(float y, StoreOperationsSnapshot store, FranchiseSupplySnapshot supply, ref Action run)
        {
            if (PictureDesk)      // 그3: 재고 막대 (SagajeongDispatchPictureDesk.cs)
            {
                y = StoreStockPicture(y, store, supply);
                StoreRow(ref y, store.Menu + " 주문", store.WaitingCups > 0 ? store.WaitingCups.ToString("0") + "잔 대기" : "없음",
                    store.WaitingCups > 0 ? Gold : Soft, false);
            }
            else
            {
            StoreLabel(ref y, "재고");
            StoreRow(ref y, store.ItemName, "가용 " + store.AvailableContent.ToString("N0") + store.ContentUnit + " · 격리 " +
                store.QuarantineContent.ToString("N0") + store.ContentUnit, store.AvailableContent > 0 ? Mint : SignalRed);
            StoreRow(ref y, "오는 물량", StoreTruckLine(), chain.TruckMoving || chain.WaitingInspection ? Sky : Soft);
            StoreRow(ref y, store.Menu + " 주문", store.WaitingCups > 0 ? store.WaitingCups.ToString("0") + "잔 대기" : "없음",
                store.WaitingCups > 0 ? Gold : Soft, false);
            }
            y += 4;

            var receipt = store.Receipts.LastOrDefault();
            if (chain.ActionKey == "order")
            {
                var box = new Rect(StoreBodyX, y, StoreBodyWidth, 142);
                Paint(box, Tile, 8);
                Paint(new Rect(box.x, box.y, 3, box.height), Mint, 1);
                Text(new Rect(box.x + 10, box.y + 4, 150, 20), "본부 발주", 12, Mint, true);
                Text(new Rect(box.xMax - 70, box.y + 4, 62, 20), "주 행동", 9, Mint, align: TextAnchor.MiddleRight);
                var centre = engine.Pack?.center?.name;
                Text(new Rect(box.x + 10, box.y + 24, box.width - 18, 16),
                    (string.IsNullOrEmpty(supply.HeadquartersName) ? "본부" : supply.HeadquartersName) + " → " +
                    (string.IsNullOrEmpty(centre) ? "물류센터" : centre) + " → 이 매장", 9, Soft);
                if (orderQuantity <= 0) orderQuantity = (int)supply.Recommended;
                Stepper(new Rect(box.x + 10, box.y + 44, box.width - 20, 24), "수량 (" + supply.Unit + ")", ref orderQuantity,
                    (int)supply.Minimum, (int)supply.Maximum);
                var preview = engine.BusinessPreview(new GameCommand { OsId = BusinessIds.FranchiseSupply, Action = "order" }
                    .With("quantity", orderQuantity));
                if (PictureDesk) PackPictures(new Rect(box.x + 10, box.y + 72, box.width - 20, 16), orderQuantity);      // 그3: 팩 그림
                Text(new Rect(box.x + 10, box.y + 72, box.width - 18, 16), "추천 " + supply.Recommended.ToString("0") + supply.Unit +
                    " · 한 잔에 " + store.ContentPerCup.ToString("0") + store.ContentUnit, 9, Soft);
                Text(new Rect(box.x + 10, box.y + 88, box.width - 18, 16),
                    preview.CanConfirm ? preview.Message + " · 본부에 낼 돈으로 적힘" : preview.Message, 9, preview.CanConfirm ? Gold : SignalRed);
                var confirm = new Rect(box.x + 10, box.y + 110, box.width - 20, 26);
                Paint(confirm, preview.CanConfirm ? Mint : Panel, 6);
                Text(confirm, 확정말("본부 발주 확정", 다음할일Policy.발주키(storeDeskPlace)), 11, preview.CanConfirm ? Canvas : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
                if (preview.CanConfirm && Hit(confirm)) run = 턴계획모드 ? (Action)(() => 매장계획담기(계획종류.발주)) : StoreOrder;
                할일강조그리기(box, 다음할일Policy.발주키(storeDeskPlace));
                y += box.height + 10;
            }
            else if (chain.ActionKey == "inspect" && receipt != null)
            {
                if (inspectReceipt != receipt.Id)
                {
                    inspectReceipt = receipt.Id;
                    inspectAccept = (int)receipt.SuggestAccept;
                    inspectQuarantine = (int)receipt.SuggestQuarantine;
                    inspectReject = 0;
                }
                var box = new Rect(StoreBodyX, y, StoreBodyWidth, 126);
                Paint(box, Tile, 8);
                Paint(new Rect(box.x, box.y, 3, box.height), Gold, 1);
                Text(new Rect(box.x + 10, box.y + 4, 150, 20), "검수", 12, Gold, true);
                Text(new Rect(box.x + 10, box.y + 24, box.width - 18, 16),
                    "하차 " + receipt.Unloaded.ToString("0") + receipt.Unit + " · 추천: " + receipt.SuggestReason, 9, Soft);
                var unloaded = (int)receipt.Unloaded;
                var cell = (box.width - 20 - 8) / 3f;
                Stepper(new Rect(box.x + 10, box.y + 44, cell, 24), "인수", ref inspectAccept, 0, unloaded);
                Stepper(new Rect(box.x + 14 + cell, box.y + 44, cell, 24), "격리", ref inspectQuarantine, 0, unloaded);
                Stepper(new Rect(box.x + 18 + cell * 2, box.y + 44, cell, 24), "거절", ref inspectReject, 0, unloaded);
                var preview = engine.BusinessPreview(InspectCommand(new GameCommand { OsId = BusinessIds.StoreOperations, Action = "inspect" }));
                Text(new Rect(box.x + 10, box.y + 72, box.width - 18, 16), preview.Message, 9, preview.CanConfirm ? Soft : SignalRed);
                var confirm = new Rect(box.x + 10, box.y + 94, box.width - 20, 26);
                Paint(confirm, preview.CanConfirm ? Mint : Panel, 6);
                Text(confirm, 확정말("검수 확정", 다음할일Policy.검수키(storeDeskPlace)), 11, preview.CanConfirm ? Canvas : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
                if (preview.CanConfirm && Hit(confirm)) run = 턴계획모드 ? (Action)(() => 매장계획담기(계획종류.검수)) : StoreInspect;
                할일강조그리기(box, 다음할일Policy.검수키(storeDeskPlace));
                y += box.height + 10;
            }
            else if (chain.ActionKey == "produce")
            {
                var box = new Rect(StoreBodyX, y, StoreBodyWidth, 84);
                Paint(box, Tile, 8);
                Paint(new Rect(box.x, box.y, 3, box.height), Gold, 1);
                Text(new Rect(box.x + 10, box.y + 4, 150, 20), "제조", 12, Gold, true);
                var preview = engine.BusinessPreview(new GameCommand { OsId = BusinessIds.StoreOperations, Action = "produce" }.With("cups", 1m));
                Text(new Rect(box.x + 10, box.y + 24, box.width - 18, 28), preview.Message, 9, preview.CanConfirm ? Soft : SignalRed, wrap: true);
                var confirm = new Rect(box.x + 10, box.y + 52, box.width - 20, 26);
                Paint(confirm, preview.CanConfirm ? Mint : Panel, 6);
                Text(confirm, 확정말(store.Menu + " 1잔 제조", 다음할일Policy.제조키(storeDeskPlace)), 11, preview.CanConfirm ? Canvas : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
                if (preview.CanConfirm && Hit(confirm)) run = 턴계획모드 ? (Action)(() => 매장계획담기(계획종류.제조)) : StoreProduce;
                할일강조그리기(box, 다음할일Policy.제조키(storeDeskPlace));
                y += box.height + 10;
            }

            if (chain.Started)
            {
                StoreLabel(ref y, "진행 · " + chain.Title.Replace("첫 인계 사슬 · ", string.Empty));
                var steps = chain.Steps.Select(step => (step.Title, step.Line,
                    step.State == ChainStepStates.Done ? 0 : step.State == ChainStepStates.Now ? 1 : 2)).ToList();
                y = StoreSteps(y, steps) + 6;
            }

            StoreLabel(ref y, "다른 길");
            StoreRow(ref y, "국내 구매", "가맹 계약 허용 여부 자료 미확인", Fade(Soft, 0.6f), true, true);
            StoreRow(ref y, "직접수입 검토", "수입 담당 없음 · 절차 준비 중", Fade(Soft, 0.6f), false, true);
            return y;
        }

        // ── 설비: 업무가 아직 없다. 지금 설비와 들이는 흐름만 ──
        private float StoreEquipment(float y)
        {
            if (PictureDesk) return StoreEquipmentPicture(y);      // 그3: 빈 탭 그림
            StoreLabel(ref y, "지금 설비");
            StoreRow(ref y, "에스프레소 머신", "자료 미확인", Soft);
            StoreRow(ref y, "그라인더", "자료 미확인", Soft);
            StoreRow(ref y, "제빙기", "자료 미확인", Soft, false);
            y += 6;
            StoreLabel(ref y, "설비 들이기 (업무가 연결되면)");
            y = StoreSteps(y, new List<(string, string, int)>
            {
                ("고르기", "성능 · 공간 · 전기 · 급배수", 2), ("구매 확정", null, 2), ("도착", null, 2), ("설치 · 점검", null, 2), ("사용 가능", null, 2),
            }) + 4;
            StoreButton(y, "설비 업무가 아직 없습니다", false, Mint);
            y += 34;
            Text(new Rect(StoreBodyX, y, StoreBodyWidth, 30), "구입 단추만으로 바로 만들 수 있는 잔 수가 늘지 않는다.", 9, Fade(Soft, 0.85f), wrap: true);
            return y + 30;
        }

        // ── 인력: 점포 직원. 동 관리 창의 기사 고용과 다르다 ──
        private float StoreStaff(float y)
        {
            if (PictureDesk) return StoreStaffPicture(y);      // 그3: 빈 탭 그림
            StoreLabel(ref y, "이 매장의 직원");
            Paint(new Rect(StoreBodyX, y, StoreBodyWidth, 32), Tile, 7);
            Text(new Rect(StoreBodyX + 10, y, StoreBodyWidth - 16, 32), "자료 미확인 · 점포별 명부가 아직 없다", 11, Soft);
            y += 42;
            StoreLabel(ref y, "바리스타 고용 (업무가 연결되면)");
            y = StoreSteps(y, new List<(string, string, int)>
            {
                ("후보 보기", null, 2), ("조건 확인", "근무 · 비용 · 시작일", 2), ("고용 확정", null, 2), ("근무 시작", null, 2), ("제조 자리 배치", null, 2),
            }) + 4;
            StoreButton(y, "직원 업무가 아직 없습니다", false, Mint);
            y += 34;
            Text(new Rect(StoreBodyX, y, StoreBodyWidth, 30), "배달 기사 고용은 동 관리 창에서 한다. 여기 직원과 다르다.", 9, Fade(Soft, 0.85f), wrap: true);
            return y + 30;
        }

        // ── 이력: 발주 · 입고 · 검수 · 제조 · 돈을 최근 것부터 ──
        private float StoreHistory(float y, string place, StoreOperationsSnapshot store, FranchiseSupplySnapshot supply, ref Action run)
        {
            var chipX = StoreBodyX;
            for (var kind = 0; kind < StoreHistoryKinds.Length; kind++)
            {
                var label = StoreHistoryKinds[kind];
                var width = 18f + label.Length * 10f;
                var chip = new Rect(chipX, y, width, 20);
                var chosen = kind == storeHistoryKind;
                Paint(chip, chosen ? Mint : Tile, 10);
                Text(chip, label, 10, chosen ? Canvas : Paper, chosen, TextAnchor.MiddleCenter);
                if (Hit(chip))
                {
                    var picked = kind;
                    run = () => storeHistoryKind = picked;
                }
                chipX += width + 4;
            }
            y += 28;

            var items = new List<(long At, int Kind, string Title, string Line, Color Colour)>();
            foreach (var order in supply.Orders)
                items.Add((order.SubmittedAtMs, 1, "본부 발주", supply.ItemName + " " + order.RequestedQuantity.ToString("0") + supply.Unit, Mint));
            foreach (var receipt in store.Receipts)
            {
                items.Add((receipt.ArrivedAtMs, 2, "입고", "하차 " + receipt.Unloaded.ToString("0") + receipt.Unit, Sky));
                if (receipt.Stage == ReceiptStages.Inspected)
                    items.Add((receipt.InspectedAtMs, 2, "검수", "인수 " + receipt.Accepted.ToString("0") + " · 격리 " + receipt.Quarantined.ToString("0") +
                        " · 거절 " + receipt.Rejected.ToString("0"), Sky));
            }
            foreach (var production in store.Productions)
                items.Add((production.AtMs, 3, "제조", production.Menu + " " + production.Cups.ToString("0") + "잔 · " +
                    production.ContentUsed.ToString("0") + store.ContentUnit, Gold));
            var ledger = engine?.Kernel?.Ledger;
            if (ledger != null)
            {
                foreach (var entry in ledger)
                {
                    if (entry.Kind != LedgerKinds.Money || entry.PlaceId != place) continue;
                    var title = entry.Bucket == "sales" ? "판매" : entry.Bucket == "supply-payable" ? "본부 대금" : entry.Bucket;
                    items.Add((entry.AtMs, 4, title, (entry.Amount > 0 && entry.Bucket != "sales" ? "+" : string.Empty) + entry.Amount.ToString("N0") + "원", Lilac));
                }
            }
            var shown = items.Where(item => storeHistoryKind == 0 || item.Kind == storeHistoryKind).OrderByDescending(item => item.At).ToList();
            if (shown.Count == 0)
            {
                Text(new Rect(StoreBodyX, y, StoreBodyWidth, 20), "아직 기록이 없습니다.", 11, Soft);
                y += 26;
            }
            foreach (var (at, _, title, line, colour) in shown)
            {
                var row = new Rect(StoreBodyX, y, StoreBodyWidth, 26);
                Paint(row, Tile, 6);
                Paint(new Rect(row.x, row.y, 3, row.height), colour, 1);
                Text(new Rect(row.x + 9, row.y, 46, row.height), StoreAgo(at), 9, Soft);
                Text(new Rect(row.x + 54, row.y, 70, row.height), title, 11, colour, true);
                Text(new Rect(row.x + 120, row.y, row.width - 128, row.height), line, 10, Paper, align: TextAnchor.MiddleRight);
                y += 30;
            }
            y += 2;
            Text(new Rect(StoreBodyX, y, StoreBodyWidth, 30), "본부 대금은 본부에 낼 돈(미지급)이다. 동 관리 창의 자금에서 빠지지 않는다.", 9, Fade(Soft, 0.85f), wrap: true);
            return y + 30;
        }

        private int StorePayableLines(string place) => engine?.Kernel?.Ledger == null ? 0 :
            engine.Kernel.Ledger.Count(entry => entry.Kind == LedgerKinds.Money && entry.PlaceId == place && entry.Bucket == "supply-payable");

        // 게임 시각으로 몇 분 전인가.
        private string StoreAgo(long atMs)
        {
            var minutes = (BusinessNow - atMs) / 60000;
            return minutes < 1 ? "방금" : minutes < 60 ? minutes + "분 전" : minutes / 60 + "시간 전";
        }

        // 방향 촬영: 카페 빛을 누른 길(GoToPlace)로 매장 창을 열고 다섯 탭을 찍는다. 여는 것 · 탭을 바꾸는 것은 시계 · 원장 · 발주를 바꾸지 않아야 한다.
        // 본부 발주를 해야 할 때면 창의 확정과 같은 명령(StoreOrder)을 한 번 보내 발주가 하나 · 이 매장의 본부 대금 줄이 하나 늘었는지 본다
        // (원장에는 센터의 재고 예약 줄도 함께 적힌다). 「← 동 관리」로 돌아간다.
        private IEnumerator ViewProofStoreDesk(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            lightMenu = null;
            storeNoteAt = -100f;
            var place = engine?.Pack?.network.store.placeId;
            receipt.storePlace = place ?? string.Empty;
            var supplyBefore = ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply);
            var ordersBefore = supplyBefore?.Orders.Count ?? 0;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            OpenDongDesk(BusinessDong()?.Entry.regionId);         // 동 관리 창이 떠 있다가 매장 창으로 바뀌는가
            yield return null;
            GoToPlace(place);
            // 촬영에서만: 그 매장을 화면 가운데로(지도의 고름 표시가 보이게). 배율은 그대로다.
            if (place != null && buildingByPlace.TryGetValue(place, out var cafe) && cafe >= 0 && cafe < buildingShapes.Length)
            {
                var mid = BuildingMid(cafe);
                ViewProofCentre(buildingIndex.origin.latitude + mid.y / buildingIndex.metresPerDegreeLatitude,
                    buildingIndex.origin.longitude + mid.x / buildingIndex.metresPerDegreeLongitude, mapZoom);
            }
            storeDeskTab = StoreTabSummary;
            yield return null;
            yield return null;
            receipt.storeDeskOpen = StoreDeskVisible;
            receipt.storeOneAtATime = StoreDeskVisible && !DongDeskVisible && selectedBuilding == null;
            receipt.storeCoversMap = StoreDeskCovers(DongDeskPanel.center) && MapOverlayCovers(DongDeskPanel.center);
            receipt.storeActionKey = chain.ActionKey;
            yield return SaveFrame(directory, "52-store-summary.png");
            var shots = new[] { (StoreTabMaterials, "53-store-materials.png"), (StoreTabEquipment, "54-store-equipment.png"),
                (StoreTabStaff, "55-store-staff.png"), (StoreTabHistory, "56-store-history.png") };
            foreach (var (tab, file) in shots)
            {
                StoreTab(tab);
                yield return null;
                yield return null;
                yield return SaveFrame(directory, file);
            }
            // 그2: 그림 중심(시험)을 켠 매장 창. 기본(꺼짐)의 장면 52~56은 위와 같다.
            receipt.pictureDeskDefaultOff = !PictureDesk;
            pictureDesk = true;
            pictureDeskRead = true;
            pictureStockDrawn = false;
            foreach (var (tab, file) in new[] { (StoreTabSummary, "71-store-picture-summary.png"), (StoreTabMaterials, "72-store-picture-materials.png"),
                (StoreTabEquipment, "74-store-picture-equipment.png"), (StoreTabStaff, "75-store-picture-staff.png") })
            {
                StoreTab(tab);
                yield return null;
                yield return null;
                yield return SaveFrame(directory, file);
            }
            receipt.pictureDeskDrawn = pictureDeskDrawn;
            receipt.pictureSummaryDrawn = pictureSummaryDrawn;
            receipt.pictureStockDrawn = pictureStockDrawn;
            pictureDesk = false;
            StoreTab(StoreTabSummary);
            yield return null;
            receipt.storeUnchangedOnOpen = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore &&
                (ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders.Count ?? 0) == ordersBefore;

            // 본부 발주를 해야 할 때만: 확정 한 번
            receipt.storeOrderTried = chain.ActionKey == "order";
            if (receipt.storeOrderTried)
            {
                StoreTab(StoreTabMaterials);
                orderQuantity = (int)(supplyBefore?.Recommended ?? 0);
                var ledgerAtOrder = engine.Kernel.Ledger.Count;
                var payableAtOrder = StorePayableLines(place);
                StoreOrder();
                yield return null;
                yield return null;
                receipt.storeOrdersAdded = (ModuleSnapshot<FranchiseSupplySnapshot>(BusinessIds.FranchiseSupply)?.Orders.Count ?? 0) - ordersBefore;
                receipt.storeLedgerAdded = engine.Kernel.Ledger.Count - ledgerAtOrder;
                receipt.storePayableAdded = StorePayableLines(place) - payableAtOrder;
                receipt.storeOrderNotice = businessNotice ?? string.Empty;
                yield return SaveFrame(directory, "57-store-after-order.png");
                StoreTab(StoreTabHistory);
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "58-store-history-after.png");
                StoreTab(StoreTabSummary);
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "59-store-summary-after.png");
                // 그3: 같은 순간을 그림 중심(시험)으로 (흐름 줄에서 발주가 끝난 모습)
                pictureDesk = true;
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "73-store-picture-after-order.png");
                receipt.pictureSummaryAfterOrder = pictureSummaryDrawn;
                pictureDesk = false;
                yield return null;
            }

            // 건물 정보(차림표의 길): 오른쪽 카드의 카페 칸은 문 하나다
            storeDeskPlace = null;
            SelectPlace(place);
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "60-store-building-card.png");
            selectedBuilding = null;

            // 「← 동 관리」
            GoToPlace(place);
            yield return null;
            StoreDeskBack();
            yield return null;
            yield return null;
            receipt.storeBackToDong = DongDeskVisible && !StoreDeskVisible;
            dongDeskOpen = false;
            storeDeskPlace = null;
            yield return null;
            holdProofClock = false;
        }
    }
}
