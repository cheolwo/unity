using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공간AreaSetLandscapeGraphTests
    {
        [Test]
        public void AreaSet은_다섯Graph와_네Connector관계를_검증한다()
        {
            var areaSet = CreateAreaSet(CreateDescriptors());

            Assert.DoesNotThrow(areaSet.Validate);
            Assert.That(areaSet.LandscapeGraphs, Has.Length.EqualTo(5));
            Assert.That(areaSet.GraphRelations, Has.Length.EqualTo(4));
            Assert.That(areaSet.LandscapeGraphs.Select(value => value.LandscapeGraphStableId),
                Is.Unique);
        }

        [Test]
        public async Task ServerRepository는_AreaSet과Graph읽기경로만_호출한다()
        {
            var descriptors = CreateDescriptors();
            var areaSet = CreateAreaSet(descriptors);
            var index = CreateIndex(descriptors.Take(1).ToArray());
            var graph = CreateGraph(LoadCatalog(), false);
            var client = new GraphApiClient(areaSet, index, graph);
            var repository = new 공간TileStreamServerRepository(client);

            await repository.LoadAreaSetAsync(
                공간AreaSetLandscapeGraphCodes.CanonicalAreaSet, CancellationToken.None);
            await repository.LoadGraphIndexAsync(
                공간AreaSetLandscapeGraphCodes.CanonicalAreaSet,
                "kr5186:l2:700:1145", 4, CancellationToken.None);
            await repository.LoadGraphAsync(graph.LandscapeGraphStableId, CancellationToken.None);

            Assert.That(client.Requests.Select(value => value.Method),
                Is.All.EqualTo("GET"));
            Assert.That(client.Requests.Select(value => value.RequiresAuthentication),
                Is.All.False);
            Assert.That(client.Requests[0].RelativePath, Is.EqualTo(
                "api/simulation/v1/world-stream/area-sets/"
                + "area-set%3Asim%3Apyeongchang%3Afarm-hub-town.v1"));
            Assert.That(client.Requests[1].RelativePath, Does.EndWith(
                "/landscape-graphs?tileKey=kr5186%3Al2%3A700%3A1145&radiusTiles=4"));
            Assert.That(client.Requests[2].RelativePath, Does.StartWith(
                "api/simulation/v1/world-stream/landscape-graphs/"));
        }

        [Test]
        public async Task StreamingSession은_같은Hash를재사용하고_두단계로Cache를해제한다()
        {
            var catalog = LoadCatalog();
            var graph = CreateGraph(catalog, false);
            var descriptor = Descriptor(
                graph.LandscapeGraphStableId,
                graph.TileRefs,
                공간LandscapeCompositionCodes.Available,
                graph.GraphHashSha256);
            var repository = new SequenceGraphRepository(
                CreateAreaSet(new[] { descriptor }, false),
                graph,
                CreateIndex(new[] { descriptor }),
                CreateIndex(new[] { descriptor }),
                CreateIndex(Array.Empty<공간LandscapeGraphDescriptorData>()),
                CreateIndex(Array.Empty<공간LandscapeGraphDescriptorData>()));
            var session = new 공간LandscapeGraphStreamingSession(repository);
            await session.InitializeAsync(
                공간AreaSetLandscapeGraphCodes.CanonicalAreaSet, CancellationToken.None);
            var tile = graph.TileRefs[0];

            var prepared = await session.RefreshAsync(
                tile, new HashSet<string>(), new HashSet<string> { tile },
                4, CancellationToken.None);
            var active = await session.RefreshAsync(
                tile, new HashSet<string> { tile }, new HashSet<string> { tile },
                4, CancellationToken.None);
            var cached = await session.RefreshAsync(
                tile, new HashSet<string>(), new HashSet<string>(),
                4, CancellationToken.None);
            var released = await session.RefreshAsync(
                tile, new HashSet<string>(), new HashSet<string>(),
                4, CancellationToken.None);

            Assert.That(prepared.Decisions.Single().NextState,
                Is.EqualTo(공간LandscapeGraphStreamingState.Prepared));
            Assert.That(active.Decisions.Single().NextState,
                Is.EqualTo(공간LandscapeGraphStreamingState.Active));
            Assert.That(active.Decisions.Single().NeedsPayloadLoad, Is.False);
            Assert.That(repository.GraphLoadCount, Is.EqualTo(1));
            Assert.That(cached.Decisions.Single().NextState,
                Is.EqualTo(공간LandscapeGraphStreamingState.Cached));
            Assert.That(released.ReleasedGraphStableIds,
                Is.EqualTo(new[] { graph.LandscapeGraphStableId }));
            Assert.That(session.TryGetGraph(graph.LandscapeGraphStableId, out _), Is.False);
        }

        [Test]
        public void Graph는_여러Tile조각을만든뒤_GraphRoot하나로교환한다()
        {
            var catalog = LoadCatalog();
            var graph = CreateGraph(catalog, true);
            var areaSetRoot = new GameObject("AreaSetRoot");
            GameObject current = new GameObject("PreviousLandscapeGraphRoot");
            current.transform.SetParent(areaSetRoot.transform, false);
            try
            {
                var assembler = new 공간AreaSetLandscapeGraphRuntimeAssembler(catalog, 24f);
                var staging = assembler.BuildStaging(
                    graph, areaSetRoot.transform, "kr5186:l2:700:1145");

                Assert.That(staging.activeSelf, Is.False);
                Assert.That(staging.transform.childCount, Is.EqualTo(2));
                Assert.That(staging.transform.Find(
                    "TileFragment_kr5186_l2_701_1145").localPosition.x,
                    Is.EqualTo(24f).Within(.001f));
                Assert.That(staging.GetComponentsInChildren<공간문법PlacementInstanceView>(true),
                    Has.Length.EqualTo(2));

                공간AreaSetLandscapeGraphRuntimeAssembler.CommitAtomic(
                    ref current, staging, false);
                Assert.That(current.name, Is.EqualTo("LandscapeGraphRoot"));
                Assert.That(current.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(areaSetRoot);
            }
        }

        private static 공간문법CompositionCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                공간문법CompositionCatalogBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            catalog.Validate();
            return catalog;
        }

        private static 공간LandscapeGraphData CreateGraph(
            공간문법CompositionCatalog catalog,
            bool includeSecondTile)
        {
            var entry = catalog.Entries[0];
            var tiles = includeSecondTile
                ? new[] { "kr5186:l2:700:1145", "kr5186:l2:701:1145" }
                : new[] { "kr5186:l2:700:1145" };
            var nodes = tiles.Select((tile, index) => new 공간LandscapeNodeData
            {
                NodeStableId = "node:test:" + index,
                NodeKindCode = entry.TopologyCode,
                SemanticCode = entry.Descriptor.SetName,
                EvidenceKindCode = "Scenario",
                CenterEastingMeters = 350250d + index * 500d,
                CenterNorthingMeters = 572750d,
                WidthMeters = entry.Descriptor.Footprint.x,
                DepthMeters = entry.Descriptor.Footprint.y,
            }).ToArray();
            var placements = tiles.Select((tile, index) => new 공간LandscapePlacementData
            {
                PlacementStableId = "placement:test:" + index,
                NodeStableId = nodes[index].NodeStableId,
                OwnerTileKey = tile,
                CompositionKey = entry.CompositionKey,
                TopologyCode = entry.TopologyCode,
                EvidenceKindCode = "Scenario",
                EastingMeters = 350250d + index * 500d,
                NorthingMeters = 572750d,
                PhysicalElevationMeters = 950d,
                RotationDegrees = 0d,
                DeterministicSeed = 51760 + index,
                FootprintWidthMeters = entry.Descriptor.Footprint.x,
                FootprintDepthMeters = entry.Descriptor.Footprint.y,
                PresentationOnly = true,
            }).ToArray();
            return new 공간LandscapeGraphData
            {
                SchemaVersion = 공간AreaSetLandscapeGraphCodes.GraphSchemaVersion,
                AreaSetStableId = 공간AreaSetLandscapeGraphCodes.CanonicalAreaSet,
                LandscapeGraphStableId =
                    "landscape-graph:sim:pyeongchang:daegwallyeong-farm.v1",
                GraphBuildStableId = "landscape-graph-build:test",
                GraphRoleCode = "FarmCore",
                GraphRevision = 1,
                DefinitionHashSha256 = new string('d', 64),
                GraphHashSha256 = new string('g', 64),
                GrammarRevision = 공간LandscapeCompositionCodes.GrammarRevision,
                GrammarHashSha256 = catalog.BuildSafeCatalogHashSha256(),
                StatusCode = 공간LandscapeCompositionCodes.Available,
                Bounds = new 공간LandscapeBoundsData
                {
                    MinEastingMeters = 350000d,
                    MinNorthingMeters = 572500d,
                    MaxEastingMeters = includeSecondTile ? 351000d : 350500d,
                    MaxNorthingMeters = 573000d,
                },
                AreaRefs = new[] { "area:sim:pyeongchang:daegwallyeong-farm" },
                TileRefs = tiles,
                Nodes = nodes,
                Placements = placements,
                PresentationOnly = true,
                IsOperationalState = false,
            };
        }

        private static 공간LandscapeGraphDescriptorData[] CreateDescriptors()
        {
            var ids = new[]
            {
                "landscape-graph:sim:pyeongchang:daegwallyeong-farm.v1",
                "landscape-graph:sim:pyeongchang:farm-hub-corridor.v1",
                "landscape-graph:sim:pyeongchang:jinbu-hub.v1",
                "landscape-graph:sim:pyeongchang:hub-town-corridor.v1",
                "landscape-graph:sim:pyeongchang:pyeongchang-town.v1",
            };
            return ids.Select((id, index) => Descriptor(
                id,
                index == 0 ? new[] { "kr5186:l2:700:1145" } : Array.Empty<string>(),
                index == 0 ? 공간LandscapeCompositionCodes.Available
                    : 공간AreaSetLandscapeGraphCodes.Declared,
                index == 0 ? new string('g', 64) : string.Empty)).ToArray();
        }

        private static 공간LandscapeGraphDescriptorData Descriptor(
            string id, string[] tileRefs, string status, string graphHash) =>
            new 공간LandscapeGraphDescriptorData
            {
                LandscapeGraphStableId = id,
                GraphRoleCode = "WorldPart",
                GraphRevision = 1,
                DefinitionHashSha256 = new string('d', 64),
                BuildStatusCode = status,
                GraphHashSha256 = graphHash,
                Bounds = new 공간LandscapeBoundsData(),
                AreaRefs = new[] { "area:sim:pyeongchang:test" },
                TileRefs = tileRefs,
            };

        private static 공간AreaSetDefinitionData CreateAreaSet(
            공간LandscapeGraphDescriptorData[] descriptors,
            bool includeRelations = true)
        {
            var relations = includeRelations
                ? Enumerable.Range(0, descriptors.Length - 1).Select(index =>
                    new 공간LandscapeGraphRelationData
                    {
                        RelationStableId = "graph-relation:" + index,
                        FromGraphStableId = descriptors[index].LandscapeGraphStableId,
                        ToGraphStableId = descriptors[index + 1].LandscapeGraphStableId,
                        RelationCode = 공간AreaSetLandscapeGraphCodes.Connected,
                        ConnectorPair = new 공간LandscapeConnectorPairData
                        {
                            FromConnectorStableId = "connector:from:" + index,
                            ToConnectorStableId = "connector:to:" + index,
                            ConnectorTypeCode = "Road",
                            RouteSignature = "route:" + index,
                        },
                    }).ToArray()
                : Array.Empty<공간LandscapeGraphRelationData>();
            return new 공간AreaSetDefinitionData
            {
                SchemaVersion = 공간AreaSetLandscapeGraphCodes.AreaSetSchemaVersion,
                AreaSetStableId = 공간AreaSetLandscapeGraphCodes.CanonicalAreaSet,
                Revision = 1,
                Title = "평창 Farm–Hub–Town",
                Summary = "경관 Graph 상위 시나리오 컨테이너",
                DefinitionHashSha256 = new string('a', 64),
                DocumentHashSha256 = new string('b', 64),
                AreaRefs = new[] { "area:sim:pyeongchang:test" },
                LandscapeGraphs = descriptors,
                GraphRelations = relations,
                DefinitionStatusCode = "Ready",
                PresentationOnly = true,
                IsOperationalState = false,
            };
        }

        private static 공간LandscapeGraphIndexData CreateIndex(
            공간LandscapeGraphDescriptorData[] descriptors) => new 공간LandscapeGraphIndexData
            {
                SchemaVersion = 공간AreaSetLandscapeGraphCodes.GraphSchemaVersion,
                AreaSetStableId = 공간AreaSetLandscapeGraphCodes.CanonicalAreaSet,
                CenterTileKey = "kr5186:l2:700:1145",
                RadiusTiles = 4,
                Graphs = descriptors,
                CoveredTileKeys = new[] { "kr5186:l2:700:1145" },
                PresentationOnly = true,
            };

        private sealed class GraphApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly 공간AreaSetDefinitionData areaSet;
            private readonly 공간LandscapeGraphIndexData index;
            private readonly 공간LandscapeGraphData graph;

            public GraphApiClient(
                공간AreaSetDefinitionData areaSetValue,
                공간LandscapeGraphIndexData indexValue,
                공간LandscapeGraphData graphValue)
            {
                areaSet = areaSetValue;
                index = indexValue;
                graph = graphValue;
            }

            public List<UnityApiRequest> Requests { get; } = new List<UnityApiRequest>();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                request.Validate();
                Requests.Add(request);
                object value = request.RelativePath.Contains("?tileKey=")
                    ? index
                    : request.RelativePath.Contains("/area-sets/") ? areaSet : graph;
                return Task.FromResult(new UnityApiResponse
                {
                    StatusCode = 200,
                    Body = JsonUtility.ToJson(value),
                });
            }
        }

        private sealed class SequenceGraphRepository : I공간AreaSetLandscapeGraphRepository
        {
            private readonly 공간AreaSetDefinitionData areaSet;
            private readonly 공간LandscapeGraphData graph;
            private readonly Queue<공간LandscapeGraphIndexData> indices;

            public SequenceGraphRepository(
                공간AreaSetDefinitionData areaSetValue,
                공간LandscapeGraphData graphValue,
                params 공간LandscapeGraphIndexData[] indexValues)
            {
                areaSet = areaSetValue;
                graph = graphValue;
                indices = new Queue<공간LandscapeGraphIndexData>(indexValues);
            }

            public int GraphLoadCount { get; private set; }

            public Task<공간AreaSetDefinitionData> LoadAreaSetAsync(
                string areaSetStableId, CancellationToken cancellationToken)
                => Task.FromResult(areaSet);

            public Task<공간LandscapeGraphIndexData> LoadGraphIndexAsync(
                string areaSetStableId, string centerTileKey, int radiusTiles,
                CancellationToken cancellationToken)
                => Task.FromResult(indices.Dequeue());

            public Task<공간LandscapeGraphData> LoadGraphAsync(
                string landscapeGraphStableId, CancellationToken cancellationToken)
            {
                GraphLoadCount++;
                return Task.FromResult(graph);
            }
        }
    }
}
