#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Ssalddel.MobileGame.Editor
{
    public static class MobileGameAssembly
    {
        [MenuItem("홍달/모바일/현재 Scene에 경영 화면 조립")]
        public static void BuildInCurrentScene()
        {
            if (Object.FindAnyObjectByType<MobileGameController>() != null)
            {
                Debug.Log("현재 Scene에 모바일 화면이 이미 있습니다.");
                return;
            }
            var controller = MobileGameBootstrap.Build();
            Undo.RegisterCreatedObjectUndo(controller.gameObject, "홍달 모바일 화면 조립");
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("모바일 화면 조립 완료. 실행 소유자가 IMobileGameSession 서비스를 연결해야 합니다.");
        }
    }
}
#endif
