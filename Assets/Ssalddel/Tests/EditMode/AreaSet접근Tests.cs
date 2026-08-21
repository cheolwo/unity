using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class AreaSet접근Tests
    {
        [Test]
        public async Task 서버근거를얻은Hub는_Preview_Confirm_Tick뒤새WI를연다()
        {
            var authority = new FakeAuthority();
            var coordinator = new AreaSet접근Coordinator(authority);
            var initial = await coordinator.InitializeAsync("simulation-session:area",
                AreaSet접근Codes.PlayerOwner, CancellationToken.None);
            Assert.That(initial.Hub.AccessStateCode, Is.EqualTo(AreaSet접근Codes.Granted));

            var preview = await coordinator.PreviewHubAsync(CancellationToken.None);
            Assert.That(preview.CanConfirm, Is.True);
            Assert.That(preview.NewWorldInteractionIds, Does.Contain("WI-MFG-01"));
            await coordinator.ConfirmHubAsync(CancellationToken.None);
            var arrived = await coordinator.AdvanceToHubAsync(CancellationToken.None);

            Assert.That(arrived.CurrentAreaSetStableId,
                Is.EqualTo(AreaSet접근Codes.HubAreaSet));
            Assert.That(arrived.Hub.AccessStateCode,
                Is.EqualTo(AreaSet접근Codes.Entered));
            Assert.That(arrived.Hub.AvailableWorldInteractionIds,
                Does.Contain("WI-MFG-01"));
            Assert.That(authority.ConfirmCount, Is.EqualTo(1));
            Assert.That(authority.TickCount, Is.EqualTo(1));
        }

        private sealed class FakeAuthority : IAreaSet접근AuthorityClient
        {
            private long revision = 7;
            private string state = AreaSet접근Codes.Granted;
            public int ConfirmCount;
            public int TickCount;

            public Task<AreaSet접근Data> RefreshAsync(string sessionStableId,
                string playerStableId, CancellationToken cancellationToken)
                => Task.FromResult(Context());

            public Task<AreaSet이동PreviewData> PreviewAsync(string sessionStableId,
                string playerStableId, long expectedRevision,
                CancellationToken cancellationToken) => Task.FromResult(new AreaSet이동PreviewData
                {
                    BaseRevision = expectedRevision,
                    TargetAreaSetStableId = AreaSet접근Codes.HubAreaSet,
                    ConnectorStableId = AreaSet접근Codes.FarmToHubConnector,
                    AccessStateCode = state,
                    EvidenceIds = new[] { "evidence:farm-hub-shipment-completed" },
                    NewWorldInteractionIds = new[] { "WI-MFG-01" },
                    DurationTicks = 1,
                    CanConfirm = true,
                    PreviewHashSha256 = "sha256:area-preview",
                });

            public Task<AreaSet접근Data> ConfirmAsync(string sessionStableId,
                string playerStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                ConfirmCount++;
                revision++;
                return Task.FromResult(Context());
            }

            public Task<AreaSet접근Data> AdvanceAsync(string sessionStableId,
                string playerStableId, long expectedRevision, int tickCount,
                CancellationToken cancellationToken)
            {
                TickCount += tickCount;
                revision++;
                state = AreaSet접근Codes.Entered;
                return Task.FromResult(Context());
            }

            private AreaSet접근Data Context() => new()
            {
                WorldRevision = revision,
                WorldTick = TickCount,
                CurrentAreaSetStableId = state == AreaSet접근Codes.Entered
                    ? AreaSet접근Codes.HubAreaSet : AreaSet접근Codes.FarmAreaSet,
                AccessEntries = new[]
                {
                    Entry(AreaSet접근Codes.FarmAreaSet, AreaSet접근Codes.Entered,
                        Array.Empty<string>()),
                    Entry(AreaSet접근Codes.HubAreaSet, state,
                        state == AreaSet접근Codes.Entered ? new[] { "WI-MFG-01" }
                            : Array.Empty<string>()),
                },
                MutatesStaticHDefinitions = false,
            };

            private static AreaSet접근EntryData Entry(string area, string access,
                string[] wi) => new()
                {
                    AreaSetStableId = area,
                    AccessStateCode = access,
                    GrantedByEvidenceIds = new[] { "evidence:farm-hub-shipment-completed" },
                    AvailableWorldInteractionIds = wi,
                    SourceHDefinitionHashSha256 = new string('a', 64),
                    AccessHashSha256 = new string('b', 64),
                };
        }
    }
}
