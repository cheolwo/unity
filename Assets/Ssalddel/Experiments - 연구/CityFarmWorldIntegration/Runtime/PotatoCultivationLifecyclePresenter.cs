using System;
using System.Linq;
using Ssalddel.Unity.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class PotatoCultivationLifecycleActionCodes
    {
        public const string Reset = "Reset";
        public const string ReviewSowing = "ReviewSowing";
        public const string Confirm = "Confirm";
        public const string ApplyTick = "ApplyTick";
        public const string AdvanceDay = "AdvanceDay";
        public const string AdvanceToReady = "AdvanceToReady";
        public const string ReviewHarvest = "ReviewHarvest";
        public const string FinishHarvest = "FinishHarvest";
    }

    [DefaultExecutionOrder(100)]
    public sealed class PotatoCultivationLifecyclePresenter : MonoBehaviour
    {
        [SerializeField] private Transform[] potatoPlants = Array.Empty<Transform>();
        [SerializeField] private Vector3[] originalPlantScales = Array.Empty<Vector3>();
        [SerializeField] private GameObject harvestedCargoVisual = null!;
        [SerializeField] private GameObject harvestLotMarker = null!;
        [SerializeField] private Text dateAndStageText = null!;
        [SerializeField] private Text calendarText = null!;
        [SerializeField] private Text lineageText = null!;
        [SerializeField] private Text actionStateText = null!;
        [SerializeField] private Text limitationText = null!;
        [SerializeField] private Text readOnlyDataTitleText = null!;
        [SerializeField] private Text readOnlyDataModeText = null!;
        [SerializeField] private bool autoRunGoldenPathOnStart;

        private readonly 재배달력ProfileValidator calendarValidator = new 재배달력ProfileValidator();
        private 감자재배LifecycleSimulationValidator validator = null!;
        private 감자재배LifecycleSimulationEngine engine = null!;
        private 감자재배LifecycleProjector projector = null!;
        private 감자재배LifecycleSimulationSnapshot snapshot = null!;
        private 감자재배LifecyclePreview? preview;
        private 감자재배LifecycleCommand? command;
        private 감자재배LifecyclePresentationModel currentModel = null!;

        public 감자재배LifecycleSimulationSnapshot CurrentSnapshot => snapshot;
        public 감자재배LifecyclePreview? CurrentPreview => preview;
        public 감자재배LifecycleCommand? CurrentCommand => command;
        public 감자재배LifecyclePresentationModel CurrentModel => currentModel;

        public bool ValidateWiring()
            => potatoPlants != null && potatoPlants.Length >= 24
               && originalPlantScales != null && originalPlantScales.Length == potatoPlants.Length
               && potatoPlants.All(value => value != null)
               && harvestedCargoVisual != null && harvestLotMarker != null
               && dateAndStageText != null && calendarText != null && lineageText != null
               && actionStateText != null && limitationText != null
               && readOnlyDataTitleText != null && readOnlyDataModeText != null;

        public void Configure(
            Transform[] plants,
            GameObject cargoVisual,
            GameObject lotMarker,
            Text dateAndStage,
            Text calendar,
            Text lineage,
            Text actionState,
            Text limitation,
            Text readOnlyDataTitle,
            Text readOnlyDataMode,
            bool runGoldenPathOnStart)
        {
            potatoPlants = plants ?? Array.Empty<Transform>();
            originalPlantScales = potatoPlants.Select(value => value.localScale).ToArray();
            harvestedCargoVisual = cargoVisual;
            harvestLotMarker = lotMarker;
            dateAndStageText = dateAndStage;
            calendarText = calendar;
            lineageText = lineage;
            actionStateText = actionState;
            limitationText = limitation;
            readOnlyDataTitleText = readOnlyDataTitle;
            readOnlyDataModeText = readOnlyDataMode;
            autoRunGoldenPathOnStart = runGoldenPathOnStart;
            EnsureServices();
            ResetLifecycle();
        }

        private void Start()
        {
            EnsureServices();
            if (snapshot == null) ResetLifecycle();
            if (autoRunGoldenPathOnStart) RunGoldenPathToHarvest();
        }

        public void ExecuteAction(string actionCode)
        {
            switch (actionCode)
            {
                case PotatoCultivationLifecycleActionCodes.Reset:
                    ResetLifecycle();
                    break;
                case PotatoCultivationLifecycleActionCodes.ReviewSowing:
                    ReviewSowing();
                    break;
                case PotatoCultivationLifecycleActionCodes.Confirm:
                    ConfirmPreview();
                    break;
                case PotatoCultivationLifecycleActionCodes.ApplyTick:
                    ApplyConfirmedTick();
                    break;
                case PotatoCultivationLifecycleActionCodes.AdvanceDay:
                    AdvanceDays(1);
                    break;
                case PotatoCultivationLifecycleActionCodes.AdvanceToReady:
                    AdvanceToHarvestReady();
                    break;
                case PotatoCultivationLifecycleActionCodes.ReviewHarvest:
                    ReviewHarvest();
                    break;
                case PotatoCultivationLifecycleActionCodes.FinishHarvest:
                    FinishHarvest();
                    break;
                default:
                    throw new InvalidOperationException("PotatoCultivationLifecycleActionUnknown:" + actionCode);
            }
        }

        public void ResetLifecycle()
        {
            EnsureServices();
            snapshot = 감자재배LifecycleSimulationFixture.Create();
            preview = null;
            command = null;
            ApplyPresentation();
        }

        public void ReviewSowing()
        {
            var tile = snapshot.Soil.Tiles.First(value =>
                value.CultivationStateCode == FarmSoilTileCultivationStateCodes.Tilled);
            preview = engine.PreviewSowing(snapshot, tile.StableId);
            command = null;
            ApplyPresentation();
        }

        public void ReviewHarvest()
        {
            preview = engine.PreviewHarvest(snapshot);
            command = null;
            ApplyPresentation();
        }

        public void ConfirmPreview()
        {
            if (preview == null)
                throw new InvalidOperationException("PotatoCultivationLifecyclePreviewMissing");
            command = engine.Confirm(snapshot, preview);
            ApplyPresentation();
        }

        public void ApplyConfirmedTick()
        {
            if (command == null)
                throw new InvalidOperationException("PotatoCultivationLifecycleCommandMissing");
            snapshot = engine.Tick(snapshot, command);
            preview = null;
            command = null;
            ApplyPresentation();
        }

        public void AdvanceDays(int days)
        {
            snapshot = engine.Tick(snapshot, engine.CreateAdvanceDaysCommand(snapshot, days));
            preview = null;
            command = null;
            ApplyPresentation();
        }

        public void AdvanceToHarvestReady()
        {
            var cultivation = snapshot.Cultivation
                ?? throw new InvalidOperationException("PotatoCultivationLifecycleCultivationMissing");
            var readyDay = snapshot.SimulationRule.GrowthStages
                .Single(value => value.StageCode == 재배생육단계Codes.HarvestReady)
                .MinimumDaysAfterSowing;
            var remaining = readyDay - cultivation.DaysAfterSowing;
            if (remaining > 0) AdvanceDays(remaining);
        }

        public void FinishHarvest()
        {
            if (preview == null) ReviewHarvest();
            if (command == null) ConfirmPreview();
            ApplyConfirmedTick();
        }

        public void RunGoldenPathToHarvest()
        {
            ResetLifecycle();
            ReviewSowing();
            ConfirmPreview();
            ApplyConfirmedTick();
            AdvanceToHarvestReady();
            ReviewHarvest();
            ConfirmPreview();
            ApplyConfirmedTick();
        }

        private void EnsureServices()
        {
            if (engine != null) return;
            validator = new 감자재배LifecycleSimulationValidator(
                new FarmSoilTileSimulationValidator(), calendarValidator);
            engine = new 감자재배LifecycleSimulationEngine(validator);
            projector = new 감자재배LifecycleProjector(validator);
        }

        private void ApplyPresentation()
        {
            currentModel = projector.Project(snapshot);
            dateAndStageText.text = "GAME DATE  " + currentModel.SimulationDateText
                + "\nSTAGE  " + currentModel.GrowthStageCode
                + "   ·   REV " + currentModel.SourceRevision;
            calendarText.text = "CALENDAR  " + currentModel.CalendarContextText
                + "\nSOURCE  " + currentModel.SourceModeCode;
            lineageText.text = snapshot.Cultivation == null
                ? "LINEAGE  product:potato → cultivation pending"
                : snapshot.HarvestLot == null
                    ? "LINEAGE  product:potato → " + snapshot.Cultivation.StableId
                    : "LINEAGE  product:potato → " + snapshot.Cultivation.StableId
                        + "\nHARVEST LOT  " + currentModel.HarvestLotText;
            actionStateText.text = ActionText();
            limitationText.text = "LIMIT  " + currentModel.LimitationText;
            readOnlyDataTitleText.text = "POTATO IDENTITY · PRICE EVIDENCE";
            readOnlyDataModeText.text = "SERVER PRODUCT DATA · READ ONLY";
            ApplyWorldVisuals(currentModel.GrowthStageCode);
        }

        private string ActionText()
        {
            if (command != null)
                return "CONFIRMED · snapshot unchanged · APPLY TICK required";
            if (preview != null)
                return "PREVIEW · snapshot unchanged · explicit CONFIRM required";
            if (snapshot.HarvestLot != null)
                return "HARVESTED · 300kg Simulation Lot ready for CARGO-1";
            if (snapshot.Cultivation == null)
                return "TILLED · choose SOW REVIEW";
            return snapshot.Cultivation.GrowthStageCode == 재배생육단계Codes.HarvestReady
                ? "HARVEST READY · choose HARVEST REVIEW"
                : "GROWING · advance Simulation date";
        }

        private void ApplyWorldVisuals(string stageCode)
        {
            var harvested = stageCode == 재배생육단계Codes.Harvested;
            var factor = stageCode switch
            {
                "NotStarted" => .12f,
                재배생육단계Codes.Sown => .15f,
                재배생육단계Codes.Emerged => .32f,
                재배생육단계Codes.Vegetative => .62f,
                재배생육단계Codes.Bulking => .86f,
                재배생육단계Codes.HarvestReady => 1.06f,
                재배생육단계Codes.Harvested => 1f,
                _ => .12f,
            };
            for (var index = 0; index < potatoPlants.Length; index++)
            {
                potatoPlants[index].gameObject.SetActive(!harvested);
                potatoPlants[index].localScale = originalPlantScales[index] * factor;
            }
            harvestedCargoVisual.SetActive(harvested);
            harvestLotMarker.SetActive(harvested);
        }
    }
}
