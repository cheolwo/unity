using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.PlayerActivities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 농장경영대상종류Codes
    {
        public const string Plot = "Plot";
        public const string Facility = "Facility";
        public const string Worker = "Worker";
    }

    public static class 농장경영작업Codes
    {
        public const string Till = "Till";
        public const string Sow = "Sow";
        public const string Water = "Water";
        public const string Harvest = "Harvest";

        public static readonly string[] All = { Till, Sow, Water, Harvest };

        public static string KoreanLabel(string code)
            => code switch
            {
                Till => "밭갈기",
                Sow => "파종",
                Water => "관수",
                Harvest => "수확",
                _ => code,
            };
    }

    [DisallowMultipleComponent]
    public sealed class 농장경영선택대상View : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string koreanName = string.Empty;
        [SerializeField] private string targetKindCode = string.Empty;
        [SerializeField] private string[] allowedActionCodes = Array.Empty<string>();
        [SerializeField] private GameObject selectionHighlight = null!;
        [SerializeField] private bool presentationOnly = true;

        public string StableId => stableId;
        public string KoreanName => koreanName;
        public string TargetKindCode => targetKindCode;
        public IReadOnlyList<string> AllowedActionCodes => allowedActionCodes;
        public bool IsSelected { get; private set; }
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            string targetStableId,
            string displayName,
            string kindCode,
            IEnumerable<string> actions,
            GameObject highlight)
        {
            stableId = targetStableId?.Trim() ?? string.Empty;
            koreanName = displayName?.Trim() ?? string.Empty;
            targetKindCode = kindCode?.Trim() ?? string.Empty;
            allowedActionCodes = actions?.Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            selectionHighlight = highlight;
            presentationOnly = true;
            SetSelected(false);
            if (!ValidateWiring())
                throw new ArgumentException("FarmManagementTargetWiringInvalid");
        }

        public bool ValidateWiring()
            => !string.IsNullOrWhiteSpace(stableId)
                && !string.IsNullOrWhiteSpace(koreanName)
                && !string.IsNullOrWhiteSpace(targetKindCode)
                && allowedActionCodes.Length > 0
                && allowedActionCodes.All(농장경영작업Codes.All.Contains)
                && selectionHighlight != null
                && GetComponent<Collider>() != null
                && presentationOnly;

        public bool Allows(string actionCode)
            => allowedActionCodes.Contains(actionCode, StringComparer.Ordinal);

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionHighlight != null)
                selectionHighlight.SetActive(selected);
        }
    }

    [Serializable]
    public sealed class 농장경영작업초안
    {
        public string DraftStableId = string.Empty;
        public string ActionCode = string.Empty;
        public string[] TargetStableIds = Array.Empty<string>();
        public Vector3 WorldPoint;
        public bool RequiresExplicitConfirm = true;
        public bool ChangesWorldState;
        public bool PresentationOnly = true;
    }

    /// <summary>
    /// 농장 전술 3인칭에서 여러 농지의 상태를 읽고 작업 초안을 만드는 표현 전용 도구입니다.
    /// 초안은 서버 Preview나 Confirm이 아니며 업무 완료를 확정하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 농장경영시점Controller : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 농장경영선택대상View[] targets =
            Array.Empty<농장경영선택대상View>();
        [SerializeField] private bool presentationOnly = true;

        private readonly List<농장경영선택대상View> selected = new();
        private string selectedActionCode = 농장경영작업Codes.Till;

        public bool IsActive { get; private set; }
        public bool PresentationOnly => presentationOnly;
        public IReadOnlyList<농장경영선택대상View> SelectedTargets => selected;
        public string SelectedActionCode => selectedActionCode;
        public 농장경영작업초안? CurrentDraft { get; private set; }
        public string RuleRevision => PlayerActivityViewPolicyCatalog.RuleRevision;

        public void Configure(
            플레이어경관Controller playerController,
            IEnumerable<농장경영선택대상View> selectableTargets)
        {
            player = playerController;
            targets = selectableTargets?.Where(value => value != null)
                .Distinct()
                .ToArray() ?? Array.Empty<농장경영선택대상View>();
            presentationOnly = true;
            if (!ValidateWiring())
                throw new ArgumentException("FarmManagementViewWiringInvalid");
        }

        public bool ValidateWiring()
            => player != null
                && player.PresentationOnly
                && targets.Length > 0
                && targets.All(value => value != null && value.ValidateWiring())
                && targets.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal).Count() == targets.Length
                && presentationOnly;

        public void SetActive(bool active)
        {
            IsActive = active && ValidateWiring();
            if (IsActive) return;
            ClearSelection();
            CurrentDraft = null;
        }

        public void TickActionHotkeys(Keyboard keyboard)
        {
            if (!IsActive || keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) SelectAction(농장경영작업Codes.Till);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectAction(농장경영작업Codes.Sow);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectAction(농장경영작업Codes.Water);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectAction(농장경영작업Codes.Harvest);
        }

        public bool TryHandlePointerInput(Mouse mouse, Keyboard keyboard)
        {
            if (!IsActive || mouse == null) return false;
            TickActionHotkeys(keyboard);
            if (mouse.leftButton.wasPressedThisFrame)
                return TrySelectAt(mouse.position.ReadValue(),
                    keyboard != null && (keyboard.leftShiftKey.isPressed
                        || keyboard.rightShiftKey.isPressed));
            if (mouse.rightButton.wasPressedThisFrame && selected.Count > 0)
                return TryCreateDraftAt(mouse.position.ReadValue());
            return false;
        }

        public void SelectAction(string actionCode)
        {
            if (!농장경영작업Codes.All.Contains(actionCode))
                throw new ArgumentException("FarmManagementActionInvalid", nameof(actionCode));
            selectedActionCode = actionCode;
            CurrentDraft = null;
        }

        public void SelectTarget(농장경영선택대상View target, bool additive)
        {
            if (!IsActive || target == null || !targets.Contains(target))
                throw new InvalidOperationException("FarmManagementTargetUnavailable");
            if (!additive) ClearSelection();
            if (selected.Contains(target))
            {
                if (additive)
                {
                    selected.Remove(target);
                    target.SetSelected(false);
                }
                return;
            }
            selected.Add(target);
            target.SetSelected(true);
            player.SetThirdPersonSelection(false);
            CurrentDraft = null;
        }

        public 농장경영작업초안 CreateWorkDraft(Vector3 worldPoint)
        {
            if (!IsActive || selected.Count == 0)
                throw new InvalidOperationException("FarmManagementSelectionRequired");
            if (selected.Any(value => !value.Allows(selectedActionCode)))
                throw new InvalidOperationException("FarmManagementActionNotAllowed");
            var ids = selected.Select(value => value.StableId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CurrentDraft = new 농장경영작업초안
            {
                DraftStableId = "farm-work-draft:"
                    + selectedActionCode.ToLowerInvariant() + ":"
                    + string.Join("+", ids),
                ActionCode = selectedActionCode,
                TargetStableIds = ids,
                WorldPoint = worldPoint,
                RequiresExplicitConfirm = true,
                ChangesWorldState = false,
                PresentationOnly = true,
            };
            return CurrentDraft;
        }

        public void ClearSelection()
        {
            foreach (var target in selected) target.SetSelected(false);
            selected.Clear();
            CurrentDraft = null;
        }

        private bool TrySelectAt(Vector2 screenPosition)
            => TrySelectAt(screenPosition, false);

        private bool TrySelectAt(Vector2 screenPosition, bool additive)
        {
            var ray = player.PlayerCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 500f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            var target = hit.collider.GetComponentInParent<농장경영선택대상View>();
            if (target == null || !targets.Contains(target)) return false;
            SelectTarget(target, additive);
            return true;
        }

        private bool TryCreateDraftAt(Vector2 screenPosition)
        {
            var ray = player.PlayerCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 500f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            CreateWorkDraft(hit.point);
            return true;
        }

        private void OnGUI()
        {
            if (!IsActive) return;
            GUI.color = new Color(.035f, .055f, .045f, .92f);
            GUI.DrawTexture(new Rect(18f, 84f, 330f, 176f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(32f, 96f, 300f, 24f), "농장 경영 · 전술 3인칭 (기본)");
            GUI.Label(new Rect(32f, 122f, 300f, 22f),
                "Shift+좌클릭 다중 선택 · 1~4 작업 · 우클릭 초안");
            GUI.Label(new Rect(32f, 148f, 300f, 22f),
                "선택: " + (selected.Count == 0
                    ? "농지를 선택하세요"
                    : string.Join(", ", selected.Select(value => value.KoreanName))));
            GUI.Label(new Rect(32f, 174f, 300f, 22f),
                "작업: " + 농장경영작업Codes.KoreanLabel(selectedActionCode));
            GUI.Label(new Rect(32f, 200f, 300f, 44f), CurrentDraft == null
                ? "초안 없음 · 화면 조작만으로 작업은 확정되지 않습니다."
                : "초안 준비 · 대상 " + CurrentDraft.TargetStableIds.Length
                    + "개 · 서버 미리보기와 명시적 확정 필요");
        }
    }
}
