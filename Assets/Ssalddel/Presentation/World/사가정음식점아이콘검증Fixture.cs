using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [Serializable]
    public sealed class 사가정음식점아이콘FixtureData
    {
        public string schema = string.Empty;
        public string fixtureRevision = string.Empty;
        public string sourceKindCode = string.Empty;
        public bool distributionApproved;
        public bool operationalEffectsAllowed;
        public string disclosure = string.Empty;
        public 사가정음식점아이콘FixtureEntry[] entries =
            Array.Empty<사가정음식점아이콘FixtureEntry>();
    }

    [Serializable]
    public sealed class 사가정음식점아이콘FixtureEntry
    {
        public string stableId = string.Empty;
        public string categoryCode = string.Empty;
        public float unityX;
        public float unityZ;
        public string[] sampleMenuDisplayNames = Array.Empty<string>();
    }

    public sealed class 사가정음식점아이콘FixtureResult
    {
        public 사가정음식점아이콘FixtureData Data { get; set; }
        public string ContentHashSha256 { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Unity Temp 아래의 비추적 JSON만 읽습니다. 실상호·주소·전화 필드는 원문 단계에서 거부합니다.
    /// </summary>
    public static class 사가정음식점아이콘FixtureLoader
    {
        public const string Schema = "sagajeong-restaurant-icon-fixture.v1";
        public const string EnvironmentVariable = "SSALDDEL_SAGAJEONG_RESTAURANT_FIXTURE";
        public const string DefaultFileName = "sagajeong-restaurant-icons.local.json";
        public const string RequiredSourceKind = "LocalPrivateReview";
        public const string RequiredDisclosure =
            "샘플 메뉴 · 실제 판매 메뉴 아님 · 제휴 관계 없음 · 로컬 검토용/비배포";
        private const int MaximumBytes = 256 * 1024;

        private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
        {
            "Korean", "Snack", "CafeBakery", "Chicken", "Grill", "Soup", "Pub", "Other"
        };

        private static readonly string[] ForbiddenJsonFields =
        {
            "\"businessName\"", "\"restaurantName\"", "\"displayName\"",
            "\"address\"", "\"roadAddress\"", "\"lotAddress\"", "\"phone\"",
            "\"telephone\"", "\"latitude\"", "\"longitude\"", "\"상호\"",
            "\"주소\"", "\"전화\""
        };

        public static string DefaultPath()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Temp", DefaultFileName);
        }

        public static bool TryLoad(out 사가정음식점아이콘FixtureResult result,
            out string statusCode, string requestedPath = null)
        {
            result = null;
            var configured = requestedPath;
            if (string.IsNullOrWhiteSpace(configured))
                configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
            var path = !string.IsNullOrWhiteSpace(configured) ? configured : DefaultPath();
            if (!File.Exists(path))
            {
                statusCode = 다중Os생명주기검증Policy.RestaurantIconInputStatus;
                return false;
            }

            var fullPath = ValidatePath(path);
            var bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length == 0 || bytes.Length > MaximumBytes)
                throw new FormatException("RestaurantIconFixtureSizeInvalid");
            var json = new UTF8Encoding(false, true).GetString(bytes);
            if (ForbiddenJsonFields.Any(value =>
                    json.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0))
                throw new FormatException("RestaurantIconFixturePrivateFieldForbidden");
            var data = JsonUtility.FromJson<사가정음식점아이콘FixtureData>(json)
                       ?? throw new FormatException("RestaurantIconFixtureJsonInvalid");
            Validate(data, LoadMap());
            result = new 사가정음식점아이콘FixtureResult
            {
                Data = data,
                ContentHashSha256 = Hash(bytes),
                SourcePath = fullPath
            };
            statusCode = "restaurant-icon fixture ready:" + data.entries.Length;
            return true;
        }

        public static void Validate(사가정음식점아이콘FixtureData data, 공간참고자료 map)
        {
            if (data == null || map == null)
                throw new ArgumentException("RestaurantIconFixtureMissing");
            if (!string.Equals(data.schema, Schema, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(data.fixtureRevision)
                || !string.Equals(data.sourceKindCode, RequiredSourceKind, StringComparison.Ordinal)
                || data.distributionApproved || data.operationalEffectsAllowed
                || !string.Equals(data.disclosure, RequiredDisclosure, StringComparison.Ordinal))
                throw new FormatException("RestaurantIconFixturePolicyInvalid");
            if (data.entries == null || data.entries.Length is < 1 or > 12)
                throw new FormatException("RestaurantIconFixtureCountInvalid");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in data.entries)
            {
                if (entry == null || !ValidStableId(entry.stableId)
                                  || !ids.Add(entry.stableId))
                    throw new FormatException("RestaurantIconFixtureStableIdInvalid");
                if (!Categories.Contains(entry.categoryCode))
                    throw new FormatException("RestaurantIconFixtureCategoryInvalid");
                if (!float.IsFinite(entry.unityX) || !float.IsFinite(entry.unityZ)
                    || Math.Abs(entry.unityX - map.offsetX) > map.halfExtent
                    || Math.Abs(entry.unityZ - map.offsetZ) > map.halfExtent)
                    throw new FormatException("RestaurantIconFixtureCoordinateInvalid");
                if (entry.sampleMenuDisplayNames == null
                    || entry.sampleMenuDisplayNames.Length is < 2 or > 3
                    || entry.sampleMenuDisplayNames.Any(value =>
                        string.IsNullOrWhiteSpace(value) || value.Length > 48
                        || !value.StartsWith("[샘플] ", StringComparison.Ordinal)
                        || value.IndexOfAny(new[] { '\r', '\n' }) >= 0))
                    throw new FormatException("RestaurantIconFixtureSampleMenuInvalid");
            }
        }

        public static string CategoryLabel(string code)
            => code switch
            {
                "Korean" => "한식",
                "Snack" => "분식",
                "CafeBakery" => "카페·제과",
                "Chicken" => "치킨",
                "Grill" => "구이",
                "Soup" => "국·탕",
                "Pub" => "주점",
                _ => "기타 음식점"
            };

        private static 공간참고자료 LoadMap()
        {
            var map = Resources.Load<TextAsset>("SagajeongReference")
                      ?? throw new FormatException("RestaurantIconFixtureMapMissing");
            return JsonUtility.FromJson<공간참고자료>(map.text)
                   ?? throw new FormatException("RestaurantIconFixtureMapInvalid");
        }

        private static string ValidatePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value)
                || !string.Equals(Path.GetExtension(value), ".json",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("RestaurantIconFixturePathInvalid");
            var full = Path.GetFullPath(value);
            var temp = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Temp"))
                       + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("RestaurantIconFixtureMustBeUnderUnityTemp");
            return full;
        }

        private static bool ValidStableId(string value)
            => !string.IsNullOrWhiteSpace(value) && value.Length <= 128
               && value.All(character => IsAsciiLetterOrDigit(character)
                                         || character is ':' or '-' or '_' or '.');

        private static bool IsAsciiLetterOrDigit(char value)
            => value is >= 'a' and <= 'z'
               || value is >= 'A' and <= 'Z'
               || value is >= '0' and <= '9';

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes))
                .Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    [DisallowMultipleComponent]
    public sealed class 사가정음식점아이콘검증Layer : MonoBehaviour
    {
        public const string RootName = "RestaurantIconsRuntime";
        private readonly Dictionary<string, 사가정음식점아이콘View> icons =
            new(StringComparer.Ordinal);
        private Transform runtimeRoot;
        private Camera observationCamera;
        private 사가정음식점아이콘FixtureData data;
        private string selectedStableId = string.Empty;
        private GUIStyle cardStyle;
        private GUIStyle noteStyle;

        public string StatusCode { get; private set; }
            = 다중Os생명주기검증Policy.RestaurantIconInputStatus;
        public string ContentHashSha256 { get; private set; } = string.Empty;
        public int IconCount => icons.Count;
        public string SelectedStableId => selectedStableId;

        public void Configure(Transform parent, Camera camera)
        {
            runtimeRoot = parent != null
                ? parent.Find(RootName) ?? CreateRoot(parent)
                : throw new ArgumentNullException(nameof(parent));
            observationCamera = camera;
        }

        public bool TryLoadLocalFixture(string requestedPath = null)
        {
            if (!사가정음식점아이콘FixtureLoader.TryLoad(out var loaded,
                    out var status, requestedPath))
            {
                ResetToStatus(status);
                return false;
            }
            Apply(loaded);
            return true;
        }

        public void Apply(사가정음식점아이콘FixtureResult fixture)
        {
            if (fixture?.Data == null)
                throw new ArgumentNullException(nameof(fixture));
            if (runtimeRoot == null)
                throw new InvalidOperationException("RestaurantIconRuntimeRootMissing");
            ClearIcons();
            data = fixture.Data;
            ContentHashSha256 = fixture.ContentHashSha256;
            foreach (var entry in data.entries.OrderBy(value => value.stableId,
                         StringComparer.Ordinal))
            {
                var root = new GameObject("RestaurantIcon__" + SafeName(entry.stableId))
                {
                    hideFlags = 다중Os생명주기검증Layer.RuntimeHideFlags,
                    layer = 사가정운영디오라마View.ObservationLayer
                };
                root.transform.SetParent(runtimeRoot, false);
                var icon = root.AddComponent<사가정음식점아이콘View>();
                icon.Apply(entry, observationCamera);
                icons.Add(entry.stableId, icon);
            }
            selectedStableId = data.entries.OrderBy(value => value.stableId,
                StringComparer.Ordinal).First().stableId;
            StatusCode = "restaurant-icon fixture ready:" + data.entries.Length;
        }

        public void Reject(string reasonCode)
        {
            var safeReason = string.IsNullOrWhiteSpace(reasonCode)
                ? "RestaurantIconFixtureRejected" : reasonCode;
            ResetToStatus("restaurant-icon fixture rejected:" + safeReason);
        }

        public bool Select(string stableId)
        {
            if (!icons.ContainsKey(stableId)) return false;
            selectedStableId = stableId;
            return true;
        }

        public void Clear()
        {
            ClearIcons();
            if (runtimeRoot != null)
                다중Os생명주기검증Layer.DestroyRuntimeObject(runtimeRoot.gameObject);
            runtimeRoot = null;
            data = null;
            ContentHashSha256 = string.Empty;
            StatusCode = 다중Os생명주기검증Policy.RestaurantIconInputStatus;
        }

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying || data == null
                                                   || observationCamera == null) return;
            EnsureStyles();
            GUI.depth = -132;
            foreach (var pair in icons)
            {
                var point = observationCamera.WorldToScreenPoint(pair.Value.transform.position
                                                                 + Vector3.up * 12f);
                if (point.z <= 0f) continue;
                var rect = new Rect(point.x - 17f, Screen.height - point.y - 17f, 34f, 34f);
                if (GUI.Button(rect, pair.Value.CategoryGlyph)) Select(pair.Key);
            }

            var selected = data.entries.FirstOrDefault(value =>
                string.Equals(value.stableId, selectedStableId, StringComparison.Ordinal));
            if (selected == null) return;
            var width = Mathf.Min(340f, Screen.width - 24f);
            var x = Screen.width - width - 12f;
            var y = Mathf.Max(230f, Screen.height - 238f);
            GUI.Box(new Rect(x, y, width, 224f), GUIContent.none);
            GUI.Label(new Rect(x + 12f, y + 10f, width - 24f, 24f),
                "검토용 음식점 아이콘 · "
                + 사가정음식점아이콘FixtureLoader.CategoryLabel(selected.categoryCode), cardStyle);
            GUI.Label(new Rect(x + 12f, y + 39f, width - 24f, 92f),
                string.Join("\n", selected.sampleMenuDisplayNames), cardStyle);
            GUI.Label(new Rect(x + 12f, y + 137f, width - 24f, 72f),
                data.disclosure + "\n실상호·주소·전화·실메뉴는 이 입력에 포함되지 않습니다.",
                noteStyle);
        }

        private void ClearIcons()
        {
            foreach (var icon in icons.Values.Where(value => value != null).ToArray())
                다중Os생명주기검증Layer.DestroyRuntimeObject(icon.gameObject);
            icons.Clear();
            selectedStableId = string.Empty;
        }

        private void ResetToStatus(string status)
        {
            ClearIcons();
            data = null;
            ContentHashSha256 = string.Empty;
            StatusCode = status;
        }

        private static Transform CreateRoot(Transform parent)
        {
            var value = new GameObject(RootName)
            {
                hideFlags = 다중Os생명주기검증Layer.RuntimeHideFlags,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            value.transform.SetParent(parent, false);
            return value.transform;
        }

        private void EnsureStyles()
        {
            if (cardStyle != null) return;
            cardStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            noteStyle = new GUIStyle(cardStyle)
            {
                fontSize = 11,
                normal = { textColor = new Color(1f, .78f, .42f) }
            };
        }

        private static string SafeName(string value)
            => new(value.Select(character => char.IsLetterOrDigit(character)
                ? character : '_').ToArray());
    }

    [DisallowMultipleComponent]
    public sealed class 사가정음식점아이콘View : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string categoryCode = string.Empty;
        private Camera observationCamera;

        public string StableId => stableId;
        public string CategoryCode => categoryCode;
        public string CategoryGlyph => categoryCode switch
        {
            "CafeBakery" => "C",
            "Chicken" => "K",
            "Grill" => "G",
            "Soup" => "S",
            "Pub" => "P",
            "Snack" => "B",
            "Korean" => "H",
            _ => "R"
        };

        public void Apply(사가정음식점아이콘FixtureEntry entry, Camera camera)
        {
            stableId = entry.stableId;
            categoryCode = entry.categoryCode;
            observationCamera = camera;
            transform.localPosition = new Vector3(entry.unityX, 24f, entry.unityZ);
            transform.localRotation = Quaternion.identity;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "음식점 카테고리 아이콘";
            marker.hideFlags = 다중Os생명주기검증Layer.RuntimeHideFlags;
            marker.layer = 사가정운영디오라마View.ObservationLayer;
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(4.5f, 2f, 4.5f);
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
                다중Os생명주기검증Layer.DestroyRuntimeObject(collider);
            var renderer = marker.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", CategoryColor(categoryCode));
            properties.SetColor("_Color", CategoryColor(categoryCode));
            renderer.SetPropertyBlock(properties);

            var textObject = new GameObject("카테고리 문자")
            {
                hideFlags = 다중Os생명주기검증Layer.RuntimeHideFlags,
                layer = 사가정운영디오라마View.ObservationLayer
            };
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            var text = textObject.AddComponent<TextMesh>();
            text.text = CategoryGlyph;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = .2f;
            text.color = Color.white;
        }

        private void Update()
        {
            if (observationCamera == null || transform.childCount < 2) return;
            transform.GetChild(1).rotation = observationCamera.transform.rotation;
        }

        private static Color CategoryColor(string code)
            => code switch
            {
                "CafeBakery" => new Color(.35f, .72f, .94f),
                "Chicken" => new Color(.95f, .55f, .16f),
                "Grill" => new Color(.85f, .3f, .2f),
                "Soup" => new Color(.3f, .72f, .48f),
                "Pub" => new Color(.66f, .4f, .9f),
                "Snack" => new Color(.96f, .72f, .2f),
                "Korean" => new Color(.4f, .78f, .58f),
                _ => new Color(.72f, .72f, .72f)
            };
    }
}
