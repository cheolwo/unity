using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>현재 그려진 건물의 정확 ID와 판본에만 외관 부품을 결속한다. 별칭·근접 매칭은 하지 않는다.</summary>
    public sealed class 사가정외관검토Gate
    {
        private readonly Dictionary<string, Bounds> targets = new(StringComparer.Ordinal);
        private readonly string spatialHash;
        private readonly string baseHash = 사가정공간표현OverlayComposer.BaseMapHash;
        private readonly string scopeKind = 사가정건물외관Profile.CanonicalScope;
        public const float VisualMarginMeters = 2f;

        public 사가정외관검토Gate(공간참고자료 map, 사가정공간표현Composition composition,
            건물높이보완조회 heights)
        {
            if (map == null || composition == null || !ReferenceEquals(map, composition.BaseMap))
                throw new ArgumentException("AppearanceSpatialContextRequired");
            spatialHash = composition.ContentHash;
            foreach (var baseline in map.buildings ?? Array.Empty<참고건물>())
            {
                if (composition.OverlayApplied
                    && composition.TryResolveLegacyBuilding(baseline.id, out _)) continue;
                Add(baseline.id, baseline.points.Select(p => new Vector2(p.x, p.z)),
                    heights?.Resolve(baseline) ?? baseline.height);
            }
            if (composition.OverlayApplied)
                foreach (var building in composition.Buildings)
                    Add(building.Id, building.Parts.SelectMany(p => p.Outer)
                        .Select(p => new Vector2((float)p.X, (float)p.Z)), (float)building.HeightMeters);
        }

        private 사가정외관검토Gate(string studyHash, IReadOnlyDictionary<string, Bounds> exactTargets)
        {
            if (!Sha256(studyHash) || exactTargets == null || exactTargets.Count is < 1 or > 3
                || exactTargets.Any(p => !ReferenceId(p.Key) || !Finite(p.Value.center)
                    || !Finite(p.Value.size) || p.Value.size.x <= 0f || p.Value.size.y <= 0f || p.Value.size.z <= 0f))
                throw new ArgumentException("AppearancePrivateStudyContextRejected");
            scopeKind = 사가정건물외관Profile.PrivateStudyScope;
            baseHash = spatialHash = studyHash;
            foreach (var pair in exactTargets) targets.Add(pair.Key, pair.Value);
        }

        /// <summary>연구의 정확 형상 ID·묶음 hash·Bounds만 소비한다. 정본 View에 적용할 수 있는 context가 아니다.</summary>
        public static 사가정외관검토Gate CreatePrivateStudy(string studyHash,
            IReadOnlyDictionary<string, Bounds> exactTargets) => new(studyHash, exactTargets);

        public string Validate(사가정건물외관Profile profile, 법정동경관VisualCatalog catalog,
            IReadOnlyList<사가정외관ModuleReview> modules, out Bounds targetBounds)
        {
            var error = ValidateProfile(profile, out targetBounds);
            if (!string.IsNullOrEmpty(error)) return error;
            if (catalog == null || profile.visualCatalogRevision != catalog.CatalogRevision)
                return "AppearanceCatalogVersionMismatch";
            var matches = modules?.Where(m => m != null && m.visualKey == profile.visualKey
                && m.catalogRevision == catalog.CatalogRevision).ToArray();
            if (matches == null || matches.Length != 1 || matches[0].moduleKind != profile.moduleKind
                || !Reviewer(matches[0].reviewerKind) || !ReferenceId(matches[0].reviewReference))
                return "AppearanceModuleReviewRequired";
            try
            {
                catalog.Validate();
                var entry = catalog.Resolve(profile.visualKey);
                if (!entry.PresentationOnly || entry.CompositionRoleCode != 법정동경관CompositionRoleCodes.Detail
                    || entry.Prefab == null) return "AppearanceModuleAssetRejected";
            }
            catch (InvalidOperationException) { return "AppearanceModuleAssetRejected"; }
            return string.Empty;
        }

        public string Validate(사가정건물외관Profile profile, 사가정외관ModuleCatalog catalog,
            out Bounds targetBounds)
        {
            var error = ValidateProfile(profile, out targetBounds);
            if (!string.IsNullOrEmpty(error)) return error;
            if (catalog == null || profile.visualCatalogRevision != catalog.CatalogRevision)
                return "AppearanceCatalogVersionMismatch";
            try
            {
                catalog.Validate();
                var entry = catalog.Resolve(profile.visualKey);
                if (entry.moduleKind != profile.moduleKind
                    || entry.reviewState != 사가정건물외관Profile.ReviewedState
                    || !Reviewer(entry.reviewerKind) || !ReferenceId(entry.reviewReference))
                    return "AppearanceModuleReviewRequired";
            }
            catch (InvalidOperationException) { return "AppearanceModuleAssetRejected"; }
            return string.Empty;
        }

        private string ValidateProfile(사가정건물외관Profile profile, out Bounds targetBounds)
        {
            targetBounds = default;
            if (profile == null || profile.schemaVersion != 사가정건물외관Profile.SchemaVersion)
                return "AppearanceSchemaRejected";
            if (profile.scopeKind != scopeKind) return "AppearanceScopeRejected";
            if (profile.reviewState != 사가정건물외관Profile.ReviewedState
                || !Reviewer(profile.reviewerKind) || !ReferenceId(profile.reviewReference))
                return "AppearancePrivateReviewRequired";
            if (!profile.presentationOnly || profile.distributionApproved)
                return "AppearanceAuthorityRejected";
            if (!ReferenceId(profile.profileStableId) || !ReferenceId(profile.targetBuildingId)
                || !ReferenceId(profile.evidenceObservationId) || !Sha256(profile.evidenceSourceSha256)
                || profile.evidenceSourcePts < 0 || profile.evidenceTimeBaseNumerator <= 0
                || profile.evidenceTimeBaseDenominator <= 0)
                return "AppearanceObservationRequired";
            if (!string.Equals(profile.baseMapHashSha256, baseHash,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(profile.spatialContentHashSha256, spatialHash,
                    StringComparison.OrdinalIgnoreCase)) return "AppearanceSpatialVersionMismatch";
            if (!targets.TryGetValue(profile.targetBuildingId, out targetBounds))
                return "AppearanceExactBuildingRequired";
            if (!ModuleKind(profile.moduleKind) || WholeBuilding(profile.visualKey))
                return "AppearanceModuleKindRejected";
            if (!Finite(profile.localPosition) || !Finite(profile.localEulerAngles)
                || !Finite(profile.localScale) || profile.localScale.x <= 0f
                || profile.localScale.y <= 0f || profile.localScale.z <= 0f
                || profile.localScale.x > 10f || profile.localScale.y > 10f || profile.localScale.z > 10f)
                return "AppearanceTransformRejected";
            targetBounds.Expand(VisualMarginMeters * 2f);
            if (!targetBounds.Contains(profile.localPosition)) return "AppearancePlacementOutsideBuilding";
            return string.Empty;
        }

        private void Add(string id, IEnumerable<Vector2> source, float height)
        {
            var points = source.ToArray();
            if (points.Length < 3 || !float.IsFinite(height) || height <= 0f
                || points.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y))) return;
            var min = new Vector3(points.Min(p => p.x), 사가정모형MeshBuilder.BuildingBaseElevation,
                points.Min(p => p.y));
            var max = new Vector3(points.Max(p => p.x), min.y + Mathf.Max(2f, height), points.Max(p => p.y));
            if (!targets.TryAdd(id, new Bounds((min + max) * .5f, max - min)))
                throw new ArgumentException("AppearanceBuildingIdentityAmbiguous");
        }

        internal static bool Finite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        internal static bool Reviewer(string value) => value is "CodexVisualReview" or "HumanVisualReview";
        internal static bool ModuleKind(string value) => value is "Facade" or "Sign" or "Awning";
        private static bool WholeBuilding(string key)
            => !string.IsNullOrEmpty(key) && key.Contains(".building.", StringComparison.Ordinal);
        internal static bool ReferenceId(string value)
            => !string.IsNullOrWhiteSpace(value) && value.Length <= 200
                && !value.Any(char.IsControl) && !value.Contains('/') && !value.Contains('\\');
        internal static bool Sha256(string value)
            => value != null && value.Length == 64 && value.All(c => c is >= '0' and <= '9'
                or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    }
}
