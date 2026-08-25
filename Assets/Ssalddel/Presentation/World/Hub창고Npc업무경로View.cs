using System;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class Hub창고Npc업무경로View : MonoBehaviour
    {
        [SerializeField] private string npcStableId = string.Empty;
        [SerializeField] private string[] worldInteractionIds =
            Array.Empty<string>();
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
        [SerializeField] private float movementSpeed = 1.4f;
        [SerializeField] private bool workActive;
        [SerializeField] private bool presentationOnly = true;
        private int _targetIndex;

        public string NpcStableId => npcStableId;
        public string[] WorldInteractionIds => worldInteractionIds;
        public Transform[] Waypoints => waypoints;
        public bool WorkActive => workActive;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string stableId, string[] wiIds,
            Transform[] route)
        {
            npcStableId = stableId;
            worldInteractionIds = wiIds;
            waypoints = route;
            movementSpeed = 1.4f;
            workActive = false;
            presentationOnly = true;
            _targetIndex = 0;
            if (waypoints.Length > 0)
                transform.position = waypoints[0].position;
            Validate();
        }

        public void Set업무활성(bool value) => workActive = value;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(npcStableId)
                || worldInteractionIds.Length == 0
                || worldInteractionIds.Any(string.IsNullOrWhiteSpace)
                || waypoints.Length < 2
                || waypoints.Any(value => value == null)
                || movementSpeed <= 0f || !presentationOnly)
                throw new InvalidOperationException(
                    "HubWarehouseNpcWorkRouteInvalid:" + npcStableId);
        }

        private void Update()
        {
            if (!workActive || waypoints.Length == 0) return;
            var target = waypoints[_targetIndex];
            var delta = target.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= .04f)
            {
                _targetIndex = (_targetIndex + 1) % waypoints.Length;
                return;
            }

            var direction = delta.normalized;
            transform.position += direction * (movementSpeed * Time.deltaTime);
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(direction,
                    Vector3.up);
        }
    }
}
