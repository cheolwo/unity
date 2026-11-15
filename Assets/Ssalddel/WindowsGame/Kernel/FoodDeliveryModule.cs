using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.Sagajeong.Core;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 업무 모듈 1호: 음식 배달. 공통 Core(SagajeongSimulationCore)를 감싸기만 한다. 규칙은 바꾸지 않는다.
    /// 명령과 응답은 Core의 JSON 원문 그대로 오간다. 저장도 Core의 save/restore가 그대로 맡는다.
    /// </summary>
    public sealed class FoodDeliveryModule : IBusinessModule, IClockAuthority, ISelfIdempotent
    {
        // 주문이 기사를 기다린 가상 시간이 이만큼 넘으면 신호를 올린다. 배차 마감(6분)보다 앞선다.
        private const long WaitingCautionMs = 120000;
        private const long WaitingDangerMs = 240000;

        private readonly SagajeongSimulationCore core;
        private readonly List<Handoff> held = new List<Handoff>();
        private readonly Dictionary<string, long> waitingSince = new Dictionary<string, long>();
        private GameKernel kernel;

        public FoodDeliveryModule(string deliverySampleJson, string roadGraphJson)
        {
            core = new SagajeongSimulationCore(deliverySampleJson, roadGraphJson);
        }

        public string OsId { get { return BusinessIds.FoodDelivery; } }
        public string LastReplyJson { get; private set; } = string.Empty;
        public bool LastTickAccepted { get; private set; } = true;
        /// <summary>다른 사업이 넘겼지만 Core가 아직 받을 수 없는 준비 완료 기록.</summary>
        public IReadOnlyList<Handoff> HeldHandoffs { get { return held; } }

        public void Start(ModuleContext context)
        {
            // 회차 시작은 Confirm(start | campaign-start | restore)으로 받는다. Core가 스테이지와 방침을 고르기 때문이다.
            held.Clear();
            waitingSince.Clear();
            kernel = context == null ? null : context.Kernel;
            LastTickAccepted = true;
        }

        public void Tick(long elapsedMs)
        {
            // 화면이 JsonUtility로 만들던 요청과 같은 모양으로 보낸다. Core의 명령 기록이 예전과 같아야 저장이 호환된다.
            LastReplyJson = core.Execute(TickRequest(elapsedMs));
            LastTickAccepted = ReadHead(LastReplyJson).ok;
            if (LastTickAccepted) RefreshSignals(elapsedMs);
        }

        // 기사를 오래 기다리는 주문이 있으면 그 매장에서 신호를 올린다. Core의 응답만 읽고 Core는 건드리지 않는다.
        // 기다린 시간은 이 모듈이 처음 본 때부터 잰다(저장을 불러오면 다시 잰다).
        private void RefreshSignals(long elapsedMs)
        {
            if (kernel == null) return;
            WaitingReply reply;
            try
            {
                reply = JsonConvert.DeserializeObject<WaitingReply>(LastReplyJson);
            }
            catch (JsonException)
            {
                return;
            }
            var orders = reply == null || reply.state == null || reply.state.orders == null ? new WaitingOrder[0] : reply.state.orders;
            var open = reply != null && reply.state != null && (reply.state.campaign == null || reply.state.campaign.phase == "open");
            var seen = new HashSet<string>();
            foreach (var order in orders)
            {
                if (order == null || string.IsNullOrEmpty(order.id) || order.stage != "Waiting" || !open) continue;
                seen.Add(order.id);
                long since;
                if (!waitingSince.TryGetValue(order.id, out since))
                {
                    since = elapsedMs;
                    waitingSince[order.id] = since;
                }
                var waited = elapsedMs - since;
                if (waited < WaitingCautionMs) continue;
                var danger = waited >= WaitingDangerMs;
                kernel.RaiseSignal(new Signal
                {
                    Id = "signal:food:waiting:" + order.id, OsId = OsId, PlaceId = "store:" + order.storeId,
                    Severity = danger ? SignalSeverities.Danger : SignalSeverities.Caution,
                    ReasonCode = "dispatch-waiting", Title = string.IsNullOrEmpty(order.storeName) ? "매장" : order.storeName,
                    Message = danger ? "주문이 기사를 오래 기다리고 있습니다. 지금 기사를 지정하세요." : "주문이 기사를 기다리고 있습니다.",
                    ActionLabel = "기사 지정", SinceMs = elapsedMs,
                });
            }
            foreach (var id in waitingSince.Keys.Where(id => !seen.Contains(id)).ToList())
            {
                waitingSince.Remove(id);
                kernel.ClearSignal("signal:food:waiting:" + id, elapsedMs);
            }
            // 저장본에서 넘어온 신호 가운데 지금은 기다리지 않는 주문의 것을 닫는다.
            foreach (var stale in kernel.Signals.Where(item => item.OsId == OsId && item.ReasonCode == "dispatch-waiting" &&
                         !seen.Contains(item.Id.Substring("signal:food:waiting:".Length))).ToList())
                kernel.ClearSignal(stale.Id, elapsedMs);
        }

        public static string TickRequest(long elapsedMs)
        {
            return "{\"action\":\"tick\",\"elapsedMs\":" + elapsedMs.ToString(CultureInfo.InvariantCulture) +
                ",\"orderId\":\"\",\"driverId\":\"\",\"commandId\":\"\",\"revision\":0,\"policy\":\"\"}";
        }

        public GamePreview Preview(GameCommand command)
        {
            LastReplyJson = core.Execute(command.PayloadJson);
            var head = ReadHead(LastReplyJson);
            return new GamePreview { CanConfirm = head.ok, Message = head.message ?? string.Empty, RawJson = LastReplyJson };
        }

        public GameResult Confirm(GameCommand command)
        {
            LastReplyJson = core.Execute(command.PayloadJson);
            var head = ReadHead(LastReplyJson);
            return new GameResult { Ok = head.ok, Message = head.message ?? string.Empty, RawJson = LastReplyJson };
        }

        /// <summary>저장·내보내기처럼 회차를 바꾸지 않는 조회.</summary>
        public string Query(string requestJson)
        {
            return core.Execute(requestJson);
        }

        public object Snapshot()
        {
            return LastReplyJson;
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            if (held.All(item => item.Id != handoff.Id)) held.Add(handoff);
            return HandoffReceipt.Held("음식 배달 Core는 아직 바깥에서 만든 주문을 받지 않습니다. 준비 완료만 적어 둡니다.");
        }

        public string Save()
        {
            return null;
        }

        public void Restore(string saved)
        {
        }

        private static ReplyHead ReadHead(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ReplyHead>(json) ?? new ReplyHead();
            }
            catch (JsonException)
            {
                return new ReplyHead();
            }
        }

        private sealed class ReplyHead
        {
            public bool ok = false;
            public string message = null;
        }

        private sealed class WaitingReply
        {
            public WaitingState state = null;
        }

        private sealed class WaitingState
        {
            public WaitingOrder[] orders = null;
            public WaitingCampaign campaign = null;
        }

        private sealed class WaitingOrder
        {
            public string id = null;
            public string storeId = null;
            public string storeName = null;
            public string stage = null;
        }

        private sealed class WaitingCampaign
        {
            public string phase = null;
        }
    }
}
