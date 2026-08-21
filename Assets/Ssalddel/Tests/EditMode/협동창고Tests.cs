using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 협동창고Tests
    {
        [Test]
        public async Task 두실제Lot기여와_철거복원은_서버단계를순서대로따른다()
        {
            var authority = new FakeAuthority();
            var coordinator = new 협동창고Coordinator(authority);
            await coordinator.InitializeAsync("simulation-session:coop",
                CancellationToken.None);

            var owner = await coordinator.PreviewNextContributionAsync(
                CancellationToken.None);
            Assert.That(owner.SourceLotStableId, Is.EqualTo(협동창고Codes.OwnerLot));
            await coordinator.ConfirmAsync(CancellationToken.None);
            await coordinator.AdvanceAsync(CancellationToken.None);

            var guest = await coordinator.PreviewNextContributionAsync(
                CancellationToken.None);
            Assert.That(guest.SourceLotStableId, Is.EqualTo(협동창고Codes.GuestLot));
            await coordinator.ConfirmAsync(CancellationToken.None);
            var operational = await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(operational.Project?.StageCode,
                Is.EqualTo(협동창고Codes.Operational));
            Assert.That(operational.Contributions.Length, Is.EqualTo(2));

            await coordinator.PreviewProtectedAsync(협동창고Codes.Demolition,
                CancellationToken.None);
            await coordinator.ConfirmProtectedAsync(CancellationToken.None);
            var removed = await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(removed.Project?.StageCode, Is.EqualTo(협동창고Codes.Removed));
            Assert.That(removed.ProtectionCheckpoints,
                Has.Exactly(1).Matches<협동창고CheckpointData>(value =>
                    !value.HistoricalEffectsDeleted));

            await coordinator.PreviewProtectedAsync(협동창고Codes.Restore,
                CancellationToken.None);
            await coordinator.ConfirmProtectedAsync(CancellationToken.None);
            var restored = await coordinator.AdvanceAsync(CancellationToken.None);
            Assert.That(restored.Project?.StageCode,
                Is.EqualTo(협동창고Codes.Operational));
            Assert.That(restored.RestoreEffects,
                Has.Exactly(1).Matches<협동창고RestoreEffectData>(value =>
                    !value.DeletesHistoricalEffects && !value.DuplicatesResources));
        }

        [Test]
        public void 협동키와_심상강도는_재결속가능한표현상태다()
        {
            var gameObject = new GameObject("협동 접근성 시험");
            try
            {
                var presenter = gameObject.AddComponent<오늘작업계획Presenter>();
                Assert.That(presenter.CoopContributionKey, Is.EqualTo(Key.Digit2));
                Assert.That(presenter.MindIntensity,
                    Is.EqualTo(MindPresentationIntensity.Full));
                presenter.CycleMindPresentationIntensity();
                Assert.That(presenter.MindIntensity,
                    Is.EqualTo(MindPresentationIntensity.Reduced));
                presenter.CycleMindPresentationIntensity();
                Assert.That(presenter.MindIntensity,
                    Is.EqualTo(MindPresentationIntensity.Minimal));
                Assert.That(presenter.HostedOverlayPausesWorld, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class FakeAuthority : I협동창고AuthorityClient
        {
            private readonly Dictionary<string, decimal> remaining = new()
            {
                [협동창고Codes.OwnerLot] = 2m,
                [협동창고Codes.GuestLot] = 2m,
            };
            private readonly Dictionary<string, long> lotRevisions = new()
            {
                [협동창고Codes.OwnerLot] = 1,
                [협동창고Codes.GuestLot] = 1,
            };
            private readonly List<협동창고ContributionData> contributions = new();
            private long revision = 5;
            private int tick;
            private decimal contributed;
            private string stage = "Planned";
            private 협동창고PreviewData? pending;
            private bool destructiveCheckpoint;
            private bool restoreEffect;

            public Task<협동창고StateData> RefreshAsync(string sessionStableId,
                CancellationToken cancellationToken) => Task.FromResult(State());

            public Task<협동창고PreviewData> PreviewContributionAsync(
                string sessionStableId, long expectedRevision, string playerStableId,
                string lotStableId, long expectedLotRevision,
                CancellationToken cancellationToken)
            {
                var accepted = Math.Min(remaining[lotStableId], 4m - contributed);
                return Task.FromResult(new 협동창고PreviewData
                {
                    BaseRevision = expectedRevision,
                    ActionCode = 협동창고Codes.Contribution,
                    PlayerStableId = playerStableId,
                    SourceLotStableId = lotStableId,
                    SourceLotRevision = expectedLotRevision,
                    AcceptedQuantity = accepted,
                    CurrentStageCode = stage,
                    ProjectedStageCode = contributed + accepted >= 4m
                        ? 협동창고Codes.Operational : "Frame",
                    DurationTicks = 1,
                    CanConfirm = accepted > 0m,
                });
            }

            public Task<협동창고StateData> ConfirmContributionAsync(
                string sessionStableId, 협동창고PreviewData preview,
                CancellationToken cancellationToken)
            {
                pending = preview;
                revision++;
                return Task.FromResult(State());
            }

            public Task<협동창고PreviewData> PreviewProtectedActionAsync(
                string sessionStableId, long expectedRevision, string actionCode,
                CancellationToken cancellationToken)
            {
                var allowed = actionCode == 협동창고Codes.Demolition
                    ? stage == 협동창고Codes.Operational
                    : stage == 협동창고Codes.Removed;
                return Task.FromResult(new 협동창고PreviewData
                {
                    BaseRevision = expectedRevision,
                    ActionCode = actionCode,
                    PlayerStableId = Hosted비콘Codes.Owner,
                    CurrentStageCode = stage,
                    ProjectedStageCode = actionCode == 협동창고Codes.Restore
                        ? 협동창고Codes.Operational : 협동창고Codes.Removed,
                    DurationTicks = 1,
                    CanConfirm = allowed,
                    BlockingReasonCodes = allowed ? Array.Empty<string>()
                        : new[] { "SimulationWorldProtectionBlocked" },
                });
            }

            public Task<협동창고StateData> ConfirmProtectedActionAsync(
                string sessionStableId, 협동창고PreviewData preview,
                CancellationToken cancellationToken)
            {
                pending = preview;
                revision++;
                if (preview.ActionCode == 협동창고Codes.Demolition)
                    destructiveCheckpoint = true;
                return Task.FromResult(State());
            }

            public Task<협동창고StateData> AdvanceAsync(string sessionStableId,
                long expectedRevision, int ticks, CancellationToken cancellationToken)
            {
                if (pending == null) throw new InvalidOperationException();
                tick += ticks;
                revision++;
                if (pending.ActionCode == 협동창고Codes.Contribution)
                {
                    remaining[pending.SourceLotStableId] -= pending.AcceptedQuantity;
                    lotRevisions[pending.SourceLotStableId]++;
                    contributed += pending.AcceptedQuantity;
                    stage = contributed >= 4m ? 협동창고Codes.Operational : "Frame";
                    contributions.Add(new 협동창고ContributionData
                    {
                        PlayerStableId = pending.PlayerStableId,
                        SourceLotStableId = pending.SourceLotStableId,
                        MaterialQuantity = pending.AcceptedQuantity,
                        EffectiveWork = pending.AcceptedQuantity,
                        StateCode = "Consumed",
                    });
                }
                else if (pending.ActionCode == 협동창고Codes.Demolition)
                    stage = 협동창고Codes.Removed;
                else
                {
                    stage = 협동창고Codes.Operational;
                    restoreEffect = true;
                }
                pending = null;
                return Task.FromResult(State());
            }

            private 협동창고StateData State() => new()
            {
                WorldRevision = revision,
                WorldTick = tick,
                Projects = contributed == 0m ? Array.Empty<협동창고ProjectData>()
                    : new[]
                    {
                        new 협동창고ProjectData
                        {
                            ProjectStableId = 협동창고Codes.Project,
                            StageCode = stage,
                            RequiredMaterialQuantity = 4m,
                            ContributedMaterialQuantity = contributed,
                            ProgressValue = contributed / 4m,
                            Revision = revision,
                        },
                    },
                Contributions = contributions.ToArray(),
                SourceLots = remaining.Select(value => new 협동창고LotData
                    {
                        LotStableId = value.Key,
                        Revision = lotRevisions[value.Key],
                        RemainingQuantity = value.Value,
                        UnitCode = "EA",
                    }).ToArray(),
                ProtectionCheckpoints = destructiveCheckpoint
                    ? new[]
                    {
                        new 협동창고CheckpointData
                        {
                            CheckpointStableId = "checkpoint:test:demolition",
                            CheckpointKindCode = "DestructiveActionCheckpoint",
                            SpatialStateHashSha256 = new string('a', 64),
                            HistoricalEffectsDeleted = false,
                        },
                    }
                    : Array.Empty<협동창고CheckpointData>(),
                RestoreEffects = restoreEffect
                    ? new[]
                    {
                        new 협동창고RestoreEffectData
                        {
                            EffectStableId = "effect:test:restore",
                            DeletesHistoricalEffects = false,
                            DuplicatesResources = false,
                        },
                    }
                    : Array.Empty<협동창고RestoreEffectData>(),
                UsesCompensatingEffects = true,
                MutatesStaticHDefinitions = false,
                StateHashSha256 = new string('b', 64),
            };
        }
    }
}
