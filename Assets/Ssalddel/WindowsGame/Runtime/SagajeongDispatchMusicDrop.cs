using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Ssalddel.WindowsGame
{
    // 게임 창에 파일을 끌어다 놓기 (2026-11-14, 사용자: 설정의 음악에 wav · mp3를 넣으면 → 「파일 창 + 끌어다 놓기」).
    //  - Windows 실행판에서만 켠다(편집기에서는 꺼 둔다). 이 게임의 창(UnityWndClass)에 끌어다 놓기를 받겠다고 알리고,
    //    그 창의 메시지 길목(WH_GETMESSAGE)에서 WM_DROPFILES만 집어 파일 경로를 모은다. 경로는 다음 프레임에 SyncMusic이 가져간다.
    //  - 다른 메시지는 건드리지 않는다. 게임이 끝날 때 길목을 걷는다.
    //  - 방향 촬영은 탐색기에서 끌어다 놓을 때와 같은 메시지(WM_DROPFILES)와 ESC 키 메시지를 이 창에 보내 본다(PostTestDrop · PostTestEscape).
    internal static class 음악끌어놓기
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SetWindowsHookExW(int idHook, HookProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr data);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr window, StringBuilder name, int capacity);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string name);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GlobalFree(IntPtr memory);

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern void DragAcceptFiles(IntPtr window, bool accept);

        [DllImport("shell32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFileW(IntPtr drop, uint index, StringBuilder file, uint capacity);

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern void DragFinish(IntPtr drop);

        private const int GetMessageHook = 3;             // WH_GETMESSAGE
        private const int Remove = 1;                     // PM_REMOVE: 메시지를 꺼낼 때 한 번만 본다
        private const uint DropFiles = 0x0233;            // WM_DROPFILES
        private const uint KeyDown = 0x0100, KeyUp = 0x0101, Escape = 0x1B;
        private const uint MovableZeroed = 0x0042;        // GHND
        private const int DropHeader = 20;                // DROPFILES: pFiles · pt · fNC · fWide

        private static HookProc callback;                 // 길목이 부르는 동안 살아 있어야 한다
        private static IntPtr hook;
        private static IntPtr window;
        private static readonly List<string> dropped = new();
        private static bool tried;

        internal static bool Ready => hook != IntPtr.Zero && window != IntPtr.Zero;

        internal static void Install()
        {
            if (tried) return;
            tried = true;
            try
            {
                var thread = GetCurrentThreadId();
                EnumThreadWindows(thread, (candidate, _) =>
                {
                    var name = new StringBuilder(64);
                    GetClassNameW(candidate, name, name.Capacity);
                    if (name.ToString() != "UnityWndClass") return true;
                    window = candidate;
                    return false;
                }, IntPtr.Zero);
                if (window == IntPtr.Zero)
                {
                    Debug.LogWarning("Hongdal drop: game window not found");
                    return;
                }
                callback = Callback;
                hook = SetWindowsHookExW(GetMessageHook, callback, GetModuleHandleW(null), thread);
                if (hook == IntPtr.Zero)
                {
                    Debug.LogWarning("Hongdal drop: hook failed " + Marshal.GetLastWin32Error());
                    return;
                }
                DragAcceptFiles(window, true);
                Application.quitting += Uninstall;
                Debug.Log("Hongdal drop: ready");
            }
            catch (Exception exception)
            {
                hook = IntPtr.Zero;
                Debug.LogWarning("Hongdal drop: " + exception.Message);
            }
        }

        private static void Uninstall()
        {
            if (window != IntPtr.Zero) DragAcceptFiles(window, false);
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }

        // 메시지 길목. 창 메시지를 꺼낼 때(PM_REMOVE)의 WM_DROPFILES만 본다. 나머지는 그대로 넘긴다.
        private static IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0 && wParam.ToInt64() == Remove && lParam != IntPtr.Zero &&
                    (uint)Marshal.ReadInt32(lParam, IntPtr.Size) == DropFiles)
                {
                    var drop = Marshal.ReadIntPtr(lParam, IntPtr.Size * 2);
                    var count = DragQueryFileW(drop, 0xFFFFFFFF, null, 0);
                    lock (dropped)
                    {
                        for (uint index = 0; index < count && index < 32; index++)
                        {
                            var length = DragQueryFileW(drop, index, null, 0);
                            var name = new StringBuilder((int)length + 1);
                            DragQueryFileW(drop, index, name, (uint)name.Capacity);
                            dropped.Add(name.ToString());
                        }
                    }
                    DragFinish(drop);
                    Marshal.WriteInt32(lParam, IntPtr.Size, 0);          // 창에는 빈 메시지(WM_NULL)로 넘긴다
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hongdal drop: " + exception.Message);
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        // 모인 파일 경로(없으면 null).
        internal static List<string> TakeDropped()
        {
            lock (dropped)
            {
                if (dropped.Count == 0) return null;
                var taken = new List<string>(dropped);
                dropped.Clear();
                return taken;
            }
        }

        internal static bool PostTestDrop(string path)
        {
            if (!Ready) return false;
            var text = (path + "\0\0").ToCharArray();
            var memory = GlobalAlloc(MovableZeroed, (UIntPtr)(uint)(DropHeader + text.Length * 2));
            if (memory == IntPtr.Zero) return false;
            var at = GlobalLock(memory);
            Marshal.WriteInt32(at, 0, DropHeader);           // pFiles
            Marshal.WriteInt32(at, 16, 1);                   // fWide
            Marshal.Copy(text, 0, IntPtr.Add(at, DropHeader), text.Length);
            GlobalUnlock(memory);
            if (PostMessageW(window, DropFiles, memory, IntPtr.Zero)) return true;
            GlobalFree(memory);
            return false;
        }

        internal static bool PostTestEscape()
        {
            if (window == IntPtr.Zero) return false;
            return PostMessageW(window, KeyDown, (IntPtr)Escape, (IntPtr)0x00010001) &&
                PostMessageW(window, KeyUp, (IntPtr)Escape, unchecked((IntPtr)(int)0xC0010001));
        }
#else
        internal static bool Ready => false;
        internal static void Install() { }
        internal static List<string> TakeDropped() => null;
        internal static bool PostTestDrop(string path) => false;
        internal static bool PostTestEscape() => false;
#endif
    }
}
