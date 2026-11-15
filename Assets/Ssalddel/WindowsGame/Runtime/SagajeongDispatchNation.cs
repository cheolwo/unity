using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // r155 N0·N1 (2026-11-13): 전국 지도와 해상·항공 흐름 관찰.
    // 제안: docs/AI/Handoffs/2026-11-13-Claude-제안-선박해양항공자료표현.md 의 N0(전국 단계 뼈대)과 N1(흐름 관찰).
    // - 서울 지도에서 더 축소하면 전국 지도 안내(3칸)가 열린다. 전국 지도 안에서 확대·이동하고,
    //   서울 근처에서 끝까지 확대하면 서울 지도로 내려간다.
    // - 통계는 거점과 관계선으로만 그린다. 배·비행기의 위치나 항적을 지어내지 않는다. 굵기는 등급 3단이다.
    // - 자료 시계(월)는 게임 시계와 따로 간다. 전국 지도로 넘어가도 게임 시계는 그대로다(r144·r162 공통 시간 계약:
    //   확대·축소·자료 기간 선택은 시계를 바꾸지 않는다). 제안서의 "자료 보기 중 자동 정지"는 기획 검토 전이라
    //   NationPausesGameClock(꺼짐) 뒤에만 남겨 두었다.
    // - 업무 상태(주문·배차·정산)는 바꾸지 않는다. 팩은 Tools~/make_nation_pack.py가 만든다. 규칙은 전국흐름표현Policy.cs.
    public sealed partial class SagajeongDispatchView
    {
        private const string NationSpaceResource = "hongdal-nation-space";
        private const string NationBaseResource = "hongdal-nation-base";
        private const float NationMaxScale = 3f;
        private const float NationSeoulReach = 60f;      // 끝까지 확대했을 때 이 거리(세계 점) 안이면 서울 지도로 내려간다
        private static readonly Rect NationSide = new(22, 156, 188, 432);
        private static readonly Rect NationPanel = new(986, 106, 272, 500);
        private static readonly Rect NationClock = new(330, 604, 590, 50);
        private static readonly Rect NationBanner = new(360, 94, 560, 46);
        private static readonly Color NationSea = Ink(0x7CC6F0);
        private static readonly Color NationWater = Ink(0x0C1F2E);     // 선 색을 미리 섞는 바다 색(겹친 마디가 진해지지 않게)
        private static readonly Color NationLand = Ink(0x2A3A46);      // 육로(가상 회차) 선을 섞는 땅 색
        // 전국 지도를 보는 동안 게임 시계를 멈출지. r162가 "기획 검토가 필요한 차이"로 둔 정책이라 꺼 둔다(2026-11-13).
        // 켜면 들어올 때 배속을 0으로 하고 배속 단추를 잠그며, 나갈 때 그 전 배속으로 돌린다.
        private static readonly bool NationPausesGameClock = false;

        [Serializable]
        private sealed class NationSpace
        {
            public string schema;
            public string version;
            public string source;
            public string[] notes;
            public float worldX;
            public float worldY;
            public float worldWidth;
            public float worldHeight;
            public int baseScale;
            public float seoulX;
            public float seoulY;
            public string[] months;
            public NationHub[] hubs;
            public NationGate[] gates;
            public NationFlow[] flows;
            public NationRoute route;
            public NationName[] names;
        }

        [Serializable]
        private sealed class NationHub
        {
            public string id;
            public string name;
            public string label;
            public string kind;
            public float x;
            public float y;
            public bool approx;
            public float labelDx;
            public float labelDy;
            public int labelSize;
            public string note;
            public double[] monthly;
            public NationPartner[] partners;
            [NonSerialized] public int Tier;
            public bool Air => kind == "airport";
        }

        [Serializable]
        private sealed class NationPartner
        {
            public string name;
            public double[] teu;
            public double[] bl;
        }

        [Serializable]
        private sealed class NationGate
        {
            public string id;
            public string label;
            public string arrow;
            public float x;
            public float y;
            public string[] countries;
            [NonSerialized] public Rect Plate;
        }

        [Serializable]
        private sealed class NationFlow
        {
            public string hub;
            public string gate;
            public string mode;
            public double[] monthly;
            public float[] path;
            [NonSerialized] public NationHub Hub;
            [NonSerialized] public NationGate Gate;
            [NonSerialized] public Vector2[] Points;
            [NonSerialized] public float[] Along;      // 처음부터 잰 길이(세계 점)
            [NonSerialized] public int Grade;
            [NonSerialized] public bool Lit;
            [NonSerialized] public bool Outward;
            [NonSerialized] public float Phase;
            public bool Air => mode == "air";
        }

        [Serializable]
        private sealed class NationRoute
        {
            public string label;
            public string note;
            public float[] path;
            public float centreX;
            public float centreY;
            public float homeX;
            public float homeY;
            public string centreLabel;
            public string homeLabel;
            [NonSerialized] public Vector2[] Points;
            [NonSerialized] public float[] Along;
        }

        [Serializable]
        private sealed class NationName
        {
            public string text;
            public string kind;
            public float x;
            public float y;
            public int size;
        }

        private NationSpace nationSpace;
        private Texture2D nationBase;
        private bool nationTried;
        private readonly Dictionary<string, NationHub> nationHubs = new();
        private NationHub[] nationHubOrder = Array.Empty<NationHub>();
        private readonly List<NationFlow> nationDrawn = new();
        private float nationScale = 1f;
        private Vector2 nationOrigin;           // 세계 (0, 0)이 지도 영역 안에서 놓이는 자리
        private bool nationCameraSet;
        private bool nationManual;
        private bool nationPressed;
        private bool nationDragging;
        private Vector2 nationPressPoint;
        private Vector2 nationPressOrigin;
        private string nationHub = "busan-new";  // 고른 거점. 처음에는 컨테이너가 가장 많은 부산신항
        private string nationGate;               // 고른 출입구(있으면 카드가 출입구 카드가 된다)
        private string nationHover;
        private int nationPeriod;                // 0 = 2025 전체, 1~12 = 그 달
        private 전국지표 nationIndicator = 전국지표.Teu;
        private bool nationSeaOn = true;
        private bool nationAirOn = true;
        private bool nationRouteOn = true;
        private int nationHeldSpeed;             // 전국 지도에 들어오며 멈춘 배속. 0이면 멈춘 것이 없다
        private string nationNotice;
        private float nationNoticeUntil;
        private string nationPick;
        private Vector2 nationPickAt;
        private float nationPickUntil;
        private string nationDrawKey;

        private bool NationReady => nationSpace != null && nationBase != null;
        private bool NationMapVisible => mapLevel == MapLevel.Nation && NationReady;
        private bool NationSideVisible => NationMapVisible && expansionView && !mapOnly && !showDesk;
        private bool NationPanelVisible => NationSideVisible && !dongPanelOpen && !zonePanelOpen && !CycleCardVisible;
        private bool NationClockVisible => NationMapVisible && (expansionView || mapOnly);
        private Rect LevelBannerRect => mapLevel == MapLevel.Nation ? NationBanner : LevelBanner;

        private bool NationHoldsClock => NationPausesGameClock && mapLevel == MapLevel.Nation;

        private string NationClockNote => !running ? "자료 보기 중 · 운영 전" :
            NationPausesGameClock && campaignMode ? "자료 보기 중 · 게임 시계 멈춤" : "자료 보기 중 · 배달은 계속 진행";

        private bool NationCovers(Vector2 point) =>
            NationClockVisible && NationClock.Contains(point) ||
            NationSideVisible && NationSide.Contains(point) ||
            NationPanelVisible && NationPanel.Contains(point);

        // ── 자료 ────────────────────────────────────────────────────────────────

        private bool LoadNation()
        {
            if (nationTried) return NationReady;
            nationTried = true;
            var started = Time.realtimeSinceStartup;
            var space = ReadMapResource<NationSpace>(NationSpaceResource);
            var asset = Resources.Load<TextAsset>(NationBaseResource);
            if (space?.hubs == null || space.flows == null || space.gates == null || space.worldWidth <= 0 ||
                space.worldHeight <= 0 || asset == null)
            {
                if (asset != null) Resources.UnloadAsset(asset);
                Debug.LogWarning("Hongdal nation map: 전국 팩이 없어 전국 지도를 열지 않습니다.");
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "Hongdal nation base", wrapMode = TextureWrapMode.Clamp };
            var loaded = texture.LoadImage(asset.bytes, true);
            Resources.UnloadAsset(asset);
            if (!loaded)
            {
                Destroy(texture);
                Debug.LogWarning("Hongdal nation map: 바탕 그림을 읽지 못했습니다.");
                return false;
            }
            texture.filterMode = FilterMode.Trilinear;

            nationHubs.Clear();
            foreach (var hub in space.hubs)
                if (!string.IsNullOrEmpty(hub.id) && hub.monthly is { Length: >= 48 })
                    nationHubs[hub.id] = hub;
            var gates = space.gates.Where(gate => !string.IsNullOrEmpty(gate.id)).ToDictionary(gate => gate.id);
            var flows = new List<NationFlow>();
            foreach (var flow in space.flows)
            {
                if (flow.monthly is not { Length: >= 48 } || flow.path == null || flow.path.Length < 4) continue;
                if (!nationHubs.TryGetValue(flow.hub ?? string.Empty, out var hub) || !gates.TryGetValue(flow.gate ?? string.Empty, out var gate))
                    continue;
                flow.Hub = hub;
                flow.Gate = gate;
                (flow.Points, flow.Along) = NationPolyline(flow.path);
                flow.Phase = flows.Count * 0.137f % 1f;
                flows.Add(flow);
            }
            space.flows = flows.ToArray();
            space.hubs = nationHubs.Values.ToArray();
            if (space.route?.path is { Length: >= 4 })
                (space.route.Points, space.route.Along) = NationPolyline(space.route.path);
            else
                space.route = null;
            if (!nationHubs.ContainsKey(nationHub)) nationHub = space.hubs.FirstOrDefault()?.id;

            nationSpace = space;
            nationBase = texture;
            Debug.Log($"Hongdal nation map: 판본 {space.version} · 거점 {space.hubs.Length} · 흐름 {space.flows.Length} · " +
                $"바탕 {texture.width}×{texture.height} · 준비 {(Time.realtimeSinceStartup - started) * 1000f:0}ms");
            return true;
        }

        private static (Vector2[] Points, float[] Along) NationPolyline(float[] path)
        {
            var points = new Vector2[path.Length / 2];
            var along = new float[points.Length];
            for (var index = 0; index < points.Length; index++)
            {
                points[index] = new Vector2(path[index * 2], path[index * 2 + 1]);
                if (index > 0) along[index] = along[index - 1] + Vector2.Distance(points[index - 1], points[index]);
            }
            return (points, along);
        }

        // 선 위의 한 점(t = 0 처음, 1 끝)과 그 자리의 방향(도).
        private static (Vector2 Point, float Angle) NationAlong(Vector2[] points, float[] along, float t)
        {
            var total = along[along.Length - 1];
            var target = Mathf.Clamp01(t) * total;
            var index = 1;
            while (index < points.Length - 1 && along[index] < target) index++;
            var span = Mathf.Max(0.0001f, along[index] - along[index - 1]);
            var point = Vector2.Lerp(points[index - 1], points[index], (target - along[index - 1]) / span);
            var delta = points[index] - points[index - 1];
            return (point, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        // ── 게임 시계 ───────────────────────────────────────────────────────────

        // NationPausesGameClock이 켜져 있을 때만: 전국 지도를 보는 동안 게임 시계를 멈춘다(제안 2절 원칙 2).
        // 보는 중에 운영을 시작해도 다음 화면에서 멈춘다.
        private void HoldNationClock()
        {
            if (!NationPausesGameClock || !campaignMode || !running || error != null || CampaignPolicyOpen || campaignJumpPending || campaignSpeed == 0)
                return;
            var speed = campaignSpeed;
            SetCampaignSpeed(0);
            if (campaignSpeed != 0) return;
            if (nationHeldSpeed == 0) nationHeldSpeed = speed;
            Debug.Log($"Hongdal nation map: 게임 시계 멈춤 (자료 보기 · 돌아가면 {nationHeldSpeed}배)");
        }

        private void ReleaseNationClock()
        {
            if (nationHeldSpeed == 0) return;
            var speed = nationHeldSpeed;
            nationHeldSpeed = 0;
            SetCampaignSpeed(speed);
            Debug.Log($"Hongdal nation map: 게임 시계 다시 {campaignSpeed}배");
        }

        // ── 화면 맞춤 ───────────────────────────────────────────────────────────

        private float NationFloor => Mathf.Max(Field.width / nationSpace.worldWidth, Field.height / nationSpace.worldHeight);

        private Vector2 NationPoint(Vector2 world) => nationOrigin + world * nationScale;

        private Vector2 NationPoint(float x, float y) => nationOrigin + new Vector2(x, y) * nationScale;

        private Vector2 NationWorld(Vector2 local) => (local - nationOrigin) / nationScale;

        private Rect NationLocal(Rect logical) => new(logical.x - Field.x, logical.y - Field.y, logical.width, logical.height);

        // 처음 보기: 시안과 같은 자리. 세계 (600, 337)이 논리 화면 (600, 337)에 온다.
        private void ResetNationCamera()
        {
            var anchor = new Vector2(600f, 337f);
            nationScale = Mathf.Clamp(1f, NationFloor, Mathf.Max(NationFloor, NationMaxScale));
            nationOrigin = anchor - Field.position - anchor * nationScale;
            ClampNation();
            nationCameraSet = true;
        }

        private void ClampNation()
        {
            var space = nationSpace;
            var floor = NationFloor;
            nationScale = Mathf.Clamp(nationScale, floor, Mathf.Max(floor, NationMaxScale));
            nationOrigin.x = Mathf.Clamp(nationOrigin.x, Field.width - (space.worldX + space.worldWidth) * nationScale, -space.worldX * nationScale);
            nationOrigin.y = Mathf.Clamp(nationOrigin.y, Field.height - (space.worldY + space.worldHeight) * nationScale, -space.worldY * nationScale);
        }

        private void ZoomNation(float scale, Vector2 pointer)
        {
            var world = NationWorld(pointer);
            nationScale = Mathf.Clamp(scale, NationFloor, Mathf.Max(NationFloor, NationMaxScale));
            nationOrigin = pointer - world * nationScale;
            nationManual = true;
            ClampNation();
        }

        // 전국 지도 안의 확대·축소. 돌려주는 값: 0 = 지도 안에서 처리했다, -1 = 서울 지도로 내려갈 차례.
        private int NationWheel(float delta, Vector2 pointer)
        {
            if (!nationCameraSet) return 0;
            if (delta < 0)
            {
                if (nationScale >= NationMaxScale - 0.001f)
                {
                    if (Vector2.Distance(NationWorld(pointer), new Vector2(nationSpace.seoulX, nationSpace.seoulY)) <= NationSeoulReach)
                        return -1;
                    NationSay("서울 근처에서 끝까지 확대하면 서울 지도로 내려갑니다");
                    return 0;
                }
                ZoomNation(nationScale * Mathf.Pow(1.08f, -delta), pointer);
                return 0;
            }
            if (nationScale <= NationFloor * 1.002f)
            {
                NationSay("전국이 가장 넓은 지도입니다 · 지구본은 따로 열립니다(연결 전)");
                return 0;
            }
            ZoomNation(nationScale * Mathf.Pow(1.08f, -delta), pointer);
            return 0;
        }

        private void NationSay(string text)
        {
            nationNotice = text;
            nationNoticeUntil = Time.unscaledTime + 3.5f;
        }

        // ── 그리기 ─────────────────────────────────────────────────────────────

        private void DrawNationMap()
        {
            var current = Event.current;
            var repaint = current.type == EventType.Repaint;
            var now = Time.unscaledTime;
            HoldNationClock();
            if (repaint)
            {
                if (!nationManual || !nationCameraSet) ResetNationCamera();
                else ClampNation();
                var space = nationSpace;
                GUI.DrawTexture(new Rect(NationPoint(space.worldX, space.worldY),
                    new Vector2(space.worldWidth, space.worldHeight) * nationScale), nationBase);
                DrawNationNames();
                PrepareNationFlows();
                DrawNationFlows(now);
                if (nationRouteOn) DrawNationRoute(now);
                PrepareNationHubs();
                var free = !MapOverlayCovers(current.mousePosition + Field.position);
                nationHover = free && !nationDragging ? NationHubAt(current.mousePosition)?.id : null;
                DrawNationHubs();
                DrawNationGates();
                DrawNationHubLabels();
                DrawNationPick(now);
            }

            if (NationClockVisible) DrawNationClockPanel();
            if (nationManual && ExpansionButton(NationLocal(new Rect(800, 572, 120, 26)), "전국 처음 보기", true, Mint))
            {
                nationManual = false;
                nationPressed = false;
                return;
            }
            if (repaint && nationNotice != null)
            {
                if (now > nationNoticeUntil) nationNotice = null;
                else GuPlate(new Vector2(600 - Field.x, 556 - Field.y), nationNotice, 12, Paper, 0.92f);
            }

            // 끌면 지도를 옮기고, 그냥 누르면 거점·출입구·선을 고른다.
            if (current.button != 0) return;
            var covered = MapOverlayCovers(current.mousePosition + Field.position);
            if (current.type == EventType.MouseDown)
            {
                nationPressed = !covered;
                nationDragging = false;
                nationPressPoint = current.mousePosition;
                nationPressOrigin = nationOrigin;
            }
            else if (current.type == EventType.MouseDrag && nationPressed)
            {
                var moved = current.mousePosition - nationPressPoint;
                if (!nationDragging && moved.sqrMagnitude < 36f) return;
                nationDragging = true;
                nationManual = true;
                nationOrigin = nationPressOrigin + moved;
                ClampNation();
                current.Use();
            }
            else if (current.type == EventType.MouseUp && nationPressed)
            {
                nationPressed = false;
                if (!nationDragging && !covered) PickNation(current.mousePosition);
                nationDragging = false;
                current.Use();
            }
        }

        private void DrawNationNames()
        {
            foreach (var name in nationSpace.names ?? Array.Empty<NationName>())
            {
                if (string.IsNullOrEmpty(name.text)) continue;
                var size = Mathf.Clamp(name.size, 9, 16);
                var point = NationPoint(name.x, name.y);
                var measured = GuNameSize(name.text, size);
                Text(Centered(point, measured.x + 4, measured.y + 2), name.text, size,
                    name.kind == "sea" ? Fade(NationSea, 0.55f) : Fade(Soft, 0.45f), true, TextAnchor.MiddleCenter);
            }
        }

        private bool NationLayerOn(bool air) => air ? nationAirOn : nationSeaOn;

        private void PrepareNationFlows()
        {
            nationDrawn.Clear();
            foreach (var flow in nationSpace.flows)
            {
                var air = flow.Air;
                if (!NationLayerOn(air)) continue;
                var teu = 전국흐름표현Policy.UsesTeu(air, nationIndicator);
                flow.Grade = 전국흐름표현Policy.Grade(전국흐름표현Policy.Both(flow.monthly, nationPeriod, teu), teu, nationPeriod);
                if (flow.Grade == 0) continue;
                flow.Lit = nationGate != null ? flow.gate == nationGate : flow.hub == nationHub;
                flow.Outward = 전국흐름표현Policy.OutwardMore(flow.monthly, teu, nationPeriod);
                nationDrawn.Add(flow);
            }
            nationDrawn.Sort((a, b) => a.Lit != b.Lit ? a.Lit.CompareTo(b.Lit) : a.Grade.CompareTo(b.Grade));
            var key = $"{nationPeriod}·{nationIndicator}·{nationSeaOn}·{nationAirOn}";
            if (key == nationDrawKey) return;
            nationDrawKey = key;
            Debug.Log($"Hongdal nation map: {전국흐름표현Policy.PeriodLabel(nationPeriod)} · 지표 {nationIndicator} · " +
                $"선 {nationDrawn.Count}개 (해상 {nationDrawn.Count(flow => !flow.Air)} · 항공 {nationDrawn.Count(flow => flow.Air)})");
        }

        private void DrawNationFlows(float now)
        {
            foreach (var flow in nationDrawn)
            {
                var color = flow.Air ? Lilac : NationSea;
                var width = 전국흐름표현Policy.Width(flow.Grade);
                var strength = flow.Lit ? 0.95f : flow.Air ? 0.30f : 0.38f;
                if (flow.Lit) DrawNationPath(flow.Points, Color.Lerp(NationWater, color, 0.2f), width + 5f, false);
                DrawNationPath(flow.Points, Color.Lerp(NationWater, color, strength), width, flow.Air);
                if (!mapEffects)
                {
                    // 간단히: 가운데에 쐐기 둘. 큰 쐐기가 입·출 중 많은 쪽을 가리킨다.
                    var (middle, angle) = NationAlong(flow.Points, flow.Along, 0.5f);
                    var point = NationPoint(middle);
                    var forward = flow.Outward ? angle : angle + 180f;
                    Chevron(point + Direction(forward) * 4f, forward, 5.5f, Color.Lerp(NationWater, color, Mathf.Min(1f, strength + 0.25f)));
                    Chevron(point - Direction(forward) * 5f, forward + 180f, 3.5f, Color.Lerp(NationWater, color, strength * 0.7f));
                    continue;
                }

                // 효과 사용: 선 위로 흐르는 빛과 상징 하나. 빛은 많은 쪽으로 흐른다. 수와 빠르기는 통계와 무관하다.
                var head = Mathf.Repeat(flow.Phase + now * 0.06f, 1f);
                for (var trail = 0; trail < 8; trail++)
                {
                    var t = head - trail * 0.02f;
                    if (t <= 0f) break;
                    var (spot, _) = NationAlong(flow.Points, flow.Along, flow.Outward ? t : 1f - t);
                    Disc(NationPoint(spot), (width + 1.2f) * (1f - trail / 12f), Fade(Color.white, (flow.Lit ? 0.5f : 0.22f) * (1f - trail / 8f)));
                }
                if (!flow.Lit && flow.Grade < 2) continue;
                var symbolAt = Mathf.Repeat(head + 0.42f, 1f);
                var (place, heading) = NationAlong(flow.Points, flow.Along, flow.Outward ? symbolAt : 1f - symbolAt);
                DrawNationSymbol(NationPoint(place), flow.Outward ? heading : heading + 180f, flow.Air, color);
            }
        }

        // 선을 짧은 둥근 막대로 잇는다. 점선은 화면에서 7점 그리고 5점 띄운다.
        private void DrawNationPath(Vector2[] points, Color color, float width, bool dashed)
        {
            var a = NationPoint(points[0]);
            if (!dashed)
            {
                for (var index = 1; index < points.Length; index++)
                {
                    var b = NationPoint(points[index]);
                    if (index < points.Length - 1 && (b - a).sqrMagnitude < 25f) continue;
                    Capsule(a, b, color, width);
                    a = b;
                }
                return;
            }
            const float dash = 7f;
            const float gap = 5f;
            var on = true;
            var left = dash;
            for (var index = 1; index < points.Length; index++)
            {
                var b = NationPoint(points[index]);
                var step = b - a;
                var length = step.magnitude;
                var at = 0f;
                while (at < length)
                {
                    var take = Mathf.Min(left, length - at);
                    if (on) Capsule(a + step * (at / length), a + step * ((at + take) / length), color, width);
                    at += take;
                    left -= take;
                    if (left > 0.001f) continue;
                    on = !on;
                    left = on ? dash : gap;
                }
                a = b;
            }
        }

        private static Vector2 Direction(float angle) =>
            new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        private static void Capsule(Vector2 a, Vector2 b, Color color, float width)
        {
            var delta = b - a;
            var length = delta.magnitude;
            if (length < 0.01f) return;
            var radius = width * 0.5f;
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(a, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Paint(new Rect(-radius, -radius, length + width, width), color, radius);
            GUI.matrix = original;
        }

        // ">" 모양. angle 쪽을 가리킨다.
        private static void Chevron(Vector2 tip, float angle, float size, Color color)
        {
            Capsule(tip, tip + Direction(angle + 145f) * size, color, 1.8f);
            Capsule(tip, tip + Direction(angle - 145f) * size, color, 1.8f);
        }

        private static void DrawNationSymbol(Vector2 centre, float angle, bool air, Color color)
        {
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(centre, angle);
            if (air)
            {
                Paint(new Rect(-8f, -1.6f, 17f, 3.2f), color, 1.6f);           // 몸통
                Paint(new Rect(-2.5f, -8f, 4f, 16f), color, 1.5f);             // 날개
                Paint(new Rect(-8f, -3.6f, 2.6f, 7.2f), color, 1.2f);          // 꼬리
            }
            else
            {
                Paint(new Rect(-9f, -5f, 18f, 10f), color, 3f);                // 배 몸
                Paint(new Rect(-7.8f, -3.8f, 15.6f, 7.6f), Canvas, 2.4f);
                Paint(new Rect(-5f, -2.5f, 6f, 5f), color, 1f);                // 짐
            }
            GUI.matrix = original;
        }

        private void DrawNationRoute(float now)
        {
            var route = nationSpace.route;
            if (route?.Points == null) return;
            DrawNationPath(route.Points, Color.Lerp(NationLand, Mint, 0.25f), 7f, false);
            DrawNationPath(route.Points, Mint, 2.2f, true);
            var centre = NationPoint(route.centreX, route.centreY);
            var home = NationPoint(route.homeX, route.homeY);
            Paint(Centered(centre, 10, 10), Mint, 2);
            Paint(Centered(centre, 5, 5), Canvas);
            Disc(home, 5f, Mint);
            NationTag(centre + new Vector2(10, 6), route.centreLabel, 10, Mint, true);
            NationTag(home + new Vector2(8, -13), route.homeLabel, 10, Mint, true);      // 인천항 이름표와 겹치지 않게 위로

            // 트럭: 간단히는 한 자리에 서 있고, 효과 사용이면 천천히 오간다(가상 · 통계와 무관).
            var t = mapEffects ? 0.08f + Mathf.PingPong(now * 0.025f, 0.8f) : 0.36f;
            var (spot, angle) = NationAlong(route.Points, route.Along, t);
            var truck = NationPoint(spot);
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(truck, angle);
            Paint(new Rect(-8f, -4.5f, 12f, 9f), Mint, 2f);
            Paint(new Rect(-6.8f, -3.3f, 9.6f, 6.6f), Canvas, 1.4f);
            Paint(new Rect(4.5f, -3.5f, 5f, 7f), Mint, 1.5f);
            GUI.matrix = original;
        }

        // 왼쪽 끝을 point에 둔(왼쪽 정렬) 또는 오른쪽 끝을 둔 이름표. 세로 가운데가 point.y다.
        private Rect NationTag(Vector2 point, string text, int size, Color color, bool leftAligned, float back = 0.85f)
        {
            if (string.IsNullOrEmpty(text)) return Rect.zero;
            var measured = GuNameSize(text, size);
            var width = measured.x + 12f;
            var rect = new Rect(leftAligned ? point.x : point.x - width, point.y - 10f, width, 20f);
            Paint(rect, Fade(Canvas, back), 6);
            Text(rect, text, size, color, true, TextAnchor.MiddleCenter);
            return rect;
        }

        private void PrepareNationHubs()
        {
            foreach (var hub in nationSpace.hubs)
            {
                var teu = 전국흐름표현Policy.UsesTeu(hub.Air, nationIndicator);
                hub.Tier = 전국흐름표현Policy.HubTier(전국흐름표현Policy.Both(hub.monthly, nationPeriod, teu), teu, nationPeriod);
            }
            nationHubOrder = nationSpace.hubs.OrderBy(hub => hub.id == nationHub && nationGate == null)
                .ThenBy(hub => hub.Tier).ToArray();
        }

        private static float NationPinRadius(int tier) => tier >= 2 ? 12f : tier == 1 ? 9.5f : 8f;

        private void DrawNationHubs()
        {
            foreach (var hub in nationHubOrder)
            {
                if (!NationLayerOn(hub.Air)) continue;
                var tip = NationPoint(hub.x, hub.y);
                var selected = nationGate == null && hub.id == nationHub;
                var accent = hub.Air ? Lilac : NationSea;
                if (hub.Tier == 0 && !selected)
                {
                    if (hub.id == nationHover) Hoop(tip, 6.5f, Fade(Paper, 0.7f), 1.4f);
                    Disc(tip, 3.4f, Fade(accent, 0.9f));
                    continue;
                }
                DrawNationPin(tip, NationPinRadius(hub.Tier), hub.Air, selected, hub.id == nationHover);
            }
        }

        private static void DrawNationPin(Vector2 tip, float radius, bool air, bool selected, bool hover)
        {
            var accent = air ? Lilac : NationSea;
            var body = selected ? accent : Canvas;
            var edge = selected ? Canvas : accent;
            var centre = new Vector2(tip.x, tip.y - radius - 7f);
            if (selected) Hoop(centre, radius + 6f, Gold, 2f);
            else if (hover) Hoop(centre, radius + 5f, Fade(Paper, 0.7f), 1.4f);
            var side = radius * 0.95f;
            var pivot = new Vector2(tip.x, tip.y - side * 0.7071f);
            var original = GUI.matrix;
            GUI.matrix = original * TurnAt(pivot, 45f);
            Paint(new Rect(-side / 2 - 1.3f, -side / 2 - 1.3f, side + 2.6f, side + 2.6f), edge);
            GUI.matrix = original;
            Disc(centre, radius + 1.4f, edge);
            GUI.matrix = original * TurnAt(pivot, 45f);
            Paint(new Rect(-side / 2, -side / 2, side, side), body);
            GUI.matrix = original;
            Disc(centre, radius, body);

            var ink = selected ? Canvas : accent;
            var u = radius * 0.5f;
            if (air)
            {
                Paint(new Rect(centre.x - 0.9f, centre.y - u * 1.3f, 1.8f, u * 2.5f), ink, 0.9f);              // 몸통
                Paint(new Rect(centre.x - u * 1.25f, centre.y - u * 0.25f, u * 2.5f, u * 0.5f), ink, 1f);      // 날개
                Paint(new Rect(centre.x - u * 0.55f, centre.y + u * 0.8f, u * 1.1f, u * 0.36f), ink, 1f);      // 꼬리 날개
                return;
            }
            Paint(new Rect(centre.x - 0.8f, centre.y - u * 0.95f, 1.6f, u * 2f), ink);                         // 닻 대
            Paint(new Rect(centre.x - u * 0.6f, centre.y - u * 0.55f, u * 1.2f, 1.5f), ink);                   // 가로대
            Hoop(new Vector2(centre.x, centre.y - u * 1.15f), u * 0.3f, ink, 1.2f);                           // 고리
            var bottom = new Vector2(centre.x, centre.y + u * 1.05f);
            Capsule(bottom, new Vector2(centre.x - u * 0.95f, centre.y + u * 0.3f), ink, 1.5f);                // 닻 팔
            Capsule(bottom, new Vector2(centre.x + u * 0.95f, centre.y + u * 0.3f), ink, 1.5f);
        }

        private void DrawNationHubLabels()
        {
            foreach (var hub in nationHubOrder)
            {
                if (!NationLayerOn(hub.Air)) continue;
                var selected = nationGate == null && hub.id == nationHub;
                if (hub.Tier == 0 && !selected && hub.id != nationHover && nationScale < 1.6f) continue;
                var size = Mathf.Clamp(hub.labelSize, 9, 13);
                var color = selected ? Gold : hub.Air ? Lilac : size <= 10 ? Soft : Paper;
                var tip = NationPoint(hub.x, hub.y);
                NationTag(new Vector2(tip.x + hub.labelDx, tip.y + hub.labelDy), hub.label, selected ? Mathf.Max(size, 12) : size,
                    color, hub.labelDx >= 0, 0.82f);
            }
        }

        private (double Sea, double Air) NationGateTotals(string gate) =>
            전국흐름표현Policy.GateTotals(nationSpace.flows.Where(flow => flow.gate == gate)
                .Select(flow => (flow.Air, (IReadOnlyList<double>)flow.monthly)), nationIndicator, nationPeriod);

        private string NationSeaUnit => nationIndicator == 전국지표.Teu ? "TEU" : "건";

        private void DrawNationGates()
        {
            foreach (var gate in nationSpace.gates)
            {
                var (sea, air) = NationGateTotals(gate.id);
                var title = gate.arrow + " " + gate.label;
                var parts = new List<string>();
                if (sea > 0) parts.Add($"해상 {전국흐름표현Policy.Short(sea)} {NationSeaUnit}");
                if (air > 0) parts.Add($"항공 {전국흐름표현Policy.Short(air)}건");
                var sub = parts.Count > 0 ? string.Join(" · ", parts) : "이 기간 흐름 없음";
                var width = Mathf.Max(GuNameSize(title, 12).x, GuNameSize(sub, 10, false).x, 96f) + 20f;
                var centre = NationPoint(gate.x, gate.y);
                var plate = new Rect(centre.x - width / 2, centre.y - 21f, width, 42f);
                gate.Plate = plate;
                var chosen = gate.id == nationGate;
                Paint(plate, chosen ? Gold : Fade(Paper, 0.2f), 8);
                Paint(new Rect(plate.x + 1, plate.y + 1, plate.width - 2, plate.height - 2), Fade(Canvas, 0.92f), 7);
                Text(new Rect(plate.x, plate.y + 3, plate.width, 20), title, 12, chosen ? Gold : Paper, true, TextAnchor.MiddleCenter);
                Text(new Rect(plate.x, plate.y + 21, plate.width, 17), sub, 10, Soft, false, TextAnchor.MiddleCenter);
            }
        }

        private void DrawNationPick(float now)
        {
            if (nationPick == null) return;
            if (now > nationPickUntil)
            {
                nationPick = null;
                return;
            }
            NationTag(nationPickAt + new Vector2(12, -14), nationPick, 11, Paper, true, 0.94f);
        }

        // ── 고르기 ─────────────────────────────────────────────────────────────

        private NationHub NationHubAt(Vector2 local)
        {
            NationHub found = null;
            foreach (var hub in nationHubOrder)      // 뒤에 그린 것(위에 보이는 것)을 먼저 고른다
            {
                if (!NationLayerOn(hub.Air)) continue;
                var tip = NationPoint(hub.x, hub.y);
                var big = hub.Tier > 0 || nationGate == null && hub.id == nationHub;
                var radius = NationPinRadius(hub.Tier);
                var hit = big ? Vector2.Distance(local, new Vector2(tip.x, tip.y - radius - 7f)) <= radius + 3f ||
                                Vector2.Distance(local, tip) <= 6f
                    : Vector2.Distance(local, tip) <= 8f;
                if (hit) found = hub;
            }
            return found;
        }

        private void PickNation(Vector2 local)
        {
            var hub = NationHubAt(local);
            if (hub != null)
            {
                SelectNationHub(hub.id);
                return;
            }
            var gate = nationSpace.gates.LastOrDefault(item => item.Plate.Contains(local));
            if (gate != null)
            {
                nationGate = gate.id;
                nationPick = null;
                NationSay("지구본은 별도 실행판이라 아직 이 나라로 넘어가지 않습니다 (연결 방식 결정 전)");
                Debug.Log("Hongdal nation map: 출입구 " + gate.id);
                return;
            }
            NationFlow nearest = null;
            var best = 7f;
            foreach (var flow in nationDrawn)
            {
                var a = NationPoint(flow.Points[0]);
                for (var index = 1; index < flow.Points.Length; index++)
                {
                    var b = NationPoint(flow.Points[index]);
                    var distance = DistanceToSegment(local, a, b);
                    if (distance < best || distance <= best + 0.01f && flow.Lit)
                    {
                        best = distance;
                        nearest = flow;
                    }
                    a = b;
                }
            }
            if (nearest == null) return;
            SelectNationHub(nearest.hub);
            var teu = 전국흐름표현Policy.UsesTeu(nearest.Air, nationIndicator);
            var unit = teu ? "TEU" : "건";
            var inbound = 전국흐름표현Policy.Sum(nearest.monthly, nationPeriod, teu ? 전국흐름표현Policy.InTeu : 전국흐름표현Policy.InBl);
            var outbound = 전국흐름표현Policy.Sum(nearest.monthly, nationPeriod, teu ? 전국흐름표현Policy.OutTeu : 전국흐름표현Policy.OutBl);
            nationPick = $"{nearest.Hub.label} ↔ {nearest.Gate.label} · 입항 {전국흐름표현Policy.Short(inbound)} · 출항 {전국흐름표현Policy.Short(outbound)} {unit}";
            nationPickAt = local;
            nationPickUntil = Time.unscaledTime + 5f;
        }

        private void SelectNationHub(string id)
        {
            if (nationHub == id && nationGate == null) return;
            nationHub = id;
            nationGate = null;
            Debug.Log("Hongdal nation map: 거점 " + id);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        // ── 자료 시계 (지도 아래 가운데) ─────────────────────────────────────────

        private void DrawNationClockPanel()
        {
            var panel = NationLocal(NationClock);
            Paint(panel, Fade(Canvas, 0.94f), 9);
            Text(new Rect(panel.x + 14, panel.y + 6, 112, 18), "자료 시계", 11, Gold, true);
            Text(new Rect(panel.x + 14, panel.y + 26, 112, 16), "게임 시계와 따로 · 월 단위", 9, Soft);
            var hubs = nationSpace.hubs.Select(hub => (hub.Air, (IReadOnlyList<double>)hub.monthly));
            var totals = 전국흐름표현Policy.MonthTotals(hubs, nationIndicator);
            var top = Math.Max(1.0, totals.Max());
            var accent = nationIndicator == 전국지표.Teu ? NationSea : Lilac;
            for (var month = 1; month <= 전국흐름표현Policy.Months; month++)
            {
                var slot = new Rect(panel.x + 128 + (month - 1) * 28, panel.y + 4, 22, 42);
                var on = nationPeriod == month;
                var height = Mathf.Max(2f, 22f * (float)(totals[month - 1] / top));
                var color = nationPeriod == 전국흐름표현Policy.Whole ? Fade(accent, 0.6f) : on ? accent : Fade(accent, 0.28f);
                Paint(new Rect(slot.x + 3, panel.y + 28 - height, 16, height), color, 2);
                Text(new Rect(slot.x - 3, panel.y + 30, 28, 14), month + "월", 9, on ? Paper : Soft, on, TextAnchor.MiddleCenter);
                if (!Hit(slot)) continue;
                nationPeriod = on ? 전국흐름표현Policy.Whole : month;
                nationPick = null;
                Debug.Log("Hongdal nation map: 기간 " + 전국흐름표현Policy.PeriodLabel(nationPeriod));
            }
            var label = new Rect(panel.xMax - 118, panel.y + 4, 106, 42);
            Text(new Rect(label.x, label.y + 1, label.width, 20), 전국흐름표현Policy.PeriodLabel(nationPeriod), 12, Paper, true,
                TextAnchor.MiddleRight);
            Text(new Rect(label.x - 20, label.y + 21, label.width + 20, 16), nationPeriod == 전국흐름표현Policy.Whole
                ? (nationIndicator == 전국지표.Teu ? "막대 = 해상 TEU 합" : "막대 = BL 건수 합") : "누르면 2025 전체", 9, Soft,
                align: TextAnchor.MiddleRight);
            if (nationPeriod == 전국흐름표현Policy.Whole || !Hit(label)) return;
            nationPeriod = 전국흐름표현Policy.Whole;
            Debug.Log("Hongdal nation map: 기간 " + 전국흐름표현Policy.PeriodLabel(nationPeriod));
        }

        // ── 위쪽 띠 ─────────────────────────────────────────────────────────────

        private void DrawNationBanner()
        {
            var banner = NationBanner;
            Paint(banner, Rim, 9);
            Paint(new Rect(banner.x + 1, banner.y + 1, banner.width - 2, banner.height - 2), Canvas, 8);
            Text(new Rect(banner.x + 14, banner.y + 4, 340, 21), "전국 · 해상·항공 자료 관찰 · " + NationClockNote.Replace("자료 보기 중 · ", string.Empty),
                13, Paper, true);
            Text(new Rect(banner.x + 14, banner.y + 25, 340, 16), "통계 관계를 그린 것 · 실제 항로·선박·항공기 위치 아님", 10, Soft);
            if (ExpansionButton(new Rect(banner.xMax - 198, banner.y + 9, 88, 28), "서울 지도로", true, Tile))
                ChangeMapLevel(-1);
            else if (ExpansionButton(new Rect(banner.xMax - 104, banner.y + 9, 94, 28), "동네로 돌아가기", true, Mint))
                ReturnToDong();
        }

        // ── 왼쪽: 자료 층 · 굵기의 지표 · 표시 ──────────────────────────────────

        private void DrawNationSide()
        {
            var x = NationSide.x;
            var y = NationSide.y;
            var layers = new Rect(x, y, NationSide.width, 226);
            Paint(layers, Panel, 10);
            Text(new Rect(x + 13, y + 8, 90, 20), "자료 층", 12, Mint, true);
            Text(new Rect(x + 88, y + 8, 88, 20), "눌러서 켜고 끔", 10, Soft, align: TextAnchor.MiddleRight);
            NationLayerRow(0, "해상 물동량", "관세청 2025 · TEU·BL", NationSea, true, ref nationSeaOn);
            NationLayerRow(1, "항공 물동량", "관세청 2025 · BL 건수", Lilac, true, ref nationAirOn);
            NationLayerRow(2, "내 화물 회차", "가상 · 전국 시범 후보", Mint, true, ref nationRouteOn);
            var off = false;
            NationLayerRow(3, "바다 상태", "해류·수온·조석 수집 후", Soft, false, ref off);
            NationLayerRow(4, "입출항 사건", "선박운항정보 수집 후", Soft, false, ref off);
            NationLayerRow(5, "산지·위판장", "위탁판매·어장 수집 후", Soft, false, ref off);

            var choose = new Rect(x, y + 234, NationSide.width, 70);
            Paint(choose, Panel, 10);
            Text(new Rect(x + 13, y + 240, 160, 18), "굵기의 지표", 11, Soft, true);
            if (NationSegment(new Rect(x + 10, y + 262, 50, 24), "TEU", nationIndicator == 전국지표.Teu, true))
                SetNationIndicator(전국지표.Teu);
            if (NationSegment(new Rect(x + 64, y + 262, 60, 24), "BL 건수", nationIndicator == 전국지표.Bl, true))
                SetNationIndicator(전국지표.Bl);
            NationSegment(new Rect(x + 128, y + 262, 50, 24), "중량", false, false);
            Text(new Rect(x + 13, y + 287, 170, 15), "중량은 원본 단위 확인 중 · 항공은 늘 BL", 9, Fade(Soft, 0.8f));

            var legend = new Rect(x, y + 312, NationSide.width, 120);
            Paint(legend, Panel, 10);
            Text(new Rect(x + 13, y + 318, 160, 18), "표시", 11, Soft, true);
            var row = y + 346f;
            var left = x + 16f;
            Capsule(new Vector2(left, row + 3), new Vector2(left + 10, row - 1), NationSea, 2.4f);
            Capsule(new Vector2(left + 10, row - 1), new Vector2(left + 20, row + 3), NationSea, 2.4f);
            Text(new Rect(x + 44, row - 9, 140, 18), "해상 · 바다로 낮게 휜 선", 10, Paper);
            row += 17;
            for (var dash = 0; dash < 3; dash++)
                Capsule(new Vector2(left + dash * 7.5f, row), new Vector2(left + dash * 7.5f + 4.5f, row), Lilac, 2f);
            Text(new Rect(x + 44, row - 9, 140, 18), "항공 · 바깥으로 솟는 점선", 10, Paper);
            row += 17;
            for (var dash = 0; dash < 3; dash++)
                Capsule(new Vector2(left + dash * 7.5f, row), new Vector2(left + dash * 7.5f + 4.5f, row), Mint, 2f);
            Text(new Rect(x + 44, row - 9, 140, 18), "내 화물 회차 · 육로(가상)", 10, Paper);
            row += 17;
            for (var grade = 1; grade <= 3; grade++)
                Capsule(new Vector2(left + (grade - 1) * 7f, row), new Vector2(left + (grade - 1) * 7f + 5f, row), NationSea,
                    전국흐름표현Policy.Width(grade));
            Text(new Rect(x + 44, row - 9, 140, 18), "굵기 = 등급 3단(값 비례 아님)", 10, Paper);
            row += 17;
            Chevron(new Vector2(left + 14, row), 0f, 5.5f, NationSea);
            Chevron(new Vector2(left + 5, row), 180f, 3.5f, Fade(NationSea, 0.6f));
            Text(new Rect(x + 44, row - 9, 140, 18), mapEffects ? "흐르는 빛 = 입·출 중 많은 쪽" : "큰 쐐기 = 입·출 중 많은 쪽", 10, Paper);
        }

        private void NationLayerRow(int index, string title, string note, Color color, bool ready, ref bool on)
        {
            var x = NationSide.x;
            var row = new Rect(x + 6, NationSide.y + 34 + index * 31, NationSide.width - 12, 29);
            var dot = new Rect(row.x + 6, row.y + 5, 11, 11);
            var shown = ready && on;
            Paint(dot, shown ? color : Tile, 3);
            if (!shown) Paint(new Rect(dot.x + 2, dot.y + 2, 7, 7), Panel, 2);
            Text(new Rect(row.x + 24, row.y, 150, 17), title, 12, ready ? Paper : Soft, true);
            Text(new Rect(row.x + 24, row.y + 15, 152, 14), note, 9, ready ? Soft : Fade(Soft, 0.6f));
            if (!ready || !Hit(row)) return;
            on = !on;
            Debug.Log($"Hongdal nation map: 층 {title} {(on ? "켬" : "끔")}");
        }

        private bool NationSegment(Rect rect, string label, bool chosen, bool enabled)
        {
            Paint(rect, chosen ? Mint : Tile, 5);
            Text(rect, label, 10, chosen ? Canvas : enabled ? Paper : Fade(Soft, 0.5f), true, TextAnchor.MiddleCenter);
            return enabled && !chosen && Hit(rect);
        }

        private void SetNationIndicator(전국지표 indicator)
        {
            if (nationIndicator == indicator) return;
            nationIndicator = indicator;
            nationPick = null;
            Debug.Log("Hongdal nation map: 지표 " + indicator);
        }

        // ── 오른쪽: 거점 카드 · 출입구 카드 ─────────────────────────────────────

        private void DrawNationPanel()
        {
            var panel = NationPanel;
            Paint(panel, Rim, 11);
            Paint(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Canvas, 10);
            var gate = nationGate == null ? null : nationSpace.gates.FirstOrDefault(item => item.id == nationGate);
            if (gate != null)
            {
                DrawNationGateCard(gate);
                return;
            }
            if (nationHub == null || !nationHubs.TryGetValue(nationHub, out var hub)) return;

            var x = panel.x + 17;
            var y = panel.y;
            var period = nationPeriod;
            Text(new Rect(x, y + 12, 238, 18), (hub.Air ? "공항 · 항공 · 관세청 " : "항만 · 해상 · 관세청 ") +
                전국흐름표현Policy.PeriodLabel(period), 11, Gold, true);
            Text(new Rect(x, y + 32, 238, 30), hub.name, 21, Paper, true);
            Text(new Rect(x, y + 62, 238, 16), string.IsNullOrEmpty(hub.note) ? "원본 항만명 그대로 · 위치는 대략" : hub.note, 10, Soft);

            float next;
            if (hub.Air)
            {
                NationStat(x, y + 86, "입항 운송서류", 전국흐름표현Policy.Sum(hub.monthly, period, 전국흐름표현Policy.InBl), "BL");
                NationStat(x + 122, y + 86, "출항 운송서류", 전국흐름표현Policy.Sum(hub.monthly, period, 전국흐름표현Policy.OutBl), "BL");
                next = y + 150;
            }
            else
            {
                NationStat(x, y + 86, "입항 컨테이너", 전국흐름표현Policy.Sum(hub.monthly, period, 전국흐름표현Policy.InTeu), "TEU");
                NationStat(x + 122, y + 86, "출항 컨테이너", 전국흐름표현Policy.Sum(hub.monthly, period, 전국흐름표현Policy.OutTeu), "TEU");
                NationStat(x, y + 144, "입항 운송서류", 전국흐름표현Policy.Sum(hub.monthly, period, 전국흐름표현Policy.InBl), "BL");
                NationStat(x + 122, y + 144, "출항 운송서류", 전국흐름표현Policy.Sum(hub.monthly, period, 전국흐름표현Policy.OutBl), "BL");
                next = y + 208;
            }

            var teu = 전국흐름표현Policy.UsesTeu(hub.Air, nationIndicator);
            Text(new Rect(x, next, 238, 18), teu ? "상대국 (입+출 TEU)" : "상대국 (입+출 BL 건수)", 11, Soft, true);
            var partners = (hub.partners ?? Array.Empty<NationPartner>())
                .Select(partner => (partner.name, Value: NationPartnerValue(partner, teu, period)))
                .Where(row => row.Value > 0).OrderByDescending(row => row.Value).Take(5).ToArray();
            var top = partners.Length > 0 ? partners[0].Value : 1.0;
            for (var index = 0; index < partners.Length; index++)
            {
                var rowY = next + 22 + index * 22;
                Text(new Rect(x, rowY, 72, 18), partners[index].name, 11, Paper, true);
                Paint(new Rect(x + 74, rowY + 5, Mathf.Max(2f, 96f * (float)(partners[index].Value / top)), 9),
                    Fade(hub.Air || !teu ? Lilac : NationSea, 0.75f), 3);
                Text(new Rect(x + 172, rowY, 66, 18), 전국흐름표현Policy.Full(partners[index].Value), 10, Soft, align: TextAnchor.MiddleRight);
            }
            if (partners.Length == 0) Text(new Rect(x, next + 22, 238, 18), "이 기간 자료 없음", 10, Soft);

            var notes = nationSpace.notes ?? Array.Empty<string>();
            var noteY = panel.y + 360;
            Text(new Rect(x, noteY, 238, 16), "주의", 10, Gold, true);
            Text(new Rect(x, noteY + 17, 238, 15), notes.Length > 0 ? notes[0] : "BL은 운송서류 건수", 9, Soft);
            Text(new Rect(x, noteY + 32, 238, 15), notes.Length > 1 ? notes[1] : "선은 통계 관계 · 항로 아님", 9, Soft);
            Text(new Rect(x, noteY + 47, 238, 15), "출처 관세청 국가별 입·출항물동량 · 내부 검토용", 9, Soft);
            Text(new Rect(x, noteY + 62, 238, 15), "판본 " + nationSpace.version + " · 위치는 대략", 9, Fade(Soft, 0.8f));

            var buttons = panel.yMax - 40;
            if (ExpansionButton(new Rect(x, buttons, 114, 26), "지구본에서 보기", true, Tile))
                NationSay("지구본은 별도 실행판입니다 · 전국 지도와 잇는 방식은 결정 전입니다");
            if (ExpansionButton(new Rect(x + 120, buttons, 118, 26), "가상 회차 보기", true, Mint))
            {
                nationRouteOn = true;
                NationSay("가상 회차 · 부산신항 → 수도권 물류센터 → 사가정 · 업무 상태와 아직 잇지 않음");
            }
        }

        private static double NationPartnerValue(NationPartner partner, bool teu, int period)
        {
            var values = teu ? partner.teu : partner.bl;
            if (values == null || values.Length < 전국흐름표현Policy.Months) return 0;
            if (period != 전국흐름표현Policy.Whole) return period <= values.Length ? values[period - 1] : 0;
            var total = 0.0;
            for (var month = 0; month < 전국흐름표현Policy.Months; month++) total += values[month];
            return total;
        }

        private void NationStat(float x, float y, string label, double value, string unit)
        {
            Paint(new Rect(x, y, 116, 52), Tile, 7);
            Text(new Rect(x + 10, y + 5, 70, 16), label, 10, Soft);
            Text(new Rect(x + 10, y + 5, 96, 16), unit, 9, Soft, align: TextAnchor.MiddleRight);
            Text(new Rect(x + 10, y + 23, 100, 24), 전국흐름표현Policy.Full(value), 15, Paper, true);
        }

        private void DrawNationGateCard(NationGate gate)
        {
            var panel = NationPanel;
            var x = panel.x + 17;
            var y = panel.y;
            Text(new Rect(x, y + 12, 238, 18), "나라 출입구 · 관세청 " + 전국흐름표현Policy.PeriodLabel(nationPeriod), 11, Gold, true);
            Text(new Rect(x, y + 32, 238, 30), gate.arrow + " " + gate.label, 20, Paper, true);
            Text(new Rect(x, y + 62, 238, 16), "나라 방향으로 묶은 것 · 실제 항로 아님", 10, Soft);
            var (sea, air) = NationGateTotals(gate.id);
            NationStat(x, y + 86, "해상 (입+출)", sea, nationIndicator == 전국지표.Teu ? "TEU" : "BL");
            NationStat(x + 122, y + 86, "항공 (입+출)", air, "BL");

            Text(new Rect(x, y + 150, 238, 18), "항만·공항별 (입+출)", 11, Soft, true);
            // 해상 다섯 곳과 항공 세 곳까지. 막대는 해상끼리, 항공끼리 견준다(단위가 다르다).
            var all = nationSpace.flows.Where(flow => flow.gate == gate.id)
                .Select(flow => (Flow: flow, Value: 전국흐름표현Policy.FlowValue(flow.monthly, flow.Air, nationIndicator, nationPeriod)))
                .Where(row => row.Value > 0).OrderByDescending(row => row.Value).ToArray();
            var rows = all.Where(row => !row.Flow.Air).Take(5).Concat(all.Where(row => row.Flow.Air).Take(3)).ToArray();
            var topSea = rows.Where(row => !row.Flow.Air).Select(row => row.Value).DefaultIfEmpty(1).Max();
            var topAir = rows.Where(row => row.Flow.Air).Select(row => row.Value).DefaultIfEmpty(1).Max();
            for (var index = 0; index < rows.Length; index++)
            {
                var (flow, value) = rows[index];
                var rowY = y + 172 + index * 19;
                Text(new Rect(x, rowY, 72, 18), flow.Hub.label, 11, Paper, true);
                Paint(new Rect(x + 74, rowY + 5, Mathf.Max(2f, 96f * (float)(value / (flow.Air ? topAir : topSea))), 9),
                    Fade(flow.Air ? Lilac : NationSea, 0.75f), 3);
                Text(new Rect(x + 172, rowY, 66, 18), 전국흐름표현Policy.Short(value) + (flow.Air ? "건" : nationIndicator == 전국지표.Teu ? "" : "건"),
                    10, Soft, align: TextAnchor.MiddleRight);
            }

            var countries = gate.countries ?? Array.Empty<string>();
            var listY = y + 330;
            Text(new Rect(x, listY, 238, 16), $"묶은 나라 {countries.Length}곳 · 원본 표기", 10, Soft, true);
            Text(new Rect(x, listY + 18, 238, 46), string.Join(" · ", countries.Take(12)) + (countries.Length > 12 ? " …" : string.Empty),
                9, Soft, wrap: true);
            Text(new Rect(x, listY + 70, 238, 32), "지구본은 별도 실행판이라 아직 이 나라로 넘어가지 않습니다 (연결 방식 결정 전)", 10, Gold,
                wrap: true);
            if (ExpansionButton(new Rect(x, panel.yMax - 40, 114, 26), "거점 카드로", true, Tile))
                nationGate = null;
        }
    }
}
