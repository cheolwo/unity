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
    public sealed class Farm창고건설Tests
    {
        [Test]
        public async Task 창고건설은_Preview_Confirm_두Tick뒤용량을연다()
        {
            var authority = new FakeAuthority();
            var coordinator = new Farm창고건설Coordinator(authority);
            var initial = await coordinator.InitializeAsync(
                "simulation-session:farm-storage", CancellationToken.None);
            Assert.That(initial.StorageAvailable, Is.EqualTo(0m));

            var resourcePreview = await coordinator.PreviewResourceAsync(
                CancellationToken.None);
            Assert.That(resourcePreview.CanConfirm, Is.True);
            Assert.That(authority.LastResourcePreviewRevision, Is.EqualTo(4));
            var producing = await coordinator.ConfirmResourceAsync(
                CancellationToken.None);
            Assert.That(producing.ResourceStateCode,
                Is.EqualTo(Farm창고건설Codes.ResourceProducing));
            await coordinator.AdvanceOneTickAsync(CancellationToken.None);
            var resources = await coordinator.AdvanceOneTickAsync(
                CancellationToken.None);
            Assert.That(resources.ResourceStateCode,
                Is.EqualTo(Farm창고건설Codes.ResourceReady));
            Assert.That(resources.FacilityComponentQuantity, Is.EqualTo(2m));

            var preview = await coordinator.PreviewAsync(CancellationToken.None);
            Assert.That(preview.CanConfirm, Is.True);
            Assert.That(authority.LastPreviewRevision, Is.EqualTo(7));

            var planned = await coordinator.ConfirmAsync(CancellationToken.None);
            Assert.That(planned.StateCode, Is.EqualTo(Farm창고건설Codes.Planned));
            Assert.That(planned.StorageCapacity, Is.EqualTo(2000m));

            var building = await coordinator.AdvanceOneTickAsync(
                CancellationToken.None);
            Assert.That(building.StateCode, Is.EqualTo(Farm창고건설Codes.Building));
            Assert.That(building.StorageAvailable, Is.EqualTo(0m));

            var operational = await coordinator.AdvanceOneTickAsync(
                CancellationToken.None);
            Assert.That(operational.StateCode,
                Is.EqualTo(Farm창고건설Codes.Operational));
            Assert.That(operational.StorageCapacity, Is.EqualTo(2500m));
            Assert.That(operational.StorageAvailable, Is.EqualTo(500m));
            Assert.That(authority.ConfirmCount, Is.EqualTo(1));
            Assert.That(authority.TickCount, Is.EqualTo(4));

            var refreshed = await coordinator.RefreshAsync(CancellationToken.None);
            Assert.That(refreshed.WorldRevision, Is.EqualTo(10));
            Assert.That(refreshed.StorageAvailable, Is.EqualTo(500m));

            var save = await coordinator.SaveAndReloadAsync(CancellationToken.None);
            Assert.That(save.SchemaVersion, Is.EqualTo("simulation-save.v8"));
            Assert.That(save.ReplayHash, Is.EqualTo("sha256:farm-g4"));
            Assert.That(save.Restored.StorageCapacity, Is.EqualTo(2500m));
        }

        [Test]
        public void 창고건설은_Preview없이확정하지않는다()
        {
            var coordinator = new Farm창고건설Coordinator(new FakeAuthority());
            Assert.DoesNotThrowAsync(async () => await coordinator.InitializeAsync(
                "simulation-session:farm-storage", CancellationToken.None));
            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
                await coordinator.ConfirmAsync(CancellationToken.None));
        }

        private sealed class FakeAuthority : IFarm창고건설AuthorityClient
        {
            private long revision = 4;
            private int tick = 2;
            private string state = Farm창고건설Codes.NotStarted;
            private string resourceState = Farm창고건설Codes.ResourceAvailable;

            public long LastPreviewRevision;
            public long LastResourcePreviewRevision;
            public int ConfirmCount;
            public int TickCount;

            public Task<Farm창고건설ContextData> RefreshAsync(
                string sessionStableId, CancellationToken cancellationToken)
                => Task.FromResult(Context(sessionStableId));

            public Task<Farm창고건설PreviewData> PreviewAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                LastPreviewRevision = expectedRevision;
                return Task.FromResult(new Farm창고건설PreviewData
                {
                    SourceWorldRevision = expectedRevision,
                    CanConfirm = true,
                    ReservedLotStableIds = new[]
                    {
                        "lot:unity.farm-small-storage-components",
                    },
                    PreviewHashSha256 = "sha256:farm-storage-preview",
                });
            }

            public Task<Farm창고건설PreviewData> PreviewResourceAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                LastResourcePreviewRevision = expectedRevision;
                return Task.FromResult(new Farm창고건설PreviewData
                {
                    SourceWorldRevision = expectedRevision,
                    CanConfirm = true,
                    ReservedLotStableIds = new[]
                    {
                        "lot:unity.hub-component-raw-material",
                    },
                    PreviewHashSha256 = "sha256:farm-storage-resource-preview",
                });
            }

            public Task<Farm창고건설ContextData> ConfirmResourceAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                revision++;
                resourceState = Farm창고건설Codes.ResourceProducing;
                return Task.FromResult(Context(sessionStableId));
            }

            public Task<Farm창고건설ContextData> ConfirmAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                ConfirmCount++;
                revision++;
                state = Farm창고건설Codes.Planned;
                return Task.FromResult(Context(sessionStableId));
            }

            public Task<Farm창고건설ContextData> AdvanceOneTickAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                TickCount++;
                revision++;
                tick++;
                if (resourceState == Farm창고건설Codes.ResourceProducing)
                {
                    if (TickCount == 2)
                        resourceState = Farm창고건설Codes.ResourceReady;
                }
                else
                {
                    state = TickCount == 3
                        ? Farm창고건설Codes.Building
                        : Farm창고건설Codes.Operational;
                }
                return Task.FromResult(Context(sessionStableId));
            }

            public Task<FarmGameplaySaveReloadData> SaveAndReloadAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
                => Task.FromResult(new FarmGameplaySaveReloadData
                {
                    SaveStableId = "save:unity.farm-g4:" + expectedRevision,
                    SchemaVersion = "simulation-save.v8",
                    ReplayHash = "sha256:farm-g4",
                    ReplayedCommandCount = 9,
                    Restored = Context(sessionStableId),
                });

            private Farm창고건설ContextData Context(string sessionStableId)
            {
                var operational = state == Farm창고건설Codes.Operational;
                return new Farm창고건설ContextData
                {
                    SessionStableId = sessionStableId,
                    WorldRevision = revision,
                    WorldTick = tick,
                    StateCode = state,
                    FacilityStableId = operational
                        ? "facility:player-built:farm-small-storage" : string.Empty,
                    ResourceStateCode = resourceState,
                    FacilityComponentQuantity = resourceState
                        == Farm창고건설Codes.ResourceReady ? 2m : 0m,
                    StorageCapacity = operational ? 2500m : 2000m,
                    StorageOccupied = 2000m,
                    StorageReserved = 0m,
                    StorageAvailable = operational ? 500m : 0m,
                    StorageUnitCode = "KGM",
                };
            }
        }
    }
}
