using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 상품상자View : MonoBehaviour
    {
        [SerializeField]
        private Renderer boxRenderer = null!;

        [SerializeField]
        private TextMesh labelText = null!;

        public void Configure(Renderer targetRenderer, TextMesh targetLabel)
        {
            boxRenderer = targetRenderer;
            labelText = targetLabel;
        }

        public void Render(string label, Color color, bool visible)
        {
            gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            labelText.text = label;
            boxRenderer.material.color = color;
        }

        public bool ValidateWiring()
        {
            return boxRenderer != null && labelText != null;
        }
    }
}
