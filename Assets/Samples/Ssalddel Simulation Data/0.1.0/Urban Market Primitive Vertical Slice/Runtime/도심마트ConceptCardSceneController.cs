using System;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트ConceptCardSceneController : MonoBehaviour
    {
        [SerializeField] private 공동주택대표NpcView representativeView = null!;
        [SerializeField] private ConceptCardDeckView deckView = null!;

        private readonly ResidentialGroupRepresentativeUnityCoordinator coordinator =
            new ResidentialGroupRepresentativeUnityCoordinator();

        public void Configure(
            공동주택대표NpcView representative,
            ConceptCardDeckView deck)
        {
            representativeView = representative;
            deckView = deck;
        }

        public bool ValidateWiring()
            => representativeView != null && representativeView.ValidateWiring()
                && deckView != null && deckView.ValidateWiring();

        private void Start()
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("UrbanMarketConceptCardSceneWiringInvalid");
            deckView.Apply(도심마트ConceptCardSampleFixture.CreateDeck());
            deckView.Hide();
            representativeView.Selected += HandleRepresentativeSelected;
            coordinator.Apply(
                ResidentialGroupRepresentativeVisitFixture.Create(),
                도심마트ConceptCardSampleFixture.CreateDialogue(),
                representativeView,
                representativeView);
        }

        private void HandleRepresentativeSelected(string npcStableId)
        {
            if (npcStableId != representativeView.NpcStableId)
                throw new InvalidOperationException("ResidentialRepresentativeSelectionMismatch");
            deckView.Show();
        }

        private void OnDestroy()
        {
            if (representativeView != null)
                representativeView.Selected -= HandleRepresentativeSelected;
        }
    }
}
