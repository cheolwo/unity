using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class FarmProductVisualCatalogTests
    {
        [OneTimeSetUp]
        public void EnsureCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<FarmProductVisualCatalog>(
                    FarmProductVisualCatalogBuilder.CatalogPath) == null)
            {
                FarmProductVisualCatalogBuilder.Build();
            }
        }

        [Test]
        public void Canonical60품목을_중복없이_모두분류한다()
        {
            var catalog = Load();

            catalog.Validate();
            Assert.That(catalog.Entries.Count, Is.EqualTo(60));
            Assert.That(catalog.Entries.Select(value => value.CanonicalProductStableId).Distinct().Count(),
                Is.EqualTo(60));
            Assert.That(catalog.Entries.Count(value => value.IsMapped), Is.EqualTo(28));
            Assert.That(catalog.Entries.Count(value => !value.IsMapped), Is.EqualTo(32));
        }

        [Test]
        public void 전용Asset과_대표Asset과_미연결을구분한다()
        {
            var catalog = Load();

            var apple = catalog.Resolve("product:food:400:411");
            Assert.That(apple.MappingStatusCode, Is.EqualTo(FarmProductVisualMappingStatusCodes.Direct));
            Assert.That(apple.VisualKey, Is.EqualTo("farm.product.apple"));
            Assert.That(apple.Prefab, Is.Not.Null);

            var cherryTomato = catalog.Resolve("product:food:200:422");
            Assert.That(cherryTomato.MappingStatusCode,
                Is.EqualTo(FarmProductVisualMappingStatusCodes.Representative));
            Assert.That(cherryTomato.VisualKey, Is.EqualTo("farm.product.tomato"));

            var rice = catalog.Resolve("product:food:100:111");
            Assert.That(rice.MappingStatusCode, Is.EqualTo(FarmProductVisualMappingStatusCodes.Unmapped));
            Assert.That(rice.VisualKey, Is.Empty);
            Assert.That(rice.Prefab, Is.Null);
        }

        [Test]
        public void StableId는_prefab이름이나경로에서파생되지않는다()
        {
            var catalog = Load();

            Assert.That(catalog.Entries, Has.All.Matches<FarmProductVisualCatalogEntry>(value =>
                value.CanonicalProductStableId.StartsWith("product:")
                && value.CanonicalProductStableId.IndexOf("SM_", System.StringComparison.Ordinal) < 0));
            Assert.That(catalog.Entries.Where(value => value.IsMapped),
                Has.All.Matches<FarmProductVisualCatalogEntry>(value =>
                    value.VisualKey.StartsWith("farm.product.") && value.Prefab != null));
        }

        private static FarmProductVisualCatalog Load()
            => AssetDatabase.LoadAssetAtPath<FarmProductVisualCatalog>(
                   FarmProductVisualCatalogBuilder.CatalogPath)
               ?? throw new AssertionException("FarmProductVisualCatalog asset was not built.");
    }
}
