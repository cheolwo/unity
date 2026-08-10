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

        private Transform? _leftUpperArm;
        private Transform? _rightUpperArm;
        private Quaternion _leftBase;
        private Quaternion _rightBase;
        private float _phase;

        public string PackCode => packCode;
        public string ActorRoleCode => actorRoleCode;
        public string CurrentIntentCode => currentIntentCode;
        public string SourceKindCode => sourceKindCode;
        public string DiagnosticCode => diagnosticCode;
        public Animator Animator => animator;
        public bool RootMotionDisabled => rootMotionDisabled;

        public void Configure(공용AnimationCatalogEntry entry, Animator targetAnimator)
        {
            if (entry == null || !entry.Validate() || targetAnimator == null)
                throw new ArgumentException("CommonAnimationAdapterConfigurationInvalid");
            packCode = entry.PackCode;
            actorRoleCode = entry.ActorRoleCode;
            sourceKindCode = entry.SourceKindCode;
            animator = targetAnimator;
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

        public void ApplyIntent(string intentCode)
        {
            if (!공용AnimationIntentCodes.IsKnown(intentCode))
                throw new ArgumentException("CommonAnimationIntentUnknown:" + intentCode);
            currentIntentCode = intentCode;
        }

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
            if (_leftUpperArm == null || _rightUpperArm == null)
                CacheBones();
            if (_leftUpperArm == null || _rightUpperArm == null)
                return;

            _phase += deltaTime * 7f;
            var swing = currentIntentCode == 공용AnimationIntentCodes.Walk
                ? Mathf.Sin(_phase) * 24f
                : Mathf.Sin(_phase * .25f) * 2f;
            _leftUpperArm.localRotation = _leftBase * Quaternion.Euler(swing, 0f, 72f);
            _rightUpperArm.localRotation = _rightBase * Quaternion.Euler(-swing, 0f, 72f);
        }

        private void Update() => TickPresentation(Time.deltaTime);

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman)
                return;
            _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (_leftUpperArm != null) _leftBase = _leftUpperArm.localRotation;
            if (_rightUpperArm != null) _rightBase = _rightUpperArm.localRotation;
        }
    }

}
