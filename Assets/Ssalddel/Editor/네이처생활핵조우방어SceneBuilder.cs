using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 기존 네이처 H3 설계를 Nature 팩만으로 canonical Scene에 compact 배치합니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public static class 네이처생활핵조우방어SceneBuilder
    {
        public const string RootName = "NatureHomeEncounterDefense_H3_PresentationOnly";
        public const string H3StableId =
            "h3-candidate:nature-home-encounter-defense";
        public const string SemanticRevision =
            "simulation-world-semantic-spatial-relations.r2";
        public const string TheoryHashSha256 =
            "2ffa247b9fc1883229a1e1262108ba3050a41c230e0a44c69bc17dceb085c9fb";
        public const string CompactPrefabPath =
            "Assets/Ssalddel/Generated/NatureH3/NatureHomeEncounterDefenseCompact.prefab";

        private const string NatureRoot = "Assets/Synty/PolygonNature/Prefabs/";
        private static readonly Vector3 HomePosition = Vector3.zero;
        private static readonly Vector3 EncounterPosition = new(22f, 0f, 24f);
        private static readonly Vector3 DefensePosition = new(45f, 0f, 0f);

        [MenuItem("Ssalddel/검토실/네이처 공간/H2 생활핵·조우·방어/Apply H3 Walkable Landscape")]
        public static void ApplyToCanonicalScene()
        {
            var scene = EditorSceneManager.OpenScene(
                통합WorldScenePolicy.CanonicalScenePath, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include);
            var sceneTransforms = UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(value => value.gameObject.scene == scene).ToArray();
            var worldMapRoot = sceneTransforms.FirstOrDefault(value =>
                value.name == "WorldMapRoot");
            if (worldMapRoot == null)
                throw new InvalidOperationException("WorldMapRootMissing:"
                    + string.Join(",", scene.GetRootGameObjects()
                        .Select(value => value.name)));
            var previous = worldMapRoot.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(worldMapRoot, true);
            var priorEncounterRoot = worldMapRoot.Find(
                네이처탐험조우SceneBuilder.RootName);
            root.position = player != null
                ? player.transform.position
                : priorEncounterRoot != null
                    ? priorEncounterRoot.position
                    : Vector3.zero;
            var marker = BuildSpaceRoot(root);

            var encounterPresenter = UnityEngine.Object.FindFirstObjectByType<
                네이처조우Presenter>(FindObjectsInactive.Include);
            if (encounterPresenter != null && player != null)
                encounterPresenter.Configure(player,
                    marker.FindConnectorAnchor("ThreatInput")!, null);

            EditorUtility.SetDirty(marker);
            if (encounterPresenter != null) EditorUtility.SetDirty(encounterPresenter);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene,
                    통합WorldScenePolicy.CanonicalScenePath))
                throw new InvalidOperationException("NatureSpaceSceneSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateOpenScene();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("NATURE-SPACE-1 applied: " + RootName);
        }

        [MenuItem("Ssalddel/검토실/네이처 공간/H2 생활핵·조우·방어/Build Compact H3 Prefab")]
        public static void BuildCompactPrefab()
        {
            EnsureAssetFolder("Assets/Ssalddel/Generated", "NatureH3");
            var temporary = new GameObject(RootName);
            try
            {
                var marker = BuildSpaceRoot(temporary.transform);
                if (!marker.Validate())
                    throw new InvalidOperationException("NatureH3SpaceContractInvalid");
                var prefab = PrefabUtility.SaveAsPrefabAsset(temporary,
                    CompactPrefabPath) ?? throw new InvalidOperationException(
                    "NatureH3CompactPrefabSaveFailed");
                ValidateMarker(prefab.GetComponent<네이처생활핵조우방어공간Root>());
                AssetDatabase.SaveAssets();
                Debug.Log("NATURE-SPACE-1 prefab built: " + CompactPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        [MenuItem("Ssalddel/검토실/네이처 공간/H2 생활핵·조우·방어/Validate Open Scene")]
        public static void ValidateOpenScene()
        {
            var marker = UnityEngine.Object.FindFirstObjectByType<
                네이처생활핵조우방어공간Root>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("NatureH3SpaceRootMissing");
            ValidateMarker(marker);
            var player = UnityEngine.Object.FindFirstObjectByType<
                플레이어경관Controller>(FindObjectsInactive.Include);
            var safeCore = marker.FindConnectorAnchor("SafeCoreGate")
                ?? throw new InvalidOperationException("NatureSafeCoreGateMissing");
            if (player != null
                && Vector3.Distance(player.transform.position, safeCore.position) > 5f)
                throw new InvalidOperationException("NaturePlayerStartOutsideSafeCore");
        }

        public static void ValidateCompactPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompactPrefabPath)
                ?? throw new InvalidOperationException("NatureH3CompactPrefabMissing");
            ValidateMarker(prefab.GetComponent<네이처생활핵조우방어공간Root>());
        }

        private static void ValidateMarker(네이처생활핵조우방어공간Root marker)
        {
            if (marker == null || !marker.Validate())
                throw new InvalidOperationException("NatureH3SpaceContractInvalid");
            var prefabRoots = marker.GetComponentsInChildren<Transform>(true)
                .Select(value => PrefabUtility.GetNearestPrefabInstanceRoot(
                    value.gameObject)).Where(value => value != null)
                .Distinct().ToArray();
            if (prefabRoots.Length < 35)
                throw new InvalidOperationException(
                    "NatureH3PrefabInventoryInsufficient:" + prefabRoots.Length);
            if (prefabRoots.Any(value => !PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    value).StartsWith("Assets/Synty/PolygonNature/",
                    StringComparison.Ordinal)))
                throw new InvalidOperationException("NatureH3NonNaturePrefabDetected");
        }

        private static 네이처생활핵조우방어공간Root BuildSpaceRoot(Transform root)
        {
            var visuals = Child(root, "NaturePackVisuals");
            var nodesRoot = Child(root, "H2Nodes");
            var edgesRoot = Child(root, "Edges");
            var connectorsRoot = Child(root, "Connectors");
            BuildWalkableFoundation(root);

            var home = Node(nodesRoot, "h2-nature-home-core", HomePosition,
                "h1-stock:nature-safe-recovery-camp",
                "h1-stock:nature-shelter", "h1-stock:nature-trailhead");
            var encounter = Node(nodesRoot, "h2-nature-encounter-route",
                EncounterPosition, "h1-stock:nature-threat-watch",
                "h1-stock:nature-incident-trace",
                "h1-stock:nature-emergency-retreat");
            var defense = Node(nodesRoot, "h2-nature-defense-ring",
                DefensePosition, "h1-stock:nature-threat-watch",
                "h1-stock:nature-shelter",
                "h1-stock:nature-safe-recovery-camp");

            BuildClearing(visuals, "HomeCore", HomePosition, 0f);
            BuildClearing(visuals, "Encounter", EncounterPosition, 35f);
            BuildClearing(visuals, "DefenseRing", DefensePosition, -25f);
            BuildPath(visuals, "ExplorePath", HomePosition, EncounterPosition);
            BuildPath(visuals, "ThreatPath", EncounterPosition, DefensePosition);
            BuildPath(visuals, "RecoveryPath", DefensePosition, HomePosition);
            BuildForestBoundary(visuals);
            BuildPlaceDetails(visuals);

            var edgeBindings = new[]
            {
                Edge(edgesRoot, "relation:h3-nature-home-encounter-defense:76e90e4a6ffdc9fe",
                    "h2-nature-home-core", "h2-nature-encounter-route",
                    "PlayerTraversal", HomePosition, EncounterPosition),
                Edge(edgesRoot, "relation:h3-nature-home-encounter-defense:16abdb87214982f7",
                    "h2-nature-encounter-route", "h2-nature-defense-ring",
                    "PlayerTraversal", EncounterPosition, DefensePosition),
                Edge(edgesRoot, "relation:h3-nature-home-encounter-defense:70143a0b9a204158",
                    "h2-nature-defense-ring", "h2-nature-home-core",
                    "RecoveryHandoff", DefensePosition, HomePosition),
            };
            var connectorBindings = new[]
            {
                Connector(connectorsRoot, "ingress", "Ingress", "Input",
                    "h2-nature-home-core", new Vector3(-5f, 0f, -4f)),
                Connector(connectorsRoot, "egress", "Egress", "Output",
                    "h2-nature-defense-ring", new Vector3(49f, 0f, -5f)),
                Connector(connectorsRoot, "safecoregate", "SafeCoreGate", "Input",
                    "h2-nature-home-core", new Vector3(-1f, 0f, -2f)),
                Connector(connectorsRoot, "explorationoutput", "ExplorationOutput",
                    "Output", "h2-nature-home-core", new Vector3(7f, 0f, 7f)),
                Connector(connectorsRoot, "threatinput", "ThreatInput", "Input",
                    "h2-nature-defense-ring", new Vector3(53f, 0f, 7f)),
                Connector(connectorsRoot, "recoveryreturn", "RecoveryReturn", "Input",
                    "h2-nature-home-core", new Vector3(2f, 0f, -1f)),
            };

            var marker = root.gameObject.AddComponent<네이처생활핵조우방어공간Root>();
            marker.Configure(H3StableId, SemanticRevision, TheoryHashSha256,
                "CompactWalkable",
                new[]
                {
                    new 네이처H3NodeBinding("h2-nature-home-core",
                        "h2-candidate:nature-home-core", home),
                    new 네이처H3NodeBinding("h2-nature-encounter-route",
                        "h2-candidate:nature-encounter-route", encounter),
                    new 네이처H3NodeBinding("h2-nature-defense-ring",
                        "h2-candidate:nature-defense-ring", defense),
                }, edgeBindings, connectorBindings);
            return marker;
        }

        private static Transform Node(Transform parent, string nodeId,
            Vector3 localPosition, params string[] h1StableIds)
        {
            var node = Child(parent, nodeId);
            node.localPosition = localPosition;
            foreach (var h1 in h1StableIds.OrderBy(value => value,
                         StringComparer.Ordinal))
                Child(node, "H1_" + h1);
            return node;
        }

        private static 네이처H3EdgeBinding Edge(Transform parent, string edgeId,
            string fromNode, string toNode, string kind, Vector3 from, Vector3 to)
        {
            var anchor = Child(parent, edgeId);
            anchor.localPosition = (from + to) * .5f;
            anchor.localRotation = Quaternion.LookRotation((to - from).normalized,
                Vector3.up);
            return new 네이처H3EdgeBinding(edgeId, fromNode, toNode, kind,
                "Directed");
        }

        private static 네이처H3ConnectorBinding Connector(Transform parent,
            string suffix, string role, string direction, string nodeId,
            Vector3 localPosition)
        {
            var id = "connector:h3:nature-home-encounter-defense:" + suffix;
            var anchor = Child(parent, id);
            anchor.localPosition = localPosition;
            return new 네이처H3ConnectorBinding(id, role, direction, nodeId, anchor);
        }

        private static void BuildWalkableFoundation(Transform root)
        {
            var foundation = Child(root, "WalkableFoundation_PresentationSupport");
            foundation.localPosition = new Vector3(22f, -.3f, 7f);
            var collider = foundation.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(76f, .6f, 66f);
        }

        private static void BuildClearing(Transform parent, string name,
            Vector3 position, float yaw)
        {
            var clearing = Child(parent, "Clearing_" + name);
            clearing.localPosition = position;
            Instance(clearing, "Terrain/SM_Terrain_Ground_Mound_Large_01.prefab",
                Vector3.zero, new Vector3(2.6f, 1f, 2.6f), yaw, false);
            for (var index = 0; index < 5; index++)
            {
                var angle = index * Mathf.PI * 2f / 5f;
                Instance(clearing, "Plants/SM_Plant_Grass_0" + (index % 5 + 1)
                    + ".prefab", new Vector3(Mathf.Cos(angle) * 6.2f, 0f,
                        Mathf.Sin(angle) * 6.2f), Vector3.one, index * 31f, false);
            }
        }

        private static void BuildPath(Transform parent, string name,
            Vector3 from, Vector3 to)
        {
            var path = Child(parent, "Path_" + name);
            var direction = to - from;
            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            const int segments = 7;
            for (var index = 1; index < segments; index++)
            {
                var position = Vector3.Lerp(from, to, index / (float)segments);
                Instance(path, "Terrain/SM_Terrain_DustPile_Long_01.prefab",
                    position, new Vector3(1.35f, 1f, 1.55f), yaw, false);
            }
        }

        private static void BuildForestBoundary(Transform parent)
        {
            var boundary = Child(parent, "ForestBoundary");
            var points = new[]
            {
                new Vector3(-10f,0f,-12f), new Vector3(-11f,0f,2f),
                new Vector3(-8f,0f,17f), new Vector3(0f,0f,31f),
                new Vector3(12f,0f,36f), new Vector3(25f,0f,38f),
                new Vector3(38f,0f,34f), new Vector3(51f,0f,28f),
                new Vector3(59f,0f,16f), new Vector3(61f,0f,2f),
                new Vector3(57f,0f,-14f), new Vector3(45f,0f,-19f),
                new Vector3(31f,0f,-22f), new Vector3(16f,0f,-21f),
                new Vector3(1f,0f,-19f), new Vector3(8f,0f,14f),
                new Vector3(17f,0f,30f), new Vector3(37f,0f,24f),
            };
            for (var index = 0; index < points.Length; index++)
            {
                var path = index % 3 == 0
                    ? "Trees/SM_Tree_Birch_02.prefab"
                    : index % 3 == 1
                        ? "Trees/SM_Tree_Pine_01.prefab"
                        : "Trees/SM_Tree_PolyPine_02.prefab";
                var scale = Vector3.one * (index % 4 == 0 ? .86f : 1f);
                Instance(boundary, path, points[index], scale,
                    (index * 47) % 360, true);
            }
        }

        private static void BuildPlaceDetails(Transform parent)
        {
            var details = Child(parent, "PlaceDetails");
            Instance(details, "Props/SM_Prop_CampFire_01.prefab",
                new Vector3(1f, 0f, 1f), Vector3.one, 0f, true);
            Instance(details, "Trees/SM_Tree_Log_01.prefab",
                new Vector3(-2f, 0f, 2f), Vector3.one, 25f, true);
            Instance(details, "Props/SM_Prop_RoadSign_01.prefab",
                new Vector3(7f, 0f, 7f), Vector3.one, 35f, true);
            Instance(details, "Props/SM_Prop_TorchStick_01.prefab",
                new Vector3(53f, 0f, 7f), Vector3.one, -40f, true);
            Instance(details, "Props/SM_Prop_StoneWall_02.prefab",
                new Vector3(43f, 0f, -6f), Vector3.one, 15f, true);
            var rockPoints = new[]
            {
                new Vector3(13f,0f,22f), new Vector3(29f,0f,24f),
                new Vector3(38f,0f,10f), new Vector3(51f,0f,10f),
                new Vector3(54f,0f,-8f),
            };
            for (var index = 0; index < rockPoints.Length; index++)
                Instance(details, "Rocks/SM_Rock_0" + (index % 4 + 1)
                    + ".prefab", rockPoints[index], Vector3.one,
                    index * 39f, true);
        }

        private static GameObject Instance(Transform parent, string relativePath,
            Vector3 localPosition, Vector3 localScale, float yaw,
            bool keepColliders)
        {
            var assetPath = NatureRoot + relativePath;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)
                ?? throw new InvalidOperationException("NaturePrefabMissing:" + assetPath);
            var value = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject
                ?? throw new InvalidOperationException("NaturePrefabInstantiateFailed:" + assetPath);
            value.transform.localPosition = localPosition;
            value.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            value.transform.localScale = localScale;
            if (!keepColliders)
                foreach (var collider in value.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            return value;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
                throw new InvalidOperationException("NatureH3AssetParentMissing:" + parent);
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
