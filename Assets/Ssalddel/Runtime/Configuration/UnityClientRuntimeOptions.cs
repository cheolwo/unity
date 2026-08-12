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
        public string OperationalApiBaseUrl { get; set; } = "https://localhost:7117/";

        public string SimulationRehearsalApiBaseUrl { get; set; } = "http://localhost:5204/";

        public string DetailBaseUrl { get; set; } = "http://localhost:5238";

        public string ExecutionMode { get; set; } = UnityExecutionModeCodes.Simulation;

        public bool AllowFixtureData { get; set; } = true;

        public void Validate()
        {
            ValidateOperationalConnection();
            ValidateSimulationRehearsalConnection();
            ValidateDetailNavigation();
            ValidateExecutionPolicy();
        }

        public void ValidateOperationalConnection()
            => ValidateHttpBaseUrl(OperationalApiBaseUrl, "운영 API");

        public void ValidateSimulationRehearsalConnection()
            => ValidateHttpBaseUrl(SimulationRehearsalApiBaseUrl, "예행연습·게임 세계 API");

        public void ValidateDetailNavigation()
        {
            if (!Uri.TryCreate(DetailBaseUrl, UriKind.Absolute, out var detailUri)
                || (detailUri.Scheme != Uri.UriSchemeHttp && detailUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("상세 페이지 기준 주소는 HTTP 또는 HTTPS 절대 주소여야 합니다.");
            }
        }

        public void ValidateExecutionPolicy()
        {
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

        private static void ValidateHttpBaseUrl(string value, string label)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    label + " 기준 주소는 HTTP 또는 HTTPS 절대 주소여야 합니다.");
            }
        }
    }
}
