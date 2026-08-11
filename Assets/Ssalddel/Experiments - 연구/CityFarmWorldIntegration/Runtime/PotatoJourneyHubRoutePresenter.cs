using Ssalddel.Unity.PotatoJourney;
using Ssalddel.Unity.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class PotatoJourneyHubRoutePresenter : MonoBehaviour
    {
        [SerializeField] private 절차형VehicleRouteFollower routeFollower = null!;
        [SerializeField] private PotatoHarvestCargoLifecyclePresenter cargoLifecycle = null!;
        [SerializeField] private GameObject routeVisual = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text stateText = null!;
        [SerializeField] private Text boundaryText = null!;

        private PotatoJourneyHubRoutePresentationModel? currentModel;

        public PotatoJourneyHubRoutePresentationModel? CurrentModel => currentModel;
        public 절차형VehicleRouteFollower RouteFollower => routeFollower;

        public void Configure(
            절차형VehicleRouteFollower follower,
            PotatoHarvestCargoLifecyclePresenter configuredCargoLifecycle,
            GameObject configuredRouteVisual,
            Text configuredTitle,
            Text configuredState,
            Text configuredBoundary)
        {
            routeFollower = follower;
            cargoLifecycle = configuredCargoLifecycle;
            routeVisual = configuredRouteVisual;
            titleText = configuredTitle;
            stateText = configuredState;
            boundaryText = configuredBoundary;
            ApplyProjection();
        }

        private void Start()
        {
            if (ValidateWiring()) ApplyProjection();
        }

        public void ApplyProjection()
        {
            cargoLifecycle.RunGoldenPath();
            currentModel = new PotatoHarvestCargoHubRouteAdapter(
                new 감자수확CargoSimulationValidator()).Project(cargoLifecycle.CurrentSnapshot);
            routeFollower.gameObject.SetActive(currentModel.IsVisible);
            routeVisual.SetActive(currentModel.IsVisible);
            titleText.text = "POTATO CARGO · FARM → HUB";
            stateText.text = currentModel.ModeLabel + " ROUTE · CARGO " + currentModel.HandoffStateCode
                             + "\n" + currentModel.CargoStableId
                             + " · " + currentModel.PackageCount + " BOX · "
                             + currentModel.Quantity + currentModel.UnitCode;
            boundaryText.text = "HARVEST → PACKAGE → CARGO IDENTITY LINKED\n"
                                + currentModel.HarvestLotStableId + " → " + currentModel.PackageLotStableId
                                + "\nVan motion is Presentation only. No dispatch or receiving.";
        }

        public bool ValidateWiring()
            => cargoLifecycle != null && routeFollower != null && routeFollower.ValidateWiring()
               && routeVisual != null && titleText != null && stateText != null && boundaryText != null;
    }
}
