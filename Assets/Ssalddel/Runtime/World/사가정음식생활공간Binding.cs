using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable] public sealed class 사가정음식생활공간Data
    {
        public string schemaVersion, revision, mapRevision, mapSha256, coordinateMethod;
        public double originLatitude, originLongitude, offsetX, offsetZ;
        public string storeName, storeAddress, buildingId, authority, gameRouteNotice, attribution;
        public double storeLatitude, storeLongitude;
        public 사가정음식생활점 buildingPoint;
        public bool facadeVisible;
        public 사가정음식생활Anchor[] anchors;
        public 사가정음식생활Segment[] segments;
        public 사가정음식생활휴식구역[] restAreas;
    }
    [Serializable] public sealed class 사가정음식생활점 { public double x, z; }
    [Serializable] public sealed class 사가정음식생활Anchor
    { public string id; public 사가정음식생활점 logical, game; }
    [Serializable] public sealed class 사가정음식생활Segment
    {
        public string id, kind;
        public 사가정음식생활점 logicalStart, logicalEnd;
        public 사가정음식생활점[] gamePoints;
        public string[] sourceRoadIds;
    }
    [Serializable] public sealed class 사가정음식생활휴식구역
    { public string id; public 사가정음식생활점 work, roadEnd, rest; }

    /// <summary>Core의 합성 좌표를 검증된 게임 표현 경로로만 투영한다.
    /// Session/업무/통행 상태를 쓰지 않으며 미등록 좌표는 보간이나 인근 위치로 대체하지 않는다.</summary>
    public sealed class 사가정음식생활공간Binding
    {
        private readonly 사가정음식생활Segment[] segments;
        private readonly Dictionary<string, 사가정음식생활점> anchors;
        private readonly 사가정음식생활휴식구역[] restAreas;
        private readonly 사가정음식생활공간Data definition;
        public 사가정음식생활공간Data Data => Copy(definition);
        public const string Schema = "sagajeong-food-life-binding.v1";
        private const double Tolerance = .00001;

        public 사가정음식생활공간Binding(사가정음식생활공간Data data, 공간참고자료 map, byte[] mapBytes)
        {
            if (data == null || map == null || mapBytes == null || mapBytes.Length == 0)
                throw new ArgumentException("SagajeongFoodLifeSourceMissing");
            if (data.schemaVersion != Schema || data.revision != "sagajeong-food-life.r1"
                || data.mapRevision != map.revision || data.mapRevision != "sagajeong-reference.r3")
                throw new ArgumentException("SagajeongFoodLifeRevisionMismatch");
            using (var sha = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(mapBytes)).Replace("-", ""), data.mapSha256, StringComparison.Ordinal))
                    throw new ArgumentException("SagajeongFoodLifeHashMismatch");
            if (data.coordinateMethod != "WGS84-ECEF-ENU-at-zero-altitude" || map.coordinateMethod != data.coordinateMethod
                || data.originLatitude != 37.5806971 || data.originLongitude != 127.0884106
                || data.offsetX != 550 || data.offsetZ != 8 || map.offsetX != data.offsetX || map.offsetZ != data.offsetZ
                || data.authority != "ReferenceOnly;NoTraversal;NoOperational")
                throw new ArgumentException("SagajeongFoodLifeFrameOrAuthorityMismatch");
            if (map.buildings == null || map.roads == null || data.buildingPoint == null
                || data.anchors == null || data.segments == null || data.segments.Length == 0)
                throw new ArgumentException("SagajeongFoodLifeBindingIncomplete");
            var buildings = map.buildings.Where(b => b.id == data.buildingId).ToArray();
            if (buildings.Length != 1 || buildings[0].street != "용마산로51길" || buildings[0].houseNumber != "50"
                || data.storeAddress != "서울 중랑구 용마산로51길 50 1층")
                throw new ArgumentException("SagajeongFoodLifeBuildingAddressMismatch");
            var geographic = GeographicPoint(data.storeLatitude, data.storeLongitude);
            if (Distance(geographic, data.buildingPoint) > .02 || !Contains(buildings[0].points, data.buildingPoint))
                throw new ArgumentException("SagajeongFoodLifeBuildingPointMismatch");
            var roads = map.roads.ToDictionary(r => r.id, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var segment in data.segments)
            {
                if (segment == null || string.IsNullOrWhiteSpace(segment.id) || !ids.Add(segment.id)
                    || !Finite(segment.logicalStart) || !Finite(segment.logicalEnd)
                    || Distance(segment.logicalStart, segment.logicalEnd) <= Tolerance
                    || segment.gamePoints == null || segment.gamePoints.Length < 2 || segment.gamePoints.Any(p => !Finite(p))
                    || segment.sourceRoadIds == null)
                    throw new ArgumentException("SagajeongFoodLifeSegmentInvalid");
                for (var i = 1; i < segment.gamePoints.Length; i++)
                {
                    if (Distance(segment.gamePoints[i - 1], segment.gamePoints[i]) <= Tolerance)
                        throw new ArgumentException("SagajeongFoodLifeZeroSegment");
                    if (segment.sourceRoadIds.Length > 0 && !segment.sourceRoadIds.Any(id => roads.TryGetValue(id, out var r)
                        && OnLine(segment.gamePoints[i - 1], Point(r.x1, r.z1), Point(r.x2, r.z2), .003, out _)
                        && OnLine(segment.gamePoints[i], Point(r.x1, r.z1), Point(r.x2, r.z2), .003, out _)))
                        throw new ArgumentException("SagajeongFoodLifeRoadBindingMismatch");
                }
                if (segment.sourceRoadIds.Length == 0 && segment.kind != "FictionalGameAccess"
                    || segment.sourceRoadIds.Length > 0 && segment.kind != "ReferenceRoadReconstruction")
                    throw new ArgumentException("SagajeongFoodLifeRouteAuthorityInvalid");
            }
            // 접점에서 위치가 튀는 잘못된 경로 결속. 접점뿐 아니라 논리선 교차도 확인한다.
            segments = data.segments;
            foreach (var segment in segments)
                foreach (var endpoint in new[] { segment.logicalStart, segment.logicalEnd }) Project(endpoint.x, endpoint.z);
            anchors = new Dictionary<string, 사가정음식생활점>(StringComparer.Ordinal);
            foreach (var anchor in data.anchors)
            {
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.id) || anchors.ContainsKey(anchor.id)
                    || !Finite(anchor.logical) || !Finite(anchor.game) || Distance(Project(anchor.logical.x, anchor.logical.z), anchor.game) > .02)
                    throw new ArgumentException("SagajeongFoodLifeAnchorMismatch");
                anchors.Add(anchor.id, Point(anchor.game.x, anchor.game.z));
            }
            foreach (var id in new[] { "restaurant", "pickup", "home-a", "home-b", "mart" })
                if (!anchors.ContainsKey(id)) throw new ArgumentException("SagajeongFoodLifeAnchorMissing");
            if (data.restAreas == null || data.restAreas.Length != 5) throw new ArgumentException("SagajeongFoodLifeRestBindingMissing");
            foreach (var area in data.restAreas)
            {
                if (area == null || !Finite(area.work) || !Finite(area.roadEnd) || !Finite(area.rest)
                    || area.rest.x != area.work.x + 1.5 || area.rest.z != area.work.z
                    || Math.Abs((area.roadEnd.x - area.work.x) * (area.rest.z - area.work.z)
                        - (area.roadEnd.z - area.work.z) * (area.rest.x - area.work.x)) < Tolerance)
                    throw new ArgumentException("SagajeongFoodLifeRestBindingInvalid");
                Project(area.work.x, area.work.z); Project(area.roadEnd.x, area.roadEnd.z); Project(area.rest.x, area.rest.z);
            }
            restAreas = data.restAreas.Select(a => new 사가정음식생활휴식구역 { id = a.id, work = Point(a.work.x, a.work.z), roadEnd = Point(a.roadEnd.x, a.roadEnd.z), rest = Point(a.rest.x, a.rest.z) }).ToArray();
            // 복사한 경로만 사용하므로 호출자의 DTO 수정은 살아 있는 투영을 바꾸지 않는다.
            segments = segments.Select(s => new 사가정음식생활Segment { id = s.id, kind = s.kind,
                logicalStart = Point(s.logicalStart.x, s.logicalStart.z), logicalEnd = Point(s.logicalEnd.x, s.logicalEnd.z),
                gamePoints = s.gamePoints.Select(p => Point(p.x, p.z)).ToArray(), sourceRoadIds = s.sourceRoadIds.ToArray() }).ToArray();
            definition = Copy(data);
        }

        public 사가정음식생활점 Anchor(string id)
        {
            if (!anchors.TryGetValue(id, out var point)) throw new ArgumentException("SagajeongFoodLifeAnchorUnknown");
            return Point(point.x, point.z);
        }
        public 사가정음식생활점 Project(double x, double z)
        {
            var logical = Point(x, z);
            if (!Finite(logical)) throw new ArgumentException("SagajeongFoodLifePositionInvalid");
            사가정음식생활점 result = null;
            foreach (var segment in segments)
            {
                if (!OnLine(logical, segment.logicalStart, segment.logicalEnd, Tolerance, out var fraction)) continue;
                var mapped = Along(segment.gamePoints, fraction);
                if (result != null && Distance(result, mapped) > .02)
                    throw new ArgumentException("SagajeongFoodLifeContinuityMismatch");
                result = mapped;
            }
            return result ?? throw new ArgumentException("SagajeongFoodLifePositionUnbound");
        }
        /// <summary>Core의 기존 직선 휴식 이동만 명시된 slot 접근 구역으로 투영한다.
        /// 일반 좌표와 다른 단계에는 이 좁은 예외가 적용되지 않는다.</summary>
        public 사가정음식생활점 ProjectActor(가상생활NpcSnapshot actor)
        {
            if (actor == null) throw new ArgumentException("SagajeongFoodLifeActorMissing");
            try { return Project(actor.X, actor.Z); }
            catch (ArgumentException e) when (e.Message == "SagajeongFoodLifePositionUnbound"
                && actor.Role == "Courier" && new[] { "actor:synthetic-courier:1", "actor:synthetic-courier:2", "actor:synthetic-courier:3" }.Contains(actor.ActorId)
                && (actor.Stage == "ReturningToRest" || actor.Stage == "GoingHome" && actor.HomeRouteStep == 0))
            {
                var areas = restAreas.Where(a => a.work.x == actor.WorkX && a.work.z == actor.WorkZ).ToArray();
                if (areas.Length != 1) throw new ArgumentException("SagajeongFoodLifeRestWorkMismatch");
                var area = areas[0]; var p = Point(actor.X, actor.Z); var a = area.work; var b = area.roadEnd; var c = area.rest;
                var denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                var u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / denominator;
                var v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / denominator; var w = 1 - u - v;
                if (u < -Tolerance || v < -Tolerance || w < -Tolerance) throw new ArgumentException("SagajeongFoodLifeRestPositionUnbound");
                var ga = Project(a.x, a.z); var gb = Project(b.x, b.z); var gc = Project(c.x, c.z);
                return Point(u * ga.x + v * gb.x + w * gc.x, u * ga.z + v * gb.z + w * gc.z);
            }
        }
        private static 사가정음식생활점 Along(사가정음식생활점[] points, double fraction)
        {
            var lengths = new double[points.Length - 1];
            var total = 0d;
            for (var i = 0; i < lengths.Length; i++) { lengths[i] = Distance(points[i], points[i + 1]); total += lengths[i]; }
            var remaining = total * Math.Max(0, Math.Min(1, fraction));
            for (var i = 0; i < lengths.Length; i++)
            {
                if (remaining <= lengths[i] || i == lengths.Length - 1)
                    return Point(points[i].x + (points[i + 1].x - points[i].x) * remaining / lengths[i],
                        points[i].z + (points[i + 1].z - points[i].z) * remaining / lengths[i]);
                remaining -= lengths[i];
            }
            throw new InvalidOperationException("SagajeongFoodLifePathEmpty");
        }
        private static bool OnLine(사가정음식생활점 p, 사가정음식생활점 a, 사가정음식생활점 b, double epsilon, out double fraction)
        {
            var dx = b.x - a.x; var dz = b.z - a.z; var squared = dx * dx + dz * dz;
            fraction = squared == 0 ? 0 : ((p.x - a.x) * dx + (p.z - a.z) * dz) / squared;
            return squared > 0 && fraction >= -epsilon && fraction <= 1 + epsilon
                && Distance(p, Point(a.x + fraction * dx, a.z + fraction * dz)) <= epsilon;
        }
        private static bool Contains(참고점[] polygon, 사가정음식생활점 p)
        {
            if (polygon == null || polygon.Length < 4) return false;
            var inside = false;
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }
        private static 사가정음식생활점 GeographicPoint(double latitude, double longitude)
        {
            double[] Ecef(double lat, double lon)
            {
                var l = lat * Math.PI / 180; var o = lon * Math.PI / 180;
                const double a = 6378137; const double f = 1 / 298.257223563; var e = f * (2 - f);
                var n = a / Math.Sqrt(1 - e * Math.Sin(l) * Math.Sin(l));
                return new[] { n * Math.Cos(l) * Math.Cos(o), n * Math.Cos(l) * Math.Sin(o), n * (1 - e) * Math.Sin(l) };
            }
            var zero = Ecef(37.5806971, 127.0884106); var target = Ecef(latitude, longitude);
            var v = target.Zip(zero, (x, y) => x - y).ToArray(); var lat0 = 37.5806971 * Math.PI / 180; var lon0 = 127.0884106 * Math.PI / 180;
            return Point(550 - Math.Sin(lon0) * v[0] + Math.Cos(lon0) * v[1],
                8 - Math.Sin(lat0) * Math.Cos(lon0) * v[0] - Math.Sin(lat0) * Math.Sin(lon0) * v[1] + Math.Cos(lat0) * v[2]);
        }
        private static bool Finite(사가정음식생활점 p) => p != null && !double.IsNaN(p.x) && !double.IsInfinity(p.x) && !double.IsNaN(p.z) && !double.IsInfinity(p.z);
        private static double Distance(사가정음식생활점 a, 사가정음식생활점 b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
        private static 사가정음식생활점 Point(double x, double z) => new 사가정음식생활점 { x = x, z = z };
        private static 사가정음식생활공간Data Copy(사가정음식생활공간Data d) => new 사가정음식생활공간Data {
            schemaVersion = d.schemaVersion, revision = d.revision, mapRevision = d.mapRevision, mapSha256 = d.mapSha256,
            coordinateMethod = d.coordinateMethod, originLatitude = d.originLatitude, originLongitude = d.originLongitude,
            offsetX = d.offsetX, offsetZ = d.offsetZ, storeName = d.storeName, storeAddress = d.storeAddress, buildingId = d.buildingId,
            authority = d.authority, gameRouteNotice = d.gameRouteNotice, attribution = d.attribution,
            storeLatitude = d.storeLatitude, storeLongitude = d.storeLongitude, buildingPoint = Point(d.buildingPoint.x, d.buildingPoint.z),
            facadeVisible = d.facadeVisible,
            anchors = d.anchors.Select(a => new 사가정음식생활Anchor { id = a.id, logical = Point(a.logical.x, a.logical.z), game = Point(a.game.x, a.game.z) }).ToArray(),
            segments = d.segments.Select(s => new 사가정음식생활Segment { id = s.id, kind = s.kind,
                logicalStart = Point(s.logicalStart.x, s.logicalStart.z), logicalEnd = Point(s.logicalEnd.x, s.logicalEnd.z),
                gamePoints = s.gamePoints.Select(p => Point(p.x, p.z)).ToArray(), sourceRoadIds = s.sourceRoadIds.ToArray() }).ToArray(),
            restAreas = d.restAreas.Select(a => new 사가정음식생활휴식구역 { id = a.id, work = Point(a.work.x, a.work.z), roadEnd = Point(a.roadEnd.x, a.roadEnd.z), rest = Point(a.rest.x, a.rest.z) }).ToArray()
        };
    }
}
