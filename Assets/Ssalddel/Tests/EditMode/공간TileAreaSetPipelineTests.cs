using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 공간TileAreaSetPipelineTests
    {
        [TestCase(0, 8000)]
        [TestCase(1, 2000)]
        [TestCase(2, 500)]
        public void EPSG5186고정격자는_단계별Tile고유식별자와크기를_유지한다(
            int level, int expectedSize)
        {
            var value = 공간TileKey.FromCoordinates(level, 309961.186d, 519763.258d);

            Assert.That(value.SizeMeters, Is.EqualTo(expectedSize));
            Assert.That(value.StableId,
                Is.EqualTo($"kr5186:l{level}:{(int)Math.Floor(309961.186d / expectedSize)}:{(int)Math.Floor(519763.258d / expectedSize)}"));
            Assert.That(value.Bounds.Validate(), Is.True);
        }

        [Test]
        public void 인접Tile은_경계좌표를_공유한다()
        {
            var left = new 공간TileKey { Level = 2, X = 619, Y = 1039 };
            var right = new 공간TileKey { Level = 2, X = 620, Y = 1039 };

            Assert.That(left.Bounds.MaxEasting, Is.EqualTo(right.Bounds.MinEasting));
            Assert.That(left.Bounds.MinNorthing, Is.EqualTo(right.Bounds.MinNorthing));
            Assert.That(left.Bounds.MaxNorthing, Is.EqualTo(right.Bounds.MaxNorthing));
        }

        [Test]
        public void 평창군2024통계는_기타나지와하천호소를_분리한다()
        {
            var value = 평창군공간PipelineFixture.CreateCompositionProfile();
            var water = value.Targets.Single(item =>
                item.TargetCode == 토지피복CompositionTargetCodes.InlandWater);
            var bare = value.Targets.Single(item =>
                item.TargetCode == 토지피복CompositionTargetCodes.BareGround);

            Assert.That(value.TotalAreaSquareKm, Is.EqualTo(1464.2839d).Within(.00001d));
            Assert.That(water.TargetAreaSquareKm, Is.EqualTo(9.2042d).Within(.00001d));
            Assert.That(bare.TargetAreaSquareKm, Is.EqualTo(23.6943d).Within(.00001d));
            Assert.That(value.Targets.Sum(item => item.TargetAreaSquareKm),
                Is.EqualTo(1297.4244d).Within(.0001d));
            Assert.That(1d - value.Targets.Sum(item => item.TargetAreaRatio),
                Is.EqualTo(.113953d).Within(.00001d));
        }

        [Test]
        public void 면적비율과_Synty개체예산은_분리되고_40Percent상한을지킨다()
        {
            var value = 평창군공간PipelineFixture.CreateCompositionProfile();

            Assert.That(value.Targets.Sum(item => item.OverviewBudget),
                Is.EqualTo(value.OverviewTileBudget));
            Assert.That(value.Targets.Sum(item => item.RegionBudget),
                Is.EqualTo(value.RegionTileBudget));
            Assert.That(value.Targets.Sum(item => item.TaskBudget),
                Is.EqualTo(value.TaskTileBudget));
            Assert.That(value.Targets.Max(item => item.RegionBudget),
                Is.LessThanOrEqualTo((int)(value.RegionTileBudget * .4d)));
            Assert.That(value.Targets.Max(item => item.TaskBudget),
                Is.LessThanOrEqualTo((int)(value.TaskTileBudget * .4d)));
            Assert.That(value.Targets, Has.All.Matches<토지피복CompositionTarget>(item =>
                item.RegionBudget >= 1 && item.TaskBudget >= 3));
        }

        [Test]
        public void 후보면적이부족하면_새로만들지않고_UnresolvedTargetArea로남긴다()
        {
            var profile = 평창군공간PipelineFixture.CreateCompositionProfile();
            var value = 토지피복CompositionAllocator.Allocate(profile,
                new[]
                {
                    new 토지피복CandidateGroupData
                    {
                        CandidateGroupCode = 토지피복CandidateGroupCodes.Agriculture,
                        CandidateAreaSquareKm = 10d,
                    },
                });
            var agriculture = value.Where(item => profile.Targets.Single(target =>
                target.TargetCode == item.TargetCode).CandidateGroupCode ==
                토지피복CandidateGroupCodes.Agriculture).ToArray();

            Assert.That(agriculture.Sum(item => item.AllocatedAreaSquareKm),
                Is.EqualTo(10d).Within(.00001d));
            Assert.That(agriculture, Has.All.Matches<토지피복AllocationResult>(item =>
                item.AllocatedAreaSquareKm <= item.TargetAreaSquareKm
                && item.UnresolvedTargetAreaSquareKm > 0d));
        }

        [Test]
        public void FarmHubTownAreaSet은_영역참조와ScenarioRoute를_분리한다()
        {
            var value = 평창군공간PipelineFixture.CreateManifest();

            Assert.That(value.Areas.Select(item => item.AreaKindCode),
                Is.EquivalentTo(new[] { WorldAreaKindCodes.Farm,
                    WorldAreaKindCodes.Hub, WorldAreaKindCodes.Town }));
            Assert.That(value.Links, Has.Length.EqualTo(2));
            Assert.That(value.Links, Has.All.Matches<WorldAreaLinkDefinition>(item =>
                item.LinkKindCode == WorldAreaLinkKindCodes.ScenarioRoute
                && item.EvidenceKind == 공간EvidenceKindCodes.ScenarioDerived));
            Assert.That(value.AreaSets.Single().AreaReferences,
                Is.EquivalentTo(value.Areas.Select(item => item.AreaStableId)));
        }

        [Test]
        public void 대관령Farm경관완결영역은_1km안의_2곱하기2_L2Tile로고정된다()
        {
            var manifest = 평창군공간PipelineFixture.CreateManifest();
            var value = manifest.CompletionAreas.Single();

            Assert.That(value.CompletionAreaStableId,
                Is.EqualTo("completion-area:sim:pyeongchang:daegwallyeong-farm.v1"));
            Assert.That(value.AreaStableId,
                Is.EqualTo("area:sim:pyeongchang:daegwallyeong-farm"));
            Assert.That(value.Bounds.WidthMeters, Is.EqualTo(1000d));
            Assert.That(value.Bounds.HeightMeters, Is.EqualTo(1000d));
            Assert.That(value.TaskTileReferences.Select(item => item.StableId),
                Is.EquivalentTo(new[]
                {
                    "kr5186:l2:700:1144", "kr5186:l2:701:1144",
                    "kr5186:l2:700:1145", "kr5186:l2:701:1145",
                }));
            Assert.That(value.Validate(), Is.True);
            Assert.That(value.CompletionHash, Is.EqualTo(value.CalculateHash()));
        }

        [Test]
        public void 첫완결영역은_4개Tile의필수공간Layer만_생성범위로삼는다()
        {
            var value = 평창군공간PipelineFixture.CreateManifest();
            var completion = value.CompletionAreas.Single();

            Assert.That(value.LayerTiles, Has.Length.EqualTo(12));
            Assert.That(value.LayerTiles.Select(item => item.TileKey.StableId).Distinct(),
                Is.EquivalentTo(completion.TaskTileReferences.Select(item => item.StableId)));
            Assert.That(value.LayerTiles.GroupBy(item => item.TileKey.StableId),
                Has.All.Matches<IGrouping<string, 공간LayerTileManifest>>(group =>
                    group.Select(item => item.LayerCode).OrderBy(item => item, StringComparer.Ordinal)
                        .SequenceEqual(new[]
                        {
                            공간LayerCodes.Elevation,
                            공간LayerCodes.LandCover,
                            공간LayerCodes.PlacementMask,
                        }.OrderBy(item => item, StringComparer.Ordinal))));
            Assert.That(value.LayerTiles.Count(item =>
                item.LayerCode == 공간LayerCodes.LandCover
                && item.CoverageStatusCode == 공간CoverageStatusCodes.Complete), Is.EqualTo(4));
            Assert.That(value.LayerTiles.Count(item =>
                item.CoverageStatusCode == 공간CoverageStatusCodes.Missing), Is.EqualTo(8));
        }

        [Test]
        public void 경관완결단계는_자료대기와Editor증거대기를_완료로오인하지않는다()
        {
            var value = 평창군공간PipelineFixture.CreateManifest()
                .CompletionAreas.Single();

            Assert.That(value.VerticalStages.Select(item => item.StageCode),
                Is.EqualTo(World경관완결단계Codes.All));
            Assert.That(value.VerticalStages.Single(item =>
                    item.StageCode == World경관완결단계Codes.PhysicalSpace).StatusCode,
                Is.EqualTo(WorldBuildValidationStatusCodes.WaitingForSpatialArtifact));
            Assert.That(value.VerticalStages.Single(item =>
                    item.StageCode == World경관완결단계Codes.UnityRuntime).StatusCode,
                Is.EqualTo(WorldBuildValidationStatusCodes.RequiresEditorEvidence));
            Assert.That(value.VerticalStages.Single(item =>
                    item.StageCode == World경관완결단계Codes.CompletionValidation).StatusCode,
                Is.EqualTo(WorldBuildValidationStatusCodes.RequiresEditorEvidence));
        }

        [Test]
        public void 같은Recipe와Profile은_같은TileFingerprint를_만든다()
        {
            var first = 평창군공간PipelineFixture.CreateManifest();
            var second = 평창군공간PipelineFixture.CreateManifest();

            Assert.That(first.RecipeHash, Is.EqualTo(second.RecipeHash));
            Assert.That(first.CompositionProfileHash,
                Is.EqualTo(second.CompositionProfileHash));
            Assert.That(first.CompletionAreas.Single().CompletionHash,
                Is.EqualTo(second.CompletionAreas.Single().CompletionHash));
            Assert.That(first.LayerTiles.Select(item => item.Fingerprint),
                Is.EqualTo(second.LayerTiles.Select(item => item.Fingerprint)));
            Assert.That(first.LayerTiles, Has.All.Matches<공간LayerTileManifest>(item =>
                item.Fingerprint == item.CalculateFingerprint()));
        }

        [Test]
        public void 원본표고와_시각표고과장은_업무판정에서_분리된다()
        {
            var value = 평창군공간PipelineFixture.CreateRecipe();

            Assert.That(value.PhysicalElevation.HeightUnit, Is.EqualTo("m"));
            Assert.That(value.PhysicalElevation.VerticalReference, Is.EqualTo("EGM2008 geoid"));
            Assert.That(value.PhysicalElevation.UsedForSlope, Is.True);
            Assert.That(value.PhysicalElevation.UsedForPlacementEligibility, Is.True);
            Assert.That(value.PhysicalElevation.UsedForHydrology, Is.True);
            Assert.That(value.VisualElevation.HeightExaggeration, Is.EqualTo(1.35d));
            Assert.That(value.VisualElevation.PresentationOnly, Is.True);
            Assert.That(value.VisualElevation.Apply(100d), Is.EqualTo(135d));
        }

        [Test]
        public void Halo와Seed는_Tile내부순번이아닌_세계좌표를_사용한다()
        {
            var recipe = 평창군공간PipelineFixture.CreateRecipe();
            var first = 공간PipelineHash.WorldCoordinateSeed(
                recipe.DeterministicSeed, 350125.25d, 572440.75d, "tree-cluster");
            var second = 공간PipelineHash.WorldCoordinateSeed(
                recipe.DeterministicSeed, 350125.25d, 572440.75d, "tree-cluster");
            var neighbor = 공간PipelineHash.WorldCoordinateSeed(
                recipe.DeterministicSeed, 350126.25d, 572440.75d, "tree-cluster");

            Assert.That(recipe.TileGeneration.TaskHaloMeters, Is.EqualTo(60));
            Assert.That(recipe.TileGeneration.SeedStrategy, Is.EqualTo("world-coordinate-hash"));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.EqualTo(neighbor));
        }

        [Test]
        public void 면적배분결과와_경관CompositionPlan은_별도산출물이다()
        {
            var value = 평창군공간PipelineFixture.CreateManifest();

            Assert.That(value.CompositionPlan.SourceAllocationHash, Has.Length.EqualTo(64));
            Assert.That(value.CompositionPlan.Items, Has.Length.EqualTo(value.AllocationResults.Length));
            Assert.That(value.CompositionPlan.Items, Has.All.Matches<LandscapeCompositionItem>(item =>
                item.AllowedVisualKeys.Length > 0
                && item.MeaningConfidenceCode ==
                    공간MeaningConfidenceCodes.StatisticallyAllocated));
        }

        [Test]
        public void 중간검증8단계뒤_마지막시각자산연결단계와_ReferenceTile을_보존한다()
        {
            var value = 평창군공간PipelineFixture.CreateManifest();

            Assert.That(value.ValidationStages.Select(item => item.StageCode),
                Is.EquivalentTo(WorldBuildValidationStageCodes.All));
            Assert.That(WorldBuildValidationStageCodes.All.Last(),
                Is.EqualTo(WorldBuildValidationStageCodes.FinalVisualAssetBinding));
            Assert.That(value.ValidationStages.Single(item => item.StageCode ==
                    WorldBuildValidationStageCodes.FinalVisualAssetBinding).StatusCode,
                Is.EqualTo(WorldBuildValidationStatusCodes.RequiresEditorEvidence));
            Assert.That(value.ReferenceTile.TileKey.Level, Is.EqualTo(공간TileLevelCodes.Task));
            Assert.That(value.ReferenceTile.AuthoringKind, Is.EqualTo("HandAuthored"));
            Assert.That(value.ReferenceTile.CompositionPrinciples,
                Is.EqualTo(new[] { "큰 덩어리", "중간 군집", "작은 디테일", "연결 지점 강조" }));
            Assert.That(value.RenderingBudgets, Has.Length.EqualTo(3));
            Assert.That(value.RenderingBudgets.Single(item => item.LodLevel == 0)
                .RequiresClusterOrHlod, Is.True);
            Assert.That(value.RenderingBudgets.Single(item => item.LodLevel == 2)
                .MaximumTriangles, Is.GreaterThan(0));
        }

        [Test]
        public void RuntimeLoader는_카메라거리에따라_L0L1L2표현만_활성화한다()
        {
            var root = new GameObject("TileRoot");
            var overview = new GameObject("L0").transform;
            var region = new GameObject("L1").transform;
            var task = new GameObject("L2").transform;
            var cameraRoot = new GameObject("Camera");
            try
            {
                overview.SetParent(root.transform);
                region.SetParent(root.transform);
                task.SetParent(root.transform);
                var camera = cameraRoot.AddComponent<Camera>();
                var loader = root.AddComponent<공간TileLodLoader>();
                camera.transform.position = new Vector3(0f, 0f, 60f);
                loader.Configure(camera, overview, region, task, 48f, 25f);
                Assert.That(loader.ActiveLevel, Is.EqualTo(0));
                camera.transform.position = new Vector3(0f, 0f, 35f);
                loader.Refresh();
                Assert.That(loader.ActiveLevel, Is.EqualTo(1));
                camera.transform.position = new Vector3(0f, 0f, 10f);
                loader.Refresh();
                Assert.That(loader.ActiveLevel, Is.EqualTo(2));
                Assert.That(task.gameObject.activeSelf, Is.True);
                Assert.That(overview.gameObject.activeSelf, Is.False);
                Assert.That(region.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
            }
        }
    }
}
