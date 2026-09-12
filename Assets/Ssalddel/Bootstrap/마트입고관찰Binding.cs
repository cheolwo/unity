using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.UrbanMarket;
using Ssalddel.Unity.Runtime.Transport;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    public sealed partial class 가상배달관찰Controller
    {
        public 마트입고관찰Store 마트입고 { get; } = new();
        private 마트입고관찰Repository 마트조회;
        private CancellationTokenSource 마트조회취소;
        private Vector3[] 마트경로 = Array.Empty<Vector3>();
        private int 마트경로순번;
        private string 마트표현키 = "";
        private bool 마트대기복귀;
        private TextMesh 마트상태표시;
        private string[] 마트보관위치 = Array.Empty<string>();
        private 마트입고검증Connection 마트검증연결;
        private bool 마트종료됨;

        private async Task 마트검증자동연결()
        {
            // 실행 모드를 추가하지 않는다. 기존 서버 관찰 모드에서 선택적으로 지정하는 연결 파일이다.
            var path = Environment.GetEnvironmentVariable("SSALDDEL_WAREHOUSE_OBSERVER_CONNECTION");
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var connection = await 마트입고검증Connection.LoadAsync(path, CancellationToken.None);
                if (this == null || 마트종료됨) { connection.Dispose(); return; }
                마트검증연결 = connection;
                Configure마트입고(connection, connection.WarehouseId, connection.StorageLocations);
            }
            catch (Exception) { if (this != null) 마트입고.Fail("창고 검증 연결 설정 확인 필요 · 자동 표본 대체 없음"); }
        }

        /// <summary>기존 로그인 연결이 주입한다. 서버 모드에서만 활성화하며 자격증명은 Scene에 저장하지 않는다.</summary>
        public void Configure마트입고(IOperationalUnityApiClient client, long warehouseId, string[] storageLocations)
        {
            if (!UnityEngine.Application.isPlaying || !SimulationWorldLocalRuntimeScope.BusinessServerObserverRequested)
                throw new InvalidOperationException("WarehouseObservationRequiresBusinessServerPlay");
            if (마트조회 != null) throw new InvalidOperationException("WarehouseObservationAlreadyConfigured");
            if (storageLocations == null || storageLocations.Length != 2 || string.IsNullOrWhiteSpace(storageLocations[0])
                || string.IsNullOrWhiteSpace(storageLocations[1]) || storageLocations[0] == storageLocations[1])
                throw new ArgumentException("WarehouseObservationRequiresTwoDistinctShelfLocations");
            마트보관위치 = (string[])storageLocations.Clone();
            마트조회 = new 마트입고관찰Repository(client, warehouseId);
            마트조회취소 = new CancellationTokenSource();
            _ = 마트조회반복(마트조회취소.Token);
        }

        private async Task 마트조회반복(CancellationToken cancel)
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    await 마트다시조회();
                    await Task.Delay(1000, cancel);
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        }

        public async Task 마트다시조회()
        {
            if (마트조회 == null || !마트입고.TryBegin()) return;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(마트조회취소.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(4));
                var value = await 마트조회.LoadAsync(deadline.Token);
                if (this == null || 마트조회취소.IsCancellationRequested) return;
                마트입고.Accept(value);
            }
            catch (OperationCanceledException) { if (this != null) 마트입고.Fail("창고 조회 중단/시간 초과 · 마지막 확인 자료 유지"); }
            catch (Exception) { if (this != null) 마트입고.Fail("창고 연결·권한·자료 확인 필요 · 마지막 확인 자료 유지"); }
            finally { 마트입고.End(); }
        }

        private void 마트서버관찰갱신()
        {
            if (martWorker == null || 마트입고.State == null) return;
            if (!martWorker.gameObject.activeSelf)
            {
                martWorker.gameObject.SetActive(true);
                martWorker.position = 마트공통공간.대기;
                var marker = martWorker.GetComponent<업무배달관찰표식View>() ?? martWorker.gameObject.AddComponent<업무배달관찰표식View>();
                marker.ActorId = 마트입고관찰Store.WorkerId;
                martWorker.GetComponent<Collider>().enabled = true;
                마트상태표시 = martWorker.GetComponentInChildren<TextMesh>();
            }
            if (마트상태표시 != null) 마트상태표시.text = "마트 작업자\n" + (마트입고.Error.Length > 0 ? "연결 대기" : 마트입고.Stage) + " · 모의 표현";
            // 조회 오류 중에는 과거 자료를 향한 동작도 정지한다.
            if (마트입고.Error.Length > 0) return;
            var item = 마트입고.Item;
            var shelfIndex = item == null ? -1 : Array.IndexOf(마트보관위치, item.StorageLocation);
            var key = 마트입고.SelectedItemId + ":" + 마트입고.Stage + ":" + (item?.StorageLocation ?? "");
            if (key != 마트표현키)
            {
                마트표현키 = key;
                var target = 마트입고.Stage == "입고 확인" ? 마트공통공간.입고
                    : 마트입고.Stage == "검수 확인" ? 마트공통공간.검수
                    : 마트입고.Stage == "적치 확인" && shelfIndex >= 0 ? 마트공통공간.선반접근 + Vector3.right * (shelfIndex * 4) : 마트공통공간.대기;
                마트경로 = 마트공통공간.이동경로(martWorker.position, target);
                마트경로순번 = 0;
                마트대기복귀 = 마트입고.Stage == "적치 확인" && shelfIndex >= 0;
            }
            if (마트경로순번 < 마트경로.Length)
            {
                martWorker.position = Vector3.MoveTowards(martWorker.position, 마트경로[마트경로순번], Mathf.Min(Time.unscaledDeltaTime, .1f) * 2);
                if (Vector3.Distance(martWorker.position, 마트경로[마트경로순번]) < .01f) 마트경로순번++;
            }
            else if (마트대기복귀)
            {
                마트대기복귀 = false;
                마트경로 = 마트공통공간.이동경로(martWorker.position, 마트공통공간.대기);
                마트경로순번 = 0;
            }
            // 표시 동작 완료는 재고/업무 명령을 발행하지 않는다. 수량은 서버 결과만 사용한다.
            for (int i = 0; i < shelfGoods.Length; i++)
            {
                var stored = i == shelfIndex && 마트입고.Stage == "적치 확인" && item.AvailableQuantity > 0;
                shelfGoods[i].gameObject.SetActive(stored);
                if (stored) shelfGoods[i].localScale = Vector3.one * .7f;
            }
            workerParcel.gameObject.SetActive(false);
            handoffParcel.gameObject.SetActive(false);
            if (마트상태표시 != null && 마트입고.Stage == "적치 확인")
                마트상태표시.text = shelfIndex < 0 ? "마트 작업자\n보관 위치 배치 연결 필요" : !마트대기복귀 && 마트경로순번 >= 마트경로.Length
                    ? "마트 작업자\n대기 · 적치 결과 확인됨" : "마트 작업자\n적치 결과 확인 · 모의 표현";
        }

        private void 마트서버관찰종료() { 마트종료됨 = true; 마트조회취소?.Cancel(); 마트검증연결?.Dispose(); }
    }
}
