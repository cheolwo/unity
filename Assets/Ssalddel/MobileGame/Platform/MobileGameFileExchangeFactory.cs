using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Ssalddel.MobileGame
{
    public static class MobileGameFileExchangeFactory
    {
        public static IMobileGameFileExchange Create(GameObject host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
#if UNITY_ANDROID && !UNITY_EDITOR
            var receiver = new GameObject("HongdalDocuments-" + Guid.NewGuid().ToString("N"));
            receiver.transform.SetParent(host.transform, false);
            return receiver.AddComponent<MobileAndroidDocumentExchange>();
#else
            return new DesktopDocuments();
#endif
        }

#if !UNITY_ANDROID || UNITY_EDITOR
        private sealed class DesktopDocuments : IMobileGameFileExchange
        {
            private bool busy;
            public Task<string> ReadTextAsync()
            {
                if (busy) return Task.FromException<string>(new InvalidOperationException("앞선 파일 선택을 마쳐 주세요."));
                busy = true;
                try
                {
                    var path = Pick(false, null);
                    if (string.IsNullOrEmpty(path)) return Task.FromResult<string>(null);
                    var info = new FileInfo(path);
                    if (info.Length > MobileGameExchangeValidator.MaximumBytes) throw new IOException("자료는 UTF-8 기준 1MB 이하만 열 수 있습니다.");
                    using (var stream = File.OpenRead(path))
                    using (var memory = new MemoryStream())
                    {
                        var buffer = new byte[8192]; int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (memory.Length + read > MobileGameExchangeValidator.MaximumBytes) throw new IOException("선택한 자료가 크기 제한을 넘었습니다.");
                            memory.Write(buffer, 0, read);
                        }
                        var text = new UTF8Encoding(false, true).GetString(memory.ToArray());
                        return Task.FromResult(text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text);
                    }
                }
                catch (Exception error) { return Task.FromException<string>(error); }
                finally { busy = false; }
            }

            public Task ShareTextAsync(string fileName, string text, string mimeType)
            {
                if (busy) return Task.FromException(new InvalidOperationException("앞선 파일 선택을 마쳐 주세요."));
                busy = true;
                try
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(text ?? string.Empty);
                    if (bytes.Length > MobileGameExchangeValidator.MaximumBytes) throw new IOException("자료는 UTF-8 기준 1MB 이하만 저장할 수 있습니다.");
                    var path = Pick(true, Path.GetFileName(fileName));
                    if (string.IsNullOrEmpty(path)) throw new OperationCanceledException("파일 저장을 취소했습니다.");
                    // 선택창에서 사용자가 확인한 파일 한 개만 기록한다. 기존 게임 저장 자료는 변경하지 않는다.
                    File.WriteAllBytes(path, bytes);
                    return Task.CompletedTask;
                }
                catch (Exception error) { return Task.FromException(error); }
                finally { busy = false; }
            }

            private static string Pick(bool save, string name)
            {
#if UNITY_EDITOR
                return save ? UnityEditor.EditorUtility.SaveFilePanel("가상 운영 자료 저장", string.Empty, name ?? "hongdal-game.json", "json")
                    : UnityEditor.EditorUtility.OpenFilePanel("가상 운영 자료 불러오기", string.Empty, "json");
#elif UNITY_STANDALONE_WIN
                var buffer = new StringBuilder(32768);
                if (save && !string.IsNullOrEmpty(name)) buffer.Append(name);
                var choice = new OpenFileName
                {
                    size = Marshal.SizeOf(typeof(OpenFileName)), filter = "JSON 자료\0*.json\0모든 파일\0*.*\0\0",
                    file = buffer, maxFile = buffer.Capacity, title = save ? "가상 운영 자료 저장" : "가상 운영 자료 불러오기",
                    defaultExtension = "json", flags = 0x00080000 | 0x00000008 | 0x00000800 | (save ? 0x00000002 : 0x00001000)
                };
                var accepted = save ? GetSaveFileName(ref choice) : GetOpenFileName(ref choice);
                if (accepted) return buffer.ToString();
                if (CommDlgExtendedError() != 0) throw new IOException("파일 선택창을 열지 못했습니다.");
                return null;
#else
                throw new PlatformNotSupportedException("이 플랫폼의 파일 선택 연결은 아직 준비되지 않았습니다.");
#endif
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct OpenFileName
            {
                public int size; public IntPtr owner, instance; public string filter, customFilter;
                public int maxCustomFilter, filterIndex; public StringBuilder file; public int maxFile;
                public string fileTitle; public int maxFileTitle; public string initialDirectory, title;
                public int flags; public short fileOffset, extensionOffset; public string defaultExtension;
                public IntPtr customData, hook; public string templateName; public IntPtr reserved; public int reservedSize, flagsEx;
            }
            [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
            [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileName(ref OpenFileName value);
            [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetSaveFileNameW")]
            [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSaveFileName(ref OpenFileName value);
            [DllImport("comdlg32.dll")] private static extern int CommDlgExtendedError();
#endif
        }
#endif
    }
}
