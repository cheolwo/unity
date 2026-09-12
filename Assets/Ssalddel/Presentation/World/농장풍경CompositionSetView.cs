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
    public sealed class 농장풍경CompositionSetView : MonoBehaviour
    {
        [SerializeField] private string setName = string.Empty;
        [SerializeField] private string variantCode = string.Empty;
        [SerializeField] private Vector2 footprint;
        [SerializeField] private Transform environmentRoot = null!;
        [SerializeField] private 농장풍경CompositionSocketView[] sockets =
            Array.Empty<농장풍경CompositionSocketView>();

        public string SetName => setName;
        public string VariantCode => variantCode;
        public Vector2 Footprint => footprint;
        public Transform EnvironmentRoot => environmentRoot;
        public IReadOnlyList<농장풍경CompositionSocketView> Sockets => sockets;

        public void Configure(
            string name,
            string variant,
            Vector2 size,
            Transform environment,
            농장풍경CompositionSocketView[] stateSockets)
        {
            setName = name;
            variantCode = variant;
            footprint = size;
            environmentRoot = environment;
            sockets = stateSockets ?? Array.Empty<농장풍경CompositionSocketView>();
        }

        public bool ValidateWiring()
            => 농장풍경SetNames.IsKnown(setName)
                && 농장풍경VariantCodes.IsKnown(variantCode)
                && footprint.x > 0f
                && footprint.y > 0f
                && environmentRoot != null
                && environmentRoot.IsChildOf(transform)
                && environmentRoot.GetComponentsInChildren<Renderer>(true).Length >= 3
                && sockets != null
                && sockets.All(value => value != null
                    && value.transform.IsChildOf(transform)
                    && value.ValidateWiring())
                && sockets.Select(value => value.SocketCode)
                    .Distinct(StringComparer.Ordinal).Count() == sockets.Length;

        public 농장풍경CompositionSocketView? FindSocket(string socketCode)
            => sockets.SingleOrDefault(value => value.SocketCode == socketCode);
    }
}
