using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Wingmate.Core
{
    public sealed class ChecklistItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public bool Done { get; set; }
    }

    public sealed class ChecklistSection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Kind { get; set; } = "custom"; // daily | weekly | custom
        public string Name { get; set; } = "";
        public List<ChecklistItem> Items { get; set; } = new();
        public bool Hidden { get; set; }
        public bool Collapsed { get; set; }

        public bool IsComplete => Items.Count > 0 && Items.All(i => i.Done);
        public bool IsCustom => Kind == "custom";
    }

    public sealed class ChecklistState
    {
        public int Version { get; set; } = 1;
        /// <summary>Character this checklist belongs to (in the multi-character file).</summary>
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>The character's class (one of CharacterClasses, e.g. "Templar"); "" until the player picks one.
        /// Optional: older files without it load as "". It never affects the lists.</summary>
        public string Class { get; set; } = "";
        public DateTime? LastDailyReset { get; set; }   // UTC reset boundary last applied
        public DateTime? LastWeeklyReset { get; set; }
        public List<ChecklistSection> Sections { get; set; } = new();

        public static readonly string[] DefaultDailies =
        {
            "Daily attendance reward", "Duty missions", "Daily dungeon runs", "Nightmare",
            "Transcendence", "Dimensional Invasion", "Shugo Festival",
        };
        public static readonly string[] DefaultWeeklies =
        {
            "Raids", "Ascension Trial", "Subjugation", "Exploration",
            "Use Abyss time", "Arena of Tactics (3 matches)", "Artifact Siege",
        };

        public static ChecklistSection DefaultSection(string kind) => new()
        {
            Kind = kind,
            Name = kind == "daily" ? "Dailies" : "Weeklies",
            Items = (kind == "daily" ? DefaultDailies : DefaultWeeklies).Select(t => new ChecklistItem { Text = t }).ToList(),
        };

        public static ChecklistState CreateDefault() => new() { Sections = { DefaultSection("daily"), DefaultSection("weekly") } };

        /// <summary>"Reset all lists": uncheck every item in every list and show hidden sections. Items are kept.</summary>
        public void ResetAll()
        {
            foreach (var sec in Sections)
            {
                foreach (var i in sec.Items) i.Done = false;
                sec.Hidden = false;
            }
        }

        public static string DefaultPath => Path.Combine(AppSettings.Dir, "checklist.json");
        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public static ChecklistState Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var s = JsonSerializer.Deserialize<ChecklistState>(File.ReadAllText(path, Encoding.UTF8), Opts);
                    if (s != null) { s.EnsureBuiltIns(); return s; }
                }
            }
            catch (Exception ex)
            {
                Log.Write("checklist load: " + ex.Message);
                try { File.Copy(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
            }
            return CreateDefault();
        }

        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opts), new UTF8Encoding(false));
                File.Move(tmp, path, true);
            }
            catch (Exception ex) { Log.Write("checklist save: " + ex.Message); }
        }

        /// <summary>Dailies and Weeklies always exist (they can be edited, not deleted).</summary>
        public void EnsureBuiltIns()
        {
            if (!Sections.Any(x => x.Kind == "daily")) Sections.Insert(0, DefaultSection("daily"));
            if (!Sections.Any(x => x.Kind == "weekly")) Sections.Insert(Math.Min(1, Sections.Count), DefaultSection("weekly"));
        }
    }

    public static class ResetLogic
    {
        private static readonly string[] DayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public static EventDef DefaultDaily => new() { Id = "daily_reset", Name = "Daily reset", Days = { "daily" }, Start = "07:00", Clock = "utc" };
        public static EventDef DefaultWeekly => new() { Id = "weekly_reset", Name = "Weekly reset", Days = { "Wed" }, Start = "07:00", Clock = "utc" };

        public static EventDef Find(ScheduleFile s, string id) =>
            s.Resets.FirstOrDefault(r => r.Id == id && ScheduleEngine.TryParseHm(r.Start, out _))
            ?? (id == "daily_reset" ? DefaultDaily : DefaultWeekly);

        /// <summary>Most recent reset moment (UTC) at or before nowUtc.</summary>
        public static DateTime LastBoundary(EventDef reset, TimeZoneInfo regionTz, DateTime nowUtc)
        {
            ScheduleEngine.TryParseHm(reset.Start, out var startMin);
            var tz = string.Equals(reset.Clock, "utc", StringComparison.OrdinalIgnoreCase) ? TimeZoneInfo.Utc : regionTz;
            var days = new HashSet<string>(reset.Days.SelectMany(d =>
                d.Equals("daily", StringComparison.OrdinalIgnoreCase) ? DayNames
                : d.Length >= 3 ? new[] { char.ToUpperInvariant(d[0]) + d.Substring(1, 2).ToLowerInvariant() } : Array.Empty<string>()));
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            for (int back = 0; back <= 8; back++)
            {
                var date = localNow.Date.AddDays(-back);
                if (!days.Contains(DayNames[(int)date.DayOfWeek])) continue;
                var local = DateTime.SpecifyKind(date.AddMinutes(startMin), DateTimeKind.Unspecified);
                if (tz.IsInvalidTime(local)) local = local.AddHours(1);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);
                if (utc <= nowUtc) return utc;
            }
            return nowUtc.AddDays(-7); // no matching day (bad data): treat as long ago
        }

        /// <summary>Next reset moment (UTC) after nowUtc.</summary>
        public static DateTime NextBoundary(EventDef reset, TimeZoneInfo regionTz, DateTime nowUtc)
        {
            for (int h = 1; h <= 24 * 8; h += 1)
            {
                var b = LastBoundary(reset, regionTz, nowUtc.AddHours(h));
                if (b > nowUtc) return b;
            }
            return nowUtc.AddDays(1);
        }

        /// <summary>Clears checkmarks (and un-hides) for each kind whose reset passed since it was last applied.
        /// Covers resets missed while the PC was off. First run just records the boundary. Returns true if anything changed.</summary>
        public static bool Apply(ChecklistState s, DateTime dailyBoundary, DateTime weeklyBoundary)
        {
            bool changed = false;
            changed |= ApplyKind(s, "daily", dailyBoundary, s.LastDailyReset, v => s.LastDailyReset = v);
            changed |= ApplyKind(s, "weekly", weeklyBoundary, s.LastWeeklyReset, v => s.LastWeeklyReset = v);
            return changed;
        }

        private static bool ApplyKind(ChecklistState s, string kind, DateTime boundary, DateTime? last, Action<DateTime> set)
        {
            if (last == null) { set(boundary); return true; }
            if (boundary <= last.Value) return false; // same period (or clock moved back)
            foreach (var sec in s.Sections.Where(x => x.Kind == kind))
            {
                foreach (var i in sec.Items) i.Done = false;
                sec.Hidden = false;
            }
            set(boundary);
            return true;
        }
    }

    /// <summary>"Copy to" another character: items arrive unchecked; same-named list (trimmed, any case) gets the new items
    /// merged in, skipping duplicates (trimmed, any case); otherwise a new list of the same kind is added.</summary>
    public static class ListCopy
    {
        public sealed record Result(int Added, int Skipped, bool Created);

        private static string Norm(string? s) => (s ?? "").Trim();

        public static Result CopyInto(ChecklistSection src, ChecklistState target)
        {
            var dest = target.Sections.FirstOrDefault(s => string.Equals(Norm(s.Name), Norm(src.Name), StringComparison.OrdinalIgnoreCase));
            bool created = dest == null;
            if (dest == null)
            {
                dest = new ChecklistSection { Kind = src.Kind, Name = Norm(src.Name) };
                target.Sections.Add(dest);
            }
            var seen = new HashSet<string>(dest.Items.Select(i => Norm(i.Text)), StringComparer.OrdinalIgnoreCase);
            int added = 0, skipped = 0;
            foreach (var it in src.Items)
            {
                var t = Norm(it.Text);
                if (t.Length == 0) continue;
                if (!seen.Add(t)) { skipped++; continue; }
                dest.Items.Add(new ChecklistItem { Text = t, Done = false });
                added++;
            }
            if (added > 0) dest.Hidden = false; // it has unchecked items again, so show it
            return new Result(added, skipped, created);
        }
    }

    public static class CelebrationLines
    {
        public static readonly Dictionary<string, string[]> Pools = new()
        {
            ["daily"] = new[] { "Nice work, dailies done!", "That's your dailies, all wrapped up.", "Dailies complete. Go enjoy the rest of your day.", "All done for today. Great job!" },
            ["weekly"] = new[] { "Weeklies complete. That's a solid week!", "Every weekly checked off. Nicely done.", "That's the whole week handled. Well played!" },
            ["custom"] = new[] { "Checklist complete!", "All done. Nice work!", "List complete. Time to make a new one!", "All checked off. Time for a new list!" },
            ["long"] = new[] { "That was a long one. Seriously impressive!", "Every last one done. That's real dedication.", "Wow, you powered through that whole list!", "Big list, fully cleared. You earned a break!" },
        };

        public static string PoolFor(ChecklistSection sec, int longThreshold) =>
            sec.Items.Count >= longThreshold ? "long" : sec.Kind is "daily" or "weekly" ? sec.Kind : "custom";

        private static readonly Dictionary<string, int> LastPick = new();

        /// <summary>Random index in the pool, never the same as the previous pick from that pool.</summary>
        public static int Pick(string pool, Random rng)
        {
            int n = Pools[pool].Length;
            int last = LastPick.TryGetValue(pool, out var l) ? l : -1;
            int i = rng.Next(n);
            if (n > 1 && i == last) i = (i + 1 + rng.Next(n - 1)) % n;
            LastPick[pool] = i;
            return i;
        }

        public static string ClipKey(string pool, int index) => $"cel_{pool}_{index + 1}";

        /// <summary>Clip played instead of the pool line when a completion finishes every list (tools/render_voice.py STITCHED).</summary>
        public const string AllDoneKey = "cel_all_1";

        /// <summary>True when every counted list of the character is complete and there are at least two of them.
        /// Counted: lists with items that are visible, or hidden because they were already completed.
        /// Empty lists and lists hidden by hand while unfinished don't count (they'd otherwise block or fake it).
        /// One list alone never counts, so single-list users keep the normal pool lines.</summary>
        /// <summary>The all-lists-done line needs at least this many counted lists; with fewer, the normal line plays.</summary>
        public const int MinListsForAllDone = 2;
        /// <summary>...and Wingmate must have been running longer than this (this session, monotonic clock).</summary>
        public static readonly TimeSpan MinUptimeForAllDone = TimeSpan.FromHours(2);

        /// <summary>Play the long all-lists-done line? Every counted list complete (2+ lists) and running for more than 2 hours.</summary>
        public static bool ShouldPlayAllDone(ChecklistState state, TimeSpan uptime) => uptime > MinUptimeForAllDone && AllListsComplete(state);

        public static bool AllListsComplete(ChecklistState state)
        {
            var counted = state.Sections.Where(s => s.Items.Count > 0 && (!s.Hidden || s.IsComplete)).ToList();
            return counted.Count >= MinListsForAllDone && counted.All(s => s.IsComplete);
        }
    }

    /// <summary>Checked items leave the list one at a time: each warps out as it's checked and stays hidden (still saved as
    /// checked) until a reset unchecks it, or until "Show checked items" is on.</summary>
    public static class CheckedItems
    {
        /// <summary>The items a list shows: all of them with "Show checked items" on, otherwise only the unchecked ones.</summary>
        public static IEnumerable<ChecklistItem> Visible(ChecklistSection s, bool showChecked) => showChecked ? s.Items : s.Items.Where(i => !i.Done);

        /// <summary>How many checked items the list is hiding right now.</summary>
        public static int HiddenCount(ChecklistSection s, bool showChecked) => showChecked ? 0 : s.Items.Count(i => i.Done);

        /// <summary>Just checked: warp this one item out on its own? Not while checked items are shown, and not when it was the last
        /// one (then the list's own celebration plays and takes it away with the list, as before).</summary>
        public static bool WarpOutOnCheck(ChecklistSection s, ChecklistItem item, bool showChecked) =>
            !showChecked && item.Done && s.Items.Contains(item) && !s.IsComplete;
    }

    public static class WarpPicker
    {
        private static int _last = -1;
        /// <summary>0 = sparkle dissolve, 1 = black hole, 2 = ripple; never the same twice in a row.</summary>
        public static int Next(Random rng)
        {
            int i = rng.Next(3);
            if (i == _last) i = (i + 1 + rng.Next(2)) % 3;
            _last = i;
            return i;
        }
    }
}

namespace Wingmate.Core
{
    /// <summary>What happens to an inline edit when it ends (Enter, Escape, focus loss, another + click, collapsing...).</summary>
    public static class EditLogic
    {
        /// <summary>Item edit. Returns the text to keep, or null to remove the item.
        /// Typed text is trimmed; empty = remove. Escape on an existing item keeps its old text;
        /// Escape on a new item keeps what was typed (or discards it if empty).</summary>
        public static string? ResolveItem(string original, string typed, bool isNew, bool escaped)
        {
            if (escaped && !isNew) return string.IsNullOrWhiteSpace(original) ? null : original;
            var t = (typed ?? "").Trim();
            return t.Length == 0 ? null : t;
        }

        /// <summary>List rename: trimmed text, or the old name if empty or Escape.</summary>
        public static string ResolveName(string original, string typed, bool escaped)
        {
            var t = (typed ?? "").Trim();
            return escaped || t.Length == 0 ? original : t;
        }

        /// <summary>Applies an item edit result to the section.</summary>
        public static void ApplyItem(ChecklistSection sec, ChecklistItem item, string? text)
        {
            if (text == null) sec.Items.Remove(item); else item.Text = text;
        }
    }
}
