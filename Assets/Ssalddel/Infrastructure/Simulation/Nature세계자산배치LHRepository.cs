using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// 로컬 LH 결과에 공통 Simulation Core가 만든 Nature 실외·실내 계획을
    /// 결속한다. 지면 생성이나 Prefab 생명주기를 소유하지 않는다.
    /// </summary>
    public sealed class Nature세계자산배치LHRepository
        : I공간LHWorldRepository
    {
        private readonly 로컬공간LHWorldEngine lhEngine;
        private readonly SimulationWorldLocalRuntimeScope runtimeScope;
        private readonly string natureOwnerCellKey;
        private readonly double ownerLocalOriginXMeters;
        private readonly double ownerLocalOriginZMeters;
        private readonly SimulationNatureWorldCellAssemblyEngine assemblyEngine
            = new();

        public Nature세계자산배치LHRepository(
            로컬공간LHWorldEngine localLhEngine,
            SimulationWorldLocalRuntimeScope localRuntimeScope,
            string ownerCellKey,
            double ownerLocalX,
            double ownerLocalZ)
        {
            lhEngine = localLhEngine
                ?? throw new ArgumentNullException(nameof(localLhEngine));
            runtimeScope = localRuntimeScope
                ?? throw new ArgumentNullException(nameof(localRuntimeScope));
            if (!공간LHCellKey.TryParseL3(ownerCellKey, out _, out _))
                throw new ArgumentException("NatureOwnerCellKeyInvalid",
                    nameof(ownerCellKey));
            natureOwnerCellKey = ownerCellKey;
            ownerLocalOriginXMeters = ownerLocalX;
            ownerLocalOriginZMeters = ownerLocalZ;
        }

        public string SourceModeCode => lhEngine.SourceModeCode;

        public async Task<공간LHCellPreviewData> PreviewCellsAsync(
            공간LHCellPreviewRequestData request,
            CancellationToken cancellationToken)
        {
            var session = await runtimeScope.Runtime.Sessions.GetAsync(
                runtimeScope.SessionStableId, cancellationToken);
            var dayNumber = Math.Max(1,
                (session.WorldContext.GameDate.Date
                 - session.WorldContext.GameDateStartsOn.Date).Days + 1);
            lhEngine.SetLocalCalendar(dayNumber,
                session.WorldContext.WorldTick,
                session.WorldContext.WorldRevision);
            var response = await lhEngine.PreviewCellsAsync(request,
                cancellationToken);
            var nature = await runtimeScope.Runtime.Nature.GetAsync(
                runtimeScope.SessionStableId, cancellationToken);

            foreach (var cell in response.Cells)
            {
                var source = ToContract(cell);
                var surfaceMode = cell.Surface?.SurfaceModeCode
                                  ?? string.Empty;
                var surfaceHash = cell.Surface?.SurfaceGridHashSha256
                                  ?? string.Empty;
                cell.WorldAssetAssembly = assemblyEngine.Compose(
                    source, nature, response.WorldRevision,
                    string.Equals(cell.CellKey, natureOwnerCellKey,
                        StringComparison.Ordinal),
                    surfaceMode, surfaceHash,
                    ownerLocalOriginXMeters,
                    ownerLocalOriginZMeters);
            }
            response.Validate(request.RequestEpoch);
            return response;
        }

        private static SimulationLhCellPlanResponse ToContract(
            공간LHCellPlanData value)
            => new()
            {
                CellKey = value.CellKey,
                CellX = value.CellX,
                CellY = value.CellY,
                L2ParentCellKey = value.L2ParentCellKey,
                WindowRoleCode = value.WindowRoleCode,
                Priority = value.Priority,
                ContentSourceCode = value.ContentSourceCode,
                BasePlanHashSha256 = value.BasePlanHashSha256,
                PresentationHashSha256 = value.PresentationHashSha256,
                HBindings = (value.HBindings
                             ?? Array.Empty<공간LHHBindingData>())
                    .Select(item => new SimulationLhHBindingResponse
                    {
                        HLevelCode = item.HLevelCode,
                        SpatialStableId = item.SpatialStableId,
                        StateCode = item.StateCode,
                        WorldInteractionIds = item.WorldInteractionIds
                            ?? Array.Empty<string>(),
                    }).ToArray(),
                Placements = (value.Placements
                              ?? Array.Empty<공간LHPlacementData>())
                    .Select(item => new SimulationLhPlacementResponse
                    {
                        GeneratedStableId = item.GeneratedStableId,
                        OwnerCellKey = item.OwnerCellKey,
                        LayerCode = item.LayerCode,
                        CompositionKey = item.CompositionKey,
                        H1StableId = item.H1StableId,
                        EvidenceKindCode = item.EvidenceKindCode,
                        LocalXMeters = item.LocalXMeters,
                        LocalZMeters = item.LocalZMeters,
                        RotationDegrees = item.RotationDegrees,
                        UniformScale = item.UniformScale,
                        FixedAnchor = item.FixedAnchor,
                        CollisionEligible = item.CollisionEligible,
                        PresentationOnly = item.PresentationOnly,
                    }).ToArray(),
                Connectors = (value.Connectors
                              ?? Array.Empty<공간LHConnectorData>())
                    .Select(item => new SimulationLhConnectorResponse
                    {
                        ConnectorStableId = item.ConnectorStableId,
                        SideCode = item.SideCode,
                        NeighborCellKey = item.NeighborCellKey,
                        BoundaryHashSha256 = item.BoundaryHashSha256,
                        Passable = item.Passable,
                    }).ToArray(),
                RequiredCapabilityCodes = value.RequiredCapabilityCodes
                    ?? Array.Empty<string>(),
                PlayerTraversalRequired = value.PlayerTraversalRequired,
                PresentationOnly = value.PresentationOnly,
            };
    }
}
