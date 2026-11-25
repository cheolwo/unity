using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 농수산물 흐름 · 관점 필터 (2026-11-24 12:24 · 12:39 사용자).
    //  12:24 「생산이 된 거가 … 도매 시장으로 가고 … 경매를 거쳐가지고 도매상한테 넘어가고 … 우리 식탁에 오르는 그런 흐름을 … 현실의 데이터를 좀 근거해가지고」
    //  12:39 「주부 관점에서 도매상 관점에서 … 화물 트럭 운전하시는 분 관점에서 … 각각의 입장에서 딱 필요한 데이터만 제공을 하고 싶다」
    //  - 절기 장터(전국 지도) 위에 배추(가을 · 김장철)의 흐름을 그린다: 시도별 심은 넓이 · 주산지와 출하정지 · 산지 → 공영도매시장 선 ·
    //    가락 경매 시각(밤 10:30에 숨) · 동네(사가정) 값.
    //  - 관점(전체 · 주부 · 도매상 · 트럭 · 농가 · 가게)을 고르면 지도 층과 오른쪽 자료 줄이 그 관점의 순위대로만 남는다.
    //    12:54 「역지사지하는 … 카드에 … 놓일 수 있는 거니까」: 관점은 따로 고르지 않고 입장 카드(SagajeongDispatchStance.cs)를 따른다.
    //    동네 주민 → 주부의 눈 · 도매상 · 화물 · 창고 → 트럭의 눈 · 농가 · 음식점 사장 → 가게의 눈 · 배달 기사 · 홍달 경영자 · 직접 고르기 → 전체.
    //    순위는 자료 파일에 있다(층 · 자료 한 줄마다 관점별 숫자, 0 = 안 보임). 새 자료를 더할 때도 순위부터 적는다.
    //  - 꼬리표: 자료 · 셈 · 추정. 흐름선 굵기 · 시장 자리 · 트럭 점은 예시(공영도매시장 정산정보 「산지명」이 들어오면 실제).
    // 자료: Resources/hongdal-data-produce-flow.json ← artifacts/local/public-data/produce-flow-20261124/build_produce_flow.py
    public sealed partial class SagajeongDispatchView
    {
        private const string FlowResource = "hongdal-data-produce-flow";

        [Serializable] private sealed class FlowPack { public string schema, item, season, asOf, note; public FlowLens[] lenses; public FlowLayer[] layers; public FlowRegion[] regions; public int areaTotal, areaRest; public FlowOrigin[] origins; public FlowMarket[] markets; public FlowLink[] flows; public FlowHome home; public FlowAuction auction; public FlowFact[] facts; }
        [Serializable] private sealed class FlowLens { public string id, name, hint; public string[] stances; }
        [Serializable] private sealed class FlowLayer { public string id, name; public int[] rank; }
        [Serializable] private sealed class FlowRegion { public string name; public int ha; public float x, y; }
        [Serializable] private sealed class FlowOrigin { public string name; public float x, y; public int main, halt; }
        [Serializable] private sealed class FlowMarket { public string name; public float x, y; public int big; }
        [Serializable] private sealed class FlowLink { public string @from, to; public float bend, w; }
        [Serializable] private sealed class FlowHome { public string name; public float x, y; }
        [Serializable] private sealed class FlowAuction { public string market; public int start, span; }
        [Serializable] private sealed class FlowFact { public string id, label, value, sub, tag, source; public int[] rank; }

        private FlowPack flow;
        private bool flowTried;
        private bool jangteoListPeek;             // 입장의 눈으로 보다가 제철 목록을 잠깐 펼침
        private int jangteoClockProof = -1;       // 촬영용 게임 시각(분). -1 = 게임 시계

        private bool LoadFlow()
        {
            if (flowTried) return flow != null;
            flowTried = true;
            var pack = ReadMapResource<FlowPack>(FlowResource);
            if (pack?.lenses == null || pack.lenses.Length == 0 || pack.layers == null || pack.facts == null)
            {
                Debug.LogWarning("Hongdal produce flow: 흐름 자료가 없어 관점 필터를 켜지 않습니다.");
                return false;
            }
            flow = pack;
            Debug.Log($"Hongdal produce flow: {pack.item} · 관점 {pack.lenses.Length} · 층 {pack.layers.Length} · 자료 줄 {pack.facts.Length} · " +
                $"흐름선 {pack.flows?.Length ?? 0} · 시장 {pack.markets?.Length ?? 0}");
            return true;
        }

        // 지금 입장 카드가 쓰는 관점(lenses 순서). 못 찾으면 0 = 전체
        private int FlowLensIndex
        {
            get
            {
                if (flow?.lenses == null || string.IsNullOrEmpty(stanceKey)) return 0;
                var index = Array.FindIndex(flow.lenses, lens => lens.stances != null && Array.IndexOf(lens.stances, stanceKey) >= 0);
                return index < 0 ? 0 : index;
            }
        }

        private string FlowStanceName => Array.Find(Stances, item => item.Key == stanceKey)?.Name;

        // 배추 흐름은 품목을 안 골랐거나 배추를 골랐을 때만
        private bool FlowItemShown => flow != null && (jangteoPick == null || jangteoPick == flow.item);

        private int FlowLayerRank(string id)
        {
            var layer = flow?.layers?.FirstOrDefault(entry => entry.id == id);
            return layer?.rank != null && FlowLensIndex < layer.rank.Length ? layer.rank[FlowLensIndex] : 0;
        }

        // 산지유통센터 · 위판장은 흐름 자료가 없어도 전처럼 보인다
        private bool FlowLayerOn(string id)
        {
            if (!LoadFlow()) return id is "apc" or "port";
            if (id is "apc" or "port") return FlowLayerRank(id) > 0;
            return FlowItemShown && FlowLayerRank(id) > 0;
        }

        private FlowFact FlowFactOf(string id) => flow?.facts?.FirstOrDefault(fact => fact.id == id);

        private int FlowClockMinutes()
        {
            if (jangteoClockProof >= 0) return jangteoClockProof;
            var now = state?.now;
            if (string.IsNullOrEmpty(now) || now.Length < 5) return -1;
            return int.TryParse(now.Substring(0, 2), out var hour) && int.TryParse(now.Substring(3, 2), out var minute) ? hour * 60 + minute : -1;
        }

        // 가락 경매가 열려 있으면 1(막 시작) → 0(끝 무렵), 아니면 -1
        private float FlowAuctionLive()
        {
            var minutes = FlowClockMinutes();
            if (minutes < 0 || flow?.auction == null || flow.auction.span <= 0) return -1f;
            var since = (minutes - flow.auction.start + 1440) % 1440;
            return since <= flow.auction.span ? 1f - since / (float)flow.auction.span : -1f;
        }

        private static Vector2 FlowBezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        private bool FlowEnds(FlowLink link, out Vector2 a, out Vector2 control, out Vector2 b)
        {
            a = control = b = Vector2.zero;
            var origin = flow.origins?.FirstOrDefault(entry => entry.name == link.@from);
            var market = flow.markets?.FirstOrDefault(entry => entry.name == link.to);
            if (origin == null || market == null) return false;
            a = NationPoint(origin.x, origin.y);
            b = NationPoint(market.x, market.y);
            var middle = (a + b) * 0.5f;
            var span = b - a;
            control = middle + new Vector2(-span.y, span.x) * link.bend;
            return true;
        }

        // ── 지도(Field 안쪽 좌표): 산지유통센터 · 위판장 밑에 깔리는 것 ─────────────────

        private void DrawFlowUnder(float now)
        {
            if (!LoadFlow() || !FlowItemShown) return;
            var green = Ink(0x7CDB72);
            if (FlowLayerOn("area") && flow.regions != null)
            {
                var strong = FlowLayerRank("area") == 1;
                foreach (var region in flow.regions)
                {
                    var point = NationPoint(region.x, region.y);
                    var radius = 4f + Mathf.Sqrt(region.ha) * 0.36f;
                    var key = "o:" + RegionOrigin(region.name);
                    var alpha = FocusAlpha(key);
                    var lit = flowFocus != null && alpha > 0.5f;           // 집중한 것과 이어진 산지의 시도
                    Disc(point, radius, Fade(green, (strong || lit ? 0.26f : 0.13f) * alpha));
                    Hoop(point, radius, Fade(green, (strong || lit ? 0.7f : 0.4f) * alpha), lit ? 1.8f : 1.2f);
                    if (FlowLensIndex != 0 && strong || lit && key != flowFocus)      // 집중한 산지의 넓이는 카드에 있다
                        FlowTag(point + new Vector2(radius + 3f, -6f), $"{region.name} {region.ha:#,0} ha", 9, Fade(green, alpha), true, 0.82f * alpha);
                    if (flow.origins != null && flow.origins.Any(origin => origin.name == RegionOrigin(region.name))) NoteFocusArea(point, radius, key);
                }
            }
            if (flow.flows == null) return;
            var layerOn = FlowLayerOn("flow");
            if (!layerOn && flowFocus == null) return;          // 관점이 감춘 흐름도, 집중한 것과 이어진 선은 보인다
            var main = FlowLayerRank("flow") == 1;
            var dim = flowFocus != null ? 0.15f : 1f;
            foreach (var link in flow.flows)
            {
                if (!layerOn || FlowLinkLit(link)) continue;
                if (!FlowEnds(link, out var a, out var control, out var b)) continue;
                var color = Fade(Gold, (main ? 0.8f : 0.45f) * dim);
                var width = Mathf.Max(1.1f, link.w * (main ? 0.5f : 0.32f));
                var previous = a;
                for (var step = 1; step <= 18; step++)
                {
                    var point = FlowBezier(a, control, b, step / 18f);
                    Capsule(previous, point, color, width);
                    previous = point;
                }
                if (!main || flowFocus != null) continue;
                for (var truck = 0; truck < 2; truck++)       // 밤길의 배추 트럭(움직임 표현)
                {
                    var t = mapEffects ? Mathf.Repeat(now * 0.05f + truck * 0.5f + link.w * 0.11f, 1f) : 0.33f + truck * 0.33f;
                    Disc(FlowBezier(a, control, b, t), 1.4f + link.w * 0.2f, Ink(0xFFF3D6));
                }
            }
            if (flowFocus == null) return;
            var litTotal = flow.flows.Where(FlowLinkLit).Sum(link => link.w);
            foreach (var link in flow.flows.Where(FlowLinkLit))      // 집중: 이어진 선은 맨 위에 굵고 밝게
                if (FlowEnds(link, out var a, out var control, out var b)) DrawLitFlow(link, a, control, b, now, litTotal);
        }

        // ── 지도: 위에 얹는 것(주산지 · 시장 · 동네 · 경매 숨) ──────────────────────

        private void DrawFlowOver(float now)
        {
            if (!LoadFlow() || !FlowItemShown) return;
            var green = Ink(0x7CDB72);
            var fire = Ink(0xFF7A6B);
            var live = FlowAuctionLive();
            var focus = FlowLensIndex != 0;
            var focusing = flowFocus != null;
            if (flow.origins != null)
                foreach (var origin in flow.origins)
                {
                    var key = "o:" + origin.name;
                    var related = focusing && FocusRelated(key);
                    if (!(FlowLayerOn("halt") && origin.main == 1) && !related) continue;      // 주산지가 아닌 산지는 집중할 때만 배지
                    var point = NationPoint(origin.x, origin.y);
                    var alpha = FocusAlpha(key);
                    if (origin.halt == 1) Hoop(point, 14f, Fade(fire, 0.9f * alpha), 2f);      // 출하정지: 붉은 고리
                    NationIconMark(point, "cabbage", green, origin.main == 1 ? 10f : 8.5f, 100f, key);      // 산지: 배추(늘 배지)
                    if ((focus || related) && origin.main == 1)
                        FlowTag(point + new Vector2(-14f, 10f), origin.halt == 1 ? $"{origin.name} · 출하정지" : $"{origin.name} · 주산지", 9,
                            Fade(origin.halt == 1 ? fire : green, alpha), false, 0.86f * alpha);
                }
            FlowMarket garak = null;
            if (flow.markets != null)
                foreach (var market in flow.markets)
                {
                    var key = "m:" + market.name;
                    var related = focusing && FocusRelated(key);
                    if (!FlowLayerOn("market") && !related) continue;
                    var point = NationPoint(market.x, market.y);
                    var alpha = FocusAlpha(key);
                    if (market.big == 1)
                    {
                        garak = market;
                        if (FlowLayerOn("pulse") && live > 0f)
                        {
                            var beat = mapEffects ? 0.5f + 0.5f * Mathf.Sin(now * 4f) : 0.6f;
                            Disc(point, 14f + 8f * beat, Fade(Gold, (0.14f + 0.22f * live) * alpha));
                        }
                        NationIconMark(point, "auction", Gold, 11f, 100f, key);                  // 가락: 큰 경매 망치(늘 배지)
                    }
                    else
                    {
                        NationIconMark(point, "auction", Paper, 7.5f, 30f, key);                 // 공영도매시장: 경매 망치(겹치면 점)
                        if (related) FlowTag(point + new Vector2(10f, 5f), market.name, 9, Paper, true, 0.8f);
                    }
                }
            if (flow.home != null && (FlowLayerOn("local") || focusing && FocusRelated("home")))
            {
                var point = NationPoint(flow.home.x, flow.home.y);
                var alpha = FocusAlpha("home");
                var fact = FlowFactOf("local");
                var tag = point + new Vector2(124f, -24f);      // 서울 둘레는 이름표가 많아 오른쪽으로 빼고 줄로 잇는다
                var badgeAt = tag - new Vector2(12f, 0f);       // 동네는 가락과 거의 겹치므로 장바구니 배지는 줄 끝(이름표 앞)에, 제자리는 작은 점
                if (focusing && focusChain && garak != null)
                {
                    // 집중: 가락 경매 → 중도매인 → 동네 가게 줄을 밝게(점 셋이 흘러감)
                    var from = NationPoint(garak.x, garak.y);
                    var end = badgeAt - new Vector2(10f, 0f);
                    Capsule(from, end, Fade(Mint, 0.25f), 6f);
                    Capsule(from, end, Mint, 2.2f);
                    for (var dot = 0; dot < 3; dot++)
                    {
                        var t = mapEffects ? Mathf.Repeat(now * 0.25f + dot / 3f, 1f) : 0.25f + dot * 0.25f;
                        Disc(Vector2.Lerp(from, end, t), 2f, Ink(0xE6FFF6));
                    }
                    FlowTag(tag + new Vector2(0f, -17f), "가락 경매 → 중도매인 → 동네 가게", 9, Mint, true, 0.85f);
                }
                else Capsule(point, badgeAt - new Vector2(10f, 0f), Fade(Mint, 0.55f * alpha), 1f);
                Disc(point, 3.2f, Fade(Canvas, 0.85f * alpha));
                Disc(point, 2.3f, Fade(Mint, alpha));
                NationIconMark(badgeAt, "basket", Mint, 9f, 100f, "home");                     // 동네 장바구니 값(늘 배지)
                FlowTag(tag, fact != null ? $"동네 배추 {fact.value}" : flow.home.name, 10, Fade(Mint, alpha), true, 0.9f * alpha);
            }
            if (garak != null && (focus || focusing && FocusRelated("m:" + garak.name)))
            {
                var alpha = FocusAlpha("m:" + garak.name);
                var point = NationPoint(garak.x, garak.y);
                var tag = point + new Vector2(112f, 2f);
                Capsule(point, tag - new Vector2(2f, 0f), Fade(Gold, 0.55f * alpha), 1f);
                FlowTag(tag, FlowLayerOn("pulse") && live > 0f ? "가락 · 지금 경매 중" : "가락시장", 10, Fade(Gold, alpha), true, 0.9f * alpha);
            }
        }

        // ── 전국 지도 시설 아이콘 배지(2026-11-24 13:50 사용자: 「전국 단위에서 다루는 시설물 데이터의 경우는 점으로서 표현하지 말고
        //    그 시설물에 어울리는 그런 이제 아이콘 또는 이제 형상을 갖춰가지고 표현이 되면 좋겠다」) ─────────────
        //  산지유통센터 = 선별 · 포장 창고(farm-hub, 품목 색) · 위판장 = 물고기(fish-market) · 공영도매시장 = 경매 망치(auction) ·
        //  주산지 = 배추(cabbage) · 동네 값 = 장바구니(basket). 그림은 Resources/HongdalMapIcons(흰 획 48점, 구 지도 아이콘과 같은 꼴).
        //  배지 = 어두운 원 + 갈래 색 테두리 + 갈래 색 아이콘. 같은 칸에 이미 배지가 있으면 작은 점으로 둔다(always는 늘 배지).
        //  앞섬(priority): 주산지 · 가락 · 동네 = 100(늘 배지) · 위판장 = 50 + √물량 · 공영도매시장 = 30 · 산지센터 = 10(품목을 고르면 20).
        //  앞선 배지와 겹치면 작은 점으로 물린다. 그리는 차례는 낮은 것 먼저, 높은 것 위. 이름표(FlowTag)는 배지 뒤에 얹는다.
        //  집중 보기(14:15): 집중한 것은 늘 배지 · 고리, 이어진 것은 앞섬 +200, 아닌 것은 옅게(0.22). 그린 배지 · 점은 누를 자리로 남긴다.
        private readonly List<(Vector2 At, string Icon, Color Colour, float Badge, float Priority, bool Always, float Alpha, string Key)> nationBadges =
            new List<(Vector2, string, Color, float, float, bool, float, string)>();
        private readonly List<int> nationOrder = new List<int>();
        private readonly List<int> nationKept = new List<int>();
        private readonly List<(Vector2 At, string Text, int Size, Color Colour, bool Left, float Alpha, bool Soft)> nationTags =
            new List<(Vector2, string, int, Color, bool, float, bool)>();
        private readonly List<Rect> nationPlaced = new List<Rect>();
        private bool nationCollecting;

        private void BeginNationIcons()
        {
            nationBadges.Clear();
            nationTags.Clear();
            nationCollecting = true;
            PrepareFlowFocus();      // 아이콘 집중 보기(SagajeongDispatchProduceFocus.cs)
        }

        // key = 집중 보기 열쇠("o:해남" · "m:가락" · "home" · "a:12" · "p:여수").
        private void NationIconMark(Vector2 at, string icon, Color colour, float badge, float priority, string key = null)
        {
            var always = priority >= 100f;
            var alpha = 1f;
            if (flowFocus != null && key != null)
            {
                if (key == flowFocus)
                {
                    flowFocusSeen = true;
                    always = true;
                    priority = 1000f;
                }
                else if (FocusRelated(key)) priority += 200f;
                else alpha = 0.22f;
            }
            if (!nationCollecting || MapIcon(icon) == null)
            {
                Disc(at, badge * 0.55f + 1.2f, Fade(Canvas, 0.85f * alpha));
                Disc(at, badge * 0.55f, Fade(colour, 0.9f * alpha));
                if (key != null) nationHits.Add((at, badge * 0.55f + 3f, key));
                return;
            }
            nationBadges.Add((at, icon, colour, badge, priority, always, alpha, key));
        }

        // 전국 지도 이름표: 배지를 모으는 동안은 미뤄서 배지 위에 얹는다. soft(흐름선 몫 % 등)는 다른 이름표 · 배지와 겹치면 건너뛴다.
        private void FlowTag(Vector2 at, string text, int size, Color colour, bool left, float alpha, bool soft = false)
        {
            if (nationCollecting) nationTags.Add((at, text, size, colour, left, alpha, soft));
            else NationTag(at, text, size, colour, left, alpha);
        }

        private Rect NationTagRect(Vector2 at, string text, int size, bool left)
        {
            var width = GuNameSize(text, size).x + 12f;
            return new Rect(left ? at.x : at.x - width, at.y - 10f, width, 20f);
        }

        private void EndNationIcons()
        {
            nationCollecting = false;
            nationOrder.Clear();
            for (var index = 0; index < nationBadges.Count; index++) nationOrder.Add(index);
            nationOrder.Sort((a, b) =>
            {
                var byPriority = nationBadges[b].Priority.CompareTo(nationBadges[a].Priority);
                return byPriority != 0 ? byPriority : a.CompareTo(b);
            });
            nationKept.Clear();
            foreach (var index in nationOrder)
            {
                var mark = nationBadges[index];
                var free = mark.Always;
                if (!free)
                {
                    free = true;
                    foreach (var other in nationKept)
                    {
                        var them = nationBadges[other];
                        if ((them.At - mark.At).sqrMagnitude < Sq(them.Badge + mark.Badge + 1.5f))
                        {
                            free = false;
                            break;
                        }
                    }
                }
                if (free) nationKept.Add(index);
                else
                {
                    Disc(mark.At, 3f, Fade(Canvas, 0.8f * mark.Alpha));
                    Disc(mark.At, 2.1f, Fade(mark.Colour, 0.9f * mark.Alpha));
                    if (mark.Key != null) nationHits.Add((mark.At, 5f, mark.Key));
                }
            }
            nationPlaced.Clear();
            for (var order = nationKept.Count - 1; order >= 0; order--)
            {
                var mark = nationBadges[nationKept[order]];
                DrawNationBadge(mark);
                if (mark.Key != null) nationHits.Add((mark.At, mark.Badge + 2f, mark.Key));
                if (mark.Alpha > 0.5f) nationPlaced.Add(new Rect(mark.At.x - mark.Badge, mark.At.y - mark.Badge, mark.Badge * 2f, mark.Badge * 2f));
            }
            foreach (var (at, text, size, colour, left, alpha, soft) in nationTags)
                if (!soft) nationPlaced.Add(NationTag(at, text, size, colour, left, alpha));
            foreach (var (at, text, size, colour, left, alpha, soft) in nationTags)
            {
                if (!soft) continue;
                var rect = NationTagRect(at, text, size, left);
                if (nationPlaced.Any(other => other.Overlaps(rect))) continue;
                nationPlaced.Add(NationTag(at, text, size, colour, left, alpha));
            }
            DrawNationHover();      // 마우스 올림 고리 · 이름표(SagajeongDispatchProduceFocus.cs)
            nationBadges.Clear();
            nationTags.Clear();
            if (flowFocus != null && !flowFocusSeen)
            {
                Debug.Log("Hongdal produce focus: 지도에서 사라져 풀림 " + flowFocus);
                flowFocus = null;
            }
        }

        private static float Sq(float value) => value * value;

        private void DrawNationBadge((Vector2 At, string Icon, Color Colour, float Badge, float Priority, bool Always, float Alpha, string Key) mark)
        {
            if (mark.Key != null && mark.Key == flowFocus)
            {
                Disc(mark.At, mark.Badge + 10f, Fade(mark.Colour, 0.2f));      // 집중한 것: 번짐 + 고리
                Hoop(mark.At, mark.Badge + 5f, mark.Colour, 2f);
            }
            Disc(mark.At, mark.Badge + 1.4f, Fade(mark.Colour, 0.92f * mark.Alpha));
            Disc(mark.At, mark.Badge, Fade(Canvas, 0.96f * mark.Alpha));
            var side = mark.Badge * 1.6f;
            DrawMapIcon(new Rect(mark.At.x - side / 2f, mark.At.y - side / 2f, side, side), mark.Icon, Fade(mark.Colour, mark.Alpha));
        }

        // 입장의 눈으로 보다가 제철 목록을 펼쳤을 때: 목록 위 오른쪽에 돌아가는 단추(논리 좌표)
        private void DrawFlowPeekBack()
        {
            if (!jangteoListPeek || flow == null || FlowLensIndex == 0 || jangteoPick != null) return;
            var panel = NationPanel;
            if (ExpansionButton(new Rect(panel.xMax - 96, panel.y + 8, 80, 22), "← 입장의 눈", true, Tile)) jangteoListPeek = false;
        }

        // ── 왼쪽 아래: 누구의 눈으로 볼까 (논리 좌표) ─────────────────────────────

        private bool DrawFlowLensBox(Rect box)
        {
            if (!LoadFlow()) return false;
            Paint(box, Panel, 10);
            var stance = Array.Find(Stances, item => item.Key == stanceKey);
            var tint = stance?.Tint ?? Soft;
            Paint(new Rect(box.x + 13, box.y + 10, 9, 9), tint, 4.5f);
            Text(new Rect(box.x + 28, box.y + 5, box.width - 36, 18), stance != null ? $"입장 · {stance.Name}" : "입장 · 아직 고르지 않음", 11, Gold, true);
            var lens = flow.lenses[FlowLensIndex];
            Text(new Rect(box.x + 13, box.y + 23, box.width - 22, 28), FlowLensIndex == 0 ? "장터를 모두 봅니다" : "장터에서 · " + lens.hint, 9, Mint, true,
                TextAnchor.UpperLeft, true);      // 2026-11-24 정리: 한 줄이면 끝이 잘려 두 줄로
            if (FlowLensIndex == 0)
            {
                var legend = new[] { ("farm-hub", "산지센터", JangteoKind("채소")), ("fish-market", "위판장", NationSea), ("auction", "도매시장", Gold) };
                for (var index = 0; index < legend.Length; index++)
                {
                    var at = new Vector2(box.x + 21 + index * 58f, box.y + 48);
                    Disc(at, 9.2f, Fade(legend[index].Item3, 0.92f));
                    Disc(at, 7.9f, Canvas);
                    DrawMapIcon(new Rect(at.x - 6.3f, at.y - 6.3f, 12.6f, 12.6f), legend[index].Item1, legend[index].Item3);
                    Text(new Rect(at.x + 10, at.y - 8, 48, 16), legend[index].Item2, 9, Paper);
                }
                Text(new Rect(box.x + 13, box.y + 61, box.width - 20, 15), "센터 색 = 품목 · 금빛 선 = 흐름(예시)", 9, Soft);
            }
            else
            {
                var shown = flow.layers.Where(layer => layer.rank != null && FlowLensIndex < layer.rank.Length && layer.rank[FlowLensIndex] > 0)
                    .OrderBy(layer => layer.rank[FlowLensIndex]).Select(layer => layer.name).ToArray();
                Text(new Rect(box.x + 13, box.y + 50, box.width - 20, 28), "지도에 남는 것: " + string.Join(" · ", shown.Take(4)), 9, Soft, false, TextAnchor.UpperLeft, true);
            }
            // 아이콘 누르기 안내(집중 보기 · SagajeongDispatchProduceFocus.cs)
            var guide = new Rect(box.x + 10, box.y + box.height - 58, box.width - 20, 22);
            Paint(guide, flowFocus != null ? Fade(Gold, 0.16f) : Fade(Tile, 0.6f), 5);
            Text(guide, flowFocus != null ? $"집중 · {FocusLabel(flowFocus)}" : "아이콘을 누르면 이어진 것만 밝게", 9,
                flowFocus != null ? Gold : Paper, flowFocus != null, TextAnchor.MiddleCenter);
            if (ExpansionButton(new Rect(box.x + 10, box.y + box.height - 32, box.width - 20, 24), "역지사지 카드 · 입장 바꾸기", true, Tile))
            {
                stancePickerOpen = true;
                Debug.Log("Hongdal produce flow: 입장 카드 열기(전국 지도)");
            }
            return true;
        }

        // ── 오른쪽 패널: 그 관점에서 먼저 볼 것 (논리 좌표) ─────────────────────────

        private bool DrawFlowLensPanel()
        {
            if (!LoadFlow() || FlowLensIndex == 0 || flow.facts == null || jangteoListPeek) return false;
            if (jangteoPick != null && jangteoPick != flow.item) return false;
            var panel = NationPanel;
            var lens = flow.lenses[FlowLensIndex];
            var x = panel.x + 16;
            var width = panel.width - 32;
            var y = panel.y + 10;
            if (ExpansionButton(new Rect(panel.xMax - 84, y, 68, 24), "제철 목록", true, Tile))
            {
                jangteoListPeek = true;
                jangteoPick = null;
                return true;
            }
            Text(new Rect(x, y, width - 84, 16), $"{FlowStanceName ?? lens.name}의 눈으로", 11, Gold, true);
            Text(new Rect(x, y + 18, 120, 30), flow.item, 21, Paper, true);
            var live = FlowAuctionLive();
            var info = jangteo.terms[JangteoShownTerm];
            var showLive = live > 0f && FlowLayerRank("pulse") > 0;
            Text(new Rect(x + 60, y + 26, width - 60, 18), showLive ? "지금 가락 경매 중" : $"{flow.season} · {info.name}", 10, showLive ? Gold : Soft, showLive,
                TextAnchor.MiddleRight);
            y += 56;
            // 필수 · 부가(2026-11-24 15:02): 지금 입장의 표에서 장터 자료 줄을 필수 칸 → 부가 칸으로. 표가 없으면 관점 순위(1~3 = 필수)
            var rows = new List<(FlowFact Fact, int Tier)>();
            var set = StanceInfoSetOf(stanceKey);
            if (set?.entries != null)
                foreach (var tier in new[] { 1, 2 })
                    foreach (var entry in set.entries)
                        if (entry.tier == tier && entry.id.StartsWith("fact:", StringComparison.Ordinal) && (tier == 1 || StanceOptionalChosen(stanceKey, entry.id)) &&
                            FlowFactOf(entry.id.Substring(5)) is FlowFact found)      // 카드에서 뺀 부가 줄은 놓지 않는다(16:28)
                            rows.Add((found, tier));
            if (rows.Count == 0)
                rows = flow.facts.Where(fact => fact.rank != null && FlowLensIndex < fact.rank.Length && fact.rank[FlowLensIndex] > 0)
                    .OrderBy(fact => fact.rank[FlowLensIndex]).Select(fact => (fact, fact.rank[FlowLensIndex] <= 3 ? 1 : 2)).ToList();
            var bottom = panel.yMax - 62;
            var drawn = 0;
            var shownTier = 0;
            foreach (var (fact, tier) in rows)
            {
                var head = tier != shownTier;
                if (y + 38 + (head ? 17 : 0) > bottom) break;
                if (head)
                {
                    Text(new Rect(x, y, width, 15), tier == 1 ? "필수 · 하루 한 번 이상" : "부가 · 주 1~2번 · 달 2~3번", 10, tier == 1 ? Gold : OptionalTint, true);
                    y += 17;
                    shownTier = tier;
                }
                var first = tier == 1;
                var row = new Rect(x - 4, y, width + 8, 38);
                Paint(row, first ? Fade(Gold, 0.15f) : Fade(Tile, 0.55f), 6);
                Paint(new Rect(row.x, row.y + 5, 3, row.height - 10), first ? Gold : OptionalTint, 1.5f);
                Text(new Rect(row.x + 8, row.y + 3, row.width - 104, 17), fact.label, 11, first ? Gold : Paper, true);
                Text(new Rect(row.xMax - 104, row.y + 2, 96, 19), fact.value, 12, Paper, true, TextAnchor.MiddleRight);
                Text(new Rect(row.x + 8, row.y + 20, row.width - 48, 15), fact.sub, 9, Soft);
                var tagColor = fact.tag == "자료" ? Mint : fact.tag == "추정" ? Ink(0xD7B8FF) : Gold;
                Text(new Rect(row.xMax - 40, row.y + 20, 32, 15), fact.tag, 9, tagColor, true, TextAnchor.MiddleRight);
                y += 40;
                drawn++;
            }
            if (drawn < rows.Count) Text(new Rect(x, y + 1, width, 14), $"부가 {rows.Count - drawn}줄은 자리가 모자라 접어 둠", 9, Fade(Soft, 0.8f));
            var top = rows.Count > 0 ? rows[0].Fact : null;
            Text(new Rect(x, panel.yMax - 42, width, 36), (top != null ? $"맨 위 출처: {top.source} · " : string.Empty) + "자료 · 셈 · 추정은 꼬리표로 · 선 굵기와 시장 자리는 예시",
                9, Soft, false, TextAnchor.UpperLeft, true);
            return true;
        }
    }
}
