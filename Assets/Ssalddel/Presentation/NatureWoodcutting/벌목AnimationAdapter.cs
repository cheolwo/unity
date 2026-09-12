using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    public readonly struct 벌목AdapterState
    {
        internal 벌목AdapterState(bool configured, bool owned, bool graphActive, string diagnostic, 벌목PlaybackState playback)
        { Configured = configured; HasOwnership = owned; GraphActive = graphActive; Diagnostic = diagnostic; Playback = playback; }
        public bool Configured { get; }
        public bool HasOwnership { get; }
        public bool GraphActive { get; }
        public string Diagnostic { get; }
        public 벌목PlaybackState Playback { get; }
        public bool ReturnReady => Playback.ReturnReady;
        public bool PresentationOnly => true;
    }

    /// <summary>명시 인계 후 기존 Clip만 평가한다. 자동 Update·Transform·권위 쓰기·Animator 초기화 없음.</summary>
    [DisallowMultipleComponent]
    public sealed class 벌목AnimationAdapter : MonoBehaviour, IDisposable
    {
        private readonly 벌목AnimationModel model = new 벌목AnimationModel();
        private Animator target;
        private 벌목AnimationSources sources;
        private string ownershipToken;
        private string diagnostic = "NotConfigured";
        private bool configured;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[5];
        private int currentIndex = 4;
        private int previousIndex = 4;
        private float blendElapsed;
        private double previousTime;
        private const float BlendSeconds = .08f;
        public Animator Animator => target;

        public bool Configure(Animator animator, 벌목AnimationSources sourceSet)
        {
            ReleaseOwnedGraph();
            configured = false; target = null; sources = null; model.Reset();
            if (animator == null || sourceSet == null
                || (animator.transform != transform && !animator.transform.IsChildOf(transform)))
                return Reject("TargetOutsideOwnerOrSourcesMissing");
            if (!sourceSet.ValidateRuntime(animator, out diagnostic)) return false;
            target = animator; sources = sourceSet; configured = true;
            diagnostic = string.Empty;
            return true;
        }

        public bool Acquire(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return Reject("OwnershipTokenRequired");
            if (ownershipToken != null)
            {
                if (ownershipToken != token) return Reject("AlreadyOwned");
                if (!CanEvaluate()) { ReleaseOwnedGraph(); model.InterruptToIdle(); return Reject("OwnershipEnvironmentChanged"); }
                diagnostic = string.Empty;
                return true;
            }
            if (!configured || !isActiveAndEnabled || target == null || sources == null)
                return Reject("NotReadyForOwnership");
            // 호출자의 공용 lease가 Animator 준비/기존 writer 중지를 소유한다. 여기서는 바꾸지 않는다.
            if (!sources.ValidateRuntime(target, out diagnostic)) return false;
            if (!target.isActiveAndEnabled || target.applyRootMotion || target.runtimeAnimatorController != null)
                return Reject("AnimatorLeasePreconditionFailed");
            try
            {
                graph = PlayableGraph.Create("NatureWoodcuttingOwnedGraph");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph, 5);
                for (int i = 0; i < 5; i++)
                {
                    playables[i] = AnimationClipPlayable.Create(graph, sources.GetClip(i));
                    playables[i].SetApplyFootIK(false);
                    playables[i].SetApplyPlayableIK(false);
                    playables[i].SetSpeed(0);
                    graph.Connect(playables[i], 0, mixer, i);
                    mixer.SetInputWeight(i, i == 4 ? 1 : 0);
                }
                output = AnimationPlayableOutput.Create(graph, "Woodcutting", target);
                output.SetSourcePlayable(mixer);
                graph.Play();
                ownershipToken = token; currentIndex = previousIndex = 4; blendElapsed = BlendSeconds;
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ReleaseOwnedGraph();
                return Reject("GraphCreationFailed:" + exception.GetType().Name);
            }
        }

        public bool Release(string token)
        {
            if (ownershipToken == null || ownershipToken != token) return Reject("OwnershipTokenMismatch");
            ReleaseOwnedGraph(); model.InterruptToIdle(); diagnostic = string.Empty;
            return true;
        }
        public bool ApplySnapshot(벌목AnimationSnapshot snapshot) => model.ApplySnapshot(snapshot);
        public bool Reconstruct(벌목AnimationSnapshot snapshot) => model.Reconstruct(snapshot);
        public void InterruptToIdle() => model.InterruptToIdle();
        public bool TryConsumeContact(out 벌목ContactCue cue) => model.TryConsumeContact(out cue);

        public bool TickPresentation(float deltaTime)
        {
            if (!CanEvaluate())
            {
                if (ownershipToken != null) { ReleaseOwnedGraph(); model.InterruptToIdle(); }
                return Reject("OwnershipRequiredOrChanged");
            }
            if (!model.TickPresentation(deltaTime)) return false;
            try
            {
            var state = model.ReadState();
            int index = state.Segment switch
            {
                벌목PlaybackSegment.WindUp => 0, 벌목PlaybackSegment.AwaitingProgress => 0,
                벌목PlaybackSegment.Hit => 1, 벌목PlaybackSegment.FollowThrough => 2,
                벌목PlaybackSegment.ReturnToIdle => 3, _ => 4,
            };
            if (index != currentIndex)
            {
                previousIndex = currentIndex; previousTime = playables[currentIndex].GetTime();
                currentIndex = index; blendElapsed = 0;
            }
            blendElapsed = Mathf.Min(BlendSeconds, blendElapsed + Mathf.Min(deltaTime, .25f));
            float weight = blendElapsed / BlendSeconds;
            for (int i = 0; i < 5; i++) mixer.SetInputWeight(i, 0);
            mixer.SetInputWeight(previousIndex, 1 - weight);
            mixer.SetInputWeight(currentIndex, previousIndex == currentIndex ? 1 : weight);
            if (previousIndex != currentIndex) playables[previousIndex].SetTime(previousTime);
            playables[currentIndex].SetTime(state.NormalizedTime * sources.GetClip(currentIndex).length);
            graph.Evaluate(0);
            diagnostic = string.Empty;
            return true;
            }
            catch (Exception exception)
            {
                ReleaseOwnedGraph(); model.InterruptToIdle();
                return Reject("GraphEvaluationFailed:" + exception.GetType().Name);
            }
        }

        public 벌목AdapterState ReadState() => new 벌목AdapterState(configured, ownershipToken != null,
            graph.IsValid(), diagnostic, model.ReadState());
        private bool CanEvaluate() => configured && ownershipToken != null && graph.IsValid()
            && isActiveAndEnabled && target != null && target.isActiveAndEnabled
            && sources != null && target.avatar == sources.TargetAvatar && !target.applyRootMotion
            && target.runtimeAnimatorController == null;
        private void ReleaseOwnedGraph()
        {
            // Destroy만으로 Animator의 결속이 즉시 풀리지 않는 Editor 경계를 피한다.
            // 소유한 출력 하나만 분리하며 Animator/다른 출력은 초기화하지 않는다.
            if (output.IsOutputValid()) output.SetTarget(null);
            if (graph.IsValid()) graph.Destroy();
            graph = default; output = default; ownershipToken = null;
        }
        private bool Reject(string code) { diagnostic = code; return false; }
        public void Dispose() { ReleaseOwnedGraph(); model.Reset(); configured = false; target = null; sources = null; }
        private void OnDisable() { ReleaseOwnedGraph(); model.InterruptToIdle(); }
        private void OnDestroy() => Dispose();
    }
}
