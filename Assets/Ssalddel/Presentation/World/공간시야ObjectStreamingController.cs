using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 공간시야Object상태
    {
        Declared,
        ProxyQueued,
        ProxyActive,
        DetailQueued,
        DetailActive,
        HiddenCached,
        Failed,
    }

    [DisallowMultipleComponent]
    public sealed class 공간시야ObjectStreamingController : MonoBehaviour
    {
        [SerializeField] private Transform target = null!;
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 공간TileStreamingController tileStreaming = null!;
        [SerializeField] private 법정동경관VisualCatalog visualCatalog = null!;
        [SerializeField] private Transform objectRoot = null!;
        [SerializeField] private Camera cameraOverride = null!;
        [SerializeField] private float viewportMargin = .18f;
        [SerializeField] private float predictionSeconds = 1.5f;
        [SerializeField] private float hiddenGraceSeconds = 2f;
        [SerializeField] private float detailDistance = 28f;
        [SerializeField] private int proxyBudgetPerFrame = 4;
        [SerializeField] private int detailBudgetPerFrame = 1;
        [SerializeField] private bool presentationOnly = true;

        private readonly Dictionary<string, ObjectSlot> slots =
            new Dictionary<string, ObjectSlot>(StringComparer.Ordinal);
        private readonly HashSet<string> loadedTiles = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Stack<GameObject>> detailPool =
            new Dictionary<string, Stack<GameObject>>(StringComparer.Ordinal);
        private readonly Stack<GameObject> proxyPool = new Stack<GameObject>();
        private I공간TileStreamRepository repository;
        private CancellationTokenSource lifetime;
        private Material proxyMaterial;
        private Vector3 previousTargetPosition;
        private Vector3 targetVelocity;
        private string currentCenterTileKey = string.Empty;
        private bool windowRefreshRunning;

        public int DeclaredCount => CountState(공간시야Object상태.Declared);
        public int ProxyActiveCount => CountState(공간시야Object상태.ProxyActive);
        public int DetailActiveCount => CountState(공간시야Object상태.DetailActive);
        public int HiddenCachedCount => CountState(공간시야Object상태.HiddenCached);
        public int FailedCount => CountState(공간시야Object상태.Failed);
        public int LoadedObjectCount => slots.Count;
        public int ActualVisibleCount { get; private set; }
        public int PredictedVisibleCount { get; private set; }
        public string ActiveCameraName { get; private set; } = string.Empty;
        public bool IsInitialized => repository != null && tileStreaming != null && tileStreaming.IsInitialized;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            Transform movementTarget,
            플레이어경관Controller playerController,
            공간TileStreamingController streamingController,
            법정동경관VisualCatalog catalog,
            Transform visualRoot,
            Camera visibilityCamera = null)
        {
            target = movementTarget;
            player = playerController;
            tileStreaming = streamingController;
            visualCatalog = catalog;
            objectRoot = visualRoot;
            cameraOverride = visibilityCamera;
            presentationOnly = true;
        }

        public async Task InitializeAsync(I공간TileStreamRepository streamRepository)
        {
            if (streamRepository == null) throw new ArgumentNullException(nameof(streamRepository));
            ValidateWiring();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            repository = streamRepository;
            previousTargetPosition = target.position;
            await RefreshWindowAsync(true);
            RefreshVisibilityNow(Time.unscaledTime, 0f);
        }

        private void Update()
        {
            if (!IsInitialized || target == null) return;
            var delta = Mathf.Max(Time.unscaledDeltaTime, .0001f);
            targetVelocity = (target.position - previousTargetPosition) / delta;
            previousTargetPosition = target.position;

            var nextCenter = tileStreaming.PreparedCenterTileKey;
            if (!windowRefreshRunning && nextCenter != currentCenterTileKey)
                _ = RefreshWindowFromUpdateAsync();
            RefreshVisibilityNow(Time.unscaledTime, delta);
        }

        private async Task RefreshWindowFromUpdateAsync()
        {
            try
            {
                await RefreshWindowAsync(false);
            }
            catch (OperationCanceledException) when (lifetime == null || lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public async Task RefreshWindowAsync(bool force)
        {
            ValidateWiring();
            if (repository == null)
                throw new InvalidOperationException("WorldVisibilityObjectRepositoryMissing");
            var nextCenter = tileStreaming.PreparedCenterTileKey;
            if (!force && nextCenter == currentCenterTileKey) return;
            if (!공간TileWindowPlanner.TryParse(nextCenter, out var centerX, out var centerY))
                throw new InvalidOperationException("WorldVisibilityCenterTileInvalid");

            windowRefreshRunning = true;
            try
            {
                currentCenterTileKey = nextCenter;
                var desired = new HashSet<string>(
                    공간TileWindowPlanner.CreateWindow(
                        centerX, centerY, tileStreaming.ActiveRadius),
                    StringComparer.Ordinal);

                foreach (var staleTile in loadedTiles.Where(value => !desired.Contains(value)).ToArray())
                    ReleaseTile(staleTile);

                var pending = desired.Where(value => !loadedTiles.Contains(value))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                for (var offset = 0; offset < pending.Length;
                     offset += tileStreaming.MaxConcurrentTileLoads)
                {
                    var count = Math.Min(
                        tileStreaming.MaxConcurrentTileLoads, pending.Length - offset);
                    var tasks = new Task[count];
                    for (var index = 0; index < count; index++)
                        tasks[index] = LoadTileObjectsAsync(
                            pending[offset + index], lifetime.Token);
                    await Task.WhenAll(tasks);
                }
            }
            finally
            {
                windowRefreshRunning = false;
            }
        }

        private async Task LoadTileObjectsAsync(string tileKey, CancellationToken cancellationToken)
        {
            try
            {
                var projection = await repository.LoadObjectsAsync(tileKey, cancellationToken);
                projection.Validate();
                if (projection.TileKey != tileKey) return;
                loadedTiles.Add(tileKey);
                foreach (var placement in projection.Objects.OrderBy(value => value.ObjectStableId))
                {
                    if (slots.ContainsKey(placement.ObjectStableId)) continue;
                    var slot = new ObjectSlot
                    {
                        TileKey = tileKey,
                        Data = placement,
                        State = 공간시야Object상태.Declared,
                        LastVisibleTime = Time.unscaledTime,
                    };
                    Position(slot);
                    slots.Add(placement.ObjectStableId, slot);
                }
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "WorldTileStreamTileNotFound"
                      || exception.Message.StartsWith("WorldTileStreamRequestFailed:404", StringComparison.Ordinal))
            {
                loadedTiles.Add(tileKey);
            }
        }

        public void RefreshVisibilityNow(float now, float deltaTime)
        {
            if (tileStreaming == null || target == null) return;
            var camera = ResolveCamera();
            if (camera == null)
            {
                ActiveCameraName = "카메라 없음";
                ActualVisibleCount = 0;
                PredictedVisibleCount = 0;
                return;
            }

            ActiveCameraName = camera.name;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var proxyBudget = Mathf.Max(0, proxyBudgetPerFrame);
            var detailBudget = Mathf.Max(0, detailBudgetPerFrame);
            var actualCount = 0;
            var predictedCount = 0;

            foreach (var slot in slots.Values.OrderBy(value => value.Data.ObjectStableId, StringComparer.Ordinal))
            {
                var actual = GeometryUtility.TestPlanesAABB(planes, slot.Bounds);
                var predicted = !actual && IsPredictedVisible(camera, slot.Bounds);
                if (actual) actualCount++;
                if (predicted) predictedCount++;

                if (actual || predicted)
                {
                    slot.LastVisibleTime = now;
                    var distance = Vector3.Distance(camera.transform.position, slot.Bounds.center);
                    var needsDetail = actual && distance <= detailDistance;
                    if (needsDetail)
                    {
                        if (slot.Detail == null && detailBudget <= 0)
                        {
                            slot.State = 공간시야Object상태.DetailQueued;
                            EnsureProxy(slot, ref proxyBudget);
                            continue;
                        }
                        if (slot.Detail == null)
                        {
                            detailBudget--;
                            if (!EnsureDetail(slot)) continue;
                        }
                        SetActive(slot.Detail, true);
                        SetActive(slot.Proxy, false);
                        slot.State = 공간시야Object상태.DetailActive;
                    }
                    else
                    {
                        if (!EnsureProxy(slot, ref proxyBudget))
                        {
                            slot.State = 공간시야Object상태.ProxyQueued;
                            continue;
                        }
                        SetActive(slot.Proxy, true);
                        SetActive(slot.Detail, false);
                        slot.State = 공간시야Object상태.ProxyActive;
                    }
                }
                else if (now - slot.LastVisibleTime >= hiddenGraceSeconds)
                {
                    SetActive(slot.Proxy, false);
                    SetActive(slot.Detail, false);
                    slot.State = 공간시야Object상태.HiddenCached;
                }
            }

            ActualVisibleCount = actualCount;
            PredictedVisibleCount = predictedCount;
        }

        public 공간시야Object상태? GetState(string objectStableId)
            => slots.TryGetValue(objectStableId, out var slot)
                ? slot.State
                : (공간시야Object상태?)null;

        public Vector3? GetWorldPosition(string objectStableId)
            => slots.TryGetValue(objectStableId, out var slot)
                ? slot.Bounds.center
                : (Vector3?)null;

        public int CountState(공간시야Object상태 state)
            => slots.Values.Count(value => value.State == state);

        private bool IsPredictedVisible(Camera camera, Bounds bounds)
        {
            var viewport = camera.WorldToViewportPoint(bounds.center);
            if (viewport.z > 0f
                && viewport.x >= -viewportMargin && viewport.x <= 1f + viewportMargin
                && viewport.y >= -viewportMargin && viewport.y <= 1f + viewportMargin)
                return true;

            var flatVelocity = Vector3.ProjectOnPlane(targetVelocity, Vector3.up);
            if (flatVelocity.sqrMagnitude < .04f) return false;
            var predictedTarget = target.position + flatVelocity * predictionSeconds;
            var toObject = Vector3.ProjectOnPlane(bounds.center - predictedTarget, Vector3.up);
            if (toObject.sqrMagnitude > tileStreaming.TileWorldSize * tileStreaming.TileWorldSize * 9f)
                return false;
            return Vector3.Dot(flatVelocity.normalized, toObject.normalized) >= .35f;
        }

        private void Position(ObjectSlot slot)
        {
            var scale = tileStreaming.TileWorldSize / 500f;
            var tileCenter = tileStreaming.WorldPositionForTile(slot.TileKey);
            var center = tileCenter + new Vector3(
                (float)slot.Data.LocalOffsetXMeters * scale,
                0f,
                (float)slot.Data.LocalOffsetYMeters * scale);
            var probeOrigin = center + Vector3.up * 500f;
            if (Physics.Raycast(probeOrigin, Vector3.down, out var hit, 1000f, ~0,
                    QueryTriggerInteraction.Ignore))
                center.y = hit.point.y;
            var width = Mathf.Max(1.2f, (float)slot.Data.FootprintWidthMeters * scale);
            var depth = Mathf.Max(1.2f, (float)slot.Data.FootprintDepthMeters * scale);
            var height = Mathf.Max(2.2f, (float)slot.Data.HeightMeters * scale * 1.8f);
            slot.Bounds = new Bounds(center + Vector3.up * height * .5f,
                new Vector3(width, height, depth));
        }

        private bool EnsureProxy(ObjectSlot slot, ref int budget)
        {
            if (slot.Proxy != null) return true;
            if (budget <= 0) return false;
            budget--;
            var proxy = proxyPool.Count > 0 ? proxyPool.Pop() : CreateProxy();
            proxy.name = "시야프록시_" + slot.Data.ObjectStableId;
            proxy.transform.SetParent(objectRoot, true);
            proxy.transform.position = slot.Bounds.center;
            proxy.transform.rotation = Quaternion.Euler(0f, (float)slot.Data.RotationDegrees, 0f);
            proxy.transform.localScale = slot.Bounds.size;
            proxy.SetActive(true);
            slot.Proxy = proxy;
            return true;
        }

        private bool EnsureDetail(ObjectSlot slot)
        {
            try
            {
                var entry = visualCatalog.Resolve(slot.Data.VisualKey);
                GameObject detail;
                if (detailPool.TryGetValue(slot.Data.VisualKey, out var pool) && pool.Count > 0)
                    detail = pool.Pop();
                else
                    detail = Instantiate(entry.Prefab);
                detail.name = "시야상세_" + slot.Data.ObjectStableId;
                detail.transform.SetParent(objectRoot, true);
                detail.transform.localScale = Vector3.one;
                detail.transform.position = new Vector3(
                    slot.Bounds.center.x, slot.Bounds.min.y, slot.Bounds.center.z);
                detail.transform.rotation = Quaternion.Euler(0f, (float)slot.Data.RotationDegrees, 0f);
                detail.SetActive(true);
                FitDetailToBounds(detail, slot.Bounds);
                foreach (var collider in detail.GetComponentsInChildren<Collider>(true))
                    collider.enabled = slot.Data.CollisionEligible;
                slot.Detail = detail;
                return true;
            }
            catch (Exception exception)
            {
                slot.State = 공간시야Object상태.Failed;
                Debug.LogWarning("시야 건물 상세 표현 실패: " + exception.Message, this);
                return false;
            }
        }

        private GameObject CreateProxy()
        {
            EnsureProxyMaterial();
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = proxy.GetComponent<Collider>();
            if (collider != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            var renderer = proxy.GetComponent<Renderer>();
            renderer.sharedMaterial = proxyMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return proxy;
        }

        private static void FitDetailToBounds(GameObject detail, Bounds targetBounds)
        {
            var renderers = detail.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var current = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                current.Encapsulate(renderers[index].bounds);
            if (current.size.x <= .001f || current.size.y <= .001f || current.size.z <= .001f)
                return;

            var fit = Mathf.Min(
                targetBounds.size.x / current.size.x,
                targetBounds.size.y / current.size.y,
                targetBounds.size.z / current.size.z);
            // 실제 면적을 압축한 World에서도 건물의 역할이 읽히도록 Renderer에서만 소폭 과장한다.
            fit = Mathf.Clamp(fit * 1.45f, .06f, .5f);
            detail.transform.localScale = Vector3.one * fit;

            current = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                current.Encapsulate(renderers[index].bounds);
            detail.transform.position += new Vector3(
                targetBounds.center.x - current.center.x,
                targetBounds.min.y - current.min.y,
                targetBounds.center.z - current.center.z);
        }

        private void ReleaseTile(string tileKey)
        {
            loadedTiles.Remove(tileKey);
            foreach (var pair in slots.Where(value => value.Value.TileKey == tileKey).ToArray())
            {
                var slot = pair.Value;
                if (slot.Proxy != null)
                {
                    slot.Proxy.SetActive(false);
                    proxyPool.Push(slot.Proxy);
                }
                if (slot.Detail != null)
                {
                    slot.Detail.SetActive(false);
                    if (!detailPool.TryGetValue(slot.Data.VisualKey, out var pool))
                    {
                        pool = new Stack<GameObject>();
                        detailPool.Add(slot.Data.VisualKey, pool);
                    }
                    pool.Push(slot.Detail);
                }
                slots.Remove(pair.Key);
            }
        }

        private Camera ResolveCamera()
        {
            if (cameraOverride != null && cameraOverride.isActiveAndEnabled) return cameraOverride;
            if (player != null)
            {
                if (player.FirstPersonCamera != null && player.FirstPersonCamera.isActiveAndEnabled)
                    return player.FirstPersonCamera;
                if (player.PlayerCamera != null && player.PlayerCamera.isActiveAndEnabled)
                    return player.PlayerCamera;
            }
            return Camera.main ?? Camera.allCameras.FirstOrDefault(value => value.isActiveAndEnabled);
        }

        private void EnsureProxyMaterial()
        {
            if (proxyMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            proxyMaterial = new Material(shader)
            {
                name = "시야건물프록시_런타임",
                color = new Color(.16f, .86f, .75f, .52f),
            };
            if (proxyMaterial.HasProperty("_Surface")) proxyMaterial.SetFloat("_Surface", 1f);
            if (proxyMaterial.HasProperty("_ZWrite")) proxyMaterial.SetFloat("_ZWrite", 0f);
            proxyMaterial.renderQueue = 3000;
        }

        private void ValidateWiring()
        {
            if (target == null || tileStreaming == null || visualCatalog == null || objectRoot == null)
                throw new InvalidOperationException("WorldVisibilityObjectWiringInvalid");
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active) value.SetActive(active);
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            if (proxyMaterial != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(proxyMaterial);
                else DestroyImmediate(proxyMaterial);
            }
        }

        private sealed class ObjectSlot
        {
            public string TileKey = string.Empty;
            public 공간TileObjectPlacementData Data = null!;
            public 공간시야Object상태 State;
            public Bounds Bounds;
            public GameObject Proxy;
            public GameObject Detail;
            public float LastVisibleTime;
        }
    }
}
