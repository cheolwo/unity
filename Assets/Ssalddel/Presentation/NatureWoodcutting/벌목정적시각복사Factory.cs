using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    /// <summary>
    /// D379 A1 staging: 정적 시각 계층만 복사한다. 메인 스레드의 동기 호출 전용.
    /// pivot/재생/시간/Audio/권위/자산 가공을 소유하지 않는다. 아직 컴파일·실행 미검증.
    /// 강제 LOD/진행 중 crossfade/외부 bounds writer 이력은 조회할 수 없다.
    /// 해당 입력 부재를 호출자가 확인하기 전에는 Runtime 보존 용도로 결속하지 않는다.
    /// destinationParent는 개발 소유 비활성 복사 container이며 계층 변경 callback/외부 writer가 없어야 한다.
    /// 임의 부모의 OnTransformChildrenChanged 부작용까지 이 Factory가 차단하는 계약은 아니다.
    /// </summary>
    public static class 벌목정적시각복사Factory
    {
        public const float AxisDotTolerance = 1e-5f;
        public const float MatrixElementTolerance = 1e-4f;

        public static bool TryCreate(Transform source, Transform destinationParent,
            out GameObject ownedWrapper, out string diagnostic)
            => TryCreateCore(source, destinationParent, out ownedWrapper, out diagnostic, null);

        // 격리시험에서 부분 생성 예외를 주입하는 비공개 경계. 제품 호출은 항상 null.
        private static bool TryCreateCore(Transform source, Transform destinationParent,
            out GameObject ownedWrapper, out string diagnostic, Action<int> afterNodeCreated)
        {
            ownedWrapper = null;
            diagnostic = string.Empty;
            var created = new List<GameObject>();
            try
            {
                if (!Preflight(source, destinationParent, out var nodes,
                        out var position, out var rotation, out var scale, out diagnostic))
                    return false;

                var wrapper = Allocate(source.name + "_OwnedStaticVisual", created);
                // 컴포넌트를 추가하기 전에 숨긴다. source/parent는 활성화하지 않는다.
                wrapper.SetActive(false);
                SceneManager.MoveGameObjectToScene(wrapper, destinationParent.gameObject.scene);
                wrapper.transform.SetParent(destinationParent, false);
                var transforms = new Dictionary<Transform, Transform>();
                var renderers = new Dictionary<Renderer, Renderer>();
                for (var index = 0; index < nodes.Count; index++)
                {
                    var original = nodes[index];
                    var copy = Allocate(original.name, created);
                    copy.SetActive(false);
                    SceneManager.MoveGameObjectToScene(copy, destinationParent.gameObject.scene);
                    copy.transform.SetParent(original == source ? wrapper.transform : transforms[original.parent], false);
                    copy.layer = original.gameObject.layer;
                    copy.transform.localPosition = original == source ? position : original.localPosition;
                    copy.transform.localRotation = original == source ? rotation : original.localRotation;
                    copy.transform.localScale = original == source ? scale : original.localScale;
                    transforms.Add(original, copy.transform);
                    copy.SetActive(original.gameObject.activeSelf); // wrapper는 비활성 그대로 유지한다.
                    afterNodeCreated?.Invoke(index + 1);
                }

                foreach (var original in nodes)
                {
                    var renderer = original.GetComponent<MeshRenderer>();
                    if (renderer == null) continue;
                    var copy = transforms[original].gameObject;
                    copy.AddComponent<MeshFilter>().sharedMesh = original.GetComponent<MeshFilter>().sharedMesh;
                    var target = copy.AddComponent<MeshRenderer>();
                    CopyRenderer(renderer, target, transforms);
                    renderers.Add(renderer, target);
                }
                foreach (var original in nodes)
                {
                    var group = original.GetComponent<LODGroup>();
                    if (group == null) continue;
                    var target = transforms[original].gameObject.AddComponent<LODGroup>();
                    var sourceLods = group.GetLODs();
                    var copiedLods = new LOD[sourceLods.Length];
                    for (var i = 0; i < sourceLods.Length; i++)
                    {
                        var mapped = new Renderer[sourceLods[i].renderers.Length];
                        for (var j = 0; j < mapped.Length; j++) mapped[j] = renderers[sourceLods[i].renderers[j]];
                        copiedLods[i] = new LOD(sourceLods[i].screenRelativeTransitionHeight, mapped)
                        { fadeTransitionWidth = sourceLods[i].fadeTransitionWidth };
                    }
                    target.SetLODs(copiedLods);
                    target.localReferencePoint = group.localReferencePoint;
                    target.size = group.size;
                    target.fadeMode = group.fadeMode;
                    target.animateCrossFading = group.animateCrossFading;
                    target.lastLODBillboard = group.lastLODBillboard;
                    target.enabled = group.enabled;
                    // 전역 crossFadeAnimationDuration, ForceLOD, RecalculateBounds는 쓰지 않는다.
                }
                // root 상대 TRS와 내부 계층을 조립한 뒤 실제 결과 행렬도 확인한다.
                foreach (var original in nodes)
                    if (!MatrixMatches(original.localToWorldMatrix, transforms[original].localToWorldMatrix))
                        throw new InvalidOperationException("WorldMatrixReconstructionMismatch");

                ownedWrapper = wrapper;
                diagnostic = "CreatedInactive";
                return true;
            }
            catch (Exception error)
            {
                diagnostic = "CreationException:" + error.GetType().Name + ":" + error.Message;
                CleanupOwned(created, ref diagnostic);
                return false;
            }
        }

        private static GameObject Allocate(string name, List<GameObject> created)
        {
            var value = new GameObject(name);
            created.Add(value); // 부모 지정 실패도 추적한다.
            return value;
        }

        private static bool Preflight(Transform source, Transform parent, out List<Transform> nodes,
            out Vector3 position, out Quaternion rotation, out Vector3 scale, out string diagnostic)
        {
            nodes = new List<Transform>(); position = default; rotation = Quaternion.identity; scale = Vector3.one;
            diagnostic = string.Empty;
            if (source == null || parent == null) return Reject("NullSourceOrParent", out diagnostic);
            if (source == parent || parent.IsChildOf(source)) return Reject("DestinationInsideSource", out diagnostic);
            if (!parent.gameObject.scene.IsValid() || !parent.gameObject.scene.isLoaded)
                return Reject("DestinationSceneUnavailable", out diagnostic);
            if (!ValidChain(source) || !ValidChain(parent)) return Reject("InvalidTransformChain", out diagnostic);
            var relative = parent.worldToLocalMatrix * source.localToWorldMatrix;
            if (!TryDecompose(relative, out position, out rotation, out scale, out diagnostic)) return false;
            Collect(source, nodes);
            var membership = new HashSet<Transform>(nodes);
            var supportedRenderers = new HashSet<Renderer>();
            foreach (var node in nodes)
            {
                if (!ValidLocal(node) || !Finite(node.localToWorldMatrix)
                    || !Finite(node.localToWorldMatrix.determinant) || node.localToWorldMatrix.determinant <= 0f)
                    return Reject("InvalidLocalTransform", out diagnostic);
                if (node.gameObject.isStatic) return Reject("StaticFlagsUnsupported", out diagnostic);
                foreach (var component in node.GetComponents<Component>())
                    if (component == null) return Reject("MissingComponent", out diagnostic);
                foreach (var renderer in node.GetComponents<Renderer>())
                {
                    if (renderer.GetType() != typeof(MeshRenderer)) return Reject("RendererTypeUnsupported", out diagnostic);
                    var meshRenderer = (MeshRenderer)renderer;
                    if (!ValidateRenderer(meshRenderer, membership, out diagnostic)) return false;
                    supportedRenderers.Add(renderer);
                }
                var filter = node.GetComponent<MeshFilter>();
                if (filter != null && (filter.sharedMesh == null || node.GetComponent<MeshRenderer>() == null))
                    return Reject("MissingMeshOrRenderer", out diagnostic);
            }
            if (supportedRenderers.Count == 0) return Reject("NoStaticRenderer", out diagnostic);
            foreach (var node in nodes)
            {
                var group = node.GetComponent<LODGroup>();
                if (group == null) continue;
                if (!Finite(group.localReferencePoint) || !Finite(group.size) || group.size <= 0f)
                    return Reject("LodBoundsInvalid", out diagnostic);
                if (group.lastLODBillboard || group.fadeMode == LODFadeMode.SpeedTree)
                    return Reject("DynamicLodUnsupported", out diagnostic);
                var lods = group.GetLODs();
                if (lods.Length == 0) return Reject("EmptyLodGroup", out diagnostic);
                foreach (var lod in lods)
                {
                    if (!UnitInterval(lod.screenRelativeTransitionHeight) || !UnitInterval(lod.fadeTransitionWidth))
                        return Reject("LodTransitionInvalid", out diagnostic);
                    if (lod.renderers == null) return Reject("MissingLodRenderer", out diagnostic);
                    foreach (var renderer in lod.renderers)
                        if (renderer == null || !supportedRenderers.Contains(renderer))
                            return Reject("ExternalOrMissingLodRenderer", out diagnostic);
                }
            }
            return true;
        }

        private static bool ValidateRenderer(MeshRenderer value, HashSet<Transform> membership, out string diagnostic)
        {
            diagnostic = string.Empty;
            var filter = value.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return Reject("MissingMesh", out diagnostic);
            if (value.HasPropertyBlock()) return Reject("MaterialPropertyBlockUnsupported", out diagnostic);
            if (value.isPartOfStaticBatch || value.additionalVertexStreams != null || value.enlightenVertexStream != null)
                return Reject("BatchedOrVertexStreamUnsupported", out diagnostic);
            if (value.probeAnchor != null && !membership.Contains(value.probeAnchor))
                return Reject("ExternalProbeAnchorUnsupported", out diagnostic);
            if (value.lightProbeProxyVolumeOverride != null || value.lightProbeUsage == LightProbeUsage.UseProxyVolume)
                return Reject("ProxyVolumeUnsupported", out diagnostic);
            if (value.lightmapIndex >= 0 && value.lightmapIndex != 65535
                || value.realtimeLightmapIndex >= 0 && value.realtimeLightmapIndex != 65535)
                return Reject("SceneLightmapUnsupported", out diagnostic);
            var materials = value.sharedMaterials;
            if (materials.Length == 0) return Reject("MissingMaterial", out diagnostic);
            foreach (var material in materials) if (material == null) return Reject("MissingMaterial", out diagnostic);
            if (!Finite(value.localBounds.center) || !Finite(value.localBounds.extents))
                return Reject("RendererBoundsInvalid", out diagnostic);
            // A1은 정적 MeshRenderer의 현재 bounds만 지원한다. 숨겨진 world override 이력은 검출 불가.
            if (!BoundsMatch(TransformBounds(value.transform.localToWorldMatrix, value.localBounds), value.bounds))
                return Reject("WorldBoundsOverrideUnsupported", out diagnostic);
            return true;
        }

        private static void CopyRenderer(MeshRenderer source, MeshRenderer target, Dictionary<Transform, Transform> map)
        {
            target.sharedMaterials = source.sharedMaterials;
            target.enabled = source.enabled;
            target.forceRenderingOff = source.forceRenderingOff;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.staticShadowCaster = source.staticShadowCaster;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.probeAnchor = source.probeAnchor == null ? null : map[source.probeAnchor];
            target.renderingLayerMask = source.renderingLayerMask;
            target.rendererPriority = source.rendererPriority;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.lightmapIndex = source.lightmapIndex;
            target.lightmapScaleOffset = source.lightmapScaleOffset;
            target.realtimeLightmapIndex = source.realtimeLightmapIndex;
            target.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
            target.receiveGI = source.receiveGI;
            target.rayTracingMode = source.rayTracingMode;
            target.rayTracingAccelerationStructureBuildFlags = source.rayTracingAccelerationStructureBuildFlags;
            target.rayTracingAccelerationStructureBuildFlagsOverride = source.rayTracingAccelerationStructureBuildFlagsOverride;
            target.forceMeshLod = source.forceMeshLod;
            target.meshLodSelectionBias = source.meshLodSelectionBias;
            target.globalIlluminationMeshLod = source.globalIlluminationMeshLod;
            target.localBounds = source.localBounds;
#if UNITY_EDITOR
            target.scaleInLightmap = source.scaleInLightmap;
            target.stitchLightmapSeams = source.stitchLightmapSeams;
#endif
        }

        private static void Collect(Transform value, List<Transform> nodes)
        {
            nodes.Add(value);
            for (var i = 0; i < value.childCount; i++) Collect(value.GetChild(i), nodes);
        }

        private static bool ValidChain(Transform value)
        {
            for (var current = value; current != null; current = current.parent)
                if (!ValidLocal(current)) return false;
            return Finite(value.localToWorldMatrix) && Finite(value.worldToLocalMatrix)
                && Finite(value.localToWorldMatrix.determinant) && value.localToWorldMatrix.determinant > 0f;
        }

        private static bool ValidLocal(Transform value)
            => Finite(value.localPosition) && Finite(value.localScale)
                && value.localScale.x > 0f && value.localScale.y > 0f && value.localScale.z > 0f
                && Finite(value.localRotation.x) && Finite(value.localRotation.y)
                && Finite(value.localRotation.z) && Finite(value.localRotation.w);

        private static bool TryDecompose(Matrix4x4 value, out Vector3 position, out Quaternion rotation,
            out Vector3 scale, out string diagnostic)
        {
            position = default; rotation = Quaternion.identity; scale = Vector3.one; diagnostic = string.Empty;
            if (!Finite(value) || !Finite(value.determinant) || value.determinant <= 0f)
                return Reject("NonFiniteSingularOrReflectedMatrix", out diagnostic);
            if (!Near(value.m30, 0f) || !Near(value.m31, 0f) || !Near(value.m32, 0f) || !Near(value.m33, 1f))
                return Reject("NonAffineMatrix", out diagnostic);
            Vector3 x = value.GetColumn(0), y = value.GetColumn(1), z = value.GetColumn(2);
            scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (!Finite(scale) || scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                return Reject("InvalidMatrixScale", out diagnostic);
            x /= scale.x; y /= scale.y; z /= scale.z;
            if (Mathf.Abs(Vector3.Dot(x, y)) > AxisDotTolerance
                || Mathf.Abs(Vector3.Dot(x, z)) > AxisDotTolerance
                || Mathf.Abs(Vector3.Dot(y, z)) > AxisDotTolerance)
                return Reject("ShearUnsupported", out diagnostic);
            if (Vector3.Dot(Vector3.Cross(x, y), z) <= 0f) return Reject("ReflectionUnsupported", out diagnostic);
            position = value.GetColumn(3);
            rotation = Quaternion.LookRotation(z, y);
            if (!MatrixMatches(value, Matrix4x4.TRS(position, rotation, scale)))
                return Reject("RelativeMatrixReconstructionMismatch", out diagnostic);
            return true;
        }

        private static bool MatrixMatches(Matrix4x4 original, Matrix4x4 reconstructed)
        {
            if (!Finite(original) || !Finite(reconstructed)) return false;
            for (var i = 0; i < 16; i++) if (!Near(reconstructed[i], original[i])) return false;
            return true;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            var x = matrix.MultiplyVector(new Vector3(bounds.extents.x, 0f, 0f));
            var y = matrix.MultiplyVector(new Vector3(0f, bounds.extents.y, 0f));
            var z = matrix.MultiplyVector(new Vector3(0f, 0f, bounds.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(bounds.center), extents * 2f);
        }

        private static bool BoundsMatch(Bounds a, Bounds b)
            => Near(a.center.x, b.center.x) && Near(a.center.y, b.center.y) && Near(a.center.z, b.center.z)
                && Near(a.extents.x, b.extents.x) && Near(a.extents.y, b.extents.y) && Near(a.extents.z, b.extents.z);
        private static bool Near(float actual, float original)
            => Finite(actual) && Finite(original)
                && Mathf.Abs(actual - original) <= MatrixElementTolerance * Mathf.Max(1f, Mathf.Abs(original));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Matrix4x4 value)
        { for (var i = 0; i < 16; i++) if (!Finite(value[i])) return false; return true; }
        private static bool UnitInterval(float value) => Finite(value) && value >= 0f && value <= 1f;
        private static bool Reject(string reason, out string diagnostic) { diagnostic = reason; return false; }

        private static void CleanupOwned(List<GameObject> created, ref string diagnostic)
        {
            if (created.Count == 0) return;
            var set = new HashSet<GameObject>(created);
            foreach (var value in created)
            {
                if (value == null) continue;
                try { value.SetActive(false); }
                catch (Exception error) { diagnostic += ";DeactivateFailed:" + error.GetType().Name; }
            }
            foreach (var value in created)
            {
                if (value == null || value.transform.parent != null && set.Contains(value.transform.parent.gameObject)) continue;
                try
                {
                    if (UnityEngine.Application.isPlaying) Object.Destroy(value);
                    else Object.DestroyImmediate(value);
                }
                catch (Exception error) { diagnostic += ";CleanupFailed:" + error.GetType().Name; }
            }
            diagnostic += UnityEngine.Application.isPlaying ? ";OwnedCleanupDeferred" : ";OwnedCleanupImmediate";
        }
    }
}
