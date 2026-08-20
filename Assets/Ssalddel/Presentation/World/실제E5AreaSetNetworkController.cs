using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// canonical SimulationWorldShell에서 실제 E5 Network payload를 조립한다.
    /// 키 입력은 표현 지역만 전환하며 서버 업무 확정이나 WorldTick을 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 실제E5AreaSetNetworkController : MonoBehaviour
    {
        [SerializeField] private Transform areaSetRoot = null!;
        [SerializeField] private Transform routeGraphRoot = null!;
        [SerializeField] private 공간문법CompositionCatalog compositionCatalog = null!;
        [SerializeField] private 공간문법SyntyBindingCatalog syntyBindingCatalog = null!;
        [SerializeField] private 실제E5AreaSetNetworkHudPresenter hud = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField, Min(1f)] private float compressedTileWorldSize = 24f;

        private readonly Dictionary<string, Transform> areaRoots =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, GameObject>> graphRoots =
            new Dictionary<string, Dictionary<string, GameObject>>(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> routeRoots =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private 실제E5AreaSetNetworkStreamingSession session;
        private 공간AreaSetLandscapeGraphRuntimeAssembler assembler;
        private bool switching;

        public bool Initialized => session?.Network != null;
        public string ActiveAreaSetStableId => session?.ActiveAreaSetStableId ?? string.Empty;
        public 실제E5AreaSetNetworkHudPresenter Hud => hud;

        public void Configure(Transform areas, Transform routes,
            공간문법CompositionCatalog catalog,
            공간문법SyntyBindingCatalog bindingCatalog,
            실제E5AreaSetNetworkHudPresenter hudPresenter,
            float tileWorldSize = 24f)
        {
            areaSetRoot = areas;
            routeGraphRoot = routes;
            compositionCatalog = catalog;
            syntyBindingCatalog = bindingCatalog;
            hud = hudPresenter;
            compressedTileWorldSize = tileWorldSize;
        }

        public void BindShell(SimulationWorldShellPresenter worldShell)
        {
            UnsubscribeShell();
            shell = worldShell;
            SubscribeShell();
        }

        private void OnEnable() => SubscribeShell();

        private void OnDisable() => UnsubscribeShell();

        public async Task InitializeAsync(
            I실제E5AreaSetNetworkRepository networkRepository,
            I공간AreaSetLandscapeGraphRepository graphRepository,
            CancellationToken cancellationToken = default)
        {
            ValidateWiring();
            assembler = syntyBindingCatalog == null
                ? new 공간AreaSetLandscapeGraphRuntimeAssembler(
                    compositionCatalog, compressedTileWorldSize)
                : new 공간AreaSetLandscapeGraphRuntimeAssembler(
                    compositionCatalog, syntyBindingCatalog, compressedTileWorldSize);
            session = new 실제E5AreaSetNetworkStreamingSession(
                networkRepository, graphRepository);
            var batch = await session.InitializeAsync(cancellationToken);
            EnsureAreaRoots(batch.Network);
            Apply(batch);
            hud.ShowRegionalCausality(shell?.CurrentSnapshot?.RegionalCausality
                                      ?? new 실제E5RegionalCausalityData());
        }

        public async Task SwitchAreaAsync(
            string areaSetStableId,
            CancellationToken cancellationToken = default)
        {
            if (switching) return;
            if (!Initialized)
                throw new InvalidOperationException("ActualE5AreaSetNetworkNotInitialized");
            switching = true;
            try
            {
                var batch = await session.ActivateAreaAsync(
                    areaSetStableId, cancellationToken);
                Apply(batch);
            }
            finally
            {
                switching = false;
            }
        }

        public void ApplyRegionalCausality(실제E5RegionalCausalityData state) =>
            hud.ShowRegionalCausality(state);

        public void ShowUnavailable(string reason)
        {
            if (hud != null) hud.ShowUnavailable(reason);
        }

        private async void Update()
        {
            if (!Initialized || switching) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            string target = null;
            if (keyboard.digit1Key.wasPressedThisFrame)
                target = 실제E5AreaSetNetworkCodes.NatureAreaSet;
            else if (keyboard.digit2Key.wasPressedThisFrame)
                target = 실제E5AreaSetNetworkCodes.FarmAreaSet;
            else if (keyboard.digit3Key.wasPressedThisFrame)
                target = 실제E5AreaSetNetworkCodes.HubAreaSet;
            else if (keyboard.digit4Key.wasPressedThisFrame)
                target = 실제E5AreaSetNetworkCodes.TownAreaSet;
            if (target == null || target == ActiveAreaSetStableId) return;
            try
            {
                await SwitchAreaAsync(target);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ShowUnavailable("지역 전환 실패 · 서버 상태를 확인하세요");
            }
        }

        private void Apply(실제E5AreaSetNetworkStreamingBatch batch)
        {
            foreach (var areaBatch in batch.AreaBatches)
            {
                var areaId = areaBatch.AreaSet.AreaSetStableId;
                공간AreaSetLandscapeGraphRuntimeAssembler.ApplyBatch(
                    areaBatch,
                    graphRoots[areaId],
                    graph => assembler.BuildStaging(
                        graph, areaRoots[areaId], areaBatch.Index.CenterTileKey));
            }
            foreach (var route in batch.LoadedRouteGraphs.OrderBy(
                         value => value.LandscapeGraphStableId, StringComparer.Ordinal))
            {
                var staging = assembler.BuildStaging(
                    route, routeGraphRoot, route.TileRefs.First());
                routeRoots.TryGetValue(route.LandscapeGraphStableId, out var current);
                공간AreaSetLandscapeGraphRuntimeAssembler.CommitAtomic(
                    ref current, staging, true);
                routeRoots[route.LandscapeGraphStableId] = current;
            }
            hud.Show(batch);
        }

        private void EnsureAreaRoots(실제E5AreaSetNetworkData network)
        {
            foreach (var area in network.AreaSets)
            {
                if (areaRoots.ContainsKey(area.AreaSetStableId)) continue;
                var root = new GameObject("AreaSet_" + SafeName(area.AreaRoleCode)).transform;
                root.SetParent(areaSetRoot, false);
                root.localPosition = AreaOffset(area.AreaSetStableId);
                areaRoots.Add(area.AreaSetStableId, root);
                graphRoots.Add(area.AreaSetStableId,
                    new Dictionary<string, GameObject>(StringComparer.Ordinal));
            }
        }

        private void ValidateWiring()
        {
            if (areaSetRoot == null || routeGraphRoot == null
                || compositionCatalog == null || hud == null
                || compressedTileWorldSize <= 0f)
                throw new InvalidOperationException("ActualE5AreaSetNetworkWiringMissing");
        }

        private void SubscribeShell()
        {
            if (shell == null) return;
            shell.AuthoritativeSnapshotApplied -= HandleAuthoritativeSnapshot;
            shell.AuthoritativeSnapshotApplied += HandleAuthoritativeSnapshot;
        }

        private void UnsubscribeShell()
        {
            if (shell != null)
                shell.AuthoritativeSnapshotApplied -= HandleAuthoritativeSnapshot;
        }

        private void HandleAuthoritativeSnapshot(SimulationWorldShellSnapshot snapshot)
        {
            if (snapshot?.RegionalCausality != null && hud != null)
                hud.ShowRegionalCausality(snapshot.RegionalCausality);
        }

        private static Vector3 AreaOffset(string stableId)
        {
            if (stableId == 실제E5AreaSetNetworkCodes.NatureAreaSet)
                return new Vector3(-45f, 0f, 0f);
            if (stableId == 실제E5AreaSetNetworkCodes.FarmAreaSet)
                return Vector3.zero;
            if (stableId == 실제E5AreaSetNetworkCodes.HubAreaSet)
                return new Vector3(45f, 0f, 0f);
            return new Vector3(90f, 0f, 0f);
        }

        private static string SafeName(string value) =>
            value.Replace(':', '_').Replace('/', '_');
    }
}
