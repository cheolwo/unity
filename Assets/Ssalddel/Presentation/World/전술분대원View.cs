using System;
using Ssalddel.Unity.Survival;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 전술분대원View : MonoBehaviour
    {
        [SerializeField] private string stableMemberId = string.Empty;
        [SerializeField] private int slotIndex;
        [SerializeField] private 공용AnimationAdapter animationAdapter = null!;
        [SerializeField] private float slotCatchUpSpeed = 6.4f;
        [SerializeField] private float slotSmoothTime = .18f;
        [SerializeField] private bool presentationOnly = true;

        private Vector3 _slotVelocity;

        public string StableMemberId => stableMemberId;
        public int SlotIndex => slotIndex;
        public 공용AnimationAdapter AnimationAdapter => animationAdapter;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string memberId, int index,
            공용AnimationAdapter adapter)
        {
            if (string.IsNullOrWhiteSpace(memberId) || index < 0 || index >= 6
                || adapter == null)
                throw new ArgumentException("TacticalSquadMemberConfigurationInvalid");
            stableMemberId = memberId.Trim();
            slotIndex = index;
            animationAdapter = adapter;
            presentationOnly = true;
            animationAdapter.ConfigureDeterministicPhase(stableMemberId);
        }

        public void RebindStableMemberId(string memberId)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("TacticalSquadMemberStableIdMissing");
            var normalized = memberId.Trim();
            if (stableMemberId == normalized) return;
            stableMemberId = normalized;
            animationAdapter.ConfigureDeterministicPhase(stableMemberId);
        }

        public void ApplySlot(Vector3 localSlot, bool anchorMoving,
            string movementIntentCode, float deltaTime)
        {
            if (!ValidateWiring() || deltaTime < 0f) return;
            var before = transform.localPosition;
            transform.localPosition = Vector3.SmoothDamp(
                before, localSlot, ref _slotVelocity,
                slotSmoothTime, slotCatchUpSpeed, deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                Quaternion.identity, 1f - Mathf.Exp(-deltaTime * 9f));
            var slotMoving = (transform.localPosition - before).sqrMagnitude
                > .000001f;
            if (anchorMoving || slotMoving)
            {
                animationAdapter.ApplyIntent(movementIntentCode ==
                    FarmCombatPresentationCodes.RunMovement
                    ? 공용AnimationIntentCodes.Run
                    : 공용AnimationIntentCodes.Walk);
                return;
            }
            animationAdapter.ApplyIntent(movementIntentCode switch
            {
                FarmCombatPresentationCodes.GuardMovement
                    => 공용AnimationIntentCodes.Guard,
                FarmCombatPresentationCodes.StaggerMovement
                    => 공용AnimationIntentCodes.Stagger,
                _ => 공용AnimationIntentCodes.Idle,
            });
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableMemberId)
                && slotIndex >= 0 && slotIndex < 6
                && slotCatchUpSpeed > 0f && slotSmoothTime > 0f
                && animationAdapter != null
                && animationAdapter.transform == transform
                && animationAdapter.RootMotionDisabled
                && presentationOnly;
    }
}
