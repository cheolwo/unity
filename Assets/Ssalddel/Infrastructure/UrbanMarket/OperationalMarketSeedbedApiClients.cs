using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.UrbanMarket;

namespace Ssalddel.Unity.Infrastructure.UrbanMarket
{
    public sealed class OperationalMarketProductApiClient : I도심마트ApiClient
    {
        private readonly IOperationalUnityApiClient apiClient;

        public OperationalMarketProductApiClient(IOperationalUnityApiClient client)
        {
            apiClient = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<도심마트목록ApiModel> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = MarketProductBusinessApiRoutes.PublicProducts,
                RequiresAuthentication = false,
            }, cancellationToken);
            EnsureSuccess(response, "MarketPublicProductsRequestFailed");

            var wire = JsonConvert.DeserializeObject<MarketProductListWire>(response.Body)
                ?? throw new InvalidOperationException("MarketPublicProductsJsonInvalid");
            if (wire.Items == null)
            {
                throw new InvalidOperationException("MarketPublicProductsItemsMissing");
            }

            var items = new 도심마트상품ApiModel[wire.Items.Length];
            for (var index = 0; index < items.Length; index++)
            {
                items[index] = wire.Items[index].ToApiModel();
            }

            return new 도심마트목록ApiModel
            {
                Items = items,
                TotalCount = wire.TotalCount,
                재고기준안내 = wire.재고기준안내,
            };
        }

        private sealed class MarketProductListWire
        {
            [JsonProperty("items")]
            public MarketProductWire[] Items { get; set; } = Array.Empty<MarketProductWire>();

            [JsonProperty("totalCount")]
            public int TotalCount { get; set; }

            [JsonProperty("재고기준안내")]
            public string 재고기준안내 { get; set; } = string.Empty;
        }

        private sealed class MarketProductWire
        {
            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("상품명")]
            public string 상품명 { get; set; } = string.Empty;

            [JsonProperty("판매단위")]
            public string 판매단위 { get; set; } = string.Empty;

            [JsonProperty("판매가")]
            public decimal 판매가 { get; set; }

            [JsonProperty("판매가능수량")]
            public int 판매가능수량 { get; set; }

            [JsonProperty("판매가능여부")]
            public bool 판매가능여부 { get; set; }

            [JsonProperty("재고기준시각Utc")]
            public string 재고기준시각Utc { get; set; } = string.Empty;

            [JsonProperty("수정일시Utc")]
            public string 수정일시Utc { get; set; } = string.Empty;

            public 도심마트상품ApiModel ToApiModel()
            {
                if (Id <= 0)
                {
                    throw new InvalidOperationException("MarketPublicProductIdentityInvalid");
                }

                return new 도심마트상품ApiModel
                {
                    Id = Id,
                    상품명 = 상품명,
                    판매단위 = 판매단위,
                    판매가 = 판매가,
                    판매가능수량 = 판매가능수량,
                    판매가능여부 = 판매가능여부,
                    재고기준시각 = ParseTimestamp(재고기준시각Utc),
                    수정시각 = ParseTimestamp(수정일시Utc),
                };
            }
        }

        private static DateTimeOffset ParseTimestamp(string value)
            => DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : throw new InvalidOperationException("MarketPublicProductTimestampInvalid");

        internal static void EnsureSuccess(UnityApiResponse response, string errorCode)
        {
            if (response == null)
            {
                throw new InvalidOperationException(errorCode + ":NoResponse");
            }

            if (!response.IsSuccess)
            {
                throw new InvalidOperationException(
                    errorCode + ":" + response.StatusCode + ":" + response.ErrorCode);
            }

            if (string.IsNullOrWhiteSpace(response.Body))
            {
                throw new InvalidOperationException(errorCode + ":EmptyBody");
            }
        }
    }

    public sealed class OperationalMarketOrderIntentApiClient : IMarketOrderIntentApiClient
    {
        private readonly IOperationalUnityApiClient apiClient;

        public OperationalMarketOrderIntentApiClient(IOperationalUnityApiClient client)
        {
            apiClient = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<MarketOrderIntentResponseApiModel> 등록Async(
            MarketOrderIntentCommandApiModel command,
            CancellationToken cancellationToken = default)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "POST",
                RelativePath = MarketProductBusinessApiRoutes.OrderRequests,
                JsonBody = JsonConvert.SerializeObject(command),
                RequiresAuthentication = true,
            }, cancellationToken);

            return Parse(response, "MarketOrderIntentCreateRequestFailed");
        }

        public async Task<MarketOrderIntentResponseApiModel> 상세조회Async(
            Guid orderRequestId,
            CancellationToken cancellationToken = default)
        {
            if (orderRequestId == Guid.Empty)
            {
                throw new ArgumentException("주문 의향 ID가 필요합니다.", nameof(orderRequestId));
            }

            var path = MarketProductBusinessApiRoutes.OrderRequestDetail
                .Replace("{orderRequestId}", orderRequestId.ToString("D"));
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = path,
                RequiresAuthentication = true,
            }, cancellationToken);

            return Parse(response, "MarketOrderIntentDetailRequestFailed");
        }

        private static MarketOrderIntentResponseApiModel Parse(
            UnityApiResponse response,
            string errorCode)
        {
            OperationalMarketProductApiClient.EnsureSuccess(response, errorCode);
            return JsonConvert.DeserializeObject<MarketOrderIntentResponseApiModel>(response.Body)
                ?? throw new InvalidOperationException(errorCode + ":JsonInvalid");
        }
    }
}
