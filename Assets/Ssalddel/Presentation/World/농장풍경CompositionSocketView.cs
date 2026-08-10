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
