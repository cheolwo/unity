using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.PresentationContracts.Cargo;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [Serializable]
    public sealed class CargoJourneyStateMaterialBinding
    {
        public string StateCode = string.Empty;
        public Material Material = null!;
    }

    [DisallowMultipleComponent]
    public sealed class CargoJourneyView : MonoBehaviour
    {
        [SerializeField] private CargoJourneyAnchorView[] anchors =
            Array.Empty<CargoJourneyAnchorView>();
        [SerializeField] private CargoJourneyStateMaterialBinding[] stateMaterials =
            Array.Empty<CargoJourneyStateMaterialBinding>();
        [SerializeField] private TextMesh summaryText = null!;
        [SerializeField] private TextMesh lineageText = null!;
        [SerializeField] private string cargoStableId = string.Empty;
        [SerializeField] private string handoffStateCode = string.Empty;
        [SerializeField] private string currentZoneCode = string.Empty;
        [SerializeField] private long sourceRevision = -1;
        [SerializeField] private string[] sourceStableIds = Array.Empty<string>();

        public string CargoStableId => cargoStableId;
        public string HandoffStateCode => handoffStateCode;
        public string CurrentZoneCode => currentZoneCode;
        public long SourceRevision => sourceRevision;
        public IReadOnlyList<string> SourceStableIds => sourceStableIds;
        public int AnchorCount => anchors?.Length ?? 0;

        public void Configure(
            CargoJourneyAnchorView[] anchorViews,
            CargoJourneyStateMaterialBinding[] materials,
            TextMesh summary,
            TextMesh lineage)
        {
            anchors = anchorViews ?? Array.Empty<CargoJourneyAnchorView>();
            stateMaterials = materials ?? Array.Empty<CargoJourneyStateMaterialBinding>();
            summaryText = summary;
            lineageText = lineage;
        }

        public bool Apply(CargoJourneyPresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (!ValidateWiring()) throw new InvalidOperationException("CargoJourneyViewWiringInvalid");
            if (model.SourceRevision < sourceRevision) return false;
            if (!string.IsNullOrWhiteSpace(cargoStableId)
                && !string.Equals(cargoStableId, model.CargoStableId, StringComparison.Ordinal))
                throw new InvalidOperationException("CargoJourneyIdentityChanged:" + model.CargoStableId);
            if (model.Identity == null
                || !string.Equals(model.Identity.WorldId.Value, model.CargoStableId, StringComparison.Ordinal))
                throw new InvalidOperationException("CargoJourneyIdentityMismatch");

            var incoming = model.Anchors.ToDictionary(value => value.ZoneCode, StringComparer.Ordinal);
            foreach (var anchor in anchors)
            {
                if (!incoming.TryGetValue(anchor.ZoneCode, out var presentation))
                    throw new InvalidOperationException("CargoJourneyAnchorMissing:" + anchor.ZoneCode);
                anchor.Apply(presentation, MaterialFor(presentation.StateCode));
            }

            cargoStableId = model.CargoStableId;
            handoffStateCode = model.HandoffStateCode;
            currentZoneCode = model.CurrentZoneCode;
            sourceRevision = model.SourceRevision;
            sourceStableIds = model.Identity.SourceIds.Select(value => value.Value).ToArray();
            summaryText.text = "CARGO JOURNEY | " + cargoStableId
                + "\n" + handoffStateCode + " | CURRENT " + currentZoneCode;
            lineageText.text = "LINEAGE\n" + string.Join("\n", sourceStableIds);
            return true;
        }

        public bool ValidateWiring()
        {
            var zones = new HashSet<string>(new[]
            {
                CargoJourneyZoneCodes.FarmYard,
                CargoJourneyZoneCodes.TransportCorridor,
                CargoJourneyZoneCodes.UrbanLogistics,
                CargoJourneyZoneCodes.UrbanMarket,
            }, StringComparer.Ordinal);
            var states = new HashSet<string>(new[]
            {
                CargoJourneyAnchorStateCodes.Previous,
                CargoJourneyAnchorStateCodes.Current,
                CargoJourneyAnchorStateCodes.Next,
                CargoJourneyAnchorStateCodes.Planned,
            }, StringComparer.Ordinal);
            return anchors != null && anchors.Length == zones.Count
                && anchors.All(value => value != null && value.ValidateSocket()
                    && zones.Remove(value.ZoneCode))
                && zones.Count == 0
                && stateMaterials != null && stateMaterials.Length == states.Count
                && stateMaterials.All(value => value != null && value.Material != null
                    && states.Remove(value.StateCode))
                && states.Count == 0
                && summaryText != null && lineageText != null;
        }

        public bool ValidateApplied()
            => ValidateWiring()
               && anchors.All(value => value.ValidateApplied()
                   && string.Equals(value.CargoStableId, cargoStableId, StringComparison.Ordinal))
               && !string.IsNullOrWhiteSpace(cargoStableId)
               && !string.IsNullOrWhiteSpace(handoffStateCode)
               && !string.IsNullOrWhiteSpace(currentZoneCode)
               && sourceRevision >= 0 && sourceStableIds.Length > 0;

        private Material MaterialFor(string stateCode)
            => stateMaterials.SingleOrDefault(value => value.StateCode == stateCode)?.Material
               ?? throw new InvalidOperationException("CargoJourneyStateMaterialMissing:" + stateCode);
    }
}
