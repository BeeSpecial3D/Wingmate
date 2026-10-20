using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>The padlock of the alerts / checklist window, hosted in its own tiny window so it stays clickable while the
    /// main window is locked (click-through). Only this 18x18 square takes clicks; the rest of a locked window still passes
    /// every click to the game. No hooks: a separate owned tool window (no-activate, so it never takes focus from the game,
    /// not in Alt+Tab or the taskbar) that sits exactly over an invisible placeholder in the main window's title row and
    /// follows its position, size, visibility and opacity. Colours are the app-wide theme resources, so it follows themes.</summary>
    public sealed class LockOverlay
    {
        private readonly Window _owner;
        private readonly FrameworkElement _slot;
        private readonly Func<bool> _locked;
        private readonly Action _toggle;
        private readonly string _name;
        private Window? _win;
        private Button? _btn;
        private (int X, int Y) _last = (int.MinValue, int.MinValue);

        /// <param name="slot">Invisible placeholder in the owner's layout that marks where the padlock goes.</param>
        public LockOverlay(Window owner, FrameworkElement slot, Func<bool> locked, Action toggle, string windowName)
        {
            _owner = owner; _slot = slot; _locked = locked; _toggle = toggle; _name = windowName;
            owner.IsVisibleChanged += (s, e) => owner.Dispatcher.BeginInvoke(new Action(Sync), System.Windows.Threading.DispatcherPriority.Loaded);
            owner.LocationChanged += (s, e) => Place();
            owner.SizeChanged += (s, e) => Place();
            slot.LayoutUpdated += (s, e) => Place(); // title/badge changes, width drags, text size
            DependencyPropertyDescriptor.FromProperty(UIElement.OpacityProperty, typeof(Window)).AddValueChanged(owner, (s, e) => { if (_win != null) _win.Opacity = owner.Opacity; });
            owner.Closed += (s, e) => { try { _win?.Close(); } catch { } };
        }

        /// <summary>Shows/hides with the owner (the owner must have been shown once before it can own a window).</summary>
        public void Sync()
        {
            if (_owner.IsVisible)
            {
                if (_win == null) Create();
                if (_win == null) return;
                _win.Opacity = _owner.Opacity;
                if (!_win.IsVisible) _win.Show();
                _last = (int.MinValue, int.MinValue);
                _owner.Dispatcher.BeginInvoke(new Action(Place), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else _win?.Hide();
            Refresh();
        }

        /// <summary>Padlock drawing and tooltip from the current lock state.</summary>
        public void Refresh()
        {
            if (_btn == null) return;
            bool on = _locked();
            _btn.Tag = on ? "locked" : "unlocked";
            _btn.ToolTip = App.LockTip(on, _name);
        }

        private void Create()
        {
            try
            {
                _btn = new Button { Style = (Style)Application.Current.FindResource("LockButton") };
                _btn.Click += (s, e) => { _toggle(); Refresh(); };
                _win = new Window
                {
                    Owner = _owner, Content = _btn, Width = 18, Height = 18,
                    WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false, ShowActivated = false, Topmost = true, Focusable = false, UseLayoutRounding = true,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000, Title = "Wingmate lock",
                };
                _win.SourceInitialized += (s, e) => { var h = new WindowInteropHelper(_win).Handle; if (h != IntPtr.Zero) Native.OverlayStyles(h); };
                Refresh();
            }
            catch (Exception ex) { Log.Write("lock overlay: " + ex.Message); _win = null; _btn = null; }
        }

        /// <summary>Puts the padlock window exactly over the placeholder (physical pixels, so any DPI works).</summary>
        private void Place()
        {
            if (_win == null || !_win.IsVisible || !_owner.IsVisible || !_slot.IsLoaded) return;
            try
            {
                if (PresentationSource.FromVisual(_slot) == null) return;
                var p = _slot.PointToScreen(new Point(0, 0));
                var pos = ((int)Math.Round(p.X), (int)Math.Round(p.Y));
                if (pos == _last) return;
                var h = new WindowInteropHelper(_win).Handle;
                if (h == IntPtr.Zero) return;
                _last = pos;
                Native.MoveTo(h, pos.Item1, pos.Item2);
            }
            catch (Exception ex) { Log.Write("lock overlay place: " + ex.Message); }
        }
    }
}
