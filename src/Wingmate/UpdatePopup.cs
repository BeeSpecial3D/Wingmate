using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Small themed "Wingmate X is available" pop-up with Update now / Later. Topmost but no-activate (a tool window
    /// that never takes focus, so a game keeps keyboard focus); centred on the launcher when it is open, else on the primary
    /// screen. "Update now" downloads with progress, then applies and restarts; errors show a friendly message and Retry.
    /// In preview mode nothing is downloaded or changed: it only plays the states.</summary>
    public sealed class UpdatePopup : Window
    {
        private readonly Func<Action<int>, CancellationToken, Task>? _download;
        private readonly Func<bool>? _apply;
        private readonly bool _preview;
        private readonly TextBlock _status;
        private readonly Border _track, _bar;
        private readonly Button _now, _later;
        private CancellationTokenSource? _cts;
        private bool _busy;

        /// <param name="download">Downloads the update (progress 0-100); throws on failure. Null in preview.</param>
        /// <param name="apply">Saves everything, applies the update and restarts; false if it could not start. Null in preview.</param>
        public UpdatePopup(Window? anchor, string newVersion, string currentVersion,
            Func<Action<int>, CancellationToken, Task>? download, Func<bool>? apply, bool preview)
        {
            _download = download; _apply = apply; _preview = preview;
            Title = "Wingmate update"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; Width = double.NaN; MaxWidth = 360;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; Foreground = Th.Br("text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            if (anchor != null && anchor.IsVisible && anchor.WindowState != WindowState.Minimized) { Owner = anchor; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen; // primary screen

            var root = new DockPanel { Width = 300 };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = (Style)Application.Current.FindResource("CloseButton"), Focusable = false, ToolTip = "Later",
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, -2, -6, 0) };
            close.Click += (s, e) => Later();
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = UpdatePrompt.Title(newVersion), FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = Th.Br("title"), TextTrimming = TextTrimming.CharacterEllipsis });
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);

            var sep = new Border { Height = 1, Background = Th.Br("divider"), Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(sep, Dock.Top); root.Children.Add(sep);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var btnStyle = (Style)Application.Current.FindResource("DialogButton");
            _now = new Button { Content = "Update now", Style = btnStyle, Tag = "primary", Focusable = false, Margin = new Thickness(0, 0, 8, 0) };
            _now.Click += async (s, e) => await UpdateNow();
            _later = new Button { Content = "Later", Style = btnStyle, Focusable = false };
            _later.Click += (s, e) => Later();
            buttons.Children.Add(_now); buttons.Children.Add(_later);
            DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);

            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = UpdatePrompt.Subtitle(currentVersion), TextWrapping = TextWrapping.Wrap, Foreground = Th.Br("text"), LineHeight = 17 });
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Th.Br("muted"), LineHeight = 17, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            body.Children.Add(_status);
            _bar = new Border { Background = Th.Br("link"), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            _track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Th.Br("divider"), Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed, Child = _bar };
            body.Children.Add(_track);
            root.Children.Add(body);

            Content = new Border { CornerRadius = new CornerRadius(10), Background = Th.Br("menuBg"), BorderBrush = Th.Br("panelBorder"),
                BorderThickness = new Thickness(1), Padding = new Thickness(14, 8, 14, 12), Child = root };

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero) Native.OverlayStyles(h); // tool window + no-activate: clicks work, focus stays with the game
            };
            Loaded += (s, e) => { var h = new WindowInteropHelper(this).Handle; if (h != IntPtr.Zero) Native.KeepTopmost(h); WindowSnap.Clamp(this); };
            Closed += (s, e) => { try { _cts?.Cancel(); } catch { } };
        }

        /// <summary>Starts "Update now" right away (launcher link / tray item).</summary>
        public void StartNow() { if (!_busy) _ = UpdateNow(); }

        private void Later()
        {
            if (_busy && _cts != null) { _cts.Cancel(); return; } // "Cancel" while downloading: back to the offer, then the user can close
            Close();
        }

        private void Status(string text, bool error = false)
        {
            _status.Text = text;
            _status.Foreground = error ? Th.Br("error") : Th.Br("muted");
            _status.Visibility = Visibility.Visible;
        }

        private void Progress(int p)
        {
            _track.Visibility = Visibility.Visible;
            double w = _track.ActualWidth > 0 ? _track.ActualWidth : 300;
            _bar.Width = w * Math.Clamp(p, 0, 100) / 100.0;
            Status(UpdatePrompt.Downloading(p));
        }

        private void Offer(string? error)
        {
            _busy = false;
            _track.Visibility = Visibility.Collapsed;
            _now.IsEnabled = true; _now.Content = error == null ? "Update now" : "Retry";
            _later.IsEnabled = true; _later.Content = "Later";
            if (error == null) _status.Visibility = Visibility.Collapsed; else Status(error, true);
        }

        private async Task UpdateNow()
        {
            if (_busy) return;
            _busy = true;
            _now.IsEnabled = false; _now.Content = "Downloading\u2026";
            _later.Content = "Cancel";
            _cts = new CancellationTokenSource();
            Progress(0);
            if (_preview)
            {
                try
                {
                    for (int p = 0; p <= 100; p += 4) { Progress(p); await Task.Delay(100, _cts.Token); } // ~2.5 s, nothing downloaded
                }
                catch (TaskCanceledException) { Close(); return; }
                Done(UpdatePrompt.PreviewDone);
                await Task.Delay(2200);
                Close();
                return;
            }
            try
            {
                if (_download != null) await _download(Progress, _cts.Token);
            }
            catch (OperationCanceledException) { Offer(null); return; }
            catch (Exception) { Offer(UpdatePrompt.Failed); return; }
            Done(UpdatePrompt.Restarting);
            await Task.Delay(600); // let the message paint
            if (_apply == null || !_apply()) Offer("Couldn't start the update. Please try again, or it installs the next time Wingmate starts. Your lists and settings are untouched.");
        }

        private void Done(string text)
        {
            _bar.Width = _track.ActualWidth > 0 ? _track.ActualWidth : 300;
            _now.IsEnabled = false; _later.IsEnabled = false;
            Status(text);
            _status.Foreground = Th.Br("text");
        }
    }
}
