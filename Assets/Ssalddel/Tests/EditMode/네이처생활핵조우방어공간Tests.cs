using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 네이처생활핵조우방어공간Tests
    {
        [Test]
        public void 기존네이처H3는_세노드와_복귀간선을가진_걷기공간으로배치된다()
        {
            네이처생활핵조우방어SceneBuilder.BuildCompactPrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                네이처생활핵조우방어SceneBuilder.CompactPrefabPath);
            var root = prefab.GetComponent<네이처생활핵조우방어공간Root>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root!.Validate(), Is.True);
            Assert.That(root.Nodes.Count, Is.EqualTo(3));
            Assert.That(root.Edges.Count, Is.EqualTo(3));
            Assert.That(root.Connectors.Count, Is.EqualTo(6));
            Assert.That(root.Edges.Any(value =>
                value.RelationKindCode == "RecoveryHandoff"
                && value.FromNodeStableId == "h2-nature-defense-ring"
                && value.ToNodeStableId == "h2-nature-home-core"), Is.True);
        }

        [Test]
        public void 가시공간자산은_PolygonNature원본Prefab만사용한다()
        {
            네이처생활핵조우방어SceneBuilder.BuildCompactPrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                네이처생활핵조우방어SceneBuilder.CompactPrefabPath);
            var root = prefab.GetComponent<네이처생활핵조우방어공간Root>();
            var prefabRoots = root.GetComponentsInChildren<Transform>(true)
                .Select(value => PrefabUtility.GetNearestPrefabInstanceRoot(
                    value.gameObject)).Where(value => value != null)
                .Distinct().ToArray();

            Assert.That(prefabRoots.Length, Is.GreaterThanOrEqualTo(35));
            Assert.That(prefabRoots.All(value =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(value)
                    .StartsWith("Assets/Synty/PolygonNature/",
                        StringComparison.Ordinal)), Is.True);
        }

    }
}
