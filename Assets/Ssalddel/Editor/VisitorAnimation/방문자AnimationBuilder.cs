using System;
using System.Linq;
using Ssalddel.Unity.Presentation.VisitorAnimation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ssalddel.Unity.Editor.VisitorAnimation
{
    /// <summary>방문자 전용 산출물만 만든다. 공식 Scene 저장·vendor 재import는 하지 않는다.</summary>
    public static class 방문자AnimationBuilder
    {
        public const string Root = "Assets/Ssalddel/Presentation/VisitorAnimation";
        public const string ControllerPath = Root + "/방문자Wrapper.controller";
        public const string ActorPath = "Assets/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01.prefab";
        public const string IdlePath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine/Idle/A_Idle_Standing_Masc.fbx";
        public const string WalkPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine/Locomotion/Walk/A_Walk_F_Masc.fbx";
        public const string GreetPath = "Assets/Synty/AnimationEmotesAndTaunts/Animations/Polygon/Masculine/Greet/A_POLY_EMOT_Greet_Wave_Masc.fbx";

        public static AnimationClip ReadClip(string path, string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .SingleOrDefault(value => value.name == name);
            if (clip == null || clip.empty || !clip.isHumanMotion || clip.length <= 0f
                || AnimationUtility.GetCurveBindings(clip).Length == 0
                || AnimationUtility.GetAnimationEvents(clip).Length != 0)
                throw new InvalidOperationException("VisitorImportedClipInvalid:" + path);
            return clip;
        }

        public static AnimatorController BuildController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing; // 기존 산출물을 조용히 재작성하지 않는다.
            var idle = ReadClip(IdlePath, "A_Idle_Standing_Masc");
            var walk = ReadClip(WalkPath, "A_Walk_F_Masc");
            var greet = ReadClip(GreetPath, "A_POLY_EMOT_Greet_Wave_Masc");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var idleState = AddState(machine, "Idle", idle);
            AddState(machine, "Walk", walk);
            var greetState = AddState(machine, "Greet", greet);
            machine.defaultState = idleState;
            var returnTransition = greetState.AddTransition(idleState);
            returnTransition.hasExitTime = true;
            returnTransition.exitTime = 1f;
            returnTransition.hasFixedDuration = true;
            returnTransition.duration = 방문자AnimationAdapter.TransitionSeconds;
            returnTransition.interruptionSource = TransitionInterruptionSource.Destination;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        // scene은 호출자가 소유한다. 위치 작성자는 이 함수 호출 뒤 이동 담당 한 개로 제한한다.
        public static GameObject CreateWrapper(Scene scene)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? throw new InvalidOperationException("VisitorWrapperControllerMissing");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath)
                ?? throw new InvalidOperationException("VisitorStarterActorMissing");
            var root = new GameObject("VisitorAnimationWrapper");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                visual.name = "VisualRoot_StarterMale";
                visual.transform.SetParent(root.transform, false);
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    renderer.updateWhenOffscreen = true;
                var animator = visual.GetComponentInChildren<Animator>(true);
                var adapter = root.AddComponent<방문자AnimationAdapter>();
                if (!adapter.Configure(animator, controller))
                    throw new InvalidOperationException(adapter.ReadState().Diagnostic);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            var state = machine.AddState(name);
            state.motion = clip;
            state.iKOnFeet = true;
            state.writeDefaultValues = false;
            return state;
        }
    }
}
