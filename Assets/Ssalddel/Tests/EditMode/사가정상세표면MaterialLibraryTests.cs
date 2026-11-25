using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Ssalddel.Unity.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정상세표면MaterialLibraryTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly string DirectoryPath, ReceiptPath;
            public readonly string Json, ReceiptHash;
            public string FirstMap => Path.Combine(DirectoryPath, "brick_wall_001-Diffuse.png");
            public Fixture(int side = 1024)
            {
                DirectoryPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ssalddel-detailed-material-test-" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(DirectoryPath);
                var license = Path.Combine(DirectoryPath, "license.html"); var terms = Path.Combine(DirectoryPath, "terms.html");
                File.WriteAllText(license, "Synthetic CC0 unit fixture"); File.WriteAllText(terms, "Synthetic API unit fixture");
                var roles = new[] { "Diffuse", "nor_gl", "Rough" };
                var encoded = roles.ToDictionary(role => role, role => Encode(side, role));
                var assets = new[] { "brick_wall_001", "concrete_wall_003", "asphalt_02" }.Select(id =>
                {
                    var rows = roles.Select(role =>
                    {
                        var path = Path.Combine(DirectoryPath, id + "-" + role + ".png"); File.WriteAllBytes(path, encoded[role]);
                        var url = "https://dl.polyhaven.org/file/ph-assets/Textures/png/1k/" + id + "/fixture_" + role + ".png";
                        var md5 = Hash(encoded[role], true);
                        return (Role: role, Path: path, Url: url, Md5: md5, Bytes: encoded[role].Length);
                    }).ToArray();
                    var metadataPath = Path.Combine(DirectoryPath, id + "-metadata.json");
                    File.WriteAllText(metadataPath, "{" + string.Join(",", rows.Select(r => Q(r.Role) + ":{\"1k\":{\"png\":{\"url\":"
                        + Q(r.Url) + ",\"size\":" + r.Bytes + ",\"md5\":" + Q(r.Md5) + "}}}")) + "}");
                    var maps = "{" + string.Join(",", rows.Select(r => Q(r.Role) + ":{\"path\":" + Q(r.Path)
                        + ",\"sha256\":" + Q(Hash(File.ReadAllBytes(r.Path))) + ",\"url\":" + Q(r.Url) + ",\"bytes\":" + r.Bytes
                        + ",\"sourceMd5\":" + Q(r.Md5) + ",\"linear\":" + (r.Role == "Diffuse" ? "false" : "true") + "}")) + "}";
                    return "{\"assetId\":" + Q(id) + ",\"sourceUrl\":" + Q("https://polyhaven.com/a/" + id)
                        + ",\"apiUrl\":" + Q("https://api.polyhaven.com/files/" + id) + ",\"metadata\":" + Ref(metadataPath) + ",\"maps\":" + maps + "}";
                }).ToArray();
                Json = "{\"schema\":" + Q(사가정상세표면MaterialLibrary.ReceiptSchema) + ",\"provider\":\"Poly Haven\",\"license\":\"CC0\",\"surfaceMeaning\":"
                    + Q(사가정상세표면MaterialLibrary.SurfaceMeaning) + ",\"sourceTerms\":[{\"url\":\"https://polyhaven.com/license\",\"document\":"
                    + Ref(license) + "},{\"url\":\"https://polyhaven.com/our-api\",\"document\":" + Ref(terms) + "}],\"assets\":[" + string.Join(",", assets) + "]}";
                ReceiptPath = Path.Combine(DirectoryPath, "receipt.private.json"); File.WriteAllText(ReceiptPath, Json);
                ReceiptHash = Hash(File.ReadAllBytes(ReceiptPath));
            }
            private static byte[] Encode(int side, string role)
            {
                var texture = new Texture2D(side, side, TextureFormat.RGBA32, false, true);
                try
                {
                    var color = role == "nor_gl" ? new Color32(128, 128, 255, 255)
                        : role == "Rough" ? new Color32(128, 77, 10, 255) : new Color32(160, 140, 120, 255);
                    texture.SetPixels32(Enumerable.Repeat(color, side * side).ToArray()); texture.Apply();
                    return ImageConversion.EncodeToPNG(texture);
                }
                finally { Object.DestroyImmediate(texture); }
            }
            public void Dispose()
            {
                var resolved = Path.GetFullPath(DirectoryPath);
                var root = Path.GetFullPath(Path.GetTempPath()) + (Path.GetTempPath().EndsWith(Path.DirectorySeparatorChar.ToString()) ? "" : Path.DirectorySeparatorChar.ToString());
                if (!resolved.StartsWith(root + "ssalddel-detailed-material-test-", StringComparison.OrdinalIgnoreCase)
                    || resolved != DirectoryPath) throw new InvalidOperationException("FixtureCleanupScopeRejected");
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
        private static string Q(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        private static string Ref(string path) => "{\"path\":" + Q(path) + ",\"sha256\":" + Q(Hash(File.ReadAllBytes(path))) + "}";
        private static string Hash(byte[] bytes, bool md5 = false)
        { using HashAlgorithm algorithm = md5 ? MD5.Create() : SHA256.Create(); return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        [Test]
        public void CC0와_고정한세자산아홉지도가_아니면_거절한다()
        {
            using var fixture = new Fixture(8);
            Assert.DoesNotThrow(() => 사가정상세표면MaterialLibrary.ValidateReceiptJson(fixture.Json));
            Assert.Throws<InvalidOperationException>(() => 사가정상세표면MaterialLibrary.ValidateReceiptJson(fixture.Json.Replace("\"CC0\"", "\"Unknown\"")));
            Assert.Throws<InvalidOperationException>(() => 사가정상세표면MaterialLibrary.ValidateReceiptJson(fixture.Json.Replace("\"asphalt_02\"", "\"other_asset\"")));
        }

        [Test]
        public void 거칠기원문은_보존하고_R만뒤집어_금속0_매끈함alpha를_만든다()
        {
            var raw = new[] { new Color32(0, 90, 120, 255), new Color32(128, 55, 99, 255), new Color32(255, 0, 0, 255) };
            var copy = raw.ToArray();
            var packed = 사가정상세표면MaterialLibrary.PackMetallicSmoothness(raw);
            Assert.That(raw, Is.EqualTo(copy));
            Assert.That(packed, Is.EqualTo(new[] { new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 127), new Color32(0, 0, 0, 0) }));
        }

        [Test]
        public void 수신이미지는_디코딩전_헤더의실제크기를_확인한다()
        {
            using var fixture = new Fixture(8);
            var size = 사가정상세표면MaterialLibrary.ReadImageDimensions(File.ReadAllBytes(fixture.FirstMap));
            Assert.That(size.Width, Is.EqualTo(8)); Assert.That(size.Height, Is.EqualTo(8));
            Assert.Throws<InvalidOperationException>(() => new 사가정상세표면MaterialLibrary(fixture.ReceiptPath, fixture.ReceiptHash));
            Assert.Throws<InvalidOperationException>(() => 사가정상세표면MaterialLibrary.ReadImageDimensions(new byte[25]));
        }

        [Test]
        public void 아홉원문을검증하고_재질역할과선형지도를_연결한뒤_모두해제한다()
        {
            using var fixture = new Fixture();
            var library = new 사가정상세표면MaterialLibrary(fixture.ReceiptPath, fixture.ReceiptHash);
            try
            {
                Assert.That(library.Result.sourceMapCount, Is.EqualTo(9)); Assert.That(library.Result.textureCount, Is.EqualTo(12));
                Assert.That(library.Result.actualOneKDimensionsVerified, Is.True);
                Assert.That(library.Result.maps.All(m => m.hashVerified && m.metadataMatched && m.sourceMd5Verified), Is.True);
                Assert.That(library.Result.maps.Where(m => m.role == "Diffuse").All(m => !m.linear), Is.True);
                Assert.That(library.Result.maps.Where(m => m.role != "Diffuse").All(m => m.linear), Is.True);
                var wall = library.Create("maekjuabi", "Wall", Color.white, .3f);
                Assert.That(library.Create("maekjuabi", "Wall", Color.white, .3f), Is.SameAs(wall));
                var diffuse = (Texture2D)wall.GetTexture("_BaseMap");
                Assert.That(diffuse.name, Does.Contain("brick_wall_001"));
                Assert.That(diffuse.GetPixels32()[0], Is.EqualTo(new Color32(160, 140, 120, 255)));
                Assert.That(wall.IsKeywordEnabled("_NORMALMAP") && wall.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(((Texture2D)wall.GetTexture("_MetallicGlossMap")).GetPixels32()[0], Is.EqualTo(new Color32(0, 0, 0, 127)));
                Assert.That(wall.GetTextureScale("_BaseMap").x, Is.EqualTo(1f / 3f).Within(.000001f));
                Assert.That(library.Create("waffle", "Wall", Color.white, .2f).GetTexture("_BaseMap").name, Does.Contain("concrete_wall_003"));
                Assert.That(library.Create("street", "Road", Color.white, .2f).GetTexture("_BaseMap").name, Does.Contain("asphalt_02"));
                Assert.That(library.Create("waffle", "Glass", Color.white, .8f).GetFloat("_Smoothness"), Is.EqualTo(.8f));
                Assert.That(library.Result.materialCount, Is.EqualTo(4));
                library.Recheck();
                library.Dispose(); library.Dispose();
                Assert.That(wall == null && diffuse == null, Is.True);
                Assert.That(library.Result.disposed && library.Result.ownedObjectsDestroyed, Is.True);
                Assert.Throws<ObjectDisposedException>(() => library.Create("waffle", "Wall", Color.white, .2f));
            }
            finally { library.Dispose(); }
        }

        [Test]
        public void 투명유리는_기존불투명역할과별개이며_반사보존과그림자설정및소유권을_유지한다()
        {
            using var fixture=new Fixture();
            var pins=Directory.GetFiles(fixture.DirectoryPath).ToDictionary(path=>path,path=>Hash(File.ReadAllBytes(path)));
            var library=new 사가정상세표면MaterialLibrary(fixture.ReceiptPath,fixture.ReceiptHash);
            try
            {
                var tint=new Color(.67f,.79f,.81f,.22f);
                var oldGlass=library.Create("waffle","Glass",Color.white,.82f);
                var glass=library.Create("waffle","TransparentGlass",tint,.82f);
                var neutral=library.Create("waffle","NeutralBackdrop",Color.gray,.12f);
                Assert.That(library.Create("waffle","TransparentGlass",tint,.82f),Is.SameAs(glass));
                Assert.That(glass,Is.Not.SameAs(oldGlass));
                Assert.That(oldGlass.GetFloat("_Surface"),Is.Zero);
                Assert.That(oldGlass.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),Is.False);
                Assert.That(glass.GetFloat("_Surface"),Is.EqualTo(1));
                Assert.That(glass.GetFloat("_ZWrite"),Is.Zero);
                Assert.That(glass.GetFloat("_SrcBlend"),Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One));
                Assert.That(glass.GetFloat("_DstBlend"),Is.EqualTo((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
                Assert.That(glass.GetFloat("_BlendModePreserveSpecular"),Is.EqualTo(1));
                Assert.That(glass.GetFloat("_SpecularHighlights"),Is.EqualTo(1));
                Assert.That(glass.GetFloat("_EnvironmentReflections"),Is.EqualTo(1));
                Assert.That(glass.GetColor("_BaseColor").a,Is.EqualTo(tint.a).Within(.00001f));
                Assert.That(glass.renderQueue,Is.EqualTo(3000));
                Assert.That(glass.GetTag("RenderType",false),Is.EqualTo("Transparent"));
                Assert.That(glass.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")&&glass.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"),Is.True);
                Assert.That(glass.GetShaderPassEnabled("DepthOnly")||glass.GetShaderPassEnabled("ShadowCaster"),Is.False);
                Assert.That(neutral.GetFloat("_Surface"),Is.Zero);
                Assert.That(neutral.GetTexture("_BaseMap"),Is.Null);
                Assert.That(library.Result.transparentGlassMaterialCount,Is.EqualTo(1));
                Assert.That(library.Result.neutralBackdropMaterialCount,Is.EqualTo(1));
                Assert.That(library.Result.neutralBackdropNotObservedInterior,Is.True);
                Assert.Throws<ArgumentException>(()=>library.Create("waffle","TransparentGlass",Color.white,.82f));
                library.Recheck();
                Assert.That(pins.All(pin=>Hash(File.ReadAllBytes(pin.Key))==pin.Value),Is.True);
                library.Dispose();library.Dispose();
                Assert.That(glass==null&&oldGlass==null&&neutral==null,Is.True);
                Assert.That(library.Result.disposed&&library.Result.ownedObjectsDestroyed,Is.True);
                Assert.That(library.Result.originalAssetsModified||library.Result.actualStoreScanClaimed||library.Result.sceneSaved,Is.False);
            }
            finally{library.Dispose();}
        }

        [Test]
        public void 고정hash나_수신원문이_변경되면_다른자료로_조용히바꾸지않는다()
        {
            using var fixture = new Fixture(8);
            Assert.Throws<InvalidOperationException>(() => new 사가정상세표면MaterialLibrary(fixture.ReceiptPath, new string('0', 64)));
            var bytes = File.ReadAllBytes(fixture.FirstMap); bytes[20] ^= 1; File.WriteAllBytes(fixture.FirstMap, bytes);
            Assert.Throws<InvalidOperationException>(() => new 사가정상세표면MaterialLibrary(fixture.ReceiptPath, fixture.ReceiptHash));
        }
    }
}
