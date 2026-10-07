using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 행정동 후보 상태 사본의 경계·건물·도로 중심선을 2x2 결합 Mesh로만 표현한다.
    /// 바닥과 도로 폭은 검토용 시각 기호이며 필지·차선·보도·통행 권위가 아니다.
    /// </summary>
    public static class 행정동디오라마검토MeshBuilder
    {
        public const int ChunkAxisCount = 2;
        public const int MaximumRendererCount = ChunkAxisCount * ChunkAxisCount;
        public const int MaximumVerticesPerBatch = 500_000;
        public const float SymbolicRoadWidth = 2.4f;
        private const float GroundY = .02f;
        private const float RoadY = .08f;
        private const float BoundaryY = .12f;
        private const float BuildingBaseY = .16f;
        private const float PrivateObservationOverlayY = .2f;
        private const float PrivateWalkNetworkOverlayY = .24f;
        private const float PrivateRoadDirectionOverlayY = .28f;
        private const float PrivateBiotopeOverlayY = .32f;
        private const float PrivateLaneMarkingOverlayY = .36f;
        private const float PrivateHorizontalContourTraceOverlayY = .40f;
        private const float CrosswalkGlyphHalfLength = 2.4f;
        private const float CrosswalkGlyphSpacing = .7f;
        private const float IntersectionGlyphRadius = 2.2f;
        private const float WalkNodeGlyphRadius = 1.35f;
        private const float WalkLinkGlyphWidth = .42f;
        private const float RoadDirectionSymbolRadius = .95f;
        private const float LaneMarkingOutlineWidth = .18f;
        private const float HorizontalContourTraceWidth = .08f;

        public sealed class MeshBatch
        {
            internal MeshBatch(string name, int chunkX, int chunkZ)
            {
                Name = name;
                ChunkX = chunkX;
                ChunkZ = chunkZ;
            }

            public string Name { get; }
            public int ChunkX { get; }
            public int ChunkZ { get; }
            public List<Vector3> Vertices { get; } = new();
            public List<int> Triangles { get; } = new();
            public List<Color> Colors { get; } = new();

            internal int Vertex(Vector3 value, Color color)
            {
                var index = Vertices.Count;
                Vertices.Add(value);
                Colors.Add(color);
                return index;
            }

            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                var first = Vertex(a, color);
                Vertex(b, color);
                Vertex(c, color);
                Vertex(d, color);
                Triangles.AddRange(new[]
                {
                    first, first + 1, first + 2,
                    first, first + 2, first + 3
                });
            }

            internal void Strip(Vector3 a, Vector3 b, float width, Color color)
            {
                var direction = b - a;
                if (direction.sqrMagnitude <= .000001f) return;
                var side = Vector3.Cross(direction.normalized, Vector3.up) * (width * .5f);
                Quad(a - side, a + side, b + side, b - side, color);
            }
        }

        public sealed class BuildReport
        {
            public string AdministrativeAreaStableId { get; internal set; }
            public string ProjectionHashSha256 { get; internal set; }
            public int RendererBatchCount { get; internal set; }
            public int BuildingCount { get; internal set; }
            public int RoadSegmentCount { get; internal set; }
            public int BoundarySegmentCount { get; internal set; }
            public string PrivateObservationOverlaySetHashSha256 { get; internal set; }
            public int CrosswalkPointCount { get; internal set; }
            public int IntersectionPointCount { get; internal set; }
            public int PrivateObservationOverlayVertexCount { get; internal set; }
            public string PrivateWalkNetworkOverlaySetHashSha256 { get; internal set; }
            public int WalkNetworkNodePointCount { get; internal set; }
            public int WalkNetworkLinkFragmentCount { get; internal set; }
            public int WalkNetworkLinkPathVertexCount { get; internal set; }
            public int WalkNetworkOutputPathVertexCount =>
                WalkNetworkNodePointCount + WalkNetworkLinkPathVertexCount;
            public int PrivateWalkNetworkOverlayVertexCount { get; internal set; }
            public string PrivateRoadDirectionOverlaySetHashSha256 { get; internal set; }
            public int RoadDirectionPointCandidateCount { get; internal set; }
            public int RoadDirectionUnresolvedCount { get; internal set; }
            public int RoadDirectionRenderedArrowCount { get; internal set; }
            public int PrivateRoadDirectionOverlayVertexCount { get; internal set; }
            public string PrivateBiotopeOverlaySetHashSha256 { get; internal set; }
            public int BiotopeCandidateCount { get; internal set; }
            public int BiotopePolygonPartCount { get; internal set; }
            public int BiotopeInteriorRingCount { get; internal set; }
            public int BiotopeRingVertexCount { get; internal set; }
            public int BiotopeSourceSegmentCount { get; internal set; }
            public int BiotopeRenderedSegmentCount { get; internal set; }
            public int BiotopeSkippedAtOrBelowOneMillimeterSegmentCount { get; internal set; }
            public int PrivateBiotopeOverlayVertexCount { get; internal set; }
            public int PrivateBiotopeOverlayIndexCount { get; internal set; }
            public string PrivateLaneMarkingOverlaySetHashSha256 { get; internal set; }
            public int LaneMarkingCandidateCount { get; internal set; }
            public int LaneMarkingFragmentCount { get; internal set; }
            public int LaneMarkingPathVertexCount { get; internal set; }
            public int LaneMarkingInputSegmentCount { get; internal set; }
            public int LaneMarkingRenderedSegmentCount { get; internal set; }
            public int LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount { get; internal set; }
            public int PrivateLaneMarkingOverlayVertexCount { get; internal set; }
            public int PrivateLaneMarkingOverlayIndexCount { get; internal set; }
            public string PrivateHorizontalContourTraceOverlaySetHashSha256
                { get; internal set; }
            public int HorizontalContourTraceCandidateCount { get; internal set; }
            public int HorizontalContourTraceFragmentCount { get; internal set; }
            public int HorizontalContourTracePathVertexCount { get; internal set; }
            public int HorizontalContourTraceInputSegmentCount { get; internal set; }
            public int HorizontalContourTraceRenderedSegmentCount { get; internal set; }
            public int HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount
                { get; internal set; }
            public int PrivateHorizontalContourTraceOverlayVertexCount { get; internal set; }
            public int PrivateHorizontalContourTraceOverlayIndexCount { get; internal set; }
            public int TotalVertexCount { get; internal set; }
            public int TotalIndexCount { get; internal set; }
            public int MaximumBatchVertexCount { get; internal set; }
            public Bounds ModelBounds { get; internal set; }
            public string FocusedBuildingGeometryHashSha256 { get; internal set; }
            public string FocusedBuildingStableId { get; internal set; }
            public int FocusedBuildingCount { get; internal set; }
            public int FocusedInteriorRingCount { get; internal set; }
            public int FocusedInteriorWallSegmentCount { get; internal set; }
            public int FocusedRoofTriangleCount { get; internal set; }
        }

        public static BuildReport Build(행정동디오라마검토Snapshot snapshot,
            Action<MeshBatch> emit, 행정동건물내부Ring검토Geometry focusedGeometry = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (!snapshot.ObservationPresentationOnly || snapshot.DistributionApproved
                || snapshot.TraversalReady || snapshot.GameplayReady)
                throw new InvalidOperationException("AdministrativeDongReviewSnapshotAuthorityInvalid");
            // 단일 대상의 별도 비공개 검토만 허용한다. 결속 실패는 batch 출력 전에 차단한다.
            focusedGeometry?.ValidateBinding(snapshot);

            var chunks = new MeshBatch[ChunkAxisCount, ChunkAxisCount];
            for (var x = 0; x < ChunkAxisCount; x++)
            for (var z = 0; z < ChunkAxisCount; z++)
                chunks[x, z] = new MeshBatch(
                    $"{snapshot.DisplayName} · 행정동 도형 검토 · chunk:{x}:{z}", x, z);

            var report = new BuildReport
            {
                AdministrativeAreaStableId = snapshot.AdministrativeAreaStableId,
                ProjectionHashSha256 = snapshot.ProjectionHashSha256,
                PrivateObservationOverlaySetHashSha256 =
                    snapshot.PrivateObservationOverlay?.OverlaySetHashSha256 ?? string.Empty,
                PrivateWalkNetworkOverlaySetHashSha256 =
                    snapshot.PrivateWalkNetworkOverlay?.OverlaySetHashSha256 ?? string.Empty,
                PrivateRoadDirectionOverlaySetHashSha256 =
                    snapshot.PrivateRoadDirectionOverlay?.OverlaySetHashSha256 ?? string.Empty,
                PrivateBiotopeOverlaySetHashSha256 =
                    snapshot.PrivateBiotopeOverlay?.OverlaySetHashSha256 ?? string.Empty,
                PrivateLaneMarkingOverlaySetHashSha256 =
                    snapshot.PrivateLaneMarkingOverlay?.OverlaySetHashSha256 ?? string.Empty,
                PrivateHorizontalContourTraceOverlaySetHashSha256 =
                    snapshot.PrivateHorizontalContourTraceOverlay?.OverlaySetHashSha256
                    ?? string.Empty
            };
            BuildNeutralGround(snapshot, chunks);
            BuildRoads(snapshot, chunks, report);
            BuildBoundary(snapshot, chunks, report);
            BuildPrivateObservationOverlay(snapshot, chunks, report);
            BuildPrivateWalkNetworkOverlay(snapshot, chunks, report);
            BuildPrivateRoadDirectionOverlay(snapshot, chunks, report);
            BuildPrivateBiotopeOverlay(snapshot, chunks, report);
            BuildPrivateLaneMarkingOverlay(snapshot, chunks, report);
            BuildPrivateHorizontalContourTraceOverlay(snapshot, chunks, report);
            BuildBuildings(snapshot, chunks, report, focusedGeometry);

            var maximumHeight = snapshot.Buildings.Count == 0
                ? 2f
                : (float)snapshot.Buildings.Max(value => value.DisplayedHeight);
            var padding = GroundPadding(snapshot.Bounds);
            report.ModelBounds = new Bounds(
                new Vector3(0f, maximumHeight * .5f,
                    0f),
                new Vector3((float)snapshot.Bounds.Width + padding * 2f,
                    Mathf.Max(2f, maximumHeight),
                    (float)snapshot.Bounds.Depth + padding * 2f));

            for (var x = 0; x < ChunkAxisCount; x++)
            for (var z = 0; z < ChunkAxisCount; z++)
            {
                var batch = chunks[x, z];
                if (batch.Vertices.Count == 0) continue;
                if (batch.Vertices.Count != batch.Colors.Count)
                    throw new InvalidOperationException(
                        "AdministrativeDongReviewVertexColorStreamMismatch");
                if (batch.Vertices.Count > MaximumVerticesPerBatch)
                    throw new InvalidOperationException(
                        "AdministrativeDongReviewMeshVertexBudgetExceeded");
                emit(batch);
                report.RendererBatchCount++;
                report.TotalVertexCount += batch.Vertices.Count;
                report.TotalIndexCount += batch.Triangles.Count;
                report.MaximumBatchVertexCount = Math.Max(report.MaximumBatchVertexCount,
                    batch.Vertices.Count);
            }
            if (report.RendererBatchCount > MaximumRendererCount)
                throw new InvalidOperationException(
                    "AdministrativeDongReviewRendererBudgetExceeded");
            return report;
        }

        private static void BuildNeutralGround(행정동디오라마검토Snapshot snapshot,
            MeshBatch[,] chunks)
        {
            var padding = GroundPadding(snapshot.Bounds);
            var minX = (float)snapshot.Bounds.MinX - padding;
            var minZ = (float)snapshot.Bounds.MinZ - padding;
            var maxX = (float)snapshot.Bounds.MaxX + padding;
            var maxZ = (float)snapshot.Bounds.MaxZ + padding;
            var width = (maxX - minX) / ChunkAxisCount;
            var depth = (maxZ - minZ) / ChunkAxisCount;
            var colors = new[]
            {
                new Color(.47f, .46f, .43f), new Color(.49f, .48f, .45f),
                new Color(.46f, .45f, .42f), new Color(.48f, .47f, .44f)
            };
            for (var x = 0; x < ChunkAxisCount; x++)
            for (var z = 0; z < ChunkAxisCount; z++)
            {
                var x0 = minX + x * width;
                var z0 = minZ + z * depth;
                chunks[x, z].Quad(
                    new Vector3(x0, GroundY, z0),
                    new Vector3(x0, GroundY, z0 + depth),
                    new Vector3(x0 + width, GroundY, z0 + depth),
                    new Vector3(x0 + width, GroundY, z0),
                    colors[(x + z * ChunkAxisCount) % colors.Length]);
            }
        }

        private static void BuildRoads(행정동디오라마검토Snapshot snapshot,
            MeshBatch[,] chunks, BuildReport report)
        {
            foreach (var road in snapshot.Roads)
            {
                var a = Point(road.From, RoadY);
                var b = Point(road.To, RoadY);
                ChunkAt(snapshot.Bounds, chunks,
                    new Vector2((a.x + b.x) * .5f, (a.z + b.z) * .5f))
                    .Strip(a, b, SymbolicRoadWidth, new Color(.23f, .26f, .28f));
                report.RoadSegmentCount++;
            }
        }

        private static void BuildBoundary(행정동디오라마검토Snapshot snapshot,
            MeshBatch[,] chunks, BuildReport report)
        {
            for (var index = 0; index < snapshot.Boundary.Count; index++)
            {
                var next = (index + 1) % snapshot.Boundary.Count;
                var a = Point(snapshot.Boundary[index], BoundaryY);
                var b = Point(snapshot.Boundary[next], BoundaryY);
                ChunkAt(snapshot.Bounds, chunks,
                    new Vector2((a.x + b.x) * .5f, (a.z + b.z) * .5f))
                    .Strip(a, b, 1.2f, new Color(.83f, .66f, .24f));
                report.BoundarySegmentCount++;
            }
        }

        private static void BuildBuildings(행정동디오라마검토Snapshot snapshot,
            MeshBatch[,] chunks, BuildReport report,
            행정동건물내부Ring검토Geometry focusedGeometry)
        {
            foreach (var building in snapshot.Buildings)
            {
                var polygon = building.Footprint.Select(value =>
                    new Vector2((float)value.X, (float)value.Z)).ToArray();
                if (polygon.Length < 3) continue;
                var batch = ChunkAt(snapshot.Bounds, chunks,
                    new Vector2((float)building.CentroidX, (float)building.CentroidZ));
                var height = Mathf.Clamp((float)building.DisplayedHeight, 2f, 300f);
                // 기존 r7에서 수치 4m로 전달된 fallback은 SourceHeight 팔레트로 그려졌다.
                // 출처 분류를 바로잡아도 이번 내부 경계 보완에서 주변 건물의 색은 유지한다.
                var colorHeightKind = building.DisplayedHeightKind == "SymbolicFallback4m"
                    && building.EvidenceKindCode.EndsWith(":SymbolicHeightFallback", StringComparison.Ordinal)
                    ? "SourceHeight" : building.DisplayedHeightKind;
                var color = BuildingColor(building.CategoryCode, colorHeightKind);
                if (focusedGeometry != null && string.Equals(building.BuildingStableId,
                        focusedGeometry.BuildingStableId, StringComparison.Ordinal))
                    BuildFocusedBuilding(batch, focusedGeometry, height, color, report);
                else
                {
                    AddWalls(batch, polygon, BuildingBaseY, BuildingBaseY + height, color);
                    AddFlatPolygon(batch, polygon, BuildingBaseY + height,
                        color * new Color(.91f, .91f, .91f, 1f));
                }
                report.BuildingCount++;
            }
        }

        private static void BuildFocusedBuilding(MeshBatch batch,
            행정동건물내부Ring검토Geometry geometry, float height, Color color,
            BuildReport report)
        {
            var outer = geometry.ExteriorRing.Select(point =>
                new Vector2((float)point.X, (float)point.Z)).ToArray();
            AddWalls(batch, outer, BuildingBaseY, BuildingBaseY + height, color);
            var outerSign = RingSignedArea(outer);
            foreach (var ring in geometry.InteriorRings)
            {
                var inner = ring.Select(point =>
                    new Vector2((float)point.X, (float)point.Z)).ToArray();
                // 원본 좌표는 유지하고 벽 winding만 외곽과 반대로 맞춘다.
                if (RingSignedArea(inner) * outerSign > 0f) Array.Reverse(inner);
                AddWalls(batch, inner, BuildingBaseY, BuildingBaseY + height, color);
                report.FocusedInteriorWallSegmentCount += inner.Length;
            }
            var roofColor = color * new Color(.91f, .91f, .91f, 1f);
            var first = batch.Vertices.Count;
            foreach (var point in geometry.RoofVertices)
                batch.Vertex(new Vector3((float)point.X, BuildingBaseY + height,
                    (float)point.Z), roofColor);
            foreach (var index in geometry.RoofTriangleIndices)
                batch.Triangles.Add(first + index);
            report.FocusedBuildingCount++;
            report.FocusedBuildingStableId = geometry.BuildingStableId;
            report.FocusedBuildingGeometryHashSha256 = geometry.ContentHashSha256;
            report.FocusedInteriorRingCount = geometry.InteriorRings.Count;
            report.FocusedRoofTriangleCount = geometry.RoofTriangleIndices.Count / 3;
        }

        private static float RingSignedArea(IReadOnlyList<Vector2> ring)
        {
            var area = 0f;
            for (var index = 0; index < ring.Count; index++)
            {
                var next = ring[(index + 1) % ring.Count];
                area += ring[index].x * next.y - next.x * ring[index].y;
            }
            return area * .5f;
        }

        private static void BuildPrivateObservationOverlay(
            행정동디오라마검토Snapshot snapshot, MeshBatch[,] chunks,
            BuildReport report)
        {
            var overlay = snapshot.PrivateObservationOverlay;
            if (overlay == null) return;

            foreach (var point in overlay.CrosswalkPoints)
            {
                var center = new Vector3((float)point.X, PrivateObservationOverlayY,
                    (float)point.Z);
                var batch = ChunkAt(snapshot.Bounds, chunks,
                    new Vector2(center.x, center.z));
                var firstVertex = batch.Vertices.Count;
                var color = point.SourceDistrictSpatialAssignmentConflict
                    ? new Color(.92f, .30f, .28f)
                    : point.PedestrianSignalPresent
                        ? new Color(.22f, .88f, .68f)
                        : new Color(.92f, .72f, .24f);
                var left = Vector3.left * CrosswalkGlyphHalfLength;
                var offset = Vector3.forward * CrosswalkGlyphSpacing;
                batch.Strip(center + left - offset, center - left - offset,
                    .55f, color);
                batch.Strip(center + left + offset, center - left + offset,
                    .55f, color);
                report.PrivateObservationOverlayVertexCount +=
                    batch.Vertices.Count - firstVertex;
                report.CrosswalkPointCount++;
            }

            foreach (var point in overlay.IntersectionPoints)
            {
                var center = new Vector3((float)point.X, PrivateObservationOverlayY,
                    (float)point.Z);
                var batch = ChunkAt(snapshot.Bounds, chunks,
                    new Vector2(center.x, center.z));
                var firstVertex = batch.Vertices.Count;
                var color = point.SourceDistrictSpatialAssignmentConflict
                    ? new Color(.92f, .30f, .28f)
                    : point.LinkedCrosswalkCount > 0
                        ? new Color(.63f, .42f, .94f)
                        : new Color(.58f, .54f, .68f);
                var north = center + Vector3.forward * IntersectionGlyphRadius;
                var east = center + Vector3.right * IntersectionGlyphRadius;
                var south = center + Vector3.back * IntersectionGlyphRadius;
                var west = center + Vector3.left * IntersectionGlyphRadius;
                batch.Strip(north, east, .5f, color);
                batch.Strip(east, south, .5f, color);
                batch.Strip(south, west, .5f, color);
                batch.Strip(west, north, .5f, color);
                report.PrivateObservationOverlayVertexCount +=
                    batch.Vertices.Count - firstVertex;
                report.IntersectionPointCount++;
            }
        }

        private static void BuildPrivateWalkNetworkOverlay(
            행정동디오라마검토Snapshot snapshot, MeshBatch[,] chunks,
            BuildReport report)
        {
            var overlay = snapshot.PrivateWalkNetworkOverlay;
            if (overlay == null) return;

            foreach (var fragment in overlay.LinkFragments)
            {
                report.WalkNetworkLinkPathVertexCount += fragment.Path.Count;
                for (var index = 0; index + 1 < fragment.Path.Count; index++)
                {
                    var start = Point(fragment.Path[index], PrivateWalkNetworkOverlayY);
                    var end = Point(fragment.Path[index + 1], PrivateWalkNetworkOverlayY);
                    var batch = ChunkAt(snapshot.Bounds, chunks,
                        new Vector2((start.x + end.x) * .5f,
                            (start.z + end.z) * .5f));
                    var firstVertex = batch.Vertices.Count;
                    batch.Strip(start, end, WalkLinkGlyphWidth,
                        WalkActorColor(fragment.ActorSummary));
                    report.PrivateWalkNetworkOverlayVertexCount +=
                        batch.Vertices.Count - firstVertex;
                }
                report.WalkNetworkLinkFragmentCount++;
            }

            foreach (var point in overlay.NodePoints)
            {
                var center = new Vector3((float)point.X,
                    PrivateWalkNetworkOverlayY + .01f, (float)point.Z);
                var batch = ChunkAt(snapshot.Bounds, chunks,
                    new Vector2(center.x, center.z));
                var radius = WalkNodeGlyphRadius;
                batch.Quad(center + Vector3.forward * radius,
                    center + Vector3.right * radius,
                    center + Vector3.back * radius,
                    center + Vector3.left * radius,
                    new Color(.16f, .78f, 1f));
                report.PrivateWalkNetworkOverlayVertexCount += 4;
                report.WalkNetworkNodePointCount++;
            }
        }

        private static void BuildPrivateRoadDirectionOverlay(
            행정동디오라마검토Snapshot snapshot, MeshBatch[,] chunks,
            BuildReport report)
        {
            var overlay = snapshot.PrivateRoadDirectionOverlay;
            if (overlay == null) return;

            foreach (var point in overlay.CandidatePoints)
            {
                var center = new Vector3((float)point.X,
                    PrivateRoadDirectionOverlayY, (float)point.Z);
                var batch = ChunkAt(snapshot.Bounds,
                    chunks, new Vector2(center.x, center.z));
                // DRN 좌표계/각도와 LENX 단위가 미확인인 동안은 회전·길이 없는 위치 표식만 둔다.
                batch.Quad(center + Vector3.forward * RoadDirectionSymbolRadius,
                    center + Vector3.right * RoadDirectionSymbolRadius,
                    center + Vector3.back * RoadDirectionSymbolRadius,
                    center + Vector3.left * RoadDirectionSymbolRadius,
                    new Color(1f, .62f, .12f));
                report.PrivateRoadDirectionOverlayVertexCount += 4;
                report.RoadDirectionPointCandidateCount++;
                report.RoadDirectionUnresolvedCount++;
            }
        }

        private static void BuildPrivateBiotopeOverlay(행정동디오라마검토Snapshot snapshot,
            MeshBatch[,] chunks, BuildReport report)
        {
            var overlay = snapshot.PrivateBiotopeOverlay;
            if (overlay == null) return;
            foreach (var candidate in overlay.Candidates)
            {
                report.BiotopeCandidateCount++;
                var color = BiotopeColor(candidate.TypeCode[0]);
                foreach (var polygon in candidate.Polygons)
                {
                    report.BiotopePolygonPartCount++;
                    report.BiotopeRingVertexCount += polygon.Exterior.Points.Count;
                    EmitBiotopeRing(snapshot, chunks, report, polygon.Exterior, color);
                    foreach (var hole in polygon.InteriorRings)
                    {
                        report.BiotopeInteriorRingCount++;
                        report.BiotopeRingVertexCount += hole.Points.Count;
                        EmitBiotopeRing(snapshot, chunks, report, hole, color);
                    }
                }
            }
        }

        private static void EmitBiotopeRing(행정동디오라마검토Snapshot snapshot,
            MeshBatch[,] chunks, BuildReport report, 행정동디오라마검토BiotopeRing ring, Color color)
        {
            for (var i = 0; i + 1 < ring.Points.Count; i++)
            {
                var a = ring.Points[i]; var b = ring.Points[i + 1];
                report.BiotopeSourceSegmentCount++;
                var dx = (b.X - a.X) * 1000d;
                var dz = (b.Z - a.Z) * 1000d;
                if (Math.Sqrt(dx * dx + dz * dz) <= 1d)
                {
                    report.BiotopeSkippedAtOrBelowOneMillimeterSegmentCount++;
                    continue;
                }
                var start = new Vector3((float)a.X, PrivateBiotopeOverlayY, (float)a.Z);
                var end = new Vector3((float)b.X, PrivateBiotopeOverlayY, (float)b.Z);
                var segment = new Vector2(end.x - start.x, end.z - start.z);
                var half = new Vector2(-segment.y, segment.x).normalized * .06f;
                var batch = ChunkAt(snapshot.Bounds, chunks,
                    new Vector2((start.x + end.x) * .5f, (start.z + end.z) * .5f));
                batch.Quad(new Vector3(start.x + half.x, start.y, start.z + half.y),
                    new Vector3(end.x + half.x, end.y, end.z + half.y),
                    new Vector3(end.x - half.x, end.y, end.z - half.y),
                    new Vector3(start.x - half.x, start.y, start.z - half.y), color);
                report.BiotopeRenderedSegmentCount++;
                report.PrivateBiotopeOverlayVertexCount += 4;
                report.PrivateBiotopeOverlayIndexCount += 6;
            }
        }

        private static Color BiotopeColor(char typeCode)
        {
            switch (typeCode)
            {
                case 'A': return new Color(.34f,.72f,.42f);
                case 'B': return new Color(.86f,.59f,.30f);
                case 'C': return new Color(.76f,.44f,.33f);
                case 'D': return new Color(.54f,.67f,.35f);
                case 'E': return new Color(.36f,.67f,.70f);
                case 'F': return new Color(.31f,.55f,.82f);
                case 'G': return new Color(.48f,.70f,.48f);
                case 'H': return new Color(.24f,.59f,.34f);
                case 'I': return new Color(.70f,.67f,.42f);
                default: throw new InvalidOperationException("AdministrativeDongPrivateBiotopeTypeCodeInvalid");
            }
        }

        private static void BuildPrivateLaneMarkingOverlay(
            행정동디오라마검토Snapshot snapshot, MeshBatch[,] chunks, BuildReport report)
        {
            var overlay = snapshot.PrivateLaneMarkingOverlay;
            if (overlay == null) return;
            foreach (var candidate in overlay.Candidates)
            {
                report.LaneMarkingCandidateCount++;
                foreach (var fragment in candidate.Fragments)
                {
                    report.LaneMarkingFragmentCount++;
                    report.LaneMarkingPathVertexCount += fragment.Path.Count;
                    for (var index = 0; index + 1 < fragment.Path.Count; index++)
                    {
                        report.LaneMarkingInputSegmentCount++;
                        var a = fragment.Path[index];
                        var b = fragment.Path[index + 1];
                        var dx = (b.X - a.X) * 1000d;
                        var dz = (b.Z - a.Z) * 1000d;
                        if (Math.Sqrt(dx * dx + dz * dz) <= 1d)
                        {
                            report.LaneMarkingSkippedAtOrBelowOneMillimeterSegmentCount++;
                            continue;
                        }
                        var start = new Vector3((float)a.X, PrivateLaneMarkingOverlayY,
                            (float)a.Z);
                        var end = new Vector3((float)b.X, PrivateLaneMarkingOverlayY,
                            (float)b.Z);
                        var batch = ChunkAt(snapshot.Bounds, chunks,
                            new Vector2((start.x + end.x) * .5f,
                                (start.z + end.z) * .5f));
                        var firstVertex = batch.Vertices.Count;
                        batch.Strip(start, end, LaneMarkingOutlineWidth,
                            new Color(.93f, .91f, .72f));
                        if (batch.Vertices.Count - firstVertex != 4)
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateLaneMarkingFloatPrecisionCollapse");
                        report.LaneMarkingRenderedSegmentCount++;
                        report.PrivateLaneMarkingOverlayVertexCount += 4;
                        report.PrivateLaneMarkingOverlayIndexCount += 6;
                    }
                }
            }
        }

        private static void BuildPrivateHorizontalContourTraceOverlay(
            행정동디오라마검토Snapshot snapshot, MeshBatch[,] chunks, BuildReport report)
        {
            var overlay = snapshot.PrivateHorizontalContourTraceOverlay;
            if (overlay == null) return;
            foreach (var candidate in overlay.Candidates)
            {
                report.HorizontalContourTraceCandidateCount++;
                foreach (var fragment in candidate.Fragments)
                {
                    report.HorizontalContourTraceFragmentCount++;
                    report.HorizontalContourTracePathVertexCount += fragment.Path.Count;
                    for (var index = 0; index + 1 < fragment.Path.Count; index++)
                    {
                        report.HorizontalContourTraceInputSegmentCount++;
                        var a = fragment.Path[index];
                        var b = fragment.Path[index + 1];
                        var dx = (b.X - a.X) * 1000d;
                        var dz = (b.Z - a.Z) * 1000d;
                        if (Math.Sqrt(dx * dx + dz * dz) <= 1d)
                        {
                            report.HorizontalContourTraceSkippedAtOrBelowOneMillimeterSegmentCount++;
                            continue;
                        }
                        var start = new Vector3((float)a.X,
                            PrivateHorizontalContourTraceOverlayY, (float)a.Z);
                        var end = new Vector3((float)b.X,
                            PrivateHorizontalContourTraceOverlayY, (float)b.Z);
                        var batch = ChunkAt(snapshot.Bounds, chunks,
                            new Vector2((start.x + end.x) * .5f,
                                (start.z + end.z) * .5f));
                        var firstVertex = batch.Vertices.Count;
                        batch.Strip(start, end, HorizontalContourTraceWidth,
                            new Color(.69f, .53f, .86f));
                        if (batch.Vertices.Count - firstVertex != 4)
                            throw new InvalidOperationException(
                                "AdministrativeDongPrivateHorizontalContourTraceFloatPrecisionCollapse");
                        report.HorizontalContourTraceRenderedSegmentCount++;
                        report.PrivateHorizontalContourTraceOverlayVertexCount += 4;
                        report.PrivateHorizontalContourTraceOverlayIndexCount += 6;
                    }
                }
            }
        }

        private static Color WalkActorColor(행정동디오라마검토WalkActorSummary flags)
        {
            if (flags.Vehicle) return new Color(.94f, .35f, .28f);
            if (flags.PersonalMobility) return new Color(.98f, .63f, .16f);
            if (flags.Bicycle) return new Color(.18f, .82f, .88f);
            if (flags.Pedestrian) return new Color(.27f, .92f, .48f);
            return flags.SourceReported
                ? new Color(.72f, .76f, .82f)
                : new Color(.48f, .55f, .64f);
        }

        private static void AddWalls(MeshBatch batch, IReadOnlyList<Vector2> polygon,
            float bottom, float top, Color color)
        {
            for (var index = 0; index < polygon.Count; index++)
            {
                var next = (index + 1) % polygon.Count;
                batch.Quad(
                    new Vector3(polygon[index].x, bottom, polygon[index].y),
                    new Vector3(polygon[index].x, top, polygon[index].y),
                    new Vector3(polygon[next].x, top, polygon[next].y),
                    new Vector3(polygon[next].x, bottom, polygon[next].y), color);
            }
        }

        private static void AddFlatPolygon(MeshBatch batch,
            IReadOnlyList<Vector2> polygon, float y, Color color)
        {
            var triangles = Triangulate(polygon);
            if (triangles.Count < 3) return;
            var first = batch.Vertices.Count;
            foreach (var point in polygon)
                batch.Vertex(new Vector3(point.x, y, point.y), color);
            foreach (var index in triangles) batch.Triangles.Add(first + index);
        }

        private static List<int> Triangulate(IReadOnlyList<Vector2> points)
        {
            float Cross(Vector2 a, Vector2 b, Vector2 c)
                => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            var remaining = Enumerable.Range(0, points.Count).ToList();
            var signedArea = 0f;
            for (var index = 0; index < points.Count; index++)
            {
                var a = points[index];
                var b = points[(index + 1) % points.Count];
                signedArea += a.x * b.y - b.x * a.y;
            }
            if (signedArea < 0f) remaining.Reverse();

            var result = new List<int>();
            var guard = points.Count * points.Count;
            while (remaining.Count > 2 && guard-- > 0)
            {
                var found = false;
                for (var index = 0; index < remaining.Count; index++)
                {
                    var a = remaining[(index + remaining.Count - 1) % remaining.Count];
                    var b = remaining[index];
                    var c = remaining[(index + 1) % remaining.Count];
                    if (Cross(points[a], points[b], points[c]) <= .0001f) continue;
                    var occupied = remaining.Any(candidate => candidate != a && candidate != b
                        && candidate != c && Cross(points[a], points[b], points[candidate]) >= 0f
                        && Cross(points[b], points[c], points[candidate]) >= 0f
                        && Cross(points[c], points[a], points[candidate]) >= 0f);
                    if (occupied) continue;
                    result.AddRange(new[] { a, c, b });
                    remaining.RemoveAt(index);
                    found = true;
                    break;
                }
                if (!found) return new List<int>();
            }
            return result;
        }

        private static MeshBatch ChunkAt(행정동디오라마검토Bounds bounds,
            MeshBatch[,] chunks, Vector2 point)
        {
            var x = Mathf.Clamp((int)Math.Floor(
                (point.x - bounds.MinX) / bounds.Width * ChunkAxisCount),
                0, ChunkAxisCount - 1);
            var z = Mathf.Clamp((int)Math.Floor(
                (point.y - bounds.MinZ) / bounds.Depth * ChunkAxisCount),
                0, ChunkAxisCount - 1);
            return chunks[x, z];
        }

        private static Vector3 Point(행정동디오라마검토Point value, float y)
            => new((float)value.X, y, (float)value.Z);

        private static float GroundPadding(행정동디오라마검토Bounds bounds)
            => Mathf.Clamp((float)Math.Max(bounds.Width, bounds.Depth) * .025f, 12f, 60f);

        private static Color BuildingColor(string category, string heightKind)
        {
            var hash = 17;
            foreach (var character in category ?? string.Empty)
                hash = unchecked(hash * 31 + character);
            var palette = new[]
            {
                new Color(.76f, .70f, .62f), new Color(.70f, .68f, .63f),
                new Color(.67f, .70f, .70f), new Color(.75f, .66f, .58f)
            };
            var color = palette[(hash & int.MaxValue) % palette.Length];
            return string.Equals(heightKind, "SymbolicFallback4m", StringComparison.Ordinal)
                ? Color.Lerp(color, new Color(.58f, .58f, .56f), .35f)
                : color;
        }
    }
}
