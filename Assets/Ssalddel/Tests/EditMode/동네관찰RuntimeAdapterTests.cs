using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Warehouse;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 동네관찰RuntimeAdapterTests
    {
        [UnityTest]
        public IEnumerator 저장검증은_프레임을_막지않고_같은슬롯으로_복원된다()
        {
            var folder = Path.GetFullPath(Path.Combine("artifacts", "observer-phone-tests", Guid.NewGuid().ToString("N")));
            using var runtime = LocalSimulationUnityRuntime.Create(folder);
            var initial = runtime.Sessions.CreateAsync(가상배달관찰표본.CreateNeighborhoodLife(Guid.NewGuid())).AsTask().GetAwaiter().GetResult();
            var state = initial;
            for (int tick = 0; tick < 200; tick++)
                state = runtime.Sessions.AdvanceWorldTickAsync(initial.SessionStableId, new 경영SimulationTick진행Request {
                    CommandId = "command:phone-worker-test:" + tick, ExpectedRevision = state.Revision, TickCount = 1 }).AsTask().GetAwaiter().GetResult();
            var adapter = new 동네관찰RuntimeAdapter(runtime, state.SessionStableId, 가상배달관찰표본.NeighborhoodLifeSlotId);
            var save = adapter.저장Async(state.Revision);
            int frames = 0; var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!save.IsCompleted && DateTime.UtcNow < deadline) { frames++; yield return null; }
            Assert.That(save.IsCompleted, Is.True, "저장 제한 시간"); save.GetAwaiter().GetResult();
            Assert.That(frames, Is.GreaterThan(0), "동기 파일/Replay 검증으로 Unity 프레임을 막으면 안 된다");
            var verify = runtime.Sessions.VerifySlotAsync(가상배달관찰표본.NeighborhoodLifeSlotId).AsTask().GetAwaiter().GetResult();
            Assert.That(verify.Restore.Session.CurrentTick, Is.EqualTo(200));
            var query = adapter.조회Async();
            while (!query.IsCompleted && DateTime.UtcNow < deadline) yield return null;
            Assert.That(query.IsCompleted, Is.True); Assert.That(query.Result.Revision, Is.EqualTo(state.Revision));
        }
    }
}
