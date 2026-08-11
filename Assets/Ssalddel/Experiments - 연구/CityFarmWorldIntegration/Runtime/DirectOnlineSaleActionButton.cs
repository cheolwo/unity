using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [RequireComponent(typeof(Button))]
    public sealed class DirectOnlineSaleActionButton : MonoBehaviour
    {
        [SerializeField] private DirectOnlineSaleLifecyclePresenter presenter = null!;
        [SerializeField] private string actionCode = string.Empty;
        public void Configure(DirectOnlineSaleLifecyclePresenter value, string code)
        { presenter = value; actionCode = code; }
        private void Awake() => GetComponent<Button>().onClick.AddListener(Execute);
        public void Execute()
        {
            if (presenter == null || string.IsNullOrWhiteSpace(actionCode))
                throw new InvalidOperationException("DirectOnlineSaleButtonInvalid");
            presenter.ExecuteAction(actionCode);
        }
    }
}
