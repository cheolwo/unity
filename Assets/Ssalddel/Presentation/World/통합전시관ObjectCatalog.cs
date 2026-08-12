using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 통합전시관ObjectVisualCatalogEntry
    {
        [SerializeField] private string objectStableId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string visualVariantKey = string.Empty;
        [SerializeField] private string placementProfileKey = string.Empty;
        [SerializeField] private string[] requiredSocketCodes = Array.Empty<string>();
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private Vector2 footprint;
        [SerializeField] private Vector3 measuredBoundsSize;

        public string ObjectStableId => objectStableId;
        public string DisplayName => displayName;
        public string VisualVariantKey => visualVariantKey;
        public string PlacementProfileKey => placementProfileKey;
        public IReadOnlyList<string> RequiredSocketCodes => requiredSocketCodes;
        public GameObject Prefab => prefab;
        public Vector2 Footprint => footprint;
        public Vector3 MeasuredBoundsSize => measuredBoundsSize;

        public void Configure(
            string stableId,
            string name,
            string variantKey,
            string profileKey,
            string[] socketCodes,
            GameObject sourcePrefab,
            Vector2 size,
            Vector3 boundsSize)
        {
            objectStableId = stableId;
            displayName = name;
            visualVariantKey = variantKey;
            placementProfileKey = profileKey;
            requiredSocketCodes = socketCodes ?? Array.Empty<string>();
            prefab = sourcePrefab;
            footprint = size;
            measuredBoundsSize = boundsSize;
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(objectStableId)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(visualVariantKey)
                || string.IsNullOrWhiteSpace(placementProfileKey)
                || requiredSocketCodes == null
                || requiredSocketCodes.Length == 0
                || requiredSocketCodes.Any(string.IsNullOrWhiteSpace)
                || requiredSocketCodes.Distinct(StringComparer.Ordinal).Count() != requiredSocketCodes.Length
                || prefab == null
                || footprint.x <= 0f
                || footprint.y <= 0f
                || measuredBoundsSize.x <= 0f
                || measuredBoundsSize.y <= 0f
                || measuredBoundsSize.z <= 0f)
                return false;

            var root = prefab.GetComponent<통합전시관SeedbedObjectRoot>();
            return root != null
                && root.ValidateWiring()
                && root.ObjectStableId == objectStableId
                && root.VisualVariantKey == visualVariantKey
                && root.PlacementProfileKey == placementProfileKey
                && root.Footprint == footprint
                && root.Sockets.Select(value => value.SocketCode)
                    .SequenceEqual(requiredSocketCodes, StringComparer.Ordinal);
        }
    }

}
