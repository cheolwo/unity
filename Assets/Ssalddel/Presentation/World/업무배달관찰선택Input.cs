using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    public sealed class 업무배달관찰표식View : MonoBehaviour
    { public string ActorId; public Vector3 Target; }

    /// <summary>지도 클릭은 관찰 대상만 바꾼다. UI 뒤 지도 입력은 차단한다.</summary>
    public sealed class 업무배달관찰선택Input : MonoBehaviour
    {
        public event Action<string> Selected;
        public Camera Observer;
        private void Update()
        {
            if (Observer == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame
                || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var pointer = Mouse.current.position.ReadValue();
            if (!Observer.pixelRect.Contains(pointer)) return;
            if (Physics.Raycast(Observer.ScreenPointToRay(pointer), out var hit, 300, 1 << 31))
            {
                var marker = hit.collider.GetComponentInParent<업무배달관찰표식View>();
                if (marker != null) Selected?.Invoke(marker.ActorId);
            }
        }
    }
}
