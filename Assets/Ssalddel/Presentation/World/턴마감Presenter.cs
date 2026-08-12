using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 턴마감Presenter : MonoBehaviour
    {
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private GameObject panel = null!;
        [SerializeField] private Text titleText = null!;
        [SerializeField] private Text cardText = null!;
        [SerializeField] private Text statusText = null!;
        [SerializeField] private Button noCardButton = null!;
        [SerializeField] private Button foolButton = null!;
        [SerializeField] private Button chariotButton = null!;
        [SerializeField] private Button cultureButton = null!;
        [SerializeField] private Button previewButton = null!;
        [SerializeField] private Button confirmButton = null!;

        private I턴마감AuthorityClient authority = null!;
        private 턴마감ContextData? context;
        private 턴마감PreviewData? preview;
        private string selectedCardStableId = string.Empty;
        private bool listenersBound;
        private bool 자동시작 = true;

        public string SelectedCardStableId => selectedCardStableId;
        public bool HasPreview => preview != null;
        public string Status => statusText != null ? statusText.text : string.Empty;

        public void Configure(
            SimulationWorldShellPresenter worldShell, GameObject surface, Text title,
            Text card, Text status, Button noCard, Button fool, Button chariot, Button culture,
            Button previewAction, Button confirmAction)
        {
            shell = worldShell; panel = surface; titleText = title; cardText = card;
            statusText = status; noCardButton = noCard; foolButton = fool;
            chariotButton = chariot; cultureButton = culture; previewButton = previewAction;
            confirmButton = confirmAction;
        }

        private async void Start()
        {
            if (!자동시작) return;
            ValidateWiring();
            authority ??= new 턴마감FixtureAuthorityClient();
            BindListeners();
            try
            {
                await LoadAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            if (!listenersBound) return;
            noCardButton.onClick.RemoveAllListeners();
            foolButton.onClick.RemoveAllListeners();
            chariotButton.onClick.RemoveAllListeners();
            cultureButton.onClick.RemoveAllListeners();
            previewButton.onClick.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners();
        }

        public void SetAuthorityForTests(I턴마감AuthorityClient client)
            => SetAuthority(client);

        public void SetAuthority(I턴마감AuthorityClient client)
            => authority = client ?? throw new ArgumentNullException(nameof(client));

        public void Set자동시작(bool enabled)
            => 자동시작 = enabled;

        public async Task InitializeAsync(I턴마감AuthorityClient client)
        {
            ValidateWiring();
            SetAuthority(client);
            BindListeners();
            await LoadAsync();
        }

        public async Task LoadAsync()
        {
            context = await authority.GetContextAsync(
                shell.SessionStableId, CancellationToken.None);
            if (context.SessionStableId != shell.SessionStableId)
                throw new InvalidOperationException("TurnClosingContextSessionMismatch");
            foreach (var card in context.AvailableCards)
                턴마감FixtureAuthorityClient.ValidateCard(card);
            selectedCardStableId = string.Empty;
            preview = null;
            ApplyContext();
        }

        public void SelectCard(string stableId)
        {
            if (context == null) throw new InvalidOperationException("TurnClosingContextRequired");
            if (!string.IsNullOrEmpty(stableId)
                && !context.AvailableCards.Any(value => value.CardStableId == stableId))
                throw new InvalidOperationException("TurnClosingCardUnavailable");
            selectedCardStableId = stableId ?? string.Empty;
            preview = null;
            ApplyContext();
        }

        public async Task PreviewAsync()
        {
            if (context == null) throw new InvalidOperationException("TurnClosingContextRequired");
            preview = await authority.PreviewAsync(
                context.SessionStableId, context.Revision, selectedCardStableId,
                CancellationToken.None);
            if (preview.BaseRevision != context.Revision
                || preview.NextTurnNumber != context.TurnNumber + 1)
                throw new InvalidOperationException("TurnClosingPreviewMismatch");
            statusText.text = "마감 Preview 준비 · 미완료 업무 " + preview.PendingTaskCount
                + "건\nConfirm하면 " + preview.NextGameDateLabel + "로 진행";
            confirmButton.interactable = true;
        }

        public async Task ConfirmAsync()
        {
            if (context == null || preview == null)
                throw new InvalidOperationException("TurnClosingPreviewRequired");
            var result = await authority.ConfirmAsync(
                context.SessionStableId,
                "command:unity.turn-closing:" + context.Revision,
                context.Revision, selectedCardStableId, CancellationToken.None);
            if (result.SessionStableId != context.SessionStableId
                || result.Revision != context.Revision + 1
                || result.WorldTick != preview.ClosingTurnNumber
                || result.ActiveTurnNumber != preview.NextTurnNumber
                || result.ActiveCardStableId != selectedCardStableId)
                throw new InvalidOperationException("TurnClosingResultMismatch");
            shell.ApplyAuthoritativeSnapshot(result.WorldSnapshot);
            titleText.text = "DAY " + result.ActiveTurnNumber + " · 경영 시작";
            statusText.text = string.IsNullOrEmpty(result.ActiveCardStableId)
                ? "카드 없이 다음 경영일을 시작했습니다."
                : "다음 턴 활성 · " + result.ActiveEffectCode;
            previewButton.interactable = false;
            confirmButton.interactable = false;
        }

        public void ValidateWiring()
        {
            if (shell == null || panel == null || titleText == null || cardText == null
                || statusText == null || noCardButton == null || foolButton == null
                || chariotButton == null || cultureButton == null
                || previewButton == null || confirmButton == null)
                throw new InvalidOperationException("TurnClosingPresenterWiringMissing");
        }

        private void BindListeners()
        {
            if (listenersBound) return;
            noCardButton.onClick.AddListener(() => SelectCard(string.Empty));
            foolButton.onClick.AddListener(() => SelectCard(턴마감CardStableIds.Fool));
            chariotButton.onClick.AddListener(() => SelectCard(턴마감CardStableIds.Chariot));
            cultureButton.onClick.AddListener(() => SelectCard(턴마감CardStableIds.SeoulCulture));
            previewButton.onClick.AddListener(() => _ = PreviewAsync());
            confirmButton.onClick.AddListener(() => _ = ConfirmAsync());
            listenersBound = true;
        }

        private void ApplyContext()
        {
            if (context == null) return;
            titleText.text = "DAY " + context.TurnNumber + " · 경영일 마감";
            var card = context.AvailableCards.SingleOrDefault(value =>
                value.CardStableId == selectedCardStableId);
            cardText.text = card == null
                ? "선택 카드 없음\n오늘의 경영 결과만 결산합니다."
                : FormatCard(card);
            statusText.text = context.GameDateLabel + " · 미완료 업무 "
                + context.PendingTaskCount + "건\n카드를 고른 뒤 마감 Preview를 확인하세요.";
            previewButton.interactable = context.CanCloseTurn;
            confirmButton.interactable = false;
        }

        private static string FormatCard(턴마감CardData card)
        {
            var text = card.Title + "\n" + card.Summary + "\n다음 턴  "
                + card.TargetStatCode + " +" + card.StatDelta;
            if (card.CardKindCode != "Culture") return text;
            return text + "\n지역 " + card.RegionKey + " · " + card.CalendarRevision
                + "\n근거 지역문화진흥원 · "
                + card.EvidenceCheckedAtUtc!.Value.ToString("yyyy-MM-dd") + " 확인";
        }
    }
}
