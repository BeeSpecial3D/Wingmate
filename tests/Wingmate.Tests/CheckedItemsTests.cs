using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class CheckedItemsTests
{
    static ChecklistSection Sec(params bool[] done) => new()
    {
        Kind = "custom", Name = "L",
        Items = done.Select((d, i) => new ChecklistItem { Text = "i" + i, Done = d }).ToList(),
    };

    [Fact] public void CheckedItemsAreHidden_UnlessShown()
    {
        var s = Sec(true, false, true, false);
        Assert.Equal(new[] { "i1", "i3" }, CheckedItems.Visible(s, showChecked: false).Select(i => i.Text));
        Assert.Equal(new[] { "i0", "i1", "i2", "i3" }, CheckedItems.Visible(s, showChecked: true).Select(i => i.Text));
        Assert.Equal(2, CheckedItems.HiddenCount(s, false));
        Assert.Equal(0, CheckedItems.HiddenCount(s, true));
    }

    [Fact] public void HidingNeverRemovesItems()
    {
        var s = Sec(true, true, false);
        _ = CheckedItems.Visible(s, false).ToList();
        Assert.Equal(3, s.Items.Count);
        Assert.Equal(new[] { true, true, false }, s.Items.Select(i => i.Done));
    }

    [Fact] public void WarpOut_OnlyForAJustCheckedItem_ThatDidNotFinishTheList()
    {
        var s = Sec(false, false, false);
        s.Items[0].Done = true;
        Assert.True(CheckedItems.WarpOutOnCheck(s, s.Items[0], showChecked: false));
        Assert.False(CheckedItems.WarpOutOnCheck(s, s.Items[0], showChecked: true));   // shown: stays in view
        Assert.False(CheckedItems.WarpOutOnCheck(s, s.Items[1], showChecked: false));  // not checked
        s.Items[1].Done = true; s.Items[2].Done = true;                                 // last one: the list celebration takes it
        Assert.True(s.IsComplete);
        Assert.False(CheckedItems.WarpOutOnCheck(s, s.Items[2], showChecked: false));
        Assert.False(CheckedItems.WarpOutOnCheck(s, new ChecklistItem { Done = true }, false)); // not in this list
    }

    [Fact] public void CompletionAndAllDone_DontDependOnVisibleItems()
    {
        var st = new ChecklistState { Sections = { Sec(true, true), Sec(true, true, true) } };
        Assert.All(st.Sections, s => Assert.Empty(CheckedItems.Visible(s, false)));
        Assert.All(st.Sections, s => Assert.True(s.IsComplete));
        Assert.True(CelebrationLines.AllListsComplete(st));
        Assert.True(CelebrationLines.ShouldPlayAllDone(st, TimeSpan.FromHours(3)));
    }

    [Fact] public void ResetAll_BringsHiddenItemsBackUnchecked()
    {
        var st = new ChecklistState { Sections = { Sec(true, false), Sec(true, true) } };
        st.Sections[1].Hidden = true;
        st.ResetAll();
        Assert.All(st.Sections.SelectMany(s => s.Items), i => Assert.False(i.Done));
        Assert.All(st.Sections, s => Assert.Equal(s.Items.Count, CheckedItems.Visible(s, false).Count()));
    }

    [Fact] public void CheckedState_IsSavedAndLoaded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "checklist.json");
        var b = ChecklistBook.CreateNew();
        b.Current.Sections[0].Items[0].Done = true;
        b.Current.Sections[1].Items[2].Done = true;
        b.Save(path);
        var l = ChecklistBook.Load(path, "x");
        Assert.True(l.Current.Sections[0].Items[0].Done);
        Assert.True(l.Current.Sections[1].Items[2].Done);
        Assert.Equal(b.Current.Sections[0].Items.Count - 1, CheckedItems.Visible(l.Current.Sections[0], false).Count());
    }

    [Fact] public void ShowCheckedItems_IsOffByDefault_AndOldSettingsLoadWithoutIt()
    {
        Assert.False(new AppSettings().ShowCheckedItems);
        var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Theme\":\"honey\",\"CelebrationVoice\":false}")!;
        Assert.False(s.ShowCheckedItems);
        Assert.Equal("honey", s.Theme);
        Assert.False(s.CelebrationVoice);
    }
}
