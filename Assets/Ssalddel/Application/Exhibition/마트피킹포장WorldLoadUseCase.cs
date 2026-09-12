using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Application.Exhibition
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
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
