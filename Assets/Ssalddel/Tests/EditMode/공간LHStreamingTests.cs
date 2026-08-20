using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공간LHStreamingTests
    {
        [Test]
        public async Task 로컬엔진Profile은_L해상도와_H의미계층을_직교하게_유지한다()
        {
            var repository = new 로컬공간LHWorldEngine();
            var response = await repository.PreviewCellsAsync(Request("profile", 2801, 4581),
                CancellationToken.None);

            Assert.That(response.Profile.Levels.Single(value => value.LevelCode == "L0")
                .DefaultHLevelCode, Is.EqualTo("H4"));
            Assert.That(response.Profile.Levels.Single(value => value.LevelCode == "L0")
                .PrimaryHQueryLevelCode, Is.EqualTo("H4"));
            Assert.That(response.Profile.Levels.Single(value => value.LevelCode == "L1")
                .DefaultHLevelCode, Is.EqualTo("H3"));
            Assert.That(response.Profile.Levels.Single(value => value.LevelCode == "L2")
                .DefaultHLevelCode, Is.EqualTo("H2"));
            Assert.That(response.Profile.Levels.Single(value => value.LevelCode == "L3")
                .DefaultHLevelCode, Is.EqualTo("H1"));
            Assert.That(response.Profile.L3CellSize, Is.EqualTo(125));
            Assert.That(response.Cells, Has.Length.EqualTo(81));
            Assert.That(response.ContentSourceCode,
                Is.EqualTo(공간LHWorldCodes.ScenarioProcedural));
            Assert.That(response.Cells.All(value => value.ContentSourceCode
                == 공간LHWorldCodes.ScenarioProcedural), Is.True);
            Assert.That(repository.SourceModeCode, Is.EqualTo(공간LHWorldCodes.LocalEngine));
        }

        [Test]
        public async Task L실행해상도는_H주조회단계를_독립적으로_바꿔도_유효하다()
        {
            var repository = new 로컬공간LHWorldEngine();
            var response = await repository.PreviewCellsAsync(Request("independent", 2801, 4581),
                CancellationToken.None);

            foreach (var level in response.Profile.Levels)
                level.PrimaryHQueryLevelCode = "H2";

            Assert.DoesNotThrow(() => response.Profile.Validate());
            Assert.That(response.Profile.Levels.Select(value => value.CellSizeMeters),
                Is.EqualTo(new[] { 8000, 2000, 500, 125 }));
        }

        [Test]
        public async Task 응답과_셀의_내용공급자가_다르면_거부한다()
        {
            var repository = new 로컬공간LHWorldEngine();
            var response = await repository.PreviewCellsAsync(Request("source-mismatch", 2801, 4581),
                CancellationToken.None);
            response.Cells[0].ContentSourceCode = 공간LHWorldCodes.AuthoritativeWorld;

            Assert.Throws<InvalidOperationException>(() => response.Validate("source-mismatch"));
        }

        [Test]
        public async Task 로컬생성은_월드시드와_계절표현에서_기본배치를_분리한다()
        {
            var spring = new 로컬공간LHWorldEngine(공간LHWorldCodes.WorldSeed, 1);
            var winter = new 로컬공간LHWorldEngine(공간LHWorldCodes.WorldSeed, 85);

            var first = await spring.PreviewCellsAsync(Request("first", 2801, 4581),
                CancellationToken.None);
            var second = await winter.PreviewCellsAsync(Request("second", 2801, 4581),
                CancellationToken.None);

            var firstCenter = first.Cells.Single(value => value.CellKey == "kr5186:l3:2801:4581");
            var secondCenter = second.Cells.Single(value => value.CellKey == "kr5186:l3:2801:4581");
            Assert.That(secondCenter.BasePlanHashSha256,
                Is.EqualTo(firstCenter.BasePlanHashSha256));
            Assert.That(first.Season.SeasonCode, Is.EqualTo("Spring"));
            Assert.That(second.Season.SeasonCode, Is.EqualTo("Winter"));
        }

        [Test]
        public async Task 로컬생성은_H계보_기준거점_이웃경계를_결정적으로_계산한다()
        {
            var repository = new 로컬공간LHWorldEngine();
            var response = await repository.PreviewCellsAsync(Request("anchors", 2801, 4581),
                CancellationToken.None);
            var center = response.Cells.Single(value => value.CellKey == "kr5186:l3:2801:4581");
            var east = response.Cells.Single(value => value.CellKey == "kr5186:l3:2802:4581");

            Assert.That(center.HBindings.Any(value => value.HLevelCode == "H1"
                && value.SpatialStableId == "h1-stock:farm-work-yard"), Is.True);
            Assert.That(center.HBindings.Single(value => value.HLevelCode == "H2").StateCode,
                Is.EqualTo("IdeaInventory"));
            Assert.That(center.Placements.Any(value => value.FixedAnchor
                && value.CompositionKey == "farm:헛간 작업마당:A"), Is.True);
            Assert.That(east.Placements.Any(value => value.FixedAnchor
                && value.CompositionKey == "farm:감자밭 두렁:A"), Is.True);
            Assert.That(center.Placements.Count(value => !value.FixedAnchor),
                Is.InRange(3, 5));
            Assert.That(center.Connectors.Single(value => value.SideCode == "E").BoundaryHashSha256,
                Is.EqualTo(east.Connectors.Single(value => value.SideCode == "W")
                    .BoundaryHashSha256));
        }

        [Test]
        public async Task 로컬생성은_H4경계밖을_생성하지않고_목록으로_남긴다()
        {
            var repository = new 로컬공간LHWorldEngine();
            var response = await repository.PreviewCellsAsync(Request("edge",
                    공간LHWorldCodes.MinimumL3X, 공간LHWorldCodes.MinimumL3Y),
                CancellationToken.None);

            Assert.That(response.Cells, Has.Length.EqualTo(25));
            Assert.That(response.OutsideCoverageCellKeys, Has.Length.EqualTo(56));
            var corner = response.Cells.Single(value => value.CellX == 공간LHWorldCodes.MinimumL3X
                && value.CellY == 공간LHWorldCodes.MinimumL3Y);
            Assert.That(corner.Connectors.Single(value => value.SideCode == "W").Passable,
                Is.False);
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await repository.PreviewCellsAsync(Request("outside",
                    공간LHWorldCodes.MinimumL3X - 1, 공간LHWorldCodes.MinimumL3Y),
                    CancellationToken.None));
        }

        [Test]
        public async Task 로컬월드시드를_바꾸면_같은위치의_배치해시만_결정적으로_바뀐다()
        {
            var first = new 로컬공간LHWorldEngine("single-player-seed-a", 29);
            var repeat = new 로컬공간LHWorldEngine("single-player-seed-a", 29);
            var other = new 로컬공간LHWorldEngine("single-player-seed-b", 29);
            var a = await first.PreviewCellsAsync(Request("a", 2801, 4581), CancellationToken.None);
            var a2 = await repeat.PreviewCellsAsync(Request("a2", 2801, 4581), CancellationToken.None);
            var b = await other.PreviewCellsAsync(Request("b", 2801, 4581), CancellationToken.None);
            var cellA = a.Cells.Single(value => value.CellX == 2801 && value.CellY == 4581);
            var cellA2 = a2.Cells.Single(value => value.CellX == 2801 && value.CellY == 4581);
            var cellB = b.Cells.Single(value => value.CellX == 2801 && value.CellY == 4581);

            Assert.That(a.Season.SeasonCode, Is.EqualTo("Summer"));
            Assert.That(cellA2.BasePlanHashSha256, Is.EqualTo(cellA.BasePlanHashSha256));
            Assert.That(cellB.BasePlanHashSha256, Is.Not.EqualTo(cellA.BasePlanHashSha256));
        }

        [Test]
        public async Task Engine은_활성5x5를_이동가능하게_준비하고_경계전에_다음창을_요청한다()
        {
            var root = new GameObject("LHStreamingTestRoot");
            var player = new GameObject("Player");
            var generated = new GameObject("Generated");
            generated.transform.SetParent(root.transform);
            var engine = root.AddComponent<공간LHStreamingEngine>();
            engine.Configure(player.transform, generated.transform, null, null,
                Vector3.zero, 125f, "fixture", 0);
            try
            {
                await engine.InitializeAsync(new 로컬공간LHWorldEngine());
                engine.DrainAssemblyForTests();

                Assert.That(engine.SourceModeCode, Is.EqualTo(공간LHWorldCodes.LocalEngine));
                Assert.That(engine.TrackedCellCount, Is.EqualTo(81));
                Assert.That(engine.IsPlayerTraversalReady("kr5186:l3:2801:4581"), Is.True);
                Assert.That(engine.IsPlayerTraversalReady("kr5186:l3:2803:4583"), Is.True);
                Assert.That(engine.IsPlayerTraversalReady("kr5186:l3:2804:4584"), Is.False);

                player.transform.position = new Vector3(32f, 0f, 0f);
                engine.EvaluateForTests();
                engine.DrainAssemblyForTests();
                Assert.That(engine.PlayerCellKey, Is.EqualTo("kr5186:l3:2801:4581"));
                Assert.That(engine.RequestedFocusCellKey, Is.EqualTo("kr5186:l3:2802:4581"));
                Assert.That(engine.IsPlayerTraversalReady("kr5186:l3:2804:4581"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public async Task Engine은_Npc경로앞쪽셀을_보조관심점으로_선행준비한다()
        {
            var root = new GameObject("LHNpcRouteStreamingTestRoot");
            var player = new GameObject("Player");
            var generated = new GameObject("Generated");
            generated.transform.SetParent(root.transform);
            var engine = root.AddComponent<공간LHStreamingEngine>();
            engine.Configure(player.transform, generated.transform, null, null,
                Vector3.zero, 125f, "fixture", 0);
            try
            {
                await engine.InitializeAsync(new 로컬공간LHWorldEngine());
                engine.DrainAssemblyForTests();
                var routeTarget = new Vector3(875f, 0f, 0f);
                Assert.That(engine.IsNpcNavigationReady(routeTarget), Is.False);

                engine.RegisterNpcRouteInterest("npc:local:freight-1", routeTarget);
                engine.EvaluateForTests();
                engine.DrainAssemblyForTests();

                Assert.That(engine.NpcRouteInterestCount, Is.EqualTo(1));
                Assert.That(engine.IsNpcNavigationReady(routeTarget), Is.True);
                Assert.That(engine.IsCapabilityReady("kr5186:l3:2808:4581",
                    공간LHWorldCodes.NpcNavigation), Is.True);
                engine.UnregisterNpcRouteInterest("npc:local:freight-1");
                Assert.That(engine.NpcRouteInterestCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public async Task Engine은_늦게도착한_이전Epoch를_새창위에적용하지않는다()
        {
            var root = new GameObject("LHStaleResponseTestRoot");
            var player = new GameObject("Player");
            var generated = new GameObject("Generated");
            generated.transform.SetParent(root.transform);
            var engine = root.AddComponent<공간LHStreamingEngine>();
            engine.Configure(player.transform, generated.transform, null, null,
                Vector3.zero, 125f, "fixture", 0);
            var repository = new 지연첫응답Repository();
            try
            {
                var initialization = engine.InitializeAsync(repository);
                await repository.FirstRequestStarted;
                player.transform.position = new Vector3(32f, 0f, 0f);
                engine.EvaluateForTests();
                engine.DrainAssemblyForTests();
                Assert.That(engine.StateOf("kr5186:l3:2806:4581"), Is.Not.Null);

                repository.ReleaseFirstRequest();
                await initialization;
                engine.DrainAssemblyForTests();

                Assert.That(engine.RequestedFocusCellKey,
                    Is.EqualTo("kr5186:l3:2802:4581"));
                Assert.That(engine.StateOf("kr5186:l3:2797:4581"), Is.Null);
                Assert.That(engine.StateOf("kr5186:l3:2806:4581"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public async Task Engine은_2km이상에서_월드Root를_L3단위로옮기고_전역Cell을유지한다()
        {
            var world = new GameObject("FloatingOriginWorld");
            var player = new GameObject("Player");
            player.transform.SetParent(world.transform, false);
            var engineRoot = new GameObject("Engine");
            engineRoot.transform.SetParent(world.transform, false);
            var generated = new GameObject("Generated");
            generated.transform.SetParent(engineRoot.transform, false);
            var engine = engineRoot.AddComponent<공간LHStreamingEngine>();
            engine.Configure(player.transform, generated.transform, null, null,
                Vector3.zero, 125f, "fixture", 0);
            engine.ConfigureFloatingOrigin(world.transform);
            try
            {
                await engine.InitializeAsync(new 로컬공간LHWorldEngine());
                engine.DrainAssemblyForTests();
                player.transform.localPosition = new Vector3(2125f, 0f, 0f);

                engine.EvaluateForTests();
                engine.DrainAssemblyForTests();

                Assert.That(engine.OriginShiftCount, Is.EqualTo(1));
                Assert.That(engine.AccumulatedOriginShift.x, Is.EqualTo(2125f));
                Assert.That(player.transform.position.x, Is.EqualTo(0f).Within(.001f));
                Assert.That(engine.CellKeyAtPosition(player.transform.position),
                    Is.EqualTo("kr5186:l3:2818:4581"));
            }
            finally
            {
                Object.DestroyImmediate(world);
            }
        }

        [Test]
        public void L3CellKey는_125m_중앙기준으로_양자화한다()
        {
            Assert.That(공간LHCellKey.FromWorldPosition(62.49d, 0d, 0d, 0d),
                Is.EqualTo("kr5186:l3:2801:4581"));
            Assert.That(공간LHCellKey.FromWorldPosition(62.5d, -62.5d, 0d, 0d),
                Is.EqualTo("kr5186:l3:2802:4581"));
            Assert.That(공간LHCellKey.FromWorldPosition(-62.51d, 0d, 0d, 0d),
                Is.EqualTo("kr5186:l3:2800:4581"));
        }

        [Test]
        public void 저장된SimulationWorldShell은_LH엔진과_125m셀Root를_참조한다()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/SimulationWorldShell.unity", OpenSceneMode.Single);
            var engine = Object.FindFirstObjectByType<공간LHStreamingEngine>(
                FindObjectsInactive.Include);
            var gate = Object.FindFirstObjectByType<공간안전이동Gate>(
                FindObjectsInactive.Include);
            var composition = Object.FindFirstObjectByType<공간TileStreamingCompositionRoot>(
                FindObjectsInactive.Include);

            Assert.That(engine, Is.Not.Null);
            Assert.That(engine!.L3CellWorldSize, Is.EqualTo(125f));
            Assert.That(engine.FloatingOriginConfigured, Is.True);
            Assert.That(engine.transform.Find("LH_L3CellPool_125m"), Is.Not.Null);
            Assert.That(gate, Is.Not.Null);
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition!.서버기준사용중, Is.False);
            Assert.That(composition.로컬월드시드값, Is.EqualTo(공간LHWorldCodes.WorldSeed));
            Assert.That(composition.로컬시작일값, Is.EqualTo(1));
        }

        private static 공간LHCellPreviewRequestData Request(string epoch, int x, int y)
            => new()
            {
                RequestEpoch = epoch,
                SessionStableId = "fixture",
                RecipeStableId = 공간LHWorldCodes.RecipeStableId,
                AreaSetStableId = 공간LHWorldCodes.AreaSetStableId,
                FocusL3CellKey = 공간LHCellKey.L3(x, y),
            };

        private sealed class 지연첫응답Repository : I공간LHWorldRepository
        {
            private readonly 대관령Farm공간LHWorldFixtureRepository fixture = new();
            private readonly TaskCompletionSource<bool> firstStarted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> releaseFirst =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int requestCount;

            public string SourceModeCode => 공간TileStreamingCodes.Fixture;
            public Task FirstRequestStarted => firstStarted.Task;

            public void ReleaseFirstRequest() => releaseFirst.TrySetResult(true);

            public async Task<공간LHCellPreviewData> PreviewCellsAsync(
                공간LHCellPreviewRequestData request,
                CancellationToken cancellationToken)
            {
                var call = Interlocked.Increment(ref requestCount);
                if (call == 1)
                {
                    firstStarted.TrySetResult(true);
                    await releaseFirst.Task;
                }
                cancellationToken.ThrowIfCancellationRequested();
                return await fixture.PreviewCellsAsync(request, cancellationToken);
            }
        }
    }
}
