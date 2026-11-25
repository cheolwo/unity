using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 공개 거리 사진에서 확인한 세 점포의 전면 특징을 비실측 기하로 조립한다.
    /// 원본 사진·사람·메뉴·실내를 복제하지 않으며 건물 높이·동일성·출입구 위치의 권위를 만들지 않는다.
    /// +Z는 전면 도로 방향, +X는 점포 가로 방향이다. 외부 재질의 UV는 미터 단위다.
    /// </summary>
    public static class 사가정상세점포ModelBuilder
    {
        public const string Revision = "private-observed-storefront-model.r1";
        public const string TransparentRevision = "private-observed-storefront-model.r2";

        [Serializable]
        public sealed class Spec
        {
            public string targetBuildingId = string.Empty;
            public string slug = string.Empty;
            public Vector3 localFace;
            public Vector3 normal = Vector3.forward;
            public float facadeWidth = 6f;
            public float symbolicHeight = 4f;
            // 기존 기호 전면은 유지하고 새 비공개 비교판에서만 투명 표현을 요청한다.
            public bool transparentGlass;
            public int layer;
        }

        public sealed class Palette
        {
            public Material Wall, LowerWall, Frame, Glass, Trim, SignBoard, Awning, Threshold, Mortar;
            public Material NeutralBackdrop;
            internal Material[] All => new[] { Wall, LowerWall, Frame, Glass, Trim, SignBoard, Awning, Threshold, Mortar };
        }

        [Serializable]
        public sealed class Report
        {
            public string revision = Revision;
            public string targetBuildingId, slug;
            public string scope = "PrivatePhotoReferencedFacadeOnly";
            public string dimensionMeaning = "UnmeasuredSymbolicProportionsNotActualWindowCountOrBuildingHeight";
            public float symbolicHeightMeters, facadeWidthMeters, maximumProjectionMeters;
            public int rendererCount, vertexCount, triangleCount, schematicGlazingBayCount, visibleAppearanceBandCount;
            public bool measuredDimensions, actualHeightKnown, exactWindowCountKnown, actualEntrancePositionKnown;
            public bool entrancePlacementHypothesis, entranceOwnershipClaimed, canopyPresent, curvedFramePresent;
            public bool interiorReconstructed, originalPhotoUsedAsTexture, personOrMenuImageCopied, syntyRequired;
            public bool canonicalApplied, sourceMaterialsModified;
            public bool transparentGlass, neutralBackdropNotObservedInterior;
            public float glazingRecessMeters;
            public string glassOpticsMeaning = "LegacyOpaqueSymbolicGlazing";
            public string neutralShellMeaning = "NoNeutralShell";
            public bool inactiveSourceRoot = true;
        }

        public sealed class Model : IDisposable
        {
            private readonly List<Mesh> meshes;
            public Transform VisualRoot { get; }
            public Transform SignAnchor { get; }
            public Vector2 SignSize { get; }
            /// <summary>호출한 parent의 로컬 미터 좌표다. 실측한 경계가 아니다.</summary>
            public Bounds DetailBounds { get; }
            public Report Result { get; }
            public bool IsDisposed { get; private set; }

            internal Model(Transform root, Transform anchor, Vector2 signSize, Bounds bounds, Report report, List<Mesh> ownedMeshes)
            { VisualRoot = root; SignAnchor = anchor; SignSize = signSize; DetailBounds = bounds; Result = report; meshes = ownedMeshes; }

            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;
                Release(VisualRoot == null ? null : VisualRoot.gameObject);
                foreach (var mesh in meshes) Release(mesh);
            }
        }

        /// <summary>호출자가 생성·소유하는 재질을 받으며 기존 자산 재질을 바꾸지 않는다.</summary>
        public static Palette CreatePalette(string slug, Func<string, Color, float, Material> create, bool transparentGlass = false)
        {
            ValidateSlug(slug);
            if (create == null) throw new ArgumentNullException(nameof(create));
            var wall = slug == "waffle" ? Hex(0xD8963C) : slug == "maekjuabi" ? Hex(0x54443F) : Hex(0x264945);
            var lower = slug == "waffle" ? Hex(0xC5B898) : slug == "maekjuabi" ? Hex(0xD4CEBC) : wall;
            var frame = slug == "touslesjours" ? Hex(0x264945) : Hex(0xD5D1C5);
            var trim = slug == "maekjuabi" ? Hex(0x637F7E) : slug == "touslesjours" ? wall : Hex(0xD8CBAE);
            var sign = slug == "maekjuabi" ? Hex(0xD4D0C5) : wall;
            var awning = slug == "waffle" ? Hex(0x173556) : Hex(0x635448);
            return new Palette
            {
                Wall = create("Wall", wall, .2f), LowerWall = create("LowerWall", lower, .22f),
                Frame = create("Frame", frame, transparentGlass ? .52f : .36f),
                Glass = create(transparentGlass ? "TransparentGlass" : "Glass",
                    transparentGlass ? new Color(.67f,.79f,.81f,.22f) : Hex(0x203A46), .82f),
                Trim = create("Trim", trim, .28f), SignBoard = create("SignBoard", sign, .28f),
                Awning = create("Awning", awning, .12f), Threshold = create("Threshold", slug == "waffle" ? Hex(0x2F6747) : lower, .13f),
                Mortar = create("Mortar", Hex(0x71675D), .15f),
                NeutralBackdrop = transparentGlass ? create("NeutralBackdrop", Hex(0x626565), .12f) : null
            };
        }

        public static Model Build(Transform parent, Spec spec, Palette palette)
        {
            Validate(parent, spec, palette);
            var root = Child(parent, "사진 참조 상세 전면 · " + spec.slug, spec.layer);
            root.gameObject.SetActive(false);
            root.localPosition = spec.localFace;
            root.localRotation = Quaternion.LookRotation(new Vector3(spec.normal.x, 0, spec.normal.z).normalized, Vector3.up);
            var geometry = new Geometry(); var ownedMeshes = new List<Mesh>();
            var report = new Report { targetBuildingId = spec.targetBuildingId, slug = spec.slug,
                revision = spec.transparentGlass ? TransparentRevision : Revision,
                symbolicHeightMeters = spec.symbolicHeight, facadeWidthMeters = spec.facadeWidth,
                transparentGlass = spec.transparentGlass, neutralBackdropNotObservedInterior = spec.transparentGlass,
                glassOpticsMeaning = spec.transparentGlass ? "UrpLitAlphaPreservedSpecularViewAngleFresnel" : "LegacyOpaqueSymbolicGlazing",
                neutralShellMeaning = spec.transparentGlass ? "EmptyIllustrativeRevealNotObservedRoomOrMeasuredDepth" : "NoNeutralShell",
                visibleAppearanceBandCount = spec.slug == "maekjuabi" ? 2 : 1 };
            try
            {
                var w = spec.facadeWidth; var h = spec.symbolicHeight;
                Vector3 signCenter; Vector2 signSize;
                if (spec.slug == "waffle") BuildWaffle(geometry, palette, w, h, report, out signCenter, out signSize);
                else if (spec.slug == "maekjuabi") BuildMaekjuabi(geometry, palette, w, h, report, out signCenter, out signSize);
                else BuildTouslesjours(geometry, palette, w, h, report, out signCenter, out signSize);
                var localBounds = geometry.Build(root, spec.layer, ownedMeshes, report);
                var sign = Child(root, "직접 관측 상호 PNG 연결 기준점", spec.layer);
                sign.localPosition = signCenter;
                report.maximumProjectionMeters = localBounds.max.z;
                var parentBounds = TransformBounds(localBounds, Matrix4x4.TRS(root.localPosition, root.localRotation, Vector3.one));
                return new Model(root, sign, signSize, parentBounds, report, ownedMeshes);
            }
            catch
            {
                Release(root.gameObject);
                foreach (var mesh in ownedMeshes) Release(mesh);
                throw;
            }
        }

        private static void BuildWaffle(Geometry g, Palette p, float w, float h, Report r, out Vector3 sign, out Vector2 signSize)
        {
            var baseY = .22f; var glazingH = h*.56f; var glazingW = w*.88f;
            var opening=new Rect(-glazingW*.5f,baseY,glazingW,glazingH);
            Wall(g,p.Wall,new Vector3(0,h*.5f,.04f),new Vector3(w,h,.10f),opening,r.transparentGlass);
            Wall(g,p.LowerWall,new Vector3(0,h*.34f,.10f),new Vector3(w*.95f,h*.60f,.08f),opening,r.transparentGlass);
            Glazing(g,p,glazingW,glazingH,baseY,3,.17f,r);
            r.schematicGlazingBayCount = 3;
            // 사진에서 출입구 위치를 실측하지 않았다. 오른쪽 창의 세로틀·손잡이는 배치 가설이다.
            var doorX = glazingW/3f;
            g.Box(p.Frame,new Vector3(doorX+glazingW/6f-.08f,baseY+glazingH*.48f,.31f),new Vector3(.025f,.32f,.045f));
            r.entrancePlacementHypothesis = true;
            g.Box(p.Threshold,new Vector3(0,.07f,.39f),new Vector3(w*.96f,.14f,.72f));
            g.Box(p.Threshold,new Vector3(0,.17f,.20f),new Vector3(w*.91f,.08f,.33f));
            var canopyY = h*.68f;
            g.Box(p.Awning,new Vector3(0,canopyY,.45f),new Vector3(w*.99f,.085f,.78f));
            g.Box(p.Awning,new Vector3(0,canopyY-.075f,.83f),new Vector3(w*.99f,.17f,.035f));
            for(var i=0;i<4;i++)
                g.Box(p.Trim,new Vector3(Mathf.Lerp(-w*.44f,w*.44f,(i+.5f)/4),canopyY-.08f,.36f),new Vector3(.025f,.035f,.55f));
            r.canopyPresent=true;
            signSize=new Vector2(w*.86f,h*.14f); sign=new Vector3(0,h*.82f,.245f);
            g.Box(p.SignBoard,new Vector3(0,sign.y,.15f),new Vector3(w*.98f,h*.27f,.18f));
        }

        private static void BuildMaekjuabi(Geometry g, Palette p, float w, float h, Report r, out Vector3 sign, out Vector2 signSize)
        {
            var lowerOpening=new Rect(-w*.35f,.25f,w*.70f,h*.28f);
            Wall(g,p.LowerWall,new Vector3(0,h*.245f,.05f),new Vector3(w,h*.49f,.12f),lowerOpening,r.transparentGlass);
            var upperBottom=h*.51f; var upperH=h*.31f;
            var windowW=Mathf.Min(w*.30f,2.0f); var windowH=h*.22f; var windowBottom=upperBottom+.10f;
            var upperOpening=new Rect(-windowW*.5f,windowBottom,windowW,windowH);
            Wall(g,p.Mortar,new Vector3(0,upperBottom+upperH*.5f,.055f),new Vector3(w,upperH,.11f),upperOpening,r.transparentGlass);
            var rows=Mathf.Max(5,Mathf.CeilToInt(upperH/.12f)); var brickH=upperH/rows;
            var columns=Mathf.Clamp(Mathf.CeilToInt(w/.32f),4,180); var brickW=w/columns;
            for(var row=0;row<rows;row++) for(var col=-1;col<=columns;col++)
            {
                var left=-w*.5f+(col+(row%2)*.5f)*brickW;
                var right=Mathf.Min(w*.5f,left+brickW); left=Mathf.Max(-w*.5f,left);
                if(right-left<.025f) continue;
                Wall(g,p.Wall,new Vector3((left+right)*.5f,upperBottom+(row+.5f)*brickH,.12f),new Vector3(right-left-.012f,brickH-.012f,.045f),upperOpening,r.transparentGlass);
            }
            Window(g,p,new Vector3(0,windowBottom+windowH*.5f,.18f),windowW,windowH,.17f,r);
            g.Box(p.Trim,new Vector3(0,h*.50f,.18f),new Vector3(w,.13f,.30f));
            g.Box(p.Trim,new Vector3(0,h*.84f,.16f),new Vector3(w,.10f,.31f));
            // 아래층 전용 출입구의 소속은 미확정이다. 창구 띠만 표현하고 문·입구 단차는 만들지 않는다.
            Glazing(g,p,w*.70f,h*.28f,.25f,2,.20f,r);
            r.schematicGlazingBayCount=3;
            g.Box(p.Awning,new Vector3(0,h*.48f,.45f),new Vector3(w*.98f,.10f,.77f),Quaternion.Euler(11,0,0));
            g.Box(p.Awning,new Vector3(0,h*.43f,.82f),new Vector3(w*.98f,.15f,.04f));
            r.canopyPresent=true;
            signSize=new Vector2(w*.88f,h*.11f); sign=new Vector3(0,h*.93f,.255f);
            g.Box(p.SignBoard,new Vector3(0,sign.y,.15f),new Vector3(w*.96f,h*.14f,.20f));
        }

        private static void BuildTouslesjours(Geometry g, Palette p, float w, float h, Report r, out Vector3 sign, out Vector2 signSize)
        {
            Wall(g,p.Wall,new Vector3(0,h*.5f,.035f),new Vector3(w,h,.07f),
                new Rect(-w*.48f,h*.085f,w*.96f,h*.57f),r.transparentGlass);
            const int bays=6; var curvature=Mathf.Min(.28f,w*.035f);
            for(var i=0;i<bays;i++)
            {
                var x0=Mathf.Lerp(-w*.48f,w*.48f,(float)i/bays); var x1=Mathf.Lerp(-w*.48f,w*.48f,(float)(i+1)/bays);
                float Z(float x)=>.13f+curvature*(1-Mathf.Pow(x/(w*.5f),2));
                var a=new Vector3(x0,0,Z(x0)); var b=new Vector3(x1,0,Z(x1));
                var center=(a+b)*.5f; var yaw=Quaternion.Euler(0,-Mathf.Atan2(b.z-a.z,b.x-a.x)*Mathf.Rad2Deg,0);
                var length=Vector3.Distance(a,b);
                var glassCenter=center+Vector3.up*(h*.37f);
                g.Box(p.Glass,glassCenter,new Vector3(length-.035f,h*.57f,r.transparentGlass ? .008f : .035f),yaw);
                if(r.transparentGlass) NeutralGlazingShell(g,p,glassCenter,length-.035f,h*.57f,yaw,r);
                g.Box(p.Frame,new Vector3(center.x,h*.08f,center.z+.045f),new Vector3(length,.14f,.13f),yaw);
                g.Box(p.Frame,new Vector3(center.x,h*.68f,center.z+.04f),new Vector3(length,.16f,.15f),yaw);
                g.Box(p.SignBoard,new Vector3(center.x,h*.84f,center.z+.025f),new Vector3(length+.015f,h*.27f,.15f),yaw);
                g.Box(p.Frame,new Vector3(a.x,h*.38f,a.z+.045f),new Vector3(.055f,h*.64f,.14f));
                if(i==bays-1) g.Box(p.Frame,new Vector3(b.x,h*.38f,b.z+.045f),new Vector3(.055f,h*.64f,.14f));
            }
            r.schematicGlazingBayCount=bays; r.curvedFramePresent=true;
            signSize=new Vector2(w*.77f,h*.12f); sign=new Vector3(0,h*.85f,.13f+curvature+.11f);
            // 이 표본에서 출입구와 돌출 천막은 미확인이다. 유리와 곡면 프레임에 한정한다.
        }

        private static void Glazing(Geometry g,Palette p,float width,float height,float bottom,int bays,float depth,Report report)
        {
            var bayW=width/bays;
            for(var i=0;i<bays;i++)
                Window(g,p,new Vector3(-width*.5f+(i+.5f)*bayW,bottom+height*.5f,depth),bayW-.055f,height,.13f,report);
        }

        private static void Window(Geometry g,Palette p,Vector3 center,float width,float height,float frameDepth,Report report)
        {
            g.Box(p.Glass,center,new Vector3(width,height,report.transparentGlass ? .008f : .03f));
            if(report.transparentGlass) NeutralGlazingShell(g,p,center,width,height,Quaternion.identity,report);
            var frame=.055f; var z=center.z+.045f;
            g.Box(p.Frame,new Vector3(center.x-width*.5f,center.y,z),new Vector3(frame,height+.11f,frameDepth));
            g.Box(p.Frame,new Vector3(center.x+width*.5f,center.y,z),new Vector3(frame,height+.11f,frameDepth));
            g.Box(p.Frame,new Vector3(center.x,center.y-height*.5f,z),new Vector3(width,.07f,frameDepth));
            g.Box(p.Frame,new Vector3(center.x,center.y+height*.5f,z),new Vector3(width,.07f,frameDepth));
        }

        // 실제 방·창고를 복원하지 않는다. 기존 GIS 전면 바깥의 얕은 빈 셸만 두어 유리의 투과와 틀 깊이를 구분한다.
        private static void NeutralGlazingShell(Geometry g,Palette p,Vector3 center,float width,float height,Quaternion rotation,Report report)
        {
            var across=rotation*Vector3.right; var forward=rotation*Vector3.forward;
            var closestEdge=center.z-Mathf.Abs(across.z)*width*.5f;
            var depth=Mathf.Min(.16f,(closestEdge-.025f)/forward.z);
            if(!float.IsFinite(depth)||depth<.025f)throw new InvalidOperationException("DetailedNeutralGlazingShellOutsideBodyRequired");
            var halfWay=center-forward*depth*.5f;
            g.Box(p.NeutralBackdrop,center-forward*depth,new Vector3(width,height,.012f),rotation);
            g.Box(p.NeutralBackdrop,halfWay+Vector3.down*(height*.5f),new Vector3(width,.025f,depth),rotation);
            g.Box(p.NeutralBackdrop,halfWay+Vector3.up*(height*.5f),new Vector3(width,.025f,depth),rotation);
            g.Box(p.Frame,halfWay-across*(width*.5f),new Vector3(.025f,height,depth),rotation);
            g.Box(p.Frame,halfWay+across*(width*.5f),new Vector3(.025f,height,depth),rotation);
            report.glazingRecessMeters=report.glazingRecessMeters<=0?depth:Mathf.Min(report.glazingRecessMeters,depth);
        }

        private static void Wall(Geometry g,Material material,Vector3 center,Vector3 size,Rect opening,bool removeOpening)
        {
            if(!removeOpening){g.Box(material,center,size);return;}
            var left=center.x-size.x*.5f;var right=center.x+size.x*.5f;
            var bottom=center.y-size.y*.5f;var top=center.y+size.y*.5f;
            var x0=Mathf.Max(left,opening.xMin);var x1=Mathf.Min(right,opening.xMax);
            var y0=Mathf.Max(bottom,opening.yMin);var y1=Mathf.Min(top,opening.yMax);
            if(x0>=x1||y0>=y1){g.Box(material,center,size);return;}
            void Strip(float a,float b,float c,float d)
            {if(b-a>.0001f&&d-c>.0001f)g.Box(material,new Vector3((a+b)*.5f,(c+d)*.5f,center.z),new Vector3(b-a,d-c,size.z));}
            Strip(left,x0,bottom,top);Strip(x1,right,bottom,top);Strip(x0,x1,bottom,y0);Strip(x0,x1,y1,top);
        }

        private sealed class Geometry
        {
            private sealed class Batch
            {
                public Material Material; public readonly List<Vector3> Vertices=new();
                public readonly List<Vector2> Uv=new(); public readonly List<int> Triangles=new();
            }
            private readonly Dictionary<Material,Batch> batches=new();
            public void Box(Material material,Vector3 center,Vector3 size,Quaternion? rotation=null)
            {
                if(!batches.TryGetValue(material,out var batch)) batches.Add(material,batch=new Batch{Material=material});
                var e=size*.5f; var q=rotation??Quaternion.identity;
                var v=new[]{new Vector3(-e.x,-e.y,-e.z),new Vector3(e.x,-e.y,-e.z),new Vector3(e.x,e.y,-e.z),new Vector3(-e.x,e.y,-e.z),
                    new Vector3(-e.x,-e.y,e.z),new Vector3(e.x,-e.y,e.z),new Vector3(e.x,e.y,e.z),new Vector3(-e.x,e.y,e.z)};
                var faces=new[]{new[]{4,5,6,7},new[]{1,0,3,2},new[]{0,4,7,3},new[]{5,1,2,6},new[]{7,6,2,3},new[]{0,1,5,4}};
                foreach(var face in faces)
                {
                    var first=batch.Vertices.Count;
                    var a=v[face[0]]; var b=v[face[1]]; var c=v[face[2]];
                    var normal=Vector3.Cross(b-a,c-a).normalized;
                    foreach(var index in face)
                    {
                        var point=q*v[index]+center; batch.Vertices.Add(point);
                        // 벽돌·틀도 전면 좌표를 공유해 사진 재질의 반복 크기가 상자마다 달라지지 않는다.
                        batch.Uv.Add(Mathf.Abs(normal.y)>.7f?new Vector2(point.x,point.z):Mathf.Abs(normal.x)>.7f?new Vector2(point.z,point.y):new Vector2(point.x,point.y));
                    }
                    batch.Triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                }
            }

            public Bounds Build(Transform root,int layer,List<Mesh> owned,Report report)
            {
                Bounds? all=null;
                foreach(var batch in batches.Values)
                {
                    var mesh=new Mesh{name="비실측 전면 상세 · "+batch.Material.name,hideFlags=HideFlags.DontSave,
                        indexFormat=batch.Vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                    mesh.SetVertices(batch.Vertices);mesh.SetTriangles(batch.Triangles,0);mesh.SetUVs(0,batch.Uv);
                    mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();owned.Add(mesh);
                    var go=Child(root,"재질별 상세 기하 · "+batch.Material.name,layer);
                    go.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.Material;
                    renderer.shadowCastingMode=batch.Material.renderQueue>=(int)RenderQueue.Transparent?ShadowCastingMode.Off:ShadowCastingMode.On;
                    renderer.receiveShadows=true;
                    report.rendererCount++; report.vertexCount+=mesh.vertexCount;report.triangleCount+=batch.Triangles.Count/3;
                    if(all.HasValue){var bounds=all.Value;bounds.Encapsulate(mesh.bounds);all=bounds;}else all=mesh.bounds;
                }
                return all??throw new InvalidOperationException("DetailedStorefrontGeometryRequired");
            }
        }

        private static void Validate(Transform parent,Spec spec,Palette palette)
        {
            if(parent==null||spec==null||palette==null)throw new ArgumentException("DetailedStorefrontContextRequired");
            ValidateSlug(spec.slug);
            if(string.IsNullOrWhiteSpace(spec.targetBuildingId)||spec.layer<0||spec.layer>31
                ||!Finite(spec.localFace)||!Finite(spec.normal)||new Vector2(spec.normal.x,spec.normal.z).sqrMagnitude<.9f
                ||Mathf.Abs(spec.normal.y)>.001f||!float.IsFinite(spec.facadeWidth)||spec.facadeWidth<1.5f||spec.facadeWidth>60
                ||Mathf.Abs(spec.symbolicHeight-4f)>.001f||!float.IsFinite(spec.symbolicHeight)||palette.All.Any(m=>m==null))
                throw new ArgumentException("DetailedStorefrontBoundedSymbolicInputRequired");
            if(spec.transparentGlass && (palette.NeutralBackdrop==null || !palette.Glass.HasProperty("_Surface")
                || palette.Glass.GetFloat("_Surface")!=1 || palette.Glass.GetFloat("_ZWrite")!=0
                || palette.Glass.renderQueue<(int)RenderQueue.Transparent
                || palette.Glass.GetColor("_BaseColor").a<=0 || palette.Glass.GetColor("_BaseColor").a>=1
                || palette.NeutralBackdrop.renderQueue>=(int)RenderQueue.Transparent))
                throw new ArgumentException("DetailedTransparentGlassAndOpaqueNeutralBackdropRequired");
        }
        private static void ValidateSlug(string slug)
        {if(slug!="waffle"&&slug!="maekjuabi"&&slug!="touslesjours")throw new ArgumentException("DetailedStorefrontObservedSlugRequired");}
        private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        private static Color Hex(int value)=>new((value>>16&255)/255f,(value>>8&255)/255f,(value&255)/255f,1);
        private static Transform Child(Transform parent,string name,int layer)
        {var go=new GameObject(name){hideFlags=HideFlags.DontSave,layer=layer};go.transform.SetParent(parent,false);return go.transform;}
        private static Bounds TransformBounds(Bounds bounds,Matrix4x4 matrix)
        {
            var center=matrix.MultiplyPoint3x4(bounds.center); var result=new Bounds(center,Vector3.zero);
            for(var i=0;i<8;i++)result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
        }
        private static void Release(Object value)
        {if(value==null)return;if(UnityEngine.Application.isPlaying)Object.Destroy(value);else Object.DestroyImmediate(value);}
    }
}
