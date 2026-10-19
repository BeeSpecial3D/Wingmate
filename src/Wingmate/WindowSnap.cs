using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Wingmate
{
    /// <summary>Keeps a window fully inside the working area of the nearest monitor (multi-monitor, DPI aware:
    /// works in physical pixels via Win32, so no DIP conversion is needed). Window placement only.</summary>
    public static class WindowSnap
    {
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT r, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO mi);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        private const uint MONITOR_DEFAULTTONEAREST = 2, SWP_NOSIZE = 1, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;

        /// <summary>Moves the window (and narrows it if allowed and wider than the screen) so it is fully on screen.</summary>
        public static void Clamp(Window w, bool canNarrow = false)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero || !w.IsVisible || w.WindowState != WindowState.Normal) return;
                if (!GetWindowRect(h, out var r)) return;
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(MonitorFromRect(ref r, MONITOR_DEFAULTTONEAREST), ref mi)) return;
                var wa = mi.rcWork;
                int width = r.R - r.L, height = r.B - r.T;
                if (canNarrow && width > wa.R - wa.L && w.ActualWidth > 0)
                {
                    double dipPerPx = w.ActualWidth / width;
                    w.Width = Math.Max(w.MinWidth, (wa.R - wa.L) * dipPerPx);
                    w.UpdateLayout();
                    if (!GetWindowRect(h, out r)) return;
                    width = r.R - r.L; height = r.B - r.T;
                }
                int x = r.L, y = r.T;
                if (x + width > wa.R) x = wa.R - width;
                if (y + height > wa.B) y = wa.B - height;
                if (x < wa.L) x = wa.L;
                if (y < wa.T) y = wa.T;
                if (x != r.L || y != r.T) SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
            catch (Exception ex) { Core.Log.Write("snap: " + ex.Message); }
        }

        /// <summary>Clamp once the window has been laid out (startup, restore from tray, saved positions).</summary>
        public static void ClampSoon(Window w, Action? after = null) =>
            w.Dispatcher.BeginInvoke(new Action(() => { Clamp(w, true); after?.Invoke(); }), DispatcherPriority.Background);

        /// <summary>Clamps whenever the window is shown.</summary>
        public static void Attach(Window w, Action? after = null)
        {
            w.IsVisibleChanged += (s, e) => { if (w.IsVisible) ClampSoon(w, after); };
        }
    }
}
