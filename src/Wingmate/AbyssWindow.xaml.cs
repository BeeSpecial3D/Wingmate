using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wingmate.Core;

namespace Wingmate
{
    public partial class AbyssWindow : Window
    {
        private const double DefaultWidth = 520, MinW = 260;
        private static Brush B(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }

        // "starting soon" follows the user's warning colour (default red = these original brushes), never the theme
        private static readonly (Brush Bg, Brush Bd, Brush L1) SoonDefault = (B("#EECC2B2B"), B("#FFFF6B6B"), B("#FFFFFFFF"));
        private static readonly Brush ExtraSoonDefault = B("#FFFF5C5C");
        private static readonly Brush FlashOn = B("#FFFF1E1E"), ExtraClear = B("#00000000");

        /// <summary>Normal / none / live looks come from the theme.</summary>
        private static (Brush Bg, Brush Bd, Brush L1) ModeLook(string mode) => mode switch
        {
            "none" => (Th.Br("panelBg"), Th.Br("panelBorder"), Th.Br("muted")),
            "live" => (Th.Br("panelBg"), Th.Br("live"), Th.Br("liveText")),
            _ => (Th.Br("panelBg"), Th.Br("panelBorder"), Th.Br("text")),
        };
        private static Brush ExtraLook(string kind) => kind == "live" ? Th.Br("live") : Th.Br("muted");

        // warning colour (default red = the original brushes)
        private Brush _flashOn = FlashOn, _extraSoon = ExtraSoonDefault;
        private (Brush Bg, Brush Bd, Brush L1) _soon = SoonDefault;

        private static Color Mix(Color a, Color b, double t) =>
            Color.FromArgb(a.A, (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        private static Brush Fz(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        /// <summary>Applies the Settings warning colour live (flash and the "starting soon" look).</summary>
        private string? _appliedWarn;

        public void ApplyWarnColor()
        {
            var norm = WarnColors.Normalize(S.ActiveWarnColor);
            if (norm == _appliedWarn) return;
            _appliedWarn = norm;
            if (WarnColors.IsDefault(S.ActiveWarnColor))
            {
                _flashOn = FlashOn; _extraSoon = ExtraSoonDefault; _soon = SoonDefault;
            }
            else
            {
                var (r, g, b) = WarnColors.Parse(S.ActiveWarnColor);
                var c = Color.FromRgb(r, g, b);
                bool light = 0.299 * r + 0.587 * g + 0.114 * b > 170; // keep white text readable on light colours
                _flashOn = Fz(c);
                _soon = (Fz(Color.FromArgb(0xEE, (byte)(r * 0.8), (byte)(g * 0.8), (byte)(b * 0.8))), Fz(Mix(c, Colors.White, 0.4)),
                         light ? Fz(Color.FromRgb(0x14, 0x1A, 0x24)) : SoonDefault.L1);
                _extraSoon = Fz(Mix(c, Colors.White, 0.3));
            }
            if (_mode == "soon") { _mode = ""; SetMode("soon"); }
            UpdateView();
        }

        private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _flash = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly Dictionary<string, DateTime> _fired = new();
        private int _tickCount, _flashTicks;
        private bool _flashing;
        private string _mode = "";

        public bool ClickThrough { get; private set; }
        private AppSettings S => App.Settings;

        public AbyssWindow()
        {
            InitializeComponent();
            _lock = new LockOverlay(this, LockSlot, () => ClickThrough, () => SetClickThrough(!ClickThrough), "alerts");
            Width = Math.Clamp(double.IsNaN(S.Width) ? DefaultWidth : S.Width, MinW, SystemParameters.VirtualScreenWidth);
            Opacity = S.WindowOpacity;
            PlaceWindow();

            SourceInitialized += (s, e) => { var h = Handle; if (h != IntPtr.Zero) { Native.OverlayStyles(h); if (ClickThrough) Native.SetClickThrough(h, true); } };
            MenuFocus.Attach(this, () => false);
            Panel.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState != MouseButtonState.Pressed) return;
                try { DragMove(); } catch { }
                WindowSnap.Clamp(this);
                SaveLayout();
            };
            ResizeGrip.DragDelta += (s, e) => Width = Math.Clamp(ActualWidth + e.HorizontalChange, MinW, SystemParameters.VirtualScreenWidth);
            ResizeGrip.DragCompleted += (s, e) => { WindowSnap.Clamp(this, true); SaveLayout(); };
            WindowSnap.Attach(this, SaveLayout);
            Loaded += (s, e) => FontScaler.Apply(this);
            Panel.ContextMenuOpening += (s, e) => BuildMenu();
            _flash.Tick += Flash_Tick;
            _tick.Tick += (s, e) =>
            {
                _tickCount++;
                UpdateView();
                if (_tickCount % 6 == 0 && IsVisible && Handle != IntPtr.Zero) Native.KeepTopmost(Handle);
            };
            App.Schedule.Changed += () => Dispatcher.BeginInvoke(new Action(UpdateView));
            IsVisibleChanged += (s, e) => { if (IsVisible) { UpdateView(); _tick.Start(); } else { _tick.Stop(); SaveLayout(); } };
            Closing += (s, e) => { e.Cancel = true; App.Current.HideAbyss(); }; // Alt+F4 just hides
            RefreshBadges();
            ApplyWarnColor();
        }

        private IntPtr Handle => new WindowInteropHelper(this).Handle;

        private RegionDef Region =>
            App.Schedule.Current.Regions.FirstOrDefault(r => r.Id == S.Region) ?? App.Schedule.Current.Regions[0];

        private void PlaceWindow()
        {
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
            if (S.Left is double l && S.Top is double t && l >= vl - 250 && l <= vr - 50 && t >= vt && t <= vb - 30)
            { Left = l; Top = t; return; }
            var wa = SystemParameters.WorkArea;
            Left = (wa.Left + wa.Right) / 2 - Width / 2;
            Top = wa.Top + 10;
        }

        public void SaveLayout()
        {
            if (!double.IsNaN(Left)) S.Left = Math.Round(Left);
            if (!double.IsNaN(Top)) S.Top = Math.Round(Top);
            if (!double.IsNaN(Width)) S.Width = Math.Round(Width);
            S.Save();
        }

        public void ResetLayout()
        {
            S.Left = null; S.Top = null; Width = DefaultWidth;
            PlaceWindow(); SaveLayout();
        }

        public void RefreshBadges()
        {
            MutedBadge.Visibility = S.Muted ? Visibility.Visible : Visibility.Collapsed;
            _lock?.Refresh();
        }

        /// <summary>Padlock in its own little window: clickable even while this window is locked, toggles the lock both ways.</summary>
        private LockOverlay? _lock;

        public void SetClickThrough(bool on)
        {
            ClickThrough = on;
            if (Handle != IntPtr.Zero) Native.SetClickThrough(Handle, on);
            RefreshBadges();
            App.Current.RefreshSettingsWindow();
        }

        public void RegionChanged() { _fired.Clear(); UpdateView(); }

        private List<int> Leads()
        {
            var l = new List<int> { AlertTypes.ClampMinutes(S.WarnMinutes) };
            if (S.AlertAtStart) l.Add(0);
            return l;
        }

        private bool Include(EventDef e) => AlertTypes.Enabled(S.AlertChoices, e);

        private void UpdateView()
        {
            try
            {
                var now = DateTime.UtcNow;
                var region = Region;
                TitleText.Text = $"Abyss alerts · {region.Name} · times in {TimeZones.LocalAbbrev(now)}";
                TitleText.ToolTip = $"Schedule {App.Schedule.Current.Version} ({App.Schedule.SourceLabel}). Drag to move, grip = width, right-click for options.";
                var occ = ScheduleEngine.Occurrences(App.Schedule.Current, region, Include, now);
                var (mode, lines) = AbyssDisplay.Build(occ, now);
                if (lines.Count == 0)
                {
                    SetMode("none");
                    Line1.Text = "No upcoming Abyss events in the schedule";
                    SetExtra(Line2, null); SetExtra(Line3, null);
                    Extra.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SetMode(mode);
                    Line1.Text = lines[0].Text;
                    SetExtra(Line2, lines.Count >= 2 ? lines[1] : null);
                    SetExtra(Line3, lines.Count >= 3 ? lines[2] : null);
                    Extra.Visibility = lines.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
                }
                var due = AlertPlanner.Due(occ, now, Leads(), _fired);
                if (due.Count > 0)
                {
                    StartFlash();
                    Speak(due.SelectMany((d, i) => (i > 0 ? new[] { "|" } : Array.Empty<string>())
                        .Concat(new[] { d.Occ.Event.Voice ?? "generic", d.LeadMinutes == 0 ? "now" : "in_" + d.LeadMinutes })));
                }
            }
            catch (Exception ex) { Log.Write("update: " + ex); }
        }

        private void SetExtra(TextBlock tb, DisplayLine? line)
        {
            if (line == null) { tb.Visibility = Visibility.Collapsed; return; }
            tb.Foreground = line.Kind == "soon" ? _extraSoon : _mode == "soon" && line.Kind == "normal" ? Th.Br("soonInsetText") : ExtraLook(line.Kind);
            tb.FontWeight = line.Kind == "normal" ? FontWeights.Normal : FontWeights.SemiBold;
            tb.Text = line.Text;
            tb.Visibility = Visibility.Visible;
        }

        private void SetMode(string mode)
        {
            if (mode == _mode) return;
            var b = mode == "soon" ? _soon : ModeLook(mode);
            if (!_flashing) Panel.Background = b.Bg;
            Panel.BorderBrush = b.Bd;
            // on the warning panel the title and × switch to readable tokens (white on light/crimson themes)
            TitleText.SetResourceReference(TextBlock.ForegroundProperty, mode == "soon" ? "Th.soonTitle" : "Th.title");
            CloseBtn.SetResourceReference(Control.ForegroundProperty, mode == "soon" ? "Th.soonIcon" : "Th.iconStroke");
            Line1.Foreground = b.L1;
            Extra.Background = mode == "soon" ? Th.Br("soonInsetBg") : ExtraClear;
            Extra.Padding = mode == "soon" ? new Thickness(5, 1, 5, 2) : new Thickness(0);
            _mode = mode;
        }

        /// <summary>Theme switched: repaint the current mode (XAML parts follow on their own).</summary>
        public void ThemeChanged()
        {
            var m = string.IsNullOrEmpty(_mode) ? "normal" : _mode;
            _mode = "";
            SetMode(m);
            UpdateView();
        }

        private void StartFlash() { _flashing = true; _flashTicks = 0; _flash.Stop(); _flash.Start(); }

        private void Flash_Tick(object? sender, EventArgs e)
        {
            _flashTicks++;
            if (_flashTicks >= 40)
            {
                _flash.Stop(); _flashing = false;
                var m = string.IsNullOrEmpty(_mode) ? "normal" : _mode; _mode = "";
                SetMode(m);
                return;
            }
            Panel.Background = _flashTicks % 2 == 1 ? _flashOn : Th.Br("panelBg");
        }

        private void Speak(IEnumerable<string> keys) { if (!S.Muted) Voice.Play(keys); }

        private void Home_Click(object sender, RoutedEventArgs e) => App.Current.ToggleLauncherFromMenu();


        private void Close_Click(object sender, RoutedEventArgs e) => App.Current.CloseAbyss();

        public void TestAlert()
        {
            StartFlash();
            var lead = AlertTypes.ClampMinutes(S.WarnMinutes);
            Speak(new[] { "test", "|", "siege", lead == 0 ? "now" : "in_" + lead });
        }

        // ------------------------------------------------------------ right-click menu
        private MenuItem Item(string header, Action click, bool isChecked = false)
        {
            var mi = new MenuItem { Header = header, IsChecked = isChecked };
            mi.Click += (s, e) => { e.Handled = true; click(); };
            return mi;
        }

        private Separator Sep() => new() { Style = (Style)FindResource("DarkSep") };

        private void BuildMenu()
        {
            Menu.Items.Clear();
            var region = new MenuItem { Header = "Region" };
            foreach (var r in App.Schedule.Current.Regions)
            {
                var id = r.Id;
                region.Items.Add(Item(r.Name, () => { S.Region = id; App.Current.RegionChanged(); }, S.Region == id));
            }
            Menu.Items.Add(region);

            var warn = new MenuItem { Header = $"Warn me before ({AlertTypes.ClampMinutes(S.WarnMinutes)} min)" };
            foreach (var m in AlertPlanner.LeadOptions)
            {
                var min = m;
                warn.Items.Add(Item(min == 60 ? "1 hour" : min == 1 ? "1 minute" : $"{min} minutes", () => { S.WarnMinutes = min; S.Save(); },
                    AlertTypes.ClampMinutes(S.WarnMinutes) == min));
            }
            warn.Items.Add(new MenuItem { Header = "Any other time: Settings", IsEnabled = false });
            warn.Items.Add(Sep());
            warn.Items.Add(Item("When it starts", () => { S.AlertAtStart = !S.AlertAtStart; S.Save(); }, S.AlertAtStart));
            Menu.Items.Add(warn);

            var types = new MenuItem { Header = "Alerts" };
            foreach (var (group, items) in AlertTypes.Grouped(App.Schedule.Current))
            {
                if (types.Items.Count > 0) types.Items.Add(Sep());
                types.Items.Add(new MenuItem { Header = group, IsEnabled = false, FontWeight = FontWeights.SemiBold });
                foreach (var t in items)
                {
                var ev = t.Event; var key = t.Key;
                types.Items.Add(Item(t.Label, () => { S.AlertChoices[key] = !AlertTypes.Enabled(S.AlertChoices, ev); S.Save(); UpdateView(); },
                    AlertTypes.Enabled(S.AlertChoices, ev)));
                }
            }
            Menu.Items.Add(types);
            Menu.Items.Add(Sep());
            Menu.Items.Add(Item("Test alert", TestAlert));
            Menu.Items.Add(Item("Mute voice", () => App.Current.SetMuted(!S.Muted), S.Muted));
            Menu.Items.Add(Item("Lock window (unlock with the padlock, the tray or the hotkey)", () => SetClickThrough(!ClickThrough), ClickThrough));
            var op = new MenuItem { Header = "Opacity (all windows)" };
            foreach (var p in new[] { 100, 90, 80, 70, 60, 50, 40 })
            {
                var v = p / 100.0;
                op.Items.Add(Item(p + "%", () => App.Current.SetOpacity(v), Math.Abs(S.WindowOpacity - v) < 0.005));
            }
            Menu.Items.Add(op);
            Menu.Items.Add(Item("Reset position / size", ResetLayout));
            Menu.Items.Add(Sep());
            Menu.Items.Add(Item("Open launcher", () => App.Current.ShowLauncher()));
            Menu.Items.Add(Item("Support this app \u2764\uFE0F", App.OpenSupport));
            Menu.Items.Add(Item("Hide alerts", () => App.Current.HideAbyss()));
            Menu.Items.Add(Item("Exit Wingmate", () => App.Current.ExitApp()));
        }
    }
}
