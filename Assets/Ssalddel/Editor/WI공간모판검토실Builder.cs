using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static class WI공간모판검토실Builder
    {
        public const string MirrorRoot =
            "Assets/Ssalddel/Data/WorldSeedbeds/wi-spatial-seedbeds";
        public const string VisualCatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/WI공간모판VisualCatalog.asset";
        public const string ScenePath = "Assets/Ssalddel/Scenes/WI공간모판검토실.unity";
        public const string EvidenceFolder =
            "Assets/Documentation/Changes/2026-08-17-wi-spatial-seedbed-visual-review";

        private const string MaterialFolder =
            "Assets/Ssalddel/Presentation/World/Materials/WI공간모판검토";
        private static readonly Color[] SeedbedColors =
        {
            new(.24f, .56f, .24f),
            new(.56f, .43f, .16f),
            new(.78f, .38f, .12f),
            new(.18f, .48f, .64f),
            new(.42f, .35f, .65f),
        };

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/00 전체 생성 및 열기")]
        public static void BuildAll()
        {
            RequireEditMode();
            SyncAuthoritativeJson();
            var catalog = BuildVisualCatalog();
            BuildScene(catalog);
            ValidateScene();
            Debug.Log("E4 증거 · H1 WI 공간 모판 검토실 생성 완료: 5개 모판 / 9개 공간 / 27개 고유 후보");
        }

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/00 빠른 원본·Catalog 새로고침")]
        public static void RefreshSourceAndCatalog()
        {
            RequireEditMode();
            var sourceChanged = WI공간모판SourceSynchronizer.Sync(MirrorRoot);
            var catalogResult = WI공간모판CatalogBuilder.Build(VisualCatalogPath);
            Debug.Log($"E4 증거 · H1 빠른 새로고침 완료: 원본 변경={sourceChanged}, Catalog 변경={catalogResult.Changed}");
        }

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/01 원본 JSON 동기화")]
        public static void SyncAuthoritativeJson()
        {
            RequireEditMode();
            WI공간모판SourceSynchronizer.Sync(MirrorRoot);
        }

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/02 Visual Catalog 생성")]
        public static WI공간모판VisualCatalog BuildVisualCatalog()
        {
            RequireEditMode();
            return WI공간모판CatalogBuilder.Build(VisualCatalogPath).Catalog;
        }

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/03 검토실 Scene 생성")]
        public static void BuildSceneFromCatalog()
        {
            RequireEditMode();
            var catalog = Required<WI공간모판VisualCatalog>(VisualCatalogPath);
            BuildScene(catalog);
        }

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/04 열린 Scene 검증")]
        public static void ValidateScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var presenter = Object.FindAnyObjectByType<WI공간모판검토Presenter>()
                ?? throw new InvalidOperationException("WiSpatialSeedbedReviewPresenterMissing");
            presenter.ValidateWiring();
            presenter.Catalog.Validate();
            if (Object.FindObjectsByType<WI공간모판OverviewItem>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 9)
                throw new InvalidOperationException("WiSpatialSeedbedReviewOverviewCountInvalid");
            if (Object.FindFirstObjectByType<InputSystemUIInputModule>() == null
                || Object.FindFirstObjectByType<StandaloneInputModule>() != null)
                throw new InvalidOperationException("WiSpatialSeedbedReviewInputModuleInvalid");
            if (scene.GetRootGameObjects().All(value => value.name != "WI공간모판검토실"))
                throw new InvalidOperationException("WiSpatialSeedbedReviewRootMissing");
            Debug.Log("E4 증거 · H1 WI 공간 모판 검토실 검증 통과");
        }

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/전체 개요")]
        public static void ShowOverview() => WithPresenter(value => value.ShowOverview());

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/01 농장 생산 대표")]
        public static void ShowFarmProduction() => ShowSeedbed("wi-spatial-seedbed:farm-production.v1");

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/02 농장 작업마당 대표")]
        public static void ShowFarmWorkYard() => ShowSeedbed("wi-spatial-seedbed:farm-work-yard.v1");

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/03 농장 상차 Gate 대표")]
        public static void ShowFarmLoadingGate() => ShowSeedbed("wi-spatial-seedbed:farm-loading-gate.v1");

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/04 Farm Hub 회랑 대표")]
        public static void ShowFarmHubCorridor() => ShowSeedbed("wi-spatial-seedbed:farm-hub-corridor.v1");

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/05 Hub 입고 보관 대표")]
        public static void ShowHubReceivingStorage() => ShowSeedbed("wi-spatial-seedbed:hub-receiving-storage.v1");

        [MenuItem("Ssalddel/E4 · H1 WI 공간 모판/보기/선택 모판 후보 비교")]
        public static void ShowCandidateSheet() => WithPresenter(value => value.ShowSelectedCandidateSheet());

        private static void BuildScene(WI공간모판VisualCatalog catalog)
        {
            catalog.Validate();
            EnsureAssetFolder(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));
            EnsureAssetFolder(MaterialFolder);
            EnsureAssetFolder(EvidenceFolder);
            var groundMaterial = Material("모판바닥", new Color(.16f, .19f, .16f, 1f));
            var connectorMaterial = Material("연결구", new Color(.16f, .72f, .92f, 1f));
            var boundaryMaterial = Material("경계", new Color(.92f, .78f, .22f, 1f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WI공간모판검토실";
            var root = new GameObject("WI공간모판검토실").transform;
            var visualRoot = Child(root, "VisualRoot_PresentationOnly");
            var overviewRoot = Child(visualRoot, "전체개요_5모판_9공간");
            var detailRoot = Child(visualRoot, "선택상세");
            detailRoot.position = new Vector3(0f, 0f, -130f);
            var detailHost = Child(detailRoot, "선택후보Host");
            BuildSizeBoundary(detailRoot, catalog.Entries[0], groundMaterial, boundaryMaterial);
            var candidateSheetRoot = Child(visualRoot, "후보비교표");
            candidateSheetRoot.position = new Vector3(0f, 0f, -280f);
            var candidateSheetHost = Child(candidateSheetRoot, "후보PrefabHost");

            BuildOverview(catalog, overviewRoot, groundMaterial, connectorMaterial);
            var cameraRig = BuildCamera(root, overviewRoot, detailRoot, candidateSheetRoot);
            BuildLighting(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .transform.SetParent(root, false);

            var presenterObject = new GameObject("WI공간모판검토Presenter");
            presenterObject.transform.SetParent(root, false);
            var presenter = presenterObject.AddComponent<WI공간모판검토Presenter>();
            var ui = WI공간모판UiFactory.Build(root, cameraRig.GetComponent<Camera>(),
                SeedbedColors);
            // Material asset creation can refresh the AssetDatabase. Re-resolve the
            // catalog so the serialized Scene reference always points at the live asset.
            catalog = Required<WI공간모판VisualCatalog>(VisualCatalogPath);
            presenter.Configure(catalog, overviewRoot, detailRoot, candidateSheetRoot,
                detailHost, candidateSheetHost, cameraRig,
                ui.ModeText, ui.TitleText, ui.SummaryText, ui.DetailText,
                ui.LineageText, ui.BoundaryText, ui.OverviewButton, ui.SheetButton,
                ui.SeedbedButtons, ui.SpaceButtons, ui.CandidateButtons);
            presenter.Initialize();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
        }

        private static void BuildOverview(
            WI공간모판VisualCatalog catalog,
            Transform parent,
            Material ground,
            Material connector)
        {
            var gridPositions = new[]
            {
                new Vector3(-25f, 0f, 30f),
                new Vector3(25f, 0f, 30f),
                new Vector3(75f, 0f, 30f),
                new Vector3(75f, 0f, 0f),
                new Vector3(25f, 0f, 0f),
                new Vector3(-25f, 0f, 0f),
                new Vector3(-25f, 0f, -30f),
                new Vector3(25f, 0f, -30f),
                new Vector3(75f, 0f, -30f),
            };
            var centers = new List<Vector3>();
            var globalSpaceIndex = 0;
            for (var seedbedIndex = 0; seedbedIndex < catalog.Entries.Count; seedbedIndex++)
            {
                var entry = catalog.Entries[seedbedIndex];
                var group = Child(parent, $"모판{seedbedIndex + 1:00}_{entry.Title}");
                for (var spaceIndex = 0; spaceIndex < entry.Spaces.Count; spaceIndex++)
                {
                    var space = entry.Spaces[spaceIndex];
                    var candidate = space.Candidates[0];
                    var width = Mathf.Max(18f, candidate.NativeFootprintMeters.x);
                    var depth = Mathf.Max(16f, candidate.NativeFootprintMeters.y);
                    var center = gridPositions[globalSpaceIndex++];
                    centers.Add(center);
                    var item = Child(group, $"공간_{space.SpaceCode}");
                    item.position = center;
                    item.gameObject.AddComponent<WI공간모판OverviewItem>()
                        .Configure(entry.StableId, space.SpaceCode, candidate.CompositionKey);
                    CreateBox(item, "공간경계", new Vector3(width, .18f, depth),
                        new Vector3(0f, -.18f, 0f), Material(
                            $"모판색_{seedbedIndex + 1:00}", SeedbedColors[seedbedIndex] * .72f));
                    var prefab = (GameObject)PrefabUtility.InstantiatePrefab(candidate.Prefab, item);
                    prefab.name = "대표후보_" + candidate.CompositionKey;
                    prefab.transform.localPosition = Vector3.zero;
                    prefab.transform.localRotation = Quaternion.identity;
                    PreparePresentationInstance(prefab);
                    WorldLabel(item, $"{seedbedIndex + 1}. {entry.Title.Replace(" 공간 모판", string.Empty)}\n"
                        + $"{space.SpaceCode}\n{candidate.CompositionKey}",
                        new Vector3(0f, .18f, depth * .5f + 2f), 44, Color.white);
                }
            }

            for (var index = 1; index < centers.Count; index++)
                Connector(parent, centers[index - 1], centers[index], connector, index);
            WorldLabel(parent, "증거 E4 · H1 재사용 공간 모판 — 실제 H2 Block/H3 경관 배치 아님",
                new Vector3(25f, .2f, 55f), 62,
                new Color(1f, .92f, .48f));
        }

        private static void BuildSizeBoundary(
            Transform parent,
            WI공간모판VisualEntry entry,
            Material ground,
            Material boundary)
        {
            CreateBox(parent, "선호크기바닥", new Vector3(entry.PreferredSizeMeters.x, .2f,
                    entry.PreferredSizeMeters.y), new Vector3(0f, -.2f, 0f), ground);
            RectangleOutline(parent, "최대허용경계", entry.MaximumSizeMeters, boundary, .22f);
            WorldLabel(parent, "선택 후보 · 원형 크기 유지", new Vector3(0f, .2f,
                entry.MaximumSizeMeters.y * .5f + 3f), 52, new Color(1f, .92f, .48f));
        }

        private static DioramaTopDownCameraRig BuildCamera(
            Transform parent,
            Transform overviewRoot,
            Transform detailRoot,
            Transform sheetRoot)
        {
            var anchors = Child(parent, "CameraAnchors");
            var overview = Child(anchors, "전체개요Focus");
            overview.position = new Vector3(25f, 2f, 0f);
            var detail = Child(anchors, "선택상세Focus");
            detail.position = detailRoot.position + new Vector3(0f, 2f, 0f);
            var sheet = Child(anchors, "후보비교Focus");
            sheet.position = sheetRoot.position + new Vector3(45f, 2f, -28f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 800f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .15f, .17f);
            var rig = cameraObject.AddComponent<DioramaTopDownCameraRig>();
            rig.Configure(camera, new[]
            {
                Binding(WI공간모판검토Presenter.OverviewAnchorId,
                    DioramaCameraFocusLevelCodes.World, overview),
                Binding(WI공간모판검토Presenter.DetailAnchorId,
                    DioramaCameraFocusLevelCodes.Object, detail),
                Binding(WI공간모판검토Presenter.SheetAnchorId,
                    DioramaCameraFocusLevelCodes.Zone, sheet),
            }, WI공간모판검토Presenter.OverviewAnchorId);
            rig.ConfigureInteractionLimits(35f, 75f, 10f, 240f);
            rig.ConfigureComposition(50f, 132f, 92f, 48f, 35f, 32f, 29f, 240f);
            rig.Initialize();
            return rig;
        }

        private static void BuildLighting(Transform parent)
        {
            var lightObject = new GameObject("Key Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(46f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.52f, .60f, .62f);
            RenderSettings.ambientEquatorColor = new Color(.27f, .32f, .31f);
            RenderSettings.ambientGroundColor = new Color(.11f, .13f, .12f);
        }

        private static void WithPresenter(Action<WI공간모판검토Presenter> action)
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var presenter = Object.FindFirstObjectByType<WI공간모판검토Presenter>()
                ?? throw new InvalidOperationException("WiSpatialSeedbedReviewPresenterMissing");
            presenter.Initialize();
            action(presenter);
            SceneView.RepaintAll();
        }

        private static void ShowSeedbed(string stableId) =>
            WithPresenter(value => value.ShowSeedbed(stableId));

        private static Transform Child(Transform parent, string name)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            return item;
        }

        private static void Connector(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            int index)
        {
            var direction = to - from;
            var center = (from + to) * .5f + Vector3.up * .12f;
            var line = CreateBox(parent, $"E4연결구_{index:00}",
                new Vector3(.85f, .12f, direction.magnitude), center, material);
            line.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static void RectangleOutline(
            Transform parent,
            string name,
            Vector2 size,
            Material material,
            float thickness)
        {
            var root = Child(parent, name);
            CreateBox(root, "North", new Vector3(size.x, .12f, thickness),
                new Vector3(0f, .02f, size.y * .5f), material);
            CreateBox(root, "South", new Vector3(size.x, .12f, thickness),
                new Vector3(0f, .02f, -size.y * .5f), material);
            CreateBox(root, "East", new Vector3(thickness, .12f, size.y),
                new Vector3(size.x * .5f, .02f, 0f), material);
            CreateBox(root, "West", new Vector3(thickness, .12f, size.y),
                new Vector3(-size.x * .5f, .02f, 0f), material);
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 scale,
            Vector3 localPosition,
            Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = scale;
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static void WorldLabel(
            Transform parent,
            string value,
            Vector3 localPosition,
            int fontSize,
            Color color)
        {
            var item = new GameObject("표지_" + value.Split('\n')[0]);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var label = item.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.characterSize = .12f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            label.text = value;
        }

        private static DioramaCameraFocusBinding Binding(
            string id,
            string level,
            Transform anchor) => new()
        {
            AnchorId = id,
            LevelCode = level,
            Anchor = anchor,
        };

        private static Material Material(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("WiSpatialSeedbedMaterialShaderMissing");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void PreparePresentationInstance(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
        }

        private static T Required<T>(string path) where T : Object =>
            WI공간모판AuthoringSource.Required<T>(path);

        private static void EnsureAssetFolder(string path) =>
            WI공간모판AuthoringSource.EnsureAssetFolder(path);

        private static void RequireEditMode() =>
            WI공간모판AuthoringSource.RequireEditMode();

    }
}
