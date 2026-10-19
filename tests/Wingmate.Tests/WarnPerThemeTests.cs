using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

// AppSettings.ThemeWarnDefaults is static: these tests fill it the same way the app does and run in one collection.
[Collection("ThemeWarnDefaults")]
public class WarnPerThemeTests
{
    static WarnPerThemeTests()
    {
        string json = "";
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "assets", "themes", "themes.json");
            if (File.Exists(p)) { json = File.ReadAllText(p); break; }
        }
        foreach (var t in ThemeSet.Parse(json).All) AppSettings.ThemeWarnDefaults[t.Id] = "#" + t.Colors["warn"][3..];
    }

    static AppSettings Load(string json) { var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!; s.Migrate(); return s; }

    [Fact] public void EachThemeHasItsOwnDefault_DefaultThemeStaysRed()
    {
        var s = new AppSettings();
        Assert.Equal("#FF1E1E", s.ActiveWarnColor);
        Assert.True(WarnColors.IsDefault(s.WarnColorFor("current")));
        var all = new[] { "current", "elyos", "asmodian", "abyss", "midnight", "honey", "light" }.Select(s.WarnColorFor).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal("#FF1E1E", s.WarnColorFor("unknown-theme"));
    }

    [Fact] public void ChoicesAreStoredPerTheme_SwitchingRestoresThem()
    {
        var s = new AppSettings { Theme = "abyss" };
        string abyssDefault = s.ActiveWarnColor;
        s.SetWarnColor("#ff8a1e"); // a preset
        Assert.Equal("#FF8A1E", s.ActiveWarnColor);
        s.Theme = "honey"; Assert.Equal(AppSettings.DefaultWarnColor("honey"), s.ActiveWarnColor);
        s.SetWarnColor("#3DDC6A");
        s.Theme = "abyss"; Assert.Equal("#FF8A1E", s.ActiveWarnColor);
        s.ResetWarnColor(); Assert.Equal(abyssDefault, s.ActiveWarnColor);
        s.Theme = "honey"; Assert.Equal("#3DDC6A", s.ActiveWarnColor);
        s.SetWarnColor("nonsense"); Assert.Equal("#3DDC6A", s.ActiveWarnColor); // invalid ignored
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        Assert.DoesNotContain("\"WarnColor\"", json);
        var back = Load(json);
        Assert.Equal("#3DDC6A", back.ActiveWarnColor); Assert.Equal(abyssDefault, back.WarnColorFor("abyss"));
    }

    [Fact] public void Migration_CarriesTheOldColourToTheActiveThemeOnly()
    {
        var s = Load("{\"SettingsVersion\":5,\"Theme\":\"midnight\",\"WarnColor\":\"#A45CFF\"}");
        Assert.Equal("#A45CFF", s.ActiveWarnColor);
        Assert.Equal(AppSettings.DefaultWarnColor("elyos"), s.WarnColorFor("elyos"));
        Assert.Equal("#FF1E1E", s.WarnColorFor("current"));
        Assert.Null(s.WarnColor); Assert.Equal(6, s.SettingsVersion);
    }

    [Fact] public void Migration_UntouchedRedOrBadOrMissing_KeepsThemeDefault()
    {
        Assert.Equal(AppSettings.DefaultWarnColor("elyos"), Load("{\"SettingsVersion\":5,\"Theme\":\"elyos\",\"WarnColor\":\"#FF1E1E\"}").ActiveWarnColor);
        Assert.Equal(AppSettings.DefaultWarnColor("abyss"), Load("{\"SettingsVersion\":5,\"Theme\":\"abyss\",\"WarnColor\":\"garbage\"}").ActiveWarnColor);
        Assert.Equal("#FF1E1E", Load("{\"SettingsVersion\":5}").ActiveWarnColor);
        Assert.Equal("#FF1E1E", Load("{\"SettingsVersion\":5,\"ThemeWarnColor\":null}").ActiveWarnColor);
        // a pre-v5 file migrates opacity and colour in one go
        var old = Load("{\"SettingsVersion\":4,\"Theme\":\"honey\",\"WarnColor\":\"#2FD6E8\",\"Opacity\":0.7}");
        Assert.Equal("#2FD6E8", old.ActiveWarnColor); Assert.Equal(0.7, old.WindowOpacity);
    }

    [Fact] public void ThemesJson_EveryThemeHasAValidWarn()
    {
        foreach (var id in new[] { "current", "elyos", "asmodian", "abyss", "midnight", "honey", "light" })
            Assert.NotNull(WarnColors.TryParse(AppSettings.DefaultWarnColor(id)));
    }
}
