using NUnit.Framework;
using Ssalddel.Unity.Presentation.NatureWoodcutting;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Editor.NatureWoodcuttingValidation.Tests
{
    // 미실행 Fixture 초안이다. 실제 원장/Presenter/Graph 증거가 아니다.
    public sealed class 벌목완료근거선택Tests
    {
        private static Nature생존State State(long revision = 10, bool active = true)
            => new Nature생존State
            {
                SessionStableId = "fixture:session", PlayerStableId = "fixture:actor", Revision = revision,
                ActiveWork = active ? new Nature생존WorkState
                {
                    KindCode = Nature생존Codes.Harvest, ActorStableId = "fixture:actor",
                    OriginCommandId = "start:1", TargetStableId = "fixture:tree",
                    RequiredSeconds = 4, CompletedSeconds = 3,
                } : null,
                ResourceNodes = new[] { new Nature생존ResourceNodeState
                    { StableId = "fixture:tree", StateCode = Nature생존Codes.Standing } },
            };

        private static Nature벌목CompletionEvidence Record(long revision = 11)
            => new Nature벌목CompletionEvidence
            {
                SessionStableId = "fixture:session", ActorStableId = "fixture:actor",
                OriginCommandId = "start:1", TargetStableId = "fixture:tree",
                EvidenceStableId = "fixture:evidence:1", RecordHashSha256 = new string('A', 64),
                ResultRevision = revision,
            };

        private static 벌목TaskObservationProjector Previous(bool active = true, long epoch = 1)
        {
            var projector = new 벌목TaskObservationProjector();
            Assert.That(projector.Project(State(active: active), new 벌목TaskObservationContext(epoch, true)).Accepted, Is.True);
            return projector;
        }

        private static Nature생존State Current(params Nature벌목CompletionEvidence[] records)
        {
            var state = State(12, false);
            state.HarvestCompletionRecords = records;
            return state;
        }

        private static void Reject(Nature생존State current, 벌목TaskObservationState previous,
            string expected = "Missing", long epoch = 1)
        {
            Assert.That(벌목완료근거선택.TrySelect(current, previous, epoch,
                out var evidence, out var hash, out var diagnostic), Is.False);
            Assert.That(evidence, Is.Null);
            Assert.That(hash, Is.EqualTo(string.Empty));
            Assert.That(diagnostic, Is.EqualTo(expected));
        }

        [TestCase(11)] [TestCase(12)]
        public void 현재보다낮거나같은완료Revision도_이전이후면선택(long resultRevision)
        {
            var record = Record(resultRevision);
            Assert.That(벌목완료근거선택.TrySelect(Current(record), Previous().ReadState(), 1,
                out var evidence, out var hash, out var diagnostic), Is.True);
            Assert.That(diagnostic, Is.EqualTo("Selected"));
            Assert.That(evidence.SessionStableId, Is.EqualTo(record.SessionStableId));
            Assert.That(evidence.ActorStableId, Is.EqualTo(record.ActorStableId));
            Assert.That(evidence.OriginCommandId, Is.EqualTo(record.OriginCommandId));
            Assert.That(evidence.TargetStableId, Is.EqualTo(record.TargetStableId));
            Assert.That(evidence.EvidenceStableId, Is.EqualTo(record.EvidenceStableId));
            Assert.That(evidence.ResultRevision, Is.EqualTo(resultRevision));
            Assert.That(evidence.ObservationEpoch, Is.EqualTo(1));
            Assert.That(evidence.Outcome, Is.EqualTo(벌목TerminalOutcome.Completed));
            Assert.That(hash, Is.EqualTo(record.RecordHashSha256));
        }

        [TestCase(9)] [TestCase(10)] [TestCase(13)]
        public void 관측구간밖결과는_미선택(long revision)
            => Reject(Current(Record(revision)), Previous().ReadState());

        [TestCase("session")] [TestCase("actor")] [TestCase("origin")] [TestCase("target")]
        [TestCase("id")] [TestCase("hash")] [TestCase("nullHash")] [TestCase("nullId")]
        public void 다른작업이나빈계보는_미선택(string wrong)
        {
            var record = Record();
            if (wrong == "session") record.SessionStableId = "other";
            if (wrong == "actor") record.ActorStableId = "other";
            if (wrong == "origin") record.OriginCommandId = "other";
            if (wrong == "target") record.TargetStableId = "other";
            if (wrong == "id") record.EvidenceStableId = " \t";
            if (wrong == "hash") record.RecordHashSha256 = " \t";
            if (wrong == "nullHash") record.RecordHashSha256 = null;
            if (wrong == "nullId") record.EvidenceStableId = null;
            Reject(Current(record), Previous().ReadState());
        }

        [TestCase("session")] [TestCase("player")] [TestCase("emptyPlayer")]
        [TestCase("sameRevision")] [TestCase("lowerRevision")]
        public void 조회소스변경이나Revision비증가는_미선택(string wrong)
        {
            var current = Current(Record());
            if (wrong == "session") current.SessionStableId = "other";
            if (wrong == "player") current.PlayerStableId = "other";
            if (wrong == "emptyPlayer") current.PlayerStableId = "";
            if (wrong == "sameRevision") current.Revision = 10;
            if (wrong == "lowerRevision") current.Revision = 2;
            Reject(current, Previous().ReadState());
        }

        [TestCase(-1)] [TestCase(0)] [TestCase(2)]
        public void 이전과다른Epoch는_Load의과거근거미선택(long epoch)
            => Reject(Current(Record()), Previous().ReadState(), epoch: epoch);

        [Test] public void 최초미관측과유휴복원은_기존완료재생안함()
        {
            Reject(Current(Record()), default);
            Reject(Current(Record()), Previous(active: false).ReadState());
            var projector = Previous();
            var restored = Current(Record()); restored.Revision = 2;
            restored.ResourceNodes[0].StateCode = Nature생존Codes.Stump;
            var result = projector.Project(restored, new 벌목TaskObservationContext(2, false, true));
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Reconstruct));
            Reject(Current(Record()), projector.ReadState(), epoch: 2);
        }

        [Test] public void 복원후실제로새완료가관측되면_새Epoch에서선택()
        {
            var projector = Previous(epoch: 2);
            Assert.That(벌목완료근거선택.TrySelect(Current(Record()), projector.ReadState(), 2,
                out var evidence, out _, out _), Is.True);
            Assert.That(evidence.ObservationEpoch, Is.EqualTo(2));
        }

        [Test] public void 같은기록동일내용중복은_하나로선택()
        {
            var record = Record();
            var current = Current(record, record, record.Clone());
            Assert.That(벌목완료근거선택.TrySelect(current, Previous().ReadState(), 1,
                out var evidence, out _, out _), Is.True);
            Assert.That(evidence.EvidenceStableId, Is.EqualTo(record.EvidenceStableId));
            Assert.That(current.HarvestCompletionRecords.Length, Is.EqualTo(3));
        }

        [TestCase("id", false)] [TestCase("id", true)]
        [TestCase("hash", false)] [TestCase("hash", true)]
        [TestCase("revision", false)] [TestCase("revision", true)]
        [TestCase("emptyHash", false)] [TestCase("emptyHash", true)]
        [TestCase("foreignOrigin", false)] [TestCase("foreignOrigin", true)]
        public void 서로다른후보나동일기록내용충돌은_순서와무관하게실패(string conflict, bool reverse)
        {
            var first = Record(); var second = first.Clone();
            if (conflict == "id") second.EvidenceStableId = "fixture:evidence:2";
            if (conflict == "hash") second.RecordHashSha256 = new string('B', 64);
            if (conflict == "revision") second.ResultRevision = 12;
            if (conflict == "emptyHash") second.RecordHashSha256 = "";
            if (conflict == "foreignOrigin") second.OriginCommandId = "start:other";
            Reject(reverse ? Current(second, first) : Current(first, second), Previous().ReadState(), "Conflict");
        }

        [Test] public void 무관기록과null항목은_정상후보를가리지않음()
        {
            var other = Record(); other.OriginCommandId = "other"; other.EvidenceStableId = "other:id";
            Assert.That(벌목완료근거선택.TrySelect(Current(null, other, Record()), Previous().ReadState(), 1,
                out _, out _, out _), Is.True);
        }

        [Test] public void null과빈기록은_빈출력으로실패()
        {
            var previous = Previous().ReadState();
            Reject(null, previous); Reject(Current(), previous);
            var current = Current(); current.HarvestCompletionRecords = null; Reject(current, previous);
            Reject(Current((Nature벌목CompletionEvidence)null), previous);
        }

        [Test] public void 입력과이전관측은불변이며_반환DTO는입력재사용과분리()
        {
            var projector = Previous(); var previous = projector.ReadState();
            var record = Record(); var current = Current(record); var array = current.HarvestCompletionRecords;
            var nodes = current.ResourceNodes;
            Assert.That(벌목완료근거선택.TrySelect(current, previous, 1,
                out var evidence, out var hash, out _), Is.True);
            Assert.That(current.HarvestCompletionRecords, Is.SameAs(array));
            Assert.That(array[0], Is.SameAs(record)); Assert.That(current.ResourceNodes, Is.SameAs(nodes));
            Assert.That(current.Revision, Is.EqualTo(12)); Assert.That(current.ActiveWork, Is.Null);
            Assert.That(nodes[0].StateCode, Is.EqualTo(Nature생존Codes.Standing));
            Assert.That(projector.ReadState().Snapshot, Is.SameAs(previous.Snapshot));
            Assert.That(projector.ReadState().Revision, Is.EqualTo(10));
            record.OriginCommandId = "mutated"; record.RecordHashSha256 = "mutated"; record.ResultRevision = 99;
            Assert.That(evidence.OriginCommandId, Is.EqualTo("start:1"));
            Assert.That(evidence.ResultRevision, Is.EqualTo(11)); Assert.That(hash, Is.EqualTo(new string('A', 64)));
        }

        [TestCase(false)] [TestCase(true)]
        public void 선택성공은Stump나작업종료판정이아님(bool active)
        {
            var projector = Previous(); var current = State(12, active);
            current.HarvestCompletionRecords = new[] { Record() };
            Assert.That(벌목완료근거선택.TrySelect(current, projector.ReadState(), 1,
                out var evidence, out _, out _), Is.True);
            var result = projector.Project(current, new 벌목TaskObservationContext(1, false, terminalEvidence: evidence));
            Assert.That(result.Snapshot.Phase, Is.Not.EqualTo(벌목WorkPhase.Completed));
            if (!active) Assert.That(result.Diagnostic, Is.EqualTo("TerminalStateEvidenceConflict"));
        }

        [Test] public void 완료Project이후재조회는_재선택안함()
        {
            var projector = Previous(); var current = Current(Record());
            current.ResourceNodes[0].StateCode = Nature생존Codes.Stump;
            Assert.That(벌목완료근거선택.TrySelect(current, projector.ReadState(), 1,
                out var evidence, out _, out _), Is.True);
            var result = projector.Project(current, new 벌목TaskObservationContext(1, false, terminalEvidence: evidence));
            Assert.That(result.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Completed));
            current.Revision = 13;
            Reject(current, projector.ReadState());
        }

        [Test] public void 근거없는종료뒤남은WorkingSnapshot은_늦은완료로재선택안함()
        {
            var projector = Previous(); var ended = Current();
            var result = projector.Project(ended, new 벌목TaskObservationContext(1, false));
            Assert.That(result.Dispatch, Is.EqualTo(벌목ProjectionDispatch.Interrupt));
            Assert.That(result.Snapshot.Phase, Is.EqualTo(벌목WorkPhase.Working));
            var late = Current(Record(13)); late.Revision = 14;
            Reject(late, projector.ReadState());
        }
    }
}
