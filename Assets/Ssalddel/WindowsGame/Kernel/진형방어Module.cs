using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>가상의 흑광 방어 시험을 진행하는 독립 규칙 모듈.</summary>
    public sealed class 진형방어Module : IBusinessModule
    {
        public const string Id = "formation-defense";
        private readonly bool enabled;
        private DefenseState state = new DefenseState();
        private uint seed;

        public 진형방어Module(bool enabled = true) { this.enabled = enabled; }
        public string OsId => Id;
        public void Start(ModuleContext context)
        {
            seed = unchecked((uint)(context?.Seed ?? 0));
            state = new DefenseState();
        }

        public void Tick(long elapsedMs)
        {
            if (!enabled || elapsedMs <= state.NowMs || elapsedMs > long.MaxValue - 진형방어Policy.StepMs) return;
            진형방어Battle.Tick(state, elapsedMs);
            state.NowMs = elapsedMs;
        }

        public GamePreview Preview(GameCommand command)
        {
            var result = Apply((DefenseState)Snapshot(), command);
            return new GamePreview { CanConfirm = result.Ok, BlockCode = result.Code, Message = result.Message, RawJson = result.RawJson };
        }

        public GameResult Confirm(GameCommand command) => Apply(state, command);
        public object Snapshot() => JsonConvert.DeserializeObject<DefenseState>(JsonConvert.SerializeObject(state));
        public HandoffReceipt Accept(Handoff handoff) => HandoffReceipt.Refused("진형 방어는 업무 인계를 받지 않습니다.");
        public string Save() => enabled ? JsonConvert.SerializeObject(state, Formatting.None) : null;

        public void Restore(string saved)
        {
            if (!enabled || string.IsNullOrEmpty(saved)) return;
            try
            {
                var candidate = JsonConvert.DeserializeObject<DefenseState>(saved);
                if (ValidSave(candidate)) state = candidate;
            }
            catch (JsonException) { }
        }

        private GameResult Apply(DefenseState draft, GameCommand command)
        {
            if (!enabled) return GameResult.Fail("이 판에서는 진형 방어가 잠겨 있습니다.", "EditionDisabled");
            if (command == null) return GameResult.Fail("명령이 필요합니다.", "UnknownAction");
            if (draft.Battle != null) return GameResult.Fail("방어전 중에는 준비를 변경할 수 없습니다.", "BattleActive");

            try
            {
                switch (command.Action)
                {
                    case "defense.site":
                        var setup = JsonConvert.DeserializeObject<DefenseSetup>(command.PayloadJson);
                        if (!ValidSetup(setup)) return GameResult.Fail("옥상이나 시험 길의 연결을 확인해 주세요.", "InvalidSetup");
                        if (draft.Alarm != null || draft.Forts.Count > 0 || draft.Placements.Count > 0)
                            return GameResult.Fail("현재 지도에 배치나 경보가 있습니다.", "SiteInUse");
                        draft.Setup = setup;
                        return GameResult.Done("시험 지도를 준비했습니다.");
                    case "squad.set":
                        return SetSquad(draft, JsonConvert.DeserializeObject<DefenseSquad>(command.PayloadJson));
                    case "formation.place":
                        return Place(draft, command);
                    case "formation.lift":
                        var lifted = draft.Placements.RemoveAll(item => item.Squad == command.Value("squad"));
                        return lifted > 0 ? GameResult.Done("분대를 물렸습니다.") : GameResult.Fail("배치된 분대가 없습니다.", "NotPlaced");
                    case "fort.build":
                        return Build(draft, command);
                    case "fort.repair":
                        return Repair(draft, command);
                    case "defense.turn":
                        foreach (var fort in draft.Forts.Where(item => item.BuildTurnsLeft > 0))
                        {
                            if (--fort.BuildTurnsLeft == 0) fort.Builder = null;
                        }
                        return GameResult.Done("공사를 한 턴 진행했습니다.");
                    case "defense.alarm":
                        return Alarm(draft, command);
                    case "defense.begin":
                        return Begin(draft);
                    default:
                        return GameResult.Fail("알 수 없는 명령입니다.", "UnknownAction");
                }
            }
            catch (JsonException)
            {
                return GameResult.Fail("시험 자료 형식을 확인해 주세요.", "InvalidPayload");
            }
        }

        private static GameResult SetSquad(DefenseState draft, DefenseSquad squad)
        {
            if (!ValidSquad(squad)) return GameResult.Fail("서로 다른 대원 3~5명과 유효한 능력치가 필요합니다.", "InvalidSquad");
            if (draft.Placements.Any(item => item.Squad == squad.Id) || Building(draft, squad.Id))
                return GameResult.Fail("배치 또는 공사에 쓰이는 분대입니다.", "SquadInUse");
            var existing = draft.Squads.Find(item => item.Id == squad.Id);
            if (draft.Squads.Where(item => item.Id != squad.Id).SelectMany(item => item.Members)
                .Any(person => squad.Members.Any(member => person.Id == member.Id)))
                return GameResult.Fail("이미 다른 분대 소속인 대원입니다.", "MemberInOtherSquad");
            if (existing != null)
            {
                foreach (var person in squad.Members)
                {
                    var previous = existing.Members.Find(item => item.Id == person.Id);
                    if (previous != null) person.DefenseCount = previous.DefenseCount;
                }
                draft.Squads[draft.Squads.IndexOf(existing)] = squad;
            }
            else draft.Squads.Add(squad);
            return GameResult.Done("분대를 편성했습니다.");
        }

        private static GameResult Place(DefenseState draft, GameCommand command)
        {
            var squad = Available(draft, command.Value("squad"));
            if (squad == null || Building(draft, squad.Id)) return GameResult.Fail("지금 배치할 수 없는 분대입니다.", "SquadUnavailable");
            if (draft.Setup == null || !TryEnum(command.Value("kind"), out PlacementKind kind))
                return GameResult.Fail("배치 위치의 종류가 유효하지 않습니다.", "InvalidPlacement");
            var target = command.Value("target");
            var placement = new DefensePlacement { Squad = squad.Id, Target = target, Kind = kind };
            var occupied = draft.Placements.Any(item => item.Squad != squad.Id && item.Target == target && item.Kind == kind);
            if (occupied) return GameResult.Fail("이미 다른 분대가 쓰는 자리입니다.", "SiteOccupied");
            double radius;
            if (kind == PlacementKind.Fort)
            {
                var fort = draft.Forts.Find(item => item.Id == target && item.Kind != FortKind.Fence &&
                    item.BuildTurnsLeft == 0 && item.Durability > 0);
                if (fort == null) return GameResult.Fail("주둔할 수 있는 진지가 아닙니다.", "UnavailableFort");
                placement.Stamina = 진형방어Policy.Stamina(squad, kind, fort.Kind);
                radius = 진형방어Policy.BeaconRadius;
            }
            else
            {
                var site = draft.Setup.Sites.Find(item => item.Id == target);
                if (site == null) return GameResult.Fail("없는 건물입니다.", "UnknownSite");
                if (kind == PlacementKind.Roof && squad.Members.Count > 진형방어Policy.Capacity(site))
                    return GameResult.Fail("옥상에 분대원 전원이 설 자리가 부족합니다.", "RoofCapacity");
                placement.Stamina = 진형방어Policy.Stamina(squad, kind);
                radius = 진형방어Policy.Radius(site) * (kind == PlacementKind.Inside ? 진형방어Policy.IndoorRadius : 1);
            }
            draft.Placements.RemoveAll(item => item.Squad == squad.Id);
            draft.Placements.Add(placement);
            return new GameResult { Ok = true, Message = "분대를 배치했습니다.", RawJson = JsonConvert.SerializeObject(new
            {
                placement, radius, formation = 진형방어Policy.Formation(draft),
            }) };
        }

        private static GameResult Build(DefenseState draft, GameCommand command)
        {
            var squad = Available(draft, command.Value("squad"));
            if (squad == null || Building(draft, squad.Id) || draft.Placements.Any(item => item.Squad == squad.Id))
                return GameResult.Fail("공사에 쓸 수 있는 빈 분대가 필요합니다.", "SquadUnavailable");
            var id = command.Value("id");
            var target = command.Value("target");
            if (draft.Setup == null || !ValidId(id) || !TryEnum(command.Value("kind"), out FortKind kind) ||
                draft.Forts.Any(item => item.Id == id || (item.Target == target && (item.Kind == FortKind.Fence) == (kind == FortKind.Fence))))
                return GameResult.Fail("진지의 종류, 이름과 위치를 확인해 주세요.", "InvalidFort");
            bool validTarget = kind == FortKind.Fence ? draft.Setup.Edges.Any(item => item.Id == target && !item.Water) :
                draft.Setup.Nodes.Any(item => item.Id == target) && (kind != FortKind.Bridgehead ||
                    draft.Setup.Edges.Any(item => item.Bridge && !item.Water && (item.A == target || item.B == target)));
            if (!validTarget) return GameResult.Fail("이 위치에는 해당 진지를 지을 수 없습니다.", "InvalidFortTarget");
            var cost = 진형방어Policy.BuildSupply(kind);
            if (draft.Supply < cost) return GameResult.Fail("보급 상자가 부족합니다.", "InsufficientSupply");
            draft.Supply -= cost;
            draft.Forts.Add(new DefenseFort { Id = id, Kind = kind, Target = target, Builder = squad.Id,
                BuildTurnsLeft = 진형방어Policy.BuildTurns(kind, squad), Durability = 진형방어Policy.Durability(kind) });
            return GameResult.Done("진지 공사를 시작했습니다.");
        }

        private static GameResult Repair(DefenseState draft, GameCommand command)
        {
            var squad = Available(draft, command.Value("squad"));
            if (squad == null || Building(draft, squad.Id) || draft.Placements.Any(item => item.Squad == squad.Id))
                return GameResult.Fail("보수할 분대가 필요합니다.", "SquadUnavailable");
            var fort = draft.Forts.Find(item => item.Id == command.Value("fort") && item.BuildTurnsLeft == 0);
            if (fort == null || fort.Durability >= 진형방어Policy.Durability(fort.Kind))
                return GameResult.Fail("보수할 진지가 없습니다.", "NoRepairNeeded");
            if (draft.Supply < 진형방어Policy.RepairSupply) return GameResult.Fail("보급 상자가 부족합니다.", "InsufficientSupply");
            draft.Supply -= 진형방어Policy.RepairSupply;
            fort.Durability = 진형방어Policy.Durability(fort.Kind);
            return GameResult.Done("진지를 보수했습니다.");
        }

        private GameResult Alarm(DefenseState draft, GameCommand command)
        {
            if (draft.Alarm != null) return GameResult.Fail("이미 확인할 경보가 있습니다.", "AlarmActive");
            var entrance = command.Value("entrance");
            var alarmSeed = seed;
            if (draft.Setup == null || !TryEnum(command.Value("element"), out DefenseElement element) ||
                !진형방어Battle.Reachable(draft.Setup, entrance) ||
                (!string.IsNullOrEmpty(command.Value("seed")) &&
                    !uint.TryParse(command.Value("seed"), NumberStyles.None, CultureInfo.InvariantCulture, out alarmSeed)))
                return GameResult.Fail("경보의 입구·기운·씨앗을 확인해 주세요.", "InvalidAlarm");
            draft.Alarm = new DefenseAlarm { Entrance = entrance, Element = element, Seed = alarmSeed };
            return GameResult.Done("흑광 경보가 열렸습니다.");
        }

        private static GameResult Begin(DefenseState draft)
        {
            if (draft.Alarm == null) return GameResult.Fail("열린 경보가 없습니다.", "NoAlarm");
            if (draft.NowMs > long.MaxValue - 진형방어Policy.StepMs)
                return GameResult.Fail("시험 시각이 범위를 넘었습니다.", "ClockOutOfRange");
            var formation = 진형방어Policy.Formation(draft);
            foreach (var placement in draft.Placements)
            {
                var squad = draft.Squads.Find(item => item.Id == placement.Squad);
                var fort = placement.Kind == PlacementKind.Fort ? draft.Forts.Find(item => item.Id == placement.Target) : null;
                placement.Stamina = 진형방어Policy.Stamina(squad, placement.Kind, fort?.Kind);
            }
            draft.Battle = new BattleState { StartedMs = draft.NowMs, NextStepMs = draft.NowMs + 진형방어Policy.StepMs,
                WaveSizes = 진형방어Policy.Waves(draft.Alarm.Seed),
                Participants = draft.Squads.Where(squad => draft.Placements.Any(item => item.Squad == squad.Id))
                    .SelectMany(squad => squad.Members).Select(member => member.Id).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                InitialKind = formation.Kind, InitialSquads = formation.Squads,
                KeptFormation = formation.Kind != FormationKind.None,
                HadCounter = formation.Kind != FormationKind.None &&
                    진형방어Policy.Beats(진형방어Policy.ElementOf(formation.Kind), draft.Alarm.Element) };
            draft.Result = null;
            return GameResult.Done("방어전을 시작합니다.");
        }

        private static bool Building(DefenseState s, string id) => s.Forts.Any(f => f.Builder == id && f.BuildTurnsLeft > 0);
        private static DefenseSquad Available(DefenseState s, string id) =>
            s.Squads.Find(item => item.Id == id && item.Members.All(진형방어Policy.Eligible));
        private static bool TryEnum<T>(string value, out T result) where T : struct =>
            Enum.TryParse(value, out result) && Enum.IsDefined(typeof(T), result) && result.ToString() == value;
        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool ValidPoint(DefensePoint point) => point != null && Finite(point.X) && Finite(point.Z) &&
            Math.Abs(point.X) <= 100000 && Math.Abs(point.Z) <= 100000;

        private static bool ValidSetup(DefenseSetup s)
        {
            if (s?.Sites == null || s.Nodes == null || s.Edges == null) return false;
            if (!s.Sites.All(p => ValidPoint(p) && ValidId(p.Id) && Finite(p.Area) && p.Area > 0 && Finite(p.Height) && p.Height >= 0) ||
                !s.Nodes.All(p => ValidPoint(p) && ValidId(p.Id))) return false;
            var nodes = s.Nodes.GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            if (!ValidId(s.Target) || !nodes.ContainsKey(s.Target) || nodes.Count != s.Nodes.Count ||
                s.Sites.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != s.Sites.Count) return false;
            return s.Edges.All(e => e != null && ValidId(e.Id) && ValidId(e.A) && ValidId(e.B) && nodes.TryGetValue(e.A, out var a) &&
                nodes.TryGetValue(e.B, out var b) && Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) >= .01 && !(e.Bridge && e.Water)) &&
                s.Edges.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() == s.Edges.Count;
        }

        private static bool ValidSquad(DefenseSquad squad) => squad != null && ValidId(squad.Id) &&
            squad.Members != null && squad.Members.Count >= 진형방어Policy.MinimumMembers && squad.Members.Count <= 진형방어Policy.MaximumMembers &&
            squad.Members.All(p => p != null && ValidId(p.Id) && ValidId(p.Name) && p.DefenseCount >= 0 &&
                p.Awakening >= 0 && p.Awakening <= 진형방어Policy.MaximumSkill && p.ReachLevel >= 0 &&
                p.ReachLevel <= 진형방어Policy.MaximumSkill && p.Breath >= 0 && p.Breath <= 진형방어Policy.MaximumSkill &&
                Enum.IsDefined(typeof(DefenseElement), p.Element)) &&
            squad.Members.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() == squad.Members.Count;

        private static bool ValidSave(DefenseState s)
        {
            if (s == null || s.Version != DefenseState.CurrentVersion || s.NowMs < 0 || s.Supply < 0 ||
                s.Squads == null || s.Placements == null || s.Forts == null || !s.Squads.All(ValidSquad))
                return false;
            if (s.Squads.Select(squad => squad.Id).Distinct(StringComparer.Ordinal).Count() != s.Squads.Count ||
                s.Squads.SelectMany(squad => squad.Members).GroupBy(member => member.Id, StringComparer.Ordinal).Any(group => group.Skip(1).Any()))
                return false;
            if (s.Setup == null) return s.Alarm == null && s.Battle == null && s.Forts.Count == 0 && s.Placements.Count == 0;
            if (!ValidSetup(s.Setup) || s.Forts.Any(f => f == null || !ValidId(f.Id) || !Enum.IsDefined(typeof(FortKind), f.Kind) ||
                f.BuildTurnsLeft < 0 || !Finite(f.Durability) || f.Durability < 0 || f.Durability > 진형방어Policy.Durability(f.Kind) ||
                (f.Kind == FortKind.Fence ? !s.Setup.Edges.Any(e => e.Id == f.Target && !e.Water) : !s.Setup.Nodes.Any(n => n.Id == f.Target))))
                return false;
            if (s.Forts.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != s.Forts.Count ||
                s.Forts.GroupBy(f => (f.Target, f.Kind == FortKind.Fence)).Any(group => group.Skip(1).Any()))
                return false;
            if (s.Placements.Any(p => p == null || Available(s, p.Squad) == null || !Finite(p.Stamina) || p.Stamina < 0 ||
                !Enum.IsDefined(typeof(PlacementKind), p.Kind) ||
                (p.Kind == PlacementKind.Fort
                    ? !s.Forts.Any(f => f.Id == p.Target && f.Kind != FortKind.Fence && f.BuildTurnsLeft == 0)
                    : !s.Setup.Sites.Any(site => site.Id == p.Target && (p.Kind == PlacementKind.Inside ||
                        Available(s, p.Squad).Members.Count <= 진형방어Policy.Capacity(site))))))
                return false;
            if (s.Placements.Select(p => p.Squad).Distinct(StringComparer.Ordinal).Count() != s.Placements.Count ||
                s.Placements.GroupBy(p => (p.Target, p.Kind)).Any(group => group.Skip(1).Any())) return false;
            if (s.Alarm != null && (!Enum.IsDefined(typeof(DefenseElement), s.Alarm.Element) || !진형방어Battle.Reachable(s.Setup, s.Alarm.Entrance)))
                return false;
            var battle = s.Battle;
            return battle == null || (s.Alarm != null && battle.StartedMs >= 0 && battle.StartedMs <= s.NowMs && battle.NextStepMs > s.NowMs &&
                battle.WaveSizes != null && battle.WaveSizes.Count == 진형방어Policy.WaveCount &&
                battle.WaveSizes.All(w => w >= 진형방어Policy.MinWave && w < 진형방어Policy.MinWave + 진형방어Policy.WaveSpread) &&
                battle.Spawned >= 0 && battle.Spawned <= battle.WaveSizes.Sum() && battle.Cleansed >= 0 && battle.Leaked >= 0 &&
                battle.EdgeEntries != null && Finite(battle.LeakExposure) && battle.LeakExposure >= 0 &&
                battle.Motes != null && (long)battle.Cleansed + battle.Leaked + battle.Motes.Count == battle.Spawned &&
                battle.Motes.All(m => ValidMote(s.Setup, m)) && battle.Participants != null && battle.InitialSquads != null);
        }

        private static bool ValidMote(DefenseSetup setup, DefenseMote mote)
        {
            if (mote == null || !Finite(mote.Purity) || mote.Purity <= 0 || !Finite(mote.Progress) || mote.Progress < 0)
                return false;
            var from = setup.Nodes.Find(node => node.Id == mote.Node);
            if (from == null || from.Id == setup.Target) return false;
            if (mote.Edge == null) return mote.Progress == 0;
            var edge = setup.Edges.Find(item => item.Id == mote.Edge && !item.Water && (item.A == from.Id || item.B == from.Id));
            if (edge == null) return false;
            var to = setup.Nodes.Find(node => node.Id == (edge.A == from.Id ? edge.B : edge.A));
            var dx = to.X - from.X;
            var dz = to.Z - from.Z;
            return mote.Progress < Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
