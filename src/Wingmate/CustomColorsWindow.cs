using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>The Custom theme's colours in their own window (opened from Settings: "Start from current theme" or
    /// "Edit custom colors"). One row per colour group plus Warning: swatch (click = pick), name, hex box (Enter applies),
    /// Pick… and Default (back to the start theme's colour). Every change repaints all windows live and is saved to the same
    /// settings fields as before (CustomColors / ThemeWarnColor). Remembers its position like the other windows.</summary>
    public sealed class CustomColorsWindow : Window
    {
        private static AppSettings S => App.Settings;
        private bool _sync;
        private readonly List<Action> _refreshers = new();

        public CustomColorsWindow(Window? near)
        {
            Title = "Wingmate - Custom colors"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; Width = 372; SizeToContent = SizeToContent.Height;
            FontScaler.Grow(this);
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; this.Ref(ForegroundProperty, "text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/wingmate.ico")); } catch { }
            if (WindowSnap.TryRestore(this, S.CustomColorsLeft, S.CustomColorsTop)) { } // last position (brought on-screen when shown)
            else if (near != null && near.IsVisible) { Left = near.Left + near.ActualWidth + 8; Top = near.Top; WindowStartupLocation = WindowStartupLocation.Manual; }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = Res<Style>("CloseButton"), Focusable = false, ToolTip = "Close", Margin = new Thickness(2, -2, -6, 0), VerticalAlignment = VerticalAlignment.Top };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = "Custom colors", FontSize = 13, FontWeight = FontWeights.SemiBold }.Ref(TextBlock.ForegroundProperty, "title"));
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
            var sep = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 6) }.Ref(Border.BackgroundProperty, "divider");
            DockPanel.SetDock(sep, Dock.Top); root.Children.Add(sep);
            var note = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 0, 0, 6) }.Ref(TextBlock.ForegroundProperty, "muted");
            _refreshers.Add(() => note.Text = $"Started from {ShortName(Th.Set.Get(S.CustomBase).Name)}. Changes apply to every window right away.");
            DockPanel.SetDock(note, Dock.Top); root.Children.Add(note);

            var body = new StackPanel();
            string StartColor(CustomTheme.Group g) => CustomTheme.ColorOf(Th.Set.Get(S.CustomBase), g);
            foreach (var g in CustomTheme.Groups)
            {
                var grp = g;
                ColorRow(body, grp.Key, grp.Label, grp.Tip,
                    get: () => CustomTheme.ColorOf(Th.Current.Id == CustomTheme.Id ? Th.Current : Th.BuildCustom(), grp),
                    set: hex => App.Current.SetCustomColor(grp.Key, hex),
                    reset: () => App.Current.ResetCustomColor(grp.Key),
                    isDefault: () => !(S.CustomColors?.ContainsKey(grp.Key) ?? false) || S.CustomColors[grp.Key] == StartColor(grp),
                    defaultTip: () => $"Back to {ShortName(Th.Set.Get(S.CustomBase).Name)}'s color ({StartColor(grp)})");
            }
            ColorRow(body, AppSettings.PreviewWarnKey, "Warning", "Alert flash and the \u201Cstarting soon\u201D look",
                get: () => S.WarnColorFor(CustomTheme.Id),
                set: hex => { if (Th.Current.Id != CustomTheme.Id) App.Current.SetTheme(CustomTheme.Id); S.SetWarnColor(hex); S.Save(); },
                reset: () => { if (Th.Current.Id != CustomTheme.Id) App.Current.SetTheme(CustomTheme.Id); S.ResetWarnColor(); S.Save(); },
                isDefault: () => S.WarnColorFor(CustomTheme.Id) == AppSettings.DefaultWarnColor(CustomTheme.Id),
                defaultTip: () => $"Back to the start theme's warning color ({AppSettings.DefaultWarnColor(CustomTheme.Id)})");
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
                MaxHeight = SystemParameters.WorkArea.Height * 0.8, Padding = new Thickness(0, 0, 6, 0) });
            Content = new Border
            {
                CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 8, 10), Child = root,
            }.Ref(Border.BackgroundProperty, "menuBg").Ref(Border.BorderBrushProperty, "panelBorder");

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); SavePosition(); } };
            WindowSnap.Attach(this, SavePosition);
            IsVisibleChanged += (s, e) => { if (IsVisible) Refresh(); };
            Loaded += (s, e) => FontScaler.Apply(this);
            Th.Changed += Refresh;
            Closing += (s, e) => SavePosition(); // remembered however it is closed (\u00D7 or Settings' toggle)
            Closed += (s, e) => Th.Changed -= Refresh;
            Refresh();
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);
        private static string ShortName(string name) => name.StartsWith("Current") ? "Default" : name.Replace(" (BeeSpecial3D)", "");

        public void Refresh()
        {
            _sync = true;
            try { foreach (var r in _refreshers) r(); }
            finally { _sync = false; }
        }

        private void SavePosition()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top) || !IsVisible) return;
            double l = Math.Round(Left), t = Math.Round(Top);
            if (S.CustomColorsLeft == l && S.CustomColorsTop == t) return;
            S.CustomColorsLeft = l; S.CustomColorsTop = t;
            S.Save();
        }

        /// <summary>"Reset window positions": back to the centre of the screen (the caller saves).</summary>
        public void ResetPosition()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2;
            Top = wa.Top + Math.Max(0, (wa.Height - ActualHeight) / 2);
            S.CustomColorsLeft = Math.Round(Left); S.CustomColorsTop = Math.Round(Top);
        }

        private TextBox HexBox() => FontScaler.Grow(new TextBox
        {
            Width = 62, FontSize = 12, MaxLength = 7, BorderThickness = new Thickness(1), Padding = new Thickness(2, 0, 2, 0),
            TextAlignment = TextAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0),
            ToolTip = "Type a hex color like #FF8800, then Enter",
        }.Ref(ForegroundProperty, "text").Ref(BackgroundProperty, "inputBg").Ref(BorderBrushProperty, "focus").Ref(TextBox.CaretBrushProperty, "caret"));

        /// <summary>One colour row: swatch (click = pick), name, hex box (Enter applies), Pick… and Default.</summary>
        private void ColorRow(Panel into, string key, string label, string tip, Func<string> get, Action<string> set, Action reset, Func<bool> isDefault, Func<string> defaultTip)
        {
            var g = new Grid { Margin = new Thickness(0, 1, 8, 1), ToolTip = tip };
            foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto })
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            var sw = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand, ToolTip = "Pick a color" }.Ref(Border.BorderBrushProperty, "panelBorder");
            var name = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            var hexBox = HexBox();
            void ApplyHex() { if (WarnColors.TryParse(hexBox.Text) != null && WarnColors.Normalize(hexBox.Text) != WarnColors.Normalize(get())) set(WarnColors.Normalize(hexBox.Text)); else Refresh(); }
            hexBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { ApplyHex(); e.Handled = true; } else if (e.Key == Key.Escape) { Refresh(); Keyboard.ClearFocus(); e.Handled = true; } };
            hexBox.LostKeyboardFocus += (s, e) => { if (!_sync) ApplyHex(); };
            void PickNow()
            {
                string? r;
                try { r = ColorPickerDialog.Pick(this, get(), hex => App.Current.PreviewColor(key, hex)); }
                finally { App.Current.EndPreview(); } // Cancel / Escape / close: the saved colour shows again everywhere
                if (r != null && WarnColors.Normalize(r) != WarnColors.Normalize(get())) set(r); // OK: saved as before
            }
            sw.MouseLeftButtonDown += (s, e) => { e.Handled = true; PickNow(); };
            var pick = new Button { Content = "Pick\u2026", Style = Res<Style>("DialogButton"), MinWidth = 0, Focusable = false, FontWeight = FontWeights.Normal, Margin = new Thickness(0, 0, 4, 0) };
            pick.Click += (s, e) => PickNow();
            var def = new Button { Content = "Default", Style = Res<Style>("DialogButton"), MinWidth = 0, Focusable = false, FontWeight = FontWeights.Normal };
            def.Click += (s, e) => reset();
            _refreshers.Add(() =>
            {
                var c = WarnColors.Parse(get());
                sw.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
                if (!hexBox.IsKeyboardFocused) hexBox.Text = WarnColors.Normalize(get());
                def.IsEnabled = !isDefault();
                def.ToolTip = defaultTip();
            });
            Grid.SetColumn(name, 1); Grid.SetColumn(hexBox, 2); Grid.SetColumn(pick, 3); Grid.SetColumn(def, 4);
            g.Children.Add(sw); g.Children.Add(name); g.Children.Add(hexBox); g.Children.Add(pick); g.Children.Add(def);
            into.Children.Add(g);
        }
    }
}
