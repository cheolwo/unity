using System;
using System.Threading.Tasks;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Observation;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Unity.Infrastructure.Simulation
{
    /// <summary>기존 전용 Session/슬롯을 연결한다. 휴대폰용 원장을 만들지 않는다.</summary>
    public sealed class 동네관찰RuntimeAdapter : I동네관찰SessionPort
    {
        private readonly LocalSimulationRuntime runtime;
        private readonly string slotId;
        private string sessionId;
        public 동네관찰RuntimeAdapter(SimulationWorldLocalRuntimeScope scope, string sessionId)
            : this(scope.Runtime, sessionId, scope.SlotStableId) { }
        public 동네관찰RuntimeAdapter(LocalSimulationRuntime runtime, string sessionId, string slotId)
        { this.runtime = runtime; this.sessionId = sessionId; this.slotId = slotId; }
        // Runtime의 ValueTask는 gate가 비어 있으면 동기 실행된다. 순수 Core/파일 작업만
        // worker로 보내고 Controller의 await 이후 Unity 컨텍스트에서 화면을 갱신한다.
        // Runtime commandGate + 관찰 Controller gate로 순서와 매 Tick 저장을 유지한다.
        public Task<경영SimulationSessionSnapshot> 조회Async() => Task.Run(async () => await runtime.Sessions.GetAsync(sessionId));
        public async Task 진행Async(long revision) => await Task.Run(async () => await runtime.Sessions.AdvanceWorldTickAsync(sessionId,
            new 경영SimulationTick진행Request { CommandId = "command:observer:" + Guid.NewGuid().ToString("N"), ExpectedRevision = revision, TickCount = 1 }));
        public async Task 정책Async(SimulationNpcPolicyChangeRequest request) => await Task.Run(async () => await runtime.UpdateNpcPolicyAsync(sessionId, request));
        public async Task 저장Async(long revision) => await Task.Run(async () => await runtime.Sessions.SaveSlotAsync(sessionId,
            new SimulationLocalSaveSlotRequest { SlotStableId = slotId, ExpectedRevision = revision }));
        public async Task 재시작Async()
        {
            var state = await Task.Run(async () => await runtime.Sessions.CreateAsync(가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid(), dayEnabled: true)));
            sessionId = state.SessionStableId;
        }
    }
}
