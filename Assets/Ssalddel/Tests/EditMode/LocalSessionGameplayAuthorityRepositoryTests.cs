using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class LocalSessionGameplayAuthorityRepositoryTests
    {
        [SetUp]
        public void SetUp() => SimulationWorldLocalRuntimeScope.Reset();

        [TearDown]
        public void TearDown() => SimulationWorldLocalRuntimeScope.Reset();

        [Test]
        public async Task 턴마감과Farm선택과물류는_같은로컬Session을조회한다()
        {
            var scope = SimulationWorldLocalRuntimeScope.Install();
            var turns = new LocalTurnClosingAuthorityRepository(scope.Runtime);
            var farm = new LocalFarmChoiceAuthorityRepository(scope.Runtime);
            var logistics = new LocalLogisticsMovementAuthorityRepository(
                scope.Runtime);

            var turnContext = await turns.GetContextAsync(scope.SessionStableId,
                CancellationToken.None);
            var farmContext = await farm.GetFarmChoiceContextAsync(
                scope.SessionStableId, CancellationToken.None);
            var snapshot = await farm.RefreshAsync(scope.SessionStableId,
                CancellationToken.None);
            var logisticsSnapshot = await logistics.RefreshAsync(
                scope.SessionStableId, CancellationToken.None);
            var session = await scope.Runtime.Sessions.GetAsync(
                scope.SessionStableId, CancellationToken.None);

            Assert.That(turnContext.SessionStableId,
                Is.EqualTo(scope.SessionStableId));
            Assert.That(farmContext.SessionStableId,
                Is.EqualTo(scope.SessionStableId));
            Assert.That(snapshot.SessionStableId,
                Is.EqualTo(scope.SessionStableId));
            Assert.That(snapshot.SourceModeCode, Is.EqualTo("LocalSimulation"));
            Assert.That(logisticsSnapshot.SessionStableId,
                Is.EqualTo(scope.SessionStableId));
            Assert.That(logisticsSnapshot.SourceModeCode,
                Is.EqualTo("LocalSimulation"));
            Assert.That(session.FarmSurvival, Is.Not.Null);
            Assert.That(scope.Runtime.Descriptor.RequiresNetwork, Is.False);
        }
    }
}
