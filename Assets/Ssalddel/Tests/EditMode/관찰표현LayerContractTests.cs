using System.Reflection;
using NUnit.Framework;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 관찰표현LayerContractTests
    {
        [Test]
        public void 사가정과오프라인생활은_서로다른전용Layer와Mask를사용한다()
        {
            Assert.That(관찰표현LayerContract.사가정운영관찰Layer, Is.InRange(8, 31));
            Assert.That(관찰표현LayerContract.오프라인생활Layer, Is.InRange(8, 31));
            Assert.That(관찰표현LayerContract.사가정운영관찰Layer,
                Is.Not.EqualTo(관찰표현LayerContract.오프라인생활Layer));
            Assert.That(관찰표현LayerContract.사가정운영관찰Mask
                        & 관찰표현LayerContract.오프라인생활Mask, Is.Zero);
            Assert.That(사가정운영디오라마View.ObservationLayer,
                Is.EqualTo(관찰표현LayerContract.사가정운영관찰Layer));

            var offlineLayer = typeof(오프라인생활Controller).GetField("ViewLayer",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(offlineLayer, Is.Not.Null);
            Assert.That(offlineLayer.GetRawConstantValue(),
                Is.EqualTo(관찰표현LayerContract.오프라인생활Layer));
        }

        [Test]
        public void 오프라인생활점유검사는_사가정표현을충돌로보지않고_자기Layer만차단한다()
        {
            var sagajeong = new GameObject("sagajeong-layer-owner")
                { layer = 관찰표현LayerContract.사가정운영관찰Layer };
            var offline = new GameObject("offline-layer-owner")
                { layer = 관찰표현LayerContract.오프라인생활Layer };
            try
            {
                var occupancy = typeof(오프라인생활Controller).GetMethod("ViewLayerOccupied",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(occupancy, Is.Not.Null);
                Assert.That(occupancy.Invoke(null, new object[]
                {
                    new[] { sagajeong.transform }
                }), Is.False);
                Assert.That(occupancy.Invoke(null, new object[]
                {
                    new[] { sagajeong.transform, offline.transform }
                }), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(sagajeong);
                Object.DestroyImmediate(offline);
            }
        }
    }
}
