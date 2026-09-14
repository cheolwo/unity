using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 역세권디오라마MobilityAdapterTests
    {
        [Test]
        public void 상태Factory는_가용과미가용상태를섞지않는다()
        {
            var ready = StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                StationDioramaModuleLayerIds.Mobility, true, "fixture.r1", "Ready");
            var blocked = StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                StationDioramaModuleLayerIds.Mobility, false, string.Empty, "Waiting");
            var missing = StationDioramaModuleLayerRuntimeStateFactory.NotProvided(
                StationDioramaModuleLayerIds.Mobility, "Missing");

            Assert.That(ready.StateCode,
                Is.EqualTo(StationDioramaModuleLayerStateCodes.PrivateReview));
            Assert.That(blocked.StateCode,
                Is.EqualTo(StationDioramaModuleLayerStateCodes.Blocked));
            Assert.That(missing.StateCode,
                Is.EqualTo(StationDioramaModuleLayerStateCodes.NotProvided));
            Assert.Throws<ArgumentException>(() =>
                StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                    StationDioramaModuleLayerIds.Mobility, true, "fixture.r1", "Ready",
                    StationDioramaModuleLayerStateCodes.Blocked));
            Assert.Throws<ArgumentException>(() =>
                StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                    StationDioramaModuleLayerIds.Mobility, false, string.Empty, "Waiting",
                    unavailableStateCode: StationDioramaModuleLayerStateCodes.Ready));
        }

        [Test]
        public void Playback이없으면_Mobility하나만미제공으로남고표시하지않는다()
        {
            var root = new GameObject("station-mobility-empty-test");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();
                adapter.ConfigureSources(host,
                    Array.Empty<IStationDioramaModulePresentationSource>());

                var state = adapter.DescribeModuleLayers().Single();
                Assert.That(state.LayerStableId,
                    Is.EqualTo(StationDioramaModuleLayerIds.Mobility));
                Assert.That(state.StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.NotProvided));
                Assert.That(host.ActivePlan.Layers.Count(value =>
                    value.Definition.LayerStableId == StationDioramaModuleLayerIds.Mobility),
                    Is.EqualTo(1));
                Assert.That(adapter.ModuleVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 같은Playback재설정은_Mobility하나와같은Hash를유지한다()
        {
            var root = new GameObject("station-mobility-idempotency-test");
            var target = new GameObject("low-density-source");
            try
            {
                target.transform.SetParent(root.transform, false);
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();
                var low = new Source(사가정MobilityModuleAdapter.LowDensityTrafficSourceStableId,
                    "traffic.r2", target.AddComponent<음식배달JourneyPlaybackLayer>(), true);

                adapter.ConfigureSources(host, new[] { low });
                var firstHash = host.ActivePlan.AssemblyHashSha256;
                var firstState = adapter.DescribeModuleLayers().Single();
                adapter.ConfigureSources(host, new[] { low });

                Assert.That(adapter.SourceCount, Is.EqualTo(1));
                Assert.That(adapter.ReadySourceCount, Is.EqualTo(1));
                Assert.That(firstState.StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.PrivateReview));
                Assert.That(firstState.DiagnosticCode,
                    Is.EqualTo("Ready"));
                Assert.That(adapter.DescribeModuleLayers().Count, Is.EqualTo(1));
                Assert.That(host.ActivePlan.AssemblyHashSha256, Is.EqualTo(firstHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 두Playback동시지정은_중복배달표현을막기위해거부한다()
        {
            var root = new GameObject("station-mobility-ambiguous-source-test");
            var firstTarget = new GameObject("first-source");
            var secondTarget = new GameObject("second-source");
            try
            {
                firstTarget.transform.SetParent(root.transform, false);
                secondTarget.transform.SetParent(root.transform, false);
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();

                var exception = Assert.Throws<ArgumentException>(() =>
                    adapter.ConfigureSources(host, new[]
                    {
                        new Source("source:first", "first.r1",
                            firstTarget.AddComponent<음식배달JourneyPlaybackLayer>(), true),
                        new Source("source:second", "second.r1",
                            secondTarget.AddComponent<음식배달JourneyPlaybackLayer>(), true)
                    }));

                Assert.That(exception.ParamName, Is.EqualTo("values"));
                Assert.That(adapter.SourceCount, Is.Zero);
                Assert.That(adapter.ModuleHost, Is.Null);
                Assert.That(host.RegisteredAdapterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 준비된Source의판본이비면_예외대신Blocked진단으로격리한다()
        {
            var root = new GameObject("station-mobility-missing-revision-test");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var behaviour = root.AddComponent<음식배달JourneyPlaybackLayer>();
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();

                adapter.ConfigureSources(host, new[]
                {
                    new Source("source:missing-revision", string.Empty, behaviour, true)
                });

                var state = adapter.DescribeModuleLayers().Single();
                Assert.That(state.StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.Blocked));
                Assert.That(state.DiagnosticCode,
                    Is.EqualTo("StationDioramaModuleSourceRevisionMissing"));
                Assert.That(adapter.ModuleVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Host숨김과복원은_Playback원래Enabled와상태를보존한다(
            bool originallyEnabled)
        {
            var root = new GameObject("station-mobility-lifetime-test");
            var target = new GameObject("mobility-source");
            try
            {
                target.transform.SetParent(root.transform, false);
                var behaviour = target.AddComponent<음식배달JourneyPlaybackLayer>();
                behaviour.enabled = originallyEnabled;
                var source = new Source("source:a", "source-a.r3", behaviour, true);
                var host = root.AddComponent<ActiveStationDioramaHost>();
                var foundation = new FoundationAdapter();
                host.Register(foundation);
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();
                adapter.ConfigureSources(host, new[] { source });
                var before = adapter.DescribeModuleLayers().Single().SourceRevision;

                host.Unregister(foundation);
                Assert.That(adapter.ModuleVisible, Is.False);
                Assert.That(behaviour.enabled, Is.False);

                host.Register(foundation);
                Assert.That(adapter.ModuleVisible, Is.True);
                Assert.That(behaviour.enabled, Is.EqualTo(originallyEnabled));
                Assert.That(adapter.DescribeModuleLayers().Single().SourceRevision,
                    Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 중복Mobility등록실패는_Host에부분등록을남기지않는다()
        {
            var root = new GameObject("station-mobility-duplicate-test");
            var firstObject = new GameObject("first-adapter");
            var secondObject = new GameObject("second-adapter");
            try
            {
                firstObject.transform.SetParent(root.transform, false);
                secondObject.transform.SetParent(root.transform, false);
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var first = firstObject.AddComponent<사가정MobilityModuleAdapter>();
                first.ConfigureSources(host, new[]
                {
                    new Source("source:first", "first.r1",
                        firstObject.AddComponent<음식배달JourneyPlaybackLayer>(), true)
                });
                var beforeHash = host.ActivePlan.AssemblyHashSha256;
                var second = secondObject.AddComponent<사가정MobilityModuleAdapter>();

                Assert.Throws<ArgumentException>(() => second.ConfigureSources(host, new[]
                {
                    new Source("source:second", "second.r1",
                        secondObject.AddComponent<음식배달JourneyPlaybackLayer>(), true)
                }));
                Assert.That(host.RegisteredAdapterCount, Is.EqualTo(2));
                Assert.That(host.ActivePlan.AssemblyHashSha256, Is.EqualTo(beforeHash));
                Assert.That(second.ModuleHost, Is.Null);
                Assert.That(second.ModuleVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 실제Playback참조는_적용전Blocked이고Detach때Enabled를복원한다()
        {
            var root = new GameObject("station-mobility-concrete-source-test");
            var playbackObject = new GameObject("existing-playback-components");
            try
            {
                playbackObject.transform.SetParent(root.transform, false);
                var low = playbackObject.AddComponent<사가정저밀도교통PlaybackLayer>();
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();
                adapter.Configure(host, low);

                var state = adapter.DescribeModuleLayers().Single();
                Assert.That(state.StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.Blocked));
                Assert.That(adapter.ModuleVisible, Is.False);
                Assert.That(low.enabled, Is.False);

                adapter.DetachFromHost();
                Assert.That(low.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 실제교통상태사본적용뒤_명시적갱신은숨김누수없이Ready로전환한다()
        {
            var root = new GameObject("station-mobility-refresh-test");
            var playbackObject = new GameObject("traffic-playback-source");
            try
            {
                playbackObject.transform.SetParent(root.transform, false);
                var trafficRoot = new GameObject("traffic-root").transform;
                trafficRoot.SetParent(playbackObject.transform, false);
                var playback = playbackObject.AddComponent<사가정저밀도교통PlaybackLayer>();
                playback.Configure(null, trafficRoot, poolCapacity: 4);
                var host = root.AddComponent<ActiveStationDioramaHost>();
                host.Register(new FoundationAdapter());
                var adapter = root.AddComponent<사가정MobilityModuleAdapter>();
                adapter.Configure(host, playback);

                Assert.That(adapter.ModuleVisible, Is.False);
                Assert.That(playback.enabled, Is.False);
                Assert.That(adapter.DescribeModuleLayers().Single().StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.Blocked));

                var interpreter = new 사가정저밀도교통Interpreter();
                var firstSnapshot = 사가정저밀도교통Tests.ModuleAdapterSnapshot();
                Assert.That(interpreter.TryInterpret(firstSnapshot,
                    사가정저밀도교통Tests.ModuleAdapterContext(firstSnapshot),
                    out var firstModel, out var failure), Is.True, failure);
                playback.Apply(firstModel);
                Assert.That(trafficRoot.Find("정적 교통 관찰과 합성 차로")
                    .gameObject.activeSelf, Is.False);
                Assert.That(trafficRoot.Find("동적 저밀도 합성 교통")
                    .gameObject.activeSelf, Is.False);

                Assert.That(adapter.RefreshModuleState(), Is.True);

                Assert.That(adapter.ModuleVisible, Is.True);
                Assert.That(playback.enabled, Is.True);
                Assert.That(adapter.DescribeModuleLayers().Single().StateCode,
                    Is.EqualTo(StationDioramaModuleLayerStateCodes.PrivateReview));
                Assert.That(host.StatusCode, Is.EqualTo("ReadyWithOptionalModuleGaps"));
                var firstHash = host.ActivePlan.AssemblyHashSha256;

                var secondSnapshot = 사가정저밀도교통Tests.ModuleAdapterSnapshot(1L);
                Assert.That(interpreter.TryInterpret(secondSnapshot,
                    사가정저밀도교통Tests.ModuleAdapterContext(secondSnapshot),
                    out var secondModel, out failure), Is.True, failure);
                playback.Apply(secondModel);
                Assert.That(adapter.RefreshModuleState(), Is.True);
                Assert.That(host.ActivePlan.AssemblyHashSha256, Is.EqualTo(firstHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FoundationAdapter : IStationDioramaModuleAdapter
        {
            public string TransitStationStableId =>
                StationDioramaProfileCatalog.SagajeongStationStableId;
            public bool ModuleVisible { get; private set; }

            public IReadOnlyList<StationDioramaModuleLayerRuntimeState> DescribeModuleLayers()
                => new[]
                {
                    StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                        StationDioramaModuleLayerIds.DataEvidence, true, "data.r1", "Ready"),
                    StationDioramaModuleLayerRuntimeStateFactory.FromAvailability(
                        StationDioramaModuleLayerIds.BaseSpatialPresentation, true,
                        "presentation.r1", "Ready")
                };

            public void SetModuleVisible(bool value) => ModuleVisible = value;
        }

        private sealed class Source : IStationDioramaModulePresentationSource
        {
            public Source(string stableId, string revision, Behaviour behaviour, bool ready)
            {
                ModuleSourceStableId = stableId;
                ModuleSourceRevision = revision;
                PresentationBehaviour = behaviour;
                HasAcceptedPresentation = ready;
            }

            public string ModuleLayerStableId => StationDioramaModuleLayerIds.Mobility;
            public string ModuleSourceStableId { get; }
            public string ModuleSourceRevision { get; }
            public string ModuleDiagnosticCode => HasAcceptedPresentation ? "Ready" : "Waiting";
            public bool HasAcceptedPresentation { get; }
            public Behaviour PresentationBehaviour { get; }
        }

    }
}
