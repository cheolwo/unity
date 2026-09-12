using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Farm플레이어배치Codes
    {
        public const int DefaultSnapCentimeters = 100;
        public const int FineNudgeCentimeters = 25;
        public const string Candidate = "Candidate";
        public const string PreviewReady = "PreviewReady";
        public const string Planned = "Planned";
        public const string Building = "Building";
        public const string Operational = "Operational";
    }

    public static class Farm연속배치Grid
    {
        public static int SnapToAnchor(int valueCentimeters,
            int anchorCentimeters, int spacingCentimeters)
        {
            if (spacingCentimeters <= 0
                || spacingCentimeters %
                Farm플레이어배치Codes.FineNudgeCentimeters != 0)
                throw new InvalidOperationException(
                    "FarmContinuousPlacementSpacingInvalid");
            return anchorCentimeters + (int)Math.Round(
                (valueCentimeters - anchorCentimeters)
                / (decimal)spacingCentimeters,
                MidpointRounding.AwayFromZero) * spacingCentimeters;
        }

        public static string CellKey(Farm배치CandidateData candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            return string.Join("|", candidate.BlueprintStableId,
                candidate.LocalXCentimeters, candidate.LocalZCentimeters,
                candidate.RotationQuarterTurns);
        }

        public static Farm배치CandidateData NextAdjacent(
            Farm배치CandidateData from, Farm배치CandidateData target,
            int spacingCentimeters)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (spacingCentimeters <= 0)
                throw new InvalidOperationException(
                    "FarmContinuousPlacementSpacingInvalid");
            var result = target.Clone();
            var deltaX = target.LocalXCentimeters - from.LocalXCentimeters;
            var deltaZ = target.LocalZCentimeters - from.LocalZCentimeters;
            if (Math.Abs(deltaX) >= Math.Abs(deltaZ) && deltaX != 0)
            {
                result.LocalXCentimeters = from.LocalXCentimeters
                    + Math.Sign(deltaX) * Math.Min(Math.Abs(deltaX),
                        spacingCentimeters);
                result.LocalZCentimeters = from.LocalZCentimeters;
            }
            else if (deltaZ != 0)
            {
                result.LocalXCentimeters = from.LocalXCentimeters;
                result.LocalZCentimeters = from.LocalZCentimeters
                    + Math.Sign(deltaZ) * Math.Min(Math.Abs(deltaZ),
                        spacingCentimeters);
            }
            return result;
        }
    }

    public sealed class Farm연속배치Stroke
    {
        private readonly System.Collections.Generic.HashSet<string> attemptedCells =
            new(StringComparer.Ordinal);

        public bool Active { get; private set; }
        public int AttemptedCellCount => attemptedCells.Count;

        public void Begin()
        {
            attemptedCells.Clear();
            Active = true;
        }

        public bool TryReserve(Farm배치CandidateData candidate)
            => Active && attemptedCells.Add(Farm연속배치Grid.CellKey(candidate));

        public void End()
        {
            Active = false;
            attemptedCells.Clear();
        }
    }

    [Serializable]
    public sealed class Farm배치CandidateData
    {
        public string BlueprintStableId = string.Empty;
        public string PlacementZoneStableId = string.Empty;
        public string TargetH2StableId = string.Empty;
        public int LocalXCentimeters;
        public int LocalZCentimeters;
        public int RotationQuarterTurns;
        public string AccessConnectorStableId = string.Empty;
        public string FenceChainStableId = string.Empty;

        public Farm배치CandidateData Clone() => new()
        {
            BlueprintStableId = BlueprintStableId,
            PlacementZoneStableId = PlacementZoneStableId,
            TargetH2StableId = TargetH2StableId,
            LocalXCentimeters = LocalXCentimeters,
            LocalZCentimeters = LocalZCentimeters,
            RotationQuarterTurns = RotationQuarterTurns,
            AccessConnectorStableId = AccessConnectorStableId,
            FenceChainStableId = FenceChainStableId,
        };
    }

    [Serializable]
    public sealed class Farm배치MaterialRequirementData
    {
        public string ItemCode = string.Empty;
        public decimal Quantity;
        public string UnitCode = string.Empty;
    }

    [Serializable]
    public sealed class Farm배치PreviewData
    {
        public string PlacementProposalStableId = string.Empty;
        public long SourceWorldRevision;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public Farm배치CandidateData NormalizedCandidate = new();
        public string PlacementKindCode = string.Empty;
        public string PlacementProfileRevision = string.Empty;
        public int FootprintWidthCentimeters;
        public int FootprintDepthCentimeters;
        public Farm배치MaterialRequirementData[] MaterialRequirements =
            Array.Empty<Farm배치MaterialRequirementData>();
        public string[] ReservedMaterialLotStableIds = Array.Empty<string>();
        public int ConstructionTicks;
        public string PreviewHashSha256 = string.Empty;
    }

    [Serializable]
    public sealed class Farm배치ObjectData
    {
        public string FacilityStableId = string.Empty;
        public string BlueprintStableId = string.Empty;
        public string LifecycleCode = string.Empty;
        public string PlacementZoneStableId = string.Empty;
        public string TargetH2StableId = string.Empty;
        public string PlacementKindCode = string.Empty;
        public int LocalXCentimeters;
        public int LocalZCentimeters;
        public int RotationQuarterTurns;
        public string PlacementProfileRevision = string.Empty;
        public string FenceChainStableId = string.Empty;
    }

    [Serializable]
    public sealed class Farm배치ContextData
    {
        public string SessionStableId = string.Empty;
        public long WorldRevision;
        public int WorldTick;
        public Farm배치ObjectData[] Objects = Array.Empty<Farm배치ObjectData>();
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public interface IFarm플레이어배치AuthorityClient
    {
        Task<Farm배치ContextData> RefreshAsync(string sessionStableId,
            CancellationToken cancellationToken);
        Task<Farm배치PreviewData> PreviewAsync(string sessionStableId,
            long expectedRevision, Farm배치CandidateData candidate,
            CancellationToken cancellationToken);
        Task<Farm배치ContextData> ConfirmAsync(string sessionStableId,
            long expectedRevision, string commandId,
            string placementProposalStableId, string previewHashSha256,
            CancellationToken cancellationToken);
        Task<Farm배치ContextData> AdvanceOneTickAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class Farm플레이어배치Coordinator
    {
        private readonly IFarm플레이어배치AuthorityClient authority;

        public Farm플레이어배치Coordinator(IFarm플레이어배치AuthorityClient authorityClient)
            => authority = authorityClient
                ?? throw new ArgumentNullException(nameof(authorityClient));

        public string SessionStableId { get; private set; } = string.Empty;
        public Farm배치ContextData? Current { get; private set; }
        public Farm배치CandidateData? CurrentCandidate { get; private set; }
        public Farm배치PreviewData? CurrentPreview { get; private set; }

        public async Task<Farm배치ContextData> InitializeAsync(string sessionStableId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("FarmPlacementSessionMissing");
            SessionStableId = sessionStableId.Trim();
            Current = ValidateContext(await authority.RefreshAsync(
                SessionStableId, cancellationToken));
            ClearCandidate();
            return Current;
        }

        public void SelectCandidate(Farm배치CandidateData candidate)
        {
            ValidateCandidate(candidate);
            CurrentCandidate = candidate.Clone();
            CurrentPreview = null;
        }

        public void RotateCandidate()
        {
            var candidate = RequireCandidate();
            candidate.RotationQuarterTurns =
                (candidate.RotationQuarterTurns + 1) % 4;
            CurrentPreview = null;
        }

        public void NudgeCandidate(int deltaXCentimeters, int deltaZCentimeters)
        {
            if (deltaXCentimeters % Farm플레이어배치Codes.FineNudgeCentimeters != 0
                || deltaZCentimeters % Farm플레이어배치Codes.FineNudgeCentimeters != 0)
                throw new InvalidOperationException("FarmPlacementNudgeStepInvalid");
            var candidate = RequireCandidate();
            candidate.LocalXCentimeters += deltaXCentimeters;
            candidate.LocalZCentimeters += deltaZCentimeters;
            CurrentPreview = null;
        }

        public async Task<Farm배치PreviewData> PreviewAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            var preview = await authority.PreviewAsync(SessionStableId,
                current.WorldRevision, RequireCandidate().Clone(), cancellationToken);
            if (preview == null || preview.NormalizedCandidate == null
                || preview.SourceWorldRevision != current.WorldRevision
                || string.IsNullOrWhiteSpace(preview.PlacementProposalStableId)
                || string.IsNullOrWhiteSpace(preview.PreviewHashSha256))
                throw new InvalidOperationException("FarmPlacementPreviewInvalid");
            CurrentCandidate = preview.NormalizedCandidate.Clone();
            CurrentPreview = preview;
            return preview;
        }

        public async Task<Farm배치ContextData> ConfirmAsync(string commandId,
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            if (string.IsNullOrWhiteSpace(commandId))
                throw new InvalidOperationException("FarmPlacementCommandIdMissing");
            var preview = CurrentPreview;
            if (preview == null || !preview.CanConfirm
                || preview.SourceWorldRevision != current.WorldRevision)
                throw new InvalidOperationException("FarmPlacementPreviewRequired");
            var next = ValidateContext(await authority.ConfirmAsync(SessionStableId,
                current.WorldRevision, commandId.Trim(),
                preview.PlacementProposalStableId,
                preview.PreviewHashSha256, cancellationToken));
            if (next.WorldRevision != current.WorldRevision + 1)
                throw new InvalidOperationException(
                    "FarmPlacementConfirmAuthorityMismatch");
            Current = next;
            ClearCandidate();
            return next;
        }

        public async Task<Farm배치ContextData> AdvanceOneTickAsync(
            CancellationToken cancellationToken)
        {
            var current = RequireCurrent();
            var next = ValidateContext(await authority.AdvanceOneTickAsync(
                SessionStableId, current.WorldRevision, cancellationToken));
            if (next.WorldRevision != current.WorldRevision + 1
                || next.WorldTick != current.WorldTick + 1)
                throw new InvalidOperationException("FarmPlacementTickAuthorityMismatch");
            Current = next;
            return next;
        }

        public void ClearCandidate()
        {
            CurrentCandidate = null;
            CurrentPreview = null;
        }

        private Farm배치ContextData RequireCurrent()
            => Current ?? throw new InvalidOperationException(
                "FarmPlacementCoordinatorNotInitialized");

        private Farm배치CandidateData RequireCandidate()
            => CurrentCandidate ?? throw new InvalidOperationException(
                "FarmPlacementCandidateMissing");

        private Farm배치ContextData ValidateContext(Farm배치ContextData context)
        {
            if (context == null || context.SessionStableId != SessionStableId
                || context.WorldRevision < 0 || context.WorldTick < 0
                || context.Objects == null
                || context.Objects.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.FacilityStableId)
                    || string.IsNullOrWhiteSpace(value.BlueprintStableId)))
                throw new InvalidOperationException("FarmPlacementContextInvalid");
            return context;
        }

        private static void ValidateCandidate(Farm배치CandidateData candidate)
        {
            if (candidate == null
                || string.IsNullOrWhiteSpace(candidate.BlueprintStableId)
                || string.IsNullOrWhiteSpace(candidate.PlacementZoneStableId)
                || string.IsNullOrWhiteSpace(candidate.TargetH2StableId))
                throw new InvalidOperationException("FarmPlacementCandidateInvalid");
        }
    }
}
