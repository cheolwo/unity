using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Unity 소비 계약·결정성·Save/Replay 또는 회귀 증거를 검증한다.",
        Boundary = "자동 시험과 코드 검증을 실제 Play Mode·Game View 증거와 구분한다.")]
    public sealed class 턴카드모판Tests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/턴카드모판.unity";

        [Test]
        public void TURN_CARD_SEEDBED_UI1_현재세후보를두모판과독립Gate로분류한다()
        {
            var catalog = 턴카드모판CatalogData.CreateCurrentFixture();
            var philosophy = catalog.FindByNursery(턴카드모판Code.철학학당);
            var culture = catalog.FindByNursery(턴카드모판Code.지역문화);

            Assert.That(catalog.Entries, Has.Length.EqualTo(3));
            Assert.That(philosophy.Count, Is.EqualTo(2));
            Assert.That(culture.Count, Is.EqualTo(1));
            Assert.That(catalog.Entries, Has.All.Matches<턴카드모판EntryData>(value =>
                value.Gates.Length == 7 && !value.게시완료));
            Assert.That(catalog.Entries, Has.All.Matches<턴카드모판EntryData>(value =>
                value.Gates.Single(gate => gate.Code == "C2").StatusCode
                    == 턴카드승격상태Code.차단));
            Assert.That(catalog.Entries, Has.All.Matches<턴카드모판EntryData>(value =>
                value.Gates.Single(gate => gate.Code == "C3").StatusCode
                    == 턴카드승격상태Code.Fixture검증));
        }

        [Test]
        public void TURN_CARD_SEEDBED_UI1_Scene은게임덱과분리된연구화면이다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single(value => value.name == "턴카드모판");
                var presenter = root.GetComponent<턴카드모판Presenter>();
                var buttons = root.GetComponentsInChildren<Button>(true);

                Assert.That(presenter, Is.Not.Null);
                Assert.That(presenter!.턴확정제공여부, Is.False);
                Assert.That(presenter.연구Revision, Is.Zero);
                Assert.That(root.GetComponentInChildren<턴마감Presenter>(true), Is.Null);
                Assert.That(buttons.Any(value => value.name.Contains("Confirm")
                    || value.name.Contains("Preview")), Is.False);
                var inputModules = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Select(value => value.GetType().Name).ToArray();
                Assert.That(inputModules, Does.Contain("InputSystemUIInputModule"));
                Assert.That(inputModules, Does.Not.Contain("StandaloneInputModule"));
                Assert.That(root.transform.Find("턴카드모판Canvas/ResearchOnlyFooter"), Is.Not.Null);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TURN_CARD_SEEDBED_UI1_모판전환은연구Revision을바꾸지않는다()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var presenter = scene.GetRootGameObjects().Single(value => value.name == "턴카드모판")
                    .GetComponent<턴카드모판Presenter>();
                presenter.Initialize();
                var before = presenter.연구Revision;

                presenter.Execute(턴카드모판ActionCode.지역문화보기);

                Assert.That(presenter.현재모판Code, Is.EqualTo(턴카드모판Code.지역문화));
                Assert.That(presenter.현재후보수, Is.EqualTo(1));
                Assert.That(presenter.연구Revision, Is.EqualTo(before));
                Assert.That(presenter.턴확정제공여부, Is.False);
            }
            finally
            {
                if (scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
