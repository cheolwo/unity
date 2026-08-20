using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 턴마감CardStableIds
    {
        public const string Empress = "tarot:major.empress";
        public const string TarotChariot = "tarot:major.chariot";
        public const string Justice = "tarot:major.justice";
        public const string Temperance = "tarot:major.temperance";
        public const string Fool = "learning:hongik.fool.beginner-mind";
        public const string Chariot = "learning:hongik.chariot.integrated-progress";
        public const string SeoulCulture = "culture:kr-seoul.living-culture-question.2026";
    }

    public sealed class 턴마감CardData
    {
        public string CardStableId { get; set; } = string.Empty;
        public string CardRevision { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string EffectCode { get; set; } = string.Empty;
        public string TargetStatCode { get; set; } = string.Empty;
        public int StatDelta { get; set; }
        public string SourceStableId { get; set; } = string.Empty;
        public string CardKindCode { get; set; } = string.Empty;
        public string RegionKey { get; set; } = string.Empty;
        public DateTimeOffset? AvailableFromGameDate { get; set; }
        public DateTimeOffset? AvailableThroughGameDate { get; set; }
        public string CalendarRevision { get; set; } = string.Empty;
        public string EffectRuleRevision { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public DateTimeOffset? EvidenceCheckedAtUtc { get; set; }
    }

    public sealed class 턴마감ContextData
    {
        public string SessionStableId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string GameDateLabel { get; set; } = string.Empty;
        public long Revision { get; set; }
        public int PendingTaskCount { get; set; }
        public bool CanCloseTurn { get; set; }
        public 턴마감CardData[] AvailableCards { get; set; } = Array.Empty<턴마감CardData>();
        public 턴마감타로DrawData TarotDraw { get; set; } = new 턴마감타로DrawData();
        public 타로ContextStateData TarotContext { get; set; } = new 타로ContextStateData();
    }

    public sealed class 타로FrameData
    {
        public string FrameStableId { get; set; } = string.Empty;
        public string CardStableId { get; set; } = string.Empty;
        public string CardCopyStableId { get; set; } = string.Empty;
        public string OrientationCode { get; set; } = string.Empty;
        public string FrameScopeCode { get; set; } = string.Empty;
        public string[] ThemeCodes { get; set; } = Array.Empty<string>();
    }

    public sealed class 타로CardRelationData
    {
        public string SourceFrameStableId { get; set; } = string.Empty;
        public string TargetCardFamilyCode { get; set; } = string.Empty;
        public string TargetCardStableId { get; set; } = string.Empty;
        public string TargetCardCopyStableId { get; set; } = string.Empty;
        public string RelationCode { get; set; } = string.Empty;
        public bool ChangesAvailability { get; set; }
    }

    public sealed class 타로ContextStateData
    {
        public long Revision { get; set; }
        public 타로FrameData[] ActiveFrames { get; set; } = Array.Empty<타로FrameData>();
        public 타로CardRelationData[] Relations { get; set; } = Array.Empty<타로CardRelationData>();
        public string ContextStateHashSha256 { get; set; } = string.Empty;
    }

    public sealed class 턴마감PreviewData
    {
        public string PreviewStableId { get; set; } = string.Empty;
        public long BaseRevision { get; set; }
        public int ClosingTurnNumber { get; set; }
        public int NextTurnNumber { get; set; }
        public string NextGameDateLabel { get; set; } = string.Empty;
        public int PendingTaskCount { get; set; }
        public 턴마감CardData[] SelectedCards { get; set; } = Array.Empty<턴마감CardData>();
    }

    public sealed class 턴마감ResultData
    {
        public string SessionStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public long WorldTick { get; set; }
        public int ActiveTurnNumber { get; set; }
        public string ActiveCardStableId { get; set; } = string.Empty;
        public string ActiveEffectCode { get; set; } = string.Empty;
        public SimulationWorldShellSnapshot WorldSnapshot { get; set; } = null!;
    }

    public interface I턴마감AuthorityClient
    {
        Task<턴마감ContextData> GetContextAsync(string sessionStableId, CancellationToken cancellationToken);
        Task<턴마감PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision, string selectedCardStableId,
            CancellationToken cancellationToken);
        Task<턴마감ResultData> ConfirmAsync(
            string sessionStableId, string commandId, long expectedRevision,
            string selectedCardStableId, CancellationToken cancellationToken);
    }

    public interface I타로턴마감AuthorityClient : I턴마감AuthorityClient
    {
        Task<타로객체반응PreviewData> Preview타로객체반응Async(
            string sessionStableId, long expectedRevision, string drawStableId,
            CancellationToken cancellationToken);
        Task<턴마감PreviewData> Preview타로Async(
            string sessionStableId, long expectedRevision, 턴마감타로SelectionData selection,
            CancellationToken cancellationToken);
        Task<턴마감ResultData> Confirm타로Async(
            string sessionStableId, string commandId, long expectedRevision,
            턴마감타로SelectionData selection, CancellationToken cancellationToken);
    }

    public sealed class 턴마감FixtureAuthorityClient : I타로턴마감AuthorityClient
    {
        private static readonly 턴마감CardData[] Cards =
        {
            new 턴마감CardData
            {
                CardStableId = 턴마감CardStableIds.Fool,
                CardRevision = "evening-hakdang.fixture-r1",
                Title = "0. 바보 · 모를 뿐",
                Summary = "모름을 인정하고 다음 경영일을 초심으로 바라본다.",
                EffectCode = "BeginnerMind",
                TargetStatCode = "Awareness",
                StatDelta = 1,
                SourceStableId = "source:fixture.evening-hakdang.fool.beginner-mind",
                CardKindCode = "Philosophy",
            },
            new 턴마감CardData
            {
                CardStableId = 턴마감CardStableIds.Chariot,
                CardRevision = "evening-hakdang.fixture-r1",
                Title = "7. 전차 · 통합된 정진",
                Summary = "힘과 지혜를 함께 써서 다음 경영일의 실천을 잇는다.",
                EffectCode = "IntegratedProgress",
                TargetStatCode = "Resolve",
                StatDelta = 1,
                SourceStableId = "source:fixture.evening-hakdang.chariot.integrated-progress",
                CardKindCode = "Philosophy",
            },
            new 턴마감CardData
            {
                CardStableId = 턴마감CardStableIds.SeoulCulture,
                CardRevision = "culture-card.fixture-r1",
                Title = "서울 생활문화 질문",
                Summary = "지역 생활을 하나의 대표 이미지로 단정하지 않고 주민의 현재 경험과 공식 원천을 함께 확인한다.",
                EffectCode = "LocalContextAwareness",
                TargetStatCode = "CommunityInsight",
                StatDelta = 1,
                SourceStableId = "source:kr-regional-culture-promotion-agency",
                CardKindCode = "Culture",
                RegionKey = "kr-seoul",
                AvailableFromGameDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                AvailableThroughGameDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
                CalendarRevision = "simulation-culture-calendar:kr-seoul:2026.r1",
                EffectRuleRevision = "culture-local-context-awareness:r1",
                SourceUrl = "https://www.mcst.go.kr/site/s_data/corpNaru/corpView.jsp?pSeq=615",
                EvidenceCheckedAtUtc = new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero),
            },
        };

        public Task<턴마감ContextData> GetContextAsync(
            string sessionStableId, CancellationToken cancellationToken)
            => Task.FromResult(new 턴마감ContextData
            {
                SessionStableId = sessionStableId,
                TurnNumber = 13,
                GameDateLabel = "Year 1 · 04-12",
                Revision = 12,
                PendingTaskCount = 2,
                CanCloseTurn = true,
                AvailableCards = Cards.Select(Clone).ToArray(),
                TarotDraw = 턴마감타로Fixture.CreateDraw(),
            });

        public Task<턴마감PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision, string selectedCardStableId,
            CancellationToken cancellationToken)
            => Task.FromResult(new 턴마감PreviewData
            {
                PreviewStableId = "turn-closing:" + sessionStableId + ":13",
                BaseRevision = expectedRevision,
                ClosingTurnNumber = 13,
                NextTurnNumber = 14,
                NextGameDateLabel = "Year 1 · 04-13",
                PendingTaskCount = 2,
                SelectedCards = string.IsNullOrEmpty(selectedCardStableId)
                    ? Array.Empty<턴마감CardData>()
                    : new[] { Clone(Find(selectedCardStableId)) },
            });

        public Task<턴마감ResultData> ConfirmAsync(
            string sessionStableId, string commandId, long expectedRevision,
            string selectedCardStableId, CancellationToken cancellationToken)
        {
            var card = string.IsNullOrEmpty(selectedCardStableId) ? null : Find(selectedCardStableId);
            return Task.FromResult(new 턴마감ResultData
            {
                SessionStableId = sessionStableId,
                Revision = expectedRevision + 1,
                WorldTick = 13,
                ActiveTurnNumber = 14,
                ActiveCardStableId = card?.CardStableId ?? string.Empty,
                ActiveEffectCode = card?.EffectCode ?? string.Empty,
                WorldSnapshot = new SimulationWorldShellSnapshot(
                    sessionStableId, expectedRevision + 1, 13, "Year 1 · 04-13",
                    12500m, 18m, 6m, 420m, 980m, 12.94m, 1,
                    "SimulationFixtureAuthority",
                    new[]
                    {
                        new SimulationWorldSettlementNode(
                            SimulationWorldShellFixture.SettlementStableId,
                            new[]
                            {
                                District("district:farm", "harvest-lot:potato-001"),
                                District("district:town"), District("district:market"),
                                District("district:storage"), District("district:logistics"),
                                District("district:residential"), District("district:garrison"),
                                District("district:gate"),
                            }),
                    }),
            });
        }

        public Task<타로객체반응PreviewData> Preview타로객체반응Async(
            string sessionStableId, long expectedRevision, string drawStableId,
            CancellationToken cancellationToken)
            => Task.FromResult(턴마감타로Fixture.CreateObjectReactionPreview(
                expectedRevision, drawStableId));

        public Task<턴마감PreviewData> Preview타로Async(
            string sessionStableId, long expectedRevision, 턴마감타로SelectionData selection,
            CancellationToken cancellationToken)
        {
            var offer = 턴마감타로Fixture.FindOffer(selection);
            return Task.FromResult(new 턴마감PreviewData
            {
                PreviewStableId = "turn-closing:" + sessionStableId + ":13",
                BaseRevision = expectedRevision,
                ClosingTurnNumber = 13,
                NextTurnNumber = 14,
                NextGameDateLabel = "Year 1 · 04-13",
                PendingTaskCount = 2,
                SelectedCards = new[] { offer.Card },
            });
        }

        public Task<턴마감ResultData> Confirm타로Async(
            string sessionStableId, string commandId, long expectedRevision,
            턴마감타로SelectionData selection, CancellationToken cancellationToken)
        {
            var offer = 턴마감타로Fixture.FindOffer(selection);
            return Task.FromResult(new 턴마감ResultData
            {
                SessionStableId = sessionStableId,
                Revision = expectedRevision + 1,
                WorldTick = 13,
                ActiveTurnNumber = 14,
                ActiveCardStableId = offer.Card.CardStableId,
                ActiveEffectCode = offer.Card.EffectCode,
                WorldSnapshot = new SimulationWorldShellSnapshot(
                    sessionStableId, expectedRevision + 1, 13, "Year 1 · 04-13",
                    12500m, 18m, 6m, 420m, 980m, 12.94m, 1,
                    "SimulationFixtureAuthority",
                    new[]
                    {
                        new SimulationWorldSettlementNode(
                            SimulationWorldShellFixture.SettlementStableId,
                            new[]
                            {
                                District("district:farm", "harvest-lot:potato-001"),
                                District("district:town"), District("district:market"),
                                District("district:storage"), District("district:logistics"),
                                District("district:residential"), District("district:garrison"),
                                District("district:gate"),
                            }),
                    }),
            });
        }

        private static 턴마감CardData Find(string stableId)
        {
            var card = Cards.SingleOrDefault(value => value.CardStableId == stableId)
                ?? throw new InvalidOperationException("TurnClosingCardUnavailable");
            ValidateCard(card);
            return card;
        }
        public static void ValidateCard(턴마감CardData card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.CardStableId)
                || string.IsNullOrWhiteSpace(card.CardRevision)
                || string.IsNullOrWhiteSpace(card.CardKindCode)
                || string.IsNullOrWhiteSpace(card.SourceStableId))
                throw new InvalidOperationException("TurnClosingCardInvalid");
            if (card.CardKindCode == "Culture"
                && (string.IsNullOrWhiteSpace(card.RegionKey)
                    || !card.AvailableFromGameDate.HasValue
                    || !card.AvailableThroughGameDate.HasValue
                    || card.AvailableFromGameDate > card.AvailableThroughGameDate
                    || string.IsNullOrWhiteSpace(card.CalendarRevision)
                    || string.IsNullOrWhiteSpace(card.EffectRuleRevision)
                    || !Uri.TryCreate(card.SourceUrl, UriKind.Absolute, out var sourceUrl)
                    || sourceUrl.Scheme != Uri.UriSchemeHttps
                    || !card.EvidenceCheckedAtUtc.HasValue))
                throw new InvalidOperationException("TurnClosingCultureCardProvenanceInvalid");
        }
        private static 턴마감CardData Clone(턴마감CardData source)
            => new 턴마감CardData
            {
                CardStableId = source.CardStableId, CardRevision = source.CardRevision,
                Title = source.Title,
                Summary = source.Summary, EffectCode = source.EffectCode,
                TargetStatCode = source.TargetStatCode, StatDelta = source.StatDelta,
                SourceStableId = source.SourceStableId,
                CardKindCode = source.CardKindCode, RegionKey = source.RegionKey,
                AvailableFromGameDate = source.AvailableFromGameDate,
                AvailableThroughGameDate = source.AvailableThroughGameDate,
                CalendarRevision = source.CalendarRevision,
                EffectRuleRevision = source.EffectRuleRevision,
                SourceUrl = source.SourceUrl,
                EvidenceCheckedAtUtc = source.EvidenceCheckedAtUtc,
            };
        private static SimulationWorldDistrictNode District(string id, params string[] objects)
            => new SimulationWorldDistrictNode(id, objects);
    }
}
