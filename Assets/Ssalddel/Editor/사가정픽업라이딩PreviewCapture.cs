using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static partial class 사가정영상선분디오라마DemoCapture
    {
        private const int PickupRideFrameRate = 24;
        private const int PickupRideFrameCount = 288;
        private const string PickupRideRevision = "sagajeong-pickup-ride-preview.r1";

        private sealed class 픽업라이딩시안Road
        {
            public string Id, Kind, MapRevision, MapHash;
            public Vector3 Start, End, PointA, PointB;
        }

        // 명시한 환경 변수가 없으면 기존 상세 비교의 실행/출력에 영향을 주지 않는다.
        private static void 픽업라이딩시안CaptureIfRequested(Transform root, Building[] buildings, Camera referenceCamera)
        {
            var output = Environment.GetEnvironmentVariable("SSALDDEL_PICKUP_RIDE_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) return;
            if (!Path.IsPathRooted(output) || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("PickupRideNewAbsoluteOutputRequired");
            if (!root.gameObject.scene.IsValid() || !root.gameObject.scene.isLoaded || referenceCamera.scene != root.gameObject.scene)
                throw new InvalidOperationException("PickupRidePreviewSceneRequired");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("PickupRideGraphicsRequired");

            var road = 픽업라이딩시안Road선택(buildings);
            Directory.CreateDirectory(output);
            var frameFolder = Path.Combine(output, "frames");
            Directory.CreateDirectory(frameFolder);
            var before = Capture(referenceCamera, Path.Combine(output, "source-before.png"));
            var owned = new List<Material>();
            Transform container = null;
            var frames = new JArray();
            var segmentList = new JArray
            {
                new JObject { ["id"] = "pickup-a", ["kind"] = "Pickup", ["startSeconds"] = 0, ["endSeconds"] = 2, ["title"] = "가상 픽업 A" },
                new JObject { ["id"] = "ride-a-b", ["kind"] = "Riding", ["startSeconds"] = 2, ["endSeconds"] = 6, ["title"] = "가상 라이딩 A→B" },
                new JObject { ["id"] = "pickup-b", ["kind"] = "Pickup", ["startSeconds"] = 6, ["endSeconds"] = 8, ["title"] = "가상 픽업 B" },
                new JObject { ["id"] = "ride-b-a", ["kind"] = "Riding", ["startSeconds"] = 8, ["endSeconds"] = 12, ["title"] = "가상 라이딩 B→A" }
            };
            var captureComplete = false;
            var sourceRestored = false;
            JObject after = null;
            try
            {
                container = Child(root, "비공개 픽업·라이딩 3D 동작시안 · 가상 A/B");
                var teal = 픽업라이딩시안Material(new Color(.04f, .63f, .66f), owned);
                var amber = 픽업라이딩시안Material(new Color(1f, .49f, .12f), owned);
                var dark = 픽업라이딩시안Material(new Color(.08f, .12f, .18f), owned);
                var cream = 픽업라이딩시안Material(new Color(.98f, .94f, .78f), owned);
                var wheel = 픽업라이딩시안Material(new Color(.035f, .045f, .055f), owned);
                픽업라이딩시안Marker(container, road.PointA, amber, cream);
                픽업라이딩시안Marker(container, road.PointB, teal, cream);

                var actor = Child(container, "가상 기사 스쿠터 · 실제 기사/주문/운행 아님");
                var wheels = new[]
                {
                    픽업라이딩시안Primitive(actor, PrimitiveType.Cylinder, "앞바퀴", new Vector3(0,.34f,.57f), new Vector3(.62f,.12f,.62f), wheel, new Vector3(0,0,90)),
                    픽업라이딩시안Primitive(actor, PrimitiveType.Cylinder, "뒷바퀴", new Vector3(0,.34f,-.57f), new Vector3(.62f,.12f,.62f), wheel, new Vector3(0,0,90))
                };
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "스쿠터 몸체", new Vector3(0,.59f,0), new Vector3(.55f,.28f,1.32f), teal);
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "발판", new Vector3(0,.44f,.1f), new Vector3(.7f,.1f,1.45f), dark);
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "안장", new Vector3(0,.84f,-.18f), new Vector3(.5f,.14f,.67f), dark);
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "앞판", new Vector3(0,.84f,.6f), new Vector3(.58f,.75f,.18f), teal);
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "손잡이", new Vector3(0,1.17f,.52f), new Vector3(.87f,.08f,.1f), dark);
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "배달 상자", new Vector3(0,1.11f,-.74f), new Vector3(.72f,.66f,.61f), amber);
                픽업라이딩시안Primitive(actor, PrimitiveType.Capsule, "기사 몸통", new Vector3(0,1.23f,-.06f), new Vector3(.4f,.36f,.36f), dark);
                픽업라이딩시안Primitive(actor, PrimitiveType.Sphere, "헬멧", new Vector3(0,1.78f,.02f), new Vector3(.5f,.48f,.5f), cream);
                픽업라이딩시안Primitive(actor, PrimitiveType.Cube, "헬멧 앞창", new Vector3(0,1.77f,.24f), new Vector3(.4f,.2f,.05f), dark);

                var cameraRoot = Child(container, "실제 3D 추적 촬영 Camera");
                var camera = cameraRoot.gameObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = root.gameObject.scene;
                camera.cullingMask = 1 << ReviewLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = referenceCamera.backgroundColor;
                camera.orthographic = true;
                camera.orthographicSize = 11f;
                camera.aspect = 16f / 9f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                var forward = (road.PointB - road.PointA).normalized;
                var side = Vector3.Cross(Vector3.up, forward).normalized;
                var cameraOffset = -forward * 4f + side * 3f + Vector3.up * 32f;

                for (var i = 0; i < PickupRideFrameCount; i++)
                {
                    var time = (double)i / PickupRideFrameRate;
                    var phase = time < 2 ? "pickup-a" : time < 6 ? "ride-a-b" : time < 8 ? "pickup-b" : "ride-b-a";
                    var amount = time < 2 ? 0f : time < 6 ? (float)((time - 2) / 4) : time < 8 ? 1f : 1f - (float)((time - 8) / 4);
                    actor.position = Vector3.Lerp(road.PointA, road.PointB, amount);
                    actor.rotation = Quaternion.LookRotation(time < 8 ? forward : -forward, Vector3.up);
                    var riding = phase.StartsWith("ride-", StringComparison.Ordinal);
                    var spin = riding ? (float)(time * 540) : 0f;
                    foreach (var tire in wheels) tire.localRotation = Quaternion.Euler(spin, 0, 90);
                    cameraRoot.position = actor.position + cameraOffset;
                    cameraRoot.rotation = Quaternion.LookRotation(actor.position + Vector3.up * .8f - cameraRoot.position, Vector3.up);
                    var filename = i.ToString("D6") + ".png";
                    var captured = Capture(camera, Path.Combine(frameFolder, filename));
                    frames.Add(new JObject
                    {
                        ["index"] = i, ["timeSeconds"] = time, ["phase"] = phase,
                        ["actorPosition"] = 픽업라이딩시안Vector(actor.position),
                        ["actorRotationEuler"] = 픽업라이딩시안Vector(actor.eulerAngles),
                        ["cameraPosition"] = 픽업라이딩시안Vector(cameraRoot.position),
                        ["cameraRotationEuler"] = 픽업라이딩시안Vector(cameraRoot.eulerAngles),
                        ["sha256"] = captured["image"]["sha256"].DeepClone(), ["path"] = "frames/" + filename,
                        ["foregroundPixelCount"] = captured["foregroundPixelCount"].DeepClone(),
                        ["brightnessVariance"] = captured["brightnessVariance"].DeepClone()
                    });
                    if (i % 48 == 0) Debug.Log("PickupRidePreviewFrame=" + i + "/" + PickupRideFrameCount);
                }
                captureComplete = frames.Count == PickupRideFrameCount;
            }
            finally
            {
                if (container != null) Object.DestroyImmediate(container.gameObject);
                foreach (var material in owned) if (material != null) Object.DestroyImmediate(material);
                after = Capture(referenceCamera, Path.Combine(output, "source-after.png"));
                sourceRestored = before["image"].Value<string>("sha256") == after["image"].Value<string>("sha256");
                WriteNew(Path.Combine(output, "cleanup.private.json"), new JObject
                {
                    ["captureComplete"] = captureComplete, ["temporaryObjectsDestroyed"] = container == null,
                    ["sourceBefore"] = before["image"].DeepClone(), ["sourceAfter"] = after["image"].DeepClone(),
                    ["sourcePixelsRestoredExactly"] = sourceRestored, ["sourceSceneSaved"] = false,
                    ["canonicalApplied"] = false, ["operationalEffects"] = false
                });
            }
            if (!sourceRestored) throw new InvalidOperationException("PickupRideSourcePixelsDifferAfterCleanup");
            if (!captureComplete) throw new InvalidOperationException("PickupRideFrameCountIncomplete");
            WriteNew(Path.Combine(output, "journey-manifest.private.json"), new JObject
            {
                ["schemaVersion"] = "private-sagajeong-pickup-ride-preview.v1",
                ["caseStableId"] = "private:sagajeong:pickup-ride-cycle", ["caseRevision"] = PickupRideRevision,
                ["fps"] = PickupRideFrameRate, ["frameCount"] = PickupRideFrameCount, ["durationSeconds"] = 12,
                ["lastFrameTimeSeconds"] = 287d / 24, ["width"] = Width, ["height"] = Height,
                ["segments"] = segmentList, ["frames"] = frames,
                ["route"] = new JObject
                {
                    ["revision"] = PickupRideRevision + ".route", ["sourceMapRevision"] = road.MapRevision,
                    ["sourceMapSha256"] = road.MapHash, ["roadId"] = road.Id, ["roadKind"] = road.Kind,
                    ["sourceSegmentStart"] = 픽업라이딩시안Vector(road.Start), ["sourceSegmentEnd"] = 픽업라이딩시안Vector(road.End),
                    ["pickupA"] = 픽업라이딩시안Vector(road.PointA), ["pickupB"] = 픽업라이딩시안Vector(road.PointB),
                    ["actualStoreToStoreRoute"] = false, ["actualRoadTravelConfirmed"] = false,
                    ["pointMeaning"] = "FictionalPickupPointsOnOneExistingReferenceRoadSegment"
                },
                ["captureKind"] = "UnityPreviewSceneCameraRenderPerFrame", ["actual3DActorAndCameraMoved"] = true,
                ["timeMeaning"] = "CompressedFictionalDemonstrationNotActualDeliveryDuration",
                ["sourceModelRevision"] = "ExistingOptInPrivateDetailedBuildingStudy",
                ["runtimeFontUsed"] = false, ["playModeVerified"] = false, ["gameViewVerified"] = false,
                ["sourcePixelsRestoredExactly"] = sourceRestored, ["canonicalApplied"] = false,
                ["operationalEffects"] = false, ["publicationReady"] = false
            });
            Debug.Log("PickupRidePreviewComplete:Frames=" + frames.Count + ";SourcePixelsRestored=" + sourceRestored);
        }

        private static 픽업라이딩시안Road 픽업라이딩시안Road선택(Building[] buildings)
        {
            var source = Resources.Load<TextAsset>("SagajeongReference");
            if (source == null || buildings == null || buildings.Length == 0)
                throw new InvalidOperationException("PickupRideExistingMapAndBuildingsRequired");
            var map = JsonUtility.FromJson<공간참고자료>(source.text);
            var center = buildings.Aggregate(Vector3.zero, (sum, b) => sum + b.Bounds.center) / buildings.Length;
            var roads = (map.roads ?? Array.Empty<참고도로>()).Where(r => r != null)
                .Select(r => new { Road = r, A = new Vector3(r.x1, .18f, r.z1), B = new Vector3(r.x2, .18f, r.z2) })
                .Where(r => float.IsFinite(r.A.x) && float.IsFinite(r.A.z) && float.IsFinite(r.B.x) && float.IsFinite(r.B.z)
                    && Vector3.Distance(r.A, r.B) >= 14f)
                .Select(r => new { r.Road, r.A, r.B, Distance = 픽업라이딩시안Segment거리(center, r.A, r.B) })
                .OrderBy(r => r.Distance).ThenBy(r => r.Road.id, StringComparer.Ordinal).ToArray();
            if (roads.Length == 0) throw new InvalidOperationException("PickupRideReferenceRoadMissing");
            var selected = roads[0];
            if (selected.Distance > 150f) throw new InvalidOperationException("PickupRideNearbyRoadRequired");
            var direction = (selected.B - selected.A).normalized;
            var length = Vector3.Distance(selected.A, selected.B);
            var half = Mathf.Min(16f, length * .35f);
            var projection = Vector3.Dot(center - selected.A, direction);
            var midpoint = selected.A + direction * Mathf.Clamp(projection, half, length - half);
            return new 픽업라이딩시안Road
            {
                Id = selected.Road.id, Kind = selected.Road.kind, MapRevision = map.revision, MapHash = Hash(source.bytes),
                Start = selected.A, End = selected.B, PointA = midpoint - direction * half, PointB = midpoint + direction * half
            };
        }

        private static float 픽업라이딩시안Segment거리(Vector3 point, Vector3 a, Vector3 b)
        {
            var direction = b - a;
            var t = Mathf.Clamp01(Vector3.Dot(point - a, direction) / direction.sqrMagnitude);
            var projected = a + direction * t;
            return Vector2.Distance(new Vector2(point.x, point.z), new Vector2(projected.x, projected.z));
        }

        private static JArray 픽업라이딩시안Vector(Vector3 value) => new(value.x, value.y, value.z);

        private static Material 픽업라이딩시안Material(Color color, List<Material> owned)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("PickupRideUnlitShaderRequired");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave, name = "비공개 동작시안 재질" };
            material.SetColor("_BaseColor", color);
            owned.Add(material);
            return material;
        }

        private static Transform 픽업라이딩시안Primitive(Transform parent, PrimitiveType kind, string name,
            Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(kind);
            go.name = name; go.hideFlags = HideFlags.DontSave; go.layer = ReviewLayer;
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localScale = scale; go.transform.localEulerAngles = rotation;
            var collider = go.GetComponent<Collider>(); if (collider != null) Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go.transform;
        }

        private static void 픽업라이딩시안Marker(Transform parent, Vector3 at, Material color, Material cream)
        {
            var marker = Child(parent, "가상 픽업 지점 · 실제 매장 위치 아님");
            marker.position = at;
            픽업라이딩시안Primitive(marker, PrimitiveType.Cylinder, "픽업 지점 바닥", new Vector3(0,.03f,0), new Vector3(2.8f,.025f,2.8f), color);
            픽업라이딩시안Primitive(marker, PrimitiveType.Cube, "픽업 지점 기둥", new Vector3(1.4f,1f,.3f), new Vector3(.17f,2f,.17f), cream);
            픽업라이딩시안Primitive(marker, PrimitiveType.Cube, "픽업 지점 표식", new Vector3(1.4f,2.2f,.3f), new Vector3(.75f,.55f,.16f), color);
        }
    }
}
