using System;
using Ssalddel.Unity.Samples.NpcMovement;
using Ssalddel.Unity.Transport;
using UnityEngine;
using UnityEngine.AI;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class TransportCorridorTruckView : MonoBehaviour, ITruckMovementTarget, ITruckMovementPresentationTarget
    {
        [SerializeField]
        private string truckStableId = string.Empty;

        [SerializeField]
        private NavMeshAgent agent = null!;

        [SerializeField]
        private Animator animator = null!;

        [SerializeField]
        private ZoneNpcWaypointRegistry waypointRegistry = null!;

        [SerializeField]
        private Transform cargoVisualRoot = null!;

        [SerializeField]
        private TextMesh statusLabel = null!;

        public string TruckStableId => truckStableId;

        public void Configure(
            string stableId,
            NavMeshAgent navAgent,
            Animator truckAnimator,
            ZoneNpcWaypointRegistry registry,
            Transform cargoRoot,
            TextMesh label)
        {
            truckStableId = stableId?.Trim() ?? string.Empty;
            agent = navAgent;
            animator = truckAnimator;
            waypointRegistry = registry;
            cargoVisualRoot = cargoRoot;
            statusLabel = label;
        }

        public void ApplyTruckMovement(TruckMovementSnapshot movement)
        {
            ApplyTruckMovementPresentation(new TruckMovementPresentationModel
            {
                TruckStableId = movement.StableId,
                CargoStableId = movement.CargoStableId,
                CanonicalTaskStableId = movement.CanonicalTaskStableId,
                RouteCode = movement.RouteCode,
                CurrentNodeKey = movement.CurrentNodeKey,
                DestinationNodeKey = movement.DestinationNodeKey,
                MovementStateCode = movement.MovementStateCode,
                ArrivalAnimationCode = movement.ArrivalActionCode,
                DataRevision = movement.Revision,
                StatusLabelText = movement.CargoStableId + "\n" + movement.CurrentNodeKey + " → " + movement.DestinationNodeKey,
            });
        }

        public void ApplyTruckMovementPresentation(TruckMovementPresentationModel model)
        {
            if (!string.Equals(model.TruckStableId, truckStableId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TruckStableIdMismatch");
            }

            if (!waypointRegistry.TryResolve(model.CurrentNodeKey, out var current)
                || !waypointRegistry.TryResolve(model.DestinationNodeKey, out var destination))
            {
                throw new InvalidOperationException("TransportCorridorWaypointMissing");
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                if (agent.isOnNavMesh)
                {
                    agent.Warp(current.position);
                }
                else
                {
                    transform.position = current.position;
                }
            }

            if (agent.isOnNavMesh)
            {
                agent.SetDestination(destination.position);
            }

            if (animator != null)
            {
                animator.SetBool("IsMoving", agent.isOnNavMesh);
            }

            cargoVisualRoot.gameObject.SetActive(true);
            statusLabel.text = model.StatusLabelText;
        }

        public void Hide()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            gameObject.SetActive(false);
        }

        public bool ValidateWiring()
        {
            return !string.IsNullOrWhiteSpace(truckStableId)
                && agent != null
                && waypointRegistry != null
                && waypointRegistry.ValidateWiring()
                && cargoVisualRoot != null
                && statusLabel != null;
        }
    }
}
