using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.VisitorAnimation
{
    public enum 방문자동작Intent { Idle, Walk, Greet }

    public readonly struct 방문자AnimationState
    {
        public 방문자AnimationState(bool ready, 방문자동작Intent intent,
            string activeState, bool greetingAvailable, string diagnostic)
        {
            Ready = ready;
            Intent = intent;
            ActiveState = activeState;
            GreetingAvailable = greetingAvailable;
            Diagnostic = diagnostic;
        }

        public bool Ready { get; }
        public 방문자동작Intent Intent { get; }
        public string ActiveState { get; }
        public bool GreetingAvailable { get; }
        public string Diagnostic { get; }
        public bool PresentationOnly => true;
    }

    /// <summary>
    /// 방문자 전용 VisualRoot의 Animator만 소유한다. 위치·회전·방문자 결정·revision은
    /// 쓰지 않으며 이동 담당이 SetIntent를 호출한다. Greet 종료는 업무 완료가 아니다.
    /// </summary>
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "승인된 방문자 Actor의 Idle/Walk/선택적 Greet를 읽기 전용으로 표현한다.",
        Boundary = "이 Adapter의 재생 검증은 Session·배치 통합 E5 완료를 대신하지 않는다.")]
    public sealed class 방문자AnimationAdapter : MonoBehaviour
    {
        public const string AnimationRole = "VisitorArrival";
        public const string WaitingCue = "Visitor.Waiting.Greet";
        public const string StateCue = "Visitor.State.IdleOrDepart";
        public const float TransitionSeconds = .12f;

        [SerializeField] private Animator animator = null!;
        [SerializeField] private RuntimeAnimatorController controller = null!;
        private bool ready;
        private bool hasGreet;
        private 방문자동작Intent intent;
        private string diagnostic = "VisitorAnimationNotConfigured";

        public Animator Animator => animator;

        public bool Configure(Animator target, RuntimeAnimatorController wrapper)
        {
            // 잘못된 대상이나 타 작성자가 있는 Actor는 소유권을 빼앗지 않는다.
            if (ready && target != animator)
                throw new InvalidOperationException("VisitorAnimationCannotRebindActor");
            if (target == null || (target.transform != transform
                && !target.transform.IsChildOf(transform)))
                return Reject("VisitorAnimationTargetOutsideWrapper");
            if (target.GetComponentsInChildren<MonoBehaviour>(true)
                    .Any(value => value != null && value != this && value.enabled)
                || target.GetComponentsInParent<World.공용AnimationAdapter>(true)
                    .Any(value => value.enabled)
                || target.GetComponentsInParent<방문자AnimationAdapter>(true)
                    .Any(value => value != this && value.enabled))
                return Reject("VisitorAnimationBoneWriterConflict");

            animator = target;
            controller = wrapper;
            if (target.avatar == null || !target.avatar.isValid || !target.avatar.isHuman)
                return StaticFallback("VisitorAnimationAvatarInvalid");
            if (wrapper == null || wrapper.animationClips.Length == 0
                || wrapper.animationClips.Any(clip => clip == null || clip.empty
                    || !clip.isHumanMotion || clip.events.Length != 0))
                return StaticFallback("VisitorAnimationClipInvalid");

            target.applyRootMotion = false;
            target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            target.runtimeAnimatorController = wrapper;
            target.enabled = true;
            target.Rebind();
            if (!target.HasState(0, StateHash(방문자동작Intent.Idle))
                || !target.HasState(0, StateHash(방문자동작Intent.Walk)))
                return StaticFallback("VisitorAnimationRequiredStateMissing");
            hasGreet = target.HasState(0, StateHash(방문자동작Intent.Greet));
            ready = true;
            diagnostic = string.Empty;
            InterruptToIdle();
            target.Update(0f);
            return true;
        }

        public void SetIntent(방문자동작Intent value)
        {
            if (!Enum.IsDefined(typeof(방문자동작Intent), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (!ready || !isActiveAndEnabled) return;
            if (value == 방문자동작Intent.Greet && !hasGreet)
                value = 방문자동작Intent.Idle;
            if (intent == value) return; // 상태 사본 반복 조회는 동작을 처음부터 재시작하지 않는다.
            intent = value;
            animator.CrossFadeInFixedTime(StateHash(value), TransitionSeconds, 0, 0f);
        }

        public void InterruptToIdle()
        {
            intent = 방문자동작Intent.Idle;
            if (ready && animator != null && animator.isInitialized)
                animator.Play(StateHash(intent), 0, 0f);
        }

        /// <summary>Load/새 권위 상태 수신 시 과거 인사를 재생하지 않고 이동 여부만 복구한다.</summary>
        public void Reconstruct(bool moving)
        {
            InterruptToIdle();
            if (moving) SetIntent(방문자동작Intent.Walk);
        }

        public 방문자AnimationState ReadState()
        {
            var active = string.Empty;
            if (ready && animator != null && animator.isInitialized)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                foreach (방문자동작Intent value in Enum.GetValues(typeof(방문자동작Intent)))
                    if (state.fullPathHash == StateHash(value)) active = value.ToString();
            }
            return new 방문자AnimationState(ready, intent, active, hasGreet, diagnostic);
        }

        // Animator가 완료한 선택적 인사의 표현 상태만 읽는다. Command/Event는 발행하지 않는다.
        public void RefreshPlaybackState()
        {
            if (!ready || animator == null || !animator.isInitialized) return;
            if (intent == 방문자동작Intent.Greet && !animator.IsInTransition(0)
                && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == StateHash(방문자동작Intent.Idle))
                intent = 방문자동작Intent.Idle;
        }

        private void Update() => RefreshPlaybackState();
        private void OnDisable() => InterruptToIdle();
        private void OnEnable()
        {
            if (ready) InterruptToIdle();
        }

        private bool Reject(string code)
        {
            ready = false;
            diagnostic = code;
            return false;
        }

        private bool StaticFallback(string code)
        {
            // 실제 Actor는 유지한다. 별도의 절차형 뼈 작성자를 켜지 않는다.
            animator.applyRootMotion = false;
            animator.enabled = false;
            intent = 방문자동작Intent.Idle;
            hasGreet = false;
            return Reject(code);
        }

        private static int StateHash(방문자동작Intent value)
            => Animator.StringToHash("Base Layer." + value);
    }
}
