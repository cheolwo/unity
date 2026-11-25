using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // r187 운영 회차의 화면 (첫 검토용 구현 2차, 2026-11-14). 규칙은 운영회차Policy.cs에 있다.
    //  - 왼쪽 아래(「운영 현황」·「오늘 결산」 단추 위)에 회차 안내 한 칸을 둔다: 지금 국면, 할 일, 단추 하나.
    //  - 준비에서 배차가 받아들여진 주문이 그 회차의 대상이 된다. 기존의 기사 고르기와 배차를 그대로 쓴다.
    //  - 「회차 마감」은 대상 주문이 실제로 전달됐을 때만 눌린다. 「다음 운영 준비」는 회차 번호만 올린다.
    //  - 마감과 다음 준비는 엔진에 아무 명령도 보내지 않는다. 시계·날짜·돈·재고·원장이 그대로다.
    //    새 게임을 여는 start/campaign-start와 다음 영업일로 넘기는 AdvanceCampaignBoundary를 부르지 않는다.
    //  - 「운영 시작」만 기존의 운영 시작(BeginRound)을 부른다. 이것도 새 게임을 열지 않는다.
    //  - 회차 자체는 기존 세션 저장에서 복원한다. 결과를 펼쳐 읽는 상태는 이 화면 안에서만 쓴다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect 회차안내칸 = new(22, 542, 336, 76);
        private static readonly Rect 회차결과칩 = new(22, 584, 284, 34);
        private 운영회차 operatingRound = new 운영회차();
        private 운영회차 펼친회차;
        private int 펼친회차번호;

        private bool RoundCardVisible => TidyScreen && expansionView && !mapOnly && !showDesk && !플랫폼장부표시 &&
            mapLevel == MapLevel.Dong && state != null;

        // 중에서 맡겨 둔 주문이 전달되면 플레이어가 회차 마감을 누르기 전에도 그 결과를 읽을 수 있다.
        private bool 회차결과읽기가능 => operatingRound.Phase == 회차국면.결과 ||
            autopilotOn && operatingRound.Phase == 회차국면.수행 && RoundTargetStatus == 대상상태.전달;
        private bool 회차결과펼쳐짐 => 회차결과읽기가능 && 펼친회차 == operatingRound && 펼친회차번호 == operatingRound.Number;
        private Rect RoundCard => 회차결과읽기가능 && !회차결과펼쳐짐 ? 회차결과칩 : 회차안내칸;

        private string 회차영업안내
        {
            get
            {
                if (CampaignPolicyOpen) return "운영 방침 선택 중";
                if (state?.campaign?.phase == "closed") return "오늘 영업 마감";
                if (!running || campaignSpeed == 0) return "시계 정지";
                return state?.campaign == null || state.campaign.phase == "open" ? "영업은 계속" : "영업 준비 중";
            }
        }

        private void 회차결과열람(bool 펼침)
        {
            펼친회차 = 펼침 && 회차결과읽기가능 ? operatingRound : null;
            펼친회차번호 = 펼친회차?.Number ?? 0;
        }

        // 이번 회차의 대상 주문. 기존 업무 상태에서 읽기만 한다.
        private DispatchOrder RoundTarget =>
            operatingRound.TargetId == null ? null : state?.orders?.FirstOrDefault(order => order.id == operatingRound.TargetId);

        private 대상상태 RoundTargetStatus => 운영회차Policy.FromOrderStage(RoundTarget?.stage ?? operatingRound.전달자료?.단계);

        // 준비에서, 배차가 받아들여진 주문을 이번 회차의 대상으로 묶는다. 앞선 회차의 대상은 다시 묶지 않는다.
        private void ObserveRound()
        {
            if (state?.orders == null) return;
            if (operatingRound.Phase == 회차국면.준비 && !AssignmentPending)
            {
                var order = state.orders.FirstOrDefault(item =>
                    item != null && !string.IsNullOrEmpty(item.driverId) && item.stage != "Waiting" && !operatingRound.WasTarget(item.id));
                if (order != null) operatingRound.Bind(order.id, order.revision);
            }
            if (operatingRound.전달자료 == null && RoundTarget is { } 대상)
                operatingRound.전달관측(대상.id, 대상.storeName, 대상.stage, state.campaign?.day ?? 0, 대상.revision);
        }

        private void PressRound(대상상태 status)
        {
            switch (operatingRound.Phase)
            {
                case 회차국면.준비:
                    if (!running) BeginRound();          // 기존 운영 시작. 새 게임을 열지 않는다
                    break;
                case 회차국면.수행:
                    var target = RoundTarget;
                    if (!operatingRound.Close(status, AssignmentPending, target?.revision ?? operatingRound.전달자료?.판본 ?? 0))
                        operatingRound.ReturnToSelection(status);      // 실패·보류·찾을 수 없음일 때만 돌아간다
                    break;
                default:
                    operatingRound.Next();               // 회차 번호만 올린다. 엔진에 명령을 보내지 않는다
                    break;
            }
        }

        private void 회차결과그리기()
        {
            var box = RoundCard;
            var x = box.x + 12;
            Paint(box, Rim, 9);
            Paint(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), Canvas, 8);
            if (!회차결과펼쳐짐)
            {
                Text(new Rect(x, box.y, box.width - 55, box.height), "회차 결과 (읽기) · " + 회차영업안내, 12, Paper, true);
                Text(new Rect(box.xMax - 50, box.y, 38, box.height), "펼치기", 10, Mint, true, TextAnchor.MiddleRight);
                if (Hit(box)) 회차결과열람(true);
                return;
            }

            Text(new Rect(x, box.y + 5, 208, 20), operatingRound.Number + "회차 결과 · " + 회차영업안내, 12, Paper, true);
            var 기록 = operatingRound.전달자료;
            var 매장명 = 기록?.매장명 ?? RoundTarget?.storeName;
            var 일차 = 기록 != null && 기록.영업일 > 0 ? 기록.영업일 + "일차 · " : string.Empty;
            var detail = RoundTargetStatus == 대상상태.전달
                ? 일차 + (string.IsNullOrEmpty(매장명) ? "대상 주문" : 매장명) + " · 전달 확인\n이 회차의 정산 금액은 미연결"
                : "과거 주문 기록 연결 전 · 기존 회차 마감 기록";
            Text(new Rect(x, box.y + 27, 205, 37), detail, 10, Soft, wrap: true);
            var fold = new Rect(box.xMax - 106, box.y + 6, 96, 25);
            if (ExpansionButton(fold, "접기", true, Tile)) 회차결과열람(false);
            var status = RoundTargetStatus;
            var guide = operatingRound.Guide(status, AssignmentPending, running);
            var next = new Rect(fold.x, fold.yMax + 7, fold.width, fold.height);
            if (ExpansionButton(next, guide.Button, guide.ButtonEnabled, Mint))
            {
                회차결과열람(false);
                PressRound(status);
            }
        }

        private void DrawRoundCard()
        {
            if (!RoundCardVisible) return;
            if (회차결과읽기가능)
            {
                회차결과그리기();
                return;
            }
            var status = RoundTargetStatus;
            var guide = operatingRound.Guide(status, AssignmentPending, running);

            Paint(회차안내칸, Rim, 9);
            Paint(new Rect(회차안내칸.x + 1, 회차안내칸.y + 1, 회차안내칸.width - 2, 회차안내칸.height - 2), Canvas, 8);
            var x = 회차안내칸.x + 12;
            Text(new Rect(x, 회차안내칸.y + 6, 206, 18), guide.Title, 12, Paper, true);
            var target = RoundTarget;
            var detail = operatingRound.Phase == 회차국면.수행 && target == null && operatingRound.전달자료 == null
                ? "과거 주문 기록 연결 전"
                : operatingRound.Phase != 회차국면.준비 && target != null ? target.storeName + " 주문 · " + guide.Detail : guide.Detail;
            Text(new Rect(x, 회차안내칸.y + 26, 208, 46), detail, 10, Soft, wrap: true);

            var button = new Rect(회차안내칸.xMax - 106, 회차안내칸.y + 8, 96, 26);
            if (ExpansionButton(button, guide.Button, guide.ButtonEnabled, Mint)) PressRound(status);
            if (!guide.ButtonEnabled && guide.Reason != null)
                Text(new Rect(button.x, button.yMax + 2, button.width, 38), guide.Reason, 9, Fade(Soft, 0.8f), wrap: true);
        }
    }
}
