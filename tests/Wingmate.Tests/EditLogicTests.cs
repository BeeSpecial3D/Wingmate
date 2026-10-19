using Wingmate.Core;
using Xunit;

public class EditLogicTests
{
    [Theory]
    [InlineData("", "  Raids  ", true, false, "Raids")]   // new item: trimmed
    [InlineData("", "   ", true, false, null)]            // new item left empty: discarded, never a blank row
    [InlineData("", "Shugo", true, true, "Shugo")]        // Escape on a new item keeps what was typed
    [InlineData("", "", true, true, null)]                // Escape on an empty new item discards it
    [InlineData("Old", "New ", false, false, "New")]      // existing item edited
    [InlineData("Old", "", false, false, null)]           // existing item emptied: removed
    [InlineData("Old", "Changed", false, true, "Old")]    // Escape on an existing item keeps the old text
    public void ResolveItem(string original, string typed, bool isNew, bool escaped, string? expected) =>
        Assert.Equal(expected, EditLogic.ResolveItem(original, typed, isNew, escaped));

    [Theory]
    [InlineData("My list", " Farm run ", false, "Farm run")]
    [InlineData("My list", "   ", false, "My list")]
    [InlineData("My list", "Other", true, "My list")]
    public void ResolveName(string original, string typed, bool escaped, string expected) =>
        Assert.Equal(expected, EditLogic.ResolveName(original, typed, escaped));

    [Fact]
    public void CommitThenAdd_LeavesNoBlankAndNewRowAtBottom()
    {
        // + clicked while editing: the open edit is committed first, then a new row is appended.
        var sec = new ChecklistSection { Items = { new ChecklistItem { Text = "1" } } };
        var editing = new ChecklistItem { Text = "" }; sec.Items.Add(editing);
        EditLogic.ApplyItem(sec, editing, EditLogic.ResolveItem("", "2", true, false));
        var next = new ChecklistItem(); sec.Items.Add(next);
        Assert.Equal(new[] { "1", "2", "" }, sec.Items.ConvertAll(i => i.Text));
        Assert.Same(next, sec.Items[^1]);
        // the new one is abandoned empty (e.g. + again, or focus lost): it disappears
        EditLogic.ApplyItem(sec, next, EditLogic.ResolveItem("", " ", true, false));
        Assert.Equal(new[] { "1", "2" }, sec.Items.ConvertAll(i => i.Text));
    }
}
