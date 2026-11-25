using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.WindowsGame
{
    // 공개판 행정동 디오라마 보기 (2026-11-25, 사용자 11:22: 「타일 두 번 누르면 디오라마로 … 폰에서도 … 수도권부터 전국으로」).
    // 굽기: make_public_dong.py — GIS건물통합정보 · 표준노드링크 · ESA WorldCover · vuski/admdongkor만 쓴다(주소 자료 없음, 재배포 가능).
    // 메시는 기존 동 메시와 같은 HDIO v2(HDIZ로 싼 것)라 같은 셰이더(HongdalDiorama)로 그린다. 원점은 그 동의 가운데.
    // 기존 동 모듈(내부 검토용, SagajeongDispatchDongModules.cs)과 따로 논다: 한 번에 한 동만 읽고, 닫으면 다 내린다.
    // 찾는 곳(앞이 먼저): ① 내려받은 것 persistentDataPath/pdong/ ② 실행 인자 --hongdal-pdong-dir=… 또는 환경 변수 HONGDAL_PDONG_DIR
    //                    ③ 앱에 함께 실은 것 Resources/HongdalPublicDong/. 없으면 「아직 이 판에 없음」을 알린다(전국은 내려받기로 넓힌다).
    // 화면: 지도 대신 동 하나를 가득. 끌기 = 옮기기, 오른쪽 끌기 · 두 손가락 비틀기 = 돌리기, 휠 · 오므리기 = 가깝게, 두 손가락 위아래 = 기울기.
    public sealed partial class SagajeongDispatchView
    {
        private const string PublicDongResourceFolder = "HongdalPublicDong/";
        private const string PublicDongAttribution = "건물 국토교통부 GIS건물통합정보 · 길 국토교통부 표준노드링크 · 녹지 © ESA WorldCover CC BY 4.0 · 경계 통계청 SGIS · vuski/admdongkor";

        private GameObject pdongRoot;
        private readonly List<Mesh> pdongMeshes = new();
        private Material pdongMaterial, pdongShadowMaterial;
        private string pdongCode, pdongName, pdongDistrict, pdongSource;
        private int pdongBuildings, pdongTrees;
        private float pdongExtent = 500f;
        private float pdYaw = 25f, pdPitch = 52f, pdDistance = 900f;
        private Vector3 pdTarget;
        private Color pdPreviousBackground;
        private bool pdPreviousOrthographic;
        private float pdGuiScale = 1f;
        private Vector2 pdLastPointer;
        private bool pdPanning, pdTurning;
        private float pdPinchDistance, pdPinchAngle, pdPinchMiddleY;
        private int pdTouchCount;
        private readonly List<Rect> pdUiRects = new();
        private float pdOpenedAt;

        private bool PublicDongOpen => pdongRoot != null;

        // 처음 거리: 동 전체가 들어오게. 세로 화면은 가로 폭이 좁아 더 물러선다.
        private float PublicDongHomeDistance => pdongExtent * 2.6f * Mathf.Max(1f, 0.75f / Mathf.Max(0.2f, Screen.width / (float)Mathf.Max(1, Screen.height)));

        private static string PublicDongDirectory()
        {
            var argument = Environment.GetCommandLineArgs().FirstOrDefault(item => item.StartsWith("--hongdal-pdong-dir=", StringComparison.Ordinal));
            if (argument != null) return argument.Substring("--hongdal-pdong-dir=".Length).Trim('"');
            var environment = Environment.GetEnvironmentVariable("HONGDAL_PDONG_DIR");
            return string.IsNullOrEmpty(environment) ? null : environment;
        }

        private static byte[] FindPublicDongBytes(string code, out string source)
        {
            var file = $"hongdal-pdong-{code}.bytes";
            try
            {
                var downloaded = Path.Combine(Application.persistentDataPath, "pdong", file);
                if (File.Exists(downloaded)) { source = "내려받은 것"; return File.ReadAllBytes(downloaded); }
                var directory = PublicDongDirectory();
                if (!string.IsNullOrEmpty(directory) && File.Exists(Path.Combine(directory, file)))
                {
                    source = "PC 굽기 폴더";
                    return File.ReadAllBytes(Path.Combine(directory, file));
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Hongdal public dong: file read failed. " + exception.Message);
            }
            var asset = Resources.Load<TextAsset>(PublicDongResourceFolder + $"hongdal-pdong-{code}");
            if (asset != null)
            {
                var bytes = asset.bytes;
                Resources.UnloadAsset(asset);
                source = "앱에 실은 것";
                return bytes;
            }
            source = null;
            return null;
        }

        // 동 하나를 연다. 못 열면 지도에 한 줄로 알리고 false.
        private bool OpenPublicDong(string code, string name, string district)
        {
            if (string.IsNullOrEmpty(code)) return false;
            var bytes = FindPublicDongBytes(code, out var source);
            if (bytes == null)
            {
                GuSay($"{name} · 디오라마는 구워 두었지만 이 판에는 아직 싣지 않았습니다");
                return false;
            }
            var shader = Resources.Load<Shader>("HongdalDiorama");
            var shadowShader = Resources.Load<Shader>("HongdalDioramaShadow");
            if (shader == null)
            {
                GuSay("디오라마 셰이더가 없습니다");
                return false;
            }
            ClosePublicDong();
            try
            {
                if (bytes.Length >= 12 && bytes[0] == 'H' && bytes[1] == 'D' && bytes[2] == 'I' && bytes[3] == 'Z')
                    bytes = 행정동모듈Policy.UnpackModule(bytes, BitConverter.ToInt32(bytes, 8), null);      // 머리의 길이 · 해시로 확인
                using var reader = new BinaryReader(new MemoryStream(bytes));
                var magic = reader.ReadBytes(4);
                if (magic.Length != 4 || magic[0] != 'H' || magic[1] != 'D' || magic[2] != 'I' || magic[3] != 'O' || reader.ReadInt32() != 2)
                    throw new InvalidDataException("unknown header");
                for (var k = 0; k < 6; k++) reader.ReadDouble();      // 원점 · 범위(메시는 원점 기준 미터)
                pdongBuildings = reader.ReadInt32();
                reader.ReadInt32();
                var mesh = ReadPublicDongMesh(reader, "Public dong " + code, true);
                var shadows = ReadPublicDongMesh(reader, "Public dong shadows " + code, false);

                pdongMaterial = new Material(shader) { name = "Hongdal public dong" };
                if (shadowShader != null) pdongShadowMaterial = new Material(shadowShader) { name = "Hongdal public dong shadows" };
                pdongRoot = new GameObject("Hongdal public dong " + code);
                AddPublicDongPart("Ground, roads and buildings", mesh, pdongMaterial);
                if (pdongShadowMaterial != null && shadows.vertexCount > 0) AddPublicDongPart("Shadows", shadows, pdongShadowMaterial);
                pdongTrees = 0;
                try
                {
                    if (EnsureTreeMaterial() != null)
                    {
                        var before = dioramaTrees;
                        var trees = BuildDongTrees(mesh, code, out var drifting);      // 나무(표현, SagajeongDispatchTrees.cs): 녹지 갈래 1 · 2 위에
                        if (trees != null) { pdongMeshes.Add(trees); AddPublicDongPart("Trees", trees, treeMaterial); }
                        if (drifting != null) Destroy(drifting);
                        pdongTrees = dioramaTrees - before;
                        dioramaTrees = before;
                    }
                }
                catch (Exception exception) when (exception is ArgumentException || exception is IndexOutOfRangeException || exception is NullReferenceException)
                {
                    Debug.LogWarning("Hongdal public dong: trees skipped. " + exception.Message);
                }
                var bounds = mesh.bounds;
                pdongExtent = Mathf.Max(200f, Mathf.Max(bounds.extents.x, bounds.extents.z));
                pdTarget = new Vector3(bounds.center.x, 0f, bounds.center.z);
                pdYaw = 25f;
                pdPitch = 52f;
                pdDistance = PublicDongHomeDistance;
                pdongCode = code;
                pdongName = name;
                pdongDistrict = district;
                pdongSource = source;
                pdOpenedAt = Time.unscaledTime;
                var camera = dioramaCamera != null ? dioramaCamera : Camera.main;
                if (camera != null)
                {
                    pdPreviousBackground = camera.backgroundColor;
                    pdPreviousOrthographic = camera.orthographic;
                }
                if (dioramaRoot != null && dioramaRoot.activeSelf) dioramaRoot.SetActive(false);
                QualitySettings.antiAliasing = 4;
                chLastTap = -1;
                hexLastClickTile = -1;
                Debug.Log($"Hongdal public dong {code} {name}: {source}, buildings {pdongBuildings}, trees {pdongTrees}, vertices {mesh.vertexCount}");
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is InvalidDataException)
            {
                Debug.LogWarning($"Hongdal public dong {code}: could not be read. {exception.Message}");
                ClosePublicDong();
                GuSay($"{name} 디오라마를 읽지 못했습니다 · 지도는 그대로예요");
                return false;
            }
        }

        private Mesh ReadPublicDongMesh(BinaryReader reader, string meshName, bool coloured)
        {
            var count = reader.ReadInt32();
            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            pdongMeshes.Add(mesh);
            mesh.vertices = ReadDioramaPoints(reader, count);
            if (coloured)
            {
                var colours = new Color32[count];
                var raw = reader.ReadBytes(count * 4);
                for (var index = 0; index < count; index++)
                    colours[index] = new Color32(raw[index * 4], raw[index * 4 + 1], raw[index * 4 + 2], raw[index * 4 + 3]);
                mesh.colors32 = colours;
                mesh.uv = ReadDioramaPairs(reader, count);
                mesh.uv2 = ReadDioramaPairs(reader, count);
            }
            mesh.triangles = ReadDioramaIndices(reader, reader.ReadInt32());
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddPublicDongPart(string partName, Mesh mesh, Material material)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(pdongRoot.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void ClosePublicDong()
        {
            if (pdongRoot != null) Destroy(pdongRoot);
            pdongRoot = null;
            foreach (var mesh in pdongMeshes) if (mesh != null) Destroy(mesh);
            pdongMeshes.Clear();
            if (pdongMaterial != null) Destroy(pdongMaterial);
            if (pdongShadowMaterial != null) Destroy(pdongShadowMaterial);
            pdongMaterial = null;
            pdongShadowMaterial = null;
            var camera = dioramaCamera != null ? dioramaCamera : Camera.main;
            if (camera != null && pdongCode != null)
            {
                camera.backgroundColor = pdPreviousBackground;
                camera.orthographic = pdPreviousOrthographic;
            }
            pdongCode = null;
            pdPanning = pdTurning = false;
            pdTouchCount = 0;
        }

        // LateUpdate(SagajeongDispatchDiorama.cs)가 맨 앞에서 부른다. 열려 있으면 카메라를 맡고 true.
        private bool SyncPublicDong(Camera camera)
        {
            if (!PublicDongOpen || camera == null) return false;
            if (dioramaRoot != null && dioramaRoot.activeSelf) dioramaRoot.SetActive(false);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClosePublicDong();
                return true;
            }
            HandlePublicDongInput();
            pdPitch = Mathf.Clamp(pdPitch, 18f, 89f);
            pdDistance = Mathf.Clamp(pdDistance, 60f, pdongExtent * 5f);
            var limit = pdongExtent * 1.2f;
            pdTarget.x = Mathf.Clamp(pdTarget.x, -limit, limit);
            pdTarget.z = Mathf.Clamp(pdTarget.z, -limit, limit);
            var rotation = Quaternion.Euler(pdPitch, pdYaw, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = Mathf.Max(0.5f, pdDistance * 0.02f);
            camera.farClipPlane = pdDistance * 4f + pdongExtent * 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DongBackdrop;
            camera.transform.SetPositionAndRotation(pdTarget - rotation * Vector3.forward * pdDistance, rotation);
            return true;
        }

        private Vector2 PublicDongLogical(Vector2 screen) => new(screen.x / pdGuiScale, (Screen.height - screen.y) / pdGuiScale);

        private bool PublicDongOverUi(Vector2 screen)
        {
            var point = PublicDongLogical(screen);
            foreach (var rect in pdUiRects) if (rect.Contains(point)) return true;
            return false;
        }

        private void HandlePublicDongInput()
        {
            var perPixel = pdDistance * 0.0011f * (1280f / Mathf.Max(1, Screen.width));
            var flatYaw = Quaternion.Euler(0f, pdYaw, 0f);
            if (Input.touchCount > 0)
            {
                var count = Input.touchCount;
                if (count != pdTouchCount)
                {
                    pdTouchCount = count;
                    pdPanning = false;
                    if (count >= 2)
                    {
                        var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                        pdPinchDistance = Vector2.Distance(a, b);
                        pdPinchAngle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                        pdPinchMiddleY = (a.y + b.y) * 0.5f;
                    }
                    else if (count == 1)
                    {
                        pdLastPointer = Input.GetTouch(0).position;
                        pdPanning = !PublicDongOverUi(pdLastPointer);
                    }
                    return;
                }
                if (count == 1 && pdPanning)
                {
                    var p = Input.GetTouch(0).position;
                    var d = p - pdLastPointer;
                    pdLastPointer = p;
                    pdTarget -= flatYaw * new Vector3(d.x, 0f, d.y / Mathf.Max(0.35f, Mathf.Sin(pdPitch * Mathf.Deg2Rad))) * perPixel;
                }
                else if (count >= 2)
                {
                    var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position;
                    var distance = Vector2.Distance(a, b);
                    var angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    var middleY = (a.y + b.y) * 0.5f;
                    if (pdPinchDistance > 1f && distance > 1f) pdDistance *= pdPinchDistance / distance;
                    pdYaw -= Mathf.DeltaAngle(pdPinchAngle, angle);
                    pdPitch += (middleY - pdPinchMiddleY) * 0.15f;
                    pdPinchDistance = distance; pdPinchAngle = angle; pdPinchMiddleY = middleY;
                }
                return;
            }
            pdTouchCount = 0;
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { pdPanning = !PublicDongOverUi(mouse); pdLastPointer = mouse; }
            if (Input.GetMouseButtonDown(1)) { pdTurning = !PublicDongOverUi(mouse); pdLastPointer = mouse; }
            if (!Input.GetMouseButton(0)) pdPanning = false;
            if (!Input.GetMouseButton(1)) pdTurning = false;
            var delta = mouse - pdLastPointer;
            pdLastPointer = mouse;
            if (pdPanning) pdTarget -= flatYaw * new Vector3(delta.x, 0f, delta.y / Mathf.Max(0.35f, Mathf.Sin(pdPitch * Mathf.Deg2Rad))) * perPixel;
            if (pdTurning) { pdYaw += delta.x * 0.25f; pdPitch -= delta.y * 0.2f; }
            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !PublicDongOverUi(mouse)) pdDistance *= Mathf.Pow(0.88f, wheel);
        }

        // OnGUI가 맨 앞에서 부른다: 지도 대신 디오라마 위의 얇은 띠와 단추만.
        private void DrawPublicDongScreen()
        {
            var logicalWidth = PortraitScreen ? PortraitWidth : (float)Width;
            pdGuiScale = Screen.width / logicalWidth;
            if (pdGuiScale <= 0f) return;
            var logicalHeight = Screen.height / pdGuiScale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(pdGuiScale, pdGuiScale, 1f));
            pdUiRects.Clear();
            var safeTop = PortraitScreen ? portraitTopInset : 0f;
            var safeBottom = PortraitScreen ? portraitBottomInset : 0f;
            var bar = new Rect(0f, 0f, logicalWidth, safeTop + 58f);
            pdUiRects.Add(bar);
            Paint(bar, Fade(Canvas, 0.86f));
            var back = new Rect(10f, safeTop + 11f, 92f, 36f);
            Paint(back, Mint, 8);
            Text(back, "◂ 지도로", 13, Canvas, true, TextAnchor.MiddleCenter);
            Text(new Rect(112f, safeTop + 6f, logicalWidth - 230f, 26f), pdongName ?? "", 18, Paper, true);
            Text(new Rect(112f, safeTop + 31f, logicalWidth - 230f, 18f), PortraitScreen ? $"{pdongDistrict} · 건물 {pdongBuildings:N0}채" : $"{pdongDistrict} · 건물 {pdongBuildings:N0}채 · 공개판 디오라마", 11, Soft);
            var reset = new Rect(logicalWidth - 108f, safeTop + 11f, 98f, 36f);
            Paint(reset, Tile, 8);
            Text(reset, "처음 보기", 12, Paper, true, TextAnchor.MiddleCenter);
            var foot = new Rect(0f, logicalHeight - safeBottom - (PortraitScreen ? 52f : 40f), logicalWidth, (PortraitScreen ? 52f : 40f) + safeBottom);
            pdUiRects.Add(foot);
            Paint(foot, Fade(Canvas, 0.72f));
            var hint = Application.isMobilePlatform ? "끌어서 옮기기 · 두 손가락: 오므려 가깝게 · 비틀어 돌리기 · 위아래로 기울이기"
                : "왼쪽 끌기 옮기기 · 오른쪽 끌기 돌리기 · 휠 가깝게 · Esc 지도로";
            Text(new Rect(10f, foot.y + 4f, logicalWidth - 20f, 16f), hint, 10, Fade(Paper, 0.85f));
            Text(new Rect(10f, foot.y + (PortraitScreen ? 21f : 21f), logicalWidth - 20f, PortraitScreen ? 28f : 15f), PublicDongAttribution, 8, Fade(Soft, 0.85f), false, TextAnchor.UpperLeft, PortraitScreen);
            var current = Event.current;
            if (current.type == EventType.MouseUp && current.button == 0)
            {
                if (back.Contains(current.mousePosition))
                {
                    current.Use();
                    ClosePublicDong();
                }
                else if (reset.Contains(current.mousePosition))
                {
                    current.Use();
                    pdYaw = 25f; pdPitch = 52f; pdDistance = PublicDongHomeDistance; pdTarget = Vector3.zero;
                }
            }
            GUI.matrix = previous;
        }
    }
}
