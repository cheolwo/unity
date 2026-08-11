using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.PotatoJourney;
using UnityEngine.Networking;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration
{
    [Serializable]
    public sealed class PotatoJourneyOperationalApiOptions
    {
        public string BaseUrl = string.Empty;
        public int TimeoutSeconds = 15;
    }

    public interface IPotatoJourneyAccessTokenProvider
    {
        string GetAccessToken();
    }

    public sealed class PotatoJourneyOperationalApiException : Exception
    {
        public PotatoJourneyOperationalApiException(string message) : base(message) { }
    }

    public sealed class OperationalPotatoJourneyApiClient : IPotatoJourneyApiClient
    {
        private readonly PotatoJourneyOperationalApiOptions options;
        private readonly IPotatoJourneyAccessTokenProvider tokenProvider;

        public OperationalPotatoJourneyApiClient(
            PotatoJourneyOperationalApiOptions apiOptions,
            IPotatoJourneyAccessTokenProvider accessTokenProvider)
        {
            options = apiOptions ?? throw new ArgumentNullException(nameof(apiOptions));
            tokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        }

        public async Task<PotatoJourneyApiModel> GetAsync(
            string? cultivationStableId,
            CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
                throw new PotatoJourneyOperationalApiException("PotatoJourneyApiBaseUrlInvalid");
            var token = tokenProvider.GetAccessToken();
            if (string.IsNullOrWhiteSpace(token))
                throw new PotatoJourneyOperationalApiException("PotatoJourneyAccessTokenMissing");
            var route = PotatoJourneyApiRequestBuilder.Build(cultivationStableId);
            using (var request = UnityWebRequest.Get(new Uri(baseUri, route)))
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
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var code = request.responseCode == 401 || request.responseCode == 403
                        ? "PotatoJourneyHttpUnauthorized"
                        : request.responseCode == 404
                            ? "PotatoJourneyHttpNotFound"
                            : "PotatoJourneyHttpRequestFailed";
                    throw new PotatoJourneyOperationalApiException(code + ":" + request.responseCode);
                }

                return ParseJson(request.downloadHandler.text);
            }
        }

        public static PotatoJourneyApiModel ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new PotatoJourneyOperationalApiException("PotatoJourneyHttpResponseEmpty");
            var wire = JsonConvert.DeserializeObject<PotatoJourneyWireModel>(json);
            return wire?.ToApiModel()
                   ?? throw new PotatoJourneyOperationalApiException("PotatoJourneyWireJsonInvalid");
        }
    }

    [Serializable]
    internal sealed class PotatoJourneyWireModel
    {
        public string stableId = string.Empty;
        public string revision = string.Empty;
        public string generatedAt = string.Empty;
        public string authorizedRoleCode = string.Empty;
        public string viewerScopeCode = string.Empty;
        public string authorizationDecisionId = string.Empty;
        public string sourceModeCode = string.Empty;
        public string linkageStatusCode = string.Empty;
        public PotatoProductWireModel product = new PotatoProductWireModel();
        public PotatoCultivationWireModel? farm;
        public PotatoPriceObservationWireModel domesticPrice = new PotatoPriceObservationWireModel();
        public PotatoCargoWireModel? cargoJourney;
        public PotatoWarehouseWireModel? warehouse;
        public PotatoMarketWireModel? market;
        public PotatoJourneySourceLineageWireModel[] sourceLineage = Array.Empty<PotatoJourneySourceLineageWireModel>();
        public string[] limitations = Array.Empty<string>();
        public bool isReadOnly;

        public PotatoJourneyApiModel ToApiModel() => new PotatoJourneyApiModel
        {
            StableId = stableId,
            Revision = revision,
            GeneratedAt = WireTime.Required(generatedAt),
            AuthorizedRoleCode = authorizedRoleCode,
            ViewerScopeCode = viewerScopeCode,
            AuthorizationDecisionId = authorizationDecisionId,
            SourceModeCode = sourceModeCode,
            LinkageStatusCode = linkageStatusCode,
            Product = product.ToApiModel(),
            Farm = farm != null && farm.HasValue() ? farm.ToApiModel() : null,
            DomesticPrice = domesticPrice.ToApiModel(),
            CargoJourney = cargoJourney != null && cargoJourney.HasValue() ? cargoJourney.ToApiModel() : null,
            Warehouse = warehouse != null && warehouse.HasValue() ? warehouse.ToApiModel() : null,
            Market = market != null && market.HasValue() ? market.ToApiModel() : null,
            SourceLineage = Array.ConvertAll(sourceLineage ?? Array.Empty<PotatoJourneySourceLineageWireModel>(),
                item => item.ToApiModel()),
            Limitations = limitations ?? Array.Empty<string>(),
            IsReadOnly = isReadOnly,
        };
    }

    [Serializable]
    internal sealed class PotatoJourneySourceLineageWireModel
    {
        public string sourceKey = string.Empty;
        public string sourceStableId = string.Empty;
        public string sourceRevision = string.Empty;
        public string observedAt = string.Empty;
        public string sourceModeCode = string.Empty;
        public PotatoJourneySourceLineageApiModel ToApiModel() => new PotatoJourneySourceLineageApiModel
        {
            SourceKey = sourceKey,
            SourceStableId = sourceStableId,
            SourceRevision = sourceRevision,
            ObservedAt = WireTime.Optional(observedAt),
            SourceModeCode = sourceModeCode,
        };
    }

    [Serializable]
    internal sealed class PotatoProductWireModel
    {
        public string productStableId = string.Empty;
        public string displayName = string.Empty;
        public string hsPrefix = string.Empty;
        public string mappingQualityCode = string.Empty;
        public string mappingQualityLabel = string.Empty;
        public string mappingEvidence = string.Empty;
        public bool informationOnly;
        public PotatoProductApiModel ToApiModel() => new PotatoProductApiModel
        {
            ProductStableId = productStableId, DisplayName = displayName, HsPrefix = hsPrefix,
            MappingQualityCode = mappingQualityCode, MappingQualityLabel = mappingQualityLabel,
            MappingEvidence = mappingEvidence, InformationOnly = informationOnly,
        };
    }

    [Serializable]
    internal sealed class PotatoPriceRangeWireModel
    {
        public string marketStageCode = string.Empty;
        public string marketStageLabel = string.Empty;
        public double averageKrwPerKg;
        public double minimumKrwPerKg;
        public double maximumKrwPerKg;
        public int sampleCount;
        public string latestSurveyDate = string.Empty;
        public bool HasValue() => !string.IsNullOrWhiteSpace(marketStageCode);
        public PotatoPriceRangeApiModel ToApiModel() => new PotatoPriceRangeApiModel
        {
            MarketStageCode = marketStageCode, MarketStageLabel = marketStageLabel,
            AverageKrwPerKg = (decimal)averageKrwPerKg, MinimumKrwPerKg = (decimal)minimumKrwPerKg,
            MaximumKrwPerKg = (decimal)maximumKrwPerKg, SampleCount = sampleCount,
            LatestSurveyDate = latestSurveyDate,
        };
    }

    [Serializable]
    internal sealed class PotatoPriceObservationWireModel
    {
        public string statusCode = string.Empty;
        public string hsCode = string.Empty;
        public string unitCode = string.Empty;
        public string currencyCode = string.Empty;
        public string dataSource = string.Empty;
        public string startDate = string.Empty;
        public string endDate = string.Empty;
        public PotatoPriceRangeWireModel? wholesale;
        public PotatoPriceRangeWireModel? retail;
        public string[] notices = Array.Empty<string>();
        public bool informationOnly;
        public PotatoPriceObservationApiModel ToApiModel() => new PotatoPriceObservationApiModel
        {
            StatusCode = statusCode, HsCode = hsCode, UnitCode = unitCode, CurrencyCode = currencyCode,
            DataSource = dataSource, StartDate = startDate, EndDate = endDate,
            Wholesale = wholesale != null && wholesale.HasValue() ? wholesale.ToApiModel() : null,
            Retail = retail != null && retail.HasValue() ? retail.ToApiModel() : null,
            Notices = notices ?? Array.Empty<string>(), InformationOnly = informationOnly,
        };
    }

    [Serializable]
    internal sealed class PotatoSensorObservationWireModel
    {
        public double value;
        public string unitCode = string.Empty;
        public string observedAt = string.Empty;
        public string freshnessStatusCode = string.Empty;
        public string conditionCode = string.Empty;
        public string assessmentRuleRevision = string.Empty;
        public string evidenceCardId = string.Empty;
        public string confidenceCode = string.Empty;
        public string limitation = string.Empty;
        public bool HasValue() => !string.IsNullOrWhiteSpace(observedAt);
        public PotatoSensorObservationApiModel ToApiModel() => new PotatoSensorObservationApiModel
        {
            Value = (decimal)value, UnitCode = unitCode, ObservedAt = WireTime.Required(observedAt),
            FreshnessStatusCode = freshnessStatusCode, ConditionCode = conditionCode,
            AssessmentRuleRevision = assessmentRuleRevision,
            EvidenceCardId = WireTime.NullIfEmpty(evidenceCardId),
            ConfidenceCode = WireTime.NullIfEmpty(confidenceCode),
            Limitation = WireTime.NullIfEmpty(limitation),
        };
    }

    [Serializable]
    internal sealed class PotatoSensorWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string sensorTypeCode = string.Empty;
        public string statusCode = string.Empty;
        public PotatoSensorObservationWireModel? latestObservation;
        public PotatoSensorApiModel ToApiModel() => new PotatoSensorApiModel
        {
            StableId = stableId, Revision = revision, SensorTypeCode = sensorTypeCode,
            StatusCode = statusCode,
            LatestObservation = latestObservation != null && latestObservation.HasValue()
                ? latestObservation.ToApiModel()
                : null,
        };
    }

    [Serializable]
    internal sealed class PotatoCultivationWireModel
    {
        public string farmStableId = string.Empty;
        public long farmRevision;
        public string plotStableId = string.Empty;
        public long plotRevision;
        public string cultivationStableId = string.Empty;
        public long cultivationRevision;
        public string cropName = string.Empty;
        public string cropReferenceStableId = string.Empty;
        public string cropReferenceSourceKey = string.Empty;
        public string growthStatusCode = string.Empty;
        public string plantedOn = string.Empty;
        public string expectedHarvestOn = string.Empty;
        public string productLinkageStatusCode = string.Empty;
        public PotatoSensorWireModel[] sensors = Array.Empty<PotatoSensorWireModel>();
        public bool HasValue() => !string.IsNullOrWhiteSpace(cultivationStableId);
        public PotatoCultivationApiModel ToApiModel() => new PotatoCultivationApiModel
        {
            FarmStableId = farmStableId, FarmRevision = farmRevision, PlotStableId = plotStableId,
            PlotRevision = plotRevision, CultivationStableId = cultivationStableId,
            CultivationRevision = cultivationRevision, CropName = cropName,
            CropReferenceStableId = WireTime.NullIfEmpty(cropReferenceStableId),
            CropReferenceSourceKey = WireTime.NullIfEmpty(cropReferenceSourceKey),
            GrowthStatusCode = growthStatusCode, PlantedOn = WireTime.NullIfEmpty(plantedOn),
            ExpectedHarvestOn = WireTime.NullIfEmpty(expectedHarvestOn),
            ProductLinkageStatusCode = productLinkageStatusCode,
            Sensors = Array.ConvertAll(sensors ?? Array.Empty<PotatoSensorWireModel>(), item => item.ToApiModel()),
        };
    }

    [Serializable]
    internal sealed class PotatoCargoWireModel
    {
        public string cargoStableId = string.Empty;
        public string transportTaskStableId = string.Empty;
        public string inboundTaskStableId = string.Empty;
        public string handoffStateCode = string.Empty;
        public bool HasValue() => !string.IsNullOrWhiteSpace(cargoStableId);
        public PotatoCargoApiModel ToApiModel() => new PotatoCargoApiModel
        {
            CargoStableId = cargoStableId, TransportTaskStableId = transportTaskStableId,
            InboundTaskStableId = inboundTaskStableId, HandoffStateCode = handoffStateCode,
        };
    }

    [Serializable]
    internal sealed class PotatoWarehouseWireModel
    {
        public string warehouseStableId = string.Empty;
        public string inventoryStableId = string.Empty;
        public string taskStableId = string.Empty;
        public string statusCode = string.Empty;
        public int? authorizedQuantity;
        public bool HasValue() => !string.IsNullOrWhiteSpace(warehouseStableId);
        public PotatoWarehouseApiModel ToApiModel() => new PotatoWarehouseApiModel
        {
            WarehouseStableId = warehouseStableId, InventoryStableId = inventoryStableId,
            TaskStableId = taskStableId, StatusCode = statusCode, AuthorizedQuantity = authorizedQuantity,
        };
    }

    [Serializable]
    internal sealed class PotatoMarketWireModel
    {
        public string publicProductStableId = string.Empty;
        public double salePrice;
        public string saleUnit = string.Empty;
        public string currencyCode = string.Empty;
        public int availableQuantity;
        public string quantityUnit = string.Empty;
        public string quantityMeaningCode = string.Empty;
        public bool isSaleAvailable;
        public string inventoryObservedAt = string.Empty;
        public string sourceStableId = string.Empty;
        public string sourceRevision = string.Empty;
        public bool HasValue() => !string.IsNullOrWhiteSpace(publicProductStableId);
        public PotatoMarketApiModel ToApiModel() => new PotatoMarketApiModel
        {
            PublicProductStableId = publicProductStableId, SalePrice = (decimal)salePrice,
            SaleUnit = saleUnit, CurrencyCode = currencyCode, AvailableQuantity = availableQuantity,
            QuantityUnit = quantityUnit, QuantityMeaningCode = quantityMeaningCode,
            IsSaleAvailable = isSaleAvailable,
            InventoryObservedAt = WireTime.Required(inventoryObservedAt),
            SourceStableId = sourceStableId, SourceRevision = sourceRevision,
        };
    }

    internal static class WireTime
    {
        public static DateTimeOffset Required(string value)
            => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : throw new PotatoJourneyOperationalApiException("PotatoJourneyWireTimestampInvalid");

        public static DateTimeOffset? Optional(string value)
            => string.IsNullOrWhiteSpace(value) ? null : Required(value);

        public static string? NullIfEmpty(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
