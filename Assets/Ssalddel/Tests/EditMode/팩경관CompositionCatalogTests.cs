using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 팩경관CompositionCatalogTests
    {
        [Test]
        public void FarmTownCity구성대장은_의미세트60개와_결정적변형을제공한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<팩경관CompositionCatalog>(
                팩경관CompositionSetBuilder.CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog!.Validate);
            Assert.That(catalog.CatalogRevision,
                Is.EqualTo(팩경관CompositionSetBuilder.CatalogRevision));
            Assert.That(catalog.Entries.Count, Is.EqualTo(60));
            Assert.That(catalog.Entries.Count(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.Farm), Is.EqualTo(24));
            Assert.That(catalog.Entries.Count(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.Town), Is.EqualTo(18));
            Assert.That(catalog.Entries.Count(value =>
                value.Descriptor.PackCode == 월드CompositionPackCodes.City), Is.EqualTo(18));
            var variantGroups = catalog.Entries.GroupBy(value =>
                value.Descriptor.PackCode + "|" + value.Descriptor.SetName).ToArray();
            Assert.That(variantGroups, Has.Length.EqualTo(20));
            Assert.That(variantGroups.All(group =>
                group.Select(value => value.Descriptor.VariantCode)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(월드CompositionVariantCodes.All)), Is.True);
            Assert.That(catalog.Entries, Has.All.Matches<팩경관CompositionCatalogEntry>(value =>
                value.PresentationOnly && !value.CompositionKey.Contains("Assets/", StringComparison.Ordinal)));
        }

        [Test]
        public void 생성Prefab은_구성계약과연결지점을유지한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<팩경관CompositionCatalog>(
                팩경관CompositionSetBuilder.CatalogPath)!;

            foreach (var entry in catalog.Entries.Where(value =>
                         value.Descriptor.PackCode != 월드CompositionPackCodes.Farm))
            {
                var view = entry.Prefab.GetComponent<팩경관CompositionSetView>();
                Assert.That(view, Is.Not.Null, entry.CompositionKey);
                Assert.That(view!.ValidateWiring(), Is.True, entry.CompositionKey);
            }
        }
    }
}
