using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.NatureWoodcutting;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    // 실제 imported Farm Rig + 공용 lease + 전문 Graph + Presenter의 격리 연결 시험.
    // Fixture 상태를 사용하며 실제 입력/소리 청취/나무 접촉/Game View 증거가 아니다.
    public sealed class Nature벌목표현연결Tests
    {
        private Scene scene;
        private GameObject actor;
        private Animator animator;
        private 공용AnimationAdapter common;
        private 벌목AnimationAdapter graph;
        private Nature감각표현Presenter presenter;
        private PlayableGraph foreign;
        private AnimationPlayableOutput foreignOutput;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var catalog = AssetDatabase.LoadAssetAtPath<공용AnimationCatalog>(공용AnimationPreviewBuilder.CatalogPath);
            var entry = catalog.Resolve(월드CompositionPackCodes.Farm);
            actor = (GameObject)PrefabUtility.InstantiatePrefab(entry.CharacterPrefab, scene);
            animator = actor.GetComponentInChildren<Animator>(true);
            common = actor.AddComponent<공용AnimationAdapter>();
            common.Configure(entry, animator);
            graph = actor.AddComponent<벌목AnimationAdapter>();
            var sources = AssetDatabase.LoadAssetAtPath<벌목AnimationSources>(
                "Assets/Ssalddel/Presentation/NatureWoodcutting/Resources/NatureWoodcuttingSources.asset");
            Assert.That(graph.Configure(animator, sources), Is.True);
            presenter = actor.AddComponent<Nature감각표현Presenter>();
            Set("woodcuttingCommon", common); Set("woodcuttingAdapter", graph);
            Set("woodcuttingAnimator", animator); Set("woodcuttingLeaseToken", "fixture:lease");
        }
        [TearDown] public void TearDown()
        {
            try
            {
                if (foreignOutput.IsOutputValid()) foreignOutput.SetTarget(null);
                if (foreign.IsValid()) foreign.Destroy();
                if (graph != null) graph.Dispose();
                if (common != null && common.HasExternalPoseOwner) common.ReleaseExternalPose("fixture:lease");
                if (actor != null) Object.DestroyImmediate(actor);
            }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        }
        private void Set(string key, object value) => typeof(Nature감각표현Presenter).GetField(key, Private).SetValue(presenter, value);
        private T Get<T>(string key) => (T)typeof(Nature감각표현Presenter).GetField(key, Private).GetValue(presenter);
        private object Call(string key, params object[] args) => typeof(Nature감각표현Presenter).GetMethod(key, Private).Invoke(presenter, args);
        private bool Refresh(Nature생존State state, bool held = true, bool moving = false, long epoch = 1)
            => (bool)Call("RefreshWoodcutting", state, held, moving, epoch);
        private void Tick(int count) { for (int i = 0; i < count; i++) Call("TickWoodcutting", .05f); }
        private int Contacts => Get<HashSet<string>>("consumedWoodcuttingCues").Count;
        private int Completions => Get<HashSet<string>>("consumedWoodcuttingCompletions").Count;
        private static Nature생존State Working(long revision = 10, int seconds = 0, string origin = "origin:1") => new()
        {
            SessionStableId = "s", PlayerStableId = "p", Revision = revision,
            ActiveWork = new Nature생존WorkState { KindCode = Nature생존Codes.Harvest, ActorStableId = "p",
                OriginCommandId = origin, TargetStableId = "t", RequiredSeconds = 4, CompletedSeconds = seconds },
            ResourceNodes = new[] { new Nature생존ResourceNodeState { StableId = "t", StateCode = Nature생존Codes.Standing } }
        };
        private static Nature생존State Completed(long revision = 12, long result = 11, string origin = "origin:1")
        {
            var value = Working(revision); value.ActiveWork = null;
            value.ResourceNodes[0].StateCode = Nature생존Codes.Stump;
            value.HarvestCompletionRecords = new[] { new Nature벌목CompletionEvidence {
                SessionStableId = "s", ActorStableId = "p", OriginCommandId = origin, TargetStableId = "t",
                EvidenceStableId = "record:" + origin, RecordHashSha256 = new string('a', 64), ResultRevision = result } };
            return value;
        }
        [Test] public void 완료기록이최신개정보다낮아도_최종접촉뒤한번낙하한다()
        {
            Assert.That(Refresh(Working()), Is.True);
            Assert.That(Refresh(Completed(), held: false), Is.True);
            Assert.That(Completions, Is.Zero); Assert.That(Contacts, Is.Zero);
            Tick(80);
            Assert.That(Contacts, Is.EqualTo(1)); Assert.That(Completions, Is.EqualTo(1));
            Assert.That(common.HasExternalPoseOwner, Is.False);
            Assert.That(graph.ReadState().GraphActive, Is.False);
            Assert.That(Refresh(Completed(13), held: false), Is.True); Tick(80);
            Assert.That(Contacts, Is.EqualTo(1)); Assert.That(Completions, Is.EqualTo(1));
        }
        [Test] public void 중간Hit뒤완료는_중간접촉과최종접촉을구분한다()
        {
            Refresh(Working()); Refresh(Working(11, 1)); Tick(8);
            Assert.That(graph.ReadState().Playback.Segment, Is.EqualTo(벌목PlaybackSegment.Hit));
            Refresh(Completed(13, 12), held: false); Tick(2);
            Assert.That(Contacts, Is.EqualTo(1)); Assert.That(Completions, Is.Zero);
            Tick(80); Assert.That(Contacts, Is.EqualTo(2)); Assert.That(Completions, Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void 최초또는복원완료기록은_과거완료를재생하지않는다(bool restore)
        {
            if (restore) { Refresh(Working()); Refresh(Working(11, 1)); Tick(8); }
            Refresh(Completed(), epoch: restore ? 2 : 1); Tick(80);
            Assert.That(Contacts, Is.Zero); Assert.That(Completions, Is.Zero);
            Assert.That(common.HasExternalPoseOwner, Is.False);
        }
        [Test] public void 근거없는종료와늦은기록은_완료효과가없다()
        {
            Refresh(Working()); var end = Completed(); end.HarvestCompletionRecords = Array.Empty<Nature벌목CompletionEvidence>();
            Refresh(end, held: false); Tick(80); Refresh(Completed(13), held: false); Tick(80);
            Assert.That(Contacts, Is.Zero); Assert.That(Completions, Is.Zero);
        }
        [Test] public void 같은개정의입력해제와일시정지는_진행을멈춘다()
        {
            var value = Working(); Refresh(value); Tick(2);
            var first = graph.ReadState().Playback.NormalizedTime;
            Refresh(value, held: false); Tick(10);
            Assert.That(graph.ReadState().Playback.NormalizedTime, Is.EqualTo(first));
            value.ClockPaused = true; Refresh(value); Tick(10);
            Assert.That(graph.ReadState().Playback.NormalizedTime, Is.EqualTo(first));
            value.ClockPaused = false; Refresh(value); Tick(2);
            Assert.That(graph.ReadState().Playback.NormalizedTime, Is.GreaterThan(first));
        }
        [Test] public void 이동중단은_권위작업을보존하고복귀후기준선으로재개한다()
        {
            var value = Working(11, 1); Refresh(Working()); Refresh(value); Tick(8);
            Refresh(value, held: false, moving: true); Tick(10);
            Assert.That(common.HasExternalPoseOwner, Is.False); Assert.That(value.ActiveWork, Is.Not.Null);
            Refresh(value); Tick(80); Assert.That(Contacts, Is.Zero);
        }
        [Test] public void 공용획득실패후같은개정복구는_과거토큰을재생하지않는다()
        {
            common.enabled = false; Assert.That(Refresh(Working(11, 1)), Is.False);
            common.enabled = true; Assert.That(Refresh(Working(11, 1)), Is.True); Tick(80);
            Assert.That(Contacts, Is.Zero); Assert.That(Completions, Is.Zero);
        }
        [Test] public void 전문획득실패는_공용소유를반환하고기준선재개한다()
        {
            graph.enabled = false; Assert.That(Refresh(Working()), Is.False);
            Assert.That(common.HasExternalPoseOwner, Is.False);
            graph.enabled = true; Assert.That(Refresh(Working()), Is.True); Tick(80);
            Assert.That(Contacts, Is.Zero);
        }
        [Test] public void Tick실패뒤같은개정복구는_과거접촉이없다()
        {
            Refresh(Working()); Refresh(Working(11, 1)); Tick(8);
            animator.applyRootMotion = true; Tick(2); animator.applyRootMotion = false;
            Assert.That(common.HasExternalPoseOwner, Is.False);
            Refresh(Working(11, 1)); Tick(80); Assert.That(Contacts, Is.Zero);
        }
        [TestCase(-1)] [TestCase(0)] public void 낮은외부세대는_관측과대기접촉을바꾸지않는다(long epoch)
        {
            Refresh(Working()); Refresh(Working(11, 1)); Tick(8);
            var before = Get<벌목TaskObservationProjector>("woodcuttingProjector").ReadState();
            Assert.That(Refresh(Working(2), epoch: epoch), Is.False);
            var after = Get<벌목TaskObservationProjector>("woodcuttingProjector").ReadState();
            Assert.That(after.Snapshot, Is.SameAs(before.Snapshot));
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(presenter.WoodcuttingHasOwnership, Is.True);
            Assert.That(presenter.WoodcuttingDiagnostic, Is.EqualTo("SourceEpochRegressed"));
            Tick(10); Assert.That(Contacts, Is.Zero);
            Refresh(Working(11, 1)); Tick(20); Assert.That(Contacts, Is.EqualTo(1));
        }
        [Test] public void 평가예외와미해제출력은_소유를보존하고복구후과거접촉을버린다()
        {
            Refresh(Working()); Refresh(Working(11, 1)); Tick(8);
            Set("woodcuttingAdapter", null); // 소비 경계 예외 주입, 실제 출력은 여전히 살아 있다.
            Tick(1);
            Assert.That(presenter.WoodcuttingHasOwnership, Is.True);
            Assert.That(common.HasExternalPoseOwner, Is.True);
            Assert.That(graph.ReadState().GraphActive, Is.True);
            Set("woodcuttingAdapter", graph);
            Assert.That(Refresh(Working(11, 1)), Is.True);
            Tick(80); Assert.That(Contacts, Is.Zero);
        }
        [Test] public void 기존LocalFixture는_전문재구성대기가반복baseline을만들지않는다()
        {
            var previous = Working(); previous.PlayerStableId = string.Empty;
            var current = Working(11); current.PlayerStableId = string.Empty;
            var method = typeof(Nature감각표현Presenter).GetMethod("IsSensoryBaseline", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((bool)method.Invoke(null, new object[] { previous, current, true, -1L, 1L }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { null, current, true, -1L, 1L }), Is.True);
        }
        [Test] public void Presenter종료본문은_다른token이재소유한전문Graph를파괴하지않는다()
        {
            Refresh(Working()); Call("InvalidateWoodcuttingObservation");
            Assert.That(common.TryAcquireExternalPose("other", animator), Is.True);
            Assert.That(graph.Acquire("other"), Is.True);
            // EditMode의 일반 MonoBehaviour는 DestroyImmediate만으로 종료 콜백을 보장하지 않는다.
            // 종료 본문을 격리 호출하며 실제 Play 생명주기 증거로 사용하지 않는다.
            Call("OnDisable"); Call("OnDestroy");
            Object.DestroyImmediate(presenter);
            Assert.That(graph != null, Is.True);
            Assert.That(graph.ReadState().GraphActive, Is.True);
            Assert.That(common.IsExternalPoseOwnedBy("other"), Is.True);
            Assert.That(graph.Release("other"), Is.True);
            Assert.That(common.ReleaseExternalPose("other"), Is.True);
        }
        [Test] public void Presenter종료본문후반환대기는_외부출력해제뒤공용소유자에서마감한다()
        {
            Refresh(Working());
            foreign = PlayableGraph.Create("D375TestOwnedForeignGraph");
            foreignOutput = AnimationPlayableOutput.Create(foreign, "fixture", animator);
            Call("OnDisable"); Call("OnDestroy");
            Assert.That(typeof(공용AnimationAdapter).GetField("_pendingExternalPoseReleaseToken", Private).GetValue(common), Is.EqualTo("fixture:lease"));
            Object.DestroyImmediate(presenter);
            Assert.That(common.HasExternalPoseOwner, Is.True);
            Assert.That(foreign.IsValid(), Is.True);
            Assert.That(common.RequestExternalPoseRelease("wrong"), Is.False);
            common.TickPresentation(.05f); Assert.That(common.HasExternalPoseOwner, Is.True);
            foreignOutput.SetTarget(null); foreign.Destroy();
            common.TickPresentation(.05f); Assert.That(common.HasExternalPoseOwner, Is.False);
        }
        [Test] public void Animator교체는_이전Adapter의외부소유가끝나기전폐기하지않는다()
        {
            Refresh(Working()); Call("InvalidateWoodcuttingObservation");
            Assert.That(common.TryAcquireExternalPose("other", animator), Is.True);
            Assert.That(graph.Acquire("other"), Is.True);
            var next = new GameObject("fixture:next-animator"); next.transform.SetParent(actor.transform, false);
            var nextAnimator = next.AddComponent<Animator>();
            Assert.That((bool)Call("RetirePreviousWoodcuttingAdapter", nextAnimator), Is.False);
            Assert.That(graph != null && graph.ReadState().GraphActive, Is.True);
            Assert.That(presenter.WoodcuttingDiagnostic, Is.EqualTo("PreviousAdapterExternallyOwned"));
            Assert.That(graph.Release("other"), Is.True);
            Assert.That(common.ReleaseExternalPose("other"), Is.True);
            Assert.That((bool)Call("RetirePreviousWoodcuttingAdapter", nextAnimator), Is.True);
            Assert.That(graph == null, Is.True);
        }
        [Test] public void 복원기준점은_새나무좌표와H1을따르고기존효과목표를재결속한다()
        {
            var tree = new GameObject("fixture:tree"); tree.transform.SetParent(actor.transform, false);
            tree.transform.position = new Vector3(12, 0, 23);
            var marker = tree.AddComponent<Nature생존상호작용표식>();
            marker.StableId = "t"; marker.Kind = Nature생존상호작용Kind.HarvestTree;
            Get<Dictionary<string, Nature생존상호작용표식>>("markers")["t"] = marker;
            var anchorObject = new GameObject("fixture:fx"); anchorObject.transform.SetParent(actor.transform, false);
            var anchor = anchorObject.AddComponent<감각표현AnchorView>();
            anchor.Configure("fixture:fx", "t", "h1:old", Ssalddel.Unity.PresentationContracts.배치표현AnchorRoleCodes.Fx, Vector3.zero);
            Set("anchors", new[] { anchor });
            var state = Working(); state.ResourceNodes[0].H1StableId = "h1:new";
            Call("RebindWoodcuttingAnchors", state);
            Assert.That(Vector3.Distance(anchor.transform.position, tree.transform.position + Vector3.up * .8f), Is.LessThan(.02f));
            Assert.That(anchor.OwningH1StableId, Is.EqualTo("h1:new"));
            Assert.DoesNotThrow(() => anchor.Validate());
        }
        [Test] public void 다른출력때문에반환거부되면_소유진단을유지한다()
        {
            Refresh(Working());
            foreign = PlayableGraph.Create("D375TestOwnedForeignGraph");
            foreignOutput = AnimationPlayableOutput.Create(foreign, "fixture", animator);
            Call("InvalidateWoodcuttingObservation");
            Assert.That(presenter.WoodcuttingHasOwnership, Is.True);
            Assert.That(common.HasExternalPoseOwner, Is.True); Assert.That(foreign.IsValid(), Is.True);
            Assert.That(graph.ReadState().GraphActive, Is.False);
            foreignOutput.SetTarget(null); foreign.Destroy();
            Assert.That(Refresh(Working()), Is.True);
            Assert.That(presenter.WoodcuttingObservationEpoch, Is.EqualTo(2));
            Assert.That(Contacts, Is.Zero);
        }
    }
}
