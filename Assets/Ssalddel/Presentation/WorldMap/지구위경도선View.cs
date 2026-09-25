using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>관측 위치를 읽기 위한 구면 기준선. 격자 간격은 관측자료 해상도가 아니다.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class 지구위경도선View : MonoBehaviour
    {
        private Transform fineRoot;
        private Transform globeRoot;
        private Camera viewCamera;
        private Canvas ownerCanvas;
        private Material majorMaterial, minorMaterial;
        private float radius;
        private readonly List<(Text Text, double Latitude, double Longitude)> labels = new();
        public int SpacingDegrees => FineVisible ? 2 : 10;
        public bool FineVisible => fineRoot != null && fineRoot.gameObject.activeSelf;
        public int LineCount { get; private set; }

        public void Initialize(Transform globe, Canvas canvas, Camera camera, float globeRadius)
        {
            if (fineRoot != null) return;
            globeRoot = globe; ownerCanvas = canvas; viewCamera = camera; radius = globeRadius + .16f;
            majorMaterial = Material(.55f); minorMaterial = Material(.22f);
            var major = Child("위경도 주선 10도"); fineRoot = Child("위경도 보조선 2도");
            for (var longitude = -180; longitude < 180; longitude += 2)
            {
                var primary = longitude % 10 == 0;
                Line(primary ? major : fineRoot, "경도 " + longitude,
                    Meridian(longitude, radius, primary ? 90 : 76), false, primary);
            }
            for (var latitude = -80; latitude <= 80; latitude += 2)
            {
                var primary = latitude % 10 == 0;
                if (!primary && Math.Abs(latitude) > 76) continue;
                Line(primary ? major : fineRoot, "위도 " + latitude, Parallel(latitude, radius), true, primary);
            }
            fineRoot.gameObject.SetActive(false);
            foreach (var lat in new[] { 20, 30, 40, 50 }) Label(lat + "°N", lat, 147);
            foreach (var lon in new[] { 110, 120, 130, 140 }) Label(lon + "°E", 24, lon);
        }

        public void UpdateDensity(float cameraDistance)
        {
            if (fineRoot == null) return;
            var visible = FineVisible ? cameraDistance < 42f : cameraDistance <= 40f;
            if (visible != FineVisible) fineRoot.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (viewCamera == null || globeRoot == null || ownerCanvas == null) return;
            UpdateDensity(Vector3.Distance(viewCamera.transform.position, globeRoot.position));
            var canvasRect = ownerCanvas.GetComponent<RectTransform>();
            foreach (var entry in labels)
            {
                if (entry.Text == null) continue;
                var world = globeRoot.TransformPoint(Coordinate(entry.Latitude, entry.Longitude, radius + .03f));
                var facing = Vector3.Dot(world - globeRoot.position, viewCamera.transform.position - world) > 0;
                var screen = viewCamera.WorldToScreenPoint(world);
                entry.Text.gameObject.SetActive(viewCamera.enabled && facing && screen.z > 0
                    && screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
                    entry.Text.rectTransform.anchoredPosition = local;
            }
        }

        public static Vector3 Coordinate(double latitude, double longitude, float radius)
        {
            var p = 세계지구구면좌표.FromLatitudeLongitude(latitude, longitude);
            return new Vector3(-(float)p.X, (float)p.Y, (float)p.Z) * radius;
        }
        public static Vector3[] Meridian(int longitude, float radius, int limit = 90)
        {
            var points = new Vector3[limit * 2 + 1];
            for (var i = 0; i < points.Length; i++) points[i] = Coordinate(-limit + i, longitude, radius);
            return points;
        }
        public static Vector3[] Parallel(int latitude, float radius)
        {
            var points = new Vector3[360];
            for (var i = 0; i < points.Length; i++) points[i] = Coordinate(latitude, -180 + i, radius);
            return points;
        }
        private Transform Child(string name)
        {
            var item = new GameObject(name); item.transform.SetParent(transform, false);
            item.layer = 세계지구본View.GlobeLayer; return item.transform;
        }
        private void Line(Transform parent, string name, Vector3[] points, bool loop, bool primary)
        {
            var item = new GameObject(name, typeof(LineRenderer)); item.layer = 세계지구본View.GlobeLayer;
            item.transform.SetParent(parent, false);
            var line = item.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = loop;
            line.sharedMaterial = primary ? majorMaterial : minorMaterial;
            line.widthMultiplier = primary ? .06f : .03f;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.positionCount = points.Length; line.SetPositions(points); LineCount++;
        }
        private static Material Material(float alpha)
        {
            var shader = Shader.Find("Sprites/Default") ?? throw new InvalidOperationException("GlobeGridShaderMissing");
            return new Material(shader) { color = new Color(1f, 1f, 1f, alpha), hideFlags = HideFlags.DontSave };
        }
        private void Label(string value, double lat, double lon)
        {
            var item = new GameObject("위경도 " + value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(ownerCanvas.transform, false);
            var text = item.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13; text.color = new Color(1, 1, 1, .85f); text.text = value;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.sizeDelta = new Vector2(65, 24);
            labels.Add((text, lat, lon));
        }
        private void OnDestroy()
        {
            foreach (var entry in labels) if (entry.Text != null) Release(entry.Text.gameObject);
            labels.Clear(); Release(majorMaterial); Release(minorMaterial);
        }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
