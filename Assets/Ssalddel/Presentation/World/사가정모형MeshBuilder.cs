using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 동결된 SagajeongReference 윤곽을 4x4 표시 chunk로 합치는 읽기 전용 모형 표현이다.
    /// 창문, 녹음, 차양과 옥상 소품은 시각 문법일 뿐 실제 시설 정보가 아니다.
    /// </summary>
    public static class 사가정모형MeshBuilder
    {
        public const string Revision = "sagajeong-spatial-density-presentation.r18";
        public const int ChunkAxisCount = 4;
        public const int MaximumRendererCount = 128;
        public const int MaximumMaterialCount = 12;
        public const float WallSurfaceCode = 1f;
        public const float UmbraAlpha = .10f;
        public const float PenumbraAlpha = .05f;
        public const float MaximumShadowLength = 8f;
        public const float StationDetailRadius = 135f;

        private const float GroundY = .02f;
        public const float BuildingBaseElevation = .16f;
        private const float RoadShoulderY = .07f;
        private const float RoadY = .10f;
        private const float RoadMarkingY = .125f;
        private const float ShadowY = .045f;

        public sealed class MeshBatch
        {
            internal MeshBatch(string name, int chunkX, int chunkZ, bool shadow, bool stationDetail)
            {
                Name = name;
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                IsShadow = shadow;
                IsStationDetail = stationDetail;
            }

            public string Name { get; }
            public int ChunkX { get; }
            public int ChunkZ { get; }
            public bool IsShadow { get; }
            public bool IsStationDetail { get; }
            public List<Vector3> Vertices { get; } = new();
            public List<int> Triangles { get; } = new();
            public List<Color> Colors { get; } = new();
            // x: surface role (1 = wall/procedural windows), y: massing variant.
            public List<Vector2> SurfaceData { get; } = new();

            private int Vertex(Vector3 position, Color color, float surface, float variant)
            {
                var index = Vertices.Count;
                Vertices.Add(position);
                Colors.Add(color);
                SurfaceData.Add(new Vector2(surface, variant));
                return index;
            }

            internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                Color color, float surface = 0f, float variant = 0f)
            {
                var first = Vertex(a, color, surface, variant);
                Vertex(b, color, surface, variant);
                Vertex(c, color, surface, variant);
                Vertex(d, color, surface, variant);
                // 양면 표시는 shader Cull Off가 담당한다. 역방향 중복 면은 만들지 않는다.
                Triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            internal void Strip(Vector3 a, Vector3 b, float width, Color color,
                float surface = 0f, float variant = 0f)
            {
                var direction = b - a;
                if (direction.sqrMagnitude < .0001f) return;
                var side = Vector3.Cross(direction.normalized, Vector3.up) * (width * .5f);
                Quad(a - side, a + side, b + side, b - side, color, surface, variant);
            }

            internal void Disk(Vector3 center, float radius, Color color, int sides = 10)
            {
                if (radius <= 0f || sides < 3) return;
                var first = Vertex(center, color, 0f, 0f);
                for (var i = 0; i <= sides; i++)
                {
                    var angle = i * Mathf.PI * 2f / sides;
                    Vertex(center + new Vector3(Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius), color, 0f, 0f);
                }
                for (var i = 0; i < sides; i++)
                    Triangles.AddRange(new[] { first, first + i + 1, first + i + 2 });
            }

            internal void Box(Vector3 center, Vector3 size, Color color,
                float surface = 0f, float variant = 0f)
            {
                var a = center - size * .5f;
                var b = center + size * .5f;
                Quad(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z),
                    new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), color, surface, variant);
                Quad(new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z),
                    new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z), color, surface, variant);
                Quad(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z),
                    new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), color, surface, variant);
                Quad(new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z),
                    new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), color, surface, variant);
                Quad(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z),
                    new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), color, surface, variant);
            }
        }

        public sealed class BuildReport
        {
            public int ChunkCount { get; internal set; }
            public int RendererBatchCount { get; internal set; }
            public int MaterialSlotCount { get; internal set; }
            public int RoadJoinCount { get; internal set; }
            public int IntersectionPadCount { get; internal set; }
            public int ContinuousMarkingJoinCount { get; internal set; }
            public int StationDetailBatchCount { get; internal set; }
            public int StationDetailBuildingCount { get; internal set; }
            public int DecorativeTreeCount { get; internal set; }
            public int WindowGeometryVertexCount { get; internal set; }
            public int HeightMassingBandCount { get; internal set; }
            public int UsageMassingKindCount { get; internal set; }
            public float LongestAnalyticShadow { get; internal set; }
            public bool OverlayApplied { get; internal set; }
            public int OverlayBuildingCount { get; internal set; }
            public int OverlaySurfaceCount { get; internal set; }
            public string SourceRevision { get; internal set; }
            public string SourceContentHash { get; internal set; }
        }

        private sealed class Chunk
        {
            public readonly MeshBatch Opaque;
            public readonly MeshBatch Shadow;
            public readonly MeshBatch StationDetail;

            public Chunk(int x, int z)
            {
                Opaque = new MeshBatch($"사가정 모형 · chunk:{x}:{z} · 불투명", x, z, false, false);
                Shadow = new MeshBatch($"사가정 모형 · chunk:{x}:{z} · 접지 그림자", x, z, true, false);
                StationDetail = new MeshBatch($"사가정 모형 · chunk:{x}:{z} · 역세권 상세", x, z, false, true);
            }
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            private readonly int x;
            private readonly int z;
            public NodeKey(Vector2 point)
            {
                // OSM 연속 구간의 float 직렬화 오차만 흡수하고 서로 다른 교차점을 합치지 않는다.
                x = Mathf.RoundToInt(point.x * 20f);
                z = Mathf.RoundToInt(point.y * 20f);
            }
            public bool Equals(NodeKey other) => x == other.x && z == other.z;
            public override bool Equals(object value) => value is NodeKey other && Equals(other);
            public override int GetHashCode() => unchecked(x * 397 ^ z);
        }

        private sealed class RoadNode
        {
            public Vector2 Position;
            public int Degree;
            public float MaximumWidth;
            public bool HasMajorRoad;
        }

        /// <summary>기존 호출자의 callback 계약을 보존하는 호환 진입점.</summary>
        public static void Build(공간참고자료 data, 건물높이보완조회 heights,
            Action<string, List<Vector3>, List<int>, Color> emit, bool greenery = true)
        {
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            BuildBatches(data, heights,
                batch => emit(batch.Name, batch.Vertices, batch.Triangles, Color.white), greenery);
        }

        public static BuildReport BuildBatches(공간참고자료 data, 건물높이보완조회 heights,
            Action<MeshBatch> emit, bool greenery = true)
            => BuildBatches(data, heights, null, emit, greenery);

        public static BuildReport BuildBatches(공간참고자료 data, 건물높이보완조회 heights,
            사가정공간표현Composition composition, Action<MeshBatch> emit, bool greenery = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (emit == null) throw new ArgumentNullException(nameof(emit));
            if (data.halfExtent <= 0f || !float.IsFinite(data.halfExtent)
                || !float.IsFinite(data.offsetX) || !float.IsFinite(data.offsetZ))
                throw new ArgumentException("SagajeongReferenceBoundsInvalid", nameof(data));

            var chunks = new Chunk[ChunkAxisCount, ChunkAxisCount];
            for (var x = 0; x < ChunkAxisCount; x++)
                for (var z = 0; z < ChunkAxisCount; z++)
                    chunks[x, z] = new Chunk(x, z);

            var report = new BuildReport
            {
                ChunkCount = ChunkAxisCount * ChunkAxisCount,
                MaterialSlotCount = 2,
                WindowGeometryVertexCount = 0,
                OverlayApplied = composition?.OverlayApplied == true,
                SourceRevision = composition?.Revision ?? data.revision,
                SourceContentHash = composition?.ContentHash
                    ?? 사가정공간표현OverlayComposer.BaseMapHash,
            };
            var heightBands = new HashSet<int>();
            var usageKinds = new HashSet<int>();

            BuildGround(data, chunks);
            if (composition?.OverlayApplied == true)
                BuildOverlaySurfaces(data, composition, chunks, report);
            BuildRoads(data, chunks, report);
            BuildBuildings(data, heights, composition, chunks, report, heightBands, usageKinds);
            if (greenery) BuildGreenery(data, composition, chunks, report);

            report.HeightMassingBandCount = heightBands.Count;
            report.UsageMassingKindCount = usageKinds.Count;
            for (var x = 0; x < ChunkAxisCount; x++)
                for (var z = 0; z < ChunkAxisCount; z++)
                {
                    foreach (var batch in new[] { chunks[x, z].Opaque, chunks[x, z].Shadow, chunks[x, z].StationDetail })
                    {
                        if (batch.Vertices.Count == 0) continue;
                        if (batch.Vertices.Count != batch.Colors.Count
                            || batch.Vertices.Count != batch.SurfaceData.Count)
                            throw new InvalidOperationException("SagajeongMiniatureVertexStreamMismatch");
                        emit(batch);
                        report.RendererBatchCount++;
                        if (batch.IsStationDetail) report.StationDetailBatchCount++;
                    }
                }

            if (report.RendererBatchCount > MaximumRendererCount)
                throw new InvalidOperationException("SagajeongMiniatureRendererBudgetExceeded");
            if (report.MaterialSlotCount > MaximumMaterialCount)
                throw new InvalidOperationException("SagajeongMiniatureMaterialBudgetExceeded");
            return report;
        }

        private static void BuildGround(공간참고자료 data, Chunk[,] chunks)
        {
            var diameter = data.halfExtent * 2f;
            var size = diameter / ChunkAxisCount;
            var minX = data.offsetX - data.halfExtent;
            var minZ = data.offsetZ - data.halfExtent;
            // 아무 표면 근거도 없는 셀은 녹지나 공터로 읽히지 않도록 중립 모형판으로 둔다.
            // 공원·수목·잔디 색은 검증된 surface가 덮는 부분에만 적용한다.
            var unknownGround = new[]
            {
                new Color(.49f, .48f, .41f), new Color(.52f, .50f, .43f),
                new Color(.47f, .46f, .40f), new Color(.54f, .52f, .45f),
            };
            for (var x = 0; x < ChunkAxisCount; x++)
                for (var z = 0; z < ChunkAxisCount; z++)
                {
                    var left = minX + x * size;
                    var right = left + size;
                    var bottom = minZ + z * size;
                    var top = bottom + size;
                    chunks[x, z].Opaque.Quad(
                        new Vector3(left, GroundY, bottom), new Vector3(left, GroundY, top),
                        new Vector3(right, GroundY, top), new Vector3(right, GroundY, bottom),
                        unknownGround[(x + z * 3) % unknownGround.Length]);
                }
        }

        private static void BuildOverlaySurfaces(공간참고자료 data,
            사가정공간표현Composition composition, Chunk[,] chunks, BuildReport report)
        {
            var fallbackGround = new Color(.49f, .48f, .41f);
            foreach (var surface in composition.Surfaces.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                var color = SurfaceColor(surface.Kind);
                foreach (var part in surface.Parts)
                {
                    var polygon = Polygon(part.Outer);
                    if (polygon.Count < 3) continue;
                    var center = polygon.Aggregate(Vector2.zero, (sum, value) => sum + value) / polygon.Count;
                    AddFlatPolygon(ChunkAt(data, chunks, center).Opaque, polygon, GroundY + .012f,
                        color, 6f);
                    // hole을 실제 면으로 채우지 않고 바탕색으로 되돌린다. 권위 지형이나 Collider는 만들지 않는다.
                    foreach (var sourceHole in part.Holes)
                    {
                        var hole = Polygon(sourceHole);
                        if (hole.Count < 3) continue;
                        var holeCenter = hole.Aggregate(Vector2.zero, (sum, value) => sum + value) / hole.Count;
                        AddFlatPolygon(ChunkAt(data, chunks, holeCenter).Opaque, hole, GroundY + .016f,
                            fallbackGround, 0f);
                    }
                    report.OverlaySurfaceCount++;
                }
            }
        }

        private static void AddFlatPolygon(MeshBatch batch, IReadOnlyList<Vector2> polygon,
            float y, Color color, float surface)
        {
            var triangles = 사가정지리MeshBuilder.참고지붕삼각형(polygon.ToList());
            if (triangles.Count < 3) return;
            var first = batch.Vertices.Count;
            foreach (var point in polygon)
            {
                batch.Vertices.Add(new Vector3(point.x, y, point.y));
                batch.Colors.Add(color);
                batch.SurfaceData.Add(new Vector2(surface, 0f));
            }
            foreach (var index in triangles) batch.Triangles.Add(first + index);
        }

        private static Color SurfaceColor(string kind)
        {
            var value = (kind ?? string.Empty).ToLowerInvariant();
            if (value.Contains("water") || value.Contains("하천") || value.Contains("수면"))
                return new Color(.27f, .55f, .68f);
            if (value.Contains("park") || value.Contains("garden") || value.Contains("green")
                || value.Contains("forest") || value.Contains("wood") || value.Contains("grass")
                || value.Contains("공원") || value.Contains("정원") || value.Contains("녹지"))
                return new Color(.35f, .57f, .22f);
            if (value.Contains("pitch")) return new Color(.45f, .61f, .28f);
            if (value.Contains("school") || value.Contains("yard") || value.Contains("playground")
                || value.Contains("religious") || value.Contains("학교") || value.Contains("운동장"))
                return new Color(.66f, .58f, .36f);
            if (value.Contains("parking") || value.Contains("pedestrian") || value.Contains("주차"))
                return new Color(.46f, .48f, .43f);
            if (value.Contains("residential") || value.Contains("industrial"))
                return new Color(.58f, .57f, .42f);
            if (value.Contains("field") || value.Contains("farm") || value.Contains("전")
                || value.Contains("답"))
                return new Color(.57f, .61f, .29f);
            return new Color(.49f, .48f, .41f);
        }

        private static void BuildRoads(공간참고자료 data, Chunk[,] chunks, BuildReport report)
        {
            var pavement = new Color(.66f, .63f, .53f);
            var asphalt = new Color(.18f, .23f, .27f);
            var localRoad = new Color(.32f, .36f, .34f);
            var marking = new Color(.96f, .66f, .16f);
            var nodes = new Dictionary<NodeKey, RoadNode>();
            var roads = (data.roads ?? Array.Empty<참고도로>())
                .Where(IsValidRoad).OrderBy(value => value.id, StringComparer.Ordinal).ToArray();

            foreach (var road in roads)
            {
                var a2 = new Vector2(road.x1, road.z1);
                var b2 = new Vector2(road.x2, road.z2);
                RegisterRoadNode(nodes, a2, road);
                RegisterRoadNode(nodes, b2, road);
                var center = (a2 + b2) * .5f;
                var batch = ChunkAt(data, chunks, center).Opaque;
                var a = new Vector3(a2.x, RoadShoulderY, a2.y);
                var b = new Vector3(b2.x, RoadShoulderY, b2.y);
                var width = RoadWidth(road.kind);
                batch.Strip(a, b, width + 2.1f, pavement);
                a.y = b.y = RoadY;
                batch.Strip(a, b, width, Major(road.kind) ? asphalt : localRoad);
                if (!Major(road.kind)) continue;
                a.y = b.y = RoadMarkingY;
                // 구간 전체를 쓰고 아래의 node cap으로 접속부를 메워, segment마다 위상이 다시 시작되지 않는다.
                batch.Strip(a, b, .22f, marking);
            }

            foreach (var node in nodes.Values.OrderBy(value => value.Position.x).ThenBy(value => value.Position.y))
            {
                if (node.Degree < 2) continue;
                var batch = ChunkAt(data, chunks, node.Position).Opaque;
                batch.Disk(new Vector3(node.Position.x, RoadShoulderY, node.Position.y),
                    node.MaximumWidth * .5f + 1.05f, pavement, 12);
                batch.Disk(new Vector3(node.Position.x, RoadY, node.Position.y),
                    node.MaximumWidth * .5f, node.HasMajorRoad ? asphalt : localRoad, 12);
                report.RoadJoinCount++;
                if (node.HasMajorRoad)
                {
                    batch.Disk(new Vector3(node.Position.x, RoadMarkingY, node.Position.y), .16f, marking, 8);
                    report.ContinuousMarkingJoinCount++;
                }
                if (node.Degree >= 3) report.IntersectionPadCount++;
            }
        }

        private static void RegisterRoadNode(Dictionary<NodeKey, RoadNode> nodes, Vector2 point, 참고도로 road)
        {
            var key = new NodeKey(point);
            if (!nodes.TryGetValue(key, out var node))
                nodes.Add(key, node = new RoadNode { Position = point });
            node.Degree++;
            node.MaximumWidth = Mathf.Max(node.MaximumWidth, RoadWidth(road.kind));
            node.HasMajorRoad |= Major(road.kind);
        }

        private static void BuildBuildings(공간참고자료 data, 건물높이보완조회 heights,
            사가정공간표현Composition composition, Chunk[,] chunks, BuildReport report,
            HashSet<int> heightBands, HashSet<int> usageKinds)
        {
            var renderedOverlayIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var baseline in (data.buildings ?? Array.Empty<참고건물>())
                         .Where(value => value != null).OrderBy(value => value.id, StringComparer.Ordinal))
            {
                if (composition?.OverlayApplied == true
                    && composition.TryResolveLegacyBuilding(baseline.id, out var overlay))
                {
                    if (renderedOverlayIds.Add(overlay.Id))
                        BuildOverlayBuilding(data, overlay, chunks, report, heightBands, usageKinds);
                    continue;
                }

                var polygon = Polygon(baseline.points);
                var height = Mathf.Max(2f, heights?.Resolve(baseline) ?? baseline.height);
                BuildBuilding(data, baseline.id, baseline.buildingKind, height,
                    new[] { polygon }, chunks, report, heightBands, usageKinds);
            }

            if (composition?.OverlayApplied != true) return;
            // 결속되지 않은 공식 footprint도 표현 자료 자체의 공간 근거로 표시한다.
            // 모호한 legacy match는 r3를 대체하지 않으므로 그 r3 외곽도 그대로 남는다.
            foreach (var overlay in composition.Buildings.OrderBy(value => value.Id, StringComparer.Ordinal))
                if (renderedOverlayIds.Add(overlay.Id))
                    BuildOverlayBuilding(data, overlay, chunks, report, heightBands, usageKinds);
        }

        private static void BuildOverlayBuilding(공간참고자료 data,
            사가정공간표현Building building, Chunk[,] chunks, BuildReport report,
            HashSet<int> heightBands, HashSet<int> usageKinds)
        {
            var polygons = building.Parts.Select(value => Polygon(value.Outer))
                .Where(value => value.Count >= 3).ToArray();
            if (polygons.Length == 0) return;
            var kind = string.Join(" ", new[] { building.BuildingKind, building.PurposeName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            BuildBuilding(data, building.Id, kind, (float)building.HeightMeters,
                polygons, chunks, report, heightBands, usageKinds);
            report.OverlayBuildingCount++;
        }

        private static void BuildBuilding(공간참고자료 data, string id, string kind, float height,
            IReadOnlyList<List<Vector2>> polygons, Chunk[,] chunks, BuildReport report,
            HashSet<int> heightBands, HashSet<int> usageKinds)
        {
            if (!float.IsFinite(height) || height <= 0f || polygons == null || polygons.Count == 0) return;
            height = Mathf.Max(2f, height);
            var heightBand = height >= 24f ? 2 : height >= 10f ? 1 : 0;
            var usage = Usage(kind);
            heightBands.Add(heightBand);
            usageKinds.Add(usage);
            var hash = Hash(id);
            var facade = FacadeColor(usage, hash);
            var roofColor = RoofColor(usage, hash);
            var top = BuildingBaseElevation + height;
            var stationDetailAdded = false;

            foreach (var polygon in polygons)
            {
                if (polygon == null || polygon.Count < 3) continue;
                var center = polygon.Aggregate(Vector2.zero, (sum, value) => sum + value) / polygon.Count;
                var chunk = ChunkAt(data, chunks, center);
                if (heightBand == 2)
                {
                    var transition = BuildingBaseElevation + height * .20f;
                    AddWalls(chunk.Opaque, polygon, BuildingBaseElevation, transition,
                        facade * new Color(.91f, .91f, .91f, 1f), usage);
                    var upper = ScalePolygon(polygon, center, .84f);
                    AddWalls(chunk.Opaque, upper, transition, top, facade, usage);
                    AddRoof(chunk.Opaque, upper, top, roofColor);
                }
                else
                {
                    AddWalls(chunk.Opaque, polygon, BuildingBaseElevation, top, facade, usage);
                    AddRoof(chunk.Opaque, polygon, top, roofColor);
                }

                var shadowLength = Mathf.Min(height * .18f, MaximumShadowLength);
                report.LongestAnalyticShadow = Mathf.Max(report.LongestAnalyticShadow, shadowLength);
                var direction = new Vector2(-.78f, .62f).normalized;
                var hull = Hull(polygon.Concat(polygon.Select(value => value + direction * shadowLength)));
                AddShadow(chunk.Shadow, hull, data, UmbraAlpha, 0f);
                AddShadow(chunk.Shadow, hull, data, PenumbraAlpha, .7f);

                if ((center - new Vector2(data.offsetX, data.offsetZ)).sqrMagnitude
                    > StationDetailRadius * StationDetailRadius) continue;
                AddStationDetail(chunk.StationDetail, polygon, center, top, height, usage, hash);
                stationDetailAdded = true;
            }

            if (stationDetailAdded) report.StationDetailBuildingCount++;
        }

        private static void AddWalls(MeshBatch batch, IReadOnlyList<Vector2> polygon,
            float bottom, float top, Color color, int usage)
        {
            var area = SignedArea(polygon);
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Count];
                var a = new Vector3(p.x, bottom, p.y);
                var b = new Vector3(q.x, bottom, q.y);
                var at = new Vector3(p.x, top, p.y);
                var bt = new Vector3(q.x, top, q.y);
                if (area > 0f) batch.Quad(a, at, bt, b, color, WallSurfaceCode, usage);
                else batch.Quad(b, bt, at, a, color, WallSurfaceCode, usage);
            }
        }

        private static void AddRoof(MeshBatch batch, IReadOnlyList<Vector2> polygon, float top, Color color)
        {
            var triangles = 사가정지리MeshBuilder.참고지붕삼각형(polygon.ToList());
            if (triangles.Count < 3) return;
            var first = batch.Vertices.Count;
            foreach (var point in polygon)
            {
                batch.Vertices.Add(new Vector3(point.x, top, point.y));
                batch.Colors.Add(color);
                batch.SurfaceData.Add(new Vector2(2f, 0f));
            }
            foreach (var index in triangles) batch.Triangles.Add(first + index);
        }

        private static void AddStationDetail(MeshBatch batch, IReadOnlyList<Vector2> polygon,
            Vector2 center, float top, float height, int usage, int hash)
        {
            var area = SignedArea(polygon);
            var trim = new Color(.94f, .89f, .76f);
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Count];
                var a = new Vector3(p.x, top - .18f, p.y);
                var b = new Vector3(q.x, top - .18f, q.y);
                var direction = (b - a).normalized;
                var outward = Vector3.Cross(direction, Vector3.up) * (area > 0f ? -1f : 1f);
                batch.Strip(a + outward * .16f, b + outward * .16f, .34f, trim, 2f, usage);
                if (height >= 15f || Vector3.Distance(a, b) < 5f || (hash + i) % 3 != 0) continue;
                var midpoint = (a + b) * .5f;
                midpoint.y = BuildingBaseElevation + Mathf.Min(2.7f, height - .5f);
                var length = Mathf.Min(3.2f, Vector3.Distance(a, b) * .42f);
                var accent = usage == 1 ? new Color(.20f, .48f, .42f)
                    : new Color(.82f, .38f, .22f);
                batch.Quad(midpoint - direction * length * .5f,
                    midpoint + direction * length * .5f,
                    midpoint + direction * length * .5f + outward * .72f - Vector3.up * .20f,
                    midpoint - direction * length * .5f + outward * .72f - Vector3.up * .20f,
                    accent, 3f, usage);
            }

            if (height <= 5f || EdgeDistance(center, polygon) <= 2.2f) return;
            var equipmentHeight = Mathf.Min(.8f, height * .08f);
            batch.Box(new Vector3(center.x, top - equipmentHeight * .55f, center.y),
                new Vector3(1.6f, equipmentHeight, 1.2f), trim, 2f, usage);
        }

        private static void BuildGreenery(공간참고자료 data,
            사가정공간표현Composition composition, Chunk[,] chunks, BuildReport report)
        {
            var used = new List<Vector2>();
            var buildingPolygons = (data.buildings ?? Array.Empty<참고건물>())
                .Where(value => value != null).Select(value => Polygon(value.points))
                .Where(value => value.Count >= 3).ToList();
            if (composition?.OverlayApplied == true)
                buildingPolygons.AddRange(composition.Buildings
                    .SelectMany(value => value.Parts)
                    .Select(value => Polygon(value.Outer))
                    .Where(value => value.Count >= 3));
            var roads = (data.roads ?? Array.Empty<참고도로>())
                .Where(value => IsValidRoad(value) && Major(value.kind))
                .OrderBy(value => value.id, StringComparer.Ordinal);
            foreach (var road in roads)
            {
                var a = new Vector2(road.x1, road.z1);
                var b = new Vector2(road.x2, road.z2);
                var distance = Vector2.Distance(a, b);
                if (distance < 14f) continue;
                var direction = (b - a).normalized;
                var side = new Vector2(-direction.y, direction.x);
                for (var along = 8f; along < distance - 4f; along += 23f)
                    foreach (var sign in new[] { -1f, 1f })
                    {
                        var point = a + direction * along
                            + side * (RoadWidth(road.kind) * .5f + 4.2f) * sign;
                        if (used.Count >= 250 || !InsideSlab(data, point)) continue;
                        if (used.Any(value => (value - point).sqrMagnitude < 81f)) continue;
                        if (buildingPolygons.Any(value => Inside(point, value)
                                                          || EdgeDistance(point, value) < 2.8f)) continue;
                        if ((data.roads ?? Array.Empty<참고도로>()).Any(value => IsValidRoad(value)
                            && SegmentDistance(point, new Vector2(value.x1, value.z1), new Vector2(value.x2, value.z2))
                            < RoadWidth(value.kind) * .5f + 2.2f)) continue;

                        used.Add(point);
                        var height = 5f + used.Count % 3;
                        var batch = ChunkAt(data, chunks, point);
                        batch.Opaque.Box(new Vector3(point.x, BuildingBaseElevation + height * .34f, point.y),
                            new Vector3(.38f, height * .68f, .38f), new Color(.34f, .24f, .13f), 4f);
                        AddCrown(batch.Opaque, new Vector3(point.x, BuildingBaseElevation + height * .73f, point.y),
                            new Vector3(2.1f, height * .42f, 2.1f),
                            used.Count % 3 == 0 ? new Color(.58f, .68f, .22f)
                            : used.Count % 3 == 1 ? new Color(.34f, .49f, .18f)
                            : new Color(.68f, .72f, .28f));
                        var shadowLength = Mathf.Min(height * .18f, MaximumShadowLength);
                        var directionShadow = new Vector2(-.78f, .62f).normalized * shadowLength;
                        var outline = new List<Vector2>();
                        for (var i = 0; i < 8; i++)
                        {
                            var angle = i * Mathf.PI * .25f;
                            outline.Add(point + directionShadow
                                + new Vector2(Mathf.Cos(angle) * 2.4f, Mathf.Sin(angle) * 2.1f));
                        }
                        AddShadow(batch.Shadow, outline, data, UmbraAlpha, 0f);
                        AddShadow(batch.Shadow, outline, data, PenumbraAlpha, .45f);
                        report.DecorativeTreeCount++;
                    }
            }
        }

        private static void AddCrown(MeshBatch batch, Vector3 center, Vector3 size, Color color)
        {
            const int sides = 7;
            for (var i = 0; i < sides; i++)
            {
                var a = i * Mathf.PI * 2f / sides;
                var b = (i + 1) * Mathf.PI * 2f / sides;
                var va = new Vector3(Mathf.Cos(a) * size.x, 0f, Mathf.Sin(a) * size.z);
                var vb = new Vector3(Mathf.Cos(b) * size.x, 0f, Mathf.Sin(b) * size.z);
                batch.Quad(center + va - Vector3.up * size.y * .35f,
                    center + vb - Vector3.up * size.y * .35f,
                    center + vb * .65f + Vector3.up * size.y * .55f,
                    center + va * .65f + Vector3.up * size.y * .55f, color, 4f);
                batch.Quad(center + va * .65f + Vector3.up * size.y * .55f,
                    center + vb * .65f + Vector3.up * size.y * .55f,
                    center + Vector3.up * size.y, center + Vector3.up * size.y, color, 4f);
                batch.Quad(center + va - Vector3.up * size.y * .35f,
                    center - Vector3.up * size.y, center - Vector3.up * size.y,
                    center + vb - Vector3.up * size.y * .35f, color, 4f);
            }
        }

        private static void AddShadow(MeshBatch batch, IEnumerable<Vector2> source,
            공간참고자료 data, float alpha, float expand)
        {
            var polygon = source.ToList();
            if (polygon.Count < 3) return;
            if (expand > 0f)
            {
                var center = polygon.Aggregate(Vector2.zero, (sum, point) => sum + point) / polygon.Count;
                for (var i = 0; i < polygon.Count; i++)
                {
                    var direction = polygon[i] - center;
                    polygon[i] += direction.sqrMagnitude > .0001f ? direction.normalized * expand : Vector2.zero;
                }
            }
            polygon = ClipToSlab(polygon, data.offsetX - data.halfExtent, data.offsetX + data.halfExtent,
                data.offsetZ - data.halfExtent, data.offsetZ + data.halfExtent);
            if (polygon.Count < 3) return;
            var color = new Color(.10f, .15f, .20f, alpha);
            var first = batch.Vertices.Count;
            foreach (var point in polygon)
            {
                batch.Vertices.Add(new Vector3(point.x, ShadowY + (alpha < UmbraAlpha ? -.004f : 0f), point.y));
                batch.Colors.Add(color);
                batch.SurfaceData.Add(new Vector2(5f, alpha));
            }
            for (var i = 1; i < polygon.Count - 1; i++)
                batch.Triangles.AddRange(new[] { first, first + i + 1, first + i });
        }

        private static List<Vector2> ClipToSlab(List<Vector2> polygon,
            float minX, float maxX, float minZ, float maxZ)
        {
            var result = Clip(polygon, 0, minX, true);
            result = Clip(result, 0, maxX, false);
            result = Clip(result, 1, minZ, true);
            return Clip(result, 1, maxZ, false);
        }

        private static List<Vector2> Clip(List<Vector2> input, int axis, float boundary, bool keepGreater)
        {
            var output = new List<Vector2>();
            if (input.Count == 0) return output;
            var previous = input[input.Count - 1];
            var previousValue = axis == 0 ? previous.x : previous.y;
            var previousInside = keepGreater ? previousValue >= boundary : previousValue <= boundary;
            foreach (var current in input)
            {
                var currentValue = axis == 0 ? current.x : current.y;
                var currentInside = keepGreater ? currentValue >= boundary : currentValue <= boundary;
                if (currentInside != previousInside)
                {
                    var denominator = currentValue - previousValue;
                    var t = Mathf.Abs(denominator) < .00001f ? 0f : (boundary - previousValue) / denominator;
                    output.Add(Vector2.Lerp(previous, current, Mathf.Clamp01(t)));
                }
                if (currentInside) output.Add(current);
                previous = current;
                previousValue = currentValue;
                previousInside = currentInside;
            }
            return output;
        }

        private static Chunk ChunkAt(공간참고자료 data, Chunk[,] chunks, Vector2 point)
        {
            var size = data.halfExtent * 2f / ChunkAxisCount;
            var x = Mathf.Clamp(Mathf.FloorToInt((point.x - data.offsetX + data.halfExtent) / size),
                0, ChunkAxisCount - 1);
            var z = Mathf.Clamp(Mathf.FloorToInt((point.y - data.offsetZ + data.halfExtent) / size),
                0, ChunkAxisCount - 1);
            return chunks[x, z];
        }

        private static List<Vector2> Polygon(참고점[] points)
        {
            var result = new List<Vector2>();
            if (points == null) return result;
            foreach (var point in points)
            {
                if (point == null || !float.IsFinite(point.x) || !float.IsFinite(point.z)) return new List<Vector2>();
                var value = new Vector2(point.x, point.z);
                if (result.Count == 0 || (result[result.Count - 1] - value).sqrMagnitude > .000001f)
                    result.Add(value);
            }
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude <= .000001f)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        private static List<Vector2> Polygon(IReadOnlyList<사가정공간표현Point> points)
        {
            var result = new List<Vector2>();
            if (points == null) return result;
            foreach (var point in points)
            {
                if (double.IsNaN(point.X) || double.IsInfinity(point.X)
                    || double.IsNaN(point.Z) || double.IsInfinity(point.Z))
                    return new List<Vector2>();
                var value = new Vector2((float)point.X, (float)point.Z);
                if (!float.IsFinite(value.x) || !float.IsFinite(value.y)) return new List<Vector2>();
                if (result.Count == 0 || (result[result.Count - 1] - value).sqrMagnitude > .000001f)
                    result.Add(value);
            }
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude <= .000001f)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        private static List<Vector2> ScalePolygon(IReadOnlyList<Vector2> polygon, Vector2 center, float scale)
        {
            var result = new List<Vector2>(polygon.Count);
            foreach (var point in polygon) result.Add(center + (point - center) * scale);
            return result;
        }

        private static List<Vector2> Hull(IEnumerable<Vector2> values)
        {
            var points = values.Distinct().OrderBy(value => value.x).ThenBy(value => value.y).ToArray();
            if (points.Length < 3) return points.ToList();
            var hull = new List<Vector2>();
            foreach (var point in points)
            {
                while (hull.Count > 1 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            var lower = hull.Count;
            for (var i = points.Length - 2; i >= 0; i--)
            {
                while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            var area = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Count];
                area += p.x * q.y - q.x * p.y;
            }
            return area * .5f;
        }

        private static int Usage(string kind)
        {
            var value = (kind ?? string.Empty).ToLowerInvariant();
            if (value.Contains("apartments") || value.Contains("공동주택")
                || value.Contains("02000")) return 3;
            if (value.Contains("civic") || value.Contains("school") || value.Contains("hospital")
                || value.Contains("kindergarten") || value.Contains("church")
                || value.Contains("종교") || value.Contains("교육") || value.Contains("의료")
                || value.Contains("노유자") || value.Contains("운동") || value.Contains("06000")
                || value.Contains("09000") || value.Contains("11000")) return 2;
            if (value.Contains("commercial") || value.Contains("retail") || value.Contains("office")
                || value.Contains("근린생활") || value.Contains("판매") || value.Contains("업무")
                || value.Contains("숙박") || value.Contains("위락") || value.Contains("자동차")
                || value.Contains("03000") || value.Contains("04000") || value.Contains("07000")) return 1;
            return 0;
        }

        private static Color FacadeColor(int usage, int hash)
        {
            var palettes = usage switch
            {
                1 => new[] { new Color(.89f, .55f, .38f), new Color(.87f, .72f, .48f), new Color(.49f, .70f, .66f) },
                2 => new[] { new Color(.91f, .80f, .58f), new Color(.82f, .86f, .76f), new Color(.93f, .67f, .48f) },
                3 => new[] { new Color(.91f, .90f, .82f), new Color(.80f, .84f, .81f), new Color(.93f, .82f, .70f) },
                _ => new[] { new Color(.91f, .84f, .68f), new Color(.86f, .67f, .48f), new Color(.93f, .89f, .77f) },
            };
            return palettes[hash % palettes.Length];
        }

        private static Color RoofColor(int usage, int hash)
        {
            var palettes = usage == 3
                ? new[] { new Color(.56f, .61f, .60f), new Color(.67f, .70f, .65f) }
                : new[] { new Color(.56f, .31f, .23f), new Color(.49f, .56f, .55f), new Color(.72f, .48f, .31f) };
            return palettes[(hash / 7) % palettes.Length];
        }

        private static bool IsValidRoad(참고도로 road) => road != null
            && float.IsFinite(road.x1) && float.IsFinite(road.z1)
            && float.IsFinite(road.x2) && float.IsFinite(road.z2)
            && (new Vector2(road.x2 - road.x1, road.z2 - road.z1)).sqrMagnitude > .0001f;

        private static int Hash(string value)
        {
            uint hash = 2166136261;
            foreach (var character in value ?? string.Empty) hash = unchecked((hash ^ character) * 16777619);
            return (int)(hash & 0x7fffffff);
        }

        public static float RoadWidth(string kind) => kind switch
        {
            "primary" => 15f,
            "secondary" => 12f,
            "secondary_link" or "tertiary" => 8f,
            "residential" => 5.5f,
            "service" => 4.5f,
            "footway" or "steps" or "path" or "cycleway" or "pedestrian" => 2.2f,
            _ => 3.5f,
        };

        private static bool Major(string kind)
            => kind is "primary" or "secondary" or "secondary_link" or "tertiary";

        private static bool InsideSlab(공간참고자료 data, Vector2 point)
            => Mathf.Abs(point.x - data.offsetX) <= data.halfExtent - 4f
               && Mathf.Abs(point.y - data.offsetZ) <= data.halfExtent - 4f;

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            return Vector2.Distance(point,
                a + ab * Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(.0001f, ab.sqrMagnitude)));
        }

        private static float EdgeDistance(Vector2 point, 참고점[] points)
        {
            if (points == null || points.Length < 2) return float.MaxValue;
            var distance = float.MaxValue;
            for (var i = 1; i < points.Length; i++)
                distance = Mathf.Min(distance, SegmentDistance(point,
                    new Vector2(points[i - 1].x, points[i - 1].z), new Vector2(points[i].x, points[i].z)));
            return distance;
        }

        private static float EdgeDistance(Vector2 point, IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2) return float.MaxValue;
            var distance = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
                distance = Mathf.Min(distance,
                    SegmentDistance(point, points[i], points[(i + 1) % points.Count]));
            return distance;
        }

        private static bool Inside(Vector2 point, IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3) return false;
            var inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                if ((points[i].y > point.y) != (points[j].y > point.y)
                    && point.x < (points[j].x - points[i].x) * (point.y - points[i].y)
                    / (points[j].y - points[i].y) + points[i].x)
                    inside = !inside;
            return inside;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
            => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }
}
