using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class SyntyPackInventoryCodes
    {
        public const string Farm = "farm";
        public const string Town = "town";
        public const string City = "city";

        public static IReadOnlyList<string> All { get; } = new[] { Farm, Town, City };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);
    }

    public static class SyntyPackAssetUseKindCodes
    {
        public const string StandaloneCandidate = "standalone-candidate";
        public const string CompositionPart = "composition-part";
        public const string Actor = "actor";
        public const string Item = "item";
        public const string Fx = "fx";
        public const string ManualReview = "manual-review";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            StandaloneCandidate,
            CompositionPart,
            Actor,
            Item,
            Fx,
            ManualReview,
        };

        public static bool IsKnown(string value) =>
            All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class SyntyPackAssetInventoryEntry
    {
        [SerializeField] private string inventoryId = string.Empty;
        [SerializeField] private string packCode = string.Empty;
        [SerializeField] private string categoryCode = string.Empty;
        [SerializeField] private string assetUseKindCode = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private long triangleCount;
        [SerializeField] private int materialSlotCount;
        [SerializeField] private int shadowCasterCount;
        [SerializeField] private int colliderCount;
        [SerializeField] private int animatorCount;
        [SerializeField] private int particleSystemCount;
        [SerializeField] private int lodGroupCount;
        [SerializeField] private string sourceFingerprintSha256 = string.Empty;
        [SerializeField] private bool presentationOnly = true;

        public string InventoryId => inventoryId;
        public string PackCode => packCode;
        public string CategoryCode => categoryCode;
        public string AssetUseKindCode => assetUseKindCode;
        public GameObject Prefab => prefab;
        public Bounds LocalBounds => localBounds;
        public long TriangleCount => triangleCount;
        public int MaterialSlotCount => materialSlotCount;
        public int ShadowCasterCount => shadowCasterCount;
        public int ColliderCount => colliderCount;
        public int AnimatorCount => animatorCount;
        public int ParticleSystemCount => particleSystemCount;
        public int LodGroupCount => lodGroupCount;
        public string SourceFingerprintSha256 => sourceFingerprintSha256;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string id,
            string sourcePackCode,
            string sourceCategoryCode,
            string useKindCode,
            GameObject sourcePrefab,
            Bounds bounds,
            long triangles,
            int materialSlots,
            int shadowCasters,
            int colliders,
            int animators,
            int particleSystems,
            int lodGroups,
            string sourceFingerprint)
        {
            inventoryId = id ?? string.Empty;
            packCode = sourcePackCode ?? string.Empty;
            categoryCode = sourceCategoryCode ?? string.Empty;
            assetUseKindCode = useKindCode ?? string.Empty;
            prefab = sourcePrefab;
            localBounds = bounds;
            triangleCount = triangles;
            materialSlotCount = materialSlots;
            shadowCasterCount = shadowCasters;
            colliderCount = colliders;
            animatorCount = animators;
            particleSystemCount = particleSystems;
            lodGroupCount = lodGroups;
            sourceFingerprintSha256 = sourceFingerprint ?? string.Empty;
            presentationOnly = true;
        }

        public bool Validate() =>
            !string.IsNullOrWhiteSpace(inventoryId)
            && !inventoryId.Contains("/", StringComparison.Ordinal)
            && !inventoryId.Contains("\\", StringComparison.Ordinal)
            && SyntyPackInventoryCodes.IsKnown(packCode)
            && !string.IsNullOrWhiteSpace(categoryCode)
            && SyntyPackAssetUseKindCodes.IsKnown(assetUseKindCode)
            && prefab != null
            && IsFinite(localBounds.center)
            && IsFinite(localBounds.size)
            && localBounds.size.x >= 0f
            && localBounds.size.y >= 0f
            && localBounds.size.z >= 0f
            && triangleCount >= 0
            && materialSlotCount >= 0
            && shadowCasterCount >= 0
            && colliderCount >= 0
            && animatorCount >= 0
            && particleSystemCount >= 0
            && lodGroupCount >= 0
            && sourceFingerprintSha256.Length == 64
            && sourceFingerprintSha256.All(Uri.IsHexDigit)
            && presentationOnly;

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/Synty 팩 전수 기술 대장")]
    public sealed class SyntyPackAssetInventoryCatalog : ScriptableObject
    {
        [SerializeField] private string scanRuleRevision = string.Empty;
        [SerializeField] private string catalogSourceHashSha256 = string.Empty;
        [SerializeField] private SyntyPackAssetInventoryEntry[] entries =
            Array.Empty<SyntyPackAssetInventoryEntry>();

        public string ScanRuleRevision => scanRuleRevision;
        public string CatalogSourceHashSha256 => catalogSourceHashSha256;
        public IReadOnlyList<SyntyPackAssetInventoryEntry> Entries => entries;

        public void Configure(
            string revision,
            string sourceHashSha256,
            SyntyPackAssetInventoryEntry[] values)
        {
            scanRuleRevision = revision ?? string.Empty;
            catalogSourceHashSha256 = sourceHashSha256 ?? string.Empty;
            entries = values ?? Array.Empty<SyntyPackAssetInventoryEntry>();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(scanRuleRevision)
                || catalogSourceHashSha256.Length != 64
                || !catalogSourceHashSha256.All(Uri.IsHexDigit)
                || entries == null
                || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.InventoryId)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length
                || entries.Select(value => value.Prefab)
                    .Distinct().Count() != entries.Length
                || SyntyPackInventoryCodes.All.Any(pack =>
                    entries.All(value => value.PackCode != pack)))
            {
                throw new InvalidOperationException("SyntyPackAssetInventoryCatalogInvalid");
            }
        }
    }
}
