using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class Hosted비콘Codes
    {
        public const string Owner = "player:session:owner";
        public const string Guest = "player:sim.guest-1";
        public const string FarmAreaSet = "area-set:sim:pyeongchang:farm-production.v1";
        public const string Solo = "Solo";
        public const string HostedMultiplayer = "HostedMultiplayer";
        public const string Invited = "Invited";
        public const string Active = "Active";
        public const string Allow = "Allow";
        public const string Deny = "Deny";
        public const string PerformWork = "PerformWork";
        public const string Build = "Build";
        public const string Demolish = "Demolish";
    }

    [Serializable] public sealed class Hosted참여자Data
    {
        public string PlayerStableId = string.Empty;
        public string ParticipantStateCode = string.Empty;
        public string CurrentAreaSetStableId = string.Empty;
    }

    [Serializable] public sealed class Hosted권한Data
    {
        public string TargetPlayerStableId = string.Empty;
        public string ScopeStableId = string.Empty;
        public string CapabilityCode = string.Empty;
        public string GrantStateCode = string.Empty;
        public string ActionRiskPolicyCode = string.Empty;
        public string GrantHashSha256 = string.Empty;
    }

    [Serializable] public sealed class Hosted감사Data
    {
        public string EffectStableId = string.Empty;
        public string EffectTypeCode = string.Empty;
        public string TargetPlayerStableId = string.Empty;
        public string CapabilityCode = string.Empty;
        public int AppliedWorldTick;
    }

    [Serializable] public sealed class Hosted세계Data
    {
        public long WorldRevision;
        public int WorldTick;
        public string HostedSessionStableId = string.Empty;
        public string WorldStableId = string.Empty;
        public string OwnerPlayerStableId = string.Empty;
        public string SessionModeCode = string.Empty;
        public Hosted참여자Data[] Participants = Array.Empty<Hosted참여자Data>();
        public Hosted권한Data[] PermissionGrants = Array.Empty<Hosted권한Data>();
        public Hosted감사Data[] AuditTrail = Array.Empty<Hosted감사Data>();
        public bool HostLossBlocksMutation;
        public bool EscPausesWorld;
        public string SessionHashSha256 = string.Empty;

        public void Validate()
        {
            if (WorldRevision < 0 || WorldTick < 0
                || string.IsNullOrWhiteSpace(WorldStableId)
                || string.IsNullOrWhiteSpace(OwnerPlayerStableId)
                || string.IsNullOrWhiteSpace(SessionModeCode)
                || Participants == null || PermissionGrants == null
                || AuditTrail == null || !HostLossBlocksMutation
                || EscPausesWorld || string.IsNullOrWhiteSpace(SessionHashSha256))
                throw new InvalidOperationException("HostedWorldContextInvalid");
        }

        public Hosted참여자Data? Guest => Participants.FirstOrDefault(value =>
            value.PlayerStableId == Hosted비콘Codes.Guest);

        public string Grant(string capability) => PermissionGrants.FirstOrDefault(value =>
            value.TargetPlayerStableId == Hosted비콘Codes.Guest
            && value.CapabilityCode == capability)?.GrantStateCode ?? "미부여";
    }

    [Serializable] public sealed class Hosted행동PreviewData
    {
        public long BaseRevision;
        public string ActionCode = string.Empty;
        public string CapabilityCode = string.Empty;
        public string GrantStateCode = string.Empty;
        public string ActionRiskPolicyCode = string.Empty;
        public int DurationTicks;
        public bool CanConfirm;
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public string PreviewHashSha256 = string.Empty;
    }

    public interface IHosted비콘AuthorityClient
    {
        Task<Hosted세계Data> RefreshAsync(string sessionStableId,
            CancellationToken cancellationToken);
        Task<Hosted행동PreviewData> PreviewOpenAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken);
        Task<Hosted세계Data> ConfirmOpenAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken);
        Task<Hosted행동PreviewData> PreviewJoinAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken);
        Task<Hosted세계Data> ConfirmJoinAsync(string sessionStableId,
            long expectedRevision, CancellationToken cancellationToken);
        Task<Hosted행동PreviewData> PreviewGuestActionAsync(string sessionStableId,
            long expectedRevision, string capabilityCode,
            CancellationToken cancellationToken);
        Task<Hosted세계Data> ConfirmGuestActionAsync(string sessionStableId,
            long expectedRevision, string capabilityCode,
            CancellationToken cancellationToken);
        Task<Hosted세계Data> AdvanceAsync(string sessionStableId,
            long expectedRevision, int tickCount, CancellationToken cancellationToken);
    }

    public sealed class Hosted비콘Coordinator
    {
        private readonly IHosted비콘AuthorityClient authority;
        public Hosted비콘Coordinator(IHosted비콘AuthorityClient authorityClient)
            => authority = authorityClient ?? throw new ArgumentNullException(nameof(authorityClient));
        public string SessionStableId { get; private set; } = string.Empty;
        public Hosted세계Data? Current { get; private set; }
        public Hosted행동PreviewData? CurrentPreview { get; private set; }

        public async Task<Hosted세계Data> InitializeAsync(string sessionStableId,
            CancellationToken cancellationToken)
        {
            SessionStableId = sessionStableId;
            return await RefreshAsync(cancellationToken);
        }

        public async Task<Hosted세계Data> RefreshAsync(CancellationToken cancellationToken)
        {
            var value = await authority.RefreshAsync(SessionStableId, cancellationToken);
            value.Validate();
            if (Current != null && value.WorldRevision < Current.WorldRevision)
                throw new InvalidOperationException("HostedWorldRevisionRegressed");
            Current = value;
            return value;
        }

        public async Task<Hosted행동PreviewData> PreviewOpenAsync(
            CancellationToken cancellationToken)
            => CurrentPreview = await authority.PreviewOpenAsync(SessionStableId,
                RequireCurrent().WorldRevision, cancellationToken);

        public async Task<Hosted세계Data> ConfirmOpenAsync(
            CancellationToken cancellationToken)
            => Current = await authority.ConfirmOpenAsync(SessionStableId,
                RequirePreview().BaseRevision, cancellationToken);

        public async Task<Hosted행동PreviewData> PreviewJoinAsync(
            CancellationToken cancellationToken)
            => CurrentPreview = await authority.PreviewJoinAsync(SessionStableId,
                RequireCurrent().WorldRevision, cancellationToken);

        public async Task<Hosted세계Data> ConfirmJoinAsync(
            CancellationToken cancellationToken)
            => Current = await authority.ConfirmJoinAsync(SessionStableId,
                RequirePreview().BaseRevision, cancellationToken);

        public async Task<Hosted행동PreviewData> PreviewGuestActionAsync(
            string capabilityCode, CancellationToken cancellationToken)
            => CurrentPreview = await authority.PreviewGuestActionAsync(SessionStableId,
                RequireCurrent().WorldRevision, capabilityCode, cancellationToken);

        public async Task<Hosted세계Data> ConfirmGuestActionAsync(
            CancellationToken cancellationToken)
            => Current = await authority.ConfirmGuestActionAsync(SessionStableId,
                RequirePreview().BaseRevision, RequirePreview().CapabilityCode,
                cancellationToken);

        public async Task<Hosted세계Data> AdvanceAsync(CancellationToken cancellationToken)
        {
            var preview = RequirePreview();
            Current = await authority.AdvanceAsync(SessionStableId,
                RequireCurrent().WorldRevision, preview.DurationTicks,
                cancellationToken);
            CurrentPreview = null;
            return Current;
        }

        private Hosted세계Data RequireCurrent() => Current
            ?? throw new InvalidOperationException("HostedWorldNotInitialized");
        private Hosted행동PreviewData RequirePreview()
            => CurrentPreview?.CanConfirm == true ? CurrentPreview
                : throw new InvalidOperationException("HostedWorldPreviewRequired");
    }
}
