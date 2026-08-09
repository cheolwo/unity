using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.WorldMap
{
    [Serializable]
    public sealed class PublicWorldMetric
    {
        public string Code = string.Empty;
        public string DisplayName = string.Empty;
        public decimal Value;
        public string Unit = string.Empty;
    }

    [Serializable]
    public sealed class PublicWorldMarker
    {
        public string StableId = string.Empty;
        public string DatasetCode = string.Empty;
        public string LayerCode = string.Empty;
        public string CountryCode = string.Empty;
        public string CountryName = string.Empty;
        public double Latitude;
        public double Longitude;
        public string Title = string.Empty;
        public string Summary = string.Empty;
        public string SourceName = string.Empty;
        public DateTimeOffset? EvidenceAsOfUtc;
        public string EvidenceStatusCode = string.Empty;
        public string DetailHref = string.Empty;
        public string SourceHref = string.Empty;
        public string LocationPrecisionCode = string.Empty;
        public string FreshnessCode = string.Empty;
        public string BoundaryNotice = string.Empty;
        public PublicWorldMetric[] Metrics = Array.Empty<PublicWorldMetric>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(StableId) || string.IsNullOrWhiteSpace(LayerCode))
                throw new InvalidOperationException("공개 지도 marker의 stable ID와 layer code가 필요합니다.");
            if (Latitude < -90d || Latitude > 90d || Longitude < -180d || Longitude > 180d)
                throw new InvalidOperationException("공개 지도 marker 좌표 범위가 올바르지 않습니다.");
            if (string.IsNullOrWhiteSpace(SourceName) || string.IsNullOrWhiteSpace(BoundaryNotice))
                throw new InvalidOperationException("공개 지도 marker에는 출처와 공개 경계 고지가 필요합니다.");
        }
    }

    [Serializable]
    public sealed class PublicWorldMapSnapshot
    {
        public string DatasetCode = string.Empty;
        public string Revision = string.Empty;
        public DateTimeOffset GeneratedAtUtc;
        public PublicWorldMarker[] Markers = Array.Empty<PublicWorldMarker>();
    }

    public interface ICommunityWorldMapRepository
    {
        Task<PublicWorldMapSnapshot> LoadAsync(string datasetCode, CancellationToken cancellationToken);
    }
}
