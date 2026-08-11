using System;
using Ssalddel.Unity.PotatoJourney;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class PotatoHubDispositionActionCodes
    {
        public const string Reset = "Reset";
        public const string ReviewSeparation = "ReviewSeparation";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
        public const string ReviewOutbound = "ReviewOutbound";
        public const string Finish = "Finish";
        public const string GoldenPath = "GoldenPath";
    }

    [DefaultExecutionOrder(500)]
    public sealed class PotatoHubDispositionLifecyclePresenter : MonoBehaviour
    {
        [SerializeField] private PotatoHubReceivingLifecyclePresenter receiving = null!;
        [SerializeField] private GameObject acceptedLotMarker = null!;
        [SerializeField] private GameObject rejectedLossLotMarker = null!;
        [SerializeField] private GameObject outboundCandidateMarker = null!;
        [SerializeField] private LineRenderer outboundCandidateRoute = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text lotsText = null!;
        [SerializeField] private Text candidateText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text actionText = null!;
        [SerializeField] private Text limitationText = null!;
        [SerializeField] private bool autoRunGoldenPathOnStart;

        private readonly PotatoHubDispositionSimulationValidator validator = new();
        private PotatoHubDispositionSimulationEngine engine = null!;
        private PotatoHubDispositionProjector projector = null!;
        private PotatoHubDispositionSimulationSnapshot snapshot = null!;
        private PotatoHubDispositionPreview? preview;
        private PotatoHubDispositionCommand? command;
        private PotatoHubDispositionPresentationModel model = null!;

        public PotatoHubDispositionSimulationSnapshot CurrentSnapshot => snapshot;
        public PotatoHubDispositionPresentationModel CurrentModel => model;
        public PotatoHubDispositionPreview? CurrentPreview => preview;
        public PotatoHubDispositionCommand? CurrentCommand => command;

        public bool ValidateWiring() => receiving != null && acceptedLotMarker != null
            && rejectedLossLotMarker != null && outboundCandidateMarker != null
            && outboundCandidateRoute != null && stateText != null && lotsText != null
            && candidateText != null && lineageText != null && actionText != null
            && limitationText != null;

        public void Configure(PotatoHubReceivingLifecyclePresenter source,
            GameObject accepted, GameObject rejected, GameObject outbound, LineRenderer route,
            Text state, Text lots, Text candidate, Text lineage, Text action, Text limitation, bool golden)
        {
            receiving = source;
            acceptedLotMarker = accepted;
            rejectedLossLotMarker = rejected;
            outboundCandidateMarker = outbound;
            outboundCandidateRoute = route;
            stateText = state;
            lotsText = lots;
            candidateText = candidate;
            lineageText = lineage;
            actionText = action;
            limitationText = limitation;
            autoRunGoldenPathOnStart = golden;
            Ensure();
            ResetLifecycle();
        }

        private void Start()
        {
            Ensure();
            if (snapshot == null) ResetLifecycle();
            if (autoRunGoldenPathOnStart) RunGoldenPath();
        }

        public void ExecuteAction(string code)
        {
            switch (code)
            {
                case PotatoHubDispositionActionCodes.Reset: ResetLifecycle(); break;
                case PotatoHubDispositionActionCodes.ReviewSeparation: ReviewSeparation(); break;
                case PotatoHubDispositionActionCodes.Confirm: ConfirmPreview(); break;
                case PotatoHubDispositionActionCodes.ApplyTick: ApplyTick(); break;
                case PotatoHubDispositionActionCodes.ReviewOutbound: ReviewOutbound(); break;
                case PotatoHubDispositionActionCodes.Finish: Finish(); break;
                case PotatoHubDispositionActionCodes.GoldenPath: RunGoldenPath(); break;
                default: throw new InvalidOperationException("PotatoHubDispositionActionUnknown:" + code);
            }
        }

        public void ResetLifecycle()
        {
            Ensure();
            receiving.RunGoldenPath();
            snapshot = PotatoHubDispositionSimulationFixture.Create(receiving.CurrentSnapshot);
            preview = null;
            command = null;
            Apply();
        }

        public void ReviewSeparation()
        {
            preview = engine.PreviewSeparation(snapshot);
            command = null;
            Apply();
        }

        public void ReviewOutbound()
        {
            preview = engine.PreviewOutboundCandidate(snapshot);
            command = null;
            Apply();
        }

        public void ConfirmPreview()
        {
            if (preview == null) throw new InvalidOperationException("PotatoHubDispositionPreviewMissing");
            command = engine.Confirm(snapshot, preview);
            Apply();
        }

        public void ApplyTick()
        {
            if (command == null) throw new InvalidOperationException("PotatoHubDispositionCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null;
            command = null;
            Apply();
        }

        public void Finish()
        {
            if (snapshot.StateCode == PotatoHubDispositionStateCodes.AcceptedAtHub)
            {
                ReviewSeparation();
                ConfirmPreview();
                ApplyTick();
            }
            if (snapshot.StateCode == PotatoHubDispositionStateCodes.LotsSeparated)
            {
                ReviewOutbound();
                ConfirmPreview();
                ApplyTick();
            }
        }

        public void RunGoldenPath()
        {
            ResetLifecycle();
            Finish();
        }

        private void Ensure()
        {
            if (engine != null) return;
            engine = new PotatoHubDispositionSimulationEngine(validator);
            projector = new PotatoHubDispositionProjector(validator);
        }

        private void Apply()
        {
            model = projector.Project(snapshot);
            stateText.text = "STATE  " + model.StateCode + "   ·   DATA REV " + snapshot.DataRevision;
            lotsText.text = "LOTS  " + model.LotsText;
            candidateText.text = "OUTBOUND  " + model.CandidateText;
            lineageText.text = "LINEAGE  " + model.LineageText;
            actionText.text = command != null ? "CONFIRMED  " + command.CommandCode + " · APPLY TICK"
                : preview != null ? "REVIEW  " + preview.CommandCode + " · CONFIRM REQUIRED"
                : snapshot.StateCode == PotatoHubDispositionStateCodes.AcceptedAtHub
                    ? "ACCEPTED INSPECTION · REVIEW LOT SPLIT"
                    : snapshot.StateCode == PotatoHubDispositionStateCodes.LotsSeparated
                        ? "LOTS SEPARATED · REVIEW CITY OUTBOUND"
                        : "CANDIDATE ONLY · NO CARGO / INVENTORY CREATED";
            limitationText.text = "LIMIT  " + model.LimitationText;
            var separated = snapshot.AcceptedLot != null;
            var candidate = snapshot.OutboundCandidate != null;
            acceptedLotMarker.SetActive(separated);
            rejectedLossLotMarker.SetActive(separated);
            outboundCandidateMarker.SetActive(candidate);
            outboundCandidateRoute.gameObject.SetActive(candidate);
        }
    }
}
