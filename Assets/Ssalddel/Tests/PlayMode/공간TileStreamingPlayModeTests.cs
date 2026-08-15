using System.Collections;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 공간TileStreamingPlayModeTests
    {
        [TearDown]
        public void TearDown()
            => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator SimulationWorldShell에서_플레이어이동은_타일창만바꾸고_World상태는바꾸지않는다()
        {
            // 통합 Scene의 별도 서버 권위 루트가 로컬 서버 상태에 따라 연결 오류를 남길 수 있다.
            // 이 검증은 해당 루트가 아니라 fixture 타일·시야 표현과 World 상태 불변성을 직접 단언한다.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("SimulationWorldShell", LoadSceneMode.Single);
            var shell = Object.FindAnyObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
            var streaming = Object.FindAnyObjectByType<공간TileStreamingController>(
                FindObjectsInactive.Include);
            var visibility = Object.FindAnyObjectByType<공간시야ObjectStreamingController>(
                FindObjectsInactive.Include);
            var diagnostic = Object.FindAnyObjectByType<공간StreamingTreeDiagnosticPresenter>(
                FindObjectsInactive.Include);
            Assert.That(shell, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(streaming, Is.Not.Null);
            Assert.That(visibility, Is.Not.Null);
            Assert.That(diagnostic, Is.Not.Null);

            var timeout = Time.realtimeSinceStartup + 5f;
            while (!streaming.IsInitialized || streaming.PreparedTileCount != 81
                   || !visibility.IsInitialized || visibility.LoadedObjectCount != 5)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(timeout),
                    "동적 타일 초기화 시간 초과");
                yield return null;
            }

            var tick = shell.WorldTick;
            var revision = shell.WorldRevision;
            Assert.That(streaming.DetailTileCount, Is.EqualTo(9));
            Assert.That(streaming.ActiveTileCount, Is.EqualTo(25));
            Assert.That(streaming.WaitingTileCount, Is.EqualTo(81));
            Assert.That(streaming.SourceModeCode, Is.EqualTo("Fixture"));

            player.EnterFirstPersonMode();
            const string barn = "scenario-object:pyeongchang-farm:barn-a";
            var barnPosition = visibility.GetWorldPosition(barn);
            Assert.That(barnPosition, Is.Not.Null);
            player.FirstPersonCamera.transform.LookAt(barnPosition.Value);
            timeout = Time.realtimeSinceStartup + 5f;
            while (visibility.GetState(barn) != 공간시야Object상태.DetailActive)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(timeout),
                    "1인칭 시야 건물 상세 승격 시간 초과");
                yield return null;
            }
            diagnostic.RefreshNow();
            Assert.That(visibility.ActualVisibleCount, Is.GreaterThan(0));
            Assert.That(visibility.ActiveCameraName, Is.EqualTo(player.FirstPersonCamera.name));
            Assert.That(diagnostic.IsPanelVisible, Is.True);
            Assert.That(diagnostic.TreeTextContent, Does.Contain("건물 표현 승격"));
            Assert.That(diagnostic.TreeTextContent, Does.Contain("Synty 상세"));
            Assert.That(visibility.PresentationOnly, Is.True);

            player.transform.position += Vector3.right * 24.1f;
            timeout = Time.realtimeSinceStartup + 5f;
            while (streaming.CurrentCenterX != 701 || streaming.OutsideCoverageCount != 0)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(timeout),
                    "타일 중심 이동 시간 초과");
                yield return null;
            }

            Assert.That(streaming.PreparedTileCount, Is.EqualTo(81));
            Assert.That(streaming.DetailTileCount, Is.EqualTo(9));
            Assert.That(streaming.ActiveTileCount, Is.EqualTo(25));
            Assert.That(streaming.WaitingTileCount, Is.EqualTo(81));
            Assert.That(streaming.ObservedWorldTick, Is.Zero);
            Assert.That(streaming.ObservedActivityRevision, Is.Zero);
            Assert.That(shell.WorldTick, Is.EqualTo(tick));
            Assert.That(shell.WorldRevision, Is.EqualTo(revision));
        }
    }
}
