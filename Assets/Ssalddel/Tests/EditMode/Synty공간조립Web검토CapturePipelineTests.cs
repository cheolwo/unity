using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class Synty공간조립Web검토CapturePipelineTests
    {
        [Test]
        public void 회복발전소A는_다섯Synty팩의_실재Prefab을_사용한다()
        {
            var paths = Synty공간조립Web검토CapturePipeline.RequiredPrefabPaths;

            Assert.That(paths, Has.Length.GreaterThanOrEqualTo(5));
            Assert.That(paths.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null),
                Is.True);
            foreach (var packFolder in new[]
                     {
                         "PolygonNature", "PolygonFarm", "PolygonTown",
                         "PolygonCity", "PolygonConstruction",
                     })
            {
                Assert.That(paths.Any(path => path.Contains("/" + packFolder + "/",
                    StringComparison.Ordinal)), Is.True, packFolder);
            }
        }

        [Test]
        public void 조립입력과_RenderingProfile은_각각_결정적인Hash를_가진다()
        {
            var sourceHash = Synty공간조립Web검토CapturePipeline.SourceCompositionHash;
            var repeatedSourceHash = Synty공간조립Web검토CapturePipeline.ComputeSourceCompositionHash();
            var renderingHash = Synty공간조립Web검토CapturePipeline.RenderingProfileHash;

            Assert.That(sourceHash, Has.Length.EqualTo(64));
            Assert.That(repeatedSourceHash, Is.EqualTo(sourceHash));
            Assert.That(renderingHash, Has.Length.EqualTo(64));
            Assert.That(renderingHash, Is.Not.EqualTo(sourceHash));
            Assert.That(Synty공간조립Web검토CapturePipeline.ReviewOnlyLayer, Is.EqualTo(31));
        }

        [Test]
        public void 부모Bundle과_expectedRevision은_최초촬영과_재촬영에서_함께검증된다()
        {
            var parentHash = new string('a', 64);

            Assert.Throws<ArgumentException>(() =>
                Synty공간조립Web검토CapturePipeline.CaptureOneCard(parentHash, 0));
            Assert.Throws<ArgumentException>(() =>
                Synty공간조립Web검토CapturePipeline.CaptureOneCard(string.Empty, 2));
        }

        [Test]
        [Category("SyntyHReviewPipeline")]
        public void H계층별_표준촬영시점수는_고정된다()
        {
            Assert.That(Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount("H1"), Is.EqualTo(4));
            Assert.That(Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount("H2"), Is.EqualTo(5));
            Assert.That(Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount("H3"), Is.EqualTo(6));
            Assert.That(Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount("H4"), Is.EqualTo(4));
            Assert.Throws<ArgumentException>(() =>
                Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount("H5"));
        }

        [Test]
        [Category("SyntyHReviewPipeline")]
        public void H조합물입력Hash는_Root의장면배치와무관하고_내부조합변경을감지한다()
        {
            var root = new GameObject("H2 검토 Root");
            var child = new GameObject("생산구획");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(2f, 0f, 3f);
            var job = CreateH2Job();
            try
            {
                var first = Synty공간조립Web검토CapturePipeline.ComputeSelectedCompositionHash(root.transform, job);
                root.transform.SetPositionAndRotation(new Vector3(120f, 7f, -40f), Quaternion.Euler(0f, 135f, 0f));
                root.transform.localScale = Vector3.one * 2f;
                var movedRoot = Synty공간조립Web검토CapturePipeline.ComputeSelectedCompositionHash(root.transform, job);
                child.transform.localPosition += Vector3.right;
                var changedComposition = Synty공간조립Web검토CapturePipeline.ComputeSelectedCompositionHash(root.transform, job);

                Assert.That(first, Is.EqualTo(movedRoot));
                Assert.That(changedComposition, Is.Not.EqualTo(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("SyntyHReviewPipeline")]
        public void H검토계획Hash는_같은계보에서결정적이고_대상변경을감지한다()
        {
            var job = CreateH2Job();
            var first = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
            var repeated = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
            job.H2StableId = "h2:block:farm-logistics-b";
            job.ReviewTargetStableId = job.H2StableId;
            var changed = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);

            Assert.That(first, Has.Length.EqualTo(64));
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(changed, Is.Not.EqualTo(first));
        }

        [Test]
        [Category("SyntyHReviewCapture")]
        public void 선택한H1Root는_저장Scene을바꾸지않고_4시점으로촬영된다()
        {
            const string TemporaryScenePath =
                "Assets/Ssalddel/Tests/EditMode/__SyntyH공간조립검토CaptureTests.unity";
            var initialScene = EditorSceneManager.GetActiveScene();
            var createdTemporaryScene = string.IsNullOrWhiteSpace(initialScene.path);
            if (createdTemporaryScene)
                Assert.That(EditorSceneManager.SaveScene(initialScene, TemporaryScenePath), Is.True);
            var activeScenePath = EditorSceneManager.GetActiveScene().path;
            var root = new GameObject("H1 자연 쉼터");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Synty공간조립Web검토CapturePipeline.RequiredPrefabPaths[0]);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(root.transform, false);
            var job = new SyntyH공간조립검토Job
            {
                BatchStableId = "review-batch:test.h1",
                BatchTitle = "H1 촬영 시험",
                ReviewItemStableId = "review-item:test.h1",
                CompositionStableId = "composition:test.h1",
                DisplayName = "자연 쉼터",
                ReviewTargetLevelCode = "H1",
                ReviewTargetStableId = "h1:expression:nature-shelter-a",
                H1StableId = "h1:expression:nature-shelter-a",
                VariantCode = "A",
                StateProfileCode = "Default",
            };
            job.PlanHash = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
            try
            {
                var bundle = Synty공간조립Web검토CapturePipeline.CaptureHierarchySelection(root, job);

                Assert.That(EditorSceneManager.GetActiveScene().path, Is.EqualTo(activeScenePath));
                Assert.That(bundle.SchemaVersion, Is.EqualTo("synty-composition-review-batch.v3"));
                Assert.That(bundle.Captures, Has.Count.EqualTo(4));
                Assert.That(bundle.PackUsages.Sum(value => value.UsagePercent), Is.EqualTo(100));
                Assert.That(bundle.Captures.All(value => File.Exists(value.FilePath)), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (createdTemporaryScene)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(TemporaryScenePath);
                }
            }
        }

        [Test]
        public void 전용CaptureStage는_저장Scene을바꾸지않고_4시점Png를만든다()
        {
            const string TemporaryScenePath =
                "Assets/Ssalddel/Tests/EditMode/__Synty공간조립Web검토CapturePipelineTests.unity";
            var initialScene = EditorSceneManager.GetActiveScene();
            var createdTemporaryScene = string.IsNullOrWhiteSpace(initialScene.path);
            if (createdTemporaryScene)
            {
                if (UnityEngine.Application.isBatchMode)
                {
                    initialScene = EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
                else if (initialScene.rootCount != 0 || initialScene.isDirty)
                {
                    Assert.Ignore("저장되지 않은 편집 중 Scene은 촬영 시험이 교체하지 않습니다.");
                }

                Assert.That(EditorSceneManager.SaveScene(initialScene, TemporaryScenePath), Is.True);
            }

            var activeScenePath = EditorSceneManager.GetActiveScene().path;
            try
            {
                var bundle = Synty공간조립Web검토CapturePipeline.CaptureOneCard(string.Empty, 0);

                Assert.That(EditorSceneManager.GetActiveScene().path, Is.EqualTo(activeScenePath));
                Assert.That(bundle.Captures, Has.Count.EqualTo(4));
                Assert.That(bundle.SourceCompositionHash, Has.Length.EqualTo(64));
                Assert.That(bundle.RenderingProfileHash, Has.Length.EqualTo(64));
                Assert.That(bundle.CaptureBundleHash, Has.Length.EqualTo(64));
                Assert.That(bundle.ParentCaptureBundleHash, Is.Empty);
                Assert.That(bundle.ExpectedReviewItemRevision, Is.Zero);
                Assert.That(File.Exists(Path.Combine(bundle.OutputFolder, "capture-manifest.json")), Is.True);
                foreach (var capture in bundle.Captures)
                {
                    Assert.That(File.Exists(capture.FilePath), Is.True, capture.ViewCode);
                    var bytes = File.ReadAllBytes(capture.FilePath);
                    Assert.That(bytes.Take(8).ToArray(), Is.EqualTo(new byte[]
                    {
                        137, 80, 78, 71, 13, 10, 26, 10,
                    }));
                    Assert.That(capture.Width, Is.EqualTo(1600));
                    Assert.That(capture.Height, Is.EqualTo(900));
                    Assert.That(capture.ImageSha256, Has.Length.EqualTo(64));
                }
                Assert.That(bundle.Captures.Select(value => value.ImageSha256).Distinct().Count(),
                    Is.EqualTo(4), "네 시점이 같은 빈 frame으로 저장되면 안 됩니다.");
            }
            finally
            {
                if (createdTemporaryScene)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(TemporaryScenePath);
                }
            }
        }

        private static SyntyH공간조립검토Job CreateH2Job()
        {
            var job = new SyntyH공간조립검토Job
            {
                BatchStableId = "review-batch:test.h2",
                BatchTitle = "H2 시험",
                ReviewItemStableId = "review-item:test.h2",
                CompositionStableId = "composition:test.h2",
                DisplayName = "농장 작업 블록",
                ReviewTargetLevelCode = "H2",
                ReviewTargetStableId = "h2:block:farm-logistics-a",
                H1StableId = "h1:space:farm-workyard-a",
                H2StableId = "h2:block:farm-logistics-a",
                VariantCode = "A",
                StateProfileCode = "Default",
            };
            job.PlanHash = Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
            return job;
        }
    }
}
