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
    public sealed class 거점CompositionSetTests
    {
        [Test]
        public void 최소거점은_네영역A형과_상태소켓을정의한다()
        {
            var descriptors = 거점CompositionSetBuilder.CreateDescriptorsForValidation();

            Assert.That(descriptors.Count, Is.EqualTo(4));
            Assert.That(descriptors.Select(value => value.PackCode), Is.EquivalentTo(new[]
            {
                월드CompositionPackCodes.Farm,
                월드CompositionPackCodes.Town,
                월드CompositionPackCodes.City,
                월드CompositionPackCodes.RegionalLogisticsHub,
            }));
            Assert.That(descriptors, Has.All.Matches<월드CompositionDescriptor>(value =>
                value.VariantCode == 월드CompositionVariantCodes.A
                && value.SourceKind == 월드CompositionSourceKinds.SyntyNestedPrefab
                && value.Sockets.Count > 0));
            월드CompositionContractValidator.Validate(descriptors, false);
        }

        [Test]
        public void 실제감자필지는_시뮬레이션대상하나와_서른여섯토양Prefab만표현한다()
        {
            var entry = LoadCatalog().Resolve(거점CompositionSetNames.실제감자6x6필지);
            var targetSockets = entry.Descriptor.Sockets.Where(value =>
                value.CategoryCode == 월드CompositionSocketCategoryCodes.SimulationTarget).ToArray();

            Assert.That(targetSockets.Length, Is.EqualTo(1));
            Assert.That(targetSockets[0].SocketCode, Is.EqualTo("farm.socket.potato-field"));
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(entry.Prefab));
            try
            {
                var view = root.GetComponent<거점CompositionSetView>();
                Assert.That(view.EnvironmentRoot.Cast<Transform>().Count(value =>
                    value.name.Contains("SM_Env_Dirt_Rows_01", StringComparison.Ordinal)), Is.EqualTo(36));
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Select(value => value.GetType().Name),
                    Has.None.Contains("FarmSoilTileGridView"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void 거점도로Connector는_CMP3도로Gate와_방향및노선이맞는다()
        {
            var anchorCatalog = LoadCatalog();
            var roadCatalog = AssetDatabase.LoadAssetAtPath<도로GateCompositionCatalog>(
                도로GateCompositionSetBuilder.CatalogPath);
            Assert.That(roadCatalog, Is.Not.Null);
            var candidates = roadCatalog.Entries.SelectMany(value => value.Descriptor.Connectors).ToArray();

            foreach (var connector in anchorCatalog.Entries
                         .SelectMany(value => value.Descriptor.Connectors))
            {
                Assert.That(candidates.Any(candidate =>
                        candidate.RouteSignature == connector.RouteSignature
                        && candidate.ConnectorKindCode == connector.ConnectorKindCode
                        && Opposes(candidate.DirectionCode, connector.DirectionCode)),
                    Is.True,
                    connector.RouteSignature + ":" + connector.ConnectorCode);
            }
        }

        [Test]
        public void 출입구방향과_회전반경및가림Root가명시된다()
        {
            var catalog = LoadCatalog();
            var farm = catalog.Resolve(거점CompositionSetNames.실제감자6x6필지).Prefab
                .GetComponent<거점CompositionSetView>();
            var town = catalog.Resolve(거점CompositionSetNames.타운기본주택).Prefab
                .GetComponent<거점CompositionSetView>();
            var city = catalog.Resolve(거점CompositionSetNames.시티공동주택가로형).Prefab
                .GetComponent<거점CompositionSetView>();

            Assert.That(farm.OcclusionRoot, Is.Null);
            Assert.That(town.SourceEntranceDirectionCode, Is.EqualTo(거점CompositionEntranceCodes.Unknown));
            Assert.That(city.SourceEntranceDirectionCode, Is.EqualTo(거점CompositionEntranceCodes.East));
            Assert.That(new[] { farm, town, city }, Has.All.Matches<거점CompositionSetView>(value =>
                value.DesignedAccessDirectionCode == 거점CompositionEntranceCodes.South
                && value.VehicleTurnRadius > 0f));
            Assert.That(catalog.Entries.Where(value => value.Descriptor.HasOcclusionRoot),
                Has.All.Matches<거점CompositionCatalogEntry>(value =>
                    value.Prefab.GetComponent<거점CompositionSetView>().OcclusionRoot != null));
        }

        [Test]
        public void 거점Prefab은_운영권위Component를포함하지않는다()
        {
            foreach (var entry in LoadCatalog().Entries)
            {
                var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(entry.Prefab));
                try
                {
                    var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                    Assert.That(behaviours.Count(value => value is 거점CompositionSetView), Is.EqualTo(1));
                    Assert.That(behaviours.Select(value => value.GetType().Name),
                        Has.None.Matches<string>(name =>
                            name.Contains("Controller", StringComparison.Ordinal)
                            || name.Contains("UseCase", StringComparison.Ordinal)
                            || name.Contains("Repository", StringComparison.Ordinal)
                            || name.Contains("Simulation", StringComparison.Ordinal)));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        [Test]
        public void PreviewScene은_거점넷과_연결상대넷을_Perspective로보여준다()
        {
            var previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    거점CompositionSetBuilder.PreviewScenePath,
                    OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(value =>
                    value.GetComponentsInChildren<거점CompositionSetView>(true)).Count(), Is.EqualTo(4));
                Assert.That(roots.SelectMany(value =>
                    value.GetComponentsInChildren<도로GateCompositionSetView>(true)).Count(), Is.EqualTo(4));
                var camera = roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Single();
                Assert.That(camera.orthographic, Is.False);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath))
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        private static 거점CompositionCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<거점CompositionCatalog>(
                거점CompositionSetBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.Validate);
            return catalog;
        }

        private static bool Opposes(string left, string right)
            => left == 월드CompositionConnectorDirectionCodes.North
                   && right == 월드CompositionConnectorDirectionCodes.South
               || left == 월드CompositionConnectorDirectionCodes.South
                   && right == 월드CompositionConnectorDirectionCodes.North
               || left == 월드CompositionConnectorDirectionCodes.East
                   && right == 월드CompositionConnectorDirectionCodes.West
               || left == 월드CompositionConnectorDirectionCodes.West
                   && right == 월드CompositionConnectorDirectionCodes.East;
    }
}
