using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Runtime.Configuration;
using Ssalddel.Unity.Runtime.Identity;
using Ssalddel.Unity.Runtime.Ledgers;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Application.Bootstrap
{
    public sealed class WorldBootstrapRequest
    {
        public string WorldId { get; set; } = string.Empty;
        public long ExpectedWorldRevision { get; set; }
    }

    public sealed class WorldBootstrapResult
    {
        public UnitySessionSnapshot Session { get; set; }
        public WorldSnapshot World { get; set; }
        public UnityLedgerProjection[] Ledgers { get; set; } = Array.Empty<UnityLedgerProjection>();
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class WorldBootstrapUseCase
    {
        private readonly UnityClientRuntimeOptions _options;
        private readonly IUnitySessionRepository _sessionRepository;
        private readonly WorldManager _worldManager;
        private readonly IUnityLedgerProjectionRepository _ledgerRepository;

        public WorldBootstrapUseCase(
            UnityClientRuntimeOptions options,
            IUnitySessionRepository sessionRepository,
            WorldManager worldManager,
            IUnityLedgerProjectionRepository ledgerRepository)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
            _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
        }

        public async Task<WorldBootstrapResult> ExecuteAsync(
            WorldBootstrapRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _options.Validate();
            var session = await _sessionRepository.LoadCurrentAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Unity session을 불러오지 못했습니다.");
            session.Validate();

            var world = await _worldManager.LoadAsync(
                    new WorldLoadContext
                    {
                        WorldId = request.WorldId,
                        ExpectedRevision = request.ExpectedWorldRevision,
                        ExecutionMode = _options.ExecutionMode,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var ledgers = await _ledgerRepository
                    .ListVisibleAsync(session, world.WorldId, cancellationToken)
                    .ConfigureAwait(false)
                ?? Array.Empty<UnityLedgerProjection>();
            foreach (var ledger in ledgers)
            {
                ledger.Validate();
                if (!string.Equals(ledger.ViewerRoleCode, session.RoleCode, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("현재 역할 범위를 벗어난 원장 projection입니다.");
                }
            }

            if (string.Equals(_options.ExecutionMode, UnityExecutionModeCodes.Operational, StringComparison.Ordinal)
                && (string.Equals(session.SourceCode, UnityDataSourceCodes.Fixture, StringComparison.Ordinal)
                    || world.Fragments.Any(x => string.Equals(x.SourceCode, UnityDataSourceCodes.Fixture, StringComparison.Ordinal))
                    || ledgers.Any(x => string.Equals(x.SourceCode, UnityDataSourceCodes.Fixture, StringComparison.Ordinal))))
            {
                throw new InvalidOperationException("Operational bootstrap에는 fixture 상태를 사용할 수 없습니다.");
            }

            return new WorldBootstrapResult
            {
                Session = session,
                World = world,
                Ledgers = ledgers,
            };
        }
    }
}
