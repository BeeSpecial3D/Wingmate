using System.Linq;
using Wingmate.Core;
using Xunit;

public class WelcomeGuideTests
{
    static string All => string.Join(" ", WelcomeGuide.Pages.SelectMany(p => p.Lines.Prepend(p.Title)));

    [Fact] public void ShortAndComplete()
    {
        Assert.InRange(WelcomeGuide.Pages.Count, 3, 7);
        Assert.All(WelcomeGuide.Pages, p => Assert.InRange(p.Lines.Count, 1, 5));
        foreach (var topic in new[] { "Abyss alerts", "Checklist", "Guide", "class", "add a new one", "+ New list", "edit", "Undo",
                                      "Events list", "kept up to date by Wingmate", "padlock", "scrolls", "checkboxes", "Right-click", "theme", "text size", "opacity", "tray", "custom alert" })
            Assert.Contains(topic, All, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void NoEasterEggNames()
    {
        foreach (var w in new[] { "Test", "Botch", "Ezio" }) Assert.DoesNotContain(w, All);
    }
}
