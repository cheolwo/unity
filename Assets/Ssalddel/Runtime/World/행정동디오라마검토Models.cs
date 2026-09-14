using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>
    /// 서버 해석기가 수용한 행정동 도형을 Unity 표현에서만 쓰도록 깊은 복사한 상태 사본이다.
    /// 원본 좌표의 중심을 로컬 원점으로 옮기지만 주소·통행·게임플레이 의미는 만들지 않는다.
    /// </summary>
    public sealed class 행정동디오라마검토Snapshot
    {
        private readonly IReadOnlyDictionary<string, 행정동디오라마검토Building> buildingsById;

        internal 행정동디오라마검토Snapshot(
            string administrativeAreaStableId,
            string displayName,
            string sourceVintage,
            string projectionHashSha256,
            string readinessCode,
            double sourceCenterX,
            double sourceCenterZ,
            double metersPerUnit,
            행정동디오라마검토Bounds bounds,
            IReadOnlyList<행정동디오라마검토Point> boundary,
            IReadOnlyList<행정동디오라마검토Building> buildings,
            IReadOnlyList<행정동디오라마검토RoadSegment> roads,
            int tileCount)
        {
            AdministrativeAreaStableId = administrativeAreaStableId;
            DisplayName = displayName;
            SourceVintage = sourceVintage;
            ProjectionHashSha256 = projectionHashSha256;
            ReadinessCode = readinessCode;
            SourceCenterX = sourceCenterX;
            SourceCenterZ = sourceCenterZ;
            MetersPerUnit = metersPerUnit;
            Bounds = bounds;
            Boundary = boundary;
            Buildings = buildings;
            Roads = roads;
            TileCount = tileCount;
            buildingsById = buildings.ToDictionary(value => value.BuildingStableId,
                StringComparer.Ordinal);
        }

        public string AdministrativeAreaStableId { get; }
        public string DisplayName { get; }
        public string SourceVintage { get; }
        public string ProjectionHashSha256 { get; }
        public string ReadinessCode { get; }
        public double SourceCenterX { get; }
        public double SourceCenterZ { get; }
        public double MetersPerUnit { get; }
        public 행정동디오라마검토Bounds Bounds { get; }
        public IReadOnlyList<행정동디오라마검토Point> Boundary { get; }
        public IReadOnlyList<행정동디오라마검토Building> Buildings { get; }
        public IReadOnlyList<행정동디오라마검토RoadSegment> Roads { get; }
        public int TileCount { get; }
        public bool ObservationPresentationOnly => true;
        public bool DistributionApproved => false;
        public bool TraversalReady => false;
        public bool GameplayReady => false;

        public bool TryGetBuilding(string buildingStableId,
            out 행정동디오라마검토Building building)
            => buildingsById.TryGetValue(buildingStableId ?? string.Empty, out building);

        public bool TrySelectBuildingAt(double localX, double localZ,
            out 행정동디오라마검토Building building)
        {
            building = null;
            var bestArea = double.PositiveInfinity;
            foreach (var candidate in Buildings)
            {
                if (!candidate.Contains(localX, localZ)) continue;
                if (candidate.FootprintArea < bestArea - 행정동디오라마검토Projection.Epsilon
                    || (Math.Abs(candidate.FootprintArea - bestArea)
                        <= 행정동디오라마검토Projection.Epsilon
                        && (building == null || string.CompareOrdinal(
                            candidate.BuildingStableId, building.BuildingStableId) < 0)))
                {
                    building = candidate;
                    bestArea = candidate.FootprintArea;
                }
            }
            return building != null;
        }
    }

    public sealed class 행정동디오라마검토Point
    {
        internal 행정동디오라마검토Point(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }
    }

    public sealed class 행정동디오라마검토Bounds
    {
        internal 행정동디오라마검토Bounds(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double Width => MaxX - MinX;
        public double Depth => MaxZ - MinZ;
    }

    public sealed class 행정동디오라마검토Building
    {
        internal 행정동디오라마검토Building(string buildingStableId,
            string categoryCode, string evidenceKindCode, double displayedHeight,
            string displayedHeightKind, IReadOnlyList<행정동디오라마검토Point> footprint)
        {
            BuildingStableId = buildingStableId;
            CategoryCode = categoryCode;
            EvidenceKindCode = evidenceKindCode;
            DisplayedHeight = displayedHeight;
            DisplayedHeightKind = displayedHeightKind;
            Footprint = footprint;

            var minX = double.PositiveInfinity;
            var minZ = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxZ = double.NegativeInfinity;
            var signedArea = 0d;
            var centroidX = 0d;
            var centroidZ = 0d;
            for (var index = 0; index < footprint.Count; index++)
            {
                var point = footprint[index];
                var next = footprint[(index + 1) % footprint.Count];
                minX = Math.Min(minX, point.X);
                minZ = Math.Min(minZ, point.Z);
                maxX = Math.Max(maxX, point.X);
                maxZ = Math.Max(maxZ, point.Z);
                var cross = point.X * next.Z - next.X * point.Z;
                signedArea += cross;
                centroidX += (point.X + next.X) * cross;
                centroidZ += (point.Z + next.Z) * cross;
            }

            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            FootprintArea = Math.Abs(signedArea) * .5d;
            if (Math.Abs(signedArea) > 행정동디오라마검토Projection.Epsilon)
            {
                CentroidX = centroidX / (3d * signedArea);
                CentroidZ = centroidZ / (3d * signedArea);
            }
            else
            {
                CentroidX = (minX + maxX) * .5d;
                CentroidZ = (minZ + maxZ) * .5d;
            }
        }

        public string BuildingStableId { get; }
        public string CategoryCode { get; }
        public string EvidenceKindCode { get; }
        public double DisplayedHeight { get; }
        public string DisplayedHeightKind { get; }
        public IReadOnlyList<행정동디오라마검토Point> Footprint { get; }
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double CentroidX { get; }
        public double CentroidZ { get; }
        public double FootprintArea { get; }

        internal bool Contains(double x, double z)
        {
            if (x < MinX - 행정동디오라마검토Projection.Epsilon
                || x > MaxX + 행정동디오라마검토Projection.Epsilon
                || z < MinZ - 행정동디오라마검토Projection.Epsilon
                || z > MaxZ + 행정동디오라마검토Projection.Epsilon) return false;

            var inside = false;
            for (int current = 0, previous = Footprint.Count - 1;
                 current < Footprint.Count; previous = current++)
            {
                var a = Footprint[previous];
                var b = Footprint[current];
                if (OnSegment(a, b, x, z)) return true;
                if ((a.Z > z) == (b.Z > z)) continue;
                var intersectX = (b.X - a.X) * (z - a.Z) / (b.Z - a.Z) + a.X;
                if (x < intersectX) inside = !inside;
            }
            return inside;
        }

        private static bool OnSegment(행정동디오라마검토Point a,
            행정동디오라마검토Point b, double x, double z)
        {
            var cross = (b.X - a.X) * (z - a.Z) - (b.Z - a.Z) * (x - a.X);
            if (Math.Abs(cross) > 행정동디오라마검토Projection.Epsilon) return false;
            return x >= Math.Min(a.X, b.X) - 행정동디오라마검토Projection.Epsilon
                   && x <= Math.Max(a.X, b.X) + 행정동디오라마검토Projection.Epsilon
                   && z >= Math.Min(a.Z, b.Z) - 행정동디오라마검토Projection.Epsilon
                   && z <= Math.Max(a.Z, b.Z) + 행정동디오라마검토Projection.Epsilon;
        }
    }

    public sealed class 행정동디오라마검토RoadSegment
    {
        internal 행정동디오라마검토RoadSegment(string roadStableId,
            string evidenceKindCode, 행정동디오라마검토Point from,
            행정동디오라마검토Point to)
        {
            RoadStableId = roadStableId;
            EvidenceKindCode = evidenceKindCode;
            From = from;
            To = to;
        }

        public string RoadStableId { get; }
        public string EvidenceKindCode { get; }
        public 행정동디오라마검토Point From { get; }
        public 행정동디오라마검토Point To { get; }
    }

    public static class 행정동디오라마검토Projection
    {
        internal const double Epsilon = .000001d;
        public const string AcceptedDiagnostic = "AdministrativeDongGeometryReviewAccepted";

        public static bool TryCreate(AdministrativeDongDioramaApplyResult source,
            out 행정동디오라마검토Snapshot snapshot, out string diagnosticCode)
        {
            if (source == null)
            {
                snapshot = null;
                diagnosticCode = "AdministrativeDongReviewApplyResultMissing";
                return false;
            }
            try
            {
                snapshot = Create(source);
                diagnosticCode = AcceptedDiagnostic;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                snapshot = null;
                diagnosticCode = exception.Message;
                return false;
            }
        }

        public static 행정동디오라마검토Snapshot Create(
            AdministrativeDongDioramaApplyResult source)
        {
            if (source == null)
                throw new InvalidOperationException("AdministrativeDongReviewApplyResultMissing");
            if (!source.Accepted)
                throw new InvalidOperationException("AdministrativeDongReviewApplyResultRejected");

            var manifest = source.Manifest
                           ?? throw new InvalidOperationException("AdministrativeDongReviewManifestMissing");
            if (!manifest.ObservationPresentationOnly
                || !string.Equals(manifest.DataPolicyCode,
                    AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("AdministrativeDongReviewAuthorityRejected");
            if (manifest.DistributionApproved)
                throw new InvalidOperationException("AdministrativeDongReviewDistributionApprovedRejected");
            if (manifest.TraversalReady)
                throw new InvalidOperationException("AdministrativeDongReviewTraversalReadyRejected");
            if (manifest.GameplayReady)
                throw new InvalidOperationException("AdministrativeDongReviewGameplayReadyRejected");
            if (!ReviewReadiness(manifest.ReadinessCode))
                throw new InvalidOperationException("AdministrativeDongReviewReadinessRejected");
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(
                    manifest.AdministrativeAreaStableId))
                throw new InvalidOperationException("AdministrativeDongReviewStableIdInvalid");
            if (!string.Equals(manifest.SchemaVersion,
                    AdministrativeDongDioramaPolicy.SchemaVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("AdministrativeDongReviewSchemaVersionInvalid");
            if (!Sha256(manifest.ProjectionHashSha256))
                throw new InvalidOperationException("AdministrativeDongReviewProjectionHashMissing");
            if ((source.DisplayOverlays ?? Array.Empty<AdministrativeDongDisplayOverlay>()).Length != 0)
                throw new InvalidOperationException("AdministrativeDongReviewGeometryOnlyOverlayRejected");

            var frame = manifest.CoordinateFrame
                        ?? throw new InvalidOperationException("AdministrativeDongReviewCoordinateFrameMissing");
            if (string.IsNullOrWhiteSpace(frame.Method) || !Finite(frame.OriginLatitude)
                || !Finite(frame.OriginLongitude) || !Finite(frame.WorldOffsetX)
                || !Finite(frame.WorldOffsetZ) || !Finite(frame.MetersPerUnit)
                || Math.Abs(frame.MetersPerUnit - 1d) > Epsilon)
                throw new InvalidOperationException("AdministrativeDongReviewCoordinateFrameInvalid");
            var sourceBounds = manifest.Bounds
                               ?? throw new InvalidOperationException("AdministrativeDongReviewBoundsMissing");
            if (!Finite(sourceBounds.MinX) || !Finite(sourceBounds.MinZ)
                || !Finite(sourceBounds.MaxX) || !Finite(sourceBounds.MaxZ)
                || sourceBounds.MaxX - sourceBounds.MinX <= Epsilon
                || sourceBounds.MaxZ - sourceBounds.MinZ <= Epsilon)
                throw new InvalidOperationException("AdministrativeDongReviewBoundsInvalid");

            var centerX = (sourceBounds.MinX + sourceBounds.MaxX) * .5d;
            var centerZ = (sourceBounds.MinZ + sourceBounds.MaxZ) * .5d;
            var scale = frame.MetersPerUnit;
            행정동디오라마검토Point Point(AdministrativeDongDioramaPoint value)
            {
                if (value == null || !Finite(value.X) || !Finite(value.Z))
                    throw new InvalidOperationException("AdministrativeDongReviewPointInvalid");
                return new 행정동디오라마검토Point(
                    (value.X - centerX) / scale, (value.Z - centerZ) / scale);
            }

            var boundary = NormalizeFootprint(manifest.Boundary, Point,
                "AdministrativeDongReviewBoundaryInvalid");
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            var buildings = new List<행정동디오라마검토Building>();
            var roads = new List<행정동디오라마검토RoadSegment>();
            var tiles = source.Tiles ?? Array.Empty<AdministrativeDongDioramaTile>();
            var summaries = manifest.Tiles ?? Array.Empty<AdministrativeDongDioramaTileSummary>();
            if (tiles.Length != summaries.Length
                || summaries.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.TileStableId))
                || summaries.GroupBy(value => value.TileStableId, StringComparer.Ordinal)
                    .Any(group => group.Count() != 1))
                throw new InvalidOperationException("AdministrativeDongReviewTileSetInvalid");
            var summaryMap = summaries.ToDictionary(value => value.TileStableId,
                StringComparer.Ordinal);
            var tileIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tile in tiles.OrderBy(value => value.TileStableId, StringComparer.Ordinal))
            {
                if (tile == null
                    || string.IsNullOrWhiteSpace(tile.TileStableId)
                    || !tileIds.Add(tile.TileStableId)
                    || !string.Equals(tile.SchemaVersion, AdministrativeDongDioramaPolicy.SchemaVersion,
                        StringComparison.Ordinal)
                    || !string.Equals(tile.AdministrativeAreaStableId,
                        manifest.AdministrativeAreaStableId, StringComparison.Ordinal)
                    || !string.Equals(tile.ProjectionHashSha256,
                        manifest.ProjectionHashSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("AdministrativeDongReviewTileVersionInvalid");

                var tileBuildings = tile.Buildings ?? Array.Empty<AdministrativeDongDioramaBuilding>();
                var tileRoads = tile.Roads ?? Array.Empty<AdministrativeDongDioramaRoadSegment>();
                if (!summaryMap.TryGetValue(tile.TileStableId, out var summary)
                    || !Sha256(tile.TileHashSha256)
                    || !string.Equals(tile.TileHashSha256, summary.TileHashSha256,
                        StringComparison.OrdinalIgnoreCase)
                    || summary.BuildingCount != tileBuildings.Length
                    || summary.RoadSegmentCount != tileRoads.Length)
                    throw new InvalidOperationException("AdministrativeDongReviewTileHashOrCountInvalid");

                foreach (var value in tileBuildings
                             .OrderBy(item => item.BuildingStableId, StringComparer.Ordinal))
                {
                    if (value == null || string.IsNullOrWhiteSpace(value.BuildingStableId)
                        || !buildingIds.Add(value.BuildingStableId))
                        throw new InvalidOperationException("AdministrativeDongReviewBuildingIdentityInvalid");
                    var footprint = NormalizeFootprint(value.Footprint, Point,
                        "AdministrativeDongReviewBuildingGeometryInvalid");
                    var height = ResolveHeight(value, scale, out var heightKind);
                    buildings.Add(new 행정동디오라마검토Building(value.BuildingStableId,
                        value.CategoryCode ?? string.Empty, value.EvidenceKindCode ?? string.Empty,
                        height, heightKind, footprint));
                }

                foreach (var value in tileRoads
                             .OrderBy(item => item.RoadStableId, StringComparer.Ordinal)
                             .ThenBy(item => item.From?.X ?? double.MinValue)
                             .ThenBy(item => item.From?.Z ?? double.MinValue))
                {
                    if (value == null || string.IsNullOrWhiteSpace(value.RoadStableId))
                        throw new InvalidOperationException("AdministrativeDongReviewRoadIdentityInvalid");
                    var from = Point(value.From);
                    var to = Point(value.To);
                    var dx = to.X - from.X;
                    var dz = to.Z - from.Z;
                    if (dx * dx + dz * dz <= Epsilon * Epsilon)
                        throw new InvalidOperationException("AdministrativeDongReviewRoadGeometryInvalid");
                    roads.Add(new 행정동디오라마검토RoadSegment(value.RoadStableId,
                        value.EvidenceKindCode ?? string.Empty, from, to));
                }
            }

            buildings.Sort((left, right) => string.CompareOrdinal(
                left.BuildingStableId, right.BuildingStableId));
            var localBounds = new 행정동디오라마검토Bounds(
                (sourceBounds.MinX - centerX) / scale,
                (sourceBounds.MinZ - centerZ) / scale,
                (sourceBounds.MaxX - centerX) / scale,
                (sourceBounds.MaxZ - centerZ) / scale);
            return new 행정동디오라마검토Snapshot(
                manifest.AdministrativeAreaStableId,
                manifest.DisplayName ?? string.Empty,
                manifest.SourceVintage ?? string.Empty,
                manifest.ProjectionHashSha256,
                manifest.ReadinessCode,
                centerX,
                centerZ,
                scale,
                localBounds,
                Array.AsReadOnly(boundary),
                Array.AsReadOnly(buildings.ToArray()),
                Array.AsReadOnly(roads.ToArray()),
                tiles.Length);
        }

        private static 행정동디오라마검토Point[] NormalizeFootprint(
            IReadOnlyList<AdministrativeDongDioramaPoint> source,
            Func<AdministrativeDongDioramaPoint, 행정동디오라마검토Point> map,
            string errorCode)
        {
            if (source == null || source.Count < 3)
                throw new InvalidOperationException(errorCode);
            var points = source.Select(map).ToList();
            if (points.Count > 1 && Same(points[0], points[points.Count - 1]))
                points.RemoveAt(points.Count - 1);
            for (var index = points.Count - 1; index > 0; index--)
                if (Same(points[index], points[index - 1])) points.RemoveAt(index);
            if (points.Count < 3 || Math.Abs(SignedArea(points)) <= Epsilon)
                throw new InvalidOperationException(errorCode);
            return points.ToArray();
        }

        private static double ResolveHeight(AdministrativeDongDioramaBuilding value,
            double metersPerUnit, out string heightKind)
        {
            double meters;
            if (value.HeightMeters.HasValue && Finite(value.HeightMeters.Value)
                                             && value.HeightMeters.Value > 0d)
            {
                meters = value.HeightMeters.Value;
                heightKind = "SourceHeight";
            }
            else if (value.AboveGroundFloorCount.HasValue
                     && value.AboveGroundFloorCount.Value > 0)
            {
                meters = value.AboveGroundFloorCount.Value * 3d;
                heightKind = "FloorCountPresentationEstimate";
            }
            else
            {
                meters = 4d;
                heightKind = "SymbolicFallback4m";
            }
            return Math.Max(2d / metersPerUnit, Math.Min(300d / metersPerUnit,
                meters / metersPerUnit));
        }

        private static bool ReviewReadiness(string value)
            => string.Equals(value,
                   AdministrativeDongDioramaReadinessCodes.WaitingForAdministrativeBoundary,
                   StringComparison.Ordinal)
               || string.Equals(value,
                   AdministrativeDongDioramaReadinessCodes.WaitingForSpatialLinkage,
                   StringComparison.Ordinal)
               || string.Equals(value,
                   AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                   StringComparison.Ordinal);

        private static double SignedArea(IReadOnlyList<행정동디오라마검토Point> points)
        {
            var result = 0d;
            for (var index = 0; index < points.Count; index++)
            {
                var next = (index + 1) % points.Count;
                result += points[index].X * points[next].Z - points[next].X * points[index].Z;
            }
            return result * .5d;
        }

        private static bool Same(행정동디오라마검토Point left,
            행정동디오라마검토Point right)
            => Math.Abs(left.X - right.X) <= Epsilon
               && Math.Abs(left.Z - right.Z) <= Epsilon;

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool Sha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            foreach (var character in value)
                if (!Uri.IsHexDigit(character)) return false;
            return true;
        }
    }
}
