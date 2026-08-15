using System;
using System.Collections.Generic;
using Ssalddel.Unity.Survival;
using UnityEngine;
using UnityEngine.AI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class 전술분대대형Controller : MonoBehaviour
    {
        [SerializeField] private string sideCode = string.Empty;
        [SerializeField] private string squadStableId = string.Empty;
        [SerializeField] private Transform innerFarmAnchor = null!;
        [SerializeField] private Transform perimeterAnchor = null!;
        [SerializeField] private Transform forwardAnchor = null!;
        [SerializeField] private NavMeshAgent navigationAgent = null!;
        [SerializeField] private 전술분대원View[] members = Array.Empty<전술분대원View>();
        [SerializeField] private string formationCode = FarmCombatPresentationCodes.LineFormation;
        [SerializeField] private string movementIntentCode = FarmCombatPresentationCodes.IdleMovement;
        [SerializeField] private string targetPositionCode = FarmCombatPresentationCodes.Perimeter;
        [SerializeField] private string diagnosticCode = string.Empty;
        [SerializeField] private float restingFacingYaw;
        [SerializeField] private float facingResponsiveness = 8f;
        [SerializeField] private bool presentationOnly = true;

        private bool _hasFrame;
        private bool _navigationReady;

        public string SideCode => sideCode;
        public string SquadStableId => squadStableId;
        public string FormationCode => formationCode;
        public string MovementIntentCode => movementIntentCode;
        public string TargetPositionCode => targetPositionCode;
        public string DiagnosticCode => diagnosticCode;
        public IReadOnlyList<전술분대원View> Members => members;
        public NavMeshAgent NavigationAgent => navigationAgent;
        public float RestingFacingYaw => restingFacingYaw;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string side, Transform innerFarm,
            Transform perimeter, Transform forward, NavMeshAgent agent,
            전술분대원View[] memberViews)
        {
            sideCode = side ?? string.Empty;
            innerFarmAnchor = innerFarm;
            perimeterAnchor = perimeter;
            forwardAnchor = forward;
            navigationAgent = agent;
            members = memberViews ?? Array.Empty<전술분대원View>();
            restingFacingYaw = sideCode == FarmCombatPresentationCodes.Hostile
                ? 180f
                : 0f;
            navigationAgent.updateRotation = false;
            transform.localRotation = Quaternion.Euler(0f, restingFacingYaw, 0f);
            presentationOnly = true;
            diagnosticCode = string.Empty;
            if (!ValidateWiring())
                throw new ArgumentException(
                    "TacticalSquadFormationConfigurationInvalid");
        }

        public void ApplyFrame(FarmTacticalSquadMovementPresentationFrame frame)
        {
            if (frame == null || !frame.PresentationOnly
                || frame.SideCode != sideCode
                || frame.DisplayedMemberCount < 0
                || frame.DisplayedMemberCount > members.Length
                || frame.DisplayMemberStableIds.Length
                    != frame.DisplayedMemberCount
                || !IsSupportedFormation(frame.FormationCode)
                || !IsSupportedMovement(frame.MovementIntentCode)
                || !IsSupportedPosition(frame.TargetPositionCode))
                throw new InvalidOperationException(
                    "TacticalSquadMovementFrameInvalid");
            squadStableId = frame.SquadStableId;
            formationCode = frame.FormationCode;
            movementIntentCode = frame.MovementIntentCode;
            targetPositionCode = frame.TargetPositionCode;
            for (var index = 0; index < members.Length; index++)
            {
                var visible = index < frame.DisplayedMemberCount;
                members[index].gameObject.SetActive(visible);
                if (visible)
                    members[index].RebindStableMemberId(
                        frame.DisplayMemberStableIds[index]);
            }
            _hasFrame = true;
            _navigationReady = false;
            TryBeginNavigation();
        }

        public void TickPresentation(float deltaTime)
        {
            if (!_hasFrame || !ValidateWiring() || deltaTime < 0f) return;
            if (!_navigationReady)
                TryBeginNavigation();
            var anchorMoving = navigationAgent.isOnNavMesh
                && navigationAgent.velocity.sqrMagnitude > .01f;
            UpdateFacing(anchorMoving, deltaTime);
            for (var index = 0; index < members.Length; index++)
            {
                var member = members[index];
                if (!member.gameObject.activeSelf) continue;
                member.ApplySlot(CalculateSlot(formationCode, member.SlotIndex),
                    anchorMoving, movementIntentCode, deltaTime);
            }
        }

        public static Vector3 CalculateSlot(string formation, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return formation switch
            {
                FarmCombatPresentationCodes.WedgeFormation => slotIndex switch
                {
                    0 => new Vector3(0f, 0f, 0f),
                    1 => new Vector3(-1.25f, 0f, -1.15f),
                    2 => new Vector3(1.25f, 0f, -1.15f),
                    3 => new Vector3(-2.5f, 0f, -2.3f),
                    4 => new Vector3(2.5f, 0f, -2.3f),
                    _ => new Vector3(0f, 0f, -3.45f),
                },
                FarmCombatPresentationCodes.ColumnFormation =>
                    new Vector3(slotIndex % 2 == 0 ? -.35f : .35f,
                        0f, -slotIndex * 1.05f),
                FarmCombatPresentationCodes.LineFormation =>
                    new Vector3((slotIndex % 3 - 1) * 1.55f,
                        0f, -(slotIndex / 3) * 1.45f),
                _ => throw new ArgumentException(
                    "TacticalFormationUnknown:" + formation),
            };
        }

        public bool ValidateWiring()
        {
            if ((sideCode != FarmCombatPresentationCodes.Allied
                    && sideCode != FarmCombatPresentationCodes.Hostile)
                || innerFarmAnchor == null || perimeterAnchor == null
                || forwardAnchor == null || navigationAgent == null
                || navigationAgent.transform != transform
                || members == null || members.Length != 6
                || facingResponsiveness <= 0f || !presentationOnly)
                return false;
            var occupiedSlots = new bool[6];
            foreach (var member in members)
            {
                if (member == null || !member.ValidateWiring()
                    || occupiedSlots[member.SlotIndex])
                    return false;
                occupiedSlots[member.SlotIndex] = true;
            }
            return true;
        }

        private Transform ResolveAnchor(string positionCode)
            => positionCode switch
            {
                FarmCombatPresentationCodes.InnerFarm => innerFarmAnchor,
                FarmCombatPresentationCodes.Perimeter => perimeterAnchor,
                FarmCombatPresentationCodes.Forward => forwardAnchor,
                _ => throw new ArgumentException(
                    "TacticalPositionUnknown:" + positionCode),
            };

        private bool TryBeginNavigation()
        {
            if (!navigationAgent.isActiveAndEnabled)
            {
                diagnosticCode = "tactical-navigation.surface-pending";
                return false;
            }

            if (!navigationAgent.isOnNavMesh
                && NavMesh.SamplePosition(transform.position, out var hit, 2f,
                    NavMesh.AllAreas))
                navigationAgent.Warp(hit.position);

            var target = ResolveAnchor(targetPositionCode);
            _navigationReady = navigationAgent.isOnNavMesh
                && navigationAgent.SetDestination(target.position);
            diagnosticCode = _navigationReady
                ? string.Empty
                : "tactical-navigation.surface-pending";
            return _navigationReady;
        }

        private void UpdateFacing(bool anchorMoving, float deltaTime)
        {
            Quaternion targetRotation;
            var planarVelocity = navigationAgent.velocity;
            planarVelocity.y = 0f;
            if (anchorMoving && planarVelocity.sqrMagnitude > .01f)
            {
                targetRotation = Quaternion.LookRotation(
                    planarVelocity.normalized, Vector3.up);
            }
            else
            {
                var parentRotation = transform.parent == null
                    ? Quaternion.identity
                    : transform.parent.rotation;
                targetRotation = parentRotation
                    * Quaternion.Euler(0f, restingFacingYaw, 0f);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation,
                targetRotation,
                1f - Mathf.Exp(-deltaTime * facingResponsiveness));
        }

        private static bool IsSupportedFormation(string value)
            => value == FarmCombatPresentationCodes.LineFormation
                || value == FarmCombatPresentationCodes.WedgeFormation
                || value == FarmCombatPresentationCodes.ColumnFormation;

        private static bool IsSupportedMovement(string value)
            => value == FarmCombatPresentationCodes.IdleMovement
                || value == FarmCombatPresentationCodes.RunMovement
                || value == FarmCombatPresentationCodes.GuardMovement
                || value == FarmCombatPresentationCodes.StaggerMovement;

        private static bool IsSupportedPosition(string value)
            => value == FarmCombatPresentationCodes.InnerFarm
                || value == FarmCombatPresentationCodes.Perimeter
                || value == FarmCombatPresentationCodes.Forward;

        private void Update() => TickPresentation(Time.deltaTime);
    }
}
