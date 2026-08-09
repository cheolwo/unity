using System;
using Ssalddel.Unity.Farm;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmSoilTileCellView : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private Renderer soilRenderer = null!;
        private Action<string>? selected;
        private string cultivationStateCode = string.Empty;

        public string StableId => stableId;
        public Renderer SoilRenderer => soilRenderer;
        public string CultivationStateCode => cultivationStateCode;

        public void Configure(string id, Renderer rendererValue)
        {
            stableId = id;
            soilRenderer = rendererValue;
        }

        public void BindSelection(Action<string> handler) => selected = handler;

        public void Apply(FarmSoilTilePresentationModel model, Material material)
        {
            if (!string.Equals(model.StableId, stableId, StringComparison.Ordinal))
                throw new InvalidOperationException("FarmSoilTileViewStableIdMismatch");
            gameObject.SetActive(true);
            cultivationStateCode = model.CultivationStateCode;
            soilRenderer.sharedMaterial = material;
            var scale = soilRenderer.transform.localScale;
            var isRow = model.CultivationStateCode
                != FarmSoilTileCultivationStateCodes.Untilled;
            scale.y = isRow ? .34f : .22f;
            scale.z = isRow ? .76f : 1.05f;
            soilRenderer.transform.localScale = scale;
            gameObject.name = "SoilTile [" + model.GridX + "," + model.GridZ + "]";
        }

        public void Select() => selected?.Invoke(stableId);

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId) && soilRenderer != null;

        private void OnMouseDown() => Select();
    }
}
