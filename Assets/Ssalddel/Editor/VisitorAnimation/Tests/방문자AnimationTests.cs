using System;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.VisitorAnimation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor.VisitorAnimation.Tests
{
    public sealed class 방문자AnimationTests
    {
        private Scene scene;
        private GameObject root = null!;
        private 방문자AnimationAdapter adapter = null!;
        private Animator animator = null!;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            root = 방문자AnimationBuilder.CreateWrapper(scene);
            adapter = root.GetComponent<방문자AnimationAdapter>();
            animator = adapter.Animator;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        [Test]
        public void 실제StarterAvatar와세Clip이유효하고RootMotion과Event는없다()
        {
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(adapter.ReadState().Ready, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(root.GetComponentsInChildren<Animator>(), Has.Length.EqualTo(1));
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                Assert.That(clip.isHumanMotion && !clip.empty, Is.True, clip.name);
                Assert.That(clip.events, Is.Empty);
            }
        }

        [Test]
        public void 실제Walk는뼈를움직이지만위치회전은작성하지않는다()
        {
            var position = root.transform.position;
            var rotation = root.transform.rotation;
            adapter.SetIntent(방문자동작Intent.Walk);
            Advance(.2f);
            var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var before = leg.localRotation;
            Advance(.35f);
            Assert.That(Quaternion.Angle(before, leg.localRotation), Is.GreaterThan(5f));
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Walk"));
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void 이동중단은즉시Idle로돌아오며반복Intent는클립을재시작하지않는다()
        {
            adapter.SetIntent(방문자동작Intent.Walk);
            Advance(.4f);
            var before = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            adapter.SetIntent(방문자동작Intent.Walk);
            Advance(.1f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(before));
            adapter.InterruptToIdle();
            Advance(.02f);
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Idle"));
        }

        [Test]
        public void 인사는실제재생뒤Idle로끝나며Walk가즉시중단할수있다()
        {
            adapter.SetIntent(방문자동작Intent.Greet);
            Advance(.3f);
            var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var before = arm.localRotation;
            Advance(.4f);
            Assert.That(Quaternion.Angle(before, arm.localRotation), Is.GreaterThan(5f));
            adapter.SetIntent(방문자동작Intent.Walk);
            Advance(.2f);
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Walk"));
            adapter.SetIntent(방문자동작Intent.Greet);
            Advance(3f);
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Idle"));
            Assert.That(adapter.ReadState().Intent, Is.EqualTo(방문자동작Intent.Idle));
        }

        [Test]
        public void 비활성콜백로직및Load재구성은과거인사를재생하지않는다()
        {
            adapter.SetIntent(방문자동작Intent.Greet);
            Advance(.4f);
            adapter.enabled = false;
            // PreviewScene 편집 모드는 MonoBehaviour 수명 콜백을 자동 실행하지 않는다.
            // 콜백 본문만 검증한다. 실제 Play Mode 호출 순서는 통합 담당 후속 시험이다.
            InvokeLifecycleBody("OnDisable");
            adapter.enabled = true;
            InvokeLifecycleBody("OnEnable");
            Advance(.02f);
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Idle"));
            adapter.Reconstruct(true);
            Advance(.2f);
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Walk"));
            adapter.Reconstruct(false);
            Advance(.02f);
            Assert.That(adapter.ReadState().ActiveState, Is.EqualTo("Idle"));
        }

        [Test]
        public void Clip실패는정적실물Actor를남기고권위기능을호출하지않는다()
        {
            Assert.That(adapter.Configure(animator, null!), Is.False);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(animator.enabled, Is.False);
            Assert.That(adapter.ReadState().Diagnostic, Is.EqualTo("VisitorAnimationClipInvalid"));
            Assert.That(adapter.ReadState().PresentationOnly, Is.True);
        }

        [Test]
        public void 다른뼈작성자가있으면그소유권을빼앗지않는다()
        {
            var other = root.AddComponent<Ssalddel.Unity.Presentation.World.공용AnimationAdapter>();
            var controller = animator.runtimeAnimatorController;
            Assert.That(adapter.Configure(animator, controller), Is.False);
            Assert.That(adapter.ReadState().Diagnostic, Is.EqualTo("VisitorAnimationBoneWriterConflict"));
            Assert.That(other.enabled, Is.True);
        }

        [Test]
        public void 알수없는Intent를거부한다()
            => Assert.Throws<ArgumentOutOfRangeException>(() => adapter.SetIntent((방문자동작Intent)99));

        private void InvokeLifecycleBody(string method)
            => typeof(방문자AnimationAdapter).GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(adapter, null);

        private void Advance(float seconds)
        {
            var frames = Mathf.CeilToInt(seconds * 60);
            for (var i = 0; i < frames; i++)
            {
                animator.Update(1f / 60);
                adapter.RefreshPlaybackState();
            }
        }
    }
}
