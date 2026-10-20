using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>The user's own "Custom" theme: a start theme (any built-in one) plus the colours the user picked.
    /// Each user-facing colour drives a group of theme tokens; every token keeps the start theme's transparency
    /// (alpha), so glassy panels stay glassy. UI-free and pure, so it is unit tested.</summary>
    public static class CustomTheme
    {
        public const string Id = "custom";
        public const string Name = "Custom";

        /// <summary>A user-facing colour: settings key, label, tooltip and the theme tokens it sets (first = the one shown).</summary>
        public sealed record Group(string Key, string Label, string Tip, string[] Tokens);

        public static readonly Group[] Groups =
        {
            new("windowBg", "Background", "Window and menu background", new[] { "panelBg", "menuBg", "soonInsetBg", "thumbStroke" }),
            new("cardBg", "Buttons & cards", "Buttons, launcher cards, badges and fields", new[] { "buttonBg", "rowHover", "badgeBg", "fieldBg" }),
            new("hoverBg", "Hover", "Highlight under the mouse", new[] { "buttonHoverBg", "menuHover", "iconHoverBg" }),
            new("border", "Borders", "Window, button, menu and checkbox borders", new[] { "panelBorder", "buttonBorder", "menuBorder", "checkBoxOutline", "scrollThumb" }),
            new("divider", "Dividers", "Divider lines and slider tracks", new[] { "divider", "sliderTrack" }),
            new("title", "Titles", "Window titles and section headings", new[] { "title", "subTitle", "soonTitle", "menuCheck" }),
            new("accent", "Accent", "Selected pills, links, focus and slider handles", new[] { "pillOnBorder", "pillOnBg", "pillOnText", "link", "focus", "buttonHoverBorder", "thumbHover", "primaryBorder", "primaryBg", "primaryHover" }),
            new("text", "Main text", "Most text and the close/hover icons", new[] { "text", "caret", "iconHover", "menuSep", "menuDisabled", "grip", "gripHover" }),
            new("muted", "Secondary text", "Second lines, hints and small icons", new[] { "muted", "faint", "iconStroke", "soonInsetText", "versionText" }),
            new("live", "Countdown", "Alert window border and text while an event is live", new[] { "live", "liveText", "onBorder", "onBg" }),
            new("counter", "Counters", "Checklist counters like 3/5", new[] { "counter", "selection" }),
            new("check", "Checkmarks", "Checklist checkmarks", new[] { "check" }),
            new("struck", "Done items", "Text of checked-off items", new[] { "struck" }),
            new("primaryText", "Button text", "Text on highlighted (primary) buttons", new[] { "primaryText" }),
        };

        public static Group? Find(string key) => Groups.FirstOrDefault(g => g.Key == key);

        /// <summary>"#RRGGBB" (upper case) of the colour a group shows in a theme.</summary>
        public static string ColorOf(ThemeSet.Theme t, Group g) =>
            t.Colors.TryGetValue(g.Tokens[0], out var h) && h.Length == 9 ? "#" + h[3..] : "#FF00FF";

        /// <summary>Every group's colour in a theme ("Start from current theme").</summary>
        public static Dictionary<string, string> Snapshot(ThemeSet.Theme t) => Groups.ToDictionary(g => g.Key, g => ColorOf(t, g));

        /// <summary>The Custom theme: the start theme with the picked colours (invalid or unknown picks are ignored).</summary>
        public static ThemeSet.Theme Build(ThemeSet.Theme start, IReadOnlyDictionary<string, string>? picks)
        {
            var colors = new Dictionary<string, string>(start.Colors, StringComparer.Ordinal);
            if (picks != null)
                foreach (var g in Groups)
                {
                    if (!picks.TryGetValue(g.Key, out var hex) || WarnColors.TryParse(hex) == null) continue;
                    var rgb = WarnColors.Normalize(hex)[1..];
                    foreach (var token in g.Tokens)
                    {
                        var alpha = colors.TryGetValue(token, out var old) && old.Length == 9 ? old.Substring(1, 2) : "FF";
                        colors[token] = "#" + alpha + rgb;
                    }
                }
            return new ThemeSet.Theme(Id, Name, "Your own colors, starting from " + start.Name, colors);
        }
    }
}
