using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// r174 결산 확인(걸음 4, 2026-11-14)을 맡는 게임 층. 업무 OS가 아니다(영역·기사 무념과 같은 자리).
    /// 결산 확인은 어느 날짜의 어느 판본 보고를 읽었는지 적는 일이다. 읽음 상태만 바뀐다.
    /// 시계를 진행하지 않고, 원장에 줄을 적지 않고, 돈·재고·주문을 바꾸지 않는다. 다른 사업의 명령을 막지도 않는다.
    /// 지난 보고를 확인하지 않았다고 오늘 일이 막히지 않는다.
    /// 명령: confirm-report (date = 한국표준시 날짜 "yyyy-MM-dd"). 기록이 없는 날짜는 확인으로 남기지 않는다.
    /// 상태는 커널 저장에 함께 담긴다. 새 회차를 열면 원장과 함께 비워진다.
    /// </summary>
    public sealed class 일일결산Module : IBusinessModule
    {
        public const string Id = "DailyReportReading";
        public const string ConfirmAction = "confirm-report";

        private GameKernel kernel;
        private 일일결산확인State state = new 일일결산확인State();

        public string OsId { get { return Id; } }

        public void Start(ModuleContext context)
        {
            kernel = context == null ? null : context.Kernel;
            state = new 일일결산확인State();
        }

        public void Tick(long elapsedMs)
        {
            // 시간이 흘러도 읽음 상태는 스스로 바뀌지 않는다.
        }

        public GamePreview Preview(GameCommand command)
        {
            return GamePreview.Blocked("결산 확인에는 미리보기가 없습니다.", "UnknownAction");
        }

        public GameResult Confirm(GameCommand command)
        {
            if (command == null || command.Action != ConfirmAction) return GameResult.Fail("알 수 없는 명령입니다.", "UnknownAction");
            var date = command.Value("date");
            if (!일일결산Policy.IsDateKey(date)) return GameResult.Fail("날짜를 읽을 수 없습니다.", "InvalidDate");
            if (kernel == null) return GameResult.Fail("회차가 아직 열리지 않았습니다.", "NoRound");
            var report = 일일결산Policy.For(kernel.Ledger, date);
            if (report.Entries == 0) return GameResult.Fail("그 날짜에는 기록이 없습니다.", "NoRecords");
            var version = 일일결산Policy.Version(report);
            var mark = state.Find(date);
            if (mark != null && mark.Version == version)
                return new GameResult { Ok = true, Code = "AlreadyRead", Message = date + " 결산은 이미 확인했습니다." };
            if (mark == null)
            {
                mark = new 일일결산확인 { DateKey = date };
                state.Read.Add(mark);
            }
            mark.Version = version;
            mark.Entries = report.Entries;
            mark.ReadAtUtcMs = kernel.RealNowUtcMs;
            return GameResult.Done(date + " 결산을 확인했습니다.");
        }

        /// <summary>날짜별 보고와 읽음 상태. 줄이 있는 날짜만 나온다.</summary>
        public object Snapshot()
        {
            var snapshot = new 일일결산Snapshot();
            if (kernel == null) return snapshot;
            foreach (var report in 일일결산Policy.ByDate(kernel.Ledger))
            {
                var mark = state.Find(report.DateKey);
                snapshot.Days.Add(new 일일결산일
                {
                    Report = report,
                    Reading = 일일결산Policy.Reading(report, mark == null ? null : mark.Version),
                });
            }
            return snapshot;
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            return HandoffReceipt.Refused("결산 확인은 인계를 받지 않습니다.");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state, Formatting.None);
        }

        public void Restore(string saved)
        {
            try
            {
                var restored = JsonConvert.DeserializeObject<일일결산확인State>(saved);
                if (restored == null || restored.Schema != 일일결산확인State.CurrentSchema) return;
                restored.Read = restored.Read ?? new List<일일결산확인>();
                restored.Read.RemoveAll(item => item == null || !일일결산Policy.IsDateKey(item.DateKey));
                state = restored;
            }
            catch (JsonException)
            {
                // 읽을 수 없는 저장은 버리고 처음 값으로 둔다.
            }
        }
    }

    public sealed class 일일결산확인State
    {
        public const string CurrentSchema = "hongdal-daily-report-reading.v1";
        public string Schema = CurrentSchema;
        public List<일일결산확인> Read = new List<일일결산확인>();

        public 일일결산확인 Find(string dateKey)
        {
            return Read.FirstOrDefault(item => item.DateKey == dateKey);
        }
    }

    /// <summary>한 날짜의 보고를 확인한 기록.</summary>
    public sealed class 일일결산확인
    {
        public string DateKey = string.Empty;
        /// <summary>확인한 때의 판본(일일결산Policy.Version).</summary>
        public string Version = string.Empty;
        public int Entries;
        /// <summary>확인한 현실 시각(UTC 밀리초). 모르면 0.</summary>
        public long ReadAtUtcMs;
    }

    public sealed class 일일결산일
    {
        public 일일결산 Report;
        public string Reading = 일일결산읽음.Unread;
    }

    public sealed class 일일결산Snapshot
    {
        public List<일일결산일> Days = new List<일일결산일>();
    }
}
