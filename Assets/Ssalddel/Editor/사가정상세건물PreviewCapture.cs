using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static partial class 사가정영상선분디오라마DemoCapture
    {
        private static JObject CaptureDetailedBuildings(Transform root, Transform originalStudy, Transform route,
            Light mainLight, Camera[] oldCameras, Vector3[] oldCenters, Building[] buildings, Inputs inputs, JObject input,
            JObject labels, 사가정외관ModuleCatalog catalog, string studyHash, string output)
        {
            var folder = Path.Combine(output, "detailed-buildings"); Directory.CreateDirectory(folder);
            inputs.Read(input["detailedMaterialReceipt"]);
            var receipt = input["detailedMaterialReceipt"];
            var envelopeStyle = input.Value<string>("buildingEnvelopeStyle");
            if (!string.IsNullOrWhiteSpace(envelopeStyle) && envelopeStyle != "source-height-glass-r1")
                throw new InvalidOperationException("DetailedBuildingEnvelopeStyleRejected");
            var useEnvelope = envelopeStyle == "source-height-glass-r1";
            var envelopeReference = input["buildingEnvelopeEvidence"];
            var envelopeEvidence = useEnvelope ? inputs.Read(envelopeReference) : null;
            if (useEnvelope) inputs.Read(input["buildingEnvelopeReview"]);
            var owned = new List<Object>();
            var smallParts = new List<사가정외관OverlayRenderer>();
            var models = new List<사가정상세점포ModelBuilder.Model>();
            var streetCameras = new List<Camera>();
            var originalActive = originalStudy.gameObject.activeSelf;
            var container = Child(root, "비공개 상세 건물 교체 연구 · 실측 외형 아님");
            사가정상세건물표현Renderer renderer = null;
            사가정비공개현실감Style.Scope lighting = null;
            사가정상세표면MaterialLibrary library = null;
            var restored = false;
            try
            {
                originalStudy.gameObject.SetActive(false);
                library = new 사가정상세표면MaterialLibrary(receipt.Value<string>("path"), receipt.Value<string>("sha256"));
                var targets = new Dictionary<string, 사가정상세건물표현Target>(StringComparer.Ordinal);
                var assets = new Dictionary<string, 사가정상세건물표현Asset>(StringComparer.Ordinal);
                var profiles = new Dictionary<string, 사가정건물외관Profile>(StringComparer.Ordinal);
                var modelReports = new JArray();
                var envelopeBounds = new Dictionary<string, Bounds>(StringComparer.Ordinal);
                foreach (var b in buildings)
                {
                    var basis = useEnvelope ? ResolveBuildingEnvelopeJson(envelopeEvidence.ToString(), b.Slug,
                        b.Id, b.Id.Split(':')[2], envelopeReference.Value<string>("path"),
                        envelopeReference.Value<string>("sha256")) : null;
                    var targetBounds = b.Bounds;
                    if (basis != null)
                        targetBounds.SetMinMax(b.Bounds.min, new Vector3(b.Bounds.max.x,
                            b.Bounds.min.y + basis.BodyHeightMeters, b.Bounds.max.z));
                    envelopeBounds.Add(b.Id, targetBounds);
                    var fallback = Child(container, "기존 간략 외형 · " + b.Slug);
                    BuildStudyMass(fallback, b, owned);
                    var oldParts = new 사가정외관OverlayRenderer(fallback,
                        사가정외관검토Gate.CreatePrivateStudy(studyHash,
                            new Dictionary<string, Bounds> { [b.Id] = b.Bounds }), ReviewLayer);
                    smallParts.Add(oldParts);
                    BuildAppearance(fallback, b, labels, inputs, catalog, oldParts, studyHash, new JArray(), owned);
                    ConstrainOwnedStudyBounds(fallback, owned);
                    targets.Add(b.Id, new 사가정상세건물표현Target(b.Id, targetBounds, fallback));
                    var source = Child(container, "상세 외형 읽기 원본 · " + b.Slug);
                    BuildDetailedBody(source, b, library, owned, basis);
                    var palette = 사가정상세점포ModelBuilder.CreatePalette(b.Slug,
                        (role, color, smoothness) => library.Create(b.Slug, role, color, smoothness), useEnvelope);
                    var model = 사가정상세점포ModelBuilder.Build(source, new 사가정상세점포ModelBuilder.Spec
                    {
                        targetBuildingId = b.Id, slug = b.Slug, localFace = b.Face, normal = b.Normal,
                        facadeWidth = b.FacadeWidth, symbolicHeight = basis?.StorefrontHeightMeters ?? SymbolicHeight,
                        transparentGlass = useEnvelope, layer = ReviewLayer
                    }, palette);
                    models.Add(model); model.VisualRoot.gameObject.SetActive(true);
                    AddDetailedSign(model.SignAnchor, model.SignSize, b, labels, inputs, owned);
                    source.gameObject.SetActive(false);
                    var key = "private-detailed-storefront:" + b.Slug;
                    var revision = useEnvelope ? "source-height-and-glass.r2" : "photo-reference-front.r1";
                    assets.Add(b.Id, new 사가정상세건물표현Asset(key, revision, source.gameObject, fallback.gameObject));
                    var clock = b.Frame.Value<string>("timeBase").Split('/');
                    profiles.Add(b.Id, new 사가정건물외관Profile
                    {
                        scopeKind = 사가정건물외관Profile.PrivateStudyScope,
                        profileStableId = "private-detailed:" + b.Slug,
                        targetBuildingId = b.Id, baseMapHashSha256 = studyHash, spatialContentHashSha256 = studyHash,
                        reviewState = 사가정건물외관Profile.ReviewedState,
                        reviewerKind = "CodexVisualReview", reviewReference = useEnvelope
                            ? "r115:source-height-unmeasured-front" : "r114:photo-reference-unmeasured-front",
                        evidenceObservationId = b.Frame.Value<string>("sampleId"),
                        evidenceSourceSha256 = b.Frame.Value<string>("sourceSha256"), evidenceSourcePts = b.Frame.Value<long>("actualPts"),
                        evidenceTimeBaseNumerator = long.Parse(clock[0]), evidenceTimeBaseDenominator = long.Parse(clock[1]),
                        visualKey = key, visualCatalogRevision = revision, moduleKind = 사가정상세건물표현Renderer.ModuleKind,
                        localPosition = Vector3.zero, localEulerAngles = Vector3.zero, localScale = Vector3.one,
                        presentationOnly = true, distributionApproved = false
                    });
                    modelReports.Add(new JObject { ["slug"] = b.Slug, ["buildingId"] = b.Id,
                        ["envelope"] = basis == null ? null : JObject.FromObject(basis),
                        ["targetBoundsMin"] = Vector(targetBounds.min), ["targetBoundsMax"] = Vector(targetBounds.max),
                        ["model"] = JObject.Parse(JsonUtility.ToJson(model.Result)), ["profile"] = JObject.Parse(JsonUtility.ToJson(profiles[b.Id])) });
                }
                streetCameras.AddRange(buildings.Select(b=>DetailedStreetCamera(container,b,root)));
                Camera envelopeCamera = null;
                if (useEnvelope)
                {
                    var allEnvelope = envelopeBounds.Values.First();
                    foreach (var bounds in envelopeBounds.Values.Skip(1)) allEnvelope.Encapsulate(bounds);
                    envelopeCamera = CameraFor(container, allEnvelope, new Vector3(1.25f, .65f, -1), 1.12f);
                    envelopeCamera.name = "공식자료 높이 전체 본체 비교 · 실제 지붕 미확인";
                }
                WriteNew(Path.Combine(folder,"camera-setup.private.json"),new JObject
                { ["cameras"]=new JArray(streetCameras.Select((c,i)=>new JObject { ["slug"]=buildings[i].Slug,
                    ["position"]=Vector(c.transform.position),["face"]=Vector(buildings[i].Face),["normal"]=Vector(buildings[i].Normal),
                    ["fieldOfView"]=c.fieldOfView,["facadeWidth"]=buildings[i].FacadeWidth,["poseIsHypothesis"]=true })) });
                // 주변 경량 모형만 기존 표현을 적용한다. 새 상세 재질과 원형은 교체하지 않는다.
                lighting = 사가정비공개현실감Style.Apply(root, mainLight, oldCameras, oldCenters, route, container);
                renderer = new 사가정상세건물표현Renderer(container, studyHash, targets, ReviewLayer);
                var before = new Dictionary<string, JObject>();
                var overviewBefore = Capture(oldCameras[0], Path.Combine(folder,"overview-before.png"));
                var envelopeBefore = envelopeCamera == null ? null : Capture(envelopeCamera,Path.Combine(folder,"envelope-before.png"));
                for (var i = 0; i < buildings.Length; i++)
                    before.Add(buildings[i].Slug, Capture(streetCameras[i], Path.Combine(folder, buildings[i].Slug + "-before.png")));
                foreach (var b in buildings)
                    if (!renderer.TryApply(profiles[b.Id], assets[b.Id], out var code)) throw new InvalidOperationException(code);
                var overviewObserver = buildings.Aggregate(Vector3.zero,(sum,b)=>sum+b.Bounds.center)/buildings.Length;
                renderer.RefreshForObserver(overviewObserver);
                var overviewAfter = Capture(oldCameras[0],Path.Combine(folder,"overview-after.png"));
                공통표면깊이CaptureIfRequested(root, oldCameras[0], buildings);
                픽업라이딩시안CaptureIfRequested(root, buildings, oldCameras[0]);
                var envelopeAfter = envelopeCamera == null ? null : Capture(envelopeCamera,Path.Combine(folder,"envelope-after.png"));
                var overview = new JObject { ["before"]=overviewBefore["image"].DeepClone(),["after"]=overviewAfter["image"].DeepClone(),
                    ["observerMatchesCamera"]=false,["observerKind"]="SharedExplicitStudyAnchor",["observer"]=Vector(overviewObserver),
                    ["representation"]=JArray.FromObject(renderer.Snapshot()) };
                var views = new JArray();
                for (var i = 0; i < buildings.Length; i++)
                {
                    var b = buildings[i]; var camera = streetCameras[i];
                    renderer.RefreshForObserver(camera.transform.position);
                    var snapshot = renderer.Snapshot();
                    var target = snapshot.Single(s => s.buildingId == b.Id);
                    if (target.level != 사가정상세건물표현Level.Detailed.ToString() || target.fallbackActive
                        || !target.detailedActive || target.middleActive) throw new InvalidOperationException("DetailedStreetTargetNotExclusive");
                    var after = Capture(camera, Path.Combine(folder, b.Slug + "-after.png"));
                    if (before[b.Slug]["image"].Value<string>("sha256") == after["image"].Value<string>("sha256"))
                        throw new InvalidOperationException("DetailedStreetChangeNotVisible");
                    views.Add(new JObject { ["slug"] = b.Slug, ["before"] = before[b.Slug]["image"].DeepClone(),
                        ["after"] = after["image"].DeepClone(), ["pixelChecks"] = new JArray(before[b.Slug], after),
                        ["observer"] = Vector(camera.transform.position), ["observerMatchesCamera"] = true,
                        ["representation"] = JArray.FromObject(snapshot), ["samePerspectiveCamera"] = true });
                }
                var testTarget = buildings[0]; var distanceChecks = new JArray();
                foreach (var distance in new[] { 0f, 85f, 500f })
                {
                    var point = distance == 0 ? envelopeBounds[testTarget.Id].center
                        : envelopeBounds[testTarget.Id].max + Vector3.right * distance;
                    renderer.RefreshForObserver(point);
                    var state = renderer.Snapshot().Single(s => s.buildingId == testTarget.Id);
                    var expected = distance == 0 ? "Detailed" : distance == 85 ? "Middle" : "Fallback";
                    if (state.level != expected || new[] { state.fallbackActive, state.middleActive, state.detailedActive }.Count(v => v) != 1)
                        throw new InvalidOperationException("DetailedDistanceFallbackMismatch");
                    distanceChecks.Add(JObject.FromObject(state));
                }
                renderer.Dispose(); renderer = null;
                var overviewRestored=Capture(oldCameras[0],Path.Combine(folder,"overview-restored.png"));
                var envelopeRestored = envelopeCamera == null ? null : Capture(envelopeCamera,Path.Combine(folder,"envelope-restored.png"));
                if (envelopeRestored != null && envelopeRestored["image"].Value<string>("sha256")
                    != envelopeBefore["image"].Value<string>("sha256"))
                    throw new InvalidOperationException("DetailedEnvelopeFallbackPixelsDiffer");
                if(overviewRestored["image"].Value<string>("sha256")!=overviewBefore["image"].Value<string>("sha256"))
                    throw new InvalidOperationException("DetailedOverviewFallbackPixelsDiffer");
                overview["restored"]=overviewRestored["image"].DeepClone();overview["restoredPixelsEqualBefore"]=true;
                for (var i = 0; i < buildings.Length; i++)
                {
                    var b = buildings[i]; var path = Path.Combine(folder, b.Slug + "-restored.png");
                    var check = Capture(streetCameras[i], path);
                    if (check["image"].Value<string>("sha256") != before[b.Slug]["image"].Value<string>("sha256"))
                        throw new InvalidOperationException("DetailedStreetFallbackPixelsDiffer");
                    var view = (JObject)views[i]; view["restored"] = check["image"].DeepClone();
                    view["restoredPixelsEqualBefore"] = true;
                    ((JArray)view["pixelChecks"]).Add(check);
                }
                restored = true;
                library.Recheck();
                var lightReport = lighting.Result;
                lighting.Dispose(); lighting = null;
                var report = new JObject
                {
                    ["schema"] = "private-detailed-building-preview-render.v1", ["views"] = views,
                    ["models"] = modelReports, ["distanceChecks"] = distanceChecks, ["overview"]=overview,
                    ["buildingEnvelopeStyle"] = useEnvelope ? envelopeStyle : null,
                    ["buildingEnvelopeEvidence"] = useEnvelope ? envelopeReference.DeepClone() : null,
                    ["envelopeView"] = envelopeBefore == null ? null : new JObject
                    { ["before"] = envelopeBefore["image"].DeepClone(), ["after"] = envelopeAfter["image"].DeepClone(),
                        ["restored"] = envelopeRestored["image"].DeepClone(),["restoredPixelsEqualBefore"] = true,
                        ["sameCamera"] = true, ["roofGeometryKnown"] = false },
                    ["distanceMetric"] = "WorldObserverToExactStudyBounds", ["detailedDistanceMeters"] = 50,
                    ["middleDistanceMeters"] = 140, ["buildingGeometryHeightMeters"] = useEnvelope ? null : new JValue(SymbolicHeight),
                    ["heightMeasured"] = false, ["frontPlacementHypothesis"] = true,
                    ["sourceHeightAppliedCount"] = modelReports.Count(m=>m["envelope"] is JObject envelope && envelope.Value<bool?>("ObservedSourceHeightApplied")==true),
                    ["floorCountConvertedToHeight"] = false, ["videoCreated"] = false,
                    ["materials"] = JObject.Parse(library.ReportJson()),
                    ["lighting"] = JObject.Parse(사가정비공개현실감Style.SerializeReport(lightReport)),
                    ["fallbackRestored"] = true, ["canonicalAppliedCount"] = 0,
                    ["originalPhotographsUsedAsTextures"] = false, ["photogrammetryPerformed"] = false,
                    ["sameSourceEvidenceReused"] = true, ["originalStudyLayerRestoredInFinally"] = true,
                    ["offscreenCameraRendering"] = true, ["gameViewVerified"] = false, ["runtimeFpsMeasured"] = false,
                    ["sourceModelsUseSynty"] = false, ["fallbackSmallPartsMayUseExistingSynty"] = true,
                    ["publicationReady"] = false
                };
                WriteNew(Path.Combine(folder, "result.private.json"), report);
                return Reference(Path.Combine(folder, "result.private.json"));
            }
            finally
            {
                renderer?.Dispose();
                foreach (var model in models) model.Dispose();
                foreach (var part in smallParts) part.Dispose();
                lighting?.Dispose();
                if (container != null) Object.DestroyImmediate(container.gameObject);
                library?.Dispose();
                foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
                originalStudy.gameObject.SetActive(originalActive);
                WriteNew(Path.Combine(folder, "cleanup.private.json"), new JObject
                {
                    ["temporaryContainerRemoved"] = container == null, ["originalStudyActiveRestored"] = originalStudy.gameObject.activeSelf == originalActive,
                    ["fallbackRestoreVerified"] = restored, ["sourceSceneSaved"] = false, ["canonicalAppliedCount"] = 0,
                    ["materialLibraryDisposed"] = library?.Result.disposed ?? false,
                    ["ownedMaterialObjectsDestroyed"] = library?.Result.ownedObjectsDestroyed ?? false,
                    ["allModelSourcesDisposed"] = models.All(m=>m.IsDisposed)
                });
            }
        }

        public static 사가정상세건물외형Basis.Result ResolveBuildingEnvelopeJson(string json,string slug,
            string targetId,string expectedSourceFeatureId,string sourceReference,string sourceSha256)
        {
            var source=JObject.Parse(json);
            if(source.Value<string>("schema")!="private-building-envelope-source-evidence.v1"
                || source["records"] is not JArray records || records.Count!=3)
                throw new InvalidOperationException("DetailedEnvelopeSourceRecordsRequired");
            var row=records.SingleOrDefault(r=>r.Value<string>("key")==slug);
            if(row==null || row.Value<string>("recordDeletionFlag")!="active"
                || row.Value<string>("gisBuildingId")!=expectedSourceFeatureId
                || "vworld:al-d010:"+expectedSourceFeatureId+":row:"+row.Value<int>("sourceRecordOrdinalZeroBased")!=targetId
                || !Sha(row.Value<string>("entireDbfRecordSha256")))
                throw new InvalidOperationException("DetailedEnvelopeExactSourceRowRequired");
            var fields=row["selectedFields"];
            if(fields?["A1"]?.Value<string>("rawValue")!=expectedSourceFeatureId
                || fields["A16"]?.Value<string>("fieldName")!="높이(m)"
                || fields["A26"]?.Value<string>("fieldName")!="지상층_수")
                throw new InvalidOperationException("DetailedEnvelopeSourceFieldMeaningRequired");
            var height=double.Parse(fields["A16"].Value<string>("rawValue"),CultureInfo.InvariantCulture);
            var floors=int.Parse(fields["A26"].Value<string>("rawValue"),CultureInfo.InvariantCulture);
            return 사가정상세건물외형Basis.Resolve(targetId,expectedSourceFeatureId,new 사가정공간표현BuildingData
            { id=targetId,sourceFeatureId=expectedSourceFeatureId,heightMeters=height>0?height:SymbolicHeight,
                heightKind=height>0?"ObservedSourceHeight":"SymbolicFallback4m",observedHeightMeters=height,
                observedAboveGroundFloors=floors,heightPolicyRevision="al-d010-exact-row.r1" },
                sourceReference+"#records["+slug+"]",sourceSha256);
        }

        private static void BuildDetailedBody(Transform parent, Building b, 사가정상세표면MaterialLibrary library, List<Object> owned,
            사가정상세건물외형Basis.Result basis = null)
        {
            var body = Child(parent, basis == null ? "원래 GIS 몸체 · 높이4m기호값 · 규칙창 제거"
                : "정확 GIS 본체 · " + basis.HeightProvenance + " · 실내/지붕 미확인");
            var map = new 공간참고자료 { revision = "private-detail-body", offsetX = 550, offsetZ = 8,
                halfExtent = 500, roads = Array.Empty<참고도로>(), buildings = new[] { new 참고건물
                { id = b.Id, height = SymbolicHeight, points = b.Ring.Select(p => new 참고점 { x = p.x, z = p.y }).ToArray() } } };
            if (basis == null)
                사가정모형MeshBuilder.BuildBatches(map, null, batch =>
                { if (!batch.IsShadow && !batch.IsStationDetail) AddBatch(body, batch, owned, true); }, false);
            else
            {
                var mesh=사가정상세건물외형Basis.BuildMesh(b.Ring,b.Bounds.min.y,basis); owned.Add(mesh);
                body.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                body.gameObject.AddComponent<MeshRenderer>();
            }
            // 미관측 몸체/지붕에 오염 사진 무늬를 반복하지 않는다. 상세 전면만 일반 실사 재질을 사용한다.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name="비실측 GIS 몸체 · 중립 표현",hideFlags=HideFlags.DontSave };
            material.SetColor("_BaseColor",new Color(.72f,.71f,.68f));material.SetFloat("_Smoothness",.2f);owned.Add(material);
            foreach (var filter in body.GetComponentsInChildren<MeshFilter>())
            {
                // 이번 함수가 생성해 소유한 새 mesh의 UV만 보완한다. 원본 asset mesh는 수정하지 않는다.
                var mesh = filter.sharedMesh; var vertices = mesh.vertices; var normals = mesh.normals;
                mesh.uv = vertices.Select((p,i) => Mathf.Abs(normals[i].y)>.7f ? new Vector2(p.x,p.z)
                    : Mathf.Abs(normals[i].x)>Mathf.Abs(normals[i].z) ? new Vector2(p.z,p.y) : new Vector2(p.x,p.y)).ToArray();
                mesh.RecalculateTangents(); filter.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            ConstrainOwnedStudyBounds(body,owned);
        }
        private static void ConstrainOwnedStudyBounds(Transform root,List<Object> owned)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh!=null && owned.Contains(filter.sharedMesh))
                    filter.sharedMesh.bounds=DetailedUsedTriangleBounds(filter.sharedMesh.vertices,filter.sharedMesh.triangles);
        }
        public static Bounds DetailedUsedTriangleBounds(Vector3[] vertices,int[] triangles)
        {
            if(vertices==null || triangles==null || triangles.Length==0 || triangles.Length%3!=0
                || triangles.Any(i=>i<0 || i>=vertices.Length || !float.IsFinite(vertices[i].x)
                    || !float.IsFinite(vertices[i].y) || !float.IsFinite(vertices[i].z)))
                throw new ArgumentException("DetailedUsedTriangleBoundsRequired");
            var result=new Bounds(vertices[triangles[0]],Vector3.zero);
            foreach(var index in triangles)result.Encapsulate(vertices[index]);
            return result;
        }
        private static Camera DetailedStreetCamera(Transform parent, Building b, Transform world)
        {
            var node = Child(parent, "점포 거리 원근 Camera · 촬영위치 복원 아님 · " + b.Slug);
            var camera = node.gameObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = parent.gameObject.scene;
            camera.cullingMask = 1 << ReviewLayer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.77f,.82f,.85f); camera.orthographic = false;
            camera.aspect = (float)Width/Height; camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            var preferred=Mathf.Clamp(b.FacadeWidth*.9f,5.5f,9f);
            var distance=preferred; var ray=new Ray(b.Face+Vector3.up*2.6f,b.Normal);
            // 공유 chunk의 큰 bounds만으로 판단하지 않고 화면을 가리는 실제 삼각형까지 대조한다.
            foreach(var filter in world.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;var surface=filter.GetComponent<MeshRenderer>();
                if(mesh==null || surface==null || !surface.enabled || !surface.gameObject.activeInHierarchy
                    || !surface.bounds.IntersectRay(ray,out var broad) || broad>preferred+1)continue;
                if(!mesh.isReadable) { if(broad>1.05f)distance=Mathf.Min(distance,broad*.65f); continue; }
                var vertices=mesh.vertices;var triangles=mesh.triangles;var matrix=filter.transform.localToWorldMatrix;
                for(var i=0;i<triangles.Length;i+=3)
                {
                    var hit=DetailedRayTriangleDistance(ray,matrix.MultiplyPoint3x4(vertices[triangles[i]]),
                        matrix.MultiplyPoint3x4(vertices[triangles[i+1]]),matrix.MultiplyPoint3x4(vertices[triangles[i+2]]));
                    // 앞쪽 간판/차양의 작은 돌출은 통과하고 그 뒤의 이웃·같은 건물 다른 날개를 피한다.
                    if(hit>1.05f && hit<preferred+1)distance=Mathf.Min(distance,hit*.65f);
                }
            }
            distance=Mathf.Max(.8f,distance);
            var fitWidth=2*Mathf.Atan(b.FacadeWidth*1.1f/(2*distance*camera.aspect))*Mathf.Rad2Deg;
            var fitHeight=2*Mathf.Atan(4.5f/(2*distance))*Mathf.Rad2Deg;
            camera.fieldOfView=Mathf.Clamp(Mathf.Max(65,fitWidth,fitHeight),40,120);
            node.position = b.Face + b.Normal * distance + Vector3.up * 2.6f;
            node.rotation = Quaternion.LookRotation(b.Face + Vector3.up*1.9f - node.position);
            node.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            return camera;
        }
        public static float DetailedRayTriangleDistance(Ray ray,Vector3 a,Vector3 b,Vector3 c)
        {
            var edge1=b-a;var edge2=c-a;var p=Vector3.Cross(ray.direction,edge2);var det=Vector3.Dot(edge1,p);
            if(Mathf.Abs(det)<.000001f)return float.PositiveInfinity;
            var inverse=1/det;var t=ray.origin-a;var u=Vector3.Dot(t,p)*inverse;
            if(u<0 || u>1)return float.PositiveInfinity;
            var q=Vector3.Cross(t,edge1);var v=Vector3.Dot(ray.direction,q)*inverse;
            if(v<0 || u+v>1)return float.PositiveInfinity;
            var distance=Vector3.Dot(edge2,q)*inverse;
            return distance>.001f?distance:float.PositiveInfinity;
        }
        private static void AddDetailedSign(Transform anchor, Vector2 size, Building b, JObject labels, Inputs inputs, List<Object> owned)
        {
            var row = ((JArray)labels["labels"]).Single(r => r.Value<string>("slug")==b.Slug
                && r.Value<string>("evidenceSha256")==b.EvidenceHash);
            var texture = new Texture2D(2,2,TextureFormat.RGBA32,false) { hideFlags=HideFlags.DontSave }; owned.Add(texture);
            if (!texture.LoadImage(inputs.Pin(row["image"],2*1024*1024,".png"),false)) throw new InvalidOperationException("DetailedSignImageRequired");
            var height = Mathf.Min(size.y,size.x*texture.height/texture.width); var width=height*texture.width/texture.height;
            var material = Material(Color.white,owned); material.renderQueue=3000; material.SetTexture("_BaseMap",texture);
            material.SetFloat("_Surface",1); material.SetFloat("_ZWrite",0); material.SetFloat("_Cull",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType","Transparent");
            var mesh=new Mesh { hideFlags=HideFlags.DontSave, vertices=new[] { new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(width/2,height/2,0),new Vector3(-width/2,height/2,0) },
                uv=new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up },triangles=new[] {0,1,2,0,2,3} };
            mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var node=Child(anchor,"관측 상호 · 기존 정적 문자PNG"); node.localPosition=Vector3.forward*.002f;
            // 밖에서 +Z 전면을 보는 화면의 오른쪽과 기존 문자 PNG의 오른쪽을 맞춘다.
            node.localRotation=Quaternion.Euler(0,180,0);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
