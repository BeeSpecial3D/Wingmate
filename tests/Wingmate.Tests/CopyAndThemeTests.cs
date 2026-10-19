using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class ListCopyTests
{
    static ChecklistSection Sec(string kind, string name, params (string T, bool D)[] items)
    {
        var s = new ChecklistSection { Kind = kind, Name = name };
        foreach (var (t, d) in items) s.Items.Add(new ChecklistItem { Text = t, Done = d });
        return s;
    }
    static ChecklistState State(params ChecklistSection[] secs) { var st = new ChecklistState(); st.Sections.Clear(); st.Sections.AddRange(secs); return st; }

    [Fact] public void SameName_MergesUnchecked_SkippingDuplicatesCaseAndSpaces()
    {
        var src = Sec("daily", "Dailies", ("Abyss quest", true), ("  shugo DELIVERY ", true), ("Gathering", false));
        var target = State(Sec("daily", " dailies ", ("Shugo delivery", true)));
        var r = ListCopy.CopyInto(src, target);
        Assert.Equal(2, r.Added); Assert.Equal(1, r.Skipped); Assert.False(r.Created);
        var d = Assert.Single(target.Sections);
        Assert.Equal(new[] { "Shugo delivery", "Abyss quest", "Gathering" }, d.Items.Select(i => i.Text));
        Assert.True(d.Items[0].Done);                       // existing item untouched
        Assert.All(d.Items.Skip(1), i => Assert.False(i.Done)); // copied items arrive unchecked
    }

    [Fact] public void NoSameName_CreatesNewListOfSameKind_WithFreshIds()
    {
        var src = Sec("weekly", "Weekly bosses", ("Boss A", true), ("Boss B", false));
        var target = State(Sec("daily", "Dailies"));
        var r = ListCopy.CopyInto(src, target);
        Assert.True(r.Created); Assert.Equal(2, r.Added);
        var n = target.Sections[1];
        Assert.Equal("weekly", n.Kind); Assert.Equal("Weekly bosses", n.Name);
        Assert.All(n.Items, i => Assert.False(i.Done));
        Assert.Empty(n.Items.Select(i => i.Id).Intersect(src.Items.Select(i => i.Id)));
        Assert.NotEqual(src.Id, n.Id);
        Assert.True(src.Items[0].Done); // source never changes
    }

    [Fact] public void DuplicatesWithinSource_AndBlankItems_AreSkipped()
    {
        var src = Sec("custom", "Farming", ("Ore", false), ("ore ", false), ("   ", false));
        var target = State();
        var r = ListCopy.CopyInto(src, target);
        Assert.Equal(1, r.Added); Assert.Equal(1, r.Skipped);
        Assert.Equal("Ore", Assert.Single(Assert.Single(target.Sections).Items).Text);
    }

    [Fact] public void MergingNewItems_ShowsAHiddenCompletedList_NothingNewKeepsItHidden()
    {
        var hidden = Sec("custom", "Farming", ("Ore", true)); hidden.Hidden = true;
        var target = State(hidden);
        ListCopy.CopyInto(Sec("custom", "Farming", ("Ore", false)), target);
        Assert.True(hidden.Hidden);
        ListCopy.CopyInto(Sec("custom", "Farming", ("Herbs", false)), target);
        Assert.False(hidden.Hidden);
    }
}

public class ThemeTests
{
    static string Json()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "assets", "themes", "themes.json");
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        throw new FileNotFoundException("assets/themes/themes.json");
    }

    [Fact] public void AllSevenThemes_ResolveEveryToken_ToValidColours()
    {
        var set = ThemeSet.Parse(Json());
        Assert.Equal(new[] { "current", "elyos", "asmodian", "abyss", "midnight", "honey", "light" }, set.All.Select(t => t.Id));
        foreach (var t in set.All)
            foreach (var tok in ThemeSet.Tokens)
            {
                Assert.True(t.Colors.TryGetValue(tok, out var c), $"{t.Id}.{tok} missing");
                Assert.NotNull(ThemeSet.Normalize(c));
                Assert.NotEqual("#FFFF00FF", c);
            }
    }

    [Fact] public void Default_IsExactlyTodaysLook()
    {
        var c = ThemeSet.Parse(Json()).Default.Colors;
        Assert.Equal("#E6141A24", c["panelBg"]); Assert.Equal("#55A0B4D2", c["panelBorder"]); Assert.Equal("#FFFFD58A", c["title"]);
        Assert.Equal("#FFE6EEF7", c["text"]); Assert.Equal("#AAE6EEF7", c["iconStroke"]); Assert.Equal("#F2141A24", c["menuBg"]);
        Assert.Equal("#22FFFFFF", c["menuHover"]); Assert.Equal("#33E6EEF7", c["menuSep"]); Assert.Equal("#77E6EEF7", c["menuDisabled"]);
        Assert.Equal("#FF7CD992", c["check"]); Assert.Equal("#FF7FB8FF", c["counter"]); Assert.Equal("#3348A0FF", c["onBg"]);
        Assert.Equal("#55FFD58A", c["primaryHover"]); Assert.Equal("#CCFFD58A", c["subTitle"]); Assert.Equal("#FF141A24", c["thumbStroke"]);
        Assert.Equal("#55000000", c["inputBg"]); Assert.Equal("#FFFFFFFF", c["caret"]); Assert.Equal("#CCFFFFFF", c["gemOutline"]);
        Assert.Equal("#FFFFD58A", c["soonTitle"]); Assert.Equal("#AAE6EEF7", c["soonIcon"]); Assert.Equal("#CC141A24", c["soonInsetBg"]);
        Assert.Equal("#66E6EEF7", c["grip"]); Assert.Equal("#14FFFFFF", c["rowHover"]); Assert.Equal("#FFFF9A9A", c["error"]);
    }

    [Fact] public void UnknownOrMissingTheme_FallsBackToCurrent()
    {
        var set = ThemeSet.Parse(Json());
        Assert.Equal("current", set.Get(null).Id);
        Assert.Equal("current", set.Get("").Id);
        Assert.Equal("current", set.Get("neon-pink").Id);
        Assert.Equal("honey", set.Get(" Honey ").Id);
    }

    [Fact] public void JudgmentCalls_FromTheMockups()
    {
        var set = ThemeSet.Parse(Json());
        foreach (var id in new[] { "elyos", "asmodian", "light" }) Assert.Equal("#FFFFFFFF", set.Get(id).Colors["soonTitle"]);
        Assert.True(ThemeSet.IsLight(set.Get("elyos"))); Assert.True(ThemeSet.IsLight(set.Get("light"))); Assert.False(ThemeSet.IsLight(set.Default));
        foreach (var t in set.All) // checks stay green
        {
            var h = t.Colors["check"]; int r = Convert.ToInt32(h.Substring(3, 2), 16), g = Convert.ToInt32(h.Substring(5, 2), 16), b = Convert.ToInt32(h.Substring(7, 2), 16);
            Assert.True(g > r && g > b, t.Id);
        }
        Assert.NotEqual(set.Default.Colors["gemOutline"], set.Get("light").Colors["gemOutline"]); // darker diamond outline on light themes
        Assert.NotEqual(set.Default.Colors["soonIcon"], set.Get("light").Colors["soonIcon"]);     // readable × on the red panel
    }

    [Fact] public void OlderSettingsFile_WithoutThemeOrQuitPrompt_GetsDefaults()
    {
        var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Region\":\"na\",\"Muted\":true}")!;
        Assert.Equal("current", s.Theme); Assert.True(s.AskBeforeQuit);
    }
}

public class OpacityTests
{
    static AppSettings Load(string json) { var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!; s.Migrate(); return s; }

    [Fact] public void Defaults_Elyos80_Others100()
    {
        var s = new AppSettings();
        Assert.Equal(1.0, s.WindowOpacity);
        foreach (var id in new[] { "current", "asmodian", "abyss", "honey", "midnight", "light", "unknown", "", null })
            Assert.Equal(1.0, s.OpacityFor(id));
        Assert.Equal(0.8, s.OpacityFor("elyos"));
        Assert.Equal(0.8, s.OpacityFor(" Elyos "));
    }

    [Fact] public void EachThemeKeepsItsOwnValue_SwitchingAppliesIt()
    {
        var s = new AppSettings { Theme = "abyss" };
        s.SetOpacity(0.65);
        Assert.Equal(0.65, s.WindowOpacity);
        s.Theme = "elyos"; Assert.Equal(0.8, s.WindowOpacity); // untouched theme: its default
        s.SetOpacity(0.7);
        s.Theme = "current"; Assert.Equal(1.0, s.WindowOpacity);
        s.Theme = "abyss"; Assert.Equal(0.65, s.WindowOpacity);
        s.Theme = "elyos"; Assert.Equal(0.7, s.WindowOpacity);
        // survives a save/load round trip, old per-window fields are not written back
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        Assert.DoesNotContain("LauncherOpacity", json); Assert.DoesNotContain("ChecklistOpacity", json); Assert.DoesNotContain("\"Opacity\"", json);
        var back = Load(json);
        Assert.Equal(0.7, back.WindowOpacity); Assert.Equal(0.65, back.OpacityFor("abyss"));
    }

    [Fact] public void SetOpacity_ClampsTo40_100()
    {
        var s = new AppSettings();
        s.SetOpacity(0.1); Assert.Equal(0.4, s.WindowOpacity);
        s.SetOpacity(1.7); Assert.Equal(1.0, s.WindowOpacity);
        s.SetOpacity(double.NaN); Assert.Equal(1.0, s.WindowOpacity);
        s.ThemeOpacity["current"] = 0.05; Assert.Equal(0.4, s.WindowOpacity); // hand-edited file
    }

    [Fact] public void Migration_TakesHighestPerWindowValue_ForSelectedTheme()
    {
        var s = Load("{\"SettingsVersion\":4,\"Theme\":\"abyss\",\"Opacity\":0.6,\"LauncherOpacity\":0.9,\"ChecklistOpacity\":0.7}");
        Assert.Equal(0.9, s.OpacityFor("abyss"));
        Assert.Equal(0.9, s.WindowOpacity);
        Assert.Equal(1.0, s.OpacityFor("current")); Assert.Equal(0.8, s.OpacityFor("elyos")); // others keep their defaults
        Assert.Null(s.Opacity); Assert.Null(s.LauncherOpacity); Assert.Null(s.ChecklistOpacity);
        Assert.True(s.SettingsVersion >= 5);
    }

    [Fact] public void Migration_ToleratesMissingFieldsAndTheme()
    {
        var a = Load("{\"SettingsVersion\":4,\"ChecklistOpacity\":0.5}"); // no Theme, only one opacity
        Assert.Equal(0.5, a.OpacityFor("current")); Assert.Equal(0.5, a.WindowOpacity);
        var b = Load("{\"SettingsVersion\":4}"); // nothing at all
        Assert.Empty(b.ThemeOpacity); Assert.Equal(1.0, b.WindowOpacity);
        var c = Load("{\"SettingsVersion\":4,\"ThemeOpacity\":null,\"Opacity\":0.2}"); // null map, below the clamp
        Assert.Equal(0.4, c.WindowOpacity);
    }

    [Fact] public void Migration_Untouched100_KeepsThemeDefault_SoElyosStays80()
    {
        var s = Load("{\"SettingsVersion\":4,\"Theme\":\"elyos\",\"Opacity\":1.0,\"LauncherOpacity\":1.0,\"ChecklistOpacity\":1.0}");
        Assert.Equal(0.8, s.WindowOpacity);
        var t = Load("{\"SettingsVersion\":4,\"Theme\":\"elyos\",\"Opacity\":0.9,\"LauncherOpacity\":1.0,\"ChecklistOpacity\":0.5}");
        Assert.Equal(0.8, t.WindowOpacity); // highest is the untouched 100%: Elyos keeps its 80% default
    }

    [Fact] public void Migration_RunsOnce_AndNewerFilesAreLeftAlone()
    {
        var s = Load("{\"SettingsVersion\":5,\"Theme\":\"honey\",\"ThemeOpacity\":{\"honey\":0.55}}");
        Assert.Equal(0.55, s.WindowOpacity);
        var t = Load("{\"SettingsVersion\":5,\"Theme\":\"honey\",\"Opacity\":0.45}"); // stray old field in a v5 file: ignored
        Assert.Equal(1.0, t.WindowOpacity);
    }
}
