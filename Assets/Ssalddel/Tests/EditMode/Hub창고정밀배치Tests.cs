using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Interior.Contracts;
using Ssalddel.Interior.Domain;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Hub창고정밀배치Tests
    {
        [Test]
        public void HubGrammar_UsesFiveWorkZonesTwoAislesAndSyntyMappings()
        {
            var request = HubWarehouseInteriorGrammar.CreateRequest();
            var plan = new DeterministicInteriorLayoutEngine()
                .Generate(request);

            Assert.That(plan.SchemaVersion,
                Is.EqualTo(InteriorLayoutCodes.SchemaVersionV2));
            Assert.That(plan.PlacementControlRuleRevision,
                Is.EqualTo(배치통제계층Root.InteriorRevision));
            Assert.That(plan.Zones, Has.Length.EqualTo(7));
            Assert.That(plan.Zones.Count(value => !value.RoleCode.Contains(
                "Aisle", StringComparison.Ordinal)), Is.EqualTo(5));
            Assert.That(plan.Placements, Has.Length.EqualTo(7));
            Assert.That(plan.TraversalValidated, Is.True);
            Assert.That(plan.UnresolvedRequiredFixtureCodes, Is.Empty);

            foreach (var placement in plan.Placements)
            {
                var path = Hub창고정밀배치Builder.ResolveVisualPrefabPath(
                    placement.VisualKey);
                Assert.That(path, Does.StartWith("Assets/Synty/"));
                Assert.That(path, Does.Not.Contain("Truck"));
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    Is.Not.Null, path);
            }
        }

        [Test]
        public void CompositionV3_UsesBottomUpH1ToH4ReadinessAsPrimaryAxis()
        {
            EditorSceneManager.OpenScene(SimulationWorldShellBuilder.ScenePath);
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            var root = new GameObject("PlacementCompositionV3Test");
            try
            {
                var hierarchy = root.AddComponent<배치통제계층Root>();
                hierarchy.ConfigureCompositionV3(root.transform, player!, 1.939f);

                Assert.That(hierarchy.Revision,
                    Is.EqualTo(배치통제계층Root.CompositionRevision));
                Assert.That(hierarchy.PrimaryCompositionDirectionCode,
                    Is.EqualTo(배치통제계층Root.BottomUpCompositionDirection));
                Assert.That(hierarchy.ConstraintEvaluationDirectionCode,
                    Is.EqualTo(배치통제계층Root.TopDownConstraintDirection));
                Assert.That(hierarchy.CompositionSteps.Select(value =>
                        value.SourceLevelCode + ">" + value.TargetLevelCode),
                    Is.EqualTo(new[] { "H1>H2", "H2>H3", "H3>H4" }));
                Assert.That(hierarchy.CompositionSteps[2]
                    .CreatesSpatialInstance, Is.False);
                Assert.DoesNotThrow(hierarchy.Validate);

                hierarchy.ConfigureInterior(root.transform, player!, 1.939f);
                Assert.That(hierarchy.Revision,
                    Is.EqualTo(배치통제계층Root.InteriorRevision));
                Assert.That(hierarchy.PrimaryCompositionDirectionCode,
                    Is.Empty);
                Assert.That(hierarchy.ConstraintEvaluationDirectionCode,
                    Is.Empty);
                Assert.That(hierarchy.CompositionSteps, Is.Empty);
                Assert.DoesNotThrow(hierarchy.Validate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HubBuilder_EmitsCompositionV3WithoutChangingSimulationState()
        {
            EditorSceneManager.OpenScene(SimulationWorldShellBuilder.ScenePath);
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            var parent = new GameObject("HubCompositionV3BuilderTest");
            try
            {
                var warehouse = Hub창고정밀배치Builder.BuildUnder(
                    parent.transform, player!);
                var hierarchy = warehouse.GetComponentInChildren<
                    배치통제계층Root>(true);

                Assert.That(hierarchy, Is.Not.Null);
                Assert.That(hierarchy!.Revision,
                    Is.EqualTo(배치통제계층Root.CompositionRevision));
                Assert.That(hierarchy.PrimaryCompositionDirectionCode,
                    Is.EqualTo(배치통제계층Root.BottomUpCompositionDirection));
                Assert.That(warehouse.PresentationOnly, Is.True);
                Assert.DoesNotThrow(warehouse.Validate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
