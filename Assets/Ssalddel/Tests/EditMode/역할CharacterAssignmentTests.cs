using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 역할CharacterAssignmentTests
    {
        [TestCase("농부", WorldActorWorkflowContextCodes.Farm,
            WorldActorRoleCodes.FarmerProducer)]
        [TestCase("화주", WorldActorWorkflowContextCodes.FreightDelivery,
            WorldActorRoleCodes.Shipper)]
        [TestCase("창고관리자", WorldActorWorkflowContextCodes.Warehouse,
            WorldActorRoleCodes.WarehouseOperator)]
        [TestCase("보세운송사", WorldActorWorkflowContextCodes.FreightDelivery,
            WorldActorRoleCodes.TransportOperator)]
        [TestCase("용달기사", WorldActorWorkflowContextCodes.FreightDelivery,
            WorldActorRoleCodes.FreightDeliveryDriver)]
        [TestCase("배달기사", WorldActorWorkflowContextCodes.FoodDelivery,
            WorldActorRoleCodes.FoodDeliveryDriver)]
        [TestCase("판매자", WorldActorWorkflowContextCodes.MarketOrder,
            WorldActorRoleCodes.Seller)]
        [TestCase("orderer", WorldActorWorkflowContextCodes.MarketOrder,
            WorldActorRoleCodes.Orderer)]
        public void 서버역할은_기존값을바꾸지않고_Unity표현역할로정규화한다(
            string sourceRole,
            string context,
            string expected)
        {
            var result = WorldActorRoleNormalizer.Normalize(sourceRole, context);

            Assert.That(result.SourceRoleCode, Is.EqualTo(sourceRole));
            Assert.That(result.ActorRoleCode, Is.EqualTo(expected));
            Assert.That(result.IsResolved, Is.True);
        }

        [Test]
        public void 기사역할은_업무문맥없이는_임의배달유형으로정하지않는다()
        {
            var result = WorldActorRoleNormalizer.Normalize(
                "기사", WorldActorWorkflowContextCodes.General);

            Assert.That(result.ActorRoleCode, Is.EqualTo(WorldActorRoleCodes.Unresolved));
            Assert.That(result.DiagnosticCode, Is.EqualTo("actor.role-unresolved"));
        }

        [Test]
        public void 같은인물_역할_대장버전_외형계열은_같은VisualKey를선택한다()
        {
            var profile = Profile("actor:test:freight-01", WorldActorAppearanceFamilyCodes.AdultA);
            var candidates = Candidates();

            var first = WorldCharacterAssignmentPolicy.Assign(
                profile, WorldActorRoleCodes.FreightDeliveryDriver,
                "role-character-catalog.v1", candidates);
            var second = WorldCharacterAssignmentPolicy.Assign(
                profile, WorldActorRoleCodes.FreightDeliveryDriver,
                "role-character-catalog.v1", candidates);

            Assert.That(first.VisualKey, Is.EqualTo(second.VisualKey));
            Assert.That(first.AppearanceFamilyCode,
                Is.EqualTo(WorldActorAppearanceFamilyCodes.AdultA));
            Assert.That(first.PresentationOnly, Is.True);
        }

        [Test]
        public void 역할이바뀌어도_사용자가선택한외형계열은유지한다()
        {
            var profile = Profile("actor:test:multi-role-01", WorldActorAppearanceFamilyCodes.AdultB);
            var candidates = Candidates();

            var shipper = WorldCharacterAssignmentPolicy.Assign(
                profile, WorldActorRoleCodes.Shipper, "role-character-catalog.v1", candidates);
            var seller = WorldCharacterAssignmentPolicy.Assign(
                profile, WorldActorRoleCodes.Seller, "role-character-catalog.v1", candidates);

            Assert.That(shipper.AppearanceFamilyCode,
                Is.EqualTo(WorldActorAppearanceFamilyCodes.AdultB));
            Assert.That(seller.AppearanceFamilyCode,
                Is.EqualTo(WorldActorAppearanceFamilyCodes.AdultB));
            Assert.That(shipper.VisualKey, Is.Not.EqualTo(seller.VisualKey));
        }

        [Test]
        public void 후보가없는역할은_중립Visual과진단을사용한다()
        {
            var profile = Profile("actor:test:unknown-01", WorldActorAppearanceFamilyCodes.Neutral);
            var candidates = Candidates();

            var result = WorldCharacterAssignmentPolicy.Assign(
                profile, WorldActorRoleCodes.Unresolved,
                "role-character-catalog.v1", candidates);

            Assert.That(result.VisualKey, Is.EqualTo(WorldCharacterVisualKeys.NeutralAdultA));
            Assert.That(result.ActorRoleCode, Is.EqualTo(WorldActorRoleCodes.Unresolved));
        }

        [Test]
        public void 캐릭터VisualKey는_Synty파일명이나경로를업무계약으로노출하지않는다()
        {
            Assert.That(WorldCharacterVisualKeys.All, Is.Unique);
            Assert.That(WorldCharacterVisualKeys.All, Has.None.Contains("SM_"));
            Assert.That(WorldCharacterVisualKeys.All, Has.None.Contains("Synty"));
            Assert.That(WorldActorRoleCodes.Playable.Count, Is.EqualTo(8));
        }

        [Test]
        public void 평창군역할Character대장은_8개역할마다_성인플레이어후보를둘이상제공한다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<역할CharacterVisualCatalog>(
                "Assets/Ssalddel/Presentation/World/Catalogs/평창군역할CharacterVisualCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.Validate);
            Assert.That(catalog.Entries.Count,
                Is.EqualTo(WorldCharacterVisualKeys.All.Count));
            foreach (var role in WorldActorRoleCodes.Playable)
                Assert.That(catalog.Entries.Count(value => value.PlayerEligible
                        && value.AllowedActorRoleCodes.Contains(role)),
                    Is.GreaterThanOrEqualTo(2), role);
            Assert.That(catalog.Entries, Has.All.Matches<역할CharacterVisualCatalogEntry>(
                value => value.PresentationOnly
                    && value.AnimatorCount > 0
                    && value.Prefab.GetComponentInChildren<Animator>(true).avatar.isHuman));
        }

        [Test]
        public void SimulationWorldShell은_FarmHubTown의_10개역할Character를_표현전용으로배치한다()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/SimulationWorldShell.unity",
                OpenSceneMode.Single);

            var views = UnityEngine.Object.FindObjectsByType<역할CharacterVisualInstanceView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var switchers = UnityEngine.Object.FindObjectsByType<역할CharacterVisualSwitcher>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.That(views, Has.Length.EqualTo(10));
            Assert.That(switchers, Has.Length.EqualTo(10));
            Assert.That(views.Select(value => value.AppearanceProfile.ActorStableId), Is.Unique);
            foreach (var role in WorldActorRoleCodes.Playable)
                Assert.That(views.Select(value => value.Assignment.ActorRoleCode),
                    Does.Contain(role));
            Assert.That(views, Has.All.Matches<역할CharacterVisualInstanceView>(
                value => value.PresentationOnly && value.ValidateWiring()));
            Assert.That(switchers, Has.All.Matches<역할CharacterVisualSwitcher>(
                value => value.PresentationOnly && value.ValidateWiring()));
            Assert.That(views.SelectMany(value =>
                    value.PrefabInstanceRoot.GetComponentsInChildren<Animator>(true)),
                Has.All.Matches<Animator>(value => value.runtimeAnimatorController == null));
            Assert.That(UnityEngine.Object.FindObjectsByType<플레이어경관Controller>(
                FindObjectsInactive.Include, FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
        }

        private static WorldActorAppearanceProfile Profile(string id, string family)
            => new()
            {
                ActorStableId = id,
                SelectedAppearanceFamilyCode = family,
                ExplicitlySelected = true,
                PresentationOnly = true,
            };

        private static WorldCharacterAssignmentCandidate[] Candidates()
        {
            var allFamilies = WorldActorAppearanceFamilyCodes.All.ToArray();
            return new[]
            {
                Candidate(WorldCharacterVisualKeys.LogisticsWorkerA,
                    new[] { WorldActorRoleCodes.FreightDeliveryDriver },
                    new[] { WorldActorAppearanceFamilyCodes.AdultA }),
                Candidate(WorldCharacterVisualKeys.LogisticsWorkerB,
                    new[] { WorldActorRoleCodes.FreightDeliveryDriver },
                    new[] { WorldActorAppearanceFamilyCodes.AdultA }),
                Candidate(WorldCharacterVisualKeys.BusinessOperatorA,
                    new[] { WorldActorRoleCodes.Shipper },
                    new[] { WorldActorAppearanceFamilyCodes.AdultB }),
                Candidate(WorldCharacterVisualKeys.TownSellerA,
                    new[] { WorldActorRoleCodes.Seller },
                    new[] { WorldActorAppearanceFamilyCodes.AdultB }),
                Candidate(WorldCharacterVisualKeys.NeutralAdultA,
                    new[] { WorldActorRoleCodes.Unresolved }, allFamilies),
            };
        }

        private static WorldCharacterAssignmentCandidate Candidate(
            string visualKey,
            string[] roles,
            string[] families)
            => new()
            {
                VisualKey = visualKey,
                AllowedActorRoleCodes = roles,
                AppearanceFamilyCodes = families,
                Weight = 1,
                PlayerEligible = true,
                PresentationOnly = true,
            };
    }
}
