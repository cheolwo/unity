using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    /// <summary>고정한 CC0 일반 표면만 읽어 비공개 상세 모델의 메모리 재질을 만든다.</summary>
    public sealed class 사가정상세표면MaterialLibrary : IDisposable
    {
        public const string ReceiptSchema = "private-detailed-building-material-receipt.v1";
        public const string SurfaceMeaning = "GenericPhotographicSurfaceAssetsNotScansOfObservedStores";
        private const long MaximumMapBytes = 6 * 1024 * 1024;
        private static readonly string[] AssetIds = { "brick_wall_001", "concrete_wall_003", "asphalt_02" };
        private static readonly string[] MapRoles = { "Diffuse", "nor_gl", "Rough" };
        private static readonly HashSet<string> PlainRoles = new(StringComparer.Ordinal)
        { "Glass", "TransparentGlass", "NeutralBackdrop", "Frame", "Trim", "Sign", "SignBoard", "Awning", "Threshold", "Mortar" };

        [Serializable]
        public sealed class MapReport
        {
            public string assetId, role, path, sha256;
            public int width, height;
            public long bytes;
            public bool linear, hashVerified, sourceMd5Verified, metadataMatched;
        }
        [Serializable]
        public sealed class Report
        {
            public string schema = "private-detailed-building-material-library-report.v1";
            public string receiptPath, receiptSha256, provider = "Poly Haven", license = "CC0";
            public string surfaceMeaning = SurfaceMeaning;
            public string normalConvention = "OpenGLRgbNormalWithUrpNormalKeyword";
            public string smoothnessPacking = "MetallicZeroAlphaOneMinusLinearRoughnessRed";
            public bool actualStoreScanClaimed, originalAssetsModified, sceneSaved;
            public int sourceAssetCount, sourceMapCount, sourceDocumentCount, materialCount, textureCount;
            public int transparentGlassMaterialCount, neutralBackdropMaterialCount;
            public string transparentGlassMeaning = "UrpLitAlphaPreservedSpecularViewAngleFresnel";
            public bool neutralBackdropNotObservedInterior;
            public bool actualOneKDimensionsVerified, disposed, ownedObjectsDestroyed;
            public MapReport[] maps;
        }
        private sealed class Surface
        { public Texture2D Diffuse, Normal, Roughness, MetallicSmoothness; }
        private sealed class Pin
        { public string Path, Hash; public long Length, Maximum; }

        private readonly string directory;
        private readonly Shader shader;
        private readonly Dictionary<string, Surface> surfaces = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> materials = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Pin> pins = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Object> owned = new();
        public Report Result { get; } = new();

        public 사가정상세표면MaterialLibrary(string receiptPath, string receiptSha256)
        {
            if (string.IsNullOrWhiteSpace(receiptPath) || !Path.IsPathRooted(receiptPath)
                || !receiptPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !Sha(receiptSha256))
                throw new ArgumentException("DetailedSurfacePinnedReceiptRequired");
            directory = Path.GetDirectoryName(Path.GetFullPath(receiptPath));
            Result.receiptPath = Path.GetFullPath(receiptPath); Result.receiptSha256 = receiptSha256.ToLowerInvariant();
            try
            {
                var input = JObject.Parse(Encoding.UTF8.GetString(ReadPinned(receiptPath, receiptSha256, 128 * 1024)).TrimStart('\uFEFF'));
                ValidateReceiptJson(input.ToString());
                var sourceTerms = (JArray)input["sourceTerms"];
                foreach (var term in sourceTerms) ReadReference(term["document"], 256 * 1024);
                Result.sourceDocumentCount = sourceTerms.Count;
                shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !shader.isSupported)
                    throw new InvalidOperationException("DetailedSurfaceUrpLitRequired");
                var verified = new List<MapReport>();
                foreach (JObject asset in (JArray)input["assets"])
                {
                    var id = asset.Value<string>("assetId");
                    var metadata = JObject.Parse(Encoding.UTF8.GetString(ReadReference(asset["metadata"], 256 * 1024)).TrimStart('\uFEFF'));
                    var maps = (JObject)asset["maps"];
                    var surface = new Surface();
                    foreach (var role in MapRoles)
                    {
                        var map = (JObject)maps[role];
                        var format = Path.GetExtension(map.Value<string>("path")).TrimStart('.').ToLowerInvariant();
                        var source = metadata[role]?["1k"]?[format];
                        if (source == null || source.Value<long?>("size") != map.Value<long?>("bytes")
                            || source.Value<string>("url") != map.Value<string>("url")
                            || !string.Equals(source.Value<string>("md5"), map.Value<string>("sourceMd5"), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("DetailedSurfaceSourceMetadataMismatch");
                        var bytes = ReadPinned(map.Value<string>("path"), map.Value<string>("sha256"), MaximumMapBytes, map.Value<long>("bytes"));
                        if (Hash(bytes, true) != map.Value<string>("sourceMd5").ToLowerInvariant())
                            throw new InvalidOperationException("DetailedSurfaceSourceMd5Mismatch");
                        var dimensions = ReadImageDimensions(bytes);
                        if (dimensions.Width != 1024 || dimensions.Height != 1024)
                            throw new InvalidOperationException("DetailedSurfaceActualOneKImageRequired");
                        var linear = role != "Diffuse";
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear)
                        { name = "PrivateCc0:" + id + ":" + role, hideFlags = HideFlags.DontSave,
                            wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                        owned.Add(texture);
                        if (!ImageConversion.LoadImage(texture, bytes, false) || texture.width != 1024 || texture.height != 1024)
                            throw new InvalidOperationException("DetailedSurfaceDecodeMismatch");
                        texture.Apply(true, false);
                        if (role == "Diffuse") surface.Diffuse = texture;
                        else if (role == "nor_gl") surface.Normal = texture;
                        else surface.Roughness = texture;
                        verified.Add(new MapReport { assetId = id, role = role, path = map.Value<string>("path"),
                            sha256 = map.Value<string>("sha256"), width = texture.width, height = texture.height,
                            bytes = bytes.LongLength, linear = linear, hashVerified = true, sourceMd5Verified = true, metadataMatched = true });
                    }
                    surface.MetallicSmoothness = new Texture2D(1024, 1024, TextureFormat.RGBA32, true, true)
                    { name = "PrivateDerivedMetallicSmoothness:" + id, hideFlags = HideFlags.DontSave,
                        wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                    owned.Add(surface.MetallicSmoothness);
                    surface.MetallicSmoothness.SetPixels32(PackMetallicSmoothness(surface.Roughness.GetPixels32()));
                    surface.MetallicSmoothness.Apply(true, false); surfaces.Add(id, surface);
                }
                Result.maps = verified.ToArray(); Result.sourceAssetCount = surfaces.Count;
                Result.sourceMapCount = verified.Count; Result.textureCount = owned.OfType<Texture2D>().Count();
                Result.actualOneKDimensionsVerified = verified.Count == 9 && verified.All(m => m.width == 1024 && m.height == 1024);
            }
            catch { Dispose(); throw; }
        }

        public Material Create(string slug, string role, Color color, float smoothness)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(role)
                || !float.IsFinite(smoothness) || smoothness < 0 || smoothness > 1
                || !float.IsFinite(color.r) || !float.IsFinite(color.g) || !float.IsFinite(color.b) || !float.IsFinite(color.a))
                throw new ArgumentException("DetailedSurfaceRoleColorRequired");
            string assetId = role == "Wall" ? slug == "maekjuabi" ? "brick_wall_001" : "concrete_wall_003"
                : role == "LowerWall" || role == "Body" || role == "Roof" ? "concrete_wall_003"
                : role == "Road" ? "asphalt_02" : null;
            if (assetId == null && !PlainRoles.Contains(role)) throw new ArgumentException("DetailedSurfaceUnknownRole");
            if (role == "TransparentGlass" && (color.a <= 0 || color.a >= 1))
                throw new ArgumentException("DetailedTransparentGlassBoundedAlphaRequired");
            var key = slug + "|" + role + "|" + color.r.ToString("R", CultureInfo.InvariantCulture) + "|"
                + color.g.ToString("R", CultureInfo.InvariantCulture) + "|" + color.b.ToString("R", CultureInfo.InvariantCulture)
                + "|" + color.a.ToString("R", CultureInfo.InvariantCulture) + "|" + smoothness.ToString("R", CultureInfo.InvariantCulture);
            if (materials.TryGetValue(key, out var existing)) return existing;
            var material = new Material(shader) { name = "PrivateDetailedSurface:" + slug + ":" + role, hideFlags = HideFlags.DontSave };
            owned.Add(material); material.SetColor("_BaseColor", color); material.SetColor("_Color", color);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", smoothness);
            if (role == "TransparentGlass")
            {
                // 설치된 URP Lit의 Alpha + Preserve Specular와 동일하다. 실내 영상·가짜 반사 사진을 만들지 않는다.
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetFloat("_BlendModePreserveSpecular", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Back);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetShaderPassEnabled("ShadowCaster", false); material.SetShaderPassEnabled("DepthOnly", false);
                material.renderQueue = (int)RenderQueue.Transparent;
                Result.transparentGlassMaterialCount++;
            }
            if (role == "NeutralBackdrop")
            {
                Result.neutralBackdropMaterialCount++; Result.neutralBackdropNotObservedInterior = true;
            }
            if (assetId != null)
            {
                var surface = surfaces[assetId];
                material.SetTexture("_BaseMap", surface.Diffuse); material.SetTextureScale("_BaseMap", Vector2.one / 3f);
                material.SetTexture("_BumpMap", surface.Normal); material.SetFloat("_BumpScale", .5f); material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", surface.MetallicSmoothness);
                material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            materials.Add(key, material); Result.materialCount = materials.Count; return material;
        }

        public void Recheck()
        {
            ThrowIfDisposed();
            foreach (var pin in pins.Values.ToArray()) ReadPinned(pin.Path, pin.Hash, pin.Maximum, pin.Length);
        }
        public void Dispose()
        {
            if (Result.disposed) return;
            foreach (var value in owned.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
            Result.disposed = true; Result.ownedObjectsDestroyed = owned.All(value => value == null);
        }
        public string ReportJson() => JsonUtility.ToJson(Result);
        private void ThrowIfDisposed() { if (Result.disposed) throw new ObjectDisposedException(nameof(사가정상세표면MaterialLibrary)); }

        public static Color32[] PackMetallicSmoothness(Color32[] roughness)
        {
            if (roughness == null || roughness.Length == 0 || roughness.Length > 1024 * 1024)
                throw new ArgumentException("DetailedSurfaceRoughnessPixelBudgetRejected");
            return roughness.Select(value => new Color32(0, 0, 0, (byte)(255 - value.r))).ToArray();
        }

        public static void ValidateReceiptJson(string json)
        {
            var input = JObject.Parse(json);
            if (input.Value<string>("schema") != ReceiptSchema || input.Value<string>("provider") != "Poly Haven"
                || input.Value<string>("license") != "CC0" || input.Value<string>("surfaceMeaning") != SurfaceMeaning)
                throw new InvalidOperationException("DetailedSurfaceReceiptScopeRejected");
            if (input["assets"] is not JArray assets || assets.Count != 3
                || !assets.Values<JObject>().Select(a => a.Value<string>("assetId")).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(AssetIds.OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidOperationException("DetailedSurfaceThreeSourceAssetsRequired");
            if (input["sourceTerms"] is not JArray terms || terms.Count != 2
                || !terms.Values<JObject>().Select(t => t.Value<string>("url")).OrderBy(x => x, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "https://polyhaven.com/license", "https://polyhaven.com/our-api" }))
                throw new InvalidOperationException("DetailedSurfaceSourceTermsRequired");
            foreach (JObject term in terms) ValidateReference(term["document"]);
            foreach (JObject asset in assets)
            {
                var id = asset.Value<string>("assetId");
                if (asset.Value<string>("sourceUrl") != "https://polyhaven.com/a/" + id
                    || asset.Value<string>("apiUrl") != "https://api.polyhaven.com/files/" + id
                    || asset["maps"] is not JObject maps || maps.Count != 3
                    || MapRoles.Any(role => maps[role] is not JObject))
                    throw new InvalidOperationException("DetailedSurfaceSourceMapSetRequired");
                ValidateReference(asset["metadata"]);
                foreach (var role in MapRoles)
                {
                    var map = (JObject)maps[role]; ValidateReference(map);
                    var ext = Path.GetExtension(map.Value<string>("path"));
                    if (map["bytes"]?.Type != JTokenType.Integer || map.Value<long>("bytes") <= 0 || map.Value<long>("bytes") > MaximumMapBytes
                        || map["linear"]?.Type != JTokenType.Boolean || map.Value<bool>("linear") != (role != "Diffuse")
                        || !Hex(map.Value<string>("sourceMd5"), 32) || ext != ".jpg" && ext != ".png"
                        || !Uri.TryCreate(map.Value<string>("url"), UriKind.Absolute, out var url)
                        || url.Scheme != "https" || url.Host != "dl.polyhaven.org" || url.Query.Length != 0
                        || !url.AbsolutePath.StartsWith("/file/ph-assets/Textures/", StringComparison.Ordinal))
                        throw new InvalidOperationException("DetailedSurfaceMapReferenceRejected");
                }
            }
        }

        private byte[] ReadReference(JToken reference, long maximum)
        { ValidateReference(reference); return ReadPinned(reference.Value<string>("path"), reference.Value<string>("sha256"), maximum); }
        private byte[] ReadPinned(string path, string hash, long maximum, long? length = null)
        {
            if (!Path.IsPathRooted(path) || !Sha(hash)) throw new InvalidOperationException("DetailedSurfacePinnedFileRequired");
            path = Path.GetFullPath(path);
            if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("DetailedSurfaceFileOutsideReceiptDirectory");
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= 0 || file.Length > maximum || length.HasValue && file.Length != length
                || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("DetailedSurfaceFileBudgetRejected");
            for (var parent = file.Directory; parent != null && parent.FullName.StartsWith(directory, StringComparison.OrdinalIgnoreCase); parent = parent.Parent)
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("DetailedSurfaceReparseRejected");
            var bytes = File.ReadAllBytes(path);
            if (bytes.LongLength != file.Length || length.HasValue && bytes.LongLength != length
                || Hash(bytes) != hash.ToLowerInvariant()) throw new InvalidOperationException("DetailedSurfacePinnedFileChanged");
            pins[path] = new Pin { Path = path, Hash = hash, Length = bytes.LongLength, Maximum = maximum }; return bytes;
        }
        private static void ValidateReference(JToken reference)
        {
            if (reference is not JObject || string.IsNullOrWhiteSpace(reference.Value<string>("path"))
                || !Path.IsPathRooted(reference.Value<string>("path")) || !Sha(reference.Value<string>("sha256")))
                throw new InvalidOperationException("DetailedSurfacePinnedReferenceRequired");
        }
        private static bool Sha(string value) => Hex(value, 64);
        private static bool Hex(string value, int count) => value != null && value.Length == count
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
        private static string Hash(byte[] bytes, bool md5 = false)
        { using HashAlgorithm algorithm = md5 ? MD5.Create() : SHA256.Create(); return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        public static (int Width, int Height) ReadImageDimensions(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 24) throw new InvalidOperationException("DetailedSurfaceImageHeaderRejected");
            int Big(int at) => bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3];
            if (bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71
                && bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10
                && bytes[12] == 'I' && bytes[13] == 'H' && bytes[14] == 'D' && bytes[15] == 'R')
                return (Big(16), Big(20));
            if (bytes[0] != 255 || bytes[1] != 216) throw new InvalidOperationException("DetailedSurfaceJpegOrPngRequired");
            var i = 2;
            while (i + 3 < bytes.Length)
            {
                if (bytes[i++] != 255) throw new InvalidOperationException("DetailedSurfaceJpegHeaderRejected");
                while (i < bytes.Length && bytes[i] == 255) i++;
                if (i >= bytes.Length) break;
                var marker = bytes[i++];
                if (marker == 217 || marker == 218) break;
                if (marker == 216 || marker == 1 || marker >= 208 && marker <= 215) continue;
                if (i + 1 >= bytes.Length) break;
                var length = bytes[i] * 256 + bytes[i + 1];
                if (length < 2 || i + length > bytes.Length) break;
                if (marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                {
                    if (length < 8) break;
                    return (bytes[i + 5] * 256 + bytes[i + 6], bytes[i + 3] * 256 + bytes[i + 4]);
                }
                i += length;
            }
            throw new InvalidOperationException("DetailedSurfaceImageDimensionsNotFound");
        }
    }
}
