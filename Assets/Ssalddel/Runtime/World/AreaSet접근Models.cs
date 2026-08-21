using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class AreaSet접근Codes
    {
        public const string PlayerOwner = "player:session:owner";
        public const string FarmAreaSet = "area-set:sim:pyeongchang:farm-production.v1";
        public const string HubAreaSet = "area-set:sim:pyeongchang:logistics-hub.v1";
        public const string FarmToHubConnector = "area-set-relation:actual-e5:farmtocityhub";
        public const string Locked = "Locked";
        public const string Granted = "Granted";
        public const string Entered = "Entered";
    }

    [Serializable] public sealed class AreaSet접근EntryData
    {
        public string AreaSetStableId = string.Empty;
        public string AccessStateCode = string.Empty;
        public string[] GrantedByEvidenceIds = Array.Empty<string>();
        public string[] AvailableWorldInteractionIds = Array.Empty<string>();
        public string SourceHDefinitionHashSha256 = string.Empty;
        public string AccessHashSha256 = string.Empty;
    }

    [Serializable] public sealed class AreaSet접근Data
    {
        public long WorldRevision;
        public int WorldTick;
        public string CurrentAreaSetStableId = string.Empty;
        public AreaSet접근EntryData[] AccessEntries = Array.Empty<AreaSet접근EntryData>();
        public bool MutatesStaticHDefinitions;

        public AreaSet접근EntryData Hub => AccessEntries.Single(value =>
            value.AreaSetStableId == AreaSet접근Codes.HubAreaSet);

        public void Validate()
        {
            if (WorldRevision < 0 || WorldTick < 0
                || string.IsNullOrWhiteSpace(CurrentAreaSetStableId)
                || AccessEntries == null || AccessEntries.Length < 2
                || AccessEntries.Any(value => string.IsNullOrWhiteSpace(value.AreaSetStableId)
                    || string.IsNullOrWhiteSpace(value.AccessStateCode)
                    || string.IsNullOrWhiteSpace(value.SourceHDefinitionHashSha256)
                    || string.IsNullOrWhiteSpace(value.AccessHashSha256))
                || MutatesStaticHDefinitions)
                throw new InvalidOperationException("AreaSetAccessContextInvalid");
        }
    }

    [Serializable] public sealed class AreaSet이동PreviewData
    {
        public long BaseRevision;
        public string TargetAreaSetStableId = string.Empty;
        public string ConnectorStableId = string.Empty;
        public string AccessStateCode = string.Empty;
        public string[] EvidenceIds = Array.Empty<string>();
        public string[] NewWorldInteractionIds = Array.Empty<string>();
        public int DurationTicks;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public string PreviewHashSha256 = string.Empty;
    }

    public interface IAreaSet접근AuthorityClient
    {
        Task<AreaSet접근Data> RefreshAsync(string sessionStableId,
            string playerStableId, CancellationToken cancellationToken);
        Task<AreaSet이동PreviewData> PreviewAsync(string sessionStableId,
            string playerStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<AreaSet접근Data> ConfirmAsync(string sessionStableId,
            string playerStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<AreaSet접근Data> AdvanceAsync(string sessionStableId,
            string playerStableId, long expectedRevision, int tickCount,
            CancellationToken cancellationToken);
    }

    public sealed class AreaSet접근Coordinator
    {
        private readonly IAreaSet접근AuthorityClient authority;
        public AreaSet접근Coordinator(IAreaSet접근AuthorityClient authorityClient)
            => authority = authorityClient ?? throw new ArgumentNullException(nameof(authorityClient));
        public string SessionStableId { get; private set; } = string.Empty;
        public string PlayerStableId { get; private set; } = string.Empty;
        public AreaSet접근Data? Current { get; private set; }
        public AreaSet이동PreviewData? CurrentPreview { get; private set; }

        public async Task<AreaSet접근Data> InitializeAsync(string sessionStableId,
            string playerStableId, CancellationToken cancellationToken)
        {
            SessionStableId = sessionStableId;
            PlayerStableId = playerStableId;
            return await RefreshAsync(cancellationToken);
        }

        public async Task<AreaSet접근Data> RefreshAsync(CancellationToken cancellationToken)
        {
            var value = await authority.RefreshAsync(SessionStableId, PlayerStableId, cancellationToken);
            value.Validate();
            if (Current != null && value.WorldRevision < Current.WorldRevision)
                throw new InvalidOperationException("AreaSetAccessRevisionRegressed");
            Current = value;
            return value;
        }

        public async Task<AreaSet이동PreviewData> PreviewHubAsync(CancellationToken cancellationToken)
        {
            EnsureReady();
            CurrentPreview = await authority.PreviewAsync(SessionStableId,
                PlayerStableId, Current!.WorldRevision, cancellationToken);
            return CurrentPreview;
        }

        public async Task<AreaSet접근Data> ConfirmHubAsync(CancellationToken cancellationToken)
        {
            EnsureReady();
            if (CurrentPreview?.CanConfirm != true)
                throw new InvalidOperationException("AreaSetTraversalPreviewRequired");
            Current = await authority.ConfirmAsync(SessionStableId, PlayerStableId,
                CurrentPreview.BaseRevision, cancellationToken);
            return Current;
        }

        public async Task<AreaSet접근Data> AdvanceToHubAsync(CancellationToken cancellationToken)
        {
            EnsureReady();
            if (CurrentPreview?.CanConfirm != true)
                throw new InvalidOperationException("AreaSetTraversalPreviewRequired");
            Current = await authority.AdvanceAsync(SessionStableId, PlayerStableId,
                Current!.WorldRevision, CurrentPreview.DurationTicks, cancellationToken);
            CurrentPreview = null;
            return Current;
        }

        private void EnsureReady()
        {
            if (Current == null) throw new InvalidOperationException("AreaSetAccessNotInitialized");
        }
    }
}
