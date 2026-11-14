using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

public class CouponTests
{
    private static readonly string RepoFile = Path.Combine("..", "..", "..", "..", "..", "schedule", "coupons.json");
    private static CouponFile Bundled() => Coupons.TryParse(File.ReadAllText(RepoFile), out var err) ?? throw new Exception(err);
    private static readonly DateTime Expiry = new(2026, 11, 25, 6, 30, 0, DateTimeKind.Utc);

    [Fact] public void BundledList_HasDaevaDressUp()
    {
        var f = Bundled();
        var c = Assert.Single(f.Codes);
        Assert.Equal("DAEVADRESSUP", c.Code);
        Assert.Equal("DAEVADRESSUP", c.Id);
        Assert.Equal(new[] { "Skin Chest (10 times) x3", "Appearance Change Voucher (7 Days) x1", "Customization Voucher x5" }, c.Rewards);
        Assert.Contains("Once per account", c.Note);
        Assert.Contains("all servers", c.Note);
        Assert.Equal(Expiry, c.ExpiresUtc);
        Assert.Equal(DateTimeKind.Utc, c.ExpiresUtc!.Value.Kind);
        Assert.False(string.IsNullOrWhiteSpace(f.Version));
    }

    [Fact] public void BundledList_IsEmbeddedInTheApp()
    {
        var csproj = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "src", "Wingmate", "Wingmate.csproj"));
        Assert.Contains("schedule\\coupons.json\" LogicalName=\"coupons.json\"", csproj);
    }

    [Fact] public void OnlineUrl_IsTheRepoRawFile_NextToTheSchedule()
    {
        // the provider is WPF-side; check its constant from the source so the URL never drifts from the repo layout
        var src = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "src", "Wingmate", "Core", "CouponProvider.cs"));
        Assert.Contains("\"https://raw.githubusercontent.com/BeeSpecial3D/Wingmate/main/schedule/coupons.json\"", src);
        Assert.Contains("Timeout = TimeSpan.FromSeconds(15)", src);
    }

    [Fact] public void Active_UntilTheExpiryMoment_ThenNever()
    {
        var f = Bundled();
        Assert.Single(Coupons.Active(f, Expiry.AddDays(-10)));
        Assert.Single(Coupons.Active(f, Expiry.AddSeconds(-1)));
        Assert.Empty(Coupons.Active(f, Expiry));
        Assert.Empty(Coupons.Active(f, Expiry.AddDays(30)));
    }

    [Fact] public void New_SkipsDismissed_CaseInsensitive()
    {
        var f = Bundled();
        var now = Expiry.AddDays(-3);
        Assert.Single(Coupons.New(f, new List<string>(), now));
        Assert.Single(Coupons.New(f, null, now));
        Assert.Empty(Coupons.New(f, new[] { "DAEVADRESSUP" }, now));
        Assert.Empty(Coupons.New(f, new[] { "daevadressup" }, now));
        Assert.Empty(Coupons.New(f, new List<string>(), Expiry.AddMinutes(1))); // expired: never shows, seen or not
    }

    [Fact] public void StartsLater_NotShownYet()
    {
        var f = Coupons.TryParse("{\"formatVersion\":1,\"version\":\"1\",\"codes\":[{\"code\":\"SOON\",\"starts\":\"2026-12-01T00:00:00Z\"}]}", out _)!;
        Assert.Empty(Coupons.Active(f, new DateTime(2026, 11, 30, 23, 0, 0, DateTimeKind.Utc)));
        Assert.Single(Coupons.Active(f, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact] public void NoExpiry_StaysUntilRemoved()
    {
        var f = Coupons.TryParse("{\"formatVersion\":1,\"version\":\"1\",\"codes\":[{\"code\":\"FOREVER\"}]}", out _)!;
        Assert.Single(Coupons.Active(f, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("No end date announced", Coupons.ExpiryText(f.Codes[0], DateTime.UtcNow));
    }

    [Fact] public void Parse_OffsetsAreConvertedToUtc()
    {
        var f = Coupons.TryParse("{\"formatVersion\":1,\"version\":\"1\",\"codes\":[{\"code\":\"JP\",\"expires\":\"2026-11-25T15:30:00+09:00\"}]}", out _)!;
        Assert.Equal(Expiry, f.Codes[0].ExpiresUtc);
    }

    [Fact] public void Parse_DropsBadEntries_KeepsGoodOnes()
    {
        var json = "{\"formatVersion\":1,\"version\":\"1\",\"codes\":[null,{\"code\":\"  \"},{\"code\":\"BADDATE\",\"expires\":\"next tuesday\"},"
                 + "{\"code\":\" GOOD \",\"rewards\":[\"A x1\",\" \",null]},{\"id\":\"GOOD\",\"code\":\"DUPLICATE\"},{\"code\":\"" + new string('X', 41) + "\"}]}";
        var f = Coupons.TryParse(json, out var err)!;
        Assert.Equal("", err);
        var c = Assert.Single(f.Codes);
        Assert.Equal("GOOD", c.Code);
        Assert.Equal("GOOD", c.Id);
        Assert.Equal(new[] { "A x1" }, c.Rewards);
        Assert.Null(c.Note);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"formatVersion\":2,\"codes\":[]}")]
    [InlineData("null")]
    public void Parse_RejectsUnreadableFiles(string json)
    {
        Assert.Null(Coupons.TryParse(json, out var err));
        Assert.NotEqual("", err);
    }

    [Fact] public void Parse_EmptyListIsValid()
    {
        var f = Coupons.TryParse("{\"formatVersion\":1,\"version\":\"2\",\"codes\":[]}", out _);
        Assert.NotNull(f);
        Assert.Empty(Coupons.Active(f, DateTime.UtcNow));
    }

    [Fact] public void Parse_IgnoresUnknownFields()
    {
        var f = Coupons.TryParse("{\"formatVersion\":1,\"version\":\"2\",\"_about\":\"x\",\"codes\":[{\"code\":\"A\",\"region\":\"KR\"}]}", out _);
        Assert.Single(f!.Codes);
    }

    [Fact] public void Newer_PicksTheHigherVersion_KeepsFirstOnTie()
    {
        var a = new CouponFile { FormatVersion = 1, Version = "2026-11-14.1" };
        var b = new CouponFile { FormatVersion = 1, Version = "2026-11-20.1" };
        var c = new CouponFile { FormatVersion = 1, Version = "2026-11-14.1" };
        Assert.Same(b, Coupons.Newer(a, b));
        Assert.Same(b, Coupons.Newer(b, a));
        Assert.Same(a, Coupons.Newer(a, c));
        Assert.Same(a, Coupons.Newer(a, null));
        Assert.Same(b, Coupons.Newer(null, b));
    }

    [Fact] public void Remember_AppendsOnce_NeverRemoves()
    {
        var list = new List<string> { "OLD" };
        Assert.True(Coupons.Remember(list, new[] { "NEW", "NEW", "old", " " }));
        Assert.Equal(new[] { "OLD", "NEW" }, list);
        Assert.False(Coupons.Remember(list, new[] { "NEW" }));
    }

    [Fact] public void Remember_CapsVeryLongLists_DroppingOldestFirst()
    {
        var list = Enumerable.Range(0, Coupons.MaxRemembered).Select(i => "C" + i).ToList();
        Coupons.Remember(list, new[] { "LATEST" });
        Assert.Equal(Coupons.MaxRemembered, list.Count);
        Assert.Equal("LATEST", list[^1]);
        Assert.DoesNotContain("C0", list);
    }

    [Fact] public void ExpiryText_InTheUsersZone()
    {
        var c = Bundled().Codes[0];
        var ny = TimeZones.Find("America/New_York", "Eastern Standard Time");
        var t = Coupons.ExpiryText(c, new DateTime(2026, 11, 14, 19, 0, 0, DateTimeKind.Utc), ny);
        Assert.StartsWith("Expires Wed, Nov 25 at 1:30 AM", t);
        var tokyo = TimeZones.Find("Asia/Tokyo", "Tokyo Standard Time");
        Assert.StartsWith("Expires Wed, Nov 25 at 3:30 PM", Coupons.ExpiryText(c, new DateTime(2026, 11, 14, 19, 0, 0, DateTimeKind.Utc), tokyo));
        var la = TimeZones.Find("America/Los_Angeles", "Pacific Standard Time");
        Assert.StartsWith("Expires Tue, Nov 24 at 10:30 PM", Coupons.ExpiryText(c, new DateTime(2026, 11, 14, 19, 0, 0, DateTimeKind.Utc), la));
    }

    [Fact] public void ExpiryText_TodayAndTomorrow()
    {
        var c = Bundled().Codes[0];
        var ny = TimeZones.Find("America/New_York", "Eastern Standard Time");
        Assert.StartsWith("Expires tomorrow at 1:30 AM", Coupons.ExpiryText(c, new DateTime(2026, 11, 24, 20, 0, 0, DateTimeKind.Utc), ny));
        Assert.StartsWith("Expires today at 1:30 AM", Coupons.ExpiryText(c, new DateTime(2026, 11, 25, 5, 30, 0, DateTimeKind.Utc), ny));
    }

    [Fact] public void RedeemPath_IsTheInGameMenuPath()
        => Assert.Equal("Settings > Miscellaneous > Account > Enter Coupon", Coupons.RedeemWhere);

    // ---------------------------------------------------------------- settings
    private static AppSettings Load(string json) { var s = JsonSerializer.Deserialize<AppSettings>(json)!; s.Migrate(); return s; }

    [Fact] public void Settings_OlderFileHasNoDismissedCodes()
    {
        var s = Load("{\"SettingsVersion\":8}");
        Assert.NotNull(s.DismissedCoupons);
        Assert.Empty(s.DismissedCoupons);
    }

    [Fact] public void Settings_DismissedCodes_RoundTrip_AndAreRepaired()
    {
        var s = Load("{\"SettingsVersion\":9,\"DismissedCoupons\":[\"DAEVADRESSUP\",null,\" \",\"daevadressup\",\"OTHER\"]}");
        Assert.Equal(new[] { "DAEVADRESSUP", "OTHER" }, s.DismissedCoupons);
        var again = Load(JsonSerializer.Serialize(s));
        Assert.Equal(new[] { "DAEVADRESSUP", "OTHER" }, again.DismissedCoupons);
        Assert.Empty(Coupons.New(Bundled(), again.DismissedCoupons, Expiry.AddDays(-1)));
    }

    [Fact] public void Settings_NullDismissedList_IsRepaired()
    {
        var s = Load("{\"SettingsVersion\":9,\"DismissedCoupons\":null}");
        Assert.NotNull(s.DismissedCoupons);
    }
}
