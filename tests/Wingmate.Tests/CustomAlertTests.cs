using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

public class CustomAlertTests
{
    static readonly RegionDef NaEast = new() { Id = "NA_East", Name = "NA East", TimeZone = "America/New_York", WindowsTimeZone = "Eastern Standard Time" };
    static readonly RegionDef NaWest = new() { Id = "NA_West", Name = "NA West", TimeZone = "America/Los_Angeles", WindowsTimeZone = "Pacific Standard Time" };
    static readonly RegionDef Eu = new() { Id = "EU", Name = "EU", TimeZone = "Europe/Berlin", WindowsTimeZone = "W. Europe Standard Time" };

    static ScheduleFile Base() => new()
    {
        FormatVersion = 1, Version = "t", Regions = { NaEast, NaWest, Eu },
        Events = { new EventDef { Id = "siege", Name = "Artifact Siege", Voice = "siege", Days = { "Sat" }, Start = "21:00", DurationMin = 60 } },
    };

    static List<DateTime> Starts(CustomAlert a, RegionDef r, DateTime nowUtc) =>
        ScheduleEngine.Occurrences(CustomAlerts.With(Base(), new[] { a }), r, e => CustomAlerts.IsCustom(e), nowUtc).Select(o => o.StartUtc).ToList();

    static DateTime U(int y, int mo, int d, int h, int mi = 0) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    [Fact] public void Daily_UsesTheRegionClock()
    {
        var a = new CustomAlert { Name = "Guild run", Time = "21:30" };
        var s = Starts(a, NaEast, U(2026, 10, 21, 12));
        Assert.Contains(U(2026, 10, 22, 1, 30), s); // 21:30 EDT
        Assert.Contains(U(2026, 10, 21, 19, 30), Starts(a, Eu, U(2026, 10, 21, 12))); // 21:30 CEST
    }

    [Fact] public void ChosenDaysOnly()
    {
        var a = new CustomAlert { Name = "x", Time = "10:00", Days = new() { "Mon", "Thu" } };
        var s = Starts(a, NaWest, U(2026, 10, 21, 12)); // Wed
        Assert.All(s, t => Assert.Contains(TimeZoneInfo.ConvertTimeFromUtc(t, TimeZones.Find("America/Los_Angeles", "Pacific Standard Time")).DayOfWeek,
            new[] { DayOfWeek.Monday, DayOfWeek.Thursday }));
        Assert.Contains(U(2026, 10, 22, 17), s); // Thu 10:00 PDT
    }

    [Fact] public void RepeatEveryNHours_UntilMidnight()
    {
        var a = new CustomAlert { Name = "x", Time = "08:00", RepeatHours = 5, Days = new() { "Thu" } };
        var s = Starts(a, NaEast, U(2026, 10, 21, 12));
        var thu = s.Where(t => t.Date >= new DateTime(2026, 10, 22) && t < U(2026, 10, 23, 4)).ToList();
        Assert.Equal(new[] { U(2026, 10, 22, 12), U(2026, 10, 22, 17), U(2026, 10, 22, 22), U(2026, 10, 23, 3) }, thu); // 08, 13, 18, 23 EDT
    }

    [Fact] public void Off_DoesNotAlert_AndBuiltInsStay()
    {
        var a = new CustomAlert { Name = "x", Time = "08:00", Enabled = false };
        var b = Base();
        Assert.Same(b, CustomAlerts.With(b, new[] { a }));
        var on = new CustomAlert { Name = "y", Time = "08:00" };
        var merged = CustomAlerts.With(b, new[] { on });
        Assert.Single(b.Events); // the built-in schedule is not changed
        Assert.Equal(2, merged.Events.Count);
        Assert.Equal("custom:" + on.Id, merged.Events[1].Id);
    }

    [Fact] public void FallBack_NewYork_Nov1_2026_SameLocalTime()
    {
        var a = new CustomAlert { Name = "x", Time = "21:00" };
        var s = Starts(a, NaEast, U(2026, 10, 31, 12));
        Assert.Contains(U(2026, 11, 1, 1), s);  // Sat Oct 31 21:00 EDT
        Assert.Contains(U(2026, 11, 2, 2), s);  // Sun Nov 1 21:00 EST
    }

    [Fact] public void FallBack_LosAngeles_And_Berlin()
    {
        var a = new CustomAlert { Name = "x", Time = "20:00" };
        var la = Starts(a, NaWest, U(2026, 10, 31, 12));
        Assert.Contains(U(2026, 11, 1, 3), la); Assert.Contains(U(2026, 11, 2, 4), la);
        var eu = Starts(a, Eu, U(2026, 10, 24, 12));
        Assert.Contains(U(2026, 10, 24, 18), eu); Assert.Contains(U(2026, 10, 25, 19), eu); // CEST -> CET on Oct 25
    }

    [Fact] public void FallBack_RepeatingAlert_KeepsLocalSlots()
    {
        // every 6 h from 00:00: 00, 06, 12, 18 local on Nov 1 (the 01:00-02:00 repeat does not add or drop a slot)
        var a = new CustomAlert { Name = "x", Time = "00:00", RepeatHours = 6 };
        var s = Starts(a, NaEast, U(2026, 11, 1, 12));
        var nov1 = s.Where(t => t >= U(2026, 11, 1, 4) && t < U(2026, 11, 2, 5)).ToList();
        Assert.Equal(new[] { U(2026, 11, 1, 4), U(2026, 11, 1, 11), U(2026, 11, 1, 17), U(2026, 11, 1, 23) }, nov1);
    }

    [Fact] public void SpringForward_MissingTimeIsSkipped_LikeBuiltIns()
    {
        var a = new CustomAlert { Name = "x", Time = "02:30" };
        var s = Starts(a, NaEast, U(2026, 3, 8, 12));
        Assert.DoesNotContain(s, t => t >= U(2026, 3, 8, 5) && t < U(2026, 3, 8, 12)); // 02:30 doesn't exist on Mar 8
        Assert.Contains(U(2026, 3, 9, 6, 30), s); // Mar 9 02:30 EDT
    }

    [Fact] public void FiresThroughTheAlertPlanner_WithTheLeadTime()
    {
        var a = new CustomAlert { Name = "x", Time = "21:30" };
        var sched = CustomAlerts.With(Base(), new[] { a });
        var now = U(2026, 10, 22, 1, 25); // 5 min before 21:30 EDT
        var occ = ScheduleEngine.Occurrences(sched, NaEast, e => AlertTypes.Enabled(new Dictionary<string, bool>(), e), now);
        var due = AlertPlanner.Due(occ, now, new[] { 5, 0 }, new Dictionary<string, DateTime>());
        Assert.Contains(due, d => d.Occ.Event.Id == "custom:" + a.Id && d.LeadMinutes == 5);
        Assert.Null(due.First(d => CustomAlerts.IsCustom(d.Occ.Event)).Occ.Event.Voice); // speaks the generic clip
    }

    [Theory]
    [InlineData("21:30", "21:30")] [InlineData("9:05", "09:05")] [InlineData(" 2130 ", "21:30")] [InlineData("21.30", "21:30")]
    [InlineData("24:00", null)] [InlineData("9:5", null)] [InlineData("", null)] [InlineData("abc", null)]
    public void Time_Normalize(string input, string? expected) => Assert.Equal(expected, CustomAlerts.NormalizeTime(input));

    [Fact] public void Problem_PlainMessages()
    {
        Assert.NotNull(CustomAlerts.Problem("", "21:00", new[] { "Mon" }, false, false, ""));
        Assert.NotNull(CustomAlerts.Problem("x", "25:00", new[] { "Mon" }, false, false, ""));
        Assert.NotNull(CustomAlerts.Problem("x", "21:00", Array.Empty<string>(), false, false, ""));
        Assert.Null(CustomAlerts.Problem("x", "21:00", Array.Empty<string>(), true, false, ""));
        Assert.NotNull(CustomAlerts.Problem("x", "21:00", Array.Empty<string>(), true, true, "0"));
        Assert.NotNull(CustomAlerts.Problem("x", "21:00", Array.Empty<string>(), true, true, "24"));
        Assert.Null(CustomAlerts.Problem("x", "21:00", Array.Empty<string>(), true, true, "3"));
    }

    [Fact] public void Days_Normalize()
    {
        Assert.Equal(new[] { "daily" }, CustomAlerts.NormalizeDays(new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }));
        Assert.Equal(new[] { "daily" }, CustomAlerts.NormalizeDays(null));
        Assert.Equal(new[] { "Mon", "Sun" }, CustomAlerts.NormalizeDays(new[] { "sunday", "MON" }));
    }

    [Fact] public void Describe_Short()
    {
        Assert.Equal("21:30 \u00b7 every day", CustomAlerts.Describe(new CustomAlert { Time = "21:30" }));
        Assert.Equal("08:00 \u00b7 Mon, Thu \u00b7 every 3 h", CustomAlerts.Describe(new CustomAlert { Time = "8:00", Days = new() { "Thu", "Mon" }, RepeatHours = 3 }));
    }

    [Fact] public void Sanitize_RepairsWithoutLosingAlerts()
    {
        var list = new List<CustomAlert>
        {
            null!, new() { Id = "a", Name = "  ", Time = "7:30", RepeatHours = 99, Days = null! },
            new() { Id = "a", Name = new string('n', 60), Time = "08:00" }, new() { Name = "bad", Time = "nope" },
        };
        var s = CustomAlerts.Sanitize(list);
        Assert.Equal(2, s.Count);
        Assert.Equal("Custom alert", s[0].Name); Assert.Equal("07:30", s[0].Time); Assert.Equal(23, s[0].RepeatHours); Assert.Equal(new[] { "daily" }, s[0].Days);
        Assert.NotEqual("a", s[1].Id); Assert.Equal(CustomAlerts.MaxName, s[1].Name.Length);
    }
}

public class SettingsV8Tests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "wm-v8-" + Guid.NewGuid().ToString("N"));
    public SettingsV8Tests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    AppSettings LoadJson(string json) { var p = Path.Combine(_dir, "settings.json"); File.WriteAllText(p, json); return AppSettings.Load(p); }

    [Fact] public void Update_From107Earlier_SeesWelcomeOnce_KeepsEverything()
    {
        var s = LoadJson("{\"SettingsVersion\":7,\"Theme\":\"honey\",\"WarnMinutes\":7,\"FontScale\":1.15,\"Region\":\"EU\",\"AlertChoices\":{\"siege\":false}}");
        Assert.Equal(8, s.SettingsVersion);
        Assert.False(s.WelcomeShown);
        Assert.Empty(s.CustomAlerts);
        Assert.Equal("honey", s.Theme); Assert.Equal(7, s.WarnMinutes); Assert.Equal(1.15, s.FontScale, 3); Assert.Equal("EU", s.Region);
        Assert.False(s.AlertChoices["siege"]);
    }

    [Fact] public void NewInstall_SeesWelcome()
    {
        var s = AppSettings.Load(Path.Combine(_dir, "none.json"));
        Assert.False(s.WelcomeShown); Assert.Equal(8, s.SettingsVersion);
    }

    [Fact] public void WelcomeFlag_AndCustomAlerts_RoundTrip_ThroughSafeSave()
    {
        var p = Path.Combine(_dir, "settings.json");
        var s = new AppSettings { SettingsVersion = 8, WelcomeShown = true };
        s.CustomAlerts.Add(new CustomAlert { Name = "Guild run", Time = "21:30", Days = new() { "Fri" }, RepeatHours = 2, Enabled = false });
        s.SaveTo(p); s.SaveTo(p);
        Assert.True(File.Exists(SafeFile.BackupPath(p)));
        var back = AppSettings.Load(p);
        Assert.True(back.WelcomeShown);
        var a = Assert.Single(back.CustomAlerts);
        Assert.Equal(("Guild run", "21:30", 2, false), (a.Name, a.Time, a.RepeatHours, a.Enabled));
        Assert.Equal(new[] { "Fri" }, a.Days);
    }

    [Fact] public void NewerFile_WithWelcomeShown_IsNotShownAgain()
    {
        var s = LoadJson("{\"SettingsVersion\":9,\"WelcomeShown\":true,\"CustomAlerts\":null}");
        Assert.True(s.WelcomeShown); Assert.NotNull(s.CustomAlerts); Assert.Equal(9, s.SettingsVersion);
    }
}
