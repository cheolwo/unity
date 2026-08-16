using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ssalddel.Unity.Editor
{
    public static class 대한민국법정동WorldBuilder
    {
        public const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";
        public const string CatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창군법정동경관VisualCatalog.asset";
        public const string RoleCharacterCatalogPath =
            "Assets/Ssalddel/Presentation/World/Catalogs/평창군역할CharacterVisualCatalog.asset";
        private const string GeneratedRoot =
            "Assets/Ssalddel/Presentation/World/Generated/평창군법정동World";
        private const string VolumeProfilePath =
            GeneratedRoot + "/대관령농촌경관VolumeProfile.asset";
        private const string SkyboxMaterialPath =
            GeneratedRoot + "/대관령맑은낮Skybox.mat";
        private const string Farm = "Assets/Synty/PolygonFarm/Prefabs/";
        private const string Town = "Assets/Synty/PolygonTown/Prefabs/";
        private const string City = "Assets/Synty/PolygonCity/Prefabs/";
        private const string Nature = "Assets/Synty/PolygonNature/Prefabs/";
        private const string Generic = "Assets/Synty/PolygonGeneric/Prefabs/";
        private const string Starter = "Assets/Synty/PolygonStarter/Prefabs/";
        private const string EvidenceRoot =
            "Assets/Documentation/Changes/2026-08-13-daegwallyeong-farm-1km-completion";

        [MenuItem("Ssalddel/WORLD-LEGAL-2 평창군 Synty 경관 배치 _F7")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var worldMap = GameObject.Find("WorldMapRoot")
                ?? throw new InvalidOperationException("LegalDongWorldMapRootMissing");
            var old = worldMap.transform.Find("OfficialRegionProjectionRoot");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            SetLegacyLayerActive(worldMap.transform, "TerrainRoot", false);
            SetLegacyLayerActive(worldMap.transform, "TerritoryRoot", false);
            SetLegacyLayerActive(worldMap.transform, "RegionMarkers", false);
            SetLegacyLayerActive(worldMap.transform, "RouteRoot", false);
            SetLegacyLayerActive(worldMap.transform, "SettlementMarkers", false);

            var projection = 평창군법정동WorldFixture.Create();
            var scenicPlan = 평창군경관Fixture.Create();
            var spatialRecipe = 평창군공간PipelineFixture.CreateRecipe();
            var compositionProfile = 평창군공간PipelineFixture.CreateCompositionProfile();
            var spatialManifest = 평창군공간PipelineFixture.CreateManifest();
            var renderingProfile = 평창군경관RenderingFixture.Create();
            var playerProfile = 평창군플레이어경관Fixture.Create();
            var fourPackProfile = 평창4Pack경관Profile.CreateDefault();
            fourPackProfile.Validate();
            자연경관CompositionSetBuilder.Build();
            자연경관EventOverlayBuilder.Build();
            var catalog = EnsureCatalog();
            var roleCharacterCatalog = EnsureRoleCharacterCatalog();
            var root = new GameObject("OfficialRegionProjectionRoot");
            root.transform.SetParent(worldMap.transform, false);
            var view = root.AddComponent<법정동WorldProjectionView>();
            view.Configure(projection);

            var pipelineRoot = Child(root.transform, "SpatialPipeline_EPSG5186_TileAreaSet");
            var fourPackProfileView = pipelineRoot.gameObject
                .AddComponent<평창4Pack경관ProfileView>();
            fourPackProfileView.Configure(fourPackProfile);
            if (!fourPackProfileView.ValidateWiring())
                throw new InvalidOperationException("PyeongchangFourPackProfileWiringInvalid");
            var tileLayerRoot = Child(pipelineRoot, "Tiles_L0_8000m_L1_2000m_L2_500m");
            var areaRoot = Child(pipelineRoot, "Areas_Farm_Hub_Town");
            var areaSetRoot = Child(pipelineRoot, "AreaSet_pyeongchang-farm-hub-town-v1");
            BuildSpatialUnitHierarchy(tileLayerRoot, areaRoot, areaSetRoot, spatialManifest);
            var activeCamera = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (activeCamera != null)
            {
                var tileLevels = Enumerable.Range(0, 3)
                    .Select(index => tileLayerRoot.GetChild(index)).ToArray();
                tileLayerRoot.gameObject.AddComponent<공간TileLodLoader>().Configure(
                    activeCamera, tileLevels[0], tileLevels[1], tileLevels[2]);
            }
            var terrainLayer = Child(pipelineRoot,
                "L2_연속지형_ScenarioTerrainPreview_DEM교체대기");
            var boundaryLayer = Child(pipelineRoot, "L1_법정동경계_OfficialBoundaryPolygon");
            var contourLayer = Child(pipelineRoot, "L8_등고표현_PhysicalDEM_아직미배치");
            var scenicLayer = Child(pipelineRoot, "L4_L7_Synty경관_PresentationOnly");
            var completionAreaRoot = Child(scenicLayer,
                "CompletionArea_대관령면Farm_1km_L2_2x2");
            var referenceTileRoot = Child(completionAreaRoot,
                "ReferenceTile_대관령면_L2_500m_HandAuthored");
            var routeEvidenceLayer = Child(pipelineRoot, "Debug_전체개략연결망_SimulationRoute");
            var corridorLayer = Child(pipelineRoot, "L5_FarmHubTown이동회랑_SimulationRoute");
            var debugLayer = Child(pipelineRoot, "Debug_대표점과근거");
            var anchors = new Dictionary<string, Transform>(StringComparer.Ordinal);

            BuildContinuousTerrain(terrainLayer);
            var regionEvidenceMeshes = Child(terrainLayer, "RegionCellEvidenceMeshes");
            foreach (var node in projection.Nodes)
            {
                BuildRegionTerrain(regionEvidenceMeshes, node);
                BuildBoundary(boundaryLayer, node);
                anchors[node.RegionStableId] = BuildDebugAnchor(debugLayer, node);
            }
            regionEvidenceMeshes.gameObject.SetActive(false);
            foreach (var route in projection.Routes)
                BuildRoute(routeEvidenceLayer, route,
                    anchors[route.FromRegionStableId].position,
                    anchors[route.ToRegionStableId].position);
            var finalVisualBindings = new 법정동Synty후단연결기().연결계획(
                scenicPlan,
                catalog,
                placement => PhysicalSlopeDegrees(placement.LocalX, placement.LocalZ));
            var rejectedFinalVisuals = finalVisualBindings.Count(item => !item.연결가능여부);
            if (rejectedFinalVisuals > 0)
                throw new InvalidOperationException(
                    "LegalDongScenicFinalPipelineBindingRejected:" + rejectedFinalVisuals);
            BuildFarmCompletionAreaScaffold(completionAreaRoot);
            BuildStaticSceneryAreaAnchors(scenicLayer);
            BuildStaticSceneryCorridorAnchors(corridorLayer);
            BuildTitle(debugLayer, projection, scenicPlan);
            BuildSimulationVan(pipelineRoot, catalog);
            BuildEvidenceCameras(pipelineRoot);
            BuildRoleNpcGroup(pipelineRoot, roleCharacterCatalog);
            BuildPlayerExplorer(pipelineRoot, playerProfile, roleCharacterCatalog);
            Build대관령L2창고일인칭상호작용(
                pipelineRoot, completionAreaRoot, catalog);
            BuildGraphicsQualityPipeline(pipelineRoot, renderingProfile);
            var pipelineView = pipelineRoot.gameObject.AddComponent<공간WorldPipelineView>();
            pipelineView.Configure(spatialRecipe, compositionProfile, spatialManifest,
                tileLayerRoot, areaRoot, areaSetRoot, referenceTileRoot,
                catalog.CatalogRevision, finalVisualBindings.Count, rejectedFinalVisuals);
            if (!pipelineView.ValidateWiring())
                throw new InvalidOperationException("SpatialWorldPipelineWiringInvalid");
            view.ConfigureLayers(scenicPlan, boundaryLayer, contourLayer, scenicLayer);
            debugLayer.gameObject.SetActive(false);
            routeEvidenceLayer.gameObject.SetActive(false);
            corridorLayer.gameObject.SetActive(true);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(catalog);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD-COMPLETION-AREA-1: 대관령면 Farm 1km 경관 완결 영역을 L2 500m 2x2 구획으로 구성했습니다. WorldCover 위치와 Scenario 경관은 분리했고 Scene Mesh는 아직 ScenarioTerrainPreview입니다.");
        }

        [MenuItem("Ssalddel/WORLD-INVENTORY-UNITY-2 대관령 L2 창고 1인칭 상호작용 연결")]
        public static void Build대관령L2창고일인칭상호작용()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var pipelineRoot = GameObject.Find(
                "SimulationWorldShell/WorldMapRoot/OfficialRegionProjectionRoot/"
                + "SpatialPipeline_EPSG5186_TileAreaSet")?.transform
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseSpatialPipelineMissing");
            var completionArea = pipelineRoot.Find(
                "L4_L7_Synty경관_PresentationOnly/"
                + "CompletionArea_대관령면Farm_1km_L2_2x2")
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseCompletionAreaMissing");
            Build대관령L2창고일인칭상호작용(
                pipelineRoot, completionArea, EnsureCatalog());
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "DaegwallyeongWarehouseInteractionSceneSaveFailed");
            AssetDatabase.SaveAssets();
            Validate대관령L2창고일인칭상호작용OpenScene();
            Debug.Log("WORLD-INVENTORY-UNITY-2: 대관령 L2 Barn Pallet의 1인칭 Preview·Confirm 연결을 저장했습니다.");
        }

        public static void Validate대관령L2창고일인칭상호작용OpenScene()
        {
            var target = UnityEngine.Object
                .FindFirstObjectByType<대관령L2창고상호작용TargetView>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseInteractionTargetMissing");
            var interaction = UnityEngine.Object
                .FindFirstObjectByType<일인칭창고상호작용Controller>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseInteractionControllerMissing");
            var composition = UnityEngine.Object
                .FindFirstObjectByType<대관령L2창고아이템CompositionRoot>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseCompositionMissing");
            if (!target.ValidateWiring() || !interaction.ValidateWiring()
                || !target.PresentationOnly || !interaction.PresentationOnly
                || composition == null)
                throw new InvalidOperationException(
                    "DaegwallyeongWarehouseInteractionWiringInvalid");
        }

        [MenuItem("Ssalddel/WORLD-LEGAL-3 공간 Pipeline 배치 증거 캡처")]
        public static void CaptureEvidence()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(EvidenceRoot);
            var worldMap = GameObject.Find("WorldMapRoot")
                ?? throw new InvalidOperationException("LegalDongWorldMapRootMissing");
            worldMap.SetActive(true);
            var projectionRoot = worldMap.transform.Find("OfficialRegionProjectionRoot")
                ?? throw new InvalidOperationException("LegalDongProjectionRootMissing");
            projectionRoot.gameObject.SetActive(true);
            var captures = new[]
            {
                ("LegalWorldFarmCamera", "01-daegwallyeong-farm-1km-first-view.png"),
                ("LegalWorldOverviewCamera", "02-world-overview.png"),
                ("LegalWorldCorridorCamera", "03-farm-hub-corridor.png"),
                ("LegalWorldHubCamera", "04-jinbu-hub.png"),
                ("LegalWorldTownCamera", "05-pyeongchang-town.png"),
                ("LegalWorldFarmFirstPersonCamera", "06-daegwallyeong-farm-first-person-camera.png"),
            };
            foreach (var capture in captures)
            {
                var root = GameObject.Find(capture.Item1)
                    ?? throw new InvalidOperationException("LegalDongEvidenceCameraMissing:" + capture.Item1);
                var camera = root.GetComponent<Camera>()
                    ?? throw new InvalidOperationException("LegalDongEvidenceCameraInvalid:" + capture.Item1);
                Capture(camera, EvidenceRoot + "/" + capture.Item2);
            }
            AssetDatabase.Refresh();
            Debug.Log("WORLD-COMPLETION-AREA-1: 대관령 Farm 1km 첫 화면과 연계 화면을 PNG로 렌더링했습니다. Play Mode 입력 검증과는 분리된 표현 증거입니다.");
        }

        [MenuItem("Ssalddel/WORLD-COMPLETION-AREA-1 대관령 Farm Play 화면 저장")]
        public static void CaptureFarmPlayGameView()
        {
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("FarmGameViewCaptureRequiresPlayMode");

            Directory.CreateDirectory(EvidenceRoot);
            var farmCameraRoot = GameObject.Find("LegalWorldFarmCamera")
                ?? throw new InvalidOperationException("LegalDongEvidenceCameraMissing:LegalWorldFarmCamera");
            var farmCamera = farmCameraRoot.GetComponent<Camera>()
                ?? throw new InvalidOperationException("LegalDongEvidenceCameraInvalid:LegalWorldFarmCamera");

            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include))
                camera.enabled = camera == farmCamera;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
                canvas.gameObject.SetActive(false);

            farmCameraRoot.SetActive(true);
            farmCamera.enabled = true;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            ScreenCapture.CaptureScreenshot(
                EvidenceRoot + "/01-daegwallyeong-farm-1km-play-game-view.png", 1);
            Debug.Log("WORLD-COMPLETION-AREA-1: 실제 Play Mode Game View에서 대관령 Farm 1km 화면 저장을 요청했습니다.");
        }

        [MenuItem("Ssalddel/WORLD-COMPLETION-AREA-2 대관령 Farm 1인칭 Play 화면 저장 _F8")]
        public static void CaptureFarmFirstPersonPlayGameView()
        {
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("FarmFirstPersonCaptureRequiresPlayMode");

            Directory.CreateDirectory(EvidenceRoot);
            var firstPersonRoot = GameObject.Find("LegalWorldFarmFirstPersonCamera")
                ?? throw new InvalidOperationException(
                    "LegalDongEvidenceCameraMissing:LegalWorldFarmFirstPersonCamera");
            var firstPersonCamera = firstPersonRoot.GetComponent<Camera>()
                ?? throw new InvalidOperationException(
                    "LegalDongEvidenceCameraInvalid:LegalWorldFarmFirstPersonCamera");
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include))
                camera.enabled = camera == firstPersonCamera;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
                canvas.gameObject.SetActive(false);

            firstPersonRoot.SetActive(true);
            firstPersonCamera.enabled = true;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            ScreenCapture.CaptureScreenshot(
                EvidenceRoot + "/06-daegwallyeong-farm-first-person-play-game-view.png", 1);
            Debug.Log("WORLD-COMPLETION-AREA-2: 경관 품질 Pipeline을 적용한 실제 1인칭 Play Mode Game View 저장을 요청했습니다.");
        }

        [MenuItem("Ssalddel/WORLD-COMPLETION-AREA-3 대관령 Farm RTS 전술 지휘 화면 저장 _F9")]
        public static void CaptureFarmPlayerWalkGameView()
        {
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("FarmPlayerCaptureRequiresPlayMode");

            Directory.CreateDirectory(EvidenceRoot);
            var controller = UnityEngine.Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("FarmPlayerControllerMissing");
            controller.EnterThirdPersonMode();
            controller.SetThirdPersonSelection(true);
            if (!controller.HasDestination)
                controller.SetThirdPersonDestination(
                    controller.transform.position + new Vector3(3.2f, 0f, 2.4f));
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
                canvas.gameObject.SetActive(false);

            EditorApplication.ExecuteMenuItem("Window/General/Game");
            ScreenCapture.CaptureScreenshot(
                EvidenceRoot + "/10-daegwallyeong-farm-rts-tactical-command-game-view.png", 1);
            Debug.Log("WORLD-COMPLETION-AREA-3: 높은 사선 RTS 전술 카메라, 선택 유닛과 우클릭 목적지 Game View 저장을 요청했습니다.");
        }

        [MenuItem("Ssalddel/WORLD-COMPLETION-AREA-3 대관령 Farm 플레이어 1인칭 화면 저장")]
        public static void CaptureFarmPlayerFirstPersonGameView()
        {
            if (!UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("FarmPlayerCaptureRequiresPlayMode");

            Directory.CreateDirectory(EvidenceRoot);
            var controller = UnityEngine.Object.FindAnyObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("FarmPlayerControllerMissing");
            controller.EnterFirstPersonMode();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
                canvas.gameObject.SetActive(false);

            EditorApplication.ExecuteMenuItem("Window/General/Game");
            ScreenCapture.CaptureScreenshot(
                EvidenceRoot + "/08-daegwallyeong-farm-player-first-person-wasd-game-view.png", 1);
            Debug.Log("WORLD-COMPLETION-AREA-3: 플레이어 눈높이 1인칭 WASD Game View 저장을 요청했습니다.");
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void BuildSpatialUnitHierarchy(
            Transform tileRoot, Transform areaRoot, Transform areaSetRoot,
            WorldBuildManifest manifest)
        {
            foreach (var level in new[] { 0, 1, 2 })
                Child(tileRoot, $"L{level}_{공간TileLevelCodes.SizeMeters(level)}m_Halo");
            foreach (var area in manifest.Areas)
            {
                var root = Child(areaRoot, area.AreaKindCode + "_" + area.AreaStableId.Split(':').Last());
                Child(root, "TileReferences_" + area.TileReferences.Length);
            }
            foreach (var link in manifest.Links)
                Child(areaSetRoot, link.LinkKindCode + "_" + link.LinkStableId.Split(':').Last());
            foreach (var completionArea in manifest.CompletionAreas)
            {
                var root = Child(areaSetRoot,
                    "CompletionArea_" + completionArea.CompletionAreaStableId.Split(':').Last());
                Child(root, "TaskTiles_" + completionArea.TaskTileReferences.Length);
                foreach (var stage in completionArea.VerticalStages)
                    Child(root, stage.StageCode + "_" + stage.StatusCode);
            }
        }

        private static void BuildRegionTerrain(Transform parent, 법정동WorldNodeData node)
        {
            var root = new GameObject("RegionCell_" + node.KoreanName);
            root.transform.SetParent(parent, false);
            var mesh = EnsureRegionMesh(node);
            var points = node.BoundaryPoints;
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var color = node.RoleCode switch
            {
                법정동WorldRoleCodes.Farm => new Color(.27f, .43f, .18f),
                법정동WorldRoleCodes.Hub => new Color(.35f, .39f, .30f),
                법정동WorldRoleCodes.Town => new Color(.46f, .43f, .31f),
                _ => new Color(.31f, .40f, .27f),
            };
            root.AddComponent<MeshRenderer>().sharedMaterial =
                EnsureMaterial("Terrain_" + node.RoleCode, color, .12f);
        }

        private static void BuildContinuousTerrain(Transform parent)
        {
            Directory.CreateDirectory(GeneratedRoot + "/Meshes");
            var path = GeneratedRoot + "/Meshes/ContinuousScenarioTerrain.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ContinuousScenarioTerrain" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            const int columns = 25;
            const int rows = 19;
            var vertices = new List<Vector3>();
            for (var zIndex = 0; zIndex < rows; zIndex++)
            for (var xIndex = 0; xIndex < columns; xIndex++)
            {
                var x = Mathf.Lerp(-31f, 31f, xIndex / (float)(columns - 1));
                var z = Mathf.Lerp(-23f, 23f, zIndex / (float)(rows - 1));
                vertices.Add(new Vector3(x, ScenarioHeight(x, z), z));
            }
            var triangles = new List<int>();
            for (var zIndex = 0; zIndex < rows - 1; zIndex++)
            for (var xIndex = 0; xIndex < columns - 1; xIndex++)
            {
                var a = zIndex * columns + xIndex;
                var b = a + 1;
                var c = a + columns;
                var d = c + 1;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var root = new GameObject("ContinuousTerrain_DEMIncomplete");
            root.transform.SetParent(parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = EnsureMaterial(
                "ContinuousTerrain", new Color(.29f, .39f, .22f), .08f);
            root.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static Mesh EnsureRegionMesh(법정동WorldNodeData node)
        {
            Directory.CreateDirectory(GeneratedRoot + "/Meshes");
            var path = GeneratedRoot + "/Meshes/RegionCell_" + node.LegalDongCode + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "OfficialBoundaryMesh_" + node.LegalDongCode };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            var vertices = new List<Vector3>
            {
                new(node.LocalX, ScenarioHeight(node.LocalX, node.LocalZ), node.LocalZ),
            };
            vertices.AddRange(node.BoundaryPoints.Select(point => new Vector3(
                point.X, ScenarioHeight(point.X, point.Z), point.Z)));
            var triangles = new List<int>();
            for (var index = 0; index < node.BoundaryPoints.Length; index++)
            {
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add(((index + 1) % node.BoundaryPoints.Length) + 1);
            }
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void BuildBoundary(Transform parent, 법정동WorldNodeData node)
        {
            var root = new GameObject("Boundary_" + node.KoreanName);
            root.transform.SetParent(parent, false);
            var line = root.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = node.BoundaryPoints.Length;
            line.widthMultiplier = .12f;
            line.sharedMaterial = Material(new Color(.88f, .94f, .82f), .35f);
            line.SetPositions(node.BoundaryPoints.Select(point => new Vector3(
                point.X, ScenarioHeight(point.X, point.Z) + .12f, point.Z)).ToArray());
        }

        private static Transform BuildDebugAnchor(Transform parent, 법정동WorldNodeData node)
        {
            var root = new GameObject($"법정동_{node.KoreanName}_{node.LegalDongCode}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(
                node.LocalX, ScenarioHeight(node.LocalX, node.LocalZ) + .5f, node.LocalZ);
            Label(root.transform, "지역Label",
                $"{node.KoreanName}\n{node.LegalDongCode}\n{node.RoleCode}",
                Vector3.up, Color.white, .18f);
            return root.transform;
        }

        private static void BuildRoute(
            Transform parent, 법정동WorldRouteData route, Vector3 from, Vector3 to)
        {
            var root = new GameObject("개략연결로_" + route.RouteStableId.Split(':').Last());
            root.transform.SetParent(parent, false);
            var line = root.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.widthMultiplier = .22f;
            line.sharedMaterial = Material(new Color(.72f, .55f, .24f), .08f);
            line.SetPositions(new[] { from + Vector3.up * .08f, to + Vector3.up * .08f });
        }

        private static void BuildScenery(
            Transform parent, Transform referenceTileRoot,
            법정동경관PlanData plan, 법정동경관VisualCatalog catalog)
        {
            foreach (var placement in plan.Placements)
            {
                var entry = catalog.Resolve(placement.VisualKey);
                if (!entry.AllowedLandCoverCodes.Contains(placement.LandCoverCode)
                    || !entry.AllowedRegionRoleCodes.Contains(placement.RegionRoleCode))
                    throw new InvalidOperationException("LegalDongScenicPlacementRejected:" + placement.PlacementStableId);
                Visual(placement.RegionStableId == 평창군법정동WorldFixture.FarmRegionStableId
                    ? referenceTileRoot : parent, catalog, placement);
            }
        }

        /// <summary>
        /// 지면·배치 Container·동적 계절 표현만 준비합니다.
        /// 정적 Synty Prefab은 WORLD-PLAN-3에서 검증된 JSON 계획으로만 적용합니다.
        /// </summary>
        private static void BuildFarmCompletionAreaScaffold(Transform completionAreaRoot)
        {
            var natureCatalog = AssetDatabase
                .LoadAssetAtPath<자연경관CompositionCatalog>(
                    자연경관CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException(
                    "PyeongchangNatureCompositionCatalogMissing");
            natureCatalog.Validate();
            var southWest = completionAreaRoot.Find(
                    "ReferenceTile_대관령면_L2_500m_HandAuthored")
                ?? throw new InvalidOperationException("FarmCompletionReferenceTileMissing");
            southWest.name = "Tile_kr5186_l2_700_1144_농장마당_Reference";
            var southEast = Child(completionAreaRoot,
                "Tile_kr5186_l2_701_1144_감자경작지");
            var northWest = Child(completionAreaRoot,
                "Tile_kr5186_l2_700_1145_산림전이");
            var northEast = Child(completionAreaRoot,
                "Tile_kr5186_l2_701_1145_출발회랑");

            정적경관배치PlanPipeline.ConfigureAnchor(
                southWest, 정적경관배치ContainerStableIds.FarmSouthWest);
            정적경관배치PlanPipeline.ConfigureAnchor(
                southEast, 정적경관배치ContainerStableIds.FarmSouthEast);
            정적경관배치PlanPipeline.ConfigureAnchor(
                northWest, 정적경관배치ContainerStableIds.FarmNorthWest);
            정적경관배치PlanPipeline.ConfigureAnchor(
                northEast, 정적경관배치ContainerStableIds.FarmNorthEast);

            CompletionGround(southWest, "농장마당지면", 16f, 7f,
                new Color(.39f, .34f, .19f));
            CompletionGround(southEast, "감자경작지지면", 26f, 7f,
                new Color(.45f, .31f, .16f));
            CompletionGround(northWest, "산림전이지면", 16f, 17f,
                new Color(.20f, .33f, .17f));
            CompletionGround(northEast, "출발회랑지면", 26f, 17f,
                new Color(.30f, .37f, .19f));

            BuildNatureSeasonAndEventPresentation(completionAreaRoot, natureCatalog);
            var labels = Child(completionAreaRoot, "MapMode_완결영역구획Label");
            Label(labels, "농장마당Label", "농장 마당\n700:1144",
                new Vector3(16f, 1.2f, 7f), Color.white, .12f);
            Label(labels, "경작지Label", "감자 경작지\n701:1144",
                new Vector3(26f, 1.2f, 7f), Color.white, .12f);
            Label(labels, "산림전이Label", "산림 전이\n700:1145",
                new Vector3(16f, 1.2f, 17f), Color.white, .12f);
            Label(labels, "출발회랑Label", "출발 회랑\n701:1145",
                new Vector3(26f, 1.2f, 17f), Color.white, .12f);
            labels.gameObject.SetActive(false);
        }

        private static void BuildStaticSceneryCorridorAnchors(Transform corridorLayer)
        {
            var farmHub = Child(corridorLayer, "StaticSceneryAnchor_FarmHubCorridor");
            var hubTown = Child(corridorLayer, "StaticSceneryAnchor_HubTownCorridor");
            정적경관배치PlanPipeline.ConfigureAnchor(
                farmHub, 정적경관배치ContainerStableIds.FarmHubCorridor);
            정적경관배치PlanPipeline.ConfigureAnchor(
                hubTown, 정적경관배치ContainerStableIds.HubTownCorridor);
        }

        private static void BuildStaticSceneryAreaAnchors(Transform scenicLayer)
        {
            var areaAnchors = Child(scenicLayer, "StaticSceneryAreaAnchors_Hub_Town");
            var jinbuHub = Child(areaAnchors, "StaticSceneryAnchor_진부Hub");
            var pyeongchangTown = Child(areaAnchors, "StaticSceneryAnchor_평창읍Town");
            정적경관배치PlanPipeline.ConfigureAnchor(
                jinbuHub, 정적경관배치ContainerStableIds.JinbuHub);
            정적경관배치PlanPipeline.ConfigureAnchor(
                pyeongchangTown, 정적경관배치ContainerStableIds.PyeongchangTown);
        }

        private static void BuildFarmCompletionArea(
            Transform completionAreaRoot, 법정동경관VisualCatalog catalog)
        {
            var natureCatalog = AssetDatabase
                .LoadAssetAtPath<자연경관CompositionCatalog>(
                    자연경관CompositionSetBuilder.CatalogPath)
                ?? throw new InvalidOperationException(
                    "PyeongchangNatureCompositionCatalogMissing");
            natureCatalog.Validate();
            var southWest = completionAreaRoot.Find(
                    "ReferenceTile_대관령면_L2_500m_HandAuthored")
                ?? throw new InvalidOperationException("FarmCompletionReferenceTileMissing");
            southWest.name = "Tile_kr5186_l2_700_1144_농장마당_Reference";
            var southEast = Child(completionAreaRoot,
                "Tile_kr5186_l2_701_1144_감자경작지");
            var northWest = Child(completionAreaRoot,
                "Tile_kr5186_l2_700_1145_산림전이");
            var northEast = Child(completionAreaRoot,
                "Tile_kr5186_l2_701_1145_출발회랑");

            CompletionGround(southWest, "농장마당지면", 16f, 7f,
                new Color(.39f, .34f, .19f));
            CompletionGround(southEast, "감자경작지지면", 26f, 7f,
                new Color(.45f, .31f, .16f));
            CompletionGround(northWest, "산림전이지면", 16f, 17f,
                new Color(.20f, .33f, .17f));
            CompletionGround(northEast, "출발회랑지면", 26f, 17f,
                new Color(.30f, .37f, .19f));

            void Place(Transform parent, string suffix, string visualKey, string cover,
                float x, float z, float rotation, float scale, int density, int lod)
                => Visual(parent, catalog, new 법정동경관PlacementData
                {
                    PlacementStableId =
                        "scenic:sim:pyeongchang:completion-area-farm-" + suffix,
                    RegionStableId = 평창군법정동WorldFixture.FarmRegionStableId,
                    VisualKey = visualKey,
                    LandCoverCode = cover,
                    RegionRoleCode = 법정동WorldRoleCodes.Farm,
                    LocalX = x,
                    LocalZ = z,
                    RotationY = rotation,
                    Scale = scale,
                    DensityTier = density,
                    LodGroup = lod,
                });

            // 남서쪽: 작업 마당과 시설재배를 묶어 농장 중심을 강화합니다.
            Place(southWest, "greenhouse-a", 법정동경관VisualKeys.Greenhouse,
                법정동LandCoverCodes.Cropland, 13.2f, 5.1f, 18f, .28f, 0, 1);
            Place(southWest, "greenhouse-b", 법정동경관VisualKeys.Greenhouse,
                법정동LandCoverCodes.Cropland, 15.8f, 4.5f, 18f, .25f, 0, 1);
            Place(southWest, "yard-tree-a", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, 12.2f, 9.8f, 12f, .68f, 2, 2);
            Place(southWest, "yard-tree-b", 법정동경관VisualKeys.Tree,
                법정동LandCoverCodes.Cropland, 17.2f, 3.2f, 72f, .62f, 2, 2);
            for (var index = 0; index < 5; index++)
                Place(southWest, "yard-fence-" + index, 법정동경관VisualKeys.Fence,
                    법정동LandCoverCodes.Corridor, 11.8f + index * 1.55f, 2.7f,
                    90f, .52f, 2, 2);

            // 남동쪽: 큰 밭고랑 덩어리와 작물 세부 표현을 분리해 밀도를 만듭니다.
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 4; column++)
                Place(southEast, $"field-{row}-{column}", 법정동경관VisualKeys.SoilRows,
                    법정동LandCoverCodes.Cropland, 22.5f + column * 2.1f,
                    3.9f + row * 2.35f, 8f, .34f, 1, 1);
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 7; column++)
                Place(southEast, $"potato-{row}-{column}", 법정동경관VisualKeys.Potato,
                    법정동LandCoverCodes.Cropland, 21.8f + column * 1.18f,
                    3.4f + row * 1.55f, (row * 7 + column) * 11f, .38f, 2, 2);
            for (var index = 0; index < 4; index++)
                Place(southEast, "field-edge-tree-" + index, 법정동경관VisualKeys.Tree,
                    법정동LandCoverCodes.Cropland, 21.1f + index * 2.7f, 11.2f,
                    index * 37f, .55f + index * .03f, 2, 2);

            // 북서쪽: Nature를 평창 경관의 바탕층으로 사용하고 Farm은 생산 영역에 남깁니다.
            Place(northWest, "forest-mountain", 법정동경관VisualKeys.MountainSoft,
                법정동LandCoverCodes.Forest, 13.2f, 20.5f, -18f, .32f, 0, 0);
            Place(northWest, "forest-distant-mountain", 법정동경관VisualKeys.DistantMountain,
                법정동LandCoverCodes.Forest, 18.8f, 22.4f, 12f, .22f, 0, 0);
            Place(northWest, "forest-cluster-a", 법정동경관VisualKeys.TreePatch,
                법정동LandCoverCodes.Forest, 13.4f, 15.2f, 16f, .44f, 0, 0);
            Place(northWest, "forest-cluster-b", 법정동경관VisualKeys.TreePatch,
                법정동LandCoverCodes.Forest, 18.1f, 18.6f, -24f, .40f, 0, 0);
            Place(northWest, "forest-mixed-a", 법정동경관VisualKeys.MixedTreePatch,
                법정동LandCoverCodes.Forest, 15.2f, 17.1f, 31f, .48f, 0, 1);
            Place(northWest, "forest-mixed-b", 법정동경관VisualKeys.MixedTreePatch,
                법정동LandCoverCodes.Forest, 19.1f, 14.4f, -17f, .42f, 0, 1);
            for (var index = 0; index < 9; index++)
                Place(northWest, "conifer-" + index, 법정동경관VisualKeys.ConiferTree,
                    법정동LandCoverCodes.Forest, 11.7f + (index % 3) * 3.1f,
                    13.2f + (index / 3) * 2.8f, index * 29f,
                    .48f + (index % 3) * .06f, 1, 1);
            for (var index = 0; index < 6; index++)
                Place(northWest, "broadleaf-" + index, 법정동경관VisualKeys.BroadleafTree,
                    법정동LandCoverCodes.Forest, 12.4f + (index % 3) * 3.2f,
                    14.1f + (index / 3) * 4.1f, 17f + index * 43f,
                    .46f + (index % 2) * .07f, 1, 1);
            for (var index = 0; index < 5; index++)
                Place(northWest, "understory-" + index, 법정동경관VisualKeys.Understory,
                    법정동LandCoverCodes.Forest, 12.9f + index * 1.55f,
                    12.9f + (index % 2) * 1.4f, index * 53f, .58f, 2, 2);
            for (var index = 0; index < 4; index++)
                Place(northWest, "forest-edge-" + index, 법정동경관VisualKeys.ForestEdge,
                    법정동LandCoverCodes.Forest, 11.3f + index * 2.7f,
                    11.6f, 90f, .52f, 2, 2);
            for (var index = 0; index < 4; index++)
                Place(northWest, "forest-rock-" + index, 법정동경관VisualKeys.SmallRocks,
                    법정동LandCoverCodes.BareGround, 12.5f + index * 2.2f,
                    12.2f + (index % 2) * 1.1f, index * 41f, .45f, 1, 1);
            Place(northWest, "forest-rock-wall", 법정동경관VisualKeys.RockWall,
                법정동LandCoverCodes.BareGround, 19.4f, 19.8f, -34f, .28f, 1, 1);
            Place(northWest, "forest-wind-leaves", 법정동경관VisualKeys.WindLeavesFx,
                법정동LandCoverCodes.Forest, 16.1f, 17.4f, 0f, .7f, 2, 2);
            PlaceNatureComposition(northWest, natureCatalog,
                자연경관SetNames.활엽수림군집,
                "forest-broadleaf-composition", "kr5186:l2:700:1145:broadleaf-01",
                법정동LandCoverCodes.Forest, false,
                11.7f, 19.4f, -16f, .38f, 24f);
            PlaceNatureComposition(northWest, natureCatalog,
                자연경관SetNames.침엽수림군집,
                "forest-conifer-composition", "kr5186:l2:700:1145:conifer-01",
                법정동LandCoverCodes.Forest, false,
                20.2f, 17.7f, 29f, .36f, 32f);
            PlaceNatureComposition(northWest, natureCatalog,
                자연경관SetNames.혼효림군집,
                "forest-mixed-composition", "kr5186:l2:700:1145:mixed-01",
                법정동LandCoverCodes.Forest, false,
                17.7f, 20.3f, 8f, .34f, 28f);
            PlaceNatureComposition(northWest, natureCatalog,
                자연경관SetNames.산능선,
                "forest-mountain-ridge-composition",
                "kr5186:l1:175:286:mountain-ridge-01",
                법정동LandCoverCodes.Forest, false,
                15.2f, 22.1f, -11f, .20f, 120f);
            PlaceNatureComposition(northWest, natureCatalog,
                자연경관SetNames.숲가장자리,
                "forest-edge-composition", "kr5186:l2:700:1145:forest-edge-01",
                법정동LandCoverCodes.Forest, false,
                15.4f, 12.1f, 88f, .42f, 18f);

            // 북동쪽: Farm에서 Hub로 빠져나가는 굽은 회랑을 하나의 장면으로 읽게 합니다.
            var roadPoints = new[]
            {
                new Vector3(20.5f, 0f, 12f), new Vector3(22.5f, 0f, 13.6f),
                new Vector3(24.6f, 0f, 15.1f), new Vector3(27f, 0f, 16.2f),
                new Vector3(29.4f, 0f, 17.8f), new Vector3(31f, 0f, 19.4f),
            };
            for (var index = 0; index < roadPoints.Length - 1; index++)
            {
                var from = roadPoints[index];
                var to = roadPoints[index + 1];
                var middle = Vector3.Lerp(from, to, .5f);
                Place(northEast, "departure-road-" + index,
                    법정동경관VisualKeys.RuralRoad, 법정동LandCoverCodes.Corridor,
                    middle.x, middle.z,
                    Quaternion.LookRotation(to - from).eulerAngles.y, .43f, 1, 1);
            }
            Place(northEast, "departure-stand", 법정동경관VisualKeys.ProduceStand,
                법정동LandCoverCodes.Cropland, 24f, 18.1f, 138f, .42f, 1, 1);
            Place(northEast, "departure-windmill", 법정동경관VisualKeys.Windmill,
                법정동LandCoverCodes.Cropland, 28.7f, 20.2f, -12f, .48f, 1, 1);
            for (var index = 0; index < 6; index++)
                Place(northEast, "departure-tree-" + index, 법정동경관VisualKeys.Tree,
                    법정동LandCoverCodes.Forest, 21.4f + index * 1.75f,
                    21.1f - (index % 2) * .7f, index * 31f, .55f, 2, 2);
            for (var index = 0; index < 5; index++)
                Place(northEast, "departure-fence-" + index, 법정동경관VisualKeys.Fence,
                    법정동LandCoverCodes.Corridor, 22f + index * 1.9f,
                    12.8f + index * 1.25f, 56f, .48f, 2, 2);

            BuildNatureSeasonAndEventPresentation(
                completionAreaRoot, natureCatalog);

            var labels = Child(completionAreaRoot, "MapMode_완결영역구획Label");
            Label(labels, "농장마당Label", "농장 마당\n700:1144",
                new Vector3(16f, 1.2f, 7f), Color.white, .12f);
            Label(labels, "경작지Label", "감자 경작지\n701:1144",
                new Vector3(26f, 1.2f, 7f), Color.white, .12f);
            Label(labels, "산림전이Label", "산림 전이\n700:1145",
                new Vector3(16f, 1.2f, 17f), Color.white, .12f);
            Label(labels, "출발회랑Label", "출발 회랑\n701:1145",
                new Vector3(26f, 1.2f, 17f), Color.white, .12f);
            labels.gameObject.SetActive(false);
        }

        private static void PlaceNatureComposition(
            Transform parent,
            자연경관CompositionCatalog catalog,
            string setName,
            string stableName,
            string worldSlotStableKey,
            string landCoverCode,
            bool hasWaterMask,
            float x,
            float z,
            float rotationY,
            float scale,
            float viewDistance)
        {
            var selector = new 자연경관CompositionSelector();
            var variantCode = selector.ResolveVariant(
                setName, worldSlotStableKey, 51760);
            var entry = catalog.Resolve(setName, variantCode);
            var slope = PhysicalSlopeDegrees(x, z);
            if (!selector.CanPlace(entry, landCoverCode, slope, hasWaterMask,
                    자연경관SeasonCodes.Spring, 자연경관MoodCodes.Peaceful,
                    viewDistance))
                throw new InvalidOperationException(
                    "NatureCompositionPlacementRejected:" + worldSlotStableKey);
            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab, parent) as GameObject
                ?? throw new InvalidOperationException(
                    "NatureCompositionPlacementFailed:" + entry.CompositionKey);
            instance.name = stableName + "-" + variantCode.ToLowerInvariant();
            instance.transform.localPosition = new Vector3(x, ScenarioHeight(x, z), z);
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale = Vector3.one * scale;
            if (!instance.TryGetComponent<자연경관CompositionSetView>(out var view)
                || view == null || !view.ValidateWiring() || !view.PresentationOnly)
                throw new InvalidOperationException(
                    "NatureCompositionPlacementWiringInvalid:" + entry.CompositionKey);
        }

        private static void BuildNatureSeasonAndEventPresentation(
            Transform completionAreaRoot,
            자연경관CompositionCatalog natureCatalog)
        {
            var seasonRoot = Child(completionAreaRoot,
                "NatureSeasonPresentation_PresentationOnly");
            var seasonDefinitions = new[]
            {
                (자연경관SeasonCodes.Spring,
                    "FX/FX_Butterlies_Particle_01_MultiColour.prefab"),
                (자연경관SeasonCodes.Summer,
                    "FX/FX_SunBeams_Particle_01.prefab"),
                (자연경관SeasonCodes.Autumn,
                    "FX/FX_Leaves_Orange_01.prefab"),
                (자연경관SeasonCodes.Winter,
                    "FX/FX_Snow_01.prefab"),
            };
            var seasonBindings = new 자연경관SeasonFxBinding[seasonDefinitions.Length];
            for (var index = 0; index < seasonDefinitions.Length; index++)
            {
                var definition = seasonDefinitions[index];
                var sourcePath = Nature + definition.Item2;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath)
                    ?? throw new InvalidOperationException(
                        "NatureSeasonFxMissing:" + sourcePath);
                var instance = PrefabUtility.InstantiatePrefab(source, seasonRoot) as GameObject
                    ?? throw new InvalidOperationException(
                        "NatureSeasonFxInstantiateFailed:" + sourcePath);
                instance.name = "SeasonFx_" + definition.Item1;
                instance.transform.localPosition = new Vector3(
                    16f, ScenarioHeight(16f, 17f) + 1f, 17f);
                instance.transform.localScale = Vector3.one * .7f;
                var binding = new 자연경관SeasonFxBinding();
                binding.Configure(definition.Item1, instance);
                seasonBindings[index] = binding;
            }

            var seasonController = seasonRoot.gameObject
                .AddComponent<자연경관SeasonPresentationController>();
            seasonController.Configure(completionAreaRoot, seasonBindings,
                자연경관SeasonCodes.Spring);
            if (!seasonController.ValidateWiring())
                throw new InvalidOperationException(
                    "NatureSeasonPresentationWiringInvalid");

            var eventCatalog = AssetDatabase
                .LoadAssetAtPath<자연경관EventOverlayCatalog>(
                    자연경관EventOverlayBuilder.CatalogPath)
                ?? throw new InvalidOperationException(
                    "NatureEventOverlayCatalogMissing");
            eventCatalog.Validate();
            var eventRoot = Child(completionAreaRoot,
                "NatureEventOverlayRoot_PresentationOnly_Inactive");
            for (var index = 0; index < eventCatalog.Entries.Count; index++)
            {
                var entry = eventCatalog.Entries[index];
                var instance = PrefabUtility.InstantiatePrefab(
                    entry.Prefab, eventRoot) as GameObject
                    ?? throw new InvalidOperationException(
                        "NatureEventOverlayPlacementFailed:"
                        + entry.PresentationKey);
                instance.name = "EventOverlay_" + entry.OverlayName.Replace(" ", string.Empty);
                var x = 13.5f + (index % 3) * 2.5f;
                var z = 15f + (index / 3) * 2.8f;
                instance.transform.localPosition = new Vector3(
                    x, ScenarioHeight(x, z), z);
                instance.transform.localScale = Vector3.one * .42f;
                instance.SetActive(false);
            }

            var eventController = eventRoot.gameObject
                .AddComponent<자연경관EventOverlayController>();
            eventController.Configure(eventRoot, eventCatalog);
            if (!eventController.ValidateWiring()
                || eventController.ActiveOverlayCount != 0)
                throw new InvalidOperationException(
                    "NatureEventOverlayWiringInvalid");

            // 수계 근거가 없는 대관령 Farm 기준 영역에는 개울 회랑을 배치하지 않는다.
            // 생성 대장만 준비하고 실제 Water mask가 있는 Area에서 선택한다.
            var streamCandidate = natureCatalog.Resolve(
                자연경관SetNames.개울회랑, 월드CompositionVariantCodes.A);
            if (!streamCandidate.RequiresWaterMask)
                throw new InvalidOperationException(
                    "NatureStreamCompositionMustRequireWaterMask");
        }

        private static void Build대관령L2창고일인칭상호작용(
            Transform pipelineRoot,
            Transform completionAreaRoot,
            법정동경관VisualCatalog catalog)
        {
            var tile = completionAreaRoot.Find(
                    "Tile_kr5186_l2_700_1145_산림전이")
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseL2TileMissing");
            var old = tile.Find("ScenarioWarehouseInteraction_ObservedFixture");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var oldBarn = tile.Find(
                "scenic_sim_pyeongchang_l2-700-1145-observed-fixture-barn");
            if (oldBarn != null) UnityEngine.Object.DestroyImmediate(oldBarn.gameObject);

            Visual(tile, catalog, new 법정동경관PlacementData
            {
                PlacementStableId =
                    "scenic:sim:pyeongchang:l2-700-1145-observed-fixture-barn",
                RegionStableId = 평창군법정동WorldFixture.FarmRegionStableId,
                VisualKey = 법정동경관VisualKeys.Barn,
                LandCoverCode = 법정동LandCoverCodes.Cropland,
                RegionRoleCode = 법정동WorldRoleCodes.Farm,
                LocalX = 15.4f,
                LocalZ = 16.4f,
                RotationY = 180f,
                Scale = .32f,
                DensityTier = 0,
                LodGroup = 1,
            });

            var root = Child(tile, "ScenarioWarehouseInteraction_ObservedFixture");
            root.position = new Vector3(15.4f,
                ScenarioHeight(15.4f, 13.25f), 13.25f);
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Pallet_표현전용";
            platform.transform.SetParent(root, false);
            platform.transform.localPosition = new Vector3(0f, .1f, 0f);
            platform.transform.localScale = new Vector3(1.8f, .2f, 1.2f);
            platform.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(
                "대관령L2창고Pallet", new Color(.34f, .20f, .09f), .02f);
            UnityEngine.Object.DestroyImmediate(platform.GetComponent<Collider>());

            var positions = new[]
            {
                new Vector3(-.48f, .48f, 0f),
                new Vector3(.02f, .48f, 0f),
                new Vector3(.28f, .93f, 0f),
            };
            var boxes = new GameObject[positions.Length];
            for (var index = 0; index < positions.Length; index++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "감자상자_서버수량표현_" + (index + 1);
                box.transform.SetParent(root, false);
                box.transform.localPosition = positions[index];
                box.transform.localScale = new Vector3(.44f, .54f, .44f);
                box.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(
                    "대관령L2감자상자", new Color(.57f, .36f, .16f), .04f);
                UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
                boxes[index] = box;
            }

            var volume = Child(root, "InteractionVolume");
            // 지형 경사로 Pallet과 플레이어 눈높이가 달라도 정면 시선이
            // 상호작용 면을 안정적으로 통과하도록 세로 범위만 넉넉히 둔다.
            volume.localPosition = new Vector3(0f, 1.7f, 0f);
            var collider = volume.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 4f, 1.45f);
            var highlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            highlight.name = "시선선택강조_PresentationOnly";
            highlight.transform.SetParent(root, false);
            highlight.transform.localPosition = new Vector3(0f, .025f, 0f);
            highlight.transform.localScale = new Vector3(1.15f, .025f, 1.15f);
            highlight.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(
                "대관령L2창고시선강조", new Color(1f, .62f, .08f), .1f);
            UnityEngine.Object.DestroyImmediate(highlight.GetComponent<Collider>());
            highlight.SetActive(false);

            var target = root.gameObject
                .AddComponent<대관령L2창고상호작용TargetView>();
            target.Configure(collider, highlight, boxes);
            var player = pipelineRoot.GetComponentInChildren<플레이어경관Controller>(true)
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehousePlayerMissing");
            // 첫 1인칭 수직 단위는 창고 진입로에서 시작해 시선만으로
            // Pallet을 바로 발견할 수 있게 한다. 서버 위치 사실은 바꾸지 않는다.
            player.SetPresentationStartPose(new Vector3(15.4f,
                ScenarioHeight(15.4f, 10.25f) + .06f, 10.25f), 0f);
            var shell = UnityEngine.Object
                .FindFirstObjectByType<SimulationWorldShellPresenter>(
                    FindObjectsInactive.Include)
                ?? throw new InvalidOperationException(
                    "DaegwallyeongWarehouseShellMissing");
            var oldPresenter = pipelineRoot
                .GetComponent<대관령L2창고아이템Presenter>();
            if (oldPresenter != null) UnityEngine.Object.DestroyImmediate(oldPresenter);
            var oldComposition = pipelineRoot
                .GetComponent<대관령L2창고아이템CompositionRoot>();
            if (oldComposition != null) UnityEngine.Object.DestroyImmediate(oldComposition);
            var oldInteraction = player.GetComponent<일인칭창고상호작용Controller>();
            if (oldInteraction != null) UnityEngine.Object.DestroyImmediate(oldInteraction);

            var presenter = pipelineRoot.gameObject
                .AddComponent<대관령L2창고아이템Presenter>();
            var settings = AssetDatabase.LoadAssetAtPath<UnityClientRuntimeSettings>(
                "Assets/Ssalddel/Settings/UnityClientRuntimeSettings.asset");
            var composition = pipelineRoot.gameObject
                .AddComponent<대관령L2창고아이템CompositionRoot>();
            // 저장 Scene 검증은 결정적 Simulation Fixture를 사용한다.
            // 같은 조립점의 서버 기준 모드는 실제 Session이 준비된 실행에서만 켠다.
            composition.Configure(settings, shell, presenter, false);
            var interaction = player.gameObject
                .AddComponent<일인칭창고상호작용Controller>();
            interaction.Configure(player, player.FirstPersonCamera,
                presenter, target, 4f);

            // Configure 메서드의 직접 필드 대입도 Binary Scene에 확실히
            // 기록되도록 새 조립 객체를 명시적으로 dirty 처리한다.
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(composition);
            EditorUtility.SetDirty(interaction);

            if (!target.ValidateWiring() || !interaction.ValidateWiring())
                throw new InvalidOperationException(
                    "DaegwallyeongWarehouseInteractionBuildInvalid");
        }

        private static void CompletionGround(
            Transform parent, string name, float centerX, float centerZ, Color color)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = name;
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(
                centerX, ScenarioHeight(centerX, centerZ) - .16f, centerZ);
            ground.transform.localScale = new Vector3(9.5f, .16f, 9.5f);
            var collider = ground.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                EnsureMaterial("CompletionArea_" + name, color, .04f);
        }

        private static 법정동경관VisualInstanceView Visual(
            Transform parent, 법정동경관VisualCatalog catalog, 법정동경관PlacementData placement)
        {
            var linked = new 법정동Synty후단연결기().연결(
                placement, catalog, PhysicalSlopeDegrees(placement.LocalX, placement.LocalZ));
            if (!linked.연결가능여부)
                throw new InvalidOperationException(
                    "LegalDongScenicFinalLinkRejected:" + linked.StatusCode + ":" +
                    placement.PlacementStableId);
            var wrapper = new GameObject(placement.PlacementStableId.Replace(':', '_'));
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.localPosition = new Vector3(
                placement.LocalX,
                ScenarioHeight(placement.LocalX, placement.LocalZ),
                placement.LocalZ);
            wrapper.transform.localRotation = Quaternion.Euler(0f, placement.RotationY, 0f);
            var visualRoot = Child(wrapper.transform, "VisualRoot");
            var instance = PrefabUtility.InstantiatePrefab(linked.Prefab) as GameObject
                ?? throw new InvalidOperationException("LegalDongScenicPrefabInstantiateFailed:" + placement.VisualKey);
            instance.name = "SyntyPrefabInstance";
            instance.transform.SetParent(visualRoot, false);
            instance.transform.localScale = Vector3.one * placement.Scale;
            var view = wrapper.AddComponent<법정동경관VisualInstanceView>();
            view.Configure(placement, catalog, visualRoot, instance);
            if (!view.ValidateWiring())
                throw new InvalidOperationException("LegalDongScenicWiringInvalid:" + placement.PlacementStableId);
            return view;
        }

        private static float PhysicalSlopeDegrees(float x, float z)
        {
            const float sampleDistance = .25f;
            var left = ScenarioHeight(x - sampleDistance, z);
            var right = ScenarioHeight(x + sampleDistance, z);
            var down = ScenarioHeight(x, z - sampleDistance);
            var up = ScenarioHeight(x, z + sampleDistance);
            var dx = (right - left) / (sampleDistance * 2f);
            var dz = (up - down) / (sampleDistance * 2f);
            return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
        }

        private static void BuildFarmHubCorridor(Transform parent, 법정동경관VisualCatalog catalog)
        {
            var points = new[]
            {
                new Vector3(22f, 0f, 12f), new Vector3(19.5f, 0f, 11f),
                new Vector3(17f, 0f, 10.4f), new Vector3(14.5f, 0f, 9.2f),
                new Vector3(12f, 0f, 7.8f), new Vector3(10f, 0f, 6.5f),
            };
            for (var index = 0; index < points.Length - 1; index++)
            {
                var from = points[index];
                var to = points[index + 1];
                var middle = Vector3.Lerp(from, to, .5f);
                var placement = new 법정동경관PlacementData
                {
                    PlacementStableId = "scenic:sim:pyeongchang:corridor-road-" + index,
                    RegionStableId = 평창군법정동WorldFixture.FarmRegionStableId,
                    VisualKey = 법정동경관VisualKeys.RuralRoad,
                    LandCoverCode = 법정동LandCoverCodes.Corridor,
                    RegionRoleCode = 법정동WorldRoleCodes.Farm,
                    LocalX = middle.x,
                    LocalZ = middle.z,
                    RotationY = Quaternion.LookRotation(to - from).eulerAngles.y,
                    Scale = .42f,
                    DensityTier = 1,
                    LodGroup = 1,
                };
                Visual(parent, catalog, placement);
            }
            for (var index = 0; index < 4; index++)
            {
                var placement = new 법정동경관PlacementData
                {
                    PlacementStableId = "scenic:sim:pyeongchang:corridor-fence-" + index,
                    RegionStableId = 평창군법정동WorldFixture.FarmRegionStableId,
                    VisualKey = 법정동경관VisualKeys.Fence,
                    LandCoverCode = 법정동LandCoverCodes.Corridor,
                    RegionRoleCode = 법정동WorldRoleCodes.Farm,
                    LocalX = 20f - index * 2.3f,
                    LocalZ = 12.5f - index * 1.1f,
                    RotationY = 62f,
                    Scale = .62f,
                    DensityTier = 2,
                    LodGroup = 2,
                };
                Visual(parent, catalog, placement);
            }
        }

        private static void BuildHubTownCorridor(
            Transform parent, 법정동경관VisualCatalog catalog)
        {
            var points = new[]
            {
                new Vector3(8f, 0f, 5f), new Vector3(3f, 0f, .5f),
                new Vector3(-2f, 0f, -4f), new Vector3(-7f, 0f, -8f),
                new Vector3(-12f, 0f, -13f), new Vector3(-15f, 0f, -15f),
            };
            for (var index = 0; index < points.Length - 1; index++)
            {
                var from = points[index];
                var to = points[index + 1];
                var middle = Vector3.Lerp(from, to, .5f);
                Visual(parent, catalog, new 법정동경관PlacementData
                {
                    PlacementStableId = "scenic:sim:pyeongchang:hub-town-road-" + index,
                    RegionStableId = 평창군법정동WorldFixture.HubRegionStableId,
                    VisualKey = 법정동경관VisualKeys.RuralRoad,
                    LandCoverCode = 법정동LandCoverCodes.Corridor,
                    RegionRoleCode = 법정동WorldRoleCodes.Hub,
                    LocalX = middle.x,
                    LocalZ = middle.z,
                    RotationY = Quaternion.LookRotation(to - from).eulerAngles.y,
                    Scale = .45f,
                    DensityTier = 1,
                    LodGroup = 1,
                });
            }
        }

        private static void BuildTitle(
            Transform parent, 법정동WorldProjectionData data, 법정동경관PlanData plan)
            => Label(parent, "출처와경계Label",
                "평창군 법정동 경계 · VWorld 2026-07-01\n"
                + "표고·WorldCover 원본 확보 / Scene Mesh는 ScenarioTerrainPreview\n"
                + "Synty 경관 · PresentationOnly · " + plan.RuleRevision,
                new Vector3(-29f, 3f, 18f), new Color(.94f, .97f, 1f), .22f,
                TextAnchor.UpperLeft);

        private static void BuildSimulationVan(Transform parent, 법정동경관VisualCatalog catalog)
        {
            var presenter = UnityEngine.Object.FindAnyObjectByType<물류이동Presenter>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("LegalDongLogisticsPresenterMissing");
            var root = new GameObject("화물차_표현전용_대관령면To진부면");
            root.transform.SetParent(parent, false);
            var vehicle = Child(root.transform, "VehicleRoot");
            var prefab = catalog.Resolve(법정동경관VisualKeys.Van).Prefab;
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject
                ?? throw new InvalidOperationException("LegalDongLogisticsVanInstantiateFailed");
            instance.name = "SyntyVehicleView_SimulationOnly";
            instance.transform.SetParent(vehicle, false);
            instance.transform.localScale = Vector3.one * .65f;
            var route = new[]
            {
                Anchor(root.transform, "FarmPackingAnchor", 22f, 12f),
                Anchor(root.transform, "RuralCorridorAnchor", 17f, 10.4f),
                Anchor(root.transform, "HubEntranceAnchor", 12f, 7.8f),
                Anchor(root.transform, "HubDockAnchor", 9f, 5.5f),
            };
            root.AddComponent<법정동화물운송View>().Configure(presenter, vehicle, route);
        }

        private static void BuildEvidenceCameras(Transform parent)
        {
            var cameras = Child(parent, "배치검증카메라_PresentationOnly");
            EvidenceCamera(cameras, "LegalWorldOverviewCamera",
                new Vector3(0f, 62f, -48f), new Vector3(0f, 0f, 0f), 42f);
            EvidenceCamera(cameras, "LegalWorldFarmCamera",
                new Vector3(38f, 34f, -7f), new Vector3(21f, 1f, 12f), 39f);
            EvidenceCamera(cameras, "LegalWorldCorridorCamera",
                new Vector3(16f, 22f, -9f), new Vector3(16f, 1f, 9f), 32f);
            EvidenceCamera(cameras, "LegalWorldHubCamera",
                new Vector3(9f, 20f, -10f), new Vector3(9f, 1f, 6f), 32f);
            EvidenceCamera(cameras, "LegalWorldTownCamera",
                new Vector3(-15f, 22f, -34f), new Vector3(-15f, 1f, -15f), 32f);
            var firstPerson = EvidenceCamera(cameras, "LegalWorldFarmFirstPersonCamera",
                new Vector3(18.5f, ScenarioHeight(18.5f, 5.8f) + 1.68f, 5.8f),
                new Vector3(24.2f, ScenarioHeight(24.2f, 13.5f) + 1.35f, 13.5f), 62f);
            firstPerson.gameObject.AddComponent<일인칭경관CameraController>();
        }

        private static void BuildRoleNpcGroup(
            Transform parent,
            역할CharacterVisualCatalog catalog)
        {
            catalog.Validate();
            var layer = Child(parent, "L6_역할Character_FarmHubTown_PresentationOnly");

            BuildRoleNpc(layer, catalog,
                "actor:sim:pyeongchang:farm:shipper-01", "화주",
                "화주", WorldActorWorkflowContextCodes.FreightDelivery,
                WorldActorRoleCodes.Shipper,
                WorldActorAppearanceFamilyCodes.AdultB,
                법정동WorldRoleCodes.Farm, 21.2f, 7.1f, 205f);

            BuildRoleNpc(layer, catalog,
                평창진부HubNpc업무행동Fixture.ManagerActorStableId, "Hub 관리자",
                "창고관리자", WorldActorWorkflowContextCodes.Warehouse,
                WorldActorRoleCodes.WarehouseOperator,
                WorldActorAppearanceFamilyCodes.AdultB,
                법정동WorldRoleCodes.Hub, 10.1f, 6.1f, 25f);
            BuildRoleNpc(layer, catalog,
                평창진부HubNpc업무행동Fixture.InboundOperatorActorStableId, "입고 검수 담당",
                "창고관리자", WorldActorWorkflowContextCodes.Warehouse,
                WorldActorRoleCodes.WarehouseOperator,
                WorldActorAppearanceFamilyCodes.AdultA,
                법정동WorldRoleCodes.Hub, 9.2f, 5.1f, 30f);
            BuildRoleNpc(layer, catalog,
                평창진부HubNpc업무행동Fixture.AssistantActorStableId, "물류 보조",
                "창고관리자", WorldActorWorkflowContextCodes.Warehouse,
                WorldActorRoleCodes.WarehouseOperator,
                WorldActorAppearanceFamilyCodes.AdultA,
                법정동WorldRoleCodes.Hub, 10.7f, 4.6f, 20f);
            BuildRoleNpc(layer, catalog,
                "actor:sim:pyeongchang:hub:transport-operator-01", "운송 운영자",
                "보세운송사", WorldActorWorkflowContextCodes.FreightDelivery,
                WorldActorRoleCodes.TransportOperator,
                WorldActorAppearanceFamilyCodes.AdultB,
                법정동WorldRoleCodes.Hub, 6.8f, 5.3f, 15f);
            BuildRoleNpc(layer, catalog,
                "actor:sim:pyeongchang:hub:freight-driver-01", "화물 배달 기사",
                "용달기사", WorldActorWorkflowContextCodes.FreightDelivery,
                WorldActorRoleCodes.FreightDeliveryDriver,
                WorldActorAppearanceFamilyCodes.AdultA,
                법정동WorldRoleCodes.Hub, 8.0f, 3.5f, 40f);

            BuildRoleNpc(layer, catalog,
                "actor:sim:pyeongchang:town:seller-01", "판매자",
                "판매자", WorldActorWorkflowContextCodes.MarketOrder,
                WorldActorRoleCodes.Seller,
                WorldActorAppearanceFamilyCodes.AdultB,
                법정동WorldRoleCodes.Town, -12.4f, -13.2f, 195f);
            BuildRoleNpc(layer, catalog,
                "actor:sim:pyeongchang:town:orderer-01", "주문자",
                "orderer", WorldActorWorkflowContextCodes.MarketOrder,
                WorldActorRoleCodes.Orderer,
                WorldActorAppearanceFamilyCodes.AdultA,
                법정동WorldRoleCodes.Town, -15.1f, -15.5f, 25f);
            BuildRoleNpc(layer, catalog,
                "actor:sim:pyeongchang:town:food-driver-01", "음식 배달 기사",
                "배달기사", WorldActorWorkflowContextCodes.FoodDelivery,
                WorldActorRoleCodes.FoodDeliveryDriver,
                WorldActorAppearanceFamilyCodes.AdultB,
                법정동WorldRoleCodes.Town, -9.7f, -12.1f, 210f);
            BuildNpcWorkActionPresentation(layer);
        }

        private static void BuildNpcWorkActionPresentation(Transform layer)
        {
            var interactionPoint = Child(layer, "상호작용지점_진부Hub_입고검수");
            const float x = 9.6f;
            const float z = 5.7f;
            interactionPoint.position = new Vector3(x, ScenarioHeight(x, z) + .06f, z);

            var actorIds = new[]
            {
                평창진부HubNpc업무행동Fixture.ManagerActorStableId,
                평창진부HubNpc업무행동Fixture.InboundOperatorActorStableId,
                평창진부HubNpc업무행동Fixture.AssistantActorStableId,
            };
            var actorViews = UnityEngine.Object.FindObjectsByType<역할CharacterVisualInstanceView>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(value => actorIds.Contains(
                    value.AppearanceProfile.ActorStableId,
                    StringComparer.Ordinal))
                .OrderBy(value => value.AppearanceProfile.ActorStableId, StringComparer.Ordinal)
                .ToArray();
            if (actorViews.Length != actorIds.Length)
                throw new InvalidOperationException("NpcWorkActionActorViewsMissing");

            var workViews = actorViews.Select(actorView =>
            {
                var adapter = actorView.GetComponent<공용AnimationAdapter>()
                    ?? throw new InvalidOperationException("NpcWorkActionAnimationAdapterMissing");
                var workView = actorView.gameObject.AddComponent<Npc업무행동View>();
                workView.Configure(
                    actorView.AppearanceProfile.ActorStableId,
                    평창진부HubNpc업무행동Fixture.InteractionPointKey,
                    interactionPoint,
                    adapter);
                return workView;
            }).ToArray();
            var presenter = layer.gameObject.AddComponent<Npc업무행동Presenter>();
            presenter.Configure(
                workViews,
                new[] { 평창진부HubNpc업무행동Fixture.Create() });
            if (!presenter.ValidateWiring())
                throw new InvalidOperationException("NpcWorkActionPresenterWiringInvalid");
        }

        private static void BuildRoleNpc(
            Transform parent,
            역할CharacterVisualCatalog catalog,
            string actorStableId,
            string koreanRoleName,
            string sourceRoleCode,
            string workflowContextCode,
            string actorRoleCode,
            string appearanceFamilyCode,
            string areaRoleCode,
            float x,
            float z,
            float rotationY)
        {
            var appearance = Appearance(
                actorStableId, appearanceFamilyCode, explicitlySelected: false);
            var assignment = WorldCharacterAssignmentPolicy.Assign(
                appearance,
                actorRoleCode,
                catalog.CatalogRevision,
                catalog.AssignmentCandidates());
            var entry = catalog.Resolve(assignment.VisualKey);
            if (!entry.AllowedAreaRoleCodes.Contains(areaRoleCode))
                throw new InvalidOperationException(
                    "RoleCharacterAreaNotAllowed:" + actorRoleCode);

            var actor = Child(parent, "역할Actor_" + koreanRoleName);
            actor.position = new Vector3(x, ScenarioHeight(x, z) + .06f, z);
            actor.rotation = Quaternion.Euler(0f, rotationY, 0f);
            var visualRoot = Child(actor, "VisualRoot_역할Character");
            var visual = PrefabUtility.InstantiatePrefab(
                    entry.Prefab, visualRoot) as GameObject
                ?? throw new InvalidOperationException(
                    "RoleCharacterPrefabInstantiateFailed:" + assignment.VisualKey);
            visual.name = "SyntyRoleCharacterVisual_" + koreanRoleName;
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = visual.GetComponentInChildren<Animator>(true)
                ?? throw new InvalidOperationException(
                    "RoleCharacterAnimatorMissing:" + assignment.VisualKey);
            animator.runtimeAnimatorController = null;

            var animationEntry = new 공용AnimationCatalogEntry();
            animationEntry.Configure(
                entry.AnimationPackCode,
                actorRoleCode,
                "locomotion.idle.v1",
                "locomotion.walk.v1",
                공용AnimationSourceKindCodes.ProceduralFallback,
                "humanoid.procedural-locomotion.v1",
                entry.Prefab,
                null,
                null);
            var animationAdapter = actor.gameObject.AddComponent<공용AnimationAdapter>();
            animationAdapter.Configure(animationEntry, animator);
            var instanceView = actor.gameObject.AddComponent<역할CharacterVisualInstanceView>();
            instanceView.Configure(
                appearance,
                assignment,
                areaRoleCode,
                catalog,
                visualRoot,
                visual);
            if (!animationAdapter.ValidateWiring() || !instanceView.ValidateWiring())
                throw new InvalidOperationException(
                    "RoleCharacterNpcWiringInvalid:" + actorRoleCode);
            var switcher = actor.gameObject.AddComponent<역할CharacterVisualSwitcher>();
            switcher.ConfigureExisting(
                appearance,
                sourceRoleCode,
                workflowContextCode,
                areaRoleCode,
                catalog,
                visualRoot,
                visual,
                animationAdapter,
                instanceView);

            var evidenceLabel = Child(actor, "역할표식_검증전용");
            Label(evidenceLabel, "역할명", koreanRoleName,
                new Vector3(0f, 2.25f, 0f), Color.white, .05f);
            evidenceLabel.gameObject.SetActive(false);
        }

        private static WorldActorAppearanceProfile Appearance(
            string actorStableId,
            string familyCode,
            bool explicitlySelected)
            => new()
            {
                ActorStableId = actorStableId,
                SelectedAppearanceFamilyCode = familyCode,
                ExplicitlySelected = explicitlySelected,
                PresentationOnly = true,
            };

        private static void BuildPlayerExplorer(
            Transform parent,
            플레이어경관Profile profile,
            역할CharacterVisualCatalog roleCharacterCatalog)
        {
            if (!profile.Validate() || roleCharacterCatalog == null)
                throw new InvalidOperationException("FarmPlayerProfileInvalid");
            var layer = Child(parent, "L10_플레이어경관탐색_PresentationOnly");
            var player = Child(layer, "LegalWorldFarmPlayer");
            player.position = new Vector3(
                18.5f, ScenarioHeight(18.5f, 5.8f) + .06f, 5.8f);
            player.gameObject.AddComponent<CharacterController>();

            var appearance = Appearance(
                "actor:sim:pyeongchang:farm:farmer-player-01",
                WorldActorAppearanceFamilyCodes.AdultA,
                explicitlySelected: false);
            var assignment = WorldCharacterAssignmentPolicy.Assign(
                appearance,
                WorldActorRoleCodes.FarmerProducer,
                roleCharacterCatalog.CatalogRevision,
                roleCharacterCatalog.AssignmentCandidates());
            var roleEntry = roleCharacterCatalog.Resolve(assignment.VisualKey);
            var visualRoot = Child(player, "VisualRoot_역할Character");
            var visual = PrefabUtility.InstantiatePrefab(
                    roleEntry.Prefab, visualRoot) as GameObject
                ?? throw new InvalidOperationException("FarmPlayerPrefabInstantiateFailed");
            visual.name = "SyntyRoleCharacterVisual_농부플레이어";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = visual.GetComponentInChildren<Animator>(true)
                ?? throw new InvalidOperationException("FarmPlayerAnimatorMissing");
            animator.runtimeAnimatorController = null;
            var animationEntry = new 공용AnimationCatalogEntry();
            animationEntry.Configure(
                roleEntry.AnimationPackCode,
                assignment.ActorRoleCode,
                "locomotion.idle.v1",
                "locomotion.walk.v1",
                공용AnimationSourceKindCodes.ProceduralFallback,
                "humanoid.procedural-locomotion.v1",
                roleEntry.Prefab,
                null,
                null);
            var animationAdapter = player.gameObject.AddComponent<공용AnimationAdapter>();
            animationAdapter.Configure(animationEntry, animator);
            var instanceView = player.gameObject.AddComponent<역할CharacterVisualInstanceView>();
            instanceView.Configure(
                appearance,
                assignment,
                법정동WorldRoleCodes.Farm,
                roleCharacterCatalog,
                visualRoot,
                visual);
            if (!instanceView.ValidateWiring())
                throw new InvalidOperationException("FarmPlayerRoleCharacterWiringInvalid");
            var roleSwitcher = player.gameObject.AddComponent<역할CharacterVisualSwitcher>();
            roleSwitcher.ConfigureExisting(
                appearance,
                "농부",
                WorldActorWorkflowContextCodes.Farm,
                법정동WorldRoleCodes.Farm,
                roleCharacterCatalog,
                visualRoot,
                visual,
                animationAdapter,
                instanceView);

            var firstPersonPivot = Child(player, "FirstPersonPivot");
            firstPersonPivot.localPosition = Vector3.up * profile.FirstPersonEyeHeight;
            var firstPersonCameraRoot = Child(firstPersonPivot, "PlayerFirstPersonCamera");
            var firstPersonCamera = ConfigurePlayerCamera(
                firstPersonCameraRoot, profile.FirstPersonFieldOfView, .05f);

            var thirdPersonPivot = Child(layer, "TacticalCameraPivot");
            thirdPersonPivot.position = player.position + Vector3.up * profile.CameraHeight;
            var thirdPersonCameraRoot = Child(thirdPersonPivot, "PlayerCamera");
            thirdPersonCameraRoot.localPosition =
                new Vector3(0f, 0f, -profile.CameraDistance);
            var thirdPersonCamera = ConfigurePlayerCamera(
                thirdPersonCameraRoot, profile.CameraFieldOfView, .12f);

            var selectionHighlight = BuildPlayerInteractionRing(
                player, "선택강조Ring", .72f, .075f,
                new Color(1f, .76f, .16f, 1f));
            selectionHighlight.transform.localPosition = Vector3.up * .08f;
            selectionHighlight.SetActive(false);
            var destinationMarker = BuildPlayerInteractionRing(
                layer, "우클릭이동목적지Ring", .52f, .06f,
                new Color(.18f, .92f, .95f, 1f));
            destinationMarker.SetActive(false);

            var controller = player.gameObject.AddComponent<플레이어경관Controller>();
            controller.Configure(
                profile,
                firstPersonPivot,
                firstPersonCamera,
                thirdPersonPivot,
                thirdPersonCamera,
                visualRoot,
                animationAdapter,
                selectionHighlight,
                destinationMarker);
            SetLayerRecursively(player.gameObject, LayerMask.NameToLayer("Ignore Raycast"));
            SetLayerRecursively(destinationMarker, LayerMask.NameToLayer("Ignore Raycast"));
            if (!controller.ValidateWiring())
                throw new InvalidOperationException("FarmPlayerWiringInvalid");
        }

        private static Camera ConfigurePlayerCamera(
            Transform cameraRoot, float fieldOfView, float nearClipPlane)
        {
            var camera = cameraRoot.gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = nearClipPlane;
            camera.farClipPlane = 300f;
            camera.allowHDR = true;
            var urp = cameraRoot.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urp.antialiasingQuality = AntialiasingQuality.High;
            return camera;
        }

        private static GameObject BuildPlayerInteractionRing(
            Transform parent, string name, float radius, float width, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var line = root.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 48;
            line.widthMultiplier = width;
            line.sharedMaterial = EnsureMaterial("Player_" + name, color, .3f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            var points = new Vector3[line.positionCount];
            for (var index = 0; index < points.Length; index++)
            {
                var angle = index * Mathf.PI * 2f / points.Length;
                points[index] = new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
            }
            line.SetPositions(points);
            return root;
        }

        private static Camera EvidenceCamera(
            Transform parent, string name, Vector3 position, Vector3 target, float fieldOfView)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.LookAt(target);
            var camera = root.AddComponent<Camera>();
            var urp = root.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urp.antialiasingQuality = AntialiasingQuality.High;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.48f, .65f, .78f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = .12f;
            camera.farClipPlane = 300f;
            camera.allowHDR = true;
            return camera;
        }

        private static void BuildGraphicsQualityPipeline(
            Transform pipelineRoot, 경관RenderingProfile profile)
        {
            if (!profile.Validate())
                throw new InvalidOperationException("LandscapeRenderingProfileInvalid");

            var root = Child(pipelineRoot, "L9_경관품질후처리_PresentationOnly");
            Child(root, "01_영역역할ToRenderingProfile");
            Child(root, "02_태양_환경광_그림자");
            Child(root, "03_안개_하늘_대기원근");
            Child(root, "04_URP색보정_Bloom_Vignette");
            Child(root, "05_카메라모드_Overview_Region_FirstPerson");
            Child(root, "06_HLOD_Rendering예산_후속연결");

            var light = GameObject.Find("GlobalDirectionalLight")?.GetComponent<Light>();
            if (light == null)
            {
                var lightRoot = new GameObject("GlobalDirectionalLight");
                lightRoot.transform.SetParent(root, false);
                light = lightRoot.AddComponent<Light>();
            }
            light.type = LightType.Directional;
            light.color = new Color(
                profile.SunColorR, profile.SunColorG, profile.SunColorB);
            light.intensity = profile.SunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = profile.ShadowStrength;
            light.shadowBias = profile.ShadowBias;
            light.shadowNormalBias = profile.ShadowNormalBias;
            light.transform.rotation = Quaternion.Euler(
                profile.SunPitch, profile.SunYaw, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(
                profile.AmbientSkyR, profile.AmbientSkyG, profile.AmbientSkyB);
            RenderSettings.ambientEquatorColor = new Color(
                profile.AmbientEquatorR,
                profile.AmbientEquatorG,
                profile.AmbientEquatorB);
            RenderSettings.ambientGroundColor = new Color(
                profile.AmbientGroundR,
                profile.AmbientGroundG,
                profile.AmbientGroundB);
            RenderSettings.ambientIntensity = profile.AmbientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(
                profile.FogColorR, profile.FogColorG, profile.FogColorB);
            RenderSettings.fogStartDistance = profile.FogStartDistance;
            RenderSettings.fogEndDistance = profile.FogEndDistance;
            RenderSettings.skybox = EnsureSkyboxMaterial();
            QualitySettings.shadowDistance = profile.ShadowDistance;
            QualitySettings.shadowCascades = profile.ShadowCascadeCount;
            QualitySettings.shadowCascade4Split = new Vector3(
                profile.ShadowCascade1,
                profile.ShadowCascade2,
                profile.ShadowCascade3);
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;

            var volumeRoot = Child(root, "GlobalVolume_농촌맑은낮");
            var volume = volumeRoot.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            volume.sharedProfile = EnsureVolumeProfile(profile);

            var firstPerson = GameObject.Find("LegalWorldFarmFirstPersonCamera")
                ?.GetComponent<Camera>()
                ?? throw new InvalidOperationException("FarmFirstPersonCameraMissing");
            var playerCamera = GameObject.Find("PlayerCamera")?.GetComponent<Camera>()
                ?? throw new InvalidOperationException("FarmPlayerCameraMissing");
            var view = root.gameObject.AddComponent<경관품질PipelineView>();
            view.Configure(profile, volume, firstPerson, playerCamera);
            if (!view.ValidateWiring())
                throw new InvalidOperationException("LandscapeQualityPipelineWiringInvalid");
        }

        private static VolumeProfile EnsureVolumeProfile(경관RenderingProfile source)
        {
            Directory.CreateDirectory(GeneratedRoot);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            profile.components.RemoveAll(item => item == null);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(source.PostExposure);
            color.contrast.Override(source.Contrast);
            color.saturation.Override(source.Saturation);
            color.colorFilter.Override(new Color(
                source.ColorFilterR, source.ColorFilterG, source.ColorFilterB));
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(source.BloomThreshold);
            bloom.intensity.Override(source.BloomIntensity);
            bloom.scatter.Override(source.BloomScatter);
            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(source.VignetteIntensity);
            vignette.smoothness.Override(source.VignetteSmoothness);
            var whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(source.WhiteBalanceTemperature);
            whiteBalance.tint.Override(source.WhiteBalanceTint);
            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            foreach (var component in profile.components)
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component) && component != null)
                return component;
            component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Material EnsureSkyboxMaterial()
        {
            Directory.CreateDirectory(GeneratedRoot);
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Skybox/Procedural")
                    ?? throw new InvalidOperationException("ProceduralSkyboxShaderMissing");
                material = new Material(shader) { name = "대관령맑은낮Skybox" };
                AssetDatabase.CreateAsset(material, SkyboxMaterialPath);
            }
            material.SetColor("_SkyTint", new Color(.45f, .63f, .82f));
            material.SetColor("_GroundColor", new Color(.30f, .35f, .27f));
            material.SetFloat("_AtmosphereThickness", .72f);
            material.SetFloat("_Exposure", 1.08f);
            material.SetFloat("_SunSize", .025f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Anchor(Transform parent, string name, float x, float z)
        {
            var value = Child(parent, name);
            value.position = new Vector3(x, ScenarioHeight(x, z), z);
            return value;
        }

        private static 법정동경관VisualCatalog EnsureCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)!);
            var catalog = AssetDatabase.LoadAssetAtPath<법정동경관VisualCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<법정동경관VisualCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var all = new[] { 법정동WorldRoleCodes.Farm, 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town };
            var forest = new[] { 법정동LandCoverCodes.Forest };
            var forestAndCrop = new[] { 법정동LandCoverCodes.Forest, 법정동LandCoverCodes.Cropland };
            var crop = new[] { 법정동LandCoverCodes.Cropland };
            var water = new[] { 법정동LandCoverCodes.Water };
            var bare = new[] { 법정동LandCoverCodes.BareGround };
            var residential = new[] { 법정동LandCoverCodes.Residential };
            var logistics = new[] { 법정동LandCoverCodes.Logistics };
            var corridor = new[] { 법정동LandCoverCodes.Corridor };
            catalog.Configure("legal-dong-scenic-catalog.v2", new[]
            {
                Entry(법정동경관VisualKeys.MountainSoft,"PolygonNature",Nature+"Terrain/SM_Terrain_Mountain_01.prefab",forest,all,30f,30f,0,0,법정동경관CompositionRoleCodes.Proxy,"nature.mountain",.85f,true),
                Entry(법정동경관VisualKeys.TreePatch,"PolygonNature",자연경관CompositionSetBuilder.PrefabPath(자연경관SetNames.활엽수림군집,월드CompositionVariantCodes.A),forest,all,12f,25f,0,0,법정동경관CompositionRoleCodes.Cluster,"nature.forest.cluster",.9f,true),
                Entry(법정동경관VisualKeys.Tree,"PolygonNature",Nature+"Trees/SM_Tree_Round_01.prefab",forestAndCrop,all,3f,25f,2,2,법정동경관CompositionRoleCodes.Detail,"nature.tree.broadleaf",.65f,false),
                Entry(법정동경관VisualKeys.SoilRows,"PolygonFarm",Farm+"Environments/SM_Env_Dirt_Rows_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},6f,8f,1,1),
                Entry(법정동경관VisualKeys.Potato,"PolygonFarm",Farm+"Plants/SM_Prop_Plant_Potato_01_L.prefab",crop,new[]{법정동WorldRoleCodes.Farm},1f,8f,2,2),
                Entry(법정동경관VisualKeys.Barn,"PolygonFarm",Farm+"Buildings/SM_Bld_Barn_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},9f,8f,0,1),
                Entry(법정동경관VisualKeys.Silo,"PolygonFarm",Farm+"Buildings/SM_Bld_Silo_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},4f,8f,1,1),
                Entry(법정동경관VisualKeys.Farmhouse,"PolygonFarm",Farm+"Buildings/SM_Bld_Farmhouse_01.prefab",residential,new[]{법정동WorldRoleCodes.Farm},8f,8f,0,1),
                Entry(법정동경관VisualKeys.Tractor,"PolygonFarm",Farm+"Vehicles/SM_Veh_Tractor_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},4f,12f,1,2),
                Entry(법정동경관VisualKeys.ProduceStand,"PolygonFarm",Farm+"Buildings/SM_Bld_ProduceStand_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},5f,8f,1,1),
                Entry(법정동경관VisualKeys.RuralRoad,"PolygonFarm",Farm+"Environments/SM_Env_Road_Dirt_Straight_01.prefab",corridor,all,6f,14f,1,1),
                Entry(법정동경관VisualKeys.Fence,"PolygonFarm",Farm+"Props/SM_Prop_Fence_Wood_01.prefab",corridor,new[]{법정동WorldRoleCodes.Farm},4f,20f,2,2),
                Entry(법정동경관VisualKeys.Windmill,"PolygonFarm",Farm+"Props/SM_Prop_Windmill_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},5f,10f,1,1),
                Entry(법정동경관VisualKeys.WaterTower,"PolygonCity",City+"Buildings/SM_Prop_Water_Tower_01.prefab",logistics,new[]{법정동WorldRoleCodes.Hub},5f,8f,1,1),
                Entry(법정동경관VisualKeys.TownHouse,"PolygonTown",Town+"Buildings/Presets/SM_Bld_House_Preset_02.prefab",residential,new[]{법정동WorldRoleCodes.Town},9f,12f,0,1),
                Entry(법정동경관VisualKeys.LogisticsBuilding,"PolygonCity",City+"Buildings/SM_Bld_Station_03.prefab",logistics,new[]{법정동WorldRoleCodes.Hub},12f,6f,0,1),
                Entry(법정동경관VisualKeys.Pallet,"PolygonCity",City+"Props/SM_Prop_Pallet_01.prefab",logistics,new[]{법정동WorldRoleCodes.Hub},2f,8f,2,2),
                Entry(법정동경관VisualKeys.CargoBox,"PolygonCity",City+"Props/SM_Prop_CardboardBox_01.prefab",logistics,new[]{법정동WorldRoleCodes.Hub},1f,8f,2,2),
                Entry(법정동경관VisualKeys.Van,"PolygonCity",City+"Vehicles/SM_Veh_Car_Van_01.prefab",logistics,new[]{법정동WorldRoleCodes.Hub},5f,12f,1,2),
                Entry(법정동경관VisualKeys.Greenhouse,"PolygonFarm",Farm+"Buildings/SM_Bld_Greenhouse_01.prefab",crop,new[]{법정동WorldRoleCodes.Farm},12f,5f,0,1),
                Entry(법정동경관VisualKeys.ConiferTree,"PolygonNature",Nature+"Trees/SM_Tree_Pine_01.prefab",forest,all,4f,30f,1,1,법정동경관CompositionRoleCodes.Cluster,"nature.tree.conifer",.75f,true),
                Entry(법정동경관VisualKeys.Reeds,"PolygonNature",Nature+"Plants/SM_Plant_Reeds_01.prefab",water,all,3f,5f,1,1,법정동경관CompositionRoleCodes.Detail,"nature.water-edge",.6f,false),
                Entry(법정동경관VisualKeys.SmallRocks,"PolygonNature",Nature+"Rocks/SM_Rock_Cluster_Large_01.prefab",bare,all,3f,35f,1,1,법정동경관CompositionRoleCodes.Cluster,"nature.rock.cluster",.55f,true),
                Entry(법정동경관VisualKeys.BroadleafTree,"PolygonNature",Nature+"Trees/SM_Tree_Birch_02.prefab",forest,all,4f,30f,1,1,법정동경관CompositionRoleCodes.Cluster,"nature.tree.broadleaf",.8f,true),
                Entry(법정동경관VisualKeys.MixedTreePatch,"PolygonNature",자연경관CompositionSetBuilder.PrefabPath(자연경관SetNames.혼효림군집,월드CompositionVariantCodes.A),forest,all,8f,30f,0,1,법정동경관CompositionRoleCodes.Cluster,"nature.forest.mixed",.85f,true),
                Entry(법정동경관VisualKeys.Understory,"PolygonNature",Nature+"Plants/SM_Plant_Undergrowth_01.prefab",forest,all,2f,35f,2,2,법정동경관CompositionRoleCodes.Detail,"nature.understory",.5f,false),
                Entry(법정동경관VisualKeys.ForestEdge,"PolygonNature",Nature+"Plants/SM_Plant_Bush_Leaves_01.prefab",forestAndCrop,all,2f,30f,2,2,법정동경관CompositionRoleCodes.Connector,"nature.forest-edge",.6f,false),
                Entry(법정동경관VisualKeys.RockWall,"PolygonNature",Nature+"Rocks/SM_Rock_Wall_01.prefab",bare,all,8f,55f,1,1,법정동경관CompositionRoleCodes.Anchor,"nature.rock-wall",.65f,true),
                Entry(법정동경관VisualKeys.DistantMountain,"PolygonNature",Nature+"Terrain/SM_MountainSkybox_01.prefab",forest,all,35f,45f,0,0,법정동경관CompositionRoleCodes.Proxy,"nature.mountain.distant",.8f,true),
                Entry(법정동경관VisualKeys.RiverSurface,"PolygonNature",Nature+"Terrain/SM_River_Plane_01.prefab",water,all,8f,5f,1,1,법정동경관CompositionRoleCodes.Connector,"nature.river",.7f,true),
                Entry(법정동경관VisualKeys.WindLeavesFx,"PolygonNature",Nature+"FX/FX_Leaves_Green_01.prefab",forest,all,5f,45f,2,2,법정동경관CompositionRoleCodes.Detail,"nature.fx.wind-leaves",.45f,false),
            });
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static 역할CharacterVisualCatalog EnsureRoleCharacterCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RoleCharacterCatalogPath)!);
            var catalog = AssetDatabase.LoadAssetAtPath<역할CharacterVisualCatalog>(
                RoleCharacterCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<역할CharacterVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, RoleCharacterCatalogPath);
            }

            var farmArea = new[] { 법정동WorldRoleCodes.Farm };
            var hubArea = new[] { 법정동WorldRoleCodes.Hub };
            var townArea = new[] { 법정동WorldRoleCodes.Town };
            var allAreas = new[]
            {
                법정동WorldRoleCodes.Farm,
                법정동WorldRoleCodes.Hub,
                법정동WorldRoleCodes.Town,
            };
            var adultA = new[] { WorldActorAppearanceFamilyCodes.AdultA };
            var adultB = new[] { WorldActorAppearanceFamilyCodes.AdultB };
            var seniorA = new[] { WorldActorAppearanceFamilyCodes.AdultSeniorA };
            var allFamilies = WorldActorAppearanceFamilyCodes.All.ToArray();
            var farmer = new[] { WorldActorRoleCodes.FarmerProducer };
            var business = new[]
            {
                WorldActorRoleCodes.Shipper,
                WorldActorRoleCodes.TransportOperator,
                WorldActorRoleCodes.WarehouseOperator,
                WorldActorRoleCodes.Seller,
            };
            var logistics = new[]
            {
                WorldActorRoleCodes.WarehouseOperator,
                WorldActorRoleCodes.FreightDeliveryDriver,
            };
            var community = new[]
            {
                WorldActorRoleCodes.FreightDeliveryDriver,
                WorldActorRoleCodes.FoodDeliveryDriver,
                WorldActorRoleCodes.Orderer,
            };
            var resident = new[]
            {
                WorldActorRoleCodes.Orderer,
                WorldActorRoleCodes.FoodDeliveryDriver,
            };
            var seller = new[] { WorldActorRoleCodes.Seller };

            catalog.Configure("role-character-catalog.pyeongchang.v1", new[]
            {
                RoleEntry(WorldCharacterVisualKeys.FarmWorkerA, "PolygonFarm",
                    월드CompositionPackCodes.Farm,
                    Farm + "Characters/SM_Chr_Farmer_Male_01.prefab",
                    farmer, adultA, farmArea, 4),
                RoleEntry(WorldCharacterVisualKeys.FarmWorkerB, "PolygonFarm",
                    월드CompositionPackCodes.Farm,
                    Farm + "Characters/SM_Chr_Farmer_Female_01.prefab",
                    farmer, adultB, farmArea, 4),
                RoleEntry(WorldCharacterVisualKeys.FarmWorkerSeniorA, "PolygonFarm",
                    월드CompositionPackCodes.Farm,
                    Farm + "Characters/SM_Chr_Farmer_Male_Old_01.prefab",
                    farmer, seniorA, farmArea, 2),

                RoleEntry(WorldCharacterVisualKeys.BusinessOperatorA, "PolygonCity",
                    월드CompositionPackCodes.City,
                    City + "Characters/Character_BusinessMan_Shirt.prefab",
                    business, adultA, allAreas, 4),
                RoleEntry(WorldCharacterVisualKeys.BusinessOperatorB, "PolygonCity",
                    월드CompositionPackCodes.City,
                    City + "Characters/Character_BusinessMan_Suit.prefab",
                    business, adultA, allAreas, 2),
                RoleEntry(WorldCharacterVisualKeys.BusinessOperatorC, "PolygonCity",
                    월드CompositionPackCodes.City,
                    City + "Characters/Character_BusinessWoman.prefab",
                    business, adultB, allAreas, 4),

                RoleEntry(WorldCharacterVisualKeys.LogisticsWorkerA, "PolygonGeneric",
                    월드CompositionPackCodes.RegionalLogisticsHub,
                    Generic + "Characters/SM_Gen_Chr_Jumpsuit_Male_01.prefab",
                    logistics, adultA, hubArea, 4),
                RoleEntry(WorldCharacterVisualKeys.LogisticsWorkerB, "PolygonGeneric",
                    월드CompositionPackCodes.RegionalLogisticsHub,
                    Generic + "Characters/SM_Gen_Chr_Jumpsuit_Female_01.prefab",
                    logistics, adultB, hubArea, 4),

                RoleEntry(WorldCharacterVisualKeys.CommunityCitizenA, "PolygonGeneric",
                    월드CompositionPackCodes.Mixed,
                    Generic + "Characters/SM_Gen_Chr_Street_Male_01.prefab",
                    community, adultA, new[] { 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town }, 3),
                RoleEntry(WorldCharacterVisualKeys.CommunityCitizenB, "PolygonGeneric",
                    월드CompositionPackCodes.Mixed,
                    Generic + "Characters/SM_Gen_Chr_Street_Female_01.prefab",
                    community, adultB, new[] { 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town }, 3),
                RoleEntry(WorldCharacterVisualKeys.CommunityCitizenC, "PolygonGeneric",
                    월드CompositionPackCodes.Mixed,
                    Generic + "Characters/SM_Gen_Chr_Street_Male_02.prefab",
                    community, adultA, new[] { 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town }, 2),
                RoleEntry(WorldCharacterVisualKeys.CommunityCitizenD, "PolygonGeneric",
                    월드CompositionPackCodes.Mixed,
                    Generic + "Characters/SM_Gen_Chr_Street_Female_02.prefab",
                    community, adultB, new[] { 법정동WorldRoleCodes.Hub, 법정동WorldRoleCodes.Town }, 2),

                RoleEntry(WorldCharacterVisualKeys.TownResidentA, "PolygonTown",
                    월드CompositionPackCodes.Town,
                    Town + "Characters/SM_Chr_Father_01.prefab",
                    resident, adultA, townArea, 3),
                RoleEntry(WorldCharacterVisualKeys.TownResidentB, "PolygonTown",
                    월드CompositionPackCodes.Town,
                    Town + "Characters/SM_Chr_Mother_01.prefab",
                    resident, adultB, townArea, 3),
                RoleEntry(WorldCharacterVisualKeys.TownResidentC, "PolygonTown",
                    월드CompositionPackCodes.Town,
                    Town + "Characters/SM_Chr_Father_02.prefab",
                    resident, adultA, townArea, 2),
                RoleEntry(WorldCharacterVisualKeys.TownResidentD, "PolygonTown",
                    월드CompositionPackCodes.Town,
                    Town + "Characters/SM_Chr_Mother_02.prefab",
                    resident, adultB, townArea, 2),
                RoleEntry(WorldCharacterVisualKeys.TownSellerA, "PolygonTown",
                    월드CompositionPackCodes.Town,
                    Town + "Characters/SM_Chr_ShopKeeper_01.prefab",
                    seller, adultB, townArea, 5),

                RoleEntry(WorldCharacterVisualKeys.NeutralAdultA, "PolygonStarter",
                    월드CompositionPackCodes.Mixed,
                    Starter + "Characters/SM_Chr_Male_01.prefab",
                    new[] { WorldActorRoleCodes.Unresolved }, allFamilies, allAreas, 1),
            });
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static 역할CharacterVisualCatalogEntry RoleEntry(
            string visualKey,
            string sourcePack,
            string animationPackCode,
            string prefabPath,
            string[] roles,
            string[] families,
            string[] areas,
            int weight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                ?? throw new InvalidOperationException(
                    "RoleCharacterPrefabMissing:" + prefabPath);
            var skinnedMeshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var triangles = skinnedMeshes.Where(value => value.sharedMesh != null)
                    .Sum(value => value.sharedMesh.triangles.Length / 3)
                + meshFilters.Where(value => value.sharedMesh != null)
                    .Sum(value => value.sharedMesh.triangles.Length / 3);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var entry = new 역할CharacterVisualCatalogEntry();
            entry.Configure(
                visualKey,
                sourcePack,
                animationPackCode,
                prefab,
                roles,
                families,
                areas,
                weight,
                canBePlayer: true,
                triangles,
                renderers.Sum(value => value.sharedMaterials.Length),
                prefab.GetComponentsInChildren<Animator>(true).Length);
            return entry;
        }

        private static 법정동경관VisualCatalogEntry Entry(
            string key, string pack, string path, string[] covers, string[] roles,
            float footprint, float maxSlope, int density, int lod,
            string compositionRole = 법정동경관CompositionRoleCodes.Detail,
            string variantGroup = "",
            float identityWeight = .5f,
            bool hlodEligible = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException("LegalDongScenicPrefabMissing:" + path);
            var value = new 법정동경관VisualCatalogEntry();
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            var triangles = meshFilters.Where(item => item.sharedMesh != null)
                .Sum(item => (long)item.sharedMesh.triangles.Length / 3L);
            var materialSlots = renderers.Sum(item => item.sharedMaterials.Length);
            var shadowCasters = renderers.Count(item =>
                item.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
            var colliders = prefab.GetComponentsInChildren<Collider>(true).Length;
            var animators = prefab.GetComponentsInChildren<Animator>(true).Length;
            value.Configure(key, pack, prefab, covers, roles,
                Vector2.one * footprint, new Vector2(0f, maxSlope), density, lod,
                new[] { "All" }, Mathf.Max(.25f, footprint * .08f),
                colliders > 0 ? 법정동경관CollisionPolicyCodes.PrefabCollider
                    : 법정동경관CollisionPolicyCodes.FootprintOnly,
                triangles, materialSlots, materialSlots, shadowCasters,
                colliders, animators, lod <= 1, true,
                compositionRole,
                string.IsNullOrWhiteSpace(variantGroup) ? key : variantGroup,
                법정동경관ReviewStatusCodes.Active,
                identityWeight,
                hlodEligible);
            return value;
        }

        private static float ScenarioHeight(float x, float z)
            => .18f + (x + 30f) * .022f + (z + 22f) * .012f
                + Mathf.Sin(x * .16f) * .28f + Mathf.Cos(z * .18f) * .22f;

        private static int[] Triangulate(법정동WorldPointData[] points)
        {
            var indices = Enumerable.Range(0, points.Length).ToList();
            // Unity의 XZ 평면에서는 시계 방향 삼각형이 +Y 법선을 만듭니다.
            if (SignedArea(points) > 0f) indices.Reverse();
            var triangles = new List<int>();
            var guard = 0;
            while (indices.Count > 2 && guard++ < points.Length * points.Length)
            {
                var earFound = false;
                for (var index = 0; index < indices.Count; index++)
                {
                    var previous = indices[(index - 1 + indices.Count) % indices.Count];
                    var current = indices[index];
                    var next = indices[(index + 1) % indices.Count];
                    if (!IsConvex(points[previous], points[current], points[next])) continue;
                    if (indices.Any(candidate => candidate != previous && candidate != current
                        && candidate != next && IsInsideTriangle(points[candidate],
                            points[previous], points[current], points[next]))) continue;
                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    indices.RemoveAt(index);
                    earFound = true;
                    break;
                }
                if (!earFound) break;
            }
            if (triangles.Count < 3)
                throw new InvalidOperationException("LegalDongBoundaryTriangulationFailed");
            return triangles.ToArray();
        }

        private static float SignedArea(법정동WorldPointData[] points)
        {
            var area = 0f;
            for (var i = 0; i < points.Length; i++)
            {
                var next = points[(i + 1) % points.Length];
                area += points[i].X * next.Z - next.X * points[i].Z;
            }
            return area * .5f;
        }

        private static bool IsConvex(
            법정동WorldPointData a, 법정동WorldPointData b, 법정동WorldPointData c)
            => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X) > .0001f;

        private static bool IsInsideTriangle(
            법정동WorldPointData p, 법정동WorldPointData a,
            법정동WorldPointData b, 법정동WorldPointData c)
        {
            var d1 = Sign(p, a, b);
            var d2 = Sign(p, b, c);
            var d3 = Sign(p, c, a);
            var negative = d1 < 0f || d2 < 0f || d3 < 0f;
            var positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static float Sign(
            법정동WorldPointData p1, 법정동WorldPointData p2, 법정동WorldPointData p3)
            => (p1.X - p3.X) * (p2.Z - p3.Z)
                - (p2.X - p3.X) * (p1.Z - p3.Z);

        private static Transform Child(Transform parent, string name)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            return value;
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            foreach (Transform child in value.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void SetLegacyLayerActive(Transform worldMap, string name, bool active)
        {
            var layer = worldMap.Find(name);
            if (layer != null) layer.gameObject.SetActive(active);
        }

        private static void Label(
            Transform parent, string name, string text, Vector3 position,
            Color color, float size, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var label = root.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = anchor;
            label.alignment = anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            label.characterSize = size;
            label.fontSize = 42;
            label.color = color;
        }

        private static Material Material(Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.name = "WORLD-LEGAL-2_" + ColorUtility.ToHtmlStringRGB(color);
            return material;
        }

        private static Material EnsureMaterial(
            string name, Color color, float smoothness)
        {
            Directory.CreateDirectory(GeneratedRoot + "/Materials");
            var path = GeneratedRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
