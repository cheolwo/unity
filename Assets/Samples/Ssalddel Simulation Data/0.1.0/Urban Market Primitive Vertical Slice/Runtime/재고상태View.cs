using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 재고상태View : MonoBehaviour
    {
        [SerializeField]
        private Renderer indicatorRenderer = null!;

        [SerializeField]
        private TextMesh stockText = null!;

        public void Configure(Renderer indicator, TextMesh text)
        {
            indicatorRenderer = indicator;
            stockText = text;
        }

        public void Render(도심마트상품ScreenModel model)
        {
            stockText.text = "재고 " + model.재고수량 + model.재고단위;
            indicatorRenderer.material.color = ResolveColor(model.재고상태Code);
        }

        public bool ValidateWiring()
        {
            return indicatorRenderer != null && stockText != null;
        }

        private static Color ResolveColor(string stateCode)
        {
            switch (stateCode)
            {
                case 재고상태Codes.InStock:
                    return new Color(0.18f, 0.68f, 0.32f);
                case 재고상태Codes.LowStock:
                    return new Color(0.95f, 0.65f, 0.12f);
                case 재고상태Codes.OutOfStock:
                    return new Color(0.78f, 0.18f, 0.18f);
                default:
                    return Color.gray;
            }
        }
    }
}
