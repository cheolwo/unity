#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Ssalddel.MobileGame
{
    /// <summary>시스템 문서 선택창의 완료만 받는다. URI와 문서 원문을 로그·환경 설정에 남기지 않는다.</summary>
    public sealed class MobileAndroidDocumentExchange : MonoBehaviour, IMobileGameFileExchange
    {
        private TaskCompletionSource<string> pending;
        private string operationId;

        public Task<string> ReadTextAsync() { return Begin("read", null, null, "application/json"); }
        public async Task ShareTextAsync(string fileName, string text, string mimeType)
        {
            if (Encoding.UTF8.GetByteCount(text ?? string.Empty) > MobileGameExchangeValidator.MaximumBytes)
                throw new IOException("자료는 UTF-8 기준 1MB 이하만 저장할 수 있습니다.");
            var result = await Begin("write", Path.GetFileName(fileName), text, mimeType ?? "application/json");
            if (result == null) throw new OperationCanceledException("파일 저장을 취소했습니다.");
        }
        private Task<string> Begin(string kind, string fileName, string text, string mimeType)
        {
            if (pending != null) return Task.FromException<string>(new InvalidOperationException("앞선 파일 선택을 마쳐 주세요."));
            pending = new TaskCompletionSource<string>();
            operationId = Guid.NewGuid().ToString("N");
            var task = pending.Task;
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var documents = new AndroidJavaClass("com.hongdal.mobile.documents.HongdalDocumentsActivity"))
                    documents.CallStatic("open", activity, gameObject.name, operationId, kind, fileName ?? string.Empty,
                        text ?? string.Empty, mimeType ?? "application/json");
            }
            catch (Exception)
            {
                var completion = pending; pending = null; operationId = null;
                completion.TrySetException(new IOException("문서 선택창을 열지 못했습니다. 현재 회차는 유지합니다."));
            }
            return task;
        }
        [UnityEngine.Scripting.Preserve]
        public void OnDocumentResult(string json)
        {
            DocumentResult result;
            try { result = JsonUtility.FromJson<DocumentResult>(json); }
            catch (ArgumentException) { return; }
            if (result == null || pending == null || result.operationId != operationId) return;
            var completion = pending; pending = null; operationId = null;
            if (result.status == "cancelled") completion.TrySetResult(null);
            else if (result.status == "ok") completion.TrySetResult(result.text ?? string.Empty);
            else completion.TrySetException(new IOException(string.IsNullOrWhiteSpace(result.error) ? "문서 처리에 실패했습니다." : result.error));
        }
        private void OnDestroy()
        {
            if (pending == null) return;
            try { using (var documents = new AndroidJavaClass("com.hongdal.mobile.documents.HongdalDocumentsActivity")) documents.CallStatic("abandon", operationId); }
            catch (Exception) { }
            var completion = pending; pending = null; operationId = null;
            completion.TrySetException(new OperationCanceledException("화면이 닫혀 문서 처리를 중단했습니다."));
        }
        [Serializable] private sealed class DocumentResult { public string operationId, status, text, error; }
    }
}
#endif
