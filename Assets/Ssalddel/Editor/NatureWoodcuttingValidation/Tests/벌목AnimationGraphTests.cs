using NUnit.Framework;
using Ssalddel.Unity.Presentation.NatureWoodcutting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor.NatureWoodcuttingValidation.Tests
{
    public sealed class 벌목AnimationGraphTests
    {
        private Scene ownedScene;
        private GameObject actor;
        private Animator animator;
        private 벌목AnimationAdapter adapter;
        private 벌목AnimationSources sources;
        [SetUp] public void SetUp()
        {
            ownedScene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(벌목AnimationSourceAudit.ActorPath);
            actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ownedScene);
            animator = actor.GetComponentInChildren<Animator>(true);
            animator.enabled = true; animator.applyRootMotion = false; animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            adapter = actor.AddComponent<벌목AnimationAdapter>();
            sources = AssetDatabase.LoadAssetAtPath<벌목AnimationSources>(벌목AnimationSourceAudit.SourcesPath);
        }
        [TearDown] public void TearDown()
        {
            try { if (adapter != null) adapter.Dispose(); if (actor != null) Object.DestroyImmediate(actor); }
            finally { if (ownedScene.IsValid()) EditorSceneManager.ClosePreviewScene(ownedScene); }
        }
        [Test] public void OriginalSources_MatchBytesAndGuidFileIds()
        { Assert.That(벌목AnimationSourceAudit.Validate(out var diagnostic), Is.True, diagnostic); }
        [Test] public void Configure_DoesNotAcquireOrChangeAnimator()
        {
            animator.enabled = false;
            Assert.That(adapter.Configure(animator, sources), Is.True);
            Assert.That(adapter.ReadState().GraphActive, Is.False);
            Assert.That(animator.enabled, Is.False);
            Assert.That(adapter.TickPresentation(.1f), Is.False);
            Assert.That(adapter.Acquire("lease"), Is.False);
        }
        [Test] public void OwnershipTokens_AreIdempotentAndForeignReleaseIsNoOp()
        {
            Assert.That(adapter.Configure(animator, sources), Is.True);
            Assert.That(adapter.Acquire("lease"), Is.True, adapter.ReadState().Diagnostic);
            adapter.ApplySnapshot(new 벌목AnimationSnapshot("s", "a", "t", 1, 1, 4, 0, 벌목WorkPhase.Working));
            Assert.That(adapter.TickPresentation(.1f), Is.True);
            float time = adapter.ReadState().Playback.NormalizedTime;
            var graphField = typeof(벌목AnimationAdapter).GetField("graph", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var originalGraph = (PlayableGraph)graphField.GetValue(adapter);
            Assert.That(adapter.Acquire("lease"), Is.True);
            Assert.That((PlayableGraph)graphField.GetValue(adapter), Is.EqualTo(originalGraph));
            Assert.That(adapter.ReadState().Playback.NormalizedTime, Is.EqualTo(time));
            Assert.That(adapter.Acquire("other"), Is.False);
            Assert.That(adapter.Release("other"), Is.False);
            Assert.That(adapter.ReadState().GraphActive, Is.True);
            Assert.That(adapter.Release("lease"), Is.True);
            Assert.That(adapter.ReadState().GraphActive, Is.False);
            Assert.That(animator.hasBoundPlayables, Is.False);
            Assert.That(originalGraph.IsValid(), Is.False);
            Assert.That(animator.enabled, Is.True); Assert.That(animator.runtimeAnimatorController, Is.Null);
        }
        [Test] public void FailedConfigure_LeavesSafeNoPlayback()
        {
            Assert.That(adapter.Configure(animator, sources), Is.True);
            Assert.That(adapter.Acquire("lease"), Is.True);
            Assert.That(adapter.Configure(animator, null), Is.False);
            Assert.That(adapter.ReadState().Configured, Is.False);
            Assert.That(adapter.ReadState().GraphActive, Is.False);
            Assert.That(adapter.Acquire("lease"), Is.False);
        }
        [Test] public void ActualFarmerAvatar_ChangesArmPoseWithoutRootMovement()
        {
            Assert.That(adapter.Configure(animator, sources), Is.True);
            Assert.That(adapter.Acquire("lease"), Is.True);
            var position = actor.transform.position; var rotation = actor.transform.rotation;
            var scale = actor.transform.localScale;
            var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.That(arm, Is.Not.Null);
            adapter.ApplySnapshot(new 벌목AnimationSnapshot("s", "a", "t", 1, 1, 4, 0, 벌목WorkPhase.Working));
            Assert.That(adapter.TickPresentation(.1f), Is.True);
            var first = arm.localRotation;
            Assert.That(adapter.TickPresentation(.1f), Is.True);
            Assert.That(Quaternion.Angle(first, arm.localRotation), Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(position, actor.transform.position), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(rotation, actor.transform.rotation), Is.LessThan(.00001f));
            Assert.That(actor.transform.localScale, Is.EqualTo(scale));
            Assert.That(벌목AnimationSourceAudit.Validate(out var diagnostic), Is.True, diagnostic);
        }
        [Test] public void EnvironmentChange_ReleasesOwnGraphWithoutResettingAnimator()
        {
            Assert.That(adapter.Configure(animator, sources), Is.True);
            Assert.That(adapter.Acquire("lease"), Is.True);
            animator.applyRootMotion = true;
            Assert.That(adapter.TickPresentation(.1f), Is.False);
            Assert.That(adapter.ReadState().GraphActive, Is.False);
            Assert.That(animator.applyRootMotion, Is.True);
        }
        [Test] public void DisableBodyAndDispose_DoNotDestroyOtherGraph()
        {
            var foreign = PlayableGraph.Create("D359TestOwnedForeignGraph");
            try
            {
                Assert.That(adapter.Configure(animator, sources), Is.True);
                Assert.That(adapter.Acquire("lease"), Is.True);
                adapter.enabled = false;
                // stopped EditMode의 일반 MonoBehaviour 생명주기 자동 실행을 가정하지 않는다.
                // 본문 격리 시험이며 실제 Play Mode Disable/재진입 증거가 아니다.
                typeof(벌목AnimationAdapter).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(adapter, null);
                Assert.That(adapter.ReadState().GraphActive, Is.False);
                Assert.That(foreign.IsValid(), Is.True);
                Assert.That(animator.enabled, Is.True);
                adapter.Dispose(); Assert.That(foreign.IsValid(), Is.True);
            }
            finally { if (foreign.IsValid()) foreign.Destroy(); }
        }
    }
}
