using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.WorkflowRules.Contracts;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "단일 건물의 원본 내부ring 표시 opt-in과 지붕·내벽·선택 경계 및 기존 출력 보존을 검증한다.",
        Boundary = "합성 fixture 검사는 실제 원본·Game View·통행·현재 형상 확정 또는 E 승격이 아니다.")]
    public sealed class 행정동건물내부Ring검토Tests
    {
        private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        [Test]
        public void 선택입력이없으면_기존Mesh출력을그대로유지한다()
        {
            var snapshot = Snapshot();
            var first = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var second = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var a = 행정동디오라마검토MeshBuilder.Build(snapshot, first.Add);
            var b = 행정동디오라마검토MeshBuilder.Build(snapshot, second.Add, null);
            Assert.That(Stream(first), Is.EqualTo(Stream(second)));
            Assert.That(a.TotalVertexCount, Is.EqualTo(b.TotalVertexCount));
            Assert.That(a.TotalIndexCount, Is.EqualTo(b.TotalIndexCount));
            Assert.That(a.FocusedBuildingCount, Is.Zero);
        }

        [Test]
        public void 수치기호높이의출처분류를바로잡아도_기존기하와팔레트를유지한다()
        {
            var sourceHeight = Snapshot(Hash, 4d);
            var symbolic = Snapshot(Hash, 4d, "Fixture:SymbolicHeightFallback");
            sourceHeight.TryGetBuilding(행정동건물내부Ring검토Geometry.TargetBuilding, out var original);
            symbolic.TryGetBuilding(행정동건물내부Ring검토Geometry.TargetBuilding, out var corrected);
            Assert.That(original.DisplayedHeightKind, Is.EqualTo("SourceHeight"));
            Assert.That(corrected.DisplayedHeightKind, Is.EqualTo("SymbolicFallback4m"));
            var before = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var after = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            행정동디오라마검토MeshBuilder.Build(sourceHeight, before.Add);
            행정동디오라마검토MeshBuilder.Build(symbolic, after.Add);
            Assert.That(Stream(before), Is.EqualTo(Stream(after)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 정확fallback접미사가있으면_층수와관계없이기존4m기호높이를보존한다(bool floorsPresent)
        {
            var snapshot = Snapshot(Hash, 4d, "Fixture:SymbolicHeightFallback", floorsPresent ? 6 : (int?)null);
            snapshot.TryGetBuilding(행정동건물내부Ring검토Geometry.TargetBuilding, out var building);
            Assert.That(building.DisplayedHeight, Is.EqualTo(4d));
            Assert.That(building.DisplayedHeightKind, Is.EqualTo("SymbolicFallback4m"));
        }

        [TestCase(null)]
        [TestCase(8d)]
        public void fallback접미사가있는데4m수치가없으면_암묵추론하지않고거절한다(double? height)
        {
            Assert.Throws<InvalidOperationException>(() => Snapshot(Hash, height,
                "Fixture:SymbolicHeightFallback", 6));
        }

        [Test]
        public void 단일대상만_내벽과빈지붕으로바꾸고_다른기하와높이는유지한다()
        {
            var snapshot = Snapshot(); var geometry = Geometry(snapshot);
            var before = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var after = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            var baseReport = 행정동디오라마검토MeshBuilder.Build(snapshot, before.Add);
            var report = 행정동디오라마검토MeshBuilder.Build(snapshot, after.Add, geometry);
            Assert.That(WithoutTarget(before), Is.EqualTo(WithoutTarget(after)));
            Assert.That(report.BuildingCount, Is.EqualTo(baseReport.BuildingCount));
            Assert.That(report.RoadSegmentCount, Is.EqualTo(baseReport.RoadSegmentCount));
            Assert.That(report.FocusedBuildingCount, Is.EqualTo(1));
            Assert.That(report.FocusedInteriorRingCount, Is.EqualTo(1));
            Assert.That(report.FocusedInteriorWallSegmentCount, Is.EqualTo(4));
            Assert.That(report.FocusedRoofTriangleCount, Is.EqualTo(8));
            Assert.That(report.FocusedBuildingGeometryHashSha256, Is.EqualTo(Hash));
            Assert.That(after.SelectMany(v => v.Vertices).Max(v => v.y),
                Is.EqualTo(before.SelectMany(v => v.Vertices).Max(v => v.y)));
            var roof = after.SelectMany(Triangles).Where(t => t.All(v =>
                Mathf.Abs(v.y - 4.16f) < .00001f && InTarget(v))).ToArray();
            Assert.That(roof, Has.Length.EqualTo(8));
            Assert.That(roof.All(t => Vector3.Cross(t[1] - t[0], t[2] - t[0]).y > 0f), Is.True);
            Assert.That(roof.Sum(t => Vector3.Cross(t[1] - t[0], t[2] - t[0]).y * .5f), Is.EqualTo(300f));
            Assert.That(roof.All(t => geometry.ContainsRoof(t.Average(v => v.x), t.Average(v => v.z), false)), Is.True);
        }

        [TestCase("projection")]
        [TestCase("height")]
        public void 정확결속이변경되면_어떤Batch도출력하지않는다(string change)
        {
            var geometry = Geometry(Snapshot());
            var changed = Snapshot(change == "projection" ? new string('B', 64) : Hash,
                change == "height" ? 8d : (double?)null);
            var emitted = new List<행정동디오라마검토MeshBuilder.MeshBatch>();
            Assert.Throws<InvalidOperationException>(() =>
                행정동디오라마검토MeshBuilder.Build(changed, emitted.Add, geometry));
            Assert.That(emitted, Is.Empty);
        }

        [TestCase("target")]
        [TestCase("authority")]
        [TestCase("closure")]
        [TestCase("winding")]
        [TestCase("index")]
        [TestCase("cover-hole")]
        [TestCase("duplicate")]
        public void 불명확하거나빈공간을덮는입력을거절한다(string change)
        {
            var data = Data();
            switch (change)
            {
                case "target": data.buildingStableId = "other"; break;
                case "authority": data.traversalReady = true; break;
                case "closure": data.interiorRingsMm[0][4][0] += 1d; break;
                case "winding": Array.Reverse(data.interiorRingsMm[0]); break;
                case "index": data.roofTriangleIndices[0] = 999; break;
                case "cover-hole": data.roofTriangleIndices = new[] { 0, 2, 1, 0, 3, 2 }; break;
                case "duplicate": data.roofTriangleIndices = data.roofTriangleIndices.Concat(new[] { 0, 7, 1 }).ToArray(); break;
            }
            Assert.That(행정동건물내부Ring검토Geometry.TryCreate(data, Snapshot(),
                out var geometry, out var diagnostic), Is.False, diagnostic);
            Assert.That(geometry, Is.Null);
        }

        [Test]
        public void 외부Dto변경이_검증된기하를바꾸지않는다()
        {
            var data = Data();
            Assert.That(행정동건물내부Ring검토Geometry.TryCreate(data, Snapshot(),
                out var geometry, out var diagnostic), Is.True, diagnostic);
            var before = geometry.ExteriorRing[0].X;
            data.exteriorRingMm[0][0] = 123456;
            data.roofTriangleIndices[0] = 999;
            Assert.That(geometry.ExteriorRing[0].X, Is.EqualTo(before));
            Assert.That(geometry.RoofTriangleIndices[0], Is.Zero);
            Assert.DoesNotThrow(() => geometry.ValidateBinding(Snapshot()));
        }

        [TestCase("self-touch-counterexample")]
        [TestCase("collinear-overlap")]
        [TestCase("millimeter-crossing")]
        public void 내부경계의비인접접촉과작은교차를_지붕검사전에거절한다(string kind)
        {
            var data = Data();
            if (kind == "self-touch-counterexample")
            {
                // 독립검토에서 기존 Factory를 실제 통과한 ring+roof 반례의 좌표를 보존한다.
                data.exteriorRingMm = new[] { P(0, 0), P(0, 12), P(12, 12), P(12, 0), P(0, 0) };
                data.interiorRingsMm = new[] { new[] { P(6, 6), P(3, 9), P(3, 6), P(9, 6), P(9, 9), P(6, 6) } };
                data.roofVerticesMm = new[] { P(0, 0), P(0, 12), P(12, 12), P(12, 0), P(6, 6), P(3, 9), P(3, 6), P(9, 6), P(9, 9) };
                data.roofTriangleIndices = new[] { 0, 1, 6, 8, 4, 5, 5, 6, 1, 4, 0, 6, 8, 5, 1, 0, 4, 3, 8, 1, 2, 3, 4, 7, 7, 8, 2, 2, 3, 7 };
            }
            else if (kind == "collinear-overlap")
                data.interiorRingsMm = new[] { new[] { P(5, 5), P(5, 15), P(15, 15), P(15, 5), P(9, 5), P(12, 5), P(5, 5) } };
            else
                data.interiorRingsMm = new[] { new[] { P(5, 5), P(5.003, 5.002), P(5, 5.002), P(5.002, 5), P(5, 5) } };
            var snapshot = Snapshot(targetRingMm: data.exteriorRingMm);
            Assert.That(행정동건물내부Ring검토Geometry.TryCreate(data, snapshot,
                out var geometry, out var diagnostic), Is.False);
            Assert.That(diagnostic, Is.EqualTo("FocusedBuildingRingRingSelfIntersection"));
            Assert.That(geometry, Is.Null);
        }

        [TestCase(4d)]
        [TestCase(8d)]
        public void 최초입력도_실제높이가있는다른대상상태를거절한다(double height)
        {
            Assert.That(행정동건물내부Ring검토Geometry.TryCreate(Data(), Snapshot(Hash, height),
                out var geometry, out var diagnostic), Is.False, diagnostic);
            Assert.That(geometry, Is.Null);
        }

        [Test]
        public void 기존선택과달리_빈공간을점및위쪽Ray선택에서제외한다()
        {
            var snapshot = Snapshot(); var geometry = Geometry(snapshot);
            var host = new GameObject("inner-ring-fixture");
            try
            {
                var view = host.AddComponent<행정동디오라마검토View>();
                var point = new Vector2(-20f, -20f);
                view.BuildFromSnapshot(snapshot, false);
                Assert.That(view.TrySelectLocalPoint(point), Is.True);
                Assert.That(view.TrySelectWorldRay(new Ray(new Vector3(-20f, 100f, -20f), Vector3.down)), Is.True);
                view.BuildFromSnapshot(snapshot, false, geometry);
                Assert.That(view.TrySelectLocalPoint(point), Is.False);
                Assert.That(view.TrySelectWorldRay(new Ray(new Vector3(-20f, 100f, -20f), Vector3.down)), Is.False);
                Assert.That(view.TrySelectLocalPoint(new Vector2(-28f, -28f)), Is.True);
                Assert.That(view.TrySelectWorldRay(new Ray(new Vector3(-28f, 100f, -28f), Vector3.down)), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static 행정동건물내부Ring검토Geometry Geometry(행정동디오라마검토Snapshot snapshot)
        {
            Assert.That(행정동건물내부Ring검토Geometry.TryCreate(Data(), snapshot,
                out var geometry, out var diagnostic), Is.True, diagnostic);
            return geometry;
        }

        private static 행정동건물내부Ring검토Data Data() => new()
        {
            schemaVersion = 행정동건물내부Ring검토Geometry.Schema, revision = "synthetic.fixture.r1",
            contentHashSha256 = Hash, administrativeAreaStableId = 행정동건물내부Ring검토Geometry.TargetArea,
            buildingStableId = 행정동건물내부Ring검토Geometry.TargetBuilding,
            baseProjectionHashSha256 = Hash, baseBundleSha256 = Hash, baseGenerationHashSha256 = Hash,
            sourceArchiveSha256 = 행정동건물내부Ring검토Geometry.SourceArchiveHash,
            sourceGeometrySha256 = Hash, sourceRecordOrdinal = 0, sourceVintage = "synthetic.fixture.r1",
            sourceCrs = "EPSG:5186", sourceRightsStatus = "RightsConflictUnresolved",
            coordinateUnit = "CommonEnuMillimeters",
            coordinateFrame = new() { method = "WGS84-ECEF-ENU-at-zero-altitude",
                originLatitude = 37.580912, originLongitude = 127.088502, metersPerUnit = 1d },
            privateReviewVisualizationAllowed = true, sourcePolygonStructureObserved = true,
            exteriorRingMm = new[] { P(0, 0), P(20, 0), P(20, 20), P(0, 20), P(0, 0) },
            interiorRingsMm = new[] { new[] { P(5, 5), P(5, 15), P(15, 15), P(15, 5), P(5, 5) } },
            roofVerticesMm = new[] { P(0, 0), P(20, 0), P(20, 20), P(0, 20), P(5, 5), P(5, 15), P(15, 15), P(15, 5) },
            roofTriangleIndices = new[] { 0, 7, 1, 0, 4, 7, 1, 6, 2, 1, 7, 6, 2, 5, 3, 2, 6, 5, 3, 4, 0, 3, 5, 4 }
        };
        private static double[] P(double x, double z) => new[] { x * 1000d, z * 1000d };
        private static AdministrativeDongDioramaPoint Q(double x, double z) => new() { X = x, Z = z };
        private static AdministrativeDongDioramaBuilding B(string id, double low, double high, double? height, string evidence = "Fixture", int? floors = null) => new()
        {
            BuildingStableId = id, CategoryCode = "Fixture", EvidenceKindCode = evidence,
            HeightMeters = height,
            AboveGroundFloorCount = floors,
            Footprint = new[] { Q(low, low), Q(high, low), Q(high, high), Q(low, high), Q(low, low) }
        };
        private static 행정동디오라마검토Snapshot Snapshot(string projection = Hash, double? height = null, string evidence = "Fixture", int? floors = null, double[][] targetRingMm = null)
        {
            var area = 행정동건물내부Ring검토Geometry.TargetArea; var tile = "tile:fixture";
            var target = B(행정동건물내부Ring검토Geometry.TargetBuilding, 0, 20, height, evidence, floors);
            if (targetRingMm != null) target.Footprint = targetRingMm.Select(p => Q(p[0] / 1000d, p[1] / 1000d)).ToArray();
            return 행정동디오라마검토Projection.Create(new AdministrativeDongDioramaApplyResult
            {
                Accepted = true,
                Manifest = new()
                {
                    SchemaVersion = AdministrativeDongDioramaPolicy.SchemaVersion,
                    AdministrativeAreaStableId = area, DisplayName = "면목제3·8동 합성 시험",
                    SourceVintage = "fixture", ProjectionHashSha256 = projection,
                    ReadinessCode = AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                    DataPolicyCode = AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
                    ObservationPresentationOnly = true,
                    CoordinateFrame = new() { Method = "WGS84-ECEF-ENU-at-zero-altitude", MetersPerUnit = 1d },
                    Bounds = new() { MinX = 0, MinZ = 0, MaxX = 60, MaxZ = 60 },
                    Boundary = new[] { Q(0, 0), Q(60, 0), Q(60, 60), Q(0, 60), Q(0, 0) },
                    Tiles = new[] { new AdministrativeDongDioramaTileSummary
                        { TileStableId = tile, TileHashSha256 = Hash, BuildingCount = 2, RoadSegmentCount = 1 } }
                },
                Tiles = new[] { new AdministrativeDongDioramaTile
                {
                    SchemaVersion = AdministrativeDongDioramaPolicy.SchemaVersion,
                    AdministrativeAreaStableId = area, TileStableId = tile,
                    TileHashSha256 = Hash, ProjectionHashSha256 = projection,
                    Buildings = new[] { target, B("fixture:unmodified", 40, 50, 12d) },
                    Roads = new[] { new AdministrativeDongDioramaRoadSegment { RoadStableId = "fixture:road", From = Q(0, 35), To = Q(60, 35) } }
                } },
                DisplayOverlays = Array.Empty<AdministrativeDongDisplayOverlay>()
            });
        }

        private static IEnumerable<Vector3[]> Triangles(행정동디오라마검토MeshBuilder.MeshBatch batch)
        { for (var i = 0; i < batch.Triangles.Count; i += 3) yield return batch.Triangles.Skip(i).Take(3).Select(n => batch.Vertices[n]).ToArray(); }
        private static bool InTarget(Vector3 p) => p.x >= -30f && p.x <= -10f && p.z >= -30f && p.z <= -10f;
        private static float[] WithoutTarget(IEnumerable<행정동디오라마검토MeshBuilder.MeshBatch> batches)
            => batches.SelectMany(batch => Enumerable.Range(0, batch.Triangles.Count / 3)
                .Select(i => batch.Triangles.Skip(i * 3).Take(3).ToArray())
                .Where(ids => !ids.All(n => batch.Vertices[n].y >= .159f && InTarget(batch.Vertices[n])))
                .SelectMany(ids => ids.SelectMany(n => new[] { batch.Vertices[n].x, batch.Vertices[n].y, batch.Vertices[n].z,
                    batch.Colors[n].r, batch.Colors[n].g, batch.Colors[n].b, batch.Colors[n].a }))).ToArray();
        private static float[] Stream(IEnumerable<행정동디오라마검토MeshBuilder.MeshBatch> batches)
            => batches.SelectMany(b => new[] { (float)b.ChunkX, b.ChunkZ }.Concat(b.Triangles.Select(n => (float)n))
                .Concat(b.Vertices.SelectMany(v => new[] { v.x, v.y, v.z }))
                .Concat(b.Colors.SelectMany(c => new[] { c.r, c.g, c.b, c.a }))).ToArray();
    }
}
