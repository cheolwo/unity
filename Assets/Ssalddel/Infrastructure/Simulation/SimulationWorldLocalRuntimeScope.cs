using System;
using System.Linq;
using Newtonsoft.Json;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>
    /// canonical World의 Solo 영역들이 같은 Runtime과 Session을 사용하게 하는 실행 범위입니다.
    /// GameObject 상태가 아니라 공통 Session Aggregate가 권위를 가집니다.
    /// </summary>
    public sealed class SimulationWorldLocalRuntimeScope : IDisposable
    {
        private static readonly object Gate = new();
        private static SimulationWorldLocalRuntimeScope? current;

        private SimulationWorldLocalRuntimeScope(LocalSimulationRuntime runtime,
            string sessionStableId)
        {
            Runtime = runtime;
            SessionStableId = sessionStableId;
        }

        public LocalSimulationRuntime Runtime { get; }
        public string SessionStableId { get; }
        public static bool IsInstalled => current != null;

        public static SimulationWorldLocalRuntimeScope Install()
        {
            lock (Gate)
            {
                if (current != null) return current;
                var runtime = LocalSimulationUnityRuntime.Create();
                try
                {
                    var request = CreateSessionRequest();
                    var created = runtime.Sessions.CreateAsync(request).AsTask()
                        .GetAwaiter().GetResult();
                    current = new SimulationWorldLocalRuntimeScope(runtime,
                        created.SessionStableId);
                    return current;
                }
                catch
                {
                    runtime.Dispose();
                    throw;
                }
            }
        }

        public static bool TryGet(out SimulationWorldLocalRuntimeScope scope)
        {
            lock (Gate)
            {
                scope = current!;
                return scope != null;
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                current?.Dispose();
                current = null;
            }
        }

        private static 경영SimulationSession생성Request CreateSessionRequest()
        {
            var request = JsonConvert.DeserializeObject<경영SimulationSession생성Request>(
                JsonConvert.SerializeObject(
                    턴마감ServerAuthorityRepository.CreateSessionRequest()))
                ?? throw new InvalidOperationException(
                    "SimulationLocalSessionFixtureInvalid");
            var nature = Nature생존LocalEngine.CreateDefaultFixture();
            request.ScenarioStableId = "scenario:unity.solo-local-world";
            request.ScenarioDataRevision = "simulation-data:solo-local-world:1";
            // Farm 공간·수확 상태는 Solo 물류가 같은 Session의 포장 Lot 계보를
            // 사용하도록 보존한다. 통합 시설 투영만 아직 별도 이관 대상이다.
            request.IntegratedWorld = null;
            request.NatureSurvival = new SimulationNatureSurvivalInitialStateRequest
            {
                PlayerStableId = "player:session:owner",
                StartsWithAxe = false,
                ResourceNodes = nature.ResourceNodes.Select(node =>
                    new SimulationNatureResourceNodeInitialStateRequest
                    {
                        ResourceNodeStableId = node.StableId,
                        H2StableId = node.H2StableId,
                        H1StableId = node.H1StableId,
                        LocalX = node.LocalX,
                        LocalZ = node.LocalZ,
                    }).ToArray(),
            };
            return request;
        }

        public void Dispose() => Runtime.Dispose();
    }
}
