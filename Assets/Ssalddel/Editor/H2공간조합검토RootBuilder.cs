using System;
using System.Collections.Generic;
using System.Globalization;
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
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static class H2공간조합검토RootBuilder
    {
        public const string OutputFolder =
            "Assets/Ssalddel/Generated/H2CompositionReview";
        public const string RootCatalogPath =
            OutputFolder + "/h2-unity-root-catalog.v1.json";

        private const string RecipeRelativePath =
            "eng/world-seedbeds/synty-bottom-up-inventory/h2-composition-recipes.v1.json";
        private const string KnowledgeCatalogRelativePath =
            "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json";

        [MenuItem("Ssalddel/Synty Web 검토/H2 상세 조립법 6종 Root 생성")]
        public static void BuildRoots()
        {
            WI공간모판AuthoringSource.RequireEditMode();
            var source = ReadSource();
            var grammarCatalog = AssetDatabase.LoadAssetAtPath<공간문법CompositionCatalog>(
                                     공간문법CompositionCatalogBuilder.CatalogPath)
                                 ?? throw new InvalidOperationException(
                                     "H2ReviewGrammarCatalogMissing:" +
                                     공간문법CompositionCatalogBuilder.CatalogPath);
            grammarCatalog.Validate();
            WI공간모판AuthoringSource.EnsureAssetFolder(OutputFolder);

            var items = new List<H2RootCatalogItem>();
            foreach (var recipe in source.Recipes.OrderBy(
                         value => (string?)value["targetKnowledgeRef"],
                         StringComparer.Ordinal))
            {
                var targetStableId = RequiredString(recipe, "targetKnowledgeRef");
                var recipeId = RequiredString(recipe, "recipeId");
                var h2Definition = ReadDefinition(source, targetStableId, "h2DefinitionRefs");
                var displayName = RequiredString(h2Definition, "title");
                var nodes = RequiredArray(recipe, "nodes");
                if (nodes.Count < 2)
                    throw new InvalidOperationException("H2ReviewRequiresTwoH1:" + targetStableId);

                var root = new GameObject(displayName + "_H2검토Root");
                try
                {
                    var childH1StableIds = nodes
                        .Select(value => RequiredString((JObject)value, "h1Ref"))
                        .ToArray();
                    root.AddComponent<H2공간조합검토Root>().Configure(
                        targetStableId,
                        recipeId,
                        source.RecipeRevision,
                        source.RecipeSha256,
                        childH1StableIds);

                    BuildRecipeGeometry(root.transform, recipe, nodes, source, grammarCatalog);
                    var fileName = targetStableId[(targetStableId.IndexOf(':') + 1)..]
                                   + ".prefab";
                    var prefabPath = OutputFolder + "/" + fileName;
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath)
                                 ?? throw new InvalidOperationException(
                                     "H2ReviewPrefabSaveFailed:" + targetStableId);
                    var marker = prefab.GetComponent<H2공간조합검토Root>();
                    if (marker == null || !marker.Validate())
                        throw new InvalidOperationException(
                            "H2ReviewPrefabMetadataInvalid:" + targetStableId);
                    items.Add(new H2RootCatalogItem
                    {
                        H2StableId = targetStableId,
                        DisplayName = displayName,
                        RecipeId = recipeId,
                        RecipeRevision = source.RecipeRevision,
                        RecipeSha256 = source.RecipeSha256,
                        PrefabPath = prefabPath,
                        ChildH1StableIds = childH1StableIds,
                    });
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            var catalog = new H2RootCatalog
            {
                SchemaVersion = "synty-h2-unity-root-catalog.v1",
                RecipeRevision = source.RecipeRevision,
                RecipeSha256 = source.RecipeSha256,
                Items = items.ToArray(),
                AuthorityBoundary = "LocationIndependentPresentationReviewOnly",
                PresentationOnly = true,
            };
            File.WriteAllText(
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", RootCatalogPath)),
                JsonUtility.ToJson(catalog, true) + Environment.NewLine,
                new UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log($"H2CompositionReviewRootsBuilt:{items.Count}:{OutputFolder}");
        }

        [MenuItem("Ssalddel/Synty Web 검토/H2 상세 조립법 6종 Root 생성·5시점 촬영")]
        public static void BuildAndCapture()
        {
            BuildRoots();
            EditorSceneManager.OpenScene(
                "Assets/Ssalddel/Scenes/WI공간모판검토실.unity",
                OpenSceneMode.Single);
            var catalogPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", RootCatalogPath));
            var catalog = JsonConvert.DeserializeObject<H2RootCatalog>(
                              File.ReadAllText(catalogPath))
                          ?? throw new InvalidOperationException("H2ReviewRootCatalogInvalid");
            var capturedAtUtc = DateTime.UtcNow;
            var collectionRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..", "artifacts", "local", "synty-web-review",
                "h2-composition-inventory",
                capturedAtUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(collectionRoot);
            var inventory = new H2LocalCaptureInventory
            {
                SchemaVersion = "synty-h2-local-capture-inventory.v1",
                CapturedAtUtc = capturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                RecipeRevision = catalog.RecipeRevision,
                RecipeSha256 = catalog.RecipeSha256,
                AuthorityBoundary = "LocalPresentationEvidenceOnly",
            };

            foreach (var item in catalog.Items.OrderBy(value => value.H2StableId, StringComparer.Ordinal))
            {
                var root = PrefabUtility.LoadPrefabContents(item.PrefabPath);
                try
                {
                    var token = Sha256(item.H2StableId)[..16];
                    var job = new SyntyH공간조립검토Job
                    {
                        BatchStableId = "review-batch:h2-composition." + token + ".r1",
                        BatchTitle = "H2 공간 조합 · " + item.DisplayName,
                        ReviewItemStableId = "review-item:h2-composition." + token + ".r1",
                        CompositionStableId = "composition:h2-composition." + token + ".r1",
                        DisplayName = item.DisplayName,
                        ReviewTargetLevelCode = "H2",
                        ReviewTargetStableId = item.H2StableId,
                        H1StableId = item.ChildH1StableIds[0],
                        H2StableId = item.H2StableId,
                        VariantCode = "A",
                        StateProfileCode = "DeterministicRecipePreview",
                    };
                    job.PlanHash =
                        Synty공간조립Web검토CapturePipeline.CreateHierarchyReviewPlanHash(job);
                    var outputFolder = Path.Combine(collectionRoot,
                        item.H2StableId[(item.H2StableId.IndexOf(':') + 1)..]);
                    var bundle = Synty공간조립Web검토CapturePipeline.CaptureHierarchySelection(
                        root, job, outputFolderOverride: outputFolder);
                    if (bundle.Captures.Count != 5)
                        throw new InvalidOperationException(
                            "H2ReviewCaptureCountInvalid:" + item.H2StableId);
                    inventory.Items.Add(new H2LocalCaptureItem
                    {
                        H2StableId = item.H2StableId,
                        DisplayName = item.DisplayName,
                        RecipeId = item.RecipeId,
                        PrefabPath = item.PrefabPath,
                        ChildH1StableIds = item.ChildH1StableIds,
                        RelativeFolder = Path.GetRelativePath(collectionRoot, bundle.OutputFolder)
                            .Replace('\\', '/'),
                        CaptureCount = bundle.Captures.Count,
                        SourceCompositionHash = bundle.SourceCompositionHash,
                        CaptureBundleHash = bundle.CaptureBundleHash,
                    });
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            File.WriteAllText(
                Path.Combine(collectionRoot, "h2-local-capture-inventory.json"),
                JsonUtility.ToJson(inventory, true) + Environment.NewLine,
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(collectionRoot, "README.md"),
                BuildReadme(inventory),
                new UTF8Encoding(false));
            Debug.Log($"H2CompositionReviewCaptured:{inventory.Items.Count}:"
                      + $"{inventory.Items.Sum(value => value.CaptureCount)}:{collectionRoot}");
            if (!UnityEngine.Application.isBatchMode)
                EditorUtility.RevealInFinder(collectionRoot);
        }

        public static void BuildAndCaptureFromCommandLine()
        {
            BuildAndCapture();
        }

        private static void BuildRecipeGeometry(
            Transform root,
            JObject recipe,
            JArray nodes,
            SourceContext source,
            공간문법CompositionCatalog grammarCatalog)
        {
            var baseMaterial = ReviewMaterial("H2기준바닥", new Color(.16f, .22f, .20f));
            var boundaryMaterial = ReviewMaterial("H1공간경계", new Color(.36f, .43f, .34f));
            var linkMaterial = ReviewMaterial("H2내부관계", new Color(.95f, .62f, .12f));
            var connectorMaterial = ReviewMaterial("H2외부연결구", new Color(.82f, .22f, .15f));
            var referenceSize = recipe["referenceSizeMeters"] as JObject
                                ?? throw new InvalidOperationException(
                                    "H2ReviewReferenceSizeMissing");
            CreateBlockBase(
                root,
                (float?)referenceSize["width"] ?? 0f,
                (float?)referenceSize["depth"] ?? 0f,
                baseMaterial);
            foreach (var edgeToken in RequiredArray(recipe, "edges"))
            {
                var edge = (JObject)edgeToken;
                var from = NodePosition(nodes, RequiredString(edge, "fromNodeId"));
                var to = NodePosition(nodes, RequiredString(edge, "toNodeId"));
                CreateLink(root, "내부관계_" + RequiredString(edge, "localEdgeId"),
                    from, to, 1.6f, linkMaterial);
            }

            foreach (var nodeToken in nodes)
            {
                var node = (JObject)nodeToken;
                var h1StableId = RequiredString(node, "h1Ref");
                var h1Definition = ReadDefinition(source, h1StableId, "h1InteractionDefinitionRefs");
                var entry = ResolveGrammarEntry(grammarCatalog, h1Definition);
                var nodeRoot = new GameObject("H1_" + h1StableId).transform;
                nodeRoot.SetParent(root, false);
                nodeRoot.localPosition = NodePosition(node);
                nodeRoot.localRotation = Quaternion.Euler(
                    0f, (float?)node["rotationDegrees"] ?? 0f, 0f);
                CreateBoundary(nodeRoot, entry.Descriptor.Footprint, boundaryMaterial);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, nodeRoot);
                instance.name = "표현_" + entry.Descriptor.SetName + "_A";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false;
            }

            foreach (var connectorToken in RequiredArray(recipe, "externalConnectors"))
            {
                var connector = (JObject)connectorToken;
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "외부연결구_" + RequiredString(connector, "connectorId");
                marker.transform.SetParent(root, false);
                marker.transform.localPosition = new Vector3(
                    (float?)connector["localX"] ?? 0f, .35f,
                    (float?)connector["localZ"] ?? 0f);
                marker.transform.localScale = new Vector3(2.2f, .35f, 2.2f);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().sharedMaterial = connectorMaterial;
            }
        }

        private static 공간문법CompositionCatalogEntry ResolveGrammarEntry(
            공간문법CompositionCatalog catalog,
            JObject h1Definition)
        {
            var candidates = new List<공간문법CompositionCatalogEntry>();
            foreach (var grammarToken in RequiredArray(h1Definition, "grammarSetRefs"))
            {
                var grammarRef = (string?)grammarToken ?? string.Empty;
                var separator = grammarRef.IndexOf(':');
                if (separator <= 0 || separator >= grammarRef.Length - 1) continue;
                var pack = grammarRef[..separator];
                var setName = grammarRef[(separator + 1)..];
                var match = catalog.Entries.FirstOrDefault(value =>
                    string.Equals(value.Descriptor.PackCode, pack, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(value.Descriptor.SetName, setName, StringComparison.Ordinal)
                    && string.Equals(value.Descriptor.VariantCode, "A", StringComparison.Ordinal));
                if (match != null) candidates.Add(match);
            }
            if (candidates.Count > 0)
                return candidates
                    .OrderBy(value => value.Descriptor.Footprint.x * value.Descriptor.Footprint.y)
                    .ThenBy(value => value.CompositionKey, StringComparer.Ordinal)
                    .First();
            throw new InvalidOperationException(
                "H2ReviewGrammarExpressionMissing:" + RequiredString(h1Definition, "stableId"));
        }

        private static void CreateBoundary(
            Transform parent,
            Vector2 footprint,
            Material material)
        {
            var boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boundary.name = "H1공간경계";
            boundary.transform.SetParent(parent, false);
            boundary.transform.localPosition = new Vector3(0f, -.16f, 0f);
            boundary.transform.localScale = new Vector3(
                Mathf.Max(10f, footprint.x), .25f, Mathf.Max(10f, footprint.y));
            Object.DestroyImmediate(boundary.GetComponent<Collider>());
            boundary.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateBlockBase(
            Transform parent,
            float width,
            float depth,
            Material material)
        {
            if (width <= 0f || depth <= 0f)
                throw new InvalidOperationException("H2ReviewReferenceSizeInvalid");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "H2기준크기바닥";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, -.34f, 0f);
            ground.transform.localScale = new Vector3(width, .12f, depth);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLink(
            Transform parent,
            string name,
            Vector3 from,
            Vector3 to,
            float width,
            Material material)
        {
            var direction = to - from;
            var link = GameObject.CreatePrimitive(PrimitiveType.Cube);
            link.name = name;
            link.transform.SetParent(parent, false);
            link.transform.localPosition = (from + to) * .5f + Vector3.up * .08f;
            link.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            link.transform.localScale = new Vector3(width, .16f, direction.magnitude);
            Object.DestroyImmediate(link.GetComponent<Collider>());
            link.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material ReviewMaterial(string name, Color color)
        {
            var path = OutputFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? throw new InvalidOperationException("H2ReviewMaterialShaderMissing");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Vector3 NodePosition(JArray nodes, string localNodeId)
        {
            var node = nodes.Cast<JObject>().SingleOrDefault(value =>
                           string.Equals((string?)value["localNodeId"], localNodeId,
                               StringComparison.Ordinal))
                       ?? throw new InvalidOperationException(
                           "H2ReviewNodeMissing:" + localNodeId);
            return NodePosition(node);
        }

        private static Vector3 NodePosition(JObject node) => new(
            (float?)node["localX"] ?? 0f,
            0f,
            (float?)node["localZ"] ?? 0f);

        private static SourceContext ReadSource()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "..", "source", "repos", "Hongdal"));
            var recipePath = Path.Combine(repositoryRoot,
                RecipeRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var catalogPath = Path.Combine(repositoryRoot,
                KnowledgeCatalogRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(recipePath) || !File.Exists(catalogPath))
                throw new InvalidOperationException("H2ReviewAuthoritativeSourceMissing");
            var recipeRoot = JObject.Parse(File.ReadAllText(recipePath));
            var catalogRoot = JObject.Parse(File.ReadAllText(catalogPath));
            return new SourceContext(
                repositoryRoot,
                Path.GetDirectoryName(catalogPath)!,
                catalogRoot,
                RequiredArray(recipeRoot, "recipes").Cast<JObject>().ToArray(),
                RequiredString(recipeRoot, "revision"),
                FileSha256(recipePath));
        }

        private static JObject ReadDefinition(
            SourceContext source,
            string stableId,
            string refsProperty)
        {
            var definitionRef = RequiredArray(source.Catalog, refsProperty)
                .Cast<JObject>()
                .SingleOrDefault(value => string.Equals(
                    (string?)value["stableId"], stableId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    "H2ReviewDefinitionRefMissing:" + stableId);
            var relativePath = RequiredString(definitionRef, "definitionPath");
            var path = Path.Combine(source.CatalogRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            var expectedHash = RequiredString(definitionRef, "definitionSha256");
            if (!string.Equals(FileSha256(path), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("H2ReviewDefinitionHashMismatch:" + stableId);
            return JObject.Parse(File.ReadAllText(path));
        }

        private static JArray RequiredArray(JObject value, string property) =>
            value[property] as JArray
            ?? throw new InvalidOperationException("H2ReviewArrayMissing:" + property);

        private static string RequiredString(JObject value, string property)
        {
            var result = (string?)value[property];
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException("H2ReviewValueMissing:" + property);
            return result;
        }

        private static string FileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(stream)
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string Sha256(string value)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string BuildReadme(H2LocalCaptureInventory inventory)
        {
            var lines = new List<string>
            {
                "# H2 공간 조합 로컬 촬영",
                string.Empty,
                "- 조립법 개정: `" + inventory.RecipeRevision + "`",
                "- 조립법 SHA-256: `" + inventory.RecipeSha256 + "`",
                "- 권위: `LocalPresentationEvidenceOnly`",
                "- 구성: 상세 조립법 H2 6개 × 표준 5시점",
                string.Empty,
                "| H2 | 하위 H1 | PNG | 폴더 |",
                "| --- | ---: | ---: | --- |",
            };
            lines.AddRange(inventory.Items.Select(value =>
                $"| `{value.H2StableId}` {value.DisplayName} | {value.ChildH1StableIds.Length} | "
                + $"{value.CaptureCount} | `{value.RelativeFolder}` |"));
            lines.Add(string.Empty);
            lines.Add("이 촬영은 위치 독립 H2 표현 검토 근거이며 공식 H2 승인, AreaSet 배치, WI E단계 또는 공공데이터 근거가 아니다.");
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private sealed class SourceContext
        {
            public SourceContext(
                string repositoryRoot,
                string catalogRoot,
                JObject catalog,
                JObject[] recipes,
                string recipeRevision,
                string recipeSha256)
            {
                RepositoryRoot = repositoryRoot;
                CatalogRoot = catalogRoot;
                Catalog = catalog;
                Recipes = recipes;
                RecipeRevision = recipeRevision;
                RecipeSha256 = recipeSha256;
            }

            public string RepositoryRoot { get; }
            public string CatalogRoot { get; }
            public JObject Catalog { get; }
            public JObject[] Recipes { get; }
            public string RecipeRevision { get; }
            public string RecipeSha256 { get; }
        }

        [Serializable]
        private sealed class H2RootCatalog
        {
            public string SchemaVersion = string.Empty;
            public string RecipeRevision = string.Empty;
            public string RecipeSha256 = string.Empty;
            public H2RootCatalogItem[] Items = Array.Empty<H2RootCatalogItem>();
            public string AuthorityBoundary = string.Empty;
            public bool PresentationOnly;
        }

        [Serializable]
        private sealed class H2RootCatalogItem
        {
            public string H2StableId = string.Empty;
            public string DisplayName = string.Empty;
            public string RecipeId = string.Empty;
            public string RecipeRevision = string.Empty;
            public string RecipeSha256 = string.Empty;
            public string PrefabPath = string.Empty;
            public string[] ChildH1StableIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class H2LocalCaptureInventory
        {
            public string SchemaVersion = string.Empty;
            public string CapturedAtUtc = string.Empty;
            public string RecipeRevision = string.Empty;
            public string RecipeSha256 = string.Empty;
            public string AuthorityBoundary = string.Empty;
            public List<H2LocalCaptureItem> Items = new();
        }

        [Serializable]
        private sealed class H2LocalCaptureItem
        {
            public string H2StableId = string.Empty;
            public string DisplayName = string.Empty;
            public string RecipeId = string.Empty;
            public string PrefabPath = string.Empty;
            public string[] ChildH1StableIds = Array.Empty<string>();
            public string RelativeFolder = string.Empty;
            public int CaptureCount;
            public string SourceCompositionHash = string.Empty;
            public string CaptureBundleHash = string.Empty;
        }
    }
}
