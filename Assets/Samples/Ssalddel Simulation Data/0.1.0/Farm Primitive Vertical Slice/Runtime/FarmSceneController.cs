using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Farm;
using UnityEngine;
using VContainer;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmSceneController : MonoBehaviour
    {
        private FarmProducerPerspectiveQueryUseCase query = null!;
        private FarmProducerPerspectiveApplicator applicator = null!;
        private FarmView zoneView = null!;
        private CancellationTokenSource? lifetime;

        [Inject]
        public void Construct(
            FarmProducerPerspectiveQueryUseCase perspectiveQuery,
            FarmProducerPerspectiveApplicator perspectiveApplicator,
            FarmView view)
        {
            query = perspectiveQuery;
            applicator = perspectiveApplicator;
            zoneView = view;
        }

        private void Awake() => lifetime = new CancellationTokenSource();
        private async void Start() => await InitializeAsync();

        public async Task InitializeAsync()
        {
            if (!zoneView.ValidateWiring())
            {
                Debug.LogError("Farm View wiring is invalid.", this);
                return;
            }

            zoneView.ShowLoading();
            try
            {
                var snapshot = await query.실행Async(lifetime!.Token);
                var unresolved = zoneView.Render(snapshot, applicator);
                if (unresolved.Length > 0)
                {
                    Debug.LogWarning("Farm target missing: " + string.Join(", ", unresolved), this);
                }
            }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true)
            {
            }
            catch (Exception exception)
            {
                zoneView.ShowError(exception.Message);
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
