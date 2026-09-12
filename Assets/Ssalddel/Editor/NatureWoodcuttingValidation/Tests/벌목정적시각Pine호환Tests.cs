using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    // D379 A2a 외부 staging. 아직 반입/컴파일/실행하지 않은 실물 자산 읽기 시험 초안.
    // Prefab을 Instantiate하지 않는다. 원본은 persistent asset, 복사 대상만 소유 Preview다.
    public sealed class 벌목정적시각Pine호환Tests
    {
        private sealed class AssetBaseline
        {
            public readonly string Path, Guid, Hash, MetaHash;
            public AssetBaseline(string path, string guid, string hash, string metaHash)
            { Path = path; Guid = guid; Hash = hash; MetaHash = metaHash; }
        }

        private static readonly AssetBaseline[] Baselines =
        {
            new AssetBaseline("Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab", "fc9f550802bde56499ac8b64cac565f0", "1331E2EF3F3E7D98C7F124C876B976D48ACD75FF659A37B397FFE32011FC6E0E", "AD80F71DAFFA156898E5A2CDFE07B2A87A1A97F71236D89B592CF12E8D15DC72"),
            new AssetBaseline("Assets/Synty/PolygonNature/Models/SM_Tree_Pine_01.fbx", "5514696c048ea514c8e0bbd59d370247", "D1DFEDBB28D1D613B73F800CDB4FDAA9C463F3C23219B9D743E3FCA4582AE0A3", "3E51F72F04769BCE131131E692E67925342D7FEE0F7C8AD4D1A3D32E544D1FA4"),
            new AssetBaseline("Assets/Synty/PolygonNature/Models/LODS/SM_Tree_Pine_01_LOD_01.fbx", "3fb50d0818667374aab581a3799635a9", "D14A5248D545C8033196105ECBB83D5BF86833DC62721372B48A0D58984D4D9D", "74007604EB93139C22F781C879A048D446BA3B531D9E49FDA5055D1FB960AB05"),
            new AssetBaseline("Assets/Synty/PolygonNature/Materials/Leaves/Leaves_Pine_01.mat", "feab95a1ad51daa4caeb40403608aa12", "8A5AE4CBBC90D4BDFE59ECDD533ACE38DFF327FFD0E0149BA0A4BF4BD99F7E0E", "76877099B8B159D0AEEE3947DDA67DAB6F9E005BA18F54547F618F15DE7C25D0"),
            new AssetBaseline("Assets/Synty/PolygonNature/Materials/Alts/PolygonNature_Tree_Trunk_01.mat", "9815abc3682332c43bb766decd901afb", "95D0367B721400A66B06B33D0471E725EEFF2EB39A648DF284B963E57D722E99", "548C4C0BF6DF0066B05FEEC84DE80D32793C018F467791EFEDCAB0CE4E09C229"),
            new AssetBaseline("Assets/Synty/PolygonNature/Materials/LODS/Leaves_Pine_LOD_01.mat", "32a7d9275f65e894c84e0dcdcf05e4e0", "BD9A6EFA3A9B67DD84A01106F18D94FC6F168C19BFEF99851A3CD86145E81B67", "F6AF35A9FEA6490FD2D1B0CF24BD512F019AF5F610486C45FD07C7EB09B44812")
        };

        private GameObject source;
        private Scene ownedScene;
        private string sourceBefore;
        private string assetsBefore;
        private int previewCountBefore;
        private string projectRoot;

        [SetUp]
        public void SetUp()
        {
            source = null; sourceBefore = null; assetsBefore = null; ownedScene = default;
            projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            previewCountBefore = EditorSceneManager.previewSceneCount;
            VerifyFileBaseline();
            source = AssetDatabase.LoadAssetAtPath<GameObject>(Baselines[0].Path);
            Assert.That(source, Is.Not.Null, "PineAssetMissing");
            Assert.That(EditorUtility.IsPersistent(source), Is.True, "SourceMustBePersistentAsset");
            sourceBefore = Snapshot(source.transform);
            assetsBefore = AssetSnapshot();
            VerifyDirectReferences();
        }

        [TearDown]
        public void TearDown()
        {
            // 자기 Preview만 닫는다. 원본/다른 Preview/Scene/Graph/설정은 복구 명목으로 쓰지 않는다.
            // 설치된 NUnit에는 Assert.Multiple이 없다. 실패해도 나머지 보존 검사를 모두 수행한다.
            var failures = new List<string>();
            Action<Action> verify = check => CollectFailure(check, failures);
            verify(() =>
            {
                if (ownedScene.IsValid()) EditorSceneManager.ClosePreviewScene(ownedScene);
            });
            if (source != null && sourceBefore != null)
                verify(() => Assert.That(Snapshot(source.transform), Is.EqualTo(sourceBefore), "SourceSerializedDrift"));
            if (assetsBefore != null)
                verify(() => Assert.That(AssetSnapshot(), Is.EqualTo(assetsBefore), "SharedAssetSerializedDrift"));
            if (projectRoot != null) VerifyFileBaseline(verify);
            verify(() => Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(previewCountBefore)));
            // source/sharedMesh/sharedMaterial은 로드된 원본 자산이므로 Destroy/Unload/Save하지 않는다.
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static void CollectFailure(Action check, ICollection<string> failures)
        {
            try { check(); }
            catch (Exception error) { failures.Add(error.ToString()); }
        }

        private void VerifyFileBaseline(Action<Action> verify = null)
        {
            // SetUp은 첫 실패에서 중단하고, TearDown은 파일/참조 검사별 실패를 수집한다.
            if (verify == null) verify = check => check();
            foreach (var entry in Baselines)
            {
                var path = System.IO.Path.Combine(projectRoot, entry.Path);
                verify(() => Assert.That(File.Exists(path), Is.True, entry.Path));
                verify(() => Assert.That(Hash(path), Is.EqualTo(entry.Hash), "AssetHashDrift:" + entry.Path));
                verify(() => Assert.That(Hash(path + ".meta"), Is.EqualTo(entry.MetaHash), "MetaHashDrift:" + entry.Path));
                verify(() => Assert.That(AssetDatabase.AssetPathToGUID(entry.Path), Is.EqualTo(entry.Guid), "GuidDrift:" + entry.Path));
                verify(() => Assert.That(AssetDatabase.GUIDToAssetPath(entry.Guid), Is.EqualTo(entry.Path)));
            }
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private static string Snapshot(Transform root)
            => string.Join("\n", root.GetComponentsInChildren<Transform>(true).SelectMany(t =>
                new[] { EditorJsonUtility.ToJson(t.gameObject) }.Concat(
                    t.GetComponents<Component>().Select(c => c == null ? "MissingComponent" : EditorJsonUtility.ToJson(c)))));

        private string AssetSnapshot()
            => string.Join("\n", Baselines.Skip(1).SelectMany(entry => AssetDatabase.LoadAllAssetsAtPath(entry.Path)
                .Where(value => value is Mesh || value is Material)
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .Select(value => entry.Guid + ":" + value.name + ":" + EditorJsonUtility.ToJson(value))));

        private static void AssertReference(Object value, string guid, long fileId)
        {
            Assert.That(value, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string actualGuid, out long actualId), Is.True);
            Assert.That(actualGuid, Is.EqualTo(guid));
            Assert.That(actualId, Is.EqualTo(fileId), "ImportedFileIdDriftRequiresReview");
        }

        private void VerifyDirectReferences()
        {
            AssertReference(source, Baselines[0].Guid, 1182756067964316L);
            Assert.That(source.transform.childCount, Is.EqualTo(2), "PineHierarchyDrift");
            var lod0 = source.transform.GetChild(0).GetComponent<MeshRenderer>();
            var lod1 = source.transform.GetChild(1).GetComponent<MeshRenderer>();
            AssertReference(lod0, Baselines[0].Guid, 23694049802177168L);
            AssertReference(lod1, Baselines[0].Guid, 23171540964131236L);
            AssertReference(lod0.GetComponent<MeshFilter>().sharedMesh, Baselines[1].Guid, 4300000L);
            AssertReference(lod1.GetComponent<MeshFilter>().sharedMesh, Baselines[2].Guid, 4300000L);
            Assert.That(lod0.sharedMaterials.Length, Is.EqualTo(2));
            Assert.That(lod1.sharedMaterials.Length, Is.EqualTo(2));
            AssertReference(lod0.sharedMaterials[0], Baselines[4].Guid, 2100000L);
            AssertReference(lod0.sharedMaterials[1], Baselines[3].Guid, 2100000L);
            AssertReference(lod1.sharedMaterials[0], Baselines[5].Guid, 2100000L);
            AssertReference(lod1.sharedMaterials[1], Baselines[5].Guid, 2100000L);
            var group = source.GetComponent<LODGroup>();
            Assert.That(group, Is.Not.Null);
            var lods = group.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(2));
            Assert.That(lods[0].renderers, Is.EqualTo(new Renderer[] { lod0 }));
            Assert.That(lods[1].renderers, Is.EqualTo(new Renderer[] { lod1 }));
        }

        private Transform CreateOwnedContainer(bool rotate, bool shear)
        {
            ownedScene = EditorSceneManager.NewPreviewScene();
            var container = new GameObject("Owned_PineRead_Container");
            container.SetActive(false);
            SceneManager.MoveGameObjectToScene(container, ownedScene);
            // Transform만 있는 비활성 소유 container. 기존 부모 callback/외부 writer 없음.
            if (rotate)
            {
                container.transform.position = new Vector3(2f, 3f, -4f);
                container.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                container.transform.localScale = shear ? new Vector3(2f, 1f, 1f) : Vector3.one * 2f;
            }
            return container.transform;
        }

        [Test]
        public void Pine직접자산과LOD계보는_고정GUIDhash와일치한다()
        {
            // SetUp의 파일/meta/GUID/fileID/직접 참조 대조. 렌더 호환 성공을 뜻하지 않는다.
            VerifyDirectReferences();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 읽기원본은_소유비활성복사에서계층TRS와LOD를보존한다(bool rotatedUniformParent)
        {
            var parent = CreateOwnedContainer(rotatedUniformParent, false);
            var parentTrs = parent.localToWorldMatrix;
            var success = 벌목정적시각복사Factory.TryCreate(source.transform, parent, out var wrapper, out var diagnostic);
            TestContext.WriteLine("PineFactoryDiagnostic=" + diagnostic + ";success=" + success);
            // 거부도 먼저 원본/소유상태를 대조한 뒤 실패로 반환한다. 설정 제거/Ignore/Inconclusive 금지.
            Assert.That(Snapshot(source.transform), Is.EqualTo(sourceBefore));
            SameMatrix(parentTrs, parent.localToWorldMatrix);
            if (!success)
            {
                Assert.That(wrapper, Is.Null);
                Assert.That(parent.childCount, Is.Zero, "RejectedCopyLeftOwnedFragments");
                Assert.Fail("PineInputRejected_NoFallback:" + diagnostic);
                return;
            }
            Assert.That(diagnostic, Is.EqualTo("CreatedInactive"));
            Assert.That(wrapper, Is.Not.Null);
            Assert.That(wrapper.activeSelf, Is.False);
            Assert.That(parent.gameObject.activeSelf, Is.False);
            Assert.That(parent.childCount, Is.EqualTo(1));
            Assert.That(wrapper.transform.childCount, Is.EqualTo(1));
            Assert.That(wrapper.transform.parent, Is.SameAs(parent));
            var map = new Dictionary<Transform, Transform>();
            CompareHierarchy(source.transform, wrapper.transform.GetChild(0), map, true);
            foreach (var pair in map)
            {
                var original = pair.Key.GetComponent<MeshRenderer>();
                if (original != null) CompareRenderer(original, pair.Value.GetComponent<MeshRenderer>(), map);
                var group = pair.Key.GetComponent<LODGroup>();
                if (group != null) CompareLod(group, pair.Value.GetComponent<LODGroup>(), map);
            }
            var allowed = new[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(LODGroup) };
            Assert.That(wrapper.GetComponentsInChildren<Component>(true).All(c => allowed.Contains(c.GetType())), Is.True);
            Assert.That(source.GetComponent<CapsuleCollider>(), Is.Not.Null, "OriginalColliderMustRemain");
            Assert.That(wrapper.GetComponentsInChildren<Transform>(true).All(t => !t.gameObject.activeInHierarchy), Is.True);
            Assert.That(AssetSnapshot(), Is.EqualTo(assetsBefore));
        }

        [Test]
        public void Pine원본무변경으로_부모상대shear를거부한다()
        {
            var parent = CreateOwnedContainer(true, true);
            var parentTrs = parent.localToWorldMatrix;
            Assert.That(벌목정적시각복사Factory.TryCreate(source.transform, parent, out var wrapper, out var diagnostic), Is.False);
            Assert.That(diagnostic, Is.EqualTo("ShearUnsupported"));
            Assert.That(wrapper, Is.Null);
            Assert.That(parent.childCount, Is.Zero);
            SameMatrix(parentTrs, parent.localToWorldMatrix);
            Assert.That(Snapshot(source.transform), Is.EqualTo(sourceBefore));
            // 합성 부모의 거부 시험이며 실제 Pine 호환 성공/실제 marker 지원 증거가 아니다.
        }

        private static void CompareHierarchy(Transform original, Transform copy,
            Dictionary<Transform, Transform> map, bool root)
        {
            Assert.That(copy.name, Is.EqualTo(original.name));
            Assert.That(copy.gameObject.activeSelf, Is.EqualTo(original.gameObject.activeSelf));
            Assert.That(copy.gameObject.layer, Is.EqualTo(original.gameObject.layer));
            Assert.That(copy.childCount, Is.EqualTo(original.childCount));
            SameMatrix(original.localToWorldMatrix, copy.localToWorldMatrix);
            if (!root)
            {
                Assert.That(copy.localPosition, Is.EqualTo(original.localPosition));
                Assert.That(copy.localRotation, Is.EqualTo(original.localRotation));
                Assert.That(copy.localScale, Is.EqualTo(original.localScale));
            }
            map.Add(original, copy);
            for (var i = 0; i < original.childCount; i++)
                CompareHierarchy(original.GetChild(i), copy.GetChild(i), map, false);
        }

        private static void CompareRenderer(MeshRenderer original, MeshRenderer copy,
            Dictionary<Transform, Transform> map)
        {
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(original.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(copy.sharedMaterials, Is.EqualTo(original.sharedMaterials));
            Assert.That(copy.probeAnchor, Is.SameAs(original.probeAnchor == null ? null : map[original.probeAnchor]));
            Assert.That(copy.HasPropertyBlock(), Is.False);
            // A1이 읽기/복사하는 scalar 설정과 localBounds만 대조. 내부 렌더 상태 전부라는 주장이 아니다.
            var fields = new[] { "enabled", "forceRenderingOff", "shadowCastingMode", "receiveShadows",
                "staticShadowCaster", "motionVectorGenerationMode", "allowOcclusionWhenDynamic",
                "lightProbeUsage", "reflectionProbeUsage", "renderingLayerMask", "rendererPriority",
                "sortingLayerID", "sortingOrder", "lightmapIndex", "lightmapScaleOffset",
                "realtimeLightmapIndex", "realtimeLightmapScaleOffset", "receiveGI", "rayTracingMode",
                "rayTracingAccelerationStructureBuildFlags", "rayTracingAccelerationStructureBuildFlagsOverride",
                "forceMeshLod", "meshLodSelectionBias", "globalIlluminationMeshLod", "localBounds",
                "scaleInLightmap", "stitchLightmapSeams" };
            foreach (var field in fields)
            {
                var property = typeof(MeshRenderer).GetProperty(field);
                Assert.That(property, Is.Not.Null, "RendererApiMissing:" + field);
                Assert.That(property.GetValue(copy), Is.EqualTo(property.GetValue(original)), field);
            }
        }

        private static void CompareLod(LODGroup original, LODGroup copy, Dictionary<Transform, Transform> map)
        {
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.enabled, Is.EqualTo(original.enabled));
            Assert.That(copy.size, Is.EqualTo(original.size));
            Assert.That(copy.localReferencePoint, Is.EqualTo(original.localReferencePoint));
            Assert.That(copy.fadeMode, Is.EqualTo(original.fadeMode));
            Assert.That(copy.animateCrossFading, Is.EqualTo(original.animateCrossFading));
            Assert.That(copy.lastLODBillboard, Is.EqualTo(original.lastLODBillboard));
            var oldLods = original.GetLODs(); var newLods = copy.GetLODs();
            Assert.That(newLods.Length, Is.EqualTo(oldLods.Length));
            for (var i = 0; i < oldLods.Length; i++)
            {
                Assert.That(newLods[i].screenRelativeTransitionHeight, Is.EqualTo(oldLods[i].screenRelativeTransitionHeight));
                Assert.That(newLods[i].fadeTransitionWidth, Is.EqualTo(oldLods[i].fadeTransitionWidth));
                Assert.That(newLods[i].renderers.Length, Is.EqualTo(oldLods[i].renderers.Length));
                for (var j = 0; j < oldLods[i].renderers.Length; j++)
                {
                    var renderer = oldLods[i].renderers[j];
                    Assert.That(newLods[i].renderers[j], Is.SameAs(map[renderer.transform].GetComponent<MeshRenderer>()));
                    Assert.That(newLods[i].renderers[j], Is.Not.SameAs(renderer));
                }
            }
        }

        private static void SameMatrix(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (var i = 0; i < 16; i++)
                Assert.That(Mathf.Abs(expected[i] - actual[i]),
                    Is.LessThanOrEqualTo(1e-4f * Mathf.Max(1f, Mathf.Abs(expected[i]))), "matrix:" + i);
        }
    }
}
