using System;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 음식배달JourneyMobilityTests
    {
        private const string Region = "world-region:kr:seoul:jungnang:sagajeong.r1";
        private const string AdministrativeArea = "region:kr:hjd:1126057500";
        private const string GraphRevision = "sagajeong-delivery-graph.r1";
        private const string GraphHash =
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

        [Test]
        public void JsonDecoder는_v1의_필수필드와_문자열모드를_보존한다()
        {
            var json = "{"
                       + "\"schemaVersion\":\"food-delivery-journey-snapshot.v1\","
                       + "\"regionStableId\":\"" + Region + "\","
                       + "\"administrativeAreaStableId\":\"" + AdministrativeArea + "\","
                       + "\"sourceKindCode\":\"SyntheticFixture\","
                       + "\"orderStableId\":\"order:synthetic:a\",\"actorStableId\":\"actor:synthetic:a\","
                       + "\"vehicleStableId\":\"vehicle:synthetic:a\",\"routeStableId\":\"route:synthetic:a\","
                       + "\"graphRevision\":\"" + GraphRevision + "\","
                       + "\"graphHash\":\"" + GraphHash + "\","
                       + "\"routeFingerprint\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\","
                       + "\"revision\":1,\"observedAt\":\"2026-09-13T01:00:00Z\","
                       + "\"blocked\":false,\"distributionApproved\":false,"
                       + "\"readinessCode\":\"Ready\",\"currentLegIndex\":0,"
                       + "\"legs\":[{\"stableId\":\"leg:synthetic:a\",\"mode\":\"Motorcycle\","
                       + "\"stateCode\":\"Active\",\"distanceMeters\":10,\"progressMeters\":2,"
                       + "\"points\":[{\"x\":0,\"z\":0},{\"x\":10,\"z\":0}],"
                       + "\"actorPosition\":{\"x\":2,\"z\":0},\"vehiclePosition\":null}]}";

            var decoded = 음식배달JourneySnapshotJsonDecoder.Decode(json);

            Assert.That(decoded.schemaVersion, Is.EqualTo(음식배달JourneyWireCodes.SchemaVersion));
            Assert.That(decoded.regionStableId, Is.EqualTo(Region));
            Assert.That(decoded.sourceKindCode,
                Is.EqualTo(음식배달JourneyWireCodes.SyntheticFixture));
            Assert.That(decoded.legs[0].mode, Is.EqualTo(음식배달JourneyWireCodes.Motorcycle));
            Assert.Throws<FormatException>(() =>
                음식배달JourneySnapshotJsonDecoder.Decode("{\"schemaVersion\":\"food-delivery-journey-snapshot.v1\"}"));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("\"revision\":1", "\"revision\":\"1\"")));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("\"blocked\":false", "\"blocked\":\"false\"")));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("\"mode\":\"Motorcycle\"", "\"mode\":2")));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("\"x\":0", "\"x\":\"0\"")));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("{\"schemaVersion\"", "{\"unknownRoot\":0,\"schemaVersion\"")));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("\"legs\":[{\"stableId\"",
                    "\"legs\":[{\"unknownLeg\":0,\"stableId\"")));
            Assert.Throws<FormatException>(() => 음식배달JourneySnapshotJsonDecoder.Decode(
                json.Replace("\"points\":[{\"x\"",
                    "\"points\":[{\"unknownPoint\":0,\"x\"")));
            Assert.Throws<FormatException>(() =>
                음식배달JourneySnapshotJsonDecoder.Decode(json + "{}"));
        }

        [Test]
        public void 표시좌표는_양끝직선이_아니라_polyline누적거리를_따른다()
        {
            var snapshot = Snapshot();
            snapshot.legs[0].progressMeters = 15;
            snapshot.legs[0].actorPosition = Point(10, 5);
            snapshot.legs[0].vehiclePosition = Point(10, 5);
            Seal(snapshot);

            var accepted = new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out var model, out var failure);

            Assert.That(accepted, Is.True, failure);
            Assert.That(model.CurrentPosition.X, Is.EqualTo(10d).Within(.0001d));
            Assert.That(model.CurrentPosition.Z, Is.EqualTo(5d).Within(.0001d));
            Assert.That(model.ObservedActorPosition.X, Is.EqualTo(10d));
            Assert.That(model.ObservedActorPosition.Z, Is.EqualTo(5d));
        }

        [Test]
        public void 실제_enum숫자나_소문자모드를_추측하지_않고_거부한다()
        {
            var snapshot = Snapshot();
            snapshot.legs[0].mode = "vehicle";
            Seal(snapshot);

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.LegModeUnknown));
        }

        [Test]
        public void leg상태는_서버의_네_문자열만_허용한다()
        {
            var snapshot = Snapshot();
            snapshot.legs[0].stateCode = "MovingToRestaurant";
            Seal(snapshot);

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.LegStateUnknown));
        }

        [Test]
        public void 비공개_preview와_region은_명시적_context에만_결속된다()
        {
            var snapshot = Snapshot();
            Seal(snapshot);
            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(), out _, out var privateFailure), Is.False);
            Assert.That(privateFailure,
                Is.EqualTo(음식배달JourneyFailureCodes.DistributionNotApproved));

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out var model, out var failure),
                Is.True, failure);
            Assert.That(model.RegionStableId, Is.EqualTo(Region));
            Assert.That(model.DistributionApproved, Is.False);

            var otherRegion = new 음식배달JourneyProjectionContext(
                "world-region:other", AdministrativeArea, GraphRevision, GraphHash,
                allowPrivatePreview: true);
            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, otherRegion, out _, out var regionFailure), Is.False);
            Assert.That(regionFailure, Is.EqualTo(음식배달JourneyFailureCodes.RegionMismatch));
        }

        [TestCase("source")]
        [TestCase("distribution")]
        [TestCase("revision")]
        [TestCase("observed-at")]
        [TestCase("observed-at-no-offset")]
        public void PhaseA는_synthetic_양수revision_UTC관측만_허용한다(string kind)
        {
            var snapshot = Snapshot();
            switch (kind)
            {
                case "source": snapshot.sourceKindCode = "PublicOpenData"; break;
                case "distribution": snapshot.distributionApproved = true; break;
                case "revision": snapshot.revision = 0; break;
                case "observed-at": snapshot.observedAt = "2026-09-13T10:00:00+09:00"; break;
                case "observed-at-no-offset": snapshot.observedAt = "2026-09-13T01:00:00"; break;
            }
            Seal(snapshot);

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out _, out var failure), Is.False);
            if (kind == "distribution")
                Assert.That(failure,
                    Is.EqualTo(음식배달JourneyFailureCodes.DistributionForbidden));
        }

        [TestCase("distance")]
        [TestCase("actor")]
        [TestCase("vehicle")]
        public void 경로거리_actor_vehicle이_polyline과_다르면_거부한다(string kind)
        {
            var snapshot = Snapshot();
            switch (kind)
            {
                case "distance": snapshot.legs[0].distanceMeters = 21; break;
                case "actor": snapshot.legs[0].actorPosition = Point(5, 5); break;
                case "vehicle": snapshot.legs[0].vehiclePosition = Point(5, 5); break;
            }
            Seal(snapshot);

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out _, out _), Is.False);
        }

        [Test]
        public void 그래프hash와_경로fingerprint_불일치를_각각_거부한다()
        {
            var snapshot = Snapshot();
            Seal(snapshot);
            var otherContext = new 음식배달JourneyProjectionContext(
                Region, AdministrativeArea, GraphRevision, new string('F', 64),
                allowPrivatePreview: true);
            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, otherContext, out _, out var graphFailure), Is.False);
            Assert.That(graphFailure, Is.EqualTo(음식배달JourneyFailureCodes.GraphHashMismatch));

            snapshot.routeFingerprint = new string('A', 64);
            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out _, out var routeFailure), Is.False);
            Assert.That(routeFailure, Is.EqualTo(음식배달JourneyFailureCodes.RouteFingerprintMismatch));
        }

        [Test]
        public void 경로fingerprint는_서버와_같은_대문자_SHA256만_허용한다()
        {
            var snapshot = Snapshot();
            Seal(snapshot);
            snapshot.routeFingerprint = snapshot.routeFingerprint.ToLowerInvariant();

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.RouteFingerprintInvalid));
        }

        [Test]
        public void 경로fingerprint는_서버_BinaryWriter_고정벡터와_같다()
        {
            var snapshot = new 음식배달JourneySnapshotData
            {
                routeStableId = "synthetic:route:test:1",
                graphRevision = "graph.r1",
                graphHash = new string('A', 64),
                legs = new[]
                {
                    new 음식배달JourneyLegData
                    {
                        stableId = "synthetic:leg:1",
                        mode = 음식배달JourneyWireCodes.Motorcycle,
                        distanceMeters = 5,
                        points = new[] { Point(0, 0), Point(3, 4) },
                    },
                },
            };

            Assert.That(음식배달JourneyFingerprint.Compute(snapshot),
                Is.EqualTo("1C3CB8A4F6E6E95C6F0FFA0B991703E22D9FC48E9D98939FEC8A067E2E35977C"));
        }

        [TestCase("blocked")]
        [TestCase("unresolved")]
        [TestCase("not-ready")]
        [TestCase("no-points")]
        public void 비공개_차단_미해결_경로누락은_직선fallback없이_거부한다(string kind)
        {
            var snapshot = Snapshot();
            switch (kind)
            {
                case "blocked": snapshot.blocked = true; break;
                case "unresolved": snapshot.readinessCode = "RouteUnresolved"; break;
                case "not-ready": snapshot.readinessCode = "WaitingForTraversalGraph"; break;
                case "no-points": snapshot.legs[0].points = Array.Empty<음식배달JourneyPointData>(); break;
            }
            Seal(snapshot);

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true),
                out var model, out _), Is.False);
            Assert.That(model, Is.Null);
        }

        [Test]
        public void 낮은_revision을_거부하고_지역전환_Reset후에만_다시_받는다()
        {
            var interpreter = new 음식배달JourneyInterpreter();
            var latest = Snapshot(5);
            Seal(latest);
            Assert.That(interpreter.TryInterpret(latest, Context(allowPrivatePreview: true), out _, out _), Is.True);

            var stale = Snapshot(4);
            Seal(stale);
            Assert.That(interpreter.TryInterpret(stale, Context(allowPrivatePreview: true), out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.StaleRevision));

            interpreter.Reset();
            Assert.That(interpreter.AcceptedRevision, Is.EqualTo(-1));
            Assert.That(interpreter.TryInterpret(stale, Context(allowPrivatePreview: true), out _, out failure), Is.True, failure);
        }

        [Test]
        public void 여러_route가_교차수신되어도_각_route의_stale_revision을_기억한다()
        {
            var interpreter = new 음식배달JourneyInterpreter();
            var routeA = Snapshot(5);
            Seal(routeA);
            Assert.That(interpreter.TryInterpret(routeA, Context(allowPrivatePreview: true),
                out _, out _), Is.True);

            var routeB = Snapshot(1);
            routeB.routeStableId = "route:synthetic:food-delivery:b";
            routeB.orderStableId = "order:synthetic:food-delivery:b";
            Seal(routeB);
            Assert.That(interpreter.TryInterpret(routeB, Context(allowPrivatePreview: true),
                out _, out _), Is.True);
            Assert.That(interpreter.AcceptedRevision, Is.EqualTo(5));

            var staleA = Snapshot(4);
            Seal(staleA);
            Assert.That(interpreter.TryInterpret(staleA, Context(allowPrivatePreview: true),
                out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.StaleRevision));
        }

        [TestCase("order")]
        [TestCase("actor")]
        [TestCase("vehicle")]
        public void 같은_revision은_동일상태만_멱등허용하고_높은revision의_identity교체도_거부한다(
            string identityKind)
        {
            var interpreter = new 음식배달JourneyInterpreter();
            var first = Snapshot(7);
            Seal(first);
            Assert.That(interpreter.TryInterpret(first, Context(allowPrivatePreview: true),
                out _, out _), Is.True);

            var same = Snapshot(7);
            Seal(same);
            Assert.That(interpreter.TryInterpret(same, Context(allowPrivatePreview: true),
                out _, out var failure), Is.True, failure);

            var conflict = Snapshot(7);
            conflict.legs[0].progressMeters = 15;
            conflict.legs[0].actorPosition = Point(10, 5);
            conflict.legs[0].vehiclePosition = Point(10, 5);
            Seal(conflict);
            Assert.That(interpreter.TryInterpret(conflict, Context(allowPrivatePreview: true),
                out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.RevisionConflict));

            var identityChanged = Snapshot(8);
            switch (identityKind)
            {
                case "order": identityChanged.orderStableId = "order:synthetic:other"; break;
                case "actor": identityChanged.actorStableId = "actor:synthetic:courier:other"; break;
                case "vehicle": identityChanged.vehicleStableId = "vehicle:synthetic:other"; break;
            }
            Seal(identityChanged);
            Assert.That(interpreter.TryInterpret(identityChanged,
                Context(allowPrivatePreview: true), out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.IdentityMismatch));
        }

        [Test]
        public void 서버유효_unresolved_blocked조합도_표시하지_않고_정확히_분류한다()
        {
            var snapshot = Snapshot();
            snapshot.blocked = true;
            snapshot.readinessCode = "RouteUnresolved";
            snapshot.legs[0].stateCode = 음식배달JourneyWireCodes.Blocked;
            snapshot.legs[0].distanceMeters = 0;
            snapshot.legs[0].progressMeters = 0;
            snapshot.legs[0].points = Array.Empty<음식배달JourneyPointData>();
            snapshot.legs[0].actorPosition = Point(10, 0);
            snapshot.legs[0].vehiclePosition = Point(10, 0);
            Seal(snapshot);

            Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                snapshot, Context(allowPrivatePreview: true), out var model, out var failure),
                Is.False);
            Assert.That(model, Is.Null);
            Assert.That(failure, Is.EqualTo(음식배달JourneyFailureCodes.RouteUnresolved));
        }

        [Test]
        public void 전체뷰기호와_확대뷰의_스쿠터도보인계를_표현하고_actor를_pooling한다()
        {
            var host = new GameObject("delivery-mobility-test");
            var mobility = new GameObject("Mobility").transform;
            mobility.SetParent(host.transform, false);
            var cameraObject = new GameObject("observation-camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            var layer = host.AddComponent<음식배달JourneyPlaybackLayer>();
            try
            {
                layer.Configure(camera, mobility, perspectiveDistance: 70f, poolCapacity: 1);
                Assert.Throws<InvalidOperationException>(() => layer.Configure(camera, mobility));
                var snapshot = Snapshot();
                snapshot.currentLegIndex = 1;
                snapshot.legs[0].stateCode = 음식배달JourneyWireCodes.Completed;
                snapshot.legs[0].progressMeters = 20;
                snapshot.legs[0].actorPosition = Point(10, 10);
                snapshot.legs[0].vehiclePosition = Point(10, 10);
                snapshot.legs[1].stateCode = 음식배달JourneyWireCodes.Active;
                snapshot.legs[1].progressMeters = 1;
                snapshot.legs[1].actorPosition = Point(11, 10);
                Seal(snapshot);
                Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                    snapshot, Context(allowPrivatePreview: true), out var model, out var failure), Is.True, failure);

                layer.Apply(model);
                layer.RefreshVisibilityForTests(500f);
                Assert.That(layer.TryGetVisualState(model.RouteStableId, out var overview), Is.True);
                Assert.That(overview.OverviewMarkerVisible, Is.True);
                Assert.That(overview.ActorVisible, Is.False);
                Assert.That(overview.VehicleVisible, Is.False);

                layer.RefreshVisibilityForTests(90f);
                Assert.That(layer.TryGetVisualState(model.RouteStableId, out var close), Is.True);
                Assert.That(close.OverviewMarkerVisible, Is.False);
                Assert.That(close.ActorVisible, Is.True);
                Assert.That(close.VehicleVisible, Is.True);
                Assert.That(close.VehiclePosition.x, Is.EqualTo(10f).Within(.001f));
                Assert.That(close.VehiclePosition.z, Is.EqualTo(10f).Within(.001f));

                camera.orthographic = false;
                camera.transform.position = mobility.TransformPoint(new Vector3(11, 20, 10));
                layer.RefreshVisibilityFromCameraForTests();
                Assert.That(layer.TryGetVisualState(model.RouteStableId, out var perspectiveClose), Is.True);
                Assert.That(perspectiveClose.ActorVisible, Is.True);
                camera.transform.position = mobility.TransformPoint(new Vector3(11, 100, 10));
                layer.RefreshVisibilityFromCameraForTests();
                Assert.That(layer.TryGetVisualState(model.RouteStableId, out var perspectiveFar), Is.True);
                Assert.That(perspectiveFar.OverviewMarkerVisible, Is.True);

                camera.transform.position = mobility.TransformPoint(new Vector3(11, 20, 10));
                layer.RefreshVisibilityFromCameraForTests();

                layer.SetSelectedRoute(model.RouteStableId);
                Assert.That(layer.RouteLinePositionCount, Is.EqualTo(4));
                Assert.That(layer.RoutePointGameObjectCount, Is.Zero);
                Assert.That(mobility.childCount, Is.EqualTo(2));
                InvokeLifecycle(layer, "OnDisable");
                Assert.That(layer.TryGetVisualState(model.RouteStableId, out var disabled), Is.True);
                Assert.That(disabled.OverviewMarkerVisible, Is.False);
                Assert.That(disabled.ActorVisible, Is.False);
                Assert.That(disabled.VehicleVisible, Is.False);
                Assert.That(layer.SelectedRouteVisible, Is.False);
                InvokeLifecycle(layer, "OnEnable");
                Assert.That(layer.TryGetVisualState(model.RouteStableId, out var enabled), Is.True);
                Assert.That(enabled.ActorVisible, Is.True);
                Assert.That(layer.SelectedRouteVisible, Is.True);

                Assert.That(layer.Remove(model.RouteStableId), Is.True);
                Assert.That(layer.ActiveVisualCount, Is.Zero);
                Assert.That(layer.PooledVisualCount, Is.EqualTo(1));
                Assert.That(layer.RouteLinePositionCount, Is.Zero);

                var next = Snapshot();
                next.routeStableId = "route:synthetic:food-delivery:b";
                next.orderStableId = "order:synthetic:food-delivery:b";
                Seal(next);
                Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                    next, Context(allowPrivatePreview: true), out var nextModel, out failure), Is.True, failure);
                layer.Apply(nextModel);
                Assert.That(layer.CreatedVisualCount, Is.EqualTo(1));
                Assert.That(layer.PooledVisualCount, Is.Zero);
                var third = Snapshot();
                third.routeStableId = "route:synthetic:food-delivery:c";
                third.orderStableId = "order:synthetic:food-delivery:c";
                Seal(third);
                Assert.That(new 음식배달JourneyInterpreter().TryInterpret(
                    third, Context(allowPrivatePreview: true), out var thirdModel, out failure),
                    Is.True, failure);
                layer.Apply(thirdModel);
                Assert.That(layer.CreatedVisualCount, Is.EqualTo(2));
                layer.ClearAll();
                Assert.That(layer.ActiveVisualCount, Is.Zero);
                Assert.That(layer.PooledVisualCount, Is.EqualTo(1));
                layer.ClearPool();
                Assert.That(layer.PooledVisualCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static 음식배달JourneyProjectionContext Context(bool allowPrivatePreview = false)
            => new(Region, AdministrativeArea, GraphRevision, GraphHash, allowPrivatePreview);

        private static 음식배달JourneySnapshotData Snapshot(long revision = 1)
            => new()
            {
                schemaVersion = 음식배달JourneyWireCodes.SchemaVersion,
                regionStableId = Region,
                administrativeAreaStableId = AdministrativeArea,
                sourceKindCode = 음식배달JourneyWireCodes.SyntheticFixture,
                orderStableId = "order:synthetic:food-delivery:a",
                actorStableId = "actor:synthetic:courier:a",
                vehicleStableId = "vehicle:synthetic:scooter:a",
                routeStableId = "route:synthetic:food-delivery:a",
                graphRevision = GraphRevision,
                graphHash = GraphHash,
                revision = revision,
                observedAt = "2026-09-13T01:00:00Z",
                blocked = false,
                distributionApproved = false,
                readinessCode = 음식배달JourneyWireCodes.Ready,
                currentLegIndex = 0,
                legs = new[]
                {
                    new 음식배달JourneyLegData
                    {
                        stableId = "leg:synthetic:food-delivery:a:vehicle",
                        mode = 음식배달JourneyWireCodes.Motorcycle,
                        stateCode = 음식배달JourneyWireCodes.Active,
                        distanceMeters = 20,
                        progressMeters = 10,
                        points = new[] { Point(0, 0), Point(10, 0), Point(10, 10) },
                        actorPosition = Point(10, 0),
                        vehiclePosition = Point(10, 0),
                    },
                    new 음식배달JourneyLegData
                    {
                        stableId = "leg:synthetic:food-delivery:a:pedestrian",
                        mode = 음식배달JourneyWireCodes.Pedestrian,
                        stateCode = 음식배달JourneyWireCodes.Pending,
                        distanceMeters = 2,
                        progressMeters = 0,
                        points = new[] { Point(10, 10), Point(12, 10) },
                        actorPosition = Point(10, 10),
                        vehiclePosition = Point(10, 10),
                    },
                },
            };

        private static void Seal(음식배달JourneySnapshotData snapshot)
            => snapshot.routeFingerprint = 음식배달JourneyFingerprint.Compute(snapshot);

        private static void InvokeLifecycle(음식배달JourneyPlaybackLayer layer,
            string methodName)
        {
            var method = typeof(음식배달JourneyPlaybackLayer).GetMethod(methodName,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(layer, null);
        }

        private static 음식배달JourneyPointData Point(double x, double z)
            => new() { x = x, z = z };
    }
}
