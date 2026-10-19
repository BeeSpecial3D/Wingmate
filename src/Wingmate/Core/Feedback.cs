using System;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>"Send feedback": a prefilled new GitHub issue (pure, so it is unit tested).</summary>
    public static class Feedback
    {
        public const string NewIssueUrl = "https://github.com/BeeSpecial3D/Wingmate/issues/new";

        /// <summary>Issue body with the versions filled in and short headings for the user to answer.</summary>
        public static string Body(string? appVersion, string? scheduleVersion, string? windowsVersion) =>
            $"**Wingmate:** {Clean(appVersion)}\n**Schedule:** {Clean(scheduleVersion)}\n**Windows:** {Clean(windowsVersion)}\n\n" +
            "### What happened\n\n\n### What did you expect\n\n\n### Steps to reproduce (optional)\n\n";

        /// <summary>{NewIssueUrl}?body=&lt;URL-encoded body&gt;. Stays well under browser/GitHub URL limits.</summary>
        public static string IssueUrl(string? appVersion, string? scheduleVersion, string? windowsVersion) =>
            NewIssueUrl + "?body=" + Uri.EscapeDataString(Body(appVersion, scheduleVersion, windowsVersion));

        /// <summary>One short line: no newlines/markdown control characters, max 40 chars, "unknown" when empty.</summary>
        private static string Clean(string? s)
        {
            var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (t.Length > 40) t = t[..40];
            return t.Length == 0 ? "unknown" : t;
        }
    }
}
