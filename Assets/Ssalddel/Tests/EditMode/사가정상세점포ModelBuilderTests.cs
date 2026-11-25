using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Ssalddel.Unity.Presentation.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class 사가정상세점포ModelBuilderTests
    {
        private readonly List<Object> cleanup = new();

        [TearDown]
        public void Cleanup()
        {
            foreach (var value in cleanup.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
            cleanup.Clear();
        }

        private Transform Parent()
        {
            var value = new GameObject("상세 전면 시험 · 저장하지 않음") { hideFlags = HideFlags.DontSave };
            cleanup.Add(value); return value.transform;
        }

        private 사가정상세점포ModelBuilder.Palette Palette(string slug, bool transparentGlass = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            return 사가정상세점포ModelBuilder.CreatePalette(slug, (role, color, smoothness) =>
            {
                var material = new Material(shader) { name = "fixture:" + role, hideFlags = HideFlags.DontSave };
                material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
                if(role=="TransparentGlass")
                {material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);material.renderQueue=3000;}
                cleanup.Add(material); return material;
            }, transparentGlass);
        }

        private static 사가정상세점포ModelBuilder.Spec Spec(string slug) => new()
        { slug = slug, targetBuildingId = "exact-private-study:" + slug, facadeWidth = 6, symbolicHeight = 4, layer = 31 };

        [TestCase("waffle", true, false)]
        [TestCase("maekjuabi", true, false)]
        [TestCase("touslesjours", false, true)]
        public void 관측한차양과곡면차이는_점포별로유지하며_소유자미확정문을_생성하지않는다(string slug, bool canopy, bool curved)
        {
            using var model = 사가정상세점포ModelBuilder.Build(Parent(), Spec(slug), Palette(slug));
            Assert.That(model.Result.canopyPresent, Is.EqualTo(canopy));
            Assert.That(model.Result.curvedFramePresent, Is.EqualTo(curved));
            Assert.That(model.Result.targetBuildingId, Is.EqualTo("exact-private-study:" + slug));
            Assert.That(model.Result.entrancePlacementHypothesis, Is.EqualTo(slug == "waffle"));
            Assert.That(model.Result.entranceOwnershipClaimed, Is.False);
            Assert.That(model.Result.actualEntrancePositionKnown, Is.False);
            Assert.That(model.Result.measuredDimensions || model.Result.actualHeightKnown || model.Result.exactWindowCountKnown, Is.False);
            Assert.That(model.Result.interiorReconstructed || model.Result.originalPhotoUsedAsTexture || model.Result.syntyRequired, Is.False);
            Assert.That(model.Result.rendererCount, Is.InRange(4, 9));
            Assert.That(model.Result.vertexCount, Is.GreaterThan(100));
            Assert.That(model.VisualRoot.gameObject.activeSelf, Is.False);
            Assert.That(model.Result.transparentGlass || model.Result.neutralBackdropNotObservedInterior, Is.False);
        }

        [Test]
        public void 사진재질UV는_미터단위이고_창틀은_유리보다앞으로_돌출한다()
        {
            using var model = 사가정상세점포ModelBuilder.Build(Parent(), Spec("waffle"), Palette("waffle"));
            var renderers = model.VisualRoot.GetComponentsInChildren<MeshRenderer>(true);
            var wall = renderers.Single(r => r.sharedMaterial.name == "fixture:Wall").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(Vector3.Distance(wall.vertices[0], wall.vertices[1]), Is.EqualTo(6).Within(.0001f));
            Assert.That(Vector2.Distance(wall.uv[0], wall.uv[1]), Is.EqualTo(6).Within(.0001f));
            Assert.That(Vector2.Distance(wall.uv[1], wall.uv[2]), Is.EqualTo(4).Within(.0001f));
            Assert.That(wall.tangents.Length, Is.EqualTo(wall.vertexCount));
            var frame = renderers.Single(r => r.sharedMaterial.name == "fixture:Frame").GetComponent<MeshFilter>().sharedMesh;
            var glass = renderers.Single(r => r.sharedMaterial.name == "fixture:Glass").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(frame.bounds.max.z, Is.GreaterThan(glass.bounds.max.z + .04f));
            Assert.That(model.SignAnchor.localPosition.z, Is.GreaterThan(0));
            Assert.That(model.SignSize.x, Is.GreaterThan(1));
            Assert.That(model.VisualRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(model.VisualRoot.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(model.VisualRoot.GetComponentsInChildren<Camera>(true), Is.Empty);
        }

        [Test]
        public void 도로방향이달라도_같은전면기하와_로컬경계로_배치한다()
        {
            var spec = Spec("touslesjours"); spec.localFace = new Vector3(550, .16f, 8); spec.normal = Vector3.right;
            using var model = 사가정상세점포ModelBuilder.Build(Parent(), spec, Palette(spec.slug));
            Assert.That(Vector3.Dot(model.VisualRoot.forward, Vector3.right), Is.GreaterThan(.999f));
            Assert.That(model.VisualRoot.localPosition, Is.EqualTo(spec.localFace));
            Assert.That(model.DetailBounds.min.y, Is.GreaterThanOrEqualTo(.16f - .001f));
            Assert.That(model.DetailBounds.max.y, Is.LessThanOrEqualTo(4.16f + .001f));
            Assert.That(model.DetailBounds.max.x, Is.GreaterThan(spec.localFace.x + .3f));
            Assert.That(model.Result.symbolicHeightMeters, Is.EqualTo(4));
        }

        [Test]
        public void 출처대상이나_기호높이가달라진_입력은_빈자식도남기지않고_거절한다()
        {
            var parent = Parent(); var spec = Spec("waffle"); var palette = Palette(spec.slug);
            spec.targetBuildingId = " ";
            Assert.Throws<ArgumentException>(() => 사가정상세점포ModelBuilder.Build(parent, spec, palette));
            spec.targetBuildingId = "exact-private-study:waffle"; spec.symbolicHeight = 8;
            Assert.Throws<ArgumentException>(() => 사가정상세점포ModelBuilder.Build(parent, spec, palette));
            spec.symbolicHeight = 4; spec.normal = Vector3.zero;
            Assert.Throws<ArgumentException>(() => 사가정상세점포ModelBuilder.Build(parent, spec, palette));
            Assert.That(parent.childCount, Is.Zero);
        }

        [Test]
        public void 폐기하면_자체기하는해제하고_외부재질과그값은_그대로유지한다()
        {
            var palette = Palette("maekjuabi"); var color = palette.Wall.GetColor("_BaseColor");
            var model = 사가정상세점포ModelBuilder.Build(Parent(), Spec("maekjuabi"), palette);
            var meshes = model.VisualRoot.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            model.Dispose(); model.Dispose();
            Assert.That(model.IsDisposed, Is.True);
            Assert.That(model.VisualRoot == null, Is.True);
            Assert.That(meshes.All(m => m == null), Is.True);
            Assert.That(palette.Wall == null, Is.False);
            Assert.That(palette.Wall.GetColor("_BaseColor"), Is.EqualTo(color));
        }

        [TestCase("waffle", -1.76f, 1.34f)]
        [TestCase("maekjuabi", -1.05f, .81f)]
        [TestCase("touslesjours", -2.40f, 1.48f)]
        public void 투명판만_창중앙의벽판을비우고_기존몸체바깥의빈중립셸을_분리한다(string slug,float x,float y)
        {
            using var legacy=사가정상세점포ModelBuilder.Build(Parent(),Spec(slug),Palette(slug));
            var spec=Spec(slug);spec.transparentGlass=true;var palette=Palette(slug,true);
            var originalColor=palette.Glass.GetColor("_BaseColor");
            using var detailed=사가정상세점포ModelBuilder.Build(Parent(),spec,palette);
            var ray=new Ray(new Vector3(x,y,2),Vector3.back);
            bool IsWall(MeshRenderer r)=>new[]{"fixture:Wall","fixture:LowerWall","fixture:Mortar"}.Contains(r.sharedMaterial.name);
            Assert.That(legacy.VisualRoot.GetComponentsInChildren<MeshRenderer>(true).Where(IsWall).Any(r=>Hits(ray,r)),Is.True);
            var renderers=detailed.VisualRoot.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Where(IsWall).Any(r=>Hits(ray,r)),Is.False,"투명 유리 바로 뒤에 기존 전체 벽판이 남으면 안 된다.");
            Assert.That(renderers.Where(r=>r.sharedMaterial==palette.Glass).Any(r=>Hits(ray,r)),Is.True);
            var backdrop=renderers.Single(r=>r.sharedMaterial==palette.NeutralBackdrop);
            Assert.That(Hits(ray,backdrop),Is.True);
            Assert.That(backdrop.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v=>v.z),Is.GreaterThan(.005f),"빈 셸은 기존 본체 전면 z=0을 침범하지 않는다.");
            Assert.That(renderers.Single(r=>r.sharedMaterial==palette.Glass).shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(detailed.Result.transparentGlass && detailed.Result.neutralBackdropNotObservedInterior,Is.True);
            Assert.That(detailed.Result.glazingRecessMeters,Is.InRange(.025f,.16f));
            Assert.That(detailed.Result.neutralShellMeaning,Is.EqualTo("EmptyIllustrativeRevealNotObservedRoomOrMeasuredDepth"));
            Assert.That(detailed.Result.glassOpticsMeaning,Is.EqualTo("UrpLitAlphaPreservedSpecularViewAngleFresnel"));
            Assert.That(detailed.Result.interiorReconstructed || detailed.Result.measuredDimensions || detailed.Result.actualHeightKnown
                || detailed.Result.personOrMenuImageCopied || detailed.Result.sourceMaterialsModified || detailed.Result.canonicalApplied,Is.False);
            Assert.That(detailed.VisualRoot.GetComponentsInChildren<Light>(true),Is.Empty);
            Assert.That(detailed.VisualRoot.GetComponentsInChildren<Camera>(true),Is.Empty);
            var meshes=detailed.VisualRoot.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            detailed.Dispose();detailed.Dispose();
            Assert.That(meshes.All(m=>m==null),Is.True);
            Assert.That(palette.Glass==null || palette.NeutralBackdrop==null,Is.False);
            Assert.That(palette.Glass.GetColor("_BaseColor"),Is.EqualTo(originalColor));
        }

        [Test]
        public void 투명옵션에_불투명유리나_빈후면재질이없으면_기하를남기지않고거절한다()
        {
            var parent=Parent();var spec=Spec("waffle");spec.transparentGlass=true;
            Assert.Throws<ArgumentException>(()=>사가정상세점포ModelBuilder.Build(parent,spec,Palette(spec.slug)));
            var palette=Palette(spec.slug,true);palette.NeutralBackdrop=null;
            Assert.Throws<ArgumentException>(()=>사가정상세점포ModelBuilder.Build(parent,spec,palette));
            Assert.That(parent.childCount,Is.Zero);
        }

        private static bool Hits(Ray ray,MeshRenderer renderer)
        {
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            for(var i=0;i<indices.Length;i+=3)
            {
                var a=vertices[indices[i]];var edge1=vertices[indices[i+1]]-a;var edge2=vertices[indices[i+2]]-a;
                var p=Vector3.Cross(ray.direction,edge2);var determinant=Vector3.Dot(edge1,p);
                if(Mathf.Abs(determinant)<.000001f)continue;
                var offset=ray.origin-a;var u=Vector3.Dot(offset,p)/determinant;if(u<0||u>1)continue;
                var q=Vector3.Cross(offset,edge1);var v=Vector3.Dot(ray.direction,q)/determinant;
                if(v>=0&&u+v<=1&&Vector3.Dot(edge2,q)/determinant>0)return true;
            }
            return false;
        }
    }
}
