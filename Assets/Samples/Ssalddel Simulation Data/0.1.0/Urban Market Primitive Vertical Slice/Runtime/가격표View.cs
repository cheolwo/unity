using System;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 가격표View : MonoBehaviour
    {
        [SerializeField]
        private TextMesh productText = null!;

        [SerializeField]
        private TextMesh priceText = null!;

        [SerializeField]
        private TextMesh sourceText = null!;

        public void Configure(TextMesh product, TextMesh price, TextMesh source)
        {
            productText = product;
            priceText = price;
            sourceText = source;
        }

        public void Render(도심마트상품ScreenModel model)
        {
            productText.text = model.상품명 + " " + model.포장표시;
            priceText.text = model.가격.ToString("N0") + " " + model.통화Code;
            sourceText.text = model.SourceName + "\n" + FormatTimestamp(model.EvidenceAsOf);
        }

        public bool ValidateWiring()
        {
            return productText != null && priceText != null && sourceText != null;
        }

        private static string FormatTimestamp(DateTimeOffset value)
        {
            return value.ToString("yyyy-MM-dd HH:mm zzz");
        }
    }
}
