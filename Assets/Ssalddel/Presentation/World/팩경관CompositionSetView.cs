using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 팩경관CompositionSetView : MonoBehaviour
    {
        [SerializeField] private 월드CompositionDescriptor descriptor = null!;
        [SerializeField] private Transform environmentRoot = null!;
        [SerializeField] private Transform[] connectorAnchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] socketAnchors = Array.Empty<Transform>();
        [SerializeField] private bool presentationOnly = true;

        public 월드CompositionDescriptor Descriptor => descriptor;
        public Transform EnvironmentRoot => environmentRoot;
        public IReadOnlyList<Transform> ConnectorAnchors => connectorAnchors;
        public IReadOnlyList<Transform> SocketAnchors => socketAnchors;
        public bool PresentationOnly => presentationOnly;

        public void Configure(
            월드CompositionDescriptor value,
            Transform environment,
            Transform[] connectors,
            Transform[] sockets)
        {
            descriptor = value;
            environmentRoot = environment;
            connectorAnchors = connectors ?? Array.Empty<Transform>();
            socketAnchors = sockets ?? Array.Empty<Transform>();
            presentationOnly = true;
        }

        public bool ValidateWiring()
        {
            if (descriptor == null
                || !descriptor.Validate()
                || environmentRoot == null
                || !environmentRoot.IsChildOf(transform)
                || environmentRoot.GetComponentsInChildren<Renderer>(true).Length == 0
                || connectorAnchors.Length != descriptor.Connectors.Count
                || socketAnchors.Length != descriptor.Sockets.Count
                || !presentationOnly)
            {
                return false;
            }

            return ValidateAnchors(
                       connectorAnchors,
                       descriptor.Connectors.Select(value =>
                           ("Connector_" + value.ConnectorCode,
                               value.LocalPosition, value.LocalYaw)).ToArray())
                   && ValidateAnchors(
                       socketAnchors,
                       descriptor.Sockets.Select(value =>
                           ("Socket_" + value.SocketCode,
                               value.LocalPosition, value.LocalEuler.y)).ToArray());
        }

        private bool ValidateAnchors(
            IReadOnlyList<Transform> anchors,
            IReadOnlyList<(string Name, Vector3 Position, float Yaw)> expected)
        {
            for (var index = 0; index < anchors.Count; index++)
            {
                var anchor = anchors[index];
                if (anchor == null
                    || !anchor.IsChildOf(transform)
                    || anchor.name != expected[index].Name
                    || Vector3.Distance(anchor.localPosition, expected[index].Position) > .001f
                    || Mathf.Abs(Mathf.DeltaAngle(
                        anchor.localEulerAngles.y, expected[index].Yaw)) > .01f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
