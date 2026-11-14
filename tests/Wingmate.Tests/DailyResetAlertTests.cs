using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

public class DailyResetAlertTests
{
    private static ScheduleFile Bundled()
    {
        var json = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "schedule", "schedule.json"));
        return ScheduleParser.TryParse(json, out var err) ?? throw new Exception(err);
    }

    private static RegionDef NaEast(ScheduleFile s) => s.Regions.First(r => r.Id == "NA_East");

    [Fact] public void DailyReset_IsAdded_WithTheChecklistResetTime()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var d = s.Events.Single(e => e.Id == ResetAlerts.DailyId);
        var r = ResetLogic.Find(s, "daily_reset");
        Assert.Equal(r.Start, d.Start);
        Assert.Equal(r.Clock, d.Clock);
        Assert.Equal(r.Days, d.Days);
        Assert.Equal("daily_reset", d.Voice);
        Assert.Equal("Daily reset", d.Name);
        Assert.Equal(0, d.DurationMin);
    }

    [Fact] public void DailyReset_SitsJustBeforeWeeklyReset_AndOriginalIsUntouched()
    {
        var orig = Bundled();
        int before = orig.Events.Count;
        var s = ResetAlerts.WithDailyReset(orig);
        Assert.Equal(before, orig.Events.Count);
        Assert.DoesNotContain(orig.Events, e => e.Id == "daily_reset");
        int d = s.Events.FindIndex(e => e.Id == "daily_reset"), w = s.Events.FindIndex(e => e.Id == "weekly_reset");
        Assert.Equal(w - 1, d);
        Assert.Same(orig.Regions, s.Regions);
        Assert.Equal(orig.Version, s.Version); // online/bundled comparison is unaffected
    }

    [Fact] public void AddingTwice_DoesNotDuplicate()
    {
        var s = ResetAlerts.WithDailyReset(ResetAlerts.WithDailyReset(Bundled()));
        Assert.Single(s.Events, e => e.Id == "daily_reset");
    }

    [Fact] public void ScheduleThatAlreadyHasIt_IsKeptAsIs()
    {
        var b = Bundled();
        b.Events.Add(new EventDef { Id = "daily_reset", Name = "Daily reset", Category = "reset", Days = { "daily" }, Start = "08:00", Clock = "utc" });
        Assert.Same(b, ResetAlerts.WithDailyReset(b));
    }

    [Fact] public void FollowsAChangedResetTime()
    {
        var b = Bundled();
        b.Resets = new List<EventDef> { new() { Id = "daily_reset", Name = "Daily reset", Days = { "daily" }, Start = "09:00", Clock = "regional" } };
        var d = ResetAlerts.WithDailyReset(b).Events.Single(e => e.Id == "daily_reset");
        Assert.Equal("09:00", d.Start);
        Assert.Equal("regional", d.Clock);
    }

    [Fact] public void NoResetsInFile_UsesDefault0700Utc()
    {
        var b = Bundled(); b.Resets = new();
        var d = ResetAlerts.WithDailyReset(b).Events.Single(e => e.Id == "daily_reset");
        Assert.Equal("07:00", d.Start);
        Assert.Equal("utc", d.Clock);
    }

    [Fact] public void IsOnByDefault_InTheGeneralGroup()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var d = s.Events.Single(e => e.Id == "daily_reset");
        Assert.True(AlertTypes.DefaultOn(d));
        Assert.True(AlertTypes.Enabled(new Dictionary<string, bool>(), d));
        Assert.False(AlertTypes.Enabled(new Dictionary<string, bool> { ["daily_reset"] = false }, d));
        var general = AlertTypes.Grouped(s).Single(g => g.Group == AlertTypes.GeneralGroup).Items;
        Assert.Contains(general, t => t.Key == "daily_reset" && t.Label == "Daily reset");
        Assert.Contains(general, t => t.Key == "weekly_reset");
    }

    [Fact] public void OccursEveryDay_AtTheChecklistBoundary()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var now = new DateTime(2026, 11, 14, 19, 0, 0, DateTimeKind.Utc); // Sat
        var occ = ScheduleEngine.Occurrences(s, NaEast(s), e => e.Id == "daily_reset", now);
        var next = occ.First(o => o.StartUtc > now);
        Assert.Equal(new DateTime(2026, 11, 15, 7, 0, 0, DateTimeKind.Utc), next.StartUtc);
        var tz = TimeZones.Find("America/New_York", "Eastern Standard Time");
        Assert.Equal(ResetLogic.NextBoundary(ResetLogic.Find(s, "daily_reset"), tz, now), next.StartUtc);
        var days = occ.Where(o => o.StartUtc > now).Take(7).Select(o => o.StartUtc.Date).Distinct().Count();
        Assert.Equal(7, days);
    }

    [Fact] public void Alerts_AtTheWarningTime_WithItsOwnVoice()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var at = new DateTime(2026, 11, 15, 6, 55, 0, DateTimeKind.Utc); // 5 minutes before
        var occ = ScheduleEngine.Occurrences(s, NaEast(s), e => e.Id == "daily_reset", at);
        var due = AlertPlanner.Due(occ, at, new[] { 5 }, new Dictionary<string, DateTime>());
        var d = Assert.Single(due);
        Assert.Equal("daily_reset", d.Occ.Event.Voice);
        Assert.Equal(5, d.LeadMinutes);
    }

    [Fact] public void VoiceClip_IsBundled()
    {
        var wav = Path.Combine("..", "..", "..", "..", "..", "src", "Wingmate", "Assets", "voice", "daily_reset.wav");
        Assert.True(File.Exists(wav));
        var b = File.ReadAllBytes(wav);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(b, 0, 4));
        Assert.Equal(24000, BitConverter.ToInt32(b, 24)); // same rate as the other clips
    }

    [Fact] public void WeeklyResetDay_OnlyWeeklyIsAnnounced()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var at = new DateTime(2026, 11, 18, 6, 55, 0, DateTimeKind.Utc); // Wed, 5 min before
        var occ = ResetAlerts.DropDailyAtWeekly(ScheduleEngine.Occurrences(s, NaEast(s), e => AlertTypes.Key(e) is "daily_reset" or "weekly_reset", at));
        var due = AlertPlanner.Due(occ, at, new[] { 5 }, new Dictionary<string, DateTime>());
        var d = Assert.Single(due);
        Assert.Equal("weekly_reset", d.Occ.Event.Id);
    }

    [Fact] public void WeeklyResetDay_WeeklyOff_DailyStillAnnounced()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var at = new DateTime(2026, 11, 18, 6, 55, 0, DateTimeKind.Utc);
        var occ = ResetAlerts.DropDailyAtWeekly(ScheduleEngine.Occurrences(s, NaEast(s), e => e.Id == "daily_reset", at));
        var due = AlertPlanner.Due(occ, at, new[] { 5 }, new Dictionary<string, DateTime>());
        Assert.Equal("daily_reset", Assert.Single(due).Occ.Event.Id);
    }

    [Fact] public void OtherDays_DailyIsNotDropped()
    {
        var s = ResetAlerts.WithDailyReset(Bundled());
        var at = new DateTime(2026, 11, 16, 6, 55, 0, DateTimeKind.Utc); // Mon
        var all = ScheduleEngine.Occurrences(s, NaEast(s), e => AlertTypes.Key(e) is "daily_reset" or "weekly_reset", at);
        var kept = ResetAlerts.DropDailyAtWeekly(all);
        var weekly = all.Where(o => o.Event.Id == "weekly_reset").Select(o => o.StartUtc).ToHashSet();
        Assert.NotEmpty(weekly);
        Assert.All(kept.Where(o => o.Event.Id == "daily_reset"), o => Assert.DoesNotContain(o.StartUtc, weekly));
        Assert.Equal(all.Count(o => o.Event.Id == "daily_reset" && !weekly.Contains(o.StartUtc)), kept.Count(o => o.Event.Id == "daily_reset"));
        Assert.Contains(kept, o => o.Event.Id == "daily_reset" && o.StartUtc == new DateTime(2026, 11, 16, 7, 0, 0, DateTimeKind.Utc)); // Monday kept
        Assert.Equal(weekly.Count, kept.Count(o => o.Event.Id == "weekly_reset"));
    }

    // ---------------------------------------------------------------- settings migration (append-only)
    private static AppSettings Load(string json) { var s = JsonSerializer.Deserialize<AppSettings>(json)!; s.Migrate(); return s; }

    [Fact] public void Migration_AddsDailyResetOn_KeepsEverythingElse()
    {
        var s = Load("{\"SettingsVersion\":8,\"Region\":\"NA_West\",\"WarnMinutes\":10,\"Theme\":\"abyss\",\"AlertChoices\":{\"siege\":false,\"shugo\":true,\"weekly_reset\":false},"
            + "\"CustomAlerts\":[{\"Id\":\"a1\",\"Name\":\"Boss\",\"Time\":\"21:30\",\"Days\":[\"Mon\"],\"Enabled\":true}]}");
        Assert.Equal(9, s.SettingsVersion);
        Assert.True(s.AlertChoices["daily_reset"]);
        Assert.False(s.AlertChoices["siege"]);
        Assert.True(s.AlertChoices["shugo"]);
        Assert.False(s.AlertChoices["weekly_reset"]);
        Assert.Equal(4, s.AlertChoices.Count);
        Assert.Equal("NA_West", s.Region);
        Assert.Equal(10, s.WarnMinutes);
        Assert.Equal("abyss", s.Theme);
        Assert.Equal("Boss", Assert.Single(s.CustomAlerts).Name);
    }

    [Fact] public void Migration_KeepsAnExistingChoice()
    {
        var s = Load("{\"SettingsVersion\":8,\"AlertChoices\":{\"daily_reset\":false}}");
        Assert.False(s.AlertChoices["daily_reset"]);
    }

    [Fact] public void Migration_RunsOnce_TurningItOffLaterSticks()
    {
        var s = Load("{\"SettingsVersion\":8}");
        s.AlertChoices["daily_reset"] = false;
        var again = Load(JsonSerializer.Serialize(s));
        Assert.False(again.AlertChoices["daily_reset"]);
        Assert.Equal(9, again.SettingsVersion);
    }

    [Fact] public void NewUser_HasItOn()
    {
        var s = new AppSettings();
        s.Migrate();
        Assert.True(s.AlertChoices["daily_reset"]);
    }

    [Fact] public void Migration_NullAlertChoices_IsRepaired()
    {
        var s = Load("{\"SettingsVersion\":8,\"AlertChoices\":null}");
        Assert.True(s.AlertChoices["daily_reset"]);
    }
}
