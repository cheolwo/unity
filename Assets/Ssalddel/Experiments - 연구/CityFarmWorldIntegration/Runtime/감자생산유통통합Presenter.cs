using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class 감자생산유통통합Presenter : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas[] stageCanvases = Array.Empty<Canvas>();
        [SerializeField] private Transform[] stageTargets = Array.Empty<Transform>();
        [SerializeField] private string[] stageLabels = Array.Empty<string>();
        [SerializeField] private Text currentStageText;
        [SerializeField] private Text demonstrationStateText;
        [SerializeField] private Text lineageText;
        [SerializeField] private Vector3 cameraOffset = new(-13f, 16f, -18f);
        [SerializeField] private PotatoCultivationLifecyclePresenter cultivation = null!;
        [SerializeField] private PotatoHarvestCargoLifecyclePresenter harvestCargo = null!;
        [SerializeField] private PotatoCargoJourneyLifecyclePresenter cargoJourney = null!;
        [SerializeField] private PotatoHubReceivingLifecyclePresenter hubReceiving = null!;
        [SerializeField] private PotatoHubDispositionLifecyclePresenter hubDisposition = null!;
        [SerializeField] private PotatoJourneyCityPresenter cityArrival = null!;
        [SerializeField] private float demonstrationStepSeconds = .65f;

        private Coroutine? demonstrationRoutine;
        private Vector3 cityTargetOriginalScale;

        public int CurrentStageIndex { get; private set; }
        public int StageCount => stageLabels?.Length ?? 0;
        public string CurrentStageLabel => ValidateIndex(CurrentStageIndex)
            ? stageLabels[CurrentStageIndex]
            : string.Empty;
        public string CurrentActionLabel { get; private set; } = "대기";
        public bool IsDemonstrating => demonstrationRoutine != null;
        public int DemonstrationRunNumber { get; private set; }

        public void Configure(
            Camera camera,
            Canvas[] canvases,
            Transform[] targets,
            string[] labels,
            Text stageText,
            Text demonstrationText,
            Text lineage,
            Vector3 offset)
        {
            worldCamera = camera;
            stageCanvases = canvases ?? Array.Empty<Canvas>();
            stageTargets = targets ?? Array.Empty<Transform>();
            stageLabels = labels ?? Array.Empty<string>();
            currentStageText = stageText;
            demonstrationStateText = demonstrationText;
            lineageText = lineage;
            cameraOffset = offset;
            SelectStage(0);
        }

        public void ConfigureDemonstrations(
            PotatoCultivationLifecyclePresenter cultivationPresenter,
            PotatoHarvestCargoLifecyclePresenter harvestCargoPresenter,
            PotatoCargoJourneyLifecyclePresenter cargoJourneyPresenter,
            PotatoHubReceivingLifecyclePresenter hubReceivingPresenter,
            PotatoHubDispositionLifecyclePresenter hubDispositionPresenter,
            PotatoJourneyCityPresenter cityArrivalPresenter,
            float stepSeconds = .65f)
        {
            cultivation = cultivationPresenter;
            harvestCargo = harvestCargoPresenter;
            cargoJourney = cargoJourneyPresenter;
            hubReceiving = hubReceivingPresenter;
            hubDisposition = hubDispositionPresenter;
            cityArrival = cityArrivalPresenter;
            demonstrationStepSeconds = Mathf.Max(.05f, stepSeconds);
            if (stageTargets.Length == 6)
                cityTargetOriginalScale = stageTargets[5].localScale;
        }

        private void Awake()
        {
            if (ValidateWiring())
                SelectStage(Mathf.Clamp(CurrentStageIndex, 0, StageCount - 1));
        }

        public void SelectStage(int stageIndex)
        {
            if (!ValidateIndex(stageIndex))
                throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, "PotatoIntegratedStageInvalid");

            CurrentStageIndex = stageIndex;
            for (var index = 0; index < stageCanvases.Length; index++)
                stageCanvases[index].gameObject.SetActive(index == stageIndex);
            foreach (var stageButton in GetComponentsInChildren<감자생산유통단계Button>(true))
                stageButton.SetSelected(stageButton.StageIndex == stageIndex);

            foreach (var controller in stageCanvases[stageIndex]
                         .GetComponentsInChildren<정보Panel상호작용Controller>(true))
                controller.ShowExpanded();

            var target = stageTargets[stageIndex];
            worldCamera.transform.position = target.position + cameraOffset;
            worldCamera.transform.LookAt(target.position + Vector3.up * 1.4f);

            currentStageText.text = $"{stageIndex + 1}/{StageCount} · {stageLabels[stageIndex]}";
            demonstrationStateText.text = "시연 준비 · 버튼 입력을 적용하는 중";
            lineageText.text = "product:potato → harvest-lot:potato-001 → cargo:potato-001\n"
                               + "Simulation · 버튼을 누르면 해당 단계 시연을 처음부터 재생합니다.";

            if (UnityEngine.Application.isPlaying && ValidateDemonstrationWiring())
                RestartStageDemonstration(stageIndex);
        }

        public void RestartCurrentStageDemonstration()
        {
            if (!ValidateIndex(CurrentStageIndex))
                throw new InvalidOperationException("PotatoIntegratedCurrentStageMissing");
            RestartStageDemonstration(CurrentStageIndex);
        }

        private void RestartStageDemonstration(int stageIndex)
        {
            if (demonstrationRoutine != null)
                StopCoroutine(demonstrationRoutine);
            RestoreCityTargetScale();
            DemonstrationRunNumber++;
            demonstrationRoutine = StartCoroutine(RunStageDemonstration(stageIndex));
        }

        private IEnumerator RunStageDemonstration(int stageIndex)
        {
            switch (stageIndex)
            {
                case 0:
                    yield return DemonstrateCultivation();
                    break;
                case 1:
                    yield return DemonstrateHarvestCargo();
                    break;
                case 2:
                    yield return DemonstrateCargoJourney();
                    break;
                case 3:
                    yield return DemonstrateHubReceiving();
                    break;
                case 4:
                    yield return DemonstrateHubDisposition();
                    break;
                case 5:
                    yield return DemonstrateCityArrival();
                    break;
            }

            SetActionLabel("시연 완료 · 버튼을 다시 누르면 처음부터 재생");
            demonstrationRoutine = null;
        }

        private IEnumerator DemonstrateCultivation()
        {
            cultivation.ResetLifecycle();
            yield return Step("밭 준비 · 파종 검토");
            cultivation.ReviewSowing();
            yield return Step("파종 Preview · 명시적 확인");
            cultivation.ConfirmPreview();
            yield return Step("파종 Confirm · Simulation Tick 적용");
            cultivation.ApplyConfirmedTick();
            yield return Step("생육 진행 · 수확 가능 상태로 이동");
            cultivation.AdvanceToHarvestReady();
            yield return Step("수확 검토 · 300kg Harvest Lot 준비");
            cultivation.ReviewHarvest();
            cultivation.ConfirmPreview();
            yield return Step("수확 적용 · 감자 상자 표시");
            cultivation.ApplyConfirmedTick();
        }

        private IEnumerator DemonstrateHarvestCargo()
        {
            harvestCargo.ResetLifecycle();
            yield return Step("수확물 300kg · 포장 검토");
            harvestCargo.ReviewPacking();
            yield return Step("포장 Preview · 명시적 확인");
            harvestCargo.ConfirmPreview();
            harvestCargo.ApplyConfirmedTick();
            yield return Step("포장 완료 · 차량 상차 검토");
            harvestCargo.ReviewLoading();
            yield return Step("상차 Preview · 명시적 확인");
            harvestCargo.ConfirmPreview();
            harvestCargo.ApplyConfirmedTick();
            yield return Step("상차 완료 · Cargo 출발 준비");
        }

        private IEnumerator DemonstrateCargoJourney()
        {
            cargoJourney.ResetJourney();
            yield return Step("농장 출발 · 배차 Simulation 검토");
            cargoJourney.ReviewDispatch();
            yield return Step("출발 Preview · 명시적 확인");
            cargoJourney.ConfirmDispatch();
            cargoJourney.ApplyConfirmedTick();
            yield return Step("Cargo 이동 시작");
            var requiredTicks = cargoJourney.CurrentSnapshot.Rule.RequiredRouteTicks;
            while (cargoJourney.CurrentSnapshot.CompletedRouteTicks < requiredTicks)
            {
                cargoJourney.AdvanceRoute(1);
                SetActionLabel($"거점 이동 중 · {cargoJourney.CurrentSnapshot.CompletedRouteTicks}/{requiredTicks}");
                yield return new WaitForSeconds(demonstrationStepSeconds * .55f);
            }
            yield return Step("물류 거점 도착 · 입고 대기");
        }

        private IEnumerator DemonstrateHubReceiving()
        {
            hubReceiving.ResetLifecycle();
            yield return Step("도착 Cargo · 입고 검토");
            hubReceiving.ReviewReceiving();
            yield return Step("입고 Preview · 명시적 확인");
            hubReceiving.ConfirmPreview();
            hubReceiving.ApplyTick();
            yield return Step("입고 완료 · 품질 검수 진행");
            hubReceiving.ReviewInspection();
            yield return Step("검수 Preview · 합격 288kg / 제외 12kg");
            hubReceiving.ConfirmPreview();
            hubReceiving.ApplyTick();
            yield return Step("입고·검수 완료");
        }

        private IEnumerator DemonstrateHubDisposition()
        {
            hubDisposition.ResetLifecycle();
            yield return Step("검수 통과 물량 · 판로별 분리 검토");
            hubDisposition.ReviewSeparation();
            yield return Step("Lot 분리 Preview · 명시적 확인");
            hubDisposition.ConfirmPreview();
            hubDisposition.ApplyTick();
            yield return Step("Lot 분리 완료 · 도시 출고 후보 검토");
            hubDisposition.ReviewOutbound();
            yield return Step("도시 판로 후보 Preview · 명시적 확인");
            hubDisposition.ConfirmPreview();
            hubDisposition.ApplyTick();
            yield return Step("판로 분배 완료 · 후보 경로 표시");
        }

        private IEnumerator DemonstrateCityArrival()
        {
            cityArrival.ApplyProjection();
            var target = stageTargets[5];
            cityTargetOriginalScale = target.localScale;
            target.localScale = cityTargetOriginalScale * .18f;
            SetActionLabel("도시 도착 · 판매 지점에 상품 전개");
            var elapsed = 0f;
            var duration = demonstrationStepSeconds * 1.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var bounce = 1f + Mathf.Sin(progress * Mathf.PI) * .22f;
                target.localScale = Vector3.Lerp(cityTargetOriginalScale * .18f,
                    cityTargetOriginalScale, progress) * bounce;
                yield return null;
            }
            target.localScale = cityTargetOriginalScale;
            yield return Step("도시 도착 완료 · 공개 상품 정보 표시");
        }

        private YieldInstruction Step(string label)
        {
            SetActionLabel(label);
            return new WaitForSeconds(demonstrationStepSeconds);
        }

        private void SetActionLabel(string label)
        {
            CurrentActionLabel = label;
            demonstrationStateText.text = "ACTION · " + label;
        }

        private void RestoreCityTargetScale()
        {
            if (stageTargets != null && stageTargets.Length == 6 && cityTargetOriginalScale != Vector3.zero)
                stageTargets[5].localScale = cityTargetOriginalScale;
        }

        public bool ValidateWiring()
            => worldCamera != null
               && currentStageText != null
               && demonstrationStateText != null
               && lineageText != null
               && stageCanvases != null
               && stageTargets != null
               && stageLabels != null
               && stageCanvases.Length == stageTargets.Length
               && stageTargets.Length == stageLabels.Length
               && stageLabels.Length == 6
               && stageCanvases.All(value => value != null)
               && stageTargets.All(value => value != null)
               && stageLabels.All(value => !string.IsNullOrWhiteSpace(value));

        public bool ValidateDemonstrationWiring()
            => cultivation != null && harvestCargo != null && cargoJourney != null
               && hubReceiving != null && hubDisposition != null && cityArrival != null
               && demonstrationStepSeconds > 0f;

        public int ActiveStageCanvasCount()
            => stageCanvases.Count(value => value != null && value.gameObject.activeSelf);

        private bool ValidateIndex(int index)
            => stageLabels != null && index >= 0 && index < stageLabels.Length;
    }

}
