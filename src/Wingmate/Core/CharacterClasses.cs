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

        /// <summary>The class's guide page, or the site's class index when the class is empty or unknown.</summary>
        public static string GuideUrlFor(string? name)
        {
            var n = Normalize(name);
            return All.FirstOrDefault(c => c.Name == n)?.GuideUrl ?? IndexUrl;
        }
    }
}
