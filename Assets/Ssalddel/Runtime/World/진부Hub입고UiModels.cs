using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 진부Hub입고UiCodes
    {
        public const string SurfaceStableId = "ui-surface:sim:pyeongchang:hub-operations";
        public const string SupportedDesignProfileRevision = "figma-maui-warehouse.v1";
        public const string PreviewAction = "Preview";
        public const string ConfirmAction = "Confirm";
        public const string Ready = "Ready";
        public const string PreviewReady = "PreviewReady";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Blocked = "Blocked";
        public const string Stale = "Stale";
        public const string Error = "Error";
    }

    [Serializable]
    public sealed class 진부Hub입고UiProjectionData
    {
        public string UI기획개정번호 = string.Empty;
        public string 업무규칙대장개정번호 = string.Empty;
        public string DesignProfileRevision = string.Empty;
        public string SessionStableId = string.Empty;
        public long StateRevision;
        public long WorldTick;
        public string SurfaceStableId = string.Empty;
        public string FacilityStableId = string.Empty;
        public string SurfaceKindCode = string.Empty;
        public string LayoutProfileCode = string.Empty;
        public string RoleCode = string.Empty;
        public string RoleStyleSemanticKey = string.Empty;
        public string WorkflowCode = string.Empty;
        public string WorkflowStageCode = string.Empty;
        public string ExecutionModeCode = string.Empty;
        public string StateCode = string.Empty;
        public string KoreanTitle = string.Empty;
        public string StateKoreanLabel = string.Empty;
        public string PresentationIntentCode = string.Empty;
        public string StateStyleSemanticKey = string.Empty;
        public string ProjectedAtUtc = string.Empty;
        public 진부Hub입고UiItemData[] InformationItems = Array.Empty<진부Hub입고UiItemData>();
        public 진부Hub입고UiActionData[] Actions = Array.Empty<진부Hub입고UiActionData>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SessionStableId)
                || string.IsNullOrWhiteSpace(SurfaceStableId)
                || SurfaceStableId != 진부Hub입고UiCodes.SurfaceStableId)
                throw new InvalidOperationException("JinbuInboundUiProjectionIdentityInvalid");
            if (StateRevision < 0 || WorldTick < 0)
                throw new InvalidOperationException("JinbuInboundUiProjectionRevisionInvalid");
            InformationItems ??= Array.Empty<진부Hub입고UiItemData>();
            Actions ??= Array.Empty<진부Hub입고UiActionData>();
        }

        public 진부Hub입고UiActionData Action(string kind)
            => Actions.Single(value => value.ActionKindCode == kind);

        public string Information(string kind)
            => InformationItems.FirstOrDefault(value => value.InformationKindCode == kind)?.ValueText
               ?? string.Empty;
    }

    [Serializable]
    public sealed class 진부Hub입고UiItemData
    {
        public string StableId = string.Empty;
        public string InformationKindCode = string.Empty;
        public string KoreanLabel = string.Empty;
        public string StyleSemanticKey = string.Empty;
        public string ValueText = string.Empty;
        public string UnitCode = string.Empty;
        public string DataStatusCode = string.Empty;
        public string SourceStableId = string.Empty;
        public string ObservedAtUtc = string.Empty;
        public string LimitationCode = string.Empty;
    }

    [Serializable]
    public sealed class 진부Hub입고UiActionData
    {
        public string StableId = string.Empty;
        public string ActionKindCode = string.Empty;
        public string KoreanLabel = string.Empty;
        public string StyleSemanticKey = string.Empty;
        public string CapabilityKey = string.Empty;
        public string CanonicalActionCode = string.Empty;
        public string ServerCommandKey = string.Empty;
        public bool Enabled;
        public string BlockReasonCode = string.Empty;
        public bool RequiresPreview;
        public bool RequiresExplicitConfirmation;
        public bool RequiresExpectedRevision;
        public string HttpMethod = string.Empty;
        public string RouteTemplate = string.Empty;
        public string RequestContractKey = string.Empty;
        public string ResponseContractKey = string.Empty;
        public string CanonicalRequeryRouteTemplate = string.Empty;
        public 진부Hub입고UiInvocationData Invocation;
    }

    [Serializable]
    public sealed class 진부Hub입고UiInvocationData
    {
        public string TargetStableId = string.Empty;
        public long TargetRevision;
        public string ActorStableId = string.Empty;
        public long ExpectedStateRevision;
        public int DurationTicks;
        public string[] SourceStableIds = Array.Empty<string>();
    }

    public sealed class 진부Hub입고UiPreviewData
    {
        public string ActionStableId { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = string.Empty;
        public string TargetStableId { get; set; } = string.Empty;
        public string ActorStableId { get; set; } = string.Empty;
        public int DurationTicks { get; set; }
        public string TaskStableId { get; set; } = string.Empty;
        public string[] BlockReasonCodes { get; set; } = Array.Empty<string>();
        public bool CanConfirm => BlockReasonCodes.Length == 0;
    }

    public interface I진부Hub입고UiAuthorityClient
    {
        Task<진부Hub입고UiProjectionData> LoadAsync(
            string sessionStableId, CancellationToken cancellationToken);
        Task<진부Hub입고UiPreviewData> PreviewAsync(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            CancellationToken cancellationToken);
        Task<진부Hub입고UiProjectionData> ConfirmAsync(
            진부Hub입고UiProjectionData projection,
            진부Hub입고UiActionData action,
            CancellationToken cancellationToken);
        Task<진부Hub입고UiProjectionData> AdvanceAsync(
            진부Hub입고UiProjectionData projection,
            CancellationToken cancellationToken);
    }

    public sealed class 진부Hub입고UiCoordinator
    {
        private readonly I진부Hub입고UiAuthorityClient authority;

        public 진부Hub입고UiCoordinator(I진부Hub입고UiAuthorityClient client)
            => authority = client ?? throw new ArgumentNullException(nameof(client));

        public 진부Hub입고UiProjectionData CurrentProjection { get; private set; }
        public 진부Hub입고UiPreviewData CurrentPreview { get; private set; }
        public bool IsStale { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;
        public string PhaseCode => IsStale
            ? 진부Hub입고UiCodes.Stale
            : CurrentPreview != null
                ? 진부Hub입고UiCodes.PreviewReady
                : CurrentProjection?.StateCode ?? 진부Hub입고UiCodes.Error;

        public async Task LoadAsync(string sessionStableId, CancellationToken cancellationToken)
        {
            try
            {
                Apply(await authority.LoadAsync(sessionStableId, cancellationToken));
                CurrentPreview = null;
            }
            catch (Exception exception)
            {
                Fail(exception);
                throw;
            }
        }

        public async Task PreviewAsync(CancellationToken cancellationToken)
        {
            EnsureProjection();
            var action = CurrentProjection.Action(진부Hub입고UiCodes.PreviewAction);
            if (!action.Enabled || action.Invocation == null)
                throw new InvalidOperationException(action.BlockReasonCode.Length == 0
                    ? "JinbuInboundUiPreviewUnavailable"
                    : action.BlockReasonCode);
            CurrentPreview = await authority.PreviewAsync(CurrentProjection, action, cancellationToken);
            IsStale = false;
            ErrorCode = string.Empty;
        }

        public async Task ConfirmAsync(CancellationToken cancellationToken)
        {
            EnsureProjection();
            if (CurrentPreview == null || !CurrentPreview.CanConfirm)
                throw new InvalidOperationException("JinbuInboundUiPreviewRequired");
            var action = CurrentProjection.Action(진부Hub입고UiCodes.ConfirmAction);
            if (!action.Enabled || action.Invocation == null
                || action.Invocation.TargetStableId != CurrentPreview.TargetStableId)
                throw new InvalidOperationException("JinbuInboundUiConfirmTargetMismatch");
            Apply(await authority.ConfirmAsync(CurrentProjection, action, cancellationToken));
            CurrentPreview = null;
        }

        public async Task AdvanceAsync(CancellationToken cancellationToken)
        {
            EnsureProjection();
            if (CurrentProjection.StateCode != 진부Hub입고UiCodes.InProgress)
                throw new InvalidOperationException("JinbuInboundUiTickUnavailable");
            Apply(await authority.AdvanceAsync(CurrentProjection, cancellationToken));
        }

        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            EnsureProjection();
            await LoadAsync(CurrentProjection.SessionStableId, cancellationToken);
        }

        public void MarkStale(Exception exception) => Fail(exception);

        private void Apply(진부Hub입고UiProjectionData projection)
        {
            projection.Validate();
            if (CurrentProjection != null
                && (projection.SessionStableId != CurrentProjection.SessionStableId
                    || projection.StateRevision < CurrentProjection.StateRevision))
                throw new InvalidOperationException("JinbuInboundUiCanonicalRequeryInvalid");
            CurrentProjection = projection;
            IsStale = false;
            ErrorCode = string.Empty;
        }

        private void EnsureProjection()
        {
            if (CurrentProjection == null)
                throw new InvalidOperationException("JinbuInboundUiProjectionMissing");
        }

        private void Fail(Exception exception)
        {
            IsStale = CurrentProjection != null;
            ErrorCode = exception.Message;
        }
    }
}
