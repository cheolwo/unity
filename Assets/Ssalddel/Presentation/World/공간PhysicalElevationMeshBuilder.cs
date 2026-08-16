using System;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 배치 판정용 원본 표고 표본을 변경하지 않고, 중앙 500m만 표현용 Mesh로 변환한다.
    /// 높이 과장은 이 Renderer 산출물에만 적용한다.
    /// </summary>
    public static class 공간PhysicalElevationMeshBuilder
    {
        public static Mesh BuildCoreMesh(
            공간TileArtifactPayloadData payload,
            int haloMeters,
            int tileSizeMeters,
            float tileWorldSize,
            float visualElevationExaggeration,
            out float minimumPhysicalElevationMeters,
            out float maximumPhysicalElevationMeters)
        {
            payload.Validate();
            if (payload.LayerCode != 공간TileStreamingCodes.ElevationLayer
                || payload.ArtifactFormatCode != "height-f32-v1"
                || payload.Bytes.Length != payload.SampleWidth * payload.SampleHeight * 4
                || payload.SampleWidth != payload.SampleHeight
                || haloMeters < 0 || tileSizeMeters <= 0 || tileWorldSize <= 0f
                || visualElevationExaggeration <= 0f)
                throw new InvalidOperationException("WorldTileElevationArtifactInvalid");

            var spacing = (tileSizeMeters + haloMeters * 2d) / (payload.SampleWidth - 1d);
            var cropStart = (int)Math.Round(haloMeters / spacing);
            var coreSampleCount = (int)Math.Round(tileSizeMeters / spacing) + 1;
            if (cropStart < 0 || cropStart + coreSampleCount > payload.SampleWidth)
                throw new InvalidOperationException("WorldTileElevationHaloCropInvalid");

            minimumPhysicalElevationMeters = float.PositiveInfinity;
            maximumPhysicalElevationMeters = float.NegativeInfinity;
            var heights = new float[coreSampleCount * coreSampleCount];
            for (var row = 0; row < coreSampleCount; row++)
            for (var column = 0; column < coreSampleCount; column++)
            {
                var sourceIndex = (row + cropStart) * payload.SampleWidth + column + cropStart;
                var height = ReadLittleEndianSingle(payload.Bytes, sourceIndex * 4);
                if (float.IsNaN(height) || float.IsInfinity(height))
                    throw new InvalidOperationException("WorldTileElevationSampleInvalid");
                heights[row * coreSampleCount + column] = height;
                minimumPhysicalElevationMeters = Mathf.Min(minimumPhysicalElevationMeters, height);
                maximumPhysicalElevationMeters = Mathf.Max(maximumPhysicalElevationMeters, height);
            }

            var vertices = new Vector3[heights.Length];
            var triangles = new int[(coreSampleCount - 1) * (coreSampleCount - 1) * 6];
            var horizontalScale = tileWorldSize / tileSizeMeters;
            var half = tileWorldSize * .5f;
            for (var row = 0; row < coreSampleCount; row++)
            for (var column = 0; column < coreSampleCount; column++)
            {
                var index = row * coreSampleCount + column;
                vertices[index] = new Vector3(
                    -half + column * (tileWorldSize / (coreSampleCount - 1)),
                    (heights[index] - minimumPhysicalElevationMeters)
                    * horizontalScale * visualElevationExaggeration,
                    half - row * (tileWorldSize / (coreSampleCount - 1)));
            }

            var triangleIndex = 0;
            for (var row = 0; row < coreSampleCount - 1; row++)
            for (var column = 0; column < coreSampleCount - 1; column++)
            {
                var a = row * coreSampleCount + column;
                var b = a + 1;
                var c = a + coreSampleCount;
                var d = c + 1;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = d;
                triangles[triangleIndex++] = c;
            }

            var mesh = new Mesh { name = "PhysicalElevation_Core500m_PresentationOnly" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float ReadLittleEndianSingle(byte[] bytes, int offset)
        {
            if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(bytes, offset);
            var copy = new[] { bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset] };
            return BitConverter.ToSingle(copy, 0);
        }
    }
}
