using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>An alert the user made: a name, a time on the region's clock (like the built-in Abyss events), the days it
    /// runs and an optional repeat every N hours (repeats run until midnight, then start again on the next chosen day).</summary>
    public sealed class CustomAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        /// <summary>"HH:mm", 24-hour, on the selected region's clock.</summary>
        public string Time { get; set; } = "20:00";
        /// <summary>"daily", or day names ("Mon", "Tue", ...).</summary>
        public List<string> Days { get; set; } = new() { "daily" };
        /// <summary>0 = once a day; 1..23 = again every N hours until midnight.</summary>
        public int RepeatHours { get; set; }
        public bool Enabled { get; set; } = true;

        public CustomAlert Clone() => new() { Id = Id, Name = Name, Time = Time, Days = new List<string>(Days ?? new()), RepeatHours = RepeatHours, Enabled = Enabled };
    }

    public static class CustomAlerts
    {
        public const string IdPrefix = "custom:";
        public const string Category = "custom";
        public const int MaxName = 40, MaxCount = 30, MaxRepeatHours = 23;
        /// <summary>Monday first, as shown in the editor.</summary>
        public static readonly string[] WeekDays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public static bool IsCustom(EventDef e) => e.Id.StartsWith(IdPrefix, StringComparison.Ordinal);

        /// <summary>"daily" when every day is picked (or none is stored), otherwise the picked days in Mon..Sun order.</summary>
        public static List<string> NormalizeDays(IEnumerable<string>? days)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in days ?? Array.Empty<string>())
            {
                var d = (raw ?? "").Trim();
                if (d.Equals("daily", StringComparison.OrdinalIgnoreCase)) return new List<string> { "daily" };
                if (d.Length >= 3) set.Add(d.Substring(0, 3));
            }
            var picked = WeekDays.Where(set.Contains).ToList();
            return picked.Count == 0 || picked.Count == 7 ? new List<string> { "daily" } : picked;
        }

        public static bool EveryDay(CustomAlert a) => NormalizeDays(a.Days).SequenceEqual(new[] { "daily" });

        /// <summary>"HH:mm" for a valid time ("9:5" stays invalid; "9:05" becomes "09:05"), or null.</summary>
        public static string? NormalizeTime(string? text)
        {
            var t = (text ?? "").Trim().Replace('.', ':');
            if (t.Length == 4 && t.All(char.IsDigit)) t = t[..2] + ":" + t[2..]; // "2130"
            var p = t.Split(':');
            if (p.Length != 2 || p[1].Length != 2) return null;
            return ScheduleEngine.TryParseHm(t, out var m) ? $"{m / 60:00}:{m % 60:00}" : null;
        }

        /// <summary>What is wrong with the editor's input (plain words), or null when it can be saved.</summary>
        public static string? Problem(string? name, string? time, IReadOnlyCollection<string> pickedDays, bool everyDay, bool repeat, string? repeatHours)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Give the alert a name.";
            if (NormalizeTime(time) == null) return "Type the time as hh:mm, for example 21:30.";
            if (!everyDay && pickedDays.Count == 0) return "Pick at least one day, or Every day.";
            if (repeat && (!int.TryParse((repeatHours ?? "").Trim(), out var h) || h < 1 || h > MaxRepeatHours))
                return $"Repeat every 1 to {MaxRepeatHours} hours.";
            return null;
        }

        /// <summary>Repairs a saved list (hand edits, newer versions): drops nulls and alerts without a valid time, trims names,
        /// fixes ids, days and repeat. Never drops an alert that has a valid time.</summary>
        public static List<CustomAlert> Sanitize(List<CustomAlert>? list)
        {
            var ids = new HashSet<string>();
            var result = new List<CustomAlert>();
            foreach (var a in list ?? new())
            {
                if (a == null) continue;
                var t = NormalizeTime(a.Time);
                if (t == null) continue;
                a.Time = t;
                a.Name = (a.Name ?? "").Trim();
                if (a.Name.Length == 0) a.Name = "Custom alert";
                if (a.Name.Length > MaxName) a.Name = a.Name[..MaxName].TrimEnd();
                if (string.IsNullOrWhiteSpace(a.Id) || !ids.Add(a.Id)) { a.Id = Guid.NewGuid().ToString("N"); ids.Add(a.Id); }
                a.Days = NormalizeDays(a.Days);
                a.RepeatHours = Math.Clamp(a.RepeatHours, 0, MaxRepeatHours);
                result.Add(a);
            }
            return result;
        }

        /// <summary>The schedule event for an alert: regional clock, no duration (it is a moment, not a window).</summary>
        public static EventDef ToEvent(CustomAlert a) => new()
        {
            Id = IdPrefix + a.Id, Name = a.Name, Voice = null, Category = Category,
            Days = NormalizeDays(a.Days), Start = NormalizeTime(a.Time) ?? a.Time, Clock = "regional",
            DurationMin = 0, RepeatEveryMin = Math.Clamp(a.RepeatHours, 0, MaxRepeatHours) * 60,
        };

        /// <summary>The built-in schedule plus the user's switched-on custom alerts (the built-in file is not changed).</summary>
        public static ScheduleFile With(ScheduleFile s, IEnumerable<CustomAlert>? custom)
        {
            var on = (custom ?? Enumerable.Empty<CustomAlert>()).Where(a => a != null && a.Enabled && NormalizeTime(a.Time) != null).ToList();
            if (on.Count == 0) return s;
            return new ScheduleFile
            {
                FormatVersion = s.FormatVersion, Version = s.Version, Updated = s.Updated, Source = s.Source,
                Regions = s.Regions, Resets = s.Resets, Events = s.Events.Concat(on.Select(ToEvent)).ToList(),
            };
        }

        /// <summary>Short summary for the list in Settings, e.g. "21:30 · Mon, Thu · every 3 h".</summary>
        public static string Describe(CustomAlert a)
        {
            var days = NormalizeDays(a.Days);
            var d = days.Count == 1 && days[0] == "daily" ? "every day" : string.Join(", ", days);
            var r = a.RepeatHours > 0 ? $" \u00b7 every {a.RepeatHours} h" : "";
            return $"{NormalizeTime(a.Time) ?? a.Time} \u00b7 {d}{r}";
        }
    }
}
