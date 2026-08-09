using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmTileView : MonoBehaviour, IFarmPlotTarget
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private Renderer tileRenderer = null!;
        [SerializeField] private TextMesh label = null!;

        public string StableId => stableId;

        public void Configure(string id, Renderer rendererValue, TextMesh labelValue)
        {
            stableId = id;
            tileRenderer = rendererValue;
            label = labelValue;
        }

        public void Apply(FarmPlotSnapshot plot)
        {
            gameObject.SetActive(true);
            label.text = plot.PlotName + "\n" + (plot.SoilManagementProfileCode ?? "토양 관리정보 없음");
        }

        public void Hide() => gameObject.SetActive(false);

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId) && tileRenderer != null && label != null;
    }
}
