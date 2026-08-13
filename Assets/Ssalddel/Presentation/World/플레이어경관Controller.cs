using Ssalddel.Unity.Runtime.World;
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
        private Transform? _leftUpperLeg;
        private Transform? _rightUpperLeg;
        private Quaternion _leftLegBase;
        private Quaternion _rightLegBase;

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
            CacheLegBones();
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

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
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
            if (!presentationOnly || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.f1Key.wasPressedThisFrame) ExitPlayerMode();
            if (keyboard.f2Key.wasPressedThisFrame) EnterFirstPersonMode();
            if (keyboard.f3Key.wasPressedThisFrame) EnterThirdPersonMode();
            if (!IsPlayerMode) return;

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
            if (mouse.leftButton.wasPressedThisFrame) TrySelectPlayer(mouse.position.ReadValue());
            if (mouse.rightButton.wasPressedThisFrame && _isSelected)
                TrySetDestination(mouse.position.ReadValue());
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
            _tacticalFocus.x = Mathf.Clamp(
                _tacticalFocus.x, profile.MinimumX, profile.MaximumX);
            _tacticalFocus.z = Mathf.Clamp(
                _tacticalFocus.z, profile.MinimumZ, profile.MaximumZ);
            _tacticalDistance = Mathf.Clamp(
                _tacticalDistance - scrollInput * .01f * profile.TacticalZoomSpeed,
                profile.TacticalMinimumDistance,
                profile.TacticalMaximumDistance);
        }

        public void FocusTacticalCameraOnPlayer()
        {
            _tacticalFocus = transform.position;
            _tacticalFocus.x = Mathf.Clamp(
                _tacticalFocus.x, profile.MinimumX, profile.MaximumX);
            _tacticalFocus.z = Mathf.Clamp(
                _tacticalFocus.z, profile.MinimumZ, profile.MaximumZ);
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
            _destination = new Vector3(
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
            _characterController.Move(
                direction * (speed * deltaTime) + Vector3.up * (_verticalVelocity * deltaTime));
            var position = transform.position;
            position.x = Mathf.Clamp(position.x, profile.MinimumX, profile.MaximumX);
            position.z = Mathf.Clamp(position.z, profile.MinimumZ, profile.MaximumZ);
            transform.position = position;

            if (isMoving)
            {
                visualRoot.rotation = Quaternion.Slerp(
                    visualRoot.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    12f * deltaTime);
                _walkPhase += deltaTime * (run ? 11f : 8f);
                visualRoot.localPosition = _visualBasePosition
                    + Vector3.up * (Mathf.Abs(Mathf.Sin(_walkPhase)) * .025f);
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Walk);
                ApplyLegSwing(Mathf.Sin(_walkPhase) * (run ? 27f : 21f));
            }
            else
            {
                visualRoot.localPosition = Vector3.Lerp(
                    visualRoot.localPosition, _visualBasePosition, 10f * deltaTime);
                animationAdapter.ApplyIntent(공용AnimationIntentCodes.Idle);
                ApplyLegSwing(0f);
            }
        }

        private void LateUpdate()
        {
            if (_currentMode != 플레이어시점Mode.ThirdPerson
                || thirdPersonPivot == null || thirdPersonCamera == null) return;
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

        public void EnterPlayerMode() => EnterThirdPersonMode();

        public void EnterFirstPersonMode()
        {
            if (!ValidateWiring()) return;
            ActivatePlayerCamera(firstPersonCamera);
            _currentMode = 플레이어시점Mode.FirstPerson;
            _yaw = visualRoot.eulerAngles.y;
            _pitch = profile.InitialPitch;
            ApplyLookRotation();
            ApplySelectionState(false);
            ClearDestination();
            SetVisualVisible(false);
        }

        public void EnterThirdPersonMode()
        {
            if (!ValidateWiring()) return;
            ActivatePlayerCamera(thirdPersonCamera);
            _currentMode = 플레이어시점Mode.ThirdPerson;
            _yaw = profile.TacticalYaw;
            _pitch = profile.TacticalPitch;
            _tacticalDistance = profile.CameraDistance;
            FocusTacticalCameraOnPlayer();
            ApplyLookRotation();
            SetVisualVisible(true);
            ReleaseCursor();
        }

        public void ExitPlayerMode()
        {
            if (firstPersonCamera != null) firstPersonCamera.enabled = false;
            if (thirdPersonCamera != null) thirdPersonCamera.enabled = false;
            if (_previousCamera != null) _previousCamera.enabled = true;
            _previousCamera = null;
            _currentMode = 플레이어시점Mode.Strategy;
            ApplySelectionState(false);
            ClearDestination();
            SetVisualVisible(true);
            ReleaseCursor();
        }

        private void ActivatePlayerCamera(Camera target)
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (camera == firstPersonCamera || camera == thirdPersonCamera)
                {
                    camera.enabled = camera == target;
                    continue;
                }
                if (camera.enabled && _previousCamera == null) _previousCamera = camera;
                camera.enabled = false;
            }
            target.enabled = true;
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

        private void CacheLegBones()
        {
            if (animationAdapter == null || animationAdapter.Animator == null
                || !animationAdapter.Animator.isHuman) return;
            _leftUpperLeg = animationAdapter.Animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _rightUpperLeg = animationAdapter.Animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (_leftUpperLeg != null) _leftLegBase = _leftUpperLeg.localRotation;
            if (_rightUpperLeg != null) _rightLegBase = _rightUpperLeg.localRotation;
        }

        private void ApplyLegSwing(float angle)
        {
            if (_leftUpperLeg == null || _rightUpperLeg == null) CacheLegBones();
            if (_leftUpperLeg != null)
                _leftUpperLeg.localRotation = _leftLegBase * Quaternion.Euler(angle, 0f, 0f);
            if (_rightUpperLeg != null)
                _rightUpperLeg.localRotation = _rightLegBase * Quaternion.Euler(-angle, 0f, 0f);
        }

        private void OnDisable() => ReleaseCursor();

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
            var message = _currentMode == 플레이어시점Mode.FirstPerson
                ? "1인칭 · WASD/방향키 이동 · Shift 달리기 · 클릭 후 마우스 시선 · F3 3인칭 · F1 전략 화면"
                : "전술 3인칭 · 좌클릭 유닛 선택 · 우클릭 이동 · WASD 화면 이동 · 휠 확대/축소 · F 재집중 · F2 1인칭 · F1 전략"
                    + (_isSelected ? " · 선택됨" : " · 캐릭터를 먼저 선택하세요");
            GUI.Label(new Rect(24f, Screen.height - 54f, 920f, 28f), message);
        }

        private static float NormalizePitch(float value)
            => value > 180f ? value - 360f : value;
    }
}
