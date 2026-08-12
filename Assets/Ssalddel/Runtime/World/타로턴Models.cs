using System;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 턴마감타로OrientationCodes
    {
        public const string Upright = "Upright";
        public const string Reversed = "Reversed";
    }

    public sealed class 턴마감타로OfferData
    {
        public string OfferStableId { get; set; } = string.Empty;
        public int OfferSlotNumber { get; set; }
        public string CardCopyStableId { get; set; } = string.Empty;
        public string OrientationCode { get; set; } = string.Empty;
        public 턴마감CardData Card { get; set; } = new 턴마감CardData();
    }

    public sealed class 턴마감타로DrawData
    {
        public string DrawStableId { get; set; } = string.Empty;
        public string DeckStableId { get; set; } = string.Empty;
        public string DeckRevision { get; set; } = string.Empty;
        public string DrawRuleRevision { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string TurnHistoryHash { get; set; } = string.Empty;
        public 턴마감타로OfferData[] Offers { get; set; } = Array.Empty<턴마감타로OfferData>();

        public bool IsAvailable => !string.IsNullOrWhiteSpace(DrawStableId) && Offers.Length == 3;
    }

    public sealed class 턴마감타로SelectionData
    {
        public string OfferStableId { get; set; } = string.Empty;
        public string CardStableId { get; set; } = string.Empty;
        public string OrientationCode { get; set; } = string.Empty;
    }

    public sealed class 타로객체반응Data
    {
        public string ObjectStableId { get; set; } = string.Empty;
        public string PlacementStableId { get; set; } = string.Empty;
        public string ReactionStateCode { get; set; } = string.Empty;
        public bool CanHighlightInWorld { get; set; }
        public string KoreanSummary { get; set; } = string.Empty;
        public string[] StateSourceStableIds { get; set; } = Array.Empty<string>();
        public string[] BlockReasonCodes { get; set; } = Array.Empty<string>();
    }

    public sealed class 타로Card객체반응Data
    {
        public string OfferStableId { get; set; } = string.Empty;
        public string CardStableId { get; set; } = string.Empty;
        public string OrientationCode { get; set; } = string.Empty;
        public 타로객체반응Data[] ObjectReactions { get; set; } = Array.Empty<타로객체반응Data>();
        public string[] HighlightObjectStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 타로객체반응PreviewData
    {
        public string PreviewStableId { get; set; } = string.Empty;
        public long BaseRevision { get; set; }
        public int TurnNumber { get; set; }
        public string DrawStableId { get; set; } = string.Empty;
        public string ObjectCatalogRevision { get; set; } = string.Empty;
        public bool IsCandidateOnly { get; set; }
        public bool DoesNotMutateSession { get; set; }
        public 타로Card객체반응Data[] CardReactions { get; set; }
            = Array.Empty<타로Card객체반응Data>();

        public 타로Card객체반응Data Find(string offerStableId)
            => CardReactions.SingleOrDefault(value => value.OfferStableId == offerStableId)
                ?? throw new InvalidOperationException("TarotObjectReactionOfferMissing");
    }

    public static class 턴마감타로Fixture
    {
        private static readonly 턴마감타로OfferData[] Offers =
        {
            Offer(1, 턴마감CardStableIds.Empress, "여제", "생산과 돌봄의 확장을 살핀다.",
                "EmpressProductionGrowth", 턴마감타로OrientationCodes.Upright),
            Offer(2, 턴마감CardStableIds.TarotChariot, "전차", "운송 속도와 비용·피로를 함께 살핀다.",
                "ChariotFastTransport", 턴마감타로OrientationCodes.Upright),
            Offer(3, 턴마감CardStableIds.Temperance, "절제", "생산과 재고 흐름의 균형을 살핀다.",
                "TemperanceFlowBalance", 턴마감타로OrientationCodes.Reversed),
        };

        public static 턴마감타로DrawData CreateDraw()
            => new 턴마감타로DrawData
            {
                DrawStableId = "tarot-draw:fixture.turn-13",
                DeckStableId = "tarot-deck:starter-12",
                DeckRevision = "tarot-deck:starter-12.r1",
                DrawRuleRevision = "tarot-draw-rule:r1",
                TurnNumber = 13,
                TurnHistoryHash = "fixture-history-r1",
                Offers = Offers.Select(Clone).ToArray(),
            };

        public static 턴마감타로OfferData FindOffer(턴마감타로SelectionData selection)
        {
            var offer = Offers.SingleOrDefault(value =>
                value.OfferStableId == selection.OfferStableId
                && value.Card.CardStableId == selection.CardStableId
                && value.OrientationCode == selection.OrientationCode)
                ?? throw new InvalidOperationException("TarotOfferUnavailable");
            return Clone(offer);
        }

        public static 타로객체반응PreviewData CreateObjectReactionPreview(
            long revision, string drawStableId)
        {
            var draw = CreateDraw();
            if (draw.DrawStableId != drawStableId)
                throw new InvalidOperationException("TarotDrawMismatch");
            return new 타로객체반응PreviewData
            {
                PreviewStableId = "tarot-object-reaction-preview:fixture.turn-13",
                BaseRevision = revision,
                TurnNumber = 13,
                DrawStableId = drawStableId,
                ObjectCatalogRevision = "integrated-seedbed:o6.r1",
                IsCandidateOnly = true,
                DoesNotMutateSession = true,
                CardReactions = draw.Offers.Select(value => new 타로Card객체반응Data
                {
                    OfferStableId = value.OfferStableId,
                    CardStableId = value.Card.CardStableId,
                    OrientationCode = value.OrientationCode,
                    HighlightObjectStableIds = value.Card.CardStableId == 턴마감CardStableIds.TarotChariot
                        ? Array.Empty<string>()
                        : new[] { "seedbed-object:city.urban-market-building.a" },
                }).ToArray(),
            };
        }

        private static 턴마감타로OfferData Offer(
            int slot, string cardStableId, string title, string summary,
            string effectCode, string orientation)
        {
            var copyId = "tarot-deck-card:starter-12.slot-" + slot;
            var offerId = "tarot-offer:fixture.turn-13.slot-" + slot;
            return new 턴마감타로OfferData
            {
                OfferStableId = offerId,
                OfferSlotNumber = slot,
                CardCopyStableId = copyId,
                OrientationCode = orientation,
                Card = new 턴마감CardData
                {
                    CardStableId = cardStableId,
                    CardRevision = "tarot-card-gameplay:r1",
                    Title = title,
                    Summary = summary,
                    EffectCode = effectCode,
                    TargetStatCode = "SimulationRuleModifier",
                    SourceStableId = "source:tarot-gameplay.fixture-r1",
                    CardKindCode = "Tarot",
                },
            };
        }

        private static 턴마감타로OfferData Clone(턴마감타로OfferData source)
            => new 턴마감타로OfferData
            {
                OfferStableId = source.OfferStableId,
                OfferSlotNumber = source.OfferSlotNumber,
                CardCopyStableId = source.CardCopyStableId,
                OrientationCode = source.OrientationCode,
                Card = new 턴마감CardData
                {
                    CardStableId = source.Card.CardStableId,
                    CardRevision = source.Card.CardRevision,
                    Title = source.Card.Title,
                    Summary = source.Card.Summary,
                    EffectCode = source.Card.EffectCode,
                    TargetStatCode = source.Card.TargetStatCode,
                    SourceStableId = source.Card.SourceStableId,
                    CardKindCode = source.Card.CardKindCode,
                },
            };
    }
}
