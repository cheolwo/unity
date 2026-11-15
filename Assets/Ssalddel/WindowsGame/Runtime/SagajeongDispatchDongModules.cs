using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Ssalddel.WindowsGame.Kernel;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // r54 첫 조각 (2026-11-13): 동네 2.5D 지도를 행정동 모듈 묶음으로 바꾼다.
    // Tools~/make_dong_modules.py가 기존 r2 행정동 모듈(2023 역사 경계 후보)을 동마다 메시 하나로 구워 두었다.
    // 모든 동이 같은 원점(사가정 ENU)을 쓰므로 한 장면에 나란히 놓으면 경계가 맞물린다.
    // 순회 정거장이 행정동에 닿으면 그 동의 경계가 화면에 다 들어오게 지도를 옮기고, 이웃 동은 어둡게 둔다.
    // 표현 자원만 읽고 내린다. 게임 시계·기사·주문·돈·재고는 읽지도 바꾸지도 않는다(r162).
    // 자료는 배포 승인 전이다(내부 검토용). 통행·배달 가능으로 올리지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private const string DongModuleManifestResource = "hongdal-dong-modules";
        private const string DongModuleSchema = "hongdal-dong-modules.v1";
        private const string DongModuleLoaded = "읽음";
        private const string DongModuleWaiting = "읽기 전";
        // 순회 결정 카드가 오른쪽을 덮을 때 동을 맞출 빈 곳: 왼쪽 패널과 결정 카드 사이, 신호 띠 아래, 사업 띠 위.
        private static readonly Rect DongFocusArea = new(222, 140, 754, 480);
        private static readonly int DongDimId = Shader.PropertyToID("_Dim");
        // 동 보기 단추 묶음: 왼쪽 사업 층 아래, 지도 효과 위.
        private static readonly Rect DongViewPanel = new(22, 520, 188, 68);
        private static readonly Color DongBackdrop = Ink(0x0E161D);           // 모듈 2.5D 바탕: 동 판이 떠 보이게 어둡게
        private static readonly Vector2 DongHiddenPoint = new(-100000f, -100000f);   // 숨긴 동의 표식은 화면 밖으로 (Line은 그리지 않는다)
        private const float DongSkirtDepth = 16f;                              // 동 판 옆면 깊이(m, 표현값)
        private const double DongGridCell = 10;                                // 표식이 든 동을 찾는 격자 칸(m)

        [Serializable]
        private sealed class DongModuleManifest
        {
            public string schema;
            public string scopeRevision;
            public string scopeStableId;
            public string scopeContentHash;
            public string sourceVintage;
            public string readiness;
            public bool distributionApproved;
            public bool traversalReady;
            public bool gameplayReady;
            public string label;
            public string attribution;
            public int scopeModuleCount;
            public double originLatitude;
            public double originLongitude;
            public double metresPerDegreeLatitude;
            public double metresPerDegreeLongitude;
            public double west;
            public double south;
            public double east;
            public double north;
            public string buildingIndexResource;
            public string buildingIndexSha256;
            public string presentationValues;
            public DongModuleEntry[] modules;
            public DongRoleLink[] roleMap;
        }

        [Serializable]
        private sealed class DongModuleEntry
        {
            public string regionId;
            public string code;
            public string name;
            public string district;
            public string resource;
            public long bytes;
            public string sha256;
            public string moduleSha256;
            public string moduleContentHash;
            public int buildings;
            public int skippedBuildings;
            public int roads;
            public int heightFromSource;
            public int heightFromFloors;
            public int heightSymbolic;
            public double west;
            public double south;
            public double east;
            public double north;
            public double centerLatitude;
            public double centerLongitude;
            public double[] boundary;      // 위도, 경도, 위도, 경도 … (0.5 m로 줄인 경계)
            public double separatedEast;   // 띄워 보기에서 옮길 자리(m). 굽기 도구가 서로 24 m 이상 떨어지게 정했다
            public double separatedNorth;
            public int vertices;
            public int triangles;
        }

        [Serializable]
        private sealed class DongRoleLink
        {
            public string from;
            public string to;
            public string method;
            public float distance;
        }

        private sealed class DongModuleState
        {
            public DongModuleEntry Entry;
            public string Status;
            public readonly List<Renderer> Renderers = new();
            public double[] Latitudes;
            public double[] Longitudes;
            public GameObject Part;            // 이 동의 메시 묶음 (띄워 보기에서 옮기고, 숨기면 끈다)
            public double CentreEast;          // 넓이 중심 (공통 원점 기준 m)
            public double CentreNorth;
            public bool Loaded => Status == DongModuleLoaded;
        }

        // 동 보기 방식 (2026-11-13 사용자 요청): 붙여 보기, 띄워 보기, 이 동만. 동 칩으로 보이는 동을 고른다.
        private enum DongView { Joined, Separated, Solo, Station }     // Station: 역 중심 보기 (SagajeongDispatchStationView.cs)

        private DongModuleManifest dongManifest;
        private bool dongManifestTried;
        private readonly List<DongModuleState> dongModules = new();
        private string focusDongRegion;
        private MaterialPropertyBlock dongDimBlock;
        private readonly Dictionary<string, DongModuleState> dongModuleById = new();
        private DongView dongView = DongView.Separated;      // 처음에는 살짝 띄워서 동마다 구분되게
        private readonly HashSet<string> dongHidden = new();
        private string soloRegion;
        private float dongSpread = -1f;                      // 지금 띄운 정도 0(붙임)~1(띄움). 목표로 천천히 움직인다
        private bool dongHasSeparatedOffsets;                // 목록에 띄워 보기 자리가 있는가 (없으면 가운데에서 밀어내기)
        private 행정동격자 dongGrid;
        private double dongUnionEast;
        private double dongUnionNorth;

        private float DongSpreadTarget => dongView == DongView.Separated ? 1f : 0f;

        // 행정동 모듈을 쓰는가. 목록을 읽었으면 동네 2.5D 지도와 건물 색인이 모듈 쪽으로 바뀐다(사용자 선택: 동네 지도를 모듈로 교체).
        private bool DongModulesActive => EnsureDongManifest() != null;

        // 메시까지 하나라도 읽었는가.
        private bool DongModulesLoaded => dioramaRoot != null && dongManifest != null && dongModules.Any(module => module.Loaded);

        private int DongModulesLoadedCount => dongModules.Count(module => module.Loaded);

        private DongModuleManifest EnsureDongManifest()
        {
            if (dongManifestTried) return dongManifest;
            dongManifestTried = true;
            // 기존 검증 촬영(--sagajeong-...proof)은 사가정 OSM 표본을 기준으로 하므로 그대로 둔다.
            if (Environment.GetCommandLineArgs().Any(item => item.StartsWith("--sagajeong-", StringComparison.Ordinal))) return null;
            var manifest = ReadMapResource<DongModuleManifest>(DongModuleManifestResource);
            if (manifest == null) return null;
            if (manifest.schema != DongModuleSchema || manifest.modules == null || manifest.modules.Length == 0 ||
                string.IsNullOrEmpty(manifest.buildingIndexResource))
            {
                Debug.LogWarning("Hongdal dong modules: manifest is not usable");
                return null;
            }

            dongManifest = manifest;
            foreach (var entry in manifest.modules)
            {
                var module = new DongModuleState { Entry = entry, Status = DongModuleWaiting };
                var problem = 행정동모듈Policy.ValidateEntry(entry.regionId, entry.resource, entry.bytes, entry.sha256,
                    entry.west, entry.south, entry.east, entry.north);
                if (problem != null) module.Status = "목록 오류 · " + problem;
                var ring = entry.boundary ?? Array.Empty<double>();
                var count = ring.Length / 2;
                module.Latitudes = new double[count];
                module.Longitudes = new double[count];
                for (var i = 0; i < count; i++)
                {
                    module.Latitudes[i] = ring[i * 2];
                    module.Longitudes[i] = ring[i * 2 + 1];
                }
                dongModules.Add(module);
                dongModuleById[entry.regionId] = module;
            }
            Debug.Log($"Hongdal dong modules: {manifest.scopeRevision}, {manifest.modules.Length} modules, {manifest.label}");
            return dongManifest;
        }

        // LoadDiorama가 부른다. 동마다 메시를 읽고 판본(바이트 수·해시)과 원점을 확인한 뒤 한 장면에 놓는다.
        // 하나도 못 읽으면 false: 동네는 평면 지도로 남는다. 다른 동이나 사가정 OSM 메시로 대신하지 않는다(r54).
        private bool LoadDongModuleMeshes(Shader shader, Shader shadowShader)
        {
            var manifest = EnsureDongManifest();
            if (manifest == null || shader == null) return false;

            dioramaMaterial = new Material(shader) { name = "Hongdal dong modules" };
            if (shadowShader != null) dioramaShadowMaterial = new Material(shadowShader) { name = "Hongdal dong module shadows" };
            dioramaRoot = new GameObject("Hongdal dong modules");
            dioramaLatitude = manifest.originLatitude;
            dioramaLongitude = manifest.originLongitude;
            dioramaWest = double.MaxValue;
            dioramaSouth = double.MaxValue;
            dioramaEast = double.MinValue;
            dioramaNorth = double.MinValue;
            dioramaBuildings = 0;
            dioramaFillBuildings = 0;
            dioramaRenderers = 0;
            var vertices = 0;

            using var sha = SHA256.Create();
            foreach (var module in dongModules)
            {
                if (module.Status != DongModuleWaiting) continue;
                var entry = module.Entry;
                var asset = Resources.Load<TextAsset>(entry.resource);
                if (asset == null)
                {
                    module.Status = "자원 없음";
                    continue;
                }
                try
                {
                    var bytes = asset.bytes;
                    var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
                    if (bytes.Length != entry.bytes || !string.Equals(hash, entry.sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        module.Status = "판본 불일치";
                        continue;
                    }
                    using var reader = new BinaryReader(new MemoryStream(bytes));
                    var magic = reader.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != 'H' || magic[1] != 'D' || magic[2] != 'I' || magic[3] != 'O' ||
                        reader.ReadInt32() != 2)
                        throw new InvalidDataException("unknown header");
                    var latitude = reader.ReadDouble();
                    var longitude = reader.ReadDouble();
                    if (!행정동모듈Policy.SameOrigin(latitude, longitude, manifest.originLatitude, manifest.originLongitude))
                    {
                        module.Status = "원점 다름";
                        continue;
                    }
                    var west = reader.ReadDouble();
                    var south = reader.ReadDouble();
                    var east = reader.ReadDouble();
                    var north = reader.ReadDouble();
                    var buildings = reader.ReadInt32();
                    reader.ReadInt32();
                    var mesh = ReadDioramaMesh(reader, "Dong " + entry.code, true);
                    var shadows = ReadDioramaMesh(reader, "Dong " + entry.code + " shadows", false);

                    var part = new GameObject(entry.regionId + " " + entry.name);
                    part.transform.SetParent(dioramaRoot.transform, false);
                    module.Part = part;
                    module.Renderers.Add(AddDongPart(part, "Ground, roads and buildings", mesh, dioramaMaterial));
                    if (dioramaShadowMaterial != null && shadows.vertexCount > 0)
                        module.Renderers.Add(AddDongPart(part, "Shadows", shadows, dioramaShadowMaterial));
                    if (module.Latitudes.Length >= 3)
                        module.Renderers.Add(AddDongPart(part, "Edge", BuildDongSkirt(module, manifest), dioramaMaterial));
                    dioramaWest = Math.Min(dioramaWest, west);
                    dioramaSouth = Math.Min(dioramaSouth, south);
                    dioramaEast = Math.Max(dioramaEast, east);
                    dioramaNorth = Math.Max(dioramaNorth, north);
                    dioramaBuildings += buildings;
                    vertices += mesh.vertexCount;
                    module.Status = DongModuleLoaded;
                }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException)
                {
                    module.Status = "읽기 실패";
                    Debug.LogWarning($"Hongdal dong module {entry.regionId}: mesh could not be read. {exception.Message}");
                }
                finally
                {
                    Resources.UnloadAsset(asset);
                }
            }

            foreach (var module in dongModules)
                Debug.Log($"Hongdal dong module {module.Entry.regionId} {module.Entry.name}: {module.Status}");
            if (DongModulesLoadedCount == 0)
            {
                ReleaseDiorama();
                return false;
            }
            PrepareDongLayout(manifest);
            dioramaRoot.SetActive(false);
            QualitySettings.antiAliasing = 4;
            ApplyDongDim();
            Debug.Log($"Hongdal dong modules: loaded {DongModulesLoadedCount}/{dongModules.Count}, buildings {dioramaBuildings}, " +
                $"vertices {vertices}, renderers {dioramaRenderers}, materials {(dioramaShadowMaterial != null ? 2 : 1)}");
            return true;
        }

        private Renderer AddDongPart(GameObject parent, string partName, Mesh mesh, Material material)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(parent.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            dioramaRenderers++;
            return renderer;
        }

        // 고른 동은 밝게, 같이 읽은 이웃 동은 어둡게. 그림자 재질은 건드리지 않는다.
        // 행정동 깃발이 켜져 있으면 밝기는 영역 단계와 운영의 깊이를 따르고, 다른 동을 볼 때만 조금 더 어둡게 한다.
        private void ApplyDongDim()
        {
            dongDimBlock ??= new MaterialPropertyBlock();
            // 역 중심 보기에서는 한 판에 여러 동이 함께 놓인다. 고른 동과 아닌 동을 밝기로 가르지 않는다.
            var focus = StationView ? null : focusDongRegion;
            foreach (var module in dongModules)
            {
                var dim = DongFlagsActive
                    ? DongBrightness(module) * 행정동모듈Policy.FocusFactor(focus, module.Entry.regionId)
                    : 행정동모듈Policy.DimFor(focus, module.Entry.regionId);
                foreach (var renderer in module.Renderers)
                {
                    if (renderer == null || renderer.sharedMaterial != dioramaMaterial) continue;
                    renderer.GetPropertyBlock(dongDimBlock);
                    dongDimBlock.SetFloat(DongDimId, dim);
                    renderer.SetPropertyBlock(dongDimBlock);
                }
            }
        }

        private void ClearDongFocus()
        {
            if (focusDongRegion == null) return;
            focusDongRegion = null;
            ApplyDongDim();
        }

        // 순회 정거장·동 칩·동 이름표가 부른다. 그 동의 경계가 결정 카드 옆 빈 곳에 다 들어오게 동네 지도를 옮긴다.
        // 숨겨 둔 동이면 다시 보이게 하고, 이 동만 보기면 그 동으로 바꾼다.
        // 그 동의 모듈을 읽지 못했으면 false: 지도는 그대로 두고 카드가 이유를 보여 준다.
        private bool FocusDongModule(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || !DongModulesActive) return false;
            if (!dongModuleById.TryGetValue(regionId, out var module)) return false;
            if (StationView)
            {
                // 역 중심 보기: 판 안의 동이면 지도는 그대로 두고 사업 칸만 그 동으로 바꾼다.
                // 판에 없는 동이면 그 동이 든 다른 역의 판으로 넘기고, 어느 판에도 없을 때만 그 동만 보기로 넘어간다.
                if (mapLevel != MapLevel.Dong) ReturnToDong();
                if (mapLevel == MapLevel.Dong && !StationHas(regionId)) StepStationTo(regionId);
                if (mapLevel == MapLevel.Dong && StationHas(regionId))
                {
                    SelectStationDong(regionId);
                    return true;
                }
                dongView = DongView.Solo;
            }
            if (mapLevel != MapLevel.Dong) ReturnToDong();
            if (mapLevel != MapLevel.Dong || !module.Loaded || dioramaRoot == null || metresPerPixel <= 0 || module.Latitudes.Length < 3)
                return false;

            if (dongView == DongView.Solo) soloRegion = regionId;
            else dongHidden.Remove(regionId);
            RecomputeDongBounds();
            var (shiftEast, shiftNorth) = DongTargetShift(module);
            var ring = new List<(double, double)>(module.Latitudes.Length);
            for (var i = 0; i < module.Latitudes.Length; i++)
                ring.Add(((module.Longitudes[i] - centerLongitude) * longitudeScale + shiftEast,
                    (module.Latitudes[i] - centerLatitude) * 111200 + shiftNorth));
            var area = CycleCardVisible ? DongFocusArea : DongFitArea;
            // 행정동 깃발: 맞춘 동 위에 동 사업 칸이 설 자리를 남긴다.
            if (DongFlagsActive)
            {
                // 돌려 본 방향에서는 칸을 띄운 만큼에 더해, 칸이 위쪽 한계에 닿아 못 올라가는 만큼(30)도 동을 내려 준다.
                var room = DongTradeCardRoom + (DongTradeCardLift > 0 ? DongTradeCardLift + 30f : 0f);
                area = new Rect(area.x, area.y + room, area.width, area.height - room);
            }
            var (metres, east, north) = 행정동모듈Policy.Fit(ring, area.width, area.height, DongFitMarginNow, dioramaOn,
                BaseLens.YawCos, BaseLens.YawSin, BaseLens.PitchSin);      // 화면에 맞추는 것은 멀리서 보는 일이라 바탕 시점으로 잰다
            var zoom = Mathf.Clamp((float)(metresPerPixel / metres), DongMinZoom, MaxMapZoom);
            var perPixel = (float)metresPerPixel / zoom;
            var centre = Field.size * 0.5f;
            mapZoom = zoom;
            SyncCloseLens();
            mapOffset = new Vector2(centre.x * (1f - zoom) - (float)east / perPixel, centre.y * (1f - zoom) + (float)north / perPixel);
            mapOffset += FlatDeltaFor(dioramaOn, area.center - Field.position - centre);
            selectedBuilding = null;
            dongZoomFloorVisible = false;
            localTextureDirty = true;
            focusDongRegion = regionId;
            ApplyDongDim();
            Debug.Log($"Hongdal dong module focus: {regionId} {module.Entry.name}, view {dongView}, zoom {zoom:0.000}");
            return true;
        }

        // 결정 카드의 정거장 메모. 행정동 정거장은 그 동 모듈의 상태를 그대로 보여 준다.
        private string CycleStopNote(순회정거장 stop)
        {
            if (stop == null) return null;
            if (stop.RegionId == null || !DongModulesActive) return stop.Note;
            var module = dongModules.FirstOrDefault(item => item.Entry.regionId == stop.RegionId);
            if (module == null) return "이 동의 행정동 모듈 없음 · 지도는 그대로";
            if (module.Status == DongModuleWaiting) return "행정동 모듈 읽는 중";
            if (!module.Loaded) return "행정동 모듈 " + module.Status + " · 지도는 그대로";
            return $"행정동 모듈 · 건물 {module.Entry.buildings:N0}동 · 2023 경계 후보";
        }

        // 기존 역할표의 건물 ID → 모듈 건물 ID (굽기 도구가 같은 자리로 맞춘 표)
        private string DongRoleBuilding(string buildingId)
        {
            var manifest = EnsureDongManifest();
            if (manifest?.roleMap == null) return null;
            return 행정동모듈Policy.MapRoleBuilding(manifest.roleMap.Select(link => (link.from, link.to)).ToList(), buildingId);
        }

        // 동 경계와 이름표. 고른 동은 민트 실선과 큰 이름표, 이웃 동은 옅은 선(띄워 보기에서는 실선)과 이름표.
        // 이름표를 누르면 그 동으로 간다. 숨긴 동은 그리지 않는다. 읽지 못한 동은 붉은 점선과 이유를 그린다.
        private void DrawDongModuleBoundaries()
        {
            if (mapLevel != MapLevel.Dong || !DioramaVisible) return;
            if (StationView)
            {
                DrawStationBoard();     // 역 중심 보기의 선과 이름표
                return;
            }
            var far = mapZoom < FarViewZoom;
            var separated = dongView == DongView.Separated;
            var repaint = Event.current.type == EventType.Repaint;
            DongModuleState focus = null;
            foreach (var module in dongModules)
            {
                var count = module.Latitudes.Length;
                if (count < 3) continue;
                if (module.Loaded && !DongVisible(module)) continue;
                if (module.Entry.regionId == focusDongRegion && module.Loaded)
                {
                    focus = module;
                    continue;
                }
                var color = module.Loaded ? Fade(Soft, separated ? 0.7f : 0.55f) : SignalRed;
                if (repaint)
                {
                    var previous = PlotSceneIn(module, module.Latitudes[count - 1], module.Longitudes[count - 1]);
                    for (var i = 0; i < count; i++)
                    {
                        var point = PlotSceneIn(module, module.Latitudes[i], module.Longitudes[i]);
                        Line(previous, point, color, module.Loaded ? 1.4f : 2f, !module.Loaded || !separated);
                        previous = point;
                    }
                }
                // 행정동 깃발이 켜져 있으면 이름표를 늘 그린다. 이름표가 단계를 알리고 그 동으로 가는 단추이기 때문이다.
                if (!far && !separated && module.Loaded && !DongFlagsActive) continue;
                var centre = PlotDongCentre(module);
                // 지도 영역 밖(아래 운영 띠 너머)이면 이름표를 그리지 않는다. 가운데의 역·건물 이름표와 겹치지 않게 조금 아래에 둔다.
                if (centre.x < 0 || centre.x > Field.width || centre.y < 0 || centre.y > Field.height - 60) continue;
                var text = module.Loaded ? module.Entry.name : module.Entry.name + " · " + module.Status;
                var tone = module.Loaded ? Paper : SignalRed;
                var rim = Color.clear;
                var flagged = false;
                if (module.Loaded && DongFlagsActive) text = DongPlate(module, out tone, out rim, out flagged);
                var plate = new Rect(centre.x - text.Length * 6.5f - 10, centre.y + 18, text.Length * 13f + 20, 22);
                if (rim.a > 0f) Paint(new Rect(plate.x - 1.5f, plate.y - 1.5f, plate.width + 3, plate.height + 3), rim, 7);
                Paint(plate, rim.a > 0f ? Canvas : Fade(Canvas, 0.85f), 6);
                Text(plate, text, 11, tone, true, TextAnchor.MiddleCenter);
                if (flagged && repaint) DrawDongFlag(centre);
                if (module.Loaded && HitMapMarker(plate))
                {
                    FocusDongModule(module.Entry.regionId);
                    if (mapDoubleClick) OpenDongDesk(module.Entry.regionId);      // 두 번 누르면 동 관리 창 (SagajeongDispatchDongDesk.cs)
                }
            }
            if (focus == null) return;

            var points = new Vector2[focus.Latitudes.Length];
            for (var i = 0; i < points.Length; i++) points[i] = PlotSceneIn(focus, focus.Latitudes[i], focus.Longitudes[i]);
            if (repaint)
                for (var i = 0; i < points.Length; i++)
                {
                    Line(points[i], points[(i + 1) % points.Length], Canvas, 5f);
                    Line(points[i], points[(i + 1) % points.Length], Mint, 2.5f);
                }
            var top = points.OrderBy(point => point.y).First();
            // 행정동 깃발: 깃발을 세우고, 이름 칸 대신 동 사업 칸을 그린다.
            if (DongFlagsActive)
            {
                var land = DongLand(focus.Entry.regionId);
                if (land != null && land.Level >= TerritoryLevels.Entering && repaint) DrawDongFlag(PlotDongCentre(focus));
                if (DrawDongBusinessCard(focus, top)) return;
            }
            var title = focus.Entry.name + " · 건물 " + focus.Entry.buildings.ToString("N0") + "동";
            var width = Mathf.Max(title.Length * 12.5f, 200f) + 24f;
            var card = new Rect(top.x - width / 2, Mathf.Max(top.y - 50, 140 - Field.y), width, 42);
            Paint(card, Mint, 8);
            Paint(new Rect(card.x + 1.5f, card.y + 1.5f, card.width - 3, card.height - 3), Canvas, 7);
            Text(new Rect(card.x, card.y + 3, card.width, 20), title, 13, Paper, true, TextAnchor.MiddleCenter);
            Text(new Rect(card.x, card.y + 22, card.width, 16), dongManifest?.label ?? "내부 검토용", 10, Soft, align: TextAnchor.MiddleCenter);
        }

        // ── 동 보기 방식: 붙여 보기 · 띄워 보기 · 이 동만, 동 칩 ─────────────────────────────

        private bool DongVisible(DongModuleState module) => module != null && module.Loaded &&
            행정동모듈Policy.Visible(dongView == DongView.Solo, soloRegion, dongHidden, module.Entry.regionId);

        private DongModuleState DongModuleById(string regionId) =>
            !string.IsNullOrEmpty(regionId) && dongModuleById.TryGetValue(regionId, out var module) ? module : null;

        // 공통 원점 기준 m (메시를 구운 좌표와 같다)
        private (double East, double North) DongLocal(double latitude, double longitude) => (
            (longitude - dongManifest.originLongitude) * dongManifest.metresPerDegreeLongitude,
            (latitude - dongManifest.originLatitude) * dongManifest.metresPerDegreeLatitude);

        // 그 점이 든 동(읽은 동만). 어느 동에도 없으면 null.
        private DongModuleState DongModuleAt(double latitude, double longitude)
        {
            if (dongGrid == null || dongManifest == null) return null;
            var (east, north) = DongLocal(latitude, longitude);
            var index = dongGrid.At(east, north);
            return index >= 0 && index < dongModules.Count && dongModules[index].Loaded ? dongModules[index] : null;
        }

        // 지금 그 동이 옮겨진 거리(m)와, 보기 방식이 끝까지 바뀌었을 때의 거리.
        private Vector2 DongShift(DongModuleState module)
        {
            var (east, north) = DongShiftAt(module, Mathf.Max(0f, dongSpread));
            return new Vector2((float)east, (float)north);
        }

        private (double East, double North) DongTargetShift(DongModuleState module) => DongShiftAt(module, DongSpreadTarget);

        // 띄운 정도(0~1)에서의 자리. 목록의 띄워 보기 자리를 쓰고, 없으면 묶음 가운데에서 밀어낸다.
        private (double East, double North) DongShiftAt(DongModuleState module, double fraction) => dongHasSeparatedOffsets
            ? (module.Entry.separatedEast * fraction, module.Entry.separatedNorth * fraction)
            : 행정동모듈Policy.ExplodeOffset(module.CentreEast, module.CentreNorth, dongUnionEast, dongUnionNorth,
                행정동모듈Policy.SeparatedSpread * fraction);

        // 그 동과 함께 옮긴 화면 자리. 숨긴 동이면 화면 밖.
        private Vector2 PlotSceneIn(DongModuleState module, double latitude, double longitude, float height = 0f)
        {
            if (!DioramaVisible) return PlotOnBoard(latitude, longitude);
            if (StationView && !StationShows(latitude, longitude)) return DongHiddenPoint;     // 역 판 밖의 표식은 그리지 않는다
            if (module == null || !module.Loaded) return ProjectScene(latitude, longitude, height, Vector2.zero);
            if (!DongVisible(module)) return DongHiddenPoint;
            return ProjectScene(latitude, longitude, height, DongShift(module));
        }

        // 건물 외곽 한 점. 모듈 건물은 그 건물의 동과 함께 옮긴다(경계 근처 건물이 이웃 동으로 잘못 가지 않게).
        private Vector2 PlotBuilding(BuildingShape shape, int index, float height = 0f)
        {
            var module = DongModuleById(shape.Record.region);
            return module == null
                ? PlotScene(shape.Latitudes[index], shape.Longitudes[index], height)
                : PlotSceneIn(module, shape.Latitudes[index], shape.Longitudes[index], height);
        }

        private Vector2 PlotDongCentre(DongModuleState module)
        {
            var latitude = dongManifest.originLatitude + module.CentreNorth / dongManifest.metresPerDegreeLatitude;
            var longitude = dongManifest.originLongitude + module.CentreEast / dongManifest.metresPerDegreeLongitude;
            return PlotSceneIn(module, latitude, longitude);
        }

        // 건물 고르기: 보이는 동마다 그 동의 이동만큼 되돌려 찾는다. 모듈이 없으면 한 겹(이동 없음).
        private IEnumerable<(Vector2 Shift, string Region)> BuildingPickLayers()
        {
            if (!DongModulesLoaded)
            {
                yield return (Vector2.zero, null);
                yield break;
            }
            foreach (var module in dongModules)
                if (DongVisible(module)) yield return (DongShift(module), module.Entry.regionId);
        }

        // 메시를 다 읽은 뒤: 동마다 넓이 중심, 묶음 가운데, 표식용 격자, 보이는 범위를 준비한다.
        private void PrepareDongLayout(DongModuleManifest manifest)
        {
            var rings = new List<IReadOnlyList<(double, double)>>(dongModules.Count);
            double sumEast = 0, sumNorth = 0;
            var loaded = 0;
            foreach (var module in dongModules)
            {
                var ring = new List<(double, double)>(module.Latitudes.Length);
                for (var i = 0; i < module.Latitudes.Length; i++) ring.Add(DongLocal(module.Latitudes[i], module.Longitudes[i]));
                rings.Add(module.Loaded ? ring : new List<(double, double)>());
                if (ring.Count >= 3) (module.CentreEast, module.CentreNorth) = 행정동모듈Policy.Centroid(ring);
                if (!module.Loaded) continue;
                sumEast += module.CentreEast;
                sumNorth += module.CentreNorth;
                loaded++;
            }
            dongUnionEast = loaded > 0 ? sumEast / loaded : 0;
            dongUnionNorth = loaded > 0 ? sumNorth / loaded : 0;
            dongGrid = new 행정동격자(rings, DongGridCell);
            dongHasSeparatedOffsets = dongModules.Any(module => module.Entry.separatedEast != 0 || module.Entry.separatedNorth != 0);
            dongSpread = DongSpreadTarget;
            RecomputeDongBounds();
            UpdateDongLayout();
        }

        // 보이는 동의 범위(띄운 자리 포함)로 2.5D 범위를 다시 잡는다. 화면 붙잡기·동네 전체 보기가 이 범위를 쓴다.
        private void RecomputeDongBounds()
        {
            if (dongManifest == null) return;
            double west = double.MaxValue, south = double.MaxValue, east = double.MinValue, north = double.MinValue;
            foreach (var module in dongModules)
            {
                if (!DongVisible(module)) continue;
                var (shiftEast, shiftNorth) = DongTargetShift(module);
                var dLongitude = shiftEast / dongManifest.metresPerDegreeLongitude;
                var dLatitude = shiftNorth / dongManifest.metresPerDegreeLatitude;
                west = Math.Min(west, module.Entry.west + dLongitude);
                east = Math.Max(east, module.Entry.east + dLongitude);
                south = Math.Min(south, module.Entry.south + dLatitude);
                north = Math.Max(north, module.Entry.north + dLatitude);
            }
            if (west > east) return;
            dioramaWest = west;
            dioramaEast = east;
            dioramaSouth = south;
            dioramaNorth = north;
        }

        // LateUpdate가 매 프레임 부른다. 벌어진 정도를 목표로 천천히 옮기고, 동 메시를 옮기거나 끈다. 업무 상태는 건드리지 않는다.
        private void UpdateDongLayout()
        {
            if (!DongModulesLoaded) return;
            var target = DongSpreadTarget;
            if (dongSpread < 0) dongSpread = target;
            if (!Mathf.Approximately(dongSpread, target))
                dongSpread = Mathf.MoveTowards(dongSpread, target, Time.unscaledDeltaTime * 3f);   // 약 0.35초
            var scaleX = Mathf.Max(0.0001f, dioramaRoot.transform.localScale.x);
            foreach (var module in dongModules)
            {
                if (module.Part == null) continue;
                var visible = DongVisible(module);
                if (module.Part.activeSelf != visible) module.Part.SetActive(visible);
                var shift = DongShift(module);
                module.Part.transform.localPosition = new Vector3(shift.x / scaleX, 0, shift.y);
            }
        }

        private void SetDongView(DongView view)
        {
            if (dongView == view) return;
            var keep = focusDongRegion;
            if (view == DongView.Solo)
                soloRegion = keep ?? DongModuleAtViewCentre()?.Entry.regionId ??
                    dongModules.FirstOrDefault(module => module.Loaded)?.Entry.regionId;
            dongView = view;
            RecomputeDongBounds();
            if (view == DongView.Station)
            {
                stationLayoutStamp = int.MinValue;
                if (!FocusStation()) ShowWholeDong();
            }
            else if (view == DongView.Solo && soloRegion != null) FocusDongModule(soloRegion);
            else if (keep != null && DongVisible(DongModuleById(keep))) FocusDongModule(keep);
            else ShowWholeDong();
            ApplyDongDim();
            Debug.Log("Hongdal dong view: " + view + (view == DongView.Solo ? " " + soloRegion : string.Empty));
        }

        private DongModuleState DongModuleAtViewCentre()
        {
            if (metresPerPixel <= 0) return null;
            var focus = DioramaFocus((float)metresPerPixel / mapZoom);
            return DongModuleAt(centerLatitude + focus.z / 111200, centerLongitude + focus.x / longitudeScale);
        }

        // 동 칩: 이 동만 보기에서는 그 동으로 바꾸고, 아니면 보이기/숨기기. 적어도 한 동은 남긴다.
        private void ToggleDongChip(DongModuleState module)
        {
            var region = module.Entry.regionId;
            if (dongView == DongView.Solo)
            {
                FocusDongModule(region);
                return;
            }
            if (dongHidden.Contains(region)) dongHidden.Remove(region);
            else
            {
                var hiddenLoaded = dongModules.Count(item => item.Loaded && dongHidden.Contains(item.Entry.regionId));
                if (!행정동모듈Policy.CanHide(DongModulesLoadedCount, hiddenLoaded)) return;
                dongHidden.Add(region);
                if (focusDongRegion == region) ClearDongFocus();
                if (selectedBuilding != null && buildingShapes[selectedBuilding.Value].Record.region == region) selectedBuilding = null;
            }
            RecomputeDongBounds();
            if (focusDongRegion != null) FocusDongModule(focusDongRegion);
            else ShowWholeDong();
            Debug.Log($"Hongdal dong chips: hidden {dongHidden.Count}");
        }

        // 동 보기(붙여·띄워·이 동만, 동 칩)를 쓸 수 있는가. 정리한 첫 화면에서는 이 도구가 설정 칸 안에 있다.
        private bool DongViewAvailable => DongModulesLoaded && DioramaVisible && mapLevel == MapLevel.Dong;

        private bool DongViewPanelVisible => !TidyScreen && DongViewAvailable && expansionView && !mapOnly && !showDesk;

        private bool DongViewPanelCovers(Vector2 point) => DongViewPanelVisible && DongViewPanel.Contains(point);

        private void DrawDongViewControl()
        {
            if (!DongViewPanelVisible) return;
            Paint(DongViewPanel, Panel, 10);
            DrawDongViewControl(DongViewPanel);
        }

        // 보기 방식 세 칸과 동 칩. 바탕은 부르는 쪽이 칠한다(왼쪽 도구 줄, 또는 설정 칸).
        private void DrawDongViewControl(Rect panel)
        {
            var x = panel.x + 4;
            var y = panel.y + 4;
            var width = (panel.width - 8 - 4) / 3f;
            // 네 번째 칸 「역 중심」: 동 단위가 아니라 역을 가운데 둔 판으로 본다(옵션).
            var viewWidth = (panel.width - 8 - 6) / 4f;
            var views = new[] { ("붙여", DongView.Joined), ("띄워", DongView.Separated), ("이 동만", DongView.Solo), ("역 중심", DongView.Station) };
            for (var i = 0; i < views.Length; i++)
            {
                var (label, view) = views[i];
                var rect = new Rect(x + i * (viewWidth + 2), y, viewWidth, 20);
                var chosen = dongView == view;
                Paint(rect, chosen ? Mint : Tile, 5);
                Text(rect, label, 10, chosen ? Canvas : Paper, true, TextAnchor.MiddleCenter);
                if (Hit(rect)) SetDongView(view);
            }
            var shown = 0;
            foreach (var module in dongModules)
            {
                if (!module.Loaded) continue;
                var rect = new Rect(x + shown % 3 * (width + 2), y + 24 + shown / 3 * 20, width, 18);
                shown++;
                if (shown > 6) break;
                var visible = DongVisible(module);
                var focused = module.Entry.regionId == focusDongRegion;
                Paint(rect, visible ? (focused ? CycleDone : Tile) : Canvas, 5);
                Text(rect, DongShortName(module.Entry.name), 10, visible ? (focused ? Mint : Paper) : Fade(Soft, 0.5f), focused,
                    TextAnchor.MiddleCenter);
                if (!visible) Paint(new Rect(rect.x + 8, rect.center.y, rect.width - 16, 1), Fade(Soft, 0.5f));
                if (Hit(rect)) ToggleDongChip(module);
            }
        }

        // "면목제3.8동" → "3·8동", "면목본동" → "본동"
        private static string DongShortName(string name)
        {
            var text = name ?? string.Empty;
            if (text.StartsWith("면목제", StringComparison.Ordinal)) text = text.Substring(3);
            else if (text.StartsWith("면목", StringComparison.Ordinal)) text = text.Substring(2);
            return text.Replace('.', '·');
        }

        // 동 판의 옆면: 경계를 따라 아래로 내린 벽. 띄워 보면 동마다 떠 있는 판처럼 보인다(표현값).
        private Mesh BuildDongSkirt(DongModuleState module, DongModuleManifest manifest)
        {
            var count = module.Latitudes.Length;
            var mesh = new Mesh { name = "Dong " + module.Entry.code + " edge", indexFormat = IndexFormat.UInt32 };
            dioramaMeshes.Add(mesh);
            var vertices = new List<Vector3>(count * 4);
            var colors = new List<Color32>(count * 4);
            var triangles = new List<int>(count * 6);
            var top = new Color32(0x34, 0x4A, 0x5B, 255);
            var bottom = new Color32(0x0C, 0x13, 0x19, 255);
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var a = new Vector3((float)((module.Longitudes[i] - manifest.originLongitude) * manifest.metresPerDegreeLongitude), 0.02f,
                    (float)((module.Latitudes[i] - manifest.originLatitude) * manifest.metresPerDegreeLatitude));
                var b = new Vector3((float)((module.Longitudes[next] - manifest.originLongitude) * manifest.metresPerDegreeLongitude), 0.02f,
                    (float)((module.Latitudes[next] - manifest.originLatitude) * manifest.metresPerDegreeLatitude));
                var first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(new Vector3(b.x, -DongSkirtDepth, b.z));
                vertices.Add(new Vector3(a.x, -DongSkirtDepth, a.z));
                colors.Add(top);
                colors.Add(top);
                colors.Add(bottom);
                colors.Add(bottom);
                triangles.Add(first);
                triangles.Add(first + 1);
                triangles.Add(first + 2);
                triangles.Add(first);
                triangles.Add(first + 2);
                triangles.Add(first + 3);
            }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var zeros = new Vector2[vertices.Count];
            mesh.uv = zeros;
            mesh.uv2 = zeros;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
