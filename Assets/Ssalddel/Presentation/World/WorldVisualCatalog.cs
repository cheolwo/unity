using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class WorldVisualCatalogEntry
    {
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private Vector3 localPositionCorrection;
        [SerializeField] private Vector3 localEulerCorrection;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public string VisualKey => visualKey;
        public GameObject Prefab => prefab;
        public Vector3 LocalPositionCorrection => localPositionCorrection;
        public Vector3 LocalEulerCorrection => localEulerCorrection;
        public Vector3 LocalScale => localScale;

        public void Configure(
            string key,
            GameObject sourcePrefab,
            Vector3 positionCorrection,
            Vector3 eulerCorrection,
            Vector3 scale)
        {
            visualKey = key;
            prefab = sourcePrefab;
            localPositionCorrection = positionCorrection;
            localEulerCorrection = eulerCorrection;
            localScale = scale;
        }

        public bool Validate()
            => WorldVisualKeys.IsKnown(visualKey)
                && prefab != null
                && localScale.x > 0f
                && localScale.y > 0f
                && localScale.z > 0f;
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/World Visual Catalog")]
    public sealed class WorldVisualCatalog : ScriptableObject
    {
        [SerializeField] private string catalogCode = string.Empty;
        [SerializeField] private WorldVisualCatalogEntry[] entries =
            Array.Empty<WorldVisualCatalogEntry>();

        public string CatalogCode => catalogCode;
        public IReadOnlyList<WorldVisualCatalogEntry> Entries => entries;

        public void Configure(string code, WorldVisualCatalogEntry[] values)
        {
            catalogCode = code;
            entries = values ?? Array.Empty<WorldVisualCatalogEntry>();
        }

        public WorldVisualCatalogEntry Resolve(string visualKey)
        {
            Validate();
            return entries.SingleOrDefault(value => value.VisualKey == visualKey)
                ?? throw new InvalidOperationException("WorldVisualKeyMissing:" + visualKey);
        }

        public void Validate()
        {
            if (!WorldVisualCatalogCodes.IsKnown(catalogCode)
                || entries == null || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.VisualKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
            {
                throw new InvalidOperationException("WorldVisualCatalogInvalid:" + catalogCode);
            }
        }
    }
}
