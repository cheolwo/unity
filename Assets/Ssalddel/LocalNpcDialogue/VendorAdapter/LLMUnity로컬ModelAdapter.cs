using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UndreamAI.LlamaLib;

namespace Ssalddel.LocalNpcDialogue
{
    /// <summary>고정 LLMUnity 3.0.3의 로컬 provider만 소비. 서버/URL/도구/모델 다운로드 경로 없음.</summary>
    public sealed class LLMUnity로컬ModelAdapter : I로컬대화Model
    {
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private LLMService service;
        private bool closed;
        public bool Enabled { get; }
        public bool IsLocal => true;
        public bool IsModelAvailable => Enabled && !closed && service != null;
        public string AvailabilityCode { get; private set; }
        public string ModelPath { get; private set; } = "";

        public LLMUnity로컬ModelAdapter(bool enabled = false)
        {
            Enabled = enabled;
            AvailabilityCode = enabled ? "ModelMissing" : "Disabled";
        }

        // 선택한 기존 로컬 모델만 명시적으로 연다. 생성자는 네이티브 모듈도 로드하지 않는다.
        public async Task<string> InitializeAsync(string path, CancellationToken token = default)
        {
            if (!Enabled) return "Disabled";
            if (!await gate.WaitAsync(0, token)) return "Busy";
            try
            {
                if (closed) return "Disposed";
                if (service != null) return AvailabilityCode;
                if (string.IsNullOrWhiteSpace(path)) return AvailabilityCode = "ModelMissing";
                if (!IsLocalModelPath(path)) return AvailabilityCode = "RemoteForbidden";
                ModelPath = Path.GetFullPath(path);
                if (!File.Exists(ModelPath)) return AvailabilityCode = "ModelMissing";
                token.ThrowIfCancellationRequested();
                // 네이티브 로드를 메인 스레드에서 기다리지 않으며 취소 후에도 완료까지 점유한다.
                var loaded = await Task.Run(() => new LLMService(ModelPath, numSlots: 1,
                    numThreads: 2, numGpuLayers: 0, contextSize: 4096, batchSize: 128));
                if (token.IsCancellationRequested)
                {
                    loaded.Dispose();
                    return AvailabilityCode = "Cancelled";
                }
                service = loaded;
                loaded.SetCompletionParameters(new JObject { ["n_predict"] = 128, ["temperature"] = 0.2,
                    ["cache_prompt"] = false, ["ignore_eos"] = false });
                return AvailabilityCode = "Ready";
            }
            catch (OperationCanceledException) { return AvailabilityCode = "Cancelled"; }
            catch (Exception) { service?.Dispose(); service = null; return AvailabilityCode = "Error"; }
            finally { gate.Release(); }
        }

        public async Task<string> GenerateAsync(string prompt, CancellationToken token)
        {
            if (!IsModelAvailable) throw new InvalidOperationException(AvailabilityCode);
            if (prompt == null || prompt.Length > 12000) throw new ArgumentException("InputLimit");
            if (!await gate.WaitAsync(0, token)) throw new InvalidOperationException("Busy");
            try
            {
                if (!IsModelAvailable) throw new InvalidOperationException("Disposed");
                token.ThrowIfCancellationRequested();
                var local = service;
                using (token.Register(() => { try { local.Cancel(0); } catch (Exception) { } }))
                {
                    var result = await local.CompletionAsync(prompt, idSlot: 0);
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(result)) throw new InvalidOperationException("EmptyResponse");
                    if (result.Length > 512) throw new InvalidOperationException("OutputLimit");
                    return result;
                }
            }
            finally { gate.Release(); }
        }

        public async Task ShutdownAsync()
        {
            await gate.WaitAsync();
            try
            {
                closed = true;
                service?.Dispose();
                service = null;
                AvailabilityCode = "Disposed";
            }
            finally { gate.Release(); }
        }

        public static bool IsLocalModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.StartsWith("\\\\") ||
                path.StartsWith("//") || path.Contains("://") ||
                !string.Equals(Path.GetExtension(path), ".gguf", StringComparison.OrdinalIgnoreCase)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                var drive = new DriveInfo(Path.GetPathRoot(full));
                if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) return false;
                // 로컬 경로 모양의 네트워크 junction/symlink도 허용하지 않는다.
                for (string part = full; !string.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                    if ((File.Exists(part) || Directory.Exists(part)) &&
                        (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) return false;
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
