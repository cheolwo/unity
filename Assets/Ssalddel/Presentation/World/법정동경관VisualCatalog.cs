using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 법정동경관CollisionPolicyCodes
    {
        public const string None = "none";
        public const string FootprintOnly = "footprint-only";
        public const string PrefabCollider = "prefab-collider";
    }

    public static class 법정동경관CompositionRoleCodes
    {
        public const string Anchor = "anchor";
        public const string Cluster = "cluster";
        public const string Connector = "connector";
        public const string Detail = "detail";
        public const string Proxy = "proxy";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Anchor, Cluster, Connector, Detail, Proxy,
        };
    }

    public static class 법정동경관ReviewStatusCodes
    {
        public const string Indexed = "indexed";
        public const string Reviewed = "reviewed";
        public const string Approved = "approved";
        public const string Active = "active";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Indexed, Reviewed, Approved, Active,
        };
    }

    [Serializable]
    public sealed class 법정동경관VisualCatalogEntry
    {
        [SerializeField] private string visualKey = string.Empty;
        [SerializeField] private string sourcePack = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private string[] allowedLandCoverCodes = Array.Empty<string>();
        [SerializeField] private string[] allowedRegionRoleCodes = Array.Empty<string>();
        [SerializeField] private Vector2 footprint = Vector2.one;
        [SerializeField] private float placementPadding = .5f;
        [SerializeField] private Vector2 slopeRange = new(0f, 30f);
        [SerializeField] private int densityTier;
        [SerializeField] private int lodGroup;
        [SerializeField] private string[] seasonCodes = { "All" };
        [SerializeField] private bool presentationOnly = true;
        [SerializeField] private string collisionPolicyCode =
            법정동경관CollisionPolicyCodes.FootprintOnly;
        [SerializeField] private long estimatedTriangles;
        [SerializeField] private int materialSlotCount;
        [SerializeField] private int estimatedDrawCalls;
        [SerializeField] private int shadowCasterCount;
        [SerializeField] private int colliderCount;
        [SerializeField] private int animatorCount;
        [SerializeField] private bool clusterAllowed;
        [SerializeField] private bool rotationAllowed = true;
        [SerializeField] private string compositionRoleCode =
            법정동경관CompositionRoleCodes.Detail;
        [SerializeField] private string variantGroupCode = string.Empty;
        [SerializeField] private string reviewStatusCode =
            법정동경관ReviewStatusCodes.Active;
        [SerializeField] private float regionIdentityWeight = .5f;
        [SerializeField] private bool hlodEligible;

        public string VisualKey => visualKey;
        public string SourcePack => sourcePack;
        public GameObject Prefab => prefab;
        public IReadOnlyList<string> AllowedLandCoverCodes => allowedLandCoverCodes;
        public IReadOnlyList<string> AllowedRegionRoleCodes => allowedRegionRoleCodes;
        public Vector2 Footprint => footprint;
        public float PlacementPadding => placementPadding;
        public Vector2 SlopeRange => slopeRange;
        public int DensityTier => densityTier;
        public int LodGroup => lodGroup;
        public IReadOnlyList<string> SeasonCodes => seasonCodes;
        public bool PresentationOnly => presentationOnly;
        public string CollisionPolicyCode => collisionPolicyCode;
        public long EstimatedTriangles => estimatedTriangles;
        public int MaterialSlotCount => materialSlotCount;
        public int EstimatedDrawCalls => estimatedDrawCalls;
        public int ShadowCasterCount => shadowCasterCount;
        public int ColliderCount => colliderCount;
        public int AnimatorCount => animatorCount;
        public bool ClusterAllowed => clusterAllowed;
        public bool RotationAllowed => rotationAllowed;
        public string CompositionRoleCode => compositionRoleCode;
        public string VariantGroupCode => variantGroupCode;
        public string ReviewStatusCode => reviewStatusCode;
        public float RegionIdentityWeight => regionIdentityWeight;
        public bool HlodEligible => hlodEligible;

        public void Configure(
            string key, string pack, GameObject sourcePrefab,
            string[] landCovers, string[] roles, Vector2 size,
            Vector2 slopes, int density, int lod, string[] seasons,
            float padding, string collisionPolicy, long triangles,
            int materials, int drawCalls, int shadowCasters,
            int colliders, int animators, bool canCluster, bool canRotate)
            => Configure(
                key, pack, sourcePrefab, landCovers, roles, size, slopes,
                density, lod, seasons, padding, collisionPolicy, triangles,
                materials, drawCalls, shadowCasters, colliders, animators,
                canCluster, canRotate, 법정동경관CompositionRoleCodes.Detail,
                key, 법정동경관ReviewStatusCodes.Active, .5f, false);

        public void Configure(
            string key, string pack, GameObject sourcePrefab,
            string[] landCovers, string[] roles, Vector2 size,
            Vector2 slopes, int density, int lod, string[] seasons,
            float padding, string collisionPolicy, long triangles,
            int materials, int drawCalls, int shadowCasters,
            int colliders, int animators, bool canCluster, bool canRotate,
            string compositionRole, string variantGroup, string reviewStatus,
            float identityWeight, bool canBuildHlod)
        {
            visualKey = key;
            sourcePack = pack;
            prefab = sourcePrefab;
            allowedLandCoverCodes = landCovers;
            allowedRegionRoleCodes = roles;
            footprint = size;
            placementPadding = padding;
            slopeRange = slopes;
            densityTier = density;
            lodGroup = lod;
            seasonCodes = seasons;
            presentationOnly = true;
            collisionPolicyCode = collisionPolicy;
            estimatedTriangles = triangles;
            materialSlotCount = materials;
            estimatedDrawCalls = drawCalls;
            shadowCasterCount = shadowCasters;
            colliderCount = colliders;
            animatorCount = animators;
            clusterAllowed = canCluster;
            rotationAllowed = canRotate;
            compositionRoleCode = compositionRole ?? string.Empty;
            variantGroupCode = variantGroup ?? string.Empty;
            reviewStatusCode = reviewStatus ?? string.Empty;
            regionIdentityWeight = identityWeight;
            hlodEligible = canBuildHlod;
        }

        public bool Validate()
            => 법정동경관VisualKeys.All.Contains(visualKey, StringComparer.Ordinal)
                && !string.IsNullOrWhiteSpace(sourcePack) && prefab != null
                && allowedLandCoverCodes.Length > 0 && allowedRegionRoleCodes.Length > 0
                && footprint.x > 0f && footprint.y > 0f
                && placementPadding >= 0f
                && slopeRange.x >= 0f && slopeRange.y >= slopeRange.x
                && densityTier is >= 0 and <= 2 && lodGroup is >= 0 and <= 2
                && seasonCodes.Length > 0 && presentationOnly
                && (collisionPolicyCode == 법정동경관CollisionPolicyCodes.None
                    || collisionPolicyCode == 법정동경관CollisionPolicyCodes.FootprintOnly
                    || collisionPolicyCode == 법정동경관CollisionPolicyCodes.PrefabCollider)
                && estimatedTriangles >= 0 && materialSlotCount >= 0
                && estimatedDrawCalls >= 0 && shadowCasterCount >= 0
                && colliderCount >= 0 && animatorCount >= 0
                && 법정동경관CompositionRoleCodes.All.Contains(
                    compositionRoleCode, StringComparer.Ordinal)
                && !string.IsNullOrWhiteSpace(variantGroupCode)
                && 법정동경관ReviewStatusCodes.All.Contains(
                    reviewStatusCode, StringComparer.Ordinal)
                && regionIdentityWeight is >= 0f and <= 1f;
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/법정동 경관 Visual Catalog")]
    public sealed class 법정동경관VisualCatalog : ScriptableObject
    {
        [SerializeField] private string catalogRevision = string.Empty;
        [SerializeField] private 법정동경관VisualCatalogEntry[] entries =
            Array.Empty<법정동경관VisualCatalogEntry>();

        public string CatalogRevision => catalogRevision;
        public IReadOnlyList<법정동경관VisualCatalogEntry> Entries => entries;

        public void Configure(string revision, 법정동경관VisualCatalogEntry[] values)
        {
            catalogRevision = revision;
            entries = values ?? Array.Empty<법정동경관VisualCatalogEntry>();
        }

        public 법정동경관VisualCatalogEntry Resolve(string key)
            => entries.SingleOrDefault(value => value.VisualKey == key)
                ?? throw new InvalidOperationException("LegalDongScenicVisualMissing:" + key);

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogRevision) || entries.Length == 0
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.VisualKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("LegalDongScenicCatalogInvalid");
        }
    }

    [DisallowMultipleComponent]
    public sealed class 법정동경관VisualInstanceView : MonoBehaviour
    {
        [SerializeField] private 법정동경관PlacementData placement = null!;
        [SerializeField] private 법정동경관VisualCatalog sourceCatalog = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private GameObject prefabInstanceRoot = null!;

        public 법정동경관PlacementData Placement => placement;
        public 법정동경관VisualCatalog SourceCatalog => sourceCatalog;
        public Transform VisualRoot => visualRoot;
        public GameObject PrefabInstanceRoot => prefabInstanceRoot;

        public void Configure(
            법정동경관PlacementData value,
            법정동경관VisualCatalog catalog,
            Transform root,
            GameObject instance)
        {
            placement = value;
            sourceCatalog = catalog;
            visualRoot = root;
            prefabInstanceRoot = instance;
        }

        public bool ValidateWiring()
        {
            if (placement == null || !placement.PresentationOnly
                || sourceCatalog == null || visualRoot == null
                || prefabInstanceRoot == null
                || !prefabInstanceRoot.transform.IsChildOf(visualRoot))
                return false;
            var entry = sourceCatalog.Resolve(placement.VisualKey);
            return entry.PresentationOnly
                && entry.AllowedLandCoverCodes.Contains(placement.LandCoverCode)
                && entry.AllowedRegionRoleCodes.Contains(placement.RegionRoleCode);
        }
    }
}
