using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Npc업무행동Tests
    {
        [Test]
        public void 서버상태사본은_배정이동작업완료단계를_단조롭게투영한다()
        {
            var store = new Npc업무행동ProjectionStore();
            store.Apply(new[] { 평창진부HubNpc업무행동Fixture.Create() });
            store.Apply(new[]
            {
                평창진부HubNpc업무행동Fixture.Create(
                    Npc업무행동PhaseCodes.Navigating, 1, 2),
            });
            store.Apply(new[]
            {
                평창진부HubNpc업무행동Fixture.Create(
                    Npc업무행동PhaseCodes.Working, 2, 3, .5m),
            });
            store.Apply(new[]
            {
                평창진부HubNpc업무행동Fixture.Create(
                    Npc업무행동PhaseCodes.Completed, 3, 4, 1m),
            });

            var current = AssertSingle(store.Current);
            Assert.That(current.PhaseCode, Is.EqualTo(Npc업무행동PhaseCodes.Completed));
            Assert.That(current.ProgressRate, Is.EqualTo(1m));
            Assert.Throws<InvalidOperationException>(() => store.Apply(new[]
            {
                평창진부HubNpc업무행동Fixture.Create(
                    Npc업무행동PhaseCodes.Working, 2, 3, .5m),
            }));
        }

        [Test]
        public void SimulationWorldShell은_진부Hub세Npc와_입고검수상호작용지점을연결한다()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/SimulationWorldShell.unity",
                OpenSceneMode.Single);

            var presenter = UnityEngine.Object.FindAnyObjectByType<Npc업무행동Presenter>(
                FindObjectsInactive.Include);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter!.PresentationOnly, Is.True);
            foreach (var view in presenter.Views)
                Assert.That(view.ValidateWiring(), Is.True, view.ActorStableId);
            Assert.That(presenter.ValidateWiring(), Is.True);
            Assert.That(presenter.Views, Has.Length.EqualTo(3));
            Assert.That(presenter.Views.Select(value => value.ActorStableId),
                Is.EquivalentTo(new[]
                {
                    평창진부HubNpc업무행동Fixture.ManagerActorStableId,
                    평창진부HubNpc업무행동Fixture.InboundOperatorActorStableId,
                    평창진부HubNpc업무행동Fixture.AssistantActorStableId,
                }));
            var projection = AssertSingle(presenter.CurrentProjections);
            Assert.That(projection.ActorStableId,
                Is.EqualTo(평창진부HubNpc업무행동Fixture.InboundOperatorActorStableId));
            Assert.That(projection.PresentationOnly, Is.True);
        }

        [Test]
        public void 이동단계는_입고담당자의표현위치만_상호작용지점으로가깝게한다()
        {
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/SimulationWorldShell.unity",
                OpenSceneMode.Single);

            var presenter = UnityEngine.Object.FindAnyObjectByType<Npc업무행동Presenter>(
                FindObjectsInactive.Include)!;
            var view = presenter.Views.Single(value => value.ActorStableId ==
                평창진부HubNpc업무행동Fixture.InboundOperatorActorStableId);
            var before = Vector3.Distance(view.transform.position, view.InteractionPoint.position);

            presenter.ApplyAuthoritativeProjections(new[]
            {
                평창진부HubNpc업무행동Fixture.Create(
                    Npc업무행동PhaseCodes.Navigating, 1, 2),
            });
            presenter.TickPresentation(.5f);

            var after = Vector3.Distance(view.transform.position, view.InteractionPoint.position);
            Assert.That(after, Is.LessThan(before));
            Assert.That(view.CurrentProjection!.PhaseCode,
                Is.EqualTo(Npc업무행동PhaseCodes.Navigating));
            Assert.That(view.PresentationOnly, Is.True);
        }

        private static T AssertSingle<T>(System.Collections.Generic.IReadOnlyList<T> values)
        {
            Assert.That(values.Count, Is.EqualTo(1));
            return values[0];
        }
    }
}
