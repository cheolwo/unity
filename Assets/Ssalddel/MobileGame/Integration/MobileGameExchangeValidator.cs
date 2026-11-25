using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Ssalddel.MobileGame
{
    /// <summary>자료 반입은 참고 사본만 허용합니다. 형식 검사는 출처·권리 승인과 다릅니다.</summary>
    public static class MobileGameExchangeValidator
    {
        public const int MaximumBytes = 1024 * 1024;
        private static readonly HashSet<string> AllowedRootFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion", "sourceKind", "sessionId", "inputHashSha256", "rulesRevision",
            "virtualElapsedMs", "stageId", "manualPickup", "stores", "couriers", "orders",
            "summary", "operationalActionsAllowed", "weightsTrained", "approvedForTraining",
            "sourceId", "sourceRevision", "observedAtUtc", "createdAtUtc", "scope", "regions",
            "items", "statistics", "limitations", "provenance"
        };
        private static readonly string[] PrivateFieldFragments =
        {
            "password", "secret", "token", "credential", "customer", "recipient", "phone",
            "email", "account", "address", "checkpoint", "executable", "authorization",
            "고객", "전화", "연락처", "비밀번호", "상세주소", "동호수", "수령인"
        };

        public static bool TryRead(string text, out JObject reference, out string message)
        {
            reference = null;
            message = null;
            if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > MaximumBytes)
            {
                message = "1MB 이내의 게임 참고 자료 JSON 파일을 선택해 주세요.";
                return false;
            }
            try
            {
                var value = JObject.Parse(text);
                var schema = (string)value["schemaVersion"];
                if (schema != "game-business-preparation.v1" && schema != "reality-game-context.v1")
                {
                    message = "지원하지 않는 참고 자료 판본입니다. 게임 세이브와 업무 원본은 여기서 가져오지 않습니다.";
                    return false;
                }
                foreach (var property in value.Properties())
                    if (!AllowedRootFields.Contains(property.Name))
                    {
                        message = "허용하지 않은 원본 항목이 있습니다. 필요한 참고 항목만 내보내 주세요.";
                        return false;
                    }
                foreach (var token in value.Descendants())
                {
                    if (!(token is JProperty property)) continue;
                    foreach (var fragment in PrivateFieldFragments)
                        if (property.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            message = "고객·계정·상세주소를 포함하는 원본 자료는 가져올 수 없습니다.";
                            return false;
                        }
                }
                if ((bool?)value["operationalActionsAllowed"] == true ||
                    (bool?)value["weightsTrained"] == true || (bool?)value["approvedForTraining"] == true)
                {
                    message = "운영 실행·모델 학습·사람 승인을 포함하는 자료는 참고 반입 범위가 아닙니다.";
                    return false;
                }
                var source = (string)value["sourceKind"];
                if (source != "Simulation" && source != "Derived" && source != "RealObservation")
                {
                    message = "게임·가공·현실 관측의 출처 구분을 확인해 주세요.";
                    return false;
                }
                value["operationalActionsAllowed"] = false;
                value["weightsTrained"] = false;
                value["approvedForTraining"] = false;
                reference = value;
                return true;
            }
            catch (Exception exception) when (exception is Newtonsoft.Json.JsonException ||
                exception is ArgumentException || exception is FormatException || exception is InvalidCastException)
            {
                message = "참고 자료 형식을 읽을 수 없습니다. 기존 게임은 그대로 유지했습니다.";
                return false;
            }
        }
    }
}
