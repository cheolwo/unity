using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Hans농장울타리E5PresenterTests
    {
        [Test]
        public void 손상과수리상태는_같은세구간의_최소World표현으로교체된다()
        {
            var root = new GameObject("HansFarmE5Root");
            try
            {
                var presenter = root.AddComponent<Hans농장울타리E5Presenter>();
                presenter.Configure(root.transform);
                var state = State(SimulationNatureSurvivalCodes.Damaged,
                    SimulationNatureSurvivalCodes.Available);

                presenter.Render(state);

                Assert.That(presenter.SegmentObjects.Count, Is.EqualTo(3));
                Assert.That(presenter.BrokenAxeObject, Is.Not.Null);
                Assert.That(presenter.SegmentObjects.All(HasVisibleBoundsAndCollider),
                    Is.True);
                Assert.That(HasVisibleBoundsAndCollider(
                    presenter.BrokenAxeObject!), Is.True);
                Assert.That(presenter.SegmentObjects.All(value =>
                    value.GetComponent<Hans농장울타리E5표식>()
                        .VisualKey == Hans농장울타리E5Presenter
                            .DamagedFenceVisualKey), Is.True);

                state.HansFarmFenceRestoration!.FenceStateCode =
                    SimulationNatureSurvivalCodes.Repaired;
                state.HansFarmFenceRestoration.BrokenAxeStateCode =
                    SimulationNatureSurvivalCodes.Carried;
                foreach (var segment in state.HansFarmFenceRestoration.Segments)
                {
                    segment.StateCode = SimulationNatureSurvivalCodes.Repaired;
                    segment.YawDegrees = 90d;
                }
                presenter.Render(state);

                Assert.That(presenter.RenderedFenceStateCode,
                    Is.EqualTo(SimulationNatureSurvivalCodes.Repaired));
                Assert.That(presenter.BrokenAxeObject, Is.Null);
                Assert.That(presenter.SegmentObjects.Count, Is.EqualTo(3));
                Assert.That(presenter.SegmentObjects.All(value =>
                    value.GetComponent<Hans농장울타리E5표식>()
                        .VisualKey == Hans농장울타리E5Presenter
                            .RepairedFenceVisualKey), Is.True);
                Assert.That(root.GetComponentsInChildren<Animator>(true),
                    Is.Empty);
                Assert.That(Hans농장울타리E5Presenter.AnimationRole,
                    Is.EqualTo("NotApplicable"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 잘못된상태사본은_기존World표현을지우기전에거부된다()
        {
            var root = new GameObject("HansFarmE5InvalidSnapshotRoot");
            try
            {
                var presenter = root.AddComponent<Hans농장울타리E5Presenter>();
                presenter.Configure(root.transform);
                presenter.Render(State(SimulationNatureSurvivalCodes.Damaged,
                    SimulationNatureSurvivalCodes.Available));
                var before = presenter.SegmentObjects.ToArray();

                var invalid = State(SimulationNatureSurvivalCodes.Repaired,
                    SimulationNatureSurvivalCodes.Carried);
                invalid.HansFarmFenceRestoration!.Segments[1].StateCode =
                    SimulationNatureSurvivalCodes.Damaged;

                var error = Assert.Throws<InvalidOperationException>(() =>
                    presenter.Render(invalid));
                Assert.That(error!.Message,
                    Is.EqualTo("HansFarmFenceSegmentStateMismatch"));
                Assert.That(presenter.SegmentObjects, Is.EqualTo(before));
                Assert.That(presenter.RenderedFenceStateCode,
                    Is.EqualTo(SimulationNatureSurvivalCodes.Damaged));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool HasVisibleBoundsAndCollider(GameObject value)
        {
            var renderers = value.GetComponentsInChildren<Renderer>(true);
            return renderers.Length > 0
                && renderers.All(item => item.enabled
                    && item.bounds.size.sqrMagnitude > 0f)
                && value.GetComponentsInChildren<Collider>(true).Length > 0;
        }

        private static SimulationNatureSurvivalStateSnapshot State(
            string fenceStateCode, string axeStateCode)
            => new SimulationNatureSurvivalStateSnapshot
            {
                IsEnabled = true,
                ProfileRevision =
                    SimulationNatureSurvivalCodes.ProfileRevisionR6,
                HansFarmFenceRestoration =
                    new SimulationHansFarmFenceRestorationSnapshot
                    {
                        FenceStateCode = fenceStateCode,
                        BrokenAxeStateCode = axeStateCode,
                        Segments = new[]
                        {
                            Segment(1, -2.4d, 90d),
                            Segment(2, 0d, 82d),
                            Segment(3, 2.4d, 96d),
                        },
                    },
            };

        private static SimulationHansFarmFenceSegmentSnapshot Segment(
            int index, double x, double yaw)
            => new SimulationHansFarmFenceSegmentSnapshot
            {
                SegmentStableId =
                    $"fence-segment:hans-farm:first-incident:{index:00}",
                StateCode = SimulationNatureSurvivalCodes.Damaged,
                LocalX = x,
                LocalZ = 2.2d,
                YawDegrees = yaw,
            };
    }
}
