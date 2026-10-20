using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wingmate.Core
{
    public sealed class AppSettings
    {
        public string Region { get; set; } = "";
        public bool Muted { get; set; }
        /// <summary>Old multi-choice warning times (read for migration only).</summary>
        public List<int> LeadMinutes { get; set; } = new() { 5 };
        /// <summary>Warn this many minutes before an event (1-60).</summary>
        public int WarnMinutes { get; set; } = 5;
        /// <summary>Per alert type on/off (key = AlertTypes.Key). Missing = default (Abyss on, PvE off).</summary>
        public Dictionary<string, bool> AlertChoices { get; set; } = new();
        /// <summary>Old single warning colour: read once for migration to <see cref="ThemeWarnColor"/>, then dropped.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WarnColor { get; set; }
        /// <summary>Alerts warning colour (flash and the "starting soon" look) chosen per theme, theme id -> "#RRGGBB".
        /// Missing or invalid = that theme's own default (<see cref="ThemeWarnDefaults"/>).</summary>
        public Dictionary<string, string> ThemeWarnColor { get; set; } = new();
        /// <summary>Each theme's default warning colour (theme id -> "#RRGGBB"), filled by the app from themes.json. Missing = red.</summary>
        public static Dictionary<string, string> ThemeWarnDefaults { get; } = new();

        public static string DefaultWarnColor(string? themeId) =>
            ThemeWarnDefaults.TryGetValue(ThemeKey(themeId), out var h) && WarnColors.TryParse(h) != null ? WarnColors.Normalize(h) : WarnColors.Default;

        /// <summary>The warning colour of a theme: the user's choice for it, or its default.</summary>
        public string WarnColorFor(string? themeId) =>
            ThemeWarnColor != null && ThemeWarnColor.TryGetValue(ThemeKey(themeId), out var h) && WarnColors.TryParse(h) != null ? WarnColors.Normalize(h) : DefaultWarnColor(themeId);

        /// <summary>Warning colour used by the alerts (and Test alert): the active theme's.</summary>
        [JsonIgnore] public string ActiveWarnColor => WarnColorFor(Theme);

        /// <summary>Sets the active theme's warning colour (invalid input is ignored).</summary>
        public void SetWarnColor(string? hex)
        {
            if (WarnColors.TryParse(hex) == null) return;
            ThemeWarnColor ??= new();
            ThemeWarnColor[ThemeKey(Theme)] = WarnColors.Normalize(hex);
        }

        /// <summary>Back to the active theme's own default colour.</summary>
        public void ResetWarnColor() => ThemeWarnColor?.Remove(ThemeKey(Theme));
        public bool HotkeyEnabled { get; set; }
        /// <summary>RegisterHotKey modifiers (MOD_ALT 1, MOD_CONTROL 2, MOD_SHIFT 4, MOD_WIN 8) and virtual key. Default Ctrl+Alt+K.</summary>
        public int HotkeyModifiers { get; set; } = 3;
        public int HotkeyKey { get; set; } = 0x4B;
        public bool AlertAtStart { get; set; }
        public bool PveAlerts { get; set; }
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double Width { get; set; } = 520;
        /// <summary>Old per-window opacities (alerts / launcher / checklist): read once for migration to <see cref="ThemeOpacity"/>, then dropped.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Opacity { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? LauncherOpacity { get; set; }
        public int SettingsVersion { get; set; }
        /// <summary>Old "Auto-minimize after opening" choice. Ignored since 1.0.3 (the launcher never auto-minimizes); kept
        /// only so the saved value round-trips unchanged and an older Wingmate still reads its own setting.</summary>
        public bool LauncherAutoMinimize { get; set; } = true;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? ChecklistOpacity { get; set; }
        /// <summary>One opacity for all windows, saved per theme (theme id -> 0.4..1). Missing = <see cref="DefaultOpacity"/>.</summary>
        public Dictionary<string, double> ThemeOpacity { get; set; } = new();
        public const double MinOpacity = 0.4, MaxOpacity = 1.0;

        /// <summary>Opacity a theme starts with: Elyos 80% (it is a light theme, so a little see-through), the rest 100%.</summary>
        public static double DefaultOpacity(string? themeId) => ThemeKey(themeId) == "elyos" ? 0.8 : 1.0;
        private static string ThemeKey(string? id) => string.IsNullOrWhiteSpace(id) ? "current" : id.Trim().ToLowerInvariant();

        /// <summary>The saved opacity of a theme (clamped), or its default.</summary>
        public double OpacityFor(string? themeId)
        {
            if (ThemeOpacity != null && ThemeOpacity.TryGetValue(ThemeKey(themeId), out var v) && double.IsFinite(v)) return Math.Clamp(v, MinOpacity, MaxOpacity);
            return DefaultOpacity(themeId);
        }

        /// <summary>Opacity of the launcher, alerts and checklist: the active theme's value.</summary>
        [JsonIgnore] public double WindowOpacity => OpacityFor(Theme);

        /// <summary>Sets the active theme's opacity (0.4..1).</summary>
        public void SetOpacity(double v)
        {
            ThemeOpacity ??= new();
            ThemeOpacity[ThemeKey(Theme)] = Math.Clamp(double.IsFinite(v) ? v : MaxOpacity, MinOpacity, MaxOpacity);
        }
        public double? ChecklistLeft { get; set; }
        public double? ChecklistTop { get; set; }
        public double ChecklistWidth { get; set; } = 340;
        /// <summary>Chosen checklist height; null = size to the lists (older settings files have none).</summary>
        public double? ChecklistHeight { get; set; }
        /// <summary>Text size multiplier for the launcher, alerts and checklist (0.9 / 1.0 / 1.15 / 1.3).</summary>
        public double FontScale { get; set; } = 1.0;
        /// <summary>Bella voice volume, 0..1.</summary>
        public double Volume { get; set; } = 1.0;
        /// <summary>A long list (for celebration lines) has at least this many items. Fixed, not a user option.</summary>
        public const int LongListThreshold = 10;
        public static readonly double[] FontScales = { 0.9, 1.0, 1.15, 1.3 };
        public static readonly string[] FontScaleNames = { "Small", "Medium", "Large", "X-Large" };

        /// <summary>Raised after every save so Settings, menus and windows stay in sync (one source of truth).</summary>
        public static event Action? Saved;
        public bool CelebrationVoice { get; set; } = true;
        /// <summary>Colour theme id from themes.json; missing or unknown = "current" (the original look).</summary>
        public string Theme { get; set; } = "current";
        /// <summary>Launcher × asks "Quit Wingmate?" first (unticked by "Don't ask again"). Older files have none = ask.</summary>
        public bool AskBeforeQuit { get; set; } = true;
        /// <summary>Checklist window title; empty means the default "Wingmate".</summary>
        public string ChecklistTitle { get; set; } = "";
        /// <summary>Custom theme: the built-in theme it starts from (missing or unknown = Current).</summary>
        public string CustomBase { get; set; } = "current";
        /// <summary>Custom theme colours the user picked: CustomTheme group key -> "#RRGGBB". Missing = the start theme's colour.</summary>
        public Dictionary<string, string> CustomColors { get; set; } = new();
        /// <summary>Last launcher position; null = centred (first run).</summary>
        public double? LauncherLeft { get; set; }
        public double? LauncherTop { get; set; }
        /// <summary>Launcher size (null = default width / height fits the content). Added in 1.0.3; older files have none.</summary>
        public double? LauncherWidth { get; set; }
        public double? LauncherHeight { get; set; }
        /// <summary>Last Settings window position; null = next to the launcher.</summary>
        public double? SettingsLeft { get; set; }
        public double? SettingsTop { get; set; }

        /// <summary>Windows that were open when Wingmate was last closed with the launcher's ×: "launcher", "alerts",
        /// "checklist", "settings". Null = never saved (first run), so startup shows just the launcher as before.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? OpenAtExit { get; set; }
        public static readonly string[] WindowNames = { "launcher", "alerts", "checklist", "settings" };

        /// <summary>Which windows to open at startup: the saved set (known names only), or null for the first-run default.</summary>
        [JsonIgnore] public IReadOnlyList<string>? StartupWindows =>
            OpenAtExit?.Select(n => (n ?? "").Trim().ToLowerInvariant()).Where(n => WindowNames.Contains(n)).Distinct().ToList();

        /// <summary>Sets one Custom colour (invalid input or an unknown key is ignored).</summary>
        public void SetCustomColor(string key, string? hex)
        {
            if (CustomTheme.Find(key) == null || WarnColors.TryParse(hex) == null) return;
            CustomColors ??= new();
            CustomColors[key] = WarnColors.Normalize(hex);
        }

        /// <summary>"Start from current theme": Custom takes the active theme's colours, warning colour and opacity.</summary>
        public void StartCustomFrom(ThemeSet.Theme t)
        {
            if (t.Id == CustomTheme.Id) return;
            CustomBase = t.Id;
            CustomColors = CustomTheme.Snapshot(t);
            ThemeWarnColor ??= new(); ThemeOpacity ??= new();
            ThemeWarnColor[CustomTheme.Id] = WarnColorFor(t.Id);
            ThemeOpacity[CustomTheme.Id] = OpacityFor(t.Id);
        }

        public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wingmate");
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static AppSettings Load()
        {
            AppSettings s = new();
            try
            {
                if (File.Exists(FilePath))
                    s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new AppSettings();
            }
            catch (Exception ex) { Log.Write("settings: " + ex.Message); }
            if (string.IsNullOrEmpty(s.Region)) s.Region = GuessRegion();
            if (s.SettingsVersion < 3) { s.Opacity = null; s.LauncherOpacity = null; s.SettingsVersion = 3; } // one-time: back to 100% default
            s.Migrate();
            return s;
        }

        /// <summary>v4: one warning time instead of several; per-alert toggles replace the single PvE switch.</summary>
        public void Migrate()
        {
            ThemeOpacity ??= new();
            ThemeWarnColor ??= new();
            Sanitize();
            if (SettingsVersion < 4) MigrateV4();
            if (SettingsVersion < 5) MigrateV5();
            if (SettingsVersion < 6) MigrateV6();
            if (SettingsVersion < 7) MigrateV7();
        }

        /// <summary>v7: Custom theme and remembered launcher/Settings positions. Nothing to convert: older files simply have
        /// none (Custom starts as a copy of Current, both windows open where they used to).</summary>
        private void MigrateV7()
        {
            SettingsVersion = 7;
        }

        /// <summary>Repairs what a hand-edited or newer file may contain; runs on every load and keeps everything valid.</summary>
        private void Sanitize()
        {
            CustomColors ??= new();
            foreach (var k in CustomColors.Keys.ToList())
                if (CustomTheme.Find(k) == null || WarnColors.TryParse(CustomColors[k]) == null) CustomColors.Remove(k);
                else CustomColors[k] = WarnColors.Normalize(CustomColors[k]);
            if (string.IsNullOrWhiteSpace(CustomBase) || CustomBase.Trim().ToLowerInvariant() == CustomTheme.Id) CustomBase = "current";
        }

        /// <summary>v6: the single warning colour becomes the selected theme's colour. The untouched old default (red)
        /// is not carried over, so that theme starts with its own default colour (like the opacity migration).</summary>
        private void MigrateV6()
        {
            if (WarnColors.TryParse(WarnColor) != null && !WarnColors.IsDefault(WarnColor) && !ThemeWarnColor.ContainsKey(ThemeKey(Theme)))
                SetWarnColor(WarnColor);
            WarnColor = null;
            SettingsVersion = 6;
        }

        /// <summary>v5: the three per-window opacities become one opacity for the selected theme (the highest of them).
        /// An untouched 100% is the old default, so the theme keeps its own default then (Elyos 80%).</summary>
        private void MigrateV5()
        {
            var old = new[] { Opacity, LauncherOpacity, ChecklistOpacity }.Where(v => v is double d && double.IsFinite(d)).Select(v => v!.Value).ToList();
            if (old.Count > 0)
            {
                double max = Math.Clamp(old.Max(), MinOpacity, MaxOpacity);
                if (max < MaxOpacity - 0.001 && !ThemeOpacity.ContainsKey(ThemeKey(Theme))) SetOpacity(max);
            }
            Opacity = LauncherOpacity = ChecklistOpacity = null;
            SettingsVersion = 5;
        }

        private void MigrateV4()
        {
            var old = (LeadMinutes ?? new List<int>()).Where(m => m >= 1 && m <= 60).ToList();
            WarnMinutes = old.Count > 0 ? old.Min() : 5;
            if (PveAlerts) { AlertChoices["shugo"] = true; AlertChoices["invasion"] = true; }
            SettingsVersion = 4;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Write("settings save: " + ex.Message); }
            Saved?.Invoke();
        }

        /// <summary>First run: pick a region from the PC's time zone.</summary>
        private static string GuessRegion()
        {
            var h = TimeZoneInfo.Local.BaseUtcOffset.TotalHours;
            if (h >= -1 && h <= 4) return "EU";
            if (h <= -7 && h >= -11) return "NA_West";
            return "NA_East";
        }
    }

    /// <summary>Warning colour helpers (pure, so they are unit tested).</summary>
    public static class WarnColors
    {
        public const string Default = "#FF1E1E";
        public static readonly (string Name, string Hex)[] Presets =
        {
            ("Red", "#FF1E1E"), ("Orange", "#FF8A1E"), ("Gold", "#FFC83D"), ("Green", "#3DDC6A"), ("Cyan", "#2FD6E8"),
            ("Blue", "#3D7BFF"), ("Purple", "#A45CFF"), ("Pink", "#FF4FB8"), ("White", "#FFFFFF"),
        };

        /// <summary>Parses "#RRGGBB" / "RRGGBB" / "#RGB"; null if invalid.</summary>
        public static (byte R, byte G, byte B)? TryParse(string? text)
        {
            var t = (text ?? "").Trim().TrimStart('#');
            if (t.Length == 3) t = string.Concat(t[0], t[0], t[1], t[1], t[2], t[2]);
            if (t.Length != 6 || !int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var v)) return null;
            return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        public static (byte R, byte G, byte B) Parse(string? text) => TryParse(text) ?? TryParse(Default)!.Value;

        public static string Normalize(string? text) { var c = Parse(text); return $"#{c.R:X2}{c.G:X2}{c.B:X2}"; }

        public static bool IsDefault(string? text) => Normalize(text) == Default;
    }

    public static class Log
    {
        public static void Write(string msg)
        {
            try
            {
                Directory.CreateDirectory(AppSettings.Dir);
                File.AppendAllText(Path.Combine(AppSettings.Dir, "wingmate.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
