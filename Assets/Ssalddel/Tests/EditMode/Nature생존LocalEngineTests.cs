using System;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Nature생존LocalEngineTests
    {
        [Test]
        public void Solo는_서버없이_도끼획득과_누르기벌목을완료한다()
        {
            var engine = new Nature생존LocalEngine(
                Nature생존LocalEngine.CreateDefaultFixture());

            Assert.That(engine.Snapshot().HasAxe, Is.False);
            engine.AcquireAxe();
            engine.BeginHarvest("resource:nature-tree:01");
            engine.AdvanceRealtime(NatureSurvivalRules.HarvestWorkSeconds,
                true, false);

            var state = engine.Snapshot();
            Assert.That(state.TimberQuantity,
                Is.EqualTo(NatureSurvivalRules.HarvestTimberQuantity));
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
