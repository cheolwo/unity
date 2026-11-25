using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ssalddel.WindowsGame
{
    // r187 같은 날 반복하는 짧은 운영 회차 (첫 검토용 구현의 규칙, 2026-11-14).
    //   준비 → 수행·관찰 → 결과 → (다음 준비) → 준비 …
    // 한 회차는 대상 작업 한 건에 묶인다. 첫 범위의 대상은 기존 가상 배달 주문 한 건이다.
    //  - 대상의 상태는 기존 업무(Core/Kernel)가 정한다. 이 규칙은 화면이 읽어 준 상태를 받기만 하고 완료를 지어내지 않는다.
    //  - 마감은 대상이 실제로 전달된 뒤에만 된다. 전달을 포함하는 뒤 상태(정산됨)도 전달로 읽는다.
    //  - 마감과 다음 준비는 시계·날짜·돈·재고·원장을 건드리지 않는다. 이 규칙은 그런 것을 알지도 못한다(순수 상태 기계).
    //  - 같은 마감을 두 번 눌러도 회차 기록은 한 번만 남는다. 이미 끝낸 대상은 다음 회차의 대상이 될 수 없다.
    //  - 회차는 자기 대상만 본다. 다른 사업의 진행 업무가 남아 있어도 마감을 막지 않는다.
    //  - 저장하지 않는다. 같은 실행 세션 안의 반복까지가 첫 범위다.
    //    (카1, 2026-11-15) 세션 저장 봉투(SagajeongDispatchEngine의 roundJson)에 ToText로 함께 담고, 저장을 불러올 때 FromText로 되읽는다.
    internal enum 회차국면 { 준비, 수행, 결과 }

    // 화면이 기존 업무 상태에서 읽어 주는 대상 작업의 상태.
    internal enum 대상상태 { 없음, 대기, 진행, 전달, 실패 }

    // 전달이 확인된 시점의 표시 자료. 새 영업일이 열려 오늘 주문 목록이 바뀌어도 그때 읽은 값으로 안내한다.
    internal sealed class 회차전달자료
    {
        public readonly int 회차번호, 영업일;
        public readonly long 판본;
        public readonly string 주문Id, 매장명, 단계;

        public 회차전달자료(int 회차번호, string 주문Id, string 매장명, string 단계, int 영업일, long 판본)
        {
            this.회차번호 = 회차번호;
            this.주문Id = 주문Id;
            this.매장명 = 매장명 ?? string.Empty;
            this.단계 = 단계;
            this.영업일 = 영업일;
            this.판본 = 판본;
        }
    }

    internal sealed class 회차기록
    {
        public int Number;
        public string TargetId;
        public long TargetRevision;     // 마감할 때 읽은 대상의 판본
    }

    // 지금 국면에서 화면에 보일 말과 단추.
    internal readonly struct 회차안내
    {
        public readonly string Title;
        public readonly string Detail;
        public readonly string Button;
        public readonly bool ButtonEnabled;
        public readonly string Reason;      // 단추가 눌리지 않는 이유. 눌리면 null

        public 회차안내(string title, string detail, string button, bool buttonEnabled, string reason)
        {
            Title = title;
            Detail = detail;
            Button = button;
            ButtonEnabled = buttonEnabled;
            Reason = reason;
        }
    }

    internal sealed class 운영회차
    {
        private readonly List<회차기록> closed = new List<회차기록>();

        public int Number { get; private set; } = 1;
        public 회차국면 Phase { get; private set; } = 회차국면.준비;
        public string TargetId { get; private set; }
        public long TargetRevision { get; private set; }
        public IReadOnlyList<회차기록> Closed => closed;
        public 회차전달자료 전달자료 { get; private set; }

        public bool 전달관측(string 주문Id, string 매장명, string 단계, int 영업일, long 판본)
        {
            if (전달자료 != null || Phase == 회차국면.준비 || string.IsNullOrEmpty(주문Id) || TargetId != 주문Id ||
                운영회차Policy.FromOrderStage(단계) != 대상상태.전달 || 판본 < TargetRevision || 영업일 < 0)
                return false;
            전달자료 = new 회차전달자료(Number, 주문Id, 매장명, 단계, 영업일, 판본);
            return true;
        }

        // 앞선 회차에서 이미 끝낸 대상인가.
        public bool WasTarget(string targetId) => closed.Any(record => record.TargetId == targetId);

        // 준비에서 대상을 묶는다(배차가 받아들여진 주문). 이미 끝낸 대상은 묶지 않는다. 묶이면 수행·관찰로 간다.
        public bool Bind(string targetId, long revision)
        {
            if (Phase != 회차국면.준비 || string.IsNullOrEmpty(targetId) || WasTarget(targetId)) return false;
            TargetId = targetId;
            TargetRevision = revision;
            Phase = 회차국면.수행;
            return true;
        }

        // 마감할 수 있는가. 안 되면 이유를 함께 돌려준다.
        public (bool Ok, string Reason) CanClose(대상상태 status, bool requestPending)
        {
            if (Phase == 회차국면.결과) return (false, "이미 마감한 회차입니다");
            if (Phase == 회차국면.준비 || TargetId == null) return (false, "대상 작업을 아직 고르지 않았습니다");
            if (requestPending) return (false, "요청을 처리하는 중입니다");
            switch (status)
            {
                case 대상상태.전달: return (true, null);
                case 대상상태.실패: return (false, "대상 작업이 실패했거나 보류됐습니다 · 대상을 다시 고를 수 있습니다");
                case 대상상태.없음: return (false, "대상 작업을 찾을 수 없습니다 · 대상을 다시 고를 수 있습니다");
                default: return (false, "작업이 진행 중입니다 · 계속 지켜보거나 수행하세요");
            }
        }

        // 마감: 수행 → 결과. 대상이 실제로 전달됐을 때만 된다. 회차 기록은 한 번만 남는다.
        public bool Close(대상상태 status, bool requestPending, long revision)
        {
            if (!CanClose(status, requestPending).Ok) return false;
            closed.Add(new 회차기록 { Number = Number, TargetId = TargetId, TargetRevision = revision });
            Phase = 회차국면.결과;
            return true;
        }

        // 다음 준비: 결과 → 다음 회차의 준비. 새 게임이나 다음 날을 시작하는 것이 아니다.
        public bool Next()
        {
            if (Phase != 회차국면.결과) return false;
            Number++;
            전달자료 = null;
            TargetId = null;
            TargetRevision = 0;
            Phase = 회차국면.준비;
            return true;
        }

        // 실패·보류·찾을 수 없음: 성공으로 치지 않고 대상 선택으로 돌아간다. 회차 번호는 그대로이고 기록도 남기지 않는다.
        public bool ReturnToSelection(대상상태 status)
        {
            if (Phase != 회차국면.수행 || (status != 대상상태.실패 && status != 대상상태.없음)) return false;
            전달자료 = null;
            TargetId = null;
            TargetRevision = 0;
            Phase = 회차국면.준비;
            return true;
        }

        // 저장 글자(카1). 첫 줄은 판본, 둘째 줄은 번호 · 국면 · 대상 · 판본, 그 뒤 줄마다 끝낸 회차. 칸은 탭이다.
        public const string TextVersion = "운영회차.v1";
        private const string 전달자료판본 = "운영회차.v2";

        public string ToText()
        {
            var head = Number + "\t" + (int)Phase + "\t" + (TargetId ?? string.Empty) + "\t" + TargetRevision;
            if (전달자료 != null)
            {
                var 이름 = Convert.ToBase64String(Encoding.UTF8.GetBytes(전달자료.매장명));
                head += "\t" + 전달자료.단계 + "\t" + 전달자료.영업일 + "\t" + 전달자료.판본 + "\t" + 이름;
            }
            var lines = new List<string> { 전달자료 == null ? TextVersion : 전달자료판본, head };
            lines.AddRange(closed.Select(record => record.Number + "\t" + record.TargetId + "\t" + record.TargetRevision));
            return string.Join("\n", lines);
        }

        // 판본이 다르거나 글자가 깨졌으면 null(처음 회차로 시작한다).
        public static 운영회차 FromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var lines = text.Split('\n');
            if (lines.Length < 2 || (lines[0] != TextVersion && lines[0] != 전달자료판본)) return null;
            var head = lines[1].Split('\t');
            if (head.Length != (lines[0] == 전달자료판본 ? 8 : 4) || !int.TryParse(head[0], out var number) || number < 1 || !int.TryParse(head[1], out var phase) ||
                phase < 0 || phase > 2 || !long.TryParse(head[3], out var revision)) return null;
            var round = new 운영회차 { Number = number, Phase = (회차국면)phase, TargetId = head[2].Length == 0 ? null : head[2], TargetRevision = revision };
            if (round.Phase == 회차국면.수행 && round.TargetId == null) return null;
            if (head.Length == 8 && int.TryParse(head[5], out var 일차) && long.TryParse(head[6], out var 판본))
            {
                try
                {
                    var 매장명 = Encoding.UTF8.GetString(Convert.FromBase64String(head[7]));
                    round.전달관측(round.TargetId, 매장명, head[4], 일차, 판본);
                }
                catch (FormatException) { /* 표시 자료만 읽지 못해도 기존 회차 진행과 마감 기록은 복원한다. */ }
            }
            for (var i = 2; i < lines.Length; i++)
            {
                var cell = lines[i].Split('\t');
                if (cell.Length != 3 || !int.TryParse(cell[0], out var n) || !long.TryParse(cell[2], out var r) || cell[1].Length == 0) return null;
                round.closed.Add(new 회차기록 { Number = n, TargetId = cell[1], TargetRevision = r });
            }
            return round;
        }

        // running: 기존 운영(영업)이 돌고 있는가. 준비에서 「운영 시작」을 보일지 정하는 데만 쓴다.
        public 회차안내 Guide(대상상태 status, bool requestPending, bool running)
        {
            var title = "운영 회차 " + Number + " · " + 운영회차Policy.PhaseName(Phase);
            if (Phase == 회차국면.준비)
            {
                return running
                    ? new 회차안내(title, "주문 하나에 기사를 배차하세요. 그 주문이 이번 회차의 대상입니다.", "배차를 기다리는 중", false,
                        "대상 작업을 아직 고르지 않았습니다")
                    : new 회차안내(title, "운영을 시작하고 주문 하나에 기사를 배차하세요. 그 주문이 전달되면 회차를 마감합니다.", "운영 시작", true, null);
            }
            if (Phase == 회차국면.결과)
                return new 회차안내(title, "대상 주문이 전달됐습니다. 이 주문의 정산 금액은 아직 연결되지 않았습니다.", "다음 운영 준비", true, null);

            var (ok, reason) = CanClose(status, requestPending);
            if (ok) return new 회차안내(title, "대상 주문이 전달됐습니다. 회차를 마감하세요.", "회차 마감", true, null);
            if (!requestPending && (status == 대상상태.실패 || status == 대상상태.없음))
                return new 회차안내(title, reason, "대상 다시 고르기", true, null);
            return new 회차안내(title, "대상 주문을 배달하는 중입니다. 전달되면 마감할 수 있습니다.", "회차 마감", false, reason);
        }
    }

    internal static class 운영회차Policy
    {
        public static string PhaseName(회차국면 phase)
        {
            switch (phase)
            {
                case 회차국면.준비: return "준비";
                case 회차국면.수행: return "수행·관찰";
                default: return "결과";
            }
        }

        // 기존 배달 주문의 단계 글자 → 대상 상태.
        // 전달을 포함하는 뒤 상태(Settled)도 전달로 읽는다. 모르는 글자는 진행으로 읽는다(완료로 읽지 않는다).
        public static 대상상태 FromOrderStage(string stage)
        {
            switch (stage)
            {
                case null:
                case "": return 대상상태.없음;
                case "Waiting": return 대상상태.대기;
                case "Delivered":
                case "Settled": return 대상상태.전달;
                case "Failed":
                case "Cancelled":
                case "Held": return 대상상태.실패;
                default: return 대상상태.진행;
            }
        }
    }
}
