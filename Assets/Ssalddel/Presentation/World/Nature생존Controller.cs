using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.PlayerActivities;
using Ssalddel.Unity.ImmersiveWorld;
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
        DroppedTimber,
        Cabin,
        Workbench,
    }

    [DisallowMultipleComponent]
    public sealed class Nature생존상호작용표식 : MonoBehaviour
    {
        public Nature생존상호작용Kind Kind;
        public string StableId = string.Empty;
        public string VisualSourceStableId = string.Empty;
        public bool UsesSyntyAsset;
    }

    /// <summary>월드 문맥 안내가 카메라를 향하게 하는 표현 전용 구성 요소입니다.</summary>
    public sealed class Nature월드안내Billboard : MonoBehaviour
    {
        public Camera TargetCamera = null!;

        private void LateUpdate()
        {
            if (TargetCamera == null) return;
            var direction = transform.position - TargetCamera.transform.position;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized,
                    Vector3.up);
        }
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
        [SerializeField] private GameObject timberLogPrefab = null!;
        [SerializeField] private GameObject cabinPrefab = null!;
        [SerializeField] private GameObject workbenchPrefab = null!;
        [SerializeField] private GameObject skeletonPlaceholderPrefab = null!;
        [SerializeField] private GameObject storedTimberPrefab = null!;
        [SerializeField] private GameObject sleepBedPrefab = null!;
        [SerializeField] private GameObject sleepCampfirePrefab = null!;
        [SerializeField] private GameObject expansionPlansPrefab = null!;
        [SerializeField, Min(1f)] private float interactionDistance = 3.2f;
        [SerializeField] private bool soloLocalAuthority = true;

        private readonly Dictionary<string, GameObject> nodeVisuals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> stumpVisuals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> droppedTimberVisuals =
            new(StringComparer.Ordinal);
        private INature생존Engine engine = null!;
        private GameObject? axeVisual;
        private GameObject? equippedAxeVisual;
        private GameObject? cabinVisual;
        private GameObject? workbenchVisual;
        private GameObject? encounterVisual;
        private GameObject? storedTimberVisual;
        private GameObject? storedTimberCompanionVisual;
        private GameObject? storedTimberStoragePadVisual;
        private GameObject? sleepBedVisual;
        private GameObject? sleepCampfireVisual;
        private GameObject? dawnPlansVisual;
        private GameObject? dawnPlanBoardVisual;
        private TextMesh? storedTimberWorldLabel;
        private TextMesh? cabinStateWorldLabel;
        private TextMesh? dawnPlanWorldLabel;
        private float approvedSurfaceWorldY;
        private float realtimeAccumulator;
        private string status = "Nature 생존 준비";
        private string launchProfileLabel = "기본 Solo";
        private Func<string>? saveWorld;
        private Mouse? activeWorkMouse;
        private Nature생존상호작용표식? assistedApproachTarget;
        private bool cabinPlacementMode;
        private bool workbenchPlacementMode;
        private bool workHoldRequested;
        private bool primaryWasHeld;
        private bool buildKeyWasHeld;
        private bool fightKeyWasHeld;
        private bool retreatKeyWasHeld;
        private bool observeKeyWasHeld;
        private bool exitKeyWasHeld;
        private bool cancelKeyWasHeld;
        private bool storeKeyWasHeld;
        private bool sleepKeyWasHeld;
        private bool plan1KeyWasHeld;
        private bool plan2KeyWasHeld;
        private bool plan3KeyWasHeld;
        private bool diagnosticKeyWasHeld;
        private bool inventoryKeyWasHeld;
        private bool escapeKeyWasHeld;
        private bool diagnosticHudExpanded;
        private bool equipmentPanelOpen;
        private int equipmentPointerHandledFrame = -1;
        private SimulationActorEquipmentStateSnapshot? equipmentState;
        private bool legacyWorldVisualsMigrated;
        private Nature위협관찰PresentationDecision? threatObservation;
        private long? lastSavedRevision;

        public bool SoloLocalAuthority => soloLocalAuthority;
        public Nature생존State State => engine.Snapshot();
        public Vector3 PlayerWorldPosition => player != null
            ? player.transform.position : transform.position;
        /// <summary>
        /// Nature 권위 좌표(LocalX/LocalZ)가 기준으로 삼는 월드 원점입니다.
        /// LH 셀 소유권 계산은 Controller GameObject가 아니라 기존 표현과
        /// 동일한 이 원점을 사용해야 합니다.
        /// </summary>
        public Transform WorldAssetPlacementOrigin
            => visualRoot != null ? visualRoot : transform;
        public string Status => status;
        public bool CurrentPrimaryInputPressedForTest
            => Mouse.current?.leftButton.isPressed == true;
        public bool UsesSkeletonPlaceholder => skeletonPlaceholderPrefab != null;
        public int VisibleDroppedTimberCount => FindObjectsByType<
                Nature생존상호작용표식>(FindObjectsInactive.Exclude)
            .Count(value => value.Kind ==
                            Nature생존상호작용Kind.DroppedTimber);
        public bool UsesSyntyCabin => cabinPrefab != null;
        public bool UsesSyntyTimberLog => timberLogPrefab != null;
        public bool StoredTimberVisualVisible => storedTimberVisual != null
            && storedTimberVisual.activeInHierarchy;
        public bool SleepingVisualVisible => sleepBedVisual != null
            && sleepBedVisual.activeInHierarchy && sleepCampfireVisual != null
            && sleepCampfireVisual.activeInHierarchy;
        public bool DawnPlanVisualVisible => dawnPlansVisual != null
            && dawnPlansVisual.activeInHierarchy;
        public float ApprovedSurfaceWorldY => approvedSurfaceWorldY;
        public bool DiagnosticHudExpanded => diagnosticHudExpanded;
        public bool EquipmentPanelOpen => equipmentPanelOpen;
        public bool AssistedApproachActive => assistedApproachTarget != null;
        public bool WorkHoldRequested => workHoldRequested;
        public bool WorkbenchPlacementMode => workbenchPlacementMode;
        public long? LastSavedRevision => lastSavedRevision;
        public bool ThreatObservationConfirmed
            => engine is not INature위협관찰Engine observation
               || observation.ThreatObservationConfirmed;
        public Nature위협관찰PresentationDecision? ThreatObservation
            => threatObservation;
        public string CurrentCombatParticipationLabel
            => player.CurrentMode == 플레이어시점Mode.FirstPerson
                ? "1인칭 직접 개입" : "3인칭 관찰 운영";
        public event Action<string, long> EncounterFightRequested = delegate { };

        public void Configure(플레이어경관Controller playerController,
            Transform root, Transform origin, Nature생존State? initialState = null,
            INature생존Engine? authorityEngine = null,
            Func<string>? saveWorldAction = null,
            string profileLabel = "기본 Solo")
        {
            player = playerController;
            visualRoot = root;
            semanticOrigin = origin;
            approvedSurfaceWorldY = root.TransformPoint(Vector3.zero).y;
            engine = authorityEngine ?? new Nature생존LocalEngine(initialState
                ?? Nature생존LocalEngine.CreateDefaultFixture());
            saveWorld = saveWorldAction;
            launchProfileLabel = string.IsNullOrWhiteSpace(profileLabel)
                ? "기본 Solo" : profileLabel;
            diagnosticHudExpanded = launchProfileLabel.Contains("E7",
                StringComparison.OrdinalIgnoreCase);
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
            observeKeyWasHeld = false;
            exitKeyWasHeld = false;
            cancelKeyWasHeld = false;
            storeKeyWasHeld = false;
            sleepKeyWasHeld = false;
            plan1KeyWasHeld = false;
            plan2KeyWasHeld = false;
            plan3KeyWasHeld = false;
            diagnosticKeyWasHeld = false;
            inventoryKeyWasHeld = false;
            escapeKeyWasHeld = false;
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

        public void EquipAxeForTest()
        {
            if (engine is not IActor장착Engine actorEquipment)
                return;
            var state = actorEquipment.ActorEquipmentSnapshot();
            var axe = state.ItemInstances.First(value =>
                value.ItemCode == SimulationNatureSurvivalCodes.AxeItemCode);
            equipmentState = actorEquipment.Equip(axe.ItemInstanceStableId,
                SimulationActorEquipmentCodes.MainHand);
            RenderState();
        }

        /// <summary>
        /// Nature 탐험 진입 직후 아직 필요한 첫 행동 대상을 조준한다.
        /// 권위 상태는 읽기만 하며 카메라 표현 외에는 변경하지 않는다.
        /// </summary>
        public bool PreparePlayerEntryView()
        {
            if (!ValidateWiring()) return false;
            var state = engine.Snapshot();
            Nature생존상호작용표식? marker = null;
            if (!state.HasAxe && axeVisual != null && axeVisual.activeInHierarchy)
                marker = axeVisual.GetComponent<Nature생존상호작용표식>();
            marker ??= FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Exclude)
                .Where(value => value.Kind ==
                    Nature생존상호작용Kind.DroppedTimber)
                .OrderBy(value => Vector3.Distance(
                    player.transform.position, MarkerAimPoint(value)))
                .ThenBy(value => value.StableId, StringComparer.Ordinal)
                .FirstOrDefault();
            marker ??= FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Exclude)
                .Where(value => value.Kind == Nature생존상호작용Kind.HarvestTree)
                .OrderBy(value => Vector3.Distance(
                    player.transform.position, MarkerAimPoint(value)))
                .ThenBy(value => value.StableId, StringComparer.Ordinal)
                .FirstOrDefault();
            marker ??= ResolveNearestMarker(Nature생존상호작용Kind.Cabin);
            if (marker == null) return false;

            player.AimFirstPersonAtPresentationTarget(MarkerAimPoint(marker));
            status = marker.Kind == Nature생존상호작용Kind.Axe
                ? "안전 빈터 · 조준선의 도끼를 왼쪽 버튼으로 획득하세요."
                : marker.Kind == Nature생존상호작용Kind.HarvestTree
                    ? "수확 구역 · 조준선의 나무를 왼쪽 버튼으로 벌목하세요."
                    : marker.Kind == Nature생존상호작용Kind.DroppedTimber
                        ? "벌목 결과 · 조준선의 통나무 묶음을 왼쪽 버튼으로 주우세요."
                    : "생활 거점 · 조준선의 오두막을 왼쪽 버튼으로 사용하세요.";
            return true;
        }

        public void BeginHarvestForTest(string stableId) => BeginHarvest(stableId);

        public void CollectDroppedTimberForTest(string stableId)
        {
            engine.CollectDroppedTimber(stableId);
            RenderState();
        }

        public void BeginCabinBuildForTest()
        {
            engine.BeginCabinBuild();
            RenderState();
        }

        public void PlaceCabinBlueprintForTest(float localX, float localZ,
            float yawDegrees)
        {
            engine.PlaceCabinBlueprint(localX, localZ, yawDegrees);
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
            RecordLatestEngineInteractionPresentation();
        }

        public void SleepInCabinForTest()
        {
            engine.SleepInCabin();
            RenderState();
            RecordLatestEngineInteractionPresentation();
        }

        public void SelectExpansionPlanForTest(string planCode)
        {
            engine.SelectExpansionPlan(planCode);
            RenderState();
            RecordLatestEngineInteractionPresentation();
        }

        public void ConfirmThreatObservationForTest()
            => ConfirmThreatObservation();

        public void ResolveAuthoritativeFightVictoryForTest()
        {
            engine.ResolveEncounter(Nature생존Codes.Victory);
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
            var keyboard = Keyboard.current;
            var inventoryHeld = keyboard?.iKey.isPressed == true;
            var escapeHeld = keyboard?.escapeKey.isPressed == true;
            var inventoryPressed = inventoryHeld && !inventoryKeyWasHeld;
            var escapePressed = escapeHeld && !escapeKeyWasHeld;
            inventoryKeyWasHeld = inventoryHeld;
            escapeKeyWasHeld = escapeHeld;
            if (inventoryPressed)
                SetEquipmentPanelOpen(!equipmentPanelOpen);
            if (escapePressed && equipmentPanelOpen)
                SetEquipmentPanelOpen(false);
            var inputBlockReason = HigherPriorityInputReason();
            var higherPriority = !string.IsNullOrEmpty(inputBlockReason);
            if (!higherPriority) TickAssistedApproach();
            var inputMouse = State.ActiveWork != null && activeWorkMouse != null
                ? activeWorkMouse : Mouse.current;
            var physicalMouseHeld = inputMouse?.leftButton.isPressed == true;
            var mouseHeld = physicalMouseHeld
                            || workHoldRequested && State.ActiveWork != null;
            var primaryPressed = physicalMouseHeld && !primaryWasHeld;
            primaryWasHeld = physicalMouseHeld;
            var paused = (!UnityEngine.Application.isFocused
                          && !UnityEngine.Application.isBatchMode)
                         || Time.timeScale <= 0f;
            realtimeAccumulator += Time.unscaledDeltaTime;
            while (realtimeAccumulator >= 1f)
            {
                realtimeAccumulator -= 1f;
                engine.AdvanceRealtime(1, mouseHeld && !higherPriority, paused);
                RenderState();
                if (State.ActiveWork == null)
                {
                    activeWorkMouse = null;
                    workHoldRequested = false;
                }
            }

            var buildHeld = keyboard?.bKey.isPressed == true;
            var fightHeld = keyboard?.fKey.isPressed == true;
            var retreatHeld = keyboard?.rKey.isPressed == true;
            var observeHeld = keyboard?.oKey.isPressed == true;
            var exitHeld = keyboard?.eKey.isPressed == true;
            var cancelHeld = keyboard?.cKey.isPressed == true;
            var storeHeld = keyboard?.gKey.isPressed == true;
            var sleepHeld = keyboard?.tKey.isPressed == true;
            var plan1Held = keyboard?.digit1Key.isPressed == true;
            var plan2Held = keyboard?.digit2Key.isPressed == true;
            var plan3Held = keyboard?.digit3Key.isPressed == true;
            var diagnosticHeld = keyboard?.f8Key.isPressed == true;
            var buildPressed = buildHeld && !buildKeyWasHeld;
            var fightPressed = fightHeld && !fightKeyWasHeld;
            var retreatPressed = retreatHeld && !retreatKeyWasHeld;
            var observePressed = observeHeld && !observeKeyWasHeld;
            var exitPressed = exitHeld && !exitKeyWasHeld;
            var cancelPressed = cancelHeld && !cancelKeyWasHeld;
            var storePressed = storeHeld && !storeKeyWasHeld;
            var sleepPressed = sleepHeld && !sleepKeyWasHeld;
            var plan1Pressed = plan1Held && !plan1KeyWasHeld;
            var plan2Pressed = plan2Held && !plan2KeyWasHeld;
            var plan3Pressed = plan3Held && !plan3KeyWasHeld;
            var diagnosticPressed = diagnosticHeld && !diagnosticKeyWasHeld;
            buildKeyWasHeld = buildHeld;
            fightKeyWasHeld = fightHeld;
            retreatKeyWasHeld = retreatHeld;
            observeKeyWasHeld = observeHeld;
            exitKeyWasHeld = exitHeld;
            cancelKeyWasHeld = cancelHeld;
            storeKeyWasHeld = storeHeld;
            sleepKeyWasHeld = sleepHeld;
            plan1KeyWasHeld = plan1Held;
            plan2KeyWasHeld = plan2Held;
            plan3KeyWasHeld = plan3Held;
            diagnosticKeyWasHeld = diagnosticHeld;
            if (diagnosticPressed)
            {
                diagnosticHudExpanded = !diagnosticHudExpanded;
                status = diagnosticHudExpanded
                    ? "진단 상태판 펼침 · F8로 다시 접을 수 있습니다."
                    : "진단 상태판 접힘 · 플레이 문맥 안내만 표시합니다.";
            }
            if (buildPressed && !higherPriority)
                BeginCabinPlacementMode();
            if (observePressed && !higherPriority
                && State.Encounter?.StateCode == Nature생존Codes.Pending)
                ConfirmThreatObservation();
            if (fightPressed
                && (State.Encounter?.StateCode == Nature생존Codes.Pending
                    || State.Encounter?.StateCode == Nature생존Codes.CombatActive))
            {
                if (!ThreatObservationConfirmed)
                {
                    status = "전투 선택 차단 · O 키로 위협을 먼저 관찰하세요.";
                    return;
                }
                var encounterId = State.Encounter!.StableId;
                var isRetry = State.Encounter.StateCode
                              == Nature생존Codes.CombatActive;
                if (!isRetry)
                {
                    engine.ResolveEncounter(Nature생존Codes.Fight);
                    RenderState();
                }
                status = isRetry
                    ? "전투 인계 재시도 · 기존 전투 권위의 결과를 기다립니다."
                    : "전투 대응 요청 · 기존 전투 권위의 결과를 기다립니다.";
                EncounterFightRequested(encounterId, State.Revision);
            }
            if (retreatPressed
                && State.Encounter?.StateCode == Nature생존Codes.Pending)
            {
                if (!ThreatObservationConfirmed)
                {
                    status = "후퇴 선택 차단 · O 키로 위협을 먼저 관찰하세요.";
                    return;
                }
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
                if (plan1Pressed && State.Day2Ready
                    && State.SelectedExpansionPlanCode ==
                    Nature생존Codes.Workbench)
                    BeginWorkbenchPlacementMode();
                else
                    RunCabinAction(() => engine.SelectExpansionPlan(plan),
                        "Day 2 계획 고정 · " + plan);
            }
            if (higherPriority)
            {
                if (primaryPressed && equipmentPanelOpen
                    && TryHandleEquipmentPrimaryPressed())
                    return;
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
            if (!diagnosticHudExpanded)
            {
                if (!EquipmentToggleButtonGuiRect().Contains(point))
                    return false;
                equipmentPointerHandledFrame = Time.frameCount;
                SetEquipmentPanelOpen(!equipmentPanelOpen);
                return true;
            }
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
            if (ApproachButtonGuiRect().Contains(point))
            {
                BeginAssistedApproach();
                return true;
            }
            if (BlueprintButtonGuiRect().Contains(point))
            {
                HandleCabinHudAction();
                return true;
            }
            if (WorkHoldButtonGuiRect().Contains(point))
            {
                ToggleWorkHold();
                return true;
            }
            return false;
        }

        private bool TryHandleEquipmentPrimaryPressed()
        {
            if (Mouse.current == null
                || engine is not IActor장착Engine actorEquipment)
                return false;
            var point = ToGuiPoint(Mouse.current.position.ReadValue());
            if (!EquipmentPanelGuiRect().Contains(point)) return false;

            equipmentState ??= actorEquipment.ActorEquipmentSnapshot();
            var state = equipmentState;
            for (var index = 0; index < state.Slots.Length; index++)
            {
                var slot = state.Slots[index];
                if (string.IsNullOrWhiteSpace(
                        slot.EquippedItemInstanceStableId)
                    || !EquipmentSlotButtonGuiRect(index).Contains(point))
                    continue;
                equipmentPointerHandledFrame = Time.frameCount;
                TryChangeEquipment(() => actorEquipment.Unequip(
                    slot.EquippedItemInstanceStableId), "장착 해제");
                return true;
            }

            var inventory = state.ItemInstances.Where(value =>
                    value.LocationCode ==
                    SimulationActorEquipmentCodes.Inventory)
                .ToArray();
            for (var index = 0; index < inventory.Length; index++)
            {
                if (!EquipmentInventoryButtonGuiRect(index).Contains(point))
                    continue;
                equipmentPointerHandledFrame = Time.frameCount;
                var item = inventory[index];
                var definition = state.ItemDefinitions.First(value =>
                    value.ItemDefinitionStableId ==
                    item.ItemDefinitionStableId);
                var targetSlot = definition.AllowedSlotCodes.FirstOrDefault()
                                 ?? SimulationActorEquipmentCodes.MainHand;
                TryChangeEquipment(() => actorEquipment.Equip(
                    item.ItemInstanceStableId, targetSlot), "장착 완료");
                return true;
            }

            return true;
        }

        private bool PointerOverHud()
        {
            if (Mouse.current == null) return false;
            if (플레이어경관Controller.IsPointerOverUnityUi()) return true;
            if (플레이어경관Controller.IsPointerInPersistentModeBand(
                    Mouse.current.position.ReadValue(), Screen.height)) return true;
            if (플레이어경관Controller.IsPointerInPersistentHudControls(
                    Mouse.current.position.ReadValue(), Screen.width,
                    Screen.height)) return true;
            if (player.CurrentMode == 플레이어시점Mode.FirstPerson) return false;
            if (Screen.width < 560 || Screen.height < 200) return false;
            if (equipmentPanelOpen && EquipmentPanelGuiRect().Contains(
                    ToGuiPoint(Mouse.current.position.ReadValue()))) return true;
            return HudGuiRect().Contains(ToGuiPoint(
                Mouse.current.position.ReadValue()));
        }

        private static Vector2 ToGuiPoint(Vector2 inputSystemPoint)
            => new(inputSystemPoint.x, Screen.height - inputSystemPoint.y);

        private Rect HudGuiRect()
            => new(16f, 16f, 430f,
                diagnosticHudExpanded ? 240f : 140f);

        private static Rect SaveButtonGuiRect()
            => new(28f, 154f, 120f, 26f);

        private static Rect CancelButtonGuiRect()
            => new(156f, 154f, 140f, 26f);

        private static Rect ApproachButtonGuiRect()
            => new(304f, 154f, 120f, 26f);

        private static Rect BlueprintButtonGuiRect()
            => new(28f, 184f, 120f, 26f);

        private static Rect WorkHoldButtonGuiRect()
            => new(156f, 184f, 140f, 26f);

        private static Rect EquipmentPanelGuiRect()
            => new(Mathf.Max(16f, Screen.width - 470f), 16f, 450f, 520f);

        private static Rect EquipmentToggleButtonGuiRect()
            => new(28f, 112f, 140f, 26f);

        private static Rect EquipmentSlotButtonGuiRect(int slotIndex)
        {
            var panel = EquipmentPanelGuiRect();
            return new Rect(panel.x + 215f,
                panel.y + 72f + 24f + slotIndex * 25f, 92f, 22f);
        }

        private static Rect EquipmentInventoryButtonGuiRect(int itemIndex)
        {
            var panel = EquipmentPanelGuiRect();
            return new Rect(panel.x + 250f,
                panel.y + 72f + 24f + 8f * 25f + 8f + 24f
                + itemIndex * 25f, 92f, 22f);
        }

        private void HandleCabinHudAction()
        {
            var state = State;
            if (state.ActiveWork != null)
            {
                status = "오두막 행동 대기 · 진행 작업을 먼저 끝내거나 취소하세요.";
                return;
            }
            if (state.PlayerInsideCabin)
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
                return;
            }
            if (state.Cabin.StateCode == Nature생존Codes.Planned)
            {
                BeginCabinPlacementMode();
                return;
            }

            var cabinMarker = ResolveNearestMarker(
                Nature생존상호작용Kind.Cabin);
            if (cabinMarker == null)
            {
                status = "오두막 접근 대기 · 배치 표현을 찾을 수 없습니다.";
                return;
            }
            cabinPlacementMode = false;
            assistedApproachTarget = cabinMarker;
            status = "오두막 접근 중 · 남은 나무보다 생활 거점을 우선합니다.";
        }

        private void BeginCabinPlacementMode()
        {
            if (State.ActiveWork != null)
            {
                status = "오두막 도면 배치 대기 · 진행 작업을 먼저 끝내거나 취소하세요.";
                return;
            }
            cabinPlacementMode = true;
            assistedApproachTarget = null;
            status = "오두막 도면 배치 · 안전 빈터 안의 지면을 클릭하세요.";
        }

        private void BeginWorkbenchPlacementMode()
        {
            if (State.ActiveWork != null)
            {
                status = "작업대 배치 대기 · 진행 작업을 먼저 끝내거나 취소하세요.";
                return;
            }
            if (State.Workbench.StateCode == Nature생존Codes.Operational)
            {
                status = "작업대 운영 중 · 현장 제작을 선택할 수 있습니다.";
                return;
            }
            workbenchPlacementMode = true;
            cabinPlacementMode = false;
            assistedApproachTarget = null;
            status = "작업대 Preview · 통나무 4개와 재건 부품 1개 · 지면을 클릭하세요.";
        }

        private void BeginAssistedApproach()
        {
            if (State.ActiveWork != null)
            {
                assistedApproachTarget = null;
                status = "목표 접근 대기 · 진행 작업을 먼저 끝내거나 취소하세요.";
                return;
            }
            assistedApproachTarget = ResolveNextApproachTarget();
            if (assistedApproachTarget == null)
            {
                status = "목표 접근 대기 · 현재 접근할 배치 객체가 없습니다.";
                return;
            }
            cabinPlacementMode = false;
            status = "다음 목표 접근 중 · 이동과 조준은 Unity 표현만 변경합니다.";
        }

        private Nature생존상호작용표식? ResolveNextApproachTarget()
        {
            var state = State;
            if (!state.HasAxe && axeVisual != null && axeVisual.activeInHierarchy)
                return axeVisual.GetComponent<Nature생존상호작용표식>();
            var droppedTimber = FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Exclude)
                .Where(value => value.Kind ==
                    Nature생존상호작용Kind.DroppedTimber)
                .OrderBy(value => Vector3.Distance(player.transform.position,
                    MarkerAimPoint(value)))
                .ThenBy(value => value.StableId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (droppedTimber != null) return droppedTimber;
            var tree = FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Exclude)
                .Where(value => value.Kind == Nature생존상호작용Kind.HarvestTree)
                .OrderBy(value => Vector3.Distance(player.transform.position,
                    MarkerAimPoint(value)))
                .ThenBy(value => value.StableId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (tree != null) return tree;
            return ResolveNearestMarker(Nature생존상호작용Kind.Cabin);
        }

        private Nature생존상호작용표식? ResolveNearestMarker(
            Nature생존상호작용Kind kind)
            => FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Exclude)
                .Where(value => value.Kind == kind)
                .OrderBy(value => Vector3.Distance(player.transform.position,
                    MarkerAimPoint(value)))
                .ThenBy(value => value.StableId, StringComparer.Ordinal)
                .FirstOrDefault();

        private void TickAssistedApproach()
        {
            if (assistedApproachTarget == null) return;
            if (!assistedApproachTarget.gameObject.activeInHierarchy)
            {
                assistedApproachTarget = null;
                return;
            }
            var stopDistance = assistedApproachTarget.Kind ==
                               Nature생존상호작용Kind.Cabin
                ? Mathf.Max(1f, interactionDistance * .94f)
                : Mathf.Max(1f, interactionDistance * .72f);
            var targetPoint = MarkerApproachPoint(assistedApproachTarget);
            var horizontalDistance = Vector2.Distance(
                new Vector2(player.transform.position.x,
                    player.transform.position.z),
                new Vector2(targetPoint.x, targetPoint.z));
            if (assistedApproachTarget.Kind ==
                    Nature생존상호작용Kind.Cabin
                && horizontalDistance < stopDistance)
            {
                var away = player.transform.position - targetPoint;
                away.y = 0f;
                if (away.sqrMagnitude < .01f)
                    away = -assistedApproachTarget.transform.forward;
                var desired = targetPoint + away.normalized * stopDistance;
                desired.y = player.transform.position.y;
                var character = player.GetComponent<CharacterController>();
                if (character != null) character.enabled = false;
                player.transform.position = desired;
                if (character != null) character.enabled = true;
                horizontalDistance = stopDistance;
            }
            var arrived = horizontalDistance <= stopDistance
                          || player.TickPresentationApproach(
                              targetPoint, stopDistance,
                              Time.unscaledDeltaTime,
                              assistedApproachTarget.Kind ==
                              Nature생존상호작용Kind.Cabin);
            if (!arrived) return;
            status = assistedApproachTarget.Kind == Nature생존상호작용Kind.HarvestTree
                ? "나무 접근 완료 · 조준선에서 왼쪽 버튼으로 벌목하세요."
                : assistedApproachTarget.Kind ==
                  Nature생존상호작용Kind.DroppedTimber
                    ? "통나무 접근 완료 · 조준선에서 왼쪽 버튼으로 주우세요."
                : assistedApproachTarget.Kind == Nature생존상호작용Kind.Cabin
                    ? "오두막 접근 완료 · 조준선에서 왼쪽 버튼으로 사용하세요."
                    : "도끼 접근 완료 · 조준선에서 왼쪽 버튼으로 획득하세요.";
            assistedApproachTarget = null;
        }

        private void ToggleWorkHold()
        {
            if (State.ActiveWork == null)
            {
                workHoldRequested = false;
                status = "작업 유지 대기 · 조준선에서 작업을 먼저 시작하세요.";
                return;
            }
            workHoldRequested = !workHoldRequested;
            status = workHoldRequested
                ? "작업 유지 켬 · C 또는 진행 작업 취소 버튼으로 중단할 수 있습니다."
                : "작업 유지 끔 · 진행하려면 왼쪽 버튼을 누르세요.";
        }

        private void SetEquipmentPanelOpen(bool open)
        {
            equipmentPanelOpen = open;
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (open && engine is IActor장착Engine actorEquipment)
                equipmentState = actorEquipment.ActorEquipmentSnapshot();
            status = open
                ? "장착 상태창 · 물품을 선택해 권위 Preview/Confirm으로 장착합니다."
                : "장착 상태창 닫힘 · 세계 시간은 계속 흐릅니다.";
        }

        private void DrawEquipmentPanel()
        {
            if (!equipmentPanelOpen) return;
            var rect = EquipmentPanelGuiRect();
            GUI.color = new Color(.045f, .055f, .075f, .96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 14f, 410f, 22f),
                "캐릭터 상태 · 장착 (I 열기/닫기 · Esc 닫기)");
            if (engine is not IActor장착Engine actorEquipment)
            {
                GUI.Label(new Rect(rect.x + 16f, rect.y + 44f, 410f, 22f),
                    "현재 실행 방식에는 보편 장착 Runtime이 연결되지 않았습니다.");
                return;
            }

            equipmentState ??= actorEquipment.ActorEquipmentSnapshot();
            var state = equipmentState;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 42f, 410f, 20f),
                $"장착 판본 {state.EquipmentRevision} · 능력 "
                + (state.CapabilityCodes.Length == 0 ? "없음"
                    : string.Join(", ", state.CapabilityCodes)));
            var y = rect.y + 72f;
            GUI.Label(new Rect(rect.x + 16f, y, 200f, 20f), "전신 장착 슬롯");
            y += 24f;
            foreach (var slot in state.Slots)
            {
                var item = state.ItemInstances.FirstOrDefault(value =>
                    value.ItemInstanceStableId ==
                    slot.EquippedItemInstanceStableId);
                GUI.Label(new Rect(rect.x + 20f, y, 185f, 22f),
                    $"{SlotKorean(slot.SlotCode)} · "
                    + (item?.KoreanName ?? "비어 있음"));
                if (item != null && GUI.Button(EquipmentSlotButtonGuiRect(
                        Array.IndexOf(state.Slots, slot)), "해제")
                    && equipmentPointerHandledFrame != Time.frameCount)
                    TryChangeEquipment(() => actorEquipment.Unequip(
                        item.ItemInstanceStableId), "장착 해제");
                y += 25f;
            }

            y += 8f;
            GUI.Label(new Rect(rect.x + 16f, y, 200f, 20f), "소지 물품");
            y += 24f;
            var inventory = state.ItemInstances.Where(value =>
                    value.LocationCode ==
                    SimulationActorEquipmentCodes.Inventory)
                .ToArray();
            for (var index = 0; index < inventory.Length; index++)
            {
                var item = inventory[index];
                GUI.Label(new Rect(rect.x + 20f, y, 225f, 22f),
                    item.KoreanName);
                if (GUI.Button(EquipmentInventoryButtonGuiRect(index), "장착")
                    && equipmentPointerHandledFrame != Time.frameCount)
                {
                    var definition = state.ItemDefinitions.First(value =>
                        value.ItemDefinitionStableId ==
                        item.ItemDefinitionStableId);
                    var targetSlot = definition.AllowedSlotCodes.FirstOrDefault()
                                     ?? SimulationActorEquipmentCodes.MainHand;
                    TryChangeEquipment(() => actorEquipment.Equip(
                        item.ItemInstanceStableId, targetSlot), "장착 완료");
                }
                y += 25f;
            }
        }

        private void TryChangeEquipment(
            Func<SimulationActorEquipmentStateSnapshot> change,
            string successMessage)
        {
            try
            {
                equipmentState = change();
                status = successMessage + " · 권위 상태를 다시 읽었습니다.";
                SyncEquippedAxeVisual();
            }
            catch (InvalidOperationException error)
            {
                status = "장착 변경 차단 · " + error.Message;
            }
        }

        private static string SlotKorean(string slotCode) => slotCode switch
        {
            SimulationActorEquipmentCodes.MainHand => "주손",
            SimulationActorEquipmentCodes.OffHand => "보조손",
            SimulationActorEquipmentCodes.Head => "머리",
            SimulationActorEquipmentCodes.Body => "몸",
            SimulationActorEquipmentCodes.Legs => "다리",
            SimulationActorEquipmentCodes.Feet => "발",
            SimulationActorEquipmentCodes.Back => "등",
            SimulationActorEquipmentCodes.Accessory => "장신구",
            _ => slotCode,
        };

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
            if (workbenchPlacementMode)
            {
                var ray = camera.ScreenPointToRay(Mouse.current!.position.ReadValue());
                if (Physics.Raycast(ray, out var groundHit, 100f,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    var local = semanticOrigin.InverseTransformPoint(groundHit.point);
                    var yaw = player.transform.eulerAngles.y;
                    try
                    {
                        var preview = engine.PreviewWorkbench(local.x, local.z, yaw);
                        if (!preview.CanConfirm)
                        {
                            status = "작업대 Preview 차단 · " + string.Join(", ",
                                preview.BlockingReasonCodes);
                            return;
                        }
                        engine.BeginWorkbenchConstruction(local.x, local.z, yaw);
                        workbenchPlacementMode = false;
                        activeWorkMouse = Mouse.current;
                        RenderState();
                        status = $"작업대 건설 중 · 통나무 {preview.RequiredTimber}, "
                                 + $"부품 {preview.RequiredRebuildParts} 예약 · "
                                 + $"{preview.RequiredWorkSeconds}초 동안 누르세요.";
                    }
                    catch (InvalidOperationException error)
                    {
                        status = "작업대 배치 차단 · " + error.Message;
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
                    equipmentState = engine is IActor장착Engine actorEquipment
                        ? actorEquipment.ActorEquipmentSnapshot() : null;
                    status = "기본 도끼 획득 · I 상태창에서 주손에 장착하세요.";
                }
                else if (marker.Kind == Nature생존상호작용Kind.HarvestTree)
                    BeginHarvest(marker.StableId);
                else if (marker.Kind == Nature생존상호작용Kind.DroppedTimber)
                {
                    engine.CollectDroppedTimber(marker.StableId);
                    status = "통나무 묶음 획득 · 추가 벌목 또는 오두막 건설을 선택하세요.";
                }
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
                else if (marker.Kind == Nature생존상호작용Kind.Workbench)
                    status = State.Workbench.StateCode ==
                             Nature생존Codes.Operational
                        ? "작업대 운영 가능 · 다음 제작 WI를 선택할 수 있습니다."
                        : "작업대 건설 상태를 확인하세요.";
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
                workHoldRequested = false;
                RenderState();
                status = kindCode == Nature생존Codes.CabinBuild
                    ? "건설 취소 · 예약 통나무를 인벤토리에 반환했습니다."
                    : kindCode == Nature생존Codes.AreaBuildingConstruction
                        ? "작업대 건설 취소 · 예약 통나무와 재건 부품을 반환했습니다."
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
                RecordLatestEngineInteractionPresentation();
                status = successStatus;
            }
            catch (InvalidOperationException error)
            {
                status = "행동 차단 · " + error.Message;
            }
        }

        private void RecordLatestEngineInteractionPresentation()
        {
            if (engine is not INature생존EngineTraceSource traceSource
                || traceSource.LastEngineInteractionTrace.Length == 0)
                return;
            var coordinator = FindFirstObjectByType<
                World공간표현조립Coordinator>(FindObjectsInactive.Include);
            coordinator?.RecordNatureInteractionPresentation(
                traceSource.LastEngineInteractionTrace);
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

        private static Vector3 MarkerApproachPoint(
            Nature생존상호작용표식 marker)
        {
            var point = MarkerAimPoint(marker);
            if (marker.Kind != Nature생존상호작용Kind.Cabin) return point;

            point.x = marker.transform.position.x;
            point.z = marker.transform.position.z;
            return point;
        }

        private string HigherPriorityInputReason()
        {
            if (equipmentPanelOpen) return "장착 상태창 사용 중";
            if (!CanUseNatureInput(player.CurrentActivityCode))
                return "Nature 탐험 문맥 대기";
            if (player.Combat != null && player.Combat.HasActiveUnifiedBattle)
                return "전투 입력 사용 중";
            var placement = FindFirstObjectByType<Farm플레이어배치Controller>(
                FindObjectsInactive.Include);
            return placement != null && placement.PlacementModeActive
                ? "Farm 배치 입력 사용 중" : string.Empty;
        }

        public static bool CanUseNatureInput(string activityCode)
            => string.Equals(activityCode, PlayerActivityCodes.Exploration,
                StringComparison.Ordinal);

        private void RebuildVisuals()
        {
            foreach (Transform child in visualRoot) DestroyRuntimeSafe(child.gameObject);
            nodeVisuals.Clear();
            stumpVisuals.Clear();
            droppedTimberVisuals.Clear();
            var state = engine.Snapshot();
            axeVisual = CreateVisual(axePrefab, PrimitiveType.Cube, "Nature_AxePickup",
                new Vector3(1.5f, .35f, 1.5f), new Vector3(.8f, .15f, .18f));
            AlignGroundVisual(axeVisual);
            var axeMarker = axeVisual.AddComponent<Nature생존상호작용표식>();
            axeMarker.Kind = Nature생존상호작용Kind.Axe;
            axeMarker.StableId = "pickup:nature-safe-clearing:basic-axe";
            axeMarker.UsesSyntyAsset = axePrefab != null;
            axeMarker.VisualSourceStableId = axePrefab != null
                ? "asset:synty:polygon-generic:sm-gen-wep-axe-01"
                : "fallback:primitive:cube";
            if (!UsesSeparatedWorldAssetPlacement())
            foreach (var node in state.ResourceNodes)
            {
                var visual = CreateVisual(treePrefab, PrimitiveType.Cylinder,
                    "Nature_HarvestTree_" + node.StableId,
                    new Vector3(node.LocalX, 0f, node.LocalZ), Vector3.one);
                AlignGroundVisual(visual);
                var marker = visual.AddComponent<Nature생존상호작용표식>();
                marker.Kind = Nature생존상호작용Kind.HarvestTree;
                marker.StableId = node.StableId;
                nodeVisuals.Add(node.StableId, visual);
                var stump = CreateVisual(stumpPrefab, PrimitiveType.Cylinder,
                    "Nature_HarvestStump_" + node.StableId,
                    new Vector3(node.LocalX, 0f, node.LocalZ),
                    stumpPrefab != null ? Vector3.one : new Vector3(1f, .25f, 1f));
                AlignGroundVisual(stump);
                stump.SetActive(false);
                stumpVisuals.Add(node.StableId, stump);
            }
            RenderState();
        }

        private void RenderState()
        {
            var state = engine.Snapshot();
            if (axeVisual != null) axeVisual.SetActive(!state.HasAxe);
            var separatedWorldPlacement = UsesSeparatedWorldAssetPlacement();
            if (separatedWorldPlacement)
                MigrateLegacyWorldVisuals();
            else
            {
                foreach (var node in state.ResourceNodes)
                {
                    if (!nodeVisuals.TryGetValue(node.StableId,
                            out var visual)) continue;
                    if (node.StateCode == Nature생존Codes.Standing)
                    {
                        visual.SetActive(true);
                        if (stumpVisuals.TryGetValue(node.StableId,
                                out var stump)) stump.SetActive(false);
                    }
                    else
                    {
                        visual.SetActive(false);
                        if (stumpVisuals.TryGetValue(node.StableId,
                                out var stump)) stump.SetActive(true);
                    }
                }
                RenderDroppedTimber(state);
                RenderCabin(state);
                RenderStoredTimber(state);
                RenderWorkbench(state);
            }
            RenderNightAndDawn(state);
            RenderEncounter(state);
            SyncEquippedAxeVisual();
            var lh = FindFirstObjectByType<공간LHStreamingEngine>(
                FindObjectsInactive.Include);
            lh?.NotifyAuthorityRevision(state.Revision);
        }

        private void SyncEquippedAxeVisual()
        {
            if (engine is not IActor장착Engine actorEquipment || player == null)
                return;
            equipmentState = actorEquipment.ActorEquipmentSnapshot();
            var axeEquipped = equipmentState.ItemInstances.Any(value =>
                value.ItemCode == SimulationNatureSurvivalCodes.AxeItemCode
                && value.LocationCode == SimulationActorEquipmentCodes.Equipped);
            if (!axeEquipped)
            {
                if (equippedAxeVisual != null)
                    DestroyRuntimeSafe(equippedAxeVisual);
                equippedAxeVisual = null;
                return;
            }
            if (equippedAxeVisual != null) return;
            equippedAxeVisual = axePrefab != null
                ? Instantiate(axePrefab) : GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
            equippedAxeVisual.name = "Player_Equipped_MainHand_Axe";
            equippedAxeVisual.transform.SetParent(player.transform, false);
            equippedAxeVisual.transform.localPosition = new Vector3(
                .42f, 1.05f, .28f);
            equippedAxeVisual.transform.localRotation =
                Quaternion.Euler(12f, 0f, -68f);
            equippedAxeVisual.transform.localScale = axePrefab != null
                ? Vector3.one : new Vector3(.08f, .65f, .08f);
            foreach (var collider in equippedAxeVisual
                         .GetComponentsInChildren<Collider>(true))
                DestroyRuntimeSafe(collider);
        }

        private static bool UsesSeparatedWorldAssetPlacement()
            => FindFirstObjectByType<실외자산배치Runtime>(
                FindObjectsInactive.Include) != null;

        private void MigrateLegacyWorldVisuals()
        {
            if (legacyWorldVisualsMigrated) return;
            foreach (var value in nodeVisuals.Values)
                if (value != null) DestroyRuntimeSafe(value);
            foreach (var value in stumpVisuals.Values)
                if (value != null) DestroyRuntimeSafe(value);
            foreach (var value in droppedTimberVisuals.Values)
                if (value != null) DestroyRuntimeSafe(value);
            nodeVisuals.Clear();
            stumpVisuals.Clear();
            droppedTimberVisuals.Clear();
            DestroyLegacyWorldVisual(ref cabinVisual);
            DestroyLegacyWorldVisual(ref workbenchVisual);
            DestroyLegacyWorldVisual(ref storedTimberVisual);
            DestroyLegacyWorldVisual(ref storedTimberCompanionVisual);
            DestroyLegacyWorldVisual(ref storedTimberStoragePadVisual);
            if (storedTimberWorldLabel != null)
                DestroyRuntimeSafe(storedTimberWorldLabel.gameObject);
            if (cabinStateWorldLabel != null)
                DestroyRuntimeSafe(cabinStateWorldLabel.gameObject);
            storedTimberWorldLabel = null;
            cabinStateWorldLabel = null;
            legacyWorldVisualsMigrated = true;
        }

        private static void DestroyLegacyWorldVisual(ref GameObject? value)
        {
            if (value != null) DestroyRuntimeSafe(value);
            value = null;
        }

        private void RenderStoredTimber(Nature생존State state)
        {
            var visible = state.Cabin.StateCode == Nature생존Codes.Completed
                          && state.StoredTimberQuantity > 0;
            if (!visible)
            {
                if (storedTimberVisual != null) storedTimberVisual.SetActive(false);
                if (storedTimberCompanionVisual != null)
                    storedTimberCompanionVisual.SetActive(false);
                if (storedTimberStoragePadVisual != null)
                    storedTimberStoragePadVisual.SetActive(false);
                if (storedTimberWorldLabel != null)
                    storedTimberWorldLabel.gameObject.SetActive(false);
                return;
            }

            storedTimberVisual ??= CreateVisual(storedTimberPrefab,
                PrimitiveType.Cylinder, "Nature_Cabin_StoredTimber",
                Vector3.zero, storedTimberPrefab != null
                    ? Vector3.one : new Vector3(.45f, 1.25f, .45f));
            storedTimberCompanionVisual ??= CreateVisual(storedTimberPrefab,
                PrimitiveType.Cylinder, "Nature_Cabin_StoredTimber_Companion",
                Vector3.zero, storedTimberPrefab != null
                    ? Vector3.one : new Vector3(.45f, 1.25f, .45f));
            storedTimberStoragePadVisual ??= CreateVisual(null!,
                PrimitiveType.Cube, "Nature_Cabin_StoredTimber_Pad",
                Vector3.zero, new Vector3(4.2f, .16f, 2.2f));
            if (storedTimberStoragePadVisual.TryGetComponent<Renderer>(out var padRenderer))
                padRenderer.material.color = new Color(.43f, .24f, .09f);

            storedTimberVisual.SetActive(true);
            storedTimberCompanionVisual.SetActive(true);
            storedTimberStoragePadVisual.SetActive(true);
            var cabinRotation = Quaternion.Euler(0f, state.Cabin.YawDegrees, 0f);
            var storageCenter = new Vector3(state.Cabin.LocalX, 0f,
                state.Cabin.LocalZ)
                + cabinRotation * new Vector3(3.8f, 0f, -.65f);
            storedTimberVisual.transform.localPosition = storageCenter
                + cabinRotation * new Vector3(0f, 0f, -.55f);
            storedTimberVisual.transform.localRotation = cabinRotation;
            storedTimberCompanionVisual.transform.localPosition = storageCenter
                + cabinRotation * new Vector3(0f, 0f, .55f);
            storedTimberCompanionVisual.transform.localRotation = cabinRotation;
            storedTimberStoragePadVisual.transform.localPosition = storageCenter;
            storedTimberStoragePadVisual.transform.localRotation = cabinRotation;
            var amountScale = Mathf.Lerp(1.05f, 1.5f,
                Mathf.Clamp01(state.StoredTimberQuantity / 20f));
            storedTimberVisual.transform.localScale = Vector3.one * amountScale;
            storedTimberCompanionVisual.transform.localScale = Vector3.one
                * Mathf.Max(1f, amountScale * .9f);
            AlignGroundVisual(storedTimberStoragePadVisual);
            if (배치객체표면정렬Utility.TryGetVisibleBounds(
                    storedTimberStoragePadVisual.transform, out var padBounds))
            {
                AlignVisualToWorldSurface(storedTimberVisual, padBounds.max.y);
                AlignVisualToWorldSurface(storedTimberCompanionVisual,
                    padBounds.max.y);
            }

            storedTimberWorldLabel ??= CreateWorldLabel(
                "Nature_Cabin_StoredTimber_Label",
                new Color(.95f, .78f, .38f));
            var storageLabelVisible = !state.Sleeping
                && state.ClockPhaseCode != NatureSurvivalClockPhaseCodes.Dawn;
            storedTimberWorldLabel.gameObject.SetActive(storageLabelVisible);
            var storageLabelWorldY = visualRoot.TransformPoint(storageCenter).y
                                     + 1.55f;
            if (배치객체표면정렬Utility.TryGetVisibleBounds(
                    storedTimberVisual.transform, out var storedBounds))
                storageLabelWorldY = storedBounds.max.y + .55f;
            storedTimberWorldLabel.transform.position = new Vector3(
                storedTimberWorldLabel.transform.position.x,
                storageLabelWorldY,
                storedTimberWorldLabel.transform.position.z);
            var storageLabelLocalPosition = storageCenter;
            storageLabelLocalPosition.y = visualRoot.InverseTransformPoint(
                storedTimberWorldLabel.transform.position).y;
            storedTimberWorldLabel.transform.localPosition =
                storageLabelLocalPosition;
            storedTimberWorldLabel.text =
                $"오두막 보관 목재 · {state.StoredTimberQuantity}/20";
        }

        private void RenderNightAndDawn(Nature생존State state)
        {
            var cabinCompleted = state.Cabin.StateCode == Nature생존Codes.Completed;
            var sleeping = cabinCompleted && state.Sleeping;
            var dawn = cabinCompleted && state.ClockPhaseCode ==
                NatureSurvivalClockPhaseCodes.Dawn;
            var showDawnPlans = dawn
                && state.Workbench.StateCode != Nature생존Codes.Building
                && state.Workbench.StateCode != Nature생존Codes.Operational;
            var cabinRotation = Quaternion.Euler(0f, state.Cabin.YawDegrees, 0f);
            var cabinPosition = new Vector3(state.Cabin.LocalX, 0f,
                state.Cabin.LocalZ);

            if (sleeping && sleepBedVisual == null)
                sleepBedVisual = CreateVisual(sleepBedPrefab, PrimitiveType.Cube,
                    "Nature_Cabin_SleepBed", Vector3.zero,
                    sleepBedPrefab != null ? Vector3.one
                        : new Vector3(1.8f, .35f, .8f));
            if (sleepBedVisual != null)
            {
                sleepBedVisual.SetActive(sleeping);
                sleepBedVisual.transform.localPosition = cabinPosition
                    + cabinRotation * new Vector3(-1f, 0f, -3.4f);
                sleepBedVisual.transform.localRotation = cabinRotation;
                sleepBedVisual.transform.localScale = Vector3.one * .85f;
                if (sleeping) AlignGroundVisual(sleepBedVisual);
            }

            if (sleeping && sleepCampfireVisual == null)
            {
                sleepCampfireVisual = CreateVisual(sleepCampfirePrefab,
                    PrimitiveType.Cylinder, "Nature_Cabin_SleepWarmLight",
                    Vector3.zero, sleepCampfirePrefab != null
                        ? new Vector3(.65f, .65f, .65f)
                        : new Vector3(.55f, .15f, .55f));
                var light = sleepCampfireVisual.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, .55f, .22f);
                light.range = 8f;
                light.intensity = 2.4f;
            }
            if (sleepCampfireVisual != null)
            {
                sleepCampfireVisual.SetActive(sleeping);
                sleepCampfireVisual.transform.localPosition = cabinPosition
                    + cabinRotation * new Vector3(2f, 0f, -3.05f);
                sleepCampfireVisual.transform.localRotation = cabinRotation;
                if (sleeping) AlignGroundVisual(sleepCampfireVisual);
            }
            cabinStateWorldLabel ??= CreateWorldLabel(
                "Nature_Cabin_State_Label", new Color(1f, .82f, .38f));
            cabinStateWorldLabel.gameObject.SetActive(sleeping);
            cabinStateWorldLabel.transform.localPosition = cabinPosition
                + cabinRotation * new Vector3(.5f, 3.15f, -3.2f);
            cabinStateWorldLabel.text = sleeping
                ? "수면 중 · 새벽까지 시간 진행" : string.Empty;

            if (showDawnPlans && dawnPlansVisual == null)
                dawnPlansVisual = CreateVisual(expansionPlansPrefab,
                    PrimitiveType.Quad, "Nature_Dawn_Expansion_Plans",
                    Vector3.zero, expansionPlansPrefab != null
                        ? new Vector3(1.35f, 1.35f, 1.35f)
                        : new Vector3(1.4f, 1f, 1f));
            if (showDawnPlans && dawnPlanBoardVisual == null)
            {
                dawnPlanBoardVisual = CreateVisual(null!, PrimitiveType.Cube,
                    "Nature_Dawn_Expansion_PlanBoard", Vector3.zero,
                    new Vector3(2.4f, 1.55f, .12f));
                if (dawnPlanBoardVisual.TryGetComponent<Renderer>(out var board))
                    board.material.color = new Color(.075f, .34f, .43f);
            }
            if (dawnPlansVisual != null)
            {
                dawnPlansVisual.SetActive(showDawnPlans);
                dawnPlansVisual.transform.localPosition = cabinPosition
                    + cabinRotation * new Vector3(3.1f, 2f, -2.78f);
                dawnPlansVisual.transform.localRotation = cabinRotation
                    * Quaternion.Euler(90f, 0f, 0f);
                dawnPlansVisual.transform.localScale = Vector3.one * 2f;
            }
            if (dawnPlanBoardVisual != null)
            {
                dawnPlanBoardVisual.SetActive(showDawnPlans);
                dawnPlanBoardVisual.transform.localPosition = cabinPosition
                    + cabinRotation * new Vector3(3.1f, 2f, -2.7f);
                dawnPlanBoardVisual.transform.localRotation = cabinRotation;
            }

            dawnPlanWorldLabel ??= CreateWorldLabel(
                "Nature_Dawn_Plan_Label", new Color(.45f, .9f, 1f));
            dawnPlanWorldLabel.gameObject.SetActive(showDawnPlans);
            dawnPlanWorldLabel.transform.localPosition = cabinPosition
                + cabinRotation * new Vector3(3.1f, 3.15f, -2.7f);
            dawnPlanWorldLabel.text = state.Day2Ready
                ? "작업대 계획 확정 · 건설 위치를 선택하세요"
                : "확장 선택 · 1 작업대 / 2 보관대 / 3 방책";
        }

        private TextMesh CreateWorldLabel(string objectName, Color color)
        {
            var value = new GameObject(objectName);
            value.transform.SetParent(visualRoot, false);
            var label = value.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .045f;
            label.fontSize = 56;
            label.color = color;
            var billboard = value.AddComponent<Nature월드안내Billboard>();
            billboard.TargetCamera = player.FirstPersonCamera;
            return label;
        }

        private void RenderDroppedTimber(Nature생존State state)
        {
            var known = state.DroppedTimber.Select(value => value.StableId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var stale in droppedTimberVisuals.Keys
                         .Where(value => !known.Contains(value)).ToArray())
            {
                DestroyRuntimeSafe(droppedTimberVisuals[stale]);
                droppedTimberVisuals.Remove(stale);
            }

            foreach (var dropped in state.DroppedTimber)
            {
                if (!droppedTimberVisuals.TryGetValue(dropped.StableId,
                        out var visual))
                {
                    visual = CreateVisual(timberLogPrefab, PrimitiveType.Cylinder,
                        "Nature_DroppedTimber_" + dropped.StableId,
                        new Vector3(dropped.LocalX + 1.25f, .27f,
                            dropped.LocalZ + .85f),
                        timberLogPrefab != null
                            ? new Vector3(.78f, .78f, .78f)
                            : new Vector3(.32f, .9f, .32f));
                    if (timberLogPrefab != null)
                    {
                        var yaw = 25f + dropped.StableId.Aggregate(0,
                            (current, character) =>
                                (current * 31 + character) & 0x7fffffff) % 130;
                        visual.transform.localRotation =
                            Quaternion.Euler(0f, yaw, 0f);
                    }
                    else
                        visual.transform.localRotation =
                            Quaternion.Euler(0f, 0f, 90f);
                    AlignGroundVisual(visual);
                    var marker = visual.AddComponent<Nature생존상호작용표식>();
                    marker.Kind = Nature생존상호작용Kind.DroppedTimber;
                    marker.StableId = dropped.StableId;
                    marker.UsesSyntyAsset = timberLogPrefab != null;
                    marker.VisualSourceStableId = timberLogPrefab != null
                        ? "asset:synty:polygon-nature:sm-tree-log-02"
                        : "fallback:primitive:cylinder";
                    droppedTimberVisuals.Add(dropped.StableId, visual);
                }
                visual.SetActive(dropped.StateCode ==
                    Nature생존Codes.DroppedTimberAvailable);
            }
        }

        private void RenderWorkbench(Nature생존State state)
        {
            var visible = state.Workbench.StateCode == Nature생존Codes.Building
                          || state.Workbench.StateCode ==
                          Nature생존Codes.Operational;
            if (!visible)
            {
                if (workbenchVisual != null) workbenchVisual.SetActive(false);
                return;
            }
            workbenchVisual ??= CreateVisual(workbenchPrefab, PrimitiveType.Cube,
                "Nature_Workbench_Authoritative", Vector3.zero,
                workbenchPrefab != null ? Vector3.one : new Vector3(2.2f, 1f, 1.4f));
            var marker = workbenchVisual.GetComponent<
                Nature생존상호작용표식>();
            if (marker == null)
                marker = workbenchVisual.AddComponent<
                    Nature생존상호작용표식>();
            marker.Kind = Nature생존상호작용Kind.Workbench;
            marker.StableId = state.Workbench.FacilityStableId;
            marker.UsesSyntyAsset = workbenchPrefab != null;
            marker.VisualSourceStableId = workbenchPrefab != null
                ? "asset:synty:polygon-construction:sm-prop-table-saw-01"
                : "fallback:primitive:cube-workbench";
            workbenchVisual.SetActive(true);
            workbenchVisual.transform.localPosition = new Vector3(
                state.Workbench.LocalX, 0f, state.Workbench.LocalZ);
            workbenchVisual.transform.localRotation = Quaternion.Euler(0f,
                state.Workbench.YawDegrees, 0f);
            var progress = state.ActiveWork?.KindCode ==
                           Nature생존Codes.AreaBuildingConstruction
                ? Mathf.Clamp01((float)state.ActiveWork.CompletedSeconds /
                                Mathf.Max(1, state.ActiveWork.RequiredSeconds))
                : 1f;
            workbenchVisual.transform.localScale = Vector3.Lerp(
                new Vector3(1f, .2f, 1f), Vector3.one, progress);
            AlignGroundVisual(workbenchVisual);
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
                cabinVisual = cabinPrefab != null
                    ? CreateVisual(cabinPrefab, PrimitiveType.Cube,
                        "Nature_Cabin_SyntyGardenShed", Vector3.zero,
                        new Vector3(1.15f, 1.15f, 1.15f))
                    : CreateCabinBlockout();
                var marker = cabinVisual.AddComponent<Nature생존상호작용표식>();
                marker.Kind = Nature생존상호작용Kind.Cabin;
                marker.StableId = state.Cabin.StableId;
                var exposureObject = new GameObject("SkyExposureVolume");
                exposureObject.transform.SetParent(cabinVisual.transform, false);
                var exposureCollider = exposureObject.AddComponent<BoxCollider>();
                exposureCollider.center = new Vector3(0f, 1.25f, 0f);
                exposureCollider.size = new Vector3(3.6f, 2.5f, 3.6f);
                exposureObject.AddComponent<SkyExposureVolume>()
                    .Configure(exposureCollider);
                marker.UsesSyntyAsset = cabinPrefab != null;
                marker.VisualSourceStableId = cabinPrefab != null
                    ? "asset:synty:polygon-town:sm-bld-garden-shed-01"
                    : "fallback:blockout:cube-cabin";
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
            AlignGroundVisual(cabinVisual);
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
                threatObservation = null;
                return;
            }
            if (encounterVisual == null)
            {
                encounterVisual = CreateVisual(skeletonPlaceholderPrefab,
                    PrimitiveType.Capsule, "Nature_Threat_Skeleton",
                    new Vector3(0f, 0f, 9.5f),
                    skeletonPlaceholderPrefab != null
                        ? new Vector3(1.45f, 1.45f, 1.45f)
                        : new Vector3(1.2f, 1.8f, 1.2f));
                AlignGroundVisual(encounterVisual);
            }
            encounterVisual.SetActive(true);
            RefreshThreatObservationPresentation(false);
        }

        private void ConfirmThreatObservation()
        {
            if (engine is not INature위협관찰Engine observation)
            {
                status = "기존 직접 실행 프로필 · 위협 관찰 Confirm을 사용하지 않습니다.";
                return;
            }
            try
            {
                var preview = observation.PreviewThreatObservation();
                var decision = Nature위협관찰PresentationPolicy.Evaluate(preview,
                    "전방 위협 경로", "후방 안전 거점");
                if (!decision.CanConfirmObservation)
                {
                    threatObservation = decision;
                    status = "위협 관찰 차단 · " + string.Join(", ",
                        decision.BlockingReasonCodes);
                    return;
                }
                observation.ConfirmThreatObservation();
                threatObservation = decision;
                if (encounterVisual != null)
                    player.AimFirstPersonAtPresentationTarget(
                        MarkerAimPoint(encounterVisual.transform));
                status = $"위협 관찰 완료 · 압력 {decision.EffectivePressure} "
                         + $"({decision.PressureLevelCode}) · 현재 "
                         + CurrentCombatParticipationLabel
                         + " · F 전투 / R 후퇴";
            }
            catch (InvalidOperationException error)
            {
                status = "위협 관찰 차단 · " + error.Message;
            }
        }

        private void RefreshThreatObservationPresentation(bool force)
        {
            if (!force && threatObservation != null) return;
            if (engine is not INature위협관찰Engine observation) return;
            try
            {
                threatObservation = Nature위협관찰PresentationPolicy.Evaluate(
                    observation.PreviewThreatObservation(), "전방 위협 경로",
                    "후방 안전 거점");
                if (!observation.ThreatObservationConfirmed)
                    status = "황혼 위협 감지 · O 키로 경로와 압력을 관찰하세요.";
            }
            catch (InvalidOperationException error)
            {
                status = "위협 관찰 준비 차단 · " + error.Message;
            }
        }

        private static Vector3 MarkerAimPoint(Transform target)
        {
            var collider = target.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : target.position;
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

        private void AlignGroundVisual(GameObject visual, float localSurfaceY = 0f)
        {
            if (visual == null || visualRoot == null) return;
            var rootWorldY = visualRoot.TransformPoint(Vector3.zero).y;
            var localOffsetWorldY = visualRoot.TransformPoint(
                new Vector3(0f, localSurfaceY, 0f)).y - rootWorldY;
            var surfaceWorldY = approvedSurfaceWorldY + localOffsetWorldY;
            AlignVisualToWorldSurface(visual, surfaceWorldY);
        }

        private static void AlignVisualToWorldSurface(
            GameObject visual,
            float surfaceWorldY)
        {
            if (visual == null) return;
            배치객체표면정렬Utility.AlignVisibleBottomToWorldHeight(
                visual.transform, surfaceWorldY);
        }

        private void LoadEditorSyntyAssetsIfAvailable()
        {
            var catalog = Resources.Load<Nature생존VisualCatalog>(
                Nature생존VisualCatalog.ResourcesPath);
            if (catalog != null)
            {
                treePrefab ??= catalog.TreePrefab;
                stumpPrefab ??= catalog.StumpPrefab;
                axePrefab ??= catalog.AxePrefab;
                timberLogPrefab ??= catalog.TimberLogPrefab;
                cabinPrefab ??= catalog.CabinPrefab;
                workbenchPrefab ??= catalog.WorkbenchPrefab;
                skeletonPlaceholderPrefab ??= catalog.SkeletonPrefab;
                storedTimberPrefab ??= catalog.StoredTimberPrefab;
                sleepBedPrefab ??= catalog.SleepBedPrefab;
                sleepCampfirePrefab ??= catalog.SleepCampfirePrefab;
                expansionPlansPrefab ??= catalog.ExpansionPlansPrefab;
            }
#if UNITY_EDITOR
            treePrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Pine_01.prefab");
            stumpPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Stump_01.prefab");
            axePrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonGeneric/Prefabs/Weapons/SM_Gen_Wep_Axe_01.prefab");
            timberLogPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Log_02.prefab");
            cabinPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonTown/Prefabs/Buildings/SM_Bld_GardenShed_01.prefab");
            workbenchPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Table_Saw_01.prefab");
            skeletonPlaceholderPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Skeleton_01.prefab");
            storedTimberPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Wood_Stack_01.prefab");
            sleepBedPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Bed_01.prefab");
            sleepCampfirePrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_CampFire_01.prefab");
            expansionPlansPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Plans_01.prefab");
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
            if (!CanUseNatureInput(player.CurrentActivityCode)) return;
            var state = engine.Snapshot();
            if (state.Sleeping)
            {
                GUI.color = new Color(.025f, .045f, .12f, .62f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUI.color = new Color(.035f, .055f, .035f, .9f);
            GUI.DrawTexture(HudGuiRect(), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, 24f, 402f, 20f),
                $"Nature 생존 · {launchProfileLabel} · {PhaseKorean(state.ClockPhaseCode)} · Day {state.CycleIndex + 1}");
            GUI.Label(new Rect(28f, 46f, 402f, 20f),
                $"도끼 {(state.HasAxe ? "보유" : "미보유")} · 통나무 소지 {state.TimberQuantity} / 보관 {state.StoredTimberQuantity}/20 · 부품 {state.RebuildPartQuantity}");
            GUI.Label(new Rect(28f, 68f, 402f, 20f),
                diagnosticHudExpanded
                    ? $"오두막 {CabinStateKorean(state)} · 작업대 {WorkbenchStateKorean(state)} · 위협 {state.RawThreatTier}→{state.EffectiveThreatTier}"
                    : status);
            if (!diagnosticHudExpanded)
            {
                GUI.Label(new Rect(28f, 90f, 402f, 20f),
                    "I 장착 상태창 · F8 진단 · 조준선 대상에 왼쪽 버튼 상호작용");
                if (GUI.Button(EquipmentToggleButtonGuiRect(),
                        equipmentPanelOpen ? "장착 상태창 닫기" : "장착 상태창 열기")
                    && equipmentPointerHandledFrame != Time.frameCount)
                    SetEquipmentPanelOpen(!equipmentPanelOpen);
                DrawEquipmentPanel();
                return;
            }
            GUI.Label(new Rect(28f, 90f, 402f, 20f), status);
            GUI.Label(new Rect(28f, 112f, 402f, 20f),
                "Nature 탐험 · LMB 상호작용 · G 보관 · T 수면 · 1/2/3 확장");
            GUI.Label(new Rect(28f, 134f, 402f, 20f),
                "O 관찰 → F2 직접 / F3 관찰 → F 전투 · R 후퇴");
            GUI.enabled = saveWorld != null;
            GUI.Button(SaveButtonGuiRect(), "현재 세계 저장");
            GUI.enabled = state.ActiveWork != null;
            GUI.Button(CancelButtonGuiRect(), "진행 작업 취소");
            GUI.enabled = state.ActiveWork == null && ResolveNextApproachTarget() != null;
            GUI.Button(ApproachButtonGuiRect(), "다음 목표 접근");
            GUI.enabled = state.ActiveWork == null
                          && (state.PlayerInsideCabin
                              || state.Cabin.StateCode == Nature생존Codes.Planned
                              || ResolveNearestMarker(
                                  Nature생존상호작용Kind.Cabin) != null);
            GUI.Button(BlueprintButtonGuiRect(), state.PlayerInsideCabin
                ? "안전 빈터로 퇴장"
                : state.Cabin.StateCode == Nature생존Codes.Planned
                    ? "오두막 도면" : "오두막 접근");
            GUI.enabled = state.ActiveWork != null;
            GUI.Button(WorkHoldButtonGuiRect(),
                workHoldRequested ? "작업 유지 끄기" : "작업 유지 켜기");
            GUI.enabled = true;
            var observation = threatObservation;
            GUI.Label(new Rect(28f, 216f, 402f, 20f), observation == null
                ? "저장 후 Play Mode 재진입 시 같은 세계를 복원합니다."
                : $"관찰 {observation.ThreatDirectionCode} · 안전 {observation.SafeCoreDirectionCode} · {observation.PressureLevelCode}");
            DrawEquipmentPanel();
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
                var savedRevision = engine.Snapshot().Revision;
                status = saveWorld();
                lastSavedRevision = savedRevision;
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

        private static string CabinStateKorean(Nature생존State state)
        {
            if (state.PlayerInsideCabin) return "내부";
            return state.Cabin.StateCode switch
            {
                Nature생존Codes.Planned => "미배치",
                Nature생존Codes.Building => "건설 중",
                Nature생존Codes.Completed => "완성",
                _ => state.Cabin.StateCode,
            };
        }

        private static string WorkbenchStateKorean(Nature생존State state)
            => state.Workbench.StateCode switch
            {
                Nature생존Codes.Available => "배치 가능",
                Nature생존Codes.Building => "건설 중",
                Nature생존Codes.Operational => "운영 가능",
                _ => state.Workbench.StateCode,
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
                ? "전투 승리 · 재건 부품을 확보하고 거점으로 복귀했습니다."
                : authoritativeResult == Nature생존Codes.Retreat
                    ? "안전 후퇴 · 보상 없이 거점으로 복귀했습니다."
                    : "전투 패배 · 소지 재료 일부를 잃고 거점으로 복귀했습니다.";
        }

        public void AllowFightRetry(string reasonCode)
            => status = "전투 요청 대기 · " + reasonCode;
    }
}
