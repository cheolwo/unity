using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect ExpansionHeader = new(0, 0, Width, 84);
        // 0단계 뼈대 S5: 아래 띠는 사업 탭 여섯 개다(SagajeongDispatchBusiness.cs의 BusinessTitles). 지도를 덜 가리도록 얇게 했다.
        private static readonly Rect ExpansionRail = new(22, 660, 1236, 44);
        private static readonly Rect ExpansionOverview = new(22, 196, 188, 184);
        private static readonly Rect ExpansionPanel = new(986, 106, 272, 430);
        private static readonly Rect ExpansionDongButton = new(22, 626, 80, 26);
        private static readonly Rect ExpansionZoneButton = new(108, 626, 86, 26);

        private bool expansionView = true;
        private bool expansionPanelOpen = !TidyScreen;        // 정리한 첫 화면에서는 사업 칸을 접어 둔다(아래 띠를 누르면 열린다)
        private int expansionSelection = BusinessFranchise;   // 처음에는 새로 열린 사업을 보여 준다

        private bool ExpansionPanelVisible => expansionPanelOpen && actorFocus == null && selectedBuilding == null &&
            !dongPanelOpen && !zonePanelOpen && !showDesk && !CycleCardVisible;

        private bool ExpansionCovers(Vector2 point)
        {
            if (!expansionView || mapOnly) return false;
            if (TidyScreen)
            {
                // 정리한 첫 화면: 옮기거나 접은 것은 지도를 가리지 않는다 (SagajeongDispatchSettings.cs)
                if (ExpansionHeader.Contains(point) || BusinessCovers(point) || SignalBarCovers(point) || CycleCovers(point) || NoticeCovers(point)) return true;
                if (!showDesk && (ExpansionRail.Contains(point) || SideCovers(point))) return true;
                return (DailyReportVisible || TerritoryPanelVisible || ExpansionPanelVisible) && ExpansionPanel.Contains(point);
            }
            if (ExpansionHeader.Contains(point) || BusinessCovers(point) || SignalBarCovers(point) || CycleCovers(point) ||
                DongViewPanelCovers(point)) return true;
            if (!showDesk && (ExpansionRail.Contains(point) || ExpansionOverview.Contains(point) ||
                ExpansionDongButton.Contains(point) || ExpansionZoneButton.Contains(point) ||
                ExpansionDioramaButton.Contains(point) || MapEffectsControl.Contains(point) ||
                DioramaVisible && ExpansionDioramaFacts.Contains(point))) return true;
            return (TerritoryPanelVisible || ExpansionPanelVisible) && ExpansionPanel.Contains(point);
        }

        private void SelectExpansion(int index)
        {
            expansionSelection = Mathf.Clamp(index, 0, BusinessTitles.Length - 1);
            expansionPanelOpen = true;
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
        }

        private void DrawExpansionView()
        {
            DrawExpansionHeader();
            if (dailyReportOpen && !DailyReportVisible) dailyReportOpen = false;     // 설정 칸과 같다
            if (!showDesk)
            {
                // 전국 지도(r155)에서는 왼쪽이 자료 층·지표·표시로 바뀐다.
                if (NationMapVisible) DrawNationSide();
                else if (SidePanelsVisible)
                {
                    DrawExpansionOverview();
                    DrawBusinessLayers();
                }
                // 정리한 첫 화면: 도구 줄(지도 효과·동 보기·법정동·배달권·2.5D)은 설정 칸으로 갔다. 왼쪽 아래에는 운영 현황 단추만 남는다.
                if (TidyScreen)
                {
                    DrawSideToggle();
                    DrawDailyReportButton();        // r174 「오늘 결산」 (SagajeongDispatchDailyReport.cs)
                    DrawRoundCard();                // r187 운영 회차 안내 (SagajeongDispatchRound.cs)
                }
                else DrawExpansionTools();
                DrawExpansionRail();
                DrawChainStrip();
                DrawSignalBar();
            }
            // 구 지도에서는 오른쪽 패널이 영역 패널로, 전국 지도에서는 거점·출입구 카드로 바뀐다. 설정은 가운데의 큰 화면이다(OnGUI의 끝).
            if (DailyReportVisible) DrawDailyReportPanel();
            else if (NationPanelVisible) DrawNationPanel();
            else if (TerritoryPanelVisible) DrawTerritoryPanel();
            else if (ExpansionPanelVisible) DrawExpansionPanel();
        }

        private void DrawExpansionHeader()
        {
            Paint(ExpansionHeader, Canvas);
            Text(new Rect(22, 12, 300, 30), "홍달 경영 시뮬레이션", 24, Paper, true);
            // 정리한 첫 화면에서는 도구 줄의 「내부 검토용」 표시가 여기로 온다(행정동 모듈 자료를 쓰는 동안 늘 보인다).
            Text(new Rect(23, 47, 295, 23), TidyScreen && DioramaVisible && DongModulesLoaded
                ? "가상 운영 · 사가정 배달권 · 내부 검토용" : "가상 운영 · 사가정 배달권", 12, Mint);
            Text(new Rect(327, 13, 135, 20), state?.campaign == null ? "첫 배차" :
                $"운영 {state.campaign.day}일차", 12, Soft);
            Text(new Rect(327, 34, 135, 32), ClockLabel(state?.now ?? "11:30:00"), 23, Paper, true);
            var headline = MindHeadline();
            Text(new Rect(484, 13, 145, 20), "경 유지 시간 · 오늘", 12, Soft);
            Text(new Rect(484, 36, 145, 26), headline.Maintained, 20, Paper, true);
            if (headline.Note != null)
                Text(new Rect(484, 62, 145, 16), headline.Note, 10, headline.Note.StartsWith("무념 깨짐") ? Red : Mint);
            Text(new Rect(651, 13, 161, 20), "경 시간당 순소득", 12, Soft);
            Text(new Rect(651, 36, 161, 26), headline.PerHour, 20, Paper, true);
            Text(new Rect(651, 62, 161, 16), "가상 · 비용 미반영", 10, Soft);

            var canStart = state != null && pending == null && !running && !CampaignPolicyOpen;
            var restart = state?.settled == true && state.campaign == null;
            var startLabel = running ? "운영 중" : restart ? "같은 조건 시작" : "운영 시작";
            if (ExpansionButton(new Rect(831, 10, 126, 28), startLabel, canStart, Mint))
            {
                if (restart) RequestStart(true);
                else BeginRound();
            }
            if (ExpansionButton(new Rect(965, 10, 79, 28), showDesk ? "배차 닫기" : "배차 보기", true, Tile))
                ToggleDesk();
            if (TidyScreen)
            {
                // 기존 화면·지도만 보기는 설정 칸으로 갔다.
                if (ExpansionButton(SettingsButton, settingsOpen ? "설정 닫기" : "설정", true, Tile)) ToggleSettings();
                DrawAutopilotChip();            // 맡김 표시 (SagajeongDispatchAutopilot.cs)
            }
            else
            {
                if (ExpansionButton(new Rect(1052, 10, 102, 28), "기존 화면", true, Tile)) ShowLegacyView();
                if (ExpansionButton(new Rect(1162, 10, 96, 28), "지도만 보기", true, Tile))
                    ToggleMapOnly();
            }

            for (var index = 0; index < CampaignSpeeds.Length; index++)
            {
                var speed = CampaignSpeeds[index];
                var label = speed == 0 ? "정지" : speed + "배";
                var accent = campaignSpeed == speed ? Mint : Tile;
                if (ExpansionButton(new Rect(831 + index * 58, 45, 52, 26), label,
                        running && campaignMode && !CampaignPolicyOpen && !NationHoldsClock, accent))
                    SetCampaignSpeed(speed);
            }
            // 급한 신호가 있으면 이 자리가 "가장 급한 일"이 된다. 신호가 없으면 전국 지도에서는 자료 보기 중임을 알린다.
            var topSignals = BarSignals;     // 맡김이 켜져 있으면 맡김이 하는 일은 뺀 목록 (SagajeongDispatchNotices.cs)
            if (topSignals.Count == 0 && mapLevel == MapLevel.Nation)
                Text(new Rect(1071, 46, 187, 25), NationClockNote, 11, Gold, true, TextAnchor.MiddleRight);
            else if (topSignals.Count > 0 && !CampaignPolicyOpen)
                Text(new Rect(1071, 46, 187, 25), (topSignals[0].IsDanger ? "위험 · " : "주의 · ") + topSignals[0].Title +
                    (topSignals.Count > 1 ? $" 외 {topSignals.Count - 1}건" : string.Empty), 11, SignalColor(topSignals[0]), true, TextAnchor.MiddleRight);
            else
                Text(new Rect(1071, 46, 187, 25), CampaignPolicyOpen ? "다음 운영 방침 선택" :
                    state?.campaign?.phase == "closed" ? "오늘 영업 마감" :
                    state?.settled == true ? "현재 주문 정산 완료" : "기사와 매장을 연결하세요", 11, Soft,
                    align: TextAnchor.MiddleRight);
        }

        private bool ExpansionButton(Rect rect, string label, bool enabled, Color background)
        {
            Paint(rect, enabled ? background : Panel, 6);
            Text(rect, label, 11, enabled && background == Mint ? Canvas : Paper, true,
                TextAnchor.MiddleCenter);
            return enabled && Hit(rect);
        }

        private void DrawExpansionOverview()
        {
            Paint(ExpansionOverview, Panel, 10);
            Text(new Rect(35, 207, 163, 20), "지금의 배달 운영", 12, Mint, true);
            if (state == null)
            {
                Text(new Rect(35, 239, 162, 90), error ?? "도로와 회차를 불러옵니다.", 12, Soft, wrap: true);
                return;
            }
            Text(new Rect(35, 240, 162, 23), $"회차 기사  {state.couriers.Length}명", 15, Paper, true);
            Text(new Rect(35, 272, 162, 22), $"배차 대기  {state.orders.Count(order => order.stage == "Waiting")}건", 13, Soft);
            Text(new Rect(35, 300, 162, 22), $"수령·정산  {state.orders.Count(order => order.stage == "Settled")}건", 13, Soft);
            // 정리한 첫 화면에서는 「처음부터 새로 시작」이 설정 칸에 있다.
            var offerRestart = !TidyScreen && RestartOffered;
            Text(new Rect(35, 329, 162, offerRestart ? 20 : 44), error ?? notice ?? "시간과 이동은 같은 회차에서 이어집니다.",
                11, error == null ? Soft : Red, wrap: true);
            // 저장을 불러오면 이어하기가 기본이다. 새 회차를 원할 때만 누른다. 저장은 새 회차로 덮인다.
            if (offerRestart && ExpansionButton(new Rect(35, 351, 162, 23), "처음부터 새로 시작", true, Tile))
                RequestStart(true);
        }

        private void DrawExpansionTools()
        {
            DrawMapEffectsControl();     // r157: 지도 효과 간단히/효과 사용
            if (mapLevel == MapLevel.Nation) return;     // 전국 지도에는 동네 도구(법정동·배달권·2.5D)가 없다
            DrawDioramaToggle();
            DrawDongViewControl();       // 행정동 모듈: 붙여 보기 · 띄워 보기 · 이 동만, 동 칩
            if (ExpansionButton(ExpansionDongButton, "법정동", state != null, Tile)) OpenDongPanel(null);
            if (!ExpansionButton(ExpansionZoneButton, "배달권", state != null, Tile)) return;
            if (zonePanelOpen)
            {
                zonePanelOpen = false;
                return;
            }
            LoadLegalDongs();
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = true;
            LoadZoneBook();
        }

        private void DrawExpansionRail()
        {
            Paint(ExpansionRail, Canvas, 10);
            var slot = 0;
            for (var index = 0; index < BusinessTitles.Length; index++)
            {
                var label = BusinessState(index, out var accent);
                var open = accent != Soft;
                var selected = expansionPanelOpen && expansionSelection == index;
                // 정리한 첫 화면: 아직 열리지 않은 사업은 띠에 두지 않는다. 열리면 그때 자리가 생긴다.
                if (TidyScreen && !open && !selected) continue;
                var tab = new Rect(ExpansionRail.x + 8 + (TidyScreen ? slot : index) * 166, ExpansionRail.y + 6, 160, 32);
                slot++;
                if (open || selected)
                {
                    Paint(tab, selected ? Paper : accent, 7);
                    Paint(new Rect(tab.x + 1, tab.y + 1, tab.width - 2, tab.height - 2),
                        accent == Mint ? Ink(0x18372F) : accent == Gold ? Ink(0x302F27) : Panel, 6);
                }
                Text(new Rect(tab.x + 12, tab.y, 86, tab.height), BusinessTitles[index], 13, open ? Paper : Soft, true);
                Text(new Rect(tab.x + 66, tab.y, 84, tab.height), label, 10, accent, align: TextAnchor.MiddleRight);
                if (Hit(tab))
                {
                    SelectExpansion(index);
                    DongDeskShortcut(index);      // 기사 운영 · 음식점은 동 관리 창의 그 묶음도 편다 (SagajeongDispatchDongDesk.cs)
                }
            }
            Text(new Rect(ExpansionRail.xMax - 226, ExpansionRail.y, 214, ExpansionRail.height),
                mapLevel == MapLevel.Dong ? "더 축소하면 구 지도 · 영역" : GuMapVisible ? "휠 확대·축소 · 끌어서 이동" :
                NationMapVisible ? "거점·출입구를 눌러 자료 보기" :
                mapLevel == MapLevel.Seoul ? "휠을 올리면 구 지도 · 내리면 전국" : "휠을 올리면 한 단계 가까이",
                11, Soft, align: TextAnchor.MiddleRight);
            DrawPlaceButton();                  // 홍달 플레이스 주문자 뷰 열기 (SagajeongDispatchPlace.cs)
        }

        private void DrawExpansionPanel()
        {
            var x = ExpansionPanel.x + 17;
            var y = ExpansionPanel.y;
            var business = expansionSelection;
            var label = BusinessState(business, out var accent);
            Paint(ExpansionPanel, Rim, 11);
            Paint(new Rect(ExpansionPanel.x + 1, y + 1, ExpansionPanel.width - 2, ExpansionPanel.height - 2), Canvas, 10);
            Text(new Rect(x, y + 13, 208, 20), accent == Mint ? "지금 운영하는 사업" : accent == Gold ? "새로 열린 사업" : "다음에 열릴 사업",
                11, accent == Soft ? Soft : Gold, true);
            Text(new Rect(x, y + 39, 234, 30), BusinessTitles[business], 23, Paper, true);
            var panelDong = BusinessDong();      // r198: 사업 창은 고른 동을 늘 말한다 (SagajeongDispatchBusinessWindow.cs)
            if (panelDong != null)
                Text(new Rect(x + 120, y + 44, 116, 22), panelDong.Entry.name, 12, Mint, true, TextAnchor.MiddleRight);
            var close = new Rect(ExpansionPanel.xMax - 35, y + 11, 23, 24);
            Text(close, "×", 19, Soft, align: TextAnchor.MiddleCenter);
            if (Hit(close)) expansionPanelOpen = false;
            Text(new Rect(x, y + 83, 237, 25), BusinessRoles[business], 12, BusinessColor(business));

            Text(new Rect(x, y + 118, 237, 84), BusinessDescriptions[business], 13, Paper, wrap: true);
            Paint(new Rect(x, y + 213, 238, 83), Tile, 7);
            if (DrawBusinessDongStatus(business, panelDong, x, y))
            {
                // 고른 동의 그 사업 현황을 그렸다 (r198)
            }
            else if (business <= BusinessRestaurant)
            {
                var flow = MindFlow();
                Text(new Rect(x + 11, y + 220, 216, 18), "오늘 생산·소비 흐름", 11, Gold, true);
                Text(new Rect(x + 11, y + 241, 216, 22),
                    $"주문 {flow.Orders} · 준비 {flow.Prepare} · 배송 {flow.Deliver} → 완료 {flow.Limit}", 13, Paper, true);
                Text(new Rect(x + 11, y + 264, 216, 30), flow.Focus, 10,
                    flow.Limit < flow.Orders ? Gold : Soft, wrap: true);
            }
            else if (engine?.Pack == null || business == BusinessProducer)
            {
                Text(new Rect(x + 11, y + 224, 216, 54), business == BusinessProducer
                    ? "지역의 수요와 공급을 살펴보며 다음에 넓힐 책임을 미리 확인합니다."
                    : "이 사업의 자료 팩이 실행판에 없습니다.", 12, Soft, wrap: true);
            }
            else if (business == BusinessCenter)
            {
                var center = ModuleSnapshot<Ssalddel.WindowsGame.Kernel.LogisticsCenterSnapshot>(Ssalddel.WindowsGame.Kernel.BusinessIds.LogisticsCenter);
                Text(new Rect(x + 11, y + 220, 216, 18), center?.CenterName ?? "물류센터", 11, Gold, true);
                Text(new Rect(x + 11, y + 241, 216, 22), center == null ? "—" :
                    $"가용 {center.Available:0} · 예약 {center.Reserved:0} · 출고 {center.Shipped:0} ({center.Unit})", 13, Paper, true);
                Text(new Rect(x + 11, y + 264, 216, 30), "첫 사슬에서는 로트와 차량이 하나씩이라 스스로 출고합니다.", 10, Soft, wrap: true);
            }
            else
            {
                var current = chain.Steps.FirstOrDefault(step => step.State == Ssalddel.WindowsGame.Kernel.ChainStepStates.Now);
                Text(new Rect(x + 11, y + 220, 216, 18), business == BusinessFranchise ? "첫 인계 사슬" : "지금 다니는 화물", 11, Gold, true);
                Text(new Rect(x + 11, y + 241, 216, 22), business == BusinessCargo
                    ? (chain.TruckMoving ? $"{chain.TruckName} · {chain.TruckLoad}" : chain.WaitingInspection ? $"{chain.TruckName} · 하차" : "다니는 화물 없음")
                    : !chain.Started ? "시작 전 · 보충 발주 필요" : current == null ? "끝까지 이어짐" : current.Title + " · " + current.Line, 13, Paper, true);
                Text(new Rect(x + 11, y + 264, 216, 30), chain.Headline, 10, string.IsNullOrEmpty(chain.ActionKey) ? Soft : Gold, wrap: true);
            }

            var canAct = state != null && business switch
            {
                BusinessFood => true,
                BusinessRestaurant => state.stores.Length > 0,
                BusinessFranchise => engine?.Pack != null,
                BusinessCargo => engine?.Pack != null,
                _ => false,
            };
            var action = business switch
            {
                BusinessFood => "배차 데스크 보기",
                BusinessRestaurant => "매장 둘러보기",
                BusinessFranchise => "카페 1호점 보기",
                BusinessCargo => "도심 창고 보기",
                BusinessCenter => "수도권 지도에서 열립니다",
                _ => "후속 운영 영역",
            };
            if (ExpansionButton(new Rect(x, y + 334, 238, 36), action, canAct, Mint))
            {
                if (business == BusinessFood) ToggleDesk();
                else if (business == BusinessRestaurant)
                {
                    showDetails = true;
                    actorFocus = (FocusKind.Store, state.stores[0].id);
                }
                else if (business == BusinessFranchise) GoToPlace(engine.Pack.network.store.placeId);
                else SelectPlace("place:urban-warehouse-1");
            }
            Text(new Rect(x, y + 385, 238, 30), business <= BusinessCargo ?
                "색이 있는 건물이 내가 운영하는 곳입니다" : "더 넓은 지도에서 열리는 사업", 10, Soft,
                align: TextAnchor.MiddleCenter, wrap: true);
        }
    }
}
