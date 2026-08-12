using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.WorldMap;

namespace Ssalddel.Unity.Infrastructure.WorldMap
{
    public sealed class CommunityWorldMapRepository : ICommunityWorldMapRepository
    {
        private const string Route = "api/v1/community/world-map/observations";
        private const string DefaultBoundaryNotice = "공개 관측 정보이며 개인 위치, 재고, 계약 또는 신청 가능 여부를 의미하지 않습니다.";
        private readonly IOperationalUnityApiClient apiClient;

        public CommunityWorldMapRepository(IOperationalUnityApiClient apiClient) =>
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

        public async Task<PublicWorldMapSnapshot> LoadAsync(string datasetCode, CancellationToken cancellationToken)
        {
            var suffix = string.IsNullOrWhiteSpace(datasetCode)
                ? string.Empty
                : "?dataset=" + Uri.EscapeDataString(datasetCode.Trim());
            var response = await apiClient.SendAsync(new UnityApiRequest
            {
                Method = "GET",
                RelativePath = Route + suffix,
                RequiresAuthentication = false
            }, cancellationToken);

            if (!response.IsSuccess)
                throw new InvalidOperationException($"공개 세계지도 조회 실패: HTTP {response.StatusCode}");

            var dto = JsonConvert.DeserializeObject<SnapshotDto>(response.Body);
            if (dto == null) throw new InvalidOperationException("공개 세계지도 응답을 해석할 수 없습니다.");
            return Map(dto);
        }

        private static PublicWorldMapSnapshot Map(SnapshotDto dto) => new PublicWorldMapSnapshot
        {
            DatasetCode = dto.DatasetCode ?? string.Empty,
            Revision = dto.Revision ?? string.Empty,
            GeneratedAtUtc = ParseTimestamp(dto.GeneratedAtUtc),
            Markers = (dto.Observations ?? Array.Empty<ObservationDto>()).Select(Map).ToArray()
        };

        private static PublicWorldMarker Map(ObservationDto dto) => new PublicWorldMarker
        {
            StableId = dto.StableId ?? string.Empty,
            DatasetCode = dto.DatasetCode ?? string.Empty,
            LayerCode = dto.LayerCode ?? string.Empty,
            CountryCode = dto.CountryCode ?? string.Empty,
            CountryName = dto.CountryName ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Title = dto.Title ?? string.Empty,
            Summary = dto.Summary ?? string.Empty,
            SourceName = dto.SourceName ?? string.Empty,
            EvidenceAsOfUtc = ParseOptionalTimestamp(dto.EvidenceAsOfUtc),
            EvidenceStatusCode = dto.EvidenceStatusCode ?? string.Empty,
            DetailHref = dto.DetailHref ?? string.Empty,
            SourceHref = dto.SourceHref ?? string.Empty,
            LocationPrecisionCode = dto.LocationPrecisionCode ?? string.Empty,
            FreshnessCode = string.IsNullOrWhiteSpace(dto.FreshnessCode) ? "Unknown" : dto.FreshnessCode,
            BoundaryNotice = string.IsNullOrWhiteSpace(dto.BoundaryNotice) ? DefaultBoundaryNotice : dto.BoundaryNotice,
            Metrics = (dto.Metrics ?? Array.Empty<MetricDto>()).Select(x => new PublicWorldMetric
            {
                Code = x.Code ?? string.Empty,
                DisplayName = x.DisplayName ?? string.Empty,
                Value = Convert.ToDecimal(x.Value, CultureInfo.InvariantCulture),
                Unit = x.Unit ?? string.Empty
            }).ToArray()
        };

        private static DateTimeOffset ParseTimestamp(string value) =>
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : throw new InvalidOperationException("공개 세계지도 기준시각이 올바르지 않습니다.");

        private static DateTimeOffset? ParseOptionalTimestamp(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : ParseTimestamp(value);

        [Serializable] private sealed class SnapshotDto { public string DatasetCode; public string Revision; public string GeneratedAtUtc; public ObservationDto[] Observations; }
        [Serializable] private sealed class MetricDto { public string Code; public string DisplayName; public double Value; public string Unit; }
        [Serializable] private sealed class ObservationDto
        {
            public string StableId; public string DatasetCode; public string LayerCode; public string CountryCode; public string CountryName;
            public double Latitude; public double Longitude; public string Title; public string Summary; public string SourceName;
            public string EvidenceAsOfUtc; public string EvidenceStatusCode; public string DetailHref; public string SourceHref;
            public string LocationPrecisionCode; public string FreshnessCode; public string BoundaryNotice; public MetricDto[] Metrics;
        }
    }
}
