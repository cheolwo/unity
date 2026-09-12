using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    public static class 농장풍경SocketCodes
    {
        public const string 실제감자밭 = "farm.socket.potato-field";
        public const string 농부 = "farm.socket.worker";
        public const string 차량 = "farm.socket.vehicle";
        public const string 농기계 = "farm.socket.implement";
        public const string 화물 = "farm.socket.cargo";
        public const string 상호작용 = "farm.socket.interaction";

        public static IReadOnlyList<string> All { get; } = new[]
        {
            실제감자밭,
            농부,
            차량,
            농기계,
            화물,
            상호작용,
        };

        public static bool IsKnown(string value)
            => !string.IsNullOrWhiteSpace(value)
                && All.Contains(value, StringComparer.Ordinal);
    }

    [DisallowMultipleComponent]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E7,
        "플레이어 입력·화면·피드백과 플레이 경험 표현을 조율한다.",
        Boundary = "Unity 표현은 권위 상태 변경이나 실제 플레이 완료를 대신하지 않는다.")]
    public sealed class 농장풍경CompositionSocketView : MonoBehaviour
    {
        [SerializeField] private string socketCode = string.Empty;

        public string SocketCode => socketCode;

        public void Configure(string code)
            => socketCode = code;

        public bool ValidateWiring()
            => 농장풍경SocketCodes.IsKnown(socketCode);
    }
}
