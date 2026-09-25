using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.WorldMap;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    /// <summary>
    /// 공식 공개자료에서 잘라낸 한반도 지형 사본을 단계적으로 보여 주는 읽기 전용 View다.
    /// 이 화면은 법적 경계, 정밀 고도, 이동 가능 구역 또는 운영 상태를 확정하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 한반도지형미리보기View : MonoBehaviour
    {
        public const string RootName = "KoreanPeninsulaTerrainPreviewRoot";
        public const string InformationPanelName = "KoreanPeninsulaTerrainPreviewInformation";
        // 사진형 지형 대신 지구본 위 행정 경계를 우선한다. 원본/계약은 보존한다.
        public static bool PhotoSurfaceEnabled => false;

        private const float GlobeRadius = 18f;
        private const float ReliefRadius = GlobeRadius + .075f;
        private const float RiverRadius = GlobeRadius + .11f;
        private const float CoastlineRadius = GlobeRadius + .145f;
        private const int ParentLatitudeSegments = 48;
        private const int ParentLongitudeSegments = 36;
        private const int RegionalTilesPerAxis = 2;
        private const int RegionalLatitudeSegments = 48;
        private const int RegionalLongitudeSegments = 36;
        private const int LocalTilesPerAxis = 4;
        private const int LocalLatitudeSegments = 32;
        private const int LocalLongitudeSegments = 24;
        private const int GlobeLayer = 세계지구본View.GlobeLayer;

        private 한반도지형미리보기Catalog catalog;
        private 세계지구본Catalog worldCatalog;
        private GameObject previewRoot;
        private GameObject coastlineLayer;
        private GameObject reliefLayer;
        private GameObject regionalReliefTileRoot;
        private GameObject localReliefTileRoot;
        private GameObject riverLayer;
        private GameObject hubReadinessLayer;
        private GameObject informationPanel;
        private Text stageLabel;
        private Material reliefMaterial;
        private Material riverMaterial;
        private Material coastlineMaterial;
        private 한반도지형미리보기단계 stage;
        private float zoomBlend;
        private bool surfaceDetailComparisonActive;
        private 한반도지형타일화면오차선택결과 surfaceDetailComparison;
        private bool initialized;

        public 한반도지형미리보기단계 Stage => stage;
        public bool IsVisible => previewRoot != null && previewRoot.activeSelf;
        public bool IsReliefVisible => reliefLayer != null && reliefLayer.activeSelf
            || regionalReliefTileRoot != null && regionalReliefTileRoot.activeSelf
            || localReliefTileRoot != null && localReliefTileRoot.activeSelf;
        public bool IsRiverVisible => riverLayer != null && riverLayer.activeSelf;
        public bool HasCollider => previewRoot != null
            && previewRoot.GetComponentInChildren<Collider>(true) != null;
        public float ZoomBlend => zoomBlend;
        public bool IsCurvedSurface => reliefLayer != null
            && reliefLayer.GetComponent<MeshFilter>()?.sharedMesh != null;
        public int ReliefVertexCount => reliefLayer != null
            ? reliefLayer.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount ?? 0
            : 0;
        public int SurfaceDetailLevel => localReliefTileRoot != null
            && localReliefTileRoot.activeSelf
                ? 2
                : regionalReliefTileRoot != null && regionalReliefTileRoot.activeSelf
                    ? 1
                    : reliefLayer != null && reliefLayer.activeSelf ? 0 : -1;
        public int ActiveSurfaceTileCount => SurfaceDetailLevel switch
        {
            0 => 1,
            1 => regionalReliefTileRoot.transform.childCount,
            2 => localReliefTileRoot.transform.childCount,
            _ => 0,
        };
        public int GeneratedSurfaceTileCount => 1
            + (regionalReliefTileRoot?.transform.childCount ?? 0)
            + (localReliefTileRoot?.transform.childCount ?? 0);
        public bool IsSurfaceDetailComparisonActive => surfaceDetailComparisonActive;
        public float TargetSsePixels => surfaceDetailComparisonActive
            ? (float)surfaceDetailComparison.TargetSsePixels
            : float.NaN;
        public float EstimatedSsePixels => surfaceDetailComparisonActive
            ? (float)surfaceDetailComparison.EstimatedSsePixels
            : float.NaN;
        public bool DetailCapReached => surfaceDetailComparisonActive
            && surfaceDetailComparison.DetailCapReached;
        public int ActiveSurfaceVertexCount => CountActiveSurfaceMeshValues(countTriangles: false);
        public int ActiveSurfaceTriangleCount => CountActiveSurfaceMeshValues(countTriangles: true);
        public bool HasOverlappingSurfaceLevels
        {
            get
            {
                var visibleLevels = 0;
                if (reliefLayer != null && reliefLayer.activeSelf) visibleLevels++;
                if (regionalReliefTileRoot != null && regionalReliefTileRoot.activeSelf)
                    visibleLevels++;
                if (localReliefTileRoot != null && localReliefTileRoot.activeSelf)
                    visibleLevels++;
                return visibleLevels > 1;
            }
        }
        public 한반도지형미리보기Catalog Catalog => catalog;

        public void Initialize(세계지구본Catalog worldCatalog, Canvas interfaceCanvas,
            Transform globeVisualRoot)
        {
            if (initialized) return;
            if (worldCatalog == null) throw new ArgumentNullException(nameof(worldCatalog));
            if (interfaceCanvas == null) throw new ArgumentNullException(nameof(interfaceCanvas));
            if (globeVisualRoot == null) throw new ArgumentNullException(nameof(globeVisualRoot));

            var manifest = Resources.Load<TextAsset>(한반도지형미리보기Catalog.ResourceName)
                ?? throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewManifestMissing");
            catalog = JsonUtility.FromJson<한반도지형미리보기Catalog>(manifest.text)
                ?? throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewManifestInvalid");
            catalog.Validate();
            this.worldCatalog = worldCatalog;

            var terrainTexture = Resources.Load<Texture2D>(catalog.textureResourceName)
                ?? throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewTextureMissing");
            var riverTexture = Resources.Load<Texture2D>(catalog.riverTextureResourceName)
                ?? throw new InvalidOperationException("KoreanPeninsulaRiverPreviewTextureMissing");

            previewRoot = new GameObject(RootName);
            previewRoot.transform.SetParent(globeVisualRoot, false);
            previewRoot.layer = GlobeLayer;

            BuildCurvedSurface(terrainTexture, riverTexture);
            BuildCoastline(worldCatalog);
            BuildHubReadinessLayer();
            BuildInformationPanel(interfaceCanvas.transform);
            initialized = true;
            SetZoomBlend(0f);
            SetStage(한반도지형미리보기단계.Hidden);
        }

        public void SetStage(한반도지형미리보기단계 value)
        {
            if (!initialized) return;
            stage = value;
            var visible = value != 한반도지형미리보기단계.Hidden;
            previewRoot.SetActive(visible);
            informationPanel.SetActive(visible);
            coastlineLayer.SetActive(visible);
            ApplySurfaceDetail(value);
            riverLayer.SetActive(PhotoSurfaceEnabled && value >= 한반도지형미리보기단계.Rivers);
            hubReadinessLayer.SetActive(value >= 한반도지형미리보기단계.Hubs);
            if (visible) UpdateStageLabel(value);
        }

        /// <summary>
        /// 의미 자료층은 그대로 두고 현재 Mesh 표본 간격에 따른 비교 상세도만 적용한다.
        /// 정식 지리 타일 streaming이나 실제 DEM 오차 선택으로 해석하지 않는다.
        /// </summary>
        public void ApplySurfaceDetailComparison(
            한반도지형타일화면오차선택결과 selection)
        {
            if (!initialized)
                throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewNotInitialized");
            if (selection.DetailLevel is < 0 or > 2)
                throw new ArgumentOutOfRangeException(nameof(selection));

            surfaceDetailComparisonActive = true;
            surfaceDetailComparison = selection;
            ApplySurfaceDetail(stage);
            if (stage != 한반도지형미리보기단계.Hidden) UpdateStageLabel(stage);
        }

        public void ClearSurfaceDetailComparison()
        {
            if (!initialized) return;
            surfaceDetailComparisonActive = false;
            surfaceDetailComparison = default;
            ApplySurfaceDetail(stage);
            if (stage != 한반도지형미리보기단계.Hidden) UpdateStageLabel(stage);
        }

        /// <summary>
        /// 마우스 휠 확대량에 따라 같은 지구 곡면의 자료층 투명도만 연속 조절한다.
        /// 이 값은 카메라·운영·Simulation 상태를 변경하지 않는다.
        /// </summary>
        public void SetZoomBlend(float value)
        {
            zoomBlend = Mathf.Clamp01(value);
            SetMaterialAlpha(coastlineMaterial,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .25f, zoomBlend)));
            SetMaterialAlpha(reliefMaterial,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .52f, zoomBlend)));
            SetMaterialAlpha(riverMaterial,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.52f, .82f, zoomBlend)));
        }

        public void MarkEditorPreviewTransient()
        {
            MarkHierarchyTransient(previewRoot);
            MarkHierarchyTransient(informationPanel);
            hideFlags |= HideFlags.DontSaveInEditor;
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(previewRoot);
            DestroyGeneratedObject(informationPanel);
            DestroyRuntimeMaterial(reliefMaterial);
            DestroyRuntimeMaterial(riverMaterial);
            DestroyRuntimeMaterial(coastlineMaterial);
        }

        private void BuildCurvedSurface(Texture2D terrainTexture, Texture2D riverTexture)
        {
            reliefMaterial = CreateUnlitMaterial(Color.white, terrainTexture, true);
            reliefLayer = CreateCurvedPatch("NaturalEarthReliefLayer", reliefMaterial,
                ReliefRadius, catalog.bounds, ParentLatitudeSegments,
                ParentLongitudeSegments, true);
            regionalReliefTileRoot = BuildCurvedTileLevel("KoreanPeninsulaSurfaceTiles.L1",
                reliefMaterial, ReliefRadius, RegionalTilesPerAxis,
                RegionalLatitudeSegments, RegionalLongitudeSegments);
            localReliefTileRoot = BuildCurvedTileLevel("KoreanPeninsulaSurfaceTiles.L2",
                reliefMaterial, ReliefRadius, LocalTilesPerAxis,
                LocalLatitudeSegments, LocalLongitudeSegments);

            riverMaterial = CreateUnlitMaterial(Color.white, riverTexture, true);
            riverLayer = CreateCurvedPatch("VerifiedRiverLayer", riverMaterial,
                RiverRadius, catalog.bounds, ParentLatitudeSegments * 2,
                ParentLongitudeSegments * 2, false);
        }

        private void BuildCoastline(세계지구본Catalog worldCatalog)
        {
            coastlineLayer = new GameObject("VerifiedCoastlineLayer");
            coastlineLayer.transform.SetParent(previewRoot.transform, false);
            coastlineLayer.layer = GlobeLayer;
            coastlineMaterial = CreateUnlitMaterial(new Color(1f, .82f, .30f, .95f), null, true);

            foreach (var iso2 in new[] { "KR", "KP" })
            {
                var country = FindCountry(worldCatalog, iso2);
                if (country?.rings == null) continue;
                foreach (var ring in country.rings)
                    BuildProjectedRing(country.stableId + "." + ring.polygonIndex, ring);
            }
        }

        private void BuildProjectedRing(string name, 세계국가경계Ring ring)
        {
            if (ring?.points == null || ring.points.Length < 2) return;
            var item = new GameObject(name, typeof(LineRenderer));
            item.transform.SetParent(coastlineLayer.transform, false);
            item.layer = GlobeLayer;
            var line = item.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = .055f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = coastlineMaterial;
            line.positionCount = ring.points.Length;
            for (var index = 0; index < ring.points.Length; index++)
            {
                var point = ring.points[index];
                line.SetPosition(index, ToSpherePoint(point.latitude, point.longitude,
                    CoastlineRadius));
            }
        }

        private void BuildHubReadinessLayer()
        {
            hubReadinessLayer = new GameObject("HubEvidenceReadinessLayer");
            hubReadinessLayer.transform.SetParent(previewRoot.transform, false);
            hubReadinessLayer.layer = GlobeLayer;

            // 근거 좌표가 결속되기 전에는 곡면 위에 거점이나 사각 프레임을 임의로 만들지 않는다.
        }

        private void BuildInformationPanel(Transform canvas)
        {
            informationPanel = new GameObject(InformationPanelName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            informationPanel.transform.SetParent(canvas, false);
            var rect = informationPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(18f, -18f);
            rect.sizeDelta = new Vector2(420f, 172f);
            informationPanel.GetComponent<Image>().color = new Color(.015f, .05f, .085f, .93f);

            var title = Label(informationPanel.transform, "Title", 24,
                new Vector2(16f, -12f), new Vector2(388f, 34f), Color.white);
            title.text = "한반도 공간 관찰 · 경계 우선";
            stageLabel = Label(informationPanel.transform, "Stage", 16,
                new Vector2(16f, -52f), new Vector2(388f, 48f), new Color(.98f, .84f, .38f));
            var notice = Label(informationPanel.transform, "Notice", 13,
                new Vector2(16f, -104f), new Vector2(388f, 58f), new Color(.78f, .88f, .94f));
            notice.text = "사진형 지형 표시 제외 · 해안 윤곽 유지\n"
                + "전국 행정 경계는 자료 준비 후 연결 · 법적 경계 아님";
        }

        private void UpdateStageLabel(한반도지형미리보기단계 value)
        {
            if (!PhotoSurfaceEnabled)
            {
                stageLabel.text = "해안 윤곽 · 시도 → 시군구 → 읍면동 준비\n사진형 지형·하천 이미지 숨김";
                return;
            }
            var semanticStage = value switch
            {
                한반도지형미리보기단계.Coastline => "1/4  해안 윤곽 · KR/KP 경계 사본",
                한반도지형미리보기단계.Relief => "2/4  한반도 상위 곡면 타일 1개",
                한반도지형미리보기단계.Rivers => "3/4  권역 타일 4개 + 검증 하천 3개",
                한반도지형미리보기단계.Hubs => "4/4  근접 타일 16개 · 임의 거점 없음",
                _ => string.Empty,
            };
            if (!surfaceDetailComparisonActive
                || value < 한반도지형미리보기단계.Relief)
            {
                stageLabel.text = semanticStage;
                return;
            }

            var cap = surfaceDetailComparison.DetailCapReached
                ? " · 현재 Mesh 상세 상한"
                : string.Empty;
            stageLabel.text = semanticStage + "\n"
                + $"표본 간격 기반 SSE {surfaceDetailComparison.TargetSsePixels:0.#}px"
                + $" → L{surfaceDetailComparison.DetailLevel}"
                + $" · {surfaceDetailComparison.ActiveTileCount}타일"
                + $" · 추정 {surfaceDetailComparison.EstimatedSsePixels:0.00}px{cap}";
        }

        private void ApplySurfaceDetail(한반도지형미리보기단계 value)
        {
            var surfaceVisible = PhotoSurfaceEnabled && value >= 한반도지형미리보기단계.Relief;
            var detailLevel = surfaceDetailComparisonActive
                ? surfaceDetailComparison.DetailLevel
                : value switch
                {
                    한반도지형미리보기단계.Relief => 0,
                    한반도지형미리보기단계.Rivers => 1,
                    한반도지형미리보기단계.Hubs => 2,
                    _ => -1,
                };
            reliefLayer.SetActive(surfaceVisible && detailLevel == 0);
            regionalReliefTileRoot.SetActive(surfaceVisible && detailLevel == 1);
            localReliefTileRoot.SetActive(surfaceVisible && detailLevel == 2);
        }

        private int CountActiveSurfaceMeshValues(bool countTriangles)
        {
            var root = SurfaceDetailLevel switch
            {
                0 => reliefLayer,
                1 => regionalReliefTileRoot,
                2 => localReliefTileRoot,
                _ => null,
            };
            if (root == null || !root.activeInHierarchy) return 0;

            var total = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                total += countTriangles ? mesh.triangles.Length / 3 : mesh.vertexCount;
            }
            return total;
        }

        private GameObject BuildCurvedTileLevel(string name, Material material, float radius,
            int tilesPerAxis, int latitudeSegments, int longitudeSegments)
        {
            var root = new GameObject(name);
            root.transform.SetParent(previewRoot.transform, false);
            root.layer = GlobeLayer;

            for (var latitudeTile = 0; latitudeTile < tilesPerAxis; latitudeTile++)
            for (var longitudeTile = 0; longitudeTile < tilesPerAxis; longitudeTile++)
            {
                var latitudeMinT = latitudeTile / (double)tilesPerAxis;
                var latitudeMaxT = (latitudeTile + 1d) / tilesPerAxis;
                var longitudeMinT = longitudeTile / (double)tilesPerAxis;
                var longitudeMaxT = (longitudeTile + 1d) / tilesPerAxis;
                var bounds = new 한반도지형미리보기Bounds
                {
                    minLatitude = Lerp(catalog.bounds.minLatitude,
                        catalog.bounds.maxLatitude, latitudeMinT),
                    maxLatitude = Lerp(catalog.bounds.minLatitude,
                        catalog.bounds.maxLatitude, latitudeMaxT),
                    minLongitude = Lerp(catalog.bounds.minLongitude,
                        catalog.bounds.maxLongitude, longitudeMinT),
                    maxLongitude = Lerp(catalog.bounds.minLongitude,
                        catalog.bounds.maxLongitude, longitudeMaxT),
                };
                var tileName = $"Tile.{latitudeTile}.{longitudeTile}";
                var tile = CreateCurvedPatch(tileName, material, radius, bounds,
                    latitudeSegments, longitudeSegments, true);
                tile.transform.SetParent(root.transform, false);
            }

            return root;
        }

        private GameObject CreateCurvedPatch(string name, Material material, float radius,
            한반도지형미리보기Bounds bounds, int latitudeSegments,
            int longitudeSegments, bool clipToPeninsulaLand)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(previewRoot.transform, false);
            item.layer = GlobeLayer;
            item.GetComponent<MeshFilter>().sharedMesh = BuildCurvedPatchMesh(name, radius,
                bounds, latitudeSegments, longitudeSegments, clipToPeninsulaLand);
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        private Mesh BuildCurvedPatchMesh(string name, float radius,
            한반도지형미리보기Bounds bounds, int latitudeSegments,
            int longitudeSegments, bool clipToPeninsulaLand)
        {
            var vertices = new List<Vector3>((latitudeSegments + 1) * (longitudeSegments + 1));
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(latitudeSegments * longitudeSegments * 6);

            for (var latitudeIndex = 0; latitudeIndex <= latitudeSegments; latitudeIndex++)
            {
                var localV = latitudeIndex / (float)latitudeSegments;
                var latitude = Mathf.Lerp((float)bounds.minLatitude,
                    (float)bounds.maxLatitude, localV);
                for (var longitudeIndex = 0; longitudeIndex <= longitudeSegments;
                     longitudeIndex++)
                {
                    var localU = longitudeIndex / (float)longitudeSegments;
                    var longitude = Mathf.Lerp((float)bounds.minLongitude,
                        (float)bounds.maxLongitude, localU);
                    vertices.Add(ToSpherePoint(latitude, longitude, radius));
                    uvs.Add(new Vector2(
                        Mathf.InverseLerp((float)catalog.bounds.minLongitude,
                            (float)catalog.bounds.maxLongitude, longitude),
                        Mathf.InverseLerp((float)catalog.bounds.minLatitude,
                            (float)catalog.bounds.maxLatitude, latitude)));
                }
            }

            var row = longitudeSegments + 1;
            for (var latitudeIndex = 0; latitudeIndex < latitudeSegments; latitudeIndex++)
            for (var longitudeIndex = 0; longitudeIndex < longitudeSegments; longitudeIndex++)
            {
                var latitude = Mathf.Lerp((float)bounds.minLatitude,
                    (float)bounds.maxLatitude, (latitudeIndex + .5f) / latitudeSegments);
                var longitude = Mathf.Lerp((float)bounds.minLongitude,
                    (float)bounds.maxLongitude, (longitudeIndex + .5f) / longitudeSegments);
                if (clipToPeninsulaLand && !IsPeninsulaLandCell(latitude, longitude))
                    continue;

                var lowerLeft = latitudeIndex * row + longitudeIndex;
                var upperLeft = lowerLeft + row;
                triangles.Add(lowerLeft);
                triangles.Add(upperLeft);
                triangles.Add(upperLeft + 1);
                triangles.Add(lowerLeft);
                triangles.Add(upperLeft + 1);
                triangles.Add(lowerLeft + 1);
            }

            var mesh = new Mesh { name = name + "CurvedMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private bool IsPeninsulaLandCell(double latitude, double longitude)
        {
            var country = worldCatalog.FindAt(latitude, longitude);
            return country != null
                && (string.Equals(country.iso2, "KR", StringComparison.Ordinal)
                    || string.Equals(country.iso2, "KP", StringComparison.Ordinal));
        }

        private static double Lerp(double minimum, double maximum, double value)
        {
            return minimum + (maximum - minimum) * value;
        }

        private static Vector3 ToSpherePoint(double latitude, double longitude, float radius)
        {
            var point = 세계지구구면좌표.FromLatitudeLongitude(latitude, longitude);
            return new Vector3(-(float)point.X, (float)point.Y, (float)point.Z) * radius;
        }

        private static 세계국가관찰자료 FindCountry(세계지구본Catalog worldCatalog, string iso2)
        {
            if (worldCatalog.countries == null) return null;
            foreach (var country in worldCatalog.countries)
                if (string.Equals(country.iso2, iso2, StringComparison.Ordinal)) return country;
            return null;
        }

        private static Material CreateUnlitMaterial(Color color, Texture texture = null,
            bool transparent = false)
        {
            var shader = transparent
                ? Shader.Find("Sprites/Default")
                : Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Texture");
            shader ??= Shader.Find("Sprites/Default")
                ?? throw new InvalidOperationException("KoreanPeninsulaTerrainPreviewShaderMissing");
            var material = new Material(shader) { color = color, mainTexture = texture };
            if (texture != null && material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (!transparent) return material;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null) return;
            var color = material.color;
            color.a = Mathf.Clamp01(alpha);
            material.color = color;
        }

        private static Text Label(Transform parent, string name, int fontSize,
            Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = item.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.UpperLeft;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void MarkHierarchyTransient(GameObject root)
        {
            if (root == null) return;
            root.hideFlags |= HideFlags.DontSaveInEditor;
            foreach (Transform child in root.transform)
                MarkHierarchyTransient(child.gameObject);
        }

        private static void DestroyGeneratedObject(GameObject value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
