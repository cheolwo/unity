using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정상세건물외형BasisTests
    {
        private const string Id = "vworld:al-d010:exact-id:row:7";
        private const string Feature = "exact-id";
        private const string Reference = "private/source-attributes.json#records[touslesjours]";
        private static string Hash => new('a', 64);
        private Mesh mesh;

        [TearDown] public void TearDown() { if (mesh != null) Object.DestroyImmediate(mesh); }

        private static 사가정공간표현BuildingData Source(double? observed = 31.5d, int? floors = 8) => new()
        {
            id = Id, sourceFeatureId = Feature, heightKind = observed > 0d ? "ObservedSourceHeight" : "SymbolicFallback4m",
            heightMeters = observed > 0d ? observed.Value : 4d,
            observedHeightMeters = observed, observedAboveGroundFloors = floors, heightPolicyRevision = "source-row.r1"
        };
        private static 사가정상세건물외형Basis.Result Resolve(사가정공간표현BuildingData source)
            => 사가정상세건물외형Basis.Resolve(Id, Feature, source, Reference, Hash);
        private static Vector2[] Rectangle() => new[]
        { new Vector2(100, 200), new Vector2(108, 200), new Vector2(108, 205), new Vector2(100, 205), new Vector2(100, 200) };

        [Test]
        public void 실제기재높이와_점포기호높이를_분리하고_출처를_복사한다()
        {
            var source = Source(); var basis = Resolve(source);
            Assert.That(basis.BodyHeightMeters, Is.EqualTo(31.5f));
            Assert.That(basis.StorefrontHeightMeters, Is.EqualTo(4f));
            Assert.That(basis.ObservedSourceHeightApplied, Is.True);
            Assert.That(basis.HeightProvenance, Is.EqualTo("ObservedSourceHeight"));
            Assert.That(basis.BuildingId, Is.EqualTo(Id)); Assert.That(basis.SourceFeatureId, Is.EqualTo(Feature));
            Assert.That(basis.SourceReference, Is.EqualTo(Reference)); Assert.That(basis.SourceSha256, Is.EqualTo(Hash));
            Assert.That(basis.ObservedAboveGroundFloors, Is.EqualTo(8));
            Assert.That(basis.HeightPolicyRevision, Is.EqualTo("source-row.r1"));
            source.observedHeightMeters = 99; source.observedAboveGroundFloors = 20; source.heightPolicyRevision = "changed";
            Assert.That(basis.ObservedHeightMeters, Is.EqualTo(31.5d));
            Assert.That(basis.ObservedAboveGroundFloors, Is.EqualTo(8));
            Assert.That(basis.HeightPolicyRevision, Is.EqualTo("source-row.r1"));
        }

        [TestCase(false)] [TestCase(true)]
        public void 정확건물이나_원천식별자가_다르면_옆건물자료를_쓰지않는다(bool changeFeature)
        {
            var source = Source(); if (changeFeature) source.sourceFeatureId = "neighbor"; else source.id = "neighbor";
            Assert.Throws<ArgumentException>(() => Resolve(source));
        }

        [TestCase(0d, 2)] [TestCase(null, 2)] [TestCase(null, null)]
        public void 영이나_미상높이는_층수가있어도_4미터로남기고_원문을보존한다(double? height, int? floors)
        {
            var source = Source(height, floors); var basis = Resolve(source);
            Assert.That(basis.BodyHeightMeters, Is.EqualTo(4f));
            Assert.That(basis.HeightProvenance, Is.EqualTo("SymbolicFallback4m"));
            Assert.That(basis.ObservedSourceHeightApplied, Is.False);
            Assert.That(basis.ObservedHeightMeters, Is.EqualTo(height));
            Assert.That(basis.ObservedAboveGroundFloors, Is.EqualTo(floors));
            Assert.That(source.observedHeightMeters, Is.EqualTo(height));
            Assert.That(source.observedAboveGroundFloors, Is.EqualTo(floors));
        }

        [Test]
        public void 원문높이근거가없는_표현값이나_양수만으로는_실제높이를_만들지않는다()
        {
            var source = Source(null, 8); source.heightMeters = 31.5d;
            Assert.That(Resolve(source).BodyHeightMeters, Is.EqualTo(4f));
            source.heightKind = "ObservedSourceHeight";
            Assert.That(Resolve(source).BodyHeightMeters, Is.EqualTo(4f));
            source.observedHeightMeters = 0;
            Assert.That(Resolve(source).BodyHeightMeters, Is.EqualTo(4f));
            source.observedHeightMeters = 31.5d; source.heightKind = "SymbolicFallback4m";
            Assert.Throws<ArgumentException>(() => Resolve(source));
        }

        [TestCase("hash")] [TestCase("reference")] [TestCase("mismatch")]
        [TestCase("nan")] [TestCase("negative")] [TestCase("height-kind")] [TestCase("floor")]
        public void 출처나_수치근거가_잘못되면_적용을_거절한다(string change)
        {
            var source = Source(); var hash = Hash; var reference = Reference;
            switch (change)
            {
                case "hash": hash = "not-a-sha"; break;
                case "reference": reference = ""; break;
                case "mismatch": source.heightMeters = 20; break;
                case "nan": source.observedHeightMeters = double.NaN; break;
                case "negative": source.observedHeightMeters = -1; break;
                case "height-kind": source.heightKind = "FloorCountEstimate"; break;
                case "floor": source.observedAboveGroundFloors = -1; break;
            }
            Assert.Throws<ArgumentException>(() => 사가정상세건물외형Basis.Resolve(Id, Feature, source, reference, hash));
        }

        [Test]
        public void 높은건물도_원래외곽을_수직유지하고_24미터단차를_추가하지않는다()
        {
            var ring = Rectangle(); var before = ring.ToArray();
            mesh = 사가정상세건물외형Basis.BuildMesh(ring, .16f, Resolve(Source()));
            Assert.That(ring, Is.EqualTo(before));
            Assert.That(Vector3.Distance(mesh.bounds.min,new Vector3(100,.16f,200)),Is.LessThan(.0001f));
            Assert.That(mesh.bounds.max.y, Is.EqualTo(31.66f).Within(.0001f));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(8f)); Assert.That(mesh.bounds.size.z, Is.EqualTo(5f));
            Assert.That(mesh.vertices.All(v => Math.Abs(v.y - .16f) < .0001f || Math.Abs(v.y - 31.66f) < .0001f), Is.True);
            Assert.That(mesh.vertices.Where(v => v.y > 30f).All(v => before.Contains(new Vector2(v.x, v.z))), Is.True);
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(12));
        }

        [Test]
        public void 미터UV와_유효한_법선접선이있고_사용삼각형으로만_경계를_계산한다()
        {
            mesh = 사가정상세건물외형Basis.BuildMesh(Rectangle(), .16f, Resolve(Source()));
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            var used = new Bounds(vertices[triangles[0]], Vector3.zero);
            foreach (var i in triangles) used.Encapsulate(vertices[i]);
            Assert.That(mesh.bounds, Is.EqualTo(used));
            Assert.That(mesh.uv[2].x - mesh.uv[1].x, Is.EqualTo(8f).Within(.0001f));
            Assert.That(mesh.uv[1].y - mesh.uv[0].y, Is.EqualTo(31.5f).Within(.0001f));
            Assert.That(mesh.normals.Length, Is.EqualTo(vertices.Length));
            Assert.That(mesh.tangents.Length, Is.EqualTo(vertices.Length));
            for (var i = 0; i < vertices.Length; i++)
            {
                Assert.That(mesh.normals[i].magnitude, Is.EqualTo(1f).Within(.001f));
                var tangent = mesh.tangents[i];
                Assert.That(float.IsFinite(tangent.x) && float.IsFinite(tangent.y) && float.IsFinite(tangent.z), Is.True);
                Assert.That(new Vector3(tangent.x, tangent.y, tangent.z).magnitude, Is.EqualTo(1f).Within(.001f));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void 오목외곽도_방향과무관하게_가운데를_메우지않는다(bool reverse)
        {
            var ring = new[] { new Vector2(0,0), new Vector2(4,0), new Vector2(4,1),
                new Vector2(1,1), new Vector2(1,4), new Vector2(0,4), new Vector2(0,0) };
            if (reverse) Array.Reverse(ring);
            mesh = 사가정상세건물외형Basis.BuildMesh(ring, 0, Resolve(Source(0, 2)));
            var v = mesh.vertices; var t = mesh.triangles; double topArea = 0;
            for (var i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i+1]]; var c = v[t[i+2]];
                if (a.y != 4 || b.y != 4 || c.y != 4) continue;
                var normal = Vector3.Cross(b-a,c-a);
                Assert.That(normal.y, Is.GreaterThan(0)); topArea += normal.magnitude * .5d;
                var center = (a+b+c)/3;
                Assert.That(center.x <= 1 || center.z <= 1, Is.True);
            }
            Assert.That(topArea, Is.EqualTo(7d).Within(.0001d));
        }

        [TestCase("open")] [TestCase("self-intersection")] [TestCase("duplicate")] [TestCase("nan")]
        public void 잘못된외곽은_볼록화하거나_새경로를_추측하지않는다(string change)
        {
            var ring = Rectangle();
            switch (change)
            {
                case "open": ring = ring.Take(4).ToArray(); break;
                case "self-intersection": (ring[1], ring[2]) = (ring[2], ring[1]); break;
                case "duplicate": ring[2] = ring[1]; break;
                case "nan": ring[1].x = float.NaN; break;
            }
            Assert.Throws<ArgumentException>(() => 사가정상세건물외형Basis.BuildMesh(ring, 0, Resolve(Source())));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(1e20f)]
        public void 기준높이가_표현불가능하면_비정상mesh를_만들지않는다(float baseY)
        {
            Assert.Throws<ArgumentException>(() => 사가정상세건물외형Basis.BuildMesh(Rectangle(), baseY, Resolve(Source())));
        }
    }
}
