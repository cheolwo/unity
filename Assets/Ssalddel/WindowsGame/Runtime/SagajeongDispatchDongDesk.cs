using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 동 관리 창 (2026-11-14, 사용자 방향: 문명 5의 도시 화면처럼).
    // 사용자의 말: 행정동 화면을 두 번 누르면 왼쪽에 목록이 뜬다. 접었다 폈다 할 수 있는 고용 목록, 건설 목록.
    //            도시를 눌러 유닛을 뽑고 건물을 짓듯이, 기사를 고용하고 음식점을 차린다.
    //  - 동을 두 번 누르면(동 이름표 · 동 카드 · 그 동의 땅과 건물 · 빛) 왼쪽에 이 창이 뜬다. 한 번은 전과 같이 고르기만 한다.
    //    동 카드의 「동 관리 열기」와 아래 사업 버튼(기사 운영 · 음식점)으로도 열린다.
    //  - 묶음: 고용(사람을 들인다) · 건설(거점을 늘린다) · 이 동의 내 곳 · 공통(배차 엔진). 묶음마다 접고 편다.
    //  - 명령은 전에 동 카드의 단추가 보내던 것 그대로다(모집 · 제휴 · 엔진 · 깃발). 커널을 바꾸지 않았고, 값과 못 하는 까닭도 커널의 사본에서 읽는다.
    //    기사와 제휴는 동별 수의 집계다. 한 사람 · 한 점포의 객체가 아니다.
    //  - 창을 열고 닫고 접고 펴는 것은 조회다. 시간 · 돈 · 주문을 바꾸지 않는다. 단추를 눌렀을 때만 명령이 한 번 간다.
    //  - 아직 없는 것: 「음식점 차리기」의 자리 고르기(다음 걸음), 음식점 안의 고용(커널에 점포와 직무가 없다).
    //  - 빛으로 보는 동네(행정동 모듈 + 동 깃발)의 정리된 화면에서만 쓴다. 검증 촬영(--sagajeong-…)에서는 동 카드의 단추가 전과 같이 나온다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect DongDeskPanel = new(22, 130, 300, 406);
        private enum DeskFold { Keep, Hire, Build, Places }

        private bool dongDeskOpen;
        private bool dongDeskHire = true;                 // 묶음이 펴져 있는가
        private bool dongDeskBuild = true;
        private bool dongDeskPlaces;
        private bool dongDeskCommon;
        private const float DeskListWidth = 298f;         // 목록 칸의 너비 (창의 안쪽)
        private float dongDeskScroll;                     // 목록을 내린 만큼
        private float dongDeskContent;                    // 지난 그리기에서 잰 목록의 높이

        private bool DongDeskReady => TidyScreen && expansionView && !mapOnly && !showDesk && DongFlagsActive && RoamActive;
        // 왼쪽의 운영 현황 두 칸과 같은 자리다. 그 칸을 펴면 이 창은 닫힌다.
        private bool DongDeskVisible => dongDeskOpen && !sidePanelsOpen && DongDeskReady && BusinessDong() != null;
        private bool DongDeskCovers(Vector2 point) => DongDeskVisible && DongDeskPanel.Contains(point);

        // 빛의 차림표에서 사업으로 가는 줄의 이름. 동 관리 창이 있는 화면에서는 그 창으로 간다.
        private string BusinessDoorLabel(int business) => DongDeskReady ? "동 관리 창" : BusinessTitles[business] + " 사업 창";

        // 그 동을 고르고 창을 연다. fold로 펴 둘 묶음을 정할 수 있다.
        private void OpenDongDesk(string region, DeskFold fold = DeskFold.Keep)
        {
            if (!DongDeskReady) return;
            SelectDeskDong(region);
            dongDeskOpen = true;
            storeDeskPlace = null;           // 같은 자리의 매장 운영 창은 닫는다 (SagajeongDispatchStoreDesk.cs)
            sidePanelsOpen = false;
            if (fold != DeskFold.Keep) dongDeskScroll = 0f;
            if (fold == DeskFold.Hire) dongDeskHire = true;
            else if (fold == DeskFold.Build) dongDeskBuild = true;
            else if (fold == DeskFold.Places) dongDeskPlaces = true;
        }

        private void SelectDeskDong(string region)
        {
            if (string.IsNullOrEmpty(region) || region == BusinessDong()?.Entry.regionId) return;
            var module = DongModuleById(region);
            if (module == null || !module.Loaded) return;
            if (dongView == DongView.Station && !stationAnchors.ContainsKey(region)) return;      // 판 밖의 동으로는 지도를 옮기지 않는다
            FocusDongModule(region);
        }

        // ◀ ▶: 이웃 동으로 넘긴다. 역 판에서는 판 안의 동을, 그 밖에서는 읽은 동을 차례로.
        private void StepDeskDong(int step)
        {
            if (dongView != DongView.Station)
            {
                StepDongSolo(step);
                return;
            }
            var regions = stationAnchors.Keys.Where(key => DongModuleById(key)?.Loaded == true)
                .OrderBy(key => key, StringComparer.Ordinal).ToList();
            if (regions.Count < 2) return;
            var at = Mathf.Max(0, regions.IndexOf(stationCardRegion));
            관찰지도수동이동();
            SelectStationDong(regions[(at + step + regions.Count) % regions.Count]);
        }

        // 아래 사업 버튼은 이 창의 그 묶음을 펴는 지름길이기도 하다. (DrawExpansionRail)
        private void DongDeskShortcut(int business)
        {
            var dong = DongDeskReady ? BusinessDong() : null;
            if (dong == null) return;
            if (business == BusinessFood) OpenDongDesk(dong.Entry.regionId, DeskFold.Hire);
            else if (business == BusinessRestaurant) OpenDongDesk(dong.Entry.regionId, DeskFold.Build);
        }

        // 지도의 땅이나 건물을 두 번 눌렀을 때: 그 건물의 동(없으면 지금 고른 동)의 창을 연다. (DrawBoard)
        private void OpenDongDeskAtPick()
        {
            if (!DongDeskReady) return;
            var picked = selectedBuilding;
            var region = picked != null ? buildingShapes[picked.Value].Record.region : null;
            if (string.IsNullOrEmpty(region)) region = BusinessDong()?.Entry.regionId;
            if (string.IsNullOrEmpty(region)) return;
            // 첫 누름이 고른 건물은 거둔다. 내 곳(역할 건물 · 제휴 자리)이면 오른쪽 카드로 남긴다.
            if (picked != null && !roleByBuilding.ContainsKey(picked.Value) && !managedPartnerSites.Contains(picked.Value))
                selectedBuilding = null;
            OpenDongDesk(region);
        }

        // ── 명령: 전에 동 카드의 단추가 보내던 것 그대로 ──
        private void DeskRecruit(string region)
        {
            var land = DongLand(region);
            if (land != null) SendDong(BusinessIds.DongDemand, DongDemandActions.Recruit, land.Code);
        }

        private void DeskContract(string region)
        {
            var land = DongLand(region);
            if (land != null) SendDong(BusinessIds.DongDemand, DongDemandActions.Contract, land.Code);
        }

        private void DeskEnter(string region)
        {
            var land = DongLand(region);
            if (land != null) SendDong(BusinessIds.Territory, "enter", land.Code);
        }

        private void DeskEngine() => SendDong(BusinessIds.DongDemand, DongDemandActions.Engine, null);

        // 이 동의 내 곳: 매장, 역할 건물(카페 · 창고). 줄을 누르면 지도에서 그곳을 고른다.
        private List<(string Key, string Title, string Status, Color Colour, Color StatusColour)> DeskPlaces(string region)
        {
            var places = new List<(string Key, string Title, string Status, Color Colour, Color StatusColour)>();
            if (string.IsNullOrEmpty(region)) return places;
            if (state?.stores != null)
            {
                foreach (var store in state.stores)
                {
                    if (DongModuleAt(store.latitude, store.longitude)?.Entry.regionId != region) continue;
                    var waiting = state.orders != null && state.orders.Any(order => order.storeId == store.id && order.stage == "Waiting");
                    places.Add(("store:" + store.id, "매장 " + Letter(store.id) + " · 음식점", waiting ? "주문 대기" : "운영 중",
                        StoreHalo, waiting ? Gold : Mint));
                }
            }
            LoadRoleBook();
            foreach (var pair in roleByBuilding.OrderBy(item => item.Key))
            {
                var business = RoleBusiness(pair.Value);
                if (business == BusinessRestaurant || pair.Key < 0 || pair.Key >= buildingShapes.Length ||
                    buildingShapes[pair.Key].Record.region != region) continue;
                var status = RoleStatus(pair.Value);
                places.Add(("role:" + pair.Value.placeId, pair.Value.title + " · " + BusinessTitles[business], status,
                    BusinessColor(business), status == "우유 없음" ? SignalRed : Soft));
            }
            return places;
        }

        private void PickDeskPlace(string key)
        {
            if (key.StartsWith("store:", StringComparison.Ordinal)) FocusLight(FocusKind.Store, key.Substring(6));
            else if (key.StartsWith("role:", StringComparison.Ordinal)) GoToPlace(key.Substring(5));      // 내 매장이면 매장 운영 창
        }

        // ── 그리기 ──
        // 목록은 창 안의 한 칸(GUI.BeginGroup)에 그린다. 아래 셋은 그 칸 안의 좌표를 쓴다. 넘치면 휠로 내린다.
        // 묶음의 머리. 눌렀으면 true(접거나 편다).
        private bool DeskSection(ref float y, string title, bool open, string note)
        {
            var bar = new Rect(7, y, DeskListWidth - 14, 26);
            Paint(bar, Tile, 6);
            Text(new Rect(bar.x + 6, bar.y, 22, 26), open ? "▼" : "▶", 9, Gold, true, TextAnchor.MiddleCenter);
            Text(new Rect(bar.x + 30, bar.y, 170, 26), title, 13, Paper, true);
            if (!string.IsNullOrEmpty(note))
                Text(new Rect(bar.xMax - 112, bar.y, 104, 26), note, 10, Soft, align: TextAnchor.MiddleRight);
            y += 30;
            return Hit(bar);
        }

        // 한 줄: 색 점 · 이름 · 값 · 단추, 그 아래 설명. 단추를 눌렀으면 true. 못 하면 그 까닭을 붉게 적는다.
        private bool DeskRow(ref float y, Color colour, string title, string sub, string cost, string button, bool enabled, string block)
        {
            const float x = 13f;
            var right = DeskListWidth - 15f;
            Paint(new Rect(x + 2, y + 6, 12, 12), colour, 6);
            Text(new Rect(x + 22, y + 1, 150, 20), title, 13, Paper, true);
            var pressed = false;
            if (button != null)
            {
                var width = button.Length <= 3 ? 58f : 78f;
                var rect = new Rect(right - width, y, width, 22);
                Paint(rect, enabled ? Mint : Tile, 6);
                Text(rect, button, 11, enabled ? Canvas : Fade(Soft, 0.7f), true, TextAnchor.MiddleCenter);
                pressed = Hit(rect) && enabled;
                right -= width + 8;
            }
            if (cost != null) Text(new Rect(right - 84, y + 1, 84, 20), cost, 12, Gold, true, TextAnchor.MiddleRight);
            Text(new Rect(x + 22, y + 23, 256, 16), sub, 10, Soft);
            y += 42;
            if (!enabled && !string.IsNullOrEmpty(block))
            {
                Text(new Rect(x + 22, y - 3, 256, 14), block, 10, SignalRed);
                y += 14;
            }
            return pressed;
        }

        // 내 곳 한 줄. 눌렀으면 true.
        private bool DeskPlace(ref float y, Color colour, string title, string status, Color statusColour)
        {
            var bar = new Rect(11, y, DeskListWidth - 22, 24);
            Paint(bar, Panel, 6);
            Paint(new Rect(bar.x + 8, bar.y + 7, 10, 10), colour, 5);
            Text(new Rect(bar.x + 26, bar.y, 176, 24), title, 12, Paper, true);
            Text(new Rect(bar.xMax - 90, bar.y, 82, 24), status, 10, statusColour, true, TextAnchor.MiddleRight);
            y += 28;
            return Hit(bar);
        }

        // 화면 좌표(오른쪽 칸 · 건물 카드와 같은 층). OnGUI에서 부른다.
        private void DrawDongDesk()
        {
            if (dongDeskOpen && sidePanelsOpen) dongDeskOpen = false;      // 운영 현황 칸을 폈다. 같은 자리이므로 이 창은 닫는다
            if (!DongDeskVisible) return;
            var dong = BusinessDong();
            var region = dong.Entry.regionId;
            var land = DongLand(region);
            var trade = DongTrade(region);
            var panel = DongDeskPanel;
            var x = panel.x + 16;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);

            // 머리: 동 이름 · 단계 · 자금, 이웃 동으로, 닫기
            Text(new Rect(x, panel.y + 8, 170, 18), "동 관리 · " + (land == null ? "자료 없음" : DongStageName(land)), 11, Mint, true);
            var close = new Rect(panel.xMax - 34, panel.y + 5, 24, 24);
            var next = new Rect(panel.xMax - 60, panel.y + 7, 22, 20);
            var prev = new Rect(panel.xMax - 84, panel.y + 7, 22, 20);
            Text(close, "×", 18, Soft, align: TextAnchor.MiddleCenter);
            Text(prev, "◀", 10, Soft, true, TextAnchor.MiddleCenter);
            Text(next, "▶", 10, Soft, true, TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                dongDeskOpen = false;
                return;
            }
            if (Hit(prev))
            {
                StepDeskDong(-1);
                dongDeskScroll = 0f;
                return;
            }
            if (Hit(next))
            {
                StepDeskDong(1);
                dongDeskScroll = 0f;
                return;
            }
            Text(new Rect(x, panel.y + 28, 170, 26), dong.Entry.name, 19, Paper, true);
            if (dongDemand != null)
                Text(new Rect(panel.xMax - 146, panel.y + 31, 130, 20), "자금 " + dongDemand.Funds.ToString("N0") + "원", 11, Gold, true,
                    TextAnchor.MiddleRight);

            // 지표 네 칸 (도시의 산출에 해당). 값이 없으면 줄표.
            var flagged = land != null && land.Level >= TerritoryLevels.Entering;
            var live = flagged && trade != null;
            var cells = new[]
            {
                ("지금 주문", live ? trade.RateNow.ToString("0.0") + "건/시간" : "—"),
                ("받아냄", live ? Mathf.RoundToInt((float)(1 - trade.UnmetRatio) * 100f) + "%" : "—"),
                ("기사", live ? trade.Riders + "명" : "—"),
                ("제휴", live ? trade.Partners + "/" + trade.PartnerLimit : "—"),
            };
            var cellWidth = (panel.width - 32 - 18) / 4f;
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = new Rect(x + i * (cellWidth + 6), panel.y + 58, cellWidth, 34);
                Paint(cell, Panel, 6);
                Text(new Rect(cell.x, cell.y + 2, cell.width, 14), cells[i].Item1, 9, Soft, align: TextAnchor.MiddleCenter);
                Text(new Rect(cell.x, cell.y + 15, cell.width, 17), cells[i].Item2, 11, Paper, true, TextAnchor.MiddleCenter);
            }

            // 목록 칸. 넘치면 휠로 내린다. 지도는 패널 위의 휠을 받지 않는다(HandleMapWheel).
            var body = new Rect(panel.x + 1, panel.y + 100, DeskListWidth, panel.height - 100 - 30);
            var overflow = Mathf.Max(0f, dongDeskContent - body.height);
            var wheel = Event.current;
            if (wheel.type == EventType.ScrollWheel && body.Contains(wheel.mousePosition))
            {
                dongDeskScroll += wheel.delta.y * 16f;
                wheel.Use();
            }
            dongDeskScroll = Mathf.Clamp(dongDeskScroll, 0f, overflow);

            // 칸 안에서는 누른 것을 적어만 두고, 칸을 닫은 뒤에 한 번 실행한다.
            Action run = null;
            GUI.BeginGroup(body);
            var y = -dongDeskScroll;
            if (land == null)
            {
                Text(new Rect(15, y, DeskListWidth - 30, 34), "이 동의 영역 자료가 없습니다.", 11, Soft, wrap: true);
                y += 34;
            }
            else if (!flagged)
            {
                // 아직 깃발을 꽂지 않은 동: 넓히기 한 줄
                DeskSection(ref y, "넓히기", true, "이웃 동으로");
                if (DeskRow(ref y, Gold, "깃발 꽂기", land.Candidate ? "맞닿은 동 · 꽂으면 주문이 들어오기 시작한다" : "아직 닿지 않는 동",
                        null, "꽂기", land.CanEnter, land.CanEnter ? null : land.EnterBlock))
                    run = () => DeskEnter(region);
            }
            else
            {
                if (DeskSection(ref y, "고용", dongDeskHire, "사람을 들인다")) run = () => dongDeskHire = !dongDeskHire;
                if (dongDeskHire && trade != null && dongDemand != null)
                {
                    var 모집줄 = y;
                    if (DeskRow(ref y, FireflyHalo, "기사", "지금 " + trade.Riders + "명 · 받아낼 수 있는 주문이 늘어난다",
                            dongDemand.RecruitCost.ToString("N0"), 동확정말("고용", 다음할일Policy.모집키(land.Code)), trade.CanRecruit, trade.RecruitBlock))
                        run = 턴계획모드 ? (Action)(() => 동계획담기(region, false)) : () => DeskRecruit(region);
                    할일강조그리기(new Rect(11, 모집줄 - 3, DeskListWidth - 22, y - 모집줄), 다음할일Policy.모집키(land.Code));
                }

                if (DeskSection(ref y, "건설", dongDeskBuild, "거점을 늘린다")) run = () => dongDeskBuild = !dongDeskBuild;
                if (dongDeskBuild && trade != null && dongDemand != null)
                {
                    var 제휴줄 = y;
                    if (DeskRow(ref y, StoreHalo, "제휴 음식점", trade.Partners + "/" + trade.PartnerLimit + "곳 · 가상 자리로 밝혀진다",
                            dongDemand.ContractCost.ToString("N0"), 동확정말("건설", 다음할일Policy.제휴키(land.Code)), trade.CanContract, trade.ContractBlock))
                        run = 턴계획모드 ? (Action)(() => 동계획담기(region, true)) : () => DeskContract(region);
                    할일강조그리기(new Rect(11, 제휴줄 - 3, DeskListWidth - 22, y - 제휴줄), 다음할일Policy.제휴키(land.Code));
                    // 자리 고르기는 다음 걸음이다. 지금은 줄만 세워 둔다.
                    DeskRow(ref y, Fade(StoreHalo, 0.45f), "음식점 차리기", "지도에서 자리 고르기 · 아직 준비 중입니다", null, "준비 중", false, null);
                }

                var places = DeskPlaces(region);
                var sites = managedPartnerSites.Count(site => site >= 0 && site < buildingShapes.Length &&
                    buildingShapes[site].Record.region == region);
                if (DeskSection(ref y, "이 동의 내 곳", dongDeskPlaces, (places.Count + sites) + "곳")) run = () => dongDeskPlaces = !dongDeskPlaces;
                if (dongDeskPlaces)
                {
                    foreach (var place in places)
                    {
                        if (!DeskPlace(ref y, place.Colour, place.Title, place.Status, place.StatusColour)) continue;
                        var key = place.Key;
                        run = () => PickDeskPlace(key);
                    }
                    if (sites > 0)
                    {
                        Text(new Rect(19, y, DeskListWidth - 38, 16), "제휴 음식점의 가상 자리 " + sites + "곳 (지도의 주황빛)", 10, Soft);
                        y += 20;
                    }
                    else if (places.Count == 0)
                    {
                        Text(new Rect(19, y, DeskListWidth - 38, 16), "아직 이 동에 내 곳이 없습니다.", 10, Soft);
                        y += 20;
                    }
                }

                if (dongDemand != null)
                {
                    if (DeskSection(ref y, "공통 · 배차 엔진 Lv" + dongDemand.EngineLevel, dongDeskCommon, "모든 동"))
                        run = () => dongDeskCommon = !dongDeskCommon;
                    if (dongDeskCommon)
                    {
                        var highest = dongDemand.EngineLevel >= dongDemand.MaxEngineLevel;
                        if (DeskRow(ref y, Gold, highest ? "배차 엔진 Lv" + dongDemand.EngineLevel : "배차 엔진 Lv" + (dongDemand.EngineLevel + 1),
                                highest ? "가장 높은 단계입니다" : "모든 동에 함께 적용된다", highest ? null : dongDemand.EngineCost.ToString("N0"),
                                highest ? null : "올리기", highest || dongDemand.CanUpgrade, highest ? null : dongDemand.UpgradeBlock))
                            run = DeskEngine;
                    }
                }
            }
            dongDeskContent = y + dongDeskScroll + 4f;
            GUI.EndGroup();

            // 넘칠 때만: 오른쪽의 가는 막대가 어디쯤인지 보인다.
            if (overflow > 0f)
            {
                var thumb = Mathf.Max(24f, body.height * body.height / dongDeskContent);
                Paint(new Rect(panel.xMax - 6, body.y + (body.height - thumb) * dongDeskScroll / overflow, 3, thumb), Fade(Soft, 0.55f), 2);
            }

            // 맨 아래: 방금 한 일 → 이 동의 신호 → 안내
            var recent = Time.unscaledTime - dongNoteAt < 4f && !string.IsNullOrEmpty(dongNote);
            var signal = land?.Signal;
            var line = recent ? dongNote : signal != null ? signal.Message :
                overflow > 0f ? "휠로 목록을 내립니다 · 묶음의 머리를 누르면 접힙니다" : "한 번 누르면 고르기 · 두 번 누르면 이 창";
            Text(new Rect(x, panel.yMax - 26, panel.width - 32, 20), line, 10,
                recent ? (dongNoteOk ? Mint : SignalRed) : signal != null ? SignalColor(signal) : Fade(Soft, 0.8f));
            run?.Invoke();
        }

        // 방향 촬영: 창을 연 모습, 묶음을 모두 편 모습과 끝까지 내린 모습, 창의 「건설」이 지금의 제휴 명령을 그대로 보내는지, 창을 닫은 뒤의 동 카드.
        // 여는 것 · 접고 펴는 것 · 내리는 것 · 동을 넘기는 것은 시계와 원장을 바꾸지 않아야 한다. 제휴는 하나 늘고 그 값만큼만 자금이 줄어야 한다.
        private IEnumerator ViewProofDongDesk(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            expansionPanelOpen = false;
            lightMenu = null;
            dongNoteAt = -100f;              // 앞 촬영이 남긴 「방금 한 일」은 걷는다
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            dongDeskHire = true;
            dongDeskBuild = true;
            dongDeskPlaces = false;
            dongDeskCommon = false;
            dongDeskScroll = 0f;
            OpenDongDesk(BusinessDong()?.Entry.regionId);
            yield return null;
            yield return null;
            receipt.deskOpen = DongDeskVisible;
            receipt.deskDong = BusinessDong()?.Entry.name ?? string.Empty;
            yield return SaveFrame(directory, "42-dong-desk.png");

            // 묶음을 모두 편다. 목록이 창보다 길어진다.
            dongDeskPlaces = true;
            dongDeskCommon = true;
            yield return null;
            yield return null;
            var region = BusinessDong()?.Entry.regionId;
            receipt.deskPlaces = DeskPlaces(region).Count;
            yield return SaveFrame(directory, "43-dong-desk-places.png");
            dongDeskScroll = 10000f;          // 끝까지 내린다 (그리기에서 끝에 맞춰진다)
            yield return null;
            yield return null;
            receipt.deskScrolled = dongDeskScroll;
            yield return SaveFrame(directory, "43b-dong-desk-scrolled.png");
            dongDeskScroll = 0f;

            // ◀ ▶로 제휴를 더 맺을 수 있는 동을 찾는다(없으면 지금 동 그대로).
            for (var step = 0; step < 8 && DongTrade(region)?.CanContract != true; step++)
            {
                StepDeskDong(1);
                yield return null;
                region = BusinessDong()?.Entry.regionId;
                receipt.deskStepped++;
            }
            receipt.deskUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;

            var trade = DongTrade(region);
            receipt.deskContractTried = trade != null && trade.CanContract && dongDemand != null;
            var fundsBefore = dongDemand?.Funds ?? 0;
            var partnersBefore = trade?.Partners ?? 0;
            receipt.deskContractCost = receipt.deskContractTried ? dongDemand.ContractCost : 0;
            if (receipt.deskContractTried) DeskContract(region);
            dongDeskPlaces = false;
            dongDeskCommon = false;
            yield return null;
            yield return null;
            receipt.deskContractDong = BusinessDong()?.Entry.name ?? string.Empty;
            receipt.deskContractAdded = (DongTrade(region)?.Partners ?? 0) - partnersBefore;
            receipt.deskContractSpent = fundsBefore - (dongDemand?.Funds ?? 0);
            yield return SaveFrame(directory, "44-dong-desk-after-build.png");

            dongDeskOpen = false;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "45-dong-card-door.png");
            holdProofClock = false;
        }
    }
}
