using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class ResetTests
{
    static readonly TimeZoneInfo NY = TimeZones.Find("America/New_York");
    static DateTime U(int y, int mo, int d, int h, int mi = 0) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);
    static DateTime Daily(DateTime now) => ResetLogic.LastBoundary(ResetLogic.DefaultDaily, NY, now);
    static DateTime Weekly(DateTime now) => ResetLogic.LastBoundary(ResetLogic.DefaultWeekly, NY, now);

    [Fact] public void DailyBoundary_BeforeAndAfter0700Utc()
    {
        Assert.Equal(U(2026, 10, 18, 7), Daily(U(2026, 10, 19, 6, 59)));
        Assert.Equal(U(2026, 10, 19, 7), Daily(U(2026, 10, 19, 7, 0)));
        Assert.Equal(U(2026, 10, 19, 7), Daily(U(2026, 10, 19, 23)));
    }

    [Fact] public void WeeklyBoundary_IsWednesday0700Utc()
    {
        // 2026-10-21 is a Wednesday
        Assert.Equal(U(2026, 10, 14, 7), Weekly(U(2026, 10, 21, 6, 59)));
        Assert.Equal(U(2026, 10, 21, 7), Weekly(U(2026, 10, 21, 7)));
        Assert.Equal(U(2026, 10, 21, 7), Weekly(U(2026, 10, 27, 12)));
    }

    [Fact] public void UtcResetsIgnoreDst()
    {
        Assert.Equal(U(2026, 11, 2, 7), Daily(U(2026, 11, 2, 8))); // after US DST ends: still 07:00 UTC
    }

    [Fact] public void SchedulePerRegionResetIsHonoured()
    {
        var reg = new EventDef { Id = "daily_reset", Days = { "daily" }, Start = "04:00", Clock = "regional" };
        Assert.Equal(U(2026, 10, 19, 8), ResetLogic.LastBoundary(reg, NY, U(2026, 10, 19, 9))); // 04:00 EDT = 08:00 UTC
        var s = new ScheduleFile { Resets = { reg } };
        Assert.Same(reg, ResetLogic.Find(s, "daily_reset"));
        Assert.Equal("Wed", ResetLogic.Find(s, "weekly_reset").Days[0]); // missing -> default
    }

    [Fact] public void NextBoundary()
    {
        Assert.Equal(U(2026, 10, 20, 7), ResetLogic.NextBoundary(ResetLogic.DefaultDaily, NY, U(2026, 10, 19, 7, 0)));
        Assert.Equal(U(2026, 10, 21, 7), ResetLogic.NextBoundary(ResetLogic.DefaultWeekly, NY, U(2026, 10, 19, 12)));
    }

    static ChecklistState Checked()
    {
        var s = ChecklistState.CreateDefault();
        s.Sections.Add(new ChecklistSection { Kind = "custom", Name = "Mine", Items = { new ChecklistItem { Text = "x" } } });
        foreach (var sec in s.Sections) { foreach (var i in sec.Items) i.Done = true; sec.Hidden = true; }
        return s;
    }

    [Fact] public void FirstRun_RecordsBoundaryWithoutClearing()
    {
        var s = Checked();
        var now = U(2026, 10, 19, 12);
        Assert.True(ResetLogic.Apply(s, Daily(now), Weekly(now)));
        Assert.All(s.Sections.SelectMany(x => x.Items), i => Assert.True(i.Done));
        Assert.Equal(U(2026, 10, 19, 7), s.LastDailyReset);
        Assert.Equal(U(2026, 10, 14, 7), s.LastWeeklyReset);
    }

    [Fact] public void SamePeriod_NoChange()
    {
        var s = Checked(); var now = U(2026, 10, 19, 12);
        ResetLogic.Apply(s, Daily(now), Weekly(now));
        var later = U(2026, 10, 20, 6, 59);
        Assert.False(ResetLogic.Apply(s, Daily(later), Weekly(later)));
        Assert.True(s.Sections[0].Items.All(i => i.Done));
    }

    [Fact] public void DailyReset_ClearsAndUnhidesOnlyDailies()
    {
        var s = Checked(); var now = U(2026, 10, 19, 12);
        ResetLogic.Apply(s, Daily(now), Weekly(now));
        var next = U(2026, 10, 20, 7, 0);
        Assert.True(ResetLogic.Apply(s, Daily(next), Weekly(next)));
        var d = s.Sections.Single(x => x.Kind == "daily");
        Assert.True(d.Items.All(i => !i.Done)); Assert.False(d.Hidden);
        Assert.True(s.Sections.Single(x => x.Kind == "weekly").Items.All(i => i.Done));
        var c = s.Sections.Single(x => x.Kind == "custom");
        Assert.True(c.Items.All(i => i.Done)); Assert.True(c.Hidden); // custom never auto-resets
    }

    [Fact] public void CatchUp_AfterPcWasOffForDays()
    {
        var s = Checked(); var now = U(2026, 10, 19, 12); // Monday
        ResetLogic.Apply(s, Daily(now), Weekly(now));
        var back = U(2026, 10, 29, 2); // Thursday next week, before that day's reset
        Assert.True(ResetLogic.Apply(s, Daily(back), Weekly(back)));
        Assert.True(s.Sections.Where(x => !x.IsCustom).SelectMany(x => x.Items).All(i => !i.Done));
        Assert.Equal(U(2026, 10, 28, 7), s.LastDailyReset);
        Assert.Equal(U(2026, 10, 28, 7), s.LastWeeklyReset);
    }

    [Fact] public void ClosedOverWeeklyResetOnly_WeekliesClearOnNextOpen()
    {
        var s = Checked(); var t = U(2026, 10, 21, 6, 0); // Wed before reset
        ResetLogic.Apply(s, Daily(t), Weekly(t));
        var after = U(2026, 10, 21, 7, 30);
        ResetLogic.Apply(s, Daily(after), Weekly(after));
        Assert.True(s.Sections.Single(x => x.Kind == "weekly").Items.All(i => !i.Done));
    }

    [Fact] public void ClockMovedBack_NoClear()
    {
        var s = Checked(); var now = U(2026, 10, 25, 12);
        ResetLogic.Apply(s, Daily(now), Weekly(now));
        var earlier = U(2026, 10, 20, 12);
        Assert.False(ResetLogic.Apply(s, Daily(earlier), Weekly(earlier)));
        Assert.True(s.Sections[0].Items.All(i => i.Done));
    }
}

public class ChecklistMiscTests
{
    [Fact] public void SaveLoadRoundTrip_AndBuiltInsRestored()
    {
        var path = Path.Combine(Path.GetTempPath(), "wm-test-" + Guid.NewGuid().ToString("N") + ".json");
        var s = ChecklistState.CreateDefault();
        s.Sections[0].Items[0].Done = true;
        s.Sections.RemoveAll(x => x.Kind == "weekly");
        s.Save(path);
        var l = ChecklistState.Load(path);
        Assert.True(l.Sections.Single(x => x.Kind == "daily").Items[0].Done);
        Assert.Single(l.Sections, x => x.Kind == "weekly");
        File.WriteAllText(path, "{ not json");
        Assert.Equal(3, ChecklistState.Load(path).Sections.Count); // broken file -> defaults
        File.Delete(path);
    }

    [Fact] public void LinePools_NeverRepeatBackToBack()
    {
        var rng = new Random(1);
        foreach (var pool in CelebrationLines.Pools.Keys)
        {
            int prev = -1;
            for (int k = 0; k < 500; k++) { var i = CelebrationLines.Pick(pool, rng); Assert.NotEqual(prev, i); Assert.InRange(i, 0, CelebrationLines.Pools[pool].Length - 1); prev = i; }
        }
    }

    [Fact] public void Warps_NeverRepeatBackToBack_AndAllUsed()
    {
        var rng = new Random(2); int prev = -1; var seen = new bool[3];
        for (int k = 0; k < 300; k++) { var i = WarpPicker.Next(rng); Assert.NotEqual(prev, i); seen[i] = true; prev = i; }
        Assert.All(seen, Assert.True);
    }

    [Fact] public void PoolSelection_LongThresholdAppliesToAnySection()
    {
        var d = ChecklistState.DefaultSection("daily");
        Assert.Equal("daily", CelebrationLines.PoolFor(d, 10));
        Assert.Equal("long", CelebrationLines.PoolFor(d, d.Items.Count));
        var c = new ChecklistSection { Kind = "custom", Items = Enumerable.Range(0, 3).Select(i => new ChecklistItem()).ToList() };
        Assert.Equal("custom", CelebrationLines.PoolFor(c, 10));
    }

    [Fact] public void BundledScheduleHasResets()
    {
        var json = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "schedule", "schedule.json"));
        var s = ScheduleParser.TryParse(json, out _)!;
        Assert.Equal("07:00", ResetLogic.Find(s, "daily_reset").Start);
        Assert.Equal("Wed", ResetLogic.Find(s, "weekly_reset").Days[0]);
    }
}

namespace Wingmate.Tests
{
    public class ResetAllTests
    {
        [Xunit.Fact]
        public void ResetAll_UnchecksEverythingKeepsItemsAndUnhides()
        {
            var s = Wingmate.Core.ChecklistState.CreateDefault();
            s.Sections.Add(new Wingmate.Core.ChecklistSection { Name = "Mine", Kind = "custom", Items = { new Wingmate.Core.ChecklistItem { Text = "a", Done = true } } });
            foreach (var sec in s.Sections) { foreach (var i in sec.Items) i.Done = true; sec.Hidden = true; }
            int count = System.Linq.Enumerable.Sum(s.Sections, x => x.Items.Count);
            s.ResetAll();
            Xunit.Assert.All(s.Sections, sec => { Xunit.Assert.False(sec.Hidden); Xunit.Assert.All(sec.Items, i => Xunit.Assert.False(i.Done)); });
            Xunit.Assert.Equal(count, System.Linq.Enumerable.Sum(s.Sections, x => x.Items.Count));
        }
    }
}

namespace Wingmate.Tests
{
    public class AlertChoiceTests
    {
        private static Wingmate.Core.EventDef Ev(string id, string voice, string cat) => new() { Id = id, Voice = voice, Category = cat, Name = id };

        [Xunit.Fact]
        public void Defaults_AbyssOn_PveOff_AndChoicesWin()
        {
            var c = new System.Collections.Generic.Dictionary<string, bool>();
            Xunit.Assert.True(Wingmate.Core.AlertTypes.Enabled(c, Ev("siege", "siege", "pvp")));
            Xunit.Assert.False(Wingmate.Core.AlertTypes.Enabled(c, Ev("shugo", "shugo", "pve")));
            c["siege"] = false; c["shugo"] = true;
            Xunit.Assert.False(Wingmate.Core.AlertTypes.Enabled(c, Ev("siege", "siege", "pvp")));
            Xunit.Assert.True(Wingmate.Core.AlertTypes.Enabled(c, Ev("shugo", "shugo", "pve")));
            // two arena sessions share one toggle via the voice key
            c["arena"] = false;
            Xunit.Assert.False(Wingmate.Core.AlertTypes.Enabled(c, Ev("arena_evening", "arena", "pvp")));
        }

        [Xunit.Fact]
        public void Migrate_PveOnTurnsBothOn_AndPicksSmallestLead()
        {
            var s = new Wingmate.Core.AppSettings { SettingsVersion = 3, PveAlerts = true, LeadMinutes = new() { 10, 3 } };
            s.Migrate();
            Xunit.Assert.True(s.AlertChoices["shugo"]); Xunit.Assert.True(s.AlertChoices["invasion"]);
            Xunit.Assert.Equal(3, s.WarnMinutes);
            var t = new Wingmate.Core.AppSettings { SettingsVersion = 3, LeadMinutes = new() };
            t.Migrate();
            Xunit.Assert.Empty(t.AlertChoices); Xunit.Assert.Equal(5, t.WarnMinutes);
        }

        [Xunit.Fact]
        public void Label_DropsBracketDetail() =>
            Xunit.Assert.Equal("Siege Bosses", Wingmate.Core.AlertTypes.Label(new Wingmate.Core.EventDef { Id = "x", Name = "Siege Bosses (Executors Tamasa, Argo, Kaira)" }));
    }
}

namespace Wingmate.Tests
{
    public class WarnColorTests
    {
        [Xunit.Fact]
        public void Parse_ToleratesMissingAndBadValues()
        {
            Xunit.Assert.True(Wingmate.Core.WarnColors.IsDefault(null));
            Xunit.Assert.True(Wingmate.Core.WarnColors.IsDefault("not a color"));
            Xunit.Assert.Equal("#FF8800", Wingmate.Core.WarnColors.Normalize("ff8800"));
            Xunit.Assert.Equal("#FFAA00", Wingmate.Core.WarnColors.Normalize("#fa0"));
            var s = System.Text.Json.JsonSerializer.Deserialize<Wingmate.Core.AppSettings>("{\"Region\":\"EU\"}")!;
            Xunit.Assert.True(Wingmate.Core.WarnColors.IsDefault(s.ActiveWarnColor));
        }
    }
}
