using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 농장풍경CompositionSetTests
    {
        private const string CatalogPath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/Catalogs/농장풍경CompositionCatalog.asset";
        private const string PreviewScenePath =
            "Assets/Ssalddel/Experiments/CityFarmWorld/CompositionSets/FarmCompositionSetLibraryPreview.unity";

        [Test]
        public void 여덟풍경Set는_A_B_C_스물네Prefab으로해결된다()
        {
            var catalog = LoadCatalog();

            Assert.DoesNotThrow(catalog.Validate);
            Assert.That(catalog.Entries.Count, Is.EqualTo(24));
            Assert.That(catalog.Entries.Select(value => value.SetName).Distinct(),
                Is.EquivalentTo(농장풍경SetNames.All));
            foreach (var setName in 농장풍경SetNames.All)
            {
                Assert.That(catalog.Entries.Where(value => value.SetName == setName)
                    .Select(value => value.VariantCode),
                    Is.EquivalentTo(농장풍경VariantCodes.All));
            }
        }

        [Test]
        public void 각Set는_Synty원본Prefab연결과상태Socket경계를유지한다()
        {
            var catalog = LoadCatalog();
            foreach (var entry in catalog.Entries)
            {
                var view = entry.Prefab.GetComponent<농장풍경CompositionSetView>();
                Assert.That(view, Is.Not.Null, entry.CompositionKey);
                Assert.That(view.ValidateWiring(), Is.True, entry.CompositionKey);

                var environmentChildren = Enumerable.Range(0, view.EnvironmentRoot.childCount)
                    .Select(view.EnvironmentRoot.GetChild).ToArray();
                Assert.That(environmentChildren.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(environmentChildren.All(child =>
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    return source != null
                        && AssetDatabase.GetAssetPath(source).StartsWith(
                            "Assets/Synty/PolygonFarm/", StringComparison.Ordinal);
                }), Is.True, entry.CompositionKey);

                Assert.That(view.Sockets.All(socket =>
                    농장풍경SocketCodes.IsKnown(socket.SocketCode)), Is.True);
                Assert.That(view.Sockets.Select(socket => socket.SocketCode).Distinct().Count(),
                    Is.EqualTo(view.Sockets.Count));
            }
        }

        [Test]
        public void 풍경Set는_Simulation이나Operational권위를소유하지않는다()
        {
            var catalog = LoadCatalog();
            var forbidden = new[] { "Simulation", "Operational", "Command", "LifetimeScope" };

            foreach (var entry in catalog.Entries)
            {
                var typeNames = entry.Prefab.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(value => value != null)
                    .Select(value => value.GetType().Name)
                    .ToArray();
                Assert.That(typeNames.Any(typeName => forbidden.Any(typeName.Contains)),
                    Is.False, entry.CompositionKey);
            }
        }

        [Test]
        public void 감자밭두렁은_실제감자밭Socket만제공한다()
        {
            var catalog = LoadCatalog();
            foreach (var variant in 농장풍경VariantCodes.All)
            {
                var view = catalog.Resolve(농장풍경SetNames.감자밭두렁, variant)
                    .Prefab.GetComponent<농장풍경CompositionSetView>();
                Assert.That(view.Sockets.Select(value => value.SocketCode),
                    Is.EqualTo(new[] { 농장풍경SocketCodes.실제감자밭 }));
            }
        }

        [Test]
        public void PreviewScene은_스물네Set와PerspectiveCamera를보존한다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var sets = roots.SelectMany(root =>
                    root.GetComponentsInChildren<농장풍경CompositionSetView>(true)).ToArray();
                var camera = roots.SelectMany(root =>
                    root.GetComponentsInChildren<Camera>(true)).Single();

                Assert.That(sets.Length, Is.EqualTo(24));
                Assert.That(sets.All(value => value.ValidateWiring()), Is.True);
                Assert.That(camera.orthographic, Is.False);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void TCS0는_스물네Set의시간반응가능Renderer와표면종류를측정한다()
        {
            var inventory = 농장Composition시간표면Inventory.Measure(LoadCatalog());

            Assert.That(inventory.Count, Is.EqualTo(24));
            Assert.That(inventory.All(value => value.Validate()), Is.True);
            Assert.That(inventory.Select(value => value.CompositionKey).Distinct().Count(),
                Is.EqualTo(24));
            foreach (var setName in 농장풍경SetNames.All)
            {
                var variants = inventory.Where(value => value.SetName == setName).ToArray();
                Assert.That(variants, Has.Length.EqualTo(3), setName);
                Assert.That(variants.Select(value => value.VariantCode),
                    Is.EquivalentTo(농장풍경VariantCodes.All), setName);
            }

            Assert.That(inventory.Sum(value => value.EligibleMaterialSlotCount),
                Is.GreaterThan(24));
            Assert.That(inventory.SelectMany(value => value.SurfaceKinds).Distinct().Count(),
                Is.GreaterThanOrEqualTo(4));
        }

        private static 농장풍경CompositionCatalog LoadCatalog()
            => AssetDatabase.LoadAssetAtPath<농장풍경CompositionCatalog>(CatalogPath)
                ?? throw new AssertionException("Catalog missing: " + CatalogPath);
    }
}
