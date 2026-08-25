using System;
using System.IO;
using NUnit.Framework;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class Nature생존CoreRuntimeAdapterTests
    {
        [TearDown]
        public void TearDown()
        {
            SimulationWorldLocalRuntimeScope.Reset();
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", null);
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", null);
        }

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

        [Test]
        public void 공통WorldScope은_기본프로필과_E7검증슬롯을분리한다()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "ssalddel-world-scope-profile-" + Guid.NewGuid().ToString("N"));
            try
            {
                var scope = SimulationWorldLocalRuntimeScope.Install(
                    Nature생존LaunchProfile.E7Validation, root);
                var state = scope.Runtime.Nature.GetAsync(scope.SessionStableId)
                    .AsTask().GetAwaiter().GetResult();

                Assert.That(scope.SlotStableId,
                    Is.EqualTo("nature-e7-validation-r3-primary"));
                Assert.That(scope.RestoredFromSlot, Is.False);
                Assert.That(state.ElapsedSecondsInCycle,
                    Is.EqualTo(NatureSurvivalRules.DaylightEndsAtSecond - 90));
                Assert.That(state.Encounter, Is.Null);
            }
            finally
            {
                SimulationWorldLocalRuntimeScope.Reset();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 공통WorldScope은_같은Session전체를저장하고_재진입때복원한다()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "ssalddel-world-scope-restore-" + Guid.NewGuid().ToString("N"));
            try
            {
                var first = SimulationWorldLocalRuntimeScope.Install(
                    Nature생존LaunchProfile.E7Validation, root);
                using (var adapter = new Nature생존CoreRuntimeAdapter(
                           first.Runtime, first.SessionStableId))
                    adapter.AcquireAxe();
                var saved = first.SaveCurrent();
                var originalSession = first.SessionStableId;
                SimulationWorldLocalRuntimeScope.Reset();

                var restored = SimulationWorldLocalRuntimeScope.Install(
                    Nature생존LaunchProfile.E7Validation, root);
                var state = restored.Runtime.Nature.GetAsync(restored.SessionStableId)
                    .AsTask().GetAwaiter().GetResult();

                Assert.That(restored.RestoredFromSlot, Is.True);
                Assert.That(restored.SessionStableId, Is.EqualTo(originalSession));
                Assert.That(state.HasAxe, Is.True);
                Assert.That(saved.ReplayHash, Is.Not.Empty);
            }
            finally
            {
                SimulationWorldLocalRuntimeScope.Reset();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 공통WorldScope은_손상슬롯을_새Session으로숨기지않는다()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "ssalddel-world-scope-corrupt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,
                "nature-e7-validation-r3-primary.ssalddel"), "{broken");
            try
            {
                var error = Assert.Catch<Exception>(() =>
                    SimulationWorldLocalRuntimeScope.Install(
                        Nature생존LaunchProfile.E7Validation, root));
                Assert.That(error!.GetType().Name,
                    Is.EqualTo("SimulationContractException"));
                Assert.That(error!.Message,
                    Is.EqualTo("SimulationLocalSaveCorrupted"));
                Assert.That(SimulationWorldLocalRuntimeScope.IsInstalled, Is.False);
            }
            finally
            {
                SimulationWorldLocalRuntimeScope.Reset();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 환경변수없이는_기본Solo프로필을사용한다()
        {
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", null);
            Assert.That(SimulationWorldLocalRuntimeScope.DetectLaunchProfile(),
                Is.EqualTo(Nature생존LaunchProfile.PlayableDefault));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Assert.That(SimulationWorldLocalRuntimeScope.DetectLaunchProfile(),
                Is.EqualTo(Nature생존LaunchProfile.E7Validation));
        }
    }
}
