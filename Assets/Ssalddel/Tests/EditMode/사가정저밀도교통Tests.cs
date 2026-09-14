using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using ServerSnapshot = Ssalddel.Simulation.Contracts.사가정저밀도교통Snapshot;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정저밀도교통Tests
    {
        [Test]
        public void 서버_v2의_lane과_표시경로_fingerprint를_Unity가_독립재계산한다()
        {
            var snapshot = Snapshot();

            Assert.That(snapshot.schemaVersion,
                Is.EqualTo(사가정저밀도교통WireCodes.SchemaVersion));
            Assert.That(snapshot.lanes, Has.Length.EqualTo(4));
            Assert.That(snapshot.courierRouteLegs, Has.Length.EqualTo(3));
            Assert.That(snapshot.lanes.All(value =>
                value.sourceDirectionCode == 사가정저밀도교통WireCodes.UnknownDirection
                && value.sourceAccessReviewCode ==
                사가정저밀도교통WireCodes.PendingHumanReview
                && !value.sourceRuntimeAuthorized), Is.True);
            Assert.That(snapshot.courierRouteLegs.SelectMany(value => value.sourceEdges)
                .Count(), Is.EqualTo(15));
            Assert.That(snapshot.courierRouteLegs.SelectMany(value => value.sourceEdges)
                .All(value => value.sourceDirectionCode ==
                                  사가정저밀도교통WireCodes.UnknownDirection
                              && value.accessReviewCode ==
                                  사가정저밀도교통WireCodes.PendingHumanReview
                              && !value.runtimeAuthorized), Is.True);
            Assert.That(사가정저밀도교통Fingerprint.ComputeLaneGraphHash(snapshot.lanes),
                Is.EqualTo(snapshot.laneGraphHash)
                    .And.EqualTo(사가정저밀도교통Fixture.LaneGraphHash));
            Assert.That(사가정저밀도교통Fingerprint
                    .ComputeCourierDisplayRouteFingerprint(snapshot),
                Is.EqualTo(snapshot.courierDisplayRouteFingerprint)
                    .And.EqualTo(
                        "EAB858B640F90E4EF2E1832B9D3C3D22B096B2F202D8A3AC04D03FEAB8DC7475"));
            Assert.That(snapshot.courierRouteState.totalDistanceMeters,
                Is.EqualTo(277.277387734062d).Within(.000001d));
            Assert.That(snapshot.sourceAccessReviewCode,
                Is.EqualTo(사가정저밀도교통WireCodes.PendingHumanReview));
            Assert.That(snapshot.sourceDirectionAuthorityCode,
                Is.EqualTo(사가정저밀도교통WireCodes.UnknownDirection));
            Assert.That(snapshot.sourceRuntimeAuthorized, Is.False);
            Assert.That(snapshot.operationalBindingReady, Is.False);
            Assert.That(snapshot.canonicalDeliveryMutationAllowed, Is.False);
        }

        [Test]
        public void 엄격한_interpreter는_대로_생활도로_골목_후보종점을_순서대로_수용한다()
        {
            var snapshots = JourneySnapshots();
            var interpreter = new 사가정저밀도교통Interpreter();
            var previous = -1d;

            for (var index = 0; index < snapshots.Length; index++)
            {
                var snapshot = snapshots[index];
                Assert.That(interpreter.TryInterpret(snapshot, Context(snapshot),
                    out var model, out var failure), Is.True, failure);
                Assert.That(model.CourierRouteState.CurrentRouteLegIndex,
                    Is.EqualTo(Math.Min(index, 2)));
                Assert.That(model.CourierRouteState.CumulativeProgressMeters,
                    Is.GreaterThanOrEqualTo(previous));
                Assert.That(model.CourierRouteState.TotalDistanceMeters,
                    Is.EqualTo(사가정저밀도교통Fixture
                        .TotalDisplayRouteDistanceMeters).Within(.000001d));
                Assert.That(model.Lanes, Has.Count.EqualTo(4));
                Assert.That(model.CourierRouteLegs.SelectMany(value => value.SourceEdges)
                    .All(value => !value.RuntimeAuthorized), Is.True);
                Assert.That(model.Actors.Single(value => value.StableId ==
                        사가정저밀도교통Fixture.CourierActorId).LaneStableId,
                    Is.EqualTo(model.CourierRouteState.CurrentLaneStableId));
                previous = model.CourierRouteState.CumulativeProgressMeters;
            }

            Assert.That(snapshots[^1].courierRouteState.candidateEndpointReached, Is.True);
            Assert.That(Courier(snapshots[^1]).stateCode,
                Is.EqualTo(사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint));
        }

        [TestCase("sourceGraph", 사가정저밀도교통FailureCodes.SourceMobilityGraphMismatch)]
        [TestCase("sourceRuntime", 사가정저밀도교통FailureCodes.SourceAuthorityBoundaryInvalid)]
        [TestCase("binding", 사가정저밀도교통FailureCodes.OperationalBindingForbidden)]
        [TestCase("mutation", 사가정저밀도교통FailureCodes.OperationalBindingForbidden)]
        public void 출처와_운영비권위_경계를_벗어난_사본은_거부한다(
            string mutation, string expected)
        {
            var snapshot = Snapshot(40);
            switch (mutation)
            {
                case "sourceGraph": snapshot.sourceMobilityGraphRevision += ".tampered"; break;
                case "sourceRuntime": snapshot.sourceRuntimeAuthorized = true; break;
                case "binding": snapshot.operationalBindingReady = true; break;
                case "mutation": snapshot.canonicalDeliveryMutationAllowed = true; break;
            }

            AssertRejected(snapshot, Context(Snapshot(40)), expected);
        }

        [Test]
        public void 수신_lane_좌표변조와_lane권위승격은_각각_거부한다()
        {
            var changedPoint = Snapshot(40);
            changedPoint.lanes[0].points[0].x += .25d;
            AssertRejected(changedPoint, Context(Snapshot(40)),
                사가정저밀도교통FailureCodes.LaneGraphHashMismatch);

            var authorized = Snapshot(40);
            authorized.lanes[2].sourceRuntimeAuthorized = true;
            AssertRejected(authorized, Context(Snapshot(40)),
                사가정저밀도교통FailureCodes.LaneStateInvalid);
        }

        [TestCase("edgeOrder")]
        [TestCase("edgeAuthority")]
        [TestCase("edgeContinuity")]
        [TestCase("legLane")]
        public void 표시경로_leg와_source_edge_변조는_거부한다(string mutation)
        {
            var snapshot = Snapshot(40);
            var edge = snapshot.courierRouteLegs[1].sourceEdges[1];
            switch (mutation)
            {
                case "edgeOrder": edge.displayGeometryOrderCode = "CanonicalForward"; break;
                case "edgeAuthority": edge.runtimeAuthorized = true; break;
                case "edgeContinuity": edge.displayFrom.x += 1d; break;
                case "legLane": snapshot.courierRouteLegs[1].laneStableId =
                    사가정저밀도교통WireCodes.AlleyCandidateLane; break;
            }

            AssertRejected(snapshot, Context(Snapshot(40)),
                사가정저밀도교통FailureCodes.CourierRouteLegInvalid);
        }

        [Test]
        public void route_state와_courier_actor의_lane_progress가_다르면_거부한다()
        {
            var snapshot = Snapshot(40);
            var courier = Courier(snapshot);
            var lane = snapshot.lanes.Single(value => value.stableId == courier.laneStableId);
            courier.progressMeters += .125d;
            Resample(courier, lane);

            AssertRejected(snapshot, Context(Snapshot(40)),
                사가정저밀도교통FailureCodes.CourierActorRouteStateMismatch);
        }

        [Test]
        public void 더_높은_revision이라도_누적표시진행이_되감기면_거부한다()
        {
            FindIncreasingCourierPair(out var low, out var high);
            var interpreter = new 사가정저밀도교통Interpreter();
            Assert.That(interpreter.TryInterpret(high, Context(high), out _, out var failure),
                Is.True, failure);

            var forged = Snapshot(high.simulationTick + 1);
            CopyCourierRouteState(low, forged);
            Assert.That(interpreter.TryInterpret(forged, Context(forged),
                out var model, out failure), Is.False);
            Assert.That(model, Is.Null);
            Assert.That(failure,
                Is.EqualTo(사가정저밀도교통FailureCodes.CourierRouteProgressRegression));
        }

        [Test]
        public void Playback은_수신_lane과_세_leg를_그리고_전_여정에_같은_courier를_재사용한다()
        {
            var snapshots = JourneySnapshots();
            var interpreter = new 사가정저밀도교통Interpreter();
            var models = snapshots.Select(snapshot =>
            {
                Assert.That(interpreter.TryInterpret(snapshot, Context(snapshot),
                    out var model, out var failure), Is.True, failure);
                return model;
            }).ToArray();
            var host = new GameObject("sagajeong-v2-playback-test");
            var root = new GameObject("traffic-root").transform;
            root.SetParent(host.transform, false);
            var layer = host.AddComponent<사가정저밀도교통PlaybackLayer>();
            try
            {
                layer.Configure(null, root, poolCapacity: 4);
                layer.Apply(models[0]);
                Assert.That(layer.StaticGeometryReady, Is.True);
                Assert.That(layer.ObservationLineCount, Is.EqualTo(4));
                Assert.That(layer.SyntheticLaneLineCount, Is.EqualTo(4));
                Assert.That(layer.CourierRouteLineCount, Is.EqualTo(3));
                Assert.That(layer.CandidateEndpointMarkerVisible, Is.True);
                Assert.That(layer.TryGetActorVisualState(
                    사가정저밀도교통Fixture.CourierActorId, out var first), Is.True);
                var instanceId = first.VisualInstanceId;

                foreach (var model in models.Skip(1))
                {
                    layer.Apply(model);
                    Assert.That(layer.TryGetActorVisualState(
                        사가정저밀도교통Fixture.CourierActorId, out var next), Is.True);
                    Assert.That(next.VisualInstanceId, Is.EqualTo(instanceId));
                    var courier = model.Actors.Single(value => value.StableId ==
                        사가정저밀도교통Fixture.CourierActorId);
                    Assert.That(next.LocalPosition.x,
                        Is.EqualTo((float)courier.Position.X).Within(.0001f));
                    Assert.That(next.LocalPosition.z,
                        Is.EqualTo((float)courier.Position.Z).Within(.0001f));
                }

                Assert.That(layer.CandidateEndpointReached, Is.True);
                Assert.That(layer.CourierRouteCumulativeProgressMeters,
                    Is.EqualTo(layer.CourierRouteTotalDistanceMeters).Within(.000001d));
                Assert.That(layer.TryGetRouteLegVisualState(
                    사가정저밀도교통WireCodes.AlleyFinalRouteLeg, out var final), Is.True);
                Assert.That(final.StateCode, Is.EqualTo("CandidateEndpointReached"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void 비활성Playback에_상태사본을적용해도_표현Root는새어나오지않는다()
        {
            var snapshot = Snapshot();
            var interpreter = new 사가정저밀도교통Interpreter();
            Assert.That(interpreter.TryInterpret(snapshot, Context(snapshot),
                out var model, out var failure), Is.True, failure);
            var host = new GameObject("sagajeong-disabled-playback-test");
            var root = new GameObject("traffic-root").transform;
            root.SetParent(host.transform, false);
            var layer = host.AddComponent<사가정저밀도교통PlaybackLayer>();
            try
            {
                layer.Configure(null, root, poolCapacity: 4);
                layer.enabled = false;
                layer.Apply(model);

                var staticRoot = root.Find("정적 교통 관찰과 합성 차로");
                var dynamicRoot = root.Find("동적 저밀도 합성 교통");
                Assert.That(staticRoot, Is.Not.Null);
                Assert.That(dynamicRoot, Is.Not.Null);
                Assert.That(staticRoot.gameObject.activeSelf, Is.False);
                Assert.That(dynamicRoot.gameObject.activeSelf, Is.False);
                Assert.That(layer.AppliedRevision, Is.EqualTo(model.Revision));

                layer.enabled = true;
                layer.Apply(model);
                Assert.That(staticRoot.gameObject.activeSelf, Is.True);
                Assert.That(dynamicRoot.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void tick0부터400까지_모든_상태사본을_순서대로_해석하고_대기는정지한다()
        {
            var interpreter = new 사가정저밀도교통Interpreter();
            var previousProgress = 0d;
            for (var tick = 0L; tick <= 400L; tick++)
            {
                var snapshot = Snapshot(tick);
                Assert.That(interpreter.TryInterpret(snapshot, Context(snapshot),
                    out var model, out var failure), Is.True,
                    $"tick={tick}; failure={failure}");
                Assert.That(model.CourierRouteState.CumulativeProgressMeters,
                    Is.GreaterThanOrEqualTo(previousProgress - .000001d),
                    $"tick={tick}");
                Assert.That(model.SourceRuntimeAuthorized, Is.False);
                Assert.That(snapshot.traversalReady, Is.False);
                Assert.That(snapshot.gameplayReady, Is.False);
                Assert.That(model.OperationalBindingReady, Is.False);
                Assert.That(model.CanonicalDeliveryMutationAllowed, Is.False);
                Assert.That(model.Lanes.All(lane => !lane.SourceRuntimeAuthorized),
                    Is.True);
                Assert.That(model.CourierRouteLegs.SelectMany(leg => leg.SourceEdges)
                    .All(edge => !edge.RuntimeAuthorized), Is.True);

                foreach (var actor in model.Actors.Where(actor =>
                             actor.StateCode ==
                             사가정저밀도교통WireCodes.WaitingAtSignal
                             || actor.StateCode == 사가정저밀도교통WireCodes.Queueing
                             || actor.StateCode ==
                             사가정저밀도교통WireCodes.WaitingForAlley
                             || actor.StateCode ==
                             사가정저밀도교통WireCodes.StoppedAtCandidateEndpoint))
                    Assert.That(actor.SpeedMetersPerSecond, Is.EqualTo(0d),
                        $"tick={tick}; actor={actor.StableId}; state={actor.StateCode}");

                previousProgress = model.CourierRouteState.CumulativeProgressMeters;
            }
        }

        internal static 사가정저밀도교통SnapshotData ModuleAdapterSnapshot(long tick = 0L)
            => Snapshot(tick);

        internal static 사가정저밀도교통ProjectionContext ModuleAdapterContext(
            사가정저밀도교통SnapshotData snapshot)
            => Context(snapshot);

        private static void AssertRejected(사가정저밀도교통SnapshotData snapshot,
            사가정저밀도교통ProjectionContext context, string expected)
        {
            var interpreter = new 사가정저밀도교통Interpreter();
            Assert.That(interpreter.TryInterpret(snapshot, context,
                out var model, out var failure), Is.False);
            Assert.That(model, Is.Null);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(interpreter.AcceptedRevision, Is.EqualTo(-1L));
        }

        private static 사가정저밀도교통SnapshotData[] JourneySnapshots()
        {
            var result = new List<사가정저밀도교통SnapshotData> { Snapshot(0) };
            for (var tick = 1L; tick <= 600L && result.Count < 4; tick++)
            {
                var candidate = Snapshot(tick);
                if (result.Count == 1 && candidate.courierRouteState.currentRouteLegIndex == 1
                    || result.Count == 2 &&
                    candidate.courierRouteState.currentRouteLegIndex == 2
                    && !candidate.courierRouteState.candidateEndpointReached
                    || result.Count == 3 &&
                    candidate.courierRouteState.candidateEndpointReached)
                    result.Add(candidate);
            }
            Assert.That(result, Has.Count.EqualTo(4), "서버 fixture 여정 전이가 누락되었습니다.");
            return result.ToArray();
        }

        private static void FindIncreasingCourierPair(
            out 사가정저밀도교통SnapshotData low,
            out 사가정저밀도교통SnapshotData high)
        {
            low = Snapshot(0);
            for (var tick = 1L; tick < 300L; tick++)
            {
                high = Snapshot(tick);
                if (high.courierRouteState.currentRouteLegIndex ==
                    low.courierRouteState.currentRouteLegIndex
                    && high.courierRouteState.cumulativeProgressMeters >
                    low.courierRouteState.cumulativeProgressMeters + .01d
                    && Courier(low).stateCode != 사가정저밀도교통WireCodes.Queueing
                    && Courier(low).stateCode != 사가정저밀도교통WireCodes.WaitingAtSignal)
                    return;
                low = high;
            }
            high = null;
            throw new AssertionException("증가하는 courier 상태 쌍을 찾지 못했습니다.");
        }

        private static void CopyCourierRouteState(사가정저밀도교통SnapshotData source,
            사가정저밀도교통SnapshotData destination)
        {
            destination.courierRouteState = Clone(source.courierRouteState);
            var sourceCourier = Courier(source);
            var index = Array.FindIndex(destination.actors, value => value.stableId ==
                사가정저밀도교통Fixture.CourierActorId);
            destination.actors[index] = Clone(sourceCourier);
        }

        private static void Resample(사가정저밀도교통ActorData actor,
            사가정저밀도교통LaneData lane)
        {
            var points = lane.points.Select(value =>
                new 사가정저밀도교통Point(value.x, value.z)).ToArray();
            Assert.That(사가정저밀도교통Fixture.TrySample(points, actor.progressMeters,
                out var position, out var direction), Is.True);
            actor.position = new 사가정저밀도교통PointData
                { x = position.X, z = position.Z };
            var heading = Math.Atan2(direction.X, direction.Z) * 180d / Math.PI;
            actor.headingDegrees = heading < 0d ? heading + 360d : heading;
        }

        private static T Clone<T>(T value)
            => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        private static 사가정저밀도교통ActorData Courier(
            사가정저밀도교통SnapshotData snapshot)
            => snapshot.actors.Single(value => value.stableId ==
                사가정저밀도교통Fixture.CourierActorId);

        private static 사가정저밀도교통ProjectionContext Context(
            사가정저밀도교통SnapshotData snapshot)
            => new(snapshot.stationStableId, snapshot.regionStableId,
                snapshot.administrativeAreaStableId, snapshot.sourceObservationRevision,
                snapshot.sourceMobilityGraphStableId,
                snapshot.sourceMobilityGraphRevision,
                snapshot.sourceMobilityGraphProjectionHashSha256,
                snapshot.sourceMobilityGraphContentHashSha256,
                snapshot.laneGraphRevision, snapshot.laneGraphHash,
                snapshot.signalPlanRevision, snapshot.trafficProfileRevision,
                snapshot.courierJourneyId, snapshot.routeFingerprint,
                snapshot.courierDisplayRouteRevision,
                snapshot.courierDisplayRouteFingerprint,
                snapshot.configurationFingerprint, snapshot.inputStateFingerprint,
                snapshot.scenarioSeed, snapshot.tickSeconds,
                사가정저밀도교통Fixture.EpochUtc, true);

        private static 사가정저밀도교통SnapshotData Snapshot(long tick = 0L)
            => Convert(new Ssalddel.Simulation.Domain.사가정저밀도교통Engine().생성(
                Ssalddel.Simulation.Domain.사가정저밀도교통검증Fixture.만들기(), tick));

        private static 사가정저밀도교통SnapshotData Convert(ServerSnapshot value)
            => new()
            {
                schemaVersion = value.SchemaVersion,
                stationStableId = value.StationStableId,
                regionStableId = value.RegionStableId,
                administrativeAreaStableId = value.AdministrativeAreaStableId,
                sourceKindCode = value.SourceKindCode,
                sourceObservationRevision = value.SourceObservationRevision,
                sourceMobilityGraphStableId = value.SourceMobilityGraphStableId,
                sourceMobilityGraphRevision = value.SourceMobilityGraphRevision,
                sourceMobilityGraphProjectionHashSha256 =
                    value.SourceMobilityGraphProjectionHashSha256,
                sourceMobilityGraphContentHashSha256 =
                    value.SourceMobilityGraphContentHashSha256,
                sourceAccessReviewCode = value.SourceAccessReviewCode,
                sourceDirectionAuthorityCode = value.SourceDirectionAuthorityCode,
                sourceRuntimeAuthorized = value.SourceRuntimeAuthorized,
                laneGraphRevision = value.LaneGraphRevision,
                laneGraphHash = value.LaneGraphHash,
                signalPlanRevision = value.SignalPlanRevision,
                trafficProfileRevision = value.TrafficProfileRevision,
                courierJourneyId = value.CourierJourneyId,
                routeFingerprint = value.RouteFingerprint,
                courierDisplayRouteRevision = value.CourierDisplayRouteRevision,
                courierDisplayRouteFingerprint = value.CourierDisplayRouteFingerprint,
                configurationFingerprint = value.ConfigurationFingerprint,
                inputStateFingerprint = value.InputStateFingerprint,
                scenarioSeed = value.ScenarioSeed,
                simulationTick = value.SimulationTick,
                tickSeconds = value.TickSeconds,
                revision = value.Revision,
                observedAt = value.ObservedAt.ToString("O", CultureInfo.InvariantCulture),
                observationPresentationOnly = value.ObservationPresentationOnly,
                distributionApproved = value.DistributionApproved,
                traversalReady = value.TraversalReady,
                gameplayReady = value.GameplayReady,
                journeyBindingStateCode = value.JourneyBindingStateCode,
                operationalBindingReady = value.OperationalBindingReady,
                canonicalDeliveryMutationAllowed = value.CanonicalDeliveryMutationAllowed,
                readinessCode = value.ReadinessCode,
                signals = value.Signals.Select(signal => new 사가정저밀도교통SignalData
                {
                    stableId = signal.StableId,
                    axisCode = signal.AxisCode,
                    stateCode = signal.StateCode,
                    secondsUntilTransition = signal.SecondsUntilTransition,
                    stopLineStableId = signal.StopLineStableId,
                }).ToArray(),
                lanes = value.Lanes.Select(lane => new 사가정저밀도교통LaneData
                {
                    stableId = lane.StableId,
                    sourceWayStableId = lane.SourceWayStableId,
                    axisCode = lane.AxisCode,
                    routeKindCode = lane.RouteKindCode,
                    sourceDirectionCode = lane.SourceDirectionCode,
                    sourceAccessReviewCode = lane.SourceAccessReviewCode,
                    sourceRuntimeAuthorized = lane.SourceRuntimeAuthorized,
                    hasStopLine = lane.HasStopLine,
                    stopLineProgressMeters = lane.StopLineProgressMeters,
                    pingPong = lane.PingPong,
                    points = lane.Points.Select(Point).ToArray(),
                }).ToArray(),
                actors = value.Actors.Select(actor => new 사가정저밀도교통ActorData
                {
                    stableId = actor.StableId,
                    kindCode = actor.KindCode,
                    laneStableId = actor.LaneStableId,
                    routeKindCode = actor.RouteKindCode,
                    stateCode = actor.StateCode,
                    displayPolylineOrderCode = actor.DisplayPolylineOrderCode,
                    progressMeters = actor.ProgressMeters,
                    speedMetersPerSecond = actor.SpeedMetersPerSecond,
                    queueIndex = actor.QueueIndex,
                    blockedReasonCode = actor.BlockedReasonCode,
                    position = Point(actor.Position),
                    headingDegrees = actor.HeadingDegrees,
                }).ToArray(),
                courierRouteLegs = value.CourierRouteLegs.Select(leg =>
                    new 사가정저밀도교통CourierRouteLegData
                    {
                        stableId = leg.StableId,
                        index = leg.Index,
                        laneStableId = leg.LaneStableId,
                        stageCode = leg.StageCode,
                        displayGeometryDistanceMeters = leg.DisplayGeometryDistanceMeters,
                        sourceDistanceMeters = leg.SourceDistanceMeters,
                        sourceEdges = leg.SourceEdges.Select(edge =>
                            new 사가정저밀도교통SourceEdgeData
                            {
                                edgeStableId = edge.EdgeStableId,
                                displayGeometryOrderCode = edge.DisplayGeometryOrderCode,
                                sourceDirectionCode = edge.SourceDirectionCode,
                                accessReviewCode = edge.AccessReviewCode,
                                runtimeAuthorized = edge.RuntimeAuthorized,
                                sourceDistanceMeters = edge.SourceDistanceMeters,
                                displayFrom = Point(edge.DisplayFrom),
                                displayTo = Point(edge.DisplayTo),
                            }).ToArray(),
                    }).ToArray(),
                courierRouteState = new 사가정저밀도교통CourierRouteStateData
                {
                    currentRouteLegStableId = value.CourierRouteState
                        .CurrentRouteLegStableId,
                    currentRouteLegIndex = value.CourierRouteState.CurrentRouteLegIndex,
                    routeLegCount = value.CourierRouteState.RouteLegCount,
                    currentLaneStableId = value.CourierRouteState.CurrentLaneStableId,
                    stageCode = value.CourierRouteState.StageCode,
                    legProgressMeters = value.CourierRouteState.LegProgressMeters,
                    cumulativeProgressMeters = value.CourierRouteState
                        .CumulativeProgressMeters,
                    totalDistanceMeters = value.CourierRouteState.TotalDistanceMeters,
                    candidateEndpointReached = value.CourierRouteState
                        .CandidateEndpointReached,
                },
            };

        private static 사가정저밀도교통PointData Point(
            Ssalddel.Simulation.Contracts.사가정저밀도교통Point value)
            => new() { x = value.X, z = value.Z };
    }
}
