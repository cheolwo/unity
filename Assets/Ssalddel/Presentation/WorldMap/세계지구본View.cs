using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>
    /// 동결된 공공자료를 구체 지구본과 국가 정보 카드로 표현하는 읽기 전용 View다.
    /// 국가 선택과 카메라 조작은 운영·Simulation 권위를 변경하지 않는다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "세계 국가 관찰 자료를 구체 지구본·선택·정보 패널로 표현한다.",
        Boundary = "국가 경계와 통계 표현은 법적 경계·운영 상태·Simulation 결과를 확정하지 않는다.")]
    public sealed class 세계지구본View : MonoBehaviour
    {
        public const string CatalogResourceName = "WorldGlobeCountryCatalog";
        public const string GlobeVisualRootName = "GlobeVisualRoot";
        public const string GlobeCameraName = "세계 지구본 관찰 카메라";
        public const string GlobeCanvasName = "세계 지구본 정보 Canvas";
        public const string ReturnCanvasName = "지구본 복귀 Canvas";
        public const int GlobeLayer = 30;

        private const float GlobeRadius = 18f;
        private const float InitialCameraDistance = 53f;
        private const float MinimumCameraDistance = 32f;
        private const float MaximumCameraDistance = 78f;
        private const float AutoRotationDegreesPerSecond = 1.25f;
        // 관찰 카메라에서 북극이 오른쪽으로 기운 표시 각도. 공전면 모사는 아니다.
        public const float DisplayAxisTiltDegrees = 23.5f;
        private const float DragDegreesPerPixel = .16f;
        private const float IdleResumeSeconds = 5f;
        private const float ClickMovementThreshold = 8f;
        private const float TerrainCoastlineDistance = 43f;
        private const float TerrainReliefDistance = 39f;
        private const float TerrainRiverDistance = 35.5f;
        private const float TerrainHubDistance = 33f;
        private const float TerrainExitDistance = 46f;
        private const string TerrainRequestPrefix = "KoreanPeninsulaTerrain:";
        private const double KoreanPeninsulaHeroLatitude = 38.5d;
        private const double KoreanPeninsulaHeroLongitude = 127.5d;

        private readonly List<(Canvas Canvas, bool Enabled)> suspendedCanvases = new();
        private readonly List<(Text Label, double Latitude, double Longitude)> heroLabels = new();
        private 세계지구본Catalog catalog;
        private Transform visualRoot;
        private Camera globeCamera;
        private Canvas globeCanvas;
        private Canvas returnCanvas;
        private GameObject informationPanel;
        private GameObject titlePanel;
        private GameObject sourcePanel;
        private Text informationTitle;
        private Text informationBody;
        private Text interactionStatus;
        private Text loadingStatus;
        private Button detailButton;
        private Button peninsulaButton;
        private Button contextPanelButton;
        private Button returnButton;
        private 한반도교류축View peninsulaView;
        private 한반도지형미리보기View terrainPreviewView;
        private 공간의미확대전환View semanticTransitionView;
        private 사가정운영디오라마View openedStationDiorama;
        private bool transitioning;
        private float transitionOpacity;
        private float returnDistance;
        private Quaternion returnRotation;
        public bool IsDioramaTransitioning => transitioning;
        private Transform highlightRoot;
        private Material oceanMaterial;
        private Material landMaterial;
        private Mesh polygonLandMesh;
        private Material borderMaterial;
        private Material highlightMaterial;
        private Material peninsulaContextMaterial;
        private Material neighborContextMaterial;
        private 단일PlayerViewController playerViewController;
        private 세계국가관찰자료 selectedCountry;
        private bool registeredAsRuntimeEnvironment;
        private bool initialized;
        private bool globeVisible;
        private bool dragging;
        private bool movedDuringPress;
        private Vector2 pressPosition;
        private Vector2 previousPointerPosition;
        private float lastInteractionTime;
        private float cameraDistance = InitialCameraDistance;
        private float terrainSseComparisonTargetPixels = float.NaN;
        private int terrainSseComparisonViewportHeight = 1080;
        private readonly 공간관찰요청안정관문 observationRequestGate = new();

        public 세계지구본Catalog Catalog => catalog;
        public 세계국가관찰자료 SelectedCountry => selectedCountry;
        public bool IsGlobeVisible => globeVisible;
        public int CountryCount => catalog?.countries?.Length ?? 0;
        public float CameraDistance => cameraDistance;
        public bool IsPeninsulaVisible => peninsulaView != null && peninsulaView.IsVisible;
        public bool IsTerrainPreviewVisible => terrainPreviewView != null
            && terrainPreviewView.IsVisible;
        public 한반도지형미리보기단계 TerrainPreviewStage => terrainPreviewView?.Stage
            ?? 한반도지형미리보기단계.Hidden;
        public 공간의미확대전환단계 SemanticTransitionStage
            => semanticTransitionView?.CurrentStage ?? 공간의미확대전환단계.Hidden;
        public string SemanticTransitionStableId
            => semanticTransitionView?.CurrentStableId ?? string.Empty;
        public string SemanticTransitionDiagnosticCode
            => semanticTransitionView?.DiagnosticCode ?? "NotInitialized";
        public bool IsSemanticTransitionVisible
            => semanticTransitionView != null && semanticTransitionView.IsVisible;
        public bool CanOpenPreparedDiorama
            => semanticTransitionView != null && semanticTransitionView.CanOpenPreparedDiorama;
        public 한반도교류축Hub SelectedPeninsulaHub => peninsulaView?.SelectedHub;
        public bool IsObservationRequestPending => observationRequestGate.대기중;
        public float TerrainZoomBlend => terrainPreviewView?.ZoomBlend ?? 0f;
        public long ObservationRequestGeneration => observationRequestGate.마지막요청세대;
        public bool IsLoadingIndicatorVisible => loadingStatus != null
            && loadingStatus.gameObject.activeSelf;
        public bool IsCountryInformationVisible => informationPanel != null
            && informationPanel.activeSelf;
        public bool AreContextPanelsExpanded => titlePanel != null
            && titlePanel.activeSelf
            && sourcePanel != null
            && sourcePanel.activeSelf;
        public bool HasReturnControl => returnButton != null;
        public int LandSurfaceVertexCount { get; private set; }
        public float ReturnControlAnchorMinY => returnButton != null
            ? returnButton.GetComponent<RectTransform>().anchorMin.y
            : float.NaN;
        public bool IsKoreanPeninsulaHeroPoseApplied
        {
            get
            {
                if (visualRoot == null) return false;
                var localDirection = ToVector3(KoreanPeninsulaHeroLatitude,
                    KoreanPeninsulaHeroLongitude, 1f).normalized;
                return Vector3.Angle(visualRoot.TransformDirection(localDirection),
                    Vector3.back) < .1f;
            }
        }

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) Initialize();
        }

        private void Start()
        {
            Initialize();
            if (!UnityEngine.Application.isPlaying) return;
            RegisterAsRuntimeEnvironment();
            ShowGlobe();
        }

        private void Update()
        {
            if (regionalPreviewVisible) return;
            if (!UnityEngine.Application.isPlaying || !initialized || !globeVisible || transitioning) return;
            ReadMouseInput();
            ApplyStableSemanticZoom(Time.unscaledTime);
            if (!dragging && !IsTerrainPreviewVisible && ShouldAutoRotate(Time.unscaledTime))
                ApplyAxialRotation(AutoRotationDegreesPerSecond * Time.unscaledDeltaTime);
            UpdateHeroLabels();
        }

        private void OnDestroy()
        {
            if (openedStationDiorama != null) openedStationDiorama.Show(false);
            if (UnityEngine.Application.isPlaying)
                세계지도표현상태.SetGlobeOverlayVisible(false);
            if (registeredAsRuntimeEnvironment && playerViewController != null)
                playerViewController.UnregisterRuntimeEnvironment(transform, this);
            RestoreSuspendedCanvases();
            DestroyGeneratedObject(globeCanvas != null ? globeCanvas.gameObject : null);
            DestroyGeneratedObject(returnCanvas != null ? returnCanvas.gameObject : null);
            DestroyRuntimeMaterial(oceanMaterial);
            DestroyRuntimeMaterial(landMaterial);
            if(polygonLandMesh!=null){if(UnityEngine.Application.isPlaying)Destroy(polygonLandMesh);else DestroyImmediate(polygonLandMesh);}
            DestroyRuntimeMaterial(borderMaterial);
            DestroyRuntimeMaterial(highlightMaterial);
            DestroyRuntimeMaterial(peninsulaContextMaterial);
            DestroyRuntimeMaterial(neighborContextMaterial);
        }

        public void InitializeForTests() => Initialize();

        private bool regionalPreviewVisible;
        public void SetRegionalPreviewVisible(bool visible)
        {
            regionalPreviewVisible=visible;
            globeCamera.enabled=!visible;
            globeCanvas.enabled=!visible;
        }
        public bool OpenRegionalDiorama()
        {
            var opened=TryShowPreparedSagajeongDiorama();
            if(opened)regionalPreviewVisible=false;
            return opened;
        }

        public 세계국가관찰자료 SelectCountryAtForTests(
            double latitude,
            double longitude)
        {
            Initialize();
            var country = catalog.FindAt(latitude, longitude);
            if (country != null) ShowCountry(country);
            return country;
        }

        public void ShowKoreaDetailForTests()
        {
            Initialize();
            ShowKoreaDetail();
        }

        public void ReturnToGlobeForTests()
        {
            Initialize();
            ShowGlobe();
        }

        public void SetCameraDistanceForTests(float value)
        {
            Initialize();
            cameraDistance = Mathf.Clamp(value, MinimumCameraDistance, MaximumCameraDistance);
            ApplyCameraDistance();
            ScheduleSemanticZoom(0f);
        }

        public 한반도지형타일화면오차선택결과 ApplyTerrainSseComparisonForTests(
            float targetSsePixels, int viewportHeightPixels = 1080)
        {
            Initialize();
            terrainSseComparisonTargetPixels = targetSsePixels;
            terrainSseComparisonViewportHeight = viewportHeightPixels;
            return ApplyTerrainSseComparison();
        }

        public void ClearTerrainSseComparisonForTests()
        {
            Initialize();
            terrainSseComparisonTargetPixels = float.NaN;
            terrainSseComparisonViewportHeight = 1080;
            terrainPreviewView.ClearSurfaceDetailComparison();
        }

        public bool AdvanceSemanticZoomForTests(float elapsedSeconds)
        {
            Initialize();
            return ApplyStableSemanticZoom(elapsedSeconds);
        }

        public bool AdvanceSpatialTransitionForTests()
        {
            Initialize();
            return semanticTransitionView.AdvanceForTests();
        }

        public bool RetreatSpatialTransitionForTests()
        {
            Initialize();
            return semanticTransitionView.RetreatForTests();
        }

        public bool OpenPreparedDioramaForTests()
        {
            Initialize();
            return semanticTransitionView.OpenPreparedDioramaForTests();
        }

        public void ReturnFromDetailForTests()
        {
            Initialize();
            ReturnFromDetail();
        }

        public void ApplyKoreanPeninsulaHeroPoseForTests(float inputTime)
        {
            Initialize();
            ApplyKoreanPeninsulaHeroPose(inputTime);
        }

        public bool ShouldAutoRotateForTests(float currentTime)
        {
            Initialize();
            return ShouldAutoRotate(currentTime);
        }

        public void RegisterInteractionForTests(float inputTime)
        {
            Initialize();
            lastInteractionTime = inputTime;
        }

        public void ToggleContextPanelsForTests()
        {
            Initialize();
            ToggleContextPanels();
        }

        public 한반도교류축Hub SelectPeninsulaHubForTests(string researchSubjectId)
        {
            Initialize();
            ShowPeninsulaObservation();
            return peninsulaView.SelectHub(researchSubjectId);
        }

        private void Initialize()
        {
            if (initialized) return;
            var asset = Resources.Load<TextAsset>(CatalogResourceName)
                ?? throw new InvalidOperationException("WorldGlobeCountryCatalogResourceMissing");
            catalog = JsonUtility.FromJson<세계지구본Catalog>(asset.text)
                ?? throw new InvalidOperationException("WorldGlobeCountryCatalogJsonInvalid");
            catalog.Validate();

            visualRoot = Child(transform, GlobeVisualRootName);
            oceanMaterial = CreateMaterial(new Color(.035f, .16f, .29f), false);
            landMaterial = CreateMaterial(new Color(.18f, .50f, .33f), false);
            borderMaterial = CreateMaterial(new Color(.72f, .90f, .92f, .82f), true);
            highlightMaterial = CreateMaterial(new Color(1f, .76f, .20f, 1f), true);
            peninsulaContextMaterial = CreateMaterial(new Color(1f, .84f, .32f, .98f), true);
            neighborContextMaterial = CreateMaterial(new Color(.55f, .82f, .90f, .90f), true);
            BuildGlobeSurface();
            BuildLandPolygons();
            BuildCountryBorders();
            if (UnityEngine.Application.isPlaying) RegisterAsRuntimeEnvironment();
            BuildCamera();
            BuildInterface();
            var graticuleRoot = Child(visualRoot, "GlobeGraticule");
            var graticule = graticuleRoot.GetComponent<지구위경도선View>()
                ?? graticuleRoot.gameObject.AddComponent<지구위경도선View>();
            graticule.Initialize(visualRoot, globeCanvas, globeCamera, GlobeRadius);
#if UNITY_EDITOR
            if (UnityEngine.Application.isPlaying)
            {
                var marine = GetComponent<해양생활연출View>() ?? gameObject.AddComponent<해양생활연출View>();
                marine.Initialize(this, visualRoot, globeCanvas, catalog);
            }
#endif
            peninsulaView = GetComponent<한반도교류축View>()
                ?? gameObject.AddComponent<한반도교류축View>();
            peninsulaView.Initialize();
            peninsulaView.SetRenderCamera(globeCamera);
            terrainPreviewView = GetComponent<한반도지형미리보기View>()
                ?? gameObject.AddComponent<한반도지형미리보기View>();
            terrainPreviewView.Initialize(catalog, globeCanvas, visualRoot);
            semanticTransitionView = GetComponent<공간의미확대전환View>()
                ?? gameObject.AddComponent<공간의미확대전환View>();
            semanticTransitionView.Initialize(globeCanvas, TryShowPreparedSagajeongDiorama);
            initialized = true;
            ShowGlobe();
            if (!UnityEngine.Application.isPlaying) MarkEditorPreviewTransient();
        }

        private void RegisterAsRuntimeEnvironment()
        {
            if (registeredAsRuntimeEnvironment) return;
            playerViewController = GetComponentInParent<단일PlayerViewController>(true);
            if (playerViewController == null) return;
            if (playerViewController.TryRegisterRuntimeEnvironment(
                    transform, this, out var diagnostic))
            {
                registeredAsRuntimeEnvironment = true;
                return;
            }
            throw new InvalidOperationException(
                "WorldGlobeRuntimeEnvironmentRegistrationFailed:" + diagnostic);
        }

        private void BuildGlobeSurface()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "GlobeSurface";
            sphere.transform.SetParent(visualRoot, false);
            sphere.transform.localScale = Vector3.one * GlobeRadius * 2f;
            sphere.layer = GlobeLayer;
            sphere.GetComponent<Renderer>().sharedMaterial = oceanMaterial;
        }

        private void BuildLandPolygons()
        {
            var asset=Resources.Load<TextAsset>("WorldLandPolygons")??throw new InvalidOperationException("WorldLandPolygonsMissing");
            if(asset.bytes.Length>16*1024*1024)throw new InvalidOperationException("WorldLandPolygonsTooLarge");
            string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())
                hash=BitConverter.ToString(sha.ComputeHash(Resources.Load<TextAsset>(CatalogResourceName).bytes)).Replace("-","").ToLowerInvariant();
            var data=JsonUtility.FromJson<세계육지Polygon사본>(asset.text);data.Validate(hash);
            var vertices=new Vector3[data.coordinates.Length/2];var normals=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++){vertices[i]=ToVector3(data.coordinates[i*2+1],data.coordinates[i*2],GlobeRadius+.035f);normals[i]=vertices[i].normalized;}
            // 날짜변경선·극점을 포함해 원본 링 방향과 무관하게 표시한다.
            var triangles=new int[data.indices.Length*2];
            for(int i=0;i<data.indices.Length;i+=3){int t=i*2;triangles[t]=data.indices[i];triangles[t+1]=data.indices[i+1];triangles[t+2]=data.indices[i+2];triangles[t+3]=data.indices[i+2];triangles[t+4]=data.indices[i+1];triangles[t+5]=data.indices[i];}
            var mesh = new Mesh { name = "NaturalEarthCatalogPolygonLand" };
            polygonLandMesh=mesh;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.normals=normals;
            mesh.RecalculateBounds();
            var land = new GameObject("LandObservationSurface",
                typeof(MeshFilter), typeof(MeshRenderer));
            land.transform.SetParent(visualRoot, false);
            land.layer = GlobeLayer;
            land.GetComponent<MeshFilter>().sharedMesh = mesh;
            land.GetComponent<MeshRenderer>().sharedMaterial = landMaterial;
            LandSurfaceVertexCount = mesh.vertexCount;
        }

        private void BuildCountryBorders()
        {
            var root = Child(visualRoot, "CountryBoundaryLines");
            foreach (var country in catalog.countries)
            foreach (var ring in country.rings)
            {
                var material = borderMaterial;
                var width = .035f;
                if (string.Equals(country.iso2, "KR", StringComparison.Ordinal)
                    || string.Equals(country.iso2, "KP", StringComparison.Ordinal))
                {
                    material = peninsulaContextMaterial;
                    width = .075f;
                }
                else if (string.Equals(country.iso2, "CN", StringComparison.Ordinal)
                         || string.Equals(country.iso2, "JP", StringComparison.Ordinal))
                {
                    material = neighborContextMaterial;
                    width = .055f;
                }
                BuildLine(root, $"{country.stableId}.{ring.polygonIndex}", ring,
                    GlobeRadius + .12f, width, material);
            }
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject(GlobeCameraName, typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.layer = GlobeLayer;
            globeCamera = cameraObject.GetComponent<Camera>();
            globeCamera.clearFlags = CameraClearFlags.SolidColor;
            globeCamera.backgroundColor = new Color(.012f, .025f, .06f);
            globeCamera.cullingMask = 1 << GlobeLayer;
            globeCamera.fieldOfView = 38f;
            globeCamera.nearClipPlane = .3f;
            globeCamera.farClipPlane = 220f;
            globeCamera.depth = 1000f;
            ApplyCameraDistance();
        }

        private void BuildInterface()
        {
            globeCanvas = CreateCanvas(GlobeCanvasName, 900);
            titlePanel = Panel(globeCanvas.transform, "GlobeTitlePanel",
                new Vector2(18f, -18f), new Vector2(420f, 116f), false);
            var title = Label(titlePanel.transform, "Title", new Vector2(16f, -12f),
                new Vector2(388f, 38f), 28, TextAnchor.MiddleLeft, Color.white);
            title.text = "세계 공공정보 지구본";
            var guide = Label(titlePanel.transform, "Guide", new Vector2(16f, -53f),
                new Vector2(388f, 50f), 15, TextAnchor.UpperLeft, new Color(.83f, .91f, .96f));
            guide.text = "천천히 자전 · 드래그로 회전 · 휠로 확대\n국가를 클릭하면 출처가 있는 정보를 표시합니다.";

            informationPanel = Panel(globeCanvas.transform, "CountryInformationPanel",
                new Vector2(-18f, -18f), new Vector2(390f, 510f), true);
            informationTitle = Label(informationPanel.transform, "CountryTitle",
                new Vector2(18f, -16f), new Vector2(354f, 44f), 26,
                TextAnchor.MiddleLeft, Color.white);
            informationBody = Label(informationPanel.transform, "CountryBody",
                new Vector2(18f, -70f), new Vector2(354f, 350f), 16,
                TextAnchor.UpperLeft, new Color(.90f, .94f, .96f));
            detailButton = UiButton(informationPanel.transform, "CountryDetailButton",
                "대한민국 자세히 보기", new Vector2(18f, -432f), new Vector2(220f, 48f),
                new Color(.17f, .48f, .72f));
            detailButton.onClick.AddListener(ShowKoreaDetail);
            peninsulaButton = UiButton(informationPanel.transform, "KoreanPeninsulaButton",
                "한반도 교류축 보기", new Vector2(18f, -376f), new Vector2(220f, 48f),
                new Color(.18f, .38f, .58f));
            peninsulaButton.onClick.AddListener(ShowPeninsulaObservation);
            var closeButton = UiButton(informationPanel.transform, "CountryCloseButton",
                "선택 닫기", new Vector2(246f, -432f), new Vector2(126f, 48f),
                new Color(.25f, .31f, .38f));
            closeButton.onClick.AddListener(CloseInformationPanel);
            informationPanel.SetActive(false);

            sourcePanel = Panel(globeCanvas.transform, "DataSourcePanel",
                new Vector2(18f, 18f), new Vector2(760f, 66f), false, true);
            var source = Label(sourcePanel.transform, "Source", new Vector2(14f, -8f),
                new Vector2(732f, 48f), 13, TextAnchor.UpperLeft,
                new Color(.78f, .86f, .91f));
            source.text = "경계: Natural Earth 1:110m · Public Domain · 법적 경계 권위 없음\n"
                + "국가 기본 정보·인구: World Bank · CC BY 4.0 · 기준 연도 개별 표시";
            interactionStatus = Label(globeCanvas.transform, "InteractionStatus",
                new Vector2(456f, -24f), new Vector2(430f, 34f), 15,
                TextAnchor.MiddleCenter, new Color(.83f, .92f, .98f));
            interactionStatus.text = "국가를 선택해 주세요";
            loadingStatus = Label(globeCanvas.transform, "SpatialObservationLoadingStatus",
                new Vector2(1030f, -24f), new Vector2(230f, 34f), 13,
                TextAnchor.MiddleRight, new Color(.78f, .88f, .95f));
            loadingStatus.text = "자료 불러오는 중";
            loadingStatus.gameObject.SetActive(false);
            contextPanelButton = UiButton(globeCanvas.transform, "ContextPanelButton",
                "정보 열기", new Vector2(18f, -18f), new Vector2(122f, 42f),
                new Color(.08f, .22f, .34f));
            contextPanelButton.onClick.AddListener(ToggleContextPanels);
            titlePanel.SetActive(false);
            sourcePanel.SetActive(false);
            BuildHeroLabels();

            returnCanvas = CreateCanvas(ReturnCanvasName, 910);
            returnButton = UiButton(returnCanvas.transform, "ReturnToGlobeButton",
                "지구본으로 돌아가기", new Vector2(-18f, 18f), new Vector2(220f, 48f),
                new Color(.10f, .28f, .48f), true, true);
            returnButton.onClick.AddListener(ReturnFromDetail);
            returnCanvas.enabled = false;
        }

        private void ReadMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var scroll = mouse.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(scroll) > .01f && !PointerOverUi())
            {
                var regional=GetComponent<서울중간공간View>();
                if(scroll>0 && cameraDistance<=MinimumCameraDistance+.01f && regional!=null && regional.CanEnterFromGlobe)
                { regional.ShowOverview();return; }
                // 선택된 준비 지역에 한해, 최대 확대에서 한 번 더 휠을 돌리면 진입한다.
                if (scroll > 0f && cameraDistance <= MinimumCameraDistance + .01f
                    && CanOpenPreparedDiorama && OpenPreparedDioramaForTests()) return;
                cameraDistance = Mathf.Clamp(cameraDistance - scroll * 2.6f,
                    MinimumCameraDistance, MaximumCameraDistance);
                ApplyCameraDistance();
                lastInteractionTime = Time.unscaledTime;
                ScheduleSemanticZoom(lastInteractionTime);
            }

            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame && !PointerOverUi())
            {
                dragging = true;
                movedDuringPress = false;
                pressPosition = position;
                previousPointerPosition = position;
                lastInteractionTime = Time.unscaledTime;
            }

            if (dragging && mouse.leftButton.isPressed)
            {
                var delta = position - previousPointerPosition;
                previousPointerPosition = position;
                if ((position - pressPosition).magnitude >= ClickMovementThreshold)
                    movedDuringPress = true;
                if (delta.sqrMagnitude > .01f)
                {
                    visualRoot.Rotate(Vector3.up, -delta.x * DragDegreesPerPixel, Space.World);
                    visualRoot.Rotate(globeCamera.transform.right,
                        delta.y * DragDegreesPerPixel, Space.World);
                }
            }

            if (!dragging || !mouse.leftButton.wasReleasedThisFrame) return;
            dragging = false;
            lastInteractionTime = Time.unscaledTime;
            if (!movedDuringPress && !PointerOverUi()) SelectAtScreenPoint(position);
        }

        private void SelectAtScreenPoint(Vector2 screenPosition)
        {
            var ray = globeCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 300f, 1 << GlobeLayer))
            {
                interactionStatus.text = "지구 표면을 선택해 주세요";
                return;
            }
            var localDirection = hit.transform.InverseTransformPoint(hit.point).normalized;
            var spherical = new 세계지구구면좌표(
                -localDirection.x, localDirection.y, localDirection.z);
            var coordinate = spherical.ToLatitudeLongitude();
            var country = catalog.FindAt(coordinate.Latitude, coordinate.Longitude);
            if (country == null)
            {
                CloseInformationPanel();
                interactionStatus.text = string.Format(CultureInfo.InvariantCulture,
                    "해양 또는 경계 결손 · {0:0.00}, {1:0.00}",
                    coordinate.Latitude, coordinate.Longitude);
                return;
            }
            ShowCountry(country);
        }

        private void ShowCountry(세계국가관찰자료 country)
        {
            selectedCountry = country;
            informationTitle.text = string.IsNullOrWhiteSpace(country.displayName)
                ? country.englishName : country.displayName;
            var population = country.populationValue >= 0
                ? country.populationValue.ToString("N0", CultureInfo.InvariantCulture)
                    + "명 · " + country.populationYear + "년"
                : "자료 없음";
            informationBody.text =
                $"공식 식별  {country.countryCode} / {country.iso3}\n"
                + $"영문명  {country.englishName}\n"
                + $"대륙·권역  {ValueOrMissing(country.continent)} · {ValueOrMissing(country.region)}\n"
                + $"수도  {ValueOrMissing(country.capitalCity)}\n"
                + $"인구  {population}\n\n"
                + $"자료 가용성  {AvailabilityLabel(country.dataAvailabilityCode)}\n"
                + $"최대 상세 수준  {DetailLabel(country.maximumDetailCode)}\n\n"
                + "경계는 Natural Earth 저해상 관찰 자료입니다.\n"
                + "인구는 World Bank의 국가별 최근 비결측 값입니다.\n"
                + catalog.boundaryNotice;
            var korea = string.Equals(country.iso2, "KR", StringComparison.Ordinal);
            detailButton.interactable = korea;
            peninsulaButton.interactable = korea;
            detailButton.GetComponentInChildren<Text>().text = korea
                ? "대한민국 자세히 보기" : "상세 자료 준비 중";
            peninsulaButton.GetComponentInChildren<Text>().text = korea
                ? "한반도 교류축 보기" : "한반도 자료 준비 중";
            if (!korea)
            {
                peninsulaView?.Hide();
                SetTerrainPreviewStage(한반도지형미리보기단계.Hidden);
                CancelPendingSemanticZoom();
            }
            informationPanel.SetActive(true);
            interactionStatus.text = country.displayName + " 선택 · 읽기 전용";
            ApplyHighlight(country);
        }

        private void ApplyHighlight(세계국가관찰자료 country)
        {
            if (highlightRoot != null) DestroyGeneratedObject(highlightRoot.gameObject);
            highlightRoot = Child(visualRoot, "CountrySelectionHighlight");
            foreach (var ring in country.rings)
                BuildLine(highlightRoot, "Selected." + ring.polygonIndex, ring,
                    GlobeRadius + .19f, .09f, highlightMaterial);
        }

        private void CloseInformationPanel()
        {
            selectedCountry = null;
            peninsulaView?.Hide();
            SetTerrainPreviewStage(한반도지형미리보기단계.Hidden);
            CancelPendingSemanticZoom();
            if (informationPanel != null) informationPanel.SetActive(false);
            if (highlightRoot != null) DestroyGeneratedObject(highlightRoot.gameObject);
            interactionStatus.text = "국가를 선택해 주세요";
        }

        private void ShowKoreaDetail()
        {
            if (selectedCountry == null
                || !string.Equals(selectedCountry.iso2, "KR", StringComparison.Ordinal))
                return;
            peninsulaView?.Hide();
            HideTerrainPreviewImmediately();
            globeVisible = false;
            세계지도표현상태.SetGlobeOverlayVisible(false);
            globeCamera.enabled = false;
            globeCanvas.enabled = false;
            RestoreSuspendedCanvases();
            returnCanvas.enabled = true;
        }

        private bool TryShowPreparedSagajeongDiorama()
        {
            if (!UnityEngine.Application.isPlaying || transitioning || openedStationDiorama != null) return false;
            var station = FindFirstObjectByType<사가정운영디오라마View>(
                FindObjectsInactive.Include);
            if (station == null) return false;
            station.Initialize();
            if (!string.Equals(station.MapStatus, "Ready", StringComparison.Ordinal)
                || station.ModuleHost == null
                || !station.ModuleHost.TryActivate(station.TransitStationStableId))
                return false;

            returnDistance = cameraDistance;
            returnRotation = visualRoot.rotation;
            dragging = false;
            StartCoroutine(TransitionDiorama(station, false));
            return true;
        }

        private IEnumerator TransitionDiorama(사가정운영디오라마View station, bool returning)
        {
            transitioning = true;
            try
            {
                var startDistance = cameraDistance;
                for (var elapsed = 0f; elapsed < .35f; elapsed += Time.unscaledDeltaTime)
                {
                    transitionOpacity = Mathf.SmoothStep(0f, 1f, elapsed / .35f);
                    if (!returning)
                    {
                        cameraDistance = Mathf.Lerp(startDistance, MinimumCameraDistance, transitionOpacity);
                        ApplyCameraDistance();
                    }
                    yield return null;
                }
                transitionOpacity = 1f;
                if (returning) RestoreDioramaOrigin();
                else ActivatePreparedDiorama(station);
                var camera = returning ? null : station.ObservationCamera;
                var targetSize = camera != null ? camera.orthographicSize : 0f;
                for (var elapsed = 0f; elapsed < .45f; elapsed += Time.unscaledDeltaTime)
                {
                    var progress = Mathf.SmoothStep(0f, 1f, elapsed / .45f);
                    transitionOpacity = 1f - progress;
                    if (camera != null) camera.orthographicSize = targetSize * Mathf.Lerp(1.12f, 1f, progress);
                    yield return null;
                }
                if (camera != null) camera.orthographicSize = targetSize;
            }
            finally
            {
                transitionOpacity = 0f;
                transitioning = false;
                lastInteractionTime = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (!transitioning) return;
            var oldColor = GUI.color;
            var oldDepth = GUI.depth;
            GUI.depth = -10000;
            GUI.color = new Color(.09f, .15f, .21f, transitionOpacity);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.depth = oldDepth;
            if (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel)
                Event.current.Use();
        }

        private void ActivatePreparedDiorama(사가정운영디오라마View station)
        {
            peninsulaView?.Hide();
            globeVisible = false;
            globeCamera.enabled = false;
            globeCanvas.enabled = false;
            RestoreSuspendedCanvases();
            station.Show(true);
            station.ShowWhole();
            openedStationDiorama = station;
            세계지도표현상태.SetObservationLayer(세계지도표현계층.Diorama);
            returnButton.GetComponentInChildren<Text>().text = "역세권 선택으로 돌아가기";
            returnCanvas.enabled = true;
        }

        private void ReturnFromDetail()
        {
            if (transitioning) return;
            if (openedStationDiorama == null)
            {
                ShowGlobe();
                return;
            }

            StartCoroutine(TransitionDiorama(openedStationDiorama, true));
        }

        private void RestoreDioramaOrigin()
        {
            openedStationDiorama.Show(false);
            openedStationDiorama = null;
            ShowGlobe();
            cameraDistance = returnDistance;
            visualRoot.rotation = returnRotation;
            ApplyCameraDistance();
            ApplySemanticZoomImmediately();
            semanticTransitionView.RestoreStationArea();
            returnButton.GetComponentInChildren<Text>().text = "지구본으로 돌아가기";
        }

        private void ShowGlobe()
        {
            if (UnityEngine.Application.isPlaying) SuspendOtherScreenCanvases();
            globeVisible = true;
            if (UnityEngine.Application.isPlaying)
                세계지도표현상태.SetObservationLayer(세계지도표현계층.Globe);
            globeCamera.enabled = true;
            globeCanvas.enabled = true;
            returnCanvas.enabled = false;
            ApplyKoreanPeninsulaHeroPose(Time.unscaledTime);
            ApplyCameraDistance();
            ApplySemanticZoomImmediately();
        }

        private void MarkEditorPreviewTransient()
        {
            MarkHierarchyTransient(visualRoot != null ? visualRoot.gameObject : null);
            MarkHierarchyTransient(globeCamera != null ? globeCamera.gameObject : null);
            MarkHierarchyTransient(globeCanvas != null ? globeCanvas.gameObject : null);
            MarkHierarchyTransient(returnCanvas != null ? returnCanvas.gameObject : null);
            if (peninsulaView != null)
                peninsulaView.hideFlags |= HideFlags.DontSaveInEditor;
            terrainPreviewView?.MarkEditorPreviewTransient();
            var corridorCanvas = GameObject.Find(한반도교류축View.CanvasName);
            MarkHierarchyTransient(corridorCanvas);
        }

        private static void MarkHierarchyTransient(GameObject root)
        {
            if (root == null) return;
            root.hideFlags |= HideFlags.DontSaveInEditor;
            foreach (Transform child in root.transform)
                MarkHierarchyTransient(child.gameObject);
        }

        private static void DestroyGeneratedObject(GameObject value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void ApplyKoreanPeninsulaHeroPose(float inputTime)
        {
            FocusCoordinate(KoreanPeninsulaHeroLatitude, KoreanPeninsulaHeroLongitude);
            cameraDistance = InitialCameraDistance;
            lastInteractionTime = inputTime;
            dragging = false;
            UpdateHeroLabels();
        }

        private bool ShouldAutoRotate(float currentTime) =>
            currentTime - lastInteractionTime >= IdleResumeSeconds;

        private void ToggleContextPanels()
        {
            var expanded = !AreContextPanelsExpanded;
            titlePanel.SetActive(expanded);
            sourcePanel.SetActive(expanded);
            contextPanelButton.GetComponentInChildren<Text>().text = expanded
                ? "정보 접기"
                : "정보 열기";
            contextPanelButton.GetComponent<RectTransform>().anchoredPosition = expanded
                ? new Vector2(18f, -142f)
                : new Vector2(18f, -18f);
        }

        private void BuildHeroLabels()
        {
            heroLabels.Clear();
            AddHeroLabel("KoreanPeninsulaHeroLabel", "한반도", 38.5d, 127.5d,
                new Color(1f, .86f, .38f));
            AddHeroLabel("ChinaHeroLabel", "중국", 35d, 115d,
                new Color(.72f, .88f, .94f));
            AddHeroLabel("JapanHeroLabel", "일본", 37d, 138d,
                new Color(.72f, .88f, .94f));
        }

        private void AddHeroLabel(string name, string value, double latitude,
            double longitude, Color color)
        {
            var label = Label(globeCanvas.transform, name, Vector2.zero,
                new Vector2(120f, 34f), 18, TextAnchor.MiddleCenter, color);
            label.text = value;
            heroLabels.Add((label, latitude, longitude));
        }

        private void UpdateHeroLabels()
        {
            if (globeCamera == null || visualRoot == null) return;
            foreach (var entry in heroLabels)
            {
                var worldPoint = visualRoot.TransformPoint(ToVector3(entry.Latitude,
                    entry.Longitude, GlobeRadius + .55f));
                var screenPoint = globeCamera.WorldToScreenPoint(worldPoint);
                var visible = globeVisible && !IsTerrainPreviewVisible && screenPoint.z > 0f
                    && screenPoint.x >= 0f && screenPoint.x <= Screen.width
                    && screenPoint.y >= 0f && screenPoint.y <= Screen.height;
                entry.Label.gameObject.SetActive(visible);
                if (visible) entry.Label.rectTransform.position = screenPoint;
            }
        }

        private void ShowPeninsulaObservation()
        {
            if (selectedCountry == null
                || !string.Equals(selectedCountry.iso2, "KR", StringComparison.Ordinal))
                return;
            SetTerrainPreviewStage(한반도지형미리보기단계.Hidden);
            peninsulaView?.Show();
            if (informationPanel != null) informationPanel.SetActive(false);
            CancelPendingSemanticZoom();
            interactionStatus.text = "한반도 서부 교류축 · 자료 상태 관찰";
        }

        private void ScheduleSemanticZoom(float inputTime)
        {
            if (!globeVisible || terrainPreviewView == null) return;
            var targetStage = DetermineTerrainPreviewStage();
            if (targetStage == terrainPreviewView.Stage)
            {
                CancelPendingSemanticZoom();
                return;
            }

            observationRequestGate.입력됨(TerrainRequestPrefix + targetStage, inputTime);
            SetLoadingIndicator(true);
        }

        private bool ApplyStableSemanticZoom(float currentTime)
        {
            if (!observationRequestGate.안정요청꺼내기(currentTime, out var request))
                return false;

            SetLoadingIndicator(false);
            if (!request.요청Key.StartsWith(TerrainRequestPrefix,
                    StringComparison.Ordinal)
                || !Enum.TryParse(request.요청Key[TerrainRequestPrefix.Length..],
                    out 한반도지형미리보기단계 stage))
                return false;

            SetTerrainPreviewStage(stage);
            return true;
        }

        private void ApplySemanticZoomImmediately()
        {
            CancelPendingSemanticZoom();
            if (!globeVisible || terrainPreviewView == null) return;
            SetTerrainPreviewStage(DetermineTerrainPreviewStage());
        }

        private 한반도지형미리보기단계 DetermineTerrainPreviewStage()
        {
            if (!KoreanPeninsulaMayBeObserved())
                return 한반도지형미리보기단계.Hidden;
            if (cameraDistance <= TerrainHubDistance)
                return 한반도지형미리보기단계.Hubs;
            if (cameraDistance <= TerrainRiverDistance)
                return 한반도지형미리보기단계.Rivers;
            if (cameraDistance <= TerrainReliefDistance)
                return 한반도지형미리보기단계.Relief;
            if (cameraDistance <= TerrainCoastlineDistance)
                return 한반도지형미리보기단계.Coastline;
            if (cameraDistance >= TerrainExitDistance)
                return 한반도지형미리보기단계.Hidden;
            return terrainPreviewView?.Stage ?? 한반도지형미리보기단계.Hidden;
        }

        private bool KoreanPeninsulaMayBeObserved()
        {
            if (selectedCountry != null)
                return string.Equals(selectedCountry.iso2, "KR", StringComparison.Ordinal);
            return IsKoreanPeninsulaHeroPoseApplied;
        }

        private void SetTerrainPreviewStage(한반도지형미리보기단계 stage)
        {
            if (terrainPreviewView == null) return;
            var terrainVisible = stage != 한반도지형미리보기단계.Hidden;
            terrainPreviewView.SetZoomBlend(terrainVisible
                ? CalculateTerrainZoomBlend()
                : 0f);
            terrainPreviewView.SetStage(stage);
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            if (UnityEngine.Application.isPlaying && globeVisible)
                세계지도표현상태.SetObservationLayer(terrainVisible
                    ? 세계지도표현계층.RegionalTerrain
                    : 세계지도표현계층.Globe);
            semanticTransitionView?.SetTerrainStage(stage);
            if (informationPanel != null)
                informationPanel.SetActive(!terrainVisible && selectedCountry != null);
            if (interactionStatus != null)
                interactionStatus.text = TerrainStageStatus(stage);
            UpdateHeroLabels();
        }

        private float CalculateTerrainZoomBlend()
        {
            if (!KoreanPeninsulaMayBeObserved()) return 0f;
            return Mathf.InverseLerp(TerrainExitDistance, MinimumCameraDistance,
                cameraDistance);
        }

        private void HideTerrainPreviewImmediately()
        {
            if (terrainPreviewView == null) return;
            terrainPreviewView.SetZoomBlend(0f);
            terrainPreviewView.SetStage(한반도지형미리보기단계.Hidden);
            semanticTransitionView?.SetTerrainStage(한반도지형미리보기단계.Hidden);
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            CancelPendingSemanticZoom();
        }

        private string TerrainStageStatus(한반도지형미리보기단계 stage)
        {
            if (stage == 한반도지형미리보기단계.Hidden)
                return selectedCountry != null
                    ? selectedCountry.displayName + " 선택 · 읽기 전용"
                    : "한반도를 중심으로 확대하면 지형 자료층이 열립니다";
            return stage switch
            {
                한반도지형미리보기단계.Coastline => "한반도 해안 윤곽 · 관찰용",
                한반도지형미리보기단계.Relief => "한반도 음영 지형 · 정밀 고도 아님",
                한반도지형미리보기단계.Rivers => "검증 하천 3개 · 대동강 자료 없음",
                한반도지형미리보기단계.Hubs => "거점 자료 결속 준비 · 임의 좌표 없음",
                _ => "한반도 지형 관찰 · 읽기 전용",
            };
        }

        private void CancelPendingSemanticZoom()
        {
            observationRequestGate.취소();
            SetLoadingIndicator(false);
        }

        private void SetLoadingIndicator(bool visible)
        {
            if (loadingStatus != null) loadingStatus.gameObject.SetActive(visible);
        }

        private void SuspendOtherScreenCanvases()
        {
            if (suspendedCanvases.Count > 0) return;
            foreach (var canvas in FindObjectsByType<Canvas>(
                         FindObjectsInactive.Exclude))
            {
                if (canvas == null || canvas == globeCanvas || canvas == returnCanvas
                    || canvas.renderMode == RenderMode.WorldSpace)
                    continue;
                suspendedCanvases.Add((canvas, canvas.enabled));
                canvas.enabled = false;
            }
        }

        private void RestoreSuspendedCanvases()
        {
            foreach (var entry in suspendedCanvases)
                if (entry.Canvas != null) entry.Canvas.enabled = entry.Enabled;
            suspendedCanvases.Clear();
        }

        private void FocusCountry(세계국가관찰자료 country)
        {
            if (country == null) return;
            FocusCoordinate(country.centerLatitude, country.centerLongitude);
        }

        private void FocusCoordinate(double latitude, double longitude)
        {
            var direction = ToVector3(latitude, longitude, 1f).normalized;
            var north = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
            if (north.sqrMagnitude < .01f) north = Vector3.forward;
            var sourceFrame = Quaternion.LookRotation(direction, north);
            var targetFrame = Quaternion.LookRotation(Vector3.back, Vector3.up);
            visualRoot.rotation = Quaternion.AngleAxis(-DisplayAxisTiltDegrees, Vector3.forward)
                * targetFrame * Quaternion.Inverse(sourceFrame);
        }

        private void ApplyAxialRotation(float degrees)
        {
            // 로컬 북극축을 보존한다. 월드 Y 회전은 북극 자체를 회전시켜 기울기가 뒤집혀 보인다.
            visualRoot.Rotate(Vector3.up, degrees, Space.Self);
        }

        public void ApplyAxialRotationForTests(float degrees)
        {
            Initialize();
            ApplyAxialRotation(degrees);
        }

        private void ApplyCameraDistance()
        {
            if (globeCamera == null) return;
            globeCamera.transform.position = new Vector3(0f, 0f, -cameraDistance);
            globeCamera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            if (terrainPreviewView != null && terrainPreviewView.IsVisible)
                terrainPreviewView.SetZoomBlend(CalculateTerrainZoomBlend());
            if (terrainPreviewView != null
                && !float.IsNaN(terrainSseComparisonTargetPixels))
                ApplyTerrainSseComparison();
        }

        private 한반도지형타일화면오차선택결과 ApplyTerrainSseComparison()
        {
            var selection = 한반도지형타일화면오차Selector.Select(
                terrainSseComparisonTargetPixels,
                terrainSseComparisonViewportHeight,
                globeCamera.fieldOfView,
                cameraDistance,
                GlobeRadius,
                KoreanPeninsulaHeroLatitude);
            terrainPreviewView.ApplySurfaceDetailComparison(selection);
            return selection;
        }

        private static LineRenderer BuildLine(
            Transform parent,
            string name,
            세계국가경계Ring ring,
            float radius,
            float width,
            Material material)
        {
            var lineObject = new GameObject(name, typeof(LineRenderer));
            lineObject.transform.SetParent(parent, false);
            lineObject.layer = GlobeLayer;
            var line = lineObject.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            var positions=new List<Vector3>();
            for(int index=0;index<ring.points.Length;index++)
            {
                var p=ring.points[index];var current=ToVector3(p.latitude,p.longitude,radius);
                if(index>0){var previous=positions[positions.Count-1];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Angle(previous,current)/.5f));
                    for(int step=1;step<steps;step++)positions.Add(Vector3.Slerp(previous,current,(float)step/steps).normalized*radius);}
                positions.Add(current);
            }
            line.positionCount=positions.Count;line.SetPositions(positions.ToArray());
            return line;
        }

        private static Vector3 ToVector3(double latitude, double longitude, float radius)
        {
            var point = 세계지구구면좌표.FromLatitudeLongitude(latitude, longitude);
            // Unity 관찰 카메라에서 동쪽이 화면 오른쪽에 놓이도록 경도 축만 반전한다.
            // 화면 선택 시에는 같은 축을 다시 반전해 원래 위경도로 복원한다.
            return new Vector3(-(float)point.X, (float)point.Y, (float)point.Z) * radius;
        }

        private static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.layer = GlobeLayer;
            return item.transform;
        }

        private static Material CreateMaterial(Color color, bool transparent)
        {
            var shader = Shader.Find(transparent
                    ? "Universal Render Pipeline/Unlit"
                    : "Universal Render Pipeline/Lit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard")
                ?? throw new InvalidOperationException("WorldGlobeShaderMissing");
            var material = new Material(shader) { color = color };
            if (!transparent || color.a >= .999f) return material;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = item.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = item.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private static GameObject Panel(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            bool rightAnchored,
            bool bottomAnchored = false)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            var anchorX = rightAnchored ? 1f : 0f;
            var anchorY = bottomAnchored ? 0f : 1f;
            rect.anchorMin = new Vector2(anchorX, anchorY);
            rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.pivot = new Vector2(anchorX, anchorY);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = new Color(.025f, .065f, .11f, .91f);
            return item;
        }

        private static Text Label(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button UiButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color,
            bool rightAnchored = false,
            bool bottomAnchored = false)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            var anchorX = rightAnchored ? 1f : 0f;
            var anchorY = bottomAnchored ? 0f : 1f;
            rect.anchorMin = new Vector2(anchorX, anchorY);
            rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.pivot = new Vector2(anchorX, anchorY);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            var button = item.GetComponent<Button>();
            var text = Label(item.transform, "Label", Vector2.zero, size, 16,
                TextAnchor.MiddleCenter, Color.white);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            text.text = label;
            return button;
        }

        private static bool PointerOverUi()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private static string ValueOrMissing(string value)
            => string.IsNullOrWhiteSpace(value) ? "자료 없음" : value;

        private static string AvailabilityLabel(string code)
            => code switch
            {
                "AdministrativeAreaReady" => "대한민국 행정 상세 준비",
                "CountryOverview" => "국가 기본 현황",
                "BoundaryOnly" => "국가 경계만 준비",
                _ => "자료 상태 미확정",
            };

        private static string DetailLabel(string code)
            => code switch
            {
                "AdministrativeArea" => "행정 구역·사가정 상세 창",
                "Country" => "국가 개요",
                _ => "상세 자료 없음",
            };

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
