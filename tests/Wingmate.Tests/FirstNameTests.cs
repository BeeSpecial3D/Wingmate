using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

/// <summary>First run: the launcher asks for a character name; only the checklist waits for it.</summary>
public class FirstNameTests
{
    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "wm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact] public void NewBook_NeedsName()
    {
        var b = ChecklistBook.CreateNew();
        Assert.True(b.NeedsName);
        Assert.Equal(ChecklistBook.DefaultName, b.Current.Name);
    }

    [Theory]
    [InlineData("My Character Name")]
    [InlineData("  my character name  ")]
    [InlineData("Character name")]
    [InlineData("")]
    public void PlaceholderNames_NeedName(string name)
    {
        var b = ChecklistBook.CreateNew();
        b.Current.Name = name;
        Assert.True(b.NeedsName);
    }

    [Theory]
    [InlineData("Kael")]
    [InlineData("Wingmate")]          // old default title of migrated single-list users: they never get asked
    [InlineData("My Character")]
    public void RealNames_NeverAsk(string name)
    {
        var b = ChecklistBook.CreateNew();
        b.Current.Name = name;
        Assert.False(b.NeedsName);
    }

    [Fact] public void SeveralCharacters_NeverAsk()
    {
        var b = ChecklistBook.CreateNew();
        b.Add();                              // two placeholder-named characters: an existing user, not a first run
        Assert.False(b.NeedsName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("My Character Name")]
    public void EmptyOrPlaceholder_Rejected_NothingChanges(string? name)
    {
        var b = ChecklistBook.CreateNew();
        var before = b.Current.Name;
        Assert.False(b.SetFirstName(name));
        Assert.Equal(before, b.Current.Name);
        Assert.True(b.NeedsName);
    }

    [Fact] public void SavedName_IsTrimmed_RenamesInPlace_KeepsListsAndProgress()
    {
        var b = ChecklistBook.CreateNew();
        var c = b.Current;
        c.Name = "My Character Name";
        c.Sections[0].Items[0].Done = true;
        c.Sections.Add(new ChecklistSection { Kind = "custom", Name = "Farming", Items = { new ChecklistItem { Text = "Ore", Done = true } } });
        var id = c.Id;
        var items = c.Sections.SelectMany(s => s.Items).Select(i => (i.Id, i.Text, i.Done)).ToList();

        Assert.True(b.SetFirstName("   Aria   "));

        var only = Assert.Single(b.Characters);               // no duplicate
        Assert.Same(c, only);
        Assert.Equal(id, only.Id);
        Assert.Equal("Aria", only.Name);
        Assert.Equal(id, b.CurrentId);
        Assert.Equal(items, only.Sections.SelectMany(s => s.Items).Select(i => (i.Id, i.Text, i.Done)));
        Assert.False(b.NeedsName);                            // unlocked, never asked again
        Assert.False(b.SetFirstName("Other"));                // a second "first name" changes nothing
        Assert.Equal("Aria", b.Current.Name);
    }

    [Fact] public void SavedName_SurvivesSaveAndLoad()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        var b = ChecklistBook.CreateNew();
        b.Current.Name = "My Character Name";
        b.Current.Sections[1].Items[0].Done = true;
        b.Save(path);

        var l = ChecklistBook.Load(path, "x");
        Assert.True(l.NeedsName);                             // a single placeholder-named character (typical upgrade)
        Assert.True(l.SetFirstName("Aria"));
        l.Save(path);

        var again = ChecklistBook.Load(path, "x");
        Assert.False(again.NeedsName);
        Assert.Equal("Aria", Assert.Single(again.Characters).Name);
        Assert.True(again.Current.Sections[1].Items[0].Done);
    }

    [Fact] public void UnreadableFile_NeverAsks()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(path, "{ not json");
        var b = ChecklistBook.Load(path, "x");
        Assert.True(b.ReadOnly);
        Assert.False(b.NeedsName);                            // could not be saved, so the checklist is not held back
    }

    [Fact] public void LongName_IsCapped()
    {
        var b = ChecklistBook.CreateNew();
        Assert.True(b.SetFirstName(new string('a', 60)));
        Assert.Equal(ChecklistBook.MaxNameLength, b.Current.Name.Length);
    }
}
