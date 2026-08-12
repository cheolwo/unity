using System;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 턴카드모판ActionCode
    {
        public const string 철학학당보기 = "ShowPhilosophyAcademy";
        public const string 지역문화보기 = "ShowRegionalCulture";
    }

    [DisallowMultipleComponent]
    public sealed class 턴카드모판Presenter : MonoBehaviour
    {
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text summaryText = null!;
        [SerializeField] private Text stageText = null!;
        [SerializeField] private Text detailText = null!;
        [SerializeField] private Text boundaryText = null!;
        [SerializeField] private Text footerText = null!;
        [SerializeField] private Button philosophyButton = null!;
        [SerializeField] private Button cultureButton = null!;
        [SerializeField] private Button[] candidateButtons = Array.Empty<Button>();

        private 턴카드모판CatalogData catalog = null!;
        private string 현재모판 = 턴카드모판Code.철학학당;
        private string 현재카드StableId = string.Empty;
        private bool listenersBound;

        public string 현재모판Code => 현재모판;
        public string 현재카드Id => 현재카드StableId;
        public int 현재후보수 => catalog?.FindByNursery(현재모판).Count ?? 0;
        public int 연구Revision => 0;
        public bool 턴확정제공여부 => false;

        public void Configure(
            Text title,
            Text summary,
            Text stage,
            Text detail,
            Text boundary,
            Text footer,
            Button philosophy,
            Button culture,
            Button[] candidates)
        {
            titleText = title;
            summaryText = summary;
            stageText = stage;
            detailText = detail;
            boundaryText = boundary;
            footerText = footer;
            philosophyButton = philosophy;
            cultureButton = culture;
            candidateButtons = candidates ?? Array.Empty<Button>();
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            ValidateWiring();
            catalog = 턴카드모판CatalogData.CreateCurrentFixture();
            BindListeners();
            ShowNursery(턴카드모판Code.철학학당);
        }

        private void OnDestroy()
        {
            if (!listenersBound) return;
            philosophyButton.onClick.RemoveAllListeners();
            cultureButton.onClick.RemoveAllListeners();
            foreach (var button in candidateButtons)
                button.onClick.RemoveAllListeners();
        }

        public void Execute(string actionCode)
        {
            switch (actionCode)
            {
                case 턴카드모판ActionCode.철학학당보기:
                    ShowNursery(턴카드모판Code.철학학당);
                    return;
                case 턴카드모판ActionCode.지역문화보기:
                    ShowNursery(턴카드모판Code.지역문화);
                    return;
                default:
                    SelectCard(actionCode);
                    return;
            }
        }

        public void ShowNursery(string nurseryCode)
        {
            if (catalog == null)
                catalog = 턴카드모판CatalogData.CreateCurrentFixture();
            var entries = catalog.FindByNursery(nurseryCode);
            if (entries.Count == 0)
                throw new InvalidOperationException("TurnCardSeedbedNurseryEmpty");
            현재모판 = nurseryCode;
            현재카드StableId = entries[0].CardStableId;
            Render();
        }

        public void SelectCard(string cardStableId)
        {
            var entry = catalog.Find(cardStableId);
            if (entry.NurseryCode != 현재모판)
                throw new InvalidOperationException("TurnCardSeedbedCardOutsideNursery");
            현재카드StableId = entry.CardStableId;
            Render();
        }

        public void ValidateWiring()
        {
            if (titleText == null || summaryText == null || stageText == null
                || detailText == null || boundaryText == null || footerText == null
                || philosophyButton == null || cultureButton == null
                || candidateButtons == null || candidateButtons.Length < 3
                || candidateButtons.Any(value => value == null))
                throw new InvalidOperationException("TurnCardSeedbedPresenterWiringMissing");
        }

        private void Render()
        {
            var entries = catalog.FindByNursery(현재모판);
            var selected = catalog.Find(현재카드StableId);
            var nurseryLabel = 현재모판 == 턴카드모판Code.철학학당
                ? "철학·학당 모판"
                : "지역문화 모판";
            titleText.text = "턴 카드 모판 · " + nurseryLabel;
            summaryText.text = "연구 후보 " + entries.Count + "장  ·  실제 게시 0장\n"
                + "모판 선택과 카드 열람은 경영 session을 변경하지 않습니다.";

            philosophyButton.image.color = 현재모판 == 턴카드모판Code.철학학당
                ? new Color(.73f, .47f, .18f, 1f) : new Color(.18f, .24f, .24f, 1f);
            cultureButton.image.color = 현재모판 == 턴카드모판Code.지역문화
                ? new Color(.22f, .53f, .4f, 1f) : new Color(.18f, .24f, .24f, 1f);

            for (var i = 0; i < candidateButtons.Length; i++)
            {
                var active = i < entries.Count;
                candidateButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                var entry = entries[i];
                candidateButtons[i].GetComponentInChildren<Text>().text = entry.Title
                    + "\n" + entry.StageSummary;
                candidateButtons[i].image.color = entry.CardStableId == 현재카드StableId
                    ? new Color(.72f, .43f, .19f, 1f) : new Color(.12f, .18f, .2f, 1f);
            }

            stageText.text = string.Join("\n", selected.Gates.Select(FormatGate));
            detailText.text = selected.Title + "\n"
                + selected.KindLabel + "\n\n"
                + "카드 ID\n" + selected.CardStableId + "\n\n"
                + "출처 revision\n" + selected.SourceRevision + "\n\n"
                + "효과 규칙\n" + selected.EffectRuleRevision;
            boundaryText.text = "확인된 범위\n" + selected.KnownBoundary + "\n\n"
                + "아직 알 수 없음\n" + selected.UnknownBoundary + "\n\n"
                + "승격 차단\n" + selected.BlockedReason;
            footerText.text = "연구 전용 · Preview/Confirm 없음 · C5 게시 snapshot 전 게임 덱 승격 금지";
            RefreshTextGeometry();
        }

        private static string FormatGate(턴카드승격GateData gate)
        {
            var status = gate.StatusCode == 턴카드승격상태Code.통과 ? "완료"
                : gate.StatusCode == 턴카드승격상태Code.Fixture검증 ? "Fixture"
                : gate.StatusCode == 턴카드승격상태Code.차단 ? "차단" : "대기";
            return "[" + status + "] " + gate.Code + " " + gate.Label + "\n  " + gate.Note;
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            philosophyButton.onClick.AddListener(() =>
                Execute(턴카드모판ActionCode.철학학당보기));
            cultureButton.onClick.AddListener(() =>
                Execute(턴카드모판ActionCode.지역문화보기));
            for (var i = 0; i < candidateButtons.Length; i++)
            {
                var index = i;
                candidateButtons[i].onClick.AddListener(() => SelectVisibleCard(index));
            }
            listenersBound = true;
        }

        private void SelectVisibleCard(int index)
        {
            var entries = catalog.FindByNursery(현재모판);
            if (index < 0 || index >= entries.Count) return;
            SelectCard(entries[index].CardStableId);
        }

        private void RefreshTextGeometry()
        {
            titleText.SetAllDirty();
            summaryText.SetAllDirty();
            stageText.SetAllDirty();
            detailText.SetAllDirty();
            boundaryText.SetAllDirty();
            footerText.SetAllDirty();
            foreach (var button in candidateButtons)
            {
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null) label.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }
    }
}
