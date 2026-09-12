using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.NatureWoodcutting;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Editor.NatureWoodcuttingValidation.Tests
{
    // 모두 분리된 Fixture이다. 행위 원장 실제 조회/Graph/Scene/Game View의 증거가 아니다.
    public sealed class 벌목TaskObservationProjectorTests
    {
        private 벌목TaskObservationProjector projector;
        [SetUp] public void SetUp() => projector = new 벌목TaskObservationProjector();

        private static Nature생존State S(long revision = 10, int seconds = 0, string command = "start:1", bool active = true)
            => new Nature생존State
            {
                SessionStableId = "fixture:session", PlayerStableId = "fixture:actor", Revision = revision,
                ActiveWork = active ? new Nature생존WorkState
                {
                    KindCode = Nature생존Codes.Harvest, ActorStableId = "fixture:actor", OriginCommandId = command,
                    TargetStableId = "fixture:tree", RequiredSeconds = 4, CompletedSeconds = seconds,
                } : null,
                ResourceNodes = new[]
                {
                    new Nature생존ResourceNodeState { StableId = "fixture:tree", StateCode = Nature생존Codes.Standing },
                    new Nature생존ResourceNodeState { StableId = "fixture:other-tree", StateCode = Nature생존Codes.Standing },
                },
            };
        private static 벌목TaskObservationContext C(long epoch = 1, bool held = true, bool reconstruct = false,
            벌목ReturnIntent intent = 벌목ReturnIntent.Idle, 벌목TaskTerminalEvidence evidence = null)
            => new 벌목TaskObservationContext(epoch, held, reconstruct, intent, evidence);
        private static 벌목TaskTerminalEvidence E(long revision = 11, 벌목TerminalOutcome outcome = 벌목TerminalOutcome.Completed,
            string session = "fixture:session", string actor = "fixture:actor", string origin = "start:1",
            string target = "fixture:tree", long epoch = 1, string id = "fixture:evidence:1")
            => new 벌목TaskTerminalEvidence(session, actor, origin, target, epoch, revision, outcome, id);
        private static Nature생존State End(long revision = 11, bool stump = true)
        {
            var state = S(revision, active: false);
            state.ResourceNodes[0].StateCode = stump ? Nature생존Codes.Stump : Nature생존Codes.Standing;
            return state;
        }
        private static void Dispatch(벌목AnimationModel model, 벌목TaskProjection projection)
        {
            Assert.That(projection.Accepted, Is.True, projection.Diagnostic);
            switch (projection.Dispatch)
            {
                case 벌목ProjectionDispatch.Apply: Assert.That(model.ApplySnapshot(projection.Snapshot), Is.True); break;
                case 벌목ProjectionDispatch.Reconstruct: Assert.That(model.Reconstruct(projection.Snapshot), Is.True); break;
                case 벌목ProjectionDispatch.Interrupt: model.InterruptToIdle(); break;
            }
        }
        private static int TickAndCount(벌목AnimationModel model, 벌목TaskProjection control, int frames = 100)
        {
            int count = 0;
            for (int i = 0; i < frames; i++)
            {
                if (control.ShouldTick) Assert.That(model.TickPresentation(.05f), Is.True);
                if (model.TryConsumeContact(out _)) count++;
            }
            return count;
        }

        [TestCase(0)] [TestCase(2)]
        public void 최초진행은_과거접촉없는기준선(int seconds)
        {
            var result = projector.Project(S(seconds: seconds), C());
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Reconstruct));
            Assert.That(result.Snapshot.CompletedSeconds, Is.EqualTo(seconds));
            Assert.That(result.Snapshot.ProgressToken, Is.Zero);
            var model = new 벌목AnimationModel(); Dispatch(model, result);
            Assert.That(TickAndCount(model, result), Is.Zero);
        }
        [Test] public void 최초Idle와이미Stump는_완료아님()
        {
            var result = projector.Project(End(), C());
            Assert.That(result.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Idle));
            Assert.That(result.Snapshot.ProgressToken, Is.Zero);
            Assert.That(projector.Project(End(12), C(evidence: E())).Dispatch, Is.EqualTo(벌목ProjectionDispatch.None));
        }
        [Test] public void 같은Revision_제어만변해도Snapshot은불변()
        {
            var state = S(); var first = projector.Project(state, C());
            state.ClockPaused = true;
            var result = projector.Project(state, C(held: false, intent: 벌목ReturnIntent.Walk));
            Assert.That(result.Accepted, Is.True); Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.None));
            Assert.That(result.Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(result.Snapshot.ReturnIntent, Is.EqualTo(벌목ReturnIntent.Idle));
            Assert.That(result.ReturnIntent, Is.EqualTo(벌목ReturnIntent.Walk)); Assert.That(result.ShouldTick, Is.False);
            state.ClockPaused = false;
            result = projector.Project(state, C());
            Assert.That(result.ShouldTick, Is.True); Assert.That(result.Snapshot, Is.SameAs(first.Snapshot));
        }
        [TestCase("progress")] [TestCase("origin")] [TestCase("node")]
        public void 동일Revision의권위변경은_원자적거부(string mutation)
        {
            var first = projector.Project(S(), C()); var next = S();
            if (mutation == "progress") next.ActiveWork.CompletedSeconds = 1;
            if (mutation == "origin") next.ActiveWork.OriginCommandId = "start:other";
            if (mutation == "node") next.ResourceNodes[1].StateCode = Nature생존Codes.Stump;
            var rejected = projector.Project(next, C());
            Assert.That(rejected.Accepted, Is.False); Assert.That(rejected.Diagnostic, Is.EqualTo("SameRevisionConflict"));
            Assert.That(projector.ReadState().Snapshot, Is.SameAs(first.Snapshot));
        }
        [Test] public void 동일Epoch의낮은Revision과진행후퇴는_거부()
        {
            var first = projector.Project(S(10, 2), C());
            Assert.That(projector.Project(S(9, 2), C()).Diagnostic, Is.EqualTo("RevisionRegressed"));
            Assert.That(projector.Project(S(11, 1), C()).Diagnostic, Is.EqualTo("ProgressRegressed"));
            Assert.That(projector.ReadState().Snapshot, Is.SameAs(first.Snapshot));
        }
        [Test] public void 새Epoch만_낮은Revision복원과큐폐기()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            Dispatch(model, projector.Project(S(11, 1), C()));
            var restored = projector.Project(S(2, 2), C(2, reconstruct: true));
            Dispatch(model, restored);
            Assert.That(restored.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Reconstruct));
            Assert.That(TickAndCount(model, restored), Is.Zero);
            Assert.That(projector.Project(S(2, 2), C(2, reconstruct: true)).Diagnostic,
                Is.EqualTo("ReconstructionEpochNotAdvanced"));
            Assert.That(projector.Project(S(2, 2), C(2)).Dispatch, Is.EqualTo(벌목ProjectionDispatch.None));
            Assert.That(projector.Project(S(12), C(1)).Diagnostic, Is.EqualTo("EpochRegressed"));
        }
        [Test] public void 새Epoch동일Revision도_Reconstruct직접호출()
        {
            var model = new 벌목AnimationModel(); var first = projector.Project(S(), C()); Dispatch(model, first);
            var restored = projector.Project(S(), C(2, reconstruct: true)); Dispatch(model, restored);
            Assert.That(restored.Snapshot.ObservationGeneration, Is.GreaterThan(first.Snapshot.ObservationGeneration));
            Assert.That(model.ReadState().Snapshot, Is.SameAs(restored.Snapshot));
        }
        [TestCase(2, 9)] [TestCase(2, 10)] [TestCase(2, 11)] [TestCase(1, 10)]
        public void 명시복원은_Epoch미증가시원자적거부(long requestedEpoch, long revision)
        {
            var first = projector.Project(S(), C(2)); var before = projector.ReadState();
            var rejected = projector.Project(S(revision), C(requestedEpoch, reconstruct: true));
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Diagnostic, Is.EqualTo("ReconstructionEpochNotAdvanced"));
            Assert.That(rejected.Dispatch, Is.EqualTo(벌목ProjectionDispatch.None));
            Assert.That(rejected.ShouldTick, Is.False);
            Assert.That(projector.ReadState().Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(projector.ReadState().ObservationEpoch, Is.EqualTo(before.ObservationEpoch));
            Assert.That(projector.ReadState().ObservationGeneration, Is.EqualTo(before.ObservationGeneration));
            Assert.That(projector.ReadState().Revision, Is.EqualTo(before.Revision));
            Assert.That(projector.ReadState().OriginCommandId, Is.EqualTo(before.OriginCommandId));
        }
        [TestCase("session", 9)] [TestCase("session", 10)] [TestCase("session", 11)]
        [TestCase("player", 9)] [TestCase("player", 10)] [TestCase("player", 11)]
        public void 소스교체는_Revision과무관하게새Epoch필수(string changed, long revision)
        {
            var first = projector.Project(S(), C()); var state = S(revision, 2);
            if (changed == "session") state.SessionStableId = "fixture:new-session";
            else { state.PlayerStableId = "fixture:new-player"; state.ActiveWork.ActorStableId = "fixture:new-player"; }
            var rejected = projector.Project(state, C());
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Diagnostic, Is.EqualTo("SourceEpochNotAdvanced"));
            Assert.That(projector.ReadState().Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(projector.ReadState().ObservationEpoch, Is.EqualTo(1));
            Assert.That(projector.ReadState().Revision, Is.EqualTo(10));
            var accepted = projector.Project(state, C(2, reconstruct: true));
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Reconstruct));
            Assert.That(accepted.Snapshot.Revision, Is.EqualTo(revision));
            Assert.That(accepted.Snapshot.ProgressToken, Is.Zero);
            Assert.That(accepted.Snapshot.ObservationGeneration, Is.GreaterThan(first.Snapshot.ObservationGeneration));
            var model = new 벌목AnimationModel(); Dispatch(model, first); Dispatch(model, accepted);
            Assert.That(TickAndCount(model, accepted), Is.Zero);
        }
        [Test] public void 최초명시복원은_이전Epoch없이기준선허용()
        {
            var result = projector.Project(S(2, 2), C(0, reconstruct: true));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Reconstruct));
            Assert.That(result.Snapshot.ProgressToken, Is.Zero);
        }
        [Test] public void Origin변경은_같은나무라도새세대와접촉폐기()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            var pending = projector.Project(S(11, 1), C()); Dispatch(model, pending);
            var restarted = projector.Project(S(12, 2, "start:2"), C()); Dispatch(model, restarted);
            Assert.That(restarted.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Reconstruct));
            Assert.That(restarted.Snapshot.ObservationGeneration, Is.GreaterThan(pending.Snapshot.ObservationGeneration));
            Assert.That(restarted.Snapshot.ProgressToken, Is.Zero); Assert.That(TickAndCount(model, restarted), Is.Zero);
        }
        [Test] public void 건너뛴진행은_최신접촉하나만발행()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            Dispatch(model, projector.Project(S(11, 1), C()));
            var latest = projector.Project(S(15, 3), C()); Dispatch(model, latest);
            Assert.That(latest.Snapshot.ProgressToken, Is.EqualTo(3)); Assert.That(TickAndCount(model, latest), Is.EqualTo(1));
            var repeated = projector.Project(S(16, 3), C()); Dispatch(model, repeated);
            Assert.That(TickAndCount(model, repeated), Is.Zero);
        }
        [Test] public void 완료근거와상태일치만_마지막토큰한번()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(10, 3), C()));
            var completed = projector.Project(End(), C(held: false, evidence: E())); Dispatch(model, completed);
            Assert.That(completed.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Completed));
            Assert.That(completed.Snapshot.ProgressToken, Is.EqualTo(4)); Assert.That(completed.ShouldTick, Is.True);
            Assert.That(TickAndCount(model, completed), Is.EqualTo(1)); Assert.That(model.ReadState().ReturnReady, Is.True);
            var duplicate = projector.Project(End(12), C(held: false, evidence: E())); Dispatch(model, duplicate);
            Assert.That(duplicate.Dispatch, Is.EqualTo(벌목ProjectionDispatch.None)); Assert.That(TickAndCount(model, duplicate), Is.Zero);
        }
        [Test] public void Stump변화만으로_우리작업완료를추정하지않음()
        {
            projector.Project(S(10, 3), C());
            var result = projector.Project(End(), C(held: false));
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Interrupt));
            Assert.That(result.Diagnostic, Is.EqualTo("CompletionEvidenceMissing"));
            Assert.That(result.ShouldTick, Is.True); Assert.That(result.Snapshot.Phase, Is.Not.EqualTo(벌목WorkPhase.Completed));
        }
        [TestCase("actor")] [TestCase("origin")] [TestCase("session")] [TestCase("target")]
        [TestCase("epoch")] [TestCase("oldRevision")] [TestCase("futureRevision")] [TestCase("noId")]
        public void 다른행위자나취소후다른작업의근거는_완료거부(string wrong)
        {
            projector.Project(S(10, 3), C());
            var evidence = E(actor: wrong == "actor" ? "other:actor" : "fixture:actor",
                origin: wrong == "origin" ? "start:after-cancel" : "start:1",
                session: wrong == "session" ? "other:session" : "fixture:session",
                target: wrong == "target" ? "fixture:other-tree" : "fixture:tree",
                epoch: wrong == "epoch" ? 2 : 1,
                revision: wrong == "oldRevision" ? 10 : wrong == "futureRevision" ? 12 : 11,
                id: wrong == "noId" ? "" : "fixture:evidence:1");
            var result = projector.Project(End(), C(evidence: evidence));
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Interrupt));
            Assert.That(result.Diagnostic, Is.EqualTo("TerminalEvidenceMismatch"));
        }
        [Test] public void 완료근거만있고해당나무상태없으면_거부()
        {
            projector.Project(S(), C()); var end = End(stump: false);
            end.ResourceNodes[1].StateCode = Nature생존Codes.Stump;
            var result = projector.Project(end, C(evidence: E()));
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Interrupt));
            Assert.That(result.Diagnostic, Is.EqualTo("TerminalStateEvidenceConflict"));
        }
        [Test] public void 취소는_미소비접촉폐기와held없는복귀()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            Dispatch(model, projector.Project(S(11, 1), C()));
            var result = projector.Project(End(12, false), C(held: false, intent: 벌목ReturnIntent.Walk,
                evidence: E(12, 벌목TerminalOutcome.Cancelled))); Dispatch(model, result);
            Assert.That(result.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Cancelled)); Assert.That(result.ShouldTick, Is.True);
            Assert.That(result.ReturnIntent, Is.EqualTo(벌목ReturnIntent.Walk));
            Assert.That(TickAndCount(model, result), Is.Zero); Assert.That(model.ReadState().ReturnReady, Is.True);
            // 취소 뒤 다른 행위자가 같은 나무를 바꿔도 우리 완료로 되살리지 않는다.
            var later = projector.Project(End(13), C(evidence: E(13))); Dispatch(model, later);
            Assert.That(later.Dispatch, Is.EqualTo(벌목ProjectionDispatch.None)); Assert.That(TickAndCount(model, later), Is.Zero);
        }
        [Test] public void 미확인종료는_Interrupt뒤늦은근거로재생하지않음()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            Dispatch(model, projector.Project(S(11, 1), C()));
            var end = projector.Project(End(12), C(held: false)); Dispatch(model, end);
            Assert.That(TickAndCount(model, end), Is.Zero); Assert.That(model.ReadState().ReturnReady, Is.True);
            var late = projector.Project(End(12), C(evidence: E(12)));
            Assert.That(late.Dispatch, Is.EqualTo(벌목ProjectionDispatch.None));
        }
        [Test] public void held정지는_권위취소없이진행정지후재개()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            var progress = projector.Project(S(11, 1), C()); Dispatch(model, progress);
            var heldStop = projector.Project(S(11, 1), C(held: false)); Dispatch(model, heldStop);
            Assert.That(heldStop.ShouldTick, Is.False); Assert.That(heldStop.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Working));
            Assert.That(TickAndCount(model, heldStop), Is.Zero);
            var resumed = projector.Project(S(11, 1), C()); Dispatch(model, resumed);
            Assert.That(resumed.Snapshot, Is.SameAs(progress.Snapshot)); Assert.That(TickAndCount(model, resumed), Is.EqualTo(1));
        }
        [Test] public void ClockPaused는_완료복귀시간도멈추고같은Revision재개()
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(10, 3), C()));
            var state = End(); state.ClockPaused = true;
            var end = projector.Project(state, C(held: false, evidence: E())); Dispatch(model, end);
            Assert.That(end.ShouldTick, Is.False); Assert.That(TickAndCount(model, end), Is.Zero);
            state.ClockPaused = false; var resumed = projector.Project(state, C(held: false)); Dispatch(model, resumed);
            Assert.That(resumed.ShouldTick, Is.True); Assert.That(TickAndCount(model, resumed), Is.EqualTo(1));
        }
        [Test] public void 호출자입력재사용은_지난관측을변조하지않음()
        {
            var state = S(); projector.Project(state, C());
            state.Revision = 11; state.ActiveWork.CompletedSeconds = 2;
            var progress = projector.Project(state, C()); Assert.That(progress.Snapshot.ProgressToken, Is.EqualTo(2));
            state.Revision = 12; state.ActiveWork = null; state.ResourceNodes[0].StateCode = Nature생존Codes.Stump;
            var end = projector.Project(state, C(evidence: E(12)));
            Assert.That(end.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Completed));
            Assert.That(state.Revision, Is.EqualTo(12)); Assert.That(state.ActiveWork, Is.Null);
            Assert.That(state.ResourceNodes[0].StateCode, Is.EqualTo(Nature생존Codes.Stump));
        }
        [Test] public void 세대Overflow는_기존수용상태보존()
        {
            projector = new 벌목TaskObservationProjector(long.MaxValue - 1);
            var first = projector.Project(S(), C());
            Assert.That(first.Snapshot.ObservationGeneration, Is.EqualTo(long.MaxValue));
            var rejected = projector.Project(S(11, command: "start:2"), C());
            Assert.That(rejected.Diagnostic, Is.EqualTo("GenerationOverflow"));
            Assert.That(projector.ReadState().Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(projector.ReadState().Revision, Is.EqualTo(10));
        }
        [TestCase("origin")] [TestCase("actor")] [TestCase("required")] [TestCase("seconds")]
        [TestCase("duplicateNode")] [TestCase("stump")] [TestCase("player")]
        public void 잘못된작업입력은_토큰없이거부(string wrong)
        {
            var state = S();
            if (wrong == "origin") state.ActiveWork.OriginCommandId = "";
            if (wrong == "actor") state.ActiveWork.ActorStableId = "other:actor";
            if (wrong == "required") state.ActiveWork.RequiredSeconds = 5;
            if (wrong == "seconds") state.ActiveWork.CompletedSeconds = 4;
            if (wrong == "duplicateNode") state.ResourceNodes[1].StableId = state.ResourceNodes[0].StableId;
            if (wrong == "stump") state.ResourceNodes[0].StateCode = Nature생존Codes.Stump;
            if (wrong == "player") state.PlayerStableId = "";
            var result = projector.Project(state, C());
            Assert.That(result.Accepted, Is.False); Assert.That(result.ShouldTick, Is.False);
            Assert.That(projector.ReadState().Observed, Is.False);
        }
        [Test] public void 다른작업은_벌목토큰을만들지않음()
        {
            var state = S(); state.ActiveWork.KindCode = Nature생존Codes.CabinBuild;
            var result = projector.Project(state, C());
            Assert.That(result.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Idle)); Assert.That(result.HasActiveTask, Is.False);
        }
        [TestCase("complete")] [TestCase("cancel")] [TestCase("load")]
        public void Hit진입뒤종료와복원은_관측된토큰만소비(string boundary)
        {
            var model = new 벌목AnimationModel(); Dispatch(model, projector.Project(S(), C()));
            Dispatch(model, projector.Project(S(11, 1), C()));
            for (int i = 0; i < 8; i++) Assert.That(model.TickPresentation(.05f), Is.True);
            Assert.That(model.ReadState().Segment, Is.EqualTo(벌목PlaybackSegment.Hit));
            Assert.That(model.TryConsumeContact(out _), Is.False);
            var next = boundary == "load" ? projector.Project(S(2, 2), C(2, reconstruct: true))
                : projector.Project(End(12, boundary == "complete"), C(held: false,
                    evidence: E(12, boundary == "complete" ? 벌목TerminalOutcome.Completed : 벌목TerminalOutcome.Cancelled)));
            Dispatch(model, next);
            var tokens = new System.Collections.Generic.List<long>();
            for (int i = 0; i < 100; i++)
            {
                if (next.ShouldTick) Assert.That(model.TickPresentation(.05f), Is.True);
                if (model.TryConsumeContact(out var cue)) tokens.Add(cue.ProgressToken);
            }
            // 완료 때 이미 시작한 1초 타격과 최종4만 존재; 미관측2/3 타격 생성 없음.
            if (boundary == "complete") Assert.That(tokens, Is.EqualTo(new long[] { 1, 4 }));
            else Assert.That(tokens, Is.Empty);
        }
        [Test] public void 음수세대와잘못된Context는_안전거부()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new 벌목TaskObservationProjector(-1));
            Assert.That(projector.Project(S(), C(-1)).Accepted, Is.False);
            Assert.That(projector.Project(null, C()).Accepted, Is.False);
            Assert.That(projector.Project(S(), null).Accepted, Is.False);
        }
    }
}
