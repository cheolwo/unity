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
    }
}
