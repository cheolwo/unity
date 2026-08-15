using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Survival;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 전술분대이동Tests
    {
        private const string ScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public void 횡대_쐐기_종대는_각각결정적이고중복없는6개슬롯을가진다()
        {
            foreach (var formation in new[]
            {
                FarmCombatPresentationCodes.LineFormation,
                FarmCombatPresentationCodes.WedgeFormation,
                FarmCombatPresentationCodes.ColumnFormation,
            })
            {
                var first = Enumerable.Range(0, 6)
                    .Select(index => 전술분대대형Controller.CalculateSlot(
                        formation, index)).ToArray();
                var second = Enumerable.Range(0, 6)
                    .Select(index => 전술분대대형Controller.CalculateSlot(
                        formation, index)).ToArray();
                Assert.That(second, Is.EqualTo(first));
                Assert.That(first.Distinct().Count(), Is.EqualTo(6));
            }

            var line = Enumerable.Range(0, 6)
                .Select(index => 전술분대대형Controller.CalculateSlot(
                    FarmCombatPresentationCodes.LineFormation, index)).ToArray();
            var column = Enumerable.Range(0, 6)
                .Select(index => 전술분대대형Controller.CalculateSlot(
                    FarmCombatPresentationCodes.ColumnFormation, index)).ToArray();
            Assert.That(line.Max(value => value.x) - line.Min(value => value.x),
                Is.GreaterThan(line.Max(value => value.z) - line.Min(value => value.z)));
            Assert.That(column.Max(value => value.x) - column.Min(value => value.x),
                Is.LessThan(column.Max(value => value.z) - column.Min(value => value.z)));
        }

        [Test]
        public void 저장Scene은_두분대중심과12명wrapper및전술대장을가진다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath,
                OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == "SimulationWorldShell");
                var presenter = root.GetComponentInChildren<전술분대Presenter>(true);
                var combat = root.GetComponentInChildren<전투시점Controller>(true);
                Assert.That(presenter, Is.Not.Null);
                Assert.That(combat.TacticalSquads, Is.EqualTo(presenter));
                Assert.That(presenter.ValidateWiring(), Is.True);
                Assert.That(presenter.Squads.Count, Is.EqualTo(2));
                Assert.That(presenter.Squads.Sum(value => value.Members.Count),
                    Is.EqualTo(12));
                Assert.That(presenter.Squads.Select(value =>
                    value.NavigationAgent).Distinct().Count(), Is.EqualTo(2));
                Assert.That(presenter.Squads.SelectMany(value => value.Members)
                    .All(value => value.AnimationAdapter.RootMotionDisabled
                        && value.PresentationOnly), Is.True);
                var allied = presenter.Squads.Single(value =>
                    value.SideCode == FarmCombatPresentationCodes.Allied);
                var hostile = presenter.Squads.Single(value =>
                    value.SideCode == FarmCombatPresentationCodes.Hostile);
                Assert.That(allied.RestingFacingYaw, Is.EqualTo(0f));
                Assert.That(hostile.RestingFacingYaw, Is.EqualTo(180f));
                Assert.That(Vector3.Distance(allied.transform.position,
                    hostile.transform.position), Is.GreaterThan(6f));
                Assert.That(Quaternion.Angle(allied.transform.rotation,
                    hostile.transform.rotation), Is.GreaterThan(170f));

                var farm = root.transform.Find(
                    "SettlementInteriorRoot/Districts/FarmDistrict");
                var districtSurface = farm.Find("DistrictSurface")
                    .GetComponent<Renderer>();
                var navigationFloor = farm.Find(
                    "TacticalBattleRoot/NavigationRoot/TacticalWalkableFloor")
                    .GetComponent<Renderer>();
                Assert.That(navigationFloor.enabled, Is.False);
                Assert.That(navigationFloor.GetComponent<BoxCollider>().enabled,
                    Is.True);
                Assert.That(navigationFloor.bounds.max.y,
                    Is.EqualTo(districtSurface.bounds.max.y).Within(.005f));
                foreach (var squadMember in presenter.Squads
                             .SelectMany(value => value.Members))
                {
                    var visualRoot = squadMember.transform.GetChild(0);
                    Assert.That(캐릭터지면정렬Utility.TryGetVisibleBounds(
                        visualRoot, out var visibleBounds), Is.True);
                    Assert.That(visibleBounds.min.y,
                        Is.GreaterThanOrEqualTo(
                            districtSurface.bounds.max.y + .02f));
                }

                var member = allied.Members[0];
                member.AnimationAdapter.TickPresentation(.1f);
                var phaseBeforeRebind = member.AnimationAdapter.PresentationPhase;
                member.RebindStableMemberId(member.StableMemberId);
                Assert.That(member.AnimationAdapter.PresentationPhase,
                    Is.EqualTo(phaseBeforeRebind));

                var catalog = AssetDatabase.LoadAssetAtPath<전술CharacterVisualCatalog>(
                    "Assets/Ssalddel/Presentation/World/Catalogs/"
                    + "평창군전술CharacterVisualCatalog.asset");
                Assert.That(catalog, Is.Not.Null);
                Assert.DoesNotThrow(catalog.Validate);
                Assert.That(catalog.Entries.Count(value =>
                    value.SideCode == FarmCombatPresentationCodes.Allied),
                    Is.EqualTo(3));
                Assert.That(catalog.Entries.Count(value =>
                    value.SideCode == FarmCombatPresentationCodes.Hostile),
                    Is.EqualTo(2));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 같은결과는한번만적용하고_낮은개정은거부한다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath,
                OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), value =>
                    value.name == "SimulationWorldShell");
                var presenter = root.GetComponentInChildren<전술분대Presenter>(true);
                var state = ResolvedState(10, "resolution:one",
                    FarmCombatPresentationCodes.AdvanceAndAttack,
                    FarmCombatPresentationCodes.Forward);

                Assert.That(presenter.TryApplyServerState(state, out var frame),
                    Is.True);
                Assert.That(frame, Is.Not.Null);
                Assert.That(presenter.TryApplyServerState(state, out _), Is.False);

                var stale = ResolvedState(9, "resolution:two",
                    FarmCombatPresentationCodes.HoldFormation,
                    FarmCombatPresentationCodes.Perimeter);
                Assert.Throws<InvalidOperationException>(() =>
                    presenter.TryApplyServerState(stale, out _));
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        internal static FarmCombatStateApiModel ResolvedState(long revision,
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
                            alliedPosition, 6, 6),
                        Squad("squad:hostile", FarmCombatPresentationCodes.Hostile,
                            FarmCombatPresentationCodes.Perimeter, 6, 0),
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

        private static FarmTacticalSquadApiModel Squad(string id, string side,
            string position, int members, int strength)
            => new()
            {
                SquadStableId = id,
                FrontStableId = "front:farm",
                SideCode = side,
                PositionCode = position,
                MemberCount = members,
                CombatStrength = strength,
                MemberActorStableIds = Enumerable.Range(1, members)
                    .Select(index => id + ":member:" + index.ToString("D2"))
                    .ToArray(),
            };
    }
}
