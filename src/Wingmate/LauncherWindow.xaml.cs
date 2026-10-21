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
            App.SetVersionLink(VersionText, $"v{v?.Major}.{v?.Minor}.{v?.Build}", suffix: $" · schedule {App.Schedule.Current.Version}");
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
            ApplyMinWidth();
            RestoreSize();
            WindowSnap.TryRestore(this, App.Settings.LauncherLeft, App.Settings.LauncherTop); // else centred (first run)
            Panel.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); SavePosition(); } };
            WindowSnap.Attach(this, SavePosition);
            EdgeRight.DragDelta += (s, e) => ResizeBy(e.HorizontalChange, 0);
            EdgeBottom.DragDelta += (s, e) => ResizeBy(0, e.VerticalChange);
            ResizeGrip.DragDelta += (s, e) => ResizeBy(e.HorizontalChange, e.VerticalChange);
            foreach (var t in new[] { EdgeRight, EdgeBottom, ResizeGrip }) t.DragCompleted += (s, e) => { WindowSnap.Clamp(this, true); SavePosition(); };
            LayoutUpdated += (s, e) => KeepMinHeight();
            Loaded += (s, e) => FontScaler.Apply(this);
            Opacity = App.Settings.WindowOpacity;
            Panel.ContextMenuOpening += (s, e) => BuildMenu();
            RefreshState();
            SyncFromSettings();
            RefreshCharacter();
            Loaded += (s, e) => { if (NamePrompt.Visibility == Visibility.Visible) Dispatcher.BeginInvoke(new Action(() => NameBox.Focus()), System.Windows.Threading.DispatcherPriority.Input); };
            // Alt+F4 / taskbar close = the × button (ask, then quit everything); the app's own shutdown passes through
            Closing += (s, e) => { if (App.Current.Exiting) return; e.Cancel = true; Dispatcher.BeginInvoke(new Action(() => App.Current.RequestQuit(this))); };
        }

        /// <summary>Remembers where the launcher is and its size, so it reopens that way (only saves when something changed).
        /// Height is only saved once the user has made it taller than its content (else it keeps fitting the content).</summary>
        private void SavePosition()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top) || WindowState != WindowState.Normal) return;
            var s = App.Settings;
            double l = Math.Round(Left), t = Math.Round(Top);
            double? w = ActualWidth > 0 ? Math.Round(ActualWidth) : s.LauncherWidth;
            double? h = SizeToContent == SizeToContent.Manual && ActualHeight > 0 ? Math.Round(ActualHeight) : null;
            if (s.LauncherLeft == l && s.LauncherTop == t && s.LauncherWidth == w && s.LauncherHeight == h) return;
            s.LauncherLeft = l; s.LauncherTop = t; s.LauncherWidth = w; s.LauncherHeight = h;
            s.Save();
        }

        public const double DefaultWidth = 340;

        /// <summary>Minimum width for the text size: 340 up to Medium, wider for Large / X-Large so the header, footer and
        /// window buttons never clip (measured: X-Large needs about 386).</summary>
        public static double MinWidthFor(double fontScale) => Math.Ceiling(DefaultWidth + Math.Max(0, fontScale - 1) * 260);

        public void ApplyMinWidth() => MinWidth = MinWidthFor(FontScaler.Scale);

        /// <summary>Saved size, kept within the screen (never smaller than the minimum; height never below the content).</summary>
        private void RestoreSize()
        {
            var s = App.Settings;
            if (s.LauncherWidth is double w && double.IsFinite(w))
                Width = Math.Clamp(w, MinWidth, Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
            if (s.LauncherHeight is double h && double.IsFinite(h) && h > 0)
            {
                SizeToContent = SizeToContent.Manual;
                Height = Math.Min(h, SystemParameters.WorkArea.Height); // KeepMinHeight raises it to the content if needed
            }
        }

        private void ResizeBy(double dx, double dy)
        {
            if (Math.Abs(dx) > 0.1) Width = Math.Clamp(ActualWidth + dx, MinWidth, Math.Max(MinWidth, SystemParameters.VirtualScreenWidth));
            if (Math.Abs(dy) > 0.1)
            {
                if (SizeToContent != SizeToContent.Manual) { Height = ActualHeight; SizeToContent = SizeToContent.Manual; }
                Height = Math.Clamp(ActualHeight + dy, MinHeight, Math.Max(MinHeight, SystemParameters.VirtualScreenHeight));
            }
        }

        /// <summary>The launcher can't be shorter than its content (text size, name box, update link): extra height is
        /// empty space at the bottom, never clipped content.</summary>
        private void KeepMinHeight()
        {
            if (Body.DesiredSize.Height <= 0) return;
            var pad = Panel.Padding; var bd = Panel.BorderThickness;
            double need = Math.Ceiling(Body.DesiredSize.Height + pad.Top + pad.Bottom + bd.Top + bd.Bottom);
            if (Math.Abs(MinHeight - need) > 0.5) MinHeight = need;
        }

        /// <summary>"Reset window positions": default width, height fits the content again.</summary>
        public void ResetSize()
        {
            Width = DefaultWidth;
            SizeToContent = SizeToContent.Height;
            UpdateLayout();
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
        }

        /// <summary>Keeps the region pills in step with the setting (changed from Settings or the alerts menu).</summary>
        public void SyncFromSettings()
        {
            foreach (var rb in RegionPanel.Children.OfType<RadioButton>())
            {
                bool on = (string)rb.Tag == App.Settings.Region;
                if (rb.IsChecked != on) rb.IsChecked = on;
            }
        }

        // ------------------------------------------------------------ characters
        public void RefreshCharacter()
        {
            var c = App.Book.Current;
            CharName.Text = c.Name;
            bool hasClass = Core.CharacterClasses.IsKnown(c.Class);
            ClassText.Text = hasClass ? Core.CharacterClasses.Normalize(c.Class) : "Class";
            ClassBtn.ToolTip = hasClass ? $"{c.Name}'s class: {ClassText.Text}. Click to change it." : $"Pick {c.Name}'s class (used by Guide)";
            GuideSub.Text = hasClass ? $"{ClassText.Text} guide on aion2-guides.info" : "Class guide on aion2-guides.info";
            bool ask = App.Book.NeedsName;
            NamePrompt.Visibility = ask ? Visibility.Visible : Visibility.Collapsed;
            CharRow.Visibility = ask ? Visibility.Collapsed : Visibility.Visible;
            RefreshState();
        }

        /// <summary>Puts the cursor in the name box (e.g. after trying to open the checklist without a name).</summary>
        public void FocusNameBox()
        {
            if (NamePrompt.Visibility != Visibility.Visible) return;
            Activate();
            NameBox.Focus();
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NameHint.Visibility = NameBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            NameSave.IsEnabled = !Core.ChecklistBook.IsPlaceholderName(NameBox.Text);
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return) return;
            e.Handled = true;
            SaveName();
        }

        private void NameSave_Click(object sender, RoutedEventArgs e) => SaveName();

        /// <summary>Class picked in the first-run box; saved together with the name.</summary>
        private string _firstClass = "";

        private void SaveName()
        {
            if (!App.Current.SaveFirstName(NameBox.Text, _firstClass)) { NameBox.Focus(); NameBox.SelectAll(); }
        }

        // ------------------------------------------------------------ class
        /// <summary>The class dropdown: one checked entry per class, plus "No class" when one is set. Follows the text size.</summary>
        private ContextMenu ClassMenu(UIElement target, string current, Action<string> pick, string? header = null)
        {
            var m = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            if (header != null) m.Items.Add(new MenuItem { Header = header, IsEnabled = false });
            foreach (var name in Core.CharacterClasses.Names)
            {
                var n = name;
                var mi = new MenuItem { Header = n, IsChecked = n == current };
                mi.Click += (s2, e2) => { e2.Handled = true; pick(n); };
                m.Items.Add(mi);
            }
            if (current.Length > 0)
            {
                m.Items.Add(new Separator { Style = (Style)FindResource("DarkSep") });
                var none = new MenuItem { Header = "No class" };
                none.Click += (s2, e2) => { e2.Handled = true; pick(""); };
                m.Items.Add(none);
            }
            m.Opened += (s2, e2) => FontScaler.Apply(m);
            return m;
        }

        private void FirstClass_Click(object sender, RoutedEventArgs e)
        {
            ClassMenu(FirstClassBtn, _firstClass, n =>
            {
                _firstClass = n;
                FirstClassText.Text = n.Length > 0 ? n : "Class (optional)";
            }).IsOpen = true;
        }

        private void Class_Click(object sender, RoutedEventArgs e)
        {
            ClassMenu(ClassBtn, Core.CharacterClasses.Normalize(App.Book.Current.Class), n => App.Current.SetCharacterClass(n)).IsOpen = true;
        }

        /// <summary>Guide with no class yet: pick it first (saved), then the guide opens.</summary>
        public void AskClassForGuide()
        {
            if (CharRow.Visibility != Visibility.Visible) return;
            Activate();
            ClassMenu(BtnGuide, "", n =>
            {
                App.Current.SetCharacterClass(n);
                if (Core.CharacterClasses.IsKnown(App.Book.Current.Class)) App.Current.OpenClassGuide();
            }, header: "Pick your class first").IsOpen = true;
        }

        private void BtnGuide_Click(object sender, RoutedEventArgs e) => App.Current.OpenClassGuide();

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
            var cls = new MenuItem { Header = "Class" };
            foreach (var entry in Core.CharacterClasses.MenuEntries(cur.Class))
            {
                var v = entry.Value;
                if (entry.SeparatorBefore) cls.Items.Add(new Separator { Style = (Style)FindResource("DarkSep") });
                cls.Items.Add(Mi(entry.Label, () => App.Current.SetCharacterClass(v), entry.IsChecked));
            }
            m.Items.Add(cls);
            if (App.Book.Characters.Count > 1)
                m.Items.Add(Mi($"Remove {cur.Name}\u2026", () => App.Current.RemoveCharacter(cur.Id, this)));
            m.Opened += (s2, e2) => FontScaler.Apply(m);
            m.IsOpen = true;
        }

        private void Gear_Click(object sender, RoutedEventArgs e) => App.Current.ToggleSettings();

        private void Project_Click(object sender, RoutedEventArgs e) => App.OpenUrl(App.ProjectUrl);

        /// <summary>– hides only the launcher (tray icon, alerts and checklist keep running; the tray brings it back).</summary>
        private void Min_Click(object sender, RoutedEventArgs e) => Hide();

        /// <summary>Shows whether the alerts window is open (called whenever it opens or hides).</summary>
        public void RefreshState()
        {
            bool a = App.Current.AbyssOpen, c = App.Current.ChecklistOpen;
            BtnAbyss.Tag = a ? "on" : null;
            AbyssBadgeText.Text = a ? "OPEN" : "CLOSED";
            AbyssBadgeText.Foreground = a ? TextOn : TextOff;
            AbyssBadge.Background = a ? BadgeOn : BadgeOff;
            AbyssSub.Text = a ? "Showing now. Click to close it." : "Countdown overlay with voice warnings";
            BtnAbyss.ToolTip = a ? "Close the Abyss alerts" : "Open the Abyss alerts";
            bool locked = App.Book.NeedsName;
            BtnChecklist.IsEnabled = !locked;
            BtnChecklist.Opacity = locked ? 0.45 : 1.0;
            BtnChecklist.Tag = c ? "on" : null;
            ChecklistBadgeText.Text = c ? "OPEN" : "CLOSED";
            ChecklistBadgeText.Foreground = c ? TextOn : TextOff;
            ChecklistBadge.Background = c ? BadgeOn : BadgeOff;
            ChecklistSub.Text = c ? "Showing now. Click to close it." : "Dailies, weeklies and your own lists";
            BtnChecklist.ToolTip = c ? "Close the checklist" : "Open the checklist";
            if (locked) { ChecklistSub.Text = "Enter your character name first"; BtnChecklist.ToolTip = "Enter your character name first (above)"; }
        }

        private void ShowStatus(string text) { Status.Text = text; Status.Visibility = Visibility.Visible; }

        private void BtnAbyss_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.AbyssOpen) App.Current.HideAbyss();
            else App.Current.ShowAbyss(); // the launcher stays open (no auto-minimize since 1.0.3)
        }

        private void BtnChecklist_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.ChecklistOpen) App.Current.HideChecklist();
            else App.Current.ShowChecklist();
        }

        /// <summary>× quits Wingmate (after the confirm, unless "Don't ask again" was ticked).</summary>
        private void Close_Click(object sender, RoutedEventArgs e) => App.Current.RequestQuit(this);

        private void Support_Click(object sender, RoutedEventArgs e) => App.OpenSupport();

        private void Update_Click(object sender, RoutedEventArgs e) => App.Current.UpdateFromLink();

        /// <summary>"Update available · v1.0.3" (click downloads and installs) or "Update ready · Restart" (downloaded); null hides it.</summary>
        public void SetUpdateOffer(string? version, bool downloaded)
        {
            UpdateLink.Visibility = version == null ? Visibility.Collapsed : Visibility.Visible;
            UpdateLink.Content = downloaded ? "Update ready \u00B7 Restart" : $"Update available \u00B7 v{version}";
            UpdateLink.ToolTip = version == null ? null : downloaded
                ? $"Wingmate {version} is downloaded. Click to choose Update now (restarts) or Later; otherwise it installs the next time Wingmate starts."
                : $"Wingmate {version} is available. Click to choose Update now or Later (Update now downloads it and restarts Wingmate; your lists and settings stay).";
        }
    }
}
