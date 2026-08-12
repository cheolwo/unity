#if SSALDDEL_UNITY_TEST_FRAMEWORK
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class 감자생산유통통합Tests
    {
        [Test]
        public void 단계선택은한Canvas와카메라초점만전환한다()
        {
            var root = new GameObject("감자생산유통통합Test");
            try
            {
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                var canvases = new Canvas[6];
                var targets = new Transform[6];
                var labels = new[] { "재배·수확", "수확·포장·상차", "농장 출발·거점 이동", "입고·검수", "판로 분배", "도시 도착" };
                for (var index = 0; index < 6; index++)
                {
                    canvases[index] = new GameObject("Canvas_" + index).AddComponent<Canvas>();
                    canvases[index].transform.SetParent(root.transform, false);
                    targets[index] = new GameObject("Target_" + index).transform;
                    targets[index].SetParent(root.transform, false);
                    targets[index].position = new Vector3(index * 10f, 0f, index);
                }

                var stageText = new GameObject("StageText").AddComponent<Text>();
                stageText.transform.SetParent(root.transform, false);
                var demonstrationText = new GameObject("DemonstrationText").AddComponent<Text>();
                demonstrationText.transform.SetParent(root.transform, false);
                var lineage = new GameObject("Lineage").AddComponent<Text>();
                lineage.transform.SetParent(root.transform, false);
                var presenter = root.AddComponent<감자생산유통통합Presenter>();
                presenter.Configure(camera, canvases, targets, labels, stageText, demonstrationText, lineage,
                    new Vector3(-3f, 4f, -5f));

                presenter.SelectStage(5);

                Assert.That(presenter.ValidateWiring(), Is.True);
                Assert.That(presenter.CurrentStageIndex, Is.EqualTo(5));
                Assert.That(presenter.CurrentStageLabel, Is.EqualTo("도시 도착"));
                Assert.That(presenter.ActiveStageCanvasCount(), Is.EqualTo(1));
                Assert.That(canvases[5].gameObject.activeSelf, Is.True);
                Assert.That(stageText.text, Does.Contain("6/6"));
                Assert.That(lineage.text, Does.Contain("product:potato"));
                Assert.That(camera.transform.position,
                    Is.EqualTo(targets[5].position + new Vector3(-3f, 4f, -5f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 통합Scene은여섯단계와기존계보Root를보존한다()
        {
            Assert.That(File.Exists(감자생산유통통합Builder.ScenePath), Is.True);
            EditorSceneManager.OpenScene(감자생산유통통합Builder.ScenePath, OpenSceneMode.Single);
            감자생산유통통합Builder.ValidateOpenScene();

            var presenter = GameObject.Find("WorldBootstrap/" + 감자생산유통통합Builder.RootName)
                ?.GetComponent<감자생산유통통합Presenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter!.StageCount, Is.EqualTo(6));
            Assert.That(presenter.ValidateDemonstrationWiring(), Is.True);
            Assert.That(presenter.GetComponentsInChildren<감자생산유통단계Button>(true).Length,
                Is.EqualTo(6));
            presenter.SelectStage(5);
            Assert.That(presenter.CurrentStageLabel, Is.EqualTo("도시 도착"));
            Assert.That(presenter.ActiveStageCanvasCount(), Is.EqualTo(1));
        }

        [Test]
        public void 통합Scene경로는감자생산유통폴더에있다()
        {
            Assert.That(감자생산유통통합Builder.ScenePath,
                Is.EqualTo(연구Scene경로.감자생산유통 + "/감자생산유통통합흐름.unity"));
        }
    }
}
#endif
