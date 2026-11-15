using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-14 2단계: 기사 무념·경 시간·등급(r124~r126), 경 시간당 순소득(r133), 생산·소비 흐름(r134)의 규칙.
    // 예전에는 화면(SagajeongDispatchMind.cs)이 계산해 저장되지 않았다. 계산식과 수치는 그때와 같다.
    // 이 파일은 System만 쓴다. 음식 배달의 상태는 관측(기사무념관측)으로만 받고 음식 배달을 바꾸지 않는다.

    /// <summary>음식 배달 상태에서 무념 계산에 필요한 것만 추린 사본.</summary>
    public sealed class 기사무념관측
    {
        /// <summary>캠페인 일차. 단일 회차는 1.</summary>
        public int Day = 1;
        /// <summary>지금의 가상 시각(하루 안의 분).</summary>
        public double NowMinutes;
        public int StoreCount;
        public List<string> CourierIds = new List<string>();
        public List<기사무념관측Order> Orders = new List<기사무념관측Order>();
        public List<기사무념관측Event> Events = new List<기사무념관측Event>();
    }

    public sealed class 기사무념관측Order
    {
        public string Id = string.Empty;
        public string DriverId = string.Empty;
        public bool Settled;
    }

    public sealed class 기사무념관측Event
    {
        public string OrderId = string.Empty;
        public string Code = string.Empty;
        /// <summary>사건 시각(하루 안의 분).</summary>
        public double AtMinutes;
        public string Text = string.Empty;
    }

    /// <summary>저장되는 기사 한 명의 무념 기록.</summary>
    public sealed class 기사무념Rider
    {
        public string Id = string.Empty;
        public int Grade;
        public double Capacity;
        public string TarotId = string.Empty;
        public string TarotName = string.Empty;
        public int ValidDays;
        public int Qualifying;
        public int CheckedDay = -1;
        public string LastDayResult = string.Empty;
    }

    /// <summary>주문 하나의 업무 구간(출발→전달).</summary>
    public sealed class 기사무념Work
    {
        public string OrderId = string.Empty;
        public string DriverId = string.Empty;
        public int Day;
        public double? Start;
        public double? End;
        public bool Settled;
        public int Pay;
    }

    /// <summary>저장되는 전체 상태.</summary>
    public sealed class 기사무념State
    {
        public const string CurrentSchema = "hongdal-rider-mind.v1";

        public string Schema = CurrentSchema;
        public int Day = -1;
        public List<기사무념Rider> Riders = new List<기사무념Rider>();
        public List<기사무념Work> Works = new List<기사무념Work>();

        public 기사무념Rider FindRider(string id)
        {
            foreach (var rider in Riders)
                if (rider.Id == id) return rider;
            return null;
        }

        public 기사무념Work FindWork(string orderId)
        {
            foreach (var work in Works)
                if (work.OrderId == orderId) return work;
            return null;
        }
    }

    /// <summary>기사 한 명의 오늘 계산 결과.</summary>
    public sealed class 기사무념Live
    {
        public double Used;
        public double Maintained;
        public double Remaining;
        public bool Broken;
        public double? BrokenAt;
        public bool Working;
        public int Settled;
        public int Pay;
        public int Linked;
    }

    /// <summary>화면용 사본의 기사 한 줄.</summary>
    public sealed class 기사무념RiderView
    {
        public string Id = string.Empty;
        public int Grade;
        public string GradeName = string.Empty;
        public double Capacity;
        public string TarotId = string.Empty;
        public string TarotName = string.Empty;
        public int ValidDays;
        public int Qualifying;
        public bool CheckedToday;
        public string LastDayResult = string.Empty;
        public bool HasNextGrade;
        public string NextGradeName = string.Empty;
        public double NextGradeMinutes;
        public double Used;
        public double Maintained;
        public double Remaining;
        public bool Broken;
        public bool Working;
        public int Settled;
        public int Pay;
        public int Linked;
        /// <summary>남은 경 시간이 대표 업무 + 여유보다 적다.</summary>
        public bool Low;
    }

    public sealed class 기사무념Snapshot
    {
        public int Day = 1;
        public bool Observed;
        public List<기사무념RiderView> Riders = new List<기사무념RiderView>();
        public double MaintainedMinutes;
        public int LinkedIncome;
        public int BrokenCount;
        public int FlowOrders;
        public int FlowPrepare;
        public int FlowDeliver;
        public int FlowLimit;
        public string FlowFocus = string.Empty;

        public 기사무념RiderView Find(string id)
        {
            foreach (var rider in Riders)
                if (rider.Id == id) return rider;
            return null;
        }
    }

    public static class 기사무념배차판정
    {
        public const string Ok = "Ok";
        /// <summary>배차할 수 있지만 남은 경 시간이 적다.</summary>
        public const string Low = "MindLow";
        /// <summary>무념이 깨져 오늘은 새 배차를 받지 않는다.</summary>
        public const string Broken = "MindBroken";
    }

    public static class 기사무념Policy
    {
        public static readonly string[] GradeNames = { "신사", "문사", "학사", "양사", "경사", "수사", "정사", "대사", "대종사" };
        // 신사 6·문사 8·학사 10분은 r125 첫 시험 기준. 양사부터는 기획 미정이라 임시값.
        public static readonly double[] GradeMinutes = { 6, 8, 10, 12, 14, 16, 18, 20, 22 };
        public const double Margin = 1;           // r125 여유 1분
        public const double TypicalTask = 2.5;    // 출발→전달 대표 업무 시간(사가정 표본 약 2분 24초)
        public const int DefaultPay = 3600;       // 음식 배달 Core의 건당 가상 기사 보수
        public const int ShiftsForPromotion = 2;
        public const double CapacityGrowth = 1;   // r125 유효 근무마다 유지 가능 시간 +1분

        /// <summary>
        /// 음식 배달 상태를 받아 업무 구간을 갱신한다. 일차가 넘어가면 전날 근무를 마감한다.
        /// 일차가 뒤로 가면(새 회차) 기록을 버린다. 같은 관측을 다시 받아도 결과가 같다.
        /// </summary>
        public static void Observe(기사무념State state, 기사무념관측 seen)
        {
            if (state == null || seen == null) return;
            var today = seen.Day > 0 ? seen.Day : 1;
            if (state.Day >= 0 && today < state.Day) Reset(state);
            else if (state.Day >= 0 && today > state.Day) CloseDay(state, state.Day);
            state.Day = today;

            foreach (var order in seen.Orders)
            {
                if (order == null || string.IsNullOrEmpty(order.Id)) continue;
                var work = state.FindWork(order.Id);
                if (work == null)
                {
                    work = new 기사무념Work { OrderId = order.Id, Day = OrderDay(order.Id, today), Pay = DefaultPay };
                    state.Works.Add(work);
                }
                work.DriverId = order.DriverId ?? string.Empty;
                work.Settled = order.Settled;
                foreach (var entry in seen.Events)
                {
                    if (entry == null || entry.OrderId != order.Id) continue;
                    if (entry.Code == "driver_departed") work.Start = entry.AtMinutes;
                    else if (entry.Code == "food_delivered") work.End = entry.AtMinutes;
                    else if (entry.Code == "driver_pay_accrued")
                    {
                        var pay = ParseWon(entry.Text);
                        if (pay.HasValue) work.Pay = pay.Value;
                    }
                }
            }
            foreach (var id in seen.CourierIds) Rider(state, id);
        }

        public static void Reset(기사무념State state)
        {
            state.Riders.Clear();
            state.Works.Clear();
            state.Day = -1;
        }

        /// <summary>기사 기록을 찾거나 처음 값으로 만든다.</summary>
        public static 기사무념Rider Rider(기사무념State state, string courierId)
        {
            var rider = state.FindRider(courierId);
            if (rider != null) return rider;
            // r126: 기사마다 시작 등급이 고정된 한 묶음. 개인 타로 장착 카드는 기획 미정이라 시안용으로 고정한다.
            var letter = Letter(courierId);
            var grade = letter == "B" ? 1 : 0;
            if (letter == "A" && courierId.IndexOf("gwangjin", StringComparison.Ordinal) >= 0) grade = 2;
            rider = new 기사무념Rider
            {
                Id = courierId,
                Grade = grade,
                Capacity = GradeMinutes[grade],
                TarotId = letter == "A" ? "chariot" : letter == "B" ? "temperance" : "empress",
                TarotName = letter == "A" ? "전차" : letter == "B" ? "절제" : "여제",
            };
            state.Riders.Add(rider);
            return rider;
        }

        /// <summary>한 기사의 하루 계산. now가 null이면 마감 계산이라 끝나지 않은 업무를 넣지 않는다.</summary>
        public static 기사무념Live Live(기사무념State state, string courierId, int day, double? now)
        {
            var rider = Rider(state, courierId);
            var live = new 기사무념Live();
            foreach (var work in state.Works
                         .Where(item => item.DriverId == courierId && item.Day == day && item.Start.HasValue)
                         .OrderBy(item => item.Start.Value))
            {
                var start = work.Start.Value;
                var end = work.End ?? now ?? start;
                // 자정을 넘긴 경우만 하루를 더한다. 지금 시각과 사건 시각의 소수점 차이로 끝이 시작보다
                // 살짝 앞서는 순간에는 0분으로 본다. (예전에는 이때 1,440분이 더해져 무념이 깨진 것으로 나왔다.)
                if (end < start - 720) end += 1440;
                else if (end < start) end = start;
                if (!work.End.HasValue) live.Working = true;
                var minutes = Math.Max(0, end - start);
                var maintained = Math.Max(0, Math.Min(minutes, rider.Capacity - live.Used));
                if (!live.Broken && live.Used + minutes > rider.Capacity)
                {
                    live.Broken = true;
                    live.BrokenAt = start + Math.Max(0, rider.Capacity - live.Used);
                }
                live.Used += minutes;
                live.Maintained += maintained;
                if (work.Settled)
                {
                    live.Settled++;
                    live.Pay += work.Pay;
                    live.Linked += minutes > 0 ? (int)Math.Round(work.Pay * maintained / minutes) : work.Pay;
                }
            }
            live.Remaining = Math.Max(0, rider.Capacity - live.Used);
            return live;
        }

        // r125: 점검 후 시작해 배달 1건 이상을 정산하고 무념을 지킨 근무만 유효. 유효 근무마다 유지 가능 시간 +1분,
        // 다음 등급 기준 이상을 유지한 유효 근무가 두 번 쌓이면 승급. 밤사이 휴식으로 경 시간과 무념은 회복된다.
        public static void CloseDay(기사무념State state, int day)
        {
            foreach (var rider in state.Riders.ToList())
            {
                var live = Live(state, rider.Id, day, null);
                if (live.Used <= 0 && live.Settled == 0)
                {
                    rider.LastDayResult = day + "일차 · 배달 기록 없음";
                    continue;
                }
                if (live.Broken)
                {
                    rider.LastDayResult = day + "일차 · 무념이 깨진 근무 · 휴식으로 회복";
                    continue;
                }
                if (rider.CheckedDay != day || live.Settled == 0)
                {
                    rider.LastDayResult = rider.CheckedDay != day
                        ? day + "일차 · 출발 전 점검이 없어 유효 기록 없음"
                        : day + "일차 · 정산된 배달이 없어 유효 기록 없음";
                    continue;
                }
                rider.ValidDays++;
                rider.Capacity += CapacityGrowth;
                var result = day + "일차 · 유효 근무 · 유지 가능 " + Duration(rider.Capacity);
                if (rider.Grade + 1 < GradeNames.Length && live.Maintained >= GradeMinutes[rider.Grade + 1])
                {
                    rider.Qualifying++;
                    if (rider.Qualifying >= ShiftsForPromotion)
                    {
                        rider.Grade++;
                        rider.Qualifying = 0;
                        result += " · " + GradeNames[rider.Grade] + " 승급";
                    }
                }
                rider.LastDayResult = result;
            }
        }

        /// <summary>출발 전 점검을 오늘 날짜로 적는다. 이미 했으면 false.</summary>
        public static bool Check(기사무념State state, string courierId)
        {
            var rider = state.FindRider(courierId);
            if (rider == null || state.Day < 0 || rider.CheckedDay == state.Day) return false;
            rider.CheckedDay = state.Day;
            return true;
        }

        // r125: 남은 경 시간이 예상 업무 + 여유 1분보다 적으면 다른 기사나 휴식을 권한다. 무념이 깨진 기사는 새 배차를 받지 않는다.
        public static string Assignable(기사무념Live live)
        {
            if (live.Broken) return 기사무념배차판정.Broken;
            return live.Remaining < TypicalTask + Margin ? 기사무념배차판정.Low : 기사무념배차판정.Ok;
        }

        /// <summary>화면용 사본. seen이 null이면 저장된 기록만으로 만든다(끝나지 않은 업무는 0분).</summary>
        public static 기사무념Snapshot Snapshot(기사무념State state, 기사무념관측 seen)
        {
            var day = state.Day > 0 ? state.Day : 1;
            var snapshot = new 기사무념Snapshot { Day = day, Observed = seen != null };
            var now = seen == null ? (double?)null : seen.NowMinutes;
            // 화면이 기사를 그리는 순서(음식 배달의 기사 순서)를 따른다. 관측이 없으면 저장된 순서다.
            var ids = seen == null ? state.Riders.Select(item => item.Id).ToList() : seen.CourierIds;
            var lives = new Dictionary<string, 기사무념Live>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || lives.ContainsKey(id)) continue;
                var rider = Rider(state, id);
                var live = Live(state, id, day, now);
                lives[id] = live;
                var hasNext = rider.Grade + 1 < GradeNames.Length;
                snapshot.Riders.Add(new 기사무념RiderView
                {
                    Id = id,
                    Grade = rider.Grade,
                    GradeName = GradeNames[rider.Grade],
                    Capacity = rider.Capacity,
                    TarotId = rider.TarotId,
                    TarotName = rider.TarotName,
                    ValidDays = rider.ValidDays,
                    Qualifying = rider.Qualifying,
                    CheckedToday = rider.CheckedDay == day,
                    LastDayResult = rider.LastDayResult ?? string.Empty,
                    HasNextGrade = hasNext,
                    NextGradeName = hasNext ? GradeNames[rider.Grade + 1] : string.Empty,
                    NextGradeMinutes = hasNext ? GradeMinutes[rider.Grade + 1] : 0,
                    Used = live.Used,
                    Maintained = live.Maintained,
                    Remaining = live.Remaining,
                    Broken = live.Broken,
                    Working = live.Working,
                    Settled = live.Settled,
                    Pay = live.Pay,
                    Linked = live.Linked,
                    Low = !live.Broken && live.Remaining < TypicalTask + Margin,
                });
                snapshot.MaintainedMinutes += live.Maintained;
                snapshot.LinkedIncome += live.Linked;
                if (live.Broken) snapshot.BrokenCount++;
            }
            if (seen != null) Flow(snapshot, seen, lives);
            return snapshot;
        }

        // r134: 오늘 주문(소비)·매장 준비 여력(생산)·배송 여력(연결)을 비교한다.
        private static void Flow(기사무념Snapshot snapshot, 기사무념관측 seen, Dictionary<string, 기사무념Live> lives)
        {
            var orders = seen.Orders.Count;
            var prepare = seen.StoreCount;
            var deliver = 0;
            foreach (var pair in lives)
            {
                var busy = seen.Orders.Any(order => order.DriverId == pair.Key);
                if (busy || !pair.Value.Broken && pair.Value.Remaining >= TypicalTask + Margin) deliver++;
            }
            var limit = Math.Min(orders, Math.Min(prepare, deliver));
            string focus;
            if (limit < orders && deliver <= prepare) focus = "배송 여력이 부족합니다. 기사의 경 시간과 휴식을 보세요.";
            else if (limit < orders) focus = "준비 여력이 부족합니다. 제때 준비할 매장을 더 살펴봅니다.";
            else if (prepare == orders && deliver == orders) focus = "준비·주문·배송이 맞습니다. 수령과 정산을 확인하세요.";
            else focus = "주문보다 여력이 큽니다. 수요를 더 연결할 여지가 있습니다.";
            snapshot.FlowOrders = orders;
            snapshot.FlowPrepare = prepare;
            snapshot.FlowDeliver = deliver;
            snapshot.FlowLimit = limit;
            snapshot.FlowFocus = focus;
        }

        /// <summary>"2026-11-12T11:30:05" 또는 "11:30:05"를 하루 안의 분으로 바꾼다.</summary>
        public static double ClockMinutes(string clock)
        {
            if (string.IsNullOrEmpty(clock)) return 0;
            var text = clock;
            var separator = text.IndexOf('T');
            if (separator >= 0) text = text.Substring(separator + 1);
            var parts = text.Split(':');
            if (parts.Length < 2) return 0;
            int hours, minutes;
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out hours);
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes);
            double seconds = 0;
            if (parts.Length > 2)
                double.TryParse(parts[2].TrimEnd('Z'), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
            return hours * 60 + minutes + seconds / 60;
        }

        public static string Duration(double minutes)
        {
            var seconds = (int)Math.Round(Math.Max(0, minutes) * 60);
            return seconds % 60 == 0
                ? seconds / 60 + "분"
                : seconds / 60 + "분 " + (seconds % 60).ToString("00", CultureInfo.InvariantCulture) + "초";
        }

        public static string Letter(string id)
        {
            return string.IsNullOrEmpty(id) ? "?" : id.Substring(id.Length - 1).ToUpperInvariant();
        }

        private static int OrderDay(string orderId, int fallback)
        {
            if (orderId.StartsWith("day-", StringComparison.Ordinal))
            {
                var colon = orderId.IndexOf(':');
                int day;
                if (colon > 4 && int.TryParse(orderId.Substring(4, colon - 4), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out day)) return day;
            }
            return fallback;
        }

        private static int? ParseWon(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var digits = new string(text.Where(char.IsDigit).ToArray());
            int value;
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0
                ? value : (int?)null;
        }
    }
}
