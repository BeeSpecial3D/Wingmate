using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wingmate.Core;

namespace Wingmate
{
    public partial class ChecklistWindow : Window
    {
        private const double DefaultWidth = 340, MinW = 240;
        /// <summary>The current character's checklist (shared book, owned by App).</summary>
        public ChecklistState State => App.Book.Current;
        private const double MinH = 120;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
        private readonly Random _rng = new();
        private int _running;          // celebrations in progress
        private bool _pendingRebuild;
        private int _editing;          // inline edits in progress (window may take focus while > 0)
        private AppSettings S => App.Settings;

        public ChecklistWindow()
        {
            InitializeComponent();
            _lock = new LockOverlay(this, LockSlot, () => ClickThrough, () => SetClickThrough(!ClickThrough), "checklist");
            InitLockedInput();
            foreach (var sec in State.Sections) if (sec.IsComplete) sec.Hidden = true; // finished before last close
            CheckResets(rebuild: false);
            Width = Math.Clamp(S.ChecklistWidth, MinW, SystemParameters.VirtualScreenWidth);
            if (S.ChecklistHeight is double hgt && hgt >= MinH) { SizeToContent = SizeToContent.Manual; Height = Math.Min(hgt, SystemParameters.WorkArea.Height); }
            Opacity = S.WindowOpacity;
            MaxHeight = SystemParameters.WorkArea.Height * 0.9;
            Place();

            TitleText.Text = DisplayTitle;
            RefreshSwap();
            SwapBtn.ContextMenuOpening += (s, e) => BuildSwapMenu();
            SourceInitialized += (s, e) => { if (Handle != IntPtr.Zero) { Native.OverlayStyles(Handle); if (ClickThrough) Native.SetClickThrough(Handle, true); } };
            MenuFocus.Attach(this, () => IsEditing);
            Deactivated += (s, e) => CommitActiveEdit();
            Panel.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState != MouseButtonState.Pressed) return;
                if (CommitActiveEdit()) return; // first click elsewhere just finishes the edit
                try { DragMove(); } catch { }
                WindowSnap.Clamp(this);
                SaveLayout();
            };
            ResizeGrip.DragDelta += (s, e) =>
            {
                Width = Math.Clamp(ActualWidth + e.HorizontalChange, MinW, SystemParameters.VirtualScreenWidth);
                if (Math.Abs(e.VerticalChange) > 0.5)
                {
                    if (SizeToContent != SizeToContent.Manual) { Height = ActualHeight; SizeToContent = SizeToContent.Manual; }
                    Height = Math.Clamp(ActualHeight + e.VerticalChange, MinH, SystemParameters.VirtualScreenHeight);
                }
            };
            ResizeGrip.DragCompleted += (s, e) => { WindowSnap.Clamp(this, true); SaveLayout(); };
            WindowSnap.Attach(this, SaveLayout);
            Loaded += (s, e) => FontScaler.Apply(this);
            Panel.ContextMenuOpening += (s, e) => { CommitActiveEdit(); BuildMenu(); };
            _timer.Tick += (s, e) => { CheckResets(rebuild: true); UpdateResetText(); if (IsVisible && Handle != IntPtr.Zero && _editing == 0) Native.KeepTopmost(Handle); };
            IsVisibleChanged += (s, e) => { if (IsVisible) { CheckResets(true); _timer.Start(); } else { _timer.Stop(); SaveLayout(); } };
            Closing += (s, e) => { e.Cancel = true; App.Current.HideChecklist(); };
            Rebuild();
            RefreshBadges();
        }

        private IntPtr Handle => new WindowInteropHelper(this).Handle;

        private void Place()
        {
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
            if (S.ChecklistLeft is double l && S.ChecklistTop is double t && l >= vl - 200 && l <= vr - 50 && t >= vt && t <= vb - 30)
            { Left = l; Top = t; return; }
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 20; Top = wa.Top + 80;
        }

        public void SaveLayout()
        {
            if (!double.IsNaN(Left)) S.ChecklistLeft = Math.Round(Left);
            if (!double.IsNaN(Top)) S.ChecklistTop = Math.Round(Top);
            if (!double.IsNaN(Width)) S.ChecklistWidth = Math.Round(Width);
            S.ChecklistHeight = SizeToContent == SizeToContent.Manual && ActualHeight > 0 ? Math.Round(ActualHeight) : null;
            S.Save();
        }

        public void Save() => App.SaveBook();

        /// <summary>Shows the current character (after a switch, add or remove): title, resets catch-up, lists.</summary>
        public void LoadCharacter()
        {
            CommitActiveEdit();
            if (_undoItemId != null || _undoAll != null) StatusTimer_Tick(null, EventArgs.Empty); // the undo line belongs to the previous character
            TitleText.Text = DisplayTitle;
            RefreshSwap();
            CheckResets(rebuild: false);
            RequestRebuild();
        }

        public void RefreshBadges()
        {
            MutedBadge.Visibility = S.Muted ? Visibility.Visible : Visibility.Collapsed;
            _lock?.Refresh();
        }

        /// <summary>Padlock in its own little window: clickable even while this window is locked, toggles the lock both ways.</summary>
        private LockOverlay? _lock;

        /// <summary>Clicks pass through to the game. Not saved (same as the alerts window); turn off from Settings, the tray or the hotkey.</summary>
        public bool ClickThrough { get; private set; }

        public void SetClickThrough(bool on)
        {
            if (on) CommitActiveEdit();
            ClickThrough = on;
            if (Handle != IntPtr.Zero) Native.SetClickThrough(Handle, on);
            ApplyLockedInput();
            RefreshBadges();
            App.Current.RefreshSettingsWindow();
        }

        // ------------------------------------------------------------ resets
        private (EventDef D, EventDef W, TimeZoneInfo Tz) ResetDefs()
        {
            var sched = App.Schedule.Current;
            var region = sched.Regions.FirstOrDefault(r => r.Id == S.Region) ?? sched.Regions[0];
            return (ResetLogic.Find(sched, "daily_reset"), ResetLogic.Find(sched, "weekly_reset"), TimeZones.Find(region.TimeZone, region.WindowsTimeZone));
        }

        public void CheckResets(bool rebuild)
        {
            try
            {
                var (d, w, tz) = ResetDefs();
                var now = DateTime.UtcNow;
                if (ResetLogic.Apply(State, ResetLogic.LastBoundary(d, tz, now), ResetLogic.LastBoundary(w, tz, now)))
                {
                    Save();
                    if (rebuild) RequestRebuild();
                }
            }
            catch (Exception ex) { Log.Write("resets: " + ex.Message); }
        }

        private string? _status;
        private DispatcherTimer? _statusTimer;

        /// <summary>Shows a short message in place of the reset line for a few seconds (e.g. after "Copy to").</summary>
        public void FlashStatus(string text, double seconds = 4)
        {
            if (string.IsNullOrEmpty(text)) return;
            ClearUndo();
            _status = text;
            ResetText.Text = text;
            ResetText.ToolTip = text;
            _statusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _statusTimer.Stop();
            _statusTimer.Interval = TimeSpan.FromSeconds(seconds);
            _statusTimer.Tick -= StatusTimer_Tick;
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
        }

        private void StatusTimer_Tick(object? sender, EventArgs e) { _statusTimer?.Stop(); _status = null; ClearUndo(); ResetText.ToolTip = null; UpdateResetText(); }

        // ------------------------------------------------------------ undo the last check
        private string? _undoItemId;

        /// <summary>An item was just checked and is disappearing: "Checked {item} · Undo" for a few seconds. A newer check replaces it.</summary>
        public void OfferUndo(ChecklistItem item)
        {
            FlashStatus(UndoCheck.Text(item.Text), UndoCheck.Seconds);
            ResetText.ToolTip = null;
            _undoItemId = item.Id;
            UndoBtn.Visibility = Visibility.Visible;
        }

        private CheckAll.Snapshot? _undoAll;

        /// <summary>"Check all and hide" (list right-click menu): checks every unchecked item, then the list finishes the normal way
        /// (celebration, then it hides; it comes back at its reset). "Checked all in {list} · Undo" puts it back in one go.</summary>
        public void CheckAllAndHide(SectionView view)
        {
            CommitActiveEdit();
            var sec = view.Section;
            if (view.Busy || !State.Sections.Contains(sec)) return;
            bool was = sec.IsComplete;
            var snap = CheckAll.Apply(sec);
            if (snap == null) return;
            view.RefreshRows();
            ItemToggled(view, was); // saves; plays the list celebration, which hides the list when it ends
            FlashStatus(CheckAll.Text(sec.Name), UndoCheck.Seconds);
            ResetText.ToolTip = null;
            _undoAll = snap;
            UndoBtn.Visibility = Visibility.Visible;
            Log.Write("check all and hide");
        }

        private void ClearUndo()
        {
            _undoItemId = null;
            _undoAll = null;
            UndoBtn.Visibility = Visibility.Collapsed;
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastCheck();

        /// <summary>Unchecks the item from the undo line and brings it back (after its warp-out ends, if it is still running).</summary>
        public void UndoLastCheck()
        {
            var id = _undoItemId; var all = _undoAll;
            _statusTimer?.Stop(); _status = null; ClearUndo(); UpdateResetText();
            if (all != null) { if (!CheckAll.Undo(State, all)) return; }
            else if (id == null || !UndoCheck.Undo(State, id)) return;
            Save();
            Log.Write("check undone");
            RequestRebuild();
        }

        /// <summary>"Copy to" from a list title menu.</summary>
        public void CopyList(ChecklistSection sec, System.Collections.Generic.IEnumerable<string> characterIds)
        {
            CommitActiveEdit();
            FlashStatus(App.Current.CopyListTo(sec, characterIds));
        }

        /// <summary>Theme switched: list rows are painted from the theme when built.</summary>
        public void ThemeChanged() => RequestRebuild();

        private void UpdateResetText()
        {
            if (_status != null) return;
            var (d, w, tz) = ResetDefs();
            var now = DateTime.UtcNow;
            ResetText.Text = $"Daily reset in {AbyssDisplay.Span(ResetLogic.NextBoundary(d, tz, now) - now)} · weekly in {AbyssDisplay.Span(ResetLogic.NextBoundary(w, tz, now) - now)}";
        }

        // ------------------------------------------------------------ building the list
        public void RequestRebuild()
        {
            if (_running > 0 || _editing > 0) { _pendingRebuild = true; return; }
            Rebuild();
        }

        public void Rebuild()
        {
            _pendingRebuild = false;
            SectionsHost.Children.Clear();
            foreach (var sec in State.Sections.Where(x => !x.Hidden))
                SectionsHost.Children.Add(new SectionView(this, sec));
            int hidden = State.Sections.Count(x => x.Hidden);
            HiddenBtn.Content = hidden == 1 ? "1 completed section hidden · show" : $"{hidden} completed sections hidden · show";
            HiddenBtn.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateResetText();
            FontScaler.Apply(SectionsHost);
        }

        // ------------------------------------------------------------ editing support
        private Action? _activeEdit; // commits the one open edit box (there is never more than one)
        public bool IsEditing => _activeEdit != null;

        /// <summary>Ends the open edit box (commit, or discard if empty). Returns true if there was one.</summary>
        public bool CommitActiveEdit()
        {
            var a = _activeEdit;
            if (a == null) return false;
            a();
            return true;
        }

        public void BeginEdit(Action commit)
        {
            CommitActiveEdit();
            _activeEdit = commit;
            _editing++;
            if (Handle != IntPtr.Zero) Native.SetNoActivate(Handle, false);
            Activate();
        }

        public void EndEdit()
        {
            _activeEdit = null;
            _editing = Math.Max(0, _editing - 1);
            if (_editing == 0 && Handle != IntPtr.Zero) Native.SetNoActivate(Handle, true);
            Save();
            if (_pendingRebuild) RequestRebuild();
        }

        /// <summary>The window title is the character's name.</summary>
        private string DisplayTitle => string.IsNullOrWhiteSpace(State.Name) ? ChecklistBook.DefaultName : State.Name.Trim();

        /// <summary>Inline rename of the window title, same rules as list renaming.</summary>
        public void StartTitleEdit()
        {
            var host = (Panel)TitleText.Parent;
            var original = DisplayTitle;
            var tb = new TextBox
            {
                Text = original, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TitleText.Foreground,
                Background = Th.Br("inputBg"), BorderBrush = TitleText.Foreground,
                BorderThickness = new Thickness(1), CaretBrush = Th.Br("caret"), Padding = new Thickness(2, 0, 2, 0), AcceptsReturn = false,
            };
            bool finished = false, typed = false;
            tb.TextChanged += (s, e) => typed = true;
            void Finish(bool escaped)
            {
                if (finished) return;
                finished = true;
                var t = EditLogic.ResolveName(original, tb.Text, escaped);
                State.Name = t; // renaming the title renames the character
                TitleText.Text = DisplayTitle;
                host.Children.Remove(tb);
                TitleText.Visibility = Visibility.Visible;
                EndEdit(); // saves
                App.Current.CharacterRenamed(entered: !escaped && typed);
            }
            BeginEdit(() => Finish(false)); // commits any other open edit first
            TitleText.Visibility = Visibility.Collapsed;
            host.Children.Insert(0, tb);
            FontScaler.Apply(tb);
            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { Finish(false); e.Handled = true; }
                else if (e.Key == Key.Escape) { Finish(true); e.Handled = true; }
            };
            tb.LostKeyboardFocus += (s, e) => Finish(false);
            Dispatcher.BeginInvoke(new Action(() => { if (!finished) { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); } }), System.Windows.Threading.DispatcherPriority.Input);
        }

        public void ItemToggled(SectionView view, bool wasComplete)
        {
            Save();
            if (!wasComplete && view.Section.IsComplete) Celebrate(view, preview: false);
        }

        public void Celebrate(SectionView view, bool preview)
        {
            string key;
            if (!preview && CelebrationLines.ShouldPlayAllDone(State, App.Uptime)) key = CelebrationLines.AllDoneKey; // that finished everything
            else
            {
                var pool = CelebrationLines.PoolFor(view.Section, AppSettings.LongListThreshold);
                key = CelebrationLines.ClipKey(pool, CelebrationLines.Pick(pool, _rng));
            }
            var dur = Voice.Duration(new[] { key });
            if (!S.Muted && S.CelebrationVoice) Voice.Play(new[] { key });
            _running++;
            view.RunCelebration(dur.TotalSeconds, WarpPicker.Next(_rng), _rng, () =>
            {
                _running--;
                if (!preview && view.Section.IsComplete) { view.Section.Hidden = true; Save(); }
                if (_running == 0) Rebuild(); else _pendingRebuild = true;
            });
        }

        /// <summary>An item warp-out is running: list rebuilds wait until it ends (same as during a celebration).</summary>
        public void BeginFx() => _running++;

        /// <summary>"Show checked items": checked items stay in view (green check, struck through) so a misclick can be undone.</summary>
        public void ToggleShowChecked()
        {
            CommitActiveEdit();
            S.ShowCheckedItems = !S.ShowCheckedItems;
            S.Save();
            RequestRebuild();
        }
        public void EndFx()
        {
            _running = Math.Max(0, _running - 1);
            if (_running == 0 && _pendingRebuild) Rebuild();
        }

        public void MoveItem(string itemId, ChecklistSection target, int index)
        {
            var src = State.Sections.FirstOrDefault(s => s.Items.Any(i => i.Id == itemId));
            if (src == null) return;
            if (!ListRules.CanMoveItem(src, target)) return; // nothing moves into or out of the Events list
            var item = src.Items.First(i => i.Id == itemId);
            int old = src.Items.IndexOf(item);
            src.Items.RemoveAt(old);
            if (src == target && old < index) index--;
            target.Items.Insert(Math.Clamp(index, 0, target.Items.Count), item);
            Save();
            RequestRebuild();
        }

        public void MoveSection(ChecklistSection sec, int delta)
        {
            int i = State.Sections.IndexOf(sec), j = i + delta;
            if (i < 0 || j < 0 || j >= State.Sections.Count) return;
            State.Sections.RemoveAt(i); State.Sections.Insert(j, sec);
            Save(); RequestRebuild();
        }

        public void DeleteSection(ChecklistSection sec)
        {
            if (!ListRules.CanDeleteList(sec)) return;
            if (!ConfirmDialog.Ask(this, $"Delete \"{sec.Name}\"?",
                    $"Delete the list \"{sec.Name}\" and its {sec.Items.Count} item{(sec.Items.Count == 1 ? "" : "s")}? This can't be undone.",
                    "Delete", ConfirmKind.Danger)) return;
            State.Sections.Remove(sec); Save(); RequestRebuild();
        }

        public void ResetToDefault(ChecklistSection sec)
        {
            if (!ListRules.CanResetToDefault(sec)) return;
            if (!ConfirmDialog.Ask(this, $"Reset {sec.Name}?",
                    $"Reset {sec.Name} to the default list? Your current items will be replaced. A backup is saved first.",
                    "Reset", ConfirmKind.Primary)) return;
            try
            {
                Save();
                File.Copy(ChecklistState.DefaultPath, Path.Combine(AppSettings.Dir, "checklist-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), true);
            }
            catch (Exception ex) { Log.Write("backup: " + ex.Message); }
            sec.Items = ChecklistState.DefaultSection(sec.Kind).Items;
            sec.Hidden = false;
            Save(); RequestRebuild();
        }

        private void NewList_Click(object sender, RoutedEventArgs e) => NewList();

        public void NewList()
        {
            CommitActiveEdit();
            var sec = new ChecklistSection { Kind = "custom", Name = "New list" };
            State.Sections.Add(sec);
            Save(); Rebuild();
            var view = SectionsHost.Children.OfType<SectionView>().LastOrDefault(v => v.Section == sec);
            view?.StartRename(isNew: true);
            Scroller.ScrollToEnd();
        }

        /// <summary>+ on a list: finish any open edit, then add a new row at the bottom of that list and edit it.</summary>
        public void AddItemTo(ChecklistSection sec)
        {
            CommitActiveEdit();
            if (!State.Sections.Contains(sec) || !ListRules.CanEditItems(sec)) return;
            var item = new ChecklistItem { Text = "" };
            sec.Items.Add(item);
            sec.Collapsed = false;
            Rebuild();
            ViewFor(sec)?.BeginItemEdit(item, isNew: true);
            if (sec == State.Sections.LastOrDefault(x => !x.Hidden)) Scroller.ScrollToEnd();
        }

        public void EditItem(ChecklistSection sec, ChecklistItem item)
        {
            CommitActiveEdit();
            if (!sec.Items.Contains(item) || !ListRules.CanEditItems(sec)) return;
            Rebuild();
            ViewFor(sec)?.BeginItemEdit(item, isNew: false);
        }

        public void RenameSection(ChecklistSection sec)
        {
            CommitActiveEdit();
            if (!State.Sections.Contains(sec) || !ListRules.CanRenameList(sec)) return;
            Rebuild();
            ViewFor(sec)?.StartRename(isNew: false);
        }

        private SectionView? ViewFor(ChecklistSection sec) => SectionsHost.Children.OfType<SectionView>().FirstOrDefault(v => v.Section == sec);

        private void ShowHidden_Click(object sender, RoutedEventArgs e) => ShowHiddenSections();

        public void ShowHiddenSections()
        {
            CommitActiveEdit();
            foreach (var s in State.Sections) s.Hidden = false;
            Save(); RequestRebuild();
        }

        /// <summary>Uncheck every item in every list and show hidden sections (no celebration: nothing completes).</summary>
        public void ResetAllLists()
        {
            CommitActiveEdit();
            State.ResetAll();
            Save(); RequestRebuild();
        }

        private SectionView? FirstPreviewable => SectionsHost.Children.OfType<SectionView>().FirstOrDefault(v => v.Section.LiveItems.Count > 0);
        public bool CanPreview => FirstPreviewable != null && _running == 0;
        public void PreviewCelebration() { var f = FirstPreviewable; if (f != null && !f.Busy && _running == 0) Celebrate(f, preview: true); }

        public void ResetLayout() { S.ChecklistLeft = null; S.ChecklistTop = null; Width = DefaultWidth; SizeToContent = SizeToContent.Height; Place(); SaveLayout(); }

        // ------------------------------------------------------------ character swap button
        private void RefreshSwap() => SwapBtn.IsEnabled = App.Book.Characters.Count > 1;

        private void Swap_Click(object sender, RoutedEventArgs e) { CommitActiveEdit(); App.Current.CycleCharacter(); }

        private void BuildSwapMenu()
        {
            CommitActiveEdit();
            SwapMenu.Items.Clear();
            var cur = App.Book.Current;
            foreach (var c in App.Book.Characters.ToList())
            {
                var id = c.Id;
                SwapMenu.Items.Add(Item(c.Name, () => App.Current.SelectCharacter(id), c.Id == cur.Id));
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e) => App.Current.ToggleLauncherFromMenu();

        private void Close_Click(object sender, RoutedEventArgs e) => App.Current.CloseChecklist();

        // ------------------------------------------------------------ window right-click menu
        private MenuItem Item(string header, Action click, bool isChecked = false, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsChecked = isChecked, IsEnabled = enabled };
            mi.Click += (s, e) => { e.Handled = true; click(); };
            return mi;
        }

        private Separator Sep() => new() { Style = (Style)FindResource("DarkSep") };

        private void BuildMenu()
        {
            Menu.Items.Clear();
            Menu.Items.Add(Item("+ New list", NewList));
            Menu.Items.Add(Item("Rename title", StartTitleEdit));
            var cls = new MenuItem { Header = "Class" };
            foreach (var entry in CharacterClasses.MenuEntries(State.Class))
            {
                var v = entry.Value;
                if (entry.SeparatorBefore) cls.Items.Add(Sep());
                cls.Items.Add(Item(entry.Label, () => App.Current.SetCharacterClass(v), entry.IsChecked));
            }
            Menu.Items.Add(cls);
            int hidden = State.Sections.Count(x => x.Hidden);
            Menu.Items.Add(Item($"Show hidden sections ({hidden})", () => ShowHidden_Click(this, new RoutedEventArgs()), enabled: hidden > 0));
            Menu.Items.Add(Item("Show checked items", ToggleShowChecked, S.ShowCheckedItems));
            Menu.Items.Add(Item("Reset all lists\u2026", () => App.Current.ResetAllLists(this)));
            Menu.Items.Add(Item("Preview celebration", PreviewCelebration, enabled: CanPreview));
            Menu.Items.Add(Sep());
            Menu.Items.Add(Item("Celebration voice", () => { S.CelebrationVoice = !S.CelebrationVoice; S.Save(); }, S.CelebrationVoice));
            Menu.Items.Add(Item("Mute voice", () => App.Current.SetMuted(!S.Muted), S.Muted));
            var op = new MenuItem { Header = "Opacity (all windows)" };
            foreach (var p in new[] { 100, 90, 80, 70, 60, 50, 40 })
            {
                var v = p / 100.0;
                op.Items.Add(Item(p + "%", () => App.Current.SetOpacity(v), Math.Abs(S.WindowOpacity - v) < 0.005));
            }
            Menu.Items.Add(op);
            Menu.Items.Add(Item("Lock window (unlock with the padlock, the tray or the hotkey)", () => SetClickThrough(!ClickThrough), ClickThrough));
            Menu.Items.Add(Item("Reset position / size", ResetLayout));
            Menu.Items.Add(Sep());
            Menu.Items.Add(Item("Open launcher", () => App.Current.ShowLauncher()));
            Menu.Items.Add(Item("Exit Wingmate", () => App.Current.ExitApp()));
        }
    }
}
