using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Wingmate
{
    /// <summary>One optional global hotkey via RegisterHotKey (no hooks). Messages arrive on a hidden message-only window.</summary>
    public sealed class Hotkey : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h, int id);
        private const int WM_HOTKEY = 0x0312, Id = 0x5747;
        public const int MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

        private readonly HwndSource _src;
        private bool _registered;
        public event Action? Pressed;

        public Hotkey()
        {
            _src = new HwndSource(new HwndSourceParameters("WingmateHotkey") { ParentWindow = new IntPtr(-3), WindowStyle = 0 }); // HWND_MESSAGE
            _src.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (msg == WM_HOTKEY && w.ToInt32() == Id) { handled = true; Pressed?.Invoke(); }
                return IntPtr.Zero;
            });
        }

        /// <summary>Registers (or clears with enabled=false). Returns false if Windows refused (combo in use).</summary>
        public bool Set(bool enabled, int mods, int vk)
        {
            Clear();
            if (!enabled || vk == 0 || mods == 0) return true;
            _registered = RegisterHotKey(_src.Handle, Id, (uint)(mods | MOD_NOREPEAT), (uint)vk);
            return _registered;
        }

        public void Clear()
        {
            if (_registered) UnregisterHotKey(_src.Handle, Id);
            _registered = false;
        }

        public void Dispose() { Clear(); _src.Dispose(); }

        /// <summary>Text like "Ctrl+Alt+K".</summary>
        public static string Describe(int mods, int vk)
        {
            var s = "";
            if ((mods & MOD_CONTROL) != 0) s += "Ctrl+";
            if ((mods & MOD_ALT) != 0) s += "Alt+";
            if ((mods & MOD_SHIFT) != 0) s += "Shift+";
            if ((mods & MOD_WIN) != 0) s += "Win+";
            var key = KeyInterop.KeyFromVirtualKey(vk);
            string k = key switch
            {
                >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => "Num" + (int)(key - Key.NumPad0),
                _ => key.ToString(),
            };
            return s + k;
        }

        /// <summary>His older overlay uses these; never take them.</summary>
        public static bool IsReserved(int mods, int vk) =>
            mods == (MOD_CONTROL | MOD_ALT) && (vk == 0x4D || vk == 0x59); // Ctrl+Alt+M, Ctrl+Alt+Y
    }
}
