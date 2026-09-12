using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
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
    /// Hongdal의 역할 슬롯 기반 AreaSet 구성 계획을 공용 검토 Scene에 투영한다.
    /// 구조와 Synty 표현을 함께 검토하지만 선택·업무 상태 권위는 만들지 않는다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E5,
        "Unity의 AreaSet·Graph·배치·통행과 제작 조립 책임을 제공한다.",
        Boundary = "Builder와 Scene 조립 코드를 저장 Scene·실제 배치 증거와 구분한다.")]
    public static class AreaSet구성패턴검토실Builder
    {
        public const string ScenePath =
            "Assets/Ssalddel/Scenes/AreaSetCompositionPatternReview.unity";
        public const string RootName = "AreaSet구성패턴검토실_PresentationOnly";
        public const string OutputFolder =
            "Assets/Ssalddel/Generated/AreaSetCompositionReview";
        public const string MirroredPlanPath =
            OutputFolder + "/area-set-composition-plans.v1.json";
        public const string SourceReceiptPath =
            OutputFolder + "/source-receipt.json";

        private const string CompositionRelativePath =
            "eng/world-seedbeds/generated/area-set-composition-plans.v1.json";
        private const string TheoryRelativePath =
            "eng/world-seedbeds/generated/theory-spatial-factory.v1.json";
        private const string KnowledgeCatalogRelativePath =
            "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json";
        private const float AreaScale = .12f;

        [MenuItem("Ssalddel/검토실/AreaSet 구성 패턴/공용 구성 패턴 검토실 생성")]
        public static void BuildReviewScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "AreaSetCompositionReviewBuilderRequiresEditMode");

            var source = ReadSource();
            EnsureFolder(OutputFolder);
            MirrorSource(source);
            var grammar = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                              공간문법CompositionCatalogBuilder.CatalogPath)
                          ?? 공간문법CompositionCatalogBuilder.Build();
            grammar.Validate();

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName).transform;
            BuildGround(root);
            var patternRoot = Child(root, "AreaSetPatterns_8");
            var markers = new List<AreaSet구성패턴검토Root>();
            foreach (var pattern in RequiredArray(source.Composition, "resolvedPatterns")
                         .Cast<JObject>()
                         .OrderBy(value => RequiredString(value,
                             "compositionPatternStableId"), StringComparer.Ordinal))
                markers.Add(BuildPattern(patternRoot, pattern, source, grammar));

            var director = root.gameObject.AddComponent<AreaSet구성패턴검토Director>();
            director.Configure(markers.ToArray());
            BuildCamera(root);
            BuildLighting(root);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "AreaSetCompositionReviewSceneSaveFailed");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateOpenScene();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("AreaSetCompositionPatternReviewBuilt:Patterns=8;"
                      + "AreaSets=4;Modes=StructureAndSynty;" + ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            BuildReviewScene();
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Ssalddel/검토실/AreaSet 구성 패턴/열린 검토실 검증")]
        public static void ValidateOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException(
                    "AreaSetCompositionReviewSceneNotOpen:" + scene.path);
            var root = GameObject.Find(RootName)
                       ?? throw new InvalidOperationException(
                           "AreaSetCompositionReviewRootMissing");
            var director = root.GetComponent<AreaSet구성패턴검토Director>()
                           ?? throw new InvalidOperationException(
                               "AreaSetCompositionReviewDirectorMissing");
            if (director.PatternRoots.Length != 8
                || director.PatternRoots.Any(value => value == null || !value.Validate())
                || director.PatternRoots.Count(value => value.PatternKindCode == "Baseline") != 4
                || director.PatternRoots.Count(value => value.PatternKindCode == "Variant") != 4
                || director.PatternRoots.Select(value => value.AreaRoleCode)
                    .Distinct(StringComparer.Ordinal).Count() != 4
                || director.PatternRoots.Any(value =>
                    value.SyntyPresentation.childCount == 0))
                throw new InvalidOperationException(
                    "AreaSetCompositionReviewContractInvalid");
            Debug.Log("AreaSetCompositionPatternReviewValidationPassed:8/4/4");
        }

        private static AreaSet구성패턴검토Root BuildPattern(
            Transform parent,
            JObject pattern,
            SourceContext source,
            공간문법CompositionCatalog grammar)
        {
            var code = RequiredString(pattern, "patternCode");
            var root = Child(parent, code + "_" + RequiredString(pattern, "patternKindCode"));
            var h3Boundaries = Child(root, "H3Boundaries");
            var h2Boundaries = Child(root, "H2Boundaries");
            var connectors = Child(root, "Connectors");
            var wiRoutes = Child(root, "WiRoutes");
            var synty = Child(root, "SyntyPresentation");

            var h3Material = ReviewMaterial("H3경계", new Color(.10f, .48f, .70f, .36f));
            var h2Material = ReviewMaterial("H2경계", new Color(.96f, .58f, .12f, .48f));
            var connectorMaterial = ReviewMaterial("연결지점", new Color(.90f, .18f, .16f));
            var routeMaterial = ReviewMaterial("WI동선", new Color(.24f, .92f, .46f));
            var slotPositions = new Dictionary<string, Vector3>(StringComparer.Ordinal);

            foreach (var placement in RequiredArray(pattern, "resolvedH3Placements")
                         .Cast<JObject>())
            {
                var roleSlot = RequiredString(placement, "roleSlotCode");
                var h3Ref = RequiredString(placement, "selectedH3PatternRef");
                var h3 = source.H3ById[h3Ref];
                var scaleFactor = ScaleFactor(RequiredString(placement,
                    "scaleVariantCode"));
                var position = new Vector3(
                    (float?)placement["x"] * AreaScale ?? 0f,
                    0f,
                    (float?)placement["z"] * AreaScale ?? 0f);
                var rotation = Quaternion.Euler(0f,
                    (float?)placement["rotationDegrees"] ?? 0f, 0f);
                slotPositions[roleSlot] = position;

                var h3Bounds = RequiredObject(RequiredObject(h3,
                    "placementContract"), "referenceBoundsMeters");
                CreateBoundary(h3Boundaries,
                    "H3_" + roleSlot,
                    position,
                    rotation,
                    ((float?)h3Bounds["width"] ?? 840f) * AreaScale * scaleFactor,
                    ((float?)h3Bounds["depth"] ?? 840f) * AreaScale * scaleFactor,
                    .10f,
                    h3Material);
                CreateLabel(h3Boundaries, roleSlot + "\n" +
                    RequiredString(placement, "h3DisplayNameKo"),
                    position + Vector3.up * 2.2f, 4.4f, Color.white);

                foreach (var h2Node in RequiredArray(h3, "nodes").Cast<JObject>())
                {
                    var h2Ref = RequiredString(h2Node, "h2Ref");
                    var h2 = source.H2ById[h2Ref];
                    var local = new Vector3(
                        ((float?)h2Node["x"] ?? 0f) * AreaScale * scaleFactor,
                        0f,
                        ((float?)h2Node["z"] ?? 0f) * AreaScale * scaleFactor);
                    var world = position + rotation * local;
                    var h2Bounds = RequiredObject(RequiredObject(h2,
                        "placementContract"), "referenceBoundsMeters");
                    CreateBoundary(h2Boundaries,
                        "H2_" + h2Ref,
                        world,
                        rotation,
                        ((float?)h2Bounds["width"] ?? 160f) * AreaScale * scaleFactor,
                        ((float?)h2Bounds["depth"] ?? 160f) * AreaScale * scaleFactor,
                        .18f,
                        h2Material);
                    CreateLabel(h2Boundaries,
                        RequiredString(h2, "displayNameKo"),
                        world + Vector3.up * 1.1f, 2.5f,
                        new Color(1f, .78f, .32f));
                    BuildSyntyH2(synty, world, rotation, h2, source, grammar,
                        scaleFactor);
                }
            }

            foreach (var connection in RequiredArray(pattern, "resolvedConnections")
                         .Cast<JObject>())
            {
                var from = slotPositions[RequiredString(connection,
                    "fromRoleSlotCode")];
                var to = slotPositions[RequiredString(connection,
                    "toRoleSlotCode")];
                CreateLink(wiRoutes,
                    "WI_" + RequiredString(connection, "connectionStableId"),
                    from + Vector3.up * .8f,
                    to + Vector3.up * .8f,
                    1.4f,
                    routeMaterial);
                CreateConnector(connectors,
                    "Connector_From_" + RequiredString(connection,
                        "fromRoleSlotCode"), from, connectorMaterial);
                CreateConnector(connectors,
                    "Connector_To_" + RequiredString(connection,
                        "toRoleSlotCode"), to, connectorMaterial);
            }

            var marker = root.gameObject.AddComponent<AreaSet구성패턴검토Root>();
            marker.Configure(
                RequiredString(pattern, "compositionPatternStableId"),
                code,
                RequiredString(pattern, "title"),
                RequiredString(pattern, "areaRoleCode"),
                RequiredString(pattern, "patternKindCode"),
                RequiredString(source.Composition, "revision"),
                source.CompositionSha256,
                RequiredString(pattern, "compositionPatternHashSha256"),
                RequiredArray(pattern, "resolvedH3Placements").Cast<JObject>()
                    .Select(value => RequiredString(value, "roleSlotCode")).ToArray(),
                h3Boundaries, h2Boundaries, connectors, wiRoutes, synty);
            if (!marker.Validate())
                throw new InvalidOperationException(
                    "AreaSetCompositionReviewMarkerInvalid:" + code);
            return marker;
        }

        private static void BuildSyntyH2(
            Transform parent,
            Vector3 h2World,
            Quaternion h3Rotation,
            JObject h2,
            SourceContext source,
            공간문법CompositionCatalog grammar,
            float scaleFactor)
        {
            foreach (var h1Node in RequiredArray(h2, "nodes").Cast<JObject>())
            {
                var h1Ref = RequiredString(h1Node, "h1Ref");
                var definition = ReadDefinition(source, h1Ref);
                var entry = ResolveGrammarEntry(grammar, definition);
                var local = new Vector3(
                    ((float?)h1Node["x"] ?? 0f) * AreaScale * scaleFactor,
                    0f,
                    ((float?)h1Node["z"] ?? 0f) * AreaScale * scaleFactor);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    entry.Prefab, parent);
                instance.name = "Synty_" + h1Ref + "_" + entry.Descriptor.SetName;
                instance.transform.position = h2World + h3Rotation * local;
                instance.transform.rotation = h3Rotation;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false;
            }
        }

        private static 공간문법CompositionCatalogEntry ResolveGrammarEntry(
            공간문법CompositionCatalog catalog,
            JObject h1Definition)
        {
            foreach (var grammarToken in RequiredArray(h1Definition, "grammarSetRefs"))
            {
                var grammarRef = (string)grammarToken;
                var separator = grammarRef.IndexOf(':');
                if (separator <= 0 || separator >= grammarRef.Length - 1) continue;
                var pack = grammarRef[..separator];
                var setName = grammarRef[(separator + 1)..];
                var match = catalog.Entries
                    .Where(value => value.Prefab != null
                                    && string.Equals(value.Descriptor.PackCode, pack,
                                        StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(value.Descriptor.SetName, setName,
                                        StringComparison.Ordinal)
                                    && string.Equals(value.Descriptor.VariantCode, "A",
                                        StringComparison.Ordinal))
                    .OrderBy(value => value.Descriptor.Footprint.x
                                      * value.Descriptor.Footprint.y)
                    .ThenBy(value => value.CompositionKey, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (match != null) return match;
            }
            var packCodes = RequiredArray(h1Definition, "sourcePackCodes")
                .Select(value => (string)value).ToArray();
            var fallback = catalog.Entries
                .Where(value => value.Prefab != null
                                && packCodes.Contains(value.Descriptor.PackCode,
                                    StringComparer.OrdinalIgnoreCase)
                                && value.Descriptor.VariantCode == "A")
                .OrderBy(value => value.CompositionKey, StringComparer.Ordinal)
                .FirstOrDefault();
            return fallback ?? throw new InvalidOperationException(
                "AreaSetCompositionSyntyExpressionMissing:"
                + RequiredString(h1Definition, "stableId"));
        }

        private static JObject ReadDefinition(SourceContext source, string stableId)
        {
            var definitionRef = RequiredArray(source.KnowledgeCatalog,
                    "h1InteractionDefinitionRefs")
                .Cast<JObject>()
                .SingleOrDefault(value => string.Equals(
                    (string)value["stableId"], stableId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    "AreaSetCompositionH1DefinitionRefMissing:" + stableId);
            var relativePath = RequiredString(definitionRef, "definitionPath");
            var path = Path.Combine(source.KnowledgeCatalogRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "AreaSetCompositionH1DefinitionMissing:" + stableId);
            return JObject.Parse(File.ReadAllText(path));
        }

        private static SourceContext ReadSource()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "..", "source", "repos", "Hongdal"));
            var compositionPath = SourcePath(repositoryRoot, CompositionRelativePath);
            var theoryPath = SourcePath(repositoryRoot, TheoryRelativePath);
            var knowledgePath = SourcePath(repositoryRoot, KnowledgeCatalogRelativePath);
            if (!File.Exists(compositionPath) || !File.Exists(theoryPath)
                || !File.Exists(knowledgePath))
                throw new InvalidOperationException(
                    "AreaSetCompositionAuthoritativeSourceMissing");
            var composition = JObject.Parse(File.ReadAllText(compositionPath));
            if (RequiredString(composition, "schemaVersion")
                != "simulation-world-area-set-composition-plans.v1")
                throw new InvalidOperationException(
                    "AreaSetCompositionAuthoritativeSchemaInvalid");
            var theory = JObject.Parse(File.ReadAllText(theoryPath));
            var h2 = RequiredArray(theory, "h2Plans").Cast<JObject>()
                .ToDictionary(value => RequiredString(value, "h2StableId"),
                    StringComparer.Ordinal);
            var h3 = RequiredArray(theory, "h3Plans").Cast<JObject>()
                .ToDictionary(value => RequiredString(value, "h3StableId"),
                    StringComparer.Ordinal);
            return new SourceContext(repositoryRoot, compositionPath,
                composition, FileSha256(compositionPath), h2, h3,
                JObject.Parse(File.ReadAllText(knowledgePath)),
                Path.GetDirectoryName(knowledgePath));
        }

        private static void MirrorSource(SourceContext source)
        {
            var mirrorFullPath = FullAssetPath(MirroredPlanPath);
            Directory.CreateDirectory(Path.GetDirectoryName(mirrorFullPath));
            CopyIfChanged(source.CompositionPath, mirrorFullPath,
                source.CompositionSha256);
            var receipt = new JObject
            {
                ["schemaVersion"] = "area-set-composition-unity-source-receipt.v1",
                ["sourceProject"] = "Hongdal",
                ["sourceRelativePath"] = CompositionRelativePath,
                ["sourceRevision"] = RequiredString(source.Composition, "revision"),
                ["sourceSha256"] = source.CompositionSha256,
                ["presentationOnly"] = true,
                ["isOperationalState"] = false,
            };
            WriteIfChanged(FullAssetPath(SourceReceiptPath),
                receipt.ToString(Formatting.Indented) + Environment.NewLine);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void BuildGround(Transform root)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "검토실바닥";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(55f, 1f, 55f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial =
                ReviewMaterial("검토실바닥", new Color(.07f, .09f, .11f));
        }

        private static void BuildCamera(Transform root)
        {
            var cameraRoot = Child(root, "고정검토Camera");
            var camera = cameraRoot.gameObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 250f, -280f);
            camera.transform.rotation = Quaternion.LookRotation(
                Vector3.zero - camera.transform.position, Vector3.up);
            camera.fieldOfView = 48f;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 1600f;
            camera.backgroundColor = new Color(.025f, .035f, .05f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private static void BuildLighting(Transform root)
        {
            var lightRoot = Child(root, "검토조명");
            var key = lightRoot.gameObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.color = new Color(1f, .94f, .84f);
            lightRoot.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(.36f, .40f, .47f);
        }

        private static void CreateBoundary(
            Transform parent, string name, Vector3 position, Quaternion rotation,
            float width, float depth, float height, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.position = position + Vector3.up * (height * .5f);
            value.transform.rotation = rotation;
            value.transform.localScale = new Vector3(
                Mathf.Max(4f, width), height, Mathf.Max(4f, depth));
            Object.DestroyImmediate(value.GetComponent<Collider>());
            value.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateConnector(
            Transform parent, string name, Vector3 position, Material material)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = position + Vector3.up * 1.3f;
            marker.transform.localScale = Vector3.one * 3.2f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLink(
            Transform parent, string name, Vector3 from, Vector3 to,
            float width, Material material)
        {
            var direction = to - from;
            if (direction.sqrMagnitude < .01f) return;
            var link = GameObject.CreatePrimitive(PrimitiveType.Cube);
            link.name = name;
            link.transform.SetParent(parent, false);
            link.transform.position = (from + to) * .5f;
            link.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            link.transform.localScale = new Vector3(width, .3f, direction.magnitude);
            Object.DestroyImmediate(link.GetComponent<Collider>());
            link.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLabel(
            Transform parent, string text, Vector3 position,
            float size, Color color)
        {
            var label = new GameObject("Label_" + text.Replace('\n', '_'));
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 44;
            mesh.characterSize = size / 44f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
        }

        private static Material ReviewMaterial(string name, Color color)
        {
            EnsureFolder(OutputFolder + "/Materials");
            var path = OutputFolder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? throw new InvalidOperationException(
                             "AreaSetCompositionReviewShaderMissing");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static float ScaleFactor(string code) => code switch
        {
            "Compact" => .82f,
            "Expanded" => 1.18f,
            _ => 1f,
        };

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
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

        private static string SourcePath(string root, string relative) =>
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

        private static string FullAssetPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", assetPath));

        private static void CopyIfChanged(
            string source, string destination, string sourceHash)
        {
            if (File.Exists(destination)
                && new FileInfo(source).Length == new FileInfo(destination).Length
                && string.Equals(FileSha256(destination), sourceHash,
                    StringComparison.Ordinal)) return;
            File.Copy(source, destination, true);
        }

        private static void WriteIfChanged(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path), content,
                    StringComparison.Ordinal)) return;
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string FileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string RequiredString(JObject value, string property)
        {
            var result = (string)value[property];
            return !string.IsNullOrWhiteSpace(result)
                ? result
                : throw new InvalidOperationException(
                    "AreaSetCompositionReviewStringMissing:" + property);
        }

        private static JArray RequiredArray(JObject value, string property) =>
            value[property] as JArray
            ?? throw new InvalidOperationException(
                "AreaSetCompositionReviewArrayMissing:" + property);

        private static JObject RequiredObject(JObject value, string property) =>
            value[property] as JObject
            ?? throw new InvalidOperationException(
                "AreaSetCompositionReviewObjectMissing:" + property);

        private sealed class SourceContext
        {
            public SourceContext(
                string repositoryRoot,
                string compositionPath,
                JObject composition,
                string compositionSha256,
                Dictionary<string, JObject> h2ById,
                Dictionary<string, JObject> h3ById,
                JObject knowledgeCatalog,
                string knowledgeCatalogRoot)
            {
                RepositoryRoot = repositoryRoot;
                CompositionPath = compositionPath;
                Composition = composition;
                CompositionSha256 = compositionSha256;
                H2ById = h2ById;
                H3ById = h3ById;
                KnowledgeCatalog = knowledgeCatalog;
                KnowledgeCatalogRoot = knowledgeCatalogRoot;
            }

            public string RepositoryRoot { get; }
            public string CompositionPath { get; }
            public JObject Composition { get; }
            public string CompositionSha256 { get; }
            public Dictionary<string, JObject> H2ById { get; }
            public Dictionary<string, JObject> H3ById { get; }
            public JObject KnowledgeCatalog { get; }
            public string KnowledgeCatalogRoot { get; }
        }
    }
}
