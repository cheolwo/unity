using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 실제E5AreaSetNetworkTests
    {
        [Test]
        public async Task Network세션은_Nature를유지하고_선택지역과경로만적재한다()
        {
            var fixture = new 실제E5NetworkFixtureRepository();
            var session = new 실제E5AreaSetNetworkStreamingSession(fixture, fixture);

            var nature = await session.InitializeAsync(CancellationToken.None);
            var farm = await session.ActivateAreaAsync(
                실제E5AreaSetNetworkCodes.FarmAreaSet, CancellationToken.None);
            var town = await session.ActivateAreaAsync(
                실제E5AreaSetNetworkCodes.TownAreaSet, CancellationToken.None);

            Assert.That(nature.Network.AreaSets, Has.Length.EqualTo(4));
            Assert.That(nature.Network.RouteGraphs, Has.Length.EqualTo(3));
            Assert.That(nature.Network.Relations, Has.Length.EqualTo(8));
            Assert.That(nature.InteractionReadiness.DirectBindings, Has.Length.EqualTo(30));
            Assert.That(nature.InteractionReadiness.ContextualBindings, Has.Length.EqualTo(5));
            Assert.That(nature.InteractionReadiness.NonSpatialBindings, Has.Length.EqualTo(6));
            Assert.That(ActiveAreaIds(nature), Is.EquivalentTo(new[]
            {
                실제E5AreaSetNetworkCodes.NatureAreaSet,
            }));
            Assert.That(ActiveAreaIds(farm), Is.EquivalentTo(new[]
            {
                실제E5AreaSetNetworkCodes.NatureAreaSet,
                실제E5AreaSetNetworkCodes.FarmAreaSet,
            }));
            Assert.That(farm.LoadedRouteGraphs.Select(value =>
                    value.LandscapeGraphStableId),
                Is.EqualTo(new[] { 실제E5NetworkFixtureRepository.NatureFarmRoute }));
            Assert.That(ActiveAreaIds(town), Is.EquivalentTo(new[]
            {
                실제E5AreaSetNetworkCodes.NatureAreaSet,
                실제E5AreaSetNetworkCodes.TownAreaSet,
            }));
            Assert.That(town.LoadedRouteGraphs.Select(value =>
                    value.LandscapeGraphStableId),
                Is.EqualTo(new[]
                {
                    실제E5NetworkFixtureRepository.FarmHubRoute,
                    실제E5NetworkFixtureRepository.HubTownRoute,
                }));
            Assert.That(fixture.GraphLoads.Count(value =>
                    value == 실제E5NetworkFixtureRepository.NatureFarmRoute),
                Is.EqualTo(1));
        }

        [Test]
        public void 지역인과Hud자료는_서버결정결과코드만허용한다()
        {
            var state = new 실제E5RegionalCausalityData
            {
                Revision = 8,
                ThreatScore = 7,
                RecoveryScore = 3,
                NetPressureModifier = 4,
                OutcomeCode = "Threat",
            };

            Assert.DoesNotThrow(state.Validate);
            state.OutcomeCode = "ClientCalculated";
            Assert.Throws<InvalidOperationException>(state.Validate);
        }

        [Test]
        public void 저장된SimulationWorldShell은_실제E5를_단일Scene안에연결한다()
        {
            var scene = EditorSceneManager.OpenScene(
                SimulationWorldShellBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var shell = scene.GetRootGameObjects().Single(value =>
                    value.name == SimulationWorldShellBuilder.RootName);
                var runtimeRoot = shell.transform.Find(
                    "ShellRuntimeRoot/" + 실제E5AreaSetNetworkShellBuilder.RuntimeRootName);
                var hudRoot = shell.transform.Find(
                    "PersistentUI/SimulationWorldHud/"
                    + 실제E5AreaSetNetworkShellBuilder.HudRootName);
                var controller = runtimeRoot?.GetComponent<실제E5AreaSetNetworkController>();
                var composition = shell.GetComponentInChildren<공간TileStreamingCompositionRoot>(
                    true);

                Assert.That(runtimeRoot, Is.Not.Null);
                Assert.That(runtimeRoot!.Find("AreaSets_4"), Is.Not.Null);
                Assert.That(runtimeRoot.Find("NetworkRouteGraphs_3"), Is.Not.Null);
                Assert.That(hudRoot, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller!.Hud, Is.Not.Null);
                Assert.That(composition, Is.Not.Null);
                Assert.That(composition!.실제E5Network연결됨, Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string[] ActiveAreaIds(실제E5AreaSetNetworkStreamingBatch batch) =>
            batch.AreaBatches
                .Where(value => value.Decisions.Any(decision =>
                    decision.NextState == 공간LandscapeGraphStreamingState.Active))
                .Select(value => value.AreaSet.AreaSetStableId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        private sealed class 실제E5NetworkFixtureRepository :
            I실제E5AreaSetNetworkRepository,
            I공간AreaSetLandscapeGraphRepository
        {
            public const string NatureFarmRoute =
                "landscape-graph:test:nature-farm-route.v1";
            public const string FarmHubRoute =
                "landscape-graph:test:farm-hub-route.v1";
            public const string HubTownRoute =
                "landscape-graph:test:hub-town-route.v1";

            private static readonly string[] AreaIds =
            {
                실제E5AreaSetNetworkCodes.NatureAreaSet,
                실제E5AreaSetNetworkCodes.FarmAreaSet,
                실제E5AreaSetNetworkCodes.HubAreaSet,
                실제E5AreaSetNetworkCodes.TownAreaSet,
            };

            private readonly Dictionary<string, 공간AreaSetDefinitionData> areaSets;
            private readonly Dictionary<string, 공간LandscapeGraphData> graphs;
            private readonly 실제E5AreaSetNetworkData network;
            private readonly 실제E5InteractionReadinessData readiness;

            public 실제E5NetworkFixtureRepository()
            {
                areaSets = AreaIds.Select((areaId, index) =>
                        CreateAreaSet(areaId, index))
                    .ToDictionary(value => value.AreaSetStableId, StringComparer.Ordinal);
                graphs = areaSets.Values.SelectMany(value => value.LandscapeGraphs)
                    .Select(descriptor => CreateGraph(
                        descriptor.SpatialOwnerStableId,
                        descriptor.LandscapeGraphStableId,
                        descriptor.TileRefs[0],
                        descriptor.GraphHashSha256,
                        false))
                    .ToDictionary(value => value.LandscapeGraphStableId,
                        StringComparer.Ordinal);
                foreach (var route in new[]
                         {
                             CreateRouteDescriptor(NatureFarmRoute, 0),
                             CreateRouteDescriptor(FarmHubRoute, 1),
                             CreateRouteDescriptor(HubTownRoute, 2),
                         })
                    graphs.Add(route.LandscapeGraphStableId, CreateGraph(
                        공간AreaSetLandscapeGraphCodes.ActualE5Network,
                        route.LandscapeGraphStableId,
                        route.TileRefs[0],
                        route.GraphHashSha256,
                        true));
                network = CreateNetwork(areaSets);
                readiness = CreateReadiness(network);
            }

            public List<string> GraphLoads { get; } = new List<string>();

            public Task<실제E5AreaSetNetworkData> LoadAreaSetNetworkAsync(
                string networkStableId, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(networkStableId,
                    Is.EqualTo(공간AreaSetLandscapeGraphCodes.ActualE5Network));
                return Task.FromResult(network);
            }

            public Task<실제E5InteractionReadinessData> LoadInteractionReadinessAsync(
                string networkStableId, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(networkStableId, Is.EqualTo(network.NetworkStableId));
                return Task.FromResult(readiness);
            }

            public Task<공간AreaSetDefinitionData> LoadAreaSetAsync(
                string areaSetStableId, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(areaSets[areaSetStableId]);
            }

            public Task<공간LandscapeGraphIndexData> LoadGraphIndexAsync(
                string areaSetStableId, string centerTileKey, int radiusTiles,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var areaSet = areaSets[areaSetStableId];
                return Task.FromResult(new 공간LandscapeGraphIndexData
                {
                    SchemaVersion = 공간AreaSetLandscapeGraphCodes.GraphSchemaVersion,
                    AreaSetStableId = areaSetStableId,
                    CenterTileKey = centerTileKey,
                    RadiusTiles = radiusTiles,
                    Graphs = areaSet.LandscapeGraphs,
                    CoveredTileKeys = areaSet.LandscapeGraphs
                        .SelectMany(value => value.TileRefs).ToArray(),
                    PresentationOnly = true,
                });
            }

            public Task<공간LandscapeGraphData> LoadGraphAsync(
                string landscapeGraphStableId, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GraphLoads.Add(landscapeGraphStableId);
                return Task.FromResult(graphs[landscapeGraphStableId]);
            }

            private static 공간AreaSetDefinitionData CreateAreaSet(
                string areaSetStableId, int index)
            {
                var hash = Hash((char)('1' + index));
                var tile = "scenario-local:area-" + index + ":0:0";
                var descriptor = new 공간LandscapeGraphDescriptorData
                {
                    LandscapeGraphStableId = "landscape-graph:test:area-" + index + ".v1",
                    GraphRoleCode = "AreaCore",
                    GraphRevision = 1,
                    DefinitionHashSha256 = Hash('d'),
                    BuildStatusCode = 공간LandscapeCompositionCodes.Available,
                    GraphHashSha256 = Hash((char)('5' + index)),
                    SpatialOwnerKindCode = 공간AreaSetLandscapeGraphCodes.AreaSetOwner,
                    SpatialOwnerStableId = areaSetStableId,
                    CoordinateSpaceCode = 공간AreaSetLandscapeGraphCodes.ScenarioLocalMeters,
                    Bounds = new 공간LandscapeBoundsData(),
                    AreaRefs = new[] { "area:test:" + index },
                    TileRefs = new[] { tile },
                    ScenarioRouteRefs = Array.Empty<string>(),
                };
                return new 공간AreaSetDefinitionData
                {
                    SchemaVersion = 공간AreaSetLandscapeGraphCodes.AreaSetSchemaVersion,
                    AreaSetStableId = areaSetStableId,
                    Revision = 1,
                    Title = "실제 E5 시험 지역 " + index,
                    DefinitionHashSha256 = hash,
                    DocumentHashSha256 = Hash('e'),
                    CanonicalNetworkStableId =
                        공간AreaSetLandscapeGraphCodes.ActualE5Network,
                    CoordinateSpaceCode =
                        공간AreaSetLandscapeGraphCodes.ScenarioLocalMeters,
                    AreaRefs = descriptor.AreaRefs,
                    ScenarioRouteRefs = Array.Empty<string>(),
                    CompletionAreaRefs = Array.Empty<string>(),
                    LandscapeGraphs = new[] { descriptor },
                    GraphRelations = Array.Empty<공간LandscapeGraphRelationData>(),
                    DefinitionStatusCode = "Ready",
                    PresentationOnly = true,
                    IsOperationalState = false,
                };
            }

            private static 실제E5AreaSetNetworkData CreateNetwork(
                IReadOnlyDictionary<string, 공간AreaSetDefinitionData> definitions)
            {
                var routeGraphs = new[]
                {
                    CreateRouteDescriptor(NatureFarmRoute, 0),
                    CreateRouteDescriptor(FarmHubRoute, 1),
                    CreateRouteDescriptor(HubTownRoute, 2),
                };
                var areas = AreaIds.Select((areaId, index) =>
                    new 실제E5NetworkAreaData
                    {
                        AreaSetStableId = areaId,
                        AreaRoleCode = new[] { "Nature", "Farm", "CityHub", "Town" }[index],
                        LoadPolicyCode = index == 0
                            ? 실제E5AreaSetNetworkCodes.Persistent
                            : 실제E5AreaSetNetworkCodes.OnDemand,
                        DefaultEntryConnectorStableId = "connector:test:" + index,
                        AreaSetRevision = 1,
                        DefinitionHashSha256 = definitions[areaId].DefinitionHashSha256,
                    }).ToArray();
                return new 실제E5AreaSetNetworkData
                {
                    SchemaVersion = 실제E5AreaSetNetworkCodes.SchemaVersion,
                    NetworkStableId = 공간AreaSetLandscapeGraphCodes.ActualE5Network,
                    Revision = 1,
                    Title = "실제 E5 시험 Network",
                    CoordinateSpaceCode = 공간AreaSetLandscapeGraphCodes.ScenarioLocalMeters,
                    EvidenceStageCode = 실제E5AreaSetNetworkCodes.ActualE5,
                    DefinitionHashSha256 = Hash('a'),
                    DocumentHashSha256 = Hash('b'),
                    DefinitionStatusCode = "Ready",
                    AreaSets = areas,
                    RouteGraphs = routeGraphs,
                    Relations = CreateRelations(),
                    PresentationOnly = true,
                    IsOperationalState = false,
                };
            }

            private static 실제E5NetworkRelationData[] CreateRelations() => new[]
            {
                Relation("naturetofarm", AreaIds[0], AreaIds[1], NatureFarmRoute),
                Relation("farmtonature", AreaIds[1], AreaIds[0], NatureFarmRoute),
                Relation("naturetohub", AreaIds[0], AreaIds[2], string.Empty),
                Relation("hubtonature", AreaIds[2], AreaIds[0], string.Empty),
                Relation("naturetotown", AreaIds[0], AreaIds[3], string.Empty),
                Relation("towntonature", AreaIds[3], AreaIds[0], string.Empty),
                Relation("farmtohub", AreaIds[1], AreaIds[2], FarmHubRoute),
                Relation("hubtotown", AreaIds[2], AreaIds[3], HubTownRoute),
            };

            private static 실제E5NetworkRelationData Relation(
                string id, string from, string to, string route) =>
                new 실제E5NetworkRelationData
                {
                    RelationStableId = "relation:test:" + id,
                    FromAreaSetStableId = from,
                    FromConnectorStableId = "connector:test:" + id + ":from",
                    ToAreaSetStableId = to,
                    ToConnectorStableId = "connector:test:" + id + ":to",
                    RelationKindCode = string.IsNullOrWhiteSpace(route)
                        ? 실제E5AreaSetNetworkCodes.PlayerTraversal
                        : 실제E5AreaSetNetworkCodes.CargoLogistics,
                    DirectionCode = "Directed",
                    RouteGraphStableId = route,
                    RouteSignature = "route-signature:test:" + id,
                    SourceStableIds = Array.Empty<string>(),
                };

            private static 공간LandscapeGraphDescriptorData CreateRouteDescriptor(
                string id, int index) => new 공간LandscapeGraphDescriptorData
                {
                    LandscapeGraphStableId = id,
                    GraphRoleCode = "NetworkRoute",
                    GraphRevision = 1,
                    DefinitionHashSha256 = Hash('f'),
                    BuildStatusCode = 공간LandscapeCompositionCodes.Available,
                    GraphHashSha256 = Hash((char)('a' + index)),
                    SpatialOwnerKindCode =
                        공간AreaSetLandscapeGraphCodes.AreaSetNetworkOwner,
                    SpatialOwnerStableId =
                        공간AreaSetLandscapeGraphCodes.ActualE5Network,
                    CoordinateSpaceCode = 공간AreaSetLandscapeGraphCodes.ScenarioLocalMeters,
                    Bounds = new 공간LandscapeBoundsData(),
                    AreaRefs = Array.Empty<string>(),
                    TileRefs = new[] { "scenario-local:route-" + index + ":0:0" },
                    ScenarioRouteRefs = Array.Empty<string>(),
                };

            private static 공간LandscapeGraphData CreateGraph(
                string ownerStableId,
                string graphStableId,
                string tile,
                string graphHash,
                bool networkOwned) => new 공간LandscapeGraphData
                {
                    SchemaVersion = 공간AreaSetLandscapeGraphCodes.GraphSchemaVersion,
                    AreaSetStableId = networkOwned ? string.Empty : ownerStableId,
                    LandscapeGraphStableId = graphStableId,
                    GraphBuildStableId = "graph-build:test:" + graphStableId,
                    GraphRoleCode = networkOwned ? "NetworkRoute" : "AreaCore",
                    GraphRevision = 1,
                    DefinitionHashSha256 = Hash('d'),
                    GraphHashSha256 = graphHash,
                    SpatialOwnerKindCode = networkOwned
                        ? 공간AreaSetLandscapeGraphCodes.AreaSetNetworkOwner
                        : 공간AreaSetLandscapeGraphCodes.AreaSetOwner,
                    SpatialOwnerStableId = ownerStableId,
                    CoordinateSpaceCode = 공간AreaSetLandscapeGraphCodes.ScenarioLocalMeters,
                    GrammarRevision = 공간LandscapeCompositionCodes.ActualE5AuthoredScenarioRevision,
                    GrammarHashSha256 = Hash('c'),
                    StatusCode = 공간LandscapeCompositionCodes.Available,
                    Bounds = new 공간LandscapeBoundsData(),
                    AreaRefs = Array.Empty<string>(),
                    TileRefs = new[] { tile },
                    ScenarioRouteRefs = Array.Empty<string>(),
                    Nodes = Array.Empty<공간LandscapeNodeData>(),
                    Edges = Array.Empty<공간LandscapeEdgeData>(),
                    Placements = Array.Empty<공간LandscapePlacementData>(),
                    ExternalConnectorStubs = Array.Empty<공간LandscapeExternalConnectorData>(),
                    Unresolved = Array.Empty<공간LandscapeUnresolvedData>(),
                    PresentationOnly = true,
                    IsOperationalState = false,
                };

            private static 실제E5InteractionReadinessData CreateReadiness(
                실제E5AreaSetNetworkData sourceNetwork) =>
                new 실제E5InteractionReadinessData
                {
                    SchemaVersion = 실제E5AreaSetNetworkCodes.ReadinessSchemaVersion,
                    NetworkStableId = sourceNetwork.NetworkStableId,
                    NetworkRevision = sourceNetwork.Revision,
                    NetworkDefinitionHashSha256 = sourceNetwork.DefinitionHashSha256,
                    BindingCatalogRevision = "test.v1",
                    BindingCatalogHashSha256 = Hash('9'),
                    OverallStatusCode = 실제E5AreaSetNetworkCodes.Ready,
                    DirectBindings = Enumerable.Range(1, 30).Select(index =>
                        new 실제E5DirectBindingData
                        {
                            WorldInteractionId = "WI-DIRECT-" + index,
                            StatusCode = 실제E5AreaSetNetworkCodes.Ready,
                            SpatialClosedLoop = true,
                        }).ToArray(),
                    ContextualBindings = Enumerable.Range(1, 5).Select(index =>
                        new 실제E5ContextBindingData
                        {
                            WorldInteractionId = "WI-CONTEXT-" + index,
                            StatusCode = 실제E5AreaSetNetworkCodes.ContextBound,
                        }).ToArray(),
                    NonSpatialBindings = Enumerable.Range(1, 6).Select(index =>
                        new 실제E5NonSpatialBindingData
                        {
                            WorldInteractionId = "WI-NONSPATIAL-" + index,
                            StatusCode = 실제E5AreaSetNetworkCodes.NotSpatiallyApplicable,
                        }).ToArray(),
                    Transitions = Array.Empty<실제E5TransitionReadinessData>(),
                    TotalWorldInteractionCount = 41,
                    PresentationOnly = true,
                    IsOperationalState = false,
                };

            private static string Hash(char value) => new string(value, 64);
        }
    }
}
