using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private const string TileFolder = "SagajeongDispatch/tiles/";
        private const float DragThreshold = 5f;
        private readonly Dictionary<Vector2Int, DispatchRoad[]> visibleTiles = new();
        private TileIndex tileIndex;
        private bool localTextureDirty;
        private bool mapPointerDown;
        private bool mapDragging;
        private bool mapClick;
        private Vector2 pressPoint;
        private Vector2 releasePoint;
        private Vector2 pressOffset;

        [Serializable]
        private sealed class TileIndex
        {
            public double west, south, step;
            public double minLongitude, maxLongitude, minLatitude, maxLatitude;
            public TileKey[] tiles;
        }

        [Serializable]
        private sealed class TileKey
        {
            public int x, y;
        }

        [Serializable]
        private sealed class RoadTile
        {
            public DispatchRoad[] roads;
        }

        private void HandleMapPointer()
        {
            mapClick = false;
            mapDoubleClick = false;
            mapMenuClick = false;
            var pointer = Event.current;
            if (mapLevel != MapLevel.Dong || tileIndex == null) return;
            if (pointer.button == 1)
            {
                // 오른쪽 단추(빛2): 뗀 자리의 빛에 차림표를 연다. 지도를 끌거나 고르지 않는다. (SagajeongDispatchRoam.cs)
                if (pointer.type == EventType.MouseUp && Field.Contains(pointer.mousePosition) && !MapOverlayCovers(pointer.mousePosition))
                {
                    mapMenuClick = true;
                    menuPoint = pointer.mousePosition - Field.position;
                    pointer.Use();
                }
                return;
            }
            if (pointer.button != 0) return;
            var point = pointer.mousePosition - Field.position;
            if (pointer.type == EventType.MouseDown && Field.Contains(pointer.mousePosition) && !MapOverlayCovers(pointer.mousePosition))
            {
                mapPointerDown = true;
                mapDragging = false;
                pressDouble = pointer.clickCount >= 2;      // 두 번 누르기의 두 번째 누름인가 (빛2)
                pressPoint = point;
                pressOffset = mapOffset;
                pointer.Use();
            }
            else if (pointer.type == EventType.MouseDrag && mapPointerDown)
            {
                var delta = point - pressPoint;
                if (mapDragging || delta.sqrMagnitude >= DragThreshold * DragThreshold)
                {
                    관찰지도수동이동();
                    mapDragging = true;
                    viewedDong = null;
                    // 2.5D에서는 화면에서 끈 방향을 평면 지도 이동량으로 되돌린다.
                    mapOffset = ClampDioramaOffset(ClampMapOffset(pressOffset + FlatDelta(delta), mapZoom), mapZoom);
                    localTextureDirty = true;
                }
                pointer.Use();
            }
            else if (pointer.type == EventType.MouseUp && mapPointerDown)
            {
                mapClick = !mapDragging && (point - pressPoint).sqrMagnitude < DragThreshold * DragThreshold &&
                    Field.Contains(pointer.mousePosition) && !MapOverlayCovers(pointer.mousePosition);
                mapDoubleClick = mapClick && pressDouble;
                mapPointerDown = false;
                releasePoint = point;
                pointer.Use();
            }
        }

        private bool HitMapMarker(Rect rect)
        {
            if (tileIndex == null) return !MapOverlayCovers(Event.current.mousePosition + Field.position) && Hit(rect);
            if (!mapClick || !rect.Contains(pressPoint) || !rect.Contains(releasePoint)) return false;
            mapClick = false;
            return true;
        }

        private Vector2 ClampMapOffset(Vector2 offset, float zoom)
        {
            // 행정동 모듈 2.5D는 도로 타일(사가정 OSM)보다 넓다. 이때는 ClampDioramaOffset이 모듈 범위 안에 붙잡는다.
            if (mapLevel == MapLevel.Dong && dioramaOn && DongModulesLoaded) return offset;
            if (tileIndex == null)
                return new Vector2(ClampMapAxis(offset.x, Field.width * (1f - zoom), 0f),
                    ClampMapAxis(offset.y, Field.height * (1f - zoom), 0f));

            const float paddingMetres = 450;
            var padding = Vector2.one * (float)(paddingMetres / metresPerPixel);
            var first = Plot(tileIndex.maxLatitude, tileIndex.minLongitude) - Field.position - padding;
            var last = Plot(tileIndex.minLatitude, tileIndex.maxLongitude) - Field.position + padding;
            return new Vector2(
                ClampMapAxis(offset.x, Field.width - last.x * zoom, -first.x * zoom),
                ClampMapAxis(offset.y, Field.height - last.y * zoom, -first.y * zoom));
        }

        // 자료가 화면보다 크면 화면을 자료 안에 붙잡고, 작으면(멀리 볼 때) 자료가 화면 안에 머무는 범위에서 움직이게 둔다.
        // 2A-1 전에는 작을 때 자료 한가운데로 고정했다. 동네 전체 보기에서 2.5D 범위 밖으로 밀려나던 원인이다.
        private static float ClampMapAxis(float offset, float minimum, float maximum) =>
            minimum <= maximum ? Mathf.Clamp(offset, minimum, maximum) : Mathf.Clamp(offset, maximum, minimum);

        private DispatchRoad[] UpdateVisibleTiles()
        {
            if (tileIndex == null) return localRoads;
            var northwest = -mapOffset / mapZoom - Field.size * 0.5f;
            var southeast = (Field.size - mapOffset) / mapZoom - Field.size * 0.5f;
            var west = centerLongitude + northwest.x * metresPerPixel / longitudeScale;
            var east = centerLongitude + southeast.x * metresPerPixel / longitudeScale;
            var north = centerLatitude - northwest.y * metresPerPixel / 111200;
            var south = centerLatitude - southeast.y * metresPerPixel / 111200;
            var firstX = TileCoordinate(west, tileIndex.west);
            var lastX = TileCoordinate(east, tileIndex.west);
            var firstY = TileCoordinate(south, tileIndex.south);
            var lastY = TileCoordinate(north, tileIndex.south);

            var current = tileIndex.tiles
                .Where(tile => tile.x >= firstX && tile.x <= lastX && tile.y >= firstY && tile.y <= lastY)
                .Select(tile => new Vector2Int(tile.x, tile.y))
                .ToHashSet();
            var removed = visibleTiles.Keys.Where(key => !current.Contains(key)).ToArray();
            foreach (var key in removed) visibleTiles.Remove(key);
            var added = 0;
            foreach (var key in current)
            {
                if (visibleTiles.ContainsKey(key)) continue;
                visibleTiles[key] = ReadMapResource<RoadTile>(TileFolder + key.x + "_" + key.y)?.roads
                    ?? Array.Empty<DispatchRoad>();
                added++;
            }
            if (added != 0 || removed.Length != 0)
                Debug.Log($"Sagajeong road tiles: loaded {added}, released {removed.Length}, visible {visibleTiles.Count}");

            var coordinates = new HashSet<(double, double, double, double)>();
            return visibleTiles.Values.SelectMany(roads => roads)
                .Where(edge => coordinates.Add((edge.a.latitude, edge.a.longitude, edge.b.latitude, edge.b.longitude)))
                .ToArray();
        }

        private int TileCoordinate(double value, double origin) =>
            (int)Math.Floor((value - origin) / tileIndex.step);
    }
}
