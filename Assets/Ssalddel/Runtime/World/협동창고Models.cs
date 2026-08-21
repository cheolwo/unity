using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 협동창고Codes
    {
        public const string Project = "coop-project:farm-small-storage.v1";
        public const string Blueprint = "blueprint:farm-coop-storage.v1";
        public const string BuildSite = "h1:Farm:coop-storage-build-site";
        public const string OwnerLot = "lot:coop.owner.components";
        public const string GuestLot = "lot:coop.guest.components";
        public const string Operational = "Operational";
        public const string Removed = "Removed";
        public const string Contribution = "CoopConstructionContribution";
        public const string Demolition = "CoopFacilityDemolition";
        public const string Restore = "CoopFacilityRestore";
    }

    [Serializable] public sealed class 협동창고ProjectData
    {
        public string ProjectStableId = string.Empty;
        public string TargetFacilityStableId = string.Empty;
        public string StageCode = string.Empty;
        public decimal RequiredMaterialQuantity;
        public decimal ContributedMaterialQuantity;
        public decimal ProgressValue;
        public long Revision;
        public string[] OpenedCapabilityCodes = Array.Empty<string>();
        public string[] OpenedWorldInteractionIds = Array.Empty<string>();
    }

    [Serializable] public sealed class 협동창고LotData
    {
        public string LotStableId = string.Empty;
        public long Revision;
        public decimal ReservedQuantity;
        public decimal RemainingQuantity;
        public string UnitCode = string.Empty;
    }

    [Serializable] public sealed class 협동창고ContributionData
    {
        public string PlayerStableId = string.Empty;
        public string SourceLotStableId = string.Empty;
        public decimal MaterialQuantity;
        public decimal EffectiveWork;
        public string StateCode = string.Empty;
    }

    [Serializable] public sealed class 협동창고CheckpointData
    {
        public string CheckpointStableId = string.Empty;
        public string CheckpointKindCode = string.Empty;
        public string SpatialStateHashSha256 = string.Empty;
        public bool HistoricalEffectsDeleted;
    }

    [Serializable] public sealed class 협동창고RestoreEffectData
    {
        public string EffectStableId = string.Empty;
        public bool DeletesHistoricalEffects;
        public bool DuplicatesResources;
    }

    [Serializable] public sealed class 협동창고StateData
    {
        public long WorldRevision;
        public int WorldTick;
        public 협동창고ProjectData[] Projects = Array.Empty<협동창고ProjectData>();
        public 협동창고ContributionData[] Contributions = Array.Empty<협동창고ContributionData>();
        public 협동창고LotData[] SourceLots = Array.Empty<협동창고LotData>();
        public 협동창고CheckpointData[] ProtectionCheckpoints = Array.Empty<협동창고CheckpointData>();
        public 협동창고RestoreEffectData[] RestoreEffects = Array.Empty<협동창고RestoreEffectData>();
        public bool UsesCompensatingEffects;
        public bool MutatesStaticHDefinitions;
        public string StateHashSha256 = string.Empty;

        public 협동창고ProjectData? Project => Projects.FirstOrDefault(value =>
            value.ProjectStableId == 협동창고Codes.Project);
        public 협동창고LotData Lot(string id) => SourceLots.Single(value =>
            value.LotStableId == id);
        public void Validate()
        {
            if (WorldRevision < 0 || WorldTick < 0 || Projects == null
                || Contributions == null || SourceLots == null
                || ProtectionCheckpoints == null || RestoreEffects == null
                || !UsesCompensatingEffects || MutatesStaticHDefinitions
                || string.IsNullOrWhiteSpace(StateHashSha256))
                throw new InvalidOperationException("CoopConstructionContextInvalid");
        }
    }

    [Serializable] public sealed class 협동창고PreviewData
    {
        public long BaseRevision;
        public string ActionCode = string.Empty;
        public string PlayerStableId = string.Empty;
        public string SourceLotStableId = string.Empty;
        public long SourceLotRevision;
        public decimal AcceptedQuantity;
        public string CurrentStageCode = string.Empty;
        public string ProjectedStageCode = string.Empty;
        public int DurationTicks;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
    }

    public interface I협동창고AuthorityClient
    {
        Task<협동창고StateData> RefreshAsync(string sessionStableId,
            CancellationToken cancellationToken);
        Task<협동창고PreviewData> PreviewContributionAsync(string sessionStableId,
            long expectedRevision, string playerStableId, string lotStableId,
            long expectedLotRevision, CancellationToken cancellationToken);
        Task<협동창고StateData> ConfirmContributionAsync(string sessionStableId,
            협동창고PreviewData preview, CancellationToken cancellationToken);
        Task<협동창고PreviewData> PreviewProtectedActionAsync(string sessionStableId,
            long expectedRevision, string actionCode, CancellationToken cancellationToken);
        Task<협동창고StateData> ConfirmProtectedActionAsync(string sessionStableId,
            협동창고PreviewData preview, CancellationToken cancellationToken);
        Task<협동창고StateData> AdvanceAsync(string sessionStableId,
            long expectedRevision, int ticks, CancellationToken cancellationToken);
    }

    public sealed class 협동창고Coordinator
    {
        private readonly I협동창고AuthorityClient authority;
        public 협동창고Coordinator(I협동창고AuthorityClient authorityClient)
            => authority = authorityClient ?? throw new ArgumentNullException(nameof(authorityClient));
        public string SessionStableId { get; private set; } = string.Empty;
        public 협동창고StateData? Current { get; private set; }
        public 협동창고PreviewData? CurrentPreview { get; private set; }

        public async Task<협동창고StateData> InitializeAsync(string sessionStableId,
            CancellationToken cancellationToken)
        {
            SessionStableId = sessionStableId;
            return await RefreshAsync(cancellationToken);
        }
        public async Task<협동창고StateData> RefreshAsync(CancellationToken cancellationToken)
        {
            var value = await authority.RefreshAsync(SessionStableId, cancellationToken);
            value.Validate();
            Current = value;
            return value;
        }
        public async Task<협동창고PreviewData> PreviewNextContributionAsync(
            CancellationToken cancellationToken)
        {
            var state = RequireCurrent();
            var lot = state.SourceLots.Where(value =>
                    (value.LotStableId == 협동창고Codes.OwnerLot
                     || value.LotStableId == 협동창고Codes.GuestLot)
                    && value.RemainingQuantity > 0m)
                .OrderBy(value => value.LotStableId == 협동창고Codes.OwnerLot ? 0 : 1)
                .ThenBy(value => value.LotStableId, StringComparer.Ordinal)
                .First();
            var player = lot.LotStableId == 협동창고Codes.OwnerLot
                ? Hosted비콘Codes.Owner : Hosted비콘Codes.Guest;
            CurrentPreview = await authority.PreviewContributionAsync(SessionStableId,
                state.WorldRevision, player, lot.LotStableId, lot.Revision,
                cancellationToken);
            return CurrentPreview;
        }
        public async Task<협동창고StateData> ConfirmAsync(CancellationToken cancellationToken)
        {
            if (CurrentPreview?.CanConfirm != true)
                throw new InvalidOperationException("CoopConstructionPreviewRequired");
            Current = await authority.ConfirmContributionAsync(SessionStableId,
                CurrentPreview, cancellationToken);
            return Current;
        }
        public async Task<협동창고PreviewData> PreviewProtectedAsync(string action,
            CancellationToken cancellationToken)
            => CurrentPreview = await authority.PreviewProtectedActionAsync(SessionStableId,
                RequireCurrent().WorldRevision, action, cancellationToken);
        public async Task<협동창고StateData> ConfirmProtectedAsync(
            CancellationToken cancellationToken)
        {
            if (CurrentPreview?.CanConfirm != true)
                throw new InvalidOperationException("CoopProtectionPreviewRequired");
            Current = await authority.ConfirmProtectedActionAsync(SessionStableId,
                CurrentPreview, cancellationToken);
            return Current;
        }
        public async Task<협동창고StateData> AdvanceAsync(
            CancellationToken cancellationToken)
        {
            if (CurrentPreview?.CanConfirm != true)
                throw new InvalidOperationException("CoopConstructionPreviewRequired");
            Current = await authority.AdvanceAsync(SessionStableId,
                RequireCurrent().WorldRevision, CurrentPreview.DurationTicks,
                cancellationToken);
            CurrentPreview = null;
            return Current;
        }
        private 협동창고StateData RequireCurrent() => Current
            ?? throw new InvalidOperationException("CoopConstructionNotInitialized");
    }
}
