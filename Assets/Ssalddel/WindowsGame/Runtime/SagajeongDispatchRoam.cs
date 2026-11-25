using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 계속 돌아다니는 기사 (R4, 2026-11-14) — 반딧불이 같은 빛으로 (같은 날 고침).
    // 사용자 방향: 고용할 때마다 기사가 지도에 나타나, 큰길과 건물 사이 골목을 계속 돌아다니며 픽업지와 전달지를 오가는 모습을 보고 싶다.
    //            기사는 모델(2D 표식 · 3D 오토바이)이 아니라 빛으로 보인다. 기사가 있는 자리가 밝아지고, 지나온 길이 한동안 밝다가
    //            멀어지면 어두워진다. 기사가 많을수록 어두운 지도가 더 밝아진다. 종류에 따라 빛이 다르다.
    //  - 표현 층이다. 동마다 고용한 기사 수(동별 수요의 값)만큼 빛이 나타나 길 그래프 위를 다닌다.
    //    주문 한 건 한 건은 커널에 없다(동별 수요는 묶음 수치다). 여기서 오가는 픽업지·전달지는 보여 주기 위한 가상의 차례이고,
    //    돈·주문 수·시계·저장을 바꾸지 않는다. 실제 주문·배차·사람과 잇지 않는다.
    //  - 픽업지는 그 동에서 밝혀 둔 제휴 음식점의 가상 자리(관리 건물), 전달지는 그 동의 아무 건물이다. 출입구는 가상 출입구다.
    //  - 길은 길찾기Policy가 찾는다(큰길이 골목보다 빠르다). 같은 기사는 같은 차례로 다닌다.
    //  - 빛의 종류: 기사 = 연둣빛 반딧불이(지나온 길에 자취가 남는다), 음식점 = 주황빛(늘 옅게, 픽업이 닿으면 잠깐 밝게),
    //    전달지 = 하늘빛(전달이 닿으면 잠깐). 자주 다닌 길에는 옅은 온기가 쌓인다. 빛의 양은 기사불빛판에 적고 셰이더가 읽는다.
    //    땅 · 길 · 지붕 · 벽이 함께 밝아지므로 기사가 건물 뒤 골목에 있어도 그 둘레가 밝아진다.
    //  - 이름표 대신 빛(같은 날): 매장 · 배송지 · 역할 건물도 이름표를 세워 두지 않고 빛 한 점으로 그린다. 빛에 올리면 그때만
    //    이름과 상태가 뜬다. 누르는 동작은 이름표 때와 같다. 주문이 기다리는 매장은 금빛으로 숨 쉬듯 밝아진다.
    //  - 누르기 한 벌(빛2, 같은 날): 빛을 한 번 누르면 오른쪽 칸 한 자리에 카드가 뜨고, 두 번 누르면 그 대상이 속한 사업 창이 열리고,
    //    오른쪽 단추로 누르면 차림표가 열린다. 누르기는 시간·돈·주문을 바꾸지 않는다(매장에 배정하는 기존 동작만 그대로다).
    //  - 빛의 숨(같은 날): 빛은 가만히 있지 않고 밝아졌다 줄어든다. 종류마다 박자가 다르다(기사는 빠르게, 음식점은 천천히).
    //    빛 한 점과 땅의 빛이 같은 식(LightBeat ↔ 셰이더)으로 함께 숨 쉰다. 실제 시간으로 흐르므로 게임을 정지해도 숨은 쉰다.
    //    기사가 움직이고 자취가 식는 것은 전과 같이 게임이 흐를 때만이다.
    //  - 배차 기사의 빛은 평면·디오라마와 검증 촬영에서 같은 상태 좌표에 표시한다. 순찰과 지면 자취는 해당 동의 길 그래프를 사용한다.
    public sealed partial class SagajeongDispatchView
    {
        private const string RouteGraphResource = "hongdal-dong-routes";
        private const int RoamLimit = 60;                 // 한꺼번에 보이는 기사의 위쪽 끝
        private const int RoamPerDong = 12;
        private const float RoamLightEvery = 0.08f;       // 빛 그림을 다시 올리는 사이 (실제 초)
        private const float RoamLightStride = 8f;         // 그 사이에 기사가 이보다 멀리 갔으면 사이사이에도 빛을 놓는다 (m)

        private static readonly int ActivityMapId = Shader.PropertyToID("_ActivityMap");
        private static readonly int ActivityRectId = Shader.PropertyToID("_ActivityRect");
        private static readonly int ActivityOnId = Shader.PropertyToID("_ActivityOn");
        private static readonly int ActivityBeatId = Shader.PropertyToID("_ActivityBeat");
        // 빛의 숨. 셰이더(HongdalDiorama.shader)의 식과 같은 값이어야 한다. 빠르기는 모두 0.1의 배수다(62.83초마다 이음매 없이 되돈다).
        private const float BeatWrap = 62.8318531f;
        private const float RiderBeat = 4.2f, StoreBeat = 2.3f, DropBeat = 3.1f;      // 1초에 몇 라디안 (기사 약 1.5초, 음식점 약 2.7초, 배송지 약 2초)
        private const float RiderLag = 1f, StoreLag = 0.7f, DropLag = 1.3f;           // 자리에 따라 엇갈리는 정도
        private static readonly Color FireflyCore = Ink(0xF6FFC2);
        private static readonly Color FireflyHalo = Ink(0xC9F25A);
        private static readonly Color StoreCore = Ink(0xFFE0B8);
        private static readonly Color StoreHalo = Ink(0xFF9F45);
        private static readonly Color DropCore = Ink(0xE2F6FF);
        private static readonly Color DropHalo = Ink(0x6CCBFF);
        private const float DropRestLight = 0.4f;         // 배송지가 늘 띠는 빛 (표현값)
        private const float GlowNameReach = 22f;          // 빛에 이만큼 가까이 올리면 이름이 뜬다 (논리 픽셀)

        private bool roamOn = true;
        private 길그래프 routeGraph;
        private bool routeGraphTried;
        private readonly List<Roamer> roamers = new();
        private readonly Dictionary<string, List<int>> roamDoors = new(StringComparer.Ordinal);     // 지역 ID → 출입구가 있는 건물
        private float roamMetres;                         // 지금까지 달린 거리 (표현)
        private int roamJobs;                             // 지금까지 마친 가상의 차례 (표현)
        private int roamProofExtra;                       // 방향 촬영에서만: 동마다 더 세우는 빛 (고용이 아니다)

        private 기사불빛판 roamLight;                     // 빛의 양 (순수 규칙)
        private Texture2D roamLightMap;                   // 셰이더가 읽는 그림
        private Texture2D roamGlow;                       // 빛 한 점 (가운데가 밝고 둘레로 스러지는 둥근 그림)
        private float roamLightDue;                       // 아직 빛에 반영하지 않은 표현의 시간
        private float roamLightAt = float.NegativeInfinity;      // 마지막으로 그림을 올린 때 (실제 초)
        private bool roamLightShown;                      // 셰이더에 빛을 켜 두었는가
        private int roamLightStamp = int.MinValue;        // 마지막으로 올린 때의 모습 (정지한 동안 다시 올리지 않으려고)
        private float roamClock;                          // 표현의 시간 (기사가 움직인 시간. 정지하면 멈춘다)
        private float lightBeat;                          // 빛의 숨이 읽는 시계 (실제 초를 BeatWrap으로 되돌린 값)

        private string glowNameKey;                       // 이번 그리기에서 올려 둔 빛 (없으면 null)
        private string glowNameTitle;
        private string glowNameStatus;
        private Vector2 glowNamePoint;
        private Color glowNameColour;
        private string glowProofName;                     // 방향 촬영에서만: 이 빛에 올린 것으로 본다
        private int glowPlaces;                           // 이번 그리기에서 빛으로 그린 곳의 수
        private int glowPlacesShown;                      // 지난 그리기에서의 수
        private readonly HashSet<int> glowRoleSites = new();      // 이번 그리기에서 빛으로 그린 역할 건물

        private enum LightTouch { None, Click, Open, Menu }       // 누르지 않음 · 한 번 · 두 번 · 오른쪽 단추
        private bool mapDoubleClick;                      // 이번 누름이 두 번 누르기의 두 번째인가 (HandleMapPointer)
        private bool pressDouble;
        private bool mapMenuClick;                        // 오른쪽 단추를 지도에서 뗐는가 (HandleMapPointer)
        private Vector2 menuPoint;
        private List<(string Label, Action Run)> lightMenu;       // 열려 있는 차림표 (없으면 null)
        private string lightMenuTitle;
        private Vector2 lightMenuAt;

        private sealed class Roamer
        {
            public string Region;
            public int Index;
            public uint Seed;
            public readonly List<(float X, float Z, byte Kind)> Path = new();
            public int Leg;
            public float Along;
            public float X, Z, Heading;
            public float Wait;
            public int Phase;        // 0: 픽업지로, 1: 전달지로
            public int At = -1;      // 마지막으로 닿은 건물
            public int Target = -1;
            public float LitX, LitZ; // 마지막으로 빛을 놓은 자리
            public bool Lit;
            public float Blink;      // 깜빡임의 엇갈림
        }

        private bool RoamActive => roamOn && mapLevel == MapLevel.Dong && DongModulesActive && EnsureRouteGraph() != null;

        private 길그래프 EnsureRouteGraph()
        {
            if (routeGraphTried || buildingShapes.Length == 0) return routeGraph;
            routeGraphTried = true;
            var asset = Resources.Load<TextAsset>(RouteGraphResource);
            if (asset == null)
            {
                Debug.LogWarning("Hongdal route graph: resource missing");
                return null;
            }
            var graph = 길그래프.Read(asset.bytes, out var problem);
            if (graph != null && graph.Buildings != buildingShapes.Length)
            {
                problem = "건물 수가 색인과 다릅니다 (" + graph.Buildings + " / " + buildingShapes.Length + ")";
                graph = null;
            }
            if (graph == null)
            {
                Debug.LogWarning("Hongdal route graph: " + problem);
                return null;
            }
            roamDoors.Clear();
            for (var building = 0; building < buildingShapes.Length; building++)
            {
                var region = buildingShapes[building].Record.region;
                if (string.IsNullOrEmpty(region) || !graph.HasDoor(building)) continue;
                if (!roamDoors.TryGetValue(region, out var list)) roamDoors[region] = list = new List<int>();
                list.Add(building);
            }
            routeGraph = graph;
            Debug.Log($"Hongdal route graph: nodes {graph.Nodes}, edges {graph.Edges}, buildings with door {roamDoors.Values.Sum(list => list.Count)}/{graph.Buildings}");
            return routeGraph;
        }

        // 동마다 고용한 기사 수만큼 빛을 두고, 게임이 흐르는 동안 길을 따라 옮긴다. (LateUpdate)
        private void SyncRoamers()
        {
            if (!RoamActive)
            {
                HideRoamLight();
                return;
            }
            Shader.SetGlobalFloat(ActivityBeatId, lightBeat);
            SyncMusicLight();            // 고른 곡이 나오는 동안에는 땅의 숨을 음악에 맞춘다 (SagajeongDispatchMusic.cs)
            foreach (var module in dongModules)
            {
                if (!module.Loaded) continue;
                var region = module.Entry.regionId;
                var trade = DongTrade(region);
                var wanted = trade != null && trade.Open ? Mathf.Min(trade.Riders, RoamPerDong) : 0;
                wanted = Mathf.Min(wanted + roamProofExtra, RoamPerDong);
                var have = 0;
                foreach (var roamer in roamers)
                    if (roamer.Region == region) have++;
                while (have < wanted && roamers.Count < RoamLimit && SpawnRoamer(region, have)) have++;
                if (have > wanted) roamers.RemoveAll(roamer => roamer.Region == region && roamer.Index >= wanted);
            }
            if (campaignSpeed > 0 && !holdProofClock) StepRoamers(Time.deltaTime * campaignSpeed);
            if (Time.unscaledTime - roamLightAt < RoamLightEvery) return;
            var stamp = roamers.Count * 31 + managedPartnerSites.Count;
            if (roamLightShown && roamLightDue <= 0f && stamp == roamLightStamp) return;      // 정지한 동안에는 그대로 둔다
            roamLightStamp = stamp;
            StepRoamLight();
            ShowRoamLight();
        }

        private bool SpawnRoamer(string region, int index)
        {
            if (!roamDoors.TryGetValue(region, out var doors) || doors.Count < 2) return false;
            var roamer = new Roamer { Region = region, Index = index, Seed = 기사순찰Policy.Seed(region, index) };
            roamer.Blink = (roamer.Seed % 628u) * 0.01f;
            var pickups = RoamPickups(region);
            roamer.At = pickups.Count > 0 ? pickups[기사순찰Policy.Pick(ref roamer.Seed, pickups.Count)] : doors[기사순찰Policy.Pick(ref roamer.Seed, doors.Count)];
            var start = routeGraph.Along(routeGraph.DoorEdge[roamer.At], routeGraph.DoorT[roamer.At]);
            roamer.X = start.X;
            roamer.Z = start.Z;
            roamer.Phase = 1;                // 픽업지에서 시작해 전달지로 간다
            roamers.Add(roamer);
            return true;
        }

        // 그 동에서 밝혀 둔 제휴 음식점의 가상 자리 가운데 출입구가 있는 건물.
        private List<int> RoamPickups(string region)
        {
            var pickups = new List<int>();
            foreach (var building in managedPartnerSites)
                if (building >= 0 && building < buildingShapes.Length && buildingShapes[building].Record.region == region && routeGraph.HasDoor(building))
                    pickups.Add(building);
            pickups.Sort();
            return pickups;
        }

        private void PlanRoam(Roamer roamer)
        {
            var doors = roamDoors[roamer.Region];
            var pickups = roamer.Phase == 0 ? RoamPickups(roamer.Region) : null;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var target = pickups != null && pickups.Count > 0
                    ? pickups[기사순찰Policy.Pick(ref roamer.Seed, pickups.Count)]
                    : doors[기사순찰Policy.Pick(ref roamer.Seed, doors.Count)];
                if (target == roamer.At) continue;
                if (!길찾기Policy.Route(routeGraph, roamer.At, target, roamer.Path, out var metres) || metres < 1f) continue;
                roamer.Target = target;
                roamer.Leg = 0;
                roamer.Along = 0f;
                return;
            }
            roamer.Path.Clear();
            roamer.Phase = 1;                // 갈 픽업지가 없으면(제휴 한 곳뿐일 때) 전달지부터 다시 고른다
            roamer.Wait = 1f;
        }

        // seconds: 게임 배속을 곱한 화면의 시간. 표현의 시간이고 게임 시계가 아니다.
        private void StepRoamers(float seconds)
        {
            if (routeGraph == null || seconds <= 0f) return;
            roamLightDue += seconds;
            roamClock += seconds;
            var scaled = seconds * 기사순찰Policy.Pace;
            StepNavRider(scaled);            // 길찾기로 보낸 기사도 같은 시간으로 (SagajeongDispatchNavigation.cs)
            foreach (var roamer in roamers)
            {
                var left = scaled;
                if (roamer.Wait > 0f)
                {
                    var used = Mathf.Min(roamer.Wait, left);
                    roamer.Wait -= used;
                    left -= used;
                    if (roamer.Wait > 0f) continue;
                }
                if (roamer.Path.Count < 2)
                {
                    PlanRoam(roamer);
                    if (roamer.Path.Count < 2) continue;
                }
                var beforeX = roamer.X;
                var beforeZ = roamer.Z;
                var arrived = 기사순찰Policy.Advance(roamer.Path, ref roamer.Leg, ref roamer.Along, left, out roamer.X, out roamer.Z, out roamer.Heading);
                roamMetres += Mathf.Sqrt((roamer.X - beforeX) * (roamer.X - beforeX) + (roamer.Z - beforeZ) * (roamer.Z - beforeZ));
                if (!arrived) continue;
                roamer.At = roamer.Target;
                roamer.Wait = roamer.Phase == 0 ? 기사순찰Policy.PickupWait : 기사순찰Policy.DropWait;
                FlashRoamStop(roamer);
                if (roamer.Phase == 1) roamJobs++;
                roamer.Phase = 1 - roamer.Phase;
                roamer.Path.Clear();
            }
        }

        // 닿은 곳이 잠깐 밝아진다: 음식점은 주황빛, 전달지는 하늘빛.
        private void FlashRoamStop(Roamer roamer)
        {
            if (roamLight == null || roamer.At < 0 || roamer.At >= buildingShapes.Length) return;
            var shape = buildingShapes[roamer.At];
            var centre = (shape.Min + shape.Max) * 0.5f;
            if (roamer.Phase == 1) roamLight.Light(기사불빛Policy.Drop, centre.x, centre.y, 기사불빛Policy.DropRadius, 1f);
            else if (managedPartnerSites.Contains(roamer.At))
                roamLight.Light(기사불빛Policy.Store, centre.x, centre.y, 기사불빛Policy.StoreRadius, 1f);
        }

        // ── 빛 ──
        // 지나간 시간만큼 어둡게 하고, 기사가 있는 자리와 음식점에 빛을 놓는다. 그림은 올리지 않는다.
        private void StepRoamLight()
        {
            if (routeGraph == null) return;
            if (roamLight == null)
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (var node = 0; node < routeGraph.Nodes; node++)
                {
                    minX = Mathf.Min(minX, routeGraph.X[node]);
                    maxX = Mathf.Max(maxX, routeGraph.X[node]);
                    minZ = Mathf.Min(minZ, routeGraph.Z[node]);
                    maxZ = Mathf.Max(maxZ, routeGraph.Z[node]);
                }
                if (routeGraph.Nodes == 0) return;
                roamLight = new 기사불빛판(minX, minZ, maxX, maxZ, 기사불빛Policy.CellMetres, 기사불빛Policy.Margin);
                Debug.Log($"Hongdal activity light: {roamLight.Width} x {roamLight.Height} cells, {roamLight.Cell:0.0} m each");
            }
            var seconds = roamLightDue;
            roamLightDue = 0f;
            roamLight.Fade(seconds);
            foreach (var roamer in navRider == null ? (IEnumerable<Roamer>)roamers : roamers.Append(navRider))      // 길찾기 기사도 같은 자취를 남긴다
            {
                var dx = roamer.X - roamer.LitX;
                var dz = roamer.Z - roamer.LitZ;
                var gap = Mathf.Sqrt(dx * dx + dz * dz);
                var steps = roamer.Lit && gap < 160f ? Mathf.Clamp(Mathf.CeilToInt(gap / RoamLightStride), 1, 20) : 1;
                for (var k = 1; k <= steps; k++)
                    roamLight.Ride(roamer.X - dx * (steps - k) / steps, roamer.Z - dz * (steps - k) / steps, seconds / steps);
                roamer.LitX = roamer.X;
                roamer.LitZ = roamer.Z;
                roamer.Lit = true;
            }
            // 배차를 받는 기사(사가정 배달권의 기사 A · B)도 같은 빛이다. 자리는 게임 상태의 값을 그대로 읽는다.
            if (state?.couriers != null && dongManifest != null)
            {
                foreach (var courier in state.couriers)
                {
                    var (east, north) = DongLocal(courier.latitude, courier.longitude);
                    var moving = courier.route != null && courier.route.Length >= 2;      // 서 있는 동안에는 온기가 쌓이지 않는다
                    roamLight.Ride((float)east, (float)north, moving ? seconds : 0f);
                }
            }
            // 음식점 빛의 자리를 먼저 모은다. 몰린 곳은 옅게 놓는다 (SagajeongDispatchLightCrowd.cs).
            crowdGround.Clear();
            foreach (var building in managedPartnerSites)
                if (building >= 0 && building < buildingShapes.Length) crowdGround.Add(BuildingMid(building));
            if (state?.stores != null && dongManifest != null)
            {
                foreach (var store in state.stores)
                {
                    var (east, north) = DongLocal(store.latitude, store.longitude);
                    crowdGround.Add(new Vector2((float)east, (float)north));
                }
            }
            foreach (var building in managedPartnerSites)
            {
                if (building < 0 || building >= buildingShapes.Length) continue;
                var shape = buildingShapes[building];
                var centre = (shape.Min + shape.Max) * 0.5f;
                roamLight.Light(기사불빛Policy.Store, centre.x, centre.y, 기사불빛Policy.StoreRadius, 기사불빛Policy.StoreRest * GroundCrowd(centre));
            }
            // 매장과 배송지도 제 빛으로 둘레를 밝힌다. 주문이 기다리는 매장은 가득 밝다.
            if (state?.stores != null && dongManifest != null)
            {
                foreach (var store in state.stores)
                {
                    var waiting = false;
                    if (state.orders != null)
                        foreach (var order in state.orders)
                            if (order.storeId == store.id && order.stage == "Waiting") waiting = true;
                    var (east, north) = DongLocal(store.latitude, store.longitude);
                    roamLight.Light(기사불빛Policy.Store, (float)east, (float)north, 기사불빛Policy.StoreRadius,
                        waiting ? 1f : 기사불빛Policy.StoreRest * GroundCrowd(new Vector2((float)east, (float)north)));
                }
            }
            if (state?.destinations != null && dongManifest != null)
            {
                foreach (var destination in state.destinations)
                {
                    var (east, north) = DongLocal(destination.latitude, destination.longitude);
                    roamLight.Light(기사불빛Policy.Drop, (float)east, (float)north, 기사불빛Policy.DropRadius, DropRestLight);
                }
            }
            LightSubwayStations();      // 역 둘레도 제 빛으로 밝다 (SagajeongDispatchSubway.cs)
        }

        // 빛의 양을 그림으로 올리고 셰이더에 알린다.
        private void ShowRoamLight()
        {
            if (roamLight == null) return;
            if (roamLightMap == null)
            {
                roamLightMap = new Texture2D(roamLight.Width, roamLight.Height, TextureFormat.RGBA32, false, true)
                {
                    name = "Hongdal activity light", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
            }
            roamLightMap.LoadRawTextureData(roamLight.Cells);
            roamLightMap.Apply(false, false);
            Shader.SetGlobalTexture(ActivityMapId, roamLightMap);
            Shader.SetGlobalVector(ActivityRectId, new Vector4(roamLight.X0, roamLight.Z0,
                1f / (roamLight.Width * roamLight.Cell), 1f / (roamLight.Height * roamLight.Cell)));
            Shader.SetGlobalFloat(ActivityOnId, 1f);
            roamLightShown = true;
            roamLightAt = Time.unscaledTime;
        }

        // 기사가 나오지 않는 화면(넓은 지도, 검증 촬영)에서는 빛을 끈다. 빛의 양은 남겨 둔다.
        private void HideRoamLight()
        {
            if (!roamLightShown) return;
            Shader.SetGlobalFloat(ActivityOnId, 0f);
            roamLightShown = false;
        }

        private void EnsureRoamGlow()
        {
            if (roamGlow != null) return;
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var near = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(near * near * 255f));
                }
            }
            roamGlow = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Hongdal firefly", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            roamGlow.SetPixels32(pixels);
            roamGlow.Apply(false, true);
        }

        // 빛의 숨: 밝아졌다 줄어드는 물결(-1~1). x · z는 메시 좌표(m). 자리에 따른 엇갈림은 셰이더의 땅의 빛과 같은 식이고,
        // phase는 빛마다의 엇갈림이다(이웃한 빛이 번갈아 퍼지게. SagajeongDispatchLightCrowd.cs).
        // 고른 곡이 나오는 동안에는 곡의 박에 맞춰 퍼진다(SagajeongDispatchMusic.cs).
        private float LightBeat(float x, float z, float rate, float lag, float phase = 0f) =>
            MusicDrivesLight ? MusicLightBeat(x, z, rate, lag, phase) : Mathf.Sin(lightBeat * rate + (x * 0.02f + z * 0.014f) * lag + phase);

        private float LightBeatAt(double latitude, double longitude, float rate, float lag, float phase = 0f)
        {
            if (dongManifest == null) return LightBeat(0f, 0f, rate, lag, phase);
            var (east, north) = DongLocal(latitude, longitude);
            return LightBeat((float)east, (float)north, rate, lag, phase);
        }

        // 빛 한 점: 가운데가 밝고 둘레로 스러진다. size는 지름(논리 픽셀). 가까이 볼수록 조금 커진다.
        // 빛이 몰린 곳에서는 연한 부분이 옅어진다. 가운데 점은 GlowCore로 맨 위에 그린다 (SagajeongDispatchLightCrowd.cs).
        private void Glow(Vector2 centre, float size, Color colour)
        {
            if (Event.current.type != EventType.Repaint) return;
            size *= GlowZoom;
            colour.a *= GlowCrowd(centre, size * 0.45f);
            GUI.DrawTexture(Centered(centre, size, size), roamGlow, ScaleMode.StretchToFill, true, 0, colour, 0, 0);
        }

        private float GlowZoom => Mathf.Clamp(0.8f + 0.25f * mapZoom, 0.9f, 1.8f);

        private void GlowNow(Vector2 centre, float size, Color colour)
        {
            size *= GlowZoom;
            GUI.DrawTexture(Centered(centre, size, size), roamGlow, ScaleMode.StretchToFill, true, 0, colour, 0, 0);
        }

        // 지금 있는 동과 함께 옮긴다. 숨긴 동에 들어가 있으면 그리지 않는다(「이 동만」 보기에서 이웃 동의 길을 지날 때).
        private Vector2 PlotRoam(DongModuleState home, float x, float z)
        {
            var latitude = buildingIndex.origin.latitude + z / buildingIndex.metresPerDegreeLatitude;
            var longitude = buildingIndex.origin.longitude + x / buildingIndex.metresPerDegreeLongitude;
            var here = DongModuleAt(latitude, longitude);
            if (dongView == DongView.Solo && (here == null || !DongVisible(here))) return DongHiddenPoint;
            return PlotSceneIn(here ?? home, latitude, longitude, 0.4f);
        }

        // 기사의 빛: 연둣빛 반딧불이 한 점. 표식도 선도 그리지 않는다. 땅과 건물이 밝아지는 것은 셰이더가 한다. (DrawBoard)
        private void DrawRoamers()
        {
            if (!RoamActive || Event.current.type != EventType.Repaint) return;
            EnsureRoamGlow();
            foreach (var roamer in roamers)
            {
                var module = DongModuleById(roamer.Region);
                var point = PlotRoam(module, roamer.X, roamer.Z);
                if (point == DongHiddenPoint || !Field.Contains(point)) continue;
                if (actorFocus is { } chosen && chosen.Kind == FocusKind.Rider && chosen.Id == roamer.Region + "#" + roamer.Index)
                    Hoop(point, 17f, Fade(Paper, 0.9f), 2f);
                // 그 자리의 땅의 빛과 같은 박자에, 기사마다 조금 다른 잔떨림을 얹는다.
                var beat = Mathf.Clamp(LightBeat(roamer.X, roamer.Z, RiderBeat, RiderLag, lightSeparate ? roamer.Blink : 0f) + 0.25f * Mathf.Sin(lightBeat * 9.1f + roamer.Blink), -1f, 1f);
                var bright = 1f + 0.34f * beat;
                var wide = 1f + 0.18f * beat;
                Glow(point, 58f * wide, Fade(FireflyHalo, 0.4f * bright));
                Glow(point, 26f * wide, Fade(FireflyHalo, 0.75f * bright));
                GlowCore(point, 12f * (1f + 0.1f * beat), FireflyCore);
            }
        }

        // 음식점(제휴 가상 자리)의 빛: 주황빛 한 점. 빛으로 보는 동안에는 옥상의 운영 표식 대신 이것을 그린다. (DrawManagedMarks)
        private bool DrawStoreGlow(int building)
        {
            if (!RoamActive) return false;
            EnsureRoamGlow();
            var top = RoofCentre(buildingShapes[building]);
            if (top.x < -24 || top.y < -24 || top.x > Field.width + 24 || top.y > Field.height + 24) return true;
            var site = buildingShapes[building];
            var beat = LightBeat((site.Min.x + site.Max.x) * 0.5f, (site.Min.y + site.Max.y) * 0.5f, StoreBeat, StoreLag, LightPhase((uint)building));
            Glow(top, 36f * (1f + 0.14f * beat), Fade(StoreHalo, 0.34f * (1f + 0.32f * beat)));
            Glow(top, 16f * (1f + 0.14f * beat), Fade(StoreHalo, 0.6f * (1f + 0.32f * beat)));
            GlowCore(top, 8f, StoreCore);
            GlowName("partner:" + building, top, "제휴 음식점", "가상 자리", StoreHalo);
            return true;
        }

        // 실제 화면을 검증하는 동안에만 그린 기사의 지도 좌표와 색을 관찰한다.
        private Action<DispatchCourier, Vector2, Color> courierLightProbe;

        // 배차 기사는 상태 사본의 좌표와 기사 고유색을 쓴다. 순찰 길이나 출입구 자료 없이도 표시한다.
        private void DrawCourierGlow(DispatchCourier courier, Vector2 position)
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureRoamGlow();
            var colour = RiderColor(courier.id);
            var key = "courier:" + courier.id;
            var beat = LightBeatAt(courier.latitude, courier.longitude, RiderBeat, RiderLag, LightPhase(key));
            var grow = MingdeGlow("courier", courier.id);     // 명1: 쌓인 명덕만큼 크고 밝게. 자료 준비 전이면 지금과 같다 (SagajeongDispatchMingdeLight.cs)
            Glow(position, 62f * grow.Size * (1f + 0.18f * beat), Fade(colour, 0.4f * grow.Strength * (1f + 0.34f * beat)));
            Glow(position, 28f * grow.Size * (1f + 0.18f * beat), Fade(colour, 0.75f * grow.Strength * (1f + 0.3f * beat)));
            GlowCore(position, 12f * (0.5f + 0.5f * grow.Strength) * (1f + 0.1f * beat), FireflyCore);
            GlowName(key, position, "기사 " + Letter(courier.id), null, colour);
            if (courierLightProbe != null && Field.Contains(position + Field.position))
                courierLightProbe(courier, position, colour);
        }

        // ── 이름표 대신 빛 ──
        // 빛에 올리면 그때만 이름이 뜬다. 이름표를 늘 세워 두지 않는다. 그리는 차례에만 본다.
        private void GlowName(string key, Vector2 point, string title, string status, Color colour)
        {
            if (Event.current.type != EventType.Repaint) return;
            glowPlaces++;
            bool over;
            if (glowProofName != null) over = glowProofName == key;
            else
            {
                var mouse = Event.current.mousePosition;
                over = (mouse - point).sqrMagnitude <= GlowNameReach * GlowNameReach && Field.Contains(mouse + Field.position) &&
                    !MapOverlayCovers(mouse + Field.position);
            }
            if (!over) return;
            glowNameKey = key;
            glowNameTitle = title;
            glowNameStatus = status;
            glowNamePoint = point;
            glowNameColour = colour;
        }

        // 올려 둔 빛의 이름을 맨 위에 그린다. (DrawBoard의 끝)
        private void DrawGlowName()
        {
            TouchLooseLights();          // 고용한 기사와 제휴 음식점의 빛은 매장·배차 기사보다 뒤에 누름을 받는다
            if (Event.current.type != EventType.Repaint) return;
            FlushGlowCores();            // 가운데 점은 모든 빛의 연한 부분 위에 (SagajeongDispatchLightCrowd.cs)
            glowPlacesShown = glowPlaces;
            glowPlaces = 0;
            glowRoleSites.Clear();
            DrawLightMenu();
            if (glowNameKey == null) return;
            if (lightMenu != null)
            {
                glowNameKey = null;      // 차림표가 열려 있는 동안에는 이름을 띄우지 않는다
                return;
            }
            var title = glowNameTitle ?? string.Empty;
            var status = glowNameStatus;
            var width = Mathf.Max(title.Length, string.IsNullOrEmpty(status) ? 0 : status.Length) * 11f + 26f;
            var height = string.IsNullOrEmpty(status) ? 24f : 38f;
            var plate = new Rect(glowNamePoint.x - width / 2, glowNamePoint.y - 26f - height, width, height);
            plate.x = Mathf.Clamp(plate.x, 4f, Mathf.Max(4f, Field.width - width - 4f));
            if (plate.y < 4f) plate.y = glowNamePoint.y + 26f;
            Paint(plate, glowNameColour, 8);
            Paint(new Rect(plate.x + 1.5f, plate.y + 1.5f, plate.width - 3, plate.height - 3), Canvas, 7);
            Text(new Rect(plate.x, plate.y + 2, plate.width, 20), title, 12, Paper, true, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(status))
                Text(new Rect(plate.x, plate.y + 19, plate.width, 16), status, 10, glowNameColour, align: TextAnchor.MiddleCenter);
            glowNameKey = null;
        }

        // 그 자리에 배차 기사의 빛이 있는가 (겹쳤을 때 누구를 누른 것인지 가리려고).
        private bool CourierNear(Vector2 point)
        {
            if (state?.couriers == null) return false;
            foreach (var courier in state.couriers)
                if ((PlotScene(courier.latitude, courier.longitude) - point).sqrMagnitude <= 23f * 23f) return true;
            return false;
        }

        // 매장의 빛: 주황빛 한 점. 주문이 기다리면 금빛으로 숨 쉬듯 밝아진다. 누르는 동작은 이름표와 같다. (DrawStore)
        private bool DrawStoreLight(DispatchStore store, Vector2 position, bool available)
        {
            if (!RoamActive) return false;
            var colour = available ? Gold : StoreHalo;
            if (Event.current.type == EventType.Repaint)
            {
                EnsureRoamGlow();
                if (IsFocused(FocusKind.Store, store.id)) Hoop(position, 22f, Fade(Paper, 0.9f), 2f);
                // 늘 천천히 숨 쉬고, 주문이 기다리면 더 빠르고 크게 숨 쉰다.
                var phase = LightPhase("store:" + store.id);
                var beat = available ? Mathf.Sin(lightBeat * 4.6f + phase) : LightBeatAt(store.latitude, store.longitude, StoreBeat, StoreLag, phase);
                var bright = 1f + (available ? 0.36f : 0.28f) * beat;
                var wide = 1f + (available ? 0.2f : 0.14f) * beat;
                var grow = MingdeGlow("store", store.id);     // 명1: 쌓인 명덕만큼 크고 밝게. 자료 준비 전이면 지금과 같다
                Glow(position, (available ? 66f : 54f) * wide * grow.Size, Fade(colour, 0.42f * bright * grow.Strength));
                Glow(position, 28f * wide * grow.Size, Fade(colour, 0.8f * bright * grow.Strength));
                GlowCore(position, 12f * (0.5f + 0.5f * grow.Strength), StoreCore);
            }
            GlowName("store:" + store.id, position, "매장 " + Letter(store.id), available ? "주문이 기다립니다" : null, colour);
            // 기사의 빛과 겹쳤으면: 기사를 고르지 않은 때에는 기사를, 고른 뒤에는 매장을 누른 것으로 본다.
            if (string.IsNullOrEmpty(selectedDriver) && CourierNear(LightPointer())) return true;
            var touch = TouchLight(position, 20f);
            if (touch == LightTouch.Click) ConnectStore(store.id);
            else if (touch == LightTouch.Open) OpenBusinessAt(BusinessRestaurant, store.latitude, store.longitude);
            else if (touch == LightTouch.Menu) OpenStoreMenu(store, position, available);
            return true;
        }

        // 배송지의 빛: 하늘빛 한 점. 번호는 올렸을 때 뜬다. (DrawBoard)
        private bool DrawDropLight(int index, Vector2 point)
        {
            if (!RoamActive) return false;
            if (Event.current.type == EventType.Repaint)
            {
                EnsureRoamGlow();
                if (actorFocus is { } chosen && chosen.Kind == FocusKind.Drop && chosen.Id == index.ToString()) Hoop(point, 18f, Fade(Paper, 0.9f), 2f);
                var spot = state?.destinations != null && index < state.destinations.Length ? state.destinations[index] : null;
                var beat = spot == null ? 0f : LightBeatAt(spot.latitude, spot.longitude, DropBeat, DropLag, LightPhase("drop:" + index));
                Glow(point, 46f * (1f + 0.16f * beat), Fade(DropHalo, 0.4f * (1f + 0.32f * beat)));
                Glow(point, 22f * (1f + 0.16f * beat), Fade(DropHalo, 0.78f * (1f + 0.26f * beat)));
                GlowCore(point, 10f, DropCore);
            }
            GlowName("drop:" + index, point, "배송지 " + (index + 1), null, DropHalo);
            if (CourierNear(LightPointer())) return true;      // 기사의 빛과 겹쳤으면 기사가 먼저다
            var touch = TouchLight(point, 18f);
            if (touch == LightTouch.Click || touch == LightTouch.Open) FocusLight(FocusKind.Drop, index.ToString());
            else if (touch == LightTouch.Menu)
            {
                var id = index.ToString();
                OpenLightMenu(point, "배송지 " + (index + 1), new List<(string Label, Action Run)>
                {
                    ("정보 보기", () => FocusLight(FocusKind.Drop, id)),
                });
            }
            return true;
        }

        // 역할 건물(카페 · 창고)의 빛: 그 사업의 색 한 점. 이름과 상태는 올렸을 때 뜬다. 누르면 전과 같이 그곳을 고른다. (DrawRoleBuildings)
        private bool DrawRoleLight(int building, BuildingRole role, Vector2 top, string status, Color colour)
        {
            if (!RoamActive) return false;
            if (Event.current.type == EventType.Repaint)
            {
                EnsureRoamGlow();
                glowRoleSites.Add(building);
                var lot = buildingShapes[building];
                var beat = LightBeat((lot.Min.x + lot.Max.x) * 0.5f, (lot.Min.y + lot.Max.y) * 0.5f, StoreBeat, StoreLag, LightPhase((uint)building));
                var mark = MarkScale;        // 가까이에서는 조금 크게 (SagajeongDispatchCloseView.cs)
                Glow(top, 52f * mark * (1f + 0.12f * beat), Fade(colour, 0.42f * (1f + 0.28f * beat)));
                Glow(top, 26f * mark * (1f + 0.12f * beat), Fade(colour, 0.8f * (1f + 0.22f * beat)));
                GlowCore(top, 11f * mark, Paper);
            }
            // 내 매장(가맹 카페)은 왼쪽 매장 운영 창으로 간다 (r215, SagajeongDispatchStoreDesk.cs). 창고는 전과 같다.
            var storeDesk = StoreDeskReady && StoreRole(role.placeId) != null;
            if (storeDesk && Event.current.type == EventType.Repaint && storeDeskPlace == role.placeId && StoreDeskVisible) StoreDeskMark(top, role.title);
            GlowName("role:" + role.placeId, top, role.title, status, colour);
            var touch = TouchLight(top, 20f);
            if (touch == LightTouch.Click) GoToPlace(role.placeId);
            else if (touch == LightTouch.Open)
            {
                if (storeDesk) GoToPlace(role.placeId);          // 두 번 눌러도 매장 창 그대로 (계획 D6)
                else OpenBusinessIn(RoleBusiness(role), buildingShapes[building].Record.region);
            }
            else if (touch == LightTouch.Menu)
            {
                var place = role.placeId;
                var business = RoleBusiness(role);
                var region = buildingShapes[building].Record.region;
                var items = new List<(string Label, Action Run)>();
                if (storeDesk) items.Add(("매장 운영 창", () => GoToPlace(place)));
                items.Add((storeDesk ? "건물 정보" : "정보 보기", () => SelectPlace(place)));
                items.Add((BusinessDoorLabel(business), () => OpenBusinessIn(business, region)));
                OpenLightMenu(top, role.title, items);
            }
            return true;
        }

        // ── 누르기 한 벌 (빛2) ──
        private bool LightCards(FocusKind kind) => expansionView && mapLevel == MapLevel.Dong && (kind == FocusKind.Courier || RoamActive);

        // 고른 대상의 카드가 서는 자리. 빛으로 보는 동안에는 사업 창 · 건물 카드와 같은 오른쪽 칸이다.
        private Rect FocusPanelNow(FocusKind kind) => LightCards(kind)
            ? new Rect(ExpansionPanel.x, ExpansionPanel.y, ExpansionPanel.width,
                kind == FocusKind.Courier ? 430 : kind == FocusKind.Store ? 266 : kind == FocusKind.Rider ? 262 : 196)
            : FocusPanelFor(kind);

        private Vector2 LightPointer() => mapMenuClick ? menuPoint : tileIndex == null ? Event.current.mousePosition : releasePoint;

        // 빛 하나가 이번 누름을 받았는가. 한 번 · 두 번 · 오른쪽 단추를 가린다. 받은 누름은 다른 빛으로 가지 않는다.
        private LightTouch TouchLight(Vector2 point, float half)
        {
            var rect = Centered(point, half * 2f, half * 2f);
            if (mapMenuClick)
            {
                if (!rect.Contains(menuPoint)) return LightTouch.None;
                mapMenuClick = false;
                return LightTouch.Menu;
            }
            if (!HitMapMarker(rect)) return LightTouch.None;
            return mapDoubleClick ? LightTouch.Open : LightTouch.Click;
        }

        // 한 번 누르기: 오른쪽 칸에 그 대상의 카드를 연다. 업무를 바꾸지 않는다.
        private void FocusLight(FocusKind kind, string id)
        {
            관찰선택해제();
            dongPanelOpen = false;
            zonePanelOpen = false;
            showDetails = true;
            actorFocus = (kind, id);
        }

        private void SelectSite(int building)
        {
            selectedBuilding = building;
            actorFocus = null;
            dongPanelOpen = false;
            zonePanelOpen = false;
        }

        // 두 번 누르기: 그 대상이 있는 동을 고르고, 그 동의 관리 창을 연다 (SagajeongDispatchDongDesk.cs. 사용자 방향 2026-11-14).
        // 기사는 「고용」, 그 밖은 「이 동의 내 곳」을 펴 둔다. 첫 누름이 띄운 카드는 오른쪽 칸에 그대로 남는다.
        // 동 관리 창이 없는 화면에서는 전과 같이 그 사업의 창을 연다.
        private void OpenBusinessIn(int business, string region)
        {
            if (DongDeskReady) OpenDongDesk(region, business == BusinessFood ? DeskFold.Hire : DeskFold.Places);
            else OpenBusinessWindowIn(business, region);
        }

        // 그 대상이 있는 동을 고르고 그 사업의 창을 연다 (r198: 동은 어디를, 사업 버튼은 무슨 일을).
        private void OpenBusinessWindowIn(int business, string region)
        {
            if (!string.IsNullOrEmpty(region) && dongView == DongView.Station && stationAnchors.ContainsKey(region)) SelectStationDong(region, false);   // 사업 창이 말하니 판 위 칸은 펴지 않는다
            selectedBuilding = null;
            SelectExpansion(business);
        }

        private void OpenBusinessAt(int business, double latitude, double longitude) =>
            OpenBusinessIn(business, DongModuleAt(latitude, longitude)?.Entry.regionId);

        // 오른쪽 단추: 차림표. 고른 줄만 실행한다.
        private void OpenLightMenu(Vector2 at, string title, List<(string Label, Action Run)> items)
        {
            lightMenu = items;
            lightMenuTitle = title;
            lightMenuAt = at;
        }

        private void OpenStoreMenu(DispatchStore store, Vector2 position, bool available)
        {
            var id = store.id;
            var latitude = store.latitude;
            var longitude = store.longitude;
            var items = new List<(string Label, Action Run)> { ("정보 보기", () => FocusLight(FocusKind.Store, id)) };
            if (available && !string.IsNullOrEmpty(selectedDriver)) items.Add(("이 매장에 배정", () => ConnectStore(id)));
            items.Add((BusinessDoorLabel(BusinessRestaurant), () => OpenBusinessAt(BusinessRestaurant, latitude, longitude)));
            OpenLightMenu(position, "매장 " + Letter(id), items);
        }

        private Rect LightMenuRect()
        {
            const float width = 176f;
            var height = 30f + lightMenu.Count * 26f + 6f;
            var x = Mathf.Clamp(lightMenuAt.x + 14f, 4f, Mathf.Max(4f, Field.width - width - 4f));
            var y = Mathf.Clamp(lightMenuAt.y - 10f, 4f, Mathf.Max(4f, Field.height - height - 4f));
            return new Rect(x, y, width, height);
        }

        // 차림표가 열려 있으면 그 누름을 먼저 받는다. 밖을 누르면 닫고 그 누름은 지도로 간다. (DrawBoard의 맨 앞)
        private void TouchLightMenu()
        {
            if (lightMenu == null) return;
            if (mapLevel != MapLevel.Dong || mapDragging)
            {
                lightMenu = null;
                return;
            }
            var rect = LightMenuRect();
            if (mapMenuClick)
            {
                if (rect.Contains(menuPoint)) mapMenuClick = false;      // 차림표 위의 오른쪽 누름은 삼킨다
                else lightMenu = null;                                    // 다른 곳을 오른쪽으로 누르면 닫고, 그 빛이 새로 연다
                return;
            }
            if (!mapClick) return;
            if (!rect.Contains(pressPoint) || !rect.Contains(releasePoint))
            {
                lightMenu = null;
                return;
            }
            mapClick = false;
            var row = Mathf.FloorToInt((releasePoint.y - rect.y - 30f) / 26f);
            var items = lightMenu;
            lightMenu = null;
            if (row >= 0 && row < items.Count) items[row].Run?.Invoke();
        }

        private void DrawLightMenu()
        {
            if (lightMenu == null || Event.current.type != EventType.Repaint) return;
            var rect = LightMenuRect();
            Paint(rect, Rim, 9);
            Paint(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Canvas, 8);
            Text(new Rect(rect.x + 12, rect.y + 6, rect.width - 24, 20), lightMenuTitle ?? string.Empty, 12, Gold, true);
            var mouse = Event.current.mousePosition;
            for (var row = 0; row < lightMenu.Count; row++)
            {
                var line = new Rect(rect.x + 5, rect.y + 30f + row * 26f, rect.width - 10, 24);
                if (line.Contains(mouse)) Paint(line, Tile, 6);
                Text(new Rect(line.x + 8, line.y, line.width - 16, line.height), lightMenu[row].Label, 12, Paper);
            }
        }

        // 평면과 디오라마에서 같은 기사 빛을 같은 누름으로 고른다.
        private void TouchCourierLight(DispatchCourier courier, Vector2 position)
        {
            var touch = TouchLight(position, 23f);
            if (touch == LightTouch.Click)
            {
                SelectCourier(courier.id);
                phoneCardView = true;           // 빛을 누르면 오른쪽 상태창(경 수치 포함)이 먼저. 휴대폰은 창의 「휴대폰」 단추로 (2026-11-23 20:33)
            }
            else if (touch == LightTouch.Open) OpenBusinessAt(BusinessFood, courier.latitude, courier.longitude);
            else if (touch == LightTouch.Menu)
            {
                var id = courier.id;
                var latitude = courier.latitude;
                var longitude = courier.longitude;
                OpenLightMenu(position, "기사 " + Letter(id), new List<(string Label, Action Run)>
                {
                    ("정보 보기", () => { FocusLight(FocusKind.Courier, id); phoneCardView = true; }),
                    ("이 기사 고르기", () => SelectCourier(id)),
                    (BusinessDoorLabel(BusinessFood), () => OpenBusinessAt(BusinessFood, latitude, longitude)),
                });
            }
        }

        // 고용한 기사의 빛과 제휴 음식점의 빛. 매장·배차 기사가 받지 않은 누름만 받는다. (DrawBoard의 끝, 건물 고르기보다 앞)
        private void TouchLooseLights()
        {
            if (!RoamActive || (!mapClick && !mapMenuClick)) return;
            foreach (var roamer in roamers)
            {
                var point = PlotRoam(DongModuleById(roamer.Region), roamer.X, roamer.Z);
                if (point == DongHiddenPoint) continue;
                var touch = TouchLight(point, 18f);
                if (touch == LightTouch.None) continue;
                var key = roamer.Region + "#" + roamer.Index;
                var region = roamer.Region;
                if (touch == LightTouch.Click) FocusLight(FocusKind.Rider, key);
                else if (touch == LightTouch.Open) OpenBusinessIn(BusinessFood, region);
                else
                    OpenLightMenu(point, "고용한 기사", new List<(string Label, Action Run)>
                    {
                        ("정보 보기", () => FocusLight(FocusKind.Rider, key)),
                        (BusinessDoorLabel(BusinessFood), () => OpenBusinessIn(BusinessFood, region)),
                    });
                return;
            }
            foreach (var building in managedPartnerSites)
            {
                if (building < 0 || building >= buildingShapes.Length) continue;
                var touch = TouchLight(RoofCentre(buildingShapes[building]), 16f);
                if (touch == LightTouch.None) continue;
                var site = building;
                var region = buildingShapes[building].Record.region;
                if (touch == LightTouch.Click) SelectSite(site);
                else if (touch == LightTouch.Open) OpenBusinessIn(BusinessRestaurant, region);
                else
                    OpenLightMenu(RoofCentre(buildingShapes[building]), "제휴 음식점", new List<(string Label, Action Run)>
                    {
                        ("건물 정보 보기", () => SelectSite(site)),
                        (BusinessDoorLabel(BusinessRestaurant), () => OpenBusinessIn(BusinessRestaurant, region)),
                    });
                return;
            }
        }

        // 빛을 눌렀을 때의 카드: 배송지와 고용한 기사. 매장 · 배차 기사의 카드(DrawFocusPanel)와 같은 자리, 같은 모양이다.
        private void DrawLightCard(FocusKind kind, string id)
        {
            var panel = FocusPanelNow(kind);
            var x = panel.x + 13;
            var y = panel.y;
            string head, title, status, detail, note;
            if (kind == FocusKind.Drop)
            {
                int.TryParse(id, out var index);
                head = "음식 배달 · 배송지";
                title = "배송지 " + (index + 1);
                status = "전달 자리";
                detail = "배달권의 주문이 닿는 곳입니다.";
                note = "어느 주문의 배송지인지는 아직 이 카드에 잇지 않았습니다.";
            }
            else
            {
                var roamer = roamers.FirstOrDefault(item => item.Region + "#" + item.Index == id);
                if (roamer == null)
                {
                    actorFocus = null;
                    return;
                }
                head = "기사 운영 · 고용한 기사";
                title = (DongModuleById(roamer.Region)?.Entry.name ?? "동") + " · 기사 " + (roamer.Index + 1);
                status = roamer.Wait > 0f ? "잠깐 멈춤" : roamer.Phase == 0 ? "픽업지로 가는 중" : "전달지로 가는 중";
                detail = "큰길과 골목을 따라 다닙니다.";
                note = "움직임은 표현입니다. 주문 한 건씩의 규칙은 아직 없어서 돈·주문 수와 이어지지 않습니다.";
            }
            Paint(panel, Rim, 10);
            Paint(new Rect(panel.x + 1, y + 1, panel.width - 2, panel.height - 2), Canvas, 9);
            Text(new Rect(x, y + 11, 190, 19), head, 11, Mint, true);
            Text(new Rect(x, y + 34, panel.width - 80, 29), title, 18, Paper, true);
            var close = new Rect(panel.xMax - 59, y + 12, 47, 27);
            Paint(close, Tile, 6);
            Text(close, "닫기", 12, Paper, align: TextAnchor.MiddleCenter);
            if (Hit(close))
            {
                actorFocus = null;
                return;
            }
            Text(new Rect(x, y + 72, panel.width - 26, 23), status, 14, Gold, true);
            Text(new Rect(x, y + 98, panel.width - 26, 23), detail, 13, Paper);
            if (kind == FocusKind.Rider)
            {
                // 이 빛 한 사람의 경 기록은 아직 잇지 않았다. 위 띠에서 옮겨 온 「홍달 기사 전체 · 오늘」 값을 보인다.
                DrawGyeongFigures(x, y + 124, panel.width - 26f, null);
                Text(new Rect(x, y + 180, panel.width - 26, 74),
                    note + " 경 수치는 배차 기사 전체의 오늘 합입니다. 이 빛 한 사람의 경 기록은 아직 잇지 않았습니다.", 11, Soft, wrap: true);
                return;
            }
            Text(new Rect(x, y + 126, panel.width - 26, 58), note, 11, Soft, wrap: true);
        }

        private bool RoamInsideBuilding(float x, float z)
        {
            foreach (var shape in buildingShapes)
            {
                if (x < shape.Min.x || x > shape.Max.x || z < shape.Min.y || z > shape.Max.y) continue;
                var inside = false;
                var ring = shape.Ring;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                    if ((ring[i].y > z) != (ring[j].y > z) && x < (ring[j].x - ring[i].x) * (z - ring[i].y) / (ring[j].y - ring[i].y) + ring[i].x)
                        inside = !inside;
                if (inside) return true;
            }
            return false;
        }

        // 방향 촬영: 빛이 나타나고, 표현의 시간으로 90초를 보낸 뒤의 모습, 빛을 더 세웠을 때의 모습, 가까이 본 모습.
        // 게임 시계와 원장은 그대로여야 한다. 더 세운 빛은 촬영에서만 쓰고 고용이 아니다(촬영 뒤에 걷는다).
        private IEnumerator ViewProofRoam(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            SetDioramaPerspective(false);
            SetDioramaLens(디오라마시점.DefaultYawDegrees, 디오라마시점.DefaultPitchDegrees);
            SetDongView(DongView.Station);          // 처음 보기(역 중심)에서 찍는다
            yield return null;
            yield return null;
            SyncRoamers();
            receipt.roamRiders = roamers.Count;
            receipt.roamGraphNodes = routeGraph?.Nodes ?? 0;
            StepRoamLight();
            ShowRoamLight();
            yield return null;
            yield return SaveFrame(directory, "32-roam-start.png");
            var inside = 0;
            for (var step = 0; step < 360; step++)
            {
                StepRoamers(0.25f);
                StepRoamLight();
                foreach (var roamer in roamers)
                    if (RoamInsideBuilding(roamer.X, roamer.Z)) inside++;
            }
            ShowRoamLight();
            receipt.roamMetres = roamMetres;
            receipt.roamJobs = roamJobs;
            receipt.roamInsideBuildings = inside;
            receipt.roamLitCells = roamLight?.Awake() ?? 0;
            receipt.roamLightKinds = 0;
            for (var layer = 0; roamLight != null && layer < 4; layer++)
                if (roamLight.Lit(layer) > 0) receipt.roamLightKinds++;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "33-roam-later.png");

            roamProofExtra = 5;
            SyncRoamers();
            receipt.roamRidersMore = roamers.Count;
            for (var step = 0; step < 240; step++)
            {
                StepRoamers(0.25f);
                StepRoamLight();
            }
            ShowRoamLight();
            receipt.roamLitCellsMore = roamLight?.Awake() ?? 0;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "34-roam-more.png");

            // 가까이: 화면 가운데에 가장 가까운 빛 하나를 두 배 반으로 당겨 본다.
            Roamer nearest = null;
            var nearestGap = float.MaxValue;
            foreach (var roamer in roamers)
            {
                var point = PlotRoam(DongModuleById(roamer.Region), roamer.X, roamer.Z);
                if (point == DongHiddenPoint) continue;
                var gap = (point - Field.size * 0.5f).sqrMagnitude;
                if (gap >= nearestGap) continue;
                nearestGap = gap;
                nearest = roamer;
            }
            if (nearest != null)
            {
                ViewProofCentre(buildingIndex.origin.latitude + nearest.Z / buildingIndex.metresPerDegreeLatitude,
                    buildingIndex.origin.longitude + nearest.X / buildingIndex.metresPerDegreeLongitude, mapZoom * 2.5f);
                yield return null;
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "35-roam-close.png");
            }

            // 빛을 한 번 고쳐 올리는 데 드는 시간을 잰다 (빛이 가장 많을 때. 표현의 시간으로 0.1초씩 스무 번).
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var turn = 0; turn < 20; turn++)
            {
                roamLightDue = 0.1f;
                StepRoamLight();
                ShowRoamLight();
            }
            receipt.roamLightMs = (float)(watch.Elapsed.TotalMilliseconds / 20.0);
            roamProofExtra = 0;
            SyncRoamers();
            receipt.roamRidersAfter = roamers.Count;

            // 이름표 대신 빛: 처음 보기로 돌아가, 매장 하나에 올렸을 때 이름이 뜨는 모습을 찍는다.
            SetDongView(DongView.Joined);
            SetDongView(DongView.Station);
            yield return null;
            yield return null;
            var firstStore = state?.stores?.FirstOrDefault();
            glowProofName = firstStore != null ? "store:" + firstStore.id : null;
            yield return null;
            yield return null;
            receipt.lightPlaces = glowPlacesShown;
            yield return SaveFrame(directory, "36-light-name.png");
            glowProofName = null;

            // 누르기 한 벌(빛2)과 사업 창의 동(사2): 고용한 기사의 카드, 매장의 차림표, 기사 운영 창을 찍는다.
            // 실제 마우스 입력이 아니라 같은 함수를 불러서 찍는다.
            var firstRoamer = roamers.FirstOrDefault();
            if (firstRoamer != null)
            {
                FocusLight(FocusKind.Rider, firstRoamer.Region + "#" + firstRoamer.Index);
                yield return null;
                yield return null;
                receipt.lightCard = actorFocus != null && Mathf.Approximately(FocusPanelNow(FocusKind.Rider).x, ExpansionPanel.x);
                yield return SaveFrame(directory, "37-light-card.png");
                actorFocus = null;
            }
            // 배차 기사 빛을 누른 모습(2026-11-23 20:33): 오른쪽 상태창에 그 기사의 경 유지 · 경 시간당 순소득.
            var firstCourier = state?.couriers?.FirstOrDefault();
            if (firstCourier != null)
            {
                var keepCard = phoneCardView;
                FocusLight(FocusKind.Courier, firstCourier.id);
                phoneCardView = true;
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "37b-courier-card.png");
                // 게임 요소를 켠 사람의 화면: 등급 이름(신사 …) · 위 띠 경 수치 (2026-11-23 20:47)
                var keepData = ProofDataPush(DataBits() | 4096 | 8192, facilityGroupMask);
                yield return null;
                yield return null;
                yield return SaveFrame(directory, "37c-courier-card-game.png");
                ProofDataPop(keepData);
                actorFocus = null;
                phoneCardView = keepCard;
            }
            if (firstStore != null)
            {
                OpenStoreMenu(firstStore, PlotScene(firstStore.latitude, firstStore.longitude), false);
                yield return null;
                yield return null;
                receipt.lightMenuRows = lightMenu?.Count ?? 0;
                yield return SaveFrame(directory, "38-light-menu.png");
                lightMenu = null;
                OpenBusinessWindowIn(BusinessFood, DongModuleAt(firstStore.latitude, firstStore.longitude)?.Entry.regionId);
                yield return null;
                yield return null;
                receipt.businessDong = BusinessDong()?.Entry.name ?? string.Empty;
                yield return SaveFrame(directory, "39-business-dong.png");
                expansionPanelOpen = false;
            }

            // 빛의 숨: 게임 시계는 멈춘 채로, 실제 시간 0.7초를 사이에 두고 두 장을 찍는다. 빛의 밝기와 크기가 달라야 한다.
            yield return null;
            var beatBefore = lightBeat;
            yield return SaveFrame(directory, "40-light-beat-a.png");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return null;
            receipt.lightBeatStep = Mathf.Repeat(lightBeat - beatBefore, BeatWrap);
            yield return SaveFrame(directory, "41-light-beat-b.png");
            receipt.roamUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }
    }
}
