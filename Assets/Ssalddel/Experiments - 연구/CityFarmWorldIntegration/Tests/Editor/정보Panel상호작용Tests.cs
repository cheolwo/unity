using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Tests.Editor
{
    public sealed class 정보Panel상호작용Tests
    {
        [Test]
        public void 펼침_접힘_닫힘_다시열기_상태를_구분한다()
        {
            var root = new GameObject("TestRoot");
            try
            {
                var controller = root.AddComponent<정보Panel상호작용Controller>();
                var panel = Child(root, "Panel");
                var controls = Child(root, "Controls");
                var expandTab = Child(root, "ExpandTab");
                var reopenTab = Child(root, "ReopenTab");
                var collapse = Child(controls, "Collapse").AddComponent<Button>();
                var close = Child(controls, "Close").AddComponent<Button>();
                var expand = expandTab.AddComponent<Button>();
                var reopen = reopenTab.AddComponent<Button>();

                controller.Configure(panel, controls, expandTab, reopenTab,
                    collapse, close, expand, reopen);

                Assert.That(controller.ValidateWiring(), Is.True);
                AssertState(controller, 정보Panel표시상태.펼침, panel, controls, expandTab, reopenTab);
                collapse.onClick.Invoke();
                AssertState(controller, 정보Panel표시상태.접힘, panel, controls, expandTab, reopenTab);
                expand.onClick.Invoke();
                AssertState(controller, 정보Panel표시상태.펼침, panel, controls, expandTab, reopenTab);
                close.onClick.Invoke();
                AssertState(controller, 정보Panel표시상태.닫힘, panel, controls, expandTab, reopenTab);
                reopen.onClick.Invoke();
                AssertState(controller, 정보Panel표시상태.펼침, panel, controls, expandTab, reopenTab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var value = new GameObject(name);
            value.transform.SetParent(parent.transform, false);
            return value;
        }

        private static void AssertState(
            정보Panel상호작용Controller controller,
            정보Panel표시상태 expected,
            GameObject panel,
            GameObject controls,
            GameObject expandTab,
            GameObject reopenTab)
        {
            Assert.That(controller.CurrentState, Is.EqualTo(expected));
            Assert.That(panel.activeSelf, Is.EqualTo(expected == 정보Panel표시상태.펼침));
            Assert.That(controls.activeSelf, Is.EqualTo(expected == 정보Panel표시상태.펼침));
            Assert.That(expandTab.activeSelf, Is.EqualTo(expected == 정보Panel표시상태.접힘));
            Assert.That(reopenTab.activeSelf, Is.EqualTo(expected == 정보Panel표시상태.닫힘));
        }
    }
}
