using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    [Serializable]
    public sealed class Nature심상Data
    {
        public string PlayerStableId = string.Empty;
        public long WorldRevision;
        public string FactStableId = string.Empty;
        public decimal FactValue;
        public string FactUnitCode = string.Empty;
        public string FactStateHashSha256 = string.Empty;
        public string InferenceCode = string.Empty;
        public string InferenceText = string.Empty;
        public string MoodProjectionCode = string.Empty;
        public string[] PrioritizedCardStableIds = Array.Empty<string>();
        public decimal RecoveryOutput;
        public decimal ThreatOutput;
        public decimal RecoveryShare;
        public decimal ThreatShare;
        public decimal InterpretationStrength;
        public string InterpretationBandCode = string.Empty;
        public long BalanceRevision;
        public string BalanceHashSha256 = string.Empty;
        public string PeriodStateCode = string.Empty;
        public string PeriodInstanceStableId = string.Empty;
        public int RecoveryWorkDurationTicks;
        public int WorkDurationModifierTicks;
        public string[] PeriodCandidateStableIds = Array.Empty<string>();
        public bool ChangesSharedFact;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PlayerStableId)
                || WorldRevision < 0
                || string.IsNullOrWhiteSpace(FactStableId)
                || string.IsNullOrWhiteSpace(FactUnitCode)
                || string.IsNullOrWhiteSpace(FactStateHashSha256)
                || string.IsNullOrWhiteSpace(InferenceCode)
                || string.IsNullOrWhiteSpace(InferenceText)
                || string.IsNullOrWhiteSpace(MoodProjectionCode)
                || string.IsNullOrWhiteSpace(InterpretationBandCode)
                || string.IsNullOrWhiteSpace(BalanceHashSha256)
                || string.IsNullOrWhiteSpace(PeriodStateCode)
                || string.IsNullOrWhiteSpace(PeriodInstanceStableId)
                || RecoveryWorkDurationTicks <= 0
                || RecoveryOutput < 0m || ThreatOutput < 0m
                || RecoveryShare < 0m || ThreatShare < 0m
                || Math.Abs((RecoveryShare + ThreatShare) - 1m) > .000001m
                || InterpretationStrength < 0m || InterpretationStrength > 1m
                || ChangesSharedFact)
                throw new InvalidOperationException("NatureMindContextInvalid");
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public interface INature심상AuthorityClient
    {
        Task<Nature심상Data> RefreshAsync(string sessionStableId,
            string playerStableId, CancellationToken cancellationToken);
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class Nature심상Coordinator
    {
        private readonly INature심상AuthorityClient authority;

        public Nature심상Coordinator(INature심상AuthorityClient authorityClient)
            => authority = authorityClient
                ?? throw new ArgumentNullException(nameof(authorityClient));

        public string SessionStableId { get; private set; } = string.Empty;
        public string PlayerStableId { get; private set; } = string.Empty;
        public Nature심상Data? Current { get; private set; }

        public async Task<Nature심상Data> InitializeAsync(string sessionStableId,
            string playerStableId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId)
                || string.IsNullOrWhiteSpace(playerStableId))
                throw new InvalidOperationException("NatureMindBindingInvalid");
            SessionStableId = sessionStableId.Trim();
            PlayerStableId = playerStableId.Trim();
            return await RefreshAsync(cancellationToken);
        }

        public async Task<Nature심상Data> RefreshAsync(
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(SessionStableId))
                throw new InvalidOperationException("NatureMindNotInitialized");
            var value = await authority.RefreshAsync(SessionStableId,
                PlayerStableId, cancellationToken);
            value.Validate();
            if (Current != null && value.WorldRevision < Current.WorldRevision)
                throw new InvalidOperationException("NatureMindRevisionRegressed");
            Current = value;
            return value;
        }
    }
}
