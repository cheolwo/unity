using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class 에셋연구소ActionButton : MonoBehaviour
    {
        [SerializeField] private 에셋연구소Presenter presenter = null!;
        [SerializeField] private string actionCode = string.Empty;

        public void Configure(에셋연구소Presenter owner, string code)
        {
            presenter = owner;
            actionCode = code;
            GetComponent<Button>().onClick.RemoveListener(Execute);
            GetComponent<Button>().onClick.AddListener(Execute);
        }

        private void Execute() => presenter?.Execute(actionCode);
    }
}
