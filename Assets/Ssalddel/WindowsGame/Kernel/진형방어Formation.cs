using System;
using System.Collections.Generic;
using System.Linq;

namespace Ssalddel.WindowsGame.Kernel
{
    /// <summary>분대 간 이음과 옥상 배치로 오행진 하나를 읽는다.</summary>
    public static class 진형방어Formation
    {
        private const double Epsilon = 1e-8;

        public static FormationRead Read(DefenseSetup setup, IEnumerable<DefenseVertex> vertices, string entrance)
        {
            var ordered = vertices.OrderBy(vertex => vertex.Squad, StringComparer.Ordinal).ToArray();
            var search = new FormationSearch(ordered);
            if (ordered.Length < 3)
                return search.Result;

            var entranceNode = setup.Nodes.FirstOrDefault(node => node.Id == entrance);
            search.직진();
            search.예진과방진(entranceNode);
            search.원진();
            search.곡진(setup);
            return search.Result;
        }

        private sealed class FormationSearch
        {
            private readonly DefenseVertex[] vertices;
            private readonly bool[,] links;
            private readonly int[] indices;
            private int[] selected = Array.Empty<int>();

            public FormationRead Result { get; } = new FormationRead();

            public FormationSearch(DefenseVertex[] vertices)
            {
                this.vertices = vertices;
                indices = Enumerable.Range(0, vertices.Length).ToArray();
                links = new bool[vertices.Length, vertices.Length];
                for (var a = 0; a < vertices.Length; a++)
                {
                    for (var b = a + 1; b < vertices.Length; b++)
                    {
                        var left = vertices[a];
                        var right = vertices[b];
                        var reach = Math.Min(left.Reach, right.Reach);
                        var connected = reach >= 0 && DistanceSquared(left, right) <= reach * (double)reach + Epsilon;
                        links[a, b] = links[b, a] = connected;
                        Result.Links.Add(new DefenseLink { A = left.Squad, B = right.Squad, Connected = connected });
                    }
                }
            }

            public void 직진()
            {
                foreach (var a in indices)
                {
                    for (var b = a + 1; b < vertices.Length; b++)
                    {
                        var start = vertices[a];
                        var end = vertices[b];
                        var span = Math.Sqrt(DistanceSquared(start, end));
                        if (!links[a, b] || span <= 진형방어Policy.LineSpan)
                            continue;
                        var members = Grow(new[] { a, b }, indices
                            .Where(i => SegmentDistanceSquared(vertices[i], start, end, out var t) <=
                                진형방어Policy.LineTolerance * 진형방어Policy.LineTolerance + Epsilon &&
                                t >= -Epsilon && t <= 1 + Epsilon)
                            .OrderBy(i => SegmentDistanceSquared(vertices[i], start, end, out _)));
                        if (members.Length >= 3)
                            Consider(FormationKind.Line, members, new[] { a, b });
                    }
                }
            }

            public void 예진과방진(DefensePoint entrance)
            {
                foreach (var a in indices)
                {
                    for (var b = a + 1; b < vertices.Length; b++)
                    {
                        if (!links[a, b])
                            continue;
                        for (var c = b + 1; c < vertices.Length; c++)
                        {
                            if (!links[a, c] || !links[b, c])
                                continue;
                            if (entrance != null)
                            {
                                예진(a, b, c, entrance);
                                예진(b, a, c, entrance);
                                예진(c, a, b, entrance);
                            }
                            for (var d = c + 1; d < vertices.Length; d++)
                                if (links[a, d] && links[b, d] && links[c, d])
                                    방진(new[] { a, b, c, d });
                        }
                    }
                }
            }

            private void 예진(int tip, int left, int right, DefensePoint entrance)
            {
                var apex = vertices[tip];
                var a = vertices[left];
                var b = vertices[right];
                if (Math.Abs(Cross(a, b, apex)) <= Epsilon || Angle(a, apex, b) > 진형방어Policy.ArrowAngle + Epsilon)
                    return;
                var middle = new DefensePoint { X = (a.X + b.X) / 2, Z = (a.Z + b.Z) / 2 };
                // 꼭지각이 적 입구 쪽으로 향하는 예진만 인정한다.
                if ((apex.X - middle.X) * (entrance.X - middle.X) +
                    (apex.Z - middle.Z) * (entrance.Z - middle.Z) <= Epsilon)
                    return;
                var corners = new[] { tip, left, right };
                Consider(FormationKind.Arrow, corners, corners, apex.Squad);
            }

            private void 방진(int[] corners)
            {
                var center = Center(corners);
                corners = Around(corners, center);
                for (var i = 0; i < corners.Length; i++)
                {
                    var a = vertices[corners[(i + 3) % 4]];
                    var b = vertices[corners[i]];
                    var c = vertices[corners[(i + 1) % 4]];
                    if (Cross(a, b, c) <= Epsilon ||
                        Math.Abs(Angle(a, b, c) - 90) > 진형방어Policy.SquareTolerance + Epsilon)
                        return;
                }
                // 네 귀가 정해지면 같은 둘레 위에 서고 네 귀와 이어진 분대도 포함할 수 있다.
                var members = Grow(corners, indices.Where(i => Enumerable.Range(0, 4).Any(edge =>
                    SegmentDistanceSquared(vertices[i], vertices[corners[edge]], vertices[corners[(edge + 1) % 4]], out var t)
                    <= 진형방어Policy.LineTolerance * 진형방어Policy.LineTolerance + Epsilon && t > 0 && t < 1)));
                Consider(FormationKind.Square, members, corners);
            }

            public void 원진()
            {
                if (vertices.Length < 5)
                    return;
                // 전체 배치의 중심과 세 점이 정하는 원의 중심을 살핀다.
                // 모든 배치 부분집합을 만들지 않고 반경과 실선이 맞는 점만 보탠다.
                var center = Center(indices);
                var radius = indices.Average(index => Math.Sqrt(DistanceSquared(vertices[index], center)));
                원진(center, radius, Array.Empty<int>());
                foreach (var a in indices)
                {
                    for (var b = a + 1; b < vertices.Length; b++)
                    {
                        if (!links[a, b])
                            continue;
                        for (var c = b + 1; c < vertices.Length; c++)
                        {
                            if (!links[a, c] || !links[b, c])
                                continue;
                            center = Circumcenter(vertices[a], vertices[b], vertices[c]);
                            if (center != null)
                                원진(center, Math.Sqrt(DistanceSquared(vertices[a], center)), new[] { a, b, c });
                        }
                    }
                }
            }

            private void 원진(DefensePoint center, double radius, int[] seeds)
            {
                if (radius <= Epsilon)
                    return;
                var members = Grow(seeds, indices.Where(i => Math.Abs(Math.Sqrt(DistanceSquared(vertices[i], center)) - radius)
                        <= radius * 진형방어Policy.CircleRadialTolerance + Epsilon)
                    .OrderBy(i => Math.Abs(Math.Sqrt(DistanceSquared(vertices[i], center)) - radius)));
                if (members.Length < 5)
                    return;
                // 판정은 후보를 고를 때 쓴 반경이 아니라 확정한 분대의 평균 거리를 사용한다.
                var radii = members.Select(index => Math.Sqrt(DistanceSquared(vertices[index], center))).ToArray();
                var average = radii.Average();
                if (radii.Any(r => Math.Abs(r - average) > average * 진형방어Policy.CircleRadialTolerance + Epsilon))
                    return;
                var ring = Around(members, center);
                for (var i = 0; i < ring.Length; i++)
                {
                    var gap = Direction(center, vertices[ring[(i + 1) % ring.Length]]) - Direction(center, vertices[ring[i]]);
                    if (i == ring.Length - 1)
                        gap += 360;
                    if (gap <= Epsilon || gap > 진형방어Policy.CircleGap + Epsilon)
                        return;
                }
                Consider(FormationKind.Circle, members, ring);
            }

            public void 곡진(DefenseSetup setup)
            {
                if (vertices.Length < 4)
                    return;
                foreach (var chain in RoadChains(setup))
                {
                    if (chain.Count < 3)
                        continue;
                    var points = indices.Select(i => ProjectToChain(i, chain))
                        .Where(point => point.OffsetSquared <= 진형방어Policy.CurveTolerance * 진형방어Policy.CurveTolerance + Epsilon)
                        .OrderBy(point => point.Along)
                        .ThenBy(point => point.Index)
                        .ToArray();
                    foreach (var seed in points)
                    {
                        var members = new List<PathProjection> { seed };
                        foreach (var p in points)
                        {
                            if (members.All(m => Math.Abs(p.Along - m.Along) > Epsilon && links[p.Index, m.Index]))
                                members.Add(p);
                        }
                        if (members.Count < 4)
                            continue;
                        members.Sort((a, b) => a.Along.CompareTo(b.Along));
                        var first = members[0];
                        var last = members[members.Count - 1];
                        if (last.Segment == first.Segment || !Enumerable.Range(first.Segment + 1, last.Segment - first.Segment)
                                .Any(i => Math.Abs(Cross(first.Point, last.Point, chain[i])) > Epsilon))
                            continue;
                        var order = members.Select(p => p.Index).ToArray();
                        Consider(FormationKind.Curve, order, order);
                    }
                }
            }

            private PathProjection ProjectToChain(int index, IReadOnlyList<DefenseNode> chain)
            {
                var best = new PathProjection { Index = index, OffsetSquared = double.PositiveInfinity };
                var along = 0.0;
                for (var i = 0; i + 1 < chain.Count; i++)
                {
                    var a = chain[i];
                    var b = chain[i + 1];
                    var length = Math.Sqrt(DistanceSquared(a, b));
                    var offset = SegmentDistanceSquared(vertices[index], a, b, out var t);
                    t = Math.Max(0, Math.Min(1, t));
                    if (offset < best.OffsetSquared - Epsilon)
                    {
                        best.OffsetSquared = offset;
                        best.Segment = i;
                        best.Along = along + t * length;
                        best.Point = new DefensePoint { X = a.X + t * (b.X - a.X), Z = a.Z + t * (b.Z - a.Z) };
                    }
                    along += length;
                }
                return best;
            }

            private int[] Grow(IEnumerable<int> initial, IEnumerable<int> additions)
            {
                var members = new List<int>(initial);
                foreach (var index in additions)
                    if (members.All(member => links[index, member] &&
                        DistanceSquared(vertices[index], vertices[member]) > Epsilon))
                        members.Add(index);
                return members.ToArray();
            }

            private void Consider(FormationKind kind, int[] members, int[] corners, string tip = "")
            {
                if (members.Length < selected.Length || members.Length == selected.Length && (int)kind > (int)Result.Kind)
                    return;
                var ordered = members.OrderBy(index => index).ToArray();
                if (members.Length == selected.Length && kind == Result.Kind && !Precedes(ordered, selected))
                    return;
                selected = ordered;
                Result.Kind = kind;
                Result.Tip = tip;
                Result.Squads = ordered.Select(index => vertices[index].Squad).ToList();
                Result.Corners = corners.Select(index => new DefensePoint { X = vertices[index].X, Z = vertices[index].Z }).ToList();
            }

            private static bool Precedes(int[] candidate, int[] previous)
            {
                // 같은 모양과 인원 수이면 분대 ID가 앞서는 배치를 일관되게 남긴다.
                for (var i = 0; i < candidate.Length; i++)
                    if (candidate[i] != previous[i])
                        return candidate[i] < previous[i];
                return false;
            }

            private DefensePoint Center(IEnumerable<int> offsets)
            {
                var group = offsets.Select(index => vertices[index]).ToArray();
                return new DefensePoint { X = group.Average(vertex => vertex.X), Z = group.Average(vertex => vertex.Z) };
            }

            private int[] Around(IEnumerable<int> offsets, DefensePoint center)
            {
                return offsets.OrderBy(index => Direction(center, vertices[index])).ThenBy(index => index).ToArray();
            }
        }

        private sealed class PathProjection
        {
            public int Index;
            public int Segment;
            public double Along;
            public double OffsetSquared;
            public DefensePoint Point;
        }

        private static IEnumerable<List<DefenseNode>> RoadChains(DefenseSetup setup)
        {
            // 도로 ID는 시험 자료에 없으므로 물을 뺀 비분기 연속 구간을 같은 길로 취급한다.
            var edges = setup.Edges.Where(edge => !edge.Water).OrderBy(edge => edge.Id, StringComparer.Ordinal).ToArray();
            var nodes = setup.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var adjacent = nodes.Keys.ToDictionary(id => id, _ => new List<DefenseEdge>(), StringComparer.Ordinal);
            foreach (var edge in edges)
            {
                adjacent[edge.A].Add(edge);
                adjacent[edge.B].Add(edge);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var start in nodes.Keys.OrderBy(id => id, StringComparer.Ordinal).Where(id => adjacent[id].Count != 2))
                foreach (var edge in adjacent[start])
                    if (!seen.Contains(edge.Id))
                        yield return ReadChain(start, edge, nodes, adjacent, seen);
            foreach (var edge in edges)
                if (!seen.Contains(edge.Id))
                    yield return ReadChain(edge.A, edge, nodes, adjacent, seen);
        }

        private static List<DefenseNode> ReadChain(string start, DefenseEdge edge,
            Dictionary<string, DefenseNode> nodes, Dictionary<string, List<DefenseEdge>> adjacent, HashSet<string> seen)
        {
            var chain = new List<DefenseNode> { nodes[start] };
            var current = start;
            while (edge != null && seen.Add(edge.Id))
            {
                current = edge.A == current ? edge.B : edge.A;
                chain.Add(nodes[current]);
                edge = adjacent[current].Count == 2
                    ? adjacent[current].FirstOrDefault(next => !seen.Contains(next.Id))
                    : null;
            }
            return chain;
        }

        private static double DistanceSquared(DefensePoint a, DefensePoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static double SegmentDistanceSquared(DefensePoint point, DefensePoint a, DefensePoint b, out double t)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            var lengthSquared = dx * dx + dz * dz;
            t = lengthSquared <= Epsilon ? 0 : ((point.X - a.X) * dx + (point.Z - a.Z) * dz) / lengthSquared;
            var clamped = Math.Max(0, Math.Min(1, t));
            var x = point.X - (a.X + clamped * dx);
            var z = point.Z - (a.Z + clamped * dz);
            return x * x + z * z;
        }

        private static double Direction(DefensePoint a, DefensePoint b)
        {
            return Math.Atan2(b.Z - a.Z, b.X - a.X) * 180 / Math.PI;
        }

        private static double Angle(DefensePoint a, DefensePoint corner, DefensePoint b)
        {
            var vectors = Math.Sqrt(DistanceSquared(a, corner) * DistanceSquared(b, corner));
            if (vectors <= Epsilon)
                return double.PositiveInfinity;
            var cosine = ((a.X - corner.X) * (b.X - corner.X) + (a.Z - corner.Z) * (b.Z - corner.Z)) / vectors;
            return Math.Acos(Math.Max(-1, Math.Min(1, cosine))) * 180 / Math.PI;
        }

        private static double Cross(DefensePoint a, DefensePoint b, DefensePoint c)
        {
            return (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
        }

        private static DefensePoint Circumcenter(DefensePoint a, DefensePoint b, DefensePoint c)
        {
            var bx = b.X - a.X;
            var bz = b.Z - a.Z;
            var cx = c.X - a.X;
            var cz = c.Z - a.Z;
            var determinant = 2 * (bx * cz - bz * cx);
            if (Math.Abs(determinant) <= Epsilon)
                return null;
            var bb = bx * bx + bz * bz;
            var cc = cx * cx + cz * cz;
            return new DefensePoint
            {
                X = a.X + (cz * bb - bz * cc) / determinant,
                Z = a.Z + (bx * cc - cx * bb) / determinant,
            };
        }
    }
}
