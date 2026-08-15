using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 월드CompositionPackCodes
    {
        public const string Nature = "nature";
        public const string Farm = "farm";
        public const string Town = "town";
        public const string City = "city";
        public const string RegionalLogisticsHub = "regional-logistics-hub";
        public const string Mixed = "mixed";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Nature,
            Farm,
            Town,
            City,
            RegionalLogisticsHub,
            Mixed,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionSourceKinds
    {
        public const string SyntyNestedPrefab = "synty-nested-prefab";
        public const string SsalddelGenerated = "ssalddel-generated";
        public const string Mixed = "mixed";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            SyntyNestedPrefab,
            SsalddelGenerated,
            Mixed,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionVariantCodes
    {
        public const string A = "A";
        public const string B = "B";
        public const string C = "C";

        public static IReadOnlyList<string> All { get; } = new[] { A, B, C };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionDetailTierCodes
    {
        public const string World = "world";
        public const string Zone = "zone";
        public const string Object = "object";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            World,
            Zone,
            Object,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionConnectorDirectionCodes
    {
        public const string North = "north";
        public const string East = "east";
        public const string South = "south";
        public const string West = "west";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            North,
            East,
            South,
            West,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionConnectorKindCodes
    {
        public const string Pedestrian = "pedestrian";
        public const string Vehicle = "vehicle";
        public const string FarmMachine = "farm-machine";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            Pedestrian,
            Vehicle,
            FarmMachine,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionSocketCategoryCodes
    {
        public const string SimulationTarget = "simulation-target";
        public const string Actor = "actor";
        public const string Vehicle = "vehicle";
        public const string Implement = "implement";
        public const string Cargo = "cargo";
        public const string Interaction = "interaction";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            SimulationTarget,
            Actor,
            Vehicle,
            Implement,
            Cargo,
            Interaction,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    public static class 월드CompositionJourneyKindCodes
    {
        public const string None = "none";
        public const string Stateful = "stateful";
        public const string Ambient = "ambient";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            None,
            Stateful,
            Ambient,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class 월드CompositionConnectorContract
    {
        [SerializeField] private string connectorCode = string.Empty;
        [SerializeField] private string directionCode = string.Empty;
        [SerializeField] private string connectorKindCode = string.Empty;
        [SerializeField] private string routeSignature = string.Empty;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private float localYaw;
        [SerializeField] private float width;
        [SerializeField] private bool expansionSocket;

        public string ConnectorCode => connectorCode;
        public string DirectionCode => directionCode;
        public string ConnectorKindCode => connectorKindCode;
        public string RouteSignature => routeSignature;
        public Vector3 LocalPosition => localPosition;
        public float LocalYaw => localYaw;
        public float Width => width;
        public bool ExpansionSocket => expansionSocket;

        public void Configure(
            string code,
            string direction,
            string kind,
            string signature,
            Vector3 position,
            float yaw,
            float connectorWidth,
            bool isExpansionSocket)
        {
            connectorCode = code ?? string.Empty;
            directionCode = direction ?? string.Empty;
            connectorKindCode = kind ?? string.Empty;
            routeSignature = signature ?? string.Empty;
            localPosition = position;
            localYaw = yaw;
            width = connectorWidth;
            expansionSocket = isExpansionSocket;
        }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(connectorCode)
                && 월드CompositionConnectorDirectionCodes.IsKnown(directionCode)
                && 월드CompositionConnectorKindCodes.IsKnown(connectorKindCode)
                && !string.IsNullOrWhiteSpace(routeSignature)
                && IsFinite(localPosition.x)
                && IsFinite(localPosition.y)
                && IsFinite(localPosition.z)
                && IsFinite(localYaw)
                && IsFinite(width)
                && width > 0f;

        public string BuildStructuralSignature()
            => string.Join("|",
                connectorCode,
                directionCode,
                connectorKindCode,
                routeSignature,
                Format(localPosition.x),
                Format(localPosition.y),
                Format(localPosition.z),
                Format(localYaw),
                Format(width),
                expansionSocket ? "1" : "0");

        private static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Format(float value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    [Serializable]
    public sealed class 월드CompositionSocketContract
    {
        [SerializeField] private string socketCode = string.Empty;
        [SerializeField] private string categoryCode = string.Empty;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEuler;

        public string SocketCode => socketCode;
        public string CategoryCode => categoryCode;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEuler => localEuler;

        public void Configure(
            string code,
            string category,
            Vector3 position,
            Vector3 euler)
        {
            socketCode = code ?? string.Empty;
            categoryCode = category ?? string.Empty;
            localPosition = position;
            localEuler = euler;
        }

        public bool Validate()
            => !string.IsNullOrWhiteSpace(socketCode)
                && 월드CompositionSocketCategoryCodes.IsKnown(categoryCode)
                && IsFinite(localPosition)
                && IsFinite(localEuler);

        public string BuildStructuralSignature()
            => string.Join("|",
                socketCode,
                categoryCode,
                Format(localPosition.x),
                Format(localPosition.y),
                Format(localPosition.z),
                Format(localEuler.x),
                Format(localEuler.y),
                Format(localEuler.z));

        private static bool IsFinite(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static string Format(float value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    [Serializable]
    public sealed class 월드CompositionDescriptor
    {
        [SerializeField] private string compositionKey = string.Empty;
        [SerializeField] private string setName = string.Empty;
        [SerializeField] private string variantCode = string.Empty;
        [SerializeField] private string packCode = string.Empty;
        [SerializeField] private string sourceKind = string.Empty;
        [SerializeField] private Vector2 footprint;
        [SerializeField] private Vector2 cellSize;
        [SerializeField] private bool hasEnvironmentRoot;
        [SerializeField] private bool hasOcclusionRoot;
        [SerializeField] private bool hasInteriorRoot;
        [SerializeField] private string journeyKindCode = string.Empty;
        [SerializeField] private string[] detailTierCodes = Array.Empty<string>();
        [SerializeField] private 월드CompositionConnectorContract[] connectors =
            Array.Empty<월드CompositionConnectorContract>();
        [SerializeField] private 월드CompositionSocketContract[] sockets =
            Array.Empty<월드CompositionSocketContract>();

        public string CompositionKey => compositionKey;
        public string SetName => setName;
        public string VariantCode => variantCode;
        public string PackCode => packCode;
        public string SourceKind => sourceKind;
        public Vector2 Footprint => footprint;
        public Vector2 CellSize => cellSize;
        public bool HasEnvironmentRoot => hasEnvironmentRoot;
        public bool HasOcclusionRoot => hasOcclusionRoot;
        public bool HasInteriorRoot => hasInteriorRoot;
        public string JourneyKindCode => journeyKindCode;
        public IReadOnlyList<string> DetailTierCodes => detailTierCodes;
        public IReadOnlyList<월드CompositionConnectorContract> Connectors => connectors;
        public IReadOnlyList<월드CompositionSocketContract> Sockets => sockets;

        public void Configure(
            string key,
            string name,
            string variant,
            string pack,
            string source,
            Vector2 size,
            Vector2 gridCellSize,
            bool environmentRoot,
            bool occlusionRoot,
            bool interiorRoot,
            string journeyKind,
            string[] detailTiers,
            월드CompositionConnectorContract[] routeConnectors,
            월드CompositionSocketContract[] stateSockets)
        {
            compositionKey = key ?? string.Empty;
            setName = name ?? string.Empty;
            variantCode = variant ?? string.Empty;
            packCode = pack ?? string.Empty;
            sourceKind = source ?? string.Empty;
            footprint = size;
            cellSize = gridCellSize;
            hasEnvironmentRoot = environmentRoot;
            hasOcclusionRoot = occlusionRoot;
            hasInteriorRoot = interiorRoot;
            journeyKindCode = journeyKind ?? string.Empty;
            detailTierCodes = detailTiers ?? Array.Empty<string>();
            connectors = routeConnectors ?? Array.Empty<월드CompositionConnectorContract>();
            sockets = stateSockets ?? Array.Empty<월드CompositionSocketContract>();
        }

        public bool Validate()
            => compositionKey == BuildKey(packCode, setName, variantCode)
                && !string.IsNullOrWhiteSpace(setName)
                && 월드CompositionVariantCodes.IsKnown(variantCode)
                && 월드CompositionPackCodes.IsKnown(packCode)
                && 월드CompositionSourceKinds.IsKnown(sourceKind)
                && footprint.x > 0f
                && footprint.y > 0f
                && cellSize.x > 0f
                && cellSize.y > 0f
                && hasEnvironmentRoot
                && 월드CompositionJourneyKindCodes.IsKnown(journeyKindCode)
                && detailTierCodes != null
                && detailTierCodes.Length > 0
                && detailTierCodes.All(월드CompositionDetailTierCodes.IsKnown)
                && detailTierCodes.Distinct(StringComparer.Ordinal).Count()
                    == detailTierCodes.Length
                && connectors != null
                && connectors.All(value => value != null && value.Validate())
                && connectors.Select(value => value.ConnectorCode)
                    .Distinct(StringComparer.Ordinal).Count() == connectors.Length
                && sockets != null
                && sockets.All(value => value != null && value.Validate())
                && sockets.Select(value => value.SocketCode)
                    .Distinct(StringComparer.Ordinal).Count() == sockets.Length;

        public string BuildStructuralSignature()
        {
            var connectorSignature = string.Join(";", connectors
                .OrderBy(value => value.ConnectorCode, StringComparer.Ordinal)
                .Select(value => value.BuildStructuralSignature()));
            var socketSignature = string.Join(";", sockets
                .OrderBy(value => value.SocketCode, StringComparer.Ordinal)
                .Select(value => value.BuildStructuralSignature()));
            return string.Join("#",
                Format(footprint.x),
                Format(footprint.y),
                Format(cellSize.x),
                Format(cellSize.y),
                hasEnvironmentRoot ? "1" : "0",
                hasOcclusionRoot ? "1" : "0",
                hasInteriorRoot ? "1" : "0",
                connectorSignature,
                socketSignature);
        }

        public static string BuildKey(string pack, string name, string variant)
            => pack + ":" + name + ":" + variant;

        private static string Format(float value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static class 월드CompositionContractValidator
    {
        public static void Validate(
            IReadOnlyList<월드CompositionDescriptor> descriptors,
            bool requireVariantParity = true)
        {
            if (descriptors == null || descriptors.Count == 0)
                throw new InvalidOperationException("CompositionDescriptorsMissing");
            if (descriptors.Any(value => value == null || !value.Validate()))
                throw new InvalidOperationException("CompositionDescriptorInvalid");
            if (descriptors.Select(value => value.CompositionKey)
                    .Distinct(StringComparer.Ordinal).Count() != descriptors.Count)
                throw new InvalidOperationException("CompositionKeyDuplicate");

            if (!requireVariantParity) return;
            foreach (var group in descriptors.GroupBy(
                         value => value.PackCode + ":" + value.SetName,
                         StringComparer.Ordinal))
            {
                if (!group.Select(value => value.VariantCode)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(월드CompositionVariantCodes.All))
                    throw new InvalidOperationException(
                        "CompositionVariantSetInvalid:" + group.Key);
                if (group.Select(value => value.BuildStructuralSignature())
                        .Distinct(StringComparer.Ordinal).Count() != 1)
                    throw new InvalidOperationException(
                        "CompositionVariantSignatureMismatch:" + group.Key);
            }
        }
    }
}
