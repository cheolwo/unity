using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator CaptureExpansionProof(string directory)
        {
            Directory.CreateDirectory(directory);
            var proof = new ExpansionProofReceipt();
            var deadline = Time.realtimeSinceStartupAsDouble + 60;

            bool FailUnless(bool condition, string message)
            {
                if (condition && error == null) return false;
                proof.failure = error ?? message;
                FinishExpansionProof(directory, proof);
                return true;
            }

            holdProofClock = true;
            yield return Until(() => state != null && roadTexture != null && pending == null, deadline);
            if (FailUnless(state?.couriers?.Length == 2 && state.orders?.Length == 2 &&
                    roadTexture != null && pending == null,
                    "초기 기사 두 명과 주문 두 건을 표시하지 못했습니다."))
                yield break;

            BeginRound();
            proof.initial = state;
            proof.started = running && expansionView;
            if (FailUnless(proof.started, "확장 화면에서 기존 운영을 시작하지 못했습니다."))
                yield break;

            const string riderA = "management-driver-jungnang-a";
            const string riderB = MotorcycleRiderId;
            SelectCourier(riderB);
            expansionView = false;
            showDetails = false;
            ShowExpansionView();
            proof.detailRestored = showDetails && expansionView && !showDesk && !mapOnly &&
                !AttributionPanel.Overlaps(FocusPanelFor(FocusKind.Courier));
            if (FailUnless(proof.detailRestored, "경영 화면 복귀 시 상세가 보이지 않거나 출처와 겹쳤습니다."))
                yield break;
            var beforeTabs = JsonUtility.ToJson(state);
            proof.selectionPreserved = selectedDriver == riderB;
            for (var index = 0; index < 4; index++)
            {
                SelectExpansion(index);
                proof.selectionPreserved &= expansionSelection == index && expansionPanelOpen &&
                    selectedDriver == riderB && actorFocus == null &&
                    beforeTabs == JsonUtility.ToJson(state);
                yield return null;
            }
            expansionPanelOpen = false;
            yield return null;
            SelectExpansion(1);
            proof.selectionPreserved &= expansionSelection == 1 && expansionPanelOpen &&
                selectedDriver == riderB && actorFocus == null &&
                beforeTabs == JsonUtility.ToJson(state);
            if (FailUnless(proof.selectionPreserved,
                    "성장 영역이나 패널을 전환할 때 기사 선택 또는 업무 상태가 달라졌습니다."))
                yield break;

            actorFocus = null;
            SelectExpansion(1);
            proof.headerBlocksMap = ExpansionCovers(new Vector2(600, 40));
            proof.railBlocksMap = ExpansionCovers(new Vector2(200, 650));
            proof.panelBlocksMap = ExpansionCovers(new Vector2(1100, 200));
            proof.mapCenterAvailable = !ExpansionCovers(new Vector2(700, 500));
            if (FailUnless(proof.headerBlocksMap && proof.railBlocksMap && proof.panelBlocksMap &&
                    proof.mapCenterAvailable,
                    "확장 화면의 패널과 지도 입력 영역이 겹쳤습니다."))
                yield break;
            yield return SaveFrame(directory, "01-expansion-overview.png");

            for (var index = 0; index < 4; index++)
            {
                SelectExpansion(index);
                yield return SaveFrame(directory, $"02-expansion-{index}.png");
            }
            expansionPanelOpen = false;
            proof.closedPanelReleasesMap = !ExpansionCovers(new Vector2(1100, 200));
            if (FailUnless(proof.closedPanelReleasesMap,
                    "확장 패널을 닫은 뒤 지도의 입력 영역을 돌려주지 못했습니다."))
                yield break;
            yield return SaveFrame(directory, "03-expansion-panel-closed.png");

            SelectCourier(riderB);
            ConnectStore("jungnang-a");
            yield return Until(() => pending == null, deadline);
            if (FailUnless(pending == null && state.orders.Any(order =>
                    order.storeId == "jungnang-a" && order.driverId == riderB),
                    "확장 화면에서 기사 B의 A매장 배차가 이어지지 않았습니다."))
                yield break;

            SelectCourier(riderA);
            ConnectStore("jungnang-b");
            yield return Until(() => pending == null, deadline);
            proof.assignmentsMatch = pending == null && state.orders.Any(order =>
                order.storeId == "jungnang-a" && order.driverId == riderB) && state.orders.Any(order =>
                order.storeId == "jungnang-b" && order.driverId == riderA);
            proof.assigned = state;
            if (FailUnless(proof.assignmentsMatch, "두 기사와 매장의 기존 배차 결과가 달랐습니다."))
                yield break;

            Send(new DispatchRequest { action = "tick", elapsedMs = state.elapsedMs + 3_600_000 });
            yield return Until(() => pending == null && state.settled, deadline);
            proof.final = state;
            proof.settlementPreserved = pending == null && state.settled &&
                state.orders.Length == 2 && state.orders.All(order => order.stage == "Settled") &&
                state.settlement?.cleared == true && state.settlement.onTimeCount == 2 &&
                state.settlement.revenue == 9600 && state.settlement.driverPay == 7200 &&
                state.settlement.baseBalance == 2400 && state.settlement.bonus == 1200 &&
                state.settlement.balance == 3600;
            if (FailUnless(proof.settlementPreserved,
                    "확장 화면에서 두 주문의 수령과 가상 정산이 기존 결과와 달랐습니다."))
                yield break;

            actorFocus = null;
            SelectExpansion(1);
            yield return SaveFrame(directory, "04-expansion-settled.png");
            proof.passed = true;
            FinishExpansionProof(directory, proof);
        }

        private static void FinishExpansionProof(string directory, ExpansionProofReceipt proof)
        {
            File.WriteAllText(Path.Combine(directory, "unity-expansion-proof.json"), JsonUtility.ToJson(proof, true));
            Application.Quit(proof.passed && proof.failure == null ? 0 : 2);
        }

        [Serializable]
        private sealed class ExpansionProofReceipt
        {
            public string input = "Unity Windows Player에서 기존 시작·기사 선택·매장 배차·시간 진행과 성장 영역 선택 처리기를 호출한 검증";
            public bool manualClicksVerified;
            public bool passed;
            public string failure;
            public bool started;
            public bool selectionPreserved;
            public bool detailRestored;
            public bool headerBlocksMap;
            public bool railBlocksMap;
            public bool panelBlocksMap;
            public bool mapCenterAvailable;
            public bool closedPanelReleasesMap;
            public bool assignmentsMatch;
            public bool settlementPreserved;
            public DispatchState initial;
            public DispatchState assigned;
            public DispatchState final;
        }
    }
}
