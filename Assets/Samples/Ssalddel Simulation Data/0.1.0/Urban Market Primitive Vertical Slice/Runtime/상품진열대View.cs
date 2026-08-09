using System;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 상품진열대View : MonoBehaviour
    {
        [SerializeField]
        private 가격표View priceTag = null!;

        [SerializeField]
        private 재고상태View stockStatus = null!;

        [SerializeField]
        private 상품상자View[] productBoxes = Array.Empty<상품상자View>();

        [SerializeField]
        private InteractionSocket selectionSocket = null!;

        private 도심마트상품ScreenModel? current;
        private Action<도심마트상품ScreenModel>? onSelected;

        public void Configure(
            가격표View targetPriceTag,
            재고상태View targetStockStatus,
            상품상자View[] boxes,
            InteractionSocket targetSelectionSocket)
        {
            priceTag = targetPriceTag;
            stockStatus = targetStockStatus;
            productBoxes = boxes;
            selectionSocket = targetSelectionSocket;
        }

        public void Render(
            도심마트상품ScreenModel model,
            Action<도심마트상품ScreenModel> selected)
        {
            if (onSelected != null)
            {
                selectionSocket.Selected -= HandleSelected;
            }

            current = model;
            onSelected = selected;
            priceTag.Render(model);
            stockStatus.Render(model);

            var visibleBoxes = ResolveVisibleBoxCount(model);
            var color = ResolveProductColor(model.상품명);
            for (var index = 0; index < productBoxes.Length; index++)
            {
                productBoxes[index].Render(model.상품명, color, index < visibleBoxes);
            }

            selectionSocket.Selected += HandleSelected;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public bool ValidateWiring()
        {
            if (priceTag == null || stockStatus == null || selectionSocket == null)
            {
                return false;
            }

            foreach (var box in productBoxes)
            {
                if (box == null || !box.ValidateWiring())
                {
                    return false;
                }
            }

            return priceTag.ValidateWiring()
                && stockStatus.ValidateWiring()
                && selectionSocket.ValidateWiring();
        }

        private void HandleSelected()
        {
            if (current != null)
            {
                onSelected?.Invoke(current);
            }
        }

        private void OnDestroy()
        {
            if (selectionSocket != null)
            {
                selectionSocket.Selected -= HandleSelected;
            }
        }

        private static int ResolveVisibleBoxCount(도심마트상품ScreenModel model)
        {
            if (model.재고상태Code == 재고상태Codes.OutOfStock || model.재고수량 == 0)
            {
                return 0;
            }

            if (model.재고상태Code == 재고상태Codes.LowStock)
            {
                return 1;
            }

            return 3;
        }

        private static Color ResolveProductColor(string productName)
        {
            switch (productName)
            {
                case "감자":
                    return new Color(0.62f, 0.43f, 0.24f);
                case "쌀":
                    return new Color(0.92f, 0.89f, 0.72f);
                case "양파":
                    return new Color(0.76f, 0.56f, 0.28f);
                default:
                    return Color.white;
            }
        }
    }
}
