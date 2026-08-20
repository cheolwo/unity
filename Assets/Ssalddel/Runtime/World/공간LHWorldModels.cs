using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public static class 공간LHWorldCodes
    {
        public const string SchemaVersion = "lh-world.v1";
        public const string GeneratorVersion = "lh-generator.pyeongchang.v1";
        public const string WorldSeed = "pyeongchang-daegwallyeong-farm-2026";
        public const string LocalEngine = "LocalLhEngine";
        public const string ScenarioProcedural = "ScenarioProcedural";
        public const string AuthoritativeWorld = "AuthoritativeWorld";
        public const string AreaSetStableId = "area-set:sim:pyeongchang:farm-hub-town.v1";
        public const string RecipeStableId = 공간TileStreamingCodes.RecipeStableId;
        public const string Detail = "Detail";
        public const string Active = "Active";
        public const string Prefetch = "Prefetch";
        public const string None = "None";
        public const string TerrainVisual = "TerrainVisual";
        public const string Collision = "Collision";
        public const string Connector = "Connector";
        public const string H1Interaction = "H1Interaction";
        public const string NpcNavigation = "NpcNavigation";
        public const string SeasonPresentation = "SeasonPresentation";
        public const string Spring = "Spring";
        public const string Summer = "Summer";
        public const string Autumn = "Autumn";
        public const string Winter = "Winter";
        public const int CenterL3X = 2801;
        public const int CenterL3Y = 4581;
        public const int L3CellSizeMeters = 125;
        public const int MinimumL3X = 2780;
        public const int MaximumL3X = 2823;
        public const int MinimumL3Y = 4560;
        public const int MaximumL3Y = 4603;
    }

    [Serializable]
    public sealed class 공간LHWorldProfileData
    {
        public string SchemaVersion = string.Empty;
        public string ProfileRevision = string.Empty;
        public string ProfileHashSha256 = string.Empty;
        public string WorldSeed = string.Empty;
        public string GeneratorVersion = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string AreaSetRevision = string.Empty;
        public string AreaSetBoundaryHashSha256 = string.Empty;
        public 공간LHLevelData[] Levels = Array.Empty<공간LHLevelData>();
        public int DetailRadius;
        public int ActiveRadius;
        public int PrefetchRadius;
        public int MaxConcurrentPreparations;
        public double BoundaryPrefetchFraction;
        public double MainThreadAssemblyBudgetMilliseconds;
        public int CachedCellCapacity;
        public int OriginShiftThresholdWorldUnits;
        public 공간LHGenerationLayerData[] GenerationLayers = Array.Empty<공간LHGenerationLayerData>();
        public bool PresentationOnly;
        public bool IsOperationalState;

        public int L3CellSize => Levels.Single(value => value.LevelCode == "L3").CellSizeMeters;

        public void Validate()
        {
            if (Levels != null)
            {
                foreach (var level in Levels)
                {
                    if (string.IsNullOrWhiteSpace(level.PrimaryHQueryLevelCode))
                        level.PrimaryHQueryLevelCode = level.DefaultHLevelCode;
                }
            }
            if (SchemaVersion != 공간LHWorldCodes.SchemaVersion
                || ProfileHashSha256 == null || ProfileHashSha256.Length != 64
                || string.IsNullOrWhiteSpace(WorldSeed)
                || GeneratorVersion != 공간LHWorldCodes.GeneratorVersion
                || AreaSetStableId != 공간LHWorldCodes.AreaSetStableId
                || AreaSetBoundaryHashSha256 == null || AreaSetBoundaryHashSha256.Length != 64
                || Levels == null || Levels.Length != 4
                || Levels.Select(value => value.LevelCode).Distinct().Count() != 4
                || Levels.Single(value => value.LevelCode == "L0").CellSizeMeters != 8000
                || Levels.Single(value => value.LevelCode == "L1").CellSizeMeters != 2000
                || Levels.Single(value => value.LevelCode == "L2").CellSizeMeters != 500
                || L3CellSize != 공간LHWorldCodes.L3CellSizeMeters
                || Levels.Any(value => !IsHLevelCode(value.PrimaryHQueryLevelCode))
                || DetailRadius != 1 || ActiveRadius != 2 || PrefetchRadius != 4
                || MaxConcurrentPreparations != 4
                || Math.Abs(BoundaryPrefetchFraction - .25d) > .000001d
                || MainThreadAssemblyBudgetMilliseconds <= 0d
                || CachedCellCapacity < 1 || OriginShiftThresholdWorldUnits < L3CellSize
                || GenerationLayers == null || GenerationLayers.Length < 6
                || !PresentationOnly || IsOperationalState)
                throw new InvalidOperationException("LHWorldProfileInvalid");
        }

        private static bool IsHLevelCode(string value)
            => value == "H1" || value == "H2" || value == "H3" || value == "H4";
    }

    [Serializable]
    public sealed class 공간LHLevelData
    {
        public string LevelCode = string.Empty;
        public int CellSizeMeters;
        // 기존 JSON 호환용 필드이며 L과 H가 같은 계층이라는 뜻은 아니다.
        public string DefaultHLevelCode = string.Empty;
        public string PrimaryHQueryLevelCode = string.Empty;
    }

    [Serializable]
    public sealed class 공간LHGenerationLayerData
    {
        public string LayerCode = string.Empty;
        public string[] DependsOnLayerCodes = Array.Empty<string>();
        public int MaximumPaddingMeters;
        public string OwnershipRuleCode = string.Empty;
    }

    [Serializable]
    public sealed class 공간LHCellPreviewRequestData
    {
        public string RequestEpoch = string.Empty;
        public string SessionStableId = string.Empty;
        public string RecipeStableId = 공간LHWorldCodes.RecipeStableId;
        public string AreaSetStableId = 공간LHWorldCodes.AreaSetStableId;
        public string FocusL3CellKey = string.Empty;
        public string MovementDirectionCode = 공간LHWorldCodes.None;
        public string[] RequiredCapabilityCodes = Array.Empty<string>();
        public string[] KnownCellPlanHashesSha256 = Array.Empty<string>();
        public long ExpectedWorldRevision;
    }

    [Serializable]
    public sealed class 공간LHCellPreviewData
    {
        public string RequestEpoch = string.Empty;
        public string RecipeStableId = string.Empty;
        public string AreaSetStableId = string.Empty;
        public string ContentSourceCode = 공간LHWorldCodes.ScenarioProcedural;
        public int WorldTick;
        public long WorldRevision;
        public 공간LHSeasonData Season = new();
        public 공간LHWorldProfileData Profile = new();
        public 공간LHCellPlanData[] Cells = Array.Empty<공간LHCellPlanData>();
        public string[] OutsideCoverageCellKeys = Array.Empty<string>();
        public bool IsCandidateOnly;
        public bool DoesNotApplyResourceLedgers;
        public bool IsOperationalState;

        public void Validate(string expectedEpoch)
        {
            Profile.Validate();
            if (RequestEpoch != expectedEpoch
                || RecipeStableId != 공간LHWorldCodes.RecipeStableId
                || AreaSetStableId != 공간LHWorldCodes.AreaSetStableId
                || (ContentSourceCode != 공간LHWorldCodes.ScenarioProcedural
                    && ContentSourceCode != 공간LHWorldCodes.AuthoritativeWorld)
                || Season == null || Cells == null || OutsideCoverageCellKeys == null
                || !IsCandidateOnly || !DoesNotApplyResourceLedgers || IsOperationalState)
                throw new InvalidOperationException("LHWorldCellPreviewInvalid");
            Season.Validate();
            foreach (var cell in Cells)
            {
                cell.Validate();
                if (cell.ContentSourceCode != ContentSourceCode)
                    throw new InvalidOperationException("LHWorldContentSourceMismatch");
            }
        }
    }

    [Serializable]
    public sealed class 공간LHSeasonData
    {
        public string SeasonCode = string.Empty;
        public int SeasonIndex;
        public int SeasonDay;
        public double SeasonProgress01;
        public string NextSeasonCode = string.Empty;
        public string SeasonRuleVersion = string.Empty;
        public int DayNumber;

        public void Validate()
        {
            if (SeasonIndex < 0 || SeasonIndex > 3 || SeasonDay < 1 || SeasonDay > 28
                || SeasonProgress01 < 0d || SeasonProgress01 > 1d || DayNumber < 1
                || string.IsNullOrWhiteSpace(SeasonRuleVersion))
                throw new InvalidOperationException("LHWorldSeasonInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LHCellPlanData
    {
        public string CellKey = string.Empty;
        public int CellX;
        public int CellY;
        public string L2ParentCellKey = string.Empty;
        public string WindowRoleCode = string.Empty;
        public int Priority;
        public string ContentSourceCode = 공간LHWorldCodes.ScenarioProcedural;
        public string BasePlanHashSha256 = string.Empty;
        public string PresentationHashSha256 = string.Empty;
        public 공간LHHBindingData[] HBindings = Array.Empty<공간LHHBindingData>();
        public 공간LHPlacementData[] Placements = Array.Empty<공간LHPlacementData>();
        public 공간LHConnectorData[] Connectors = Array.Empty<공간LHConnectorData>();
        public string[] RequiredCapabilityCodes = Array.Empty<string>();
        public bool PlayerTraversalRequired;
        public bool PresentationOnly;

        public void Validate()
        {
            if (CellKey != 공간LHCellKey.L3(CellX, CellY)
                || !공간LHCellKey.TryParseL3(CellKey, out _, out _)
                || (ContentSourceCode != 공간LHWorldCodes.ScenarioProcedural
                    && ContentSourceCode != 공간LHWorldCodes.AuthoritativeWorld)
                || BasePlanHashSha256 == null || BasePlanHashSha256.Length != 64
                || PresentationHashSha256 == null || PresentationHashSha256.Length != 64
                || HBindings == null || Placements == null || Connectors == null
                || RequiredCapabilityCodes == null || !PresentationOnly)
                throw new InvalidOperationException("LHWorldCellPlanInvalid:" + CellKey);
            foreach (var placement in Placements)
                if (placement.OwnerCellKey != CellKey || !placement.PresentationOnly)
                    throw new InvalidOperationException("LHWorldPlacementOwnershipInvalid");
        }
    }

    [Serializable]
    public sealed class 공간LHHBindingData
    {
        public string HLevelCode = string.Empty;
        public string SpatialStableId = string.Empty;
        public string StateCode = string.Empty;
        public string[] WorldInteractionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class 공간LHPlacementData
    {
        public string GeneratedStableId = string.Empty;
        public string OwnerCellKey = string.Empty;
        public string LayerCode = string.Empty;
        public string CompositionKey = string.Empty;
        public string H1StableId = string.Empty;
        public string EvidenceKindCode = string.Empty;
        public double LocalXMeters;
        public double LocalZMeters;
        public double RotationDegrees;
        public double UniformScale = 1d;
        public bool FixedAnchor;
        public bool CollisionEligible;
        public bool PresentationOnly;
    }

    [Serializable]
    public sealed class 공간LHConnectorData
    {
        public string ConnectorStableId = string.Empty;
        public string SideCode = string.Empty;
        public string NeighborCellKey = string.Empty;
        public string BoundaryHashSha256 = string.Empty;
        public bool Passable;
    }

    public interface I공간LHWorldRepository
    {
        string SourceModeCode { get; }

        Task<공간LHCellPreviewData> PreviewCellsAsync(
            공간LHCellPreviewRequestData request,
            CancellationToken cancellationToken);
    }

    public static class 공간LHCellKey
    {
        public static string L3(int x, int y) => $"kr5186:l3:{x}:{y}";

        public static bool TryParseL3(string value, out int x, out int y)
        {
            x = 0;
            y = 0;
            var parts = (value ?? string.Empty).Split(':');
            return parts.Length == 4 && parts[0] == "kr5186" && parts[1] == "l3"
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        public static string FromWorldPosition(
            double worldX, double worldZ, double originWorldX, double originWorldZ)
        {
            var x = 공간LHWorldCodes.CenterL3X
                    + (int)Math.Floor((worldX - originWorldX
                                      + 공간LHWorldCodes.L3CellSizeMeters * .5d)
                                     / 공간LHWorldCodes.L3CellSizeMeters);
            var y = 공간LHWorldCodes.CenterL3Y
                    + (int)Math.Floor((worldZ - originWorldZ
                                      + 공간LHWorldCodes.L3CellSizeMeters * .5d)
                                     / 공간LHWorldCodes.L3CellSizeMeters);
            return L3(x, y);
        }
    }

    /// <summary>
    /// 서버 접속 없이 플레이어 위치, 월드 시드와 로컬 달력만으로 주변 LH 셀을 계산한다.
    /// 결과는 운영 상태를 변경하지 않는 싱글 플레이용 결정적 공간 후보이다.
    /// </summary>
    public class 로컬공간LHWorldEngine : I공간LHWorldRepository
    {
        private static readonly string[] CompositionKeys =
        {
            "farm:감자밭 두렁:A", "farm:감자밭 두렁:B", "farm:혼합 작물밭:A",
            "nature:초지·야생화:A", "nature:숲 가장자리:A", "nature:침엽수림 군집:A",
        };

        private static readonly string[] SeasonCodes =
        {
            공간LHWorldCodes.Spring, 공간LHWorldCodes.Summer,
            공간LHWorldCodes.Autumn, 공간LHWorldCodes.Winter,
        };

        private readonly string worldSeed;
        private int dayNumber;
        private int worldTick;
        private long worldRevision;

        public 로컬공간LHWorldEngine(
            string localWorldSeed = 공간LHWorldCodes.WorldSeed,
            int localDayNumber = 1,
            int localWorldTick = 0,
            long localWorldRevision = 0)
        {
            if (string.IsNullOrWhiteSpace(localWorldSeed))
                throw new ArgumentException("LHLocalWorldSeedMissing", nameof(localWorldSeed));
            worldSeed = localWorldSeed.Trim();
            SetLocalCalendar(localDayNumber, localWorldTick, localWorldRevision);
        }

        public virtual string SourceModeCode => 공간LHWorldCodes.LocalEngine;
        public string WorldSeed => worldSeed;
        public int DayNumber => dayNumber;

        public void SetLocalCalendar(int localDayNumber, int localWorldTick, long localWorldRevision)
        {
            if (localDayNumber < 1 || localWorldTick < 0 || localWorldRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(localDayNumber),
                    "LHLocalCalendarInvalid");
            dayNumber = localDayNumber;
            worldTick = localWorldTick;
            worldRevision = localWorldRevision;
        }

        public Task<공간LHCellPreviewData> PreviewCellsAsync(
            공간LHCellPreviewRequestData request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateRequest(request);
            if (!공간LHCellKey.TryParseL3(request.FocusL3CellKey, out var focusX, out var focusY))
                throw new InvalidOperationException("LHLocalWorldFocusInvalid");
            if (!IsInsideCoverage(focusX, focusY))
                throw new ArgumentOutOfRangeException(nameof(request),
                    "LHLocalWorldFocusOutsideH4");
            var profile = Profile();
            var cells = new List<공간LHCellPlanData>();
            var outside = new List<string>();
            var movement = DirectionVector(request.MovementDirectionCode);
            for (var y = focusY - profile.PrefetchRadius; y <= focusY + profile.PrefetchRadius; y++)
            for (var x = focusX - profile.PrefetchRadius; x <= focusX + profile.PrefetchRadius; x++)
            {
                var cellKey = 공간LHCellKey.L3(x, y);
                if (!IsInsideCoverage(x, y))
                {
                    outside.Add(cellKey);
                    continue;
                }
                var dx = x - focusX;
                var dy = y - focusY;
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var role = distance <= 1 ? 공간LHWorldCodes.Detail
                    : distance <= 2 ? 공간LHWorldCodes.Active : 공간LHWorldCodes.Prefetch;
                var directionalBias = dx * movement.X + dy * movement.Y > 0 ? -10 : 0;
                cells.Add(Cell(x, y, role,
                    distance * 100 + directionalBias + Math.Abs(dx) + Math.Abs(dy),
                    request.RequiredCapabilityCodes ?? Array.Empty<string>()));
            }
            var response = new 공간LHCellPreviewData
            {
                RequestEpoch = request.RequestEpoch,
                RecipeStableId = 공간LHWorldCodes.RecipeStableId,
                AreaSetStableId = 공간LHWorldCodes.AreaSetStableId,
                WorldTick = worldTick,
                WorldRevision = worldRevision,
                Season = Season(dayNumber),
                Profile = profile,
                Cells = cells.OrderBy(value => value.Priority)
                    .ThenBy(value => value.CellKey, StringComparer.Ordinal).ToArray(),
                OutsideCoverageCellKeys = outside.OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                IsCandidateOnly = true,
                DoesNotApplyResourceLedgers = true,
                IsOperationalState = false,
            };
            response.Validate(request.RequestEpoch);
            return Task.FromResult(response);
        }

        private 공간LHWorldProfileData Profile()
        {
            var value = new 공간LHWorldProfileData
            {
                SchemaVersion = 공간LHWorldCodes.SchemaVersion,
                ProfileRevision = "lh-world.pyeongchang-farm.r1",
                WorldSeed = worldSeed,
                GeneratorVersion = 공간LHWorldCodes.GeneratorVersion,
                AreaSetStableId = 공간LHWorldCodes.AreaSetStableId,
                AreaSetRevision = "area-set.pyeongchang-farm-hub-town.r1",
                AreaSetBoundaryHashSha256 = Hash(string.Join("|", new[]
                {
                    공간LHWorldCodes.AreaSetStableId,
                    공간LHWorldCodes.MinimumL3X.ToString(CultureInfo.InvariantCulture),
                    공간LHWorldCodes.MaximumL3X.ToString(CultureInfo.InvariantCulture),
                    공간LHWorldCodes.MinimumL3Y.ToString(CultureInfo.InvariantCulture),
                    공간LHWorldCodes.MaximumL3Y.ToString(CultureInfo.InvariantCulture),
                })),
                Levels = new[]
                {
                    Level("L0", 8000, "H4"), Level("L1", 2000, "H3"),
                    Level("L2", 500, "H2"), Level("L3", 125, "H1"),
                },
                DetailRadius = 1, ActiveRadius = 2, PrefetchRadius = 4,
                MaxConcurrentPreparations = 4, BoundaryPrefetchFraction = .25d,
                MainThreadAssemblyBudgetMilliseconds = 2d, CachedCellCapacity = 32,
                OriginShiftThresholdWorldUnits = 2048,
                GenerationLayers = new[]
                {
                    Layer("H4Boundary", 0, "AuthoritativeEnvelope"),
                    Layer("H3Intent", 500, "ApprovedAnchor", "H4Boundary"),
                    Layer("H2BlockPlan", 250, "OwnedWithinBounds", "H3Intent"),
                    Layer("H1Workspace", 125, "OwnedWithinBounds", "H2BlockPlan"),
                    Layer("L3Surface", 60, "OverlappingBounds", "H1Workspace"),
                    Layer("SeasonOverlay", 0, "PresentationOnly", "L3Surface"),
                },
                PresentationOnly = true,
            };
            value.ProfileHashSha256 = Hash("local-profile|" + value.WorldSeed + "|"
                + value.AreaSetBoundaryHashSha256 + "|" + value.GeneratorVersion);
            value.Validate();
            return value;
        }

        private 공간LHCellPlanData Cell(
            int x, int y, string role, int priority, IReadOnlyCollection<string> requestedCapabilities)
        {
            var cellKey = 공간LHCellKey.L3(x, y);
            var hBindings = HBindings(x, y);
            var placements = Placements(cellKey, x, y);
            var connectors = Connectors(x, y);
            var capabilities = requestedCapabilities.Count > 0
                ? requestedCapabilities.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : role == 공간LHWorldCodes.Prefetch
                ? new[] { 공간LHWorldCodes.TerrainVisual }
                : role == 공간LHWorldCodes.Active
                    ? new[] { 공간LHWorldCodes.TerrainVisual, 공간LHWorldCodes.Collision,
                        공간LHWorldCodes.Connector }
                    : new[] { 공간LHWorldCodes.TerrainVisual, 공간LHWorldCodes.Collision,
                        공간LHWorldCodes.Connector, 공간LHWorldCodes.H1Interaction,
                        공간LHWorldCodes.NpcNavigation, 공간LHWorldCodes.SeasonPresentation };
            var baseHash = Hash(string.Join("|", new[]
            {
                worldSeed, 공간LHWorldCodes.GeneratorVersion, cellKey,
                string.Join(",", hBindings.Select(value =>
                    value.HLevelCode + ":" + value.SpatialStableId + ":" + value.StateCode)),
                string.Join(",", placements.Select(PlacementCanonical)),
                string.Join(",", connectors.Select(value =>
                    value.ConnectorStableId + ":" + value.BoundaryHashSha256)),
            }));
            var season = Season(dayNumber);
            return new 공간LHCellPlanData
            {
                CellKey = cellKey, CellX = x, CellY = y,
                L2ParentCellKey = $"kr5186:l2:{FloorDiv(x, 4)}:{FloorDiv(y, 4)}",
                WindowRoleCode = role, Priority = priority,
                BasePlanHashSha256 = baseHash,
                PresentationHashSha256 = Hash(baseHash + "|" + season.SeasonCode + "|"
                    + season.SeasonRuleVersion),
                HBindings = hBindings,
                Placements = placements,
                Connectors = connectors,
                RequiredCapabilityCodes = capabilities,
                PlayerTraversalRequired = role != 공간LHWorldCodes.Prefetch,
                PresentationOnly = true,
            };
        }

        private static 공간LHHBindingData[] HBindings(int x, int y)
        {
            var values = new List<공간LHHBindingData>
            {
                Binding("H4", 공간LHWorldCodes.AreaSetStableId, "ApprovedReference"),
                Binding("H3", "landscape-graph:sim:pyeongchang:daegwallyeong-farm.v1",
                    "ApprovedReference"),
                Binding("H2", "landscape-block-candidate:sim:pyeongchang:daegwallyeong-harvest-day.v1",
                    "IdeaInventory"),
            };
            if (Math.Abs(x - 공간LHWorldCodes.CenterL3X) <= 1
                && Math.Abs(y - 공간LHWorldCodes.CenterL3Y) <= 1)
            {
                values.Add(Binding("H1", "h1-stock:farm-production", "ApprovedReference",
                    "WI-FARM-04"));
                values.Add(Binding("H1", "h1-stock:farm-work-yard", "ApprovedReference",
                    "WI-FARM-05", "WI-FARM-06"));
                values.Add(Binding("H1", "h1-stock:farm-loading-gate", "ApprovedReference",
                    "WI-LOG-01"));
            }
            return values.ToArray();
        }

        private 공간LHPlacementData[] Placements(string cellKey, int x, int y)
        {
            var values = new List<공간LHPlacementData>();
            AddFixedAnchor(values, cellKey, x, y, 공간LHWorldCodes.CenterL3X,
                공간LHWorldCodes.CenterL3Y, "farmhouse", "farm:헛간 작업마당:A",
                "h1-stock:farm-work-yard", -18d, 12d, 0d);
            AddFixedAnchor(values, cellKey, x, y, 공간LHWorldCodes.CenterL3X + 1,
                공간LHWorldCodes.CenterL3Y, "potato-field", "farm:감자밭 두렁:A",
                "h1-stock:farm-production", 4d, -6d, 0d);
            AddFixedAnchor(values, cellKey, x, y, 공간LHWorldCodes.CenterL3X,
                공간LHWorldCodes.CenterL3Y + 1, "work-yard", "farm:농산물 집하·직판장:A",
                "h1-stock:farm-work-yard", 10d, 8d, 90d);
            AddFixedAnchor(values, cellKey, x, y, 공간LHWorldCodes.CenterL3X + 1,
                공간LHWorldCodes.CenterL3Y + 1, "farm-gate", "network:농촌도로 T자:A",
                "h1-stock:farm-loading-gate", -8d, -10d, 0d);

            var randomBytes = HashBytes(string.Join("|", new[]
            {
                worldSeed, 공간LHWorldCodes.GeneratorVersion, "L3Surface", cellKey,
            }));
            var count = 3 + randomBytes[0] % 3;
            for (var index = 0; index < count; index++)
            {
                var offset = 1 + index * 5;
                var key = CompositionKeys[randomBytes[offset] % CompositionKeys.Length];
                var identity = Hash(cellKey + "|L3Surface|" + index + "|" + key);
                values.Add(new 공간LHPlacementData
                {
                    GeneratedStableId = "lh-object:" + identity.Substring(0, 24),
                    OwnerCellKey = cellKey,
                    LayerCode = "L3Surface",
                    CompositionKey = key,
                    H1StableId = key.StartsWith("farm:", StringComparison.Ordinal)
                        ? "h1-stock:farm-production" : string.Empty,
                    EvidenceKindCode = "ScenarioProcedural",
                    LocalXMeters = -50d + randomBytes[offset + 1] / 255d * 100d,
                    LocalZMeters = -50d + randomBytes[offset + 2] / 255d * 100d,
                    RotationDegrees = randomBytes[offset + 3] / 255d * 360d,
                    UniformScale = .85d + randomBytes[offset + 4] / 255d * .3d,
                    PresentationOnly = true,
                });
            }
            return values.OrderBy(value => value.GeneratedStableId, StringComparer.Ordinal).ToArray();
        }

        private static void AddFixedAnchor(
            ICollection<공간LHPlacementData> values, string cellKey, int x, int y,
            int expectedX, int expectedY, string slot, string compositionKey,
            string h1StableId, double localX, double localZ, double rotation)
        {
            if (x != expectedX || y != expectedY) return;
            var identity = Hash(cellKey + "|H3Intent|" + slot);
            values.Add(new 공간LHPlacementData
            {
                GeneratedStableId = "lh-anchor:" + identity.Substring(0, 24),
                OwnerCellKey = cellKey,
                LayerCode = "H3Intent",
                CompositionKey = compositionKey,
                H1StableId = h1StableId,
                EvidenceKindCode = "ScenarioProcedural",
                LocalXMeters = localX,
                LocalZMeters = localZ,
                RotationDegrees = rotation,
                UniformScale = 1d,
                FixedAnchor = true,
                PresentationOnly = true,
            });
        }

        private static 공간LHConnectorData[] Connectors(int x, int y) => new[]
        {
            Connector(x, y, x, y + 1, "N"), Connector(x, y, x + 1, y, "E"),
            Connector(x, y, x, y - 1, "S"), Connector(x, y, x - 1, y, "W"),
        };

        private static 공간LHConnectorData Connector(
            int x, int y, int neighborX, int neighborY, string side)
        {
            var neighbor = 공간LHCellKey.L3(neighborX, neighborY);
            var pair = new[] { 공간LHCellKey.L3(x, y), neighbor }
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var boundaryHash = Hash(pair[0] + "|" + pair[1] + "|connector.v1");
            return new 공간LHConnectorData
            {
                ConnectorStableId = "lh-connector:" + boundaryHash.Substring(0, 24),
                SideCode = side,
                NeighborCellKey = neighbor,
                BoundaryHashSha256 = boundaryHash,
                Passable = IsInsideCoverage(neighborX, neighborY),
            };
        }

        private static 공간LHSeasonData Season(int day)
        {
            var zero = day - 1;
            var index = zero / 28 % SeasonCodes.Length;
            return new 공간LHSeasonData
            {
                SeasonCode = SeasonCodes[index], SeasonIndex = index, SeasonDay = zero % 28 + 1,
                SeasonProgress01 = (zero % 28) / 27d,
                NextSeasonCode = SeasonCodes[(index + 1) % 4],
                SeasonRuleVersion = "simulation-season.28-day.r1", DayNumber = day,
            };
        }

        private static 공간LHLevelData Level(string level, int size, string h)
            => new()
            {
                LevelCode = level,
                CellSizeMeters = size,
                DefaultHLevelCode = h,
                PrimaryHQueryLevelCode = h,
            };

        private static 공간LHGenerationLayerData Layer(
            string code, int paddingMeters, string ownership, params string[] dependencies)
            => new() { LayerCode = code, DependsOnLayerCodes = dependencies,
                MaximumPaddingMeters = paddingMeters, OwnershipRuleCode = ownership };

        private void ValidateRequest(공간LHCellPreviewRequestData request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestEpoch)
                || request.RecipeStableId != 공간LHWorldCodes.RecipeStableId
                || request.AreaSetStableId != 공간LHWorldCodes.AreaSetStableId
                || !IsDirectionCode(request.MovementDirectionCode)
                || (request.RequiredCapabilityCodes ?? Array.Empty<string>())
                    .Any(value => !IsCapabilityCode(value))
                || (request.KnownCellPlanHashesSha256 ?? Array.Empty<string>())
                    .Any(value => value == null || value.Length != 64
                                  || value.Any(character => !Uri.IsHexDigit(character)))
                || request.ExpectedWorldRevision != worldRevision)
                throw new ArgumentException("LHLocalWorldRequestInvalid", nameof(request));
        }

        private static bool IsDirectionCode(string value)
            => value == 공간LHWorldCodes.None || value == "N" || value == "NE"
               || value == "E" || value == "SE" || value == "S" || value == "SW"
               || value == "W" || value == "NW";

        private static bool IsCapabilityCode(string value)
            => value == 공간LHWorldCodes.TerrainVisual || value == 공간LHWorldCodes.Collision
               || value == 공간LHWorldCodes.Connector || value == 공간LHWorldCodes.H1Interaction
               || value == 공간LHWorldCodes.NpcNavigation
               || value == 공간LHWorldCodes.SeasonPresentation;

        private static bool IsInsideCoverage(int x, int y)
            => x >= 공간LHWorldCodes.MinimumL3X && x <= 공간LHWorldCodes.MaximumL3X
               && y >= 공간LHWorldCodes.MinimumL3Y && y <= 공간LHWorldCodes.MaximumL3Y;

        private static (int X, int Y) DirectionVector(string directionCode)
            => directionCode switch
            {
                "N" => (0, 1), "NE" => (1, 1), "E" => (1, 0), "SE" => (1, -1),
                "S" => (0, -1), "SW" => (-1, -1), "W" => (-1, 0), "NW" => (-1, 1),
                _ => (0, 0),
            };

        private static 공간LHHBindingData Binding(
            string level, string stableId, string state, params string[] interactions)
            => new() { HLevelCode = level, SpatialStableId = stableId, StateCode = state,
                WorldInteractionIds = interactions };

        private static string PlacementCanonical(공간LHPlacementData value)
            => string.Join(":", new[]
            {
                value.GeneratedStableId, value.CompositionKey, value.H1StableId,
                value.LocalXMeters.ToString("R", CultureInfo.InvariantCulture),
                value.LocalZMeters.ToString("R", CultureInfo.InvariantCulture),
                value.RotationDegrees.ToString("R", CultureInfo.InvariantCulture),
                value.UniformScale.ToString("R", CultureInfo.InvariantCulture),
                value.FixedAnchor.ToString(),
            });

        private static int FloorDiv(int value, int divisor)
        {
            var quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        private static byte[] HashBytes(string value)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static string Hash(string value)
            => BitConverter.ToString(HashBytes(value)).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>기존 시험 코드 호환용 이름이다. 새 씬 구성에는 로컬공간LHWorldEngine을 사용한다.</summary>
    public sealed class 대관령Farm공간LHWorldFixtureRepository : 로컬공간LHWorldEngine
    {
        public 대관령Farm공간LHWorldFixtureRepository(int fixtureDayNumber = 1)
            : base(공간LHWorldCodes.WorldSeed, fixtureDayNumber) { }

        public override string SourceModeCode => 공간TileStreamingCodes.Fixture;
    }
}
