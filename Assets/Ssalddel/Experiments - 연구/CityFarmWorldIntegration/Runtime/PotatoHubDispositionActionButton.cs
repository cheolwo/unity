using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [RequireComponent(typeof(Button))]
    public sealed class PotatoHubDispositionActionButton : MonoBehaviour
    {
        [SerializeField] private PotatoHubDispositionLifecyclePresenter presenter = null!;
        [SerializeField] private string actionCode = string.Empty;

        public void Configure(PotatoHubDispositionLifecyclePresenter value, string code)
        {
            presenter = value;
            actionCode = code;
        }

        private void Awake() => GetComponent<Button>().onClick.AddListener(Execute);

        public void Execute()
        {
            if (presenter == null || string.IsNullOrWhiteSpace(actionCode))
                throw new InvalidOperationException("PotatoHubDispositionButtonInvalid");
            presenter.ExecuteAction(actionCode);
        }
    }
}
