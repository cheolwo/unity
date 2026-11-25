using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 아이콘 집중 보기 (2026-11-24 14:15 사용자): 「아이콘 같은 것들을 이제 클릭을 하면은 그것과 관련한 이제 정보들이 집중되어서 표현이 된다거나
    //  그것과 관련한 이제 흐름들이 이렇게 좀 집중돼서 좀 더 강조되어서 표현이 된다거나 하는 식으로 작용을 하면은 좋겠다」
    //  - 전국 지도(절기 장터)의 시설 아이콘을 누르면 그 시설에 「집중」한다.
    //    지도: 이어진 흐름선은 굵고 밝게(트럭 · 화살 · 몫 %), 이어진 시설은 그대로(겹쳐도 먼저 자리), 나머지는 옅게.
    //    오른쪽: 그 시설 카드(이어진 곳 · 값 · 관련 자료 줄). 자료 줄은 지금 입장 카드의 관점 순위대로 먼저 온다.
    //  - 무엇이 이어졌나(자료에 있는 것만):
    //    주산지 → 그 산지가 보내는 시장(가락이면 동네까지) · 시장 ← 보내는 산지(가락이면 → 동네) · 동네 ← 가락 ← 가락으로 오는 산지 ·
    //    산지유통센터 ↔ 같은 품목 센터(배추면 가까운 주산지와 그 흐름) · 위판장 ↔ 이 절기 가장 많은 제철 어종을 받는 위판장.
    //    주산지를 누르면 그 둘레 50 km의 배추 산지유통센터도 밝다.
    //  - 같은 아이콘을 다시 누르거나 빈 곳을 누르면 풀린다. 다른 아이콘을 누르면 바뀐다. 그 시설이 지도에서 사라지면(품목 · 입장을 바꿈) 풀린다.
    //  - 흐름선 굵기 · 몫 %는 예시(공영도매시장 정산정보 「산지명」이 들어오면 실제), 거리는 직선 셈 · 시장 자리는 대략.
    public sealed partial class SagajeongDispatchView
    {
        private string flowFocus;     // "o:해남" 산지 · "m:가락" 시장 · "home" 동네 · "a:12" 산지유통센터(jangteo.apc 번호) · "p:여수" 위판장
        private bool flowFocusSeen;   // 이번에 그린 것 가운데 집중한 것이 있었나(없으면 풀림)
        private readonly HashSet<string> focusOrigins = new HashSet<string>();
        private readonly HashSet<string> focusMarkets = new HashSet<string>();
        private readonly HashSet<string> focusTags = new HashSet<string>();
        private string focusSpecies;  // 위판장 집중: 이어 주는 어종
        private readonly HashSet<string> focusPorts = new HashSet<string>();      // 그 어종을 많이 받는 위판장(위 8곳)
        private const int FocusPortCount = 8;
        private bool focusChain;      // 가락 → 동네 줄을 밝힘
        private readonly List<(Vector2 At, float Radius, string Key)> nationHits = new List<(Vector2, float, string)>();
        private readonly List<(Vector2 At, float Radius, string Key)> nationAreaHits = new List<(Vector2, float, string)>();

        private string FlowFocusKind => flowFocus == null ? null : flowFocus == "home" ? "home" : flowFocus.Substring(0, 1);
        private string FlowFocusName => flowFocus != null && flowFocus.Length > 2 && flowFocus[1] == ':' ? flowFocus.Substring(2) : null;

        // 시도 넓이 원 ↔ 산지(전남광주 원을 누르면 해남, 충북 원을 누르면 괴산)
        private static string RegionOrigin(string region) => region switch { "전남광주" => "해남", "충북" => "괴산", _ => region };
        private static string OriginRegion(string origin) => origin switch { "해남" => "전남광주", "괴산" => "충북", _ => origin };

        private static Vector2 NationLonLat(float x, float y) => new Vector2(126.55f + (x - 525.2468f) / 71.1935f, 37.95f - (y - 165.4f) / 88f);

        // 전국 지도 좌표 두 점 사이 직선 거리(km)
        private static float NationKm(float x1, float y1, float x2, float y2)
        {
            var a = NationLonLat(x1, y1);
            var b = NationLonLat(x2, y2);
            const float rad = Mathf.Deg2Rad;
            var dLat = (b.y - a.y) * rad;
            var dLon = (b.x - a.x) * rad;
            var h = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f) +
                    Mathf.Cos(a.y * rad) * Mathf.Cos(b.y * rad) * Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);
            return 6371f * 2f * Mathf.Atan2(Mathf.Sqrt(h), Mathf.Sqrt(Mathf.Max(0f, 1f - h)));
        }

        private string BigMarketName => flow?.markets?.FirstOrDefault(market => market.big == 1)?.name;

        private JangteoApc FocusApc => FlowFocusKind == "a" && int.TryParse(FlowFocusName, out var index) ? ApcAt(index) : null;
        private JangteoApc ApcAt(int index) => jangteo?.apc != null && index >= 0 && index < jangteo.apc.Length ? jangteo.apc[index] : null;
        private JangteoPort FocusPort => FlowFocusKind == "p" ? jangteo?.ports?.FirstOrDefault(port => port.name == FlowFocusName) : null;

        private static float PortTons(JangteoPort port, string species)
        {
            if (port?.sea == null || port.seaTons == null) return 0f;
            var index = Array.IndexOf(port.sea, species);
            return index >= 0 && index < port.seaTons.Length ? port.seaTons[index] : 0f;
        }

        // 위판장이 이 절기에 가장 많이 받는 제철 어종(수산 품목을 골랐으면 그 어종)
        private string FocusPortSpecies(JangteoPort port, JangteoMonth month)
        {
            if (port?.sea == null || port.seaTons == null) return null;
            if (jangteoPick != null && Array.IndexOf(port.sea, jangteoPick) >= 0) return jangteoPick;
            string best = null, any = null;
            float bestTons = 0f, anyTons = 0f;
            for (var index = 0; index < port.sea.Length && index < port.seaTons.Length; index++)
            {
                var tons = port.seaTons[index];
                if (tons > anyTons) (any, anyTons) = (port.sea[index], tons);
                if (month?.sea != null && Array.IndexOf(month.sea, port.sea[index]) >= 0 && tons > bestTons) (best, bestTons) = (port.sea[index], tons);
            }
            return best ?? any;
        }

        // 센터의 중심 품목: 고른 품목 → 이 절기 제철 첫 품목 → 첫 품목(지도 배지 색과 같은 차례)
        private string FocusApcMain(JangteoApc apc)
        {
            if (apc?.tags == null || apc.tags.Length == 0) return null;
            if (jangteoPick != null && Array.IndexOf(apc.tags, jangteoPick) >= 0) return jangteoPick;
            var month = jangteo != null ? JangteoMonthOf(JangteoShownTerm) : null;
            return apc.tags.FirstOrDefault(tag => month?.food != null && Array.IndexOf(month.food, tag) >= 0) ?? apc.tags[0];
        }

        // 배추를 다루는 센터면 60 km 안의 가장 가까운 산지
        private FlowOrigin FocusNearOrigin(JangteoApc apc, out float km)
        {
            km = 0f;
            if (apc?.tags == null || flow?.origins == null || Array.IndexOf(apc.tags, flow.item) < 0) return null;
            FlowOrigin best = null;
            var bestKm = 60f;
            foreach (var origin in flow.origins)
            {
                var distance = NationKm(apc.x, apc.y, origin.x, origin.y);
                if (distance < bestKm) (best, bestKm) = (origin, distance);
            }
            km = bestKm;
            return best;
        }

        // 다시 그릴 때마다(BeginNationIcons): 무엇이 이어졌는지 셈
        private void PrepareFlowFocus()
        {
            focusOrigins.Clear();
            focusMarkets.Clear();
            focusTags.Clear();
            focusSpecies = null;
            focusPorts.Clear();
            focusChain = false;
            flowFocusSeen = false;
            nationHits.Clear();
            nationAreaHits.Clear();
            if (flowFocus == null) return;
            var name = FlowFocusName;
            var big = BigMarketName;
            var links = flow?.flows ?? Array.Empty<FlowLink>();
            switch (FlowFocusKind)
            {
                case "o":
                    focusOrigins.Add(name);
                    foreach (var link in links.Where(link => link.@from == name))
                    {
                        focusMarkets.Add(link.to);
                        if (link.to == big) focusChain = true;
                    }
                    break;
                case "m":
                    focusMarkets.Add(name);
                    foreach (var link in links.Where(link => link.to == name)) focusOrigins.Add(link.@from);
                    focusChain = name == big;
                    break;
                case "home":
                    focusChain = true;
                    if (big != null) focusMarkets.Add(big);
                    foreach (var link in links.Where(link => link.to == big)) focusOrigins.Add(link.@from);
                    break;
                case "a":
                    var apc = FocusApc;
                    var main = FocusApcMain(apc);      // 같은 품목 센터 = 이 센터의 지금 제철 첫 품목(품목을 골랐으면 그것)
                    if (main != null) focusTags.Add(main);
                    var near = FocusNearOrigin(apc, out _);
                    if (near != null)
                    {
                        focusOrigins.Add(near.name);
                        foreach (var link in links.Where(link => link.@from == near.name)) focusMarkets.Add(link.to);
                    }
                    break;
                case "p":
                    var port = FocusPort;
                    focusSpecies = LoadJangteo() ? FocusPortSpecies(port, JangteoMonthOf(JangteoShownTerm)) : null;
                    if (focusSpecies != null && jangteo.ports != null)
                        foreach (var other in jangteo.ports.Where(other => other != port && PortTons(other, focusSpecies) > 0.05f)
                                     .OrderByDescending(other => PortTons(other, focusSpecies)).Take(FocusPortCount))
                            focusPorts.Add(other.name);
                    break;
            }
        }

        // 집중한 것과 이어졌나(집중이 없으면 늘 참)
        private bool FocusRelated(string key)
        {
            if (flowFocus == null || key == null || key == flowFocus) return true;
            if (key == "home") return focusChain;
            if (key.Length < 3 || key[1] != ':') return false;
            var name = key.Substring(2);
            switch (key[0])
            {
                case 'o':
                    return focusOrigins.Contains(name);
                case 'm':
                    return focusMarkets.Contains(name);
                case 'a':
                    if (!int.TryParse(name, out var index)) return false;
                    var apc = ApcAt(index);
                    if (apc?.tags == null) return false;
                    if (FlowFocusKind == "a") return apc.tags.Any(focusTags.Contains);
                    if (FlowFocusKind == "o" && flow != null && Array.IndexOf(apc.tags, flow.item) >= 0)
                    {
                        var origin = flow.origins?.FirstOrDefault(entry => entry.name == FlowFocusName);
                        return origin != null && NationKm(apc.x, apc.y, origin.x, origin.y) < 50f;
                    }
                    return false;
                case 'p':
                    return FlowFocusKind == "p" && focusPorts.Contains(name);
            }
            return false;
        }

        private float FocusAlpha(string key) => FocusRelated(key) ? 1f : 0.2f;

        private bool FlowLinkLit(FlowLink link)
        {
            if (flowFocus == null) return false;
            return FlowFocusKind switch
            {
                "o" => link.@from == FlowFocusName,
                "m" => link.to == FlowFocusName,
                "home" => focusMarkets.Contains(link.to),
                "a" => focusOrigins.Contains(link.@from),
                _ => false,
            };
        }

        private void NoteFocusArea(Vector2 at, float radius, string key)
        {
            nationAreaHits.Add((at, radius, key));
            if (key == flowFocus) flowFocusSeen = true;
        }

        // 밝힌 흐름선: 금빛 굵은 선 + 번짐 + 끝 화살 + 트럭 셋 + 몫 %(예시)
        private void DrawLitFlow(FlowLink link, Vector2 a, Vector2 control, Vector2 b, float now, float total)
        {
            var width = Mathf.Max(2.2f, link.w * 0.6f);
            var previous = a;
            for (var step = 1; step <= 22; step++)
            {
                var point = FlowBezier(a, control, b, step / 22f);
                Capsule(previous, point, Fade(Gold, 0.22f), width + 4f);
                previous = point;
            }
            previous = a;
            for (var step = 1; step <= 22; step++)
            {
                var point = FlowBezier(a, control, b, step / 22f);
                Capsule(previous, point, Gold, width);
                previous = point;
            }
            var tip = FlowBezier(a, control, b, 0.9f);
            var back = (tip - FlowBezier(a, control, b, 0.84f)).normalized;
            var side = new Vector2(-back.y, back.x);
            Capsule(tip, tip - back * 8f + side * 5f, Gold, 2f);
            Capsule(tip, tip - back * 8f - side * 5f, Gold, 2f);
            for (var truck = 0; truck < 3; truck++)
            {
                var t = mapEffects ? Mathf.Repeat(now * 0.07f + truck / 3f + link.w * 0.11f, 1f) : 0.22f + truck * 0.24f;
                Disc(FlowBezier(a, control, b, t), 1.8f + link.w * 0.22f, Ink(0xFFF3D6));
            }
            if (total > 0f)
                FlowTag(FlowBezier(a, control, b, 0.5f) + new Vector2(5f, -9f), $"{Mathf.RoundToInt(100f * link.w / total)}%", 9, Gold, true, 0.85f, true);
        }

        // 이름(안내 · 마우스 올림 이름표에 씀)
        private string FocusLabel(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (key == "home") return "동네 장바구니";
            var name = key.Length > 2 && key[1] == ':' ? key.Substring(2) : key;
            switch (key[0])
            {
                case 'o': return name + " 산지";
                case 'm': return name == BigMarketName ? "가락시장" : name + " 시장";
                case 'p': return name + " 위판장";
                case 'a':
                    var apc = int.TryParse(name, out var index) ? ApcAt(index) : null;
                    var main = FocusApcMain(apc);
                    return main != null ? main + " 산지유통센터" : "산지유통센터";
            }
            return name;
        }

        // ── 마우스 올림(2026-11-24 정리): 누를 수 있는 아이콘에 고리와 「… · 눌러서 집중」 이름표 ─────────
        private bool nationHoverQuiet;     // 촬영 중에는 실제 마우스를 무시한다
        private string nationHoverProof;   // 촬영용: 이 열쇠에 마우스를 올린 것처럼

        private void DrawNationHover()
        {
            (Vector2 At, float Radius, string Key) found = default;
            var hit = false;
            if (nationHoverProof != null)
            {
                foreach (var entry in nationHits)
                    if (entry.Key == nationHoverProof) (found, hit) = (entry, true);
            }
            else if (!nationHoverQuiet)
            {
                var mouse = Event.current.mousePosition;
                for (var index = nationHits.Count - 1; index >= 0 && !hit; index--)
                    if ((nationHits[index].At - mouse).sqrMagnitude <= Sq(nationHits[index].Radius)) (found, hit) = (nationHits[index], true);
            }
            if (!hit || found.Key == flowFocus) return;
            Hoop(found.At, found.Radius + 2.5f, Fade(Paper, 0.9f), 1.6f);
            NationTag(found.At + new Vector2(found.Radius + 6f, -found.Radius - 6f), FocusLabel(found.Key) + " · 눌러서 집중", 10, Paper, true, 0.94f);
        }

        // ── 고르기: 아이콘(위에 보이는 것 먼저) → 시도 넓이 원 → 빈 곳(풀림) ─────────────

        private bool PickFlowFocus(Vector2 local)
        {
            string key = null;
            for (var index = nationHits.Count - 1; index >= 0 && key == null; index--)
                if ((nationHits[index].At - local).sqrMagnitude <= Sq(nationHits[index].Radius)) key = nationHits[index].Key;
            if (key == null)
            {
                var best = float.MaxValue;
                foreach (var (at, radius, area) in nationAreaHits)
                {
                    var distance = Vector2.Distance(at, local);
                    if (distance <= radius && distance < best) (best, key) = (distance, area);
                }
            }
            if (key == null)
            {
                if (flowFocus == null) return false;
                flowFocus = null;      // 빈 곳: 풀고, 아래 층(거점 · 흐름) 고르기는 그대로 이어 간다
                Debug.Log("Hongdal produce focus: 풀림(빈 곳)");
                return false;
            }
            flowFocus = key == flowFocus ? null : key;
            jangteoListPeek = false;
            nationPick = null;
            Debug.Log("Hongdal produce focus: " + (flowFocus ?? "풀림(같은 아이콘)"));
            return true;
        }

        // 촬영용: 해남에서 가장 가까운 배추 산지유통센터
        private string FocusApcProofKey()
        {
            if (jangteo?.apc == null || flow?.origins == null || flow.origins.Length == 0) return null;
            var haenam = flow.origins.FirstOrDefault(origin => origin.halt == 1) ?? flow.origins[0];
            var best = -1;
            var bestKm = float.MaxValue;
            for (var index = 0; index < jangteo.apc.Length; index++)
            {
                var apc = jangteo.apc[index];
                if (apc.tags == null || Array.IndexOf(apc.tags, flow.item) < 0) continue;
                var km = NationKm(apc.x, apc.y, haenam.x, haenam.y);
                if (km < bestKm) (best, bestKm) = (index, km);
            }
            return best >= 0 ? "a:" + best : null;
        }

        // ── 오른쪽 카드 (논리 좌표) ─────────────────────────────────────────────

        private bool DrawFlowFocusPanel()
        {
            if (flowFocus == null || !LoadJangteo()) return false;
            if ((FlowFocusKind is "o" or "m" or "home") && !LoadFlow()) return false;
            var panel = NationPanel;
            var x = panel.x + 16;
            var width = panel.width - 32;
            var y = panel.y + 10;
            var bottom = panel.yMax - 46;
            string foot;
            switch (FlowFocusKind)
            {
                case "o": foot = DrawFocusOrigin(x, y, width, bottom); break;
                case "m": foot = DrawFocusMarket(x, y, width, bottom); break;
                case "home": foot = DrawFocusHome(x, y, width, bottom); break;
                case "p": foot = DrawFocusPort(x, y, width, bottom); break;
                case "a": foot = DrawFocusApc(x, y, width, bottom); break;
                default: return false;
            }
            if (foot == null)
            {
                flowFocus = null;
                return false;
            }
            if (ExpansionButton(new Rect(panel.xMax - 66, panel.y + 8, 52, 22), "닫기 ×", true, Tile))
            {
                flowFocus = null;
                Debug.Log("Hongdal produce focus: 풀림(닫기)");
            }
            Text(new Rect(x, panel.yMax - 40, width, 34), "같은 아이콘이나 빈 곳을 누르면 풀림 · " + foot, 9, Soft, false, TextAnchor.UpperLeft, true);
            return true;
        }

        private float FocusHeader(float x, float y, float width, string kind, Color tint, string icon, string name, string sub)
        {
            var at = new Vector2(x + 14f, y + 24f);
            Disc(at, 14.5f, Fade(tint, 0.92f));
            Disc(at, 13f, Canvas);
            DrawMapIcon(new Rect(at.x - 10f, at.y - 10f, 20f, 20f), icon, tint);
            Text(new Rect(x + 34, y, width - 96, 16), kind, 10, tint, true);
            Text(new Rect(x + 34, y + 13, width - 94, 28), name, 20, Paper, true);
            Text(new Rect(x, y + 44, width, 30), sub, 10, Soft, false, TextAnchor.UpperLeft, true);
            return y + 78;
        }

        private float FocusTitle(float x, float y, float width, string title, Color color)
        {
            Text(new Rect(x, y, width, 15), title, 10, color, true);
            return y + 17;
        }

        private float FocusBar(float x, float y, float width, string label, string value, float fraction, Color color, string tag = null)
        {
            var tagWidth = tag != null ? 30f : 0f;
            Text(new Rect(x, y, width - 96, 16), label, 11, Paper, true);
            Text(new Rect(x + width - 96 - tagWidth, y, 96, 16), value, 11, Paper, true, TextAnchor.MiddleRight);
            if (tag != null) Text(new Rect(x + width - 28, y + 1, 28, 15), tag, 9, FocusTagColor(tag), true, TextAnchor.MiddleRight);
            Paint(new Rect(x, y + 18, width, 4), Fade(Tile, 0.95f), 2);
            Paint(new Rect(x, y + 18, Mathf.Max(3f, width * Mathf.Clamp01(fraction)), 4), color, 2);
            return y + 27;
        }

        private float FocusLine(float x, float y, float width, string text, Color color, bool bold = false)
        {
            Text(new Rect(x, y, width, 16), text, 10, color, bold);
            return y + 18;
        }

        private static Color FocusTagColor(string tag) => tag == "자료" ? Mint : tag == "추정" ? Ink(0xD7B8FF) : tag == "예시" ? Soft : Gold;

        // 관련 자료 줄: 지금 입장 카드의 관점 순위가 있는 줄 먼저(1~3순위는 금빛), 나머지는 적은 차례
        private float FocusFacts(float x, float y, float width, float bottom, params string[] ids)
        {
            var lens = FlowLensIndex;
            // 필수 · 부가(15:02): 지금 입장의 필수 → 부가 → 그 밖(관점 순위) 차례
            var facts = ids.Select(FlowFactOf).Where(fact => fact != null)
                .Select((fact, order) => (Fact: fact, Order: order, Rank: fact.rank != null && lens < fact.rank.Length ? fact.rank[lens] : 0,
                    Tier: StanceTier(stanceKey, "fact:" + fact.id)))
                .OrderBy(entry => entry.Tier == 1 ? 0 : entry.Tier == 2 ? 1 : 2)
                .ThenBy(entry => entry.Rank > 0 ? 0 : 1).ThenBy(entry => entry.Rank > 0 ? entry.Rank : entry.Order).ToList();
            if (facts.Count == 0 || y + 17 + 34 > bottom) return y;
            y = FocusTitle(x, y, width, lens == 0 ? "관련 자료" : $"{FlowStanceName ?? flow.lenses[lens].name}의 눈으로 먼저", Gold);
            foreach (var (fact, _, rank, tier) in facts)
            {
                if (y + 34 > bottom) break;
                var first = tier == 1 || tier == 0 && StanceInfoSetOf(stanceKey) == null && rank >= 1 && rank <= 3;
                var row = new Rect(x - 4, y, width + 8, 34);
                Paint(row, first ? Fade(Gold, 0.15f) : Fade(Tile, 0.55f), 6);
                if (tier > 0) Paint(new Rect(row.x, row.y + 5, 3, row.height - 10), tier == 1 ? Gold : OptionalTint, 1.5f);
                Text(new Rect(row.x + 8, row.y + 2, row.width - 104, 16), fact.label, 10, first ? Gold : Paper, true);
                Text(new Rect(row.xMax - 100, row.y + 1, 92, 17), fact.value, 11, Paper, true, TextAnchor.MiddleRight);
                Text(new Rect(row.x + 8, row.y + 18, row.width - 48, 14), fact.sub, 9, Soft);
                Text(new Rect(row.xMax - 40, row.y + 18, 32, 14), fact.tag, 9, FocusTagColor(fact.tag), true, TextAnchor.MiddleRight);
                y += 36;
            }
            return y;
        }

        private static int FactWon(FlowFact fact)
        {
            if (fact?.value == null) return 0;
            var value = 0;
            var started = false;
            foreach (var letter in fact.value)
            {
                if (char.IsDigit(letter))
                {
                    value = value * 10 + (letter - '0');
                    started = true;
                }
                else if (letter == ',' && started) continue;
                else if (started) break;
            }
            return value;
        }

        // 이어진 선 줄(산지 → 시장 또는 시장 ← 산지): 몫 %(예시) · 직선 km
        private float FocusLinks(float x, float y, float width, float bottom, IEnumerable<FlowLink> links, bool outbound)
        {
            var list = links.OrderByDescending(link => link.w).ToList();
            var total = list.Sum(link => link.w);
            foreach (var link in list)
            {
                if (y + 27 > bottom) break;
                var origin = flow.origins.FirstOrDefault(entry => entry.name == link.@from);
                var market = flow.markets.FirstOrDefault(entry => entry.name == link.to);
                if (origin == null || market == null) continue;
                var km = NationKm(origin.x, origin.y, market.x, market.y);
                var share = total > 0f ? link.w / total : 0f;
                y = FocusBar(x, y, width, outbound ? link.to : link.@from, $"{Mathf.RoundToInt(share * 100f)}% · {km:0} km", share, Gold, "예시");
            }
            return y;
        }

        private string DrawFocusOrigin(float x, float y, float width, float bottom)
        {
            var origin = flow.origins?.FirstOrDefault(entry => entry.name == FlowFocusName);
            if (origin == null) return null;
            var green = Ink(0x7CDB72);
            var fire = Ink(0xFF7A6B);
            var region = flow.regions?.FirstOrDefault(entry => entry.name == OriginRegion(origin.name));
            var sub = origin.halt == 1 ? "출하정지 대상 지역(농식품부 11/11) · 3만 t을 밭에 남긴다"
                : origin.main == 1 ? "김장철 배추가 많이 나는 곳" : $"{region?.name ?? origin.name}의 가을배추 밭";
            y = FocusHeader(x, y, width, origin.main == 1 ? "김장배추 주산지" : "가을배추 산지(도)", origin.halt == 1 ? fire : green, "cabbage",
                origin.name, sub);
            if (region != null)
            {
                var biggest = flow.regions.Max(entry => entry.ha);
                y = FocusTitle(x, y, width, "심은 넓이 · 가을배추", green);
                y = FocusBar(x, y, width, region.name, $"{region.ha:#,0} ha", region.ha / (float)Mathf.Max(1, biggest), green, "자료");
                if (flow.areaTotal > 0) y = FocusLine(x, y - 2, width, $"전국 {flow.areaTotal:#,0} ha의 {100f * region.ha / flow.areaTotal:0}%", Soft);
            }
            y = FocusTitle(x, y + 2, width, "보내는 곳 · 지도에 금빛으로", Gold);
            y = FocusLinks(x, y, width, bottom, flow.flows.Where(link => link.@from == origin.name), true);
            var centres = jangteo.apc?.Count(apc => apc.tags != null && Array.IndexOf(apc.tags, flow.item) >= 0 &&
                NationKm(apc.x, apc.y, origin.x, origin.y) < 50f) ?? 0;
            if (centres > 0) y = FocusLine(x, y, width, $"둘레 50 km 배추 산지유통센터 {centres}곳(지도에 밝게)", Mint);
            var ids = new List<string>();
            if (origin.halt == 1) ids.Add("halt");
            ids.AddRange(new[] { "farmgate", "supply", "stock", "insure", "cost" });
            if (origin.halt == 1) ids.Insert(1, "route");
            FocusFacts(x, y + 4, width, bottom, ids.ToArray());
            return "선 몫 %는 예시 · 거리는 직선";
        }

        private string DrawFocusMarket(float x, float y, float width, float bottom)
        {
            var market = flow.markets?.FirstOrDefault(entry => entry.name == FlowFocusName);
            if (market == null) return null;
            var big = market.big == 1;
            y = FocusHeader(x, y, width, "공영도매시장", big ? Gold : Paper, "auction", big ? "가락시장" : market.name,
                big ? "공영도매시장 거래량의 약 40%(2020) · 채소 경매는 밤 10:30께" : "공영도매시장 · 지도 자리는 대략");
            if (big && flow.auction != null)
            {
                var live = FlowAuctionLive();
                y = FocusLine(x, y, width, live > 0f ? $"지금 경매 중 · 약 {Mathf.CeilToInt(flow.auction.span * live)}분 남음" :
                    $"다음 경매 밤 {flow.auction.start / 60 - 12}:{flow.auction.start % 60:00} · {flow.auction.span}분쯤", Gold, true);
            }
            y = FocusTitle(x, y + 2, width, "들어오는 곳 · 지도에 금빛으로", Gold);
            y = FocusLinks(x, y, width, bottom, flow.flows.Where(link => link.to == market.name), false);
            if (big)
            {
                var local = FlowFactOf("local");
                y = FocusLine(x, y, width, $"→ 중도매인 → 동네 가게({flow.home?.name ?? "동네"} {local?.value ?? "-"})", Mint);
                FocusFacts(x, y + 4, width, bottom, "auction", "auction_time", "wholesale", "margin", "garak", "consumer");
            }
            else FocusFacts(x, y + 4, width, bottom, "wholesale", "consumer", "cost");
            return "선 몫 %는 예시 · 거리는 직선 · 시장 자리는 대략";
        }

        private string DrawFocusHome(float x, float y, float width, float bottom)
        {
            if (flow.home == null) return null;
            y = FocusHeader(x, y, width, "동네 값 · 장바구니", Mint, "basket", flow.home.name,
                "배추 한 포기가 밭에서 동네 가게까지 오며 붙는 값(날짜가 조금씩 다른 값을 이음)");
            var steps = new[]
            {
                ("농가가 받는 값", FlowFactOf("farmgate"), Ink(0x7CDB72)),
                ("가락 경락", FlowFactOf("auction"), Gold),
                ("도매값 · 중도매인", FlowFactOf("wholesale"), Ink(0xF0913A)),
                ("동네 가게", FlowFactOf("local"), Mint),
            };
            var top = Mathf.Max(1, steps.Max(step => FactWon(step.Item2)));
            y = FocusTitle(x, y, width, "한 포기 값이 붙는 길", Mint);
            foreach (var (label, fact, color) in steps)
            {
                if (fact == null) continue;
                y = FocusBar(x, y, width, label, fact.value, FactWon(fact) / (float)top, color, fact.tag);
            }
            var origins = flow.flows.Where(link => link.to == BigMarketName).Select(link => link.@from).Distinct().Count();
            y = FocusLine(x, y, width, $"가락으로 오는 산지 {origins}곳 → 가락 → 동네(지도에 밝게)", Gold);
            FocusFacts(x, y + 4, width, bottom, "cost", "shop_choice", "kimjang", "consumer", "supply_home");
            return "값 = 서울 동부 21곳 · 전국 소비자 값 · 가락 경락(날짜가 다름)";
        }

        private string DrawFocusPort(float x, float y, float width, float bottom)
        {
            var port = FocusPort;
            if (port == null) return null;
            var month = JangteoMonthOf(JangteoShownTerm);
            y = FocusHeader(x, y, width, "위판장 · 수협 위판", NationSea, "fish-market", port.name, $"많이 나는 어종 {port.top}");
            y = FocusLine(x, y, width, $"10월 위판 {JangteoTons(port.tons)}", Paper, true);
            y = FocusTitle(x, y + 2, width, "어종별 10월 위판 · 제철은 밝게", NationSea);
            if (port.sea != null && port.seaTons != null)
            {
                var rows = port.sea.Select((species, index) => (Species: species, Tons: index < port.seaTons.Length ? port.seaTons[index] : 0f))
                    .Where(row => row.Tons > 0.05f).OrderByDescending(row => row.Tons).Take(6).ToList();
                var top = rows.Count > 0 ? rows[0].Tons : 1f;
                foreach (var (species, tons) in rows)
                {
                    if (y + 27 > bottom) break;
                    var season = month?.sea != null && Array.IndexOf(month.sea, species) >= 0;
                    y = FocusBar(x, y, width, season ? $"{species} · 제철" : species, JangteoTons(tons), tons / top, season ? NationSea : Fade(Soft, 0.6f), "자료");
                }
            }
            if (focusSpecies != null && jangteo.ports != null)
            {
                var same = jangteo.ports.Where(other => other.name != port.name && PortTons(other, focusSpecies) > 0.05f)
                    .OrderByDescending(other => PortTons(other, focusSpecies)).ToList();
                y = FocusTitle(x, y + 4, width, $"{focusSpecies}을(를) 받는 다른 위판장 {same.Count}곳", NationSea);
                y = FocusLine(x, y - 2, width, $"많이 받는 {Mathf.Min(FocusPortCount, same.Count)}곳을 지도에 밝게", Soft);
                foreach (var other in same.Take(3))
                {
                    if (y + 18 > bottom) break;
                    y = FocusLine(x, y, width, $"{other.name} {JangteoTons(PortTons(other, focusSpecies))} · {NationKm(port.x, port.y, other.x, other.y):0} km", Paper);
                }
            }
            return "위판장 자리 = 항구 대표점 · 수협 10월 위판";
        }

        private string DrawFocusApc(float x, float y, float width, float bottom)
        {
            var apc = FocusApc;
            if (apc?.tags == null || apc.tags.Length == 0) return null;
            var month = JangteoMonthOf(JangteoShownTerm);
            var main = FocusApcMain(apc);
            bool Season(string tag) => month?.food != null && Array.IndexOf(month.food, tag) >= 0;
            var ordered = apc.tags.OrderBy(tag => tag == main ? 0 : Season(tag) ? 1 : 2).ToList();
            var tint = JangteoKind(jangteoItems.TryGetValue(main, out var mainItem) ? mainItem.kind : string.Empty);
            var title = ordered.Count <= 2 ? string.Join(" · ", ordered) : $"{ordered[0]} · {ordered[1]} 외 {ordered.Count - 2}";
            y = FocusHeader(x, y, width, "산지유통센터 · 선별 · 포장", tint, "farm-hub", title,
                "모아서 고르고 포장해 시장 · 마트로 보내는 곳(이름은 두지 않음)");
            var near = FocusNearOrigin(apc, out var km);
            var reserve = near != null && LoadFlow() ? 64f : 0f;
            y = FocusTitle(x, y, width, "다루는 품목 · 서울 동부 값 · 전국 센터 수", tint);
            var drawn = 0;
            foreach (var tag in ordered)
            {
                if (y + 21 > bottom - reserve - 40) break;
                var season = Season(tag);
                var price = "값 없음";
                var color = Fade(Soft, 0.7f);
                if (jangteoItems.TryGetValue(tag, out var item))
                {
                    color = season ? JangteoKind(item.kind) : Fade(JangteoKind(item.kind), 0.55f);
                    if (item.prices != null)
                        for (var index = item.prices.Length - 1; index >= 0; index--)
                            if (item.prices[index] > 0)
                            {
                                price = $"{JangteoWon(item.prices[index])} · {index + 1}월";
                                break;
                            }
                }
                var count = jangteo.apc.Count(other => other.tags != null && Array.IndexOf(other.tags, tag) >= 0);
                var row = new Rect(x - 4, y, width + 8, 20);
                if (tag == main) Paint(row, Fade(tint, 0.16f), 5);
                Text(new Rect(x, y + 1, 92, 18), season ? $"{tag} · 제철" : tag, 11, color, true);
                Text(new Rect(x + 92, y + 1, 56, 18), $"{count}곳", 10, Soft, false, TextAnchor.MiddleRight);
                Text(new Rect(x + width - 92, y + 1, 92, 18), price, 10, Paper, false, TextAnchor.MiddleRight);
                y += 21;
                drawn++;
            }
            if (drawn < ordered.Count) y = FocusLine(x, y, width, $"그 밖 {ordered.Count - drawn}품목", Soft);
            var same = jangteo.apc.Count(other => other.tags != null && Array.IndexOf(other.tags, main) >= 0);
            y = FocusLine(x, y + 3, width, $"같은 {main} 센터 {same}곳을 지도에 밝게", Mint, true);
            if (near != null && LoadFlow())
            {
                y = FocusLine(x, y + 4, width, $"가까운 배추 산지 {near.name} · {km:0} km", Gold, true);
                y = FocusLine(x, y, width, "→ 그 산지가 보내는 시장이 금빛으로", Gold);
                FocusFacts(x, y + 4, width, bottom, "farmgate", "supply", "halt");
            }
            return "센터 자리 = 산지유통센터 목록 · 값 = 서울 동부 월평균";
        }
    }
}
