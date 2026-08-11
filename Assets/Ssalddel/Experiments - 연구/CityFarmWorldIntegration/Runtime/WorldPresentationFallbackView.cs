using System;
using System.Linq;
using Ssalddel.Unity.InterpretationContracts;
using Ssalddel.Unity.Samples.UrbanMarket;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    /// <summary>
    /// Presentation asset만 교체하는 socket입니다. stable ID와 업무 View는 이 컴포넌트 밖에 남습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldPresentationFallbackView : MonoBehaviour
    {
        [SerializeField] private Transform syntyVisualRoot = null!;
        [SerializeField] private GameObject primitiveFallbackRoot = null!;

        public Transform SyntyVisualRoot => syntyVisualRoot;
        public GameObject PrimitiveFallbackRoot => primitiveFallbackRoot;
        public bool IsUsingPrimitiveFallback => primitiveFallbackRoot != null
            && primitiveFallbackRoot.activeSelf;

        public void Configure(Transform visualRoot, GameObject fallbackRoot)
        {
            syntyVisualRoot = visualRoot;
            primitiveFallbackRoot = fallbackRoot;
            UsePrimitiveFallback(false);
        }

        public void UsePrimitiveFallback(bool enabled)
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("WorldPresentationFallbackWiringInvalid");
            syntyVisualRoot.gameObject.SetActive(!enabled);
            primitiveFallbackRoot.SetActive(enabled);
        }

        public bool ValidateWiring()
            => syntyVisualRoot != null
               && primitiveFallbackRoot != null
               && primitiveFallbackRoot.transform.parent == transform
               && !syntyVisualRoot.IsChildOf(primitiveFallbackRoot.transform);
    }

    /// <summary>Unity 선택 결과를 보여 주는 Presentation-only 상태입니다.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldSelectionEvidenceView : MonoBehaviour
    {
        [SerializeField] private string selectedStableId = string.Empty;
        [SerializeField] private TextMesh label = null!;

        public string SelectedStableId => selectedStableId;

        public void Configure(TextMesh targetLabel)
        {
            label = targetLabel;
            Apply(string.Empty);
        }

        public void Apply(string stableId)
        {
            selectedStableId = stableId?.Trim() ?? string.Empty;
            if (label != null)
                label.text = string.IsNullOrWhiteSpace(selectedStableId)
                    ? "SELECT BUSINESS OBJECT"
                    : "SELECTED\n" + selectedStableId;
        }

        public bool ValidateWiring() => label != null;
    }

    /// <summary>
    /// Scene reload 뒤 Presentation callback만 복구합니다. Command나 Simulation Tick은 실행하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldBusinessPresentationCoordinator : MonoBehaviour
    {
        [SerializeField] private 도심마트ManagerShelfView[] marketShelves =
            Array.Empty<도심마트ManagerShelfView>();
        [SerializeField] private ConceptCardDeckView conceptCards = null!;
        [SerializeField] private WorldSelectionEvidenceView selectionEvidence = null!;

        public void Configure(
            도심마트ManagerShelfView[] shelves,
            ConceptCardDeckView cards,
            WorldSelectionEvidenceView selection)
        {
            marketShelves = shelves ?? Array.Empty<도심마트ManagerShelfView>();
            conceptCards = cards;
            selectionEvidence = selection;
        }

        public void InitializePresentation()
        {
            if (!ValidateWiring())
                throw new InvalidOperationException("WorldBusinessPresentationCoordinatorWiringInvalid");
            foreach (var shelf in marketShelves)
            {
                var worldId = shelf.PresentationStableId.Substring("urban-market-shelf:".Length);
                var potato = worldId.EndsWith("potato", StringComparison.Ordinal);
                shelf.Apply(new 도심마트ShelfSurfaceItem
                {
                    StableId = new PresentationStableId(shelf.PresentationStableId),
                    ShelfWorldId = new WorldStableId(worldId),
                    PresentationRevision = "world-3:market-shelf:1",
                    DisplayBoxCount = potato ? 4 : 10,
                    QuantityText = potato ? "Potato 4/12 kg" : "Onion 10/12 kg",
                    VisualStateCode = potato ? "InboundRequired" : "Sufficient",
                    ColorCode = potato ? "Orange" : "Green",
                    IsHighlighted = potato,
                }, value => selectionEvidence.Apply(value.Value));
            }

            var wasVisible = conceptCards.VisualRoot.activeSelf;
            conceptCards.Apply(도심마트ConceptCardSampleFixture.CreateDeck());
            if (!wasVisible) conceptCards.Hide();
        }

        public bool SelectCardForTests(string stableId) => conceptCards.SelectCard(stableId);

        public bool ValidateWiring()
            => marketShelves != null && marketShelves.Length == 2
               && marketShelves.All(value => value != null && value.ValidateWiring()
                   && value.PresentationStableId.StartsWith(
                       "urban-market-shelf:", StringComparison.Ordinal))
               && conceptCards != null && conceptCards.ValidateWiring()
               && selectionEvidence != null && selectionEvidence.ValidateWiring();

        private void Awake() => InitializePresentation();
    }
}
