using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 통합전시관SeedbedObjectRoot : MonoBehaviour
    {
        [SerializeField] private string objectStableId = string.Empty;
        [SerializeField] private string visualVariantKey = string.Empty;
        [SerializeField] private string placementProfileKey = string.Empty;
        [SerializeField] private Vector2 footprint;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private 통합전시관ObjectSocket[] sockets =
            Array.Empty<통합전시관ObjectSocket>();

        public string ObjectStableId => objectStableId;
        public string VisualVariantKey => visualVariantKey;
        public string PlacementProfileKey => placementProfileKey;
        public Vector2 Footprint => footprint;
        public Transform VisualRoot => visualRoot;
        public IReadOnlyList<통합전시관ObjectSocket> Sockets => sockets;

        public void Configure(
            string stableId,
            string variantKey,
            string profileKey,
            Vector2 size,
            Transform visual,
            통합전시관ObjectSocket[] objectSockets)
        {
            objectStableId = stableId;
            visualVariantKey = variantKey;
            placementProfileKey = profileKey;
            footprint = size;
            visualRoot = visual;
            sockets = objectSockets ?? Array.Empty<통합전시관ObjectSocket>();
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(objectStableId)
                && !string.IsNullOrWhiteSpace(visualVariantKey)
                && !string.IsNullOrWhiteSpace(placementProfileKey)
                && footprint.x > 0f
                && footprint.y > 0f
                && visualRoot != null
                && visualRoot.IsChildOf(transform)
                && visualRoot.GetComponentsInChildren<Renderer>(true).Length > 0
                && sockets != null
                && sockets.Length > 0
                && sockets.All(value => value != null
                    && value.transform.IsChildOf(transform)
                    && value.ValidateWiring())
                && sockets.Select(value => value.SocketCode)
                    .Distinct(StringComparer.Ordinal).Count() == sockets.Length;

        public Transform? FindSocket(string socketCode)
            => sockets.SingleOrDefault(value => value.SocketCode == socketCode)?.transform;
    }
}
