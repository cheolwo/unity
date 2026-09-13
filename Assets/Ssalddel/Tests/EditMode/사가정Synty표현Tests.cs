using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정Synty표현Tests
    {
        private static TextAsset MapAsset => Resources.Load<TextAsset>("SagajeongReference");
        private static 공간참고자료 Map => JsonUtility.FromJson<공간참고자료>(MapAsset.text);
        private static 법정동경관VisualCatalog Catalog =>
            Resources.Load<법정동경관VisualCatalog>("SagajeongSyntyVisualCatalog");

        private static 건물높이보완조회 Heights(공간참고자료 map)
        {
            var asset = Resources.Load<TextAsset>("SagajeongBuildingHeightOverlay");
            return 건물높이보완조회.Validate(
                JsonUtility.FromJson<건물높이보완자료>(asset.text), map, MapAsset.bytes);
        }

        [Test]
        public void 표현대장은_네CityPrefab을_표현전용VisualKey로연결한다()
        {
            Assert.That(Catalog, Is.Not.Null);
            Catalog.Validate();
            Assert.That(Catalog.CatalogRevision, Is.EqualTo(사가정Synty표현Planner.Revision));
            Assert.That(Catalog.Entries.Count, Is.EqualTo(4));
            Assert.That(Catalog.Entries.All(value => value.PresentationOnly), Is.True);
            Assert.That(Catalog.Entries.All(value =>
                AssetDatabase.GetAssetPath(value.Prefab).StartsWith(
                    "Assets/Synty/PolygonCity/Prefabs/", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void 역세권표본은_높이근거일곱과_가까운저층여섯을_결정적으로고른다()
        {
            var map = Map;
            var original = JsonUtility.ToJson(map);
            var first = 사가정Synty표현Planner.Build(map, Heights(map), Catalog, 150f, 13);
            var second = 사가정Synty표현Planner.Build(map, Heights(map), Catalog, 150f, 13);

            Assert.That(first.CandidateCount, Is.EqualTo(13));
            Assert.That(first.AcceptedCount, Is.EqualTo(13));
            Assert.That(first.FallbackCount, Is.Zero);
            Assert.That(first.Placements.Select(value => value.SourceBuildingId),
                Is.EqualTo(second.Placements.Select(value => value.SourceBuildingId)));
            Assert.That(first.Rejected.Select(value => value.SourceBuildingId),
                Is.EqualTo(second.Rejected.Select(value => value.SourceBuildingId)));
            Assert.That(first.Placements.All(value => value.UniformScale
                is >= 사가정Synty표현Planner.MinimumUniformScale
                and <= 사가정Synty표현Planner.MaximumUniformScale), Is.True);
            Assert.That(first.Placements.All(value => Math.Abs(value.ActualHeight - value.TargetHeight)
                / Math.Max(1f, value.TargetHeight)
                <= 사가정Synty표현Planner.MaximumHeightErrorRatio), Is.True);
            Assert.That(JsonUtility.ToJson(map), Is.EqualTo(original));
        }

        [Test]
        public void 전체혼합계획은_최대백이십후보와_fallback을분리한다()
        {
            var map = Map;
            var plan = 사가정Synty표현Planner.Build(map, Heights(map), Catalog, 750f, 120);
            Assert.That(plan.CandidateCount, Is.EqualTo(120));
            Assert.That(plan.AcceptedCount, Is.EqualTo(90));
            Assert.That(plan.FallbackCount, Is.EqualTo(30));
            Assert.That(plan.ReplacedBuildingIds.Count, Is.EqualTo(plan.AcceptedCount));
            Assert.That(plan.Placements.All(value => plan.Replaces(value.SourceBuildingId)), Is.True);
            Assert.That(plan.Rejected.All(value => !plan.Replaces(value.SourceBuildingId)), Is.True);
        }

        [Test]
        public void 향상Mesh는_도로위계와보도와_fallback건물과Synty기단을분리한다()
        {
            var map = Map;
            var plan = 사가정Synty표현Planner.Build(map, Heights(map), Catalog, 150f, 13);
            var names = new List<string>();
            사가정지리MeshBuilder.Build(map, (name, vertices, triangles, color) =>
            {
                names.Add(name);
                Assert.That(vertices, Is.Not.Empty);
                Assert.That(triangles.All(index => index >= 0 && index < vertices.Count), Is.True);
            }, Heights(map), plan);

            Assert.That(names, Does.Contain("사가정 · 큰길 · 위계 표현"));
            Assert.That(names, Does.Contain("사가정 · 생활도로 · 위계 표현"));
            Assert.That(names, Does.Contain("사가정 · 보행로 · 위계 표현"));
            Assert.That(names, Does.Contain("사가정 · 연석과 보도 · 표현 전용"));
            Assert.That(names, Does.Contain("사가정 · 건물 외곽 · 자료 높이와 fallback"));
            Assert.That(names, Does.Contain("사가정 · Synty 접지 기단 · 원문 외곽"));
        }

        [Test]
        public void 실제View는_하나의VisualRoot아래에만Synty를만들고Collider를제거한다()
        {
            var root = new GameObject("sagajeong-synty-view-test");
            try
            {
                var view = root.AddComponent<사가정운영디오라마View>();
                typeof(사가정운영디오라마View).GetField("useSyntyPresentation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(view, true);
                view.Initialize();
                Assert.That(view.MapStatus, Is.EqualTo("Ready"));
                Assert.That(view.VisualTargetRevision, Is.EqualTo(사가정Synty표현Planner.Revision));
                Assert.That(view.SyntyPlacementCount, Is.EqualTo(90));
                Assert.That(view.SyntyFallbackCount, Is.EqualTo(30));
                Assert.That(view.SyntyVisualRoot, Is.Not.Null);
                Assert.That(view.SpatialOverlayApplied, Is.False,
                    "Synty 분기는 r3 기준 지도만 그리므로 비공개 공간 overlay를 적용했다고 보고하면 안 됩니다.");
                Assert.That(view.SpatialPresentationRevision,
                    Is.EqualTo(사가정공간표현OverlayComposer.BaseMapRevision));
                Assert.That(view.SpatialPresentationContentHash,
                    Is.EqualTo(사가정공간표현OverlayComposer.BaseMapHash));
                Assert.That(view.SpatialPresentationStatus,
                    Does.Contain("SpatialOverlayNotRenderedInSyntyMode"));
                Assert.That(root.GetComponentsInChildren<Transform>(true)
                    .Count(value => value.name == "Synty VisualRoot · 표현 전용"), Is.EqualTo(1));
                Assert.That(view.SyntyVisualRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Renderer>(true)
                    .All(value => value.gameObject.layer == 사가정운영디오라마View.ObservationLayer),
                    Is.True, "관찰 카메라가 지도 Mesh와 Synty 표현을 함께 읽어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
