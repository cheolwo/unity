using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    /// <summary>검증된 로컬 파일을 읽는 경계가 제공한다. 위치 승인이나 부분 지도 계약이 아니다.</summary>
    public sealed class 방문검토범위입력
    {
        public string AdministrativeAreaStableId { get; set; }
        public string ProjectionHashSha256 { get; set; }
        public string FocusContentHashSha256 { get; set; }
        public string SourceBundleHashSha256 { get; set; }
        public string CoordinateFrameKey { get; set; }
        public double SourceCenterX { get; set; }
        public double SourceCenterZ { get; set; }
        public double MetersPerUnit { get; set; }
        public double MinX { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxZ { get; set; }
        public int SourceTileCount { get; set; }
        public int SourceBuildingCount { get; set; }
        public int SourceRoadCount { get; set; }
        public string[] SelectedBuildingIds { get; set; } = Array.Empty<string>();
        public string[] SelectedRoadIds { get; set; } = Array.Empty<string>();
        public bool PrivateReviewOnly { get; set; }
        public bool DistributionApproved { get; set; }
        public bool TraversalReady { get; set; }
        public bool GameplayReady { get; set; }
    }

    public readonly struct 방문검토좌표
    {
        public 방문검토좌표(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }
    }

    /// <summary>완전 지도 사본과 작은 참조 범위를 결속한다. UnityEngine·파일 읽기·위치 추정은 없다.</summary>
    public sealed class 방문재생공간검토
    {
        private 방문재생공간검토(행정동디오라마검토Snapshot snapshot, 방문검토범위입력 input)
        {
            Snapshot = snapshot;
            FocusContentHashSha256 = input.FocusContentHashSha256;
            SourceBundleHashSha256 = input.SourceBundleHashSha256;
            CoordinateFrameKey = input.CoordinateFrameKey;
            MinX = input.MinX; MinZ = input.MinZ; MaxX = input.MaxX; MaxZ = input.MaxZ;
            var buildings = new HashSet<string>(input.SelectedBuildingIds, StringComparer.Ordinal);
            var roads = new HashSet<string>(input.SelectedRoadIds, StringComparer.Ordinal);
            SelectedBuildings = Array.AsReadOnly(snapshot.Buildings.Where(v => buildings.Contains(v.BuildingStableId)).ToArray());
            SelectedRoads = Array.AsReadOnly(snapshot.Roads.Where(v => roads.Contains(v.RoadStableId)).ToArray());
        }

        public 행정동디오라마검토Snapshot Snapshot { get; }
        public string FocusContentHashSha256 { get; }
        public string SourceBundleHashSha256 { get; }
        public string CoordinateFrameKey { get; }
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public IReadOnlyList<행정동디오라마검토Building> SelectedBuildings { get; }
        public IReadOnlyList<행정동디오라마검토RoadSegment> SelectedRoads { get; }

        public static 방문재생공간검토 Create(행정동디오라마검토Snapshot snapshot, 방문검토범위입력 input)
        {
            Require(snapshot != null && input != null, "VisitReviewMapRequired");
            Require(input.PrivateReviewOnly && !input.DistributionApproved && !input.TraversalReady
                    && !input.GameplayReady && snapshot.ObservationPresentationOnly
                    && !snapshot.DistributionApproved && !snapshot.TraversalReady && !snapshot.GameplayReady,
                "VisitReviewAuthorityInvalid");
            Require(input.AdministrativeAreaStableId == snapshot.AdministrativeAreaStableId
                    && string.Equals(input.ProjectionHashSha256, snapshot.ProjectionHashSha256, StringComparison.OrdinalIgnoreCase)
                    && Hash(input.FocusContentHashSha256) && Hash(input.SourceBundleHashSha256), "VisitReviewSourceMismatch");
            Require(input.SourceTileCount == snapshot.TileCount && input.SourceBuildingCount == snapshot.Buildings.Count
                    && input.SourceRoadCount == snapshot.Roads.Count, "VisitReviewCompleteSnapshotRequired");
            Require(Finite(input.SourceCenterX) && Finite(input.SourceCenterZ) && input.MetersPerUnit == 1d
                    && snapshot.MetersPerUnit == input.MetersPerUnit
                    && Math.Abs(input.SourceCenterX - snapshot.SourceCenterX) <= .000001d
                    && Math.Abs(input.SourceCenterZ - snapshot.SourceCenterZ) <= .000001d
                    && !string.IsNullOrWhiteSpace(input.CoordinateFrameKey), "VisitReviewCoordinateFrameMismatch");
            Require(Finite(input.MinX) && Finite(input.MinZ) && Finite(input.MaxX) && Finite(input.MaxZ)
                    && input.MaxX > input.MinX && input.MaxZ > input.MinZ
                    && input.MaxX - input.MinX <= 500 && input.MaxZ - input.MinZ <= 500, "VisitReviewBoundsInvalid");
            ValidateIds(input.SelectedBuildingIds, snapshot.Buildings.Select(v => v.BuildingStableId));
            ValidateIds(input.SelectedRoadIds, snapshot.Roads.Select(v => v.RoadStableId));
            return new 방문재생공간검토(snapshot, input);
        }

        public 방문검토좌표 ToLocal(double x, double z)
        {
            Require(Finite(x) && Finite(z), "VisitReviewCoordinateInvalid");
            return new 방문검토좌표((x - Snapshot.SourceCenterX) / Snapshot.MetersPerUnit,
                (z - Snapshot.SourceCenterZ) / Snapshot.MetersPerUnit);
        }

        public string ValidatePrepared(방문재생준비Result prepared)
        {
            if (prepared == null || !prepared.CanReplay || prepared.Visits.Count == 0)
                return "VisitPlaybackPreparationRequired";
            foreach (var visit in prepared.Visits)
            {
                if (visit.AdministrativeAreaStableId != Snapshot.AdministrativeAreaStableId
                    || visit.CoordinateFrameKey != CoordinateFrameKey) return "VisitReviewVisitFrameMismatch";
                if (!Finite(visit.X) || !Finite(visit.Z) || visit.X < MinX || visit.X > MaxX
                    || visit.Z < MinZ || visit.Z > MaxZ) return "VisitReviewVisitOutsideFocus";
                var local = ToLocal(visit.X, visit.Z);
                if (!WithinBoundary(local)) return "VisitReviewVisitOutsideHistoricalMap";
            }
            return string.Empty;
        }

        public static string FrameKey(AdministrativeDongDioramaCoordinateFrame frame)
        {
            Require(frame != null && !string.IsNullOrWhiteSpace(frame.Method) && Finite(frame.OriginLatitude)
                    && Finite(frame.OriginLongitude) && Finite(frame.WorldOffsetX) && Finite(frame.WorldOffsetZ)
                    && frame.MetersPerUnit == 1, "VisitReviewCoordinateFrameMismatch");
            var text = string.Join("|", frame.Method, Number(frame.OriginLatitude), Number(frame.OriginLongitude),
                Number(frame.WorldOffsetX), Number(frame.WorldOffsetZ), Number(frame.MetersPerUnit));
            using var sha = SHA256.Create();
            return "administrative-dong-enu:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }

        private bool WithinBoundary(방문검토좌표 p)
        {
            var result = false;
            for (var i = 0; i < Snapshot.Boundary.Count; i++)
            {
                var a = Snapshot.Boundary[i]; var b = Snapshot.Boundary[(i + 1) % Snapshot.Boundary.Count];
                var dx = b.X - a.X; var dz = b.Z - a.Z; var length = dx * dx + dz * dz;
                var t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / length));
                if (Math.Abs(p.X - a.X - t * dx) <= .000001 && Math.Abs(p.Z - a.Z - t * dz) <= .000001) return true;
                if ((a.Z > p.Z) != (b.Z > p.Z) && p.X < (b.X - a.X) * (p.Z - a.Z) / (b.Z - a.Z) + a.X)
                    result = !result;
            }
            return result;
        }

        private static void ValidateIds(string[] selected, IEnumerable<string> all)
        {
            Require(selected != null && selected.Length > 0 && selected.All(v => !string.IsNullOrWhiteSpace(v))
                    && selected.Distinct(StringComparer.Ordinal).Count() == selected.Length, "VisitReviewSelectionIdsInvalid");
            var source = new HashSet<string>(all, StringComparer.Ordinal);
            Require(selected.All(source.Contains), "VisitReviewSelectedIdMissing");
        }
        private static bool Hash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
