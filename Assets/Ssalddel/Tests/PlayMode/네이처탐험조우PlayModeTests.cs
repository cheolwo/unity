using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 네이처탐험조우PlayModeTests
    {
        [UnityTest]
        public IEnumerator 네이처경관에서_위협이접근하고_기존현장전투요청으로_인계된다()
        {
            yield return SceneManager.LoadSceneAsync("SimulationWorldShell");
            var shell = Object.FindFirstObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var presenter = Object.FindFirstObjectByType<네이처조우Presenter>(
                FindObjectsInactive.Include);
            var composition = Object.FindFirstObjectByType<
                네이처탐험조우CompositionRoot>(FindObjectsInactive.Include);
            Assert.That(shell, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(composition, Is.Not.Null);
            composition!.enabled = false;
            var revision = shell!.WorldRevision;
            player!.EnterExplorationMode();
            var requested = string.Empty;
            presenter!.EncounterResponseRequested += value => requested = value;
            presenter.Apply(new 네이처탐험조우StateApiModel
            {
                SessionStableId = "session:nature:playmode",
                WorldRevision = revision,
                SimulationOnly = true,
                Encounters = new[]
                {
                    new 네이처탐험조우ApiModel
                    {
                        EncounterStableId = "encounter:nature:playmode",
                        EncounterRevision = 1,
                        NatureRouteCode = 네이처탐험조우Codes.NatureToFarm,
                        StateCode = 네이처탐험조우Codes.Active,
                        ThreatUnitCount = 2,
                    },
                },
            });
            yield return null;
            presenter.EvaluateApproach(10f);

            Assert.That(requested, Is.EqualTo("encounter:nature:playmode"));
            Assert.That(presenter.ActiveEncounterCount, Is.EqualTo(1));
            Assert.That(shell.WorldRevision, Is.EqualTo(revision));
            presenter.MarkResolved("encounter:nature:playmode");
            Assert.That(presenter.ActiveEncounterCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator 현장전투종료는_고정공간을풀고_네이처탐험으로_복귀한다()
        {
            yield return SceneManager.LoadSceneAsync("SimulationWorldShell");
            var player = Object.FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var battle = Object.FindFirstObjectByType<현장전투CompositionRoot>(
                FindObjectsInactive.Include);
            var lh = Object.FindFirstObjectByType<공간LHStreamingEngine>(
                FindObjectsInactive.Include);
            Assert.That(player, Is.Not.Null);
            Assert.That(battle, Is.Not.Null);
            Assert.That(lh, Is.Not.Null);
            battle!.enabled = false;
            lh!.PinFocusCell("kr5186:l3:2801:4581");
            player!.EnterCombatMode(
                Ssalddel.Unity.Survival.FarmCombatPresentationCodes
                    .FirstPersonPrecision);
            var resolvedEncounter = string.Empty;
            battle.WorldLocalBattleResolved += value => resolvedEncounter = value;
            var apply = typeof(현장전투CompositionRoot).GetMethod("Apply",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            apply!.Invoke(battle, new object[]
            {
                new BattleInstanceApiModel
                {
                    BattleStableId = "battle:nature:resolved",
                    EncounterStableId = "encounter:nature:resolved",
                    AreaStableId = "area:nature",
                    CombatSpaceCode = BattlePresentationCodes.WorldLocal,
                    PhaseCode = BattlePresentationCodes.Completed,
                    BattleRevision = 9,
                    SimulationOnly = true,
                    LocalCombat = new LocalCombatStateApiModel
                    {
                        StateCode = BattlePresentationCodes.Completed,
                        WorldContext = new LocalCombatWorldContextApiModel(),
                    },
                },
            });
            yield return null;

            Assert.That(lh.IsFocusPinned, Is.False);
            Assert.That(player.CurrentActivityCode,
                Is.EqualTo(Ssalddel.Unity.PlayerActivities.PlayerActivityCodes
                    .Exploration));
            Assert.That(resolvedEncounter,
                Is.EqualTo("encounter:nature:resolved"));
        }
    }
}
