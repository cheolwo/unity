using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 전략카메라탐색Mode
    {
        FreeExplore,
        ObjectFocus,
    }

    /// <summary>
    /// Game View 사용자의 전략 카메라 입력만 처리합니다.
    /// 서버·Simulation 상태와 업무 완료 여부는 읽거나 변경하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 전략카메라Controller : MonoBehaviour
    {
        [SerializeField] private DioramaTopDownCameraRig cameraRig = null!;
        [SerializeField] private Transform cameraPivot = null!;
        [SerializeField] private Camera targetCamera = null!;

        [Header("World bounds (X/Z)")]
        [SerializeField] private Vector2 worldMinimum = new(-65f, -50f);
        [SerializeField] private Vector2 worldMaximum = new(65f, 50f);

        [Header("Movement and rotation")]
        [SerializeField, Min(.1f)] private float moveUnitsPerSecond = 22f;
        [SerializeField, Min(.1f)] private float keyboardYawDegreesPerSecond = 90f;
        [SerializeField, Min(.01f)] private float mouseYawDegreesPerPixel = .18f;
        [SerializeField, Min(.01f)] private float mousePitchDegreesPerPixel = .12f;

        [Header("Zoom")]
        [SerializeField, Min(.1f)] private float zoomUnitsPerWheelStep = 4f;
        [SerializeField, Min(.1f)] private float minimumZoomDistance = 12f;
        [SerializeField, Min(.1f)] private float maximumZoomDistance = 110f;

        private bool dragging;

        public 전략카메라탐색Mode Mode { get; private set; } = 전략카메라탐색Mode.FreeExplore;
        public Vector2 WorldMinimum => worldMinimum;
        public Vector2 WorldMaximum => worldMaximum;
        public float MinimumZoomDistance => minimumZoomDistance;
        public float MaximumZoomDistance => maximumZoomDistance;
        public bool IsDragging => dragging;

        public void Configure(
            DioramaTopDownCameraRig rig,
            Transform pivot,
            Camera camera,
            Vector2 minimum,
            Vector2 maximum,
            float minimumZoom,
            float maximumZoom)
        {
            cameraRig = rig;
            cameraPivot = pivot;
            targetCamera = camera;
            worldMinimum = minimum;
            worldMaximum = maximum;
            minimumZoomDistance = minimumZoom;
            maximumZoomDistance = maximumZoom;
            ValidateConfiguration();
            ApplyRigLimits();
            cameraRig.SetPrototypeInputEnabled(false);
        }

        private void Awake()
        {
            ValidateConfiguration();
            ApplyRigLimits();
            cameraRig.SetPrototypeInputEnabled(false);
        }

        private void OnDisable() => EndMouseDrag();

        private void Update()
        {
            var deltaTime = Time.unscaledDeltaTime;
            ReadKeyboard(deltaTime);
            ReadMouse();
        }

        public void ApplyMoveInput(Vector2 input, float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (input.sqrMagnitude > 1f) input.Normalize();
            var worldDirection = Quaternion.Euler(0f, cameraRig.YawDegrees, 0f)
                * new Vector3(input.x, 0f, input.y);
            cameraRig.PanWithinBounds(
                worldDirection.x * moveUnitsPerSecond * deltaTime,
                worldDirection.z * moveUnitsPerSecond * deltaTime,
                worldMinimum,
                worldMaximum);
            if (input.sqrMagnitude > 0f) Mode = 전략카메라탐색Mode.FreeExplore;
        }

        public void ApplyKeyboardYaw(float direction, float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Mathf.Abs(direction) <= .001f) return;
            cameraRig.RotateYaw(direction * keyboardYawDegreesPerSecond * deltaTime);
            Mode = 전략카메라탐색Mode.FreeExplore;
        }

        public void ApplyMouseRotation(Vector2 pointerDelta)
        {
            if (pointerDelta.sqrMagnitude <= 0f) return;
            cameraRig.RotateYaw(pointerDelta.x * mouseYawDegreesPerPixel);
            cameraRig.AdjustPitch(-pointerDelta.y * mousePitchDegreesPerPixel);
            Mode = 전략카메라탐색Mode.FreeExplore;
        }

        public void ApplyZoomInput(float scrollY)
        {
            if (Mathf.Abs(scrollY) <= .01f) return;
            cameraRig.Zoom(-(scrollY / 120f) * zoomUnitsPerWheelStep);
            Mode = 전략카메라탐색Mode.FreeExplore;
        }

        public void BeginObjectFocus(string anchorId)
        {
            cameraRig.Focus(anchorId);
            Mode = 전략카메라탐색Mode.ObjectFocus;
        }

        public void ClearObjectFocus()
        {
            Mode = 전략카메라탐색Mode.FreeExplore;
        }

        public bool CanBeginWorldPointerInteraction()
            => EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();

        public void ValidateConfiguration()
        {
            if (cameraRig == null || cameraPivot == null || targetCamera == null)
                throw new InvalidOperationException("StrategyCameraWiringInvalid");
            if (worldMaximum.x < worldMinimum.x || worldMaximum.y < worldMinimum.y)
                throw new InvalidOperationException("StrategyCameraWorldBoundsInvalid");
            if (minimumZoomDistance <= 0f || maximumZoomDistance < minimumZoomDistance)
                throw new InvalidOperationException("StrategyCameraZoomBoundsInvalid");
            if (!cameraPivot.IsChildOf(transform) || !targetCamera.transform.IsChildOf(cameraPivot))
                throw new InvalidOperationException("StrategyCameraHierarchyInvalid");
        }

        private void ReadKeyboard(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var horizontal = Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
            var vertical = Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
            ApplyMoveInput(new Vector2(horizontal, vertical), deltaTime);
            ApplyKeyboardYaw(Axis(keyboard.qKey.isPressed, keyboard.eKey.isPressed), deltaTime);
        }

        private void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            ApplyZoomInput(mouse.scroll.ReadValue().y);

            if (mouse.rightButton.wasPressedThisFrame && CanBeginWorldPointerInteraction())
            {
                dragging = true;
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = false;
            }
            if (mouse.rightButton.wasReleasedThisFrame) EndMouseDrag();
            if (dragging && mouse.rightButton.isPressed)
                ApplyMouseRotation(mouse.delta.ReadValue());
        }

        private void EndMouseDrag()
        {
            dragging = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ApplyRigLimits()
        {
            cameraRig.ConfigureInteractionLimits(
                35f,
                75f,
                minimumZoomDistance,
                maximumZoomDistance);
            cameraRig.Initialize();
        }

        private static float Axis(bool negative, bool positive)
            => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
