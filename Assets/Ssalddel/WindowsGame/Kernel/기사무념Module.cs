using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 기사 무념·경 시간·등급을 맡는 게임 층. 업무 OS가 아니다(영역과 같은 자리).
    /// 음식 배달 모듈의 상태 사본을 읽기만 하고, 음식 배달에 명령하지 않는다. 규칙은 기사무념Policy에 있다.
    /// 상태는 커널 저장(kernelJson)에 함께 담긴다. 예전 저장에는 이 칸이 없어 그때는 처음 값으로 시작한다.
    /// 명령: check(출발 전 점검, rider). 미리보기: assign(배차해도 되는가, rider).
    /// </summary>
    public sealed class 기사무념Module : IBusinessModule
    {
        public const string Id = "RiderMindGrowth";

        private 기사무념State state = new 기사무념State();
        private GameKernel kernel;
        private string observedReply;
        private 기사무념관측 seen;
        private 기사무념Snapshot cached;

        public string OsId { get { return Id; } }

        public void Start(ModuleContext context)
        {
            kernel = context == null ? null : context.Kernel;
            state = new 기사무념State();
            Forget();
        }

        public void Tick(long elapsedMs)
        {
            Observe();
        }

        public GamePreview Preview(GameCommand command)
        {
            Observe();
            if (command == null || command.Action != "assign")
                return GamePreview.Blocked("알 수 없는 미리보기입니다.", "UnknownAction");
            var rider = command.Value("rider");
            if (state.Day < 0 || state.FindRider(rider) == null)
                return new GamePreview { CanConfirm = true, BlockCode = 기사무념배차판정.Ok };
            var live = 기사무념Policy.Live(state, rider, state.Day, seen == null ? (double?)null : seen.NowMinutes);
            var verdict = 기사무념Policy.Assignable(live);
            return new GamePreview
            {
                CanConfirm = verdict != 기사무념배차판정.Broken,
                BlockCode = verdict,
                Message = verdict == 기사무념배차판정.Broken
                    ? "무념 상태가 깨졌습니다. 오늘은 새 배차를 받지 않고 휴식합니다."
                    : verdict == 기사무념배차판정.Low
                        ? "경 " + 기사무념Policy.Duration(live.Remaining) + " 남음 · 예상 업무와 여유 1분보다 적어 무념이 깨질 수 있습니다."
                        : string.Empty,
            };
        }

        public GameResult Confirm(GameCommand command)
        {
            Observe();
            if (command == null || command.Action != "check")
                return GameResult.Fail("알 수 없는 명령입니다.", "UnknownAction");
            var rider = command.Value("rider");
            if (state.Day < 0) return GameResult.Fail("회차가 아직 열리지 않았습니다.", "NoRound");
            if (state.FindRider(rider) == null) return GameResult.Fail("없는 기사입니다.", "UnknownRider");
            if (!기사무념Policy.Check(state, rider))
                return new GameResult { Ok = true, Code = "AlreadyChecked", Message = "오늘은 이미 점검했습니다." };
            cached = null;
            return GameResult.Done("출발 전 점검 · 지금 마음을 살피고 무념으로 하루를 시작합니다.");
        }

        public object Snapshot()
        {
            Observe();
            return cached ?? (cached = 기사무념Policy.Snapshot(state, seen));
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            return HandoffReceipt.Refused("기사 무념은 인계를 받지 않습니다.");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state, Formatting.None);
        }

        public void Restore(string saved)
        {
            try
            {
                var restored = JsonConvert.DeserializeObject<기사무념State>(saved);
                if (restored == null || restored.Schema != 기사무념State.CurrentSchema) return;
                restored.Riders = restored.Riders ?? new List<기사무념Rider>();
                restored.Works = restored.Works ?? new List<기사무념Work>();
                restored.Riders.RemoveAll(item => item == null || string.IsNullOrEmpty(item.Id));
                restored.Works.RemoveAll(item => item == null || string.IsNullOrEmpty(item.OrderId));
                foreach (var rider in restored.Riders)
                {
                    if (rider.Grade < 0) rider.Grade = 0;
                    if (rider.Grade >= 기사무념Policy.GradeNames.Length) rider.Grade = 기사무념Policy.GradeNames.Length - 1;
                }
                state = restored;
            }
            catch (JsonException)
            {
                // 읽을 수 없는 저장은 버리고 처음 값으로 둔다.
            }
            Forget();
        }

        // 다음에 볼 때 음식 배달의 상태를 다시 읽게 한다.
        private void Forget()
        {
            observedReply = null;
            seen = null;
            cached = null;
        }

        // 음식 배달의 마지막 응답이 바뀌었을 때만 다시 읽는다. 상태가 없는 응답(후보 조회 등)은 넘긴다.
        private void Observe()
        {
            var food = kernel == null ? null : kernel.Module<FoodDeliveryModule>();
            var reply = food == null ? null : food.LastReplyJson;
            if (string.IsNullOrEmpty(reply) || ReferenceEquals(reply, observedReply)) return;
            observedReply = reply;
            var next = Read(reply);
            if (next == null) return;
            seen = next;
            기사무념Policy.Observe(state, seen);
            cached = null;
        }

        private static 기사무념관측 Read(string replyJson)
        {
            JObject root;
            try
            {
                // 시각 글자를 날짜 값으로 바꾸지 않고 그대로 읽는다(시간대 변환을 피한다).
                using (var reader = new JsonTextReader(new StringReader(replyJson)) { DateParseHandling = DateParseHandling.None })
                    root = JObject.Load(reader);
            }
            catch (JsonException)
            {
                return null;
            }
            var body = root["state"] as JObject;
            if (body == null) return null;
            var campaign = body["campaign"] as JObject;
            var seen = new 기사무념관측
            {
                Day = campaign == null ? 1 : (int?)campaign["day"] ?? 1,
                NowMinutes = 기사무념Policy.ClockMinutes((string)body["now"]),
                StoreCount = body["stores"] is JArray stores ? stores.Count : 0,
            };
            if (body["couriers"] is JArray couriers)
                foreach (var courier in couriers)
                {
                    var id = courier is JObject ? (string)courier["id"] : null;
                    if (!string.IsNullOrEmpty(id)) seen.CourierIds.Add(id);
                }
            if (body["orders"] is JArray orders)
                foreach (var order in orders)
                {
                    if (!(order is JObject)) continue;
                    seen.Orders.Add(new 기사무념관측Order
                    {
                        Id = (string)order["id"] ?? string.Empty,
                        DriverId = (string)order["driverId"] ?? string.Empty,
                        Settled = (string)order["stage"] == "Settled",
                    });
                }
            if (body["events"] is JArray events)
                foreach (var entry in events)
                {
                    if (!(entry is JObject)) continue;
                    seen.Events.Add(new 기사무념관측Event
                    {
                        OrderId = (string)entry["orderId"] ?? string.Empty,
                        Code = (string)entry["code"] ?? string.Empty,
                        AtMinutes = 기사무념Policy.ClockMinutes((string)entry["at"]),
                        Text = (string)entry["text"] ?? string.Empty,
                    });
                }
            return seen;
        }
    }
}
