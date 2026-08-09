using System;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public enum FarmSoilTileActionCode
    {
        Preview,
        Confirm,
        SimulationTick,
    }

    /// <summary>클릭을 Grid View 요청으로 전달할 뿐 Simulation 상태를 직접 변경하지 않습니다.</summary>
    public sealed class FarmSoilTileActionButtonView : MonoBehaviour
    {
        [SerializeField] private FarmSoilTileGridView gridView = null!;
        [SerializeField] private FarmSoilTileActionCode actionCode;

        public void Configure(FarmSoilTileGridView view, FarmSoilTileActionCode action)
        {
            gridView = view;
            actionCode = action;
        }

        public void Invoke()
        {
            if (gridView == null)
                throw new InvalidOperationException("FarmSoilTileActionButtonWiringInvalid");
            switch (actionCode)
            {
                case FarmSoilTileActionCode.Preview:
                    gridView.RequestTillingPreview();
                    break;
                case FarmSoilTileActionCode.Confirm:
                    gridView.RequestTillingConfirm();
                    break;
                case FarmSoilTileActionCode.SimulationTick:
                    gridView.RequestSimulationTick();
                    break;
                default:
                    throw new InvalidOperationException("FarmSoilTileActionUnknown:" + actionCode);
            }
        }

        private void OnMouseDown() => Invoke();
    }
}
