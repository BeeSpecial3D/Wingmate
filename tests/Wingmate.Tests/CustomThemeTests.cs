using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

public class CustomThemeTests
{
    static ThemeSet Set() => ThemeSet.Parse(ThemeTests.Json());

    [Fact] public void Groups_UseRealTokens_EachTokenOnce_UniqueKeys()
    {
        var all = CustomTheme.Groups.SelectMany(g => g.Tokens).ToList();
        Assert.All(all, t => Assert.Contains(t, ThemeSet.Tokens));
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(CustomTheme.Groups.Length, CustomTheme.Groups.Select(g => g.Key).Distinct().Count());
        Assert.DoesNotContain("warn", all); // the warning colour keeps its own per-theme setting
    }

    [Fact] public void NoPicks_LooksExactlyLikeTheStartTheme()
    {
        foreach (var t in Set().All)
        {
            var c = CustomTheme.Build(t, new Dictionary<string, string>());
            Assert.Equal("custom", c.Id);
            foreach (var tok in ThemeSet.Tokens) Assert.Equal(t.Colors[tok], c.Colors[tok]);
        }
    }

    [Fact] public void Snapshot_ThenBuild_ReproducesTheTheme()
    {
        foreach (var t in Set().All)
        {
            var c = CustomTheme.Build(Set().Default, CustomTheme.Snapshot(t)); // start theme doesn't matter for the picked groups' main tokens
            foreach (var g in CustomTheme.Groups) Assert.Equal(CustomTheme.ColorOf(t, g), CustomTheme.ColorOf(c, g));
        }
    }

    [Fact] public void Pick_SetsEveryTokenOfTheGroup_KeepingEachTokensAlpha()
    {
        var start = Set().Default;
        var c = CustomTheme.Build(start, new Dictionary<string, string> { ["windowBg"] = "#112233", ["accent"] = "abc" });
        foreach (var tok in CustomTheme.Find("windowBg")!.Tokens)
        {
            Assert.EndsWith("112233", c.Colors[tok]);
            Assert.Equal(start.Colors[tok].Substring(1, 2), c.Colors[tok].Substring(1, 2));
        }
        Assert.EndsWith("AABBCC", c.Colors["pillOnBg"]);
        Assert.Equal(start.Colors["text"], c.Colors["text"]); // untouched group
    }

    [Fact] public void BadPicks_AreIgnored()
    {
        var start = Set().Default;
        var c = CustomTheme.Build(start, new Dictionary<string, string> { ["windowBg"] = "nope", ["unknownKey"] = "#123456" });
        Assert.Equal(start.Colors["panelBg"], c.Colors["panelBg"]);
    }

    [Fact] public void StartFrom_CopiesColoursWarningAndOpacity_AndIgnoresCustomItself()
    {
        var elyos = Set().Get("elyos");
        var s = new AppSettings { Theme = "elyos" };
        s.Migrate();
        s.SetWarnColor("#00FF00");
        s.SetOpacity(0.7);
        s.StartCustomFrom(elyos);
        Assert.Equal("elyos", s.CustomBase);
        Assert.Equal(CustomTheme.Snapshot(elyos), s.CustomColors);
        Assert.Equal("#00FF00", s.WarnColorFor("custom"));
        Assert.Equal(0.7, s.OpacityFor("custom"), 3);
        var before = new Dictionary<string, string>(s.CustomColors);
        s.StartCustomFrom(CustomTheme.Build(elyos, s.CustomColors)); // Custom -> Custom does nothing
        Assert.Equal(before, s.CustomColors);
        Assert.Equal("elyos", s.CustomBase);
    }

    [Fact] public void OldSettingsFile_LoadsUnchanged_AndGetsV7Defaults()
    {
        var json = "{\"Region\":\"NA_East\",\"Theme\":\"honey\",\"SettingsVersion\":6,\"ThemeWarnColor\":{\"honey\":\"#123456\"},\"ThemeOpacity\":{\"honey\":0.9},\"Left\":100,\"Top\":50}";
        var s = JsonSerializer.Deserialize<AppSettings>(json)!;
        s.Migrate();
        Assert.Equal(8, s.SettingsVersion); // v8 (1.0.7) adds nothing to convert
        Assert.Equal("honey", s.Theme);
        Assert.Equal("#123456", s.WarnColorFor("honey"));
        Assert.Equal(0.9, s.OpacityFor("honey"), 3);
        Assert.Equal(100, s.Left);
        Assert.Equal("current", s.CustomBase);
        Assert.Empty(s.CustomColors);
        Assert.Null(s.LauncherLeft); Assert.Null(s.SettingsLeft); // first run after the update: launcher centred, Settings beside it
    }

    [Fact] public void SavedCustomColours_SurviveSaveAndLoad_BadOnesDropped()
    {
        var s = new AppSettings { SettingsVersion = 7, CustomBase = "abyss" };
        s.CustomColors = new() { ["windowBg"] = "#0a0b0c", ["check"] = "zzz", ["bogus"] = "#FFFFFF" };
        s.LauncherLeft = 10; s.LauncherTop = 20; s.SettingsLeft = 30; s.SettingsTop = 40;
        var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
        back.Migrate();
        Assert.Equal(new Dictionary<string, string> { ["windowBg"] = "#0A0B0C" }, back.CustomColors);
        Assert.Equal("abyss", back.CustomBase);
        Assert.Equal((10.0, 20.0, 30.0, 40.0), (back.LauncherLeft!.Value, back.LauncherTop!.Value, back.SettingsLeft!.Value, back.SettingsTop!.Value));
    }

    [Fact] public void SetCustomColor_ValidatesKeyAndHex()
    {
        var s = new AppSettings();
        s.SetCustomColor("text", "#ff8800");
        s.SetCustomColor("text2", "#ff8800");
        s.SetCustomColor("check", "xyz");
        Assert.Equal(new Dictionary<string, string> { ["text"] = "#FF8800" }, s.CustomColors);
    }
}

public class StartupWindowsTests
{
    [Fact] public void FirstRun_HasNoSavedSet()
    {
        var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"SettingsVersion\":6}")!;
        s.Migrate();
        Assert.Null(s.StartupWindows);
        Assert.DoesNotContain("OpenAtExit", System.Text.Json.JsonSerializer.Serialize(s)); // not written until a real quit
    }

    [Fact] public void SavedSet_RoundTrips_UnknownNamesDropped()
    {
        var s = new AppSettings { OpenAtExit = new() { "launcher", "Checklist", "settings", "bogus", "checklist" } };
        var back = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(s))!;
        Assert.Equal(new[] { "launcher", "checklist", "settings" }, back.StartupWindows);
    }

    [Fact] public void EmptySavedSet_IsKept_AsEmpty()
    {
        var s = new AppSettings { OpenAtExit = new() };
        Assert.NotNull(s.StartupWindows);
        Assert.Empty(s.StartupWindows!);
    }
}
