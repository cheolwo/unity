using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 도로GateCompositionSetView : MonoBehaviour
    {
        [SerializeField] private 월드CompositionDescriptor descriptor = null!;
        [SerializeField] private Transform environmentRoot = null!;
        [SerializeField] private Transform[] connectorAnchors = Array.Empty<Transform>();

        public 월드CompositionDescriptor Descriptor => descriptor;
        public Transform EnvironmentRoot => environmentRoot;
        public IReadOnlyList<Transform> ConnectorAnchors => connectorAnchors;

        public void Configure(
            월드CompositionDescriptor value,
            Transform environment,
            Transform[] anchors)
        {
            descriptor = value;
            environmentRoot = environment;
            connectorAnchors = anchors ?? Array.Empty<Transform>();
        }

        public bool ValidateWiring()
        {
            if (descriptor == null
                || !descriptor.Validate()
                || environmentRoot == null
                || !environmentRoot.IsChildOf(transform)
                || environmentRoot.GetComponentsInChildren<Renderer>(true).Length == 0
                || connectorAnchors == null
                || connectorAnchors.Length != descriptor.Connectors.Count
                || connectorAnchors.Any(value => value == null
                    || !value.IsChildOf(transform))
                || connectorAnchors.Select(value => value.name)
                    .Distinct(StringComparer.Ordinal).Count() != connectorAnchors.Length)
            {
                return false;
            }

            for (var index = 0; index < connectorAnchors.Length; index++)
            {
                var anchor = connectorAnchors[index];
                var contract = descriptor.Connectors[index];
                if (anchor.name != "Connector_" + contract.ConnectorCode
                    || Vector3.Distance(anchor.localPosition, contract.LocalPosition) > .001f
                    || Mathf.Abs(Mathf.DeltaAngle(
                        anchor.localEulerAngles.y,
                        contract.LocalYaw)) > .01f)
                {
                    return false;
                }
            }

            return true;
        }

        public Transform? FindConnector(string connectorCode)
            => connectorAnchors.SingleOrDefault(value =>
                value.name == "Connector_" + connectorCode);
    }
}
