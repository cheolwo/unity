using System;
using System.IO;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Nature생존CoreRuntimeAdapterTests
    {
        [Test]
        public void 공통LocalRuntime은_서버없이_도끼와4초벌목을처리한다()
        {
            using var adapter = new Nature생존CoreRuntimeAdapter(
                LocalSimulationUnityRuntime.Create());

            var initial = adapter.Snapshot();
            Assert.That(initial.AuthorityModeCode,
                Is.EqualTo(Nature생존Codes.SoloLocal));
            Assert.That(initial.HasAxe, Is.False);
            Assert.That(initial.WorldTick, Is.Zero);

            adapter.AcquireAxe();
            adapter.BeginHarvest("resource:nature-tree:01");
            adapter.AdvanceRealtime(NatureSurvivalRules.HarvestWorkSeconds,
                true, false);

            var state = adapter.Snapshot();
            Assert.That(state.TimberQuantity,
                Is.EqualTo(NatureSurvivalRules.HarvestTimberQuantity));
            Assert.That(state.ResourceNodes[0].StateCode,
                Is.EqualTo(Nature생존Codes.Stump));
            Assert.That(state.ActiveWork, Is.Null);
            Assert.That(state.Revision, Is.GreaterThan(initial.Revision));
        }

        [Test]
        public void 공통LocalRuntime은_메뉴정지중_실시간을진행하지않는다()
        {
            using var adapter = new Nature생존CoreRuntimeAdapter(
                LocalSimulationUnityRuntime.Create());

            adapter.AdvanceRealtime(60, false, true);

            var state = adapter.Snapshot();
            Assert.That(state.ElapsedSecondsInCycle, Is.Zero);
            Assert.That(state.ClockPaused, Is.True);
        }

        [Test]
        public void 공통LocalRuntime은_V13슬롯을_새Runtime에서복원한다()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "ssalddel-unity-local-save-" + Guid.NewGuid().ToString("N"));
            try
            {
                string replayHash;
                using (var first = new Nature생존CoreRuntimeAdapter(
                           LocalSimulationUnityRuntime.Create(root)))
                {
                    first.AcquireAxe();
                    var saved = first.SaveSlot("slot-01");
                    replayHash = saved.ReplayHash;
                    Assert.That(saved.SaveStableId, Does.StartWith(
                        "simulation-save:local:slot-01:"));
                }

                using var restored = Nature생존CoreRuntimeAdapter.Load(
                    LocalSimulationUnityRuntime.Create(root), "slot-01");
                var state = restored.Snapshot();

                Assert.That(state.HasAxe, Is.True);
                Assert.That(state.WorldTick, Is.Zero);
                Assert.That(replayHash, Is.Not.Empty);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
