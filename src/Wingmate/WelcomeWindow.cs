using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>The welcome guide: a few short pages with Back / Next and "Got it". Shown once on first open (or after
    /// updating to 1.0.7), and again from Settings. Follows the theme live and the text size setting; always fully opaque.</summary>
    public sealed class WelcomeWindow : Window
    {
        private readonly List<FrameworkElement> _pages = new();
        private readonly List<Ellipse> _dots = new();
        private readonly Button _back, _next;
        private int _index;

        public WelcomeWindow(Window? near)
        {
            Title = "Wingmate - Welcome"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; Width = 380; SizeToContent = SizeToContent.Height;
            FontScaler.Grow(this);
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; this.Ref(ForegroundProperty, "text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/wingmate.ico")); } catch { }
            if (near != null && near.IsVisible) { Owner = near; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = Res<Style>("CloseButton"), Focusable = false, ToolTip = "Close", Margin = new Thickness(2, -2, -6, 0), VerticalAlignment = VerticalAlignment.Top };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Right); head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = "Welcome", FontSize = 13, FontWeight = FontWeights.SemiBold }.Ref(TextBlock.ForegroundProperty, "title"));
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
            var sep = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8) }.Ref(Border.BackgroundProperty, "divider");
            DockPanel.SetDock(sep, Dock.Top); root.Children.Add(sep);

            // footer: page dots on the left, Back / Next (or Got it) on the right
            var foot = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _back = new Button { Content = "Back", Style = Res<Style>("DialogButton"), Margin = new Thickness(0, 0, 8, 0), Focusable = false };
            _next = new Button { Content = "Next", Style = Res<Style>("DialogButton"), Tag = "primary", IsDefault = true };
            _back.Click += (s, e) => ShowPage(_index - 1);
            _next.Click += (s, e) => { if (_index >= _pages.Count - 1) Close(); else ShowPage(_index + 1); };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(_back); buttons.Children.Add(_next);
            DockPanel.SetDock(buttons, Dock.Right);
            foot.Children.Add(buttons); foot.Children.Add(dots);
            DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot);

            // every page sits in the same cell (hidden, not collapsed), so the window keeps the height of the longest page
            var stack = new Grid();
            foreach (var p in WelcomeGuide.Pages)
            {
                var page = new StackPanel { Visibility = Visibility.Hidden };
                page.Children.Add(new TextBlock { Text = p.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6) }.Ref(TextBlock.ForegroundProperty, "title"));
                foreach (var line in p.Lines)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                    var bullet = new TextBlock { Text = "\u2022", Margin = new Thickness(2, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top }.Ref(TextBlock.ForegroundProperty, "muted");
                    DockPanel.SetDock(bullet, Dock.Left);
                    row.Children.Add(bullet);
                    row.Children.Add(new TextBlock { Text = line, FontSize = 12, TextWrapping = TextWrapping.Wrap, LineHeight = 17 });
                    page.Children.Add(row);
                }
                _pages.Add(page);
                stack.Children.Add(page);
                var dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 5, 0) };
                _dots.Add(dot);
                dots.Children.Add(dot);
            }
            root.Children.Add(stack);

            Content = new Border
            {
                CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(14, 8, 10, 12), Child = root,
            }.Ref(Border.BackgroundProperty, "menuBg").Ref(Border.BorderBrushProperty, "panelBorder");

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { Close(); e.Handled = true; }
                else if (e.Key == Key.Right && _index < _pages.Count - 1) { ShowPage(_index + 1); e.Handled = true; }
                else if (e.Key == Key.Left && _index > 0) { ShowPage(_index - 1); e.Handled = true; }
            };
            Loaded += (s, e) => { FontScaler.Apply(this); Activate(); };
            ShowPage(0);
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);

        /// <summary>Opens a page (0-based); for screenshots and the keyboard too.</summary>
        public void ShowPage(int index)
        {
            _index = Math.Clamp(index, 0, _pages.Count - 1);
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visibility = i == _index ? Visibility.Visible : Visibility.Hidden;
                _dots[i].Ref(Shape.FillProperty, i == _index ? "title" : "divider");
            }
            _back.Visibility = _index == 0 ? Visibility.Hidden : Visibility.Visible;
            _next.Content = _index == _pages.Count - 1 ? "Got it" : "Next";
        }
    }
}
