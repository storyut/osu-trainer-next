using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;

namespace osu_trainer_avalonia.Interop
{
    /// <summary>
    /// System-wide Ctrl+Alt+Up/Down rate-nudge hotkeys. <c>RegisterHotKey</c> delivers
    /// WM_HOTKEY to whichever window handle is passed in, and that handle's owning thread
    /// must be pumping Win32 messages to receive it — Avalonia's own dispatcher doesn't do
    /// this, so a dedicated background thread creates a hidden message-only window and runs
    /// its own GetMessage/DispatchMessage loop. See .fable/research/windows-global-hotkey.md.
    /// </summary>
    public sealed class GlobalHotKey : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int WM_QUIT = 0x0012;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_UP = 0x26;
        private const uint VK_DOWN = 0x28;
        private const int HotkeyIdIncrease = 1;
        private const int HotkeyIdDecrease = 2;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        /// <summary>Raised on the Avalonia UI thread. Argument is true for "increase".</summary>
        public event Action<bool>? RateNudgeRequested;

        private Thread? pumpThread;
        private uint pumpThreadId;
        // Kept as a field: a delegate passed to native code as a function pointer must stay
        // rooted for as long as the native side can call back into it, or the GC can collect
        // it out from under the callback.
        private WndProc? wndProc;

        public void Start()
        {
            pumpThread = new Thread(PumpMessages) { IsBackground = true, Name = "GlobalHotKeyPump" };
            pumpThread.SetApartmentState(ApartmentState.STA);
            pumpThread.Start();
        }

        public void Dispose()
        {
            if (pumpThreadId != 0)
                PostThreadMessage(pumpThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            pumpThread?.Join(TimeSpan.FromSeconds(1));
        }

        private void PumpMessages()
        {
            pumpThreadId = GetCurrentThreadId();
            wndProc = DefWindowProc;

            var wndClass = new WNDCLASS
            {
                lpfnWndProc = wndProc,
                lpszClassName = "OsuTrainerNextHotkeyWindow" + Guid.NewGuid().ToString("N")
            };
            RegisterClass(ref wndClass);

            IntPtr hwnd = CreateWindowEx(0, wndClass.lpszClassName, "", 0, 0, 0, 0, 0,
                HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
            {
                Trace.WriteLine("GlobalHotKey: failed to create the message-only window; hotkeys are unavailable this session.");
                return;
            }

            if (!RegisterHotKey(hwnd, HotkeyIdIncrease, MOD_CONTROL | MOD_ALT, VK_UP))
                Trace.WriteLine("GlobalHotKey: failed to register Ctrl+Alt+Up (likely claimed by another app).");
            if (!RegisterHotKey(hwnd, HotkeyIdDecrease, MOD_CONTROL | MOD_ALT, VK_DOWN))
                Trace.WriteLine("GlobalHotKey: failed to register Ctrl+Alt+Down (likely claimed by another app).");

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == WM_HOTKEY)
                {
                    bool increase = msg.wParam.ToInt32() == HotkeyIdIncrease;
                    Dispatcher.UIThread.Post(() => RateNudgeRequested?.Invoke(increase));
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            UnregisterHotKey(hwnd, HotkeyIdIncrease);
            UnregisterHotKey(hwnd, HotkeyIdDecrease);
            DestroyWindow(hwnd);
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }
}
