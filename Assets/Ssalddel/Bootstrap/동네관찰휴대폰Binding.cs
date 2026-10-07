using System;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Observation;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using Ssalddel.Unity.Runtime.World;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        public event Action<동네관찰ScreenModel>? ObservationApplied;
        public 동네관찰ScreenModel? CurrentObservationModel => 휴대폰모델;

        private 동네관찰SessionController 동네실행;
        private 동네관찰휴대폰View 휴대폰;
        private 동네관찰ScreenModel 휴대폰모델;
        private object 마지막표시사본;
        private string 선택시설 = "";
        private bool 이전휴대폰열림;
        private 실제E5AreaSetNetworkController 운영지도Network;
        private DioramaTopDownCameraRig 운영지도Rig;
        private Camera 운영지도Camera;
        private bool 운영지도중;
        private bool 운영지도RigActiveBefore;
        private bool 운영지도CameraEnabledBefore;
        private bool 운영지도ObserverEnabledBefore;
        private DioramaCameraFocus 운영지도PreviousFocus;
        private void 휴대폰연결()
        {
            동네실행 = new 동네관찰SessionController(new 동네관찰RuntimeAdapter(scope, state.SessionStableId), state);
            var root = new GameObject("동네 관찰 휴대폰"); root.transform.SetParent(transform, false);
            휴대폰 = root.AddComponent<동네관찰휴대폰View>(); 휴대폰.구성();
            휴대폰.명령요청 += 휴대폰명령;
            휴대폰.주체관찰요청 += id => { 선택시설 = ""; 선택생활주체 = id; follow = true; Render(); };
            휴대폰.시설관찰요청 += id => { 선택시설 = id; follow = false; Render(); 시설카메라갱신(); };
            휴대폰.AreaSet관찰요청 += 운영지도관찰;
            휴대폰.생활현장복귀요청 += 운영지도복귀;
            동네실행.Changed += 휴대폰상태갱신;
            휴대폰상태갱신();
            if (SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested) { 휴대폰.열기(); 휴대폰.이동(동네휴대폰App.동네); }
        }
        private async void 휴대폰명령(string command)
        {
            if (동네실행 == null) return;
            if (공간표시차단) return;
            if (SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested && !사가정음식생활관찰Profile.관찰명령허용(command)) return;
            switch (command)
            {
                case "play": 동네실행.재생전환(); break;
                case "step": await 동네실행.한초진행Async(); break;
                case "save": await 동네실행.저장Async(); break;
                case "policy":
                    if (await 동네실행.정책적용Async(휴대폰.Draft.요청()) && this != null)
                    { 휴대폰.Draft.동기화(동네실행.State, true); 휴대폰상태갱신(); }
                    break;
                case "discard": 휴대폰.Draft.동기화(동네실행.State, true); 휴대폰상태갱신(); break;
                case "restart":
                    if (await 동네실행.재시작Async(true) && this != null)
                    { 선택시설 = ""; 선택생활주체 = ""; follow = false; 휴대폰.Draft.동기화(동네실행.State, true); 휴대폰상태갱신(); }
                    break;
                case "overview": 선택시설 = ""; 선택생활주체 = ""; follow = false; Render(); break;
            }
        }
        private void 휴대폰상태갱신()
        {
            if (this == null || 휴대폰 == null) return;
            state = 동네실행.State; playing = 동네실행.Playing; busy = 동네실행.Busy; 저장복구필요 = 동네실행.SaveBlocked;
            if (!공간표시차단) message = 동네실행.Message;
            if (!ReferenceEquals(마지막표시사본, state))
            {
                마지막표시사본 = state;
                휴대폰모델 = SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested
                    ? 사가정음식생활관찰Profile.조회(state) : 동네관찰Presenter.생성(state);
                Render();
                ObservationApplied?.Invoke(휴대폰모델);
            }
            휴대폰.Draft.동기화(state);
            휴대폰.표시(휴대폰모델, playing, busy, 저장복구필요 || 공간표시차단, state.IsCompleted, message,
                $"저장 {동네실행.LastSaveMs:F1}ms · 최대 {동네실행.PeakSaveMs:F1}ms\n명령+조회+저장 {동네실행.LastOperationMs:F1}ms\n이 실행에서 저장 {동네실행.SaveCount}회", 동네실행.CommandPending);
            운영지도목록갱신();
            시설카메라갱신();
        }
        private void LateUpdate()
        {
            if (휴대폰 == null || observer == null) return;
            if (이전휴대폰열림 != 휴대폰.IsOpen)
            {
                이전휴대폰열림 = 휴대폰.IsOpen;
                // 패널 아래 세계를 클릭할 수 없고 남은 화면에서 관찰을 계속한다.
                var occupied = (412f / 720f) * Screen.height / Mathf.Max(1, Screen.width);
                observer.rect = new Rect(0, 0, 휴대폰.IsOpen ? Mathf.Clamp(1 - occupied, .25f, 1) : 1, 1);
            }
            if (운영지도중) return;
            if (선택시설.Length == 0) 동네입체카메라갱신();
            시설카메라갱신();
        }
        private void 시설카메라갱신()
        {
            if (선택시설.Length == 0 || observer == null) return;
            if (SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested) { 사가정음식생활시설관찰(선택시설); return; }
            var target = 선택시설 switch {
                "depot" => new Vector3(-26, 0, -12), "mart" => new Vector3(-3, 0, -12),
                "restaurant" => new Vector3(10, 0, 7), "home-a" => new Vector3(30, 0, 27), _ => new Vector3(10, 0, 27) };
            observer.orthographicSize = 12; observer.transform.position = target + Ssalddel.Unity.Presentation.World.동네전체관찰CameraMath.근접관찰오프셋; observer.transform.LookAt(target);
        }

        private void 운영지도목록갱신()
        {
            if (휴대폰 == null) return;
            if (SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested) return;
            if (!운영지도찾기(out var reason))
            {
                휴대폰.운영지도표시(Array.Empty<운영지도AreaViewModel>(), reason);
                return;
            }
            휴대폰.운영지도표시(운영지도Network.GetObservationAreas(),
                운영지도중 ? "운영지도 관찰 중" : "지역을 선택하면 기존 Diorama 카메라가 해당 runtime Root를 봅니다.");
        }

        private bool 운영지도찾기(out string reason)
        {
            reason = "";
            if (운영지도Network == null)
            {
                var networks = FindObjectsByType<실제E5AreaSetNetworkController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (networks.Length != 1)
                {
                    reason = networks.Length == 0 ? "실제 E5 AreaSet network 미연결" : "실제 E5 AreaSet network 중복";
                    return false;
                }
                운영지도Network = networks[0];
            }
            if (!운영지도Network.Initialized)
            {
                reason = "실제 E5 AreaSet network 초기화 대기";
                return false;
            }
            if (운영지도Rig == null)
            {
                var rigs = FindObjectsByType<DioramaTopDownCameraRig>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (rigs.Length != 1)
                {
                    reason = rigs.Length == 0 ? "Diorama 관찰 카메라 미연결" : "Diorama 관찰 카메라 중복";
                    return false;
                }
                운영지도Rig = rigs[0];
                운영지도Camera = 운영지도Rig.TargetCamera;
            }
            if (운영지도Camera == null)
            {
                reason = "Diorama Camera 미연결";
                return false;
            }
            return true;
        }

        private async void 운영지도관찰(string areaSetStableId)
        {
            if (SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested) return;
            if (!운영지도찾기(out var reason))
            {
                휴대폰?.운영지도표시(Array.Empty<운영지도AreaViewModel>(), reason);
                return;
            }
            var wasMap = 운영지도중;
            var previousArea = 운영지도Network.ActiveAreaSetStableId;
            var rigActiveBeforeAttempt = 운영지도Rig.gameObject.activeSelf;
            var cameraEnabledBeforeAttempt = 운영지도Camera.enabled;
            var observerEnabledBeforeAttempt = observer != null && observer.enabled;
            var focusBeforeAttempt = 운영지도Rig.CurrentFocusAnchorId.Length == 0 ? null : new DioramaCameraFocus
            {
                AnchorId = 운영지도Rig.CurrentFocusAnchorId,
                LevelCode = 운영지도Rig.CurrentFocusLevelCode,
                Point = new DioramaPoint(운영지도Rig.CurrentFocusPosition.x,
                    운영지도Rig.CurrentFocusPosition.y, 운영지도Rig.CurrentFocusPosition.z),
            };
            if (!wasMap)
            {
                운영지도RigActiveBefore = 운영지도Rig.gameObject.activeSelf;
                운영지도CameraEnabledBefore = 운영지도Camera.enabled;
                운영지도ObserverEnabledBefore = observer != null && observer.enabled;
                운영지도PreviousFocus = 운영지도Rig.CurrentFocusAnchorId.Length == 0 ? null : new DioramaCameraFocus
                {
                    AnchorId = 운영지도Rig.CurrentFocusAnchorId,
                    LevelCode = 운영지도Rig.CurrentFocusLevelCode,
                    Point = new DioramaPoint(운영지도Rig.CurrentFocusPosition.x,
                        운영지도Rig.CurrentFocusPosition.y, 운영지도Rig.CurrentFocusPosition.z),
                };
            }
            try
            {
                var focus = await 운영지도Network.ObserveAreaAsync(areaSetStableId);
                if (this == null) return;
                운영지도Camera.enabled = false;
                if (!운영지도Rig.gameObject.activeSelf) 운영지도Rig.gameObject.SetActive(true);
                운영지도Rig.FocusAt(
                    focus.AnchorId,
                    focus.LevelCode,
                    new Vector3(focus.Point.X, focus.Point.Y, focus.Point.Z));
                if (observer != null) observer.enabled = false;
                운영지도Camera.enabled = true;
                운영지도중 = true;
                운영지도목록갱신();
            }
            catch (Exception exception)
            {
                Exception restoreError = null;
                if (!string.IsNullOrWhiteSpace(previousArea)
                    && !string.Equals(previousArea, 운영지도Network.ActiveAreaSetStableId,
                        StringComparison.Ordinal))
                {
                    try { await 운영지도Network.ObserveAreaAsync(previousArea); }
                    catch (Exception error) { restoreError = error; }
                }
                운영지도시도복원(focusBeforeAttempt, rigActiveBeforeAttempt,
                    cameraEnabledBeforeAttempt, observerEnabledBeforeAttempt, wasMap);
                Debug.LogException(exception, this);
                if (restoreError != null) Debug.LogException(restoreError, this);
                휴대폰?.운영지도표시(운영지도Network.GetObservationAreas(),
                    "지역 관찰 실패 · 기존 생활 현장을 유지합니다.");
            }
        }

        private void 운영지도시도복원(DioramaCameraFocus focus, bool rigActive,
            bool cameraEnabled, bool observerEnabled, bool wasMap)
        {
            if (운영지도Rig != null && focus != null && 운영지도Rig.gameObject.activeInHierarchy)
            {
                try
                {
                    운영지도Rig.FocusAt(focus.AnchorId, focus.LevelCode,
                        new Vector3(focus.Point.X, focus.Point.Y, focus.Point.Z));
                }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            if (운영지도Camera != null) 운영지도Camera.enabled = cameraEnabled;
            if (운영지도Rig != null && 운영지도Rig.gameObject.activeSelf != rigActive)
                운영지도Rig.gameObject.SetActive(rigActive);
            if (observer != null) observer.enabled = observerEnabled;
            운영지도중 = wasMap;
        }

        private void 운영지도복귀()
        {
            if (!운영지도중) return;
            운영지도카메라복원();
            운영지도목록갱신();
        }

        private void 운영지도카메라복원()
        {
            if (!운영지도중 && 운영지도Rig == null) return;
            if (운영지도Rig != null && 운영지도PreviousFocus != null && 운영지도Rig.gameObject.activeInHierarchy)
            {
                try
                {
                    운영지도Rig.FocusAt(
                        운영지도PreviousFocus.AnchorId,
                        운영지도PreviousFocus.LevelCode,
                        new Vector3(
                            운영지도PreviousFocus.Point.X,
                            운영지도PreviousFocus.Point.Y,
                            운영지도PreviousFocus.Point.Z));
                }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            if (운영지도Camera != null) 운영지도Camera.enabled = 운영지도CameraEnabledBefore;
            if (운영지도Rig != null && 운영지도Rig.gameObject.activeSelf != 운영지도RigActiveBefore)
                운영지도Rig.gameObject.SetActive(운영지도RigActiveBefore);
            if (observer != null) observer.enabled = 운영지도ObserverEnabledBefore;
            운영지도중 = false;
        }
    }
}
