using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor
{
    public static class SyntyCompositionSourceRoleCodes
    {
        public const string Base = "base";
        public const string Overlay = "overlay";
        public const string CompleteBuilding = "complete-building";
        public const string ModularPart = "modular-part";
        public const string Accent = "accent";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Base,
            Overlay,
            CompleteBuilding,
            ModularPart,
            Accent,
        };
    }

    public static class SyntyCompositionAxisCodes
    {
        public const string X = "x";
        public const string Z = "z";
        public const string Square = "square";
    }

    public static class SyntyCompositionEntranceDirectionCodes
    {
        public const string None = "none";
        public const string Unknown = "unknown";
        public const string North = "north";
        public const string East = "east";
        public const string South = "south";
        public const string West = "west";
    }

    public sealed class SyntyCompositionSourceDefinition
    {
        public SyntyCompositionSourceDefinition(
            string packCode,
            string sourceRoleCode,
            string assetPath,
            float gridCellSize = 0f,
            bool inspectEntrance = false)
        {
            PackCode = packCode;
            SourceRoleCode = sourceRoleCode;
            AssetPath = assetPath;
            GridCellSize = gridCellSize;
            InspectEntrance = inspectEntrance;
        }

        public string PackCode { get; }
        public string SourceRoleCode { get; }
        public string AssetPath { get; }
        public float GridCellSize { get; }
        public bool InspectEntrance { get; }
    }

    [Serializable]
    public sealed class SyntyCompositionSourceMeasurementEntry
    {
        public string assetPath = string.Empty;
        public string packCode = string.Empty;
        public string sourceRoleCode = string.Empty;
        public Vector3 localBoundsCenter;
        public Vector3 localBoundsSize;
        public Vector3 pivotToBoundsCenter;
        public string dominantHorizontalAxisCode = string.Empty;
        public string entranceDirectionCode = string.Empty;
        public int rendererCount;
        public int colliderCount;
        public int lodGroupCount;
        public int animatorCount;
        public int particleSystemCount;
        public int nonUnitScaleTransformCount;
        public bool rootScaleIsUnit;
        public bool navMeshCandidate;
        public float gridCellSize;
        public float boundsXGridError;
        public float boundsZGridError;
        public string[] shaderNames = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SyntyCompositionSourceMeasurementReport
    {
        public string generatedAtUtc = string.Empty;
        public float townCityGridCellSize;
        public float gridTolerance;
        public bool townGridConfirmed;
        public bool cityGridConfirmed;
        public string farmAdapterAxisCode = string.Empty;
        public float farmRoadModuleLength;
        public float farmRoadGridError;
        public Vector3 farmToTownAdapterOffset;
        public int totalRendererCount;
        public int totalColliderCount;
        public int totalLodGroupCount;
        public string[] sharedShaderNames = Array.Empty<string>();
        public SyntyCompositionSourceMeasurementEntry[] entries =
            Array.Empty<SyntyCompositionSourceMeasurementEntry>();
    }

    public static class SyntyCompositionSourceMeasurementCatalog
    {
        private const string TownBuildingRoot =
            "Assets/Synty/PolygonTown/Prefabs/Buildings/Presets/";
        private const string TownEnvironmentRoot =
            "Assets/Synty/PolygonTown/Prefabs/Environment/";
        private const string CityBuildingRoot =
            "Assets/Synty/PolygonCity/Prefabs/Buildings/";
        private const string CityEnvironmentRoot =
            "Assets/Synty/PolygonCity/Prefabs/Environments/";
        private const string FarmBuildingRoot =
            "Assets/Synty/PolygonFarm/Prefabs/Buildings/";
        private const string FarmEnvironmentRoot =
            "Assets/Synty/PolygonFarm/Prefabs/Environments/";

        public static IReadOnlyList<SyntyCompositionSourceDefinition> Definitions { get; } =
            BuildDefinitions();

        private static IReadOnlyList<SyntyCompositionSourceDefinition> BuildDefinitions()
        {
            var values = new List<SyntyCompositionSourceDefinition>();
            for (var index = 1; index <= 11; index++)
            {
                values.Add(new SyntyCompositionSourceDefinition(
                    "town",
                    SyntyCompositionSourceRoleCodes.CompleteBuilding,
                    TownBuildingRoot + $"SM_Bld_House_Preset_{index:00}.prefab",
                    0f,
                    true));
            }

            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.CompleteBuilding,
                TownBuildingRoot + "SM_Bld_House_Preset_Garage_01.prefab",
                0f,
                true));
            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.Base,
                TownEnvironmentRoot + "SM_Env_Road_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.Base,
                TownEnvironmentRoot + "SM_Env_Road_Corner_End_02.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.Base,
                TownEnvironmentRoot + "SM_Env_Sidewalk_Straight_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.Base,
                TownEnvironmentRoot + "SM_Env_Sidewalk_Corner_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.Overlay,
                TownEnvironmentRoot + "SM_Env_Driveway_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "town",
                SyntyCompositionSourceRoleCodes.Accent,
                TownEnvironmentRoot + "SM_Env_Road_SpeedBump_01.prefab"));

            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Base,
                CityEnvironmentRoot + "SM_Env_Road_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Base,
                CityEnvironmentRoot + "SM_Env_Road_02.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Base,
                CityEnvironmentRoot + "SM_Env_Road_03.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Base,
                CityEnvironmentRoot + "SM_Env_Sidewalk_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Base,
                CityEnvironmentRoot + "SM_Env_Sidewalk_Corner_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Overlay,
                CityEnvironmentRoot + "SM_Env_Road_Lines_01.prefab",
                5f));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.Accent,
                CityEnvironmentRoot + "SM_Env_Road_Arrow_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.ModularPart,
                CityBuildingRoot + "SM_Bld_Apartment_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.ModularPart,
                CityBuildingRoot + "SM_Bld_Apartment_Corner_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.ModularPart,
                CityBuildingRoot + "SM_Bld_Apartment_Door_01.prefab",
                0f,
                true));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.ModularPart,
                CityBuildingRoot + "SM_Bld_Apartment_Stack_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "city",
                SyntyCompositionSourceRoleCodes.ModularPart,
                CityBuildingRoot + "SM_Bld_Apartment_Roof_01.prefab"));

            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Base,
                FarmEnvironmentRoot + "SM_Env_Dirt_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Base,
                FarmEnvironmentRoot + "SM_Env_Dirt_Rows_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.ModularPart,
                FarmEnvironmentRoot + "SM_Env_Dirt_Rows_Center_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.ModularPart,
                FarmEnvironmentRoot + "SM_Env_Dirt_Rows_End_Top_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Overlay,
                FarmEnvironmentRoot + "SM_Env_Dirt_Rows_Mounds_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Overlay,
                FarmEnvironmentRoot + "SM_Env_Dirt_Rows_Skirt_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Base,
                FarmEnvironmentRoot + "SM_Env_Road_Dirt_Straight_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Base,
                FarmEnvironmentRoot + "SM_Env_Road_Dirt_Corner_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Base,
                FarmEnvironmentRoot + "SM_Env_Road_Dirt_T_Section_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.Base,
                FarmEnvironmentRoot + "SM_Env_Road_Dirt_Intersection_01.prefab"));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.CompleteBuilding,
                FarmBuildingRoot + "SM_Bld_Greenhouse_01.prefab",
                0f,
                true));
            values.Add(new SyntyCompositionSourceDefinition(
                "farm",
                SyntyCompositionSourceRoleCodes.CompleteBuilding,
                FarmBuildingRoot + "SM_Bld_Greenhouse_Large_01.prefab",
                0f,
                true));

            return values;
        }
    }

    public static class SyntyCompositionSourceMeasurementInspector
    {
        public const float TownCityGridCellSize = 5f;
        public const float GridTolerance = 0.05f;
        public const string DefaultReportPath =
            "artifacts/CMP2/SyntyCompositionSourceMeasurements.json";

        [MenuItem("Ssalddel/Validation/Measure Synty Composition Sources")]
        public static void MeasureAndWriteReport()
        {
            var report = Inspect();
            var absolutePath = Path.GetFullPath(DefaultReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)
                ?? throw new InvalidOperationException("MeasurementReportDirectoryMissing"));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(report, true));
            Debug.Log(
                "SyntyCompositionMeasurements:"
                + $"entries={report.entries.Length};"
                + $"townGrid={report.townGridConfirmed};"
                + $"cityGrid={report.cityGridConfirmed};"
                + $"farmAxis={report.farmAdapterAxisCode};"
                + $"farmLength={report.farmRoadModuleLength:0.###};"
                + $"farmGridError={report.farmRoadGridError:0.###};"
                + $"farmOffset={report.farmToTownAdapterOffset};"
                + $"lodGroups={report.totalLodGroupCount};"
                + $"report={absolutePath}");
        }

        public static SyntyCompositionSourceMeasurementReport Inspect()
        {
            var entries = SyntyCompositionSourceMeasurementCatalog.Definitions
                .Select(Measure)
                .ToArray();
            var farmRoad = entries.Single(value => value.assetPath.EndsWith(
                "/SM_Env_Road_Dirt_Straight_01.prefab",
                StringComparison.Ordinal));
            var farmLength = farmRoad.dominantHorizontalAxisCode ==
                             SyntyCompositionAxisCodes.X
                ? farmRoad.localBoundsSize.x
                : farmRoad.localBoundsSize.z;
            var snappedFarmLength = Mathf.Round(farmLength / TownCityGridCellSize)
                                    * TownCityGridCellSize;
            if (snappedFarmLength <= 0f) snappedFarmLength = TownCityGridCellSize;
            var adapterDelta = (snappedFarmLength - farmLength) * 0.5f;
            var adapterOffset = farmRoad.dominantHorizontalAxisCode ==
                                SyntyCompositionAxisCodes.X
                ? new Vector3(adapterDelta, 0f, 0f)
                : new Vector3(0f, 0f, adapterDelta);

            return new SyntyCompositionSourceMeasurementReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                townCityGridCellSize = TownCityGridCellSize,
                gridTolerance = GridTolerance,
                townGridConfirmed = IsGridConfirmed(entries, "town"),
                cityGridConfirmed = IsGridConfirmed(entries, "city"),
                farmAdapterAxisCode = farmRoad.dominantHorizontalAxisCode,
                farmRoadModuleLength = farmLength,
                farmRoadGridError = Mathf.Abs(snappedFarmLength - farmLength),
                farmToTownAdapterOffset = adapterOffset,
                totalRendererCount = entries.Sum(value => value.rendererCount),
                totalColliderCount = entries.Sum(value => value.colliderCount),
                totalLodGroupCount = entries.Sum(value => value.lodGroupCount),
                sharedShaderNames = entries.SelectMany(value => value.shaderNames)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                entries = entries,
            };
        }

        private static SyntyCompositionSourceMeasurementEntry Measure(
            SyntyCompositionSourceDefinition definition)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(definition.AssetPath) == null)
                throw new InvalidOperationException(
                    "SyntyCompositionSourceMissing:" + definition.AssetPath);

            var root = PrefabUtility.LoadPrefabContents(definition.AssetPath);
            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException(
                        "SyntyCompositionRendererMissing:" + definition.AssetPath);
                var bounds = CalculateLocalBounds(root.transform, renderers);
                var colliders = root.GetComponentsInChildren<Collider>(true);
                var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
                return new SyntyCompositionSourceMeasurementEntry
                {
                    assetPath = definition.AssetPath,
                    packCode = definition.PackCode,
                    sourceRoleCode = definition.SourceRoleCode,
                    localBoundsCenter = bounds.center,
                    localBoundsSize = bounds.size,
                    pivotToBoundsCenter = bounds.center,
                    dominantHorizontalAxisCode = ClassifyHorizontalAxis(bounds.size),
                    entranceDirectionCode = definition.InspectEntrance
                        ? FindEntranceDirection(root.transform, renderers, bounds)
                        : SyntyCompositionEntranceDirectionCodes.None,
                    rendererCount = renderers.Length,
                    colliderCount = colliders.Length,
                    lodGroupCount = lodGroups.Length,
                    animatorCount = root.GetComponentsInChildren<Animator>(true).Length,
                    particleSystemCount = root.GetComponentsInChildren<ParticleSystem>(true).Length,
                    nonUnitScaleTransformCount = root.GetComponentsInChildren<Transform>(true)
                        .Count(value => !ApproximatelyUnit(value.localScale)),
                    rootScaleIsUnit = ApproximatelyUnit(root.transform.localScale),
                    navMeshCandidate = colliders.Length > 0
                                       && bounds.size.x > 0f
                                       && bounds.size.z > 0f,
                    gridCellSize = definition.GridCellSize,
                    boundsXGridError = CalculateGridError(bounds.size.x, definition.GridCellSize),
                    boundsZGridError = CalculateGridError(bounds.size.z, definition.GridCellSize),
                    shaderNames = renderers.SelectMany(value => value.sharedMaterials)
                        .Where(value => value != null && value.shader != null)
                        .Select(value => value.shader.name)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Bounds CalculateLocalBounds(
            Transform root,
            IReadOnlyList<Renderer> renderers)
        {
            var initialized = false;
            var bounds = default(Bounds);
            foreach (var renderer in renderers)
            {
                var worldBounds = renderer.bounds;
                var center = worldBounds.center;
                var extents = worldBounds.extents;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var worldCorner = center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z));
                    var localCorner = root.InverseTransformPoint(worldCorner);
                    if (!initialized)
                    {
                        bounds = new Bounds(localCorner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localCorner);
                    }
                }
            }

            return bounds;
        }

        private static string FindEntranceDirection(
            Transform root,
            IReadOnlyList<Renderer> renderers,
            Bounds bounds)
        {
            var entranceRenderers = renderers.Where(value =>
                HasDoorNamedAncestor(value.transform, root)).ToArray();
            if (entranceRenderers.Length == 0)
                return SyntyCompositionEntranceDirectionCodes.Unknown;

            var center = entranceRenderers
                .Select(value => root.InverseTransformPoint(value.bounds.center))
                .Aggregate(Vector3.zero, (sum, value) => sum + value)
                / entranceRenderers.Length;
            var candidates = new[]
            {
                (distance: Mathf.Abs(bounds.max.z - center.z),
                    code: SyntyCompositionEntranceDirectionCodes.North),
                (distance: Mathf.Abs(bounds.max.x - center.x),
                    code: SyntyCompositionEntranceDirectionCodes.East),
                (distance: Mathf.Abs(bounds.min.z - center.z),
                    code: SyntyCompositionEntranceDirectionCodes.South),
                (distance: Mathf.Abs(bounds.min.x - center.x),
                    code: SyntyCompositionEntranceDirectionCodes.West),
            };
            return candidates.OrderBy(value => value.distance).First().code;
        }

        private static bool HasDoorNamedAncestor(Transform transform, Transform root)
        {
            var current = transform;
            while (current != null)
            {
                if (current.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (current == root) break;
                current = current.parent;
            }

            return false;
        }

        private static string ClassifyHorizontalAxis(Vector3 size)
        {
            if (Mathf.Abs(size.x - size.z) <= GridTolerance)
                return SyntyCompositionAxisCodes.Square;
            return size.x > size.z
                ? SyntyCompositionAxisCodes.X
                : SyntyCompositionAxisCodes.Z;
        }

        private static float CalculateGridError(float length, float gridCellSize)
        {
            if (gridCellSize <= 0f) return 0f;
            return Mathf.Abs(length - Mathf.Round(length / gridCellSize) * gridCellSize);
        }

        private static bool IsGridConfirmed(
            IEnumerable<SyntyCompositionSourceMeasurementEntry> entries,
            string packCode)
        {
            var candidates = entries.Where(value =>
                    string.Equals(value.packCode, packCode, StringComparison.Ordinal)
                    && value.gridCellSize > 0f)
                .ToArray();
            return candidates.Length > 0 && candidates.All(value =>
                value.boundsXGridError <= GridTolerance
                && value.boundsZGridError <= GridTolerance);
        }

        private static bool ApproximatelyUnit(Vector3 scale)
            => Mathf.Approximately(scale.x, 1f)
               && Mathf.Approximately(scale.y, 1f)
               && Mathf.Approximately(scale.z, 1f);
    }
}
