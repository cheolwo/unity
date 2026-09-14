using System;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 사가정합성Actor관찰FailureCodes
    {
        public const string SnapshotMissing = "SagajeongSyntheticActorSnapshotMissing";
        public const string SchemaUnsupported = "SagajeongSyntheticActorSchemaUnsupported";
        public const string IdentityInvalid = "SagajeongSyntheticActorIdentityInvalid";
        public const string SourceInvalid = "SagajeongSyntheticActorSourceInvalid";
        public const string PrivacyBoundaryInvalid = "SagajeongSyntheticActorPrivacyBoundaryInvalid";
        public const string ProgressInvalid = "SagajeongSyntheticActorProgressInvalid";
        public const string CountInvalid = "SagajeongSyntheticActorCountInvalid";
        public const string SettlementBoundaryInvalid = "SagajeongSyntheticActorSettlementBoundaryInvalid";
        public const string FingerprintInvalid = "SagajeongSyntheticActorFingerprintInvalid";
        public const string IdentityChanged = "SagajeongSyntheticActorIdentityChanged";
        public const string StaleRevision = "SagajeongSyntheticActorStaleRevision";
        public const string StaleTick = "SagajeongSyntheticActorStaleTick";
        public const string RevisionConflict = "SagajeongSyntheticActorRevisionConflict";
    }

    public sealed class 사가정합성Actor관찰ApplyResult
    {
        public bool Accepted { get; internal set; }
        public bool Changed { get; internal set; }
        public string ErrorCode { get; internal set; } = string.Empty;
        public 사가정합성기사관찰Snapshot Snapshot { get; internal set; }
    }

    /// <summary>
    /// Simulation이 생성한 합성 기사 관찰 사본을 검증하고 최신본만
    /// Unity 메모리에 보존한다. 이 해석기는 건수나 금액을 계산하지 않는다.
    /// </summary>
    public sealed class 사가정합성Actor관찰Interpreter
    {
        private 사가정합성기사관찰Snapshot current;

        public 사가정합성기사관찰Snapshot Current => Copy(current);

        public 사가정합성Actor관찰ApplyResult Apply(
            사가정합성기사관찰Snapshot incoming)
        {
            var error = Validate(incoming);
            if (error.Length > 0) return Reject(error);

            if (current != null)
            {
                if (!string.Equals(current.SessionStableId, incoming.SessionStableId,
                        StringComparison.Ordinal)
                    || !string.Equals(current.TransitStationStableId,
                        incoming.TransitStationStableId, StringComparison.Ordinal)
                    || !string.Equals(current.ActorStableId, incoming.ActorStableId,
                        StringComparison.Ordinal)
                    || !string.Equals(current.ScenarioStableId,
                        incoming.ScenarioStableId, StringComparison.Ordinal)
                    || !string.Equals(current.ScenarioDataRevision,
                        incoming.ScenarioDataRevision, StringComparison.Ordinal))
                    return Reject(사가정합성Actor관찰FailureCodes.IdentityChanged);
                if (incoming.SourceSessionRevision < current.SourceSessionRevision)
                    return Reject(사가정합성Actor관찰FailureCodes.StaleRevision);
                if (incoming.SourceSessionRevision == current.SourceSessionRevision)
                {
                    if (!string.Equals(incoming.ProjectionFingerprintSha256,
                            current.ProjectionFingerprintSha256,
                            StringComparison.Ordinal))
                        return Reject(사가정합성Actor관찰FailureCodes.RevisionConflict);
                    return new 사가정합성Actor관찰ApplyResult
                    {
                        Accepted = true,
                        Changed = false,
                        Snapshot = Copy(current)
                    };
                }
                if (incoming.CurrentTick < current.CurrentTick)
                    return Reject(사가정합성Actor관찰FailureCodes.StaleTick);
            }

            current = Copy(incoming);
            return new 사가정합성Actor관찰ApplyResult
            {
                Accepted = true,
                Changed = true,
                Snapshot = Copy(current)
            };
        }

        public void Clear() => current = null;

        private static string Validate(사가정합성기사관찰Snapshot value)
        {
            if (value == null) return 사가정합성Actor관찰FailureCodes.SnapshotMissing;
            if (!string.Equals(value.SchemaVersion,
                    사가정합성기사관찰Policy.SchemaVersion,
                    StringComparison.Ordinal))
                return 사가정합성Actor관찰FailureCodes.SchemaUnsupported;
            if (string.IsNullOrWhiteSpace(value.SessionStableId)
                || !string.Equals(value.TransitStationStableId,
                    사가정합성기사관찰Policy.TransitStationStableId,
                    StringComparison.Ordinal)
                || !string.Equals(value.ActorStableId,
                    사가정합성기사관찰Policy.ActorStableId,
                    StringComparison.Ordinal)
                || !string.Equals(value.DisplayAlias,
                    사가정합성기사관찰Policy.DisplayAlias,
                    StringComparison.Ordinal)
                || !string.Equals(value.RoleCode,
                    사가정합성기사관찰Policy.RoleCode,
                    StringComparison.Ordinal)
                || !string.Equals(value.RoleDisplayName,
                    사가정합성기사관찰Policy.RoleDisplayName,
                    StringComparison.Ordinal))
                return 사가정합성Actor관찰FailureCodes.IdentityInvalid;
            if (!string.Equals(value.ScenarioStableId,
                    사가정합성기사관찰Policy.ScenarioStableId,
                    StringComparison.Ordinal)
                || !string.Equals(value.ScenarioDataRevision,
                    사가정합성기사관찰Policy.ScenarioDataRevision,
                    StringComparison.Ordinal)
                || value.SourceSessionRevision <= 0
                || value.SourceSessionRevision != value.SourceWorldRevision
                || value.CurrentTick < 0 || value.DurationTicks <= 0
                || value.CurrentTick > value.DurationTicks
                || !TryGetStage(value.CurrentStageCode, out var stage)
                || !string.Equals(value.CurrentStageDisplayName,
                    stage.StageDisplayName, StringComparison.Ordinal)
                || !string.Equals(value.CurrentActionCode,
                    stage.ActionCode, StringComparison.Ordinal)
                || !string.Equals(value.CurrentActionDisplayName,
                    stage.ActionDisplayName, StringComparison.Ordinal)
                || (string.Equals(value.CurrentStageCode, "Idle",
                        StringComparison.Ordinal)
                    ? !string.IsNullOrEmpty(value.CurrentOrderStableId)
                      || !string.IsNullOrEmpty(value.CurrentOrderStateCode)
                    : !IsSyntheticId(value.CurrentOrderStableId)
                      || !IsSupportedOrderState(value.CurrentOrderStateCode)))
                return 사가정합성Actor관찰FailureCodes.SourceInvalid;
            if (!value.Synthetic || value.ActualPerson || value.Operational
                || value.SettlementWriteAllowed || value.ChangesAuthorityState
                || !value.ObservationPresentationOnly)
                return 사가정합성Actor관찰FailureCodes.PrivacyBoundaryInvalid;
            if (!Finite(value.RouteProgressMeters) || !Finite(value.RouteLengthMeters)
                || value.RouteProgressMeters < 0 || value.RouteLengthMeters < 0
                || value.RouteProgressMeters > value.RouteLengthMeters
                || !string.Equals(value.RouteProgressBasisCode,
                    사가정합성기사관찰RouteProgressBasisCodes.StageLocalDistanceMeters,
                    StringComparison.Ordinal)
                || Math.Abs(value.RouteLengthMeters - stage.RouteLengthMeters) > 0.000001
                || !Finite(value.ActorX) || !Finite(value.ActorZ)
                || !Finite(value.VehicleX) || !Finite(value.VehicleZ))
                return 사가정합성Actor관찰FailureCodes.ProgressInvalid;
            if (value.RouteLengthMeters == 0
                ? value.RouteProgressMeters != 0
                  || value.RouteProgressRatio.HasValue
                  || !string.Equals(value.RouteProgressStatusCode,
                      사가정합성기사관찰RouteProgressStatusCodes.NotApplicable,
                      StringComparison.Ordinal)
                : !value.RouteProgressRatio.HasValue
                  || !Finite(value.RouteProgressRatio.Value)
                  || value.RouteProgressRatio.Value < 0
                  || value.RouteProgressRatio.Value > 1
                  || Math.Abs(value.RouteProgressRatio.Value
                              - value.RouteProgressMeters / value.RouteLengthMeters) > 0.000001
                  || !string.Equals(value.RouteProgressStatusCode,
                      사가정합성기사관찰RouteProgressStatusCodes.Ready,
                      StringComparison.Ordinal))
                return 사가정합성Actor관찰FailureCodes.ProgressInvalid;
            if (value.CompletedDeliveryCount < 0 || value.InProgressDeliveryCount < 0
                || value.InProgressDeliveryCount > 1
                || (value.FailedDeliveryCount.HasValue && value.FailedDeliveryCount.Value < 0)
                || (value.RecoveredDeliveryCount.HasValue && value.RecoveredDeliveryCount.Value < 0)
                || !string.Equals(value.CountAttributionBasisCode,
                    사가정합성기사관찰Policy.CountAttributionBasisCode,
                    StringComparison.Ordinal)
                || value.FailedDeliveryCount.HasValue
                || !string.Equals(value.FailureCountStatusCode,
                    사가정합성기사관찰MetricStatusCodes.NotTracked,
                    StringComparison.Ordinal)
                || value.RecoveredDeliveryCount.HasValue
                || !string.Equals(value.RecoveryCountStatusCode,
                    사가정합성기사관찰MetricStatusCodes.NotTracked,
                    StringComparison.Ordinal))
                return 사가정합성Actor관찰FailureCodes.CountInvalid;
            var payment = value.SyntheticCompensation;
            if (payment == null
                || !string.Equals(payment.StatusCode,
                    사가정합성기사관찰SettlementStatusCodes.PolicyPending,
                    StringComparison.Ordinal)
                || !string.Equals(payment.BlockReasonCode,
                    사가정합성기사관찰SettlementStatusCodes
                        .SimulationSettlementRulePending,
                    StringComparison.Ordinal)
                || payment.AmountMinor.HasValue
                || !string.IsNullOrEmpty(payment.CurrencyCode)
                || !string.IsNullOrEmpty(payment.PolicyRevision))
                return 사가정합성Actor관찰FailureCodes.SettlementBoundaryInvalid;
            if (!IsUpperSha256(value.ProjectionFingerprintSha256)
                || !string.Equals(
                    사가정합성기사관찰Fingerprint.Compute(value),
                    value.ProjectionFingerprintSha256,
                    StringComparison.Ordinal))
                return 사가정합성Actor관찰FailureCodes.FingerprintInvalid;
            return string.Empty;
        }

        private 사가정합성Actor관찰ApplyResult Reject(string code)
            => new 사가정합성Actor관찰ApplyResult
            {
                ErrorCode = code,
                Snapshot = Copy(current)
            };

        private static bool Finite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsSyntheticId(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.IndexOf(":synthetic:", StringComparison.Ordinal) >= 0;

        private static bool IsSupportedOrderState(string value)
            => string.Equals(value, 음식배달상태코드.주문대기, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.조리중, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.픽업대기, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.기사배정, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.픽업완료, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.전달완료, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.수령확인, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.거절, StringComparison.Ordinal)
               || string.Equals(value, 음식배달상태코드.취소, StringComparison.Ordinal);

        private static bool TryGetStage(string code, out StageShape stage)
        {
            switch (code)
            {
                case "Idle":
                    stage = new StageShape("업무 대기", "WaitForAssignment",
                        "다음 합성 주문 대기", 0);
                    return true;
                case "DriveRestaurant":
                    stage = new StageShape("픽업 장소로 운전", "MoveToPickupByVehicle",
                        "픽업 장소로 이동", 10);
                    return true;
                case "WalkRestaurant":
                    stage = new StageShape("픽업점 접근", "WalkToPickup",
                        "픽업점 출입구로 이동", 4);
                    return true;
                case "WaitFood":
                    stage = new StageShape("조리 완료 대기", "WaitForPreparation",
                        "조리 완료 대기", 0);
                    return true;
                case "Pickup":
                    stage = new StageShape("물품 픽업", "PickupOrder",
                        "주문 물품 픽업", 0);
                    return true;
                case "WalkRestaurantOut":
                    stage = new StageShape("차량으로 복귀", "ReturnToVehicle",
                        "픽업점에서 차량으로 복귀", 4);
                    return true;
                case "DriveHome":
                    stage = new StageShape("주문지로 운전", "MoveToDeliveryDestination",
                        "주문지로 이동", 40);
                    return true;
                case "WalkHome":
                    stage = new StageShape("주택 출입구 접근", "WalkToDeliveryEntrance",
                        "전달 지점으로 도보 이동", 4);
                    return true;
                case "Deliver":
                    stage = new StageShape("주문자에게 전달", "HandOffOrder",
                        "주문 물품 전달", 0);
                    return true;
                case "Receive":
                    stage = new StageShape("수령 확인 대기", "WaitForReceipt",
                        "주문자 수령 확인 대기", 0);
                    return true;
                case "WalkHomeOut":
                    stage = new StageShape("차량으로 복귀", "ReturnToVehicle",
                        "전달 지점에서 차량으로 복귀", 4);
                    return true;
                case "Return":
                    stage = new StageShape("대기점 복귀", "ReturnToStandby",
                        "대기점으로 복귀", 50);
                    return true;
                default:
                    stage = default;
                    return false;
            }
        }

        private readonly struct StageShape
        {
            public readonly string StageDisplayName;
            public readonly string ActionCode;
            public readonly string ActionDisplayName;
            public readonly double RouteLengthMeters;

            public StageShape(string stageDisplayName, string actionCode,
                string actionDisplayName, double routeLengthMeters)
            {
                StageDisplayName = stageDisplayName;
                ActionCode = actionCode;
                ActionDisplayName = actionDisplayName;
                RouteLengthMeters = routeLengthMeters;
            }
        }

        private static bool IsUpperSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var character in value)
                if (!((character >= '0' && character <= '9')
                      || (character >= 'A' && character <= 'F')))
                    return false;
            return true;
        }

        private static 사가정합성기사관찰Snapshot Copy(
            사가정합성기사관찰Snapshot value)
        {
            if (value == null) return null;
            return new 사가정합성기사관찰Snapshot
            {
                SchemaVersion = value.SchemaVersion,
                SessionStableId = value.SessionStableId,
                TransitStationStableId = value.TransitStationStableId,
                ScenarioStableId = value.ScenarioStableId,
                ScenarioDataRevision = value.ScenarioDataRevision,
                SourceSessionRevision = value.SourceSessionRevision,
                SourceWorldRevision = value.SourceWorldRevision,
                CurrentTick = value.CurrentTick,
                DurationTicks = value.DurationTicks,
                ActorStableId = value.ActorStableId,
                DisplayAlias = value.DisplayAlias,
                RoleCode = value.RoleCode,
                RoleDisplayName = value.RoleDisplayName,
                CurrentStageCode = value.CurrentStageCode,
                CurrentStageDisplayName = value.CurrentStageDisplayName,
                CurrentActionCode = value.CurrentActionCode,
                CurrentActionDisplayName = value.CurrentActionDisplayName,
                CurrentOrderStableId = value.CurrentOrderStableId,
                CurrentOrderStateCode = value.CurrentOrderStateCode,
                RouteProgressBasisCode = value.RouteProgressBasisCode,
                RouteProgressStatusCode = value.RouteProgressStatusCode,
                RouteProgressMeters = value.RouteProgressMeters,
                RouteLengthMeters = value.RouteLengthMeters,
                RouteProgressRatio = value.RouteProgressRatio,
                ActorX = value.ActorX,
                ActorZ = value.ActorZ,
                VehicleX = value.VehicleX,
                VehicleZ = value.VehicleZ,
                Carrying = value.Carrying,
                CountAttributionBasisCode = value.CountAttributionBasisCode,
                CompletedDeliveryCount = value.CompletedDeliveryCount,
                InProgressDeliveryCount = value.InProgressDeliveryCount,
                FailedDeliveryCount = value.FailedDeliveryCount,
                FailureCountStatusCode = value.FailureCountStatusCode,
                RecoveredDeliveryCount = value.RecoveredDeliveryCount,
                RecoveryCountStatusCode = value.RecoveryCountStatusCode,
                SyntheticCompensation = new 사가정합성기사모의지급Snapshot
                {
                    StatusCode = value.SyntheticCompensation.StatusCode,
                    BlockReasonCode = value.SyntheticCompensation.BlockReasonCode,
                    PolicyRevision = value.SyntheticCompensation.PolicyRevision,
                    AmountMinor = value.SyntheticCompensation.AmountMinor,
                    CurrencyCode = value.SyntheticCompensation.CurrencyCode
                },
                Synthetic = value.Synthetic,
                ActualPerson = value.ActualPerson,
                Operational = value.Operational,
                SettlementWriteAllowed = value.SettlementWriteAllowed,
                ChangesAuthorityState = value.ChangesAuthorityState,
                ObservationPresentationOnly = value.ObservationPresentationOnly,
                ProjectionFingerprintSha256 = value.ProjectionFingerprintSha256
            };
        }
    }
}
