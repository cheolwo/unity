using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ssalddel.Unity.Presentation.World
{
    public enum Nature생존상호작용Kind
    {
        Axe,
        HarvestTree,
        Cabin,
    }

    [DisallowMultipleComponent]
    public sealed class Nature생존상호작용표식 : MonoBehaviour
    {
        public Nature생존상호작용Kind Kind;
        public string StableId = string.Empty;
        public string VisualSourceStableId = string.Empty;
        public bool UsesSyntyAsset;
    }

    /// <summary>
    /// Solo 로컬 권위의 벌목·오두막·황혼 조우를 canonical World 위에 표현합니다.
    /// 입력 우선순위는 전투, Farm 배치, Nature 문맥 작업 순입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class Nature생존Controller : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private Transform semanticOrigin = null!;
        [SerializeField] private GameObject treePrefab = null!;
        [SerializeField] private GameObject stumpPrefab = null!;
        [SerializeField] private GameObject axePrefab = null!;
        [SerializeField] private GameObject skeletonPlaceholderPrefab = null!;
        [SerializeField, Min(1f)] private float interactionDistance = 3.2f;
        [SerializeField] private bool soloLocalAuthority = true;

        private readonly Dictionary<string, GameObject> nodeVisuals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> stumpVisuals = new(StringComparer.Ordinal);
        private INature생존Engine engine = null!;
        private GameObject? axeVisual;
        private GameObject? cabinVisual;
        private GameObject? encounterVisual;
        private float realtimeAccumulator;
        private string status = "Nature 생존 준비";
        private string launchProfileLabel = "기본 Solo";
        private Func<string>? saveWorld;
        private Mouse? activeWorkMouse;
        private bool cabinPlacementMode;
        private bool primaryWasHeld;
        private bool buildKeyWasHeld;
        private bool fightKeyWasHeld;
        private bool retreatKeyWasHeld;
        private bool exitKeyWasHeld;
        private bool cancelKeyWasHeld;
        private bool storeKeyWasHeld;
        private bool sleepKeyWasHeld;
        private bool plan1KeyWasHeld;
        private bool plan2KeyWasHeld;
        private bool plan3KeyWasHeld;

        public bool SoloLocalAuthority => soloLocalAuthority;
        public Nature생존State State => engine.Snapshot();
        public string Status => status;
        public bool CurrentPrimaryInputPressedForTest
            => Mouse.current?.leftButton.isPressed == true;
        public bool UsesSkeletonPlaceholder => skeletonPlaceholderPrefab != null;
        public event Action<string> EncounterFightRequested = delegate { };

        public void Configure(플레이어경관Controller playerController,
            Transform root, Transform origin, Nature생존State? initialState = null,
            INature생존Engine? authorityEngine = null,
            Func<string>? saveWorldAction = null,
            string profileLabel = "기본 Solo")
        {
            player = playerController;
            visualRoot = root;
            semanticOrigin = origin;
            engine = authorityEngine ?? new Nature생존LocalEngine(initialState
                ?? Nature생존LocalEngine.CreateDefaultFixture());
            saveWorld = saveWorldAction;
            launchProfileLabel = string.IsNullOrWhiteSpace(profileLabel)
                ? "기본 Solo" : profileLabel;
            LoadEditorSyntyAssetsIfAvailable();
            RebuildVisuals();
            status = "안전 빈터 · 도끼를 찾아 왼쪽 버튼으로 획득하세요.";
        }

        public void SetStartupStatus(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) status = value;
        }

        public void SaveWorldForTest() => SaveWorld();

        public void ProcessCurrentInputForTest()
        {
            primaryWasHeld = false;
            buildKeyWasHeld = false;
            fightKeyWasHeld = false;
            retreatKeyWasHeld = false;
            exitKeyWasHeld = false;
            cancelKeyWasHeld = false;
            storeKeyWasHeld = false;
            sleepKeyWasHeld = false;
            plan1KeyWasHeld = false;
            plan2KeyWasHeld = false;
            plan3KeyWasHeld = false;
            Update();
        }

        public void ContinueCurrentInputForTest() => Update();

        public bool ValidateWiring()
            => player != null && visualRoot != null && semanticOrigin != null
                && engine != null && soloLocalAuthority;

        public void AcquireAxeForTest()
        {
            engine.AcquireAxe();
            RenderState();
        }

        public void BeginHarvestForTest(string stableId) => BeginHarvest(stableId);

        public void BeginCabinBuildForTest()
        {
            engine.BeginCabinBuild();
            RenderState();
        }

        public void HoldWorkForTest(int seconds)
        {
            engine.AdvanceRealtime(seconds, true, false);
            RenderState();
        }

        public void CancelActiveWorkForTest() => CancelActiveWork();

        public void EnterCabinForTest()
        {
            engine.EnterCabin();
            RenderState();
        }

        public void LeaveCabinForTest()
        {
            engine.LeaveCabin();
            RenderState();
        }

        public void StoreAtCabinForTest()
        {
            engine.StoreAtCabin();
            RenderState();
        }

        public void SleepInCabinForTest()
        {
            engine.SleepInCabin();
            RenderState();
        }

        public void SelectExpansionPlanForTest(string planCode)
        {
            engine.SelectExpansionPlan(planCode);
            RenderState();
        }

        private void Awake()
        {
            if (engine == null && player != null && visualRoot != null)
                Configure(player, visualRoot, semanticOrigin != null
                    ? semanticOrigin : visualRoot);
        }

        private void OnDestroy()
        {
            if (engine is IDisposable disposable) disposable.Dispose();
        }

        private void Update()
        {
            if (!ValidateWiring()) return;
            var inputBlockReason = HigherPriorityInputReason();
            var higherPriority = !string.IsNullOrEmpty(inputBlockReason);
            var inputMouse = State.ActiveWork != null && activeWorkMouse != null
                ? activeWorkMouse : Mouse.current;
            var mouseHeld = inputMouse?.leftButton.isPressed == true;
            var primaryPressed = mouseHeld && !primaryWasHeld;
            primaryWasHeld = mouseHeld;
            var paused = (!UnityEngine.Application.isFocused
                          && !UnityEngine.Application.isBatchMode)
                         || Time.timeScale <= 0f;
            realtimeAccumulator += Time.unscaledDeltaTime;
            while (realtimeAccumulator >= 1f)
            {
                realtimeAccumulator -= 1f;
                engine.AdvanceRealtime(1, mouseHeld && !higherPriority, paused);
                RenderState();
                if (State.ActiveWork == null) activeWorkMouse = null;
            }

            var keyboard = Keyboard.current;
            var buildHeld = keyboard?.bKey.isPressed == true;
            var fightHeld = keyboard?.fKey.isPressed == true;
            var retreatHeld = keyboard?.rKey.isPressed == true;
            var exitHeld = keyboard?.eKey.isPressed == true;
            var cancelHeld = keyboard?.cKey.isPressed == true;
            var storeHeld = keyboard?.gKey.isPressed == true;
            var sleepHeld = keyboard?.tKey.isPressed == true;
            var plan1Held = keyboard?.digit1Key.isPressed == true;
            var plan2Held = keyboard?.digit2Key.isPressed == true;
            var plan3Held = keyboard?.digit3Key.isPressed == true;
            var buildPressed = buildHeld && !buildKeyWasHeld;
            var fightPressed = fightHeld && !fightKeyWasHeld;
            var retreatPressed = retreatHeld && !retreatKeyWasHeld;
            var exitPressed = exitHeld && !exitKeyWasHeld;
            var cancelPressed = cancelHeld && !cancelKeyWasHeld;
            var storePressed = storeHeld && !storeKeyWasHeld;
            var sleepPressed = sleepHeld && !sleepKeyWasHeld;
            var plan1Pressed = plan1Held && !plan1KeyWasHeld;
            var plan2Pressed = plan2Held && !plan2KeyWasHeld;
            var plan3Pressed = plan3Held && !plan3KeyWasHeld;
            buildKeyWasHeld = buildHeld;
            fightKeyWasHeld = fightHeld;
            retreatKeyWasHeld = retreatHeld;
            exitKeyWasHeld = exitHeld;
            cancelKeyWasHeld = cancelHeld;
            storeKeyWasHeld = storeHeld;
            sleepKeyWasHeld = sleepHeld;
            plan1KeyWasHeld = plan1Held;
            plan2KeyWasHeld = plan2Held;
            plan3KeyWasHeld = plan3Held;
            if (buildPressed && !higherPriority)
            {
                cabinPlacementMode = true;
                status = "오두막 도면 배치 · 안전 빈터 안의 지면을 클릭하세요.";
            }
            if (fightPressed
                && State.Encounter?.StateCode == Nature생존Codes.Pending)
            {
                var encounterId = State.Encounter!.StableId;
                status = "전투 대응 요청 · 기존 전투 권위의 결과를 기다립니다.";
                EncounterFightRequested(encounterId);
            }
            if (retreatPressed
                && State.Encounter?.StateCode == Nature생존Codes.Pending)
            {
                engine.ResolveEncounter(Nature생존Codes.Retreat);
                RenderState();
                status = State.Cabin.DefenseAvailable
                    ? "오두막 방어 거점으로 후퇴했습니다." : "안전 빈터로 후퇴했습니다.";
            }
            if (exitPressed && !higherPriority && State.PlayerInsideCabin)
            {
                try
                {
                    engine.LeaveCabin();
                    RenderState();
                    status = "오두막 퇴장 · 안전 빈터로 돌아왔습니다.";
                }
                catch (InvalidOperationException error)
                {
                    status = "오두막 퇴장 차단 · " + error.Message;
                }
            }
            if (cancelPressed && !higherPriority && State.ActiveWork != null)
                CancelActiveWork();
            if (storePressed && !higherPriority)
                RunCabinAction(engine.StoreAtCabin, "여분 통나무를 오두막에 보관했습니다.");
            if (sleepPressed && !higherPriority)
                RunCabinAction(engine.SleepInCabin, "수면 시작 · 밤 시간이 6배로 흐릅니다.");
            if (!higherPriority && (plan1Pressed || plan2Pressed || plan3Pressed))
            {
                var plan = plan1Pressed ? Nature생존Codes.Workbench
                    : plan2Pressed ? Nature생존Codes.StorageRack
                    : Nature생존Codes.Palisade;
                RunCabinAction(() => engine.SelectExpansionPlan(plan),
                    "Day 2 계획 고정 · " + plan);
            }
            if (higherPriority)
            {
                if (primaryPressed)
                    status = "Nature 입력 대기 · " + inputBlockReason;
                return;
            }
            if (!primaryPressed)
                return;
            if (TryHandleHudPrimaryPressed()) return;
            if (PointerOverHud()) return;
            HandlePrimaryPressed();
        }

        private bool TryHandleHudPrimaryPressed()
        {
            if (Mouse.current == null) return false;
            var point = ToGuiPoint(Mouse.current.position.ReadValue());
            if (SaveButtonGuiRect().Contains(point))
            {
                SaveWorld();
                return true;
            }
            if (CancelButtonGuiRect().Contains(point))
            {
                if (State.ActiveWork != null) CancelActiveWork();
                return true;
            }
            return false;
        }

        private bool PointerOverHud()
        {
            if (Mouse.current == null) return false;
            if (player.CurrentMode == 플레이어시점Mode.FirstPerson) return false;
            if (Screen.width < 560 || Screen.height < 200) return false;
            return HudGuiRect().Contains(ToGuiPoint(
                Mouse.current.position.ReadValue()));
        }

        private static Vector2 ToGuiPoint(Vector2 inputSystemPoint)
            => new(inputSystemPoint.x, Screen.height - inputSystemPoint.y);

        private static Rect HudGuiRect()
            => new(20f, Screen.height - 260f, 560f, 234f);

        private static Rect SaveButtonGuiRect()
            => new(34f, Screen.height - 102f, 132f, 28f);

        private static Rect CancelButtonGuiRect()
            => new(178f, Screen.height - 102f, 150f, 28f);

        private void HandlePrimaryPressed()
        {
            var camera = player.CurrentMode == 플레이어시점Mode.FirstPerson
                ? player.FirstPersonCamera : player.PlayerCamera;
            if (camera == null) return;
            if (cabinPlacementMode)
            {
                var ray = camera.ScreenPointToRay(Mouse.current!.position.ReadValue());
                if (Physics.Raycast(ray, out var groundHit, 100f,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    var local = semanticOrigin.InverseTransformPoint(groundHit.point);
                    try
                    {
                        engine.PlaceCabinBlueprint(local.x, local.z,
                            player.transform.eulerAngles.y);
                        cabinPlacementMode = false;
                        RenderState();
                        status = "오두막 도면 배치 완료 · 통나무 6개를 모아 누르고 건설하세요.";
                    }
                    catch (InvalidOperationException error)
                    {
                        status = "오두막 배치 차단 · " + error.Message;
                    }
                }
                return;
            }

            var marker = FindFocusedMarker(camera);
            if (marker == null)
            {
                status = "상호작용 대상을 조준선에서 찾지 못했습니다.";
                return;
            }
            try
            {
                if (marker.Kind == Nature생존상호작용Kind.Axe)
                {
                    engine.AcquireAxe();
                    status = "기본 도끼 획득 · 수확 구역 나무를 누르고 벌목하세요.";
                }
                else if (marker.Kind == Nature생존상호작용Kind.HarvestTree)
                    BeginHarvest(marker.StableId);
                else if (marker.Kind == Nature생존상호작용Kind.Cabin)
                {
                    if (State.Cabin.StateCode == Nature생존Codes.Completed)
                    {
                        engine.EnterCabin();
                        status = "오두막 입장 · E 키로 안전 빈터에 나갈 수 있습니다.";
                    }
                    else
                    {
                        engine.BeginCabinBuild();
                        activeWorkMouse = Mouse.current;
                        status = "오두막 건설 중 · 왼쪽 버튼을 계속 누르세요.";
                    }
                }
                RenderState();
            }
            catch (InvalidOperationException error)
            {
                status = "상호작용 차단 · " + error.Message;
            }
        }

        private void BeginHarvest(string stableId)
        {
            engine.BeginHarvest(stableId);
            activeWorkMouse = Mouse.current;
            status = "벌목 중 · 왼쪽 버튼을 4초 동안 누르세요.";
        }

        private void CancelActiveWork()
        {
            try
            {
                var kindCode = State.ActiveWork?.KindCode ?? string.Empty;
                engine.CancelActiveWork();
                activeWorkMouse = null;
                RenderState();
                status = kindCode == Nature생존Codes.CabinBuild
                    ? "건설 취소 · 예약 통나무를 인벤토리에 반환했습니다."
                    : "벌목 취소 · 나무와 자원 상태를 그대로 유지했습니다.";
            }
            catch (InvalidOperationException error)
            {
                status = "작업 취소 차단 · " + error.Message;
            }
        }

        private void RunCabinAction(Action action, string successStatus)
        {
            try
            {
                action();
                RenderState();
                status = successStatus;
            }
            catch (InvalidOperationException error)
            {
                status = "행동 차단 · " + error.Message;
            }
        }

        private Nature생존상호작용표식? FindFocusedMarker(Camera camera)
        {
            var screenPoint = player.CurrentMode == 플레이어시점Mode.FirstPerson
                ? new Vector2(Screen.width * .5f, Screen.height * .5f)
                : Mouse.current?.position.ReadValue()
                    ?? new Vector2(Screen.width * .5f, Screen.height * .5f);
            var ray = camera.ScreenPointToRay(screenPoint);
            var direct = Physics.RaycastAll(ray, 100f, ~0,
                    QueryTriggerInteraction.Collide)
                .OrderBy(hit => hit.distance)
                .Select(hit => hit.collider.GetComponentInParent<
                    Nature생존상호작용표식>())
                .FirstOrDefault(marker => marker != null
                    && Vector3.Distance(player.transform.position,
                        marker.transform.position) <= interactionDistance);
            if (direct != null) return direct;

            return FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Exclude)
                .Where(marker => Vector3.Distance(player.transform.position,
                    marker.transform.position) <= interactionDistance)
                .Select(marker => new
                {
                    Marker = marker,
                    Angle = Vector3.Angle(ray.direction,
                        MarkerAimPoint(marker) - ray.origin),
                })
                .Where(candidate => candidate.Angle <= 10f)
                .OrderBy(candidate => candidate.Angle)
                .ThenBy(candidate => Vector3.Distance(ray.origin,
                    MarkerAimPoint(candidate.Marker)))
                .Select(candidate => candidate.Marker)
                .FirstOrDefault();
        }

        private static Vector3 MarkerAimPoint(Nature생존상호작용표식 marker)
        {
            var collider = marker.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : marker.transform.position;
        }

        private string HigherPriorityInputReason()
        {
            if (player.Combat != null && player.Combat.HasActiveUnifiedBattle)
                return "전투 입력 사용 중";
            var placement = FindFirstObjectByType<Farm플레이어배치Controller>(
                FindObjectsInactive.Include);
            return placement != null && placement.PlacementModeActive
                ? "Farm 배치 입력 사용 중" : string.Empty;
        }

        private void RebuildVisuals()
        {
            foreach (Transform child in visualRoot) DestroyRuntimeSafe(child.gameObject);
            nodeVisuals.Clear();
            stumpVisuals.Clear();
            var state = engine.Snapshot();
            axeVisual = CreateVisual(axePrefab, PrimitiveType.Cube, "Nature_AxePickup",
                new Vector3(1.5f, .35f, 1.5f), new Vector3(.8f, .15f, .18f));
            var axeMarker = axeVisual.AddComponent<Nature생존상호작용표식>();
            axeMarker.Kind = Nature생존상호작용Kind.Axe;
            axeMarker.StableId = "pickup:nature-safe-clearing:basic-axe";
            axeMarker.UsesSyntyAsset = axePrefab != null;
            axeMarker.VisualSourceStableId = axePrefab != null
                ? "asset:synty:polygon-generic:sm-gen-wep-axe-01"
                : "fallback:primitive:cube";
            foreach (var node in state.ResourceNodes)
            {
                var visual = CreateVisual(treePrefab, PrimitiveType.Cylinder,
                    "Nature_HarvestTree_" + node.StableId,
                    new Vector3(node.LocalX, 0f, node.LocalZ), Vector3.one);
                var marker = visual.AddComponent<Nature생존상호작용표식>();
                marker.Kind = Nature생존상호작용Kind.HarvestTree;
                marker.StableId = node.StableId;
                nodeVisuals.Add(node.StableId, visual);
                var stump = CreateVisual(stumpPrefab, PrimitiveType.Cylinder,
                    "Nature_HarvestStump_" + node.StableId,
                    new Vector3(node.LocalX, 0f, node.LocalZ),
                    stumpPrefab != null ? Vector3.one : new Vector3(1f, .25f, 1f));
                stump.SetActive(false);
                stumpVisuals.Add(node.StableId, stump);
            }
            RenderState();
        }

        private void RenderState()
        {
            var state = engine.Snapshot();
            if (axeVisual != null) axeVisual.SetActive(!state.HasAxe);
            foreach (var node in state.ResourceNodes)
            {
                if (!nodeVisuals.TryGetValue(node.StableId, out var visual)) continue;
                if (node.StateCode == Nature생존Codes.Standing)
                {
                    visual.SetActive(true);
                    if (stumpVisuals.TryGetValue(node.StableId, out var stump))
                        stump.SetActive(false);
                }
                else
                {
                    visual.SetActive(false);
                    if (stumpVisuals.TryGetValue(node.StableId, out var stump))
                        stump.SetActive(true);
                }
            }
            RenderCabin(state);
            RenderEncounter(state);
        }

        private void RenderCabin(Nature생존State state)
        {
            if (state.Cabin.StateCode == Nature생존Codes.Planned)
            {
                if (cabinVisual != null) cabinVisual.SetActive(false);
                return;
            }
            if (cabinVisual == null)
            {
                cabinVisual = CreateCabinBlockout();
                var marker = cabinVisual.AddComponent<Nature생존상호작용표식>();
                marker.Kind = Nature생존상호작용Kind.Cabin;
                marker.StableId = state.Cabin.StableId;
            }
            cabinVisual.SetActive(true);
            cabinVisual.transform.localPosition = new Vector3(state.Cabin.LocalX, 0f,
                state.Cabin.LocalZ);
            cabinVisual.transform.localRotation = Quaternion.Euler(0f,
                state.Cabin.YawDegrees, 0f);
            var alpha = state.Cabin.StateCode == Nature생존Codes.Completed ? 1f : .42f;
            var buildProgress = state.ActiveWork?.KindCode == Nature생존Codes.CabinBuild
                ? Mathf.Clamp01((float)state.ActiveWork.CompletedSeconds
                    / Mathf.Max(1, state.ActiveWork.RequiredSeconds)) : 0f;
            cabinVisual.transform.localScale = state.Cabin.StateCode == Nature생존Codes.Completed
                ? Vector3.one : new Vector3(1f, Mathf.Lerp(.2f, 1f, buildProgress), 1f);
            foreach (var renderer in cabinVisual.GetComponentsInChildren<Renderer>())
            {
                if (renderer.material.HasProperty("_Color"))
                {
                    var color = renderer.material.color;
                    color.a = alpha;
                    renderer.material.color = color;
                }
            }
        }

        private void RenderEncounter(Nature생존State state)
        {
            var pending = state.Encounter?.StateCode == Nature생존Codes.Pending;
            if (!pending)
            {
                if (encounterVisual != null) encounterVisual.SetActive(false);
                return;
            }
            if (encounterVisual == null)
            {
                encounterVisual = CreateVisual(skeletonPlaceholderPrefab,
                    PrimitiveType.Capsule, "Nature_Threat_Skeleton_PLACEHOLDER",
                    new Vector3(0f, 0f, 14f), Vector3.one);
            }
            encounterVisual.SetActive(true);
        }

        private GameObject CreateCabinBlockout()
        {
            var root = new GameObject("Nature_Cabin_StagedBlueprint");
            root.transform.SetParent(visualRoot, false);
            foreach (var spec in new[]
            {
                (new Vector3(0f,.2f,0f), new Vector3(5f,.4f,4f)),
                (new Vector3(-2.3f,1.5f,0f), new Vector3(.35f,2.6f,4f)),
                (new Vector3(2.3f,1.5f,0f), new Vector3(.35f,2.6f,4f)),
                (new Vector3(0f,1.5f,1.8f), new Vector3(4.4f,2.6f,.35f)),
                (new Vector3(-1.5f,1.5f,-1.8f), new Vector3(1.4f,2.6f,.35f)),
                (new Vector3(1.5f,1.5f,-1.8f), new Vector3(1.4f,2.6f,.35f)),
            })
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = spec.Item1;
                part.transform.localScale = spec.Item2;
            }
            return root;
        }

        private GameObject CreateVisual(GameObject prefab, PrimitiveType fallback,
            string objectName, Vector3 localPosition, Vector3 localScale)
        {
            var value = prefab != null ? Instantiate(prefab, visualRoot)
                : GameObject.CreatePrimitive(fallback);
            value.name = objectName;
            value.transform.SetParent(visualRoot, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = localScale;
            if (value.GetComponentsInChildren<Collider>(true).Length == 0)
                value.AddComponent<BoxCollider>();
            return value;
        }

        private void LoadEditorSyntyAssetsIfAvailable()
        {
#if UNITY_EDITOR
            treePrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab");
            stumpPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Stump_01.prefab");
            axePrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonGeneric/Prefabs/Weapons/SM_Gen_Wep_Axe_01.prefab");
            skeletonPlaceholderPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Skeleton_01.prefab");
#endif
        }

        private static void DestroyRuntimeSafe(UnityEngine.Object value)
        {
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnGUI()
        {
            if (!ValidateWiring()) return;
            var state = engine.Snapshot();
            GUI.color = new Color(.035f, .055f, .035f, .9f);
            GUI.DrawTexture(HudGuiRect(), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, Screen.height - 252f, 530f, 22f),
                $"Nature 생존 · {launchProfileLabel} · {PhaseKorean(state.ClockPhaseCode)} · Day {state.CycleIndex + 1}");
            GUI.Label(new Rect(34f, Screen.height - 228f, 530f, 22f),
                $"도끼 {(state.HasAxe ? "보유" : "미보유")} · 통나무 소지 {state.TimberQuantity} / 보관 {state.StoredTimberQuantity}/20 · 부품 {state.RebuildPartQuantity}");
            GUI.Label(new Rect(34f, Screen.height - 204f, 530f, 22f),
                $"소음 {state.NoiseEventCount} · 위협 {state.RawThreatTier}→{state.EffectiveThreatTier} · {(state.Sleeping ? "수면 중" : state.Day2Ready ? "Day2 준비" : "활동 중")}");
            GUI.Label(new Rect(34f, Screen.height - 180f, 530f, 22f), status);
            GUI.Label(new Rect(34f, Screen.height - 156f, 530f, 22f),
                "LMB 상호작용 · G 보관 · T 수면 · 1 작업대 / 2 보관대 / 3 방책");
            GUI.Label(new Rect(34f, Screen.height - 132f, 530f, 22f),
                "B 도면 · C 취소 · E 퇴장 · F 전투 / R 후퇴");
            GUI.enabled = saveWorld != null;
            GUI.Button(SaveButtonGuiRect(), "현재 세계 저장");
            GUI.enabled = state.ActiveWork != null;
            GUI.Button(CancelButtonGuiRect(), "진행 작업 취소");
            GUI.enabled = true;
            GUI.Label(new Rect(34f, Screen.height - 66f, 520f, 22f),
                "저장 후 Play Mode 재진입 시 같은 세계를 복원합니다.");
        }

        private void SaveWorld()
        {
            if (saveWorld == null)
            {
                status = "저장 사용 불가 · Local Runtime 슬롯이 없습니다.";
                return;
            }
            try
            {
                status = saveWorld();
            }
            catch (Exception error)
            {
                status = "저장 실패 · " + error.Message;
            }
        }

        private static string PhaseKorean(string phaseCode) => phaseCode switch
        {
            NatureSurvivalClockPhaseCodes.Daylight => "낮",
            NatureSurvivalClockPhaseCodes.Dusk => "황혼",
            NatureSurvivalClockPhaseCodes.Night => "밤",
            NatureSurvivalClockPhaseCodes.Dawn => "새벽",
            _ => phaseCode,
        };

        public void ResolveFightVictory(string encounterStableId)
        {
            var encounter = State.Encounter;
            if (encounter == null || encounter.StableId != encounterStableId
                || encounter.StateCode != Nature생존Codes.Pending) return;
            engine.ResolveEncounter(Nature생존Codes.Fight);
            RenderState();
            status = "위협 대응 완료 · Nature 생활 거점으로 복귀했습니다.";
        }

        public void RefreshAfterBattle(string encounterStableId, string resultCode)
        {
            var encounter = State.Encounter;
            if (encounter == null || encounter.StableId != encounterStableId) return;
            RenderState();
            var authoritativeResult = string.IsNullOrWhiteSpace(State.LastCombatResultCode)
                ? resultCode : State.LastCombatResultCode;
            status = authoritativeResult == Nature생존Codes.Victory
                ? "전투 승리 · 재건 부품을 확보했습니다."
                : authoritativeResult == Nature생존Codes.Retreat
                    ? "안전 후퇴 · 보상 없이 거점으로 복귀했습니다."
                    : "전투 패배 · 소지 재료 일부를 잃고 거점으로 복귀했습니다.";
        }

        public void AllowFightRetry(string reasonCode)
            => status = "전투 요청 대기 · " + reasonCode;
    }
}
