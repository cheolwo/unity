using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Solo LocalProcess가 설치된 공식 World에서 범위 밖 원격 요청 회귀를 검증한다.",
        Boundary = "로컬 능력 대기는 DailyWork 완료나 E7 플레이 완료를 뜻하지 않는다.")]
    public sealed class 오늘작업계획LocalProcessPlayModeTests
    {
        [UnityTest]
        public IEnumerator SoloLocalProcess에서는_오늘작업원격요청을시작하지않는다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-daily-work-local-wait-" + Guid.NewGuid().ToString("N"));
            GameObject? rootObject = null;
            Environment.SetEnvironmentVariable("SSALDDEL_NATURE_E7_VALIDATION", null);
            try
            {
                SimulationWorldLocalRuntimeScope.Install(
                    Nature생존LaunchProfile.PlayableDefault, savesRoot);
                rootObject = new GameObject("오늘작업계획LocalProcess회귀");
                var composition = rootObject.AddComponent<오늘작업계획SceneCompositionRoot>();

                LogAssert.Expect(LogType.Log,
                    "DailyWorkPlanLocalSimulationCapabilityWaiting:network=0");
                yield return null;

                Assert.That(composition.enabled, Is.False);
                Assert.That(composition.서버기준사용중, Is.False);
            }
            finally
            {
                if (rootObject != null) UnityEngine.Object.DestroyImmediate(rootObject);
                SimulationWorldLocalRuntimeScope.Reset();
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }
    }
}
