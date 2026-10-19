using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Wingmate.Core;
using WF = System.Windows.Forms;

namespace Wingmate
{
    public partial class App : Application
    {
        public const string SupportUrl = "https://ko-fi.com/beespecial3d";
        public const string ProjectUrl = "https://github.com/BeeSpecial3D/Wingmate";
        public static AppSettings Settings { get; private set; } = new();
        /// <summary>Every character's checklist (checklist.json). Loaded once at startup.</summary>
        public static ChecklistBook Book { get; private set; } = ChecklistBook.CreateNew();
        public static ScheduleProvider Schedule { get; } = new();
        public static new App Current => (App)Application.Current;

        /// <summary>Monotonic start mark (Stopwatch), taken when the App type is first touched at process start.</summary>
        private static readonly long StartStamp = Stopwatch.GetTimestamp();
        /// <summary>How long Wingmate has been running this session (unaffected by clock changes or sleep adjustments).</summary>
        public static TimeSpan Uptime => Stopwatch.GetElapsedTime(StartStamp);
        static App() { } // explicit static ctor: StartStamp is taken before Main runs, not lazily on first use

        private Mutex? _mutex;
        private EventWaitHandle? _showSignal;
        private WF.NotifyIcon? _tray;
        private WF.ToolStripMenuItem? _trayAbyss, _trayMute, _trayClick, _trayClickList;
        private Hotkey? _hotkey;
        /// <summary>Shown in Settings when the hotkey could not be registered.</summary>
        public string HotkeyNote { get; private set; } = "";
        private LauncherWindow? _launcher;
        private AbyssWindow? _abyss;
        private ChecklistWindow? _checklist;
        private SettingsWindow? _settings;
        private bool _abyssActive, _checklistActive; // shown or tucked in the tray (only × makes them inactive)
        private WF.ToolStripMenuItem? _trayChecklist;
        private DispatcherTimer? _onlineTimer;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _mutex = new Mutex(true, @"Local\Wingmate_BeeSpecial3D_v1", out bool first);
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Wingmate_BeeSpecial3D_Show");
            if (!first) { _showSignal.Set(); Shutdown(); return; } // already running: bring up its launcher

            DispatcherUnhandledException += (s, a) => { Log.Write("Unhandled: " + a.Exception); a.Handled = true; };
            var t = new Thread(() =>
            {
                while (_showSignal.WaitOne()) Dispatcher.BeginInvoke(new Action(ShowLauncher));
            }) { IsBackground = true };
            t.Start();

            foreach (var th in Th.Set.All) // each theme's default warning colour ("#AARRGGBB" -> "#RRGGBB")
                if (th.Colors.TryGetValue("warn", out var w) && w.Length == 9) AppSettings.ThemeWarnDefaults[th.Id] = "#" + w[3..];
            Settings = AppSettings.Load();
            Th.Apply(Settings.Theme); // unknown or missing = Current (Default)
            if (Settings.Theme != Th.Current.Id) // e.g. a theme id from a newer version: carry its saved opacity over to the theme actually used
            {
                var old = (Settings.Theme ?? "").Trim().ToLowerInvariant();
                if (Settings.ThemeOpacity.TryGetValue(old, out var v) && !Settings.ThemeOpacity.ContainsKey(Th.Current.Id)) Settings.ThemeOpacity[Th.Current.Id] = v;
                if (Settings.ThemeWarnColor.TryGetValue(old, out var wc) && !Settings.ThemeWarnColor.ContainsKey(Th.Current.Id)) Settings.ThemeWarnColor[Th.Current.Id] = wc;
                Settings.Theme = Th.Current.Id;
            }
            Voice.Volume = Settings.Volume;
            LoadBook();
            AppSettings.Saved += OnSettingsSaved;
            try { Schedule.LoadLocal(); }
            catch (Exception ex) { Log.Write(ex.ToString()); ConfirmDialog.Info(null, "Wingmate", ex.Message); Shutdown(); return; }
            _ = Schedule.RefreshOnlineAsync();
            _onlineTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
            _onlineTimer.Tick += async (s, a) => await Schedule.RefreshOnlineAsync();
            _onlineTimer.Start();

            CreateTray();
            try { _hotkey = new Hotkey(); _hotkey.Pressed += ToggleBothClickThrough; ApplyHotkey(); }
            catch (Exception ex) { Log.Write("hotkey: " + ex.Message); }
            ShowLauncher();
            if (!Release.DataIsSeparateFromInstall(AppSettings.Dir, AppContext.BaseDirectory))
                Log.Write("warning: data folder and program folder overlap: " + AppContext.BaseDirectory);
            Updates.ReadyChanged += RefreshUpdateUi;
            RefreshUpdateUi();
            Updates.Start();
        }

        // ------------------------------------------------------------ updates (Velopack, GitHub Releases)
        public Updater Updates { get; } = new();
        private WF.ToolStripMenuItem? _trayUpdate;

        /// <summary>Shows the subtle "Update ready" link in the launcher and the tray item while an update is downloaded.</summary>
        private void RefreshUpdateUi()
        {
            bool ready = Updates.Ready != null;
            if (_trayUpdate != null) { _trayUpdate.Visible = ready; _trayUpdate.Text = $"Restart to update (v{Updates.ReadyVersion})"; }
            _launcher?.SetUpdateReady(ready ? Updates.ReadyVersion : null);
        }

        /// <summary>Saves everything, hands over to Velopack's updater and quits; the updater restarts Wingmate.</summary>
        public void RestartToUpdate()
        {
            if (Exiting || Updates.Ready == null) return;
            try { _checklist?.CommitActiveEdit(); SaveBook(); Settings.Save(); } catch { }
            Updates.ApplyAndRestart();
            ExitApp();
        }

        private void CreateTray()
        {
            _tray = new WF.NotifyIcon { Text = "Wingmate", Visible = true };
            try
            {
                using var st = GetResourceStream(new Uri("pack://application:,,,/Assets/wingmate.ico")).Stream;
                _tray.Icon = new System.Drawing.Icon(st, WF.SystemInformation.SmallIconSize); // crisp 16 px frame
            }
            catch { _tray.Icon = System.Drawing.SystemIcons.Application; }
            var m = new WF.ContextMenuStrip();
            _trayUpdate = new WF.ToolStripMenuItem("Restart to update", null, (s, a) => RestartToUpdate()) { Visible = false };
            m.Items.Add(_trayUpdate);
            m.Items.Add("Open launcher", null, (s, a) => ShowLauncher());
            _trayAbyss = new WF.ToolStripMenuItem("Abyss alerts", null, (s, a) => ToggleAbyss());
            m.Items.Add(_trayAbyss);
            _trayChecklist = new WF.ToolStripMenuItem("Checklist", null, (s, a) => ToggleChecklist());
            m.Items.Add(_trayChecklist);
            m.Items.Add(new WF.ToolStripSeparator());
            _trayMute = new WF.ToolStripMenuItem("Mute voice", null, (s, a) => SetMuted(!Settings.Muted));
            m.Items.Add(_trayMute);
            m.Items.Add("Test alert", null, (s, a) => TestAlert());
            _trayClick = new WF.ToolStripMenuItem("Alerts click-through", null, (s, a) => _abyss?.SetClickThrough(!_abyss.ClickThrough));
            m.Items.Add(_trayClick);
            _trayClickList = new WF.ToolStripMenuItem("Checklist click-through", null, (s, a) => _checklist?.SetClickThrough(!_checklist.ClickThrough));
            m.Items.Add(_trayClickList);
            m.Items.Add(new WF.ToolStripSeparator());
            m.Items.Add("Send feedback", null, (s, a) => OpenFeedback());
            m.Items.Add("Support this app \u2764\uFE0F", null, (s, a) => OpenSupport());
            m.Items.Add("Exit Wingmate", null, (s, a) => ExitApp());
            m.Opening += (s, a) =>
            {
                _trayAbyss.Checked = AbyssOpen;
                _trayChecklist.Checked = ChecklistOpen;
                _trayMute.Checked = Settings.Muted;
                _trayClick.Enabled = _abyss != null && _abyss.IsVisible;
                _trayClick.Checked = _abyss?.ClickThrough == true;
                _trayClickList.Enabled = _checklist != null && _checklist.IsVisible;
                _trayClickList.Checked = _checklist?.ClickThrough == true;
            };
            _tray.ContextMenuStrip = m;
            _tray.MouseClick += (s, a) => { if (a.Button == WF.MouseButtons.Left) ShowLauncher(); };
        }

        public void ShowLauncher()
        {
            if (_launcher == null)
            {
                _launcher = new LauncherWindow();
                _launcher.Closed += (s, a) =>
                {
                    _launcher = null;
                    MaybeQuit();
                };
                _launcher.SetUpdateReady(Updates.Ready != null ? Updates.ReadyVersion : null);
            }
            _launcher.Show();
            if (_launcher.WindowState == WindowState.Minimized) _launcher.WindowState = WindowState.Normal;
            _launcher.Topmost = true; // stays on top like the overlays; its dialogs are owned + topmost and menus are popups, so they stay above it
            _launcher.Activate();
        }

        // ------------------------------------------------------------ settings (one source of truth: App.Settings)
        // ------------------------------------------------------------ characters (each has its own checklists)
        private static void LoadBook()
        {
            var legacy = string.IsNullOrWhiteSpace(Settings.ChecklistTitle) ? "Wingmate" : Settings.ChecklistTitle.Trim(); // the old window title
            Book = ChecklistBook.Load(ChecklistState.DefaultPath, legacy);
            if (Book.Migrated) { SaveBook(); Log.Write("checklist migration: saved in the new per-character format"); }
            if (Book.ReadOnly) Log.Write("checklist: running without saving because checklist.json could not be read safely");
        }

        public static void SaveBook() => Book.Save(ChecklistState.DefaultPath);

        private void CharacterChanged()
        {
            SaveBook();
            _checklist?.LoadCharacter();
            _launcher?.RefreshCharacter();
            _settings?.Refresh();
        }

        private void BeforeCharacterChange() { _checklist?.CommitActiveEdit(); }

        public void CycleCharacter() { BeforeCharacterChange(); Book.Next(); CharacterChanged(); }
        public void SelectCharacter(string id) { BeforeCharacterChange(); if (Book.Select(id)) CharacterChanged(); }
        public void AddCharacter() { BeforeCharacterChange(); Book.Add(); CharacterChanged(); }

        public void RemoveCharacter(string id, Window? owner)
        {
            var c = Book.Characters.FirstOrDefault(x => x.Id == id);
            if (c == null || Book.Characters.Count <= 1) return;
            if (!ConfirmDialog.Ask(owner, $"Remove {c.Name}?", $"Remove {c.Name} and all of their checklists? This can't be undone.", "Remove", ConfirmKind.Danger)) return;
            BeforeCharacterChange();
            if (Book.Remove(id)) CharacterChanged();
        }

        /// <summary>Rename = edit the checklist title (shows the checklist if needed).</summary>
        public void RenameCharacter()
        {
            ShowChecklist();
            _checklist?.Dispatcher.BeginInvoke(new Action(() => _checklist?.StartTitleEdit()), DispatcherPriority.Loaded);
        }

        public void CharacterRenamed() { _launcher?.RefreshCharacter(); }

        public void ToggleSettings()
        {
            if (_settings != null && _settings.IsVisible) { _settings.Close(); return; }
            if (_settings == null)
            {
                _settings = new SettingsWindow(_launcher);
                _settings.Closed += (s, a) => _settings = null;
            }
            _settings.Show();
            _settings.Activate();
        }

        public void RefreshSettingsWindow() => _settings?.Refresh();

        /// <summary>Every save re-applies the look everywhere and syncs the Settings window and launcher.</summary>
        private void OnSettingsSaved()
        {
            Voice.Volume = Settings.Volume;
            ApplyLook();
            _launcher?.SyncFromSettings();
            _settings?.Refresh();
        }

        public void ApplyLook()
        {
            double op = Settings.WindowOpacity; // one value for all windows, saved per theme
            if (_launcher != null) { _launcher.Opacity = op; FontScaler.Apply(_launcher); }
            if (_abyss != null) { _abyss.Opacity = op; FontScaler.Apply(_abyss); _abyss.ApplyWarnColor(); }
            if (_checklist != null) { _checklist.Opacity = op; FontScaler.Apply(_checklist); }
        }

        public void SetFontScale(double k) { Settings.FontScale = k; Settings.Save(); }

        /// <summary>Opacity of all windows for the active theme (40-100%). Saving applies it everywhere.</summary>
        public void SetOpacity(double v) { Settings.SetOpacity(v); Settings.Save(); }

        /// <summary>Switches the theme live everywhere and saves it.</summary>
        public void SetTheme(string id)
        {
            Th.Apply(id);
            Settings.Theme = Th.Current.Id;
            Settings.Save(); // ApplyLook (via Saved) switches every window to this theme's own opacity
            _abyss?.ThemeChanged();
            _checklist?.ThemeChanged();
            _launcher?.RefreshState();
        }

        // ------------------------------------------------------------ copy a list to other characters
        /// <summary>Copies a list of the current character into the given characters (unchecked, merged by name, no duplicates).</summary>
        public string CopyListTo(ChecklistSection sec, System.Collections.Generic.IEnumerable<string> characterIds)
        {
            int added = 0, skipped = 0, targets = 0;
            string lastName = "";
            foreach (var id in characterIds.Distinct())
            {
                var c = Book.Characters.FirstOrDefault(x => x.Id == id);
                if (c == null || c == Book.Current) continue;
                var r = ListCopy.CopyInto(sec, c);
                added += r.Added; skipped += r.Skipped; targets++; lastName = c.Name;
            }
            if (targets == 0) return "";
            SaveBook();
            string who = targets == 1 ? lastName : $"{targets} characters";
            string what = added == 0 ? "nothing new" : added == 1 ? "1 item" : $"{added} items";
            return $"Copied \"{sec.Name}\" to {who}: {what}" + (skipped > 0 ? $", {skipped} already there" : "");
        }

        // ------------------------------------------------------------ quitting
        /// <summary>True while the app shuts down (windows may close for real).</summary>
        public bool Exiting { get; private set; }

        /// <summary>Launcher ×: "Quit Wingmate?" (unless turned off with "Don't ask again"), then quit everything.</summary>
        public void RequestQuit(Window? owner)
        {
            if (Exiting) return;
            if (Settings.AskBeforeQuit)
            {
                bool ok = ConfirmDialog.AskWithCheck(owner, "Quit Wingmate?", "Your alerts will stop.", "Quit", ConfirmKind.Danger, "Don't ask again", out bool dontAsk);
                if (!ok) return;
                if (dontAsk) { Settings.AskBeforeQuit = false; Settings.Save(); }
            }
            ExitApp();
        }

        public void ResetAllPositions()
        {
            if (_abyss != null) _abyss.ResetLayout(); else { Settings.Left = null; Settings.Top = null; Settings.Width = 520; }
            if (_checklist != null) _checklist.ResetLayout(); else { Settings.ChecklistLeft = null; Settings.ChecklistTop = null; Settings.ChecklistWidth = 340; Settings.ChecklistHeight = null; }
            if (_launcher != null)
            {
                var wa = SystemParameters.WorkArea;
                _launcher.Left = wa.Left + (wa.Width - _launcher.ActualWidth) / 2;
                _launcher.Top = wa.Top + (wa.Height - _launcher.ActualHeight) / 2;
            }
            Settings.Save();
        }

        public int HiddenSectionCount =>
            Book.Current.Sections.Count(x => x.Hidden);

        public void ShowHiddenSections()
        {
            if (_checklist != null) { _checklist.ShowHiddenSections(); return; }
            foreach (var x in Book.Current.Sections) x.Hidden = false;
            SaveBook();
        }

        /// <summary>"Reset all lists…": confirm, then uncheck everything (items kept, hidden sections shown). Never celebrates.</summary>
        public void ResetAllLists(Window? owner)
        {
            if (!ConfirmDialog.Ask(owner, "Reset all lists?",
                    "Uncheck every item in every list? Your items stay; only the checkmarks are cleared.", "Reset all", ConfirmKind.Primary)) return;
            if (_checklist != null) { _checklist.ResetAllLists(); return; }
            Book.Current.ResetAll();
            SaveBook();
        }

        public void PreviewCelebration() => _checklist?.PreviewCelebration();
        public bool CanPreviewCelebration => ChecklistOpen && _checklist!.CanPreview;
        public bool? AbyssClickThrough => _abyss != null && _abyss.IsVisible ? _abyss.ClickThrough : null;
        public void SetAbyssClickThrough(bool on) => _abyss?.SetClickThrough(on);
        public bool? ChecklistClickThrough => _checklist != null && _checklist.IsVisible ? _checklist.ClickThrough : null;
        public void SetChecklistClickThrough(bool on) => _checklist?.SetClickThrough(on);

        /// <summary>Hotkey: if the open windows differ or are off, turn click-through on for both; if both on, turn both off.</summary>
        public void ToggleBothClickThrough()
        {
            var open = new System.Collections.Generic.List<bool>();
            if (AbyssOpen) open.Add(_abyss!.ClickThrough);
            if (ChecklistOpen) open.Add(_checklist!.ClickThrough);
            if (open.Count == 0) return;
            bool on = !open.All(x => x);
            if (AbyssOpen) _abyss!.SetClickThrough(on);
            if (ChecklistOpen) _checklist!.SetClickThrough(on);
        }

        /// <summary>(Re)registers the hotkey from the settings. Returns false if Windows refused it.</summary>
        public bool ApplyHotkey()
        {
            if (_hotkey == null) { HotkeyNote = Settings.HotkeyEnabled ? "Hotkeys are unavailable." : ""; return false; }
            int m = Settings.HotkeyModifiers, k = Settings.HotkeyKey;
            if (Settings.HotkeyEnabled && Hotkey.IsReserved(m, k)) { _hotkey.Clear(); HotkeyNote = "That combo is used by your other overlay. Pick another."; return false; }
            bool ok = _hotkey.Set(Settings.HotkeyEnabled, m, k);
            HotkeyNote = ok ? "" : $"{Hotkey.Describe(m, k)} is already in use by another app. Pick another.";
            return ok;
        }

        /// <summary>Pauses the hotkey while the user types a new combo in Settings.</summary>
        public void SuspendHotkey() => _hotkey?.Clear();

        public void TestAlert() { ShowAbyss(); _abyss?.TestAlert(); }

        public bool AbyssOpen => _abyss != null && _abyss.IsVisible;
        public bool ChecklistOpen => _checklist != null && _checklist.IsVisible;

        /// <summary>Quit only when every window was closed with × (a launcher or window hidden in the tray still counts as open).</summary>
        private void MaybeQuit() { if (_launcher == null && !_abyssActive && !_checklistActive) ExitApp(); }

        private void EnsureLauncherIfNothingShown() { if (_launcher == null && !AbyssOpen && !ChecklistOpen) ShowLauncher(); }

        public void ShowAbyss()
        {
            if (_abyss == null)
            {
                _abyss = new AbyssWindow();
                _abyss.IsVisibleChanged += (s, a) => _launcher?.RefreshState();
            }
            _abyssActive = true;
            _abyss.Show();
        }

        /// <summary>Launcher/tray toggle off.</summary>
        public void HideAbyss() { _abyss?.Hide(); _abyssActive = false; EnsureLauncherIfNothingShown(); }
        /// <summary>The window's – button: tuck into the tray.</summary>
        /// <summary>The window's × button.</summary>
        public void CloseAbyss() { _abyss?.Hide(); _abyssActive = false; MaybeQuit(); }
        public void ToggleAbyss() { if (AbyssOpen) HideAbyss(); else ShowAbyss(); }

        public void ShowChecklist()
        {
            if (_checklist == null)
            {
                _checklist = new ChecklistWindow();
                _checklist.IsVisibleChanged += (s, a) => _launcher?.RefreshState();
            }
            _checklistActive = true;
            _checklist.Show();
        }

        public void HideChecklist() { _checklist?.Hide(); _checklistActive = false; EnsureLauncherIfNothingShown(); }
        public void CloseChecklist() { _checklist?.Hide(); _checklistActive = false; MaybeQuit(); }
        public void ToggleChecklist() { if (ChecklistOpen) HideChecklist(); else ShowChecklist(); }

        public void SetMuted(bool on)
        {
            Settings.Muted = on;
            if (on) Voice.Stop();
            Settings.Save();
            _abyss?.RefreshBadges();
            _checklist?.RefreshBadges();
            _settings?.Refresh();
        }

        public void RegionChanged() { Settings.Save(); _abyss?.RegionChanged(); _checklist?.CheckResets(true); }

        public static void OpenSupport() => OpenUrl(SupportUrl);

        /// <summary>Send feedback: a new GitHub issue prefilled with the app, schedule and Windows versions.</summary>
        public static void OpenFeedback()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string app = v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
            string sched = "";
            try { sched = Schedule.Current.Version; } catch { }
            OpenUrl(Feedback.IssueUrl(app, sched, Environment.OSVersion.Version.ToString()));
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Write("open url: " + ex.Message); }
        }

        public void ExitApp()
        {
            if (Exiting) return;
            Exiting = true;
            try { _checklist?.CommitActiveEdit(); } catch { }
            try { _abyss?.SaveLayout(); } catch { }
            try { _checklist?.SaveLayout(); } catch { }
            try { SaveBook(); } catch { }
            Settings.Save();
            Voice.Stop();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            try { _hotkey?.Dispose(); } catch { }
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            try { _mutex?.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }
    }
}
