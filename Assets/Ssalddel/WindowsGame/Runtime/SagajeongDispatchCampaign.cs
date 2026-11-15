using System;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private static readonly int[] CampaignSpeeds = { 0, 1, 2, 4 };
        private static readonly string[] CampaignSpeedLabels = { "일시정지", "기본", "2배", "4배" };
        private readonly CampaignPolicyCard[] campaignPolicyCards =
        {
            new("chariot", "전차", "장안 권역 확대", "장안에서 새 주문을 받습니다.", "두 권역의 배차를 함께 관리합니다.", Gold,
                "전차 앞에 검고 흰 스핑크스가 있습니다.\n웨이트는 전차에서 정신적 성취를 보았습니다.",
                "두 권역의 기사와 매장이 같은 방향으로 움직이려면 무엇부터 맞출까요?",
                "통합된 정진 · 지혜와 힘을 한 방향으로"),
            new("temperance", "절제", "사가정 운영 안정", "A매장 주문에 집중합니다.", "이번 피크는 주문 1건만 받습니다.", Mint,
                "천사는 땅과 물에 한 발씩 두고\n두 잔 사이로 물을 옮깁니다.\n웨이트는 서로 다른 성질의 조화를 읽었습니다.",
                "조리와 배달 속도에 맞춰 주문을 얼마나 받을까요?"),
            new("empress", "여제", "A매장 우선 운영", "A매장 주문을 먼저 받습니다.", "B매장 주문은 게임상 2분 뒤에 열립니다.", Lilac,
                "여제 앞에 곡식이 익고, 뒤로 물이 흐릅니다.\n웨이트는 여제를 결실을 맺는 자연과 연결했습니다.",
                "매장이 꾸준히 성장하도록 무엇을 지원할까요?"),
        };

        private const string TarotCredit = "1909 라이더–웨이트–스미스 덱  ·  삽화 파멜라 콜먼 스미스";
        private CampaignPolicyCard selectedCampaignPolicy;
        private bool showCampaignArtPreview;

        private void Start()
        {
            foreach (var card in campaignPolicyCards)
                card.LoadArt();
        }

        private void DrawCampaignHeader()
        {
            var campaign = state.campaign;
            var isPolicyTurn = campaign.phase == "policy";
            Metric(new Rect(700, 14, 177, 62), "가상 시각", ClockLabel(state.now), Paper);
            var status = new Rect(888, 14, 364, 62);
            if (isPolicyTurn)
                Metric(status, $"{campaign.day}일차 마감", "다음 운영 방침 선택", Lilac);
            else if (campaign.phase == "closed")
                Metric(status, $"{campaign.day}일차 마감", "다음 영업 준비", Paper);
            else if (state.settled && state.settlement != null)
                Metric(status, $"{campaign.day}일차 · 제시간 배차",
                    $"{state.settlement.onTimeCount} / {state.orders.Length}건", Mint);
            else
            {
                var remaining = Math.Max(0, state.remainingSeconds);
                Metric(status, $"{campaign.day}일차 · 배차 마감까지",
                    $"{remaining / 60:00}:{remaining % 60:00}", remaining > 0 ? Gold : Red);
            }

            var canChangeSpeed = running && !isPolicyTurn && !NationHoldsClock;   // 전국 지도 시계 정지 정책이 켜져 있을 때만 잠근다
            Text(new Rect(700, 82, 83, 28), "진행 속도", 12, Soft, true);
            for (var index = 0; index < CampaignSpeeds.Length; index++)
            {
                var speed = CampaignSpeeds[index];
                var rect = new Rect(783 + 83 * index, 82, 76, 28);
                var selected = running && (isPolicyTurn ? speed == 0 : speed == campaignSpeed);
                Paint(rect, selected ? Mint : Tile, 6);
                Text(rect, CampaignSpeedLabels[index], 12,
                    selected ? Canvas : canChangeSpeed ? Paper : Soft, true, TextAnchor.MiddleCenter);
                if (canChangeSpeed && Hit(rect)) SetCampaignSpeed(speed);
            }
            if (isPolicyTurn)
                Text(new Rect(1115, 82, 137, 28), "방침 확정 대기", 12, Lilac, align: TextAnchor.MiddleRight);
        }

        private void DrawCampaignCloseout()
        {
            var campaign = state.campaign;
            var summary = campaign.latestSummary;
            var isOpen = campaign.phase == "open";
            var isPolicyTurn = campaign.phase == "policy";
            var hasDaySummary = !isOpen && summary != null && summary.day == campaign.day;
            Paint(new Rect(45, 442, 258, 208), Canvas, 10);
            Text(new Rect(56, 450, 235, 25),
                isOpen ? $"{campaign.day}일차 · 피크 정산" : $"{campaign.day}일차 · 영업 마감",
                15, Mint, true);

            if (hasDaySummary)
            {
                Text(new Rect(56, 484, 235, 23), $"정산 완료 {summary.completed} / {summary.totalOrders}건", 14, Paper);
                Text(new Rect(56, 510, 235, 23), $"배차 대기 {summary.waiting}건  ·  배송 진행 {summary.delivering}건", 12, Soft);
                MoneyRow(536, "가상 차액", summary.balance, Mint);
            }
            else if (isOpen && state.settlement != null)
            {
                MoneyRow(483, "플랫폼 수입", state.settlement.revenue, Soft);
                MoneyRow(508, "기사 보수", state.settlement.driverPay, Soft);
                MoneyRow(536, "가상 차액", state.settlement.balance, Mint);
            }

            if (isPolicyTurn)
                Text(new Rect(56, 566, 235, 60), "오른쪽에서 다음 운영 방침을 확정하면 시간이 이어집니다.", 12, Lilac, wrap: true);
            else if (campaign.canFastForward)
            {
                var action = isOpen ? "오늘 영업 마감까지" : "다음 영업 시작까지";
                Text(new Rect(56, 568, 235, 30), "시간을 계속 흘려보낼 수도 있습니다.", 12, Soft);
                if (Action(new Rect(56, 608, 235, 32), action, Mint))
                    AdvanceCampaignBoundary();
            }
            else
                Text(new Rect(56, 568, 235, 59), "진행 중인 업무가 지도에서 계속됩니다.", 12, Soft, wrap: true);
        }

        private void DrawCampaignPolicy()
        {
            if (!CampaignPolicyOpen)
            {
                selectedCampaignPolicy = null;
                showCampaignArtPreview = false;
                return;
            }

            var panel = new Rect(532, 129, 696, 533);
            Paint(panel, Rim, 13);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 12);
            if (showCampaignArtPreview && selectedCampaignPolicy is { } preview)
            {
                DrawCampaignArtPreview(preview);
                return;
            }

            Text(new Rect(550, 140, 660, 22), $"사가정  ·  {state.campaign.day}일차 결산  ·  시계 정지",
                13, Mint, true, TextAnchor.MiddleCenter);
            Text(new Rect(550, 165, 660, 36), "다음 운영 방침",
                27, Paper, true, TextAnchor.MiddleCenter);

            for (var index = 0; index < campaignPolicyCards.Length; index++)
                DrawCampaignPolicyCard(new Rect(550 + 224 * index, 211, 212, 287), campaignPolicyCards[index]);

            var selected = selectedCampaignPolicy;
            var detail = new Rect(550, 508, 660, 63);
            Paint(detail, Panel, 8);
            if (selected == null)
                Text(detail, "카드를 골라 다음 피크에서 얻는 것과 감수할 점을 살펴보세요.",
                    14, Soft, align: TextAnchor.MiddleCenter);
            else
            {
                Text(new Rect(567, 515, 626, 22), $"얻는 것  ·  {selected.Benefit}", 14, Paper, true);
                Text(new Rect(567, 539, 626, 22), $"감수할 점  ·  {selected.Tradeoff}", 14, Soft);
            }

            if (selected?.Art != null && pending == null &&
                Action(new Rect(550, 582, 165, 38), "그림 크게 보기", Paper))
                ToggleCampaignArtPreview();

            var confirm = new Rect(929, 582, 281, 38);
            if (selected != null && pending == null)
            {
                if (Action(confirm, "다음 운영에 적용", Mint))
                    ApplyCampaignPolicy(selected.Id);
            }
            else
            {
                Paint(confirm, Tile, 8);
                Text(confirm, pending != null ? "방침 적용 중" : "카드를 먼저 고르세요", 14, Soft,
                    align: TextAnchor.MiddleCenter);
            }
            Text(new Rect(550, 627, 660, 22), TarotCredit, 11, Soft, align: TextAnchor.MiddleCenter);
        }

        private void DrawCampaignPolicyCard(Rect rect, CampaignPolicyCard card)
        {
            var picked = card == selectedCampaignPolicy;
            Paint(rect, picked ? card.Accent : Rim, 9);
            var inset = picked ? 2 : 1;
            Paint(new Rect(rect.x + inset, rect.y + inset, rect.width - 2 * inset, rect.height - 2 * inset),
                picked ? Tile : Panel, 8);

            DrawCampaignArt(new Rect(rect.center.x - 75, rect.y + 12, 150, 221), card);
            Text(new Rect(rect.x + 8, rect.y + 234, rect.width - 16, 27), card.Name,
                19, card.Accent, true, TextAnchor.MiddleCenter);
            Text(new Rect(rect.x + 8, rect.y + 259, rect.width - 16, 21), card.Subtitle,
                13, Paper, true, TextAnchor.MiddleCenter);
            if (picked)
            {
                var badge = new Rect(rect.xMax - 56, rect.y + 238, 47, 18);
                Paint(badge, Tile, 4);
                Text(badge, "선택됨", 10, card.Accent, true, TextAnchor.MiddleCenter);
            }
            if (pending == null && Hit(rect)) selectedCampaignPolicy = card;
        }

        private void ToggleCampaignArtPreview()
        {
            if (CampaignPolicyOpen && pending == null && selectedCampaignPolicy?.Art != null)
                showCampaignArtPreview = !showCampaignArtPreview;
        }

        private void DrawCampaignArtPreview(CampaignPolicyCard card)
        {
            Text(new Rect(556, 146, 406, 28), "타로 삽화와 해설", 17, Soft, true);
            DrawCampaignArt(new Rect(552, 184, 256, 444), card);
            Text(new Rect(555, 630, 259, 20), "1909년판 · 삽화 파멜라 콜먼 스미스", 10, Soft);
            Text(new Rect(837, 151, 355, 48), card.Name, 33, card.Accent, true);
            Text(new Rect(838, 201, 354, 30), card.Subtitle, 17, Paper, true);

            Paint(new Rect(837, 243, 355, 125), Panel, 8);
            Text(new Rect(852, 252, 324, 23), "카드 해설", 14, Paper, true);
            Text(new Rect(852, 280, 324, 55), card.Reading, 14, Paper, wrap: true);
            if (!string.IsNullOrEmpty(card.LearningNote))
                Text(new Rect(852, 340, 324, 21), "기존 학습 메모  ·  " + card.LearningNote, 11, Gold);

            Text(new Rect(838, 379, 354, 22), "운영에 던지는 질문", 14, card.Accent, true);
            Text(new Rect(838, 405, 354, 39), card.Reflection, 15, Paper, wrap: true);

            Paint(new Rect(837, 454, 355, 109), Panel, 8);
            Text(new Rect(852, 462, 324, 22), "게임에서의 효과", 14, Paper, true);
            Text(new Rect(852, 487, 68, 27), "얻는 것", 12, Mint, true);
            Text(new Rect(923, 486, 253, 28), card.Benefit, 13, Paper, wrap: true);
            Text(new Rect(852, 520, 68, 29), "감수할 점", 12, Gold, true);
            Text(new Rect(923, 518, 253, 35), card.Tradeoff, 13, Paper, wrap: true);
            Text(new Rect(838, 572, 354, 23), "해설 참고 · A. E. 웨이트의 타로 원전 제2부", 11, Soft);

            if (Action(new Rect(838, 607, 166, 36), "카드 목록으로", Paper))
                ToggleCampaignArtPreview();
        }

        private void DrawCampaignArt(Rect rect, CampaignPolicyCard card)
        {
            if (card.Art != null)
                GUI.DrawTexture(rect, card.Art, ScaleMode.ScaleToFit, alphaBlend: false);
            else
                Text(rect, "삽화를 불러오지 못했습니다.", 12, Soft, align: TextAnchor.MiddleCenter, wrap: true);
        }

        private sealed class CampaignPolicyCard
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly string Subtitle;
            internal readonly string Benefit;
            internal readonly string Tradeoff;
            internal readonly Color Accent;
            internal readonly string Reading;
            internal readonly string Reflection;
            internal readonly string LearningNote;
            internal Texture2D Art { get; private set; }

            internal void LoadArt() =>
                Art = Resources.Load<Texture2D>("SagajeongDispatch/tarot/" + Id);

            internal CampaignPolicyCard(string id, string name, string subtitle, string benefit, string tradeoff,
                Color accent, string reading, string reflection, string learningNote = null)
            {
                Id = id;
                Name = name;
                Subtitle = subtitle;
                Benefit = benefit;
                Tradeoff = tradeoff;
                Accent = accent;
                Reading = reading;
                Reflection = reflection;
                LearningNote = learningNote;
            }
        }
    }
}
