using System;
using System.Collections;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 가까이 보기 (품1, 2026-11-14. 사용자: r216 시안 A를 보며 「이 지도마냥 3D적인 표현이 되면 좋겠다」 → 제안서의 권함대로 진행).
    //  - 휠로 끝까지 확대하면 건물이 크게 보이는 거리(화면 가로 약 200 m)까지 이어진다. 전의 한계는 4배였다.
    //  - 가까워질수록 내려다보는 각이 눕는다: 화면 가로 900 m에서 눕기 시작해 260 m에서 36°. 그보다 멀리서는 전과 똑같다.
    //  - 사용자가 고른 방향(회전)과 각은 「바탕 시점」으로 따로 기억한다. 화면이 쓰는 dioramaLens는 바탕 시점을 지금 배율만큼 눕힌 것이다.
    //    그래서 끌기 · 휠의 기준점 · 범위 붙잡기 · 표식 눕히기는 모두 지금 보이는 각으로 계산된다.
    //  - 동 · 역 판을 화면에 맞추는 계산은 멀리서 보는 것이므로 바탕 시점으로 한다.
    //  - 보는 방식만 바뀐다. 시계 · 주문 · 업무 상태는 건드리지 않는다. 평면 지도와 기존 화면의 한계는 전과 같다.
    public sealed partial class SagajeongDispatchView
    {
        private const float LegacyMaxMapZoom = 4f;             // 평면 지도 · 기존 화면의 확대 한계 (전과 같다)
        private const float CloseViewWidthMetres = 200f;        // 가장 가까이: 화면 가로에 담기는 땅 (m, 표현값)
        private const float CloseTiltStartMetres = 900f;        // 이보다 가까워지면 눕기 시작한다 (m, 표현값)
        private const float CloseTiltFullMetres = 260f;         // 이보다 가까우면 다 누웠다 (m, 표현값)
        private const double ClosePitchDegrees = 36;            // 다 누웠을 때의 내려다보는 각 (표현값)

        // 셰이더에 넘기는 값(전역). 멀리서는 모두 0이라 화면이 전과 같다 (품3, HongdalDiorama.shader).
        private static readonly int CloseId = Shader.PropertyToID("_HongdalClose");
        private static readonly int CloseFogId = Shader.PropertyToID("_HongdalFog");
        private static readonly int ClosePickId = Shader.PropertyToID("_HongdalPick");
        private static readonly int ClosePickColorId = Shader.PropertyToID("_HongdalPickColor");
        private static readonly int CloseRevealId = Shader.PropertyToID("_HongdalReveal");
        private static readonly int CloseRevealDirId = Shader.PropertyToID("_HongdalRevealDir");
        private static readonly int CloseRevealOwnId = Shader.PropertyToID("_HongdalRevealOwn");
        private const float CloseFogStrength = 0.5f;            // 가장 가까이에서 먼 땅이 바탕색에 섞이는 정도 (표현값)
        private const float CloseRevealMetres = 13f;            // 비침 창의 반경 (m, 표현값)

        private double dioramaBaseYaw = 디오라마시점.DefaultYawDegrees;        // 사용자가 고른 방향
        private double dioramaBasePitch = 디오라마시점.DefaultPitchDegrees;    // 사용자가 고른 각 (가까이에서 눕기 전)

        // 확대 한계. 행정동 모듈 2.5D에서는 화면 가로 CloseViewWidthMetres까지 간다. 그 밖에는 전과 같다.
        private float MaxMapZoom => dioramaOn && DongModulesLoaded && metresPerPixel > 0
            ? Mathf.Max(LegacyMaxMapZoom, (float)(Width * metresPerPixel / CloseViewWidthMetres))
            : LegacyMaxMapZoom;

        // 그 배율에서 화면 가로에 담기는 땅 (m)
        private float ViewWidthMetres(float zoom) => metresPerPixel > 0 && zoom > 0 ? (float)(Width * metresPerPixel / zoom) : 0f;

        // 0 = 멀리(전과 같다) … 1 = 다 누웠다
        private float CloseAmount(float zoom)
        {
            if (!dioramaOn || !DongModulesLoaded || mapLevel != MapLevel.Dong) return 0f;
            var width = ViewWidthMetres(zoom);
            if (width <= 0f) return 0f;
            var t = Mathf.InverseLerp(CloseTiltStartMetres, CloseTiltFullMetres, width);
            return t * t * (3f - 2f * t);
        }

        private float CloseNow => CloseAmount(mapZoom);

        // 가까이에서는 빛 한 점 · 고리를 조금 키운다. 건물이 커진 만큼 표식이 묻히지 않게.
        private float MarkScale => 1f + 0.6f * CloseNow;

        private 디오라마시점 BaseLens => 디오라마시점.Of(dioramaBaseYaw, dioramaBasePitch);

        private 디오라마시점 CloseLensFor(float zoom)
        {
            var low = Math.Min(dioramaBasePitch, ClosePitchDegrees);
            return 디오라마시점.Of(dioramaBaseYaw, dioramaBasePitch + (low - dioramaBasePitch) * CloseAmount(zoom));
        }

        // 그 배율에 맞는 시점으로 바꾼다. 바뀌었으면 true.
        private bool UseCloseLens(float zoom)
        {
            var next = CloseLensFor(zoom);
            if (Math.Abs(next.YawDegrees - dioramaLens.YawDegrees) < 1e-6 && Math.Abs(next.PitchDegrees - dioramaLens.PitchDegrees) < 1e-4) return false;
            dioramaLens = next;
            return true;
        }

        private bool SyncCloseLens() => UseCloseLens(mapZoom);

        // 가까이 보기에서 강조할 건물: 매장 창이 떠 있으면 그 매장, 아니면 고른 건물. 없으면 -1.
        private int ClosePick()
        {
            if (StoreDeskVisible && storeDeskPlace != null && buildingByPlace.TryGetValue(storeDeskPlace, out var store) &&
                store >= 0 && store < buildingShapes.Length) return store;
            return selectedBuilding is { } chosen && chosen >= 0 && chosen < buildingShapes.Length ? chosen : -1;
        }

        // 셰이더에 지금의 가까움 · 안개 · 고른 곳을 넘긴다 (LateUpdate, 카메라를 놓은 뒤).
        //  - 가까움: 창 불빛이 벽마다 달라지고 벽이 땅 쪽으로 어두워진다.
        //  - 안개: 화면 가운데보다 먼 땅이 바탕색으로 옅어진다.
        //  - 고른 곳: 그 건물이 조금 밝아지고, 그 건물을 가리는 앞 건물은 둥글게 비운다(비침 창).
        private void SyncCloseShader(Camera camera, Vector3 focus, float halfHeight)
        {
            var close = CloseNow;
            Shader.SetGlobalFloat(CloseId, close);
            SyncLamps(close);        // 길가 불빛(표현, SagajeongDispatchStreetLamps.cs)
            var forward = camera.transform.forward;
            var centreDepth = Vector3.Dot(focus - camera.transform.position, forward);
            var span = halfHeight / Mathf.Max(0.2f, Mathf.Tan((float)dioramaLens.PitchDegrees * Mathf.Deg2Rad));     // 가운데에서 화면 위 끝까지의 깊이 차
            Shader.SetGlobalVector(CloseFogId, new Vector4(centreDepth + 0.3f * span, centreDepth + 1.7f * span, CloseFogStrength * close, 0f));
            var chosen = close > 0.02f ? ClosePick() : -1;
            if (chosen < 0)
            {
                Shader.SetGlobalVector(ClosePickId, Vector4.zero);
                Shader.SetGlobalVector(CloseRevealId, Vector4.zero);
                return;
            }
            var shape = buildingShapes[chosen];
            var mid = (shape.Min + shape.Max) * 0.5f;
            var latitude = buildingIndex.origin.latitude + mid.y / buildingIndex.metresPerDegreeLatitude;
            var longitude = buildingIndex.origin.longitude + mid.x / buildingIndex.metresPerDegreeLongitude;
            var shift = Vector2.zero;
            var module = DongModuleById(shape.Record.region);
            if (module != null) shift = DongShift(module);
            var east = (float)((longitude - centerLongitude) * longitudeScale) + shift.x;
            var north = (float)((latitude - centerLatitude) * 111200) + shift.y;
            var radius = 0.5f * (shape.Max - shape.Min).magnitude + 1.5f;
            var store = StoreDeskVisible && storeDeskPlace != null;
            Shader.SetGlobalVector(ClosePickId, new Vector4(east, north, radius, 0.9f * close));
            Shader.SetGlobalColor(ClosePickColorId, store ? Lilac : Mint);
            var open = Mathf.SmoothStep(0f, 1f, (close - 0.35f) / 0.5f);
            Shader.SetGlobalVector(CloseRevealId, new Vector4(east, Mathf.Min(3f, shape.Record.height * 0.4f), north, CloseRevealMetres * open));
            Shader.SetGlobalVector(CloseRevealDirId, new Vector4(forward.x, forward.y, forward.z, 0f));
            Shader.SetGlobalFloat(CloseRevealOwnId, radius);
        }

        // 품5(2026-11-15): 읽어 들인 동 메시에서 지붕 테두리 정점(uv2.y = -1)과 근린생활시설 벽 정점(uv2.x = 1, uv2.y >= 1000)을 센다.
        // 계1 · 보1 · 거1(같은 날): 벽 · 테두리가 아닌 정점의 uv.x 표시도 센다. 1~4 녹지, 5 보도, 6 횡단보도 줄, 7 점자블록, 8 연석선, 9 시설물대.
        private void ViewProofRebakeCounts(ViewProofReceipt receipt)
        {
            var rims = 0;
            var shops = 0;
            var tags = new int[10];
            foreach (var mesh in dioramaMeshes)
            {
                if (mesh == null || !mesh.name.StartsWith("Dong ", StringComparison.Ordinal)) continue;
                var pairs = mesh.uv2;
                var marks = mesh.uv;
                for (var at = 0; at < pairs.Length; at++)
                {
                    if (pairs[at].y < -0.5f) rims++;
                    else if (pairs[at].x > 0.5f && pairs[at].y >= 999.5f) shops++;
                    else if (pairs[at].x < 0.5f && at < marks.Length)
                    {
                        var tag = Mathf.RoundToInt(marks[at].x);
                        if (tag >= 1 && tag <= 9 && Mathf.Abs(marks[at].x - tag) < 0.01f) tags[tag]++;
                    }
                }
            }
            receipt.rebakeRimVertices = rims;
            receipt.rebakeShopWallVertices = shops;
            receipt.greenVertices = tags[1] + tags[2] + tags[3] + tags[4];
            receipt.streetSidewalkVertices = tags[5];
            receipt.streetCrosswalkVertices = tags[6];
            receipt.streetTactileVertices = tags[7];
            receipt.streetKerbLineVertices = tags[8];
            receipt.streetFurnitureVertices = tags[9];
        }

        // 거리 풍경 보1 · 거1(2026-11-15): 사가정역 사거리 가운데(행정동 모듈 원점에서 동 2 m · 북 -30 m)를 가장 가까이에서 본다.
        private const double StreetProofLatitude = 37.58064221582734;
        private const double StreetProofLongitude = 127.08852469498217;

        // 방향 촬영: 카페 1호점을 가운데 두고 멀리 → 중간 → 가장 가까이로 당겨 본다. 각이 눕는지, 옥상을 눌러 그 건물이 골라지는지,
        // 멀리로 돌아오면 전의 각으로 돌아오는지, 보는 것만으로 시계 · 원장이 그대로인지 본다.
        private IEnumerator ViewProofCloseView(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            actorFocus = null;
            selectedBuilding = null;
            settingsOpen = false;
            dailyReportOpen = false;
            lightMenu = null;
            var elapsedBefore = engine.Kernel.ElapsedMs;
            var ledgerBefore = engine.Kernel.Ledger.Count;
            receipt.closeMetresPerPixel = (float)metresPerPixel;
            receipt.closeOldLimitMetres = ViewWidthMetres(LegacyMaxMapZoom);
            receipt.closeMaxZoom = MaxMapZoom;
            receipt.closeLimitMetres = ViewWidthMetres(MaxMapZoom);
            receipt.closeVSync = QualitySettings.vSyncCount;
            receipt.closeTargetFps = Application.targetFrameRate;
            var place = engine?.Pack?.network.store.placeId;
            if (place == null || !buildingByPlace.TryGetValue(place, out var cafe) || cafe < 0 || cafe >= buildingShapes.Length)
            {
                holdProofClock = false;
                yield break;
            }
            var mid = BuildingMid(cafe);
            var latitude = buildingIndex.origin.latitude + mid.y / buildingIndex.metresPerDegreeLatitude;
            var longitude = buildingIndex.origin.longitude + mid.x / buildingIndex.metresPerDegreeLongitude;
            GoToPlace(place);                    // 매장 창을 연 채로 본다
            yield return null;

            var steps = new[] { (1400f, "61-close-far.png"), (560f, "62-close-mid.png"), (CloseViewWidthMetres, "63-close-near.png") };
            for (var index = 0; index < steps.Length; index++)
            {
                var (width, file) = steps[index];
                ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / width));
                yield return null;
                yield return null;
                yield return null;
                var pitch = (float)dioramaLens.PitchDegrees;
                if (index == 0) receipt.closePitchFar = pitch;
                else if (index == 1) receipt.closePitchMid = pitch;
                else
                {
                    receipt.closePitchNear = pitch;
                    receipt.closeNearMetres = ViewWidthMetres(mapZoom);
                    receipt.closeDeskOpen = StoreDeskVisible;
                }
                if (index != 1)
                {
                    // 프레임 시간: 멀리와 가장 가까이에서 마흔 프레임의 평균 (ms). 그림을 저장하기 전에 잰다.
                    var total = 0f;
                    for (var frame = 0; frame < 40; frame++)
                    {
                        yield return null;
                        total += Time.unscaledDeltaTime;
                    }
                    if (index == 0) receipt.closeFrameMsFar = total / 40f * 1000f;
                    else receipt.closeFrameMsNear = total / 40f * 1000f;

                    // 수직 동기를 끈 프레임 시간(여유를 본다, 2026-11-15). 백스무 프레임의 평균과 가장 긴 한 프레임. 재고 나면 전의 설정으로 되돌린다.
                    var keepVSync = QualitySettings.vSyncCount;
                    var keepTarget = Application.targetFrameRate;
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    var freeTotal = 0f;
                    var freeWorst = 0f;
                    for (var frame = 0; frame < 120; frame++)
                    {
                        yield return null;
                        freeTotal += Time.unscaledDeltaTime;
                        freeWorst = Mathf.Max(freeWorst, Time.unscaledDeltaTime);
                    }
                    QualitySettings.vSyncCount = keepVSync;
                    Application.targetFrameRate = keepTarget;
                    if (index == 0)
                    {
                        receipt.closeFrameMsFarFree = freeTotal / 120f * 1000f;
                        receipt.closeFrameMsFarWorst = freeWorst * 1000f;
                    }
                    else
                    {
                        receipt.closeFrameMsNearFree = freeTotal / 120f * 1000f;
                        receipt.closeFrameMsNearWorst = freeWorst * 1000f;
                    }
                    receipt.closeFrameRestored = QualitySettings.vSyncCount == keepVSync && Application.targetFrameRate == keepTarget;
                    yield return null;
                }
                yield return SaveFrame(directory, file);
                if (index == 1)
                {
                    // 자료 창(2026-11-23 20:15): 동 디오라마에서 길 · 버스 · 생활 시설을 켜고 자료 창을 연 모습, 닫고 범례 줄 · 이름표만 본 모습.
                    var keepData = ProofDataPush(DataDefaultBits | 512 | 1024, FacilityAllMask);
                    dataPanelOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "62d-close-data-panel.png");
                    dataPanelOpen = false;
                    proofHoverAuto = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "62e-close-data-map.png");
                    proofHoverAuto = false;
                    ProofDataPop(keepData);
                    for (var frame = 0; frame < 2; frame++) yield return null;
                    // 입장 × 단위(21:30): 「배달 기사」를 고르면 동 단위 묶음(보행등 · 건물 입구 · 쉼터 · 역 출입구 · 길)이 켜진다. 저장하지 않는다.
                    var keepStance = ProofDataPush(DataBits(), facilityGroupMask);
                    ApplyStance(Array.Find(Stances, item => item.Key == "rider"), false);
                    dataPanelOpen = true;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "62f-stance-rider-dong-panel.png");
                    dataPanelOpen = false;
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return SaveFrame(directory, "62g-stance-rider-dong.png");
                    ProofDataPop(keepStance);
                    for (var frame = 0; frame < 2; frame++) yield return null;
                    // 단위 × 자료 시각 검증(2026-11-23 21:43): 동 단위 (SagajeongDispatchVisualAudit.cs)
                    yield return ViewProofVisualAuditDong(directory, latitude, longitude);
                }
            }

            // 가장 가까이에서: 옥상 가운데를 눌렀을 때 그 건물이 골라지는가 (표식 · 누르기가 새 각을 따라오는가)
            var (sampled, picked) = ViewProofRoofPicks();
            receipt.closeRoofsSampled = sampled;
            receipt.closeRoofsPicked = picked;

            // 매장 창을 닫은 지도만
            storeDeskPlace = null;
            selectedBuilding = null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "64-close-near-map.png");
            yield return ViewProofLamps(directory, receipt);        // 길가 불빛: 켠 그림과 끈 그림
            ViewProofRebakeCounts(receipt);                         // 다시 구운 동 메시: 지붕 테두리 · 1층 가게 벽 · 녹지 · 거리 겹
            ViewProofCentre(StreetProofLatitude, StreetProofLongitude, (float)(Width * metresPerPixel / CloseViewWidthMetres));
            yield return null;
            yield return null;
            yield return null;
            yield return SaveFrame(directory, "78-street-crossing.png");   // 보도 블록 · 연석선 · 횡단보도 · 점자블록(가까이에서만 칠한다)

            // 멀리로 돌아오면 전의 각이다
            ViewProofCentre(latitude, longitude, (float)(Width * metresPerPixel / 1400f));
            yield return null;
            yield return null;
            receipt.closePitchBack = (float)dioramaLens.PitchDegrees;
            receipt.lampFar = Shader.GetGlobalFloat(LampId);        // 멀리서는 0이어야 전과 같다
            ShowWholeDong();
            yield return null;
            yield return null;
            receipt.closeUnchanged = engine.Kernel.ElapsedMs == elapsedBefore && engine.Kernel.Ledger.Count == ledgerBefore;
            holdProofClock = false;
        }
    }
}
