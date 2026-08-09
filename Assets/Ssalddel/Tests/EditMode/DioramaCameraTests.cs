using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class DioramaCameraTests
    {
        [Test]
        public void StateMachine은_90도단계회전과PitchZoom범위를지킨다()
        {
            var settings = new DioramaCameraSettings();
            var stateMachine = new DioramaCameraStateMachine(settings, Focus(
                "camera-focus:world.overview",
                DioramaCameraFocusLevelCodes.World,
                0f, 0f, 0f));

            stateMachine.RotateQuarterTurns(-1);
            stateMachine.SetPitch(100f);
            stateMachine.Zoom(1000f);

            Assert.That(stateMachine.State.YawQuarterTurns, Is.EqualTo(3));
            Assert.That(stateMachine.State.Pitch, Is.EqualTo(settings.MaxPitch));
            Assert.That(stateMachine.State.Distance, Is.EqualTo(settings.MaxDistance));

            stateMachine.RotateQuarterTurns(2);
            stateMachine.Zoom(-1000f);

            Assert.That(stateMachine.State.YawQuarterTurns, Is.EqualTo(1));
            Assert.That(stateMachine.State.Distance, Is.EqualTo(settings.MinDistance));
        }

        [Test]
        public void Focus는_WorldZoneObject의Presentation거리만바꾼다()
        {
            var settings = new DioramaCameraSettings();
            var stateMachine = new DioramaCameraStateMachine(settings, Focus(
                "camera-focus:world.overview",
                DioramaCameraFocusLevelCodes.World,
                0f, 0f, 0f));

            stateMachine.Focus(Focus(
                "camera-focus:zone.farm",
                DioramaCameraFocusLevelCodes.Zone,
                -20f, 0f, 12f));

            Assert.That(stateMachine.State.FocusAnchorId, Is.EqualTo("camera-focus:zone.farm"));
            Assert.That(stateMachine.State.FocusPoint.X, Is.EqualTo(-20f));
            Assert.That(stateMachine.State.Distance, Is.EqualTo(settings.ZoneDistance));
            Assert.That(stateMachine.State.FieldOfView, Is.EqualTo(settings.ZoneFieldOfView));

            stateMachine.Focus(Focus(
                "camera-focus:object.farm-tile-0-0",
                DioramaCameraFocusLevelCodes.Object,
                -22f, 0f, 14f));

            Assert.That(stateMachine.State.Distance, Is.EqualTo(settings.ObjectDistance));
            Assert.That(stateMachine.State.FieldOfView, Is.EqualTo(settings.ObjectFieldOfView));
        }

        [Test]
        public void CameraRig은_Perspective와AnchorFocus를적용한다()
        {
            var cameraObject = new GameObject("DioramaTopDownCameraRig");
            var worldAnchor = new GameObject("WorldFocusAnchor");
            var farmAnchor = new GameObject("FarmFocusAnchor");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
                worldAnchor.transform.position = Vector3.zero;
                farmAnchor.transform.position = new Vector3(-20f, 1f, 12f);
                rig.Configure(camera, new[]
                {
                    Binding("camera-focus:world.overview", DioramaCameraFocusLevelCodes.World, worldAnchor.transform),
                    Binding("camera-focus:zone.farm", DioramaCameraFocusLevelCodes.Zone, farmAnchor.transform),
                }, "camera-focus:world.overview", false);

                rig.Initialize();
                rig.Focus("camera-focus:zone.farm");
                rig.RotateRight();
                rig.ZoomIn();
                rig.ApplyNowForTests();

                Assert.That(camera.orthographic, Is.False);
                Assert.That(rig.CurrentFocusAnchorId, Is.EqualTo("camera-focus:zone.farm"));
                Assert.That(rig.CurrentFocusLevelCode, Is.EqualTo(DioramaCameraFocusLevelCodes.Zone));
                Assert.That(rig.YawQuarterTurns, Is.EqualTo(1));
                Assert.That(rig.Distance, Is.LessThan(34f));
                Assert.That(camera.fieldOfView, Is.EqualTo(30f).Within(.01f));
                Assert.That(cameraObject.transform.position, Is.Not.EqualTo(farmAnchor.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(worldAnchor);
                Object.DestroyImmediate(farmAnchor);
            }
        }

        [Test]
        public void OcclusionController는_명시적으로표시된전경만Cutaway한다()
        {
            var cameraObject = new GameObject("DioramaTopDownCameraRig");
            var focusAnchor = new GameObject("ObjectFocusAnchor");
            var foreground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
                focusAnchor.transform.position = Vector3.zero;
                foreground.transform.position = new Vector3(0f, 6f, -7f);
                foreground.transform.localScale = new Vector3(5f, 5f, 2f);
                var renderer = foreground.GetComponent<Renderer>();
                foreground.AddComponent<DioramaOcclusionView>();
                rig.Configure(camera, new[]
                {
                    Binding("camera-focus:object.test", DioramaCameraFocusLevelCodes.Object, focusAnchor.transform),
                }, "camera-focus:object.test", false);
                rig.Initialize();
                var controller = cameraObject.AddComponent<DioramaForegroundOcclusionController>();
                controller.Configure(rig);

                controller.ApplyNow();

                Assert.That(renderer.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(focusAnchor);
                Object.DestroyImmediate(foreground);
            }
        }

        private static DioramaCameraFocus Focus(
            string id,
            string level,
            float x,
            float y,
            float z)
            => new()
            {
                AnchorId = id,
                LevelCode = level,
                Point = new DioramaPoint(x, y, z),
            };

        private static DioramaCameraFocusBinding Binding(
            string id,
            string level,
            Transform anchor)
            => new()
            {
                AnchorId = id,
                LevelCode = level,
                Anchor = anchor,
            };
    }
}
