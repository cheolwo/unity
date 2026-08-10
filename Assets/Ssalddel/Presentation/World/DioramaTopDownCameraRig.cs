using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class DioramaCameraFocusBinding
    {
        public string AnchorId = string.Empty;
        public string LevelCode = DioramaCameraFocusLevelCodes.Zone;
        public Transform Anchor = null!;
    }

    /// <summary>
    /// Perspective 3/4 camera with bounded pan, zoom and 90-degree rotation.
    /// Camera input changes Presentation focus only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class DioramaTopDownCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera = null!;
        [SerializeField] private DioramaCameraFocusBinding[] focusBindings =
            Array.Empty<DioramaCameraFocusBinding>();
        [SerializeField] private string initialFocusAnchorId = string.Empty;
        [SerializeField] private bool enablePrototypeInput = true;

        [Header("Candidate composition values")]
        [SerializeField, Range(45f, 55f)] private float pitch = 50f;
        [SerializeField, Min(1f)] private float maxDistance = 110f;
        [SerializeField, Min(1f)] private float worldDistance = 96f;
        [SerializeField, Min(1f)] private float zoneDistance = 28f;
        [SerializeField, Min(1f)] private float objectDistance = 20f;
        [SerializeField, Range(25f, 35f)] private float worldFieldOfView = 35f;
        [SerializeField, Range(25f, 35f)] private float zoneFieldOfView = 30f;
        [SerializeField, Range(25f, 35f)] private float objectFieldOfView = 28f;

        [Header("Prototype input")]
        [SerializeField, Min(.1f)] private float panUnitsPerSecond = 22f;
        [SerializeField, Min(.1f)] private float zoomUnitsPerStep = 4f;
        [SerializeField, Min(.1f)] private float transitionSharpness = 12f;

        private readonly Dictionary<string, DioramaCameraFocusBinding> bindings =
            new(StringComparer.Ordinal);
        private DioramaCameraStateMachine stateMachine = null!;
        private bool initialized;

        public string CurrentFocusAnchorId => initialized && stateMachine != null
            ? stateMachine.State.FocusAnchorId
            : string.Empty;
        public string CurrentFocusLevelCode => initialized && stateMachine != null
            ? stateMachine.State.FocusLevelCode
            : string.Empty;
        public int YawQuarterTurns => initialized && stateMachine != null
            ? stateMachine.State.YawQuarterTurns : 0;
        public float Distance => initialized && stateMachine != null
            ? stateMachine.State.Distance : 0f;
        public float ConfiguredMaxDistance => maxDistance;
        public float ConfiguredWorldDistance => worldDistance;
        public float ConfiguredZoneDistance => zoneDistance;
        public float ConfiguredObjectDistance => objectDistance;
        public Vector3 CurrentFocusPosition => initialized && stateMachine != null
            ? new Vector3(
                stateMachine.State.FocusPoint.X,
                stateMachine.State.FocusPoint.Y,
                stateMachine.State.FocusPoint.Z)
            : Vector3.zero;

        private void Awake() => Initialize();

        private void Update()
        {
            if (!initialized || stateMachine == null || !enablePrototypeInput) return;
            ReadPrototypeInput();
        }

        private void LateUpdate()
        {
            if (initialized && stateMachine != null) ApplyPose(false);
        }

        public void Configure(
            Camera camera,
            DioramaCameraFocusBinding[] anchors,
            string initialAnchorId,
            bool inputEnabled = true)
        {
            targetCamera = camera;
            focusBindings = anchors ?? Array.Empty<DioramaCameraFocusBinding>();
            initialFocusAnchorId = initialAnchorId;
            enablePrototypeInput = inputEnabled;
            initialized = false;
        }

        public void Initialize()
        {
            if (initialized) return;
            targetCamera = targetCamera != null ? targetCamera : GetComponent<Camera>();
            if (targetCamera == null)
                throw new InvalidOperationException("DioramaCameraTargetMissing");

            bindings.Clear();
            foreach (var binding in focusBindings)
            {
                if (binding == null || binding.Anchor == null
                    || string.IsNullOrWhiteSpace(binding.AnchorId)
                    || !DioramaCameraFocusLevelCodes.IsKnown(binding.LevelCode))
                {
                    throw new InvalidOperationException("DioramaCameraFocusBindingInvalid");
                }

                if (!bindings.TryAdd(binding.AnchorId, binding))
                    throw new InvalidOperationException("DioramaCameraFocusBindingDuplicate:" + binding.AnchorId);
            }

            if (!bindings.TryGetValue(initialFocusAnchorId, out var initial))
                throw new InvalidOperationException("DioramaCameraInitialFocusMissing:" + initialFocusAnchorId);

            var settings = new DioramaCameraSettings
            {
                MaxDistance = maxDistance,
                WorldDistance = worldDistance,
                ZoneDistance = zoneDistance,
                ObjectDistance = objectDistance,
                WorldFieldOfView = worldFieldOfView,
                ZoneFieldOfView = zoneFieldOfView,
                ObjectFieldOfView = objectFieldOfView,
            };
            stateMachine = new DioramaCameraStateMachine(settings, ToFocus(initial));
            stateMachine.SetPitch(pitch);
            targetCamera.orthographic = false;
            initialized = true;
            ApplyPose(true);
        }

        public void ConfigureComposition(
            float targetPitch,
            float targetWorldDistance,
            float targetZoneDistance,
            float targetObjectDistance,
            float targetWorldFieldOfView,
            float targetZoneFieldOfView,
            float targetObjectFieldOfView,
            float targetMaxDistance = 110f)
        {
            if (targetPitch < 45f || targetPitch > 55f)
                throw new InvalidOperationException("DioramaCameraPitchInvalid");
            var settings = new DioramaCameraSettings
            {
                MaxDistance = targetMaxDistance,
                WorldDistance = targetWorldDistance,
                ZoneDistance = targetZoneDistance,
                ObjectDistance = targetObjectDistance,
                WorldFieldOfView = targetWorldFieldOfView,
                ZoneFieldOfView = targetZoneFieldOfView,
                ObjectFieldOfView = targetObjectFieldOfView,
            };
            settings.Validate();

            pitch = targetPitch;
            maxDistance = targetMaxDistance;
            worldDistance = targetWorldDistance;
            zoneDistance = targetZoneDistance;
            objectDistance = targetObjectDistance;
            worldFieldOfView = targetWorldFieldOfView;
            zoneFieldOfView = targetZoneFieldOfView;
            objectFieldOfView = targetObjectFieldOfView;
            initialized = false;
            Initialize();
        }

        public void ConfigureInitialFocus(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId))
                throw new InvalidOperationException("DioramaCameraInitialFocusEmpty");
            initialFocusAnchorId = anchorId;
            initialized = false;
            Initialize();
        }

        public void Focus(string anchorId)
        {
            EnsureInitialized();
            if (!bindings.TryGetValue(anchorId, out var binding))
                throw new InvalidOperationException("DioramaCameraFocusUnknown:" + anchorId);
            stateMachine.Focus(ToFocus(binding));
        }

        public void Pan(float worldDeltaX, float worldDeltaZ)
        {
            EnsureInitialized();
            stateMachine.PanWorld(worldDeltaX, worldDeltaZ);
        }

        public void ZoomIn()
        {
            EnsureInitialized();
            stateMachine.Zoom(-zoomUnitsPerStep);
        }

        public void ZoomOut()
        {
            EnsureInitialized();
            stateMachine.Zoom(zoomUnitsPerStep);
        }

        public void RotateLeft()
        {
            EnsureInitialized();
            stateMachine.RotateQuarterTurns(-1);
        }

        public void RotateRight()
        {
            EnsureInitialized();
            stateMachine.RotateQuarterTurns(1);
        }

        public void ApplyNowForTests()
        {
            EnsureInitialized();
            ApplyPose(true);
        }

        private void ReadPrototypeInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) RotateLeft();
                if (keyboard.eKey.wasPressedThisFrame) RotateRight();

                var horizontal = Axis(keyboard.aKey.isPressed, keyboard.dKey.isPressed)
                    + Axis(keyboard.leftArrowKey.isPressed, keyboard.rightArrowKey.isPressed);
                var vertical = Axis(keyboard.sKey.isPressed, keyboard.wKey.isPressed)
                    + Axis(keyboard.downArrowKey.isPressed, keyboard.upArrowKey.isPressed);
                if (Mathf.Abs(horizontal) > .01f || Mathf.Abs(vertical) > .01f)
                {
                    var yaw = Quaternion.Euler(0f, stateMachine.State.YawQuarterTurns * 90f, 0f);
                    var delta = yaw * new Vector3(horizontal, 0f, vertical);
                    stateMachine.PanWorld(
                        delta.x * panUnitsPerSecond * Time.unscaledDeltaTime,
                        delta.z * panUnitsPerSecond * Time.unscaledDeltaTime);
                }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            var steps = mouse.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(steps) > .01f)
                stateMachine.Zoom(-steps * zoomUnitsPerStep);
        }

        private void ApplyPose(bool immediate)
        {
            var state = stateMachine.State;
            var focus = new Vector3(
                state.FocusPoint.X,
                state.FocusPoint.Y,
                state.FocusPoint.Z);
            var rotation = Quaternion.Euler(
                state.Pitch,
                state.YawQuarterTurns * 90f,
                0f);
            var position = focus + rotation * Vector3.back * state.Distance;

            if (immediate)
            {
                transform.SetPositionAndRotation(position, rotation);
                targetCamera.fieldOfView = state.FieldOfView;
                return;
            }

            var blend = 1f - Mathf.Exp(-transitionSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, state.FieldOfView, blend);
        }

        private static DioramaCameraFocus ToFocus(DioramaCameraFocusBinding binding)
        {
            var position = binding.Anchor.position;
            return new DioramaCameraFocus
            {
                AnchorId = binding.AnchorId,
                LevelCode = binding.LevelCode,
                Point = new DioramaPoint(position.x, position.y, position.z),
            };
        }

        private static float Axis(bool negative, bool positive)
            => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        private void EnsureInitialized()
        {
            if (initialized && stateMachine != null) return;
            initialized = false;
            Initialize();
        }
    }
}
