using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 정착지상호작용Presenter : MonoBehaviour
    {
        public const string 수확LotObjectStableId = "harvest-lot:potato-001";
        public const string FixtureHarvestLotStableId = "harvest-lot:sim.potato.20260407.r1";

        [SerializeField] private SimulationWorldShellPresenter shellPresenter = null!;
        [SerializeField] private GameObject cardRoot = null!;
        [SerializeField] private Text lotText = null!;
        [SerializeField] private Text phaseText = null!;
        [SerializeField] private Text previewText = null!;
        [SerializeField] private Button cooperativeButton = null!;
        [SerializeField] private Button directButton = null!;
        [SerializeField] private Button storageButton = null!;
        [SerializeField] private Button exportButton = null!;
        [SerializeField] private Button confirmButton = null!;
        [SerializeField] private Button advanceButton = null!;

        private 정착지상호작용Coordinator coordinator = null!;
        private readonly 수확판로결과Projector 수확판로Projector = new();
        private 수확LotObjectMappingCatalog 수확LotMappingCatalog = DefaultMappingCatalog();
        private bool listenersBound;
        private bool busy;

        public string PhaseCode => coordinator?.PhaseCode ?? string.Empty;
        public string CurrentChoiceCode => coordinator?.CurrentPreview?.ChoiceCode
            ?? coordinator?.Current수확판로결과?.SelectedChoiceCode
            ?? string.Empty;
        public bool IsCardVisible => cardRoot != null && cardRoot.activeSelf;
        public string PreviewSummary => previewText != null ? previewText.text : string.Empty;
        public string 수확판로결과Summary => coordinator?.Current수확판로결과 == null
            ? string.Empty
            : Build수확판로결과Text(coordinator.Current수확판로결과);
        public string CurrentMappedHarvestLotStableId
        {
            get
            {
                if (shellPresenter == null) return string.Empty;
                return 수확LotMappingCatalog.TryResolve(
                    shellPresenter.SelectedObjectStableId, out var harvestLotStableId)
                    ? harvestLotStableId : string.Empty;
            }
        }

        private void Start()
        {
            ValidateWiring();
            BindListeners();
            shellPresenter.PresentationChanged += ApplyPresentation;
            if (coordinator != null)
            {
                ApplyPresentation();
                return;
            }
            var initial = 정착지상호작용FixtureAuthorityClient.CreateInitialSnapshot();
            InitializeAuthority(new 정착지상호작용FixtureAuthorityClient(initial), initial);
        }

        private void OnDestroy()
        {
            if (shellPresenter != null)
                shellPresenter.PresentationChanged -= ApplyPresentation;
            if (!listenersBound) return;
            cooperativeButton.onClick.RemoveAllListeners();
            directButton.onClick.RemoveAllListeners();
            storageButton.onClick.RemoveAllListeners();
            exportButton.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners();
            advanceButton.onClick.RemoveAllListeners();
        }

        public void Configure(
            SimulationWorldShellPresenter shell,
            GameObject root,
            Text lot,
            Text phase,
            Text preview,
            Button cooperative,
            Button direct,
            Button storage,
            Button export,
            Button confirm,
            Button advance)
        {
            shellPresenter = shell;
            cardRoot = root;
            lotText = lot;
            phaseText = phase;
            previewText = preview;
            cooperativeButton = cooperative;
            directButton = direct;
            storageButton = storage;
            exportButton = export;
            confirmButton = confirm;
            advanceButton = advance;
        }

        public void InitializeAuthority(
            I정착지상호작용AuthorityClient authorityClient,
            정착지상호작용AuthoritySnapshot initialSnapshot)
        {
            ValidateWiring();
            coordinator = new 정착지상호작용Coordinator(authorityClient, initialSnapshot);
            shellPresenter.ApplyAuthoritativeSnapshot(initialSnapshot.ToWorldShellSnapshot());
            ApplyPresentation();
            if (!string.IsNullOrWhiteSpace(initialSnapshot.AllocationStateCode))
                _ = TryRefresh수확판로결과목록Async();
        }

        public void Configure수확LotMappings(params 수확LotObjectMappingData[] mappings)
        {
            수확LotMappingCatalog = new 수확LotObjectMappingCatalog(mappings);
            ApplyPresentation();
        }

        public async Task SelectChoiceAsync(string choiceCode)
        {
            if (busy) return;
            busy = true;
            try
            {
                var envelope = 정착지상호작용BranchFixture.CreateEnvelope(choiceCode);
                if (envelope.PreviewRequest.HarvestLotStableId
                    != CurrentMappedHarvestLotStableId)
                    throw new InvalidOperationException("HarvestRouteDecisionSourceLotMismatch");
                await coordinator.PreviewAsync(envelope);
            }
            catch (Exception error)
            {
                Debug.LogError("SettlementInteractionPreviewFailed:" + error.Message);
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        public async Task ConfirmAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                await coordinator.ConfirmAsync();
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.ToWorldShellSnapshot());
                await TryRefresh수확판로결과Async();
            }
            catch (Exception error)
            {
                Debug.LogError("SettlementInteractionConfirmFailed:" + error.Message);
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        public async Task AdvanceToCompletionAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                await coordinator.AdvanceToCompletionAsync();
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.ToWorldShellSnapshot());
                await TryRefresh수확판로결과Async();
            }
            catch (Exception error)
            {
                Debug.LogError("SettlementInteractionTickFailed:" + error.Message);
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        public async Task RunReserveStorageGoldenPathAsync()
        {
            await SelectChoiceAsync(HarvestDispositionChoiceCodes.ReserveStorage);
            await ConfirmAsync();
            await AdvanceToCompletionAsync();
        }

        public async Task Refresh수확판로결과Async()
        {
            if (busy || coordinator == null) return;
            var harvestLotStableId = coordinator.CurrentPreview?.HarvestLotStableId
                ?? coordinator.Current수확판로결과?.HarvestLotStableId;
            if (string.IsNullOrWhiteSpace(harvestLotStableId)) return;
            busy = true;
            try
            {
                await coordinator.Refresh수확판로결과Async(harvestLotStableId);
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        public async Task Refresh수확판로결과목록Async()
        {
            if (busy || coordinator == null) return;
            busy = true;
            try
            {
                await coordinator.Refresh수확판로결과목록Async(
                    CurrentMappedHarvestLotStableId);
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.ToWorldShellSnapshot());
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        public void ValidateWiring()
        {
            if (shellPresenter == null || cardRoot == null || lotText == null
                || phaseText == null || previewText == null || cooperativeButton == null
                || directButton == null || storageButton == null || exportButton == null
                || confirmButton == null || advanceButton == null)
                throw new InvalidOperationException("SettlementInteractionWiringMissing");
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            cooperativeButton.onClick.AddListener(() =>
                _ = SelectChoiceAsync(HarvestDispositionChoiceCodes.CooperativeShipment));
            directButton.onClick.AddListener(() =>
                _ = SelectChoiceAsync(HarvestDispositionChoiceCodes.DirectOnlineSale));
            storageButton.onClick.AddListener(() =>
                _ = SelectChoiceAsync(HarvestDispositionChoiceCodes.ReserveStorage));
            exportButton.onClick.AddListener(() =>
                _ = SelectChoiceAsync(HarvestDispositionChoiceCodes.ExportAgent));
            confirmButton.onClick.AddListener(() => _ = ConfirmAsync());
            advanceButton.onClick.AddListener(() => _ = AdvanceToCompletionAsync());
            listenersBound = true;
        }

        private void ApplyPresentation()
        {
            if (coordinator == null || shellPresenter == null || cardRoot == null) return;
            var selected = 수확LotMappingCatalog.TryResolve(
                shellPresenter.SelectedObjectStableId, out var mappedHarvestLotStableId);
            cardRoot.SetActive(selected);
            if (!selected) return;

            if (coordinator.Current수확판로결과?.HarvestLotStableId
                    != mappedHarvestLotStableId
                && coordinator.Current수확판로결과목록.Any(value =>
                    value.HarvestLotStableId == mappedHarvestLotStableId))
                coordinator.Select수확판로결과(mappedHarvestLotStableId);

            var outcome = coordinator.Current수확판로결과;
            lotText.text = "감자 HARVEST LOT · 300 kg · "
                + (outcome == null ? "판로 미결정" : ChoiceLabel(outcome.SelectedChoiceCode))
                + "\n화면 " + StableTail(shellPresenter.SelectedObjectStableId)
                + " → 원장 " + StableTail(mappedHarvestLotStableId);
            phaseText.text = PhaseLabel(coordinator.PhaseCode)
                + "\nAuthority revision " + coordinator.CurrentSnapshot.Revision
                + " · WorldTick " + coordinator.CurrentSnapshot.WorldTick;
            previewText.text = BuildPreviewText();

            var canChoose = !busy
                && (coordinator.PhaseCode == 정착지상호작용PhaseCodes.LotSelected
                    || coordinator.PhaseCode == 정착지상호작용PhaseCodes.PreviewReady);
            cooperativeButton.interactable = canChoose;
            directButton.interactable = canChoose;
            storageButton.interactable = canChoose;
            exportButton.interactable = canChoose;
            confirmButton.interactable = !busy
                && coordinator.PhaseCode == 정착지상호작용PhaseCodes.PreviewReady;
            advanceButton.interactable = !busy
                && coordinator.CanResumeReservedTask;
            advanceButton.GetComponentInChildren<Text>().text = coordinator.CanResumeReservedTask
                ? "WORLD TICK × " + coordinator.CurrentSnapshot.TaskRemainingTicks
                : "WORLD TICK";
        }

        private string BuildPreviewText()
        {
            if (coordinator.PhaseCode == 정착지상호작용PhaseCodes.Failed)
                return "차단: " + coordinator.ErrorCode;
            var preview = coordinator.CurrentPreview;
            if (preview == null)
            {
                if (coordinator.Current수확판로결과 != null)
                    return "최신 서버 판로 결과\n"
                        + Build수확판로결과Text(coordinator.Current수확판로결과)
                        + BuildReservedTaskText()
                        + "\nRevision " + coordinator.CurrentSnapshot.Revision
                        + " · WorldTick " + coordinator.CurrentSnapshot.WorldTick;
                return "판로를 선택하면 서버 정책 Preview가 표시됩니다.\n"
                    + "Preview는 재정·노동·재고를 변경하지 않습니다.";
            }
            var revenue = preview.ProjectedRevenue.HasValue
                ? Number(preview.ProjectedRevenue.Value) + " KRW"
                : "없음";
            var storage = preview.ExpectedStoredQuantity.HasValue
                ? "\n예상 비축 " + Number(preview.ExpectedStoredQuantity.Value) + " kg"
                    + " · FoodSecurity " + Number(preview.FoodSecurityDaysBefore)
                    + " → " + Number(preview.FoodSecurityDaysCandidate) + "일"
                : string.Empty;
            var result = coordinator.PhaseCode == 정착지상호작용PhaseCodes.EffectApplied
                ? "\nEffect 적용 완료 · 새 snapshot 재조회 완료"
                : coordinator.PhaseCode == 정착지상호작용PhaseCodes.TaskReserved
                    ? "\nAllocation 예약 · Task 예정"
                    : "\n후보만 표시 · Confirm 전 무변경";
            var outcome = coordinator.Current수확판로결과 == null
                ? string.Empty
                : "\n\n판로 결과\n" + Build수확판로결과Text(
                    coordinator.Current수확판로결과);
            return ChoiceLabel(preview.ChoiceCode)
                + "\n비용 " + Number(preview.SimulationCost) + " KRW"
                + " · 노동 " + Number(preview.RequiredLabor)
                + " · 기간 " + preview.DurationTicks + " Tick"
                + "\n예상 Simulation 수입 " + revenue
                + storage + result
                + "\n정책 " + preview.PolicyRevision
                + " · 실제 판매/배송/정산 아님"
                + outcome;
        }

        private async Task TryRefresh수확판로결과Async()
        {
            var harvestLotStableId = coordinator.CurrentPreview?.HarvestLotStableId
                ?? coordinator.Current수확판로결과?.HarvestLotStableId;
            if (string.IsNullOrWhiteSpace(harvestLotStableId)) return;
            try
            {
                await coordinator.Refresh수확판로결과Async(harvestLotStableId);
            }
            catch (Exception error)
            {
                Debug.LogWarning("HarvestRouteOutcomeRefreshFailed:" + error.Message);
            }
        }

        private async Task TryRefresh수확판로결과목록Async()
        {
            if (busy) return;
            busy = true;
            try
            {
                await coordinator.Refresh수확판로결과목록Async();
                var mappedHarvestLotStableId = CurrentMappedHarvestLotStableId;
                if (!string.IsNullOrWhiteSpace(mappedHarvestLotStableId))
                    coordinator.Select수확판로결과(mappedHarvestLotStableId);
                shellPresenter.ApplyAuthoritativeSnapshot(
                    coordinator.CurrentSnapshot.ToWorldShellSnapshot());
            }
            catch (Exception error)
            {
                Debug.LogWarning("HarvestRouteOutcomeListRefreshFailed:" + error.Message);
            }
            finally
            {
                busy = false;
                ApplyPresentation();
            }
        }

        private string Build수확판로결과Text(수확판로결과Data source)
        {
            var model = 수확판로Projector.Project(source);
            return model.SelectedRouteText + "\n"
                + model.CurrentStageText + "\n"
                + model.ResultText;
        }

        private string BuildReservedTaskText()
            => coordinator.CanResumeReservedTask
                ? "\n예약 Task " + coordinator.CurrentSnapshot.AllocationTaskStableId
                    + " · 남은 " + coordinator.CurrentSnapshot.TaskRemainingTicks + " Tick"
                : string.Empty;

        private static string PhaseLabel(string phaseCode)
            => phaseCode switch
            {
                정착지상호작용PhaseCodes.LotSelected => "수확 Lot 선택",
                정착지상호작용PhaseCodes.PreviewReady => "판로 Preview 준비",
                정착지상호작용PhaseCodes.TaskReserved => "작업 예약",
                정착지상호작용PhaseCodes.EffectApplied => "Effect 적용 완료",
                _ => "상호작용 차단",
            };

        private static string ChoiceLabel(string choiceCode)
            => choiceCode switch
            {
                HarvestDispositionChoiceCodes.CooperativeShipment => "생산자 조합 출하",
                HarvestDispositionChoiceCodes.DirectOnlineSale => "온라인 직접 판매",
                HarvestDispositionChoiceCodes.ReserveStorage => "비축 보관",
                HarvestDispositionChoiceCodes.ExportAgent => "외부 교역 준비",
                _ => choiceCode,
            };

        private static string Number(decimal value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string StableTail(string stableId)
        {
            var separator = stableId.LastIndexOf(':');
            var tail = separator >= 0 ? stableId.Substring(separator + 1) : stableId;
            const string simulationPrefix = "sim.";
            return tail.StartsWith(simulationPrefix, StringComparison.Ordinal)
                ? tail.Substring(simulationPrefix.Length)
                : tail;
        }

        private static 수확LotObjectMappingCatalog DefaultMappingCatalog()
            => new(new 수확LotObjectMappingData(
                수확LotObjectStableId, FixtureHarvestLotStableId));
    }
}
