using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    public static class 화물JourneyStageCodes
    {
        public const string HubStored = "hub-stored";
        public const string CityOutbound = "city-outbound";

        public static bool IsKnown(string value)
            => value == HubStored || value == CityOutbound;
    }

    [Serializable]
    public sealed class 화물JourneyPresentationModel
    {
        public string CargoStableId = string.Empty;
        public string OriginRegionCode = string.Empty;
        public string ProductStableId = string.Empty;
        public string CurrentStageCode = string.Empty;
        public bool AcceptedAtHub;
        public bool StoredAtHub;
        public bool OutboundAllocated;
        public string[] SourceStableIds = Array.Empty<string>();

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(CargoStableId)
                || string.IsNullOrWhiteSpace(OriginRegionCode)
                || string.IsNullOrWhiteSpace(ProductStableId)
                || !화물JourneyStageCodes.IsKnown(CurrentStageCode)
                || !AcceptedAtHub
                || !StoredAtHub
                || SourceStableIds == null
                || SourceStableIds.Length < 4
                || SourceStableIds.Any(string.IsNullOrWhiteSpace)
                || SourceStableIds.Distinct(StringComparer.Ordinal).Count()
                    != SourceStableIds.Length
                || !SourceStableIds.Contains(CargoStableId, StringComparer.Ordinal)
                || !SourceStableIds.Contains(ProductStableId, StringComparer.Ordinal))
                return false;

            var hasAllocation = SourceStableIds.Any(value =>
                value.StartsWith("outbound-allocation:", StringComparison.Ordinal));
            return OutboundAllocated
                ? CurrentStageCode == 화물JourneyStageCodes.CityOutbound && hasAllocation
                : CurrentStageCode == 화물JourneyStageCodes.HubStored && !hasAllocation;
        }
    }

    [DisallowMultipleComponent]
    public sealed class 화물PresentationJourneyView : MonoBehaviour
    {
        [SerializeField] private GameObject cargoVisual = null!;
        [SerializeField] private 절차형VehicleRouteFollower? outboundFollower;
        [SerializeField] private string cargoStableId = string.Empty;
        [SerializeField] private string originRegionCode = string.Empty;
        [SerializeField] private string productStableId = string.Empty;
        [SerializeField] private string currentStageCode = string.Empty;
        [SerializeField] private bool acceptedAtHub;
        [SerializeField] private bool storedAtHub;
        [SerializeField] private bool outboundAllocated;
        [SerializeField] private string[] sourceStableIds = Array.Empty<string>();

        public string CargoStableId => cargoStableId;
        public string OriginRegionCode => originRegionCode;
        public string ProductStableId => productStableId;
        public string CurrentStageCode => currentStageCode;
        public bool OutboundAllocated => outboundAllocated;
        public IReadOnlyList<string> SourceStableIds => sourceStableIds;
        public 절차형VehicleRouteFollower? OutboundFollower => outboundFollower;

        public void Configure(GameObject visual, 절차형VehicleRouteFollower? follower)
        {
            cargoVisual = visual;
            outboundFollower = follower;
        }

        public void Apply(화물JourneyPresentationModel model)
        {
            if (model == null || !model.Validate())
                throw new InvalidOperationException("RegionalCargoPresentationInvalid");
            if (!string.IsNullOrWhiteSpace(cargoStableId)
                && cargoStableId != model.CargoStableId)
                throw new InvalidOperationException("RegionalCargoIdentityChanged");
            cargoStableId = model.CargoStableId;
            originRegionCode = model.OriginRegionCode;
            productStableId = model.ProductStableId;
            currentStageCode = model.CurrentStageCode;
            acceptedAtHub = model.AcceptedAtHub;
            storedAtHub = model.StoredAtHub;
            outboundAllocated = model.OutboundAllocated;
            sourceStableIds = model.SourceStableIds.ToArray();
            if (outboundFollower != null)
                outboundFollower.enabled = outboundAllocated;
        }

        public bool ValidateApplied()
        {
            var model = new 화물JourneyPresentationModel
            {
                CargoStableId = cargoStableId,
                OriginRegionCode = originRegionCode,
                ProductStableId = productStableId,
                CurrentStageCode = currentStageCode,
                AcceptedAtHub = acceptedAtHub,
                StoredAtHub = storedAtHub,
                OutboundAllocated = outboundAllocated,
                SourceStableIds = sourceStableIds,
            };
            return cargoVisual != null
                   && model.Validate()
                   && (outboundAllocated
                       ? outboundFollower != null
                         && outboundFollower.enabled
                         && outboundFollower.ValidateWiring()
                       : outboundFollower == null || !outboundFollower.enabled);
        }
    }
}
