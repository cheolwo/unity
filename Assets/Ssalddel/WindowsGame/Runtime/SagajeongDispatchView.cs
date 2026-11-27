using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView : MonoBehaviour
    {
        private const int Width = 1280;
        private const int Height = 720;
        private static readonly Rect TimePanel = new(18, 8, 1244, 104);
        private static readonly Rect DeskPanel = new(28, 119, 293, 548);
        private Rect AttributionPanel => WidenAttribution(expansionView && !mapOnly && !showDesk
            ? new Rect(actorFocus == null ? 979 : 674, 아래줄걷음 && actorFocus == null ? 666 : 630, 281, 24) : new Rect(979, 677, 281, 28));

        // 구 지도는 출처가 길다(행정동 경계 출처를 더했다, 2026-11-20). 오른쪽 끝을 두고 왼쪽으로 넓힌다.
        // 수도권 바탕(2026-11-25)이 들어오면 출처가 더 길다 — 「영역 전체 보기」(x 720까지)에 닿지 않을 만큼만 더 넓힌다.
        private Rect WidenAttribution(Rect panel) => GuMapVisible ? new Rect(panel.x - (CapitalReady ? 255 : 200), panel.y, panel.width + (CapitalReady ? 255 : 200), panel.height) : panel;
        private static readonly Rect SessionNoticePanel = new(28, 677, 362, 28);
        private const long DeadlineMs = 360000;
        private const int ClockMultiplier = 12;
        private static readonly Color Canvas = Ink(0x101A25);
        private static readonly Color Panel = Ink(0x192735);
        private static readonly Color Tile = Ink(0x223545);
        private static readonly Color Rim = Ink(0x304758);
        private static readonly Color Paper = Ink(0xECF5F8);
        private static readonly Color Soft = Ink(0xABC1CB);
        private static readonly Color Mint = Ink(0x6EE7CC);
        private static readonly Color Gold = Ink(0xFFD395);
        private static readonly Color Lilac = Ink(0xC6ACFF);
        private static readonly Color Red = Ink(0xFFB0A6);

        private SagajeongDispatchEngine engine;
        private Task<DispatchReply> pending;
        private DispatchRequest queuedAssignment;
        private DispatchState state;
        private string pendingAction;
        private string selectedDriver;
        private string notice;
        private bool restoredSession;   // 저장을 불러온 뒤 아직 이어가지 않았다. 이때만 "처음부터 새로 시작"을 보인다.
        private string error;
        private double realStart;
        private double nextTick;
        private bool running;
        private bool startAfterLoad;
        private bool holdProofClock;
        private Font font;
        private Font boldFont;
        private readonly Dictionary<(int, bool, TextAnchor, bool), GUIStyle> styles = new();
        private Texture2D roadTexture;
        private double centerLongitude;
        private double centerLatitude;
        private double longitudeScale;
        private double metresPerPixel;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            LoadMapEffects();
            LoadNotices();                   // 기본 알림값을 읽은 다음 모드별 정도를 적용한다
            LoadAutopilot();                 // 켜 두기 성장 · 맡김 (SagajeongDispatchAutopilot.cs)
            LoadPackage();                   // 패키지(배달 플랫폼 뼈대) (SagajeongDispatchPlatformPackage.cs)
            LoadStance();                    // 입장 고르기: 켤 때마다 시작 화면에 입장 카드 (SagajeongDispatchStance.cs)
            engine = new SagajeongDispatchEngine(Application.dataPath);
            engine.RoundText = () => operatingRound.ToText();      // 카1: 운영 회차를 세션 저장에 함께
            const string campaignProofFlag = "--sagajeong-campaign-proof=";
            const string expansionProofFlag = "--sagajeong-expansion-proof=";
            const string motorcycleProofFlag = "--sagajeong-motorcycle-proof=";
            const string proofFlag = "--sagajeong-proof=";
            var args = Environment.GetCommandLineArgs();
            var motorcycleProof = args.FirstOrDefault(item => item.StartsWith(motorcycleProofFlag, StringComparison.Ordinal));
            var standardProof = args.FirstOrDefault(item => item.StartsWith(proofFlag, StringComparison.Ordinal));
            var campaignProof = args.FirstOrDefault(item => item.StartsWith(campaignProofFlag, StringComparison.Ordinal));
            var expansionProof = args.FirstOrDefault(item => item.StartsWith(expansionProofFlag, StringComparison.Ordinal));
            campaignMode = motorcycleProof == null && standardProof == null;
            RequestStart(false);
            if (expansionProof != null && campaignMode)
                StartCoroutine(CaptureExpansionProof(expansionProof.Substring(expansionProofFlag.Length)));
            else if (campaignProof != null && campaignMode)
                StartCoroutine(CaptureCampaignProof(campaignProof.Substring(campaignProofFlag.Length)));
            else if (motorcycleProof != null)
                StartCoroutine(CaptureProof(motorcycleProof.Substring(motorcycleProofFlag.Length), captureMotorcycle: true));
            else if (standardProof != null)
                StartCoroutine(CaptureProof(standardProof.Substring(proofFlag.Length)));
            else
                TryStartViewProof(args);      // --hongdal-view-proof=<폴더> (SagajeongDispatchViewProof.cs)
        }

        private void OnDestroy()
        {
            engine?.Dispose();
            ReleaseDiorama();
            ReleaseGu();
            ReleaseSignals();
            if (roadTexture != null) Destroy(roadTexture);
            if (font != null) Destroy(font);
            if (boldFont != null) Destroy(boldFont);
        }

        private void Update()
        {
            lightBeat = Mathf.Repeat(Time.unscaledTime, BeatWrap);
            StepStanceLoading();         // 입장 자료를 한 줄씩 읽는다 (SagajeongDispatchDongFocus.cs)
            StartBaseEditionOnGu();      // 기본 판은 구 지도에서 시작한다 (SagajeongDispatchMap.cs · 판Policy.cs)
            if (pending != null)
            {
                if (!pending.IsCompleted) return;
                var response = pending;
                var action = pendingAction;
                pending = null;
                if (response.IsFaulted)
                {
                    running = false;
                    error = response.Exception.GetBaseException().Message;
                    return;
                }

                var reply = response.Result;
                if (reply.state != null)
                {
                    var previousCampaign = state?.campaign;
                    state = reply.state;
                    var newRound = action is "start" or "campaign-start" || previousCampaign?.day != state.campaign?.day;
                    UpdateMotorcycleHeading(newRound);
                    if (state.campaign != null) SynchronizeCampaignClock(previousCampaign, action, reply.ok);
                    if (reply.roads is { Length: > 0 }) PrepareMap(reply.roads);
                    UpdateMind();
                    ObserveNewOrders(action);      // r154: 새 주문 표현. 상태는 읽기만 한다
                    ObserveNotices(action, previousCampaign);     // 떴다 사라지는 알림. 상태는 읽기만 한다 (SagajeongDispatchNotices.cs)
                    ObserveRecommendations();                     // 역할 휴대폰: 배차 엔진이 추천한 기사를 기억한다. 읽기만 (SagajeongDispatchRiderPhone.cs)
                }
                if (action is "start" or "campaign-start" && reply.ok && !string.IsNullOrEmpty(engine.RestoredNotice))
                {
                    notice = engine.RestoredNotice;
                    engine.RestoredNotice = null;
                    restoredSession = true;
                    var savedRound = 운영회차.FromText(engine.RestoredRound);   // 카1: 저장한 회차의 진행을 이어서
                    if (savedRound != null) operatingRound = savedRound;
                    engine.RestoredRound = null;
                }
                if (action is "start" or "campaign-start" && reply.ok && state != null && startAfterLoad)
                    BeginRound();
                else if (action is "assign" or "campaign-policy")
                {
                    notice = action == "assign" ? WithMindWarning(reply.message) : reply.message;
                    if (reply.ok) selectedDriver = null;
                }
                AutopilotReply(action, reply.ok, reply.message);     // 맡김이 보낸 것만 일지에 적는다
                if (reply.ok && reply.state != null) ObserveRound();
                if (!reply.ok && action is not ("assign" or "campaign-policy"))
                {
                    error = reply.message;
                    running = false;
                }
                if (!campaignMode && state?.settled == true)
                    running = false;
            }

            if (running && pending == null && queuedAssignment != null)
            {
                var assignment = queuedAssignment;
                queuedAssignment = null;
                if (AssignmentOpen) Send(assignment);
                return;
            }

            if (pending == null && SendCampaignJump()) return;
            if (pending == null && StepAutopilot()) return;     // 중에서 맡김이 한 가지씩 (SagajeongDispatchAutopilot.cs)

            var now = Time.realtimeSinceStartupAsDouble;
            if (!running || holdProofClock || pending != null || now < nextTick ||
                CampaignPolicyOpen || (campaignMode && campaignSpeed == 0 && !campaignPauseTick)) return;
            campaignPauseTick = false;
            nextTick = now + 0.10;
            Send(new DispatchRequest { action = "tick", elapsedMs = Elapsed(now) });
        }

        private void Send(DispatchRequest request)
        {
            pendingAction = request.action;
            pending = engine.Send(request);
        }

        private void RequestStart(bool thenPlay)
        {
            if (pending != null) return;
            관찰선택해제();
            running = false;
            queuedAssignment = null;
            selectedDriver = null;
            actorFocus = null;
            notice = null;
            error = null;
            startAfterLoad = thenPlay;
            operatingRound = new 운영회차();     // r187: 새 게임을 열면 1회차로 돌아간다
            ResetMind();
            ResetAutopilot();
            ResetCampaignClock();
            Send(new DispatchRequest { action = campaignMode ? "campaign-start" : "start" });
        }

        private void BeginRound()
        {
            // 캠페인은 정산 뒤에도 시간이 이어진다. 불러온 회차가 정산 상태여도 시계를 다시 걸 수 있어야 한다.
            if (state == null || pending != null || running || (state.settled && state.campaign == null) || error != null) return;
            realStart = Time.realtimeSinceStartupAsDouble;
            restoredSession = false;
            AnchorCampaignClock(state.elapsedMs, realStart);
            nextTick = realStart + 0.10;
            running = true;
            notice = null;
        }

        private long Elapsed(double now)
        {
            if (holdProofClock || CampaignPolicyOpen) return state.elapsedMs;
            if (campaignMode) return (long)CampaignElapsed(now);
            return Math.Max(state.elapsedMs, (long)((now - realStart) * 1000 * ClockMultiplier));
        }

        private void SelectCourier(string id)
        {
            dongPanelOpen = false;
            zonePanelOpen = false;
            if (autopilotOn && !showDesk) 관찰선택해제();
            actorFocus = (FocusKind.Courier, id);
            selectedDriver = CanSelect(id) && !AssignmentPending ? id : null;
            notice = null;
        }

        private bool CanSelect(string id) => AssignmentOpen && !state.orders.Any(order => order.driverId == id);
        private bool AssignmentPending => queuedAssignment != null || pendingAction == "assign" && pending != null;

        private void ConnectStore(string storeId)
        {
            주문배차연결(storeId, null);
        }

        private void 주문배차연결(string storeId, string orderId)
        {
            dongPanelOpen = false;
            zonePanelOpen = false;
            if (autopilotOn && !showDesk) 관찰선택해제();
            actorFocus = (FocusKind.Store, storeId);
            if (!AssignmentOpen || AssignmentPending) return;
            var order = state.orders.FirstOrDefault(item => item.storeId == storeId && item.stage == "Waiting" &&
                (orderId == null || item.id == orderId));
            if (order == null)
            {
                selectedDriver = null;
                notice = null;
                return;
            }
            if (string.IsNullOrEmpty(selectedDriver))
            {
                notice = "지도나 목록에서 기사를 먼저 선택하세요.";
                return;
            }
            if (!직접업무가능()) return;
            if (MindBlocksAssignment(selectedDriver, out var mindMessage))
            {
                notice = mindMessage;
                selectedDriver = null;
                return;
            }
            if (턴계획모드)
            {
                배차계획담기(order, selectedDriver);      // 화: 보내지 않고 이번 턴 계획에 담는다 (SagajeongDispatchNextTodo.cs)
                return;
            }
            var assignment = new DispatchRequest
            {
                action = "assign",
                orderId = order.id,
                driverId = selectedDriver,
                revision = order.revision,
                commandId = "unity-" + Guid.NewGuid().ToString("N"),
                elapsedMs = Elapsed(Time.realtimeSinceStartupAsDouble),
            };
            if (pending != null)
                queuedAssignment = assignment;
            else
                Send(assignment);
        }

        private void OnGUI()
        {
            if (font == null)
            {
                // 폰(Android · iOS)에는 맑은 고딕이 없어 글자가 하나도 안 보였다(2026-11-25 A34 실기) → 함께 싣는 나눔고딕(OFL, Resources/hongdal-font-korean.ttf).
                var phone = Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
                var bundled = phone ? Resources.Load<Font>("hongdal-font-korean") : null;
                font = bundled != null ? bundled : Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 18);
                boldFont = bundled != null ? bundled : Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 18);
            }
            NoteHands();                         // 손이 빈 시간 (SagajeongDispatchAutopilot.cs)
            if (creditsOpen)                     // 출처 · 라이선스 (SagajeongDispatchCredits.cs)
            {
                DrawCreditsScreen();
                return;
            }
            if (PublicDongOpen)                  // 공개판 동 디오라마: 지도 대신 동 하나 (SagajeongDispatchPublicDong.cs)
            {
                DrawPublicDongScreen();
                return;
            }
            if (PortraitScreen)                  // 세로 화면(폰): 입장으로 자료 보기 (SagajeongDispatchPortrait.cs)
            {
                DrawPortraitScreen();
                return;
            }
            if (!집중전환대기 && !패키지고르기표시 && !StancePickerVisible)
            {
                HandleSettingsKey();
                HandleShortcutKeys();
            }
            SyncSettingsClock();                 // 설정 화면이 떠 있는 동안 게임 시간을 멈춘다

            var scale = Mathf.Min(Screen.width / (float)Width, Screen.height / (float)Height);
            if (scale <= 0) return;
            var previous = GUI.matrix;
            var offset = new Vector3((Screen.width - Width * scale) * 0.5f, (Screen.height - Height * scale) * 0.5f, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
            if (집중전환입력처리() || 턴넘김입력처리())
            {
                GUI.matrix = previous;
                return;
            }
            ResizeMapViewport(scale);
            if (관찰띠표시) 관찰따라가기갱신();
            RefreshBusiness();
            RefreshSignals();
            var controlsEnabled = GUI.enabled;
            GUI.enabled = controlsEnabled && !CampaignPolicyOpen && !SettingsVisible && !DongPickerVisible && !패키지고르기표시 && !StancePickerVisible && !StanceLoadingVisible && !경영판보임;      // 설정 화면 · 행정동 이동 창이 떠 있는 동안 아래는 눌리지 않는다
            ReadDataControl();                  // 자료 창의 켜고 끔을 먼저 읽는다 (SagajeongDispatchDataControl.cs)
            SyncDataUnit();                     //   지금 단위(구 · 타일 · 동)의 묶음으로 (같은 파일)
            DrawDongDataControl(true);          // 동 디오라마의 자료 창: 누르기를 지도보다 먼저 받는다
            DrawMap();
            DrawDongFocusCardLate();                        // 디오라마 집중 카드: 지도 표시 위 · 판 아래 (SagajeongDispatchDongFocus.cs)
            DrawNoticeBreath();                             // 하루 마감 · 영업 시작의 숨 (SagajeongDispatchNotices.cs)
            if (!mapOnly)
            {
                if (expansionView)
                    DrawExpansionView();
                else if (showTime)
                {
                    Paint(TimePanel, Canvas, 10);
                    DrawHeader();
                }
                if (showDesk) DrawSidebar();
                if (!expansionView) DrawMapChrome();
                if (showDetails) DrawFocusPanel();
                DrawBuildingCard();
                DrawNavPanel();                     // 길찾기 띠와 단추 (SagajeongDispatchNavigation.cs)
                DrawDongDataControl(false);         // 동 디오라마의 자료 창 · 범례 줄: 그리기 (SagajeongDispatchDataControl.cs)
                DrawDongPanel();
                DrawZonePanel();
                DrawDongDesk();                     // 동 관리 창: 동을 두 번 누르면 왼쪽에 (SagajeongDispatchDongDesk.cs)
                DrawStoreDesk();                    // 매장 운영 창: 내 매장을 누르면 같은 자리에 (SagajeongDispatchStoreDesk.cs)
                if (expansionView) DrawCycle();     // 결정 순회 뼈대: 오른쪽 아래 같은 자리의 버튼 (SagajeongDispatchCycle.cs)
                DrawMiniMap();                      // 요약 지도: 한 단계 위 지도에 지금 보는 곳 (SagajeongDispatchMiniMap.cs)
                DrawIconProofSheet();               // 방향 촬영에서만: 아이콘 한 벌 (SagajeongDispatchIcons.cs)
            }
            DrawLevelBanner();
            DrawMapContinuity();                            // 이어 보기 띠: 전국 › 수도권 › 구 › 동 · 축척 자 (SagajeongDispatchMapContinuity.cs)
            DrawLevelPrompt();
            if ((!expansionView || mapOnly) && !PlaceVisible) DrawPanelVisibility();      // 홍달 플레이스가 접은 동안에는 「패널 복원」을 두지 않는다
            GUI.enabled = controlsEnabled && !SettingsVisible && !DongPickerVisible && !패키지고르기표시 && !StancePickerVisible && !StanceLoadingVisible && !경영판보임;
            DrawNoticeStack();                              // 떴다 사라지는 알림: 위쪽 가운데 (SagajeongDispatchNotices.cs)
            DrawDayCloseCard();                             // 카1: 하루 마감 카드 (SagajeongDispatchDayClose.cs)
            DrawAutopilotReturn();                          // 「다녀오신 사이」 (SagajeongDispatchAutopilot.cs)
            DrawCampaignPolicy();
            DrawPlace();                                    // 홍달 플레이스 주문자 뷰 (SagajeongDispatchPlace.cs)
            if (!mapOnly && !expansionView)
            {
                Paint(SessionNoticePanel, Canvas, 6);
                Text(new Rect(38, 680, 348, 24), "가상 운영 · 실제 주문 및 결제와 연결되지 않습니다.", 12, Soft);
            }
            Paint(AttributionPanel, Canvas, 6);
            // 구 지도의 바탕은 토지피복(ESA)·차선(서울시)·법정구역 경계(브이월드)로 구웠다.
            Text(new Rect(AttributionPanel.x + 6, AttributionPanel.y + 3, AttributionPanel.width - 14, 21),
                GuMapVisible ? (CapitalReady ? "© ESA WorldCover CC BY 4.0 · Copernicus DEM · 국토부 · 서울시 · 브이월드 · SGIS · admdongkor" : "© ESA WorldCover CC BY 4.0 · 서울시 · 브이월드 · 행정동 통계청 SGIS · vuski/admdongkor") :
                NationMapVisible ? "경계 Natural Earth · 관세청 2025 · 내부 검토용" :
                DioramaVisible && DongModulesLoaded ? dongManifest.attribution : "© OpenStreetMap contributors · ODbL", 11, Soft,
                align: TextAnchor.MiddleRight);
            CreditsTapArea(AttributionPanel);      // 누르면 출처 · 라이선스 전체 (SagajeongDispatchCredits.cs)
            GUI.enabled = controlsEnabled;
            if (CampaignPolicyOpen && TidyScreen && expansionView && !mapOnly && !SettingsVisible && !DongPickerVisible && !PlaceVisible && !helpOpen && !집중전환대기)
                DrawAutopilotChip();
            Draw오행경영();                                 // 00 경영: 오행에서 위→아래로 (SagajeongDispatch오행경영.cs)
            DrawDongPicker();                               // M 행정동 이동 창, r236 (SagajeongDispatchDongPicker.cs)
            DrawShortcutHelp();                             // F1 도움말 (SagajeongDispatchShortcuts.cs)
            if (SettingsVisible) DrawSettingsScreen();
            DrawPackagePicker();                            // 패키지 고르기: 처음 한 번 · 아래 줄 「배달 플랫폼 ▾」 (SagajeongDispatchPlatformPackage.cs)
            DrawStancePicker();                             // 입장 고르기: 켤 때마다 · 자료 창 「입장」 단추 (SagajeongDispatchStance.cs)
            DrawStanceLoading();                            // 입장 → 자료 불러오기 창 · 들어간 뒤 띠 (SagajeongDispatchDongFocus.cs)
            if (집중전환대기) 집중전환창그리기();
            if (턴넘김확인) 턴넘김창그리기();                // 다음 할 일: 고르지 않은 일을 미루고 넘길지 (SagajeongDispatchNextTodo.cs)
            GUI.matrix = previous;
        }

        private void DrawHeader()
        {
            var campaign = state?.campaign;
            Text(new Rect(28, 16, 500, 22), campaign == null ? "음식 배달   /   STAGE 01" :
                $"음식 배달   /   사가정 운영 {campaign.day}일차", 12, Mint, true);
            Text(new Rect(28, 36, 675, 48), "홍달 경영 시뮬레이션", 34, Paper, true);
            var subtitle = campaign?.policy switch
            {
                "chariot" => "전차  ·  사가정과 장안의 주문을 함께 관리합니다.",
                "temperance" => "절제  ·  A매장 주문 한 건에 집중합니다.",
                "empress" => "여제  ·  A매장을 먼저 운영하고 2분 뒤 B매장 주문을 받습니다.",
                _ => campaign == null ? "사가정 첫 배차  ·  기사와 매장을 연결해 두 건을 정산하세요." :
                    "사가정 3일 운영  ·  배달을 마치고 다음 운영 방침을 정하세요."
            };
            Text(new Rect(30, 85, 663, 22), subtitle, 14, Soft);
            if (campaign != null)
            {
                DrawCampaignHeader();
                return;
            }
            var elapsed = state?.elapsedMs ?? 0;
            var remaining = Math.Max(0, (DeadlineMs - elapsed + 999) / 1000);
            Metric(new Rect(878, 32, 174, 66), "가상 시각", ClockLabel(state?.now ?? "11:30:00"), Paper);
            var deadlineMetric = new Rect(1064, 32, 188, 66);
            if (state?.settled == true && state.settlement != null)
                Metric(deadlineMetric, "제시간 배차", $"{state.settlement.onTimeCount} / {state.orders.Length}건",
                    state.settlement.cleared ? Mint : Gold);
            else
                Metric(deadlineMetric, "배차 마감까지", $"{remaining / 60:00}:{remaining % 60:00}", remaining > 0 ? Gold : Red);
        }

        private void Metric(Rect rect, string title, string value, Color accent)
        {
            Paint(rect, Panel, 10);
            Text(new Rect(rect.x + 14, rect.y + 6, rect.width - 28, 19), title, 12, Soft);
            Text(new Rect(rect.x + 14, rect.y + 25, rect.width - 28, 35), value, 24, accent, true);
        }

        private void DrawSidebar()
        {
            Paint(DeskPanel, Panel, 13);
            Text(new Rect(45, 134, 256, 28), "배차 데스크", 21, Paper, true);
            var subtitle = state == null ? "로컬 도로 표본 읽는 중" : state.campaign?.phase is "closed" or "policy"
                ? "영업 마감 · 남은 배송은 이어서 진행" : state.settled ? "현재 주문의 정산이 끝났습니다." :
                !running ? "두 매장 · 두 기사 · 가상 6분" :
                HasWaitingOrder() ? "기사 선택 → 대기 주문 연결" : "기사 이동 → 고객 수령 → 정산";
            Text(new Rect(46, 166, 253, 22), subtitle, 13, Soft);

            if (error != null)
            {
                Text(new Rect(46, 202, 252, 94), error, 15, Red, wrap: true);
                if (Action(new Rect(46, 315, 254, 44), "다시 연결", Mint)) RequestStart(false);
                return;
            }
            if (state == null) return;

            var expanded = state.couriers.Length > 2;
            for (var i = 0; i < state.couriers.Length; i++)
            {
                var courier = state.couriers[i];
                var color = RiderColor(courier.id);
                var rect = new Rect(45 + i % 2 * 131, 202 + i / 2 * 38, 121, expanded ? 32 : 45);
                var picked = IsFocused(FocusKind.Courier, courier.id) || selectedDriver == courier.id;
                Paint(rect, picked ? color : Tile, 8);
                Text(rect, CourierCaption(courier), expanded ? 13 : 15, picked ? Canvas : color, true, TextAnchor.MiddleCenter);
                if (Hit(rect)) SelectCourier(courier.id);
            }
            for (var i = 0; i < state.orders.Length; i++)
                DrawOrder(state.orders[i], new Rect(45, expanded ? 280 + i * 52 : 261 + i * 87, 258, expanded ? 47 : 77));

            if (state.campaign != null && (state.settled || state.campaign.phase != "open"))
            {
                DrawCampaignCloseout();
                return;
            }
            if (state.settled && state.settlement != null)
            {
                DrawSettlement(state.settlement);
                return;
            }
            if (!running)
            {
                Text(new Rect(47, 447, 248, 50), "6분 안에 배차하고 정산하면 주문마다 600원 보너스가 붙습니다.", 14, Soft, wrap: true);
                if (Action(new Rect(45, 520, 258, 48), campaignMode ? "운영 시작" : "스테이지 시작", Mint))
                    BeginRound();
                return;
            }

            Paint(new Rect(45, 443, 258, 206), Canvas, 10);
            Text(new Rect(56, 452, 235, 22), "최근 진행", 13, Mint, true);
            var events = state.events ?? Array.Empty<DispatchEvent>();
            for (var i = Math.Max(0, events.Length - 4); i < events.Length; i++)
            {
                var entry = events[i];
                var y = 479 + (i - Math.Max(0, events.Length - 4)) * 30;
                Text(new Rect(56, y, 56, 24), ClockLabel(entry.at), 11, Soft);
                Text(new Rect(117, y, 176, 24), $"{Letter(entry.orderId)} · {EventLabel(entry.code, entry.text)}", 12, Paper);
            }
            if (!string.IsNullOrEmpty(notice))
                Text(new Rect(56, 603, 234, 38), notice, 12, Gold, wrap: true);
            else
                Text(new Rect(56, 611, 235, 20), campaignMode && campaignSpeed == 0 ? "가상 시계 일시정지" :
                    "진행 속도 ×" + (ClockMultiplier * (campaignMode ? campaignSpeed : 1)), 11, Soft);
        }

        private void DrawOrder(DispatchOrder order, Rect rect)
        {
            var waiting = order.stage == "Waiting";
            Paint(rect, IsFocused(FocusKind.Store, order.storeId) ? Rim : Tile, 8);
            var compact = rect.height < 60;
            if (waiting && AssignmentOpen && selectedDriver != null)
                Paint(new Rect(rect.x, rect.y + 7, 3, rect.height - 14), Gold, 2);
            Text(new Rect(rect.x + 11, rect.y + (compact ? 2 : 6), 235, 25),
                $"{order.storeName}  ·  {order.menu}", compact ? 13 : 15, Paper, true);
            var label = waiting && state.campaign?.phase is "closed" or "policy" ? "마감 시 미배차" : StageLabel(order.stage);
            Text(new Rect(rect.x + 11, rect.y + (compact ? 25 : 35), compact ? 170 : 231, 20),
                label, compact ? 11 : 13, waiting ? Gold : Mint);
            var rider = !string.IsNullOrEmpty(order.driverId) ? "기사 " + Letter(order.driverId) :
                AssignmentOpen && !string.IsNullOrEmpty(order.recommendedDriverId) ? "추천 " + Letter(order.recommendedDriverId) : "";
            Text(new Rect(rect.x + 11, rect.y + (compact ? 27 : 54), 225, 16), rider, 11, Soft, align: TextAnchor.MiddleRight);
            할일주문표시(order, rect);                     // 다음 할 일: 고르는 중 · 계획에 담긴 주문 (SagajeongDispatchNextTodo.cs)
            if (Hit(rect)) 주문배차연결(order.storeId, order.id);
        }

        private void DrawSettlement(DispatchSettlement settlement)
        {
            Paint(new Rect(45, 442, 258, 168), Canvas, 9);
            Text(new Rect(56, 448, 235, 24), settlement.cleared ? "사가정 통과  ·  가상 정산" : "배차 마감 초과  ·  가상 정산",
                13, settlement.cleared ? Mint : Gold, true);
            MoneyRow(475, "플랫폼 수입", settlement.revenue, Soft);
            MoneyRow(496, "기사 보수", settlement.driverPay, Soft);
            MoneyRow(517, "기본 차액", settlement.baseBalance, Soft);
            MoneyRow(538, "빠른 배차 보너스", settlement.bonus, Gold);
            MoneyRow(567, "최종 차액", settlement.balance, Mint);
            if (Action(new Rect(45, 620, 258, 31), "같은 조건 다시 시작", Mint)) RequestStart(true);
        }

        private void MoneyRow(float y, string title, int value, Color color)
        {
            Text(new Rect(56, y, 135, 22), title, 12, color);
            Text(new Rect(185, y, 106, 22), $"{value:N0}원", 15, color, true, TextAnchor.MiddleRight);
        }

        private bool Action(Rect rect, string title, Color background)
        {
            Paint(rect, background, 8);
            Text(rect, title, 15, Canvas, true, TextAnchor.MiddleCenter);
            return Hit(rect);
        }

        private static bool Hit(Rect rect) => GUI.Button(rect, GUIContent.none, GUIStyle.none);

        private static void Paint(Rect rect, Color color, float radius = 0) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);

        private void Text(Rect rect, string content, int size, Color color, bool bold = false,
            TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var key = (size, bold, align, wrap);
            if (!styles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = bold ? boldFont : font,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = size,
                    alignment = align,
                    clipping = TextClipping.Clip,
                    wordWrap = wrap,
                    padding = new RectOffset(),
                };
                styles.Add(key, style);
            }
            style.normal.textColor = color;
            GUI.Label(rect, content, style);
        }

        private static Color Ink(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        private bool HasWaitingOrder() => state.orders.Any(order => order.stage == "Waiting");
        private static string Letter(string id) => string.IsNullOrEmpty(id) ? "?" : id.Substring(id.Length - 1).ToUpperInvariant();
        private static Color RiderColor(string id) => Letter(id) == "A" ? Mint : Lilac;
        private string CourierCaption(DispatchCourier courier) =>
            state.couriers.Length > 2 ? courier.label : "기사 " + Letter(courier.id);
        private static string ClockLabel(string clock)
        {
            if (string.IsNullOrEmpty(clock)) return "—";
            var separator = clock.IndexOf('T');
            var first = separator + 1;
            return clock.Length >= first + 8 ? clock.Substring(first, 8) : clock;
        }

        private static string StageLabel(string stage) => stage switch
        {
            "Waiting" => "기사 지정 대기",
            "Assigned" => "배차 지정",
            "Accepted" => "기사 수락",
            "ToPickup" => "매장으로 이동",
            "AtPickup" => "매장 도착 · 픽업 준비",
            "ToDropoff" => "픽업 완료 · 배달 중",
            "Delivered" => "전달 완료 · 수령 대기",
            "Received" => "고객 수령 확인",
            "Settled" => "가상 정산 완료",
            _ => stage ?? "대기",
        };

        private static string EventLabel(string code, string fallback) => code switch
        {
            "order_received" => "주문 접수",
            "assignment_designated" => "기사 지정",
            "driver_accepted" => "기사 수락",
            "driver_departed" => "매장으로 출발",
            "pickup_arrived" => "매장 도착",
            "food_picked_up" => "음식 픽업",
            "food_delivered" => "배송지 전달",
            "driver_pay_accrued" => "기사 보수 발생",
            "customer_received" => "고객 수령 확인",
            "virtual_settlement_recorded" => "가상 정산 기록",
            _ => fallback ?? "상태 변경",
        };
    }
}
