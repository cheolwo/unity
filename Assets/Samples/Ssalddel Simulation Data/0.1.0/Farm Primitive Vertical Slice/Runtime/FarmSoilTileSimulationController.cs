using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    /// <summary>명시적인 Simulation fixture만 표시하며 operational 실패 fallback으로 사용하지 않습니다.</summary>
    public sealed class FarmSoilTileSimulationController : MonoBehaviour
    {
        [SerializeField] private FarmSoilTileGridView gridView = null!;
        private FarmSoilTileSimulationDataSnapshot snapshot = null!;
        private FarmSoilTileMapProjector projector = null!;
        private FarmSoilTileTillingSimulationEngine tilling = null!;
        private string? selectedTileStableId;
        private FarmSoilTileTillingPreview? preview;
        private FarmSoilTileTillingCommand? confirmedCommand;

        public FarmSoilTileSimulationDataSnapshot CurrentSnapshot => snapshot;
        public FarmSoilTileTillingPreview? CurrentPreview => preview;
        public FarmSoilTileTillingCommand? ConfirmedCommand => confirmedCommand;

        public void Configure(FarmSoilTileGridView view) => gridView = view;

        private void Start() => Initialize();

        public void Initialize()
        {
            snapshot = FarmPotatoSoilTileSimulationFixture.Create();
            projector = new FarmSoilTileMapProjector(new FarmSoilTileSimulationValidator());
            tilling = new FarmSoilTileTillingSimulationEngine(new FarmSoilTileSimulationValidator());
            gridView.TileSelected -= Select;
            gridView.TileSelected += Select;
            gridView.TillingPreviewRequested -= PreviewSelectedTilling;
            gridView.TillingPreviewRequested += PreviewSelectedTilling;
            gridView.TillingConfirmRequested -= ConfirmSelectedTilling;
            gridView.TillingConfirmRequested += ConfirmSelectedTilling;
            gridView.SimulationTickRequested -= TickConfirmedTilling;
            gridView.SimulationTickRequested += TickConfirmedTilling;
            selectedTileStableId = null;
            preview = null;
            confirmedCommand = null;
            gridView.Apply(projector.Project(snapshot));
        }

        public void Select(string stableId)
        {
            selectedTileStableId = stableId;
            preview = null;
            confirmedCommand = null;
            Present();
        }

        public void PreviewSelectedTilling()
        {
            if (selectedTileStableId == null)
                throw new System.InvalidOperationException("FarmSoilTileTillingSelectionRequired");
            preview = tilling.Preview(snapshot, selectedTileStableId);
            confirmedCommand = null;
            Present();
        }

        public void ConfirmSelectedTilling()
        {
            if (preview == null)
                throw new System.InvalidOperationException("FarmSoilTileTillingPreviewRequired");
            confirmedCommand = tilling.Confirm(snapshot, preview);
            Present();
        }

        public void TickConfirmedTilling()
        {
            if (confirmedCommand == null)
                throw new System.InvalidOperationException("FarmSoilTileTillingConfirmationRequired");
            snapshot = tilling.Tick(snapshot, confirmedCommand);
            preview = null;
            confirmedCommand = null;
            Present();
        }

        private void Present()
            => gridView.Apply(projector.Project(
                snapshot,
                selectedTileStableId,
                preview,
                confirmedCommand));

        private void OnDestroy()
        {
            if (gridView == null) return;
            gridView.TileSelected -= Select;
            gridView.TillingPreviewRequested -= PreviewSelectedTilling;
            gridView.TillingConfirmRequested -= ConfirmSelectedTilling;
            gridView.SimulationTickRequested -= TickConfirmedTilling;
        }
    }
}
