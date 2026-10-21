using System.Collections.Generic;

namespace Wingmate.Core
{
    /// <summary>The text of the one-time welcome guide (shown on first open, and again from Settings, About tab).</summary>
    public static class WelcomeGuide
    {
        public sealed record Page(string Title, IReadOnlyList<string> Lines);

        public static readonly IReadOnlyList<Page> Pages = new[]
        {
            new Page("Welcome to Wingmate", new[]
            {
                "Wingmate keeps Abyss event alerts and your daily and weekly checklist on top of Aion 2. It never touches the game itself.",
                "This short guide covers the basics. You can open it again any time from Settings, on the About tab.",
            }),
            new Page("The launcher", new[]
            {
                "Abyss alerts opens the alert window: the next events with a countdown, and a spoken warning before each one starts.",
                "Checklist opens your dailies, weeklies, events and your own lists.",
                "Click your character's name to switch characters or add a new one. Pick your class next to it, and Guide opens a guide for that class in your browser.",
                "The gear button opens Settings.",
            }),
            new Page("Your checklist", new[]
            {
                "Tick an item when it's done. It slides away, and \"Checked \u2026 \u00b7 Undo\" shows for a few seconds in case you misclicked.",
                "Lists reset on their own at the daily and weekly reset.",
                "Right-click a list to add an item, or an item to edit (rename), move or remove it. \"+ New list\" makes a list of your own. Each character has their own lists.",
                "The Events list (Shugo Festival, Artifact Siege and the like) is kept up to date by Wingmate: each item resets on its own schedule, and limited-time events disappear when they end. You can check items off, but not edit this list.",
            }),
            new Page("Alerts", new[]
            {
                "In Settings, on the Alerts tab, pick your region, how many minutes before an event to warn you, and which events you want alerts for.",
                "\"Add custom alert\u2026\" lets you add your own alert with a name, a time, the days it runs and an optional repeat.",
            }),
            new Page("Locking a window", new[]
            {
                "Click the padlock on the alert or checklist window to lock it. Clicks then go through it to the game.",
                "A locked checklist still scrolls with the mouse wheel, and its checkboxes and Undo still work. Clicks anywhere else on the list do nothing.",
                "Click the padlock again to unlock. You can also set a lock hotkey in Settings.",
            }),
            new Page("Menus, Settings and the tray", new[]
            {
                "Right-click any Wingmate window for more options.",
                "Settings has the theme, text size and window opacity on the Appearance tab.",
                "Hiding the launcher keeps Wingmate running in the tray, by the clock. Click the tray icon to bring the launcher back, or right-click it for more, including Exit.",
            }),
        };
    }
}
