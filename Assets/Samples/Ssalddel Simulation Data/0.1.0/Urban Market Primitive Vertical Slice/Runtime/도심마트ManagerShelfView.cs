using System;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트ManagerShelfView : MonoBehaviour
    {
        [SerializeField] private string presentationStableId = string.Empty;
        [SerializeField] private Renderer shelfRenderer = null!;
        [SerializeField] private TextMesh quantityText = null!;
        [SerializeField] private GameObject[] displayBoxes = Array.Empty<GameObject>();
        [SerializeField] private InteractionSocket selectionSocket = null!;

        private WorldStableId shelfWorldId;
        private Action<WorldStableId>? selected;

        public string PresentationStableId => presentationStableId;

        public void Configure(
            string stableId,
            Renderer renderer,
            TextMesh text,
            GameObject[] boxes,
            InteractionSocket socket)
        {
            presentationStableId = stableId?.Trim() ?? string.Empty;
            shelfRenderer = renderer;
            quantityText = text;
            displayBoxes = boxes ?? Array.Empty<GameObject>();
            selectionSocket = socket;
        }

        public void Apply(도심마트ShelfSurfaceItem surface, Action<WorldStableId> onSelected)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (!string.Equals(presentationStableId, surface.StableId.Value, StringComparison.Ordinal))
                throw new InvalidOperationException("UrbanMarketManagerShelfSurfaceMismatch:" + surface.StableId.Value);
            shelfWorldId = surface.ShelfWorldId;
            selected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
            quantityText.text = surface.QuantityText;
            ApplyColor(shelfRenderer, ColorToken(surface.ColorCode, surface.IsHighlighted));
            for (var index = 0; index < displayBoxes.Length; index++)
                displayBoxes[index].SetActive(index < surface.DisplayBoxCount);
            selectionSocket.Selected -= HandleSelected;
            selectionSocket.Selected += HandleSelected;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(presentationStableId)
               && shelfRenderer != null
               && quantityText != null
               && selectionSocket != null
               && selectionSocket.ValidateWiring()
               && displayBoxes != null;

        private void HandleSelected()
        {
            if (shelfWorldId.IsDefined) selected?.Invoke(shelfWorldId);
        }

        private void OnDestroy()
        {
            if (selectionSocket != null) selectionSocket.Selected -= HandleSelected;
        }

        private static Color ColorToken(string code, bool highlighted)
        {
            Color color;
            switch (code)
            {
                case "Red": color = new Color(0.78f, 0.18f, 0.18f); break;
                case "Orange": color = new Color(0.95f, 0.45f, 0.12f); break;
                case "Yellow": color = new Color(0.95f, 0.72f, 0.12f); break;
                case "Blue": color = new Color(0.18f, 0.42f, 0.78f); break;
                case "Green": color = new Color(0.18f, 0.68f, 0.32f); break;
                default: color = Color.gray; break;
            }
            return highlighted ? Color.Lerp(color, Color.white, 0.35f) : color;
        }

        private static void ApplyColor(Renderer target, Color color)
        {
            var block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            var material = target.sharedMaterial;
            if (material != null && material.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", color);
            else
                block.SetColor("_Color", color);
            target.SetPropertyBlock(block);
        }
    }
}
