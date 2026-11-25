using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>기존 정확 건물 ID와 전용 간략 모형만 연결한다. 공유 지도 chunk를 전달하지 않는다.</summary>
    public sealed class 사가정상세건물표현Target
    {
        public string BuildingId { get; }
        public Bounds LocalBounds { get; }
        public Transform FallbackRoot { get; }
        public 사가정상세건물표현Target(string buildingId, Bounds localBounds, Transform fallbackRoot)
        { BuildingId = buildingId; LocalBounds = localBounds; FallbackRoot = fallbackRoot; }
    }

    /// <summary>새 자산 원장이 아니라 기존 visualKey/판본에 결속한 이번 연구의 두 정적 입력이다.</summary>
    public sealed class 사가정상세건물표현Asset
    {
        public string VisualKey { get; }
        public string CatalogRevision { get; }
        public GameObject DetailedSource { get; }
        public GameObject MiddleSource { get; }
        public 사가정상세건물표현Asset(string visualKey, string catalogRevision,
            GameObject detailedSource, GameObject middleSource)
        { VisualKey = visualKey; CatalogRevision = catalogRevision; DetailedSource = detailedSource; MiddleSource = middleSource; }
    }

    public enum 사가정상세건물표현Level { Fallback, Middle, Detailed }

    [Serializable]
    public sealed class 사가정상세건물표현Observation
    {
        public string buildingId, profileStableId, visualKey, catalogRevision, level;
        public float observerDistanceMeters = -1;
        public bool fallbackActive, middleActive, detailedActive;
        public bool presentationOnly = true, canonicalApplied, measuredDimensionsClaimed;
    }

    /// <summary>
    /// 비공개 연구의 건물별 상세 표현과 거리 전환을 소유한다. 기존 소형 외관 부품 Renderer의 제한을 풀지 않는다.
    /// source의 Mesh/Material은 빌린 참조이며 스크립트·Collider·LODGroup을 실행/복제하지 않는다.
    /// 호출자는 이 객체를 먼저 Dispose한 다음 source 모델의 소유 자원을 해제해야 한다.
    /// </summary>
    public sealed class 사가정상세건물표현Renderer : IDisposable
    {
        public const string ModuleKind = "DetailedBuilding";
        public const int MaximumStudyTargets = 3;
        public const int MaximumSourceTransforms = 512;
        public const int MaximumSourceRenderers = 128;
        public const int MaximumSourceVertices = 600000;
        private sealed class TargetState
        {
            public 사가정상세건물표현Target Target;
            public bool OriginalActive;
        }
        private sealed class Applied
        {
            public TargetState Target;
            public string ProfileId, VisualKey, CatalogRevision;
            public GameObject Root, Detailed, Middle;
            public 사가정상세건물표현Level Level;
            public float Distance = -1;
        }
        private readonly string studyHash;
        private readonly int layer;
        private readonly float detailedDistance, middleDistance;
        private readonly Dictionary<string, TargetState> targets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Applied> applied = new(StringComparer.Ordinal);
        private bool disposed, hasObserver;
        private Vector3 observerWorld;
        public Transform VisualRoot { get; }
        public int Count => applied.Count;
        public string StatusCode { get; private set; } = "NoPrivateDetailedBuilding";

        public 사가정상세건물표현Renderer(Transform parent, string studyHash,
            IReadOnlyDictionary<string, 사가정상세건물표현Target> exactTargets, int layer,
            float detailedDistance = 50f, float middleDistance = 140f)
        {
            if (parent == null || !사가정외관검토Gate.Sha256(studyHash) || exactTargets == null
                || exactTargets.Count is < 1 or > MaximumStudyTargets || layer is < 0 or > 31
                || !float.IsFinite(detailedDistance) || !float.IsFinite(middleDistance)
                || detailedDistance <= 0 || middleDistance <= detailedDistance)
                throw new ArgumentException("DetailedBuildingPrivateContextRequired");
            foreach (var pair in exactTargets)
            {
                var target = pair.Value;
                if (!사가정외관검토Gate.ReferenceId(pair.Key) || target == null || pair.Key != target.BuildingId
                    || target.FallbackRoot == null || target.FallbackRoot == parent || !target.FallbackRoot.IsChildOf(parent)
                    || !사가정외관검토Gate.Finite(target.LocalBounds.center)
                    || !사가정외관검토Gate.Finite(target.LocalBounds.size)
                    || target.LocalBounds.size.x <= 0 || target.LocalBounds.size.y <= 0 || target.LocalBounds.size.z <= 0)
                    throw new ArgumentException("DetailedBuildingExactDedicatedTargetRequired");
                if (targets.Values.Any(existing => existing.Target.FallbackRoot == target.FallbackRoot
                    || existing.Target.FallbackRoot.IsChildOf(target.FallbackRoot)
                    || target.FallbackRoot.IsChildOf(existing.Target.FallbackRoot)))
                    throw new ArgumentException("DetailedBuildingSharedFallbackRejected");
                targets.Add(pair.Key, new TargetState { Target = target, OriginalActive = target.FallbackRoot.gameObject.activeSelf });
            }
            this.studyHash = studyHash; this.layer = layer;
            this.detailedDistance = detailedDistance; this.middleDistance = middleDistance;
            var root = new GameObject("비공개 상세 건물 표현 · 지도 권위 미변경")
                { hideFlags = HideFlags.DontSave, layer = layer };
            root.transform.SetParent(parent, false); VisualRoot = root.transform;
        }

        public bool TryApply(사가정건물외관Profile profile, 사가정상세건물표현Asset asset, out string statusCode)
        {
            var error = Validate(profile, asset, out var target);
            if (!string.IsNullOrEmpty(error)) return Reject(error, out statusCode);
            GameObject staging = null;
            try
            {
                staging = Child(VisualRoot, "상세 시안 · " + profile.targetBuildingId);
                staging.SetActive(false); staging.transform.localPosition = profile.localPosition;
                staging.transform.localEulerAngles = profile.localEulerAngles; staging.transform.localScale = profile.localScale;
                var detailed = Child(staging.transform, "가까이 · 상세 외형");
                var middle = Child(staging.transform, "중간 · 단순 외형");
                CopySource(asset.DetailedSource, detailed.transform, target.Target.LocalBounds);
                CopySource(asset.MiddleSource, middle.transform, target.Target.LocalBounds);
                var next = new Applied { Target = target, ProfileId = profile.profileStableId,
                    VisualKey = profile.visualKey, CatalogRevision = profile.visualCatalogRevision,
                    Root = staging, Detailed = detailed, Middle = middle };
                // 두 입력의 정적 사본과 범위 검사를 끝낸 다음에만 이전 표현을 치환한다.
                RemoveBuilding(profile.targetBuildingId);
                applied.Add(profile.targetBuildingId, next);
                staging.SetActive(true);
                SetLevel(next, hasObserver ? LevelFor(next, observerWorld) : 사가정상세건물표현Level.Detailed);
                statusCode = StatusCode = "PrivateDetailedBuildingApplied";
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnityException)
            {
                if (staging != null) Release(staging);
                // 유효한 정확 대상의 새 자산이 실패했으면 오래된 상세 표현 대신 원래 간략 모형으로 돌아온다.
                RemoveBuilding(profile.targetBuildingId);
                RestoreFallback(target);
                return Reject("DetailedBuildingStaticAssetRejected", out statusCode);
            }
        }

        public int RefreshForObserver(Vector3 worldPosition)
        {
            if (disposed) return 0;
            if (VisualRoot == null) { ReleaseLostContext(); return 0; }
            if (!사가정외관검토Gate.Finite(worldPosition)) throw new ArgumentException("DetailedBuildingObserverInvalid");
            hasObserver = true; observerWorld = worldPosition;
            var changes = 0;
            foreach (var value in applied.Values)
            {
                var next = LevelFor(value, worldPosition);
                if (next != value.Level) changes++;
                SetLevel(value, next);
            }
            return changes;
        }

        public 사가정상세건물표현Observation[] Snapshot() => applied.Values.Select(value => new 사가정상세건물표현Observation
        {
            buildingId = value.Target.Target.BuildingId, profileStableId = value.ProfileId,
            visualKey = value.VisualKey, catalogRevision = value.CatalogRevision, level = value.Level.ToString(),
            observerDistanceMeters = value.Distance,
            fallbackActive = value.Target.Target.FallbackRoot != null && value.Target.Target.FallbackRoot.gameObject.activeSelf,
            middleActive = value.Middle != null && value.Middle.activeSelf,
            detailedActive = value.Detailed != null && value.Detailed.activeSelf
        }).ToArray();

        public bool RemoveBuilding(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId) || !applied.TryGetValue(buildingId, out var value)) return false;
            applied.Remove(buildingId); Release(value.Root); RestoreFallback(value.Target);
            StatusCode = applied.Count == 0 ? "NoPrivateDetailedBuilding" : "PrivateDetailedBuildingApplied";
            return true;
        }
        public void Clear()
        {
            foreach (var id in applied.Keys.ToArray()) RemoveBuilding(id);
            if (!disposed) StatusCode = "NoPrivateDetailedBuilding";
        }
        public void Dispose()
        {
            if (disposed) return;
            Clear(); disposed = true;
            if (VisualRoot != null) Release(VisualRoot.gameObject);
            StatusCode = "DetailedBuildingRendererReleased";
        }

        private string Validate(사가정건물외관Profile profile, 사가정상세건물표현Asset asset, out TargetState target)
        {
            target = null;
            if (disposed) return "DetailedBuildingRendererReleased";
            if (VisualRoot == null) { ReleaseLostContext(); return "DetailedBuildingRendererReleased"; }
            if (profile == null || profile.schemaVersion != 사가정건물외관Profile.SchemaVersion
                || profile.scopeKind != 사가정건물외관Profile.PrivateStudyScope) return "DetailedBuildingPrivateScopeRequired";
            if (!profile.presentationOnly || profile.distributionApproved) return "DetailedBuildingAuthorityRejected";
            if (profile.reviewState != 사가정건물외관Profile.ReviewedState
                || !사가정외관검토Gate.Reviewer(profile.reviewerKind)
                || !사가정외관검토Gate.ReferenceId(profile.reviewReference)) return "DetailedBuildingReviewRequired";
            if (!사가정외관검토Gate.ReferenceId(profile.profileStableId)
                || !사가정외관검토Gate.ReferenceId(profile.evidenceObservationId)
                || !사가정외관검토Gate.Sha256(profile.evidenceSourceSha256)
                || profile.evidenceSourcePts < 0 || profile.evidenceTimeBaseNumerator <= 0
                || profile.evidenceTimeBaseDenominator <= 0) return "DetailedBuildingObservationRequired";
            if (!string.Equals(profile.baseMapHashSha256, studyHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(profile.spatialContentHashSha256, studyHash, StringComparison.OrdinalIgnoreCase))
                return "DetailedBuildingSpatialVersionMismatch";
            if (!targets.TryGetValue(profile.targetBuildingId ?? string.Empty, out target)
                || target.Target.FallbackRoot == null) return "DetailedBuildingExactTargetRequired";
            var resolvedTarget = target;
            if (applied.Values.Any(v => v.ProfileId == profile.profileStableId && v.Target != resolvedTarget))
                return "DetailedBuildingProfileTargetChanged";
            if (profile.moduleKind != ModuleKind || asset == null
                || !사가정외관검토Gate.ReferenceId(asset.VisualKey)
                || !사가정외관검토Gate.ReferenceId(asset.CatalogRevision)
                || profile.visualKey != asset.VisualKey || profile.visualCatalogRevision != asset.CatalogRevision)
                return "DetailedBuildingVisualVersionMismatch";
            if (!사가정외관검토Gate.Finite(profile.localPosition) || !사가정외관검토Gate.Finite(profile.localEulerAngles)
                || !사가정외관검토Gate.Finite(profile.localScale) || profile.localScale.x <= 0
                || profile.localScale.y <= 0 || profile.localScale.z <= 0
                || profile.localScale.x > 10 || profile.localScale.y > 10 || profile.localScale.z > 10)
                return "DetailedBuildingPlacementRejected";
            return string.Empty;
        }

        private 사가정상세건물표현Level LevelFor(Applied value, Vector3 worldPosition)
        {
            var point = VisualRoot.InverseTransformPoint(worldPosition);
            value.Distance = Vector3.Distance(point, value.Target.Target.LocalBounds.ClosestPoint(point));
            if (value.Detailed == null || value.Middle == null || value.Target.Target.FallbackRoot == null)
                return 사가정상세건물표현Level.Fallback;
            return value.Distance <= detailedDistance ? 사가정상세건물표현Level.Detailed
                : value.Distance <= middleDistance ? 사가정상세건물표현Level.Middle : 사가정상세건물표현Level.Fallback;
        }
        private static void SetLevel(Applied value, 사가정상세건물표현Level level)
        {
            // 먼저 둘을 내리고 하나만 켜므로 이전/새 외형이 같은 프레임에 겹치지 않는다.
            if (value.Detailed != null) value.Detailed.SetActive(false);
            if (value.Middle != null) value.Middle.SetActive(false);
            if (value.Target.Target.FallbackRoot != null) value.Target.Target.FallbackRoot.gameObject.SetActive(false);
            if (level == 사가정상세건물표현Level.Detailed && value.Detailed != null) value.Detailed.SetActive(true);
            else if (level == 사가정상세건물표현Level.Middle && value.Middle != null) value.Middle.SetActive(true);
            else { level = 사가정상세건물표현Level.Fallback; RestoreFallback(value.Target); }
            value.Level = level;
        }
        private void CopySource(GameObject source, Transform parent, Bounds localBounds)
        {
            if (source == null || source.transform == VisualRoot || source.transform.IsChildOf(VisualRoot))
                throw new ArgumentException("DetailedBuildingExternalStaticSourceRequired");
            var transforms = source.GetComponentsInChildren<Transform>(true);
            var renderers = source.GetComponentsInChildren<Renderer>(true);
            if (transforms.Length > MaximumSourceTransforms || renderers.Length > MaximumSourceRenderers
                || renderers.Any(r => r is not MeshRenderer) || source.GetComponentsInChildren<LODGroup>(true).Length != 0)
                throw new ArgumentException("DetailedBuildingSourceBudgetOrControllerRejected");
            var vertices = source.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null)
                .Sum(f => (long)f.sharedMesh.vertexCount);
            if (vertices > MaximumSourceVertices) throw new ArgumentException("DetailedBuildingSourceVertexBudgetExceeded");
            localBounds.Expand(사가정외관검토Gate.VisualMarginMeters * 2);
            if (CopyTree(source.transform, parent, localBounds, true, true) == 0)
                throw new ArgumentException("DetailedBuildingStaticGeometryRequired");
        }
        private int CopyTree(Transform source, Transform parent, Bounds allowed, bool activeAncestors, bool sourceRoot)
        {
            if (!사가정외관검토Gate.Finite(source.localPosition) || !사가정외관검토Gate.Finite(source.localEulerAngles)
                || !사가정외관검토Gate.Finite(source.localScale)) throw new ArgumentException("DetailedBuildingSourceTransformInvalid");
            var target = Child(parent, source.name);
            target.transform.localPosition = source.localPosition; target.transform.localRotation = source.localRotation;
            target.transform.localScale = source.localScale;
            var sourceActive = sourceRoot || source.gameObject.activeSelf;
            target.SetActive(sourceActive); var active = activeAncestors && sourceActive;
            var original = source.GetComponent<MeshRenderer>(); var filter = source.GetComponent<MeshFilter>(); var count = 0;
            if (active && original != null && original.enabled)
            {
                if (filter == null || filter.sharedMesh == null || original.sharedMaterials.Length == 0
                    || original.sharedMaterials.Any(m => m == null)) throw new ArgumentException("DetailedBuildingSourceMeshMissing");
                var matrix = VisualRoot.worldToLocalMatrix * target.transform.localToWorldMatrix;
                var bounds = filter.sharedMesh.bounds;
                for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
                    if (!allowed.Contains(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x,y,z)))))
                        throw new ArgumentException("DetailedBuildingGeometryOutsideExactTarget");
                target.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = target.AddComponent<MeshRenderer>(); copy.sharedMaterials = original.sharedMaterials;
                copy.shadowCastingMode = original.shadowCastingMode; copy.receiveShadows = original.receiveShadows;
                copy.reflectionProbeUsage = original.reflectionProbeUsage;
                // 관측 차양색처럼 Renderer 단위로 설정한 값을 재질 원본 변경 없이 보존한다.
                if (original.HasPropertyBlock())
                {
                    var block = new MaterialPropertyBlock(); original.GetPropertyBlock(block);
                    if (!block.isEmpty) copy.SetPropertyBlock(block);
                    for (var i = 0; i < original.sharedMaterials.Length; i++)
                    {
                        block.Clear(); original.GetPropertyBlock(block, i);
                        if (!block.isEmpty) copy.SetPropertyBlock(block, i);
                    }
                }
                count++;
            }
            foreach (Transform child in source) count += CopyTree(child, target.transform, allowed, active, false);
            return count;
        }
        private GameObject Child(Transform parent, string name)
        {
            var value = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = layer };
            value.transform.SetParent(parent, false); return value;
        }
        private static void RestoreFallback(TargetState target)
        { if (target.Target.FallbackRoot != null) target.Target.FallbackRoot.gameObject.SetActive(target.OriginalActive); }
        private void ReleaseLostContext()
        { Clear(); disposed = true; StatusCode = "DetailedBuildingRendererReleased"; }
        private bool Reject(string code, out string result) { result = StatusCode = code; return false; }
        private static void Release(GameObject value)
        {
            if (value == null) return;
            value.SetActive(false);
            if (UnityEngine.Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
