using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 정적경관배치PlanPipelineTests
    {
        [Test]
        public void 평창기본계획은_여덟구획과Nature치환_조명Profile을함께선언한다()
        {
            var plan = 정적경관배치PlanPipeline.LoadMergedPlan();

            Assert.That(plan.SchemaVersion, Is.EqualTo(3));
            Assert.That(plan.PlanStableId,
                Is.EqualTo("world-placement:pyeongchang-farm-hub-town-v1"));
            Assert.That(plan.PlanRevision, Is.EqualTo("pyeongchang-static-scenery.v4"));
            Assert.That(plan.Placements.Length, Is.GreaterThan(100));
            Assert.That(plan.ContainerTransforms, Has.Length.EqualTo(8));
            Assert.That(plan.ContainerTransforms.Select(item => item.ContainerStableId),
                Is.EquivalentTo(plan.Placements.Select(item => item.TargetContainerStableId)
                    .Distinct(StringComparer.Ordinal)));
            Assert.That(plan.Placements.Select(item => item.TargetContainerStableId)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(8));
            Assert.That(plan.Placements, Has.None.Matches<정적경관배치ItemData>(item =>
                item.TargetContainerStableId == 정적경관배치ContainerStableIds.ScenicRoot));
            Assert.That(plan.Placements.Count(item =>
                item.TargetContainerStableId == 정적경관배치ContainerStableIds.FarmSouthWest),
                Is.EqualTo(10));
            Assert.That(plan.Placements.Count(item =>
                item.TargetContainerStableId == 정적경관배치ContainerStableIds.JinbuHub),
                Is.EqualTo(8));
            Assert.That(plan.Placements.Count(item =>
                item.TargetContainerStableId == 정적경관배치ContainerStableIds.PyeongchangTown),
                Is.EqualTo(13));
            Assert.That(plan.Placements.Count(item =>
                item.AssetReferenceKindCode
                == 정적경관배치AssetReferenceKindCodes.CompositionKey), Is.EqualTo(34));
            Assert.That(plan.Placements.Count(item =>
                item.AssetReferenceKindCode
                == 정적경관배치AssetReferenceKindCodes.CompositionKey
                && item.AssetKey.StartsWith("nature:", StringComparison.Ordinal)),
                Is.EqualTo(24));
            Assert.That(plan.Placements, Has.None.Matches<정적경관배치ItemData>(item =>
                item.AssetKey.StartsWith("nature:수변 완충지", StringComparison.Ordinal)
                || item.AssetKey.StartsWith("nature:개울 회랑", StringComparison.Ordinal)));
            var renderingProfile = 평창군경관RenderingFixture.Create();
            Assert.That(plan.RenderingProfileStableId,
                Is.EqualTo(renderingProfile.ProfileStableId));
            Assert.That(plan.RenderingProfileRevision,
                Is.EqualTo(renderingProfile.RuleRevision));
            Assert.That(plan.RenderingProfileHashSha256,
                Is.EqualTo(경관RenderingProfileHash.Compute(renderingProfile)));
            Assert.That(plan.Placements, Has.None.Matches<정적경관배치ItemData>(item =>
                item.AssetKey.Contains("Assets/") || item.AssetKey.Contains(".prefab")
                || item.AssetKey.Contains("\\")));
            Assert.That(정적경관배치PlanHash.Compute(plan), Has.Length.EqualTo(64));
        }

        [Test]
        public void 대관령감자작물은_밭고랑안쪽격자와같은방향으로배치된다()
        {
            var plan = 정적경관배치PlanPipeline.LoadMergedPlan();
            var crops = plan.Placements.Where(item => item.Enabled
                    && item.TargetContainerStableId
                    == 정적경관배치ContainerStableIds.FarmSouthEast
                    && item.AssetKey == 법정동경관VisualKeys.Potato)
                .OrderBy(item => item.PlacementStableId, StringComparer.Ordinal)
                .ToArray();

            Assert.That(crops, Has.Length.EqualTo(24));
            Assert.That(crops, Has.All.Matches<정적경관배치ItemData>(item =>
                Mathf.Abs(Mathf.DeltaAngle(item.RotationY, 8f)) < .001f));

            var report = 정적경관배치PlanPipeline.ValidateAndStage();
            Assert.That(report.Issues, Has.None.Matches<정적경관배치ValidationIssueData>(
                item => item.IssueCode == "CropOutsideSoilRowBounds"
                    || item.IssueCode == "CropRowRotationMismatch"));
        }

        [Test]
        public void 보정계획은_배치Hash가맞을때만_수정과비활성화를적용한다()
        {
            var basePlan = 정적경관배치PlanPipeline.LoadMergedPlan();
            var first = basePlan.Placements[0];
            var second = basePlan.Placements[1];
            var expectedX = first.Position.X + .75f;
            var overridePlan = new 정적경관배치OverridePlanData
            {
                OverrideStableId = "override:test",
                BasePlanStableId = basePlan.PlanStableId,
                ExpectedBasePlanHashSha256 = 정적경관배치PlanHash.Compute(basePlan),
                Changes = new[]
                {
                    new 정적경관배치OverrideChangeData
                    {
                        OperationCode = 정적경관배치OverrideOperationCodes.Modify,
                        PlacementStableId = first.PlacementStableId,
                        ExpectedPlacementHashSha256 =
                            정적경관배치PlanHash.ComputePlacement(first),
                        Adjustment = new 정적경관배치AdjustmentData
                        {
                            Position = new 정적경관배치PositionData
                            {
                                X = expectedX,
                                Z = first.Position.Z,
                            },
                        },
                    },
                    new 정적경관배치OverrideChangeData
                    {
                        OperationCode = 정적경관배치OverrideOperationCodes.Disable,
                        PlacementStableId = second.PlacementStableId,
                        ExpectedPlacementHashSha256 =
                            정적경관배치PlanHash.ComputePlacement(second),
                    },
                },
            };

            var merged = 정적경관배치PlanMerger.Merge(basePlan, overridePlan);

            Assert.That(merged.Placements.Single(item =>
                item.PlacementStableId == first.PlacementStableId).Position.X,
                Is.EqualTo(expectedX));
            Assert.That(merged.Placements.Single(item =>
                item.PlacementStableId == second.PlacementStableId).Enabled, Is.False);
        }

        [Test]
        public void 오래된보정Hash는_자동적용하지않는다()
        {
            var basePlan = 정적경관배치PlanPipeline.LoadMergedPlan();
            var target = basePlan.Placements[0];
            var overridePlan = new 정적경관배치OverridePlanData
            {
                OverrideStableId = "override:stale-test",
                BasePlanStableId = basePlan.PlanStableId,
                ExpectedBasePlanHashSha256 = new string('0', 64),
                Changes = new[]
                {
                    new 정적경관배치OverrideChangeData
                    {
                        OperationCode = 정적경관배치OverrideOperationCodes.Disable,
                        PlacementStableId = target.PlacementStableId,
                        ExpectedPlacementHashSha256 =
                            정적경관배치PlanHash.ComputePlacement(target),
                    },
                },
            };

            var error = Assert.Throws<InvalidOperationException>(() =>
                정적경관배치PlanMerger.Merge(basePlan, overridePlan));

            Assert.That(error!.Message, Does.Contain(
                정적경관배치PlanValidator.InvalidCode));
        }

        [Test]
        public void 같은배치를_한보정계획에서두번수정할수없다()
        {
            var basePlan = 정적경관배치PlanPipeline.LoadMergedPlan();
            var target = basePlan.Placements[0];
            var change = new 정적경관배치OverrideChangeData
            {
                OperationCode = 정적경관배치OverrideOperationCodes.Disable,
                PlacementStableId = target.PlacementStableId,
                ExpectedPlacementHashSha256 =
                    정적경관배치PlanHash.ComputePlacement(target),
            };
            var overridePlan = new 정적경관배치OverridePlanData
            {
                OverrideStableId = "override:duplicate-test",
                BasePlanStableId = basePlan.PlanStableId,
                ExpectedBasePlanHashSha256 = 정적경관배치PlanHash.Compute(basePlan),
                Changes = new[] { change, change },
            };

            var error = Assert.Throws<InvalidOperationException>(() =>
                정적경관배치PlanMerger.Merge(basePlan, overridePlan));

            Assert.That(error!.Message, Does.Contain(
                정적경관배치PlanValidator.InvalidCode));
        }

        [Test]
        public void 계획검증과Staging생성은_현재Scene을변경하지않는다()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            var objectCount = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            var report = 정적경관배치PlanPipeline.ValidateAndStage();

            Assert.That(report.ErrorCount, Is.Zero);
            Assert.That(report.CanStage, Is.True);
            Assert.That(report.EnabledPlacementCount,
                Is.EqualTo(정적경관배치PlanPipeline.LoadMergedPlan().Placements
                    .Count(item => item.Enabled)));
            Assert.That(report.StagingPrefabPaths, Has.Length.EqualTo(8));
            Assert.That(report.StagingPrefabPaths, Has.None.EndsWith(
                "pyeongchang-scenic-root.prefab"));
            Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.EqualTo(wasDirty));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(objectCount));
        }

        [Test]
        public void 모든StagingPrefab은_계획영수증과배치View를가진다()
        {
            var report = 정적경관배치PlanPipeline.ValidateAndStage();
            var instanceCount = 0;

            foreach (var path in report.StagingPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var receipt = prefab.GetComponent<정적경관배치ReceiptView>();
                Assert.That(receipt, Is.Not.Null, path);
                Assert.That(receipt!.ValidateWiring(), Is.True, path);
                Assert.That(receipt.MergedPlanHashSha256,
                    Is.EqualTo(report.MergedPlanHashSha256), path);
                Assert.That(receipt.RenderingProfileHashSha256,
                    Is.EqualTo(report.RenderingProfileHashSha256), path);
                var instances = prefab.GetComponentsInChildren<정적경관배치InstanceView>(true);
                Assert.That(instances, Has.All.Matches<정적경관배치InstanceView>(
                    item => item.ValidateWiring()));
                instanceCount += instances.Length;
            }

            Assert.That(instanceCount, Is.EqualTo(report.EnabledPlacementCount));
        }

        [Test]
        public void 배치와검토JSON파일은_Prefab경로나Guid를저장하지않는다()
        {
            var json = string.Join("\n", Directory.GetFiles(
                    정적경관배치PlanPipeline.PlanDirectory, "*.json")
                .OrderBy(item => item, StringComparer.Ordinal)
                .Select(File.ReadAllText));

            Assert.That(json, Does.Not.Contain("Assets/"));
            Assert.That(json, Does.Not.Contain(".prefab"));
            Assert.That(json, Does.Not.Contain("\"guid\""));
        }

        [Test]
        public void 경관기획서는_계획과AreaSet을명시하고_개행과무관하게같은Hash를만든다()
        {
            var metadata = 정적경관배치ReviewService.LoadBriefMetadata();
            var lf = "---\nbrief: value\n---\n\n## 제목\n";
            var crlf = lf.Replace("\n", "\r\n");

            Assert.That(metadata.PlanStableId,
                Is.EqualTo("world-placement:pyeongchang-farm-hub-town-v1"));
            Assert.That(metadata.AreaSetStableId,
                Is.EqualTo("pyeongchang-farm-hub-town-v1"));
            Assert.That(metadata.BriefSchemaVersion, Is.EqualTo(4));
            Assert.That(metadata.NatureGuideStableId,
                Is.EqualTo("landscape-guide:polygon-nature-forest"));
            Assert.That(metadata.NatureGuideHashSha256,
                Is.EqualTo(정적경관배치ReviewService.ComputeNatureGuideHash()));
            Assert.That(metadata.FarmGuideHashSha256,
                Is.EqualTo(정적경관배치ReviewService.ComputeFarmGuideHash()));
            Assert.That(metadata.TownGuideHashSha256,
                Is.EqualTo(정적경관배치ReviewService.ComputeTownGuideHash()));
            Assert.That(metadata.CityGuideHashSha256,
                Is.EqualTo(정적경관배치ReviewService.ComputeCityGuideHash()));
            Assert.That(metadata.FourPackGuideBundleHashSha256,
                Is.EqualTo(정적경관배치ReviewService.ComputeFourPackGuideBundleHash()));
            Assert.That(metadata.CompositionCatalogRevision,
                Is.EqualTo(정적경관배치PlanPipeline.CompositionCatalogRevision));
            var renderingProfile = 평창군경관RenderingFixture.Create();
            Assert.That(metadata.RenderingProfileStableId,
                Is.EqualTo(renderingProfile.ProfileStableId));
            Assert.That(metadata.RenderingProfileRevision,
                Is.EqualTo(renderingProfile.RuleRevision));
            Assert.That(metadata.RenderingProfileHashSha256,
                Is.EqualTo(경관RenderingProfileHash.Compute(renderingProfile)));
            Assert.That(
                정적경관배치PlanHash.Sha256(
                    정적경관배치ReviewService.NormalizeBrief(lf)),
                Is.EqualTo(정적경관배치PlanHash.Sha256(
                    정적경관배치ReviewService.NormalizeBrief(crlf))));
            Assert.That(정적경관배치ReviewService.ComputeBriefHash(), Has.Length.EqualTo(64));
        }

        [Test]
        public void 네팩배치기준은_구성대장과승인BundleHash를고정한다()
        {
            var metadata = 정적경관배치ReviewService.LoadNatureGuideMetadata();
            var brief = File.ReadAllText(정적경관배치ReviewService.BriefPath);
            var guide = File.ReadAllText(정적경관배치ReviewService.NatureGuidePath);
            var expectedRows = new[]
            {
                "| 활엽수림 군집 | 수관 | 산림 | 0~35도 |",
                "| 침엽수림 군집 | 수관 | 산림 | 0~45도 |",
                "| 혼효림 군집 | 수관 | 산림 | 0~40도 |",
                "| 수변 완충지 | 수변 경계 | 수계 | 0~12도 | 필수 |",
                "| 바위 절개지 | 지형 전환 | 기타 나지 | 15~70도 |",
                "| 산 능선 | 원경 | 산림·기타 나지 | 0~90도 |",
                "| 숲 가장자리 | 하층 식생 | 산림·경작지 | 0~35도 |",
                "| 개울 회랑 | 수변 경계 | 수계 | 0~10도 | 필수 |",
            };

            Assert.That(metadata.SourcePackCode, Is.EqualTo("nature"));
            Assert.That(metadata.PresentationOnly, Is.True);
            Assert.That(metadata.CompositionCatalogRevision,
                Is.EqualTo(정적경관배치PlanPipeline.CompositionCatalogRevision));
            Assert.That(expectedRows, Has.All.Matches<string>(guide.Contains));
            Assert.That(guide, Does.Not.Contain("Assets/"));
            Assert.That(guide, Does.Not.Contain(".prefab"));
            Assert.That(guide, Does.Not.Contain("GUID"));
            var packGuides = new[]
            {
                정적경관배치ReviewService.FarmGuidePath,
                정적경관배치ReviewService.TownGuidePath,
                정적경관배치ReviewService.CityGuidePath,
            }.Select(File.ReadAllText).ToArray();
            Assert.That(packGuides, Has.All.Matches<string>(value =>
                !value.Contains("Assets/") && !value.Contains(".prefab")
                && !value.Contains("GUID")));

            var currentHash = 정적경관배치ReviewService.ComputeBriefHash();
            var changedGuideHash = 정적경관배치ReviewService.ComputeBriefBundleHash(
                brief, guide + "\n검토되지 않은 변경");
            Assert.That(changedGuideHash, Is.Not.EqualTo(currentHash));
        }

        [Test]
        public void Scene적용승인은_기획서와세계획Hash가모두일치할때만유효하다()
        {
            var basePlan = 정적경관배치PlanPipeline.LoadBasePlan();
            var overridePlan = 정적경관배치PlanPipeline.LoadOverridePlan();
            var mergedPlan = 정적경관배치PlanMerger.Merge(basePlan, overridePlan);
            var brief = 정적경관배치ReviewService.LoadBriefMetadata();
            var briefHash = 정적경관배치ReviewService.ComputeBriefHash();
            var baseHash = 정적경관배치PlanHash.Compute(basePlan);
            var overrideHash = 정적경관배치PlanPipeline.ComputeOverrideHash(overridePlan);
            var mergedHash = 정적경관배치PlanHash.Compute(mergedPlan);
            var receipt = new 정적경관배치ReviewReceiptData
            {
                ReviewStableId = "review:test",
                BriefStableId = brief.BriefStableId,
                BriefRevision = brief.BriefRevision,
                BriefHashSha256 = briefHash,
                PlanStableId = brief.PlanStableId,
                BasePlanHashSha256 = baseHash,
                OverrideHashSha256 = overrideHash,
                MergedPlanHashSha256 = mergedHash,
                RenderingProfileStableId = brief.RenderingProfileStableId,
                RenderingProfileRevision = brief.RenderingProfileRevision,
                RenderingProfileHashSha256 = brief.RenderingProfileHashSha256,
                ReviewStateCode = 정적경관배치ReviewStateCodes.ApprovedForSceneApply,
                ReviewedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            };

            var approved = 정적경관배치ReviewService.Evaluate(
                brief, briefHash, receipt, baseHash, overrideHash, mergedHash);
            var stale = 정적경관배치ReviewService.Evaluate(
                brief, briefHash, receipt, baseHash, new string('a', 64), mergedHash);
            var guideStale = 정적경관배치ReviewService.Evaluate(
                brief, new string('b', 64), receipt, baseHash, overrideHash, mergedHash);
            receipt.RenderingProfileHashSha256 = new string('c', 64);
            var profileStale = 정적경관배치ReviewService.Evaluate(
                brief, briefHash, receipt, baseHash, overrideHash, mergedHash);

            Assert.That(approved.IsApprovedForSceneApply, Is.True);
            Assert.That(stale.IsApprovedForSceneApply, Is.False);
            Assert.That(stale.EffectiveReviewStateCode,
                Is.EqualTo(정적경관배치ReviewStateCodes.Stale));
            Assert.That(stale.MismatchReason, Does.Contain("OverrideHash"));
            Assert.That(guideStale.IsApprovedForSceneApply, Is.False);
            Assert.That(guideStale.EffectiveReviewStateCode,
                Is.EqualTo(정적경관배치ReviewStateCodes.Stale));
            Assert.That(guideStale.MismatchReason, Does.Contain("BriefHash"));
            Assert.That(profileStale.IsApprovedForSceneApply, Is.False);
            Assert.That(profileStale.MismatchReason,
                Does.Contain("RenderingProfileHash"));
        }

        [Test]
        public void 검증기록은_Staging과Scene적용가능성을분리한다()
        {
            var report = new 정적경관배치ValidationReportData
            {
                ErrorCount = 0,
                ReviewMatchesInputs = false,
                EffectiveReviewStateCode = 정적경관배치ReviewStateCodes.Draft,
            };

            Assert.That(report.CanStage, Is.True);
            Assert.That(report.CanApply, Is.False);

            report.ReviewMatchesInputs = true;
            report.EffectiveReviewStateCode =
                정적경관배치ReviewStateCodes.ApprovedForSceneApply;
            Assert.That(report.CanApply, Is.True);
        }

        [Test]
        public void 배치검토창을열고닫아도_현재Scene은변경되지않는다()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var wasDirty = scene.isDirty;
            var objectCount = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            var window = EditorWindow.GetWindow<정적경관배치ReviewWindow>();
            window.Close();

            Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.EqualTo(wasDirty));
            Assert.That(UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(objectCount));
        }

        [Test]
        public void 통합Scene경관Scaffold는_기존타일을보존하고_여덟Anchor를멱등복구한다()
        {
            var completion = new GameObject(
                "CompletionArea_대관령면Farm_1km_L2_2x2").transform;
            var corridor = new GameObject(
                "L5_FarmHubTown이동회랑_SimulationRoute").transform;
            var scenic = new GameObject("L4_L7_Synty경관_PresentationOnly").transform;
            try
            {
                foreach (var name in new[]
                         {
                             "Tile_kr5186_l2_700_1144_농장마당_Reference",
                             "Tile_kr5186_l2_701_1144_감자경작지",
                             "Tile_kr5186_l2_700_1145_산림전이",
                             "Tile_kr5186_l2_701_1145_출발회랑",
                         })
                    new GameObject(name).transform.SetParent(completion, false);

                var first = 정적경관배치PlanPipeline.EnsureAnchorsForSceneLayout(
                    completion, corridor, scenic);
                var second = 정적경관배치PlanPipeline.EnsureAnchorsForSceneLayout(
                    completion, corridor, scenic);

                Assert.That(first.Count, Is.EqualTo(8));
                Assert.That(first, Has.All.Matches<정적경관배치AnchorView>(
                    value => value.ValidateWiring()));
                foreach (var value in first)
                    Assert.That(second.Single(item =>
                            item.ContainerStableId == value.ContainerStableId),
                        Is.SameAs(value));
                Assert.That(first.Select(value => value.ContainerStableId),
                    Is.EquivalentTo(정적경관배치PlanPipeline.GetContainerInfos()
                        .Select(value => value.StableId)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(completion.gameObject);
                UnityEngine.Object.DestroyImmediate(corridor.gameObject);
                UnityEngine.Object.DestroyImmediate(scenic.gameObject);
            }
        }

        [Test]
        public void Scene적용은_이전정적경관만정리하고_관측창고Fixture와새배치를보존한다()
        {
            var legacy = new GameObject("scenic_sim_pyeongchang_farm-mountain-a");
            var observed = new GameObject(
                "scenic_sim_pyeongchang_l2-700-1145-observed-fixture-barn");
            var generated = new GameObject("scenic_sim_pyeongchang_generated");
            generated.AddComponent<정적경관배치InstanceView>();
            try
            {
                Assert.That(
                    정적경관배치PlanPipeline.IsLegacyStaticSceneryRoot(legacy.transform),
                    Is.True);
                Assert.That(
                    정적경관배치PlanPipeline.IsLegacyStaticSceneryRoot(observed.transform),
                    Is.False);
                Assert.That(
                    정적경관배치PlanPipeline.IsLegacyStaticSceneryRoot(generated.transform),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacy);
                UnityEngine.Object.DestroyImmediate(observed);
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }
    }
}
