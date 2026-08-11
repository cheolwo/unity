#if SSALDDEL_UNITY_TEST_FRAMEWORK
using NUnit.Framework;
using Ssalddel.Unity.Learning;
using UnityEngine;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class EveningHakdangViewTests
    {
        [Test]
        public void EVENING1_Builder는밤학습화면을배선한다()
        {
            Editor.EveningHakdangBuilder.Build();
            Editor.EveningHakdangBuilder.ValidateOpenScene();
            var presenter = GameObject.Find(Editor.EveningHakdangBuilder.RootName)
                .GetComponent<EveningHakdangPresenter>();
            Assert.That(presenter.ValidateWiring(), Is.True);
            Assert.That(presenter.CurrentSnapshot.DayPhaseCode, Is.EqualTo(하루단계Codes.EveningStudy));
        }

        [Test]
        public void EVENING1_완료경로는다음아침효과를표시한다()
        {
            Editor.EveningHakdangBuilder.Build();
            var presenter = GameObject.Find(Editor.EveningHakdangBuilder.RootName)
                .GetComponent<EveningHakdangPresenter>();
            presenter.RunFoolStudyPath();
            Assert.That(presenter.CurrentSnapshot.DayPhaseCode, Is.EqualTo(하루단계Codes.Day));
            Assert.That(presenter.CurrentSnapshot.InnerState.알아차림, Is.EqualTo(1));
            Assert.That(presenter.CurrentSnapshot.InnerState.ActiveRuleCodes,
                Does.Contain(내면규칙Codes.BeginnerMind));
        }

        [Test]
        public void EVENING2_오전상차행동은저녁전차학습으로이어진다()
        {
            Editor.EveningHakdangBuilder.Build();
            var presenter = GameObject.Find(Editor.EveningHakdangBuilder.RootName)
                .GetComponent<EveningHakdangPresenter>();
            presenter.RunChariotStudyPath();
            Assert.That(presenter.CurrentSnapshot.InnerState.의지, Is.EqualTo(1));
            Assert.That(presenter.CurrentSnapshot.InnerState.ActiveRuleCodes,
                Does.Contain(내면규칙Codes.IntegratedProgress));
            Assert.That(presenter.CurrentSnapshot.InnerState.알아차림, Is.EqualTo(0));
        }
    }
}
#endif
