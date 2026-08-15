using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 공간Tile표현상태
    {
        Preparing,
        Prepared,
        Active,
        WaitingForSpatialArtifact,
        OutsideCoverage,
        Failed,
    }

    [DisallowMultipleComponent]
    public sealed class 공간TileStreamingController : MonoBehaviour
    {
        [SerializeField] private Transform target = null!;
        [SerializeField] private Transform tilesRoot = null!;
        [SerializeField] private Text statusText = null!;
        [SerializeField] private Vector3 centerTileWorldPosition;
        [SerializeField] private float tileWorldSize = 24f;
        [SerializeField] private float markerWorldY = .16f;
        [SerializeField] private int originTileX = 공간TileStreamingCodes.CenterX;
        [SerializeField] private int originTileY = 공간TileStreamingCodes.CenterY;
        [SerializeField] private bool presentationOnly = true;

        private readonly Dictionary<string, TileSlot> slots =
            new Dictionary<string, TileSlot>(StringComparer.Ordinal);
        private readonly Stack<TileSlot> pool = new Stack<TileSlot>();
        private I공간TileStreamRepository repository;
        private 공간TileStreamRecipeData recipe;
        private CancellationTokenSource lifetime;
        private Material activeMaterial;
        private Material preparedMaterial;
        private Material unavailableMaterial;
        private int centerX = int.MinValue;
        private int centerY = int.MinValue;
        private int preparedCenterX = int.MinValue;
        private int preparedCenterY = int.MinValue;
        private Vector3 previousTargetPosition;
        private Vector3 movementDirection;
        private bool refreshRunning;

        public int DetailTileCount => slots.Values.Count(value => value.IsDetailWindow);
        public int ActiveTileCount => slots.Values.Count(value => value.IsActiveWindow);
        public int PreparedTileCount => slots.Count;
        public int WaitingTileCount => slots.Values.Count(value =>
            value.State == 공간Tile표현상태.WaitingForSpatialArtifact);
        public int OutsideCoverageCount => slots.Values.Count(value =>
            value.State == 공간Tile표현상태.OutsideCoverage);
        public int CurrentCenterX => centerX;
        public int CurrentCenterY => centerY;
        public int PreparedCenterX => preparedCenterX;
        public int PreparedCenterY => preparedCenterY;
        public string PreparedCenterTileKey => IsInitialized
            ? 공간TileWindowPlanner.TileKey(preparedCenterX, preparedCenterY)
            : string.Empty;
        public int DetailRadius => recipe?.DetailRadius ?? 0;
        public int ActiveRadius => recipe?.ActiveRadius ?? 0;
        public int PrefetchRadius => recipe?.PrefetchRadius ?? 0;
        public int MaxConcurrentTileLoads => recipe?.MaxConcurrentTileLoads ?? 0;
        public int DetailWindowCapacity => WindowCapacity(DetailRadius);
        public int ActiveWindowCapacity => WindowCapacity(ActiveRadius);
        public int PrefetchWindowCapacity => WindowCapacity(PrefetchRadius);
        public int ObservedWorldTick { get; private set; }
        public long ObservedActivityRevision { get; private set; }
        public string SourceModeCode => repository?.SourceModeCode ?? string.Empty;
        public bool IsInitialized => recipe != null;
        public bool PresentationOnly => presentationOnly;
        public float TileWorldSize => tileWorldSize;
        public Vector3 CenterTileWorldPosition => centerTileWorldPosition;
        public Transform FocusTarget => target;

        public void Configure(
            Transform movementTarget,
            Transform visualRoot,
            Text stateLabel,
            Vector3 centerWorldPosition,
            float compressedTileWorldSize,
            float boundaryMarkerWorldY = .16f)
        {
            target = movementTarget;
            tilesRoot = visualRoot;
            statusText = stateLabel;
            centerTileWorldPosition = centerWorldPosition;
            tileWorldSize = compressedTileWorldSize;
            markerWorldY = boundaryMarkerWorldY;
            originTileX = 공간TileStreamingCodes.CenterX;
            originTileY = 공간TileStreamingCodes.CenterY;
            presentationOnly = true;
        }

        public void SetFocusTarget(Transform focusTarget)
        {
            if (focusTarget == null)
                throw new ArgumentNullException(nameof(focusTarget));
            target = focusTarget;
            previousTargetPosition = target.position;
            movementDirection = Vector3.zero;
        }

        public async Task InitializeAsync(I공간TileStreamRepository streamRepository)
        {
            if (streamRepository == null) throw new ArgumentNullException(nameof(streamRepository));
            ValidateWiring();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            repository = streamRepository;
            recipe = await repository.LoadRecipeAsync(
                공간TileStreamingCodes.RecipeStableId, lifetime.Token);
            recipe.Validate();
            previousTargetPosition = target.position;
            movementDirection = Vector3.zero;
            await RefreshAsync(true);
        }

        private void Update()
        {
            if (repository == null || refreshRunning || target == null) return;
            SampleMovement();
            GetWindowCenters(target.position,
                out var nextX, out var nextY, out var nextPreparedX, out var nextPreparedY);
            if (nextX == centerX && nextY == centerY
                && nextPreparedX == preparedCenterX && nextPreparedY == preparedCenterY) return;
            _ = RefreshFromUpdateAsync();
        }

        private async Task RefreshFromUpdateAsync()
        {
            try
            {
                await RefreshAsync(false);
            }
            catch (OperationCanceledException) when (lifetime == null || lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                SetStatus("동적 공간 타일 갱신 실패 · " + exception.Message);
            }
        }

        public async Task RefreshAsync(bool force)
        {
            ValidateWiring();
            if (repository == null || recipe == null)
                throw new InvalidOperationException("WorldTileStreamRepositoryMissing");
            SampleMovement();
            GetWindowCenters(target.position,
                out var nextX, out var nextY, out var nextPreparedX, out var nextPreparedY);
            if (!force && nextX == centerX && nextY == centerY
                && nextPreparedX == preparedCenterX && nextPreparedY == preparedCenterY) return;
            refreshRunning = true;
            try
            {
                centerX = nextX;
                centerY = nextY;
                preparedCenterX = nextPreparedX;
                preparedCenterY = nextPreparedY;
                var desired = new HashSet<string>(
                    공간TileWindowPlanner.CreateWindow(
                        preparedCenterX, preparedCenterY, recipe.PrefetchRadius),
                    StringComparer.Ordinal);
                var active = new HashSet<string>(
                    공간TileWindowPlanner.CreateWindow(centerX, centerY, recipe.ActiveRadius),
                    StringComparer.Ordinal);
                var detail = new HashSet<string>(
                    공간TileWindowPlanner.CreateWindow(centerX, centerY, recipe.DetailRadius),
                    StringComparer.Ordinal);

                foreach (var stale in slots.Keys.Where(key => !desired.Contains(key)).ToArray())
                    Release(stale);

                var newSlots = new List<TileSlot>();
                foreach (var key in desired
                             .OrderBy(value => LoadPriority(value, detail, active))
                             .ThenBy(value => DistanceSquared(value, preparedCenterX, preparedCenterY))
                             .ThenBy(value => value, StringComparer.Ordinal))
                {
                    if (!slots.TryGetValue(key, out var slot))
                    {
                        slot = Rent(key);
                        slots.Add(key, slot);
                        newSlots.Add(slot);
                    }
                    slot.IsActiveWindow = active.Contains(key);
                    slot.IsDetailWindow = detail.Contains(key);
                    if (slot.Manifest == null)
                        slot.State = 공간Tile표현상태.Preparing;
                    else if (!slot.Manifest.IsWaitingForSpatialArtifact
                             && slot.State != 공간Tile표현상태.OutsideCoverage
                             && slot.State != 공간Tile표현상태.Failed)
                        slot.State = slot.IsActiveWindow
                            ? 공간Tile표현상태.Active
                            : 공간Tile표현상태.Prepared;
                    Position(slot, key);
                    ApplyVisual(slot);
                }

                await LoadInBatchesAsync(newSlots, lifetime.Token);
                var centerKey = 공간TileWindowPlanner.TileKey(centerX, centerY);
                if (slots.TryGetValue(centerKey, out var centerSlot)
                    && centerSlot.State != 공간Tile표현상태.OutsideCoverage)
                {
                    try
                    {
                        var activity = await repository.LoadActivitiesAsync(centerKey, lifetime.Token);
                        ObservedWorldTick = activity.WorldTick;
                        ObservedActivityRevision = activity.ActivityRevision;
                    }
                    catch (InvalidOperationException)
                    {
                        ObservedWorldTick = 0;
                        ObservedActivityRevision = 0;
                    }
                }
                UpdateStatus();
            }
            finally
            {
                refreshRunning = false;
            }
        }

        public string TileKeyAtPosition(Vector3 position)
        {
            GetCenter(position, out var x, out var y);
            return 공간TileWindowPlanner.TileKey(x, y);
        }

        public Vector3 WorldPositionForTile(string tileKey)
        {
            if (!공간TileWindowPlanner.TryParse(tileKey, out var x, out var y))
                throw new ArgumentException("WorldTileKeyInvalid", nameof(tileKey));
            return new Vector3(
                centerTileWorldPosition.x + (x - originTileX) * tileWorldSize,
                markerWorldY,
                centerTileWorldPosition.z + (y - originTileY) * tileWorldSize);
        }

        public bool IsTracked(string tileKey) => slots.ContainsKey(tileKey);

        public bool IsSafeBaseReady(string tileKey)
            => slots.TryGetValue(tileKey, out var slot)
               && slot.Manifest != null
               && !slot.Manifest.IsWaitingForSpatialArtifact
               && slot.State != 공간Tile표현상태.Failed
               && slot.State != 공간Tile표현상태.OutsideCoverage;

        public int CountState(공간Tile표현상태 state)
            => slots.Values.Count(value => value.State == state);

        private async Task LoadInBatchesAsync(
            IReadOnlyList<TileSlot> pending,
            CancellationToken cancellationToken)
        {
            var batch = new List<Task>(recipe.MaxConcurrentTileLoads);
            foreach (var slot in pending)
            {
                batch.Add(LoadSlotAsync(slot, slot.TileKey, cancellationToken));
                if (batch.Count < recipe.MaxConcurrentTileLoads) continue;
                await Task.WhenAll(batch);
                batch.Clear();
            }
            if (batch.Count > 0) await Task.WhenAll(batch);
        }

        private async Task LoadSlotAsync(
            TileSlot slot, string expectedKey, CancellationToken cancellationToken)
        {
            try
            {
                var manifest = await repository.LoadManifestAsync(expectedKey, cancellationToken);
                if (slot.TileKey != expectedKey) return;
                slot.Manifest = manifest;
                slot.State = manifest.IsWaitingForSpatialArtifact
                    ? 공간Tile표현상태.WaitingForSpatialArtifact
                    : slot.IsActiveWindow ? 공간Tile표현상태.Active : 공간Tile표현상태.Prepared;
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "WorldTileStreamTileNotFound"
                      || exception.Message.StartsWith("WorldTileStreamRequestFailed:404", StringComparison.Ordinal))
            {
                if (slot.TileKey == expectedKey)
                    slot.State = 공간Tile표현상태.OutsideCoverage;
            }
            catch
            {
                if (slot.TileKey == expectedKey) slot.State = 공간Tile표현상태.Failed;
            }
            finally
            {
                if (slot.TileKey == expectedKey) ApplyVisual(slot);
            }
        }

        private TileSlot Rent(string key)
        {
            var slot = pool.Count > 0 ? pool.Pop() : CreateSlot();
            slot.TileKey = key;
            slot.Root.name = "TileBoundary_" + key.Replace(':', '_');
            slot.Root.SetActive(true);
            return slot;
        }

        private void Release(string key)
        {
            var slot = slots[key];
            slots.Remove(key);
            slot.TileKey = string.Empty;
            slot.Manifest = null;
            slot.Root.SetActive(false);
            pool.Push(slot);
        }

        private TileSlot CreateSlot()
        {
            EnsureMaterials();
            var root = new GameObject("PooledTileBoundary");
            root.transform.SetParent(tilesRoot, true);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = .075f;
            line.numCornerVertices = 2;
            line.receiveShadows = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new TileSlot { Root = root, Boundary = line };
        }

        private void Position(TileSlot slot, string key)
        {
            if (!공간TileWindowPlanner.TryParse(key, out var x, out var y)) return;
            slot.Root.transform.position = new Vector3(
                centerTileWorldPosition.x + (x - originTileX) * tileWorldSize,
                markerWorldY,
                centerTileWorldPosition.z + (y - originTileY) * tileWorldSize);
            var half = tileWorldSize * .5f;
            slot.Boundary.SetPosition(0, new Vector3(-half, 0f, -half));
            slot.Boundary.SetPosition(1, new Vector3(-half, 0f, half));
            slot.Boundary.SetPosition(2, new Vector3(half, 0f, half));
            slot.Boundary.SetPosition(3, new Vector3(half, 0f, -half));
        }

        private void ApplyVisual(TileSlot slot)
        {
            EnsureMaterials();
            Material material;
            if (slot.State == 공간Tile표현상태.OutsideCoverage
                || slot.State == 공간Tile표현상태.Failed)
                material = unavailableMaterial;
            else if (slot.State == 공간Tile표현상태.WaitingForSpatialArtifact)
                material = slot.IsActiveWindow ? activeMaterial : preparedMaterial;
            else
                material = slot.IsActiveWindow ? activeMaterial : preparedMaterial;
            slot.Boundary.sharedMaterial = material;
            slot.Boundary.widthMultiplier = slot.IsActiveWindow ? .11f : .055f;
        }

        private void EnsureMaterials()
        {
            if (activeMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? throw new InvalidOperationException("WorldTileBoundaryShaderMissing");
            activeMaterial = BoundaryMaterial(shader, "TileBoundary_Active", new Color(.18f, .92f, .82f, .9f));
            preparedMaterial = BoundaryMaterial(shader, "TileBoundary_Prepared", new Color(1f, .63f, .18f, .52f));
            unavailableMaterial = BoundaryMaterial(shader, "TileBoundary_Unavailable", new Color(1f, .22f, .18f, .72f));
        }

        private static Material BoundaryMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            return material;
        }

        private void GetCenter(Vector3 position, out int x, out int y)
        {
            x = originTileX + Mathf.FloorToInt(
                (position.x - centerTileWorldPosition.x + tileWorldSize * .5f) / tileWorldSize);
            y = originTileY + Mathf.FloorToInt(
                (position.z - centerTileWorldPosition.z + tileWorldSize * .5f) / tileWorldSize);
        }

        private void GetWindowCenters(
            Vector3 position,
            out int currentX,
            out int currentY,
            out int prefetchX,
            out int prefetchY)
        {
            GetCenter(position, out currentX, out currentY);
            var tileCenterX = centerTileWorldPosition.x
                              + (currentX - originTileX) * tileWorldSize;
            var tileCenterY = centerTileWorldPosition.z
                              + (currentY - originTileY) * tileWorldSize;
            공간TileWindowPlanner.ResolveDirectionalPrefetchCenter(
                currentX,
                currentY,
                (position.x - tileCenterX) / tileWorldSize,
                (position.z - tileCenterY) / tileWorldSize,
                movementDirection.x,
                movementDirection.z,
                recipe.BoundaryPrefetchFraction,
                out prefetchX,
                out prefetchY);
        }

        private void SampleMovement()
        {
            var delta = target.position - previousTargetPosition;
            previousTargetPosition = target.position;
            if (delta.sqrMagnitude > .000001f) movementDirection = delta.normalized;
        }

        private static int LoadPriority(
            string key,
            ISet<string> detail,
            ISet<string> active)
            => detail.Contains(key) ? 0 : active.Contains(key) ? 1 : 2;

        private static int DistanceSquared(string key, int x, int y)
        {
            if (!공간TileWindowPlanner.TryParse(key, out var tileX, out var tileY))
                return int.MaxValue;
            var dx = tileX - x;
            var dy = tileY - y;
            return dx * dx + dy * dy;
        }

        private static int WindowCapacity(int radius)
        {
            var width = radius * 2 + 1;
            return width * width;
        }

        private void UpdateStatus()
        {
            SetStatus(
                "동적 공간 타일 · " + SourceModeCode + "\n"
                + $"현재 {공간TileWindowPlanner.TileKey(centerX, centerY)}"
                + $" · 선행 {PreparedCenterTileKey}\n"
                + $"상세 {DetailTileCount}/{DetailWindowCapacity}"
                + $" · 활성 {ActiveTileCount}/{ActiveWindowCapacity}"
                + $" · 준비 {PreparedTileCount}/{PrefetchWindowCapacity}\n"
                + $"실제 DEM·배치 마스크 자료 대기 {WaitingTileCount} · 범위 밖 {OutsideCoverageCount}\n"
                + $"동시 로드 {MaxConcurrentTileLoads} · 표현 전용 경계"
                + $" · WorldTick {ObservedWorldTick} · 활동판 {ObservedActivityRevision}");
        }

        private void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value;
        }

        private void ValidateWiring()
        {
            if (target == null || tilesRoot == null || statusText == null
                || tileWorldSize <= 0f || !presentationOnly)
                throw new InvalidOperationException("WorldTileStreamingSceneWiringInvalid");
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            if (activeMaterial != null) Destroy(activeMaterial);
            if (preparedMaterial != null) Destroy(preparedMaterial);
            if (unavailableMaterial != null) Destroy(unavailableMaterial);
        }

        private sealed class TileSlot
        {
            public string TileKey = string.Empty;
            public GameObject Root;
            public LineRenderer Boundary;
            public 공간TileStreamManifestData Manifest;
            public 공간Tile표현상태 State;
            public bool IsDetailWindow;
            public bool IsActiveWindow;
        }
    }
}
