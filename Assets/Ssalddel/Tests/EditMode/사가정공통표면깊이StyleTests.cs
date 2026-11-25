using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정공통표면깊이StyleTests
    {
        private GameObject root;
        private Material material;
        private readonly List<Mesh> meshes = new();
        private 사가정공통표면깊이Style.Scope scope;

        [SetUp] public void SetUp()
        {
            root = new GameObject("공통 표면 시험");
            material = new Material(Shader.Find("Ssalddel/ObservationPrimitive"));
        }
        [TearDown] public void TearDown()
        {
            scope?.Dispose(); scope = null;
            Object.DestroyImmediate(root); Object.DestroyImmediate(material);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        [Test] public void 지붕외곽네변만띠를만들고_삼각형내부대각선을제외한다()
        {
            var mesh = CreateMesh(); var filter = Add("첫째 · 불투명", mesh);
            var fingerprint = 사가정공통표면깊이Style.Fingerprint(mesh);
            scope = 사가정공통표면깊이Style.Apply(root);
            Assert.That(scope.Result.roofBoundaryEdgeCount, Is.EqualTo(4));
            Assert.That(scope.Result.excludedInternalRoofEdgeCount, Is.EqualTo(1));
            Assert.That(scope.Result.roofComponentCount, Is.EqualTo(1));
            Assert.That(scope.Result.addedVertexCount, Is.EqualTo(16));
            Assert.That(scope.Result.addedRendererCount, Is.EqualTo(1));
            Assert.That(filter.sharedMesh, Is.Not.SameAs(mesh));
            Assert.That(filter.sharedMesh.vertices, Is.EqualTo(mesh.vertices));
            Assert.That(filter.sharedMesh.triangles, Is.EqualTo(mesh.triangles));
            Assert.That(scope.Result.wallBaseVertexCount, Is.EqualTo(2));
            Assert.That(사가정공통표면깊이Style.Fingerprint(mesh), Is.EqualTo(fingerprint));
        }

        [Test] public void 중심반경없이_먼공통배치에도적용하고_공유사본과변환을보존한다()
        {
            var mesh = CreateMesh(); var first = Add("근접 · 불투명", mesh); var far = Add("주변 · 불투명", mesh);
            far.transform.localPosition = new Vector3(700, 0, -500); far.transform.localRotation = Quaternion.Euler(0, 30, 0);
            var beforePosition = far.transform.localPosition; var beforeRotation = far.transform.localRotation;
            scope = 사가정공통표면깊이Style.Apply(root);
            Assert.That(scope.Result.selectedRendererCount, Is.EqualTo(2));
            Assert.That(scope.Result.roofBoundaryEdgeCount, Is.EqualTo(8));
            Assert.That(scope.Result.sourceMeshCount, Is.EqualTo(1));
            Assert.That(first.sharedMesh, Is.SameAs(far.sharedMesh));
            Assert.That(far.transform.localPosition, Is.EqualTo(beforePosition)); Assert.That(far.transform.localRotation, Is.EqualTo(beforeRotation));
        }

        [Test] public void 같은도로평면의_정확한12변팬만3mm올리고_strip와비원형팬을유지한다()
        {
            var mesh = CreateMesh(true); var filter = Add("도로 포함 · 불투명", mesh); var source = mesh.vertices;
            scope = 사가정공통표면깊이Style.Apply(root); var candidate = filter.sharedMesh.vertices;
            Assert.That(scope.Result.roadFan12Count, Is.EqualTo(1));
            Assert.That(scope.Result.roadFanCount, Is.EqualTo(1));
            for (var i = 0; i < source.Length; i++)
            {
                Assert.That(candidate[i].x, Is.EqualTo(source[i].x)); Assert.That(candidate[i].z, Is.EqualTo(source[i].z));
                Assert.That(candidate[i].y, Is.EqualTo(source[i].y + (i >= 8 && i < 22 ? .003f : 0)).Within(.00001f));
            }
            Assert.That(filter.sharedMesh.triangles, Is.EqualTo(mesh.triangles));
        }

        [Test] public void 뒤쪽UV입력오류는_앞쪽참조를바꾸기전에거절하고사본을정리한다()
        {
            var firstMesh = CreateMesh(); var first = Add("유효 · 불투명", firstMesh);
            var invalid = CreateMesh(); invalid.uv2 = Array.Empty<Vector2>(); Add("오류 · 불투명", invalid);
            var count = root.GetComponentsInChildren<MeshRenderer>().Length;
            Assert.Throws<InvalidOperationException>(() => 사가정공통표면깊이Style.Apply(root));
            Assert.That(first.sharedMesh, Is.SameAs(firstMesh));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(count));
        }

        [Test] public void 중앙표시8팬은125mm에서만올리고_다른평면과12팬표시는거절한다()
        {
            var mesh = CreateMesh(); var vertices = mesh.vertices.ToList(); var uv = mesh.uv2.ToList(); var triangles = mesh.triangles.ToList();
            Fan(vertices, uv, triangles, new Vector3(20,.125f,0), 8, false);
            Fan(vertices, uv, triangles, new Vector3(30,.125f,0), 12, false);
            Fan(vertices, uv, triangles, new Vector3(40,.1f,0), 8, false);
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(1, uv); mesh.SetColors(Enumerable.Repeat(Color.white, vertices.Count).ToList());
            var filter = Add("중앙 표시 · 불투명", mesh); var source = mesh.vertices;
            scope = 사가정공통표면깊이Style.Apply(root); var candidate = filter.sharedMesh.vertices;
            Assert.That(scope.Result.roadFanCount, Is.EqualTo(1)); Assert.That(scope.Result.roadFan8Count, Is.EqualTo(1)); Assert.That(scope.Result.roadFan12Count, Is.Zero);
            for (var i = 0; i < source.Length; i++) Assert.That(candidate[i].y, Is.EqualTo(source[i].y + (i >= 8 && i < 18 ? .003f : 0)).Within(.00001f));
        }

        [Test] public void 상세점포_그림자_표식_비활성객체는공통층에서제외한다()
        {
            var mesh = CreateMesh(); Add("공통 · 불투명", mesh);
            var detail = Add("역세권 상세", mesh); var shadow = Add("접지 그림자", mesh); var label = Add("label", mesh);
            var inactive = Add("비활성 · 불투명", mesh); inactive.gameObject.SetActive(false);
            scope = 사가정공통표면깊이Style.Apply(root);
            Assert.That(scope.Result.selectedRendererCount, Is.EqualTo(1)); Assert.That(scope.Result.excludedRendererCount, Is.EqualTo(4));
            Assert.That(detail.sharedMesh, Is.SameAs(mesh)); Assert.That(shadow.sharedMesh, Is.SameAs(mesh)); Assert.That(label.sharedMesh, Is.SameAs(mesh));
            Assert.That(inactive.sharedMesh, Is.SameAs(mesh));
        }

        [Test] public void 겹친적용을거절하고_기존활성층은유지한다()
        {
            var filter = Add("공통 · 불투명", CreateMesh()); scope = 사가정공통표면깊이Style.Apply(root); var candidate = filter.sharedMesh;
            Assert.Throws<InvalidOperationException>(() => 사가정공통표면깊이Style.Apply(root));
            Assert.That(filter.sharedMesh, Is.SameAs(candidate)); Assert.That(scope.Result.disposed, Is.False);
        }

        [Test] public void 반복해제가_원본Mesh와Material을정확히복구하고_소유표현을제거한다()
        {
            var mesh = CreateMesh(true); var filter = Add("공통 · 불투명", mesh); var original = 사가정공통표면깊이Style.Fingerprint(mesh);
            scope = 사가정공통표면깊이Style.Apply(root); var candidate = filter.sharedMesh;
            scope.Dispose(); scope.Dispose();
            Assert.That(filter.sharedMesh, Is.SameAs(mesh)); Assert.That(filter.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
            Assert.That(candidate == null, Is.True); Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
            Assert.That(scope.Result.sourceReferencesRestored, Is.True); Assert.That(scope.Result.originalMeshDataPreserved, Is.True);
            Assert.That(사가정공통표면깊이Style.Fingerprint(mesh), Is.EqualTo(original));
        }

        [Test] public void 세삼각형이공유하는지붕edge는_외곽으로오인하지않고거절한다()
        {
            var mesh = CreateMesh(); var vertices = mesh.vertices.ToList(); var colors = mesh.colors.ToList(); var uv = mesh.uv2.ToList();
            vertices.Add(new Vector3(12, 4, 2)); colors.Add(Color.white); uv.Add(new Vector2(2, 0));
            var triangles = mesh.triangles.Concat(new[] { 0, 2, 8 }).ToArray(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(1, uv); mesh.triangles = triangles;
            var filter = Add("비다양체 · 불투명", mesh);
            Assert.Throws<InvalidOperationException>(() => 사가정공통표면깊이Style.Apply(root)); Assert.That(filter.sharedMesh, Is.SameAs(mesh));
        }

        [Test] public void 아주뾰족한지붕도_명암띠가원래삼각형의실루엣밖으로나가지않는다()
        {
            var mesh = new Mesh { name = "뾰족한 지붕" }; meshes.Add(mesh);
            mesh.vertices = new[] { new Vector3(0,4,0), new Vector3(10,4,0), new Vector3(.2f,4,.04f) };
            mesh.triangles = new[] { 0,1,2 }; mesh.colors = Enumerable.Repeat(Color.white, 3).ToArray();
            mesh.uv2 = Enumerable.Repeat(new Vector2(2,0), 3).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var filter = Add("좁은 지붕 · 불투명", mesh); scope = 사가정공통표면깊이Style.Apply(root);
            var ribbon = root.GetComponentsInChildren<MeshFilter>().Single(f => f != filter).sharedMesh;
            foreach (var point in ribbon.vertices)
            {
                var c = point.z / .04f; var b = (point.x - .2f * c) / 10;
                Assert.That(c, Is.GreaterThanOrEqualTo(-.0001f)); Assert.That(b, Is.GreaterThanOrEqualTo(-.0001f)); Assert.That(b+c, Is.LessThanOrEqualTo(1.0001f));
            }
        }

        private MeshFilter Add(string name, Mesh mesh)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var filter = go.AddComponent<MeshFilter>(); filter.sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material; return filter;
        }

        private Mesh CreateMesh(bool fans = false)
        {
            var vertices = new List<Vector3>
            {
                new(0,4,0),new(10,4,0),new(10,4,10),new(0,4,10),
                new(0,.16f,0),new(0,4,0),new(10,4,0),new(10,.16f,0)
            };
            var uv = Enumerable.Range(0, 8).Select(i => new Vector2(i < 4 ? 2 : 1, 0)).ToList();
            var triangles = new List<int> { 0,1,2,0,2,3,4,5,6,4,6,7 };
            if (fans)
            {
                Fan(vertices, uv, triangles, new Vector3(20,.1f,0), 12, false);
                Fan(vertices, uv, triangles, new Vector3(30,.1f,0), 12, true);
                Fan(vertices, uv, triangles, new Vector3(50,.1f,0), 10, false);
                var at = vertices.Count; vertices.AddRange(new[] { new Vector3(40,.1f,0),new Vector3(42,.1f,0),new Vector3(42,.1f,2),new Vector3(40,.1f,2) });
                uv.AddRange(Enumerable.Repeat(Vector2.zero, 4)); triangles.AddRange(new[] { at,at+1,at+2,at,at+2,at+3 });
            }
            var mesh = new Mesh { name = "표면 시험 원본" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.SetColors(Enumerable.Repeat(Color.white, vertices.Count).ToList()); mesh.SetUVs(1, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh); return mesh;
        }

        private static void Fan(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 center, int sides, bool irregular)
        {
            var first = vertices.Count; vertices.Add(center); uv.Add(Vector2.zero);
            for (var i = 0; i <= sides; i++)
            {
                var angle = i * Mathf.PI * 2 / sides; var radius = irregular && i == 3 ? 2.3f : 2;
                vertices.Add(center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius)); uv.Add(Vector2.zero);
            }
            for (var i = 0; i < sides; i++) triangles.AddRange(new[] { first, first+i+1, first+i+2 });
        }
    }
}
