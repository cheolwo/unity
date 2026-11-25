using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정개인용운영조회차단Tests
    {
        private const string ProfileVariable = "SSALDDEL_SAGAJEONG_FOOD_LIFE";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> roots = new();
        private string previousProfile;

        [SetUp]
        public void 환경보존()
        {
            previousProfile = Environment.GetEnvironmentVariable(ProfileVariable);
        }

        [TearDown]
        public void 환경복구()
        {
            try
            {
                foreach (var root in roots)
                    if (root != null) UnityEngine.Object.DestroyImmediate(root);
                roots.Clear();
            }
            finally { Environment.SetEnvironmentVariable(ProfileVariable, previousProfile); }
        }

        [Test]
        public void 사가정의_Awake_초기화_Enable_Update는_HTTP객체를만들지않는다()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, "1");
            foreach (var entry in new[] { "Awake", "InitializeClient", "OnEnable", "Update" })
            {
                var controller = Controller(entry);
                Invoke(controller, entry);
                Blocked(controller);
                Assert.That(controller.GetComponent<다중Os생명주기검증Layer>(), Is.Null, entry);
                Assert.That(controller.GetComponent<생활관찰표현Layer>(), Is.Null, entry);
            }
        }

        [Test]
        public void 사가정에서_재활성화와직접재호출도_같은차단을유지한다()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, "1");
            var controller = Controller("sagajeong-reenable");
            Invoke(controller, "Awake");
            for (var attempt = 0; attempt < 3; attempt++)
            {
                controller.enabled = true;
                Invoke(controller, "OnEnable");
                Invoke(controller, "InitializeClient");
                Invoke(controller, "Update");
                Blocked(controller);
            }
        }

        [Test]
        public void 기존V2초기화뒤_사가정전환은_대기조회취소와객체해제를수행한다()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, null);
            Assert.That(SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested, Is.False,
                "이 시험은 설치된 Runtime이 없는 격리 EditMode에서 실행합니다.");
            var controller = Controller("existing-v2-to-local");
            Invoke(controller, "InitializeClient");
            Assert.That(Field(controller, "transport"), Is.Not.Null);
            Assert.That(Field(controller, "client"), Is.Not.Null);
            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            SetField(controller, "lifetime", cancellation);
            SetField(controller, "pendingRefresh", Task.FromResult(default(OperationalWorldSceneApplyResult)));
            SetField(controller, "cursor", 37L);

            Environment.SetEnvironmentVariable(ProfileVariable, "1");
            Invoke(controller, "Update");

            Assert.That(token.IsCancellationRequested, Is.True);
            Blocked(controller);
        }

        [Test]
        public void 사가정플래그를제거하면_명시재활성화의기존V2초기화를보존한다()
        {
            Environment.SetEnvironmentVariable(ProfileVariable, "1");
            var controller = Controller("existing-v2-after-profile");
            Invoke(controller, "Awake");
            Blocked(controller);
            Environment.SetEnvironmentVariable(ProfileVariable, null);
            Assert.That(SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested, Is.False,
                "이 시험은 설치된 Runtime이 없는 격리 EditMode에서 실행합니다.");
            controller.enabled = true;
            Invoke(controller, "OnEnable");

            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.StatusCode, Is.EqualTo("WaitingForConnection"));
            Assert.That(Field(controller, "transport"), Is.Not.Null);
            Assert.That(Field(controller, "client"), Is.Not.Null);
            Assert.That(Field(controller, "planner"), Is.Not.Null);
            Assert.That(Field(controller, "lifetime"), Is.Not.Null);
            Assert.That(Field(controller, "pendingRefresh"), Is.Null);
            Assert.That(controller.Cursor, Is.Zero);
        }

        private 운영관찰WorldController Controller(string name)
        {
            var root = new GameObject(name);
            roots.Add(root);
            root.SetActive(false);
            root.AddComponent<운영관찰WorldView>();
            return root.AddComponent<운영관찰WorldController>();
        }

        private static void Blocked(운영관찰WorldController controller)
        {
            Assert.That(controller.StatusCode, Is.EqualTo("LocalOnly"));
            Assert.That(controller.enabled, Is.False);
            Assert.That(controller.Cursor, Is.Zero);
            foreach (var field in new[] { "transport", "client", "planner", "lifetime", "pendingRefresh" })
                Assert.That(Field(controller, field), Is.Null, field);
        }

        private static object Field(object target, string name)
            => target.GetType().GetField(name, PrivateInstance).GetValue(target);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        private static void Invoke(object target, string name)
            => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    }
}
