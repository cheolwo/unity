using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    public readonly struct 전투입력Frame
    {
        public 전투입력Frame(
            bool attackPressed,
            bool defendPressed,
            bool pointerOverUi)
        {
            AttackPressed = attackPressed;
            DefendPressed = defendPressed;
            PointerOverUi = pointerOverUi;
        }

        public bool AttackPressed { get; }
        public bool DefendPressed { get; }
        public bool PointerOverUi { get; }
        public bool HasAction => AttackPressed || DefendPressed;
    }

    /// <summary>
    /// Input System의 의미 기반 Attack·Defend Action만 전투 계층에 전달합니다.
    /// Scene에 Action Asset이 아직 연결되지 않은 경우에도 같은 binding의 로컬 Action을
    /// 만들어 기존 저장 Scene을 깨뜨리지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 전투입력Adapter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions = null!;

        private InputAction? attack;
        private InputAction? defend;
        private bool ownsFallbackActions;

        public void Configure(InputActionAsset value)
        {
            inputActions = value;
            ReleaseActions();
            EnsureActions();
            EnableActions();
        }

        public bool ValidateWiring()
        {
            EnsureActions();
            return attack != null && defend != null;
        }

        public 전투입력Frame ReadFrame()
        {
            EnsureActions();
            return new 전투입력Frame(
                attack?.WasPressedThisFrame() == true,
                defend?.WasPressedThisFrame() == true,
                EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject());
        }

        private void OnEnable()
        {
            EnsureActions();
            EnableActions();
        }

        private void OnDisable()
        {
            attack?.Disable();
            defend?.Disable();
        }

        private void OnDestroy() => ReleaseActions();

        private void EnsureActions()
        {
            if (attack != null && defend != null) return;
            if (inputActions != null)
            {
                var player = inputActions.FindActionMap("Player", true);
                attack = player.FindAction("Attack", true);
                defend = player.FindAction("Defend", true);
                ownsFallbackActions = false;
                return;
            }

            attack = new InputAction("Attack", InputActionType.Button,
                "<Mouse>/leftButton");
            defend = new InputAction("Defend", InputActionType.Button,
                "<Mouse>/rightButton");
            ownsFallbackActions = true;
        }

        private void EnableActions()
        {
            attack?.Enable();
            defend?.Enable();
        }

        private void ReleaseActions()
        {
            if (ownsFallbackActions)
            {
                attack?.Dispose();
                defend?.Dispose();
            }
            attack = null;
            defend = null;
            ownsFallbackActions = false;
        }
    }
}
