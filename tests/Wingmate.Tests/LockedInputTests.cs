using System.Collections.Generic;
using Wingmate.Core;
using Xunit;

public class LockedInputTests
{
    // A 300x400 list starting at (10, 50), with two checkboxes (14x14) down its left side.
    static readonly Box List = new(10, 50, 300, 400);
    static readonly List<Box> Checks = new() { new Box(12, 60, 14, 14), new Box(12, 90, 14, 14) };

    [Fact] public void OutsideTheList_PassesThrough()
    {
        Assert.Equal(LockZone.PassThrough, LockedInput.Classify(100, 20, List, Checks).Zone);  // header
        Assert.Equal(LockZone.PassThrough, LockedInput.Classify(100, 460, List, Checks).Zone); // footer
        Assert.Equal(LockZone.PassThrough, LockedInput.Classify(5, 100, List, Checks).Zone);   // left edge
        Assert.False(LockedInput.TakesMouse(LockZone.PassThrough));
        Assert.False(LockedInput.ClickToggles(LockZone.PassThrough));
    }

    [Fact] public void OverItemTextOrHeaders_TakesTheWheel_ButClicksDoNothing()
    {
        var (zone, i) = LockedInput.Classify(150, 66, List, Checks); // item text on the first row
        Assert.Equal(LockZone.List, zone); Assert.Equal(-1, i);
        Assert.True(LockedInput.TakesMouse(zone));
        Assert.False(LockedInput.ClickToggles(zone));
    }

    [Fact] public void OverACheckbox_TogglesThatOne()
    {
        var (zone, i) = LockedInput.Classify(19, 97, List, Checks);
        Assert.Equal(LockZone.Checkbox, zone); Assert.Equal(1, i);
        Assert.True(LockedInput.TakesMouse(zone));
        Assert.True(LockedInput.ClickToggles(zone));
        // a little slop around the box still counts, further away does not
        Assert.Equal((LockZone.Checkbox, 0), LockedInput.Classify(12 - 2, 60 - 2, List, Checks));
        Assert.Equal(LockZone.List, LockedInput.Classify(12 + 14 + LockedInput.CheckboxSlop + 1, 66, List, Checks).Zone);
    }

    [Fact] public void CheckboxScrolledOutOfView_DoesNotTakeClicks()
    {
        var hidden = new List<Box> { new Box(12, 20, 14, 14) }; // above the visible list
        Assert.Equal(LockZone.PassThrough, LockedInput.Classify(19, 27, List, hidden).Zone);
    }

    [Fact] public void NoCheckboxes_ListStillScrolls()
    {
        Assert.Equal((LockZone.List, -1), LockedInput.Classify(100, 100, List, new List<Box>()));
    }
}
