using System;
using System.Runtime.InteropServices;

namespace Wingmate.Core
{
    /// <summary>Window styles only. No input hooks, no game interaction.</summary>
    public static class Native
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);

        public static void OverlayStyles(IntPtr h) => SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

        public static void SetClickThrough(IntPtr h, bool on)
        {
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            ex = on ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
            SetWindowLong(h, GWL_EXSTYLE, ex);
        }

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);

        /// <summary>Mouse position in physical screen pixels (a plain read, no hook).</summary>
        public static bool TryGetCursorPos(out int x, out int y)
        {
            bool ok = GetCursorPos(out var p);
            x = p.X; y = p.Y;
            return ok;
        }

        /// <summary>Turns only the click-through bit on or off (the window stays layered); used while a locked window
        /// lets the mouse reach its list and checkboxes.</summary>
        public static void SetTransparentBit(IntPtr h, bool on)
        {
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            int nx = on ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
            if (nx != ex) SetWindowLong(h, GWL_EXSTYLE, nx);
        }

        /// <summary>Temporarily allow the window to take focus (for typing in the checklist), then turn it back off.</summary>
        public static void SetNoActivate(IntPtr h, bool on)
        {
            int ex = GetWindowLong(h, GWL_EXSTYLE);
            SetWindowLong(h, GWL_EXSTYLE, on ? ex | WS_EX_NOACTIVATE : ex & ~WS_EX_NOACTIVATE);
        }

        /// <summary>Moves a window to a screen position in physical pixels without activating it or changing its z-order.</summary>
        public static void MoveTo(IntPtr h, int x, int y) => SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        private const uint SWP_NOZORDER = 4;

        public static void KeepTopmost(IntPtr h) => SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
