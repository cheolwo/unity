using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 이론 H1~H3 기준안을 canonical SimulationWorldShell의 H5 상대 위치에 배치한다.
    /// 배치는 표현 증거이며 Simulation·운영 상태나 AreaSet 이동 권위를 만들지 않는다.
    /// </summary>
    public static class H계층실증ShellBuilder
    {
        public const string RootName = "H계층실증Root_PresentationOnly";
        public const string EvidenceStableId =
            "scene-evidence:simulation-world-shell:h1-h3-baselines.v1";
        public const string PlacementPolicyCode =
            "FarmCenteredScenarioLocalMeters";
        // H5 의미 좌표는 유지하되, Player 크기에서 서로 다른 H4 핵이 겹치지
        // 않도록 검토 Scene의 .12보다 넓은 공식 World 표현 축척을 사용한다.
        public const float ScenarioMetersToUnityUnits = .24f;
        public const string GameViewEvidencePath =
            "Documentation/Changes/2026-08-23-h-hierarchy-scene-evidence/"
            + "h-hierarchy-game-view.png";

        private const string H5RelativePath =
            "eng/world-seedbeds/generated/h5-world-layout.v1.json";
        private const string CorridorMaterialPath =
            "Assets/Ssalddel/Generated/HHierarchyEvidence/Materials/전이회랑.mat";
        private const string EvidenceMaterialFolder =
            "Assets/Ssalddel/Generated/HHierarchyEvidence/Materials";
        private static readonly string[] ReviewMaterialPrefixes =
        {
            "Assets/Ssalddel/Generated/AreaSetCompositionReview/",
            "Assets/Ssalddel/Generated/H2CompositionReview/",
        };
        private static readonly string[] ReviewMaterialNames =
        {
            "H3경계", "H2경계", "연결지점", "WI동선",
            "H1공간경계", "H2기준바닥", "H2내부관계", "H2외부연결구",
        };

        [MenuItem("Ssalddel/통합 월드/H 계층 실증/H1-H3 기준안을 공식 Scene에 배치")]
        public static void BuildCanonicalSceneEvidence()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "HHierarchyEvidenceBuilderRequiresEditMode");

            var h5 = ReadH5();
            var shellScene = EditorSceneManager.OpenScene(
                SimulationWorldShellBuilder.ScenePath, OpenSceneMode.Single);
            var shell = shellScene.GetRootGameObjects().Single(value =>
                value.name == SimulationWorldShellBuilder.RootName);
            RemoveOwnedRoot(shell.transform);

            var reviewScene = EditorSceneManager.OpenScene(
                AreaSet구성패턴검토실Builder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var director = reviewScene.GetRootGameObjects()
                    .Single(value => value.name ==
                        AreaSet구성패턴검토실Builder.RootName)
                    .GetComponent<AreaSet구성패턴검토Director>()
                    ?? throw new InvalidOperationException(
                        "HHierarchyEvidenceReviewDirectorMissing");
                var baselines = director.PatternRoots
                    .Where(value => value != null
                                    && value.PatternKindCode == "Baseline")
                    .ToDictionary(value => value.AreaRoleCode,
                        StringComparer.Ordinal);
                if (baselines.Count != 4)
                    throw new InvalidOperationException(
                        "HHierarchyEvidenceBaselineCountInvalid");

                var evidenceRoot = new GameObject(RootName).transform;
                SceneManager.MoveGameObjectToScene(evidenceRoot.gameObject, shellScene);
                evidenceRoot.SetParent(shell.transform, false);
                var areaRoot = Child(evidenceRoot, "AreaSets_H4_4");
                var corridorRoot = Child(evidenceRoot, "TransitionCorridors_H5_3");
                var farmPlacement = h5.AreaPlacements.Single(value =>
                    value.AreaRoleCode == "Farm");
                var instances = new List<AreaSet구성패턴검토Root>();

                foreach (var placement in h5.AreaPlacements
                             .OrderBy(value => value.AreaRoleCode,
                                 StringComparer.Ordinal))
                {
                    var clone = Object.Instantiate(
                        baselines[placement.AreaRoleCode].gameObject);
                    clone.name = placement.AreaRoleCode + "_H4_Baseline";
                    SceneManager.MoveGameObjectToScene(clone, shellScene);
                    clone.transform.SetParent(areaRoot, false);
                    clone.transform.localPosition = Position(
                        placement.XMeters - farmPlacement.XMeters,
                        placement.ZMeters - farmPlacement.ZMeters);
                    clone.transform.localRotation = Quaternion.Euler(
                        0f, placement.RotationDegrees, 0f);
                    clone.SetActive(true);
                    var marker = clone.GetComponent<AreaSet구성패턴검토Root>()
                                 ?? throw new InvalidOperationException(
                                     "HHierarchyEvidenceMarkerMissing");
                    DisableRuntimeAuthorityComponents(clone);
                    DisableEmbeddedReviewHelpers(marker.SyntyPresentation);
                    BuildTerrainUnderlay(marker, placement.AreaRoleCode);
                    marker.ApplyVisibility(false, false, false, false, true);
                    instances.Add(marker);
                }

                BuildCorridors(corridorRoot, h5, farmPlacement);
                var singlePlayerView = shell.GetComponent<단일PlayerViewController>()
                                       ?? throw new InvalidOperationException(
                                           "HHierarchyEvidenceSinglePlayerViewMissing");
                var placementRules = H공간배치규칙EditorEngine.Apply(
                    evidenceRoot, instances,
                    singlePlayerView.Player);
                var source = instances[0];
                var evidence = evidenceRoot.gameObject
                    .AddComponent<H계층배치증거Root>();
                evidence.Configure(
                    EvidenceStableId,
                    h5.WorldLayoutStableId,
                    h5.WorldLayoutRevision,
                    h5.WorldLayoutHash,
                    source.SourceRevision,
                    source.SourceHashSha256,
                    PlacementPolicyCode,
                    ScenarioMetersToUnityUnits,
                    instances.ToArray(),
                    corridorRoot,
                    placementRules);
                evidence.SetHierarchyOverlay(false);

                singlePlayerView.SetApprovedEnvironmentRoots(evidenceRoot);
                EditorUtility.SetDirty(singlePlayerView);
                EditorUtility.SetDirty(evidence);
                EditorSceneManager.MarkSceneDirty(shellScene);
            }
            finally
            {
                if (reviewScene.isLoaded)
                    EditorSceneManager.CloseScene(reviewScene, true);
            }

            SceneManager.SetActiveScene(shellScene);
            if (!EditorSceneManager.SaveScene(
                    shellScene, SimulationWorldShellBuilder.ScenePath))
                throw new InvalidOperationException(
                    "HHierarchyEvidenceSceneSaveFailed");
            AssetDatabase.SaveAssets();
            ValidateOpenScene();
            Selection.activeGameObject = GameObject.Find(RootName);
            SceneView.RepaintAll();
            Debug.Log("WORLD-H-EVIDENCE-1 built:AreaSets=4;Corridors=3;"
                      + SimulationWorldShellBuilder.ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildCanonicalSceneEvidence();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Ssalddel/통합 월드/H 계층 실증/열린 공식 Scene 검증")]
        public static void ValidateOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != SimulationWorldShellBuilder.ScenePath)
                throw new InvalidOperationException(
                    "HHierarchyEvidenceCanonicalSceneNotOpen");
            var shell = scene.GetRootGameObjects().Single(value =>
                value.name == SimulationWorldShellBuilder.RootName);
            var root = shell.transform.Find(RootName)
                       ?? throw new InvalidOperationException(
                           "HHierarchyEvidenceRootMissing");
            var evidence = root.GetComponent<H계층배치증거Root>()
                           ?? throw new InvalidOperationException(
                               "HHierarchyEvidenceComponentMissing");
            var rules = evidence.PlacementRuleEvidence;
            var placementHierarchy = rules.PlacementControlHierarchy;
            var frontage = rules.RoadsideFrontagePatterns.Single();
            var view = root.parent.GetComponent<단일PlayerViewController>();
            Debug.Log($"WORLD-H-RULE-1 summary:revision={rules.RuleRevision};"
                      + $"areas={rules.AreaSetRecords.Length};"
                      + $"objects={rules.H1Records.Length};"
                      + $"fenceChains={rules.FenceChainCount};"
                      + $"farmCoreAnchors={rules.FarmCoreAnchorCount};"
                      + $"relocated={rules.RelocatedObjectCount};"
                      + $"normalized={rules.NormalizedFootprintCount};"
                      + $"violations={rules.Violations.Length};"
                      + $"playerHeight={placementHierarchy.PlayerReferenceHeight:F3};"
                      + $"maxBuildingHeightRatio={rules.H1Records.Where(value => value.CategoryCode == "Building").Max(value => value.HeightToPlayerRatio):F3};"
                      + $"maxBuildingFootprintRatio={rules.H1Records.Where(value => value.CategoryCode == "Building").Max(value => value.FootprintToPlayerRatio):F3};"
                      + $"frontageH3={frontage.H3LandscapeNodeName};"
                      + $"frontageH2={frontage.H2BlockNodeName};"
                      + $"entranceGap={frontage.EntranceGapWidth:F3};"
                      + $"buildingSetback={frontage.BuildingSetback:F3};"
                      + $"player={view.Player.transform.position}");
            evidence.Validate();
            if (!evidence.ShowHierarchyOverlayOnPlay
                && evidence.AreaSetRoots.Any(value =>
                    value.H3Boundaries.gameObject.activeSelf
                    || value.H2Boundaries.gameObject.activeSelf
                    || value.Connectors.gameObject.activeSelf
                    || value.WiRoutes.gameObject.activeSelf))
                throw new InvalidOperationException(
                    "HHierarchyEvidenceOverlayVisibleByDefault");
            var singlePlayerView = shell.GetComponent<단일PlayerViewController>()
                                   ?? throw new InvalidOperationException(
                                       "HHierarchyEvidenceSinglePlayerViewMissing");
            singlePlayerView.ValidateWiring();
            if (!singlePlayerView.ApprovedEnvironmentRoots.Contains(root))
                throw new InvalidOperationException(
                    "HHierarchyEvidenceVisibilityPolicyMissing");
            if (root.GetComponentsInChildren<Renderer>(true).Any(value =>
                    value.enabled && value.gameObject.activeInHierarchy
                                  && value.sharedMaterials.Any(IsReviewMaterial)))
                throw new InvalidOperationException(
                    "HHierarchyEvidenceReviewHelperRendererVisible");
            Debug.Log($"WORLD-H-EVIDENCE-1 validation passed:"
                      + $"H3={evidence.H3PlacementCount};"
                      + $"H2={evidence.H2PlacementCount};"
                      + $"H1={evidence.H1ExpressionCount}");
        }

        [MenuItem("Ssalddel/통합 월드/H 계층 실증/활성 Material 감사")]
        public static void LogEnabledMaterialAudit()
        {
            var root = GameObject.Find(RootName)
                       ?? throw new InvalidOperationException(
                           "HHierarchyEvidenceRootMissing");
            var summary = root.GetComponentsInChildren<Renderer>(true)
                .Where(value => value.enabled && value.gameObject.activeInHierarchy)
                .SelectMany(value => value.sharedMaterials)
                .Where(value => value != null)
                .GroupBy(value => value.name, StringComparer.Ordinal)
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => $"{value.Key}={value.Count()}@"
                                 + AssetDatabase.GetAssetPath(value.First()));
            Debug.Log("WORLD-H-EVIDENCE-1 enabled-materials:\n"
                      + string.Join("\n", summary));
        }

        [MenuItem("Ssalddel/통합 월드/H 계층 실증/H1 배치 구조 감사")]
        public static void LogH1PlacementAudit()
        {
            var root = GameObject.Find(RootName)
                       ?? throw new InvalidOperationException(
                           "HHierarchyEvidenceRootMissing");
            var evidence = root.GetComponent<H계층배치증거Root>()
                           ?? throw new InvalidOperationException(
                               "HHierarchyEvidenceComponentMissing");
            foreach (var area in evidence.AreaSetRoots.OrderBy(value =>
                         value.AreaRoleCode, StringComparer.Ordinal))
            foreach (Transform h1 in area.SyntyPresentation)
            {
                var renderers = h1.GetComponentsInChildren<Renderer>(true)
                    .Where(value => value.enabled
                                    && value.gameObject.activeInHierarchy)
                    .ToArray();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                    bounds.Encapsulate(renderers[index].bounds);
                var names = h1.GetComponentsInChildren<Transform>(true)
                    .Select(value => value.name)
                    .Where(value => value.Contains("Fence",
                                         StringComparison.OrdinalIgnoreCase)
                                    || value.Contains("Bld",
                                        StringComparison.OrdinalIgnoreCase)
                                    || value.Contains("House",
                                        StringComparison.OrdinalIgnoreCase)
                                    || value.Contains("Mountain",
                                        StringComparison.OrdinalIgnoreCase)
                                    || value.Contains("Cliff",
                                        StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.Ordinal)
                    .Take(8);
                Debug.Log($"WORLD-H-RULE-1 audit:{area.AreaRoleCode};"
                          + $"{h1.name};position={h1.position};"
                          + $"bounds={bounds.size};markers={string.Join(",", names)}");
            }
        }

        public static void LogH1PlacementAuditFromCommandLine()
        {
            EditorSceneManager.OpenScene(SimulationWorldShellBuilder.ScenePath,
                OpenSceneMode.Single);
            LogH1PlacementAudit();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void LogEnabledMaterialAuditFromCommandLine()
        {
            EditorSceneManager.OpenScene(SimulationWorldShellBuilder.ScenePath,
                OpenSceneMode.Single);
            LogEnabledMaterialAudit();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Ssalddel/통합 월드/H 계층 실증/Play Mode Game View 저장")]
        public static void CaptureGameViewEvidence()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "HHierarchyEvidenceCaptureRequiresPlayMode");
            var evidence = Object.FindAnyObjectByType<H계층배치증거Root>(
                               FindObjectsInactive.Include)
                           ?? throw new InvalidOperationException(
                               "HHierarchyEvidenceRootMissing");
            evidence.Validate();
            var view = Object.FindAnyObjectByType<단일PlayerViewController>(
                           FindObjectsInactive.Include)
                       ?? throw new InvalidOperationException(
                           "HHierarchyEvidenceSinglePlayerViewMissing");
            view.ApplyView();
            var absolutePath = Path.GetFullPath(GameViewEvidencePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);
            Debug.Log("WORLD-H-EVIDENCE-1 Game View 저장 요청: "
                      + absolutePath);
        }

        private static void BuildCorridors(
            Transform root,
            H5Source source,
            H5AreaPlacement farm)
        {
            var material = EvidenceMaterial(CorridorMaterialPath,
                new Color(.20f, .24f, .22f, 1f));
            var byId = source.AreaPlacements.ToDictionary(
                value => value.AreaSetStableId, StringComparer.Ordinal);
            foreach (var corridor in source.Corridors.OrderBy(value =>
                         value.StableId, StringComparer.Ordinal))
            {
                var from = byId[corridor.FromAreaSetStableId];
                var to = byId[corridor.ToAreaSetStableId];
                var fromPosition = Position(from.XMeters - farm.XMeters,
                    from.ZMeters - farm.ZMeters) + Vector3.up * .6f;
                var toPosition = Position(to.XMeters - farm.XMeters,
                    to.ZMeters - farm.ZMeters) + Vector3.up * .6f;
                var direction = toPosition - fromPosition;
                var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
                value.name = corridor.StableId;
                value.transform.SetParent(root, false);
                value.transform.position = (fromPosition + toPosition) * .5f;
                value.transform.rotation = Quaternion.LookRotation(
                    direction.normalized, Vector3.up);
                value.transform.localScale = new Vector3(
                    1.2f, .12f, direction.magnitude);
                Object.DestroyImmediate(value.GetComponent<Collider>());
                value.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static void BuildTerrainUnderlay(
            AreaSet구성패턴검토Root marker,
            string areaRoleCode)
        {
            var material = EvidenceMaterial(
                EvidenceMaterialFolder + "/" + areaRoleCode + "바닥.mat",
                areaRoleCode switch
                {
                    "NatureHome" => new Color(.17f, .29f, .18f, 1f),
                    "Farm" => new Color(.31f, .25f, .14f, 1f),
                    "Town" => new Color(.29f, .28f, .27f, 1f),
                    _ => new Color(.22f, .25f, .28f, 1f),
                });
            var root = Child(marker.transform, "H3TerrainUnderlay");
            foreach (Transform boundary in marker.H3Boundaries)
            {
                if (!boundary.name.StartsWith("H3_", StringComparison.Ordinal))
                    continue;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground_" + boundary.name;
                ground.transform.SetParent(root, false);
                ground.transform.localPosition = boundary.localPosition
                                                 + Vector3.down * .08f;
                ground.transform.localRotation = boundary.localRotation;
                ground.transform.localScale = new Vector3(
                    boundary.localScale.x, .16f, boundary.localScale.z);
                ground.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static Material EvidenceMaterial(string path, Color color)
        {
            EnsureFolder(EvidenceMaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.color = color;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
                return material;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? throw new InvalidOperationException(
                             "HHierarchyEvidenceShaderMissing");
            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Replace('\\', '/').Trim('/').Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static void DisableRuntimeAuthorityComponents(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
        }

        private static void DisableEmbeddedReviewHelpers(Transform root)
        {
            foreach (var value in root.GetComponentsInChildren<Transform>(true))
            {
                if (value == root) continue;
                if (value.name == "RouteConnectors"
                    || value.name == "StateSockets"
                    || value.name.StartsWith("Connector_", StringComparison.Ordinal)
                    || value.name.StartsWith("Socket_", StringComparison.Ordinal))
                    value.gameObject.SetActive(false);
            }

            // H2 구성 Prefab은 실제 Synty 표현과 함께 검토용 바닥·경계·관계선·
            // 연결구 Renderer를 포함한다. GameObject 전체를 끄면 실제 건물도 함께
            // 사라질 수 있으므로 생성 검토 Material을 사용하는 Renderer만 분리한다.
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterials.Any(IsReviewMaterial))
                    renderer.enabled = false;
        }

        private static bool IsReviewMaterial(Material material)
        {
            if (material == null) return false;
            if (ReviewMaterialNames.Any(name => material.name.StartsWith(
                    name, StringComparison.Ordinal)))
                return true;
            var path = AssetDatabase.GetAssetPath(material);
            return ReviewMaterialPrefixes.Any(prefix =>
                path.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static Vector3 Position(double xMeters, double zMeters) =>
            new((float)xMeters * ScenarioMetersToUnityUnits, .15f,
                (float)zMeters * ScenarioMetersToUnityUnits);

        private static void RemoveOwnedRoot(Transform shell)
        {
            var previous = shell.Find(RootName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
        }

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static H5Source ReadH5()
        {
            var repository = FindRepositoryRoot();
            var path = Path.Combine(repository,
                H5RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "HHierarchyEvidenceH5SourceMissing");
            var output = JObject.Parse(File.ReadAllText(path));
            var layout = output["worldLayoutDefinition"] as JObject
                         ?? throw new InvalidOperationException(
                             "HHierarchyEvidenceH5LayoutMissing");
            var placements = RequiredArray(layout, "areaSetInstances")
                .Cast<JObject>()
                .Select(value =>
                {
                    var transform = RequiredObject(value, "placementTransform");
                    return new H5AreaPlacement(
                        RequiredString(value, "areaSetInstanceStableId"),
                        RequiredString(value, "areaRoleCode"),
                        (double)transform["localXMeters"],
                        (double)transform["localZMeters"],
                        (float)transform["rotationDegrees"]);
                }).ToArray();
            var corridors = RequiredArray(layout, "corridorInstances")
                .Cast<JObject>()
                .Select(value => new H5Corridor(
                    RequiredString(value, "corridorInstanceStableId"),
                    RequiredString(value, "fromAreaSetInstanceStableId"),
                    RequiredString(value, "toAreaSetInstanceStableId")))
                .ToArray();
            return new H5Source(
                RequiredString(layout, "worldLayoutStableId"),
                (int)layout["worldLayoutRevision"],
                RequiredString(layout, "worldLayoutHashSha256"),
                placements,
                corridors);
        }

        private static string FindRepositoryRoot()
        {
            var standard = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "..", "source", "repos", "Hongdal"));
            if (File.Exists(Path.Combine(standard,
                    H5RelativePath.Replace('/', Path.DirectorySeparatorChar))))
                return standard;
            var current = Directory.GetParent(UnityEngine.Application.dataPath);
            for (var index = 0; current != null && index < 6;
                 index++, current = current.Parent)
            {
                var sibling = Path.Combine(current.FullName, "Hongdal");
                if (File.Exists(Path.Combine(sibling,
                        H5RelativePath.Replace('/', Path.DirectorySeparatorChar))))
                    return sibling;
                if (File.Exists(Path.Combine(current.FullName,
                        H5RelativePath.Replace('/', Path.DirectorySeparatorChar))))
                    return current.FullName;
            }
            throw new InvalidOperationException(
                "HHierarchyEvidenceRepositoryRootMissing");
        }

        private static string RequiredString(JObject value, string property) =>
            (string)value[property] is { Length: > 0 } result
                ? result
                : throw new InvalidOperationException(
                    "HHierarchyEvidenceStringMissing:" + property);

        private static JArray RequiredArray(JObject value, string property) =>
            value[property] as JArray
            ?? throw new InvalidOperationException(
                "HHierarchyEvidenceArrayMissing:" + property);

        private static JObject RequiredObject(JObject value, string property) =>
            value[property] as JObject
            ?? throw new InvalidOperationException(
                "HHierarchyEvidenceObjectMissing:" + property);

        private sealed class H5Source
        {
            public H5Source(string stableId, int revision, string hash,
                H5AreaPlacement[] placements, H5Corridor[] corridors)
            {
                WorldLayoutStableId = stableId;
                WorldLayoutRevision = revision;
                WorldLayoutHash = hash;
                AreaPlacements = placements;
                Corridors = corridors;
            }

            public string WorldLayoutStableId { get; }
            public int WorldLayoutRevision { get; }
            public string WorldLayoutHash { get; }
            public H5AreaPlacement[] AreaPlacements { get; }
            public H5Corridor[] Corridors { get; }
        }

        private sealed class H5AreaPlacement
        {
            public H5AreaPlacement(string stableId, string roleCode,
                double xMeters, double zMeters, float rotationDegrees)
            {
                AreaSetStableId = stableId;
                AreaRoleCode = roleCode;
                XMeters = xMeters;
                ZMeters = zMeters;
                RotationDegrees = rotationDegrees;
            }

            public string AreaSetStableId { get; }
            public string AreaRoleCode { get; }
            public double XMeters { get; }
            public double ZMeters { get; }
            public float RotationDegrees { get; }
        }

        private sealed class H5Corridor
        {
            public H5Corridor(string stableId, string from, string to)
            {
                StableId = stableId;
                FromAreaSetStableId = from;
                ToAreaSetStableId = to;
            }

            public string StableId { get; }
            public string FromAreaSetStableId { get; }
            public string ToAreaSetStableId { get; }
        }
    }
}
