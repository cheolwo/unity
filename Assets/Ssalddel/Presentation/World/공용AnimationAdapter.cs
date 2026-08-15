using System;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 공용AnimationAdapter : MonoBehaviour
    {
        [SerializeField] private string packCode = string.Empty;
        [SerializeField] private string actorRoleCode = string.Empty;
        [SerializeField] private string currentIntentCode = 공용AnimationIntentCodes.Idle;
        [SerializeField] private string sourceKindCode = string.Empty;
        [SerializeField] private string diagnosticCode = string.Empty;
        [SerializeField] private Animator animator = null!;
        [SerializeField] private bool rootMotionDisabled = true;
        [SerializeField, Range(0f, 1f)] private float locomotionWeight;
        [SerializeField] private string stableActorId = string.Empty;
        [SerializeField, Range(.95f, 1.05f)] private float playbackScale = 1f;

        private Transform? _leftUpperArm;
        private Transform? _rightUpperArm;
        private Transform? _leftUpperLeg;
        private Transform? _rightUpperLeg;
        private Transform? _leftLowerLeg;
        private Transform? _rightLowerLeg;
        private Transform? _spine;
        private Transform? _hips;
        private Quaternion _leftArmBase;
        private Quaternion _rightArmBase;
        private Quaternion _leftLegBase;
        private Quaternion _rightLegBase;
        private Quaternion _leftLowerLegBase;
        private Quaternion _rightLowerLegBase;
        private Quaternion _spineBase;
        private Vector3 _hipsBasePosition;
        private float _phase;
        private float _targetLocomotionWeight;
        private float _actionPoseWeight;
        private string _blendedActionIntentCode = 공용AnimationIntentCodes.Idle;
        private bool _bonesCached;

        public string PackCode => packCode;
        public string ActorRoleCode => actorRoleCode;
        public string CurrentIntentCode => currentIntentCode;
        public string SourceKindCode => sourceKindCode;
        public string DiagnosticCode => diagnosticCode;
        public Animator Animator => animator;
        public bool RootMotionDisabled => rootMotionDisabled;
        public float LocomotionWeight => locomotionWeight;
        public string StableActorId => stableActorId;
        public float PlaybackScale => playbackScale;
        public float PresentationPhase => _phase;
        public float ActionPoseWeight => _actionPoseWeight;
        public bool UsesFullBodyProceduralPose
            => sourceKindCode == 공용AnimationSourceKindCodes.ProceduralFallback
               && _leftUpperArm != null && _rightUpperArm != null
               && _leftUpperLeg != null && _rightUpperLeg != null
               && _leftLowerLeg != null && _rightLowerLeg != null
               && _spine != null && _hips != null;

        public void Configure(공용AnimationCatalogEntry entry, Animator targetAnimator)
        {
            if (entry == null || !entry.Validate() || targetAnimator == null)
                throw new ArgumentException("CommonAnimationAdapterConfigurationInvalid");
            packCode = entry.PackCode;
            actorRoleCode = entry.ActorRoleCode;
            sourceKindCode = entry.SourceKindCode;
            animator = targetAnimator;
            _bonesCached = false;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (entry.UsesFallback)
            {
                animator.runtimeAnimatorController = null;
                diagnosticCode = "animation.clip-unavailable:using-procedural-fallback";
            }
            else
            {
                diagnosticCode = string.Empty;
            }
            rootMotionDisabled = !animator.applyRootMotion;
            CacheBones();
            ApplyIntent(공용AnimationIntentCodes.Idle);
        }

        public void ConfigureDeterministicPhase(string actorStableId)
        {
            if (string.IsNullOrWhiteSpace(actorStableId))
                throw new ArgumentException("CommonAnimationStableActorMissing");
            stableActorId = actorStableId.Trim();
            _phase = 결정적표현Seed.PhaseRadians(stableActorId);
            playbackScale = 결정적표현Seed.PlaybackScale(stableActorId);
        }

        public void ApplyIntent(string intentCode)
        {
            if (!공용AnimationIntentCodes.IsKnown(intentCode))
                throw new ArgumentException("CommonAnimationIntentUnknown:" + intentCode);
            if (IsActionIntent(intentCode))
                _blendedActionIntentCode = intentCode;
            currentIntentCode = intentCode;
            _targetLocomotionWeight = intentCode switch
            {
                공용AnimationIntentCodes.Run => 1f,
                공용AnimationIntentCodes.Walk => .62f,
                _ => 0f,
            };
        }

        public void ApplyLocomotion(bool moving, bool running)
            => ApplyIntent(!moving
                ? 공용AnimationIntentCodes.Idle
                : running
                    ? 공용AnimationIntentCodes.Run
                    : 공용AnimationIntentCodes.Walk);

        public bool ValidateWiring()
            => 월드CompositionPackCodes.IsKnown(packCode)
               && !string.IsNullOrWhiteSpace(actorRoleCode)
               && 공용AnimationIntentCodes.IsKnown(currentIntentCode)
               && 공용AnimationSourceKindCodes.IsKnown(sourceKindCode)
               && animator != null
               && animator.avatar != null
               && animator.avatar.isHuman
               && !animator.applyRootMotion
               && rootMotionDisabled
               && (sourceKindCode != 공용AnimationSourceKindCodes.ProceduralFallback
                   || !string.IsNullOrWhiteSpace(diagnosticCode));

        public void TickPresentation(float deltaTime)
        {
            if (!ValidateWiring() || deltaTime < 0f)
                return;
            if (!UsesFullBodyProceduralPose)
                CacheBones();
            if (!UsesFullBodyProceduralPose)
                return;

            locomotionWeight = Mathf.MoveTowards(
                locomotionWeight,
                _targetLocomotionWeight,
                deltaTime * 5.5f);
            _actionPoseWeight = Mathf.MoveTowards(
                _actionPoseWeight,
                IsActionIntent(currentIntentCode) ? 1f : 0f,
                deltaTime * 6.5f);
            var cadence = Mathf.Lerp(1.3f, 9.6f, locomotionWeight);
            _phase += deltaTime * cadence * playbackScale;
            ApplyProceduralPose(deltaTime);
        }

        private void Update() => TickPresentation(Time.deltaTime);

        private void CacheBones()
        {
            if (_bonesCached || animator == null || !animator.isHuman)
                return;
            _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (_leftUpperArm != null) _leftArmBase = _leftUpperArm.localRotation;
            if (_rightUpperArm != null) _rightArmBase = _rightUpperArm.localRotation;
            if (_leftUpperLeg != null) _leftLegBase = _leftUpperLeg.localRotation;
            if (_rightUpperLeg != null) _rightLegBase = _rightUpperLeg.localRotation;
            if (_leftLowerLeg != null) _leftLowerLegBase = _leftLowerLeg.localRotation;
            if (_rightLowerLeg != null) _rightLowerLegBase = _rightLowerLeg.localRotation;
            if (_spine != null) _spineBase = _spine.localRotation;
            if (_hips != null) _hipsBasePosition = _hips.localPosition;
            _bonesCached = UsesFullBodyProceduralPose;
        }

        private void ApplyProceduralPose(float deltaTime)
        {
            var stride = Mathf.Sin(_phase);
            var oppositeStride = Mathf.Sin(_phase + Mathf.PI);
            var stepLiftLeft = Mathf.Max(0f, -stride);
            var stepLiftRight = Mathf.Max(0f, -oppositeStride);
            var walkAmount = locomotionWeight;
            var idleBreath = Mathf.Sin(_phase * .55f) * (1f - walkAmount);
            var armAngle = stride * Mathf.Lerp(2f, 31f, walkAmount);
            var legAngle = stride * Mathf.Lerp(0f, 30f, walkAmount);
            var kneeMaximum = Mathf.Lerp(0f, 24f, walkAmount);
            var armRestAngle = Mathf.Lerp(82f, 68f, walkAmount);

            var leftArmTarget = _leftArmBase * Quaternion.Euler(
                armAngle + idleBreath * 1.4f, 0f, armRestAngle);
            var rightArmTarget = _rightArmBase * Quaternion.Euler(
                -armAngle - idleBreath * 1.4f, 0f, armRestAngle);
            var leftLegTarget = _leftLegBase
                * Quaternion.Euler(-legAngle, 0f, 0f);
            var rightLegTarget = _rightLegBase
                * Quaternion.Euler(legAngle, 0f, 0f);
            var leftLowerLegTarget = _leftLowerLegBase
                * Quaternion.Euler(stepLiftLeft * kneeMaximum, 0f, 0f);
            var rightLowerLegTarget = _rightLowerLegBase
                * Quaternion.Euler(stepLiftRight * kneeMaximum, 0f, 0f);
            var spineTarget = _spineBase * Quaternion.Euler(
                Mathf.Lerp(idleBreath * .8f, -3.5f, walkAmount),
                stride * walkAmount * 2.4f,
                0f);
            var hipsTarget = _hipsBasePosition + Vector3.up * (
                Mathf.Abs(Mathf.Sin(_phase)) * walkAmount * .018f
                + idleBreath * .004f);

            if (_blendedActionIntentCode == 공용AnimationIntentCodes.Guard)
            {
                leftArmTarget = Quaternion.Slerp(leftArmTarget, _leftArmBase
                    * Quaternion.Euler(-38f, -10f, 54f),
                    _actionPoseWeight);
                rightArmTarget = Quaternion.Slerp(rightArmTarget, _rightArmBase
                    * Quaternion.Euler(-38f, 10f, 54f),
                    _actionPoseWeight);
                spineTarget = Quaternion.Slerp(spineTarget,
                    _spineBase * Quaternion.Euler(-6f, 0f, 0f),
                    _actionPoseWeight);
                hipsTarget = Vector3.Lerp(hipsTarget,
                    _hipsBasePosition + Vector3.down * .025f,
                    _actionPoseWeight);
            }
            else if (_blendedActionIntentCode == 공용AnimationIntentCodes.Attack)
            {
                var pulse = Mathf.Sin(_phase * 1.7f) * 8f;
                leftArmTarget = Quaternion.Slerp(leftArmTarget, _leftArmBase
                    * Quaternion.Euler(-54f + pulse, -8f, 48f),
                    _actionPoseWeight);
                rightArmTarget = Quaternion.Slerp(rightArmTarget, _rightArmBase
                    * Quaternion.Euler(-62f - pulse, 8f, 48f),
                    _actionPoseWeight);
                spineTarget = Quaternion.Slerp(spineTarget,
                    _spineBase * Quaternion.Euler(-11f, 7f, 0f),
                    _actionPoseWeight);
            }
            else if (_blendedActionIntentCode == 공용AnimationIntentCodes.Stagger)
            {
                var sway = Mathf.Sin(_phase * .7f) * 5f;
                leftArmTarget = Quaternion.Slerp(leftArmTarget, _leftArmBase
                    * Quaternion.Euler(20f, 0f, 82f), _actionPoseWeight);
                rightArmTarget = Quaternion.Slerp(rightArmTarget, _rightArmBase
                    * Quaternion.Euler(-8f, 0f, 86f), _actionPoseWeight);
                spineTarget = Quaternion.Slerp(spineTarget, _spineBase
                    * Quaternion.Euler(12f, 0f, 15f + sway),
                    _actionPoseWeight);
                hipsTarget = Vector3.Lerp(hipsTarget,
                    _hipsBasePosition + Vector3.down * .04f,
                    _actionPoseWeight);
            }

            var response = 1f - Mathf.Exp(-deltaTime * 12f);
            _leftUpperArm!.localRotation = Quaternion.Slerp(
                _leftUpperArm.localRotation, leftArmTarget, response);
            _rightUpperArm!.localRotation = Quaternion.Slerp(
                _rightUpperArm.localRotation, rightArmTarget, response);
            _leftUpperLeg!.localRotation = Quaternion.Slerp(
                _leftUpperLeg.localRotation, leftLegTarget, response);
            _rightUpperLeg!.localRotation = Quaternion.Slerp(
                _rightUpperLeg.localRotation, rightLegTarget, response);
            _leftLowerLeg!.localRotation = Quaternion.Slerp(
                _leftLowerLeg.localRotation, leftLowerLegTarget, response);
            _rightLowerLeg!.localRotation = Quaternion.Slerp(
                _rightLowerLeg.localRotation, rightLowerLegTarget, response);
            _spine!.localRotation = Quaternion.Slerp(
                _spine.localRotation, spineTarget, response);
            _hips!.localPosition = Vector3.Lerp(
                _hips.localPosition, hipsTarget, response);
        }

        private static bool IsActionIntent(string intentCode)
            => intentCode == 공용AnimationIntentCodes.Guard
                || intentCode == 공용AnimationIntentCodes.Attack
                || intentCode == 공용AnimationIntentCodes.Stagger;
    }

}
