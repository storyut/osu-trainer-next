using System.Runtime.InteropServices;
using Avalonia;

namespace osu_trainer_avalonia.Interop
{
    /// <summary>
    /// Reads the OS cursor position, used to anchor the tray's quick-settings popup near the
    /// tray icon the user just clicked (Avalonia's TrayIcon.Clicked carries no position).
    /// </summary>
    public static class CursorInterop
    {
        public static PixelPoint GetCursorScreenPosition()
        {
            if (GetCursorPos(out var point))
                return new PixelPoint(point.X, point.Y);
            return PixelPoint.Origin;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}
