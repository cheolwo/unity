using System;
using Ssalddel.Unity.PotatoJourney;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class PotatoCargoJourneyActionCodes
    {
        public const string Reset = "Reset";
        public const string ReviewDispatch = "ReviewDispatch";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
        public const string AdvanceOne = "AdvanceOne";
        public const string AdvanceToHub = "AdvanceToHub";
        public const string GoldenPath = "GoldenPath";
    }

    [DefaultExecutionOrder(300)]
    public sealed class PotatoCargoJourneyLifecyclePresenter : MonoBehaviour
    {
        [SerializeField] private PotatoHarvestCargoLifecyclePresenter cargoLifecycle = null!;
        [SerializeField] private 절차형VehicleRouteFollower routeFollower = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text cargoText = null!;
        [SerializeField] private Text progressText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text actionText = null!;
        [SerializeField] private Text limitationText = null!;
        [SerializeField] private bool autoRunGoldenPathOnStart;

        private readonly PotatoCargoJourneySimulationValidator validator = new PotatoCargoJourneySimulationValidator();
        private PotatoCargoJourneySimulationEngine engine = null!;
        private PotatoCargoJourneyProjector projector = null!;
        private PotatoCargoJourneySimulationSnapshot snapshot = null!;
        private PotatoCargoJourneyPreview? preview;
        private PotatoCargoJourneyCommand? command;
        private PotatoCargoJourneyPresentationModel model = null!;

        public PotatoCargoJourneySimulationSnapshot CurrentSnapshot => snapshot;
        public PotatoCargoJourneyPresentationModel CurrentModel => model;
        public PotatoCargoJourneyPreview? CurrentPreview => preview;
        public PotatoCargoJourneyCommand? CurrentCommand => command;

        public bool ValidateWiring() => cargoLifecycle != null && routeFollower != null
            && routeFollower.ValidateWiring() && stateText != null && cargoText != null
            && progressText != null && lineageText != null && actionText != null && limitationText != null;

        public void Configure(PotatoHarvestCargoLifecyclePresenter cargo,
            절차형VehicleRouteFollower follower, Text state, Text cargoLabel, Text progress,
            Text lineage, Text action, Text limitation, bool runGoldenPathOnStart)
        {
            cargoLifecycle = cargo; routeFollower = follower; stateText = state; cargoText = cargoLabel;
            progressText = progress; lineageText = lineage; actionText = action; limitationText = limitation;
            autoRunGoldenPathOnStart = runGoldenPathOnStart;
            EnsureServices(); ResetJourney();
        }

        private void Start()
        {
            EnsureServices();
            if (snapshot == null) ResetJourney();
            if (autoRunGoldenPathOnStart) RunGoldenPath();
        }

        public void ExecuteAction(string code)
        {
            switch (code)
            {
                case PotatoCargoJourneyActionCodes.Reset: ResetJourney(); break;
                case PotatoCargoJourneyActionCodes.ReviewDispatch: ReviewDispatch(); break;
                case PotatoCargoJourneyActionCodes.Confirm: ConfirmDispatch(); break;
                case PotatoCargoJourneyActionCodes.ApplyTick: ApplyConfirmedTick(); break;
                case PotatoCargoJourneyActionCodes.AdvanceOne: AdvanceRoute(1); break;
                case PotatoCargoJourneyActionCodes.AdvanceToHub: AdvanceToHub(); break;
                case PotatoCargoJourneyActionCodes.GoldenPath: RunGoldenPath(); break;
                default: throw new InvalidOperationException("PotatoCargoJourneyActionUnknown:" + code);
            }
        }

        public void ResetJourney()
        {
            EnsureServices(); cargoLifecycle.RunGoldenPath();
            snapshot = PotatoCargoJourneySimulationFixture.Create(cargoLifecycle.CurrentSnapshot);
            preview = null; command = null; ApplyPresentation();
        }

        public void ReviewDispatch() { preview = engine.PreviewDispatch(snapshot); command = null; ApplyPresentation(); }
        public void ConfirmDispatch()
        {
            if (preview == null) throw new InvalidOperationException("PotatoCargoJourneyPreviewMissing");
            command = engine.ConfirmDispatch(snapshot, preview); ApplyPresentation();
        }
        public void ApplyConfirmedTick()
        {
            if (command == null) throw new InvalidOperationException("PotatoCargoJourneyCommandMissing");
            snapshot = engine.Tick(snapshot, command); preview = null; command = null; ApplyPresentation();
        }
        public void AdvanceRoute(int ticks)
        {
            snapshot = engine.Tick(snapshot, engine.CreateAdvanceRouteCommand(snapshot, ticks));
            preview = null; command = null; ApplyPresentation();
        }
        public void AdvanceToHub()
        {
            var remaining = snapshot.Rule.RequiredRouteTicks - snapshot.CompletedRouteTicks;
            if (remaining > 0) AdvanceRoute(remaining);
        }
        public void RunGoldenPath()
        {
            ResetJourney(); ReviewDispatch(); ConfirmDispatch(); ApplyConfirmedTick(); AdvanceToHub();
        }

        private void EnsureServices()
        {
            if (engine != null) return;
            engine = new PotatoCargoJourneySimulationEngine(validator);
            projector = new PotatoCargoJourneyProjector(validator);
        }

        private void ApplyPresentation()
        {
            model = projector.Project(snapshot);
            stateText.text = "STATE  " + model.StateCode + "   ·   DATE " + model.DateText
                + "   ·   DATA REV " + snapshot.DataRevision;
            cargoText.text = "CARGO  " + model.CargoText;
            progressText.text = "ROUTE  " + model.ProgressText;
            lineageText.text = "LINEAGE  " + snapshot.HarvestLotStableId
                + "\n→ " + snapshot.PackageLotStableId + " → " + snapshot.Cargo.StableId;
            actionText.text = command != null ? "CONFIRMED DISPATCH · APPLY TICK"
                : preview != null ? "DISPATCH REVIEW · CONFIRM REQUIRED"
                : snapshot.StateCode == PotatoCargoJourneyStateCodes.Loaded ? "LOADED · REVIEW DISPATCH"
                : snapshot.StateCode == PotatoCargoJourneyStateCodes.InTransit ? "IN TRANSIT · ADVANCE ROUTE TICK"
                : "ARRIVED AT HUB · RECEIVING NOT CONFIRMED";
            limitationText.text = "LIMIT  " + model.LimitationText;
            routeFollower.enabled = false;
            routeFollower.transform.position = Vector3.Lerp(routeFollower.RouteStart.position,
                routeFollower.RouteEnd.position, model.NormalizedProgress);
            var direction = routeFollower.RouteEnd.position - routeFollower.RouteStart.position;
            if (direction.sqrMagnitude > .0001f)
                routeFollower.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
