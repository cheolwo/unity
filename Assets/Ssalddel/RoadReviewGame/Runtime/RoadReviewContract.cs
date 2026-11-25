using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ssalddel.RoadReviewGame
{
    /// <summary>플레이 선택은 기존 관측 범위 승인 요청만 만들며, 통과 시각이나 새 권위를 만들지 않습니다.</summary>
    public static class RoadReviewContract
    {
        public static bool IsAllowedServer(string address)
        {
            return Uri.TryCreate(address, UriKind.Absolute, out var uri)
                   && uri.Scheme == "http" && (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                   && uri.Port == 8791 && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/"
                   && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
        }

        public static JObject BuildApproval(JObject candidate, JObject listing, IEnumerable<string> selected, string requestId)
        {
            if (candidate == null || !candidate.Value<bool>("approvable") || candidate.Value<bool?>("contextCurrent") != true)
                throw new InvalidOperationException("현재 등록할 수 없는 후보입니다.");
            var sha = candidate.Value<string>("sourceSha256");
            var sourceId = candidate.Value<string>("sourceId");
            var snapshot = candidate.Value<string>("snapshotSha256");
            var version = candidate["version"];
            if (!IsSha256(sha) || sourceId != "sha256:" + sha || string.IsNullOrWhiteSpace(candidate.Value<string>("candidateId"))
                || !IsSha256(snapshot) || version == null || version.Type == JTokenType.Null
                || (version.Type != JTokenType.String && version.Type != JTokenType.Integer)
                || string.IsNullOrWhiteSpace(version.ToString()) || !Guid.TryParse(requestId, out var requestGuid) || requestGuid == Guid.Empty)
                throw new InvalidOperationException("원본·후보·판본·요청의 필수 식별 근거가 없습니다.");
            if (listing == null || listing.Value<string>("sourceId") != sourceId || listing.Value<string>("sourceSha256") != sha
                || listing.Value<string>("snapshotSha256") != snapshot)
                throw new InvalidOperationException("현재 조회 목록과 후보의 원본 또는 판본이 다릅니다.");
            if (candidate["holdReasons"] is JArray holds && holds.Count > 0)
                throw new InvalidOperationException("보류 근거가 남아 있는 후보입니다.");
            if (candidate["conflictingApprovedCandidateIds"] is JArray conflicts && conflicts.Count > 0)
                throw new InvalidOperationException("서로 양립하지 않는 기존 연결을 먼저 확인해 주세요.");
            var ordered = (candidate["segments"] as JArray ?? new JArray()).OfType<JObject>().Select(x => x.Value<string>("segmentId")).ToList();
            var ids = selected?.Distinct().ToList() ?? new List<string>();
            if (ordered.Count == 0 || ordered.Any(string.IsNullOrWhiteSpace) || ordered.Distinct().Count() != ordered.Count
                || ids.Count == 0 || ids.Any(x => !ordered.Contains(x))) throw new InvalidOperationException("관측 후보에 포함된 선분을 선택해 주세요.");
            return new JObject
            {
                ["op"] = "approve", ["sourceId"] = candidate["sourceId"], ["sourceSha256"] = candidate["sourceSha256"],
                ["candidateId"] = candidate["candidateId"], ["segmentIds"] = new JArray(ordered.Where(ids.Contains)),
                ["version"] = version, ["snapshotSha256"] = snapshot,
                ["requestId"] = requestId
            };
        }

        private static bool IsSha256(string value) => value != null && value.Length == 64 && value.All(x => (x >= '0' && x <= '9') || (x >= 'a' && x <= 'f'));

        public static List<List<Vector2>> ReadLines(JObject segment)
        {
            var result = new List<List<Vector2>>();
            void Add(JToken coordinates)
            {
                if (!(coordinates is JArray array) || array.Count == 0) return;
                if (array[0] is JArray first && first.Count >= 2 && first[0]?.Type != JTokenType.Array)
                {
                    var points = array.OfType<JArray>().Where(x => x.Count >= 2).Select(x => new Vector2(x[0].Value<float>(), x[1].Value<float>())).ToList();
                    if (points.Count > 1) result.Add(points);
                }
                else foreach (var item in array) Add(item);
            }
            var geometryLines = segment["geometryLines"];
            var geometry = segment["geometry"];
            if (geometryLines != null) Add(geometryLines);
            else if (geometry is JObject geo) Add(geo["coordinates"]);
            else Add(geometry);
            return result;
        }
    }
}
