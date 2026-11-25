using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>
    /// 홍달 플레이스 나눔 카드(N0)의 관문 검사. 정답 해시는 Codex share-r1 scene-share.js normalize()를 node로 돌려 얻은 값이다(2026-11-23).
    /// 브라우저 검토판과 게임이 같은 파일을 같은 모양으로 읽는지를 이 해시로 맞춘다.
    /// </summary>
    public static class 나눔카드Checks
    {
        const string SpaceJson = "{\"format\":\"hongdal-card.v1\",\"id\":\"card:space:local:cafe-corner\",\"kind\":\"space\",\"revision\":2,\"from\":null,\"where\":null,\"visibility\":\"export\",\"rights\":\"game-made\",\"body\":{\"profile\":\"local-slot-review\",\"catalogPack\":\"hongdal-product-review-r2\",\"items\":[{\"field\":\"tabletop\",\"profileId\":\"mug-a\"},{\"field\":\"shelf\",\"profileId\":null}]}}";
        const string SpaceHash = "2df405a91c28c6ecf43e2d57ca52155a21edf8df69ef8df8f137feab775f5cc0";
        const string ProductHash = "72c69276da4cfff0b32e21bdd987f5dddf45967c53a29bbb0e449a506ddee290";
        const string DerivedHash = "a1de4602e374e7409500fe1602269b6583cf0f651e024daec529abf97b3e7357";

        public static string Run()
        {
            var lines = new List<string>();
            var failed = 0;
            Action<string, bool, string> check = (name, ok, detail) =>
            {
                if (!ok) failed++;
                lines.Add((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " — " + detail));
            };
            try { Envelope(check); }
            catch (Exception exception) { check("나눔 카드: 봉투 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message); }
            try { Lineage(check); }
            catch (Exception exception) { check("나눔 카드: 계보 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message); }
            try { Text(check); }
            catch (Exception exception) { check("나눔 카드: 글 관문 검사가 끝까지 돌지 못했습니다", false, exception.GetType().Name + " · " + exception.Message); }
            return "Hongdal share card checks: " + (lines.Count - failed) + " passed, " + failed + " failed\n" + string.Join("\n", lines);
        }

        static JObject Space() { return JObject.Parse(SpaceJson); }

        static bool Rejects(JObject payload)
        {
            나눔카드Policy.Card card;
            string reason;
            return !나눔카드Policy.TryNormalize(payload, out card, out reason) && !string.IsNullOrEmpty(reason);
        }

        static void Envelope(Action<string, bool, string> check)
        {
            나눔카드Policy.Card space;
            string reason;
            var ok = 나눔카드Policy.TryNormalize(SpaceJson, out space, out reason);
            check("공간 카드를 읽고 Codex 검토판과 같은 해시를 냄", ok && 나눔카드Policy.Hash(space) == SpaceHash,
                ok ? 나눔카드Policy.Hash(space) : reason);
            check("다시 읽어도 같은 모양(정규형 고정점)",
                ok && 나눔카드Policy.TryNormalize(나눔카드Policy.Canonical(space), out var again, out reason) && 나눔카드Policy.Canonical(again) == 나눔카드Policy.Canonical(space), null);

            // 키 순서·공백이 달라도 같은 카드
            var shuffled = "{ \"body\": {\"items\":[{\"profileId\":\"mug-a\",\"field\":\"tabletop\"},{\"profileId\":null,\"field\":\"shelf\"}],\"catalogPack\":\"hongdal-product-review-r2\",\"profile\":\"local-slot-review\"},\n \"rights\":\"game-made\",\"visibility\":\"export\",\"where\":null,\"from\":null,\"revision\":2,\"kind\":\"space\",\"id\":\"card:space:local:cafe-corner\",\"format\":\"hongdal-card.v1\" }";
            check("키 순서와 줄바꿈이 달라도 해시가 같음",
                나눔카드Policy.TryNormalize(shuffled, out var shuffledCard, out reason) && 나눔카드Policy.Hash(shuffledCard) == SpaceHash, reason);

            var product = JObject.Parse("{\"format\":\"hongdal-card.v1\",\"id\":\"card:product:local:red-top\",\"kind\":\"product\",\"revision\":1,\"from\":null,\"where\":null,\"visibility\":\"export\",\"rights\":\"game-made\",\"body\":{\"profile\":\"local-slot-review\",\"catalogPack\":\"hongdal-product-review-r2\",\"items\":[{\"field\":\"body\",\"profileId\":\"tee-basic\",\"color\":\"#AA3311\"}]}}");
            check("상의 색은 소문자로 맞추고 상품 카드 해시가 같음",
                나눔카드Policy.TryNormalize(product, out var productCard, out reason) && productCard.Items[0].Color == "#aa3311" && 나눔카드Policy.Hash(productCard) == ProductHash, reason);

            var extra = Space(); extra["owner"] = "someone";
            check("모르는 키(owner 등)가 붙으면 거절", Rejects(extra), null);
            var noWhere = Space(); noWhere.Remove("where");
            check("where 키가 빠지면 거절(검토판과 같음)", Rejects(noWhere), null);
            var where = Space(); where["where"] = "dong:면목제7동";
            check("이 판은 where를 비워 둠 — 지도 핀은 다음 판", Rejects(where), null);
            var open = Space(); open["visibility"] = "public";
            check("공개(public)는 아직 거절 — 파일 내보내기(export)만", Rejects(open), null);
            var rights = Space(); rights["rights"] = "photo";
            check("게임에서 만든 것(game-made)만 나눔", Rejects(rights), null);
            var wrongKind = Space(); wrongKind["kind"] = "product";
            check("id 머리와 kind가 다르면 거절", Rejects(wrongKind), null);
            var zero = Space(); zero["revision"] = 0;
            check("판본 0은 거절", Rejects(zero), null);
            var half = Space(); half["revision"] = 1.5;
            check("판본 1.5는 거절", Rejects(half), null);

            var wear = Space(); ((JArray)wear["body"]["items"]).Add(JObject.Parse("{\"field\":\"head\",\"profileId\":null}"));
            check("공간 카드에 옷 칸(head)이 들어오면 거절", Rejects(wear), null);
            var four = Space(); var fourItems = (JArray)four["body"]["items"];
            fourItems.Add(JObject.Parse("{\"field\":\"floor\",\"profileId\":null}"));
            check("공간 카드 칸 3개는 받음", !Rejects((JObject)four.DeepClone()), null);
            fourItems.Add(JObject.Parse("{\"field\":\"floor\",\"profileId\":\"plant\"}"));
            check("같은 칸 두 번·4개 넘음은 거절", Rejects(four), null);
            var two = (JObject)product.DeepClone(); ((JArray)two["body"]["items"]).Add(JObject.Parse("{\"field\":\"head\",\"profileId\":null}"));
            check("상품 카드는 물건 하나", Rejects(two), null);
            var own = Space(); own["body"]["items"][0]["profileId"] = "user-my-lamp";
            check("내가 만든 물건(user-)은 밖으로 안 나감", Rejects(own), null);
            var noProfile = Space(); ((JObject)noProfile["body"]["items"][0]).Remove("profileId");
            check("profileId 키가 없으면 거절(null은 받음)", Rejects(noProfile), null);
            var headColor = (JObject)product.DeepClone(); headColor["body"]["items"][0]["field"] = "head";
            check("색은 상의 칸만", Rejects(headColor), null);
            var badColor = (JObject)product.DeepClone(); badColor["body"]["items"][0]["color"] = "red";
            check("색은 #RRGGBB만", Rejects(badColor), null);
            var self = Space(); self["from"] = JObject.Parse("{\"id\":\"card:space:local:cafe-corner\",\"revision\":1}");
            check("자기 자신에서 파생은 거절", Rejects(self), null);
            var cross = Space(); cross["from"] = JObject.Parse("{\"id\":\"card:product:local:red-top\",\"revision\":1}");
            check("다른 종류 카드에서 파생은 거절", Rejects(cross), null);
            var bad = 나눔카드Policy.TryNormalize("{not json", out var none, out reason);
            check("깨진 파일은 이유와 함께 거절", !bad && none == null && !string.IsNullOrEmpty(reason), reason);
        }

        static void Lineage(Action<string, bool, string> check)
        {
            나눔카드Policy.TryNormalize(SpaceJson, out var source, out var reason);
            var ok = 나눔카드Policy.TryDerive(source, "card:space:local:cafe-corner-2", new[] { "shelf" }, out var derived, out reason);
            check("변형 카드: 원본·판본을 가리키고 판본 1, 고른 칸만, 검토판과 같은 해시",
                ok && derived.FromId == source.Id && derived.FromRevision == 2 && derived.Revision == 1 &&
                derived.Items.Count == 1 && derived.Items[0].Field == "shelf" && 나눔카드Policy.Hash(derived) == DerivedHash,
                ok ? 나눔카드Policy.Hash(derived) : reason);
            check("남길 칸이 하나도 없으면 변형을 만들지 않음",
                !나눔카드Policy.TryDerive(source, "card:space:local:empty", new string[0], out var empty, out reason) && empty == null, null);

            나눔카드Policy.TryDerive(derived, "card:space:local:cafe-corner-3", new[] { "shelf" }, out var third, out reason);
            var depth = 나눔카드Policy.Lineage(new[] { source, derived, third }, out var missing);
            check("계보 길이: 원본 0 · 변형 1 · 변형의 변형 2",
                depth[source.Id] == 0 && depth[derived.Id] == 1 && depth[third.Id] == 2 && missing.Count == 0, null);

            var orphanDepth = 나눔카드Policy.Lineage(new[] { third }, out missing);
            check("원본이 없으면 '원본 자료 미확인'으로 남김(지우거나 0으로 바꾸지 않음)",
                orphanDepth[third.Id] == 1 && missing.Count == 1 && missing[0] == derived.Id, null);

            var a = new 나눔카드Policy.Card { Id = "card:space:local:a", Kind = "space", Revision = 1, FromId = "card:space:local:b", FromRevision = 1 };
            var b = new 나눔카드Policy.Card { Id = "card:space:local:b", Kind = "space", Revision = 1, FromId = "card:space:local:a", FromRevision = 1 };
            var loop = 나눔카드Policy.Lineage(new[] { a, b }, out missing);
            check("서로를 원본으로 가리키는 고리는 -1로 표시", loop[a.Id] == -1 && loop[b.Id] == -1, null);
        }

        static void Text(Action<string, bool, string> check)
        {
            string reason;
            check("게임 안 이름은 통과", 나눔카드Policy.TextSafe("면목제7동 카페 1호점 창가 자리", out reason) && 나눔카드Policy.TextSafe("기사 A가 좋아한 선반", out reason), reason);
            check("전화번호 꼴은 막음", !나눔카드Policy.TextSafe("연락 010-1234-5678", out reason) && !나눔카드Policy.TextSafe("02 123 4567", out reason), null);
            check("전자우편은 막음", !나눔카드Policy.TextSafe("someone@example.com 으로", out reason), null);
            check("실제 주소 꼴은 막음", !나눔카드Policy.TextSafe("OO로 12길 3", out reason) && !나눔카드Policy.TextSafe("101동 1203호", out reason), null);
            check("링크는 막음", !나눔카드Policy.TextSafe("https://example.com", out reason), null);
            check("280자 넘으면 막음", !나눔카드Policy.TextSafe(new string('가', 281), out reason), null);
        }
    }
}
