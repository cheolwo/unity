using System;
using System.Collections.Generic;
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
        [SerializeField] private 타로객체강조Presenter tarotHighlighter = null!;

        private I턴마감AuthorityClient authority = null!;
        private 턴마감ContextData? context;
        private 턴마감PreviewData? preview;
        private 타로객체반응PreviewData? tarotReactionPreview;
        private 턴마감타로SelectionData? selectedTarot;
        private string selectedCardStableId = string.Empty;
        private bool listenersBound;
        private bool 자동시작 = true;

        public string SelectedCardStableId => selectedCardStableId;
        public bool HasPreview => preview != null;
        public string Status => statusText != null ? statusText.text : string.Empty;
        public string SelectedTarotOfferStableId => selectedTarot?.OfferStableId ?? string.Empty;
        public IReadOnlyCollection<string> HighlightedObjectStableIds
            => tarotHighlighter != null
                ? tarotHighlighter.HighlightedObjectStableIds
                : Array.Empty<string>();

        public void Configure(
            SimulationWorldShellPresenter worldShell, GameObject surface, Text title,
            Text card, Text status, Button noCard, Button fool, Button chariot, Button culture,
            Button previewAction, Button confirmAction,
            타로객체강조Presenter? objectHighlighter = null)
        {
            shell = worldShell; panel = surface; titleText = title; cardText = card;
            statusText = status; noCardButton = noCard; foolButton = fool;
            chariotButton = chariot; cultureButton = culture; previewButton = previewAction;
            confirmButton = confirmAction;
            tarotHighlighter = objectHighlighter!;
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
            if (tarotHighlighter != null) tarotHighlighter.Clear();
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
            selectedTarot = null;
            tarotReactionPreview = null;
            preview = null;
            if (tarotHighlighter != null) tarotHighlighter.Clear();
            ApplyContext();
        }

        public void SelectCard(string stableId)
        {
            if (context == null) throw new InvalidOperationException("TurnClosingContextRequired");
            if (!string.IsNullOrEmpty(stableId)
                && !context.AvailableCards.Any(value => value.CardStableId == stableId))
                throw new InvalidOperationException("TurnClosingCardUnavailable");
            selectedCardStableId = stableId ?? string.Empty;
            selectedTarot = null;
            preview = null;
            if (tarotHighlighter != null) tarotHighlighter.Clear();
            ApplyContext();
        }

        public async Task SelectTarotOfferAsync(int slotIndex)
        {
            if (context == null || !context.TarotDraw.IsAvailable)
                throw new InvalidOperationException("TarotDrawRequired");
            var tarotAuthority = authority as I타로턴마감AuthorityClient
                ?? throw new InvalidOperationException("TarotTurnAuthorityRequired");
            var offer = context.TarotDraw.Offers.SingleOrDefault(value =>
                value.OfferSlotNumber == slotIndex)
                ?? throw new InvalidOperationException("TarotOfferUnavailable");
            selectedTarot = new 턴마감타로SelectionData
            {
                OfferStableId = offer.OfferStableId,
                CardStableId = offer.Card.CardStableId,
                OrientationCode = offer.OrientationCode,
            };
            selectedCardStableId = offer.Card.CardStableId;
            preview = null;
            tarotReactionPreview ??= await tarotAuthority.Preview타로객체반응Async(
                context.SessionStableId, context.Revision, context.TarotDraw.DrawStableId,
                CancellationToken.None);
            if (tarotReactionPreview.BaseRevision != context.Revision
                || tarotReactionPreview.DrawStableId != context.TarotDraw.DrawStableId)
                throw new InvalidOperationException("TarotObjectReactionPreviewMismatch");
            var reaction = tarotReactionPreview.Find(offer.OfferStableId);
            if (tarotHighlighter != null)
                tarotHighlighter.Apply(reaction.HighlightObjectStableIds);
            ApplyContext();
        }

        public async Task PreviewAsync()
        {
            if (context == null) throw new InvalidOperationException("TurnClosingContextRequired");
            preview = selectedTarot == null
                ? await authority.PreviewAsync(
                    context.SessionStableId, context.Revision, selectedCardStableId,
                    CancellationToken.None)
                : await (authority as I타로턴마감AuthorityClient
                    ?? throw new InvalidOperationException("TarotTurnAuthorityRequired"))
                    .Preview타로Async(context.SessionStableId, context.Revision,
                        selectedTarot, CancellationToken.None);
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
            var commandId = "command:unity.turn-closing:" + context.Revision;
            var result = selectedTarot == null
                ? await authority.ConfirmAsync(
                    context.SessionStableId, commandId, context.Revision,
                    selectedCardStableId, CancellationToken.None)
                : await (authority as I타로턴마감AuthorityClient
                    ?? throw new InvalidOperationException("TarotTurnAuthorityRequired"))
                    .Confirm타로Async(context.SessionStableId, commandId, context.Revision,
                        selectedTarot, CancellationToken.None);
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
            if (tarotHighlighter != null) tarotHighlighter.Clear();
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
            foolButton.onClick.AddListener(() => _ = SelectSlotOrLegacyAsync(
                1, 턴마감CardStableIds.Fool));
            chariotButton.onClick.AddListener(() => _ = SelectSlotOrLegacyAsync(
                2, 턴마감CardStableIds.Chariot));
            cultureButton.onClick.AddListener(() => _ = SelectSlotOrLegacyAsync(
                3, 턴마감CardStableIds.SeoulCulture));
            previewButton.onClick.AddListener(() => _ = PreviewAsync());
            confirmButton.onClick.AddListener(() => _ = ConfirmAsync());
            listenersBound = true;
        }

        private void ApplyContext()
        {
            if (context == null) return;
            titleText.text = "DAY " + context.TurnNumber + " · 경영일 마감";
            ApplyOfferButtons();
            var tarotOffer = selectedTarot == null ? null : context.TarotDraw.Offers
                .SingleOrDefault(value => value.OfferStableId == selectedTarot.OfferStableId);
            var card = tarotOffer?.Card ?? context.AvailableCards.SingleOrDefault(value =>
                value.CardStableId == selectedCardStableId);
            cardText.text = tarotOffer != null
                ? FormatTarotCard(tarotOffer)
                : card == null
                ? "선택 카드 없음\n오늘의 경영 결과만 결산합니다."
                : FormatCard(card);
            statusText.text = context.GameDateLabel + " · 미완료 업무 "
                + context.PendingTaskCount + "건\n카드를 고른 뒤 마감 Preview를 확인하세요.";
            previewButton.interactable = context.CanCloseTurn;
            confirmButton.interactable = false;
        }

        private async Task SelectSlotOrLegacyAsync(int slotIndex, string legacyStableId)
        {
            if (context != null && context.TarotDraw.IsAvailable)
                await SelectTarotOfferAsync(slotIndex);
            else
                SelectCard(legacyStableId);
        }

        private void ApplyOfferButtons()
        {
            if (context == null || !context.TarotDraw.IsAvailable) return;
            var buttons = new[] { foolButton, chariotButton, cultureButton };
            for (var index = 0; index < buttons.Length; index++)
            {
                var offer = context.TarotDraw.Offers.Single(value =>
                    value.OfferSlotNumber == index + 1);
                var label = buttons[index].GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = offer.Card.Title + " · " + OrientationLabel(offer.OrientationCode);
                buttons[index].interactable = context.CanCloseTurn;
            }
        }

        private static string FormatTarotCard(턴마감타로OfferData offer)
        {
            var interpretation = TarotInterpretation(offer.Card.CardStableId,
                offer.OrientationCode);
            return offer.Card.Title + " · " + OrientationLabel(offer.OrientationCode)
                + "\n" + offer.Card.Summary
                + "\n기회  " + interpretation.opportunity
                + "\n부담  " + interpretation.burden;
        }

        private static (string opportunity, string burden) TarotInterpretation(
            string cardStableId, string orientation)
        {
            var reversed = orientation == 턴마감타로OrientationCodes.Reversed;
            if (cardStableId == 턴마감CardStableIds.Empress)
                return reversed
                    ? ("무리한 확장을 멈추고 생산 기반을 돌봅니다.", "생산 증가가 늦어질 수 있습니다.")
                    : ("생산과 공급 회복의 여지가 커집니다.", "노동과 보관 부담도 함께 늘 수 있습니다.");
            if (cardStableId == 턴마감CardStableIds.TarotChariot)
                return reversed
                    ? ("합배송과 비용 통제 선택을 검토합니다.", "운송 지연 가능성이 커집니다.")
                    : ("운송 기간과 처리량을 개선할 수 있습니다.", "연료·노동·위험 부담이 커질 수 있습니다.");
            if (cardStableId == 턴마감CardStableIds.Justice)
                return reversed
                    ? ("불균형한 거래 조건을 다시 살핍니다.", "합의와 거래 확정이 늦어질 수 있습니다.")
                    : ("거래 기준과 배분 근거가 선명해집니다.", "선택 가능한 거래 폭이 줄 수 있습니다.");
            return reversed
                ? ("막힌 생산·재고 흐름을 다시 배분합니다.", "단기 처리량이 감소할 수 있습니다.")
                : ("생산·운송·재고의 손실을 줄일 수 있습니다.", "빠른 확장보다 균형을 우선합니다.");
        }

        private static string OrientationLabel(string code)
            => code == 턴마감타로OrientationCodes.Reversed ? "역방향" : "정방향";

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
