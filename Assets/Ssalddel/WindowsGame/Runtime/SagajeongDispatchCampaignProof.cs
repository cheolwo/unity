using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private IEnumerator CaptureCampaignProof(string directory)
        {
            expansionView = false;
            showDesk = true;
            showTime = true;
            Directory.CreateDirectory(directory);
            var receipt = new CampaignProofReceipt();
            var deadline = Time.realtimeSinceStartupAsDouble + 115;
            var orderIds = new HashSet<string>();
            DispatchCourier RiderB() => state?.couriers?.FirstOrDefault(courier => courier.id == MotorcycleRiderId);
            bool SelectFromLight(string riderId)
            {
                var rider = state.couriers.First(courier => courier.id == riderId);
                var position = PlotScene(rider.latitude, rider.longitude);
                var screenPoint = position + Field.position;
                if (tileIndex == null || !Field.Contains(screenPoint) || MapOverlayCovers(screenPoint))
                    return false;

                pressPoint = releasePoint = position;
                mapClick = true;
                mapDoubleClick = false;
                TouchCourierLight(rider, position);
                return IsFocused(FocusKind.Courier, riderId) && !mapClick;
            }

            bool FailUnless(bool condition, string message)
            {
                if (condition && error == null) return false;
                receipt.failure = error ?? message;
                FinishCampaignProof(directory, receipt);
                return true;
            }

            yield return Until(() => state?.campaign?.day == 1 && roadTexture != null && pending == null, deadline);
            if (FailUnless(state?.campaign?.day == 1 && roadTexture != null && pending == null,
                    "첫날의 지도와 운영 시계를 표시하지 못했습니다."))
                yield break;

            var lightColours = new Dictionary<string, Color>();
            courierLightProbe = (courier, _, colour) => lightColours[courier.id] = colour;
            yield return new WaitForEndOfFrame();
            courierLightProbe = null;
            BeginRound();
            for (var day = 1; day <= 3; day++)
            {
                var currentDay = day;
                if (FailUnless(state.campaign.day == currentDay && state.campaign.phase == "open" &&
                        state.orders.Length == 2 && state.orders.All(order => orderIds.Add(order.id)),
                        $"{day}일차의 서로 다른 주문 두 건을 열지 못했습니다."))
                    yield break;

                if (day == 1)
                {
                    receipt.distinctCourierLights = lightColours.TryGetValue("management-driver-jungnang-a", out var a) && a == Mint &&
                        lightColours.TryGetValue(MotorcycleRiderId, out var b) && b == Lilac && a != b;
                    receipt.riderBSelectedFromLight = SelectFromLight(MotorcycleRiderId);
                    if (FailUnless(receipt.distinctCourierLights && receipt.riderBSelectedFromLight,
                            "두 기사의 빛 색 또는 B 기사의 빛 선택을 확인하지 못했습니다."))
                        yield break;
                }
                else
                    SelectCourier(MotorcycleRiderId);
                ConnectStore("jungnang-a");
                yield return Until(() => pending == null && state.orders.Any(order =>
                    order.storeId == "jungnang-a" && order.driverId == MotorcycleRiderId), deadline);
                if (FailUnless(state.orders.Any(order =>
                        order.storeId == "jungnang-a" && order.driverId == MotorcycleRiderId),
                        $"{day}일차 A매장 배차에 실패했습니다."))
                    yield break;

                const string riderA = "management-driver-jungnang-a";
                if (day == 1)
                {
                    receipt.riderASelectedFromLight = SelectFromLight(riderA);
                    if (FailUnless(receipt.riderASelectedFromLight, "A 기사의 빛을 눌러 선택하지 못했습니다."))
                        yield break;
                }
                else
                    SelectCourier(riderA);
                ConnectStore("jungnang-b");
                yield return Until(() => pending == null && state.orders.Any(order =>
                    order.storeId == "jungnang-b" && order.driverId == riderA), deadline);
                if (FailUnless(state.orders.Any(order => order.storeId == "jungnang-b" && order.driverId == riderA),
                        $"{day}일차 B매장 배차에 실패했습니다."))
                    yield break;

                if (day == 1)
                {
                    yield return Until(() => RiderB() is { phase: "ToPickup", route: { Length: > 1 } }, deadline);
                    if (FailUnless(RiderB() is { phase: "ToPickup", route: { Length: > 1 } },
                            "B 기사의 픽업 경로가 시작되지 않았습니다."))
                        yield break;
                    SelectCourier(MotorcycleRiderId);
                    receipt.drivingCapacityEstimate = EstimateDailyDeliveries(BeforeDrivingMinutes) == (6, 8) &&
                        EstimateDailyDeliveries(AfterDrivingMinutes) == (9, 12) &&
                        EstimateDailyDeliveries(0) == (0, 0) && EstimateDailyDeliveries(90) == (4, 6);
                    receipt.riderSettledBefore = SettledCourierDeliveries(MotorcycleRiderId);
                    if (FailUnless(receipt.drivingCapacityEstimate && receipt.riderSettledBefore == 0 &&
                            IsFocused(FocusKind.Courier, MotorcycleRiderId),
                            "운행 전망 계산 또는 배달 전 실제 가상 정산 건수가 달랐습니다."))
                        yield break;
                    yield return SaveFrame(directory, "01-day1-moving.png");

                    SetCampaignSpeed(0);
                    yield return Until(() => campaignSpeed == 0 && !campaignPauseTick && pending == null, deadline);
                    if (FailUnless(campaignSpeed == 0 && !campaignPauseTick && pending == null && RiderB() is { phase: "ToPickup" },
                            "이동 중인 B 기사를 일시정지하지 못했습니다."))
                        yield break;
                    var stopped = RiderB();
                    receipt.pauseElapsedMs = state.elapsedMs;
                    yield return new WaitForSecondsRealtime(0.65f);
                    receipt.pauseStable = state.elapsedMs == receipt.pauseElapsedMs &&
                        RiderB() is { phase: "ToPickup" } still && still.latitude == stopped.latitude &&
                        still.longitude == stopped.longitude;
                    if (FailUnless(receipt.pauseStable, "정지 중 시계 또는 기사 위치가 변했습니다."))
                        yield break;
                    yield return SaveFrame(directory, "02-day1-panels.png");

                    receipt.storeMarkersClear = state.stores.All(store =>
                    {
                        var label = PlotOnBoard(store.latitude, store.longitude) + Field.position + new Vector2(0, -32);
                        return Field.Contains(label) && !MapOverlayCovers(label);
                    });
                    var deskPoint = new Vector2(36, 350);
                    var timePoint = new Vector2(36, 37);
                    var detailPoint = new Vector2(1195, 250);
                    receipt.mapFillsDefaultViewport = Field == new Rect(0, 0, Width, Height) &&
                        roadTexture.width == Width && roadTexture.height == Height;
                    receipt.mapExtendsAcrossAspectRatios =
                        MapViewport(1600, 720, 1f) == new Rect(-160, 0, 1600, 720) &&
                        MapViewport(1024, 768, 0.8f) == new Rect(0, -120, 1280, 960);
                    var coversShownPanels = MapOverlayCovers(deskPoint) && MapOverlayCovers(timePoint) &&
                        MapOverlayCovers(detailPoint);
                    ToggleTime();
                    var timeHidden = !MapOverlayCovers(timePoint) && MapOverlayCovers(deskPoint);
                    ToggleDetails();
                    var detailsHidden = !MapOverlayCovers(detailPoint) && IsFocused(FocusKind.Courier, MotorcycleRiderId);
                    ToggleDetails();
                    ToggleDesk();
                    var deskHidden = !MapOverlayCovers(deskPoint) && MapOverlayCovers(detailPoint);
                    ToggleDesk();
                    receipt.independentPanels = coversShownPanels && timeHidden && detailsHidden && deskHidden;
                    yield return SaveFrame(directory, "02a-time-panel-hidden.png");

                    var mapBefore = PlotOnBoard(stopped.latitude, stopped.longitude);
                    ToggleMapOnly();
                    receipt.mapOnlyUnblocksFormerPanels = mapOnly && !MapOverlayCovers(deskPoint) &&
                        !MapOverlayCovers(detailPoint) && MapOverlayCovers(new Vector2(644, 25));
                    yield return SaveFrame(directory, "02b-map-only.png");

                    var oldZoom = mapZoom;
                    var oldOffset = mapOffset;
                    var center = Field.size * 0.5f;
                    mapZoom = DongMinZoom;
                    mapOffset = ClampMapOffset(center - (center - oldOffset) * (mapZoom / oldZoom), mapZoom);
                    localTextureDirty = true;
                    receipt.compactLightSelection = SelectFromLight(riderA) && SelectFromLight(MotorcycleRiderId);
                    yield return SaveFrame(directory, "02bb-map-only-compact.png");
                    mapZoom = oldZoom;
                    mapOffset = oldOffset;
                    localTextureDirty = true;
                    ToggleMapOnly();
                    receipt.mapOnlyRestoredPriorPanels = !mapOnly && showDesk && !showTime && showDetails &&
                        IsFocused(FocusKind.Courier, MotorcycleRiderId) && state.elapsedMs == receipt.pauseElapsedMs &&
                        PlotOnBoard(stopped.latitude, stopped.longitude) == mapBefore;
                    yield return SaveFrame(directory, "02c-panels-restored.png");
                    ToggleTime();
                    if (FailUnless(receipt.mapFillsDefaultViewport && receipt.mapExtendsAcrossAspectRatios &&
                            receipt.independentPanels && receipt.mapOnlyUnblocksFormerPanels && receipt.mapOnlyRestoredPriorPanels && receipt.storeMarkersClear && receipt.compactLightSelection,
                            "지도 화면 크기 또는 패널 전환 뒤 지도 입력·선택 유지가 달랐습니다."))
                        yield break;

                    SetCampaignSpeed(1);
                    var normalStart = state.elapsedMs;
                    yield return new WaitForSecondsRealtime(0.65f);
                    receipt.normalAdvanceMs = state.elapsedMs - normalStart;
                    SetCampaignSpeed(2);
                    var doubleStart = state.elapsedMs;
                    yield return new WaitForSecondsRealtime(0.65f);
                    receipt.doubleAdvanceMs = state.elapsedMs - doubleStart;
                    SetCampaignSpeed(4);
                    var fastStart = state.elapsedMs;
                    yield return new WaitForSecondsRealtime(0.65f);
                    receipt.fastAdvanceMs = state.elapsedMs - fastStart;
                    receipt.resumedRoute = RiderB() is { } resumed &&
                        (resumed.latitude != stopped.latitude || resumed.longitude != stopped.longitude) &&
                        state.orders.Any(order => order.storeId == "jungnang-a" && order.driverId == MotorcycleRiderId);
                    if (FailUnless(receipt.normalAdvanceMs > 0 && receipt.doubleAdvanceMs > receipt.normalAdvanceMs &&
                            receipt.fastAdvanceMs > receipt.doubleAdvanceMs &&
                            receipt.resumedRoute && state.campaign.day == 1,
                            "배속 변경 후 시계와 B 기사의 이동이 이어지지 않았습니다."))
                        yield break;
                }

                yield return Until(() => state.settled && state.campaign.canFastForward && pending == null, deadline);
                if (FailUnless(state.settled && state.campaign.canFastForward && pending == null &&
                        state.settlement?.balance == 3600 && state.settlement.onTimeCount == 2,
                        $"{day}일차 두 주문의 가상 정산 결과가 달랐습니다."))
                    yield break;

                if (day == 1)
                {
                    SelectCourier(MotorcycleRiderId);
                    receipt.riderSettledAfter = SettledCourierDeliveries(MotorcycleRiderId);
                    if (FailUnless(receipt.riderSettledAfter == 1, "기사 B의 실제 가상 정산 건수가 달랐습니다."))
                        yield break;
                    yield return SaveFrame(directory, "03a-rider-driving-capacity-settled.png");
                }

                var closeAt = state.campaign.nextBoundaryMs;
                AdvanceCampaignBoundary();
                var expectedPhase = day == 3 ? "policy" : "closed";
                yield return Until(() => state.campaign.phase == expectedPhase && pending == null, deadline);
                var summary = state.campaign.latestSummary;
                if (FailUnless(state.campaign.phase == expectedPhase && pending == null &&
                        state.elapsedMs >= closeAt && summary?.day == currentDay && summary.completed == 2 &&
                        summary.totalOrders == 2 && summary.waiting == 0 && summary.delivering == 0 && summary.balance == 3600,
                        $"{day}일차 운영 마감에 두 주문이 반영되지 않았습니다."))
                    yield break;
                receipt.closedDays.Add(day);
                receipt.closeTimesMs.Add(closeAt);
                if (day == 1)
                    yield return SaveFrame(directory, "03-day1-close.png");

                if (day < 3)
                {
                    yield return Until(() => state.campaign.canFastForward && pending == null, deadline);
                    var nextOpen = state.campaign.nextBoundaryMs;
                    AdvanceCampaignBoundary();
                    yield return Until(() => state.campaign.day == currentDay + 1 && pending == null, deadline);
                    if (FailUnless(state.campaign.day == currentDay + 1 && pending == null &&
                            state.elapsedMs >= nextOpen && state.elapsedMs > closeAt && state.campaign.phase == "open",
                            $"{day + 1}일차가 이어지는 시각에서 열리지 않았습니다."))
                        yield break;
                }
            }

            var policyGateTime = state.elapsedMs;
            receipt.policyGateMs = policyGateTime;
            yield return new WaitForSecondsRealtime(0.65f);
            receipt.policyGateStable = state.campaign.phase == "policy" && state.elapsedMs == policyGateTime &&
                state.now == "18:00:00";
            if (FailUnless(receipt.policyGateStable, "3일차 18:00의 방침 선택에서 시계가 멈추지 않았습니다."))
                yield break;
            var expectedArt = new[]
            {
                (id: "chariot", width: 838, height: 1441),
                (id: "temperance", width: 826, height: 1433),
                (id: "empress", width: 860, height: 1455),
            };
            var distinctNotes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, width, height) in expectedArt)
            {
                var card = campaignPolicyCards.SingleOrDefault(candidate => candidate.Id == id);
                var art = card?.Art;
                var cardProof = new CampaignArtProof
                {
                    id = id,
                    width = art?.width ?? 0,
                    height = art?.height ?? 0,
                    hasReading = !string.IsNullOrWhiteSpace(card?.Reading),
                    hasReflection = !string.IsNullOrWhiteSpace(card?.Reflection),
                    hasLearningNote = !string.IsNullOrWhiteSpace(card?.LearningNote),
                };
                receipt.cardArt.Add(cardProof);
                if (FailUnless(art != null && art.width == width && art.height == height,
                        $"운영 방침 카드 {id}의 원본 해상도 그림을 읽지 못했습니다."))
                    yield break;
                if (FailUnless(cardProof.hasReading && cardProof.hasReflection &&
                        distinctNotes.Add(card.Reading.Trim()) && distinctNotes.Add(card.Reflection.Trim()) &&
                        cardProof.hasLearningNote == (id == "chariot"),
                        $"운영 방침 카드 {id}의 고유 해설, 질문 또는 학습 메모 범위가 맞지 않습니다."))
                    yield break;
            }
            receipt.distinctCardNotes = true;

            var temperance = campaignPolicyCards.Single(card => card.Id == "temperance");
            selectedCampaignPolicy = temperance;
            yield return SaveFrame(directory, "04-day3-policy.png");

            bool ArtPreviewAtGate(CampaignPolicyCard card, bool open) => showCampaignArtPreview == open &&
                state.campaign.phase == "policy" && state.elapsedMs == policyGateTime &&
                string.IsNullOrEmpty(state.campaign.policy) && selectedCampaignPolicy == card;
            var previews = new[]
            {
                (id: "temperance", file: "04b-day3-policy-art.png"),
                (id: "chariot", file: "04c-day3-chariot-notes.png"),
                (id: "empress", file: "04d-day3-empress-notes.png"),
            };
            foreach (var (id, file) in previews)
            {
                var card = campaignPolicyCards.Single(candidate => candidate.Id == id);
                var cardProof = receipt.cardArt.Single(candidate => candidate.id == id);
                selectedCampaignPolicy = card;
                ToggleCampaignArtPreview();
                yield return SaveFrame(directory, file);
                cardProof.previewOpened = ArtPreviewAtGate(card, true);
                if (FailUnless(cardProof.previewOpened, $"카드 {id} 확대 중 선택과 시계 정지 상태가 유지되지 않았습니다."))
                    yield break;

                ToggleCampaignArtPreview();
                yield return null;
                cardProof.previewRestoredSelection = ArtPreviewAtGate(card, false);
                if (FailUnless(cardProof.previewRestoredSelection, $"확대 닫기 뒤 카드 {id} 선택과 시각으로 돌아오지 않았습니다."))
                    yield break;
            }

            receipt.artPreviewOpened = true;
            receipt.artPreviewRestoredSelection = true;
            selectedCampaignPolicy = temperance;
            ApplyCampaignPolicy(temperance.Id);
            yield return Until(() => state.campaign.policy == "temperance", deadline);
            receipt.policyAcceptedAtGate = state.campaign.policy == "temperance" &&
                state.campaign.phase == "closed" && state.elapsedMs == policyGateTime;
            if (FailUnless(receipt.policyAcceptedAtGate, "절제를 선택할 때 기존 시각과 마감 상태가 이어지지 않았습니다."))
                yield break;

            yield return Until(() => state.elapsedMs > policyGateTime && state.campaign.canFastForward && pending == null,
                deadline);
            if (FailUnless(state.elapsedMs > policyGateTime && state.campaign.canFastForward && pending == null,
                    "방침 선택 후 시계가 다시 움직이지 않았습니다."))
                yield break;

            var fourthOpen = state.campaign.nextBoundaryMs;
            AdvanceCampaignBoundary();
            yield return Until(() => state.campaign.day == 4 && pending == null, deadline);
            receipt.day4ElapsedMs = state.elapsedMs;
            receipt.day4OrderCount = state.orders.Length;
            receipt.policy = state.campaign.policy;
            receipt.day4PolicyEffect = state.campaign.day == 4 && state.campaign.phase == "open" &&
                state.elapsedMs >= fourthOpen && state.campaign.policy == "temperance" &&
                state.orders.Length == 1 && state.orders[0].storeId == "jungnang-a" && orderIds.Add(state.orders[0].id);
            if (FailUnless(receipt.day4PolicyEffect, "4일차에 절제의 신규 주문 한 건 제한이 적용되지 않았습니다."))
                yield break;

            yield return SaveFrame(directory, "05-day4-temperance.png");
            receipt.passed = true;
            FinishCampaignProof(directory, receipt);
        }

        private static void FinishCampaignProof(string directory, CampaignProofReceipt receipt)
        {
            File.WriteAllText(Path.Combine(directory, "unity-campaign-proof.json"), JsonUtility.ToJson(receipt, true));
            Application.Quit(receipt.passed && receipt.failure == null ? 0 : 2);
        }

        [Serializable]
        private sealed class CampaignProofReceipt
        {
            public string input = "Unity Windows Player에서 UI와 같은 시작, 배차, 배속, 다음 시각 이동, 카드 확대와 운영 방침 처리기 호출";
            public bool passed;
            public string failure;
            public long pauseElapsedMs;
            public bool pauseStable;
            public long normalAdvanceMs;
            public long doubleAdvanceMs;
            public long fastAdvanceMs;
            public bool resumedRoute;
            public bool drivingCapacityEstimate;
            public bool mapFillsDefaultViewport;
            public bool mapExtendsAcrossAspectRatios;
            public bool independentPanels;
            public bool storeMarkersClear;
            public bool distinctCourierLights;
            public bool riderASelectedFromLight;
            public bool riderBSelectedFromLight;
            public bool compactLightSelection;
            public bool mapOnlyUnblocksFormerPanels;
            public bool mapOnlyRestoredPriorPanels;
            public int riderSettledBefore;
            public int riderSettledAfter;
            public List<int> closedDays = new();
            public List<long> closeTimesMs = new();
            public long policyGateMs;
            public bool policyGateStable;
            public List<CampaignArtProof> cardArt = new();
            public bool distinctCardNotes;
            public bool artPreviewOpened;
            public bool artPreviewRestoredSelection;
            public bool policyAcceptedAtGate;
            public long day4ElapsedMs;
            public int day4OrderCount;
            public string policy;
            public bool day4PolicyEffect;
        }

        [Serializable]
        private sealed class CampaignArtProof
        {
            public string id;
            public int width;
            public int height;
            public bool hasReading;
            public bool hasReflection;
            public bool hasLearningNote;
            public bool previewOpened;
            public bool previewRestoredSelection;
        }
    }
}
