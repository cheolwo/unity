using System;
using Ssalddel.Unity.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class CooperativeIntakeActionCodes
    {
        public const string Reset = "Reset";
        public const string Review = "Review";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
        public const string ConnectCargo = "ConnectCargo";
    }

    [DefaultExecutionOrder(210)]
    public sealed class CooperativeIntakeLifecyclePresenter : MonoBehaviour
    {
        [SerializeField] private HarvestDispositionChoicePresenter disposition = null!;
        [SerializeField] private GameObject intakeMarker = null!;
        [SerializeField] private GameObject cargoCandidateMarker = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text intakeText = null!;
        [SerializeField] private Text candidateText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text actionText = null!;
        [SerializeField] private Text limitationText = null!;

        private readonly CooperativeIntakeSimulationValidator validator = new();
        private CooperativeIntakeSimulationEngine engine = null!;
        private CooperativeIntakeProjector projector = null!;
        private CooperativeIntakeSimulationSnapshot snapshot = null!;
        private CooperativeIntakePreview? preview;
        private CooperativeIntakeCommand? command;
        private CooperativeIntakePresentationModel model = null!;
        private 감자수확CargoSimulationSnapshot? cargoPreparation;

        public CooperativeIntakeSimulationSnapshot CurrentSnapshot => snapshot;
        public CooperativeIntakePreview? CurrentPreview => preview;
        public CooperativeIntakeCommand? CurrentCommand => command;
        public CooperativeIntakePresentationModel CurrentModel => model;
        public 감자수확CargoSimulationSnapshot? CurrentCargoPreparation => cargoPreparation;

        public bool ValidateWiring() => disposition != null && intakeMarker != null
            && cargoCandidateMarker != null && stateText != null && intakeText != null
            && candidateText != null && lineageText != null && actionText != null && limitationText != null;

        public void Configure(HarvestDispositionChoicePresenter source, GameObject intake,
            GameObject cargoCandidate, Text state, Text intakeLine, Text candidate, Text lineage,
            Text action, Text limitation)
        {
            disposition = source; intakeMarker = intake; cargoCandidateMarker = cargoCandidate;
            stateText = state; intakeText = intakeLine; candidateText = candidate;
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
                case CooperativeIntakeActionCodes.Reset: ResetLifecycle(); break;
                case CooperativeIntakeActionCodes.Review: ReviewIntake(); break;
                case CooperativeIntakeActionCodes.Confirm: ConfirmIntake(); break;
                case CooperativeIntakeActionCodes.ApplyTick: ApplyTick(); break;
                case CooperativeIntakeActionCodes.ConnectCargo: ConnectCargoPreparation(); break;
                default: throw new InvalidOperationException("CooperativeIntakeActionUnknown:" + actionCode);
            }
        }

        public void ResetLifecycle()
        {
            Ensure();
            disposition.RunCooperativePath();
            snapshot = CooperativeIntakeSimulationFixture.Create(disposition.CurrentSnapshot);
            preview = null; command = null; cargoPreparation = null;
            Apply("조합 인수 검토가 필요합니다.");
        }

        public void ReviewIntake()
        {
            preview = engine.Preview(snapshot);
            command = null;
            Apply("PREVIEW · 300kg 전량 인수 검토 · CONFIRM REQUIRED");
        }

        public void ConfirmIntake()
        {
            if (preview == null) throw new InvalidOperationException("CooperativeIntakePreviewMissing");
            command = engine.Confirm(snapshot, preview);
            Apply("CONFIRMED · APPLY TICK");
        }

        public void ApplyTick()
        {
            if (command == null) throw new InvalidOperationException("CooperativeIntakeCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null; command = null;
            Apply("조합 인수 준비 승인 · CARGO-1 연결 가능");
        }

        public void ConnectCargoPreparation()
        {
            cargoPreparation = new CooperativeHarvestCargoAdapter(validator).Create(snapshot);
            Apply("CARGO-1 포장 검토 OPEN · PackageLot/Cargo 미생성");
        }

        public void RunGoldenPath()
        {
            ResetLifecycle();
            ReviewIntake();
            ConfirmIntake();
            ApplyTick();
            ConnectCargoPreparation();
        }

        private void Ensure()
        {
            if (engine != null) return;
            engine = new CooperativeIntakeSimulationEngine(validator);
            projector = new CooperativeIntakeProjector(validator);
        }

        private void Apply(string action)
        {
            model = projector.Project(snapshot);
            stateText.text = "STATE  " + model.StateText;
            intakeText.text = "INTAKE  " + model.IntakeText;
            candidateText.text = cargoPreparation == null
                ? "NEXT  " + model.CandidateText
                : "NEXT  CARGO-1 PACKING PREVIEW READY · NO PACKAGE/CARGO";
            lineageText.text = "LINEAGE  " + model.LineageText;
            actionText.text = "ACTION  " + action;
            limitationText.text = "LIMIT  " + model.LimitationText;
            intakeMarker.SetActive(snapshot.IntakeLot != null);
            cargoCandidateMarker.SetActive(snapshot.CargoPreparationCandidate != null);
        }
    }
}
