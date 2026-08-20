using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.PlayerActivities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 플레이어시점Mode
    {
        Strategy,
        FirstPerson,
        ThirdPerson,
    }

    public static class 카메라시점전환Math
    {
        public static float EaseInOut(float progress)
        {
            var value = Mathf.Clamp01(progress);
            return value * value * (3f - 2f * value);
        }

        public static Vector3 EvaluateCurvedPosition(
            Vector3 start, Vector3 end, float progress, float arcHeight)
        {
            var control = CreateSharedControlPoint(start, end, arcHeight);
            return EvaluateQuadraticPosition(start, control, end, progress);
        }

        public static Vector3 CreateSharedControlPoint(
            Vector3 firstEndpoint, Vector3 secondEndpoint, float arcHeight)
            => Vector3.Lerp(firstEndpoint, secondEndpoint, .5f)
                + Vector3.up * Mathf.Max(0f, arcHeight);

        public static Vector3 EvaluateQuadraticPosition(
            Vector3 start, Vector3 control, Vector3 end, float progress)
        {
            var value = Mathf.Clamp01(progress);
            var inverse = 1f - value;
            return inverse * inverse * start
                + 2f * inverse * value * control
                + value * value * end;
        }
    }

    /// <summary>
    /// Synty 플레이어의 1인칭 직접 이동과 RTS 전술 선택 이동을 담당하는 표현 전용 입력입니다.
    /// 서버 상태, Simulation Tick과 업무 개정 번호를 변경하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class 플레이어경관Controller : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Profile profile = new();
        [SerializeField] private Transform firstPersonPivot = null!;
        [SerializeField] private Camera firstPersonCamera = null!;
        [SerializeField] private Transform thirdPersonPivot = null!;
        [SerializeField] private Camera thirdPersonCamera = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private 공용AnimationAdapter animationAdapter = null!;
        [SerializeField] private GameObject selectionHighlight = null!;
        [SerializeField] private GameObject destinationMarker = null!;
        [SerializeField] private 공간안전이동Gate movementGate = null!;
        [SerializeField] private 농장경영시점Controller farmManagement = null!;
        [SerializeField] private 전투시점Controller combat = null!;
        [SerializeField, Min(.1f)] private float viewTransitionDuration = .9f;
        [SerializeField, Min(0f)] private float viewTransitionArcHeight = 1.8f;
        [SerializeField] private bool presentationOnly = true;

        private CharacterController _characterController = null!;
        private Camera? _previousCamera;
        private Renderer[] _visualRenderers = System.Array.Empty<Renderer>();
        private bool[] _visualRendererStates = System.Array.Empty<bool>();
        private 플레이어시점Mode _currentMode = 플레이어시점Mode.Strategy;
        private bool _isSelected;
        private bool _hasDestination;
        private Vector3 _destination;
        private Vector3 _tacticalFocus;
        private float _tacticalDistance;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;
        private float _walkPhase;
        private Vector3 _visualBasePosition;
        private readonly PlayerActivityViewPolicyCatalog _activityViewPolicies =
            PlayerActivityViewPolicyCatalog.CreateDefault();
        private PlayerActivityViewDecision _currentViewDecision = null!;
        private string _currentActivityCode = PlayerActivityCodes.WorldOverview;
        private Camera? _viewTransitionCamera;
        private Camera? _viewTransitionTarget;
        private Vector3 _viewTransitionStartPosition;
        private Quaternion _viewTransitionStartRotation;
        private float _viewTransitionStartFieldOfView;
        private Vector3 _viewTransitionEndPosition;
        private Quaternion _viewTransitionEndRotation;
        private float _viewTransitionEndFieldOfView;
        private Vector3 _viewTransitionControlPoint;
        private float _viewTransitionElapsed;
        private bool _viewTransitionTargetShowsVisual;
        private bool _viewTransitionTargetEnablesFarmManagement;
        private bool _viewTransitionVisualSwitched;

        public 플레이어경관Profile Profile => profile;
        public Camera PlayerCamera => thirdPersonCamera;
        public Camera FirstPersonCamera => firstPersonCamera;
        public 플레이어시점Mode CurrentMode => _currentMode;
        public bool PresentationOnly => presentationOnly;
        public bool IsPlayerMode => _currentMode != 플레이어시점Mode.Strategy;
        public bool IsSelected => _isSelected;
        public bool HasDestination => _hasDestination;
        public Vector3 Destination => _destination;
        public Vector3 TacticalFocus => _tacticalFocus;
        public float TacticalDistance => _tacticalDistance;
        public string CurrentMovementIntent => animationAdapter == null
            ? string.Empty : animationAdapter.CurrentIntentCode;
        public bool MovementBlockedByStreaming { get; private set; }
        public bool HasMovementSafetyGate => movementGate != null;
        public bool UsesStreamingTraversalCoverage
            => movementGate != null && movementGate.UsesStreamingCoverage;
        public string CurrentActivityCode => _currentActivityCode;
        public PlayerActivityViewDecision CurrentViewDecision
            => _currentViewDecision ?? _activityViewPolicies.Resolve(
                PlayerActivityCodes.WorldOverview);
        public 농장경영시점Controller FarmManagement => farmManagement;
        public 전투시점Controller Combat => combat;
        public bool IsCameraTransitioning { get; private set; }
        public float CameraTransitionProgress => !IsCameraTransitioning
            ? 1f
            : Mathf.Clamp01(_viewTransitionElapsed / Mathf.Max(.1f, viewTransitionDuration));
        public float ViewTransitionDuration => viewTransitionDuration;
        public float ViewTransitionArcHeight => viewTransitionArcHeight;

        public void Configure(
            플레이어경관Profile value,
            Transform firstPivot,
            Camera firstCamera,
            Transform thirdPivot,
            Camera thirdCamera,
            Transform visual,
            공용AnimationAdapter adapter,
            GameObject highlight,
            GameObject marker)
        {
            profile = value;
            firstPersonPivot = firstPivot;
            firstPersonCamera = firstCamera;
            thirdPersonPivot = thirdPivot;
            thirdPersonCamera = thirdCamera;
            visualRoot = visual;
            animationAdapter = adapter;
            selectionHighlight = highlight;
            destinationMarker = marker;
            presentationOnly = value?.PresentationOnly == true;
            _characterController = GetComponent<CharacterController>();
            _characterController.height = profile.CapsuleHeight;
            _characterController.radius = profile.CapsuleRadius;
            _characterController.center = Vector3.up * (profile.CapsuleHeight * .5f);
            _characterController.stepOffset = .28f;
            _characterController.slopeLimit = 42f;
            _visualBasePosition = visualRoot.localPosition;
            CacheVisualRendererStates();
            _yaw = transform.eulerAngles.y;
            _pitch = profile.InitialPitch;
            _tacticalFocus = transform.position;
            _tacticalDistance = profile.CameraDistance;
            ApplyLookRotation();
            ApplySelectionState(false);
            destinationMarker.SetActive(false);
        }

        public bool ValidateWiring()
            => profile != null
                && profile.Validate()
                && firstPersonPivot != null
                && firstPersonCamera != null
                && firstPersonCamera.transform.IsChildOf(firstPersonPivot)
                && thirdPersonPivot != null
                && thirdPersonCamera != null
                && thirdPersonCamera.transform.IsChildOf(thirdPersonPivot)
                && visualRoot != null
                && visualRoot.IsChildOf(transform)
                && animationAdapter != null
                && animationAdapter.transform == transform
                && animationAdapter.ValidateWiring()
                && selectionHighlight != null
                && selectionHighlight.transform.IsChildOf(transform)
                && destinationMarker != null
                && GetComponent<CharacterController>() != null
                && presentationOnly;

        public void RebindVisual(
            Transform visual,
            공용AnimationAdapter adapter)
        {
            if (visual == null || !visual.IsChildOf(transform)
                || adapter == null || adapter.transform != transform
                || !adapter.ValidateWiring())
                throw new System.ArgumentException("PlayerLandscapeVisualRebindInvalid");
            visualRoot = visual;
            animationAdapter = adapter;
            _visualRenderers = System.Array.Empty<Renderer>();
            _visualRendererStates = System.Array.Empty<bool>();
            _visualBasePosition = visualRoot.localPosition;
            CacheVisualRendererStates();
            SetVisualVisible(_currentMode != 플레이어시점Mode.FirstPerson);
        }

        public void ConfigureMovementGate(공간안전이동Gate value)
            => movementGate = value;

        /// <summary>
        /// 저장 Scene의 플레이어 입력·카메라 배선은 유지하면서 보행 가능한 월드 범위만 갱신합니다.
        /// 서버 위치, WorldTick과 업무 개정 번호에는 관여하지 않습니다.
        /// </summary>
        public void ConfigureTraversalProfile(플레이어경관Profile value)
        {
            if (value == null || !value.Validate())
                throw new System.ArgumentException("PlayerTraversalProfileInvalid");
            profile = value;
            _characterController ??= GetComponent<CharacterController>();
            _characterController.height = profile.CapsuleHeight;
            _characterController.radius = profile.CapsuleRadius;
            _characterController.center = Vector3.up * (profile.CapsuleHeight * .5f);
            _characterController.stepOffset = .28f;
            _characterController.slopeLimit = 42f;
            _tacticalDistance = Mathf.Clamp(
                _tacticalDistance <= 0f ? profile.CameraDistance : _tacticalDistance,
                profile.TacticalMinimumDistance,
                profile.TacticalMaximumDistance);
            ClampToTraversalBounds();
            _tacticalFocus = ClampTacticalFocus(_tacticalFocus);
        }

        /// <summary>
        /// 저장 Scene의 수직 단위가 시작 위치와 바라보는 방향을 함께 고정할 때 사용한다.
        /// 서버 위치나 시뮬레이션 상태는 변경하지 않는 표현 전용 초기 자세다.
        /// </summary>
        public void SetPresentationStartPose(Vector3 position, float yaw)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _yaw = yaw;
            _pitch = profile.InitialPitch;
            _tacticalFocus = position;
            ApplyLookRotation();
        }

        public void ConfigureFarmManagement(농장경영시점Controller value)
        {
            farmManagement = value;
            if (farmManagement == null || !farmManagement.ValidateWiring())
                throw new System.ArgumentException("FarmManagementWiringInvalid");
            farmManagement.SetActive(
                _currentMode == 플레이어시점Mode.ThirdPerson
                && string.Equals(_currentActivityCode,
                    PlayerActivityCodes.FarmManagement,
                    System.StringComparison.Ordinal));
        }

        public void ConfigureCombat(전투시점Controller value)
        {
            combat = value;
            if (combat == null || !combat.ValidateWiring())
                throw new System.ArgumentException("FarmCombatViewWiringInvalid");
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            farmManagement ??= FindFirstObjectByType<농장경영시점Controller>(
                FindObjectsInactive.Include);
            combat ??= FindFirstObjectByType<전투시점Controller>(
                FindObjectsInactive.Include);
            if (profile != null && profile.Validate() && thirdPersonPivot != null)
            {
                _yaw = thirdPersonPivot.eulerAngles.y;
                _pitch = NormalizePitch(thirdPersonPivot.eulerAngles.x);
                _visualBasePosition = visualRoot == null
                    ? Vector3.zero : visualRoot.localPosition;
                _tacticalFocus = transform.position;
                _tacticalDistance = profile.CameraDistance;
                CacheVisualRendererStates();
            }
        }

        private void Update()
        {
            if (!presentationOnly) return;
            if (IsCameraTransitioning) TickCameraTransition(Time.unscaledDeltaTime);
            if (Keyboard.current == null) return;
            if (combat != null)
            {
                var combatInputHandled = combat.TryHandleCombatInput();
                if (combat.LocksPlayerMovement || combatInputHandled) return;
            }
            if (combat != null
                && combat.TryHandleTacticalViewInput(Keyboard.current))
                return;
            var keyboard = Keyboard.current;
            if (keyboard.f1Key.wasPressedThisFrame) ExitPlayerMode();
            if (keyboard.f2Key.wasPressedThisFrame) EnterFirstPersonMode();
            if (keyboard.f3Key.wasPressedThisFrame) EnterThirdPersonMode();
            if (!IsPlayerMode || IsCameraTransitioning) return;

            if (keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
            if (_currentMode == 플레이어시점Mode.FirstPerson)
                UpdateFirstPersonInput(keyboard);
            else
                UpdateThirdPersonInput(keyboard);
        }

        private void UpdateFirstPersonInput(Keyboard keyboard)
        {
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            var movement = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            if (movement.sqrMagnitude > 1f) movement.Normalize();

            var lookDelta = Cursor.lockState == CursorLockMode.Locked
                ? Mouse.current?.delta.ReadValue() ?? Vector2.zero
                : Vector2.zero;
            TickPresentation(
                movement,
                lookDelta,
                keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed,
                Time.deltaTime);
        }

        private void UpdateThirdPersonInput(Keyboard keyboard)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var farmInputHandled = farmManagement != null
                && farmManagement.TryHandlePointerInput(mouse, keyboard);
            if (!farmInputHandled)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    TrySelectPlayer(mouse.position.ReadValue());
                if (mouse.rightButton.wasPressedThisFrame && _isSelected)
                    TrySetDestination(mouse.position.ReadValue());
            }
            if (mouse.middleButton.isPressed)
                TickLook(mouse.delta.ReadValue());
            var panInput = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) panInput.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) panInput.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) panInput.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) panInput.x -= 1f;
            if (panInput.sqrMagnitude > 1f) panInput.Normalize();
            if (keyboard.fKey.wasPressedThisFrame && _isSelected)
                FocusTacticalCameraOnPlayer();
            TickTacticalCamera(
                panInput, mouse.scroll.ReadValue().y, Time.deltaTime);
            TickThirdPersonMovement(Time.deltaTime);
        }

        public void TickTacticalCamera(
            Vector2 panInput, float scrollInput, float deltaTime)
        {
            if (!ValidateWiring() || deltaTime <= 0f) return;
            var forward = Vector3.ProjectOnPlane(thirdPersonPivot.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(thirdPersonPivot.right, Vector3.up).normalized;
            var direction = forward * panInput.y + right * panInput.x;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            _tacticalFocus += direction * (profile.TacticalPanSpeed * deltaTime);
            _tacticalFocus = ClampTacticalFocus(_tacticalFocus);
            _tacticalDistance = Mathf.Clamp(
                _tacticalDistance - scrollInput * .01f * profile.TacticalZoomSpeed,
                profile.TacticalMinimumDistance,
                profile.TacticalMaximumDistance);
        }

        public void FocusTacticalCameraOnPlayer()
        {
            _tacticalFocus = transform.position;
            _tacticalFocus = ClampTacticalFocus(_tacticalFocus);
        }

        public void TickPresentation(
            Vector2 movementInput, Vector2 lookDelta, bool run, float deltaTime)
        {
            if (!ValidateWiring() || deltaTime <= 0f) return;
            TickLook(lookDelta);
            var forward = Vector3.ProjectOnPlane(firstPersonPivot.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(firstPersonPivot.right, Vector3.up).normalized;
            var direction = forward * movementInput.y + right * movementInput.x;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            MoveCharacter(direction, run, deltaTime);
        }

        public void TickThirdPersonMovement(float deltaTime)
        {
            if (!ValidateWiring() || deltaTime <= 0f) return;
            if (!_hasDestination)
            {
                MoveCharacter(Vector3.zero, false, deltaTime);
                return;
            }

            var offset = _destination - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= profile.ClickMoveStopDistance)
            {
                ClearDestination();
                MoveCharacter(Vector3.zero, false, deltaTime);
                return;
            }

            MoveCharacter(offset.normalized, false, deltaTime);
            var remaining = _destination - transform.position;
            remaining.y = 0f;
            if (remaining.magnitude <= profile.ClickMoveStopDistance) ClearDestination();
        }

        public void SetThirdPersonSelection(bool selected) => ApplySelectionState(selected);

        public void SetThirdPersonDestination(Vector3 destination)
        {
            _destination = UsesStreamingTraversalCoverage
                ? destination
                : new Vector3(
                    Mathf.Clamp(destination.x, profile.MinimumX, profile.MaximumX),
                    destination.y,
                    Mathf.Clamp(destination.z, profile.MinimumZ, profile.MaximumZ));
            _hasDestination = true;
            destinationMarker.transform.position = _destination + Vector3.up * .08f;
            destinationMarker.SetActive(true);
        }

        private void MoveCharacter(Vector3 direction, bool run, float deltaTime)
        {
            var isMoving = direction.sqrMagnitude > .0001f;
            var speed = profile.WalkSpeed * (run ? profile.RunMultiplier : 1f);
            if (_characterController.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -1.5f;
            else
                _verticalVelocity += Physics.gravity.y * deltaTime;
            var horizontalMovement = direction * (speed * deltaTime);
            MovementBlockedByStreaming = movementGate != null
                && horizontalMovement.sqrMagnitude > .000001f
                && !movementGate.CanEnter(transform.position + horizontalMovement);
            if (MovementBlockedByStreaming)
            {
                horizontalMovement = Vector3.zero;
                isMoving = false;
            }
            _characterController.Move(
                horizontalMovement + Vector3.up * (_verticalVelocity * deltaTime));
            ClampToTraversalBounds();

            if (isMoving)
            {
                visualRoot.rotation = Quaternion.Slerp(
                    visualRoot.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    12f * deltaTime);
                _walkPhase += deltaTime * (run ? 11f : 8f);
                visualRoot.localPosition = _visualBasePosition
                    + Vector3.up * (Mathf.Abs(Mathf.Sin(_walkPhase)) * .025f);
                animationAdapter.ApplyLocomotion(true, run);
            }
            else
            {
                visualRoot.localPosition = Vector3.Lerp(
                    visualRoot.localPosition, _visualBasePosition, 10f * deltaTime);
                animationAdapter.ApplyLocomotion(false, false);
            }
        }

        private void ClampToTraversalBounds()
        {
            if (UsesStreamingTraversalCoverage) return;
            var position = transform.position;
            position.x = Mathf.Clamp(position.x, profile.MinimumX, profile.MaximumX);
            position.z = Mathf.Clamp(position.z, profile.MinimumZ, profile.MaximumZ);
            transform.position = position;
        }

        private Vector3 ClampTacticalFocus(Vector3 value)
        {
            if (movementGate != null
                && movementGate.TryGetTrackedWorldBounds(out var trackedBounds))
            {
                value.x = Mathf.Clamp(value.x, trackedBounds.min.x, trackedBounds.max.x);
                value.z = Mathf.Clamp(value.z, trackedBounds.min.z, trackedBounds.max.z);
                return value;
            }

            value.x = Mathf.Clamp(value.x, profile.MinimumX, profile.MaximumX);
            value.z = Mathf.Clamp(value.z, profile.MinimumZ, profile.MaximumZ);
            return value;
        }

        private void LateUpdate()
        {
            if (_currentMode != 플레이어시점Mode.ThirdPerson
                || thirdPersonPivot == null || thirdPersonCamera == null) return;
            ApplyThirdPersonCameraPose();
        }

        private void ApplyThirdPersonCameraPose()
        {
            thirdPersonPivot.position = _tacticalFocus + Vector3.up * profile.CameraHeight;
            var origin = thirdPersonPivot.position;
            var direction = -thirdPersonPivot.forward;
            var distance = _tacticalDistance;
            if (Physics.SphereCast(
                    origin, .18f, direction, out var hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(.55f, hit.distance - .16f);
            thirdPersonCamera.transform.SetPositionAndRotation(
                origin + direction * distance,
                thirdPersonPivot.rotation);
        }

        public void EnterPlayerMode() => EnterFarmManagementMode();

        public void EnterFarmManagementMode()
            => ApplyActivityViewDecision(_activityViewPolicies.Resolve(
                PlayerActivityCodes.FarmManagement));

        public void EnterFarmManagementFirstPersonMode()
            => ApplyActivityViewDecision(_activityViewPolicies.Resolve(
                PlayerActivityCodes.FarmManagement,
                PlayerActivityViewModeCodes.FirstPerson));

        public void EnterExplorationMode()
            => ApplyActivityViewDecision(_activityViewPolicies.Resolve(
                PlayerActivityCodes.Exploration));

        public void EnterCombatMode(string perspectiveCode)
        {
            var viewMode = string.Equals(perspectiveCode,
                Ssalddel.Unity.Survival.FarmCombatPresentationCodes
                    .FirstPersonPrecision,
                System.StringComparison.Ordinal)
                    ? PlayerActivityViewModeCodes.FirstPerson
                    : string.Equals(perspectiveCode,
                        Ssalddel.Unity.Survival.FarmCombatPresentationCodes
                            .ThirdPersonAwareness,
                        System.StringComparison.Ordinal)
                            ? PlayerActivityViewModeCodes.TacticalThirdPerson
                            : throw new System.ArgumentException(
                                "FarmCombatPerspectiveInvalid",
                                nameof(perspectiveCode));
            ApplyActivityViewDecision(_activityViewPolicies.Resolve(
                PlayerActivityCodes.Combat, viewMode));
        }

        public void ApplyCombatAnimation(string intentCode)
        {
            if (animationAdapter == null)
                throw new System.InvalidOperationException(
                    "FarmCombatAnimationAdapterMissing");
            animationAdapter.ApplyIntent(intentCode);
        }

        public void EnterFirstPersonMode()
        {
            var activityCode = string.Equals(_currentActivityCode,
                PlayerActivityCodes.FarmManagement,
                System.StringComparison.Ordinal)
                ? PlayerActivityCodes.FarmManagement
                : PlayerActivityCodes.Exploration;
            ApplyActivityViewDecision(_activityViewPolicies.Resolve(
                activityCode, PlayerActivityViewModeCodes.FirstPerson));
        }

        private void EnterFirstPersonModeCore()
        {
            if (!ValidateWiring()) return;
            var sourcePose = CaptureActiveCameraPose();
            _currentMode = 플레이어시점Mode.FirstPerson;
            _yaw = visualRoot.eulerAngles.y;
            _pitch = profile.InitialPitch;
            ApplyLookRotation();
            ApplySelectionState(false);
            ClearDestination();
            if (farmManagement != null) farmManagement.SetActive(false);
            BeginCameraTransition(firstPersonCamera, false, false, sourcePose);
        }

        public void EnterThirdPersonMode()
        {
            var activityCode = string.Equals(_currentActivityCode,
                PlayerActivityCodes.Exploration,
                System.StringComparison.Ordinal)
                ? PlayerActivityCodes.Exploration
                : PlayerActivityCodes.FarmManagement;
            ApplyActivityViewDecision(_activityViewPolicies.Resolve(
                activityCode, PlayerActivityViewModeCodes.TacticalThirdPerson));
        }

        private void EnterThirdPersonModeCore()
        {
            if (!ValidateWiring()) return;
            var sourcePose = CaptureActiveCameraPose();
            _currentMode = 플레이어시점Mode.ThirdPerson;
            _yaw = profile.TacticalYaw;
            _pitch = profile.TacticalPitch;
            _tacticalDistance = profile.CameraDistance;
            FocusTacticalCameraOnPlayer();
            ApplyLookRotation();
            ApplyThirdPersonCameraPose();
            ReleaseCursor();
            BeginCameraTransition(
                thirdPersonCamera,
                true,
                string.Equals(_currentActivityCode,
                    PlayerActivityCodes.FarmManagement,
                    System.StringComparison.Ordinal),
                sourcePose);
        }

        public void ExitPlayerMode()
        {
            CancelCameraTransition();
            if (firstPersonCamera != null) firstPersonCamera.enabled = false;
            if (thirdPersonCamera != null) thirdPersonCamera.enabled = false;
            if (_previousCamera != null) _previousCamera.enabled = true;
            _previousCamera = null;
            _currentMode = 플레이어시점Mode.Strategy;
            _currentActivityCode = PlayerActivityCodes.WorldOverview;
            _currentViewDecision = _activityViewPolicies.Resolve(
                PlayerActivityCodes.WorldOverview);
            if (farmManagement != null) farmManagement.SetActive(false);
            ApplySelectionState(false);
            ClearDestination();
            SetVisualVisible(true);
            ReleaseCursor();
        }

        private void ApplyActivityViewDecision(PlayerActivityViewDecision decision)
        {
            if (decision == null || !decision.PresentationOnly
                || decision.ChangesWorldState)
                throw new System.ArgumentException("PlayerActivityViewDecisionInvalid");
            _currentActivityCode = decision.ActivityCode;
            _currentViewDecision = decision;
            if (string.Equals(decision.ViewModeCode,
                    PlayerActivityViewModeCodes.FirstPerson,
                    System.StringComparison.Ordinal))
                EnterFirstPersonModeCore();
            else if (string.Equals(decision.ViewModeCode,
                         PlayerActivityViewModeCodes.TacticalThirdPerson,
                         System.StringComparison.Ordinal))
                EnterThirdPersonModeCore();
            else
                ExitPlayerMode();
        }

        public void TickCameraTransition(float deltaTime)
        {
            if (!IsCameraTransitioning || _viewTransitionCamera == null
                || _viewTransitionTarget == null) return;
            _viewTransitionElapsed += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(
                _viewTransitionElapsed / Mathf.Max(.1f, viewTransitionDuration));
            var eased = 카메라시점전환Math.EaseInOut(progress);
            _viewTransitionCamera.transform.SetPositionAndRotation(
                EvaluateTransitionPosition(eased),
                Quaternion.Slerp(
                    _viewTransitionStartRotation,
                    _viewTransitionEndRotation,
                    eased));
            _viewTransitionCamera.fieldOfView = Mathf.Lerp(
                _viewTransitionStartFieldOfView,
                _viewTransitionEndFieldOfView,
                eased);

            var visualSwitchProgress = _viewTransitionTargetShowsVisual ? .32f : .72f;
            if (!_viewTransitionVisualSwitched && progress >= visualSwitchProgress)
            {
                SetVisualVisible(_viewTransitionTargetShowsVisual);
                _viewTransitionVisualSwitched = true;
            }
            if (progress >= 1f) CompleteCameraTransition();
        }

        private Vector3 EvaluateTransitionPosition(float easedProgress)
            => 카메라시점전환Math.EvaluateQuadraticPosition(
                _viewTransitionStartPosition,
                _viewTransitionControlPoint,
                _viewTransitionEndPosition,
                easedProgress);

        private void BeginCameraTransition(
            Camera target,
            bool showVisual,
            bool enableFarmManagement,
            CameraTransitionPose? sourcePose = null)
        {
            var source = FindActiveCamera();
            if (!UnityEngine.Application.isPlaying || source == null || source == target)
            {
                ActivatePlayerCameraImmediately(target);
                SetVisualVisible(showVisual);
                if (farmManagement != null)
                    farmManagement.SetActive(enableFarmManagement);
                return;
            }

            if (source != firstPersonCamera && source != thirdPersonCamera
                && source != _viewTransitionCamera && _previousCamera == null)
                _previousCamera = source;

            var transition = EnsureViewTransitionCamera();
            if (source != transition) transition.CopyFrom(source);
            var startPose = sourcePose ?? CameraTransitionPose.Capture(source);
            var endPose = CameraTransitionPose.Capture(target);
            transition.transform.SetPositionAndRotation(
                startPose.Position, startPose.Rotation);
            transition.fieldOfView = startPose.FieldOfView;
            _viewTransitionStartPosition = startPose.Position;
            _viewTransitionStartRotation = startPose.Rotation;
            _viewTransitionStartFieldOfView = startPose.FieldOfView;
            _viewTransitionEndPosition = endPose.Position;
            _viewTransitionEndRotation = endPose.Rotation;
            _viewTransitionEndFieldOfView = endPose.FieldOfView;
            _viewTransitionControlPoint =
                카메라시점전환Math.CreateSharedControlPoint(
                    _viewTransitionStartPosition,
                    _viewTransitionEndPosition,
                    viewTransitionArcHeight);
            _viewTransitionTarget = target;
            _viewTransitionElapsed = 0f;
            _viewTransitionTargetShowsVisual = showVisual;
            _viewTransitionTargetEnablesFarmManagement = enableFarmManagement;
            _viewTransitionVisualSwitched = false;
            IsCameraTransitioning = true;
            if (farmManagement != null) farmManagement.SetActive(false);
            DisableAllCamerasExcept(transition);
            transition.enabled = true;
        }

        private CameraTransitionPose? CaptureActiveCameraPose()
        {
            var activeCamera = FindActiveCamera();
            return activeCamera == null
                ? null
                : CameraTransitionPose.Capture(activeCamera);
        }

        private readonly struct CameraTransitionPose
        {
            public CameraTransitionPose(
                Vector3 position, Quaternion rotation, float fieldOfView)
            {
                Position = position;
                Rotation = rotation;
                FieldOfView = fieldOfView;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public float FieldOfView { get; }

            public static CameraTransitionPose Capture(Camera camera)
                => new(
                    camera.transform.position,
                    camera.transform.rotation,
                    camera.fieldOfView);
        }

        private Camera EnsureViewTransitionCamera()
        {
            if (_viewTransitionCamera != null) return _viewTransitionCamera;
            var root = new GameObject("시점전환Camera");
            root.transform.SetParent(transform, false);
            _viewTransitionCamera = root.AddComponent<Camera>();
            _viewTransitionCamera.enabled = false;
            return _viewTransitionCamera;
        }

        private Camera? FindActiveCamera()
        {
            if (_viewTransitionCamera != null && _viewTransitionCamera.enabled)
                return _viewTransitionCamera;
            if (firstPersonCamera != null && firstPersonCamera.enabled)
                return firstPersonCamera;
            if (thirdPersonCamera != null && thirdPersonCamera.enabled)
                return thirdPersonCamera;
            var main = Camera.main;
            if (main != null && main.enabled) return main;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (camera != null && camera.enabled) return camera;
            return null;
        }

        private void CompleteCameraTransition()
        {
            if (_viewTransitionTarget == null) return;
            var target = _viewTransitionTarget;
            IsCameraTransitioning = false;
            if (_viewTransitionCamera != null) _viewTransitionCamera.enabled = false;
            DisableAllCamerasExcept(target);
            target.enabled = true;
            SetVisualVisible(_viewTransitionTargetShowsVisual);
            if (farmManagement != null)
                farmManagement.SetActive(_viewTransitionTargetEnablesFarmManagement);
            _viewTransitionTarget = null;
            _viewTransitionElapsed = 0f;
        }

        private void CancelCameraTransition()
        {
            IsCameraTransitioning = false;
            if (_viewTransitionCamera != null) _viewTransitionCamera.enabled = false;
            _viewTransitionTarget = null;
            _viewTransitionElapsed = 0f;
        }

        private void ActivatePlayerCameraImmediately(Camera target)
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (camera == _viewTransitionCamera) camera.enabled = false;
                if (camera == firstPersonCamera || camera == thirdPersonCamera) continue;
                if (camera.enabled && _previousCamera == null) _previousCamera = camera;
                camera.enabled = false;
            }
            firstPersonCamera.enabled = target == firstPersonCamera;
            thirdPersonCamera.enabled = target == thirdPersonCamera;
            target.enabled = true;
        }

        private static void DisableAllCamerasExcept(Camera exception)
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                camera.enabled = camera == exception;
        }

        private void TrySelectPlayer(Vector2 screenPosition)
        {
            var ray = thirdPersonCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                ApplySelectionState(false);
                return;
            }
            ApplySelectionState(hit.transform == transform || hit.transform.IsChildOf(transform));
        }

        private void TrySetDestination(Vector2 screenPosition)
        {
            var ray = thirdPersonCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(
                    ray, out var hit, 500f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                SetThirdPersonDestination(hit.point);
        }

        private void ApplySelectionState(bool selected)
        {
            _isSelected = selected;
            if (selectionHighlight != null) selectionHighlight.SetActive(selected);
            if (!selected) ClearDestination();
        }

        private void ClearDestination()
        {
            _hasDestination = false;
            if (destinationMarker != null) destinationMarker.SetActive(false);
        }

        private void TickLook(Vector2 lookDelta)
        {
            _yaw += lookDelta.x * profile.LookSensitivity;
            var minimumPitch = _currentMode == 플레이어시점Mode.ThirdPerson ? 35f : -12f;
            var maximumPitch = _currentMode == 플레이어시점Mode.ThirdPerson ? 70f : 52f;
            _pitch = Mathf.Clamp(
                _pitch - lookDelta.y * profile.LookSensitivity,
                minimumPitch, maximumPitch);
            ApplyLookRotation();
        }

        private void ApplyLookRotation()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (firstPersonPivot != null) firstPersonPivot.rotation = rotation;
            if (thirdPersonPivot != null) thirdPersonPivot.rotation = rotation;
        }

        private void CacheVisualRendererStates()
        {
            if (visualRoot == null) return;
            _visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            _visualRendererStates = new bool[_visualRenderers.Length];
            for (var index = 0; index < _visualRenderers.Length; index++)
                _visualRendererStates[index] = _visualRenderers[index].enabled;
        }

        private void SetVisualVisible(bool visible)
        {
            if (_visualRenderers.Length == 0) CacheVisualRendererStates();
            for (var index = 0; index < _visualRenderers.Length; index++)
                if (_visualRenderers[index] != null)
                    _visualRenderers[index].enabled = visible && _visualRendererStates[index];
        }

        private void OnDisable()
        {
            CancelCameraTransition();
            ReleaseCursor();
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!IsPlayerMode) return;
            GUI.color = new Color(0f, 0f, 0f, .52f);
            GUI.DrawTexture(new Rect(14f, Screen.height - 62f, 940f, 42f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            var message = IsCameraTransitioning
                ? $"시점 전환 중 · 곡선 이동 {CameraTransitionProgress:P0} · 입력은 전환 완료 후 활성화"
                : _currentMode == 플레이어시점Mode.FirstPerson
                ? "1인칭 · WASD/방향키 이동 · Shift 달리기 · 클릭 후 마우스 시선 · F3 농장 경영 · F1 전략 화면"
                : "전술 3인칭 · 농지 다중 선택/작업 초안 · 좌클릭 유닛 선택 · 우클릭 이동 · WASD 화면 이동 · 휠 확대/축소 · F2 1인칭 · F1 전략"
                    + (_isSelected ? " · 선택됨" : " · 캐릭터를 먼저 선택하세요");
            GUI.Label(new Rect(24f, Screen.height - 54f, 920f, 28f), message);
        }

        private static float NormalizePitch(float value)
            => value > 180f ? value - 360f : value;
    }
}
