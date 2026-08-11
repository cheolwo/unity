using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public sealed class HarvestDispositionInteractable : MonoBehaviour
    {
        [SerializeField] private HarvestDispositionChoicePresenter presenter = null!;
        public void Configure(HarvestDispositionChoicePresenter value) => presenter = value;
        private void OnMouseDown() => presenter.OpenCard();
        public void Interact() => presenter.OpenCard();
    }
}
