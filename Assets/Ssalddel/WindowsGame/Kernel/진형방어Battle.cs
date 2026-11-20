using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    public sealed partial class BattleState
    {
        public double LeakExposure;
        public bool FormationBroken;
        public Dictionary<string, int> EdgeEntries = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public sealed partial class DefenseResult
    {
        public Dictionary<string, int> EdgeEntries = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public static class 진형방어Battle
    {
        public static bool Reachable(DefenseSetup setup, string entrance)
        {
            if (setup == null || string.IsNullOrEmpty(entrance) || entrance == setup.Target)
                return false;
            return new Routes(setup, null).Next(entrance) != null;
        }

        public static void Tick(DefenseState state, long elapsedMs)
        {
            if (state.Battle == null || state.Battle.NextStepMs > elapsedMs) return;
            var arena = new Arena(state.Setup);
            var battle = state.Battle;
            var formation = battle.FormationBroken ? new FormationRead() : 진형방어Policy.Formation(state);
            while (battle.NextStepMs <= elapsedMs)
            {
                Step(state, arena, battle, formation);
                if (battle.Spawned == battle.WaveSizes.Sum() && battle.Motes.Count == 0)
                {
                    Finish(state, battle);
                    return;
                }
                if (battle.NextStepMs > long.MaxValue - 진형방어Policy.StepMs)
                    throw new InvalidOperationException("시험 방어전이 지원되는 시각을 넘었습니다.");
                battle.NextStepMs += 진형방어Policy.StepMs;
                if (battle.FormationBroken && formation.Kind != FormationKind.None)
                    formation = new FormationRead();
            }
        }

        private static void Step(DefenseState state, Arena arena, BattleState battle, FormationRead formation)
        {
            Spawn(state.Alarm, battle);
            bool formed = formation.Kind != FormationKind.None;
            var element = 진형방어Policy.ElementOf(formation.Kind);
            if (formed)
                battle.HadCounter |= 진형방어Policy.Beats(element, state.Alarm.Element);
            battle.KeptFormation &= formed && formation.Kind == battle.InitialKind &&
                battle.InitialSquads.Count == formation.Squads.Count &&
                battle.InitialSquads.All(formation.Squads.Contains);

            const double seconds = 진형방어Policy.StepMs / 1000d;
            var sources = Sources(state, arena, formation, state.Alarm.Element);
            var fences = state.Forts.Where(fort => IsActive(fort) && fort.Kind == FortKind.Fence)
                .ToDictionary(fort => fort.Target, StringComparer.Ordinal);
            Routes routes = null;
            var exhaustedByContact = new HashSet<DefensePlacement>();
            var moving = new List<DefenseMote>(battle.Motes.Count);
            var goal = arena.Nodes[state.Setup.Target];
            bool shielded = formation.Kind == FormationKind.Square && Inside(goal, formation.Corners);
            foreach (var mote in battle.Motes)
            {
                double timeLeft = seconds;
                while (mote.Node != state.Setup.Target && timeLeft > 0)
                {
                    if (string.IsNullOrEmpty(mote.Edge))
                    {
                        if (routes == null)
                            routes = new Routes(state.Setup, (edge, a, b) => Burden(edge, a, b, fences, sources));
                        var next = routes.Next(mote.Node);
                        if (next == null) break;
                        mote.Edge = next.Id;
                        battle.EdgeEntries.TryGetValue(next.Id, out int count);
                        battle.EdgeEntries[next.Id] = count + 1;
                    }
                    var edge = arena.Edges[mote.Edge];
                    var a = arena.Nodes[mote.Node];
                    string nextNode = Other(edge, mote.Node);
                    var b = arena.Nodes[nextNode];
                    double length = Distance(a, b);
                    var position = Along(a, b, mote.Progress / length);
                    if (!mote.CurveUsed && mote.Progress > 0 && formation.Kind == FormationKind.Curve &&
                        formation.Squads.Count != 0 && NearCurve(position, formation.Corners))
                    {
                        mote.Progress = Math.Max(0, mote.Progress - 진형방어Policy.CurvePushMetres);
                        mote.CurveUsed = true;
                        position = Along(a, b, mote.Progress / length);
                    }
                    var speed = (double)진형방어Policy.MoteSpeed;
                    // 직진을 가로지르는 길 구간은 다음 마디에 도착할 때까지 느려진다.
                    if (formation.Kind == FormationKind.Line && Crosses(a, b, formation.Corners[0], formation.Corners[formation.Corners.Count - 1]))
                        speed *= 1 - 진형방어Policy.LineSlow;
                    if (fences.TryGetValue(edge.Id, out var fence))
                        speed *= 1 - 진형방어Policy.FenceSlow;
                    double advance = Math.Min(length - mote.Progress, speed * timeLeft);
                    double contactSeconds = advance / speed;
                    var mid = Along(a, b, (mote.Progress + advance / 2) / length);
                    foreach (var source in sources)
                    {
                        if (!source.Covers(edge, mid)) continue;
                        mote.Purity -= source.Strength * contactSeconds;
                        if (source.Placement != null) exhaustedByContact.Add(source.Placement);
                    }
                    if (fence != null)
                    {
                        fence.Durability = Math.Max(0, fence.Durability - 진형방어Policy.FenceWearPerSecond * contactSeconds);
                        if (fence.Durability == 0)
                        {
                            fences.Remove(edge.Id);
                            routes = null;
                        }
                    }
                    if (mote.Purity <= 0) break;
                    timeLeft -= contactSeconds;
                    if (advance >= length - mote.Progress)
                    {
                        mote.Node = nextNode;
                        mote.Edge = null;
                        mote.Progress = 0;
                    }
                    else
                    {
                        mote.Progress += advance;
                    }
                }
                if (mote.Purity <= 0)
                    battle.Cleansed++;
                else if (mote.Node == state.Setup.Target)
                {
                    battle.Leaked++;
                    battle.LeakExposure += shielded ? 진형방어Policy.SquareLeakMultiplier : 1;
                }
                else
                    moving.Add(mote);
            }
            battle.Motes = moving;
            foreach (var placement in exhaustedByContact)
            {
                placement.Stamina = Math.Max(0, placement.Stamina - seconds);
                if (placement.Stamina > 1e-9) continue;
                state.Placements.Remove(placement);
                if (!formation.Squads.Contains(placement.Squad)) continue;
                battle.FormationBroken = true;
                battle.KeptFormation = false;
            }
        }

        private static void Spawn(DefenseAlarm alarm, BattleState battle)
        {
            var eligibleWave = (battle.NextStepMs - battle.StartedMs - 진형방어Policy.StepMs) / 진형방어Policy.WaveIntervalMs;
            var count = battle.WaveSizes.Take((int)Math.Min(eligibleWave + 1, battle.WaveSizes.Count)).Sum();
            while (battle.Spawned < count)
            {
                battle.Motes.Add(new DefenseMote { Node = alarm.Entrance, Purity = 진형방어Policy.MotePurity });
                battle.Spawned++;
            }
        }

        private static void Finish(DefenseState state, BattleState battle)
        {
            var grade = 진형방어Policy.Grade(battle.Spawned == 0 ? 0 : (double)battle.Cleansed / battle.Spawned);
            var multiplier = 진형방어Policy.Reward(grade, battle.KeptFormation, battle.HadCounter);
            var adjusted = (double)multiplier * battle.Cleansed;
            var result = new DefenseResult
            {
                Spawned = battle.Spawned,
                Cleansed = battle.Cleansed,
                Leaked = battle.Leaked,
                Light = (int)Math.Floor(adjusted * 진형방어Policy.LightPerCleanse),
                Reputation = (int)Math.Floor(adjusted * 진형방어Policy.ReputationPerCleanse),
                Supply = (int)Math.Floor(adjusted * 진형방어Policy.SupplyPerCleanse),
                Education = (int)Math.Floor(battle.Cleansed * 진형방어Policy.EducationPerCleanse),
                LightLost = (int)Math.Ceiling(battle.LeakExposure * 진형방어Policy.LeakLight),
                AwakeLost = (int)Math.Ceiling(battle.LeakExposure * 진형방어Policy.LeakAwake),
                Grade = grade,
                Multiplier = multiplier,
                KeptFormation = battle.KeptFormation,
                Counter = battle.HadCounter,
                EdgeEntries = battle.EdgeEntries
            };
            var participants = new HashSet<string>(battle.Participants, StringComparer.Ordinal);
            foreach (var member in state.Squads.SelectMany(squad => squad.Members))
                if (participants.Contains(member.Id) && member.DefenseCount < int.MaxValue)
                    member.DefenseCount++;
            state.Supply += Math.Min(result.Supply, int.MaxValue - state.Supply);
            state.Result = result;
            state.Battle = null;
            state.Alarm = null;
        }

        private static List<Source> Sources(DefenseState state, Arena arena, FormationRead formation, DefenseElement enemy)
        {
            var list = new List<Source>();
            foreach (var placement in state.Placements)
            {
                var squad = state.Squads.First(s => s.Id == placement.Squad);
                var leader = 진형방어Policy.Leader(squad);
                double strength = 진형방어Policy.Strength(squad);
                if (placement.Kind == PlacementKind.Fort)
                {
                    var fort = state.Forts.First(f => f.Id == placement.Target);
                    if (!IsActive(fort)) continue;
                    if (fort.Kind == FortKind.Beacon) strength *= 진형방어Policy.BeaconGarrisonStrength;
                    strength *= 1 + 진형방어Policy.Stars(leader) * 진형방어Policy.FortStrengthPerStar;
                    list.Add(Source.Fort(fort, arena.Nodes[fort.Target], strength, placement));
                    continue;
                }
                var site = arena.Sites[placement.Target];
                double radius = 진형방어Policy.Radius(site);
                if (placement.Kind == PlacementKind.Inside)
                    radius *= 진형방어Policy.IndoorRadius;
                if (formation.Squads.Contains(squad.Id))
                {
                    var own = 진형방어Policy.ElementOf(formation.Kind);
                    strength += squad.Members.Where(member => member.Element == own)
                        .Sum(member => (1 + 0.5 * member.Awakening) * (진형방어Policy.MatchingElement - 1));
                    strength *= 진형방어Policy.ElementMultiplier(own, enemy);
                    if (formation.Kind == FormationKind.Arrow && formation.Tip == squad.Id)
                        strength *= 진형방어Policy.ArrowStrength;
                    if (formation.Kind == FormationKind.Circle)
                        radius *= 진형방어Policy.CircleRadius;
                }
                list.Add(new Source { Position = site, Radius = radius, Strength = strength, Placement = placement });
            }
            foreach (var fort in state.Forts)
                if (IsActive(fort) && fort.Kind != FortKind.Fence)
                    list.Add(Source.Fort(fort, arena.Nodes[fort.Target], 진형방어Policy.BeaconStrength, null));
            return list;
        }

        private static bool IsActive(DefenseFort fort)
        {
            return fort.BuildTurnsLeft == 0 && fort.Durability > 0;
        }

        private static double Burden(DefenseEdge edge, DefensePoint a, DefensePoint b,
            Dictionary<string, DefenseFort> fences, List<Source> sources)
        {
            var burden = fences.ContainsKey(edge.Id) ? 진형방어Policy.FenceSlow : 0;
            foreach (var source in sources)
                if (source.Intersects(edge, a, b)) burden += source.Strength;
            return burden;
        }

        private static DefensePoint Along(DefensePoint a, DefensePoint b, double t)
        {
            return new DefensePoint { X = a.X + (b.X - a.X) * t, Z = a.Z + (b.Z - a.Z) * t };
        }

        private static bool NearCurve(DefensePoint p, List<DefensePoint> corners)
        {
            for (int i = 1; i < corners.Count; i++)
                if (DistanceToSegment(p, corners[i - 1], corners[i]) <= 진형방어Policy.CurveTolerance)
                    return true;
            return false;
        }

        private static double DistanceToSegment(DefensePoint p, DefensePoint a, DefensePoint b)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            var lengthSquared = dx * dx + dz * dz;
            if (lengthSquared == 0) return Distance(p, a);
            var fraction = ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / lengthSquared;
            return Distance(p, Along(a, b, Math.Max(0, Math.Min(1, fraction))));
        }

        private static bool Inside(DefensePoint point, List<DefensePoint> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((point.Z < a.Z) == (point.Z < b.Z)) continue;
                if (point.X < a.X + (b.X - a.X) * (point.Z - a.Z) / (b.Z - a.Z))
                    inside = !inside;
            }
            return inside;
        }

        private static bool Crosses(DefensePoint a, DefensePoint b, DefensePoint c, DefensePoint d)
        {
            double start = Side(c, d, a);
            double end = Side(c, d, b);
            return (start != 0 || end != 0) && start * end <= 0 && Side(a, b, c) * Side(a, b, d) <= 0;
        }

        private static double Side(DefensePoint a, DefensePoint b, DefensePoint p)
        {
            return (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);
        }

        private sealed class Arena
        {
            public readonly Dictionary<string, DefenseNode> Nodes;
            public readonly Dictionary<string, DefenseEdge> Edges;
            public readonly Dictionary<string, RoofSite> Sites;

            public Arena(DefenseSetup setup)
            {
                Nodes = setup.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
                Edges = setup.Edges.ToDictionary(edge => edge.Id, StringComparer.Ordinal);
                Sites = setup.Sites.ToDictionary(site => site.Id, StringComparer.Ordinal);
            }
        }

        private sealed class Source
        {
            public DefensePoint Position;
            public double Radius;
            public double Strength;
            public DefensePlacement Placement;
            private string bridgeNode;

            public bool Covers(DefenseEdge edge, DefensePoint position)
            {
                return bridgeNode == null
                    ? Distance(Position, position) <= Radius
                    : SpansBridge(edge);
            }

            public bool Intersects(DefenseEdge edge, DefensePoint a, DefensePoint b)
            {
                return bridgeNode == null ? DistanceToSegment(Position, a, b) <= Radius : SpansBridge(edge);
            }

            private bool SpansBridge(DefenseEdge edge) => edge.Bridge && (edge.A == bridgeNode || edge.B == bridgeNode);

            public static Source Fort(DefenseFort fort, DefensePoint position, double strength, DefensePlacement placement)
            {
                return new Source
                {
                    Position = position,
                    Strength = strength,
                    Placement = placement,
                    Radius = 진형방어Policy.BeaconRadius,
                    bridgeNode = fort.Kind == FortKind.Bridgehead ? fort.Target : null
                };
            }
        }

        private static double Distance(DefensePoint a, DefensePoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static string Other(DefenseEdge edge, string node)
        {
            return edge.A == node ? edge.B : edge.A;
        }

        private sealed class Routes
        {
            private sealed class Label
            {
                public string Node;
                public double Burden;
                public double Metres;
            }

            private readonly Dictionary<string, DefenseEdge> next = new Dictionary<string, DefenseEdge>(StringComparer.Ordinal);

            public Routes(DefenseSetup setup, Func<DefenseEdge, DefensePoint, DefensePoint, double> burden)
            {
                var nodes = setup.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
                if (!nodes.ContainsKey(setup.Target)) return;
                var edgesByNode = nodes.Keys.ToDictionary(key => key, key => new List<DefenseEdge>(), StringComparer.Ordinal);
                var costs = new Dictionary<string, (double Burden, double Length)>(StringComparer.Ordinal);
                foreach (var edge in setup.Edges)
                {
                    if (edge.Water) continue;
                    var a = nodes[edge.A];
                    var b = nodes[edge.B];
                    costs.Add(edge.Id, (burden == null ? 0 : burden(edge, a, b), Distance(a, b)));
                    edgesByNode[edge.A].Add(edge);
                    edgesByNode[edge.B].Add(edge);
                }

                // 목표부터 역으로 계산한다. 막힘 합이 같으면 길이와 길 식별자 순이다.
                var pending = new SortedSet<Label>(Comparer<Label>.Create((a, b) =>
                {
                    int cost = a.Burden.CompareTo(b.Burden);
                    if (cost != 0) return cost;
                    cost = a.Metres.CompareTo(b.Metres);
                    return cost != 0 ? cost : StringComparer.Ordinal.Compare(a.Node, b.Node);
                }));
                var best = new Dictionary<string, Label>(StringComparer.Ordinal);
                var start = new Label { Node = setup.Target };
                best.Add(start.Node, start);
                pending.Add(start);
                while (pending.Count != 0)
                {
                    var to = pending.Min;
                    pending.Remove(to);
                    foreach (var edge in edgesByNode[to.Node])
                    {
                        string from = Other(edge, to.Node);
                        var cost = costs[edge.Id];
                        var label = new Label { Node = from, Burden = to.Burden + cost.Burden, Metres = to.Metres + cost.Length };
                        if (best.TryGetValue(from, out var prior))
                        {
                            int comparison = pending.Comparer.Compare(label, prior);
                            if (comparison > 0) continue;
                            if (comparison == 0 && next.TryGetValue(from, out var previous) &&
                                StringComparer.Ordinal.Compare(edge.Id, previous.Id) >= 0) continue;
                            if (comparison == 0 && from == setup.Target) continue;
                            if (comparison < 0) pending.Remove(prior);
                            else
                            {
                                next[from] = edge;
                                continue;
                            }
                        }
                        best[from] = label;
                        next[from] = edge;
                        pending.Add(label);
                    }
                }
            }

            public DefenseEdge Next(string node)
            {
                return next.TryGetValue(node, out var edge) ? edge : null;
            }
        }
    }
}
