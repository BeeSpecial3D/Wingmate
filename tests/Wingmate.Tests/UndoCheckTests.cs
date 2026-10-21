using System.Collections.Generic;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class UndoCheckTests
{
    static ChecklistState State(out ChecklistItem a, out ChecklistItem b)
    {
        a = new ChecklistItem { Text = "Daily quests" };
        b = new ChecklistItem { Text = "Shugo run" };
        var s = new ChecklistState();
        s.Sections.Clear();
        s.Sections.Add(new ChecklistSection { Name = "Daily", Items = { a, b } });
        return s;
    }

    [Fact] public void Text_NamesTheItem_BeforeTheUndoLink()
    {
        Assert.Equal("Checked Daily quests \u00b7 ", UndoCheck.Text("Daily quests"));
        Assert.Equal("Checked \u00b7 ", UndoCheck.Text("  "));
        Assert.Equal("Checked a b \u00b7 ", UndoCheck.Text("a\nb"));
    }

    [Fact] public void Text_ShortensLongNames()
    {
        var t = UndoCheck.Text(new string('x', 100));
        Assert.EndsWith("\u2026 \u00b7 ", t);
        Assert.True(t.Length <= "Checked ".Length + UndoCheck.MaxNameLength + 3);
    }

    [Fact] public void Undo_UnchecksTheItem_AndKeepsTheRest()
    {
        var s = State(out var a, out var b);
        a.Done = true; b.Done = true;
        Assert.True(UndoCheck.Undo(s, a.Id));
        Assert.False(a.Done);
        Assert.True(b.Done);
        Assert.Contains(a, CheckedItems.Visible(s.Sections[0], showChecked: false));
    }

    [Fact] public void Undo_ShowsAHiddenList_Again()
    {
        var s = State(out var a, out var b);
        a.Done = b.Done = true; s.Sections[0].Hidden = true;
        Assert.True(UndoCheck.Undo(s, b.Id));
        Assert.False(s.Sections[0].Hidden);
    }

    [Fact] public void Undo_DoesNothing_WhenAlreadyUncheckedOrGone()
    {
        var s = State(out var a, out _);
        Assert.False(UndoCheck.Undo(s, a.Id));            // e.g. a reset already unchecked it
        Assert.False(UndoCheck.Undo(s, "no-such-item"));   // deleted, or another character's item
        Assert.False(a.Done);
    }

    [Fact] public void Seconds_IsAboutFive() => Assert.Equal(5, UndoCheck.Seconds);
}
