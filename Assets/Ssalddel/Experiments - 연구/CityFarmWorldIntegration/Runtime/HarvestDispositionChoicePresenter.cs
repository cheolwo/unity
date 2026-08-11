using System;
using Ssalddel.Unity.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class HarvestDispositionChoiceActionCodes
    {
        public const string Reset = "Reset";
        public const string Cooperative = "Cooperative";
        public const string DirectOnline = "DirectOnline";
        public const string ExportAgent = "ExportAgent";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
    }

    [DefaultExecutionOrder(200)]
    public sealed class HarvestDispositionChoicePresenter : MonoBehaviour
    {
        [SerializeField] private PotatoCultivationLifecyclePresenter cultivation = null!;
        [SerializeField] private GameObject cardRoot = null!;
        [SerializeField] private GameObject cooperativeMarker = null!;
        [SerializeField] private GameObject directOnlineMarker = null!;
        [SerializeField] private GameObject exportAgentMarker = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text harvestText = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text selectionText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text limitationText = null!;
        [SerializeField] private bool autoOpenOnStart;

        private readonly HarvestDispositionSimulationValidator validator = new();
        private HarvestDispositionSimulationEngine engine = null!;
        private HarvestDispositionProjector projector = null!;
        private HarvestDispositionSimulationSnapshot snapshot = null!;
        private HarvestDispositionPreview? preview;
        private HarvestDispositionCommand? command;
        private HarvestDispositionCardModel model = null!;

        public HarvestDispositionSimulationSnapshot CurrentSnapshot => snapshot;
        public HarvestDispositionPreview? CurrentPreview => preview;
        public HarvestDispositionCommand? CurrentCommand => command;
        public HarvestDispositionCardModel CurrentModel => model;
        public bool IsCardOpen => cardRoot != null && cardRoot.activeSelf;

        public bool ValidateWiring() => cultivation != null && cardRoot != null
            && cooperativeMarker != null && directOnlineMarker != null && exportAgentMarker != null
            && titleText != null && harvestText != null && stateText != null
            && selectionText != null && detailText != null && limitationText != null;

        public void Configure(PotatoCultivationLifecyclePresenter source, GameObject card,
            GameObject cooperative, GameObject direct, GameObject export,
            Text title, Text harvest, Text state, Text selection, Text detail, Text limitation,
            bool autoOpen)
        {
            cultivation = source;
            cardRoot = card;
            cooperativeMarker = cooperative;
            directOnlineMarker = direct;
            exportAgentMarker = export;
            titleText = title;
            harvestText = harvest;
            stateText = state;
            selectionText = selection;
            detailText = detail;
            limitationText = limitation;
            autoOpenOnStart = autoOpen;
            Ensure();
            ResetChoice();
        }

        private void Start()
        {
            Ensure();
            if (snapshot == null) ResetChoice();
            if (autoOpenOnStart) OpenCard();
        }

        public void ExecuteAction(string actionCode)
        {
            switch (actionCode)
            {
                case HarvestDispositionChoiceActionCodes.Reset: ResetChoice(); OpenCard(); break;
                case HarvestDispositionChoiceActionCodes.Cooperative:
                    SelectChoice(HarvestDispositionChoiceCodes.CooperativeShipment); break;
                case HarvestDispositionChoiceActionCodes.DirectOnline:
                    SelectChoice(HarvestDispositionChoiceCodes.DirectOnlineSale); break;
                case HarvestDispositionChoiceActionCodes.ExportAgent:
                    SelectChoice(HarvestDispositionChoiceCodes.ExportAgent); break;
                case HarvestDispositionChoiceActionCodes.Confirm: ConfirmChoice(); break;
                case HarvestDispositionChoiceActionCodes.ApplyTick: ApplyTick(); break;
                default: throw new InvalidOperationException("HarvestDispositionActionUnknown:" + actionCode);
            }
        }

        public void ResetChoice()
        {
            Ensure();
            cultivation.RunGoldenPathToHarvest();
            snapshot = HarvestDispositionSimulationFixture.Create(cultivation.CurrentSnapshot);
            preview = null;
            command = null;
            Apply();
            cardRoot.SetActive(false);
        }

        public void OpenCard()
        {
            cardRoot.SetActive(true);
            Apply();
        }

        public void SelectChoice(string choiceCode)
        {
            preview = engine.Preview(snapshot, choiceCode);
            command = null;
            cardRoot.SetActive(true);
            Apply();
        }

        public void ConfirmChoice()
        {
            if (preview == null) throw new InvalidOperationException("HarvestDispositionPreviewMissing");
            command = engine.Confirm(snapshot, preview);
            Apply();
        }

        public void ApplyTick()
        {
            if (command == null) throw new InvalidOperationException("HarvestDispositionCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null;
            command = null;
            Apply();
        }

        public void RunDirectOnlinePath()
        {
            ResetChoice();
            OpenCard();
            SelectChoice(HarvestDispositionChoiceCodes.DirectOnlineSale);
            ConfirmChoice();
            ApplyTick();
        }

        public void RunCooperativePath()
        {
            ResetChoice();
            OpenCard();
            SelectChoice(HarvestDispositionChoiceCodes.CooperativeShipment);
            ConfirmChoice();
            ApplyTick();
        }

        private void Ensure()
        {
            if (engine != null) return;
            engine = new HarvestDispositionSimulationEngine(validator);
            projector = new HarvestDispositionProjector(validator);
        }

        private void Apply()
        {
            model = projector.Project(snapshot);
            titleText.text = model.Title;
            harvestText.text = "HARVEST LOT  " + model.HarvestText;
            stateText.text = "STATE  " + model.StateText + "   ·   REV " + snapshot.DataRevision;
            var option = preview == null ? null : Array.Find(snapshot.Options,
                value => value.ChoiceCode == preview.ChoiceCode);
            selectionText.text = command != null
                ? "CONFIRMED  " + command.ChoiceCode + " · APPLY TICK"
                : option != null
                    ? "PREVIEW  " + option.DisplayName + " · CONFIRM REQUIRED"
                    : snapshot.Decision == null ? "수확물과 상호작용해 판로를 선택하세요."
                        : "DECIDED  " + model.DecisionText;
            detailText.text = option != null ? option.Summary
                : snapshot.Decision == null
                    ? "조합 출하 / 온라인 직접 판매 / 수출대행 준비"
                    : snapshot.Options[Array.FindIndex(snapshot.Options,
                        value => value.ChoiceCode == snapshot.Decision.ChoiceCode)].Summary;
            limitationText.text = snapshot.Decision == null
                ? "선택은 후속 업무 후보만 만들며 출하·판매·수출을 확정하지 않습니다."
                : string.Join(" · ", snapshot.Options[Array.FindIndex(snapshot.Options,
                    value => value.ChoiceCode == snapshot.Decision.ChoiceCode)].Limitations);
            var choice = snapshot.Decision?.ChoiceCode ?? preview?.ChoiceCode ?? string.Empty;
            cooperativeMarker.SetActive(choice == HarvestDispositionChoiceCodes.CooperativeShipment);
            directOnlineMarker.SetActive(choice == HarvestDispositionChoiceCodes.DirectOnlineSale);
            exportAgentMarker.SetActive(choice == HarvestDispositionChoiceCodes.ExportAgent);
        }
    }
}
