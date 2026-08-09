using System;

namespace Ssalddel.Unity.Runtime.Configuration
{
    public static class UnityExecutionModeCodes
    {
        public const string Simulation = "Simulation";
        public const string Operational = "Operational";

        public static bool IsSupported(string value)
            => string.Equals(value, Simulation, StringComparison.Ordinal)
               || string.Equals(value, Operational, StringComparison.Ordinal);
    }

    public static class UnityDataSourceCodes
    {
        public const string Live = "Live";
        public const string Cached = "Cached";
        public const string Fixture = "Fixture";

        public static bool IsSupported(string value)
            => string.Equals(value, Live, StringComparison.Ordinal)
               || string.Equals(value, Cached, StringComparison.Ordinal)
               || string.Equals(value, Fixture, StringComparison.Ordinal);
    }

    public sealed class UnityClientRuntimeOptions
    {
        public string ApiBaseUrl { get; set; } = "http://localhost:5104";

        public string DetailBaseUrl { get; set; } = "http://localhost:5238";

        public string ExecutionMode { get; set; } = UnityExecutionModeCodes.Simulation;

        public bool AllowFixtureData { get; set; } = true;

        public void Validate()
        {
            if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("API base URL은 HTTP 또는 HTTPS 절대 주소여야 합니다.");
            }

            if (!Uri.TryCreate(DetailBaseUrl, UriKind.Absolute, out var detailUri)
                || (detailUri.Scheme != Uri.UriSchemeHttp && detailUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("상세 페이지 기준 주소는 HTTP 또는 HTTPS 절대 주소여야 합니다.");
            }

            if (!UnityExecutionModeCodes.IsSupported(ExecutionMode))
            {
                throw new InvalidOperationException("지원하지 않는 Unity 실행 모드입니다.");
            }

            if (string.Equals(ExecutionMode, UnityExecutionModeCodes.Operational, StringComparison.Ordinal)
                && AllowFixtureData)
            {
                throw new InvalidOperationException("Operational 모드에서는 fixture 데이터를 허용할 수 없습니다.");
            }
        }
    }
}
