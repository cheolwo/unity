using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Battles;
using Ssalddel.Unity.Cards;
using Ssalddel.Unity.Infrastructure.Simulation;
using Ssalddel.Unity.Infrastructure.Transport;
using Ssalddel.Unity.Presentation.Configuration;
using Ssalddel.Unity.Presentation.World;
using Ssalddel.Unity.Runtime.World;
using Ssalddel.Unity.TeamRoles;
using UnityEngine;

namespace Ssalddel.Unity.Bootstrap
{
    [DefaultExecutionOrder(-820)]
    [DisallowMultipleComponent]
    public sealed class 카드서랍CompositionRoot : MonoBehaviour
    {
        [SerializeField] private UnityClientRuntimeSettings runtimeSettings = null!;
        [SerializeField] private SimulationWorldShellPresenter shell = null!;
        [SerializeField] private 카드서랍Presenter presenter = null!;
        [SerializeField] private 턴마감Presenter turnClosing = null!;
        [SerializeField] private 현장전투CompositionRoot battle = null!;
        [SerializeField] private string actorStableId = "actor:sim:player-survivor";
        [SerializeField] private bool 서버기준사용 = true;

        private TeamRoleCardClientCoordinator? teamCards;
        private CancellationTokenSource? lifetime;
        private bool commandInFlight;

        public bool ServerAuthorityEnabled => 서버기준사용;

        public void Configure(UnityClientRuntimeSettings settings,
            SimulationWorldShellPresenter worldShell, 카드서랍Presenter drawer,
            턴마감Presenter turnClosingPresenter,
            현장전투CompositionRoot battleComposition,
            string actorId, bool useServerAuthority)
        {
            runtimeSettings = settings; shell = worldShell; presenter = drawer;
            turnClosing = turnClosingPresenter; battle = battleComposition;
            actorStableId = actorId ?? string.Empty;
            서버기준사용 = useServerAuthority;
            Bind();
        }

        private void Awake()
        {
            shell ??= FindFirstObjectByType<SimulationWorldShellPresenter>(
                FindObjectsInactive.Include);
            presenter ??= FindFirstObjectByType<카드서랍Presenter>(
                FindObjectsInactive.Include);
            turnClosing ??= FindFirstObjectByType<턴마감Presenter>(
                FindObjectsInactive.Include);
            battle ??= FindFirstObjectByType<현장전투CompositionRoot>(
                FindObjectsInactive.Include);
            Bind();
        }

        private void Start()
        {
            ValidateWiring();
            if (!서버기준사용) return;
            var api = new SimulationRehearsalUnityWebRequestApiClient(
                runtimeSettings.ToOptions());
            teamCards = new TeamRoleCardClientCoordinator(
                new 팀역할CardServerRepository(api),
                new TeamRoleCardPresentationMapper());
        }

        private void OnDestroy()
        {
            if (presenter != null)
            {
                presenter.RefreshRequested -= OnRefreshRequested;
                presenter.ActionRequested -= OnActionRequested;
            }
            lifetime?.Cancel();
            lifetime?.Dispose();
        }

        public void ValidateWiring()
        {
            if (runtimeSettings == null || shell == null || presenter == null
                || turnClosing == null
                || string.IsNullOrWhiteSpace(actorStableId))
                throw new InvalidOperationException("CardDrawerCompositionWiringInvalid");
            presenter.ValidateWiring();
        }

        private void Bind()
        {
            if (presenter == null) return;
            presenter.RefreshRequested -= OnRefreshRequested;
            presenter.RefreshRequested += OnRefreshRequested;
            presenter.ActionRequested -= OnActionRequested;
            presenter.ActionRequested += OnActionRequested;
        }

        private async void OnRefreshRequested()
        {
            try { await RefreshAsync(); }
            catch (Exception exception)
            {
                presenter.SetStatus("카드 서랍 조회 실패: " + exception.Message);
                Debug.LogException(exception, this);
            }
        }

        public async Task RefreshAsync()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            var token = lifetime.Token;
            TeamRoleCardPresentationState? teamState = null;
            if (teamCards != null && !string.IsNullOrWhiteSpace(shell.SessionStableId))
            {
                try
                {
                    teamState = await teamCards.LoadAsync(shell.SessionStableId,
                        actorStableId, token);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("팀 역할 카드 원장을 아직 조회할 수 없습니다: "
                        + exception.Message, this);
                }
            }

            var families = BuildFamilies(turnClosing.CurrentContext, teamState,
                battle?.Current);
            var coordinator = new CardWorkspaceCoordinator(families.Select(value =>
                new DelegateCardFamilySource(value.FamilyCode,
                    _ => Task.FromResult(value))));
            presenter.Apply(await coordinator.LoadAsync(token));
            presenter.SetStatus("서버 원장 재조회 완료 · 위계는 의미 층이며 권한이 아닙니다.");
        }

        private async void OnActionRequested(CardWorkspaceItem item,
            string controlModeCode)
        {
            if (commandInFlight) return;
            try
            {
                commandInFlight = true;
                if (!string.IsNullOrWhiteSpace(controlModeCode))
                {
                    await SetPrimaryCombatCardAsync(item, controlModeCode);
                    return;
                }

                switch (item.ActionRouteCode)
                {
                    case CardActionRouteCodes.OpenTurnClosing:
                        presenter.SetOpen(false);
                        turnClosing.SetContextVisible(true);
                        break;
                    case CardActionRouteCodes.OpenBattle:
                        presenter.SetOpen(false);
                        presenter.SetStatus("전투 시작 사본은 현재 BattleInstance에서만 사용됩니다.");
                        break;
                    case CardActionRouteCodes.OpenResearchSeedbed:
                        presenter.SetStatus("연구 모판은 실행 권위가 없는 별도 시험 영역입니다.");
                        break;
                    case CardActionRouteCodes.OpenInformation:
                        presenter.SetStatus(item.Summary);
                        break;
                    default:
                        presenter.SetStatus("이 행동은 기존 역할 카드 소유자에게 위임됩니다.");
                        break;
                }
            }
            catch (Exception exception)
            {
                presenter.SetStatus("카드 행동 실패: " + exception.Message);
                Debug.LogException(exception, this);
            }
            finally { commandInFlight = false; }
        }

        private async Task SetPrimaryCombatCardAsync(CardWorkspaceItem item,
            string controlModeCode)
        {
            if (item.FamilyCode != CardFamilyCodes.TeamRole || item.IsLocked)
                throw new InvalidOperationException("CombatLoadoutCardUnavailable");
            if (battle?.Current != null
                && battle.Current.PhaseCode == BattlePresentationCodes.Active)
                throw new InvalidOperationException("CombatLoadoutFrozenDuringBattle");
            var state = teamCards?.Current
                ?? throw new InvalidOperationException("TeamRoleCardStateMissing");
            var support = state.CombatLoadouts
                .SingleOrDefault(value => value.ActorStableId == actorStableId
                    && value.CombatControlModeCode == controlModeCode)?.Slots
                .SingleOrDefault(value => value.SlotCode == "Support")
                ?.CardCopyStableId ?? string.Empty;
            var slots = new List<TeamCombatCardLoadoutSlotApiModel>
            {
                new TeamCombatCardLoadoutSlotApiModel
                {
                    SlotCode = "Primary",
                    CardCopyStableId = item.CardCopyStableId,
                },
            };
            if (!string.IsNullOrWhiteSpace(support)
                && support != item.CardCopyStableId)
                slots.Add(new TeamCombatCardLoadoutSlotApiModel
                {
                    SlotCode = "Support",
                    CardCopyStableId = support,
                });
            await teamCards.SetCombatLoadoutAsync(shell.SessionStableId,
                new TeamCombatCardLoadoutSetApiRequest
                {
                    ClientRequestId = Guid.NewGuid(),
                    ExpectedRevision = state.Revision,
                    ExpectedTeamPolicyRevision = state.TeamPolicyRevision,
                    RequestingActorStableId = actorStableId,
                    TargetActorStableId = actorStableId,
                    CombatControlModeCode = controlModeCode,
                    Slots = slots.ToArray(),
                }, lifetime?.Token ?? CancellationToken.None);
            presenter.SetStatus((controlModeCode == "DirectAction"
                    ? "직접 전투" : "전술 지휘")
                + " 주력 편성을 서버가 확정했습니다.");
            await RefreshAsync();
        }

        private static CardWorkspaceFamilySnapshot[] BuildFamilies(
            턴마감ContextData? turn, TeamRoleCardPresentationState? team,
            BattleInstanceApiModel? battle)
            => new[]
            {
                BuildTarot(turn),
                BuildTurnCards(turn, false),
                BuildTurnCards(turn, true),
                BuildTeamCards(team),
                BuildBattleCards(battle),
                StaticFamily(CardFamilyCodes.ConceptInformation,
                    CardHierarchyTierCodes.Knowledge,
                    CardAuthorityCodes.ProjectionReadOnly,
                    "information:world.card-authority",
                    "카드 권위 경계",
                    "타로는 문맥을 제안하고, 실제 허용과 결과는 각 서버 도메인 규칙이 확정합니다.",
                    CardActionRouteCodes.OpenInformation),
                StaticFamily(CardFamilyCodes.ResearchSeedbed,
                    CardHierarchyTierCodes.Research,
                    CardAuthorityCodes.ResearchOnly,
                    "research-seedbed:turn-card-candidates",
                    "턴 카드 연구 모판",
                    "기존 연구 Scene의 후보 카드이며 운영 세계를 변경하지 않습니다.",
                    CardActionRouteCodes.OpenResearchSeedbed),
            };

        private static CardWorkspaceFamilySnapshot BuildTarot(턴마감ContextData? turn)
        {
            var frames = turn?.TarotContext.ActiveFrames ?? Array.Empty<타로FrameData>();
            var offers = turn?.TarotDraw.Offers ?? Array.Empty<턴마감타로OfferData>();
            return new CardWorkspaceFamilySnapshot
            {
                FamilyCode = CardFamilyCodes.Tarot,
                SourceRevision = turn?.TarotContext.Revision ?? 0,
                Items = frames.Select(frame =>
                {
                    var card = offers.FirstOrDefault(value =>
                        value.Card.CardStableId == frame.CardStableId)?.Card;
                    return new CardWorkspaceItem
                    {
                        CardStableId = frame.CardStableId,
                        CardCopyStableId = frame.CardCopyStableId,
                        Title = card?.Title ?? frame.CardStableId,
                        Summary = string.Join(" · ", frame.ThemeCodes)
                            + " / " + frame.OrientationCode,
                        FamilyCode = CardFamilyCodes.Tarot,
                        HierarchyTierCode = CardHierarchyTierCodes.Meta,
                        AuthorityCode = CardAuthorityCodes.ServerMutable,
                        ActionRouteCode = CardActionRouteCodes.OpenTurnClosing,
                        IsAvailable = true,
                    };
                }).ToArray(),
                Relations = (turn?.TarotContext.Relations
                        ?? Array.Empty<타로CardRelationData>())
                    .Select(relation => new CardWorkspaceRelation
                    {
                        SourceCardStableId = frames.FirstOrDefault(value =>
                            value.FrameStableId == relation.SourceFrameStableId)
                            ?.CardStableId ?? relation.SourceFrameStableId,
                        TargetCardStableId = relation.TargetCardStableId,
                        RelationCode = relation.RelationCode,
                        ChangesAvailability = relation.ChangesAvailability,
                    }).ToArray(),
            };
        }

        private static CardWorkspaceFamilySnapshot BuildTurnCards(
            턴마감ContextData? turn, bool culture)
        {
            var family = culture ? CardFamilyCodes.Culture
                : CardFamilyCodes.TurnClosing;
            return new CardWorkspaceFamilySnapshot
            {
                FamilyCode = family,
                SourceRevision = turn?.Revision ?? 0,
                Items = (turn?.AvailableCards ?? Array.Empty<턴마감CardData>())
                    .Where(card => (card.CardKindCode == "Culture") == culture)
                    .Select(card => new CardWorkspaceItem
                    {
                        CardStableId = card.CardStableId,
                        Title = card.Title,
                        Summary = card.Summary,
                        FamilyCode = family,
                        HierarchyTierCode = CardHierarchyTierCodes.Context,
                        AuthorityCode = CardAuthorityCodes.ServerMutable,
                        ActionRouteCode = CardActionRouteCodes.OpenTurnClosing,
                        IsAvailable = true,
                    }).ToArray(),
            };
        }

        private static CardWorkspaceFamilySnapshot BuildTeamCards(
            TeamRoleCardPresentationState? team)
            => new()
            {
                FamilyCode = CardFamilyCodes.TeamRole,
                SourceRevision = team?.Revision ?? 0,
                Items = (team?.Cards ?? Array.Empty<TeamRoleCardApiModel>())
                    .Select(card => new CardWorkspaceItem
                    {
                        CardStableId = card.CardDefinitionStableId,
                        CardCopyStableId = card.CardCopyStableId,
                        Title = card.Title,
                        Summary = string.Join(" · ", card.ActivityRoleCodes),
                        FamilyCode = CardFamilyCodes.TeamRole,
                        HierarchyTierCode = CardHierarchyTierCodes.Action,
                        AuthorityCode = CardAuthorityCodes.ServerMutable,
                        ActionRouteCode = CardActionRouteCodes.SetTeamRole,
                        IsAvailable = !card.IsLocked,
                        IsLocked = card.IsLocked,
                    }).ToArray(),
            };

        private static CardWorkspaceFamilySnapshot BuildBattleCards(
            BattleInstanceApiModel? current)
            => new()
            {
                FamilyCode = CardFamilyCodes.BattleSnapshot,
                SourceRevision = current?.BattleRevision ?? 0,
                Items = (current?.UnitRoster.CardModifiers
                        ?? Array.Empty<BattleCardModifierApiModel>())
                    .Select(card => new CardWorkspaceItem
                    {
                        CardStableId = card.CardDefinitionStableId,
                        CardCopyStableId = card.CardCopyStableId,
                        Title = string.IsNullOrWhiteSpace(card.CardDefinitionStableId)
                            ? card.CardCopyStableId : card.CardDefinitionStableId,
                        Summary = card.ModifierCode + " " + card.BasisPoints
                            + "bp · 전투 시작 사본",
                        FamilyCode = CardFamilyCodes.BattleSnapshot,
                        HierarchyTierCode = CardHierarchyTierCodes.Action,
                        AuthorityCode = CardAuthorityCodes.ServerFrozenSnapshot,
                        ActionRouteCode = CardActionRouteCodes.OpenBattle,
                        ApplicableControlModeCode = card.ApplicableControlModeCode,
                        IsAvailable = false,
                        IsLocked = true,
                    }).ToArray(),
            };

        private static CardWorkspaceFamilySnapshot StaticFamily(string family,
            string tier, string authority, string stableId, string title,
            string summary, string actionRoute)
            => new()
            {
                FamilyCode = family,
                Items = new[]
                {
                    new CardWorkspaceItem
                    {
                        CardStableId = stableId,
                        Title = title,
                        Summary = summary,
                        FamilyCode = family,
                        HierarchyTierCode = tier,
                        AuthorityCode = authority,
                        ActionRouteCode = actionRoute,
                        IsAvailable = true,
                    },
                },
            };
    }
}
