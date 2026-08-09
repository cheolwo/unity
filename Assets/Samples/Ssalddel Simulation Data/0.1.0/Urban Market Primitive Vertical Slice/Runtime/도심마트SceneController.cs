using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.UrbanMarket;
using UnityEngine;
using VContainer;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class 도심마트SceneController : MonoBehaviour
    {
        private I도심마트조회UseCase marketQuery = null!;
        private 도심마트ScreenModelValidator validator = null!;
        private 도심마트View marketView = null!;
        private CancellationTokenSource? lifetime;
        private readonly object initializationSync = new object();
        private Task? activeInitialization;

        [Inject]
        public void Construct(
            I도심마트조회UseCase query,
            도심마트ScreenModelValidator modelValidator,
            도심마트View view)
        {
            marketQuery = query;
            validator = modelValidator;
            marketView = view;
        }

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        public Task InitializeAsync()
        {
            lock (initializationSync)
            {
                if (activeInitialization != null && !activeInitialization.IsCompleted)
                {
                    return activeInitialization;
                }

                activeInitialization = InitializeCoreAsync();
                return activeInitialization;
            }
        }

        private async Task InitializeCoreAsync()
        {
            if (marketView == null || !marketView.ValidateWiring())
            {
                Debug.LogError("도심마트View wiring이 완료되지 않았습니다.", this);
                return;
            }

            marketView.ShowLoading();
            try
            {
                var model = await marketQuery.조회Async(lifetime!.Token);
                var errors = validator.Validate(model);
                if (errors.Length > 0)
                {
                    var message = string.Join(", ", errors);
                    marketView.ShowError(message);
                    Debug.LogError("도심마트 ScreenModel invalid: " + message, this);
                    return;
                }

                marketView.Render(model, HandleProductSelected);
            }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true)
            {
            }
            catch (Exception exception)
            {
                marketView.ShowError(exception.Message);
                Debug.LogException(exception, this);
            }
        }

        private void HandleProductSelected(도심마트상품ScreenModel product)
        {
            marketView.OpenProductDetail(product);
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
        }
    }
}
