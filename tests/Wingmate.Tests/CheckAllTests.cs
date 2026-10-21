using System;
using System.Collections.Generic;
using System.Linq;
using Wingmate.Core;
using Xunit;
using static Old106;

[Collection("ChecklistClock")]
public class CheckAllTests : IDisposable
{
    public CheckAllTests() => ChecklistClock.UtcNow = () => new DateTime(2026, 10, 21, 17, 0, 0, DateTimeKind.Utc);
    public void Dispose() => ChecklistClock.UtcNow = () => DateTime.UtcNow;

    static readonly DateTime D0 = new(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc);

    [Fact] public void ChecksEveryItem_ListCompletes()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var w = Weekly(c);
        w.Items[2].Done = true;
        var snap = CheckAll.Apply(w);
        Assert.NotNull(snap);
        Assert.All(w.Items, i => Assert.True(i.Done));
        Assert.True(w.IsComplete); // so it finishes and hides like a hand-checked last item
        Assert.Equal(w.Items.Count - 1, snap!.CheckedIds.Count);
        Assert.DoesNotContain(w.Items[2].Id, snap.CheckedIds);
        Assert.All(Daily(c).Items, i => Assert.False(i.Done)); // other lists untouched
    }

    [Fact] public void NothingToCheck_ReturnsNull()
    {
        var w = Weekly(ChecklistBook.NewCharacter("Daeva"));
        foreach (var i in w.Items) i.Done = true;
        Assert.Null(CheckAll.Apply(w));
        Assert.Null(CheckAll.Apply(new ChecklistSection { Kind = "custom", Name = "Empty" }));
    }

    [Fact] public void Undo_RestoresTheExactPriorState()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var d = Daily(c);
        d.Items[0].Done = true; d.Items[3].Done = true;
        var before = d.Items.Select(i => i.Done).ToList();
        var snap = CheckAll.Apply(d)!;
        d.Hidden = true; // the celebration hid it
        Assert.True(CheckAll.Undo(c, snap));
        Assert.Equal(before, d.Items.Select(i => i.Done));
        Assert.False(d.Hidden);
        Assert.False(CheckAll.Undo(c, snap)); // nothing left to undo
    }

    [Fact] public void Undo_AfterAResetCleared_ChangesNothing()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        var d = Daily(c);
        ResetLogic.Apply(c, D0, D0);
        var snap = CheckAll.Apply(d)!;
        d.Hidden = true;
        ResetLogic.Apply(c, D0.AddDays(1), D0);
        Assert.False(CheckAll.Undo(c, snap));
        Assert.All(d.Items, i => Assert.False(i.Done));
    }

    [Fact] public void Reset_BringsTheListBack()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        ResetLogic.Apply(c, D0, D0);
        var w = Weekly(c);
        CheckAll.Apply(w); w.Hidden = true;
        ResetLogic.Apply(c, D0.AddDays(1), D0); // daily reset: weeklies stay done and hidden
        Assert.True(w.Hidden); Assert.All(w.Items, i => Assert.True(i.Done));
        ResetLogic.Apply(c, D0.AddDays(7), D0.AddDays(7));
        Assert.False(w.Hidden); Assert.All(w.Items, i => Assert.False(i.Done));
    }

    [Fact] public void Events_ReadOnlyListCanBeCheckedAll_EachItemComesBackOnItsOwnReset()
    {
        var c = ChecklistBook.NewCharacter("Daeva");
        ResetLogic.Apply(c, D0, D0);
        var ev = Events(c);
        Assert.False(ListRules.CanEditItems(ev));
        Assert.NotNull(CheckAll.Apply(ev));
        Assert.True(ev.IsComplete);
        ev.Hidden = true;
        ResetLogic.Apply(c, D0.AddDays(1), D0); // daily reset
        var byId = ev.Items.ToDictionary(i => i.Id);
        Assert.False(ev.Hidden); // daily events are back, so the list shows again
        Assert.False(byId["shugo-festival"].Done); Assert.False(byId["dimensional-invasion"].Done);
        Assert.True(byId["artifact-siege"].Done); Assert.True(byId["arena-of-tactics"].Done); Assert.True(byId["spiritstone-shop-2026"].Done);
        Assert.Equal(new[] { "shugo-festival", "dimensional-invasion" }, CheckedItems.Visible(ev, false).Select(i => i.Id));
        ResetLogic.Apply(c, D0.AddDays(7), D0.AddDays(7));
        Assert.All(ev.Items, i => Assert.False(i.Done));
    }

    [Fact] public void EndedEvents_AreLeftAlone()
    {
        var sec = EventsList.NewSection(new List<EventDefinition> { new("a", "A", "daily"), new("old", "Old", "weekly", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)) });
        var snap = CheckAll.Apply(sec)!;
        Assert.Equal(new[] { "a" }, snap.CheckedIds);
        Assert.False(sec.Items.Single(i => i.Id == "old").Done);
    }

    [Fact] public void ShowCheckedItems_StillRevealsThem()
    {
        var w = Weekly(ChecklistBook.NewCharacter("Daeva"));
        CheckAll.Apply(w);
        Assert.Empty(CheckedItems.Visible(w, false));
        Assert.Equal(w.Items, CheckedItems.Visible(w, true));
        Assert.Equal(w.Items.Count, CheckedItems.HiddenCount(w, false));
    }

    [Fact] public void UndoLine_Text()
    {
        Assert.Equal("Checked all in Weeklies \u00b7 ", CheckAll.Text("Weeklies"));
        Assert.Equal("Checked all \u00b7 ", CheckAll.Text("  "));
        Assert.EndsWith("\u2026 \u00b7 ", CheckAll.Text(new string('x', 80)));
    }
}
