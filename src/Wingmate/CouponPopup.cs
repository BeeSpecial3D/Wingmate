using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Small themed "Free code" pop-up: each code with a Copy button, what it gives, where to type it in game and
    /// when it expires (in the user's own time). Opens in the centre of the screen. Topmost but no-activate like the update pop-up (focus stays with the game).
    /// Shown on its own once per new code; Settings opens it again with every code that still works.</summary>
    public sealed class CouponPopup : Window
    {
        private readonly StackPanel _list = new();
        private readonly Window? _monitorOf;
        private bool _placed;
        public IReadOnlyList<string> ShownIds { get; private set; } = Array.Empty<string>();

        public void ApplyFontScale()
        {
            MaxWidth = Math.Round(380 * FontScaler.Scale);
            FontScaler.Apply(this);
        }

        public CouponPopup(Window? anchor, IReadOnlyList<CouponCode> codes, bool fromSettings)
        {
            Title = "Wingmate - Free codes"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; Width = double.NaN; MaxWidth = 380;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; this.Ref(ForegroundProperty, "text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            // always opens in the centre of the screen (the monitor Wingmate is on, else the primary one); see CenterOnScreen below
            if (anchor != null && anchor.IsVisible && anchor.WindowState != WindowState.Minimized) Owner = anchor; // stays above it
            WindowStartupLocation = WindowStartupLocation.Manual;
            _monitorOf = anchor;

            var root = FontScaler.Grow(new DockPanel { Width = 320 });
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = Res<Style>("CloseButton"), Focusable = false, ToolTip = "Close",
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, -2, -6, 0) };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            string title = codes.Count == 0 ? "Free codes" : codes.Count == 1 ? (fromSettings ? "Free code" : "New free code") : (fromSettings ? "Free codes" : "New free codes");
            head.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }
                .Ref(TextBlock.ForegroundProperty, "title"));
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
            var sep = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8) }.Ref(Border.BackgroundProperty, "divider");
            DockPanel.SetDock(sep, Dock.Top); root.Children.Add(sep);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "Got it", Style = Res<Style>("DialogButton"), Tag = "primary", Focusable = false };
            ok.Click += (s, e) => Close();
            buttons.Children.Add(ok);
            DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);

            var body = new StackPanel();
            if (codes.Count == 0)
            {
                body.Children.Add(Text("No free codes right now. When a new one comes out, Wingmate shows it here once.", "text"));
            }
            else
            {
                body.Children.Add(Text(codes.Count == 1 ? "Free gift for Aion 2. Copy the code and type it in game:" : "Free gifts for Aion 2. Copy a code and type it in game:", "text"));
                body.Children.Add(Text(Coupons.RedeemWhere, "muted", top: 2, size: 11));
                foreach (var c in codes) _list.Children.Add(Card(c));
                body.Children.Add(_list);
            }
            root.Children.Add(body);
            ShownIds = codes.Select(c => c.Id).ToList();

            Content = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(14, 8, 14, 12), Child = root }
                .Ref(Border.BackgroundProperty, "menuBg").Ref(Border.BorderBrushProperty, "panelBorder");

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero) Native.OverlayStyles(h); // tool window + no-activate: clicks work, focus stays with the game
            };
            ApplyFontScale();
            Loaded += (s, e) => { var h = new WindowInteropHelper(this).Handle; if (h != IntPtr.Zero) Native.KeepTopmost(h); WindowSnap.CenterOnScreen(this, _monitorOf); };
            // the size is final once the content has rendered (text size setting applied): centre again then, once
            ContentRendered += (s, e) => { if (_placed) return; _placed = true; WindowSnap.CenterOnScreen(this, _monitorOf); WindowSnap.Clamp(this); };
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);

        private static TextBlock Text(string text, string token, double top = 0, double size = 12, FontWeight? weight = null) =>
            new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 17, FontSize = size, FontWeight = weight ?? FontWeights.Normal,
                Margin = new Thickness(0, top, 0, 0) }.Ref(TextBlock.ForegroundProperty, token);

        /// <summary>One code: the code and Copy, what it gives, the note and the expiry time.</summary>
        private FrameworkElement Card(CouponCode c)
        {
            var card = new StackPanel();
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var copy = new Button { Content = "Copy", Style = Res<Style>("DialogButton"), Focusable = false, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Copy the code, then paste it in game with Ctrl+V" };
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) => { timer.Stop(); copy.Content = "Copy"; };
            copy.Click += (s, e) =>
            {
                copy.Content = TryCopy(c.Code) ? "Copied" : "Couldn't copy";
                timer.Stop(); timer.Start();
            };
            DockPanel.SetDock(copy, Dock.Right);
            row.Children.Add(copy);
            row.Children.Add(new TextBlock { Text = c.Code, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 8, 0) }.Ref(TextBlock.ForegroundProperty, "link"));
            card.Children.Add(row);
            if (c.Rewards.Count > 0)
            {
                card.Children.Add(Text("What you get:", "text", weight: FontWeights.SemiBold));
                foreach (var r in c.Rewards) card.Children.Add(Text("\u2022 " + r, "text", top: 1));
            }
            if (!string.IsNullOrEmpty(c.Note)) card.Children.Add(Text(c.Note!, "muted", top: 4, size: 11));
            card.Children.Add(Text(Coupons.ExpiryText(c, DateTime.UtcNow), "muted", top: 2, size: 11));
            return new Border { Child = card, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 10, 0, 0) }.Ref(Border.BorderBrushProperty, "panelBorder").Ref(Border.BackgroundProperty, "buttonBg");
        }

        /// <summary>The clipboard can be busy for a moment (another app holding it): try a few times, never throw.</summary>
        private static bool TryCopy(string text)
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetDataObject(text, true); return true; }
                catch (Exception ex) { if (i == 4) Log.Write("copy code: " + ex.Message); System.Threading.Thread.Sleep(40); }
            }
            return false;
        }
    }
}
