using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;

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
        public const string FarmAreaSetStableId =
            "area-set:sim:pyeongchang:farm-production.v1";
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
    public sealed class 공간동적MapWindowPolicyData
    {
        public int DetailRadius = 1;
        public int ActiveRadius = 2;
        public int PrefetchRadius = 4;

        public void Validate()
        {
            if (DetailRadius < 0
                || ActiveRadius < DetailRadius
                || PrefetchRadius < ActiveRadius)
                throw new InvalidOperationException("DynamicMapWindowPolicyInvalid");
        }

        public 공간동적MapWindowPolicyData Clone()
            => new()
            {
                DetailRadius = DetailRadius,
                ActiveRadius = ActiveRadius,
                PrefetchRadius = PrefetchRadius,
            };
    }

    public sealed class 공간동적MapWindowCellData
    {
        public int CellX;
        public int CellY;
        public string WindowRoleCode = string.Empty;
        public int Priority;
    }

    /// <summary>
    /// L/H 의미 계층이나 셀 식별자 형식을 알지 않고 중심 좌표, 반경과 이동 방향만으로
    /// 동적 맵 후보 창을 계산한다. 현재 LH의 L3 셀은 이 일반 계산기의 호환 Adapter다.
    /// </summary>
    public static class 공간동적MapWindowPlanner
    {
        public static 공간동적MapWindowCellData[] Plan(
            int focusX,
            int focusY,
            공간동적MapWindowPolicyData policy,
            string movementDirectionCode)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            policy.Validate();

            var movement = DirectionVector(movementDirectionCode);
            var cells = new List<공간동적MapWindowCellData>();
            for (var y = focusY - policy.PrefetchRadius;
                 y <= focusY + policy.PrefetchRadius; y++)
            for (var x = focusX - policy.PrefetchRadius;
                 x <= focusX + policy.PrefetchRadius; x++)
            {
                var dx = x - focusX;
                var dy = y - focusY;
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var role = distance <= policy.DetailRadius
                    ? 공간LHWorldCodes.Detail
                    : distance <= policy.ActiveRadius
                        ? 공간LHWorldCodes.Active
                        : 공간LHWorldCodes.Prefetch;
                var directionalBias = dx * movement.X + dy * movement.Y > 0 ? -10 : 0;
                cells.Add(new 공간동적MapWindowCellData
                {
                    CellX = x,
                    CellY = y,
                    WindowRoleCode = role,
                    Priority = distance * 100 + directionalBias + Math.Abs(dx) + Math.Abs(dy),
                });
            }

            return cells.OrderBy(value => value.Priority)
                .ThenBy(value => value.CellY)
                .ThenBy(value => value.CellX)
                .ToArray();
        }

        private static (int X, int Y) DirectionVector(string directionCode)
            => directionCode switch
            {
                "N" => (0, 1), "NE" => (1, 1), "E" => (1, 0), "SE" => (1, -1),
                "S" => (0, -1), "SW" => (-1, -1), "W" => (-1, 0), "NW" => (-1, 1),
                _ => (0, 0),
            };
    }

    [Serializable]
    public sealed class 공간동적배치PolicyData
    {
        public double 셀크기Meters = 공간LHWorldCodes.L3CellSizeMeters;
        public double 경계여유Meters = 6d;
        public double 최소객체간격Meters = 12d;
        public double 최대표면경사Degrees = 18d;
        public int 객체당최대시도횟수 = 12;

        public void Validate()
        {
            if (!IsFinite(셀크기Meters) || 셀크기Meters <= 0d
                || !IsFinite(경계여유Meters) || 경계여유Meters < 0d
                || 경계여유Meters * 2d >= 셀크기Meters
                || !IsFinite(최소객체간격Meters) || 최소객체간격Meters <= 0d
                || 경계여유Meters * 2d < 최소객체간격Meters
                || !IsFinite(최대표면경사Degrees)
                || 최대표면경사Degrees < 0d || 최대표면경사Degrees > 90d
                || 객체당최대시도횟수 < 1)
                throw new InvalidOperationException("DynamicPlacementPolicyInvalid");
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class 공간동적표면표본Data
    {
        public double 표면높이Meters;
        public double 표면경사Degrees;
        public bool 배치허용;
        public string 차단원인Code = string.Empty;
    }

    public interface I공간동적표면Provider
    {
        string SurfaceModeCode { get; }
        string SurfaceRuleRevision { get; }
        string SurfaceConfigurationHashSha256 { get; }

        bool TrySample(double worldX, double worldZ, out 공간동적표면표본Data sample);
    }

    public sealed class 평면공간동적표면Provider : I공간동적표면Provider
    {
        private readonly double 표면높이Meters;

        public 평면공간동적표면Provider(double surfaceHeightMeters = 0d)
        {
            if (double.IsNaN(surfaceHeightMeters) || double.IsInfinity(surfaceHeightMeters))
                throw new ArgumentOutOfRangeException(nameof(surfaceHeightMeters));
            표면높이Meters = surfaceHeightMeters;
        }

        public string SurfaceModeCode => "Flat";
        public string SurfaceRuleRevision => "dynamic-surface.flat.r1";
        public string SurfaceConfigurationHashSha256 => Hash(
            SurfaceRuleRevision + "|" + 표면높이Meters.ToString("R",
                CultureInfo.InvariantCulture));

        public bool TrySample(double worldX, double worldZ, out 공간동적표면표본Data sample)
        {
            sample = new 공간동적표면표본Data
            {
                표면높이Meters = 표면높이Meters,
                표면경사Degrees = 0d,
                배치허용 = true,
            };
            return true;
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    /// <summary>
    /// UnityEngine의 Mathf나 현재 셀 원점에 의존하지 않는 결정적 fractal value noise다.
    /// 월드 좌표를 직접 표본화하므로 인접 셀의 같은 경계 좌표는 같은 높이를 반환한다.
    /// </summary>
    public sealed class 결정적ValueNoise공간표면Provider : I공간동적표면Provider
    {
        private readonly string seed;
        private readonly double baseHeightMeters;
        private readonly double amplitudeMeters;
        private readonly double wavelengthMeters;
        private readonly int octaveCount;
        private readonly double persistence;
        private readonly double slopeSampleStepMeters;

        public 결정적ValueNoise공간표면Provider(
            string worldSeed,
            double baseHeight = 0d,
            double amplitude = 5d,
            double wavelength = 96d,
            int octaves = 3,
            double octavePersistence = .5d,
            double slopeSampleStep = 1d)
        {
            if (string.IsNullOrWhiteSpace(worldSeed)
                || !IsFinite(baseHeight) || !IsFinite(amplitude) || amplitude < 0d
                || !IsFinite(wavelength) || wavelength <= 0d
                || octaves < 1 || octaves > 8
                || !IsFinite(octavePersistence) || octavePersistence <= 0d
                || octavePersistence > 1d
                || !IsFinite(slopeSampleStep) || slopeSampleStep <= 0d)
                throw new ArgumentException("DynamicNoiseSurfaceConfigurationInvalid");
            seed = worldSeed.Trim();
            baseHeightMeters = baseHeight;
            amplitudeMeters = amplitude;
            wavelengthMeters = wavelength;
            octaveCount = octaves;
            persistence = octavePersistence;
            slopeSampleStepMeters = slopeSampleStep;
        }

        public string SurfaceModeCode => "DeterministicValueNoise";
        public string SurfaceRuleRevision => "dynamic-surface.value-noise.r1";
        public string SurfaceConfigurationHashSha256 => Hash(string.Join("|", new[]
        {
            SurfaceRuleRevision, seed,
            baseHeightMeters.ToString("R", CultureInfo.InvariantCulture),
            amplitudeMeters.ToString("R", CultureInfo.InvariantCulture),
            wavelengthMeters.ToString("R", CultureInfo.InvariantCulture),
            octaveCount.ToString(CultureInfo.InvariantCulture),
            persistence.ToString("R", CultureInfo.InvariantCulture),
            slopeSampleStepMeters.ToString("R", CultureInfo.InvariantCulture),
        }));

        public bool TrySample(double worldX, double worldZ,
            out 공간동적표면표본Data sample)
        {
            if (!IsFinite(worldX) || !IsFinite(worldZ))
            {
                sample = new 공간동적표면표본Data
                {
                    배치허용 = false,
                    차단원인Code = "GroundSurfaceCoordinateInvalid",
                };
                return false;
            }
            var height = Height(worldX, worldZ);
            var dx = (Height(worldX + slopeSampleStepMeters, worldZ)
                      - Height(worldX - slopeSampleStepMeters, worldZ))
                     / (2d * slopeSampleStepMeters);
            var dz = (Height(worldX, worldZ + slopeSampleStepMeters)
                      - Height(worldX, worldZ - slopeSampleStepMeters))
                     / (2d * slopeSampleStepMeters);
            sample = new 공간동적표면표본Data
            {
                표면높이Meters = height,
                표면경사Degrees = Math.Atan(Math.Sqrt(dx * dx + dz * dz))
                                     * 180d / Math.PI,
                배치허용 = true,
            };
            return true;
        }

        private double Height(double worldX, double worldZ)
        {
            var value = baseHeightMeters;
            var amplitude = amplitudeMeters;
            var frequency = 1d / wavelengthMeters;
            for (var octave = 0; octave < octaveCount; octave++)
            {
                value += (ValueNoise(worldX * frequency, worldZ * frequency, octave)
                          * 2d - 1d) * amplitude;
                amplitude *= persistence;
                frequency *= 2d;
            }
            return value;
        }

        private double ValueNoise(double x, double z, int octave)
        {
            var x0 = (long)Math.Floor(x);
            var z0 = (long)Math.Floor(z);
            var tx = Smooth(x - x0);
            var tz = Smooth(z - z0);
            var bottom = Lerp(Lattice(x0, z0, octave),
                Lattice(x0 + 1, z0, octave), tx);
            var top = Lerp(Lattice(x0, z0 + 1, octave),
                Lattice(x0 + 1, z0 + 1, octave), tx);
            return Lerp(bottom, top, tz);
        }

        private double Lattice(long x, long z, int octave)
        {
            var hash = HashBytes(seed + "|" + octave.ToString(CultureInfo.InvariantCulture)
                                 + "|" + x.ToString(CultureInfo.InvariantCulture)
                                 + "|" + z.ToString(CultureInfo.InvariantCulture));
            var integer = (uint)(hash[0] | hash[1] << 8 | hash[2] << 16
                                 | hash[3] << 24);
            return integer / (double)uint.MaxValue;
        }

        private static double Smooth(double value)
            => value * value * (3d - 2d * value);

        private static double Lerp(double from, double to, double t)
            => from + (to - from) * t;

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        private static byte[] HashBytes(string value)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static string Hash(string value)
            => BitConverter.ToString(HashBytes(value)).Replace("-", string.Empty)
                .ToLowerInvariant();
    }

    public sealed class 공간동적배치점Data
    {
        public string 배치StableId = string.Empty;
        public double LocalXMeters;
        public double LocalYMeters;
        public double LocalZMeters;
        public double RotationDegrees;
        public double UniformScale = 1d;
        public double SurfaceSlopeDegrees;
        public string ValidationCode = string.Empty;
        public int VariantIndex;
    }

    public sealed class 공간동적배치PlanData
    {
        public int 요청수;
        public 공간동적배치점Data[] 배치점 = Array.Empty<공간동적배치점Data>();
        public string[] 거부원인Codes = Array.Empty<string>();

        public bool IsComplete => 배치점.Length == 요청수;
    }

    /// <summary>
    /// 셀 키 형식과 H 의미를 알지 않고 월드 seed, 셀 좌표, 표면 표본과 간격 정책만으로
    /// 동적 배치점을 계산한다. 경계 여유가 최소 간격의 절반 이상이므로 인접 셀이
    /// 서로 다른 순서로 생성돼도 경계 객체가 겹치지 않는다.
    /// </summary>
    public static class 공간동적ObjectPlacementPlanner
    {
        public const string RuleRevision = "dynamic-placement.surface-aware.r1";

        public static 공간동적배치PlanData Plan(
            string worldSeed,
            int cellX,
            int cellY,
            int requestedCount,
            공간동적배치PolicyData policy,
            I공간동적표면Provider surfaceProvider,
            IReadOnlyCollection<공간동적배치점Data>? occupiedPoints = null)
        {
            if (string.IsNullOrWhiteSpace(worldSeed))
                throw new ArgumentException("DynamicPlacementWorldSeedMissing", nameof(worldSeed));
            if (requestedCount < 0)
                throw new ArgumentOutOfRangeException(nameof(requestedCount));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (surfaceProvider == null) throw new ArgumentNullException(nameof(surfaceProvider));
            policy.Validate();

            var accepted = new List<공간동적배치점Data>();
            var occupied = (occupiedPoints ?? Array.Empty<공간동적배치점Data>()).ToArray();
            var rejectionCodes = new HashSet<string>(StringComparer.Ordinal);
            var half = policy.셀크기Meters * .5d;
            var minimum = -half + policy.경계여유Meters;
            var maximum = half - policy.경계여유Meters;
            var span = maximum - minimum;
            var minimumDistanceSquared = policy.최소객체간격Meters
                                         * policy.최소객체간격Meters;

            for (var index = 0; index < requestedCount; index++)
            {
                var placed = false;
                for (var attempt = 0; attempt < policy.객체당최대시도횟수; attempt++)
                {
                    var canonical = string.Join("|", new[]
                    {
                        worldSeed.Trim(), RuleRevision,
                        cellX.ToString(CultureInfo.InvariantCulture),
                        cellY.ToString(CultureInfo.InvariantCulture),
                        index.ToString(CultureInfo.InvariantCulture),
                        attempt.ToString(CultureInfo.InvariantCulture),
                    });
                    var bytes = HashBytes(canonical);
                    var localX = minimum + UInt16(bytes, 0) / 65535d * span;
                    var localZ = minimum + UInt16(bytes, 2) / 65535d * span;
                    var worldX = cellX * policy.셀크기Meters + localX;
                    var worldZ = cellY * policy.셀크기Meters + localZ;
                    if (!surfaceProvider.TrySample(worldX, worldZ, out var surface)
                        || surface == null)
                    {
                        rejectionCodes.Add("GroundSurfaceMissing");
                        continue;
                    }
                    if (!surface.배치허용)
                    {
                        rejectionCodes.Add(string.IsNullOrWhiteSpace(surface.차단원인Code)
                            ? "SurfacePlacementDenied" : surface.차단원인Code);
                        continue;
                    }
                    if (surface.표면경사Degrees > policy.최대표면경사Degrees)
                    {
                        rejectionCodes.Add("SlopeTooSteep");
                        continue;
                    }
                    if (occupied.Concat(accepted).Any(value =>
                            DistanceSquared(localX, localZ, value.LocalXMeters,
                                value.LocalZMeters) < minimumDistanceSquared))
                    {
                        rejectionCodes.Add("ObjectSpacingTooSmall");
                        continue;
                    }

                    accepted.Add(new 공간동적배치점Data
                    {
                        배치StableId = "dynamic-object:"
                            + BitConverter.ToString(bytes).Replace("-", string.Empty)
                                .ToLowerInvariant().Substring(0, 24),
                        LocalXMeters = localX,
                        LocalYMeters = surface.표면높이Meters,
                        LocalZMeters = localZ,
                        RotationDegrees = UInt16(bytes, 4) / 65535d * 360d,
                        UniformScale = .85d + UInt16(bytes, 6) / 65535d * .3d,
                        SurfaceSlopeDegrees = surface.표면경사Degrees,
                        ValidationCode = "Passed",
                        VariantIndex = bytes[8],
                    });
                    placed = true;
                    break;
                }
                if (!placed) rejectionCodes.Add("PlacementCandidateExhausted");
            }

            return new 공간동적배치PlanData
            {
                요청수 = requestedCount,
                배치점 = accepted.OrderBy(value => value.배치StableId,
                    StringComparer.Ordinal).ToArray(),
                거부원인Codes = rejectionCodes.OrderBy(value => value,
                    StringComparer.Ordinal).ToArray(),
            };
        }

        private static int UInt16(byte[] bytes, int offset)
            => bytes[offset] | bytes[offset + 1] << 8;

        private static byte[] HashBytes(string value)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static double DistanceSquared(double x1, double z1, double x2, double z2)
        {
            var dx = x1 - x2;
            var dz = z1 - z2;
            return dx * dx + dz * dz;
        }
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
                || DetailRadius < 0 || ActiveRadius < DetailRadius
                || PrefetchRadius < ActiveRadius
                || MaxConcurrentPreparations < 1
                || BoundaryPrefetchFraction <= 0d || BoundaryPrefetchFraction > .5d
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

    public static class 공간AreaSetHandoverCodes
    {
        public const string SchemaVersion = "area-set-handover-plan.v1";
        public const string PlannerRevision = "area-set-handover-planner.r1";
        public const string H5Known = "H5Known";
        public const string NotAvailable = "NotAvailable";
        public const string PreviewConfirmWorldTick = "PreviewConfirmWorldTick";
    }

    [Serializable]
    public sealed class 공간AreaSetHandoverPlanData
    {
        public string SchemaVersion = 공간AreaSetHandoverCodes.SchemaVersion;
        public string PlannerRevision = 공간AreaSetHandoverCodes.PlannerRevision;
        public string RequestEpoch = string.Empty;
        public string CurrentAreaSetStableId = string.Empty;
        public string FocusL3CellKey = string.Empty;
        public string MovementDirectionCode = 공간LHWorldCodes.None;
        public string WorldLayoutStableId = string.Empty;
        public int WorldLayoutRevision;
        public string WorldLayoutHashSha256 = string.Empty;
        public string WorldGroundingStateCode = string.Empty;
        public string AvailabilityCode = 공간AreaSetHandoverCodes.NotAvailable;
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public 공간AreaSetHandoverCandidateData[] Candidates =
            Array.Empty<공간AreaSetHandoverCandidateData>();
        public string PlanHashSha256 = string.Empty;
        public bool ChangesCurrentAreaSet;
        public bool RequiresExplicitTraversalConfirm = true;
        public bool IsCandidateOnly = true;
        public bool IsOperationalState;

        public void Validate(string expectedEpoch)
        {
            if (SchemaVersion != 공간AreaSetHandoverCodes.SchemaVersion
                || PlannerRevision != 공간AreaSetHandoverCodes.PlannerRevision
                || RequestEpoch != expectedEpoch
                || !공간LHCellKey.TryParseL3(FocusL3CellKey, out _, out _)
                || BlockingReasonCodes == null || Candidates == null
                || PlanHashSha256 == null || PlanHashSha256.Length != 64
                || ChangesCurrentAreaSet || !RequiresExplicitTraversalConfirm
                || !IsCandidateOnly || IsOperationalState)
                throw new InvalidOperationException("AreaSetHandoverPlanInvalid");
            if (AvailabilityCode == 공간AreaSetHandoverCodes.H5Known
                && (string.IsNullOrWhiteSpace(CurrentAreaSetStableId)
                    || string.IsNullOrWhiteSpace(WorldLayoutStableId)
                    || WorldLayoutRevision <= 0
                    || WorldLayoutHashSha256 == null
                    || WorldLayoutHashSha256.Length != 64))
                throw new InvalidOperationException("AreaSetHandoverH5LineageInvalid");
            if (AvailabilityCode != 공간AreaSetHandoverCodes.H5Known
                && AvailabilityCode != 공간AreaSetHandoverCodes.NotAvailable)
                throw new InvalidOperationException("AreaSetHandoverAvailabilityInvalid");
            foreach (var candidate in Candidates) candidate.Validate();
        }
    }

    [Serializable]
    public sealed class 공간AreaSetHandoverCandidateData
    {
        public string TargetAreaSetStableId = string.Empty;
        public string RelationStableId = string.Empty;
        public string CorridorInstanceStableId = string.Empty;
        public string CorridorLandscapeGraphStableId = string.Empty;
        public string OverlapPolicyCode = string.Empty;
        public string SpatialRealizationCode = string.Empty;
        public double DistanceToTransitionMeters;
        public double HeadingAlignment01;
        public int Priority;
        public string PreparationTargetCode = string.Empty;
        public string SemanticDepthCode = string.Empty;
        public string ArtifactAvailabilityCode = string.Empty;
        public string ResidencyStateCode = string.Empty;
        public string SimulationAccessStateCode = string.Empty;
        public string ActivationAuthorityCode = string.Empty;
        public string[] RequiredCapabilityCodes = Array.Empty<string>();
        public string[] BlockingReasonCodes = Array.Empty<string>();
        public bool RequiresActualE5Package;
        public bool RequiresE6Grounding;
        public bool CanRequestTraversal;
        public bool CanActivate;
        public string CandidateHashSha256 = string.Empty;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TargetAreaSetStableId)
                || string.IsNullOrWhiteSpace(RelationStableId)
                || string.IsNullOrWhiteSpace(CorridorInstanceStableId)
                || string.IsNullOrWhiteSpace(CorridorLandscapeGraphStableId)
                || string.IsNullOrWhiteSpace(OverlapPolicyCode)
                || string.IsNullOrWhiteSpace(SpatialRealizationCode)
                || DistanceToTransitionMeters < 0d
                || HeadingAlignment01 < 0d || HeadingAlignment01 > 1d
                || Priority < 0
                || (SemanticDepthCode != "H1" && SemanticDepthCode != "H2"
                    && SemanticDepthCode != "H3" && SemanticDepthCode != "H4")
                || RequiredCapabilityCodes == null || BlockingReasonCodes == null
                || ActivationAuthorityCode !=
                    공간AreaSetHandoverCodes.PreviewConfirmWorldTick
                || CanActivate
                || CandidateHashSha256 == null || CandidateHashSha256.Length != 64)
                throw new InvalidOperationException("AreaSetHandoverCandidateInvalid");
        }
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
        public 공간AreaSetHandoverPlanData AreaSetHandover = new();
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
            AreaSetHandover.Validate(expectedEpoch);
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
        public 공간LHSurfaceGridData Surface = new();
        public 공간LHHBindingData[] HBindings = Array.Empty<공간LHHBindingData>();
        public 공간LHPlacementData[] Placements = Array.Empty<공간LHPlacementData>();
        public 공간LHConnectorData[] Connectors = Array.Empty<공간LHConnectorData>();
        public SimulationWorldCellAssemblyResponse WorldAssetAssembly;
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
                || Surface == null || HBindings == null || Placements == null
                || Connectors == null
                || RequiredCapabilityCodes == null || !PresentationOnly)
                throw new InvalidOperationException("LHWorldCellPlanInvalid:" + CellKey);
            Surface.Validate();
            if (WorldAssetAssembly != null
                && (!WorldAssetAssembly.IsAvailable
                    || WorldAssetAssembly.CellStableId != CellKey
                    || WorldAssetAssembly.SourceWorldRevision < 0
                    || WorldAssetAssembly.SourceLhBasePlanHashSha256 !=
                    BasePlanHashSha256
                    || WorldAssetAssembly.AssemblyHashSha256 == null
                    || WorldAssetAssembly.AssemblyHashSha256.Length != 64))
                throw new InvalidOperationException(
                    "LHWorldAssetAssemblyInvalid:" + CellKey);
            foreach (var placement in Placements)
            {
                if (placement.OwnerCellKey != CellKey || !placement.PresentationOnly)
                    throw new InvalidOperationException("LHWorldPlacementOwnershipInvalid");
                if (!string.IsNullOrWhiteSpace(placement.PlacementRuleRevision)
                    && placement.PlacementValidationCode != "Passed")
                    throw new InvalidOperationException("LHWorldPlacementValidationInvalid");
            }
        }
    }

    [Serializable]
    public sealed class 공간LHSurfaceGridData
    {
        public string SurfaceModeCode = string.Empty;
        public string SurfaceRuleRevision = string.Empty;
        public string SurfaceConfigurationHashSha256 = string.Empty;
        public string SurfaceGridHashSha256 = string.Empty;
        public int SampleResolution;
        public double CellSizeMeters;
        public double[] HeightMeters = Array.Empty<double>();

        public bool HasGeneratedGrid => SampleResolution >= 2
                                        && HeightMeters != null
                                        && HeightMeters.Length
                                        == SampleResolution * SampleResolution;

        public void Validate()
        {
            // 기존 서버 응답은 표면 상태 사본이 없으므로 빈 값은 평면 호환으로 읽는다.
            if (string.IsNullOrWhiteSpace(SurfaceModeCode)) return;
            if (string.IsNullOrWhiteSpace(SurfaceRuleRevision)
                || SurfaceConfigurationHashSha256 == null
                || SurfaceConfigurationHashSha256.Length != 64
                || SurfaceGridHashSha256 == null || SurfaceGridHashSha256.Length != 64
                || SampleResolution < 2 || CellSizeMeters <= 0d
                || HeightMeters == null
                || HeightMeters.Length != SampleResolution * SampleResolution
                || HeightMeters.Any(value => double.IsNaN(value)
                                             || double.IsInfinity(value)))
                throw new InvalidOperationException("LHWorldSurfaceGridInvalid");
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
        public double LocalYMeters;
        public double LocalZMeters;
        public double RotationDegrees;
        public double UniformScale = 1d;
        public bool FixedAnchor;
        public bool CollisionEligible;
        public bool PresentationOnly;
        public double SurfaceSlopeDegrees;
        public string PlacementRuleRevision = string.Empty;
        public string SurfaceRuleRevision = string.Empty;
        public string PlacementValidationCode = string.Empty;
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

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
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
        private static readonly string[] SeasonCodes =
        {
            공간LHWorldCodes.Spring, 공간LHWorldCodes.Summer,
            공간LHWorldCodes.Autumn, 공간LHWorldCodes.Winter,
        };

        private readonly string worldSeed;
        private readonly 공간동적MapWindowPolicyData windowPolicy;
        private readonly I공간동적표면Provider surfaceProvider;
        private readonly 공간동적배치PolicyData placementPolicy;
        private readonly I공간실외자산배치PlanProvider exteriorPlacementProvider;
        private int dayNumber;
        private int worldTick;
        private long worldRevision;

        public 로컬공간LHWorldEngine(
            string localWorldSeed = 공간LHWorldCodes.WorldSeed,
            int localDayNumber = 1,
            int localWorldTick = 0,
            long localWorldRevision = 0,
            공간동적MapWindowPolicyData? localWindowPolicy = null,
            I공간동적표면Provider? localSurfaceProvider = null,
            공간동적배치PolicyData? localPlacementPolicy = null,
            I공간실외자산배치PlanProvider? localExteriorPlacementProvider = null)
        {
            if (string.IsNullOrWhiteSpace(localWorldSeed))
                throw new ArgumentException("LHLocalWorldSeedMissing", nameof(localWorldSeed));
            worldSeed = localWorldSeed.Trim();
            windowPolicy = (localWindowPolicy ?? new 공간동적MapWindowPolicyData()).Clone();
            windowPolicy.Validate();
            surfaceProvider = localSurfaceProvider
                              ?? new 평면공간동적표면Provider();
            placementPolicy = localPlacementPolicy ?? new 공간동적배치PolicyData();
            placementPolicy.Validate();
            exteriorPlacementProvider = localExteriorPlacementProvider
                ?? new 결정적공간실외자산배치PlanProvider(
                    surfaceProvider, placementPolicy);
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
            var window = 공간동적MapWindowPlanner.Plan(
                focusX, focusY, windowPolicy, request.MovementDirectionCode);
            foreach (var candidate in window)
            {
                var x = candidate.CellX;
                var y = candidate.CellY;
                var cellKey = 공간LHCellKey.L3(x, y);
                if (!IsInsideCoverage(x, y))
                {
                    outside.Add(cellKey);
                    continue;
                }
                cells.Add(Cell(x, y, candidate.WindowRoleCode, candidate.Priority,
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
                AreaSetHandover = new 공간AreaSetHandoverPlanData
                {
                    RequestEpoch = request.RequestEpoch,
                    CurrentAreaSetStableId = 공간LHWorldCodes.FarmAreaSetStableId,
                    FocusL3CellKey = request.FocusL3CellKey,
                    MovementDirectionCode = request.MovementDirectionCode,
                    AvailabilityCode = 공간AreaSetHandoverCodes.NotAvailable,
                    BlockingReasonCodes = new[]
                    {
                        "AreaSetHandoverTopologyPackageUnavailable",
                    },
                    Candidates = Array.Empty<공간AreaSetHandoverCandidateData>(),
                    PlanHashSha256 = Hash("local-handover-unavailable|"
                                          + request.RequestEpoch + "|"
                                          + request.FocusL3CellKey),
                },
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
                DetailRadius = windowPolicy.DetailRadius,
                ActiveRadius = windowPolicy.ActiveRadius,
                PrefetchRadius = windowPolicy.PrefetchRadius,
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
            value.ProfileHashSha256 = Hash(string.Join("|", new[]
            {
                "local-profile", value.WorldSeed, value.AreaSetBoundaryHashSha256,
                value.GeneratorVersion,
                value.DetailRadius.ToString(CultureInfo.InvariantCulture),
                value.ActiveRadius.ToString(CultureInfo.InvariantCulture),
                value.PrefetchRadius.ToString(CultureInfo.InvariantCulture),
                surfaceProvider.SurfaceConfigurationHashSha256,
            }));
            value.Validate();
            return value;
        }

        private 공간LHCellPlanData Cell(
            int x, int y, string role, int priority, IReadOnlyCollection<string> requestedCapabilities)
        {
            var cellKey = 공간LHCellKey.L3(x, y);
            var hBindings = HBindings(x, y);
            var surface = Surface(x, y);
            var placements = exteriorPlacementProvider.Plan(
                worldSeed, cellKey, x, y);
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
                surface.SurfaceGridHashSha256,
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
                Surface = surface,
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

        private 공간LHSurfaceGridData Surface(int cellX, int cellY)
        {
            // 기존 평면 프로필은 별도 높이 배열을 만들지 않아 응답 크기와 조립 방식을 유지한다.
            if (surfaceProvider.SurfaceModeCode == "Flat")
                return new 공간LHSurfaceGridData();
            const int resolution = 17;
            var heights = new double[resolution * resolution];
            var half = placementPolicy.셀크기Meters * .5d;
            var step = placementPolicy.셀크기Meters / (resolution - 1d);
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var localX = -half + x * step;
                var localZ = -half + z * step;
                var worldX = cellX * placementPolicy.셀크기Meters + localX;
                var worldZ = cellY * placementPolicy.셀크기Meters + localZ;
                if (!surfaceProvider.TrySample(worldX, worldZ, out var sample)
                    || sample == null)
                    throw new InvalidOperationException("LHLocalSurfaceSampleUnavailable");
                heights[z * resolution + x] = sample.표면높이Meters;
            }
            var gridHash = Hash(string.Join("|", new[]
            {
                surfaceProvider.SurfaceConfigurationHashSha256,
                cellX.ToString(CultureInfo.InvariantCulture),
                cellY.ToString(CultureInfo.InvariantCulture),
                resolution.ToString(CultureInfo.InvariantCulture),
                string.Join(",", heights.Select(value =>
                    value.ToString("R", CultureInfo.InvariantCulture))),
            }));
            var value = new 공간LHSurfaceGridData
            {
                SurfaceModeCode = surfaceProvider.SurfaceModeCode,
                SurfaceRuleRevision = surfaceProvider.SurfaceRuleRevision,
                SurfaceConfigurationHashSha256 =
                    surfaceProvider.SurfaceConfigurationHashSha256,
                SurfaceGridHashSha256 = gridHash,
                SampleResolution = resolution,
                CellSizeMeters = placementPolicy.셀크기Meters,
                HeightMeters = heights,
            };
            value.Validate();
            return value;
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

        private static 공간LHHBindingData Binding(
            string level, string stableId, string state, params string[] interactions)
            => new() { HLevelCode = level, SpatialStableId = stableId, StateCode = state,
                WorldInteractionIds = interactions };

        private static string PlacementCanonical(공간LHPlacementData value)
            => string.Join(":", new[]
            {
                value.GeneratedStableId, value.CompositionKey, value.H1StableId,
                value.LocalXMeters.ToString("R", CultureInfo.InvariantCulture),
                value.LocalYMeters.ToString("R", CultureInfo.InvariantCulture),
                value.LocalZMeters.ToString("R", CultureInfo.InvariantCulture),
                value.RotationDegrees.ToString("R", CultureInfo.InvariantCulture),
                value.UniformScale.ToString("R", CultureInfo.InvariantCulture),
                value.SurfaceSlopeDegrees.ToString("R", CultureInfo.InvariantCulture),
                value.PlacementRuleRevision, value.SurfaceRuleRevision,
                value.PlacementValidationCode,
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
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class 대관령Farm공간LHWorldFixtureRepository : 로컬공간LHWorldEngine
    {
        public 대관령Farm공간LHWorldFixtureRepository(int fixtureDayNumber = 1)
            : base(공간LHWorldCodes.WorldSeed, fixtureDayNumber) { }

        public override string SourceModeCode => 공간TileStreamingCodes.Fixture;
    }
}
