using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 홍달 플레이스 나눔 카드(N0, 2026-11-23). Codex share-r1(scene-share.js)의 hongdal-card.v1 규칙을 게임 커널로 옮긴 것.
    /// 같은 파일을 브라우저 검토판과 게임이 똑같이 받거나 거절해야 한다. 규칙을 넓힐 때는 두 쪽을 함께 바꾼다.
    /// 지금 판은 로컬 파일 나눔만: where는 null, visibility는 export, rights는 game-made뿐이다.
    /// </summary>
    public static class 나눔카드Policy
    {
        public const string Format = "hongdal-card.v1";
        public const string BodyProfile = "local-slot-review";
        public const string DefaultCatalogPack = "hongdal-product-review-r2";

        public sealed class FieldInfo
        {
            public string CatalogSlot;
            public string Label;
            public string Group;
        }

        public static readonly IReadOnlyDictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>
        {
            { "body", new FieldInfo { CatalogSlot = "Body", Label = "상의", Group = "wear" } },
            { "head", new FieldInfo { CatalogSlot = "Head", Label = "모자", Group = "wear" } },
            { "back", new FieldInfo { CatalogSlot = "Back", Label = "가방", Group = "wear" } },
            { "tabletop", new FieldInfo { CatalogSlot = "tabletop", Label = "탁상 소품", Group = "room" } },
            { "floor", new FieldInfo { CatalogSlot = "floor", Label = "바닥 가구", Group = "room" } },
            { "shelf", new FieldInfo { CatalogSlot = "shelf", Label = "진열 상품", Group = "room" } },
        };

        static readonly Regex IdPattern = new Regex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);
        static readonly Regex CardId = new Regex("^card:(product|space):local:[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);
        static readonly Regex Color = new Regex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);
        static readonly string[] EnvelopeKeys = { "format", "id", "kind", "revision", "from", "where", "visibility", "rights", "body" };

        public sealed class Item
        {
            public string Field;
            public string ProfileId; // null이면 자료 미확인 → 칸 이름으로 대신 보인다.
            public string Color;     // body 칸만, 소문자 #rrggbb
        }

        public sealed class Card
        {
            public string Id;
            public string Kind; // product | space
            public int Revision;
            public string FromId;
            public int FromRevision;
            public string CatalogPack;
            public List<Item> Items = new List<Item>();
        }

        public static bool TryNormalize(string json, out Card card, out string reason)
        {
            card = null;
            JToken token;
            try
            {
                token = JToken.Parse(json);
            }
            catch (JsonException)
            {
                reason = "JSON으로 읽을 수 없는 파일입니다.";
                return false;
            }
            return TryNormalize(token, out card, out reason);
        }

        public static bool TryNormalize(JToken token, out Card card, out string reason)
        {
            card = null;
            reason = "이 판에서 지원하는 공유 카드가 아닙니다.";
            var payload = token as JObject;
            if (!KeepOnly(payload, EnvelopeKeys)) return false;
            var kind = Str(payload["kind"]);
            var id = Str(payload["id"]);
            var revision = Int(payload["revision"]);
            if (Str(payload["format"]) != Format || (kind != "product" && kind != "space")
                || id == null || !CardId.IsMatch(id) || !id.StartsWith("card:" + kind + ":", StringComparison.Ordinal)
                || revision < 1 || Str(payload["visibility"]) != "export" || Str(payload["rights"]) != "game-made"
                || !IsNull(payload["where"], true)) return false;
            var body = payload["body"] as JObject;
            if (!KeepOnly(body, new[] { "profile", "catalogPack", "items" }) || Str(body["profile"]) != BodyProfile) return false;
            var pack = Str(body["catalogPack"]);
            var items = body["items"] as JArray;
            var max = kind == "space" ? 3 : 1;
            if (pack == null || !IdPattern.IsMatch(pack) || items == null || items.Count < 1 || items.Count > max) return false;

            var result = new Card { Id = id, Kind = kind, Revision = revision, CatalogPack = pack };
            if (!IsNull(payload["from"], true))
            {
                var from = payload["from"] as JObject;
                var fromId = from == null ? null : Str(from["id"]);
                var fromRevision = from == null ? 0 : Int(from["revision"]);
                if (!KeepOnly(from, new[] { "id", "revision" }) || fromId == null || !CardId.IsMatch(fromId)
                    || !fromId.StartsWith("card:" + kind + ":", StringComparison.Ordinal) || fromId == id || fromRevision < 1)
                {
                    reason = "원본 카드와 판본을 확인해 주세요.";
                    return false;
                }
                result.FromId = fromId;
                result.FromRevision = fromRevision;
            }

            var used = new HashSet<string>();
            foreach (var raw in items)
            {
                var item = raw as JObject;
                reason = "공유 파일의 물건이나 위치가 올바르지 않습니다.";
                if (!KeepOnly(item, new[] { "field", "profileId", "color" })) return false;
                var field = Str(item["field"]);
                if (field == null || !Fields.ContainsKey(field) || used.Contains(field)) return false;
                if (kind == "space" && Fields[field].Group != "room") return false;
                // profileId는 키가 있어야 하고 null 또는 목록 id. user-로 시작하는 개인 물건은 밖으로 안 나간다.
                if (item["profileId"] == null) return false;
                string profileId = null;
                if (!IsNull(item["profileId"], false))
                {
                    profileId = Str(item["profileId"]);
                    if (profileId == null || !IdPattern.IsMatch(profileId) || profileId.StartsWith("user-", StringComparison.Ordinal)) return false;
                }
                string color = null;
                if (!IsNull(item["color"], false))
                {
                    color = Str(item["color"]);
                    if (field != "body" || color == null || !Color.IsMatch(color)) return false;
                    color = color.ToLowerInvariant();
                }
                used.Add(field);
                result.Items.Add(new Item { Field = field, ProfileId = profileId, Color = string.IsNullOrEmpty(color) ? null : color });
            }
            card = result;
            reason = null;
            return true;
        }

        /// <summary>share-r1 normalize()가 내놓는 것과 같은 키 순서의 JSON(공백 없음). 해시와 비교의 기준.</summary>
        public static string Canonical(Card card)
        {
            var items = new JArray(card.Items.Select(item =>
            {
                var o = new JObject { { "field", item.Field }, { "profileId", item.ProfileId == null ? JValue.CreateNull() : (JToken)item.ProfileId } };
                if (item.Color != null) o.Add("color", item.Color);
                return (JToken)o;
            }));
            var from = card.FromId == null ? JValue.CreateNull() : (JToken)new JObject { { "id", card.FromId }, { "revision", card.FromRevision } };
            var root = new JObject
            {
                { "format", Format }, { "id", card.Id }, { "kind", card.Kind }, { "revision", card.Revision },
                { "from", from }, { "where", JValue.CreateNull() }, { "visibility", "export" }, { "rights", "game-made" },
                { "body", new JObject { { "profile", BodyProfile }, { "catalogPack", card.CatalogPack }, { "items", items } } },
            };
            return root.ToString(Formatting.None);
        }

        public static string Hash(Card card)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical(card)));
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
        }

        /// <summary>share-r1 makeDerived와 같다: 원본을 가리키는 새 카드, 판본 1, 고른 칸만 남김.</summary>
        public static bool TryDerive(Card source, string newId, IEnumerable<string> fieldsToKeep, out Card derived, out string reason)
        {
            var keep = new HashSet<string>(fieldsToKeep ?? Enumerable.Empty<string>());
            var copy = JObject.Parse(Canonical(source));
            copy["id"] = newId;
            copy["revision"] = 1;
            copy["from"] = new JObject { { "id", source.Id }, { "revision", source.Revision } };
            var items = new JArray(((JArray)copy["body"]["items"]).Where(item => keep.Contains((string)item["field"])));
            copy["body"]["items"] = items;
            return TryNormalize(copy, out derived, out reason);
        }

        /// <summary>
        /// 받은 카드 묶음의 계보. from을 따라가다 같은 카드로 돌아오면 고리(거절). 없는 원본은 "원본 자료 미확인"으로 둔다(0이나 오류로 바꾸지 않음).
        /// 반환: 카드 id → 맨 처음 원본까지의 길이(-1이면 고리).
        /// </summary>
        public static Dictionary<string, int> Lineage(IEnumerable<Card> cards, out List<string> missingOrigins)
        {
            var byId = new Dictionary<string, Card>();
            foreach (var card in cards)
            {
                Card existing;
                if (!byId.TryGetValue(card.Id, out existing) || existing.Revision < card.Revision) byId[card.Id] = card;
            }
            missingOrigins = new List<string>();
            var depth = new Dictionary<string, int>();
            foreach (var card in byId.Values)
            {
                var seen = new HashSet<string>();
                var current = card;
                var steps = 0;
                var loop = false;
                while (current != null && current.FromId != null)
                {
                    if (!seen.Add(current.Id)) { loop = true; break; }
                    Card next;
                    if (!byId.TryGetValue(current.FromId, out next))
                    {
                        if (!missingOrigins.Contains(current.FromId)) missingOrigins.Add(current.FromId);
                        steps++;
                        break;
                    }
                    if (seen.Contains(next.Id)) { loop = true; break; }
                    current = next;
                    steps++;
                }
                depth[card.Id] = loop ? -1 : steps;
            }
            return depth;
        }

        static readonly Regex Phone = new Regex(@"(\+?82[-\s.]?)?0\d{1,2}[-\s.)]?\d{3,4}[-\s.]?\d{4}", RegexOptions.CultureInvariant);
        static readonly Regex Email = new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant);
        static readonly Regex Address = new Regex(@"\d+(-\d+)?\s*(번길|로|길)\s*\d+|\d{1,4}\s*번지|\d+동\s*\d+호", RegexOptions.CultureInvariant);
        static readonly Regex Url = new Regex(@"https?://|www\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// 다음 판(이야기 카드·제목·설명)에 사람이 쓰는 글이 들어올 때의 관문. 전화번호·전자우편·실제 주소 꼴·링크는 막는다.
        /// 게임 안 이름(면목제7동, 카페 1호점)은 통과한다.
        /// </summary>
        public static bool TextSafe(string text, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(text)) return true;
            if (text.Length > 280) { reason = "글이 너무 깁니다(280자까지)."; return false; }
            if (Phone.IsMatch(text)) { reason = "전화번호처럼 보이는 글은 나눌 수 없어요."; return false; }
            if (Email.IsMatch(text)) { reason = "전자우편 주소는 나눌 수 없어요."; return false; }
            if (Address.IsMatch(text)) { reason = "실제 주소처럼 보이는 글은 나눌 수 없어요."; return false; }
            if (Url.IsMatch(text)) { reason = "링크는 이 판에서 나눌 수 없어요."; return false; }
            return true;
        }

        static bool KeepOnly(JObject o, string[] keys)
        {
            return o != null && o.Properties().All(p => keys.Contains(p.Name));
        }

        static string Str(JToken t)
        {
            return t != null && t.Type == JTokenType.String ? (string)t : null;
        }

        static int Int(JToken t)
        {
            if (t == null) return 0;
            double d;
            if (t.Type == JTokenType.Integer) d = (long)t;
            else if (t.Type == JTokenType.Float) d = (double)t;
            else return 0;
            if (Math.Floor(d) != d) return 0;
            var v = (long)d;
            return v < 1 || v > int.MaxValue ? 0 : (int)v;
        }

        // required: 키가 꼭 있어야 하는지. share-r1은 where/from 키가 없으면 거절하고(undefined !== null), color 키가 없으면 통과한다.
        static bool IsNull(JToken t, bool required)
        {
            if (t == null) return !required;
            return t.Type == JTokenType.Null;
        }
    }
}
