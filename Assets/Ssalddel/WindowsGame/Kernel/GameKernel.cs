using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 게임 커널. 가상 시각, 명령의 중복 방지, 인계 전달, 공통 원장, 저장을 맡는다.
    /// 사업 규칙은 모르고 모듈에 넘긴다.
    /// </summary>
    public sealed class GameKernel
    {
        public const string SaveSchema = "hongdal-kernel-save.v1";

        private readonly List<IBusinessModule> modules = new List<IBusinessModule>();
        private readonly Dictionary<string, IBusinessModule> byId = new Dictionary<string, IBusinessModule>(StringComparer.Ordinal);
        private readonly List<Handoff> handoffs = new List<Handoff>();
        private readonly Queue<Handoff> outbox = new Queue<Handoff>();
        private readonly List<LedgerEntry> ledger = new List<LedgerEntry>();
        private readonly Dictionary<string, AppliedCommand> applied = new Dictionary<string, AppliedCommand>(StringComparer.Ordinal);
        private readonly List<Signal> signals = new List<Signal>();
        private readonly List<Signal> resolvedSignals = new List<Signal>();
        private const int ResolvedSignalsKept = 32;
        private bool draining;

        public long ElapsedMs { get; private set; }
        /// <summary>
        /// 지금의 현실 시각(UTC 밀리초). 커널 밖(화면)에서 넣어 준다. 원장 줄에 함께 적기만 하고 가상 시각과 규칙에는 쓰지 않는다.
        /// 넣지 않으면 0이고 그때 적은 줄은 날짜 미연결로 읽힌다. 저장에는 담지 않는다 (r174 일일 결산).
        /// </summary>
        public long RealNowUtcMs { get; set; }
        public long Seed { get; private set; }
        public IReadOnlyList<IBusinessModule> Modules { get { return modules; } }
        public IReadOnlyList<Handoff> Handoffs { get { return handoffs; } }
        public IReadOnlyList<LedgerEntry> Ledger { get { return ledger; } }
        /// <summary>지금 열려 있는 신호.</summary>
        public IReadOnlyList<Signal> Signals { get { return signals; } }
        /// <summary>최근에 닫힌 신호. 화면이 "해결됨"을 한 번 보여 줄 때 쓴다.</summary>
        public IReadOnlyList<Signal> ResolvedSignals { get { return resolvedSignals; } }

        public void Register(IBusinessModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (byId.ContainsKey(module.OsId)) throw new InvalidOperationException("같은 사업을 두 번 등록했습니다: " + module.OsId);
            modules.Add(module);
            byId.Add(module.OsId, module);
        }

        public IBusinessModule Find(string osId)
        {
            IBusinessModule module;
            return osId != null && byId.TryGetValue(osId, out module) ? module : null;
        }

        public T Module<T>() where T : class, IBusinessModule
        {
            return modules.OfType<T>().FirstOrDefault();
        }

        /// <summary>새 회차를 연다. 인계·원장·명령 기록을 비우고 모든 모듈을 처음 상태로 돌린다.</summary>
        public void Start(long seed)
        {
            Seed = seed;
            ElapsedMs = 0;
            handoffs.Clear();
            outbox.Clear();
            ledger.Clear();
            applied.Clear();
            signals.Clear();
            resolvedSignals.Clear();
            var context = new ModuleContext(this, seed);
            foreach (var module in modules) module.Start(context);
            Drain();
        }

        /// <summary>가상 시각을 진행한다. 시각의 기준 모듈이 받지 않으면 false를 돌려주고 나머지는 진행하지 않는다.</summary>
        public bool Tick(long elapsedMs)
        {
            foreach (var module in modules)
            {
                module.Tick(elapsedMs);
                var clock = module as IClockAuthority;
                if (clock != null && !clock.LastTickAccepted) return false;
            }
            if (elapsedMs > ElapsedMs) ElapsedMs = elapsedMs;
            Drain();
            return true;
        }

        public GamePreview Preview(GameCommand command)
        {
            var module = Find(command == null ? null : command.OsId);
            if (module == null) return GamePreview.Blocked("아직 열리지 않은 사업입니다.", "BusinessNotRegistered");
            return module.Preview(command);
        }

        public GameResult Confirm(GameCommand command)
        {
            var module = Find(command == null ? null : command.OsId);
            if (module == null) return GameResult.Fail("아직 열리지 않은 사업입니다.", "BusinessNotRegistered");
            var remember = !string.IsNullOrEmpty(command.CommandId) && !(module is ISelfIdempotent);
            var key = command.OsId + "\n" + command.CommandId;
            if (remember)
            {
                AppliedCommand previous;
                if (applied.TryGetValue(key, out previous))
                {
                    return previous.Signature == command.Signature()
                        ? previous.Result.AsReplay()
                        : GameResult.Fail("같은 명령 번호로 다른 내용을 보냈습니다.", "CommandPayloadConflict");
                }
            }
            var result = module.Confirm(command) ?? GameResult.Fail("사업이 응답하지 않았습니다.", "NoResult");
            if (remember && result.Ok)
                applied[key] = new AppliedCommand { Signature = command.Signature(), Result = result };
            Drain();
            return result;
        }

        /// <summary>인계를 낸다. 같은 ID의 인계는 한 번만 전달한다.</summary>
        public void Post(Handoff handoff)
        {
            if (handoff == null || string.IsNullOrEmpty(handoff.Id)) throw new ArgumentException("인계에는 ID가 있어야 합니다.");
            if (handoffs.Any(item => item.Id == handoff.Id)) return;
            handoff.State = HandoffStates.Posted;
            handoffs.Add(handoff);
            outbox.Enqueue(handoff);
        }

        public void Record(LedgerEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.RealAtUtcMs == 0) entry.RealAtUtcMs = RealNowUtcMs;
            ledger.Add(entry);
        }

        /// <summary>
        /// 신호를 올린다. 같은 ID가 열려 있으면 내용만 고치고 처음 올린 시각은 그대로 둔다.
        /// 주의에서 위험으로 올라가면 그 시각을 EscalatedMs에 적는다.
        /// </summary>
        public Signal RaiseSignal(Signal signal)
        {
            if (signal == null || string.IsNullOrEmpty(signal.Id)) throw new ArgumentException("신호에는 ID가 있어야 합니다.");
            var open = FindSignal(signal.Id);
            if (open == null)
            {
                signal.ResolvedMs = 0;
                signal.EscalatedMs = signal.IsDanger ? signal.SinceMs : 0;
                signals.Add(signal);
                return signal;
            }
            if (signal.IsDanger && !open.IsDanger) open.EscalatedMs = signal.SinceMs;
            if (!signal.IsDanger) open.EscalatedMs = 0;
            open.OsId = signal.OsId;
            open.PlaceId = signal.PlaceId;
            open.Severity = signal.Severity;
            open.ReasonCode = signal.ReasonCode;
            open.Title = signal.Title;
            open.Message = signal.Message;
            open.ActionLabel = signal.ActionLabel;
            return open;
        }

        /// <summary>신호를 닫는다. 열려 있지 않으면 아무 일도 하지 않는다.</summary>
        public bool ClearSignal(string id, long atMs)
        {
            var open = FindSignal(id);
            if (open == null) return false;
            signals.Remove(open);
            open.ResolvedMs = Math.Max(atMs, open.SinceMs);
            resolvedSignals.Add(open);
            if (resolvedSignals.Count > ResolvedSignalsKept) resolvedSignals.RemoveAt(0);
            return true;
        }

        public Signal FindSignal(string id)
        {
            foreach (var signal in signals)
                if (signal.Id == id) return signal;
            return null;
        }

        /// <summary>원장에서 한 곳·한 품목·한 칸의 합을 읽는다.</summary>
        public decimal Balance(string kind, string placeId, string itemId, string bucket)
        {
            var total = 0m;
            foreach (var entry in ledger)
            {
                if (entry.Kind == kind && entry.PlaceId == placeId && entry.ItemId == itemId && entry.Bucket == bucket)
                    total += entry.Amount;
            }
            return total;
        }

        public string Save()
        {
            var states = new JObject();
            foreach (var module in modules)
            {
                var saved = module.Save();
                if (saved != null) states[module.OsId] = saved;
            }
            var root = new JObject
            {
                ["schema"] = SaveSchema,
                ["seed"] = Seed,
                ["elapsedMs"] = ElapsedMs,
                ["handoffs"] = JArray.FromObject(handoffs),
                ["ledger"] = JArray.FromObject(ledger),
                ["applied"] = JObject.FromObject(applied),
                ["signals"] = JArray.FromObject(signals),
                ["resolvedSignals"] = JArray.FromObject(resolvedSignals),
                ["modules"] = states,
            };
            return root.ToString(Formatting.None);
        }

        /// <summary>저장을 되살린다. 읽을 수 없으면 false를 돌려주고 지금 상태를 그대로 둔다.</summary>
        public bool Restore(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return false;
            }
            if ((string)root["schema"] != SaveSchema) return false;
            try
            {
                var restoredHandoffs = root["handoffs"] == null ? new List<Handoff>() : root["handoffs"].ToObject<List<Handoff>>();
                var restoredLedger = root["ledger"] == null ? new List<LedgerEntry>() : root["ledger"].ToObject<List<LedgerEntry>>();
                var restoredApplied = root["applied"] == null
                    ? new Dictionary<string, AppliedCommand>()
                    : root["applied"].ToObject<Dictionary<string, AppliedCommand>>();
                // 신호가 없던 때의 저장본에는 이 칸이 없다. 그때는 빈 목록으로 시작하고 다음 시각 진행에서 다시 올라온다.
                var restoredSignals = root["signals"] == null ? new List<Signal>() : root["signals"].ToObject<List<Signal>>();
                var restoredResolved = root["resolvedSignals"] == null ? new List<Signal>() : root["resolvedSignals"].ToObject<List<Signal>>();
                var states = root["modules"] as JObject ?? new JObject();

                Seed = (long?)root["seed"] ?? 0;
                ElapsedMs = (long?)root["elapsedMs"] ?? 0;
                // 스스로 저장하는 모듈(음식 배달)은 건드리지 않는다. 나머지는 처음 상태로 돌린 뒤 저장본을 얹는다.
                var targets = modules.Where(module => module.Save() != null).ToList();
                var context = new ModuleContext(this, Seed);
                foreach (var module in targets) module.Start(context);
                // Start가 적은 기초 원장과 인계는 버린다. 저장본이 이미 갖고 있다.
                handoffs.Clear();
                handoffs.AddRange(restoredHandoffs ?? new List<Handoff>());
                outbox.Clear();
                ledger.Clear();
                ledger.AddRange(restoredLedger ?? new List<LedgerEntry>());
                applied.Clear();
                if (restoredApplied != null)
                    foreach (var pair in restoredApplied) applied[pair.Key] = pair.Value;
                signals.Clear();
                signals.AddRange(restoredSignals ?? new List<Signal>());
                resolvedSignals.Clear();
                resolvedSignals.AddRange(restoredResolved ?? new List<Signal>());
                foreach (var module in targets)
                {
                    var saved = (string)states[module.OsId];
                    if (!string.IsNullOrEmpty(saved)) module.Restore(saved);
                }
                outbox.Clear();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private void Drain()
        {
            if (draining) return;
            draining = true;
            try
            {
                var guard = 0;
                while (outbox.Count > 0 && guard++ < 512)
                {
                    var handoff = outbox.Dequeue();
                    var target = Find(handoff.ToOsId);
                    if (target == null)
                    {
                        handoff.State = HandoffStates.Held;
                        handoff.StateNote = "받는 사업이 아직 열리지 않았습니다.";
                        continue;
                    }
                    var receipt = target.Accept(handoff) ?? HandoffReceipt.Held("받는 사업이 응답하지 않았습니다.");
                    handoff.State = receipt.State;
                    handoff.StateNote = receipt.Note ?? string.Empty;
                }
            }
            finally
            {
                draining = false;
            }
        }

        public sealed class AppliedCommand
        {
            public string Signature = string.Empty;
            public GameResult Result = new GameResult();
        }
    }
}
