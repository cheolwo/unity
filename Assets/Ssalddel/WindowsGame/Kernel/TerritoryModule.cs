using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-12 넓어지는 영역(G3a·b). 동 하나가 판 하나다.
    // 2026-11-13 판의 단위를 행정동으로 통일했다(팩 unit = "hjd"). 모듈은 단위를 모른다. 팩의 코드·맞닿음·자리만 읽는다.
    //  - 행정동 팩은 처음부터 운영하는 동을 여럿 둘 수 있다(startDongs). 기존 가상 매장이 놓인 동들이다.
    //  - 지어낸 사건을 끌 수 있다(rules.eventEveryMs = 0). 그때 신호는 동별 수요 모듈이 올리고, 이 모듈은 놓친 위험만 어둡게 한다.
    //  - 주문을 받아내지 못하는 동은 깊어지지 않는다(ITerritoryGate).
    //  - 맞닿아야 간다: 운영 이상인 동과 경계를 맞댄 동만 진출할 수 있다.
    //  - 밝기는 운영의 깊이다: 진출 중 → 운영 → 자리 잡음. 위험 신호가 열려 있는 동안에는 깊어지지 않는다.
    //  - 위험 신호를 놓치면 한 단계 어두워진다. 회색(미지)으로는 돌아가지 않는다.
    // 조건과 시간은 모두 팩의 rules에 있는 임시 값이다(Claude 초안). 기획 판본이 정하면 팩만 바꾼다.
    // 홈 동(면목동)의 표식은 건물 역할표의 실제 자리이고, 그 밖의 동은 가상 매장이다. 가상 매장에는 아직 주문·정산이 없다.

    public static class TerritoryLevels
    {
        public const int Unknown = 0;
        public const int Entering = 1;
        public const int Operating = 2;
        public const int Settled = 3;

        public static string Name(int level)
        {
            return level >= Settled ? "자리 잡음" : level == Operating ? "운영" : level == Entering ? "진출 중" : "미지";
        }
    }

    public static class TerritoryPinKinds
    {
        public const string Branch = "branch";
        public const string Food = "food";
        public const string Cafe = "cafe";
        public const string Store = "store";
    }

    public sealed class TerritoryRules
    {
        public long operateAfterMs = 1200000;    // 진출하고 이만큼(가상) 돌아가면 운영
        public long settleAfterMs = 5400000;     // 운영하고 이만큼 더 돌아가면 자리 잡음
        public int maxEntering = 1;              // 한꺼번에 진출 중일 수 있는 동의 수
        public long eventEveryMs = 1500000;      // 가상 매장에서 신호가 생기는 간격(여기에 절반까지 더해진다)
        public long escalateAfterMs = 600000;    // 주의를 이만큼 두면 위험
        public long neglectAfterMs = 900000;     // 위험을 이만큼 두면 한 단계 어두워진다
        public long maxStepMs = 60000;           // 시각이 크게 건너뛰어도 한 번에 쌓이는 운영 시간
    }

    public sealed class TerritoryPackPin
    {
        public string id = string.Empty;
        public string kind = string.Empty;
        public string placeId = string.Empty;
        public string title = string.Empty;
        public double x;
        public double y;
    }

    public sealed class TerritoryPackDong
    {
        public string code = string.Empty;
        public string name = string.Empty;
        public string sig = string.Empty;
        public string sigName = string.Empty;
        public double x;
        public double y;
        public List<string> near = new List<string>();
        public List<double> spots = new List<double>();
        /// <summary>행정동 팩에서만: 이 행정동이 속한 법정동 코드. 구 지도의 법정동 판을 밝히는 데 쓴다.</summary>
        public string legal = string.Empty;
        /// <summary>행정동 팩에서만: 행정동 모듈의 지역 ID("region:kr:hjd:…").</summary>
        public string region = string.Empty;
    }

    public sealed class TerritoryPackStart
    {
        public string code = string.Empty;
        public List<TerritoryPackPin> pins = new List<TerritoryPackPin>();
    }

    public sealed class TerritoryPack
    {
        public const string Schema = "hongdal-territory.v1";
        public string schema = string.Empty;
        /// <summary>판의 단위. 비어 있으면 법정동(예전 팩), "hjd"면 행정동.</summary>
        public string unit = string.Empty;
        public string label = string.Empty;
        public string homeDong = string.Empty;
        public TerritoryRules rules = new TerritoryRules();
        public List<TerritoryPackPin> homePins = new List<TerritoryPackPin>();
        /// <summary>처음부터 운영하는 동들. 비어 있으면 homeDong 하나다.</summary>
        public List<TerritoryPackStart> startDongs = new List<TerritoryPackStart>();
        public List<TerritoryPackDong> dongs = new List<TerritoryPackDong>();

        public bool IsStart(string code)
        {
            if (code == homeDong) return true;
            foreach (var start in startDongs)
                if (start.code == code) return true;
            return false;
        }

        public static TerritoryPack Parse(string json)
        {
            var pack = JsonConvert.DeserializeObject<TerritoryPack>(json);
            if (pack == null || pack.schema != Schema) throw new InvalidOperationException("영역 팩의 형식이 다릅니다.");
            if (pack.rules == null) pack.rules = new TerritoryRules();
            if (pack.homePins == null) pack.homePins = new List<TerritoryPackPin>();
            if (pack.startDongs == null) pack.startDongs = new List<TerritoryPackStart>();
            if (pack.dongs == null || pack.dongs.Count == 0) throw new InvalidOperationException("영역 팩에 동이 없습니다.");
            if (pack.Find(pack.homeDong) == null) throw new InvalidOperationException("영역 팩에 시작하는 동이 없습니다.");
            foreach (var start in pack.startDongs)
            {
                if (pack.Find(start.code) == null) throw new InvalidOperationException("영역 팩의 처음 동이 목록에 없습니다: " + start.code);
                if (start.pins == null) start.pins = new List<TerritoryPackPin>();
            }
            return pack;
        }

        public TerritoryPackDong Find(string code)
        {
            foreach (var dong in dongs)
                if (dong.code == code) return dong;
            return null;
        }
    }

    public sealed class TerritoryPinState
    {
        public string Id = string.Empty;
        public string Kind = string.Empty;
        public string PlaceId = string.Empty;
        public string Title = string.Empty;
        public double X;
        public double Y;
        public long AtMs;
    }

    public sealed class TerritoryDongState
    {
        public string Code = string.Empty;
        public int Level;
        /// <summary>지금까지 오른 가장 높은 단계. 어두워졌다가 다시 올라도 표식을 또 꽂지 않는다.</summary>
        public int MaxLevel;
        public long OperatedMs;
        public long EnteredAtMs;
        public long LevelAtMs;
        public long DimmedAtMs;
        public long LastEventMs;
        public int Events;
        public List<TerritoryPinState> Pins = new List<TerritoryPinState>();
    }

    public sealed class TerritoryDongView
    {
        public string Code = string.Empty;
        public string Name = string.Empty;
        public string Sig = string.Empty;
        public string SigName = string.Empty;
        public double X;
        public double Y;
        public int Level;
        public bool IsHome;
        /// <summary>행정동 팩에서만: 속한 법정동 코드와 모듈 지역 ID.</summary>
        public string Legal = string.Empty;
        public string Region = string.Empty;
        public bool Candidate;
        public bool CanEnter;
        public string EnterBlock = string.Empty;
        /// <summary>다음 단계까지 온 정도(0~1). 자리 잡은 동은 1.</summary>
        public double Progress;
        public long EnteredAtMs;
        public long LevelAtMs;
        public long DimmedAtMs;
        public List<TerritoryPinState> Pins = new List<TerritoryPinState>();
        public Signal Signal;
    }

    public sealed class TerritorySnapshot
    {
        /// <summary>판의 단위. 비어 있으면 법정동, "hjd"면 행정동.</summary>
        public string Unit = string.Empty;
        public List<TerritoryDongView> Dongs = new List<TerritoryDongView>();
        public int Operating;
        public int Entering;
        public int Branches;
        public int Foods;
        public int Cafes;
        public int Stores;
        public int MaxEntering;
        public long NowMs;
        public string LastNote = string.Empty;
        public long LastNoteAtMs;

        public TerritoryDongView Find(string code)
        {
            foreach (var dong in Dongs)
                if (dong.Code == code) return dong;
            return null;
        }
    }

    public sealed class TerritoryModule : IBusinessModule, ITerritoryView
    {
        private static readonly string[][] EventKinds =
        {
            new[] { "dispatch-backlog", "배차 대기가 늘고 있습니다. 기사를 보내야 합니다.", "배차 대기가 쌓였습니다. 기사가 모자랍니다.", "기사 보내기" },
            new[] { "stock-low", "재료가 곧 떨어집니다. 저녁 주문 전에 채워야 합니다.", "재료가 떨어졌습니다. 주문을 받을 수 없습니다.", "보충 발주" },
            new[] { "kitchen-delay", "조리가 밀리기 시작했습니다.", "조리가 밀려 약속 시간을 넘기고 있습니다.", "주문 잠시 닫기" },
        };

        private readonly TerritoryPack pack;
        private GameKernel kernel;
        private long seed;
        private State state = new State();
        private long now;

        public TerritoryModule(TerritoryPack pack)
        {
            this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        }

        public string OsId { get { return BusinessIds.Territory; } }

        private const string SignalPrefix = "signal:territory:";

        public static string SignalId(string dongCode)
        {
            return SignalPrefix + dongCode;
        }

        public void Start(ModuleContext context)
        {
            kernel = context.Kernel;
            seed = context.Seed;
            now = 0;
            state = new State();
            if (pack.startDongs.Count == 0)
            {
                state.Dongs.Add(StartDong(pack.homeDong, pack.homePins));
                return;
            }
            // 홈 동이 처음 동 목록에 없으면 앞에 둔다. 처음 동들은 모두 운영 단계에서 시작한다.
            if (pack.startDongs.All(item => item.code != pack.homeDong)) state.Dongs.Add(StartDong(pack.homeDong, pack.homePins));
            foreach (var start in pack.startDongs) state.Dongs.Add(StartDong(start.code, start.pins));
        }

        private TerritoryDongState StartDong(string code, List<TerritoryPackPin> pins)
        {
            var dong = new TerritoryDongState
            {
                Code = code, Level = TerritoryLevels.Operating, MaxLevel = TerritoryLevels.Operating,
                OperatedMs = pack.rules.operateAfterMs,
            };
            foreach (var pin in pins)
            {
                dong.Pins.Add(new TerritoryPinState
                {
                    Id = pin.id, Kind = pin.kind, PlaceId = pin.placeId ?? string.Empty, Title = pin.title ?? string.Empty, X = pin.x, Y = pin.y,
                });
            }
            return dong;
        }

        public int LevelOf(string dongCode)
        {
            var dong = Find(dongCode);
            return dong == null ? TerritoryLevels.Unknown : dong.Level;
        }

        public void Tick(long elapsedMs)
        {
            if (elapsedMs <= now) return;
            var step = Math.Min(elapsedMs - now, Math.Max(1L, pack.rules.maxStepMs));
            now = elapsedMs;
            state.NowMs = now;
            if (kernel == null) return;
            var gates = kernel.Modules.OfType<ITerritoryGate>().ToList();
            foreach (var dong in state.Dongs.ToList())
            {
                if (dong.Level < TerritoryLevels.Entering) continue;
                var signal = kernel.FindSignal(SignalId(dong.Code));
                var danger = Danger(dong, signal);
                // 주문을 받아내지 못하는 동은 깊어지지 않는다(동별 수요 모듈이 있을 때).
                if (!danger && gates.All(gate => gate.Deepens(dong.Code))) dong.OperatedMs += step;
                if (dong.Level == TerritoryLevels.Entering && dong.OperatedMs >= pack.rules.operateAfterMs)
                    Promote(dong, TerritoryLevels.Operating);
                else if (dong.Level == TerritoryLevels.Operating && dong.OperatedMs >= pack.rules.operateAfterMs + pack.rules.settleAfterMs)
                    Promote(dong, TerritoryLevels.Settled);
                // 처음 동의 신호는 실제 운영(배차·재고·검수)에서 온다. 그 밖의 동만 여기서 사건을 다룬다.
                if (!pack.IsStart(dong.Code)) Events(dong, signal);
            }
            // 주인 없는 영역 신호는 닫는다. 팩의 단위가 바뀐 저장(법정동 → 행정동)에는 지금 판에 없는 동의 신호가 남아 있을 수 있다.
            foreach (var stale in kernel.Signals.Where(item => item.OsId == OsId && item.Id != null &&
                item.Id.StartsWith(SignalPrefix, StringComparison.Ordinal)).ToList())
            {
                var owner = Find(stale.Id.Substring(SignalPrefix.Length));
                if (owner == null || owner.Level < TerritoryLevels.Entering) kernel.ClearSignal(stale.Id, now);
            }
        }

        // 처음 동은 실제 운영에서 온 위험 신호가 열려 있으면 깊어지지 않는다.
        //  - 예전 팩(홈 하나): 동 신호가 아닌 위험이 하나라도 있으면.
        //  - 처음 동이 여럿인 팩: 그 동의 표식 자리(placeId)에서 난 위험이 있으면.
        private bool Danger(TerritoryDongState dong, Signal signal)
        {
            if (signal != null && signal.IsDanger) return true;
            if (!pack.IsStart(dong.Code)) return false;
            if (pack.startDongs.Count == 0)
                return kernel.Signals.Any(item => item.IsDanger && !item.PlaceId.StartsWith("dong:", StringComparison.Ordinal));
            return kernel.Signals.Any(item => item.IsDanger && !string.IsNullOrEmpty(item.PlaceId) &&
                dong.Pins.Any(pin => pin.PlaceId == item.PlaceId));
        }

        public GamePreview Preview(GameCommand command)
        {
            var code = command.Value("dong");
            if (command.Action == "enter")
            {
                var problem = EnterProblem(code);
                var info = pack.Find(code);
                return new GamePreview
                {
                    CanConfirm = problem == null,
                    Message = problem ?? info.name + "에 진출합니다. 첫 음식점이 들어섭니다.",
                    BlockCode = problem == null ? string.Empty : "EnterBlocked",
                };
            }
            if (command.Action == "respond")
            {
                var open = kernel == null ? null : kernel.FindSignal(SignalId(code));
                return open == null ? GamePreview.Blocked("처리할 신호가 없습니다.", "NoSignal")
                    : new GamePreview { CanConfirm = true, Message = open.Title + " · " + open.Message };
            }
            return GamePreview.Blocked("지원하지 않는 행동입니다.", "UnknownAction");
        }

        public GameResult Confirm(GameCommand command)
        {
            if (command.ElapsedMs > now)
            {
                now = command.ElapsedMs;
                state.NowMs = now;
            }
            var code = command.Value("dong");
            if (command.Action == "enter")
            {
                var problem = EnterProblem(code);
                if (problem != null) return GameResult.Fail(problem, "EnterBlocked");
                var info = pack.Find(code);
                var dong = new TerritoryDongState
                {
                    Code = code, Level = TerritoryLevels.Entering, MaxLevel = TerritoryLevels.Entering,
                    EnteredAtMs = now, LevelAtMs = now, LastEventMs = now,
                };
                state.Dongs.Add(dong);
                AddPin(dong, info, TerritoryPinKinds.Food);
                Note(info.name + "에 진출했습니다. 첫 음식점이 들어섰습니다.");
                return GameResult.Done(state.LastNote);
            }
            if (command.Action == "respond")
            {
                var open = kernel == null ? null : kernel.FindSignal(SignalId(code));
                var dong = Find(code);
                if (open == null || dong == null) return GameResult.Fail("처리할 신호가 없습니다.", "NoSignal");
                var label = string.IsNullOrEmpty(open.ActionLabel) ? "처리" : open.ActionLabel;
                kernel.ClearSignal(open.Id, now);
                dong.LastEventMs = now;
                dong.Events++;
                Note(open.Title + " — " + label + " · 신호를 껐습니다.");
                return GameResult.Done(state.LastNote);
            }
            return GameResult.Fail("지원하지 않는 행동입니다.", "UnknownAction");
        }

        public object Snapshot()
        {
            var snapshot = new TerritorySnapshot
            {
                Unit = pack.unit ?? string.Empty,
                MaxEntering = pack.rules.maxEntering, NowMs = now, LastNote = state.LastNote ?? string.Empty, LastNoteAtMs = state.LastNoteAtMs,
            };
            foreach (var info in pack.dongs)
            {
                var dong = Find(info.code);
                var level = dong == null ? TerritoryLevels.Unknown : dong.Level;
                var problem = level == TerritoryLevels.Unknown ? EnterProblem(info.code) : "이미 진출한 동입니다.";
                var view = new TerritoryDongView
                {
                    Code = info.code, Name = info.name, Sig = info.sig, SigName = info.sigName, X = info.x, Y = info.y,
                    Level = level, IsHome = pack.IsStart(info.code), Legal = info.legal ?? string.Empty, Region = info.region ?? string.Empty,
                    Candidate = level == TerritoryLevels.Unknown && IsCandidate(info),
                    CanEnter = problem == null, EnterBlock = problem ?? string.Empty,
                    Progress = Progress(dong),
                    EnteredAtMs = dong == null ? 0 : dong.EnteredAtMs, LevelAtMs = dong == null ? 0 : dong.LevelAtMs,
                    DimmedAtMs = dong == null ? 0 : dong.DimmedAtMs,
                    Signal = kernel == null ? null : kernel.FindSignal(SignalId(info.code)),
                };
                if (dong != null) view.Pins.AddRange(dong.Pins);
                snapshot.Dongs.Add(view);
                if (level >= TerritoryLevels.Operating) snapshot.Operating++;
                else if (level == TerritoryLevels.Entering) snapshot.Entering++;
                foreach (var pin in view.Pins)
                {
                    if (pin.Kind == TerritoryPinKinds.Branch) snapshot.Branches++;
                    else if (pin.Kind == TerritoryPinKinds.Food) snapshot.Foods++;
                    else if (pin.Kind == TerritoryPinKinds.Cafe) snapshot.Cafes++;
                    else if (pin.Kind == TerritoryPinKinds.Store) snapshot.Stores++;
                }
            }
            return snapshot;
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            return HandoffReceipt.Refused("영역은 인계를 받지 않습니다.");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state);
        }

        public void Restore(string saved)
        {
            var restored = JsonConvert.DeserializeObject<State>(saved);
            // 홈 동이 없는 저장(다른 단위의 팩으로 한 저장 포함)은 얹지 않는다. 처음 상태로 남는다.
            if (restored == null || restored.Dongs == null || restored.Dongs.All(item => item.Code != pack.homeDong)) return;
            // 팩에서 빠진 동은 버린다. 팩 판본이 바뀌어도 불러올 수 있어야 한다.
            restored.Dongs.RemoveAll(item => pack.Find(item.Code) == null);
            foreach (var dong in restored.Dongs)
            {
                if (dong.Pins == null) dong.Pins = new List<TerritoryPinState>();
                if (dong.MaxLevel < dong.Level) dong.MaxLevel = dong.Level;
            }
            state = restored;
            now = state.NowMs;
        }

        private TerritoryDongState Find(string code)
        {
            foreach (var dong in state.Dongs)
                if (dong.Code == code) return dong;
            return null;
        }

        private bool IsCandidate(TerritoryPackDong info)
        {
            foreach (var near in info.near)
            {
                var neighbour = Find(near);
                if (neighbour != null && neighbour.Level >= TerritoryLevels.Operating) return true;
            }
            return false;
        }

        private string EnterProblem(string code)
        {
            var info = pack.Find(code);
            if (info == null) return "이 지도에 없는 동입니다.";
            if (Find(code) != null) return "이미 진출한 동입니다.";
            if (!IsCandidate(info)) return "아직 닿지 않습니다. 맞닿은 동을 먼저 운영해야 합니다.";
            var entering = state.Dongs.Where(item => item.Level == TerritoryLevels.Entering).ToList();
            if (entering.Count >= pack.rules.maxEntering)
                return pack.Find(entering[0].Code).name + "이 운영 단계에 오를 때까지 기다려야 합니다.";
            return null;
        }

        private double Progress(TerritoryDongState dong)
        {
            if (dong == null) return 0;
            if (dong.Level >= TerritoryLevels.Settled) return 1;
            var rules = pack.rules;
            var value = dong.Level == TerritoryLevels.Entering
                ? dong.OperatedMs / (double)Math.Max(1L, rules.operateAfterMs)
                : (dong.OperatedMs - rules.operateAfterMs) / (double)Math.Max(1L, rules.settleAfterMs);
            return Math.Max(0, Math.Min(1, value));
        }

        private void Promote(TerritoryDongState dong, int level)
        {
            dong.Level = level;
            dong.LevelAtMs = now;
            var info = pack.Find(dong.Code);
            var added = string.Empty;
            var firstTime = level > dong.MaxLevel;
            if (firstTime) dong.MaxLevel = level;
            if (firstTime && !pack.IsStart(dong.Code))
            {
                if (level == TerritoryLevels.Operating)
                {
                    if (!SigHas(info.sig, TerritoryPinKinds.Branch))
                    {
                        AddPin(dong, info, TerritoryPinKinds.Branch);
                        added = " · 지사가 생겼습니다";
                    }
                    AddPin(dong, info, TerritoryPinKinds.Cafe);
                }
                else if (level == TerritoryLevels.Settled)
                {
                    var store = !SigHas(info.sig, TerritoryPinKinds.Store);
                    AddPin(dong, info, store ? TerritoryPinKinds.Store : TerritoryPinKinds.Food);
                    if (store) added = " · 창고가 생겼습니다";
                }
            }
            Note(info.name + " — " + TerritoryLevels.Name(level) + added);
        }

        private bool SigHas(string sig, string kind)
        {
            foreach (var dong in state.Dongs)
            {
                var info = pack.Find(dong.Code);
                if (info == null || info.sig != sig) continue;
                if (dong.Pins.Any(pin => pin.Kind == kind)) return true;
            }
            return false;
        }

        private void AddPin(TerritoryDongState dong, TerritoryPackDong info, string kind)
        {
            var count = info.spots.Count / 2;
            var index = count == 0 ? -1 : Math.Min(dong.Pins.Count, count - 1);
            var stem = info.name.EndsWith("동", StringComparison.Ordinal) ? info.name.Substring(0, info.name.Length - 1) : info.name;
            var same = dong.Pins.Count(pin => pin.Kind == kind);
            var title = kind == TerritoryPinKinds.Branch ? stem + " 지사" : kind == TerritoryPinKinds.Cafe ? stem + " 카페" :
                kind == TerritoryPinKinds.Store ? stem + " 창고" : stem + " 음식점" + (same > 0 ? " " + (same + 1) + "호" : string.Empty);
            dong.Pins.Add(new TerritoryPinState
            {
                Id = "pin:" + dong.Code + ":" + dong.Pins.Count, Kind = kind, Title = title,
                X = index < 0 ? info.x : info.spots[index * 2], Y = index < 0 ? info.y : info.spots[index * 2 + 1], AtMs = now,
            });
        }

        // 가상 매장의 사건: 주의 → (두면) 위험 → (더 두면) 한 단계 어두워짐.
        private void Events(TerritoryDongState dong, Signal signal)
        {
            var rules = pack.rules;
            var info = pack.Find(dong.Code);
            var id = SignalId(dong.Code);
            if (signal == null)
            {
                if (rules.eventEveryMs <= 0) return;      // 지어낸 사건을 끈 팩. 신호는 동별 수요가 올린다
                if (now - dong.LastEventMs < rules.eventEveryMs + Jitter(dong)) return;
                var kind = EventKinds[dong.Events % EventKinds.Length];
                kernel.RaiseSignal(new Signal
                {
                    Id = id, OsId = OsId, PlaceId = "dong:" + dong.Code, Severity = SignalSeverities.Caution,
                    ReasonCode = kind[0], Title = info.name, Message = kind[1], ActionLabel = kind[3], SinceMs = now,
                });
                return;
            }
            if (!signal.IsDanger)
            {
                if (signal.OsId != OsId) return;          // 다른 층이 올린 주의는 그 층이 위험으로 올린다
                if (now - signal.SinceMs < rules.escalateAfterMs) return;
                var kind = EventKinds.FirstOrDefault(item => item[0] == signal.ReasonCode) ?? EventKinds[0];
                kernel.RaiseSignal(new Signal
                {
                    Id = id, OsId = OsId, PlaceId = "dong:" + dong.Code, Severity = SignalSeverities.Danger,
                    ReasonCode = kind[0], Title = info.name, Message = kind[2], ActionLabel = kind[3], SinceMs = now,
                });
                return;
            }
            if (now - signal.EscalatedMs < rules.neglectAfterMs) return;
            kernel.ClearSignal(id, now);
            dong.LastEventMs = now;
            dong.Events++;
            if (dong.Level > TerritoryLevels.Entering)
            {
                dong.Level--;
                dong.OperatedMs = dong.Level == TerritoryLevels.Entering ? 0 : rules.operateAfterMs;
                dong.LevelAtMs = now;
                dong.DimmedAtMs = now;
                Note(info.name + " — 신호를 놓쳐 한 단계 어두워졌습니다 (" + TerritoryLevels.Name(dong.Level) + ")");
            }
            else
            {
                dong.OperatedMs = 0;
                dong.DimmedAtMs = now;
                Note(info.name + " — 신호를 놓쳐 처음부터 다시 쌓습니다");
            }
        }

        // 같은 씨앗이면 같은 간격이 나온다.
        private long Jitter(TerritoryDongState dong)
        {
            unchecked
            {
                var hash = (ulong)seed * 6364136223846793005UL + 1442695040888963407UL;
                foreach (var letter in dong.Code) hash = (hash ^ (ulong)letter) * 1099511628211UL;
                hash = (hash ^ (ulong)dong.Events) * 1099511628211UL;
                var half = (ulong)Math.Max(1L, pack.rules.eventEveryMs / 2);
                return (long)(hash % half);
            }
        }

        private void Note(string text)
        {
            state.LastNote = text;
            state.LastNoteAtMs = now;
        }

        private sealed class State
        {
            public long NowMs;
            public string LastNote = string.Empty;
            public long LastNoteAtMs;
            public List<TerritoryDongState> Dongs = new List<TerritoryDongState>();
        }
    }
}
