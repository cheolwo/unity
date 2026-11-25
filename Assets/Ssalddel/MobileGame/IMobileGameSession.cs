using System;
using System.Threading.Tasks;

namespace Ssalddel.MobileGame
{
    /// <summary>화면은 같은 로컬 회차의 조회 사본과 명령만 소비한다. 시계·저장·권한은 서비스가 소유한다.</summary>
    public interface IMobileGameSession
    {
        Task<string> RequestJsonAsync(string json);
        event Action<string> Changed;
        string SnapshotJson { get; }
    }
}
