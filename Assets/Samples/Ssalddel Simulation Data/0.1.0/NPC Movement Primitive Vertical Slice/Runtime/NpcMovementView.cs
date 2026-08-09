using System;
using Ssalddel.Unity.Npcs;
using UnityEngine;
using UnityEngine.AI;

namespace Ssalddel.Unity.Samples.NpcMovement
{
    [Serializable]
    public sealed class NpcActionAnimationBinding
    {
        public string ActionCode = string.Empty;

        public string AnimatorTrigger = string.Empty;
    }

    public sealed class NpcMovementView : MonoBehaviour, INpcMovementTarget, INpcMovementPresentationTarget
    {
        [SerializeField]
        private string npcStableId = string.Empty;

        [SerializeField]
        private NavMeshAgent movementAgent = null!;

        [SerializeField]
        private Animator animator = null!;

        [SerializeField]
        private ZoneNpcWaypointRegistry waypointRegistry = null!;

        [SerializeField]
        private string speedParameter = "Speed";

        [SerializeField]
        private NpcActionAnimationBinding[] actionBindings = Array.Empty<NpcActionAnimationBinding>();

        private NpcMovementPresentationModel? current;
        private bool arrivalPresentationApplied;

        public string NpcStableId => npcStableId;

        public void Configure(
            string stableId,
            NavMeshAgent agent,
            Animator targetAnimator,
            ZoneNpcWaypointRegistry registry,
            NpcActionAnimationBinding[] bindings)
        {
            npcStableId = stableId?.Trim() ?? string.Empty;
            movementAgent = agent;
            animator = targetAnimator;
            waypointRegistry = registry;
            actionBindings = bindings ?? Array.Empty<NpcActionAnimationBinding>();
        }

        public void ApplyMovement(NpcMovementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ApplyMovementPresentation(new NpcMovementPresentationModel
            {
                StableId = snapshot.StableId,
                DataRevision = snapshot.Revision,
                NpcStableId = snapshot.NpcStableId,
                RouteCode = snapshot.RouteCode,
                CurrentWaypointKey = snapshot.CurrentWaypointKey,
                DestinationWaypointKey = snapshot.DestinationWaypointKey,
                MovementStateCode = snapshot.MovementStateCode,
                ArrivalAnimationCode = snapshot.ArrivalActionCode,
                CanonicalTaskStableId = snapshot.CanonicalTaskStableId,
            });
        }

        public void ApplyMovementPresentation(NpcMovementPresentationModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (!string.Equals(model.NpcStableId, npcStableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("다른 NPC의 이동 snapshot을 적용할 수 없습니다.");
            }

            current = model;
            arrivalPresentationApplied = false;

            if (string.Equals(model.MovementStateCode, NpcMovementStateCodes.Moving, StringComparison.Ordinal))
            {
                MoveTo(model.DestinationWaypointKey);
                return;
            }

            StopMovement();
            if (string.Equals(
                model.MovementStateCode,
                NpcMovementStateCodes.PerformingAction,
                StringComparison.Ordinal))
            {
                ApplyArrivalPresentation(model.ArrivalAnimationCode);
            }
        }

        public bool ValidateWiring()
        {
            return !string.IsNullOrWhiteSpace(npcStableId)
                && movementAgent != null
                && animator != null
                && waypointRegistry != null
                && waypointRegistry.ValidateWiring()
                && !string.IsNullOrWhiteSpace(speedParameter);
        }

        private void Update()
        {
            if (movementAgent == null || animator == null)
            {
                return;
            }

            animator.SetFloat(speedParameter, movementAgent.velocity.magnitude);
            if (current == null
                || arrivalPresentationApplied
                || !string.Equals(current.MovementStateCode, NpcMovementStateCodes.Moving, StringComparison.Ordinal)
                || !movementAgent.isOnNavMesh
                || movementAgent.pathPending
                || movementAgent.remainingDistance > movementAgent.stoppingDistance
                || movementAgent.velocity.sqrMagnitude > 0.01f)
            {
                return;
            }

            StopMovement();
            ApplyArrivalPresentation(current.ArrivalAnimationCode);
        }

        private void MoveTo(string waypointKey)
        {
            if (!waypointRegistry.TryResolve(waypointKey, out var destination))
            {
                throw new InvalidOperationException("NPC waypoint을 찾을 수 없습니다: " + waypointKey);
            }

            if (!movementAgent.isOnNavMesh)
            {
                throw new InvalidOperationException("NPC NavMeshAgent가 NavMesh 위에 있지 않습니다.");
            }

            movementAgent.isStopped = false;
            movementAgent.SetDestination(destination.position);
        }

        private void StopMovement()
        {
            if (movementAgent.isOnNavMesh)
            {
                movementAgent.isStopped = true;
                movementAgent.ResetPath();
            }

            animator.SetFloat(speedParameter, 0f);
        }

        private void ApplyArrivalPresentation(string actionCode)
        {
            arrivalPresentationApplied = true;
            if (string.IsNullOrWhiteSpace(actionCode))
            {
                return;
            }

            foreach (var binding in actionBindings)
            {
                if (binding != null
                    && string.Equals(binding.ActionCode, actionCode, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(binding.AnimatorTrigger))
                {
                    animator.SetTrigger(binding.AnimatorTrigger);
                    return;
                }
            }
        }
    }
}
