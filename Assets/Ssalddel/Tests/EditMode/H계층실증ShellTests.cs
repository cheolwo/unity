using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class H계층실증ShellTests
    {
        [Test]
        public void 공식Scene은_H5상대위치에_H3_H2_H1기준안을_배치한다()
        {
            var scene = EditorSceneManager.OpenScene(
                SimulationWorldShellBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var shell = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == SimulationWorldShellBuilder.RootName);
                Assert.That(shell, Is.Not.Null);
                var root = shell!.transform.Find(H계층실증ShellBuilder.RootName);
                Assert.That(root, Is.Not.Null);
                var evidence = root!.GetComponent<H계층배치증거Root>();
                Assert.That(evidence, Is.Not.Null);
                Assert.DoesNotThrow(evidence!.Validate);
                Assert.That(evidence.PresentationOnly, Is.True);
                Assert.That(evidence.AreaSetRoots.Select(value => value.AreaRoleCode),
                    Is.EquivalentTo(new[] { "NatureHome", "Farm", "Town", "CityHub" }));
                Assert.That(evidence.AreaSetRoots.All(value =>
                    value.PatternKindCode == "Baseline"), Is.True);
                Assert.That(evidence.H3PlacementCount, Is.GreaterThanOrEqualTo(12));
                Assert.That(evidence.H2PlacementCount, Is.GreaterThanOrEqualTo(24));
                Assert.That(evidence.H1ExpressionCount, Is.GreaterThanOrEqualTo(24));
                Assert.That(evidence.CorridorRoot.childCount, Is.EqualTo(3));
                Assert.That(evidence.ShowHierarchyOverlayOnPlay, Is.False);
                Assert.That(evidence.AreaSetRoots.All(value =>
                    !value.H3Boundaries.gameObject.activeSelf
                    && !value.H2Boundaries.gameObject.activeSelf
                    && value.SyntyPresentation.gameObject.activeSelf), Is.True);
                Assert.That(evidence.AreaSetRoots.All(value =>
                    value.transform.Find("H3TerrainUnderlay") != null), Is.True);

                var rules = evidence.PlacementRuleEvidence;
                Assert.That(rules, Is.Not.Null);
                Assert.DoesNotThrow(rules.Validate);
                Assert.That(rules.RuleRevision,
                    Is.EqualTo(H공간배치규칙증거Root.RequiredRevision));
                Assert.That(rules.SurfacePolicyCode,
                    Is.EqualTo("SyntheticFlatH3Underlay"));
                Assert.That(rules.AreaSetRecords, Has.Length.EqualTo(4));
                Assert.That(rules.AreaSetRecords.All(value =>
                    value.AppliedScale < 1f), Is.True);
                Assert.That(rules.H1Records, Has.Length.EqualTo(125));
                Assert.That(rules.FenceChainCount, Is.GreaterThanOrEqualTo(4));
                Assert.That(rules.FarmCoreAnchorPolicyCode,
                    Is.EqualTo("FivePointPlayerWorkRing"));
                Assert.That(rules.FarmCoreAnchorCount, Is.EqualTo(5));
                Assert.That(rules.NormalizedFootprintCount, Is.GreaterThan(0));
                Assert.That(rules.RelocatedObjectCount, Is.GreaterThan(0));
                Assert.That(rules.Violations, Is.Empty);
                var placementHierarchy = rules.PlacementControlHierarchy;
                Assert.That(placementHierarchy, Is.Not.Null);
                Assert.DoesNotThrow(placementHierarchy.Validate);
                Assert.That(placementHierarchy.StableId,
                    Is.EqualTo(배치통제계층Root.RequiredStableId));
                Assert.That(placementHierarchy.Levels, Has.Length.EqualTo(6));
                Assert.That(placementHierarchy.ScaleBands, Has.Length.EqualTo(5));
                Assert.That(placementHierarchy.ModuleOrchestrationPolicyCode,
                    Is.EqualTo("H3Axis-H2Frontage-H1ModuleAssembly"));
                Assert.That(placementHierarchy.PlayerReferenceHeight,
                    Is.InRange(.8f, 3f));
                Assert.That(rules.H1Records.All(value =>
                    !string.IsNullOrWhiteSpace(value.LandscapeNodeName)
                    && !string.IsNullOrWhiteSpace(value.BlockNodeName)
                    && placementHierarchy.FindBand(value.ScaleBandCode)
                        .Contains(value.HeightToPlayerRatio,
                            value.FootprintToPlayerRatio)), Is.True);
                Assert.That(rules.H1Records.Where(value =>
                        value.CategoryCode == "Ridge")
                    .All(value => Mathf.Max(value.FootprintAfter.x,
                        value.FootprintAfter.y)
                        <= rules.MaximumRidgeEnvelope + .01f), Is.True);
                Assert.That(rules.H1Records.Where(value =>
                        value.CategoryCode == "Building")
                    .All(value => value.HeightToPlayerRatio <= 3.51f
                                  && value.FootprintToPlayerRatio <= 4.21f),
                    Is.True);
                Assert.That(rules.RoadsideFrontagePatterns,
                    Has.Length.EqualTo(1));
                var frontage = rules.RoadsideFrontagePatterns[0];
                Assert.DoesNotThrow(frontage.Validate);
                Assert.That(frontage.AreaRoleCode, Is.EqualTo("Farm"));
                Assert.That(frontage.H3LandscapeNodeName,
                    Is.EqualTo("H3_HomeProcessing"));
                Assert.That(frontage.H2BlockNodeName,
                    Is.EqualTo("H2_h2-candidate:farm-worker-support"));
                Assert.That(frontage.EntranceGapWidth,
                    Is.GreaterThanOrEqualTo(2.55f));
                Assert.That(frontage.RoadFenceClearance,
                    Is.GreaterThanOrEqualTo(.35f));
                Assert.That(frontage.BuildingSetback,
                    Is.GreaterThanOrEqualTo(1.2f));
                Assert.That(frontage.EntrancePathClear, Is.True);
                AssertRequiredClearance(rules);

                var farm = evidence.AreaSetRoots.Single(value =>
                    value.AreaRoleCode == "Farm");
                Assert.That(farm.transform.localPosition.x, Is.EqualTo(0f).Within(.001f));
                Assert.That(farm.transform.localPosition.z, Is.EqualTo(0f).Within(.001f));
                Assert.That(evidence.AreaSetRoots.Where(value => value != farm)
                    .All(value => value.transform.localPosition.sqrMagnitude > 1f), Is.True);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 최소Player보기는_승인된H계층환경만_함께표시한다()
        {
            var scene = EditorSceneManager.OpenScene(
                SimulationWorldShellBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var shell = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == SimulationWorldShellBuilder.RootName)!;
                var evidenceRoot = shell.transform.Find(
                    H계층실증ShellBuilder.RootName)!;
                var policy = shell.GetComponent<단일PlayerViewController>();
                Assert.That(policy, Is.Not.Null);
                Assert.That(policy!.ApprovedEnvironmentRoots,
                    Does.Contain(evidenceRoot));

                policy.ApplyView();
                var evidenceRenderers = evidenceRoot
                    .GetComponentsInChildren<Renderer>(true);
                Assert.That(evidenceRenderers, Is.Not.Empty);
                Assert.That(evidenceRenderers.Any(value => value.enabled),
                    Is.True);
                Assert.That(evidenceRenderers.Where(IsReviewHelper)
                    .All(value => !value.enabled
                                  || !value.gameObject.activeInHierarchy), Is.True,
                    "승인된 환경 안에서도 검토용 경계·연결선은 다시 켜지면 안 된다.");
                var unrelated = shell.GetComponentsInChildren<Renderer>(true)
                    .First(value => !value.transform.IsChildOf(evidenceRoot)
                                    && value.transform != policy.Player.VisualRoot
                                    && !value.transform.IsChildOf(
                                        policy.Player.VisualRoot));
                Assert.That(unrelated.enabled, Is.False);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool IsReviewHelper(Renderer renderer) =>
            renderer.sharedMaterials.Any(material => material != null &&
                new[]
                {
                    "H3경계", "H2경계", "연결지점", "WI동선",
                    "H1공간경계", "H2기준바닥", "H2내부관계", "H2외부연결구",
                }.Any(prefix => material.name.StartsWith(
                    prefix, StringComparison.Ordinal)));

        private static void AssertRequiredClearance(
            H공간배치규칙증거Root rules)
        {
            for (var leftIndex = 0; leftIndex < rules.H1Records.Length;
                 leftIndex++)
            for (var rightIndex = leftIndex + 1;
                 rightIndex < rules.H1Records.Length; rightIndex++)
            {
                var left = rules.H1Records[leftIndex];
                var right = rules.H1Records[rightIndex];
                var required = left.CategoryCode == "Ridge"
                               && right.CategoryCode == "Building"
                               || left.CategoryCode == "Building"
                               && right.CategoryCode == "Ridge"
                    ? rules.BuildingRidgeClearance
                    : rules.MinimumObjectClearance;
                var dx = Mathf.Max(0f, Mathf.Abs(
                        left.FootprintCenterAfter.x
                        - right.FootprintCenterAfter.x)
                    - (left.FootprintAfter.x + right.FootprintAfter.x) * .5f);
                var dz = Mathf.Max(0f, Mathf.Abs(
                        left.FootprintCenterAfter.z
                        - right.FootprintCenterAfter.z)
                    - (left.FootprintAfter.y + right.FootprintAfter.y) * .5f);
                Assert.That(Mathf.Sqrt(dx * dx + dz * dz),
                    Is.GreaterThanOrEqualTo(required - .01f),
                    $"배치 이격 위반: {left.H1Name} / {right.H1Name}");
            }
        }
    }
}
