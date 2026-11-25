using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;

namespace Ssalddel.Unity.Tests.EditMode
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "출고 프로필의 명시 선택·슬롯 격리·부분 피킹 저장 복원과 잘못된 슬롯 보존을 검증한다.",
        Boundary = "독립 임시 저장 루트의 EditMode 시험이며 실제 Game View·Play Mode·운영 재고 증거가 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3저장재생검증)]
    public sealed class 다중센터출고프로필Tests
    {
        private const string FulfillmentFlag = "SSALDDEL_FULFILLMENT_OBSERVER";
        private static readonly string[] EnvironmentNames =
        {
            FulfillmentFlag,
            "SSALDDEL_SAGAJEONG_FOOD_LIFE",
            "SSALDDEL_SYNTHETIC_DELIVERY",
            "SSALDDEL_OFFLINE_LIFE_BUNDLE",
            "SSALDDEL_RESTAURANT_OBSERVER",
            "SSALDDEL_HANS_FENCE_E5",
            "SSALDDEL_NATURE_E7_VALIDATION",
        };

        private readonly Dictionary<string, string> previousEnvironment =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private string savesRoot;
        private bool environmentCaptured;
        private SimulationWorldLocalRuntimeScope ownedScope;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SimulationWorldLocalRuntimeScope.IsInstalled, Is.False,
                "이미 설치된 다른 실행 범위는 시험이 종료하거나 초기화하지 않습니다.");
            previousEnvironment.Clear();
            foreach (var name in EnvironmentNames)
                previousEnvironment.Add(name, Environment.GetEnvironmentVariable(name));
            environmentCaptured = true;
            foreach (var name in EnvironmentNames)
                Environment.SetEnvironmentVariable(name, null);

            savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-fulfillment-profile-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(savesRoot);
            TestContext.Progress.WriteLine("출고 프로필 시험 저장 루트: " + savesRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { ResetOwnedScope(); }
            finally
            {
                if (environmentCaptured)
                    foreach (var pair in previousEnvironment)
                        Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                environmentCaptured = false;
                // 임시 파일은 검증 근거로 보존하며 사용자 저장이나 다른 시험 폴더를 삭제하지 않는다.
            }
        }

        [Test]
        public async Task 출고플래그가꺼져있으면_기본프로필과기존저장정체성을유지한다()
        {
            Environment.SetEnvironmentVariable(FulfillmentFlag, "0");
            Assert.That(SimulationWorldLocalRuntimeScope.FulfillmentObserverRequested, Is.False);
            Assert.That(SimulationWorldLocalRuntimeScope.DetectLaunchProfile(),
                Is.EqualTo(Nature생존LaunchProfile.PlayableDefault));

            var initial = Install(Nature생존LaunchProfile.PlayableDefault);
            var original = await initial.Runtime.Sessions.GetAsync(initial.SessionStableId);
            Assert.That(initial.RestoredFromSlot, Is.False);
            Assert.That(initial.SlotStableId, Is.EqualTo("solo-local-r5-primary"));
            Assert.That(original.ScenarioStableId, Is.EqualTo("scenario:unity.solo-local-world"));
            Assert.That(original.Fulfillment, Is.Null);
            initial.SaveCurrent();
            var defaultFiles = ReadSavedFiles();
            Assert.That(defaultFiles, Is.Not.Empty);

            ResetOwnedScope();
            Environment.SetEnvironmentVariable(FulfillmentFlag, "1");
            var fulfillment = Install(Nature생존LaunchProfile.FulfillmentCenter);
            Assert.That(fulfillment.SlotStableId,
                Is.EqualTo(SimulationWorldLocalRuntimeScope.FulfillmentSlotStableId));
            Assert.That(fulfillment.SlotStableId, Is.Not.EqualTo(initial.SlotStableId));
            fulfillment.SaveCurrent();
            AssertFilesUnchanged(defaultFiles);

            ResetOwnedScope();
            Environment.SetEnvironmentVariable(FulfillmentFlag, "0");
            var restored = Install(Nature생존LaunchProfile.PlayableDefault);
            var state = await restored.Runtime.Sessions.GetAsync(restored.SessionStableId);
            Assert.That(restored.RestoredFromSlot, Is.True);
            Assert.That(restored.LaunchProfile, Is.EqualTo(Nature생존LaunchProfile.PlayableDefault));
            Assert.That(state.SessionStableId, Is.EqualTo(original.SessionStableId));
            Assert.That(state.ScenarioStableId, Is.EqualTo(original.ScenarioStableId));
            Assert.That(state.Revision, Is.EqualTo(original.Revision));
            Assert.That(state.Fulfillment, Is.Null);
            AssertFilesUnchanged(defaultFiles);
        }

        [Test]
        public async Task 출고프로필은한번만초기화하고_부분피킹을저장복원한뒤같은명령을재사용한다()
        {
            Environment.SetEnvironmentVariable(FulfillmentFlag, "1");
            Assert.That((int)Nature생존LaunchProfile.FulfillmentCenter, Is.EqualTo(11));
            Assert.That(SimulationWorldLocalRuntimeScope.DetectLaunchProfile(),
                Is.EqualTo(Nature생존LaunchProfile.FulfillmentCenter));

            var scope = Install(Nature생존LaunchProfile.FulfillmentCenter);
            Assert.That(scope.RestoredFromSlot, Is.False);
            var initial = await scope.Runtime.Sessions.GetAsync(scope.SessionStableId);
            Assert.That(initial.ScenarioStableId, Is.EqualTo(다중센터출고표본.ScenarioId));
            Assert.That(initial.Fulfillment, Is.Not.Null);
            Assert.That(initial.Fulfillment.ScenarioStableId, Is.EqualTo(다중센터출고표본.ScenarioId));
            Assert.That(initial.Revision, Is.EqualTo(1));
            Assert.That(initial.Fulfillment.Centers.Length, Is.GreaterThanOrEqualTo(2));

            var reused = SimulationWorldLocalRuntimeScope.Install(
                Nature생존LaunchProfile.FulfillmentCenter, savesRoot);
            Assert.That(reused, Is.SameAs(scope));
            Assert.That((await reused.Runtime.Sessions.GetAsync(reused.SessionStableId)).Revision,
                Is.EqualTo(initial.Revision));

            var reserved = await scope.Runtime.ConfirmFulfillmentAsync(scope.SessionStableId,
                new SimulationFulfillmentCommand
                {
                    CommandId = "command:unity-profile:reserve",
                    ExpectedRevision = initial.Revision,
                    ActionCode = SimulationFulfillmentActionCodes.Reserve,
                    Order = 다중센터출고표본.Order(),
                });
            var allocation = reserved.Fulfillment.Orders.Single().Allocations.Single();
            var partialPick = new SimulationFulfillmentCommand
            {
                CommandId = "command:unity-profile:partial-pick",
                ExpectedRevision = reserved.Revision,
                ActionCode = SimulationFulfillmentActionCodes.Pick,
                OrderStableId = reserved.Fulfillment.Orders.Single().OrderStableId,
                AllocationStableId = allocation.AllocationStableId,
                LocationBarcode = allocation.LocationBarcode,
                ProductBarcode = allocation.ProductBarcode,
                Quantity = 1m,
            };
            var picked = await scope.Runtime.ConfirmFulfillmentAsync(scope.SessionStableId, partialPick);
            AssertPartialPick(picked, allocation.StockStableId);
            var saved = scope.SaveCurrent();
            Assert.That(saved.SavedWorldRevision, Is.EqualTo(picked.Revision));

            ResetOwnedScope();
            var restored = Install(Nature생존LaunchProfile.FulfillmentCenter);
            Assert.That(restored.RestoredFromSlot, Is.True);
            Assert.That(restored.SlotStableId, Is.EqualTo(saved.SlotStableId));
            var state = await restored.Runtime.Sessions.GetAsync(restored.SessionStableId);
            Assert.That(state.SessionStableId, Is.EqualTo(picked.SessionStableId));
            Assert.That(state.ScenarioStableId, Is.EqualTo(다중센터출고표본.ScenarioId));
            Assert.That(state.Fulfillment.ScenarioStableId, Is.EqualTo(다중센터출고표본.ScenarioId));
            Assert.That(state.Revision, Is.EqualTo(picked.Revision),
                "저장된 부분 피킹 뒤에 초기 Configure를 다시 실행하지 않아야 합니다.");
            AssertPartialPick(state, allocation.StockStableId);

            await restored.Runtime.ConfirmFulfillmentAsync(restored.SessionStableId, partialPick);
            var afterRetry = await restored.Runtime.Sessions.GetAsync(restored.SessionStableId);
            Assert.That(afterRetry.Revision, Is.EqualTo(state.Revision));
            AssertPartialPick(afterRetry, allocation.StockStableId);
        }

        [TestCase("WrongSession")]
        [TestCase("MissingModule")]
        [TestCase("WrongModule")]
        public async Task 다른시나리오나모듈이없는슬롯은_덮어쓰지않고_설치를거부한다(string mismatch)
        {
            using (var runtime = LocalSimulationUnityRuntime.Create(savesRoot))
            {
                var request = 다중센터출고표본.Create(Guid.NewGuid());
                if (mismatch == "WrongSession")
                    request.ScenarioStableId = "scenario:profile-test:foreign";
                var created = await runtime.Sessions.CreateAsync(request);
                var revision = created.Revision;
                if (mismatch != "MissingModule")
                {
                    var state = 다중센터출고표본.InitialState();
                    state.ScenarioStableId = "scenario:profile-test:foreign";
                    var configured = await runtime.ConfirmFulfillmentAsync(created.SessionStableId,
                        new SimulationFulfillmentCommand
                        {
                            CommandId = "command:profile-test:foreign-configure",
                            ExpectedRevision = revision,
                            ActionCode = SimulationFulfillmentActionCodes.Configure,
                            InitialState = state,
                        });
                    revision = configured.Revision;
                }
                await runtime.Sessions.SaveSlotAsync(created.SessionStableId,
                    new SimulationLocalSaveSlotRequest
                    {
                        SlotStableId = SimulationWorldLocalRuntimeScope.FulfillmentSlotStableId,
                        ExpectedRevision = revision,
                    });
            }
            var originalFiles = ReadSavedFiles();
            Assert.That(originalFiles, Is.Not.Empty);

            var error = Assert.Throws<InvalidOperationException>(() =>
                SimulationWorldLocalRuntimeScope.Install(Nature생존LaunchProfile.FulfillmentCenter, savesRoot));
            Assert.That(error.Message, Is.EqualTo("FulfillmentProfileSaveMismatch"));
            Assert.That(SimulationWorldLocalRuntimeScope.IsInstalled, Is.False);
            Assert.That(ReadSavedFiles().Keys, Is.EquivalentTo(originalFiles.Keys));
            AssertFilesUnchanged(originalFiles);
        }

        private SimulationWorldLocalRuntimeScope Install(Nature생존LaunchProfile profile)
        {
            ownedScope = SimulationWorldLocalRuntimeScope.Install(profile, savesRoot);
            return ownedScope;
        }

        private void ResetOwnedScope()
        {
            if (ownedScope != null
                && SimulationWorldLocalRuntimeScope.TryGet(out var current)
                && ReferenceEquals(current, ownedScope))
                SimulationWorldLocalRuntimeScope.Reset();
            ownedScope = null;
        }

        private Dictionary<string, byte[]> ReadSavedFiles()
            => Directory.GetFiles(savesRoot, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);

        private static void AssertFilesUnchanged(Dictionary<string, byte[]> originals)
        {
            foreach (var file in originals)
            {
                Assert.That(File.Exists(file.Key), Is.True, file.Key);
                Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
            }
        }

        private static void AssertPartialPick(경영SimulationSessionSnapshot snapshot, string stockId)
        {
            var order = snapshot.Fulfillment.Orders.Single();
            var allocation = order.Allocations.Single();
            var stock = snapshot.Fulfillment.Stocks.Single(value => value.StockStableId == stockId);
            Assert.That(order.StateCode, Is.EqualTo(SimulationFulfillmentStateCodes.Picking));
            Assert.That(allocation.PickedQuantity, Is.EqualTo(1m));
            Assert.That(stock.OnHandQuantity, Is.EqualTo(9m));
            Assert.That(stock.ReservedQuantity, Is.EqualTo(2m));
        }
    }
}
