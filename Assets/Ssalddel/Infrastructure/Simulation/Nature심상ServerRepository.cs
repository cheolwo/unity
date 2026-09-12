using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "Unity가 공통 Core 또는 원격 Host와 연결되는 실행 경계를 제공한다.",
        Boundary = "Unity GameObject와 표현은 권위 상태를 직접 소유하지 않는다.")]
    public sealed class Nature심상ServerRepository : INature심상AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public Nature심상ServerRepository(ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<Nature심상Data> RefreshAsync(string sessionStableId,
            string playerStableId, CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = "api/simulation/v1/sessions/"
                    + Uri.EscapeDataString(sessionStableId.Trim())
                    + "/nature-minds/" + Uri.EscapeDataString(playerStableId.Trim())
                    + "/farm-interpretation",
                JsonBody = string.Empty,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("NatureMindServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            var wire = JsonConvert.DeserializeObject<InterpretationWire>(response.Body)
                ?? throw new InvalidOperationException("NatureMindJsonInvalid");
            var balance = wire.Balance
                ?? throw new InvalidOperationException("NatureMindBalanceMissing");
            var period = wire.Period
                ?? throw new InvalidOperationException("NaturePeriodMissing");
            return new Nature심상Data
            {
                PlayerStableId = wire.PlayerStableId ?? string.Empty,
                WorldRevision = wire.WorldRevision,
                FactStableId = wire.FactStableId ?? string.Empty,
                FactValue = wire.FactValue,
                FactUnitCode = wire.FactUnitCode ?? string.Empty,
                FactStateHashSha256 = wire.FactStateHashSha256 ?? string.Empty,
                InferenceCode = wire.InferenceCode ?? string.Empty,
                InferenceText = wire.InferenceText ?? string.Empty,
                MoodProjectionCode = wire.MoodProjectionCode ?? string.Empty,
                PrioritizedCardStableIds = wire.PrioritizedCardStableIds
                    ?? Array.Empty<string>(),
                RecoveryOutput = balance.RecoveryOutput,
                ThreatOutput = balance.ThreatOutput,
                RecoveryShare = balance.RecoveryShare,
                ThreatShare = balance.ThreatShare,
                InterpretationStrength = balance.InterpretationStrength,
                InterpretationBandCode = balance.InterpretationBandCode
                    ?? string.Empty,
                BalanceRevision = balance.Revision,
                BalanceHashSha256 = balance.BalanceHashSha256 ?? string.Empty,
                PeriodStateCode = period.PeriodStateCode ?? string.Empty,
                PeriodInstanceStableId = period.PeriodInstanceStableId
                    ?? string.Empty,
                RecoveryWorkDurationTicks =
                    period.EffectiveRecoveryWorkDurationTicks,
                WorkDurationModifierTicks = period.WorkDurationModifierTicks,
                PeriodCandidateStableIds = period.CandidateStableIds
                    ?? Array.Empty<string>(),
                ChangesSharedFact = wire.ChangesSharedFact,
            };
        }

        [Serializable] private sealed class InterpretationWire
        {
            public string PlayerStableId;
            public long WorldRevision;
            public string FactStableId;
            public decimal FactValue;
            public string FactUnitCode;
            public string FactStateHashSha256;
            public string InferenceCode;
            public string InferenceText;
            public string MoodProjectionCode;
            public string[] PrioritizedCardStableIds;
            public BalanceWire Balance;
            public PeriodWire Period;
            public bool ChangesSharedFact;
        }

        [Serializable] private sealed class BalanceWire
        {
            public decimal RecoveryOutput;
            public decimal ThreatOutput;
            public decimal RecoveryShare;
            public decimal ThreatShare;
            public decimal InterpretationStrength;
            public string InterpretationBandCode;
            public long Revision;
            public string BalanceHashSha256;
        }

        [Serializable] private sealed class PeriodWire
        {
            public string PeriodStateCode;
            public string PeriodInstanceStableId;
            public int EffectiveRecoveryWorkDurationTicks;
            public int WorkDurationModifierTicks;
            public string[] CandidateStableIds;
        }
    }
}
