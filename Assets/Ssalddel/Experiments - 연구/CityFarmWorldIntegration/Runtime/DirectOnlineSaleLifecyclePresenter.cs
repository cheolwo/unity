using System;
using Ssalddel.Unity.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class DirectOnlineSaleActionCodes
    {
        public const string Reset = "Reset";
        public const string ReviewPacking = "ReviewPacking";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
        public const string OpenListingDraft = "OpenListingDraft";
    }

    [DefaultExecutionOrder(210)]
    public sealed class DirectOnlineSaleLifecyclePresenter : MonoBehaviour
    {
        [SerializeField] private HarvestDispositionChoicePresenter disposition = null!;
        [SerializeField] private GameObject packingMarker = null!;
        [SerializeField] private GameObject listingMarker = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text packingText = null!;
        [SerializeField] private Text candidateText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text actionText = null!;
        [SerializeField] private Text limitationText = null!;

        private readonly DirectOnlineSaleSimulationValidator validator = new();
        private DirectOnlineSaleSimulationEngine engine = null!;
        private DirectOnlineSaleProjector projector = null!;
        private DirectOnlineSaleSimulationSnapshot snapshot = null!;
        private DirectOnlinePackingPreview? preview;
        private DirectOnlinePackingCommand? command;
        private DirectOnlineSalePresentationModel model = null!;
        private OnlineMarketListingDraftSnapshot? listingDraft;

        public DirectOnlineSaleSimulationSnapshot CurrentSnapshot => snapshot;
        public DirectOnlinePackingPreview? CurrentPreview => preview;
        public DirectOnlinePackingCommand? CurrentCommand => command;
        public DirectOnlineSalePresentationModel CurrentModel => model;
        public OnlineMarketListingDraftSnapshot? CurrentListingDraft => listingDraft;

        public bool ValidateWiring() => disposition != null && packingMarker != null && listingMarker != null
            && stateText != null && packingText != null && candidateText != null && lineageText != null
            && actionText != null && limitationText != null;

        public void Configure(HarvestDispositionChoicePresenter source, GameObject packing,
            GameObject listing, Text state, Text packingLine, Text candidate, Text lineage,
            Text action, Text limitation)
        {
            disposition = source; packingMarker = packing; listingMarker = listing;
            stateText = state; packingText = packingLine; candidateText = candidate;
            lineageText = lineage; actionText = action; limitationText = limitation;
            Ensure();
            ResetLifecycle();
        }

        private void Start()
        {
            Ensure();
            if (snapshot == null) ResetLifecycle();
        }

        public void ExecuteAction(string actionCode)
        {
            switch (actionCode)
            {
                case DirectOnlineSaleActionCodes.Reset: ResetLifecycle(); break;
                case DirectOnlineSaleActionCodes.ReviewPacking: ReviewPacking(); break;
                case DirectOnlineSaleActionCodes.Confirm: ConfirmPacking(); break;
                case DirectOnlineSaleActionCodes.ApplyTick: ApplyTick(); break;
                case DirectOnlineSaleActionCodes.OpenListingDraft: OpenListingDraft(); break;
                default: throw new InvalidOperationException("DirectOnlineSaleActionUnknown:" + actionCode);
            }
        }

        public void ResetLifecycle()
        {
            Ensure();
            disposition.RunDirectOnlinePath();
            snapshot = DirectOnlineSaleSimulationFixture.Create(disposition.CurrentSnapshot);
            preview = null; command = null; listingDraft = null;
            Apply("생산자 소포장 검토가 필요합니다.");
        }

        public void ReviewPacking()
        {
            preview = engine.PreviewPacking(snapshot);
            command = null;
            Apply("PREVIEW · 5kg × 60 ParcelBox · CONFIRM REQUIRED");
        }

        public void ConfirmPacking()
        {
            if (preview == null) throw new InvalidOperationException("DirectOnlinePackingPreviewMissing");
            command = engine.Confirm(snapshot, preview);
            Apply("CONFIRMED · APPLY TICK");
        }

        public void ApplyTick()
        {
            if (command == null) throw new InvalidOperationException("DirectOnlinePackingCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null; command = null;
            Apply("소포장 완료 · 상품 등록 초안 열기 가능");
        }

        public void OpenListingDraft()
        {
            listingDraft = new DirectOnlineListingDraftAdapter(validator).Create(snapshot);
            Apply("상품 등록 DRAFT OPEN · 비공개 · 가격/주문 없음");
        }

        public void RunGoldenPath()
        {
            ResetLifecycle();
            ReviewPacking();
            ConfirmPacking();
            ApplyTick();
            OpenListingDraft();
        }

        private void Ensure()
        {
            if (engine != null) return;
            engine = new DirectOnlineSaleSimulationEngine(validator);
            projector = new DirectOnlineSaleProjector(validator);
        }

        private void Apply(string action)
        {
            model = projector.Project(snapshot);
            stateText.text = "STATE  " + model.StateText;
            packingText.text = "PACKING  " + model.PackingText;
            candidateText.text = listingDraft == null
                ? "NEXT  " + model.CandidateText
                : "DRAFT  감자 5kg · 60개 · UNPUBLISHED · PRICE — · ORDERS 0";
            lineageText.text = "LINEAGE  " + model.LineageText;
            actionText.text = "ACTION  " + action;
            limitationText.text = "LIMIT  " + model.LimitationText;
            packingMarker.SetActive(snapshot.PackingLot != null);
            listingMarker.SetActive(listingDraft != null);
        }
    }
}
