using System;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    public sealed partial class SagajeongDispatchView
    {
        private const string CityBoardFolder = "SagajeongDispatch/city-board/";
        private const double MyeonmokStationLatitude = 37.588671;
        private const double MyeonmokStationLongitude = 127.087503;
        private CitySurface defaultCityBoard;
        private CitySurface selectedCityBoard;
        private bool cityBoardLoaded;

        [Serializable]
        private sealed class CitySurfaceBounds
        {
            public double west, south, east, north;

            public bool Contains(double latitude, double longitude) =>
                latitude >= south && latitude <= north && longitude >= west && longitude <= east;
        }

        private sealed class CitySurface
        {
            public CitySurfaceBounds Bounds;
            public Color32[] Pixels;
            public int Width, Height;
        }

        private void LoadCityBoard()
        {
            if (cityBoardLoaded) return;
            cityBoardLoaded = true;
            defaultCityBoard = ReadCitySurface(CityBoardFolder);
        }

        private static CitySurface ReadCitySurface(string folder)
        {
            var bounds = ReadMapResource<CitySurfaceBounds>(folder + "bounds");
            if (bounds == null || bounds.east <= bounds.west || bounds.north <= bounds.south) return null;

            var surface = Resources.Load<Texture2D>(folder + "surface");
            if (surface == null) return null;
            var result = surface.isReadable
                ? new CitySurface { Bounds = bounds, Pixels = surface.GetPixels32(), Width = surface.width, Height = surface.height }
                : null;
            Resources.UnloadAsset(surface);
            return result;
        }

        private void SelectDongCityBoard(LegalDong dong)
        {
            var code = dong.id.Substring(dong.id.LastIndexOf(':') + 1);
            selectedCityBoard = ReadCitySurface("SagajeongDispatch/legal-city-board/" + code + "/");
            if (selectedCityBoard != null)
                Debug.Log("Sagajeong city board detail: " + dong.id);
        }

        private bool ShowsMyeonmokStation =>
            defaultCityBoard?.Bounds.Contains(MyeonmokStationLatitude, MyeonmokStationLongitude) == true;

        private void DrawCityBoardControls()
        {
            if (!ShowsMyeonmokStation || tileIndex == null) return;

            var button = new Rect(687, 136, 113, 28);
            Paint(button, Tile, 7);
            Text(button, "면목역 보기", 13, Paper, true, TextAnchor.MiddleCenter);
            if (!Hit(button)) return;

            viewedDong = null;
            selectedCityBoard = null;
            mapZoom = 1f;
            mapOffset = ClampMapOffset(Field.center - Plot(MyeonmokStationLatitude, MyeonmokStationLongitude), mapZoom);
            mapPointerDown = false;
            mapDragging = false;
            mapClick = false;
            localTextureDirty = true;
            Debug.Log("Sagajeong city board: Myeonmok station");
        }

        private void DrawCityBoardLandmark()
        {
            if (!ShowsMyeonmokStation) return;

            var point = PlotScene(MyeonmokStationLatitude, MyeonmokStationLongitude);
            if (!new Rect(Vector2.zero, Field.size).Contains(point)) return;
            var plate = Centered(point + new Vector2(0, -17), 65, 19);
            Paint(Centered(point, 8, 8), Soft, 4);
            Paint(plate, Canvas, 5);
            Text(plate, "면목역", 11, Soft, true, TextAnchor.MiddleCenter);
        }

        private bool IsMyeonmokStationOnBoard() => ShowsMyeonmokStation &&
            new Rect(Vector2.zero, Field.size).Contains(PlotOnBoard(MyeonmokStationLatitude, MyeonmokStationLongitude));

        private void BlendCityBoard(Color32[] pixels, int width, int height)
        {
            LoadCityBoard();
            var city = selectedCityBoard ?? defaultCityBoard;
            if (city == null) return;

            var northwest = PlotOnBoard(city.Bounds.north, city.Bounds.west);
            var southeast = PlotOnBoard(city.Bounds.south, city.Bounds.east);
            var left = Mathf.Max(0, Mathf.CeilToInt(northwest.x));
            var right = Mathf.Min(width, Mathf.FloorToInt(southeast.x));
            var top = Mathf.Max(0, Mathf.CeilToInt(northwest.y));
            var bottom = Mathf.Min(height, Mathf.FloorToInt(southeast.y));
            if (left >= right || top >= bottom) return;

            var sampleWidth = city.Width / (southeast.x - northwest.x);
            var sampleHeight = city.Height / (southeast.y - northwest.y);
            for (var y = top; y < bottom; y++)
            {
                var sourceY = Mathf.Clamp((int)((southeast.y - y - 0.5f) * sampleHeight), 0, city.Height - 1);
                var sourceRow = sourceY * city.Width;
                var targetRow = (height - 1 - y) * width;
                for (var x = left; x < right; x++)
                {
                    var sourceX = Mathf.Clamp((int)((x + 0.5f - northwest.x) * sampleWidth), 0, city.Width - 1);
                    var surface = city.Pixels[sourceRow + sourceX];
                    if (surface.a == 0) continue;

                    var target = targetRow + x;
                    var ground = pixels[target];
                    var inverse = 255 - surface.a;
                    pixels[target] = new Color32(
                        (byte)((surface.r * surface.a + ground.r * inverse) / 255),
                        (byte)((surface.g * surface.a + ground.g * inverse) / 255),
                        (byte)((surface.b * surface.a + ground.b * inverse) / 255),
                        255);
                }
            }
        }
    }
}
