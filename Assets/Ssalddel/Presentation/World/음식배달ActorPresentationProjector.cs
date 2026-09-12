using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 권위 상태 사본에서 한 배달기사 배치 객체가 표시할 값만 분리한다.
    /// 이 모델은 주문·배차·배송 상태나 Session revision을 변경할 수 없다.
    /// </summary>
    public sealed class 음식배달ActorPresentationModel
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long SessionRevision { get; set; }
        public string ActorStableId { get; set; } = string.Empty;
        public string OrderStableId { get; set; } = string.Empty;
        public string CourierStage { get; set; } = string.Empty;
        public string OrderStateCode { get; set; } = string.Empty;
        public long OrderRevision { get; set; } = -1;
        public double ActorX { get; set; }
        public double ActorZ { get; set; }
        public double VehicleX { get; set; }
        public double VehicleZ { get; set; }
        public bool Carrying { get; set; }
        public string WorkflowObservationStatusCode { get; set; } = string.Empty;
        public string WorkflowEngineCode { get; set; } = string.Empty;
        public string WorkflowEngineClassificationPrimaryCode { get; set; } = string.Empty;
        public string[] WorkflowEngineClassificationSupportCodes { get; set; } = Array.Empty<string>();
        public string WorkflowModuleCode { get; set; } = string.Empty;
        public string WorkflowModuleClassificationPrimaryCode { get; set; } = string.Empty;
        public string WorkflowMeaningRevision { get; set; } = string.Empty;
        public string WorkflowRuleRevision { get; set; } = string.Empty;
        public bool WorkflowIsExecutionAuthority { get; set; }
        public string LatestTransitionFromStateCode { get; set; } = string.Empty;
        public string LatestTransitionToStateCode { get; set; } = string.Empty;
        public int LatestTransitionWorldTick { get; set; } = -1;
        public string CourierActivityDisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 합성 배달기사와 같은 주문 상태 사본의 결속을 검사한 뒤 표현 값만 반환한다.
    /// 근접 추정이나 누락 보정은 하지 않는다.
    /// </summary>
    public static class 음식배달ActorPresentationProjector
    {
        public const string SessionIdentityMissing = "FoodDeliveryActorSessionIdentityMissing";
        public const string ActorIdentityMissing = "FoodDeliveryActorIdentityMissing";
        public const string ActorIdentityChanged = "FoodDeliveryActorIdentityChanged";
        public const string CourierStageMissing = "FoodDeliveryCourierStageMissing";
        public const string StaleRevision = "FoodDeliveryActorSessionRevisionStale";
        public const string ActiveOrderIdentityMissing = "FoodDeliveryActorActiveOrderIdentityMissing";
        public const string OrderMissing = "FoodDeliveryActorOrderMissing";
        public const string OrderDuplicate = "FoodDeliveryActorOrderDuplicate";
        public const string CoordinateInvalid = "FoodDeliveryActorCoordinateInvalid";
        public const string CourierStageUnknown = "FoodDeliveryCourierStageUnknown";
        public const string WorkflowEngineMissing = "FoodDeliveryWorkflowEngineMissing";
        public const string WorkflowEngineDescriptorInvalid = "FoodDeliveryWorkflowEngineDescriptorInvalid";
        public const string WorkflowEngineAuthorityInvalid = "FoodDeliveryWorkflowEngineAuthorityInvalid";
        public const string TransitionHistoryInvalid = "FoodDeliveryTransitionHistoryInvalid";
        public const string TransitionOrderStateMismatch = "FoodDeliveryTransitionOrderStateMismatch";
        public const string WorkflowTransitionRejected = "FoodDeliveryWorkflowTransitionRejected";
        public const string WorkflowRuleRevisionMismatch = "FoodDeliveryWorkflowRuleRevisionMismatch";
        public const string WorkflowMeaningMissing = "FoodDeliveryWorkflowMeaningMissing";

        public const string WorkflowObservationPending = "Pending";
        public const string WorkflowObservationVerified = "Verified";

        public static bool TryProject(
            경영SimulationSessionSnapshot session,
            가상배달기사Snapshot courier,
            string lastAppliedSessionStableId,
            long lastAppliedSessionRevision,
            string lastAppliedActorStableId,
            out 음식배달ActorPresentationModel model,
            out string failureCode)
            => TryProject(session, courier, BusinessWorkflowRuleEngine.기본,
                lastAppliedSessionStableId, lastAppliedSessionRevision,
                lastAppliedActorStableId, out model, out failureCode);

        public static bool TryProject(
            경영SimulationSessionSnapshot session,
            가상배달기사Snapshot courier,
            IBusinessWorkflowRuleEngine workflowEngine,
            string lastAppliedSessionStableId,
            long lastAppliedSessionRevision,
            string lastAppliedActorStableId,
            out 음식배달ActorPresentationModel model,
            out string failureCode)
        {
            model = null;
            failureCode = string.Empty;
            if (session == null || string.IsNullOrWhiteSpace(session.SessionStableId))
                return Fail(SessionIdentityMissing, out failureCode);
            if (courier == null || string.IsNullOrWhiteSpace(courier.ActorStableId))
                return Fail(ActorIdentityMissing, out failureCode);

            var stage = courier.Stage?.Trim() ?? string.Empty;
            if (stage.Length == 0)
                return Fail(CourierStageMissing, out failureCode);
            if (!TryResolveCourierActivity(stage, out var courierActivity))
                return Fail(CourierStageUnknown, out failureCode);
            if (workflowEngine == null)
                return Fail(WorkflowEngineMissing, out failureCode);
            if (string.Equals(session.SessionStableId, lastAppliedSessionStableId, StringComparison.Ordinal)
                && session.Revision < lastAppliedSessionRevision)
            {
                return Fail(StaleRevision, out failureCode);
            }
            if (string.Equals(session.SessionStableId, lastAppliedSessionStableId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(lastAppliedActorStableId)
                && !string.Equals(courier.ActorStableId, lastAppliedActorStableId, StringComparison.Ordinal))
            {
                return Fail(ActorIdentityChanged, out failureCode);
            }
            if (!IsFinite(courier.X) || !IsFinite(courier.Z)
                || !IsFinite(courier.VehicleX) || !IsFinite(courier.VehicleZ))
            {
                return Fail(CoordinateInvalid, out failureCode);
            }

            var orderStableId = courier.OrderStableId?.Trim() ?? string.Empty;
            Simulation음식배달Snapshot order = null;
            if (orderStableId.Length == 0)
            {
                if (!string.Equals(stage, "Idle", StringComparison.Ordinal))
                    return Fail(ActiveOrderIdentityMissing, out failureCode);
            }
            else
            {
                var matches = (session.FoodDeliveries ?? Array.Empty<Simulation음식배달Snapshot>())
                    .Where(candidate => candidate != null && string.Equals(
                        candidate.FoodOrderStableId, orderStableId, StringComparison.Ordinal))
                    .Take(2)
                    .ToArray();
                if (matches.Length == 0)
                    return Fail(OrderMissing, out failureCode);
                if (matches.Length > 1)
                    return Fail(OrderDuplicate, out failureCode);
                order = matches[0];
            }

            BusinessWorkflowEngineDescriptor engineDescriptor;
            try
            {
                engineDescriptor = workflowEngine.Engine정보조회();
            }
            catch
            {
                return Fail(WorkflowEngineDescriptorInvalid, out failureCode);
            }
            if (engineDescriptor == null
                || string.IsNullOrWhiteSpace(engineDescriptor.EngineCode)
                || string.IsNullOrWhiteSpace(engineDescriptor.MeaningRevision))
            {
                return Fail(WorkflowEngineDescriptorInvalid, out failureCode);
            }
            if (engineDescriptor.IsExecutionAuthority)
                return Fail(WorkflowEngineAuthorityInvalid, out failureCode);

            var projected = new 음식배달ActorPresentationModel
            {
                SessionStableId = session.SessionStableId,
                SessionRevision = session.Revision,
                ActorStableId = courier.ActorStableId,
                OrderStableId = orderStableId,
                CourierStage = stage,
                OrderStateCode = order?.StateCode ?? string.Empty,
                OrderRevision = order?.Revision ?? -1,
                ActorX = courier.X,
                ActorZ = courier.Z,
                VehicleX = courier.VehicleX,
                VehicleZ = courier.VehicleZ,
                Carrying = courier.Carrying,
                WorkflowObservationStatusCode = WorkflowObservationPending,
                WorkflowEngineCode = engineDescriptor.EngineCode,
                WorkflowEngineClassificationPrimaryCode =
                    engineDescriptor.ClassificationMetadata?.PrimaryCode ?? string.Empty,
                WorkflowEngineClassificationSupportCodes =
                    engineDescriptor.ClassificationMetadata?.SupportCodes?.ToArray()
                        ?? Array.Empty<string>(),
                WorkflowIsExecutionAuthority = engineDescriptor.IsExecutionAuthority,
                CourierActivityDisplayName = courierActivity,
            };

            if (order != null && order.StateHistory != null && order.StateHistory.Length > 0)
            {
                if (!TryProjectLatestWorkflowTransition(session, order, workflowEngine,
                    projected, out failureCode))
                {
                    return false;
                }
            }

            model = projected;
            return true;
        }

        private static bool TryProjectLatestWorkflowTransition(
            경영SimulationSessionSnapshot session,
            Simulation음식배달Snapshot order,
            IBusinessWorkflowRuleEngine workflowEngine,
            음식배달ActorPresentationModel projected,
            out string failureCode)
        {
            failureCode = string.Empty;
            var history = order.StateHistory;
            Simulation음식배달상태전이Snapshot previous = null;
            foreach (var transition in history)
            {
                if (transition == null
                    || string.IsNullOrWhiteSpace(transition.FromStateCode)
                    || string.IsNullOrWhiteSpace(transition.ToStateCode)
                    || transition.WorldTick < 0
                    || transition.WorldTick > session.CurrentTick
                    || (previous != null && (transition.WorldTick < previous.WorldTick
                        || !string.Equals(previous.ToStateCode, transition.FromStateCode,
                            StringComparison.Ordinal))))
                {
                    return Fail(TransitionHistoryInvalid, out failureCode);
                }
                previous = transition;
            }

            var latest = history[history.Length - 1];
            if (!string.Equals(latest.ToStateCode, order.StateCode, StringComparison.Ordinal))
                return Fail(TransitionOrderStateMismatch, out failureCode);

            BusinessWorkflowTransitionDecision decision;
            try
            {
                decision = workflowEngine.판정(new BusinessWorkflowTransitionRequest
                {
                    업무흐름코드 = 업무흐름코드.음식배달,
                    현재상태코드 = latest.FromStateCode,
                    목표상태코드 = latest.ToStateCode,
                });
            }
            catch
            {
                return Fail(WorkflowTransitionRejected, out failureCode);
            }
            if (decision == null || !decision.허용여부
                || (decision.차단사유코드목록?.Length ?? 0) > 0)
            {
                return Fail(WorkflowTransitionRejected, out failureCode);
            }
            if (!string.Equals(decision.RuleRevision, latest.RuleRevision,
                StringComparison.Ordinal))
            {
                return Fail(WorkflowRuleRevisionMismatch, out failureCode);
            }
            if (string.IsNullOrWhiteSpace(decision.판정ModuleCode)
                || string.IsNullOrWhiteSpace(decision.MeaningRevision)
                || string.IsNullOrWhiteSpace(decision.RuleRevision))
            {
                return Fail(WorkflowMeaningMissing, out failureCode);
            }

            projected.WorkflowObservationStatusCode = WorkflowObservationVerified;
            projected.WorkflowModuleCode = decision.판정ModuleCode;
            projected.WorkflowModuleClassificationPrimaryCode =
                decision.ClassificationMetadata?.PrimaryCode ?? string.Empty;
            projected.WorkflowMeaningRevision = decision.MeaningRevision;
            projected.WorkflowRuleRevision = decision.RuleRevision;
            projected.LatestTransitionFromStateCode = latest.FromStateCode;
            projected.LatestTransitionToStateCode = latest.ToStateCode;
            projected.LatestTransitionWorldTick = latest.WorldTick;
            return true;
        }

        private static bool TryResolveCourierActivity(string stage, out string displayName)
        {
            switch (stage)
            {
                case "Idle": displayName = "대기"; return true;
                case "DriveRestaurant": displayName = "음식점으로 운전"; return true;
                case "WalkRestaurant": displayName = "음식점 입구로 걸어감"; return true;
                case "WaitFood": displayName = "음식 준비 대기"; return true;
                case "Pickup": displayName = "음식 픽업"; return true;
                case "WalkRestaurantOut": displayName = "음식점에서 차량으로 이동"; return true;
                case "DriveHome": displayName = "전달지로 운전"; return true;
                case "WalkHome": displayName = "전달지 입구로 걸어감"; return true;
                case "Deliver": displayName = "음식 전달"; return true;
                case "Receive": displayName = "수령 확인"; return true;
                case "WalkHomeOut": displayName = "전달지에서 차량으로 이동"; return true;
                case "Return": displayName = "거점으로 복귀"; return true;
                // 초기 합성 표본과 저장 사본의 호환 단계명이다.
                case "ToRestaurant": displayName = "음식점으로 이동"; return true;
                default: displayName = string.Empty; return false;
            }
        }

        private static bool Fail(string code, out string failureCode)
        {
            failureCode = code;
            return false;
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
