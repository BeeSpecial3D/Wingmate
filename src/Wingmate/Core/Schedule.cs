using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Wingmate.Core
{
    public sealed class ScheduleFile
    {
        public int FormatVersion { get; set; }
        public string Version { get; set; } = "";
        public string? Updated { get; set; }
        public string? Source { get; set; }
        public List<RegionDef> Regions { get; set; } = new();
        public List<EventDef> Events { get; set; } = new();
        public List<EventDef> Resets { get; set; } = new();
    }

    public sealed class RegionDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string TimeZone { get; set; } = "";
        public string? WindowsTimeZone { get; set; }
    }

    public sealed class EventDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Voice { get; set; }
        public string Category { get; set; } = "pvp";   // pvp | pve | reset
        public bool Optional { get; set; }
        public List<string> Days { get; set; } = new();
        public string Start { get; set; } = "";
        public string Clock { get; set; } = "regional"; // regional | utc
        public int DurationMin { get; set; }
        public int RepeatEveryMin { get; set; }
        public bool Unverified { get; set; }
        public string? Note { get; set; }

        public bool IsPve => string.Equals(Category, "pve", StringComparison.OrdinalIgnoreCase);
        public string DisplayName => Unverified ? Name + " (?)" : Name;
    }

    public sealed record Occurrence(EventDef Event, DateTime StartUtc, DateTime EndUtc);

    public static class ScheduleParser
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static ScheduleFile? TryParse(string json, out string error)
        {
            error = "";
            try
            {
                var s = JsonSerializer.Deserialize<ScheduleFile>(json, Opts);
                if (s == null) { error = "empty"; return null; }
                if (s.FormatVersion != 1) { error = "unsupported formatVersion " + s.FormatVersion; return null; }
                s.Events = s.Events.Where(e => !string.IsNullOrWhiteSpace(e.Name) && ScheduleEngine.TryParseHm(e.Start, out _)).ToList();
                foreach (var e in s.Events) if (string.IsNullOrWhiteSpace(e.Id)) e.Id = e.Name;
                if (s.Events.Count == 0) { error = "no valid events"; return null; }
                if (s.Regions.Count == 0) { error = "no regions"; return null; }
                return s;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }
    }

    public static class TimeZones
    {
        public static TimeZoneInfo Find(params string?[] ids)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            }
            return TimeZoneInfo.Utc;
        }

        /// <summary>Short label for the user's own zone, e.g. "EDT". Falls back to "local".</summary>
        public static string LocalAbbrev(DateTime utc)
        {
            var tz = TimeZoneInfo.Local;
            var name = tz.IsDaylightSavingTime(utc) ? tz.DaylightName : tz.StandardName;
            var words = (name ?? "").Replace(".", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words.All(w => w.All(c => c < 128 && char.IsLetter(c))))
                return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
            return "local";
        }
    }

    public static class ScheduleEngine
    {
        private static readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public static bool TryParseHm(string? text, out int minutes)
        {
            minutes = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var p = text.Trim().Split(':');
            if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            minutes = h * 60 + m;
            return true;
        }

        private static HashSet<string> Days(IEnumerable<string> days)
        {
            var set = new HashSet<string>();
            foreach (var raw in days)
            {
                var d = (raw ?? "").Trim();
                if (d.Equals("daily", StringComparison.OrdinalIgnoreCase)) { foreach (var n in DayNames) set.Add(n); continue; }
                if (d.Length < 3) continue;
                d = char.ToUpperInvariant(d[0]) + d.Substring(1, 2).ToLowerInvariant();
                if (DayNames.Contains(d)) set.Add(d);
            }
            return set;
        }

        /// <summary>All occurrences from ~1 day ago to ~8 days ahead, sorted by start (UTC).</summary>
        public static List<Occurrence> Occurrences(ScheduleFile s, RegionDef region, Func<EventDef, bool> include, DateTime nowUtc)
        {
            var regionTz = TimeZones.Find(region.TimeZone, region.WindowsTimeZone);
            var list = new List<Occurrence>();
            foreach (var ev in s.Events)
            {
                if (!include(ev) || !TryParseHm(ev.Start, out var startMin)) continue;
                var days = Days(ev.Days);
                if (days.Count == 0) continue;
                var tz = string.Equals(ev.Clock, "utc", StringComparison.OrdinalIgnoreCase) ? TimeZoneInfo.Utc : regionTz;
                var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
                var rep = Math.Max(0, ev.RepeatEveryMin);
                var dur = Math.Max(0, ev.DurationMin);
                for (int off = -1; off <= 8; off++)
                {
                    var date = localNow.Date.AddDays(off);
                    if (!days.Contains(DayNames[(int)date.DayOfWeek])) continue;
                    int m = startMin;
                    while (m < 1440)
                    {
                        var local = DateTime.SpecifyKind(date.AddMinutes(m), DateTimeKind.Unspecified);
                        if (!tz.IsInvalidTime(local))
                        {
                            var st = TimeZoneInfo.ConvertTimeToUtc(local, tz);
                            list.Add(new Occurrence(ev, st, st.AddMinutes(dur)));
                        }
                        if (rep <= 0) break;
                        m += rep;
                    }
                }
            }
            list.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
            return list;
        }
    }

    public sealed record DisplayLine(string Text, string Kind); // kind: normal | soon | live

    public static class AbyssDisplay
    {
        public const int SoonMinutes = 5;

        public static string Span(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int totalMin = (int)Math.Floor(t.TotalMinutes);
            if (totalMin >= 1440) return $"{(int)Math.Floor(t.TotalDays)}d {t.Hours}h";
            if (totalMin >= 60) return $"{(int)Math.Floor(t.TotalHours)}h {t.Minutes}m";
            if (totalMin >= 10) return $"{totalMin}m";
            return $"{totalMin}m {t.Seconds:00}s";
        }

        public static string When(DateTime utc, DateTime nowUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local);
            string day = local.ToString("ddd", CultureInfo.InvariantCulture);
            if (local.Date == localNow.Date) day = "Today";
            else if (local.Date == localNow.Date.AddDays(1)) day = "Tomorrow";
            return day + " " + local.ToString("h:mm tt", CultureInfo.InvariantCulture) + " " + TimeZones.LocalAbbrev(utc);
        }

        /// <summary>Same rules as Abyss.ps1: soon (red) / live (blue) / normal.</summary>
        public static (string Mode, List<DisplayLine> Lines) Build(List<Occurrence> occ, DateTime nowUtc)
        {
            var live = occ.Where(o => o.EndUtc > o.StartUtc && o.StartUtc <= nowUtc && nowUtc < o.EndUtc)
                          .OrderBy(o => o.EndUtc).FirstOrDefault();
            var up = occ.Where(o => o.StartUtc > nowUtc).Take(3).ToList();
            var items = new List<(Occurrence O, bool Live)>();
            string mode = "none";
            if (up.Count >= 1 && (up[0].StartUtc - nowUtc).TotalMinutes <= SoonMinutes)
            {
                mode = "soon";
                items.Add((up[0], false));
                if (live != null) items.Add((live, true));
                if (up.Count >= 2) items.Add((up[1], false));
            }
            else if (live != null)
            {
                mode = "live";
                items.Add((live, true));
                foreach (var o in up.Take(2)) items.Add((o, false));
            }
            else if (up.Count >= 1)
            {
                mode = "normal";
                foreach (var o in up.Take(2)) items.Add((o, false));
            }
            var lines = new List<DisplayLine>();
            for (int i = 0; i < items.Count; i++)
            {
                var (o, isLive) = items[i];
                if (isLive)
                {
                    lines.Add(new DisplayLine($"LIVE now: {o.Event.DisplayName} (ends in {Span(o.EndUtc - nowUtc)})", "live"));
                }
                else
                {
                    var until = o.StartUtc - nowUtc;
                    var prefix = i == 0 ? "Next: " : "Then: ";
                    lines.Add(new DisplayLine($"{prefix}{o.Event.DisplayName} - {When(o.StartUtc, nowUtc)} - in {Span(until)}",
                        until.TotalMinutes <= SoonMinutes ? "soon" : "normal"));
                }
            }
            return (mode, lines);
        }
    }

    /// <summary>The alert types the user can pick (one per voice, so the two Arena sessions share a toggle).</summary>
    public static class AlertTypes
    {
        public static string Key(EventDef e) => string.IsNullOrEmpty(e.Voice) ? e.Id : e.Voice!;

        public static bool DefaultOn(EventDef e) => !e.IsPve;

        /// <summary>The one mapping from schedule category to picker group (new categories fall into General).</summary>
        public static readonly (string Category, string Group)[] Groups = { ("pvp", "PvP"), ("pve", "PvE") };
        public const string GeneralGroup = "General";
        public static string Group(EventDef e) =>
            Groups.FirstOrDefault(g => string.Equals(g.Category, e.Category, StringComparison.OrdinalIgnoreCase)).Group ?? GeneralGroup;

        /// <summary>Alert types grouped for display: PvP, PvE, then General (empty groups left out).</summary>
        public static List<(string Group, List<(string Key, string Label, string Full, EventDef Event)> Items)> Grouped(ScheduleFile d)
        {
            var all = All(d);
            return Groups.Select(g => g.Group).Append(GeneralGroup)
                .Select(g => (g, all.Where(t => Group(t.Event) == g).ToList())).Where(x => x.Item2.Count > 0).ToList();
        }

        public static bool Enabled(IReadOnlyDictionary<string, bool> choices, EventDef e) =>
            choices.TryGetValue(Key(e), out var on) ? on : DefaultOn(e);

        /// <summary>Short label: the event name without its bracketed detail.</summary>
        public static string Label(EventDef e)
        {
            var n = e.Name ?? e.Id;
            int i = n.IndexOf(" (", StringComparison.Ordinal);
            return i > 0 ? n.Substring(0, i) : n;
        }

        /// <summary>Distinct alert types in schedule order: (key, label, full name, representative event).</summary>
        public static List<(string Key, string Label, string Full, EventDef Event)> All(ScheduleFile d) =>
            d.Events.GroupBy(Key).Select(g => (g.Key, Label(g.First()), g.First().Name, g.First())).ToList();

        public static int ClampMinutes(int m) => Math.Clamp(m, 1, 60);
    }

    public sealed record DueAlert(Occurrence Occ, int LeadMinutes);

    public static class AlertPlanner
    {
        public static readonly int[] LeadOptions = { 1, 2, 3, 5, 10, 15, 30, 60 };

        /// <summary>Each (occurrence, lead) fires once, only within graceSec after its moment.
        /// Lead 0 means "at start". Thresholds passed earlier (startup, sleep) are marked handled silently.</summary>
        public static List<DueAlert> Due(List<Occurrence> occ, DateTime nowUtc, IReadOnlyCollection<int> leads,
                                         Dictionary<string, DateTime> fired, int graceSec = 60)
        {
            var due = new List<DueAlert>();
            if (leads.Count == 0) return due;
            int maxSec = leads.Max() * 60;
            foreach (var o in occ)
            {
                var sec = (o.StartUtc - nowUtc).TotalSeconds;
                if (sec > maxSec || sec < -graceSec) continue;
                foreach (var t in leads)
                {
                    var key = $"{o.Event.Id}|{o.StartUtc.Ticks}|{t}";
                    if (fired.ContainsKey(key) || sec > t * 60) continue;
                    fired[key] = o.StartUtc;
                    if (sec > t * 60 - graceSec) due.Add(new DueAlert(o, t));
                }
            }
            foreach (var k in fired.Where(kv => kv.Value < nowUtc.AddHours(-1)).Select(kv => kv.Key).ToList()) fired.Remove(k);
            return due;
        }
    }
}
