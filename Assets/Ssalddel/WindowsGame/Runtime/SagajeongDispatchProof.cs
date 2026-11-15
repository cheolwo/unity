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
        private IEnumerator CaptureProof(string directory, bool captureMotorcycle = false)
        {
            var proof = new ProofReceipt();
            expansionView = false;
            showDesk = true;
            showTime = true;
            Directory.CreateDirectory(directory);
            var deadline = Time.realtimeSinceStartupAsDouble + 100;
            yield return Until(() => state != null && roadTexture != null, deadline);
            if (state == null || roadTexture == null || error != null)
            {
                FinishProof(directory, proof, error ?? "초기 상태를 표시하지 못했습니다.");
                yield break;
            }

            proof.initial = state;
            yield return SaveFrame(directory, "01-start.png");

            holdProofClock = true;
            BeginRound();
            const string riderA = "management-driver-jungnang-a";
            const string riderB = MotorcycleRiderId;
            const string storeA = "jungnang-a";
            const string storeB = "jungnang-b";
            SelectCourier(riderB);
            yield return SaveFrame(directory, "02-select-rider.png");
            ConnectStore(storeA);
            yield return Until(() => pending == null, deadline);
            SelectCourier(riderA);
            ConnectStore(storeB);
            yield return Until(() => pending == null, deadline);
            proof.assigned = state;
            proof.assignmentsMatch = state.orders.Any(order => order.storeId == storeA && order.driverId == riderB) &&
                state.orders.Any(order => order.storeId == storeB && order.driverId == riderA);
            if (!proof.assignmentsMatch)
            {
                FinishProof(directory, proof, error ?? notice ?? "기사 지정 결과가 달랐습니다.");
                yield break;
            }
            holdProofClock = false;
            realStart = Time.realtimeSinceStartupAsDouble;
            Coroutine motorcycleCapture = null;
            if (captureMotorcycle)
            {
                SelectCourier(riderB);
                proof.motorcycle = new MotorcycleReceipt();
                courierLightProbe = (courier, _, colour) =>
                {
                    if (courier.id == riderB && colour == Lilac) proof.motorcycle.lightDrawn = true;
                };
                motorcycleCapture = StartCoroutine(CaptureMotorcycle(directory, proof.motorcycle, deadline));
            }

            bool OnRoad() => state.couriers.Any(courier => courier.phase == "ToPickup" && courier.route?.Length > 1);
            yield return Until(OnRoad, deadline);
            if (!OnRoad())
            {
                FinishProof(directory, proof, error ?? "픽업 이동을 표시하지 못했습니다.");
                yield break;
            }
            proof.onRoad = state;
            yield return SaveFrame(directory, "03-on-road.png");
            var untilMovement = Time.realtimeSinceStartupAsDouble + 1.5;
            yield return Until(() => state.couriers.Any(courier => courier.phase == "ToPickup" &&
                proof.onRoad.couriers.Any(previous => previous.id == courier.id && previous.phase == "ToPickup" &&
                    (previous.latitude != courier.latitude || previous.longitude != courier.longitude))), untilMovement);
            proof.pickupMovementObserved = state.couriers.Any(courier => courier.phase == "ToPickup" &&
                proof.onRoad.couriers.Any(previous => previous.id == courier.id && previous.phase == "ToPickup" &&
                    (previous.latitude != courier.latitude || previous.longitude != courier.longitude)));
            bool Delivering() => state.couriers.Any(courier => courier.phase == "ToDropoff" && courier.route?.Length > 1);
            yield return Until(Delivering, deadline);
            if (!Delivering())
            {
                FinishProof(directory, proof, error ?? "배송 이동을 표시하지 못했습니다.");
                yield break;
            }
            proof.delivering = state;
            yield return SaveFrame(directory, "04-delivery.png");
            yield return Until(() => state.settled, deadline);
            if (!state.settled)
            {
                FinishProof(directory, proof, error ?? "가상 정산에 도달하지 못했습니다.");
                yield break;
            }
            if (motorcycleCapture != null) yield return motorcycleCapture;
            proof.final = state;
            proof.passed = proof.pickupMovementObserved && state.settlement?.cleared == true && state.settlement.onTimeCount == 2 &&
                state.settlement.revenue == 9600 && state.settlement.driverPay == 7200 &&
                state.settlement.baseBalance == 2400 && state.settlement.bonus == 1200 && state.settlement.balance == 3600;
            if (proof.motorcycle != null)
                proof.passed &= proof.motorcycle.passed;
            yield return SaveFrame(directory, "05-settled.png");
            FinishProof(directory, proof, proof.passed ? null : "이동, 픽업 정지, 방향 회전 또는 가상 정산 검증에 실패했습니다.");
        }

        private IEnumerator CaptureMotorcycle(string directory, MotorcycleReceipt receipt, double deadline)
        {
            var frames = Path.Combine(directory, "motion");
            Directory.CreateDirectory(frames);
            var previousPhase = "";
            MotorcycleFrame firstOnLeg = null;
            MotorcycleFrame firstAtPickup = null;
            var movement = new HashSet<string>();
            var turn = new HashSet<string>();
            var nextCapture = Time.realtimeSinceStartupAsDouble;

            while (error == null && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return new WaitForEndOfFrame();
                var rider = state.couriers.First(courier => courier.id == MotorcycleRiderId);
                var sample = new MotorcycleFrame
                {
                    seconds = Time.realtimeSinceStartupAsDouble - realStart,
                    phase = rider.phase,
                    latitude = rider.latitude,
                    longitude = rider.longitude,
                    heading = MotorcycleHeading(rider.id)
                };
                if (sample.phase != previousPhase)
                {
                    receipt.phases.Add(sample.phase);
                    previousPhase = sample.phase;
                    firstOnLeg = sample;
                }
                if (sample.phase == "AtPickup")
                {
                    firstAtPickup ??= sample;
                    receipt.pickupFrames++;
                    receipt.pickupStationary &= SamePosition(firstAtPickup, sample);
                }
                if (sample.phase is "ToPickup" or "ToDropoff" && !SamePosition(firstOnLeg, sample))
                {
                    movement.Add(sample.phase);
                    if (Mathf.Abs(Mathf.DeltaAngle(firstOnLeg.heading, sample.heading)) >= 25)
                        turn.Add(sample.phase);
                }

                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    var index = receipt.frames.Count;
                    File.WriteAllBytes(Path.Combine(frames, $"frame-{index:D4}.jpg"), frame.EncodeToJPG(89));
                    if (sample == firstAtPickup)
                        File.WriteAllBytes(Path.Combine(directory, "06-motorcycle-pickup.png"), frame.EncodeToPNG());
                    if (!receipt.turnCaptured && turn.Contains("ToPickup"))
                    {
                        File.WriteAllBytes(Path.Combine(directory, "07-motorcycle-turn.png"), frame.EncodeToPNG());
                        receipt.turnCaptured = true;
                    }
                    if (!receipt.deliveryCaptured && movement.Contains("ToDropoff"))
                    {
                        File.WriteAllBytes(Path.Combine(directory, "08-motorcycle-delivery.png"), frame.EncodeToPNG());
                        receipt.deliveryCaptured = true;
                    }
                }
                finally
                {
                    Destroy(frame);
                }
                receipt.frames.Add(sample);
                if (state.settled) break;

                nextCapture += 0.10;
                while (Time.realtimeSinceStartupAsDouble < nextCapture) yield return null;
            }

            courierLightProbe = null;
            receipt.passed = receipt.lightDrawn && receipt.pickupFrames > 1 && receipt.pickupStationary &&
                movement.Contains("ToPickup") && movement.Contains("ToDropoff") && receipt.turnCaptured &&
                receipt.deliveryCaptured && state.settled;
        }

        private static bool SamePosition(MotorcycleFrame a, MotorcycleFrame b) =>
            a.latitude == b.latitude && a.longitude == b.longitude;

        private IEnumerator Until(Func<bool> predicate, double deadline)
        {
            while (error == null && !predicate() && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
        }

        private IEnumerator SaveFrame(string directory, string file)
        {
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(directory, file), frame.EncodeToPNG());
            }
            finally
            {
                Destroy(frame);
            }
        }

        private static void FinishProof(string directory, ProofReceipt proof, string failure)
        {
            proof.failure = failure;
            File.WriteAllText(Path.Combine(directory, "unity-player-proof.json"), JsonUtility.ToJson(proof, true));
            Application.Quit(proof.passed && failure == null ? 0 : 2);
        }

        [Serializable]
        private sealed class ProofReceipt
        {
            public string input = "Unity Player scripted invocation of the same Start, SelectCourier and ConnectStore handlers used by marker clicks";
            public bool passed;
            public bool assignmentsMatch;
            public bool pickupMovementObserved;
            public DispatchState initial;
            public DispatchState assigned;
            public DispatchState onRoad;
            public DispatchState delivering;
            public DispatchState final;
            public MotorcycleReceipt motorcycle;
            public string failure;
        }

        [Serializable]
        private sealed class MotorcycleReceipt
        {
            public bool passed;
            public bool spriteLoaded = false;   // 이전 촬영의 JSON 필드
            public bool lightDrawn;
            public bool pickupStationary = true;
            public int pickupFrames;
            public bool turnCaptured;
            public bool deliveryCaptured;
            public List<string> phases = new();
            public List<MotorcycleFrame> frames = new();
        }

        [Serializable]
        private sealed class MotorcycleFrame
        {
            public double seconds;
            public string phase;
            public double latitude;
            public double longitude;
            public float heading;
        }
    }
}
