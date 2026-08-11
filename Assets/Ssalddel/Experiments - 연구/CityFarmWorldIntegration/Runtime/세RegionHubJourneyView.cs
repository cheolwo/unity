using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [DisallowMultipleComponent]
    public sealed class 세RegionHubJourneyView : MonoBehaviour
    {
        [SerializeField] private 거점CompositionSetView[] regionAndHubAnchors =
            Array.Empty<거점CompositionSetView>();
        [SerializeField] private 도로GateCompositionSetView[] boundaryAndFreightGates =
            Array.Empty<도로GateCompositionSetView>();
        [SerializeField] private 공용ActorRouteFollower[] passengerJourneys =
            Array.Empty<공용ActorRouteFollower>();
        [SerializeField] private 화물PresentationJourneyView[] cargoJourneys =
            Array.Empty<화물PresentationJourneyView>();
        [SerializeField] private 야간MonsterRaidPresenter nightRaidPresenter = null!;

        public IReadOnlyList<거점CompositionSetView> RegionAndHubAnchors => regionAndHubAnchors;
        public IReadOnlyList<도로GateCompositionSetView> BoundaryAndFreightGates => boundaryAndFreightGates;
        public IReadOnlyList<공용ActorRouteFollower> PassengerJourneys => passengerJourneys;
        public IReadOnlyList<화물PresentationJourneyView> CargoJourneys => cargoJourneys;
        public 야간MonsterRaidPresenter NightRaidPresenter => nightRaidPresenter;

        public void Configure(
            거점CompositionSetView[] anchors,
            도로GateCompositionSetView[] gates,
            공용ActorRouteFollower[] passengers,
            화물PresentationJourneyView[] cargo,
            야간MonsterRaidPresenter nightRaid)
        {
            regionAndHubAnchors = anchors ?? Array.Empty<거점CompositionSetView>();
            boundaryAndFreightGates = gates ?? Array.Empty<도로GateCompositionSetView>();
            passengerJourneys = passengers ?? Array.Empty<공용ActorRouteFollower>();
            cargoJourneys = cargo ?? Array.Empty<화물PresentationJourneyView>();
            nightRaidPresenter = nightRaid;
        }

        public bool ValidateWiring()
        {
            var expectedPacks = new[]
            {
                월드CompositionPackCodes.Farm,
                월드CompositionPackCodes.Town,
                월드CompositionPackCodes.City,
                월드CompositionPackCodes.RegionalLogisticsHub,
            };
            return regionAndHubAnchors.Length == 4
                   && regionAndHubAnchors.All(value => value != null && value.ValidateWiring())
                   && regionAndHubAnchors.Select(value => value.Descriptor.PackCode)
                       .OrderBy(value => value, StringComparer.Ordinal)
                       .SequenceEqual(expectedPacks.OrderBy(value => value, StringComparer.Ordinal))
                   && boundaryAndFreightGates.Length == 10
                   && boundaryAndFreightGates.All(value => value != null && value.ValidateWiring())
                   && passengerJourneys.Length == 2
                   && passengerJourneys.All(value => value != null && value.ValidateWiring()
                       && value.AnimationAdapter.RootMotionDisabled)
                   && cargoJourneys.Length == 2
                   && cargoJourneys.All(value => value != null && value.ValidateApplied())
                   && cargoJourneys.Select(value => value.OriginRegionCode)
                       .Distinct(StringComparer.Ordinal).Count() == 2
                   && cargoJourneys.Count(value => value.OutboundAllocated) == 1
                   && nightRaidPresenter != null
                   && nightRaidPresenter.ValidateWiring();
        }
    }
}
