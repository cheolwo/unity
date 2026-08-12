using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 턴카드모판Code
    {
        public const string 철학학당 = "PhilosophyAcademy";
        public const string 지역문화 = "RegionalCulture";
    }

    public static class 턴카드승격상태Code
    {
        public const string 통과 = "Passed";
        public const string Fixture검증 = "FixtureValidated";
        public const string 차단 = "Blocked";
        public const string 대기 = "Pending";
    }

    public sealed class 턴카드승격GateData
    {
        public 턴카드승격GateData(string code, string label, string statusCode, string note)
        {
            Code = code ?? string.Empty;
            Label = label ?? string.Empty;
            StatusCode = statusCode ?? string.Empty;
            Note = note ?? string.Empty;
        }

        public string Code { get; }
        public string Label { get; }
        public string StatusCode { get; }
        public string Note { get; }
    }

    public sealed class 턴카드모판EntryData
    {
        public 턴카드모판EntryData(
            string cardStableId,
            string nurseryCode,
            string title,
            string kindLabel,
            string stageSummary,
            string sourceRevision,
            string effectRuleRevision,
            string knownBoundary,
            string unknownBoundary,
            string blockedReason,
            턴카드승격GateData[] gates)
        {
            CardStableId = cardStableId ?? string.Empty;
            NurseryCode = nurseryCode ?? string.Empty;
            Title = title ?? string.Empty;
            KindLabel = kindLabel ?? string.Empty;
            StageSummary = stageSummary ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            EffectRuleRevision = effectRuleRevision ?? string.Empty;
            KnownBoundary = knownBoundary ?? string.Empty;
            UnknownBoundary = unknownBoundary ?? string.Empty;
            BlockedReason = blockedReason ?? string.Empty;
            Gates = gates ?? Array.Empty<턴카드승격GateData>();
        }

        public string CardStableId { get; }
        public string NurseryCode { get; }
        public string Title { get; }
        public string KindLabel { get; }
        public string StageSummary { get; }
        public string SourceRevision { get; }
        public string EffectRuleRevision { get; }
        public string KnownBoundary { get; }
        public string UnknownBoundary { get; }
        public string BlockedReason { get; }
        public 턴카드승격GateData[] Gates { get; }
        public bool 게시완료 => Gates.Any(value =>
            value.Code == "C5" && value.StatusCode == 턴카드승격상태Code.통과);
    }

    public sealed class 턴카드모판CatalogData
    {
        public 턴카드모판CatalogData(턴카드모판EntryData[] entries)
        {
            Entries = entries ?? Array.Empty<턴카드모판EntryData>();
            Validate();
        }

        public 턴카드모판EntryData[] Entries { get; }

        public IReadOnlyList<턴카드모판EntryData> FindByNursery(string nurseryCode)
            => Entries.Where(value => value.NurseryCode == nurseryCode).ToArray();

        public 턴카드모판EntryData Find(string cardStableId)
            => Entries.Single(value => value.CardStableId == cardStableId);

        private void Validate()
        {
            if (Entries.Length == 0)
                throw new InvalidOperationException("TurnCardSeedbedCatalogEmpty");
            if (Entries.Any(value => string.IsNullOrWhiteSpace(value.CardStableId)
                || string.IsNullOrWhiteSpace(value.Title)
                || value.Gates.Length != 7))
                throw new InvalidOperationException("TurnCardSeedbedEntryInvalid");
            if (Entries.Select(value => value.CardStableId).Distinct().Count() != Entries.Length)
                throw new InvalidOperationException("TurnCardSeedbedStableIdDuplicate");
            if (Entries.Any(value => value.NurseryCode != 턴카드모판Code.철학학당
                && value.NurseryCode != 턴카드모판Code.지역문화))
                throw new InvalidOperationException("TurnCardSeedbedNurseryUnknown");
            if (Entries.Any(value => value.Gates.Select(gate => gate.Code).Distinct().Count() != 7))
                throw new InvalidOperationException("TurnCardSeedbedGateDuplicate");
        }

        public static 턴카드모판CatalogData CreateCurrentFixture()
        {
            return new 턴카드모판CatalogData(new[]
            {
                Philosophy(
                    턴마감CardStableIds.Fool,
                    "0. 바보 · 모를 뿐",
                    "BeginnerMind",
                    "원음 대조와 사람 승인 snapshot이 없어 실제 게시를 차단합니다."),
                Philosophy(
                    턴마감CardStableIds.Chariot,
                    "7. 전차 · 통합 정진",
                    "IntegratedProgress",
                    "원음 대조와 사람 승인 snapshot이 없어 실제 게시를 차단합니다."),
                new 턴카드모판EntryData(
                    턴마감CardStableIds.SeoulCulture,
                    턴카드모판Code.지역문화,
                    "서울 생활문화 질문",
                    "지역문화 Fixture 후보",
                    "C1 통과 · C3/C4 Fixture 검증 · C2/C5 차단",
                    "simulation-culture-calendar:kr-seoul:2026.r1",
                    "culture-local-context-awareness:r1",
                    "서울 지역·2026년 범위와 공식 관계기관 출처, 질문형 카드임을 압니다.",
                    "특정 행사·계절 문화의 사실, 주민 전체의 대표성과 실제 운영 상태는 알 수 없습니다.",
                    "행사별 원문 대조와 사람 검수 publication이 없어 실제 문화 덱 게시를 차단합니다.",
                    Gates("공식 출처 메타데이터 확인", "행사별 원문·사람 검수 필요",
                        "LocalContextAwareness Fixture 규칙 검증", "WorldShell Fixture 화면 검증"))
            });
        }

        private static 턴카드모판EntryData Philosophy(
            string stableId, string title, string effectCode, string blockedReason)
        {
            return new 턴카드모판EntryData(
                stableId,
                턴카드모판Code.철학학당,
                title,
                "철학·학당 Fixture 후보",
                "C1 통과 · C3/C4 Fixture 검증 · C2/C5 차단",
                "hongik-unity-learning-card-publication.v1 · 승인 snapshot 0건",
                "evening-hakdang.fixture-r1 / " + effectCode,
                "Preview 설명과 다음 턴 Fixture 효과의 허용 경계를 압니다.",
                "승인된 원문 의미와 실제 학당 게시 내용은 아직 알 수 없습니다.",
                blockedReason,
                Gates("게시 schema·출처 계보 확인", "원음 대조·사람 승인 필요",
                    effectCode + " Fixture 규칙 검증", "저녁 학당·WorldShell Fixture 화면 검증"));
        }

        private static 턴카드승격GateData[] Gates(
            string c1, string c2, string c3, string c4)
        {
            return new[]
            {
                new 턴카드승격GateData("C0", "카드 씨앗", 턴카드승격상태Code.통과,
                    "질문·분야·금지 효과 기록"),
                new 턴카드승격GateData("C1", "출처 메타데이터", 턴카드승격상태Code.통과, c1),
                new 턴카드승격GateData("C2", "내용·사람 검수", 턴카드승격상태Code.차단, c2),
                new 턴카드승격GateData("C3", "효과 규칙", 턴카드승격상태Code.Fixture검증, c3),
                new 턴카드승격GateData("C4", "모판 화면", 턴카드승격상태Code.Fixture검증, c4),
                new 턴카드승격GateData("C5", "게시 snapshot", 턴카드승격상태Code.차단,
                    "immutable 승인 publication 없음"),
                new 턴카드승격GateData("C6", "게임 덱 이식", 턴카드승격상태Code.Fixture검증,
                    "개발용 Simulation Fixture 덱에서만 검증")
            };
        }
    }
}
