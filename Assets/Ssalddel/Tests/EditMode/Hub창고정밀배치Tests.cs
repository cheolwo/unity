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
        // D423: 기존 Scene/Player 조회 없이 선언 검사만 수행하는 비활성 소유 사본.
        [TestCase(1, "None")]
        [TestCase(2, "None")]
        [TestCase(3, "None")]
        [TestCase(4, "None")]
        [TestCase(3, "Composition")]
        [TestCase(4, "Composition")]
        [TestCase(4, "ChannelAuthority")]
        [TestCase(4, "ChannelDuplicate")]
        [TestCase(4, "ChannelMissing")]
        [TestCase(4, "ChannelNull")]
        [TestCase(4, "EarlyFailureBeforeNull")]
        [TestCase(3, "CompositionNull")]
        [TestCase(2, "UnexpectedChannel")]
        public void D423_DeclarationValidationPreservesProfilesFailuresAndInputs(
            int version, string defect)
        {
            var ownedScene = EditorSceneManager.NewPreviewScene();
            GameObject ownedRoot = null;
            try
            {
                ownedRoot = new GameObject("D423_OwnedDeclarationFixture");
                ownedRoot.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownedRoot, ownedScene);
                var playerObject = new GameObject("OwnedPlayerReference");
                playerObject.transform.SetParent(ownedRoot.transform, false);
                var player = playerObject.AddComponent<플레이어경관Controller>();
                SetOwnedField(player, "visualRoot", playerObject.transform);
                var hierarchy = ownedRoot.AddComponent<배치통제계층Root>();
                switch (version)
                {
                    case 1: hierarchy.Configure(ownedRoot.transform, player, 1.939f); break;
                    case 2: hierarchy.ConfigureInterior(ownedRoot.transform, player, 1.939f); break;
                    case 3: hierarchy.ConfigureCompositionV3(ownedRoot.transform, player, 1.939f); break;
                    case 4: hierarchy.ConfigureSensoryPresentationV4(ownedRoot.transform, player, 1.939f); break;
                    default: throw new ArgumentOutOfRangeException(nameof(version));
                }

                switch (defect)
                {
                    case "Composition": hierarchy.CompositionSteps[2].CreatesSpatialInstance = true; break;
                    case "ChannelAuthority": hierarchy.PresentationChannels[0].MutatesCanonicalState = true; break;
                    case "ChannelDuplicate": hierarchy.PresentationChannels[1].DomainCode = hierarchy.PresentationChannels[0].DomainCode; break;
                    case "ChannelMissing": SetOwnedField(hierarchy, "presentationChannels", Array.Empty<감각표현ChannelRecord>()); break;
                    case "ChannelNull": SetOwnedField(hierarchy, "presentationChannels", null); break;
                    case "CompositionNull": SetOwnedField(hierarchy, "compositionSteps", null); break;
                    case "EarlyFailureBeforeNull":
                        SetOwnedField(hierarchy, "presentationChannels", null);
                        SetOwnedField(hierarchy, "moduleOrchestrationPolicyCode", "Invalid");
                        break;
                    case "UnexpectedChannel":
                        SetOwnedField(hierarchy, "presentationChannels", new[] { new 감각표현ChannelRecord() });
                        break;
                }

                var before = EditorJsonUtility.ToJson(hierarchy);
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    if (defect == "None")
                        Assert.DoesNotThrow(hierarchy.Validate);
                    else if (defect is "ChannelNull" or "CompositionNull")
                        Assert.Throws<NullReferenceException>(hierarchy.Validate);
                    else
                    {
                        var failure = Assert.Throws<InvalidOperationException>(hierarchy.Validate);
                        Assert.That(failure!.Message, Is.EqualTo(
                            "PlacementControlHierarchyInvalid:"
                            + $"stable={hierarchy.StableId};revision={hierarchy.Revision};"
                            + $"playerHeight={hierarchy.PlayerReferenceHeight:F3};"
                            + $"policy={hierarchy.ModuleOrchestrationPolicyCode};"
                            + $"levels={hierarchy.Levels.Length};bands={hierarchy.ScaleBands.Length};"
                            + $"presentationOnly={hierarchy.PresentationOnly}"));
                    }
                    Assert.That(EditorJsonUtility.ToJson(hierarchy), Is.EqualTo(before));
                }
                Assert.That(ownedRoot.activeInHierarchy, Is.False);
                Assert.That(ownedRoot.GetComponentsInChildren<Renderer>(true), Is.Empty,
                    "선언 검사는 실제 Renderer·지면·통행 성공을 요구하거나 입증하지 않는다.");
            }
            finally
            {
                try
                {
                    if (ownedRoot != null) UnityEngine.Object.DestroyImmediate(ownedRoot);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(ownedScene);
                }
            }
        }

        private static void SetOwnedField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field!.SetValue(target, value);
        }

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
        public void SensoryPresentationV4_KeepsBothPlacementAxesAndAddsCrossCuttingChannels()
        {
            EditorSceneManager.OpenScene(SimulationWorldShellBuilder.ScenePath);
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            var root = new GameObject("PlacementSensoryPresentationV4Test");
            try
            {
                var hierarchy = root.AddComponent<배치통제계층Root>();
                hierarchy.ConfigureSensoryPresentationV4(root.transform,
                    player!, 1.939f);

                Assert.That(hierarchy.Revision,
                    Is.EqualTo(배치통제계층Root.SensoryPresentationRevision));
                Assert.That(hierarchy.PrimaryCompositionDirectionCode,
                    Is.EqualTo(배치통제계층Root.BottomUpCompositionDirection));
                Assert.That(hierarchy.ConstraintEvaluationDirectionCode,
                    Is.EqualTo(배치통제계층Root.TopDownConstraintDirection));
                Assert.That(hierarchy.PresentationEvaluationDirectionCode,
                    Is.EqualTo(배치통제계층Root.SensoryPresentationDirection));
                Assert.That(hierarchy.PresentationBindingSchemaCode,
                    Is.EqualTo(배치통제계층Root.SensoryPresentationBindingSchema));
                Assert.That(hierarchy.PresentationChannels.Select(value =>
                        value.DomainCode),
                    Is.EquivalentTo(Ssalddel.Unity.PresentationContracts
                        .표현규칙영역Codes.All));
                Assert.That(hierarchy.PresentationChannels,
                    Has.All.Matches<감각표현ChannelRecord>(value =>
                        !value.MutatesCanonicalState
                        && !value.ConfirmsBusinessCompletion));
                Assert.DoesNotThrow(hierarchy.Validate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
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
        public void HubBuilder_EmitsSensoryPresentationV4WithoutChangingSimulationState()
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
                    Is.EqualTo(배치통제계층Root.SensoryPresentationRevision));
                Assert.That(hierarchy.PrimaryCompositionDirectionCode,
                    Is.EqualTo(배치통제계층Root.BottomUpCompositionDirection));
                Assert.That(hierarchy.PresentationEvaluationDirectionCode,
                    Is.EqualTo(배치통제계층Root.SensoryPresentationDirection));
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
