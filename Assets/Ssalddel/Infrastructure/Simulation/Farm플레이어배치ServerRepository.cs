using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public sealed class Farm플레이어배치ServerRepository
        : IFarm플레이어배치AuthorityClient
    {
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public Farm플레이어배치ServerRepository(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<Farm배치ContextData> RefreshAsync(string sessionStableId,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", SessionRoute(sessionStableId),
                string.Empty, cancellationToken);
            return MapContext(response.Body, sessionStableId);
        }

        public async Task<Farm배치PreviewData> PreviewAsync(string sessionStableId,
            long expectedRevision, Farm배치CandidateData candidate,
            CancellationToken cancellationToken)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            var response = await SendAsync("POST", PlacementRoute(sessionStableId)
                + "/previews", JsonConvert.SerializeObject(new
                {
                    ExpectedRevision = expectedRevision,
                    candidate.BlueprintStableId,
                    candidate.PlacementZoneStableId,
                    candidate.TargetH2StableId,
                    candidate.LocalXCentimeters,
                    candidate.LocalZCentimeters,
                    candidate.RotationQuarterTurns,
                    candidate.AccessConnectorStableId,
                    candidate.FenceChainStableId,
                }), cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException("FarmPlacementPreviewJsonInvalid");
            return new Farm배치PreviewData
            {
                PlacementProposalStableId = wire.PlacementProposalStableId
                    ?? string.Empty,
                SourceWorldRevision = wire.SourceWorldRevision,
                CanConfirm = wire.CanConfirm,
                BlockingReasonCodes = wire.BlockingReasonCodes
                    ?? Array.Empty<string>(),
                NormalizedCandidate = new Farm배치CandidateData
                {
                    BlueprintStableId = wire.BlueprintStableId ?? string.Empty,
                    PlacementZoneStableId = wire.PlacementZoneStableId
                        ?? string.Empty,
                    TargetH2StableId = wire.TargetH2StableId ?? string.Empty,
                    LocalXCentimeters = wire.LocalXCentimeters,
                    LocalZCentimeters = wire.LocalZCentimeters,
                    RotationQuarterTurns = wire.RotationQuarterTurns,
                    AccessConnectorStableId = wire.AccessConnectorStableId
                        ?? string.Empty,
                    FenceChainStableId = wire.FenceChainStableId ?? string.Empty,
                },
                PlacementKindCode = wire.PlacementKindCode ?? string.Empty,
                PlacementProfileRevision = wire.PlacementProfileRevision
                    ?? string.Empty,
                FootprintWidthCentimeters = wire.FootprintWidthCentimeters,
                FootprintDepthCentimeters = wire.FootprintDepthCentimeters,
                MaterialRequirements = (wire.MaterialRequirements
                    ?? Array.Empty<MaterialWire>()).Select(value =>
                    new Farm배치MaterialRequirementData
                    {
                        ItemCode = value.ItemCode ?? string.Empty,
                        Quantity = value.Quantity,
                        UnitCode = value.UnitCode ?? string.Empty,
                    }).ToArray(),
                ReservedMaterialLotStableIds = wire.ReservedMaterialLotStableIds
                    ?? Array.Empty<string>(),
                ConstructionTicks = wire.ConstructionTicks,
                PreviewHashSha256 = wire.PreviewHashSha256 ?? string.Empty,
            };
        }

        public async Task<Farm배치ContextData> ConfirmAsync(string sessionStableId,
            long expectedRevision, string commandId,
            string placementProposalStableId, string previewHashSha256,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", PlacementRoute(sessionStableId) + "/commands",
                JsonConvert.SerializeObject(new
                {
                    CommandId = commandId,
                    ExpectedRevision = expectedRevision,
                    PlacementProposalStableId = placementProposalStableId,
                    PreviewHashSha256 = previewHashSha256,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<Farm배치ContextData> AdvanceOneTickAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", SessionRoute(sessionStableId) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.farm-placement.tick:"
                        + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = 1,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        private static Farm배치ContextData MapContext(string json,
            string expectedSessionStableId)
        {
            var session = JsonConvert.DeserializeObject<SessionWire>(json)
                ?? throw new InvalidOperationException("FarmPlacementSessionJsonInvalid");
            var world = session.IntegratedWorld
                ?? throw new InvalidOperationException("FarmPlacementWorldMissing");
            var facilities = world.Facilities ?? Array.Empty<FacilityWire>();
            var objects = (world.ConstructionProjects
                    ?? Array.Empty<ConstructionProjectWire>())
                .Where(value => !string.IsNullOrWhiteSpace(
                    value.PlacementZoneStableId))
                .OrderBy(value => value.ConstructionProjectStableId,
                    StringComparer.Ordinal)
                .Select(project =>
                {
                    var facility = facilities.SingleOrDefault(value =>
                        value.FacilityStableId == project.TargetFacilityStableId);
                    return new Farm배치ObjectData
                    {
                        FacilityStableId = project.TargetFacilityStableId
                            ?? string.Empty,
                        BlueprintStableId = project.BlueprintStableId ?? string.Empty,
                        LifecycleCode = facility?.LifecycleCode
                            ?? project.StateCode ?? string.Empty,
                        PlacementZoneStableId = project.PlacementZoneStableId
                            ?? string.Empty,
                        TargetH2StableId = project.TargetH2StableId ?? string.Empty,
                        PlacementKindCode = project.PlacementKindCode ?? string.Empty,
                        LocalXCentimeters = project.LocalXCentimeters,
                        LocalZCentimeters = project.LocalZCentimeters,
                        RotationQuarterTurns = project.RotationQuarterTurns,
                        PlacementProfileRevision = project.PlacementProfileRevision
                            ?? string.Empty,
                        FenceChainStableId = project.FenceChainStableId
                            ?? string.Empty,
                    };
                }).ToArray();
            return new Farm배치ContextData
            {
                SessionStableId = session.SessionStableId
                    ?? expectedSessionStableId,
                WorldRevision = session.Revision,
                WorldTick = session.WorldContext?.WorldTick ?? 0,
                Objects = objects,
            };
        }

        private async Task<UnityApiResponse> SendAsync(string method, string route,
            string body, CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route,
                JsonBody = body,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("FarmPlacementServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string PlacementRoute(string sessionStableId)
            => SessionRoute(sessionStableId)
               + "/integrated-world/farm-construction-placement";

        private static string SessionRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("FarmPlacementSessionMissing");
            return "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId.Trim());
        }

        [Serializable]
        private sealed class PreviewWire
        {
            public string PlacementProposalStableId;
            public long SourceWorldRevision;
            public bool CanConfirm;
            public string[] BlockingReasonCodes;
            public string BlueprintStableId;
            public string PlacementKindCode;
            public string PlacementZoneStableId;
            public string TargetH2StableId;
            public int LocalXCentimeters;
            public int LocalZCentimeters;
            public int RotationQuarterTurns;
            public int FootprintWidthCentimeters;
            public int FootprintDepthCentimeters;
            public string AccessConnectorStableId;
            public string FenceChainStableId;
            public string PlacementProfileRevision;
            public MaterialWire[] MaterialRequirements;
            public string[] ReservedMaterialLotStableIds;
            public int ConstructionTicks;
            public string PreviewHashSha256;
        }

        [Serializable]
        private sealed class MaterialWire
        {
            public string ItemCode;
            public decimal Quantity;
            public string UnitCode;
        }

        [Serializable]
        private sealed class SessionWire
        {
            public string SessionStableId;
            public long Revision;
            public WorldWire WorldContext;
            public IntegratedWorldWire IntegratedWorld;
        }

        [Serializable] private sealed class WorldWire { public int WorldTick; }
        [Serializable] private sealed class IntegratedWorldWire { public FacilityWire[] Facilities; public ConstructionProjectWire[] ConstructionProjects; }
        [Serializable] private sealed class FacilityWire { public string FacilityStableId; public string LifecycleCode; }

        [Serializable]
        private sealed class ConstructionProjectWire
        {
            public string ConstructionProjectStableId;
            public string BlueprintStableId;
            public string StateCode;
            public string TargetFacilityStableId;
            public string PlacementZoneStableId;
            public string TargetH2StableId;
            public string PlacementKindCode;
            public int LocalXCentimeters;
            public int LocalZCentimeters;
            public int RotationQuarterTurns;
            public string PlacementProfileRevision;
            public string FenceChainStableId;
        }
    }
}
