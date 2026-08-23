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
    }

    /// <summary>
    /// Solo 로컬 권위의 벌목·오두막·황혼 조우를 canonical World 위에 표현합니다.
    /// 입력 우선순위는 전투, Farm 배치, Nature 문맥 작업 순입니다.
    /// </summary>
    [DisallowMultipleComponent]
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
        private bool cabinPlacementMode;

        public bool SoloLocalAuthority => soloLocalAuthority;
        public Nature생존State State => engine.Snapshot();
        public string Status => status;
        public bool UsesSkeletonPlaceholder => skeletonPlaceholderPrefab != null;
        public event Action<string> EncounterFightRequested = delegate { };

        public void Configure(플레이어경관Controller playerController,
            Transform root, Transform origin, Nature생존State? initialState = null,
            INature생존Engine? authorityEngine = null)
        {
            player = playerController;
            visualRoot = root;
            semanticOrigin = origin;
            engine = authorityEngine ?? new Nature생존LocalEngine(initialState
                ?? Nature생존LocalEngine.CreateDefaultFixture());
            LoadEditorSyntyAssetsIfAvailable();
            RebuildVisuals();
            status = "안전 빈터 · 도끼를 찾아 왼쪽 버튼으로 획득하세요.";
        }

        public bool ValidateWiring()
            => player != null && visualRoot != null && semanticOrigin != null
                && engine != null && soloLocalAuthority;

        public void AcquireAxeForTest()
        {
            engine.AcquireAxe();
            RenderState();
        }

        public void BeginHarvestForTest(string stableId) => BeginHarvest(stableId);

        public void HoldWorkForTest(int seconds)
        {
            engine.AdvanceRealtime(seconds, true, false);
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
            var higherPriority = HasHigherPriorityInput();
            var mouseHeld = Mouse.current?.leftButton.isPressed == true;
            var paused = !UnityEngine.Application.isFocused || Time.timeScale <= 0f;
            realtimeAccumulator += Time.unscaledDeltaTime;
            while (realtimeAccumulator >= 1f)
            {
                realtimeAccumulator -= 1f;
                engine.AdvanceRealtime(1, mouseHeld && !higherPriority, paused);
                RenderState();
            }

            var keyboard = Keyboard.current;
            if (keyboard?.bKey.wasPressedThisFrame == true && !higherPriority)
            {
                cabinPlacementMode = true;
                status = "오두막 도면 배치 · 안전 빈터 안의 지면을 클릭하세요.";
            }
            if (keyboard?.fKey.wasPressedThisFrame == true
                && State.Encounter?.StateCode == Nature생존Codes.Pending)
            {
                var encounterId = State.Encounter!.StableId;
                status = "전투 대응 요청 · 기존 전투 권위의 결과를 기다립니다.";
                EncounterFightRequested(encounterId);
            }
            if (keyboard?.rKey.wasPressedThisFrame == true
                && State.Encounter?.StateCode == Nature생존Codes.Pending)
            {
                engine.ResolveEncounter(Nature생존Codes.Retreat);
                RenderState();
                status = State.Cabin.DefenseAvailable
                    ? "오두막 방어 거점으로 후퇴했습니다." : "안전 빈터로 후퇴했습니다.";
            }
            if (higherPriority || Mouse.current?.leftButton.wasPressedThisFrame != true)
                return;
            HandlePrimaryPressed();
        }

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
            if (marker == null) return;
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
                    engine.BeginCabinBuild();
                    status = "오두막 건설 중 · 왼쪽 버튼을 계속 누르세요.";
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
            status = "벌목 중 · 왼쪽 버튼을 4초 동안 누르세요.";
        }

        private Nature생존상호작용표식? FindFocusedMarker(Camera camera)
        {
            var screenPoint = Mouse.current?.position.ReadValue()
                ?? new Vector2(Screen.width * .5f, Screen.height * .5f);
            var ray = camera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out var hit, 100f,
                    ~0, QueryTriggerInteraction.Collide)) return null;
            var marker = hit.collider.GetComponentInParent<Nature생존상호작용표식>();
            return marker != null && Vector3.Distance(player.transform.position,
                marker.transform.position) <= interactionDistance ? marker : null;
        }

        private bool HasHigherPriorityInput()
        {
            if (player.Combat != null && player.Combat.HasActiveUnifiedBattle) return true;
            var placement = FindFirstObjectByType<Farm플레이어배치Controller>(
                FindObjectsInactive.Include);
            return placement != null && placement.PlacementModeActive;
        }

        private void RebuildVisuals()
        {
            foreach (Transform child in visualRoot) DestroyRuntimeSafe(child.gameObject);
            nodeVisuals.Clear();
            stumpVisuals.Clear();
            var state = engine.Snapshot();
            axeVisual = CreateVisual(axePrefab, PrimitiveType.Cube, "Nature_AxePickup",
                new Vector3(1.5f, .35f, 1.5f), new Vector3(.8f, .15f, .18f));
            axeVisual.AddComponent<Nature생존상호작용표식>().Kind =
                Nature생존상호작용Kind.Axe;
            axeVisual.GetComponent<Nature생존상호작용표식>().StableId =
                "pickup:nature-safe-clearing:basic-axe";
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
            GUI.DrawTexture(new Rect(20f, Screen.height - 138f, 520f, 112f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(34f, Screen.height - 130f, 490f, 22f),
                $"Nature 생존 · {PhaseKorean(state.ClockPhaseCode)} · Day {state.CycleIndex + 1}");
            GUI.Label(new Rect(34f, Screen.height - 106f, 490f, 22f),
                $"도끼 {(state.HasAxe ? "보유" : "미보유")} · 통나무 {state.TimberQuantity}/6 · WorldTick {state.WorldTick}");
            GUI.Label(new Rect(34f, Screen.height - 82f, 490f, 22f), status);
            GUI.Label(new Rect(34f, Screen.height - 58f, 490f, 22f),
                "LMB 획득/벌목/건설(누르기) · B 도면 · 조우 F 전투 / R 후퇴");
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

        public void AllowFightRetry(string reasonCode)
            => status = "전투 요청 대기 · " + reasonCode;
    }
}
