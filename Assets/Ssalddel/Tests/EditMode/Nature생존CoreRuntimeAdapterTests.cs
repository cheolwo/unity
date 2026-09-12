using System;
using System.IO;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class Nature생존CoreRuntimeAdapterTests
    {
        [Test]
        public void 한스E5프로필은_별도슬롯에서_권위수리와표현조회로이어진다()
        {
            var root = Path.Combine(Path.GetTempPath(), "hans-fence-e5-" + Guid.NewGuid().ToString("N"));
            SimulationWorldLocalRuntimeScope.Reset();
            try
            {
                var scope = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.HansFenceE5, root);
                using var adapter = new Nature생존CoreRuntimeAdapter(scope.Runtime, scope.SessionStableId);
                var initial = adapter.Snapshot();
                Assert.That(scope.SlotStableId, Is.EqualTo("hans-fence-e5-r6-primary"));
                Assert.That(initial.HasAxe, Is.True);
                Assert.That(adapter.LastAuthoritySnapshot!.HansFarmFenceRestoration, Is.Not.Null);
                Assert.Catch<InvalidOperationException>(() => adapter.RepairHansFarmFence());
                Assert.That(adapter.Snapshot().Revision, Is.EqualTo(initial.Revision));
                adapter.AcquireHansBrokenAxe();
                adapter.Snapshot();
                Assert.Catch<InvalidOperationException>(() => adapter.RepairHansFarmFence());
                adapter.BeginHarvest(initial.ResourceNodes[0].StableId);
                adapter.AdvanceRealtime(4, true, false);
                var harvested = adapter.Snapshot();
                adapter.CollectDroppedTimber(harvested.DroppedTimber[0].StableId);
                Assert.That(adapter.Snapshot().TimberQuantity, Is.EqualTo(2));
                adapter.RepairHansFarmFence();
                var repaired = adapter.Snapshot();
                Assert.That(repaired.TimberQuantity, Is.Zero);
                Assert.That(adapter.LastAuthoritySnapshot!.HansFarmFenceRestoration!.FenceStateCode,
                    Is.EqualTo(SimulationNatureSurvivalCodes.Repaired));
                Assert.Catch<InvalidOperationException>(() => adapter.RepairHansFarmFence());
                Assert.That(adapter.Snapshot().Revision, Is.EqualTo(repaired.Revision));
                scope.SaveCurrent();
                SimulationWorldLocalRuntimeScope.Reset();
                var restored = SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.HansFenceE5, root);
                using var restoredAdapter = new Nature생존CoreRuntimeAdapter(restored.Runtime, restored.SessionStableId);
                Assert.That(restoredAdapter.Snapshot().TimberQuantity, Is.Zero);
                Assert.That(restoredAdapter.LastAuthoritySnapshot!.HansFarmFenceRestoration!.FenceStateCode,
                    Is.EqualTo(SimulationNatureSurvivalCodes.Repaired));
            }
            finally { SimulationWorldLocalRuntimeScope.Reset(); }
        }

        [Test]
        public void 벌목완료조회는_소멸한ActiveWork대신_같은권위원장근거를보존한다()
        {
            var runtime = LocalSimulationUnityRuntime.Create();
            using var adapter = new Nature생존CoreRuntimeAdapter(runtime);
            adapter.AcquireAxe();
            EquipAxe(adapter);
            adapter.BeginHarvest("resource:nature-tree:01");
            var working = adapter.Snapshot();
            Assert.That(working.HarvestCompletionRecords, Is.Empty);
            adapter.AdvanceRealtime(4, true, false);
            var completed = adapter.Snapshot();
            Assert.That(completed.ActiveWork, Is.Null);
            Assert.That(completed.HarvestCompletionRecords, Has.Length.EqualTo(1));
            var record = completed.HarvestCompletionRecords[0];
            Assert.That(record.SessionStableId, Is.EqualTo(completed.SessionStableId));
            Assert.That(record.ActorStableId, Is.EqualTo(working.ActiveWork!.ActorStableId));
            Assert.That(record.OriginCommandId, Is.EqualTo(working.ActiveWork.OriginCommandId));
            Assert.That(record.TargetStableId, Is.EqualTo(working.ActiveWork.TargetStableId));
            Assert.That(record.ResultRevision, Is.EqualTo(completed.Revision));
            Assert.That(record.EvidenceStableId, Is.Not.Empty);
            Assert.That(record.RecordHashSha256, Has.Length.EqualTo(64));
            var clone = completed.Clone();
            clone.HarvestCompletionRecords[0].ActorStableId = "changed-by-reader";
            Assert.That(record.ActorStableId, Is.EqualTo(completed.PlayerStableId));
            var again = adapter.Snapshot();
            Assert.That(again.Revision, Is.EqualTo(completed.Revision));
            Assert.That(again.HarvestCompletionRecords[0].ActorStableId, Is.EqualTo(completed.PlayerStableId));
            Assert.That(UnityEngine.JsonUtility.ToJson(completed), Does.Not.Contain("HarvestCompletionRecords"));
        }

        [Test]
        public void 벌목취소와새관측은_완료근거를합성하지않는다()
        {
            using var adapter = new Nature생존CoreRuntimeAdapter(LocalSimulationUnityRuntime.Create());
            Assert.That(adapter.Snapshot().HarvestCompletionRecords, Is.Empty);
            adapter.AcquireAxe();
            EquipAxe(adapter);
            adapter.BeginHarvest("resource:nature-tree:01");
            adapter.AdvanceRealtime(1, true, false);
            adapter.CancelActiveWork();
            var cancelled = adapter.Snapshot();
            Assert.That(cancelled.ActiveWork, Is.Null);
            Assert.That(cancelled.HarvestCompletionRecords, Is.Empty);
            adapter.BeginHarvest("resource:nature-tree:01");
            Assert.That(adapter.Snapshot().HarvestCompletionRecords, Is.Empty);
        }

        [Test]
        public void 벌목조회는_Core의Actor와시작명령을_추정없이보존한다()
        {
            var runtime = LocalSimulationUnityRuntime.Create();
            using var adapter = new Nature생존CoreRuntimeAdapter(runtime);
            adapter.AcquireAxe();
            EquipAxe(adapter);
            adapter.BeginHarvest("resource:nature-tree:01");
            var first = adapter.Snapshot();
            var core = runtime.Nature.GetAsync(first.SessionStableId)
                .AsTask().GetAwaiter().GetResult();
            var repeated = adapter.Snapshot();

            Assert.That(first.PlayerStableId, Is.EqualTo(core.PlayerStableId));
            Assert.That(first.ActiveWork!.ActorStableId,
                Is.EqualTo(core.ActiveWork!.ActorStableId).And.Not.Empty);
            Assert.That(first.ActiveWork.OriginCommandId,
                Is.EqualTo(core.ActiveWork.OriginCommandId).And.Not.Empty);
            Assert.That(repeated.Revision, Is.EqualTo(first.Revision));
            Assert.That(repeated.ActiveWork!.OriginCommandId,
                Is.EqualTo(first.ActiveWork.OriginCommandId));
            var clone = first.Clone();
            Assert.That(clone.PlayerStableId, Is.EqualTo(first.PlayerStableId));
            Assert.That(clone.ActiveWork!.ActorStableId,
                Is.EqualTo(first.ActiveWork.ActorStableId));
            Assert.That(clone.ActiveWork.OriginCommandId,
                Is.EqualTo(first.ActiveWork.OriginCommandId));
            Assert.That(clone.ActiveWork, Is.Not.SameAs(first.ActiveWork));
        }

        [Test]
        public void 같은나무재작업은_이전시작명령과구분한다()
        {
            using var adapter = new Nature생존CoreRuntimeAdapter(
                LocalSimulationUnityRuntime.Create());
            adapter.AcquireAxe();
            EquipAxe(adapter);
            adapter.BeginHarvest("resource:nature-tree:01");
            var first = adapter.Snapshot();
            adapter.CancelActiveWork();
            Assert.That(adapter.Snapshot().ActiveWork, Is.Null);
            adapter.BeginHarvest("resource:nature-tree:01");
            var second = adapter.Snapshot();
            Assert.That(second.ActiveWork!.TargetStableId,
                Is.EqualTo(first.ActiveWork!.TargetStableId));
            Assert.That(second.ActiveWork.ActorStableId,
                Is.EqualTo(first.ActiveWork.ActorStableId));
            Assert.That(second.ActiveWork.OriginCommandId,
                Is.Not.EqualTo(first.ActiveWork.OriginCommandId));
        }

        [Test]
        public void 작업계보는_Unity구형직렬화형식에추가하지않는다()
        {
            var state = Nature생존LocalEngine.CreateDefaultFixture();
            state.PlayerStableId = "read-only-player";
            state.ActiveWork = new Nature생존WorkState
            {
                KindCode = Nature생존Codes.Harvest,
                TargetStableId = "resource:nature-tree:01",
                ActorStableId = "read-only-actor",
                OriginCommandId = "read-only-command",
                RequiredSeconds = 4,
                CompletedSeconds = 1,
            };
            var json = UnityEngine.JsonUtility.ToJson(state);
            Assert.That(json, Does.Not.Contain("PlayerStableId"));
            Assert.That(json, Does.Not.Contain("ActorStableId"));
            Assert.That(json, Does.Not.Contain("OriginCommandId"));
            var restored = UnityEngine.JsonUtility.FromJson<Nature생존State>(json);
            Assert.That(restored.ActiveWork!.CompletedSeconds, Is.EqualTo(1));
            Assert.That(string.IsNullOrEmpty(restored.PlayerStableId), Is.True);
            Assert.That(string.IsNullOrEmpty(restored.ActiveWork.ActorStableId), Is.True);
            Assert.That(string.IsNullOrEmpty(restored.ActiveWork.OriginCommandId), Is.True);
        }

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
            EquipAxe(adapter);
            adapter.BeginHarvest("resource:nature-tree:01");
            adapter.AdvanceRealtime(NatureSurvivalRules.HarvestWorkSeconds,
                true, false);

            var harvested = adapter.Snapshot();
            Assert.That(harvested.TimberQuantity, Is.Zero);
            Assert.That(harvested.DroppedTimber, Has.Length.EqualTo(1));
            adapter.CollectDroppedTimber(harvested.DroppedTimber[0].StableId);
            var state = adapter.Snapshot();
            Assert.That(state.TimberQuantity,
                Is.EqualTo(NatureSurvivalRules.HarvestTimberQuantity));
            Assert.That(state.ResourceNodes[0].StateCode,
                Is.EqualTo(Nature생존Codes.Stump));
            Assert.That(state.ActiveWork, Is.Null);
            Assert.That(state.Revision, Is.GreaterThan(initial.Revision));
        }

        [Test]
        public void 도끼획득표현Trace는_전술폐루프의_권위행위기록을읽는다()
        {
            using var adapter = new Nature생존CoreRuntimeAdapter(
                LocalSimulationUnityRuntime.Create());

            adapter.AcquireAxe();

            Assert.That(adapter.LastEngineInteractionTrace, Is.Not.Empty);
            Assert.That(adapter.LastEngineInteractionTrace, Has.All.Matches<
                SimulationPlayableLoopEngineTraceEntry>(entry =>
                    entry.PlayableLoopStableId == SimulationNatureSurvivalCodes
                        .TacticalSelfNavigationPlayableLoopStableId
                    && entry.WorldInteractionId ==
                    SimulationNatureSurvivalCodes.AcquireAxeWorldInteractionId));
            Assert.That(adapter.LastEngineInteractionTrace,
                Has.Exactly(1).Matches<SimulationPlayableLoopEngineTraceEntry>(
                    entry => entry.PhaseCode ==
                        SimulationEngineInteractionPhaseCodes.ActionRecordAppend
                        && entry.StatusCode ==
                        SimulationEngineInteractionStatusCodes.Executed));
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
        public void 공통LocalRuntime은_V27장착슬롯을_새Runtime에서복원한다()
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
                    EquipAxe(first);
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
                Assert.That(restored.ActorEquipmentSnapshot().CapabilityCodes,
                    Does.Contain(SimulationActorEquipmentCodes.Woodcutting));
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
                    Is.EqualTo("nature-e7-validation-r5-primary"));
                Assert.That(state.ProfileRevision,
                    Is.EqualTo(SimulationNatureSurvivalCodes.ProfileRevisionR5));
                Assert.That(scope.RestoredFromSlot, Is.False);
                Assert.That(state.ElapsedSecondsInCycle,
                    Is.EqualTo(NatureSurvivalRules.DaylightEndsAtSecond - 90));
                Assert.That(state.Encounter, Is.Null);
                var session = scope.Runtime.Sessions
                    .GetAsync(scope.SessionStableId)
                    .AsTask().GetAwaiter().GetResult();
                Assert.That(session.Atmosphere.IsEnabled, Is.True);
                Assert.That(session.Atmosphere.ProfileStableId, Is.EqualTo(
                    WorldAtmosphereProfileCodes.NatureNightDay2FixtureR1));
                Assert.That(session.Atmosphere.RuleRevision,
                    Is.EqualTo(WorldAtmosphereRuleRevisions.R1));
                Assert.That(session.Atmosphere.ClockSourceCode, Is.EqualTo(
                    WorldAtmosphereClockSourceCodes.NatureCycleClock));
                Assert.That(session.Atmosphere.ElapsedSecondsInCycle,
                    Is.EqualTo(state.ElapsedSecondsInCycle));
            }
            finally
            {
                SimulationWorldLocalRuntimeScope.Reset();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void 황혼조우는_WI01실제E5공간에서_관찰Confirm후선택을연다()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-wi01-e7-" + Guid.NewGuid().ToString("N"));
            try
            {
                var scope = SimulationWorldLocalRuntimeScope.Install(
                    Nature생존LaunchProfile.E7Validation, root);
                using var adapter = new Nature생존CoreRuntimeAdapter(
                    scope.Runtime, scope.SessionStableId);
                adapter.AcquireAxe();
                EquipAxe(adapter);
                foreach (var node in adapter.Snapshot().ResourceNodes)
                {
                    adapter.BeginHarvest(node.StableId);
                    adapter.AdvanceRealtime(
                        NatureSurvivalRules.HarvestWorkSeconds, true, false);
                }
                adapter.AdvanceRealtime(60, false, false);
                adapter.AdvanceRealtime(14, false, false);

                var encounter = adapter.Snapshot().Encounter;
                Assert.That(encounter, Is.Not.Null);
                Assert.That(encounter!.StateCode,
                    Is.EqualTo(Nature생존Codes.Pending));
                Assert.That(adapter.ThreatObservationConfirmed, Is.False);

                var preview = adapter.PreviewThreatObservation();
                Assert.That(preview.NatureRouteCode,
                    Is.EqualTo("NatureHomeTwilight"));
                Assert.That(preview.CanConfirm, Is.True,
                    string.Join(",", preview.BlockingReasonCodes));
                Assert.That(preview.NextWorldInteractionIds,
                    Does.Contain("WI-NATURE-11"));

                adapter.ConfirmThreatObservation();

                Assert.That(adapter.ThreatObservationConfirmed, Is.True);
                var session = scope.Runtime.Sessions.GetAsync(scope.SessionStableId)
                    .AsTask().GetAwaiter().GetResult();
                Assert.That(session.Tasks, Has.Exactly(1).Matches<
                    SimulationTaskSnapshot>(task => task.TaskStableId.StartsWith(
                        "task:nature-threat-observation:",
                        StringComparison.Ordinal)));
                Assert.That(session.IsOperationalState, Is.False);
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
                {
                    adapter.AcquireAxe();
                    EquipAxe(adapter);
                }
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

        private static void EquipAxe(Nature생존CoreRuntimeAdapter adapter)
        {
            var acquired = adapter.ActorEquipmentSnapshot();
            var axe = Array.Find(acquired.ItemInstances, value =>
                value.ItemCode == SimulationNatureSurvivalCodes.AxeItemCode);
            Assert.That(axe, Is.Not.Null);
            var equipped = adapter.Equip(axe!.ItemInstanceStableId,
                SimulationActorEquipmentCodes.MainHand);
            Assert.That(equipped.CapabilityCodes,
                Does.Contain(SimulationActorEquipmentCodes.Woodcutting));
        }

        [Test]
        public void 공통WorldScope은_손상슬롯을_새Session으로숨기지않는다()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "ssalddel-world-scope-corrupt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,
                "nature-e7-validation-r5-primary.ssalddel"), "{broken");
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
