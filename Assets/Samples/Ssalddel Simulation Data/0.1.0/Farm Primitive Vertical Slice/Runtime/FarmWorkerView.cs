using System;
using Ssalddel.Unity.Npcs;
using UnityEngine;
using UnityEngine.AI;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmWorkerView : MonoBehaviour, INpcMovementTarget
    {
        [Serializable]
        public struct WaypointBinding
        {
            public string Key;
            public Transform Transform;
        }

        [SerializeField] private string npcStableId = string.Empty;
        [SerializeField] private NavMeshAgent movementAgent = null!;
        [SerializeField] private Animator animator = null!;
        [SerializeField] private Renderer bodyRenderer = null!;
        [SerializeField] private WaypointBinding[] waypoints = Array.Empty<WaypointBinding>();
        private string arrivalActionCode = string.Empty;
        private bool moving;

        public string NpcStableId => npcStableId;

        public void Configure(
            string stableId,
            NavMeshAgent agent,
            Animator animatorValue,
            Renderer rendererValue,
            WaypointBinding[] bindings)
        {
            npcStableId = stableId;
            movementAgent = agent;
            animator = animatorValue;
            bodyRenderer = rendererValue;
            waypoints = bindings;
        }

        public void ApplyMovement(NpcMovementSnapshot snapshot)
        {
            if (!string.Equals(snapshot.NpcStableId, npcStableId, StringComparison.Ordinal))
                throw new InvalidOperationException("FarmWorkerStableIdMismatch");
            var destination = Find(snapshot.DestinationWaypointKey)
                ?? throw new InvalidOperationException("FarmWorkerWaypointMissing:" + snapshot.DestinationWaypointKey);
            arrivalActionCode = snapshot.ArrivalActionCode;
            moving = string.Equals(snapshot.MovementStateCode, NpcMovementStateCodes.Moving, StringComparison.Ordinal);
            if (animator != null) animator.SetBool("IsMoving", moving);
            if (moving && movementAgent.isOnNavMesh) movementAgent.SetDestination(destination.position);
        }

        public bool ValidateWiring()
        {
            if (string.IsNullOrWhiteSpace(npcStableId) || movementAgent == null
                || bodyRenderer == null || waypoints.Length < 2) return false;
            foreach (var waypoint in waypoints)
                if (string.IsNullOrWhiteSpace(waypoint.Key) || waypoint.Transform == null) return false;
            return true;
        }

        private void Update()
        {
            if (!moving || !movementAgent.isOnNavMesh || movementAgent.pathPending
                || movementAgent.remainingDistance > movementAgent.stoppingDistance) return;
            moving = false;
            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
                if (!string.IsNullOrWhiteSpace(arrivalActionCode)) animator.SetTrigger("ArrivalAction");
            }
        }

        private Transform? Find(string key)
        {
            foreach (var waypoint in waypoints)
                if (string.Equals(waypoint.Key, key, StringComparison.Ordinal)) return waypoint.Transform;
            return null;
        }
    }
}
