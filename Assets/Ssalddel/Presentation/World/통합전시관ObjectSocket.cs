using UnityEngine;

namespace Ssalddel.Unity.Presentation.World
{
    [DisallowMultipleComponent]
    public sealed class 통합전시관ObjectSocket : MonoBehaviour
    {
        [SerializeField] private string socketCode = string.Empty;

        public string SocketCode => socketCode;

        public void Configure(string code) => socketCode = code;

        public bool ValidateWiring() => !string.IsNullOrWhiteSpace(socketCode);
    }
}
