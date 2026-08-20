using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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
    public sealed class 공간문법LandscapeRuntimeTests
    {
        [Test]
        public void 안전ManifestHash와_Unity구성대장Hash가_일치한다()
        {
            var catalog = LoadCatalog();
            var json = File.ReadAllText(공간문법ManifestExporter.ManifestPath);
            var header = JsonUtility.FromJson<ManifestHeader>(json);

            Assert.That(catalog.BuildSafeCatalogHashSha256(),
                Is.EqualTo(header.catalogHashSha256).IgnoreCase);
            Assert.That(catalog.Entries.Count, Is.EqualTo(156));
        }

        [Test]
        public void 중립문법V2Hash는_Synty자산지표를제외하고_서버Manifest와일치한다()
        {
            var catalog = LoadCatalog();
            var json = File.ReadAllText(공간문법ManifestExporter.NeutralManifestPath);
            var header = JsonUtility.FromJson<ManifestHeader>(json);

            Assert.That(catalog.BuildNeutralGrammarHashSha256(),
                Is.EqualTo(header.catalogHashSha256).IgnoreCase);
            Assert.That(json, Does.Not.Contain("sourceCompositionKey"));
            Assert.That(json, Does.Not.Contain("triangleCount"));
        }

        [Test]
        public void SyntyBinding은_Primary가사용불가하면_우선순위Fallback을선택한다()
        {
            var grammar = LoadCatalog();
            var source = grammar.Entries[0];
            var primary = new 공간문법SyntyBindingCandidate();
            primary.Configure(공간문법SyntyBindingCodes.Primary, 0,
                source.SourceCompositionKey, source.Prefab,
                source.InternalGeneration.DetailGeneratorRevision);
            var slowerFallback = new 공간문법SyntyBindingCandidate();
            slowerFallback.Configure(공간문법SyntyBindingCodes.Fallback, 20,
                "fixture:fallback:slow", source.Prefab, "fixture-detail.v1");
            var preferredFallback = new 공간문법SyntyBindingCandidate();
            preferredFallback.Configure(공간문법SyntyBindingCodes.Fallback, 10,
                "fixture:fallback:preferred", source.Prefab, "fixture-detail.v1");
            var replacement = new 공간문법SyntyBindingEntry();
            replacement.Configure(source.CompositionKey,
                new[] { primary, slowerFallback, preferredFallback });

            var entries = grammar.Entries.Select(value =>
            {
                if (value.CompositionKey == source.CompositionKey) return replacement;
                var candidate = new 공간문법SyntyBindingCandidate();
                candidate.Configure(공간문법SyntyBindingCodes.Primary, 0,
                    value.SourceCompositionKey, value.Prefab,
                    value.InternalGeneration.DetailGeneratorRevision);
                var entry = new 공간문법SyntyBindingEntry();
                entry.Configure(value.CompositionKey, new[] { candidate });
                return entry;
            }).ToArray();
            var receipt = new 공간문법SyntyInventoryReceipt();
            receipt.Configure("fixture-inventory.v1", new string('b', 64));
            var binding = ScriptableObject.CreateInstance<공간문법SyntyBindingCatalog>();
            try
            {
                binding.Configure(
                    공간문법SyntyBindingCodes.BindingRevision,
                    공간문법CompositionCatalog.NeutralGrammarRevision,
                    grammar.BuildNeutralGrammarHashSha256(),
                    new[] { receipt }, entries);

                var resolved = binding.Resolve(source.CompositionKey,
                    value => !value.IsPrimary);

                Assert.That(resolved.FallbackUsed, Is.True);
                Assert.That(resolved.Candidate.SourceCompositionKey,
                    Is.EqualTo("fixture:fallback:preferred"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(binding);
            }
        }

        [Test]
        public void 생성된SyntyBinding대장은_156개Primary와_중립문법영수증을가진다()
        {
            var grammar = LoadCatalog();
            var binding = AssetDatabase.LoadAssetAtPath<공간문법SyntyBindingCatalog>(
                공간문법SyntyBindingCatalogBuilder.CatalogPath);

            Assert.That(binding, Is.Not.Null);
            binding.Validate();
            Assert.That(binding.Entries.Count, Is.EqualTo(156));
            Assert.That(binding.Entries.All(value =>
                value.Candidates.Count(candidate => candidate.IsPrimary) == 1), Is.True);
            Assert.That(binding.TargetGrammarHashSha256,
                Is.EqualTo(grammar.BuildNeutralGrammarHashSha256()).IgnoreCase);
            Assert.That(binding.SourceCatalogs.Count, Is.EqualTo(3));
        }

        [Test]
        public void 중립문법V2Graph조립은_Binding선택영수증을_배치View에남긴다()
        {
            var grammar = LoadCatalog();
            var binding = AssetDatabase.LoadAssetAtPath<공간문법SyntyBindingCatalog>(
                공간문법SyntyBindingCatalogBuilder.CatalogPath);
            Assert.That(binding, Is.Not.Null);
            var entry = grammar.Entries[0];
            var data = AvailableData(grammar, entry);
            data.GrammarRevision = 공간문법CompositionCatalog.NeutralGrammarRevision;
            data.GrammarHashSha256 = grammar.BuildNeutralGrammarHashSha256();
            var tileRoot = new GameObject("TileRoot");
            try
            {
                var assembler = new 공간문법LandscapeRuntimeAssembler(
                    grammar, binding, 24f);

                var staging = assembler.BuildStaging(data, tileRoot.transform);
                var view = staging.GetComponentInChildren<공간문법PlacementInstanceView>(true);

                Assert.That(view.GrammarRevision,
                    Is.EqualTo(공간문법CompositionCatalog.NeutralGrammarRevision));
                Assert.That(view.BindingRevision,
                    Is.EqualTo(공간문법SyntyBindingCodes.BindingRevision));
                Assert.That(view.BindingHashSha256, Has.Length.EqualTo(64));
                Assert.That(view.SelectedSourceCompositionKey,
                    Is.EqualTo(entry.SourceCompositionKey));
                Assert.That(view.FallbackUsed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tileRoot);
            }
        }

        [Test]
        public void 검증된Graph는_Staging전체를만든후_LandscapeCompositionRoot로교환한다()
        {
            var catalog = LoadCatalog();
            var entry = catalog.Entries[0];
            var tileRoot = new GameObject("TileRoot");
            GameObject current = new GameObject("PreviousLandscapeCompositionRoot");
            current.transform.SetParent(tileRoot.transform, false);
            try
            {
                var data = AvailableData(catalog, entry);
                var assembler = new 공간문법LandscapeRuntimeAssembler(catalog, 24f);

                var staging = assembler.BuildStaging(data, tileRoot.transform);

                Assert.That(staging.activeSelf, Is.False);
                Assert.That(staging.GetComponentsInChildren<공간문법PlacementInstanceView>(true),
                    Has.Length.EqualTo(1));
                공간문법LandscapeRuntimeAssembler.CommitAtomic(ref current, staging);
                Assert.That(current.name, Is.EqualTo("LandscapeCompositionRoot"));
                Assert.That(current.activeSelf, Is.True);
                var view = current.GetComponentInChildren<공간문법PlacementInstanceView>(true);
                Assert.That(view.CompositionKey, Is.EqualTo(entry.CompositionKey));
                Assert.That(view.DeterministicSeed, Is.EqualTo(51760));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tileRoot);
            }
        }

        [Test]
        public void 서버GrammarHash가다르면_기존경관을교환하지않는다()
        {
            var catalog = LoadCatalog();
            var entry = catalog.Entries[0];
            var tileRoot = new GameObject("TileRoot");
            try
            {
                var data = AvailableData(catalog, entry);
                data.GrammarHashSha256 = new string('0', 64);
                var assembler = new 공간문법LandscapeRuntimeAssembler(catalog, 24f);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => assembler.BuildStaging(data, tileRoot.transform));

                Assert.That(exception!.Message,
                    Is.EqualTo("WorldLandscapeGrammarCatalogMismatch"));
                Assert.That(tileRoot.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tileRoot);
            }
        }

        [Test]
        public async Task 서버Repository는_경관Composition읽기경로와응답계약만사용한다()
        {
            var catalog = LoadCatalog();
            var expected = AvailableData(catalog, catalog.Entries[0]);
            var client = new FixtureApiClient(JsonUtility.ToJson(expected));
            var repository = new 공간TileStreamServerRepository(client);

            var actual = await repository.LoadLandscapeCompositionsAsync(
                expected.TileKey, CancellationToken.None);

            Assert.That(actual.GraphHashSha256, Is.EqualTo(expected.GraphHashSha256));
            Assert.That(client.LastRequest, Is.Not.Null);
            Assert.That(client.LastRequest!.Method, Is.EqualTo("GET"));
            Assert.That(client.LastRequest.RelativePath, Is.EqualTo(
                "api/simulation/v1/world-stream/tiles/kr5186%3Al2%3A700%3A1145/landscape-compositions"));
            Assert.That(client.LastRequest.RequiresAuthentication, Is.False);
        }

        private static 공간문법CompositionCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                공간문법CompositionCatalogBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            catalog.Validate();
            return catalog;
        }

        private static 공간LandscapeCompositionTileData AvailableData(
            공간문법CompositionCatalog catalog,
            공간문법CompositionCatalogEntry entry) => new 공간LandscapeCompositionTileData
        {
            SchemaVersion = 공간LandscapeCompositionCodes.SchemaVersion,
            TileKey = "kr5186:l2:700:1145",
            AreaSetStableId = "pyeongchang-farm-hub-town-v1",
            GraphBuildStableId = "landscape-graph:test",
            GraphHashSha256 = new string('a', 64),
            GrammarRevision = catalog.CatalogRevision,
            GrammarHashSha256 = catalog.BuildSafeCatalogHashSha256(),
            StatusCode = 공간LandscapeCompositionCodes.Available,
            Nodes = new[]
            {
                new 공간LandscapeNodeData
                {
                    NodeStableId = "node:test",
                    NodeKindCode = entry.TopologyCode,
                    SemanticCode = entry.Descriptor.SetName,
                    EvidenceKindCode = "Scenario",
                    CenterEastingMeters = 350250d,
                    CenterNorthingMeters = 572750d,
                    WidthMeters = entry.Descriptor.Footprint.x,
                    DepthMeters = entry.Descriptor.Footprint.y,
                },
            },
            Placements = new[]
            {
                new 공간LandscapePlacementData
                {
                    PlacementStableId = "placement:test",
                    NodeStableId = "node:test",
                    OwnerTileKey = "kr5186:l2:700:1145",
                    CompositionKey = entry.CompositionKey,
                    TopologyCode = entry.TopologyCode,
                    EvidenceKindCode = "Scenario",
                    EastingMeters = 350250d,
                    NorthingMeters = 572750d,
                    PhysicalElevationMeters = 950d,
                    RotationDegrees = 0d,
                    DeterministicSeed = 51760,
                    FootprintWidthMeters = entry.Descriptor.Footprint.x,
                    FootprintDepthMeters = entry.Descriptor.Footprint.y,
                    PresentationOnly = true,
                },
            },
            PresentationOnly = true,
            IsOperationalState = false,
        };

        [Serializable]
        private sealed class ManifestHeader
        {
            public string catalogHashSha256 = string.Empty;
        }

        private sealed class FixtureApiClient : ISimulationRehearsalUnityApiClient
        {
            private readonly string responseBody;

            public FixtureApiClient(string body) => responseBody = body;

            public UnityApiRequest LastRequest { get; private set; }

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                request.Validate();
                LastRequest = request;
                return Task.FromResult(new UnityApiResponse
                {
                    StatusCode = 200,
                    Body = responseBody,
                });
            }
        }
    }
}
