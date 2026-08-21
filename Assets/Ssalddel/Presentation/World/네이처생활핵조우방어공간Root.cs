using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>
    /// 위치 독립 H3 설계를 canonical Scene에 compact 형태로 배치한 표현 계보입니다.
    /// 실제 지역 좌표, 공공데이터 또는 Simulation 상태의 권위가 아닙니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class 네이처생활핵조우방어공간Root : MonoBehaviour
    {
        [SerializeField] private string h3StableId = string.Empty;
        [SerializeField] private string semanticRevision = string.Empty;
        [SerializeField] private string theoryHashSha256 = string.Empty;
        [SerializeField] private string compactVariantCode = string.Empty;
        [SerializeField] private 네이처H3NodeBinding[] nodes = Array.Empty<네이처H3NodeBinding>();
        [SerializeField] private 네이처H3EdgeBinding[] edges = Array.Empty<네이처H3EdgeBinding>();
        [SerializeField] private 네이처H3ConnectorBinding[] connectors =
            Array.Empty<네이처H3ConnectorBinding>();
        [SerializeField] private bool presentationOnly = true;

        public string H3StableId => h3StableId;
        public string SemanticRevision => semanticRevision;
        public string TheoryHashSha256 => theoryHashSha256;
        public string CompactVariantCode => compactVariantCode;
        public IReadOnlyList<네이처H3NodeBinding> Nodes => nodes;
        public IReadOnlyList<네이처H3EdgeBinding> Edges => edges;
        public IReadOnlyList<네이처H3ConnectorBinding> Connectors => connectors;
        public bool PresentationOnly => presentationOnly;

        public void Configure(string stableId, string relationRevision,
            string theoryHash, string variantCode,
            네이처H3NodeBinding[] nodeBindings,
            네이처H3EdgeBinding[] edgeBindings,
            네이처H3ConnectorBinding[] connectorBindings)
        {
            h3StableId = stableId ?? string.Empty;
            semanticRevision = relationRevision ?? string.Empty;
            theoryHashSha256 = theoryHash ?? string.Empty;
            compactVariantCode = variantCode ?? string.Empty;
            nodes = nodeBindings ?? Array.Empty<네이처H3NodeBinding>();
            edges = edgeBindings ?? Array.Empty<네이처H3EdgeBinding>();
            connectors = connectorBindings ?? Array.Empty<네이처H3ConnectorBinding>();
            presentationOnly = true;
        }

        public Transform? FindConnectorAnchor(string roleCode)
            => connectors.FirstOrDefault(value => string.Equals(value.RoleCode,
                roleCode, StringComparison.Ordinal))?.Anchor;

        public bool Validate()
        {
            if (h3StableId != "h3-candidate:nature-home-encounter-defense"
                || semanticRevision != "simulation-world-semantic-spatial-relations.r2"
                || theoryHashSha256.Length != 64
                || !theoryHashSha256.All(Uri.IsHexDigit)
                || compactVariantCode != "CompactWalkable"
                || nodes.Length != 3 || edges.Length != 3
                || connectors.Length != 6 || !presentationOnly)
                return false;

            var nodeIds = nodes.Select(value => value.NodeStableId)
                .ToHashSet(StringComparer.Ordinal);
            if (nodeIds.Count != nodes.Length || nodes.Any(value => !value.Validate()))
                return false;
            if (edges.Any(value => !value.Validate(nodeIds))) return false;
            if (connectors.Any(value => !value.Validate(nodeIds))) return false;
            if (edges.Select(value => value.EdgeStableId)
                    .Distinct(StringComparer.Ordinal).Count() != edges.Length
                || connectors.Select(value => value.ConnectorStableId)
                    .Distinct(StringComparer.Ordinal).Count() != connectors.Length)
                return false;

            return edges.Any(value => value.RelationKindCode == "RecoveryHandoff"
                                      && value.FromNodeStableId ==
                                      "h2-nature-defense-ring"
                                      && value.ToNodeStableId ==
                                      "h2-nature-home-core")
                   && connectors.Any(value => value.RoleCode == "ThreatInput")
                   && connectors.Any(value => value.RoleCode == "RecoveryReturn");
        }
    }

    [Serializable]
    public sealed class 네이처H3NodeBinding
    {
        [SerializeField] private string nodeStableId = string.Empty;
        [SerializeField] private string h2StableId = string.Empty;
        [SerializeField] private Transform anchor = null!;

        public string NodeStableId => nodeStableId;
        public string H2StableId => h2StableId;
        public Transform Anchor => anchor;

        public 네이처H3NodeBinding(string nodeId, string h2Id, Transform nodeAnchor)
        {
            nodeStableId = nodeId;
            h2StableId = h2Id;
            anchor = nodeAnchor;
        }

        public bool Validate()
            => nodeStableId.StartsWith("h2-nature-", StringComparison.Ordinal)
               && h2StableId.StartsWith("h2-candidate:nature-",
                   StringComparison.Ordinal) && anchor != null;
    }

    [Serializable]
    public sealed class 네이처H3EdgeBinding
    {
        [SerializeField] private string edgeStableId = string.Empty;
        [SerializeField] private string fromNodeStableId = string.Empty;
        [SerializeField] private string toNodeStableId = string.Empty;
        [SerializeField] private string relationKindCode = string.Empty;
        [SerializeField] private string relationDirectionCode = string.Empty;

        public string EdgeStableId => edgeStableId;
        public string FromNodeStableId => fromNodeStableId;
        public string ToNodeStableId => toNodeStableId;
        public string RelationKindCode => relationKindCode;

        public 네이처H3EdgeBinding(string edgeId, string fromNodeId,
            string toNodeId, string relationKind, string relationDirection)
        {
            edgeStableId = edgeId;
            fromNodeStableId = fromNodeId;
            toNodeStableId = toNodeId;
            relationKindCode = relationKind;
            relationDirectionCode = relationDirection;
        }

        public bool Validate(ISet<string> nodeIds)
            => edgeStableId.StartsWith(
                   "relation:h3-nature-home-encounter-defense:",
                   StringComparison.Ordinal)
               && nodeIds.Contains(fromNodeStableId)
               && nodeIds.Contains(toNodeStableId)
               && fromNodeStableId != toNodeStableId
               && relationKindCode is "PlayerTraversal" or "RecoveryHandoff"
               && relationDirectionCode == "Directed";
    }

    [Serializable]
    public sealed class 네이처H3ConnectorBinding
    {
        [SerializeField] private string connectorStableId = string.Empty;
        [SerializeField] private string roleCode = string.Empty;
        [SerializeField] private string directionCode = string.Empty;
        [SerializeField] private string attachedNodeStableId = string.Empty;
        [SerializeField] private Transform anchor = null!;

        public string ConnectorStableId => connectorStableId;
        public string RoleCode => roleCode;
        public string DirectionCode => directionCode;
        public string AttachedNodeStableId => attachedNodeStableId;
        public Transform Anchor => anchor;

        public 네이처H3ConnectorBinding(string connectorId, string role,
            string direction, string nodeId, Transform connectorAnchor)
        {
            connectorStableId = connectorId;
            roleCode = role;
            directionCode = direction;
            attachedNodeStableId = nodeId;
            anchor = connectorAnchor;
        }

        public bool Validate(ISet<string> nodeIds)
            => connectorStableId.StartsWith(
                   "connector:h3:nature-home-encounter-defense:",
                   StringComparison.Ordinal)
               && !string.IsNullOrWhiteSpace(roleCode)
               && directionCode is "Input" or "Output" or "Bidirectional"
               && nodeIds.Contains(attachedNodeStableId) && anchor != null;
    }
}
