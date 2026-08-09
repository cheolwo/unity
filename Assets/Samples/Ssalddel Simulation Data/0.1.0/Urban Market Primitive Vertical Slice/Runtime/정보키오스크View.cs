using System;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 정보키오스크View : MonoBehaviour
    {
        [SerializeField]
        private TextMesh titleText = null!;

        [SerializeField]
        private TextMesh statusText = null!;

        public void Configure(TextMesh title, TextMesh status)
        {
            titleText = title;
            statusText = status;
        }

        public void Render(도심마트ScreenModel model)
        {
            titleText.text = model.마트명;
            statusText.text = model.SourceTypeCode + "\n기준 "
                + model.GeneratedAt.ToString("yyyy-MM-dd HH:mm zzz");
        }

        public void ShowLoading()
        {
            statusText.text = "Loading...";
        }

        public void ShowError(string message)
        {
            statusText.text = "Error\n" + message;
        }

        public bool ValidateWiring()
        {
            return titleText != null && statusText != null;
        }
    }
}
