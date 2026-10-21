using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wingmate.Core
{
    public sealed class ChecklistItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = "";
        public bool Done { get; set; }

        // Events list only: filled in from the app's event definitions on load (EventsList.Sync), never saved.
        /// <summary>This item's own reset: "daily", "weekly" or "none". Null = follows its list (Dailies daily, Weeklies weekly).</summary>
        [JsonIgnore] public string? Reset { get; set; }
        /// <summary>When a limited-time event ends (UTC); the item hides from then on (its saved state is kept).</summary>
        [JsonIgnore] public DateTime? EndsUtc { get; set; }
        /// <summary>No longer in the app's event definitions: hidden, but its saved state is kept.</summary>
        [JsonIgnore] public bool Retired { get; set; }

        public bool IsHiddenAt(DateTime nowUtc) => Retired || (EndsUtc is DateTime end && nowUtc >= end);
    }

    /// <summary>The clock the checklist uses to hide ended events (tests can set it).</summary>
    public static class ChecklistClock
    {
        public static Func<DateTime> UtcNow = () => DateTime.UtcNow;
    }

    public sealed class ChecklistSection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Kind { get; set; } = "custom"; // daily | weekly | events | custom
        public string Name { get; set; } = "";
        public List<ChecklistItem> Items { get; set; } = new();
        public bool Hidden { get; set; }
        public bool Collapsed { get; set; }

        /// <summary>The items that show: everything except ended or retired events.</summary>
        [JsonIgnore] public List<ChecklistItem> LiveItems { get { var now = ChecklistClock.UtcNow(); return Items.Where(i => !i.IsHiddenAt(now)).ToList(); } }
        [JsonIgnore] public bool IsComplete { get { var live = LiveItems; return live.Count > 0 && live.All(i => i.Done); } }
        [JsonIgnore] public bool IsCustom => Kind == "custom";
        /// <summary>The Events list: kept up to date by Wingmate. Items can only be checked and unchecked.</summary>
        [JsonIgnore] public bool IsManaged => Kind == EventsList.Kind;
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
            "Daily attendance reward", "Duty missions", "Daily dungeon runs", "Nightmare", "Transcendence",
        }; // Dimensional Invasion and Shugo Festival moved to the Events list in 1.0.7
        public static readonly string[] DefaultWeeklies =
        {
            "Raids", "Ascension Trial", "Subjugation", "Exploration",
            "Use Abyss time", // Arena of Tactics and Artifact Siege moved to the Events list in 1.0.7
            "Command Missions (12)", "Abyss Command Missions (20)", "Battlefield (3 wins)", "Subscriber Shop",
        };
        // Defaults are used for a new list (new install, new character, a missing built-in list) and "Reset to default".
        // Saved lists only get new items through a one-time, append-only list update (see ApplyListUpdates).

        /// <summary>1.0.7 list update: these weeklies are appended once to each character's existing Weeklies.</summary>
        public const string Update107Weeklies = "weeklies-1.0.7";
        public static readonly string[] AddedWeeklies107 = { "Command Missions (12)", "Abyss Command Missions (20)", "Battlefield (3 wins)", "Subscriber Shop" };

        /// <summary>One-time list updates already applied to this character (each runs once, so removed items never come back).
        /// Lists created from the defaults already include them.</summary>
        public List<string> AppliedUpdates { get; set; } = new();

        /// <summary>Runs the list updates this character hasn't had yet. Append-only: an item is added to the end of the built-in
        /// Weeklies only when no item with that name (case and spaces ignored) is there; nothing is removed, reordered, renamed,
        /// checked or unchecked, and hidden lists stay hidden. True when something was recorded (the caller saves).</summary>
        public bool ApplyListUpdates()
        {
            AppliedUpdates ??= new();
            if (AppliedUpdates.Contains(Update107Weeklies)) return false;
            var weekly = Sections.FirstOrDefault(x => x.Kind == "weekly");
            if (weekly != null)
            {
                weekly.Items ??= new();
                foreach (var t in AddedWeeklies107)
                    if (!weekly.Items.Any(i => string.Equals((i?.Text ?? "").Trim(), t, StringComparison.OrdinalIgnoreCase)))
                        weekly.Items.Add(new ChecklistItem { Text = t });
            }
            AppliedUpdates.Add(Update107Weeklies);
            return true;
        }

        /// <summary>1.0.7 list update: event activities move from Dailies/Weeklies into the app-managed Events list.</summary>
        public const string Update107Events = "events-1.0.7";

        /// <summary>Lists ready for use: Dailies and Weeklies exist, one-time list updates are applied, and the Events list exists
        /// and matches the app's event definitions. True when a one-time update ran (the caller saves).</summary>
        public bool PrepareLists()
        {
            EnsureBuiltIns();
            bool changed = ApplyListUpdates();
            if (MoveEventsOnce()) changed = true;
            EnsureEvents();
            return changed;
        }

        /// <summary>Runs once per character. Each default event item still in Dailies/Weeklies under exactly its default name is
        /// taken out, and its checked state carries over to the Events entry. Renamed or deleted ones are left alone (a renamed
        /// copy stays as the user's own item); user-made items and every other item are untouched.</summary>
        public bool MoveEventsOnce()
        {
            AppliedUpdates ??= new();
            if (AppliedUpdates.Contains(Update107Events)) return false;
            var events = EnsureEvents();
            foreach (var (name, fromKind, eventId) in EventsList.MovedIn107)
            {
                var from = Sections.FirstOrDefault(x => x.Kind == fromKind);
                var item = from?.Items.FirstOrDefault(i => i != null && i.Text == name);
                if (from == null || item == null) continue;
                from.Items.Remove(item);
                var target = events.Items.FirstOrDefault(i => i.Id == eventId);
                if (target != null) target.Done = item.Done;
            }
            AppliedUpdates.Add(Update107Events);
            return true;
        }

        /// <summary>The Events list (created after Weeklies when missing), synced with the app's event definitions.</summary>
        public ChecklistSection EnsureEvents()
        {
            var ev = Sections.FirstOrDefault(x => x.Kind == EventsList.Kind);
            if (ev == null)
            {
                ev = EventsList.NewSection();
                int w = Sections.FindIndex(x => x.Kind == "weekly");
                Sections.Insert(w < 0 ? Sections.Count : w + 1, ev);
            }
            else EventsList.Sync(ev);
            return ev;
        }

        public static ChecklistSection DefaultSection(string kind) => kind == EventsList.Kind ? EventsList.NewSection() : new()
        {
            Kind = kind,
            Name = kind == "daily" ? "Dailies" : "Weeklies",
            Items = (kind == "daily" ? DefaultDailies : DefaultWeeklies).Select(t => new ChecklistItem { Text = t }).ToList(),
        };

        public static ChecklistState CreateDefault() => new()
        {
            Sections = { DefaultSection("daily"), DefaultSection("weekly"), EventsList.NewSection() },
            AppliedUpdates = { Update107Weeklies, Update107Events }, // the defaults already have the 1.0.7 layout
        };

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
                SafeFile.WriteAtomic(path, JsonSerializer.Serialize(this, Opts));
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
        /// <summary>When an item resets: its own cadence (Events items), otherwise its list's ("none" for custom lists).</summary>
        public static string ItemReset(ChecklistSection sec, ChecklistItem item) =>
            item.Reset ?? (sec.Kind is "daily" or "weekly" ? sec.Kind : "none");

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
            foreach (var sec in s.Sections)
            {
                bool touched = false;
                foreach (var i in sec.Items)
                    if (ItemReset(sec, i) == kind) { i.Done = false; touched = true; }
                if (sec.Kind == kind || touched) sec.Hidden = false;
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
            if (!ListRules.CanEditItems(src)) return new Result(0, 0, false); // the Events list is not copied
            var dest = target.Sections.FirstOrDefault(s => ListRules.CanEditItems(s) && string.Equals(Norm(s.Name), Norm(src.Name), StringComparison.OrdinalIgnoreCase));
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
            ["events"] = new[] { "All events done. Nice!", "Every event checked off. Well played!", "That's all the events handled. Great job!" },
            ["custom"] = new[] { "Checklist complete!", "All done. Nice work!", "List complete. Time to make a new one!", "All checked off. Time for a new list!" },
            ["long"] = new[] { "That was a long one. Seriously impressive!", "Every last one done. That's real dedication.", "Wow, you powered through that whole list!", "Big list, fully cleared. You earned a break!" },
        };

        public static string PoolFor(ChecklistSection sec, int longThreshold) =>
            sec.Items.Count >= longThreshold ? "long" : sec.Kind is "daily" or "weekly" or "events" ? sec.Kind : "custom";

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
        public static IEnumerable<ChecklistItem> Visible(ChecklistSection s, bool showChecked) => showChecked ? s.LiveItems : s.LiveItems.Where(i => !i.Done);

        /// <summary>How many checked items the list is hiding right now.</summary>
        public static int HiddenCount(ChecklistSection s, bool showChecked) => showChecked ? 0 : s.LiveItems.Count(i => i.Done);

        /// <summary>Just checked: warp this one item out on its own? Not while checked items are shown, and not when it was the last
        /// one (then the list's own celebration plays and takes it away with the list, as before).</summary>
        public static bool WarpOutOnCheck(ChecklistSection s, ChecklistItem item, bool showChecked) =>
            !showChecked && item.Done && s.Items.Contains(item) && !s.IsComplete;
    }

    /// <summary>"Check all and hide" from a list's right-click menu: checks every unchecked item that shows (ended events are
    /// left alone), so the list completes and goes away like when its last item is checked by hand. Works on every list,
    /// the read-only Events list too (it only checks). Each item comes back unchecked at its own reset.</summary>
    public static class CheckAll
    {
        /// <summary>What one "Check all" changed: the list and the items it checked (to undo exactly those).</summary>
        public sealed record Snapshot(string SectionId, IReadOnlyList<string> CheckedIds, bool WasHidden);

        /// <summary>Checks the unchecked items. Null (and nothing changes) when there is nothing to check.</summary>
        public static Snapshot? Apply(ChecklistSection sec)
        {
            var todo = sec.LiveItems.Where(i => !i.Done).ToList();
            if (todo.Count == 0) return null;
            foreach (var i in todo) i.Done = true;
            return new Snapshot(sec.Id, todo.Select(i => i.Id).ToList(), sec.Hidden);
        }

        /// <summary>Undo: unchecks the items it checked (items checked before stay checked) and shows the list again.
        /// False when none of them is still there and checked (e.g. a reset already cleared them).</summary>
        public static bool Undo(ChecklistState state, Snapshot snap)
        {
            var sec = state.Sections.FirstOrDefault(s => s.Id == snap.SectionId);
            if (sec == null) return false;
            var ids = new HashSet<string>(snap.CheckedIds);
            bool any = false;
            foreach (var i in sec.Items.Where(i => ids.Contains(i.Id) && i.Done)) { i.Done = false; any = true; }
            if (any) sec.Hidden = snap.WasHidden;
            return any;
        }

        /// <summary>The status line before "Undo", e.g. "Checked all in Weeklies · ".</summary>
        public static string Text(string? listName)
        {
            var n = (listName ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (n.Length > UndoCheck.MaxNameLength) n = n[..(UndoCheck.MaxNameLength - 1)].TrimEnd() + "\u2026";
            return n.Length == 0 ? "Checked all \u00b7 " : $"Checked all in {n} \u00b7 ";
        }
    }

    /// <summary>"Checked {item} · Undo": after an item is checked and starts disappearing, the status line offers to undo it for a
    /// few seconds. Only the most recent check can be undone.</summary>
    public static class UndoCheck
    {
        public const double Seconds = 5;
        public const int MaxNameLength = 40;

        /// <summary>The status line text before the "Undo" link, e.g. "Checked Daily quests · ".</summary>
        public static string Text(string? itemText)
        {
            var t = (itemText ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (t.Length > MaxNameLength) t = t[..(MaxNameLength - 1)].TrimEnd() + "\u2026";
            return t.Length == 0 ? "Checked \u00b7 " : $"Checked {t} \u00b7 ";
        }

        /// <summary>Unchecks the item if it is still in the list and still checked. False (and nothing changes) if it was
        /// removed, already unchecked (e.g. a reset happened) or belongs to another character.</summary>
        public static bool Undo(ChecklistState state, string itemId)
        {
            var item = state.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Id == itemId);
            if (item == null || !item.Done) return false;
            item.Done = false;
            var sec = state.Sections.First(s => s.Items.Contains(item));
            sec.Hidden = false; // never leave it in a hidden (completed) list
            return true;
        }
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
