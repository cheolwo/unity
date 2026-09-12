using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class Nature심상Tests
    {
        [Test]
        public async Task 같은_Farm사실을_플레이어별_다른해석으로표현한다()
        {
            var owner = new Nature심상Coordinator(new FakeAuthority(
                "recovery-leading", "안도", "mood:relieved", .6m, .4m));
            var neighbor = new Nature심상Coordinator(new FakeAuthority(
                "threat-leading", "불안", "mood:anxious", .4m, .6m));

            var ownerView = await owner.InitializeAsync(
                "simulation-session:mind", "player:owner", CancellationToken.None);
            var neighborView = await neighbor.InitializeAsync(
                "simulation-session:mind", "player:neighbor", CancellationToken.None);

            Assert.That(ownerView.FactStableId,
                Is.EqualTo(neighborView.FactStableId));
            Assert.That(ownerView.FactValue, Is.EqualTo(neighborView.FactValue));
            Assert.That(ownerView.FactStateHashSha256,
                Is.EqualTo(neighborView.FactStateHashSha256));
            Assert.That(ownerView.InferenceCode,
                Is.Not.EqualTo(neighborView.InferenceCode));
            Assert.That(ownerView.MoodProjectionCode,
                Is.Not.EqualTo(neighborView.MoodProjectionCode));
            Assert.That(ownerView.RecoveryShare + ownerView.ThreatShare,
                Is.EqualTo(1m));
            Assert.That(neighborView.RecoveryShare + neighborView.ThreatShare,
                Is.EqualTo(1m));
        }

        [Test]
        public void 심상조회는_공유사실변경표시를거부한다()
        {
            var coordinator = new Nature심상Coordinator(new FakeAuthority(
                "recovery-leading", "안도", "mood:relieved", .6m, .4m,
                changesSharedFact: true));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await coordinator.InitializeAsync(
                    "simulation-session:mind", "player:owner",
                    CancellationToken.None));
            Assert.That(exception!.Message, Is.EqualTo("NatureMindContextInvalid"));
        }

        private sealed class FakeAuthority : INature심상AuthorityClient
        {
            private readonly string inferenceCode;
            private readonly string inferenceText;
            private readonly string moodCode;
            private readonly decimal recoveryShare;
            private readonly decimal threatShare;
            private readonly bool changesSharedFact;

            public FakeAuthority(string inferenceCode, string inferenceText,
                string moodCode, decimal recoveryShare, decimal threatShare,
                bool changesSharedFact = false)
            {
                this.inferenceCode = inferenceCode;
                this.inferenceText = inferenceText;
                this.moodCode = moodCode;
                this.recoveryShare = recoveryShare;
                this.threatShare = threatShare;
                this.changesSharedFact = changesSharedFact;
            }

            public Task<Nature심상Data> RefreshAsync(string sessionStableId,
                string playerStableId, CancellationToken cancellationToken)
                => Task.FromResult(new Nature심상Data
                {
                    PlayerStableId = playerStableId,
                    WorldRevision = 9,
                    FactStableId = "fact:settlement-storage-utilization",
                    FactValue = 80m,
                    FactUnitCode = "PCT",
                    FactStateHashSha256 = "sha256:shared-storage-fact",
                    InferenceCode = inferenceCode,
                    InferenceText = inferenceText,
                    MoodProjectionCode = moodCode,
                    PrioritizedCardStableIds = new[] { "card:farm-storage" },
                    RecoveryOutput = recoveryShare * 10m,
                    ThreatOutput = threatShare * 10m,
                    RecoveryShare = recoveryShare,
                    ThreatShare = threatShare,
                    InterpretationStrength = Math.Abs(recoveryShare - threatShare),
                    InterpretationBandCode = inferenceCode,
                    BalanceRevision = 3,
                    BalanceHashSha256 = "sha256:player-balance",
                    PeriodStateCode = recoveryShare >= .8m
                        ? "GwangbokPeriod" : threatShare >= .8m
                            ? "DarkAgePeriod" : "OrdinaryPeriod",
                    PeriodInstanceStableId = "period-instance:" + playerStableId,
                    RecoveryWorkDurationTicks = recoveryShare >= .8m ? 1
                        : threatShare >= .8m ? 3 : 2,
                    WorkDurationModifierTicks = recoveryShare >= .8m ? -1
                        : threatShare >= .8m ? 1 : 0,
                    PeriodCandidateStableIds = Array.Empty<string>(),
                    ChangesSharedFact = changesSharedFact,
                });
        }
    }
}
