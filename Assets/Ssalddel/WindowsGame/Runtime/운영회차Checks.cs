using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // r187 운영 회차 규칙의 순수 검사. 빌드의 kernel-checks.txt에 붙는다.
    // 시계·원장 없이 돈다. 마감과 다음 준비가 시간·돈을 건드리지 않는다는 것은, 이 규칙이 그런 것을 받지도 돌려주지도 않는다는 뜻이다.
    internal static class 운영회차Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            var round = new 운영회차();
            Check("처음은 1회차 준비이고 대상이 없음", round.Number == 1 && round.Phase == 회차국면.준비 && round.TargetId == null && round.Closed.Count == 0);
            var idle = round.Guide(대상상태.없음, false, false);
            Check("준비에서 운영 전이면 「운영 시작」을 안내함", idle.Button == "운영 시작" && idle.ButtonEnabled, idle.Title + " · " + idle.Detail);
            var waiting = round.Guide(대상상태.없음, false, true);
            Check("운영 중인데 대상이 없으면 배차를 기다리고 이유를 말함", !waiting.ButtonEnabled && !string.IsNullOrEmpty(waiting.Reason), waiting.Reason);
            Check("대상이 없으면 마감할 수 없음", !round.Close(대상상태.전달, false, 1) && round.Phase == 회차국면.준비 && round.Closed.Count == 0,
                round.CanClose(대상상태.전달, false).Reason);

            Check("배차된 주문을 대상으로 묶으면 수행·관찰이 됨", round.Bind("order-a", 3) && round.Phase == 회차국면.수행 && round.TargetId == "order-a");
            Check("수행 중에는 다른 주문으로 대상을 바꾸지 않음", !round.Bind("order-b", 1) && round.TargetId == "order-a");
            var running = round.Guide(대상상태.진행, false, true);
            Check("진행 중에는 마감할 수 없고 이유와 계속하기를 말함 (r187 3절)",
                !round.Close(대상상태.진행, false, 4) && !round.Close(대상상태.대기, false, 4) && round.Phase == 회차국면.수행 &&
                !running.ButtonEnabled && running.Reason != null && running.Reason.Contains("계속"), running.Reason);
            Check("요청을 처리하는 중이면 전달됐어도 마감하지 않음", !round.Close(대상상태.전달, true, 5) && round.Phase == 회차국면.수행,
                round.CanClose(대상상태.전달, true).Reason);

            Check("정산까지 간 주문도 전달로 읽고, 모르는 단계는 완료로 읽지 않음",
                운영회차Policy.FromOrderStage("Delivered") == 대상상태.전달 && 운영회차Policy.FromOrderStage("Settled") == 대상상태.전달 &&
                운영회차Policy.FromOrderStage("ToDropoff") == 대상상태.진행 && 운영회차Policy.FromOrderStage("SomethingNew") == 대상상태.진행 &&
                운영회차Policy.FromOrderStage("Waiting") == 대상상태.대기 && 운영회차Policy.FromOrderStage(null) == 대상상태.없음);

            var ready = round.Guide(대상상태.전달, false, true);
            Check("전달되면 「회차 마감」이 눌림", ready.Button == "회차 마감" && ready.ButtonEnabled && ready.Reason == null);
            Check("전달된 뒤 마감하면 결과가 되고 회차 기록이 하나 남음",
                round.Close(대상상태.전달, false, 6) && round.Phase == 회차국면.결과 && round.Closed.Count == 1 &&
                round.Closed[0].Number == 1 && round.Closed[0].TargetId == "order-a" && round.Closed[0].TargetRevision == 6);
            Check("같은 마감을 다시 눌러도 기록이 늘지 않음 (r187 3절)", !round.Close(대상상태.전달, false, 6) && round.Closed.Count == 1,
                round.CanClose(대상상태.전달, false).Reason);
            var result = round.Guide(대상상태.전달, false, true);
            Check("결과에서는 「다음 운영 준비」를 안내하고 정산 금액은 미연결이라고 적음", result.Button == "다음 운영 준비" && result.ButtonEnabled &&
                result.Detail.Contains("연결되지"), result.Detail);

            Check("다음 준비는 회차 번호만 올리고 대상을 비움", round.Next() && round.Number == 2 && round.Phase == 회차국면.준비 &&
                round.TargetId == null && round.Closed.Count == 1);
            Check("결과가 아니면 다음 준비로 넘어가지 않음", !round.Next() && round.Number == 2);
            Check("이미 끝낸 대상은 다음 회차의 대상이 될 수 없음", !round.Bind("order-a", 7) && round.Phase == 회차국면.준비 && round.WasTarget("order-a"));
            Check("다른 대상으로 두 번째 회차를 이어 마침 (서로 다른 대상 ID)",
                round.Bind("order-b", 2) && round.Close(대상상태.전달, false, 9) && round.Closed.Count == 2 &&
                round.Closed.Select(record => record.TargetId).Distinct().Count() == 2 && round.Closed[1].Number == 2,
                string.Join(", ", round.Closed.Select(record => record.Number + ":" + record.TargetId)));

            var failing = new 운영회차();
            failing.Bind("order-x", 1);
            var failed = failing.Guide(대상상태.실패, false, true);
            Check("실패·보류는 성공으로 치지 않고 대상 선택으로 돌아감",
                !failing.Close(대상상태.실패, false, 2) && failed.Button == "대상 다시 고르기" && failed.ButtonEnabled &&
                failing.ReturnToSelection(대상상태.실패) && failing.Phase == 회차국면.준비 && failing.Number == 1 && failing.Closed.Count == 0 &&
                failing.Bind("order-y", 1), failed.Detail);
            Check("진행 중인 대상을 실패로 꾸며 되돌리지 않음", !failing.ReturnToSelection(대상상태.진행) && failing.TargetId == "order-y");

            // 카1: 회차 진행 저장
            var saved = 운영회차.FromText(round.ToText());
            Check("저장 글자로 되읽으면 번호 · 국면 · 대상 · 끝낸 회차가 같음", saved != null && saved.Number == round.Number && saved.Phase == round.Phase &&
                saved.TargetId == round.TargetId && saved.Closed.Count == round.Closed.Count &&
                saved.Closed.Select(record => record.Number + ":" + record.TargetId + ":" + record.TargetRevision).SequenceEqual(
                    round.Closed.Select(record => record.Number + ":" + record.TargetId + ":" + record.TargetRevision)), round.ToText().Replace('\n', '|'));
            var mid = 운영회차.FromText(failing.ToText());
            Check("수행 중인 회차도 대상과 함께 되읽고, 되읽은 회차로 이어서 마감할 수 있음",
                mid != null && mid.Phase == 회차국면.수행 && mid.TargetId == "order-y" && mid.Close(대상상태.전달, false, 3) && mid.Closed.Count == 1);
            Check("판본이 다르거나 깨진 글자는 되읽지 않음(처음 회차로)",
                운영회차.FromText(null) == null && 운영회차.FromText("운영회차.v0\n1\t0\t\t0") == null && 운영회차.FromText("운영회차.v1\n1\t9\t\t0") == null &&
                운영회차.FromText("운영회차.v1\n2\t1\t\t0") == null && 운영회차.FromText("운영회차.v1\n1\t0\t\t0\nx") == null);
            Check("되읽은 회차도 이미 끝낸 대상을 다시 묶지 않음", saved != null && !saved.Bind("order-a", 1) || saved != null && saved.Phase != 회차국면.준비);
            var 관찰 = new 운영회차();
            관찰.Bind("original-order", 3);
            Check("다른 주문, 진행·미확인 상태, 오래된 판본은 전달로 저장하지 않음",
                !관찰.전달관측("other-order", "다른 가게", "Delivered", 1, 6) &&
                !관찰.전달관측("original-order", "가게", "ToDropoff", 1, 6) &&
                !관찰.전달관측("original-order", "가게", "Unknown", 1, 6) &&
                !관찰.전달관측("original-order", "가게", "Delivered", 1, 2) && 관찰.전달자료 == null);
            const string 매장명 = "가게\t별관\n1층";
            Check("확인한 전달을 기록해도 회차는 수행이고 수동 마감은 그대로 남음",
                관찰.전달관측("original-order", 매장명, "Delivered", 1, 6) &&
                관찰.Phase == 회차국면.수행 && 관찰.Closed.Count == 0 &&
                !관찰.전달관측("original-order", "나중 이름", "Settled", 4, 7));
            var 보관문 = 관찰.ToText();
            var 복원 = 운영회차.FromText(보관문);
            var 전달 = 복원?.전달자료;
            Check("다음 날들에 원래 주문이 없어도 저장한 가게·원 주문·회차·판본·영업일을 복원",
                전달 != null && 전달.회차번호 == 1 && 전달.주문Id == "original-order" && 전달.단계 == "Delivered" &&
                전달.매장명 == 매장명 && 전달.판본 == 6 && 전달.영업일 == 1 && 복원.Closed.Count == 0 && 복원.ToText() == 보관문);
            Check("늦게 직접 마감하면 관측 판본으로 한 번 기록되고 다음 준비는 이전 회차 자료를 비움",
                전달 != null && 복원.Close(운영회차Policy.FromOrderStage(전달.단계), false, 전달.판본) &&
                복원.Closed.Count == 1 && 복원.Closed[0].TargetRevision == 6 &&
                운영회차.FromText(복원.ToText())?.전달자료?.주문Id == "original-order" && 복원.Next() && 복원.전달자료 == null);
            var 옛수행 = 운영회차.FromText("운영회차.v1\n1\t1\toriginal-order\t3");
            var 옛마감 = 운영회차.FromText("운영회차.v1\n1\t2\toriginal-order\t3\n1\toriginal-order\t6");
            Check("관측하지 않은 옛 수행·마감 파일은 회차를 살리고 과거 주문 상세를 만들어 내지 않음",
                옛수행 != null && 옛수행.Phase == 회차국면.수행 && 옛수행.전달자료 == null &&
                옛마감 != null && 옛마감.Closed.Count == 1 && 옛마감.전달자료 == null);
            var 손상 = 운영회차.FromText("운영회차.v2\n1\t1\toriginal-order\t3\tDelivered\t1\t6\t!bad");
            Check("읽기 자료가 손상돼도 회차를 살리고 전달을 단정하지 않음",
                손상 != null && 손상.TargetId == "original-order" && 손상.전달자료 == null);
            return results;
        }
    }
}
