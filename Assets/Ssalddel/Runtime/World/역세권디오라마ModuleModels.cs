using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Unity.Runtime.World
{
    public static class StationDioramaModuleContract
    {
        public const string SchemaVersion = "station-diorama-module-profile.v1";
        public const string SagajeongReferenceRevision =
            "station-diorama-module:sagajeong-reference.r1";
    }

    public static class StationDioramaModuleLayerIds
    {
        public const string DataEvidence = "station-diorama-layer:data-evidence.v1";
        public const string BaseSpatialPresentation =
            "station-diorama-layer:base-spatial-presentation.v1";
        public const string SpatialMeaning = "station-diorama-layer:spatial-meaning.v1";
        public const string Mobility = "station-diorama-layer:mobility.v1";
        public const string Interaction = "station-diorama-layer:interaction.v1";
        public const string LifeSimulation = "station-diorama-layer:life-simulation.v1";
        public const string BusinessOverlay = "station-diorama-layer:business-overlay.v1";
        public const string EnvironmentPresentation =
            "station-diorama-layer:environment-presentation.v1";
    }

    public static class StationDioramaModuleLayerKinds
    {
        public const string DataEvidence = "DataEvidence";
        public const string BaseSpatialPresentation = "BaseSpatialPresentation";
        public const string SpatialMeaning = "SpatialMeaning";
        public const string Mobility = "Mobility";
        public const string Interaction = "Interaction";
        public const string LifeSimulation = "LifeSimulation";
        public const string BusinessOverlay = "BusinessOverlay";
        public const string EnvironmentPresentation = "EnvironmentPresentation";
    }

    public static class StationDioramaModuleLayerStateCodes
    {
        public const string Ready = "Ready";
        public const string PrivateReview = "PrivateReview";
        public const string Blocked = "Blocked";
        public const string NotProvided = "NotProvided";
        public const string BlockedByDependency = "BlockedByDependency";

        public static bool IsAvailableForObservation(string value)
            => string.Equals(value, Ready, StringComparison.Ordinal)
               || string.Equals(value, PrivateReview, StringComparison.Ordinal);

        public static bool IsKnown(string value)
            => IsAvailableForObservation(value)
               || string.Equals(value, Blocked, StringComparison.Ordinal)
               || string.Equals(value, NotProvided, StringComparison.Ordinal)
               || string.Equals(value, BlockedByDependency, StringComparison.Ordinal);
    }

    public sealed class StationDioramaModuleLayerDefinition
    {
        public StationDioramaModuleLayerDefinition(string layerStableId, string layerKindCode,
            int assemblyOrder, bool requiredForObservation,
            params string[] dependencyLayerStableIds)
        {
            LayerStableId = Required(layerStableId, nameof(layerStableId));
            LayerKindCode = Required(layerKindCode, nameof(layerKindCode));
            if (assemblyOrder < 1) throw new ArgumentOutOfRangeException(nameof(assemblyOrder));
            AssemblyOrder = assemblyOrder;
            RequiredForObservation = requiredForObservation;
            DependencyLayerStableIds = Array.AsReadOnly(
                (dependencyLayerStableIds ?? Array.Empty<string>())
                .Select(value => Required(value, nameof(dependencyLayerStableIds)))
                .ToArray());
        }

        public string LayerStableId { get; }
        public string LayerKindCode { get; }
        public int AssemblyOrder { get; }
        public bool RequiredForObservation { get; }
        public IReadOnlyList<string> DependencyLayerStableIds { get; }

        private static string Required(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                throw new ArgumentException("StationDioramaModuleTextInvalid", parameter);
            return value.Trim();
        }
    }

    public sealed class StationDioramaModuleProfile
    {
        public StationDioramaModuleProfile(string transitStationStableId, string displayName,
            string revision, IEnumerable<StationDioramaModuleLayerDefinition> layers)
        {
            TransitStationStableId = Required(transitStationStableId,
                nameof(transitStationStableId));
            if (!TransitStationStableId.StartsWith("station:kr:kric:", StringComparison.Ordinal))
                throw new ArgumentException("StationDioramaModuleStationStableIdInvalid",
                    nameof(transitStationStableId));
            DisplayName = Required(displayName, nameof(displayName));
            Revision = Required(revision, nameof(revision));
            var ordered = (layers ?? throw new ArgumentNullException(nameof(layers)))
                .OrderBy(value => value.AssemblyOrder)
                .ThenBy(value => value.LayerStableId, StringComparer.Ordinal)
                .ToArray();
            Validate(ordered);
            Layers = Array.AsReadOnly(ordered);
        }

        public string SchemaVersion => StationDioramaModuleContract.SchemaVersion;
        public string TransitStationStableId { get; }
        public string DisplayName { get; }
        public string Revision { get; }
        public IReadOnlyList<StationDioramaModuleLayerDefinition> Layers { get; }
        public bool ObservationPresentationOnly => true;
        public bool ChangesOperationalState => false;
        public bool TraversalReady => false;
        public bool GameplayReady => false;
        public bool DistributionApproved => false;

        private static void Validate(IReadOnlyList<StationDioramaModuleLayerDefinition> layers)
        {
            if (layers.Count == 0) throw new ArgumentException("StationDioramaModuleLayersEmpty");
            if (layers.Any(value => value == null))
                throw new ArgumentException("StationDioramaModuleLayerNull");
            if (layers.GroupBy(value => value.LayerStableId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
                throw new ArgumentException("StationDioramaModuleLayerDuplicate");
            if (layers.GroupBy(value => value.AssemblyOrder).Any(group => group.Count() != 1))
                throw new ArgumentException("StationDioramaModuleAssemblyOrderDuplicate");

            var byId = layers.ToDictionary(value => value.LayerStableId, StringComparer.Ordinal);
            foreach (var layer in layers)
            foreach (var dependencyId in layer.DependencyLayerStableIds)
            {
                if (!byId.TryGetValue(dependencyId, out var dependency))
                    throw new ArgumentException("StationDioramaModuleDependencyMissing");
                if (dependency.AssemblyOrder >= layer.AssemblyOrder)
                    throw new ArgumentException("StationDioramaModuleDependencyOrderInvalid");
            }

            var required = layers.Where(value => value.RequiredForObservation)
                .Select(value => value.LayerStableId).ToArray();
            if (!required.Contains(StationDioramaModuleLayerIds.DataEvidence, StringComparer.Ordinal)
                || !required.Contains(StationDioramaModuleLayerIds.BaseSpatialPresentation,
                    StringComparer.Ordinal))
                throw new ArgumentException("StationDioramaModuleRequiredFoundationMissing");
        }

        private static string Required(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
                throw new ArgumentException("StationDioramaModuleTextInvalid", parameter);
            return value.Trim();
        }
    }

    public sealed class StationDioramaModuleLayerRuntimeState
    {
        public StationDioramaModuleLayerRuntimeState(string layerStableId, string stateCode,
            string sourceRevision, string diagnosticCode)
        {
            if (string.IsNullOrWhiteSpace(layerStableId))
                throw new ArgumentException("StationDioramaModuleLayerStableIdMissing",
                    nameof(layerStableId));
            if (!StationDioramaModuleLayerStateCodes.IsKnown(stateCode))
                throw new ArgumentException("StationDioramaModuleLayerStateInvalid",
                    nameof(stateCode));
            LayerStableId = layerStableId.Trim();
            StateCode = stateCode;
            SourceRevision = sourceRevision?.Trim() ?? string.Empty;
            DiagnosticCode = diagnosticCode?.Trim() ?? string.Empty;
        }

        public string LayerStableId { get; }
        public string StateCode { get; }
        public string SourceRevision { get; }
        public string DiagnosticCode { get; }
        public bool AvailableForObservation =>
            StationDioramaModuleLayerStateCodes.IsAvailableForObservation(StateCode);
    }

    /// <summary>
    /// 역별 Adapter가 가용 여부를 서로 다른 상태 코드로 해석하지 않도록 만드는 공통 생성기다.
    /// 자료 판정이나 공개 승격은 하지 않고 이미 판정된 읽기 전용 상태만 정규화한다.
    /// </summary>
    public static class StationDioramaModuleLayerRuntimeStateFactory
    {
        public static StationDioramaModuleLayerRuntimeState FromAvailability(
            string layerStableId, bool available, string sourceRevision,
            string diagnosticCode,
            string availableStateCode = StationDioramaModuleLayerStateCodes.PrivateReview,
            string unavailableStateCode = StationDioramaModuleLayerStateCodes.Blocked)
        {
            if (!StationDioramaModuleLayerStateCodes.IsAvailableForObservation(
                    availableStateCode))
                throw new ArgumentException("StationDioramaModuleAvailableStateInvalid",
                    nameof(availableStateCode));
            if (!StationDioramaModuleLayerStateCodes.IsKnown(unavailableStateCode)
                || StationDioramaModuleLayerStateCodes.IsAvailableForObservation(
                    unavailableStateCode))
                throw new ArgumentException("StationDioramaModuleUnavailableStateInvalid",
                    nameof(unavailableStateCode));
            return new StationDioramaModuleLayerRuntimeState(layerStableId,
                available ? availableStateCode : unavailableStateCode,
                sourceRevision, diagnosticCode);
        }

        public static StationDioramaModuleLayerRuntimeState NotProvided(
            string layerStableId, string diagnosticCode)
            => new(layerStableId, StationDioramaModuleLayerStateCodes.NotProvided,
                string.Empty, diagnosticCode);
    }

    public sealed class StationDioramaModuleLayerResolution
    {
        public StationDioramaModuleLayerResolution(
            StationDioramaModuleLayerDefinition definition,
            StationDioramaModuleLayerRuntimeState runtimeState)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RuntimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
        }

        public StationDioramaModuleLayerDefinition Definition { get; }
        public StationDioramaModuleLayerRuntimeState RuntimeState { get; }
        public bool AvailableForObservation => RuntimeState.AvailableForObservation;
    }

    public sealed class StationDioramaModuleActivationPlan
    {
        public StationDioramaModuleActivationPlan(StationDioramaModuleProfile profile,
            IEnumerable<StationDioramaModuleLayerResolution> layers,
            IEnumerable<string> missingRequiredLayerStableIds,
            IEnumerable<string> missingOptionalLayerStableIds, string assemblyHashSha256)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Layers = Array.AsReadOnly((layers ?? throw new ArgumentNullException(nameof(layers)))
                .ToArray());
            MissingRequiredLayerStableIds = Array.AsReadOnly(
                (missingRequiredLayerStableIds ?? throw new ArgumentNullException(
                    nameof(missingRequiredLayerStableIds))).ToArray());
            MissingOptionalLayerStableIds = Array.AsReadOnly(
                (missingOptionalLayerStableIds ?? throw new ArgumentNullException(
                    nameof(missingOptionalLayerStableIds))).ToArray());
            AssemblyHashSha256 = assemblyHashSha256 ?? string.Empty;
        }

        public StationDioramaModuleProfile Profile { get; }
        public IReadOnlyList<StationDioramaModuleLayerResolution> Layers { get; }
        public IReadOnlyList<string> MissingRequiredLayerStableIds { get; }
        public IReadOnlyList<string> MissingOptionalLayerStableIds { get; }
        public string AssemblyHashSha256 { get; }
        public bool CanPresentObservation => MissingRequiredLayerStableIds.Count == 0;
        public bool OptionalLayersComplete => MissingOptionalLayerStableIds.Count == 0;
    }

    public static class StationDioramaModulePlanner
    {
        public static StationDioramaModuleActivationPlan Build(
            StationDioramaModuleProfile profile,
            IEnumerable<StationDioramaModuleLayerRuntimeState> runtimeStates)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var states = (runtimeStates ?? Array.Empty<StationDioramaModuleLayerRuntimeState>())
                .ToArray();
            if (states.Any(value => value == null))
                throw new ArgumentException("StationDioramaModuleRuntimeStateNull");
            if (states.GroupBy(value => value.LayerStableId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
                throw new ArgumentException("StationDioramaModuleRuntimeStateDuplicate");

            var definitions = profile.Layers.ToDictionary(value => value.LayerStableId,
                StringComparer.Ordinal);
            if (states.Any(value => !definitions.ContainsKey(value.LayerStableId)))
                throw new ArgumentException("StationDioramaModuleRuntimeLayerUnknown");
            var provided = states.ToDictionary(value => value.LayerStableId,
                StringComparer.Ordinal);
            var resolved = new List<StationDioramaModuleLayerResolution>();
            var resolvedById = new Dictionary<string, StationDioramaModuleLayerRuntimeState>(
                StringComparer.Ordinal);

            foreach (var definition in profile.Layers)
            {
                var state = provided.TryGetValue(definition.LayerStableId, out var supplied)
                    ? supplied
                    : new StationDioramaModuleLayerRuntimeState(definition.LayerStableId,
                        StationDioramaModuleLayerStateCodes.NotProvided, string.Empty,
                        "WaitingForModuleAdapter");
                var blockedDependency = definition.DependencyLayerStableIds.FirstOrDefault(
                    dependencyId => !resolvedById.TryGetValue(dependencyId, out var dependency)
                                    || !dependency.AvailableForObservation);
                if (blockedDependency != null && state.AvailableForObservation)
                    state = new StationDioramaModuleLayerRuntimeState(definition.LayerStableId,
                        StationDioramaModuleLayerStateCodes.BlockedByDependency,
                        state.SourceRevision, "BlockedByDependency:" + blockedDependency);
                resolvedById.Add(definition.LayerStableId, state);
                resolved.Add(new StationDioramaModuleLayerResolution(definition, state));
            }

            var missingRequired = resolved.Where(value => value.Definition.RequiredForObservation
                                                           && !value.AvailableForObservation)
                .Select(value => value.Definition.LayerStableId).ToArray();
            var missingOptional = resolved.Where(value => !value.Definition.RequiredForObservation
                                                           && !value.AvailableForObservation)
                .Select(value => value.Definition.LayerStableId).ToArray();
            return new StationDioramaModuleActivationPlan(profile, resolved, missingRequired,
                missingOptional, Hash(profile, resolved));
        }

        private static string Hash(StationDioramaModuleProfile profile,
            IEnumerable<StationDioramaModuleLayerResolution> layers)
        {
            var canonical = string.Join("\n", new[]
            {
                profile.SchemaVersion,
                profile.TransitStationStableId,
                profile.Revision
            }.Concat(layers.Select(value => string.Join("|",
                value.Definition.AssemblyOrder,
                value.Definition.LayerStableId,
                value.Definition.RequiredForObservation ? "required" : "optional",
                string.Join(",", value.Definition.DependencyLayerStableIds),
                value.RuntimeState.StateCode,
                value.RuntimeState.SourceRevision,
                value.RuntimeState.DiagnosticCode))));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
                .Replace("-", string.Empty);
        }
    }

    public static class StationDioramaModuleStandard
    {
        /// <summary>
        /// 새 역의 자료나 View를 만들지 않고도 공통 모듈 구조를 먼저 검증할 수 있는
        /// 위치 독립 Profile factory다. 역별 값은 식별자·표시명·판본만 받는다.
        /// </summary>
        public static StationDioramaModuleProfile CreateProfile(string transitStationStableId,
            string displayName, string revision)
            => new(transitStationStableId, displayName, revision,
                new[]
                {
                    new StationDioramaModuleLayerDefinition(StationDioramaModuleLayerIds.DataEvidence,
                        StationDioramaModuleLayerKinds.DataEvidence, 10, true),
                    new StationDioramaModuleLayerDefinition(
                        StationDioramaModuleLayerIds.BaseSpatialPresentation,
                        StationDioramaModuleLayerKinds.BaseSpatialPresentation, 20, true,
                        StationDioramaModuleLayerIds.DataEvidence),
                    new StationDioramaModuleLayerDefinition(
                        StationDioramaModuleLayerIds.SpatialMeaning,
                        StationDioramaModuleLayerKinds.SpatialMeaning, 30, false,
                        StationDioramaModuleLayerIds.DataEvidence),
                    new StationDioramaModuleLayerDefinition(StationDioramaModuleLayerIds.Mobility,
                        StationDioramaModuleLayerKinds.Mobility, 40, false,
                        StationDioramaModuleLayerIds.DataEvidence),
                    new StationDioramaModuleLayerDefinition(StationDioramaModuleLayerIds.Interaction,
                        StationDioramaModuleLayerKinds.Interaction, 50, false,
                        StationDioramaModuleLayerIds.SpatialMeaning),
                    new StationDioramaModuleLayerDefinition(
                        StationDioramaModuleLayerIds.LifeSimulation,
                        StationDioramaModuleLayerKinds.LifeSimulation, 60, false,
                        StationDioramaModuleLayerIds.Mobility),
                    new StationDioramaModuleLayerDefinition(
                        StationDioramaModuleLayerIds.BusinessOverlay,
                        StationDioramaModuleLayerKinds.BusinessOverlay, 70, false,
                        StationDioramaModuleLayerIds.DataEvidence),
                    new StationDioramaModuleLayerDefinition(
                        StationDioramaModuleLayerIds.EnvironmentPresentation,
                        StationDioramaModuleLayerKinds.EnvironmentPresentation, 80, false,
                        StationDioramaModuleLayerIds.BaseSpatialPresentation)
                });
    }

    /// <summary>
    /// 활성 공통 Host에 올릴 수 있는 프로필만 둔다. 기존 면목·용마산 비공개 자료 대장은
    /// 보존하지만 이번 표준화 절편에서는 활성 모듈 프로필로 등록하지 않는다.
    /// </summary>
    public static class StationDioramaModuleProfileCatalog
    {
        public static readonly StationDioramaModuleProfile SagajeongReference =
            StationDioramaModuleStandard.CreateProfile(
                StationDioramaProfileCatalog.SagajeongStationStableId,
                "사가정역 1km 참조 디오라마",
                StationDioramaModuleContract.SagajeongReferenceRevision);

        private static readonly IReadOnlyDictionary<string, StationDioramaModuleProfile>
            Profiles = new ReadOnlyDictionary<string, StationDioramaModuleProfile>(
                new Dictionary<string, StationDioramaModuleProfile>(StringComparer.Ordinal)
                {
                    [SagajeongReference.TransitStationStableId] = SagajeongReference
                });

        public static bool TryResolve(string transitStationStableId,
            out StationDioramaModuleProfile profile)
            => Profiles.TryGetValue(transitStationStableId ?? string.Empty, out profile);
    }
}
