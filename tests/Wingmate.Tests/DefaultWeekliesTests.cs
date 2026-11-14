using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wingmate.Core;
using Xunit;
using static Old106;

/// <summary>1.0.7 and 1.0.9 added default weeklies: in new lists, and appended once (append-only) to existing Weeklies.</summary>
public class DefaultWeekliesTests : IDisposable
{
    static readonly string[] Added107 = { "Command Missions (12)", "Abyss Command Missions (20)", "Battlefield (3 wins)", "Subscriber Shop" };
    static readonly string[] Added109 = { "Shugo Festival Season Mission", "Transcendence Season Missions (3)", "Chaotic Outer Reshanta (3h, lv. 45)" };
    static readonly string[] Added = Added107.Concat(Added109).ToArray();
    static readonly string[] BaseWeeklies = { "Raids", "Ascension Trial", "Subjugation", "Exploration", "Use Abyss time" };

    readonly string _dir = Path.Combine(Path.GetTempPath(), "wm-weeklies-" + Guid.NewGuid().ToString("N"));
    public DefaultWeekliesTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    string P => Path.Combine(_dir, "checklist.json");

    [Fact] public void Defaults_HaveTheNewWeeklies()
    {
        Assert.Equal(BaseWeeklies.Concat(Added), ChecklistState.DefaultWeeklies);
        Assert.Equal(Added107, ChecklistState.AddedWeeklies107);
        Assert.Equal(Added109, ChecklistState.AddedWeeklies109);
        var w = ChecklistState.DefaultSection("weekly");
        Assert.Equal(ChecklistState.DefaultWeeklies, w.Items.Select(i => i.Text));
        Assert.Equal(w.Items.Count, w.Items.Select(i => i.Id).Distinct().Count());
    }

    [Fact] public void NewInstall_AndNewCharacter_GetTheNewWeeklies()
    {
        var book = ChecklistBook.Load(Path.Combine(_dir, "missing.json"), "x");
        foreach (var t in Added) Assert.Contains(Weekly(book.Current).Items, i => i.Text == t);
        var c = book.Add("Daeva");
        foreach (var t in Added) Assert.Contains(Weekly(c).Items, i => i.Text == t);
    }

    static ChecklistState Customised()
    {
        var c = Character("Daeva");
        var w = Weekly(c);
        w.Items[1].Done = true;
        w.Items.RemoveAt(4); // the user removed "Use Abyss time"
        w.Items.Add(new ChecklistItem { Text = "My own weekly" });
        return c;
    }

    [Fact] public void ExistingBook_GetsTheNewWeekliesAppendedOnce_RestUntouched()
    {
        var json = BookJson(Customised());
        File.WriteAllText(P, json);
        var before = Weekly(JsonSerializer.Deserialize<ChecklistBook>(json)!.Characters[0]).Items
            .Where(i => i.Text is not ("Artifact Siege" or "Arena of Tactics (3 matches)")).ToList(); // those two move to Events
        var book = ChecklistBook.Load(P, "x");
        Assert.True(book.ListsUpdated);
        var after = Weekly(book.Current).Items;
        Assert.Equal(before.Select(i => (i.Id, i.Text, i.Done)), after.Take(before.Count).Select(i => (i.Id, i.Text, i.Done)));
        Assert.Equal(Added, after.Skip(before.Count).Select(i => i.Text));
        Assert.All(after.Skip(before.Count), i => Assert.False(i.Done));
        Assert.Equal(json, File.ReadAllText(P)); // loading itself writes nothing (the app saves afterwards)
    }

    [Fact] public void RunsOnce_SaveAndLoadAgain_AddsNothingMore()
    {
        File.WriteAllText(P, BookJson(Customised()));
        ChecklistBook.Load(P, "x").Save(P);
        var again = ChecklistBook.Load(P, "x");
        Assert.False(again.ListsUpdated);
        Assert.Equal(new[] { "Raids", "Ascension Trial", "Subjugation", "Exploration", "My own weekly" }.Concat(Added), Weekly(again.Current).Items.Select(i => i.Text));
    }

    [Fact] public void DeletedNewItem_NeverComesBack()
    {
        File.WriteAllText(P, BookJson(Customised()));
        var b = ChecklistBook.Load(P, "x");
        Weekly(b.Current).Items.RemoveAll(i => i.Text == "Subscriber Shop");
        b.Save(P);
        var again = ChecklistBook.Load(P, "x");
        Assert.False(again.ListsUpdated);
        Assert.DoesNotContain(Weekly(again.Current).Items, i => i.Text == "Subscriber Shop");
    }

    [Fact] public void SkipsNamesAlreadyThere_CaseAndSpacesIgnored()
    {
        var c = Character("Daeva");
        var w = Weekly(c);
        w.Items.Insert(0, new ChecklistItem { Text = "  battlefield (3 WINS) ", Done = true });
        w.Items.Add(new ChecklistItem { Text = "Subscriber Shop" });
        File.WriteAllText(P, BookJson(c));
        var items = Weekly(ChecklistBook.Load(P, "x").Current).Items;
        Assert.Single(items, i => i.Text.Trim().Equals("Battlefield (3 wins)", StringComparison.OrdinalIgnoreCase));
        Assert.Single(items, i => i.Text == "Subscriber Shop");
        Assert.True(items[0].Done); Assert.Equal("  battlefield (3 WINS) ", items[0].Text);
        Assert.Equal(new[] { "Command Missions (12)", "Abyss Command Missions (20)" }.Concat(Added109), items.Skip(items.Count - 5).Select(i => i.Text));
    }

    [Fact] public void EveryCharacter_GetsIt_HiddenListStaysHidden()
    {
        var a = Character("Daeva"); var b = Character("Second");
        Weekly(b).Hidden = true;
        File.WriteAllText(P, BookJson(a, b));
        var book = ChecklistBook.Load(P, "x");
        foreach (var c in book.Characters) foreach (var t in Added) Assert.Contains(Weekly(c).Items, i => i.Text == t);
        Assert.True(Weekly(book.Characters[1]).Hidden);
    }

    [Fact] public void NewCharacter_IsAlreadyMarked_NoDuplicates()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        Assert.Contains(ChecklistState.Update107Weeklies, c.AppliedUpdates);
        Assert.Contains(ChecklistState.Update109Weeklies, c.AppliedUpdates);
        Assert.False(c.PrepareLists());
        Assert.Equal(ChecklistState.DefaultWeeklies, Weekly(c).Items.Select(i => i.Text));
    }

    [Fact] public void LegacySingleList_GetsThemAppendedOnce()
    {
        File.WriteAllText(P, StateJson(Character("x")));
        var book = ChecklistBook.Load(P, "Daeva");
        Assert.Equal(new[] { "Raids", "Ascension Trial", "Subjugation", "Exploration", "Use Abyss time" }.Concat(Added), Weekly(book.Current).Items.Select(i => i.Text));
    }

    [Fact] public void EmptiedWeeklyList_AfterTheUpdate_StaysEmpty()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        Weekly(c).Items.Clear();
        var b = new ChecklistBook { Characters = { c }, CurrentId = c.Id };
        File.WriteAllText(P, JsonSerializer.Serialize(b));
        Assert.Empty(Weekly(ChecklistBook.Load(P, "x").Current).Items);
    }

    [Fact] public void Existing107Book_GetsOnlyThe109WeekliesAppendedOnce_RestUntouched()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        c.AppliedUpdates.Remove(ChecklistState.Update109Weeklies);
        Weekly(c).Items.RemoveAll(i => Added109.Contains(i.Text));
        Weekly(c).Items[0].Done = true; // Raids
        Weekly(c).Items.Add(new ChecklistItem { Text = "My own weekly" });
        var json = JsonSerializer.Serialize(new ChecklistBook { Characters = { c }, CurrentId = c.Id });
        File.WriteAllText(P, json);
        var before = Weekly(JsonSerializer.Deserialize<ChecklistBook>(json)!.Characters[0]).Items.ToList();
        var book = ChecklistBook.Load(P, "x");
        Assert.True(book.ListsUpdated);
        var after = Weekly(book.Current).Items;
        Assert.Equal(before.Select(i => (i.Id, i.Text, i.Done)), after.Take(before.Count).Select(i => (i.Id, i.Text, i.Done)));
        Assert.Equal(Added109, after.Skip(before.Count).Select(i => i.Text));
        Assert.All(after.Skip(before.Count), i => Assert.False(i.Done));
        Assert.Contains(ChecklistState.Update107Weeklies, book.Current.AppliedUpdates);
        Assert.Contains(ChecklistState.Update109Weeklies, book.Current.AppliedUpdates);
        Assert.Equal(json, File.ReadAllText(P)); // loading itself writes nothing
    }

    [Fact] public void Deleted109Item_NeverComesBack()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        c.AppliedUpdates.Remove(ChecklistState.Update109Weeklies);
        Weekly(c).Items.RemoveAll(i => Added109.Contains(i.Text));
        File.WriteAllText(P, JsonSerializer.Serialize(new ChecklistBook { Characters = { c }, CurrentId = c.Id }));
        var b = ChecklistBook.Load(P, "x");
        Weekly(b.Current).Items.RemoveAll(i => i.Text == "Shugo Festival Season Mission");
        b.Save(P);
        var again = ChecklistBook.Load(P, "x");
        Assert.False(again.ListsUpdated);
        Assert.DoesNotContain(Weekly(again.Current).Items, i => i.Text == "Shugo Festival Season Mission");
        Assert.Contains(Weekly(again.Current).Items, i => i.Text == "Transcendence Season Missions (3)");
    }
}
