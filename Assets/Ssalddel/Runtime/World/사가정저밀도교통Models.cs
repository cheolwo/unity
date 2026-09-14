using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 사가정저밀도교통WireCodes
    {
        public const string SchemaVersion = "station-synthetic-traffic-snapshot.v2";
        public const string SyntheticFixture = "SyntheticFixture";
        public const string Ready = "ReadyForPrivateSyntheticDeliveryObservation";
        public const string SyntheticLaneDisplayOnly = "SyntheticLaneDisplayOnly";
        public const string ManualVisualReviewCandidate = "ManualVisualReviewCandidate";

        public const string NorthSouth = "NorthSouth";
        public const string EastWest = "EastWest";
        public const string NeighborhoodUncontrolled = "NeighborhoodUncontrolled";
        public const string AlleyUncontrolled = "AlleyUncontrolled";
        public const string Green = "Green";
        public const string Amber = "Amber";
        public const string Red = "Red";
        public const string NorthSouthGreen = "NorthSouthGreen";
        public const string NorthSouthAmber = "NorthSouthAmber";
        public const string AllRedBeforeEastWest = "AllRedBeforeEastWest";
        public const string EastWestGreen = "EastWestGreen";
        public const string EastWestAmber = "EastWestAmber";
        public const string AllRedBeforeNorthSouth = "AllRedBeforeNorthSouth";

        public const string CourierMotorcycle = "CourierMotorcycle";
        public const string AmbientPassengerCar = "AmbientPassengerCar";
        public const string Moving = "Moving";
        public const string MovingOnNeighborhoodRoad = "MovingOnNeighborhoodRoad";
        public const string MovingInAlley = "MovingInAlley";
        public const string WaitingAtSignal = "WaitingAtSignal";
        public const string Queueing = "Queueing";
        public const string WaitingForAlley = "WaitingForAlley";
        public const string StoppedAtCandidateEndpoint = "StoppedAtCandidateEndpoint";

        public const string AsDefined = "AsDefined";
        public const string ReverseDefined = "ReverseDefined";
        public const string AsStored = "AsStored";
        public const string ReverseStored = "ReverseStored";
        public const string UnknownDirection = "Unknown";
        public const string PendingHumanReview = "PendingHumanReview";
        public const string DeclaredSyntheticReferenceOnly =
            "DeclaredSyntheticReferenceOnly";

        // 이전 Editor 도구의 컴파일 호환 이름. v2 의미는 OSM 통행 방향이 아니다.
        public const string Forward = AsDefined;
        public const string Reverse = ReverseDefined;
        public const string SyntheticSignalGate = "SyntheticSignalGate";
        public const string MinimumGap = "MinimumGap";

        public const string MainRoadApproach = "MainRoadApproach";
        public const string NeighborhoodRoadApproach = "NeighborhoodRoadApproach";
        public const string AlleyFinalApproach = "AlleyFinalApproach";
        public const string CandidateEndpointReached = "CandidateEndpointReached";

        public const string EastWestLane =
            "lane:synthetic-display:sagajeong:ew-eastbound.r1";
        public const string NorthSouthLane =
            "lane:synthetic-display:sagajeong:ns-northbound.r1";
        public const string NeighborhoodAccessLane =
            "lane:manual-visual-candidate:sagajeong:delivery-access.r2";
        public const string AlleyCandidateLane =
            "lane:manual-visual-candidate:osm:way:1256772587.r1";

        public const string MainRoadRouteLeg =
            "route-leg:synthetic:sagajeong:main-road.r2";
        public const string NeighborhoodRoadRouteLeg =
            "route-leg:synthetic:sagajeong:neighborhood-road.r2";
        public const string AlleyFinalRouteLeg =
            "route-leg:synthetic:sagajeong:alley-final.r2";

        public static bool IsSignalState(string value)
            => value == Green || value == Amber || value == Red;

        public static bool IsActorKind(string value)
            => value == CourierMotorcycle || value == AmbientPassengerCar;

        public static bool IsActorState(string value)
            => value == Moving || value == MovingOnNeighborhoodRoad
               || value == MovingInAlley || value == WaitingAtSignal
               || value == Queueing || value == WaitingForAlley
               || value == StoppedAtCandidateEndpoint;
    }

    public static class 사가정저밀도교통FailureCodes
    {
        public const string ContextInvalid = "StationSyntheticTrafficContextInvalid";
        public const string SnapshotMissing = "StationSyntheticTrafficSnapshotMissing";
        public const string SchemaMismatch = "StationSyntheticTrafficSchemaMismatch";
        public const string IdentityMissing = "StationSyntheticTrafficIdentityMissing";
        public const string IdentityInvalid = "StationSyntheticTrafficIdentityInvalid";
        public const string StationMismatch = "StationSyntheticTrafficStationMismatch";
        public const string RegionMismatch = "StationSyntheticTrafficRegionMismatch";
        public const string AdministrativeAreaMismatch =
            "StationSyntheticTrafficAdministrativeAreaMismatch";
        public const string SourceKindUnsupported =
            "StationSyntheticTrafficSourceKindUnsupported";
        public const string SourceObservationRevisionMismatch =
            "StationSyntheticTrafficSourceObservationRevisionMismatch";
        public const string SourceMobilityGraphMismatch =
            "StationSyntheticTrafficSourceMobilityGraphMismatch";
        public const string SourceAuthorityBoundaryInvalid =
            "StationSyntheticTrafficSourceAuthorityBoundaryInvalid";
        public const string ObservationBoundaryInvalid =
            "StationSyntheticTrafficObservationBoundaryInvalid";
        public const string DistributionForbidden =
            "StationSyntheticTrafficDistributionForbidden";
        public const string PrivatePreviewRequired =
            "StationSyntheticTrafficPrivatePreviewRequired";
        public const string TraversalMustRemainUnready =
            "StationSyntheticTrafficTraversalMustRemainUnready";
        public const string GameplayMustRemainUnready =
            "StationSyntheticTrafficGameplayMustRemainUnready";
        public const string OperationalBindingForbidden =
            "StationSyntheticTrafficOperationalBindingForbidden";
        public const string ReadinessMismatch = "StationSyntheticTrafficReadinessMismatch";
        public const string LaneGraphRevisionMismatch =
            "StationSyntheticTrafficLaneGraphRevisionMismatch";
        public const string LaneGraphHashInvalid =
            "StationSyntheticTrafficLaneGraphHashInvalid";
        public const string LaneGraphHashMismatch =
            "StationSyntheticTrafficLaneGraphHashMismatch";
        public const string LaneStateInvalid =
            "StationSyntheticTrafficLaneStateInvalid";
        public const string SignalPlanRevisionMismatch =
            "StationSyntheticTrafficSignalPlanRevisionMismatch";
        public const string TrafficProfileRevisionMismatch =
            "StationSyntheticTrafficProfileRevisionMismatch";
        public const string CourierJourneyMismatch =
            "StationSyntheticTrafficCourierJourneyMismatch";
        public const string RouteFingerprintInvalid =
            "StationSyntheticTrafficRouteFingerprintInvalid";
        public const string RouteFingerprintMismatch =
            "StationSyntheticTrafficRouteFingerprintMismatch";
        public const string CourierDisplayRouteRevisionMismatch =
            "StationSyntheticTrafficCourierDisplayRouteRevisionMismatch";
        public const string CourierDisplayRouteFingerprintInvalid =
            "StationSyntheticTrafficCourierDisplayRouteFingerprintInvalid";
        public const string CourierDisplayRouteFingerprintMismatch =
            "StationSyntheticTrafficCourierDisplayRouteFingerprintMismatch";
        public const string CourierRouteLegInvalid =
            "StationSyntheticTrafficCourierRouteLegInvalid";
        public const string CourierRouteStateInvalid =
            "StationSyntheticTrafficCourierRouteStateInvalid";
        public const string CourierActorRouteStateMismatch =
            "StationSyntheticTrafficCourierActorRouteStateMismatch";
        public const string CourierRouteProgressRegression =
            "StationSyntheticTrafficCourierRouteProgressRegression";
        public const string ConfigurationFingerprintInvalid =
            "StationSyntheticTrafficConfigurationFingerprintInvalid";
        public const string ConfigurationFingerprintMismatch =
            "StationSyntheticTrafficConfigurationFingerprintMismatch";
        public const string InputStateFingerprintInvalid =
            "StationSyntheticTrafficInputStateFingerprintInvalid";
        public const string InputStateFingerprintMismatch =
            "StationSyntheticTrafficInputStateFingerprintMismatch";
        public const string ScenarioConfigurationMismatch =
            "StationSyntheticTrafficScenarioConfigurationMismatch";
        public const string ObservationTimeInvalid =
            "StationSyntheticTrafficObservedAtInvalid";
        public const string RevisionInvalid = "StationSyntheticTrafficRevisionInvalid";
        public const string StaleRevision = "StationSyntheticTrafficStaleRevision";
        public const string RevisionConflict = "StationSyntheticTrafficRevisionConflict";
        public const string SignalStateInvalid = "StationSyntheticTrafficSignalStateInvalid";
        public const string ActorStateInvalid = "StationSyntheticTrafficActorStateInvalid";
    }

    [Serializable]
    public sealed class 사가정저밀도교통SnapshotData
    {
        public string schemaVersion = string.Empty;
        public string stationStableId = string.Empty;
        public string regionStableId = string.Empty;
        public string administrativeAreaStableId = string.Empty;
        public string sourceKindCode = string.Empty;
        public string sourceObservationRevision = string.Empty;
        public string sourceMobilityGraphStableId = string.Empty;
        public string sourceMobilityGraphRevision = string.Empty;
        public string sourceMobilityGraphProjectionHashSha256 = string.Empty;
        public string sourceMobilityGraphContentHashSha256 = string.Empty;
        public string sourceAccessReviewCode = string.Empty;
        public string sourceDirectionAuthorityCode = string.Empty;
        public bool sourceRuntimeAuthorized;
        public string laneGraphRevision = string.Empty;
        public string laneGraphHash = string.Empty;
        public string signalPlanRevision = string.Empty;
        public string trafficProfileRevision = string.Empty;
        public string courierJourneyId = string.Empty;
        public string routeFingerprint = string.Empty;
        public string courierDisplayRouteRevision = string.Empty;
        public string courierDisplayRouteFingerprint = string.Empty;
        public string configurationFingerprint = string.Empty;
        public string inputStateFingerprint = string.Empty;
        public int scenarioSeed;
        public long simulationTick;
        public double tickSeconds;
        public long revision;
        public string observedAt = string.Empty;
        public bool observationPresentationOnly;
        public bool distributionApproved;
        public bool traversalReady;
        public bool gameplayReady;
        public string journeyBindingStateCode = string.Empty;
        public bool operationalBindingReady;
        public bool canonicalDeliveryMutationAllowed;
        public string readinessCode = string.Empty;
        public 사가정저밀도교통SignalData[] signals =
            Array.Empty<사가정저밀도교통SignalData>();
        public 사가정저밀도교통LaneData[] lanes =
            Array.Empty<사가정저밀도교통LaneData>();
        public 사가정저밀도교통ActorData[] actors =
            Array.Empty<사가정저밀도교통ActorData>();
        public 사가정저밀도교통CourierRouteLegData[] courierRouteLegs =
            Array.Empty<사가정저밀도교통CourierRouteLegData>();
        public 사가정저밀도교통CourierRouteStateData courierRouteState = new();
    }

    [Serializable]
    public sealed class 사가정저밀도교통SignalData
    {
        public string stableId = string.Empty;
        public string axisCode = string.Empty;
        public string stateCode = string.Empty;
        public double secondsUntilTransition;
        public string stopLineStableId = string.Empty;
    }

    [Serializable]
    public sealed class 사가정저밀도교통LaneData
    {
        public string stableId = string.Empty;
        public string sourceWayStableId = string.Empty;
        public string axisCode = string.Empty;
        public string routeKindCode = string.Empty;
        public string sourceDirectionCode = string.Empty;
        public string sourceAccessReviewCode = string.Empty;
        public bool sourceRuntimeAuthorized;
        public bool hasStopLine;
        public double stopLineProgressMeters;
        public bool pingPong;
        public 사가정저밀도교통PointData[] points =
            Array.Empty<사가정저밀도교통PointData>();
    }

    [Serializable]
    public sealed class 사가정저밀도교통ActorData
    {
        public string stableId = string.Empty;
        public string kindCode = string.Empty;
        public string laneStableId = string.Empty;
        public string routeKindCode = string.Empty;
        public string stateCode = string.Empty;
        public string displayPolylineOrderCode = string.Empty;
        public double progressMeters;
        public double speedMetersPerSecond;
        public int queueIndex = -1;
        public string blockedReasonCode = string.Empty;
        public 사가정저밀도교통PointData position = new();
        public double headingDegrees;

        // 이전 Editor 도구의 소스 호환용이다. JSON/계약 필드로 사용하지 않는다.
        public string travelDirectionCode
        {
            get => displayPolylineOrderCode;
            set => displayPolylineOrderCode = value;
        }
    }

    [Serializable]
    public sealed class 사가정저밀도교통SourceEdgeData
    {
        public string edgeStableId = string.Empty;
        public string displayGeometryOrderCode = string.Empty;
        public string sourceDirectionCode = string.Empty;
        public string accessReviewCode = string.Empty;
        public bool runtimeAuthorized;
        public double sourceDistanceMeters;
        public 사가정저밀도교통PointData displayFrom = new();
        public 사가정저밀도교통PointData displayTo = new();
    }

    [Serializable]
    public sealed class 사가정저밀도교통CourierRouteLegData
    {
        public string stableId = string.Empty;
        public int index;
        public string laneStableId = string.Empty;
        public string stageCode = string.Empty;
        public double displayGeometryDistanceMeters;
        public double sourceDistanceMeters;
        public 사가정저밀도교통SourceEdgeData[] sourceEdges =
            Array.Empty<사가정저밀도교통SourceEdgeData>();
    }

    [Serializable]
    public sealed class 사가정저밀도교통CourierRouteStateData
    {
        public string currentRouteLegStableId = string.Empty;
        public int currentRouteLegIndex;
        public int routeLegCount;
        public string currentLaneStableId = string.Empty;
        public string stageCode = string.Empty;
        public double legProgressMeters;
        public double cumulativeProgressMeters;
        public double totalDistanceMeters;
        public bool candidateEndpointReached;
    }

    [Serializable]
    public sealed class 사가정저밀도교통PointData
    {
        public double x;
        public double z;
    }

    public readonly struct 사가정저밀도교통Point : IEquatable<사가정저밀도교통Point>
    {
        public 사가정저밀도교통Point(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }

        public double DistanceTo(사가정저밀도교통Point other)
        {
            var dx = other.X - X;
            var dz = other.Z - Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Equals(사가정저밀도교통Point other)
            => X.Equals(other.X) && Z.Equals(other.Z);

        public override bool Equals(object value)
            => value is 사가정저밀도교통Point other && Equals(other);

        public override int GetHashCode()
            => unchecked(X.GetHashCode() * 397 ^ Z.GetHashCode());
    }

    public static class 사가정저밀도교통Fixture
    {
        public const string StationStableId = "station:kr:kric:s1107:0722";
        public const string RegionStableId =
            "world-region:kr:seoul:jungnang:sagajeong.r1";
        public const string AdministrativeAreaStableId = "region:kr:hjd:1126057500";
        public const string SourceObservationRevision =
            "sagajeong-reference.r3+oa-23081-20260824";
        public const string SourceMobilityGraphStableId =
            "mobility-graph:kr:seoul:jungnang:sagajeong.r1";
        public const string SourceMobilityGraphRevision =
            "sagajeong-mobility-graph.osm-candidate.r1";
        public const string SourceMobilityGraphProjectionHashSha256 =
            "59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7";
        public const string SourceMobilityGraphContentHashSha256 =
            "CD3E8FBD6428F7499F9E313E1AD3CFC04688EFAEF370E6C109F2321D70B11768";
        public const string LaneGraphRevision =
            "sagajeong-low-density-lane-display.r2";
        public const string SignalPlanRevision =
            "sagajeong-synthetic-signal-cycle.r1";
        public const string TrafficProfileRevision = "sagajeong-low-density-traffic.r2";
        public const string CourierJourneyId =
            "route:synthetic:sagajeong-food-delivery:a";
        public const string RouteFingerprint =
            "720664785E1CE71ECB7D492ED5903514FA5D1A6237D91896187C99385146B88A";
        public const string CourierDisplayRouteRevision =
            "sagajeong-courier-display-route.osm-candidate.r2";
        public const string LaneGraphHash =
            "FF3BA30BF64B2D20C88A41EC529A6DE1655AD73B3E3CEE5673F6A7CDACA815BE";
        public const string CourierDisplayRouteFingerprint =
            "EAB858B640F90E4EF2E1832B9D3C3D22B096B2F202D8A3AC04D03FEAB8DC7475";
        public const string ConfigurationFingerprint =
            "6F0B43A2E2E3E44490B135FC785AFF55C67EC49E1D4F94D9103B7378AAA3F4BE";
        public const string InputStateFingerprint =
            "E93FEF991675D4706D52432724B4BE5BC23AFE1CC2616FC5DD4DF8923BBB1EE4";

        public const string CourierActorId = "actor:synthetic-courier:sagajeong:1";
        public const string AmbientActor1 = "actor:synthetic-ambient-car:sagajeong:1";
        public const string AmbientActor2 = "actor:synthetic-ambient-car:sagajeong:2";
        public const string AmbientActor3 = "actor:synthetic-ambient-car:sagajeong:3";
        public const int ScenarioSeed = 20260913;
        public const double TickSeconds = .25d;
        public const double SignalCycleSeconds = 28d;
        public const double MainSpeedMetersPerSecond = 6d;
        public const double NeighborhoodSpeedMetersPerSecond = 4d;
        public const double AlleySpeedMetersPerSecond = 2.5d;
        public const double StopBufferMeters = 1.5d;
        public const double MinimumGapMeters = 6d;
        public const double TotalDisplayRouteDistanceMeters = 277.277387734062d;
        public const int MaximumActorCount = 4;
        public const int ExpectedInitialActorCount = 4;
        public const int ExpectedLaneCount = 4;
        public const int ExpectedRouteLegCount = 3;
        public const long MaximumSimulationTick = 100000L;
        public const bool AlleyTraversalApproved = false;

        public static readonly DateTimeOffset EpochUtc =
            new(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);

        // 아직 수정하지 않는 v1 Editor 캡처 도구의 컴파일 호환용이다.
        // v2 Interpreter와 Playback은 이 좌표를 읽지 않고 수신 Lanes만 사용한다.
        public static readonly 사가정저밀도교통Point Intersection =
            new(554.576d, 3.594d);
        public static readonly IReadOnlyList<사가정저밀도교통Point> AlleyCenterline =
            Array.AsReadOnly(new[]
            {
                new 사가정저밀도교통Point(668.194d, 86.381d),
                new 사가정저밀도교통Point(646.429d, 89.222d),
                new 사가정저밀도교통Point(645.961d, 89.211d),
                new 사가정저밀도교통Point(645.625d, 89.089d),
            });

        public static double Length(IReadOnlyList<사가정저밀도교통Point> points)
        {
            if (points == null || points.Count < 2) return 0d;
            var result = 0d;
            for (var index = 1; index < points.Count; index++)
                result += points[index - 1].DistanceTo(points[index]);
            return result;
        }

        public static bool TrySample(IReadOnlyList<사가정저밀도교통Point> points,
            double progressMeters, out 사가정저밀도교통Point point,
            out 사가정저밀도교통Point direction)
        {
            point = default;
            direction = default;
            if (points == null || points.Count < 2 || !Finite(progressMeters)) return false;
            var remaining = Math.Max(0d, Math.Min(Length(points), progressMeters));
            for (var index = 1; index < points.Count; index++)
            {
                var from = points[index - 1];
                var to = points[index];
                var dx = to.X - from.X;
                var dz = to.Z - from.Z;
                var length = Math.Sqrt(dx * dx + dz * dz);
                if (length <= 0d) return false;
                if (remaining <= length || index == points.Count - 1)
                {
                    var ratio = Math.Min(1d, remaining / length);
                    point = new 사가정저밀도교통Point(
                        from.X + dx * ratio, from.Z + dz * ratio);
                    direction = new 사가정저밀도교통Point(dx / length, dz / length);
                    return true;
                }
                remaining -= length;
            }
            return false;
        }

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class 사가정저밀도교통ProjectionContext
    {
        public 사가정저밀도교통ProjectionContext(
            string stationStableId,
            string regionStableId,
            string administrativeAreaStableId,
            string sourceObservationRevision,
            string laneGraphRevision,
            string laneGraphHash,
            string signalPlanRevision,
            string trafficProfileRevision,
            string courierJourneyId,
            string routeFingerprint,
            string configurationFingerprint,
            string inputStateFingerprint,
            int scenarioSeed,
            double tickSeconds,
            DateTimeOffset epochUtc,
            bool allowPrivatePreview = false)
            : this(stationStableId, regionStableId, administrativeAreaStableId,
                sourceObservationRevision,
                사가정저밀도교통Fixture.SourceMobilityGraphStableId,
                사가정저밀도교통Fixture.SourceMobilityGraphRevision,
                사가정저밀도교통Fixture.SourceMobilityGraphProjectionHashSha256,
                사가정저밀도교통Fixture.SourceMobilityGraphContentHashSha256,
                laneGraphRevision, laneGraphHash, signalPlanRevision,
                trafficProfileRevision, courierJourneyId, routeFingerprint,
                사가정저밀도교통Fixture.CourierDisplayRouteRevision,
                사가정저밀도교통Fixture.CourierDisplayRouteFingerprint,
                configurationFingerprint, inputStateFingerprint, scenarioSeed,
                tickSeconds, epochUtc, allowPrivatePreview)
        {
        }

        public 사가정저밀도교통ProjectionContext(
            string stationStableId,
            string regionStableId,
            string administrativeAreaStableId,
            string sourceObservationRevision,
            string sourceMobilityGraphStableId,
            string sourceMobilityGraphRevision,
            string sourceMobilityGraphProjectionHashSha256,
            string sourceMobilityGraphContentHashSha256,
            string laneGraphRevision,
            string laneGraphHash,
            string signalPlanRevision,
            string trafficProfileRevision,
            string courierJourneyId,
            string routeFingerprint,
            string courierDisplayRouteRevision,
            string courierDisplayRouteFingerprint,
            string configurationFingerprint,
            string inputStateFingerprint,
            int scenarioSeed,
            double tickSeconds,
            DateTimeOffset epochUtc,
            bool allowPrivatePreview = false)
        {
            StationStableId = stationStableId?.Trim() ?? string.Empty;
            RegionStableId = regionStableId?.Trim() ?? string.Empty;
            AdministrativeAreaStableId = administrativeAreaStableId?.Trim() ?? string.Empty;
            SourceObservationRevision = sourceObservationRevision?.Trim() ?? string.Empty;
            SourceMobilityGraphStableId = sourceMobilityGraphStableId?.Trim() ?? string.Empty;
            SourceMobilityGraphRevision = sourceMobilityGraphRevision?.Trim() ?? string.Empty;
            SourceMobilityGraphProjectionHashSha256 =
                sourceMobilityGraphProjectionHashSha256?.Trim() ?? string.Empty;
            SourceMobilityGraphContentHashSha256 =
                sourceMobilityGraphContentHashSha256?.Trim() ?? string.Empty;
            LaneGraphRevision = laneGraphRevision?.Trim() ?? string.Empty;
            LaneGraphHash = laneGraphHash?.Trim() ?? string.Empty;
            SignalPlanRevision = signalPlanRevision?.Trim() ?? string.Empty;
            TrafficProfileRevision = trafficProfileRevision?.Trim() ?? string.Empty;
            CourierJourneyId = courierJourneyId?.Trim() ?? string.Empty;
            RouteFingerprint = routeFingerprint?.Trim() ?? string.Empty;
            CourierDisplayRouteRevision = courierDisplayRouteRevision?.Trim() ?? string.Empty;
            CourierDisplayRouteFingerprint =
                courierDisplayRouteFingerprint?.Trim() ?? string.Empty;
            ConfigurationFingerprint = configurationFingerprint?.Trim() ?? string.Empty;
            InputStateFingerprint = inputStateFingerprint?.Trim() ?? string.Empty;
            ScenarioSeed = scenarioSeed;
            TickSeconds = tickSeconds;
            EpochUtc = epochUtc;
            AllowPrivatePreview = allowPrivatePreview;
        }

        public string StationStableId { get; }
        public string RegionStableId { get; }
        public string AdministrativeAreaStableId { get; }
        public string SourceObservationRevision { get; }
        public string SourceMobilityGraphStableId { get; }
        public string SourceMobilityGraphRevision { get; }
        public string SourceMobilityGraphProjectionHashSha256 { get; }
        public string SourceMobilityGraphContentHashSha256 { get; }
        public string LaneGraphRevision { get; }
        public string LaneGraphHash { get; }
        public string SignalPlanRevision { get; }
        public string TrafficProfileRevision { get; }
        public string CourierJourneyId { get; }
        public string RouteFingerprint { get; }
        public string CourierDisplayRouteRevision { get; }
        public string CourierDisplayRouteFingerprint { get; }
        public string ConfigurationFingerprint { get; }
        public string InputStateFingerprint { get; }
        public int ScenarioSeed { get; }
        public double TickSeconds { get; }
        public DateTimeOffset EpochUtc { get; }
        public bool AllowPrivatePreview { get; }

        public bool Validate()
            => StationStableId == 사가정저밀도교통Fixture.StationStableId
               && RegionStableId == 사가정저밀도교통Fixture.RegionStableId
               && AdministrativeAreaStableId ==
               사가정저밀도교통Fixture.AdministrativeAreaStableId
               && SourceObservationRevision ==
               사가정저밀도교통Fixture.SourceObservationRevision
               && SourceMobilityGraphStableId ==
               사가정저밀도교통Fixture.SourceMobilityGraphStableId
               && SourceMobilityGraphRevision ==
               사가정저밀도교통Fixture.SourceMobilityGraphRevision
               && SourceMobilityGraphProjectionHashSha256 ==
               사가정저밀도교통Fixture.SourceMobilityGraphProjectionHashSha256
               && SourceMobilityGraphContentHashSha256 ==
               사가정저밀도교통Fixture.SourceMobilityGraphContentHashSha256
               && LaneGraphRevision == 사가정저밀도교통Fixture.LaneGraphRevision
               && LaneGraphHash == 사가정저밀도교통Fixture.LaneGraphHash
               && SignalPlanRevision == 사가정저밀도교통Fixture.SignalPlanRevision
               && TrafficProfileRevision ==
               사가정저밀도교통Fixture.TrafficProfileRevision
               && CourierJourneyId == 사가정저밀도교통Fixture.CourierJourneyId
               && RouteFingerprint == 사가정저밀도교통Fixture.RouteFingerprint
               && CourierDisplayRouteRevision ==
               사가정저밀도교통Fixture.CourierDisplayRouteRevision
               && CourierDisplayRouteFingerprint ==
               사가정저밀도교통Fixture.CourierDisplayRouteFingerprint
               && ConfigurationFingerprint ==
               사가정저밀도교통Fixture.ConfigurationFingerprint
               && InputStateFingerprint ==
               사가정저밀도교통Fixture.InputStateFingerprint
               && ScenarioSeed == 사가정저밀도교통Fixture.ScenarioSeed
               && Same(TickSeconds, 사가정저밀도교통Fixture.TickSeconds)
               && EpochUtc == 사가정저밀도교통Fixture.EpochUtc;

        private static bool Same(double first, double second)
            => Math.Abs(first - second) <= .000001d;
    }

    public sealed class 사가정저밀도교통PresentationModel
    {
        public string StationStableId { get; internal set; } = string.Empty;
        public string RegionStableId { get; internal set; } = string.Empty;
        public string AdministrativeAreaStableId { get; internal set; } = string.Empty;
        public string SourceObservationRevision { get; internal set; } = string.Empty;
        public string SourceMobilityGraphStableId { get; internal set; } = string.Empty;
        public string SourceMobilityGraphRevision { get; internal set; } = string.Empty;
        public string SourceMobilityGraphProjectionHashSha256 { get; internal set; }
            = string.Empty;
        public string SourceMobilityGraphContentHashSha256 { get; internal set; }
            = string.Empty;
        public string SourceAccessReviewCode { get; internal set; } = string.Empty;
        public string SourceDirectionAuthorityCode { get; internal set; } = string.Empty;
        public bool SourceRuntimeAuthorized { get; internal set; }
        public string LaneGraphRevision { get; internal set; } = string.Empty;
        public string LaneGraphHash { get; internal set; } = string.Empty;
        public string SignalPlanRevision { get; internal set; } = string.Empty;
        public string TrafficProfileRevision { get; internal set; } = string.Empty;
        public string CourierJourneyId { get; internal set; } = string.Empty;
        public string RouteFingerprint { get; internal set; } = string.Empty;
        public string CourierDisplayRouteRevision { get; internal set; } = string.Empty;
        public string CourierDisplayRouteFingerprint { get; internal set; } = string.Empty;
        public string ConfigurationFingerprint { get; internal set; } = string.Empty;
        public string InputStateFingerprint { get; internal set; } = string.Empty;
        public string JourneyBindingStateCode { get; internal set; } = string.Empty;
        public bool OperationalBindingReady { get; internal set; }
        public bool CanonicalDeliveryMutationAllowed { get; internal set; }
        public long Revision { get; internal set; }
        public long SimulationTick { get; internal set; }
        public DateTimeOffset ObservedAt { get; internal set; }
        public string SignalPhaseCode { get; internal set; } = string.Empty;
        public IReadOnlyList<사가정저밀도교통SignalModel> Signals { get; internal set; }
            = Array.Empty<사가정저밀도교통SignalModel>();
        public IReadOnlyList<사가정저밀도교통LaneModel> Lanes { get; internal set; }
            = Array.Empty<사가정저밀도교통LaneModel>();
        public IReadOnlyList<사가정저밀도교통ActorModel> Actors { get; internal set; }
            = Array.Empty<사가정저밀도교통ActorModel>();
        public IReadOnlyList<사가정저밀도교통CourierRouteLegModel> CourierRouteLegs
            { get; internal set; } = Array.Empty<사가정저밀도교통CourierRouteLegModel>();
        public 사가정저밀도교통CourierRouteStateModel CourierRouteState
            { get; internal set; } = new();
    }

    public sealed class 사가정저밀도교통SignalModel
    {
        public string StableId { get; internal set; } = string.Empty;
        public string AxisCode { get; internal set; } = string.Empty;
        public string StateCode { get; internal set; } = string.Empty;
        public double SecondsUntilTransition { get; internal set; }
        public string StopLineStableId { get; internal set; } = string.Empty;
    }

    public sealed class 사가정저밀도교통LaneModel
    {
        public string StableId { get; internal set; } = string.Empty;
        public string SourceWayStableId { get; internal set; } = string.Empty;
        public string AxisCode { get; internal set; } = string.Empty;
        public string RouteKindCode { get; internal set; } = string.Empty;
        public string SourceDirectionCode { get; internal set; } = string.Empty;
        public string SourceAccessReviewCode { get; internal set; } = string.Empty;
        public bool SourceRuntimeAuthorized { get; internal set; }
        public bool HasStopLine { get; internal set; }
        public double StopLineProgressMeters { get; internal set; }
        public bool PingPong { get; internal set; }
        public IReadOnlyList<사가정저밀도교통Point> Points { get; internal set; }
            = Array.Empty<사가정저밀도교통Point>();
        public double LengthMeters { get; internal set; }
    }

    public sealed class 사가정저밀도교통ActorModel
    {
        public string StableId { get; internal set; } = string.Empty;
        public string KindCode { get; internal set; } = string.Empty;
        public string LaneStableId { get; internal set; } = string.Empty;
        public string RouteKindCode { get; internal set; } = string.Empty;
        public string StateCode { get; internal set; } = string.Empty;
        public string DisplayPolylineOrderCode { get; internal set; } = string.Empty;
        public double ProgressMeters { get; internal set; }
        public double SpeedMetersPerSecond { get; internal set; }
        public int QueueIndex { get; internal set; }
        public string BlockedReasonCode { get; internal set; } = string.Empty;
        public 사가정저밀도교통Point Position { get; internal set; }
        public double HeadingDegrees { get; internal set; }
    }

    public sealed class 사가정저밀도교통SourceEdgeModel
    {
        public string EdgeStableId { get; internal set; } = string.Empty;
        public string DisplayGeometryOrderCode { get; internal set; } = string.Empty;
        public string SourceDirectionCode { get; internal set; } = string.Empty;
        public string AccessReviewCode { get; internal set; } = string.Empty;
        public bool RuntimeAuthorized { get; internal set; }
        public double SourceDistanceMeters { get; internal set; }
        public 사가정저밀도교통Point DisplayFrom { get; internal set; }
        public 사가정저밀도교통Point DisplayTo { get; internal set; }
    }

    public sealed class 사가정저밀도교통CourierRouteLegModel
    {
        public string StableId { get; internal set; } = string.Empty;
        public int Index { get; internal set; }
        public string LaneStableId { get; internal set; } = string.Empty;
        public string StageCode { get; internal set; } = string.Empty;
        public double DisplayGeometryDistanceMeters { get; internal set; }
        public double SourceDistanceMeters { get; internal set; }
        public IReadOnlyList<사가정저밀도교통SourceEdgeModel> SourceEdges
            { get; internal set; } = Array.Empty<사가정저밀도교통SourceEdgeModel>();

        public double DistanceMeters => DisplayGeometryDistanceMeters;
    }

    public sealed class 사가정저밀도교통CourierRouteStateModel
    {
        public string CurrentRouteLegStableId { get; internal set; } = string.Empty;
        public int CurrentRouteLegIndex { get; internal set; }
        public int RouteLegCount { get; internal set; }
        public string CurrentLaneStableId { get; internal set; } = string.Empty;
        public string StageCode { get; internal set; } = string.Empty;
        public double LegProgressMeters { get; internal set; }
        public double CumulativeProgressMeters { get; internal set; }
        public double TotalDistanceMeters { get; internal set; }
        public bool CandidateEndpointReached { get; internal set; }
    }

    /// <summary>
    /// 서버 상태 사본의 경계와 수신 lane/route/actor 정합성을 모두 검증한 뒤에만
    /// 읽기 전용 표현 모델을 원자 수용한다. Unity는 통행이나 배송 완료를 판정하지 않는다.
    /// </summary>
    public sealed class 사가정저밀도교통Interpreter
    {
        private readonly Dictionary<string, AcceptedState> accepted =
            new(StringComparer.Ordinal);
        private long acceptedRevision = -1;

        public long AcceptedRevision => acceptedRevision;
        public int AcceptedStationCount => accepted.Count;

        public bool TryInterpret(사가정저밀도교통SnapshotData snapshot,
            사가정저밀도교통ProjectionContext context,
            out 사가정저밀도교통PresentationModel model,
            out string failureCode)
        {
            model = null;
            failureCode = string.Empty;
            if (context == null || !context.Validate())
                return Fail(사가정저밀도교통FailureCodes.ContextInvalid, out failureCode);
            if (snapshot == null)
                return Fail(사가정저밀도교통FailureCodes.SnapshotMissing, out failureCode);
            if (snapshot.schemaVersion != 사가정저밀도교통WireCodes.SchemaVersion)
                return Fail(사가정저밀도교통FailureCodes.SchemaMismatch, out failureCode);
            if (Missing(snapshot.stationStableId, snapshot.regionStableId,
                    snapshot.administrativeAreaStableId, snapshot.sourceKindCode,
                    snapshot.sourceObservationRevision, snapshot.sourceMobilityGraphStableId,
                    snapshot.sourceMobilityGraphRevision,
                    snapshot.sourceMobilityGraphProjectionHashSha256,
                    snapshot.sourceMobilityGraphContentHashSha256,
                    snapshot.sourceAccessReviewCode, snapshot.sourceDirectionAuthorityCode,
                    snapshot.laneGraphRevision, snapshot.laneGraphHash,
                    snapshot.signalPlanRevision, snapshot.trafficProfileRevision,
                    snapshot.courierJourneyId, snapshot.routeFingerprint,
                    snapshot.courierDisplayRouteRevision,
                    snapshot.courierDisplayRouteFingerprint,
                    snapshot.configurationFingerprint, snapshot.inputStateFingerprint,
                    snapshot.observedAt, snapshot.journeyBindingStateCode,
                    snapshot.readinessCode))
                return Fail(사가정저밀도교통FailureCodes.IdentityMissing, out failureCode);
            if (!IsStationStableId(snapshot.stationStableId)
                || !IsRegionStableId(snapshot.regionStableId)
                || !IsAdministrativeAreaStableId(snapshot.administrativeAreaStableId))
                return Fail(사가정저밀도교통FailureCodes.IdentityInvalid, out failureCode);
            if (snapshot.stationStableId != context.StationStableId)
                return Fail(사가정저밀도교통FailureCodes.StationMismatch, out failureCode);
            if (snapshot.regionStableId != context.RegionStableId)
                return Fail(사가정저밀도교통FailureCodes.RegionMismatch, out failureCode);
            if (snapshot.administrativeAreaStableId != context.AdministrativeAreaStableId)
                return Fail(사가정저밀도교통FailureCodes.AdministrativeAreaMismatch,
                    out failureCode);
            if (snapshot.sourceKindCode != 사가정저밀도교통WireCodes.SyntheticFixture)
                return Fail(사가정저밀도교통FailureCodes.SourceKindUnsupported,
                    out failureCode);
            if (snapshot.sourceObservationRevision != context.SourceObservationRevision)
                return Fail(사가정저밀도교통FailureCodes.SourceObservationRevisionMismatch,
                    out failureCode);
            if (snapshot.sourceMobilityGraphStableId !=
                    context.SourceMobilityGraphStableId
                || snapshot.sourceMobilityGraphRevision !=
                    context.SourceMobilityGraphRevision
                || snapshot.sourceMobilityGraphProjectionHashSha256 !=
                    context.SourceMobilityGraphProjectionHashSha256
                || snapshot.sourceMobilityGraphContentHashSha256 !=
                    context.SourceMobilityGraphContentHashSha256
                || !사가정저밀도교통Fingerprint.IsUpperSha256(
                    snapshot.sourceMobilityGraphProjectionHashSha256)
                || !사가정저밀도교통Fingerprint.IsUpperSha256(
                    snapshot.sourceMobilityGraphContentHashSha256))
                return Fail(사가정저밀도교통FailureCodes.SourceMobilityGraphMismatch,
                    out failureCode);
            if (snapshot.sourceAccessReviewCode !=
                    사가정저밀도교통WireCodes.PendingHumanReview
                || snapshot.sourceDirectionAuthorityCode !=
                    사가정저밀도교통WireCodes.UnknownDirection
                || snapshot.sourceRuntimeAuthorized)
                return Fail(사가정저밀도교통FailureCodes.SourceAuthorityBoundaryInvalid,
                    out failureCode);
            if (!snapshot.observationPresentationOnly)
                return Fail(사가정저밀도교통FailureCodes.ObservationBoundaryInvalid,
                    out failureCode);
            if (snapshot.distributionApproved)
                return Fail(사가정저밀도교통FailureCodes.DistributionForbidden,
                    out failureCode);
            if (!context.AllowPrivatePreview)
                return Fail(사가정저밀도교통FailureCodes.PrivatePreviewRequired,
                    out failureCode);
            if (snapshot.traversalReady)
                return Fail(사가정저밀도교통FailureCodes.TraversalMustRemainUnready,
                    out failureCode);
            if (snapshot.gameplayReady)
                return Fail(사가정저밀도교통FailureCodes.GameplayMustRemainUnready,
                    out failureCode);
            if (snapshot.journeyBindingStateCode !=
                    사가정저밀도교통WireCodes.DeclaredSyntheticReferenceOnly
                || snapshot.operationalBindingReady
                || snapshot.canonicalDeliveryMutationAllowed)
                return Fail(사가정저밀도교통FailureCodes.OperationalBindingForbidden,
                    out failureCode);
            if (snapshot.readinessCode != 사가정저밀도교통WireCodes.Ready)
                return Fail(사가정저밀도교통FailureCodes.ReadinessMismatch,
                    out failureCode);
            if (snapshot.laneGraphRevision != context.LaneGraphRevision)
                return Fail(사가정저밀도교통FailureCodes.LaneGraphRevisionMismatch,
                    out failureCode);
            if (!사가정저밀도교통Fingerprint.IsUpperSha256(snapshot.laneGraphHash))
                return Fail(사가정저밀도교통FailureCodes.LaneGraphHashInvalid,
                    out failureCode);
            if (snapshot.laneGraphHash != context.LaneGraphHash)
                return Fail(사가정저밀도교통FailureCodes.LaneGraphHashMismatch,
                    out failureCode);
            if (snapshot.signalPlanRevision != context.SignalPlanRevision)
                return Fail(사가정저밀도교통FailureCodes.SignalPlanRevisionMismatch,
                    out failureCode);
            if (snapshot.trafficProfileRevision != context.TrafficProfileRevision)
                return Fail(사가정저밀도교통FailureCodes.TrafficProfileRevisionMismatch,
                    out failureCode);
            if (snapshot.courierJourneyId != context.CourierJourneyId)
                return Fail(사가정저밀도교통FailureCodes.CourierJourneyMismatch,
                    out failureCode);
            if (!사가정저밀도교통Fingerprint.IsUpperSha256(snapshot.routeFingerprint))
                return Fail(사가정저밀도교통FailureCodes.RouteFingerprintInvalid,
                    out failureCode);
            if (snapshot.routeFingerprint != context.RouteFingerprint)
                return Fail(사가정저밀도교통FailureCodes.RouteFingerprintMismatch,
                    out failureCode);
            if (snapshot.courierDisplayRouteRevision !=
                context.CourierDisplayRouteRevision)
                return Fail(
                    사가정저밀도교통FailureCodes.CourierDisplayRouteRevisionMismatch,
                    out failureCode);
            if (!사가정저밀도교통Fingerprint.IsUpperSha256(
                    snapshot.courierDisplayRouteFingerprint))
                return Fail(
                    사가정저밀도교통FailureCodes.CourierDisplayRouteFingerprintInvalid,
                    out failureCode);
            if (snapshot.courierDisplayRouteFingerprint !=
                context.CourierDisplayRouteFingerprint)
                return Fail(
                    사가정저밀도교통FailureCodes.CourierDisplayRouteFingerprintMismatch,
                    out failureCode);
            if (!사가정저밀도교통Fingerprint.IsUpperSha256(
                    snapshot.configurationFingerprint))
                return Fail(사가정저밀도교통FailureCodes.ConfigurationFingerprintInvalid,
                    out failureCode);
            if (snapshot.configurationFingerprint != context.ConfigurationFingerprint)
                return Fail(사가정저밀도교통FailureCodes.ConfigurationFingerprintMismatch,
                    out failureCode);
            if (!사가정저밀도교통Fingerprint.IsUpperSha256(snapshot.inputStateFingerprint))
                return Fail(사가정저밀도교통FailureCodes.InputStateFingerprintInvalid,
                    out failureCode);
            if (snapshot.inputStateFingerprint != context.InputStateFingerprint)
                return Fail(사가정저밀도교통FailureCodes.InputStateFingerprintMismatch,
                    out failureCode);
            if (snapshot.scenarioSeed != context.ScenarioSeed
                || !Same(snapshot.tickSeconds, context.TickSeconds)
                || snapshot.simulationTick < 0L
                || snapshot.simulationTick > 사가정저밀도교통Fixture.MaximumSimulationTick)
                return Fail(사가정저밀도교통FailureCodes.ScenarioConfigurationMismatch,
                    out failureCode);
            if (snapshot.revision <= 0L || snapshot.revision != snapshot.simulationTick + 1L)
                return Fail(사가정저밀도교통FailureCodes.RevisionInvalid, out failureCode);
            if (!TryUtc(snapshot.observedAt, out var observedAt)
                || observedAt != context.EpochUtc.AddSeconds(
                    snapshot.simulationTick * snapshot.tickSeconds))
                return Fail(사가정저밀도교통FailureCodes.ObservationTimeInvalid,
                    out failureCode);
            if (!ValidateSignals(snapshot, out var signalPhase, out var signalModels))
                return Fail(사가정저밀도교통FailureCodes.SignalStateInvalid,
                    out failureCode);
            if (!ValidateLanes(snapshot, out var lanesById, out var laneModels,
                    out var laneFailure))
                return Fail(laneFailure, out failureCode);
            if (!ValidateRouteLegs(snapshot, lanesById, out var routeLegModels))
                return Fail(사가정저밀도교통FailureCodes.CourierRouteLegInvalid,
                    out failureCode);
            if (!ValidateRouteState(snapshot.courierRouteState, routeLegModels,
                    lanesById, out var routeStateModel))
                return Fail(사가정저밀도교통FailureCodes.CourierRouteStateInvalid,
                    out failureCode);
            if (!ValidateActors(snapshot, lanesById, routeStateModel,
                    out var actorModels, out var actorRouteMismatch))
                return Fail(actorRouteMismatch
                        ? 사가정저밀도교통FailureCodes.CourierActorRouteStateMismatch
                        : 사가정저밀도교통FailureCodes.ActorStateInvalid,
                    out failureCode);

            var key = snapshot.stationStableId + "\u001f" + snapshot.trafficProfileRevision;
            var stateFingerprint = 사가정저밀도교통Fingerprint.ComputeSnapshotState(snapshot);
            if (accepted.TryGetValue(key, out var previous))
            {
                if (snapshot.revision < previous.Revision)
                    return Fail(사가정저밀도교통FailureCodes.StaleRevision,
                        out failureCode);
                if (snapshot.revision == previous.Revision
                    && stateFingerprint != previous.StateFingerprint)
                    return Fail(사가정저밀도교통FailureCodes.RevisionConflict,
                        out failureCode);
                if (snapshot.revision > previous.Revision
                    && (routeStateModel.CurrentRouteLegIndex < previous.RouteLegIndex
                        || routeStateModel.CumulativeProgressMeters + .000001d <
                        previous.CumulativeProgressMeters))
                    return Fail(
                        사가정저밀도교통FailureCodes.CourierRouteProgressRegression,
                        out failureCode);
            }

            model = new 사가정저밀도교통PresentationModel
            {
                StationStableId = snapshot.stationStableId,
                RegionStableId = snapshot.regionStableId,
                AdministrativeAreaStableId = snapshot.administrativeAreaStableId,
                SourceObservationRevision = snapshot.sourceObservationRevision,
                SourceMobilityGraphStableId = snapshot.sourceMobilityGraphStableId,
                SourceMobilityGraphRevision = snapshot.sourceMobilityGraphRevision,
                SourceMobilityGraphProjectionHashSha256 =
                    snapshot.sourceMobilityGraphProjectionHashSha256,
                SourceMobilityGraphContentHashSha256 =
                    snapshot.sourceMobilityGraphContentHashSha256,
                SourceAccessReviewCode = snapshot.sourceAccessReviewCode,
                SourceDirectionAuthorityCode = snapshot.sourceDirectionAuthorityCode,
                SourceRuntimeAuthorized = snapshot.sourceRuntimeAuthorized,
                LaneGraphRevision = snapshot.laneGraphRevision,
                LaneGraphHash = snapshot.laneGraphHash,
                SignalPlanRevision = snapshot.signalPlanRevision,
                TrafficProfileRevision = snapshot.trafficProfileRevision,
                CourierJourneyId = snapshot.courierJourneyId,
                RouteFingerprint = snapshot.routeFingerprint,
                CourierDisplayRouteRevision = snapshot.courierDisplayRouteRevision,
                CourierDisplayRouteFingerprint = snapshot.courierDisplayRouteFingerprint,
                ConfigurationFingerprint = snapshot.configurationFingerprint,
                InputStateFingerprint = snapshot.inputStateFingerprint,
                JourneyBindingStateCode = snapshot.journeyBindingStateCode,
                OperationalBindingReady = snapshot.operationalBindingReady,
                CanonicalDeliveryMutationAllowed = snapshot.canonicalDeliveryMutationAllowed,
                Revision = snapshot.revision,
                SimulationTick = snapshot.simulationTick,
                ObservedAt = observedAt,
                SignalPhaseCode = signalPhase,
                Signals = Array.AsReadOnly(signalModels),
                Lanes = Array.AsReadOnly(laneModels),
                Actors = Array.AsReadOnly(actorModels),
                CourierRouteLegs = Array.AsReadOnly(routeLegModels),
                CourierRouteState = routeStateModel,
            };
            accepted[key] = new AcceptedState(snapshot.revision, stateFingerprint,
                routeStateModel.CurrentRouteLegIndex,
                routeStateModel.CumulativeProgressMeters);
            acceptedRevision = Math.Max(acceptedRevision, snapshot.revision);
            return true;
        }

        public void Reset()
        {
            accepted.Clear();
            acceptedRevision = -1L;
        }

        internal static bool IsStationStableId(string value)
            => value != null && value.StartsWith("station:kr:", StringComparison.Ordinal);

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

        private static bool ValidateSignals(사가정저밀도교통SnapshotData snapshot,
            out string phaseCode, out 사가정저밀도교통SignalModel[] models)
        {
            phaseCode = string.Empty;
            models = Array.Empty<사가정저밀도교통SignalModel>();
            if (snapshot.signals == null || snapshot.signals.Length != 2) return false;
            var cycle = snapshot.simulationTick * snapshot.tickSeconds
                        % 사가정저밀도교통Fixture.SignalCycleSeconds;
            ResolveSignal(cycle, 사가정저밀도교통WireCodes.EastWest,
                out var expectedEastWest, out var eastWestRemaining);
            ResolveSignal(cycle, 사가정저밀도교통WireCodes.NorthSouth,
                out var expectedNorthSouth, out var northSouthRemaining);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var axes = new Dictionary<string, 사가정저밀도교통SignalData>(
                StringComparer.Ordinal);
            foreach (var signal in snapshot.signals)
            {
                if (signal == null || string.IsNullOrWhiteSpace(signal.stableId)
                    || !ids.Add(signal.stableId)
                    || (signal.axisCode != 사가정저밀도교통WireCodes.EastWest
                        && signal.axisCode != 사가정저밀도교통WireCodes.NorthSouth)
                    || !사가정저밀도교통WireCodes.IsSignalState(signal.stateCode)
                    || !Finite(signal.secondsUntilTransition)
                    || signal.secondsUntilTransition <= 0d
                    || string.IsNullOrWhiteSpace(signal.stopLineStableId)
                    || axes.ContainsKey(signal.axisCode))
                    return false;
                axes.Add(signal.axisCode, signal);
            }
            if (!axes.TryGetValue(사가정저밀도교통WireCodes.EastWest, out var eastWest)
                || !axes.TryGetValue(사가정저밀도교통WireCodes.NorthSouth,
                    out var northSouth)
                || eastWest.stableId != "signal-group:synthetic:sagajeong:ew.r1"
                || eastWest.stopLineStableId != "stop-line:synthetic:sagajeong:ew.r1"
                || northSouth.stableId != "signal-group:synthetic:sagajeong:ns.r1"
                || northSouth.stopLineStableId != "stop-line:synthetic:sagajeong:ns.r1"
                || eastWest.stateCode != expectedEastWest
                || northSouth.stateCode != expectedNorthSouth
                || !Same(eastWest.secondsUntilTransition, eastWestRemaining)
                || !Same(northSouth.secondsUntilTransition, northSouthRemaining))
                return false;
            phaseCode = Phase(cycle);
            models = snapshot.signals.Select(value => new 사가정저밀도교통SignalModel
            {
                StableId = value.stableId,
                AxisCode = value.axisCode,
                StateCode = value.stateCode,
                SecondsUntilTransition = value.secondsUntilTransition,
                StopLineStableId = value.stopLineStableId,
            }).ToArray();
            return true;
        }

        private static bool ValidateLanes(사가정저밀도교통SnapshotData snapshot,
            out Dictionary<string, 사가정저밀도교통LaneModel> byId,
            out 사가정저밀도교통LaneModel[] models,
            out string failureCode)
        {
            byId = new Dictionary<string, 사가정저밀도교통LaneModel>(
                StringComparer.Ordinal);
            models = Array.Empty<사가정저밀도교통LaneModel>();
            failureCode = 사가정저밀도교통FailureCodes.LaneStateInvalid;
            if (snapshot.lanes == null
                || snapshot.lanes.Length != 사가정저밀도교통Fixture.ExpectedLaneCount)
                return false;
            var expectations = LaneExpectations();
            var incomingModels = new List<사가정저밀도교통LaneModel>(snapshot.lanes.Length);
            foreach (var lane in snapshot.lanes)
            {
                if (lane == null || string.IsNullOrWhiteSpace(lane.stableId)
                    || !expectations.TryGetValue(lane.stableId, out var expected)
                    || lane.sourceWayStableId != expected.SourceWayStableId
                    || lane.axisCode != expected.AxisCode
                    || lane.routeKindCode != expected.RouteKindCode
                    || lane.sourceDirectionCode != 사가정저밀도교통WireCodes.UnknownDirection
                    || lane.sourceAccessReviewCode !=
                    사가정저밀도교통WireCodes.PendingHumanReview
                    || lane.sourceRuntimeAuthorized
                    || lane.hasStopLine != expected.HasStopLine
                    || lane.pingPong || lane.points == null || lane.points.Length < 2
                    || !Finite(lane.stopLineProgressMeters)
                    || lane.hasStopLine && (!Same(lane.stopLineProgressMeters,
                            expected.StopLineProgressMeters)
                        || lane.stopLineProgressMeters <=
                        사가정저밀도교통Fixture.StopBufferMeters)
                    || !lane.hasStopLine && !Same(lane.stopLineProgressMeters, 0d)
                    || byId.ContainsKey(lane.stableId))
                    return false;
                var points = new 사가정저밀도교통Point[lane.points.Length];
                for (var index = 0; index < lane.points.Length; index++)
                {
                    var point = lane.points[index];
                    if (point == null || !Finite(point.x) || !Finite(point.z)) return false;
                    points[index] = new 사가정저밀도교통Point(point.x, point.z);
                    if (index > 0 && Same(points[index - 1].DistanceTo(points[index]), 0d))
                        return false;
                }
                var length = 사가정저밀도교통Fixture.Length(points);
                if (!Finite(length) || length <= 0d
                    || lane.hasStopLine && lane.stopLineProgressMeters >= length)
                    return false;
                var model = new 사가정저밀도교통LaneModel
                {
                    StableId = lane.stableId,
                    SourceWayStableId = lane.sourceWayStableId,
                    AxisCode = lane.axisCode,
                    RouteKindCode = lane.routeKindCode,
                    SourceDirectionCode = lane.sourceDirectionCode,
                    SourceAccessReviewCode = lane.sourceAccessReviewCode,
                    SourceRuntimeAuthorized = lane.sourceRuntimeAuthorized,
                    HasStopLine = lane.hasStopLine,
                    StopLineProgressMeters = lane.stopLineProgressMeters,
                    PingPong = lane.pingPong,
                    Points = Array.AsReadOnly(points),
                    LengthMeters = length,
                };
                byId.Add(model.StableId, model);
                incomingModels.Add(model);
            }
            string computed;
            try
            {
                computed = 사가정저밀도교통Fingerprint.ComputeLaneGraphHash(snapshot.lanes);
            }
            catch (Exception exception) when (exception is ArgumentException
                                              || exception is InvalidOperationException)
            {
                return false;
            }
            if (computed != snapshot.laneGraphHash)
            {
                failureCode = 사가정저밀도교통FailureCodes.LaneGraphHashMismatch;
                return false;
            }
            models = incomingModels.ToArray();
            return true;
        }

        private static bool ValidateRouteLegs(사가정저밀도교통SnapshotData snapshot,
            IReadOnlyDictionary<string, 사가정저밀도교통LaneModel> lanes,
            out 사가정저밀도교통CourierRouteLegModel[] models)
        {
            models = Array.Empty<사가정저밀도교통CourierRouteLegModel>();
            if (snapshot.courierRouteLegs == null
                || snapshot.courierRouteLegs.Length !=
                사가정저밀도교통Fixture.ExpectedRouteLegCount)
                return false;
            var expectedIds = new[]
            {
                사가정저밀도교통WireCodes.MainRoadRouteLeg,
                사가정저밀도교통WireCodes.NeighborhoodRoadRouteLeg,
                사가정저밀도교통WireCodes.AlleyFinalRouteLeg,
            };
            var expectedLanes = new[]
            {
                사가정저밀도교통WireCodes.EastWestLane,
                사가정저밀도교통WireCodes.NeighborhoodAccessLane,
                사가정저밀도교통WireCodes.AlleyCandidateLane,
            };
            var expectedStages = new[]
            {
                사가정저밀도교통WireCodes.MainRoadApproach,
                사가정저밀도교통WireCodes.NeighborhoodRoadApproach,
                사가정저밀도교통WireCodes.AlleyFinalApproach,
            };
            var sourceEdgeIds = new HashSet<string>(StringComparer.Ordinal);
            var result = new 사가정저밀도교통CourierRouteLegModel[
                snapshot.courierRouteLegs.Length];
            for (var index = 0; index < snapshot.courierRouteLegs.Length; index++)
            {
                var leg = snapshot.courierRouteLegs[index];
                if (leg == null || leg.index != index || leg.stableId != expectedIds[index]
                    || leg.laneStableId != expectedLanes[index]
                    || leg.stageCode != expectedStages[index]
                    || !lanes.TryGetValue(leg.laneStableId, out var lane)
                    || !Finite(leg.displayGeometryDistanceMeters)
                    || !Same(leg.displayGeometryDistanceMeters, lane.LengthMeters)
                    || !Finite(leg.sourceDistanceMeters) || leg.sourceDistanceMeters <= 0d
                    || leg.sourceEdges == null || leg.sourceEdges.Length == 0)
                    return false;
                if (index > 0)
                {
                    var previous = lanes[expectedLanes[index - 1]].Points;
                    if (!SamePoint(previous[previous.Count - 1], lane.Points[0])) return false;
                }
                var edgeModels = new 사가정저밀도교통SourceEdgeModel[
                    leg.sourceEdges.Length];
                var sourceDistance = 0d;
                for (var edgeIndex = 0; edgeIndex < leg.sourceEdges.Length; edgeIndex++)
                {
                    var edge = leg.sourceEdges[edgeIndex];
                    if (edge == null || string.IsNullOrWhiteSpace(edge.edgeStableId)
                        || !sourceEdgeIds.Add(edge.edgeStableId)
                        || edge.displayGeometryOrderCode != 사가정저밀도교통WireCodes.AsStored
                        && edge.displayGeometryOrderCode !=
                        사가정저밀도교통WireCodes.ReverseStored
                        || edge.sourceDirectionCode !=
                        사가정저밀도교통WireCodes.UnknownDirection
                        || edge.accessReviewCode !=
                        사가정저밀도교통WireCodes.PendingHumanReview
                        || edge.runtimeAuthorized || !Finite(edge.sourceDistanceMeters)
                        || edge.sourceDistanceMeters <= 0d || edge.displayFrom == null
                        || edge.displayTo == null || !Finite(edge.displayFrom.x)
                        || !Finite(edge.displayFrom.z) || !Finite(edge.displayTo.x)
                        || !Finite(edge.displayTo.z))
                        return false;
                    var from = new 사가정저밀도교통Point(
                        edge.displayFrom.x, edge.displayFrom.z);
                    var to = new 사가정저밀도교통Point(
                        edge.displayTo.x, edge.displayTo.z);
                    if (SamePoint(from, to)
                        || edgeIndex > 0
                        && !SamePoint(edgeModels[edgeIndex - 1].DisplayTo, from))
                        return false;
                    sourceDistance += edge.sourceDistanceMeters;
                    edgeModels[edgeIndex] = new 사가정저밀도교통SourceEdgeModel
                    {
                        EdgeStableId = edge.edgeStableId,
                        DisplayGeometryOrderCode = edge.displayGeometryOrderCode,
                        SourceDirectionCode = edge.sourceDirectionCode,
                        AccessReviewCode = edge.accessReviewCode,
                        RuntimeAuthorized = edge.runtimeAuthorized,
                        SourceDistanceMeters = edge.sourceDistanceMeters,
                        DisplayFrom = from,
                        DisplayTo = to,
                    };
                }
                if (!Same(sourceDistance, leg.sourceDistanceMeters)
                    || !SamePoint(edgeModels[0].DisplayFrom, lane.Points[0])
                    || !SamePoint(edgeModels[edgeModels.Length - 1].DisplayTo,
                        lane.Points[lane.Points.Count - 1]))
                    return false;
                result[index] = new 사가정저밀도교통CourierRouteLegModel
                {
                    StableId = leg.stableId,
                    Index = leg.index,
                    LaneStableId = leg.laneStableId,
                    StageCode = leg.stageCode,
                    DisplayGeometryDistanceMeters = leg.displayGeometryDistanceMeters,
                    SourceDistanceMeters = leg.sourceDistanceMeters,
                    SourceEdges = Array.AsReadOnly(edgeModels),
                };
            }
            string computed;
            try
            {
                computed = 사가정저밀도교통Fingerprint
                    .ComputeCourierDisplayRouteFingerprint(snapshot);
            }
            catch (Exception exception) when (exception is ArgumentException
                                              || exception is InvalidOperationException)
            {
                return false;
            }
            if (computed != snapshot.courierDisplayRouteFingerprint) return false;
            models = result;
            return true;
        }

        private static bool ValidateRouteState(
            사가정저밀도교통CourierRouteStateData state,
            IReadOnlyList<사가정저밀도교통CourierRouteLegModel> legs,
            IReadOnlyDictionary<string, 사가정저밀도교통LaneModel> lanes,
            out 사가정저밀도교통CourierRouteStateModel model)
        {
            model = null;
            if (state == null || state.routeLegCount != legs.Count
                || state.currentRouteLegIndex < 0
                || state.currentRouteLegIndex >= legs.Count
                || !Finite(state.legProgressMeters)
                || !Finite(state.cumulativeProgressMeters)
                || !Finite(state.totalDistanceMeters))
                return false;
            var current = legs[state.currentRouteLegIndex];
            var total = legs.Sum(value => value.DisplayGeometryDistanceMeters);
            var completed = legs.Take(state.currentRouteLegIndex)
                .Sum(value => value.DisplayGeometryDistanceMeters);
            var expectedStage = state.candidateEndpointReached
                ? 사가정저밀도교통WireCodes.CandidateEndpointReached
                : current.StageCode;
            if (state.currentRouteLegStableId != current.StableId
                || state.currentLaneStableId != current.LaneStableId
                || state.stageCode != expectedStage
                || state.legProgressMeters < 0d
                || state.legProgressMeters > current.DisplayGeometryDistanceMeters + .000001d
                || !Same(state.cumulativeProgressMeters,
                    completed + state.legProgressMeters)
                || !Same(state.totalDistanceMeters, total)
                || !Same(total, 사가정저밀도교통Fixture.TotalDisplayRouteDistanceMeters)
                || !lanes.ContainsKey(state.currentLaneStableId))
                return false;
            var terminal = state.currentRouteLegIndex == legs.Count - 1
                           && Same(state.legProgressMeters,
                               current.DisplayGeometryDistanceMeters)
                           && Same(state.cumulativeProgressMeters, total);
            if (state.candidateEndpointReached != terminal) return false;
            model = new 사가정저밀도교통CourierRouteStateModel
            {
                CurrentRouteLegStableId = state.currentRouteLegStableId,
                CurrentRouteLegIndex = state.currentRouteLegIndex,
                RouteLegCount = state.routeLegCount,
                CurrentLaneStableId = state.currentLaneStableId,
                StageCode = state.stageCode,
                LegProgressMeters = state.legProgressMeters,
                CumulativeProgressMeters = state.cumulativeProgressMeters,
                TotalDistanceMeters = state.totalDistanceMeters,
                CandidateEndpointReached = state.candidateEndpointReached,
            };
            return true;
        }

        private static bool ValidateActors(사가정저밀도교통SnapshotData snapshot,
            IReadOnlyDictionary<string, 사가정저밀도교통LaneModel> lanes,
            사가정저밀도교통CourierRouteStateModel routeState,
            out 사가정저밀도교통ActorModel[] models,
            out bool routeMismatch)
        {
            models = Array.Empty<사가정저밀도교통ActorModel>();
            routeMismatch = false;
            var actors = snapshot.actors;
            if (actors == null || actors.Length < 1
                || actors.Length > 사가정저밀도교통Fixture.MaximumActorCount)
                return false;
            var known = new Dictionary<string, ActorExpectation>(StringComparer.Ordinal)
            {
                [사가정저밀도교통Fixture.CourierActorId] = new ActorExpectation(
                    사가정저밀도교통WireCodes.CourierMotorcycle, null),
                [사가정저밀도교통Fixture.AmbientActor1] = new ActorExpectation(
                    사가정저밀도교통WireCodes.AmbientPassengerCar,
                    사가정저밀도교통WireCodes.EastWestLane),
                [사가정저밀도교통Fixture.AmbientActor2] = new ActorExpectation(
                    사가정저밀도교통WireCodes.AmbientPassengerCar,
                    사가정저밀도교통WireCodes.EastWestLane),
                [사가정저밀도교통Fixture.AmbientActor3] = new ActorExpectation(
                    사가정저밀도교통WireCodes.AmbientPassengerCar,
                    사가정저밀도교통WireCodes.NorthSouthLane),
            };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var queueSlots = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<사가정저밀도교통ActorModel>(actors.Length);
            사가정저밀도교통ActorData courier = null;
            foreach (var actor in actors)
            {
                if (actor == null || !known.TryGetValue(actor.stableId, out var expected)
                    || actor.kindCode != expected.KindCode || !ids.Add(actor.stableId)
                    || !사가정저밀도교통WireCodes.IsActorKind(actor.kindCode)
                    || !사가정저밀도교통WireCodes.IsActorState(actor.stateCode)
                    || actor.displayPolylineOrderCode != 사가정저밀도교통WireCodes.AsDefined
                    || !lanes.TryGetValue(actor.laneStableId, out var lane)
                    || expected.LaneStableId != null
                    && actor.laneStableId != expected.LaneStableId
                    || actor.routeKindCode != lane.RouteKindCode
                    || !Finite(actor.progressMeters) || actor.progressMeters < 0d
                    || actor.progressMeters > lane.LengthMeters + .000001d
                    || !Finite(actor.speedMetersPerSecond)
                    || actor.speedMetersPerSecond < 0d
                    || actor.position == null || !Finite(actor.position.x)
                    || !Finite(actor.position.z) || !Finite(actor.headingDegrees)
                    || actor.headingDegrees < 0d || actor.headingDegrees >= 360d)
                    return false;
                if (!ValidateActorState(actor, lane, queueSlots)) return false;
                if (!사가정저밀도교통Fixture.TrySample(lane.Points, actor.progressMeters,
                        out var expectedPosition, out var expectedDirection))
                    return false;
                var expectedHeading = NormalizeHeading(Math.Atan2(expectedDirection.X,
                    expectedDirection.Z) * 180d / Math.PI);
                if (!Same(actor.position.x, expectedPosition.X)
                    || !Same(actor.position.z, expectedPosition.Z)
                    || !Same(actor.headingDegrees, expectedHeading))
                    return false;
                if (actor.kindCode == 사가정저밀도교통WireCodes.CourierMotorcycle)
                {
                    if (courier != null) return false;
                    courier = actor;
                }
                result.Add(new 사가정저밀도교통ActorModel
                {
                    StableId = actor.stableId,
                    KindCode = actor.kindCode,
                    LaneStableId = actor.laneStableId,
                    RouteKindCode = actor.routeKindCode,
                    StateCode = actor.stateCode,
                    DisplayPolylineOrderCode = actor.displayPolylineOrderCode,
                    ProgressMeters = actor.progressMeters,
                    SpeedMetersPerSecond = actor.speedMetersPerSecond,
                    QueueIndex = actor.queueIndex,
                    BlockedReasonCode = actor.blockedReasonCode ?? string.Empty,
                    Position = new 사가정저밀도교통Point(actor.position.x, actor.position.z),
                    HeadingDegrees = actor.headingDegrees,
                });
            }
            if (courier == null) return false;
            routeMismatch = courier.laneStableId != routeState.CurrentLaneStableId
                            || !Same(courier.progressMeters, routeState.LegProgressMeters)
                            || routeState.CandidateEndpointReached !=
                            (courier.stateCode ==
                             사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint);
            if (routeMismatch) return false;
            models = result.ToArray();
            return true;
        }

        private static bool ValidateActorState(사가정저밀도교통ActorData actor,
            사가정저밀도교통LaneModel lane, ISet<string> queueSlots)
        {
            var courier = actor.kindCode == 사가정저밀도교통WireCodes.CourierMotorcycle;
            var controlled = lane.AxisCode == 사가정저밀도교통WireCodes.EastWest
                             || lane.AxisCode == 사가정저밀도교통WireCodes.NorthSouth;
            var neighborhood = lane.AxisCode ==
                               사가정저밀도교통WireCodes.NeighborhoodUncontrolled;
            var alley = lane.AxisCode == 사가정저밀도교통WireCodes.AlleyUncontrolled;
            if (courier)
            {
                var stateAllowed = controlled && (actor.stateCode ==
                                                   사가정저밀도교통WireCodes.Moving
                                                   || actor.stateCode ==
                                                   사가정저밀도교통WireCodes.WaitingAtSignal
                                                   || actor.stateCode ==
                                                   사가정저밀도교통WireCodes.Queueing)
                                   || neighborhood && actor.stateCode ==
                                   사가정저밀도교통WireCodes.MovingOnNeighborhoodRoad
                                   || alley && (actor.stateCode ==
                                                사가정저밀도교통WireCodes.MovingInAlley
                                                || actor.stateCode ==
                                                사가정저밀도교통WireCodes.WaitingForAlley
                                                || actor.stateCode ==
                                                사가정저밀도교통WireCodes
                                                    .StoppedAtCandidateEndpoint);
                if (!stateAllowed) return false;
            }
            else if (!controlled || actor.stateCode != 사가정저밀도교통WireCodes.Moving
                     && actor.stateCode != 사가정저밀도교통WireCodes.WaitingAtSignal
                     && actor.stateCode != 사가정저밀도교통WireCodes.Queueing)
                return false;

            var maxSpeed = controlled ? 사가정저밀도교통Fixture.MainSpeedMetersPerSecond
                : neighborhood ? 사가정저밀도교통Fixture.NeighborhoodSpeedMetersPerSecond
                : 사가정저밀도교통Fixture.AlleySpeedMetersPerSecond;
            if (actor.speedMetersPerSecond > maxSpeed + .000001d) return false;

            var queued = actor.stateCode == 사가정저밀도교통WireCodes.WaitingAtSignal
                         || actor.stateCode == 사가정저밀도교통WireCodes.Queueing;
            if (queued)
            {
                var expectedReason = actor.stateCode ==
                    사가정저밀도교통WireCodes.WaitingAtSignal
                    ? 사가정저밀도교통WireCodes.SyntheticSignalGate
                    : 사가정저밀도교통WireCodes.MinimumGap;
                return actor.queueIndex >= 0
                       && queueSlots.Add(actor.laneStableId + "\u001f" + actor.queueIndex)
                       && actor.blockedReasonCode == expectedReason
                       && Same(actor.speedMetersPerSecond, 0d);
            }
            if (actor.queueIndex != -1) return false;
            if (actor.stateCode == 사가정저밀도교통WireCodes.WaitingForAlley)
                return !string.IsNullOrWhiteSpace(actor.blockedReasonCode)
                       && Same(actor.speedMetersPerSecond, 0d);
            if (actor.stateCode == 사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint)
                return string.IsNullOrEmpty(actor.blockedReasonCode)
                       && Same(actor.speedMetersPerSecond, 0d);
            return string.IsNullOrEmpty(actor.blockedReasonCode);
        }

        private static Dictionary<string, LaneExpectation> LaneExpectations()
            => new(StringComparer.Ordinal)
            {
                [사가정저밀도교통WireCodes.EastWestLane] = new LaneExpectation(
                    "osm:way:218790209", 사가정저밀도교통WireCodes.EastWest,
                    사가정저밀도교통WireCodes.SyntheticLaneDisplayOnly, true, 52.59d),
                [사가정저밀도교통WireCodes.NorthSouthLane] = new LaneExpectation(
                    "osm:way:218798305", 사가정저밀도교통WireCodes.NorthSouth,
                    사가정저밀도교통WireCodes.SyntheticLaneDisplayOnly, true, 37.25d),
                [사가정저밀도교통WireCodes.NeighborhoodAccessLane] =
                    new LaneExpectation(
                        "osm:way-chain:1112326984+1112326970+576294786",
                        사가정저밀도교통WireCodes.NeighborhoodUncontrolled,
                        사가정저밀도교통WireCodes.ManualVisualReviewCandidate,
                        false, 0d),
                [사가정저밀도교통WireCodes.AlleyCandidateLane] = new LaneExpectation(
                    "osm:way:1256772587", 사가정저밀도교통WireCodes.AlleyUncontrolled,
                    사가정저밀도교통WireCodes.ManualVisualReviewCandidate, false, 0d),
            };

        private static void ResolveSignal(double cycle, string axis,
            out string state, out double remaining)
        {
            if (axis == 사가정저밀도교통WireCodes.NorthSouth)
            {
                if (cycle < 10d)
                {
                    state = 사가정저밀도교통WireCodes.Green;
                    remaining = 10d - cycle;
                    return;
                }
                if (cycle < 12d)
                {
                    state = 사가정저밀도교통WireCodes.Amber;
                    remaining = 12d - cycle;
                    return;
                }
                state = 사가정저밀도교통WireCodes.Red;
                remaining = 28d - cycle;
                return;
            }
            if (cycle < 14d)
            {
                state = 사가정저밀도교통WireCodes.Red;
                remaining = 14d - cycle;
            }
            else if (cycle < 24d)
            {
                state = 사가정저밀도교통WireCodes.Green;
                remaining = 24d - cycle;
            }
            else if (cycle < 26d)
            {
                state = 사가정저밀도교통WireCodes.Amber;
                remaining = 26d - cycle;
            }
            else
            {
                state = 사가정저밀도교통WireCodes.Red;
                remaining = 28d - cycle;
            }
        }

        private static string Phase(double cycle)
            => cycle < 10d ? 사가정저밀도교통WireCodes.NorthSouthGreen
                : cycle < 12d ? 사가정저밀도교통WireCodes.NorthSouthAmber
                : cycle < 14d ? 사가정저밀도교통WireCodes.AllRedBeforeEastWest
                : cycle < 24d ? 사가정저밀도교통WireCodes.EastWestGreen
                : cycle < 26d ? 사가정저밀도교통WireCodes.EastWestAmber
                : 사가정저밀도교통WireCodes.AllRedBeforeNorthSouth;

        private static double NormalizeHeading(double value)
        {
            value %= 360d;
            return value < 0d ? value + 360d : value;
        }

        private static bool TryUtc(string value, out DateTimeOffset result)
        {
            result = default;
            var normalized = value?.TrimEnd() ?? string.Empty;
            var explicitUtc = normalized.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                              || normalized.EndsWith("+00:00", StringComparison.Ordinal);
            return explicitUtc && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                       DateTimeStyles.AllowWhiteSpaces, out result)
                   && result.Offset == TimeSpan.Zero;
        }

        private static bool Missing(params string[] values)
            => values.Any(string.IsNullOrWhiteSpace);

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool Same(double first, double second)
            => Math.Abs(first - second) <= .000001d;

        private static bool SamePoint(사가정저밀도교통Point first,
            사가정저밀도교통Point second)
            => Same(first.X, second.X) && Same(first.Z, second.Z);

        private static bool Fail(string code, out string failureCode)
        {
            failureCode = code;
            return false;
        }

        private sealed class LaneExpectation
        {
            public LaneExpectation(string sourceWayStableId, string axisCode,
                string routeKindCode, bool hasStopLine, double stopLineProgressMeters)
            {
                SourceWayStableId = sourceWayStableId;
                AxisCode = axisCode;
                RouteKindCode = routeKindCode;
                HasStopLine = hasStopLine;
                StopLineProgressMeters = stopLineProgressMeters;
            }

            public string SourceWayStableId { get; }
            public string AxisCode { get; }
            public string RouteKindCode { get; }
            public bool HasStopLine { get; }
            public double StopLineProgressMeters { get; }
        }

        private sealed class ActorExpectation
        {
            public ActorExpectation(string kindCode, string laneStableId)
            {
                KindCode = kindCode;
                LaneStableId = laneStableId;
            }

            public string KindCode { get; }
            public string LaneStableId { get; }
        }

        private sealed class AcceptedState
        {
            public AcceptedState(long revision, string stateFingerprint,
                int routeLegIndex, double cumulativeProgressMeters)
            {
                Revision = revision;
                StateFingerprint = stateFingerprint;
                RouteLegIndex = routeLegIndex;
                CumulativeProgressMeters = cumulativeProgressMeters;
            }

            public long Revision { get; }
            public string StateFingerprint { get; }
            public int RouteLegIndex { get; }
            public double CumulativeProgressMeters { get; }
        }
    }

    public static class 사가정저밀도교통Fingerprint
    {
        public static bool IsUpperSha256(string value)
            => value != null && value.Length == 64
               && value.All(character => character >= '0' && character <= '9'
                                         || character >= 'A' && character <= 'F');

        public static string ComputeLaneGraphHash(
            IReadOnlyList<사가정저밀도교통LaneData> lanes)
        {
            if (lanes == null) throw new ArgumentNullException(nameof(lanes));
            var ordered = lanes.OrderBy(value => value?.stableId, StringComparer.Ordinal)
                .ToArray();
            var builder = new StringBuilder();
            Token(builder, "sagajeong-lane-graph.v3");
            Token(builder, ordered.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var lane in ordered)
            {
                if (lane == null || lane.points == null)
                    throw new ArgumentException("TrafficLaneMissing", nameof(lanes));
                Token(builder, "lane");
                Token(builder, lane.stableId);
                Token(builder, lane.sourceWayStableId);
                Token(builder, lane.axisCode);
                Token(builder, lane.routeKindCode);
                Token(builder, lane.sourceDirectionCode);
                Token(builder, lane.sourceAccessReviewCode);
                Token(builder, lane.sourceRuntimeAuthorized ? "1" : "0");
                Token(builder, Number(lane.hasStopLine ? lane.stopLineProgressMeters : -1d));
                Token(builder, lane.pingPong ? "1" : "0");
                Token(builder, lane.points.Length.ToString(CultureInfo.InvariantCulture));
                foreach (var point in lane.points)
                {
                    if (point == null)
                        throw new ArgumentException("TrafficLanePointMissing", nameof(lanes));
                    Token(builder, Number(point.x));
                    Token(builder, Number(point.z));
                }
            }
            return Hash(builder.ToString());
        }

        public static string ComputeCourierDisplayRouteFingerprint(
            사가정저밀도교통SnapshotData snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.courierRouteLegs == null)
                throw new ArgumentException("CourierRouteLegsMissing", nameof(snapshot));
            if (snapshot.lanes == null)
                throw new ArgumentException("TrafficLanesMissing", nameof(snapshot));
            var byId = snapshot.lanes.ToDictionary(value => value?.stableId
                ?? throw new ArgumentException("TrafficLaneMissing", nameof(snapshot)),
                StringComparer.Ordinal);
            var builder = new StringBuilder();
            Token(builder, "sagajeong-courier-display-route.v2");
            Token(builder, snapshot.courierDisplayRouteRevision);
            Token(builder, snapshot.laneGraphHash);
            Token(builder, snapshot.sourceMobilityGraphStableId);
            Token(builder, snapshot.sourceMobilityGraphRevision);
            Token(builder, snapshot.sourceMobilityGraphProjectionHashSha256);
            Token(builder, snapshot.sourceMobilityGraphContentHashSha256);
            Token(builder, snapshot.sourceAccessReviewCode);
            Token(builder, snapshot.sourceDirectionAuthorityCode);
            Token(builder, snapshot.sourceRuntimeAuthorized ? "1" : "0");
            Token(builder, snapshot.courierRouteLegs.Length.ToString(
                CultureInfo.InvariantCulture));
            foreach (var leg in snapshot.courierRouteLegs)
            {
                if (leg == null || !byId.TryGetValue(leg.laneStableId, out var lane)
                    || lane.points == null || leg.sourceEdges == null)
                    throw new ArgumentException("CourierRouteLegInvalid", nameof(snapshot));
                var points = lane.points.Select(value => value == null
                        ? throw new ArgumentException("TrafficLanePointMissing", nameof(snapshot))
                        : new 사가정저밀도교통Point(value.x, value.z))
                    .ToArray();
                Token(builder, leg.stableId);
                Token(builder, leg.laneStableId);
                Token(builder, leg.stageCode);
                Token(builder, Number(사가정저밀도교통Fixture.Length(points)));
                Token(builder, leg.sourceEdges.Length.ToString(CultureInfo.InvariantCulture));
                foreach (var edge in leg.sourceEdges)
                {
                    if (edge == null || edge.displayFrom == null || edge.displayTo == null)
                        throw new ArgumentException("CourierRouteSourceEdgeMissing",
                            nameof(snapshot));
                    Token(builder, edge.edgeStableId);
                    Token(builder, edge.displayGeometryOrderCode);
                    Token(builder, edge.sourceDirectionCode);
                    Token(builder, edge.accessReviewCode);
                    Token(builder, edge.runtimeAuthorized ? "1" : "0");
                    Token(builder, Number(edge.sourceDistanceMeters));
                    Token(builder, Number(edge.displayFrom.x));
                    Token(builder, Number(edge.displayFrom.z));
                    Token(builder, Number(edge.displayTo.x));
                    Token(builder, Number(edge.displayTo.z));
                }
            }
            return Hash(builder.ToString());
        }

        internal static string ComputeSnapshotState(사가정저밀도교통SnapshotData value)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
            {
                writer.Write(value.sourceMobilityGraphProjectionHashSha256 ?? string.Empty);
                writer.Write(value.sourceMobilityGraphContentHashSha256 ?? string.Empty);
                writer.Write(value.laneGraphHash ?? string.Empty);
                writer.Write(value.courierDisplayRouteFingerprint ?? string.Empty);
                writer.Write(value.configurationFingerprint ?? string.Empty);
                writer.Write(value.inputStateFingerprint ?? string.Empty);
                writer.Write(value.simulationTick);
                writer.Write(value.revision);
                writer.Write(value.observedAt ?? string.Empty);
                writer.Write(value.journeyBindingStateCode ?? string.Empty);
                writer.Write(value.operationalBindingReady);
                writer.Write(value.canonicalDeliveryMutationAllowed);
                foreach (var signal in (value.signals ??
                             Array.Empty<사가정저밀도교통SignalData>())
                         .OrderBy(item => item?.stableId, StringComparer.Ordinal))
                {
                    writer.Write(signal?.stableId ?? string.Empty);
                    writer.Write(signal?.axisCode ?? string.Empty);
                    writer.Write(signal?.stateCode ?? string.Empty);
                    writer.Write(signal?.secondsUntilTransition ?? 0d);
                    writer.Write(signal?.stopLineStableId ?? string.Empty);
                }
                foreach (var lane in (value.lanes ?? Array.Empty<사가정저밀도교통LaneData>())
                         .OrderBy(item => item?.stableId, StringComparer.Ordinal))
                {
                    writer.Write(lane?.stableId ?? string.Empty);
                    writer.Write(lane?.sourceWayStableId ?? string.Empty);
                    writer.Write(lane?.axisCode ?? string.Empty);
                    writer.Write(lane?.routeKindCode ?? string.Empty);
                    writer.Write(lane?.sourceDirectionCode ?? string.Empty);
                    writer.Write(lane?.sourceAccessReviewCode ?? string.Empty);
                    writer.Write(lane?.sourceRuntimeAuthorized ?? false);
                    writer.Write(lane?.hasStopLine ?? false);
                    writer.Write(lane?.stopLineProgressMeters ?? 0d);
                    writer.Write(lane?.pingPong ?? false);
                    foreach (var point in lane?.points ??
                                 Array.Empty<사가정저밀도교통PointData>())
                    {
                        writer.Write(point?.x ?? 0d);
                        writer.Write(point?.z ?? 0d);
                    }
                }
                foreach (var actor in (value.actors ??
                             Array.Empty<사가정저밀도교통ActorData>())
                         .OrderBy(item => item?.stableId, StringComparer.Ordinal))
                {
                    writer.Write(actor?.stableId ?? string.Empty);
                    writer.Write(actor?.kindCode ?? string.Empty);
                    writer.Write(actor?.laneStableId ?? string.Empty);
                    writer.Write(actor?.routeKindCode ?? string.Empty);
                    writer.Write(actor?.stateCode ?? string.Empty);
                    writer.Write(actor?.displayPolylineOrderCode ?? string.Empty);
                    writer.Write(actor?.progressMeters ?? 0d);
                    writer.Write(actor?.speedMetersPerSecond ?? 0d);
                    writer.Write(actor?.queueIndex ?? -1);
                    writer.Write(actor?.blockedReasonCode ?? string.Empty);
                    writer.Write(actor?.position?.x ?? 0d);
                    writer.Write(actor?.position?.z ?? 0d);
                    writer.Write(actor?.headingDegrees ?? 0d);
                }
                foreach (var leg in (value.courierRouteLegs ??
                             Array.Empty<사가정저밀도교통CourierRouteLegData>())
                         .OrderBy(item => item?.index ?? -1))
                {
                    writer.Write(leg?.stableId ?? string.Empty);
                    writer.Write(leg?.index ?? -1);
                    writer.Write(leg?.laneStableId ?? string.Empty);
                    writer.Write(leg?.stageCode ?? string.Empty);
                    writer.Write(leg?.displayGeometryDistanceMeters ?? 0d);
                    writer.Write(leg?.sourceDistanceMeters ?? 0d);
                    foreach (var edge in leg?.sourceEdges ??
                                 Array.Empty<사가정저밀도교통SourceEdgeData>())
                    {
                        writer.Write(edge?.edgeStableId ?? string.Empty);
                        writer.Write(edge?.displayGeometryOrderCode ?? string.Empty);
                        writer.Write(edge?.sourceDirectionCode ?? string.Empty);
                        writer.Write(edge?.accessReviewCode ?? string.Empty);
                        writer.Write(edge?.runtimeAuthorized ?? false);
                        writer.Write(edge?.sourceDistanceMeters ?? 0d);
                        writer.Write(edge?.displayFrom?.x ?? 0d);
                        writer.Write(edge?.displayFrom?.z ?? 0d);
                        writer.Write(edge?.displayTo?.x ?? 0d);
                        writer.Write(edge?.displayTo?.z ?? 0d);
                    }
                }
                var state = value.courierRouteState;
                writer.Write(state?.currentRouteLegStableId ?? string.Empty);
                writer.Write(state?.currentRouteLegIndex ?? -1);
                writer.Write(state?.routeLegCount ?? 0);
                writer.Write(state?.currentLaneStableId ?? string.Empty);
                writer.Write(state?.stageCode ?? string.Empty);
                writer.Write(state?.legProgressMeters ?? 0d);
                writer.Write(state?.cumulativeProgressMeters ?? 0d);
                writer.Write(state?.totalDistanceMeters ?? 0d);
                writer.Write(state?.candidateEndpointReached ?? false);
                writer.Flush();
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray()))
                .Replace("-", string.Empty);
        }

        private static string Number(double value)
            => value.ToString("R", CultureInfo.InvariantCulture);

        private static void Token(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            builder.Append(Encoding.UTF8.GetByteCount(value).ToString(
                    CultureInfo.InvariantCulture))
                .Append(':').Append(value);
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty);
        }
    }
}
