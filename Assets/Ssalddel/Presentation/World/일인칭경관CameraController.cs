using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 사람 눈높이에서 경관을 둘러보는 표현 전용 카메라입니다.
    /// 이동과 시선은 서버 상태, Simulation Tick과 업무 개정을 변경하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class 일인칭경관CameraController : MonoBehaviour
    {
        [SerializeField] private Vector2 xBounds = new(10.5f, 31.5f);
        [SerializeField] private Vector2 zBounds = new(2.5f, 22f);
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float runMultiplier = 1.8f;
        [SerializeField] private float lookSensitivity = .09f;
        [SerializeField] private bool presentationOnly = true;

        private float yaw;
        private float pitch;

        public bool PresentationOnly => presentationOnly;

        private void OnEnable()
        {
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizePitch(angles.x);
        }

        private void Update()
        {
            if (!presentationOnly || !GetComponent<Camera>().enabled
                || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            var direction = Vector3.zero;
            if (keyboard.wKey.isPressed) direction += transform.forward;
            if (keyboard.sKey.isPressed) direction -= transform.forward;
            if (keyboard.dKey.isPressed) direction += transform.right;
            if (keyboard.aKey.isPressed) direction -= transform.right;
            direction.y = 0f;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            var speed = keyboard.leftShiftKey.isPressed
                ? walkSpeed * runMultiplier : walkSpeed;
            var next = transform.position + direction * (speed * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, xBounds.x, xBounds.y);
            next.z = Mathf.Clamp(next.z, zBounds.x, zBounds.y);
            transform.position = next;

            if (Mouse.current?.rightButton.isPressed != true) return;
            var delta = Mouse.current.delta.ReadValue() * lookSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -65f, 65f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void OnGUI()
        {
            if (!isActiveAndEnabled || !GetComponent<Camera>().enabled) return;
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            GUI.color = new Color(1f, 1f, 1f, .9f);
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - 2f, 4f, 4f),
                Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, .76f);
            GUI.Label(new Rect(22f, Screen.height - 42f, 480f, 26f),
                "1인칭 경관 검증 · WASD 이동 · Shift 빠르게 · 오른쪽 마우스 시선");
            GUI.color = Color.white;
        }

        private static float NormalizePitch(float value)
            => value > 180f ? value - 360f : value;
    }
}
