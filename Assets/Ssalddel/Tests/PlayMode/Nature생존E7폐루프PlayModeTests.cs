using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Bootstrap;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.WorkflowRules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ssalddel.Unity.Tests.PlayMode
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "canonical Scene에서 도끼·벌목·오두막·취소·저장·재진입을 실제 입력으로 검증한다.",
        Boundary = "자동 시험 증거를 사람의 Game View 확인 또는 Hosted 완료로 확대하지 않는다.")]
    public sealed class Nature생존E7폐루프PlayModeTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_Nature생활거점저장복원을완주한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-e7-playmode-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureE7ValidationKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>(
                "NatureE7ValidationMouse");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();
                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                var hierarchyEvidence = UnityEngine.Object.FindAnyObjectByType<
                    H계층배치증거Root>(FindObjectsInactive.Include)!;
                var natureArea = hierarchyEvidence.AreaSetRoots.Single(value =>
                    value.AreaRoleCode == "NatureHome");
                var farmArea = hierarchyEvidence.AreaSetRoots.Single(value =>
                    value.AreaRoleCode == "Farm");
                Assert.That(Vector3.Distance(controller.transform.position,
                    natureArea.transform.position), Is.LessThan(.1f));
                Assert.That(Vector3.Distance(controller.transform.position,
                    farmArea.transform.position), Is.GreaterThan(100f),
                    "Nature 폐루프는 Farm 시작점 위에 겹쳐 생성되면 안 된다.");
                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                var axe = Marker(Nature생존상호작용Kind.Axe);
                yield return ClickMarker(controller, player, axe, mouse);
                Assert.That(controller.State.HasAxe, Is.True,
                    $"status={controller.Status}; screen={Screen.width}x{Screen.height}; "
                    + $"mode={player.CurrentMode}; mouse={Mouse.current?.name}; "
                    + $"active={controller.isActiveAndEnabled}");

                yield return PressKey(controller, keyboard, Key.I);
                Assert.That(controller.EquipmentPanelOpen, Is.True,
                    "I 실제 입력으로 장착 상태창이 열려야 한다.");
                yield return Click(controller, mouse,
                    EquipmentInventoryActionPoint());
                Assert.That(controller.Status, Does.StartWith("장착 완료"));
                Assert.That(GameObject.Find("Player_Equipped_MainHand_Axe"),
                    Is.Not.Null, "MainHand 장착 결과가 Player 손 도끼로 보여야 한다.");

                yield return Click(controller, mouse,
                    EquipmentSlotActionPoint(
                        SimulationActorEquipmentCodes.MainHand));
                Assert.That(controller.Status, Does.StartWith("장착 해제"));
                yield return null;
                Assert.That(GameObject.Find("Player_Equipped_MainHand_Axe"),
                    Is.Null, "장착 해제 결과가 손 표현에서도 사라져야 한다.");
                yield return PressKey(controller, keyboard, Key.I);
                Assert.That(controller.EquipmentPanelOpen, Is.False);

                var trees = UnityEngine.Object.FindObjectsByType<
                             Nature생존상호작용표식>(FindObjectsInactive.Include)
                         .Where(value => value.Kind ==
                             Nature생존상호작용Kind.HarvestTree)
                         .OrderBy(value => Vector3.Distance(
                             player.transform.position, value.transform.position))
                         .ThenBy(value => value.StableId,
                             StringComparer.Ordinal)
                         .Take(3).ToArray();
                yield return ClickMarker(controller, player, trees[0], mouse);
                Assert.That(controller.State.ActiveWork, Is.Null,
                    "도끼를 보유해도 MainHand에 장착하지 않으면 벌목이 시작되면 안 된다.");
                Assert.That(controller.Status,
                    Does.Contain(SimulationNatureSurvivalCodes.AxeRequired));

                yield return PressKey(controller, keyboard, Key.I);
                yield return Click(controller, mouse,
                    EquipmentInventoryActionPoint());
                Assert.That(controller.Status, Does.StartWith("장착 완료"));
                yield return PressKey(controller, keyboard, Key.I);
                Assert.That(controller.EquipmentPanelOpen, Is.False);
                Assert.That(GameObject.Find("Player_Equipped_MainHand_Axe"),
                    Is.Not.Null);
                var revisionBeforeApproach = controller.State.Revision;
                var cycleBeforeApproach = controller.State.CycleIndex;
                var elapsedBeforeApproach = controller.State.ElapsedSecondsInCycle;
                var timberBeforeApproach = controller.State.TimberQuantity;
                var resourcesBeforeApproach = string.Join("|",
                    controller.State.ResourceNodes
                        .OrderBy(value => value.StableId, StringComparer.Ordinal)
                        .Select(value => value.StableId + ":" + value.StateCode));
                yield return Click(controller, mouse,
                    new Vector2(364f, Screen.height - 167f));
                Assert.That(controller.AssistedApproachActive
                            || Vector3.Distance(player.transform.position,
                                trees[0].transform.position) < 3.3f,
                    Is.True,
                    "이미 상호작용 거리 안이면 접근 이동을 새로 시작하지 않아도 된다.");
                var approachDeadline = Time.unscaledTime + 8f;
                while (controller.AssistedApproachActive
                       && Time.unscaledTime < approachDeadline)
                    yield return null;
                Assert.That(controller.AssistedApproachActive, Is.False,
                    "다음 목표 접근이 제한 시간 안에 끝나야 한다.");
                Assert.That(controller.State.CycleIndex,
                    Is.EqualTo(cycleBeforeApproach));
                Assert.That(controller.State.Revision - revisionBeforeApproach,
                    Is.EqualTo(controller.State.ElapsedSecondsInCycle
                               - elapsedBeforeApproach),
                    "목표 접근 중 revision 변화는 기존 실시간 시계 진행분뿐이어야 한다.");
                Assert.That(controller.State.TimberQuantity,
                    Is.EqualTo(timberBeforeApproach),
                    "목표 접근은 자원 상태를 직접 변경하면 안 된다.");
                Assert.That(controller.State.ActiveWork, Is.Null);
                Assert.That(string.Join("|", controller.State.ResourceNodes
                        .OrderBy(value => value.StableId, StringComparer.Ordinal)
                        .Select(value => value.StableId + ":" + value.StateCode)),
                    Is.EqualTo(resourcesBeforeApproach));
                Assert.That(Vector3.Distance(player.transform.position,
                        trees[0].transform.position), Is.LessThan(3.3f));
                yield return ClickMarker(controller, player, trees[0], mouse);
                Assert.That(controller.State.ActiveWork?.TargetStableId,
                    Is.EqualTo(trees[0].StableId));
                yield return PressKey(controller, keyboard, Key.C);
                Assert.That(controller.State.ActiveWork, Is.Null);
                Assert.That(controller.State.TimberQuantity, Is.Zero);
                Assert.That(controller.State.ResourceNodes.Single(value =>
                    value.StableId == trees[0].StableId).StateCode,
                    Is.EqualTo(Nature생존Codes.Standing));

                yield return HoldPrimaryOnMarker(controller, player, trees[0], mouse,
                    NatureSurvivalRules.HarvestWorkSeconds + .25f);
                yield return ClickMarker(controller, player, trees[1], mouse);
                yield return Click(controller, mouse,
                    new Vector2(226f, Screen.height - 197f));
                Assert.That(controller.WorkHoldRequested, Is.True);
                var workHoldDeadline = Time.unscaledTime
                                       + NatureSurvivalRules.HarvestWorkSeconds + 3f;
                while (controller.State.ActiveWork != null
                       && Time.unscaledTime < workHoldDeadline)
                    yield return null;
                Assert.That(controller.State.ActiveWork, Is.Null,
                    "작업 유지 입력으로 벌목이 완료돼야 한다.");
                Assert.That(controller.WorkHoldRequested, Is.False);
                foreach (var tree in trees.Skip(2))
                {
                    yield return HoldPrimaryOnMarker(controller, player, tree, mouse,
                        NatureSurvivalRules.HarvestWorkSeconds + .25f);
                    Assert.That(controller.State.ActiveWork, Is.Null,
                        $"벌목 입력 완료 뒤 작업이 남았습니다: {tree.StableId}; "
                        + $"progress={controller.State.ActiveWork?.CompletedSeconds}/"
                        + controller.State.ActiveWork?.RequiredSeconds);
                }
                var droppedTimber = UnityEngine.Object.FindObjectsByType<
                        Nature생존상호작용표식>(FindObjectsInactive.Exclude)
                    .Where(value => value.Kind ==
                        Nature생존상호작용Kind.DroppedTimber)
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .ToArray();
                var authorityDrops = controller.State.DroppedTimber
                    .Where(value => value.StateCode ==
                        Nature생존Codes.DroppedTimberAvailable)
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(authorityDrops, Has.Length.EqualTo(3));
                Assert.That(droppedTimber, Is.Not.Empty,
                    "현재 LH Detail 창에는 획득 가능한 통나무가 하나 이상 표현되어야 한다.");
                Assert.That(controller.UsesSyntyTimberLog, Is.True,
                    "벌목 결과는 설치된 Synty 통나무로 식별 가능해야 한다.");
                PointAt(player, droppedTimber[0]);
                var timberEvidenceDirectory =
                    "Assets/Documentation/Changes/2026-08-26-nature-dual-loop-visual";
                Directory.CreateDirectory(timberEvidenceDirectory);
                var timberEvidencePath = Path.Combine(timberEvidenceDirectory,
                    "nature-wi18-dropped-timber-game-view.png");
                if (File.Exists(timberEvidencePath)) File.Delete(timberEvidencePath);
                ScreenCapture.CaptureScreenshot(timberEvidencePath, 1);
                var timberEvidenceDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(timberEvidencePath)
                       && Time.realtimeSinceStartup < timberEvidenceDeadline)
                    yield return null;
                Assert.That(File.Exists(timberEvidencePath), Is.True);
                var lh = UnityEngine.Object.FindAnyObjectByType<
                    공간LHStreamingEngine>(FindObjectsInactive.Include)!;
                foreach (var droppedState in authorityDrops)
                {
                    var marker = UnityEngine.Object.FindObjectsByType<
                            Nature생존상호작용표식>(FindObjectsInactive.Exclude)
                        .FirstOrDefault(value => value.Kind ==
                            Nature생존상호작용Kind.DroppedTimber
                            && value.StableId == droppedState.StableId);
                    if (marker == null)
                    {
                        player.transform.position = controller.transform.TransformPoint(
                            new Vector3(droppedState.LocalX + 1.25f, 0f,
                                droppedState.LocalZ + .85f)) + Vector3.back * 2f;
                        var streamDeadline = Time.unscaledTime + 5f;
                        while (marker == null
                               && Time.unscaledTime < streamDeadline)
                        {
                            lh.EvaluateForTests();
                            yield return null;
                            marker = UnityEngine.Object.FindObjectsByType<
                                    Nature생존상호작용표식>(
                                        FindObjectsInactive.Exclude)
                                .FirstOrDefault(value => value.Kind ==
                                    Nature생존상호작용Kind.DroppedTimber
                                    && value.StableId == droppedState.StableId);
                        }
                    }
                    Assert.That(marker, Is.Not.Null,
                        "LH Detail 창을 해당 위치로 옮기면 권위 통나무가 다시 표현되어야 한다: "
                        + droppedState.StableId);
                    yield return ClickMarker(controller, player, marker!, mouse);
                }
                var collectedReconcileDeadline = Time.unscaledTime + 5f;
                while (controller.VisibleDroppedTimberCount > 0
                       && Time.unscaledTime < collectedReconcileDeadline)
                {
                    lh.EvaluateForTests();
                    yield return null;
                }
                Assert.That(controller.VisibleDroppedTimberCount, Is.Zero);
                Assert.That(controller.State.TimberQuantity, Is.EqualTo(6));

                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 197f));
                var placementGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                placementGround.name = "Nature_E7_ValidationPlacementGround";
                placementGround.transform.position = controller.transform.position;
                placementGround.transform.localScale = Vector3.one * .6f;
                player.transform.position = placementGround.transform.position
                    + Vector3.back * 2f;
                var camera = player.FirstPersonCamera;
                camera.transform.position = placementGround.transform.position
                    + Vector3.up * 7f;
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                yield return Click(controller, mouse);
                UnityEngine.Object.Destroy(placementGround);
                Assert.That(controller.State.Cabin.StateCode,
                    Is.Not.EqualTo(Nature생존Codes.Planned));

                var cabinMarkerDeadline = Time.unscaledTime + 5f;
                while (!UnityEngine.Object.FindObjectsByType<
                           Nature생존상호작용표식>(FindObjectsInactive.Include)
                           .Any(value => value.Kind ==
                               Nature생존상호작용Kind.Cabin)
                       && Time.unscaledTime < cabinMarkerDeadline)
                {
                    lh.EvaluateForTests();
                    yield return null;
                }
                var cabin = Marker(Nature생존상호작용Kind.Cabin);
                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 197f));
                Assert.That(controller.AssistedApproachActive
                            || controller.Status.Contains("오두막 접근"), Is.True,
                    "도면 배치 뒤 같은 HUD 버튼은 남은 나무보다 오두막 접근을 우선해야 한다."
                    + $" status={controller.Status}");
                var cabinApproachDeadline = Time.unscaledTime + 8f;
                while (controller.AssistedApproachActive
                       && Time.unscaledTime < cabinApproachDeadline)
                    yield return null;
                Assert.That(controller.AssistedApproachActive, Is.False);
                cabin = Marker(Nature생존상호작용Kind.Cabin);
                Assert.That(Vector3.Distance(player.transform.position,
                    cabin.transform.position), Is.LessThan(3.3f));
                Assert.That(Vector3.Distance(player.transform.position,
                    cabin.transform.position), Is.GreaterThan(2.7f),
                    "오두막 접근은 카메라가 외벽 안으로 파고들지 않는 거리에서 끝나야 한다.");
                yield return HoldPrimaryOnMarker(controller, player, cabin, mouse,
                    NatureSurvivalRules.CabinWorkSeconds + 1.25f);
                Assert.That(controller.State.Cabin.StateCode,
                    Is.EqualTo(Nature생존Codes.Completed),
                    $"progress={controller.State.ActiveWork?.CompletedSeconds}/"
                    + controller.State.ActiveWork?.RequiredSeconds);
                cabin = Marker(Nature생존상호작용Kind.Cabin);
                Assert.That(cabin.UsesSyntyAsset, Is.True,
                    "완성 오두막은 설치된 Synty 오두막으로 식별 가능해야 한다.");
                yield return PressKey(controller, keyboard, Key.F8);
                Assert.That(controller.DiagnosticHudExpanded, Is.False);
                FrameForEvidence(player, cabin, 8f);
                var cabinEvidenceDirectory =
                    "Assets/Documentation/Changes/2026-08-26-nature-dual-loop-visual";
                Directory.CreateDirectory(cabinEvidenceDirectory);
                var cabinEvidencePath = Path.Combine(cabinEvidenceDirectory,
                    "nature-synty-cabin-compact-hud-game-view.png");
                if (File.Exists(cabinEvidencePath)) File.Delete(cabinEvidencePath);
                ScreenCapture.CaptureScreenshot(cabinEvidencePath, 1);
                var cabinEvidenceDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(cabinEvidencePath)
                       && Time.realtimeSinceStartup < cabinEvidenceDeadline)
                    yield return null;
                Assert.That(File.Exists(cabinEvidencePath), Is.True);
                yield return PressKey(controller, keyboard, Key.F8);
                Assert.That(controller.DiagnosticHudExpanded, Is.True);
                cabin = Marker(Nature생존상호작용Kind.Cabin);
                yield return ClickMarker(controller, player, cabin, mouse);
                Assert.That(controller.State.PlayerInsideCabin, Is.True);
                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 197f));
                Assert.That(controller.State.PlayerInsideCabin, Is.False);

                yield return Click(controller, mouse,
                    new Vector2(88f, Screen.height - 167f));
                Assert.That(controller.Status, Does.StartWith("저장 완료"));
                var savedSession = controller.State.SessionStableId;
                var savedRevision = controller.LastSavedRevision;
                Assert.That(savedRevision, Is.Not.Null,
                    "저장 결과는 실시간 시계와 구분되는 저장 시점 revision을 남겨야 한다.");

                controller.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                Time.timeScale = 1f;
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();
                var restored = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                Assert.That(restored.State.SessionStableId,
                    Is.EqualTo(savedSession));
                Assert.That(restored.State.Revision, Is.EqualTo(savedRevision!.Value));
                Assert.That(restored.State.Cabin.StateCode,
                    Is.EqualTo(Nature생존Codes.Completed));
                Assert.That(restored.State.HasAxe, Is.True);
                Assert.That(restored.State.ResourceNodes.Count(value =>
                    value.StateCode == Nature생존Codes.Stump), Is.EqualTo(3));
                Assert.That(restored.State.PlayerInsideCabin, Is.False);

                var restoredPlayer = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                restoredPlayer.EnterExplorationFirstPersonMode();
                restoredPlayer.TickCameraTransition(
                    restoredPlayer.ViewTransitionDuration);
                restoredPlayer.enabled = false;
                var twilightTree = UnityEngine.Object.FindObjectsByType<
                        Nature생존상호작용표식>(FindObjectsInactive.Include)
                    .Where(value => value.Kind ==
                        Nature생존상호작용Kind.HarvestTree
                        && value.gameObject.activeInHierarchy)
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .First();
                yield return HoldPrimaryOnMarker(restored, restoredPlayer,
                    twilightTree, mouse,
                    NatureSurvivalRules.HarvestWorkSeconds + .25f);
                while (restored.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DaylightEndsAtSecond)
                {
                    restored.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DaylightEndsAtSecond
                        - restored.State.ElapsedSecondsInCycle));
                }
                Assert.That(restored.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Pending));

                yield return PressKey(restored, keyboard, Key.R);
                Assert.That(restored.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Pending));
                Assert.That(restored.Status, Does.Contain("O 키"));

                yield return PressKey(restored, keyboard, Key.O);
                Assert.That(restored.ThreatObservationConfirmed, Is.True);
                Assert.That(restored.ThreatObservation, Is.Not.Null);
                Assert.That(restored.ThreatObservation!.NextChoices
                    .Select(value => value.WorldInteractionId),
                    Does.Contain("WI-NATURE-11"));
                Assert.That(restored.Status, Does.StartWith("위협 관찰 완료"));

                var evidenceDirectory = Path.Combine(Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..")), "artifacts",
                    "local", "validation", "nature-twilight");
                Directory.CreateDirectory(evidenceDirectory);
                var evidencePath = Path.Combine(evidenceDirectory,
                    "nature-twilight-wi01-observed-game-view.png");
                if (File.Exists(evidencePath)) File.Delete(evidencePath);
                ScreenCapture.CaptureScreenshot(evidencePath, 1);
                yield return new WaitForEndOfFrame();
                var captureDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(evidencePath)
                       && Time.realtimeSinceStartup < captureDeadline)
                    yield return null;
                Assert.That(File.Exists(evidencePath), Is.True,
                    "황혼 관찰 Game View PNG가 생성되어야 한다.");
                Assert.That(new FileInfo(evidencePath).Length,
                    Is.GreaterThan(1024));

                restoredPlayer.EnterThirdPersonMode();
                restoredPlayer.TickCameraTransition(
                    restoredPlayer.ViewTransitionDuration);
                Assert.That(restored.CurrentCombatParticipationLabel,
                    Is.EqualTo("3인칭 관찰 운영"));
                var battleRoot = UnityEngine.Object.FindAnyObjectByType<
                    현장전투CompositionRoot>(FindObjectsInactive.Include)!;
                var authorityDeadline = Time.realtimeSinceStartup + 10f;
                while (!battleRoot.AuthorityReady
                       && Time.realtimeSinceStartup < authorityDeadline)
                    yield return null;
                Assert.That(battleRoot.AuthorityReady, Is.True,
                    "F 입력 전에 LocalProcess 전투 권위가 준비되어야 한다.");
                yield return PressKey(restored, keyboard, Key.F);
                var battleRequestDeadline = Time.realtimeSinceStartup + 10f;
                while (battleRoot.Current == null
                       && Time.realtimeSinceStartup < battleRequestDeadline)
                    yield return null;
                Assert.That(battleRoot.Current, Is.Not.Null,
                    "F 실제 입력이 기존 현장 전투 권위에 조우를 인계해야 한다. "
                    + $"status={restored.Status};authority={battleRoot.AuthorityReady};"
                    + $"mode={restoredPlayer.CurrentMode}");
                Assert.That(battleRoot.Current!.LocalCombat.ControlModeCode,
                    Is.EqualTo("ObserverOperation"));
                var battleDeadline = Time.realtimeSinceStartup + 45f;
                while (battleRoot.Current.PhaseCode == "Active"
                       && Time.realtimeSinceStartup < battleDeadline)
                    yield return null;
                Assert.That(battleRoot.Current.PhaseCode, Is.EqualTo("Completed"),
                    "관찰 운영 전투가 제한 시간 안에 권위 결과로 닫혀야 한다.");
                Assert.That(restored.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Resolved));
                Assert.That(restored.State.LastCombatResultCode,
                    Is.Not.Empty);
                Assert.That(restored.Status, Does.Contain("거점으로 복귀"));
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_획득자원을_오두막에보관한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-wi13-store-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureWI13StoreKeyboard");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();

                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                controller.AcquireAxeForTest();
                controller.EquipAxeForTest();
                foreach (var tree in controller.State.ResourceNodes
                             .OrderBy(value => value.StableId,
                                 StringComparer.Ordinal).Take(4))
                {
                    controller.BeginHarvestForTest(tree.StableId);
                    controller.HoldWorkForTest(
                        NatureSurvivalRules.HarvestWorkSeconds);
                    CollectAllDroppedTimber(controller);
                }
                controller.PlaceCabinBlueprintForTest(2f, -2f, 0f);
                controller.BeginCabinBuildForTest();
                controller.HoldWorkForTest(NatureSurvivalRules.CabinWorkSeconds);
                controller.EnterCabinForTest();

                Assert.That(controller.State.TimberQuantity, Is.EqualTo(2));
                Assert.That(controller.State.StoredTimberQuantity, Is.Zero);
                yield return PressKey(controller, keyboard, Key.G);

                Assert.That(controller.State.TimberQuantity, Is.Zero);
                Assert.That(controller.State.StoredTimberQuantity, Is.EqualTo(2));
                Assert.That(controller.Status, Does.Contain("보관"));
                yield return WaitForInteriorOverlay(":stored-timber");

                yield return PressKey(controller, keyboard, Key.F8);
                var storedTimberPile = FindInteriorOverlay(":stored-timber");
                Assert.That(storedTimberPile, Is.Not.Null);
                Assert.That(storedTimberPile!.GetComponentsInChildren<Renderer>()
                    .Any(value => value.enabled), Is.True);
                FrameForEvidence(player, storedTimberPile.transform, 4.5f);

                var evidenceDirectory = Path.Combine(Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "artifacts", "local", "validation", "nature-night-day2");
                Directory.CreateDirectory(evidenceDirectory);
                var evidencePath = Path.Combine(evidenceDirectory,
                    "nature-night-day2-wi13-store-game-view.png");
                if (File.Exists(evidencePath)) File.Delete(evidencePath);
                ScreenCapture.CaptureScreenshot(evidencePath, 1);
                yield return new WaitForEndOfFrame();
                var captureDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(evidencePath)
                       && Time.realtimeSinceStartup < captureDeadline)
                    yield return null;
                Assert.That(File.Exists(evidencePath), Is.True);
                Assert.That(new FileInfo(evidencePath).Length,
                    Is.GreaterThan(1024));
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_오두막에서수면해_새벽을맞는다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-wi14-sleep-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureWI14SleepKeyboard");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();

                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                controller.AcquireAxeForTest();
                controller.EquipAxeForTest();
                foreach (var tree in controller.State.ResourceNodes
                             .OrderBy(value => value.StableId,
                                 StringComparer.Ordinal).Take(4))
                {
                    controller.BeginHarvestForTest(tree.StableId);
                    controller.HoldWorkForTest(
                        NatureSurvivalRules.HarvestWorkSeconds);
                    CollectAllDroppedTimber(controller);
                }
                controller.PlaceCabinBlueprintForTest(2f, -2f, 0f);
                controller.BeginCabinBuildForTest();
                controller.HoldWorkForTest(NatureSurvivalRules.CabinWorkSeconds);
                controller.EnterCabinForTest();
                controller.StoreAtCabinForTest();

                while (controller.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DaylightEndsAtSecond)
                {
                    controller.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DaylightEndsAtSecond
                        - controller.State.ElapsedSecondsInCycle));
                }
                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Pending));
                yield return PressKey(controller, keyboard, Key.O);
                yield return PressKey(controller, keyboard, Key.R);
                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Resolved));
                Assert.That(controller.State.PlayerInsideCabin, Is.True);

                while (controller.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DuskEndsAtSecond)
                {
                    controller.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DuskEndsAtSecond
                        - controller.State.ElapsedSecondsInCycle));
                }
                Assert.That(controller.State.ClockPhaseCode,
                    Is.EqualTo(Nature생존Codes.Night));
                Assert.That(controller.State.Sleeping, Is.False);

                yield return PressKey(controller, keyboard, Key.F8);
                yield return WaitForMarker(
                    Nature생존상호작용Kind.Cabin);
                FrameForEvidence(player,
                    Marker(Nature생존상호작용Kind.Cabin), 7f);

                yield return PressKey(controller, keyboard, Key.T);
                Assert.That(controller.State.Sleeping, Is.True);
                Assert.That(controller.Status, Does.Contain("수면 시작"));
                Assert.That(controller.SleepingVisualVisible, Is.True,
                    "수면 중에는 침상과 따뜻한 불빛이 보여야 한다.");

                var sleepBed = GameObject.Find("Nature_Cabin_SleepBed");
                var sleepCue = GameObject.Find("Nature_Cabin_SleepWarmLight");
                Assert.That(sleepBed, Is.Not.Null);
                Assert.That(sleepCue, Is.Not.Null);
                var approvedGroundWorldY = controller.ApprovedSurfaceWorldY;
                Assert.That(배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(sleepBed!.transform,
                        approvedGroundWorldY).Passed, Is.True,
                    "침상 하단은 승인 지면 위 3cm에 보여야 한다.");
                Assert.That(배치객체표면정렬Utility
                    .ValidateVisibleBottomClearance(sleepCue.transform,
                        approvedGroundWorldY).Passed, Is.True,
                    "모닥불 하단은 승인 지면 위 3cm에 보여야 한다.");
                FrameForEvidence(player, sleepCue!.transform, 5.5f);
                yield return CaptureEvidence("nature-night-day2",
                    "nature-night-day2-wi14-sleeping-game-view.png");

                controller.HoldWorkForTest(60);
                Assert.That(controller.State.ClockPhaseCode,
                    Is.EqualTo(Nature생존Codes.Dawn));
                Assert.That(controller.State.ElapsedSecondsInCycle,
                    Is.EqualTo(NatureSurvivalRules.NightEndsAtSecond));
                Assert.That(controller.State.Sleeping, Is.False);
                Assert.That(controller.SleepingVisualVisible, Is.False);
                Assert.That(controller.DawnPlanVisualVisible, Is.True,
                    "새벽에는 확장 계획 자료와 월드 안내가 보여야 한다.");

                var dawnPlans = GameObject.Find(
                    "Nature_Dawn_Expansion_Plans");
                Assert.That(dawnPlans, Is.Not.Null);
                FrameForEvidence(player, dawnPlans!.transform, 5.5f);

                var evidenceDirectory = Path.Combine(Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "artifacts", "local", "validation", "nature-night-day2");
                Directory.CreateDirectory(evidenceDirectory);
                var evidencePath = Path.Combine(evidenceDirectory,
                    "nature-night-day2-wi14-dawn-game-view.png");
                if (File.Exists(evidencePath)) File.Delete(evidencePath);
                ScreenCapture.CaptureScreenshot(evidencePath, 1);
                yield return new WaitForEndOfFrame();
                var captureDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(evidencePath)
                       && Time.realtimeSinceStartup < captureDeadline)
                    yield return null;
                Assert.That(File.Exists(evidencePath), Is.True);
                Assert.That(new FileInfo(evidencePath).Length,
                    Is.GreaterThan(1024));
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_Day2작업대건설폐루프를완주한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-wi15-plan-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureWI15PlanKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>(
                "NatureWICon01WorkbenchMouse");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();

                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                controller.AcquireAxeForTest();
                controller.EquipAxeForTest();
                foreach (var tree in controller.State.ResourceNodes
                             .OrderBy(value => value.StableId,
                                 StringComparer.Ordinal).Take(4))
                {
                    controller.BeginHarvestForTest(tree.StableId);
                    controller.HoldWorkForTest(
                        NatureSurvivalRules.HarvestWorkSeconds);
                    CollectAllDroppedTimber(controller);
                }
                controller.PlaceCabinBlueprintForTest(2f, -2f, 0f);
                controller.BeginCabinBuildForTest();
                controller.HoldWorkForTest(NatureSurvivalRules.CabinWorkSeconds);
                controller.EnterCabinForTest();
                controller.StoreAtCabinForTest();

                while (controller.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DaylightEndsAtSecond)
                {
                    controller.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DaylightEndsAtSecond
                        - controller.State.ElapsedSecondsInCycle));
                }
                yield return PressKey(controller, keyboard, Key.O);
                var encounterId = controller.State.Encounter!.StableId;
                controller.ResolveFightVictory(encounterId);
                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.CombatActive));
                controller.ResolveAuthoritativeFightVictoryForTest();
                controller.RefreshAfterBattle(encounterId,
                    Nature생존Codes.Victory);
                Assert.That(controller.State.RebuildPartQuantity,
                    Is.GreaterThanOrEqualTo(1));
                while (controller.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DuskEndsAtSecond)
                {
                    controller.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DuskEndsAtSecond
                        - controller.State.ElapsedSecondsInCycle));
                }
                controller.SleepInCabinForTest();
                controller.HoldWorkForTest(60);
                Assert.That(controller.State.ClockPhaseCode,
                    Is.EqualTo(Nature생존Codes.Dawn));
                Assert.That(controller.State.Day2Ready, Is.False);

                yield return PressKey(controller, keyboard, Key.Digit1);
                Assert.That(controller.State.SelectedExpansionPlanCode,
                    Is.EqualTo(Nature생존Codes.Workbench));
                Assert.That(controller.State.Day2Ready, Is.True);
                Assert.That(controller.Status, Does.Contain("Day 2 계획 고정"));
                Assert.That(controller.DawnPlanVisualVisible, Is.True);

                yield return PressKey(controller, keyboard, Key.F8);
                var selectedPlans = GameObject.Find(
                    "Nature_Dawn_Expansion_Plans");
                Assert.That(selectedPlans, Is.Not.Null);
                FrameForEvidence(player, selectedPlans!.transform, 5.5f);
                yield return CaptureEvidence("nature-night-day2",
                    "nature-night-day2-wi15-day2-ready-game-view.png");
                yield return PressKey(controller, keyboard, Key.F8);

                controller.LeaveCabinForTest();
                foreach (var tree in controller.State.ResourceNodes
                             .Where(value => value.StateCode ==
                                 Nature생존Codes.Standing)
                             .OrderBy(value => value.StableId,
                                 StringComparer.Ordinal).Take(2))
                {
                    controller.BeginHarvestForTest(tree.StableId);
                    controller.HoldWorkForTest(
                        NatureSurvivalRules.HarvestWorkSeconds);
                    CollectAllDroppedTimber(controller);
                }
                controller.EnterCabinForTest();
                Assert.That(controller.State.TimberQuantity,
                    Is.GreaterThanOrEqualTo(4));
                Assert.That(controller.State.RebuildPartQuantity,
                    Is.GreaterThanOrEqualTo(1));
                var timberBeforeWorkbench = controller.State.TimberQuantity
                    + controller.State.StoredTimberQuantity;
                var partsBeforeWorkbench = controller.State.RebuildPartQuantity;

                var cabin = Marker(Nature생존상호작용Kind.Cabin);
                var placementGround = GameObject.CreatePrimitive(
                    PrimitiveType.Plane);
                placementGround.name = "Nature_Workbench_E7_PlacementGround";
                placementGround.transform.position = cabin.transform.position
                    + Vector3.right * 6f;
                placementGround.transform.localScale = Vector3.one * .25f;
                player.FirstPersonCamera.transform.position =
                    placementGround.transform.position + Vector3.up * 7f;
                player.FirstPersonCamera.transform.rotation =
                    Quaternion.Euler(90f, 0f, 0f);

                yield return PressKey(controller, keyboard, Key.Digit1);
                Assert.That(controller.WorkbenchPlacementMode, Is.True);
                yield return Click(controller, mouse);
                Assert.That(controller.State.ActiveWork?.KindCode,
                    Is.EqualTo(Nature생존Codes.AreaBuildingConstruction),
                    controller.Status);
                Assert.That(controller.State.Workbench.StateCode,
                    Is.EqualTo(Nature생존Codes.Building));
                yield return PressKey(controller, keyboard, Key.C);
                Assert.That(controller.State.ActiveWork, Is.Null);
                Assert.That(controller.State.Workbench.StateCode,
                    Is.EqualTo(Nature생존Codes.Available));
                Assert.That(controller.State.TimberQuantity
                            + controller.State.StoredTimberQuantity,
                    Is.EqualTo(timberBeforeWorkbench));
                Assert.That(controller.State.RebuildPartQuantity,
                    Is.EqualTo(partsBeforeWorkbench));

                yield return PressKey(controller, keyboard, Key.Digit1);
                yield return Click(controller, mouse);
                Assert.That(controller.State.ActiveWork?.KindCode,
                    Is.EqualTo(Nature생존Codes.AreaBuildingConstruction),
                    controller.Status);
                yield return Click(controller, mouse,
                    new Vector2(226f, Screen.height - 197f));
                Assert.That(controller.WorkHoldRequested, Is.True);
                var workbenchDeadline = Time.unscaledTime
                    + Nature생존Codes.WorkbenchConstructionSeconds + 4f;
                while (controller.State.ActiveWork != null
                       && Time.unscaledTime < workbenchDeadline)
                    yield return null;
                UnityEngine.Object.Destroy(placementGround);
                Assert.That(controller.State.ActiveWork, Is.Null);
                Assert.That(controller.State.Workbench.StateCode,
                    Is.EqualTo(Nature생존Codes.Operational));
                Assert.That(controller.Status, Does.Not.Contain("차단"));

                var workbench = Marker(
                    Nature생존상호작용Kind.Workbench);
                Assert.That(workbench.UsesSyntyAsset, Is.True,
                    "Day 2 작업대는 설치된 Synty Table Saw로 식별 가능해야 한다.");
                FrameForEvidence(player, workbench, 5f);

                var evidenceDirectory = Path.Combine(Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "artifacts", "local", "validation", "nature-workbench-foundation");
                Directory.CreateDirectory(evidenceDirectory);
                var evidencePath = Path.Combine(evidenceDirectory,
                    "nature-workbench-wi-con-01-operational-game-view.png");
                if (File.Exists(evidencePath)) File.Delete(evidencePath);
                ScreenCapture.CaptureScreenshot(evidencePath, 1);
                yield return new WaitForEndOfFrame();
                var captureDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(evidencePath)
                       && Time.realtimeSinceStartup < captureDeadline)
                    yield return null;
                Assert.That(File.Exists(evidencePath), Is.True);
                Assert.That(new FileInfo(evidencePath).Length,
                    Is.GreaterThan(1024));
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_황혼조우에서_안전후퇴한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-wi11-retreat-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureWI11RetreatKeyboard");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();

                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                controller.AcquireAxeForTest();
                controller.EquipAxeForTest();
                foreach (var tree in controller.State.ResourceNodes
                             .OrderBy(value => value.StableId,
                                 StringComparer.Ordinal).Take(4))
                {
                    controller.BeginHarvestForTest(tree.StableId);
                    controller.HoldWorkForTest(
                        NatureSurvivalRules.HarvestWorkSeconds);
                    CollectAllDroppedTimber(controller);
                }
                while (controller.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DaylightEndsAtSecond)
                {
                    controller.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DaylightEndsAtSecond
                        - controller.State.ElapsedSecondsInCycle));
                }
                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Pending));

                yield return PressKey(controller, keyboard, Key.O);
                Assert.That(controller.ThreatObservationConfirmed, Is.True);
                yield return PressKey(controller, keyboard, Key.R);

                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Resolved));
                Assert.That(controller.State.LastCombatResultCode,
                    Is.EqualTo(Nature생존Codes.Retreat));
                Assert.That(controller.Status, Does.Contain("후퇴"));

                var evidenceDirectory = Path.Combine(Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "artifacts", "local", "validation", "nature-twilight");
                Directory.CreateDirectory(evidenceDirectory);
                var evidencePath = Path.Combine(evidenceDirectory,
                    "nature-twilight-wi11-retreat-result-game-view.png");
                if (File.Exists(evidencePath)) File.Delete(evidencePath);
                ScreenCapture.CaptureScreenshot(evidencePath, 1);
                yield return new WaitForEndOfFrame();
                var captureDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(evidencePath)
                       && Time.realtimeSinceStartup < captureDeadline)
                    yield return null;
                Assert.That(File.Exists(evidencePath), Is.True);
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SimulationWorldShell은_실제입력으로_황혼직접개입후_거점에복귀한다()
        {
            var savesRoot = Path.Combine(Path.GetTempPath(),
                "ssalddel-nature-wi11-direct-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(
                "SSALDDEL_NATURE_E7_VALIDATION", "1");
            Environment.SetEnvironmentVariable(
                "SSALDDEL_LOCAL_SAVE_ROOT", savesRoot);
            var keyboard = InputSystem.AddDevice<Keyboard>(
                "NatureWI11DirectKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>(
                "NatureWI11DirectMouse");
            try
            {
                Screen.SetResolution(1280, 720, false);
                yield return null;
                SimulationWorldLocalRuntimeScope.Reset();
                yield return SceneManager.LoadSceneAsync(
                    "SimulationWorldShell", LoadSceneMode.Single);
                Nature생존AutoBootstrap.InstallForCurrentScene();
                yield return WaitForNatureController();

                var controller = UnityEngine.Object.FindAnyObjectByType<
                    Nature생존Controller>(FindObjectsInactive.Include)!;
                var player = UnityEngine.Object.FindAnyObjectByType<
                    플레이어경관Controller>(FindObjectsInactive.Include)!;
                var combat = UnityEngine.Object.FindAnyObjectByType<
                    전투시점Controller>(FindObjectsInactive.Include)!;
                var battleRoot = UnityEngine.Object.FindAnyObjectByType<
                    현장전투CompositionRoot>(FindObjectsInactive.Include)!;

                player.EnterExplorationFirstPersonMode();
                player.TickCameraTransition(player.ViewTransitionDuration);
                player.enabled = false;
                Time.timeScale = 1f;

                // 이 시험의 E7 입력 범위는 O/F/전투 행동이다. 도끼·소음 전제는
                // 같은 Simulation Core API로 빠르게 준비해 긴 생활거점 회귀와 분리한다.
                controller.AcquireAxeForTest();
                controller.EquipAxeForTest();
                var trees = controller.State.ResourceNodes
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .Take(4).ToArray();
                Assert.That(trees.Length, Is.EqualTo(4));
                foreach (var tree in trees)
                {
                    controller.BeginHarvestForTest(tree.StableId);
                    controller.HoldWorkForTest(NatureSurvivalRules.HarvestWorkSeconds);
                    CollectAllDroppedTimber(controller);
                }
                while (controller.State.ElapsedSecondsInCycle <
                       NatureSurvivalRules.DaylightEndsAtSecond)
                {
                    controller.HoldWorkForTest(Math.Min(60,
                        NatureSurvivalRules.DaylightEndsAtSecond
                        - controller.State.ElapsedSecondsInCycle));
                }
                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Pending));

                yield return PressKey(controller, keyboard, Key.O);
                Assert.That(controller.ThreatObservationConfirmed, Is.True);
                Assert.That(controller.CurrentCombatParticipationLabel,
                    Is.EqualTo("1인칭 직접 개입"));

                var authorityDeadline = Time.realtimeSinceStartup + 10f;
                while (!battleRoot.AuthorityReady
                       && Time.realtimeSinceStartup < authorityDeadline)
                    yield return null;
                Assert.That(battleRoot.AuthorityReady, Is.True);
                yield return PressKey(controller, keyboard, Key.F);

                var battleRequestDeadline = Time.realtimeSinceStartup + 10f;
                while (battleRoot.Current == null
                       && Time.realtimeSinceStartup < battleRequestDeadline)
                    yield return null;
                Assert.That(battleRoot.Current, Is.Not.Null,
                    "F 실제 입력이 직접 개입 전투를 생성해야 한다. "
                    + $"status={controller.Status}; error={combat.LastAuthorityErrorCode}");

                var modeDeadline = Time.realtimeSinceStartup + 10f;
                while (!battleRoot.Current!.LocalCombat.ParticipationModeLocked
                       && Time.realtimeSinceStartup < modeDeadline)
                    yield return null;
                Assert.That(battleRoot.Current.LocalCombat.ControlModeCode,
                    Is.EqualTo("DirectAction"));

                var battleDeadline = Time.realtimeSinceStartup + 45f;
                while (battleRoot.Current.PhaseCode == "Active"
                       && Time.realtimeSinceStartup < battleDeadline)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = new Vector2(Screen.width * .5f,
                            Screen.height * .5f),
                    }.WithButton(MouseButton.Left));
                    InputSystem.Update();
                    mouse.MakeCurrent();
                    combat.TryHandleCombatInput();
                    yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState
                    {
                        position = new Vector2(Screen.width * .5f,
                            Screen.height * .5f),
                    });
                    InputSystem.Update();
                    mouse.MakeCurrent();
                    yield return null;
                }

                Assert.That(battleRoot.Current.PhaseCode, Is.EqualTo("Completed"),
                    "직접 개입 전투가 실제 공격 입력 뒤 제한 시간 안에 닫혀야 한다. "
                    + $"error={combat.LastAuthorityErrorCode}");
                Assert.That(controller.State.Encounter?.StateCode,
                    Is.EqualTo(Nature생존Codes.Resolved));
                Assert.That(controller.State.LastCombatResultCode,
                    Is.Not.Empty);
                Assert.That(controller.Status, Does.Contain("거점으로 복귀"));

                var evidenceDirectory = Path.Combine(Path.GetFullPath(
                    Path.Combine(UnityEngine.Application.dataPath, "..")), "artifacts",
                    "local", "validation", "nature-twilight");
                Directory.CreateDirectory(evidenceDirectory);
                var evidencePath = Path.Combine(evidenceDirectory,
                    "nature-twilight-wi11-direct-result-game-view.png");
                if (File.Exists(evidencePath)) File.Delete(evidencePath);
                ScreenCapture.CaptureScreenshot(evidencePath, 1);
                yield return new WaitForEndOfFrame();
                var captureDeadline = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(evidencePath)
                       && Time.realtimeSinceStartup < captureDeadline)
                    yield return null;
                Assert.That(File.Exists(evidencePath), Is.True);
                Assert.That(new FileInfo(evidencePath).Length,
                    Is.GreaterThan(1024));
            }
            finally
            {
                foreach (var activeController in UnityEngine.Object.FindObjectsByType<
                             Nature생존Controller>(FindObjectsInactive.Include))
                    activeController.enabled = false;
                SimulationWorldLocalRuntimeScope.Reset();
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_NATURE_E7_VALIDATION", null);
                Environment.SetEnvironmentVariable(
                    "SSALDDEL_LOCAL_SAVE_ROOT", null);
                if (Directory.Exists(savesRoot)) Directory.Delete(savesRoot, true);
            }
        }

        private static Nature생존상호작용표식 Marker(
            Nature생존상호작용Kind kind)
            => UnityEngine.Object.FindObjectsByType<Nature생존상호작용표식>(
                    FindObjectsInactive.Include)
                .First(value => value.Kind == kind);

        private static IEnumerator WaitForMarker(
            Nature생존상호작용Kind kind, float timeoutSeconds = 5f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var stableFrames = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (UnityEngine.Object.FindObjectsByType<
                        Nature생존상호작용표식>(FindObjectsInactive.Include)
                    .Any(value => value.Kind == kind))
                {
                    stableFrames++;
                    if (stableFrames >= 2)
                        yield break;
                }
                else
                {
                    stableFrames = 0;
                }
                yield return null;
            }

            Assert.Fail($"LH 재조립 뒤 {kind} 상호작용 표식이 준비되지 않았습니다.");
        }

        private static IEnumerator WaitForInteriorOverlay(
            string placementSuffix, float timeoutSeconds = 5f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var stableFrames = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (FindInteriorOverlay(placementSuffix) != null)
                {
                    stableFrames++;
                    if (stableFrames >= 2)
                        yield break;
                }
                else
                {
                    stableFrames = 0;
                }
                yield return null;
            }

            Assert.Fail($"LH 재조립 뒤 {placementSuffix} 실내 표현이 준비되지 않았습니다.");
        }

        private static GameObject? FindInteriorOverlay(string placementSuffix)
            => UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(value => value.name.StartsWith(
                        "InteriorOverlay_", StringComparison.Ordinal)
                    && value.name.EndsWith(placementSuffix,
                        StringComparison.Ordinal))?.gameObject;

        private static void CollectAllDroppedTimber(
            Nature생존Controller controller)
        {
            foreach (var dropped in controller.State.DroppedTimber
                         .Where(value => value.StateCode ==
                             Nature생존Codes.DroppedTimberAvailable)
                         .OrderBy(value => value.StableId,
                             StringComparer.Ordinal).ToArray())
                controller.CollectDroppedTimberForTest(dropped.StableId);
        }

        private static IEnumerator ClickMarker(Nature생존Controller controller,
            플레이어경관Controller player,
            Nature생존상호작용표식 marker, Mouse mouse)
        {
            var screenPoint = PointAt(player, marker);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = screenPoint,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            PointAt(player, marker);
            Assert.That(플레이어경관Controller.IsPointerInPersistentModeBand(
                screenPoint, Screen.height), Is.False,
                $"배치 객체 조준점이 하단 모드 영역과 겹칩니다: {screenPoint}");
            Assert.That(플레이어경관Controller.IsPointerInPersistentHudControls(
                screenPoint, Screen.width, Screen.height), Is.False,
                $"배치 객체 조준점이 HUD 버튼 영역과 겹칩니다: {screenPoint}");
            Assert.That(플레이어경관Controller.IsPointerOverUnityUi(), Is.False,
                $"배치 객체 조준점 위에 Unity UI가 입력을 가립니다: {screenPoint}");
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(mouse,
                new MouseState { position = screenPoint });
            InputSystem.Update();
            mouse.MakeCurrent();
            yield return null;
        }

        private static IEnumerator Click(Nature생존Controller controller,
            Mouse mouse, Vector2? requestedPosition = null)
        {
            var position = requestedPosition
                ?? new Vector2(Screen.width * .5f, Screen.height * .5f);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            Assert.That(controller.CurrentPrimaryInputPressedForTest, Is.True,
                "Nature controller did not observe the pressed mouse device.");
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            mouse.MakeCurrent();
            yield return null;
        }

        private static IEnumerator HoldPrimaryOnMarker(
            Nature생존Controller controller, 플레이어경관Controller player,
            Nature생존상호작용표식 marker, Mouse mouse, float seconds)
        {
            var position = PointAt(player, marker);
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = position,
            }.WithButton(MouseButton.Left));
            InputSystem.Update();
            mouse.MakeCurrent();
            PointAt(player, marker);
            controller.ProcessCurrentInputForTest();
            Assert.That(controller.State.ActiveWork, Is.Not.Null,
                $"지속 작업 시작 입력이 수락되지 않았습니다: {marker.Kind}/"
                + $"{marker.StableId}; status={controller.Status}; "
                + $"screen={position}; mode={player.CurrentMode}; "
                + $"distance={Vector3.Distance(player.transform.position, marker.transform.position)}; "
                + $"angle={Vector3.Angle(player.FirstPersonCamera.transform.forward, MarkerAimPoint(marker) - player.FirstPersonCamera.transform.position)}; "
                + $"active={marker.gameObject.activeInHierarchy}");

            var deadline = Time.unscaledTime + seconds * 2f + 2f;
            while (controller.State.ActiveWork != null
                   && Time.unscaledTime < deadline)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    position = position,
                }.WithButton(MouseButton.Left));
                InputSystem.Update();
                mouse.MakeCurrent();
                controller.ContinueCurrentInputForTest();
                yield return null;
            }

            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            mouse.MakeCurrent();
            controller.ContinueCurrentInputForTest();
            yield return null;
        }

        private static IEnumerator PressKey(Nature생존Controller controller,
            Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            keyboard.MakeCurrent();
            controller.ProcessCurrentInputForTest();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            keyboard.MakeCurrent();
            yield return null;
        }

        private static Vector2 EquipmentInventoryActionPoint()
        {
            var panelX = Mathf.Max(16f, Screen.width - 470f);
            const int slotCount = 8;
            var guiY = 16f + 72f + 24f + slotCount * 25f + 8f + 24f + 11f;
            return new Vector2(panelX + 296f, Screen.height - guiY);
        }

        private static Vector2 EquipmentSlotActionPoint(string slotCode)
        {
            var orderedSlots = new[]
            {
                SimulationActorEquipmentCodes.Accessory,
                SimulationActorEquipmentCodes.Back,
                SimulationActorEquipmentCodes.Body,
                SimulationActorEquipmentCodes.Feet,
                SimulationActorEquipmentCodes.Head,
                SimulationActorEquipmentCodes.Legs,
                SimulationActorEquipmentCodes.MainHand,
                SimulationActorEquipmentCodes.OffHand,
            };
            var slotIndex = Array.IndexOf(orderedSlots, slotCode);
            Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));
            var panelX = Mathf.Max(16f, Screen.width - 470f);
            var guiY = 16f + 72f + 24f + slotIndex * 25f + 11f;
            return new Vector2(panelX + 261f, Screen.height - guiY);
        }

        private static Vector2 PointAt(플레이어경관Controller player,
            Nature생존상호작용표식 marker)
        {
            var target = MarkerAimPoint(marker);
            player.transform.position = marker.transform.position
                + Vector3.back * 2.4f;
            player.transform.LookAt(new Vector3(target.x,
                player.transform.position.y, target.z));
            var camera = player.FirstPersonCamera;
            camera.transform.position = player.transform.position
                + Vector3.up * 1.35f;
            camera.transform.LookAt(target);
            Physics.SyncTransforms();
            return camera.WorldToScreenPoint(target);
        }

        private static void FrameForEvidence(플레이어경관Controller player,
            Nature생존상호작용표식 marker, float distance)
            => FrameForEvidence(player, marker.transform, distance,
                MarkerAimPoint(marker));

        private static void FrameForEvidence(플레이어경관Controller player,
            Transform targetTransform, float distance)
        {
            var hasVisibleBounds = 배치객체표면정렬Utility.TryGetVisibleBounds(
                targetTransform, out var visibleBounds);
            var target = hasVisibleBounds
                ? visibleBounds.center : targetTransform.position;
            FrameForEvidence(player, targetTransform, distance, target);
        }

        private static void FrameForEvidence(플레이어경관Controller player,
            Transform targetTransform, float distance, Vector3 target)
        {
            var natureController = targetTransform.GetComponentInParent<
                Nature생존Controller>();
            var approvedGroundWorldY = natureController != null
                ? natureController.ApprovedSurfaceWorldY
                : targetTransform.position.y;
            var groundFrameOrigin = new Vector3(targetTransform.position.x,
                approvedGroundWorldY, targetTransform.position.z);
            player.transform.position = groundFrameOrigin
                + Vector3.back * distance + Vector3.right * (distance * .3f);
            player.transform.LookAt(new Vector3(target.x,
                player.transform.position.y, target.z));
            var camera = player.FirstPersonCamera;
            camera.transform.position = player.transform.position
                + Vector3.up * 1.6f;
            camera.transform.LookAt(target);
            Physics.SyncTransforms();
        }

        private static IEnumerator CaptureEvidence(string directoryName,
            string fileName)
        {
            var evidenceDirectory = Path.Combine(Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..")),
                "artifacts", "local", "validation", directoryName);
            Directory.CreateDirectory(evidenceDirectory);
            var evidencePath = Path.Combine(evidenceDirectory, fileName);
            if (File.Exists(evidencePath)) File.Delete(evidencePath);
            ScreenCapture.CaptureScreenshot(evidencePath, 1);
            yield return new WaitForEndOfFrame();
            var captureDeadline = Time.realtimeSinceStartup + 5f;
            while (!File.Exists(evidencePath)
                   && Time.realtimeSinceStartup < captureDeadline)
                yield return null;
            Assert.That(File.Exists(evidencePath), Is.True);
            Assert.That(new FileInfo(evidencePath).Length,
                Is.GreaterThan(1024));
        }

        private static Vector3 MarkerAimPoint(Nature생존상호작용표식 marker)
        {
            var collider = marker.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : marker.transform.position;
        }

        private static IEnumerator WaitForNatureController()
        {
            var timeout = 10f;
            while (UnityEngine.Object.FindAnyObjectByType<Nature생존Controller>(
                       FindObjectsInactive.Include) == null && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(UnityEngine.Object.FindAnyObjectByType<Nature생존Controller>(
                FindObjectsInactive.Include), Is.Not.Null);
        }
    }
}
