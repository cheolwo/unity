using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 오늘작업계획Tests
    {
        [Test]
        public async Task 작업계획은_Preview뒤한개개정으로원자확정한다()
        {
            var authority = new FakeAuthority();
            var coordinator = new 오늘작업계획Coordinator(authority);
            coordinator.Bind("simulation-session:daily-work", 7);
            var items = Items();

            var preview = await coordinator.PreviewAsync(items, CancellationToken.None);
            Assert.That(preview.CanConfirm, Is.True);
            Assert.That(preview.Items[0].ProjectedQuantity, Is.EqualTo(300m));

            var state = await coordinator.ConfirmAsync(
                "command:daily-work:confirm", items, CancellationToken.None);
            Assert.That(state.WorldRevision, Is.EqualTo(8));
            Assert.That(coordinator.Revision, Is.EqualTo(8));
            Assert.That(coordinator.CurrentPreview, Is.Null);
            Assert.That(authority.ConfirmCount, Is.EqualTo(1));
        }

        [Test]
        public void 작업계획은_Preview없이확정하지않는다()
        {
            var coordinator = new 오늘작업계획Coordinator(new FakeAuthority());
            coordinator.Bind("simulation-session:daily-work", 3);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await coordinator.ConfirmAsync("command:daily-work:invalid",
                    Items(), CancellationToken.None));
        }

        [Test]
        public async Task 현장한Tick은_서버Canonical개정과수확Lot을다시읽는다()
        {
            var coordinator = new 오늘작업계획Coordinator(new FakeAuthority());
            coordinator.Bind("simulation-session:daily-work", 10);

            var state = await coordinator.AdvanceOneTickAsync(CancellationToken.None);

            Assert.That(state.WorldRevision, Is.EqualTo(11));
            Assert.That(state.HarvestLots, Has.Length.EqualTo(1));
            Assert.That(state.HarvestLots[0].Quantity, Is.EqualTo(300m));
        }

        private static 오늘작업계획ItemData[] Items()
            => new[]
            {
                new 오늘작업계획ItemData
                {
                    PlanItemStableId = "plan-item:daily-work:harvest",
                    Priority = 10,
                    ActorStableId = 오늘작업계획Codes.PlayerActor,
                    TargetStableId = "farm-plot:pyeongchang:0:0",
                    ActionCode = 오늘작업계획Codes.Harvesting,
                    AssignmentKindCode = 오늘작업계획Codes.PlayerDirect,
                    PreferredSpatialStableId =
                        "spatial:unity:harvest-day:production-plot",
                },
            };

        private sealed class FakeAuthority : I오늘작업계획AuthorityClient
        {
            public int ConfirmCount;

            public Task<오늘작업계획PreviewData> PreviewAsync(
                string sessionStableId, long expectedRevision,
                오늘작업계획ItemData[] items, CancellationToken cancellationToken)
                => Task.FromResult(new 오늘작업계획PreviewData
                {
                    ExpectedRevision = expectedRevision,
                    CanConfirm = true,
                    Items = new[]
                    {
                        new 오늘작업계획ItemPreviewData
                        {
                            PlanItemStableId = items[0].PlanItemStableId,
                            Priority = items[0].Priority,
                            ActorStableId = items[0].ActorStableId,
                            TargetStableId = items[0].TargetStableId,
                            ActionCode = items[0].ActionCode,
                            AssignmentKindCode = items[0].AssignmentKindCode,
                            ProjectedQuantity = 300m,
                            ProjectedQuantityUnitCode = "KGM",
                            DurationTicks = 1,
                            EstimatedCompletionWorldTick = 1,
                            CanConfirm = true,
                        },
                    },
                });

            public Task<오늘작업CanonicalStateData> ConfirmAsync(
                string sessionStableId, string commandId, long expectedRevision,
                오늘작업계획ItemData[] items, CancellationToken cancellationToken)
            {
                ConfirmCount++;
                return Task.FromResult(State(expectedRevision + 1));
            }

            public Task<오늘작업CanonicalStateData> AdvanceOneTickAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
                => Task.FromResult(State(expectedRevision + 1));

            public Task<오늘작업CanonicalStateData> RefreshAsync(
                string sessionStableId, CancellationToken cancellationToken)
                => Task.FromResult(State(0));

            private static 오늘작업CanonicalStateData State(long revision)
                => new 오늘작업CanonicalStateData
                {
                    WorldRevision = revision,
                    WorldTick = 1,
                    HarvestLots = new[]
                    {
                        new 오늘수확LotData
                        {
                            HarvestLotStableId = "harvest-lot:daily-work:potato",
                            Quantity = 300m,
                            UnitCode = "KGM",
                            StateCode = "HarvestedAtField",
                        },
                    },
                };
        }
    }
}
