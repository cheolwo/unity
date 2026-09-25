using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.WorldMap
{
    [Serializable] public class 서울경계점 { public float x,z; public double latitude,longitude; public Vector3 Vector(float y=0) => new Vector3(x,y,z); }
    [Serializable] public class 서울경계Ring { public 서울경계점[] points; }
    [Serializable] public class 서울경계Area { public string code,name,parentCode; public int level; public 서울경계점 center; public 서울경계Ring[] rings; public 서울경계점[] triangles; }
    [Serializable] public class 광역경계Context
    {
        public string schemaVersion,sourceHash,attribution;public 서울경계Area[] areas;
        public 수도권육지Patch landPatch;
        public 지형높이Patch terrainPatch;
        public void Validate()
        {
            landPatch?.Validate();
            terrainPatch?.Validate();
            if(schemaVersion!="korea-admin1-context.v1"||sourceHash==null||sourceHash.Length!=64||string.IsNullOrWhiteSpace(attribution)||areas==null||areas.Length!=28)
                throw new ArgumentException("AdministrativeContextInvalid");
            if(areas.Any(a=>a==null||string.IsNullOrEmpty(a.code))||areas.Select(a=>a.code).Distinct().Count()!=28)throw new ArgumentException("AdministrativeContextIdentityInvalid");
            foreach(var a in areas)
            {
                if(a.level!=-1||a.parentCode!="KOR"&&a.parentCode!="PRK"||a.rings==null||a.rings.Length==0||a.rings.Any(r=>r==null||r.points==null||r.points.Length<4))throw new ArgumentException("AdministrativeContextAreaInvalid");
                foreach(var p in a.rings.SelectMany(r=>r.points).Append(a.center))
                    if(p==null||!(p.latitude>32&&p.latitude<44&&p.longitude>123&&p.longitude<132))throw new ArgumentException("AdministrativeContextCoordinateInvalid");
            }
        }
    }
    [Serializable] public class 수도권육지Patch
    {
        public double west,east,south,north;
        public 서울경계점[] vertices;public int[] indices;
        public void Validate()
        {
            // 첫 교체 범위를 기존 0.25도 격자와 맞춘다. 임의 영역 삭제를 허용하지 않는다.
            if(west!=125||east!=128||south!=36||north!=39||vertices==null||vertices.Length<3||vertices.Length>20000||indices==null||indices.Length==0||indices.Length%3!=0||indices.Length>120000)
                throw new ArgumentException("CapitalLandPatchInvalid");
            if(vertices.Any(p=>p==null||!(p.latitude>=south&&p.latitude<=north&&p.longitude>=west&&p.longitude<=east))||indices.Any(i=>i<0||i>=vertices.Length))
                throw new ArgumentException("CapitalLandPatchGeometryInvalid");
        }
        public bool Contains(double latitude,double longitude)=>latitude>south&&latitude<north&&longitude>west&&longitude<east;
    }
    [Serializable] public class 지형높이Patch
    {
        public string schemaVersion,sourceHash,riverSourceHash,review;
        public double west,east,south,north;
        public int width;
        public float[] heightMeters;
        public 서울경계Ring[] rivers;
        public void Validate()
        {
            if(schemaVersion!="globe-relief-review.v1"||review!="LocalEditorOnly_NotForDistribution"||sourceHash?.Length!=64||riverSourceHash?.Length!=64||width<2||width>201||heightMeters==null||heightMeters.Length!=width*width||rivers==null||rivers.Length==0)
                throw new ArgumentException("ReliefReviewContractInvalid");
            if(!(west>=127&&east<=128&&west<east&&south>=37&&north<=38&&south<north)||heightMeters.Any(h=>!(h>=-100&&h<3000)))throw new ArgumentException("ReliefReviewHeightInvalid");
            foreach(var ring in rivers)
                if(ring?.points==null||ring.points.Length<2||ring.points.Any(p=>p==null||!(p.longitude>=west&&p.longitude<=east&&p.latitude>=south&&p.latitude<=north)))throw new ArgumentException("ReliefReviewRiverInvalid");
        }
        public float Sample(double latitude,double longitude)
        {
            float x=Mathf.Clamp01((float)((longitude-west)/(east-west)))*(width-1),y=Mathf.Clamp01((float)((north-latitude)/(north-south)))*(width-1);
            int ix=Mathf.Min((int)x,width-2),iy=Mathf.Min((int)y,width-2);
            return Mathf.Lerp(Mathf.Lerp(heightMeters[iy*width+ix],heightMeters[iy*width+ix+1],x-ix),Mathf.Lerp(heightMeters[(iy+1)*width+ix],heightMeters[(iy+1)*width+ix+1],x-ix),y-iy);
        }
    }
    [Serializable] public class 서울경계사본
    {
        public string schemaVersion,sourceDate,attribution; public bool distributionApproved,observationPresentationOnly,traversalReady;
        public float metersPerUnit; public 서울경계Area[] areas;
        public 광역경계Context context;
        public void Validate()
        {
            if(context!=null){if(schemaVersion!="seoul-intermediate-preview.v3")throw new ArgumentException("ContextRequiresGeographicData");context.Validate();}
            bool geographic=schemaVersion=="seoul-intermediate-preview.v3";
            bool expanded=geographic || schemaVersion=="seoul-intermediate-preview.v2";
            int total=expanded?89:41;
            if((!expanded && schemaVersion!="seoul-intermediate-preview.v1") || distributionApproved || !observationPresentationOnly || traversalReady || metersPerUnit!=1000
                || areas==null || areas.Length!=total || areas.Any(a=>a==null) || areas.Count(a=>a.level==0)!=25 || areas.Count(a=>a.level==1)!=total-25
                || areas.Select(a=>a.code).Distinct().Count()!=total) throw new ArgumentException("SeoulPreviewContractInvalid");
            if(expanded)
                foreach(var pair in new Dictionary<string,int>{{"11260",16},{"11230",14},{"11215",15},{"11350",19}})
                    if(!areas.Any(a=>a.level==0&&a.code==pair.Key) || areas.Count(a=>a.level==1&&a.parentCode==pair.Key&&a.code.StartsWith(pair.Key,StringComparison.Ordinal))!=pair.Value)
                        throw new ArgumentException("SeoulPreviewParentInvalid");
            foreach(var a in areas)
                if(a.triangles==null || a.triangles.Length%3!=0 || a.rings==null || a.triangles.Any(p=>float.IsNaN(p.x)||float.IsNaN(p.z)||Mathf.Abs(p.x)>100||Mathf.Abs(p.z)>100))
                    throw new ArgumentException("SeoulPreviewGeometryInvalid");
            if(geographic)
                foreach(var a in areas)
                    foreach(var p in a.triangles.Concat(a.rings.SelectMany(r=>r.points)).Append(a.center))
                        if(p==null || !(p.latitude>37&&p.latitude<38&&p.longitude>126&&p.longitude<128))
                            throw new ArgumentException("SeoulGeographicCoordinateInvalid");
        }
    }
    // 로컬 검토 사본을 같은 Scene의 별도 표현 루트에만 결속한다. 업무 상태 쓰기 없음.
    public sealed class 서울중간공간View : MonoBehaviour
    {
        // 2023 경계에서 사가정 원점(127.0884106,37.5806971)을 포함하는 동.
        // 동 전체 재현이 아닌 기존 역세권 일부 보기 연결이다.
        public static bool HasStationPreview(string code) => code=="11260570";
        public static bool ContainsPoint(서울경계Area area,Vector2 point)
        {
            for(int i=0;i<area.triangles.Length;i+=3)
            {
                var a=area.triangles[i];var b=area.triangles[i+1];var c=area.triangles[i+2];
                float s1=(b.x-a.x)*(point.y-a.z)-(b.z-a.z)*(point.x-a.x);
                float s2=(c.x-b.x)*(point.y-b.z)-(c.z-b.z)*(point.x-b.x);
                float s3=(a.x-c.x)*(point.y-c.z)-(a.z-c.z)*(point.x-c.x);
                if(Mathf.Abs((b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x))<.0000001f)continue;
                if((s1>=0&&s2>=0&&s3>=0)||(s1<=0&&s2<=0&&s3<=0))return true;
            }
            return false;
        }
        public string SelectionMessage {get;private set;}="";
        private 세계지구본View globe;
        private 서울경계사본 data;
        private 행정구역Hierarchy hierarchy;
        private bool curved, cameraOwned, previousGlobeEnabled;
        private Transform surface;
        private Vector3 previousPosition,focus;
        private Quaternion previousRotation;
        private float previousNear,previousFar,previousFov;
        private readonly List<(Renderer renderer,bool enabled)> hiddenRenderers=new();
        private const float Radius=18.076f, KmScale=18f/6371f;
        private readonly List<Rect> labelRects=new();
        private readonly Dictionary<string,MeshCollider> boundaryColliders=new();
        private readonly List<LineRenderer> contextLines=new();
        private GameObject terrainRoot;
        private readonly List<LineRenderer> riverLines=new();
        private bool terrainInspection;
        public int TerrainVertexCount=>data?.context?.terrainPatch?.heightMeters.Length??0;
        public bool IsTerrainVisible=>terrainRoot!=null&&terrainRoot.activeInHierarchy;
        public int ContextAreaCount=>data?.context?.areas.Length??0;
        public void ShowOverview(){if(ContextAreaCount>0)ShowContext(false);else ShowLevel(0);}
        public void ShowContext(bool capital)
        {
            terrainInspection=false;
            if(ContextAreaCount==0)return;
            selectedLevel=-1;selectedName=capital?"수도권":"한반도 광역 참고";SelectionMessage="";
            focus=지구위경도선View.Coordinate(capital?37.5:38.5,capital?127:127.5,Radius);
            targetSize=capital?100:500;BeginObservation();RefreshDongs();
        }
        public void ShowRelief()
        {
            var patch=data?.context?.terrainPatch;if(patch==null)return;
            ShowContext(true);terrainInspection=true;selectedName="한강·경기 동부 실제 높이 표본";
            focus=지구위경도선View.Coordinate((patch.south+patch.north)/2,(patch.west+patch.east)/2,18.036f);targetSize=55;
            SelectionMessage="Copernicus DSM · 높이 1배 / 약 500m 표본 · 한강 NE 참고선\n건물·수목 포함 표면 높이 / 하상·실제 강 폭 아님 / 로컬 검토 전용";
        }
        public bool IsCurved => curved;
        public static bool IsRegionInFocus(Vector3 viewport,bool frontFacing) => frontFacing && viewport.z>0
            && viewport.x>=.25f && viewport.x<=.75f && viewport.y>=.2f && viewport.y<=.8f;
        public bool CanEnterFromGlobe
        {
            get
            {
                if(data==null||globe==null||surface==null||!globe.IsGlobeVisible)return false;
                var camera=globe.transform.Find(세계지구본View.GlobeCameraName).GetComponent<Camera>();
                var local=지구위경도선View.Coordinate(37.5665,126.978,Radius);
                var world=surface.TransformPoint(local);
                return IsRegionInFocus(camera.WorldToViewportPoint(world),Vector3.Dot(surface.TransformDirection(local.normalized),camera.transform.position-world)>0);
            }
        }
        private Vector3 Point(서울경계점 p,float height=0) => curved
            ? 지구위경도선View.Coordinate(p.latitude,p.longitude,Radius+height*KmScale):p.Vector(height);
        private Vector3 World(서울경계점 p) => curved?surface.TransformPoint(Point(p)):Offset+p.Vector(.1f);
        // 긴 선분의 직선 현이 구면 아래로 파묻히지 않도록 표현만 세분한다.
        // 원본 꼭짓점을 유지하며 지리 자료의 정확도를 높였다고 해석하지 않는다.
        public static Vector3[] ProjectBoundary(서울경계점[] points,float radius)
        {
            if(points==null||points.Length==0||float.IsNaN(radius)||float.IsInfinity(radius)||radius<=0)
                throw new ArgumentException("BoundaryProjectionInvalid");
            var result=new List<Vector3>();
            for(int i=0;i<points.Length;i++)
            {
                var p=points[i];
                if(p==null||double.IsNaN(p.latitude)||double.IsNaN(p.longitude)||Math.Abs(p.latitude)>90||Math.Abs(p.longitude)>180)
                    throw new ArgumentException("BoundaryCoordinateInvalid");
                var current=지구위경도선View.Coordinate(p.latitude,p.longitude,radius);
                if(i>0)
                {
                    var previous=result[result.Count-1];
                    int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Angle(previous,current)/.01f));
                    for(int step=1;step<steps;step++)result.Add(Vector3.Slerp(previous,current,(float)step/steps).normalized*radius);
                }
                result.Add(current);
            }
            return result.ToArray();
        }
        private GameObject root;
        private MeshFilter coarseLand;
        private Mesh originalLand,trimmedLand;
        public int LandPatchTriangleCount=>data?.context?.landPatch?.indices.Length/3??0;
        private Camera mapCamera;
        private readonly List<GameObject> dongRoots=new List<GameObject>();
        private readonly List<GameObject> districtRoots=new List<GameObject>();
        private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        private float targetSize=24;
        private bool waitingReturn;
        private Vector3 targetCenter;
        private string selectedDistrict="11260";
        private string selectedName="중랑구";
        private int selectedLevel;
        private static readonly Vector3 Offset=new Vector3(10000,0,10000);
        public bool IsVisible {get;private set;}
        public string Stage => selectedLevel==0 ? "서울" : selectedLevel==2 ? "사가정 역세권" : selectedName;
        public int AreaCount => data?.areas.Length??0;
        public void Load(string json,세계지구본View owner)
        {
            var candidate=JsonUtility.FromJson<서울경계사본>(json); candidate.Validate();
            var candidateHierarchy=행정구역Hierarchy.FromSeoul(candidate);
            Clear(); data=candidate;hierarchy=candidateHierarchy;globe=owner;
            curved=data.schemaVersion=="seoul-intermediate-preview.v3";
            surface=owner.transform.Find(세계지구본View.GlobeVisualRootName);
            // Domain reload로 비직렬화 참조가 끊긴 이전 로컬 검토 루트만 회수한다.
            foreach(var stale in Resources.FindObjectsOfTypeAll<GameObject>())
                if(stale.name=="서울 중간 경계 로컬 검토" && (stale.hideFlags&HideFlags.DontSave)==HideFlags.DontSave)
                    DestroyImmediate(stale);
            root=new GameObject("서울 중간 경계 로컬 검토"){hideFlags=HideFlags.DontSave};root.transform.position=Offset;
            root.transform.SetParent(owner.transform,true);
            if(curved){root.transform.SetParent(surface,false);root.transform.localPosition=Vector3.zero;}
            var shader=Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            if(curved&&data.context?.landPatch!=null)BuildLandPatch(data.context.landPatch,shader);
            if(curved&&data.context?.terrainPatch!=null)BuildTerrain(data.context.terrainPatch,shader);
            if(data.context!=null)
                foreach(var area in data.context.areas)
                    foreach(var ring in area.rings)
                    {
                        var node=new GameObject("광역참고 "+area.code);node.transform.SetParent(root.transform,false);node.layer=세계지구본View.GlobeLayer;
                        var line=node.AddComponent<LineRenderer>();line.useWorldSpace=false;
                        var positions=ProjectBoundary(ring.points,Radius+.0002f);line.positionCount=positions.Length;
                        line.SetPositions(positions);
                        line.sharedMaterial=Material(shader,area.parentCode=="KOR"?new Color(.95f,.83f,.5f):new Color(.65f,.73f,.78f));contextLines.Add(line);
                    }
            foreach(var area in data.areas)
            {
                var node=new GameObject(area.code);node.transform.SetParent(root.transform,false);node.layer=29;
                if(curved)node.layer=세계지구본View.GlobeLayer;
                if(area.level==1)dongRoots.Add(node);
                else districtRoots.Add(node);
                var mesh=new Mesh {name=area.code, indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};owned.Add(mesh);
                mesh.vertices=area.triangles.Select(p=>Point(p,area.level*.025f)).ToArray();
                // 양면을 명시하여 원본 링 방향에 관계없이 관찰한다.
                var indices=new List<int>();for(int i=0;i<mesh.vertexCount;i+=3){indices.AddRange(new[]{i,i+1,i+2,i+2,i+1,i});}
                mesh.triangles=indices.ToArray();mesh.RecalculateBounds();
                node.AddComponent<MeshFilter>().sharedMesh=mesh;
                if(curved){var collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;boundaryColliders.Add(area.code,collider);}
                var color=area.level==1?new Color(.28f,.47f,.43f):area.code=="11260"?new Color(.38f,.64f,.51f):new Color(.24f,.34f,.38f);
                var material=new Material(shader){color=color};owned.Add(material);node.AddComponent<MeshRenderer>().sharedMaterial=material;
                foreach(var ring in area.rings)
                {
                    var lineNode=new GameObject("경계");lineNode.layer=29;lineNode.transform.SetParent(node.transform,false);
                    if(curved)lineNode.layer=세계지구본View.GlobeLayer;
                    var line=lineNode.AddComponent<LineRenderer>();line.useWorldSpace=false;
                    var positions=curved?ProjectBoundary(ring.points,Radius+(.04f+area.level*.025f)*KmScale):ring.points.Select(p=>Point(p,.04f+area.level*.025f)).ToArray();
                    line.positionCount=positions.Length;line.SetPositions(positions);
                    line.widthMultiplier=(area.level==1?.012f:.028f)*(curved?KmScale:1);line.sharedMaterial=Material(shader,new Color(.63f,.78f,.77f));
                }
            }
            if(curved){mapCamera=owner.transform.Find(세계지구본View.GlobeCameraName).GetComponent<Camera>();root.SetActive(false);return;}
            var cameraNode=new GameObject("서울 중간 관찰 Camera");cameraNode.transform.SetParent(root.transform,false);
            mapCamera=cameraNode.AddComponent<Camera>();mapCamera.orthographic=true;mapCamera.orthographicSize=24;
            mapCamera.cullingMask=1<<29;mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=new Color(.08f,.13f,.18f);
            mapCamera.nearClipPlane=.1f;mapCamera.farClipPlane=200;mapCamera.depth=500;
            cameraNode.transform.localPosition=new Vector3(0,80,-28);cameraNode.transform.LookAt(Offset);
            root.SetActive(false);
        }
        private Material Material(Shader shader,Color color){var m=new Material(shader){color=color};owned.Add(m);return m;}
        private void BuildTerrain(지형높이Patch patch,Shader shader)
        {
            terrainRoot=new GameObject("실제 DSM 곡면 지형 · 로컬 검토");terrainRoot.transform.SetParent(root.transform,false);terrainRoot.layer=세계지구본View.GlobeLayer;
            var mesh=new Mesh{name="CopernicusGlobeRelief",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};owned.Add(mesh);
            var vertices=new Vector3[patch.heightMeters.Length];int n=patch.width;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)vertices[y*n+x]=TerrainPoint(patch,patch.north-(patch.north-patch.south)*y/(n-1),patch.west+(patch.east-patch.west)*x/(n-1));
            var indices=new List<int>();
            for(int y=0;y<n-1;y++)for(int x=0;x<n-1;x++)
            {
                int a=y*n+x,b=a+1,c=a+n,d=c+1;
                foreach(var triangle in new[]{new[]{a,b,c},new[]{b,d,c}})
                {if(Vector3.Dot(Vector3.Cross(vertices[triangle[1]]-vertices[triangle[0]],vertices[triangle[2]]-vertices[triangle[0]]),vertices[triangle[0]])<0)Array.Reverse(triangle);indices.AddRange(triangle);}
            }
            mesh.vertices=vertices;mesh.triangles=indices.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            terrainRoot.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=terrainRoot.AddComponent<MeshRenderer>();renderer.sharedMaterial=coarseLand.GetComponent<MeshRenderer>().sharedMaterial;
            foreach(var river in patch.rivers)
            {
                var node=new GameObject("한강 · 위치 참고선");node.transform.SetParent(terrainRoot.transform,false);node.layer=세계지구본View.GlobeLayer;
                var line=node.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=river.points.Length;
                line.SetPositions(river.points.Select(p=>TerrainPoint(patch,p.latitude,p.longitude,.0002f)).ToArray());
                line.sharedMaterial=Material(shader,new Color(.12f,.68f,1));riverLines.Add(line);
            }
            terrainRoot.SetActive(false);
        }
        private static Vector3 TerrainPoint(지형높이Patch p,double latitude,double longitude,float offset=0)
            =>지구위경도선View.Coordinate(latitude,longitude,18.0352f+p.Sample(latitude,longitude)*KmScale/1000f+offset);
        private void BuildLandPatch(수도권육지Patch patch,Shader shader)
        {
            coarseLand=surface.Find("LandObservationSurface")?.GetComponent<MeshFilter>();
            if(coarseLand==null)throw new InvalidOperationException("CoarseLandMissing");
            originalLand=coarseLand.sharedMesh;
            trimmedLand=Instantiate(originalLand);owned.Add(trimmedLand);
            var vertices=originalLand.vertices;var source=originalLand.triangles;var kept=new List<int>();
            for(int i=0;i<source.Length;i+=3)
            {
                var center=(vertices[source[i]]+vertices[source[i+1]]+vertices[source[i+2]]).normalized;
                var coordinate=new Ssalddel.Unity.Runtime.WorldMap.세계지구구면좌표(-center.x,center.y,center.z).ToLatitudeLongitude();
                if(!patch.Contains(coordinate.Latitude,coordinate.Longitude))kept.AddRange(new[]{source[i],source[i+1],source[i+2]});
            }
            trimmedLand.triangles=kept.ToArray();
            var node=new GameObject("수도권 해안 폴리곤 육지");node.transform.SetParent(root.transform,false);node.layer=세계지구본View.GlobeLayer;
            var mesh=new Mesh{name="CapitalCoastPolygon",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};owned.Add(mesh);
            mesh.vertices=patch.vertices.Select(p=>지구위경도선View.Coordinate(p.latitude,p.longitude,18.035f)).ToArray();
            var doubleSided=new List<int>();for(int i=0;i<patch.indices.Length;i+=3){int a=patch.indices[i],b=patch.indices[i+1],c=patch.indices[i+2];doubleSided.AddRange(new[]{a,b,c,c,b,a});}
            mesh.triangles=doubleSided.ToArray();mesh.normals=mesh.vertices.Select(p=>p.normalized).ToArray();mesh.RecalculateBounds();
            node.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial=coarseLand.GetComponent<MeshRenderer>().sharedMaterial;
        }
        private void BeginObservation()
        {
            IsVisible=true;root.SetActive(true);
            if(!curved){globe.SetRegionalPreviewVisible(true);return;}
            if(cameraOwned)return;
            if(coarseLand!=null&&trimmedLand!=null)coarseLand.sharedMesh=trimmedLand;
            previousPosition=mapCamera.transform.position;previousRotation=mapCamera.transform.rotation;
            previousNear=mapCamera.nearClipPlane;previousFar=mapCamera.farClipPlane;previousFov=mapCamera.fieldOfView;
            previousGlobeEnabled=globe.enabled;globe.enabled=false;
            globe.SetRegionalPreviewVisible(true);mapCamera.enabled=true;cameraOwned=true;
            // 광역 기호와 위경도선은 상세 경계보다 크므로 관찰 중에만 숨기고 원상 복원한다.
            foreach(var r in surface.GetComponentsInChildren<Renderer>(true))
                if(!r.transform.IsChildOf(root.transform) && (r is LineRenderer || r.transform.GetComponentInParent<지구위경도선View>()!=null))
                {hiddenRenderers.Add((r,r.enabled));r.enabled=false;}
            foreach(var name in new[]{"2025 해상·항공 통계 관계 · 실제 항로 아님","가상 해양 생활 · 실제 항로 아님"})
            {
                var symbols=surface.Find(name);if(symbols==null)continue;
                foreach(var r in symbols.GetComponentsInChildren<Renderer>(true))
                    if(!hiddenRenderers.Any(item=>item.renderer==r)){hiddenRenderers.Add((r,r.enabled));r.enabled=false;}
            }
        }
        private void RestoreCamera()
        {
            if(!cameraOwned)return;
            cameraOwned=false;
            if(coarseLand!=null&&originalLand!=null)coarseLand.sharedMesh=originalLand;
            mapCamera.transform.SetPositionAndRotation(previousPosition,previousRotation);
            mapCamera.nearClipPlane=previousNear;mapCamera.farClipPlane=previousFar;mapCamera.fieldOfView=previousFov;
            globe.enabled=previousGlobeEnabled;globe.SetRegionalPreviewVisible(false);
            foreach(var item in hiddenRenderers)if(item.renderer!=null)item.renderer.enabled=item.enabled;
            hiddenRenderers.Clear();
        }
        public void ShowLevel(int level)
        {
            terrainInspection=false;
            if(root==null)return;
            if(level==1){ShowDistrict("11260");return;}
            selectedLevel=level;targetCenter=Vector3.zero;
            BeginObservation();
            if(curved)focus=지구위경도선View.Coordinate(level==2?37.5806971:37.5665,level==2?127.0884106:126.978,Radius);
            targetSize=level==0?24:.85f;
            if(level==2){selectedDistrict="11260";selectedName="중랑구";}
            RefreshDongs();
        }
        public bool ShowDistrict(string code)
        {
            var area=data?.areas.FirstOrDefault(a=>a.level==0&&a.code==code);
            if(area==null)return false;
            if(!hierarchy.HasChildren(code)){SelectionMessage=area.name+" · 하위 행정 경계 자료 준비 중";return false;}
            var bounds=new Bounds(area.triangles[0].Vector(),Vector3.zero);
            foreach(var p in area.triangles)bounds.Encapsulate(p.Vector());
            selectedDistrict=code;selectedName=area.name;selectedLevel=1;SelectionMessage="";
            targetCenter=bounds.center;
            targetSize=Mathf.Max(bounds.extents.z,bounds.extents.x/Mathf.Max(.5f,mapCamera.aspect))*1.35f;
            BeginObservation();if(curved)focus=Point(area.center);RefreshDongs();return true;
        }
        private void RefreshDongs(){foreach(var item in dongRoots)item.SetActive(selectedLevel>0&&hierarchy.IsChild(item.name,selectedDistrict));}
        public void ReturnGlobe()
        {
            RestoreCamera();IsVisible=false;waitingReturn=false;if(root!=null)root.SetActive(false);globe.SetRegionalPreviewVisible(false);
        }
        public bool EnterDiorama()
        {
            if(!IsVisible || selectedLevel!=2)return false;
            return OpenPreparedDiorama();
        }
        public bool SelectDong(string code)
        {
            if(!IsVisible || selectedLevel==0)return false;
            var area=data.areas.FirstOrDefault(a=>a.level==1&&a.code==code&&hierarchy.IsChild(a.code,selectedDistrict));
            if(area==null)return false;
            if(!HasStationPreview(code)){SelectionMessage=area.name+" · 디오라마 준비 중";return false;}
            SelectionMessage=area.name+" · 사가정 역세권 일부 보기 (동 전체 재현 아님)";
            if(OpenPreparedDiorama())return true;
            SelectionMessage=area.name+" · 사가정 모듈을 열 수 없습니다. 지도를 유지합니다.";return false;
        }
        private bool OpenPreparedDiorama()
        {
            RestoreCamera();
            if(!globe.OpenRegionalDiorama()){BeginObservation();return false;}
            IsVisible=false;root.SetActive(false);waitingReturn=true;return true;
        }
        void Update()
        {
            if(waitingReturn && !globe.IsDioramaTransitioning && globe.IsGlobeVisible)
            {waitingReturn=false;BeginObservation();RefreshDongs();}
            if(!IsVisible || mapCamera==null)return;
            foreach(var line in contextLines)line.widthMultiplier=Mathf.Max(.00004f,targetSize*KmScale*.006f);
            if(terrainRoot!=null)terrainRoot.SetActive(IsVisible&&selectedLevel<0&&targetSize<=120);
            foreach(var district in districtRoots)district.SetActive(!terrainInspection);
            foreach(var line in riverLines)line.widthMultiplier=Mathf.Max(.000035f,targetSize*KmScale*.008f);
            if(curved)
            {
                var worldFocus=surface.TransformPoint(focus);var normal=surface.TransformDirection(focus.normalized);
                var north=surface.TransformDirection(Vector3.ProjectOnPlane(Vector3.up,focus).normalized);
                float distance=targetSize*KmScale/Mathf.Tan(24*Mathf.Deg2Rad),blend=1-Mathf.Exp(-Time.unscaledDeltaTime*5);
                var current=mapCamera.transform.position-surface.position;
                var desired=worldFocus+normal*distance-(terrainInspection?north*distance*.6f:Vector3.zero)-surface.position;
                mapCamera.transform.position=surface.position+Vector3.Slerp(current.normalized,desired.normalized,blend)*Mathf.Lerp(current.magnitude,desired.magnitude,blend);
                mapCamera.transform.rotation=Quaternion.Slerp(mapCamera.transform.rotation,Quaternion.LookRotation(worldFocus-mapCamera.transform.position,north),blend);
                mapCamera.nearClipPlane=Mathf.Max(.00003f,distance*.005f);mapCamera.farClipPlane=100;mapCamera.fieldOfView=48;
            }
            else
            {
                mapCamera.orthographicSize=Mathf.Lerp(mapCamera.orthographicSize,targetSize,1-Mathf.Exp(-Time.unscaledDeltaTime*7));
                mapCamera.transform.localPosition=Vector3.Lerp(mapCamera.transform.localPosition,targetCenter+new Vector3(0,80,-28),1-Mathf.Exp(-Time.unscaledDeltaTime*7));
            }
            var mouse=Mouse.current;if(mouse==null)return;
            var position=mouse.position.ReadValue();if(position.y>Screen.height-110||position.y<50)return;
            var scroll=mouse.scroll.ReadValue().y/120f;
            if(scroll>0 && targetSize<=.85f){if(selectedLevel==2)EnterDiorama();return;}
            float maximum=selectedLevel<0?500:24;
            if(scroll<0 && targetSize>=maximum){ReturnGlobe();return;}
            // 확대만으로 중랑구를 임의 선택하지 않는다. 사용자가 선택한 지역의 자료만 연다.
            if(Mathf.Abs(scroll)>.01f)targetSize=Mathf.Clamp(targetSize*Mathf.Pow(.78f,scroll),.85f,maximum);
        }
        void OnGUI()
        {
            if(data==null)return;
            if(!IsVisible){if(!waitingReturn && CanEnterFromGlobe && globe.CameraDistance<=35 && GUI.Button(new Rect(18,76,190,40),"행정 권역 확대"))ShowOverview();return;}
            if(selectedLevel<0)
            {
                if(GUI.Button(new Rect(18,18,100,34),"지구본"))ReturnGlobe();
                if(GUI.Button(new Rect(128,18,100,34),"한반도 권역"))ShowContext(false);
                if(GUI.Button(new Rect(238,18,100,34),"수도권"))ShowContext(true);
                if(data.context.terrainPatch!=null&&GUI.Button(new Rect(458,18,120,34),"산·강 표본"))ShowRelief();
                if(GUI.Button(new Rect(348,18,100,34),"서울 상세"))ShowLevel(0);
                labelRects.Clear();
                foreach(var area in data.context.areas)
                {
                    var p=mapCamera.WorldToScreenPoint(World(area.center));var rect=new Rect(p.x-45,Screen.height-p.y,90,24);
                    if(p.z<=0||rect.x<0||rect.xMax>Screen.width||rect.y<80||rect.yMax>Screen.height-90)continue;
                    var originalY=rect.y;
                    foreach(var offset in new[]{0,-26,26,-52,52})
                    {rect.y=originalY+offset;if(rect.y>=80&&rect.yMax<Screen.height-90&&!labelRects.Any(r=>r.Overlaps(rect)))break;}
                    if(rect.y<80||rect.yMax>=Screen.height-90||labelRects.Any(r=>r.Overlaps(rect)))continue;
                    labelRects.Add(rect);
                    if(GUI.Button(rect,area.name))
                    {
                        if(area.code=="KOR-2497")ShowLevel(0);
                        else{focus=Point(area.center);targetSize=80;selectedName=area.name;SelectionMessage=area.name+" · 광역 참고 경계 / 시군구·행정동 자료 미연결";}
                    }
                }
                GUI.Label(new Rect(18,Screen.height-70,Screen.width-36,60),Stage+" · "+data.context.attribution+"\n"+SelectionMessage);return;
            }
            GUI.Box(new Rect(12,12,560,52),"");
            if(GUI.Button(new Rect(20,22,110,32),"한반도로"))ReturnGlobe();
            if(GUI.Button(new Rect(140,22,90,32),"서울"))ShowLevel(0);
            if(ContextAreaCount>0&&GUI.Button(new Rect(580,22,100,32),"수도권으로"))ShowContext(true);
            if(GUI.Button(new Rect(240,22,90,32),"중랑구"))ShowLevel(1);
            if(GUI.Button(new Rect(340,22,100,32),"사가정"))ShowLevel(2);
            if(selectedLevel==2 && GUI.Button(new Rect(450,22,110,32),"디오라마 진입"))EnterDiorama();
            var prepared=hierarchy.Children(null).Where(a=>hierarchy.HasChildren(a.Id)).ToArray();
            for(int i=0;i<prepared.Length;i++)if(GUI.Button(new Rect(20+i*110,65,100,32),prepared[i].Name))ShowDistrict(prepared[i].Id);
            GUI.Label(new Rect(20,Screen.height-42,900,32),Stage+" · "+data.sourceDate+" 참고 경계 · "+data.attribution+" · 휠 확대/축소");
            labelRects.Clear();
            foreach(var area in data.areas)
            {
                if(area.level!=(selectedLevel==0?0:1) || (area.level==1&&!hierarchy.IsChild(area.code,selectedDistrict)))continue;
                var p=mapCamera.WorldToScreenPoint(World(area.center));
                if(p.z>0 && p.x>40 && p.x<Screen.width-100 && p.y>60 && p.y<Screen.height-100)
                {
                    var rect=new Rect(p.x-35,Screen.height-p.y,105,24);
                    if(labelRects.Any(r=>r.Overlaps(rect)))continue;
                    labelRects.Add(new Rect(rect.x-3,rect.y-3,rect.width+6,rect.height+6));
                    if(area.level==1){if(GUI.Button(rect,area.name+(HasStationPreview(area.code)?" ↗":"")))SelectDong(area.code);}
                    else if(GUI.Button(rect,area.name))ShowDistrict(area.code);
                }
            }
            if(!string.IsNullOrEmpty(SelectionMessage))GUI.Box(new Rect(20,Screen.height-82,Screen.width-40,34),SelectionMessage);
            // 지명 버튼 외에 실제 행정동 도형도 같은 선택 경로를 사용한다.
            var ev=Event.current;
            if(curved && IsVisible && ev.type==EventType.MouseDown && ev.button==0 && ev.mousePosition.y>110 && ev.mousePosition.y<Screen.height-90)
            {
                var ray=mapCamera.ScreenPointToRay(new Vector3(ev.mousePosition.x,Screen.height-ev.mousePosition.y,0));
                foreach(var area in data.areas)
                {
                    if(area.level!=(selectedLevel==0?0:1) || (area.level==1&&!hierarchy.IsChild(area.code,selectedDistrict)))continue;
                    if(boundaryColliders[area.code].Raycast(ray,out var hit,100))
                    {if(area.level==0)ShowDistrict(area.code);else SelectDong(area.code);ev.Use();break;}
                }
            }
            if(!curved && IsVisible && selectedLevel!=0 && ev.type==EventType.MouseDown && ev.button==0 && ev.mousePosition.y>110 && ev.mousePosition.y<Screen.height-90)
            {
                var ray=mapCamera.ScreenPointToRay(new Vector3(ev.mousePosition.x,Screen.height-ev.mousePosition.y,0));
                if(new Plane(Vector3.up,Offset+Vector3.up*.025f).Raycast(ray,out float distance))
                {
                    var p=ray.GetPoint(distance)-Offset;
                    var hit=data.areas.FirstOrDefault(a=>a.level==1&&hierarchy.IsChild(a.code,selectedDistrict)&&ContainsPoint(a,new Vector2(p.x,p.z)));
                    if(hit!=null){SelectDong(hit.code);ev.Use();}
                }
            }
            if(selectedLevel==2){var station=mapCamera.WorldToScreenPoint(curved?surface.TransformPoint(focus):Offset);
            GUI.Label(new Rect(station.x-45,Screen.height-station.y-24,130,28),"● 사가정역");}
        }
        void Clear(){RestoreCamera();if(root!=null)DestroyImmediate(root);foreach(var obj in owned)if(obj!=null)DestroyImmediate(obj);owned.Clear();coarseLand=null;originalLand=null;trimmedLand=null;terrainRoot=null;riverLines.Clear();terrainInspection=false;dongRoots.Clear();districtRoots.Clear();boundaryColliders.Clear();contextLines.Clear();IsVisible=false;waitingReturn=false;}
        void OnDestroy(){if(IsVisible&&globe!=null)globe.SetRegionalPreviewVisible(false);Clear();}
    }
}
