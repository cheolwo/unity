using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class 도로GateCompositionSetTests
    {
        [Test]
        public void 정의는_세Pack도로열두개와_Gate열개A형을제공한다()
        {
            var descriptors = 도로GateCompositionSetBuilder.CreateDescriptorsForValidation();

            Assert.That(descriptors.Count, Is.EqualTo(22));
            Assert.That(descriptors.Count(value =>
                    도로GateCompositionSetNames.RoadSets.Contains(value.SetName)),
                Is.EqualTo(12));
            Assert.That(descriptors.Count(value =>
                    도로GateCompositionSetNames.GateSets.Contains(value.SetName)),
                Is.EqualTo(10));
            Assert.That(descriptors,
                Has.All.Matches<월드CompositionDescriptor>(value =>
                    value.VariantCode == 월드CompositionVariantCodes.A
                    && value.HasEnvironmentRoot
                    && !value.HasInteriorRoot));
            월드CompositionContractValidator.Validate(descriptors, false);
        }

        [Test]
        public void 도로Connector는_pack별차량보행과_농기계경계를분리한다()
        {
            var descriptors = 도로GateCompositionSetBuilder.CreateDescriptorsForValidation();
            var townCross = descriptors.Single(value =>
                value.SetName == 도로GateCompositionSetNames.타운도로십자);
            var cityCross = descriptors.Single(value =>
                value.SetName == 도로GateCompositionSetNames.도시도로십자);
            var farmCross = descriptors.Single(value =>
                value.SetName == 도로GateCompositionSetNames.농촌도로십자);

            Assert.That(townCross.Connectors.Count, Is.EqualTo(8));
            Assert.That(cityCross.Connectors.Count, Is.EqualTo(8));
            Assert.That(farmCross.Connectors.Count, Is.EqualTo(12));
            Assert.That(townCross.Connectors,
                Has.None.Matches<월드CompositionConnectorContract>(value =>
                    value.ConnectorKindCode == 월드CompositionConnectorKindCodes.FarmMachine));
            Assert.That(farmCross.Connectors.Count(value =>
                    value.ConnectorKindCode == 월드CompositionConnectorKindCodes.FarmMachine),
                Is.EqualTo(4));
            Assert.That(townCross.Connectors.Select(value => value.RouteSignature).Distinct(),
                Is.EquivalentTo(new[]
                {
                    "road.town.vehicle.v1",
                    "road.town.pedestrian.v1",
                }));
        }

        [Test]
        public void Gate외부Connector는_사람네쌍과화물세쌍으로정확히이어진다()
        {
            var catalog = LoadCatalog();

            Assert.DoesNotThrow(catalog.ValidateGatePairs);
            var external = catalog.Entries
                .SelectMany(value => value.Descriptor.Connectors)
                .Where(value => value.ExpansionSocket
                                && (value.RouteSignature.StartsWith("boundary.",
                                        StringComparison.Ordinal)
                                    || value.RouteSignature.StartsWith("freight.",
                                        StringComparison.Ordinal)))
                .ToArray();
            Assert.That(external.Length, Is.EqualTo(14));
            Assert.That(external.Count(value =>
                    value.RouteSignature.Contains(".pedestrian.",
                        StringComparison.Ordinal)),
                Is.EqualTo(4));
            Assert.That(external.Count(value =>
                    value.RouteSignature.StartsWith("freight.",
                        StringComparison.Ordinal)),
                Is.EqualTo(6));
        }

        [Test]
        public void 생성Prefab은_Synty원본을중첩하고_Farm보정값을보존한다()
        {
            var catalog = LoadCatalog();
            foreach (var entry in catalog.Entries)
            {
                var view = entry.Prefab.GetComponent<도로GateCompositionSetView>();
                Assert.That(view, Is.Not.Null, entry.CompositionKey);
                Assert.That(view.ValidateWiring(), Is.True, entry.CompositionKey);
                Assert.That(view.EnvironmentRoot.childCount, Is.GreaterThan(0));
                Assert.That(Enumerable.Range(0, view.EnvironmentRoot.childCount)
                        .Select(index => view.EnvironmentRoot.GetChild(index).gameObject)
                        .All(PrefabUtility.IsPartOfPrefabInstance),
                    Is.True,
                    entry.CompositionKey);
            }

            var farmGate = catalog.Resolve(도로GateCompositionSetNames.농장타운농장출구);
            var farmView = farmGate.Prefab.GetComponent<도로GateCompositionSetView>();
            var farmRoad = Enumerable.Range(0, farmView.EnvironmentRoot.childCount)
                .Select(index => farmView.EnvironmentRoot.GetChild(index))
                .Single(value => value.name.Contains(
                    "SM_Env_Road_Dirt_Straight_01",
                    StringComparison.Ordinal));
            Assert.That(farmRoad.localPosition.z,
                Is.EqualTo(도로GateCompositionSetBuilder.FarmToTownAdapterOffset)
                    .Within(.0001f));
        }

        [Test]
        public void 십자도로는_타일면적이겹치지않고_90도회전시Connector도함께회전한다()
        {
            var entry = LoadCatalog().Resolve(도로GateCompositionSetNames.타운도로십자);
            var path = AssetDatabase.GetAssetPath(entry.Prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<도로GateCompositionSetView>();
                var renderers = view.EnvironmentRoot.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(5));
                for (var left = 0; left < renderers.Length; left++)
                for (var right = left + 1; right < renderers.Length; right++)
                {
                    var first = renderers[left].bounds;
                    var second = renderers[right].bounds;
                    var overlapX = Mathf.Max(0f,
                        Mathf.Min(first.max.x, second.max.x)
                        - Mathf.Max(first.min.x, second.min.x));
                    var overlapZ = Mathf.Max(0f,
                        Mathf.Min(first.max.z, second.max.z)
                        - Mathf.Max(first.min.z, second.min.z));
                    Assert.That(overlapX * overlapZ, Is.LessThan(.001f));
                }

                root.transform.eulerAngles = new Vector3(0f, 90f, 0f);
                var north = view.FindConnector("vehicle-north");
                Assert.That(north, Is.Not.Null);
                Assert.That(north.position.x, Is.EqualTo(7.5f).Within(.001f));
                Assert.That(north.position.z, Is.EqualTo(0f).Within(.001f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void PreviewScene은_스물두Set와_PerspectiveCamera를보존한다()
        {
            var previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    도로GateCompositionSetBuilder.PreviewScenePath,
                    OpenSceneMode.Single);
                var views = scene.GetRootGameObjects()
                    .SelectMany(value =>
                        value.GetComponentsInChildren<도로GateCompositionSetView>(true))
                    .ToArray();
                var camera = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<Camera>(true))
                    .Single();

                Assert.That(views.Length, Is.EqualTo(22));
                Assert.That(views.Select(value => value.Descriptor.CompositionKey)
                        .Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(22));
                Assert.That(camera.orthographic, Is.False);
                Assert.That(camera.fieldOfView, Is.EqualTo(38f));
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath))
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        private static 도로GateCompositionCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<도로GateCompositionCatalog>(
                도로GateCompositionSetBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.Validate);
            return catalog;
        }
    }
}
