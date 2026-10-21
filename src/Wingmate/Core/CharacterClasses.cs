using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>The Aion 2 classes and their guide pages on AION 2 Global Guides (aion2-guides.info).
    /// This table is the only place the class list and the links live; every URL was checked against the site's own navigation.</summary>
    public static class CharacterClasses
    {
        public sealed record Info(string Name, string GuideUrl);

        public const string GuideSite = "AION 2 Global Guides";
        public const string GuideHost = "aion2-guides.info";
        /// <summary>The site's English start page, which lists every class; used when a class has no page of its own.</summary>
        public const string IndexUrl = "https://aion2-guides.info/en/";
        public const string Credit = "Class guides by " + GuideSite + " (" + GuideHost + ")";

        public static readonly IReadOnlyList<Info> All = new[]
        {
            new Info("Gladiator",    "https://aion2-guides.info/en/gladiator/"),
            new Info("Templar",      "https://aion2-guides.info/en/templar/"),
            new Info("Assassin",     "https://aion2-guides.info/en/assassin/"),
            new Info("Ranger",       "https://aion2-guides.info/en/ranger/"),
            new Info("Sorcerer",     "https://aion2-guides.info/en/sorcerer/"),
            new Info("Spiritmaster", "https://aion2-guides.info/en/elementalist/"), // the site calls the page "elementalist"
            new Info("Cleric",       "https://aion2-guides.info/en/cleric/"),
            new Info("Chanter",      "https://aion2-guides.info/en/chanter/"),
        };

        public static IEnumerable<string> Names => All.Select(c => c.Name);

        /// <summary>The class's proper name for any spelling or casing of it; "" for empty or unknown.</summary>
        public static string Normalize(string? name)
        {
            var n = (name ?? "").Trim();
            return All.FirstOrDefault(c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase))?.Name ?? "";
        }

        public static bool IsKnown(string? name) => Normalize(name).Length > 0;

        public const string NoClassLabel = "No class";

        /// <summary>One entry of a right-click "Class" submenu: the label, the class it sets ("" = none) and whether it is ticked.</summary>
        public sealed record MenuEntry(string Label, string Value, bool IsChecked, bool SeparatorBefore = false);

        /// <summary>The right-click "Class" submenu: the eight classes, then "No class". The character's class is ticked;
        /// "No class" is ticked when it has none. A class this version doesn't know (e.g. from a newer version) ticks nothing.</summary>
        public static IReadOnlyList<MenuEntry> MenuEntries(string? current)
        {
            var n = Normalize(current);
            bool none = string.IsNullOrWhiteSpace(current);
            var list = All.Select(c => new MenuEntry(c.Name, c.Name, c.Name == n)).ToList();
            list.Add(new MenuEntry(NoClassLabel, "", none, SeparatorBefore: true));
            return list;
        }

        /// <summary>The class's guide page, or the site's class index when the class is empty or unknown.</summary>
        public static string GuideUrlFor(string? name)
        {
            var n = Normalize(name);
            return All.FirstOrDefault(c => c.Name == n)?.GuideUrl ?? IndexUrl;
        }
    }
}
