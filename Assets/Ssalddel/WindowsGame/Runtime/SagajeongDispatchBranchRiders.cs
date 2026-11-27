using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 지사장 판 · 동 디오라마의 기사 빛 (2026-11-27 17:08 사용자: 「디오라마로 내려가면 지사장 입장에서 보는 거니까
    //  기사님들이 열심히 배달을 하는 모습이 빛으로서 표현이 되면 … 시각적으로 느낄 수 있도록」).
    //  - 지사장 판에서 연 공개판 동 디오라마에만 뜬다. 내 지사의 기사 수만큼 민트빛, 경쟁 지사의 기사는 옅은 금빛.
    //  - 길: 디오라마 메시의 차도 면(높이 0.06 m)의 꼭짓점과 삼각형 변을 따라 달린다(길 자료를 따로 싣지 않는다).
    //  - 빛은 꼬리를 끌고, 배달을 마친 자리에서는 빛이 한 번 번진다. 움직임 · 빠르기 · 마친 자리는 표현이다(실제 경로 아님).
    public sealed partial class SagajeongDispatchView
    {
        private sealed class BranchRider
        {
            public int From, To;
            public float T, Speed, NextDrop, FlashAt = -10f, TrailAt;
            public bool Mine;
            public Vector3 Heading, FlashPoint;
            public readonly Vector3[] Trail = new Vector3[26];
            public int TrailCount;
        }

        private const float BranchRoadHeight = 0.06f;
        private Vector3[] brNodes;
        private int[][] brLinks;
        private readonly List<BranchRider> branchRiders = new List<BranchRider>();
        private System.Random brRandom;
        private float brFrameAt;
        private int branchRiderMine, branchRiderRival;

        private void ClearBranchRiders()
        {
            branchRiders.Clear();
            brNodes = null;
            brLinks = null;
            branchRiderMine = branchRiderRival = 0;
        }

        // OpenPublicDong이 메시를 읽은 뒤 부른다.
        private void BuildBranchRiders(Mesh mesh, string code)
        {
            ClearBranchRiders();
            if (!BranchStance || branchBoard == null || mesh == null || BranchSlotOfCode(code) < 0) return;
            foreach (var seat in branchBoard.동자리(code))
            {
                if (seat.운영자 == BranchMe) branchRiderMine += seat.기사;
                else branchRiderRival += seat.기사;
            }
            if (branchRiderMine + branchRiderRival == 0) return;

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var map = new int[vertices.Length];
            var index = new Dictionary<long, int>();
            var nodes = new List<Vector3>();
            for (var i = 0; i < vertices.Length; i++)
            {
                map[i] = -1;
                if (Mathf.Abs(vertices[i].y - BranchRoadHeight) > 0.025f) continue;
                var key = ((long)Mathf.RoundToInt(vertices[i].x * 4f) << 32) ^ (uint)Mathf.RoundToInt(vertices[i].z * 4f);
                int node;
                if (!index.TryGetValue(key, out node))
                {
                    node = nodes.Count;
                    nodes.Add(vertices[i]);
                    index[key] = node;
                }
                map[i] = node;
            }
            if (nodes.Count < 40) { ClearBranchRiders(); return; }
            var links = new HashSet<int>[nodes.Count];
            for (var t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = map[triangles[t]], b = map[triangles[t + 1]], c = map[triangles[t + 2]];
                if (a < 0 || b < 0 || c < 0) continue;
                BranchLink(links, a, b);
                BranchLink(links, b, c);
                BranchLink(links, c, a);
            }
            brNodes = nodes.ToArray();
            brLinks = links.Select(set => set == null ? Array.Empty<int>() : set.ToArray()).ToArray();
            var starts = Enumerable.Range(0, brNodes.Length).Where(node => brLinks[node].Length >= 2).ToArray();
            if (starts.Length < 20) { ClearBranchRiders(); return; }
            brRandom = new System.Random(code.GetHashCode() ^ branchBoard.회차);
            for (var k = 0; k < branchRiderMine + branchRiderRival; k++)
            {
                var rider = new BranchRider { Mine = k < branchRiderMine, From = starts[brRandom.Next(starts.Length)] };
                rider.To = brLinks[rider.From][brRandom.Next(brLinks[rider.From].Length)];
                rider.Heading = (brNodes[rider.To] - brNodes[rider.From]).normalized;
                rider.Speed = 26f + (float)brRandom.NextDouble() * 14f;         // 보이도록 실제보다 빠르게(표현)
                rider.NextDrop = 150f + (float)brRandom.NextDouble() * 400f;
                rider.T = (float)brRandom.NextDouble();
                branchRiders.Add(rider);
            }
            brFrameAt = 0f;
            Debug.Log($"Hongdal branch riders {code}: road nodes {brNodes.Length}, mine {branchRiderMine}, rival {branchRiderRival}");
        }

        private static void BranchLink(HashSet<int>[] links, int a, int b)
        {
            if (a == b) return;
            (links[a] ??= new HashSet<int>()).Add(b);
            (links[b] ??= new HashSet<int>()).Add(a);
        }

        private Vector3 BranchRiderPoint(BranchRider rider)
        {
            var point = Vector3.Lerp(brNodes[rider.From], brNodes[rider.To], rider.T);
            point.y = 1.6f;
            return point;
        }

        private void StepBranchRider(BranchRider rider, float delta, float now)
        {
            var left = rider.Speed * delta;
            for (var guard = 0; guard < 24 && left > 0f; guard++)
            {
                var length = Mathf.Max(0.05f, Vector3.Distance(brNodes[rider.From], brNodes[rider.To]));
                var remain = (1f - rider.T) * length;
                if (left < remain)
                {
                    rider.T += left / length;
                    break;
                }
                left -= remain;
                rider.NextDrop -= length;
                var came = rider.From;
                rider.From = rider.To;
                rider.T = 0f;
                if (rider.NextDrop <= 0f)                    // 배달을 마친 자리: 빛이 한 번 번진다
                {
                    rider.FlashAt = now;
                    rider.FlashPoint = brNodes[rider.From];
                    rider.NextDrop = 220f + (float)brRandom.NextDouble() * 480f;
                }
                var options = brLinks[rider.From];
                if (options.Length == 0) { rider.To = came; continue; }
                var best = options[0];
                var bestScore = float.MinValue;
                foreach (var option in options)                // 가던 쪽으로 이어 달린다(되돌아가기는 막다른 길에서만)
                {
                    var direction = brNodes[option] - brNodes[rider.From];
                    var reach = direction.magnitude;
                    if (reach < 0.01f) continue;
                    var score = Vector3.Dot(direction / reach, rider.Heading) + (float)brRandom.NextDouble() * 0.7f + Mathf.Min(reach, 40f) * 0.004f;
                    if (option == came) score -= 2.5f;
                    if (score > bestScore) { bestScore = score; best = option; }
                }
                rider.To = best;
                var heading = brNodes[rider.To] - brNodes[rider.From];
                if (heading.sqrMagnitude > 0.0001f) rider.Heading = Vector3.Slerp(rider.Heading, heading.normalized, 0.75f).normalized;
            }
        }

        private void BranchGlow(Vector2 at, float radius, Color colour) =>
            Paint(new Rect(at.x - radius, at.y - radius, radius * 2f, radius * 2f), colour, radius);

        // DrawPublicDongScreen이 띠보다 먼저 부른다(띠 · 단추가 위에 온다).
        private void DrawBranchRiders()
        {
            if (branchRiders.Count == 0 || brNodes == null || Event.current.type != EventType.Repaint) return;
            var now = Time.unscaledTime;
            var delta = brFrameAt <= 0f ? 0f : Mathf.Min(0.1f, now - brFrameAt);
            brFrameAt = now;
            var gold = Ink(0xFFC46B);
            // 빛이 살도록 동네를 살짝 어둡게 덮는다(해 질 녘 느낌 · 표현)
            Paint(new Rect(0f, 0f, Screen.width / pdGuiScale, Screen.height / pdGuiScale), Fade(Ink(0x060B14), 0.34f));
            var near = Mathf.Clamp(PublicDongHomeDistance / Mathf.Max(60f, pdDistance), 1f, 2.6f);      // 가까이 갈수록 빛이 커진다
            foreach (var rider in branchRiders)
            {
                StepBranchRider(rider, delta, now);
                var head = BranchRiderPoint(rider);
                if (now - rider.TrailAt >= 0.07f)
                {
                    rider.TrailAt = now;
                    for (var k = rider.Trail.Length - 1; k > 0; k--) rider.Trail[k] = rider.Trail[k - 1];
                    rider.Trail[0] = head;
                    rider.TrailCount = Mathf.Min(rider.Trail.Length, rider.TrailCount + 1);
                }
                var tint = rider.Mine ? Mint : gold;
                var power = rider.Mine ? 1f : 0.5f;
                Vector2 at;
                var age = now - rider.FlashAt;
                if (age >= 0f && age < 1.1f && OpsToLogical(rider.FlashPoint + Vector3.up * 1.6f, out at))
                {
                    var grow = age / 1.1f;
                    BranchGlow(at, (8f + 34f * grow) * near, Fade(rider.Mine ? Gold : gold, 0.34f * (1f - grow) * power));
                    BranchGlow(at, (3f + 10f * grow) * near, Fade(Paper, 0.5f * (1f - grow) * power));
                }
                for (var k = rider.TrailCount - 1; k >= 1; k--)
                {
                    if (!OpsToLogical(rider.Trail[k], out at)) continue;
                    var fade = 1f - k / (float)rider.Trail.Length;
                    BranchGlow(at, (1.4f + 2.6f * fade) * near, Fade(tint, 0.6f * fade * power));
                }
                if (!OpsToLogical(head, out at)) continue;
                var pulse = 0.85f + 0.15f * Mathf.Sin(now * 7f + rider.Speed);
                BranchGlow(at, 16f * pulse * near, Fade(tint, 0.18f * power));
                BranchGlow(at, 8f * pulse * near, Fade(tint, 0.5f * power));
                BranchGlow(at, 3.2f * near, Fade(Paper, rider.Mine ? 1f : 0.7f));
            }
        }
    }
}
