using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.ImmersiveWorld;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 몰입경관InstanceBinding
    {
        [SerializeField] private string instanceStableId = string.Empty;
        [SerializeField] private GameObject instanceRoot = null!;
        [SerializeField] private Transform entryAnchor = null!;
        [SerializeField] private bool traversalReady;

        public string InstanceStableId => instanceStableId;
        public GameObject InstanceRoot => instanceRoot;
        public Transform EntryAnchor => entryAnchor;
        public bool TraversalReady => traversalReady;

        public void Configure(
            string stableId,
            GameObject root,
            Transform entry,
            bool ready)
        {
            instanceStableId = stableId ?? string.Empty;
            instanceRoot = root;
            entryAnchor = entry;
            traversalReady = ready;
        }

        public void SetTraversalReady(bool value) => traversalReady = value;

        public bool Validate()
            => 몰입WorldInstanceCodes.All.Contains(instanceStableId,
                   StringComparer.Ordinal)
               && instanceRoot != null
               && entryAnchor != null
               && entryAnchor.IsChildOf(instanceRoot.transform);
    }

    /// <summary>
    /// Nature 생활 거점을 유지하면서 준비된 전문 경관 하나만 원자적으로 활성화한다.
    /// 이 Controller는 플레이어 표현 위치만 바꾸며 Simulation 상태를 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 몰입경관InstanceController : MonoBehaviour
    {
        [SerializeField] private GameObject naturePersistentRoot = null!;
        [SerializeField] private Transform player = null!;
        [SerializeField] private 몰입경관InstanceBinding[] bindings =
            Array.Empty<몰입경관InstanceBinding>();
        [SerializeField] private bool presentationOnly = true;

        private 몰입WorldTransitionCoordinator coordinator = null!;
        private IReadOnlyDictionary<string, 몰입경관InstanceBinding> bindingById =
            new Dictionary<string, 몰입경관InstanceBinding>();

        public string ActiveInstanceStableId => coordinator == null
            ? string.Empty : coordinator.ActiveInstanceStableId;
        public bool IsTransitioning => coordinator != null && coordinator.IsTransitioning;
        public bool PresentationOnly => presentationOnly;
        public bool ChangesWorldState => false;
        public int ActiveSpecialistInstanceCount => bindings.Count(value =>
            value.InstanceStableId != 몰입WorldInstanceCodes.NatureHome
            && value.InstanceRoot != null && value.InstanceRoot.activeSelf);

        public void Configure(
            GameObject natureRoot,
            Transform playerTransform,
            몰입경관InstanceBinding[] values)
        {
            naturePersistentRoot = natureRoot;
            player = playerTransform;
            bindings = values ?? Array.Empty<몰입경관InstanceBinding>();
            presentationOnly = true;
            Initialize();
        }

        public bool ValidateWiring()
        {
            if (naturePersistentRoot == null
                || player == null
                || !presentationOnly
                || bindings.Length != 몰입WorldInstanceCodes.All.Count
                || bindings.Any(value => value == null || !value.Validate())
                || bindings.Select(value => value.InstanceStableId)
                    .Distinct(StringComparer.Ordinal).Count() != bindings.Length
                || bindings.Select(value => value.InstanceRoot)
                    .Distinct().Count() != bindings.Length)
                return false;

            var natureBindings = bindings.Where(value =>
                    value.InstanceStableId == 몰입WorldInstanceCodes.NatureHome)
                .ToArray();
            return natureBindings.Length == 1
                   && natureBindings[0].InstanceRoot == naturePersistentRoot;
        }

        public void Initialize()
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("ImmersiveLandscapeInstanceWiringInvalid");
            bindingById = bindings.ToDictionary(
                value => value.InstanceStableId,
                value => value,
                StringComparer.Ordinal);
            coordinator = new 몰입WorldTransitionCoordinator(bindingById.Keys);
            ApplyActiveInstance(몰입WorldInstanceCodes.NatureHome);
            player.position = bindingById[몰입WorldInstanceCodes.NatureHome]
                .EntryAnchor.position;
            player.rotation = bindingById[몰입WorldInstanceCodes.NatureHome]
                .EntryAnchor.rotation;
        }

        public void SetTraversalReady(string instanceStableId, bool ready)
        {
            if (!bindingById.TryGetValue(instanceStableId, out var binding))
                throw new InvalidOperationException("ImmersiveWorldInstanceUnknown");
            binding.SetTraversalReady(ready);
        }

        public bool TryActivatePreparedInstance(string instanceStableId)
        {
            if (coordinator == null) Initialize();
            if (!bindingById.TryGetValue(instanceStableId, out var target))
                throw new InvalidOperationException("ImmersiveWorldInstanceUnknown");
            if (string.Equals(coordinator.ActiveInstanceStableId, instanceStableId,
                StringComparison.Ordinal)) return true;
            coordinator.Request(instanceStableId);
            var completed = coordinator.Complete(
                instanceStableId, target.TraversalReady);
            if (!string.Equals(completed.ActiveInstanceStableId, instanceStableId,
                StringComparison.Ordinal)) return false;
            ApplyActiveInstance(instanceStableId);
            player.position = target.EntryAnchor.position;
            player.rotation = target.EntryAnchor.rotation;
            return true;
        }

        private void ApplyActiveInstance(string activeInstanceStableId)
        {
            naturePersistentRoot.SetActive(true);
            foreach (var binding in bindings)
            {
                if (binding.InstanceStableId == 몰입WorldInstanceCodes.NatureHome)
                    continue;
                binding.InstanceRoot.SetActive(string.Equals(
                    binding.InstanceStableId, activeInstanceStableId,
                    StringComparison.Ordinal));
            }
        }
    }
}
