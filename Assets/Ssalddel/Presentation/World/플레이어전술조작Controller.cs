using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 플레이어전술조작Math
    {
        public static bool IsDrag(Vector2 start, Vector2 current,
            float thresholdPixels)
            => (current - start).sqrMagnitude
               >= thresholdPixels * thresholdPixels;

        public static Rect ScreenRect(Vector2 first, Vector2 second)
            => Rect.MinMaxRect(
                Mathf.Min(first.x, second.x),
                Mathf.Min(first.y, second.y),
                Mathf.Max(first.x, second.x),
                Mathf.Max(first.y, second.y));

        public static bool IsDoubleTap(float previousTapTime,
            float currentTapTime, float maximumIntervalSeconds)
            => previousTapTime >= 0f
               && currentTapTime >= previousTapTime
               && currentTapTime - previousTapTime <= maximumIntervalSeconds;
    }

    /// <summary>
    /// 전술 3인칭의 자기 캐릭터 선택과 카메라 재집중 입력을 조율합니다.
    /// 선택 사각형과 카메라 입력은 표현 전용이며 Simulation 상태를 바꾸지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "전술 3인칭의 실제 포인터·키보드 선택과 재집중 피드백을 조율한다.",
        Boundary = "표현 입력은 WorldTick·WorldRevision·온라인 세계 개정을 변경하지 않는다.")]
    public sealed class 플레이어전술조작Controller : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField, Min(1f)] private float selectionDragThresholdPixels = 8f;
        [SerializeField, Min(.1f)] private float recenterDoubleTapWindowSeconds = .45f;
        [SerializeField] private Color selectionFillColor =
            new(.22f, .74f, 1f, .16f);
        [SerializeField] private Color selectionBorderColor =
            new(.42f, .86f, 1f, .9f);

        private bool selectionPointerActive;
        private bool selectionDragging;
        private Vector2 selectionStart;
        private Vector2 selectionCurrent;
        private float previousBackspaceTapTime = -1f;
        private GUIStyle selectedActorStyle = null!;

        public bool SelectionPointerActive => selectionPointerActive;
        public bool SelectionDragging => selectionDragging;
        public Rect SelectionScreenRect => 플레이어전술조작Math.ScreenRect(
            selectionStart, selectionCurrent);
        public float SelectionDragThresholdPixels => selectionDragThresholdPixels;
        public float RecenterDoubleTapWindowSeconds => recenterDoubleTapWindowSeconds;
        public bool PresentationOnly => true;

        public void Configure(플레이어경관Controller playerController)
        {
            player = playerController;
            if (!ValidateWiring())
                throw new System.ArgumentException(
                    "PlayerTacticalControlWiringInvalid");
        }

        public bool ValidateWiring()
            => player != null && player.transform == transform
                && player.PresentationOnly;

        public bool TickPointer(Mouse mouse, Keyboard keyboard)
        {
            if (!ValidateWiring() || mouse == null
                || player.CurrentMode != 플레이어시점Mode.ThirdPerson)
            {
                CancelSelectionGesture();
                return false;
            }

            var farm = player.FarmManagement;
            farm?.TickActionHotkeys(keyboard);
            if (player.PlacementInputBlocked)
            {
                CancelSelectionGesture();
                return false;
            }

            var screenPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (플레이어경관Controller.IsPointerOverUnityUi())
                    return false;

                if (player.IsScreenPointOverPlayer(screenPosition))
                {
                    CancelSelectionGesture();
                    farm?.ClearSelection();
                    player.SetThirdPersonSelection(true);
                    return true;
                }

                if (farm != null && farm.TryHandlePointerInput(mouse, keyboard))
                {
                    CancelSelectionGesture();
                    return true;
                }

                selectionPointerActive = true;
                selectionDragging = false;
                selectionStart = screenPosition;
                selectionCurrent = screenPosition;
                return true;
            }

            if (selectionPointerActive && mouse.leftButton.isPressed)
            {
                selectionCurrent = screenPosition;
                selectionDragging = 플레이어전술조작Math.IsDrag(
                    selectionStart, selectionCurrent,
                    selectionDragThresholdPixels);
                return true;
            }

            if (selectionPointerActive && mouse.leftButton.wasReleasedThisFrame)
            {
                selectionCurrent = screenPosition;
                var selected = selectionDragging
                    && player.DoesScreenRectIntersectPlayer(SelectionScreenRect);
                player.SetThirdPersonSelection(selected);
                if (selected) farm?.ClearSelection();
                CancelSelectionGesture();
                return true;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (플레이어경관Controller.IsPointerOverUnityUi()) return false;
                if (player.IsSelected)
                    return player.TrySetThirdPersonDestinationAtScreenPoint(
                        screenPosition);
                return farm != null && farm.TryHandlePointerInput(mouse, keyboard);
            }

            return false;
        }

        public bool TryConsumeRecenterInput(Keyboard keyboard, float now)
        {
            if (keyboard?.backspaceKey.wasPressedThisFrame != true) return false;
            if (IsTextEntryFocused())
            {
                previousBackspaceTapTime = -1f;
                return false;
            }

            if (플레이어전술조작Math.IsDoubleTap(previousBackspaceTapTime,
                    now, recenterDoubleTapWindowSeconds))
            {
                previousBackspaceTapTime = -1f;
                return true;
            }

            previousBackspaceTapTime = now;
            return false;
        }

        public bool RegisterBackspaceTapForTest(float now,
            bool textEntryFocused = false)
        {
            if (textEntryFocused)
            {
                previousBackspaceTapTime = -1f;
                return false;
            }

            if (플레이어전술조작Math.IsDoubleTap(previousBackspaceTapTime,
                    now, recenterDoubleTapWindowSeconds))
            {
                previousBackspaceTapTime = -1f;
                return true;
            }

            previousBackspaceTapTime = now;
            return false;
        }

        public void CancelSelectionGesture()
        {
            selectionPointerActive = false;
            selectionDragging = false;
        }

        private static bool IsTextEntryFocused()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null) return false;
            var input = selected.GetComponent<InputField>();
            return input != null && input.isFocused;
        }

        private void OnGUI()
        {
            if (player == null
                || player.CurrentMode != 플레이어시점Mode.ThirdPerson)
                return;

            DrawSelectedActorLabel();
            if (!selectionDragging) return;

            var screenRect = SelectionScreenRect;
            var guiRect = new Rect(screenRect.xMin,
                Screen.height - screenRect.yMax,
                screenRect.width, screenRect.height);
            var previousColor = GUI.color;
            GUI.color = selectionFillColor;
            GUI.DrawTexture(guiRect, Texture2D.whiteTexture);
            GUI.color = selectionBorderColor;
            GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMin,
                guiRect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMax - 1f,
                guiRect.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMin,
                1f, guiRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(guiRect.xMax - 1f, guiRect.yMin,
                1f, guiRect.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawSelectedActorLabel()
        {
            if (!player.IsSelected || player.PlayerCamera == null
                || !player.PlayerCamera.enabled)
                return;

            var screenPoint = player.PlayerCamera.WorldToScreenPoint(
                player.transform.position + Vector3.up * 2.25f);
            if (screenPoint.z <= 0f) return;

            selectedActorStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, .88f, .34f, 1f) }
            };
            var labelRect = new Rect(
                screenPoint.x - 62f,
                Screen.height - screenPoint.y - 52f,
                124f,
                28f);
            GUI.Box(labelRect, "내 캐릭터 선택됨", selectedActorStyle);
        }
    }
}
