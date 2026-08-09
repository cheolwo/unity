using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.PresentationContracts.LearningCards;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class ConceptCardDeckView : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot = null!;
        [SerializeField] private TextMesh deckStatusText = null!;
        [SerializeField] private ConceptCardVisualSkin visualSkin = null!;
        [SerializeField] private ConceptCardView[] cardSlots = Array.Empty<ConceptCardView>();

        private readonly Dictionary<string, ConceptCardView> visible =
            new Dictionary<string, ConceptCardView>(StringComparer.Ordinal);
        private ConceptCardDeckPresentationModel? current;

        public event Action<string>? CardSelected;

        public int SlotCount => cardSlots?.Length ?? 0;
        public int DisplayedCardCount => visible.Count;
        public string SelectedCardStableId => current?.SelectedCardStableId?.Value ?? string.Empty;
        public GameObject VisualRoot => visualRoot;

        public void Configure(
            GameObject visual,
            TextMesh status,
            ConceptCardVisualSkin skin,
            ConceptCardView[] slots)
        {
            visualRoot = visual;
            deckStatusText = status;
            visualSkin = skin;
            cardSlots = slots ?? Array.Empty<ConceptCardView>();
        }

        public void Apply(ConceptCardDeckPresentationModel deck)
        {
            current = deck ?? throw new ArgumentNullException(nameof(deck));
            if (!ValidateWiring()) throw new InvalidOperationException("ConceptCardDeckViewWiringInvalid");
            if (deck.Cards.Length > cardSlots.Length)
                throw new InvalidOperationException("ConceptCardDeckSlotCapacityExceeded");

            visible.Clear();
            for (var index = 0; index < cardSlots.Length; index++)
            {
                var slot = cardSlots[index];
                slot.Selected -= HandleCardSelected;
                if (index >= deck.Cards.Length)
                {
                    slot.Hide();
                    continue;
                }

                var card = deck.Cards[index];
                if (!visible.TryAdd(card.StableId.Value, slot))
                    throw new InvalidOperationException("DuplicateConceptCardViewStableId:" + card.StableId.Value);
                slot.Apply(card, visualSkin,
                    deck.SelectedCardStableId.HasValue
                    && deck.SelectedCardStableId.Value.Value == card.StableId.Value);
                slot.Selected += HandleCardSelected;
            }

            deckStatusText.text = deck.ModeCode + " · " + deck.RoleCode
                + " · revision " + deck.SourceRevision;
        }

        public void Show() => visualRoot.SetActive(true);
        public void Hide() => visualRoot.SetActive(false);

        public bool SelectCard(string stableId)
        {
            if (current == null || string.IsNullOrWhiteSpace(stableId)
                || !visible.ContainsKey(stableId)) return false;
            foreach (var pair in visible) pair.Value.SetSelected(pair.Key == stableId);
            current.SelectedCardStableId = current.Cards
                .Where(value => value.StableId.Value == stableId)
                .Select(value => (Ssalddel.Unity.InterpretationContracts.PresentationStableId?)value.StableId)
                .Single();
            CardSelected?.Invoke(stableId);
            return true;
        }

        public bool ValidateWiring()
            => visualRoot != null && deckStatusText != null && visualSkin != null
                && visualSkin.ValidateWiring() && cardSlots != null && cardSlots.Length > 0
                && cardSlots.All(value => value != null && value.ValidateWiring())
                && cardSlots.Distinct().Count() == cardSlots.Length;

        private void HandleCardSelected(string stableId) => SelectCard(stableId);
    }
}
