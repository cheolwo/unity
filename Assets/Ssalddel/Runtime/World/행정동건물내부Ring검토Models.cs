using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>동결 원본의 누락된 내부 경계를 한 건물의 비공개 표현에만 복원하는 입력.</summary>
    [DataContract]
    public sealed class 행정동건물내부Ring검토Data
    {
        [DataMember(IsRequired = true)] public string schemaVersion;
        [DataMember(IsRequired = true)] public string revision;
        [DataMember(IsRequired = true)] public string contentHashSha256;
        [DataMember(IsRequired = true)] public string administrativeAreaStableId;
        [DataMember(IsRequired = true)] public string buildingStableId;
        [DataMember(IsRequired = true)] public string baseProjectionHashSha256;
        [DataMember(IsRequired = true)] public string baseBundleSha256;
        [DataMember(IsRequired = true)] public string baseGenerationHashSha256;
        [DataMember(IsRequired = true)] public string sourceArchiveSha256;
        [DataMember(IsRequired = true)] public string sourceGeometrySha256;
        [DataMember(IsRequired = true)] public int sourceRecordOrdinal;
        [DataMember(IsRequired = true)] public string sourceVintage;
        [DataMember(IsRequired = true)] public string sourceCrs;
        [DataMember(IsRequired = true)] public string sourceRightsStatus;
        [DataMember(IsRequired = true)] public string coordinateUnit;
        [DataMember(IsRequired = true)] public 행정동건물내부Ring검토FrameData coordinateFrame;
        [DataMember(IsRequired = true)] public double[][] exteriorRingMm;
        [DataMember(IsRequired = true)] public double[][][] interiorRingsMm;
        [DataMember(IsRequired = true)] public double[][] roofVerticesMm;
        [DataMember(IsRequired = true)] public int[] roofTriangleIndices;
        [DataMember(IsRequired = true)] public bool privateReviewVisualizationAllowed;
        [DataMember(IsRequired = true)] public bool sourcePolygonStructureObserved;
        [DataMember(IsRequired = true)] public bool currentBuildingEstablished;
        [DataMember(IsRequired = true)] public bool currentBoundaryEstablished;
        [DataMember(IsRequired = true)] public bool databaseWriteAllowed;
        [DataMember(IsRequired = true)] public bool mongoWriteAllowed;
        [DataMember(IsRequired = true)] public bool passageAuthorized;
        [DataMember(IsRequired = true)] public bool buildingHeightPrecisionImproved;
        [DataMember(IsRequired = true)] public bool sourceCandidateUnityApplyAllowed;
        [DataMember(IsRequired = true)] public bool publicDisplayAllowed;
        [DataMember(IsRequired = true)] public bool distributionApproved;
        [DataMember(IsRequired = true)] public bool runtimeAuthorized;
        [DataMember(IsRequired = true)] public bool traversalReady;
        [DataMember(IsRequired = true)] public bool gameplayReady;
        [DataMember(IsRequired = true)] public bool colliderAuthorized;
        [DataMember(IsRequired = true)] public bool entranceMeaningAuthorized;
        [DataMember(IsRequired = true)] public bool courtyardMeaningAuthorized;
    }

    [DataContract]
    public sealed class 행정동건물내부Ring검토FrameData
    {
        [DataMember(IsRequired = true)] public string method;
        [DataMember(IsRequired = true)] public double originLatitude;
        [DataMember(IsRequired = true)] public double originLongitude;
        [DataMember(IsRequired = true)] public double worldOffsetX;
        [DataMember(IsRequired = true)] public double worldOffsetZ;
        [DataMember(IsRequired = true)] public double metersPerUnit;
    }

    /// <summary>Core와 원본 Snapshot을 바꾸지 않는 단일 대상 형상 사본. 높이는 기존 값을 사용한다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "동결 원본 내부 경계와 단일 건물의 비공개 표현 계약·삼각분할·기존 도형 결속을 확인한다.",
        Boundary = "원본 Snapshot·Core·높이·현재성·공개·통행 권위를 변경하지 않는다.")]
    public sealed class 행정동건물내부Ring검토Geometry
    {
        public const string Schema = "administrative-dong-focused-building-ring-review.v1";
        public const string TargetArea = "region:kr:hjd:1126057500";
        public const string TargetBuilding = "vworld:al-d010:0000208470354536004400000000";
        public const string SourceArchiveHash = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755";
        private const double Epsilon = 1e-8;
        private readonly double displayedHeight;

        private 행정동건물내부Ring검토Geometry(행정동건물내부Ring검토Data data,
            행정동디오라마검토Snapshot snapshot)
        {
            Require(data.schemaVersion == Schema && !string.IsNullOrWhiteSpace(data.revision), "SchemaInvalid");
            Require(data.administrativeAreaStableId == TargetArea && data.buildingStableId == TargetBuilding,
                "TargetInvalid");
            Require(data.privateReviewVisualizationAllowed && data.sourcePolygonStructureObserved
                && !data.currentBuildingEstablished && !data.currentBoundaryEstablished
                && !data.databaseWriteAllowed && !data.mongoWriteAllowed && !data.passageAuthorized
                && !data.buildingHeightPrecisionImproved && !data.sourceCandidateUnityApplyAllowed
                && !data.publicDisplayAllowed && !data.distributionApproved
                && !data.runtimeAuthorized && !data.traversalReady && !data.gameplayReady
                && !data.colliderAuthorized && !data.entranceMeaningAuthorized && !data.courtyardMeaningAuthorized,
                "AuthorityInvalid");
            Require(string.Equals(data.sourceArchiveSha256, SourceArchiveHash, StringComparison.OrdinalIgnoreCase)
                && Hash(data.sourceGeometrySha256) && Hash(data.contentHashSha256)
                && Hash(data.baseBundleSha256) && Hash(data.baseGenerationHashSha256)
                && data.sourceRecordOrdinal >= 0 && !string.IsNullOrWhiteSpace(data.sourceVintage)
                && data.sourceCrs == "EPSG:5186" && data.sourceRightsStatus == "RightsConflictUnresolved",
                "SourceBindingInvalid");
            var frame = data.coordinateFrame;
            Require(data.coordinateUnit == "CommonEnuMillimeters" && frame != null
                && frame.method == "WGS84-ECEF-ENU-at-zero-altitude"
                && frame.originLatitude == 37.580912 && frame.originLongitude == 127.088502
                && frame.worldOffsetX == 0d && frame.worldOffsetZ == 0d && frame.metersPerUnit == 1d,
                "CoordinateFrameInvalid");
            Require(snapshot != null && snapshot.AdministrativeAreaStableId == data.administrativeAreaStableId
                && snapshot.ObservationPresentationOnly && !snapshot.DistributionApproved
                && !snapshot.TraversalReady && !snapshot.GameplayReady
                && string.Equals(snapshot.ProjectionHashSha256, data.baseProjectionHashSha256,
                    StringComparison.OrdinalIgnoreCase), "ProjectionBindingInvalid");
            Require(snapshot.TryGetBuilding(data.buildingStableId, out var building), "BuildingMissing");
            Require(building.DisplayedHeight == 4d && building.DisplayedHeightKind == "SymbolicFallback4m",
                "SymbolicHeightBindingInvalid");
            BuildingStableId = data.buildingStableId;
            AdministrativeAreaStableId = data.administrativeAreaStableId;
            BaseProjectionHashSha256 = data.baseProjectionHashSha256;
            ContentHashSha256 = data.contentHashSha256;
            displayedHeight = building.DisplayedHeight;
            ExteriorRing = Ring(data.exteriorRingMm, snapshot);
            Require(SameRing(ExteriorRing, building.Footprint), "ExteriorBindingMismatch");
            Require(data.interiorRingsMm != null && data.interiorRingsMm.Length == 1, "InteriorCountInvalid");
            InteriorRings = Array.AsReadOnly(data.interiorRingsMm.Select(r => Ring(r, snapshot)).ToArray());
            foreach (var ring in InteriorRings)
            {
                Require(Math.Sign(Area(ExteriorRing)) != Math.Sign(Area(ring)), "RingWindingInvalid");
                foreach (var point in ring)
                    Require(Location(ExteriorRing, point.X, point.Z) == 1, "InteriorOutsideExterior");
                Require(!Crosses(ring, ExteriorRing), "InteriorCrossesExterior");
            }
            RoofVertices = Array.AsReadOnly(Points(data.roofVerticesMm, snapshot));
            Require(RoofVertices.Count <= 256 && RoofVertices.Count >= 3, "RoofVertexBudgetInvalid");
            var sourcePoints = ExteriorRing.Concat(InteriorRings.SelectMany(r => r)).ToArray();
            Require(RoofVertices.All(p => sourcePoints.Any(q => SamePoint(p, q)))
                && RoofVertices.Select(p => (p.X, p.Z)).Distinct().Count() == RoofVertices.Count,
                "RoofVertexBindingInvalid");
            Require(data.roofTriangleIndices != null && data.roofTriangleIndices.Length > 0
                && data.roofTriangleIndices.Length <= 2048 && data.roofTriangleIndices.Length % 3 == 0,
                "RoofIndexBudgetInvalid");
            RoofTriangleIndices = Array.AsReadOnly((int[])data.roofTriangleIndices.Clone());
            InteriorArea = InteriorRings.Sum(r => Math.Abs(Area(r)));
            RoofArea = Math.Abs(Area(ExteriorRing)) - InteriorArea;
            ValidateRoof();
        }

        public string BuildingStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string BaseProjectionHashSha256 { get; }
        public string ContentHashSha256 { get; }
        public IReadOnlyList<행정동디오라마검토Point> ExteriorRing { get; }
        public IReadOnlyList<IReadOnlyList<행정동디오라마검토Point>> InteriorRings { get; }
        public IReadOnlyList<행정동디오라마검토Point> RoofVertices { get; }
        public IReadOnlyList<int> RoofTriangleIndices { get; }
        public double RoofArea { get; }
        public double InteriorArea { get; }

        public static bool TryCreate(행정동건물내부Ring검토Data data,
            행정동디오라마검토Snapshot snapshot, out 행정동건물내부Ring검토Geometry geometry,
            out string diagnostic)
        {
            geometry = null;
            try
            {
                Require(data != null, "InputMissing");
                geometry = new 행정동건물내부Ring검토Geometry(data, snapshot);
                diagnostic = "FocusedBuildingRingPrivateReviewReady";
                return true;
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            { diagnostic = error.Message; return false; }
        }

        public void ValidateBinding(행정동디오라마검토Snapshot snapshot)
        {
            Require(snapshot != null && snapshot.AdministrativeAreaStableId == AdministrativeAreaStableId
                && string.Equals(snapshot.ProjectionHashSha256, BaseProjectionHashSha256,
                    StringComparison.OrdinalIgnoreCase)
                && snapshot.ObservationPresentationOnly && !snapshot.DistributionApproved
                && !snapshot.TraversalReady && !snapshot.GameplayReady, "ProjectionBindingInvalid");
            Require(snapshot.TryGetBuilding(BuildingStableId, out var building)
                && building.DisplayedHeight == displayedHeight
                && building.DisplayedHeightKind == "SymbolicFallback4m"
                && SameRing(ExteriorRing, building.Footprint), "BuildingBindingChanged");
        }

        public bool ContainsRoof(double x, double z, bool includeBoundary = true)
        {
            var outer = Location(ExteriorRing, x, z);
            if (outer < 0 || (outer == 0 && !includeBoundary)) return false;
            foreach (var ring in InteriorRings)
            {
                var inner = Location(ring, x, z);
                if (inner > 0 || (inner == 0 && !includeBoundary)) return false;
            }
            return true;
        }

        private void ValidateRoof()
        {
            var triangles = new List<행정동디오라마검토Point[]>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var area = 0d;
            for (var index = 0; index < RoofTriangleIndices.Count; index += 3)
            {
                var ids = RoofTriangleIndices.Skip(index).Take(3).ToArray();
                Require(ids.All(i => i >= 0 && i < RoofVertices.Count) && ids.Distinct().Count() == 3,
                    "RoofIndexInvalid");
                Require(keys.Add(string.Join(",", ids.OrderBy(i => i))), "RoofTriangleDuplicate");
                var triangle = ids.Select(i => RoofVertices[i]).ToArray();
                var cross = Cross(triangle[0], triangle[1], triangle[2]);
                Require(cross < -Epsilon, "RoofWindingOrDegenerateInvalid");
                area += -cross * .5d;
                Require(ContainsRoof(triangle.Average(p => p.X), triangle.Average(p => p.Z), false),
                    "RoofTriangleOutsideSurface");
                for (var edge = 0; edge < 3; edge++)
                {
                    var a = triangle[edge]; var b = triangle[(edge + 1) % 3];
                    Require(ContainsRoof((a.X + b.X) * .5d, (a.Z + b.Z) * .5d)
                        && !Crosses(new[] { a, b }, ExteriorRing, false)
                        && !InteriorRings.Any(r => Crosses(new[] { a, b }, r, false)),
                        "RoofEdgeCrossesBoundary");
                }
                Require(!InteriorRings.SelectMany(r => r).Any(p => StrictTriangle(triangle, p)),
                    "RoofTriangleCoversInterior");
                foreach (var previous in triangles)
                    Require(!Crosses(previous, triangle)
                        && !previous.Any(p => StrictTriangle(triangle, p))
                        && !triangle.Any(p => StrictTriangle(previous, p)), "RoofTrianglesOverlap");
                triangles.Add(triangle);
            }
            Require(RoofArea > Epsilon && Math.Abs(area - RoofArea) <= Math.Max(1e-6, RoofArea * 1e-9),
                "RoofCoverageAreaMismatch");
        }

        private static IReadOnlyList<행정동디오라마검토Point> Ring(double[][] values,
            행정동디오라마검토Snapshot snapshot)
        {
            var points = Points(values, snapshot);
            Require(points.Length >= 4 && points.Length <= 256
                && SamePoint(points[0], points[points.Length - 1]), "RingClosureInvalid");
            var ring = points.Take(points.Length - 1).ToArray();
            Require(ring.Select(p => (p.X, p.Z)).Distinct().Count() == ring.Length
                && Math.Abs(Area(ring)) > Epsilon, "RingDegenerateInvalid");
            for (var i = 0; i < ring.Length; i++)
            for (var j = i + 1; j < ring.Length; j++)
            {
                if (j == i + 1 || (i == 0 && j == ring.Length - 1)) continue;
                Require(!IntersectsOrTouches(ring[i], ring[(i + 1) % ring.Length], ring[j],
                    ring[(j + 1) % ring.Length]), "RingSelfIntersection");
            }
            return Array.AsReadOnly(ring);
        }

        private static 행정동디오라마검토Point[] Points(double[][] values,
            행정동디오라마검토Snapshot snapshot)
        {
            Require(values != null && values.Length <= 256, "PointsMissingOrBudgetInvalid");
            return values.Select(p =>
            {
                Require(p != null && p.Length == 2 && p.All(v => !double.IsNaN(v)
                    && !double.IsInfinity(v) && Math.Abs(v) <= 2e9 && v == Math.Truncate(v)),
                    "PointMillimeterInvalid");
                return new 행정동디오라마검토Point((p[0] / 1000d - snapshot.SourceCenterX)
                    / snapshot.MetersPerUnit, (p[1] / 1000d - snapshot.SourceCenterZ) / snapshot.MetersPerUnit);
            }).ToArray();
        }

        private static bool SameRing(IReadOnlyList<행정동디오라마검토Point> a,
            IReadOnlyList<행정동디오라마검토Point> b)
        {
            if (a.Count != b.Count) return false;
            for (var start = 0; start < b.Count; start++)
            foreach (var step in new[] { 1, -1 })
                if (Enumerable.Range(0, a.Count).All(i => SamePoint(a[i], b[(start + i * step + b.Count) % b.Count])))
                    return true;
            return false;
        }

        private static int Location(IReadOnlyList<행정동디오라마검토Point> ring, double x, double z)
        {
            var p = new 행정동디오라마검토Point(x, z); var inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                var a = ring[j]; var b = ring[i];
                if (Math.Abs(Cross(a, b, p)) <= Epsilon && x >= Math.Min(a.X, b.X) - Epsilon
                    && x <= Math.Max(a.X, b.X) + Epsilon && z >= Math.Min(a.Z, b.Z) - Epsilon
                    && z <= Math.Max(a.Z, b.Z) + Epsilon) return 0;
                if ((a.Z > z) != (b.Z > z) && x < (b.X - a.X) * (z - a.Z) / (b.Z - a.Z) + a.X)
                    inside = !inside;
            }
            return inside ? 1 : -1;
        }

        private static double Area(IReadOnlyList<행정동디오라마검토Point> ring)
        {
            var origin = ring[0]; var area = 0d;
            for (var i = 1; i < ring.Count - 1; i++) area += Cross(origin, ring[i], ring[i + 1]);
            return area * .5d;
        }

        private static double Cross(행정동디오라마검토Point a, 행정동디오라마검토Point b,
            행정동디오라마검토Point c) => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

        private static bool ProperCross(행정동디오라마검토Point a, 행정동디오라마검토Point b,
            행정동디오라마검토Point c, 행정동디오라마검토Point d)
            => Opposite(Cross(a, b, c), Cross(a, b, d))
                && Opposite(Cross(c, d, a), Cross(c, d, b));

        private static bool Opposite(double a, double b)
            => (a > Epsilon && b < -Epsilon) || (a < -Epsilon && b > Epsilon);

        private static bool IntersectsOrTouches(행정동디오라마검토Point a, 행정동디오라마검토Point b,
            행정동디오라마검토Point c, 행정동디오라마검토Point d)
            => ProperCross(a, b, c, d) || OnSegment(a, b, c) || OnSegment(a, b, d)
                || OnSegment(c, d, a) || OnSegment(c, d, b);

        private static bool OnSegment(행정동디오라마검토Point a, 행정동디오라마검토Point b,
            행정동디오라마검토Point p)
            => Math.Abs(Cross(a, b, p)) <= Epsilon
                && p.X >= Math.Min(a.X, b.X) - Epsilon && p.X <= Math.Max(a.X, b.X) + Epsilon
                && p.Z >= Math.Min(a.Z, b.Z) - Epsilon && p.Z <= Math.Max(a.Z, b.Z) + Epsilon;

        private static bool Crosses(IReadOnlyList<행정동디오라마검토Point> a,
            IReadOnlyList<행정동디오라마검토Point> b, bool closedA = true)
        {
            for (var i = 0; i < a.Count - (closedA ? 0 : 1); i++)
            for (var j = 0; j < b.Count; j++)
                if (ProperCross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
            return false;
        }

        private static bool StrictTriangle(IReadOnlyList<행정동디오라마검토Point> triangle,
            행정동디오라마검토Point p)
        {
            var a = Cross(triangle[0], triangle[1], p);
            var b = Cross(triangle[1], triangle[2], p);
            var c = Cross(triangle[2], triangle[0], p);
            return (a > Epsilon && b > Epsilon && c > Epsilon)
                || (a < -Epsilon && b < -Epsilon && c < -Epsilon);
        }

        private static bool SamePoint(행정동디오라마검토Point a, 행정동디오라마검토Point b)
            => Math.Abs(a.X - b.X) <= Epsilon && Math.Abs(a.Z - b.Z) <= Epsilon;
        private static bool Hash(string value) => value != null && value.Length == 64
            && value.All(c => Uri.IsHexDigit(c));
        private static void Require(bool valid, string code)
        { if (!valid) throw new InvalidOperationException("FocusedBuildingRing" + code); }
    }
}
