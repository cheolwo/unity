using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>2.0 시험 규칙의 가안 수치를 모아 둔다.</summary>
    public static class 진형방어Policy
    {
        public const int MinimumMembers = 3, MaximumMembers = 5, MaximumSkill = 3, StartingSupply = 3;
        public const bool ResidentsAllowed = false;
        public const int RoofAreaPerPerson = 60, RoofMaxPeople = 4;
        public const int LineTolerance = 15, LineSpan = 150, ArrowAngle = 60, SquareTolerance = 20;
        public const double CircleRadialTolerance = .20;
        public const int CircleGap = 100, CurveTolerance = 20;
        public const int StepMs = 100, WaveIntervalMs = 4000, WaveCount = 3, MinWave = 5, WaveSpread = 5;
        public const double MoteSpeed = 25, MotePurity = 5, FenceSlow = .70, FenceWearPerSecond = 30;
        public const double BeaconRadius = 60, BeaconStrength = .5, IndoorRadius = .6, ArrowStrength = 1.5;
        public const double CircleRadius = 1.3, MatchingElement = 1.1, LineSlow = .60, CurvePushMetres = 12;
        public const double FortStrengthPerStar = .05, BeaconGarrisonStrength = 2, SquareLeakMultiplier = .5;
        public const double LightPerCleanse = 1, ReputationPerCleanse = .2, SupplyPerCleanse = .25, EducationPerCleanse = .2;
        public const int LeakLight = 1, LeakAwake = 0, RepairSupply = 1;
        private static readonly int[] ReachMetres = { 90, 130, 170, 210 };
        private static readonly int[] FortTurns = { 1, 2, 3 };
        private static readonly int[] FortSupplies = { 1, 2, 3 };
        private static readonly double[] FortDurability = { 120, 100, 150 };

        public static bool Eligible(Defender person) => person.Awakening >= 1 && !person.Busy && (ResidentsAllowed || !person.Resident);
        public static Defender Leader(DefenseSquad squad) => squad.Members.Where(Eligible)
            .OrderByDescending(person => person.DefenseCount).ThenByDescending(person => person.Breath)
            .ThenBy(person => person.Name, StringComparer.Ordinal).ThenBy(person => person.Id, StringComparer.Ordinal).FirstOrDefault();

        public static int Stars(Defender leader) => leader == null ? 0 :
            leader.DefenseCount >= 7 ? 3 : leader.DefenseCount >= 5 ? 2 : leader.DefenseCount >= 3 ? 1 : 0;
        public static int Capacity(RoofSite site) => (int)Math.Min(RoofMaxPeople, Math.Max(1, Math.Floor(site.Area / RoofAreaPerPerson)));
        public static double Radius(RoofSite site) => Math.Min(90, 30 + .8 * site.Height);
        public static double Strength(DefenseSquad squad) => squad.Members.Sum(person => 1 + .5 * person.Awakening);
        public static double Stamina(DefenseSquad squad, PlacementKind kind, FortKind? fort = null) =>
            (3 + squad.Members.Min(person => person.Breath) + .5 * Stars(Leader(squad))) *
            (kind == PlacementKind.Inside || fort == FortKind.Bridgehead ? 2 : 1);
        public static int BuildTurns(FortKind kind, DefenseSquad squad) => Math.Max(1, FortTurns[(int)kind] - (Stars(Leader(squad)) == 3 ? 1 : 0));
        public static int BuildSupply(FortKind kind) => FortSupplies[(int)kind];
        public static double Durability(FortKind kind) => FortDurability[(int)kind];

        public static FormationRead Formation(DefenseState state)
        {
            if (state.Setup == null) return new FormationRead();
            var vertices = new List<DefenseVertex>();
            foreach (var placed in state.Placements.Where(item => item.Kind == PlacementKind.Roof))
            {
                var squad = state.Squads.Find(item => item.Id == placed.Squad);
                var site = state.Setup.Sites.Find(item => item.Id == placed.Target);
                vertices.Add(new DefenseVertex { X = site.X, Z = site.Z, Squad = squad.Id,
                    Reach = ReachMetres[squad.Members.Min(person => person.ReachLevel)], Element = Leader(squad).Element });
            }
            return 진형방어Formation.Read(state.Setup, vertices, state.Alarm?.Entrance);
        }

        public static DefenseElement ElementOf(FormationKind kind) => (DefenseElement)((int)kind - 1);
        public static bool Beats(DefenseElement own, DefenseElement enemy) => ((int)own + 2) % 5 == (int)enemy;
        public static double ElementMultiplier(DefenseElement own, DefenseElement enemy) =>
            Beats(own, enemy) ? 1.5 : ((int)own + 1) % 5 == (int)enemy ? .7 : 1;
        public static string Grade(double stopped) => stopped >= .95 ? "S" : stopped >= .80 ? "A" : stopped >= .60 ? "B" : "C";
        public static decimal Reward(string grade, bool kept, bool counter)
        {
            var basis = grade == "S" ? 1.5m : grade == "A" ? 1.2m : grade == "B" ? 1m : .6m;
            return basis * (1 + (kept ? .2m : 0m) + (counter ? .1m : 0m));
        }

        public static List<int> Waves(uint seed)
        {
            var waves = new List<int>();
            for (var i = 0; i < WaveCount; i++)
            {
                seed = unchecked(1664525u * seed + 1013904223u);
                waves.Add(MinWave + (int)(seed % WaveSpread));
            }
            return waves;
        }
    }
}
