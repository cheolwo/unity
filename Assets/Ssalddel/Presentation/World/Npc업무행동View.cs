using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class Npc업무행동View : MonoBehaviour
    {
        [SerializeField] private string actorStableId = string.Empty;
        [SerializeField] private string interactionPointKey = string.Empty;
        [SerializeField] private Transform interactionPoint = null!;
        [SerializeField] private 공용AnimationAdapter animationAdapter = null!;
        [SerializeField] private float movementSpeed = 1.6f;
        [SerializeField] private bool presentationOnly = true;
        [SerializeField] private Npc업무행동ProjectionData? currentProjection;

        public string ActorStableId => actorStableId;
        public string InteractionPointKey => interactionPointKey;
        public Transform InteractionPoint => interactionPoint;
        public Npc업무행동ProjectionData? CurrentProjection => currentProjection?.Clone();
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string actorId,
            string pointKey,
            Transform point,
            공용AnimationAdapter adapter,
            float speed = 1.6f)
        {
            actorStableId = actorId?.Trim() ?? string.Empty;
            interactionPointKey = pointKey?.Trim() ?? string.Empty;
            interactionPoint = point;
            animationAdapter = adapter;
            movementSpeed = speed;
            presentationOnly = true;
        }

        public void ApplyAuthoritativeProjection(Npc업무행동ProjectionData projection)
        {
            if (projection == null || !projection.Validate()
                || !projection.PresentationOnly
                || !string.Equals(projection.ActorStableId, actorStableId, StringComparison.Ordinal)
                || !string.Equals(
                    projection.InteractionPointKey,
                    interactionPointKey,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("NpcWorkActionViewProjectionMismatch");
            if (currentProjection != null
                && (projection.Revision < currentProjection.Revision
                    || projection.WorldTick < currentProjection.WorldTick))
                throw new InvalidOperationException("NpcWorkActionViewProjectionRegressed");

            currentProjection = projection.Clone();
            ApplyAnimationIntent();
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(actorStableId)
                && !string.IsNullOrWhiteSpace(interactionPointKey)
                && interactionPoint != null
                && animationAdapter != null
                && animationAdapter.transform == transform
                && movementSpeed > 0f
                && presentationOnly;

        public void TickPresentation(float deltaTime)
        {
            if (!ValidateWiring() || currentProjection == null || deltaTime <= 0f)
                return;
            if (currentProjection.PhaseCode != Npc업무행동PhaseCodes.Navigating)
            {
                ApplyAnimationIntent();
                return;
            }

            var before = transform.position;
            transform.position = Vector3.MoveTowards(
                before,
                interactionPoint.position,
                movementSpeed * deltaTime);
            var delta = transform.position - before;
            if (delta.sqrMagnitude > .000001f)
            {
                transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Walk);
            }
            else
            {
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Idle);
            }
        }

        private void ApplyAnimationIntent()
        {
            if (animationAdapter == null || currentProjection == null) return;
            animationAdapter.ApplyIntent(
                currentProjection.PhaseCode == Npc업무행동PhaseCodes.Navigating
                    ? 공용AnimationIntentCodes.Walk
                    : 공용AnimationIntentCodes.Idle);
        }
    }
}
