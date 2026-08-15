using Ssalddel.Unity.TeamObservation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버가 허용한 같은 팀원의 카메라 상태만 관찰한다.
    /// 관찰 대상의 Transform, 이동 입력 또는 상호작용 Command를 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 팀원관전CameraController : MonoBehaviour
    {
        [SerializeField] private Camera observationCamera = null!;
        [SerializeField] private Camera returnCamera = null!;
        [SerializeField] private 플레이어경관Controller localPlayerController = null!;
        [SerializeField, Min(.5f)] private float followDistance = 4.5f;
        [SerializeField, Min(.5f)] private float followHeight = 2.2f;
        [SerializeField, Min(1f)] private float followSmoothing = 12f;
        [SerializeField] private bool presentationOnly = true;

        private TeamObservationPresentationState? _state;
        private Transform? _targetFirstPersonAnchor;
        private Transform? _targetFollowAnchor;
        private Camera? _previousCamera;
        private bool _localControllerWasEnabled;

        public bool IsObserving => _state?.IsActive == true;
        public bool CanControlObservedTarget => false;
        public string ObservedActorStableId =>
            _state?.CameraTargetActorStableId ?? string.Empty;
        public string TileFocusKey => _state?.TileFocusKey ?? string.Empty;
        public string ViewModeCode => _state?.ViewModeCode ?? string.Empty;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            Camera camera,
            플레이어경관Controller localController,
            Camera cameraToRestore)
        {
            observationCamera = camera;
            localPlayerController = localController;
            returnCamera = cameraToRestore;
            presentationOnly = true;
            if (observationCamera != null) observationCamera.enabled = false;
        }

        public bool ValidateWiring()
            => observationCamera != null
                && localPlayerController != null
                && followDistance > 0f
                && followHeight > 0f
                && followSmoothing > 0f
                && presentationOnly;

        public void BeginObservation(
            TeamObservationPresentationState state,
            Transform targetFirstPersonAnchor,
            Transform targetFollowAnchor)
        {
            if (!ValidateWiring()
                || state == null
                || !state.IsActive
                || state.AcceptsTargetCommands
                || state.MovesLocalActor
                || !state.ShowObservedIndicator
                || !state.PresentationOnly
                || !TeamObservationPresentationMapper.IsSupportedView(
                    state.ViewModeCode)
                || targetFirstPersonAnchor == null
                || targetFollowAnchor == null)
                throw new System.InvalidOperationException(
                    "TeamObservationCameraBoundaryInvalid");

            if (IsObserving) EndObservation();
            _state = state;
            _targetFirstPersonAnchor = targetFirstPersonAnchor;
            _targetFollowAnchor = targetFollowAnchor;
            _localControllerWasEnabled = localPlayerController.enabled;
            localPlayerController.enabled = false;
            _previousCamera = returnCamera != null
                && returnCamera != observationCamera
                ? returnCamera : null;

            foreach (var camera in FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera == observationCamera) continue;
                if (camera.enabled && _previousCamera == null)
                    _previousCamera = camera;
                camera.enabled = false;
            }

            observationCamera.enabled = true;
            TickCamera(1f);
        }

        public void SetViewMode(string viewModeCode)
        {
            if (!IsObserving
                || !TeamObservationPresentationMapper.IsSupportedView(viewModeCode))
                return;
            _state!.ViewModeCode = viewModeCode;
            TickCamera(1f);
        }

        public void SignalLocalDanger()
        {
            if (_state?.ExitOnLocalDanger == true) EndObservation();
        }

        public void TickCamera(float deltaTime)
        {
            if (!IsObserving || observationCamera == null
                || _targetFirstPersonAnchor == null || _targetFollowAnchor == null)
                return;

            if (string.Equals(ViewModeCode,
                    TeamObservationViewModeCodes.FirstPerson,
                    System.StringComparison.Ordinal))
            {
                observationCamera.transform.SetPositionAndRotation(
                    _targetFirstPersonAnchor.position,
                    _targetFirstPersonAnchor.rotation);
                return;
            }

            var focus = _targetFollowAnchor.position + Vector3.up * followHeight;
            var desired = focus - _targetFollowAnchor.forward * followDistance;
            var factor = deltaTime <= 0f
                ? 1f : 1f - Mathf.Exp(-followSmoothing * deltaTime);
            observationCamera.transform.position = Vector3.Lerp(
                observationCamera.transform.position, desired, factor);
            observationCamera.transform.rotation = Quaternion.Slerp(
                observationCamera.transform.rotation,
                Quaternion.LookRotation(focus - observationCamera.transform.position,
                    Vector3.up), factor);
        }

        public void EndObservation()
        {
            var wasObserving = IsObserving;
            if (observationCamera != null) observationCamera.enabled = false;
            if (wasObserving && localPlayerController != null)
                localPlayerController.enabled = _localControllerWasEnabled;
            if (wasObserving && _previousCamera != null)
                _previousCamera.enabled = true;
            _state = null;
            _targetFirstPersonAnchor = null;
            _targetFollowAnchor = null;
            _previousCamera = null;
            _localControllerWasEnabled = false;
        }

        private void Update()
        {
            if (!IsObserving || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                EndObservation();
            else if (Keyboard.current.vKey.wasPressedThisFrame)
                SetViewMode(string.Equals(ViewModeCode,
                        TeamObservationViewModeCodes.FirstPerson,
                        System.StringComparison.Ordinal)
                    ? TeamObservationViewModeCodes.Follow
                    : TeamObservationViewModeCodes.FirstPerson);
        }

        private void LateUpdate() => TickCamera(Time.deltaTime);

        private void OnDisable()
        {
            if (IsObserving) EndObservation();
        }

        private void OnGUI()
        {
            if (!IsObserving) return;
            GUI.color = new Color(0f, 0f, 0f, .62f);
            GUI.DrawTexture(new Rect(14f, 14f, 540f, 42f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 22f, 520f, 28f),
                $"팀원 관전 중 · {ObservedActorStableId} · V 시점 전환 · Esc 복귀 · 조작 불가");
        }
    }
}
