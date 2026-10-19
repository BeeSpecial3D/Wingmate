using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>All options in one place. Reads and writes App.Settings only (the right-click menus use the same values),
    /// and refreshes itself whenever settings are saved from anywhere.</summary>
    public sealed class SettingsWindow : Window
    {
        private static AppSettings S => App.Settings;
        private static SolidColorBrush B(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

        private bool _sync;
        private readonly List<Action> _refreshers = new();
        private readonly StackPanel _body = new();

        public SettingsWindow(Window? near)
        {
            Title = "Wingmate - Settings"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; Width = 372; SizeToContent = SizeToContent.Height;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; this.Ref(ForegroundProperty, "text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/wingmate.ico")); } catch { }
            if (near != null && near.IsVisible) { Left = near.Left + near.ActualWidth + 8; Top = near.Top; WindowStartupLocation = WindowStartupLocation.Manual; }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = Res<Style>("CloseButton"), Focusable = false, ToolTip = "Close", Margin = new Thickness(2, -2, -6, 0), VerticalAlignment = VerticalAlignment.Top };
            close.Click += (s, e) => Close();
            var min = new Button { Style = Res<Style>("MinButton"), Focusable = false, ToolTip = "Hide", Margin = new Thickness(4, -2, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            min.Click += (s, e) => Hide();
            DockPanel.SetDock(close, Dock.Right); DockPanel.SetDock(min, Dock.Right);
            head.Children.Add(close); head.Children.Add(min);
            head.Children.Add(new TextBlock { Text = "Settings", FontSize = 13, FontWeight = FontWeights.SemiBold }.Ref(TextBlock.ForegroundProperty, "title"));
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
            var sep = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 4) }.Ref(Border.BackgroundProperty, "divider");
            DockPanel.SetDock(sep, Dock.Top); root.Children.Add(sep);
            var feedback = new Button { Content = "Send feedback", Style = Res<Style>("LinkButton"), FontSize = 11, Focusable = false, Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), ToolTip = "Report a problem or suggest something (opens GitHub in your browser)" };
            feedback.Click += (s, e) => App.OpenFeedback();
            var av = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var version = new TextBlock { Text = $"Wingmate v{av?.Major}.{av?.Minor}.{av?.Build}", FontSize = 10, VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 6, 0) }.Ref(TextBlock.ForegroundProperty, "versionText");
            var foot = new DockPanel();
            DockPanel.SetDock(feedback, Dock.Left); foot.Children.Add(feedback); foot.Children.Add(version);
            DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot);
            root.Children.Add(new ScrollViewer
            {
                Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
                MaxHeight = SystemParameters.WorkArea.Height * 0.8, Padding = new Thickness(0, 0, 6, 0),
            });
            Content = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 8, 10), Child = root,
            }.Ref(Border.BackgroundProperty, "menuBg").Ref(Border.BorderBrushProperty, "panelBorder");
            _sync = true; // building sets slider ranges, which must not write settings
            try { Build(); } finally { _sync = false; }

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            IsVisibleChanged += (s, e) => { if (IsVisible) Refresh(); };
            Th.Changed += Refresh; // colours set by refreshers (swatch borders) follow the theme
            Closed += (s, e) => Th.Changed -= Refresh;
            Refresh();
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);

        /// <summary>Pulls every control's state from the settings (called after any save anywhere).</summary>
        public void Refresh()
        {
            _sync = true;
            try { foreach (var r in _refreshers) r(); }
            finally { _sync = false; }
        }

        // ------------------------------------------------------------ builders
        private void Group(string title)
        {
            _body.Children.Add(new TextBlock
            {
                Text = title, FontSize = 10.5, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, _body.Children.Count == 0 ? 2 : 10, 0, 3),
            }.Ref(TextBlock.ForegroundProperty, "title"));
        }

        private void Add(UIElement e) => _body.Children.Add(e);

        /// <summary>Two-per-row grid for short items.</summary>
        private System.Windows.Controls.Primitives.UniformGrid Grid2()
        {
            var g = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            Add(g);
            return g;
        }

        private CheckBox Check(string text, Func<bool> get, Action<bool> set, Func<bool>? enabled = null, Panel? into = null, string? tip = null)
        {
            var cb = new CheckBox { Content = text, Style = Res<Style>("DarkCheck"), ToolTip = tip, Margin = new Thickness(0, 2, 8, 2) };
            cb.Click += (s, e) => { if (!_sync) set(cb.IsChecked == true); };
            _refreshers.Add(() => { cb.IsChecked = get(); if (enabled != null) cb.IsEnabled = enabled(); });
            (into ?? _body).Children.Add(cb);
            return cb;
        }

        private void Pills<T>(IEnumerable<(string Name, T Value)> options, Func<T, bool> isOn, Action<T> pick, Func<T, double>? fontSize = null)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            string group = "g" + Guid.NewGuid().ToString("N");
            foreach (var (name, value) in options)
            {
                var v = value;
                var rb = new RadioButton { Content = name, GroupName = group, Style = Res<Style>("Pill"), Margin = new Thickness(0, 0, 6, 4), VerticalAlignment = VerticalAlignment.Center };
                if (fontSize != null) rb.FontSize = fontSize(v);
                rb.Checked += (s, e) => { if (!_sync) pick(v); };
                _refreshers.Add(() => rb.IsChecked = isOn(v));
                wrap.Children.Add(rb);
            }
            Add(wrap);
        }

        private TextBox SmallBox(string text, double width) => new TextBox
        {
            Text = text, Width = width, FontSize = 12,
            BorderThickness = new Thickness(1), Padding = new Thickness(2, 0, 2, 0),
            TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, MaxLength = 3,
        }.Ref(ForegroundProperty, "text").Ref(BackgroundProperty, "inputBg").Ref(BorderBrushProperty, "focus").Ref(TextBox.CaretBrushProperty, "caret");

        /// <summary>A number readout that turns into a box on click: Enter or click-away applies (clamped), Escape cancels, junk reverts.</summary>
        private FrameworkElement EditableNumber(Func<double> get, Action<double> set, double minV, double maxV, string suffix, double boxWidth)
        {
            var host = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock { TextAlignment = TextAlignment.Right, Cursor = Cursors.IBeam, ToolTip = "Click to type a value",
                Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center }.Ref(TextBlock.ForegroundProperty, "muted");
            _refreshers.Add(() => label.Text = Math.Round(get()) + suffix);
            host.Children.Add(label);
            label.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                var tb = SmallBox(Math.Round(get()).ToString(), boxWidth);
                bool done = false;
                void Finish(bool apply)
                {
                    if (done) return;
                    done = true;
                    if (apply && int.TryParse(tb.Text.Trim().TrimEnd('%'), out var n)) set(Math.Clamp(n, minV, maxV));
                    host.Children.Remove(tb);
                    label.Visibility = Visibility.Visible;
                    Refresh();
                }
                tb.KeyDown += (s2, e2) =>
                {
                    if (e2.Key == Key.Enter) { Finish(true); e2.Handled = true; }
                    else if (e2.Key == Key.Escape) { Finish(false); e2.Handled = true; }
                };
                tb.LostKeyboardFocus += (s2, e2) => Finish(true);
                label.Visibility = Visibility.Hidden;
                host.Children.Add(tb);
                Dispatcher.BeginInvoke(new Action(() => { if (!done) { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); } }), System.Windows.Threading.DispatcherPriority.Input);
            };
            return host;
        }

        private void SliderRow(string label, double minV, double maxV, Func<double> get, Action<double> set, UIElement? extra = null, string? tip = null)
        {
            var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
            var sl = new Slider { Style = Res<Style>("DarkSlider"), Minimum = minV, Maximum = maxV, SmallChange = 1, LargeChange = 10,
                TickFrequency = 5, IsSnapToTickEnabled = false, VerticalAlignment = VerticalAlignment.Center };
            sl.ValueChanged += (s, e) => { if (!_sync) set(Math.Round(sl.Value)); };
            _refreshers.Add(() => sl.Value = Math.Clamp(get(), minV, maxV));
            var val = EditableNumber(get, set, minV, maxV, "%", 40);
            Grid.SetColumn(sl, 1); Grid.SetColumn(val, 2);
            g.Children.Add(name); g.Children.Add(sl); g.Children.Add(val);
            if (extra != null) { Grid.SetColumn(extra, 3); g.Children.Add(extra); }
            Add(g);
        }

        private Button Small(string text, Action click, Panel? into = null, Func<bool>? enabled = null, Func<string>? label = null)
        {
            var b = new Button { Content = text, Style = Res<Style>("DialogButton"), HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 3, 8, 3), Focusable = false, FontWeight = FontWeights.Normal, MinWidth = 0 };
            b.Click += (s, e) => click();
            if (enabled != null || label != null)
                _refreshers.Add(() => { if (enabled != null) b.IsEnabled = enabled(); if (label != null) b.Content = label(); });
            (into ?? _body).Children.Add(b);
            return b;
        }

        private static void SaveS() => S.Save();

        // ------------------------------------------------------------ content
        private void Build()
        {
            Group("TEXT");
            Pills(AppSettings.FontScaleNames.Select((n, i) => (n, AppSettings.FontScales[i])),
                v => Math.Abs(S.FontScale - v) < 0.01, v => App.Current.SetFontScale(v), v => 11 * v);

            Group("THEME");
            BuildThemePills();

            Group("VOICE");
            var test = new Button { Content = "Test", Style = Res<Style>("DialogButton"), MinWidth = 0, Margin = new Thickness(6, 0, 0, 0),
                Focusable = false, FontWeight = FontWeights.Normal, ToolTip = "Play a short clip at this volume", Padding = new Thickness(0) };
            test.Click += (s, e) => Voice.Play(new[] { "test" });
            SliderRow("Volume", 0, 100, () => Math.Round(S.Volume * 100), v => { S.Volume = Math.Clamp(v, 0, 100) / 100.0; SaveS(); }, test);
            Check("Mute voice", () => S.Muted, on => App.Current.SetMuted(on)).HorizontalAlignment = HorizontalAlignment.Left;

            Group("ABYSS ALERTS");
            Pills(App.Schedule.Current.Regions.Select(r => (r.Name, r.Id)), id => S.Region == id, id => { S.Region = id; App.Current.RegionChanged(); });
            var wg = Grid2();
            var warn = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            warn.Children.Add(new TextBlock { Text = "Warn me", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var num = EditableNumber(() => AlertTypes.ClampMinutes(S.WarnMinutes), v => { S.WarnMinutes = AlertTypes.ClampMinutes((int)v); SaveS(); }, 1, 60, "", 30);
            warn.Children.Add(new Border { Child = num, MinWidth = 26, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center }.Ref(Border.BorderBrushProperty, "panelBorder"));
            var arrows = new StackPanel { Margin = new Thickness(2, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            foreach (var (glyph, d) in new[] { ("\u25B2", 1), ("\u25BC", -1) })
            {
                var delta = d;
                var a = new Button { Content = glyph, Style = Res<Style>("LinkButton"), FontSize = 7, Margin = new Thickness(0, -1, 0, -1), ToolTip = delta > 0 ? "One more minute" : "One less minute" };
                a.Click += (s, e) => { S.WarnMinutes = AlertTypes.ClampMinutes(AlertTypes.ClampMinutes(S.WarnMinutes) + delta); SaveS(); };
                arrows.Children.Add(a);
            }
            warn.Children.Add(arrows);
            warn.Children.Add(new TextBlock { Text = "min before", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
            wg.Children.Add(warn);
            Check("When it starts", () => S.AlertAtStart, on => { S.AlertAtStart = on; SaveS(); }, into: wg);

            foreach (var (group, items) in AlertTypes.Grouped(App.Schedule.Current))
            {
                Add(new TextBlock { Text = group + " alerts", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 1) }.Ref(TextBlock.ForegroundProperty, "subTitle"));
                var ag = Grid2();
                foreach (var t in items)
                {
                    var key = t.Key; var ev = t.Event;
                    Check(t.Label, () => AlertTypes.Enabled(S.AlertChoices, ev), on => { S.AlertChoices[key] = on; SaveS(); }, into: ag, tip: t.Full);
                }
            }
            var tg = BuildWarnColorRows();
            Small("Test alert", () => App.Current.TestAlert(), tg);

            Group("WINDOWS");
            // one opacity for the launcher, alerts and checklist; each theme remembers its own (switching themes applies it)
            SliderRow("Opacity", 40, 100, () => Math.Round(S.WindowOpacity * 100), v => App.Current.SetOpacity(Math.Clamp(v, 40, 100) / 100.0),
                tip: "All windows. Each theme keeps its own opacity.");
            var cg = Grid2();
            Check("Alerts click-through", () => App.Current.AbyssClickThrough == true, on => App.Current.SetAbyssClickThrough(on),
                () => App.Current.AbyssClickThrough != null, cg, "Clicks pass through the alerts window (only while it is showing)");
            Check("Checklist click-through", () => App.Current.ChecklistClickThrough == true, on => App.Current.SetChecklistClickThrough(on),
                () => App.Current.ChecklistClickThrough != null, cg, "Clicks pass through the checklist window (only while it is showing)");
            BuildHotkeyRow();
            var og = Grid2();
            Check("Auto-minimize launcher", () => S.LauncherAutoMinimize, on => { S.LauncherAutoMinimize = on; SaveS(); }, into: og,
                tip: "Tuck the launcher into the tray after opening a window");
            Small("Reset window positions", () => App.Current.ResetAllPositions(), og);
            Check("Ask before quitting", () => S.AskBeforeQuit, on => { S.AskBeforeQuit = on; SaveS(); }, into: og,
                tip: "The launcher's \u00D7 asks before quitting Wingmate");

            Group("CHECKLIST");
            var cv = Grid2();
            Check("Celebration voice", () => S.CelebrationVoice, on => { S.CelebrationVoice = on; SaveS(); }, into: cv);
            Small("Preview celebration", () => App.Current.PreviewCelebration(), cv, () => App.Current.CanPreviewCelebration);
            var lg = Grid2();
            Small("Show hidden", () => { App.Current.ShowHiddenSections(); Refresh(); }, lg,
                () => App.Current.HiddenSectionCount > 0, () => $"Show hidden ({App.Current.HiddenSectionCount})");
            Small("Reset all lists\u2026", () => { App.Current.ResetAllLists(this); Refresh(); }, lg);
        }

        /// <summary>Theme pills, each with a dot in that theme's panel and title colours. Applies live.</summary>
        private void BuildThemePills()
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            foreach (var t in ThemeOrder(Th.Set.All))
            {
                var id = t.Id;
                var dot = new Ellipse { Width = 10, Height = 10, StrokeThickness = 2, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(t.Colors["panelBg"])) { Opacity = 1 },
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(t.Colors["title"])) };
                var c = ((SolidColorBrush)dot.Fill).Color; ((SolidColorBrush)dot.Fill).Color = Color.FromRgb(c.R, c.G, c.B); // opaque dot
                var label = new StackPanel { Orientation = Orientation.Horizontal };
                label.Children.Add(dot);
                label.Children.Add(new TextBlock { Text = ShortName(t.Name), VerticalAlignment = VerticalAlignment.Center });
                var rb = new RadioButton { Content = label, GroupName = "theme", Style = Res<Style>("Pill"), Margin = new Thickness(0, 0, 6, 4), ToolTip = t.Description };
                rb.Checked += (s, e) => { if (!_sync) App.Current.SetTheme(id); };
                _refreshers.Add(() => rb.IsChecked = Th.Current.Id == id);
                wrap.Children.Add(rb);
            }
            Add(wrap);
        }

        /// <summary>Picker order: Current, Elyos, Asmodian, Abyss, Honey, Midnight, Light (Midnight beside Light); any other themes after.</summary>
        internal static readonly string[] PickerOrder = { "current", "elyos", "asmodian", "abyss", "honey", "midnight", "light" };
        private static IEnumerable<Core.ThemeSet.Theme> ThemeOrder(IEnumerable<Core.ThemeSet.Theme> all) =>
            all.OrderBy(t => { int i = Array.IndexOf(PickerOrder, t.Id); return i < 0 ? int.MaxValue : i; });

        private static string ShortName(string name) => name.StartsWith("Current") ? "Default" : name.Replace(" (BeeSpecial3D)", "");

        /// <summary>Warning colour of the active theme: preset swatches, then hex box + Pick… + Default (the theme's own colour).</summary>
        private System.Windows.Controls.Primitives.UniformGrid BuildWarnColorRows()
        {
            Add(new TextBlock { Text = "Warning color", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 2) }.Ref(TextBlock.ForegroundProperty, "subTitle"));
            void SetColor(string hex) { S.SetWarnColor(hex); SaveS(); }
            var swatches = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            foreach (var (name, hex) in WarnColors.Presets)
            {
                var h = hex;
                var c = WarnColors.Parse(h);
                var sw = new Border { Width = 22, Height = 18, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 4), Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)), BorderThickness = new Thickness(2), ToolTip = name };
                sw.MouseLeftButtonDown += (s, e) => { e.Handled = true; SetColor(h); };
                _refreshers.Add(() => sw.BorderBrush = S.ActiveWarnColor == h ? Th.Br("focus") : Th.Br("iconHoverBg"));
                swatches.Children.Add(sw);
            }
            Add(swatches);
            var g = Grid2();
            var custom = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 3) };
            var cur = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center }.Ref(Border.BorderBrushProperty, "panelBorder");
            var hexBox = SmallBox("", 66);
            hexBox.MaxLength = 7; hexBox.TextAlignment = TextAlignment.Left; hexBox.Margin = new Thickness(5, 0, 5, 0); hexBox.ToolTip = "Type a hex color like #FF8800, then Enter";
            void ApplyHex() { if (WarnColors.TryParse(hexBox.Text) != null) SetColor(hexBox.Text); else Refresh(); }
            hexBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { ApplyHex(); e.Handled = true; } else if (e.Key == Key.Escape) { Refresh(); Keyboard.ClearFocus(); e.Handled = true; } };
            hexBox.LostKeyboardFocus += (s, e) => ApplyHex();
            _refreshers.Add(() =>
            {
                var c = WarnColors.Parse(S.ActiveWarnColor);
                cur.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
                if (!hexBox.IsKeyboardFocused) hexBox.Text = S.ActiveWarnColor;
            });
            var pick = new Button { Content = "Pick\u2026", Style = Res<Style>("DialogButton"), MinWidth = 0, Focusable = false, FontWeight = FontWeights.Normal, Padding = new Thickness(0) };
            pick.Click += (s, e) => { var r = ColorPickerDialog.Pick(this, S.ActiveWarnColor); if (r != null) SetColor(r); };
            var reset = new Button { Content = "Default", Style = Res<Style>("DialogButton"), MinWidth = 0, Focusable = false, FontWeight = FontWeights.Normal,
                Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(4, 0, 0, 0) };
            reset.Click += (s, e) => { S.ResetWarnColor(); SaveS(); };
            _refreshers.Add(() =>
            {
                var d = AppSettings.DefaultWarnColor(S.Theme);
                reset.IsEnabled = S.ActiveWarnColor != d;
                reset.ToolTip = $"Back to this theme's own warning color ({d})";
            });
            custom.Children.Add(cur); custom.Children.Add(hexBox); custom.Children.Add(pick); custom.Children.Add(reset);
            g.Children.Add(custom);
            return g; // the caller puts Test alert next to the custom colour
        }

        /// <summary>Optional click-through hotkey: on/off plus a "press new keys" box. RegisterHotKey only.</summary>
        private void BuildHotkeyRow()
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var cb = Check("Click-through hotkey", () => S.HotkeyEnabled, on => { S.HotkeyEnabled = on; App.Current.ApplyHotkey(); SaveS(); }, into: row,
                tip: "Toggles click-through for the alerts and checklist together");
            DockPanel.SetDock(cb, Dock.Left);
            var box = new TextBox
            {
                IsReadOnly = true, IsReadOnlyCaretVisible = false, Width = 130, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 1, 4, 1), Cursor = Cursors.Hand, ToolTip = "Click, then press the new key combo (Esc cancels)",
                TextAlignment = TextAlignment.Center,
            }.Ref(ForegroundProperty, "text").Ref(BackgroundProperty, "fieldBg").Ref(BorderBrushProperty, "panelBorder");
            _refreshers.Add(() => { if (!box.IsKeyboardFocused) box.Text = Hotkey.Describe(S.HotkeyModifiers, S.HotkeyKey); });
            // the key-combo box only shows while the hotkey is on. Hidden (not collapsed) keeps the row height, so nothing below moves.
            void SyncBox() => box.Visibility = cb.IsChecked == true ? Visibility.Visible : Visibility.Hidden;
            _refreshers.Add(SyncBox);
            cb.Checked += (s, e) => SyncBox();
            cb.Unchecked += (s, e) => SyncBox();
            box.GotKeyboardFocus += (s, e) => { App.Current.SuspendHotkey(); box.Text = "Press keys\u2026"; box.Ref(BorderBrushProperty, "focus"); };
            box.LostKeyboardFocus += (s, e) => { box.Ref(BorderBrushProperty, "panelBorder"); App.Current.ApplyHotkey(); Refresh(); };
            box.PreviewKeyDown += (s, e) =>
            {
                e.Handled = true;
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key == Key.Escape) { Keyboard.ClearFocus(); FocusManager.SetFocusedElement(this, null); Refresh(); return; }
                if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
                int mods = 0;
                var km = Keyboard.Modifiers;
                if (km.HasFlag(ModifierKeys.Control)) mods |= Hotkey.MOD_CONTROL;
                if (km.HasFlag(ModifierKeys.Alt)) mods |= Hotkey.MOD_ALT;
                if (km.HasFlag(ModifierKeys.Shift)) mods |= Hotkey.MOD_SHIFT;
                if (km.HasFlag(ModifierKeys.Windows)) mods |= Hotkey.MOD_WIN;
                int vk = KeyInterop.VirtualKeyFromKey(key);
                if (mods == 0 || vk == 0) { box.Text = "Use Ctrl, Alt or Shift + a key"; return; }
                if (Hotkey.IsReserved(mods, vk)) { box.Text = "Reserved, pick another"; return; }
                S.HotkeyModifiers = mods; S.HotkeyKey = vk;
                SaveS();
                box.Text = Hotkey.Describe(mods, vk);
                Keyboard.ClearFocus(); // applies on lost focus
            };
            row.Children.Add(box);
            Add(row);
            var note = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) }.Ref(TextBlock.ForegroundProperty, "error");
            _refreshers.Add(() => { note.Text = App.Current.HotkeyNote; note.Visibility = string.IsNullOrEmpty(note.Text) ? Visibility.Collapsed : Visibility.Visible; });
            Add(note);
        }
    }
}
