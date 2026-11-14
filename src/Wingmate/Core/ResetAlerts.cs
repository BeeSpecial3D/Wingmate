using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>The Daily reset alert (added in 1.0.9). Its time comes from the same reset entry the checklist uses
    /// (<see cref="ResetLogic.Find"/>), so the alert and the checklist reset always agree, also with an older online schedule
    /// that has no such alert yet. On the weekly reset day both fall on the same moment: then only "Weekly reset" is
    /// announced (if that alert is on), so nobody hears two lines for one reset.</summary>
    public static class ResetAlerts
    {
        public const string DailyId = "daily_reset", WeeklyId = "weekly_reset";
        public const string DailyName = "Daily reset";

        /// <summary>The alert event for the daily reset: same days, time and clock as the checklist's daily reset.</summary>
        public static EventDef DailyAlert(ScheduleFile s)
        {
            var r = ResetLogic.Find(s, DailyId);
            return new EventDef
            {
                Id = DailyId, Name = DailyName, Voice = DailyId, Category = "reset",
                Days = new List<string>(r.Days ?? new List<string> { "daily" }), Start = r.Start, Clock = r.Clock, DurationMin = 0,
            };
        }

        /// <summary>The schedule with the Daily reset alert added (just before Weekly reset when that is there), or the same
        /// schedule when it already has one. The original is never changed.</summary>
        public static ScheduleFile WithDailyReset(ScheduleFile s)
        {
            if (s.Events.Any(e => e.Id == DailyId || AlertTypes.Key(e) == DailyId)) return s;
            var events = new List<EventDef>(s.Events);
            int i = events.FindIndex(e => e.Id == WeeklyId);
            events.Insert(i < 0 ? events.Count : i, DailyAlert(s));
            return new ScheduleFile
            {
                FormatVersion = s.FormatVersion, Version = s.Version, Updated = s.Updated, Source = s.Source,
                Regions = s.Regions, Resets = s.Resets, Events = events,
            };
        }

        /// <summary>Drops a Daily reset that starts at the same moment as a Weekly reset in the list (the weekly one covers it).
        /// When Weekly reset is switched off it is not in the list, so the daily one stays.</summary>
        public static List<Occurrence> DropDailyAtWeekly(List<Occurrence> occ)
        {
            var weekly = new HashSet<DateTime>(occ.Where(o => o.Event.Id == WeeklyId).Select(o => o.StartUtc));
            if (weekly.Count == 0) return occ;
            return occ.Where(o => !(o.Event.Id == DailyId && weekly.Contains(o.StartUtc))).ToList();
        }
    }
}
