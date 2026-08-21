using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 위치 독립 네이처 H3를 여러 Game View 관점에서 살펴보기 위한 표현 전용 조종기입니다.
    /// 플레이어 이동과 카메라 전환은 Simulation WorldTick 또는 WorldRevision을 변경하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 네이처H3GameViewDirector : MonoBehaviour
    {
        private enum ViewMode
        {
            Overview,
            Home,
            Encounter,
            Defense,
            River,
            ThirdPerson,
            FirstPerson,
        }

        [SerializeField] private Camera reviewCamera = null!;
        [SerializeField] private Transform player = null!;
        [SerializeField] private CharacterController playerController = null!;
        [SerializeField] private Transform home = null!;
        [SerializeField] private Transform encounter = null!;
        [SerializeField] private Transform defense = null!;
        [SerializeField] private Transform bridge = null!;
        [SerializeField] private Transform ford = null!;
        [SerializeField] private bool captureEvidenceOnPlay;
        [SerializeField] private float movementSpeed = 7f;
        [SerializeField] private ViewMode mode = ViewMode.Overview;

        private float verticalVelocity;

        private const string EvidenceFolder =
            "Documentation/Changes/2026-08-20-Nature-H3-GameView";

        public void Configure(Camera cameraValue, Transform playerValue,
            CharacterController playerControllerValue, Transform homeValue,
            Transform encounterValue, Transform defenseValue, Transform bridgeValue,
            Transform fordValue, bool shouldCaptureEvidence)
        {
            reviewCamera = cameraValue;
            player = playerValue;
            playerController = playerControllerValue;
            home = homeValue;
            encounter = encounterValue;
            defense = defenseValue;
            bridge = bridgeValue;
            ford = fordValue;
            captureEvidenceOnPlay = shouldCaptureEvidence;
        }

        public bool Validate()
            => reviewCamera != null && player != null && playerController != null
               && home != null && encounter != null && defense != null
               && bridge != null && ford != null && movementSpeed > 0f;

        public CharacterController PlayerController => playerController;
        public Transform Bridge => bridge;
        public Transform Ford => ford;
        public bool CaptureEvidenceOnPlay => captureEvidenceOnPlay;

        private void Start()
        {
            ApplyCameraImmediate();
            if (captureEvidenceOnPlay) StartCoroutine(CaptureEvidenceSequence());
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) SetMode(ViewMode.Overview);
            else if (keyboard.digit2Key.wasPressedThisFrame) SetMode(ViewMode.Home);
            else if (keyboard.digit3Key.wasPressedThisFrame) SetMode(ViewMode.Encounter);
            else if (keyboard.digit4Key.wasPressedThisFrame) SetMode(ViewMode.Defense);
            else if (keyboard.digit5Key.wasPressedThisFrame) SetMode(ViewMode.River);
            else if (keyboard.f3Key.wasPressedThisFrame) SetMode(ViewMode.ThirdPerson);
            else if (keyboard.f2Key.wasPressedThisFrame) SetMode(ViewMode.FirstPerson);

            if (mode is ViewMode.ThirdPerson or ViewMode.FirstPerson)
                MovePlayer(keyboard);
        }

        private void LateUpdate()
        {
            if (mode is ViewMode.ThirdPerson or ViewMode.FirstPerson)
                ApplyCameraImmediate();
        }

        private void MovePlayer(Keyboard keyboard)
        {
            var input = Vector3.zero;
            if (keyboard.wKey.isPressed) input.z += 1f;
            if (keyboard.sKey.isPressed) input.z -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (input.sqrMagnitude < .01f) return;
            MovePlayerForReview(input, Time.deltaTime);
        }

        /// <summary>검토 Scene의 표현 전용 이동입니다. 서버 명령이나 Simulation 시간을 만들지 않습니다.</summary>
        public void MovePlayerForReview(Vector3 worldInput, float deltaTime)
        {
            if (!Validate() || deltaTime <= 0f) return;
            worldInput.y = 0f;
            if (worldInput.sqrMagnitude < .01f) return;
            var input = worldInput.normalized;
            if (playerController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -.5f;
            else
                verticalVelocity += Physics.gravity.y * deltaTime;

            var before = player.position;
            playerController.Move(input * (movementSpeed * deltaTime)
                                  + Vector3.up * (verticalVelocity * deltaTime));
            var clamped = player.position;
            clamped.x = Mathf.Clamp(clamped.x, -10f, 70f);
            clamped.z = Mathf.Clamp(clamped.z, -16f, 42f);
            if (clamped != player.position)
            {
                playerController.enabled = false;
                player.position = clamped;
                playerController.enabled = true;
            }

            if ((player.position - before).sqrMagnitude < .000001f) return;
            player.rotation = Quaternion.Slerp(player.rotation,
                Quaternion.LookRotation(input, Vector3.up), 12f * deltaTime);
        }

        private void SetMode(ViewMode next)
        {
            mode = next;
            ApplyCameraImmediate();
        }

        private void ApplyCameraImmediate()
        {
            if (!Validate()) return;
            Vector3 position;
            Vector3 focus;
            switch (mode)
            {
                case ViewMode.Home:
                    position = home.position + new Vector3(-11f, 6.5f, -13f);
                    focus = home.position + new Vector3(1f, 1.2f, 1f);
                    break;
                case ViewMode.Encounter:
                    position = encounter.position + new Vector3(-17f, 11f, -18f);
                    focus = encounter.position + new Vector3(2f, 1f, 1f);
                    break;
                case ViewMode.Defense:
                    position = defense.position + new Vector3(-12f, 7f, -13f);
                    focus = defense.position + new Vector3(1f, 1f, 1f);
                    break;
                case ViewMode.River:
                    var crossingFocus = (bridge.position + ford.position) * .5f;
                    position = crossingFocus + new Vector3(-23f, 17f, -25f);
                    focus = crossingFocus + new Vector3(2f, 0f, 3f);
                    break;
                case ViewMode.ThirdPerson:
                    position = player.position - player.forward * 8f
                               + Vector3.up * 5.2f;
                    focus = player.position + Vector3.up * 1.4f
                            + player.forward * 3f;
                    break;
                case ViewMode.FirstPerson:
                    position = player.position + Vector3.up * 1.65f
                               + player.forward * .25f;
                    focus = position + player.forward * 8f;
                    break;
                default:
                    position = new Vector3(25f, 39f, -47f);
                    focus = new Vector3(27f, 0f, 12f);
                    break;
            }

            reviewCamera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation((focus - position).normalized, Vector3.up));
        }

        private IEnumerator CaptureEvidenceSequence()
        {
            yield return new WaitForSecondsRealtime(.75f);
            yield return Capture(ViewMode.Overview, "NatureH3-01-Overview.png");
            yield return Capture(ViewMode.Home, "NatureH3-02-HomeCore.png");
            yield return Capture(ViewMode.Defense, "NatureH3-03-DefenseRing.png");
            SetMode(ViewMode.Overview);
        }

        private IEnumerator Capture(ViewMode viewMode, string fileName)
        {
            SetMode(viewMode);
            yield return new WaitForEndOfFrame();
            var projectRoot = Directory.GetParent(
                UnityEngine.Application.dataPath)!.FullName;
            var folder = Path.Combine(projectRoot, EvidenceFolder);
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, fileName), 1);
            yield return new WaitForSecondsRealtime(.4f);
        }

        private void OnGUI()
        {
            var panel = new Rect(24f, 24f, 430f, 124f);
            GUI.Box(panel, string.Empty);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(.92f, .95f, .9f) },
            };
            GUI.Label(new Rect(42f, 34f, 350f, 28f),
                "네이처 생활핵 · 조우 · 방어 H3", titleStyle);
            GUI.Label(new Rect(42f, 65f, 350f, 66f),
                "1 전체  2 생활핵  3 조우로  4 방어환  5 하천\n"
                + "F3 3인칭  F2 1인칭  WASD 이동\n"
                + "표현 전용 검토 Scene · 서버 상태 변경 없음", bodyStyle);
        }
    }
}
