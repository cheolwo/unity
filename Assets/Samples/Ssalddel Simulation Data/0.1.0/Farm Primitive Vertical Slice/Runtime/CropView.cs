using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class CropView : MonoBehaviour, IFarmCultivationTarget
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private Renderer cropRenderer = null!;
        [SerializeField] private TextMesh label = null!;

        public string StableId => stableId;

        public void Configure(string id, Renderer rendererValue, TextMesh labelValue)
        {
            stableId = id;
            cropRenderer = rendererValue;
            label = labelValue;
        }

        public void Apply(FarmCultivationSnapshot cultivation)
        {
            gameObject.SetActive(true);
            label.text = cultivation.CropName + " · " + cultivation.GrowthStatusCode
                + "\n기준 " + (cultivation.CropReferenceStableId ?? "연결 없음");
        }

        public void Hide() => gameObject.SetActive(false);

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId) && cropRenderer != null && label != null;
    }
}
