using System.Collections.Generic;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정합성Actor관찰Tests
    {
        [Test]
        public void 유효한_관찰사본은_복사본으로_수용하고_지급액을_추정하지않는다()
        {
            var snapshot = Snapshot(7, 12, "픽업 장소로 이동");
            var sourceHash = snapshot.ProjectionFingerprintSha256;
            var interpreter = new 사가정합성Actor관찰Interpreter();

            var result = interpreter.Apply(snapshot);

            Assert.That(result.Accepted, Is.True, result.ErrorCode);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Snapshot, Is.Not.SameAs(snapshot));
            Assert.That(result.Snapshot.SyntheticCompensation.StatusCode,
                Is.EqualTo(사가정합성기사관찰SettlementStatusCodes.PolicyPending));
            Assert.That(result.Snapshot.SyntheticCompensation.BlockReasonCode,
                Is.EqualTo(사가정합성기사관찰SettlementStatusCodes
                    .SimulationSettlementRulePending));
            Assert.That(result.Snapshot.SyntheticCompensation.AmountMinor, Is.Null);
            Assert.That(result.Snapshot.SyntheticCompensation.CurrencyCode, Is.Empty);

            result.Snapshot.CurrentActionDisplayName = "소비자가 변경한 문구";
            result.Snapshot.SyntheticCompensation.AmountMinor = 9999;

            Assert.That(interpreter.Current.CurrentActionDisplayName,
                Is.EqualTo("픽업 장소로 이동"));
            Assert.That(interpreter.Current.SyntheticCompensation.AmountMinor, Is.Null);
            Assert.That(snapshot.SourceSessionRevision, Is.EqualTo(7));
            Assert.That(snapshot.ProjectionFingerprintSha256, Is.EqualTo(sourceHash));
            Assert.That(snapshot.SyntheticCompensation.AmountMinor, Is.Null);
        }

        [Test]
        public void 최신_revision을_수용한_해석기는_stale사본을_거절한다()
        {
            var interpreter = new 사가정합성Actor관찰Interpreter();
            Assert.That(interpreter.Apply(Snapshot(8, 14)).Accepted, Is.True);

            var result = interpreter.Apply(Snapshot(7, 13));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode,
                Is.EqualTo(사가정합성Actor관찰FailureCodes.StaleRevision));
            Assert.That(result.Snapshot.SourceSessionRevision, Is.EqualTo(8));
            Assert.That(interpreter.Current.SourceSessionRevision, Is.EqualTo(8));
        }

        [Test]
        public void 같은_revision의_다른_fingerprint는_충돌로_거절한다()
        {
            var interpreter = new 사가정합성Actor관찰Interpreter();
            var accepted = Snapshot(9, 15, "픽업 장소로 이동");
            var conflict = Snapshot(9, 15, "픽업 장소로 이동");
            conflict.ActorX += 1;
            conflict.ProjectionFingerprintSha256 =
                사가정합성기사관찰Fingerprint.Compute(conflict);
            Assert.That(interpreter.Apply(accepted).Accepted, Is.True);
            Assert.That(conflict.ProjectionFingerprintSha256,
                Is.Not.EqualTo(accepted.ProjectionFingerprintSha256));

            var result = interpreter.Apply(conflict);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode,
                Is.EqualTo(사가정합성Actor관찰FailureCodes.RevisionConflict));
            Assert.That(interpreter.Current.CurrentActionDisplayName,
                Is.EqualTo(accepted.CurrentActionDisplayName));
        }

        [Test]
        public void 서버와_같은_역_단계_주문_revision_tick_경계를_검증한다()
        {
            var wrongStation = Snapshot(11, 20);
            wrongStation.TransitStationStableId = "station:kr:kric:test:other";
            Rehash(wrongStation);
            AssertRejected(wrongStation,
                사가정합성Actor관찰FailureCodes.IdentityInvalid);

            var zeroRevision = Snapshot(0, 20);
            Rehash(zeroRevision);
            AssertRejected(zeroRevision,
                사가정합성Actor관찰FailureCodes.SourceInvalid);

            var wrongAction = Snapshot(11, 20);
            wrongAction.CurrentActionDisplayName = "주문지로 이동";
            Rehash(wrongAction);
            AssertRejected(wrongAction,
                사가정합성Actor관찰FailureCodes.SourceInvalid);

            var operationalOrder = Snapshot(11, 20);
            operationalOrder.CurrentOrderStableId = "food-order:operational:test:1";
            Rehash(operationalOrder);
            AssertRejected(operationalOrder,
                사가정합성Actor관찰FailureCodes.SourceInvalid);

            var interpreter = new 사가정합성Actor관찰Interpreter();
            Assert.That(interpreter.Apply(Snapshot(11, 20)).Accepted, Is.True);
            var staleTick = interpreter.Apply(Snapshot(12, 19));
            Assert.That(staleTick.Accepted, Is.False);
            Assert.That(staleTick.ErrorCode,
                Is.EqualTo(사가정합성Actor관찰FailureCodes.StaleTick));
            Assert.That(interpreter.Current.CurrentTick, Is.EqualTo(20));
        }

        [Test]
        public void 좌표와_camera가_결속되기_전에는_사본을_보존하되_관찰준비로_보고하지않는다()
        {
            var root = new GameObject("사가정 합성 Actor 좌표 결속 시험");
            var cameraRoot = new GameObject("사가정 합성 Actor 좌표 결속 카메라");
            try
            {
                var layer = root.AddComponent<사가정합성Actor관찰Layer>();
                var result = layer.Apply(Snapshot(13, 21));

                Assert.That(result.Accepted, Is.True, result.ErrorCode);
                Assert.That(layer.IsConfigured, Is.False);
                Assert.That(layer.HasAcceptedObservation, Is.False);
                Assert.That(layer.RuntimeVisualCount, Is.Zero);
                Assert.That(layer.SelectCurrentActor(), Is.False);
                Assert.That(layer.StatusCode, Is.EqualTo(
                    사가정합성Actor관찰Layer.WaitingForCoordinateBindingStatus));
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAndMobilityAdapter());
                var adapter = root.AddComponent<사가정LifeSimulationModuleAdapter>();
                adapter.Configure(host, layer);
                var waitingState = adapter.DescribeModuleLayers()[0];
                Assert.That(waitingState.AvailableForObservation, Is.False);
                Assert.That(waitingState.DiagnosticCode, Is.EqualTo(
                    사가정합성Actor관찰Layer.WaitingForCoordinateBindingStatus));

                layer.Configure(cameraRoot.AddComponent<Camera>(), Vector3.zero);
                Assert.That(adapter.RefreshModuleState(), Is.True);

                Assert.That(layer.IsConfigured, Is.True);
                Assert.That(layer.HasAcceptedObservation, Is.True);
                Assert.That(layer.RuntimeVisualCount, Is.EqualTo(1));
                Assert.That(layer.StatusCode,
                    Is.EqualTo(사가정합성Actor관찰Layer.ReadyStatus));
                Assert.That(adapter.DescribeModuleLayers()[0].AvailableForObservation,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void 유효사본뒤_형태오류는_표시를_숨기고_후속_정상_revision이_복구한다()
        {
            var root = new GameObject("사가정 합성 Actor 거절 수명 시험");
            var cameraRoot = new GameObject("사가정 합성 Actor 거절 수명 카메라");
            try
            {
                var layer = root.AddComponent<사가정합성Actor관찰Layer>();
                layer.Configure(cameraRoot.AddComponent<Camera>(), Vector3.zero);
                Assert.That(layer.Apply(Snapshot(14, 22)).Accepted, Is.True);
                Assert.That(layer.SelectCurrentActor(), Is.True);

                var invalid = Snapshot(15, 23);
                invalid.CurrentStageDisplayName = "모순된 단계";
                Rehash(invalid);
                var rejected = layer.Apply(invalid);

                Assert.That(rejected.Accepted, Is.False);
                Assert.That(rejected.ErrorCode,
                    Is.EqualTo(사가정합성Actor관찰FailureCodes.SourceInvalid));
                Assert.That(layer.HasAcceptedObservation, Is.False);
                var runtimeRoot = root.transform.Find(
                    사가정합성Actor관찰Layer.RuntimeRootName);
                Assert.That(runtimeRoot, Is.Not.Null);
                Assert.That(runtimeRoot.gameObject.activeSelf, Is.False);

                var recovered = layer.Apply(Snapshot(16, 24));
                Assert.That(recovered.Accepted, Is.True, recovered.ErrorCode);
                Assert.That(layer.HasAcceptedObservation, Is.True);
                Assert.That(runtimeRoot.gameObject.activeSelf, Is.True);
                Assert.That(layer.IsSelected, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void fingerprint_계산후_변조한_사본은_거절한다()
        {
            var snapshot = Snapshot(10, 16);
            snapshot.CompletedDeliveryCount += 1;
            var interpreter = new 사가정합성Actor관찰Interpreter();

            var result = interpreter.Apply(snapshot);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode,
                Is.EqualTo(사가정합성Actor관찰FailureCodes.FingerprintInvalid));
            Assert.That(interpreter.Current, Is.Null);
        }

        [Test]
        public void 표시를_숨긴동안_상위_revision을_수용하고_재표시해도_Simulation사본은_불변이다()
        {
            var root = new GameObject("사가정 합성 Actor 관찰 시험");
            var cameraRoot = new GameObject("사가정 관찰 카메라");
            try
            {
                var camera = cameraRoot.AddComponent<Camera>();
                var layer = root.AddComponent<사가정합성Actor관찰Layer>();
                layer.Configure(camera, new Vector3(100f, 0f, -40f), 2f);
                var initial = Snapshot(20, 31, "픽업 장소로 이동");
                var initialHash = initial.ProjectionFingerprintSha256;
                var initialTick = initial.CurrentTick;

                var first = layer.Apply(initial);

                Assert.That(first.Accepted, Is.True, first.ErrorCode);
                Assert.That(layer.HasAcceptedObservation, Is.True);
                Assert.That(layer.RuntimeVisualCount, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(layer.SelectCurrentActor(), Is.True);
                Assert.That(layer.IsSelected, Is.True);
                Assert.That(layer.SelectedActorStableId,
                    Is.EqualTo(사가정합성기사관찰Policy.ActorStableId));

                var runtimeRoot = root.transform.Find(
                    사가정합성Actor관찰Layer.RuntimeRootName);
                Assert.That(runtimeRoot, Is.Not.Null);
                Assert.That(runtimeRoot.gameObject.activeSelf, Is.True);

                layer.SetDisplayVisible(false);
                Assert.That(layer.DisplayVisible, Is.False);
                Assert.That(runtimeRoot.gameObject.activeSelf, Is.False);
                Assert.That(layer.SelectCurrentActor(), Is.False);

                var updated = Snapshot(21, 32, "픽업 장소로 이동");
                var updatedHash = updated.ProjectionFingerprintSha256;
                var second = layer.Apply(updated);

                Assert.That(second.Accepted, Is.True, second.ErrorCode);
                Assert.That(second.Changed, Is.True);
                Assert.That(layer.AppliedRevision, Is.EqualTo(21));
                Assert.That(layer.Current.CurrentActionDisplayName,
                    Is.EqualTo("픽업 장소로 이동"));
                Assert.That(layer.IsSelected, Is.True,
                    "표시를 끄는 것은 선택 정책이나 사본 revision을 바꾸지 않는다.");
                Assert.That(runtimeRoot.gameObject.activeSelf, Is.False);

                layer.SetDisplayVisible(true);

                Assert.That(layer.DisplayVisible, Is.True);
                Assert.That(layer.StatusCode,
                    Is.EqualTo(사가정합성Actor관찰Layer.ReadyStatus));
                Assert.That(runtimeRoot.gameObject.activeSelf, Is.True);
                Assert.That(layer.Current.SyntheticCompensation.StatusCode,
                    Is.EqualTo(사가정합성기사관찰SettlementStatusCodes.PolicyPending));
                Assert.That(layer.Current.SyntheticCompensation.AmountMinor, Is.Null);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);

                Assert.That(initial.SourceSessionRevision, Is.EqualTo(20));
                Assert.That(initial.CurrentTick, Is.EqualTo(initialTick));
                Assert.That(initial.ProjectionFingerprintSha256, Is.EqualTo(initialHash));
                Assert.That(initial.SyntheticCompensation.AmountMinor, Is.Null);
                Assert.That(updated.SourceSessionRevision, Is.EqualTo(21));
                Assert.That(updated.ProjectionFingerprintSha256, Is.EqualTo(updatedHash));
                Assert.That(updated.SyntheticCompensation.AmountMinor, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void LifeSimulationAdapter는_private_review와_fingerprint를_보고하고_Host표시와_사용자토글을_분리한다()
        {
            var root = new GameObject("사가정 LifeSimulation Adapter 시험");
            var cameraRoot = new GameObject("사가정 LifeSimulation 카메라");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAndMobilityAdapter());
                var layer = root.AddComponent<사가정합성Actor관찰Layer>();
                layer.Configure(cameraRoot.AddComponent<Camera>(), Vector3.zero);
                var snapshot = Snapshot(30, 44);
                Assert.That(layer.Apply(snapshot).Accepted, Is.True);
                var adapter = root.AddComponent<사가정LifeSimulationModuleAdapter>();

                adapter.Configure(host, layer);
                Assert.That(adapter.RefreshModuleState(), Is.True);
                var state = adapter.DescribeModuleLayers()[0];

                Assert.That(state.LayerStableId,
                    Is.EqualTo(StationDioramaModuleLayerIds.LifeSimulation));
                Assert.That(state.StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.PrivateReview));
                Assert.That(state.DiagnosticCode,
                    Is.EqualTo(사가정합성Actor관찰Layer.ReadyStatus));
                Assert.That(state.SourceRevision,
                    Is.EqualTo(사가정LifeSimulationModuleAdapter.AdapterRevision
                               + "|" + snapshot.ProjectionFingerprintSha256));
                Assert.That(state.AvailableForObservation, Is.True);
                Assert.That(adapter.ModuleVisible, Is.True);
                Assert.That(layer.HostVisible, Is.True);

                layer.SetDisplayVisible(false);
                adapter.SetModuleVisible(false);
                Assert.That(layer.HostVisible, Is.False);
                Assert.That(layer.DisplayVisible, Is.False);
                Assert.That(layer.EffectiveVisible, Is.False);

                adapter.SetModuleVisible(true);
                Assert.That(layer.HostVisible, Is.True);
                Assert.That(layer.DisplayVisible, Is.False,
                    "Host 전환은 사용자의 로컬 표시 토글을 덮어쓰지 않는다.");
                Assert.That(layer.EffectiveVisible, Is.False);

                layer.SetDisplayVisible(true);
                Assert.That(layer.EffectiveVisible, Is.True);
                Assert.That(snapshot.SourceSessionRevision, Is.EqualTo(30));
                Assert.That(snapshot.ProjectionFingerprintSha256,
                    Is.EqualTo(state.SourceRevision.Substring(
                        사가정LifeSimulationModuleAdapter.AdapterRevision.Length + 1)));

                adapter.DetachFromHost();
                Assert.That(layer.HostVisible, Is.False,
                    "Host에서 명시적으로 해제한 Adapter의 표시가 다시 켜지면 안 된다.");
                Object.DestroyImmediate(adapter);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
            }
        }

        private static 사가정합성기사관찰Snapshot Snapshot(
            long revision, int tick, string actionDisplayName = "픽업 장소로 이동")
        {
            var value = new 사가정합성기사관찰Snapshot
            {
                SchemaVersion = 사가정합성기사관찰Policy.SchemaVersion,
                SessionStableId = "simulation-session:sagajeong:test",
                TransitStationStableId = 사가정합성기사관찰Policy
                    .TransitStationStableId,
                ScenarioStableId = 사가정합성기사관찰Policy.ScenarioStableId,
                ScenarioDataRevision = 사가정합성기사관찰Policy.ScenarioDataRevision,
                SourceSessionRevision = revision,
                SourceWorldRevision = revision,
                CurrentTick = tick,
                DurationTicks = 120,
                ActorStableId = 사가정합성기사관찰Policy.ActorStableId,
                DisplayAlias = 사가정합성기사관찰Policy.DisplayAlias,
                RoleCode = 사가정합성기사관찰Policy.RoleCode,
                RoleDisplayName = 사가정합성기사관찰Policy.RoleDisplayName,
                CurrentStageCode = "DriveRestaurant",
                CurrentStageDisplayName = "픽업 장소로 운전",
                CurrentActionCode = "MoveToPickupByVehicle",
                CurrentActionDisplayName = actionDisplayName,
                CurrentOrderStableId = "food-order:synthetic:test:1",
                CurrentOrderStateCode = "기사배정",
                RouteProgressBasisCode = 사가정합성기사관찰RouteProgressBasisCodes
                    .StageLocalDistanceMeters,
                RouteProgressStatusCode = 사가정합성기사관찰RouteProgressStatusCodes.Ready,
                RouteProgressMeters = 2.5,
                RouteLengthMeters = 10,
                RouteProgressRatio = .25,
                ActorX = 2,
                ActorZ = 3,
                VehicleX = 2.25,
                VehicleZ = 3.25,
                Carrying = false,
                CountAttributionBasisCode = 사가정합성기사관찰Policy
                    .CountAttributionBasisCode,
                CompletedDeliveryCount = 1,
                InProgressDeliveryCount = 1,
                FailedDeliveryCount = null,
                FailureCountStatusCode = 사가정합성기사관찰MetricStatusCodes.NotTracked,
                RecoveredDeliveryCount = null,
                RecoveryCountStatusCode = 사가정합성기사관찰MetricStatusCodes.NotTracked,
                SyntheticCompensation = new 사가정합성기사모의지급Snapshot
                {
                    StatusCode = 사가정합성기사관찰SettlementStatusCodes.PolicyPending,
                    BlockReasonCode = 사가정합성기사관찰SettlementStatusCodes
                        .SimulationSettlementRulePending,
                    PolicyRevision = string.Empty,
                    AmountMinor = null,
                    CurrencyCode = string.Empty
                },
                Synthetic = true,
                ActualPerson = false,
                Operational = false,
                SettlementWriteAllowed = false,
                ChangesAuthorityState = false,
                ObservationPresentationOnly = true
            };
            value.ProjectionFingerprintSha256 =
                사가정합성기사관찰Fingerprint.Compute(value);
            return value;
        }

        private static void Rehash(사가정합성기사관찰Snapshot value)
            => value.ProjectionFingerprintSha256 =
                사가정합성기사관찰Fingerprint.Compute(value);

        private static void AssertRejected(
            사가정합성기사관찰Snapshot value, string expectedErrorCode)
        {
            var result = new 사가정합성Actor관찰Interpreter().Apply(value);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(expectedErrorCode));
        }

        private sealed class FoundationAndMobilityAdapter : IStationDioramaModuleAdapter
        {
            public string TransitStationStableId =>
                StationDioramaProfileCatalog.SagajeongStationStableId;
            public bool ModuleVisible { get; private set; }

            public IReadOnlyList<StationDioramaModuleLayerRuntimeState>
                DescribeModuleLayers()
                => new[]
                {
                    StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                        StationDioramaModuleLayerIds.DataEvidence, true,
                        "fixture:data.r1", "Ready"),
                    StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                        StationDioramaModuleLayerIds.BaseSpatialPresentation, true,
                        "fixture:presentation.r1", "Ready"),
                    StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                        StationDioramaModuleLayerIds.Mobility, true,
                        "fixture:mobility.r1", "Ready")
                };

            public void SetModuleVisible(bool value) => ModuleVisible = value;
        }
    }
}
