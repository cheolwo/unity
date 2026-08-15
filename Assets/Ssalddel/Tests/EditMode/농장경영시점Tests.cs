using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.PlayerActivities;
using Ssalddel.Unity.Presentation.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 농장경영시점Tests
    {
        private const string ScenePath =
            "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test]
        public void 시점전환곡선은_시작과끝을보존하고_중간에서위로완만하게휘어진다()
        {
            var start = new Vector3(0f, 1.7f, 0f);
            var end = new Vector3(12f, 9f, -8f);
            var middle = 카메라시점전환Math.EvaluateCurvedPosition(
                start, end, .5f, 2f);
            var linearMiddle = Vector3.Lerp(start, end, .5f);

            Assert.That(카메라시점전환Math.EvaluateCurvedPosition(
                start, end, 0f, 2f), Is.EqualTo(start));
            Assert.That(카메라시점전환Math.EvaluateCurvedPosition(
                start, end, 1f, 2f), Is.EqualTo(end));
            Assert.That(middle.y, Is.GreaterThan(linearMiddle.y));
            Assert.That(카메라시점전환Math.EaseInOut(.25f), Is.LessThan(.25f));
            Assert.That(카메라시점전환Math.EaseInOut(.75f), Is.GreaterThan(.75f));
        }

        [Test]
        public void 농장진입은_3인칭경영을기본으로하고_1인칭수동전환을허용한다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var player = Required<플레이어경관Controller>();
                var management = Required<농장경영시점Controller>();

                Assert.That(management.ValidateWiring(), Is.True);
                Assert.That(management.PresentationOnly, Is.True);

                player.EnterFarmManagementMode();
                Assert.That(player.CurrentActivityCode,
                    Is.EqualTo(PlayerActivityCodes.FarmManagement));
                Assert.That(player.CurrentMode,
                    Is.EqualTo(플레이어시점Mode.ThirdPerson));
                Assert.That(player.CurrentViewDecision.UsedActivityDefault, Is.True);
                Assert.That(management.IsActive, Is.True);

                player.EnterFarmManagementFirstPersonMode();
                Assert.That(player.CurrentMode,
                    Is.EqualTo(플레이어시점Mode.FirstPerson));
                Assert.That(player.CurrentViewDecision.ManualOverrideApplied, Is.True);
                Assert.That(management.IsActive, Is.False);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void 전술3인칭은_여러농지를선택해_확정되지않은작업초안을만든다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var player = Required<플레이어경관Controller>();
                var management = Required<농장경영시점Controller>();
                var targets = UnityEngine.Object
                    .FindObjectsByType<농장경영선택대상View>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(value => value.TargetKindCode == 농장경영대상종류Codes.Plot)
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(targets, Has.Length.EqualTo(10));

                player.EnterFarmManagementMode();
                management.SelectTarget(targets[0], false);
                management.SelectTarget(targets[1], true);
                management.SelectAction(농장경영작업Codes.Sow);
                var draft = management.CreateWorkDraft(new Vector3(-27f, 0f, 2f));

                Assert.That(management.SelectedTargets, Has.Count.EqualTo(2));
                Assert.That(draft.ActionCode, Is.EqualTo(농장경영작업Codes.Sow));
                Assert.That(draft.TargetStableIds, Is.Ordered);
                Assert.That(draft.RequiresExplicitConfirm, Is.True);
                Assert.That(draft.ChangesWorldState, Is.False);
                Assert.That(draft.PresentationOnly, Is.True);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T Required<T>() where T : UnityEngine.Object
            => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)
                ?? throw new AssertionException(typeof(T).Name + " 배선 누락");
    }
}
