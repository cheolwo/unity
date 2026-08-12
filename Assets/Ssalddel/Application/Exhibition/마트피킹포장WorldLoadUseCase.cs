using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Application.Exhibition
{
    public sealed class 마트피킹포장WorldLoadUseCase
    {
        private readonly I마트피킹포장WorldRepository repository;
        private readonly 마트피킹포장WorldStateStore stateStore;

        public 마트피킹포장WorldLoadUseCase(
            I마트피킹포장WorldRepository worldRepository,
            마트피킹포장WorldStateStore store)
        {
            repository = worldRepository ?? throw new ArgumentNullException(nameof(worldRepository));
            stateStore = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<마트피킹포장WorldSnapshot> ExecuteAsync(
            long warehouseId,
            CancellationToken cancellationToken)
        {
            if (warehouseId <= 0)
                throw new ArgumentOutOfRangeException(nameof(warehouseId));

            var loaded = await repository.LoadAsync(warehouseId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return stateStore.Accept(warehouseId, loaded);
        }
    }
}
