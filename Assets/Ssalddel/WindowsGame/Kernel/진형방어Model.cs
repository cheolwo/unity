using System;
using System.Collections.Generic;

namespace Ssalddel.WindowsGame.Kernel
{
    public enum DefenseElement { Wood, Fire, Earth, Metal, Water }
    public enum FormationKind { None, Line, Arrow, Square, Circle, Curve }
    public enum PlacementKind { Roof, Inside, Fort }
    public enum FortKind { Fence, Beacon, Bridgehead }

    public class DefensePoint { public double X, Z; }
    public sealed class RoofSite : DefensePoint
    {
        public string Id;
        public double Height, Area;
    }

    public sealed class DefenseNode : DefensePoint { public string Id; }
    public sealed class DefenseEdge
    {
        public string Id, A, B;
        public bool Bridge, Water;
    }

    public sealed class DefenseSetup
    {
        public List<RoofSite> Sites = new List<RoofSite>();
        public List<DefenseNode> Nodes = new List<DefenseNode>();
        public List<DefenseEdge> Edges = new List<DefenseEdge>();
        public string Target;
    }

    public sealed class Defender
    {
        public string Id, Name;
        public int Awakening, ReachLevel, Breath, DefenseCount;
        public DefenseElement Element;
        public bool Busy, Resident;
    }

    public sealed class DefenseSquad
    {
        public string Id;
        public List<Defender> Members = new List<Defender>();
    }

    public sealed class DefensePlacement
    {
        public string Squad, Target;
        public PlacementKind Kind;
        public double Stamina;
    }

    public sealed class DefenseFort
    {
        public string Id, Target, Builder;
        public FortKind Kind;
        public int BuildTurnsLeft;
        public double Durability;
    }

    public sealed class DefenseVertex : DefensePoint
    {
        public string Squad;
        public int Reach;
        public DefenseElement Element;
    }

    public sealed class DefenseLink
    {
        public string A, B;
        public bool Connected;
    }

    public sealed class FormationRead
    {
        public FormationKind Kind;
        public List<string> Squads = new List<string>();
        public List<DefensePoint> Corners = new List<DefensePoint>();
        public List<DefenseLink> Links = new List<DefenseLink>();
        public string Tip;
    }

    public sealed class DefenseAlarm
    {
        public string Entrance;
        public DefenseElement Element;
        public uint Seed;
    }

    public sealed partial class BattleState
    {
        public long StartedMs, NextStepMs;
        public List<int> WaveSizes = new List<int>();
        public int Spawned, Cleansed, Leaked;
        public List<string> Participants = new List<string>();
        public FormationKind InitialKind;
        public List<string> InitialSquads = new List<string>();
        public bool KeptFormation, HadCounter;
        public List<DefenseMote> Motes = new List<DefenseMote>();
    }

    public sealed partial class DefenseMote
    {
        public string Node, Edge;
        public double Progress, Purity;
        public bool CurveUsed;
    }

    public sealed partial class DefenseResult
    {
        public int Spawned, Cleansed, Leaked, Light, Reputation, Supply, Education, LightLost, AwakeLost;
        public string Grade;
        public decimal Multiplier;
        public bool KeptFormation, Counter;
    }

    public sealed class DefenseState
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public long NowMs;
        public int Supply = 진형방어Policy.StartingSupply;
        public DefenseSetup Setup;
        public List<DefenseSquad> Squads = new List<DefenseSquad>();
        public List<DefensePlacement> Placements = new List<DefensePlacement>();
        public List<DefenseFort> Forts = new List<DefenseFort>();
        public DefenseAlarm Alarm;
        public BattleState Battle;
        public DefenseResult Result;
    }
}
