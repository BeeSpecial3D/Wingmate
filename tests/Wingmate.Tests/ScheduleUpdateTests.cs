using System;
using System.IO;
using System.Linq;
using System.Text;
using Wingmate.Core;
using Xunit;

/// <summary>1.0.9 schedule: siege days, Argo the Spirit King, and the stale rift DST note.</summary>
public class ScheduleUpdateTests
{
    static string Repo(params string[] parts) => Path.Combine(new[] { "..", "..", "..", "..", ".." }.Concat(parts).ToArray());

    static ScheduleFile Bundled()
    {
        var json = File.ReadAllText(Repo("schedule", "schedule.json"));
        return ScheduleParser.TryParse(json, out var err) ?? throw new Exception(err);
    }

    static RegionDef NaEast(ScheduleFile s) => s.Regions.Single(r => r.Id == "NA_East");

    [Fact] public void SiegeAndSiegeBosses_AreThursdayAndSaturdayOnly()
    {
        var s = Bundled();
        foreach (var id in new[] { "siege", "siege_bosses" })
        {
            var ev = s.Events.Single(e => e.Id == id);
            Assert.Equal(new[] { "Thu", "Sat" }, ev.Days);
            Assert.DoesNotContain("Mon", ev.Days);
            Assert.Equal("regional", ev.Clock);
        }
        Assert.Equal("21:00", s.Events.Single(e => e.Id == "siege").Start);
        Assert.Equal("21:30", s.Events.Single(e => e.Id == "siege_bosses").Start);
    }

    [Fact] public void Rift_HasNoStaleDstNote()
    {
        var ev = Bundled().Events.Single(e => e.Id == "rift");
        Assert.True(string.IsNullOrEmpty(ev.Note));
        Assert.DoesNotContain("DST", ev.Note ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Argo_IsDailyAt1030And2230Regional()
    {
        var s = Bundled();
        var ev = s.Events.Single(e => e.Id == "argo");
        Assert.Equal("Argo, the Spirit King", ev.Name);
        Assert.Equal("argo", ev.Voice);
        Assert.Equal("pvp", ev.Category);
        Assert.Equal("10:30", ev.Start);
        Assert.Equal(720, ev.RepeatEveryMin);
        Assert.Equal("regional", ev.Clock);
        Assert.Contains("daily", ev.Days, StringComparer.OrdinalIgnoreCase);
        Assert.False(ev.Optional);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var now = new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc); // Sat afternoon ET
        var occ = ScheduleEngine.Occurrences(s, NaEast(s), e => e.Id == "argo", now);
        var times = occ
            .Select(o => TimeZoneInfo.ConvertTimeFromUtc(o.StartUtc, tz))
            .Where(l => l.Date == new DateTime(2026, 11, 14))
            .Select(l => l.ToString("HH:mm"))
            .ToList();
        Assert.Equal(new[] { "10:30", "22:30" }, times);
    }

    [Fact] public void ArgoVoiceClip_IsBundled()
    {
        var wav = Repo("src", "Wingmate", "Assets", "voice", "argo.wav");
        Assert.True(File.Exists(wav), wav);
        var b = File.ReadAllBytes(wav);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(b, 0, 4));
        Assert.Equal(24000, BitConverter.ToInt32(b, 24));
    }

    [Fact] public void ScheduleVersion_WasBumped()
    {
        var s = Bundled();
        Assert.Equal("2026-11-14.1", s.Version);
        Assert.StartsWith("2026-11-14", s.Updated);
    }
}
