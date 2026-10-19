using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class CharacterTests
{
    static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "wm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact] public void OldFormat_MigratesIntoFirstCharacter_WithBackup_NothingLost()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        var old = ChecklistState.CreateDefault();
        old.LastDailyReset = new DateTime(2026, 10, 19, 7, 0, 0, DateTimeKind.Utc);
        old.LastWeeklyReset = new DateTime(2026, 10, 14, 7, 0, 0, DateTimeKind.Utc);
        old.Sections[0].Items[1].Done = true;
        old.Sections[1].Hidden = true;
        old.Sections.Add(new ChecklistSection { Kind = "custom", Name = "Farming", Collapsed = true, Items = { new ChecklistItem { Text = "Ore", Done = true } } });
        old.Save(path);
        var oldText = File.ReadAllText(path);

        var b = ChecklistBook.Load(path, "Wingmate");
        Assert.True(b.Migrated); Assert.False(b.ReadOnly);
        Assert.Equal(oldText, File.ReadAllText(Path.Combine(dir, ChecklistBook.PreCharactersBackup))); // exact backup
        Assert.Equal(oldText, File.ReadAllText(path)); // load itself never writes
        var c = Assert.Single(b.Characters);
        Assert.Equal("Wingmate", c.Name); Assert.Same(c, b.Current);
        Assert.Equal(3, c.Sections.Count);
        Assert.Equal(old.Sections.SelectMany(x => x.Items).Select(i => (i.Id, i.Text, i.Done)), c.Sections.SelectMany(x => x.Items).Select(i => (i.Id, i.Text, i.Done)));
        Assert.True(c.Sections[1].Hidden); Assert.True(c.Sections[2].Collapsed);
        Assert.Equal(old.LastDailyReset, c.LastDailyReset); Assert.Equal(old.LastWeeklyReset, c.LastWeeklyReset);

        b.Save(path); // new format round-trips
        var again = ChecklistBook.Load(path, "ignored");
        Assert.False(again.Migrated);
        Assert.Equal("Wingmate", again.Current.Name);
        Assert.Equal(c.Sections.SelectMany(x => x.Items).Select(i => (i.Id, i.Done)), again.Current.Sections.SelectMany(x => x.Items).Select(i => (i.Id, i.Done)));
    }

    [Fact] public void SecondMigration_DoesNotOverwriteExistingBackup()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(Path.Combine(dir, ChecklistBook.PreCharactersBackup), "keep me");
        ChecklistState.CreateDefault().Save(path);
        ChecklistBook.Load(path, "A");
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(dir, ChecklistBook.PreCharactersBackup)));
        Assert.Single(Directory.GetFiles(dir, "checklist.backup-pre-characters-*.json"));
    }

    [Fact] public void RoundTrip_KeepsCharactersAndCurrent()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        var b = ChecklistBook.CreateNew();
        b.Current.Sections[0].Items[0].Done = true;
        var two = b.Add("Healer"); two.Sections[1].Hidden = true;
        b.Save(path);
        var l = ChecklistBook.Load(path, "x");
        Assert.Equal(new[] { ChecklistBook.DefaultName, "Healer" }, l.Characters.Select(c => c.Name));
        Assert.Equal(two.Id, l.CurrentId);
        Assert.True(l.Characters[0].Sections[0].Items[0].Done); Assert.False(l.Characters[1].Sections[0].Items[0].Done);
        Assert.True(l.Characters[1].Sections[1].Hidden); Assert.False(l.Characters[0].Sections[1].Hidden);
    }

    [Fact] public void Cycle_LoopsInOrder_AndRemoveKeepsOne()
    {
        var b = ChecklistBook.CreateNew();
        var a = b.Current; var c2 = b.Add("Two"); var c3 = b.Add("Three");
        b.Select(a.Id);
        Assert.Same(c2, b.Next()); Assert.Same(c3, b.Next()); Assert.Same(a, b.Next()); // loops to first
        Assert.True(b.Remove(a.Id)); Assert.Same(c2, b.Current);
        Assert.True(b.Remove(c3.Id)); Assert.False(b.Remove(c2.Id)); // never the last one
        Assert.Same(c2, b.Next());
    }

    [Fact] public void MissingFile_NewInstallUsesCharacterName()
    {
        var b = ChecklistBook.Load(Path.Combine(TempDir(), "nope.json"), "Wingmate");
        Assert.Equal(ChecklistBook.DefaultName, b.Current.Name); Assert.False(b.Migrated);
    }

    [Fact] public void BrokenFile_IsNeverOverwritten()
    {
        var dir = TempDir(); var path = Path.Combine(dir, "checklist.json");
        File.WriteAllText(path, "{ this is not json");
        var b = ChecklistBook.Load(path, "x");
        Assert.True(b.ReadOnly);
        b.Save(path);
        Assert.Equal("{ this is not json", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(dir, "checklist.json.broken-*"));
    }

    [Fact] public void ResetsArePerCharacter()
    {
        var b = ChecklistBook.CreateNew();
        var a = b.Current; var c = b.Add("Alt");
        var t0 = new DateTime(2026, 10, 18, 7, 0, 0, DateTimeKind.Utc);
        foreach (var ch in b.Characters) { ResetLogic.Apply(ch, t0, t0); ch.Sections[0].Items[0].Done = true; }
        Assert.True(ResetLogic.Apply(a, t0.AddDays(1), t0)); // only a catches up now
        Assert.False(a.Sections[0].Items[0].Done); Assert.True(c.Sections[0].Items[0].Done);
    }
}
