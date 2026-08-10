using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 거점CompositionSetNames
    {
        public const string 실제감자6x6필지 = "실제 감자 6×6 필지";
        public const string 타운기본주택 = "타운 기본주택";
        public const string 시티공동주택가로형 = "시티 공동주택 가로형";
        public const string 지역물류허브Dock = "지역 물류허브 Dock";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            실제감자6x6필지,
            타운기본주택,
            시티공동주택가로형,
            지역물류허브Dock,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
               && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 거점CompositionEntranceCodes
    {
        public const string None = "none";
        public const string Unknown = "unknown";
        public const string North = "north";
        public const string East = "east";
        public const string South = "south";
        public const string West = "west";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            None,
            Unknown,
            North,
            East,
            South,
            West,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
               && All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class 거점CompositionCatalogEntry
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
                || !거점CompositionSetNames.IsKnown(descriptor.SetName)
                || descriptor.VariantCode != 월드CompositionVariantCodes.A
                || prefab == null)
            {
                return false;
            }

            var view = prefab.GetComponent<거점CompositionSetView>();
            return view != null
                   && view.Descriptor.CompositionKey == descriptor.CompositionKey
                   && view.ValidateWiring();
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/거점 Composition Catalog")]
    public sealed class 거점CompositionCatalog : ScriptableObject
    {
        [SerializeField] private 거점CompositionCatalogEntry[] entries =
            Array.Empty<거점CompositionCatalogEntry>();

        public IReadOnlyList<거점CompositionCatalogEntry> Entries => entries;

        public void Configure(거점CompositionCatalogEntry[] values)
            => entries = values ?? Array.Empty<거점CompositionCatalogEntry>();

        public 거점CompositionCatalogEntry Resolve(string setName)
        {
            Validate();
            return entries.SingleOrDefault(value =>
                       value.Descriptor.SetName == setName)
                   ?? throw new InvalidOperationException(
                       "AnchorCompositionMissing:" + setName);
        }

        public void Validate()
        {
            var expectedPacks = new[]
            {
                월드CompositionPackCodes.Farm,
                월드CompositionPackCodes.Town,
                월드CompositionPackCodes.City,
                월드CompositionPackCodes.RegionalLogisticsHub,
            };
            if (entries == null
                || entries.Length != 거점CompositionSetNames.All.Count
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || 거점CompositionSetNames.All.Any(name =>
                    entries.Count(value => value.Descriptor.SetName == name) != 1)
                || !entries.Select(value => value.Descriptor.PackCode)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedPacks.OrderBy(
                        value => value,
                        StringComparer.Ordinal)))
            {
                throw new InvalidOperationException("AnchorCompositionCatalogInvalid");
            }

            월드CompositionContractValidator.Validate(
                entries.Select(value => value.Descriptor).ToArray(),
                false);
        }
    }
}
