using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 방향광표현FailureCodes
    {
        public const string ProfileUnknown =
            "DirectionalLightingProfileUnknown";
        public const string LightDirectionInvalid =
            "LightDirectionInvalid";
        public const string TargetMissing =
            "DirectionalLightingTargetMissing";
        public const string SurfaceNormalMissing =
            "SurfaceNormalMissing";
        public const string LitShaderUnsupported =
            "LitShaderUnsupported";
        public const string ShadowPolicyMismatch =
            "ShadowPolicyMismatch";
    }

    public readonly struct 방향광표현Profile
    {
        public 방향광표현Profile(
            string stableId,
            float directLightMultiplier,
            float shadowStrengthMultiplier,
            float dayAmbientLift,
            float twilightAmbientLift,
            float nightAmbientLift)
        {
            StableId = stableId ?? string.Empty;
            DirectLightMultiplier = directLightMultiplier;
            ShadowStrengthMultiplier = shadowStrengthMultiplier;
            DayAmbientLift = dayAmbientLift;
            TwilightAmbientLift = twilightAmbientLift;
            NightAmbientLift = nightAmbientLift;
        }

        public string StableId { get; }
        public float DirectLightMultiplier { get; }
        public float ShadowStrengthMultiplier { get; }
        public float DayAmbientLift { get; }
        public float TwilightAmbientLift { get; }
        public float NightAmbientLift { get; }

        public float AmbientLift(월드시간대AnchorCode anchor) => anchor switch
        {
            월드시간대AnchorCode.Dawn => TwilightAmbientLift,
            월드시간대AnchorCode.GoldenDusk => TwilightAmbientLift,
            월드시간대AnchorCode.Night => NightAmbientLift,
            _ => DayAmbientLift,
        };
    }

    /// <summary>
    /// 서버의 LightingProfileKey와 같은 의미 키를 Unity 자연광 설정으로 연결합니다.
    /// 알 수 없는 키는 화면 안전성을 위해 기본값으로 표시하되 검증은 실패합니다.
    /// </summary>
    public static class World방향광표현Catalog
    {
        public const string DefaultProfileStableId =
            "lighting.pyeongchang.shared-day.v2";
        public const string RuleRevision = "directional-lighting.natural.r1";

        private static readonly 방향광표현Profile DefaultProfile = new(
            DefaultProfileStableId,
            directLightMultiplier: 1f,
            shadowStrengthMultiplier: 1f,
            dayAmbientLift: 1f,
            twilightAmbientLift: 1.1f,
            nightAmbientLift: 1.2f);

        public static 방향광표현Profile Resolve(string? stableId,
            out bool wasResolved)
        {
            wasResolved = string.Equals(stableId, DefaultProfileStableId,
                StringComparison.Ordinal);
            return DefaultProfile;
        }
    }

    public readonly struct 방향광표현Model
    {
        public 방향광표현Model(
            string profileStableId,
            string ruleRevision,
            Vector3 surfaceToLightDirectionWorld,
            float directLightMultiplier,
            float shadowStrengthMultiplier,
            float ambientLift,
            bool profileWasResolved,
            string failureCode)
        {
            ProfileStableId = profileStableId ?? string.Empty;
            RuleRevision = ruleRevision ?? string.Empty;
            SurfaceToLightDirectionWorld = surfaceToLightDirectionWorld;
            DirectLightMultiplier = directLightMultiplier;
            ShadowStrengthMultiplier = shadowStrengthMultiplier;
            AmbientLift = ambientLift;
            ProfileWasResolved = profileWasResolved;
            FailureCode = failureCode ?? string.Empty;
        }

        public string ProfileStableId { get; }
        public string RuleRevision { get; }
        public Vector3 SurfaceToLightDirectionWorld { get; }
        public float DirectLightMultiplier { get; }
        public float ShadowStrengthMultiplier { get; }
        public float AmbientLift { get; }
        public bool ProfileWasResolved { get; }
        public string FailureCode { get; }
    }

    public static class 방향광내적Evaluator
    {
        public static float Evaluate(Vector3 surfaceNormalWorld,
            Vector3 surfaceToLightDirectionWorld)
        {
            RequireDirection(surfaceNormalWorld, nameof(surfaceNormalWorld));
            RequireDirection(surfaceToLightDirectionWorld,
                nameof(surfaceToLightDirectionWorld));
            return Mathf.Clamp01(Vector3.Dot(surfaceNormalWorld.normalized,
                surfaceToLightDirectionWorld.normalized));
        }

        internal static bool IsDirectionValid(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z)
               && value.sqrMagnitude > .0000001f;

        private static void RequireDirection(Vector3 value, string name)
        {
            if (!IsDirectionValid(value))
                throw new ArgumentException(
                    방향광표현FailureCodes.LightDirectionInvalid, name);
        }

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class 방향광표현Projector
    {
        public static 방향광표현Model Project(
            월드시간대PresentationModel timeOfDay,
            string? profileStableId)
        {
            var profile = World방향광표현Catalog.Resolve(profileStableId,
                out var profileWasResolved);
            var rotation = Quaternion.Euler(timeOfDay.SunPitch,
                timeOfDay.SunYaw, 0f);
            var direction = -(rotation * Vector3.forward);
            var validDirection = 방향광내적Evaluator.IsDirectionValid(direction);
            var ambientLift = Mathf.Lerp(
                profile.AmbientLift(timeOfDay.PreviousAnchor),
                profile.AmbientLift(timeOfDay.NextAnchor),
                Mathf.Clamp01(timeOfDay.BlendWeight));
            var failureCode = !validDirection
                ? 방향광표현FailureCodes.LightDirectionInvalid
                : !profileWasResolved
                    ? 방향광표현FailureCodes.ProfileUnknown
                    : string.Empty;
            return new 방향광표현Model(
                profile.StableId,
                World방향광표현Catalog.RuleRevision,
                validDirection ? direction.normalized : Vector3.up,
                profile.DirectLightMultiplier,
                profile.ShadowStrengthMultiplier,
                ambientLift,
                profileWasResolved,
                failureCode);
        }
    }

    public readonly struct 방향광표현TargetPolicy
    {
        public 방향광표현TargetPolicy(bool castShadows,
            bool receiveShadows)
        {
            CastShadows = castShadows;
            ReceiveShadows = receiveShadows;
        }

        public bool CastShadows { get; }
        public bool ReceiveShadows { get; }
    }

    /// <summary>
    /// CompositionKey와 배치 분류를 Synty 원본과 분리된 Renderer 정책에 연결합니다.
    /// </summary>
    public static class World방향광표현TargetCatalog
    {
        public static readonly 방향광표현TargetPolicy GroundSurface =
            new(false, true);
        public static readonly 방향광표현TargetPolicy CastReceive =
            new(true, true);

        public static 방향광표현대상View? AttachIfSupported(
            GameObject instance,
            string placementStableId,
            string compositionKey,
            string categoryCode,
            string? profileStableId = null)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (!TryResolve(compositionKey, categoryCode, out var policy))
                return null;
            var view = instance.GetComponent<방향광표현대상View>();
            view ??= instance.AddComponent<방향광표현대상View>();
            var existingShadowPolicy =
                instance.GetComponent<자연경관ShadowPolicyView>();
            view.Configure(placementStableId, compositionKey,
                profileStableId ?? World방향광표현Catalog.DefaultProfileStableId,
                policy, applyRendererPolicy: existingShadowPolicy == null);
            return view;
        }

        public static bool TryResolve(string compositionKey,
            string categoryCode, out 방향광표현TargetPolicy policy)
        {
            var categoryMatched = categoryCode == "NatureResourceNode"
                                  || categoryCode == "NatureDroppedTimber"
                                  || categoryCode == "NatureCabin"
                                  || categoryCode == "NatureWorkbench";
            var compositionMatched = compositionKey == "Nature.Tree.Standing"
                                     || compositionKey == "Nature.Tree.Stump"
                                     || compositionKey == "Nature.Timber.Dropped"
                                     || compositionKey == "Nature.Cabin.Completed"
                                     || compositionKey == "Nature.Cabin.Construction"
                                     || compositionKey == "Nature.Workbench.Operational"
                                     || compositionKey == "Nature.Workbench.Construction";
            policy = CastReceive;
            return categoryMatched || compositionMatched;
        }
    }

    public sealed class 방향광표현ValidationReceipt
    {
        public string CellStableId { get; set; } = string.Empty;
        public string PlacementStableId { get; set; } = string.Empty;
        public string ProfileStableId { get; set; } = string.Empty;
        public string RuleRevision { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string[] FailureCodes { get; set; } = Array.Empty<string>();
    }

    [DisallowMultipleComponent]
    public sealed class 방향광표현대상View : MonoBehaviour
    {
        [SerializeField] private string placementStableId = string.Empty;
        [SerializeField] private string compositionKey = string.Empty;
        [SerializeField] private string lightingProfileKey = string.Empty;
        [SerializeField] private bool castShadows;
        [SerializeField] private bool receiveShadows = true;

        public string PlacementStableId => placementStableId;
        public string LightingProfileKey => lightingProfileKey;

        public void Configure(string stableId, string sourceCompositionKey,
            string profileStableId, 방향광표현TargetPolicy policy,
            bool applyRendererPolicy = true)
        {
            placementStableId = stableId ?? string.Empty;
            compositionKey = sourceCompositionKey ?? string.Empty;
            lightingProfileKey = profileStableId ?? string.Empty;
            castShadows = policy.CastShadows;
            receiveShadows = policy.ReceiveShadows;
            if (!applyRendererPolicy) return;
            foreach (var renderer in LitRenderers())
            {
                renderer.shadowCastingMode = castShadows
                    ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = receiveShadows;
            }
        }

        public 방향광표현ValidationReceipt Validate(string cellStableId)
        {
            var failures = new List<string>();
            var profile = World방향광표현Catalog.Resolve(lightingProfileKey,
                out var profileWasResolved);
            if (!profileWasResolved)
                failures.Add(방향광표현FailureCodes.ProfileUnknown);
            var renderers = LitRenderers();
            if (renderers.Length == 0)
                failures.Add(방향광표현FailureCodes.TargetMissing);
            foreach (var renderer in renderers)
            {
                if (!HasValidNormals(renderer))
                    AddOnce(failures,
                        방향광표현FailureCodes.SurfaceNormalMissing);
                if (!HasCompatibleLitMaterials(renderer))
                    AddOnce(failures,
                        방향광표현FailureCodes.LitShaderUnsupported);
                if (renderer.shadowCastingMode != (castShadows
                        ? ShadowCastingMode.On : ShadowCastingMode.Off)
                    || renderer.receiveShadows != receiveShadows)
                    AddOnce(failures,
                        방향광표현FailureCodes.ShadowPolicyMismatch);
            }
            return new 방향광표현ValidationReceipt
            {
                CellStableId = cellStableId ?? string.Empty,
                PlacementStableId = placementStableId,
                ProfileStableId = profile.StableId,
                RuleRevision = World방향광표현Catalog.RuleRevision,
                Passed = failures.Count == 0,
                FailureCodes = failures.ToArray(),
            };
        }

        private Renderer[] LitRenderers()
            => GetComponentsInChildren<Renderer>(true)
                .Where(value => value is MeshRenderer
                                || value is SkinnedMeshRenderer)
                .ToArray();

        private static bool HasValidNormals(Renderer renderer)
        {
            var mesh = renderer switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                MeshRenderer meshRenderer => meshRenderer
                    .GetComponent<MeshFilter>()?.sharedMesh,
                _ => null,
            };
            return mesh != null && mesh.vertexCount > 0
                   && mesh.HasVertexAttribute(VertexAttribute.Normal);
        }

        private static bool HasCompatibleLitMaterials(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0) return false;
            foreach (var material in materials)
            {
                if (material == null || material.shader == null) return false;
                var shaderName = material.shader.name ?? string.Empty;
                if (shaderName == "Hidden/InternalErrorShader"
                    || shaderName.IndexOf("Unlit",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || shaderName.IndexOf("Particle",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
            return true;
        }

        private static void AddOnce(ICollection<string> failures,
            string failureCode)
        {
            if (!failures.Contains(failureCode)) failures.Add(failureCode);
        }
    }

    /// <summary>
    /// 방향광 검증은 표현 E6 증거만 판정하며 LH 통행이나 Simulation 상태를 바꾸지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E6,
        "표면 법선·방향광·Lit 재질·그림자 정책의 판독 가능성을 검증한다.",
        Boundary = "검증 실패는 표현 승격만 차단하며 셀 통행과 권위 Revision을 변경하지 않는다.")]
    public sealed class 방향광표현Controller : MonoBehaviour
    {
        private readonly Dictionary<string,
            방향광표현ValidationReceipt[]> receipts =
                new(StringComparer.Ordinal);

        public bool ValidateCell(string cellStableId, Transform cellRoot,
            GameObject? floor, string? profileStableId,
            bool lightingTargetRequired)
        {
            if (cellRoot == null) throw new ArgumentNullException(nameof(cellRoot));
            var profileKey = string.IsNullOrWhiteSpace(profileStableId)
                ? World방향광표현Catalog.DefaultProfileStableId
                : profileStableId;
            if (floor != null && floor.activeSelf)
            {
                var floorView = floor.GetComponent<방향광표현대상View>();
                floorView ??= floor.AddComponent<방향광표현대상View>();
                floorView.Configure("ground-surface:" + cellStableId,
                    "World.Ground.Dynamic", profileKey,
                    World방향광표현TargetCatalog.GroundSurface);
            }
            var values = cellRoot
                .GetComponentsInChildren<방향광표현대상View>(true)
                .Select(value => value.Validate(cellStableId))
                .OrderBy(value => value.PlacementStableId,
                    StringComparer.Ordinal)
                .ToList();
            var hasNatureCoreTarget = values.Any(value =>
                !value.PlacementStableId.StartsWith("ground-surface:",
                    StringComparison.Ordinal));
            if (lightingTargetRequired && !hasNatureCoreTarget)
            {
                values.Add(new 방향광표현ValidationReceipt
                {
                    CellStableId = cellStableId ?? string.Empty,
                    PlacementStableId = "required-target:" + cellStableId,
                    ProfileStableId = profileKey,
                    RuleRevision = World방향광표현Catalog.RuleRevision,
                    Passed = false,
                    FailureCodes = new[]
                    {
                        방향광표현FailureCodes.TargetMissing,
                    },
                });
            }
            receipts[cellStableId ?? string.Empty] = values.ToArray();
            return values.All(value => value.Passed);
        }

        public 방향광표현ValidationReceipt[] ReceiptsOf(string cellStableId)
            => receipts.TryGetValue(cellStableId ?? string.Empty,
                out var values)
                ? values.Select(Clone).ToArray()
                : Array.Empty<방향광표현ValidationReceipt>();

        public void ReleaseCell(string cellStableId)
            => receipts.Remove(cellStableId ?? string.Empty);

        private static 방향광표현ValidationReceipt Clone(
            방향광표현ValidationReceipt value) => new()
        {
            CellStableId = value.CellStableId,
            PlacementStableId = value.PlacementStableId,
            ProfileStableId = value.ProfileStableId,
            RuleRevision = value.RuleRevision,
            Passed = value.Passed,
            FailureCodes = value.FailureCodes.ToArray(),
        };
    }
}
