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
            public int TotalVertexCount { get; internal set; }
            public int MaximumBatchVertexCount { get; internal set; }
            public Bounds ModelBounds { get; internal set; }
        }

        public static BuildReport Build(행정동디오라마검토Snapshot snapshot,
            Action<MeshBatch> emit)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (!snapshot.ObservationPresentationOnly || snapshot.DistributionApproved
                || snapshot.TraversalReady || snapshot.GameplayReady)
                throw new InvalidOperationException("AdministrativeDongReviewSnapshotAuthorityInvalid");

            var chunks = new MeshBatch[ChunkAxisCount, ChunkAxisCount];
            for (var x = 0; x < ChunkAxisCount; x++)
            for (var z = 0; z < ChunkAxisCount; z++)
                chunks[x, z] = new MeshBatch(
                    $"{snapshot.DisplayName} · 행정동 도형 검토 · chunk:{x}:{z}", x, z);

            var report = new BuildReport
            {
                AdministrativeAreaStableId = snapshot.AdministrativeAreaStableId,
                ProjectionHashSha256 = snapshot.ProjectionHashSha256
            };
            BuildNeutralGround(snapshot, chunks);
            BuildRoads(snapshot, chunks, report);
            BuildBoundary(snapshot, chunks, report);
            BuildBuildings(snapshot, chunks, report);

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
            MeshBatch[,] chunks, BuildReport report)
        {
            foreach (var building in snapshot.Buildings)
            {
                var polygon = building.Footprint.Select(value =>
                    new Vector2((float)value.X, (float)value.Z)).ToArray();
                if (polygon.Length < 3) continue;
                var batch = ChunkAt(snapshot.Bounds, chunks,
                    new Vector2((float)building.CentroidX, (float)building.CentroidZ));
                var height = Mathf.Clamp((float)building.DisplayedHeight, 2f, 300f);
                var color = BuildingColor(building.CategoryCode,
                    building.DisplayedHeightKind);
                AddWalls(batch, polygon, BuildingBaseY, BuildingBaseY + height, color);
                AddFlatPolygon(batch, polygon, BuildingBaseY + height,
                    color * new Color(.91f, .91f, .91f, 1f));
                report.BuildingCount++;
            }
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
