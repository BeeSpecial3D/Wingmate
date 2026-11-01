using System;
using System.Collections.Generic;
using System.Linq;
using Wingmate.Core;
using Xunit;

/// <summary>The repeated hour when clocks go back: an event in that hour alerts on its FIRST occurrence (still daylight time).</summary>
public class ClockChangeTests
{
    static DateTime U(int y, int mo, int d, int h, int mi = 0) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    static List<DateTime> Starts(string tzId, string winId, string days, string start, int repeat, DateTime nowUtc)
    {
        var ev = new EventDef { Id = "e", Name = "E", Days = { days }, Start = start, RepeatEveryMin = repeat, DurationMin = 10 };
        var s = new ScheduleFile { Events = { ev } };
        var region = new RegionDef { Id = "r", TimeZone = tzId, WindowsTimeZone = winId };
        return ScheduleEngine.Occurrences(s, region, _ => true, nowUtc).Select(o => o.StartUtc).ToList();
    }

    static List<DateTime> Hourly(string tz, string win, DateTime now) => Starts(tz, win, "daily", "00:00", 60, now);

    [Fact] public void NewYork_Nov1_RepeatedOneAm_UsesTheFirstOne()
    {
        // Nov 1, 2026: 02:00 EDT -> 01:00 EST. 01:00 EDT = 05:00 UTC (first), 01:00 EST = 06:00 UTC (second).
        var starts = Hourly("America/New_York", "Eastern Standard Time", U(2026, 10, 31, 12));
        Assert.Contains(U(2026, 11, 1, 4), starts);   // 00:00 EDT
        Assert.Contains(U(2026, 11, 1, 5), starts);   // 01:00, first occurrence
        Assert.DoesNotContain(U(2026, 11, 1, 6), starts);
        Assert.Contains(U(2026, 11, 1, 7), starts);   // 02:00 EST
        Assert.Equal(1, starts.Count(t => t >= U(2026, 11, 1, 5) && t < U(2026, 11, 1, 7)));
    }

    [Fact] public void NewYork_Nov1_HalfPastOne_UsesTheFirstOne()
    {
        var starts = Starts("America/New_York", "Eastern Standard Time", "daily", "00:30", 60, U(2026, 10, 31, 12));
        Assert.Contains(U(2026, 11, 1, 5, 30), starts);
        Assert.DoesNotContain(U(2026, 11, 1, 6, 30), starts);
    }

    [Fact] public void LosAngeles_Nov1_RepeatedOneAm_UsesTheFirstOne()
    {
        // 01:00 PDT = 08:00 UTC (first), 01:00 PST = 09:00 UTC (second).
        var starts = Hourly("America/Los_Angeles", "Pacific Standard Time", U(2026, 10, 31, 12));
        Assert.Contains(U(2026, 11, 1, 8), starts);
        Assert.DoesNotContain(U(2026, 11, 1, 9), starts);
        Assert.Contains(U(2026, 11, 1, 10), starts);  // 02:00 PST
    }

    [Fact] public void Berlin_Oct25_RepeatedTwoAm_UsesTheFirstOne()
    {
        // Oct 25, 2026: 03:00 CEST -> 02:00 CET. 02:00 CEST = 00:00 UTC (first), 02:00 CET = 01:00 UTC (second).
        var starts = Hourly("Europe/Berlin", "W. Europe Standard Time", U(2026, 10, 24, 12));
        Assert.Contains(U(2026, 10, 25, 0), starts);
        Assert.DoesNotContain(U(2026, 10, 25, 1), starts);
        Assert.Contains(U(2026, 10, 25, 2), starts);  // 03:00 CET
        var rift = Starts("Europe/Berlin", "W. Europe Standard Time", "daily", "02:00", 180, U(2026, 10, 24, 12));
        Assert.Contains(U(2026, 10, 25, 0), rift);    // 02:00 Rift, first occurrence
        Assert.DoesNotContain(U(2026, 10, 25, 1), rift);
    }

    [Fact] public void EveningEvents_FollowTheClock_AcrossTheChange()
    {
        // Siege 21:00 New York: 01:00 UTC while on EDT, 02:00 UTC once on EST.
        var starts = Starts("America/New_York", "Eastern Standard Time", "daily", "21:00", 0, U(2026, 10, 31, 12));
        Assert.Contains(U(2026, 11, 1, 1), starts);   // Oct 31 21:00 EDT
        Assert.Contains(U(2026, 11, 2, 2), starts);   // Nov 1 21:00 EST
    }

    [Fact] public void SpringForward_SkippedHour_StillHasNoEvent()
    {
        // Mar 8, 2026 New York: 02:00-02:59 doesn't exist.
        var starts = Hourly("America/New_York", "Eastern Standard Time", U(2026, 3, 7, 12));
        Assert.Contains(U(2026, 3, 8, 6), starts);    // 01:00 EST
        Assert.Contains(U(2026, 3, 8, 7), starts);    // 03:00 EDT
        Assert.Equal(2, starts.Count(t => t >= U(2026, 3, 8, 6) && t < U(2026, 3, 8, 8))); // 01:00 and 03:00, nothing for 02:00
    }

    [Fact] public void ToUtcFirst_NormalTimes_AreUnchanged()
    {
        var ny = TimeZones.Find("America/New_York", "Eastern Standard Time");
        Assert.Equal(U(2026, 7, 1, 16), TimeZones.ToUtcFirst(new DateTime(2026, 7, 1, 12, 0, 0), ny));
        Assert.Equal(U(2026, 12, 1, 17), TimeZones.ToUtcFirst(new DateTime(2026, 12, 1, 12, 0, 0), ny));
        Assert.Equal(U(2026, 11, 1, 5, 15), TimeZones.ToUtcFirst(new DateTime(2026, 11, 1, 1, 15, 0), ny));
    }
}
