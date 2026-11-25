using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 정확히 대응한 원자료의 높이와 원래 외곽만 비공개 본체 표현으로 옮긴다.
    /// 층수를 높이로 환산하거나 전면·지붕·후면의 미관측 구조를 추정하지 않는다.
    /// </summary>
    public static class 사가정상세건물외형Basis
    {
        public const string Revision = "private-exact-building-envelope.r1";
        public const float SymbolicHeightMeters = 4f;
        private const string ObservedHeightKind = "ObservedSourceHeight";
        private const string FallbackHeightKind = "SymbolicFallback4m";
        private const double MaximumHeightMeters = 500d;

        public sealed class Result
        {
            public string BuildingId { get; }
            public string SourceFeatureId { get; }
            public float BodyHeightMeters { get; }
            // 점포의 비실측 전면 비율은 건물 전체 높이와 독립적으로 유지한다.
            public float StorefrontHeightMeters => SymbolicHeightMeters;
            public string HeightProvenance { get; }
            public string SourceHeightKind { get; }
            public double? ObservedHeightMeters { get; }
            public int? ObservedAboveGroundFloors { get; }
            public string HeightPolicyRevision { get; }
            public string SourceReference { get; }
            public string SourceSha256 { get; }
            public bool ObservedSourceHeightApplied => HeightProvenance == ObservedHeightKind;

            internal Result(사가정공간표현BuildingData source, float height, string provenance,
                string sourceReference, string sourceSha256)
            {
                BuildingId = source.id; SourceFeatureId = source.sourceFeatureId;
                BodyHeightMeters = height; HeightProvenance = provenance; SourceHeightKind = source.heightKind;
                ObservedHeightMeters = source.observedHeightMeters;
                ObservedAboveGroundFloors = source.observedAboveGroundFloors;
                HeightPolicyRevision = source.heightPolicyRevision;
                SourceReference = sourceReference; SourceSha256 = sourceSha256;
            }
        }

        /// <summary>기존 계약을 바꾸지 않고 값과 출처를 복사한다. 원자료 객체는 수정하지 않는다.</summary>
        public static Result Resolve(string targetBuildingId, string expectedSourceFeatureId,
            사가정공간표현BuildingData source, string sourceReference, string sourceSha256)
        {
            if (!Text(targetBuildingId) || !Text(expectedSourceFeatureId) || source == null
                || source.id != targetBuildingId || source.sourceFeatureId != expectedSourceFeatureId)
                throw new ArgumentException("DetailedEnvelopeExactSourceBindingRequired");
            if (!Text(sourceReference) || sourceSha256 == null || sourceSha256.Length != 64
                || !sourceSha256.All(Uri.IsHexDigit) || !Text(source.heightPolicyRevision))
                throw new ArgumentException("DetailedEnvelopeSourceEvidenceRequired");
            if (source.heightKind != ObservedHeightKind && source.heightKind != FallbackHeightKind)
                throw new ArgumentException("DetailedEnvelopeHeightKindRejected");
            if (!double.IsFinite(source.heightMeters) || source.heightMeters < 0d
                || source.heightMeters > MaximumHeightMeters
                || source.observedHeightMeters.HasValue && (!double.IsFinite(source.observedHeightMeters.Value)
                    || source.observedHeightMeters.Value < 0d || source.observedHeightMeters.Value > MaximumHeightMeters)
                || source.observedAboveGroundFloors.HasValue
                    && (source.observedAboveGroundFloors.Value < 0 || source.observedAboveGroundFloors.Value > 200))
                throw new ArgumentException("DetailedEnvelopeSourceDimensionsRejected");
            if (source.heightKind == FallbackHeightKind && source.observedHeightMeters > 0d)
                throw new ArgumentException("DetailedEnvelopeSymbolicHeightEvidenceMismatch");
            var observed = source.heightKind == ObservedHeightKind && source.observedHeightMeters > 0d;
            if (observed && Math.Abs(source.heightMeters - source.observedHeightMeters.Value) > .0001d)
                throw new ArgumentException("DetailedEnvelopeObservedHeightMismatch");
            return new Result(source, observed ? (float)source.observedHeightMeters.Value : SymbolicHeightMeters,
                observed ? ObservedHeightKind : FallbackHeightKind, sourceReference, sourceSha256);
        }

        /// <summary>
        /// 닫힌 단일 외곽의 수직 벽과 중립 수평 마감만 생성한다. 원본 외곽·기존 mesh는 바꾸지 않는다.
        /// UV0는 미터 단위이며 반환 mesh의 해제 책임은 호출자에게 있다.
        /// </summary>
        public static Mesh BuildMesh(Vector2[] ring, float baseY, Result basis)
        {
            if (basis == null || !float.IsFinite(baseY) || !float.IsFinite(baseY + basis.BodyHeightMeters)
                || baseY + basis.BodyHeightMeters <= baseY)
                throw new ArgumentException("DetailedEnvelopeBasisRequired");
            var polygon = ReadClosedRing(ring);
            var cap = 사가정지리MeshBuilder.참고지붕삼각형(polygon);
            if (cap.Count != (polygon.Count - 2) * 3)
                throw new ArgumentException("DetailedEnvelopeFootprintTriangulationFailed");
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            var topY = baseY + basis.BodyHeightMeters; var positive = SignedArea(polygon) > 0d;
            float perimeter = 0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i]; var q = polygon[(i + 1) % polygon.Count];
                var length = Vector2.Distance(p, q); var first = vertices.Count;
                if (!float.IsFinite(length) || !float.IsFinite(perimeter + length))
                    throw new ArgumentException("DetailedEnvelopeFootprintDimensionsRejected");
                vertices.AddRange(new[] { Point(p, baseY), Point(p, topY), Point(q, topY), Point(q, baseY) });
                uv.AddRange(new[] { new Vector2(perimeter, 0), new Vector2(perimeter, basis.BodyHeightMeters),
                    new Vector2(perimeter + length, basis.BodyHeightMeters), new Vector2(perimeter + length, 0) });
                if (positive) triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                else triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                perimeter += length;
            }
            AddCap(topY, false); AddCap(baseY, true);
            var bounds = new Bounds(vertices[triangles[0]], Vector3.zero);
            foreach (var index in triangles) bounds.Encapsulate(vertices[index]);
            var mesh = new Mesh { name = "비공개 정확외곽 본체 · " + basis.BuildingId,
                hideFlags = HideFlags.DontSave,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            try
            {
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0, false);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.bounds = bounds;
                return mesh;
            }
            catch
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }

            void AddCap(float y, bool bottom)
            {
                var first = vertices.Count;
                foreach (var p in polygon) { vertices.Add(Point(p, y)); uv.Add(p - polygon[0]); }
                for (var i = 0; i < cap.Count; i += 3)
                {
                    triangles.Add(first + cap[i]);
                    triangles.Add(first + cap[i + (bottom ? 2 : 1)]);
                    triangles.Add(first + cap[i + (bottom ? 1 : 2)]);
                }
            }
        }

        private static List<Vector2> ReadClosedRing(Vector2[] ring)
        {
            if (ring == null || ring.Length < 4 || ring.Length > 2049
                || ring.Any(p => !float.IsFinite(p.x) || !float.IsFinite(p.y)) || !ring[0].Equals(ring[^1]))
                throw new ArgumentException("DetailedEnvelopeClosedFootprintRequired");
            var polygon = ring.Take(ring.Length - 1).ToList();
            if (polygon.Distinct().Count() != polygon.Count || Math.Abs(SignedArea(polygon)) <= .0001d)
                throw new ArgumentException("DetailedEnvelopeSimpleFootprintRequired");
            for (var i = 0; i < polygon.Count; i++)
            {
                var next = (i + 1) % polygon.Count;
                if ((polygon[i] - polygon[next]).sqrMagnitude <= .00000001f)
                    throw new ArgumentException("DetailedEnvelopeSimpleFootprintRequired");
                for (var j = i + 1; j < polygon.Count; j++)
                {
                    var otherNext = (j + 1) % polygon.Count;
                    if (j == next || otherNext == i) continue;
                    if (Intersects(polygon[i], polygon[next], polygon[j], polygon[otherNext]))
                        throw new ArgumentException("DetailedEnvelopeSimpleFootprintRequired");
                }
            }
            return polygon;
        }

        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var abC = Cross(a, b, c); var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a); var cdB = Cross(c, d, b);
            if ((abC > 0d && abD < 0d || abC < 0d && abD > 0d)
                && (cdA > 0d && cdB < 0d || cdA < 0d && cdB > 0d)) return true;
            return abC == 0d && OnLine(a, b, c) || abD == 0d && OnLine(a, b, d)
                || cdA == 0d && OnLine(c, d, a) || cdB == 0d && OnLine(c, d, b);
        }
        private static bool OnLine(Vector2 a, Vector2 b, Vector2 p)
            => p.x >= Math.Min(a.x, b.x) && p.x <= Math.Max(a.x, b.x)
                && p.y >= Math.Min(a.y, b.y) && p.y <= Math.Max(a.y, b.y);
        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
            => ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
        private static double SignedArea(IReadOnlyList<Vector2> polygon)
        {
            double area = 0;
            for (var i = 1; i < polygon.Count - 1; i++) area += Cross(polygon[0], polygon[i], polygon[i + 1]);
            return area * .5d;
        }
        private static Vector3 Point(Vector2 p, float y) => new(p.x, y, p.y);
        private static bool Text(string value)
            => !string.IsNullOrWhiteSpace(value) && value == value.Trim() && !value.Any(char.IsControl);
    }
}
