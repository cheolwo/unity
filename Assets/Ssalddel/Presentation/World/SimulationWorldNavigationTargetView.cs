using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class SimulationWorldNavigationTargetView : MonoBehaviour
    {
        [SerializeField] private string observationScaleCode = string.Empty;
        [SerializeField] private string settlementStableId = string.Empty;
        [SerializeField] private string districtStableId = string.Empty;
        [SerializeField] private string objectStableId = string.Empty;
        [SerializeField] private string focusAnchorId = string.Empty;
        [SerializeField] private Renderer selectionRenderer = null!;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new(1f, .78f, .18f, 1f);

        private MaterialPropertyBlock propertyBlock = null!;

        public string ObservationScaleCode => observationScaleCode;
        public string SettlementStableId => settlementStableId;
        public string DistrictStableId => districtStableId;
        public string ObjectStableId => objectStableId;
        public string FocusAnchorId => focusAnchorId;

        public void Configure(
            string scaleCode,
            string settlementId,
            string districtId,
            string objectId,
            string anchorId,
            Renderer renderer,
            Color baseColor,
            Color highlightColor)
        {
            observationScaleCode = scaleCode;
            settlementStableId = settlementId;
            districtStableId = districtId;
            objectStableId = objectId;
            focusAnchorId = anchorId;
            selectionRenderer = renderer;
            normalColor = baseColor;
            selectedColor = highlightColor;
            propertyBlock = new MaterialPropertyBlock();
            ApplySelected(false);
        }

        public void Validate()
        {
            if (!SimulationObservationScaleCodes.IsKnown(observationScaleCode)
                || observationScaleCode == SimulationObservationScaleCodes.WorldMap)
                throw new InvalidOperationException("SimulationNavigationTargetScaleInvalid");
            if (string.IsNullOrWhiteSpace(settlementStableId)
                || string.IsNullOrWhiteSpace(focusAnchorId)
                || selectionRenderer == null)
                throw new InvalidOperationException("SimulationNavigationTargetWiringMissing");
            if ((observationScaleCode == SimulationObservationScaleCodes.District
                    || observationScaleCode == SimulationObservationScaleCodes.Object)
                && string.IsNullOrWhiteSpace(districtStableId))
                throw new InvalidOperationException("SimulationNavigationTargetDistrictMissing");
            if (observationScaleCode == SimulationObservationScaleCodes.Object
                && string.IsNullOrWhiteSpace(objectStableId))
                throw new InvalidOperationException("SimulationNavigationTargetObjectMissing");
            if (GetComponent<Collider>() == null)
                throw new InvalidOperationException("SimulationNavigationTargetColliderMissing");
        }

        public void ApplySelected(bool selected)
        {
            if (selectionRenderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            selectionRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", selected ? selectedColor : normalColor);
            propertyBlock.SetColor("_Color", selected ? selectedColor : normalColor);
            selectionRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            var presenter = FindFirstObjectByType<SimulationWorldShellPresenter>();
            if (presenter == null) return;
            if (observationScaleCode == SimulationObservationScaleCodes.Object)
                presenter.SelectObjectForInteraction(this);
            else
                presenter.NavigateTo(this);
        }
    }
}
