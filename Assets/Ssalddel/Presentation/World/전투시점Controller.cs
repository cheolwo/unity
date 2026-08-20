using System;
using System.Linq;
using Ssalddel.Unity.Battles;
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
        [SerializeField] private 전투입력Adapter input = null!;
        [SerializeField] private bool presentationOnly = true;

        private readonly FarmCombatPresentationMapper _mapper = new();
        private readonly FarmTacticalOrderPresentationMapper _tacticalMapper = new();
        private FarmCombatPresentationFrame? _activeFrame;
        private FarmTacticalOrderPresentationFrame? _tacticalFrame;
        private readonly FarmCombatBeatClock _beatClock = new();
        private FarmCombatStateApiModel? _latestState;
        private string _actorStableId = string.Empty;
        private string _inputPhaseCode = FarmCombatPresentationCodes.Ready;
        private string _lastAuthorityErrorCode = string.Empty;
        private string _lastPresentedReactionStableId = string.Empty;
        private long _observedWorldRevision;
        private int _commandSequence;
        private bool _reactionSubmitted;
        private BattleInstanceApiModel? _localBattle;

        public event Action<FarmCombatReactionCommandDraft> ReactionCommandPrepared
            = delegate { };
        public event Action<string> CombatEntryRequested = delegate { };
        public event Action<LocalCombatActionCommandDraft> LocalActionCommandPrepared
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
        public string InputPhaseCode => _inputPhaseCode;
        public string LastAuthorityErrorCode => _lastAuthorityErrorCode;
        public string DesiredLocalControlModeCode
            => player != null && player.CurrentMode == 플레이어시점Mode.FirstPerson
                ? LocalCombatPresentationCodes.DirectAction
                : LocalCombatPresentationCodes.TacticalCommand;
        public bool LocksPlayerMovement
            => _localBattle == null && (_activeFrame != null
                || _inputPhaseCode == FarmCombatPresentationCodes.Entering
                || _inputPhaseCode == FarmCombatPresentationCodes.Submitting);

        public void ApplyUnifiedBattleState(BattleInstanceApiModel state,
            string actorStableId)
        {
            if (state == null || state.CombatSpaceCode !=
                    BattlePresentationCodes.WorldLocal
                || !state.SimulationOnly || state.IsOperationalState)
                throw new InvalidOperationException("LocalCombatAuthorityBoundaryInvalid");
            _localBattle = state;
            _actorStableId = actorStableId?.Trim() ?? string.Empty;
            _lastAuthorityErrorCode = string.Empty;
            _inputPhaseCode = state.PhaseCode == BattlePresentationCodes.Active
                ? FarmCombatPresentationCodes.Telegraph
                : FarmCombatPresentationCodes.Resolved;
        }

        private void Awake()
        {
            input ??= GetComponent<전투입력Adapter>();
            player ??= FindFirstObjectByType<플레이어경관Controller>(
                FindObjectsInactive.Include);
        }

        public void Configure(플레이어경관Controller value)
        {
            player = value;
            input = GetComponent<전투입력Adapter>()
                ?? gameObject.AddComponent<전투입력Adapter>();
            presentationOnly = true;
            if (!ValidateWiring())
                throw new ArgumentException("FarmCombatViewWiringInvalid");
        }

        public void ConfigureInput(전투입력Adapter value)
        {
            input = value;
            if (input == null || !input.ValidateWiring())
                throw new ArgumentException("FarmCombatInputWiringInvalid");
        }

        public void ConfigureTacticalSquads(전술분대Presenter value)
        {
            tacticalSquads = value;
            if (tacticalSquads == null || !tacticalSquads.ValidateWiring())
                throw new ArgumentException("FarmTacticalSquadViewWiringInvalid");
        }

        public bool ValidateWiring()
            => player != null && player.PresentationOnly
                && input != null && input.ValidateWiring()
                && presentationOnly;

        public void ApplyServerState(
            FarmCombatStateApiModel state,
            string actorStableId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!state.SimulationOnly || state.IsOperationalState)
                throw new InvalidOperationException("FarmCombatAuthorityBoundaryInvalid");
            _latestState = state;
            _actorStableId = actorStableId ?? string.Empty;
            _lastAuthorityErrorCode = string.Empty;
            if (tacticalSquads != null
                && tacticalSquads.TryApplyServerState(state, out var movement)
                && movement != null)
                TacticalMovementPrepared(movement);
            var hasActiveBeat = (state.Beats ?? Array.Empty<FarmCombatBeatApiModel>())
                .Any(value => value.ActorStableId == actorStableId
                    && value.StateCode == FarmCombatPresentationCodes.Active);
            if (!hasActiveBeat)
            {
                PresentLatestReaction(state, actorStableId);
                ClearActiveBeat();
                var hasOpenTacticalWindow = state.Tactical != null
                    && (state.Tactical.OrderWindows
                        ?? Array.Empty<FarmTacticalOrderWindowApiModel>())
                    .Any(value => value.AuthorizedActorStableId == actorStableId
                        && value.StateCode == FarmCombatPresentationCodes.Open);
                if (hasOpenTacticalWindow)
                {
                    _tacticalFrame = _tacticalMapper.Map(state, actorStableId);
                    _inputPhaseCode = FarmCombatPresentationCodes.Resolved;
                    _observedWorldRevision = state.WorldRevision;
                    LastTacticalPreview = null;
                    LastTacticalConfirm = null;
                    TacticalViewTransitionSuggested(_tacticalFrame);
                }
                else
                {
                    ClearTacticalOrderWindow();
                    _inputPhaseCode = HasReadyEngagement(state)
                        ? FarmCombatPresentationCodes.Ready
                        : FarmCombatPresentationCodes.Resolved;
                }
                return;
            }

            ClearTacticalOrderWindow();
            var activeFrame = _mapper.Map(state, actorStableId);
            var newBeat = _beatClock.Observe(activeFrame.BeatStableId,
                Time.realtimeSinceStartupAsDouble * 1000d);
            _activeFrame = activeFrame;
            _observedWorldRevision = state.WorldRevision;
            if (newBeat || _inputPhaseCode == FarmCombatPresentationCodes.Failed)
            {
                _reactionSubmitted = false;
                LastPreparedCommand = null;
                _inputPhaseCode = FarmCombatPresentationCodes.Telegraph;
            }
            if (newBeat) player.EnterCombatMode(_activeFrame.PerspectiveCode);
        }

        public bool TryHandleCombatInput()
        {
            if (input == null) return false;
            var frame = input.ReadFrame();
            if (frame.PointerOverUi && frame.HasAction) return true;

            if (_localBattle != null
                && _localBattle.PhaseCode == BattlePresentationCodes.Active)
                return TryHandleLocalCombatInput(frame, Keyboard.current);

            if (_activeFrame == null)
            {
                if (_inputPhaseCode != FarmCombatPresentationCodes.Ready
                    || !frame.AttackPressed) return LocksPlayerMovement;
                var encounter = (_latestState?.Engagements
                    ?? Array.Empty<FarmCombatEngagementApiModel>())
                    .FirstOrDefault(value => value.StateCode
                        == FarmCombatPresentationCodes.AwaitingCombat);
                if (encounter == null) return false;
                _inputPhaseCode = FarmCombatPresentationCodes.Entering;
                player.ApplyCombatAnimation(공용AnimationIntentCodes.Attack);
                CombatEntryRequested(encounter.EncounterStableId);
                return true;
            }

            if (_reactionSubmitted || player.IsCameraTransitioning)
                return true;

            string? actionCode = null;
            if (frame.AttackPressed)
                actionCode = FarmCombatPresentationCodes.Counter;
            else if (frame.DefendPressed)
                actionCode = FarmCombatPresentationCodes.Guard;
            if (actionCode == null) return true;

            var elapsedMs = _beatClock.ElapsedMilliseconds(
                Time.realtimeSinceStartupAsDouble * 1000d, 1600);
            var command = FarmCombatReactionCommandFactory.Create(
                _activeFrame,
                _observedWorldRevision,
                "command:unity:combat-reaction:"
                    + _activeFrame.BeatStableId + ":" + actionCode,
                actionCode,
                elapsedMs);
            LastPreparedCommand = command;
            _reactionSubmitted = true;
            _inputPhaseCode = FarmCombatPresentationCodes.Submitting;
            player.ApplyCombatAnimation(actionCode == FarmCombatPresentationCodes.Guard
                ? 공용AnimationIntentCodes.Guard
                : 공용AnimationIntentCodes.Attack);
            ReactionCommandPrepared(command);
            return true;
        }

        private bool TryHandleLocalCombatInput(전투입력Frame frame, Keyboard? keyboard)
        {
            if (_localBattle == null || string.IsNullOrWhiteSpace(_actorStableId))
                return false;
            if (_localBattle.LocalCombat.ControlModeCode !=
                    DesiredLocalControlModeCode)
                return true;
            var perspective = player.CurrentMode == 플레이어시점Mode.FirstPerson
                ? LocalCombatPresentationCodes.FirstPerson
                : LocalCombatPresentationCodes.TacticalThirdPerson;
            var target = _localBattle.LocalCombat.FocusedTargetStableId;
            LocalCombatActionCommandDraft? command = null;
            if (keyboard?.digit1Key.wasPressedThisFrame == true
                || keyboard?.numpad1Key.wasPressedThisFrame == true)
                command = LocalCombatInputCommandFactory.CreateActionSlot(_localBattle,
                    perspective, 1, _actorStableId, target, NextLocalCommandId(), 0);
            else if (keyboard?.digit2Key.wasPressedThisFrame == true
                || keyboard?.numpad2Key.wasPressedThisFrame == true)
                command = LocalCombatInputCommandFactory.CreateActionSlot(_localBattle,
                    perspective, 2, _actorStableId, target, NextLocalCommandId(), 0);
            else if (keyboard?.digit3Key.wasPressedThisFrame == true
                || keyboard?.numpad3Key.wasPressedThisFrame == true)
                command = LocalCombatInputCommandFactory.CreateActionSlot(_localBattle,
                    perspective, 3, _actorStableId, target, NextLocalCommandId(), 0);
            else if (keyboard?.digit4Key.wasPressedThisFrame == true
                || keyboard?.numpad4Key.wasPressedThisFrame == true)
                command = LocalCombatInputCommandFactory.CreateActionSlot(_localBattle,
                    perspective, 4, _actorStableId, target, NextLocalCommandId(), 0);
            else if (frame.AttackPressed)
                command = LocalCombatInputCommandFactory.CreatePointerAction(_localBattle,
                    perspective, LocalCombatPresentationCodes.LeftPointer,
                    _localBattle.LocalCombat.HostileTelegraphActive, _actorStableId,
                    target, NextLocalCommandId(), 0);
            else if (frame.DefendPressed)
                command = LocalCombatInputCommandFactory.CreatePointerAction(_localBattle,
                    perspective, LocalCombatPresentationCodes.RightPointer,
                    _localBattle.LocalCombat.HostileTelegraphActive, _actorStableId,
                    target, NextLocalCommandId(), 0);
            if (command == null) return false;
            player.ApplyCombatAnimation(command.ActionCode ==
                LocalCombatPresentationCodes.Guard
                || command.ActionCode == LocalCombatPresentationCodes.Dodge
                || command.ActionCode == LocalCombatPresentationCodes.HoldPosition
                    ? 공용AnimationIntentCodes.Guard
                    : 공용AnimationIntentCodes.Attack);
            LocalActionCommandPrepared(command);
            return true;
        }

        private string NextLocalCommandId() => "command:unity:local-combat:"
            + (++_commandSequence).ToString();

        public void SetAuthorityFailure(string errorCode)
        {
            _lastAuthorityErrorCode = string.IsNullOrWhiteSpace(errorCode)
                ? "FarmCombatAuthorityRequestFailed"
                : errorCode.Trim();
            _inputPhaseCode = FarmCombatPresentationCodes.Failed;
            _reactionSubmitted = false;
            if (player != null)
                player.ApplyCombatAnimation(공용AnimationIntentCodes.Idle);
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
            _beatClock.Clear();
            _reactionSubmitted = false;
            LastPreparedCommand = null;
        }

        public void ClearUnifiedBattle() => _localBattle = null;

        public void ClearTacticalOrderWindow()
        {
            _tacticalFrame = null;
            LastTacticalPreview = null;
            LastTacticalConfirm = null;
        }

        private static bool HasReadyEngagement(FarmCombatStateApiModel state)
            => (state.Engagements ?? Array.Empty<FarmCombatEngagementApiModel>())
                .Any(value => value.StateCode
                    == FarmCombatPresentationCodes.AwaitingCombat);

        private void PresentLatestReaction(
            FarmCombatStateApiModel state,
            string actorStableId)
        {
            var reaction = (state.Reactions
                    ?? Array.Empty<FarmCombatReactionApiModel>())
                .LastOrDefault(value => value.ActorStableId == actorStableId);
            if (reaction == null || reaction.ReactionStableId
                == _lastPresentedReactionStableId) return;
            _lastPresentedReactionStableId = reaction.ReactionStableId;
            player.ApplyCombatAnimation(reaction.ActorDamageUnits > 0m
                ? 공용AnimationIntentCodes.Stagger
                : 공용AnimationIntentCodes.Idle);
        }

        private void OnGUI()
        {
            if (_latestState == null
                || (_inputPhaseCode == FarmCombatPresentationCodes.Resolved
                    && !HasReadyEngagement(_latestState)
                    && _tacticalFrame == null)) return;
            var center = Screen.width * .5f;
            GUI.color = new Color(.025f, .035f, .04f, .9f);
            GUI.DrawTexture(new Rect(center - 185f, 24f, 370f, 82f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(center - 168f, 34f, 336f, 22f),
                "1인칭 전투 · " + KoreanPhase(_inputPhaseCode));
            GUI.Label(new Rect(center - 168f, 58f, 336f, 22f),
                _activeFrame == null
                    ? "좌클릭 공격: 서버 전투 진입"
                    : "좌클릭 반격 · 우클릭 방어");
            GUI.Label(new Rect(center - 168f, 80f, 336f, 20f),
                string.IsNullOrWhiteSpace(_lastAuthorityErrorCode)
                    ? "피해·등급·전술 효과는 서버가 판정합니다."
                    : "동기화 필요: " + _lastAuthorityErrorCode);
        }

        private static string KoreanPhase(string value)
            => value switch
            {
                FarmCombatPresentationCodes.Ready => "전투 준비",
                FarmCombatPresentationCodes.Entering => "전투 진입 중",
                FarmCombatPresentationCodes.Telegraph => "공격 전조",
                FarmCombatPresentationCodes.Submitting => "판정 요청 중",
                FarmCombatPresentationCodes.Failed => "서버 재동기화 필요",
                _ => "전투 결과",
            };
    }
}
