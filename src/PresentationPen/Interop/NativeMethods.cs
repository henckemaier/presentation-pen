using System;
using System.Runtime.InteropServices;

namespace PresentationPen.Interop
{
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint WDA_NONE = 0x00;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public static readonly IntPtr HWND_MESSAGE = new(-3);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public const uint SRCCOPY = 0x00CC0020;
        public const uint CAPTUREBLT = 0x40000000;

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height,
            IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>Escala de DPI (1.0 = 100%) do monitor que contém o centro do retângulo.</summary>
        public static double GetDpiScale(System.Drawing.Rectangle area)
        {
            var center = new POINT { X = area.Left + area.Width / 2, Y = area.Top + area.Height / 2 };
            IntPtr monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
            return GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33; // Windows 11
        public const int DWMWCP_DONOTROUND = 1;
        public const int DWMWA_BORDER_COLOR = 34;              // Windows 11
        public const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref uint value, int size);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
