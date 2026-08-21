using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Farm창고건설Codes
    {
        public const string BlueprintStableId =
            "blueprint:farm-small-storage.v1";
        public const string BuildSiteH1StableId =
            "h1:Farm:small-storage-build-site";
        public const string NotStarted = "NotStarted";
        public const string PreviewReady = "PreviewReady";
        public const string Planned = "Planned";
        public const string Building = "Building";
        public const string Operational = "Operational";
        public const string ResourceAvailable = "ResourceAvailable";
        public const string ResourceProducing = "ResourceProducing";
        public const string ResourceReady = "ResourceReady";
        public const string ResourceUnavailable = "ResourceUnavailable";
    }

    [Serializable]
    public sealed class Farm창고건설ContextData
    {
        public string SessionStableId = string.Empty;
        public long WorldRevision;
        public int WorldTick;
        public string StateCode = Farm창고건설Codes.NotStarted;
        public string FacilityStableId = string.Empty;
        public string ResourceStateCode = Farm창고건설Codes.ResourceUnavailable;
        public decimal FacilityComponentQuantity;
        public decimal StorageCapacity;
        public decimal StorageOccupied;
        public decimal StorageReserved;
        public decimal StorageAvailable;
        public string StorageUnitCode = string.Empty;
    }

    [Serializable]
    public sealed class Farm창고건설PreviewData
    {
        public long SourceWorldRevision;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public string[] ReservedLotStableIds = Array.Empty<string>();
        public string PreviewHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class FarmGameplaySaveReloadData
    {
        public string SaveStableId = string.Empty;
        public string SchemaVersion = string.Empty;
        public string ReplayHash = string.Empty;
        public int ReplayedCommandCount;
        public Farm창고건설ContextData Restored = new();
    }

    public interface IFarm창고건설AuthorityClient
    {
        Task<Farm창고건설ContextData> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken);
        Task<Farm창고건설PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<Farm창고건설PreviewData> PreviewResourceAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<Farm창고건설ContextData> ConfirmResourceAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<Farm창고건설ContextData> ConfirmAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<Farm창고건설ContextData> AdvanceOneTickAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
        Task<FarmGameplaySaveReloadData> SaveAndReloadAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken);
    }

    public sealed class Farm창고건설Coordinator
    {
        private readonly IFarm창고건설AuthorityClient authority;

        public Farm창고건설Coordinator(IFarm창고건설AuthorityClient authorityClient)
            => authority = authorityClient
                ?? throw new ArgumentNullException(nameof(authorityClient));

        public string SessionStableId { get; private set; } = string.Empty;
        public Farm창고건설ContextData? Current { get; private set; }
        public Farm창고건설PreviewData? CurrentPreview { get; private set; }
        public Farm창고건설PreviewData? CurrentResourcePreview { get; private set; }
        public FarmGameplaySaveReloadData? LastSaveReload { get; private set; }

        public async Task<Farm창고건설ContextData> InitializeAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("FarmStorageSessionMissing");
            SessionStableId = sessionStableId.Trim();
            Current = ValidateContext(await authority.RefreshAsync(
                SessionStableId, cancellationToken));
            CurrentPreview = null;
            CurrentResourcePreview = null;
            return Current;
        }

        public async Task<Farm창고건설ContextData> RefreshAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            var refreshed = ValidateContext(await authority.RefreshAsync(
                SessionStableId, cancellationToken));
            if (refreshed.WorldRevision < current.WorldRevision)
                throw new InvalidOperationException("FarmStorageRefreshWentBackwards");
            Current = refreshed;
            CurrentPreview = null;
            CurrentResourcePreview = null;
            return refreshed;
        }

        public async Task<Farm창고건설PreviewData> PreviewResourceAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (current.ResourceStateCode != Farm창고건설Codes.ResourceAvailable)
                throw new InvalidOperationException("FarmStorageResourcePreviewUnavailable");
            var preview = await authority.PreviewResourceAsync(
                SessionStableId, current.WorldRevision, cancellationToken);
            if (preview.SourceWorldRevision != current.WorldRevision)
                throw new InvalidOperationException(
                    "FarmStorageResourcePreviewRevisionMismatch");
            CurrentResourcePreview = preview;
            CurrentPreview = null;
            return preview;
        }

        public async Task<Farm창고건설ContextData> ConfirmResourceAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (CurrentResourcePreview == null
                || !CurrentResourcePreview.CanConfirm
                || CurrentResourcePreview.SourceWorldRevision != current.WorldRevision)
                throw new InvalidOperationException("FarmStorageResourcePreviewRequired");
            var next = ValidateContext(await authority.ConfirmResourceAsync(
                SessionStableId, current.WorldRevision, cancellationToken));
            if (next.WorldRevision != current.WorldRevision + 1
                || next.ResourceStateCode
                    != Farm창고건설Codes.ResourceProducing)
                throw new InvalidOperationException(
                    "FarmStorageResourceConfirmAuthorityMismatch");
            Current = next;
            CurrentResourcePreview = null;
            return next;
        }

        public async Task<Farm창고건설PreviewData> PreviewAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (current.StateCode != Farm창고건설Codes.NotStarted)
                throw new InvalidOperationException("FarmStorageConstructionAlreadyStarted");
            var preview = await authority.PreviewAsync(
                SessionStableId, current.WorldRevision, cancellationToken);
            if (preview.SourceWorldRevision != current.WorldRevision)
                throw new InvalidOperationException("FarmStoragePreviewRevisionMismatch");
            CurrentPreview = preview;
            CurrentResourcePreview = null;
            return preview;
        }

        public async Task<Farm창고건설ContextData> ConfirmAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (CurrentPreview == null || !CurrentPreview.CanConfirm
                || CurrentPreview.SourceWorldRevision != current.WorldRevision)
                throw new InvalidOperationException("FarmStoragePreviewRequired");
            var next = ValidateContext(await authority.ConfirmAsync(
                SessionStableId, current.WorldRevision, cancellationToken));
            if (next.WorldRevision != current.WorldRevision + 1
                || next.StateCode != Farm창고건설Codes.Planned)
                throw new InvalidOperationException("FarmStorageConfirmAuthorityMismatch");
            Current = next;
            CurrentPreview = null;
            CurrentResourcePreview = null;
            return next;
        }

        public async Task<Farm창고건설ContextData> AdvanceOneTickAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (current.StateCode != Farm창고건설Codes.Planned
                && current.StateCode != Farm창고건설Codes.Building
                && current.ResourceStateCode
                    != Farm창고건설Codes.ResourceProducing)
                throw new InvalidOperationException("FarmStorageConstructionTickUnavailable");
            var next = ValidateContext(await authority.AdvanceOneTickAsync(
                SessionStableId, current.WorldRevision, cancellationToken));
            if (next.WorldRevision != current.WorldRevision + 1
                || next.WorldTick != current.WorldTick + 1)
                throw new InvalidOperationException("FarmStorageTickAuthorityMismatch");
            Current = next;
            CurrentPreview = null;
            CurrentResourcePreview = null;
            return next;
        }

        public async Task<FarmGameplaySaveReloadData> SaveAndReloadAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (current.StateCode != Farm창고건설Codes.Operational)
                throw new InvalidOperationException("FarmGameplaySaveUnavailable");
            var result = await authority.SaveAndReloadAsync(
                SessionStableId, current.WorldRevision, cancellationToken);
            if (result == null || string.IsNullOrWhiteSpace(result.SaveStableId)
                || string.IsNullOrWhiteSpace(result.SchemaVersion)
                || string.IsNullOrWhiteSpace(result.ReplayHash)
                || result.Restored == null
                || result.Restored.WorldRevision != current.WorldRevision
                || result.Restored.WorldTick != current.WorldTick
                || result.Restored.StorageCapacity != current.StorageCapacity
                || result.Restored.StorageOccupied != current.StorageOccupied
                || result.Restored.StorageReserved != current.StorageReserved
                || result.Restored.StateCode != current.StateCode)
                throw new InvalidOperationException("FarmGameplaySaveReloadMismatch");
            LastSaveReload = result;
            return result;
        }

        private Farm창고건설ContextData RequireCurrent()
            => Current ?? throw new InvalidOperationException(
                "FarmStorageCoordinatorNotInitialized");

        private Farm창고건설ContextData ValidateContext(
            Farm창고건설ContextData context)
        {
            if (context == null
                || context.SessionStableId != SessionStableId
                || context.WorldRevision < 0
                || context.WorldTick < 0
                || context.StorageCapacity < 0m
                || context.StorageOccupied < 0m
                || context.StorageReserved < 0m
                || context.StorageAvailable
                    != context.StorageCapacity - context.StorageOccupied
                        - context.StorageReserved
                || string.IsNullOrWhiteSpace(context.ResourceStateCode)
                || context.FacilityComponentQuantity < 0m
                || string.IsNullOrWhiteSpace(context.StorageUnitCode))
                throw new InvalidOperationException("FarmStorageContextInvalid");
            return context;
        }
    }
}
