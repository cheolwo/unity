using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class FarmProductVisualMappingStatusCodes
    {
        public const string Direct = "Direct";
        public const string Representative = "Representative";
        public const string Unmapped = "Unmapped";

        public static bool IsKnown(string value)
            => value == Direct || value == Representative || value == Unmapped;
    }

    [Serializable]
    public sealed class FarmProductVisualCatalogEntry
    {
        [SerializeField] private string canonicalProductStableId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string mappingStatusCode = FarmProductVisualMappingStatusCodes.Unmapped;
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string evidenceNote = string.Empty;

        public string CanonicalProductStableId => canonicalProductStableId;
        public string DisplayName => displayName;
        public string MappingStatusCode => mappingStatusCode;
        public string VisualKey => visualKey;
        public GameObject Prefab => prefab;
        public string EvidenceNote => evidenceNote;
        public bool IsMapped => mappingStatusCode != FarmProductVisualMappingStatusCodes.Unmapped;

        public void Configure(
            string stableId,
            string name,
            string statusCode,
            string semanticVisualKey,
            GameObject sourcePrefab,
            string note)
        {
            canonicalProductStableId = stableId;
            displayName = name;
            mappingStatusCode = statusCode;
            visualKey = semanticVisualKey;
            prefab = sourcePrefab;
            evidenceNote = note;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(canonicalProductStableId)
                || string.IsNullOrWhiteSpace(displayName)
                || !FarmProductVisualMappingStatusCodes.IsKnown(mappingStatusCode)
                || string.IsNullOrWhiteSpace(evidenceNote))
            {
                return false;
            }

            return mappingStatusCode == FarmProductVisualMappingStatusCodes.Unmapped
                ? string.IsNullOrEmpty(visualKey) && prefab == null
                : visualKey.StartsWith("farm.product.", StringComparison.Ordinal)
                  && prefab != null;
        }
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/Farm Product Visual Catalog")]
    public sealed class FarmProductVisualCatalog : ScriptableObject
    {
        [SerializeField] private string revision = string.Empty;
        [SerializeField] private FarmProductVisualCatalogEntry[] entries =
            Array.Empty<FarmProductVisualCatalogEntry>();

        public string Revision => revision;
        public IReadOnlyList<FarmProductVisualCatalogEntry> Entries => entries;

        public void Configure(string catalogRevision, FarmProductVisualCatalogEntry[] values)
        {
            revision = catalogRevision;
            entries = values ?? Array.Empty<FarmProductVisualCatalogEntry>();
        }

        public FarmProductVisualCatalogEntry Resolve(string canonicalProductStableId)
        {
            Validate();
            return entries.SingleOrDefault(value =>
                       value.CanonicalProductStableId == canonicalProductStableId)
                   ?? throw new InvalidOperationException(
                       "FarmProductVisualMappingMissing:" + canonicalProductStableId);
        }

        public bool TryResolveMapped(
            string canonicalProductStableId,
            out FarmProductVisualCatalogEntry entry)
        {
            entry = entries.SingleOrDefault(value =>
                value.CanonicalProductStableId == canonicalProductStableId)!;
            return entry != null && entry.IsMapped;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(revision)
                || entries == null
                || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CanonicalProductStableId)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
            {
                throw new InvalidOperationException("FarmProductVisualCatalogInvalid:" + revision);
            }
        }
    }
}
