using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Application.WorldMap;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Infrastructure.WorldMap;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.Configuration;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Ssalddel.Unity.Tests.PlayMode
{
    public sealed class PublicWorldMapPresenterPlayModeTests
    {
        [UnityTest]
        public IEnumerator Marker는_PlayMode에서_증분갱신되고_선택된다()
        {
            var root = new GameObject("PlayModePresenterTest");
            var presenter = root.AddComponent<PublicWorldMapPresenter>();
            string selected = null;
            presenter.SetMarkerSelectedHandler(id => selected = id);

            presenter.Apply(Snapshot(Marker("observation:play-a"), Marker("observation:play-b")));
            Assert.That(presenter.MarkerCount, Is.EqualTo(2));
            Assert.That(presenter.TrySelect("observation:play-b"), Is.True);
            Assert.That(selected, Is.EqualTo("observation:play-b"));

            presenter.Apply(Snapshot(Marker("observation:play-b")));
            yield return null;
            Assert.That(presenter.MarkerCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Loopback_HTTP응답은_Repository_Controller_Presenter까지_연결된다()
        {
            const string json = "{\"DatasetCode\":\"public-world\",\"Revision\":\"revision:loopback\",\"GeneratedAtUtc\":\"2026-08-06T00:00:00Z\",\"Observations\":[{\"StableId\":\"observation:loopback\",\"DatasetCode\":\"public-world\",\"LayerCode\":\"news-publisher\",\"CountryCode\":\"KR\",\"CountryName\":\"대한민국\",\"Latitude\":37.5,\"Longitude\":127.0,\"Title\":\"Loopback\",\"Summary\":\"공개 관측\",\"SourceName\":\"공식 공개 출처\",\"EvidenceAsOfUtc\":\"2026-08-06T00:00:00Z\",\"EvidenceStatusCode\":\"Verified\",\"DetailHref\":\"/community/world-map/observations/observation:loopback\",\"LocationPrecisionCode\":\"City\",\"FreshnessCode\":\"Current\",\"BoundaryNotice\":\"공개 정보이며 개인 위치나 계약을 의미하지 않습니다.\"}]}";
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = ServeOnceAsync(listener, json);
            var root = new GameObject("LoopbackPresenterTest");
            var presenter = root.AddComponent<PublicWorldMapPresenter>();
            var client = new OperationalUnityWebRequestApiClient(new UnityClientRuntimeOptions
            {
                OperationalApiBaseUrl = $"http://127.0.0.1:{port}",
                SimulationRehearsalApiBaseUrl = "http://127.0.0.1:5204",
                ExecutionMode = UnityExecutionModeCodes.Simulation,
                AllowFixtureData = false
            });
            var controller = new PublicWorldMapSceneController(
                new LoadPublicWorldMapUseCase(new CommunityWorldMapRepository(client)),
                presenter.Apply,
                presenter.Clear);

            var initializeTask = controller.InitializeAsync(string.Empty, CancellationToken.None);
            while (!initializeTask.IsCompleted) yield return null;

            Assert.That(initializeTask.IsFaulted, Is.False);
            Assert.That(serverTask.IsFaulted, Is.False);
            Assert.That(
                controller.CurrentState.Status,
                Is.EqualTo(PublicWorldMapSceneStatus.Success),
                controller.CurrentState.ErrorMessage);
            Assert.That(controller.CurrentState.Revision, Is.EqualTo("revision:loopback"));
            Assert.That(presenter.MarkerCount, Is.EqualTo(1));

            listener.Stop();
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldBootstrapScene은_실제_공개API_marker를_표시한다()
        {
            SceneManager.LoadScene("WorldBootstrapScene", LoadSceneMode.Single);
            var deadline = Time.realtimeSinceStartup + 15f;
            WorldBootstrapSceneCompositionRoot root = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                root = UnityEngine.Object.FindAnyObjectByType<WorldBootstrapSceneCompositionRoot>();
                if (root?.Controller != null
                    && (root.Controller.CurrentState.Status == PublicWorldMapSceneStatus.Success
                        || root.Controller.CurrentState.Status == PublicWorldMapSceneStatus.InitialLoadError)) break;
                yield return null;
            }

            Assert.That(root, Is.Not.Null);
            Assert.That(root.Controller.CurrentState.Status, Is.EqualTo(PublicWorldMapSceneStatus.Success), root.Controller.CurrentState.ErrorMessage);
            var presenter = UnityEngine.Object.FindAnyObjectByType<PublicWorldMapPresenter>();
            Assert.That(presenter.MarkerCount, Is.EqualTo(root.Controller.CurrentSnapshot.Markers.Length));
            Assert.That(presenter.MarkerCount, Is.GreaterThan(0));
        }

        private static async Task ServeOnceAsync(TcpListener listener, string body)
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[4096];
            var received = new StringBuilder();
            do
            {
                var count = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (count == 0) break;
                received.Append(Encoding.ASCII.GetString(buffer, 0, count));
            } while (!received.ToString().Contains("\r\n\r\n"));

            var payload = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, 0, header.Length);
            await stream.WriteAsync(payload, 0, payload.Length);
            await stream.FlushAsync();
        }

        private static PublicWorldMapSnapshot Snapshot(params PublicWorldMarker[] markers) => new PublicWorldMapSnapshot
        {
            DatasetCode = "public-world",
            Revision = "revision:play",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Markers = markers
        };

        private static PublicWorldMarker Marker(string id) => new PublicWorldMarker
        {
            StableId = id,
            DatasetCode = "public-world",
            LayerCode = "news-publisher",
            CountryCode = "KR",
            CountryName = "대한민국",
            Latitude = 37.5,
            Longitude = 127,
            Title = id,
            Summary = "공개 관측",
            SourceName = "공식 공개 출처",
            EvidenceAsOfUtc = DateTimeOffset.UtcNow,
            DetailHref = "/community/world-map/observations/" + id,
            LocationPrecisionCode = "City",
            FreshnessCode = "Current",
            BoundaryNotice = "공개 정보이며 개인 위치나 계약을 의미하지 않습니다."
        };
    }

    public sealed class 전략카메라PlayModeTests
    {
        [UnityTest]
        public IEnumerator GameViewInputSystem의_WASD회전Wheel우클릭Drag가_카메라만변경한다()
        {
            SceneManager.LoadScene("SimulationWorldShell", LoadSceneMode.Single);
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<전략카메라Controller>();
            var rig = UnityEngine.Object.FindAnyObjectByType<DioramaTopDownCameraRig>();
            var shell = UnityEngine.Object.FindAnyObjectByType<SimulationWorldShellPresenter>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(shell, Is.Not.Null);

            var keyboard = InputSystem.AddDevice<Keyboard>("StrategyCameraTestKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>("StrategyCameraTestMouse");
            var initialFocus = rig!.CurrentFocusPosition;
            var initialDistance = rig.Distance;
            var initialYaw = rig.YawDegrees;
            var initialTick = shell!.WorldTick;
            var initialRevision = shell.WorldRevision;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(rig.CurrentFocusPosition.z, Is.GreaterThan(initialFocus.z));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(rig.YawDegrees, Is.GreaterThan(initialYaw));

                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                Assert.That(rig.Distance, Is.LessThan(initialDistance));

                var yawBeforeDrag = rig.YawDegrees;
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    buttons = 1 << 1,
                    delta = new Vector2(60f, -20f),
                });
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                Assert.That(rig.YawDegrees, Is.Not.EqualTo(yawBeforeDrag).Within(.001f));

                Assert.That(controller!.Mode, Is.EqualTo(전략카메라탐색Mode.FreeExplore));
                Assert.That(shell.WorldTick, Is.EqualTo(initialTick));
                Assert.That(shell.WorldRevision, Is.EqualTo(initialRevision));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
