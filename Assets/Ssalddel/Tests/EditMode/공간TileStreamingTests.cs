using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공간TileStreamingTests
    {
        [Test]
        public void WindowPlanner는_3x3상세_5x5활성_9x9준비범위와_방향선행중심을계산한다()
        {
            var detail = 공간TileWindowPlanner.CreateWindow(700, 1145, 1);
            var active = 공간TileWindowPlanner.CreateWindow(700, 1145, 2);
            var prepared = 공간TileWindowPlanner.CreateWindow(700, 1145, 4);

            Assert.That(detail, Has.Length.EqualTo(9));
            Assert.That(active, Has.Length.EqualTo(25));
            Assert.That(prepared, Has.Length.EqualTo(81));
            Assert.That(detail.All(active.Contains), Is.True);
            Assert.That(active.All(prepared.Contains), Is.True);
            Assert.That(공간TileWindowPlanner.TryParse(
                "kr5186:l2:700:1145", out var x, out var y), Is.True);
            Assert.That((x, y), Is.EqualTo((700, 1145)));
            공간TileWindowPlanner.ResolveDirectionalPrefetchCenter(
                700, 1145, .26d, 0d, 1d, 0d, .25d,
                out var prefetchX, out var prefetchY);
            Assert.That((prefetchX, prefetchY), Is.EqualTo((701, 1145)));
        }

        [Test]
        public async Task Fixture는_실제산출물주소를꾸며내지않고_자료대기로남긴다()
        {
            var repository = new 대관령Farm공간TileStreamFixtureRepository();
            var manifest = await repository.LoadManifestAsync(
                "kr5186:l2:700:1145", CancellationToken.None);

            Assert.That(manifest.IsWaitingForSpatialArtifact, Is.True);
            Assert.That(manifest.Layers, Has.Length.EqualTo(3));
            Assert.That(manifest.Layers.All(value =>
                string.IsNullOrEmpty(value.ArtifactRelativePath)
                && string.IsNullOrEmpty(value.ArtifactHashSha256)), Is.True);
        }

        [Test]
        public async Task Fixture는_5개시나리오건물을_타일별결정적배치로제공한다()
        {
            var repository = new 대관령Farm공간TileStreamFixtureRepository();
            var projections = await Task.WhenAll(
                공간TileWindowPlanner.CreateWindow(700, 1145, 2)
                    .Select(value => repository.LoadObjectsAsync(value, CancellationToken.None)));

            Assert.That(projections.Sum(value => value.Objects.Length), Is.EqualTo(5));
            Assert.That(projections.SelectMany(value => value.Objects).All(value =>
                value.EvidenceKindCode == 공간TileStreamingCodes.Scenario
                && value.PresentationOnly && !value.CollisionEligible), Is.True);
            Assert.That(projections.Single(value => value.TileKey == "kr5186:l2:700:1145")
                .Objects, Has.Length.EqualTo(2));
        }

        [Test]
        public async Task ServerRepository는_읽기경로만사용하고_응답계약을검증한다()
        {
            var client = new CapturingApiClient();
            var repository = new 공간TileStreamServerRepository(client);

            var recipe = await repository.LoadRecipeAsync(
                공간TileStreamingCodes.RecipeStableId, CancellationToken.None);

            Assert.That(recipe.CoverageTileKeys, Has.Length.EqualTo(121));
            Assert.That(recipe.DetailRadius, Is.EqualTo(1));
            Assert.That(recipe.ActiveRadius, Is.EqualTo(2));
            Assert.That(recipe.PrefetchRadius, Is.EqualTo(4));
            Assert.That(recipe.MaxConcurrentTileLoads, Is.EqualTo(4));
            Assert.That(client.LastRequest.Method, Is.EqualTo("GET"));
            Assert.That(client.LastRequest.RequiresAuthentication, Is.False);
            Assert.That(client.LastRequest.RelativePath, Does.StartWith(
                "api/simulation/v1/world-stream/recipes/"));

            var objects = await repository.LoadObjectsAsync(
                "kr5186:l2:700:1145", CancellationToken.None);
            Assert.That(objects.Objects, Has.Length.EqualTo(1));
            Assert.That(client.LastRequest.RelativePath, Does.EndWith("/objects"));
        }

        [Test]
        public async Task Controller는_경계전에9x9준비창을앞당기고_기존Slot을재사용한다()
        {
            var root = new GameObject("TileStreamingTestRoot");
            try
            {
                var target = new GameObject("Target").transform;
                target.SetParent(root.transform);
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform);
                var canvas = new GameObject("Canvas", typeof(Canvas));
                canvas.transform.SetParent(root.transform);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text))
                    .GetComponent<Text>();
                label.transform.SetParent(canvas.transform);
                var controller = root.AddComponent<공간TileStreamingController>();
                controller.Configure(target, visual, label, Vector3.zero, 24f);

                await controller.InitializeAsync(new 대관령Farm공간TileStreamFixtureRepository());
                Assert.That(controller.DetailTileCount, Is.EqualTo(9));
                Assert.That(controller.ActiveTileCount, Is.EqualTo(25));
                Assert.That(controller.PreparedTileCount, Is.EqualTo(81));
                Assert.That(controller.WaitingTileCount, Is.EqualTo(81));
                Assert.That(controller.ObservedWorldTick, Is.Zero);
                var childCount = visual.childCount;

                target.position = new Vector3(6.1f, 0f, 0f);
                await controller.RefreshAsync(false);

                Assert.That(controller.CurrentCenterX, Is.EqualTo(700));
                Assert.That(controller.PreparedCenterX, Is.EqualTo(701));
                Assert.That(controller.DetailTileCount, Is.EqualTo(9));
                Assert.That(controller.ActiveTileCount, Is.EqualTo(25));
                Assert.That(controller.PreparedTileCount, Is.EqualTo(81));
                Assert.That(controller.OutsideCoverageCount, Is.Zero);
                Assert.That(visual.childCount, Is.EqualTo(childCount));
                Assert.That(controller.ObservedActivityRevision, Is.Zero);
                Assert.That(controller.PresentationOnly, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task 시야Controller는_건물을프록시와상세로승격하고_화면밖에서캐시한다()
        {
            var root = new GameObject("VisibilityStreamingTestRoot");
            try
            {
                var target = new GameObject("Target").transform;
                target.SetParent(root.transform);
                var tileVisual = new GameObject("TileVisual").transform;
                tileVisual.SetParent(root.transform);
                var objectVisual = new GameObject("ObjectVisual").transform;
                objectVisual.SetParent(root.transform);
                var canvas = new GameObject("Canvas", typeof(Canvas));
                canvas.transform.SetParent(root.transform);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text))
                    .GetComponent<Text>();
                label.transform.SetParent(canvas.transform);
                var tileController = root.AddComponent<공간TileStreamingController>();
                tileController.Configure(target, tileVisual, label, Vector3.zero, 24f);
                var repository = new 대관령Farm공간TileStreamFixtureRepository();
                await tileController.InitializeAsync(repository);

                var cameraObject = new GameObject("VisibilityCamera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = new Vector3(0f, 2.2f, -8f);
                cameraObject.transform.LookAt(new Vector3(3.8f, 1.2f, 2f));
                var catalog = AssetDatabase.LoadAssetAtPath<법정동경관VisualCatalog>(
                    "Assets/Ssalddel/Presentation/World/Catalogs/평창군법정동경관VisualCatalog.asset");
                Assert.That(catalog, Is.Not.Null);

                var objectController = root.AddComponent<공간시야ObjectStreamingController>();
                objectController.Configure(
                    target, null, tileController, catalog, objectVisual,
                    cameraObject.GetComponent<Camera>());
                await objectController.InitializeAsync(repository);
                for (var index = 0; index < 5; index++)
                    objectController.RefreshVisibilityNow(index * .1f, .1f);

                const string barn = "scenario-object:pyeongchang-farm:barn-a";
                Assert.That(objectController.LoadedObjectCount, Is.EqualTo(5));
                Assert.That(objectController.ActualVisibleCount, Is.GreaterThan(0));
                Assert.That(objectController.GetState(barn), Is.EqualTo(
                    공간시야Object상태.DetailActive));

                cameraObject.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
                objectController.RefreshVisibilityNow(3f, .1f);
                Assert.That(objectController.GetState(barn), Is.EqualTo(
                    공간시야Object상태.HiddenCached));

                cameraObject.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
                objectController.RefreshVisibilityNow(3.1f, .1f);
                Assert.That(objectController.GetState(barn), Is.EqualTo(
                    공간시야Object상태.DetailActive));
                Assert.That(objectController.PresentationOnly, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task 안전이동Gate는_fixture지면안에서는통과하고_추적밖에서는멈춘다()
        {
            var root = new GameObject("SafeMovementGateTestRoot");
            try
            {
                var target = new GameObject("Target").transform;
                target.SetParent(root.transform);
                var tileVisual = new GameObject("TileVisual").transform;
                tileVisual.SetParent(root.transform);
                var canvas = new GameObject("Canvas", typeof(Canvas));
                canvas.transform.SetParent(root.transform);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text))
                    .GetComponent<Text>();
                label.transform.SetParent(canvas.transform);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.SetParent(root.transform);
                ground.transform.localScale = new Vector3(20f, 1f, 20f);

                var tileController = root.AddComponent<공간TileStreamingController>();
                tileController.Configure(target, tileVisual, label, Vector3.zero, 24f);
                await tileController.InitializeAsync(new 대관령Farm공간TileStreamFixtureRepository());
                var gate = root.AddComponent<공간안전이동Gate>();
                gate.Configure(tileController, ~0, true);

                Assert.That(gate.CanEnter(Vector3.zero), Is.True);
                Assert.That(gate.LastProbeHadGround, Is.True);
                Assert.That(gate.CanEnter(new Vector3(1000f, 0f, 1000f)), Is.False);
                Assert.That(gate.LastMoveAllowed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class CapturingApiClient : ISimulationRehearsalUnityApiClient
        {
            public UnityApiRequest LastRequest { get; private set; }

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                if (request.RelativePath.EndsWith("/objects", StringComparison.Ordinal))
                {
                    return Task.FromResult(new UnityApiResponse
                    {
                        StatusCode = 200,
                        Body = "{\"tileKey\":\"kr5186:l2:700:1145\","
                               + "\"placementRevision\":\"r1\",\"placementHashSha256\":\""
                               + new string('b', 64) + "\",\"objects\":[{"
                               + "\"objectStableId\":\"scenario-object:test:barn\","
                               + "\"objectTypeCode\":\"Building\","
                               + "\"visualKey\":\"legal.agriculture.building.barn\","
                               + "\"evidenceKindCode\":\"Scenario\","
                               + "\"landCoverCode\":\"cropland\",\"regionRoleCode\":\"Farm\","
                               + "\"localOffsetXMeters\":0,\"localOffsetYMeters\":0,"
                               + "\"rotationDegrees\":0,\"footprintWidthMeters\":20,"
                               + "\"footprintDepthMeters\":15,\"heightMeters\":12,"
                               + "\"collisionEligible\":false,\"presentationOnly\":true}],"
                               + "\"presentationOnly\":true,\"isOperationalState\":false}",
                    });
                }
                var coverage = string.Join(",", 공간TileWindowPlanner.CreateWindow(700, 1145, 5)
                    .Select(value => "\"" + value + "\""));
                return Task.FromResult(new UnityApiResponse
                {
                    StatusCode = 200,
                    Body = "{\"recipeStableId\":\"" + 공간TileStreamingCodes.RecipeStableId
                           + "\",\"recipeRevision\":\"r1\",\"recipeHashSha256\":\""
                           + new string('a', 64)
                           + "\",\"coordinateReferenceSystem\":\"EPSG:5186\","
                           + "\"tileLevel\":2,\"tileSizeMeters\":500,\"detailRadius\":1,"
                           + "\"activeRadius\":2,\"prefetchRadius\":4,"
                           + "\"maxConcurrentTileLoads\":4,\"boundaryPrefetchFraction\":0.25,"
                           + "\"centerTileX\":700,\"centerTileY\":1145,"
                           + "\"coverageTileKeys\":[" + coverage + "],"
                           + "\"layerCodes\":[\"elevation\"],\"isOperationalState\":false,"
                           + "\"evidenceKindCode\":\"Derived\"}",
                });
            }
        }
    }
}
