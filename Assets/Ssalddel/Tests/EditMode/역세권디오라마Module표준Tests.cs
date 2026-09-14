using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 역세권디오라마Module표준Tests
    {
        [Test]
        public void 사가정참조Profile은_공통8모듈의순서와읽기전용경계를고정한다()
        {
            var profile = StationDioramaModuleProfileCatalog.SagajeongReference;

            Assert.That(profile.TransitStationStableId,
                Is.EqualTo(StationDioramaProfileCatalog.SagajeongStationStableId));
            Assert.That(profile.Layers.Count, Is.EqualTo(8));
            Assert.That(profile.Layers.Zip(profile.Layers.Skip(1),
                    (left, right) => left.AssemblyOrder < right.AssemblyOrder),
                Is.All.True);
            Assert.That(profile.Layers.Where(value => value.RequiredForObservation)
                    .Select(value => value.LayerStableId),
                Is.EqualTo(new[]
                {
                    StationDioramaModuleLayerIds.DataEvidence,
                    StationDioramaModuleLayerIds.BaseSpatialPresentation
                }));
            Assert.That(profile.ObservationPresentationOnly, Is.True);
            Assert.That(profile.ChangesOperationalState, Is.False);
            Assert.That(profile.TraversalReady, Is.False);
            Assert.That(profile.GameplayReady, Is.False);
            Assert.That(profile.DistributionApproved, Is.False);
        }

        [Test]
        public void 공통Factory는_새역의구현없이_식별자와판본만으로같은구조를만든다()
        {
            var candidate = StationDioramaModuleStandard.CreateProfile(
                "station:kr:kric:fixture:0001", "후속 역 검증 후보",
                "station-diorama-module:fixture.r1");
            var reference = StationDioramaModuleProfileCatalog.SagajeongReference;

            Assert.That(candidate.Layers.Select(value => value.LayerStableId),
                Is.EqualTo(reference.Layers.Select(value => value.LayerStableId)));
            Assert.That(candidate.Layers.Select(value => value.AssemblyOrder),
                Is.EqualTo(reference.Layers.Select(value => value.AssemblyOrder)));
            Assert.That(candidate.Layers.Select(value => value.RequiredForObservation),
                Is.EqualTo(reference.Layers.Select(value => value.RequiredForObservation)));
            Assert.That(StationDioramaModuleProfileCatalog.TryResolve(
                candidate.TransitStationStableId, out _), Is.False);
        }

        [Test]
        public void Profile은_중복과없는의존성_역순의존성을거부한다()
        {
            var data = Layer(StationDioramaModuleLayerIds.DataEvidence, 10, true);
            var presentation = Layer(StationDioramaModuleLayerIds.BaseSpatialPresentation,
                20, true, StationDioramaModuleLayerIds.DataEvidence);

            Assert.Throws<ArgumentException>(() => Profile(data, data, presentation));
            Assert.Throws<ArgumentException>(() => Profile(data, presentation,
                Layer(StationDioramaModuleLayerIds.SpatialMeaning, 30, false,
                    "station-diorama-layer:missing.v1")));
            Assert.Throws<ArgumentException>(() => Profile(
                Layer(StationDioramaModuleLayerIds.DataEvidence, 20, true),
                Layer(StationDioramaModuleLayerIds.BaseSpatialPresentation, 10, true,
                    StationDioramaModuleLayerIds.DataEvidence)));
        }

        [Test]
        public void Planner는_입력순서와무관하게_같은조립Hash와선택모듈결손을만든다()
        {
            var states = ReadyFoundationAndSelection();
            var first = StationDioramaModulePlanner.Build(
                StationDioramaModuleProfileCatalog.SagajeongReference, states);
            var second = StationDioramaModulePlanner.Build(
                StationDioramaModuleProfileCatalog.SagajeongReference, states.Reverse());

            Assert.That(first.CanPresentObservation, Is.True);
            Assert.That(first.OptionalLayersComplete, Is.False);
            Assert.That(first.MissingRequiredLayerStableIds, Is.Empty);
            Assert.That(first.MissingOptionalLayerStableIds,
                Does.Contain(StationDioramaModuleLayerIds.Mobility)
                    .And.Contain(StationDioramaModuleLayerIds.LifeSimulation)
                    .And.Contain(StationDioramaModuleLayerIds.BusinessOverlay)
                    .And.Contain(StationDioramaModuleLayerIds.EnvironmentPresentation));
            Assert.That(first.AssemblyHashSha256, Has.Length.EqualTo(64));
            Assert.That(second.AssemblyHashSha256, Is.EqualTo(first.AssemblyHashSha256));
        }

        [Test]
        public void Planner는_선행모듈이막히면_준비된후속모듈도차단한다()
        {
            var result = StationDioramaModulePlanner.Build(
                StationDioramaModuleProfileCatalog.SagajeongReference,
                new[]
                {
                    State(StationDioramaModuleLayerIds.DataEvidence,
                        StationDioramaModuleLayerStateCodes.Blocked),
                    State(StationDioramaModuleLayerIds.BaseSpatialPresentation,
                        StationDioramaModuleLayerStateCodes.PrivateReview),
                    State(StationDioramaModuleLayerIds.SpatialMeaning,
                        StationDioramaModuleLayerStateCodes.PrivateReview)
                });

            Assert.That(result.CanPresentObservation, Is.False);
            Assert.That(result.Layers.Single(value => value.Definition.LayerStableId ==
                    StationDioramaModuleLayerIds.BaseSpatialPresentation)
                .RuntimeState.StateCode,
                Is.EqualTo(StationDioramaModuleLayerStateCodes.BlockedByDependency));
            Assert.That(result.MissingRequiredLayerStableIds,
                Does.Contain(StationDioramaModuleLayerIds.DataEvidence)
                    .And.Contain(StationDioramaModuleLayerIds.BaseSpatialPresentation));
        }

        [Test]
        public void Host는_필수모듈이있는사가정Adapter만활성화하고_미등록역을대체하지않는다()
        {
            var root = new GameObject("station-diorama-module-host-test");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                var adapter = new FakeAdapter(ReadyFoundationAndSelection());
                host.Register(adapter);

                Assert.That(host.ActiveTransitStationStableId,
                    Is.EqualTo(StationDioramaProfileCatalog.SagajeongStationStableId));
                Assert.That(host.ActivePlan.CanPresentObservation, Is.True);
                Assert.That(host.StatusCode, Is.EqualTo("ReadyWithOptionalModuleGaps"));
                Assert.That(adapter.Visible, Is.True);

                Assert.That(host.TryActivate(StationDioramaProfileCatalog.MyeonmokStationStableId),
                    Is.False);
                Assert.That(host.StatusCode,
                    Is.EqualTo("StationDioramaModuleProfileNotRegistered"));
                Assert.That(host.ActiveTransitStationStableId,
                    Is.EqualTo(StationDioramaProfileCatalog.SagajeongStationStableId));
                Assert.That(adapter.Visible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Host는_필수표현이빠진Adapter를표시하지않는다()
        {
            var root = new GameObject("station-diorama-required-module-test");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                var adapter = new FakeAdapter(new[]
                {
                    State(StationDioramaModuleLayerIds.DataEvidence,
                        StationDioramaModuleLayerStateCodes.PrivateReview)
                });
                host.Register(adapter);

                Assert.That(host.ActiveTransitStationStableId, Is.Empty);
                Assert.That(host.ActivePlan.CanPresentObservation, Is.False);
                Assert.That(host.ActivePlan.MissingRequiredLayerStableIds,
                    Does.Contain(StationDioramaModuleLayerIds.BaseSpatialPresentation));
                Assert.That(host.StatusCode, Is.EqualTo("WaitingForRequiredModule"));
                Assert.That(adapter.Visible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Host는_중복모듈등록을원자적으로거부하고_거부된Adapter를숨긴다()
        {
            var root = new GameObject("station-diorama-atomic-register-test");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                var foundation = new FakeAdapter(ReadyFoundationAndSelection().Concat(new[]
                {
                    State(StationDioramaModuleLayerIds.Mobility,
                        StationDioramaModuleLayerStateCodes.PrivateReview)
                }).ToArray());
                host.Register(foundation);
                var duplicate = new FakeAdapter(new[]
                {
                    State(StationDioramaModuleLayerIds.Mobility,
                        StationDioramaModuleLayerStateCodes.PrivateReview)
                });
                duplicate.SetModuleVisible(true);
                var beforeHash = host.ActivePlan.AssemblyHashSha256;

                Assert.Throws<ArgumentException>(() => host.Register(duplicate));

                Assert.That(host.RegisteredAdapterCount, Is.EqualTo(1));
                Assert.That(host.ActivePlan.AssemblyHashSha256, Is.EqualTo(beforeHash));
                Assert.That(foundation.Visible, Is.True);
                Assert.That(duplicate.Visible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Host는_해제한Adapter를즉시숨기고_남은Contribution으로갱신한다()
        {
            var root = new GameObject("station-diorama-unregister-lifetime-test");
            try
            {
                var host = root.AddComponent<ActiveStationDioramaHost>();
                var adapter = new FakeAdapter(ReadyFoundationAndSelection());
                host.Register(adapter);

                host.Unregister(adapter);

                Assert.That(adapter.Visible, Is.False);
                Assert.That(host.RegisteredAdapterCount, Is.Zero);
                Assert.That(host.ActiveTransitStationStableId, Is.Empty);
                Assert.That(host.ActivePlan.CanPresentObservation, Is.False);
                Assert.That(host.StatusCode, Is.EqualTo("WaitingForRequiredModule"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 기존사가정View는_Host호환Adapter로등록되고_Mesh기준을보존한다()
        {
            var root = new GameObject("sagajeong-station-module-adapter-test");
            try
            {
                var view = root.AddComponent<사가정운영디오라마View>();
                view.Initialize();

                Assert.That(view.ModuleHost, Is.Not.Null);
                Assert.That(view.ModuleHost.RegisteredAdapterCount, Is.EqualTo(1));
                Assert.That(view.ModuleHost.ActivePlan.CanPresentObservation, Is.True);
                Assert.That(view.ModuleHost.ActiveTransitStationStableId,
                    Is.EqualTo(StationDioramaProfileCatalog.SagajeongStationStableId));
                Assert.That(view.ModuleVisible, Is.True);
                Assert.That(view.MiniatureBuildReport, Is.Not.Null);
                Assert.That(view.MiniatureBuildReport.ChunkCount, Is.EqualTo(16));
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 활성ModuleCatalog은_이번절편에서사가정만등록한다()
        {
            Assert.That(StationDioramaModuleProfileCatalog.TryResolve(
                StationDioramaProfileCatalog.SagajeongStationStableId, out _), Is.True);
            Assert.That(StationDioramaModuleProfileCatalog.TryResolve(
                StationDioramaProfileCatalog.MyeonmokStationStableId, out _), Is.False);
            Assert.That(StationDioramaModuleProfileCatalog.TryResolve(
                StationDioramaProfileCatalog.YongmasanStationStableId, out _), Is.False);
        }

        private static StationDioramaModuleProfile Profile(
            params StationDioramaModuleLayerDefinition[] layers)
            => new(StationDioramaProfileCatalog.SagajeongStationStableId,
                "사가정 검증", "station-diorama-module:test.r1", layers);

        private static StationDioramaModuleLayerDefinition Layer(string id, int order,
            bool required, params string[] dependencies)
            => new(id, id, order, required, dependencies);

        private static StationDioramaModuleLayerRuntimeState[] ReadyFoundationAndSelection()
            => new[]
            {
                State(StationDioramaModuleLayerIds.DataEvidence,
                    StationDioramaModuleLayerStateCodes.PrivateReview),
                State(StationDioramaModuleLayerIds.BaseSpatialPresentation,
                    StationDioramaModuleLayerStateCodes.PrivateReview),
                State(StationDioramaModuleLayerIds.SpatialMeaning,
                    StationDioramaModuleLayerStateCodes.PrivateReview),
                State(StationDioramaModuleLayerIds.Interaction,
                    StationDioramaModuleLayerStateCodes.PrivateReview)
            };

        private static StationDioramaModuleLayerRuntimeState State(string id, string state)
            => new(id, state, "fixture.r1", "Fixture");

        private sealed class FakeAdapter : IStationDioramaModuleAdapter
        {
            private readonly IReadOnlyList<StationDioramaModuleLayerRuntimeState> states;

            public FakeAdapter(IReadOnlyList<StationDioramaModuleLayerRuntimeState> states)
                => this.states = states;

            public string TransitStationStableId =>
                StationDioramaProfileCatalog.SagajeongStationStableId;
            public bool ModuleVisible => Visible;
            public bool Visible { get; private set; }

            public IReadOnlyList<StationDioramaModuleLayerRuntimeState> DescribeModuleLayers()
                => states;

            public void SetModuleVisible(bool value) => Visible = value;
        }
    }
}
