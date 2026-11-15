using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    // 2026-11-13 동마다 생기는 주문(행정동 단위). 사용자 방향: "동마다 주문이 생기게, 인구·소득 정보가 있으면 그것에 기반해서".
    //  - 깃발을 꽂은 동(영역 모듈의 진출 중 이상)에만 가상 주문이 생긴다. 깃발이 없는 동은 조용하다.
    //  - 주문의 크기는 팩이 정한다: 동의 하루 주문 = 묶음 전체의 가상 주문 × 동의 비율(인구 바탕 × 소득 지수).
    //    시간대(6칸)와 요일(7칸)의 모양도 팩에 있다. 인구·소득 자료는 비율과 모양에만 쓰고, 건수 자체는 가상 값이다.
    //  - 받아내는 힘은 그 동의 기사와 제휴 음식점이다. 둘 중 모자란 쪽이 병목이다. 못 받은 주문은 사라지고 신호가 된다.
    //  - 신호는 영역 모듈과 같은 ID(TerritoryModule.SignalId)로 올린다. 주의는 여기서 위험으로 올리고,
    //    위험을 놓치면 영역 모듈이 그 동을 한 단계 어둡게 한다. 받아내는 비율이 낮은 동은 깊어지지 않는다(ITerritoryGate).
    // 한 동 한 동을 길 위에서 굴리는 음식 배달 Core와 달리 이 모듈은 동 단위의 합계만 센다. Core의 주문·정산과는 섞지 않는다.
    // 비용·보수·처리량은 모두 팩 rules의 임시 값이다(Claude 초안). 기획이 정하면 팩만 바꾼다.
    // 하루(일일일생)의 마감은 여기서 하지 않는다. 화면 층이 이 모듈의 누적 값을 날짜별로 나눠 읽는다.

    public static class DongDemandActions
    {
        /// <summary>그 동에 기사 한 명을 모집한다. Values: dong</summary>
        public const string Recruit = "recruit";
        /// <summary>그 동의 음식점 한 곳과 제휴한다. Values: dong</summary>
        public const string Contract = "contract";
        /// <summary>배차 엔진을 한 단계 올린다.</summary>
        public const string Engine = "engine";
        /// <summary>오늘의 요일을 알린다(현실 달력). Values: weekday(0=월 … 6=일), date(yyyy-MM-dd)</summary>
        public const string Calendar = "calendar";
    }

    public static class DongDemandBottlenecks
    {
        public const string Rider = "rider-short";
        public const string Partner = "partner-short";
    }

    public sealed class DongDemandRules
    {
        public double areaDailyOrders = 240;
        public double riderOrdersPerHour = 3.5;
        public double partnerOrdersPerHour = 4;
        public long revenuePerOrder = 4800;
        public long riderPayPerOrder = 3600;
        public long recruitCost = 20000;
        public long contractCost = 6000;
        public long engineCost = 60000;
        public double engineBoostPerLevel = 0.12;
        public int maxEngineLevel = 5;
        public long startFunds = 60000;
        public int maxRidersPerDong = 12;
        public double maxPartnerShare = 0.6;
        public long windowMs = 1800000;
        public double minWindowOrders = 0.5;
        public double cautionUnmetRatio = 0.35;
        public double clearUnmetRatio = 0.15;
        public long escalateAfterMs = 600000;
        public double deepenServeRatio = 0.5;
        public long maxStepMs = 60000;
        public double startHour = 11.5;
        public int startRiders = 2;
        public int startPartners = 2;
    }

    public sealed class DongDemandBasisPart
    {
        public string kind = string.Empty;
        public string dataset = string.Empty;
        public string quarter = string.Empty;
        public string note = string.Empty;
    }

    public sealed class DongDemandBasis
    {
        public DongDemandBasisPart population = new DongDemandBasisPart();
        public DongDemandBasisPart income = new DongDemandBasisPart();
        public DongDemandBasisPart restaurants = new DongDemandBasisPart();
        public double incomeElasticity;
    }

    public sealed class DongDemandPackDong
    {
        public string code = string.Empty;
        public string name = string.Empty;
        public double population;
        public double incomeIndex = 1;
        public int restaurants;
        public double weight;
        public double dailyOrders;
        public List<double> bands = new List<double>();
        public List<double> weekdays = new List<double>();
        /// <summary>처음부터 운영하는 동. 영역 팩의 처음 동과 같아야 한다.</summary>
        public bool start;
        /// <summary>처음 동에 이미 있는 기사·제휴. 음수면 rules의 값을 쓴다.</summary>
        public int startRiders = -1;
        public int startPartners = -1;
    }

    public sealed class DongDemandPack
    {
        public const string Schema = "hongdal-dong-demand.v1";
        public string schema = string.Empty;
        public string label = string.Empty;
        public string note = string.Empty;
        public DongDemandBasis basis = new DongDemandBasis();
        public List<double> bandHours = new List<double>();
        public DongDemandRules rules = new DongDemandRules();
        public List<DongDemandPackDong> dongs = new List<DongDemandPackDong>();

        public static DongDemandPack Parse(string json)
        {
            var pack = JsonConvert.DeserializeObject<DongDemandPack>(json);
            if (pack == null || pack.schema != Schema) throw new InvalidOperationException("동별 수요 팩의 형식이 다릅니다.");
            if (pack.rules == null) pack.rules = new DongDemandRules();
            if (pack.basis == null) pack.basis = new DongDemandBasis();
            if (pack.basis.population == null) pack.basis.population = new DongDemandBasisPart();
            if (pack.basis.income == null) pack.basis.income = new DongDemandBasisPart();
            if (pack.basis.restaurants == null) pack.basis.restaurants = new DongDemandBasisPart();
            if (pack.dongs == null || pack.dongs.Count == 0) throw new InvalidOperationException("동별 수요 팩에 동이 없습니다.");
            if (pack.bandHours == null || pack.bandHours.Count < 2 || pack.bandHours[0] != 0 || pack.bandHours[pack.bandHours.Count - 1] != 24)
                throw new InvalidOperationException("동별 수요 팩의 시간대는 0시에서 24시까지여야 합니다.");
            for (var i = 1; i < pack.bandHours.Count; i++)
                if (pack.bandHours[i] <= pack.bandHours[i - 1]) throw new InvalidOperationException("동별 수요 팩의 시간대 순서가 틀렸습니다.");
            foreach (var dong in pack.dongs)
            {
                if (string.IsNullOrEmpty(dong.code)) throw new InvalidOperationException("동별 수요 팩에 코드가 없는 동이 있습니다.");
                if (dong.bands == null || dong.bands.Count != pack.bandHours.Count - 1)
                    throw new InvalidOperationException(dong.name + ": 시간대 비율의 수가 시간대와 다릅니다.");
                if (dong.weekdays == null || dong.weekdays.Count != 7) throw new InvalidOperationException(dong.name + ": 요일 값은 일곱 개여야 합니다.");
                if (dong.dailyOrders < 0 || double.IsNaN(dong.dailyOrders) || double.IsInfinity(dong.dailyOrders))
                    throw new InvalidOperationException(dong.name + ": 하루 주문 수가 틀렸습니다.");
            }
            return pack;
        }

        public DongDemandPackDong Find(string code)
        {
            foreach (var dong in dongs)
                if (dong.code == code) return dong;
            return null;
        }

        /// <summary>그 시각(0~24)에 한 시간 동안 생기는 주문 수. weekday가 0~6이 아니면 평균 요일이다.</summary>
        public double RateAt(DongDemandPackDong dong, double hour, int weekday)
        {
            var h = hour % 24;
            if (h < 0) h += 24;
            var band = bandHours.Count - 2;
            for (var i = 0; i < bandHours.Count - 1; i++)
            {
                if (h >= bandHours[i] && h < bandHours[i + 1])
                {
                    band = i;
                    break;
                }
            }
            var day = weekday >= 0 && weekday < 7 ? dong.weekdays[weekday] : 1.0;
            return dong.dailyOrders * day * dong.bands[band] / (bandHours[band + 1] - bandHours[band]);
        }

        /// <summary>평균 요일에 가장 바쁜 시간대의 한 시간 주문 수.</summary>
        public double PeakRate(DongDemandPackDong dong)
        {
            var peak = 0.0;
            for (var i = 0; i < bandHours.Count - 1; i++)
                peak = Math.Max(peak, dong.dailyOrders * dong.bands[i] / (bandHours[i + 1] - bandHours[i]));
            return peak;
        }
    }

    public sealed class DongDemandDongState
    {
        public string Code = string.Empty;
        /// <summary>깃발이 꽂혀 주문이 생기는 동.</summary>
        public bool Open;
        public int Riders;
        public int Partners;
        /// <summary>처음부터 지금까지 생긴·받아낸·놓친 주문(건, 소수).</summary>
        public double Demand;
        public double Served;
        public double Unmet;
        /// <summary>최근 창(rules.windowMs)의 감쇠 합.</summary>
        public double WindowDemand;
        public double WindowUnmet;
        public string Bottleneck = string.Empty;
        public long OpenedAtMs;
    }

    public sealed class DongDemandDongView
    {
        public string Code = string.Empty;
        public string Name = string.Empty;
        public int Level;
        public bool Open;
        public bool Start;
        public int Riders;
        public int Partners;
        public int PartnerLimit;
        public int Restaurants;
        public double Weight;
        public double DailyOrders;
        /// <summary>지금 시각에 한 시간 동안 생기는 주문과, 기사·제휴가 한 시간에 받아낼 수 있는 주문.</summary>
        public double RateNow;
        public double RiderCapacity;
        public double PartnerCapacity;
        public double Demand;
        public double Served;
        public double Unmet;
        /// <summary>최근 창에서 놓친 비율(0~1).</summary>
        public double UnmetRatio;
        public string Bottleneck = string.Empty;
        /// <summary>운영의 깊이(0~1): 가장 바쁜 시간대를 기사·제휴가 얼마나 덮는지와 최근에 받아낸 비율의 평균. 밝기에 쓴다. 점수가 아니다.</summary>
        public double Depth;
        public bool CanRecruit;
        public string RecruitBlock = string.Empty;
        public bool CanContract;
        public string ContractBlock = string.Empty;
        public Signal Signal;
    }

    public sealed class DongDemandSnapshot
    {
        public List<DongDemandDongView> Dongs = new List<DongDemandDongView>();
        public long Funds;
        public double Revenue;
        public double RiderPay;
        public long Spent;
        public int EngineLevel;
        public int MaxEngineLevel;
        public long EngineCost;
        public bool CanUpgrade;
        public string UpgradeBlock = string.Empty;
        public double EngineBoost;
        public long RecruitCost;
        public long ContractCost;
        public long NowMs;
        public double Hour;
        public int Weekday = -1;
        public string DateKey = string.Empty;
        public double Demand;
        public double Served;
        public double Unmet;
        public int Riders;
        public int Partners;
        public string Label = string.Empty;
        public string PopulationBasis = string.Empty;
        public string IncomeBasis = string.Empty;
        public string LastNote = string.Empty;
        public long LastNoteAtMs;

        public DongDemandDongView Find(string code)
        {
            foreach (var dong in Dongs)
                if (dong.Code == code) return dong;
            return null;
        }
    }

    public sealed class DongDemandModule : IBusinessModule, ITerritoryGate
    {
        private const double HourMs = 3600000.0;
        private readonly DongDemandPack pack;
        private GameKernel kernel;
        private State state = new State();
        private long now;

        public DongDemandModule(DongDemandPack pack)
        {
            this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        }

        public string OsId { get { return BusinessIds.DongDemand; } }

        public void Start(ModuleContext context)
        {
            kernel = context.Kernel;
            now = 0;
            state = new State { EngineLevel = 1 };
            foreach (var info in pack.dongs)
                if (info.start) OpenDong(info, true);
        }

        public void Tick(long elapsedMs)
        {
            if (elapsedMs <= now) return;
            // 시각이 크게 건너뛰면(마감 뒤 다음 영업까지 넘기기 등) 끝의 한 걸음만 센다. 지켜본 시간에만 주문이 생긴다.
            var step = Math.Min(elapsedMs - now, Math.Max(1L, pack.rules.maxStepMs));
            var middle = elapsedMs - step / 2;
            now = elapsedMs;
            state.NowMs = now;
            if (kernel == null) return;
            var territory = Territory();
            var hours = step / HourMs;
            var hour = HourAt(middle);
            var keep = Math.Exp(-(double)step / Math.Max(1L, pack.rules.windowMs));
            foreach (var info in pack.dongs)
            {
                var level = LevelOf(territory, info);
                var dong = Find(info.code);
                if (level < TerritoryLevels.Entering)
                {
                    if (dong != null && dong.Open) Close(info, dong);
                    continue;
                }
                if (dong == null) dong = OpenDong(info, false);
                else if (!dong.Open) dong.Open = true;

                var demand = pack.RateAt(info, hour, state.Weekday) * hours;
                var riders = RiderCapacity(dong) * hours;
                var partners = PartnerCapacity(dong) * hours;
                var served = Math.Max(0, Math.Min(demand, Math.Min(riders, partners)));
                var unmet = demand - served;
                dong.Demand += demand;
                dong.Served += served;
                dong.Unmet += unmet;
                dong.WindowDemand = dong.WindowDemand * keep + demand;
                dong.WindowUnmet = dong.WindowUnmet * keep + unmet;
                if (unmet > 1e-9) dong.Bottleneck = riders <= partners ? DongDemandBottlenecks.Rider : DongDemandBottlenecks.Partner;
                state.Revenue += served * pack.rules.revenuePerOrder;
                state.RiderPay += served * pack.rules.riderPayPerOrder;
                Signals(info, dong, demand <= 1e-12 ? 0 : unmet / demand);
            }
        }

        public GamePreview Preview(GameCommand command)
        {
            long cost;
            string done;
            var problem = Problem(command, out cost, out done);
            return problem == null
                ? new GamePreview { CanConfirm = true, Message = done + (cost > 0 ? " · 비용 " + cost.ToString("N0") + "원" : string.Empty) }
                : GamePreview.Blocked(problem, "DongDemandBlocked");
        }

        public GameResult Confirm(GameCommand command)
        {
            if (command.ElapsedMs > now)
            {
                now = command.ElapsedMs;
                state.NowMs = now;
            }
            long cost;
            string done;
            var problem = Problem(command, out cost, out done);
            if (problem != null) return GameResult.Fail(problem, "DongDemandBlocked");
            var code = command.Value("dong");
            if (command.Action == DongDemandActions.Recruit) Find(code).Riders++;
            else if (command.Action == DongDemandActions.Contract) Find(code).Partners++;
            else if (command.Action == DongDemandActions.Engine) state.EngineLevel++;
            else if (command.Action == DongDemandActions.Calendar)
            {
                state.Weekday = (int)command.Number("weekday");
                state.DateKey = command.Value("date");
                return GameResult.Done(done);
            }
            if (cost > 0)
            {
                state.Spent += cost;
                if (kernel != null)
                {
                    kernel.Record(new LedgerEntry
                    {
                        Kind = LedgerKinds.Money, OsId = OsId, PlaceId = string.IsNullOrEmpty(code) ? "area" : "dong:" + code,
                        ItemId = "krw-simulation", Bucket = "spent", Amount = cost, Unit = "원", AtMs = now, Reason = done,
                        RefId = command.CommandId ?? string.Empty,
                    });
                }
            }
            Note(done);
            return GameResult.Done(done);
        }

        public object Snapshot()
        {
            var rules = pack.rules;
            var territory = Territory();
            var hour = HourAt(now);
            var snapshot = new DongDemandSnapshot
            {
                Funds = Funds(), Revenue = state.Revenue, RiderPay = state.RiderPay, Spent = state.Spent,
                EngineLevel = state.EngineLevel, MaxEngineLevel = rules.maxEngineLevel, EngineCost = EngineCost(), EngineBoost = EngineBoost(),
                RecruitCost = rules.recruitCost, ContractCost = rules.contractCost, NowMs = now, Hour = hour,
                Weekday = state.Weekday, DateKey = state.DateKey ?? string.Empty, Label = pack.label ?? string.Empty,
                PopulationBasis = pack.basis.population.kind ?? string.Empty, IncomeBasis = pack.basis.income.kind ?? string.Empty,
                LastNote = state.LastNote ?? string.Empty, LastNoteAtMs = state.LastNoteAtMs,
            };
            long cost;
            string done;
            var upgrade = Problem(new GameCommand { OsId = OsId, Action = DongDemandActions.Engine }, out cost, out done);
            snapshot.CanUpgrade = upgrade == null;
            snapshot.UpgradeBlock = upgrade ?? string.Empty;
            foreach (var info in pack.dongs)
            {
                var dong = Find(info.code);
                var open = dong != null && dong.Open;
                var view = new DongDemandDongView
                {
                    Code = info.code, Name = info.name, Level = LevelOf(territory, info), Open = open, Start = info.start,
                    Restaurants = info.restaurants, PartnerLimit = PartnerLimit(info), Weight = info.weight, DailyOrders = info.dailyOrders,
                    RateNow = open ? pack.RateAt(info, hour, state.Weekday) : 0,
                    Signal = kernel == null ? null : kernel.FindSignal(TerritoryModule.SignalId(info.code)),
                };
                if (dong != null)
                {
                    view.Riders = dong.Riders;
                    view.Partners = dong.Partners;
                    view.RiderCapacity = RiderCapacity(dong);
                    view.PartnerCapacity = PartnerCapacity(dong);
                    view.Demand = dong.Demand;
                    view.Served = dong.Served;
                    view.Unmet = dong.Unmet;
                    view.UnmetRatio = UnmetRatio(dong);
                    view.Bottleneck = dong.Bottleneck ?? string.Empty;
                    view.Depth = Depth(info, dong);
                    snapshot.Demand += dong.Demand;
                    snapshot.Served += dong.Served;
                    snapshot.Unmet += dong.Unmet;
                    snapshot.Riders += dong.Riders;
                    snapshot.Partners += dong.Partners;
                }
                var recruit = Problem(new GameCommand { OsId = OsId, Action = DongDemandActions.Recruit }.With("dong", info.code), out cost, out done);
                view.CanRecruit = recruit == null;
                view.RecruitBlock = recruit ?? string.Empty;
                var contract = Problem(new GameCommand { OsId = OsId, Action = DongDemandActions.Contract }.With("dong", info.code), out cost, out done);
                view.CanContract = contract == null;
                view.ContractBlock = contract ?? string.Empty;
                snapshot.Dongs.Add(view);
            }
            return snapshot;
        }

        public HandoffReceipt Accept(Handoff handoff)
        {
            return HandoffReceipt.Refused("동별 수요는 인계를 받지 않습니다.");
        }

        public string Save()
        {
            return JsonConvert.SerializeObject(state);
        }

        public void Restore(string saved)
        {
            var restored = JsonConvert.DeserializeObject<State>(saved);
            if (restored == null || restored.Dongs == null) return;
            // 팩에서 빠진 동은 버린다. 팩 판본이 바뀌어도 불러올 수 있어야 한다.
            restored.Dongs.RemoveAll(item => item == null || pack.Find(item.Code) == null);
            if (restored.EngineLevel < 1) restored.EngineLevel = 1;
            state = restored;
            now = state.NowMs;
        }

        /// <summary>영역 모듈이 묻는다. 기사와 제휴가 있고, 최근에 생긴 주문의 일정 비율을 받아내야 깊어진다.</summary>
        public bool Deepens(string dongCode)
        {
            var dong = Find(dongCode);
            if (dong == null) return pack.Find(dongCode) == null;     // 이 팩이 모르는 동은 막지 않는다
            if (!dong.Open || dong.Riders <= 0 || dong.Partners <= 0) return false;
            if (dong.WindowDemand < pack.rules.minWindowOrders) return true;
            return 1 - UnmetRatio(dong) >= pack.rules.deepenServeRatio;
        }

        // ── 안쪽 ─────────────────────────────────────────────────────────────

        private ITerritoryView Territory()
        {
            return kernel == null ? null : kernel.Modules.OfType<ITerritoryView>().FirstOrDefault();
        }

        // 영역 모듈이 없으면 처음 동만 운영 중으로 본다.
        private static int LevelOf(ITerritoryView territory, DongDemandPackDong info)
        {
            if (territory == null) return info.start ? TerritoryLevels.Operating : TerritoryLevels.Unknown;
            return territory.LevelOf(info.code);
        }

        private double HourAt(long elapsedMs)
        {
            var hour = (pack.rules.startHour + elapsedMs / HourMs) % 24;
            return hour < 0 ? hour + 24 : hour;
        }

        private double EngineBoost()
        {
            return 1 + pack.rules.engineBoostPerLevel * Math.Max(0, state.EngineLevel - 1);
        }

        private long EngineCost()
        {
            var cost = pack.rules.engineCost;
            for (var level = 1; level < state.EngineLevel; level++) cost *= 2;
            return cost;
        }

        private double RiderCapacity(DongDemandDongState dong)
        {
            return dong.Riders * pack.rules.riderOrdersPerHour * EngineBoost();
        }

        private double PartnerCapacity(DongDemandDongState dong)
        {
            return dong.Partners * pack.rules.partnerOrdersPerHour;
        }

        private int PartnerLimit(DongDemandPackDong info)
        {
            if (info.restaurants <= 0) return pack.rules.maxRidersPerDong * 2;
            return Math.Max(1, (int)Math.Floor(info.restaurants * pack.rules.maxPartnerShare));
        }

        private static double UnmetRatio(DongDemandDongState dong)
        {
            if (dong.WindowDemand <= 1e-9) return 0;
            return Math.Max(0, Math.Min(1, dong.WindowUnmet / dong.WindowDemand));
        }

        private double Depth(DongDemandPackDong info, DongDemandDongState dong)
        {
            if (!dong.Open) return 0;
            var peak = pack.PeakRate(info);
            var riders = peak <= 1e-9 ? (dong.Riders > 0 ? 1.0 : 0.0) : Math.Min(1, RiderCapacity(dong) / peak);
            var partners = peak <= 1e-9 ? (dong.Partners > 0 ? 1.0 : 0.0) : Math.Min(1, PartnerCapacity(dong) / peak);
            var served = dong.WindowDemand <= 1e-9 ? Math.Min(riders, partners) : 1 - UnmetRatio(dong);
            return Math.Max(0, Math.Min(1, (riders + partners + served) / 3));
        }

        private long Funds()
        {
            // 소수 주문의 합이라 2,400원이 2,399.9999…원으로 쌓일 수 있다. 내림하기 전에 아주 조금 더한다.
            return pack.rules.startFunds + (long)Math.Floor(state.Revenue - state.RiderPay + 1e-6) - state.Spent;
        }

        private DongDemandDongState Find(string code)
        {
            foreach (var dong in state.Dongs)
                if (dong.Code == code) return dong;
            return null;
        }

        // 처음 동은 이미 있는 기사·제휴로, 새로 깃발을 꽂은 동은 첫 음식점(제휴 한 곳)만 갖고 시작한다. 기사는 모집해야 한다.
        private DongDemandDongState OpenDong(DongDemandPackDong info, bool start)
        {
            var dong = new DongDemandDongState
            {
                Code = info.code, Open = true, OpenedAtMs = now,
                Riders = start ? (info.startRiders >= 0 ? info.startRiders : pack.rules.startRiders) : 0,
                Partners = start ? (info.startPartners >= 0 ? info.startPartners : pack.rules.startPartners) : 1,
            };
            dong.Partners = Math.Min(dong.Partners, PartnerLimit(info));
            state.Dongs.Add(dong);
            if (!start) Note(info.name + "에 주문이 들어오기 시작했습니다. 기사를 모집해야 받아낼 수 있습니다.");
            return dong;
        }

        private void Close(DongDemandPackDong info, DongDemandDongState dong)
        {
            dong.Open = false;
            dong.WindowDemand = 0;
            dong.WindowUnmet = 0;
            var open = kernel.FindSignal(TerritoryModule.SignalId(info.code));
            if (open != null && open.OsId == OsId) kernel.ClearSignal(open.Id, now);
        }

        // 최근에 놓친 비율이 높고 지금도 놓치고 있으면 주의, 주의를 두면 위험.
        // 지금 생기는 주문을 받아내기 시작하면(기사·제휴를 늘리면) 바로 끈다. 다른 층이 올린 신호는 건드리지 않는다.
        private void Signals(DongDemandPackDong info, DongDemandDongState dong, double unmetNow)
        {
            var rules = pack.rules;
            var id = TerritoryModule.SignalId(info.code);
            var open = kernel.FindSignal(id);
            if (open != null && open.OsId != OsId) return;
            if (open == null)
            {
                if (dong.WindowDemand < rules.minWindowOrders || UnmetRatio(dong) < rules.cautionUnmetRatio ||
                    unmetNow < rules.cautionUnmetRatio) return;
                kernel.RaiseSignal(Compose(info, dong, id, false));
                return;
            }
            if (unmetNow <= rules.clearUnmetRatio)
            {
                kernel.ClearSignal(id, now);
                Note(info.name + " — 주문을 받아내고 있습니다. 신호를 껐습니다.");
                return;
            }
            var danger = open.IsDanger || now - open.SinceMs >= rules.escalateAfterMs;
            if (danger != open.IsDanger || open.ReasonCode != Reason(dong)) kernel.RaiseSignal(Compose(info, dong, id, danger));
        }

        private static string Reason(DongDemandDongState dong)
        {
            return dong.Bottleneck == DongDemandBottlenecks.Partner ? DongDemandBottlenecks.Partner : DongDemandBottlenecks.Rider;
        }

        private Signal Compose(DongDemandPackDong info, DongDemandDongState dong, string id, bool danger)
        {
            var rider = Reason(dong) == DongDemandBottlenecks.Rider;
            return new Signal
            {
                Id = id, OsId = OsId, PlaceId = "dong:" + info.code,
                Severity = danger ? SignalSeverities.Danger : SignalSeverities.Caution,
                ReasonCode = Reason(dong),
                Title = info.name,
                Message = rider
                    ? (danger ? "기사가 모자라 주문을 놓치고 있습니다. 두면 동이 어두워집니다." : "기사가 모자라 주문을 놓치기 시작했습니다.")
                    : (danger ? "제휴 음식점이 모자라 주문을 놓치고 있습니다. 두면 동이 어두워집니다." : "제휴 음식점이 모자라 주문을 놓치기 시작했습니다."),
                ActionLabel = rider ? "기사 모집" : "제휴 맺기",
                SinceMs = now,
            };
        }

        // 할 수 없으면 이유를, 할 수 있으면 null을 돌려준다. cost는 드는 돈, done은 하고 난 뒤의 말이다.
        private string Problem(GameCommand command, out long cost, out string done)
        {
            cost = 0;
            done = string.Empty;
            var rules = pack.rules;
            if (command == null) return "지원하지 않는 행동입니다.";
            if (command.Action == DongDemandActions.Calendar)
            {
                if (string.IsNullOrEmpty(command.Value("weekday"))) return "요일이 없습니다.";
                var weekday = command.Number("weekday");
                if (weekday < 0 || weekday > 6 || weekday != Math.Floor(weekday)) return "요일은 0(월)에서 6(일)까지입니다.";
                done = "오늘의 요일을 맞췄습니다.";
                return null;
            }
            if (command.Action == DongDemandActions.Engine)
            {
                if (state.EngineLevel >= rules.maxEngineLevel) return "배차 엔진이 가장 높은 단계입니다.";
                cost = EngineCost();
                if (Funds() < cost) return "자금이 모자랍니다. " + cost.ToString("N0") + "원이 듭니다.";
                done = "배차 엔진을 " + (state.EngineLevel + 1) + "단계로 올렸습니다.";
                return null;
            }
            if (command.Action != DongDemandActions.Recruit && command.Action != DongDemandActions.Contract)
                return "지원하지 않는 행동입니다.";
            var info = pack.Find(command.Value("dong"));
            if (info == null) return "이 지도에 없는 동입니다.";
            var dong = Find(info.code);
            if (dong == null || !dong.Open) return "깃발을 먼저 꽂아야 합니다.";
            if (command.Action == DongDemandActions.Recruit)
            {
                if (dong.Riders >= rules.maxRidersPerDong) return "이 동의 기사가 가득 찼습니다.";
                cost = rules.recruitCost;
                if (Funds() < cost) return "자금이 모자랍니다. " + cost.ToString("N0") + "원이 듭니다.";
                done = info.name + "에 기사 한 명을 모집했습니다. (" + (dong.Riders + 1) + "명)";
                return null;
            }
            if (dong.Partners >= PartnerLimit(info)) return "이 동에서 더 제휴할 음식점이 없습니다.";
            cost = rules.contractCost;
            if (Funds() < cost) return "자금이 모자랍니다. " + cost.ToString("N0") + "원이 듭니다.";
            done = info.name + "의 음식점 한 곳과 제휴했습니다. (" + (dong.Partners + 1) + "곳)";
            return null;
        }

        private void Note(string text)
        {
            state.LastNote = text;
            state.LastNoteAtMs = now;
        }

        private sealed class State
        {
            public long NowMs;
            public int EngineLevel = 1;
            public int Weekday = -1;
            public string DateKey = string.Empty;
            public double Revenue;
            public double RiderPay;
            public long Spent;
            public string LastNote = string.Empty;
            public long LastNoteAtMs;
            public List<DongDemandDongState> Dongs = new List<DongDemandDongState>();
        }
    }
}
