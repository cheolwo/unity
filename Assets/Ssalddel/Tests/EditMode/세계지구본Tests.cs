using System;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.WorldMap;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 세계지구본Tests
    {
        private const string ScenePath = "Assets/Ssalddel/Scenes/SimulationWorldShell.unity";

        [Test] public void 전세계육지폴리곤은원본판본과전영역을검증한다()
        {
            var source=Resources.Load<TextAsset>(세계지구본View.CatalogResourceName);string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(source.bytes)).Replace("-","").ToLowerInvariant();
            var data=JsonUtility.FromJson<세계육지Polygon사본>(Resources.Load<TextAsset>("WorldLandPolygons").text);
            Assert.DoesNotThrow(()=>data.Validate(hash));Assert.That(data.countryCount,Is.EqualTo(177));Assert.That(data.polygonCount,Is.EqualTo(288));
            Assert.That(data.indices.Length/3,Is.EqualTo(63405));Assert.That(data.coordinates.Any(x=>x<=-89.99),Is.True);
            Assert.Throws<InvalidOperationException>(()=>data.Validate("mismatched"));
            data.indices[0]=data.coordinates.Length;Assert.Throws<InvalidOperationException>(()=>data.Validate(hash));
        }

        [Test]
        public void 북극표시각은_오른쪽23점5도이며_자전중유지된다()
        {
            var root = new GameObject("AxialTiltTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.ApplyKoreanPeninsulaHeroPoseForTests(0);
                var visual = root.transform.Find(세계지구본View.GlobeVisualRootName);
                var axis = visual.up;
                Assert.That(Mathf.Atan2(axis.x,axis.y)*Mathf.Rad2Deg, Is.EqualTo(23.5f).Within(.01f));
                Assert.That(view.IsKoreanPeninsulaHeroPoseApplied, Is.True);
                view.ApplyAxialRotationForTests(180);
                Assert.That(Vector3.Angle(axis,visual.up), Is.LessThan(.01f));
                view.ApplyAxialRotationForTests(180);
                Assert.That(view.IsKoreanPeninsulaHeroPoseApplied, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void 동결Catalog는_출처와177개국가를검증한다()
        {
            var catalog = LoadCatalog();

            Assert.DoesNotThrow(catalog.Validate);
            Assert.That(catalog.revision,
                Is.EqualTo("ne-ca96624a56bd+wb-2026-07-13"));
            Assert.That(catalog.countries, Has.Length.EqualTo(177));
            Assert.That(catalog.sources[0].contentHashSha256,
                Is.EqualTo("6866C877D39CBA9C357620878839B336D569F8C662D3CFAB4CB1DBE2D39C977F"));
            Assert.That(catalog.observationPresentationOnly, Is.True);
            Assert.That(catalog.boundaryNotice, Does.Contain("법적"));
            var korea = catalog.FindByStableId("country:kr");
            Assert.That(korea, Is.Not.Null);
            Assert.That(korea!.displayName, Is.EqualTo("대한민국"));
            Assert.That(korea.populationYear, Is.EqualTo("2025"));
            Assert.That(korea.populationValue, Is.EqualTo(51684564));
            Assert.That(korea.maximumDetailCode, Is.EqualTo("AdministrativeArea"));
        }

        [TestCase(37.5665d, 126.9780d, "country:kr")]
        [TestCase(35.6762d, 139.6503d, "country:jp")]
        [TestCase(39.9042d, 116.4074d, "country:cn")]
        [TestCase(40.7128d, -74.0060d, "country:us")]
        public void 대표도시는_올바른국가로판독된다(
            double latitude,
            double longitude,
            string expectedStableId)
        {
            Assert.That(LoadCatalog().FindAt(latitude, longitude)?.stableId,
                Is.EqualTo(expectedStableId));
        }

        [Test]
        public void 대표대양은_국가로판독되지않는다()
        {
            Assert.That(LoadCatalog().FindAt(0d, -140d), Is.Null);
        }

        [Test]
        public void 한반도교류축Catalog는_출처와거점상태를검증한다()
        {
            var asset = Resources.Load<TextAsset>(한반도교류축View.CatalogResourceName);
            Assert.That(asset, Is.Not.Null);
            var catalog = JsonUtility.FromJson<한반도교류축Catalog>(asset!.text);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog!.Validate);
            Assert.That(catalog.sources, Has.Length.EqualTo(5));
            Assert.That(catalog.hubs, Has.Length.EqualTo(8));
            Assert.That(catalog.segments, Has.Length.EqualTo(7));
            Assert.That(catalog.operationalAuthority, Is.False);
            Assert.That(catalog.detailedGeometryAllowed, Is.False);
            Assert.That(catalog.FindHub("corridor-hub:panmun")!.evidenceStateCode,
                Is.EqualTo("AwaitingIdentityReview"));
            Assert.That(catalog.FindHub("corridor-hub:bongdong")!.evidenceStateCode,
                Is.EqualTo("AwaitingIdentityReview"));
        }

        [Test]
        public void 편집모드에서도_저장되지않는지구본미리보기를조립한다()
        {
            var root = new GameObject("WorldGlobeEditorPreviewTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();

                Assert.That(view.CountryCount, Is.EqualTo(177));
                var visual = root.transform.Find(세계지구본View.GlobeVisualRootName);
                var camera = root.transform.Find(세계지구본View.GlobeCameraName);
                Assert.That(visual, Is.Not.Null);
                Assert.That(camera, Is.Not.Null);
                Assert.That(visual!.gameObject.hideFlags & HideFlags.DontSaveInEditor,
                    Is.EqualTo(HideFlags.DontSaveInEditor));
                Assert.That(camera!.gameObject.hideFlags & HideFlags.DontSaveInEditor,
                    Is.EqualTo(HideFlags.DontSaveInEditor));
                Assert.That(view.IsGlobeVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 시작화면은_한반도중심구도를유지하고_정보를접어둔다()
        {
            var root = new GameObject("KoreanPeninsulaHeroPoseTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.InitializeForTests();
                view.ApplyKoreanPeninsulaHeroPoseForTests(10f);

                Assert.That(view.IsKoreanPeninsulaHeroPoseApplied, Is.True);
                Assert.That(view.AreContextPanelsExpanded, Is.False);
                Assert.That(GameObject.Find("KoreanPeninsulaHeroLabel"), Is.Not.Null);
                Assert.That(GameObject.Find("ChinaHeroLabel"), Is.Not.Null);
                Assert.That(GameObject.Find("JapanHeroLabel"), Is.Not.Null);
                Assert.That(view.ShouldAutoRotateForTests(14.99f), Is.False);
                Assert.That(view.ShouldAutoRotateForTests(15f), Is.True);

                view.RegisterInteractionForTests(14.5f);
                Assert.That(view.ShouldAutoRotateForTests(19.49f), Is.False);
                Assert.That(view.ShouldAutoRotateForTests(19.5f), Is.True);

                view.ToggleContextPanelsForTests();
                Assert.That(view.AreContextPanelsExpanded, Is.True);
                view.ToggleContextPanelsForTests();
                Assert.That(view.AreContextPanelsExpanded, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 대한민국을확대하면_삼백밀리초뒤지형미리보기를단계적으로관찰한다()
        {
            var root = new GameObject("PeninsulaSemanticZoomTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.InitializeForTests();
                view.SelectCountryAtForTests(37.5665d, 126.9780d);

                view.SetCameraDistanceForTests(43f);
                Assert.That(view.IsObservationRequestPending, Is.True);
                Assert.That(view.IsLoadingIndicatorVisible, Is.True);
                Assert.That(view.IsPeninsulaVisible, Is.False);
                Assert.That(view.IsTerrainPreviewVisible, Is.False);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Hidden));
                Assert.That(view.AdvanceSemanticZoomForTests(.299f), Is.False);
                Assert.That(view.IsTerrainPreviewVisible, Is.False);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.IsTerrainPreviewVisible, Is.True);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Coastline));
                var terrainView = root.GetComponent<한반도지형미리보기View>();
                Assert.That(terrainView, Is.Not.Null);
                Assert.That(terrainView!.IsCurvedSurface, Is.True);
                Assert.That(terrainView.ReliefVertexCount, Is.GreaterThan(1000));
                Assert.That(terrainView.GeneratedSurfaceTileCount, Is.EqualTo(21));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);
                Assert.That(terrainView.HasCollider, Is.False);
                Assert.That(GameObject.Find("RegionalTerrainBoard"), Is.Null,
                    "기본 확대 경로는 직사각형 지형판을 생성하지 않는다.");
                Assert.That(view.TerrainZoomBlend, Is.GreaterThan(0f));
                Assert.That(view.IsPeninsulaVisible, Is.False,
                    "자동 의미 확대는 기존 교류축 Canvas를 열지 않는다.");
                Assert.That(view.IsLoadingIndicatorVisible, Is.False);
                Assert.That(view.ObservationRequestGeneration, Is.EqualTo(1));

                view.SetCameraDistanceForTests(39f);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Relief));
                Assert.That(view.ObservationRequestGeneration, Is.EqualTo(2));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.IsReliefVisible, Is.False);
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);

                view.SetCameraDistanceForTests(35.5f);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Rivers));
                Assert.That(view.ObservationRequestGeneration, Is.EqualTo(3));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.IsRiverVisible, Is.False);
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);

                view.SetCameraDistanceForTests(33f);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Hubs));
                Assert.That(view.ObservationRequestGeneration, Is.EqualTo(4));
                Assert.That(terrainView.SurfaceDetailLevel, Is.EqualTo(-1));
                Assert.That(terrainView.ActiveSurfaceTileCount, Is.EqualTo(0));
                Assert.That(terrainView.HasOverlappingSurfaceLevels, Is.False);
                Assert.That(view.IsPeninsulaVisible, Is.False);
                Assert.That(view.SemanticTransitionStage,
                    Is.EqualTo(공간의미확대전환단계.Region));
                Assert.That(view.IsSemanticTransitionVisible, Is.True);
                Assert.That(view.SemanticTransitionStableId,
                    Is.EqualTo(사가정저밀도교통Fixture.RegionStableId));

                Assert.That(view.AdvanceSpatialTransitionForTests(), Is.True);
                Assert.That(view.SemanticTransitionStage,
                    Is.EqualTo(공간의미확대전환단계.AdministrativeArea));
                Assert.That(view.SemanticTransitionDiagnosticCode,
                    Is.EqualTo("HistoricalBoundaryCandidate_CurrentPublicationBlocked"));
                Assert.That(view.AdvanceSpatialTransitionForTests(), Is.True);
                Assert.That(view.SemanticTransitionStage,
                    Is.EqualTo(공간의미확대전환단계.StationArea));
                Assert.That(view.CanOpenPreparedDiorama, Is.True);
                Assert.That(view.SemanticTransitionStableId,
                    Is.EqualTo(StationDioramaProfileCatalog.SagajeongStationStableId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 한반도지형은_직사각형판없이_같은지구곡면에서연속표시된다()
        {
            var root = new GameObject("RegionalTerrainCurvedSurfaceTestRoot");
            try
            {
                var globeView = root.AddComponent<세계지구본View>();
                globeView.InitializeForTests();
                var terrainView = root.GetComponent<한반도지형미리보기View>();
                Assert.That(terrainView, Is.Not.Null);

                terrainView!.SetZoomBlend(.2f);
                Assert.That(terrainView.ZoomBlend, Is.EqualTo(.2f).Within(.001f));
                terrainView.SetZoomBlend(.8f);
                Assert.That(terrainView.ZoomBlend, Is.EqualTo(.8f).Within(.001f));

                var previewRoot = root.transform.Find(
                    세계지구본View.GlobeVisualRootName + "/"
                    + 한반도지형미리보기View.RootName);
                Assert.That(previewRoot, Is.Not.Null);
                Assert.That(previewRoot!.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(previewRoot.localScale, Is.EqualTo(Vector3.one));
                var reliefMesh = previewRoot.Find("NaturalEarthReliefLayer")
                    ?.GetComponent<MeshFilter>()?.sharedMesh;
                Assert.That(reliefMesh, Is.Not.Null);
                Assert.That(reliefMesh!.vertices[0].magnitude, Is.GreaterThan(18f));
                Assert.That(reliefMesh.triangles.Length, Is.GreaterThan(0));
                Assert.That(reliefMesh.triangles.Length, Is.LessThan(96 * 72 * 6),
                    "지형 음영은 원본 직사각형 전체가 아니라 KR/KP 육지 셀만 그린다.");
                Assert.That(previewRoot.Find("KoreanPeninsulaSurfaceTiles.L1")
                    ?.childCount, Is.EqualTo(4));
                Assert.That(previewRoot.Find("KoreanPeninsulaSurfaceTiles.L2")
                    ?.childCount, Is.EqualTo(16));
                Assert.That(GameObject.Find("RegionalTerrainBoard"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 표현상태는_지구본_지형_권역_행정동_역세권_디오라마계층을구분한다()
        {
            try
            {
                세계지도표현상태.SetObservationLayer(세계지도표현계층.Globe);
                Assert.That(세계지도표현상태.CurrentLayer,
                    Is.EqualTo(세계지도표현계층.Globe));
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.True);

                세계지도표현상태.SetObservationLayer(세계지도표현계층.RegionalTerrain);
                Assert.That(세계지도표현상태.CurrentLayer,
                    Is.EqualTo(세계지도표현계층.RegionalTerrain));
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.True);

                세계지도표현상태.SetObservationLayer(세계지도표현계층.Region);
                Assert.That(세계지도표현상태.CurrentLayer,
                    Is.EqualTo(세계지도표현계층.Region));
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.True);

                세계지도표현상태.SetObservationLayer(
                    세계지도표현계층.AdministrativeArea);
                Assert.That(세계지도표현상태.CurrentLayer,
                    Is.EqualTo(세계지도표현계층.AdministrativeArea));
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.True);

                세계지도표현상태.SetObservationLayer(세계지도표현계층.StationArea);
                Assert.That(세계지도표현상태.CurrentLayer,
                    Is.EqualTo(세계지도표현계층.StationArea));
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.True);

                세계지도표현상태.SetObservationLayer(세계지도표현계층.Diorama);
                Assert.That(세계지도표현상태.CurrentLayer,
                    Is.EqualTo(세계지도표현계층.Diorama));
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.False);
            }
            finally
            {
                세계지도표현상태.SetObservationLayer(세계지도표현계층.Diorama);
            }
        }

        [Test]
        public void 교류축은_명시적으로선택할때만열리고_도라산카드를관찰한다()
        {
            var root = new GameObject("PeninsulaExplicitCorridorTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.InitializeForTests();
                view.SelectCountryAtForTests(37.5665d, 126.9780d);

                view.SetCameraDistanceForTests(33f);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.IsTerrainPreviewVisible, Is.True);
                Assert.That(view.IsPeninsulaVisible, Is.False);

                var dorasan = view.SelectPeninsulaHubForTests("corridor-hub:dorasan");
                Assert.That(view.IsPeninsulaVisible, Is.True);
                var corridorView = root.GetComponent<한반도교류축View>();
                Assert.That(corridorView, Is.Not.Null);
                Assert.That(corridorView!.ScenarioPresentationOnly, Is.True);
                Assert.That(corridorView.HubCount, Is.EqualTo(8));
                Assert.That(corridorView.SegmentCount, Is.EqualTo(7));
                Assert.That(corridorView.AnimatedConvoyCount, Is.EqualTo(2));
                Assert.That(GameObject.Find("ScenarioBoundary")?.GetComponent<Text>()?.text,
                    Does.Contain("실제 운행/통행/교역량이 아닙니다"));

                Assert.That(dorasan, Is.Not.Null);
                Assert.That(dorasan!.operationStateCode, Is.EqualTo("OperationalDomestic"));
                Assert.That(dorasan.maximumObservationLevel, Is.EqualTo("Z4"));
                Assert.That(dorasan.sourceReceiptStableIds, Has.Length.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 확대축소는_지형미리보기를숨긴다()
        {
            var root = new GameObject("PeninsulaTerrainHideTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.InitializeForTests();
                view.SelectCountryAtForTests(37.5665d, 126.9780d);

                view.SetCameraDistanceForTests(35.5f);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Rivers));

                view.SetCameraDistanceForTests(47f);
                Assert.That(view.IsObservationRequestPending, Is.True);
                Assert.That(view.IsTerrainPreviewVisible, Is.True);
                Assert.That(view.AdvanceSemanticZoomForTests(.299f), Is.False);
                Assert.That(view.IsTerrainPreviewVisible, Is.True);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.IsTerrainPreviewVisible, Is.False);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Hidden));
                Assert.That(view.IsCountryInformationVisible, Is.True);
                Assert.That(view.IsGlobeVisible, Is.True);
                Assert.That(view.SelectedCountry?.stableId, Is.EqualTo("country:kr"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 비대한민국을선택하면_지형미리보기를즉시숨긴다()
        {
            var root = new GameObject("NonKoreaTerrainHideTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.InitializeForTests();

                view.SetCameraDistanceForTests(39f);
                Assert.That(view.AdvanceSemanticZoomForTests(.3f), Is.True);
                Assert.That(view.IsTerrainPreviewVisible, Is.True);

                view.SelectCountryAtForTests(35.6762d, 139.6503d);
                Assert.That(view.SelectedCountry?.stableId, Is.EqualTo("country:jp"));
                Assert.That(view.IsTerrainPreviewVisible, Is.False);
                Assert.That(view.TerrainPreviewStage,
                    Is.EqualTo(한반도지형미리보기단계.Hidden));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 공간관찰요청은_마지막입력뒤삼백밀리초에최종세대만꺼낸다()
        {
            var gate = new 공간관찰요청안정관문();

            gate.입력됨("Z2:KoreanPeninsula", 1f);
            Assert.That(gate.안정요청꺼내기(1.29f, out _), Is.False);

            gate.입력됨("Z2.5:KoreanPeninsulaHubs", 1.2f);
            Assert.That(gate.안정요청꺼내기(1.49f, out _), Is.False);
            Assert.That(gate.안정요청꺼내기(1.5f, out var request), Is.True);
            Assert.That(request.요청Key, Is.EqualTo("Z2.5:KoreanPeninsulaHubs"));
            Assert.That(request.요청세대, Is.EqualTo(1));
            Assert.That(gate.대기중, Is.False);
        }

        [TestCase(37.5665d, 126.9780d)]
        [TestCase(-33.8688d, 151.2093d)]
        [TestCase(0d, -179.5d)]
        public void 위경도와구면좌표는_왕복된다(double latitude, double longitude)
        {
            var point = 세계지구구면좌표.FromLatitudeLongitude(latitude, longitude);
            var result = point.ToLatitudeLongitude();

            Assert.That(result.Latitude, Is.EqualTo(latitude).Within(.000001d));
            Assert.That(result.Longitude, Is.EqualTo(longitude).Within(.000001d));
        }

        [Test]
        public void View는_독립카메라와국가선택상세복귀를조립한다()
        {
            var root = new GameObject("GlobeTestRoot");
            try
            {
                var view = root.AddComponent<세계지구본View>();
                view.InitializeForTests();

                Assert.That(view.CountryCount, Is.EqualTo(177));
                Assert.That(root.transform.Find(세계지구본View.GlobeVisualRootName), Is.Not.Null);
                var camera = root.transform.Find(세계지구본View.GlobeCameraName)
                    ?.GetComponent<Camera>();
                Assert.That(camera, Is.Not.Null);
                Assert.That(camera!.depth, Is.EqualTo(1000f));
                Assert.That(view.IsGlobeVisible, Is.True);
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.False,
                    "편집 미리보기는 런타임 표현 상태를 변경하지 않는다.");

                var landMesh = root.transform
                    .Find(세계지구본View.GlobeVisualRootName + "/LandObservationSurface")
                    ?.GetComponent<MeshFilter>()?.sharedMesh;
                Assert.That(landMesh, Is.Not.Null);
                Assert.That(Vector3.Dot(landMesh!.vertices[0].normalized,
                    landMesh.normals[0].normalized), Is.GreaterThan(.9f));
                Assert.That(view.LandSurfaceVertexCount, Is.GreaterThan(20000),
                    "세계 육지를 원본 윤곽의 공유 꼭짓점 메시로 구성해야 한다.");

                var korea = view.SelectCountryAtForTests(37.5665d, 126.9780d);
                Assert.That(korea?.stableId, Is.EqualTo("country:kr"));
                Assert.That(view.SelectedCountry, Is.SameAs(korea));

                view.ShowKoreaDetailForTests();
                Assert.That(view.IsGlobeVisible, Is.False);
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.False);
                view.ReturnToGlobeForTests();
                Assert.That(view.IsGlobeVisible, Is.True);
                Assert.That(세계지도표현상태.GlobeOverlayVisible, Is.False,
                    "편집 미리보기 복귀도 런타임 표현 상태를 변경하지 않는다.");
                Assert.That(view.SelectedCountry?.stableId, Is.EqualTo("country:kr"));

                Assert.That(view.HasReturnControl, Is.True);
                Assert.That(view.ReturnControlAnchorMinY, Is.EqualTo(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include))
                    if (canvas.name is 세계지구본View.GlobeCanvasName
                        or 세계지구본View.ReturnCanvasName
                        or 한반도교류축View.CanvasName)
                        UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void 공식Scene은_WorldMapRoot아래지구본을한번만가진다()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = Array.Find(scene.GetRootGameObjects(), item =>
                    item.name == "SimulationWorldShell");
                Assert.That(root, Is.Not.Null);
                var globeRoot = root!.transform.Find("WorldMapRoot/GlobeRoot");
                Assert.That(globeRoot, Is.Not.Null);
                Assert.That(globeRoot!.GetComponents<세계지구본View>(), Has.Length.EqualTo(1));
            }
            finally
            {
                if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static 세계지구본Catalog LoadCatalog()
        {
            var asset = Resources.Load<TextAsset>(세계지구본View.CatalogResourceName);
            Assert.That(asset, Is.Not.Null);
            var catalog = JsonUtility.FromJson<세계지구본Catalog>(asset!.text);
            Assert.That(catalog, Is.Not.Null);
            return catalog!;
        }
    }
}
