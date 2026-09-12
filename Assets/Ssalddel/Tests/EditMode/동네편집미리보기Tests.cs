using System;
using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public class 동네편집미리보기Tests
    {
        [TestCase("5", "바닥", 90f, 58f)]
        [TestCase("6", "업무 서버 관찰 바닥", 120f, 96f)]
        public void 같은배치와카메라만_조립하고_Runtime과Api를_설치하지않는다(string profile, string floorName, float width, float depth)
        {
            var old = Environment.GetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY");
            var root = new GameObject("편집 미리보기 시험"); root.SetActive(false);
            try
            {
                Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", profile);
                var controller = root.AddComponent<가상배달관찰Controller>(); controller.enabled = false;
                controller.편집공간조립();
                var floor = root.transform.Find(floorName);
                Assert.That(floor.localScale.x, Is.EqualTo(width)); Assert.That(floor.localScale.z, Is.EqualTo(depth));
                foreach (var field in new[] { "scope", "state", "업무서버Api", "업무서버Store", "업무서버취소" })
                    Assert.That(typeof(가상배달관찰Controller).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.Null, field);
                var camera = root.GetComponentInChildren<Camera>(true);
                Assert.That(camera, Is.Not.Null); Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.transform.forward.x, Is.GreaterThan(.4f));
                Assert.That(controller.enabled, Is.False);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Has.All.Matches<Collider>(x => !x.enabled));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Environment.SetEnvironmentVariable("SSALDDEL_SYNTHETIC_DELIVERY", old); }
        }
    }
}
