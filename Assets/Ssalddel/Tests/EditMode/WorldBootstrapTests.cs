using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Application.Bootstrap;
using Ssalddel.Unity.Data;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Runtime.Configuration;
using Ssalddel.Unity.Runtime.Identity;
using Ssalddel.Unity.Runtime.Ledgers;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class WorldBootstrapTests
    {
        [Test]
        public void LocalPackage의_StableId와_DataManager를_참조한다()
        {
            Assert.That(StableDataId.IsValid("scenario:potato-basic-kr-001"), Is.True);
            Assert.That(typeof(DataManager).Assembly.GetName().Name, Is.EqualTo("Ssalddel.Unity.Data"));
        }

        [Test]
        public void Operational은_Fixture허용설정을_거부한다()
        {
            var options = new UnityClientRuntimeOptions
            {
                OperationalApiBaseUrl = "https://api.example.test",
                SimulationRehearsalApiBaseUrl = "http://simulation.example.test",
                ExecutionMode = UnityExecutionModeCodes.Operational,
                AllowFixtureData = true,
            };

            Assert.Throws<InvalidOperationException>(options.Validate);
        }

        [Test]
        public void 운영ApiClient는_예행연습서버주소에_의존하지않는다()
        {
            var options = new UnityClientRuntimeOptions
            {
                OperationalApiBaseUrl = "https://api.example.test",
                SimulationRehearsalApiBaseUrl = "invalid-rehearsal-address",
            };

            Assert.DoesNotThrow(() => new OperationalUnityWebRequestApiClient(options));
        }

        [Test]
        public void 예행연습ApiClient는_운영서버주소에_의존하지않는다()
        {
            var options = new UnityClientRuntimeOptions
            {
                OperationalApiBaseUrl = "invalid-operational-address",
                SimulationRehearsalApiBaseUrl = "http://simulation.example.test",
            };

            Assert.DoesNotThrow(() => new SimulationRehearsalUnityWebRequestApiClient(options));
        }

        [Test]
        public void ApiTransport는_절대주소와_경로이탈을_거부한다()
        {
            Assert.Throws<InvalidOperationException>(() => new UnityApiRequest
            {
                RelativePath = "https://example.test/api/v1/community/posts",
            }.Validate());
            Assert.Throws<InvalidOperationException>(() => new UnityApiRequest
            {
                RelativePath = "/api/../private",
            }.Validate());

            Assert.DoesNotThrow(() => new UnityApiRequest
            {
                RelativePath = "/api/v1/community/posts",
            }.Validate());
        }

        [Test]
        public void WorldManager는_서로다른_Revision을_거부한다()
        {
            var manager = new WorldManager(new IWorldStateProvider[]
            {
                new StubWorldProvider(Fragment(7, "world:clock")),
                new StubWorldProvider(Fragment(6, "world:market")),
            });

            Assert.ThrowsAsync<InvalidOperationException>(() => manager.LoadAsync(new WorldLoadContext
            {
                WorldId = "world:ssalddel-local",
                ExpectedRevision = 7,
                ExecutionMode = UnityExecutionModeCodes.Simulation,
            }));
        }

        [Test]
        public async Task Bootstrap은_역할과_World와_원장Revision을_보존한다()
        {
            var session = new UnitySessionSnapshot
            {
                SessionId = "session:local-001",
                UserStableId = "user:local-001",
                RoleCode = UnityRoleCodes.Orderer,
                Revision = 3,
                SourceCode = UnityDataSourceCodes.Fixture,
            };
            var manager = new WorldManager(new IWorldStateProvider[]
            {
                new StubWorldProvider(Fragment(7, "world:clock")),
                new StubWorldProvider(Fragment(7, "world:market")),
            });
            var ledger = new UnityLedgerProjection
            {
                LedgerId = "ledger:order-001",
                LedgerTypeCode = "Order",
                SubjectStableId = "product:potato-001",
                WorldObjectStableId = "world-object:potato-crate-001",
                StatusCode = "Draft",
                ViewerRoleCode = UnityRoleCodes.Orderer,
                Revision = 11,
                SourceCode = UnityDataSourceCodes.Fixture,
            };
            var useCase = new WorldBootstrapUseCase(
                new UnityClientRuntimeOptions
                {
                    OperationalApiBaseUrl = "http://localhost:5239",
                    SimulationRehearsalApiBaseUrl = "http://localhost:5204",
                    ExecutionMode = UnityExecutionModeCodes.Simulation,
                    AllowFixtureData = true,
                },
                new StubSessionRepository(session),
                manager,
                new StubLedgerRepository(ledger));

            var result = await useCase.ExecuteAsync(new WorldBootstrapRequest
            {
                WorldId = "world:ssalddel-local",
                ExpectedWorldRevision = 7,
            });

            Assert.That(result.Session.RoleCode, Is.EqualTo(UnityRoleCodes.Orderer));
            Assert.That(result.World.Revision, Is.EqualTo(7));
            Assert.That(result.Ledgers, Has.Length.EqualTo(1));
            Assert.That(result.Ledgers[0].Revision, Is.EqualTo(11));
            Assert.That(result.World.ExecutionMode, Is.EqualTo(UnityExecutionModeCodes.Simulation));
        }

        private static WorldStateFragment Fragment(long revision, string providerKey)
            => new WorldStateFragment
            {
                WorldId = "world:ssalddel-local",
                WorldRevision = revision,
                ProviderKey = providerKey,
                SourceCode = UnityDataSourceCodes.Fixture,
                ObservedAt = DateTimeOffset.UtcNow,
                SeasonCode = "Summer",
                WorldTime = DateTimeOffset.UtcNow,
                EvidenceIds = new[] { "evidence:fixture-001" },
            };

        private sealed class StubSessionRepository : IUnitySessionRepository
        {
            private readonly UnitySessionSnapshot _session;
            public StubSessionRepository(UnitySessionSnapshot session) => _session = session;
            public Task<UnitySessionSnapshot> LoadCurrentAsync(CancellationToken cancellationToken)
                => Task.FromResult(_session);
        }

        private sealed class StubWorldProvider : IWorldStateProvider
        {
            private readonly WorldStateFragment _fragment;
            public StubWorldProvider(WorldStateFragment fragment) => _fragment = fragment;
            public Task<WorldStateFragment> LoadAsync(WorldLoadContext context, CancellationToken cancellationToken)
                => Task.FromResult(_fragment);
        }

        private sealed class StubLedgerRepository : IUnityLedgerProjectionRepository
        {
            private readonly UnityLedgerProjection[] _ledgers;
            public StubLedgerRepository(params UnityLedgerProjection[] ledgers) => _ledgers = ledgers;
            public Task<UnityLedgerProjection[]> ListVisibleAsync(
                UnitySessionSnapshot session,
                string worldId,
                CancellationToken cancellationToken)
                => Task.FromResult(_ledgers);
        }
    }
}
