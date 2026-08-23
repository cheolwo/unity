using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Farm플레이어배치Tests
    {
        [Test]
        public async Task 후보는_25cm조정과_서버Preview뒤에만_확정된다()
        {
            var authority = new FakeAuthority();
            var coordinator = new Farm플레이어배치Coordinator(authority);
            await coordinator.InitializeAsync("session:farm-placement",
                CancellationToken.None);
            coordinator.SelectCandidate(new Farm배치CandidateData
            {
                BlueprintStableId = "blueprint:farm-small-storage.v1",
                PlacementZoneStableId = "placement-zone:farm:processing-south",
                TargetH2StableId = "h2:Farm:processing-south",
                LocalXCentimeters = 100,
                LocalZCentimeters = -5000,
                AccessConnectorStableId = "connector:FarmSouthRoad",
            });
            coordinator.NudgeCandidate(25, -25);
            coordinator.RotateCandidate();
            Assert.That(coordinator.CurrentCandidate!.LocalXCentimeters,
                Is.EqualTo(125));
            Assert.That(coordinator.CurrentCandidate.LocalZCentimeters,
                Is.EqualTo(-5025));
            Assert.That(coordinator.CurrentCandidate.RotationQuarterTurns,
                Is.EqualTo(1));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await coordinator.ConfirmAsync("command:without-preview",
                    CancellationToken.None));

            var preview = await coordinator.PreviewAsync(CancellationToken.None);
            Assert.That(preview.CanConfirm, Is.True);
            Assert.That(authority.LastCandidate!.LocalXCentimeters, Is.EqualTo(125));
            var confirmed = await coordinator.ConfirmAsync(
                "command:farm-placement", CancellationToken.None);
            Assert.That(confirmed.WorldRevision, Is.EqualTo(2));
            Assert.That(confirmed.Objects, Has.Length.EqualTo(1));
            Assert.That(authority.LastProposalStableId,
                Is.EqualTo("placement-proposal:test"));
            Assert.That(coordinator.CurrentCandidate, Is.Null);
        }

        [Test]
        public void 미세조정은_25cm배수만_받는다()
        {
            var coordinator = new Farm플레이어배치Coordinator(new FakeAuthority());
            Assert.DoesNotThrowAsync(async () => await coordinator.InitializeAsync(
                "session:farm-placement", CancellationToken.None));
            coordinator.SelectCandidate(new Farm배치CandidateData
            {
                BlueprintStableId = "blueprint:farm-small-storage.v1",
                PlacementZoneStableId = "placement-zone:farm:processing-south",
                TargetH2StableId = "h2:Farm:processing-south",
            });
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.NudgeCandidate(10, 0));
        }

        [Test]
        public void 배치모드입력잠금은_플레이어이동선택과_분리된다()
        {
            var gameObject = new GameObject("FarmPlacementPlayer");
            try
            {
                var player = gameObject.AddComponent<플레이어경관Controller>();
                player.SetPlacementInputBlocked(true);
                Assert.That(player.PlacementInputBlocked, Is.True);
                player.SetPlacementInputBlocked(false);
                Assert.That(player.PlacementInputBlocked, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void 연속배치격자는_기준점간격을_지키고_같은칸을_한번만예약한다()
        {
            Assert.That(Farm연속배치Grid.SnapToAnchor(-3190, -3600, 800),
                Is.EqualTo(-2800));
            Assert.That(Farm연속배치Grid.SnapToAnchor(3976, 3500, 300),
                Is.EqualTo(4100));
            Assert.Throws<InvalidOperationException>(() =>
                Farm연속배치Grid.SnapToAnchor(0, 0, 10));

            var stroke = new Farm연속배치Stroke();
            var first = new Farm배치CandidateData
            {
                BlueprintStableId = "blueprint:farm-fence-straight.v1",
                LocalXCentimeters = -3600,
                LocalZCentimeters = 3200,
            };
            stroke.Begin();
            Assert.That(stroke.TryReserve(first), Is.True);
            Assert.That(stroke.TryReserve(first.Clone()), Is.False);
            first.LocalXCentimeters = -2800;
            Assert.That(stroke.TryReserve(first), Is.True);
            Assert.That(stroke.AttemptedCellCount, Is.EqualTo(2));
            var farTarget = first.Clone();
            farTarget.LocalXCentimeters = 400;
            var adjacent = Farm연속배치Grid.NextAdjacent(first, farTarget, 800);
            Assert.That(adjacent.LocalXCentimeters, Is.EqualTo(-2000));
            Assert.That(adjacent.LocalZCentimeters, Is.EqualTo(3200));
            stroke.End();
            Assert.That(stroke.TryReserve(first), Is.False);
            Assert.That(stroke.AttemptedCellCount, Is.Zero);
        }

        private sealed class FakeAuthority : IFarm플레이어배치AuthorityClient
        {
            private long revision = 1;
            private int tick;
            private Farm배치ObjectData[] objects = Array.Empty<Farm배치ObjectData>();
            public Farm배치CandidateData? LastCandidate;
            public string LastProposalStableId = string.Empty;

            public Task<Farm배치ContextData> RefreshAsync(string sessionStableId,
                CancellationToken cancellationToken)
                => Task.FromResult(Context(sessionStableId));

            public Task<Farm배치PreviewData> PreviewAsync(string sessionStableId,
                long expectedRevision, Farm배치CandidateData candidate,
                CancellationToken cancellationToken)
            {
                LastCandidate = candidate.Clone();
                return Task.FromResult(new Farm배치PreviewData
                {
                    PlacementProposalStableId = "placement-proposal:test",
                    SourceWorldRevision = expectedRevision,
                    CanConfirm = true,
                    NormalizedCandidate = candidate.Clone(),
                    PlacementKindCode = "Building",
                    PlacementProfileRevision = "farm-placement-profile.r1",
                    FootprintWidthCentimeters = 600,
                    FootprintDepthCentimeters = 800,
                    MaterialRequirements = new[]
                    {
                        new Farm배치MaterialRequirementData
                        {
                            ItemCode = "FacilityComponent",
                            Quantity = 2m,
                            UnitCode = "unit",
                        },
                    },
                    ConstructionTicks = 2,
                    PreviewHashSha256 = "sha256:placement-preview",
                });
            }

            public Task<Farm배치ContextData> ConfirmAsync(string sessionStableId,
                long expectedRevision, string commandId,
                string placementProposalStableId, string previewHashSha256,
                CancellationToken cancellationToken)
            {
                LastProposalStableId = placementProposalStableId;
                revision++;
                objects = new[]
                {
                    new Farm배치ObjectData
                    {
                        FacilityStableId = "facility:player-built:" + commandId,
                        BlueprintStableId = "blueprint:farm-small-storage.v1",
                        LifecycleCode = Farm플레이어배치Codes.Planned,
                        PlacementZoneStableId =
                            "placement-zone:farm:processing-south",
                        TargetH2StableId = "h2:Farm:processing-south",
                    },
                };
                return Task.FromResult(Context(sessionStableId));
            }

            public Task<Farm배치ContextData> AdvanceOneTickAsync(
                string sessionStableId, long expectedRevision,
                CancellationToken cancellationToken)
            {
                revision++;
                tick++;
                return Task.FromResult(Context(sessionStableId));
            }

            private Farm배치ContextData Context(string sessionStableId) => new()
            {
                SessionStableId = sessionStableId,
                WorldRevision = revision,
                WorldTick = tick,
                Objects = objects,
            };
        }
    }
}
