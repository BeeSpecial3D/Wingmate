using System;
using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>Texts and the "once per version per session" rule of the update pop-up (pure, so it is unit tested).</summary>
    public sealed class UpdatePrompt
    {
        /// <summary>Command-line flag that shows the pop-up with sample text and never downloads or changes anything.</summary>
        public const string PreviewFlag = "--preview-update-popup";
        public const string PreviewNew = "1.0.3", PreviewCurrent = "1.0.2";

        private readonly HashSet<string> _shown = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>True the first time a given version is offered this session; false for empty or already-offered versions.</summary>
        public bool ShouldShow(string? version)
        {
            var v = Clean(version);
            return v.Length > 0 && _shown.Add(v);
        }

        public static bool IsPreview(IEnumerable<string>? args)
            => args != null && args.Any(a => string.Equals(a?.Trim(), PreviewFlag, StringComparison.OrdinalIgnoreCase));

        public static string Title(string? newVersion) => $"Wingmate {Clean(newVersion)} is available";
        public static string Subtitle(string? currentVersion)
            => string.IsNullOrWhiteSpace(currentVersion) ? "A new version of Wingmate is ready to install." : $"You're on {Clean(currentVersion)}.";
        public static string Downloading(int percent)
            => percent <= 0 ? "Downloading\u2026" : $"Downloading\u2026 {Math.Clamp(percent, 0, 100)}%";
        public const string Failed = "Couldn't download the update. Check your internet connection and try again. Your lists and settings are untouched.";
        public const string Restarting = "Installing and restarting\u2026 Your lists and settings stay as they are.";
        public const string PreviewDone = "Preview only, nothing was installed.";

        /// <summary>"1.0.3+abc" / " v1.0.3 " -> "1.0.3".</summary>
        public static string Clean(string? version)
        {
            var v = (version ?? "").Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v[1..];
            int plus = v.IndexOf('+');
            return plus >= 0 ? v[..plus] : v;
        }
    }
}
