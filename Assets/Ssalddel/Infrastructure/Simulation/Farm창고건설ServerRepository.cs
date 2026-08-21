using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    public sealed class Farm창고건설ServerRepository
        : IFarm창고건설AuthorityClient
    {
        private const string ActionCode = "WI-CON-01";
        private const string ManufacturingActionCode = "WI-MFG-01";
        private const string ComponentRecipeStableId =
            "recipe:farm-small-storage-components.v1";
        private const string HubWorkshopStableId = "facility:unity:hub-workshop";
        private readonly ISimulationRehearsalUnityApiClient apiClient;

        public Farm창고건설ServerRepository(
            ISimulationRehearsalUnityApiClient client)
            => apiClient = client ?? throw new ArgumentNullException(nameof(client));

        public async Task<Farm창고건설ContextData> RefreshAsync(
            string sessionStableId, CancellationToken cancellationToken)
        {
            var response = await SendAsync("GET", SessionRoute(sessionStableId),
                string.Empty, cancellationToken);
            return ParseContext(response.Body, sessionStableId);
        }

        public async Task<Farm창고건설PreviewData> PreviewAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync("POST",
                IntegratedWorldRoute(sessionStableId) + "/previews",
                CommandJson(expectedRevision), cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException("FarmStoragePreviewJsonInvalid");
            return new Farm창고건설PreviewData
            {
                SourceWorldRevision = wire.SourceWorldRevision,
                CanConfirm = wire.CanConfirm,
                BlockingReasonCodes = wire.BlockingReasonCodes
                    ?? Array.Empty<string>(),
                ReservedLotStableIds = wire.ReservedLotStableIds
                    ?? Array.Empty<string>(),
                PreviewHashSha256 = wire.PreviewHashSha256 ?? string.Empty,
            };
        }

        public async Task<Farm창고건설PreviewData> PreviewResourceAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            var response = await SendAsync("POST",
                IntegratedWorldRoute(sessionStableId) + "/previews",
                ResourceCommandJson(expectedRevision), cancellationToken);
            var wire = JsonConvert.DeserializeObject<PreviewWire>(response.Body)
                ?? throw new InvalidOperationException(
                    "FarmStorageResourcePreviewJsonInvalid");
            return new Farm창고건설PreviewData
            {
                SourceWorldRevision = wire.SourceWorldRevision,
                CanConfirm = wire.CanConfirm,
                BlockingReasonCodes = wire.BlockingReasonCodes
                    ?? Array.Empty<string>(),
                ReservedLotStableIds = wire.ReservedLotStableIds
                    ?? Array.Empty<string>(),
                PreviewHashSha256 = wire.PreviewHashSha256 ?? string.Empty,
            };
        }

        public async Task<Farm창고건설ContextData> ConfirmResourceAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST",
                IntegratedWorldRoute(sessionStableId) + "/commands",
                ResourceCommandJson(expectedRevision), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<Farm창고건설ContextData> ConfirmAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST",
                IntegratedWorldRoute(sessionStableId) + "/commands",
                CommandJson(expectedRevision), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<Farm창고건설ContextData> AdvanceOneTickAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            await SendAsync("POST", SessionRoute(sessionStableId) + "/ticks",
                JsonConvert.SerializeObject(new
                {
                    CommandId = "command:unity.farm-small-storage.tick:"
                        + expectedRevision,
                    ExpectedRevision = expectedRevision,
                    TickCount = 1,
                }), cancellationToken);
            return await RefreshAsync(sessionStableId, cancellationToken);
        }

        public async Task<FarmGameplaySaveReloadData> SaveAndReloadAsync(
            string sessionStableId, long expectedRevision,
            CancellationToken cancellationToken)
        {
            var saveStableId = "save:unity.farm-g4:"
                + expectedRevision;
            var saveResponse = await SendAsync("POST",
                SessionRoute(sessionStableId) + "/saves",
                JsonConvert.SerializeObject(new
                {
                    SaveStableId = saveStableId,
                    ExpectedRevision = expectedRevision,
                }), cancellationToken);
            var save = JsonConvert.DeserializeObject<SaveWire>(saveResponse.Body)
                ?? throw new InvalidOperationException("FarmGameplaySaveJsonInvalid");
            var restoreResponse = await SendAsync("POST",
                "api/simulation/v1/sessions/replay-verifications",
                JsonConvert.SerializeObject(new
                {
                    SaveStableId = saveStableId,
                }), cancellationToken);
            var restore = JsonConvert.DeserializeObject<RestoreWire>(
                    restoreResponse.Body)
                ?? throw new InvalidOperationException("FarmGameplayRestoreJsonInvalid");
            if (save.SaveStableId != saveStableId
                || restore.SaveStableId != saveStableId
                || save.ReplayHash != restore.ReplayHash
                || restore.Session == null)
                throw new InvalidOperationException("FarmGameplayReplayHashMismatch");
            return new FarmGameplaySaveReloadData
            {
                SaveStableId = saveStableId,
                SchemaVersion = restore.SchemaVersion ?? string.Empty,
                ReplayHash = restore.ReplayHash ?? string.Empty,
                ReplayedCommandCount = restore.ReplayedCommandCount,
                Restored = MapContext(restore.Session, sessionStableId),
            };
        }

        private static string CommandJson(long expectedRevision)
            => JsonConvert.SerializeObject(new
            {
                ActionCode,
                CommandId = "command:unity.farm-small-storage.build:"
                    + expectedRevision,
                ExpectedRevision = expectedRevision,
                Construction = new
                {
                    Farm창고건설Codes.BlueprintStableId,
                    Farm창고건설Codes.BuildSiteH1StableId,
                },
            });

        private static string ResourceCommandJson(long expectedRevision)
            => JsonConvert.SerializeObject(new
            {
                ActionCode = ManufacturingActionCode,
                CommandId = "command:unity.farm-small-storage.components:"
                    + expectedRevision,
                ExpectedRevision = expectedRevision,
                Manufacturing = new
                {
                    RecipeStableId = ComponentRecipeStableId,
                    PreferredManufacturingFacilityStableId = HubWorkshopStableId,
                },
            });

        private static Farm창고건설ContextData ParseContext(
            string json, string expectedSessionStableId)
        {
            var wire = JsonConvert.DeserializeObject<SessionWire>(json)
                ?? throw new InvalidOperationException("FarmStorageSessionJsonInvalid");
            return MapContext(wire, expectedSessionStableId);
        }

        private static Farm창고건설ContextData MapContext(
            SessionWire wire, string expectedSessionStableId)
        {
            var settlement = wire.Settlement
                ?? throw new InvalidOperationException("FarmStorageSettlementMissing");
            var world = wire.IntegratedWorld
                ?? throw new InvalidOperationException("FarmStorageIntegratedWorldMissing");
            var project = (world.ConstructionProjects
                    ?? Array.Empty<ConstructionProjectWire>())
                .Where(value => value.BlueprintStableId
                    == Farm창고건설Codes.BlueprintStableId)
                .OrderBy(value => value.ConstructionProjectStableId,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            var facility = project == null ? null
                : (world.Facilities ?? Array.Empty<FacilityWire>())
                    .SingleOrDefault(value => value.FacilityStableId
                        == project.TargetFacilityStableId);
            var jobs = world.ManufacturingJobs
                ?? Array.Empty<ManufacturingJobWire>();
            var componentQuantity = (world.Lots ?? Array.Empty<LotWire>())
                .Where(value => value.ItemCode == "FacilityComponent")
                .Sum(value => value.Quantity);
            var resourceState = componentQuantity >= 2m
                ? Farm창고건설Codes.ResourceReady
                : jobs.Any(value => value.RecipeStableId == ComponentRecipeStableId
                    && value.StateCode != "Completed"
                    && value.StateCode != "Cancelled")
                    ? Farm창고건설Codes.ResourceProducing
                    : (world.Lots ?? Array.Empty<LotWire>()).Any(value =>
                        value.ItemCode == "ManufacturingRawMaterial"
                        && value.Quantity >= 2m)
                        ? Farm창고건설Codes.ResourceAvailable
                        : Farm창고건설Codes.ResourceUnavailable;
            var state = facility?.LifecycleCode switch
            {
                "Operational" => Farm창고건설Codes.Operational,
                "UnderConstruction" => Farm창고건설Codes.Building,
                "Planned" => Farm창고건설Codes.Planned,
                _ => Farm창고건설Codes.NotStarted,
            };
            return new Farm창고건설ContextData
            {
                SessionStableId = wire.SessionStableId ?? expectedSessionStableId,
                WorldRevision = wire.Revision,
                WorldTick = wire.WorldContext?.WorldTick ?? 0,
                StateCode = state,
                FacilityStableId = facility?.FacilityStableId ?? string.Empty,
                ResourceStateCode = resourceState,
                FacilityComponentQuantity = componentQuantity,
                StorageCapacity = settlement.StorageCapacity,
                StorageOccupied = settlement.StorageOccupied,
                StorageReserved = settlement.StorageReserved,
                StorageAvailable = settlement.StorageAvailable,
                StorageUnitCode = settlement.StorageUnitCode ?? string.Empty,
            };
        }

        private async Task<UnityApiResponse> SendAsync(
            string method, string route, string jsonBody,
            CancellationToken cancellationToken)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = method,
                RelativePath = route,
                JsonBody = jsonBody,
                RequiresAuthentication = false,
            }, cancellationToken);
            if (!response.IsSuccess)
                throw new InvalidOperationException("FarmStorageServerRequestFailed:"
                    + response.StatusCode + ":" + response.ErrorCode);
            return response;
        }

        private static string IntegratedWorldRoute(string sessionStableId)
            => SessionRoute(sessionStableId) + "/integrated-world";

        private static string SessionRoute(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new InvalidOperationException("FarmStorageSessionMissing");
            return "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId.Trim());
        }

        [Serializable] private sealed class PreviewWire { public long SourceWorldRevision; public bool CanConfirm; public string[] BlockingReasonCodes; public string[] ReservedLotStableIds; public string PreviewHashSha256; }
        [Serializable] private sealed class SessionWire { public string SessionStableId; public long Revision; public WorldWire WorldContext; public SettlementWire Settlement; public IntegratedWorldWire IntegratedWorld; }
        [Serializable] private sealed class WorldWire { public int WorldTick; }
        [Serializable] private sealed class SettlementWire { public decimal StorageCapacity; public decimal StorageOccupied; public decimal StorageReserved; public decimal StorageAvailable; public string StorageUnitCode; }
        [Serializable] private sealed class IntegratedWorldWire { public FacilityWire[] Facilities; public ConstructionProjectWire[] ConstructionProjects; public ManufacturingJobWire[] ManufacturingJobs; public LotWire[] Lots; }
        [Serializable] private sealed class FacilityWire { public string FacilityStableId; public string LifecycleCode; }
        [Serializable] private sealed class ConstructionProjectWire { public string ConstructionProjectStableId; public string BlueprintStableId; public string TargetFacilityStableId; }
        [Serializable] private sealed class ManufacturingJobWire { public string RecipeStableId; public string StateCode; }
        [Serializable] private sealed class LotWire { public string ItemCode; public decimal Quantity; }
        [Serializable] private sealed class SaveWire { public string SaveStableId; public string SchemaVersion; public string ReplayHash; public long SavedWorldRevision; public int SavedWorldTick; }
        [Serializable] private sealed class RestoreWire { public string SaveStableId; public string SchemaVersion; public string ReplayHash; public int ReplayedCommandCount; public SessionWire Session; }
    }
}
