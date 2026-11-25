using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 검토된 정적 외관 부품만 추가한다. 원본 매스·선택·Collider·업무 상태를 변경하지 않으며
    /// Prefab의 스크립트나 동작을 실행하지 않고 Mesh/Material 참조와 Transform만 복사한다.
    /// </summary>
    public sealed class 사가정외관OverlayRenderer : IDisposable
    {
        private sealed class Applied
        {
            public string BuildingId;
            public GameObject Root;
        }

        private readonly 사가정외관검토Gate gate;
        private readonly int layer;
        private readonly Dictionary<string, Applied> applied = new(StringComparer.Ordinal);
        public Transform VisualRoot { get; }
        public int Count => applied.Count;
        public string StatusCode { get; private set; } = "NoReviewedAppearance";

        public 사가정외관OverlayRenderer(Transform parent, 사가정외관검토Gate gate, int layer)
        {
            if (parent == null || gate == null || layer is < 0 or > 31)
                throw new ArgumentException("AppearanceRendererContextRequired");
            this.gate = gate;
            this.layer = layer;
            var root = new GameObject("검토 외관 VisualRoot · 비공개 표현 전용")
                { hideFlags = HideFlags.DontSave, layer = layer };
            root.transform.SetParent(parent, false);
            VisualRoot = root.transform;
        }

        public bool TryApply(사가정건물외관Profile profile, 법정동경관VisualCatalog catalog,
            IReadOnlyList<사가정외관ModuleReview> modules, out string statusCode)
        {
            if (VisualRoot == null) return Reject("AppearanceRendererReleased", out statusCode);
            var error = gate.Validate(profile, catalog, modules, out var bounds);
            if (!string.IsNullOrEmpty(error)) return Reject(error, out statusCode);
            return ApplyValidated(profile, catalog.Resolve(profile.visualKey).Prefab, bounds, out statusCode);
        }

        public bool TryApply(사가정건물외관Profile profile, 사가정외관ModuleCatalog catalog,
            out string statusCode)
        {
            if (VisualRoot == null) return Reject("AppearanceRendererReleased", out statusCode);
            var error = gate.Validate(profile, catalog, out var bounds);
            if (!string.IsNullOrEmpty(error)) return Reject(error, out statusCode);
            return ApplyValidated(profile, catalog.ResolvePrefab(profile.visualKey), bounds, out statusCode);
        }

        private bool ApplyValidated(사가정건물외관Profile profile, GameObject prefab,
            Bounds bounds, out string statusCode)
        {
            if (applied.TryGetValue(profile.profileStableId, out var previous)
                && previous.BuildingId != profile.targetBuildingId)
                return Reject("AppearanceProfileTargetChanged", out statusCode);
            if (previous == null && applied.Count >= 64)
                return Reject("AppearanceModuleBudgetExceeded", out statusCode);
            GameObject staging = null;
            try
            {
                // LOD 군이나 동적 Renderer를 무시해 중복·왜곡된 외관을 만드는 대신 보류한다.
                if (prefab.GetComponentsInChildren<LODGroup>(true).Length != 0
                    || prefab.GetComponentsInChildren<Transform>(true).Length > 128
                    || prefab.GetComponentsInChildren<Renderer>(true).Length > 32
                    || prefab.GetComponentsInChildren<Renderer>(true).Any(r => r is not MeshRenderer))
                    return Reject("AppearanceStaticModuleRequired", out statusCode);
                staging = new GameObject("검토 외관 · " + profile.profileStableId)
                    { hideFlags = HideFlags.DontSave, layer = layer };
                staging.SetActive(false);
                staging.transform.SetParent(VisualRoot, false);
                staging.transform.localPosition = profile.localPosition;
                staging.transform.localEulerAngles = profile.localEulerAngles;
                staging.transform.localScale = profile.localScale;
                var count = CopyStaticTree(prefab.transform, staging.transform, VisualRoot, bounds, true);
                if (count == 0) throw new ArgumentException("AppearanceStaticMeshRequired");
                if (previous != null) Release(previous.Root);
                applied[profile.profileStableId] = new Applied
                    { BuildingId = profile.targetBuildingId, Root = staging };
                staging.SetActive(true);
                statusCode = StatusCode = "ReviewedPrivateAppearanceApplied";
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException
                or InvalidOperationException or UnityException)
            {
                if (staging != null) Release(staging);
                return Reject("AppearanceStaticGeometryRejected", out statusCode);
            }
        }

        public bool Remove(string profileStableId)
        {
            if (string.IsNullOrEmpty(profileStableId) || !applied.TryGetValue(profileStableId, out var value))
                return false;
            applied.Remove(profileStableId);
            Release(value.Root);
            StatusCode = applied.Count == 0 ? "NoReviewedAppearance" : "ReviewedPrivateAppearanceApplied";
            return true;
        }

        public void Clear()
        {
            foreach (var value in applied.Values) Release(value.Root);
            applied.Clear();
            StatusCode = "NoReviewedAppearance";
        }

        public int RemoveBuilding(string buildingId)
        {
            var ids = applied.Where(p => p.Value.BuildingId == buildingId).Select(p => p.Key).ToArray();
            foreach (var id in ids) Remove(id);
            return ids.Length;
        }

        public void Dispose()
        {
            Clear();
            if (VisualRoot != null) Release(VisualRoot.gameObject);
            StatusCode = "AppearanceRendererReleased";
        }

        private int CopyStaticTree(Transform source, Transform parent, Transform coordinateRoot,
            Bounds bounds, bool ancestorsActive)
        {
            if (!사가정외관검토Gate.Finite(source.localPosition)
                || !사가정외관검토Gate.Finite(source.localEulerAngles)
                || !사가정외관검토Gate.Finite(source.localScale))
                throw new ArgumentException("AppearanceSourceTransformInvalid");
            var target = new GameObject(source.name) { hideFlags = HideFlags.DontSave, layer = layer };
            target.transform.SetParent(parent, false);
            target.transform.localPosition = source.localPosition;
            target.transform.localRotation = source.localRotation;
            target.transform.localScale = source.localScale;
            target.SetActive(source.gameObject.activeSelf);
            var active = ancestorsActive && source.gameObject.activeSelf;
            var original = source.GetComponent<MeshRenderer>();
            var filter = source.GetComponent<MeshFilter>();
            var count = 0;
            if (active && original != null && original.enabled)
            {
                if (filter == null || filter.sharedMesh == null || original.sharedMaterials.Length == 0
                    || original.sharedMaterials.Any(m => m == null))
                    throw new ArgumentException("AppearanceSourceMeshMissing");
                var matrix = coordinateRoot.worldToLocalMatrix * target.transform.localToWorldMatrix;
                var sourceBounds = filter.sharedMesh.bounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var point = matrix.MultiplyPoint3x4(sourceBounds.center
                        + Vector3.Scale(sourceBounds.extents, new Vector3(x, y, z)));
                    if (!사가정외관검토Gate.Finite(point) || !bounds.Contains(point))
                        throw new ArgumentException("AppearanceSourceBoundsOutsideBuilding");
                }
                target.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = target.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = original.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                count++;
            }
            foreach (Transform child in source)
                count += CopyStaticTree(child, target.transform, coordinateRoot, bounds, active);
            return count;
        }

        private bool Reject(string code, out string result)
        {
            result = StatusCode = code;
            return false;
        }

        private static void Release(GameObject value)
        {
            if (value == null) return;
            value.SetActive(false);
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
