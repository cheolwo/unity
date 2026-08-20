using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 오늘작업계획Codes
    {
        public const string Harvesting = "Harvesting";
        public const string HarvestCollection = "HarvestCollection";
        public const string OutboundPacking = "OutboundPacking";
        public const string PlayerDirect = "PlayerDirect";
        public const string NpcDelegated = "NpcDelegated";
        public const string PlayerActor = "actor:unity:harvest-day:player";
        public const string NpcActor = "actor:unity:harvest-day:npc";
        public const string ProductionPlotSpatial =
            "spatial:unity:harvest-day:production-plot";
        public const string WorkYardSpatial =
            "spatial:unity:harvest-day:work-yard";
    }

    [Serializable]
    public sealed class 오늘작업계획ItemData
    {
        public string PlanItemStableId = string.Empty;
        public int Priority;
        public string ActorStableId = string.Empty;
        public string TargetStableId = string.Empty;
        public string ActionCode = string.Empty;
        public string AssignmentKindCode = string.Empty;
        public string PreferredSpatialStableId = string.Empty;
    }

    [Serializable]
    public sealed class 오늘작업계획ItemPreviewData
    {
        public string PlanItemStableId = string.Empty;
        public int Priority;
        public string ActorStableId = string.Empty;
        public string TargetStableId = string.Empty;
        public string ActionCode = string.Empty;
        public string AssignmentKindCode = string.Empty;
        public decimal ProjectedQuantity;
        public string ProjectedQuantityUnitCode = string.Empty;
        public int DurationTicks;
        public int EstimatedCompletionWorldTick;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class 오늘작업계획PreviewData
    {
        public long ExpectedRevision;
        public 오늘작업계획ItemPreviewData[] Items = Array.Empty<오늘작업계획ItemPreviewData>();
        public decimal TotalRequiredLabor;
        public decimal TotalStaminaCost;
        public int EstimatedCompletionWorldTick;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
    }

    [Serializable]
    public sealed class 오늘작업CanonicalStateData
    {
        public long WorldRevision;
        public int WorldTick;
        public 오늘작업OrderData[] WorkOrders = Array.Empty<오늘작업OrderData>();
        public 오늘수확LotData[] HarvestLots = Array.Empty<오늘수확LotData>();
        public 오늘포장LotData[] PackageLots = Array.Empty<오늘포장LotData>();
    }

    [Serializable]
    public sealed class 오늘작업OrderData
    {
        public string WorkOrderStableId = string.Empty;
        public string TargetStableId = string.Empty;
        public string ActionCode = string.Empty;
        public string AssignmentKindCode = string.Empty;
        public string StatusCode = string.Empty;
        public int CompletesWorldTick;
    }

    [Serializable]
    public sealed class 오늘수확LotData
    {
        public string HarvestLotStableId = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
        public string StateCode = string.Empty;
    }

    [Serializable]
    public sealed class 오늘포장LotData
    {
        public string PackageLotStableId = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
        public string StateCode = string.Empty;
    }

    public interface I오늘작업계획AuthorityClient
    {
        Task<오늘작업계획PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision,
            오늘작업계획ItemData[] items, CancellationToken cancellationToken);
        Task<오늘작업CanonicalStateData> ConfirmAsync(
            string sessionStableId, string commandId, long expectedRevision,
            오늘작업계획ItemData[] items, CancellationToken cancellationToken);
        Task<오늘작업CanonicalStateData> AdvanceOneTickAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<오늘작업CanonicalStateData> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken);
    }

    public sealed class 오늘작업계획Coordinator
    {
        private readonly I오늘작업계획AuthorityClient authority;

        public 오늘작업계획Coordinator(I오늘작업계획AuthorityClient authorityClient)
            => authority = authorityClient
                ?? throw new ArgumentNullException(nameof(authorityClient));

        public string SessionStableId { get; private set; } = string.Empty;
        public long Revision { get; private set; }
        public 오늘작업계획PreviewData? CurrentPreview { get; private set; }
        public 오늘작업CanonicalStateData? CurrentState { get; private set; }

        public void Bind(string sessionStableId, long revision)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId) || revision < 0)
                throw new InvalidOperationException("DailyWorkPlanSessionInvalid");
            SessionStableId = sessionStableId.Trim();
            Revision = revision;
            CurrentPreview = null;
        }

        public async Task<오늘작업계획PreviewData> PreviewAsync(
            오늘작업계획ItemData[] items, CancellationToken cancellationToken)
        {
            ValidateItems(items);
            EnsureBound();
            var preview = await authority.PreviewAsync(
                SessionStableId, Revision, items, cancellationToken);
            if (preview.ExpectedRevision != Revision
                || preview.Items.Length != items.Length
                || preview.Items.Select(value => value.PlanItemStableId)
                    .Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new InvalidOperationException("DailyWorkPlanPreviewAuthorityMismatch");
            CurrentPreview = preview;
            return preview;
        }

        public async Task<오늘작업CanonicalStateData> ConfirmAsync(
            string commandId, 오늘작업계획ItemData[] items,
            CancellationToken cancellationToken)
        {
            ValidateItems(items);
            EnsureBound();
            if (CurrentPreview == null || !CurrentPreview.CanConfirm)
                throw new InvalidOperationException("DailyWorkPlanPreviewRequired");
            if (string.IsNullOrWhiteSpace(commandId))
                throw new InvalidOperationException("DailyWorkPlanCommandInvalid");
            var state = await authority.ConfirmAsync(
                SessionStableId, commandId.Trim(), Revision, items, cancellationToken);
            if (state.WorldRevision != Revision + 1)
                throw new InvalidOperationException("DailyWorkPlanCanonicalRevisionMismatch");
            Revision = state.WorldRevision;
            CurrentState = state;
            CurrentPreview = null;
            return state;
        }

        public async Task<오늘작업CanonicalStateData> AdvanceOneTickAsync(
            CancellationToken cancellationToken)
        {
            EnsureBound();
            var state = await authority.AdvanceOneTickAsync(
                SessionStableId, Revision, cancellationToken);
            if (state.WorldRevision != Revision + 1)
                throw new InvalidOperationException("DailyWorkTickCanonicalRevisionMismatch");
            Revision = state.WorldRevision;
            CurrentState = state;
            return state;
        }

        private void EnsureBound()
        {
            if (SessionStableId.Length == 0)
                throw new InvalidOperationException("DailyWorkPlanSessionRequired");
        }

        private static void ValidateItems(오늘작업계획ItemData[] items)
        {
            if (items == null || items.Length == 0 || items.Length > 32
                || items.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.PlanItemStableId)
                    || string.IsNullOrWhiteSpace(value.ActorStableId)
                    || string.IsNullOrWhiteSpace(value.TargetStableId)
                    || string.IsNullOrWhiteSpace(value.ActionCode)
                    || string.IsNullOrWhiteSpace(value.AssignmentKindCode))
                || items.Select(value => value.PlanItemStableId)
                    .Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new InvalidOperationException("DailyWorkPlanItemsInvalid");
        }
    }
}
