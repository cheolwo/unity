using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.Perspectives;
using UnityEngine;
using UnityEngine.Networking;

namespace Ssalddel.Unity.Samples.UrbanLogisticsCenter
{
    public sealed class OperationalWorldApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 15;
    }

    public interface IRuntimeAccessTokenProvider
    {
        string GetAccessToken();
    }

    public sealed class OperationalWorldApiException : Exception
    {
        public OperationalWorldApiException(string message)
            : base(message)
        {
        }
    }

    public sealed class UnityWebRequestWorldGetClient
    {
        private readonly OperationalWorldApiOptions options;
        private readonly IRuntimeAccessTokenProvider tokenProvider;

        public UnityWebRequestWorldGetClient(
            OperationalWorldApiOptions options,
            IRuntimeAccessTokenProvider tokenProvider)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        public async Task<string?> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new OperationalWorldApiException("OperationalApiBaseUrlInvalid");
            }

            var token = tokenProvider.GetAccessToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new OperationalWorldApiException("OperationalAccessTokenMissing");
            }

            var requestUri = new Uri(baseUri, relativeRoute.TrimStart('/'));
            using (var request = UnityWebRequest.Get(requestUri))
            using (cancellationToken.Register(request.Abort))
            {
                request.timeout = Math.Max(1, options.TimeoutSeconds);
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + token.Trim());

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (allowNotFound && request.responseCode == 404)
                {
                    return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new OperationalWorldApiException(
                        "OperationalApiRequestFailed:" + request.responseCode + ":" + relativeRoute);
                }

                var json = request.downloadHandler.text;
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new OperationalWorldApiException("OperationalApiResponseEmpty:" + relativeRoute);
                }

                return json;
            }
        }
    }

    public sealed class OperationalUrbanLogisticsRoleApiClient : IRolePerspectiveApiClient
    {
        private readonly UnityWebRequestWorldGetClient getClient;

        public OperationalUrbanLogisticsRoleApiClient(UnityWebRequestWorldGetClient getClient)
        {
            this.getClient = getClient;
        }

        public async Task<RolePerspectiveApiModel> GetAsync(
            역할관점조회Request request,
            CancellationToken cancellationToken = default)
        {
            var json = await getClient.GetAsync(
                RolePerspectiveApiRoutes.DriverUrbanLogisticsCenter,
                false,
                cancellationToken);
            var wire = JsonUtility.FromJson<RolePerspectiveWireModel>(json);
            return wire?.ToApiModel()
                ?? throw new OperationalWorldApiException("RolePerspectiveJsonInvalid");
        }
    }

    public sealed class OperationalUrbanLogisticsNpcApiClient : INpcMovementApiClient
    {
        private readonly UnityWebRequestWorldGetClient getClient;

        public OperationalUrbanLogisticsNpcApiClient(UnityWebRequestWorldGetClient getClient)
        {
            this.getClient = getClient;
        }

        public async Task<NpcMovementApiModel?> GetAsync(
            NpcMovementQuery query,
            CancellationToken cancellationToken = default)
        {
            var json = await getClient.GetAsync(
                NpcMovementApiRoutes.DriverUrbanLogisticsCenter,
                true,
                cancellationToken);
            return json == null ? null : ParseMovement(json);
        }

        internal static NpcMovementApiModel ParseMovement(string json)
        {
            var wire = JsonUtility.FromJson<NpcMovementWireModel>(json);
            return wire?.ToApiModel()
                ?? throw new OperationalWorldApiException("NpcMovementJsonInvalid");
        }
    }

    public sealed class OperationalCargoWarehouseHandoffApiClient
        : ICargoWarehouseHandoffApiClient
    {
        private readonly UnityWebRequestWorldGetClient getClient;

        public OperationalCargoWarehouseHandoffApiClient(UnityWebRequestWorldGetClient getClient)
        {
            this.getClient = getClient;
        }

        public async Task<CargoWarehouseHandoffApiModel?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var json = await getClient.GetAsync(
                CargoWarehouseHandoffApiRoutes.DriverWarehouseHandoff,
                true,
                cancellationToken);
            if (json == null)
            {
                return null;
            }

            var wire = JsonUtility.FromJson<CargoWarehouseHandoffWireModel>(json);
            return wire?.ToApiModel()
                ?? throw new OperationalWorldApiException("CargoWarehouseHandoffJsonInvalid");
        }
    }

    [Serializable]
    internal sealed class RolePerspectiveWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string authorizedRoleCode = string.Empty;
        public string worldZoneCode = string.Empty;
        public string viewerScopeCode = string.Empty;
        public string sourceTypeCode = string.Empty;
        public string authorizationDecisionId = string.Empty;
        public string generatedAt = string.Empty;
        public RoleObjectEmphasisWireModel[] objectEmphases = Array.Empty<RoleObjectEmphasisWireModel>();
        public RoleAllowedInteractionWireModel[] allowedInteractions = Array.Empty<RoleAllowedInteractionWireModel>();

        public RolePerspectiveApiModel ToApiModel()
        {
            return new RolePerspectiveApiModel
            {
                StableId = stableId,
                Revision = revision,
                AuthorizedRoleCode = authorizedRoleCode,
                WorldZoneCode = worldZoneCode,
                ViewerScopeCode = viewerScopeCode,
                SourceTypeCode = sourceTypeCode,
                AuthorizationDecisionId = authorizationDecisionId,
                GeneratedAt = WireDateTime.Parse(generatedAt),
                ObjectEmphases = Array.ConvertAll(objectEmphases, item => item.ToApiModel()),
                AllowedInteractions = Array.ConvertAll(allowedInteractions, item => item.ToApiModel()),
            };
        }
    }

    [Serializable]
    internal sealed class RoleObjectEmphasisWireModel
    {
        public string targetStableId = string.Empty;
        public string emphasisCode = string.Empty;
        public string label = string.Empty;
        public string detailPanelCode = string.Empty;

        public RoleObjectEmphasisApiModel ToApiModel() => new RoleObjectEmphasisApiModel
        {
            TargetStableId = targetStableId,
            EmphasisCode = emphasisCode,
            Label = label,
            DetailPanelCode = detailPanelCode,
        };
    }

    [Serializable]
    internal sealed class RoleAllowedInteractionWireModel
    {
        public string interactionCode = string.Empty;
        public string targetStableId = string.Empty;
        public string effectCode = string.Empty;
        public bool requiresExplicitConfirmation;
        public bool requiresCanonicalStateRefresh;

        public RoleAllowedInteractionApiModel ToApiModel() => new RoleAllowedInteractionApiModel
        {
            InteractionCode = interactionCode,
            TargetStableId = targetStableId,
            EffectCode = effectCode,
            RequiresExplicitConfirmation = requiresExplicitConfirmation,
            RequiresCanonicalStateRefresh = requiresCanonicalStateRefresh,
        };
    }

    [Serializable]
    internal sealed class NpcMovementWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string npcStableId = string.Empty;
        public string actorRoleCode = string.Empty;
        public string worldZoneCode = string.Empty;
        public string routeCode = string.Empty;
        public string currentWaypointKey = string.Empty;
        public string destinationWaypointKey = string.Empty;
        public string movementStateCode = string.Empty;
        public string arrivalActionCode = string.Empty;
        public string sourceTypeCode = string.Empty;
        public string canonicalTaskStableId = string.Empty;
        public string generatedAt = string.Empty;

        public NpcMovementApiModel ToApiModel() => new NpcMovementApiModel
        {
            StableId = stableId,
            Revision = revision,
            NpcStableId = npcStableId,
            ActorRoleCode = actorRoleCode,
            WorldZoneCode = worldZoneCode,
            RouteCode = routeCode,
            CurrentWaypointKey = currentWaypointKey,
            DestinationWaypointKey = destinationWaypointKey,
            MovementStateCode = movementStateCode,
            ArrivalActionCode = arrivalActionCode,
            SourceTypeCode = sourceTypeCode,
            CanonicalTaskStableId = canonicalTaskStableId,
            GeneratedAt = WireDateTime.Parse(generatedAt),
        };
    }

    [Serializable]
    internal sealed class CargoWarehouseHandoffWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string handoffStateCode = string.Empty;
        public string cargoStableId = string.Empty;
        public string transportTaskStableId = string.Empty;
        public string inboundTaskStableId = string.Empty;
        public NpcMovementWireModel[] movements = Array.Empty<NpcMovementWireModel>();
        public string generatedAt = string.Empty;

        public CargoWarehouseHandoffApiModel ToApiModel() => new CargoWarehouseHandoffApiModel
        {
            StableId = stableId,
            Revision = revision,
            HandoffStateCode = handoffStateCode,
            CargoStableId = cargoStableId,
            TransportTaskStableId = transportTaskStableId,
            InboundTaskStableId = inboundTaskStableId,
            Movements = Array.ConvertAll(movements, item => item.ToApiModel()),
            GeneratedAt = WireDateTime.Parse(generatedAt),
        };
    }

    internal static class WireDateTime
    {
        public static DateTimeOffset Parse(string value)
        {
            if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var result))
            {
                throw new OperationalWorldApiException("OperationalApiGeneratedAtInvalid");
            }

            return result;
        }
    }
}
