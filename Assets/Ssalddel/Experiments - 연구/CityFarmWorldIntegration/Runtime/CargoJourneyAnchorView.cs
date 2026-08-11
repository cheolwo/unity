using System;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts.Cargo;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    public sealed class CargoJourneyAnchorView : MonoBehaviour
    {
        [SerializeField] private string presentationStableId = string.Empty;
        [SerializeField] private string cargoStableId = string.Empty;
        [SerializeField] private string zoneCode = string.Empty;
        [SerializeField] private string visualRoleCode = string.Empty;
        [SerializeField] private string stateCode = string.Empty;
        [SerializeField] private WorldVisualInstanceView visualInstance = null!;
        [SerializeField] private Renderer stateRenderer = null!;
        [SerializeField] private TextMesh stateLabel = null!;

        public string PresentationStableId => presentationStableId;
        public string CargoStableId => cargoStableId;
        public string ZoneCode => zoneCode;
        public string VisualRoleCode => visualRoleCode;
        public string StateCode => stateCode;
        public WorldVisualInstanceView VisualInstance => visualInstance;

        public void Configure(
            string targetZoneCode,
            string targetVisualRoleCode,
            WorldVisualInstanceView worldVisual,
            Renderer marker,
            TextMesh label)
        {
            zoneCode = targetZoneCode;
            visualRoleCode = targetVisualRoleCode;
            visualInstance = worldVisual;
            stateRenderer = marker;
            stateLabel = label;
        }

        public void Apply(CargoJourneyAnchorPresentationModel model, Material stateMaterial)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (!ValidateSocket())
                throw new InvalidOperationException("CargoJourneyAnchorWiringInvalid:" + zoneCode);
            if (!string.Equals(zoneCode, model.ZoneCode, StringComparison.Ordinal))
                throw new InvalidOperationException("CargoJourneyAnchorZoneMismatch:" + model.ZoneCode);
            if (!string.Equals(visualRoleCode, model.VisualRoleCode, StringComparison.Ordinal))
                throw new InvalidOperationException("CargoJourneyAnchorVisualRoleMismatch:" + model.VisualRoleCode);
            if (stateMaterial == null) throw new ArgumentNullException(nameof(stateMaterial));

            presentationStableId = model.StableId.Value;
            cargoStableId = model.CargoStableId;
            stateCode = model.StateCode;
            stateRenderer.sharedMaterial = stateMaterial;
            stateLabel.text = model.LabelText;
            gameObject.name = "CargoJourneyAnchor [" + zoneCode + "]";
        }

        public bool ValidateSocket()
            => !string.IsNullOrWhiteSpace(zoneCode)
               && !string.IsNullOrWhiteSpace(visualRoleCode)
               && visualInstance != null && visualInstance.ValidateWiring()
               && stateRenderer != null && stateLabel != null;

        public bool ValidateApplied()
            => ValidateSocket()
               && !string.IsNullOrWhiteSpace(presentationStableId)
               && !string.IsNullOrWhiteSpace(cargoStableId)
               && !string.IsNullOrWhiteSpace(stateCode);
    }
}
