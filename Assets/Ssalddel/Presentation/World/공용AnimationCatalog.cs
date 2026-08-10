using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [CreateAssetMenu(menuName = "Ssalddel/Presentation/Common Animation Catalog")]
    public sealed class 공용AnimationCatalog : ScriptableObject
    {
        [SerializeField] private 공용AnimationCatalogEntry[] entries =
            Array.Empty<공용AnimationCatalogEntry>();

        public IReadOnlyList<공용AnimationCatalogEntry> Entries => entries;

        public void Configure(공용AnimationCatalogEntry[] values)
            => entries = values ?? Array.Empty<공용AnimationCatalogEntry>();

        public 공용AnimationCatalogEntry Resolve(string packCode)
        {
            Validate();
            return entries.SingleOrDefault(value => value.PackCode == packCode)
                   ?? throw new InvalidOperationException("CommonAnimationPackMissing:" + packCode);
        }

        public void Validate()
        {
            var expected = new[]
            {
                월드CompositionPackCodes.Farm,
                월드CompositionPackCodes.Town,
                월드CompositionPackCodes.City,
            };
            if (entries == null
                || entries.Length != expected.Length
                || entries.Any(value => value == null || !value.Validate())
                || !entries.Select(value => value.PackCode)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)))
                throw new InvalidOperationException("CommonAnimationCatalogInvalid");
        }
    }
}
