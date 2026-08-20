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
        [SerializeField, Min(.1f)] private float visualElevationExaggeration = 1.4f;
        [SerializeField] private int originTileX = 공간TileStreamingCodes.CenterX;
        [SerializeField] private int originTileY = 공간TileStreamingCodes.CenterY;
        [SerializeField] private bool presentationOnly = true;
        [SerializeField] private 공간문법CompositionCatalog landscapeCompositionCatalog = null!;
        [SerializeField] private 공간문법SyntyBindingCatalog landscapeSyntyBindingCatalog = null!;

        private readonly Dictionary<string, TileSlot> slots =
            new Dictionary<string, TileSlot>(StringComparer.Ordinal);
        private readonly Stack<TileSlot> pool = new Stack<TileSlot>();
        private I공간TileStreamRepository repository;
        private I공간TileLandscapeCompositionRepository landscapeRepository;
        private 공간문법LandscapeRuntimeAssembler landscapeAssembler;
        private 공간TileStreamRecipeData recipe;
        private CancellationTokenSource lifetime;
        private Material activeMaterial;
        private Material preparedMaterial;
        private Material unavailableMaterial;
        private Material physicalTerrainMaterial;
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
        public int ActualElevationTileCount => slots.Values.Count(value =>
            value.PhysicalTerrainRoot != null && value.PhysicalTerrainRoot.activeSelf);
        public int LandscapeCompositionTileCount => slots.Values.Count(value =>
            value.LandscapeCompositionRoot != null && value.LandscapeCompositionRoot.activeSelf);
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

        public void ConfigureLandscapeAssembly(공간문법CompositionCatalog catalog)
        {
            landscapeCompositionCatalog = catalog;
            landscapeSyntyBindingCatalog = null!;
            landscapeAssembler = catalog == null
                ? null
                : new 공간문법LandscapeRuntimeAssembler(catalog, tileWorldSize);
        }

        public void ConfigureLandscapeAssembly(
            공간문법CompositionCatalog catalog,
            공간문법SyntyBindingCatalog bindingCatalog)
        {
            landscapeCompositionCatalog = catalog;
            landscapeSyntyBindingCatalog = bindingCatalog;
            landscapeAssembler = catalog == null || bindingCatalog == null
                ? null
                : new 공간문법LandscapeRuntimeAssembler(
                    catalog, bindingCatalog, tileWorldSize);
        }

        public async Task InitializeAsync(I공간TileStreamRepository streamRepository)
        {
            if (streamRepository == null) throw new ArgumentNullException(nameof(streamRepository));
            ValidateWiring();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            repository = streamRepository;
            landscapeRepository = streamRepository as I공간TileLandscapeCompositionRepository;
            landscapeAssembler = landscapeCompositionCatalog == null
                ? null
                : landscapeSyntyBindingCatalog == null
                    ? new 공간문법LandscapeRuntimeAssembler(
                        landscapeCompositionCatalog, tileWorldSize)
                    : new 공간문법LandscapeRuntimeAssembler(
                        landscapeCompositionCatalog,
                        landscapeSyntyBindingCatalog,
                        tileWorldSize);
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
                    if (slot.PhysicalTerrainRoot != null)
                        slot.PhysicalTerrainRoot.SetActive(slot.IsDetailWindow);
                    if (slot.LandscapeCompositionRoot != null)
                        slot.LandscapeCompositionRoot.SetActive(slot.IsDetailWindow);
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
                await LoadDetailArtifactsInBatchesAsync(lifetime.Token);
                await LoadDetailLandscapeInBatchesAsync(lifetime.Token);
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

        /// <summary>
        /// 현재 준비·활성·선행 적재 창이 덮는 Unity 로컬 X/Z 범위를 반환합니다.
        /// 고정 World 사각형 대신 스트리밍 Coverage를 카메라·이동 보조 경계로 사용할 때 쓴다.
        /// </summary>
        public bool TryGetTrackedWorldBounds(out Bounds bounds)
        {
            bounds = default;
            if (!IsInitialized || slots.Count == 0) return false;

            var minimumX = float.PositiveInfinity;
            var maximumX = float.NegativeInfinity;
            var minimumZ = float.PositiveInfinity;
            var maximumZ = float.NegativeInfinity;
            var half = tileWorldSize * .5f;
            foreach (var key in slots.Keys)
            {
                var center = WorldPositionForTile(key);
                minimumX = Mathf.Min(minimumX, center.x - half);
                maximumX = Mathf.Max(maximumX, center.x + half);
                minimumZ = Mathf.Min(minimumZ, center.z - half);
                maximumZ = Mathf.Max(maximumZ, center.z + half);
            }

            bounds = new Bounds(
                new Vector3(
                    (minimumX + maximumX) * .5f,
                    centerTileWorldPosition.y,
                    (minimumZ + maximumZ) * .5f),
                new Vector3(
                    maximumX - minimumX,
                    0f,
                    maximumZ - minimumZ));
            return true;
        }

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

        private async Task LoadDetailArtifactsInBatchesAsync(CancellationToken cancellationToken)
        {
            var pending = slots.Values
                .Where(slot => slot.IsDetailWindow
                    && slot.Manifest != null
                    && !slot.ArtifactLoadAttempted
                    && slot.Manifest.Layers.Any(layer =>
                        layer.LayerCode == 공간TileStreamingCodes.ElevationLayer
                        && layer.StatusCode == 공간TileStreamingCodes.Available))
                .OrderBy(slot => DistanceSquared(slot.TileKey, centerX, centerY))
                .ThenBy(slot => slot.TileKey, StringComparer.Ordinal)
                .ToArray();
            var batch = new List<Task>(recipe.MaxConcurrentTileLoads);
            foreach (var slot in pending)
            {
                batch.Add(LoadPhysicalElevationAsync(slot, slot.TileKey, cancellationToken));
                if (batch.Count < recipe.MaxConcurrentTileLoads) continue;
                await Task.WhenAll(batch);
                batch.Clear();
            }
            if (batch.Count > 0) await Task.WhenAll(batch);
        }

        private async Task LoadPhysicalElevationAsync(
            TileSlot slot,
            string expectedKey,
            CancellationToken cancellationToken)
        {
            slot.ArtifactLoadAttempted = true;
            try
            {
                var payload = await repository.LoadArtifactContentAsync(
                    expectedKey, 공간TileStreamingCodes.ElevationLayer, cancellationToken);
                if (slot.TileKey != expectedKey) return;
                ApplyPhysicalElevation(slot, payload);
            }
            catch (Exception exception)
            {
                if (slot.TileKey == expectedKey)
                {
                    slot.ArtifactLoadFailed = true;
                    Debug.LogWarning("물리 표고 산출물 표현 실패 · " + expectedKey
                                     + " · " + exception.Message, this);
                }
            }
        }

        private async Task LoadDetailLandscapeInBatchesAsync(
            CancellationToken cancellationToken)
        {
            if (landscapeRepository == null || landscapeAssembler == null) return;
            var pending = slots.Values
                .Where(slot => slot.IsDetailWindow
                    && slot.Manifest != null
                    && !slot.Manifest.IsWaitingForSpatialArtifact
                    && !slot.LandscapeLoadAttempted)
                .OrderBy(slot => DistanceSquared(slot.TileKey, centerX, centerY))
                .ThenBy(slot => slot.TileKey, StringComparer.Ordinal)
                .ToArray();
            var batch = new List<Task>(recipe.MaxConcurrentTileLoads);
            foreach (var slot in pending)
            {
                batch.Add(LoadLandscapeCompositionAsync(
                    slot, slot.TileKey, cancellationToken));
                if (batch.Count < recipe.MaxConcurrentTileLoads) continue;
                await Task.WhenAll(batch);
                batch.Clear();
            }
            if (batch.Count > 0) await Task.WhenAll(batch);
        }

        private async Task LoadLandscapeCompositionAsync(
            TileSlot slot,
            string expectedKey,
            CancellationToken cancellationToken)
        {
            slot.LandscapeLoadAttempted = true;
            try
            {
                var data = await landscapeRepository.LoadLandscapeCompositionsAsync(
                    expectedKey, cancellationToken);
                if (slot.TileKey != expectedKey) return;
                if (!data.CanAssemble)
                {
                    slot.LandscapeLoadAttempted = false;
                    return;
                }
                var staging = landscapeAssembler.BuildStaging(data, slot.Root.transform);
                if (slot.TileKey != expectedKey)
                {
                    Destroy(staging);
                    return;
                }
                공간문법LandscapeRuntimeAssembler.CommitAtomic(
                    ref slot.LandscapeCompositionRoot, staging);
                slot.LandscapeGraphHashSha256 = data.GraphHashSha256;
                slot.LandscapeLoadFailed = false;
            }
            catch (InvalidOperationException exception)
                when (exception.Message.StartsWith(
                    "WorldTileStreamRequestFailed:404", StringComparison.Ordinal))
            {
                if (slot.TileKey == expectedKey)
                    slot.LandscapeLoadAttempted = false;
            }
            catch (Exception exception)
            {
                if (slot.TileKey == expectedKey)
                {
                    slot.LandscapeLoadFailed = true;
                    Debug.LogWarning("경관 Graph 조립 실패 · " + expectedKey
                                     + " · " + exception.Message, this);
                }
            }
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
            ClearPhysicalElevation(slot);
            ClearLandscapeComposition(slot);
            slot.ArtifactLoadAttempted = false;
            slot.ArtifactLoadFailed = false;
            slot.LandscapeLoadAttempted = false;
            slot.LandscapeLoadFailed = false;
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

        private void ApplyPhysicalElevation(
            TileSlot slot,
            공간TileArtifactPayloadData payload)
        {
            ClearPhysicalElevation(slot);
            var mesh = 공간PhysicalElevationMeshBuilder.BuildCoreMesh(
                payload,
                slot.Manifest.HaloMeters,
                recipe.TileSizeMeters,
                tileWorldSize,
                visualElevationExaggeration,
                out slot.MinimumPhysicalElevationMeters,
                out slot.MaximumPhysicalElevationMeters);
            var terrain = new GameObject("PhysicalElevation_검증된DEM_PresentationOnly");
            terrain.transform.SetParent(slot.Root.transform, false);
            terrain.transform.localPosition = new Vector3(0f, -markerWorldY, 0f);
            var filter = terrain.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PhysicalTerrainMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            slot.PhysicalTerrainRoot = terrain;
            slot.PhysicalTerrainMesh = mesh;
            slot.ArtifactLoadFailed = false;
        }

        private static void ClearPhysicalElevation(TileSlot slot)
        {
            if (slot.PhysicalTerrainRoot != null) Destroy(slot.PhysicalTerrainRoot);
            if (slot.PhysicalTerrainMesh != null) Destroy(slot.PhysicalTerrainMesh);
            slot.PhysicalTerrainRoot = null;
            slot.PhysicalTerrainMesh = null;
            slot.MinimumPhysicalElevationMeters = 0f;
            slot.MaximumPhysicalElevationMeters = 0f;
        }

        private static void ClearLandscapeComposition(TileSlot slot)
        {
            if (slot.LandscapeCompositionRoot != null)
                Destroy(slot.LandscapeCompositionRoot);
            slot.LandscapeCompositionRoot = null;
            slot.LandscapeGraphHashSha256 = string.Empty;
        }

        private Material PhysicalTerrainMaterial()
        {
            if (physicalTerrainMaterial != null) return physicalTerrainMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? throw new InvalidOperationException("PhysicalTerrainShaderMissing");
            physicalTerrainMaterial = new Material(shader)
            {
                name = "PhysicalTerrain_대관령저채도초지_PresentationOnly",
                color = new Color(.31f, .48f, .24f, 1f),
            };
            if (physicalTerrainMaterial.HasProperty("_BaseColor"))
                physicalTerrainMaterial.SetColor("_BaseColor", physicalTerrainMaterial.color);
            if (physicalTerrainMaterial.HasProperty("_Smoothness"))
                physicalTerrainMaterial.SetFloat("_Smoothness", .12f);
            return physicalTerrainMaterial;
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
                + $"검증된 DEM 지형 {ActualElevationTileCount} · "
                + $"경관 Graph 조립 {LandscapeCompositionTileCount} · "
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
            if (physicalTerrainMaterial != null) Destroy(physicalTerrainMaterial);
            foreach (var slot in slots.Values) ClearPhysicalElevation(slot);
            foreach (var slot in slots.Values) ClearLandscapeComposition(slot);
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
            public GameObject PhysicalTerrainRoot;
            public Mesh PhysicalTerrainMesh;
            public bool ArtifactLoadAttempted;
            public bool ArtifactLoadFailed;
            public float MinimumPhysicalElevationMeters;
            public float MaximumPhysicalElevationMeters;
            public GameObject LandscapeCompositionRoot;
            public string LandscapeGraphHashSha256 = string.Empty;
            public bool LandscapeLoadAttempted;
            public bool LandscapeLoadFailed;
        }
    }
}
