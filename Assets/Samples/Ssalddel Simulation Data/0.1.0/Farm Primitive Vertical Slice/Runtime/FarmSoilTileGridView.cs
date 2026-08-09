using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    [Serializable]
    public sealed class FarmSoilTileMaterialBinding
    {
        public string ColorToken = string.Empty;
        public Material Material = null!;
    }

    public sealed class FarmSoilTileGridView : MonoBehaviour
    {
        [SerializeField] private FarmSoilTileCellView[] cells =
            Array.Empty<FarmSoilTileCellView>();
        [SerializeField] private FarmSoilTileMaterialBinding[] materials =
            Array.Empty<FarmSoilTileMaterialBinding>();
        [SerializeField] private TextMesh modeLabel = null!;
        [SerializeField] private TextMesh selectedTitle = null!;
        [SerializeField] private TextMesh selectedDetail = null!;
        [SerializeField] private TextMesh tillingActionStatus = null!;

        public event Action<string>? TileSelected;
        public event Action? TillingPreviewRequested;
        public event Action? TillingConfirmRequested;
        public event Action? SimulationTickRequested;

        public int CellCount => cells?.Length ?? 0;
        public string SelectedTitleText => selectedTitle == null ? string.Empty : selectedTitle.text;
        public string SelectedDetailText => selectedDetail == null ? string.Empty : selectedDetail.text;
        public string TillingActionStatusText => tillingActionStatus == null
            ? string.Empty
            : tillingActionStatus.text;

        public void Configure(
            FarmSoilTileCellView[] tileCells,
            FarmSoilTileMaterialBinding[] materialBindings,
            TextMesh mode,
            TextMesh title,
            TextMesh detail,
            TextMesh? actionStatus = null)
        {
            cells = tileCells ?? Array.Empty<FarmSoilTileCellView>();
            materials = materialBindings ?? Array.Empty<FarmSoilTileMaterialBinding>();
            modeLabel = mode;
            selectedTitle = title;
            selectedDetail = detail;
            tillingActionStatus = actionStatus!;
            RebindSelection();
        }

        public void Apply(FarmSoilTileMapPresentationModel model)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("FarmSoilTileGridViewWiringInvalid");
            var incoming = model.Tiles.ToDictionary(value => value.StableId, StringComparer.Ordinal);
            if (incoming.Count != cells.Length)
                throw new InvalidOperationException("FarmSoilTilePresentationCountMismatch");

            foreach (var cell in cells)
            {
                if (!incoming.TryGetValue(cell.StableId, out var presentation))
                    throw new InvalidOperationException("FarmSoilTilePresentationMissing:" + cell.StableId);
                cell.Apply(presentation, MaterialFor(presentation.ColorToken));
            }

            modeLabel.text = "SOIL TILE MAP · " + model.ModeCode
                + " · REV " + model.SourceRevision
                + "\nRULE " + model.RuleRevision;
            selectedTitle.text = model.SelectedTileTitleText;
            selectedDetail.text = model.SelectedTileDetailText;
            if (tillingActionStatus != null)
            {
                tillingActionStatus.text = model.SelectedTileStableId == null
                    ? "SELECT TILE"
                    : model.CanPreviewTilling
                        ? "1  PREVIEW"
                        : model.RequiresExplicitTillingConfirmation
                            ? "2  CONFIRM"
                            : model.HasConfirmedTillingCommand
                                ? "3  SIMULATION TICK"
                                : "TILLING APPLIED";
            }
        }

        public void RequestTillingPreview() => TillingPreviewRequested?.Invoke();

        public void RequestTillingConfirm() => TillingConfirmRequested?.Invoke();

        public void RequestSimulationTick() => SimulationTickRequested?.Invoke();

        public void SelectTileForTests(string stableId)
        {
            var cell = cells.SingleOrDefault(value => value.StableId == stableId)
                ?? throw new InvalidOperationException("FarmSoilTileCellMissing:" + stableId);
            cell.Select();
        }

        public void RebindSelection()
        {
            if (cells == null) return;
            foreach (var cell in cells)
                if (cell != null) cell.BindSelection(OnTileSelected);
        }

        public bool ValidateWiring()
        {
            if (cells == null || cells.Length == 0 || materials == null
                || modeLabel == null || selectedTitle == null || selectedDetail == null)
                return false;
            var expectedTokens = new HashSet<string>(new[]
            {
                FarmSoilTileColorTokens.Untilled,
                FarmSoilTileColorTokens.Tilled,
                FarmSoilTileColorTokens.Sown,
                FarmSoilTileColorTokens.Selected,
            }, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            return cells.All(value => value != null && value.ValidateWiring()
                    && ids.Add(value.StableId))
                && materials.Length == expectedTokens.Count
                && materials.All(value => value != null && value.Material != null
                    && expectedTokens.Remove(value.ColorToken))
                && expectedTokens.Count == 0;
        }

        private Material MaterialFor(string token)
            => materials.SingleOrDefault(value => value.ColorToken == token)?.Material
                ?? throw new InvalidOperationException("FarmSoilTileMaterialMissing:" + token);

        private void OnTileSelected(string stableId) => TileSelected?.Invoke(stableId);

        private void OnEnable()
        {
            RebindSelection();
        }
    }
}
