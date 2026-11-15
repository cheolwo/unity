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
    //  - 저장하지 않는다. 새 게임을 열면(RequestStart) 1회차로 돌아간다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly Rect RoundCard = new(22, 542, 336, 76);
        private 운영회차 operatingRound = new 운영회차();

        private bool RoundCardVisible => TidyScreen && expansionView && !mapOnly && !showDesk && mapLevel == MapLevel.Dong && state != null;

        // 이번 회차의 대상 주문. 기존 업무 상태에서 읽기만 한다.
        private DispatchOrder RoundTarget =>
            operatingRound.TargetId == null ? null : state?.orders?.FirstOrDefault(order => order.id == operatingRound.TargetId);

        private 대상상태 RoundTargetStatus => 운영회차Policy.FromOrderStage(RoundTarget?.stage);

        // 준비에서, 배차가 받아들여진 주문을 이번 회차의 대상으로 묶는다. 앞선 회차의 대상은 다시 묶지 않는다.
        private void ObserveRound()
        {
            if (state?.orders == null || operatingRound.Phase != 회차국면.준비 || AssignmentPending) return;
            var order = state.orders.FirstOrDefault(item =>
                !string.IsNullOrEmpty(item.driverId) && item.stage != "Waiting" && !operatingRound.WasTarget(item.id));
            if (order != null) operatingRound.Bind(order.id, order.revision);
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
                    if (!operatingRound.Close(status, AssignmentPending, target == null ? 0 : target.revision))
                        operatingRound.ReturnToSelection(status);      // 실패·보류·찾을 수 없음일 때만 돌아간다
                    break;
                default:
                    operatingRound.Next();               // 회차 번호만 올린다. 엔진에 명령을 보내지 않는다
                    break;
            }
        }

        private void DrawRoundCard()
        {
            if (!RoundCardVisible) return;
            ObserveRound();
            var status = RoundTargetStatus;
            var guide = operatingRound.Guide(status, AssignmentPending, running);

            Paint(RoundCard, Rim, 9);
            Paint(new Rect(RoundCard.x + 1, RoundCard.y + 1, RoundCard.width - 2, RoundCard.height - 2), Canvas, 8);
            var x = RoundCard.x + 12;
            Text(new Rect(x, RoundCard.y + 6, 206, 18), guide.Title, 12, Paper, true);
            var target = RoundTarget;
            var detail = operatingRound.Phase != 회차국면.준비 && target != null ? target.storeName + " 주문 · " + guide.Detail : guide.Detail;
            Text(new Rect(x, RoundCard.y + 26, 208, 46), detail, 10, Soft, wrap: true);

            var button = new Rect(RoundCard.xMax - 106, RoundCard.y + 8, 96, 26);
            if (ExpansionButton(button, guide.Button, guide.ButtonEnabled, Mint)) PressRound(status);
            if (!guide.ButtonEnabled && guide.Reason != null)
                Text(new Rect(button.x, button.yMax + 2, button.width, 38), guide.Reason, 9, Fade(Soft, 0.8f), wrap: true);
        }
    }
}
