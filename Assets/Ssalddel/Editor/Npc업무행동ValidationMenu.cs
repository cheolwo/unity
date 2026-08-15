using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    public static class Npc업무행동ValidationMenu
    {
        private const string TargetTestClass =
            "Ssalddel.Unity.Tests.EditMode.Npc업무행동Tests";

        [MenuItem("Ssalddel/Validation/NPC-WORKFORCE-1 진부 Hub 집중 검증 _F6")]
        public static void RunFocusedTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCallbacks());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[] { TargetTestClass },
            }));
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
                => Debug.Log($"NPC-WORKFORCE-1 집중 검증 시작: {testsToRun.TestCaseCount}개");

            public void RunFinished(ITestResultAdaptor result)
                => Debug.Log(
                    $"NPC-WORKFORCE-1 집중 검증 완료: "
                    + $"통과 {result.PassCount}, 실패 {result.FailCount}, "
                    + $"건너뜀 {result.SkipCount}");

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                    Debug.LogError(
                        $"NPC-WORKFORCE-1 실패: {result.FullName}\n{result.Message}");
            }
        }
    }
}
