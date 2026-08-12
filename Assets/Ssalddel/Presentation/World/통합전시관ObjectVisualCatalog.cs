using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [CreateAssetMenu(menuName = "Ssalddel/Presentation/통합 전시관 Object Visual Catalog")]
    public sealed class 통합전시관ObjectVisualCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private 통합전시관ObjectVisualCatalogEntry[] entries =
            Array.Empty<통합전시관ObjectVisualCatalogEntry>();

        public string CatalogRevision => catalogRevision;
        public IReadOnlyList<통합전시관ObjectVisualCatalogEntry> Entries => entries;

        public void Configure(string revision, 통합전시관ObjectVisualCatalogEntry[] values)
        {
            catalogRevision = revision;
            entries = values ?? Array.Empty<통합전시관ObjectVisualCatalogEntry>();
        }

        public 통합전시관ObjectVisualCatalogEntry Resolve(string objectStableId)
        {
            Validate();
            return entries.SingleOrDefault(value => value.ObjectStableId == objectStableId)
                ?? throw new InvalidOperationException("IntegratedExhibitionObjectVisualMissing:" + objectStableId);
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogRevision)
                || entries == null
                || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.ObjectStableId)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || entries.Select(value => value.VisualVariantKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("IntegratedExhibitionObjectVisualCatalogInvalid");
        }
    }
}
