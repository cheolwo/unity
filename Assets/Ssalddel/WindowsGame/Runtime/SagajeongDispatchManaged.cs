using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // 회색 동네와 관리 건물 — 화면 층 (기획 r143 첫 걸음, 2026-11-14).
    // 사용자 방향: 동 지도에는 건물이 나열돼 있기만 해서 어디에 집중할지 모르겠다.
    // 내가 관리하는 건물과 그렇지 않은 건물의 명암을 분명히 가른다.
    //  - 행정동 모듈의 바탕(땅·도로·건물)은 색을 빼고 대비를 낮춘 회색으로 그린다(셰이더의 _Grey).
    //    밝기 순서는 남아서 도로와 건물은 구별된다. 동의 밝기(영역 단계·운영의 깊이)는 그대로 곱해진다.
    //  - 내가 관리하는 건물은 그 위에 밝은 민트 건물로 덧그린다. 창에 불이 더 많이 켜진다.
    //      · 역할표의 내 건물(매장 A·B, 카페, 창고). 사업 층을 끄면 그 건물도 꺼진다.
    //      · 제휴 음식점: 동별 수요의 제휴 수만큼. 자리가 있는 매장을 먼저 세고 나머지는 가상 자리다(관리건물Policy).
    //        제휴를 맺을 때마다 건물이 하나씩 켜진다.
    //  - 내 건물 곁의 동네는 회색으로 빼지 않고 제 색으로 남는다(관리 건물의 빛). 제휴가 늘수록 동 안에 밝은 곳이 늘어난다.
    //    밝힌 건물의 옥상에는 작은 민트 점을 찍어 멀리서도 찾을 수 있게 한다.
    //  - 건물을 고르거나 거래했다고 켜지지 않는다. 있는 관리 관계(역할표·제휴 수)만 읽고 새 상태를 만들지 않는다.
    // 표현만 한다. 시계·주문·돈·재고를 바꾸지 않는다(동별 수요 사본에서 제휴 수만 읽는다).
    // 검증 촬영(--sagajeong-…)에는 행정동 모듈이 없어 해당하지 않는다. 자료는 내부 검토용이고 가상 자리는 실제 점포와 무관하다.
    public sealed partial class SagajeongDispatchView
    {
        private static readonly int DioramaGreyId = Shader.PropertyToID("_Grey");
        private static readonly int DioramaLitId = Shader.PropertyToID("_Lit");
        private static readonly int DioramaLightMapId = Shader.PropertyToID("_LightMap");
        private static readonly int DioramaLightRectId = Shader.PropertyToID("_LightRect");
        private const int ManagedLightTexels = 512;       // 빛 그림의 한 변(칸)
        private const float ManagedGlowCore = 16f;        // 건물 둘레에서 이만큼은 빛이 가득하다(m, 표현값)
        private const float ManagedGlowReach = 80f;       // 그 바깥으로 빛이 옅어지며 닿는 거리(m, 표현값)
        private const float ManagedOutset = 0.3f;         // 원래 건물을 덮도록 외곽을 넓히는 거리(m, 표현값)
        private const float ManagedLift = 0.55f;          // 지붕을 올리는 높이(m, 표현값). 다시 구운 동 메시의 지붕 테두리(0.45 m)를 덮는다(품5)
        private const float ManagedLitWindows = 0.55f;    // 불 켜진 창의 비율(표현값). 바탕 건물은 0.2다
        private static readonly Vector2 ManagedToLight = new(-0.931f, -0.364f);     // 굽기 도구의 빛 방향과 같다
        // 민트(0x6EE7CC)에서 만든다. 다른 파일의 정적 색(Mint)에 기대지 않는다: 부분 클래스 사이의 초기화 순서는 정해져 있지 않다.
        private static readonly Color ManagedRoof = Color.Lerp(Ink(0x6EE7CC), Color.white, 0.2f);
        private static readonly Color ManagedWall = Color.Lerp(Ink(0x6EE7CC), Ink(0x0F3A33), 0.3f);

        private sealed class ManagedLayer
        {
            public GameObject Part;
            public Mesh Mesh;
            public string Signature = string.Empty;
        }

        private bool managedLight = true;                 // 회색 동네와 관리 건물 밝히기를 쓰는가
        private Material managedMaterial;
        private Texture2D managedLightMap;
        private byte[] managedLightBytes;
        private readonly Dictionary<string, ManagedLayer> managedLayers = new(StringComparer.Ordinal);        // 지역 ID → 그 동의 밝힌 건물
        private readonly Dictionary<string, List<int>> managedCandidates = new(StringComparer.Ordinal);     // 지역 ID → 가상 자리 후보
        private readonly HashSet<int> managedPartnerSites = new();      // 제휴 음식점의 가상 자리로 밝힌 건물
        private readonly HashSet<int> managedRoleSites = new();         // 역할표의 내 건물 가운데 밝힌 것
        private bool managedCandidatesReady;
        private int managedStamp = int.MinValue;

        private bool ManagedLightActive => managedLight && DongModulesLoaded;

        // 제휴 음식점의 가상 자리로 밝혀 둔 건물인가. 건물 카드가 그렇다고 밝힌다.
        private bool IsPartnerSite(int building) => ManagedLightActive && managedPartnerSites.Contains(building);

        private bool ManagedLayerOn(int business) => business >= layerOn.Length || layerOn[business];

        // LateUpdate가 부른다(2.5D가 보이는 동안). 관리 관계가 달라졌을 때만 메시를 다시 만든다.
        private void SyncManagedBuildings()
        {
            if (!DongModulesLoaded || dioramaMaterial == null) return;
            var on = managedLight;
            var stamp = on ? 1 : 0;
            if (on)
            {
                LoadRoleBook();          // 건물 색인과 역할표. 이미 읽었으면 아무 일도 하지 않는다
                stamp = stamp * 31 + buildingShapes.Length;
                stamp = stamp * 31 + roleByBuilding.Count;
                for (var i = 0; i < layerOn.Length; i++) stamp = stamp * 31 + (layerOn[i] ? 1 : 0);
                if (dongDemand != null)
                    foreach (var dong in dongDemand.Dongs) stamp = stamp * 31 + (dong.Open ? dong.Partners + 1 : 0);
            }
            if (stamp == managedStamp) return;
            managedStamp = stamp;
            dioramaMaterial.SetFloat(DioramaGreyId, on ? 1f : 0f);

            managedPartnerSites.Clear();
            managedRoleSites.Clear();
            var wanted = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            if (on && buildingShapes.Length > 0)
            {
                EnsureManagedCandidates();
                var placedStores = new Dictionary<string, int>(StringComparer.Ordinal);
                var taken = new HashSet<int>(roleByBuilding.Keys);
                foreach (var pair in roleByBuilding)
                {
                    var region = buildingShapes[pair.Key].Record.region;
                    if (string.IsNullOrEmpty(region)) continue;
                    var business = RoleBusiness(pair.Value);
                    if (business == BusinessRestaurant)
                    {
                        placedStores.TryGetValue(region, out var stores);
                        placedStores[region] = stores + 1;
                    }
                    if (pair.Value.owner != "mine" || !ManagedLayerOn(business)) continue;
                    managedRoleSites.Add(pair.Key);
                    AddManaged(wanted, region, pair.Key);
                }
                if (dongDemand != null && ManagedLayerOn(BusinessRestaurant))
                {
                    foreach (var dong in dongDemand.Dongs)
                    {
                        if (!dong.Open || dong.Partners <= 0) continue;
                        var region = HjdRegionPrefix + dong.Code;
                        managedCandidates.TryGetValue(region, out var candidates);
                        placedStores.TryGetValue(region, out var placed);
                        foreach (var index in 관리건물Policy.PartnerSites(candidates, dong.Partners, placed, taken))
                        {
                            managedPartnerSites.Add(index);
                            AddManaged(wanted, region, index);
                        }
                    }
                }
            }
            foreach (var module in dongModules)
            {
                if (!module.Loaded || module.Part == null) continue;
                wanted.TryGetValue(module.Entry.regionId, out var buildings);
                ApplyManagedLayer(module, buildings);
            }
            if (on) UpdateManagedLightMap(wanted);
            Debug.Log($"Hongdal managed buildings: {(on ? "on" : "off")}, mine {managedRoleSites.Count}, partner sites {managedPartnerSites.Count}");
        }

        private static void AddManaged(Dictionary<string, List<int>> wanted, string region, int building)
        {
            if (!wanted.TryGetValue(region, out var list)) wanted[region] = list = new List<int>();
            list.Add(building);
        }

        // 건물 색인을 동별로 나눠 가상 자리 후보를 정해 둔다. 색인을 읽은 뒤 한 번만 한다.
        private void EnsureManagedCandidates()
        {
            if (managedCandidatesReady) return;
            managedCandidatesReady = true;
            var byRegion = new Dictionary<string, List<(int Index, string Id, string Kind, double Area, bool Fill)>>(StringComparer.Ordinal);
            for (var index = 0; index < buildingShapes.Length; index++)
            {
                var record = buildingShapes[index].Record;
                if (string.IsNullOrEmpty(record.region)) continue;
                if (!byRegion.TryGetValue(record.region, out var list))
                    byRegion[record.region] = list = new List<(int Index, string Id, string Kind, double Area, bool Fill)>();
                list.Add((index, record.id, record.kind, record.area, record.fill));
            }
            foreach (var pair in byRegion) managedCandidates[pair.Key] = 관리건물Policy.Candidates(pair.Value);
        }

        // 한 동의 밝힌 건물 묶음. 그 동의 메시 묶음 아래에 두어 띄워 보기·숨기기를 함께 따른다.
        private void ApplyManagedLayer(DongModuleState module, List<int> buildings)
        {
            managedLayers.TryGetValue(module.Entry.regionId, out var layer);
            if (buildings == null || buildings.Count == 0)
            {
                if (layer == null) return;
                if (layer.Part != null) layer.Part.SetActive(false);
                return;
            }
            buildings.Sort();
            var signature = string.Join(",", buildings);
            if (layer == null)
            {
                if (managedMaterial == null)
                {
                    managedMaterial = new Material(dioramaMaterial.shader) { name = "Hongdal managed buildings" };
                    managedMaterial.SetFloat(DongDimId, 1f);
                    managedMaterial.SetFloat(DioramaGreyId, 0f);
                    managedMaterial.SetFloat(DioramaLitId, ManagedLitWindows);
                }
                var mesh = new Mesh { name = "Managed " + module.Entry.code, indexFormat = IndexFormat.UInt32 };
                var renderer = AddDongPart(module.Part, "Managed buildings", mesh, managedMaterial);
                layer = new ManagedLayer { Part = renderer.gameObject, Mesh = mesh };
                managedLayers[module.Entry.regionId] = layer;
            }
            if (layer.Signature != signature)
            {
                BuildManagedMesh(layer.Mesh, buildings);
                layer.Signature = signature;
            }
            if (!layer.Part.activeSelf) layer.Part.SetActive(true);
        }

        // 밝힌 건물의 메시. 색인의 외곽(자료 원점 기준 m)과 높이로 벽과 지붕을 세운다. 원래 건물보다 조금 크게 만들어 그 위를 덮는다.
        private void BuildManagedMesh(Mesh mesh, List<int> buildings)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var triangles = new List<int>();
            Color32 roofColor = ManagedRoof;
            foreach (var building in buildings)
            {
                var shape = buildingShapes[building];
                var ring = new List<(double X, double Y)>(shape.Ring.Length);
                foreach (var point in shape.Ring) ring.Add((point.x, point.y));
                // 처음 점으로 되돌아와 닫은 외곽이면 마지막 점을 뺀다.
                if (ring.Count > 3 && Math.Abs(ring[0].X - ring[ring.Count - 1].X) < 1e-3 && Math.Abs(ring[0].Y - ring[ring.Count - 1].Y) < 1e-3)
                    ring.RemoveAt(ring.Count - 1);
                if (ring.Count < 3) continue;
                var outer = 관리건물Policy.Outset(ring, ManagedOutset);
                var counterClockwise = 관리건물Policy.SignedArea(outer) >= 0;
                var top = shape.Record.height + ManagedLift;
                var seed = building % 97 + 0.37f;

                var first = vertices.Count;
                foreach (var (x, y) in outer)
                {
                    vertices.Add(new Vector3((float)x, top, (float)y));
                    colors.Add(roofColor);
                    uv.Add(Vector2.zero);
                    uv2.Add(Vector2.zero);
                }
                foreach (var corner in 관리건물Policy.Triangulate(outer)) triangles.Add(first + corner);

                var along = 0f;
                for (var i = 0; i < outer.Count; i++)
                {
                    var start = outer[i];
                    var end = outer[(i + 1) % outer.Count];
                    var length = (float)Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y));
                    if (length < 0.01f) continue;
                    var (nx, ny) = 관리건물Policy.Outward(start, end, counterClockwise);
                    var lit = 0.6f + 0.4f * Mathf.Max(0f, (float)nx * ManagedToLight.x + (float)ny * ManagedToLight.y);
                    Color32 wallColor = new Color(ManagedWall.r * lit, ManagedWall.g * lit, ManagedWall.b * lit, 1f);
                    var at = vertices.Count;
                    vertices.Add(new Vector3((float)start.X, 0f, (float)start.Y));
                    vertices.Add(new Vector3((float)end.X, 0f, (float)end.Y));
                    vertices.Add(new Vector3((float)end.X, top, (float)end.Y));
                    vertices.Add(new Vector3((float)start.X, top, (float)start.Y));
                    uv.Add(new Vector2(along, 0f));
                    uv.Add(new Vector2(along + length, 0f));
                    uv.Add(new Vector2(along + length, top));
                    uv.Add(new Vector2(along, top));
                    for (var corner = 0; corner < 4; corner++)
                    {
                        colors.Add(wallColor);
                        uv2.Add(new Vector2(1f, seed));
                    }
                    triangles.Add(at);
                    triangles.Add(at + 1);
                    triangles.Add(at + 2);
                    triangles.Add(at);
                    triangles.Add(at + 2);
                    triangles.Add(at + 3);
                    along += length;
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, uv2);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // ── 관리 건물의 빛 ──
        // 내 건물 곁의 동네는 회색으로 빼지 않고 제 색으로 둔다(조금 더 밝게). 제휴가 늘수록 동 안에 밝은 곳이 늘어난다.
        // 빛이 닿는 거리는 표현값이다. 배달 가능 범위나 상권을 뜻하지 않는다.
        // 동 묶음 전체를 덮는 작은 그림(한 칸에 몇 m) 하나에 밝기를 적어 셰이더가 읽는다. 건물 수와 무관하게 값이 싸다.
        private void UpdateManagedLightMap(Dictionary<string, List<int>> wanted)
        {
            if (dongManifest == null) return;
            if (managedLightMap == null)
            {
                managedLightMap = new Texture2D(ManagedLightTexels, ManagedLightTexels, TextureFormat.R8, false, true)
                {
                    name = "Hongdal managed light", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
                managedLightBytes = new byte[ManagedLightTexels * ManagedLightTexels];
            }
            Array.Clear(managedLightBytes, 0, managedLightBytes.Length);
            var (west, south) = DongLocal(dioramaSouth, dioramaWest);
            var (east, north) = DongLocal(dioramaNorth, dioramaEast);
            var pad = ManagedGlowReach + ManagedGlowCore + 40.0;
            var x0 = west - pad;
            var z0 = south - pad;
            var width = Math.Max(1.0, east - west + 2 * pad);
            var depth = Math.Max(1.0, north - south + 2 * pad);
            var cellX = width / ManagedLightTexels;
            var cellZ = depth / ManagedLightTexels;
            foreach (var list in wanted.Values)
            {
                foreach (var building in list)
                {
                    var shape = buildingShapes[building];
                    var centre = (shape.Min + shape.Max) * 0.5f;
                    var full = (shape.Max - shape.Min).magnitude * 0.5f + ManagedGlowCore;     // 이 안쪽은 빛이 가득하다
                    var reach = full + ManagedGlowReach;
                    var ix0 = Math.Max(0, (int)Math.Floor((centre.x - reach - x0) / cellX));
                    var ix1 = Math.Min(ManagedLightTexels - 1, (int)Math.Ceiling((centre.x + reach - x0) / cellX));
                    var iz0 = Math.Max(0, (int)Math.Floor((centre.y - reach - z0) / cellZ));
                    var iz1 = Math.Min(ManagedLightTexels - 1, (int)Math.Ceiling((centre.y + reach - z0) / cellZ));
                    for (var iz = iz0; iz <= iz1; iz++)
                    {
                        var dz = z0 + (iz + 0.5) * cellZ - centre.y;
                        for (var ix = ix0; ix <= ix1; ix++)
                        {
                            var dx = x0 + (ix + 0.5) * cellX - centre.x;
                            var distance = Math.Sqrt(dx * dx + dz * dz);
                            var near = 1.0 - Math.Max(0, Math.Min(1, (distance - full) / ManagedGlowReach));
                            var value = (byte)Math.Round(near * near * (3 - 2 * near) * 255);
                            var at = iz * ManagedLightTexels + ix;
                            if (value > managedLightBytes[at]) managedLightBytes[at] = value;
                        }
                    }
                }
            }
            managedLightMap.LoadRawTextureData(managedLightBytes);
            managedLightMap.Apply(false, false);
            dioramaMaterial.SetTexture(DioramaLightMapId, managedLightMap);
            dioramaMaterial.SetVector(DioramaLightRectId, new Vector4((float)x0, (float)z0, (float)(1 / width), (float)(1 / depth)));
        }

        // 밝힌 건물의 옥상에 작은 민트 점을 찍는다(운영 표식). 멀리서 봐도 내 건물이 어디 있는지 보인다.
        // 매장은 기존 표식이 있으므로 찍지 않는다. 누름은 받지 않는다. GUI.BeginGroup(Field) 안의 좌표.
        private void DrawManagedMarks()
        {
            if (!ManagedLightActive || mapLevel != MapLevel.Dong || !DioramaVisible || Event.current.type != EventType.Repaint) return;
            foreach (var building in managedPartnerSites)
                if (!DrawStoreGlow(building)) DrawManagedMark(building);      // 빛으로 보는 동안에는 주황빛 한 점 (SagajeongDispatchRoam.cs)
            foreach (var building in managedRoleSites)
                if (!glowRoleSites.Contains(building) && roleByBuilding.TryGetValue(building, out var role) &&
                    RoleBusiness(role) != BusinessRestaurant) DrawManagedMark(building);      // 빛으로 그린 건물에는 점을 겹쳐 찍지 않는다
        }

        private void DrawManagedMark(int building)
        {
            var top = RoofCentre(buildingShapes[building]);
            if (top.x < -8 || top.y < -8 || top.x > Field.width + 8 || top.y > Field.height + 8) return;
            Paint(new Rect(top.x - 5.5f, top.y - 5.5f, 11, 11), Fade(Canvas, 0.92f), 5.5f);
            Paint(new Rect(top.x - 3.5f, top.y - 3.5f, 7, 7), Mint, 3.5f);
        }

        // 2.5D 장면을 내릴 때 함께 내린다. 묶음의 게임 오브젝트는 장면 뿌리와 함께 없어진다.
        private void ReleaseManaged()
        {
            foreach (var layer in managedLayers.Values)
                if (layer.Mesh != null) Destroy(layer.Mesh);
            managedLayers.Clear();
            if (managedMaterial != null) Destroy(managedMaterial);
            managedMaterial = null;
            if (managedLightMap != null) Destroy(managedLightMap);
            managedLightMap = null;
            managedPartnerSites.Clear();
            managedRoleSites.Clear();
            managedStamp = int.MinValue;
        }
    }
}
