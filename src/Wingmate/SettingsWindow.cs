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
        private StackPanel _body = new(); // the tab page being built (see Page)
        private readonly Grid _pages = new();
        private readonly WrapPanel _tabs = new() { Margin = new Thickness(0, 2, 0, 4) };
        private readonly Dictionary<string, (StackPanel Page, RadioButton Tab)> _tabPages = new();
        /// <summary>Settings reopens on the tab it was last on (for this run of Wingmate).</summary>
        private static string _lastTab = "General";
        public static readonly string[] TabNames = { "General", "Appearance", "Alerts", "Checklist", "About" };

        public SettingsWindow(Window? near)
        {
            Title = "Wingmate - Settings"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; Width = 372; SizeToContent = SizeToContent.Height;
            FontScaler.Grow(this); // wider with bigger text (the Font size setting applies here too)
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; this.Ref(ForegroundProperty, "text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/wingmate.ico")); } catch { }
            if (WindowSnap.TryRestore(this, App.Settings.SettingsLeft, App.Settings.SettingsTop)) { } // last position (brought on-screen when shown)
            else if (near != null && near.IsVisible) { Left = near.Left + near.ActualWidth + 8; Top = near.Top; WindowStartupLocation = WindowStartupLocation.Manual; }
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
            DockPanel.SetDock(_tabs, Dock.Top); root.Children.Add(_tabs);
            var feedback = new Button { Content = "Send feedback", Style = Res<Style>("LinkButton"), FontSize = 11, Focusable = false, Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), ToolTip = "Report a problem or suggest something (opens GitHub in your browser)" };
            feedback.Click += (s, e) => App.OpenFeedback();
            var av = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var version = new TextBlock { Text = $"Wingmate v{av?.Major}.{av?.Minor}.{av?.Build}", FontSize = 10, VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 6, 0) }.Ref(TextBlock.ForegroundProperty, "versionText");
            App.SetVersionLink(version, $"v{av?.Major}.{av?.Minor}.{av?.Build}", prefix: "Wingmate ");
            var foot = new DockPanel();
            DockPanel.SetDock(feedback, Dock.Left); foot.Children.Add(feedback); foot.Children.Add(version);
            DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot);
            root.Children.Add(new ScrollViewer
            {
                Content = _pages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false,
                MaxHeight = SystemParameters.WorkArea.Height * 0.8, Padding = new Thickness(0, 0, 6, 0),
            });
            Content = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1), Padding = new Thickness(12, 8, 8, 10), Child = root,
            }.Ref(Border.BackgroundProperty, "menuBg").Ref(Border.BorderBrushProperty, "panelBorder");
            _sync = true; // building sets slider ranges, which must not write settings
            try { Build(); } finally { _sync = false; }
            ShowTab(_lastTab);

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); SavePosition(); } };
            WindowSnap.Attach(this, SavePosition);
            IsVisibleChanged += (s, e) => { if (IsVisible) Refresh(); };
            Loaded += (s, e) => FontScaler.Apply(this);
            Th.Changed += Refresh; // colours set by refreshers (swatch borders) follow the theme
            Closed += (s, e) => Th.Changed -= Refresh;
            Refresh();
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);

        /// <summary>Starts a tab: what is added next goes on its page. The tab strip sits at the top under the title.</summary>
        private void Page(string name)
        {
            var page = new StackPanel { Visibility = Visibility.Collapsed };
            _pages.Children.Add(page);
            var tab = new RadioButton { Content = name, GroupName = "settings-tab", Style = Res<Style>("Pill"), Margin = new Thickness(0, 0, 5, 4), Focusable = false };
            tab.Checked += (s, e) => ShowTab(name);
            _tabs.Children.Add(tab);
            _tabPages[name] = (page, tab);
            _body = page;
        }

        /// <summary>Shows one tab (unknown name: General). The window keeps its width and grows or shrinks to the page.</summary>
        public void ShowTab(string name)
        {
            if (!_tabPages.ContainsKey(name)) name = TabNames[0];
            _lastTab = name;
            foreach (var (n, (page, tab)) in _tabPages)
            {
                page.Visibility = n == name ? Visibility.Visible : Visibility.Collapsed;
                if (n == name && tab.IsChecked != true) tab.IsChecked = true;
            }
            if (IsLoaded) FontScaler.Apply(_tabPages[name].Page);
        }

        public string CurrentTab => _lastTab;

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

        /// <summary>Remembers where Settings is, so it reopens there (only saves when it actually moved).</summary>
        private void SavePosition()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top) || !IsVisible) return;
            double l = Math.Round(Left), t = Math.Round(Top);
            if (S.SettingsLeft == l && S.SettingsTop == t) return;
            S.SettingsLeft = l; S.SettingsTop = t;
            S.Save();
        }

        /// <summary>"Reset window positions": back to the centre of the screen (the caller saves).</summary>
        public void ResetPosition()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2;
            Top = wa.Top + Math.Max(0, (wa.Height - ActualHeight) / 2);
            S.SettingsLeft = Math.Round(Left); S.SettingsTop = Math.Round(Top);
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

        private TextBox SmallBox(string text, double width) => FontScaler.Scaled(FontScaler.Grow(new TextBox
        {
            Text = text, Width = width, FontSize = 12,
            BorderThickness = new Thickness(1), Padding = new Thickness(2, 0, 2, 0),
            TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, MaxLength = 3,
        }.Ref(ForegroundProperty, "text").Ref(BackgroundProperty, "inputBg").Ref(BorderBrushProperty, "focus").Ref(TextBox.CaretBrushProperty, "caret")));

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
            // ---------------------------------------------------------------- General
            Page("General");
            Group("VOICE");
            var test = new Button { Content = "Test", Style = Res<Style>("DialogButton"), MinWidth = 0, Margin = new Thickness(6, 0, 0, 0),
                Focusable = false, FontWeight = FontWeights.Normal, ToolTip = "Play a short clip at this volume", Padding = new Thickness(0) };
            test.Click += (s, e) => Voice.Play(new[] { "test" });
            SliderRow("Volume", 0, 100, () => Math.Round(S.Volume * 100), v => { S.Volume = Math.Clamp(v, 0, 100) / 100.0; SaveS(); }, test);
            Check("Mute voice", () => S.Muted, on => App.Current.SetMuted(on)).HorizontalAlignment = HorizontalAlignment.Left;

            Group("WINDOWS");
            var cg = Grid2();
            Check("Alert window lock", () => App.Current.AbyssClickThrough == true, on => App.Current.SetAbyssClickThrough(on),
                () => App.Current.AbyssClickThrough != null, cg, "Locked: clicks pass through the alerts window to the game (only while it is showing)");
            Check("Checklist window lock", () => App.Current.ChecklistClickThrough == true, on => App.Current.SetChecklistClickThrough(on),
                () => App.Current.ChecklistClickThrough != null, cg, "Locked: clicks pass through the checklist window to the game (only while it is showing)");
            BuildHotkeyRow();
            var og = Grid2();
            Check("Ask before quitting", () => S.AskBeforeQuit, on => { S.AskBeforeQuit = on; SaveS(); }, into: og,
                tip: "The launcher's \u00D7 asks before quitting Wingmate");
            Small("Reset window positions", () => App.Current.ResetAllPositions(), og);

            // ---------------------------------------------------------------- Appearance
            Page("Appearance");
            Group("TEXT");
            Pills(AppSettings.FontScaleNames.Select((n, i) => (n, AppSettings.FontScales[i])),
                v => Math.Abs(S.FontScale - v) < 0.01, v => App.Current.SetFontScale(v), v => 11 * v);

            Group("THEME");
            BuildThemePills();

            Group("OPACITY");
            // one opacity for the launcher, alerts and checklist; each theme remembers its own (switching themes applies it)
            SliderRow("Opacity", 40, 100, () => Math.Round(S.WindowOpacity * 100), v => App.Current.SetOpacity(Math.Clamp(v, 40, 100) / 100.0),
                tip: "All windows. Each theme keeps its own opacity.");
            Add(new TextBlock { Text = "The launcher, alerts and checklist windows. Settings always stays fully visible.", FontSize = 11,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) }.Ref(TextBlock.ForegroundProperty, "muted"));

            // ---------------------------------------------------------------- Alerts
            Page("Alerts");
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
            // the warning colour is edited in the Custom colors window since 1.0.5 (saved choices keep working on every theme)
            Small("Test alert", () => App.Current.TestAlert(), Grid2()).ToolTip = "Show a sample alert in the current warning color";
            BuildCustomAlerts();

            // ---------------------------------------------------------------- Checklist
            Page("Checklist");
            Group("CHECKLIST");
            var cv = Grid2();
            Check("Celebration voice", () => S.CelebrationVoice, on => { S.CelebrationVoice = on; SaveS(); }, into: cv);
            Small("Preview celebration", () => App.Current.PreviewCelebration(), cv, () => App.Current.CanPreviewCelebration);
            var lg = Grid2();
            Small("Show hidden", () => { App.Current.ShowHiddenSections(); Refresh(); }, lg,
                () => App.Current.HiddenSectionCount > 0, () => $"Show hidden ({App.Current.HiddenSectionCount})");
            Small("Reset all lists\u2026", () => { App.Current.ResetAllLists(this); Refresh(); }, lg);
            // ---------------------------------------------------------------- About
            Page("About");
            Group("WINGMATE");
            Add(new TextBlock { Text = "A companion for Aion 2: Abyss event alerts and a daily and weekly checklist for each character.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
            var ab = Grid2();
            Small("Show welcome guide", () => App.Current.ShowWelcome(), ab).ToolTip = "The short guide shown the first time Wingmate opens";
            Small("Version history", () => App.OpenUrl(App.ReleasesUrl), ab).ToolTip = "What changed in each release (opens GitHub in your browser)";
            var ab2 = Grid2();
            Small("Project page", () => App.OpenUrl(App.ProjectUrl), ab2).ToolTip = "Wingmate on GitHub";
            Small("Send feedback", () => App.OpenFeedback(), ab2).ToolTip = "Report a problem or suggest something (opens GitHub in your browser)";
        }

        /// <summary>The custom alerts list (on/off, Edit, Delete) and "Add custom alert…". The list rebuilds after every save.</summary>
        private void BuildCustomAlerts()
        {
            Group("CUSTOM ALERTS");
            var list = new StackPanel();
            Add(list);
            var empty = new TextBlock { Text = "None yet. Add your own alert with a name, time and days.", FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2) }.Ref(TextBlock.ForegroundProperty, "muted");
            string shown = "";
            _refreshers.Add(() =>
            {
                var alerts = S.CustomAlerts ?? new List<CustomAlert>();
                var key = string.Join("|", alerts.Select(a => $"{a.Id}/{a.Name}/{a.Time}/{string.Join(",", a.Days)}/{a.RepeatHours}/{a.Enabled}"));
                if (key == shown && list.Children.Count > 0) return; // unchanged: keep the rows (and their focus)
                shown = key;
                list.Children.Clear();
                if (alerts.Count == 0) { list.Children.Add(empty); return; }
                foreach (var a in alerts.ToList()) list.Children.Add(FontScaler.Scaled(CustomAlertRow(a)));
            });
            var row = Grid2();
            Small("Add custom alert\u2026", () => { CustomAlertWindow.Edit(this, null); Refresh(); }, row, () => (S.CustomAlerts?.Count ?? 0) < CustomAlerts.MaxCount)
                .ToolTip = "Your own alert: a name, a time on the region's clock, days and an optional repeat";
        }

        private FrameworkElement CustomAlertRow(CustomAlert a)
        {
            var id = a.Id;
            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var links = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var edit = new Button { Content = "Edit", Style = Res<Style>("LinkButton"), FontSize = 11, Focusable = false, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Change this alert" };
            edit.Click += (s, e) => { var cur = S.CustomAlerts.FirstOrDefault(x => x.Id == id); if (cur != null) { CustomAlertWindow.Edit(this, cur); Refresh(); } };
            var del = new Button { Content = "Delete", Style = Res<Style>("LinkButton"), FontSize = 11, Focusable = false, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Delete this alert" };
            del.Click += (s, e) =>
            {
                var cur = S.CustomAlerts.FirstOrDefault(x => x.Id == id);
                if (cur == null || !ConfirmDialog.Ask(this, "Delete alert?", $"Delete the custom alert \"{cur.Name}\"?", "Delete", ConfirmKind.Danger)) return;
                S.CustomAlerts.RemoveAll(x => x.Id == id);
                SaveS();
            };
            links.Children.Add(edit); links.Children.Add(del);
            DockPanel.SetDock(links, Dock.Right);
            row.Children.Add(links);
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(new TextBlock { Text = a.Name, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150 });
            label.Children.Add(new TextBlock { Text = "  " + CustomAlerts.Describe(a), FontSize = 11, VerticalAlignment = VerticalAlignment.Center }.Ref(TextBlock.ForegroundProperty, "muted"));
            var cb = new CheckBox { Content = label, Style = Res<Style>("DarkCheck"), IsChecked = a.Enabled, Margin = new Thickness(0, 2, 0, 2),
                ToolTip = a.Enabled ? "On: click to turn this alert off" : "Off: click to turn this alert on" };
            cb.Click += (s, e) =>
            {
                var cur = S.CustomAlerts.FirstOrDefault(x => x.Id == id);
                if (cur == null) return;
                cur.Enabled = cb.IsChecked == true;
                SaveS();
            };
            row.Children.Add(cb);
            return row;
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
            // the user's own theme, always last
            {
                var dot = new Ellipse { Width = 10, Height = 10, StrokeThickness = 2, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
                var label = new StackPanel { Orientation = Orientation.Horizontal };
                label.Children.Add(dot);
                label.Children.Add(new TextBlock { Text = CustomTheme.Name, VerticalAlignment = VerticalAlignment.Center });
                var rb = new RadioButton { Content = label, GroupName = "theme", Style = Res<Style>("Pill"), Margin = new Thickness(0, 0, 6, 4),
                    ToolTip = "Your own colors. Use \u201CStart from current theme\u201D to begin from the theme you like." };
                rb.Checked += (s, e) => { if (!_sync) App.Current.SetTheme(CustomTheme.Id); };
                _refreshers.Add(() =>
                {
                    rb.IsChecked = Th.Current.Id == CustomTheme.Id;
                    var c = Th.Current.Id == CustomTheme.Id ? Th.Current : Th.BuildCustom();
                    Color Opaque(string hex) { var k = (Color)ColorConverter.ConvertFromString(hex); return Color.FromRgb(k.R, k.G, k.B); }
                    dot.Fill = new SolidColorBrush(Opaque(c.Colors["panelBg"]));
                    dot.Stroke = new SolidColorBrush(Opaque(c.Colors["title"]));
                });
                wrap.Children.Add(rb);
            }
            Add(wrap);
            BuildCustomEditor();
        }

        /// <summary>"Start from current theme" (copies the theme in use into Custom, switches to it and opens the Custom colors
        /// window) and "Edit custom colors" (reopens that window once a Custom theme exists). The colour rows live in
        /// <see cref="CustomColorsWindow"/>.</summary>
        private void BuildCustomEditor()
        {
            var row = Grid2();
            Small("Start from current theme", () => App.Current.StartCustomFromCurrent(), row, () => Th.Current.Id != CustomTheme.Id)
                .ToolTip = "Copy the colors of the theme you're using into Custom, switch to Custom and open the Custom colors window";
            var edit = Small("Edit custom colors\u2026", () => App.Current.ToggleCustomColors(), row, () => S.HasCustomTheme);
            _refreshers.Add(() => edit.ToolTip = App.Current.CustomColorsOpen ? "Close the Custom colors window" : "Open the Custom colors window");
        }

        /// <summary>Picker order: Current, Elyos, Asmodian, Abyss, Honey, Midnight, Light (Midnight beside Light); any other themes after.</summary>
        internal static readonly string[] PickerOrder = { "current", "elyos", "asmodian", "abyss", "honey", "midnight", "light" };
        private static IEnumerable<Core.ThemeSet.Theme> ThemeOrder(IEnumerable<Core.ThemeSet.Theme> all) =>
            all.OrderBy(t => { int i = Array.IndexOf(PickerOrder, t.Id); return i < 0 ? int.MaxValue : i; });

        private static string ShortName(string name) => name.StartsWith("Current") ? "Default" : name.Replace(" (BeeSpecial3D)", "");

        /// <summary>Optional window lock (click-through) hotkey: on/off plus a "press new keys" box. RegisterHotKey only.</summary>
        private void BuildHotkeyRow()
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var cb = Check("Window lock hotkey", () => S.HotkeyEnabled, on => { S.HotkeyEnabled = on; App.Current.ApplyHotkey(); SaveS(); }, into: row,
                tip: "Locks or unlocks the alerts and checklist windows together");
            DockPanel.SetDock(cb, Dock.Left);
            var box = new TextBox
            {
                IsReadOnly = true, IsReadOnlyCaretVisible = false, Width = 130, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 1, 4, 1), Cursor = Cursors.Hand, ToolTip = "Click, then press the new key combo (Esc cancels)",
                TextAlignment = TextAlignment.Center,
            }.Ref(ForegroundProperty, "text").Ref(BackgroundProperty, "fieldBg").Ref(BorderBrushProperty, "panelBorder");
            FontScaler.Grow(box);
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
