using System;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor
{
    public static class 통합월드자유이동Builder
    {
        [MenuItem("Ssalddel/WORLD-TRAVERSAL-1/SimulationWorldShell 전체 지도 자유 이동 적용")]
        public static void ApplyToSimulationWorldShell()
        {
            var scene = OpenShell();
            var player = UnityEngine.Object.FindFirstObjectByType<플레이어경관Controller>(
                             FindObjectsInactive.Include)
                         ?? throw new InvalidOperationException("UnifiedWorldPlayerMissing");
            player.ConfigureTraversalProfile(평창군플레이어경관Fixture.Create());
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, SimulationWorldShellBuilder.ScenePath))
                throw new InvalidOperationException("UnifiedWorldTraversalSceneSaveFailed");
            ValidateOpenScene();
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD-TRAVERSAL-1: SimulationWorldShell 전체 평창 지도 WASD 이동 범위를 적용했습니다.");
        }

        [MenuItem("Ssalddel/WORLD-TRAVERSAL-1/Validate Open Scene")]
        public static void ValidateOpenScene()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<플레이어경관Controller>(
                             FindObjectsInactive.Include)
                         ?? throw new InvalidOperationException("UnifiedWorldPlayerMissing");
            if (!player.ValidateWiring())
                throw new InvalidOperationException("UnifiedWorldPlayerWiringInvalid");
            if (!player.HasMovementSafetyGate)
                throw new InvalidOperationException("UnifiedWorldMovementSafetyGateMissing");

            var profile = player.Profile;
            var projection = 평창군법정동WorldFixture.Create();
            var points = projection.Nodes.SelectMany(node => node.BoundaryPoints).ToArray();
            if (points.Any(point => point.X < profile.MinimumX || point.X > profile.MaximumX
                                    || point.Z < profile.MinimumZ || point.Z > profile.MaximumZ))
                throw new InvalidOperationException("UnifiedWorldTraversalDoesNotCoverEveryRegion");

            var terrain = GameObject.Find("ContinuousTerrain_DEMIncomplete");
            if (terrain == null || terrain.GetComponent<MeshCollider>() == null)
                throw new InvalidOperationException("UnifiedWorldContinuousWalkableTerrainMissing");
        }

        private static Scene OpenShell()
        {
            var active = SceneManager.GetActiveScene();
            return active.path == SimulationWorldShellBuilder.ScenePath
                ? active
                : EditorSceneManager.OpenScene(
                    SimulationWorldShellBuilder.ScenePath, OpenSceneMode.Single);
        }
    }
}
