using System.Threading.Tasks;

namespace Ssalddel.MobileGame
{
    /// <summary>Android 문서 선택·공유와 게임의 오프라인 자료 검사를 분리합니다.</summary>
    public interface IMobileGameFileExchange
    {
        Task<string> ReadTextAsync();
        Task ShareTextAsync(string fileName, string text, string mimeType);
    }
}
