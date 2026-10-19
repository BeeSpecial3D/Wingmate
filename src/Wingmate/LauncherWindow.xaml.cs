using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Wingmate
{
    public partial class LauncherWindow : Window
    {
        private static readonly Brush BadgeOn = Make("#CC3F7FD0"), TextOn = Make("#FFFFFFFF"); // OPEN badge: same in every theme
        private static Brush BadgeOff => Th.Br("badgeBg");
        private static Brush TextOff => Th.Br("muted");
        private static Brush Make(string hex) { var b = (Brush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }

        public LauncherWindow()
        {
            InitializeComponent();
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{v?.Major}.{v?.Minor}.{v?.Build} · schedule {App.Schedule.Current.Version}";
            foreach (var r in App.Schedule.Current.Regions)
            {
                var rb = new RadioButton
                {
                    Content = r.Name, GroupName = "region", Style = (Style)FindResource("Pill"),
                    IsChecked = r.Id == App.Settings.Region, Tag = r.Id,
                };
                rb.Checked += (s, e) => { App.Settings.Region = (string)((RadioButton)s).Tag; App.Current.RegionChanged(); };
                RegionPanel.Children.Add(rb);
            }
            Panel.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            Loaded += (s, e) => FontScaler.Apply(this);
            Opacity = App.Settings.WindowOpacity;
            Panel.ContextMenuOpening += (s, e) => BuildMenu();
            RefreshState();
            SyncFromSettings();
            RefreshCharacter();
            // Alt+F4 / taskbar close = the × button (ask, then quit everything); the app's own shutdown passes through
            Closing += (s, e) => { if (App.Current.Exiting) return; e.Cancel = true; Dispatcher.BeginInvoke(new Action(() => App.Current.RequestQuit(this))); };
        }

        private void BuildMenu()
        {
            Menu.Items.Clear();
            var op = new MenuItem { Header = "Opacity (all windows)" };
            foreach (var p in new[] { 100, 90, 80, 70, 60, 50, 40 })
            {
                var v = p / 100.0;
                var mi = new MenuItem { Header = p + "%", IsChecked = System.Math.Abs(App.Settings.WindowOpacity - v) < 0.005 };
                mi.Click += (s, e) => App.Current.SetOpacity(v);
                op.Items.Add(mi);
            }
            Menu.Items.Add(op);
            var am = new MenuItem { Header = "Auto-minimize after opening", IsChecked = App.Settings.LauncherAutoMinimize };
            am.Click += (s, e) => { App.Settings.LauncherAutoMinimize = !App.Settings.LauncherAutoMinimize; App.Settings.Save(); };
            Menu.Items.Add(am);
        }

        /// <summary>Keeps the region pills in step with the setting (changed from Settings or the alerts menu).</summary>
        public void SyncFromSettings()
        {
            AutoMinBox.IsChecked = App.Settings.LauncherAutoMinimize;
            foreach (var rb in RegionPanel.Children.OfType<RadioButton>())
            {
                bool on = (string)rb.Tag == App.Settings.Region;
                if (rb.IsChecked != on) rb.IsChecked = on;
            }
        }

        private void AutoMin_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.LauncherAutoMinimize = AutoMinBox.IsChecked == true;
            App.Settings.Save();
        }

        // ------------------------------------------------------------ characters
        public void RefreshCharacter()
        {
            var c = App.Book.Current;
            CharName.Text = c.Name;
        }

        private void CharName_Click(object sender, RoutedEventArgs e)
        {
            var m = new ContextMenu { PlacementTarget = CharBtn, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            MenuItem Mi(string header, Action a, bool isChecked = false, bool enabled = true)
            {
                var mi = new MenuItem { Header = header, IsChecked = isChecked, IsEnabled = enabled };
                mi.Click += (s2, e2) => { e2.Handled = true; a(); };
                return mi;
            }
            var cur = App.Book.Current;
            foreach (var c in App.Book.Characters.ToList())
            {
                var id = c.Id;
                m.Items.Add(Mi(c.Name, () => App.Current.SelectCharacter(id), c.Id == cur.Id));
            }
            m.Items.Add(new Separator { Style = (Style)FindResource("DarkSep") });
            m.Items.Add(Mi("Add character", () => App.Current.AddCharacter()));
            m.Items.Add(Mi("Rename\u2026", () => App.Current.RenameCharacter()));
            if (App.Book.Characters.Count > 1)
                m.Items.Add(Mi($"Remove {cur.Name}\u2026", () => App.Current.RemoveCharacter(cur.Id, this)));
            m.IsOpen = true;
        }

        private void Gear_Click(object sender, RoutedEventArgs e) => App.Current.ToggleSettings();

        private void Project_Click(object sender, RoutedEventArgs e) => App.OpenUrl(App.ProjectUrl);

        /// <summary>– hides only the launcher (tray icon, alerts and checklist keep running; the tray brings it back).</summary>
        private void Min_Click(object sender, RoutedEventArgs e) => Hide();

        /// <summary>Shows whether the alerts window is open (called whenever it opens or hides).</summary>
        public void RefreshState()
        {
            bool a = App.Current.AbyssOpen, c = App.Current.ChecklistOpen, both = a && c;
            BtnAbyss.Tag = a ? "on" : null;
            AbyssBadgeText.Text = a ? "OPEN" : "CLOSED";
            AbyssBadgeText.Foreground = a ? TextOn : TextOff;
            AbyssBadge.Background = a ? BadgeOn : BadgeOff;
            AbyssSub.Text = a ? "Showing now. Click to close it." : "Countdown overlay with voice warnings";
            BtnAbyss.ToolTip = a ? "Close the Abyss alerts" : "Open the Abyss alerts";
            BtnChecklist.Tag = c ? "on" : null;
            ChecklistBadgeText.Text = c ? "OPEN" : "CLOSED";
            ChecklistBadgeText.Foreground = c ? TextOn : TextOff;
            ChecklistBadge.Background = c ? BadgeOn : BadgeOff;
            ChecklistSub.Text = c ? "Showing now. Click to close it." : "Dailies, weeklies and your own lists";
            BtnChecklist.ToolTip = c ? "Close the checklist" : "Open the checklist";
            BtnBoth.Tag = both ? "on" : null;
            BothText.Text = both ? "Close both" : "Open both";
        }

        private void ShowStatus(string text) { Status.Text = text; Status.Visibility = Visibility.Visible; }

        private void BtnAbyss_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.AbyssOpen) App.Current.HideAbyss();
            else { App.Current.ShowAbyss(); AutoHide(); }
        }

        private void BtnChecklist_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.ChecklistOpen) App.Current.HideChecklist();
            else { App.Current.ShowChecklist(); AutoHide(); }
        }

        private void BtnBoth_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.AbyssOpen && App.Current.ChecklistOpen) { App.Current.HideAbyss(); App.Current.HideChecklist(); }
            else { App.Current.ShowAbyss(); App.Current.ShowChecklist(); AutoHide(); }
        }

        /// <summary>After opening a window, tuck the launcher into the tray (it still counts as open).</summary>
        private void AutoHide()
        {
            if (App.Settings.LauncherAutoMinimize) Hide();
        }

        /// <summary>× quits Wingmate (after the confirm, unless "Don't ask again" was ticked).</summary>
        private void Close_Click(object sender, RoutedEventArgs e) => App.Current.RequestQuit(this);

        private void Support_Click(object sender, RoutedEventArgs e) => App.OpenSupport();

        private void Update_Click(object sender, RoutedEventArgs e) => App.Current.RestartToUpdate();

        /// <summary>Shows "Update ready · Restart" while a downloaded update waits (null hides it).</summary>
        public void SetUpdateReady(string? version)
        {
            UpdateLink.Visibility = version == null ? Visibility.Collapsed : Visibility.Visible;
            UpdateLink.ToolTip = version == null ? null : $"Wingmate {version} is downloaded. Restart now to update, or it installs the next time Wingmate starts.";
        }
    }
}
