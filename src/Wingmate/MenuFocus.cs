using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Right-click menus on the no-activate overlays: the window takes focus only while its menu is open, so a click
    /// anywhere else (game, desktop) or Escape closes the menu. Afterwards focus goes back to whatever had it before
    /// (normally the game). No hooks: just activation, which Windows allows because the user just clicked our window.</summary>
    public static class MenuFocus
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);

        /// <param name="keepActive">True while the window needs focus for its own reasons (an inline edit box).</param>
        public static void Attach(Window w, Func<bool> keepActive)
        {
            IntPtr prev = IntPtr.Zero;
            bool open = false;
            w.AddHandler(FrameworkElement.ContextMenuOpeningEvent, new ContextMenuEventHandler((s, e) =>
            {
                var h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero || e.Handled) return;
                var fg = GetForegroundWindow();
                if (!open) prev = fg == h ? IntPtr.Zero : fg;
                open = true;
                Native.SetNoActivate(h, false);
                w.Activate();
            }));
            w.AddHandler(FrameworkElement.ContextMenuClosingEvent, new ContextMenuEventHandler((s, e) =>
            {
                // let the clicked item run first (it may open a dialog or start an edit)
                w.Dispatcher.BeginInvoke(new Action(() =>
                {
                    open = false;
                    var h = new WindowInteropHelper(w).Handle;
                    if (h == IntPtr.Zero || keepActive()) return;
                    Native.SetNoActivate(h, true);
                    if (GetForegroundWindow() == h && prev != IntPtr.Zero && IsWindow(prev)) SetForegroundWindow(prev);
                    prev = IntPtr.Zero;
                }), DispatcherPriority.Background);
            }), true);
        }
    }
}
