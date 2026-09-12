using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Warehouse;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    /// <summary>업무 서버 전용 분기. LocalSimulationRuntime 또는 로컬 주문 엔진을 설치하지 않는다.</summary>
    public sealed partial class 가상배달관찰Controller
    {
        private 업무배달관찰ApiAdapter 업무서버Api;
        private 업무배달관찰Store 업무서버Store;
        private 업무배달관찰휴대폰View 업무서버휴대폰;
        private CancellationTokenSource 업무서버취소;
        private readonly Dictionary<string, 업무배달관찰표식View> 업무서버주체 = new();
        private Transform 업무서버선택표시;
        private string 업무서버선택 = "";
        private float 업무서버Ui갱신;
        private Vector2 업무서버맞춤화면크기;

        private void 업무서버관찰시작()
        {
            foreach (var modes in FindObjectsByType<통합월드ModePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                modes.업무서버관찰전용으로잠금();
            업무서버Store = new 업무배달관찰Store(); 업무서버Api = new 업무배달관찰ApiAdapter(); 업무서버취소 = new CancellationTokenSource();
            // 기존 도형/오행색/카메라 조립 함수를 재사용한다. 좌표는 업무상의 실제 주소·거리가 아니다.
            업무배달동네배경조립();
            // 휴대폰 여백에도 다른 영역의 카메라가 비치지 않도록 이 관찰 분기가 소유하는 단색 바탕만 합성한다.
            var backdrop = new GameObject("업무 배달 관찰 단색 바탕").AddComponent<Camera>(); backdrop.transform.SetParent(transform);
            backdrop.depth = 99; backdrop.rect = new Rect(0,0,1,1); backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor; backdrop.backgroundColor = new Color(.5f,.65f,.8f);
            observer = new GameObject("업무 배달 관찰 카메라").AddComponent<Camera>(); observer.transform.SetParent(transform);
            observer.depth = 100; observer.orthographic = true;
            observer.clearFlags = CameraClearFlags.SolidColor; observer.backgroundColor = new Color(.5f,.65f,.8f);
            observer.cullingMask = 1 << 31; observer.nearClipPlane = .1f; observer.farClipPlane = 300;
            업무서버전체동네맞춤();
            업무서버선택표시 = Block("관찰 대상 선택 표시", Vector3.zero, new Vector3(2,.05f,2), new Color(.95f,.8f,.3f));
            업무서버선택표시.gameObject.SetActive(false);
            var phone = new GameObject("업무 서버 관찰 휴대폰"); phone.transform.SetParent(transform);
            업무서버휴대폰 = phone.AddComponent<업무배달관찰휴대폰View>(); 업무서버휴대폰.Configure(업무서버Store);
            업무서버휴대폰.Warehouse = 마트입고;
            업무서버휴대폰.WarehouseRetryRequested += () => _ = 마트다시조회();
            업무서버휴대폰.StartRequested += () => _ = 업무서버요청(true);
            업무서버휴대폰.RetryRequested += () => _ = 업무서버요청(false);
            업무서버휴대폰.ActorSelected += id => 업무서버선택 = id;
            var selection = observer.gameObject.AddComponent<업무배달관찰선택Input>(); selection.Observer = observer;
            selection.Selected += id => 업무서버휴대폰.SelectActor(id);
            _ = 업무서버조회루프(업무서버취소.Token);
            _ = 마트검증자동연결();
        }
        private async Task 업무서버조회루프(CancellationToken cancel)
        {
            try { while (!cancel.IsCancellationRequested) { await 업무서버요청(false); await Task.Delay(1000, cancel); } }
            catch (OperationCanceledException) { }
        }
        private async Task 업무서버요청(bool start)
        {
            if (업무서버취소 == null || 업무서버취소.IsCancellationRequested || 업무서버Store == null) return;
            if (start && (업무서버Store.State?.Status != "Idle" || 업무서버Store.Error.Length > 0)) return;
            if (!업무서버Store.TryBegin()) return;
            if (start) 업무서버Store.AllowExplicitStart();
            try
            {
                var incoming = start ? await 업무서버Api.시작Async(업무서버취소.Token) : await 업무서버Api.조회Async(업무서버취소.Token);
                if (this != null && !업무서버취소.IsCancellationRequested && 업무서버Store.Accept(incoming, DateTime.UtcNow)) 업무서버주체연결(incoming);
            }
            catch (OperationCanceledException) { if (this != null && !업무서버취소.IsCancellationRequested) 업무서버Store.Fail(start ? "시작 응답 확인 필요 · 자동 시작 재시도 없음" : "조회 시간 초과 · 재시도 중"); }
            catch (System.IO.FileNotFoundException) { if (this != null) 업무서버Store.Fail("연결 파일 없음 · 검증 서버 준비 필요"); }
            catch (System.IO.DirectoryNotFoundException) { if (this != null) 업무서버Store.Fail("연결 파일 없음 · 검증 서버 준비 필요"); }
            catch (FormatException) { if (this != null) 업무서버Store.Fail("조회 계약 불일치 · 마지막 결과 유지"); }
            catch (Exception) { if (this != null) 업무서버Store.Fail(start ? "시작 응답 확인 필요 · 자동 시작 재시도 없음" : "서버 연결/조회 실패 · 재시도 중"); }
            finally { 업무서버Store.End(); if (this != null) 업무서버휴대폰.Refresh(); }
        }
        private void 업무서버주체연결(업무배달관찰Model model)
        {
            var incomingIds = new HashSet<string>();
            foreach (var actor in model.Actors)
            {
                incomingIds.Add(actor.Id);
                if (!업무서버주체.TryGetValue(actor.Id, out var marker))
                {
                    var color = actor.Role == "restaurant" ? 가상동네역할색상.음식점 : actor.Role == "orderer" ? 가상동네역할색상.주문자 : 가상동네역할색상.배송기사;
                    var root = Block("서버 NPC · " + actor.Name, new Vector3(actor.X,.9f,actor.Z), new Vector3(1,1.8f,1), color);
                    root.GetComponent<Collider>().enabled = true;
                    marker = root.gameObject.AddComponent<업무배달관찰표식View>(); marker.ActorId = actor.Id; 업무서버주체.Add(actor.Id, marker);
                    Label(root, actor.Name, Color.white);
                }
                marker.Target = new Vector3(actor.X,.9f,actor.Z);
            }
            var removed = new List<string>(); foreach (var pair in 업무서버주체) if (!incomingIds.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed) { Destroy(업무서버주체[id].gameObject); 업무서버주체.Remove(id); }
        }
        private void 업무서버관찰갱신()
        {
            if (업무서버휴대폰 == null || observer == null) return;
            마트서버관찰갱신();
            // 보간은 마지막 서버 위치 사이의 표시일 뿐, 도착·픽업·전달 명령을 만들지 않는다.
            foreach (var marker in 업무서버주체.Values) marker.transform.position = Vector3.Lerp(marker.transform.position, marker.Target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            var selected = 업무서버주체.TryGetValue(업무서버선택, out var value) ? value : null;
            var selectedTransform = 업무서버선택 == 마트입고관찰Store.WorkerId && martWorker != null && martWorker.gameObject.activeSelf
                ? martWorker : selected != null ? selected.transform : null;
            업무서버선택표시.gameObject.SetActive(selectedTransform != null);
            if (selectedTransform != null) 업무서버선택표시.position = new Vector3(selectedTransform.position.x,.12f,selectedTransform.position.z);
            var occupied = (412f / 720f) * Screen.height / Mathf.Max(1,Screen.width);
            observer.rect = new Rect(0,0,업무서버휴대폰.Shell.IsOpen ? Mathf.Clamp(1-occupied,.25f,1) : 1,1);
            업무서버전체동네맞춤();
            업무서버Ui갱신 += Time.unscaledDeltaTime;
            if (업무서버Ui갱신 >= .5f) { 업무서버Ui갱신 = 0; 업무서버휴대폰.Refresh(); }
        }
        private void 업무서버관찰종료()
        { 업무서버취소?.Cancel(); 업무서버Api?.Dispose(); }

        private void 업무서버전체동네맞춤()
        {
            var pixels = observer.pixelRect.size;
            if (pixels.x < 1 || pixels.y < 1 || pixels == 업무서버맞춤화면크기) return;
            업무서버맞춤화면크기 = pixels;
            var bounds = 업무배달동네배경Layout.관찰Bounds;
            var rotation = 동네전체관찰CameraMath.기본회전;
            observer.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * 120, rotation);
            observer.aspect = pixels.x / pixels.y;
            observer.orthographicSize = 동네전체관찰CameraMath.맞춤크기(bounds, rotation, observer.aspect);
        }
    }
}
