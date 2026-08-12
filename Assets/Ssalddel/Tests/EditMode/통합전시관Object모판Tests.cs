using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 통합전시관Object모판Tests
    {
        private const string CatalogPath =
            "Assets/Ssalddel/Presentation/ExhibitionObjects/통합전시관ObjectVisualCatalog.asset";
        private const string PreviewScenePath = "Assets/Ssalddel/Scenes/통합Object모판.unity";

        [Test]
        public void 열다섯Object는_semanticKey와_wrapperPrefab으로분리된다()
        {
            var catalog = LoadCatalog();

            Assert.DoesNotThrow(catalog.Validate);
            Assert.That(catalog.CatalogRevision,
                Is.EqualTo("integrated-exhibition-object-visual-catalog:obj-7a.r1"));
            Assert.That(catalog.Entries.Count, Is.EqualTo(15));
            Assert.That(catalog.Entries.Select(value => value.ObjectStableId), Is.EquivalentTo(new[]
            {
                "seedbed-object:farm.potato-harvest-box.a",
                "seedbed-object:town.hub-inbound-gate.a",
                "seedbed-object:shared.food-pickup-handoff-box.a",
                "seedbed-object:farm.greenhouse.a",
                "seedbed-object:farm.potato-row.a",
                "seedbed-object:farm.potato-plant-visual.a",
                "seedbed-object:farm.irrigation-sprinkler.a",
                "seedbed-object:town.delivery-truck.a",
                "seedbed-object:shared.cargo-pallet.a",
                "seedbed-object:farm.pallet-crate.a",
                "seedbed-object:town.resident-visual.a",
                "seedbed-object:town.grouping-cart-table.a",
                "seedbed-object:city.urban-market-building.a",
                "seedbed-object:city.operator-inventory-shelf.a",
                "seedbed-object:city.market-operator-visual.a",
            }));
            Assert.That(catalog.Entries.All(value =>
                !value.VisualVariantKey.Contains("Assets/", StringComparison.OrdinalIgnoreCase)
                && !value.VisualVariantKey.Contains(".prefab", StringComparison.OrdinalIgnoreCase)
                && !value.PlacementProfileKey.Contains("Assets/", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(catalog.Entries.All(value =>
                AssetDatabase.GetAssetPath(value.Prefab).StartsWith(
                    "Assets/Ssalddel/Presentation/ExhibitionObjects/Prefabs/", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void OBJ6B_물류Object는_권위와분리된필수Socket을가진다()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.Resolve("seedbed-object:town.delivery-truck.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "Driver", "Cargo", "RouteEntry", "RouteExit" }));
            Assert.That(catalog.Resolve("seedbed-object:shared.cargo-pallet.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "Cargo", "Forklift" }));
            Assert.That(catalog.Resolve("seedbed-object:farm.pallet-crate.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "HarvestCargo", "Vehicle", "HubHandoff" }));
        }

        [Test]
        public void OBJ7A_주민CartShopShelf운영자는_공개범위별필수Socket을가진다()
        {
            var catalog = LoadCatalog();

            Assert.That(catalog.Resolve("seedbed-object:town.resident-visual.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "Perspective", "AggregateBoundary" }));
            Assert.That(catalog.Resolve("seedbed-object:town.grouping-cart-table.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "IntentInput", "AggregateOutput", "ConsentBoundary" }));
            Assert.That(catalog.Resolve("seedbed-object:city.urban-market-building.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "PublicProduct", "DemandSignal" }));
            Assert.That(catalog.Resolve("seedbed-object:city.operator-inventory-shelf.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "Inventory", "ShelfTask", "Operator" }));
            Assert.That(catalog.Resolve("seedbed-object:city.market-operator-visual.a").RequiredSocketCodes,
                Is.SupersetOf(new[] { "Perspective", "Inventory", "ShelfTask" }));
        }

        [Test]
        public void wrapper는_footprint_bounds와_고유Socket을검증한다()
        {
            foreach (var entry in LoadCatalog().Entries)
            {
                var root = entry.Prefab.GetComponent<통합전시관SeedbedObjectRoot>();
                Assert.That(root, Is.Not.Null, entry.ObjectStableId);
                Assert.That(root.ValidateWiring(), Is.True, entry.ObjectStableId);
                Assert.That(root.Sockets.Select(value => value.SocketCode),
                    Is.EqualTo(entry.RequiredSocketCodes), entry.ObjectStableId);
                Assert.That(root.Sockets.All(value => root.FindSocket(value.SocketCode) == value.transform),
                    Is.True, entry.ObjectStableId);
                Assert.That(entry.MeasuredBoundsSize.x, Is.GreaterThan(0f));
                Assert.That(entry.MeasuredBoundsSize.y, Is.GreaterThan(0f));
                Assert.That(entry.MeasuredBoundsSize.z, Is.GreaterThan(0f));
                Assert.That(root.VisualRoot.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true).All(value => !value.enabled), Is.True);
            }
        }

        [Test]
        public void 독립모판은_Object하나만선택하고_운영Command를제공하지않는다()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var presenter = roots.SelectMany(value =>
                    value.GetComponentsInChildren<통합전시관ObjectPreviewPresenter>(true)).Single();
                Assert.DoesNotThrow(presenter.ValidateWiring);
                Assert.That(presenter.Catalog개수, Is.EqualTo(LoadCatalog().Entries.Count));
                Assert.That(presenter.운영Command제공여부, Is.False);
                foreach (var stableId in LoadCatalog().Entries.Select(value => value.ObjectStableId))
                {
                    presenter.SelectObject(stableId);
                    Assert.That(presenter.선택ObjectStableId, Is.EqualTo(stableId));
                    Assert.That(presenter.현재ObjectRoot, Is.Not.Null);
                    Assert.That(presenter.현재ObjectRoot!.ObjectStableId, Is.EqualTo(stableId));
                    Assert.That(roots.SelectMany(value =>
                        value.GetComponentsInChildren<통합전시관SeedbedObjectRoot>(true)).Count(), Is.EqualTo(1));
                }
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Button>(true))
                    .Select(value => value.name), Has.None.Contains("Confirm"));
                var objectButtons = roots.SelectMany(value => value.GetComponentsInChildren<Button>(true))
                    .Where(value => value.name.StartsWith("Object_", StringComparison.Ordinal)).ToArray();
                Assert.That(objectButtons.Length, Is.EqualTo(15));
                Assert.That(objectButtons.Select(value => ((RectTransform)value.transform).anchorMin.y),
                    Has.All.GreaterThanOrEqualTo(0f));
                Assert.That(objectButtons.Select(value => ((RectTransform)value.transform).anchorMax.y),
                    Has.All.LessThanOrEqualTo(1f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static 통합전시관ObjectVisualCatalog LoadCatalog()
            => AssetDatabase.LoadAssetAtPath<통합전시관ObjectVisualCatalog>(CatalogPath)
               ?? throw new AssertionException("Catalog missing: " + CatalogPath);
    }
}
