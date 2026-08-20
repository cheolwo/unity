using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Ssalddel.Unity.Presentation.World
{
    public enum 공간LHCellPreparationState
    {
        Requested,
        DependenciesReady,
        GeneratedDataReady,
        VisualPrepared,
        PlayerTraversalReady,
        Active,
        Cached,
        Released,
        Failed,
    }

    [DisallowMultipleComponent]
    public sealed class 공간LHStreamingEngine : MonoBehaviour
    {
        private static readonly int SeasonIndex = Shader.PropertyToID("_SsalddelSeasonIndex");
        private static readonly int SeasonProgress = Shader.PropertyToID("_SsalddelSeasonProgress");

        [SerializeField] private Transform focusTarget = null!;
        [SerializeField] private Transform generatedCellRoot = null!;
        [SerializeField] private Transform floatingOriginRoot = null!;
        [SerializeField] private Text statusLabel = null!;
        [SerializeField] private 공간문법CompositionCatalog compositionCatalog = null!;
        [SerializeField] private SimulationWorldShellPresenter authorityShell = null!;
        [SerializeField] private Vector3 originWorldPosition;
        [SerializeField] private float l3CellWorldSize = 공간LHWorldCodes.L3CellSizeMeters;
        [SerializeField] private string sessionStableId = "simulation-unity-harvest-day";
        [SerializeField] private long expectedWorldRevision;
        [SerializeField] private bool presentationOnly = true;

        private readonly Dictionary<string, CellRuntime> cells =
            new(StringComparer.Ordinal);
        private readonly Queue<CellRuntime> pooledCells = new();
        private readonly Queue<Action> mainThreadAssembly = new();
        private readonly Dictionary<string, string> npcRouteInterestCellKeys =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> acceptedNpcEpochs = new(StringComparer.Ordinal);
        private readonly object assemblyLock = new();
        private CancellationTokenSource lifetime = null!;
        private I공간LHWorldRepository repository = null!;
        private 공간LHWorldProfileData profile = null!;
        private string acceptedEpoch = string.Empty;
        private string requestedFocusCellKey = string.Empty;
        private string playerCellKey = string.Empty;
        private string pinnedFocusCellKey = string.Empty;
        private string movementDirectionCode = 공간LHWorldCodes.None;
        private long requestSequence;
        private long npcRequestSequence;
        private int requestsInFlight;
        private string requestedNpcCellKey = string.Empty;
        private string activeSeasonCode = string.Empty;
        private int activeSeasonDay;
        private string activeContentSourceCode = 공간LHWorldCodes.ScenarioProcedural;
        private string lastError = string.Empty;
        private Vector3 accumulatedOriginShift;
        private int originShiftCount;

        public bool IsInitialized => profile != null;
        public bool PresentationOnly => presentationOnly;
        public string PlayerCellKey => playerCellKey;
        public string RequestedFocusCellKey => requestedFocusCellKey;
        public string PinnedFocusCellKey => pinnedFocusCellKey;
        public bool IsFocusPinned => !string.IsNullOrWhiteSpace(pinnedFocusCellKey);
        public string ActiveSeasonCode => activeSeasonCode;
        public int ActiveSeasonDay => activeSeasonDay;
        public float L3CellWorldSize => l3CellWorldSize;
        public Vector3 AccumulatedOriginShift => accumulatedOriginShift;
        public int OriginShiftCount => originShiftCount;
        public bool FloatingOriginConfigured => floatingOriginRoot != null;
        public int TrackedCellCount => cells.Count;
        public int NpcRouteInterestCount => npcRouteInterestCellKeys.Count;
        public string SourceModeCode => repository?.SourceModeCode ?? string.Empty;
        public int PendingAssemblyCount
        {
            get { lock (assemblyLock) return mainThreadAssembly.Count; }
        }

        public void Configure(
            Transform player,
            Transform cellRoot,
            Text stateLabel,
            공간문법CompositionCatalog catalog,
            Vector3 worldOrigin,
            float cellWorldSize = 공간LHWorldCodes.L3CellSizeMeters,
            string sessionId = "simulation-unity-harvest-day",
            long worldRevision = 0)
        {
            focusTarget = player;
            generatedCellRoot = cellRoot;
            statusLabel = stateLabel;
            compositionCatalog = catalog;
            originWorldPosition = worldOrigin;
            l3CellWorldSize = Mathf.Max(1f, cellWorldSize);
            sessionStableId = sessionId ?? string.Empty;
            expectedWorldRevision = worldRevision;
            presentationOnly = true;
        }

        public void ConfigureAuthority(SimulationWorldShellPresenter shell)
            => authorityShell = shell;

        public void ConfigureFloatingOrigin(Transform worldRoot)
            => floatingOriginRoot = worldRoot;

        public void PinFocusCell(string cellKey)
        {
            if (!공간LHCellKey.TryParseL3(cellKey, out _, out _))
                throw new ArgumentException("LHWorldPinnedFocusCellInvalid", nameof(cellKey));
            pinnedFocusCellKey = cellKey.Trim();
            movementDirectionCode = 공간LHWorldCodes.None;
            if (repository != null && lifetime != null
                && pinnedFocusCellKey != requestedFocusCellKey)
                _ = RequestWindowAsync(false, lifetime.Token);
        }

        public void ReleaseFocusPin() => pinnedFocusCellKey = string.Empty;

        public async Task InitializeAsync(I공간LHWorldRepository worldRepository)
        {
            repository = worldRepository ?? throw new ArgumentNullException(nameof(worldRepository));
            if (focusTarget == null || generatedCellRoot == null)
                throw new InvalidOperationException("LHWorldStreamingWiringMissing");
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            await RequestWindowAsync(true, lifetime.Token);
        }

        private void Update()
        {
            if (repository == null || lifetime == null) return;
            TryShiftFloatingOrigin();
            EvaluateFocusAndRequest();
            EvaluateNpcRouteInterests();
            DrainAssemblyBudget(profile == null ? 2d
                : profile.MainThreadAssemblyBudgetMilliseconds);
            UpdateStatus();
        }

        public void EvaluateForTests()
        {
            TryShiftFloatingOrigin();
            EvaluateFocusAndRequest();
            EvaluateNpcRouteInterests();
            DrainAssemblyBudget(double.MaxValue);
            UpdateStatus();
        }

        public void DrainAssemblyForTests() => DrainAssemblyBudget(double.MaxValue);

        public bool IsCapabilityReady(string cellKey, string capabilityCode)
            => cells.TryGetValue(cellKey, out var cell)
               && cell.ReadyCapabilities.Contains(capabilityCode);

        public bool IsPlayerTraversalReady(string cellKey)
            => cells.TryGetValue(cellKey, out var cell)
               && cell.ReadyCapabilities.Contains(공간LHWorldCodes.Collision)
               && cell.ReadyCapabilities.Contains(공간LHWorldCodes.Connector)
               && cell.State >= 공간LHCellPreparationState.PlayerTraversalReady;

        public string CellKeyAtPosition(Vector3 worldPosition)
            => 공간LHCellKey.FromWorldPosition(
                worldPosition.x, worldPosition.z,
                originWorldPosition.x, originWorldPosition.z);

        public void RegisterNpcRouteInterest(string npcStableId, Vector3 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(npcStableId))
                throw new ArgumentException("NpcRouteInterestStableIdMissing", nameof(npcStableId));
            npcRouteInterestCellKeys[npcStableId] = CellKeyAtPosition(worldPosition);
        }

        public void UnregisterNpcRouteInterest(string npcStableId)
        {
            if (!string.IsNullOrWhiteSpace(npcStableId))
                npcRouteInterestCellKeys.Remove(npcStableId);
        }

        public bool IsNpcNavigationReady(Vector3 worldPosition)
        {
            var cellKey = CellKeyAtPosition(worldPosition);
            return cells.TryGetValue(cellKey, out var cell)
                   && cell.ReadyCapabilities.Contains(공간LHWorldCodes.Collision)
                   && cell.ReadyCapabilities.Contains(공간LHWorldCodes.Connector)
                   && cell.ReadyCapabilities.Contains(공간LHWorldCodes.NpcNavigation)
                   && cell.State >= 공간LHCellPreparationState.PlayerTraversalReady;
        }

        public 공간LHCellPreparationState? StateOf(string cellKey)
            => cells.TryGetValue(cellKey, out var cell) ? cell.State : null;

        public bool TryGetTrackedWorldBounds(out Bounds bounds)
        {
            if (cells.Count == 0)
            {
                bounds = default;
                return false;
            }
            var positions = cells.Values.Where(value => value.Root != null)
                .Select(value => value.Root.transform.position).ToArray();
            if (positions.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = new Bounds(positions[0], Vector3.one * l3CellWorldSize);
            foreach (var position in positions) bounds.Encapsulate(position);
            bounds.Expand(new Vector3(l3CellWorldSize, 1f, l3CellWorldSize));
            return true;
        }

        private void EvaluateFocusAndRequest()
        {
            if (!string.IsNullOrWhiteSpace(pinnedFocusCellKey))
            {
                if (pinnedFocusCellKey != requestedFocusCellKey
                    && Volatile.Read(ref requestsInFlight) <
                        (profile?.MaxConcurrentPreparations ?? 4))
                    _ = RequestWindowAsync(false, lifetime.Token);
                return;
            }
            var current = 공간LHCellKey.FromWorldPosition(
                focusTarget.position.x, focusTarget.position.z,
                originWorldPosition.x, originWorldPosition.z);
            if (!공간LHCellKey.TryParseL3(current, out var x, out var y)) return;
            if (current != playerCellKey)
            {
                movementDirectionCode = Direction(playerCellKey, current);
                playerCellKey = current;
            }

            var centerX = originWorldPosition.x
                          + (x - 공간LHWorldCodes.CenterL3X) * l3CellWorldSize;
            var centerZ = originWorldPosition.z
                          + (y - 공간LHWorldCodes.CenterL3Y) * l3CellWorldSize;
            var normalizedX = (focusTarget.position.x - centerX) / l3CellWorldSize;
            var normalizedZ = (focusTarget.position.z - centerZ) / l3CellWorldSize;
            var threshold = profile == null ? .25d : profile.BoundaryPrefetchFraction;
            var edge = .5f - (float)threshold;
            var requestX = x;
            var requestY = y;
            if (normalizedX >= edge) requestX++;
            else if (normalizedX <= -edge) requestX--;
            if (normalizedZ >= edge) requestY++;
            else if (normalizedZ <= -edge) requestY--;
            var desired = 공간LHCellKey.L3(requestX, requestY);
            var maximum = profile?.MaxConcurrentPreparations ?? 4;
            if (Volatile.Read(ref requestsInFlight) < maximum
                && desired != requestedFocusCellKey)
                _ = RequestWindowAsync(false, lifetime.Token);
        }

        private void EvaluateNpcRouteInterests()
        {
            if (repository == null || lifetime == null || npcRouteInterestCellKeys.Count == 0)
                return;
            var desired = npcRouteInterestCellKeys
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => value.Value)
                .FirstOrDefault(cellKey => !IsCapabilityReady(
                    cellKey, 공간LHWorldCodes.NpcNavigation));
            if (string.IsNullOrWhiteSpace(desired)
                || desired == requestedNpcCellKey
                || Volatile.Read(ref requestsInFlight) >= (profile?.MaxConcurrentPreparations ?? 4))
                return;
            requestedNpcCellKey = desired;
            _ = RequestNpcWindowAsync(desired, lifetime.Token);
        }

        private void TryShiftFloatingOrigin()
        {
            if (floatingOriginRoot == null || profile == null) return;
            var offset = focusTarget.position - originWorldPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude
                < profile.OriginShiftThresholdWorldUnits
                * profile.OriginShiftThresholdWorldUnits) return;
            var shift = new Vector3(
                Mathf.Round(offset.x / l3CellWorldSize) * l3CellWorldSize,
                0f,
                Mathf.Round(offset.z / l3CellWorldSize) * l3CellWorldSize);
            if (shift.sqrMagnitude < .01f) return;
            floatingOriginRoot.position -= shift;
            originWorldPosition -= shift;
            accumulatedOriginShift += shift;
            originShiftCount++;
        }

        private async Task RequestWindowAsync(bool initial, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestsInFlight);
            try
            {
                if (repository.SourceModeCode == 공간TileStreamingCodes.SimulationServer)
                {
                    if (authorityShell == null
                        || string.IsNullOrWhiteSpace(authorityShell.SessionStableId)
                        || authorityShell.SessionStableId == SimulationWorldShellFixture.SessionStableId
                        || authorityShell.WorldRevision < 0)
                        throw new InvalidOperationException("LHWorldAuthoritativeSessionMissing");
                    sessionStableId = authorityShell.SessionStableId;
                    expectedWorldRevision = authorityShell.WorldRevision;
                }
                var playerKey = 공간LHCellKey.FromWorldPosition(
                    focusTarget.position.x, focusTarget.position.z,
                    originWorldPosition.x, originWorldPosition.z);
                var focusKey = !string.IsNullOrWhiteSpace(pinnedFocusCellKey)
                    ? pinnedFocusCellKey
                    : initial ? playerKey : DesiredPrefetchFocus(playerKey);
                var epoch = "lh-request:" + Interlocked.Increment(ref requestSequence);
                var request = new 공간LHCellPreviewRequestData
                {
                    RequestEpoch = epoch,
                    SessionStableId = sessionStableId,
                    RecipeStableId = 공간LHWorldCodes.RecipeStableId,
                    AreaSetStableId = 공간LHWorldCodes.AreaSetStableId,
                    FocusL3CellKey = focusKey,
                    MovementDirectionCode = movementDirectionCode,
                    RequiredCapabilityCodes = Array.Empty<string>(),
                    KnownCellPlanHashesSha256 = cells.Values
                        .Where(value => value.Plan != null)
                        .Select(value => value.Plan.BasePlanHashSha256).ToArray(),
                    ExpectedWorldRevision = expectedWorldRevision,
                };
                requestedFocusCellKey = focusKey;
                var response = await repository.PreviewCellsAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (epoch != "lh-request:" + Interlocked.Read(ref requestSequence)) return;
                response.Validate(epoch);
                acceptedEpoch = epoch;
                expectedWorldRevision = response.WorldRevision;
                profile = response.Profile;
                activeContentSourceCode = response.ContentSourceCode;
                ApplySeason(response.Season);
                QueueResponse(response, epoch);
                lastError = string.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                Interlocked.Decrement(ref requestsInFlight);
            }
        }

        private async Task RequestNpcWindowAsync(
            string focusCellKey, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestsInFlight);
            var epoch = "lh-npc-request:" + Interlocked.Increment(ref npcRequestSequence);
            try
            {
                var request = new 공간LHCellPreviewRequestData
                {
                    RequestEpoch = epoch,
                    SessionStableId = sessionStableId,
                    RecipeStableId = 공간LHWorldCodes.RecipeStableId,
                    AreaSetStableId = 공간LHWorldCodes.AreaSetStableId,
                    FocusL3CellKey = focusCellKey,
                    MovementDirectionCode = 공간LHWorldCodes.None,
                    RequiredCapabilityCodes = new[]
                    {
                        공간LHWorldCodes.TerrainVisual,
                        공간LHWorldCodes.Collision,
                        공간LHWorldCodes.Connector,
                        공간LHWorldCodes.NpcNavigation,
                    },
                    KnownCellPlanHashesSha256 = cells.Values
                        .Where(value => value.Plan != null)
                        .Select(value => value.Plan.BasePlanHashSha256).ToArray(),
                    ExpectedWorldRevision = expectedWorldRevision,
                };
                var response = await repository.PreviewCellsAsync(request, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                response.Validate(epoch);
                lock (assemblyLock)
                {
                    acceptedNpcEpochs.Add(epoch);
                    foreach (var plan in response.Cells.OrderBy(value => value.Priority))
                    {
                        var captured = plan;
                        mainThreadAssembly.Enqueue(() => ApplyPlan(captured, epoch, true));
                    }
                    mainThreadAssembly.Enqueue(() =>
                    {
                        lock (assemblyLock) acceptedNpcEpochs.Remove(epoch);
                        requestedNpcCellKey = string.Empty;
                    });
                }
                lastError = string.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                requestedNpcCellKey = string.Empty;
            }
            catch (Exception exception)
            {
                requestedNpcCellKey = string.Empty;
                lastError = exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                Interlocked.Decrement(ref requestsInFlight);
            }
        }

        private string DesiredPrefetchFocus(string current)
        {
            if (!공간LHCellKey.TryParseL3(current, out var x, out var y)) return current;
            var centerX = originWorldPosition.x
                          + (x - 공간LHWorldCodes.CenterL3X) * l3CellWorldSize;
            var centerZ = originWorldPosition.z
                          + (y - 공간LHWorldCodes.CenterL3Y) * l3CellWorldSize;
            var nx = (focusTarget.position.x - centerX) / l3CellWorldSize;
            var nz = (focusTarget.position.z - centerZ) / l3CellWorldSize;
            var edge = .5f - (float)(profile?.BoundaryPrefetchFraction ?? .25d);
            if (nx >= edge) x++;
            else if (nx <= -edge) x--;
            if (nz >= edge) y++;
            else if (nz <= -edge) y--;
            return 공간LHCellKey.L3(x, y);
        }

        private void QueueResponse(공간LHCellPreviewData response, string epoch)
        {
            var responseKeys = new HashSet<string>(
                response.Cells.Select(value => value.CellKey), StringComparer.Ordinal);
            lock (assemblyLock)
            {
                // 최신 창이 승인되면 아직 적용하지 않은 이전 창의 조립 작업은 버린다.
                mainThreadAssembly.Clear();
                foreach (var plan in response.Cells.OrderBy(value => value.Priority))
                {
                    var captured = plan;
                    mainThreadAssembly.Enqueue(() => ApplyPlan(captured, epoch));
                }
                mainThreadAssembly.Enqueue(() => CacheOutsideWindow(responseKeys, epoch));
            }
        }

        private void DrainAssemblyBudget(double budgetMilliseconds)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalMilliseconds <= budgetMilliseconds)
            {
                Action action;
                lock (assemblyLock)
                {
                    if (mainThreadAssembly.Count == 0) break;
                    action = mainThreadAssembly.Dequeue();
                }
                action();
            }
        }

        private void ApplyPlan(공간LHCellPlanData plan, string epoch, bool npcWindow = false)
        {
            bool npcEpochAccepted;
            lock (assemblyLock) npcEpochAccepted = acceptedNpcEpochs.Contains(epoch);
            if (npcWindow ? !npcEpochAccepted : epoch != acceptedEpoch) return;
            if (!cells.TryGetValue(plan.CellKey, out var cell))
            {
                cell = AcquireCell(plan.CellKey);
                cells.Add(plan.CellKey, cell);
            }
            cell.State = 공간LHCellPreparationState.Requested;
            cell.Plan = plan;
            cell.State = 공간LHCellPreparationState.DependenciesReady;
            PositionCell(cell, plan);
            cell.State = 공간LHCellPreparationState.GeneratedDataReady;
            cell.ReadyCapabilities.Clear();
            cell.ReadyCapabilities.Add(공간LHWorldCodes.TerrainVisual);

            var shouldTraverse = plan.PlayerTraversalRequired
                                 || plan.RequiredCapabilityCodes.Contains(
                                     공간LHWorldCodes.NpcNavigation);
            EnsureFloor(cell, shouldTraverse);
            if (shouldTraverse)
            {
                cell.ReadyCapabilities.Add(공간LHWorldCodes.Collision);
                cell.ReadyCapabilities.Add(공간LHWorldCodes.Connector);
            }
            EnsurePlacements(cell, plan.WindowRoleCode == 공간LHWorldCodes.Detail);
            cell.State = 공간LHCellPreparationState.VisualPrepared;

            if (plan.HBindings.Any(value => value.HLevelCode == "H1"))
                cell.ReadyCapabilities.Add(공간LHWorldCodes.H1Interaction);
            if (plan.WindowRoleCode == 공간LHWorldCodes.Detail)
            {
                cell.ReadyCapabilities.Add(공간LHWorldCodes.SeasonPresentation);
                // 실제 NavMesh 빌드는 별도 능력으로 남기되, 시각·충돌 준비와 섞지 않는다.
            }
            if (plan.RequiredCapabilityCodes.Contains(공간LHWorldCodes.NpcNavigation)
                && cell.ReadyCapabilities.Contains(공간LHWorldCodes.Collision)
                && cell.ReadyCapabilities.Contains(공간LHWorldCodes.Connector))
                cell.ReadyCapabilities.Add(공간LHWorldCodes.NpcNavigation);
            if (shouldTraverse)
                cell.State = 공간LHCellPreparationState.PlayerTraversalReady;
            cell.State = 공간LHCellPreparationState.Active;
            cell.Root.SetActive(plan.WindowRoleCode != 공간LHWorldCodes.Prefetch);
        }

        private CellRuntime AcquireCell(string cellKey)
        {
            CellRuntime cell;
            if (pooledCells.Count > 0)
            {
                cell = pooledCells.Dequeue();
                cell.CellKey = cellKey;
                cell.Root.name = "LHCell_" + cellKey.Replace(':', '_');
                cell.Root.SetActive(true);
            }
            else
            {
                var root = new GameObject("LHCell_" + cellKey.Replace(':', '_'));
                root.transform.SetParent(generatedCellRoot, false);
                cell = new CellRuntime(cellKey, root);
            }
            return cell;
        }

        private void PositionCell(CellRuntime cell, 공간LHCellPlanData plan)
        {
            cell.Root.transform.position = originWorldPosition + new Vector3(
                (plan.CellX - 공간LHWorldCodes.CenterL3X) * l3CellWorldSize,
                0f,
                (plan.CellY - 공간LHWorldCodes.CenterL3Y) * l3CellWorldSize);
        }

        private void EnsureFloor(CellRuntime cell, bool shouldExist)
        {
            if (!shouldExist)
            {
                if (cell.Floor != null) cell.Floor.SetActive(false);
                return;
            }
            if (cell.Floor == null)
            {
                cell.Floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.Floor.name = "PlayerTraversalFloor";
                cell.Floor.transform.SetParent(cell.Root.transform, false);
                var renderer = cell.Floor.GetComponent<Renderer>();
                renderer.sharedMaterial = SharedSeasonMaterial();
            }
            cell.Floor.SetActive(true);
            cell.Floor.transform.localPosition = new Vector3(0f, -.15f, 0f);
            cell.Floor.transform.localScale = new Vector3(l3CellWorldSize, .2f, l3CellWorldSize);
        }

        private void EnsurePlacements(CellRuntime cell, bool shouldExist)
        {
            foreach (var placement in cell.PlacementObjects)
                if (placement != null) Destroy(placement);
            cell.PlacementObjects.Clear();
            if (!shouldExist || compositionCatalog == null) return;
            foreach (var placement in cell.Plan.Placements)
            {
                try
                {
                    var entry = compositionCatalog.Resolve(placement.CompositionKey);
                    if (entry.Prefab == null) continue;
                    var instance = Instantiate(entry.Prefab, cell.Root.transform);
                    instance.name = "LHPlacement_" + placement.GeneratedStableId;
                    instance.transform.localPosition = new Vector3(
                        (float)placement.LocalXMeters, 0f, (float)placement.LocalZMeters);
                    instance.transform.localRotation = Quaternion.Euler(
                        0f, (float)placement.RotationDegrees, 0f);
                    instance.transform.localScale = Vector3.one * (float)placement.UniformScale;
                    cell.PlacementObjects.Add(instance);
                }
                catch (InvalidOperationException)
                {
                    // 누락된 로컬 시각 에셋은 권위 데이터와 충돌 준비를 막지 않는다.
                }
            }
        }

        private void CacheOutsideWindow(HashSet<string> activeKeys, string epoch)
        {
            if (epoch != acceptedEpoch) return;
            foreach (var pair in cells.Where(value => !activeKeys.Contains(value.Key)).ToArray())
            {
                pair.Value.State = 공간LHCellPreparationState.Cached;
                pair.Value.Root.SetActive(false);
            }
            var capacity = profile?.CachedCellCapacity ?? 32;
            var cached = cells.Values.Where(value => value.State == 공간LHCellPreparationState.Cached)
                .OrderBy(value => value.LastTouchedSequence).ToArray();
            for (var index = 0; index < cached.Length - capacity; index++)
                ReleaseCell(cached[index]);
        }

        private void ReleaseCell(CellRuntime cell)
        {
            cells.Remove(cell.CellKey);
            foreach (var placement in cell.PlacementObjects)
                if (placement != null) Destroy(placement);
            cell.PlacementObjects.Clear();
            cell.Plan = null!;
            cell.ReadyCapabilities.Clear();
            cell.State = 공간LHCellPreparationState.Released;
            cell.Root.SetActive(false);
            pooledCells.Enqueue(cell);
        }

        private void ApplySeason(공간LHSeasonData season)
        {
            activeSeasonCode = season.SeasonCode;
            activeSeasonDay = season.SeasonDay;
            Shader.SetGlobalFloat(SeasonIndex, season.SeasonIndex);
            Shader.SetGlobalFloat(SeasonProgress, (float)season.SeasonProgress01);
            var material = SharedSeasonMaterial();
            if (material != null) material.color = SeasonColor(season.SeasonCode);
        }

        private Material SharedSeasonMaterial()
        {
            const string materialName = "LHSharedSeasonGround";
            var existing = Resources.FindObjectsOfTypeAll<Material>()
                .FirstOrDefault(value => value.name == materialName);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null) return null;
            return new Material(shader) { name = materialName, color = SeasonColor(activeSeasonCode) };
        }

        private static Color SeasonColor(string code)
            => code == 공간LHWorldCodes.Summer ? new Color(.28f, .48f, .19f)
                : code == 공간LHWorldCodes.Autumn ? new Color(.55f, .36f, .13f)
                : code == 공간LHWorldCodes.Winter ? new Color(.72f, .77f, .78f)
                : new Color(.42f, .58f, .27f);

        private void UpdateStatus()
        {
            if (statusLabel == null) return;
            var traversable = cells.Values.Count(value =>
                value.State >= 공간LHCellPreparationState.PlayerTraversalReady);
            var sourceLabel = SourceModeCode == 공간LHWorldCodes.LocalEngine
                ? "로컬 싱글 플레이 생성"
                : SourceModeCode == 공간TileStreamingCodes.SimulationServer
                    ? "시뮬레이션 서버 생성" : SourceModeCode;
            var contentLabel = activeContentSourceCode == 공간LHWorldCodes.AuthoritativeWorld
                ? "권위 있는 실제 세계"
                : "시나리오 절차생성";
            statusLabel.text = string.IsNullOrWhiteSpace(lastError)
                ? $"LH 오픈 월드 · {sourceLabel} · {contentLabel} · L3 125m · {activeSeasonCode} {activeSeasonDay}/28\n"
                  + $"플레이어 {playerCellKey} · 준비 {traversable}/{cells.Count} · 조립 대기 {PendingAssemblyCount}"
                : "LH 오픈 월드 준비 실패\n" + lastError;
        }

        private static string Direction(string previous, string current)
        {
            if (!공간LHCellKey.TryParseL3(previous, out var px, out var py)
                || !공간LHCellKey.TryParseL3(current, out var cx, out var cy))
                return 공간LHWorldCodes.None;
            var dx = Math.Sign(cx - px);
            var dy = Math.Sign(cy - py);
            if (dx == 0 && dy > 0) return "N";
            if (dx > 0 && dy > 0) return "NE";
            if (dx > 0 && dy == 0) return "E";
            if (dx > 0 && dy < 0) return "SE";
            if (dx == 0 && dy < 0) return "S";
            if (dx < 0 && dy < 0) return "SW";
            if (dx < 0 && dy == 0) return "W";
            return dx < 0 && dy > 0 ? "NW" : 공간LHWorldCodes.None;
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }

        private sealed class CellRuntime
        {
            public string CellKey;
            public readonly GameObject Root;
            public GameObject Floor;
            public 공간LHCellPlanData Plan = null!;
            public 공간LHCellPreparationState State;
            public readonly HashSet<string> ReadyCapabilities = new(StringComparer.Ordinal);
            public readonly List<GameObject> PlacementObjects = new();
            public long LastTouchedSequence;

            public CellRuntime(string key, GameObject root)
            {
                CellKey = key;
                Root = root;
            }
        }
    }
}
