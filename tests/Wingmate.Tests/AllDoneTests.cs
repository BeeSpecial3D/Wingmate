using System;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class AllDoneTests
{
    static ChecklistSection Sec(string kind, params bool[] done)
    {
        var s = new ChecklistSection { Kind = kind, Name = kind };
        foreach (var d in done) s.Items.Add(new ChecklistItem { Text = "x", Done = d });
        return s;
    }
    static ChecklistState State(params ChecklistSection[] secs) { var st = new ChecklistState(); st.Sections.Clear(); st.Sections.AddRange(secs); return st; }

    [Fact] public void AllComplete_TriggersOnlyWhenEveryListIsDone()
    {
        var st = State(Sec("daily", true, true), Sec("weekly", true, false));
        Assert.False(CelebrationLines.AllListsComplete(st));
        st.Sections[1].Items[1].Done = true;
        Assert.True(CelebrationLines.AllListsComplete(st));
    }

    [Fact] public void EarlierCompletedListThatWasHidden_StillCounts()
    {
        var a = Sec("daily", true); a.Hidden = true; // hidden after its own celebration
        Assert.True(CelebrationLines.AllListsComplete(State(a, Sec("weekly", true, true))));
    }

    [Fact] public void ListHiddenByHandWhileUnfinished_AndEmptyLists_AreIgnored()
    {
        var manual = Sec("custom", false, false); manual.Hidden = true;
        Assert.True(CelebrationLines.AllListsComplete(State(Sec("daily", true), Sec("weekly", true), manual, Sec("custom"))));
        Assert.False(CelebrationLines.AllListsComplete(State(Sec("daily", true), manual, Sec("custom")))); // only 1 counts
    }

    [Fact] public void VisibleUnfinishedList_Blocks()
        => Assert.False(CelebrationLines.AllListsComplete(State(Sec("daily", true), Sec("weekly", true), Sec("custom", false))));

    static readonly TimeSpan Over2h = TimeSpan.FromHours(2) + TimeSpan.FromSeconds(1);

    [Fact] public void TwoListsAreEnough_AgainForTheMinimum()
    {
        Assert.Equal(2, CelebrationLines.MinListsForAllDone);
        Assert.True(CelebrationLines.AllListsComplete(State(Sec("daily", true), Sec("weekly", true))));
        Assert.False(CelebrationLines.AllListsComplete(State(Sec("daily", true), Sec("custom")))); // empty list doesn't count
    }

    [Fact] public void AllDoneLine_NeedsMoreThanTwoHoursRunning()
    {
        var done = State(Sec("daily", true), Sec("weekly", true, true));
        Assert.False(CelebrationLines.ShouldPlayAllDone(done, TimeSpan.Zero));
        Assert.False(CelebrationLines.ShouldPlayAllDone(done, TimeSpan.FromMinutes(119)));
        Assert.False(CelebrationLines.ShouldPlayAllDone(done, TimeSpan.FromHours(2))); // exactly 2 h is not "more than"
        Assert.True(CelebrationLines.ShouldPlayAllDone(done, Over2h));
        Assert.True(CelebrationLines.ShouldPlayAllDone(done, TimeSpan.FromHours(30)));
    }

    [Fact] public void AllDoneLine_NeedsEveryListAndTwoLists_EvenAfterTwoHours()
    {
        Assert.False(CelebrationLines.ShouldPlayAllDone(State(Sec("daily", true), Sec("weekly", true, false)), Over2h)); // one list unfinished
        Assert.False(CelebrationLines.ShouldPlayAllDone(State(Sec("daily", true, true)), Over2h));                       // only one list
        Assert.False(CelebrationLines.ShouldPlayAllDone(State(Sec("daily", true), Sec("custom")), Over2h));              // empty list doesn't count
        var hiddenDone = Sec("daily", true); hiddenDone.Hidden = true;
        Assert.True(CelebrationLines.ShouldPlayAllDone(State(hiddenDone, Sec("weekly", true)), Over2h));               // hidden after completing counts
        var hiddenOpen = Sec("custom", false); hiddenOpen.Hidden = true;
        Assert.False(CelebrationLines.ShouldPlayAllDone(State(Sec("daily", true), hiddenOpen), Over2h));               // hidden unfinished ignored -> 1 list
    }

    [Fact] public void SingleList_NeverCounts_SoPoolLinesStillPlay()
    {
        Assert.False(CelebrationLines.AllListsComplete(State(Sec("daily", true, true))));
        Assert.False(CelebrationLines.AllListsComplete(State(Sec("daily", true), Sec("custom"))));
        Assert.False(CelebrationLines.AllListsComplete(State()));
    }

    [Fact] public void AllDoneKey_IsTheRenderedClip() => Assert.Equal("cel_all_1", CelebrationLines.AllDoneKey);
}
