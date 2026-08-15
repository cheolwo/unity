using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.TeamObservation;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 팀원관전PipelineTests
    {
        [Test]
        public void 공개Pose는_압축공간과카메라기준점에투영되고_타일초점은복원된다()
        {
            var root = new GameObject("TeamObservationPipelineTestRoot");
            try
            {
                var local = Child(root, "LocalPlayer");
                var tiles = Child(root, "Tiles");
                var label = Child(root, "Status").gameObject.AddComponent<Text>();
                var streaming = root.AddComponent<공간TileStreamingController>();
                streaming.Configure(local, tiles, label,
                    new Vector3(100f, .16f, 200f), 24f);

                var observed = Child(root, "ObservedActor");
                var anchor = Child(observed.gameObject, "FirstPersonAnchor");
                var presenter = root.AddComponent<팀원관전PosePresenter>();
                presenter.Configure(streaming, observed, anchor);
                var bridge = root.AddComponent<팀원관전TileFocusBridge>();
                bridge.Configure(streaming, observed);

                var state = Frame(streaming.TileKeyAtPosition(
                    streaming.CenterTileWorldPosition));
                presenter.Apply(state);
                bridge.Begin();

                Assert.That(observed.position.x, Is.EqualTo(112f).Within(.001f));
                Assert.That(observed.position.y, Is.EqualTo(.16f).Within(.001f));
                Assert.That(observed.position.z, Is.EqualTo(194f).Within(.001f));
                Assert.That(anchor.position.y, Is.EqualTo(1.86f).Within(.001f),
                    "사람 카메라 높이는 공간 거리 압축률을 적용하면 안 됩니다.");
                Assert.That(presenter.ObservedElevationMeters, Is.EqualTo(742d));
                Assert.That(presenter.MovementIntentCode, Is.EqualTo("Walking"));
                Assert.That(streaming.FocusTarget, Is.SameAs(observed));

                bridge.End();

                Assert.That(streaming.FocusTarget, Is.SameAs(local));
                Assert.That(bridge.IsFocusingObservedActor, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PosePresenter는_개인UI가섞인Frame과_과거Revision을거절한다()
        {
            var root = new GameObject("TeamObservationPoseBoundaryTestRoot");
            try
            {
                var local = Child(root, "LocalPlayer");
                var tiles = Child(root, "Tiles");
                var label = Child(root, "Status").gameObject.AddComponent<Text>();
                var streaming = root.AddComponent<공간TileStreamingController>();
                streaming.Configure(local, tiles, label, Vector3.zero, 24f);
                var observed = Child(root, "ObservedActor");
                var anchor = Child(observed.gameObject, "Anchor");
                var presenter = root.AddComponent<팀원관전PosePresenter>();
                presenter.Configure(streaming, observed, anchor);
                var tileKey = streaming.TileKeyAtPosition(Vector3.zero);

                presenter.Apply(Frame(tileKey));
                var stale = Frame(tileKey);
                stale.PoseRevision = 2;
                Assert.Throws<InvalidOperationException>(() => presenter.Apply(stale));
                var privateFrame = Frame(tileKey);
                privateFrame.PoseRevision = 4;
                privateFrame.ContainsInventory = true;
                Assert.Throws<InvalidOperationException>(
                    () => presenter.Apply(privateFrame));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task Coordinator는_ServerSession부터Camera와타일복원까지연결한다()
        {
            var root = new GameObject("TeamObservationCoordinatorTestRoot");
            try
            {
                var local = Child(root, "LocalPlayer");
                local.gameObject.AddComponent<CharacterController>();
                var localController = local.gameObject
                    .AddComponent<플레이어경관Controller>();
                var tiles = Child(root, "Tiles");
                var label = Child(root, "Status").gameObject.AddComponent<Text>();
                var streaming = root.AddComponent<공간TileStreamingController>();
                streaming.Configure(local, tiles, label, Vector3.zero, 24f);
                var observed = Child(root, "ObservedActor");
                var anchor = Child(observed.gameObject, "Anchor");
                var pose = root.AddComponent<팀원관전PosePresenter>();
                pose.Configure(streaming, observed, anchor);
                var focus = root.AddComponent<팀원관전TileFocusBridge>();
                focus.Configure(streaming, observed);
                var returnCamera = Child(root, "ReturnCamera").gameObject
                    .AddComponent<Camera>();
                returnCamera.enabled = true;
                var observationCamera = Child(root, "ObservationCamera")
                    .gameObject.AddComponent<Camera>();
                var camera = root.AddComponent<팀원관전CameraController>();
                camera.Configure(observationCamera, localController, returnCamera);
                var coordinator = root.AddComponent<팀원관전Coordinator>();
                coordinator.Configure(camera, pose, focus);
                var authority = new FakeAuthority(
                    streaming.TileKeyAtPosition(Vector3.zero));
                coordinator.Initialize(authority);

                await coordinator.StartObservationAsync(
                    "session:sim:team-1",
                    "actor:sim:farmer-1",
                    "actor:sim:explorer-1",
                    TeamObservationViewModeCodes.FirstPerson,
                    3,
                    authority.TileKey);

                Assert.That(coordinator.IsObserving, Is.True);
                Assert.That(camera.IsObserving, Is.True);
                Assert.That(streaming.FocusTarget, Is.SameAs(observed));
                Assert.That(localController.enabled, Is.False);

                await coordinator.EndObservationAsync();

                Assert.That(coordinator.IsObserving, Is.False);
                Assert.That(camera.IsObserving, Is.False);
                Assert.That(streaming.FocusTarget, Is.SameAs(local));
                Assert.That(localController.enabled, Is.True);
                Assert.That(authority.EndCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform Child(GameObject parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent.transform);
            return child;
        }

        private static TeamObservationFramePresentationState Frame(string tileKey)
            => new TeamObservationFramePresentationState
            {
                ObservationSessionStableId = "team-observation:test",
                Camera = new TeamObservationPresentationState
                {
                    SessionStableId = "session:sim:team-1",
                    TeamStableId = "team:sim:survivors",
                    LocalControlActorStableId = "actor:sim:farmer-1",
                    CameraTargetActorStableId = "actor:sim:explorer-1",
                    ViewModeCode = TeamObservationViewModeCodes.FirstPerson,
                    TileFocusKey = tileKey,
                    TeamRevision = 3,
                    IsActive = true,
                    AcceptsTargetCommands = false,
                    MovesLocalActor = false,
                    ShowObservedIndicator = true,
                    ExitOnLocalDanger = true,
                    PresentationOnly = true,
                },
                PoseRevision = 3,
                CapturedAtUtc = DateTimeOffset.UtcNow,
                LocalOffsetXMeters = 250d,
                LocalOffsetYMeters = -125d,
                ElevationMeters = 742d,
                CameraHeightMeters = 1.7d,
                YawDegrees = 35d,
                PitchDegrees = -8d,
                MovementIntentCode = "Walking",
                PresentationOnly = true,
            };

        private sealed class FakeAuthority : ITeamObservationAuthorityClient
        {
            public FakeAuthority(string tileKey) => TileKey = tileKey;

            public string TileKey { get; }
            public int EndCount { get; private set; }

            public Task<TeamObservationSessionApiModel> StartAsync(
                string sessionStableId,
                TeamObservationSessionStartApiModel request,
                CancellationToken cancellationToken)
                => Task.FromResult(Session(sessionStableId, request));

            public Task<TeamObservationFrameApiModel> LoadFrameAsync(
                string sessionStableId,
                string observationSessionStableId,
                CancellationToken cancellationToken)
                => Task.FromResult(new TeamObservationFrameApiModel
                {
                    Observation = Session(sessionStableId,
                        new TeamObservationSessionStartApiModel
                        {
                            ObserverActorStableId = "actor:sim:farmer-1",
                            TargetActorStableId = "actor:sim:explorer-1",
                            RequestedViewModeCode =
                                TeamObservationViewModeCodes.FirstPerson,
                            ExpectedTeamRevision = 3,
                        }),
                    TargetPose = new TeamMemberPoseApiModel
                    {
                        SessionStableId = sessionStableId,
                        ActorStableId = "actor:sim:explorer-1",
                        PoseRevision = 1,
                        CapturedAtUtc = DateTimeOffset.UtcNow,
                        TileKey = TileKey,
                        CameraHeightMeters = 1.7d,
                        IsAvailable = true,
                        SimulationOnly = true,
                        PresentationOnly = true,
                    },
                    PresentationOnly = true,
                });

            public Task<TeamObservationSessionApiModel> EndAsync(
                string sessionStableId,
                string observationSessionStableId,
                TeamObservationSessionEndApiModel request,
                CancellationToken cancellationToken)
            {
                EndCount++;
                var session = Session(sessionStableId,
                    new TeamObservationSessionStartApiModel
                    {
                        ObserverActorStableId = request.ObserverActorStableId,
                        TargetActorStableId = "actor:sim:explorer-1",
                        RequestedViewModeCode =
                            TeamObservationViewModeCodes.FirstPerson,
                        ExpectedTeamRevision = 3,
                    });
                session.StateCode = "Ended";
                return Task.FromResult(session);
            }

            public Task<TeamObserverIndicatorApiModel> LoadObserversAsync(
                string sessionStableId,
                string targetActorStableId,
                CancellationToken cancellationToken)
                => Task.FromResult(new TeamObserverIndicatorApiModel
                {
                    SessionStableId = sessionStableId,
                    TargetActorStableId = targetActorStableId,
                    PresentationOnly = true,
                });

            private static TeamObservationSessionApiModel Session(
                string sessionStableId,
                TeamObservationSessionStartApiModel request)
                => new TeamObservationSessionApiModel
                {
                    ObservationSessionStableId = "team-observation:test",
                    SessionStableId = sessionStableId,
                    TeamStableId = "team:sim:survivors",
                    ObserverActorStableId = request.ObserverActorStableId,
                    TargetActorStableId = request.TargetActorStableId,
                    ViewModeCode = request.RequestedViewModeCode,
                    StateCode = "Active",
                    TeamRevision = request.ExpectedTeamRevision,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    ShowObserverIndicator = true,
                    SimulationOnly = true,
                    PresentationOnly = true,
                };
        }
    }
}
