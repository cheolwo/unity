using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// Synty 조합물을 일반 Game View와 분리된 임시 촬영 Stage에서 렌더링하고,
    /// 서버의 불변 촬영 영수증을 거쳐 Web 검토 원장에 등록한다.
    /// 이 도구는 Scene 적용 승인이나 E5 증거를 만들지 않는다.
    /// </summary>
    public static class Synty공간조립Web검토CapturePipeline
    {
        public const string BatchStableId = "review-batch:synty-power-plant-one-card.r1";
        public const string ReviewItemStableId = "review-item:nature-recovery-plant-a-normal.r1";
        public const string CompositionStableId = "composition:nature-recovery-plant-a.r1";
        public const string H1StableId = "h1-action:nature-recovery-plant-core.r1";
        public const string H2StableId = "h2-composition:nature-restoration-recovery.r1";
        public const string H3StableId = "h3-candidate:nature-threat-recovery";
        public const string RenderingProfileId = "rendering-profile:synty-web-review-normal.r1";
        public const string RenderingProfileRevision = "r1";
        public const string BatchSchemaVersion = "synty-composition-review-batch.v2";
        public const string NeedsRevisionStateCode = "NeedsRevision";
        public const int CaptureWidth = 1600;
        public const int CaptureHeight = 900;
        public const int ReviewOnlyLayer = 31;

        private const string DefaultApiBaseUrl = "https://localhost:7117/";
        private const string ApiBaseUrlEnvironmentName = "SSALDDEL_OPERATIONAL_API_BASE_URL";
        private const string AccessTokenEnvironmentName = "SSALDDEL_UNITY_ADMIN_ACCESS_TOKEN";
        internal const string ApiRoute = "api/v1/platform/world-composition-reviews";
        private const string LocalEvidenceRelativePath = "artifacts/local/synty-web-review";
        private const string PlanRevision = "synty-five-pack-h1-h3-assembly-plan.r1";

        private static readonly 배치항목[] Placements =
        {
            new("Construction", "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Generator_Large_01.prefab", 0f, 0f, 0f, 0f, 1f),
            new("Construction", "Assets/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_WaterTower_01.prefab", -6f, 0f, 4f, 15f, .78f),
            new("Nature", "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Willow_Medium_01.prefab", -8f, 0f, -4f, -18f, .9f),
            new("Nature", "Assets/Synty/PolygonNature/Prefabs/Plants/SM_Plant_FlowerPatch_01.prefab", -3f, 0f, -3f, 0f, 1.4f),
            new("Farm", "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Windmill_01.prefab", 7f, 0f, 5f, -20f, .72f),
            new("Farm", "Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_Silo_Small_01.prefab", 8f, 0f, -4f, 12f, .8f),
            new("Town", "Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_ParkBench_01.prefab", -4f, 0f, -7f, 12f, 1f),
            new("Town", "Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_Outdoor_Light_01.prefab", 0f, 0f, -7f, 0f, 1f),
            new("City", "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PowerBox_01.prefab", 4f, 0f, -3f, -12f, 1f),
            new("City", "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Power_Cables_01.prefab", 3f, 0f, 2f, 0f, 1f),
        };

        private static readonly 촬영시점[] Views =
        {
            new("FrontThreeQuarter", "정면 3/4", new Vector3(1f, .72f, -1f)),
            new("RearThreeQuarter", "후면 3/4", new Vector3(-1f, .68f, 1f)),
            new("LeftSide", "좌측면", new Vector3(-1f, .55f, -.08f)),
            new("TopOblique", "상부 사선", new Vector3(.75f, 1.55f, -.75f)),
        };

        public static IReadOnlyList<string> RequiredPrefabPaths
            => Placements.Select(value => value.PrefabPath).ToArray();

        public static string SourceCompositionHash => ComputeSourceCompositionHash();

        public static string RenderingProfileHash => Sha256(string.Join("|",
            RenderingProfileId,
            RenderingProfileRevision,
            CaptureWidth.ToString(CultureInfo.InvariantCulture),
            CaptureHeight.ToString(CultureInfo.InvariantCulture),
            ReviewOnlyLayer.ToString(CultureInfo.InvariantCulture),
            "transparent=false",
            "ui=review-metadata-on-web-only",
            "camera=four-fixed-bounds-relative-views"));

        [MenuItem("Ssalddel/Synty Web 검토/01 회복 발전소 A Normal · 4시점 로컬 촬영 _F10")]
        public static void CaptureOneCardMenu()
        {
            var bundle = CaptureOneCard(string.Empty, 0);
            Debug.Log($"Synty Web 검토용 4시점 촬영 완료: {bundle.OutputFolder}\n"
                      + $"CaptureBundleHash={bundle.CaptureBundleHash}");
            EditorUtility.RevealInFinder(bundle.OutputFolder);
        }

        [MenuItem("Ssalddel/Synty Web 검토/02 회복 발전소 A Normal · 최초 업로드 및 등록")]
        public static async void UploadInitialOneCardMenu()
        {
            try
            {
                await CaptureUploadAndRegisterAsync(string.Empty, 0);
                Debug.Log("Synty Web 검토 최초 1카드가 ReadyForReview로 등록되었습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Ssalddel/Synty Web 검토/03 NeedsRevision 1카드 · 재촬영 및 등록")]
        public static async void RecaptureNeedsRevisionOneCardMenu()
        {
            try
            {
                var api = CreateApiClient();
                var pending = await api.GetNeedsRevisionItemAsync(ReviewItemStableId);
                if (pending == null)
                    throw new InvalidOperationException("NeedsRevision 상태인 회복 발전소 A Normal 검토 항목이 없습니다.");
                if (!string.Equals(pending.composition.compositionInputHash,
                        SourceCompositionHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "원본 조립 입력이 바뀌어 재촬영으로 되살릴 수 없습니다. 서버에서 Stale 갱신을 먼저 처리해야 합니다.");
                }

                await CaptureUploadAndRegisterAsync(
                    pending.composition.captureBundleHash,
                    pending.revision,
                    api);
                Debug.Log("NeedsRevision 1카드의 부모 촬영 묶음과 revision을 확인하고 재등록했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static SyntyWeb검토CaptureBundle CaptureOneCard(
            string parentCaptureBundleHash,
            long expectedReviewItemRevision)
        {
            ValidateLineageInput(parentCaptureBundleHash, expectedReviewItemRevision);
            ValidateRequiredPrefabs();
            var capturedAtUtc = DateTime.UtcNow;
            var outputFolder = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                LocalEvidenceRelativePath,
                capturedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(outputFolder);

            var previousActiveScene = SceneManager.GetActiveScene();
            var captureScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            SceneManager.SetActiveScene(captureScene);
            var temporaryMaterials = new List<Material>();
            try
            {
                var stage = BuildReviewOnlyStage(captureScene, temporaryMaterials);
                ValidateReviewOnlyStage(stage.CompositionRoot, stage.Camera);
                var bounds = CalculateBounds(stage.CompositionRoot);
                var captures = new List<SyntyWeb검토LocalCapture>(Views.Length);
                foreach (var view in Views)
                {
                    PositionCamera(stage.Camera, bounds, view.Direction);
                    var fileName = view.ViewCode + ".png";
                    var filePath = Path.Combine(outputFolder, fileName);
                    CapturePng(stage.Camera, filePath);
                    var bytes = File.ReadAllBytes(filePath);
                    captures.Add(new SyntyWeb검토LocalCapture
                    {
                        CaptureStableId = $"capture:nature-recovery-a-normal:{view.ViewCode.ToLowerInvariant()}.r1",
                        ViewCode = view.ViewCode,
                        DisplayName = view.DisplayName,
                        FileName = fileName,
                        FilePath = filePath,
                        ImageSha256 = Sha256(bytes),
                        Width = CaptureWidth,
                        Height = CaptureHeight,
                    });
                }
                if (captures.Select(value => value.ImageSha256)
                    .Distinct(StringComparer.Ordinal).Count() != Views.Length)
                {
                    throw new InvalidOperationException(
                        "CaptureViewsAreNotDistinct: URP가 빈 frame 또는 같은 시점을 반복 저장했습니다.");
                }

                var captureBundleHash = Sha256(string.Join("\n",
                    SourceCompositionHash,
                    RenderingProfileHash,
                    parentCaptureBundleHash,
                    capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    string.Join("\n", captures.OrderBy(value => value.ViewCode, StringComparer.Ordinal)
                        .Select(value => value.ViewCode + "=" + value.ImageSha256))));
                var bundle = new SyntyWeb검토CaptureBundle
                {
                    SchemaVersion = BatchSchemaVersion,
                    BatchStableId = BatchStableId,
                    ReviewItemStableId = ReviewItemStableId,
                    SourceCompositionHash = SourceCompositionHash,
                    PlanHash = Sha256(PlanRevision),
                    RenderingProfileHash = RenderingProfileHash,
                    ParentCaptureBundleHash = parentCaptureBundleHash,
                    CaptureBundleHash = captureBundleHash,
                    ExpectedReviewItemRevision = expectedReviewItemRevision,
                    CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    OutputFolder = outputFolder,
                    Captures = captures,
                };
                File.WriteAllText(
                    Path.Combine(outputFolder, "capture-manifest.json"),
                    JsonUtility.ToJson(bundle, true),
                    new UTF8Encoding(false));
                return bundle;
            }
            finally
            {
                foreach (var material in temporaryMaterials)
                    Object.DestroyImmediate(material);
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                EditorSceneManager.CloseScene(captureScene, true);
            }
        }

        public static string ComputeSourceCompositionHash()
        {
            var lines = new List<string>
            {
                CompositionStableId,
                H1StableId,
                H2StableId,
                H3StableId,
                "variant=A",
                "state=Normal",
            };
            foreach (var placement in Placements)
            {
                var guid = AssetDatabase.AssetPathToGUID(placement.PrefabPath);
                lines.Add(string.Join("|",
                    placement.PackCode,
                    placement.PrefabPath,
                    guid,
                    Number(placement.X), Number(placement.Y), Number(placement.Z),
                    Number(placement.Yaw), Number(placement.Scale)));
            }

            return Sha256(string.Join("\n", lines));
        }

        public static string Sha256(string value)
            => Sha256(Encoding.UTF8.GetBytes(value ?? string.Empty));

        public static string Sha256(byte[] value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(value))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static async Task CaptureUploadAndRegisterAsync(
            string parentCaptureBundleHash,
            long expectedReviewItemRevision,
            Synty공간조립Web검토ApiClient api = null)
        {
            api ??= CreateApiClient();
            var bundle = CaptureOneCard(parentCaptureBundleHash, expectedReviewItemRevision);
            var receipts = new List<SyntyWeb검토UploadResponse>(bundle.Captures.Count);
            foreach (var capture in bundle.Captures)
                receipts.Add(await api.UploadCaptureAsync(bundle, capture));
            await api.RegisterBatchAsync(bundle, receipts);
        }

        private static Synty공간조립Web검토ApiClient CreateApiClient()
        {
            var baseUrl = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentName);
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = DefaultApiBaseUrl;
            var token = Environment.GetEnvironmentVariable(AccessTokenEnvironmentName);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    $"관리자 access token이 없습니다. Unity를 시작하기 전에 {AccessTokenEnvironmentName} 환경 변수에 설정하세요.");
            }

            return new Synty공간조립Web검토ApiClient(baseUrl, token);
        }

        private static 촬영Stage BuildReviewOnlyStage(Scene scene, ICollection<Material> temporaryMaterials)
        {
            var root = NewSceneObject("SyntyWeb검토CaptureStage_PresentationOnly", scene).transform;
            var compositionRoot = NewSceneObject("VisualRoot_조합물전용", scene).transform;
            compositionRoot.SetParent(root, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.name = "회복발전소_고정바닥_40m";
            ground.transform.SetParent(compositionRoot, false);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var groundMaterial = new Material(
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMaterial.name = "임시_검토바닥";
            groundMaterial.color = new Color(.19f, .27f, .18f, 1f);
            temporaryMaterials.Add(groundMaterial);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            foreach (var placement in Placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(placement.PrefabPath)
                             ?? throw new InvalidOperationException("SyntyPrefabMissing:" + placement.PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = placement.PackCode + "_" + prefab.name;
                instance.transform.SetParent(compositionRoot, false);
                instance.transform.localPosition = new Vector3(placement.X, placement.Y, placement.Z);
                instance.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                instance.transform.localScale = Vector3.one * placement.Scale;
            }

            SetLayerRecursively(compositionRoot.gameObject, ReviewOnlyLayer);

            var cameraObject = NewSceneObject("CaptureCamera_조합물전용", scene);
            cameraObject.transform.SetParent(root, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << ReviewOnlyLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.58f, .72f, .8f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var sunObject = NewSceneObject("CaptureLight_주광", scene);
            sunObject.transform.SetParent(root, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, .94f, .84f, 1f);
            sun.cullingMask = 1 << ReviewOnlyLayer;

            var fillObject = NewSceneObject("CaptureLight_보조", scene);
            fillObject.transform.SetParent(root, false);
            fillObject.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = .55f;
            fill.color = new Color(.72f, .82f, 1f, 1f);
            fill.cullingMask = 1 << ReviewOnlyLayer;

            return new 촬영Stage(compositionRoot, camera);
        }

        private static void ValidateReviewOnlyStage(Transform compositionRoot, Camera camera)
        {
            if (camera.cullingMask != 1 << ReviewOnlyLayer)
                throw new InvalidOperationException("CaptureCameraLayerMaskInvalid");
            if (compositionRoot.GetComponentsInChildren<Canvas>(true).Length != 0)
                throw new InvalidOperationException("CaptureStageMustNotContainApplicationCanvas");
            if (compositionRoot.GetComponentsInChildren<Camera>(true).Length != 0)
                throw new InvalidOperationException("CompositionRootMustNotContainCamera");
            if (compositionRoot.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.gameObject.layer != ReviewOnlyLayer))
            {
                throw new InvalidOperationException("CaptureStageRendererLayerLeakDetected");
            }
        }

        private static Bounds CalculateBounds(Transform compositionRoot)
        {
            var renderers = compositionRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("CaptureStageRendererMissing");
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void PositionCamera(Camera camera, Bounds bounds, Vector3 direction)
        {
            var target = bounds.center + Vector3.up * Mathf.Max(.5f, bounds.extents.y * .08f);
            var radius = Mathf.Max(12f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            var distance = Mathf.Max(radius * 2.45f, bounds.extents.y * 2.3f);
            camera.transform.position = target + direction.normalized * distance;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        }

        private static void CapturePng(Camera camera, string filePath)
        {
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                // 새 Additive Scene에서 URP가 첫 수동 render에 파이프라인 상태만 준비하는
                // 경우가 있어 한 번 예열한 뒤 실제 검토 frame을 읽는다.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(filePath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }

        private static void ValidateRequiredPrefabs()
        {
            var missing = Placements
                .Where(value => AssetDatabase.LoadAssetAtPath<GameObject>(value.PrefabPath) == null)
                .Select(value => value.PrefabPath)
                .ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException("SyntyRequiredPrefabsMissing:\n" + string.Join("\n", missing));
            if (Placements.Select(value => value.PackCode).Distinct(StringComparer.Ordinal).Count() != 5)
                throw new InvalidOperationException("FivePackCoverageInvalid");
        }

        private static void ValidateLineageInput(string parentCaptureBundleHash, long expectedRevision)
        {
            if (expectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (expectedRevision == 0 && !string.IsNullOrEmpty(parentCaptureBundleHash))
                throw new ArgumentException("최초 촬영에는 부모 CaptureBundleHash가 없어야 합니다.");
            if (expectedRevision > 0 && !IsSha256(parentCaptureBundleHash))
                throw new ArgumentException("재촬영에는 부모 CaptureBundleHash가 필요합니다.");
        }

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static GameObject NewSceneObject(string name, Scene scene)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static string Number(float value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);

        private readonly struct 배치항목
        {
            public 배치항목(string packCode, string prefabPath,
                float x, float y, float z, float yaw, float scale)
            {
                PackCode = packCode;
                PrefabPath = prefabPath;
                X = x;
                Y = y;
                Z = z;
                Yaw = yaw;
                Scale = scale;
            }

            public string PackCode { get; }
            public string PrefabPath { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public float Yaw { get; }
            public float Scale { get; }
        }

        private readonly struct 촬영시점
        {
            public 촬영시점(string viewCode, string displayName, Vector3 direction)
            {
                ViewCode = viewCode;
                DisplayName = displayName;
                Direction = direction;
            }

            public string ViewCode { get; }
            public string DisplayName { get; }
            public Vector3 Direction { get; }
        }

        private readonly struct 촬영Stage
        {
            public 촬영Stage(Transform compositionRoot, Camera camera)
            {
                CompositionRoot = compositionRoot;
                Camera = camera;
            }

            public Transform CompositionRoot { get; }
            public Camera Camera { get; }
        }
    }

    [Serializable]
    public sealed class SyntyWeb검토CaptureBundle
    {
        public string SchemaVersion;
        public string BatchStableId;
        public string ReviewItemStableId;
        public string SourceCompositionHash;
        public string PlanHash;
        public string RenderingProfileHash;
        public string ParentCaptureBundleHash;
        public string CaptureBundleHash;
        public long ExpectedReviewItemRevision;
        public string CapturedAtUtc;
        public string OutputFolder;
        public List<SyntyWeb검토LocalCapture> Captures = new();
    }

    [Serializable]
    public sealed class SyntyWeb검토LocalCapture
    {
        public string CaptureStableId;
        public string ViewCode;
        public string DisplayName;
        public string FileName;
        [NonSerialized] public string FilePath;
        public string ImageSha256;
        public int Width;
        public int Height;
    }

    internal sealed class Synty공간조립Web검토ApiClient
    {
        private readonly string baseUrl;
        private readonly string accessToken;

        public Synty공간조립Web검토ApiClient(string baseUrl, string accessToken)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException("운영 API 기준 주소가 올바르지 않습니다.", nameof(baseUrl));
            }

            this.baseUrl = baseUrl.TrimEnd('/') + "/";
            this.accessToken = string.IsNullOrWhiteSpace(accessToken)
                ? throw new ArgumentException("관리자 access token이 필요합니다.", nameof(accessToken))
                : accessToken.Trim();
        }

        public async Task<SyntyWeb검토InboxItem> GetNeedsRevisionItemAsync(string reviewItemStableId)
        {
            var url = baseUrl + Synty공간조립Web검토CapturePipeline.ApiRoute
                      + "?reviewStateCode=NeedsRevision&take=50";
            using var request = UnityWebRequest.Get(url);
            Authorize(request);
            var json = await SendAsync(request);
            var response = JsonUtility.FromJson<SyntyWeb검토InboxResponse>(json)
                           ?? throw new InvalidOperationException("SyntyReviewInboxResponseInvalid");
            return response.items?.FirstOrDefault(value =>
                string.Equals(value.reviewItemStableId, reviewItemStableId, StringComparison.Ordinal));
        }

        public async Task<SyntyWeb검토UploadResponse> UploadCaptureAsync(
            SyntyWeb검토CaptureBundle bundle,
            SyntyWeb검토LocalCapture capture)
        {
            var bytes = File.ReadAllBytes(capture.FilePath);
            var sections = new List<IMultipartFormSection>
            {
                Field("BatchStableId", bundle.BatchStableId),
                Field("ReviewItemStableId", bundle.ReviewItemStableId),
                Field("CaptureStableId", capture.CaptureStableId),
                Field("ViewCode", capture.ViewCode),
                Field("CaptureBundleHash", bundle.CaptureBundleHash),
                Field("ParentCaptureBundleHash", bundle.ParentCaptureBundleHash),
                Field("SourceCompositionHash", bundle.SourceCompositionHash),
                Field("ExpectedReviewItemRevision", bundle.ExpectedReviewItemRevision.ToString(CultureInfo.InvariantCulture)),
                Field("RenderingProfileHash", bundle.RenderingProfileHash),
                Field("ImageSha256", capture.ImageSha256),
                Field("Width", capture.Width.ToString(CultureInfo.InvariantCulture)),
                Field("Height", capture.Height.ToString(CultureInfo.InvariantCulture)),
                new MultipartFormFileSection("File", bytes, capture.FileName, "image/png"),
            };
            using var request = UnityWebRequest.Post(
                baseUrl + Synty공간조립Web검토CapturePipeline.ApiRoute + "/capture-uploads",
                sections);
            Authorize(request);
            var json = await SendAsync(request);
            return JsonUtility.FromJson<SyntyWeb검토UploadResponse>(json)
                   ?? throw new InvalidOperationException("SyntyReviewUploadResponseInvalid");
        }

        public async Task RegisterBatchAsync(
            SyntyWeb검토CaptureBundle bundle,
            IReadOnlyList<SyntyWeb검토UploadResponse> receipts)
        {
            if (receipts.Count != bundle.Captures.Count)
                throw new InvalidOperationException("CaptureReceiptCountMismatch");
            var requestBody = new BatchRequest
            {
                SchemaVersion = Synty공간조립Web검토CapturePipeline.BatchSchemaVersion,
                BatchStableId = bundle.BatchStableId,
                BatchRevision = "capture-" + bundle.CaptureBundleHash[..16],
                Title = "회복 발전소 A Normal · Web 시각 검토 1카드",
                GeneratedAtUtc = bundle.CapturedAtUtc,
                Items = new List<BatchItem>
                {
                    new()
                    {
                        ExpectedRevision = bundle.ExpectedReviewItemRevision,
                        ReviewItemStableId = bundle.ReviewItemStableId,
                        CompositionStableId = Synty공간조립Web검토CapturePipeline.CompositionStableId,
                        DisplayName = "회복 발전소 A · Normal",
                        H1StableId = Synty공간조립Web검토CapturePipeline.H1StableId,
                        H2StableId = Synty공간조립Web검토CapturePipeline.H2StableId,
                        H3StableId = Synty공간조립Web검토CapturePipeline.H3StableId,
                        VariantCode = "A",
                        StateProfileCode = "Normal",
                        CompositionInputHash = bundle.SourceCompositionHash,
                        PlanHash = bundle.PlanHash,
                        RenderingProfileId = Synty공간조립Web검토CapturePipeline.RenderingProfileId,
                        RenderingProfileRevision = Synty공간조립Web검토CapturePipeline.RenderingProfileRevision,
                        RenderingProfileHash = bundle.RenderingProfileHash,
                        ParentCaptureBundleHash = bundle.ParentCaptureBundleHash,
                        CaptureBundleHash = bundle.CaptureBundleHash,
                        PackUsages = PackUsages(),
                        Captures = bundle.Captures.Select((capture, index) =>
                            ToCapture(bundle, capture, receipts[index])).ToList(),
                    },
                },
            };
            var json = JsonUtility.ToJson(requestBody);
            using var request = new UnityWebRequest(
                baseUrl + Synty공간조립Web검토CapturePipeline.ApiRoute + "/batches",
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            Authorize(request);
            await SendAsync(request);
        }

        private static CaptureDto ToCapture(
            SyntyWeb검토CaptureBundle bundle,
            SyntyWeb검토LocalCapture capture,
            SyntyWeb검토UploadResponse receipt)
        {
            if (!string.Equals(receipt.batchStableId, bundle.BatchStableId, StringComparison.Ordinal)
                || !string.Equals(receipt.reviewItemStableId, bundle.ReviewItemStableId, StringComparison.Ordinal)
                || !string.Equals(receipt.captureStableId, capture.CaptureStableId, StringComparison.Ordinal)
                || !string.Equals(receipt.viewCode, capture.ViewCode, StringComparison.Ordinal)
                || !string.Equals(receipt.captureBundleHash, bundle.CaptureBundleHash, StringComparison.Ordinal)
                || !string.Equals(receipt.parentCaptureBundleHash, bundle.ParentCaptureBundleHash, StringComparison.Ordinal)
                || !string.Equals(receipt.sourceCompositionHash, bundle.SourceCompositionHash, StringComparison.Ordinal)
                || receipt.expectedReviewItemRevision != bundle.ExpectedReviewItemRevision
                || !string.Equals(receipt.renderingProfileHash, bundle.RenderingProfileHash, StringComparison.Ordinal)
                || !string.Equals(receipt.uploadedSourceSha256, capture.ImageSha256, StringComparison.Ordinal)
                || !IsSha256(receipt.storedImageSha256))
            {
                throw new InvalidOperationException("CaptureUploadReceiptMismatch");
            }

            return new CaptureDto
            {
                CaptureStableId = capture.CaptureStableId,
                ViewCode = capture.ViewCode,
                DisplayName = capture.DisplayName,
                CaptureUploadId = receipt.captureUploadId,
                StorageProviderCode = receipt.storageProviderCode,
                ContainerName = receipt.containerName,
                ObjectName = receipt.objectName,
                ImageUrl = receipt.imageUrl,
                ImageSha256 = receipt.storedImageSha256,
                ContentType = receipt.contentType,
                ContentLength = receipt.contentLength,
                ETag = receipt.eTag,
                Width = receipt.width,
                Height = receipt.height,
            };
        }

        private static List<PackUsage> PackUsages()
            => new()
            {
                new() { PackCode = "Construction", UsagePercent = 35, RoleCode = "StructureAndRecovery" },
                new() { PackCode = "Nature", UsagePercent = 20, RoleCode = "RecoveryAtmosphere" },
                new() { PackCode = "Farm", UsagePercent = 20, RoleCode = "ProductionAndPower" },
                new() { PackCode = "Town", UsagePercent = 10, RoleCode = "RestAndLifeTrace" },
                new() { PackCode = "City", UsagePercent = 15, RoleCode = "Infrastructure" },
            };

        private static MultipartFormDataSection Field(string name, string value)
            => new(name, value ?? string.Empty, Encoding.UTF8, "text/plain");

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private void Authorize(UnityWebRequest request)
            => request.SetRequestHeader("Authorization", "Bearer " + accessToken);

        private static Task<string> SendAsync(UnityWebRequest request)
        {
            var completion = new TaskCompletionSource<string>();
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    completion.TrySetResult(request.downloadHandler?.text ?? string.Empty);
                    return;
                }

                var response = request.downloadHandler?.text ?? string.Empty;
                if (response.Length > 1200)
                    response = response[..1200];
                completion.TrySetException(new InvalidOperationException(
                    $"SyntyReviewHttpFailed:{request.responseCode}:{request.error}:{response}"));
            };
            return completion.Task;
        }

        [Serializable]
        private sealed class BatchRequest
        {
            public string SchemaVersion;
            public string BatchStableId;
            public string BatchRevision;
            public string Title;
            public string GeneratedAtUtc;
            public List<BatchItem> Items;
        }

        [Serializable]
        private sealed class BatchItem
        {
            public long ExpectedRevision;
            public string ReviewItemStableId;
            public string CompositionStableId;
            public string DisplayName;
            public string H1StableId;
            public string H2StableId;
            public string H3StableId;
            public string VariantCode;
            public string StateProfileCode;
            public string CompositionInputHash;
            public string PlanHash;
            public string RenderingProfileId;
            public string RenderingProfileRevision;
            public string RenderingProfileHash;
            public string ParentCaptureBundleHash;
            public string CaptureBundleHash;
            public List<PackUsage> PackUsages;
            public List<CaptureDto> Captures;
        }

        [Serializable]
        private sealed class PackUsage
        {
            public string PackCode;
            public int UsagePercent;
            public string RoleCode;
        }

        [Serializable]
        private sealed class CaptureDto
        {
            public string CaptureStableId;
            public string ViewCode;
            public string DisplayName;
            public string CaptureUploadId;
            public string StorageProviderCode;
            public string ContainerName;
            public string ObjectName;
            public string ImageUrl;
            public string ImageSha256;
            public string ContentType;
            public long ContentLength;
            public string ETag;
            public int Width;
            public int Height;
        }
    }

    [Serializable]
    internal sealed class SyntyWeb검토UploadResponse
    {
        public string captureUploadId;
        public string batchStableId;
        public string reviewItemStableId;
        public string captureStableId;
        public string viewCode;
        public string captureBundleHash;
        public string parentCaptureBundleHash;
        public string sourceCompositionHash;
        public long expectedReviewItemRevision;
        public string renderingProfileHash;
        public string storageProviderCode;
        public string containerName;
        public string objectName;
        public string imageUrl;
        public string uploadedSourceSha256;
        public string storedImageSha256;
        public string contentType;
        public long contentLength;
        public string eTag;
        public int width;
        public int height;
    }

    [Serializable]
    internal sealed class SyntyWeb검토InboxResponse
    {
        public List<SyntyWeb검토InboxItem> items;
    }

    [Serializable]
    internal sealed class SyntyWeb검토InboxItem
    {
        public string reviewItemStableId;
        public long revision;
        public string reviewStateCode;
        public SyntyWeb검토InboxComposition composition;
    }

    [Serializable]
    internal sealed class SyntyWeb검토InboxComposition
    {
        public string compositionInputHash;
        public string captureBundleHash;
    }
}
