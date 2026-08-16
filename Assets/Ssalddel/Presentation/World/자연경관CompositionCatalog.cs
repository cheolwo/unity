using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 자연경관SetNames
    {
        public const string 활엽수림군집 = "활엽수림 군집";
        public const string 침엽수림군집 = "침엽수림 군집";
        public const string 혼효림군집 = "혼효림 군집";
        public const string 수변완충지 = "수변 완충지";
        public const string 바위절개지 = "바위 절개지";
        public const string 산능선 = "산 능선";
        public const string 숲가장자리 = "숲 가장자리";
        public const string 개울회랑 = "개울 회랑";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            활엽수림군집, 침엽수림군집, 혼효림군집, 수변완충지, 바위절개지,
            산능선, 숲가장자리, 개울회랑,
        };
    }

    public static class 자연경관RoleCodes
    {
        public const string Backdrop = "backdrop";
        public const string TerrainTransition = "terrain-transition";
        public const string Canopy = "canopy";
        public const string Understory = "understory";
        public const string WaterEdge = "water-edge";
        public const string Detail = "detail";
        public const string Fx = "fx";
        public const string EventOnly = "event-only";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Backdrop, TerrainTransition, Canopy, Understory,
            WaterEdge, Detail, Fx, EventOnly,
        };
    }

    public static class 자연경관SeasonCodes
    {
        public const string Spring = "spring";
        public const string Summer = "summer";
        public const string Autumn = "autumn";
        public const string Winter = "winter";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Spring, Summer, Autumn, Winter,
        };
    }

    public static class 자연경관MoodCodes
    {
        public const string Peaceful = "peaceful";
        public const string SurvivalEvent = "survival-event";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Peaceful, SurvivalEvent,
        };
    }

    public static class 자연경관MotionPolicyCodes
    {
        public const string Static = "static";
        public const string VegetationWind = "vegetation-wind";
        public const string WaterSurface = "water-surface";
        public const string AmbientParticle = "ambient-particle";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Static, VegetationWind, WaterSurface, AmbientParticle,
        };
    }

    public static class 자연경관ShaderFeatureCodes
    {
        public const string Standard = "standard";
        public const string VegetationWind = "vegetation-wind";
        public const string Water = "water";
        public const string MossSnow = "moss-snow";
        public const string TerrainNormal = "terrain-normal";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Standard, VegetationWind, Water, MossSnow, TerrainNormal,
        };
    }

    public static class 자연경관GpuBudgetTierCodes
    {
        public const string Overview = "overview";
        public const string Region = "region";
        public const string Task = "task";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Overview, Region, Task,
        };
    }

    public static class 자연경관ShadowPolicyCodes
    {
        public const string CastReceive = "cast-receive";
        public const string ReceiveOnly = "receive-only";
        public const string Disabled = "disabled";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            CastReceive, ReceiveOnly, Disabled,
        };
    }

    [Serializable]
    public sealed class 자연경관CompositionCatalogEntry
    {
        [SerializeField] private string setName = string.Empty;
        [SerializeField] private string variantCode = string.Empty;
        [SerializeField] private GameObject prefab = null!;
        [SerializeField] private Vector2 footprint = Vector2.one;
        [SerializeField] private bool hlodEligible = true;
        [SerializeField] private string natureRoleCode = string.Empty;
        [SerializeField] private string[] allowedLandCoverCodes = Array.Empty<string>();
        [SerializeField] private float minimumSlopeDegrees;
        [SerializeField] private float maximumSlopeDegrees = 90f;
        [SerializeField] private bool requiresWaterMask;
        [SerializeField] private string[] allowedSeasonCodes = Array.Empty<string>();
        [SerializeField] private string[] allowedMoodCodes = Array.Empty<string>();
        [SerializeField] private string motionPolicyCode = string.Empty;
        [SerializeField] private string[] shaderFeatureCodes = Array.Empty<string>();
        [SerializeField] private float minimumViewDistance;
        [SerializeField] private float maximumViewDistance = 100f;
        [SerializeField] private string gpuBudgetTierCode = string.Empty;
        [SerializeField] private string shadowPolicyCode = string.Empty;
        [SerializeField] private int triangleCount;
        [SerializeField] private int materialSlotCount;
        [SerializeField] private int colliderCount;
        [SerializeField] private int particleSystemCount;
        [SerializeField] private int animatorCount;
        [SerializeField] private int lodGroupCount;
        [SerializeField] private bool presentationOnly = true;

        public string SetName => setName;
        public string VariantCode => variantCode;
        public GameObject Prefab => prefab;
        public Vector2 Footprint => footprint;
        public bool HlodEligible => hlodEligible;
        public string NatureRoleCode => natureRoleCode;
        public IReadOnlyList<string> AllowedLandCoverCodes => allowedLandCoverCodes;
        public float MinimumSlopeDegrees => minimumSlopeDegrees;
        public float MaximumSlopeDegrees => maximumSlopeDegrees;
        public bool RequiresWaterMask => requiresWaterMask;
        public IReadOnlyList<string> AllowedSeasonCodes => allowedSeasonCodes;
        public IReadOnlyList<string> AllowedMoodCodes => allowedMoodCodes;
        public string MotionPolicyCode => motionPolicyCode;
        public IReadOnlyList<string> ShaderFeatureCodes => shaderFeatureCodes;
        public float MinimumViewDistance => minimumViewDistance;
        public float MaximumViewDistance => maximumViewDistance;
        public string GpuBudgetTierCode => gpuBudgetTierCode;
        public string ShadowPolicyCode => shadowPolicyCode;
        public int TriangleCount => triangleCount;
        public int MaterialSlotCount => materialSlotCount;
        public int ColliderCount => colliderCount;
        public int ParticleSystemCount => particleSystemCount;
        public int AnimatorCount => animatorCount;
        public int LodGroupCount => lodGroupCount;
        public bool PresentationOnly => presentationOnly;
        public string CompositionKey => 월드CompositionDescriptor.BuildKey(
            월드CompositionPackCodes.Nature, setName, variantCode);

        public void Configure(
            string name,
            string variant,
            GameObject sourcePrefab,
            Vector2 size,
            bool canBuildHlod,
            string roleCode,
            string[] landCoverCodes,
            float minSlopeDegrees,
            float maxSlopeDegrees,
            bool needsWaterMask,
            string[] seasonCodes,
            string[] moodCodes,
            string motionCode,
            string[] shaderCodes,
            float minViewDistance,
            float maxViewDistance,
            string budgetTierCode,
            string rendererShadowPolicyCode)
        {
            setName = name;
            variantCode = variant;
            prefab = sourcePrefab;
            footprint = size;
            hlodEligible = canBuildHlod;
            natureRoleCode = roleCode ?? string.Empty;
            allowedLandCoverCodes = landCoverCodes ?? Array.Empty<string>();
            minimumSlopeDegrees = minSlopeDegrees;
            maximumSlopeDegrees = maxSlopeDegrees;
            requiresWaterMask = needsWaterMask;
            allowedSeasonCodes = seasonCodes ?? Array.Empty<string>();
            allowedMoodCodes = moodCodes ?? Array.Empty<string>();
            motionPolicyCode = motionCode ?? string.Empty;
            shaderFeatureCodes = shaderCodes ?? Array.Empty<string>();
            minimumViewDistance = minViewDistance;
            maximumViewDistance = maxViewDistance;
            gpuBudgetTierCode = budgetTierCode ?? string.Empty;
            shadowPolicyCode = rendererShadowPolicyCode ?? string.Empty;
            presentationOnly = true;

            triangleCount = sourcePrefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(value => value.sharedMesh != null)
                .Sum(value => value.sharedMesh.triangles.Length / 3);
            materialSlotCount = sourcePrefab.GetComponentsInChildren<Renderer>(true)
                .Sum(value => value.sharedMaterials.Length);
            colliderCount = sourcePrefab.GetComponentsInChildren<Collider>(true).Length;
            particleSystemCount = sourcePrefab
                .GetComponentsInChildren<ParticleSystem>(true).Length;
            animatorCount = sourcePrefab.GetComponentsInChildren<Animator>(true).Length;
            lodGroupCount = sourcePrefab.GetComponentsInChildren<LODGroup>(true).Length;
        }

        public bool Validate()
            => 자연경관SetNames.All.Contains(setName, StringComparer.Ordinal)
                && 월드CompositionVariantCodes.IsKnown(variantCode)
                && prefab != null && footprint.x > 0f && footprint.y > 0f
                && 자연경관RoleCodes.All.Contains(natureRoleCode, StringComparer.Ordinal)
                && allowedLandCoverCodes.Length > 0
                && allowedLandCoverCodes.All(value =>
                    법정동LandCoverCodes.All.Contains(value, StringComparer.Ordinal))
                && minimumSlopeDegrees >= 0f
                && maximumSlopeDegrees >= minimumSlopeDegrees
                && maximumSlopeDegrees <= 90f
                && (!requiresWaterMask || allowedLandCoverCodes.Contains(
                    법정동LandCoverCodes.Water, StringComparer.Ordinal))
                && allowedSeasonCodes.Length > 0
                && allowedSeasonCodes.All(value =>
                    자연경관SeasonCodes.All.Contains(value, StringComparer.Ordinal))
                && allowedMoodCodes.Length > 0
                && allowedMoodCodes.All(value =>
                    자연경관MoodCodes.All.Contains(value, StringComparer.Ordinal))
                && 자연경관MotionPolicyCodes.All.Contains(
                    motionPolicyCode, StringComparer.Ordinal)
                && shaderFeatureCodes.Length > 0
                && shaderFeatureCodes.All(value =>
                    자연경관ShaderFeatureCodes.All.Contains(value, StringComparer.Ordinal))
                && minimumViewDistance >= 0f
                && maximumViewDistance > minimumViewDistance
                && 자연경관GpuBudgetTierCodes.All.Contains(
                    gpuBudgetTierCode, StringComparer.Ordinal)
                && 자연경관ShadowPolicyCodes.All.Contains(
                    shadowPolicyCode, StringComparer.Ordinal)
                && triangleCount >= 0 && materialSlotCount > 0
                && colliderCount >= 0 && particleSystemCount >= 0
                && animatorCount >= 0 && lodGroupCount >= 0
                && presentationOnly
                && prefab.TryGetComponent<자연경관CompositionSetView>(out var view)
                && view != null && view.ValidateWiring()
                && prefab.TryGetComponent<자연경관ShadowPolicyView>(out var shadowView)
                && shadowView != null && shadowView.ValidateWiring()
                && shadowView.ShadowPolicyCode == shadowPolicyCode
                && view.SetName == setName && view.VariantCode == variantCode;
    }

    [CreateAssetMenu(menuName = "Ssalddel/Presentation/자연 경관 Composition Catalog")]
    public sealed class 자연경관CompositionCatalog : ScriptableObject
    {
        [SerializeField] private 자연경관CompositionCatalogEntry[] entries =
            Array.Empty<자연경관CompositionCatalogEntry>();

        public IReadOnlyList<자연경관CompositionCatalogEntry> Entries => entries;

        public void Configure(자연경관CompositionCatalogEntry[] values)
            => entries = values ?? Array.Empty<자연경관CompositionCatalogEntry>();

        public 자연경관CompositionCatalogEntry Resolve(string setName, string variantCode)
        {
            Validate();
            return entries.SingleOrDefault(value =>
                    value.SetName == setName && value.VariantCode == variantCode)
                ?? throw new InvalidOperationException(
                    "NatureCompositionMissing:" + setName + ":" + variantCode);
        }

        public void Validate()
        {
            var expected = 자연경관SetNames.All.Count * 월드CompositionVariantCodes.All.Count;
            if (entries.Length != expected
                || entries.Any(value => value == null || !value.Validate())
                || entries.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidOperationException("NatureCompositionCatalogInvalid");
        }
    }
}
