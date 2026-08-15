using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class 진부Hub입고UiPlayModeTests
    {
        [UnityTest]
        public IEnumerator 저장Scene의버튼으로_검수부터적재완료까지수직흐름을진행한다()
        {
            var removedServerCompositions = 0;
            void RemoveLiveServerCompositions(Scene loadedScene, LoadSceneMode mode)
            {
                foreach (var value in UnityEngine.Object.FindObjectsByType<턴마감SceneCompositionRoot>(
                             FindObjectsInactive.Include))
                {
                    UnityEngine.Object.DestroyImmediate(value);
                    removedServerCompositions++;
                }
                foreach (var value in UnityEngine.Object.FindObjectsByType<진부Hub입고UiSceneCompositionRoot>(
                             FindObjectsInactive.Include))
                {
                    UnityEngine.Object.DestroyImmediate(value);
                    removedServerCompositions++;
                }
            }

            SceneManager.sceneLoaded += RemoveLiveServerCompositions;
            yield return SceneManager.LoadSceneAsync(
                "SimulationWorldShell", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= RemoveLiveServerCompositions;
            Assert.That(removedServerCompositions, Is.GreaterThanOrEqualTo(2));

            var presenter = UnityEngine.Object.FindAnyObjectByType<진부Hub입고UiPresenter>(
                FindObjectsInactive.Include);
            Assert.That(presenter, Is.Not.Null);
            presenter.ForceVisibleForTests(true);
            yield return Await(presenter.InitializeAsync(
                new 진부Hub입고UiFixtureAuthorityClient(),
                SimulationWorldShellFixture.SessionStableId));

            var panel = GameObject.Find(
                "SimulationWorldShell/PersistentUI/SimulationWorldHud/JinbuInboundPanel");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.activeInHierarchy, Is.True);

            var preview = RequiredButton(panel, "PreviewButton");
            var confirm = RequiredButton(panel, "ConfirmButton");
            var tick = RequiredButton(panel, "TickButton");

            Assert.That(preview.interactable, Is.True);
            preview.onClick.Invoke();
            yield return WaitUntil(
                () => presenter.CurrentPhaseCode == 진부Hub입고UiCodes.PreviewReady,
                "입고 검수 미리보기");
            Assert.That(confirm.interactable, Is.True);

            confirm.onClick.Invoke();
            yield return WaitUntil(
                () => presenter.CurrentProjection?.StateCode == 진부Hub입고UiCodes.InProgress,
                "입고 검수 확정");
            yield return ClickTicksUntil(
                tick, presenter,
                value => value.WorkflowStageCode == "PutAwayPending",
                "적재 대기");

            Assert.That(preview.interactable, Is.True);
            preview.onClick.Invoke();
            yield return WaitUntil(
                () => presenter.CurrentPhaseCode == 진부Hub입고UiCodes.PreviewReady,
                "적재 미리보기");
            confirm.onClick.Invoke();
            yield return WaitUntil(
                () => presenter.CurrentProjection?.StateCode == 진부Hub입고UiCodes.InProgress,
                "적재 확정");
            yield return ClickTicksUntil(
                tick, presenter,
                value => value.StateCode == 진부Hub입고UiCodes.Completed,
                "적재 완료");

            Assert.That(presenter.CurrentProjection.WorkflowStageCode,
                Is.EqualTo("PutAwayCompleted"));
            Assert.That(panel.GetComponentsInChildren<Text>(true)
                    .Any(value => value.text.Contains("적재 완료")),
                Is.True);

        }

        private static Button RequiredButton(GameObject panel, string name)
        {
            var child = panel.transform.Find(name);
            Assert.That(child, Is.Not.Null, name + " 배선 누락");
            return child.GetComponent<Button>();
        }

        private static IEnumerator ClickTicksUntil(
            Button tick,
            진부Hub입고UiPresenter presenter,
            Func<진부Hub입고UiProjectionData, bool> completed,
            string label)
        {
            for (var index = 0; index < 8 && !completed(presenter.CurrentProjection); index++)
            {
                Assert.That(tick.interactable, Is.True, label + " 전 WorldTick 버튼 비활성");
                var revision = presenter.CurrentProjection.StateRevision;
                tick.onClick.Invoke();
                yield return WaitUntil(
                    () => presenter.CurrentProjection.StateRevision > revision,
                    label + " WorldTick");
            }
            Assert.That(completed(presenter.CurrentProjection), Is.True, label + " 미도달");
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
                throw task.Exception?.GetBaseException() ?? new InvalidOperationException("비동기 검증 실패");
            if (task.IsCanceled) throw new OperationCanceledException("비동기 검증 취소");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string label)
        {
            var startedAt = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - startedAt > 5f)
                    Assert.Fail(label + " 상태 전이 제한 시간 초과");
                yield return null;
            }
        }
    }
}
