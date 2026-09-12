using NUnit.Framework;
using Ssalddel.Unity.Presentation.NatureWoodcutting;

namespace Ssalddel.Unity.Editor.NatureWoodcuttingValidation.Tests
{
    public sealed class 벌목AnimationModelTests
    {
        private 벌목AnimationModel model;
        private static 벌목AnimationSnapshot S(long revision = 1, int seconds = 0, long token = 0,
            벌목WorkPhase phase = 벌목WorkPhase.Working, long generation = 1, string target = "tree",
            string session = "session", 벌목ReturnIntent intent = 벌목ReturnIntent.Idle)
            => new 벌목AnimationSnapshot(session, "farmer", target, generation, revision, 4, seconds, phase, token, intent);
        [SetUp] public void SetUp() { model = new 벌목AnimationModel(); Assert.That(model.ApplySnapshot(S()), Is.True); }
        private void Advance(int frames = 40)
        { for (int i = 0; i < frames; i++) Assert.That(model.TickPresentation(.05f), Is.True); }

        [Test] public void NoProgress_HoldsWindUpWithoutContactOrAuthorityChange()
        {
            var baseline = model.ReadState().Snapshot;
            Advance(200);
            Assert.That(model.ReadState().Segment, Is.EqualTo(벌목PlaybackSegment.AwaitingProgress));
            Assert.That(model.ReadState().Snapshot, Is.SameAs(baseline));
            Assert.That(model.TryConsumeContact(out _), Is.False);
        }
        [Test] public void ConfirmedToken_EmitsOnceAndDuplicateDoesNotRestart()
        {
            var value = S(2, 1, 1);
            Assert.That(model.ApplySnapshot(value), Is.True); Advance(10);
            var before = model.ReadState();
            Assert.That(model.ApplySnapshot(value), Is.True);
            Assert.That(model.ReadState().NormalizedTime, Is.EqualTo(before.NormalizedTime));
            Assert.That(model.TryConsumeContact(out var cue), Is.True);
            Assert.That(cue.Snapshot, Is.SameAs(value)); Assert.That(cue.ProgressToken, Is.EqualTo(1));
            Advance(); Assert.That(model.TryConsumeContact(out _), Is.False);
        }
        [Test] public void SameRevisionConflict_IsAtomic()
        {
            var before = model.ReadState().Snapshot;
            Assert.That(model.ApplySnapshot(S(1, 1, 1)), Is.False);
            Assert.That(model.ReadState().Snapshot, Is.SameAs(before));
        }
        [Test] public void OldRevisionOrGeneration_IsRejected()
        {
            Assert.That(model.ApplySnapshot(S(2)), Is.True);
            Assert.That(model.ApplySnapshot(S(1)), Is.False);
            Assert.That(model.ApplySnapshot(S(3, generation: 0)), Is.False);
        }
        [Test] public void TargetOrSessionChange_RequiresExplicitBaseline()
        {
            Assert.That(model.ApplySnapshot(S(2, target: "other")), Is.False);
            Assert.That(model.ApplySnapshot(S(2, session: "other")), Is.False);
            Assert.That(model.Reconstruct(S(1, session: "loaded")), Is.True);
        }
        [Test] public void ObservationGeneration_ResetsQueueWithoutHistoricalContact()
        {
            Assert.That(model.ApplySnapshot(S(2, 1, 1)), Is.True);
            Assert.That(model.ApplySnapshot(S(3, 2, 2, generation: 2)), Is.True);
            Advance(); Assert.That(model.TryConsumeContact(out _), Is.False);
            Assert.That(model.ReadState().ConsumedProgressToken, Is.Zero);
            Assert.That(model.ApplySnapshot(S(4, 3, 3, generation: 2)), Is.True);
            Advance(); Assert.That(model.TryConsumeContact(out var cue), Is.True);
            Assert.That(cue.ProgressToken, Is.EqualTo(3));
        }
        [Test] public void SkippedProgress_CollapsesToLatestOneContact()
        {
            Assert.That(model.ApplySnapshot(S(2, 1, 1)), Is.True);
            Assert.That(model.ApplySnapshot(S(3, 3, 3)), Is.True);
            Advance(); Assert.That(model.TryConsumeContact(out var cue), Is.True);
            Assert.That(cue.ProgressToken, Is.EqualTo(3));
            Advance(); Assert.That(model.TryConsumeContact(out _), Is.False);
        }
        [Test] public void TokenWithoutProgressAndRegressedToken_AreRejected()
        {
            Assert.That(model.ApplySnapshot(S(2, 0, 1)), Is.False);
            Assert.That(model.ApplySnapshot(S(2, 2, 2)), Is.True);
            Assert.That(model.ApplySnapshot(S(3, 3, 1)), Is.False);
            Assert.That(model.ApplySnapshot(S(3, 1, 3)), Is.False);
        }
        [Test] public void ReusedToken_DoesNotReplay()
        {
            Assert.That(model.ApplySnapshot(S(2, 1, 1)), Is.True); Advance();
            Assert.That(model.TryConsumeContact(out _), Is.True);
            Assert.That(model.ApplySnapshot(S(3, 2, 1)), Is.True); Advance();
            Assert.That(model.TryConsumeContact(out _), Is.False);
        }
        [Test] public void Reconstruct_ClearsAlreadyProducedAndPendingContact()
        {
            Assert.That(model.ApplySnapshot(S(2, 1, 1)), Is.True); Advance(10);
            Assert.That(model.ApplySnapshot(S(3, 2, 2)), Is.True);
            Assert.That(model.Reconstruct(S(3, 2, 2)), Is.True); Advance();
            Assert.That(model.TryConsumeContact(out _), Is.False);
            Assert.That(model.ReadState().PendingProgressToken, Is.Zero);
        }
        [Test] public void CancelDuringStrike_ClearsContactAndReturnsWithWalkIntent()
        {
            Assert.That(model.ApplySnapshot(S(2, 1, 1)), Is.True); Advance(8);
            Assert.That(model.ApplySnapshot(S(3, 1, 1, 벌목WorkPhase.Cancelled, intent: 벌목ReturnIntent.Walk)), Is.True);
            Advance(); Assert.That(model.TryConsumeContact(out _), Is.False);
            Assert.That(model.ReadState().ReturnReady, Is.True);
            Assert.That(model.ReadState().Snapshot.ReturnIntent, Is.EqualTo(벌목ReturnIntent.Walk));
        }
        [Test] public void Completed_ConsumesFinalTokenOnceThenReturns()
        {
            Assert.That(model.ApplySnapshot(S(2, 4, 4, 벌목WorkPhase.Completed)), Is.True);
            Advance(); Assert.That(model.TryConsumeContact(out var cue), Is.True);
            Assert.That(cue.ProgressToken, Is.EqualTo(4));
            Assert.That(model.ReadState().ReturnReady, Is.True);
            Advance(); Assert.That(model.TryConsumeContact(out _), Is.False);
        }
        [Test] public void RepeatedTerminalRevision_DoesNotRestartReturn()
        {
            Assert.That(model.ApplySnapshot(S(2, phase: 벌목WorkPhase.Cancelled)), Is.True);
            Advance(); Assert.That(model.ReadState().ReturnReady, Is.True);
            Assert.That(model.ApplySnapshot(S(3, phase: 벌목WorkPhase.Cancelled)), Is.True);
            Assert.That(model.ReadState().ReturnReady, Is.True);
            Assert.That(model.ApplySnapshot(S(4)), Is.False);
        }
        [Test] public void ExplicitInterrupt_ClearsUnconsumedContact()
        {
            Assert.That(model.ApplySnapshot(S(2, 1, 1)), Is.True); Advance(10);
            model.InterruptToIdle(); Advance();
            Assert.That(model.TryConsumeContact(out _), Is.False);
            Assert.That(model.ReadState().ReturnReady, Is.True);
        }
        [Test] public void InvalidDeltaTime_DoesNotAdvance()
        {
            Assert.That(model.TickPresentation(float.NaN), Is.False);
            Assert.That(model.TickPresentation(float.PositiveInfinity), Is.False);
            Assert.That(model.TickPresentation(-1), Is.False);
            Assert.That(model.ReadState().NormalizedTime, Is.Zero);
        }
        [Test] public void FourSecondTaskContract_InvalidInputDoesNotReplaceBaseline()
        {
            var before = model.ReadState().Snapshot;
            Assert.That(model.ApplySnapshot(new 벌목AnimationSnapshot("session", "farmer", "tree", 1, 2, 5, 0, 벌목WorkPhase.Working)), Is.False);
            Assert.That(model.ReadState().Snapshot, Is.SameAs(before));
        }
    }
}
