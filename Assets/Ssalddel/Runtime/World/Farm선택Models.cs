using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Farm선택Codes
    {
        public const string SituationStableId =
            "farm-situation:potato-harvest-complete.v1";
        public const string AwaitingHarvest = "AwaitingHarvest";
        public const string AwaitingChoice = "AwaitingChoice";
        public const string ChoiceConfirmed = "ChoiceConfirmed";

        public const string ReserveStorageChoice =
            "farm-choice:potato.reserve-storage.v1";
        public const string HubShipmentChoice =
            "farm-choice:potato.hub-shipment.v1";
        public const string TownDirectSaleChoice =
            "farm-choice:potato.town-direct-sale.v1";
    }

    public sealed class Farm선택FactData
    {
        public string FactStableId { get; set; } = string.Empty;
        public string FactCode { get; set; } = string.Empty;
        public string TargetStableId { get; set; } = string.Empty;
        public string ValueCode { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class Farm선택CandidateReasonData
    {
        public string ReasonCode { get; set; } = string.Empty;
        public string[] SourceFactStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class Farm선택CandidateData
    {
        public string ChoiceStableId { get; set; } = string.Empty;
        public string ChoiceCode { get; set; } = string.Empty;
        public string CardFunctionCode { get; set; } = string.Empty;
        public string KoreanDisplayName { get; set; } = string.Empty;
        public string NextWorkflowCode { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string[] BlockReasonCodes { get; set; } = Array.Empty<string>();
        public Farm선택CandidateReasonData[] CandidateReasons { get; set; }
            = Array.Empty<Farm선택CandidateReasonData>();
    }

    public sealed class Farm선택ContextData
    {
        public string SessionStableId { get; set; } = string.Empty;
        public string SituationStableId { get; set; } = string.Empty;
        public int SituationRevision { get; set; }
        public long WorldRevision { get; set; }
        public int WorldTick { get; set; }
        public string AreaSetStableId { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string HarvestLotStableId { get; set; } = string.Empty;
        public string SituationStateCode { get; set; } = string.Empty;
        public string AppliedChoiceStableId { get; set; } = string.Empty;
        public bool IsSimulationOnly { get; set; }
        public bool IsOperationalState { get; set; }
        public Farm선택FactData[] Facts { get; set; } = Array.Empty<Farm선택FactData>();
        public Farm선택CandidateData[] Candidates { get; set; }
            = Array.Empty<Farm선택CandidateData>();
    }

    public sealed class Farm선택PreviewData
    {
        public string SituationStableId { get; set; } = string.Empty;
        public string ChoiceStableId { get; set; } = string.Empty;
        public long BaseRevision { get; set; }
        public bool IsCandidateOnly { get; set; }
        public bool RequiresExplicitConfirm { get; set; }
        public 정착지상호작용PreviewData Impact { get; set; }
            = new 정착지상호작용PreviewData();
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface IFarm선택AuthorityClient
    {
        Task<Farm선택ContextData> GetFarmChoiceContextAsync(
            string sessionStableId,
            CancellationToken cancellationToken);
        Task<Farm선택PreviewData> PreviewFarmChoiceAsync(
            string sessionStableId,
            long expectedRevision,
            string choiceStableId,
            CancellationToken cancellationToken);
        Task<정착지상호작용AuthoritySnapshot> ConfirmFarmChoiceAsync(
            string sessionStableId,
            long expectedRevision,
            string choiceStableId,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// 실제 서버의 Farm 선택 문맥을 사용한다. Unity는 선택 고유 식별자와
    /// 예상 revision만 전달하며 수확량, 비용, 결과를 만들지 않는다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class Farm선택Coordinator
    {
        private readonly IFarm선택AuthorityClient farmAuthority;
        private readonly I정착지상호작용AuthorityClient sessionAuthority;

        public Farm선택Coordinator(
            IFarm선택AuthorityClient farmChoiceAuthority,
            I정착지상호작용AuthorityClient sessionSnapshotAuthority,
            정착지상호작용AuthoritySnapshot initialSnapshot,
            Farm선택ContextData initialContext)
        {
            farmAuthority = farmChoiceAuthority
                ?? throw new ArgumentNullException(nameof(farmChoiceAuthority));
            sessionAuthority = sessionSnapshotAuthority
                ?? throw new ArgumentNullException(nameof(sessionSnapshotAuthority));
            CurrentSnapshot = initialSnapshot
                ?? throw new ArgumentNullException(nameof(initialSnapshot));
            CurrentContext = initialContext
                ?? throw new ArgumentNullException(nameof(initialContext));
            ValidateContext(CurrentContext, CurrentSnapshot, false);
            PhaseCode = ResolvePhase(CurrentContext, CurrentSnapshot);
        }

        public 정착지상호작용AuthoritySnapshot CurrentSnapshot { get; private set; }
        public Farm선택ContextData CurrentContext { get; private set; }
        public Farm선택PreviewData? CurrentPreview { get; private set; }
        public string PhaseCode { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;
        public string CurrentChoiceStableId => CurrentPreview?.ChoiceStableId
            ?? CurrentContext.AppliedChoiceStableId;
        public bool CanResumeReservedTask =>
            PhaseCode == 정착지상호작용PhaseCodes.TaskReserved
            && HasAuthoritativeReservedTask(CurrentSnapshot);

        public async Task PreviewAsync(
            string choiceStableId,
            CancellationToken cancellationToken = default)
        {
            var candidate = RequireAvailableCandidate(choiceStableId);
            try
            {
                var preview = await farmAuthority.PreviewFarmChoiceAsync(
                    CurrentContext.SessionStableId,
                    CurrentContext.WorldRevision,
                    candidate.ChoiceStableId,
                    cancellationToken);
                if (preview == null
                    || preview.SituationStableId != CurrentContext.SituationStableId
                    || preview.ChoiceStableId != candidate.ChoiceStableId
                    || preview.BaseRevision != CurrentContext.WorldRevision
                    || !preview.IsCandidateOnly
                    || !preview.RequiresExplicitConfirm)
                    throw new InvalidOperationException("FarmChoicePreviewBoundaryInvalid");

                preview.Impact.SessionStableId = CurrentContext.SessionStableId;
                preview.Impact.ObservedRevision = CurrentContext.WorldRevision;
                preview.Impact.ObservedWorldTick = CurrentContext.WorldTick;
                CurrentPreview = preview;
                PhaseCode = 정착지상호작용PhaseCodes.PreviewReady;
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task ConfirmAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentPreview == null
                || PhaseCode != 정착지상호작용PhaseCodes.PreviewReady)
                throw new InvalidOperationException("FarmChoicePreviewRequired");

            var selectedChoiceStableId = CurrentPreview.ChoiceStableId;
            try
            {
                var confirmed = await farmAuthority.ConfirmFarmChoiceAsync(
                    CurrentContext.SessionStableId,
                    CurrentContext.WorldRevision,
                    selectedChoiceStableId,
                    cancellationToken);
                ValidateNewerSnapshot(confirmed, "FarmChoiceConfirmSnapshotInvalid");

                var canonical = await farmAuthority.GetFarmChoiceContextAsync(
                    CurrentContext.SessionStableId,
                    cancellationToken);
                ValidateContext(canonical, confirmed, true);
                if (canonical.SituationStateCode != Farm선택Codes.ChoiceConfirmed
                    || canonical.AppliedChoiceStableId != selectedChoiceStableId)
                    throw new InvalidOperationException("FarmChoiceCanonicalRequeryMismatch");

                CurrentSnapshot = confirmed;
                CurrentContext = canonical;
                PhaseCode = ResolvePhase(canonical, confirmed);
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task RefreshAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var latestSnapshot = await sessionAuthority.RefreshAsync(
                    CurrentContext.SessionStableId,
                    cancellationToken);
                if (latestSnapshot == null
                    || latestSnapshot.SessionStableId != CurrentSnapshot.SessionStableId
                    || latestSnapshot.Revision < CurrentSnapshot.Revision
                    || latestSnapshot.WorldTick < CurrentSnapshot.WorldTick)
                    throw new InvalidOperationException("FarmChoiceRefreshSnapshotInvalid");
                var latestContext = await farmAuthority.GetFarmChoiceContextAsync(
                    CurrentContext.SessionStableId,
                    cancellationToken);
                ValidateContext(latestContext, latestSnapshot, true);
                if (latestContext.WorldRevision > CurrentContext.WorldRevision)
                    CurrentPreview = null;
                CurrentSnapshot = latestSnapshot;
                CurrentContext = latestContext;
                PhaseCode = ResolvePhase(latestContext, latestSnapshot);
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        public async Task AdvanceToCompletionAsync(
            CancellationToken cancellationToken = default)
        {
            if (!CanResumeReservedTask)
                throw new InvalidOperationException("FarmChoiceReservedTaskRequired");
            try
            {
                var next = await sessionAuthority.AdvanceAsync(
                    CurrentContext.SessionStableId,
                    CurrentSnapshot.Revision,
                    CurrentSnapshot.TaskRemainingTicks,
                    cancellationToken);
                ValidateNewerSnapshot(next, "FarmChoiceTickSnapshotInvalid");
                var canonical = await farmAuthority.GetFarmChoiceContextAsync(
                    CurrentContext.SessionStableId,
                    cancellationToken);
                ValidateContext(canonical, next, true);
                CurrentSnapshot = next;
                CurrentContext = canonical;
                PhaseCode = ResolvePhase(canonical, next);
                ErrorCode = string.Empty;
            }
            catch (Exception error)
            {
                Fail(error);
                throw;
            }
        }

        private Farm선택CandidateData RequireAvailableCandidate(string choiceStableId)
        {
            if (CurrentContext.SituationStateCode != Farm선택Codes.AwaitingChoice)
                throw new InvalidOperationException("FarmChoiceSituationNotAwaitingChoice");
            if (string.IsNullOrWhiteSpace(choiceStableId))
                throw new InvalidOperationException("FarmChoiceStableIdMissing");
            var candidate = CurrentContext.Candidates.SingleOrDefault(value =>
                value.ChoiceStableId == choiceStableId.Trim())
                ?? throw new InvalidOperationException("FarmChoiceCandidateMissing");
            if (!candidate.IsAvailable)
                throw new InvalidOperationException("FarmChoiceCandidateUnavailable");
            return candidate;
        }

        private static void ValidateContext(
            Farm선택ContextData context,
            정착지상호작용AuthoritySnapshot snapshot,
            bool allowConfirmed)
        {
            if (context == null
                || context.SessionStableId != snapshot.SessionStableId
                || context.SituationStableId != Farm선택Codes.SituationStableId
                || context.WorldRevision != snapshot.Revision
                || context.WorldTick != snapshot.WorldTick
                || !context.IsSimulationOnly
                || context.IsOperationalState
                || context.Facts == null
                || context.Candidates == null
                || context.Candidates.Any(value => value == null)
                || context.Candidates.Select(value => value.ChoiceStableId)
                    .Distinct(StringComparer.Ordinal).Count() != context.Candidates.Length)
                throw new InvalidOperationException("FarmChoiceContextInvalid");
            if (!allowConfirmed
                && context.SituationStateCode == Farm선택Codes.ChoiceConfirmed)
                return;
            if (context.SituationStateCode == Farm선택Codes.AwaitingHarvest
                && (context.Candidates.Length != 0
                    || !string.IsNullOrWhiteSpace(context.HarvestLotStableId)))
                throw new InvalidOperationException("FarmChoiceAwaitingHarvestContextInvalid");
        }

        private static string ResolvePhase(
            Farm선택ContextData context,
            정착지상호작용AuthoritySnapshot snapshot)
        {
            if (snapshot.AllocationStateCode == "Reserved")
            {
                if (!HasAuthoritativeReservedTask(snapshot))
                    throw new InvalidOperationException("FarmChoiceReservedTaskSnapshotInvalid");
                return 정착지상호작용PhaseCodes.TaskReserved;
            }
            if (snapshot.AllocationStateCode == "Applied")
                return 정착지상호작용PhaseCodes.EffectApplied;
            return context.SituationStateCode == Farm선택Codes.ChoiceConfirmed
                ? 정착지상호작용PhaseCodes.EffectApplied
                : 정착지상호작용PhaseCodes.LotSelected;
        }

        private static bool HasAuthoritativeReservedTask(
            정착지상호작용AuthoritySnapshot snapshot)
            => snapshot.AllocationStateCode == "Reserved"
                && snapshot.ActiveTaskCount > 0
                && !string.IsNullOrWhiteSpace(snapshot.AllocationTaskStableId)
                && (snapshot.TaskStateCode == "Scheduled"
                    || snapshot.TaskStateCode == "InProgress")
                && snapshot.TaskScheduledStartTick <= snapshot.TaskExpectedEndTick
                && snapshot.TaskExpectedEndTick > snapshot.WorldTick
                && snapshot.TaskRemainingTicks
                    == snapshot.TaskExpectedEndTick - snapshot.WorldTick;

        private void ValidateNewerSnapshot(
            정착지상호작용AuthoritySnapshot next,
            string error)
        {
            if (next == null
                || next.SessionStableId != CurrentSnapshot.SessionStableId
                || next.Revision <= CurrentSnapshot.Revision
                || next.WorldTick < CurrentSnapshot.WorldTick)
                throw new InvalidOperationException(error);
        }

        private void Fail(Exception error)
        {
            PhaseCode = 정착지상호작용PhaseCodes.Failed;
            ErrorCode = error.Message;
        }
    }
}
