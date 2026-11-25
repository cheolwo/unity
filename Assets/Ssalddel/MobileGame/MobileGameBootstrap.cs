using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Ssalddel.MobileGame
{
    public static class MobileGameBootstrap
    {
        /// <summary>현재 Scene에 모바일 화면 하나만 조립한다. 회차 서비스는 소유자가 AttachSession으로 연결한다.</summary>
        public static MobileGameController Build(Transform parent = null)
        {
            var root = new GameObject("홍달 모바일 경영 화면", typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Mobile EventSystem", typeof(EventSystem));
                var module = events.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                if (parent != null) events.transform.SetParent(parent, false);
            }
            return root.AddComponent<MobileGameController>();
        }
    }
}
