using System;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정영상선분디오라마DemoTests
    {
        private static string HeightEvidence(string height="31.5", string floors="8") =>
            "{\"schema\":\"private-building-envelope-source-evidence.v1\",\"records\":[{"
            + "\"key\":\"fixture\",\"recordDeletionFlag\":\"active\",\"gisBuildingId\":\"123\","
            + "\"sourceRecordOrdinalZeroBased\":7,\"entireDbfRecordSha256\":\""+new string('c',64)+"\","
            + "\"selectedFields\":{\"A1\":{\"rawValue\":\"123\"},"
            + "\"A16\":{\"rawValue\":\""+height+"\",\"fieldName\":\"높이(m)\"},"
            + "\"A26\":{\"rawValue\":\""+floors+"\",\"fieldName\":\"지상층_수\"}}},"
            + "{\"key\":\"other-a\"},{\"key\":\"other-b\"}]}";

        [Test]
        public void 원자료행의_높이는_정확GIS와_원문필드가_일치할때만_본체에연결한다()
        {
            var source=HeightEvidence();
            var basis=사가정영상선분디오라마DemoCapture.ResolveBuildingEnvelopeJson(source,"fixture",
                "vworld:al-d010:123:row:7","123","C:/private/evidence.json",new string('a',64));
            Assert.That(basis.BodyHeightMeters,Is.EqualTo(31.5f));
            Assert.That(basis.StorefrontHeightMeters,Is.EqualTo(4f));
            Assert.That(basis.ObservedAboveGroundFloors,Is.EqualTo(8));
            Assert.That(basis.SourceReference,Does.EndWith("#records[fixture]"));
            source=source.Replace("\"A1\":{\"rawValue\":\"123\"}","\"A1\":{\"rawValue\":\"neighbor\"}");
            Assert.Throws<InvalidOperationException>(()=>사가정영상선분디오라마DemoCapture.ResolveBuildingEnvelopeJson(
                source,"fixture","vworld:al-d010:123:row:7","123","C:/private/evidence.json",new string('a',64)));
        }

        [Test]
        public void 높이0은_층수가있어도_환산하지않고_기호높이를유지하며_다른행은거절한다()
        {
            var source=HeightEvidence("0","2");
            var basis=사가정영상선분디오라마DemoCapture.ResolveBuildingEnvelopeJson(source,"fixture",
                "vworld:al-d010:123:row:7","123","C:/private/evidence.json",new string('a',64));
            Assert.That(basis.BodyHeightMeters,Is.EqualTo(4f));
            Assert.That(basis.ObservedHeightMeters,Is.EqualTo(0));
            Assert.That(basis.ObservedSourceHeightApplied,Is.False);
            Assert.Throws<InvalidOperationException>(()=>사가정영상선분디오라마DemoCapture.ResolveBuildingEnvelopeJson(
                source,"fixture","vworld:al-d010:123:row:8","123","C:/private/evidence.json",new string('a',64)));
            source=source.Replace("높이(m)","층수");
            Assert.Throws<InvalidOperationException>(()=>사가정영상선분디오라마DemoCapture.ResolveBuildingEnvelopeJson(
                source,"fixture","vworld:al-d010:123:row:7","123","C:/private/evidence.json",new string('a',64)));
        }

        [Test]
        public void 원근카메라는_공유chunk경계대신_실제양면삼각형가림거리를_판정한다()
        {
            var ray=new Ray(Vector3.zero,Vector3.forward);
            var a=new Vector3(-1,-1,2);var b=new Vector3(1,-1,2);var c=new Vector3(0,1,2);
            Assert.That(사가정영상선분디오라마DemoCapture.DetailedRayTriangleDistance(ray,a,b,c),Is.EqualTo(2).Within(.00001));
            Assert.That(사가정영상선분디오라마DemoCapture.DetailedRayTriangleDistance(ray,c,b,a),Is.EqualTo(2).Within(.00001));
            Assert.That(float.IsPositiveInfinity(사가정영상선분디오라마DemoCapture.DetailedRayTriangleDistance(
                new Ray(new Vector3(5,0,0),Vector3.forward),a,b,c)),Is.True);
        }

        [Test]
        public void 상세사본경계는_사용하지않는바탕판정점을_포함하지않고_원기하를보존한다()
        {
            var vertices=new[] { new Vector3(-500,0,-500),new Vector3(500,0,500),
                new Vector3(10,.16f,20),new Vector3(15,4.16f,20),new Vector3(10,.16f,25) };
            var before=(Vector3[])vertices.Clone();
            var bounds=사가정영상선분디오라마DemoCapture.DetailedUsedTriangleBounds(vertices,new[]{2,3,4});
            Assert.That(Vector3.Distance(bounds.min,new Vector3(10,.16f,20)),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(bounds.max,new Vector3(15,4.16f,25)),Is.LessThan(.0001f));
            Assert.That(vertices,Is.EqualTo(before));
            Assert.Throws<ArgumentException>(()=>사가정영상선분디오라마DemoCapture.DetailedUsedTriangleBounds(vertices,new[]{2,3,99}));
        }

        private static string Ref => "{\"path\":\"C:/private/fixture.json\",\"sha256\":\"" + new string('a', 64) + "\"}";
        private static string Input(string extra = "") => "{\"schema\":\"" + 사가정영상선분디오라마DemoCapture.InputSchema
            + "\",\"baseOverlay\":" + Ref + ",\"evidenceIndex\":" + Ref + ",\"previousStudy\":" + Ref
            + ",\"signLabels\":" + Ref + ",\"modulePreview\":" + Ref + ",\"roadCandidateLayer\":" + Ref
            + ",\"reviewedModuleKeys\":[\"appearance.awning.city-cover.04\"]" + extra + "}";

        [Test]
        public void 연구입력은_기존정본적용이나_사용자승인을_요청할수없다()
        {
            Assert.DoesNotThrow(() => 사가정영상선분디오라마DemoCapture.ValidateInputJson(Input()));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateInputJson(Input(",\"canonicalApplyRequested\":true")));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateInputJson(Input(",\"userApprovalRequested\":true")));
        }

        [Test]
        public void 지도영상외관각입력은_고정SHA참조가_필요하다()
        {
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateInputJson(
                Input().Replace(new string('a', 64), "unbound")));
        }

        private static string Road(string extra = "") => "{\"schema\":\"" + 사가정영상선분디오라마DemoCapture.RoadSchema
            + "\",\"candidates\":[{\"candidateId\":\"candidate-1\",\"sourceSha256\":\"" + new string('b', 64)
            + "\",\"segmentId\":\"segment-1\",\"segmentCode\":\"JN-S000001\",\"geometryHash\":\"" + new string('c', 64)
            + "\",\"status\":\"" + 사가정영상선분디오라마DemoCapture.CandidateStatus + "\"" + extra + "}]}";

        [Test]
        public void 공통관측창_선분후보를_승인이나_개별통과시각으로_승격하지않는다()
        {
            Assert.DoesNotThrow(() => 사가정영상선분디오라마DemoCapture.ValidateRoadLayerJson(Road()));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateRoadLayerJson(Road(",\"userApproved\":true")));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateRoadLayerJson(Road(",\"individualTimingConfirmed\":true")));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateRoadLayerJson(Road(",\"actualRouteConfirmed\":true")));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidateRoadLayerJson(Road(",\"sourceWindowEvenlyDivided\":true")));
        }

        [Test]
        public void 정면가설은_최장변대신_공개도로와가까운변을_선택한다()
        {
            var ring = new[] { new Vector2(0,0), new Vector2(20,0), new Vector2(20,5), new Vector2(0,5), new Vector2(0,0) };
            var roads = new[] { new[] { new Vector2(-2,-10), new Vector2(-2,15) } };
            Assert.That(사가정영상선분디오라마DemoCapture.SelectRoadFacingEdge(ring, roads), Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => 사가정영상선분디오라마DemoCapture.SelectRoadFacingEdge(ring, Array.Empty<Vector2[]>()));
        }

        [Test]
        public void 위경도변환은_기존동네원점과_미터단위를_유지한다()
        {
            var point = 사가정영상선분디오라마DemoCapture.ProjectWgs84(
                사가정공간표현OverlayComposer.OriginLatitude, 사가정공간표현OverlayComposer.OriginLongitude);
            Assert.That(point.x, Is.EqualTo(550).Within(.001f));
            Assert.That(point.y, Is.EqualTo(8).Within(.001f));
            Assert.Throws<ArgumentException>(() => 사가정영상선분디오라마DemoCapture.ProjectWgs84(double.NaN, 127));
            Assert.That(사가정영상선분디오라마DemoCapture.SymbolicHeight, Is.EqualTo(4f));
        }

        [Test]
        public void 별도연구건물은_기존동네를덮는_바탕판을_추가하지않는다()
        {
            var vertices = new[] { new Vector3(0,.02f,0), new Vector3(1,.02f,0), new Vector3(0,.02f,1),
                new Vector3(0,.16f,0), new Vector3(1,.16f,0), new Vector3(0,4.16f,0) };
            var triangles = new[] { 0,1,2, 3,4,5 };
            Assert.That(사가정영상선분디오라마DemoCapture.FilterStudyBuildingTriangles(vertices, triangles),
                Is.EqualTo(new[] { 3,4,5 }));
            Assert.That(triangles, Is.EqualTo(new[] { 0,1,2,3,4,5 }), "입력 원문은 수정하지 않는다.");
            Assert.Throws<ArgumentException>(() => 사가정영상선분디오라마DemoCapture.FilterStudyBuildingTriangles(vertices, new[] { 0,1,99 }));
        }

        [Test]
        public void 충돌가림은_명시한옛건물만_새사본에서제외하고_원문을보존한다()
        {
            var source = new 공간참고자료 { revision = "fixture", roads = new[] { new 참고도로 { id = "road-1", x1 = 1, z1 = 2, x2 = 3, z2 = 4 } },
                buildings = new[] { new 참고건물 { id = "legacy-a", height = 4 }, new 참고건물 { id = "legacy-b", height = 8 } } };
            var before = JsonUtility.ToJson(source);
            var preview = 사가정영상선분디오라마DemoCapture.CreatePreviewBaselineCopy(source, new[] { "legacy-a" });
            Assert.That(preview.buildings, Has.Length.EqualTo(1));
            Assert.That(preview.buildings[0].id, Is.EqualTo("legacy-b"));
            Assert.That(preview.roads[0].id, Is.EqualTo("road-1"));
            Assert.That(preview.roads[0], Is.Not.SameAs(source.roads[0]));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => 사가정영상선분디오라마DemoCapture.CreatePreviewBaselineCopy(source, new[] { "unknown" }));
            Assert.Throws<ArgumentException>(() => 사가정영상선분디오라마DemoCapture.CreatePreviewBaselineCopy(source, new[] { "legacy-a", "legacy-a" }));
        }

        [Test]
        public void 기하충돌근거는_동일건물확정과구분하고_양의교차면적을요구한다()
        {
            var hash = new string('a', 64);
            var mask = "{\"schema\":\"private-neighborhood-demo-preview-occlusion-mask.v1\","
                + "\"suppressionBasis\":\"GeometricOverlapOnlyNotIdentityBinding\",\"baseMapSha256\":\"" + hash
                + "\",\"baseOverlaySha256\":\"" + hash + "\",\"evidenceIndexSha256\":\"" + hash
                + "\",\"suppressedLegacyBuildings\":[{\"legacyBuildingId\":\"legacy-a\",\"studyTargetBuildingId\":\"study-a\","
                + "\"intersectionAreaSquareMeters\":10,\"intersectionOverUnion\":0.8,\"smallerFootprintCoverage\":0.9}]}";
            Assert.DoesNotThrow(() => 사가정영상선분디오라마DemoCapture.ValidatePreviewMaskJson(mask));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidatePreviewMaskJson(mask.Replace("GeometricOverlapOnlyNotIdentityBinding", "same-building-confirmed")));
            Assert.Throws<InvalidOperationException>(() => 사가정영상선분디오라마DemoCapture.ValidatePreviewMaskJson(mask.Replace("SquareMeters\":10", "SquareMeters\":0")));
        }
    }
}
