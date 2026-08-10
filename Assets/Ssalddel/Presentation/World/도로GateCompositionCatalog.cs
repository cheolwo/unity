using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 도로GateCompositionSetNames
    {
        public const string 농촌도로직선 = "농촌도로 직선";
        public const string 농촌도로모서리 = "농촌도로 모서리";
        public const string 농촌도로T자 = "농촌도로 T자";
        public const string 농촌도로십자 = "농촌도로 십자";
        public const string 타운도로직선 = "타운도로 직선";
        public const string 타운도로모서리 = "타운도로 모서리";
        public const string 타운도로T자 = "타운도로 T자";
        public const string 타운도로십자 = "타운도로 십자";
        public const string 도시도로직선 = "도시도로 직선";
        public const string 도시도로모서리 = "도시도로 모서리";
        public const string 도시도로T자 = "도시도로 T자";
        public const string 도시도로십자 = "도시도로 십자";
        public const string 농장타운농장출구 = "농장-타운 농장출구";
        public const string 농장타운타운입구 = "농장-타운 타운입구";
        public const string 타운시티타운출구 = "타운-시티 타운출구";
        public const string 타운시티시티입구 = "타운-시티 시티입구";
        public const string 농장허브농장출구 = "농장-허브 농장출구";
        public const string 농장허브허브입구 = "농장-허브 허브입구";
        public const string 타운허브타운출구 = "타운-허브 타운출구";
        public const string 타운허브허브입구 = "타운-허브 허브입구";
        public const string 허브시티허브출구 = "허브-시티 허브출구";
        public const string 허브시티시티입구 = "허브-시티 시티입구";

        public static IReadOnlyList<string> RoadSets { get; } = new[]
        {
            농촌도로직선,
            농촌도로모서리,
            농촌도로T자,
            농촌도로십자,
            타운도로직선,
            타운도로모서리,
            타운도로T자,
            타운도로십자,
            도시도로직선,
            도시도로모서리,
            도시도로T자,
            도시도로십자,
        };

        public static IReadOnlyList<string> GateSets { get; } = new[]
        {
            농장타운농장출구,
            농장타운타운입구,
            타운시티타운출구,
            타운시티시티입구,
            농장허브농장출구,
            농장허브허브입구,
            타운허브타운출구,
            타운허브허브입구,
            허브시티허브출구,
            허브시티시티입구,
        };

        public static IReadOnlyList<string> All { get; } = RoadSets.Concat(GateSets).ToArray();

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
               && All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class 도로GateCompositionCatalogEntry
    {
        [SerializeField] private 월드CompositionDescriptor descriptor = null!;
        [SerializeField] private GameObject prefab = null!;

        public 월드CompositionDescriptor Descriptor => descriptor;
        public GameObject Prefab => prefab;
        public string CompositionKey => descriptor?.CompositionKey ?? string.Empty;

        public void Configure(월드CompositionDescriptor value, GameObject sourcePrefab)
        {
            descriptor = value;
            prefab = sourcePrefab;
        }

        public bool Validate()
        {
            if (descriptor == null
                || !descriptor.Validate()
                || !도로GateCompositionSetNames.IsKnown(descriptor.SetName)
                || descriptor.VariantCode != 월드CompositionVariantCodes.A
                || prefab == null)
            {
                return false;
            }

            var view = prefab.GetComponent<도로GateCompositionSetView>();
            return view != null
                   && view.Descriptor.CompositionKey == descriptor.CompositionKey
                   && view.ValidateWiring();
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/도로 Gate Composition Catalog")]
    public sealed class 도로GateCompositionCatalog : ScriptableObject
    {
        [SerializeField] private 도로GateCompositionCatalogEntry[] entries =
            Array.Empty<도로GateCompositionCatalogEntry>();

        public IReadOnlyList<도로GateCompositionCatalogEntry> Entries => entries;

        public void Configure(도로GateCompositionCatalogEntry[] values)
            => entries = values ?? Array.Empty<도로GateCompositionCatalogEntry>();

        public 도로GateCompositionCatalogEntry Resolve(string setName)
        {
            Validate();
            return entries.SingleOrDefault(value =>
                       value.Descriptor.SetName == setName
                       && value.Descriptor.VariantCode == 월드CompositionVariantCodes.A)
                   ?? throw new InvalidOperationException("RoadGateCompositionMissing:" + setName);
        }

        public void Validate()
        {
            if (entries == null
                || entries.Length != 도로GateCompositionSetNames.All.Count
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || 도로GateCompositionSetNames.All.Any(name =>
                    entries.Count(value => value.Descriptor.SetName == name) != 1))
            {
                throw new InvalidOperationException("RoadGateCompositionCatalogInvalid");
            }

            월드CompositionContractValidator.Validate(
                entries.Select(value => value.Descriptor).ToArray(),
                false);
            ValidateGatePairs();
        }

        public void ValidateGatePairs()
        {
            var pairedConnectors = entries
                .SelectMany(value => value.Descriptor.Connectors)
                .Where(value => value.ExpansionSocket
                                && (value.RouteSignature.StartsWith(
                                        "boundary.", StringComparison.Ordinal)
                                    || value.RouteSignature.StartsWith(
                                        "freight.", StringComparison.Ordinal)))
                .GroupBy(value => value.RouteSignature, StringComparer.Ordinal)
                .ToArray();
            if (pairedConnectors.Length != 7
                || pairedConnectors.Any(group => group.Count() != 2
                    || group.Select(value => value.DirectionCode)
                        .Distinct(StringComparer.Ordinal).Count() != 2
                    || !group.Any(value =>
                        value.DirectionCode == 월드CompositionConnectorDirectionCodes.North)
                    || !group.Any(value =>
                        value.DirectionCode == 월드CompositionConnectorDirectionCodes.South)))
            {
                throw new InvalidOperationException("RoadGateConnectorPairInvalid");
            }
        }
    }
}
