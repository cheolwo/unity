using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 배달 판 · 구 지도에서 기사가 칸에서 칸으로 달리는 모습 (2026-11-27 19:02 사용자: 「주소를 특정할 수는 없으니까 육각형 타일이 있으니까
    //  픽업지랑 전달지를 선으로 잇는다고 할 때 그 타일을 가지고 이어서 기사님들이 이동하는 거를 표현 … 구 단위 지도 안에서는」).
    //  - 주소 대신 칸: 픽업 칸(가게가 많은 칸일수록 잘 뽑힘) → 전달 칸. 길은 이웃 칸의 가운데를 하나씩 밟아 간다.
    //  - 내 지사의 기사 수만큼(동마다 · 많으면 30명까지) 민트 점이 달린다. 기사 자리의 「나」는 고른 칸에서 그 턴의 콜을 차례로 나른다.
    //  - 픽업 칸은 금빛 테, 전달 칸은 민트 테로 잠깐 빛난다. 칸 · 빠르기 · 차례는 표현이다(실제 주소 · 경로 아님).
    public sealed partial class SagajeongDispatchView
    {
        private sealed class GuRun
        {
            public readonly List<int> Path = new List<int>();
            public int Pickup = -1, Drop = -1;
            public float At;                      // 경로 위 자리(칸 번호 + 사이)
            public float Speed;                   // 초당 칸
            public float Wait;                    // 픽업 칸에서 잠깐 멈춤
            public bool Me;
            public string Dong;
        }

        private const int GuRunLimit = 30;
        private readonly List<GuRun> guRuns = new List<GuRun>();
        private readonly Queue<GuRun> guMyRuns = new Queue<GuRun>();
        private GuRun guMyRun;
        private readonly List<(int Cell, float At, bool Drop)> guRunFlashes = new List<(int, float, bool)>();
        private System.Random guRunRandom;
        private float guRunFrameAt;
        private int guRunRound = -1;

        private float GuRunStep => chRadius * 1.7320508f;      // 이웃 칸 가운데 사이

        // 이웃 칸을 하나씩 밟아 a에서 b까지(양 끝 포함)
        private List<int> GuRunHexPath(int a, int b)
        {
            var path = new List<int> { a };
            var cur = a;
            for (var guard = 0; guard < 40 && cur != b; guard++)
            {
                var here = new Vector2(chX[cur], chY[cur]);
                var there = new Vector2(chX[b], chY[b]);
                var to = there - here;
                if (to.magnitude <= GuRunStep * 0.6f) { path.Add(b); break; }
                var dir = to.normalized;
                var next = -1;
                foreach (var turn in new[] { 0f, 30f, -30f, 60f, -60f })
                {
                    var d = (Vector2)(Quaternion.Euler(0f, 0f, turn) * dir);
                    var candidate = CapitalHexAt(here + d * GuRunStep);
                    if (candidate >= 0 && candidate != cur && !path.Contains(candidate)) { next = candidate; break; }
                }
                if (next < 0) { path.Add(b); break; }
                path.Add(next);
                cur = next;
            }
            return path;
        }

        // 둘레에서 한 칸 뽑기(주문이 짙은 칸일수록 잘 뽑힘). 16동 밖은 뽑지 않는다.
        private int GuRunPick(int around, float near, float far, bool heavy)
        {
            var from = new Vector2(chX[around], chY[around]);
            for (var guard = 0; guard < 40; guard++)
            {
                var angle = (float)guRunRandom.NextDouble() * Mathf.PI * 2f;
                var reach = GuRunStep * (near + (float)guRunRandom.NextDouble() * (far - near));
                var cell = CapitalHexAt(from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach);
                if (cell < 0 || cell == around || BranchSlotOfCell(cell) < 0) continue;
                if (heavy && RiderSeatHeat(cell) < (float)guRunRandom.NextDouble() * 0.7f) continue;
                return cell;
            }
            return around;
        }

        private GuRun GuRunMake(int start, bool me, int dropHint = -1)
        {
            var pickup = me ? GuRunPick(start, 0f, 1.2f, true) : GuRunPick(start, 0.5f, 2.5f, true);
            var drop = dropHint >= 0 ? dropHint : GuRunPick(pickup, 2f, 5f, false);
            var run = new GuRun { Pickup = pickup, Drop = drop, Me = me, Speed = (me ? 3.2f : 2.0f) + (float)guRunRandom.NextDouble() * 0.9f, Wait = 0.5f };
            run.Path.AddRange(GuRunHexPath(start, pickup));
            var second = GuRunHexPath(pickup, drop);
            run.Path.AddRange(second.Skip(1));
            if (run.Path.Count < 2) run.Path.Add(drop);
            return run;
        }

        // 내 지사의 기사 수가 바뀌면(회차) 다시 놓는다.
        private void SyncGuRuns()
        {
            guRunRandom ??= new System.Random(1127);
            if (branchBoard == null || guRunRound == branchBoard.회차) return;
            guRunRound = branchBoard.회차;
            EnsureRiderSeatCells();
            var want = new List<(int Cell, string Dong)>();
            foreach (var seat in branchBoard.운영자자리(BranchMe))
            {
                var slot = BranchSlotOfCode(seat.동);
                if (slot < 0 || branchCell[slot] < 0) continue;
                for (var k = 0; k < seat.기사; k++) want.Add((branchCell[slot], seat.동));
            }
            while (want.Count > GuRunLimit) want.RemoveAt(guRunRandom.Next(want.Count));
            // 이미 달리는 기사는 그대로 두고 수만 맞춘다
            while (guRuns.Count > want.Count) guRuns.RemoveAt(guRuns.Count - 1);
            for (var k = guRuns.Count; k < want.Count; k++)
            {
                var start = GuRunPick(want[k].Cell, 0f, 2f, true);
                var run = GuRunMake(start, false);
                run.Dong = want[k].Dong;
                run.At = (float)guRunRandom.NextDouble() * (run.Path.Count - 1);
                guRuns.Add(run);
            }
        }

        // 기사 자리: 그 턴의 콜을 고른 칸에서 차례로 나른다.
        private void QueueMyGuRuns(int wait, List<int> drops)
        {
            guRunRandom ??= new System.Random(1127);
            guMyRuns.Clear();
            guMyRun = null;
            var from = wait;
            foreach (var drop in drops)
            {
                var run = GuRunMake(from, true, drop);
                guMyRuns.Enqueue(run);
                var back = new GuRun { Me = true, Speed = 4.2f, Pickup = -1, Drop = -1 };
                back.Path.AddRange(GuRunHexPath(drop, wait));
                if (back.Path.Count >= 2) guMyRuns.Enqueue(back);
            }
        }

        private bool StepGuRun(GuRun run, float delta, float now)
        {
            if (run.Path.Count < 2) return true;
            var pickupAt = run.Pickup >= 0 ? run.Path.IndexOf(run.Pickup) : -1;
            var before = run.At;
            if (pickupAt >= 0 && before < pickupAt && before + run.Speed * delta >= pickupAt && run.Wait > 0f)
            {
                run.At = pickupAt;
                run.Wait -= delta;
                if (run.Wait <= 0f || run.Wait > 0.49f) guRunFlashes.Add((run.Pickup, now, false));
                return false;
            }
            run.At = Mathf.Min(run.Path.Count - 1, run.At + run.Speed * delta);
            if (run.At >= run.Path.Count - 1)
            {
                if (run.Drop >= 0) guRunFlashes.Add((run.Drop, now, true));
                return true;
            }
            return false;
        }

        private Vector2 GuRunPoint(GuRun run)
        {
            var i = Mathf.Clamp(Mathf.FloorToInt(run.At), 0, run.Path.Count - 2);
            var t = Mathf.Clamp01(run.At - i);
            var a = GuPoint(chX[run.Path[i]], chY[run.Path[i]]);
            var b = GuPoint(chX[run.Path[i + 1]], chY[run.Path[i + 1]]);
            return Vector2.Lerp(a, b, t);
        }

        private void DrawGuRunPath(GuRun run, Color tint, float alpha)
        {
            var from = Mathf.Max(0, Mathf.FloorToInt(run.At));
            for (var i = from; i < run.Path.Count - 1; i++)
            {
                var a = GuPoint(chX[run.Path[i]], chY[run.Path[i]]);
                var b = GuPoint(chX[run.Path[i + 1]], chY[run.Path[i + 1]]);
                var steps = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 6f));
                for (var s = 0; s < steps; s++)
                {
                    var p = Vector2.Lerp(a, b, s / (float)steps);
                    Paint(new Rect(p.x - 1.1f, p.y - 1.1f, 2.2f, 2.2f), Fade(tint, alpha), 1.1f);
                }
                Paint(new Rect(b.x - 2f, b.y - 2f, 4f, 4f), Fade(tint, alpha * 1.3f), 2f);      // 밟는 칸 가운데
            }
        }

        // 구 지도 위(DrawBranchMapMarks · DrawRiderSeatMarks가 부른다). 그린 「나」의 자리를 돌려준다(없으면 null).
        private Vector2? DrawGuRuns()
        {
            if (branchBoard == null || Event.current.type != EventType.Repaint) return null;
            SyncGuRuns();
            var now = Time.unscaledTime;
            var delta = guRunFrameAt <= 0f ? 0f : Mathf.Min(0.1f, now - guRunFrameAt);
            guRunFrameAt = now;
            var gold = Ink(0xFFC46B);
            guRunFlashes.RemoveAll(item => now - item.At > 1.4f);
            foreach (var flash in guRunFlashes)
            {
                var fade = 1f - (now - flash.At) / 1.4f;
                DrawCapitalHexRing(flash.Cell, Fade(flash.Drop ? Mint : gold, 0.9f * fade), 1.5f);
            }
            for (var k = 0; k < guRuns.Count; k++)
            {
                var run = guRuns[k];
                if (StepGuRun(run, delta, now))
                {
                    var next = GuRunMake(run.Path[run.Path.Count - 1], false);
                    next.Dong = run.Dong;
                    guRuns[k] = run = next;
                }
                var at = GuRunPoint(run);
                if (at.x < -20f || at.y < -20f || at.x > Field.width + 20f || at.y > Field.height + 20f) continue;
                BranchGlow(at, 7f, Fade(Mint, 0.22f));
                BranchGlow(at, 3.2f, Fade(Mint, 0.95f));
            }
            if (guMyRun == null && guMyRuns.Count > 0) guMyRun = guMyRuns.Dequeue();
            if (guMyRun == null) return null;
            DrawGuRunPath(guMyRun, guMyRun.Drop >= 0 ? Mint : Soft, 0.75f);
            if (guMyRun.Pickup >= 0 && guMyRun.At < guMyRun.Path.IndexOf(guMyRun.Pickup)) DrawCapitalHexRing(guMyRun.Pickup, Fade(gold, 0.9f), 1f);
            if (guMyRun.Drop >= 0) DrawCapitalHexRing(guMyRun.Drop, Fade(Mint, 0.85f), 1f);
            var mine = GuRunPoint(guMyRun);
            if (StepGuRun(guMyRun, delta, now)) guMyRun = null;
            return mine;
        }
    }
}
