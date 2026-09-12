using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
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
        public void 동적MapPlanner는_LH기본계층과분리된_가변창을계산한다()
        {
            var policy = new 공간동적MapWindowPolicyData
            {
                DetailRadius = 0,
                ActiveRadius = 1,
                PrefetchRadius = 2,
            };
            var window = 공간동적MapWindowPlanner.Plan(2801, 4581, policy, "E");

            Assert.That(window, Has.Length.EqualTo(25));
            Assert.That(window.Count(value => value.WindowRoleCode
                == 공간LHWorldCodes.Detail), Is.EqualTo(1));
            Assert.That(window.Count(value => value.WindowRoleCode
                == 공간LHWorldCodes.Active), Is.EqualTo(8));
            Assert.That(window.Count(value => value.WindowRoleCode
                == 공간LHWorldCodes.Prefetch), Is.EqualTo(16));

            var east = window.Single(value => value.CellX == 2803
                && value.CellY == 4581);
            var west = window.Single(value => value.CellX == 2799
                && value.CellY == 4581);
            Assert.That(east.Priority, Is.LessThan(west.Priority));
        }

        [Test]
        public void 동적Map창은_상세활성선행반경의_포함순서를검증한다()
        {
            var invalid = new 공간동적MapWindowPolicyData
            {
                DetailRadius = 2,
                ActiveRadius = 1,
                PrefetchRadius = 4,
            };

            Assert.Throws<InvalidOperationException>(() => invalid.Validate());
        }

        [Test]
        public void 동적배치Planner는_셀순서와무관하게_표면간격경계를지킨다()
        {
            var policy = new 공간동적배치PolicyData();
            var surface = new 평면공간동적표면Provider(1.25d);
            var first = 공간동적ObjectPlacementPlanner.Plan(
                "dynamic-placement-seed", 2801, 4581, 5, policy, surface);
            var repeat = 공간동적ObjectPlacementPlanner.Plan(
                "dynamic-placement-seed", 2801, 4581, 5, policy, surface);
            var east = 공간동적ObjectPlacementPlanner.Plan(
                "dynamic-placement-seed", 2802, 4581, 5, policy, surface);

            Assert.That(first.IsComplete, Is.True);
            Assert.That(first.배치점.Select(Signature),
                Is.EqualTo(repeat.배치점.Select(Signature)));
            Assert.That(first.배치점.All(value => value.LocalYMeters == 1.25d
                && value.ValidationCode == "Passed"), Is.True);
            AssertMinimumSpacing(first.배치점, first.배치점, policy, true);
            AssertMinimumSpacing(first.배치점, east.배치점, policy, false,
                cellOffsetXMeters: policy.셀크기Meters);
        }

        [Test]
        public void 동적배치Planner는_허용경사를넘는_표면을_배치하지않는다()
        {
            var result = 공간동적ObjectPlacementPlanner.Plan(
                "steep-surface", 10, 20, 2, new 공간동적배치PolicyData(),
                new 고정경사표면Provider(32d));

            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.배치점, Is.Empty);
            Assert.That(result.거부원인Codes, Does.Contain("SlopeTooSteep"));
            Assert.That(result.거부원인Codes,
                Does.Contain("PlacementCandidateExhausted"));
        }

        [Test]
        public async Task 로컬동적셀의_절차배치는_공통표면검증계보를남긴다()
        {
            var repository = new 로컬공간LHWorldEngine();
            var response = await repository.PreviewCellsAsync(
                Request("placement-lineage", 2801, 4581), CancellationToken.None);
            var center = response.Cells.Single(value => value.CellX == 2801
                && value.CellY == 4581);
            var generated = center.Placements.Where(value => !value.FixedAnchor).ToArray();

            Assert.That(generated, Has.Length.InRange(3, 5));
            Assert.That(generated.All(value => value.PlacementRuleRevision
                == 공간동적ObjectPlacementPlanner.RuleRevision), Is.True);
            Assert.That(generated.All(value => value.PlacementValidationCode == "Passed"),
                Is.True);
        }

        [Test]
        public void 결정적Noise표면은_seed를재현하고_다른seed를구분한다()
        {
            var first = new 결정적ValueNoise공간표면Provider("noise-seed-a");
            var repeat = new 결정적ValueNoise공간표면Provider("noise-seed-a");
            var other = new 결정적ValueNoise공간표면Provider("noise-seed-b");

            Assert.That(first.TrySample(350187.5d, 572687.5d, out var a), Is.True);
            Assert.That(repeat.TrySample(350187.5d, 572687.5d, out var a2), Is.True);
            Assert.That(other.TrySample(350187.5d, 572687.5d, out var b), Is.True);
            Assert.That(a.표면높이Meters, Is.EqualTo(a2.표면높이Meters));
            Assert.That(a.표면경사Degrees, Is.EqualTo(a2.표면경사Degrees));
            Assert.That(b.표면높이Meters, Is.Not.EqualTo(a.표면높이Meters));
            Assert.That(first.SurfaceConfigurationHashSha256,
                Is.EqualTo(repeat.SurfaceConfigurationHashSha256));
        }

        [Test]
        public async Task Noise셀상태사본은_인접경계높이를공유하고_나무후보를표면에맞춘다()
        {
            var surface = new 결정적ValueNoise공간표면Provider(
                "noise-boundary", amplitude: 2d, wavelength: 110d);
            var repository = new 로컬공간LHWorldEngine("noise-boundary",
                localSurfaceProvider: surface);
            var response = await repository.PreviewCellsAsync(
                Request("noise-grid", 2801, 4581), CancellationToken.None);
            var center = response.Cells.Single(value => value.CellX == 2801
                && value.CellY == 4581);
            var east = response.Cells.Single(value => value.CellX == 2802
                && value.CellY == 4581);
            var resolution = center.Surface.SampleResolution;

            Assert.That(center.Surface.SurfaceModeCode,
                Is.EqualTo("DeterministicValueNoise"));
            for (var row = 0; row < resolution; row++)
            {
                var centerEast = center.Surface.HeightMeters[
                    row * resolution + resolution - 1];
                var eastWest = east.Surface.HeightMeters[row * resolution];
                Assert.That(centerEast, Is.EqualTo(eastWest));
            }
            foreach (var placement in center.Placements)
            {
                var worldX = center.CellX * center.Surface.CellSizeMeters
                             + placement.LocalXMeters;
                var worldZ = center.CellY * center.Surface.CellSizeMeters
                             + placement.LocalZMeters;
                Assert.That(surface.TrySample(worldX, worldZ, out var sample), Is.True);
                Assert.That(placement.LocalYMeters,
                    Is.EqualTo(sample.표면높이Meters).Within(.0000001d));
                Assert.That(placement.SurfaceSlopeDegrees,
                    Is.LessThanOrEqualTo(18d));
            }
        }

        [Test]
        public async Task Noise셀은_높이Mesh와Collider를만들고_배치가시하단을표면에맞춘다()
        {
            var root = new GameObject("LHNoiseSurfaceTestRoot");
            var player = new GameObject("Player");
            var generated = new GameObject("Generated");
            generated.transform.SetParent(root.transform);
            var catalog = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                "Assets/Ssalddel/Presentation/World/Catalogs/평창공간문법CompositionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var engine = root.AddComponent<공간LHStreamingEngine>();
            engine.Configure(player.transform, generated.transform, null, catalog,
                Vector3.zero, 125f, "noise-surface", 0);
            var surface = new 결정적ValueNoise공간표면Provider(
                "noise-surface", amplitude: 2d, wavelength: 110d);
            try
            {
                await engine.InitializeAsync(new 로컬공간LHWorldEngine("noise-surface",
                    localSurfaceProvider: surface));
                engine.DrainAssemblyForTests();

                var center = generated.transform.Find("LHCell_kr5186_l3_2801_4581");
                Assert.That(center, Is.Not.Null);
                var floor = center!.Find("PlayerTraversalFloor");
                Assert.That(floor, Is.Not.Null);
                var mesh = floor!.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(17 * 17));
                Assert.That(mesh.bounds.size.y, Is.GreaterThan(.1f));
                Assert.That(floor.GetComponent<MeshCollider>().sharedMesh,
                    Is.SameAs(mesh));

                var placements = center.GetComponentsInChildren<Transform>(true)
                    .Where(value => value.name.StartsWith("ExteriorPlacement_",
                        StringComparison.Ordinal)).ToArray();
                Assert.That(placements, Is.Not.Empty);
                var stages = engine.WorldPresentationCoordinator.StageHistoryOf(
                    "kr5186:l3:2801:4581");
                Assert.That(stages[0], Is.EqualTo(
                    World공간표현조립Coordinator.LhSurfacePrepared));
                Assert.That(
                    stages[1] == World공간표현조립Coordinator.SkyApplied
                    || stages[1] == World공간표현조립Coordinator.SkyUnavailable,
                    Is.True);
                Assert.That(stages[2], Is.EqualTo(
                    World공간표현조립Coordinator.ExteriorApplied));
                Assert.That(stages[3], Is.EqualTo(
                    World공간표현조립Coordinator.InteriorApplied));
                Assert.That(stages[4], Is.EqualTo(
                    World공간표현조립Coordinator.DirectionalLightingValidated));
                Assert.That(stages[5], Is.EqualTo(
                    World공간표현조립Coordinator.PresentationReady));
                var lightingReceipts = engine.WorldPresentationCoordinator
                    .DirectionalLightingReceiptsOf("kr5186:l3:2801:4581");
                Assert.That(lightingReceipts, Is.Not.Empty);
                Assert.That(lightingReceipts,
                    Has.All.Matches<방향광표현ValidationReceipt>(value =>
                        value.Passed));
                var authorityTrace = new[]
                {
                    EngineTrace(1,
                        SimulationEngineInteractionComponentCodes
                            .WorldInteractionPipeline,
                        SimulationEngineInteractionPhaseCodes.Preview,
                        SimulationEngineInteractionComponentKinds.Orchestration),
                    EngineTrace(2,
                        SimulationEngineInteractionComponentCodes
                            .WorldInteractionPipeline,
                        SimulationEngineInteractionPhaseCodes.Confirm,
                        SimulationEngineInteractionComponentKinds.Orchestration),
                    EngineTrace(3,
                        SimulationEngineInteractionComponentCodes.AuthorityCore,
                        SimulationEngineInteractionPhaseCodes.AuthorityCommit,
                        SimulationEngineInteractionComponentKinds.Authority),
                    EngineTrace(4,
                        SimulationEngineInteractionComponentCodes
                            .WorldInteractionPipeline,
                        SimulationEngineInteractionPhaseCodes.ReturnProjection,
                        SimulationEngineInteractionComponentKinds.Orchestration),
                };
                var interaction = engine.WorldPresentationCoordinator
                    .RecordNatureInteractionPresentation(authorityTrace);
                Assert.That(interaction.Select(value => value.ComponentCode),
                    Is.EqualTo(new[]
                    {
                        SimulationEngineInteractionComponentCodes
                            .WorldInteractionPipeline,
                        SimulationEngineInteractionComponentCodes
                            .WorldInteractionPipeline,
                        SimulationEngineInteractionComponentCodes.AuthorityCore,
                        SimulationEngineInteractionComponentCodes
                            .WorldInteractionPipeline,
                        SimulationEngineInteractionComponentCodes.LhSurface,
                        SimulationEngineInteractionComponentCodes.SkyPresentation,
                        SimulationEngineInteractionComponentCodes.ExteriorPlacement,
                        SimulationEngineInteractionComponentCodes.InteriorPlacement,
                        SimulationEngineInteractionComponentCodes.WorldPresentation,
                    }));
                Assert.That(interaction.Where(value => value.ComponentKindCode ==
                            SimulationEngineInteractionComponentKinds.Presentation),
                    Has.All.Matches<SimulationPlayableLoopEngineTraceEntry>(value =>
                        value.BeforeAuthorityRevision == 42
                        && value.AfterAuthorityRevision == 42));
                foreach (var placement in placements)
                {
                    Assert.That(배치객체표면정렬Utility.TryGetVisibleBounds(
                        placement, out var bounds), Is.True);
                    Assert.That(surface.TrySample(
                        2801d * 125d + placement.localPosition.x,
                        4581d * 125d + placement.localPosition.z,
                        out var sample), Is.True);
                    Assert.That(bounds.min.y,
                        Is.EqualTo((float)sample.표면높이Meters
                                   + 배치객체표면정렬Utility.DefaultSurfaceClearance)
                            .Within(.02f));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }

        private static SimulationPlayableLoopEngineTraceEntry EngineTrace(
            int sequence, string componentCode, string phaseCode,
            string kindCode) => new()
        {
            PlayableLoopStableId = "playable-loop:nature-night-day2.v1",
            WorldInteractionId = "WI-NATURE-14",
            CommandId = "command:test:sleep",
            AuthorityLocationCode = "LocalProcess",
            ComponentCode = componentCode,
            ComponentKindCode = kindCode,
            ComponentRevision = "test.r1",
            PhaseCode = phaseCode,
            Sequence = sequence,
            InputHashSha256 = "input",
            OutputHashSha256 = "output",
            StatusCode = SimulationEngineInteractionStatusCodes.Executed,
            BeforeAuthorityRevision = kindCode ==
                                      SimulationEngineInteractionComponentKinds
                                          .Authority ? 41 : 42,
            AfterAuthorityRevision = 42,
        };

        [Test]
        public async Task 로컬엔진은_H5인계Topology가없으면_조용히후퇴하지않고_사용불가를표시한다()
        {
            var repository = new 로컬공간LHWorldEngine();

            var response = await repository.PreviewCellsAsync(
                Request("handover-unavailable", 2801, 4581), CancellationToken.None);

            Assert.That(response.AreaSetHandover.AvailabilityCode,
                Is.EqualTo(공간AreaSetHandoverCodes.NotAvailable));
            Assert.That(response.AreaSetHandover.BlockingReasonCodes,
                Does.Contain("AreaSetHandoverTopologyPackageUnavailable"));
            Assert.That(response.AreaSetHandover.Candidates, Is.Empty);
            Assert.That(response.AreaSetHandover.ChangesCurrentAreaSet, Is.False);
            Assert.That(response.AreaSetHandover.RequiresExplicitTraversalConfirm, Is.True);
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
                Assert.That(engine.ActiveAreaSetHandover.AvailabilityCode,
                    Is.EqualTo(공간AreaSetHandoverCodes.NotAvailable));
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
        public async Task PlayerTraversalFloor는_플레이어눈높이가아닌_셀공간기준면에생성된다()
        {
            var root = new GameObject("LHFloorSurfaceTestRoot");
            var player = new GameObject("Player");
            player.transform.position = new Vector3(15f, 1.74f, 10f);
            var generated = new GameObject("Generated");
            generated.transform.SetParent(root.transform);
            generated.transform.position = new Vector3(0f, .15f, 0f);
            var engine = root.AddComponent<공간LHStreamingEngine>();
            engine.Configure(player.transform, generated.transform, null, null,
                player.transform.position, 125f, "floor-surface", 0);
            try
            {
                await engine.InitializeAsync(new 로컬공간LHWorldEngine());
                engine.DrainAssemblyForTests();

                var floor = generated.GetComponentsInChildren<Transform>(true)
                    .Single(value => value.name == "PlayerTraversalFloor");
                Assert.That(floor.position.y + floor.lossyScale.y * .5f,
                    Is.EqualTo(generated.transform.position.y - .05f)
                        .Within(.001f));
                Assert.That(floor.position.y + floor.lossyScale.y * .5f,
                    Is.LessThan(player.transform.position.y - 1f));
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

        private static string Signature(공간동적배치점Data value)
            => $"{value.배치StableId}|{value.LocalXMeters:R}|{value.LocalYMeters:R}|"
               + $"{value.LocalZMeters:R}|{value.RotationDegrees:R}|{value.UniformScale:R}";

        private static void AssertMinimumSpacing(
            공간동적배치점Data[] left,
            공간동적배치점Data[] right,
            공간동적배치PolicyData policy,
            bool sameCollection,
            double cellOffsetXMeters = 0d)
        {
            for (var leftIndex = 0; leftIndex < left.Length; leftIndex++)
            for (var rightIndex = 0; rightIndex < right.Length; rightIndex++)
            {
                if (sameCollection && leftIndex >= rightIndex) continue;
                var dx = left[leftIndex].LocalXMeters
                         - (right[rightIndex].LocalXMeters + cellOffsetXMeters);
                var dz = left[leftIndex].LocalZMeters - right[rightIndex].LocalZMeters;
                Assert.That(Math.Sqrt(dx * dx + dz * dz),
                    Is.GreaterThanOrEqualTo(policy.최소객체간격Meters - .000001d));
            }
        }

        private sealed class 고정경사표면Provider : I공간동적표면Provider
        {
            private readonly double slopeDegrees;

            public 고정경사표면Provider(double slopeDegrees)
                => this.slopeDegrees = slopeDegrees;

            public string SurfaceModeCode => "FixedSlope";
            public string SurfaceRuleRevision => "test.fixed-slope.r1";
            public string SurfaceConfigurationHashSha256 => new string('a', 64);

            public bool TrySample(double worldX, double worldZ,
                out 공간동적표면표본Data sample)
            {
                sample = new 공간동적표면표본Data
                {
                    배치허용 = true,
                    표면경사Degrees = slopeDegrees,
                };
                return true;
            }
        }

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
