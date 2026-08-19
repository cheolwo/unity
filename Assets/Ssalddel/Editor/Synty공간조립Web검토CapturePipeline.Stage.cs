using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ssalddel.Unity.Editor
{
    public static partial class Synty공간조립Web검토CapturePipeline
    {
        private static readonly 배치항목[] Placements =
        {
            new("Construction", "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Generator_Large_01.prefab", 0f, 0f, 0f, 0f, 1f),
            new("Construction", "Assets/Synty/PolygonConstruction/Prefabs/Buildings/SM_Bld_WaterTower_01.prefab", -6f, 0f, 4f, 15f, .78f),
            new("Nature", "Assets/Synty/PolygonNature/Prefabs/Trees/SM_Tree_Willow_Medium_01.prefab", -8f, 0f, -4f, -18f, .9f),
            new("Nature", "Assets/Synty/PolygonNature/Prefabs/Plants/SM_Plant_FlowerPatch_01.prefab", -3f, 0f, -3f, 0f, 1.4f),
            new("Farm", "Assets/Synty/PolygonFarm/Prefabs/Props/SM_Prop_Windmill_01.prefab", 7f, 0f, 5f, -20f, .72f),
            new("Farm", "Assets/Synty/PolygonFarm/Prefabs/Buildings/SM_Bld_Silo_Small_01.prefab", 8f, 0f, -4f, 12f, .8f),
            new("Town", "Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_ParkBench_01.prefab", -4f, 0f, -7f, 12f, 1f),
            new("Town", "Assets/Synty/PolygonTown/Prefabs/Props/SM_Prop_Outdoor_Light_01.prefab", 0f, 0f, -7f, 0f, 1f),
            new("City", "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_PowerBox_01.prefab", 4f, 0f, -3f, -12f, 1f),
            new("City", "Assets/Synty/PolygonCity/Prefabs/Props/SM_Prop_Power_Cables_01.prefab", 3f, 0f, 2f, 0f, 1f),
        };

        private static readonly 촬영시점[] Views =
        {
            new("FrontThreeQuarter", "정면 3/4", new Vector3(1f, .72f, -1f)),
            new("RearThreeQuarter", "후면 3/4", new Vector3(-1f, .68f, 1f)),
            new("LeftSide", "좌측면", new Vector3(-1f, .55f, -.08f)),
            new("TopOblique", "상부 사선", new Vector3(.75f, 1.55f, -.75f)),
        };

        public static IReadOnlyList<string> RequiredPrefabPaths
            => Placements.Select(value => value.PrefabPath).ToArray();

        public static string ComputeSourceCompositionHash()
        {
            var lines = new List<string>
            {
                CompositionStableId,
                H1StableId,
                H2StableId,
                H3StableId,
                "variant=A",
                "state=Normal",
            };
            foreach (var placement in Placements)
            {
                var guid = AssetDatabase.AssetPathToGUID(placement.PrefabPath);
                lines.Add(string.Join("|",
                    placement.PackCode,
                    placement.PrefabPath,
                    guid,
                    Number(placement.X), Number(placement.Y), Number(placement.Z),
                    Number(placement.Yaw), Number(placement.Scale)));
            }

            return Sha256(string.Join("\n", lines));
        }

        private static 촬영Stage BuildReviewOnlyStage(Scene scene, ICollection<Material> temporaryMaterials)
        {
            var root = NewSceneObject("SyntyWeb검토CaptureStage_PresentationOnly", scene).transform;
            var compositionRoot = NewSceneObject("VisualRoot_조합물전용", scene).transform;
            compositionRoot.SetParent(root, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.name = "회복발전소_고정바닥_40m";
            ground.transform.SetParent(compositionRoot, false);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var groundMaterial = new Material(
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMaterial.name = "임시_검토바닥";
            groundMaterial.color = new Color(.19f, .27f, .18f, 1f);
            temporaryMaterials.Add(groundMaterial);
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            foreach (var placement in Placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(placement.PrefabPath)
                             ?? throw new InvalidOperationException("SyntyPrefabMissing:" + placement.PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = placement.PackCode + "_" + prefab.name;
                instance.transform.SetParent(compositionRoot, false);
                instance.transform.localPosition = new Vector3(placement.X, placement.Y, placement.Z);
                instance.transform.localRotation = Quaternion.Euler(0f, placement.Yaw, 0f);
                instance.transform.localScale = Vector3.one * placement.Scale;
            }

            SetLayerRecursively(compositionRoot.gameObject, ReviewOnlyLayer);

            var cameraObject = NewSceneObject("CaptureCamera_조합물전용", scene);
            cameraObject.transform.SetParent(root, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << ReviewOnlyLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.58f, .72f, .8f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var sunObject = NewSceneObject("CaptureLight_주광", scene);
            sunObject.transform.SetParent(root, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, .94f, .84f, 1f);
            sun.cullingMask = 1 << ReviewOnlyLayer;

            var fillObject = NewSceneObject("CaptureLight_보조", scene);
            fillObject.transform.SetParent(root, false);
            fillObject.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = .55f;
            fill.color = new Color(.72f, .82f, 1f, 1f);
            fill.cullingMask = 1 << ReviewOnlyLayer;

            return new 촬영Stage(compositionRoot, camera);
        }

        private static void ValidateReviewOnlyStage(Transform compositionRoot, Camera camera)
        {
            if (camera.cullingMask != 1 << ReviewOnlyLayer)
                throw new InvalidOperationException("CaptureCameraLayerMaskInvalid");
            if (compositionRoot.GetComponentsInChildren<Canvas>(true).Length != 0)
                throw new InvalidOperationException("CaptureStageMustNotContainApplicationCanvas");
            if (compositionRoot.GetComponentsInChildren<Camera>(true).Length != 0)
                throw new InvalidOperationException("CompositionRootMustNotContainCamera");
            if (compositionRoot.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.gameObject.layer != ReviewOnlyLayer))
            {
                throw new InvalidOperationException("CaptureStageRendererLayerLeakDetected");
            }
        }

        private static Bounds CalculateBounds(Transform compositionRoot)
        {
            var renderers = compositionRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("CaptureStageRendererMissing");
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void PositionCamera(Camera camera, Bounds bounds, Vector3 direction)
        {
            var target = bounds.center + Vector3.up * Mathf.Max(.5f, bounds.extents.y * .08f);
            var radius = Mathf.Max(12f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            var distance = Mathf.Max(radius * 2.45f, bounds.extents.y * 2.3f);
            camera.transform.position = target + direction.normalized * distance;
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        }

        private static void CapturePng(Camera camera, string filePath)
        {
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                // 새 Additive Scene에서 URP가 첫 수동 render에 파이프라인 상태만 준비하는
                // 경우가 있어 한 번 예열한 뒤 실제 검토 frame을 읽는다.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(filePath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }

        private static void ValidateRequiredPrefabs()
        {
            var missing = Placements
                .Where(value => AssetDatabase.LoadAssetAtPath<GameObject>(value.PrefabPath) == null)
                .Select(value => value.PrefabPath)
                .ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException("SyntyRequiredPrefabsMissing:\n" + string.Join("\n", missing));
            if (Placements.Select(value => value.PackCode).Distinct(StringComparer.Ordinal).Count() != 5)
                throw new InvalidOperationException("FivePackCoverageInvalid");
        }

        private static void ValidateLineageInput(string parentCaptureBundleHash, long expectedRevision)
        {
            if (expectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (expectedRevision == 0 && !string.IsNullOrEmpty(parentCaptureBundleHash))
                throw new ArgumentException("최초 촬영에는 부모 CaptureBundleHash가 없어야 합니다.");
            if (expectedRevision > 0 && !IsSha256(parentCaptureBundleHash))
                throw new ArgumentException("재촬영에는 부모 CaptureBundleHash가 필요합니다.");
        }

        private static bool IsSha256(string value)
            => value != null && value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static GameObject NewSceneObject(string name, Scene scene)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static string Number(float value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);

        private readonly struct 배치항목
        {
            public 배치항목(string packCode, string prefabPath,
                float x, float y, float z, float yaw, float scale)
            {
                PackCode = packCode;
                PrefabPath = prefabPath;
                X = x;
                Y = y;
                Z = z;
                Yaw = yaw;
                Scale = scale;
            }

            public string PackCode { get; }
            public string PrefabPath { get; }
            public float X { get; }
            public float Y { get; }
            public float Z { get; }
            public float Yaw { get; }
            public float Scale { get; }
        }

        private readonly struct 촬영시점
        {
            public 촬영시점(string viewCode, string displayName, Vector3 direction)
            {
                ViewCode = viewCode;
                DisplayName = displayName;
                Direction = direction;
            }

            public string ViewCode { get; }
            public string DisplayName { get; }
            public Vector3 Direction { get; }
        }

        private readonly struct 촬영Stage
        {
            public 촬영Stage(Transform compositionRoot, Camera camera)
            {
                CompositionRoot = compositionRoot;
                Camera = camera;
            }

            public Transform CompositionRoot { get; }
            public Camera Camera { get; }
        }
    }
}
