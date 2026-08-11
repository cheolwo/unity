using Ssalddel.Unity.PotatoJourney;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class PotatoJourneySelectableAnchor : MonoBehaviour
    {
        [SerializeField] private PotatoJourneyFarmSlicePresenter presenter = null!;
        [SerializeField] private string anchorKindCode = string.Empty;

        public string AnchorKindCode => anchorKindCode;

        public void Configure(PotatoJourneyFarmSlicePresenter configuredPresenter, string configuredAnchorKind)
        {
            presenter = configuredPresenter;
            anchorKindCode = configuredAnchorKind;
        }

        public void Select()
        {
            if (presenter == null) return;
            if (anchorKindCode == PotatoJourneyAnchorKindCodes.FarmPlot) presenter.ApplyFarmSelection();
            else if (anchorKindCode == PotatoJourneyAnchorKindCodes.FarmYardCargo) presenter.ApplyCargoSelection();
        }

        private void OnMouseDown() => Select();
    }
}
