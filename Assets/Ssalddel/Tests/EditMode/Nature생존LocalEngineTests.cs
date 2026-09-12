using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.PlayerActivities;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class Nature생존LocalEngineTests
    {
        [Test]
        public void Nature입력은_탐험문맥에서만허용된다()
        {
            Assert.That(Nature생존Controller.CanUseNatureInput(
                PlayerActivityCodes.Exploration), Is.True);
            Assert.That(Nature생존Controller.CanUseNatureInput(
                PlayerActivityCodes.FarmManagement), Is.False);
            Assert.That(Nature생존Controller.CanUseNatureInput(
                PlayerActivityCodes.WorldOverview), Is.False);
        }

        [Test]
        public void Solo는_서버없이_벌목결과를_지면통나무로남기고_별도로줍는다()
        {
            var engine = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());

            Assert.That(engine.Snapshot().HasAxe, Is.False);
            engine.AcquireAxe();
            engine.BeginHarvest("resource:nature-tree:01");
            engine.AdvanceRealtime(NatureSurvivalRules.HarvestWorkSeconds,
                true, false);

            var harvested = engine.Snapshot();
            Assert.That(harvested.TimberQuantity, Is.Zero);
            Assert.That(harvested.DroppedTimber, Has.Length.EqualTo(1));
            Assert.That(harvested.DroppedTimber[0].StateCode,
                Is.EqualTo(Nature생존Codes.DroppedTimberAvailable));
            engine.CollectDroppedTimber(harvested.DroppedTimber[0].StableId);

            var state = engine.Snapshot();
            Assert.That(state.TimberQuantity,
                Is.EqualTo(NatureSurvivalRules.HarvestTimberQuantity));
            Assert.That(state.DroppedTimber[0].StateCode,
                Is.EqualTo(Nature생존Codes.DroppedTimberCollected));
            Assert.That(state.ResourceNodes[0].StateCode,
                Is.EqualTo(Nature생존Codes.Stump));
            Assert.That(state.ActiveWork, Is.Null);
        }

        [Test]
        public void Solo메뉴는정지하지만_HostedProfile은같은입력에도계속간다()
        {
            var solo = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());
            solo.AdvanceRealtime(60, false, true);
            Assert.That(solo.Snapshot().ElapsedSecondsInCycle, Is.Zero);
            Assert.That(solo.Snapshot().ClockPaused, Is.True);

            var hostedState = Nature생존LocalEngine.CreateDefaultFixture();
            hostedState.AuthorityModeCode = Nature생존Codes.HostedServer;
            var hosted = new Nature생존LocalEngine(hostedState);
            hosted.AdvanceRealtime(60, false, true);
            Assert.That(hosted.Snapshot().ElapsedSecondsInCycle, Is.EqualTo(60));
            Assert.That(hosted.Snapshot().ClockPaused, Is.False);
        }

        [Test]
        public void 세그루목재와_도면과_누르기건설이_오두막기능을연다()
        {
            var engine = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());
            engine.AcquireAxe();
            for (var index = 1; index <= 3; index++)
            {
                engine.BeginHarvest($"resource:nature-tree:{index:00}");
                engine.AdvanceRealtime(4, true, false);
                var dropped = engine.Snapshot().DroppedTimber.Single(value =>
                    value.StateCode == Nature생존Codes.DroppedTimberAvailable);
                engine.CollectDroppedTimber(dropped.StableId);
            }
            engine.PlaceCabinBlueprint(3f, -2f, 90f);
            engine.BeginCabinBuild();
            engine.AdvanceRealtime(30, true, false);

            var cabin = engine.Snapshot().Cabin;
            Assert.That(cabin.StateCode, Is.EqualTo(Nature생존Codes.Completed));
            Assert.That(cabin.RecoveryAvailable, Is.True);
            Assert.That(cabin.DefenseAvailable, Is.True);
            Assert.That(engine.Snapshot().TimberQuantity, Is.Zero);
        }

        [Test]
        public void 실시간주기경계는_WorldTick과_3일후나무재생을함께진행한다()
        {
            var engine = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());
            engine.AcquireAxe();
            engine.BeginHarvest("resource:nature-tree:01");
            engine.AdvanceRealtime(4, true, false);
            for (var index = 0; index < 60; index++)
                engine.AdvanceRealtime(60, false, false);

            var state = engine.Snapshot();
            Assert.That(state.WorldTick, Is.EqualTo(3));
            Assert.That(state.CycleIndex, Is.EqualTo(3));
            Assert.That(state.ResourceNodes[0].StateCode,
                Is.EqualTo(Nature생존Codes.Standing));
            Assert.That(state.ClockPhaseCode,
                Is.EqualTo(NatureSurvivalClockPhaseCodes.Daylight));
        }
    }
}
