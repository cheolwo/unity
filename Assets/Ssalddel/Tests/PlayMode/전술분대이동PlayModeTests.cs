using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Survival;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 전술분대이동PlayModeTests
    {
        [UnityTest]
        public IEnumerator 서버결과에따라_대형사수_전진공격_후퇴를연속표현한다()
        {
            void RemoveLiveServerCompositions(Scene loadedScene,
                LoadSceneMode mode)
            {
                foreach (var value in Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
                foreach (var value in Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                             FindObjectsInactive.Include))
                    Object.DestroyImmediate(value);
            }
            SceneManager.sceneLoaded += RemoveLiveServerCompositions;
            yield return SceneManager.LoadSceneAsync("SimulationWorldShell",
                LoadSceneMode.Single);
            SceneManager.sceneLoaded -= RemoveLiveServerCompositions;
            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            var combat = Object.FindAnyObjectByType<전투시점Controller>(
                FindObjectsInactive.Include);
            var presenter = Object.FindAnyObjectByType<전술분대Presenter>(
                FindObjectsInactive.Include);
            var root = GameObject.Find("SimulationWorldShell");
            var settlement = root.transform.Find("SettlementInteriorRoot")
                .gameObject;
            Assert.That(shell, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            settlement.SetActive(true);
            yield return null;

            var worldTick = shell.WorldTick;
            var worldRevision = shell.WorldRevision;
            combat.ApplyServerState(ResolvedState(10, "resolution:hold",
                FarmCombatPresentationCodes.HoldFormation,
                FarmCombatPresentationCodes.Perimeter), "actor:player");
            yield return null;
            var allied = presenter.Squads.Single(value =>
                value.SideCode == FarmCombatPresentationCodes.Allied);
            var hostile = presenter.Squads.Single(value =>
                value.SideCode == FarmCombatPresentationCodes.Hostile);
            Assert.That(allied.FormationCode,
                Is.EqualTo(FarmCombatPresentationCodes.LineFormation));
            Assert.That(allied.Members.Count(value => value.gameObject.activeSelf),
                Is.EqualTo(6));
            Assert.That(allied.Members.All(value =>
                value.AnimationAdapter.RootMotionDisabled), Is.True);
            Assert.That(Vector3.Distance(allied.transform.position,
                hostile.transform.position), Is.GreaterThan(6f));
            Assert.That(Quaternion.Angle(allied.transform.rotation,
                hostile.transform.rotation), Is.GreaterThan(150f));
            var farm = root.transform.Find(
                "SettlementInteriorRoot/Districts/FarmDistrict");
            var districtSurface = farm.Find("DistrictSurface")
                .GetComponent<Renderer>();
            var navigationFloor = farm.Find(
                "TacticalBattleRoot/NavigationRoot/TacticalWalkableFloor")
                .GetComponent<Renderer>();
            Assert.That(navigationFloor.enabled, Is.False);
            foreach (var squadMember in presenter.Squads
                         .SelectMany(value => value.Members))
            {
                var visualRoot = squadMember.transform.GetChild(0);
                Assert.That(캐릭터지면정렬Utility.TryGetVisibleBounds(
                    visualRoot, out var visibleBounds), Is.True);
                Assert.That(visibleBounds.min.y,
                    Is.GreaterThanOrEqualTo(
                        districtSurface.bounds.max.y + .01f));
            }

            var beforeAdvance = allied.transform.position;
            combat.ApplyServerState(ResolvedState(11, "resolution:advance",
                FarmCombatPresentationCodes.AdvanceAndAttack,
                FarmCombatPresentationCodes.Forward), "actor:player");
            Assert.That(allied.FormationCode,
                Is.EqualTo(FarmCombatPresentationCodes.WedgeFormation));
            yield return WaitForMovement(allied, beforeAdvance, 2.5f);
            var afterAdvance = allied.transform.position;
            Assert.That(allied.DiagnosticCode, Is.Empty);

            combat.ApplyServerState(ResolvedState(12, "resolution:retreat",
                FarmCombatPresentationCodes.TacticalRetreat,
                FarmCombatPresentationCodes.InnerFarm), "actor:player");
            Assert.That(allied.FormationCode,
                Is.EqualTo(FarmCombatPresentationCodes.ColumnFormation));
            yield return WaitForMovement(allied, afterAdvance, 2.5f);
            Assert.That(shell.WorldTick, Is.EqualTo(worldTick));
            Assert.That(shell.WorldRevision, Is.EqualTo(worldRevision));
        }

        private static IEnumerator WaitForMovement(
            전술분대대형Controller squad, Vector3 before, float timeout)
        {
            while (Vector3.Distance(squad.transform.position, before) < .2f
                   && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.That(Vector3.Distance(squad.transform.position, before),
                Is.GreaterThan(.2f), squad.DiagnosticCode);
        }

        private static FarmCombatStateApiModel ResolvedState(long revision,
            string resolutionId, string orderCode, string alliedPosition)
            => new()
            {
                WorldRevision = revision,
                Tactical = new FarmTacticalCombatStateApiModel
                {
                    Fronts = new[]
                    {
                        new FarmTacticalFrontApiModel
                        {
                            FrontStableId = "front:farm",
                            EncounterStableId = "encounter:zombie",
                            PositionCode = alliedPosition,
                            StateCode = FarmCombatPresentationCodes.Resolved,
                        },
                    },
                    Squads = new[]
                    {
                        Squad("squad:allied", FarmCombatPresentationCodes.Allied,
                            alliedPosition, 6),
                        Squad("squad:hostile", FarmCombatPresentationCodes.Hostile,
                            FarmCombatPresentationCodes.Perimeter, 0),
                    },
                    Orders = new[]
                    {
                        new FarmTacticalOrderApiModel
                        {
                            OrderStableId = "order:" + resolutionId,
                            FrontStableId = "front:farm",
                            ActorStableId = "actor:player",
                            OrderCode = orderCode,
                            StateCode = FarmCombatPresentationCodes.Resolved,
                        },
                    },
                    Resolutions = new[]
                    {
                        new FarmTacticalResolutionApiModel
                        {
                            ResolutionStableId = resolutionId,
                            OrderStableId = "order:" + resolutionId,
                            EncounterStableId = "encounter:zombie",
                            FrontStableId = "front:farm",
                            OrderCode = orderCode,
                            ResolvedWorldTick = (int)revision,
                            FrontPositionCode = alliedPosition,
                        },
                    },
                    SimulationOnly = true,
                    IsOperationalState = false,
                },
                SimulationOnly = true,
                IsOperationalState = false,
            };

        private static FarmTacticalSquadApiModel Squad(
            string id, string side, string position, int strength)
            => new()
            {
                SquadStableId = id,
                FrontStableId = "front:farm",
                SideCode = side,
                PositionCode = position,
                MemberCount = 6,
                CombatStrength = strength,
                MemberActorStableIds = Enumerable.Range(1, 6)
                    .Select(index => id + ":member:" + index.ToString("D2"))
                    .ToArray(),
            };
    }
}
