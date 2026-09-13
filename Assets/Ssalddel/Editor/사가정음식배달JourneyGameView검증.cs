using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Editor
{
    /// <summary>
    /// 저장 Scene을 바꾸지 않고 Play Mode에서만 음식 배달 이동 표현을 검증한다.
    /// 이 표본은 서버 권위나 실제 길찾기 결과가 아니며 배달 Command를 만들지 않는다.
    /// </summary>
    public static class 사가정음식배달JourneyGameView검증
    {
        public const string RuntimeRootName =
            "사가정 음식 배달 Game View 검증 · runtime only";

        [MenuItem("Ssalddel/검증/사가정 음식 배달/1 자동 재생")]
        public static void 자동재생()
            => EnsureDriver().SetStage(사가정음식배달Journey검증Stage.Automatic);

        [MenuItem("Ssalddel/검증/사가정 음식 배달/2 전체 보기 고정")]
        public static void 전체보기()
            => EnsureDriver().SetStage(사가정음식배달Journey검증Stage.Overview);

        [MenuItem("Ssalddel/검증/사가정 음식 배달/3 오토바이 확대 고정")]
        public static void 오토바이확대()
            => EnsureDriver().SetStage(사가정음식배달Journey검증Stage.MotorcycleClose);

        [MenuItem("Ssalddel/검증/사가정 음식 배달/4 도보 인계 확대 고정")]
        public static void 도보인계확대()
            => EnsureDriver().SetStage(사가정음식배달Journey검증Stage.PedestrianClose);

        [MenuItem("Ssalddel/검증/사가정 음식 배달/5 증거 사진 3장 캡처")]
        public static void 증거사진캡처()
            => EnsureDriver().BeginEvidenceCapture();

        [MenuItem("Ssalddel/검증/사가정 음식 배달/9 임시 검증 객체 제거")]
        public static void 정리()
        {
            var root = GameObject.Find(RuntimeRootName);
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        private static 사가정음식배달JourneyGameView검증Driver EnsureDriver()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "SagajeongFoodDeliveryGameViewValidationRequiresPlayMode");

            var existing = Resources.FindObjectsOfTypeAll<사가정음식배달JourneyGameView검증Driver>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());
            if (existing != null) return existing;

            var view = Resources.FindObjectsOfTypeAll<사가정운영디오라마View>()
                .FirstOrDefault(value => value != null && value.gameObject.scene.IsValid());
            if (view == null)
                throw new InvalidOperationException("SagajeongOperationalDioramaViewMissing");

            view.Initialize();
            if (view.ObservationCamera == null || view.MapStatus != "Ready")
                throw new InvalidOperationException("SagajeongOperationalDioramaNotReady");

            var root = new GameObject(RuntimeRootName)
            {
                layer = 사가정운영디오라마View.ObservationLayer,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            root.transform.SetParent(view.transform, false);
            var driver = root.AddComponent<사가정음식배달JourneyGameView검증Driver>();
            driver.Initialize(view);
            Debug.Log("사가정 음식 배달 Game View 검증: Play Mode 임시 표현을 준비했습니다. "
                      + "SyntheticFixture / 서버 Command 없음 / Scene 저장 없음");
            return driver;
        }
    }

    public enum 사가정음식배달Journey검증Stage
    {
        Automatic,
        Overview,
        MotorcycleClose,
        PedestrianClose,
    }

    [DisallowMultipleComponent]
    public sealed class 사가정음식배달JourneyGameView검증Driver : MonoBehaviour
    {
        public const string RegionStableId =
            "world-region:kr:seoul:jungnang:sagajeong.r1";
        public const string AdministrativeAreaStableId = "region:kr:hjd:1126057500";
        public const string GraphRevision = "synthetic-spatial.r1";
        public const string GraphHash =
            "56EA63D55FC720B2348B7A310B0FF4DBBE96F41C392EDA6E270CD87B9BFEAECF";
        public const string RouteStableId =
            "route:synthetic:sagajeong-food-delivery:a";
        public const string ExpectedRouteFingerprint =
            "720664785E1CE71ECB7D492ED5903514FA5D1A6237D91896187C99385146B88A";

        private const float AutomaticSpeedMetersPerSecond = 5f;
        private const float CloseOrthographicSize = 24f;
        // 서버 합성 경로의 30x24m 범위를 사가정역 기준점 주위에 가운데 정렬한다.
        // 이 표현 변환은 실제 OSM 길찾기 결속이 아니다.
        private const float MobilityRootX = 535f;
        private const float MobilityRootY = 2.6f;
        private const float MobilityRootZ = -4f;

        private static readonly LegDefinition[] Definitions =
        {
            Leg("leg:synthetic:depot-restaurant-drive",
                음식배달JourneyWireCodes.Motorcycle, P(0, 0), P(10, 0)),
            Leg("leg:synthetic:restaurant-enter",
                음식배달JourneyWireCodes.Pedestrian, P(10, 0), P(10, 4)),
            Leg("leg:synthetic:restaurant-exit",
                음식배달JourneyWireCodes.Pedestrian, P(10, 4), P(10, 0)),
            Leg("leg:synthetic:restaurant-destination-drive",
                음식배달JourneyWireCodes.Motorcycle,
                P(10, 0), P(20, 0), P(20, 20), P(30, 20)),
            Leg("leg:synthetic:destination-enter",
                음식배달JourneyWireCodes.Pedestrian, P(30, 20), P(30, 24)),
            Leg("leg:synthetic:destination-exit",
                음식배달JourneyWireCodes.Pedestrian, P(30, 24), P(30, 20)),
            Leg("leg:synthetic:return-depot",
                음식배달JourneyWireCodes.Motorcycle,
                P(30, 20), P(20, 20), P(20, 0), P(10, 0), P(0, 0)),
        };

        private readonly 음식배달JourneyInterpreter interpreter = new();
        private readonly List<UnityEngine.Object> owned = new();
        private readonly List<CameraState> cameraStates = new();
        private readonly List<CanvasState> canvasStates = new();
        private readonly List<BehaviourState> overlayBehaviourStates = new();
        private 사가정운영디오라마View view = null!;
        private 음식배달JourneyPlaybackLayer playback = null!;
        private Transform mobilityRoot = null!;
        private Transform landmarkRoot = null!;
        private 음식배달JourneyProjectionContext context = null!;
        private bool previousGameOverlayVisible;
        private float automaticProgressMeters;
        private bool automaticComplete;
        private long revision;
        private int currentLegIndex;
        private double currentLegProgress;
        private string currentMode = string.Empty;
        private string currentLegLabel = string.Empty;
        private string currentRouteFingerprint = string.Empty;
        private Vector2 currentRoutePosition;
        private Vector2 currentVehicleRoutePosition;
        private bool overviewMarkerVisible;
        private bool actorVisible;
        private bool vehicleVisible;
        private bool captureInProgress;
        private Vector3 previousWorldPosition;
        private bool hasPreviousWorldPosition;
        private GUIStyle titleStyle = null!;
        private GUIStyle labelStyle = null!;
        private GUIStyle accentStyle = null!;

        public 사가정음식배달Journey검증Stage Stage { get; private set; }
        public long AppliedRevision => revision;
        public int AppliedSnapshotCount { get; private set; }
        public float ObservedMovementMeters { get; private set; }
        public Vector3 CurrentWorldPosition { get; private set; }
        public Vector3 CurrentVehicleWorldPosition { get; private set; }
        public string CurrentModeCode => currentMode;
        public int CurrentLegIndex => currentLegIndex;
        public double CurrentLegProgressMeters => currentLegProgress;
        public string CurrentRouteFingerprint => currentRouteFingerprint;
        public Vector2 CurrentRoutePosition => currentRoutePosition;
        public Vector2 CurrentVehicleRoutePosition => currentVehicleRoutePosition;
        public bool OverviewMarkerVisible => overviewMarkerVisible;
        public bool ActorVisible => actorVisible;
        public bool VehicleVisible => vehicleVisible;
        public int RouteLinePositionCount => playback == null ? 0 : playback.RouteLinePositionCount;
        public bool SceneWasChanged => gameObject.scene.isDirty;
        public bool EvidenceCaptureComplete { get; private set; }
        public string EvidenceDirectory { get; private set; } = string.Empty;

        public void Initialize(사가정운영디오라마View value)
        {
            if (view != null) throw new InvalidOperationException(
                "SagajeongFoodDeliveryGameViewValidationAlreadyInitialized");
            view = value != null ? value : throw new ArgumentNullException(nameof(value));
            previousGameOverlayVisible = view.GameOverlayVisible;
            view.GameOverlayVisible = true;
            view.Show(true);
            IsolateObservationCamera();

            mobilityRoot = new GameObject("Mobility · 합성 음식 배달 경로").transform;
            mobilityRoot.gameObject.layer = 사가정운영디오라마View.ObservationLayer;
            mobilityRoot.SetParent(transform, false);
            mobilityRoot.localPosition = new Vector3(MobilityRootX, MobilityRootY, MobilityRootZ);
            mobilityRoot.localRotation = Quaternion.Euler(0f, 8f, 0f);

            var prefabs = new GameObject("합성 검증 기호 원형");
            prefabs.transform.SetParent(transform, false);
            prefabs.SetActive(false);
            var marker = CreateOverviewMarker(prefabs.transform);
            var actor = CreateActor(prefabs.transform);
            var vehicle = CreateScooter(prefabs.transform);

            playback = gameObject.AddComponent<음식배달JourneyPlaybackLayer>();
            playback.Configure(view.ObservationCamera, mobilityRoot, marker, actor, vehicle,
                CreateMaterial("합성 배달 경로", new Color(.98f, .30f, .08f, 1f), true),
                음식배달JourneyPlaybackLayer.DefaultOverviewThreshold,
                음식배달JourneyPlaybackLayer.DefaultPerspectiveOverviewDistance, 2);
            var serialized = new SerializedObject(playback);
            serialized.FindProperty("routeWidth").floatValue = .8f;
            serialized.FindProperty("routeElevation").floatValue = .55f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            context = new 음식배달JourneyProjectionContext(RegionStableId,
                AdministrativeAreaStableId, GraphRevision, GraphHash, true);
            BuildLandmarks();
            SetStage(사가정음식배달Journey검증Stage.Automatic);
        }

        public void BeginEvidenceCapture()
        {
            if (captureInProgress) throw new InvalidOperationException(
                "SagajeongFoodDeliveryEvidenceCaptureAlreadyRunning");
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            EvidenceDirectory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..",
                "Documentation", "Changes",
                "2026-09-13-sagajeong-food-delivery-gameview", stamp));
            Directory.CreateDirectory(EvidenceDirectory);
            EvidenceCaptureComplete = false;
            captureInProgress = true;
            StartCoroutine(CaptureEvidenceRoutine());
        }

        public void SetStage(사가정음식배달Journey검증Stage value)
        {
            Stage = value;
            if (landmarkRoot != null)
                landmarkRoot.gameObject.SetActive(
                    value == 사가정음식배달Journey검증Stage.Overview
                    || value == 사가정음식배달Journey검증Stage.Automatic);
            switch (value)
            {
                case 사가정음식배달Journey검증Stage.Automatic:
                    interpreter.Reset();
                    playback.ClearAll();
                    automaticProgressMeters = 0f;
                    automaticComplete = false;
                    AppliedSnapshotCount = 0;
                    ObservedMovementMeters = 0f;
                    hasPreviousWorldPosition = false;
                    ApplyAt(0, 0d);
                    FocusClose();
                    Debug.Log("사가정 음식 배달 Game View 검증: 자동 재생 시작");
                    break;
                case 사가정음식배달Journey검증Stage.Overview:
                    automaticComplete = true;
                    ApplyAt(3, 15d);
                    view.ShowWhole();
                    playback.RefreshVisibilityFromCameraForTests();
                    RefreshVisualState();
                    Debug.Log("사가정 음식 배달 Game View 검증: 전체 보기 · leg 3 · 15m");
                    break;
                case 사가정음식배달Journey검증Stage.MotorcycleClose:
                    automaticComplete = true;
                    ApplyAt(3, 35d);
                    FocusResidenceClose();
                    Debug.Log("사가정 음식 배달 Game View 검증: 오토바이 확대 · leg 3 · 35m");
                    break;
                case 사가정음식배달Journey검증Stage.PedestrianClose:
                    automaticComplete = true;
                    ApplyAt(4, 2d);
                    FocusResidenceClose();
                    Debug.Log("사가정 음식 배달 Game View 검증: 도보 인계 확대 · leg 4 · 2m");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        private void Update()
        {
            if (Stage != 사가정음식배달Journey검증Stage.Automatic
                || automaticComplete) return;
            automaticProgressMeters = Mathf.Min(TotalDistance,
                automaticProgressMeters + Time.unscaledDeltaTime * AutomaticSpeedMetersPerSecond);
            ResolveRouteProgress(automaticProgressMeters, out var legIndex, out var legProgress);
            ApplyAt(legIndex, legProgress);
            FocusClose();
            automaticComplete = automaticProgressMeters >= TotalDistance;
        }

        private void LateUpdate()
        {
            if (view == null || view.ObservationCamera == null) return;
            view.GameOverlayVisible = true;
            view.ObservationCamera.enabled = true;
            foreach (var state in cameraStates)
                if (state.Camera != null && state.Camera != view.ObservationCamera)
                    state.Camera.enabled = false;
            foreach (var state in canvasStates)
                if (state.Canvas != null) state.Canvas.enabled = false;
            foreach (var state in overlayBehaviourStates)
                if (state.Behaviour != null) state.Behaviour.enabled = false;
        }

        private IEnumerator CaptureEvidenceRoutine()
        {
            yield return CaptureStage(사가정음식배달Journey검증Stage.Overview,
                "01-overview.png");
            yield return CaptureStage(사가정음식배달Journey검증Stage.MotorcycleClose,
                "02-close-motorcycle.png");
            yield return CaptureStage(사가정음식배달Journey검증Stage.PedestrianClose,
                "03-close-pedestrian.png");
            captureInProgress = false;
            EvidenceCaptureComplete = true;
            Debug.Log("사가정 음식 배달 Game View 검증: 증거 사진 3장 캡처 완료 · "
                      + EvidenceDirectory);
        }

        private IEnumerator CaptureStage(사가정음식배달Journey검증Stage value,
            string fileName)
        {
            SetStage(value);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(EvidenceDirectory, fileName);
            ScreenCapture.CaptureScreenshot(path, 1);
            for (var frame = 0; frame < 120; frame++)
            {
                yield return null;
                if (File.Exists(path) && new FileInfo(path).Length > 1024L)
                    yield break;
            }
            throw new TimeoutException("SagajeongFoodDeliveryEvidenceCaptureTimedOut:" + fileName);
        }

        private void ApplyAt(int legIndex, double progressMeters)
        {
            var snapshot = BuildSnapshot(legIndex, progressMeters, ++revision);
            if (!string.Equals(snapshot.routeFingerprint, ExpectedRouteFingerprint,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "SagajeongFoodDeliveryFixtureFingerprintMismatch");
            if (!interpreter.TryInterpret(snapshot, context, out var model, out var failureCode))
            {
                enabled = false;
                throw new InvalidOperationException(failureCode);
            }

            playback.Apply(model);
            playback.SetSelectedRoute(RouteStableId);
            currentLegIndex = legIndex;
            currentLegProgress = progressMeters;
            currentMode = model.CurrentModeCode;
            currentLegLabel = StageLabel(legIndex);
            currentRouteFingerprint = model.RouteFingerprint;
            currentRoutePosition = new Vector2((float)model.CurrentPosition.X,
                (float)model.CurrentPosition.Z);
            currentVehicleRoutePosition = model.HasVehiclePosition
                ? new Vector2((float)model.VehiclePosition.X,
                    (float)model.VehiclePosition.Z)
                : currentRoutePosition;
            AppliedSnapshotCount++;
            if (playback.TryGetVisualState(RouteStableId, out var state))
            {
                CurrentWorldPosition = mobilityRoot.TransformPoint(state.ActorPosition);
                CurrentVehicleWorldPosition = mobilityRoot.TransformPoint(state.VehiclePosition);
                if (Stage == 사가정음식배달Journey검증Stage.Automatic
                    && hasPreviousWorldPosition)
                    ObservedMovementMeters += Vector3.Distance(
                        previousWorldPosition, CurrentWorldPosition);
                previousWorldPosition = CurrentWorldPosition;
                hasPreviousWorldPosition =
                    Stage == 사가정음식배달Journey검증Stage.Automatic;
            }
            RefreshVisualState();
        }

        private void FocusClose()
        {
            view.FocusPoint(CurrentWorldPosition);
            view.ObservationCamera.orthographicSize = CloseOrthographicSize;
            playback.RefreshVisibilityFromCameraForTests();
            RefreshVisualState();
        }

        private void FocusResidenceClose()
        {
            var sharedFocus = mobilityRoot.TransformPoint(new Vector3(28f, 0f, 21f));
            view.FocusPoint(sharedFocus);
            view.ObservationCamera.orthographicSize = CloseOrthographicSize;
            playback.RefreshVisibilityFromCameraForTests();
            RefreshVisualState();
        }

        private void RefreshVisualState()
        {
            if (playback == null
                || !playback.TryGetVisualState(RouteStableId, out var state)) return;
            overviewMarkerVisible = state.OverviewMarkerVisible;
            actorVisible = state.ActorVisible;
            vehicleVisible = state.VehicleVisible;
        }

        private void IsolateObservationCamera()
        {
            cameraStates.Clear();
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera == null || !camera.gameObject.scene.IsValid()) continue;
                cameraStates.Add(new CameraState(camera, camera.enabled));
                camera.enabled = camera == view.ObservationCamera;
            }
            canvasStates.Clear();
            foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || !canvas.gameObject.scene.IsValid()) continue;
                canvasStates.Add(new CanvasState(canvas, canvas.enabled));
                canvas.enabled = false;
            }
            overlayBehaviourStates.Clear();
            foreach (var overlay in Resources.FindObjectsOfTypeAll<역방어준비View>())
            {
                if (overlay == null || !overlay.gameObject.scene.IsValid()) continue;
                overlayBehaviourStates.Add(new BehaviourState(overlay, overlay.enabled));
                overlay.enabled = false;
            }
        }

        private static 음식배달JourneySnapshotData BuildSnapshot(
            int currentLeg, double currentProgress, long valueRevision)
        {
            var legs = new 음식배달JourneyLegData[Definitions.Length];
            for (var index = 0; index < Definitions.Length; index++)
            {
                var definition = Definitions[index];
                var progress = index < currentLeg ? definition.Distance
                    : index > currentLeg ? 0d : currentProgress;
                var state = index < currentLeg ? 음식배달JourneyWireCodes.Completed
                    : index > currentLeg ? 음식배달JourneyWireCodes.Pending
                    : 음식배달JourneyWireCodes.Active;
                음식배달JourneyPolyline.TrySample(definition.Points, progress,
                    definition.Distance, out var actor, out _);
                var vehicle = definition.Mode == 음식배달JourneyWireCodes.Pedestrian
                    ? PedestrianVehiclePosition(index)
                    : actor;
                legs[index] = new 음식배달JourneyLegData
                {
                    stableId = definition.StableId,
                    mode = definition.Mode,
                    stateCode = state,
                    distanceMeters = definition.Distance,
                    progressMeters = progress,
                    points = definition.Points,
                    actorPosition = Data(actor),
                    vehiclePosition = Data(vehicle),
                };
            }

            var snapshot = new 음식배달JourneySnapshotData
            {
                schemaVersion = 음식배달JourneyWireCodes.SchemaVersion,
                regionStableId = RegionStableId,
                administrativeAreaStableId = AdministrativeAreaStableId,
                sourceKindCode = 음식배달JourneyWireCodes.SyntheticFixture,
                orderStableId = "order:synthetic:sagajeong-gameview:a",
                actorStableId = "actor:synthetic:sagajeong-driver:a",
                vehicleStableId = "vehicle:synthetic:sagajeong-scooter:a",
                routeStableId = RouteStableId,
                graphRevision = GraphRevision,
                graphHash = GraphHash,
                revision = valueRevision,
                observedAt = DateTimeOffset.UtcNow.ToString(
                    "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
                blocked = false,
                distributionApproved = false,
                readinessCode = 음식배달JourneyWireCodes.Ready,
                currentLegIndex = currentLeg,
                legs = legs,
            };
            snapshot.routeFingerprint = 음식배달JourneyFingerprint.Compute(snapshot);
            return snapshot;
        }

        private static 음식배달JourneyPoint PedestrianVehiclePosition(int legIndex)
            => legIndex <= 2
                ? new 음식배달JourneyPoint(10d, 0d)
                : new 음식배달JourneyPoint(30d, 20d);

        private static void ResolveRouteProgress(float value,
            out int legIndex, out double legProgress)
        {
            var remaining = (double)value;
            for (var index = 0; index < Definitions.Length; index++)
            {
                if (remaining <= Definitions[index].Distance || index == Definitions.Length - 1)
                {
                    legIndex = index;
                    legProgress = Math.Min(Definitions[index].Distance,
                        Math.Max(0d, remaining));
                    return;
                }
                remaining -= Definitions[index].Distance;
            }
            legIndex = Definitions.Length - 1;
            legProgress = Definitions[legIndex].Distance;
        }

        private static float TotalDistance => (float)Definitions.Sum(value => value.Distance);

        private GameObject CreateOverviewMarker(Transform parent)
        {
            var root = CreateSymbolRoot("전체뷰 배달 기호 원형", parent);
            CreatePrimitive(root.transform, "배달 위치 원", PrimitiveType.Cylinder,
                new Vector3(11f, .6f, 11f), new Vector3(0f, 0f, 0f),
                new Color(.98f, .27f, .08f, 1f));
            CreatePrimitive(root.transform, "배달 위치 중심", PrimitiveType.Sphere,
                new Vector3(2.2f, 2.2f, 2.2f), new Vector3(0f, 2.1f, 0f),
                new Color(1f, .86f, .18f, 1f));
            return root;
        }

        private GameObject CreateActor(Transform parent)
        {
            var root = CreateSymbolRoot("확대 배달 기사 원형", parent);
            CreatePrimitive(root.transform, "기사 몸", PrimitiveType.Capsule,
                new Vector3(1.3f, 1.8f, 1.3f), new Vector3(0f, 1.8f, 0f),
                new Color(1f, .78f, .08f, 1f));
            CreatePrimitive(root.transform, "기사 가방", PrimitiveType.Cube,
                new Vector3(1.45f, 1.25f, .65f), new Vector3(0f, 1.75f, -.85f),
                new Color(.91f, .16f, .08f, 1f));
            CreatePrimitive(root.transform, "기사 헬멧", PrimitiveType.Sphere,
                new Vector3(1.45f, .8f, 1.45f), new Vector3(0f, 3.75f, 0f),
                new Color(.17f, .25f, .34f, 1f));
            return root;
        }

        private GameObject CreateScooter(Transform parent)
        {
            var root = CreateSymbolRoot("확대 배달 스쿠터 원형", parent);
            CreatePrimitive(root.transform, "스쿠터 차체", PrimitiveType.Cube,
                new Vector3(1.8f, .75f, 3.6f), new Vector3(0f, .9f, 0f),
                new Color(.12f, .55f, .92f, 1f));
            CreatePrimitive(root.transform, "배달 상자", PrimitiveType.Cube,
                new Vector3(1.65f, 1.6f, 1.55f), new Vector3(0f, 1.75f, -1.1f),
                new Color(.94f, .20f, .09f, 1f));
            var front = CreatePrimitive(root.transform, "앞바퀴", PrimitiveType.Cylinder,
                new Vector3(.82f, .35f, .82f), new Vector3(0f, .45f, 1.5f),
                new Color(.10f, .12f, .15f, 1f));
            front.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var rear = CreatePrimitive(root.transform, "뒷바퀴", PrimitiveType.Cylinder,
                new Vector3(.82f, .35f, .82f), new Vector3(0f, .45f, -1.5f),
                new Color(.10f, .12f, .15f, 1f));
            rear.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return root;
        }

        private void BuildLandmarks()
        {
            landmarkRoot = new GameObject("합성 의미 위치 기호").transform;
            landmarkRoot.gameObject.layer = 사가정운영디오라마View.ObservationLayer;
            landmarkRoot.SetParent(mobilityRoot, false);
            CreateLandmark("기사 대기", P(0, 0), new Color(.18f, .50f, .92f, 1f));
            CreateLandmark("음식점", P(10, 4), new Color(.92f, .25f, .11f, 1f));
            CreateLandmark("전달지", P(30, 24), new Color(.16f, .72f, .34f, 1f));
        }

        private void CreateLandmark(string label, 음식배달JourneyPointData point, Color color)
        {
            var root = new GameObject("합성 의미 위치 · " + label)
            {
                layer = 사가정운영디오라마View.ObservationLayer,
            };
            root.transform.SetParent(landmarkRoot, false);
            root.transform.localPosition = new Vector3((float)point.x, .5f, (float)point.z);
            CreatePrimitive(root.transform, label + " 기호", PrimitiveType.Cylinder,
                new Vector3(1.5f, .8f, 1.5f), Vector3.zero, color);
        }

        private GameObject CreateSymbolRoot(string name, Transform parent)
        {
            var value = new GameObject(name)
            {
                layer = 사가정운영디오라마View.ObservationLayer,
            };
            value.transform.SetParent(parent, false);
            return value;
        }

        private GameObject CreatePrimitive(Transform parent, string name,
            PrimitiveType type, Vector3 scale, Vector3 localPosition, Color color)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.layer = 사가정운영디오라마View.ObservationLayer;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = scale;
            var renderer = value.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(name, color, false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = value.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return value;
        }

        private Material CreateMaterial(string name, Color color, bool unlit)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            var alwaysOnTop = shader != null;
            shader ??= unlit
                ? Shader.Find("Universal Render Pipeline/Unlit")
                : Shader.Find("Universal Render Pipeline/Lit");
            shader ??= Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException(
                "SagajeongFoodDeliveryValidationShaderMissing");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (alwaysOnTop)
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_ZTest", (int)CompareFunction.Always);
                material.renderQueue = unlit ? 4500 : 5000;
            }
            owned.Add(material);
            return material;
        }

        private void OnGUI()
        {
            if (view == null || !view.ObservationCamera.enabled) return;
            GUI.depth = -10000;
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(.94f, .96f, .98f) },
            };
            accentStyle ??= new GUIStyle(labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, .78f, .12f) },
            };

            var previous = GUI.color;
            GUI.color = new Color(.07f, .10f, .14f, .92f);
            GUI.Box(new Rect(16, 16, 590, 212), GUIContent.none);
            GUI.color = previous;
            GUI.Label(new Rect(30, 26, 550, 30), "사가정 음식 배달 이동 · Game View 검증", titleStyle);
            GUI.Label(new Rect(30, 60, 550, 24),
                $"화면: {StageText(Stage)}  |  구간 {currentLegIndex + 1}/7 · {currentLegLabel}", accentStyle);
            GUI.Label(new Rect(30, 86, 550, 42),
                $"{ModeText(currentMode)} · {currentLegProgress:0.0}/{Definitions[currentLegIndex].Distance:0.0}m"
                + $"  |  revision {revision}\n경로좌표 기사 ({currentRoutePosition.x:0.0}, {currentRoutePosition.y:0.0})"
                + $"  |  스쿠터 ({currentVehicleRoutePosition.x:0.0}, {currentVehicleRoutePosition.y:0.0})", labelStyle);
            GUI.Label(new Rect(30, 132, 550, 24),
                Stage == 사가정음식배달Journey검증Stage.Automatic
                    ? $"프레임 사본 {AppliedSnapshotCount}회 · 누적 위치 변화 {ObservedMovementMeters:0.0}m"
                    : "고정 검증 좌표 · 합성 상태 사본 직접 적용  |  노랑: 기사 · 파랑: 스쿠터",
                labelStyle);
            GUI.Label(new Rect(30, 158, 550, 24),
                $"표시 marker={overviewMarkerVisible} actor={actorVisible} vehicle={vehicleVisible}"
                + $"  |  routeLine={RouteLinePositionCount}", labelStyle);
            GUI.Label(new Rect(30, 184, 550, 30),
                $"route …food-delivery:a · hash {ShortFingerprint(currentRouteFingerprint)}"
                + " · distributionApproved=false · readOnly=true", labelStyle);

            var footerWidth = Screen.width - 32f;
            GUI.color = new Color(.07f, .10f, .14f, .92f);
            GUI.Box(new Rect(16, Screen.height - 68, footerWidth, 52), GUIContent.none);
            GUI.Box(new Rect(Screen.width - 250, 16, 234, 44), GUIContent.none);
            GUI.color = previous;
            GUI.Label(new Rect(30, Screen.height - 59, footerWidth - 28, 38),
                "SyntheticFixture · 비공개 검증 표본 · 실제 위치 추적/길찾기/주문 완료 권위 없음 · 저장 Scene 변경 없음",
                labelStyle);
            GUI.Label(new Rect(Screen.width - 236, 27, 206, 24),
                "검증 전용 카메라 · read only", labelStyle);
        }

        private void OnDestroy()
        {
            if (view != null) view.GameOverlayVisible = previousGameOverlayVisible;
            foreach (var state in cameraStates)
                if (state.Camera != null) state.Camera.enabled = state.Enabled;
            cameraStates.Clear();
            foreach (var state in canvasStates)
                if (state.Canvas != null) state.Canvas.enabled = state.Enabled;
            canvasStates.Clear();
            foreach (var state in overlayBehaviourStates)
                if (state.Behaviour != null) state.Behaviour.enabled = state.Enabled;
            overlayBehaviourStates.Clear();
            foreach (var value in owned)
                if (value != null) Destroy(value);
            owned.Clear();
        }

        private static string StageText(사가정음식배달Journey검증Stage value)
            => value switch
            {
                사가정음식배달Journey검증Stage.Automatic => "자동 재생 · 기사 추적",
                사가정음식배달Journey검증Stage.Overview => "전체 디오라마",
                사가정음식배달Journey검증Stage.MotorcycleClose => "오토바이 이동 확대",
                사가정음식배달Journey검증Stage.PedestrianClose => "도보 인계 확대",
                _ => value.ToString(),
            };

        private static string StageLabel(int index)
            => index switch
            {
                0 => "대기지→음식점",
                1 => "스쿠터 주차→음식점 문",
                2 => "픽업→스쿠터",
                3 => "음식점→전달지",
                4 => "스쿠터 주차→전달 문",
                5 => "전달 완료→스쿠터",
                6 => "전달지→대기지 복귀",
                _ => "알 수 없음",
            };

        private static string ModeText(string value)
            => value == 음식배달JourneyWireCodes.Pedestrian ? "도보"
                : value == 음식배달JourneyWireCodes.Motorcycle ? "오토바이"
                : value;

        private static string ShortFingerprint(string value)
            => string.IsNullOrEmpty(value) ? "없음"
                : value.Length <= 16 ? value
                : value.Substring(0, 8) + "…" + value.Substring(value.Length - 8);

        private static LegDefinition Leg(string id, string mode,
            params 음식배달JourneyPointData[] points)
        {
            var distance = 0d;
            for (var index = 1; index < points.Length; index++)
            {
                var dx = points[index].x - points[index - 1].x;
                var dz = points[index].z - points[index - 1].z;
                distance += Math.Sqrt(dx * dx + dz * dz);
            }
            return new LegDefinition(id, mode, distance, points);
        }

        private static 음식배달JourneyPointData P(double x, double z)
            => new() { x = x, z = z };

        private static 음식배달JourneyPointData Data(음식배달JourneyPoint value)
            => new() { x = value.X, z = value.Z };

        private sealed class LegDefinition
        {
            public LegDefinition(string stableId, string mode, double distance,
                음식배달JourneyPointData[] points)
            {
                StableId = stableId;
                Mode = mode;
                Distance = distance;
                Points = points;
            }

            public string StableId { get; }
            public string Mode { get; }
            public double Distance { get; }
            public 음식배달JourneyPointData[] Points { get; }
        }

        private sealed class CameraState
        {
            public CameraState(Camera camera, bool enabled)
            {
                Camera = camera;
                Enabled = enabled;
            }

            public Camera Camera { get; }
            public bool Enabled { get; }
        }

        private sealed class CanvasState
        {
            public CanvasState(Canvas canvas, bool enabled)
            {
                Canvas = canvas;
                Enabled = enabled;
            }

            public Canvas Canvas { get; }
            public bool Enabled { get; }
        }

        private sealed class BehaviourState
        {
            public BehaviourState(Behaviour behaviour, bool enabled)
            {
                Behaviour = behaviour;
                Enabled = enabled;
            }

            public Behaviour Behaviour { get; }
            public bool Enabled { get; }
        }
    }
}
