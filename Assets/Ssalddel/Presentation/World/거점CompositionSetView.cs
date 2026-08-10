using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 거점CompositionSetView : MonoBehaviour
    {
        [SerializeField] private 월드CompositionDescriptor descriptor = null!;
        [SerializeField] private Transform environmentRoot = null!;
        [SerializeField] private Transform? occlusionRoot;
        [SerializeField] private Transform[] connectorAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] stateSocketAnchors = Array.Empty<Transform>();
        [SerializeField] private string sourceEntranceDirectionCode = string.Empty;
        [SerializeField] private string designedAccessDirectionCode = string.Empty;
        [SerializeField] private float vehicleTurnRadius;

        public 월드CompositionDescriptor Descriptor => descriptor;
        public Transform EnvironmentRoot => environmentRoot;
        public Transform? OcclusionRoot => occlusionRoot;
        public IReadOnlyList<Transform> ConnectorAnchors => connectorAnchors;
        public IReadOnlyList<Transform> StateSocketAnchors => stateSocketAnchors;
        public string SourceEntranceDirectionCode => sourceEntranceDirectionCode;
        public string DesignedAccessDirectionCode => designedAccessDirectionCode;
        public float VehicleTurnRadius => vehicleTurnRadius;

        public void Configure(
            월드CompositionDescriptor value,
            Transform environment,
            Transform? occlusion,
            Transform[] connectors,
            Transform[] sockets,
            string sourceEntranceDirection,
            string designedAccessDirection,
            float turnRadius)
        {
            descriptor = value;
            environmentRoot = environment;
            occlusionRoot = occlusion;
            connectorAnchors = connectors ?? Array.Empty<Transform>();
            stateSocketAnchors = sockets ?? Array.Empty<Transform>();
            sourceEntranceDirectionCode = sourceEntranceDirection ?? string.Empty;
            designedAccessDirectionCode = designedAccessDirection ?? string.Empty;
            vehicleTurnRadius = turnRadius;
        }

        public bool ValidateWiring()
        {
            if (descriptor == null
                || !descriptor.Validate()
                || environmentRoot == null
                || !environmentRoot.IsChildOf(transform)
                || !거점CompositionEntranceCodes.IsKnown(sourceEntranceDirectionCode)
                || !거점CompositionEntranceCodes.IsKnown(designedAccessDirectionCode)
                || designedAccessDirectionCode == 거점CompositionEntranceCodes.Unknown
                || vehicleTurnRadius < 0f
                || connectorAnchors == null
                || connectorAnchors.Length != descriptor.Connectors.Count
                || stateSocketAnchors == null
                || stateSocketAnchors.Length != descriptor.Sockets.Count)
            {
                return false;
            }

            if (descriptor.HasOcclusionRoot != (occlusionRoot != null)
                || occlusionRoot != null && !occlusionRoot.IsChildOf(transform))
            {
                return false;
            }

            var rendererCount = environmentRoot.GetComponentsInChildren<Renderer>(true).Length
                                + (occlusionRoot == null
                                    ? 0
                                    : occlusionRoot.GetComponentsInChildren<Renderer>(true).Length);
            if (rendererCount == 0)
                return false;
            if (descriptor.Connectors.Any(value =>
                    value.ConnectorKindCode == 월드CompositionConnectorKindCodes.Vehicle)
                && vehicleTurnRadius <= 0f)
            {
                return false;
            }

            return ValidateAnchors(
                       connectorAnchors,
                       transform,
                       descriptor.Connectors.Select(value =>
                           new AnchorExpectation(
                               "Connector_" + value.ConnectorCode,
                               value.LocalPosition,
                               value.LocalYaw)).ToArray())
                   && ValidateAnchors(
                       stateSocketAnchors,
                       transform,
                       descriptor.Sockets.Select(value =>
                           new AnchorExpectation(
                               "Socket_" + value.SocketCode,
                               value.LocalPosition,
                               value.LocalEuler.y)).ToArray());
        }

        public Transform? FindConnector(string connectorCode)
            => connectorAnchors.SingleOrDefault(value =>
                value.name == "Connector_" + connectorCode);

        public Transform? FindSocket(string socketCode)
            => stateSocketAnchors.SingleOrDefault(value =>
                value.name == "Socket_" + socketCode);

        private static bool ValidateAnchors(
            IReadOnlyList<Transform> anchors,
            Transform owner,
            IReadOnlyList<AnchorExpectation> expected)
        {
            if (anchors.Count != expected.Count
                || anchors.Any(value => value == null
                    || value.name == null
                    || !value.IsChildOf(owner)))
            {
                return false;
            }

            for (var index = 0; index < anchors.Count; index++)
            {
                if (anchors[index].name != expected[index].Name
                    || Vector3.Distance(
                        anchors[index].localPosition,
                        expected[index].Position) > .001f
                    || Mathf.Abs(Mathf.DeltaAngle(
                        anchors[index].localEulerAngles.y,
                        expected[index].Yaw)) > .01f)
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct AnchorExpectation
        {
            public AnchorExpectation(string name, Vector3 position, float yaw)
            {
                Name = name;
                Position = position;
                Yaw = yaw;
            }

            public string Name { get; }
            public Vector3 Position { get; }
            public float Yaw { get; }
        }
    }
}
