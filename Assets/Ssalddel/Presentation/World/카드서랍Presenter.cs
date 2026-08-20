using System;
using System.Linq;
using Ssalddel.Unity.Cards;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 카드서랍Presenter : MonoBehaviour
    {
        [SerializeField] private GameObject panel = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text contentText = null!;
        [SerializeField] private Text selectionText = null!;
        [SerializeField] private Text statusText = null!;
        [SerializeField] private Button previousButton = null!;
        [SerializeField] private Button nextButton = null!;
        [SerializeField] private Button ownerActionButton = null!;
        [SerializeField] private Button directPrimaryButton = null!;
        [SerializeField] private Button tacticalPrimaryButton = null!;
        [SerializeField] private Button closeButton = null!;

        private CardWorkspaceSnapshot snapshot = new();
        private int selectedIndex;
        private bool listenersBound;

        public event Action? RefreshRequested;
        public event Action<CardWorkspaceItem, string>? ActionRequested;

        public bool IsOpen => panel != null && panel.activeSelf;
        public CardWorkspaceItem? SelectedItem => snapshot.Items.Length == 0
            ? null : snapshot.Items[Mathf.Clamp(selectedIndex, 0, snapshot.Items.Length - 1)];
        public string Status => statusText != null ? statusText.text : string.Empty;

        public void Configure(GameObject surface, Text title, Text content,
            Text selection, Text status, Button previous, Button next,
            Button ownerAction, Button directPrimary, Button tacticalPrimary,
            Button close)
        {
            panel = surface; titleText = title; contentText = content;
            selectionText = selection; statusText = status;
            previousButton = previous; nextButton = next;
            ownerActionButton = ownerAction; directPrimaryButton = directPrimary;
            tacticalPrimaryButton = tacticalPrimary; closeButton = close;
            Bind();
            SetOpen(false);
        }

        private void Update()
        {
            if (Keyboard.current?.cKey.wasPressedThisFrame != true) return;
            SetOpen(!IsOpen);
            if (IsOpen) RefreshRequested?.Invoke();
        }

        private void OnDestroy()
        {
            if (!listenersBound) return;
            previousButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
            ownerActionButton.onClick.RemoveAllListeners();
            directPrimaryButton.onClick.RemoveAllListeners();
            tacticalPrimaryButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
        }

        public void ValidateWiring()
        {
            if (panel == null || titleText == null || contentText == null
                || selectionText == null || statusText == null
                || previousButton == null || nextButton == null
                || ownerActionButton == null || directPrimaryButton == null
                || tacticalPrimaryButton == null || closeButton == null)
                throw new InvalidOperationException("CardDrawerWiringInvalid");
        }

        public void Apply(CardWorkspaceSnapshot next)
        {
            if (next == null || !next.PresentationOnly || next.Items == null
                || next.Relations == null)
                throw new InvalidOperationException("CardWorkspaceSnapshotInvalid");
            snapshot = next;
            selectedIndex = snapshot.Items.Length == 0 ? 0
                : Mathf.Clamp(selectedIndex, 0, snapshot.Items.Length - 1);
            Render();
        }

        public void SetOpen(bool open)
        {
            if (panel != null) panel.SetActive(open);
            if (open) Render();
        }

        public void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
        }

        private void Bind()
        {
            if (listenersBound) return;
            ValidateWiring();
            previousButton.onClick.AddListener(() => Move(-1));
            nextButton.onClick.AddListener(() => Move(1));
            ownerActionButton.onClick.AddListener(() => Request(string.Empty));
            directPrimaryButton.onClick.AddListener(() => Request("DirectAction"));
            tacticalPrimaryButton.onClick.AddListener(() => Request("TacticalCommand"));
            closeButton.onClick.AddListener(() => SetOpen(false));
            listenersBound = true;
        }

        private void Move(int delta)
        {
            if (snapshot.Items.Length == 0) return;
            selectedIndex = (selectedIndex + delta + snapshot.Items.Length)
                % snapshot.Items.Length;
            Render();
        }

        private void Request(string controlModeCode)
        {
            var selected = SelectedItem;
            if (selected == null) return;
            ActionRequested?.Invoke(selected, controlModeCode);
        }

        private void Render()
        {
            if (titleText == null) return;
            titleText.text = "C 카드 서랍  ·  의미 → 상황 → 행동 → 지식";
            contentText.text = snapshot.Items.Length == 0
                ? "서버에서 조회된 카드가 없습니다."
                : string.Join("\n", snapshot.Items.Select((item, index) =>
                    (index == selectedIndex ? "▶ " : "  ")
                    + TierLabel(item.HierarchyTierCode) + " / "
                    + FamilyLabel(item.FamilyCode) + "  " + item.Title
                    + (item.IsLocked ? "  [잠김]" : string.Empty)));
            var selected = SelectedItem;
            selectionText.text = selected == null ? "선택 카드 없음"
                : selected.Title + "\n" + selected.Summary + "\n권위: "
                  + selected.AuthorityCode + " · 실행 소유자: "
                  + selected.ActionRouteCode;
            var roleCard = selected?.FamilyCode == CardFamilyCodes.TeamRole
                && !selected.IsLocked;
            directPrimaryButton.interactable = roleCard;
            tacticalPrimaryButton.interactable = roleCard;
            ownerActionButton.interactable = selected != null
                && selected.ActionRouteCode != CardActionRouteCodes.None;
        }

        private static string TierLabel(string code) => code switch
        {
            CardHierarchyTierCodes.Meta => "상위 문맥",
            CardHierarchyTierCodes.Context => "현재 상황",
            CardHierarchyTierCodes.Action => "행동",
            CardHierarchyTierCodes.Knowledge => "지식",
            CardHierarchyTierCodes.Research => "연구",
            _ => code,
        };

        private static string FamilyLabel(string code) => code switch
        {
            CardFamilyCodes.Tarot => "타로",
            CardFamilyCodes.TurnClosing => "턴 마감",
            CardFamilyCodes.Culture => "문화",
            CardFamilyCodes.TeamRole => "역할·전투 편성",
            CardFamilyCodes.BattleSnapshot => "전투 시작 사본",
            CardFamilyCodes.ConceptInformation => "정보·개념",
            CardFamilyCodes.ResearchSeedbed => "연구 모판",
            _ => code,
        };
    }
}
