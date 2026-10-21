using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Wingmate.Core;
using Xunit;
using static Old106;

[Collection("ChecklistClock")]
public class EventsListTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "wm-events-" + Guid.NewGuid().ToString("N"));
    public EventsListTests() { Directory.CreateDirectory(_dir); ChecklistClock.UtcNow = () => new DateTime(2026, 10, 21, 17, 0, 0, DateTimeKind.Utc); }
    public void Dispose() { ChecklistClock.UtcNow = () => DateTime.UtcNow; try { Directory.Delete(_dir, true); } catch { } }
    string P => Path.Combine(_dir, "checklist.json");

    static readonly string[] EventNames = { "Shugo Festival", "Dimensional Invasion", "Artifact Siege", "Arena of Tactics (3 matches)", "Spiritstone shop (event)" };

    // ------------------------------------------------------------ new installs
    [Fact] public void NewCharacter_HasDailiesWeekliesEvents_EventItemsOffDailyWeekly()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        Assert.Equal(new[] { "daily", "weekly", "events" }, c.Sections.Select(s => s.Kind));
        Assert.Equal("Events", Events(c).Name);
        Assert.Equal(EventNames, Events(c).Items.Select(i => i.Text));
        foreach (var n in EventNames) { Assert.DoesNotContain(Daily(c).Items, i => i.Text == n); Assert.DoesNotContain(Weekly(c).Items, i => i.Text == n); }
        Assert.Equal(EventsList.Definitions.Select(d => d.Id), Events(c).Items.Select(i => i.Id));
    }

    // ------------------------------------------------------------ migration
    [Fact] public void Migration_MovesExactDefaults_KeepsCheckedState_TouchesNothingElse()
    {
        var c = Character("Daeva");
        Daily(c).Items.Single(i => i.Text == "Shugo Festival").Done = true;
        Weekly(c).Items.Single(i => i.Text == "Artifact Siege").Done = true;
        Daily(c).Items.Single(i => i.Text == "Nightmare").Done = true;
        Daily(c).Items.Add(new ChecklistItem { Text = "My daily" });
        var dailyIds = Daily(c).Items.Where(i => i.Text is not ("Shugo Festival" or "Dimensional Invasion")).Select(i => (i.Id, i.Text, i.Done)).ToList();
        File.WriteAllText(P, BookJson(c));
        var b = ChecklistBook.Load(P, "x");
        Assert.True(b.ListsUpdated);
        var cur = b.Current;
        Assert.Equal(dailyIds, Daily(cur).Items.Select(i => (i.Id, i.Text, i.Done)));
        Assert.DoesNotContain(Weekly(cur).Items, i => i.Text is "Artifact Siege" or "Arena of Tactics (3 matches)");
        var ev = Events(cur).Items.ToDictionary(i => i.Id);
        Assert.True(ev["shugo-festival"].Done); Assert.True(ev["artifact-siege"].Done);
        Assert.False(ev["dimensional-invasion"].Done); Assert.False(ev["arena-of-tactics"].Done); Assert.False(ev["spiritstone-shop-2026"].Done);
        Assert.Equal(new[] { "daily", "weekly", "events" }, cur.Sections.Select(s => s.Kind)); // Events goes after Weeklies
    }

    [Fact] public void Migration_RenamedItemStaysAsUsersOwn_DeletedSkipped_EventStillShows()
    {
        var c = Character("Daeva");
        var shugo = Daily(c).Items.Single(i => i.Text == "Shugo Festival");
        shugo.Text = "Shugo Festival x3"; shugo.Done = true;
        Weekly(c).Items.RemoveAll(i => i.Text == "Artifact Siege");
        File.WriteAllText(P, BookJson(c));
        var cur = ChecklistBook.Load(P, "x").Current;
        var mine = Assert.Single(Daily(cur).Items, i => i.Text == "Shugo Festival x3");
        Assert.True(mine.Done); Assert.Equal(shugo.Id, mine.Id);
        Assert.False(Events(cur).Items.Single(i => i.Id == "shugo-festival").Done); // official one, unchecked
        Assert.Contains(Events(cur).Items, i => i.Id == "artifact-siege");
        Assert.DoesNotContain(Weekly(cur).Items, i => i.Text == "Artifact Siege");
    }

    [Fact] public void Migration_OnlyExactName_CaseOrSpacesCountAsRenamed()
    {
        var c = Character("Daeva");
        Daily(c).Items.Single(i => i.Text == "Dimensional Invasion").Text = "dimensional invasion";
        File.WriteAllText(P, BookJson(c));
        Assert.Contains(Daily(ChecklistBook.Load(P, "x").Current).Items, i => i.Text == "dimensional invasion");
    }

    [Fact] public void Migration_CustomListsUntouched()
    {
        var c = Character("Daeva");
        c.Sections.Add(new ChecklistSection { Kind = "custom", Name = "Events", Items = { new ChecklistItem { Text = "Shugo Festival", Done = true } } });
        File.WriteAllText(P, BookJson(c));
        var cur = ChecklistBook.Load(P, "x").Current;
        var mine = cur.Sections.Single(s => s.IsCustom);
        Assert.Equal("Events", mine.Name); Assert.True(Assert.Single(mine.Items).Done);
        Assert.Single(cur.Sections, s => s.IsManaged);
    }

    [Fact] public void Migration_RunsOnce()
    {
        File.WriteAllText(P, BookJson(Character("Daeva")));
        var b = ChecklistBook.Load(P, "x");
        Daily(b.Current).Items.Add(new ChecklistItem { Text = "Shugo Festival" }); // the user adds their own later
        b.Save(P);
        var again = ChecklistBook.Load(P, "x");
        Assert.False(again.ListsUpdated);
        Assert.Contains(Daily(again.Current).Items, i => i.Text == "Shugo Festival");
        Assert.Contains(ChecklistState.Update107Events, again.Current.AppliedUpdates);
    }

    [Fact] public void Migration_NeverResets()
    {
        var c = Character("Daeva");
        foreach (var s in c.Sections) foreach (var i in s.Items) i.Done = true;
        c.LastDailyReset = new DateTime(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(P, BookJson(c));
        var cur = ChecklistBook.Load(P, "x").Current;
        Assert.All(Daily(cur).Items, i => Assert.True(i.Done));
        Assert.Equal(c.LastDailyReset, cur.LastDailyReset);
        Assert.True(Events(cur).Items.Single(i => i.Id == "arena-of-tactics").Done);
    }

    // ------------------------------------------------------------ definitions win, state by stable id
    static readonly List<EventDefinition> Defs1 = new() { new("a", "Alpha", "daily"), new("b", "Beta", "weekly") };

    [Fact] public void Sync_RenamedDefinition_KeepsCheckedState()
    {
        var sec = EventsList.NewSection(Defs1);
        sec.Items[0].Done = true;
        EventsList.Sync(sec, new List<EventDefinition> { new("a", "Alpha (new name)", "daily"), new("b", "Beta", "weekly") });
        Assert.Equal("Alpha (new name)", sec.Items[0].Text); Assert.True(sec.Items[0].Done);
    }

    [Fact] public void Sync_NewEventsAppear_RetiredHide_StateKept()
    {
        var sec = EventsList.NewSection(Defs1);
        sec.Items[1].Done = true;
        EventsList.Sync(sec, new List<EventDefinition> { new("a", "Alpha", "daily"), new("c", "Gamma", "weekly") });
        Assert.Equal(new[] { "Alpha", "Gamma" }, sec.LiveItems.Select(i => i.Text));
        var beta = Assert.Single(sec.Items, i => i.Id == "b");
        Assert.True(beta.Retired); Assert.True(beta.Done); // kept in the file, hidden
        EventsList.Sync(sec, Defs1); // back again later: same state
        Assert.True(sec.Items.Single(i => i.Id == "b").Done); Assert.False(sec.Items.Single(i => i.Id == "b").Retired);
    }

    [Fact] public void Sync_UsersFileCannotChangeNamesOrAddItems()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var ev = Events(c);
        ev.Name = "Hacked"; ev.Items[0].Text = "Renamed by hand"; ev.Items.Add(new ChecklistItem { Text = "Added by hand" });
        File.WriteAllText(P, JsonSerializer.Serialize(new ChecklistBook { Characters = { c }, CurrentId = c.Id }));
        var cur = ChecklistBook.Load(P, "x").Current;
        Assert.Equal("Events", Events(cur).Name);
        Assert.Equal(EventNames, Events(cur).LiveItems.Select(i => i.Text));
    }

    [Fact] public void SavedFile_KeepsOnlyIdTextAndCheckedState()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var json = JsonSerializer.Serialize(Events(c));
        Assert.DoesNotContain("Reset\"", json); Assert.DoesNotContain("EndsUtc", json); Assert.DoesNotContain("Retired", json);
    }

    // ------------------------------------------------------------ expiry
    [Fact] public void Spiritstone_HidesWhenTheEventEnds_StateKept()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var ev = Events(c);
        var spirit = ev.Items.Single(i => i.Id == "spiritstone-shop-2026");
        spirit.Done = true;
        Assert.Contains(spirit, ev.LiveItems);
        ChecklistClock.UtcNow = () => new DateTime(2026, 12, 16, 6, 59, 0, DateTimeKind.Utc);
        Assert.Contains(spirit, ev.LiveItems);
        ChecklistClock.UtcNow = () => new DateTime(2026, 12, 16, 7, 0, 0, DateTimeKind.Utc); // Tue 11 PM PST / Wed 8 AM CET
        Assert.DoesNotContain(spirit, ev.LiveItems);
        Assert.Contains(spirit, ev.Items); Assert.True(spirit.Done); // not deleted
        Assert.DoesNotContain(spirit, CheckedItems.Visible(ev, showChecked: true));
    }

    [Fact] public void ExpiredItems_DoNotBlockCompletion()
    {
        var sec = EventsList.NewSection(new List<EventDefinition> { new("a", "A", "daily"), new("old", "Old", "weekly", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)) });
        sec.Items[0].Done = true;
        Assert.True(sec.IsComplete);
        Assert.False(CheckedItems.WarpOutOnCheck(sec, sec.Items[0], false)); // last live one: the list celebration plays
        Assert.Equal("events", CelebrationLines.PoolFor(sec, 20));
    }

    // ------------------------------------------------------------ per-item reset
    [Fact] public void DailyReset_ClearsDailyEventsOnly_WeeklyResetClearsWeeklyEvents()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        foreach (var s in c.Sections) foreach (var i in s.Items) i.Done = true;
        DateTime d0 = new(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc), w0 = d0;
        ResetLogic.Apply(c, d0, w0); // first run records
        ResetLogic.Apply(c, d0.AddDays(1), w0); // next daily reset only
        var ev = Events(c).Items.ToDictionary(i => i.Id);
        Assert.False(ev["shugo-festival"].Done); Assert.False(ev["dimensional-invasion"].Done);
        Assert.True(ev["artifact-siege"].Done); Assert.True(ev["arena-of-tactics"].Done); Assert.True(ev["spiritstone-shop-2026"].Done);
        Assert.All(Daily(c).Items, i => Assert.False(i.Done));
        Assert.All(Weekly(c).Items, i => Assert.True(i.Done));
        ResetLogic.Apply(c, d0.AddDays(7), w0.AddDays(7));
        Assert.All(Events(c).Items, i => Assert.False(i.Done));
        Assert.All(Weekly(c).Items, i => Assert.False(i.Done));
    }

    [Fact] public void OneOffItems_NeverReset_CustomListsNeverReset()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var once = EventsList.NewSection(new List<EventDefinition> { new("o", "Once", "none") });
        c.Sections.Add(once);
        var mine = new ChecklistSection { Kind = "custom", Name = "Mine", Items = { new ChecklistItem { Text = "x", Done = true } } };
        c.Sections.Add(mine);
        once.Items[0].Done = true;
        var d0 = new DateTime(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc);
        ResetLogic.Apply(c, d0, d0); ResetLogic.Apply(c, d0.AddDays(7), d0.AddDays(7));
        Assert.True(once.Items[0].Done); Assert.True(mine.Items[0].Done);
    }

    // ------------------------------------------------------------ read-only
    [Fact] public void EventsList_IsReadOnly_ForEditing()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var ev = Events(c);
        Assert.True(ev.IsManaged); Assert.False(ev.IsCustom);
        Assert.False(ListRules.CanEditItems(ev));
        Assert.False(ListRules.CanRenameList(ev));
        Assert.False(ListRules.CanDeleteList(ev));
        Assert.False(ListRules.CanResetToDefault(ev));
        Assert.False(ListRules.CanMoveItem(ev, Daily(c)));
        Assert.False(ListRules.CanMoveItem(Daily(c), ev));
        Assert.True(ListRules.CanMoveItem(Daily(c), Weekly(c)));
        Assert.True(ListRules.CanResetToDefault(Daily(c)));
    }

    [Fact] public void CopyTo_NeverCopiesFromOrMergesIntoEvents()
    {
        var a = ChecklistBook.NewCharacter("Daeva"); var b = ChecklistBook.NewCharacter("Second");
        var r = ListCopy.CopyInto(Events(a), b);
        Assert.Equal(0, r.Added); Assert.Single(b.Sections, s => s.Name == "Events");
        var mine = new ChecklistSection { Kind = "custom", Name = "Events", Items = { new ChecklistItem { Text = "Mine" } } };
        var r2 = ListCopy.CopyInto(mine, b);
        Assert.True(r2.Created); // a new custom list, not merged into the managed one
        Assert.DoesNotContain(Events(b).Items, i => i.Text == "Mine");
    }
}

[CollectionDefinition("ChecklistClock", DisableParallelization = true)]
public class ChecklistClockCollection { }
