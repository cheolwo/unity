using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Hosted비콘Tests
    {
        [Test]
        public async Task Solo에서초대손님작업까지_서버단계를따르고_ESC는세계를멈추지않는다()
        {
            var authority = new FakeAuthority();
            var coordinator = new Hosted비콘Coordinator(authority);
            var solo = await coordinator.InitializeAsync("simulation-session:hosted",
                CancellationToken.None);
            Assert.That(solo.SessionModeCode, Is.EqualTo(Hosted비콘Codes.Solo));
            Assert.That(solo.EscPausesWorld, Is.False);

            Assert.That((await coordinator.PreviewOpenAsync(CancellationToken.None))
                .CanConfirm, Is.True);
            await coordinator.ConfirmOpenAsync(CancellationToken.None);
            var invited = await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(invited.Guest?.ParticipantStateCode,
                Is.EqualTo(Hosted비콘Codes.Invited));

            Assert.That((await coordinator.PreviewJoinAsync(CancellationToken.None))
                .CanConfirm, Is.True);
            await coordinator.ConfirmJoinAsync(CancellationToken.None);
            var active = await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(active.Guest?.ParticipantStateCode,
                Is.EqualTo(Hosted비콘Codes.Active));
            Assert.That(active.Grant(Hosted비콘Codes.PerformWork),
                Is.EqualTo(Hosted비콘Codes.Allow));
            Assert.That(active.Grant(Hosted비콘Codes.Build),
                Is.EqualTo(Hosted비콘Codes.Deny));

            var denied = await coordinator.PreviewGuestActionAsync(
                Hosted비콘Codes.Build, CancellationToken.None);
            Assert.That(denied.CanConfirm, Is.False);
            Assert.That(denied.BlockingReasonCodes,
                Does.Contain("SimulationHostedPermissionDenied"));
            var work = await coordinator.PreviewGuestActionAsync(
                Hosted비콘Codes.PerformWork, CancellationToken.None);
            Assert.That(work.CanConfirm, Is.True);
            await coordinator.ConfirmGuestActionAsync(CancellationToken.None);
            var audited = await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(audited.AuditTrail.Length, Is.EqualTo(3));
            Assert.That(authority.TickCount, Is.EqualTo(3));
        }

        private sealed class FakeAuthority : IHosted비콘AuthorityClient
        {
            private long revision = 3;
            private int tick;
            private string mode = Hosted비콘Codes.Solo;
            private string guestState = string.Empty;
            private string pending = string.Empty;
            public int TickCount => tick;

            public Task<Hosted세계Data> RefreshAsync(string sessionStableId,
                CancellationToken cancellationToken) => Task.FromResult(State());
            public Task<Hosted행동PreviewData> PreviewOpenAsync(string sessionStableId,
                long expectedRevision, CancellationToken cancellationToken)
                => Preview(expectedRevision, "OpenHostedWorld", string.Empty, true);
            public Task<Hosted세계Data> ConfirmOpenAsync(string sessionStableId,
                long expectedRevision, CancellationToken cancellationToken)
                => Confirm("OpenHostedWorld");
            public Task<Hosted행동PreviewData> PreviewJoinAsync(string sessionStableId,
                long expectedRevision, CancellationToken cancellationToken)
                => Preview(expectedRevision, "JoinHostedWorld", string.Empty, true);
            public Task<Hosted세계Data> ConfirmJoinAsync(string sessionStableId,
                long expectedRevision, CancellationToken cancellationToken)
                => Confirm("JoinHostedWorld");
            public Task<Hosted행동PreviewData> PreviewGuestActionAsync(
                string sessionStableId, long expectedRevision, string capabilityCode,
                CancellationToken cancellationToken)
                => Preview(expectedRevision, "HostedGuestAction", capabilityCode,
                    capabilityCode == Hosted비콘Codes.PerformWork);
            public Task<Hosted세계Data> ConfirmGuestActionAsync(string sessionStableId,
                long expectedRevision, string capabilityCode,
                CancellationToken cancellationToken)
                => Confirm("HostedGuestAction");

            public Task<Hosted세계Data> AdvanceAsync(string sessionStableId,
                long expectedRevision, int tickCount, CancellationToken cancellationToken)
            {
                revision++;
                tick += tickCount;
                if (pending == "OpenHostedWorld")
                {
                    mode = Hosted비콘Codes.HostedMultiplayer;
                    guestState = Hosted비콘Codes.Invited;
                }
                else if (pending == "JoinHostedWorld") guestState = Hosted비콘Codes.Active;
                pending = string.Empty;
                return Task.FromResult(State());
            }

            private Task<Hosted세계Data> Confirm(string action)
            {
                pending = action;
                revision++;
                return Task.FromResult(State());
            }

            private static Task<Hosted행동PreviewData> Preview(long revision,
                string action, string capability, bool allowed)
                => Task.FromResult(new Hosted행동PreviewData
                {
                    BaseRevision = revision,
                    ActionCode = action,
                    CapabilityCode = capability,
                    GrantStateCode = allowed ? Hosted비콘Codes.Allow : Hosted비콘Codes.Deny,
                    DurationTicks = 1,
                    CanConfirm = allowed,
                    BlockingReasonCodes = allowed ? Array.Empty<string>()
                        : new[] { "SimulationHostedPermissionDenied" },
                    PreviewHashSha256 = new string('c', 64),
                });

            private Hosted세계Data State()
            {
                var active = guestState == Hosted비콘Codes.Active;
                return new Hosted세계Data
                {
                    WorldRevision = revision,
                    WorldTick = tick,
                    HostedSessionStableId = mode == Hosted비콘Codes.Solo
                        ? string.Empty : "hosted-session:test",
                    WorldStableId = "simulation-session:hosted",
                    OwnerPlayerStableId = Hosted비콘Codes.Owner,
                    SessionModeCode = mode,
                    Participants = string.IsNullOrWhiteSpace(guestState)
                        ? new[] { Participant(Hosted비콘Codes.Owner, Hosted비콘Codes.Active) }
                        : new[]
                        {
                            Participant(Hosted비콘Codes.Owner, Hosted비콘Codes.Active),
                            Participant(Hosted비콘Codes.Guest, guestState),
                        },
                    PermissionGrants = active ? new[]
                    {
                        Grant(Hosted비콘Codes.PerformWork, Hosted비콘Codes.Allow),
                        Grant(Hosted비콘Codes.Build, Hosted비콘Codes.Deny),
                        Grant(Hosted비콘Codes.Demolish, Hosted비콘Codes.Deny),
                    } : Array.Empty<Hosted권한Data>(),
                    AuditTrail = tick == 0 ? Array.Empty<Hosted감사Data>()
                        : new Hosted감사Data[tick],
                    HostLossBlocksMutation = true,
                    EscPausesWorld = false,
                    SessionHashSha256 = new string('d', 64),
                };
            }

            private static Hosted참여자Data Participant(string id, string state)
                => new()
                {
                    PlayerStableId = id,
                    ParticipantStateCode = state,
                    CurrentAreaSetStableId = Hosted비콘Codes.FarmAreaSet,
                };
            private static Hosted권한Data Grant(string capability, string state)
                => new()
                {
                    TargetPlayerStableId = Hosted비콘Codes.Guest,
                    ScopeStableId = Hosted비콘Codes.FarmAreaSet,
                    CapabilityCode = capability,
                    GrantStateCode = state,
                    GrantHashSha256 = new string('e', 64),
                };
        }
    }
}
