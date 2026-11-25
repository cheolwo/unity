using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ssalddel.MobileGame
{
    /// <summary>가상 선택 경험입니다. 기록을 사람 승인이나 모델 학습으로 승격하지 않습니다.</summary>
    internal sealed class MobileGameExperienceLog
    {
        private readonly string path;
        private readonly string inputHash;
        private readonly HashSet<string> written = new HashSet<string>(StringComparer.Ordinal);
        public int CorruptLineCount { get; private set; }
        public MobileGameExperienceLog(string directory, string inputHashSha256)
        {
            path = Path.Combine(directory, "game-experiences.v1.jsonl");
            inputHash = inputHashSha256;
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadLines(path))
            {
                try
                {
                    var id = (string)JObject.Parse(line)["eventId"];
                    if (!string.IsNullOrWhiteSpace(id)) written.Add(id);
                }
                catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
                { CorruptLineCount++; /* 손상된 과거 행을 정답으로 읽거나 자동 수정하지 않습니다. */ }
            }
        }

        public void Record(string sessionId, string action, JObject command, JObject result, JArray candidates = null)
        {
            var commandId = (string)command["commandId"];
            var id = sessionId + ":" + (string.IsNullOrWhiteSpace(commandId) ? Guid.NewGuid().ToString("N") : commandId);
            if (written.Contains(id)) return;
            var record = new JObject
            {
                ["schemaVersion"] = "hongdal-mobile-experience.v1",
                ["eventId"] = id,
                ["sourceKind"] = "Simulation",
                ["sessionId"] = sessionId,
                ["inputHashSha256"] = inputHash,
                ["gameRulesRevision"] = Ssalddel.Sagajeong.Core.SagajeongSimulationCore.RulesRevision,
                ["recordedAtUtc"] = DateTime.UtcNow.ToString("o"),
                ["policyRevision"] = "mobile-rule-eligible-candidate.r1",
                ["policyFingerprintKind"] = "RevisionDescriptor",
                ["policyFingerprintSha256"] = PolicyFingerprint(),
                ["action"] = action,
                ["actorKind"] = action == "ai-trial" ? "RuleAdvisor" :
                    action == "round-outcome" ? "SimulationAuthority" : "HumanPlayer",
                ["orderId"] = command["orderId"]?.DeepClone(),
                ["selectedDriverId"] = command["driverId"]?.DeepClone(),
                ["inputRevision"] = command["revision"]?.DeepClone(),
                ["virtualElapsedMs"] = command["elapsedMs"]?.DeepClone(),
                ["candidates"] = CandidateSummary(candidates),
                ["accepted"] = (bool?)result["ok"] == true,
                ["message"] = (string)result["message"],
                ["modelMode"] = action == "ai-trial" ? "RuleOnlyNoModel" : "NotInvoked",
                ["modelInvoked"] = false,
                ["modelId"] = null,
                ["modelDigest"] = null,
                ["approvedForTraining"] = false,
                ["weightsTrained"] = false,
                ["operationalActionsAllowed"] = false
            };
            if (action == "round-outcome")
            {
                var state = result["state"] as JObject;
                record["settlement"] = NumericSummary(state?["settlement"] as JObject,
                    "cleared", "onTimeCount", "revenue", "driverPay", "baseBalance", "bonus", "balance",
                    "platformRevenue", "platformBalance", "fastDispatchBonus", "totalOrders", "completedOrders");
                var orders = new JArray();
                if (state?["orders"] is JArray values)
                    foreach (var token in values)
                    {
                        if (!(token is JObject order)) continue;
                        var summary = NumericSummary(order, "deliveryMinutes", "assignedOnTime");
                        if (order["id"]?.Type == JTokenType.String) summary["id"] = order["id"].DeepClone();
                        if (order["driverId"]?.Type == JTokenType.String) summary["driverId"] = order["driverId"].DeepClone();
                        orders.Add(summary);
                    }
                record["orders"] = orders;
            }
            File.AppendAllText(path, record.ToString(Formatting.None) + "\n", new UTF8Encoding(false));
            written.Add(id);
        }

        private static JObject NumericSummary(JObject source, params string[] fields)
        {
            var result = new JObject();
            if (source == null) return result;
            foreach (var field in fields)
            {
                var value = source[field];
                if (value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float ||
                    value.Type == JTokenType.Boolean)) result[field] = value.DeepClone();
            }
            return result;
        }

        private static JArray CandidateSummary(JArray source)
        {
            var result = new JArray();
            if (source == null) return result;
            foreach (var token in source)
            {
                if (!(token is JObject candidate)) continue;
                var summary = NumericSummary(candidate, "eligible", "pickupKilometers", "readyDifferenceSeconds");
                foreach (var field in new[] { "driverId", "label", "arrivalAt", "reason" })
                    if (candidate[field]?.Type == JTokenType.String) summary[field] = candidate[field].DeepClone();
                result.Add(summary);
            }
            return result;
        }

        private static string PolicyFingerprint()
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                    Ssalddel.Sagajeong.Core.SagajeongSimulationCore.RulesRevision + "|mobile-rule-eligible-candidate.r1")))
                    .Replace("-", "").ToLowerInvariant();
        }
    }
}
