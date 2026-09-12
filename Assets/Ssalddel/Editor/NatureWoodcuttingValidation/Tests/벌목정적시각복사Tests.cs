using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Presentation.NatureWoodcutting
{
    // D379 A1 staging. 합성 정적 메시/재질만 사용하며 Pine/vendor/공식 Scene/Scope/Save를 읽지 않는다.
    // 아래 사례는 아직 컴파일/Runner/Play 검증되지 않았다.
    public sealed class 벌목정적시각복사Tests
    {
        private Scene ownedScene;
        private Transform source;
        private Transform destination;
        private Mesh mesh;
        private Material material;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            ownedScene = EditorSceneManager.NewPreviewScene();
            source = Node("Owned_Source", null);
            destination = Node("Owned_Destination", null);
            // 개발 소유 비활성 container. 계층 변경 callback/외부 writer 없는 합성 입력이다.
            destination.gameObject.SetActive(false);
            mesh = new Mesh { name = "Owned_SyntheticTriangle" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds(); // 시험 전용 합성 메시. 기존 자산/메시 가공 아님.
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null, "FixtureShaderMissing");
            material = new Material(shader) { name = "Owned_SyntheticMaterial" };
            Visual("Visual", source);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (ownedScene.IsValid()) EditorSceneManager.ClosePreviewScene(ownedScene); }
            finally
            {
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
            }
        }

        private Transform Node(string name, Transform parent)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, ownedScene);
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        private MeshRenderer Visual(string name, Transform parent)
        {
            var node = Node(name, parent);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private GameObject Create()
        {
            Assert.That(벌목정적시각복사Factory.TryCreate(source, destination, out var wrapper, out var diagnostic),
                Is.True, diagnostic);
            Assert.That(wrapper, Is.Not.Null);
            Assert.That(wrapper.activeSelf, Is.False);
            Assert.That(wrapper.transform.parent, Is.SameAs(destination));
            Assert.That(diagnostic, Is.EqualTo("CreatedInactive"));
            return wrapper;
        }

        private static string Snapshot(Transform root)
            => string.Join("\n", root.GetComponentsInChildren<Transform>(true).SelectMany(t =>
                t.GetComponents<Component>().Select(c => EditorJsonUtility.ToJson(c))));

        private void Reject(string reason)
        {
            var beforeSource = Snapshot(source); var beforeParent = Snapshot(destination);
            var roots = ownedScene.rootCount;
            Assert.That(벌목정적시각복사Factory.TryCreate(source, destination, out var wrapper, out var diagnostic), Is.False);
            Assert.That(wrapper, Is.Null);
            Assert.That(diagnostic, Is.EqualTo(reason));
            Assert.That(Snapshot(source), Is.EqualTo(beforeSource));
            Assert.That(Snapshot(destination), Is.EqualTo(beforeParent));
            Assert.That(ownedScene.rootCount, Is.EqualTo(roots));
        }

        private static void SameMatrix(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (var i = 0; i < 16; i++)
                Assert.That(Mathf.Abs(expected[i] - actual[i]),
                    Is.LessThanOrEqualTo(1e-4f * Mathf.Max(1f, Mathf.Abs(expected[i]))), "matrix element " + i);
        }

        [Test]
        public void 기본복사는_비활성wrapper와원본메시재질참조만추가한다()
        {
            var sourceBefore = Snapshot(source);
            var parentPosition = destination.localPosition;
            var wrapper = Create();
            var copy = wrapper.transform.GetChild(0);
            Assert.That(copy.name, Is.EqualTo(source.name));
            Assert.That(copy.GetChild(0).GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(copy.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
            SameMatrix(source.localToWorldMatrix, copy.localToWorldMatrix);
            Assert.That(Snapshot(source), Is.EqualTo(sourceBefore));
            Assert.That(destination.localPosition, Is.EqualTo(parentPosition));
        }

        [TestCase(1f)]
        [TestCase(3f)]
        public void 회전된부모와균일크기는_원본월드행렬을보존한다(float uniformScale)
        {
            source.position = new Vector3(7f, -2f, 5f);
            source.rotation = Quaternion.Euler(13f, 47f, 9f);
            source.localScale = Vector3.one * uniformScale;
            destination.position = new Vector3(-3f, 8f, 2f);
            destination.rotation = Quaternion.Euler(-15f, 29f, 31f);
            destination.localScale = Vector3.one * 2f;
            SameMatrix(source.localToWorldMatrix, Create().transform.GetChild(0).localToWorldMatrix);
        }

        [Test]
        public void 표현가능한양의비균일크기는_일괄거부하지않는다()
        {
            source.localScale = new Vector3(2f, 3f, 4f);
            source.rotation = Quaternion.Euler(5f, 30f, 20f);
            destination.rotation = Quaternion.Euler(10f, -25f, 5f);
            SameMatrix(source.localToWorldMatrix, Create().transform.GetChild(0).localToWorldMatrix);
        }

        [Test]
        public void 내부자식의회전비균일조합은_계층그대로보존한다()
        {
            var child = source.GetChild(0);
            var group = Node("NonUniformGroup", source);
            child.SetParent(group, false);
            group.localScale = new Vector3(2f, 1f, 3f);
            child.localRotation = Quaternion.Euler(0f, 30f, 0f);
            child.localPosition = new Vector3(1f, 2f, 3f);
            var copy = Create().transform.GetChild(0).GetChild(0).GetChild(0);
            Assert.That(copy.localRotation, Is.EqualTo(child.localRotation));
            Assert.That(copy.localScale, Is.EqualTo(child.localScale));
            Assert.That(copy.localPosition, Is.EqualTo(child.localPosition));
            SameMatrix(child.localToWorldMatrix, copy.localToWorldMatrix);
        }

        [Test]
        public void 부모비균일과원본회전의상대shear는_무보정거부한다()
        {
            destination.localScale = new Vector3(2f, 1f, 1f);
            source.rotation = Quaternion.Euler(0f, 0f, 45f);
            Reject("ShearUnsupported");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void 원본영또는음수scale은_거부한다(float x)
        {
            source.localScale = new Vector3(x, 1f, 1f);
            Reject("InvalidTransformChain");
        }

        [Test]
        public void 이중음수축도_양의행렬식만으로허용하지않는다()
        {
            source.localScale = new Vector3(-1f, -1f, 1f);
            Reject("InvalidTransformChain");
        }

        [Test]
        public void 부모상위의음수scale도_거부한다()
        {
            var ancestor = Node("Owned_Ancestor", null);
            destination.SetParent(ancestor, false);
            ancestor.localScale = new Vector3(-1f, 1f, 1f);
            Reject("InvalidTransformChain");
        }

        [Test]
        public void 내부자식의음수scale을_성공으로누락하지않는다()
        {
            source.GetChild(0).localScale = new Vector3(-1f, 1f, 1f);
            Reject("InvalidLocalTransform");
        }

        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("singular")]
        [TestCase("reflection")]
        public void 비유한특이반사행렬은_Transform주입없이분해검사로거부한다(string kind)
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = kind == "nan" ? float.NaN : kind == "infinity" ? float.PositiveInfinity
                : kind == "singular" ? 0f : -1f;
            var args = new object[] { matrix, null, null, null, null };
            Assert.That(typeof(벌목정적시각복사Factory).GetMethod("TryDecompose", PrivateStatic).Invoke(null, args), Is.False);
            Assert.That((string)args[4], Is.EqualTo("NonFiniteSingularOrReflectedMatrix"));
        }

        [TestCase(0.0001f, true)]
        [TestCase(0.00011f, false)]
        public void 행렬원소허용오차는_승인시작값을쓴다(float delta, bool expected)
        {
            Assert.That(벌목정적시각복사Factory.AxisDotTolerance, Is.EqualTo(1e-5f));
            Assert.That(벌목정적시각복사Factory.MatrixElementTolerance, Is.EqualTo(1e-4f));
            Assert.That(typeof(벌목정적시각복사Factory).GetMethod("Near", PrivateStatic)
                .Invoke(null, new object[] { delta, 0f }), Is.EqualTo(expected));
        }

        [Test]
        public void 비활성원본과자식활성상태를_wrapper와구별한다()
        {
            source.gameObject.SetActive(false);
            source.GetChild(0).gameObject.SetActive(true);
            var hidden = Node("Hidden", source); hidden.gameObject.SetActive(false);
            var wrapper = Create(); var copy = wrapper.transform.GetChild(0);
            Assert.That(copy.gameObject.activeSelf, Is.False);
            Assert.That(copy.GetChild(0).gameObject.activeSelf, Is.True);
            Assert.That(copy.GetChild(1).gameObject.activeSelf, Is.False);
            Assert.That(copy.GetChild(0).gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void Renderer설정과내부probe참조를_원본대신복사본으로보존한다()
        {
            var renderer = source.GetComponentInChildren<MeshRenderer>();
            var anchor = Node("ProbeAnchor", source);
            renderer.enabled = false; renderer.forceRenderingOff = true;
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.receiveShadows = false; renderer.staticShadowCaster = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.probeAnchor = anchor; renderer.sortingOrder = 13;
            renderer.renderingLayerMask = 5; renderer.rendererPriority = 3;
            var copyRoot = Create().transform.GetChild(0);
            var copy = copyRoot.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(copy.enabled, Is.False); Assert.That(copy.forceRenderingOff, Is.True);
            Assert.That(copy.shadowCastingMode, Is.EqualTo(renderer.shadowCastingMode));
            Assert.That(copy.receiveShadows, Is.False); Assert.That(copy.staticShadowCaster, Is.True);
            Assert.That(copy.motionVectorGenerationMode, Is.EqualTo(renderer.motionVectorGenerationMode));
            Assert.That(copy.allowOcclusionWhenDynamic, Is.False);
            Assert.That(copy.lightProbeUsage, Is.EqualTo(renderer.lightProbeUsage));
            Assert.That(copy.reflectionProbeUsage, Is.EqualTo(renderer.reflectionProbeUsage));
            Assert.That(copy.sortingOrder, Is.EqualTo(13)); Assert.That(copy.renderingLayerMask, Is.EqualTo(5));
            Assert.That(copy.rendererPriority, Is.EqualTo(3));
            Assert.That(copy.probeAnchor, Is.SameAs(copyRoot.GetChild(1)));
            Assert.That(copy.probeAnchor, Is.Not.SameAs(anchor));
        }

        [Test]
        public void LOD의두단계와비활성Renderer참조는_재매핑한다()
        {
            var lod0 = source.GetComponentInChildren<MeshRenderer>();
            var lod1 = Visual("LOD1", source); lod1.enabled = false;
            lod1.gameObject.SetActive(false);
            var group = source.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.5f, new Renderer[] { lod0 }) { fadeTransitionWidth = .1f },
                new LOD(.1f, new Renderer[] { lod1 }) { fadeTransitionWidth = .2f } });
            group.localReferencePoint = Vector3.up; group.size = 4f;
            group.fadeMode = LODFadeMode.CrossFade; group.animateCrossFading = true; group.enabled = false;
            var copy = Create().transform.GetChild(0).GetComponent<LODGroup>();
            Assert.That(copy.GetLODs().Length, Is.EqualTo(2));
            Assert.That(copy.enabled, Is.False); Assert.That(copy.localReferencePoint, Is.EqualTo(Vector3.up));
            Assert.That(copy.size, Is.EqualTo(4f)); Assert.That(copy.fadeMode, Is.EqualTo(LODFadeMode.CrossFade));
            Assert.That(copy.animateCrossFading, Is.True);
            for (var i = 0; i < 2; i++)
            {
                var oldLod = group.GetLODs()[i]; var newLod = copy.GetLODs()[i];
                Assert.That(newLod.screenRelativeTransitionHeight, Is.EqualTo(oldLod.screenRelativeTransitionHeight));
                Assert.That(newLod.fadeTransitionWidth, Is.EqualTo(oldLod.fadeTransitionWidth));
                Assert.That(newLod.renderers[0], Is.Not.SameAs(oldLod.renderers[0]));
                Assert.That(newLod.renderers[0].transform.IsChildOf(copy.transform), Is.True);
                Assert.That(newLod.renderers[0].enabled, Is.EqualTo(oldLod.renderers[0].enabled));
            }
        }

        [Test]
        public void 원본ColliderAnimator업무컴포넌트는_복사하지않고보존한다()
        {
            source.gameObject.AddComponent<BoxCollider>();
            source.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            source.gameObject.AddComponent<Animator>();
            source.gameObject.AddComponent<AudioSource>(); // 제외할 업무/음향 구성의 합성 대역.
            var before = Snapshot(source);
            var wrapper = Create();
            var allowed = new[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(LODGroup) };
            Assert.That(wrapper.GetComponentsInChildren<Component>(true).All(c => allowed.Contains(c.GetType())), Is.True);
            Assert.That(Snapshot(source), Is.EqualTo(before));
        }

        [Test]
        public void 원본이destination의기존자손이면_형제wrapper만추가한다()
        {
            source.SetParent(destination, false);
            var originalPosition = source.localPosition;
            var wrapper = Create();
            Assert.That(destination.childCount, Is.EqualTo(2));
            Assert.That(destination.GetChild(0), Is.SameAs(source));
            Assert.That(destination.GetChild(1), Is.SameAs(wrapper.transform));
            Assert.That(source.localPosition, Is.EqualTo(originalPosition));
        }

        [Test]
        public void 같은source와parent는_거부한다()
        {
            Assert.That(벌목정적시각복사Factory.TryCreate(source, source, out var value, out var diagnostic), Is.False);
            Assert.That(value, Is.Null); Assert.That(diagnostic, Is.EqualTo("DestinationInsideSource"));
        }

        [Test]
        public void 원본내destination은_자기참조로거부한다()
        {
            destination.SetParent(source, false); Reject("DestinationInsideSource");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void null입력은_생성없이거부한다(bool nullSource)
        {
            Assert.That(벌목정적시각복사Factory.TryCreate(nullSource ? null : source,
                nullSource ? destination : null, out var value, out var diagnostic), Is.False);
            Assert.That(value, Is.Null); Assert.That(diagnostic, Is.EqualTo("NullSourceOrParent"));
        }

        [Test]
        public void 속성블록은_원본을지우거나기본재질로대체하지않는다()
        {
            var renderer = source.GetComponentInChildren<MeshRenderer>();
            var block = new MaterialPropertyBlock(); block.SetFloat("_OwnedTest", .5f);
            renderer.SetPropertyBlock(block); Reject("MaterialPropertyBlockUnsupported");
            var result = new MaterialPropertyBlock(); renderer.GetPropertyBlock(result);
            Assert.That(result.GetFloat("_OwnedTest"), Is.EqualTo(.5f));
        }

        [Test]
        public void 외부probeAnchor는_거부한다()
        {
            source.GetComponentInChildren<MeshRenderer>().probeAnchor = destination;
            Reject("ExternalProbeAnchorUnsupported");
        }

        [Test]
        public void proxyVolume참조는_내부라도지원으로가정하지않는다()
        {
            source.GetComponentInChildren<MeshRenderer>().lightProbeProxyVolumeOverride = source.gameObject;
            Reject("ProxyVolumeUnsupported");
        }

        [Test]
        public void 외부LOD참조는_생성전거부한다()
        {
            var external = Visual("External", destination);
            var group = source.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.2f, new Renderer[] { external }) }); group.size = 1f;
            Reject("ExternalOrMissingLodRenderer");
        }

        [Test]
        public void 누락mesh는_성공으로누락하지않는다()
        {
            source.GetComponentInChildren<MeshFilter>().sharedMesh = null;
            Reject("MissingMesh");
        }

        [Test]
        public void 누락material은_거부한다()
        {
            source.GetComponentInChildren<MeshRenderer>().sharedMaterials = new Material[] { null };
            Reject("MissingMaterial");
        }

        [Test]
        public void SkinnedRenderer는_정적메시로변환하지않는다()
        {
            Node("Skinned", source).gameObject.AddComponent<SkinnedMeshRenderer>();
            Reject("RendererTypeUnsupported");
        }

        [Test]
        public void LineRenderer도_지원외형상으로거부한다()
        {
            Node("Line", source).gameObject.AddComponent<LineRenderer>();
            Reject("RendererTypeUnsupported");
        }

        [Test]
        public void 추가vertexStream을_조용히누락하지않는다()
        {
            source.GetComponentInChildren<MeshRenderer>().additionalVertexStreams = mesh;
            Reject("BatchedOrVertexStreamUnsupported");
        }

        [Test]
        public void Scene라이트맵할당은_별도결속없이지원하지않는다()
        {
            source.GetComponentInChildren<MeshRenderer>().lightmapIndex = 0;
            Reject("SceneLightmapUnsupported");
        }

        [Test]
        public void 부분생성예외는_EditMode자기조각만즉시정리한다()
        {
            var existing = Node("ExistingChild", destination);
            var beforeSource = Snapshot(source); var beforeParent = Snapshot(destination);
            var roots = ownedScene.rootCount;
            var args = new object[] { source, destination, null, null,
                new Action<int>(index => { if (index == 2) throw new InvalidOperationException("OwnedInjectedFailure"); }) };
            Assert.That(typeof(벌목정적시각복사Factory).GetMethod("TryCreateCore", PrivateStatic).Invoke(null, args), Is.False);
            Assert.That(args[2], Is.Null);
            StringAssert.Contains("OwnedInjectedFailure", (string)args[3]);
            StringAssert.Contains("OwnedCleanupImmediate", (string)args[3]);
            Assert.That(destination.GetChild(0), Is.SameAs(existing));
            Assert.That(Snapshot(source), Is.EqualTo(beforeSource));
            Assert.That(Snapshot(destination), Is.EqualTo(beforeParent));
            Assert.That(ownedScene.rootCount, Is.EqualTo(roots));
            // 실제 Play Destroy 지연/MonoBehaviour 수명 검증이 아니다.
        }

        [Test]
        public void 원본root자체의Renderer도_별도내부root로복사한다()
        {
            source.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            source.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var copy = Create().transform.GetChild(0);
            Assert.That(copy.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(copy.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void 재질별속성블록도_명시거부한다()
        {
            var renderer = source.GetComponentInChildren<MeshRenderer>();
            var block = new MaterialPropertyBlock(); block.SetFloat("_OwnedTest", .25f);
            renderer.SetPropertyBlock(block, 0);
            Reject("MaterialPropertyBlockUnsupported");
            var retained = new MaterialPropertyBlock(); renderer.GetPropertyBlock(retained, 0);
            Assert.That(retained.GetFloat("_OwnedTest"), Is.EqualTo(.25f));
        }

        [Test]
        public void 누락LOD참조도_미리거부한다()
        {
            var group = source.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.2f, new Renderer[] { null }) }); group.size = 1f;
            Reject("ExternalOrMissingLodRenderer");
        }

        [Test]
        public void Renderer없는MeshFilter는_성공으로누락하지않는다()
        {
            Node("OrphanFilter", source).gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Reject("MissingMeshOrRenderer");
        }

        [Test]
        public void 정적배치플래그는_동적복사본지원으로가정하지않는다()
        {
            source.gameObject.isStatic = true;
            Reject("StaticFlagsUnsupported");
        }

        [Test]
        public void 계산값과다른worldBounds는_명시거부한다()
        {
            source.GetComponentInChildren<MeshRenderer>().bounds = new Bounds(Vector3.one * 50f, Vector3.one);
            Reject("WorldBoundsOverrideUnsupported");
        }

        [TestCase(0.000009f, true)]
        [TestCase(0.000011f, false)]
        public void 정규화축내적허용오차의양쪽을_검사한다(float dot, bool expected)
        {
            var matrix = Matrix4x4.identity;
            matrix.m01 = dot;
            var args = new object[] { matrix, null, null, null, null };
            Assert.That(typeof(벌목정적시각복사Factory).GetMethod("TryDecompose", PrivateStatic)
                .Invoke(null, args), Is.EqualTo(expected));
            if (!expected) Assert.That((string)args[4], Is.EqualTo("ShearUnsupported"));
        }
    }
}
