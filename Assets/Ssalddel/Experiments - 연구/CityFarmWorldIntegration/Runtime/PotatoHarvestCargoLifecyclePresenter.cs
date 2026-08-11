using System;
using Ssalddel.Unity.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class PotatoHarvestCargoActionCodes
    {
        public const string Reset = "Reset";
        public const string ReviewPacking = "ReviewPacking";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
        public const string ReviewLoading = "ReviewLoading";
        public const string FinishLoading = "FinishLoading";
        public const string GoldenPath = "GoldenPath";
    }

    [DefaultExecutionOrder(200)]
    public sealed class PotatoHarvestCargoLifecyclePresenter : MonoBehaviour
    {
        [SerializeField] private PotatoCultivationLifecyclePresenter cultivation = null!;
        [SerializeField] private GameObject cargoVisual = null!;
        [SerializeField] private GameObject packageMarker = null!;
        [SerializeField] private GameObject cargoMarker = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text packageText = null!;
        [SerializeField] private Text cargoText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text actionText = null!;
        [SerializeField] private Text limitationText = null!;
        [SerializeField] private bool autoRunGoldenPathOnStart;

        private readonly 감자수확CargoSimulationValidator validator = new 감자수확CargoSimulationValidator();
        private 감자수확CargoSimulationEngine engine = null!;
        private 감자수확CargoProjector projector = null!;
        private 감자수확CargoSimulationSnapshot snapshot = null!;
        private 감자수확CargoPreview? preview;
        private 감자수확CargoCommand? command;
        private 감자수확CargoPresentationModel model = null!;
        private Vector3 cargoScale;

        public 감자수확CargoSimulationSnapshot CurrentSnapshot => snapshot;
        public 감자수확CargoPresentationModel CurrentModel => model;
        public 감자수확CargoPreview? CurrentPreview => preview;
        public 감자수확CargoCommand? CurrentCommand => command;

        public bool ValidateWiring() => cultivation != null && cargoVisual != null
            && packageMarker != null && cargoMarker != null && stateText != null
            && packageText != null && cargoText != null && lineageText != null
            && actionText != null && limitationText != null;

        public void Configure(PotatoCultivationLifecyclePresenter cultivationPresenter,
            GameObject visual, GameObject packedMarker, GameObject loadedMarker,
            Text state, Text package, Text cargo, Text lineage, Text action, Text limitation,
            bool runGoldenPathOnStart)
        {
            cultivation = cultivationPresenter;
            cargoVisual = visual;
            packageMarker = packedMarker;
            cargoMarker = loadedMarker;
            stateText = state;
            packageText = package;
            cargoText = cargo;
            lineageText = lineage;
            actionText = action;
            limitationText = limitation;
            autoRunGoldenPathOnStart = runGoldenPathOnStart;
            cargoScale = cargoVisual.transform.localScale;
            EnsureServices();
            ResetLifecycle();
        }

        private void Start()
        {
            EnsureServices();
            if (snapshot == null) ResetLifecycle();
            if (autoRunGoldenPathOnStart) RunGoldenPath();
        }

        public void ExecuteAction(string code)
        {
            switch (code)
            {
                case PotatoHarvestCargoActionCodes.Reset: ResetLifecycle(); break;
                case PotatoHarvestCargoActionCodes.ReviewPacking: ReviewPacking(); break;
                case PotatoHarvestCargoActionCodes.Confirm: ConfirmPreview(); break;
                case PotatoHarvestCargoActionCodes.ApplyTick: ApplyConfirmedTick(); break;
                case PotatoHarvestCargoActionCodes.ReviewLoading: ReviewLoading(); break;
                case PotatoHarvestCargoActionCodes.FinishLoading: FinishLoading(); break;
                case PotatoHarvestCargoActionCodes.GoldenPath: RunGoldenPath(); break;
                default: throw new InvalidOperationException("PotatoHarvestCargoActionUnknown:" + code);
            }
        }

        public void ResetLifecycle()
        {
            EnsureServices();
            cultivation.RunGoldenPathToHarvest();
            snapshot = 감자수확CargoSimulationFixture.Create(cultivation.CurrentSnapshot.HarvestLot!);
            preview = null;
            command = null;
            ApplyPresentation();
        }

        public void ReviewPacking() { preview = engine.PreviewPacking(snapshot); command = null; ApplyPresentation(); }
        public void ReviewLoading() { preview = engine.PreviewLoading(snapshot); command = null; ApplyPresentation(); }

        public void ConfirmPreview()
        {
            if (preview == null) throw new InvalidOperationException("PotatoHarvestCargoPreviewMissing");
            command = engine.Confirm(snapshot, preview);
            ApplyPresentation();
        }

        public void ApplyConfirmedTick()
        {
            if (command == null) throw new InvalidOperationException("PotatoHarvestCargoCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null;
            command = null;
            ApplyPresentation();
        }

        public void FinishLoading()
        {
            if (snapshot.PackageLot == null) { ReviewPacking(); ConfirmPreview(); ApplyConfirmedTick(); }
            if (snapshot.Cargo == null) { ReviewLoading(); ConfirmPreview(); ApplyConfirmedTick(); }
        }

        public void RunGoldenPath() { ResetLifecycle(); FinishLoading(); }

        private void EnsureServices()
        {
            if (engine != null) return;
            engine = new 감자수확CargoSimulationEngine(validator);
            projector = new 감자수확CargoProjector(validator);
        }

        private void ApplyPresentation()
        {
            model = projector.Project(snapshot);
            stateText.text = "STATE  " + model.StateCode + "   ·   REV " + snapshot.DataRevision
                + "\nSOURCE  " + model.SourceModeCode;
            packageText.text = "PACKAGE  " + model.PackageLotText;
            cargoText.text = "CARGO  " + model.CargoText;
            lineageText.text = snapshot.Cargo == null
                ? "LINEAGE  " + model.LineageText
                : "LINEAGE  " + snapshot.HarvestLot.StableId
                    + "\n→ " + snapshot.PackageLot!.StableId + " → " + snapshot.Cargo.StableId;
            actionText.text = command != null ? "CONFIRMED  " + command.CommandCode + " · APPLY TICK"
                : preview != null ? "REVIEW  " + preview.CommandCode + " · CONFIRM REQUIRED"
                : snapshot.Cargo != null ? "LOADED · READY FOR SIMULATION JOURNEY"
                : snapshot.PackageLot != null ? "PACKED · REVIEW LOADING" : "HARVEST LOT · REVIEW PACKING";
            limitationText.text = "LIMIT  " + model.LimitationText;
            packageMarker.SetActive(snapshot.PackageLot != null);
            cargoMarker.SetActive(snapshot.Cargo != null);
            cargoVisual.SetActive(true);
            cargoVisual.transform.localScale = cargoScale * (snapshot.Cargo != null ? 1f
                : snapshot.PackageLot != null ? .86f : .72f);
        }
    }
}
