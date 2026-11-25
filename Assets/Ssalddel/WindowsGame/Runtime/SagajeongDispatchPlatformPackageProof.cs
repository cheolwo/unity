using System.Collections;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 배달 플랫폼 패키지 뼈대 촬영 (2026-11-16). 129 패키지 고르기 · 130 동네(아래 줄 · 본사 띠) · 131 지사 쪽 · 132 구 지도 · 133 모든 사업 보기.
    //  - 촬영은 패키지를 메모리에서만 바꾼다(PlayerPrefs를 읽지도 쓰지도 않는다). 끝나면 처음 값으로 되돌린다.
    //  - 업무 · 시계 · 저장은 그대로다(시계는 촬영이 잡아 둔다). 경과 시간 · 원장 전후를 받은 값에 적는다.
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator ViewProofPackage(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            helpOpen = false;
            dongDeskOpen = false;
            dongPickerOpen = false;
            storeDeskPlace = null;
            actorFocus = null;
            selectedBuilding = null;
            showDesk = false;
            mapOnly = false;
            플랫폼장부열림 = false;
            expansionPanelOpen = false;
            var elapsedBefore = engine?.Kernel?.ElapsedMs ?? 0;
            var ledgerBefore = engine?.Kernel?.Ledger.Count ?? 0;
            var before = 패키지;
            ReturnToDong();
            for (var frame = 0; frame < 6; frame++) yield return null;

            // P0 패키지 고르기
            패키지고르기열림 = true;
            for (var frame = 0; frame < 3; frame++) yield return null;
            receipt.pkgPickerShown = 패키지고르기표시;
            yield return SaveFrame(directory, "129-pkg-picker.png");

            // 배달 플랫폼으로: 아래 줄 · 본사 띠
            패키지 = 패키지종류.배달플랫폼;
            패키지고르기열림 = false;
            모든사업보기 = false;
            플랫폼조회시각 = -1f;
            for (var frame = 0; frame < 6; frame++) yield return null;
            receipt.pkgRail = 플랫폼줄표시;
            receipt.pkgStrip = ChainStripVisible;
            yield return SaveFrame(directory, "130-pkg-dong.png");

            // 지사 쪽
            본사쪽 = 0;
            플랫폼장부열기();
            플랫폼조회시각 = -1f;
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.pkgBranchShown = 플랫폼장부표시 && 본사쪽 == 0;
            var 지사 = 지사캐시;
            receipt.pkgBranchDongs = 지사?.담당.Count ?? -1;
            receipt.pkgBranchRiders = 지사?.기사 ?? -1;
            receipt.pkgBranchPartners = 지사?.제휴 ?? -1;
            receipt.pkgBranchOrders = (float)(지사?.하루주문 ?? -1);
            receipt.pkgServed = 배달플랫폼Policy.비율글(지사?.받아냄);
            receipt.pkgOutsideDongs = 지사?.지사밖.Count ?? -1;
            receipt.pkgOutsideOrders = (float)(지사?.지사밖하루주문 ?? -1);
            receipt.pkgNeed = 시간대필요캐시 == null ? "자료 준비 전" : string.Join(",", 시간대필요캐시);
            receipt.pkgTodo = string.Join(" / ", 배달플랫폼Policy.정할일(지사, 저녁필요, 제휴부족동));
            yield return SaveFrame(directory, "131-pkg-branch.png");

            // 구 지도
            플랫폼장부열림 = false;
            LoadMapLevel(MapLevel.District);
            for (var frame = 0; frame < 8; frame++) yield return null;
            if (GuMapVisible) yield return SaveFrame(directory, "132-pkg-gu.png");

            // 모든 사업 보기(지금까지의 아래 줄) · 맨 왼쪽 「◀ 배달 플랫폼」
            ReturnToDong();
            모든사업보기 = true;
            for (var frame = 0; frame < 6; frame++) yield return null;
            receipt.pkgAllRail = !플랫폼줄표시 && 플랫폼돌아가기폭 > 0f;
            yield return SaveFrame(directory, "133-pkg-all.png");

            모든사업보기 = false;
            패키지 = before;
            패키지고르기열림 = false;
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.pkgStateSame = (engine?.Kernel?.ElapsedMs ?? 0) == elapsedBefore && (engine?.Kernel?.Ledger.Count ?? 0) == ledgerBefore;
            holdProofClock = false;
        }
    }
}
