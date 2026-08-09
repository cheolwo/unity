using System;
using System.Linq;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;
using UnityEngine.AI;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    [Serializable]
    public sealed class 공동주택대표WaypointBinding
    {
        public string WaypointKey = string.Empty;
        public Transform Target = null!;
    }

    [Serializable]
    public sealed class 공동주택대표ActionBinding
    {
        public string ActionCode = string.Empty;
        public string AnimatorTrigger = string.Empty;
    }

    public sealed class 공동주택대표NpcView : MonoBehaviour,
        INpcMovementPresentationTarget,
        IResidentialGroupRepresentativeDialogueTarget
    {
        [SerializeField] private string npcStableId =
            "npc:sim:residential-group-representative:1";
        [SerializeField] private NavMeshAgent movementAgent = null!;
        [SerializeField] private Animator animator = null!;
        [SerializeField] private TextMesh dialogueText = null!;
        [SerializeField] private GameObject visualRoot = null!;
        [SerializeField] private Collider selectionCollider = null!;
        [SerializeField] private 공동주택대표WaypointBinding[] waypoints =
            Array.Empty<공동주택대표WaypointBinding>();
        [SerializeField] private 공동주택대표ActionBinding[] actions =
            Array.Empty<공동주택대표ActionBinding>();
        [SerializeField] private string speedParameter = "Speed";

        private NpcMovementPresentationModel? current;
        private bool arrivalApplied;

        public event Action<string>? Selected;

        public string NpcStableId => npcStableId;
        public GameObject VisualRoot => visualRoot;

        public void Configure(
            string stableId,
            NavMeshAgent agent,
            Animator targetAnimator,
            TextMesh dialogue,
            GameObject visual,
            Collider collider,
            공동주택대표WaypointBinding[] waypointBindings,
            공동주택대표ActionBinding[] actionBindings)
        {
            npcStableId = stableId?.Trim() ?? string.Empty;
            movementAgent = agent;
            animator = targetAnimator;
            dialogueText = dialogue;
            visualRoot = visual;
            selectionCollider = collider;
            waypoints = waypointBindings ?? Array.Empty<공동주택대표WaypointBinding>();
            actions = actionBindings ?? Array.Empty<공동주택대표ActionBinding>();
        }

        public void ApplyMovementPresentation(NpcMovementPresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.NpcStableId != npcStableId)
                throw new InvalidOperationException("ResidentialRepresentativeNpcMismatch");
            var destination = waypoints.SingleOrDefault(value => value != null
                && value.WaypointKey == model.DestinationWaypointKey)?.Target
                ?? throw new InvalidOperationException(
                    "ResidentialRepresentativeWaypointMissing:" + model.DestinationWaypointKey);
            current = model;
            arrivalApplied = false;
            if (model.MovementStateCode != NpcMovementStateCodes.Moving) return;
            if (!movementAgent.isOnNavMesh)
                throw new InvalidOperationException("ResidentialRepresentativeNavMeshMissing");
            movementAgent.isStopped = false;
            movementAgent.SetDestination(destination.position);
        }

        public void ApplyRepresentativeDialogue(
            ResidentialGroupRepresentativeDialoguePresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            dialogueText.text = model.TitleText + "\n" + model.DemandText + "\n" + model.BoundaryText;
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(npcStableId) && movementAgent != null
                && animator != null && dialogueText != null && visualRoot != null
                && selectionCollider != null && waypoints.Length > 0
                && waypoints.All(value => value != null
                    && !string.IsNullOrWhiteSpace(value.WaypointKey) && value.Target != null);

        private void OnMouseDown() => Selected?.Invoke(npcStableId);

        private void Update()
        {
            if (movementAgent == null || animator == null) return;
            animator.SetFloat(speedParameter, movementAgent.velocity.magnitude);
            if (current == null || arrivalApplied || !movementAgent.isOnNavMesh
                || movementAgent.pathPending
                || movementAgent.remainingDistance > movementAgent.stoppingDistance
                || movementAgent.velocity.sqrMagnitude > 0.01f) return;
            arrivalApplied = true;
            movementAgent.isStopped = true;
            animator.SetFloat(speedParameter, 0f);
            var binding = actions.FirstOrDefault(value => value != null
                && value.ActionCode == current.ArrivalAnimationCode);
            if (binding != null && !string.IsNullOrWhiteSpace(binding.AnimatorTrigger))
                animator.SetTrigger(binding.AnimatorTrigger);
        }
    }
}
