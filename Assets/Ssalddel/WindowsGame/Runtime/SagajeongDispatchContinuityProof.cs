using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 이어지는 지도 촬영 (2026-11-16). 사가정을 화면 가운데에서 조금 비켜 두고 전국 1배 → 3배 → 10배 → 30배 → 넘김 배율로 들어간 뒤
    // 같은 자리의 구 지도로 넘기고, 다시 전국으로, 동네에서 구로 넘기는 장면을 찍는다. 넘김 앞뒤 사가정의 화면 자리를 받은 값에 적는다.
    //  - 지도 높이와 카메라만 바뀐다. 업무 · 시계 · 저장은 그대로다(시계는 촬영이 잡아 둔다).
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator ViewProofContinuity(string directory, ViewProofReceipt receipt)
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
            var elapsedBefore = engine?.Kernel?.ElapsedMs ?? 0;
            var ledgerBefore = engine?.Kernel?.Ledger.Count ?? 0;
            LoadMapLevel(MapLevel.Nation);
            for (var frame = 0; frame < 6; frame++) yield return null;
            LoadRegion();
            receipt.contShown = NationMapVisible && NationDeepReady;
            if (!receipt.contShown)
            {
                ReturnToDong();
                holdProofClock = false;
                yield break;
            }
            receipt.contHandoffScale = NationHandoffScale;
            var home = 이어지는지도Policy.전국세계(HomeLongitude, HomeLatitude);
            var spot = new Vector2(Field.width * 0.5f - 160f, Field.height * 0.5f + 40f);      // 사가정을 둘 화면 자리(가운데에서 비켜)
            var scales = new[] { 1f, 3f, 10f, 30f, NationHandoffScale };
            var names = new[] { "118-cont-nation-1.png", "119-cont-nation-3.png", "120-cont-region-10.png", "121-cont-region-30.png", "122-cont-handoff.png" };
            receipt.contMetres = new float[scales.Length + 2];
            receipt.contHomeX = new float[scales.Length + 2];
            receipt.contHomeY = new float[scales.Length + 2];
            for (var i = 0; i < scales.Length; i++)
            {
                nationScale = Mathf.Clamp(scales[i], NationFloor, NationScaleCap);
                nationOrigin = spot - Vector2.Scale(home, NationScaleXY);
                nationManual = true;
                nationCameraSet = true;
                ClampNation();
                for (var frame = 0; frame < 4; frame++) yield return null;
                var at = NationPoint(home);
                receipt.contMetres[i] = 이어지는지도Policy.전국미터(nationScale);
                receipt.contHomeX[i] = at.x;
                receipt.contHomeY[i] = at.y;
                yield return SaveFrame(directory, names[i]);
            }

            // 전국 → 구 (화면 가운데를 붙잡고)
            var centre = new Vector2(Field.width * 0.5f, Field.height * 0.5f);
            NationToGu(centre);
            for (var frame = 0; frame < 2; frame++) yield return null;
            if (GuMapVisible) yield return SaveFrame(directory, "123a-cont-gu-veil.png");      // 넘긴 직후: 구 바탕이 덮인 채
            yield return new WaitForSecondsRealtime(이어지는지도Policy.넘김가림초 + 0.1f);
            receipt.contGuShown = GuMapVisible;
            if (receipt.contGuShown)
            {
                var metres = 이어지는지도Policy.구미터(HomeLongitude, HomeLatitude, guSpace.west, guSpace.south);
                var at = GuPoint(metres.x, metres.y);
                var i = scales.Length;
                receipt.contMetres[i] = 이어지는지도Policy.구미터점(guScale);
                receipt.contHomeX[i] = at.x;
                receipt.contHomeY[i] = at.y;
                receipt.contHandoffDx = at.x - receipt.contHomeX[i - 1];
                receipt.contHandoffDy = at.y - receipt.contHomeY[i - 1];
                yield return SaveFrame(directory, "123-cont-gu.png");

                // 구 → 전국
                GuToNation(centre);
                for (var frame = 0; frame < 6; frame++) yield return null;
                receipt.contBackNation = mapLevel == MapLevel.Nation;
                var back = NationPoint(home);
                receipt.contMetres[i + 1] = 이어지는지도Policy.전국미터(nationScale);
                receipt.contHomeX[i + 1] = back.x;
                receipt.contHomeY[i + 1] = back.y;
                receipt.contReturnDx = back.x - receipt.contHomeX[i];
                receipt.contReturnDy = back.y - receipt.contHomeY[i];
                yield return SaveFrame(directory, "124-cont-back-nation.png");
            }

            // 동네 전체 보기 → 구
            ReturnToDong();
            for (var frame = 0; frame < 6; frame++) yield return null;
            DongToGu();
            for (var frame = 0; frame < 6; frame++) yield return null;
            receipt.contDongToGu = GuMapVisible;
            receipt.contDongToGuMetres = GuMapVisible ? 이어지는지도Policy.구미터점(guScale) : 0f;
            if (GuMapVisible) yield return SaveFrame(directory, "125-cont-dong-to-gu.png");

            ReturnToDong();
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.contBack = mapLevel == MapLevel.Dong;
            receipt.contStateSame = (engine?.Kernel?.ElapsedMs ?? 0) == elapsedBefore && (engine?.Kernel?.Ledger.Count ?? 0) == ledgerBefore;
            holdProofClock = false;
        }

        // 정밀 3 · 휠 왕복(2026-11-16): 실제 휠과 같은 길(ApplyMapWheel)로 한 칸씩(±3) 굴린다. 칸 사이에는 그림이 두 번 돈다.
        //  1) 동네 전체 보기 → 바깥으로 끝까지(동 → 구 → 전국 가장 넓게)
        //  2) 가장 넓은 전국에서 한꺼번에 12칸 더(관성 스크롤): 단계가 더 바뀌면 안 된다
        //  3) 마우스로 사가정을 따라가며 안으로(전국 → 구 → 동). 확대 중 사가정이 마우스에서 얼마나 벗어나는지 잰다.
        //  OS의 입력 층만 빼고 휠 처리 전부(막힘 시간 · 바닥 · 넘김)를 지난다. 시계 · 원장은 그대로여야 한다.
        private IEnumerator ViewProofWheel(string directory, ViewProofReceipt receipt)
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
            var elapsedBefore = engine?.Kernel?.ElapsedMs ?? 0;
            var ledgerBefore = engine?.Kernel?.Ledger.Count ?? 0;
            ReturnToDong();
            for (var frame = 0; frame < 8; frame++) yield return null;
            var transitions = new List<string>();
            var last = mapLevel;
            var promptSeen = false;
            void Note()
            {
                if (mapLevel != last)
                {
                    transitions.Add(last + ">" + mapLevel);
                    last = mapLevel;
                }
                if (levelPromptOpen) promptSeen = true;
            }

            // 1) 바깥으로
            var mouse = Field.position + Field.size * 0.5f;
            var outNotches = 0;
            for (var i = 0; i < 160; i++)
            {
                ApplyMapWheel(3f, mouse, Time.realtimeSinceStartupAsDouble);
                outNotches++;
                for (var frame = 0; frame < 2; frame++) yield return null;
                Note();
                if (mapLevel == MapLevel.Nation && NationReady && nationScale <= NationFloor * 1.002f) break;
            }
            receipt.wheelOutNotches = outNotches;
            receipt.wheelOutLevels = string.Join(",", transitions);
            receipt.wheelOutReachedNation = mapLevel == MapLevel.Nation;
            yield return SaveFrame(directory, "126-wheel-out-nation.png");

            // 2) 관성: 한꺼번에 12칸
            var before = transitions.Count;
            for (var i = 0; i < 12; i++) ApplyMapWheel(3f, mouse, Time.realtimeSinceStartupAsDouble);
            for (var frame = 0; frame < 2; frame++) yield return null;
            Note();
            receipt.wheelInertiaExtra = transitions.Count - before;

            // 3) 사가정을 따라가며 안으로
            var home = 이어지는지도Policy.전국세계(HomeLongitude, HomeLatitude);
            var inStart = transitions.Count;
            var inNotches = 0;
            var driftNation = 0f;
            var driftGu = 0f;
            var savedGu = false;
            for (var i = 0; i < 220; i++)
            {
                if (mapLevel == MapLevel.Nation && NationReady) mouse = Field.position + NationPoint(home);
                else if (mapLevel == MapLevel.District && GuReady)
                {
                    var at = 이어지는지도Policy.구미터(HomeLongitude, HomeLatitude, guSpace.west, guSpace.south);
                    mouse = Field.position + GuPoint(at.x, at.y);
                }
                else break;
                var levelBefore = mapLevel;
                wheelToDongOpen = true;      // 상위 판의 길(휠로 동네 지도까지)이 살아 있는지 본다. 끝나면 판 값으로 되돌린다
                ApplyMapWheel(-3f, mouse, Time.realtimeSinceStartupAsDouble);
                wheelToDongOpen = 판Policy.휠로동지도;
                inNotches++;
                for (var frame = 0; frame < 2; frame++) yield return null;
                Note();
                if (mapLevel == levelBefore && mapLevel == MapLevel.Nation)
                    driftNation = Mathf.Max(driftNation, Vector2.Distance(Field.position + NationPoint(home), mouse));
                else if (mapLevel == MapLevel.District && GuReady)
                {
                    var at = 이어지는지도Policy.구미터(HomeLongitude, HomeLatitude, guSpace.west, guSpace.south);
                    driftGu = Mathf.Max(driftGu, Vector2.Distance(Field.position + GuPoint(at.x, at.y), mouse));
                    if (!savedGu)
                    {
                        savedGu = true;
                        yield return new WaitForSecondsRealtime(이어지는지도Policy.넘김가림초 + 0.1f);
                        yield return SaveFrame(directory, "127-wheel-in-gu.png");
                    }
                }
                if (mapLevel == MapLevel.Dong) break;
            }
            receipt.wheelInNotches = inNotches;
            receipt.wheelInLevels = string.Join(",", transitions.GetRange(inStart, transitions.Count - inStart));
            receipt.wheelDriftNation = driftNation;
            receipt.wheelDriftGu = driftGu;
            receipt.wheelBackDong = mapLevel == MapLevel.Dong;
            receipt.wheelTransitions = transitions.Count;
            receipt.wheelPromptSeen = promptSeen;
            for (var frame = 0; frame < 6; frame++) yield return null;
            if (mapLevel == MapLevel.Dong) yield return SaveFrame(directory, "128-wheel-in-dong.png");
            ReturnToDong();
            for (var frame = 0; frame < 4; frame++) yield return null;
            receipt.wheelStateSame = (engine?.Kernel?.ElapsedMs ?? 0) == elapsedBefore && (engine?.Kernel?.Ledger.Count ?? 0) == ledgerBefore;
            holdProofClock = false;
        }
    }
}
