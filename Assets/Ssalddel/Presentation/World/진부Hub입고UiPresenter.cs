using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 진부Hub입고UiPresenter : MonoBehaviour
    {
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private FigmaMauiWarehouseUiThemeCatalog theme = null!;
        [SerializeField] private GameObject panelRoot = null!;
        [SerializeField] private Image panelSurface = null!;
        [SerializeField] private Image roleAccent = null!;
        [SerializeField] private Text contextText = null!;
        [SerializeField] private Image stateBadge = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text summaryText = null!;
        [SerializeField] private Text workflowText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text previewText = null!;
        [SerializeField] private Text boundaryText = null!;
        [SerializeField] private GameObject staleBanner = null!;
        [SerializeField] private Text staleText = null!;
        [SerializeField] private Button previewButton = null!;
        [SerializeField] private Button confirmButton = null!;
        [SerializeField] private Button tickButton = null!;
        [SerializeField] private Button refreshButton = null!;
        [SerializeField] private string selectionObjectStableId = string.Empty;

        private 진부Hub입고UiCoordinator coordinator = null!;
        private CancellationTokenSource lifetimeCancellation;
        private bool busy;
        private bool listenersBound;
        private bool contextVisible;

        public 진부Hub입고UiProjectionData CurrentProjection
            => coordinator?.CurrentProjection;
        public 진부Hub입고UiPreviewData CurrentPreview
            => coordinator?.CurrentPreview;
        public string CurrentPhaseCode
            => coordinator?.PhaseCode ?? 진부Hub입고UiCodes.Error;
        public bool ContextVisible => contextVisible;

        public void Configure(
            SimulationWorldShellPresenter worldShell,
            FigmaMauiWarehouseUiThemeCatalog themeCatalog,
            GameObject root,
            Image surface,
            Image accent,
            Text context,
            Image badge,
            Text state,
            Text title,
            Text summary,
            Text workflow,
            Text detail,
            Text preview,
            Text boundary,
            GameObject staleRoot,
            Text staleLabel,
            Button previewAction,
            Button confirmAction,
            Button tickAction,
            Button refreshAction,
            string selectedObjectStableId)
        {
            shell = worldShell;
            theme = themeCatalog;
            panelRoot = root;
            panelSurface = surface;
            roleAccent = accent;
            contextText = context;
            stateBadge = badge;
            stateText = state;
            titleText = title;
            summaryText = summary;
            workflowText = workflow;
            detailText = detail;
            previewText = preview;
            boundaryText = boundary;
            staleBanner = staleRoot;
            staleText = staleLabel;
            previewButton = previewAction;
            confirmButton = confirmAction;
            tickButton = tickAction;
            refreshButton = refreshAction;
            selectionObjectStableId = selectedObjectStableId;
        }

        private void Awake()
        {
            ValidateWiring();
            BindListeners();
            ApplyLoading();
        }

        private void OnEnable()
        {
            if (shell != null) shell.PresentationChanged += ApplyPresentation;
        }

        private void OnDisable()
        {
            if (shell != null) shell.PresentationChanged -= ApplyPresentation;
        }

        private void OnDestroy()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        public async Task InitializeAsync(
            I진부Hub입고UiAuthorityClient authority,
            string sessionStableId)
        {
            coordinator = new 진부Hub입고UiCoordinator(authority);
            RenewCancellation();
            ApplyLoading();
            await Run(async () => await coordinator.LoadAsync(
                sessionStableId, lifetimeCancellation.Token));
        }

        public async Task PreviewAsync()
            => await Run(async () => await coordinator.PreviewAsync(Token()));

        public async Task ConfirmAsync()
            => await Run(async () => await coordinator.ConfirmAsync(Token()));

        public async Task AdvanceAsync()
            => await Run(async () => await coordinator.AdvanceAsync(Token()));

        public async Task RefreshAsync()
            => await Run(async () => await coordinator.RefreshAsync(Token()));

        public async Task RunGoldenPathAsync()
        {
            await PreviewAsync();
            await ConfirmAsync();
            await AdvanceUntilReadyOrCompleted();
            if (CurrentProjection.StateCode == 진부Hub입고UiCodes.Ready)
            {
                await PreviewAsync();
                await ConfirmAsync();
                await AdvanceUntilReadyOrCompleted();
            }
        }

        public void ForceVisibleForTests(bool visible)
        {
            SetContextVisible(visible);
        }

        public void SetContextVisible(bool visible)
        {
            contextVisible = visible;
            ApplyPresentation();
        }

        public void ValidateWiring()
        {
            if (shell == null || theme == null || panelRoot == null || panelSurface == null
                || roleAccent == null || contextText == null || stateBadge == null
                || stateText == null || titleText == null || summaryText == null
                || workflowText == null || detailText == null || previewText == null
                || boundaryText == null || staleBanner == null || staleText == null
                || previewButton == null || confirmButton == null || tickButton == null
                || refreshButton == null || string.IsNullOrWhiteSpace(selectionObjectStableId))
                throw new InvalidOperationException("JinbuInboundUiWiringMissing");
        }

        private async Task AdvanceUntilReadyOrCompleted()
        {
            for (var index = 0; index < 8
                                && CurrentProjection.StateCode == 진부Hub입고UiCodes.InProgress;
                 index++)
                await AdvanceAsync();
        }

        private async Task Run(Func<Task> action)
        {
            if (busy || coordinator == null) return;
            busy = true;
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (
                lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                coordinator.MarkStale(exception);
                Debug.LogError("JinbuInboundUiFailed:" + exception.Message, this);
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        private void ApplyLoading()
        {
            if (panelRoot == null) return;
            contextText.text = "진부 HUB  ·  창고 입고  ·  SIMULATION";
            titleText.text = "입고 업무 상태를 불러오는 중";
            stateText.text = "불러오는 중";
            summaryText.text = "Simulation 서버의 상태 사본을 기다리고 있습니다.";
            workflowText.text = "입고예정  →  검수대기  →  적재대기  →  적재완료";
            detailText.text = string.Empty;
            previewText.text = string.Empty;
            boundaryText.text = "SIMULATION ONLY · 실제 입고·재고·운영 원장을 변경하지 않음";
            ApplyTheme("State.Loading", true);
            SetButtons(false, false, false, false);
            staleBanner.SetActive(false);
        }

        private void ApplyPresentation()
        {
            if (panelRoot == null || shell == null) return;
            var selected = shell.SelectedObjectStableId == selectionObjectStableId
                           || shell.SelectedDistrictStableId == selectionObjectStableId;
            panelRoot.SetActive(contextVisible || selected);
            if (!panelRoot.activeSelf || coordinator?.CurrentProjection == null) return;

            var projection = coordinator.CurrentProjection;
            var profileCompatible = theme.Supports(projection.DesignProfileRevision);
            contextText.text = "진부 HUB  ·  창고 입고  ·  " + projection.ExecutionModeCode
                + "  ·  r" + projection.StateRevision;
            titleText.text = projection.KoreanTitle;
            stateText.text = coordinator.IsStale ? "오래된 상태" : projection.StateKoreanLabel;
            summaryText.text = projection.Information("Summary");
            workflowText.text = Workflow(projection.WorkflowStageCode);

            var detail = new StringBuilder();
            detail.Append("현재 상태\n").Append(projection.Information("Status"))
                .Append("\n\n다음 단계\n").Append(projection.Information("NextStep"))
                .Append("\n\n판정 근거\n").Append(projection.Information("Evidence"));
            detailText.text = detail.ToString();
            boundaryText.text = projection.Information("Limitation");

            var preview = coordinator.CurrentPreview;
            previewText.text = preview == null
                ? projection.Information("Refresh")
                : "PREVIEW · " + preview.ActionLabel
                  + "\n대상  " + preview.TargetStableId
                  + "\n담당  " + preview.ActorStableId
                  + " · 예상 " + preview.DurationTicks + " Tick"
                  + (string.IsNullOrWhiteSpace(preview.SpatialStableId)
                      ? string.Empty
                      : "\n공간  " + preview.SpatialStableId
                        + " · " + 진부Hub입고Ui표시문구.공간근거(preview.SpatialEvidenceKindCode))
                  + (preview.CanConfirm ? "\n확정 전에는 상태가 변경되지 않습니다."
                      : "\n차단  " + string.Join(", ", Array.ConvertAll(
                          preview.BlockReasonCodes, 진부Hub입고Ui표시문구.공간차단사유)));

            staleBanner.SetActive(coordinator.IsStale || !profileCompatible);
            staleText.text = coordinator.IsStale
                ? "마지막 성공 상태를 표시 중 · " + coordinator.ErrorCode
                : "디자인 개정 불일치 · 중립 표현 사용";

            var previewAction = projection.Action(진부Hub입고UiCodes.PreviewAction);
            var confirmAction = projection.Action(진부Hub입고UiCodes.ConfirmAction);
            SetButtonLabel(previewButton, previewAction.KoreanLabel);
            SetButtonLabel(confirmButton, confirmAction.KoreanLabel);
            SetButtonLabel(tickButton, "WorldTick +1");
            SetButtonLabel(refreshButton, "상태 다시 불러오기");
            SetButtons(
                !busy && !coordinator.IsStale && previewAction.Enabled
                    && coordinator.CurrentPreview == null,
                !busy && !coordinator.IsStale && confirmAction.Enabled
                    && coordinator.CurrentPreview?.CanConfirm == true,
                !busy && !coordinator.IsStale
                    && projection.StateCode == 진부Hub입고UiCodes.InProgress,
                !busy);
            ApplyTheme(coordinator.IsStale ? "State.Stale" : projection.StateStyleSemanticKey,
                profileCompatible);
        }

        private void ApplyTheme(string stateStyle, bool profileCompatible)
        {
            panelSurface.color = theme.Background;
            roleAccent.color = profileCompatible ? theme.WarehouseAccent : theme.Muted;
            stateBadge.color = theme.ResolveState(stateStyle);
            contextText.color = theme.Muted;
            stateText.color = Color.white;
            titleText.color = theme.Text;
            summaryText.color = theme.Text;
            workflowText.color = theme.WarehouseAccent;
            detailText.color = theme.Text;
            previewText.color = theme.Text;
            boundaryText.color = theme.Muted;
            ApplyButtonColor(previewButton, theme.ResolveAction("Action.Preview"));
            ApplyButtonColor(confirmButton, theme.ResolveAction("Action.Confirm"));
            ApplyButtonColor(tickButton, theme.Success);
            ApplyButtonColor(refreshButton, theme.Muted);
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            previewButton.onClick.AddListener(() => _ = PreviewAsync());
            confirmButton.onClick.AddListener(() => _ = ConfirmAsync());
            tickButton.onClick.AddListener(() => _ = AdvanceAsync());
            refreshButton.onClick.AddListener(() => _ = RefreshAsync());
            listenersBound = true;
        }

        private void RenewCancellation()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = new CancellationTokenSource();
        }

        private CancellationToken Token()
            => lifetimeCancellation?.Token ?? CancellationToken.None;

        private static string Workflow(string stage)
        {
            var expected = stage == "Expected" ? "●" : "○";
            var inspection = stage == "PendingInspection" ? "●" : "○";
            var putAway = stage == "PutAwayPending" ? "●" : "○";
            var completed = stage == "PutAwayCompleted" ? "●" : "○";
            return expected + " 입고예정  →  " + inspection + " 검수대기  →  "
                   + putAway + " 적재대기  →  " + completed + " 적재완료";
        }

        private void SetButtons(bool previewEnabled, bool confirmEnabled, bool tickEnabled,
            bool refreshEnabled)
        {
            previewButton.interactable = previewEnabled;
            confirmButton.interactable = confirmEnabled;
            tickButton.interactable = tickEnabled;
            refreshButton.interactable = refreshEnabled;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        private static void ApplyButtonColor(Button button, Color color)
        {
            var image = button.GetComponent<Image>();
            if (image != null) image.color = color;
        }
    }
}
