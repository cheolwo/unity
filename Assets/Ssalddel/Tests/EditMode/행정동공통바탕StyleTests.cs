using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 행정동공통바탕StyleTests
    {
        private GameObject root;
        private Mesh mesh;
        private Material material;
        private 행정동공통바탕Style.Scope scope;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("공통 바탕 시험");
            material = new Material(Shader.Find("Ssalddel/ObservationPrimitive"));
            mesh = CreateMesh(); AddChild("표본 하나", mesh);
        }

        [TearDown]
        public void TearDown()
        {
            scope?.Dispose(); scope = null;
            Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material);
        }

        [Test]
        public void 같은모듈전체에_역할별색만바꾸고_형상속성과공유관계를보존한뒤_정확복구한다()
        {
            var second = AddChild("표본 둘", mesh);
            second.transform.localPosition = new Vector3(10, 0, 20);
            second.transform.localRotation = Quaternion.Euler(0, 25, 0);
            var originalVertices = mesh.vertices; var originalNormals = mesh.normals;
            var originalIndices = mesh.triangles; var originalColors = mesh.colors;
            var originalUv = mesh.uv; var originalUv2 = mesh.uv2; var originalTangents = mesh.tangents;
            var originalBounds = mesh.bounds; var originalPosition = second.transform.localPosition;
            var originalRotation = second.transform.localRotation; var ambient = RenderSettings.ambientLight;
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var filters = root.GetComponentsInChildren<MeshFilter>();
            scope = 행정동공통바탕Style.Apply(root);
            var candidate = filters[0].sharedMesh;
            Assert.That(candidate, Is.Not.SameAs(mesh));
            Assert.That(filters[1].sharedMesh, Is.SameAs(candidate));
            Assert.That(candidate.vertices, Is.EqualTo(originalVertices));
            Assert.That(candidate.normals, Is.EqualTo(originalNormals));
            Assert.That(candidate.triangles, Is.EqualTo(originalIndices));
            Assert.That(candidate.uv, Is.EqualTo(originalUv)); Assert.That(candidate.uv2, Is.EqualTo(originalUv2));
            Assert.That(candidate.tangents, Is.EqualTo(originalTangents)); Assert.That(candidate.bounds, Is.EqualTo(originalBounds));
            Assert.That(candidate.colors, Is.Not.EqualTo(originalColors));
            Assert.That(mesh.colors, Is.EqualTo(originalColors));
            Assert.That(scope.Result.styledRendererCount, Is.EqualTo(2));
            Assert.That(scope.Result.candidateMeshCount, Is.EqualTo(1));
            Assert.That(scope.Result.groundTriangleCount, Is.EqualTo(1));
            Assert.That(scope.Result.roadTriangleCount, Is.EqualTo(1));
            Assert.That(scope.Result.boundaryTriangleCount, Is.EqualTo(1));
            Assert.That(scope.Result.roofTriangleCount, Is.EqualTo(1));
            Assert.That(scope.Result.wallTriangleCount, Is.EqualTo(1));
            Assert.That(scope.Result.unchangedTriangleCount, Is.EqualTo(1));
            Assert.That(candidate.colors.Skip(15).Take(3), Is.EqualTo(originalColors.Skip(15).Take(3)));
            scope.Dispose(); scope.Dispose();
            Assert.That(filters.All(filter => filter.sharedMesh == mesh), Is.True);
            Assert.That(renderers.All(renderer => renderer.sharedMaterials.SequenceEqual(new[] { material })), Is.True);
            Assert.That(second.transform.localPosition, Is.EqualTo(originalPosition));
            Assert.That(second.transform.localRotation, Is.EqualTo(originalRotation));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
            Assert.That(scope.Result.rendererReferencesRestored && scope.Result.sourceDataPreserved
                        && scope.Result.ownedResourcesDestroyed, Is.True);
            Assert.That(candidate == null, Is.True);
            scope = 행정동공통바탕Style.Apply(root);
            Assert.That(scope.Result.styledRendererCount, Is.EqualTo(2));
        }

        [Test]
        public void 중복또는겹친범위는_이미적용한사본을변경하지않고거절한다()
        {
            scope = 행정동공통바탕Style.Apply(root);
            var filter = root.GetComponentInChildren<MeshFilter>(); var candidate = filter.sharedMesh;
            Assert.Throws<InvalidOperationException>(() => 행정동공통바탕Style.Apply(root));
            Assert.Throws<InvalidOperationException>(() => 행정동공통바탕Style.Apply(filter.gameObject));
            Assert.That(filter.sharedMesh, Is.SameAs(candidate));
        }

        [Test]
        public void 뒤쪽입력검사가실패해도_앞사본과재질을누출하거나원본참조를바꾸지않는다()
        {
            var invalid = new Mesh { name = "색 근거 없는 입력" };
            invalid.vertices = mesh.vertices; invalid.triangles = mesh.triangles; invalid.RecalculateNormals();
            var child = AddChild("뒤쪽 잘못된 입력", invalid);
            var ownedBefore = OwnedResourceCount();
            try
            {
                Assert.Throws<InvalidOperationException>(() => 행정동공통바탕Style.Apply(root));
                Assert.That(root.GetComponentsInChildren<MeshFilter>()[0].sharedMesh, Is.SameAs(mesh));
                Assert.That(child.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(invalid));
                Assert.That(OwnedResourceCount(), Is.EqualTo(ownedBefore));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().All(renderer => renderer.sharedMaterial == material), Is.True);
                Object.DestroyImmediate(child);
                scope = 행정동공통바탕Style.Apply(root);
                Assert.That(scope.Result.sourceDataPreserved, Is.True);
            }
            finally { Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void 정점이서로다른표면역할을공유하면_형상을쪼개지않고거절한다()
        {
            var invalid = new Mesh();
            invalid.vertices = new[] { new Vector3(0,.02f,0), new Vector3(0,.02f,1),
                new Vector3(1,.02f,0), new Vector3(0,4.16f,0) };
            invalid.triangles = new[] {0,1,2,0,3,1}; invalid.colors = Enumerable.Repeat(Color.white,4).ToArray();
            invalid.RecalculateNormals();
            root.GetComponentInChildren<MeshFilter>().sharedMesh = invalid;
            try
            {
                Assert.Throws<InvalidOperationException>(() => 행정동공통바탕Style.Apply(root));
                Assert.That(root.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(invalid));
            }
            finally { Object.DestroyImmediate(invalid); }
        }

        private GameObject AddChild(string name, Mesh value)
        {
            var child = new GameObject(name); child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = value;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
        }

        private static int OwnedResourceCount() => Resources.FindObjectsOfTypeAll<Mesh>()
            .Count(value => value.name.StartsWith("PrivateAdministrativeCommonBase:", StringComparison.Ordinal))
            + Resources.FindObjectsOfTypeAll<Material>()
            .Count(value => value.name.StartsWith("PrivateAdministrativeCommonBase:", StringComparison.Ordinal));

        private static Mesh CreateMesh()
        {
            var result = new Mesh { name = "행정동 바탕 역할 시험 도형" };
            var vertices = new[] {
                new Vector3(0,.02f,0),new Vector3(0,.02f,1),new Vector3(1,.02f,0),
                new Vector3(0,.08f,0),new Vector3(0,.08f,1),new Vector3(1,.08f,0),
                new Vector3(0,.12f,0),new Vector3(0,.12f,1),new Vector3(1,.12f,0),
                new Vector3(0,4.16f,0),new Vector3(0,4.16f,1),new Vector3(1,4.16f,0),
                new Vector3(0,.16f,0),new Vector3(0,4.16f,0),new Vector3(1,4.16f,0),
                new Vector3(0,.24f,0),new Vector3(0,.24f,1),new Vector3(1,.24f,0) };
            result.vertices = vertices; result.triangles = Enumerable.Range(0,vertices.Length).ToArray();
            result.colors = Enumerable.Range(0,vertices.Length).Select(index => new Color(.6f+index*.001f,.65f,.7f,1)).ToArray();
            result.uv = vertices.Select(value => new Vector2(value.x,value.z)).ToArray();
            result.uv2 = vertices.Select(value => new Vector2(value.y,value.x)).ToArray();
            result.tangents = Enumerable.Repeat(new Vector4(1,0,0,1),vertices.Length).ToArray();
            result.RecalculateNormals(); result.RecalculateBounds(); return result;
        }
    }
}
