using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 공급망PresentationZoneCodes
    {
        public const string FarmProduction = "farm-production";
        public const string FarmYard = "farm-yard";
        public const string TransportCorridor = "transport-corridor";
        public const string UrbanLogistics = "urban-logistics";
        public const string UrbanMarket = "urban-market";
        public const string ResidentialCommunity = "residential-community";

        public static bool IsKnown(string value)
            => value == FarmProduction
                || value == FarmYard
                || value == TransportCorridor
                || value == UrbanLogistics
                || value == UrbanMarket
                || value == ResidentialCommunity;
    }

    public sealed class 공급망WorldZoneDefinition
    {
        public string StableId { get; set; } = string.Empty;
        public string PresentationZoneCode { get; set; } = string.Empty;
        public string CanonicalWorldZoneCode { get; set; } = string.Empty;
        public string FocusAnchorId { get; set; } = string.Empty;
        public int FlowOrder { get; set; }
        public DioramaPoint Center { get; set; }
    }

    public sealed class 공급망WorldRouteLegDefinition
    {
        public string StableId { get; set; } = string.Empty;
        public string FromZoneStableId { get; set; } = string.Empty;
        public string ToZoneStableId { get; set; } = string.Empty;
        public int FlowOrder { get; set; }
    }

    /// <summary>
    /// Presentation layout only. Route arrival does not mutate Simulation or Operational state.
    /// </summary>
    public sealed class 공급망WorldLayoutDefinition
    {
        public string StableId { get; set; } = string.Empty;
        public 공급망WorldZoneDefinition[] Zones { get; set; } =
            Array.Empty<공급망WorldZoneDefinition>();
        public 공급망WorldRouteLegDefinition[] RouteLegs { get; set; } =
            Array.Empty<공급망WorldRouteLegDefinition>();

        public void Validate()
        {
            if (!StableDataId.IsValid(StableId) || Zones == null || Zones.Length < 2
                || RouteLegs == null || RouteLegs.Length != Zones.Length - 1)
            {
                throw new InvalidOperationException("SupplyChainWorldLayoutInvalid");
            }

            RequireUnique(Zones.Select(value => value.StableId), "SupplyChainWorldZoneStableIdDuplicate");
            RequireUnique(Zones.Select(value => value.PresentationZoneCode), "SupplyChainWorldPresentationZoneDuplicate");
            RequireUnique(Zones.Select(value => value.FocusAnchorId), "SupplyChainWorldFocusAnchorDuplicate");
            RequireUnique(Zones.Select(value => value.FlowOrder.ToString()), "SupplyChainWorldFlowOrderDuplicate");

            var orderedZones = Zones.OrderBy(value => value.FlowOrder).ToArray();
            for (var index = 0; index < orderedZones.Length; index++)
            {
                var zone = orderedZones[index];
                if (zone == null || !StableDataId.IsValid(zone.StableId)
                    || !공급망PresentationZoneCodes.IsKnown(zone.PresentationZoneCode)
                    || !IsKnownCanonicalZone(zone.CanonicalWorldZoneCode)
                    || string.IsNullOrWhiteSpace(zone.FocusAnchorId)
                    || zone.FlowOrder != index
                    || zone.CanonicalWorldZoneCode != CanonicalZoneFor(zone.PresentationZoneCode))
                {
                    throw new InvalidOperationException("SupplyChainWorldZoneInvalid");
                }
            }

            RequireUnique(RouteLegs.Select(value => value.StableId), "SupplyChainWorldRouteStableIdDuplicate");
            RequireUnique(RouteLegs.Select(value => value.FlowOrder.ToString()), "SupplyChainWorldRouteOrderDuplicate");
            var orderedRoutes = RouteLegs.OrderBy(value => value.FlowOrder).ToArray();
            for (var index = 0; index < orderedRoutes.Length; index++)
            {
                var route = orderedRoutes[index];
                if (route == null || !StableDataId.IsValid(route.StableId)
                    || route.FlowOrder != index
                    || route.FromZoneStableId != orderedZones[index].StableId
                    || route.ToZoneStableId != orderedZones[index + 1].StableId)
                {
                    throw new InvalidOperationException("SupplyChainWorldRouteDisconnected");
                }
            }
        }

        private static string CanonicalZoneFor(string presentationZoneCode)
            => presentationZoneCode switch
            {
                공급망PresentationZoneCodes.FarmProduction => WorldZoneCodes.Farm,
                공급망PresentationZoneCodes.FarmYard => WorldZoneCodes.Farm,
                공급망PresentationZoneCodes.TransportCorridor => WorldZoneCodes.TransportNetwork,
                공급망PresentationZoneCodes.UrbanLogistics => WorldZoneCodes.UrbanLogisticsCenter,
                공급망PresentationZoneCodes.UrbanMarket => WorldZoneCodes.MarketOrder,
                공급망PresentationZoneCodes.ResidentialCommunity => WorldZoneCodes.ResidentialCommunity,
                _ => string.Empty,
            };

        private static bool IsKnownCanonicalZone(string value)
            => value == WorldZoneCodes.Farm
                || value == WorldZoneCodes.TransportNetwork
                || value == WorldZoneCodes.UrbanLogisticsCenter
                || value == WorldZoneCodes.MarketOrder
                || value == WorldZoneCodes.ResidentialCommunity;

        private static void RequireUnique(IEnumerable<string> values, string error)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !known.Add(value))
                    throw new InvalidOperationException(error);
            }
        }
    }

    public static class 공급망WorldLayoutFixture
    {
        public static 공급망WorldLayoutDefinition Create()
        {
            var zones = new[]
            {
                Zone("presentation-zone:farm-production", 공급망PresentationZoneCodes.FarmProduction,
                    WorldZoneCodes.Farm, 0, -36f, 0f, 22f),
                Zone("presentation-zone:farm-yard", 공급망PresentationZoneCodes.FarmYard,
                    WorldZoneCodes.Farm, 1, -22f, 0f, 14f),
                Zone("presentation-zone:transport-corridor", 공급망PresentationZoneCodes.TransportCorridor,
                    WorldZoneCodes.TransportNetwork, 2, -8f, 0f, 6f),
                Zone("presentation-zone:urban-logistics", 공급망PresentationZoneCodes.UrbanLogistics,
                    WorldZoneCodes.UrbanLogisticsCenter, 3, 8f, 0f, -2f),
                Zone("presentation-zone:urban-market", 공급망PresentationZoneCodes.UrbanMarket,
                    WorldZoneCodes.MarketOrder, 4, 25f, 0f, -12f),
                Zone("presentation-zone:residential-community", 공급망PresentationZoneCodes.ResidentialCommunity,
                    WorldZoneCodes.ResidentialCommunity, 5, 40f, 0f, -22f),
            };
            return new 공급망WorldLayoutDefinition
            {
                StableId = "presentation-world:city-farm-supply-chain",
                Zones = zones,
                RouteLegs = Enumerable.Range(0, zones.Length - 1)
                    .Select(index => new 공급망WorldRouteLegDefinition
                    {
                        StableId = "presentation-route:" + zones[index].PresentationZoneCode
                            + "." + zones[index + 1].PresentationZoneCode,
                        FromZoneStableId = zones[index].StableId,
                        ToZoneStableId = zones[index + 1].StableId,
                        FlowOrder = index,
                    })
                    .ToArray(),
            };
        }

        private static 공급망WorldZoneDefinition Zone(
            string stableId,
            string presentationCode,
            string canonicalCode,
            int order,
            float x,
            float y,
            float z)
            => new()
            {
                StableId = stableId,
                PresentationZoneCode = presentationCode,
                CanonicalWorldZoneCode = canonicalCode,
                FocusAnchorId = "camera-focus:zone." + presentationCode,
                FlowOrder = order,
                Center = new DioramaPoint(x, y, z),
            };
    }
}
