using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class WorldVisualCatalogTests
    {
        private const string CatalogRoot =
            "Assets/Ssalddel/Experiments/CityFarmWorld/Catalogs/";
        [Test]
        public void VisualKey는_Vendor파일명을Domain계약으로노출하지않는다()
        {
            Assert.That(WorldVisualKeys.All.Count, Is.EqualTo(
                WorldVisualKeys.All.Distinct(StringComparer.Ordinal).Count()));
            Assert.That(WorldVisualKeys.All, Has.None.Contains("SM_"));
            Assert.That(WorldVisualKeys.All, Has.None.Contains("Synty"));
            Assert.That(WorldVisualKeys.All.All(WorldVisualKeys.IsKnown), Is.True);
        }

        [Test]
        public void 네Catalog는_Allowlist전체를PrefabReference로해결한다()
        {
            var catalogs = new[]
            {
                Load(CatalogRoot + "FarmVisualCatalog.asset"),
                Load(CatalogRoot + "UrbanVisualCatalog.asset"),
                Load(CatalogRoot + "TransitionVisualCatalog.asset"),
                Load(CatalogRoot + "FarmCityEnvironmentCatalog.asset"),
            };
            foreach (var catalog in catalogs) catalog.Validate();

            var entries = catalogs.SelectMany(value => value.Entries).ToArray();
            Assert.That(entries.Select(value => value.VisualKey),
                Is.EquivalentTo(WorldVisualKeys.All));
            Assert.That(entries.All(value =>
                AssetDatabase.GetAssetPath(value.Prefab)
                    .StartsWith("Assets/Synty/", StringComparison.Ordinal)), Is.True);
            Assert.That(entries.SelectMany(value =>
                    value.Prefab.GetComponentsInChildren<Renderer>(true))
                .SelectMany(value => value.sharedMaterials)
                .All(value => value != null && value.shader != null
                    && value.shader.name != "Hidden/InternalErrorShader"), Is.True);
        }

        private static WorldVisualCatalog Load(string path)
            => AssetDatabase.LoadAssetAtPath<WorldVisualCatalog>(path)
               ?? throw new AssertionException("Catalog missing: " + path);
    }
}
