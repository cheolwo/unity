using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.TeamObservation;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버 관전 session과 Unity 표현을 연결한다. 원격 캐릭터 Command나
    /// WorldTick은 만들지 않으며, 서버 거절 시 즉시 로컬 관전을 종료한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 팀원관전Coordinator : MonoBehaviour
    {
        [SerializeField] private 팀원관전CameraController cameraController = null!;
        [SerializeField] private 팀원관전PosePresenter posePresenter = null!;
        [SerializeField] private 팀원관전TileFocusBridge tileFocusBridge = null!;
        [SerializeField] private 팀원관전자표시Presenter observerIndicator = null!;
        [SerializeField] private bool presentationOnly = true;

        private ITeamObservationAuthorityClient? authority;
        private TeamObservationClientCoordinator? clientCoordinator;
        private CancellationTokenSource? lifetime;

        public bool IsInitialized => clientCoordinator != null;
        public bool IsObserving => clientCoordinator?.Current != null;
        public long LastPoseRevision => posePresenter == null
            ? -1 : posePresenter.LastPoseRevision;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            팀원관전CameraController camera,
            팀원관전PosePresenter pose,
            팀원관전TileFocusBridge tileFocus,
            팀원관전자표시Presenter indicator = null)
        {
            cameraController = camera;
            posePresenter = pose;
            tileFocusBridge = tileFocus;
            observerIndicator = indicator;
            presentationOnly = true;
        }

        public void Initialize(ITeamObservationAuthorityClient authorityClient)
        {
            if (authorityClient == null || cameraController == null
                || posePresenter == null || tileFocusBridge == null
                || !presentationOnly)
                throw new InvalidOperationException(
                    "TeamObservationCompositionBoundaryInvalid");
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            authority = authorityClient;
            clientCoordinator = new TeamObservationClientCoordinator(
                authorityClient, new TeamObservationPresentationMapper());
        }

        public async Task StartObservationAsync(
            string sessionStableId,
            string observerActorStableId,
            string targetActorStableId,
            string requestedViewModeCode,
            long expectedTeamRevision,
            string targetTileKey,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (IsObserving)
                throw new InvalidOperationException(
                    "TeamObservationSessionAlreadyActive");
            StopLocalPresentation();
            var state = await clientCoordinator!.StartAsync(sessionStableId,
                new TeamObservationSessionStartApiModel
                {
                    ClientRequestId = Guid.NewGuid(),
                    ObserverActorStableId = observerActorStableId,
                    TargetActorStableId = targetActorStableId,
                    RequestedViewModeCode = requestedViewModeCode,
                    ExpectedTeamRevision = expectedTeamRevision,
                    TargetTileKey = targetTileKey,
                }, LinkedToken(cancellationToken));
            Apply(state, beginCamera: true);
        }

        public async Task RefreshObservationAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            try
            {
                var state = await clientCoordinator!.RefreshAsync(
                    LinkedToken(cancellationToken));
                Apply(state, beginCamera: false);
            }
            catch
            {
                StopLocalPresentation();
                throw;
            }
        }

        public async Task EndObservationAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            try
            {
                await clientCoordinator!.EndAsync(Guid.NewGuid(),
                    LinkedToken(cancellationToken));
            }
            finally
            {
                StopLocalPresentation();
            }
        }

        public async Task SignalLocalDangerAsync(
            CancellationToken cancellationToken = default)
        {
            if (!IsObserving) return;
            await EndObservationAsync(cancellationToken);
        }

        public async Task<TeamObserverIndicatorApiModel> RefreshObserverIndicatorAsync(
            string sessionStableId,
            string localActorStableId,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            var result = await authority!.LoadObserversAsync(sessionStableId,
                localActorStableId, LinkedToken(cancellationToken));
            if (result == null || !result.PresentationOnly
                || !string.Equals(result.SessionStableId, sessionStableId,
                    StringComparison.Ordinal)
                || !string.Equals(result.TargetActorStableId,
                    localActorStableId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "TeamObserverIndicatorTargetMismatch");
            if (observerIndicator != null) observerIndicator.Apply(result);
            return result;
        }

        public void StopLocalPresentation()
        {
            if (cameraController != null) cameraController.EndObservation();
            if (tileFocusBridge != null) tileFocusBridge.End();
            if (posePresenter != null) posePresenter.ResetPresentation();
            clientCoordinator?.ClearLocalPresentation();
        }

        private void Apply(
            TeamObservationFramePresentationState state,
            bool beginCamera)
        {
            posePresenter.Apply(state);
            tileFocusBridge.Begin();
            if (beginCamera)
                cameraController.BeginObservation(state.Camera,
                    posePresenter.FirstPersonAnchor,
                    posePresenter.ObservedActorRoot);
        }

        private CancellationToken LinkedToken(CancellationToken external)
        {
            if (external.CanBeCanceled) return external;
            return lifetime?.Token ?? external;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || authority == null || lifetime == null)
                throw new InvalidOperationException(
                    "TeamObservationCoordinatorNotInitialized");
        }

        private void OnDisable()
        {
            lifetime?.Cancel();
            StopLocalPresentation();
        }

        private void OnDestroy()
        {
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
