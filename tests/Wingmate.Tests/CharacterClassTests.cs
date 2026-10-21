using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class CharacterClassTests
{
    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "wm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    // A 1.0.5 checklist.json: two characters, no "Class" field anywhere.
    const string OldBook = @"{
  ""Version"": 2,
  ""CurrentId"": ""b2"",
  ""Characters"": [
    { ""Version"": 1, ""Id"": ""a1"", ""Name"": ""Aria"", ""LastDailyReset"": ""2026-10-19T07:00:00Z"", ""LastWeeklyReset"": ""2026-10-14T07:00:00Z"",
      ""Sections"": [
        { ""Id"": ""d"", ""Kind"": ""daily"", ""Name"": ""Dailies"", ""Items"": [ { ""Id"": ""i1"", ""Text"": ""Duty missions"", ""Done"": true }, { ""Id"": ""i2"", ""Text"": ""Nightmare"", ""Done"": false } ], ""Hidden"": false, ""Collapsed"": true },
        { ""Id"": ""w"", ""Kind"": ""weekly"", ""Name"": ""Weeklies"", ""Items"": [ { ""Id"": ""i3"", ""Text"": ""Raids"", ""Done"": true } ], ""Hidden"": true, ""Collapsed"": false },
        { ""Id"": ""c"", ""Kind"": ""custom"", ""Name"": ""Farming"", ""Items"": [ { ""Id"": ""i4"", ""Text"": ""Ore"", ""Done"": true } ], ""Hidden"": false, ""Collapsed"": false }
      ] },
    { ""Version"": 1, ""Id"": ""b2"", ""Name"": ""Brom"",
      ""Sections"": [
        { ""Id"": ""d2"", ""Kind"": ""daily"", ""Name"": ""Dailies"", ""Items"": [ { ""Id"": ""j1"", ""Text"": ""Duty missions"", ""Done"": false } ] },
        { ""Id"": ""w2"", ""Kind"": ""weekly"", ""Name"": ""Weeklies"", ""Items"": [] }
      ] }
  ]
}";

    static string Snapshot(ChecklistBook b) => string.Join("|", b.Characters.Select(c =>
        $"{c.Id}:{c.Name}:{c.LastDailyReset:o}:{c.LastWeeklyReset:o}:" +
        string.Join(";", c.Sections.Select(s => $"{s.Id},{s.Kind},{s.Name},{s.Hidden},{s.Collapsed}," + string.Join("/", s.Items.Select(i => $"{i.Id}={i.Text}={i.Done}"))))));

    [Fact] public void OldFileWithoutClass_LoadsWithEmptyClass_AndKeepsEverything()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(path, OldBook);
        var b = ChecklistBook.Load(path, "x");
        Assert.False(b.ReadOnly); Assert.False(b.Migrated);
        Assert.Equal(OldBook, File.ReadAllText(path)); // loading never writes
        Assert.Equal(new[] { "Aria", "Brom" }, b.Characters.Select(c => c.Name));
        Assert.All(b.Characters, c => Assert.Equal("", c.Class));
        Assert.Equal("b2", b.CurrentId);
        var a = b.Characters[0];
        Assert.Equal(4, a.Sections.Count); // 1.0.7 adds the Events list after Weeklies
        Assert.Equal(new[] { true, false }, a.Sections[0].Items.Select(i => i.Done));
        Assert.True(a.Sections[0].Collapsed); Assert.True(a.Sections[1].Hidden);
        Assert.Equal("Farming", a.Sections[3].Name);
        Assert.Equal(new DateTime(2026, 10, 19, 7, 0, 0, DateTimeKind.Utc), a.LastDailyReset?.ToUniversalTime());
        Assert.Equal(new DateTime(2026, 10, 14, 7, 0, 0, DateTimeKind.Utc), a.LastWeeklyReset?.ToUniversalTime());

        var before = Snapshot(b);
        b.Save(path);
        var again = ChecklistBook.Load(path, "x");
        Assert.Equal(before, Snapshot(again));
        Assert.All(again.Characters, c => Assert.Equal("", c.Class));
    }

    [Fact] public void SettingClass_ChangesOnlyTheClass_AndRoundTrips()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(path, OldBook);
        var b = ChecklistBook.Load(path, "x");
        var before = Snapshot(b);
        Assert.True(b.SetClass("a1", "templar"));
        Assert.Equal("Templar", b.Characters[0].Class);
        Assert.Equal("", b.Characters[1].Class);
        Assert.Equal(before, Snapshot(b)); // lists, checks, names, resets untouched
        Assert.Equal("b2", b.CurrentId);    // current character unchanged
        b.Save(path);
        var again = ChecklistBook.Load(path, "x");
        Assert.Equal("Templar", again.Characters[0].Class);
        Assert.Equal(before, Snapshot(again));

        Assert.True(again.SetClass("a1", "Spiritmaster"));
        Assert.Equal("Spiritmaster", again.Characters[0].Class);
        Assert.Equal(before, Snapshot(again));
        Assert.True(again.SetClass("a1", ""));
        Assert.Equal("", again.Characters[0].Class);
    }

    [Fact] public void SetClass_RejectsUnknownClassOrCharacter()
    {
        var b = ChecklistBook.CreateNew();
        Assert.False(b.SetClass(b.Current.Id, "Bard"));
        Assert.Equal("", b.Current.Class);
        Assert.False(b.SetClass("nope", "Cleric"));
    }

    [Fact] public void ClassIsNormalizedOnLoad_UnknownKept()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(path, OldBook.Replace(@"""Name"": ""Aria"",", @"""Name"": ""Aria"", ""Class"": "" ranger "",")
                                       .Replace(@"""Name"": ""Brom"",", @"""Name"": ""Brom"", ""Class"": ""Painter"","));
        var b = ChecklistBook.Load(path, "x");
        Assert.Equal("Ranger", b.Characters[0].Class);
        Assert.Equal("Painter", b.Characters[1].Class); // never thrown away
        Assert.Equal(CharacterClasses.IndexUrl, CharacterClasses.GuideUrlFor(b.Characters[1].Class));
    }

    [Fact] public void FirstName_CanSaveClassToo_AndNewCharactersHaveNone()
    {
        var b = ChecklistBook.CreateNew();
        Assert.True(b.SetFirstName("Aria", "chanter"));
        Assert.Equal("Aria", b.Current.Name);
        Assert.Equal("Chanter", b.Current.Class);
        Assert.Equal("", b.Add("Brom").Class);

        var c = ChecklistBook.CreateNew();
        Assert.True(c.SetFirstName("Solo"));
        Assert.Equal("", c.Current.Class);
    }

    [Fact] public void UrlMap_HasAllEightClasses_WithVerifiedPages()
    {
        Assert.Equal(new[] { "Gladiator", "Templar", "Assassin", "Ranger", "Sorcerer", "Spiritmaster", "Cleric", "Chanter" }, CharacterClasses.Names);
        var expected = new[]
        {
            ("Gladiator", "https://aion2-guides.info/en/gladiator/"),
            ("Templar", "https://aion2-guides.info/en/templar/"),
            ("Assassin", "https://aion2-guides.info/en/assassin/"),
            ("Ranger", "https://aion2-guides.info/en/ranger/"),
            ("Sorcerer", "https://aion2-guides.info/en/sorcerer/"),
            ("Spiritmaster", "https://aion2-guides.info/en/elementalist/"),
            ("Cleric", "https://aion2-guides.info/en/cleric/"),
            ("Chanter", "https://aion2-guides.info/en/chanter/"),
        };
        foreach (var (name, url) in expected)
        {
            Assert.Equal(url, CharacterClasses.GuideUrlFor(name));
            Assert.Equal(url, CharacterClasses.GuideUrlFor(name.ToUpperInvariant()));
        }
        Assert.Equal(CharacterClasses.All.Count, CharacterClasses.All.Select(c => c.GuideUrl).Distinct().Count());
        Assert.All(CharacterClasses.All, c =>
        {
            var u = new Uri(c.GuideUrl);
            Assert.Equal("https", u.Scheme);
            Assert.Equal("aion2-guides.info", u.Host);
        });
    }

    [Fact] public void UrlMap_FallsBackToClassIndex()
    {
        Assert.Equal("https://aion2-guides.info/en/", CharacterClasses.IndexUrl);
        Assert.Equal(CharacterClasses.IndexUrl, CharacterClasses.GuideUrlFor(""));
        Assert.Equal(CharacterClasses.IndexUrl, CharacterClasses.GuideUrlFor(null));
        Assert.Equal(CharacterClasses.IndexUrl, CharacterClasses.GuideUrlFor("Bard"));
        Assert.Contains("aion2-guides.info", CharacterClasses.Credit);
    }
}

public class ClassMenuTests
{
    [Xunit.Fact] public void Menu_ListsEveryClass_ThenNoClass()
    {
        var m = Wingmate.Core.CharacterClasses.MenuEntries("Templar");
        Xunit.Assert.Equal(9, m.Count);
        Xunit.Assert.Equal(Wingmate.Core.CharacterClasses.Names, System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(m, 8), e => e.Label));
        Xunit.Assert.Equal("No class", m[8].Label);
        Xunit.Assert.Equal("", m[8].Value);
        Xunit.Assert.True(m[8].SeparatorBefore);
    }

    [Xunit.Fact] public void Menu_TicksTheCurrentClass()
    {
        var m = Wingmate.Core.CharacterClasses.MenuEntries("cleric");
        Xunit.Assert.Single(m, e => e.IsChecked);
        Xunit.Assert.True(System.Linq.Enumerable.First(m, e => e.Label == "Cleric").IsChecked);
        Xunit.Assert.False(m[8].IsChecked);
    }

    [Xunit.Theory, Xunit.InlineData(""), Xunit.InlineData(null), Xunit.InlineData("  ")]
    public void Menu_TicksNoClass_WhenThereIsNone(string? cls)
    {
        var m = Wingmate.Core.CharacterClasses.MenuEntries(cls);
        Xunit.Assert.Single(m, e => e.IsChecked);
        Xunit.Assert.True(m[8].IsChecked);
    }

    [Xunit.Fact] public void Menu_UnknownClassFromNewerVersion_TicksNothing()
        => Xunit.Assert.DoesNotContain(Wingmate.Core.CharacterClasses.MenuEntries("Necromancer"), e => e.IsChecked);

    [Xunit.Fact] public void PickingNoClass_ClearsIt()
    {
        var b = Wingmate.Core.ChecklistBook.CreateNew();
        b.SetFirstName("Daeva", "Ranger");
        Xunit.Assert.True(b.SetClass(b.Current.Id, Wingmate.Core.CharacterClasses.MenuEntries(b.Current.Class)[8].Value));
        Xunit.Assert.Equal("", b.Current.Class);
    }
}
