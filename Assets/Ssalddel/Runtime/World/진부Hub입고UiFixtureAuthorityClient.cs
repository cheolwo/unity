using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public sealed class 진부Hub입고UiFixtureAuthorityClient : I진부Hub입고UiAuthorityClient
    {
        private const string SessionId = "simulation-session:unity-jinbu-inbound-ui.fixture";
        private int phase;
        private int workTicks;
        private long revision = 11;
        private long worldTick = 5;

        public Task<진부Hub입고UiProjectionData> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Project());
        }

        public Task<진부Hub입고UiPreviewData> PreviewAsync(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((phase != 0 && phase != 2) || action.Invocation == null)
                throw new InvalidOperationException("JinbuInboundUiFixturePreviewUnavailable");
            return Task.FromResult(new 진부Hub입고UiPreviewData
            {
                ActionStableId = action.StableId,
                ActionLabel = action.KoreanLabel,
                TargetStableId = action.Invocation.TargetStableId,
                ActorStableId = action.Invocation.ActorStableId,
                DurationTicks = action.Invocation.DurationTicks,
                TaskStableId = phase == 0
                    ? "task:sim:jinbu-inbound-inspection.fixture"
                    : "task:sim:jinbu-put-away.fixture",
            });
        }

        public Task<진부Hub입고UiProjectionData> ConfirmAsync(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (action.Invocation == null || action.Invocation.ExpectedStateRevision != revision)
                throw new InvalidOperationException("JinbuInboundUiFixtureRevisionMismatch");
            if (phase == 0) phase = 1;
            else if (phase == 2) phase = 3;
            else throw new InvalidOperationException("JinbuInboundUiFixtureConfirmUnavailable");
            workTicks = 0;
            revision++;
            return Task.FromResult(Project());
        }

        public Task<진부Hub입고UiProjectionData> AdvanceAsync(
            진부Hub입고UiProjectionData projection,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (phase != 1 && phase != 3)
                throw new InvalidOperationException("JinbuInboundUiFixtureTickUnavailable");
            revision++;
            worldTick++;
            workTicks++;
            if (workTicks >= 3) phase = phase == 1 ? 2 : 4;
            return Task.FromResult(Project());
        }

        private 진부Hub입고UiProjectionData Project()
        {
            var ready = phase == 0 || phase == 2;
            var working = phase == 1 || phase == 3;
            var putAway = phase >= 2;
            var state = working ? 진부Hub입고UiCodes.InProgress
                : phase == 4 ? 진부Hub입고UiCodes.Completed
                : 진부Hub입고UiCodes.Ready;
            var target = putAway
                ? "inventory:sim:jinbu-potato.fixture"
                : "freight-transport:sim:jinbu-potato.fixture";
            var actor = putAway
                ? "actor:sim:pyeongchang:jinbu-logistics-assistant"
                : "actor:sim:pyeongchang:jinbu-inbound-operator";
            var actionSubject = putAway ? "적재" : "입고 검수";
            var invocation = ready ? new 진부Hub입고UiInvocationData
            {
                TargetStableId = target,
                TargetRevision = putAway ? 1 : 6,
                ActorStableId = actor,
                ExpectedStateRevision = revision,
                DurationTicks = 2,
                SourceStableIds = new[] { target, "source:fixture:jinbu-inbound-ui" },
            } : null;
            return new 진부Hub입고UiProjectionData
            {
                UI기획개정번호 = "pyeongchang-farm-hub-town-ui-plan.v3",
                업무규칙대장개정번호 = "pyeongchang-farm-hub-town-business-rules.v2",
                DesignProfileRevision = 진부Hub입고UiCodes.SupportedDesignProfileRevision,
                SessionStableId = SessionId,
                StateRevision = revision,
                WorldTick = worldTick,
                SurfaceStableId = 진부Hub입고UiCodes.SurfaceStableId,
                FacilityStableId = "facility:sim:pyeongchang:jinbu-hub",
                SurfaceKindCode = "TaskDetailPanel",
                LayoutProfileCode = "WorldSidePanel",
                RoleCode = "Warehouse",
                RoleStyleSemanticKey = "Role.Warehouse",
                WorkflowCode = "WarehouseInbound",
                WorkflowStageCode = phase == 0 ? "Expected"
                    : phase == 1 ? "PendingInspection"
                    : phase == 2 || phase == 3 ? "PutAwayPending"
                    : "PutAwayCompleted",
                ExecutionModeCode = "Simulation",
                StateCode = state,
                KoreanTitle = "진부면 물류 거점 입출고",
                StateKoreanLabel = state == 진부Hub입고UiCodes.Completed ? "완료"
                    : state == 진부Hub입고UiCodes.InProgress ? "진행 중" : "검토 가능",
                PresentationIntentCode = state,
                StateStyleSemanticKey = "State." + state,
                ProjectedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                InformationItems = new[]
                {
                    Item("Summary", "업무 요약", phase == 4
                        ? "도착 화물 1건 · 적재 완료 1건"
                        : putAway ? "검수 완료 · 적재 대기 1건" : "도착 화물 1건 · 검수 대기"),
                    Item("Status", "현재 상태", working
                        ? (putAway ? "NPC 적재 작업 중" : "NPC 검수 작업 중")
                        : state == 진부Hub입고UiCodes.Completed ? "적재 완료" : actionSubject + " 준비"),
                    Item("NextStep", "다음 단계", working
                        ? "NPC 업무 행동을 WorldTick으로 진행"
                        : phase == 4 ? "적재 완료된 재고 확인" : actionSubject + " 미리보기 후 명시적으로 확정"),
                    Item("Evidence", "판정 근거", putAway
                        ? "창고 적재 규칙 · 업무 규칙 v2" : "창고 입고 검수 규칙 · 업무 규칙 v2", "Information.Evidence"),
                    Item("Limitation", "표현 한계", "Simulation 상태이며 실제 입고·재고를 변경하지 않음", "Information.Limitation"),
                    Item("Refresh", "확정 뒤 원장 재조회", "상태 사본 r" + revision + " · WorldTick " + worldTick),
                },
                Actions = new[]
                {
                    Action("Inspect", "입출고 상세 보기", true, null, string.Empty),
                    Action("Preview", actionSubject + " 미리보기", ready, invocation,
                        putAway ? "SimulationWarehousePutAwayPreviewRequest" : "SimulationFreightReceiptPreviewRequest"),
                    Action("Confirm", actionSubject + " 확정", ready, invocation,
                        putAway ? "SimulationWarehousePutAwayConfirmRequest" : "SimulationFreightReceiptConfirmRequest"),
                },
            };
        }

        private static 진부Hub입고UiItemData Item(
            string kind, string label, string value,
            string style = "Information.Default")
            => new 진부Hub입고UiItemData
            {
                StableId = "ui-info:fixture:" + kind.ToLowerInvariant(),
                InformationKindCode = kind,
                KoreanLabel = label,
                StyleSemanticKey = style,
                ValueText = value,
                DataStatusCode = kind == "Limitation" ? "Scenario" : "Derived",
                SourceStableId = "source:fixture:jinbu-inbound-ui",
            };

        private static 진부Hub입고UiActionData Action(
            string kind, string label, bool enabled,
            진부Hub입고UiInvocationData invocation,
            string contract)
            => new 진부Hub입고UiActionData
            {
                StableId = "ui-action:fixture:" + kind.ToLowerInvariant(),
                ActionKindCode = kind,
                KoreanLabel = label,
                StyleSemanticKey = kind == "Confirm" ? "Action.Confirm"
                    : kind == "Preview" ? "Action.Preview" : "Action.Secondary",
                Enabled = enabled,
                BlockReasonCode = enabled ? string.Empty : "SimulationWorldUiWorkInProgress",
                RequiresPreview = kind == "Confirm",
                RequiresExplicitConfirmation = kind == "Confirm",
                RequiresExpectedRevision = kind == "Confirm",
                HttpMethod = kind == "Inspect" ? "GET" : "POST",
                RequestContractKey = contract,
                Invocation = invocation,
            };
    }
}
