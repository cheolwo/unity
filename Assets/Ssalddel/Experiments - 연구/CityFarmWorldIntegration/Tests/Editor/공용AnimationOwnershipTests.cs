using System;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "공용 절차형 자세와 외부 Graph의 명시 인계·거부·복귀를 격리 시험한다.",
        Boundary = "Scene/실제 벌목 접촉·Game View 완료를 증명하지 않는다.")]
    public sealed class 공용AnimationOwnershipTests
    {
        private Scene scene;
        private GameObject actor;
        private Animator animator;
        private 공용AnimationAdapter adapter;
        private 공용AnimationCatalogEntry entry;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var catalog = AssetDatabase.LoadAssetAtPath<공용AnimationCatalog>(공용AnimationPreviewBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            entry = catalog.Resolve(월드CompositionPackCodes.Farm);
            actor = (GameObject)PrefabUtility.InstantiatePrefab(entry.CharacterPrefab, scene);
            animator = actor.GetComponentInChildren<Animator>(true);
            adapter = actor.AddComponent<공용AnimationAdapter>();
            adapter.Configure(entry, animator);
            Assert.That(adapter.UsesFullBodyProceduralPose, Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            ReleaseOwnedGraph();
            if (adapter != null && adapter.HasExternalPoseOwner) adapter.ReleaseExternalPose("lease");
            if (actor != null) Object.DestroyImmediate(actor);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void 빈토큰은_상태변경없이거부한다(string token)
        {
            var enabled = animator.enabled;
            Assert.That(adapter.TryAcquireExternalPose(token, animator), Is.False);
            Assert.That(adapter.HasExternalPoseOwner, Is.False);
            Assert.That(animator.enabled, Is.EqualTo(enabled));
        }

        [Test]
        public void 다른대상은_소유할수없다()
        {
            Assert.That(adapter.TryAcquireExternalPose("lease", null), Is.False);
            Assert.That(adapter.HasExternalPoseOwner, Is.False);
        }

        [Test]
        public void 동일토큰은멱등이고_다른토큰은인계와해제를못한다()
        {
            Assert.That(adapter.TryAcquireExternalPose("lease", animator), Is.True);
            Assert.That(adapter.TryAcquireExternalPose("lease", animator), Is.True);
            Assert.That(adapter.TryAcquireExternalPose("other", animator), Is.False);
            Assert.That(adapter.ReleaseExternalPose("other"), Is.False);
            Assert.That(adapter.HasExternalPoseOwner, Is.True);
            Assert.That(adapter.ReleaseExternalPose("lease"), Is.True);
            Assert.That(adapter.ReleaseExternalPose("lease"), Is.False);
        }

        [Test]
        public void 소유중에는뼈와위상을쓰지않고_최신이동의도는보존한다()
        {
            var arm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var basePose = Field<Quaternion>("_leftArmBase");
            var originalEnabled = animator.enabled;
            Assert.That(adapter.TryAcquireExternalPose("lease", animator), Is.True);
            var pose = Quaternion.Euler(10, 20, 30);
            arm.localRotation = pose;
            var phase = adapter.PresentationPhase;
            var root = actor.transform.position;
            adapter.ApplyLocomotion(true, true);
            adapter.TickPresentation(.2f);
            Assert.That(Quaternion.Angle(arm.localRotation, pose), Is.LessThan(.001f));
            Assert.That(adapter.PresentationPhase, Is.EqualTo(phase));
            Assert.That(Field<Quaternion>("_leftArmBase"), Is.EqualTo(basePose));
            Assert.That(adapter.CurrentIntentCode, Is.EqualTo(공용AnimationIntentCodes.Run));
            Assert.That(adapter.ReleaseExternalPose("lease"), Is.True);
            Assert.That(animator.enabled, Is.EqualTo(originalEnabled));
            adapter.TickPresentation(.1f);
            Assert.That(adapter.PresentationPhase, Is.GreaterThan(phase));
            Assert.That(Field<Quaternion>("_leftArmBase"), Is.EqualTo(basePose));
            Assert.That(actor.transform.position, Is.EqualTo(root));
        }

        [Test]
        public void 소유중OnEnable과Configure는_기준자세를다시수집하지않는다()
        {
            var basePose = Field<Quaternion>("_leftArmBase");
            Assert.That(adapter.TryAcquireExternalPose("lease", animator), Is.True);
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation = Quaternion.Euler(40, 50, 60);
            // 생명주기 본문만 격리 호출한다. 실제 Play Mode 재진입 증거가 아니다.
            typeof(공용AnimationAdapter).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
            Assert.That(Field<Quaternion>("_leftArmBase"), Is.EqualTo(basePose));
            Assert.That(animator.enabled, Is.True);
            Assert.Throws<InvalidOperationException>(() => adapter.Configure(entry, animator));
            Assert.That(adapter.HasExternalPoseOwner, Is.True);
        }

        [Test]
        public void 이미Graph가있으면_새소유를거부한다()
        {
            animator.enabled = true;
            BindGraph();
            Assert.That(animator.hasBoundPlayables, Is.True);
            Assert.That(adapter.TryAcquireExternalPose("lease", animator), Is.False);
        }

        [Test]
        public void Graph를해제하기전에는_절차형쓰기를재개하지않는다()
        {
            Assert.That(adapter.TryAcquireExternalPose("lease", animator), Is.True);
            BindGraph();
            Assert.That(adapter.ReleaseExternalPose("lease"), Is.False);
            Assert.That(adapter.HasExternalPoseOwner, Is.True);
            ReleaseOwnedGraph();
            Assert.That(graph.IsValid(), Is.False);
            Assert.That(animator.hasBoundPlayables, Is.False);
            Assert.That(adapter.ReleaseExternalPose("lease"), Is.True);
        }

        private T Field<T>(string name) => (T)typeof(공용AnimationAdapter)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter);

        private void BindGraph()
        {
            graph = PlayableGraph.Create("D359_Ownership_Test");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var mixer = AnimationMixerPlayable.Create(graph, 0);
            output = AnimationPlayableOutput.Create(graph, "Owned", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        private void ReleaseOwnedGraph()
        {
            if (!graph.IsValid()) return;
            // Destroy의 지연 해제에 기대지 않고 이 시험이 소유한 출력만 먼저 분리한다.
            if (output.IsOutputValid()) output.SetTarget(null);
            graph.Destroy();
            output = default;
        }
    }
}
