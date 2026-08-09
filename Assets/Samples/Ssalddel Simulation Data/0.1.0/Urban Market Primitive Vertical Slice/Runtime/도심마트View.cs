using System;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트View : MonoBehaviour
    {
        [SerializeField]
        private GameObject buildingVisualRoot = null!;

        [SerializeField]
        private 상품진열대View[] shelves = Array.Empty<상품진열대View>();

        [SerializeField]
        private 정보키오스크View informationKiosk = null!;

        [SerializeField]
        private GameObject detailPanelRoot = null!;

        [SerializeField]
        private TextMesh detailText = null!;

        [SerializeField]
        private InteractionSocket entranceSocket = null!;

        public event Action? EntranceSelected;

        public void Configure(
            GameObject building,
            상품진열대View[] targetShelves,
            정보키오스크View kiosk,
            GameObject panelRoot,
            TextMesh panelText,
            InteractionSocket entrance)
        {
            buildingVisualRoot = building;
            shelves = targetShelves;
            informationKiosk = kiosk;
            detailPanelRoot = panelRoot;
            detailText = panelText;
            entranceSocket = entrance;
            SubscribeEntrance();
        }

        public void ShowLoading()
        {
            informationKiosk.ShowLoading();
            detailPanelRoot.SetActive(false);
            HideShelves();
        }

        public void ShowError(string message)
        {
            informationKiosk.ShowError(message);
            detailPanelRoot.SetActive(false);
            HideShelves();
        }

        public void Render(
            도심마트ScreenModel model,
            Action<도심마트상품ScreenModel> productSelected)
        {
            buildingVisualRoot.SetActive(true);
            informationKiosk.Render(model);

            for (var index = 0; index < shelves.Length; index++)
            {
                if (index < model.상품목록.Length)
                {
                    shelves[index].Render(model.상품목록[index], productSelected);
                }
                else
                {
                    shelves[index].Hide();
                }
            }
        }

        public void OpenProductDetail(도심마트상품ScreenModel product)
        {
            detailText.text = product.상품명 + " " + product.포장표시
                + "\n" + product.가격.ToString("N0") + " " + product.통화Code
                + "\n재고 " + product.재고수량 + product.재고단위
                + "\n" + product.SourceName
                + "\n기준 " + product.EvidenceAsOf.ToString("yyyy-MM-dd HH:mm zzz");
            detailPanelRoot.SetActive(true);
        }

        public bool ValidateWiring()
        {
            if (buildingVisualRoot == null
                || informationKiosk == null
                || detailPanelRoot == null
                || detailText == null
                || entranceSocket == null
                || shelves.Length == 0)
            {
                return false;
            }

            foreach (var shelf in shelves)
            {
                if (shelf == null || !shelf.ValidateWiring())
                {
                    return false;
                }
            }

            return informationKiosk.ValidateWiring() && entranceSocket.ValidateWiring();
        }

        private void HandleEntranceSelected()
        {
            EntranceSelected?.Invoke();
        }

        private void HideShelves()
        {
            foreach (var shelf in shelves)
            {
                if (shelf != null)
                {
                    shelf.Hide();
                }
            }
        }

        private void OnEnable()
        {
            SubscribeEntrance();
        }

        private void OnDisable()
        {
            UnsubscribeEntrance();
        }

        private void SubscribeEntrance()
        {
            if (entranceSocket == null)
            {
                return;
            }

            entranceSocket.Selected -= HandleEntranceSelected;
            entranceSocket.Selected += HandleEntranceSelected;
        }

        private void UnsubscribeEntrance()
        {
            if (entranceSocket != null)
            {
                entranceSocket.Selected -= HandleEntranceSelected;
            }
        }
    }
}
