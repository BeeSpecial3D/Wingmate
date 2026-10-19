using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Wingmate.Core
{
    /// <summary>App-wide colour themes from themes.json (assets/themes, embedded). UI-free: resolves every token to "#AARRGGBB".</summary>
    public sealed class ThemeSet
    {
        public const string DefaultId = "current";

        /// <summary>Every token the app uses. Missing ones come from <see cref="Fallbacks"/>.</summary>
        public static readonly string[] Tokens =
        {
            "panelBg", "panelBorder", "divider", "title", "text", "muted", "faint", "struck",
            "buttonBg", "buttonBorder", "buttonHoverBg", "buttonHoverBorder", "iconHoverBg", "iconStroke", "iconHover",
            "pillOnBg", "pillOnBorder", "pillOnText", "badgeBg", "link", "check", "checkBoxOutline", "counter", "live", "liveText",
            "menuBg", "menuBorder", "menuHover", "menuSep", "menuDisabled", "menuCheck",
            "soonTitle", "soonIcon", "soonInsetBg", "soonInsetText", "onBorder", "onBg",
            "grip", "gripHover", "versionText", "scrollThumb", "sliderTrack", "focus", "thumbHover", "thumbStroke",
            "inputBg", "fieldBg", "caret", "selection", "subTitle", "error", "primaryBorder", "primaryBg", "primaryHover", "primaryText",
            "rowHover", "gemOutline", "warn",
        };

        /// <summary>token -> other token, "token@AA" (same colour, new alpha) or a literal "#AARRGGBB".</summary>
        public static readonly Dictionary<string, string> Fallbacks = new()
        {
            ["menuSep"] = "text@33", ["menuDisabled"] = "text@77", ["menuCheck"] = "title",
            ["soonTitle"] = "title", ["soonIcon"] = "#E6FFFFFF", ["soonInsetBg"] = "panelBg@CC", ["soonInsetText"] = "muted",
            ["onBorder"] = "live", ["onBg"] = "live@33",
            ["grip"] = "text@66", ["gripHover"] = "text@CC", ["versionText"] = "text@66", ["scrollThumb"] = "panelBorder", ["sliderTrack"] = "divider",
            ["focus"] = "pillOnBorder", ["thumbHover"] = "focus", ["thumbStroke"] = "panelBg@FF",
            ["inputBg"] = "#55000000", ["fieldBg"] = "#22FFFFFF", ["caret"] = "text", ["selection"] = "counter", ["subTitle"] = "title@CC",
            ["error"] = "#FFFF9A9A", ["primaryBorder"] = "title@CC", ["primaryBg"] = "title@33", ["primaryHover"] = "title@55", ["primaryText"] = "pillOnText",
            ["rowHover"] = "buttonBg", ["gemOutline"] = "#CCFFFFFF", ["warn"] = "#FFFF1E1E", // warn: default alert warning colour (red)
            ["menuBg"] = "panelBg", ["menuBorder"] = "panelBorder", ["menuHover"] = "buttonHoverBg",
        };

        public sealed record Theme(string Id, string Name, string Description, IReadOnlyDictionary<string, string> Colors);

        public IReadOnlyList<Theme> All { get; }

        private ThemeSet(List<Theme> all) { All = all; }

        public Theme Default => All.First(t => t.Id == DefaultId);

        /// <summary>The theme with this id; unknown, missing or empty -> Current (Default).</summary>
        public Theme Get(string? id) =>
            All.FirstOrDefault(t => string.Equals(t.Id, (id ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;

        public static ThemeSet Parse(string json)
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var list = new List<Theme>();
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Name.StartsWith("_") || p.Value.ValueKind != JsonValueKind.Object) continue;
                var raw = new Dictionary<string, string>(StringComparer.Ordinal);
                string name = p.Name, desc = "";
                foreach (var f in p.Value.EnumerateObject())
                {
                    if (f.Value.ValueKind != JsonValueKind.String) continue;
                    var s = f.Value.GetString() ?? "";
                    if (f.Name == "name") name = s; else if (f.Name == "desc") desc = s; else raw[f.Name] = s;
                }
                list.Add(new Theme(p.Name, name, desc, Resolve(raw)));
            }
            if (!list.Any(t => t.Id == DefaultId)) throw new InvalidOperationException("themes.json has no 'current' theme");
            return new ThemeSet(list);
        }

        /// <summary>Fills every token: own value if it is a valid colour, else the fallback chain, else Current's built-in look.</summary>
        private static Dictionary<string, string> Resolve(Dictionary<string, string> raw)
        {
            var done = new Dictionary<string, string>(StringComparer.Ordinal);
            string Get(string token, int depth)
            {
                if (done.TryGetValue(token, out var v)) return v;
                if (depth > 12) return "#FFFF00FF";
                string? result = raw.TryGetValue(token, out var own) ? Normalize(own) : null;
                if (result == null && Fallbacks.TryGetValue(token, out var fb)) result = Eval(fb, depth);
                result ??= "#FFFF00FF"; // obviously wrong (magenta) rather than silently invisible
                done[token] = result;
                return result;
            }
            string Eval(string expr, int depth)
            {
                if (expr.StartsWith("#")) return Normalize(expr) ?? "#FFFF00FF";
                var at = expr.IndexOf('@');
                if (at < 0) return Get(expr, depth + 1);
                var baseHex = Get(expr[..at], depth + 1);
                return "#" + expr[(at + 1)..].ToUpperInvariant() + baseHex[3..];
            }
            foreach (var t in Tokens) Get(t, 0);
            return done;
        }

        /// <summary>"#RRGGBB" or "#AARRGGBB" -> "#AARRGGBB" (upper case); anything else -> null.</summary>
        public static string? Normalize(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            var h = hex.Trim().TrimStart('#');
            if (h.Length == 6) h = "FF" + h;
            if (h.Length != 8 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return null;
            return "#" + h.ToUpperInvariant();
        }

        /// <summary>Light themes (dark text) need a few different choices, e.g. the alert title on the red warning panel.</summary>
        public static bool IsLight(Theme t)
        {
            var h = t.Colors["panelBg"];
            int r = Convert.ToInt32(h.Substring(3, 2), 16), g = Convert.ToInt32(h.Substring(5, 2), 16), b = Convert.ToInt32(h.Substring(7, 2), 16);
            return 0.299 * r + 0.587 * g + 0.114 * b > 150;
        }
    }
}
