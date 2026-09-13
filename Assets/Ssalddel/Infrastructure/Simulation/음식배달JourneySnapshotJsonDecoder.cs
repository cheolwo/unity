using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// 음식 배달 이동 상태 사본의 v1 wire 모양만 해독한다.
    /// 해독은 정확한 필드 이름과 JSON 타입을 확인하며 배포 승인, 판본, hash와 경로 의미는 Interpreter가 판정한다.
    /// </summary>
    public static class 음식배달JourneySnapshotJsonDecoder
    {
        private static readonly string[] RootFields =
        {
            "schemaVersion", "regionStableId", "administrativeAreaStableId",
            "sourceKindCode", "orderStableId", "actorStableId", "vehicleStableId",
            "routeStableId", "graphRevision", "graphHash", "routeFingerprint",
            "revision", "observedAt", "blocked", "distributionApproved",
            "readinessCode", "currentLegIndex", "legs",
        };

        private static readonly string[] LegFields =
        {
            "stableId", "mode", "stateCode", "distanceMeters", "progressMeters",
            "points", "actorPosition", "vehiclePosition",
        };

        private static readonly string[] PointFields = { "x", "z" };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            });

        public static 음식배달JourneySnapshotData Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("FoodDeliveryJourneyJsonMissing");
            try
            {
                var root = ParseRoot(json);
                Require(root, RootFields);
                RejectUnknown(root, RootFields);
                RequireType(root, JTokenType.String,
                    "schemaVersion", "regionStableId", "administrativeAreaStableId",
                    "sourceKindCode", "orderStableId", "actorStableId", "vehicleStableId",
                    "routeStableId", "graphRevision", "graphHash", "routeFingerprint",
                    "observedAt", "readinessCode");
                RequireType(root, JTokenType.Integer, "revision", "currentLegIndex");
                RequireType(root, JTokenType.Boolean, "blocked", "distributionApproved");
                if (root["legs"] is not JArray legs)
                    throw new FormatException("FoodDeliveryJourneyLegsJsonInvalid");
                foreach (var token in legs)
                {
                    if (token is not JObject leg)
                        throw new FormatException("FoodDeliveryJourneyLegJsonInvalid");
                    RejectUnknown(leg, LegFields);
                    Require(leg, "stableId", "mode", "stateCode", "distanceMeters",
                        "progressMeters", "points", "actorPosition");
                    RequireType(leg, JTokenType.String, "stableId", "mode", "stateCode");
                    RequireNumber(leg, "distanceMeters", "progressMeters");
                    if (leg["points"] is not JArray points)
                        throw new FormatException("FoodDeliveryJourneyPointsJsonInvalid");
                    foreach (var point in points)
                        RequirePoint(point, "FoodDeliveryJourneyPointJsonInvalid");
                    RequirePoint(leg["actorPosition"],
                        "FoodDeliveryJourneyActorPositionJsonInvalid");
                    if (leg.TryGetValue("vehiclePosition", out var vehicle)
                        && vehicle.Type != JTokenType.Null)
                    {
                        RequirePoint(vehicle,
                            "FoodDeliveryJourneyVehiclePositionJsonInvalid");
                    }
                }

                return root.ToObject<음식배달JourneySnapshotData>(Serializer)
                       ?? throw new FormatException("FoodDeliveryJourneyJsonInvalid");
            }
            catch (FormatException)
            {
                throw;
            }
            catch (JsonException error)
            {
                throw new FormatException("FoodDeliveryJourneyJsonInvalid", error);
            }
        }

        private static void Require(JObject value, params string[] names)
        {
            foreach (var name in names)
                if (!value.TryGetValue(name, StringComparison.Ordinal, out var token)
                    || token.Type == JTokenType.Null)
                {
                    throw new FormatException(
                        "FoodDeliveryJourneyJsonFieldMissing:" + name);
                }
        }

        private static void RequirePoint(JToken token, string failureCode)
        {
            if (token is not JObject point)
                throw new FormatException(failureCode);
            Require(point, PointFields);
            RejectUnknown(point, PointFields);
            RequireNumber(point, PointFields);
        }

        private static JObject ParseRoot(string json)
        {
            using var textReader = new StringReader(json);
            using var jsonReader = new JsonTextReader(textReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                CloseInput = true,
            };
            var root = JObject.Load(jsonReader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                CommentHandling = CommentHandling.Ignore,
            });
            if (jsonReader.Read())
                throw new FormatException("FoodDeliveryJourneyJsonTrailingContent");
            return root;
        }

        private static void RejectUnknown(JObject value, params string[] names)
        {
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var property in value.Properties())
                if (!allowed.Contains(property.Name))
                    throw new FormatException(
                        "FoodDeliveryJourneyJsonFieldUnknown:" + property.Name);
        }

        private static void RequireType(JObject value, JTokenType type,
            params string[] names)
        {
            foreach (var name in names)
                if (value[name]?.Type != type)
                    throw new FormatException(
                        "FoodDeliveryJourneyJsonFieldTypeInvalid:" + name);
        }

        private static void RequireNumber(JObject value, params string[] names)
        {
            foreach (var name in names)
            {
                var type = value[name]?.Type;
                if (type != JTokenType.Integer && type != JTokenType.Float)
                    throw new FormatException(
                        "FoodDeliveryJourneyJsonFieldTypeInvalid:" + name);
            }
        }
    }
}
