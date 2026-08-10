using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Experiments.CityFarmWorldIntegration.Editor.Tests
{
    public sealed class 공용AnimationAdapterTests
    {
        [Test]
        public void 세Pack은_같은IdleWalkKey와_명시적Fallback을사용한다()
        {
            var entries = 공용AnimationPreviewBuilder.CreateEntriesForValidation();

            Assert.That(entries.Length, Is.EqualTo(3));
            Assert.That(entries.Select(value => value.PackCode), Is.EquivalentTo(new[]
            {
                월드CompositionPackCodes.Farm,
                월드CompositionPackCodes.Town,
                월드CompositionPackCodes.City,
            }));
            Assert.That(entries.Select(value => value.IdleKey.Value).Distinct().Single(),
                Is.EqualTo("locomotion.idle.v1"));
            Assert.That(entries.Select(value => value.WalkKey.Value).Distinct().Single(),
                Is.EqualTo("locomotion.walk.v1"));
            Assert.That(entries, Has.All.Matches<공용AnimationCatalogEntry>(value =>
                value.UsesFallback && value.IdleClip == null && value.WalkClip == null));
        }

        [Test]
        public void Town누락Controller는_오류로검출되고Catalog에승격되지않는다()
        {
            var report = SyntyAnimationSourceInventory.Inspect();

            Assert.That(report.MissingControllerPrefabPaths.Count, Is.EqualTo(8));
            var exception = Assert.Throws<InvalidOperationException>(
                report.EnsureNoMissingControllerReferences);
            Assert.That(exception!.Message,
                Does.StartWith("SyntyAnimatorControllerReferenceMissing:"));
            var town = LoadCatalog().Resolve(월드CompositionPackCodes.Town);
            Assert.That(town.SourceKindCode,
                Is.EqualTo(공용AnimationSourceKindCodes.ProceduralFallback));
        }

        [Test]
        public void Adapter는_Humanoid와RootMotion비활성및진단을유지한다()
        {
            foreach (var adapter in LoadPreviewComponents<공용AnimationAdapter>())
            {
                Assert.That(adapter.ValidateWiring(), Is.True, adapter.PackCode);
                Assert.That(adapter.Animator.avatar.isHuman, Is.True);
                Assert.That(adapter.Animator.applyRootMotion, Is.False);
                Assert.That(adapter.Animator.runtimeAnimatorController, Is.Null);
                Assert.That(adapter.DiagnosticCode,
                    Is.EqualTo("animation.clip-unavailable:using-procedural-fallback"));
            }
        }

        [Test]
        public void RouteFollower만_사람위치를바꾸고Adapter는Intent만표현한다()
        {
            var entry = LoadCatalog().Resolve(월드CompositionPackCodes.Farm);
            var actor = Object.Instantiate(entry.CharacterPrefab);
            try
            {
                var animator = actor.GetComponentInChildren<Animator>(true);
                var adapter = actor.AddComponent<공용AnimationAdapter>();
                adapter.Configure(entry, animator);
                var beforeAdapter = actor.transform.position;
                adapter.ApplyIntent(공용AnimationIntentCodes.Walk);
                adapter.TickPresentation(.2f);
                Assert.That(actor.transform.position, Is.EqualTo(beforeAdapter));

                var start = new GameObject("start").transform;
                var end = new GameObject("end").transform;
                start.position = Vector3.zero;
                end.position = Vector3.forward * 5f;
                var follower = actor.AddComponent<공용ActorRouteFollower>();
                follower.Configure(start, end, adapter, 2f, 0f);
                follower.TickRoute(.5f);
                Assert.That(actor.transform.position.z, Is.EqualTo(1f).Within(.001f));
                Assert.That(adapter.CurrentIntentCode, Is.EqualTo(공용AnimationIntentCodes.Walk));
                Object.DestroyImmediate(start.gameObject);
                Object.DestroyImmediate(end.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void 잘못된Intent와_불완전한Catalog를거부한다()
        {
            var adapter = LoadPreviewComponents<공용AnimationAdapter>().First();
            Assert.Throws<ArgumentException>(() => adapter.ApplyIntent("confirm-order"));

            var emptyCatalog = ScriptableObject.CreateInstance<공용AnimationCatalog>();
            try
            {
                Assert.Throws<InvalidOperationException>(emptyCatalog.Validate);
            }
            finally
            {
                Object.DestroyImmediate(emptyCatalog);
            }
        }

        [Test]
        public void PreviewScene은_대표Actor셋과_PerspectiveCamera를저장한다()
        {
            var previous = SceneManager.GetActiveScene().path;
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    공용AnimationPreviewBuilder.PreviewScenePath,
                    OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(value =>
                    value.GetComponentsInChildren<공용AnimationAdapter>(true)).Count(), Is.EqualTo(3));
                Assert.That(roots.SelectMany(value =>
                    value.GetComponentsInChildren<공용ActorRouteFollower>(true)).Count(), Is.EqualTo(3));
                var camera = roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Single();
                Assert.That(camera.orthographic, Is.False);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previous))
                    EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
        }

        private static 공용AnimationCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<공용AnimationCatalog>(
                공용AnimationPreviewBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.Validate);
            return catalog;
        }

        private static T[] LoadPreviewComponents<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != 공용AnimationPreviewBuilder.PreviewScenePath)
                scene = EditorSceneManager.OpenScene(
                    공용AnimationPreviewBuilder.PreviewScenePath,
                    OpenSceneMode.Single);
            return scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
