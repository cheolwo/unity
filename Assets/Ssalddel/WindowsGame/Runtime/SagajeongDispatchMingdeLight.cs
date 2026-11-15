using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 명1 명덕 → 빛의 연결 자리 (2026-11-15, 사용자: 「크기도 … 세기도 … 둘 다 그냥 우상향」). 규칙은 명덕빛Policy.cs.
    //  - 기사 빛(DrawCourierGlow)과 매장 빛(DrawStoreLight)이 대상의 쌓인 명덕만큼 크고 밝아진다.
    //  - 커널에는 아직 명덕이 없다(쌓는 규칙 · 귀속 · 저장 계약은 r204 후속, Codex 기획). 그래서 게임에서는 늘 「자료 준비 전」이고
    //    빛은 지금과 똑같다. 값이 생기면 MingdeOf 한 곳에서 읽는다.
    //  - 방향 촬영만 시험값을 넣어 모양을 본다(장면 90). 시험값은 업무 상태 · 저장과 잇지 않는다.
    public sealed partial class SagajeongDispatchView
    {
        private readonly Dictionary<string, double> mingdeProof = new(StringComparer.Ordinal);    // 촬영의 시험값만

        /// <summary>대상의 쌓인 명덕. 없으면 null(자료 준비 전). kind는 "courier" · "store".</summary>
        private double? MingdeOf(string kind, string id) =>
            mingdeProof.TryGetValue(kind + ":" + id, out var value) ? (double?)value : null;

        private 명덕빛 MingdeGlow(string kind, string id) => 명덕빛Policy.Of(MingdeOf(kind, id));

        // 방향 촬영: 기사 둘 · 매장 둘에 서로 다른 시험 명덕을 넣고, 같은 화면에서 크기와 세기가 함께 커지는지 본다.
        private IEnumerator ViewProofMingde(string directory, ViewProofReceipt receipt)
        {
            holdProofClock = true;
            settingsOpen = false;
            dailyReportOpen = false;
            actorFocus = null;
            receipt.mingdeDefaultUnbound = state?.couriers != null && state.couriers.All(courier => !MingdeGlow("courier", courier.id).Bound);
            var couriers = state?.couriers ?? Array.Empty<DispatchCourier>();
            var stores = state?.stores ?? Array.Empty<DispatchStore>();
            var values = new[] { 0.0, 240.0 };
            for (var index = 0; index < couriers.Length && index < values.Length; index++) mingdeProof["courier:" + couriers[index].id] = values[index];
            for (var index = 0; index < stores.Length && index < values.Length; index++) mingdeProof["store:" + stores[index].id] = values[index];
            receipt.mingdeLights = mingdeProof.Count;
            var low = 명덕빛Policy.Of(values[0]);
            var high = 명덕빛Policy.Of(values[1]);
            receipt.mingdeLowSize = low.Size;
            receipt.mingdeLowStrength = low.Strength;
            receipt.mingdeHighSize = high.Size;
            receipt.mingdeHighStrength = high.Strength;
            receipt.mingdeRises = high.Size > low.Size && high.Strength > low.Strength;
            if (couriers.Length > 1)
            {
                var a = couriers[0];
                var b = couriers[1];
                ViewProofCentre((a.latitude + b.latitude) / 2, (a.longitude + b.longitude) / 2, 2.6f);
            }
            for (var frame = 0; frame < 4; frame++) yield return null;
            yield return SaveFrame(directory, "90-mingde-light.png");
            mingdeProof.Clear();
            yield return null;
            receipt.mingdeClearedUnbound = state?.couriers != null && state.couriers.All(courier => !MingdeGlow("courier", courier.id).Bound);
            holdProofClock = false;
        }
    }
}
