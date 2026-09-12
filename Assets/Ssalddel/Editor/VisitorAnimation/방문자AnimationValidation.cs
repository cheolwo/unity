using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Unity.Presentation.VisitorAnimation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ssalddel.Unity.Editor.VisitorAnimation
{
    /// <summary>공식 Scene을 저장하지 않는 실제 imported Animator의 격리 평가 기록.</summary>
    public static class 방문자AnimationValidation
    {
        [Serializable]
        public sealed class Sample
        {
            public string intent;
            public string state;
            public float elapsed;
            public float legAngle;
            public float armAngle;
            public float rootDelta;
            public float visualRootDelta;
            public float skinnedMinimumY;
            public string image;
        }

        [Serializable]
        public sealed class Result
        {
            public string scope = "PreviewScene Animator.Update + BakeMesh pose samples with temporary unlit material; not PlayMode or canonical integration";
            public bool avatarValid;
            public bool avatarHuman;
            public bool canonicalDirtyBefore;
            public bool canonicalDirtyAfter;
            public Sample[] samples;
        }

        public static Result Capture()
        {
            var output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../artifacts/local/validation/visitor-animation-e5"));
            Directory.CreateDirectory(output);
            var canonical = EditorSceneManager.GetActiveScene();
            var result = new Result { canonicalDirtyBefore = canonical.isDirty };
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            var preview = new PreviewRenderUtility();
            try
            {
                root = 방문자AnimationBuilder.CreateWrapper(scene);
                preview.AddSingleGO(root);
                var adapter = root.GetComponent<방문자AnimationAdapter>();
                var animator = adapter.Animator;
                result.avatarValid = animator.avatar.isValid;
                result.avatarHuman = animator.avatar.isHuman;
                var rootPosition = root.transform.position;
                var visualPosition = animator.transform.localPosition;
                preview.camera.transform.position = new Vector3(3f, 1.65f, 4.5f);
                preview.camera.transform.LookAt(new Vector3(0f, .95f, 0f));
                preview.camera.nearClipPlane = .05f;
                preview.camera.farClipPlane = 20f;
                preview.camera.fieldOfView = 28f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.17f, .20f, .25f);
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
                preview.lights[1].intensity = .7f;
                preview.ambientColor = new Color(.4f, .4f, .4f);
                var samples = new List<Sample>();
                foreach (var intent in new[] { 방문자동작Intent.Idle, 방문자동작Intent.Walk, 방문자동작Intent.Greet })
                {
                    adapter.InterruptToIdle();
                    animator.Update(0f);
                    adapter.SetIntent(intent);
                    var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    var legStart = leg.localRotation;
                    var armStart = arm.localRotation;
                    for (var frame = 1; frame <= 180; frame++)
                    {
                        animator.Update(1f / 60f);
                        adapter.RefreshPlaybackState();
                        if (frame != 15 && frame != 30 && frame != 60 && frame != 180) continue;
                        var path = Path.Combine(output, intent + "-" + frame + ".png");
                        var minY = float.PositiveInfinity;
                        var temporary = new List<UnityEngine.Object>();
                        preview.BeginStaticPreview(new Rect(0, 0, 512, 512));
                        try
                        {
                            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                                var mesh = new Mesh();
                                temporary.Add(mesh);
                                renderer.BakeMesh(mesh);
                                foreach (var vertex in mesh.vertices)
                                    minY = Mathf.Min(minY, renderer.transform.TransformPoint(vertex).y);
                                var snapshot = new GameObject("VisitorPoseSnapshot");
                                temporary.Add(snapshot);
                                preview.AddSingleGO(snapshot);
                                snapshot.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                                snapshot.transform.localScale = renderer.transform.lossyScale;
                                snapshot.AddComponent<MeshFilter>().sharedMesh = mesh;
                                var materials = renderer.sharedMaterials.Select(source => {
                                    var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                                    temporary.Add(material);
                                    material.SetTexture("_BaseMap", source.GetTexture("_Albedo_Map"));
                                    material.SetColor("_BaseColor", Color.white);
                                    return material;
                                }).ToArray();
                                snapshot.AddComponent<MeshRenderer>().sharedMaterials = materials;
                                renderer.forceRenderingOff = true;
                            }
                            preview.Render(true, true);
                            var texture = preview.EndStaticPreview();
                            try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
                            finally { UnityEngine.Object.DestroyImmediate(texture); }
                        }
                        finally
                        {
                            foreach (var item in temporary.AsEnumerable().Reverse())
                                if (item != null) UnityEngine.Object.DestroyImmediate(item);
                        }
                        samples.Add(new Sample {
                            intent = intent.ToString(), state = adapter.ReadState().ActiveState,
                            elapsed = frame / 60f,
                            legAngle = Quaternion.Angle(legStart, leg.localRotation),
                            armAngle = Quaternion.Angle(armStart, arm.localRotation),
                            rootDelta = Vector3.Distance(rootPosition, root.transform.position),
                            visualRootDelta = Vector3.Distance(visualPosition, animator.transform.localPosition),
                            skinnedMinimumY = minY, image = path
                        });
                    }
                }
                result.samples = samples.ToArray();
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                preview.Cleanup();
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                result.canonicalDirtyAfter = canonical.isDirty;
            }
            File.WriteAllText(Path.Combine(output, "actual-playback.json"), JsonUtility.ToJson(result, true));
            return result;
        }
    }
}
