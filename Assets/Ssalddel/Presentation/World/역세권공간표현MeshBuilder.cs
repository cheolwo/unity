using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 역별 자료 이름을 모른 채 검증된 1km snapshot을 중립 모형으로 묶는다.
    /// 도로 폭·결손 셀·4m 높이는 표현 기호이며 통행·공터·실측 권위가 아니다.
    /// </summary>
    public static class 역세권공간표현MeshBuilder
    {
        private const float GroundY = .02f;
        private const float SurfaceY = .045f;
        private const float RoadY = .08f;
        private const float BoundaryY = .105f;
        private const float BuildingBaseY = .13f;

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
                if (direction.sqrMagnitude <= .0001f) return;
                var side = Vector3.Cross(direction.normalized, Vector3.up) * (width * .5f);
                Quad(a - side, a + side, b + side, b - side, color);
            }

            internal void Disk(Vector3 center, float radius, Color color, int sides = 12)
            {
                var first = Vertex(center, color);
                for (var index = 0; index <= sides; index++)
                {
                    var angle = index * Mathf.PI * 2f / sides;
                    Vertex(center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius), color);
                }
                for (var index = 0; index < sides; index++)
                    Triangles.AddRange(new[] { first, first + index + 1, first + index + 2 });
            }
        }

        public sealed class BuildReport
        {
            public bool SnapshotApplied { get; internal set; }
            public string CoverageStatus { get; internal set; }
            public string SourceRevision { get; internal set; }
            public string SourceContentHash { get; internal set; }
            public int ChunkCount { get; internal set; }
            public int RendererBatchCount { get; internal set; }
            public int BuildingCount { get; internal set; }
            public int RoadCount { get; internal set; }
            public int SurfaceCount { get; internal set; }
            public int AdministrativeBoundaryCount { get; internal set; }
            public int MissingCoverageCellCount { get; internal set; }
            public int IncompleteSurfaceEvidenceCellCount { get; internal set; }
        }

        public static BuildReport Build(StationDioramaSpatialComposition composition,
            Action<MeshBatch> emit)
        {
            if (composition == null) throw new ArgumentNullException(nameof(composition));
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            var descriptor = composition.Descriptor;
            var profile = composition.PresentationProfile;
            var axis = profile.ChunkAxisCount;
            var chunks = new MeshBatch[axis, axis];
            for (var x = 0; x < axis; x++)
                for (var z = 0; z < axis; z++)
                    chunks[x, z] = new MeshBatch(
                        $"{descriptor.DisplayName} · 역세권 모형 · chunk:{x}:{z}", x, z);

            var report = new BuildReport
            {
                SnapshotApplied = composition.SnapshotApplied,
                CoverageStatus = composition.CoverageStatus,
                SourceRevision = composition.Revision,
                SourceContentHash = composition.ContentHash,
                ChunkCount = axis * axis
            };

            BuildNeutralGround(descriptor, chunks);
            if (composition.SnapshotApplied)
            {
                BuildCoverage(composition, chunks, report);
                BuildSurfaces(composition, chunks, report);
                BuildRoads(composition, chunks, report);
                BuildAdministrativeBoundaries(composition, chunks, report);
                BuildBuildings(composition, chunks, report);
            }
            BuildStationMarker(descriptor, chunks);

            for (var x = 0; x < axis; x++)
                for (var z = 0; z < axis; z++)
                {
                    var batch = chunks[x, z];
                    if (batch.Vertices.Count == 0) continue;
                    if (batch.Vertices.Count != batch.Colors.Count)
                        throw new InvalidOperationException("StationDioramaVertexColorStreamMismatch");
                    emit(batch);
                    report.RendererBatchCount++;
                }
            if (report.RendererBatchCount > profile.MaximumRendererCount)
                throw new InvalidOperationException("StationDioramaRendererBudgetExceeded");
            return report;
        }

        private static void BuildNeutralGround(StationDioramaDatasetDescriptor descriptor,
            MeshBatch[,] chunks)
        {
            var axis = chunks.GetLength(0);
            var diameter = (float)(descriptor.HalfExtentMeters * 2d);
            var size = diameter / axis;
            var minX = (float)(descriptor.OffsetX - descriptor.HalfExtentMeters);
            var minZ = (float)(descriptor.OffsetZ - descriptor.HalfExtentMeters);
            var colors = new[]
            {
                new Color(.48f, .47f, .43f), new Color(.51f, .50f, .46f),
                new Color(.46f, .45f, .42f), new Color(.50f, .49f, .45f)
            };
            for (var x = 0; x < axis; x++)
                for (var z = 0; z < axis; z++)
                {
                    var x0 = minX + x * size;
                    var z0 = minZ + z * size;
                    var color = colors[(x + z * axis) % colors.Length];
                    chunks[x, z].Quad(new Vector3(x0, GroundY, z0),
                        new Vector3(x0, GroundY, z0 + size),
                        new Vector3(x0 + size, GroundY, z0 + size),
                        new Vector3(x0 + size, GroundY, z0), color);
                }
        }

        private static void BuildCoverage(StationDioramaSpatialComposition composition,
            MeshBatch[,] chunks, BuildReport report)
        {
            foreach (var cell in composition.CoverageCells)
            {
                Color color;
                switch (cell.Classification)
                {
                    case "MissingCoverage":
                        color = new Color(.40f, .41f, .42f);
                        report.MissingCoverageCellCount++;
                        break;
                    case "IncompleteSurfaceEvidence":
                        color = new Color(.45f, .44f, .42f);
                        report.IncompleteSurfaceEvidenceCellCount++;
                        break;
                    default:
                        continue;
                }
                const float inset = 1.2f;
                var minX = (float)cell.Bounds.MinX + inset;
                var minZ = (float)cell.Bounds.MinZ + inset;
                var maxX = (float)cell.Bounds.MaxX - inset;
                var maxZ = (float)cell.Bounds.MaxZ - inset;
                if (maxX <= minX || maxZ <= minZ) continue;
                var center = new Vector2((minX + maxX) * .5f, (minZ + maxZ) * .5f);
                ChunkAt(composition.Descriptor, chunks, center).Quad(
                    new Vector3(minX, GroundY + .008f, minZ),
                    new Vector3(minX, GroundY + .008f, maxZ),
                    new Vector3(maxX, GroundY + .008f, maxZ),
                    new Vector3(maxX, GroundY + .008f, minZ), color);
            }
        }

        private static void BuildSurfaces(StationDioramaSpatialComposition composition,
            MeshBatch[,] chunks, BuildReport report)
        {
            foreach (var surface in composition.Surfaces)
                foreach (var part in surface.Parts)
                {
                    var polygon = Polygon(part.Outer);
                    if (polygon.Count < 3) continue;
                    AddFlatPolygon(ChunkAt(composition.Descriptor, chunks, Center(polygon)),
                        polygon, SurfaceY, SurfaceColor(surface.Kind));
                    foreach (var sourceHole in part.Holes)
                    {
                        var hole = Polygon(sourceHole);
                        if (hole.Count < 3) continue;
                        AddFlatPolygon(ChunkAt(composition.Descriptor, chunks, Center(hole)),
                            hole, SurfaceY + .002f, new Color(.48f, .47f, .43f));
                    }
                    report.SurfaceCount++;
                }
        }

        private static void BuildRoads(StationDioramaSpatialComposition composition,
            MeshBatch[,] chunks, BuildReport report)
        {
            var width = composition.PresentationProfile.SymbolicRoadWidthMeters;
            foreach (var road in composition.Roads)
            {
                var emitted = false;
                foreach (var part in road.Parts)
                    for (var index = 1; index < part.Count; index++)
                    {
                        var a = Point(part[index - 1], RoadY);
                        var b = Point(part[index], RoadY);
                        var center = new Vector2((a.x + b.x) * .5f, (a.z + b.z) * .5f);
                        ChunkAt(composition.Descriptor, chunks, center).Strip(a, b, width,
                            new Color(.24f, .27f, .29f));
                        emitted = true;
                    }
                if (emitted) report.RoadCount++;
            }
        }

        private static void BuildAdministrativeBoundaries(
            StationDioramaSpatialComposition composition, MeshBatch[,] chunks,
            BuildReport report)
        {
            foreach (var area in composition.AdministrativeAreas)
                foreach (var part in area.Parts)
                {
                    var points = part.Outer;
                    for (var index = 1; index < points.Count; index++)
                    {
                        var a = Point(points[index - 1], BoundaryY);
                        var b = Point(points[index], BoundaryY);
                        var center = new Vector2((a.x + b.x) * .5f, (a.z + b.z) * .5f);
                        ChunkAt(composition.Descriptor, chunks, center).Strip(a, b, .65f,
                            new Color(.72f, .63f, .35f));
                    }
                    report.AdministrativeBoundaryCount++;
                }
        }

        private static void BuildBuildings(StationDioramaSpatialComposition composition,
            MeshBatch[,] chunks, BuildReport report)
        {
            foreach (var building in composition.Buildings)
            {
                var emitted = false;
                foreach (var part in building.Parts)
                {
                    var outer = Polygon(part.Outer);
                    if (outer.Count < 3) continue;
                    var batch = ChunkAt(composition.Descriptor, chunks, Center(outer));
                    var height = Mathf.Clamp((float)building.HeightMeters, 2f, 300f);
                    var color = BuildingColor(building.BuildingKind, building.HeightKind);
                    AddWalls(batch, outer, BuildingBaseY, BuildingBaseY + height, color);
                    foreach (var hole in part.Holes)
                    {
                        var inner = Polygon(hole);
                        if (inner.Count >= 3)
                            AddWalls(batch, inner, BuildingBaseY, BuildingBaseY + height,
                                color * new Color(.86f, .86f, .86f, 1f));
                    }
                    // courtyard/hole을 지붕으로 채우지 않는다. 구멍 없는 외곽만 닫는다.
                    if (part.Holes.Count == 0)
                        AddFlatPolygon(batch, outer, BuildingBaseY + height,
                            color * new Color(.88f, .88f, .88f, 1f));
                    emitted = true;
                }
                if (emitted) report.BuildingCount++;
            }
        }

        private static void BuildStationMarker(StationDioramaDatasetDescriptor descriptor,
            MeshBatch[,] chunks)
        {
            var center = new Vector2((float)descriptor.OffsetX, (float)descriptor.OffsetZ);
            ChunkAt(descriptor, chunks, center).Disk(
                new Vector3(center.x, .18f, center.y), 5f, new Color(.78f, .72f, .25f));
        }

        private static void AddWalls(MeshBatch batch, IReadOnlyList<Vector2> polygon,
            float bottom, float top, Color color)
        {
            for (var index = 0; index < polygon.Count; index++)
            {
                var next = (index + 1) % polygon.Count;
                batch.Quad(new Vector3(polygon[index].x, bottom, polygon[index].y),
                    new Vector3(polygon[index].x, top, polygon[index].y),
                    new Vector3(polygon[next].x, top, polygon[next].y),
                    new Vector3(polygon[next].x, bottom, polygon[next].y), color);
            }
        }

        private static void AddFlatPolygon(MeshBatch batch, IReadOnlyList<Vector2> polygon,
            float y, Color color)
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

        private static List<Vector2> Polygon(IReadOnlyList<StationSpatialPoint> points)
        {
            var result = new List<Vector2>();
            if (points == null) return result;
            foreach (var point in points) result.Add(new Vector2((float)point.X, (float)point.Z));
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude < .000001f)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        private static Vector2 Center(IReadOnlyList<Vector2> points)
        {
            var sum = Vector2.zero;
            foreach (var point in points) sum += point;
            return sum / Mathf.Max(1, points.Count);
        }

        private static Vector3 Point(StationSpatialPoint point, float y)
            => new((float)point.X, y, (float)point.Z);

        private static MeshBatch ChunkAt(StationDioramaDatasetDescriptor descriptor,
            MeshBatch[,] chunks, Vector2 point)
        {
            var axis = chunks.GetLength(0);
            var minX = descriptor.OffsetX - descriptor.HalfExtentMeters;
            var minZ = descriptor.OffsetZ - descriptor.HalfExtentMeters;
            var diameter = descriptor.HalfExtentMeters * 2d;
            var x = Mathf.Clamp((int)Math.Floor((point.x - minX) / diameter * axis), 0, axis - 1);
            var z = Mathf.Clamp((int)Math.Floor((point.y - minZ) / diameter * axis), 0, axis - 1);
            return chunks[x, z];
        }

        private static Color SurfaceColor(string kind) => kind switch
        {
            "park" or "garden" or "grass" or "wood" => new Color(.34f, .47f, .31f),
            "water" => new Color(.29f, .47f, .58f),
            "school" or "playground" or "pitch" => new Color(.50f, .52f, .35f),
            "commercial" => new Color(.58f, .47f, .39f),
            "parking" or "pedestrian" => new Color(.43f, .43f, .41f),
            _ => new Color(.48f, .45f, .40f)
        };

        private static Color BuildingColor(string kind, string heightKind)
        {
            var hash = 17;
            foreach (var character in kind ?? string.Empty) hash = unchecked(hash * 31 + character);
            var palette = new[]
            {
                new Color(.75f, .70f, .62f), new Color(.70f, .68f, .63f),
                new Color(.67f, .70f, .71f), new Color(.76f, .66f, .57f)
            };
            var color = palette[(hash & int.MaxValue) % palette.Length];
            return heightKind == "SymbolicFallback4m"
                ? Color.Lerp(color, new Color(.58f, .58f, .56f), .35f)
                : color;
        }
    }
}
