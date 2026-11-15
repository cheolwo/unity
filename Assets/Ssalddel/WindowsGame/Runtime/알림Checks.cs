using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame
{
    // 떴다 사라지는 알림 규칙의 순수 검사 (2026-11-15). 빌드의 kernel-checks.txt에 붙는다. 시계 · 원장 없이 돈다.
    internal static class 알림Checks
    {
        internal static List<(string Name, bool Ok, string Detail)> Run()
        {
            var results = new List<(string, bool, string)>();
            void Check(string name, bool ok, string detail = null) => results.Add((name, ok, detail));

            var queue = new 알림큐();
            var first = queue.Add(알림종류.배차, "store-a", "기사 A · 배차", 0, 10);
            Check("나타남 → 머묾 → 사라짐", 알림큐.Alpha(first, 10) == 0f && 알림큐.Alpha(first, 10 + 알림큐.InSeconds) == 1f &&
                알림큐.Alpha(first, 10 + 알림큐.InSeconds + 1) == 1f && 알림큐.Alpha(first, 알림큐.EndOf(first) - 알림큐.OutSeconds / 2) > 0f &&
                알림큐.Alpha(first, 알림큐.EndOf(first)) == 0f);
            queue.Expire(알림큐.EndOf(first));
            Check("정한 시간이 지나면 사라짐", queue.Items.Count == 0);

            queue.Clear();
            var a = queue.Add(알림종류.전달, "drop-1", "배달 완료", 0, 20);
            var b = queue.Add(알림종류.전달, "drop-1", "배달 완료", 0, 20 + 알림큐.MergeSeconds - 0.1);
            Check("같은 자리 · 같은 종류가 짧은 사이에 또 오면 합침", ReferenceEquals(a, b) && a.Count == 2 && 알림큐.Line(a) == "배달 완료 ×2");
            Check("합치면 마지막 때부터 다시 머묾", 알림큐.EndOf(a) > 20 + 알림큐.MergeSeconds - 0.1 + 알림큐.HoldSeconds);
            var c = queue.Add(알림종류.전달, "drop-1", "배달 완료", 0, 20 + 알림큐.MergeSeconds * 2 + 0.1);
            Check("사이가 길면 따로", !ReferenceEquals(a, c) && queue.Items.Count == 2);
            var d = queue.Add(알림종류.전달, "drop-2", "배달 완료", 0, 20 + 알림큐.MergeSeconds * 2 + 0.2);
            Check("다른 자리는 따로", !ReferenceEquals(c, d) && queue.Items.Count == 3);

            queue.Clear();
            var money = queue.Add(알림종류.정산, "store-a", null, 1200, 30);
            Check("정산은 엔진 금액을 그대로", 알림큐.Line(money) == $"정산 +{1200:N0}원");
            queue.Add(알림종류.정산, "store-a", null, 1200, 30.5);
            Check("합친 정산은 금액을 더함", money.Count == 2 && money.Amount == 2400 && 알림큐.Line(money) == $"정산 2건 +{2400:N0}원");
            var unknown = queue.Add(알림종류.정산, string.Empty, null, 0, 31);
            Check("금액을 모르면 0원으로 적지 않음", 알림큐.Line(unknown) == "정산 기록");

            queue.Clear();
            for (var index = 0; index < 알림큐.MaxShown + 2; index++) queue.Add(알림종류.배차, "store-" + index, "배차", 0, 40);
            Check("넘치면 오래된 것부터 뺌", queue.Items.Count == 알림큐.MaxShown && queue.Items[0].Key == "store-2");

            queue.Clear();
            var closing = queue.Add(알림종류.마감, string.Empty, "1일차 영업 마감", 0, 50, longHold: true);
            Check("하루 마감은 눌러 볼 만큼 오래 머묾", 알림큐.EndOf(closing) - 50 > 알림큐.HoldSeconds + 3);

            Check("정도: 보통만 글, 그대로는 알림을 쓰지 않음",
                알림큐.ShowsText(알림정도.보통) && !알림큐.ShowsText(알림정도.조용히) &&
                알림큐.Active(알림정도.조용히) && !알림큐.Active(알림정도.그대로));

            var share = new 정산몫();
            share.Baseline(1, 0, 0);
            var one = share.Take(1, 1, 1200);
            var two = share.Take(1, 3, 3600);
            var same = share.Take(1, 3, 3600);
            Check("정산 몫은 늘어난 만큼만", one == 1200 && two == 2400 && same == 0);
            var nextDay = share.Take(2, 1, 1200);
            Check("새 날은 0부터", nextDay == 1200);
            share.Baseline(2, 2, 2400);
            Check("불러온 뒤의 기준선에서는 늘지 않으면 0", share.Take(2, 2, 2400) == 0 && share.Take(2, 1, 1000) == 0);

            return results;
        }
    }
}
