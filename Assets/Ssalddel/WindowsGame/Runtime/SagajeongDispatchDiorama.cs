using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // 구조 개편 1단계: 동네 지도를 2.5D로 보여 주는 시제품.
    // 미리 구운 메시(도로 위계, 압출 건물, 그림자)를 기울인 정사영 카메라로 그리고,
    // 기존 IMGUI 표식(매장, 배송지, 기사, 경로)은 같은 카메라로 투영해 그 위에 올린다.
    // 규칙과 상태는 건드리지 않는다. 평면 지도의 확대·이동 값(mapZoom, mapOffset)을 그대로 쓴다.
    public sealed partial class SagajeongDispatchView
    {
        // 보는 방향(북쪽 기준 회전 30°, 내려다보는 각 55°)과 땅↔화면 계산은 디오라마시점이 맡는다 (3D 기반 A1).
        // 기본값에서 화면은 전과 같다. 방향을 바꾸는 일은 이 값 하나를 바꾸는 일이다(SetDioramaLens).
        private 디오라마시점 dioramaLens = 디오라마시점.Default;
        private const string DioramaViewOption = "--hongdal-view=";    // 시험용: --hongdal-view=<회전>,<내려다보는 각>
        private const float DioramaDistance = 900f;
        private const double DioramaEdgeMetres = 150;
        private const float DioramaInsetMetres = 120f;   // 끌어서 갈 수 있는 범위는 자료 가장자리에서 이만큼 안쪽까지
        private const float DioramaPerspectiveMargin = 0.08f;    // 원근에서는 화면 아래쪽이 더 크게 보이므로 동을 맞출 때 여백을 이만큼 더 둔다(비율)
        private static readonly Rect ExpansionDioramaButton = new(200, 626, 82, 26);
        private static readonly Rect ExpansionDioramaFacts = new(288, 626, 292, 26);
        private static readonly Color DioramaGround = Ink(0x1D2C38);
        private static readonly Color FlatGround = new Color32(17, 27, 33, 255);

        private GameObject dioramaRoot;
        private readonly List<Mesh> dioramaMeshes = new();
        private readonly List<GameObject> dioramaFillParts = new();
        private Material dioramaMaterial;
        private Material dioramaShadowMaterial;
        private Camera dioramaCamera;
        private bool dioramaTried;
        private bool dioramaOn = true;
        private bool dioramaPerspective;                 // 원근 보기(A6). 기본은 평행 보기다
        private bool dioramaShown;
        private float dioramaGuiScale = 1f;
        private double dioramaLatitude;
        private double dioramaLongitude;
        private double dioramaWest;
        private double dioramaSouth;
        private double dioramaEast;
        private double dioramaNorth;
        private int dioramaBuildings;
        private int dioramaFillBuildings;      // 자료가 없는 블록을 메운 예시 건물 수 (실제 건물 아님)
        private bool dioramaFillOn = true;
        private int dioramaRenderers;

        // 이번 프레임에 2.5D 장면이 그려졌는가. 카메라를 놓은 LateUpdate의 판단을 OnGUI가 그대로 따른다.
        private bool DioramaVisible => dioramaShown && mapLevel == MapLevel.Dong && dioramaCamera != null;

        private void LateUpdate()
        {
            SyncMusic();                 // 고른 곡: 불러오기 · 분석 · 지금의 박 (SagajeongDispatchMusic.cs)
            if (!dioramaTried && state != null && roadTexture != null) LoadDiorama();
            if (dioramaCamera == null) dioramaCamera = Camera.main;
            if (SyncPublicDong(dioramaCamera)) return;      // 공개판 동 디오라마가 열려 있으면 카메라를 맡긴다 (SagajeongDispatchPublicDong.cs)
            var camera = dioramaCamera;
            var scale = Mathf.Min(Screen.width / (float)Width, Screen.height / (float)Height);
            var show = dioramaOn && dioramaRoot != null && camera != null && scale > 0 && mapLevel == MapLevel.Dong &&
                roadTexture != null && metresPerPixel > 0 && DioramaCoversView();
            if (dioramaRoot != null && dioramaRoot.activeSelf != show) dioramaRoot.SetActive(show);
            if (camera != null && show != dioramaShown)
                camera.backgroundColor = show ? (DongModulesLoaded ? DongBackdrop : DioramaGround) : FlatGround;
            dioramaShown = show;
            if (!show)
            {
                StopWeatherParticles();      // 비 · 눈 알갱이는 동 디오라마에서만 (SagajeongDispatchWeatherParticles.cs)
                StopRealismLens();           // 렌즈는 동 디오라마에서만 (SagajeongDispatchRealism.cs)
                return;
            }

            // 가까이 보기: 지금 배율에 맞는 각으로 (SagajeongDispatchCloseView.cs). 멀리서는 바탕 시점 그대로다.
            if (SyncCloseLens()) mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset, mapZoom), mapZoom);
            dioramaGuiScale = scale;
            var metres = (float)metresPerPixel / mapZoom;          // 논리 픽셀 하나의 길이(m)
            var focus = DioramaFocus(metres);
            var rotation = Quaternion.Euler((float)dioramaLens.PitchDegrees, (float)dioramaLens.YawDegrees, 0);
            var halfHeight = Screen.height * 0.5f * metres / scale;      // 평행 보기에서 화면 세로 절반에 담기는 길이(m)
            if (dioramaPerspective)
            {
                // 원근 보기(A6): 화면 가운데의 땅이 평행 보기와 같은 크기로 보이는 거리에 카메라를 둔다.
                var distance = (float)디오라마시점.PerspectiveDistance(halfHeight);
                var (near, far) = 디오라마시점.PerspectiveClip(halfHeight);
                camera.orthographic = false;
                camera.fieldOfView = (float)디오라마시점.PerspectiveFovDegrees;
                camera.nearClipPlane = (float)near;
                camera.farClipPlane = (float)far;
                camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            }
            else
            {
                camera.orthographic = true;
                camera.orthographicSize = halfHeight;
                camera.nearClipPlane = 1f;
                camera.farClipPlane = DioramaDistance * 2.5f;
                camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * DioramaDistance, rotation);
            }

            // 메시는 자기 원점 기준 미터로 구워져 있다. 지금 지도 중심(Plot의 기준)에 맞춰 놓는다.
            var bakedLongitudeMetres = 111200 * Math.Cos(dioramaLatitude * Math.PI / 180);
            dioramaRoot.transform.position = new Vector3(
                (float)((dioramaLongitude - centerLongitude) * longitudeScale), 0,
                (float)((dioramaLatitude - centerLatitude) * 111200));
            dioramaRoot.transform.localScale = new Vector3((float)(longitudeScale / bakedLongitudeMetres), 1, 1);
            UpdateDongLayout();          // 행정동 모듈: 띄워 보기·숨기기 (SagajeongDispatchDongModules.cs)
            SyncManagedBuildings();      // 회색 동네와 관리 건물 (SagajeongDispatchManaged.cs)
            SyncStationWindow();         // 역 중심 보기의 둥근 창 (SagajeongDispatchStationView.cs)
            SyncRoamers();               // 계속 돌아다니는 기사 (SagajeongDispatchRoam.cs)
            SyncEnvironment();           // 계절 · 날씨 · 해 (SagajeongDispatchEnvironment.cs)
            SyncRealism(camera, halfHeight);     // 현실감 시험: 해 방향 · 그림자 · 렌즈 (SagajeongDispatchRealism.cs)
            SyncDriftSize(halfHeight);   // 날리는 잎 크기 (SagajeongDispatchTrees.cs)
            SyncWeatherParticles(focus, halfHeight, (float)dioramaLens.PitchDegrees);     // 땅 위의 비 · 눈 (SagajeongDispatchWeatherParticles.cs)
            SyncCloseShader(camera, focus, halfHeight);      // 가까이 보기: 창 · 안개 · 고른 곳 (SagajeongDispatchCloseView.cs)
        }

        // 보는 방향을 바꾼다 (3D 기반 A3). 이동량과 배율은 평면 지도 기준이라 화면 한가운데에 놓인 땅은 그대로다.
        // 새 방향에서 동네가 다 들어오도록 배율 한계와 이동 범위만 다시 맞춘다. 시계·주문·업무 상태는 건드리지 않는다.
        // 표식과 건물 고르기는 카메라로 계산하고, 이동·확대·동에 맞추기는 dioramaLens로 계산하므로 함께 따라온다.
        private void SetDioramaLens(double yawDegrees, double pitchDegrees)
        {
            // 받은 값은 바탕 시점이다. 화면이 쓰는 시점은 여기에 지금 배율만큼 눕힌 것이다 (SagajeongDispatchCloseView.cs).
            var next = 디오라마시점.Of(yawDegrees, pitchDegrees);
            if (Math.Abs(next.YawDegrees - dioramaBaseYaw) < 1e-6 && Math.Abs(next.PitchDegrees - dioramaBasePitch) < 1e-6) return;
            dioramaBaseYaw = next.YawDegrees;
            dioramaBasePitch = next.PitchDegrees;
            dioramaLens = CloseLensFor(mapZoom);
            Debug.Log("Hongdal diorama view: yaw " + next.YawDegrees.ToString("0.#") + ", pitch " + next.PitchDegrees.ToString("0.#"));
            if (!DioramaVisible || mapZoom <= 0) return;
            KeepDongZoomFloor();
            mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset, mapZoom), mapZoom);
        }

        // 원근 보기를 켜고 끈다 (3D 기반 A6). 보는 방식만 바뀐다. 시계·주문·업무 상태는 건드리지 않는다.
        // 표식과 건물 고르기는 카메라로 계산하므로 함께 따라온다. 이동·확대는 화면 가운데를 기준으로 평행 보기와 같은 계산을 쓴다.
        private void SetDioramaPerspective(bool on)
        {
            if (dioramaPerspective == on) return;
            var whole = AtDongFloor;
            dioramaPerspective = on;
            mapPointerDown = false;
            mapDragging = false;
            mapClick = false;
            Debug.Log("Hongdal diorama perspective: " + (on ? "on" : "off"));
            if (!DioramaVisible || mapZoom <= 0) return;
            // 동이나 동네 전체에 맞춰 보던 중이면 새 여백으로 다시 맞춘다. 고른 건물은 그대로 둔다.
            var chosen = selectedBuilding;
            if (StationView && FocusStation()) selectedBuilding = chosen;
            else if (focusDongRegion != null && FocusDongModule(focusDongRegion)) selectedBuilding = chosen;
            else if (whole) ShowWholeDong();
            else
            {
                KeepDongZoomFloor();
                mapOffset = ClampDioramaOffset(ClampMapOffset(mapOffset, mapZoom), mapZoom);
            }
        }

        // 시험용 실행 인자. 설정 칸에는 아직 두지 않았다(방향을 바꾼 화면을 눈으로 확인하기 전이다).
        private static bool TryReadDioramaViewOption(out double yawDegrees, out double pitchDegrees)
        {
            yawDegrees = 0;
            pitchDegrees = 0;
            var option = Environment.GetCommandLineArgs().FirstOrDefault(item => item.StartsWith(DioramaViewOption, StringComparison.Ordinal));
            if (option == null) return false;
            var parts = option.Substring(DioramaViewOption.Length).Split(',');
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            return parts.Length == 2 &&
                double.TryParse(parts[0], System.Globalization.NumberStyles.Float, culture, out yawDegrees) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, culture, out pitchDegrees);
        }

        // 화면 한가운데에 놓인 지점(평면 지도 기준)의 월드 좌표. x 동쪽, z 북쪽, 단위 m.
        private Vector3 DioramaFocus(float metres)
        {
            var centre = Field.size * 0.5f;
            return new Vector3((centre.x * (1f - mapZoom) - mapOffset.x) * metres, 0,
                -(centre.y * (1f - mapZoom) - mapOffset.y) * metres);
        }

        private bool DioramaCoversView()
        {
            var focus = DioramaFocus((float)metresPerPixel / mapZoom);
            var latitude = centerLatitude + focus.z / 111200;
            var longitude = centerLongitude + focus.x / longitudeScale;
            var latitudeEdge = DioramaEdgeMetres / 111200;
            var longitudeEdge = DioramaEdgeMetres / longitudeScale;
            return latitude >= dioramaSouth - latitudeEdge && latitude <= dioramaNorth + latitudeEdge &&
                longitude >= dioramaWest - longitudeEdge && longitude <= dioramaEast + longitudeEdge;
        }

        // 표식 위치. 2.5D일 때는 카메라로 투영하고, 아니면 평면 지도 좌표를 그대로 쓴다. (Field 그룹 안 좌표)
        // 행정동 모듈을 띄워 보면 그 점이 든 동만큼 함께 옮긴다. 숨긴 동의 점은 화면 밖(DongHiddenPoint)으로 보낸다.
        private Vector2 PlotScene(double latitude, double longitude, float height = 0f)
        {
            if (!DioramaVisible) return PlotOnBoard(latitude, longitude);
            var shift = Vector2.zero;
            if (StationView && !StationShows(latitude, longitude)) return DongHiddenPoint;     // 역 판 밖의 표식은 그리지 않는다
            if (DongModulesLoaded)
            {
                var module = DongModuleAt(latitude, longitude);
                if (module != null)
                {
                    if (!DongVisible(module)) return DongHiddenPoint;
                    shift = DongShift(module);
                }
            }
            return ProjectScene(latitude, longitude, height, shift);
        }

        // 위경도(+옮긴 거리 m) → 지도 영역 안 화면 좌표
        private Vector2 ProjectScene(double latitude, double longitude, float height, Vector2 shift)
        {
            var world = new Vector3((float)((longitude - centerLongitude) * longitudeScale) + shift.x, height,
                (float)((latitude - centerLatitude) * 111200) + shift.y);
            var screen = dioramaCamera.WorldToScreenPoint(world);
            var left = (Screen.width - Width * dioramaGuiScale) * 0.5f;
            var top = (Screen.height - Height * dioramaGuiScale) * 0.5f;
            return new Vector2((screen.x - left) / dioramaGuiScale - Field.x,
                (Screen.height - screen.y - top) / dioramaGuiScale - Field.y);
        }

        // 화면에서 끈 거리 → 평면 지도 이동량. 지도가 돌아가고 세로로 눌려 있으므로 되돌려 준다.
        private Vector2 FlatDelta(Vector2 screenDelta) => FlatDeltaFor(DioramaVisible, screenDelta);

        // 화면의 한 점(Field 그룹 안) → 같은 땅 위 지점의 평면 지도 좌표. 휠 확대의 기준점에 쓴다.
        private Vector2 FlatPoint(Vector2 screenPoint)
        {
            if (!DioramaVisible) return screenPoint;
            var centre = Field.size * 0.5f;
            return centre + FlatDelta(screenPoint - centre);
        }

        // 오토바이 그림을 지면에 눕힌다: 지도 회전을 뺀 방향으로 돌리고 세로로 누른다.
        private void LayOnGround(float heading, Vector2 pivot)
        {
            GUI.matrix = GUI.matrix * TurnAround(pivot,
                Matrix4x4.Scale(new Vector3(1, (float)dioramaLens.PitchSin, 1)) *
                Matrix4x4.Rotate(Quaternion.Euler(0, 0, heading - (float)dioramaLens.YawDegrees)));
        }

        private void DrawDioramaToggle()
        {
            var ready = dioramaRoot != null && mapLevel == MapLevel.Dong;
            var label = !dioramaOn ? "평면 지도" : ready && !DioramaVisible ? "2.5D 범위 밖" : "2.5D 지도";
            if (DioramaVisible)
            {
                // 1단계 관문(렌더러 128개, 재질 12개 안)을 화면에서 바로 확인할 수 있게 적어 둔다.
                // 누르면 예시 채움 건물을 끄고 켠다. 꺼 두면 실제 외곽이 있는 건물만 남는다.
                var fill = dioramaFillBuildings == 0 ? "없음" : dioramaFillOn ? $"{dioramaFillBuildings:N0}동" : "꺼짐";
                var renderers = dioramaRenderers - (dioramaFillOn ? 0 : dioramaFillParts.Count);
                var facts = DongModulesLoaded
                    ? $"모듈 {DongModulesLoadedCount}/{dongModules.Count} · 건물 {dioramaBuildings:N0}동 · 내부 검토용"
                    : $"건물 {dioramaBuildings:N0}동 · 채움 {fill} · 렌더러 {renderers} · 재질 {(dioramaShadowMaterial != null ? 2 : 1)}";
                if (ExpansionButton(ExpansionDioramaFacts, facts, dioramaFillBuildings > 0, Canvas))
                {
                    dioramaFillOn = !dioramaFillOn;
                    foreach (var part in dioramaFillParts) part.SetActive(dioramaFillOn);
                    mapPointerDown = false;
                    mapDragging = false;
                    mapClick = false;
                    Debug.Log("Hongdal diorama example fill: " + (dioramaFillOn ? "on" : "off"));
                }
            }
            if (!ExpansionButton(ExpansionDioramaButton, label, ready, dioramaOn && DioramaVisible ? Mint : Tile)) return;
            dioramaOn = !dioramaOn;
            mapPointerDown = false;
            mapDragging = false;
            mapClick = false;
            Debug.Log("Hongdal diorama: " + (dioramaOn ? "on" : "off"));
        }

        private void LoadDiorama()
        {
            dioramaTried = true;
            // 기존 검증 촬영(--sagajeong-...proof)은 평면 지도 좌표를 기준으로 하므로 그대로 둔다.
            if (Environment.GetCommandLineArgs().Any(item => item.StartsWith("--sagajeong-", StringComparison.Ordinal)))
            {
                dioramaOn = false;
                return;
            }
            if (TryReadDioramaViewOption(out var optionYaw, out var optionPitch)) SetDioramaLens(optionYaw, optionPitch);

            var shader = Resources.Load<Shader>("HongdalDiorama");
            var shadowShader = Resources.Load<Shader>("HongdalDioramaShadow");
            // r54 첫 조각: 행정동 모듈 목록이 있으면 동네 2.5D 지도를 모듈 묶음으로 바꾼다(SagajeongDispatchDongModules.cs).
            // 이때는 사가정 OSM 메시를 읽지 않는다. 모듈을 하나도 못 읽으면 평면 지도로 남는다.
            if (DongModulesActive)
            {
                if (shader == null) Debug.LogWarning("Hongdal diorama: shader resource is missing");
                else if (!LoadDongModuleMeshes(shader, shadowShader))
                    Debug.LogWarning("Hongdal dong modules: no module mesh could be read; the flat map stays");
                return;
            }

            var asset = Resources.Load<TextAsset>("hongdal-diorama");
            if (asset == null || shader == null)
            {
                Debug.LogWarning("Hongdal diorama: mesh or shader resource is missing");
                return;
            }

            try
            {
                using var reader = new BinaryReader(new MemoryStream(asset.bytes));
                var magic = reader.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'H' || magic[1] != 'D' || magic[2] != 'I' || magic[3] != 'O' ||
                    reader.ReadInt32() != 2)
                    throw new InvalidDataException("unknown header");
                dioramaLatitude = reader.ReadDouble();
                dioramaLongitude = reader.ReadDouble();
                dioramaWest = reader.ReadDouble();
                dioramaSouth = reader.ReadDouble();
                dioramaEast = reader.ReadDouble();
                dioramaNorth = reader.ReadDouble();
                dioramaBuildings = reader.ReadInt32();
                reader.ReadInt32();

                // 바탕(지면, 도로, 실제 외곽 건물)과 그 그림자, 이어서 예시 채움 건물과 그 그림자.
                var mesh = ReadDioramaMesh(reader, "Hongdal diorama", true);
                var shadows = ReadDioramaMesh(reader, "Hongdal diorama shadows", false);
                dioramaFillBuildings = reader.ReadInt32();
                var fill = ReadDioramaMesh(reader, "Hongdal diorama example fill", true);
                var fillShadows = ReadDioramaMesh(reader, "Hongdal diorama example fill shadows", false);

                dioramaMaterial = new Material(shader) { name = "Hongdal diorama" };
                if (shadowShader != null) dioramaShadowMaterial = new Material(shadowShader) { name = "Hongdal diorama shadows" };
                dioramaRoot = new GameObject("Hongdal diorama");
                dioramaRenderers = 0;
                AddDioramaPart("Ground, roads and buildings", mesh, dioramaMaterial);
                if (dioramaShadowMaterial != null && shadows.vertexCount > 0)
                    AddDioramaPart("Shadows", shadows, dioramaShadowMaterial);
                if (fill.vertexCount > 0)
                {
                    dioramaFillParts.Add(AddDioramaPart("Example fill buildings", fill, dioramaMaterial));
                    if (dioramaShadowMaterial != null && fillShadows.vertexCount > 0)
                        dioramaFillParts.Add(AddDioramaPart("Example fill shadows", fillShadows, dioramaShadowMaterial));
                }
                else
                    dioramaFillBuildings = 0;
                dioramaRoot.SetActive(false);
                QualitySettings.antiAliasing = 4;
                Debug.Log($"Hongdal diorama: buildings {dioramaBuildings}, example fill {dioramaFillBuildings}, " +
                    $"vertices {mesh.vertexCount + fill.vertexCount}, renderers {dioramaRenderers}, " +
                    $"materials {(dioramaShadowMaterial != null ? 2 : 1)}");
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                Debug.LogWarning("Hongdal diorama: mesh could not be read. " + exception.Message);
                ReleaseDiorama();
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        private GameObject AddDioramaPart(string partName, Mesh mesh, Material material)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(dioramaRoot.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            dioramaRenderers++;
            return part;
        }

        private void ReleaseDiorama()
        {
            ReleaseManaged();
            ReleaseRealism();
            if (dioramaRoot != null) Destroy(dioramaRoot);
            foreach (var mesh in dioramaMeshes)
                if (mesh != null) Destroy(mesh);
            if (dioramaMaterial != null) Destroy(dioramaMaterial);
            if (dioramaShadowMaterial != null) Destroy(dioramaShadowMaterial);
            if (treeMaterial != null) Destroy(treeMaterial);
            if (driftMaterial != null) Destroy(driftMaterial);
            treeMaterial = null;
            driftMaterial = null;
            driftRenderers.Clear();
            driftShown = false;
            dioramaTrees = 0;
            dioramaDriftLeaves = 0;
            dioramaMeshes.Clear();
            dioramaFillParts.Clear();
            dioramaRoot = null;
            dioramaMaterial = null;
            dioramaShadowMaterial = null;
            dioramaShown = false;
        }

        // 메시 하나를 읽는다. coloured면 정점 색과 UV 두 벌이 뒤따르고, 아니면 위치와 삼각형뿐이다(그림자).
        private Mesh ReadDioramaMesh(BinaryReader reader, string meshName, bool coloured)
        {
            var count = reader.ReadInt32();
            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            dioramaMeshes.Add(mesh);
            mesh.vertices = ReadDioramaPoints(reader, count);
            if (coloured)
            {
                var colors = new Color32[count];
                for (var index = 0; index < count; index++)
                {
                    var r = reader.ReadByte();
                    var g = reader.ReadByte();
                    var b = reader.ReadByte();
                    var a = reader.ReadByte();
                    colors[index] = new Color32(r, g, b, a);
                }
                mesh.colors32 = colors;
                mesh.uv = ReadDioramaPairs(reader, count);
                mesh.uv2 = ReadDioramaPairs(reader, count);
            }
            mesh.triangles = ReadDioramaIndices(reader, reader.ReadInt32());
            mesh.RecalculateBounds();
            return mesh;
        }

        // 2.5D에서는 화면 한가운데가 자료 범위 안쪽에 머물도록 이동량을 붙잡는다.
        // 끌거나 확대하다가 범위를 벗어나 평면 지도로 갑자기 바뀌는 일을 막는다.
        private Vector2 ClampDioramaOffset(Vector2 offset, float zoom)
        {
            if (!DioramaVisible || zoom <= 0) return offset;
            var metres = (float)metresPerPixel / zoom;
            var centre = Field.size * 0.5f;
            var east = (centre.x * (1f - zoom) - offset.x) * metres;
            var north = -(centre.y * (1f - zoom) - offset.y) * metres;
            // 화면에 담기는 땅의 절반 크기(동서, 남북)를 구해, 그 60%만큼은 가장자리에서 떨어져 있게 한다.
            // 멀리 볼수록 더 안쪽에서 멈추므로 자료가 끝나는 빈 땅이 화면을 크게 차지하지 않는다.
            var (halfEast, halfNorth) = dioramaLens.GroundSpan(Field.width * 0.5f * metres, Field.height * 0.5f * metres);
            var insetEast = Mathf.Max(DioramaInsetMetres, 0.6f * (float)halfEast);
            var insetNorth = Mathf.Max(DioramaInsetMetres, 0.6f * (float)halfNorth);
            var minEast = (float)((dioramaWest - centerLongitude) * longitudeScale) + insetEast;
            var maxEast = (float)((dioramaEast - centerLongitude) * longitudeScale) - insetEast;
            var minNorth = (float)((dioramaSouth - centerLatitude) * 111200) + insetNorth;
            var maxNorth = (float)((dioramaNorth - centerLatitude) * 111200) - insetNorth;
            east = minEast <= maxEast ? Mathf.Clamp(east, minEast, maxEast) : (minEast + maxEast) * 0.5f;
            north = minNorth <= maxNorth ? Mathf.Clamp(north, minNorth, maxNorth) : (minNorth + maxNorth) * 0.5f;
            return new Vector2(centre.x * (1f - zoom) - east / metres, centre.y * (1f - zoom) + north / metres);
        }

        private static Vector3[] ReadDioramaPoints(BinaryReader reader, int count)
        {
            if (count < 0 || count > 4000000) throw new InvalidDataException("vertex count");
            var points = new Vector3[count];
            for (var index = 0; index < count; index++)
            {
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var z = reader.ReadSingle();
                points[index] = new Vector3(x, y, z);
            }
            return points;
        }

        private static Vector2[] ReadDioramaPairs(BinaryReader reader, int count)
        {
            var pairs = new Vector2[count];
            for (var index = 0; index < count; index++)
            {
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                pairs[index] = new Vector2(x, y);
            }
            return pairs;
        }

        private static int[] ReadDioramaIndices(BinaryReader reader, int count)
        {
            if (count < 0 || count > 12000000) throw new InvalidDataException("index count");
            var indices = new int[count];
            for (var index = 0; index < count; index++) indices[index] = reader.ReadInt32();
            return indices;
        }
    }
}
