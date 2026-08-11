using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.PotatoJourney;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class PotatoProductionDistributionBootstrapCatalogTests
    {
        private const string FarmCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/FarmVisualCatalog.asset";
        private const string UrbanCatalogPath =
            "Assets/Ssalddel/Experiments - 연구/CityFarmWorld/Catalogs/UrbanVisualCatalog.asset";

        [Test]
        public void BootstrapVisualKey는_기존FarmUrbanCatalog의실제Prefab으로해석된다()
        {
            var farm = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(FarmCatalogPath);
            var urban = AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(UrbanCatalogPath);
            Assert.IsNotNull(farm);
            Assert.IsNotNull(urban);

            var keys = new[]
            {
                PotatoProductionDistributionVisualKeys.PotatoProduct,
                PotatoProductionDistributionVisualKeys.PotatoCultivation,
                PotatoProductionDistributionVisualKeys.DeliveryVan,
                PotatoProductionDistributionVisualKeys.Warehouse,
                PotatoProductionDistributionVisualKeys.Market,
                PotatoProductionDistributionVisualKeys.MarketShelf,
            };

            foreach (var key in keys)
            {
                var catalog = key.StartsWith("farm.", StringComparison.Ordinal) ? farm : urban;
                var entry = catalog.Resolve(key);
                Assert.IsNotNull(entry.Prefab, key);
                StringAssert.DoesNotContain("Assets/", key);
                StringAssert.DoesNotContain(".prefab", key);
            }
            Assert.AreEqual(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        }
    }
}
