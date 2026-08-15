using System;
using System.Linq;
using Ssalddel.Unity.Survival;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 서버가 확정한 전투 박자를 카메라와 입력으로만 표현합니다.
    /// Unity는 판정 등급·피해·점수를 만들지 않고 행동과 반응 시각만 전송 대상으로 만듭니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 전투시점Controller : MonoBehaviour
    {
        [SerializeField] private 플레이어경관Controller player = null!;
        [SerializeField] private 전술분대Presenter tacticalSquads = null!;
        [SerializeField] private bool presentationOnly = true;

        private readonly FarmCombatPresentationMapper _mapper = new();
        private readonly FarmTacticalOrderPresentationMapper _tacticalMapper = new();
        private FarmCombatPresentationFrame? _activeFrame;
        private FarmTacticalOrderPresentationFrame? _tacticalFrame;
        private float _beatStartedAt;
        private long _observedWorldRevision;
        private int _commandSequence;
        private bool _reactionSubmitted;

        public event Action<FarmCombatReactionCommandDraft> ReactionCommandPrepared
            = delegate { };
        public event Action<FarmTacticalOrderPresentationFrame>
            TacticalViewTransitionSuggested = delegate { };
        public event Action<FarmTacticalOrderPreviewDraft>
            TacticalOrderPreviewPrepared = delegate { };
        public event Action<FarmTacticalOrderConfirmDraft>
            TacticalOrderConfirmPrepared = delegate { };
        public event Action<FarmTacticalMovementPresentationFrame>
            TacticalMovementPrepared = delegate { };

        public bool HasActiveBeat => _activeFrame != null;
        public bool ReactionSubmitted => _reactionSubmitted;
        public bool HasOpenTacticalOrderWindow => _tacticalFrame != null;
        public FarmCombatPresentationFrame? ActiveFrame => _activeFrame;
        public FarmTacticalOrderPresentationFrame? TacticalFrame => _tacticalFrame;
        public FarmCombatReactionCommandDraft? LastPreparedCommand { get; private set; }
        public FarmTacticalOrderPreviewDraft? LastTacticalPreview { get; private set; }
        public FarmTacticalOrderConfirmDraft? LastTacticalConfirm { get; private set; }
        public bool PresentationOnly => presentationOnly;
        public 전술분대Presenter TacticalSquads => tacticalSquads;

        public void Configure(플레이어경관Controller value)
        {
            player = value;
            presentationOnly = true;
            if (!ValidateWiring())
                throw new ArgumentException("FarmCombatViewWiringInvalid");
        }

        public void ConfigureTacticalSquads(전술분대Presenter value)
        {
            tacticalSquads = value;
            if (tacticalSquads == null || !tacticalSquads.ValidateWiring())
                throw new ArgumentException("FarmTacticalSquadViewWiringInvalid");
        }

        public bool ValidateWiring()
            => player != null && player.PresentationOnly && presentationOnly;

        public void ApplyServerState(
            FarmCombatStateApiModel state,
            string actorStableId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (tacticalSquads != null
                && tacticalSquads.TryApplyServerState(state, out var movement)
                && movement != null)
                TacticalMovementPrepared(movement);
            var hasActiveBeat = (state.Beats ?? Array.Empty<FarmCombatBeatApiModel>())
                .Any(value => value.ActorStableId == actorStableId
                    && value.StateCode == FarmCombatPresentationCodes.Active);
            if (!hasActiveBeat)
            {
                ClearActiveBeat();
                var hasOpenTacticalWindow = state.Tactical != null
                    && (state.Tactical.OrderWindows
                        ?? Array.Empty<FarmTacticalOrderWindowApiModel>())
                    .Any(value => value.AuthorizedActorStableId == actorStableId
                        && value.StateCode == FarmCombatPresentationCodes.Open);
                if (hasOpenTacticalWindow)
                {
                    _tacticalFrame = _tacticalMapper.Map(state, actorStableId);
                    _observedWorldRevision = state.WorldRevision;
                    LastTacticalPreview = null;
                    LastTacticalConfirm = null;
                    TacticalViewTransitionSuggested(_tacticalFrame);
                }
                else
                {
                    ClearTacticalOrderWindow();
                }
                return;
            }

            ClearTacticalOrderWindow();
            _activeFrame = _mapper.Map(state, actorStableId);
            _observedWorldRevision = state.WorldRevision;
            _beatStartedAt = Time.unscaledTime;
            _reactionSubmitted = false;
            LastPreparedCommand = null;
            player.EnterCombatMode(_activeFrame.PerspectiveCode);
        }

        public bool TryHandleCombatInput(Mouse? mouse)
        {
            if (_activeFrame == null) return false;
            if (_reactionSubmitted || mouse == null || player.IsCameraTransitioning)
                return true;

            string? actionCode = null;
            if (mouse.leftButton.wasPressedThisFrame)
                actionCode = FarmCombatPresentationCodes.Counter;
            else if (mouse.rightButton.wasPressedThisFrame)
                actionCode = FarmCombatPresentationCodes.Guard;
            if (actionCode == null) return true;

            var elapsedMs = Mathf.Clamp(
                Mathf.RoundToInt((Time.unscaledTime - _beatStartedAt) * 1000f),
                0, 1600);
            var command = FarmCombatReactionCommandFactory.Create(
                _activeFrame,
                _observedWorldRevision,
                "command:unity:combat-reaction:"
                    + (++_commandSequence).ToString(),
                actionCode,
                elapsedMs);
            LastPreparedCommand = command;
            _reactionSubmitted = true;
            ReactionCommandPrepared(command);
            return true;
        }

        public bool TryHandleTacticalViewInput(Keyboard? keyboard)
        {
            if (_tacticalFrame == null || keyboard == null
                || !keyboard.tKey.wasPressedThisFrame)
                return false;
            return AcceptTacticalViewSuggestion();
        }

        public bool AcceptTacticalViewSuggestion()
        {
            if (_tacticalFrame == null || player.IsCameraTransitioning)
                return false;
            player.EnterCombatMode(
                FarmCombatPresentationCodes.ThirdPersonAwareness);
            return true;
        }

        public FarmTacticalOrderPreviewDraft PrepareTacticalOrderPreview(
            string orderCode,
            string opportunityStableId = "")
        {
            if (_tacticalFrame == null)
                throw new InvalidOperationException(
                    "FarmTacticalOrderWindowNotActive");
            var draft = FarmTacticalOrderCommandFactory.CreatePreview(
                _tacticalFrame, _observedWorldRevision, orderCode,
                opportunityStableId);
            LastTacticalPreview = draft;
            TacticalOrderPreviewPrepared(draft);
            return draft;
        }

        public FarmTacticalOrderConfirmDraft PrepareTacticalOrderConfirm(
            string orderCode,
            string opportunityStableId = "")
        {
            if (_tacticalFrame == null)
                throw new InvalidOperationException(
                    "FarmTacticalOrderWindowNotActive");
            var draft = FarmTacticalOrderCommandFactory.CreateConfirm(
                _tacticalFrame, _observedWorldRevision,
                "command:unity:tactical-order:"
                    + (++_commandSequence).ToString(),
                orderCode, opportunityStableId);
            LastTacticalConfirm = draft;
            TacticalOrderConfirmPrepared(draft);
            return draft;
        }

        public void ClearActiveBeat()
        {
            _activeFrame = null;
            _reactionSubmitted = false;
            LastPreparedCommand = null;
        }

        public void ClearTacticalOrderWindow()
        {
            _tacticalFrame = null;
            LastTacticalPreview = null;
            LastTacticalConfirm = null;
        }
    }
}
