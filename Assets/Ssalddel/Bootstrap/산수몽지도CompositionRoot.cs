using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class 산수몽지도CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 산수몽지도Presenter presenter = null!;
        [SerializeField] private string actorStableId = "actor:sim:player-survivor";
        private 산수몽지도ServerRepository? repository;
        private CancellationTokenSource? lifetime;

        public void Configure(UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell,
            산수몽지도Presenter mapPresenter, string actorId)
        {
            runtimeSettings = settings; shell = worldShell;
            presenter = mapPresenter; actorStableId = actorId;
        }

        private void Awake()
        {
            shell ??= FindFirstObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            presenter ??= FindFirstObjectByType<산수몽지도Presenter>(
                FindObjectsInactive.Include);
        }

        private async void Start()
        {
            if (runtimeSettings == null || shell == null || presenter == null
                || string.IsNullOrWhiteSpace(actorStableId))
                throw new InvalidOperationException("MengMapCompositionWiringInvalid");
            repository = new 산수몽지도ServerRepository(
                new SimulationRehearsalUnityWebRequestApiClient(
                    runtimeSettings.ToOptions()));
            shell.AuthoritativeSnapshotApplied += OnWorldSnapshotApplied;
            await RefreshAsync();
        }

        private async void OnWorldSnapshotApplied(SimulationWorldShellSnapshot _)
        {
            try { await RefreshAsync(); }
            catch (Exception error) { Debug.LogWarning(error.Message, this); }
        }

        public async Task RefreshAsync()
        {
            if (repository == null || string.IsNullOrWhiteSpace(shell.SessionStableId))
                return;
            lifetime?.Cancel(); lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            var exploration = await repository.LoadAsync(shell.SessionStableId,
                actorStableId, lifetime.Token);
            presenter.ApplyExploration(exploration,
                shell.CurrentSnapshot?.GameDateLabel ?? "절기 미확인");
        }

        private void OnDestroy()
        {
            if (shell != null)
                shell.AuthoritativeSnapshotApplied -= OnWorldSnapshotApplied;
            lifetime?.Cancel(); lifetime?.Dispose();
        }
    }
}
