using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 음식배달JourneyWireCodes
    {
        public const string SchemaVersion = "food-delivery-journey-snapshot.v1";
        public const string Ready = "Ready";
        public const string SyntheticFixture = "SyntheticFixture";
        public const string Vehicle = "Vehicle";
        public const string Pedestrian = "Pedestrian";
        public const string Motorcycle = "Motorcycle";
        public const string Pending = "Pending";
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string Blocked = "Blocked";

        public static bool IsKnownMode(string value)
            => value == Vehicle || value == Pedestrian || value == Motorcycle;

        public static bool IsKnownLegState(string value)
            => value == Pending || value == Active || value == Completed || value == Blocked;
    }

    public static class 음식배달JourneyFailureCodes
    {
        public const string ContextInvalid = "FoodDeliveryJourneyContextInvalid";
        public const string SnapshotMissing = "FoodDeliveryJourneySnapshotMissing";
        public const string SchemaMismatch = "FoodDeliveryJourneySchemaMismatch";
        public const string IdentityMissing = "FoodDeliveryJourneyIdentityMissing";
        public const string IdentityInvalid = "FoodDeliveryJourneyIdentityInvalid";
        public const string SourceKindUnsupported = "FoodDeliveryJourneySourceKindUnsupported";
        public const string ObservationTimeInvalid = "FoodDeliveryJourneyObservedAtInvalid";
        public const string DistributionNotApproved = "FoodDeliveryJourneyDistributionNotApproved";
        public const string DistributionForbidden = "FoodDeliveryJourneyDistributionForbidden";
        public const string JourneyBlocked = "FoodDeliveryJourneyBlocked";
        public const string RouteUnresolved = "FoodDeliveryJourneyRouteUnresolved";
        public const string ReadinessNotReady = "FoodDeliveryJourneyReadinessNotReady";
        public const string RegionMismatch = "FoodDeliveryJourneyRegionMismatch";
        public const string AdministrativeAreaMismatch = "FoodDeliveryJourneyAdministrativeAreaMismatch";
        public const string GraphRevisionMismatch = "FoodDeliveryJourneyGraphRevisionMismatch";
        public const string GraphHashInvalid = "FoodDeliveryJourneyGraphHashInvalid";
        public const string GraphHashMismatch = "FoodDeliveryJourneyGraphHashMismatch";
        public const string RouteFingerprintInvalid = "FoodDeliveryJourneyRouteFingerprintInvalid";
        public const string RouteFingerprintMismatch = "FoodDeliveryJourneyRouteFingerprintMismatch";
        public const string RevisionInvalid = "FoodDeliveryJourneyRevisionInvalid";
        public const string StaleRevision = "FoodDeliveryJourneyStaleRevision";
        public const string RevisionConflict = "FoodDeliveryJourneyRevisionConflict";
        public const string IdentityMismatch = "FoodDeliveryJourneyIdentityMismatch";
        public const string LegsMissing = "FoodDeliveryJourneyLegsMissing";
        public const string CurrentLegInvalid = "FoodDeliveryJourneyCurrentLegInvalid";
        public const string LegInvalid = "FoodDeliveryJourneyLegInvalid";
        public const string LegModeUnknown = "FoodDeliveryJourneyLegModeUnknown";
        public const string LegStateUnknown = "FoodDeliveryJourneyLegStateUnknown";
        public const string LegProgressInvalid = "FoodDeliveryJourneyLegProgressInvalid";
        public const string LegPointsInvalid = "FoodDeliveryJourneyLegPointsInvalid";
        public const string LegConnectionInvalid = "FoodDeliveryJourneyLegConnectionInvalid";
        public const string ActorPositionInvalid = "FoodDeliveryJourneyActorPositionInvalid";
        public const string VehiclePositionInvalid = "FoodDeliveryJourneyVehiclePositionInvalid";
    }

    [Serializable]
    public sealed class 음식배달JourneySnapshotData
    {
        public string schemaVersion = string.Empty;
        public string regionStableId = string.Empty;
        public string administrativeAreaStableId = string.Empty;
        public string sourceKindCode = string.Empty;
        public string orderStableId = string.Empty;
        public string actorStableId = string.Empty;
        public string vehicleStableId = string.Empty;
        public string routeStableId = string.Empty;
        public string graphRevision = string.Empty;
        public string graphHash = string.Empty;
        public string routeFingerprint = string.Empty;
        public long revision;
        public string observedAt = string.Empty;
        public bool blocked;
        public bool distributionApproved;
        public string readinessCode = string.Empty;
        public int currentLegIndex;
        public 음식배달JourneyLegData[] legs = Array.Empty<음식배달JourneyLegData>();
    }

    [Serializable]
    public sealed class 음식배달JourneyLegData
    {
        public string stableId = string.Empty;
        public string mode = string.Empty;
        public string stateCode = string.Empty;
        public double distanceMeters;
        public double progressMeters;
        public 음식배달JourneyPointData[] points = Array.Empty<음식배달JourneyPointData>();
        public 음식배달JourneyPointData actorPosition = new();
        public 음식배달JourneyPointData vehiclePosition;
    }

    [Serializable]
    public sealed class 음식배달JourneyPointData
    {
        public double x;
        public double z;
    }

    public readonly struct 음식배달JourneyPoint : IEquatable<음식배달JourneyPoint>
    {
        public 음식배달JourneyPoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }

        public double DistanceTo(음식배달JourneyPoint other)
        {
            var dx = other.X - X;
            var dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Equals(음식배달JourneyPoint other)
            => X.Equals(other.X) && Z.Equals(other.Z);

        public override bool Equals(object value)
            => value is 음식배달JourneyPoint other && Equals(other);

        public override int GetHashCode()
            => unchecked(X.GetHashCode() * 397 ^ Z.GetHashCode());
    }

    public sealed class 음식배달JourneyProjectionContext
    {
        public 음식배달JourneyProjectionContext(
            string regionStableId,
            string administrativeAreaStableId,
            string graphRevision,
            string graphHash,
            bool allowPrivatePreview = false)
        {
            RegionStableId = regionStableId?.Trim() ?? string.Empty;
            AdministrativeAreaStableId = administrativeAreaStableId?.Trim() ?? string.Empty;
            GraphRevision = graphRevision?.Trim() ?? string.Empty;
            GraphHash = graphHash?.Trim() ?? string.Empty;
            AllowPrivatePreview = allowPrivatePreview;
        }

        public string RegionStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string GraphRevision { get; }
        public string GraphHash { get; }
        public bool AllowPrivatePreview { get; }

        public bool Validate()
            => 음식배달JourneyInterpreter.IsRegionStableId(RegionStableId)
               && 음식배달JourneyInterpreter.IsAdministrativeAreaStableId(
                   AdministrativeAreaStableId)
               && !string.IsNullOrWhiteSpace(GraphRevision)
               && 음식배달JourneyFingerprint.IsSha256(GraphHash);
    }

    public sealed class 음식배달JourneyPresentationModel
    {
        public string RegionStableId { get; internal set; } = string.Empty;
        public string AdministrativeAreaStableId { get; internal set; } = string.Empty;
        public string SourceKindCode { get; internal set; } = string.Empty;
        public string OrderStableId { get; internal set; } = string.Empty;
        public string ActorStableId { get; internal set; } = string.Empty;
        public string VehicleStableId { get; internal set; } = string.Empty;
        public string RouteStableId { get; internal set; } = string.Empty;
        public string GraphRevision { get; internal set; } = string.Empty;
        public string GraphHash { get; internal set; } = string.Empty;
        public string RouteFingerprint { get; internal set; } = string.Empty;
        public long Revision { get; internal set; }
        public DateTimeOffset ObservedAt { get; internal set; }
        public bool DistributionApproved { get; internal set; }
        public int CurrentLegIndex { get; internal set; }
        public string CurrentLegStableId { get; internal set; } = string.Empty;
        public string CurrentModeCode { get; internal set; } = string.Empty;
        public string CurrentStateCode { get; internal set; } = string.Empty;
        public double CurrentLegDistanceMeters { get; internal set; }
        public double CurrentLegProgressMeters { get; internal set; }
        public 음식배달JourneyPoint CurrentPosition { get; internal set; }
        public 음식배달JourneyPoint CurrentDirection { get; internal set; }
        public 음식배달JourneyPoint ObservedActorPosition { get; internal set; }
        public bool HasVehiclePosition { get; internal set; }
        public 음식배달JourneyPoint VehiclePosition { get; internal set; }
        public IReadOnlyList<음식배달JourneyPoint> RoutePoints { get; internal set; }
            = Array.Empty<음식배달JourneyPoint>();
    }

    /// <summary>
    /// 서버가 확정한 polyline 진행률을 Unity 표시 좌표로 해석한다.
    /// actorPosition 두 점 사이를 직선 보간하거나 도착 Command를 만들지 않는다.
    /// </summary>
    public sealed class 음식배달JourneyInterpreter
    {
        public const double LegConnectionToleranceMeters = 2d;

        private readonly Dictionary<string, AcceptedState> acceptedStates =
            new(StringComparer.Ordinal);
        private long acceptedRevision = -1;

        public long AcceptedRevision => acceptedRevision;

        public bool TryInterpret(
            음식배달JourneySnapshotData snapshot,
            음식배달JourneyProjectionContext context,
            out 음식배달JourneyPresentationModel model,
            out string failureCode)
        {
            model = null;
            failureCode = string.Empty;
            if (context == null || !context.Validate())
                return Fail(음식배달JourneyFailureCodes.ContextInvalid, out failureCode);
            if (snapshot == null)
                return Fail(음식배달JourneyFailureCodes.SnapshotMissing, out failureCode);
            if (snapshot.schemaVersion != 음식배달JourneyWireCodes.SchemaVersion)
                return Fail(음식배달JourneyFailureCodes.SchemaMismatch, out failureCode);
            if (Missing(snapshot.regionStableId, snapshot.administrativeAreaStableId,
                    snapshot.sourceKindCode,
                    snapshot.orderStableId,
                    snapshot.actorStableId, snapshot.vehicleStableId, snapshot.routeStableId,
                    snapshot.graphRevision, snapshot.graphHash, snapshot.routeFingerprint,
                    snapshot.observedAt, snapshot.readinessCode))
            {
                return Fail(음식배달JourneyFailureCodes.IdentityMissing, out failureCode);
            }
            if (snapshot.sourceKindCode != 음식배달JourneyWireCodes.SyntheticFixture)
                return Fail(음식배달JourneyFailureCodes.SourceKindUnsupported,
                    out failureCode);
            if (snapshot.distributionApproved)
                return Fail(음식배달JourneyFailureCodes.DistributionForbidden,
                    out failureCode);
            if (!context.AllowPrivatePreview)
                return Fail(음식배달JourneyFailureCodes.DistributionNotApproved, out failureCode);
            // 서버-valid RouteUnresolved/Blocked도 이동 자료로 승격하지 않는다.
            // 마지막 좌표를 목적지까지 직선 연결하지 않도록 readiness를 먼저 분류한다.
            if (snapshot.readinessCode != 음식배달JourneyWireCodes.Ready)
            {
                return Fail(snapshot.readinessCode.IndexOf("Unresolved", StringComparison.OrdinalIgnoreCase) >= 0
                    ? 음식배달JourneyFailureCodes.RouteUnresolved
                    : 음식배달JourneyFailureCodes.ReadinessNotReady, out failureCode);
            }
            if (snapshot.blocked)
                return Fail(음식배달JourneyFailureCodes.JourneyBlocked, out failureCode);
            if (!string.Equals(snapshot.regionStableId,
                    context.RegionStableId, StringComparison.Ordinal))
            {
                return Fail(음식배달JourneyFailureCodes.RegionMismatch, out failureCode);
            }
            if (!string.Equals(snapshot.administrativeAreaStableId,
                    context.AdministrativeAreaStableId, StringComparison.Ordinal))
            {
                return Fail(음식배달JourneyFailureCodes.AdministrativeAreaMismatch,
                    out failureCode);
            }
            if (!IsRegionStableId(snapshot.regionStableId)
                || !IsAdministrativeAreaStableId(snapshot.administrativeAreaStableId)
                || !IsSyntheticStableId(snapshot.orderStableId)
                || !IsSyntheticStableId(snapshot.actorStableId)
                || !IsSyntheticStableId(snapshot.vehicleStableId)
                || !IsSyntheticStableId(snapshot.routeStableId))
            {
                return Fail(음식배달JourneyFailureCodes.IdentityInvalid,
                    out failureCode);
            }
            if (!string.Equals(snapshot.graphRevision, context.GraphRevision,
                    StringComparison.Ordinal))
            {
                return Fail(음식배달JourneyFailureCodes.GraphRevisionMismatch,
                    out failureCode);
            }
            if (!음식배달JourneyFingerprint.IsSha256(snapshot.graphHash))
                return Fail(음식배달JourneyFailureCodes.GraphHashInvalid, out failureCode);
            if (!string.Equals(snapshot.graphHash, context.GraphHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Fail(음식배달JourneyFailureCodes.GraphHashMismatch, out failureCode);
            }
            if (snapshot.revision <= 0)
                return Fail(음식배달JourneyFailureCodes.RevisionInvalid, out failureCode);
            if (!HasExplicitUtcOffset(snapshot.observedAt)
                || !DateTimeOffset.TryParse(snapshot.observedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var observedAt) || observedAt.Offset != TimeSpan.Zero)
            {
                return Fail(음식배달JourneyFailureCodes.ObservationTimeInvalid,
                    out failureCode);
            }
            if (snapshot.legs == null || snapshot.legs.Length == 0)
                return Fail(음식배달JourneyFailureCodes.LegsMissing, out failureCode);
            if (snapshot.currentLegIndex < 0 || snapshot.currentLegIndex >= snapshot.legs.Length)
                return Fail(음식배달JourneyFailureCodes.CurrentLegInvalid, out failureCode);

            var legIds = new HashSet<string>(StringComparer.Ordinal);
            음식배달JourneyPoint? previousEnd = null;
            for (var legIndex = 0; legIndex < snapshot.legs.Length; legIndex++)
            {
                var leg = snapshot.legs[legIndex];
                if (leg == null || string.IsNullOrWhiteSpace(leg.stableId)
                    || !IsSyntheticStableId(leg.stableId)
                    || string.IsNullOrWhiteSpace(leg.stateCode) || !legIds.Add(leg.stableId))
                {
                    return Fail(음식배달JourneyFailureCodes.LegInvalid, out failureCode);
                }
                if (!음식배달JourneyWireCodes.IsKnownMode(leg.mode))
                    return Fail(음식배달JourneyFailureCodes.LegModeUnknown, out failureCode);
                if (!음식배달JourneyWireCodes.IsKnownLegState(leg.stateCode))
                    return Fail(음식배달JourneyFailureCodes.LegStateUnknown, out failureCode);
                if (legIndex < snapshot.currentLegIndex
                    && (leg.stateCode != 음식배달JourneyWireCodes.Completed
                        || leg.progressMeters != leg.distanceMeters))
                {
                    return Fail(음식배달JourneyFailureCodes.LegProgressInvalid,
                        out failureCode);
                }
                if (legIndex > snapshot.currentLegIndex
                    && (leg.stateCode != 음식배달JourneyWireCodes.Pending
                        || leg.progressMeters != 0d))
                {
                    return Fail(음식배달JourneyFailureCodes.LegProgressInvalid,
                        out failureCode);
                }
                if (legIndex == snapshot.currentLegIndex
                    && leg.stateCode != 음식배달JourneyWireCodes.Active
                    && leg.stateCode != 음식배달JourneyWireCodes.Completed)
                {
                    return Fail(음식배달JourneyFailureCodes.LegStateUnknown,
                        out failureCode);
                }
                if (!Finite(leg.distanceMeters) || leg.distanceMeters <= 0d
                    || !Finite(leg.progressMeters) || leg.progressMeters < 0d
                    || leg.progressMeters > leg.distanceMeters)
                {
                    return Fail(음식배달JourneyFailureCodes.LegProgressInvalid,
                        out failureCode);
                }
                if (!ValidatePoints(leg.points, out var first, out var last,
                        out var geometryLength)
                    || Math.Abs(geometryLength - leg.distanceMeters) > .000001d)
                    return Fail(음식배달JourneyFailureCodes.LegPointsInvalid, out failureCode);
                if (previousEnd.HasValue
                    && previousEnd.Value.DistanceTo(first) > LegConnectionToleranceMeters)
                {
                    return Fail(음식배달JourneyFailureCodes.LegConnectionInvalid,
                        out failureCode);
                }
                previousEnd = last;
                if (!ValidPoint(leg.actorPosition))
                    return Fail(음식배달JourneyFailureCodes.ActorPositionInvalid,
                        out failureCode);
                if (leg.vehiclePosition != null && !ValidPoint(leg.vehiclePosition))
                    return Fail(음식배달JourneyFailureCodes.VehiclePositionInvalid,
                        out failureCode);
                if (!음식배달JourneyPolyline.TrySample(leg.points, leg.progressMeters,
                        leg.distanceMeters, out var expectedActor, out _)
                    || !SamePosition(Point(leg.actorPosition), expectedActor))
                {
                    return Fail(음식배달JourneyFailureCodes.ActorPositionInvalid,
                        out failureCode);
                }
                if (leg.mode != 음식배달JourneyWireCodes.Pedestrian
                    && (leg.vehiclePosition == null
                        || !SamePosition(Point(leg.vehiclePosition), expectedActor)))
                {
                    return Fail(음식배달JourneyFailureCodes.VehiclePositionInvalid,
                        out failureCode);
                }
            }

            if (!음식배달JourneyFingerprint.IsUpperSha256(snapshot.routeFingerprint))
                return Fail(음식배달JourneyFailureCodes.RouteFingerprintInvalid,
                    out failureCode);
            var computedFingerprint = 음식배달JourneyFingerprint.Compute(snapshot);
            if (!string.Equals(snapshot.routeFingerprint, computedFingerprint,
                    StringComparison.Ordinal))
            {
                return Fail(음식배달JourneyFailureCodes.RouteFingerprintMismatch,
                    out failureCode);
            }

            var journeyKey = JourneyKey(snapshot);
            var stateFingerprint = 음식배달JourneyFingerprint.ComputeState(snapshot, observedAt);
            if (acceptedStates.TryGetValue(journeyKey, out var accepted))
            {
                if (!string.Equals(snapshot.orderStableId, accepted.OrderStableId,
                        StringComparison.Ordinal)
                    || !string.Equals(snapshot.actorStableId, accepted.ActorStableId,
                        StringComparison.Ordinal)
                    || !string.Equals(snapshot.vehicleStableId, accepted.VehicleStableId,
                        StringComparison.Ordinal))
                {
                    return Fail(음식배달JourneyFailureCodes.IdentityMismatch,
                        out failureCode);
                }
                if (snapshot.revision < accepted.Revision)
                    return Fail(음식배달JourneyFailureCodes.StaleRevision, out failureCode);
                if (snapshot.revision == accepted.Revision
                    && !string.Equals(stateFingerprint, accepted.StateFingerprint,
                        StringComparison.Ordinal))
                {
                    return Fail(음식배달JourneyFailureCodes.RevisionConflict,
                        out failureCode);
                }
            }

            var current = snapshot.legs[snapshot.currentLegIndex];
            if (!음식배달JourneyPolyline.TrySample(current.points,
                    current.progressMeters, current.distanceMeters,
                    out var position, out var direction))
            {
                return Fail(음식배달JourneyFailureCodes.LegPointsInvalid, out failureCode);
            }

            var routePoints = Flatten(snapshot.legs);
            model = new 음식배달JourneyPresentationModel
            {
                RegionStableId = snapshot.regionStableId,
                AdministrativeAreaStableId = snapshot.administrativeAreaStableId,
                SourceKindCode = snapshot.sourceKindCode,
                OrderStableId = snapshot.orderStableId,
                ActorStableId = snapshot.actorStableId,
                VehicleStableId = snapshot.vehicleStableId,
                RouteStableId = snapshot.routeStableId,
                GraphRevision = snapshot.graphRevision,
                GraphHash = snapshot.graphHash,
                RouteFingerprint = computedFingerprint,
                Revision = snapshot.revision,
                ObservedAt = observedAt,
                DistributionApproved = snapshot.distributionApproved,
                CurrentLegIndex = snapshot.currentLegIndex,
                CurrentLegStableId = current.stableId,
                CurrentModeCode = current.mode,
                CurrentStateCode = current.stateCode,
                CurrentLegDistanceMeters = current.distanceMeters,
                CurrentLegProgressMeters = current.progressMeters,
                CurrentPosition = position,
                CurrentDirection = direction,
                ObservedActorPosition = Point(current.actorPosition),
                HasVehiclePosition = current.vehiclePosition != null,
                VehiclePosition = current.vehiclePosition == null
                    ? default : Point(current.vehiclePosition),
                RoutePoints = routePoints,
            };

            acceptedStates[journeyKey] = new AcceptedState(snapshot.revision,
                snapshot.orderStableId, snapshot.actorStableId, snapshot.vehicleStableId,
                stateFingerprint);
            acceptedRevision = Math.Max(acceptedRevision, snapshot.revision);
            return true;
        }

        public void Reset()
        {
            acceptedStates.Clear();
            acceptedRevision = -1;
        }

        private static string JourneyKey(음식배달JourneySnapshotData snapshot)
            => snapshot.regionStableId + "\u001f"
               + snapshot.administrativeAreaStableId + "\u001f"
               + snapshot.routeStableId;

        private static bool ValidatePoints(
            IReadOnlyList<음식배달JourneyPointData> points,
            out 음식배달JourneyPoint first,
            out 음식배달JourneyPoint last,
            out double geometryLength)
        {
            first = default;
            last = default;
            geometryLength = 0d;
            if (points == null || points.Count < 2 || points.Any(value => !ValidPoint(value)))
                return false;
            first = Point(points[0]);
            last = Point(points[points.Count - 1]);
            for (var index = 1; index < points.Count; index++)
            {
                var segmentLength = Point(points[index - 1]).DistanceTo(Point(points[index]));
                if (segmentLength <= .001d)
                    return false;
                geometryLength += segmentLength;
            }
            return true;
        }

        private static IReadOnlyList<음식배달JourneyPoint> Flatten(
            IEnumerable<음식배달JourneyLegData> legs)
        {
            var result = new List<음식배달JourneyPoint>();
            foreach (var leg in legs)
                foreach (var value in leg.points)
                {
                    var point = Point(value);
                    if (result.Count == 0 || !result[result.Count - 1].Equals(point))
                        result.Add(point);
                }
            return result.AsReadOnly();
        }

        private static bool Missing(params string[] values)
            => values.Any(string.IsNullOrWhiteSpace);

        private static bool ValidPoint(음식배달JourneyPointData value)
            => value != null && Finite(value.x) && Finite(value.z);

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool HasExplicitUtcOffset(string value)
        {
            var normalized = value?.TrimEnd() ?? string.Empty;
            return normalized.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                   || normalized.EndsWith("+00:00", StringComparison.Ordinal);
        }

        internal static bool IsRegionStableId(string value)
            => value != null && value.StartsWith("world-region:", StringComparison.Ordinal);

        internal static bool IsAdministrativeAreaStableId(string value)
        {
            const string prefix = "region:kr:hjd:";
            return value != null && value.StartsWith(prefix, StringComparison.Ordinal)
                                 && value.Length == prefix.Length + 10
                                 && value.Skip(prefix.Length).All(character =>
                                     character >= '0' && character <= '9');
        }

        private static bool IsSyntheticStableId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.IndexOf(":synthetic", StringComparison.Ordinal) >= 0;

        private static bool SamePosition(
            음식배달JourneyPoint first,
            음식배달JourneyPoint second)
            => Math.Abs(first.X - second.X) <= .000001d
               && Math.Abs(first.Z - second.Z) <= .000001d;

        private static 음식배달JourneyPoint Point(음식배달JourneyPointData value)
            => new(value.x, value.z);

        private static bool Fail(string code, out string failureCode)
        {
            failureCode = code;
            return false;
        }

        private sealed class AcceptedState
        {
            public AcceptedState(long revision, string orderStableId,
                string actorStableId, string vehicleStableId, string stateFingerprint)
            {
                Revision = revision;
                OrderStableId = orderStableId;
                ActorStableId = actorStableId;
                VehicleStableId = vehicleStableId;
                StateFingerprint = stateFingerprint;
            }

            public long Revision { get; }
            public string OrderStableId { get; }
            public string ActorStableId { get; }
            public string VehicleStableId { get; }
            public string StateFingerprint { get; }
        }
    }

    public static class 음식배달JourneyPolyline
    {
        public static bool TrySample(
            IReadOnlyList<음식배달JourneyPointData> points,
            double progressMeters,
            double declaredDistanceMeters,
            out 음식배달JourneyPoint position,
            out 음식배달JourneyPoint direction)
        {
            position = default;
            direction = default;
            if (points == null || points.Count < 2
                || !Finite(progressMeters) || !Finite(declaredDistanceMeters)
                || declaredDistanceMeters <= 0d || progressMeters < 0d
                || progressMeters > declaredDistanceMeters)
            {
                return false;
            }

            var lengths = new double[points.Count - 1];
            var geometryLength = 0d;
            for (var index = 0; index < lengths.Length; index++)
            {
                if (!Valid(points[index]) || !Valid(points[index + 1])) return false;
                var a = Point(points[index]);
                var b = Point(points[index + 1]);
                var length = a.DistanceTo(b);
                if (length <= .001d) return false;
                lengths[index] = length;
                geometryLength += length;
            }
            if (geometryLength <= .001d) return false;

            var target = geometryLength * Math.Min(1d,
                Math.Max(0d, progressMeters / declaredDistanceMeters));
            var traversed = 0d;
            for (var index = 0; index < lengths.Length; index++)
            {
                var length = lengths[index];
                if (target > traversed + length && index < lengths.Length - 1)
                {
                    traversed += length;
                    continue;
                }
                var a = Point(points[index]);
                var b = Point(points[index + 1]);
                var t = Math.Min(1d, Math.Max(0d, (target - traversed) / length));
                position = new 음식배달JourneyPoint(
                    a.X + (b.X - a.X) * t,
                    a.Z + (b.Z - a.Z) * t);
                direction = new 음식배달JourneyPoint(
                    (b.X - a.X) / length,
                    (b.Z - a.Z) / length);
                return true;
            }
            return false;
        }

        private static bool Valid(음식배달JourneyPointData value)
            => value != null && Finite(value.x) && Finite(value.z);

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static 음식배달JourneyPoint Point(음식배달JourneyPointData value)
            => new(value.x, value.z);
    }

    public static class 음식배달JourneyFingerprint
    {
        public static string Compute(음식배달JourneySnapshotData value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
            {
                writer.Write(음식배달JourneyWireCodes.SchemaVersion);
                writer.Write(value.routeStableId ?? string.Empty);
                writer.Write(value.graphRevision ?? string.Empty);
                writer.Write(value.graphHash ?? string.Empty);
                var legs = value.legs ?? Array.Empty<음식배달JourneyLegData>();
                writer.Write(legs.Length);
                foreach (var leg in legs)
                {
                    writer.Write(leg?.stableId ?? string.Empty);
                    writer.Write(leg?.mode ?? string.Empty);
                    writer.Write(leg?.distanceMeters ?? 0d);
                    var points = leg?.points ?? Array.Empty<음식배달JourneyPointData>();
                    writer.Write(points.Length);
                    foreach (var point in points)
                    {
                        writer.Write(point?.x ?? 0d);
                        writer.Write(point?.z ?? 0d);
                    }
                }
                writer.Flush();
            }

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(stream.ToArray());
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        public static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        public static bool IsUpperSha256(string value)
            => IsSha256(value) && string.Equals(value, value.ToUpperInvariant(),
                StringComparison.Ordinal);

        internal static string ComputeState(
            음식배달JourneySnapshotData value,
            DateTimeOffset observedAt)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
            {
                writer.Write(value.schemaVersion ?? string.Empty);
                writer.Write(value.regionStableId ?? string.Empty);
                writer.Write(value.administrativeAreaStableId ?? string.Empty);
                writer.Write(value.orderStableId ?? string.Empty);
                writer.Write(value.actorStableId ?? string.Empty);
                writer.Write(value.vehicleStableId ?? string.Empty);
                writer.Write(value.routeStableId ?? string.Empty);
                writer.Write(value.graphRevision ?? string.Empty);
                writer.Write(value.graphHash ?? string.Empty);
                writer.Write(value.routeFingerprint ?? string.Empty);
                writer.Write(value.revision);
                writer.Write(observedAt.UtcDateTime.Ticks);
                writer.Write(value.distributionApproved);
                writer.Write(value.readinessCode ?? string.Empty);
                writer.Write(value.blocked);
                writer.Write(value.currentLegIndex);
                foreach (var leg in value.legs ?? Array.Empty<음식배달JourneyLegData>())
                {
                    writer.Write(leg?.stableId ?? string.Empty);
                    writer.Write(leg?.mode ?? string.Empty);
                    writer.Write(leg?.stateCode ?? string.Empty);
                    writer.Write(leg?.distanceMeters ?? 0d);
                    writer.Write(leg?.progressMeters ?? 0d);
                    writer.Write(leg?.actorPosition?.x ?? 0d);
                    writer.Write(leg?.actorPosition?.z ?? 0d);
                    writer.Write(leg?.vehiclePosition != null);
                    if (leg?.vehiclePosition != null)
                    {
                        writer.Write(leg.vehiclePosition.x);
                        writer.Write(leg.vehiclePosition.z);
                    }
                }
                writer.Flush();
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray()))
                .Replace("-", string.Empty);
        }

    }
}
