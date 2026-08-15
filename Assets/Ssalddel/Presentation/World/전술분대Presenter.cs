using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Unity.Survival;
using Unity.AI.Navigation;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 전술분대Presenter : MonoBehaviour
    {
        [SerializeField] private GameObject tacticalContextRoot = null!;
        [SerializeField] private GameObject tacticalBattleRoot = null!;
        [SerializeField] private NavMeshSurface navigationSurface = null!;
        [SerializeField] private 전술분대대형Controller[] squads
            = Array.Empty<전술분대대형Controller>();
        [SerializeField] private bool presentationOnly = true;

        private readonly FarmTacticalMovementPresentationMapper _mapper = new();
        private long _lastWorldRevision = -1;
        private string _lastResolutionStableId = string.Empty;
        private bool _navigationBuiltForRuntime;

        public string LastResolutionStableId => _lastResolutionStableId;
        public long LastWorldRevision => _lastWorldRevision;
        public IReadOnlyList<전술분대대형Controller> Squads => squads;
        public bool PresentationOnly => presentationOnly;

        public void Configure(GameObject contextRoot, GameObject root,
            NavMeshSurface surface,
            전술분대대형Controller[] squadViews)
        {
            tacticalContextRoot = contextRoot;
            tacticalBattleRoot = root;
            navigationSurface = surface;
            squads = squadViews ?? Array.Empty<전술분대대형Controller>();
            presentationOnly = true;
            if (!ValidateWiring())
                throw new ArgumentException("TacticalSquadPresenterConfigurationInvalid");
        }

        public bool TryApplyServerState(FarmCombatStateApiModel state,
            out FarmTacticalMovementPresentationFrame? frame)
        {
            frame = null;
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Tactical?.Resolutions == null
                || state.Tactical.Resolutions.Length == 0)
                return false;
            var mapped = _mapper.MapLatest(state);
            if (mapped.WorldRevision < _lastWorldRevision)
                throw new InvalidOperationException(
                    "TacticalSquadMovementRevisionStale");
            if (mapped.ResolutionStableId == _lastResolutionStableId)
                return false;

            var context = tacticalContextRoot.transform;
            while (context != null)
            {
                context.gameObject.SetActive(true);
                if (context == transform.root) break;
                context = context.parent;
            }
            tacticalBattleRoot.SetActive(true);
            // 정착지 루트가 비활성인 편집 시점에 저장한 자료는 빈 면일 수 있다.
            // 실제 전술 구역이 처음 열린 시점의 활성 Collider로 한 번만 다시 만든다.
            if (!_navigationBuiltForRuntime)
            {
                navigationSurface.RemoveData();
                navigationSurface.BuildNavMesh();
                _navigationBuiltForRuntime = true;
            }
            foreach (var squadFrame in mapped.Squads)
            {
                var view = squads.SingleOrDefault(value =>
                    value.SideCode == squadFrame.SideCode)
                    ?? throw new InvalidOperationException(
                        "TacticalSquadViewSideMissing:" + squadFrame.SideCode);
                view.ApplyFrame(squadFrame);
            }
            _lastWorldRevision = mapped.WorldRevision;
            _lastResolutionStableId = mapped.ResolutionStableId;
            frame = mapped;
            return true;
        }

        public bool ValidateWiring()
            => tacticalContextRoot != null && tacticalBattleRoot != null
                && tacticalBattleRoot.transform.IsChildOf(tacticalContextRoot.transform)
                && navigationSurface != null
                && navigationSurface.transform.IsChildOf(tacticalBattleRoot.transform)
                && squads.Length == 2
                && squads.All(value => value != null && value.ValidateWiring())
                && squads.Select(value => value.SideCode).Distinct().Count() == 2
                && presentationOnly;
    }
}
