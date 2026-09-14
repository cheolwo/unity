using System;
using System.Collections.Generic;
using Ssalddel.Unity.Runtime.World;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 기존 표현 구성 요소가 공통 역세권 모듈에 제공하는 읽기 전용 준비도다.
    /// 이 계약은 자료를 해석하거나 상태 전이를 실행하지 않는다.
    /// </summary>
    public interface IStationDioramaModulePresentationSource
    {
        string ModuleLayerStableId { get; }
        string ModuleSourceStableId { get; }
        string ModuleSourceRevision { get; }
        string ModuleDiagnosticCode { get; }
        bool HasAcceptedPresentation { get; }
        Behaviour PresentationBehaviour { get; }
    }

    /// <summary>
    /// 한 역의 한 모듈 contribution과 Host 등록 수명을 공통으로 관리한다.
    /// 실제 자료·Mesh·업무 상태는 소유하지 않는다.
    /// </summary>
    public abstract class StationDioramaSingleLayerAdapter : MonoBehaviour,
        IStationDioramaModuleAdapter
    {
        private ActiveStationDioramaHost moduleHost;
        private bool moduleVisible = true;

        public abstract string TransitStationStableId { get; }
        public abstract string LayerStableId { get; }
        public bool ModuleVisible => moduleVisible;
        public ActiveStationDioramaHost ModuleHost => moduleHost;
        protected virtual bool RestoreStandaloneVisibilityAfterDetach => true;

        public IReadOnlyList<StationDioramaModuleLayerRuntimeState> DescribeModuleLayers()
        {
            var state = DescribeLayerState()
                        ?? throw new InvalidOperationException(
                            "StationDioramaModuleLayerStateMissing");
            if (!string.Equals(state.LayerStableId, LayerStableId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "StationDioramaModuleLayerStateMismatch");
            return new[] { state };
        }

        public void AttachToHost(ActiveStationDioramaHost value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(moduleHost, value))
            {
                value.Refresh();
                return;
            }

            DetachFromHost();
            moduleHost = value;
            try
            {
                value.Register(this);
            }
            catch
            {
                moduleHost = null;
                SetModuleVisible(false);
                throw;
            }
        }

        public void DetachFromHost()
        {
            var previous = moduleHost;
            moduleHost = null;
            if (previous != null) previous.Unregister(this);
            SetModuleVisible(RestoreStandaloneVisibilityAfterDetach);
        }

        public bool RefreshHost()
            => moduleHost != null && moduleHost.Refresh();

        public void SetModuleVisible(bool value)
        {
            if (moduleVisible == value) return;
            moduleVisible = value;
            OnModuleVisibilityChanged(value);
        }

        protected abstract StationDioramaModuleLayerRuntimeState DescribeLayerState();

        protected virtual void OnModuleVisibilityChanged(bool value)
        {
        }

        protected virtual void OnDestroy()
            => DetachFromHost();
    }
}
