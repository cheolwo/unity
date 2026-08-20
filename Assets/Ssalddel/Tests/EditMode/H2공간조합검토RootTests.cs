using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class H2공간조합검토RootTests
    {
        [Test]
        public void 상세조립법_6종은_하위H1과_Synty표현을_가진_H2Root로_생성된다()
        {
            H2공간조합검토RootBuilder.BuildRoots();

            var paths = AssetDatabase.FindAssets("t:Prefab", new[]
                { H2공간조합검토RootBuilder.OutputFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Assert.That(paths, Has.Length.EqualTo(6));
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var marker = prefab.GetComponent<H2공간조합검토Root>();
                Assert.That(marker, Is.Not.Null, path);
                Assert.That(marker.Validate(), Is.True, path);
                Assert.That(marker.ChildH1StableIds.Count, Is.GreaterThanOrEqualTo(2), path);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, path);
                Assert.That(prefab.GetComponentsInChildren<Transform>(true)
                    .Count(value => value.name.StartsWith("H1_", StringComparison.Ordinal)),
                    Is.EqualTo(marker.ChildH1StableIds.Count), path);
            }

            var catalogPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", H2공간조합검토RootBuilder.RootCatalogPath));
            Assert.That(File.Exists(catalogPath), Is.True);
            Assert.That(Synty공간조립Web검토CapturePipeline.ExpectedHierarchyCaptureCount("H2"),
                Is.EqualTo(5));
        }
    }
}
