using NUnit.Framework;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 현장전투입력번역Tests
    {
        [Test]
        public void 우클릭은_1인칭평시에는_시점을보존하고_3인칭에서는_접근한다()
        {
            var firstBattle = Battle(LocalCombatPresentationCodes.DirectAction);
            var tacticalBattle = Battle(LocalCombatPresentationCodes.TacticalCommand);
            var firstPerson = LocalCombatInputCommandFactory.CreatePointerAction(
                firstBattle, LocalCombatPresentationCodes.FirstPerson,
                LocalCombatPresentationCodes.RightPointer, false,
                "actor:player", "threat:1", "command:first", 0);
            var thirdPerson = LocalCombatInputCommandFactory.CreatePointerAction(
                tacticalBattle, LocalCombatPresentationCodes.TacticalThirdPerson,
                LocalCombatPresentationCodes.RightPointer, false,
                "actor:player", "threat:1", "command:third", 0);

            Assert.That(firstPerson, Is.Null);
            Assert.That(thirdPerson!.ActionCode,
                Is.EqualTo(LocalCombatPresentationCodes.Approach));
        }

        [Test]
        public void 피격예고중_우클릭은_일인칭회피_삼인칭대형유지다()
        {
            var direct = LocalCombatInputCommandFactory.CreatePointerAction(
                Battle(LocalCombatPresentationCodes.DirectAction),
                LocalCombatPresentationCodes.FirstPerson,
                LocalCombatPresentationCodes.RightPointer, true,
                "actor:player", "threat:1", "command:direct", 140);
            var tactical = LocalCombatInputCommandFactory.CreatePointerAction(
                Battle(LocalCombatPresentationCodes.TacticalCommand),
                LocalCombatPresentationCodes.TacticalThirdPerson,
                LocalCombatPresentationCodes.RightPointer, true,
                "actor:player", "threat:1", "command:tactical", 140);
            Assert.That(direct!.ActionCode,
                Is.EqualTo(LocalCombatPresentationCodes.Dodge));
            Assert.That(tactical!.ActionCode,
                Is.EqualTo(LocalCombatPresentationCodes.HoldPosition));
        }

        [Test]
        public void 행동슬롯은_일인칭기술과_삼인칭지휘를_분리한다()
        {
            var guard = LocalCombatInputCommandFactory.CreateActionSlot(
                Battle(LocalCombatPresentationCodes.DirectAction),
                LocalCombatPresentationCodes.FirstPerson, 2,
                "actor:player", "threat:1", "command:guard", 0);
            Assert.That(guard!.ActionCode,
                Is.EqualTo(LocalCombatPresentationCodes.Guard));

            var tacticalBattle = Battle(LocalCombatPresentationCodes.TacticalCommand);
            var hold = LocalCombatInputCommandFactory.CreateActionSlot(tacticalBattle,
                LocalCombatPresentationCodes.TacticalThirdPerson, 2,
                "actor:player", "threat:1", "command:hold", 0);
            var skill = LocalCombatInputCommandFactory.CreateActionSlot(tacticalBattle,
                LocalCombatPresentationCodes.TacticalThirdPerson, 4,
                "actor:player", "threat:1", "command:no-skill", 0);
            Assert.That(hold!.ActionCode,
                Is.EqualTo(LocalCombatPresentationCodes.HoldPosition));
            Assert.That(skill, Is.Null);
        }

        [Test]
        public void 공식SimulationWorldShell은_새현장전투권위를_사용한다()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/SimulationWorldShell.unity");
            var unified = Object.FindFirstObjectByType<현장전투CompositionRoot>(
                FindObjectsInactive.Include);
            var legacy = Object.FindFirstObjectByType<농장전투CompositionRoot>(
                FindObjectsInactive.Include);
            var lh = Object.FindFirstObjectByType<공간LHStreamingEngine>(
                FindObjectsInactive.Include);

            Assert.That(unified, Is.Not.Null);
            Assert.That(unified!.ValidateWiring(), Is.True);
            Assert.That(unified.ServerAuthorityEnabled, Is.True);
            Assert.That(legacy, Is.Not.Null);
            Assert.That(legacy!.ServerAuthorityEnabled, Is.False);
            Assert.That(lh, Is.Not.Null);
            lh!.PinFocusCell("kr5186:l3:2801:4581");
            Assert.That(lh.IsFocusPinned, Is.True);
            Assert.That(lh.PinnedFocusCellKey,
                Is.EqualTo("kr5186:l3:2801:4581"));
            lh.ReleaseFocusPin();
        }

        private static BattleInstanceApiModel Battle(string controlModeCode) => new()
        {
            BattleStableId = "battle:local",
            AreaStableId = "area:farm",
            CombatSpaceCode = BattlePresentationCodes.WorldLocal,
            PhaseCode = BattlePresentationCodes.Active,
            BattleRevision = 3,
            ReplayHashSha256 = new string('a', 64),
            SimulationOnly = true,
            LocalCombat = new LocalCombatStateApiModel
            {
                StateCode = LocalCombatPresentationCodes.Active,
                ControlModeCode = controlModeCode,
                FocusedTargetStableId = "threat:1",
                Actors = new[]
                {
                    new LocalCombatActorApiModel
                    {
                        ActorStableId = "actor:player",
                        StateCode = LocalCombatPresentationCodes.Active,
                    },
                },
            },
        };
    }
}
