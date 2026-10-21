using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>One event activity in the Events list. Shipped with the app: the user's file only keeps checked state, by Id.
    /// Ids never change (a new name keeps the checked state). EndsUtc: a limited-time event hides from then on.</summary>
    public sealed record EventDefinition(string Id, string Name, string Reset, DateTime? EndsUtc = null);

    /// <summary>The Events list: event activities with their own resets (daily, weekly or none), kept up to date by Wingmate.
    /// Users can check and uncheck them only. On every load the app's current definitions win: new events appear, renamed ones
    /// take the new name, removed (retired) and ended ones hide. Saved state of hidden items is kept, never deleted.</summary>
    public static class EventsList
    {
        public const string Kind = "events";
        public const string Name = "Events";

        /// <summary>The Spiritstone shop event ends with the Dec 15 PST / Dec 16 CET 2026 maintenance; it hides at that week's
        /// reset, Wed Dec 16 07:00 UTC (Tue 11 PM PST, Wed 8 AM CET).</summary>
        public static readonly DateTime SpiritstoneShopEnds = new(2026, 12, 16, 7, 0, 0, DateTimeKind.Utc);

        public static readonly IReadOnlyList<EventDefinition> Definitions = new[]
        {
            new EventDefinition("shugo-festival", "Shugo Festival", "daily"),
            new EventDefinition("dimensional-invasion", "Dimensional Invasion", "daily"),
            new EventDefinition("artifact-siege", "Artifact Siege", "weekly"),
            new EventDefinition("arena-of-tactics", "Arena of Tactics (3 matches)", "weekly"),
            new EventDefinition("spiritstone-shop-2026", "Spiritstone shop (event)", "weekly", SpiritstoneShopEnds),
        };

        /// <summary>1.0.7: default items that move out of Dailies/Weeklies (exact default name, from-list kind, event id).</summary>
        public static readonly (string Name, string FromKind, string EventId)[] MovedIn107 =
        {
            ("Shugo Festival", "daily", "shugo-festival"),
            ("Dimensional Invasion", "daily", "dimensional-invasion"),
            ("Artifact Siege", "weekly", "artifact-siege"),
            ("Arena of Tactics (3 matches)", "weekly", "arena-of-tactics"),
        };

        public static ChecklistSection NewSection(IReadOnlyList<EventDefinition>? defs = null)
        {
            var sec = new ChecklistSection { Kind = Kind, Name = Name };
            Sync(sec, defs);
            return sec;
        }

        /// <summary>Makes the list match the definitions (in their order), keeping each item's checked state by id. Items the
        /// definitions no longer have stay at the end, hidden (Retired), so nothing saved is lost.</summary>
        public static void Sync(ChecklistSection sec, IReadOnlyList<EventDefinition>? defs = null)
        {
            defs ??= Definitions;
            sec.Kind = Kind;
            sec.Name = Name;
            var saved = (sec.Items ?? new List<ChecklistItem>()).Where(i => i != null && !string.IsNullOrEmpty(i.Id)).ToList();
            var byId = new Dictionary<string, ChecklistItem>();
            foreach (var i in saved) byId.TryAdd(i.Id, i);
            var list = new List<ChecklistItem>();
            foreach (var d in defs)
            {
                if (!byId.Remove(d.Id, out var item)) item = new ChecklistItem { Id = d.Id };
                item.Text = d.Name; item.Reset = d.Reset; item.EndsUtc = d.EndsUtc; item.Retired = false;
                list.Add(item);
            }
            foreach (var i in saved)
                if (byId.Remove(i.Id)) { i.Retired = true; i.Reset = "none"; i.EndsUtc = null; list.Add(i); }
            sec.Items = list;
        }
    }

    /// <summary>What the user may change in a list. The Events list is app-managed: check and uncheck only (plus Undo, Show
    /// checked items, collapse, hide and moving the whole list).</summary>
    public static class ListRules
    {
        /// <summary>Add, edit (rename), remove, reorder or drag items, and Copy to.</summary>
        public static bool CanEditItems(ChecklistSection s) => !s.IsManaged;
        public static bool CanMoveItem(ChecklistSection from, ChecklistSection to) => CanEditItems(from) && CanEditItems(to);
        public static bool CanRenameList(ChecklistSection s) => s.IsCustom;
        public static bool CanDeleteList(ChecklistSection s) => s.IsCustom;
        public static bool CanResetToDefault(ChecklistSection s) => s.Kind is "daily" or "weekly";
    }
}
